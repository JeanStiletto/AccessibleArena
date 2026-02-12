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

### Text input: 3-tier approach
Typing into form fields uses three approaches in order, because different payment forms respond to different methods:

1. **`document.execCommand('insertText')`** — Works for most standard forms (Xsolla login, PayPal login). Same API that Puppeteer/Playwright use. Triggers React/Angular/Vue change events natively.

2. **Full keyboard event sequence** (per character) — Dispatches `keydown`, `keypress`, `InputEvent` (with `data`/`inputType`), `keyup` for each character. Works for masked input components (card number fields, date fields) that listen for individual keystroke events.

3. **Direct `el.value` set + React-compatible `InputEvent`** — Fallback. Sets value directly and dispatches `InputEvent` with `inputType: 'insertText'` plus a `change` event.

The script returns which method succeeded (e.g. `execCommand:4242`, `keyboard_events:4242`, `all_failed:`) for debugging.

**Approaches that don't work:**
- **browser.TypeText() / browser.PressKey()** - Depend on ZFBrowser's `KeyboardHasFocus` being true. Since our mod intercepts all keyboard input before ZFBrowser sees it, the browser never gets keyboard focus.
- **Native value setter via Object.getOwnPropertyDescriptor** - Fails on cross-iframe elements due to cross-realm prototype mismatch ("Illegal invocation"). CEF's iframe `defaultView` returns null.

**CRITICAL:** Never try to use `Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')` on elements that might be from iframes.

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
- Printable keys - Append text (3-tier: execCommand, keyboard events, direct value)
- Backspace - Delete last character (3-tier approach)
- Arrow Up/Down - Read full field content (live from JS)
- Arrow Left/Right - Read character at cursor position
- Enter - Submit form, exit edit mode
- Escape - Exit edit mode (blocked from reaching game)
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

## CAPTCHA Detection

PayPal's security step-up flow loads a visual CAPTCHA (Arkose Labs FunCaptcha) in a cross-origin iframe. This is completely inaccessible to blind users — there is no audio alternative.

**Detection:** After 3 consecutive empty rescans (~4.5 seconds), the mod:
1. Checks the browser URL for keywords: `authflow`, `challenge`, `captcha`, `stepup`, `security`, `verify`
2. Runs a JS script to detect cross-origin iframes (iframes where `contentDocument` throws or returns null)
3. If either indicator is found, announces a warning and stops the rescan loop

**Warning message:** "Security verification detected. This is a visual CAPTCHA that cannot be solved without sight. PayPal does not provide an audio alternative. Please ask someone for sighted assistance, or press Backspace to go back and try a different payment method."

The detection resets on new page loads, so normal operation resumes after navigating past the CAPTCHA.

## Escape Key Blocking

While the web browser is active, Escape is blocked from reaching the game via `KeyboardManagerPatch.BlockEscape`. This prevents the settings menu from opening when Escape is used to exit edit mode. The flag is set on `Activate()` and cleared on `Deactivate()`.

## Live Field Value Reading

When navigating to a text field (Up/Down/Tab), the mod live-reads the current value from JS via `ReadValueScript` instead of using the cached value from extraction time. This ensures fields that were filled in are announced correctly (e.g. "Kartennummer, text field, 4242 4242 4242 4242" instead of "empty").

## Known Limitations

- **Cross-origin iframes**: Elements inside cross-origin iframes (e.g. bank 3D-Secure frames, PayPal CAPTCHA) cannot be accessed. The extraction script silently skips them. CAPTCHA detection warns the user.
- **Shadow DOM**: Elements inside Shadow DOM are not scanned. Not currently encountered in Xsolla/PayPal.
- **Dropdowns**: HTML `<select>` elements are clicked to open, but native Chromium dropdown rendering is inaccessible. Arrow keys work inside the opened dropdown but items are not announced.
- **execCommand deprecation**: `document.execCommand` is deprecated but still works in all Chromium versions. If it stops working, the keyboard event and direct value fallbacks take over.
- **Password field value reading**: `el.value` on password fields returns the actual value. We only announce the character count (full read) or "star" (per-character) for security.
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
- `Typing 'X' into element N: ...` - Character being sent to JS
- `TypeText result: method:value` - Which input method worked (execCommand/keyboard_events/direct_value/all_failed)
- `TypeText error: ...` / `Backspace error: ...` - Input script errors
- `Checking for CAPTCHA / security verification...` - CAPTCHA detection triggered
- `CAPTCHA detected! Stopping rescan loop.` - CAPTCHA confirmed

## Related Files

- `src/Core/Services/WebBrowserAccessibility.cs` - Main implementation
- `src/Core/Services/StoreNavigator.cs` - Integration (lines 60-61, 265-285, 302-310, 896-902, 946-948)
- `libs/ZFBrowser.dll` - ZenFulcrum.EmbeddedBrowser library reference
- `src/Core/Services/PanelDetection/AlphaPanelDetector.cs` - Detects browser canvas via alpha transition
- `docs/PAYMENT_POPUP_INVESTIGATION.md` - Investigation that led to this implementation
