using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using ZenFulcrum.EmbeddedBrowser;

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
        private GameObject _browserPanel;
        private IAnnouncementService _announcer;
        private bool _isActive;

        private List<WebElement> _elements = new List<WebElement>();
        private int _currentIndex;
        private bool _isEditingField;
        private bool _isLoading;
        private int _lastWebElementCount; // Track for silent rescan comparison

        // Rescan timers for detecting page changes after clicks
        private bool _pendingRescan;
        private float _rescanTimer;
        private float _secondRescanTimer; // Second delayed rescan for slow transitions
        private float _extractionStartTime; // For timeout detection

        // "Back to Arena" button (Unity button outside the browser)
        private GameObject _backToArenaButton;

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

        #region JavaScript

        // Script injected into the page to extract all navigable elements.
        // Used with EvalJSCSP (which wraps in IIFE) to bypass CSP eval restrictions.
        // Scans main document AND same-origin iframes (recursively).
        private const string ExtractionScript = @"
    var results = [];
    var idx = 0;
    var selectors = 'button, a, input, select, textarea, h1, h2, h3, h4, h5, h6, p, label, span, div, [role=""button""], [role=""link""], [role=""checkbox""], [role=""tab""], [role=""menuitem""]';

    function scanDocument(doc) {
        var allEls;
        try { allEls = doc.querySelectorAll(selectors); } catch(e) { return; }
        var interactiveParents = new Set();

        for (var i = 0; i < allEls.length; i++) {
            var el = allEls[i];
            var tag = el.tagName.toLowerCase();
            if (tag === 'button' || tag === 'a' || tag === 'input' || tag === 'select' || tag === 'textarea' || el.getAttribute('role') === 'button' || el.getAttribute('role') === 'link' || el.getAttribute('role') === 'checkbox') {
                interactiveParents.add(el);
            }
        }

        for (var i = 0; i < allEls.length; i++) {
            var el = allEls[i];

            var rect = el.getBoundingClientRect();
            if (rect.width === 0 && rect.height === 0) continue;
            var style;
            try { style = doc.defaultView.getComputedStyle(el); } catch(e) { continue; }
            if (style.display === 'none' || style.visibility === 'hidden' || style.opacity === '0') continue;

            var tag = el.tagName.toLowerCase();
            var role = el.getAttribute('role') || '';
            var text = '';
            var inputType = '';
            var placeholder = '';
            var value = '';
            var isInteractive = false;
            var isChecked = false;

            if (tag === 'button' || role === 'button') {
                role = 'button';
                isInteractive = true;
                text = el.textContent.trim() || el.getAttribute('aria-label') || el.title || '';
            } else if (tag === 'a' || role === 'link') {
                role = 'link';
                isInteractive = true;
                text = el.textContent.trim() || el.getAttribute('aria-label') || '';
            } else if (tag === 'input') {
                inputType = (el.type || 'text').toLowerCase();
                if (inputType === 'hidden') continue;
                if (inputType === 'checkbox' || role === 'checkbox') {
                    role = 'checkbox';
                    isChecked = el.checked;
                    text = el.getAttribute('aria-label') || '';
                    if (!text) {
                        var lbl = el.closest('label') || doc.querySelector('label[for=""' + el.id + '""]');
                        if (lbl) text = lbl.textContent.trim();
                    }
                } else if (inputType === 'submit' || inputType === 'button') {
                    role = 'button';
                    text = el.value || el.getAttribute('aria-label') || '';
                } else if (inputType === 'radio') {
                    role = 'radio';
                    isChecked = el.checked;
                    text = el.getAttribute('aria-label') || '';
                    if (!text) {
                        var lbl = el.closest('label') || doc.querySelector('label[for=""' + el.id + '""]');
                        if (lbl) text = lbl.textContent.trim();
                    }
                } else {
                    role = 'textbox';
                    placeholder = el.placeholder || '';
                    value = el.value || '';
                    text = el.getAttribute('aria-label') || '';
                    // Try label[for=id]
                    if (!text && el.id) {
                        var lbl = doc.querySelector('label[for=""' + el.id + '""]');
                        if (lbl) text = lbl.textContent.trim();
                    }
                    // Try wrapping label
                    if (!text) {
                        var lbl = el.closest('label');
                        if (lbl) text = lbl.textContent.trim();
                    }
                    // Try preceding sibling label/span (common in Xsolla forms)
                    if (!text) {
                        var prev = el.previousElementSibling;
                        if (prev && (prev.tagName === 'LABEL' || prev.tagName === 'SPAN' || prev.tagName === 'DIV')) {
                            var lt = prev.textContent.trim();
                            if (lt && lt.length < 60) text = lt;
                        }
                    }
                    // Try parent's label child before this input
                    if (!text && el.parentElement) {
                        var siblings = el.parentElement.children;
                        for (var si = 0; si < siblings.length; si++) {
                            if (siblings[si] === el) break;
                            var st = siblings[si].textContent.trim();
                            if (st && st.length < 60) text = st;
                        }
                    }
                    // Fallback to placeholder or name attribute
                    if (!text) text = placeholder || el.getAttribute('name') || '';
                }
                isInteractive = true;
            } else if (tag === 'select') {
                role = 'combobox';
                isInteractive = true;
                text = el.getAttribute('aria-label') || '';
                value = el.options[el.selectedIndex] ? el.options[el.selectedIndex].text : '';
                if (!text) {
                    var lbl = doc.querySelector('label[for=""' + el.id + '""]');
                    if (lbl) text = lbl.textContent.trim();
                }
            } else if (tag === 'textarea') {
                role = 'textbox';
                isInteractive = true;
                placeholder = el.placeholder || '';
                value = el.value || '';
                text = el.getAttribute('aria-label') || placeholder || '';
            } else if (/^h[1-6]$/.test(tag)) {
                role = 'heading';
                text = el.textContent.trim();
            } else if (role === 'checkbox') {
                isInteractive = true;
                isChecked = el.getAttribute('aria-checked') === 'true';
                text = el.textContent.trim() || el.getAttribute('aria-label') || '';
            } else if (role === 'tab' || role === 'menuitem') {
                isInteractive = true;
                text = el.textContent.trim() || el.getAttribute('aria-label') || '';
            } else {
                var isChildOfInteractive = false;
                var parent = el.parentElement;
                while (parent) {
                    if (interactiveParents.has(parent)) { isChildOfInteractive = true; break; }
                    parent = parent.parentElement;
                }
                if (isChildOfInteractive) continue;

                text = el.textContent.trim();
                if (!text || text.length < 2) continue;
                var children = el.querySelectorAll('button, a, input, select, textarea, [role=""button""]');
                if (children.length > 0) continue;
                role = 'text';
            }

            if (!text && !isInteractive) continue;
            if (!text) text = tag;

            el.setAttribute('data-aa-idx', idx.toString());

            results.push({
                tag: tag,
                text: text.substring(0, 200),
                role: role || tag,
                inputType: inputType,
                placeholder: placeholder,
                value: value.substring(0, 100),
                index: idx,
                isInteractive: isInteractive,
                isChecked: isChecked
            });
            idx++;
        }

        // Recurse into same-origin iframes
        var iframes;
        try { iframes = doc.querySelectorAll('iframe'); } catch(e) { return; }
        for (var f = 0; f < iframes.length; f++) {
            try {
                var iframeDoc = iframes[f].contentDocument;
                if (iframeDoc) scanDocument(iframeDoc);
            } catch(e) { /* cross-origin, skip */ }
        }
    }

    try { scanDocument(document); } catch(e) {}
    return results;
