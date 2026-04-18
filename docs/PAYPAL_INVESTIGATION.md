# PayPal / CEF Web Browser Input Investigation

Working notes for the embedded-browser keyboard input problem. Keep this doc
when touching `src/Core/Services/WebBrowserAccessibility.cs` — it summarises
what has been tried, which theories held up, and the fallback code paths.

## Context

MTGA opens login / payment flows in an embedded ZenFulcrum `Browser`
(`ZFBrowser.dll`, wrapping CEF). For blind users, `WebBrowserAccessibility`
extracts interactive elements via JS (`data-aa-idx` attribute), announces them,
and routes arrow / Tab / Enter keys into the page.

For plain forms (username, search, etc.) the JS path works: we update
`input.value`, dispatch synthetic `input` / `change` events, and React's
controlled components accept it. **PayPal rejects this.** The final
`password` field either silently drops the value or — when we set it via
`el.value = …` — PayPal's React layer reverts it. Login returns to
`/signin/return?flowFrom=anw-stepup&failedBecause=invalid_input&YWRzZGRjYXB0Y2hh=1`
(base64 `adsddcaptcha`), i.e. bot-protection rejection.

Root cause: PayPal's password field is wrapped in a React component that
relies on `isTrusted=true` keyboard events from the OS / CEF — not on
scripted DOM writes. Any event dispatched from page JS has `isTrusted=false`
and is filtered.

## Paths Explored

### Path A — Native CEF mouse click + `Browser.TypeText` (abandoned, code retained)

Theory: if we can inject a *trusted* mouse click at the field's CEF-surface
coordinates, CEF moves top-level focus onto the correct iframe's input,
and then `Browser.TypeText()` (which queues `zfb_characterEvent` / `zfb_keyEvent`
through the native delegates) arrives as `isTrusted=true` keystrokes.

Implementation sketch (still present in code as fallback — `_useNativeInput`,
`SimulateNativeClickThenType`, `FireNativeMouseClick`, `GetBoundingBoxScript`,
`TryParseBBox`, `_pendingPostClickType`):

1. Detect JS input failure from the `AppendTextScript` return string
   (`execCommand:` empty, or `all_failed:`).
2. Query the element's bbox via `GetBoundingBoxScript(index)`, which walks
   up `frameElement` chains so coordinates land on the CEF top surface:
   ```js
   var r = el.getBoundingClientRect();
   var x = r.left + r.width / 2, y = r.top + r.height / 2;
   var w = el.ownerDocument.defaultView;
   while (w && w !== window.top) {
       var fe = w.frameElement;
       if (!fe) break;
       var fr = fe.getBoundingClientRect();
       x += fr.left; y += fr.top;
       w = fe.ownerDocument.defaultView;
   }
   return x + ',' + y + ',' + window.top.innerWidth + ',' + window.top.innerHeight;
   ```
3. Normalise to 0..1 and fire:
   ```csharp
   BrowserNative.zfb_mouseMove(id, nx, ny);
   BrowserNative.zfb_mouseButton(id, BrowserNative.MouseButton.MBT_LEFT, true,  1);
   BrowserNative.zfb_mouseButton(id, BrowserNative.MouseButton.MBT_LEFT, false, 1);
   ```
4. Defer `Browser.TypeText(chars)` by one `Update()` tick
   (`_pendingPostClickType` is flushed at the top of `Update()`), so CEF has
   finished processing the click before the keystrokes arrive.

`Browser.browserId` is `protected internal int`, reachable only via
reflection (`_browserIdField`, `BindingFlags.NonPublic | Instance`). The
native delegate fields on `BrowserNative` are `public static`.

Why it failed on PayPal:

- `el.getBoundingClientRect()` returned `(0,0)` for the password field.
  The field sits inside a nested iframe *or* React had swapped the DOM
  node, leaving `data-aa-idx` on a detached element that reports zero size.
  Click therefore landed at the top-left corner of the browser surface.
- Even when the bbox lookup worked on other inputs, `Browser.TypeText`'s
  queued events (`BrowserInput.extraEventsToInject`) are only flushed by
  `BrowserInput.HandleInput` when `browser.UIHandler.KeyboardHasFocus` is
  true — and we had deliberately set `PointerUIGUI.enableInput = false`
  during edit mode to avoid double delivery. **So the queued characters
  were never processed.** This is the key insight that made Path A
  architecturally wrong without also keeping the forwarder enabled.

