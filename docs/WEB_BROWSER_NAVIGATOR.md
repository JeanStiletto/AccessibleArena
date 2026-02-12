# Web Browser Accessibility (Payment Popup)

## Overview

`WebBrowserAccessibility` provides full keyboard navigation and screen reader support for the embedded Chromium browser (ZFBrowser) that opens when the user clicks "Change payment method" in the Store. It extracts all page elements via injected JavaScript, presents them as a flat navigable list, and allows clicking buttons, typing into form fields, and navigating between pages.

The browser popup hosts third-party payment pages (Xsolla, PayPal, etc.) that are completely inaccessible without this system.

## Architecture

**WebBrowserAccessibility** (`src/Core/Services/WebBrowserAccessibility.cs`)
- Self-contained helper class, not a standalone navigator
- StoreNavigator owns it, delegates to it when ZFBrowser popup is detected
- Communicates with the browser via `EvalJSCSP()` (JavaScript injection)

**StoreNavigator integration** (`src/Core/Services/StoreNavigator.cs`)
- `IsWebBrowserPanel()` detects panels containing a `ZenFulcrum.EmbeddedBrowser.Browser` component
- `OnPanelChanged` activates WebBrowserAccessibility before checking for regular popups
- `HandleStoreInput` delegates to `_webBrowser.HandleInput()` when active
- `Update` calls `_webBrowser.Update()` for rescan timers and timeout checks
- `OnDeactivating` calls `_webBrowser.Deactivate()`

**Panel detection chain:**
Store "Change payment method" -> `OnButton_PaymentSetup()` via reflection -> `FullscreenZFBrowserCanvas(Clone)` opens -> AlphaPanelDetector detects alpha transition -> PanelStateManager fires OnPanelChanged -> StoreNavigator detects browser component -> WebBrowserAccessibility activates

## How Element Extraction Works

A JavaScript extraction script is injected via `browser.EvalJSCSP()`. It:

1. Queries all potentially navigable elements (buttons, links, inputs, headings, text blocks, ARIA-role elements)
2. Filters out invisible elements (display:none, zero size, opacity 0)
3. Skips text nodes that are children of interactive elements (avoids double-announcing)
4. Deduplicates identical non-interactive text elements
5. Tags each element with a `data-aa-idx="N"` attribute for reliable re-targeting
6. Recursively scans same-origin iframes via `iframe.contentDocument`
7. Returns a JSON array with tag, text, role, inputType, value, index, etc.

The "Back to Arena" Unity button (outside the browser) is appended as the last element.

## Key Design Decisions

### Why EvalJSCSP instead of EvalJS
Xsolla's Content-Security-Policy blocks `unsafe-eval`. `EvalJS` uses `eval()` internally, which is blocked. `EvalJSCSP` wraps the script in an IIFE (Immediately Invoked Function Expression) instead, which bypasses CSP restrictions.

**CRITICAL:** Always use `EvalJSCSP`, never `EvalJS`, for any script that runs on payment pages.

### Why execCommand for text input
We tried three approaches for typing into form fields:

1. **browser.TypeText() / browser.PressKey()** - Failed because these depend on ZFBrowser's `KeyboardHasFocus` being true. Since our mod intercepts all keyboard input before ZFBrowser sees it, the browser never gets keyboard focus.

2. **Native value setter via Object.getOwnPropertyDescriptor** - Failed on cross-iframe elements. When `findEl()` returns an element from an iframe, calling `HTMLInputElement.prototype.value.set.call(el, ...)` with a setter from the wrong JavaScript realm throws "Illegal invocation". Even using `el.ownerDocument.defaultView` to get the correct realm failed — likely because CEF's iframe `defaultView` returns null or has other cross-realm quirks.

3. **document.execCommand('insertText')** (current approach) - Works reliably across all pages and iframes. This is the same API that browser automation tools (Puppeteer, Playwright) use. It triggers all the correct React/Angular/Vue change events natively. Falls back to direct `el.value` assignment if `execCommand` fails.

**CRITICAL:** Never try to use `Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')` on elements that might be from iframes. The cross-realm prototype mismatch causes "Illegal invocation" errors that are extremely hard to debug.

### Why iframe scanning is needed
The Xsolla payment page (main document) loads its actual content inside same-origin iframes. Without recursive iframe scanning, only the "Back to Arena" button is found (0 web elements). The extraction script and `findEl()` helper both search iframes.

### Why extraction sometimes fails silently
The `EvalJSCSP` Promise can fail to resolve when:
- The page is in a transitional state (iframes loading/unloading)
- The extraction script modifies the DOM (`data-aa-idx` attributes) which triggers framework re-renders
- CEF drops the script evaluation during navigation