";

        // Helper JS function to find element by data-aa-idx across main doc + iframes
        private const string FindElementFunc = @"
            function findEl(idx) {
                var el = document.querySelector('[data-aa-idx=""' + idx + '""]');
                if (el) return el;
                var iframes = document.querySelectorAll('iframe');
                for (var i = 0; i < iframes.length; i++) {
                    try {
                        var d = iframes[i].contentDocument;
                        if (d) { el = d.querySelector('[data-aa-idx=""' + idx + '""]'); if (el) return el; }
                    } catch(e) {}
                }
                return null;
            }";

        // Click an element by its data-aa-idx attribute
        // Used with EvalJSCSP — script is a function body (no IIFE wrapper needed)
        private static string ClickScript(int index)
        {
            return FindElementFunc + $" var el = findEl({index}); if (el) {{ el.click(); return 'ok'; }} return 'not_found';";
        }

        // Focus an element by its data-aa-idx
        private static string FocusScript(int index)
        {
            return FindElementFunc + $" var el = findEl({index}); if (el) {{ el.focus(); return 'ok'; }} return 'not_found';";
        }

        // Read current value of a text input by its data-aa-idx
        private static string ReadValueScript(int index)
        {
            return FindElementFunc + $@"
                var el = findEl({index});
                if (!el) return '';
                try {{ return el.value || ''; }} catch(e) {{ return ''; }}";
        }

        // Append text to an input field via execCommand (most compatible approach)
        // Works across iframes and with all frameworks (React, Angular, etc.)
        private static string AppendTextScript(int index, string text)
        {
            string escaped = text.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "");
            return FindElementFunc + $@"
                var el = findEl({index});
                if (!el) return 'not_found';
                el.focus();
                var doc = el.ownerDocument;
                var ok = false;
                try {{ ok = doc.execCommand('insertText', false, '{escaped}'); }} catch(e) {{}}
                if (!ok) {{
                    try {{ el.value = (el.value || '') + '{escaped}'; }} catch(e) {{}}
                    try {{
                        el.dispatchEvent(new Event('input', {{bubbles: true}}));
                        el.dispatchEvent(new Event('change', {{bubbles: true}}));
                    }} catch(e) {{}}
                }}
                try {{ return el.value || ''; }} catch(e) {{ return 'typed'; }}";
        }

        // Delete last character from input field via execCommand
        private static string BackspaceScript(int index)
        {
            return FindElementFunc + $@"
                var el = findEl({index});
                if (!el) return 'not_found';
                el.focus();
                var doc = el.ownerDocument;
                el.selectionStart = el.selectionEnd = (el.value || '').length;
                var ok = false;
                try {{ ok = doc.execCommand('delete', false); }} catch(e) {{}}
                if (!ok) {{
                    try {{ el.value = (el.value || '').slice(0, -1); }} catch(e) {{}}
                    try {{
                        el.dispatchEvent(new Event('input', {{bubbles: true}}));
                        el.dispatchEvent(new Event('change', {{bubbles: true}}));
                    }} catch(e) {{}}
                }}
                try {{ return el.value || ''; }} catch(e) {{ return ''; }}";
        }

        // Submit form via JS (simulates Enter on input)
        // Uses element's own window context for Event constructors (cross-iframe support)
        private static string SubmitScript(int index)
        {
            return FindElementFunc + $@"
                var el = findEl({index});
                if (!el) return 'not_found';
                var win = el.ownerDocument.defaultView || window;
                var form = el.closest('form');
                if (form) {{
                    var submitBtn = form.querySelector('button[type=""submit""], input[type=""submit""]');
                    if (submitBtn) {{ submitBtn.click(); return 'clicked_submit'; }}
                    form.dispatchEvent(new win.Event('submit', {{bubbles: true, cancelable: true}}));
                    return 'form_submit';
                }}
                el.dispatchEvent(new win.KeyboardEvent('keydown', {{key: 'Enter', code: 'Enter', keyCode: 13, bubbles: true}}));
                el.dispatchEvent(new win.KeyboardEvent('keypress', {{key: 'Enter', code: 'Enter', keyCode: 13, bubbles: true}}));
                el.dispatchEvent(new win.KeyboardEvent('keyup', {{key: 'Enter', code: 'Enter', keyCode: 13, bubbles: true}}));
                return 'key_enter';";
        }

        #endregion

        #region Public API

        public bool IsActive => _isActive;

        /// <summary>
        /// Activate browser accessibility for the given panel.
        /// Finds the Browser component and starts element extraction.
        /// </summary>
        public void Activate(GameObject panel, IAnnouncementService announcer)
        {
            _browserPanel = panel;
            _announcer = announcer;
            _currentIndex = 0;
            _isEditingField = false;
            _isLoading = true;
            _pendingRescan = false;
            _secondRescanTimer = 0;
            _elements.Clear();

            // Find ZFBrowser.Browser component
            _browser = panel.GetComponentInChildren<Browser>(true);
            if (_browser == null)
            {
                MelonLogger.Msg("[WebBrowser] No Browser component found in panel");
                _announcer.AnnounceInterrupt("Browser panel opened. No browser found.");
                _isActive = false;
                return;
            }

            _isActive = true;

            // Find "Back to Arena" button (Unity Button outside/alongside the browser)
            FindBackToArenaButton(panel);

            // Subscribe to page load events
            _browser.onLoad += OnPageLoad;

            MelonLogger.Msg($"[WebBrowser] Activated. Browser URL: {_browser.Url}, IsLoaded: {_browser.IsLoaded}");

            if (_browser.IsLoaded)
            {
                _announcer.AnnounceInterrupt("Payment page. Loading elements...");
                ExtractElements();
            }
            else
            {
                _announcer.AnnounceInterrupt("Payment page loading...");
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

            _browser = null;
            _browserPanel = null;
            _isActive = false;
            _isEditingField = false;
            _isLoading = false;
            _pendingRescan = false;
            _secondRescanTimer = 0;
            _elements.Clear();
            _backToArenaButton = null;

            MelonLogger.Msg("[WebBrowser] Deactivated");
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

            // Extraction timeout — if Promise never resolved, reset and retry
            if (_isLoading && Time.realtimeSinceStartup - _extractionStartTime > 5f)
            {
                MelonLogger.Msg("[WebBrowser] Extraction timed out, resetting");
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

            // Second rescan timer (catches slow page transitions)
            if (_secondRescanTimer > 0)
            {
                _secondRescanTimer -= Time.deltaTime;
                if (_secondRescanTimer <= 0)
                {
                    ExtractElements();
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

            if (_isEditingField)
            {
                HandleEditModeInput();
            }
            else
            {
                HandleNavigationInput();
            }
        }

        #endregion

        #region Navigation Input

        private void HandleNavigationInput()
        {
            // Up/Down, W/S navigate elements
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                MoveElement(-1);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                MoveElement(1);
                return;
            }

            // Tab/Shift+Tab
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                MoveElement(shift ? -1 : 1);
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
            // Escape — exit edit mode
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ExitEditMode();
                _announcer.AnnounceInterrupt("Stopped editing");
                return;
            }

            // Tab — exit edit mode and move to next element
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                ExitEditMode();
                MoveElement(shift ? -1 : 1);
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
                    _browser.EvalJSCSP(SubmitScript(elem.Index))
                        .Catch(ex => MelonLogger.Msg($"[WebBrowser] Submit error: {ex.Message}"));
                }
                ExitEditMode();
                _announcer.AnnounceInterrupt("Submitted");
                ScheduleRescan(RescanDelayClick);
                _secondRescanTimer = RescanDelaySecond;
                return;
            }

            // Arrow Up/Down — read current field value
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                ReadCurrentFieldValue();
                return;
            }

            // Arrow Left/Right — read value (cursor movement not supported via JS)
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                ReadCurrentFieldValue();
                return;
            }

            // Backspace — delete last character via JS
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                if (_currentIndex >= 0 && _currentIndex < _elements.Count)
                {
                    var elem = _elements[_currentIndex];
                    _browser.EvalJSCSP(BackspaceScript(elem.Index))
                        .Then(result =>
                        {
                            string val = (string)result;
                            if (val == "not_found")
                                _announcer.AnnounceInterrupt("Field not found");
                        })
                        .Catch(ex => MelonLogger.Msg($"[WebBrowser] Backspace error: {ex.Message}"));
                }
                return;
            }

            // Printable characters — append via JS
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
                    _browser.EvalJSCSP(AppendTextScript(elem.Index, filtered.ToString()))
                        .Catch(ex => MelonLogger.Msg($"[WebBrowser] TypeText error: {ex.Message}"));
                }
            }
        }

        private void ExitEditMode()
        {
            _isEditingField = false;
        }

        private void ReadCurrentFieldValue()
        {
            if (_currentIndex < 0 || _currentIndex >= _elements.Count) return;
            var elem = _elements[_currentIndex];

            _browser.EvalJSCSP(ReadValueScript(elem.Index))
                .Then(result =>
                {
                    string val = (string)result;
                    if (string.IsNullOrEmpty(val))
                    {
                        _announcer.AnnounceInterrupt(Strings.InputFieldEmpty);
                    }
                    else if (elem.InputType == "password")
                    {
                        _announcer.AnnounceInterrupt($"{val.Length} characters");
                    }
                    else
                    {
                        _announcer.AnnounceInterrupt(val);
                    }
                })
                .Catch(ex =>
                {
                    MelonLogger.Msg($"[WebBrowser] Error reading field value: {ex.Message}");
                });
        }

        #endregion

        #region Element Extraction

        private void ExtractElements()
        {
            if (_browser == null || !_browser.IsReady)
            {
                MelonLogger.Msg("[WebBrowser] Cannot extract - browser not ready");
                return;
            }

            if (_isLoading)
            {
                MelonLogger.Msg("[WebBrowser] Extraction already in progress, skipping");
                return;
            }

            _isLoading = true;
            _extractionStartTime = Time.realtimeSinceStartup;
            MelonLogger.Msg("[WebBrowser] Extracting page elements...");

            _browser.EvalJSCSP(ExtractionScript)
                .Then(result => OnElementsExtracted(result))
                .Catch(ex => OnExtractionError(ex));
        }

        private void OnElementsExtracted(JSONNode result)
        {
            _elements.Clear();
            _isLoading = false;

            if (result == null || result.Type != JSONNode.NodeType.Array)
            {
                MelonLogger.Msg($"[WebBrowser] Extraction returned non-array: {result?.Type}");
                _announcer.AnnounceInterrupt("Could not read page elements.");
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
                    Text = "Back to Arena",
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
            MelonLogger.Msg($"[WebBrowser] Extracted {_elements.Count} elements ({webElementCount} from page)");

            // If we found no web elements, iframes may still be loading — schedule retry
            if (webElementCount == 0 && !_pendingRescan)
            {
                MelonLogger.Msg("[WebBrowser] No web elements found, scheduling rescan for iframe content");
                ScheduleRescan(1.5f);
                _announcer.AnnounceInterrupt("Payment page loading...");
                return;
            }

            // If element count hasn't changed, this is likely a silent rescan — don't re-announce
            if (webElementCount == _lastWebElementCount)
            {
                MelonLogger.Msg("[WebBrowser] Element count unchanged, silent rescan");
                return;
            }

            _lastWebElementCount = webElementCount;

            // Reset to first element
            _currentIndex = 0;
            _announcer.AnnounceInterrupt($"Payment page. {_elements.Count} elements.");

            if (_elements.Count > 0)
            {
                AnnounceCurrentElement();
            }
        }

        private void OnExtractionError(Exception ex)
        {
            _isLoading = false;
            MelonLogger.Msg($"[WebBrowser] Extraction error: {ex.Message}");
            _announcer.AnnounceInterrupt("Could not read page elements.");
        }

        #endregion

        #region Page Load Handling

        private void OnPageLoad(JSONNode loadData)
        {
            MelonLogger.Msg($"[WebBrowser] Page loaded: {_browser?.Url}");

            if (!_isActive) return;

            // Cancel any pending rescan timers — they were for the previous page
            _pendingRescan = false;
            _secondRescanTimer = 0;

            _isEditingField = false;
            _isLoading = false; // Reset in case a previous extraction never resolved
            _announcer.AnnounceInterrupt("Page loaded. Reading elements...");
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
                _announcer.Announce(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }
            if (newIndex >= _elements.Count)
            {
                _announcer.Announce(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            _currentIndex = newIndex;
            AnnounceCurrentElement();
        }

        private void AnnounceCurrentElement()
        {
            if (_currentIndex < 0 || _currentIndex >= _elements.Count) return;

            var elem = _elements[_currentIndex];
            string announcement = FormatElementAnnouncement(elem, _currentIndex, _elements.Count);
            _announcer.AnnounceInterrupt(announcement);
        }

        private string FormatElementAnnouncement(WebElement elem, int index, int total)
        {
            string position = $"{index + 1} of {total}";
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
                            ? ", empty"
                            : $", has {elem.Value.Length} characters";
                    }
                    else
                    {
                        extra = string.IsNullOrEmpty(elem.Value) ? ", empty" : $", {elem.Value}";
                    }
                    break;
                case "checkbox":
                case "radio":
                    extra = elem.IsChecked ? ", checked" : ", unchecked";
                    break;
                case "combobox":
                    if (!string.IsNullOrEmpty(elem.Value))
                        extra = $", {elem.Value}";
                    break;
            }

            // For text/heading, omit role — just announce content
            if (elem.Role == "text" || elem.Role == "heading")
                return $"{position}: {text}";

            return $"{position}: {text}, {roleStr}{extra}";
        }

        private string FormatRole(WebElement elem)
        {
            switch (elem.Role)
            {
                case "button": return "button";
                case "link": return "link";
                case "textbox":
                    if (elem.InputType == "password") return "password field";
                    if (elem.InputType == "email") return "email field";
                    if (elem.InputType == "number") return "number field";
                    return "text field";
                case "combobox": return "dropdown";
                case "checkbox": return "checkbox";
                case "radio": return "radio button";
                case "heading": return "heading";
                case "text": return "text";
                case "tab": return "tab";
                case "menuitem": return "menu item";
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

            MelonLogger.Msg($"[WebBrowser] Activating element {elem.Index}: {elem.Text} ({elem.Role})");

            switch (elem.Role)
            {
                case "textbox":
                    EnterEditMode(elem);
                    break;

                case "checkbox":
                case "radio":
                    ClickElement(elem);
                    ScheduleRescan(RescanDelayCheckbox);
                    break;

                case "button":
                case "link":
                case "tab":
                case "menuitem":
                    ClickElement(elem);
                    ScheduleRescan(RescanDelayClick);
                    _secondRescanTimer = RescanDelaySecond;
                    break;

                case "combobox":
                    // Click to open native dropdown, let browser handle arrow keys
                    ClickElement(elem);
                    _announcer.AnnounceInterrupt($"Dropdown opened. Use arrow keys to select, Enter to confirm.");
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

            // Focus the element in the browser
            _browser.EvalJSCSP(FocusScript(elem.Index))
                .Catch(ex => MelonLogger.Msg($"[WebBrowser] Focus error: {ex.Message}"));

            string fieldType = elem.InputType == "password" ? "password field" : "text field";
            _announcer.AnnounceInterrupt($"Editing {elem.Text}, {fieldType}. Type to enter text, Escape to exit.");
        }

        private void ClickElement(WebElement elem)
        {
            _browser.EvalJSCSP(ClickScript(elem.Index))
                .Then(result =>
                {
                    string res = (string)result;
                    if (res == "not_found")
                    {
                        MelonLogger.Msg($"[WebBrowser] Element {elem.Index} not found for click");
                        _announcer.AnnounceInterrupt("Element not found. Page may have changed.");
                        ScheduleRescan(0.2f);
                    }
                    else
                    {
                        MelonLogger.Msg($"[WebBrowser] Clicked element {elem.Index}: {elem.Text}");
                    }
                })
                .Catch(ex =>
                {
                    MelonLogger.Msg($"[WebBrowser] Click error: {ex.Message}");
                });
        }

        #endregion

        #region Back to Arena

        private void FindBackToArenaButton(GameObject panel)
        {
            _backToArenaButton = null;

            // Look for a Unity Button that isn't part of the Browser itself
            // Typically labeled "Back" or has a back/close icon
            var buttons = panel.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (btn == null || !btn.gameObject.activeInHierarchy || !btn.interactable) continue;

                // Skip buttons that are children of the Browser's RawImage/render surface
                if (_browser != null && btn.transform.IsChildOf(_browser.transform)) continue;

                string name = btn.gameObject.name.ToLowerInvariant();
                string label = UITextExtractor.GetText(btn.gameObject)?.ToLowerInvariant() ?? "";

                if (name.Contains("back") || name.Contains("close") || name.Contains("return") ||
                    label.Contains("back") || label.Contains("close") || label.Contains("return") ||
                    label.Contains("arena"))
                {
                    _backToArenaButton = btn.gameObject;
                    MelonLogger.Msg($"[WebBrowser] Found Back to Arena button: {btn.gameObject.name}");
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
                    MelonLogger.Msg($"[WebBrowser] Using fallback Back button: {btn.gameObject.name}");
                    break;
                }
            }
        }

        private void ClickBackToArena()
        {
            if (_backToArenaButton != null)
            {
                MelonLogger.Msg("[WebBrowser] Clicking Back to Arena");
                _announcer.AnnounceInterrupt("Back to Arena");
                UIActivator.Activate(_backToArenaButton);
            }
            else
            {
                MelonLogger.Msg("[WebBrowser] No Back to Arena button found");
                _announcer.AnnounceInterrupt("No back button found");
            }
        }

        #endregion
    }
}