Code still lives in `WebBrowserAccessibility.cs` (search for
`_useNativeInput`, `FireNativeMouseClick`, `SimulateNativeClickThenType`,
`GetBoundingBoxScript`, `TryParseBBox`, `_pendingPostClickType`,
`GetBrowserId`). If Path B fails on another site it may still be useful,
but the `enableInput`-during-typing issue must be fixed first.

### Path B — Passthrough typing mode (current implementation)

Theory: don't try to inject keystrokes at all. When the user is editing a
password field, let Unity's regular input pipeline forward keys to
`PointerUIGUI.OnGUI` → `keyEvents.Feed(Event.current)` → `BrowserInput.HandleKeyInput`
→ `BrowserNative.zfb_keyEvent`. That path produces `isTrusted=true` events
because CEF treats them as OS-originated.

Trade-off accepted by the user: we lose per-character echo during typing on
password fields. Arrow keys still read the stored value back via JS.

Activation criteria — in the extraction JS, any `<input>` is classified as a
password field when either:

- `el.type === 'password'`, **or**
- `el.autocomplete` is `current-password` or `new-password` (PayPal uses
  `type='text'` + custom masking on the visible password field).

`EnterEditMode` picks passthrough when *either* condition holds:

- `elem.InputType == "password"`, **or**
- `IsPayPalLoginPage(_browser?.Url)` is true and the input type is
  `email` / `text` / `tel`. The URL check matches `paypal.com` hosts on
  `/signin`, `/agreements/approve`, `/webapps/hermes`, or `/checkoutweb`
  paths only, so card-entry forms on Xsolla and unrelated `paypal.com`
  pages keep the JS path. Log line when this branch fires:
  `Passthrough edit mode (paypal-login-text): …`.

In passthrough mode:

- **Passthrough branch:** keep `_browserInputForwarder.enableInput = true`,
  call `EventSystem.current.SetSelectedGameObject(_browserInputForwarder.gameObject)`
  so `PointerUIGUI.OnSelect` sets `_keyboardHasFocus = true`. Still call
  `FocusScript(index)` so the right DOM element has focus inside CEF.
- **Non-passthrough branch:** unchanged — `enableInput = false`, JS input path
  via `AppendTextScript` / `BackspaceScript`.

The edit-mode announcement calls `FormatRole(elem)` for the role label so
passthrough on the email field still announces "E-Mail-Feld" rather than
falling back to "Passwortfeld" / "Textfeld".

`HandlePassthroughEditModeInput` only intercepts control keys:

| Key | Behaviour |
|--|--|
| Escape | consume + `ExitEditMode` |
| Tab / Shift+Tab | consume + `ExitEditMode` + `TabNavigate` |
| Enter | `ExitEditMode` + announce submit + schedule rescan (keystroke still flows through to CEF, triggering form submit) |
| Arrow Up/Down | async re-read field value, announce full content |
| Arrow Left/Right / Home / End | async re-read field value, announce char at cursor |
| Everything else (printable, Backspace, Delete, Ctrl+A…) | ignored by the mod; flows through `OnGUI` → CEF naturally |

Cleanup paths that must restore `enableInput = true` and deselect the browser
GameObject:

- `ExitEditMode`
- `Deactivate`
- `ResetEditSessionOnPageChange` (called from both CAPTCHA detection and
  the normal `OnPageLoad` reset block) — a URL change invalidates the old
  DOM, so leaving a disabled forwarder / stale selection would break the
  new page.

## Key Technical Findings (keep in mind for future work)

- `Browser.browserId` is `protected internal int`. From outside
  `ZFBrowser.dll`, use reflection with `BindingFlags.NonPublic | Instance`.
- `BrowserNative.MouseButton` values are `MBT_LEFT / MBT_MIDDLE / MBT_RIGHT`.
- `BrowserInput.HandleInput` gates `HandleKeyInput()` on
  `browser.UIHandler.KeyboardHasFocus`. `PointerUIGUI.KeyboardHasFocus`
  returns `_keyboardHasFocus && enableInput`. So **disabling `enableInput`
  during edit mode also blocks `Browser.TypeText()`'s queued events**.
- `PointerUIGUI._keyboardHasFocus` is set to `true` only by `OnSelect`
  (Unity EventSystem selection). `SetSelectedGameObject(panel.gameObject)`
  is the cheapest way to trigger that from code.
