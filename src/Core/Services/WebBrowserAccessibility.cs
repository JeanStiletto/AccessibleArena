using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Patches;
using ZenFulcrum.EmbeddedBrowser;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Provides full keyboard navigation and screen reader support for
    /// embedded Chromium browser popups (ZFBrowser). Extracts page elements
    /// via JavaScript, presents them as a navigable list, and allows
    /// clicking buttons, typing into fields, etc.
    ///
    /// Used by StoreNavigator when a payment popup opens.
    /// </summary>
    public class WebBrowserAccessibility
    {
        #region Constants

        private const float RescanDelayClick = 1.2f;
        private const float RescanDelaySecond = 3.0f; // Second rescan for slow page transitions
        private const float RescanDelayCheckbox = 0.3f;
        private const float LoadTimeout = 10f;

        #endregion

        #region State

        private Browser _browser;
        private PointerUIGUI _browserInputForwarder; // Disabled while editing to prevent double key delivery
        private GameObject _browserPanel;
        private IAnnouncementService _announcer;
        private bool _isActive;
        private string _contextLabel;

        private List<WebElement> _elements = new List<WebElement>();
        private int _currentIndex;
        private bool _isEditingField;
        private bool _useNativeInput; // Use native CEF keyboard input (for fields that reject JS events)
        private bool _passthroughMode; // Password fields: Unity→ZFBrowser keystroke forwarding, no JS interception
        private bool _isLoading;
        private int _lastWebElementCount; // Track for silent rescan comparison
        private string _lastContentFingerprint = ""; // Detect AJAX content changes (same count, different text)

        // Edit mode cursor tracking for character-by-character reading
        private string _editFieldValue = "";
        private int _editCursorPos;

        // Rescan timers for detecting page changes after clicks
        private bool _pendingRescan;
        private float _rescanTimer;
        private float _secondRescanTimer; // Second delayed rescan for slow transitions
        private float _extractionStartTime; // For timeout detection

        // "Back to Arena" button (Unity button outside the browser)
        private GameObject _backToArenaButton;
        private string _backToArenaLabel;

        // CAPTCHA / security check detection
        private int _emptyRescanCount;
        private bool _captchaDetected;
        private bool _captchaCheckCompleted; // one-shot guard, reset on URL change
        private bool _emptyLoadingAnnounced;  // suppress repeated "loading…" announcements on the same page
        private const int MaxEmptyRescansBeforeCheck = 3; // ~4.5 seconds of empty rescans

        // Click cooldown — prevents double-activation of payment buttons
        private float _clickCooldownUntil;
        private const float ClickCooldownSeconds = 1.5f;
        private const float CheckboxCooldownSeconds = 0.5f;

        // MutationObserver polling — detects dynamically loaded content (e.g. payment method buttons)
        private bool _mutationObserverActive;
        private float _mutationPollTimer;
        private float _mutationStableTime;           // Time since last DOM change (for auto-stop)
        private bool _hasInteractiveElements;         // Whether current extraction found any interactive elements
        private const float MutationPollInterval = 0.5f;
        private const float MutationStableTimeout = 8f;          // Default: stop polling after DOM is stable for this long
        private const float MutationStableTimeoutPostClick = 45f; // After button click: payment processing can take 30+ seconds
        private float _mutationCurrentTimeout = MutationStableTimeout;

        #endregion

        #region WebElement

        private struct WebElement
        {
            public string Tag;
            public string Text;
            public string Role;       // button, link, textbox, combobox, checkbox, heading, text
            public string InputType;   // text, password, email, number, etc.
            public string Placeholder;
            public string Value;
            public int Index;          // data-aa-idx value for re-targeting
            public bool IsInteractive;
            public bool IsChecked;
            public bool IsBackToArena; // True for the Unity "Back to Arena" button
        }

        #endregion

        #region Public API

        public bool IsActive => _isActive;

        /// <summary>
        /// Activate browser accessibility for the given panel.
        /// Finds the Browser component and starts element extraction.
        /// </summary>
        public void Activate(GameObject panel, IAnnouncementService announcer, string contextLabel = null)
        {
            // Clean up previous session if still active (prevents dangling onLoad handlers)
            if (_isActive && _browser != null)
            {
                _browser.onLoad -= OnPageLoad;
            }

            _browserPanel = panel;
            _announcer = announcer;
            _contextLabel = contextLabel ?? Strings.WebBrowser_PaymentPage;
            _currentIndex = 0;
            _isEditingField = false;
            _isLoading = true;
            _pendingRescan = false;
            _secondRescanTimer = 0;
            _emptyRescanCount = 0;
            _captchaDetected = false;
            _captchaCheckCompleted = false;
            _emptyLoadingAnnounced = false;
            _clickCooldownUntil = 0;
            _mutationObserverActive = false;
            _mutationPollTimer = 0;
            _mutationStableTime = 0;
            _mutationCurrentTimeout = MutationStableTimeout;
            _hasInteractiveElements = false;
            _lastWebElementCount = 0;
            _lastContentFingerprint = "";
            _elements.Clear();

            // Find ZFBrowser.Browser component
            _browser = panel.GetComponentInChildren<Browser>(true);
            if (_browser == null)
            {
                Log.Msg("WebBrowser", "No Browser component found in panel");
                _announcer.AnnounceInterrupt(Strings.WebBrowser_NoBrowserFound);
                _isActive = false;
                return;
            }

            _browserInputForwarder = panel.GetComponentInChildren<PointerUIGUI>(true);

            _isActive = true;

            // Block Escape from reaching the game (would open settings menu)
            KeyboardManagerPatch.BlockEscape = true;

            // Find "Back to Arena" button (Unity Button outside/alongside the browser)
            FindBackToArenaButton(panel);

            // Subscribe to page load events
            _browser.onLoad += OnPageLoad;

            Log.Msg("WebBrowser", $"Activated. Browser URL: {_browser.Url}, IsLoaded: {_browser.IsLoaded}");

            if (_browser.IsLoaded)
            {
                _announcer.AnnounceInterrupt(Strings.WebBrowser_LoadingElements(_contextLabel));
                ExtractElements();
            }
            else
            {
                _announcer.AnnounceInterrupt(Strings.WebBrowser_ContextLoading(_contextLabel));
            }
        }

        /// <summary>
        /// Deactivate and clean up.
        /// </summary>
        public void Deactivate()
        {
            if (_browser != null)
            {
                _browser.onLoad -= OnPageLoad;
            }

            // Safety net in case we're deactivating mid-edit (e.g. panel torn down).
            if (_browserInputForwarder != null)
            {
                _browserInputForwarder.enableInput = true;
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null && es.currentSelectedGameObject == _browserInputForwarder.gameObject)
                    es.SetSelectedGameObject(null);
            }

            _browser = null;
            _browserInputForwarder = null;
            _browserPanel = null;
            _isActive = false;
            _isEditingField = false;
            _passthroughMode = false;
            _isLoading = false;
            _pendingRescan = false;
            _secondRescanTimer = 0;
            _emptyRescanCount = 0;
            _captchaDetected = false;
            _captchaCheckCompleted = false;
            _emptyLoadingAnnounced = false;
            _clickCooldownUntil = 0;
            _mutationObserverActive = false;
            _mutationPollTimer = 0;
            _mutationStableTime = 0;
            _hasInteractiveElements = false;
            _elements.Clear();
            _backToArenaButton = null;
            _backToArenaLabel = null;

            // Release Escape blocking
            KeyboardManagerPatch.BlockEscape = false;

            Log.Msg("WebBrowser", "Deactivated");
        }

        /// <summary>
        /// Called each frame by StoreNavigator while active.
        /// Handles rescan timer and validity checks.
        /// </summary>
        public void Update()
        {
            if (!_isActive) return;

            // Validity check
            if (_browserPanel == null || !_browserPanel.activeInHierarchy ||
                _browser == null || _browser.gameObject == null)
            {
                Deactivate();
                return;
            }

            // Flush any characters queued after a native click — gives CEF one frame
            // to process the focus-change triggered by the click before keystrokes arrive.
            if (_pendingPostClickType != null)
            {
                string queued = _pendingPostClickType;
                _pendingPostClickType = null;
                try
                {
                    _browser.TypeText(queued);
                    Log.Msg("WebBrowser", $"TypeText (post-click): ***");
                }
                catch (Exception ex)
                {
                    Log.Msg("WebBrowser", $"Post-click TypeText error: {ex.Message}");
                }
            }

            // Extraction timeout — if Promise never resolved, reset and retry
            if (_isLoading && Time.realtimeSinceStartup - _extractionStartTime > 5f)
            {
                Log.Msg("WebBrowser", "Extraction timed out, resetting");
                _isLoading = false;
                ScheduleRescan(1.0f);
            }

            // Rescan timer
            if (_pendingRescan)
            {
                _rescanTimer -= Time.deltaTime;
                if (_rescanTimer <= 0)
                {
                    _pendingRescan = false;
                    ExtractElements();
                }
            }

            // Second rescan timer (catches slow page transitions after clicks)
            if (_secondRescanTimer > 0)
            {
                _secondRescanTimer -= Time.deltaTime;
                if (_secondRescanTimer <= 0)
                {
                    ExtractElements();
                }
            }

            // MutationObserver polling — detect dynamically loaded page content
            if (_mutationObserverActive && !_isLoading && !_pendingRescan)
            {
                _mutationPollTimer -= Time.deltaTime;
                if (_mutationPollTimer <= 0)
                {
                    _mutationPollTimer = MutationPollInterval;
                    PollMutationObserver();
                }
            }
        }

        /// <summary>
        /// Handle keyboard input. Called by StoreNavigator each frame.
        /// </summary>
        public void HandleInput()
        {
            if (!_isActive) return;

            // While loading, block input
            if (_isLoading)
            {
                // Allow Backspace to exit even while loading
                if (Input.GetKeyDown(KeyCode.Backspace))
                {
                    InputManager.ConsumeKey(KeyCode.Backspace);
                    ClickBackToArena();
                }
                return;
            }

            // During click cooldown, allow navigation and Backspace but block activation
            if (IsClickOnCooldown())
            {
                // Consume Enter/Space so they don't leak to the game
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    InputManager.ConsumeKey(KeyCode.Return);
                    InputManager.ConsumeKey(KeyCode.KeypadEnter);
                    return;
                }
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    InputManager.ConsumeKey(KeyCode.Space);
                    return;
                }
                // Allow Backspace to exit
                if (Input.GetKeyDown(KeyCode.Backspace))
                {
                    InputManager.ConsumeKey(KeyCode.Backspace);
                    ClickBackToArena();
                    return;
                }
                // Allow navigation keys (Up/Down/Tab/Home/End) during cooldown
                HandleNavigationOnlyInput();
                return;
            }

            if (_isEditingField)
            {
                HandleEditModeInput();
            }
            else
            {
                HandleNavigationInput();
            }
        }

        private bool IsClickOnCooldown()
        {
            return Time.realtimeSinceStartup < _clickCooldownUntil;
        }

        private void StartClickCooldown(float seconds)
        {
            _clickCooldownUntil = Time.realtimeSinceStartup + seconds;
        }

        #endregion

        #region Navigation Input

        /// <summary>
        /// Navigation-only input during click cooldown. Allows moving between elements
        /// but blocks all activation keys (Enter/Space are consumed in HandleInput).
        /// </summary>
        private void HandleNavigationOnlyInput()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow)) { MoveElement(-1); return; }
            if (Input.GetKeyDown(KeyCode.DownArrow)) { MoveElement(1); return; }
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                // Don't auto-enter edit mode during cooldown — just navigate
                MoveElement(shift ? -1 : 1);
                return;
            }
            if (Input.GetKeyDown(KeyCode.Home) && _elements.Count > 0)
            {
                _currentIndex = 0;
                AnnounceCurrentElement();
                return;
            }
            if (Input.GetKeyDown(KeyCode.End) && _elements.Count > 0)
            {
                _currentIndex = _elements.Count - 1;
                AnnounceCurrentElement();
                return;
            }
        }

        private void HandleNavigationInput()
        {
            // Up/Down navigate elements
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveElement(-1);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveElement(1);
                return;
            }

            // Tab/Shift+Tab — auto-enter edit mode if landing on a text field
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                TabNavigate(shift ? -1 : 1);
                return;
            }

            // Home/End
            if (Input.GetKeyDown(KeyCode.Home))
            {
                if (_elements.Count > 0)
                {
                    _currentIndex = 0;
                    AnnounceCurrentElement();
                }
                return;
            }
            if (Input.GetKeyDown(KeyCode.End))
            {
                if (_elements.Count > 0)
                {
                    _currentIndex = _elements.Count - 1;
                    AnnounceCurrentElement();
                }
                return;
            }

            // Enter — activate
            bool enterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            if (enterPressed)
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
                ActivateCurrentElement();
                return;
            }

            // Space — activate buttons/links/checkboxes (not text fields)
            if (InputManager.GetKeyDownAndConsume(KeyCode.Space))
            {
                if (_currentIndex >= 0 && _currentIndex < _elements.Count)
                {
                    var elem = _elements[_currentIndex];
                    if (elem.Role != "textbox" && elem.IsInteractive)
                    {
                        ActivateCurrentElement();
                    }
                }
                return;
            }

            // Backspace — click "Back to Arena"
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                ClickBackToArena();
                return;
            }
        }

        #endregion

        #region Edit Mode Input

        private void HandleEditModeInput()
        {
            // Passthrough mode (password fields): Unity forwards keystrokes directly
            // to CEF via PointerUIGUI. We only intercept edit-mode control keys
            // (Escape/Tab to exit, arrows/Home/End for readback) — everything else
            // (printable chars, Backspace, Enter, Ctrl+A…) passes through naturally
            // so CEF sees isTrusted=true events that bot-protected sites accept.
            if (_passthroughMode)
            {
                HandlePassthroughEditModeInput();
                return;
            }

            // Escape — exit edit mode
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                InputManager.ConsumeKey(KeyCode.Escape);
                ExitEditMode();
                _announcer.AnnounceInterrupt(Strings.ExitedInputField);
                return;
            }

            // Tab — exit edit mode and move to next element (auto-enter if next is also a text field)
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                ExitEditMode();
                TabNavigate(shift ? -1 : 1);
                return;
            }

            // Enter — submit form, exit edit mode, schedule rescan
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
                if (_currentIndex >= 0 && _currentIndex < _elements.Count)
                {
                    var elem = _elements[_currentIndex];
                    _browser.EvalJSCSP(WebBrowserScripts.SubmitScript(elem.Index))
                        .Catch(ex => Log.Msg("WebBrowser", $"Submit error: {ex.Message}"));
                }
                ExitEditMode();
                _announcer.AnnounceInterrupt(Strings.WebBrowser_Submitted);
                StartClickCooldown(ClickCooldownSeconds);
                ScheduleRescan(RescanDelayClick);
                _secondRescanTimer = RescanDelaySecond;
                return;
            }

            // Arrow Up/Down — read full field content
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                RefreshAndReadFieldValue(readFull: true);
                return;
            }

            // Arrow Left/Right — read character at cursor position
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                RefreshAndReadFieldValue(readFull: false, cursorDelta: -1);
                return;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                RefreshAndReadFieldValue(readFull: false, cursorDelta: 1);
                return;
            }

            // Home/End — jump to beginning/end of field
            if (Input.GetKeyDown(KeyCode.Home))
            {
                RefreshAndReadFieldValue(readFull: false, cursorJump: 0);
                return;
            }
            if (Input.GetKeyDown(KeyCode.End))
            {
                RefreshAndReadFieldValue(readFull: false, cursorJump: -1);
                return;
            }

            // Ctrl+A — select all text in field (so next keystroke replaces it)
            if (Input.GetKeyDown(KeyCode.A) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                if (_currentIndex >= 0 && _currentIndex < _elements.Count)
                {
                    var elem = _elements[_currentIndex];
                    _browser.EvalJSCSP(WebBrowserScripts.SelectAllScript(elem.Index))
                        .Catch(ex => Log.Msg("WebBrowser", $"SelectAll error: {ex.Message}"));
                    _announcer.AnnounceInterrupt(Strings.AllSelected);
                }
                return;
            }

            // Backspace — delete last character
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                if (_currentIndex >= 0 && _currentIndex < _elements.Count)
                {
                    var elem = _elements[_currentIndex];
                    if (_useNativeInput)
                    {
                        _browser.PressKey(KeyCode.Backspace);
                        Log.Msg("WebBrowser", $"Backspace (native) in element {elem.Index}: {elem.Text}");
                    }
                    else
                    {
                        _browser.EvalJSCSP(WebBrowserScripts.BackspaceScript(elem.Index))
                            .Then(result =>
                            {
                                string val = (string)result;
                                if (val == "not_found")
                                    _announcer.AnnounceInterrupt(Strings.WebBrowser_FieldNotFound);
                            })
                            .Catch(ex => Log.Msg("WebBrowser", $"Backspace error: {ex.Message}"));
                    }
                }
                return;
            }

            // Printable characters — append text
            string inputStr = Input.inputString;
            if (!string.IsNullOrEmpty(inputStr))
            {
                // Filter out control characters
                var filtered = new System.Text.StringBuilder();
                foreach (char c in inputStr)
                {
                    if (c >= ' ' && c != '\r' && c != '\n' && c != '\t' && c != '\b')
                    {
                        filtered.Append(c);
                    }
                }
                if (filtered.Length > 0 && _currentIndex >= 0 && _currentIndex < _elements.Count)
                {
                    var elem = _elements[_currentIndex];
                    string chars = filtered.ToString();
                    if (_useNativeInput)
                    {
                        // Native CEF input — sends isTrusted keyboard events
                        _browser.TypeText(chars);
                        Log.Msg("WebBrowser", $"TypeText (native): {(elem.InputType == "password" ? "***" : chars)}");
                    }
                    else
                    {
                        Log.Msg("WebBrowser", $"Typing '{chars}' into element {elem.Index}: {elem.Text}");
                        _browser.EvalJSCSP(WebBrowserScripts.AppendTextScript(elem.Index, chars))
                            .Then(result =>
                            {
                                string res = (string)result;
                                Log.Msg("WebBrowser", $"TypeText result: {res}");
                                // Detect JS input failure: execCommand reports success but value is empty
                                // This happens on sites like PayPal that reject non-trusted events
                                if (res != null && res.StartsWith("execCommand:") && res == "execCommand:"
                                    || res != null && res == "all_failed:")
                                {
                                    Log.Msg("WebBrowser", $"JS input failed, switching to native CEF input");
                                    _useNativeInput = true;
                                    // Simulate a native mouse click on the field first — gives CEF top-level
                                    // focus to the correct iframe so TypeText's trusted keyboard events
                                    // actually reach the input's DOM. Then re-send the failed chars.
                                    SimulateNativeClickThenType(elem, chars);
                                }
                            })
                            .Catch(ex => Log.Msg("WebBrowser", $"TypeText error: {ex.Message}"));
                    }
                }
            }
        }

        /// <summary>
        /// Edit-mode input for password fields (passthrough mode).
        /// Only intercepts control keys — printable chars and Backspace flow
        /// through Unity's OnGUI → PointerUIGUI → CEF path as native trusted events.
        /// </summary>
        private void HandlePassthroughEditModeInput()
        {
            // Escape — exit edit mode (consume so CEF doesn't see it either)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                InputManager.ConsumeKey(KeyCode.Escape);
                ExitEditMode();
                _announcer.AnnounceInterrupt(Strings.ExitedInputField);
                return;
            }

            // Tab — exit edit mode and navigate (consume so CEF doesn't steal focus)
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                ExitEditMode();
                TabNavigate(shift ? -1 : 1);
                return;
            }

            // Enter — natural form submission passes through; schedule rescan for new page
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ExitEditMode();
                _announcer.AnnounceInterrupt(Strings.WebBrowser_Submitted);
                StartClickCooldown(ClickCooldownSeconds);
                ScheduleRescan(RescanDelayClick);
                _secondRescanTimer = RescanDelaySecond;
                return;
            }

            // Arrow Up/Down — read full field content (cursor stays put in browser)
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                RefreshAndReadFieldValue(readFull: true);
                return;
            }

            // Arrow Left/Right — read char at cursor (also moves cursor naturally in browser)
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                RefreshAndReadFieldValue(readFull: false, cursorDelta: -1);
                return;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                RefreshAndReadFieldValue(readFull: false, cursorDelta: 1);
                return;
            }
            if (Input.GetKeyDown(KeyCode.Home))
            {
                RefreshAndReadFieldValue(readFull: false, cursorJump: 0);
                return;
            }
            if (Input.GetKeyDown(KeyCode.End))
            {
                RefreshAndReadFieldValue(readFull: false, cursorJump: -1);
                return;
            }

            // All other keys (printable chars, Backspace, Ctrl+A, Delete…) flow
            // through Unity OnGUI → PointerUIGUI → CEF. Nothing to do here.
        }

        private void ExitEditMode()
        {
            _isEditingField = false;
            _useNativeInput = false;
            _passthroughMode = false;
            if (_browserInputForwarder != null)
            {
                _browserInputForwarder.enableInput = true;
                // Deselect the browser GameObject so Unity's OnGUI stops forwarding
                // keystrokes to CEF (KeyboardHasFocus flips back to false on Deselect).
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null && es.currentSelectedGameObject == _browserInputForwarder.gameObject)
                    es.SetSelectedGameObject(null);
            }
        }

        /// <summary>
        /// Reset edit-mode state when the page URL changes. The old DOM is gone,
        /// so any passthrough selection + disabled input forwarder would leave
        /// the new page with broken keyboard/mouse input.
        /// </summary>
        private void ResetEditSessionOnPageChange()
        {
            _isEditingField = false;
            _useNativeInput = false;
            _passthroughMode = false;
            if (_browserInputForwarder != null)
            {
                _browserInputForwarder.enableInput = true;
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null && es.currentSelectedGameObject == _browserInputForwarder.gameObject)
                    es.SetSelectedGameObject(null);
            }
        }

        // Cached reflection handle for Browser.browserId (protected internal int in ZFBrowser.dll).
        private static FieldInfo _browserIdField;

        private int GetBrowserId()
        {
            if (_browser == null) return 0;
            if (_browserIdField == null)
            {
                _browserIdField = typeof(Browser).GetField(
                    "browserId",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }
            if (_browserIdField == null) return 0;
            try { return (int)_browserIdField.GetValue(_browser); }
            catch { return 0; }
        }

        // Inject a trusted mouse click at the field's screen position via CEF natives.
        // Needed for login forms (PayPal etc.) that filter non-isTrusted events — JS focus()
        // only sets DOM focus within the iframe, while native TypeText() routes keys to
        // CEF's top-level focused frame, which may still be something else.
        // Coords are normalized 0..1 with y top-origin (matching CEF convention).
        private void FireNativeMouseClick(float nx, float ny)
        {
            int id = GetBrowserId();
            if (id == 0) return;
            try
            {
                BrowserNative.zfb_mouseMove(id, nx, ny);
                BrowserNative.zfb_mouseButton(id, BrowserNative.MouseButton.MBT_LEFT, true, 1);
                BrowserNative.zfb_mouseButton(id, BrowserNative.MouseButton.MBT_LEFT, false, 1);
                Log.Msg("WebBrowser", $"Native click fired at ({nx:F3}, {ny:F3})");
            }
            catch (Exception ex)
            {
                Log.Msg("WebBrowser", $"Native click failed: {ex.Message}");
            }
        }

        // Look up the element's bbox and inject a native click at its center,
        // then invoke onClicked() to continue the input sequence (e.g. TypeText).
        private void SimulateNativeClickThenType(WebElement elem, string chars)
        {
            _browser.EvalJSCSP(WebBrowserScripts.GetBoundingBoxScript(elem.Index))
                .Then(result =>
                {
                    string s = (string)result;
                    Log.Msg("WebBrowser", $"BBox raw CSV: '{s}'");
                    if (!TryParseBBox(s, out float nx, out float ny))
                    {
                        Log.Msg("WebBrowser", $"BBox lookup failed ('{s}') — sending TypeText without native click");
                        _browser.TypeText(chars);
                        return;
                    }
                    FireNativeMouseClick(nx, ny);
                    // Delay TypeText by one frame so CEF finishes processing the mouse click
                    // (focus change) before keystrokes are injected.
                    _pendingPostClickType = chars;
                })
                .Catch(ex =>
                {
                    Log.Msg("WebBrowser", $"BBox error: {ex.Message} — sending TypeText without native click");
                    _browser.TypeText(chars);
                });
        }

        // Set by SimulateNativeClickThenType after firing the native click.
        // Flushed on the next Update() tick so CEF has time to process the click's focus change
        // before we inject keyboard events.
        private string _pendingPostClickType;

        private static bool TryParseBBox(string csv, out float nx, out float ny)
        {
            nx = ny = 0f;
            if (string.IsNullOrEmpty(csv)) return false;
            var parts = csv.Split(',');
            if (parts.Length != 4) return false;
            var inv = CultureInfo.InvariantCulture;
            if (!float.TryParse(parts[0], NumberStyles.Float, inv, out float cx)) return false;
            if (!float.TryParse(parts[1], NumberStyles.Float, inv, out float cy)) return false;
            if (!float.TryParse(parts[2], NumberStyles.Float, inv, out float vw)) return false;
            if (!float.TryParse(parts[3], NumberStyles.Float, inv, out float vh)) return false;
            if (vw <= 0f || vh <= 0f) return false;
            nx = Mathf.Clamp01(cx / vw);
            ny = Mathf.Clamp01(cy / vh);
            return true;
        }

        private void RefreshAndReadFieldValue(bool readFull, int cursorDelta = 0, int cursorJump = int.MinValue)
        {
            if (_currentIndex < 0 || _currentIndex >= _elements.Count) return;
            var elem = _elements[_currentIndex];

            _browser.EvalJSCSP(WebBrowserScripts.ReadValueScript(elem.Index))
                .Then(result =>
                {
                    string val = (string)result;
                    _editFieldValue = val ?? "";

                    if (readFull)
                    {
                        // Up/Down: read full value
                        if (string.IsNullOrEmpty(val))
                        {
                            _announcer.AnnounceInterrupt(Strings.InputFieldEmpty);
                        }
                        else if (elem.InputType == "password")
                        {
                            _announcer.AnnounceInterrupt(Strings.Characters(val.Length));
                        }
                        else
                        {
                            _announcer.AnnounceInterrupt(val);
                        }
                    }
                    else
                    {
                        // Left/Right/Home/End: read character at cursor
                        if (string.IsNullOrEmpty(_editFieldValue))
                        {
                            _announcer.AnnounceInterrupt(Strings.InputFieldEmpty);
                            return;
                        }

                        // Home/End: jump to absolute position (-1 = end)
                        if (cursorJump != int.MinValue)
                            _editCursorPos = cursorJump < 0 ? _editFieldValue.Length - 1 : cursorJump;
                        else
                            _editCursorPos += cursorDelta;

                        if (_editCursorPos < 0) _editCursorPos = 0;
                        if (_editCursorPos >= _editFieldValue.Length)
                            _editCursorPos = _editFieldValue.Length - 1;

                        if (elem.InputType == "password")
                        {
                            _announcer.AnnounceInterrupt(Strings.WebBrowser_PasswordStar);
                        }
                        else
                        {
                            char c = _editFieldValue[_editCursorPos];
                            _announcer.AnnounceInterrupt(Strings.GetCharacterName(c));
                        }
                    }
                })
                .Catch(ex =>
                {
                    Log.Msg("WebBrowser", $"Error reading field value: {ex.Message}");
                });
        }

        #endregion

        #region Element Extraction

        private void ExtractElements()
        {
            if (_browser == null || !_browser.IsReady)
            {
                Log.Msg("WebBrowser", "Cannot extract - browser not ready");
                return;
            }

            if (_isLoading)
            {
                Log.Msg("WebBrowser", "Extraction already in progress, skipping");
                return;
            }

            _isLoading = true;
            _extractionStartTime = Time.realtimeSinceStartup;
            Log.Msg("WebBrowser", "Extracting page elements...");

            _browser.EvalJSCSP(WebBrowserScripts.ExtractionScript)
                .Then(result => OnElementsExtracted(result))
                .Catch(ex => OnExtractionError(ex));
        }

        private void OnElementsExtracted(JSONNode result)
        {
            _elements.Clear();
            _isLoading = false;

            if (result == null || result.Type != JSONNode.NodeType.Array)
            {
                Log.Msg("WebBrowser", $"Extraction returned non-array: {result?.Type}");
                _announcer.AnnounceInterrupt(Strings.WebBrowser_CouldNotRead);
                return;
            }

            var seenTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < result.Count; i++)
            {
                var node = result[i];
                string text = (string)node["text"] ?? "";
                string role = (string)node["role"] ?? "text";
                bool isInteractive = (bool)node["isInteractive"];

                // Deduplicate non-interactive text elements with identical content
                if (!isInteractive && (role == "text" || role == "heading"))
                {
                    if (seenTexts.Contains(text)) continue;
                    seenTexts.Add(text);
                }

                _elements.Add(new WebElement
                {
                    Tag = (string)node["tag"] ?? "",
                    Text = text,
                    Role = role,
                    InputType = (string)node["inputType"] ?? "",
                    Placeholder = (string)node["placeholder"] ?? "",
                    Value = (string)node["value"] ?? "",
                    Index = (int)node["index"],
                    IsInteractive = isInteractive,
                    IsChecked = (bool)node["isChecked"],
                    IsBackToArena = false
                });
            }

            // Append "Back to Arena" as the last element
            if (_backToArenaButton != null)
            {
                _elements.Add(new WebElement
                {
                    Tag = "button",
                    Text = _backToArenaLabel ?? Strings.WebBrowser_PaymentPage,
                    Role = "button",
                    InputType = "",
                    Placeholder = "",
                    Value = "",
                    Index = -1,
                    IsInteractive = true,
                    IsChecked = false,
                    IsBackToArena = true
                });
            }

            // Count web elements (excluding Back to Arena)
            int webElementCount = _elements.Count - (_backToArenaButton != null ? 1 : 0);

            // Check if we found any interactive elements (buttons, links, inputs, etc.)
            bool foundInteractive = false;
            for (int i = 0; i < _elements.Count; i++)
            {
                if (_elements[i].IsInteractive && !_elements[i].IsBackToArena)
                {
                    foundInteractive = true;
                    break;
                }
            }
            _hasInteractiveElements = foundInteractive;

            Log.Msg("WebBrowser", $"Extracted {_elements.Count} elements ({webElementCount} from page, interactive={foundInteractive})");

            // Layer 1: No web elements at all — iframes may still be loading
            if (webElementCount == 0 && !_pendingRescan)
            {
                _emptyRescanCount++;

                // After several failed attempts, run the CAPTCHA probe once per page.
                // _captchaCheckCompleted prevents the probe from re-firing on every
                // subsequent empty rescan (which previously caused a spam loop on
                // pages whose content lives entirely in a cross-origin iframe).
                if (_emptyRescanCount >= MaxEmptyRescansBeforeCheck && !_captchaDetected && !_captchaCheckCompleted)
                {
                    CheckForCaptcha();
                    return;
                }

                // If CAPTCHA already detected, don't keep rescanning
                if (_captchaDetected)
                {
                    return;
                }

                Log.Msg("WebBrowser", "No web elements found, scheduling rescan for iframe content");
                ScheduleRescan(_captchaCheckCompleted ? 4.0f : 1.5f);  // back off once probe is done
                if (!_emptyLoadingAnnounced)
                {
                    _announcer.AnnounceInterrupt(Strings.WebBrowser_ContextLoading(_contextLabel));
                    _emptyLoadingAnnounced = true;
                }
                return;
            }

            // Reset empty counter when we find elements
            _emptyRescanCount = 0;

            // Layer 2: Found text but no interactive elements — page skeleton loaded
            // but dynamic content (buttons, inputs) hasn't rendered yet.
            // Install MutationObserver to detect when they appear.
            if (!foundInteractive && !_mutationObserverActive)
            {
                Log.Msg("WebBrowser", "No interactive elements found, installing MutationObserver to watch for dynamic content");
                _mutationCurrentTimeout = MutationStableTimeout;
                InstallMutationObserver();
                _announcer.AnnounceInterrupt(Strings.WebBrowser_ContextLoading(_contextLabel));
                return;
            }

            // Build content fingerprint to detect AJAX page changes (same element count, different text)
            var fingerprint = ComputeContentFingerprint();

            // If neither count nor content changed, this is a silent rescan — don't re-announce
            if (webElementCount == _lastWebElementCount && fingerprint == _lastContentFingerprint)
            {
                Log.Msg("WebBrowser", "Element count unchanged, silent rescan");
                return;
            }

            bool contentChangedOnly = webElementCount == _lastWebElementCount && fingerprint != _lastContentFingerprint;
            if (contentChangedOnly)
            {
                Log.Msg("WebBrowser", "Element count unchanged but content changed (AJAX update detected)");
            }

            _lastWebElementCount = webElementCount;
            _lastContentFingerprint = fingerprint;

            // If we now have interactive elements, stop the MutationObserver
            if (foundInteractive && _mutationObserverActive)
            {
                Log.Msg("WebBrowser", "Interactive elements found, stopping MutationObserver");
                _mutationObserverActive = false;
            }

            // Reset to first element
            _currentIndex = 0;
            _announcer.AnnounceInterrupt(Strings.WebBrowser_ElementCount(_contextLabel, _elements.Count));

            if (_elements.Count > 0)
            {
                AnnounceCurrentElement();
            }
        }

        private void OnExtractionError(Exception ex)
        {
            _isLoading = false;
            Log.Msg("WebBrowser", $"Extraction error: {ex.Message}");
            _announcer.AnnounceInterrupt(Strings.WebBrowser_CouldNotRead);
        }

        /// <summary>
        /// Build a fingerprint from element texts to detect AJAX content changes
        /// (e.g. payment success pages that replace content without navigation).
        /// </summary>
        private string ComputeContentFingerprint()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _elements.Count; i++)
            {
                if (_elements[i].IsBackToArena) continue;
                sb.Append(_elements[i].Text);
                sb.Append('|');
            }
            return sb.ToString();
        }

        #endregion

        #region MutationObserver

        /// <summary>
        /// Inject a MutationObserver into the page that sets a flag when DOM nodes change.
        /// </summary>
        private void InstallMutationObserver()
        {
            if (_browser == null || !_browser.IsReady) return;

            _browser.EvalJSCSP(WebBrowserScripts.InstallMutationObserverScript)
                .Then(result =>
                {
                    _mutationObserverActive = true;
                    _mutationPollTimer = MutationPollInterval;
                    _mutationStableTime = 0;
                    Log.Msg("WebBrowser", "MutationObserver installed");
                })
                .Catch(ex =>
                {
                    Log.Msg("WebBrowser", $"MutationObserver install error: {ex.Message}");
                });
        }

        /// <summary>
        /// Poll the MutationObserver flag. If DOM changed, re-extract.
        /// If DOM has been stable long enough, stop polling.
        /// </summary>
        private void PollMutationObserver()
        {
            if (_browser == null || !_browser.IsReady || !_mutationObserverActive) return;

            _browser.EvalJSCSP(WebBrowserScripts.PollMutationScript)
                .Then(result =>
                {
                    if (!_mutationObserverActive) return; // Deactivated while polling

                    bool changed = result != null && (bool)result;
                    if (changed)
                    {
                        _mutationStableTime = 0;
                        Log.Msg("WebBrowser", "DOM changed, re-extracting");
                        ExtractElements();
                    }
                    else
                    {
                        _mutationStableTime += MutationPollInterval;
                        if (_mutationStableTime >= _mutationCurrentTimeout)
                        {
                            Log.Msg("WebBrowser", "DOM stable, stopping MutationObserver polling");
                            _mutationObserverActive = false;

                            // If we still have no interactive elements after timeout, warn user
                            if (!_hasInteractiveElements && !_captchaDetected)
                            {
                                CheckForCaptcha();
                            }
                        }
                    }
                })
                .Catch(ex =>
                {
                    Log.Msg("WebBrowser", $"MutationObserver poll error: {ex.Message}");
                });
        }

        #endregion

        #region Page Load Handling

        private void OnPageLoad(JSONNode loadData)
        {
            Log.Msg("WebBrowser", $"Page loaded: {_browser?.Url}");

            if (!_isActive) return;

            // If CAPTCHA was already detected, swallow further page loads silently.
            // PayPal's failed-login flow keeps reloading the same captcha URL ~1×/sec;
            // without this short-circuit the warning would spam on every reload.
            if (_captchaDetected)
            {
                Log.Msg("WebBrowser", "CAPTCHA already detected, ignoring redirect page load");
                return;
            }

            // Check for CAPTCHA / auth-failure URL patterns BEFORE resetting state.
            // PayPal does rapid redirect chains (login → CAPTCHA → back to login) that
            // complete in ~3 seconds. The old approach of waiting for 3 empty rescans
            // (~4.5s) never triggered because each OnPageLoad reset the counter.
            string url = _browser?.Url ?? "";
            if (IsCaptchaUrl(url))
            {
                Log.Msg("WebBrowser", "CAPTCHA/security URL detected on page load, announcing warning");
                _captchaDetected = true;
                _pendingRescan = false;
                _secondRescanTimer = 0;
                _mutationObserverActive = false;
                ResetEditSessionOnPageChange();
                _isLoading = false;
                _elements.Clear();
                _announcer.AnnounceInterrupt(Strings.WebBrowser_CaptchaWarning);
                return;
            }
            if (IsLoginFailureUrl(url))
            {
                // Soft login failure: PayPal re-prompts for credentials on the same
                // login page rather than showing a visual CAPTCHA. Announce a retry
                // hint but let the normal page-load flow continue so the user can
                // re-enter the password.
                Log.Msg("WebBrowser", "Login failure URL detected, announcing retry hint");
                _announcer.AnnounceInterrupt(Strings.WebBrowser_LoginFailed);
            }

            // Cancel any pending rescan timers — they were for the previous page
            _pendingRescan = false;
            _secondRescanTimer = 0;
            _emptyRescanCount = 0;
            _captchaCheckCompleted = false;  // new page → re-allow one CAPTCHA probe
            _emptyLoadingAnnounced = false;  // new page → allow one "loading…" announcement
            _clickCooldownUntil = 0; // New page = new buttons, clear cooldown
            _mutationObserverActive = false; // Will be re-installed after extraction
            _hasInteractiveElements = false;
            _lastWebElementCount = 0;
            _lastContentFingerprint = "";

            ResetEditSessionOnPageChange();
            _isLoading = false; // Reset in case a previous extraction never resolved
            _announcer.AnnounceInterrupt(Strings.WebBrowser_PageLoaded);
            ExtractElements();
        }

        private void ScheduleRescan(float delay)
        {
            _pendingRescan = true;
            _rescanTimer = delay;
        }

        #endregion

        #region Element Navigation

        private void MoveElement(int direction)
        {
            if (_elements.Count == 0) return;

            int newIndex = _currentIndex + direction;

            if (newIndex < 0)
            {
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }
            if (newIndex >= _elements.Count)
            {
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            _currentIndex = newIndex;
            AnnounceCurrentElement();
        }

        /// <summary>
        /// Tab navigation: move to next/previous element, auto-enter edit mode if it's a text field.
        /// Mirrors BaseNavigator behavior where Tab between input fields keeps you in edit mode.
        /// </summary>
        private void TabNavigate(int direction)
        {
            MoveElement(direction);

            if (_currentIndex >= 0 && _currentIndex < _elements.Count)
            {
                var elem = _elements[_currentIndex];
                if (elem.Role == "textbox" && !elem.IsBackToArena)
                {
                    EnterEditMode(elem);
                }
            }
        }

        private void AnnounceCurrentElement()
        {
            if (_currentIndex < 0 || _currentIndex >= _elements.Count) return;

            var elem = _elements[_currentIndex];

            // For text fields, live-read the value from JS (cached value may be stale)
            if (elem.Role == "textbox" && !elem.IsBackToArena && _browser != null)
            {
                int idx = _currentIndex; // capture for closure
                _browser.EvalJSCSP(WebBrowserScripts.ReadValueScript(elem.Index))
                    .Then(result =>
                    {
                        string val = (string)result ?? "";
                        // Update cached value
                        if (idx >= 0 && idx < _elements.Count)
                        {
                            var updated = _elements[idx];
                            updated.Value = val;
                            _elements[idx] = updated;
                        }
                        string announcement = FormatElementAnnouncement(
                            idx < _elements.Count ? _elements[idx] : elem, idx, _elements.Count);
                        _announcer.AnnounceInterrupt(announcement);
                    })
                    .Catch(ex =>
                    {
                        // Fallback to cached value
                        string announcement = FormatElementAnnouncement(elem, idx, _elements.Count);
                        _announcer.AnnounceInterrupt(announcement);
                    });
            }
            else
            {
                string announcement = FormatElementAnnouncement(elem, _currentIndex, _elements.Count);
                _announcer.AnnounceInterrupt(announcement);
            }
        }

        private string FormatElementAnnouncement(WebElement elem, int index, int total)
        {
            string position = Strings.PositionOf(index + 1, total);
            string prefix = position != "" ? $"{position}: " : "";
            string text = elem.Text;
            string roleStr = FormatRole(elem);
            string extra = "";

            // Add value/state information
            switch (elem.Role)
            {
                case "textbox":
                    if (elem.InputType == "password")
                    {
                        extra = string.IsNullOrEmpty(elem.Value)
                            ? $", {Strings.InputFieldEmpty}"
                            : $", {Strings.HasCharacters(elem.Value.Length)}";
                    }
                    else
                    {
                        extra = string.IsNullOrEmpty(elem.Value) ? $", {Strings.InputFieldEmpty}" : $", {elem.Value}";
                    }
                    break;
                case "checkbox":
                case "radio":
                    extra = $", {(elem.IsChecked ? Strings.RoleChecked : Strings.RoleUnchecked)}";
                    break;
                case "combobox":
                    if (!string.IsNullOrEmpty(elem.Value))
                        extra = $", {elem.Value}";
                    break;
            }

            // For text/heading, omit role — just announce content
            if (elem.Role == "text" || elem.Role == "heading")
                return $"{prefix}{text}";

            return $"{prefix}{text}, {roleStr}{extra}";
        }

        private string FormatRole(WebElement elem)
        {
            switch (elem.Role)
            {
                case "button": return Strings.RoleButton;
                case "link": return Strings.RoleLink;
                case "textbox":
                    if (elem.InputType == "password") return Strings.RolePasswordField;
                    if (elem.InputType == "email") return Strings.RoleEmailField;
                    if (elem.InputType == "number") return Strings.RoleNumberField;
                    return Strings.TextField;
                case "combobox": return Strings.RoleDropdown;
                case "checkbox": return Strings.RoleCheckbox;
                case "radio": return Strings.RoleRadioButton;
                case "heading": return Strings.RoleHeading;
                case "text": return Strings.RoleText;
                case "tab": return Strings.RoleTab;
                case "menuitem": return Strings.RoleMenuItem;
                default: return elem.Role;
            }
        }

        #endregion

        #region Element Activation

        private void ActivateCurrentElement()
        {
            if (_currentIndex < 0 || _currentIndex >= _elements.Count) return;

            var elem = _elements[_currentIndex];

            // "Back to Arena" Unity button
            if (elem.IsBackToArena)
            {
                ClickBackToArena();
                return;
            }

            Log.Msg("WebBrowser", $"Activating element {elem.Index}: {elem.Text} ({elem.Role})");

            switch (elem.Role)
            {
                case "textbox":
                    EnterEditMode(elem);
                    break;

                case "checkbox":
                case "radio":
                    ClickElement(elem);
                    StartClickCooldown(CheckboxCooldownSeconds);
                    ScheduleRescan(RescanDelayCheckbox);
                    break;

                case "button":
                case "link":
                case "tab":
                case "menuitem":
                    ClickElement(elem);
                    StartClickCooldown(ClickCooldownSeconds);
                    ScheduleRescan(RescanDelayClick);
                    _secondRescanTimer = RescanDelaySecond;
                    // Monitor for slow AJAX transitions (e.g. payment processing can take 30+ seconds).
                    // The two fixed rescans at 1.2s and 3.0s catch fast transitions; the MutationObserver
                    // catches slow ones by polling for DOM changes until the page is stable.
                    _mutationCurrentTimeout = MutationStableTimeoutPostClick;
                    _mutationStableTime = 0; // Reset in case observer was already running
                    InstallMutationObserver();
                    break;

                case "combobox":
                    // Click to open native dropdown, let browser handle arrow keys
                    ClickElement(elem);
                    StartClickCooldown(ClickCooldownSeconds);
                    _announcer.AnnounceInterrupt(Strings.DropdownOpened);
                    break;

                default:
                    // Non-interactive element — just re-announce
                    AnnounceCurrentElement();
                    break;
            }
        }

        private void EnterEditMode(WebElement elem)
        {
            _isEditingField = true;
            _editFieldValue = elem.Value ?? "";
            _editCursorPos = _editFieldValue.Length > 0 ? _editFieldValue.Length - 1 : 0;

            // Passthrough always for password fields. Additionally, on PayPal login pages
            // the email/username field is also a React-controlled input that rejects the
            // JS execCommand path (same failure mode as the password field: DOM updates,
            // React state stays empty, server rejects with invalid_input + adsddcaptcha).
            // Scoped strictly to PayPal login URLs so card-entry forms on Xsolla and other
            // checkout pages keep the JS path with per-character echo.
            bool isPasswordField = elem.InputType == "password";
            bool isPayPalLoginText = IsPayPalLoginPage(_browser?.Url)
                && (elem.InputType == "email" || elem.InputType == "text" || elem.InputType == "tel");
            _passthroughMode = isPasswordField || isPayPalLoginText;

            if (_passthroughMode)
            {
                // Passthrough: keep enableInput=true and select the browser GameObject so
                // PointerUIGUI's OnGUI/KeyboardHasFocus path is live. Unity forwards physical
                // keystrokes directly to CEF as native (isTrusted=true) events — no JS layer
                // that bot-protected sites (PayPal) reject. We lose character echo during
                // typing, but arrow keys still read the field value back.
                if (_browserInputForwarder != null)
                {
                    _browserInputForwarder.enableInput = true;
                    var es = UnityEngine.EventSystems.EventSystem.current;
                    if (es != null)
                        es.SetSelectedGameObject(_browserInputForwarder.gameObject);
                }
                string reason = isPasswordField ? "password" : "paypal-login-text";
                Log.Msg("WebBrowser", $"Passthrough edit mode ({reason}): element {elem.Index} ({elem.Text})");
            }
            else
            {
                // Non-password fields use the JS input path — disable Unity forwarding so
                // keystrokes don't get delivered twice (once by PointerUIGUI and once by us).
                if (_browserInputForwarder != null)
                    _browserInputForwarder.enableInput = false;
            }

            // Focus the element in the browser (works for both modes)
            _browser.EvalJSCSP(WebBrowserScripts.FocusScript(elem.Index))
                .Catch(ex => Log.Msg("WebBrowser", $"Focus error: {ex.Message}"));

            // Use the real role label (RolePasswordField / RoleEmailField / RoleNumberField / TextField)
            // so passthrough on email still announces "E-Mail-Feld", not "Passwortfeld".
            string fieldType = FormatRole(elem);
            _announcer.AnnounceInterrupt(Strings.WebBrowser_Editing(elem.Text, fieldType));
        }

        private void ClickElement(WebElement elem)
        {
            _browser.EvalJSCSP(WebBrowserScripts.ClickScript(elem.Index))
                .Then(result =>
                {
                    string res = (string)result;
                    if (res == "not_found")
                    {
                        Log.Msg("WebBrowser", $"Element {elem.Index} not found for click");
                        _announcer.AnnounceInterrupt(Strings.WebBrowser_ElementNotFound);
                        ScheduleRescan(0.2f);
                    }
                    else
                    {
                        Log.Msg("WebBrowser", $"Clicked element {elem.Index}: {elem.Text}");
                    }
                })
                .Catch(ex =>
                {
                    Log.Msg("WebBrowser", $"Click error: {ex.Message}");
                });
        }

        #endregion

        #region CAPTCHA Detection

        /// <summary>
        /// Quick URL-based check for CAPTCHA / security verification pages.
        /// Called on every page load for immediate detection.
        /// </summary>
        private static bool IsCaptchaUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            string lower = url.ToLowerInvariant();

            // Generic CAPTCHA / challenge page patterns (single keyword is too broad,
            // but these specific paths are reliable indicators)
            if (lower.Contains("/challenge") || lower.Contains("/captcha"))
                return true;

            return false;
        }

        /// <summary>
        /// URL patterns PayPal uses when credentials were rejected but the user is
        /// being bounced back to the login page to retry (not a visual CAPTCHA).
        /// These used to be treated as hard CAPTCHA but that produced a false warning
        /// — the user saw another login page, not a challenge.
        /// </summary>
        private static bool IsLoginFailureUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            string lower = url.ToLowerInvariant();

            // PayPal: Base64 "adsddcaptcha" param added to failed login redirects
            // e.g. &YWRzZGRjYXB0Y2hh=1
            if (lower.Contains("ywrzzgrjyxb0y2hh"))
                return true;

            // PayPal: step-up auth flow combined with login failure
            // e.g. /signin/return?flowFrom=anw-stepup&...&failedBecause=invalid_input
            if (lower.Contains("stepup") && lower.Contains("failedbecause"))
                return true;

            return false;
        }

        // PayPal login-form hosts where the email field is a React-controlled input.
        // Typing via execCommand('insertText') updates the DOM but PayPal's React state
        // stays empty → server rejects with failedBecause=invalid_input + adsddcaptcha
        // regardless of the password. On these URLs the email field must take the same
        // passthrough path as the password field. Scoped narrowly so unrelated paypal.com
        // pages (and other checkout hosts like Xsolla card forms) keep the JS input path.
        private static bool IsPayPalLoginPage(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            string lower = url.ToLowerInvariant();
            if (!lower.Contains("paypal.com")) return false;
            return lower.Contains("/signin")
                || lower.Contains("/agreements/approve")
                || lower.Contains("/webapps/hermes")
                || lower.Contains("/checkoutweb");
        }

        private void CheckForCaptcha()
        {
            Log.Msg("WebBrowser", "Checking for CAPTCHA / security verification...");

            // Check the URL for known security step-up patterns
            string url = _browser?.Url?.ToLowerInvariant() ?? "";
            bool urlSuspicious = IsCaptchaUrl(url) ||
                                 url.Contains("authflow") || url.Contains("challenge") ||
                                 url.Contains("stepup");

            // Inspect iframe sources for known user-facing CAPTCHA vendors only.
            // Bare "cross-origin iframe present" was too broad: PayPal silently embeds
            // geo.ddc.paypal.com/captcha/ for bot fingerprinting on every login load,
            // which falsely tripped the warning before the user could enter credentials.
            _browser.EvalJSCSP(WebBrowserScripts.DetectCrossOriginIframesScript)
                .Then(result =>
                {
                    string json = (string)result ?? "";
                    bool hasUserFacingCaptchaIframe = ContainsUserFacingCaptchaVendor(json);

                    Log.Msg("WebBrowser", $"CAPTCHA check: urlSuspicious={urlSuspicious}, vendorIframe={hasUserFacingCaptchaIframe}, details={json}");

                    _captchaCheckCompleted = true;
                    if (urlSuspicious || hasUserFacingCaptchaIframe)
                    {
                        _captchaDetected = true;
                        Log.Msg("WebBrowser", "CAPTCHA detected! Stopping rescan loop.");
                        _announcer.AnnounceInterrupt(Strings.WebBrowser_CaptchaWarning);
                    }
                    else
                    {
                        // Not a CAPTCHA — back off to a slow poll and let the
                        // MutationObserver / next OnPageLoad pick up real content.
                        Log.Msg("WebBrowser", "No CAPTCHA indicators found, backing off rescan loop");
                        ScheduleRescan(4.0f);
                        if (!_emptyLoadingAnnounced)
                        {
                            _announcer.AnnounceInterrupt(Strings.WebBrowser_ContextLoading(_contextLabel));
                            _emptyLoadingAnnounced = true;
                        }
                    }
                })
                .Catch(ex =>
                {
                    Log.Msg("WebBrowser", $"CAPTCHA detection error: {ex.Message}");
                    _captchaCheckCompleted = true;
                    // If the URL alone was suspicious, still warn
                    if (urlSuspicious)
                    {
                        _captchaDetected = true;
                        _announcer.AnnounceInterrupt(Strings.WebBrowser_CaptchaWarning);
                    }
                    else
                    {
                        ScheduleRescan(4.0f);
                        if (!_emptyLoadingAnnounced)
                        {
                            _announcer.AnnounceInterrupt(Strings.WebBrowser_ContextLoading(_contextLabel));
                            _emptyLoadingAnnounced = true;
                        }
                    }
                });
        }

        // Hostnames/path tokens for CAPTCHA iframes.
        // This check only runs once per page from CheckForCaptcha(), which is gated on
        // "no extractable elements after ~4.5s of rescans". That gate means the iframe
        // in question IS the page content, so a matching token indicates a user-facing
        // blocker — not a silent background script. Keeps third-party vendors AND
        // PayPal's ddc/captcha endpoint (served directly when PayPal flags the session;
        // verified via user OCR showing "confirm you're human" on /agreements/approve).
        private static readonly string[] UserFacingCaptchaTokens =
        {
            "recaptcha",       // google reCAPTCHA (recaptcha.net, google.com/recaptcha, gstatic.com/recaptcha)
            "hcaptcha",        // hCaptcha (hcaptcha.com, newassets.hcaptcha.com)
            "arkoselabs",      // Arkose Labs / FunCaptcha
            "funcaptcha",
            "challenges.cloudflare.com",  // Cloudflare Turnstile / challenge platform
            "turnstile",
            "ddc.paypal.com/captcha",     // PayPal's device-data-collection captcha (blocker variant)
        };

        private static bool ContainsUserFacingCaptchaVendor(string iframeJson)
        {
            if (string.IsNullOrEmpty(iframeJson)) return false;
            string lower = iframeJson.ToLowerInvariant();
            foreach (var token in UserFacingCaptchaTokens)
            {
                if (lower.Contains(token)) return true;
            }
            return false;
        }

        #endregion

        #region Back to Arena

        private void FindBackToArenaButton(GameObject panel)
        {
            _backToArenaButton = null;
            _backToArenaLabel = null;

            // Look for a Unity Button that isn't part of the Browser itself
            // Typically labeled "Back" or has a back/close icon
            var buttons = panel.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (btn == null || !btn.gameObject.activeInHierarchy || !btn.interactable) continue;

                // Skip buttons that are children of the Browser's RawImage/render surface
                if (_browser != null && btn.transform.IsChildOf(_browser.transform)) continue;

                string name = btn.gameObject.name.ToLowerInvariant();
                string label = UITextExtractor.GetText(btn.gameObject) ?? "";
                string labelLower = label.ToLowerInvariant();

                if (name.Contains("back") || name.Contains("close") || name.Contains("return") ||
                    labelLower.Contains("back") || labelLower.Contains("close") || labelLower.Contains("return") ||
                    labelLower.Contains("arena"))
                {
                    _backToArenaButton = btn.gameObject;
                    _backToArenaLabel = !string.IsNullOrEmpty(label) ? label : btn.gameObject.name;
                    Log.Msg("WebBrowser", $"Found Back to Arena button: {btn.gameObject.name}, label: {_backToArenaLabel}");
                    break;
                }
            }

            // Fallback: take the first non-browser button
            if (_backToArenaButton == null && buttons.Length > 0)
            {
                foreach (var btn in buttons)
                {
                    if (btn == null || !btn.gameObject.activeInHierarchy || !btn.interactable) continue;
                    if (_browser != null && btn.transform.IsChildOf(_browser.transform)) continue;

                    _backToArenaButton = btn.gameObject;
                    string label = UITextExtractor.GetText(btn.gameObject) ?? "";
                    _backToArenaLabel = !string.IsNullOrEmpty(label) ? label : btn.gameObject.name;
                    Log.Msg("WebBrowser", $"Using fallback Back button: {btn.gameObject.name}, label: {_backToArenaLabel}");
                    break;
                }
            }
        }

        private void ClickBackToArena()
        {
            if (_backToArenaButton != null)
            {
                Log.Msg("WebBrowser", "Clicking Back to Arena");
                _announcer.AnnounceInterrupt(_backToArenaLabel ?? Strings.WebBrowser_PaymentPage);
                UIActivator.Activate(_backToArenaButton);
            }
            else
            {
                Log.Msg("WebBrowser", "No Back to Arena button found");
                _announcer.AnnounceInterrupt(Strings.WebBrowser_NoBackButton);
            }
        }

        #endregion
    }
}
