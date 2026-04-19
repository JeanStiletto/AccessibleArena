# WebBrowserAccessibility.cs
Path: src/Core/Services/WebBrowserAccessibility.cs
Lines: 2236

## Top-level comments
- Provides full keyboard navigation and screen reader support for embedded Chromium (ZFBrowser) popups. Extracts page elements via JavaScript, presents them as a navigable list, and supports clicking buttons, typing into fields, etc. Used by StoreNavigator when a payment popup opens.

## public class WebBrowserAccessibility (line 23)

### Constants
- private const float RescanDelayClick = 1.2f (line 27)
- private const float RescanDelaySecond = 3.0f (line 28) — Note: second rescan for slow page transitions
- private const float RescanDelayCheckbox = 0.3f (line 29)
- private const float LoadTimeout = 10f (line 30)

### Fields
- private Browser _browser (line 36)
- private PointerUIGUI _browserInputForwarder (line 37) — Note: disabled while editing to prevent double key delivery
- private GameObject _browserPanel (line 38)
- private IAnnouncementService _announcer (line 39)
- private bool _isActive (line 40)
- private string _contextLabel (line 41)
- private List<WebElement> _elements (line 43)
- private int _currentIndex (line 44)
- private bool _isEditingField (line 45)
- private bool _useNativeInput (line 46) — Note: use native CEF keyboard input for fields that reject JS events
- private bool _passthroughMode (line 47) — Note: password field mode — Unity→ZFBrowser keystroke forwarding, no JS interception
- private bool _isLoading (line 48)
- private int _lastWebElementCount (line 49)
- private string _lastContentFingerprint (line 50) — Note: detects AJAX content changes (same count, different text)
- private string _editFieldValue (line 53)
- private int _editCursorPos (line 54)
- private bool _pendingRescan (line 57)
- private float _rescanTimer (line 58)
- private float _secondRescanTimer (line 59)
- private float _extractionStartTime (line 60) — Note: used for timeout detection
- private GameObject _backToArenaButton (line 63)
- private string _backToArenaLabel (line 64)
- private int _emptyRescanCount (line 67)
- private bool _captchaDetected (line 68)
- private bool _captchaCheckCompleted (line 69) — Note: one-shot guard, reset on URL change
- private bool _emptyLoadingAnnounced (line 70) — Note: suppresses repeated "loading…" announcements on the same page
- private const int MaxEmptyRescansBeforeCheck = 3 (line 71)
- private float _clickCooldownUntil (line 74)
- private const float ClickCooldownSeconds = 1.5f (line 75)
- private const float CheckboxCooldownSeconds = 0.5f (line 76)
- private bool _mutationObserverActive (line 79)
- private float _mutationPollTimer (line 80)
- private float _mutationStableTime (line 81)
- private bool _hasInteractiveElements (line 82)
- private const float MutationPollInterval = 0.5f (line 83)
- private const float MutationStableTimeout = 8f (line 84)
- private const float MutationStableTimeoutPostClick = 45f (line 85) — Note: payment processing can take 30+ seconds
- private float _mutationCurrentTimeout (line 86)

### Nested types
- private struct WebElement (line 92) — see below

### JavaScript source constants (string literals)
- private const string ExtractionScript (line 113) — Note: IIFE-ready; scans main doc + same-origin iframes
- private const string FindElementFunc (line 302) — Note: helper to find element by data-aa-idx across main doc + iframes
- private const string InstallMutationObserverScript (line 536) — Note: idempotent; observes body + same-origin iframe bodies
- private const string PollMutationScript (line 561)
- private const string DetectCrossOriginIframesScript (line 568)

### Static helpers (script builders)
- private static string ClickScript(int index) (line 318)
- private static string FocusScript(int index) (line 324)
- private static string GetBoundingBoxScript(int index) (line 332) — Note: returns CSV "cx,cy,vw,vh" in CSS pixels, walks iframe offsets
- private static string SelectAllScript(int index) (line 355)
- private static string ReadValueScript(int index) (line 361)
- private static string AppendTextScript(int index, string text) (line 373) — Note: tries execCommand, keyboard events, then native value setter with React tracker reset
- private static string BackspaceScript(int index) (line 467)
- private static string SubmitScript(int index) (line 514)

### Properties
- public bool IsActive => _isActive (line 588)

### Methods (public API)
- public void Activate(GameObject panel, IAnnouncementService announcer, string contextLabel = null) (line 594) — Note: also unsubscribes previous onLoad handler, blocks Escape via KeyboardManagerPatch
- public void Deactivate() (line 663) — Note: re-enables browser input forwarder, clears EventSystem selection, releases Escape block
- public void Update() (line 711) — Note: also flushes _pendingPostClickType, handles extraction timeout and MutationObserver polling
- public void HandleInput() (line 784) — Note: routes to loading/cooldown/edit/navigation branches
- private bool IsClickOnCooldown() (line 837)
- private void StartClickCooldown(float seconds) (line 842)

### Methods (navigation input)
- private void HandleNavigationOnlyInput() (line 855) — Note: allows navigation keys but consumes activation keys during click cooldown
- private void HandleNavigationInput() (line 880)