- `PointerUIBase.OnGUI` is what actually forwards physical keyboard events
  to CEF. `InputManager.ConsumeKey` is mod-internal and does **not** hide
  `Event.current` from Unity's OnGUI dispatch, so consuming keys inside
  the mod does not prevent them reaching the browser.
- `autocomplete="current-password" / "new-password"` is a reliable
  password-field signal even when `type="text"` is used with masking.
  PayPal relies on this.
- PayPal failure URL pattern:
  `/signin/return?flowFrom=anw-stepup&failedBecause=invalid_input&YWRzZGRjYXB0Y2hh=1`
  (the base64 param decodes to `adsddcaptcha`). `IsLoginFailureUrl`
  handles this and announces a retry hint without forcing CAPTCHA state.
- `Browser.TypeText("…")` internally calls `QueueKeyEvent` per character,
  which pushes into `browserInput.extraEventsToInject`. That queue is only
  drained when `HandleKeyInput()` runs.

## If We Need to Revisit Path A

Preconditions that must be fixed before the native-click path can work:

1. **Keep `enableInput = true` during native typing** (same selection trick
   Path B uses), or pre-flush `extraEventsToInject` manually by calling a
   custom wrapper that bypasses the `KeyboardHasFocus` gate.
2. **Resolve the zero-bbox problem.** Options:
   - Walk the DOM from the extraction phase forward and store the *live*
     element reference in JS (e.g. in a `WeakMap`) rather than relying on
     a `data-aa-idx` attribute that React may strip when it replaces the
     node.
   - Query the element fresh on bbox lookup with a richer selector (name,
     autocomplete, type) instead of `data-aa-idx`.
   - If the node is genuinely zero-sized, fall back to the parent's bbox
     or to `document.activeElement` after issuing `focus()`.
3. **Verify iframe traversal.** The current `GetBoundingBoxScript` walks
   `frameElement` — double-check it is not blocked by cross-origin iframe
   restrictions on PayPal's pages (the inner `try/catch` is the guard).

The existing helpers (`FireNativeMouseClick`, `SimulateNativeClickThenType`,
`GetBoundingBoxScript`, `TryParseBBox`, `GetBrowserId`, the
`_pendingPostClickType` flush in `Update()`, the `_useNativeInput` escape
hatch triggered from `AppendTextScript` result strings) are intentionally
left in place so reviving Path A does not require reconstruction.

## Session Log: Email Passthrough Widening

Recorded here so a future revisit doesn't have to reconstruct it from git
history. **Read the "observed vs. hypothesised" split carefully** — the
causal claim is not proven.

### What was changed

`EnterEditMode` gate widened from "password only" to "password, OR email /
text / tel field on a PayPal login URL". New helper `IsPayPalLoginPage`
scopes the widening to `paypal.com` on `/signin`, `/agreements/approve`,
`/webapps/hermes`, `/checkoutweb`. The edit-mode announcement switched
from a hardcoded "Password field" / "Text field" pair to `FormatRole(elem)`
so email passthrough announces the correct role.

Motivating clue from the prior failing log: after a failed login the mod
re-announced the email field content plus the page's own visible error
text "Diese Angabe ist erforderlich. Das Format der E-Mail-Adresse oder
Handynummer ist ungültig." even though the DOM `el.value` clearly held
`fabian@nordwiesen30.de`. This *resembles* the React-controlled-component
symptom already documented for the password field.

### Observed — before vs. after (single before/after comparison)

- **Before:** Clicking "Einloggen" landed on
  `/signin/return?flowFrom=anw-stepup&…&failedBecause=invalid_input` and
  then `…&YWRzZGRjYXB0Y2hh=1` (base64 `adsddcaptcha`). The `/signin/return`
  page extracted 1–2 non-interactive elements ("Willkommen zurück!") and
  silently redirected back to `/webapps/hermes`. No CAPTCHA iframe ever
  appeared in any page.
- **After:** Clicking "Einloggen" landed on
  `/signin?intent=checkout&ctxId=xo_ctx_EC-…` with no `failedBecause` and no
  `adsddcaptcha` param. After DOM settled, the mod's CAPTCHA detector found
  an hCaptcha iframe (`paypalobjects.com/web/res/…/hcaptcha/hcaptcha_fph.html
  ?siteKey=bf07db68-… domain=hcaptcha.paypal.com`) and announced the
  visual-CAPTCHA warning.

Both runs used the same user, same password length (13 chars), same
two-screen SPA flow (URL stayed at `/agreements/approve` while DOM
element count went 17 → 22 after clicking "Weiter"). The only intentional
difference between the two runs was the passthrough widening.