**Safeguards:**
- 5-second extraction timeout in `Update()` resets `_isLoading` and schedules a retry
- Master try-catch around `scanDocument(document)` ensures the script always returns
- `_isLoading` guard prevents concurrent extractions
- Page load events cancel pending rescan timers from previous pages

### Why rescans after clicks
After clicking a button/link, the page content often changes (new form, new page section). Since we can't reliably detect all DOM mutations, we schedule timed rescans:
- **1.2s** after button/link clicks (first rescan)
- **3.0s** after button/link clicks (second rescan, catches slow transitions)
- **0.3s** after checkbox/radio clicks (quick state change)
- **1.5s** retry when 0 web elements found (iframes still loading)

Silent rescan: if the element count hasn't changed, the rescan result is discarded silently (no re-announcement).

### Why IsReady instead of IsLoaded
`browser.IsLoaded` returns false whenever any iframe on the page is still loading. Since payment pages have multiple iframes loading at different times, `IsLoaded` is unreliable — it would keep the browser in "loading" state indefinitely. `browser.IsReady` only checks if the native browser ID exists, which is sufficient for our purposes.

## Keyboard Controls

**Navigation mode:**
- Up/Down, W/S, Tab/Shift+Tab - Navigate elements
- Home/End - Jump to first/last element
- Enter - Activate element (click button, enter edit mode for inputs)
- Space - Activate buttons/links/checkboxes (not text fields)
- Backspace - Click "Back to Arena" (exit browser)

**Edit mode (text fields):**
- Printable keys - Append text via execCommand
- Backspace - Delete last character via execCommand
- Arrow Up/Down - Read current field value
- Arrow Left/Right - Read current field value
- Enter - Submit form, exit edit mode
- Escape - Exit edit mode
- Tab/Shift+Tab - Exit edit mode, move to next/previous element

## Announcements

- Activation: "Payment page. {N} elements."
- Elements: "{pos} of {count}: {text}, {role}" (e.g. "3 of 12: Email, email field, empty")
- Password fields: show character count instead of value
- Checkboxes: append ", checked" or ", unchecked"
- Text/heading elements: omit role suffix (just announce content)
- Edit mode: "Editing {name}, {type}. Type to enter text, Escape to exit."
- Page load: "Page loaded. Reading elements..."
- Loading: "Payment page loading..."

## Known Limitations

- **Cross-origin iframes**: Elements inside cross-origin iframes (e.g. bank 3D-Secure frames) cannot be accessed. The extraction script silently skips them.
- **Shadow DOM**: Elements inside Shadow DOM are not scanned. Not currently encountered in Xsolla/PayPal.
- **Dropdowns**: HTML `<select>` elements are clicked to open, but native Chromium dropdown rendering is inaccessible. Arrow keys work inside the opened dropdown but items are not announced.
- **execCommand deprecation**: `document.execCommand` is deprecated but still works in all Chromium versions. If it stops working in a future CEF update, the fallback (direct value assignment) will take over, though it may not trigger framework change handlers on some pages.
- **Password field value reading**: `el.value` on password fields returns the actual value. We only announce the character count for security.
- **Page wipe on repeated extraction**: Running the extraction script too many times in quick succession can trigger some frameworks' mutation observers, causing them to re-render and wipe the page. The concurrent-extraction guard and timer cancellation on page load prevent this.

## Debugging

Log prefix: `[WebBrowser]`

Key log messages:
- `Activated. Browser URL: ..., IsLoaded: ...` - Browser found and activated
- `Page loaded: ...` - Full page navigation detected
- `Extracting page elements...` - Extraction script injected
- `Extracted N elements (M from page)` - Extraction succeeded
- `No web elements found, scheduling rescan` - Iframes not loaded yet
- `Element count unchanged, silent rescan` - Rescan found same elements
- `Extraction timed out, resetting` - Promise never resolved, retrying
- `Extraction already in progress, skipping` - Concurrent extraction prevented
- `TypeText error: ...` / `Backspace error: ...` - Input script errors

## Related Files

- `src/Core/Services/WebBrowserAccessibility.cs` - Main implementation
- `src/Core/Services/StoreNavigator.cs` - Integration (lines 60-61, 265-285, 302-310, 896-902, 946-948)
- `libs/ZFBrowser.dll` - ZenFulcrum.EmbeddedBrowser library reference
- `src/Core/Services/PanelDetection/AlphaPanelDetector.cs` - Detects browser canvas via alpha transition
- `docs/PAYMENT_POPUP_INVESTIGATION.md` - Investigation that led to this implementation