### Methods (edit mode input)
- private void HandleEditModeInput() (line 959) — Note: delegates to passthrough variant when _passthroughMode is set
- private void HandlePassthroughEditModeInput() (line 1135) — Note: intercepts only control keys; printable chars flow through Unity→CEF
- private void ExitEditMode() (line 1199) — Note: deselects browser GameObject from EventSystem
- private void ResetEditSessionOnPageChange() (line 1220) — Note: same cleanup as ExitEditMode but framed as page-change reset

### Fields (continued — cached reflection)
- private static FieldInfo _browserIdField (line 1235) — Note: cached handle for Browser.browserId

### Methods (native input)
- private int GetBrowserId() (line 1237) — Note: lazily initializes _browserIdField via reflection
- private void FireNativeMouseClick(float nx, float ny) (line 1256) — Note: calls BrowserNative.zfb_mouseMove / zfb_mouseButton
- private void SimulateNativeClickThenType(WebElement elem, string chars) (line 1275) — Note: defers TypeText by one frame via _pendingPostClickType

### Fields (continued)
- private string _pendingPostClickType (line 1303) — Note: flushed on next Update() tick after native click

### Methods (bbox parsing + field reading)
- private static bool TryParseBBox(string csv, out float nx, out float ny) (line 1305)
- private void RefreshAndReadFieldValue(bool readFull, int cursorDelta = 0, int cursorJump = int.MinValue) (line 1322) — Note: announces cached character at cursor; password fields announce star only

### Methods (element extraction)
- private void ExtractElements() (line 1389)
- private void OnElementsExtracted(JSONNode result) (line 1412) — Note: also deduplicates text/heading, appends Back-to-Arena, manages empty/captcha/mutation-observer branches
- private void OnExtractionError(Exception ex) (line 1571)
- private string ComputeContentFingerprint() (line 1582)

### Methods (MutationObserver)
- private void InstallMutationObserver() (line 1601)
- private void PollMutationObserver() (line 1623) — Note: triggers CheckForCaptcha when DOM stable but no interactive elements found

### Methods (page load handling)
- private void OnPageLoad(JSONNode loadData) (line 1665) — Note: short-circuits if captcha already detected; calls IsCaptchaUrl / IsLoginFailureUrl before resetting state
- private void ScheduleRescan(float delay) (line 1726)

### Methods (element navigation)
- private void MoveElement(int direction) (line 1736) — Note: announces BeginningOfList / EndOfList at boundaries
- private void TabNavigate(int direction) (line 1761) — Note: auto-enters edit mode on landing on a textbox
- private void AnnounceCurrentElement() (line 1775) — Note: for textboxes, re-reads value from JS and updates cached _elements[idx]
- private string FormatElementAnnouncement(WebElement elem, int index, int total) (line 1814)
- private string FormatRole(WebElement elem) (line 1854)

### Methods (element activation)
- private void ActivateCurrentElement() (line 1880) — Note: dispatches by role, starts cooldown, schedules rescans, installs MutationObserver with post-click timeout
- private void EnterEditMode(WebElement elem) (line 1938) — Note: selects passthrough for password and PayPal login email/text/tel fields
- private void ClickElement(WebElement elem) (line 1990)

### Methods (CAPTCHA detection)
- private static bool IsCaptchaUrl(string url) (line 2021)
- private static bool IsLoginFailureUrl(string url) (line 2040) — Note: matches PayPal base64 "adsddcaptcha" param and stepup+failedBecause combos
- private static bool IsPayPalLoginPage(string url) (line 2064) — Note: narrowly scoped to /signin, /agreements/approve, /webapps/hermes, /checkoutweb paths on paypal.com
- private void CheckForCaptcha() (line 2075) — Note: combines URL heuristics with cross-origin iframe vendor scan

### Fields (continued)
- private static readonly string[] UserFacingCaptchaTokens (line 2146) — Note: recaptcha, hcaptcha, arkoselabs, funcaptcha, challenges.cloudflare.com, turnstile, ddc.paypal.com/captcha

### Methods (continued)
- private static bool ContainsUserFacingCaptchaVendor(string iframeJson) (line 2157)

### Methods (back to arena)
- private void FindBackToArenaButton(GameObject panel) (line 2172) — Note: matches on name/label containing back/close/return/arena, falls back to first non-browser Button
- private void ClickBackToArena() (line 2219)

## private struct WebElement (line 92) — nested in WebBrowserAccessibility

### Fields
- public string Tag (line 94)
- public string Text (line 95)
- public string Role (line 96) — Note: button, link, textbox, combobox, checkbox, heading, text
- public string InputType (line 97) — Note: text, password, email, number, etc.
- public string Placeholder (line 98)
- public string Value (line 99)
- public int Index (line 100) — Note: data-aa-idx value for re-targeting in the DOM
- public bool IsInteractive (line 101)
- public bool IsChecked (line 102)
- public bool IsBackToArena (line 103) — Note: true for the Unity "Back to Arena" button, not a DOM element