### Hypotheses (plausible, not confirmed)

Treat everything below as working theory, not fact. Don't let these
guide future work without re-checking against fresh evidence.

- **H1 — Email React-state mismatch was the blocker.** The same
  execCommand/React-state pattern documented for the password field may
  also apply to the email field, so the backend saw an empty email on the
  combined Einloggen submit and returned `invalid_input`. Passthrough
  populated React state, the submission validated, and PayPal's normal
  risk engine then chose to present hCaptcha. *Why only plausible:* we
  never logged the actual POST payload, never inspected React devtools,
  and never verified the email field is in fact a controlled component
  on the specific page version we tested.
- **H2 — The "Weiter" step is purely client-side.** The URL does not
  change and element count simply grows from 17 to 22, which *looks like*
  a DOM swap rather than a navigation. If Weiter ever performs a
  server-side email pre-validation via AJAX, the silent 200 response
  wouldn't show up in our logs. Don't assume email validity was asserted
  by reaching the password stage.
- **H3 — PayPal's `adsddcaptcha` shortcut suppressed the visible
  challenge.** The first log's redirect chain set that param and
  immediately bounced to `/webapps/hermes` without rendering hCaptcha.
  The second log's flow never hit `adsddcaptcha` and *did* render
  hCaptcha. We *guess* `adsddcaptcha` is a "already-failed-validation,
  skip the challenge" shortcut, but we have no PayPal-side
  documentation. The flag could also be triggered by unrelated risk
  heuristics.
- **H4 — Prior sessions "always worked" because PayPal's risk score had
  not yet flagged this CEF fingerprint.** Offered as an explanation for
  the user's reasonable objection that the same two-page email flow used
  to succeed. Unverifiable without historical risk-score data. Could
  equally be explained by PayPal having tightened its React validation
  recently, by cookie state differences, or by coincidence.

### Alternative explanations that are *not* ruled out

Keep these in mind when re-investigating:

- PayPal may simply randomise between "silent reject" and "present
  hCaptcha" responses on the same flagged session, and the
  before/after difference could be coincidence on a single attempt
  each. A larger sample would be needed to rule this out.
- hCaptcha presence is not proof credentials validated server-side.
  PayPal could present hCaptcha as a *pre-authentication* challenge on
  sufficiently suspect sessions. The `/signin?intent=checkout` URL path
  is suggestive but not documented.
- The `FormatRole` announcement change happens to accompany the
  passthrough widening. If a future regression surfaces in the editing
  announcement, don't forget this edit touched both.

### What would actually confirm H1

- Intercept the Einloggen POST body (e.g. via ZFBrowser network hook or
  CEF devtools) on a failing session vs. a succeeding session and verify
  the `email`/`login_email`/equivalent field is present + populated in
  the passing one and empty in the failing one.
- Reproduce the failure N times with JS-path email + passthrough
  password, vs. N times with passthrough email + passthrough password,
  on a cold session. Statistical difference = evidence for H1. Identical
  distributions = coincidence / different cause.
- Confirm via React devtools (or a scripted `el._valueTracker` poke)
  that the specific email field on `/agreements/approve` is a
  controlled component.

## Related Source Locations

- `src/Core/Services/WebBrowserAccessibility.cs`
  - `EnterEditMode` — passthrough activation (password + PayPal login text)
  - `HandleEditModeInput` / `HandlePassthroughEditModeInput` — key dispatch
  - `ExitEditMode` / `ResetEditSessionOnPageChange` — cleanup
  - `FireNativeMouseClick` / `SimulateNativeClickThenType` / `GetBrowserId` — Path A fallback
  - `GetBoundingBoxScript` / `TryParseBBox` — bbox lookup for Path A
  - `IsCaptchaUrl` / `IsLoginFailureUrl` / `IsPayPalLoginPage` — URL classification
- `llm-docs/decompiled/ZenFulcrum.EmbeddedBrowser.Browser.decompiled.cs`
- `llm-docs/decompiled/ZenFulcrum.EmbeddedBrowser.BrowserInput.decompiled.cs`
- `llm-docs/decompiled/ZenFulcrum.EmbeddedBrowser.BrowserNative.decompiled.cs`
- `llm-docs/decompiled/ZenFulcrum.EmbeddedBrowser.PointerUIGUI.decompiled.cs`
- `llm-docs/decompiled/ZenFulcrum.EmbeddedBrowser.PointerUIBase.decompiled.cs`
