using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Patches;
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
        private string _contextLabel;

        private List<WebElement> _elements = new List<WebElement>();
        private int _currentIndex;
        private bool _isEditingField;
        private bool _isLoading;
        private int _lastWebElementCount; // Track for silent rescan comparison

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
        private const float MutationStableTimeout = 8f; // Stop polling after DOM is stable for this long

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

        // Select all text in an input field (Ctrl+A equivalent)
        private static string SelectAllScript(int index)
        {
            return FindElementFunc + $" var el = findEl({index}); if (el) {{ el.focus(); try {{ el.select(); }} catch(e) {{}} return 'ok'; }} return 'not_found';";
        }

        // Read current value of a text input by its data-aa-idx
        private static string ReadValueScript(int index)
        {
            return FindElementFunc + $@"
                var el = findEl({index});
                if (!el) return '';
                try {{ return el.value || ''; }} catch(e) {{ return ''; }}";
        }

        // Append text to an input field. Tries multiple approaches:
        // 1. execCommand('insertText') — works for most forms
        // 2. Full keyboard event sequence — works for masked inputs (card numbers, dates)
        // 3. Direct value + React-compatible InputEvent — fallback
        private static string AppendTextScript(int index, string text)
        {
            string escaped = text.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "");
            return FindElementFunc + $@"
                var el = findEl({index});
                if (!el) return 'not_found';
                el.focus();
                var doc = el.ownerDocument;
                var win = doc.defaultView || window;
                var valueBefore = el.value || '';
                var method = 'none';

                // Approach 1: execCommand
                var ok = false;
                try {{ ok = doc.execCommand('insertText', false, '{escaped}'); }} catch(e) {{}}
                if (ok && el.value !== valueBefore) {{
                    method = 'execCommand';
                    // Sync React/framework state — execCommand modifies the DOM but
                    // bypasses React's value tracker, so onChange never fires.
                    // Reset the tracker and dispatch events so the framework sees the change.
                    try {{ if (el._valueTracker) el._valueTracker.setValue(valueBefore); }} catch(e2) {{}}
                    try {{ el.dispatchEvent(new win.Event('input', {{bubbles: true}})); }} catch(e3) {{}}
                    try {{ el.dispatchEvent(new win.Event('change', {{bubbles: true}})); }} catch(e4) {{}}
                }} else {{
                    // Approach 2: keyboard events per character (for masked inputs)
                    var text = '{escaped}';
                    for (var ci = 0; ci < text.length; ci++) {{
                        var ch = text[ci];
                        var kc = ch.charCodeAt(0);
                        var evInit = {{key: ch, code: 'Key' + ch.toUpperCase(), keyCode: kc, charCode: kc, which: kc, bubbles: true, cancelable: true, composed: true}};
                        try {{
                            el.dispatchEvent(new win.KeyboardEvent('keydown', evInit));
                            el.dispatchEvent(new win.KeyboardEvent('keypress', evInit));
                        }} catch(e) {{}}
                        try {{
                            el.dispatchEvent(new win.InputEvent('input', {{
                                data: ch, inputType: 'insertText', bubbles: true, cancelable: false, composed: true
                            }}));
                        }} catch(e) {{
                            el.dispatchEvent(new win.Event('input', {{bubbles: true}}));
                        }}
                        try {{
                            el.dispatchEvent(new win.KeyboardEvent('keyup', evInit));
                        }} catch(e) {{}}
                    }}
                    if (el.value !== valueBefore) {{
                        method = 'keyboard_events';
                    }} else {{
                        // Approach 3: direct value set + events
                        try {{ el.value = valueBefore + text; }} catch(e) {{}}
                        try {{
                            el.dispatchEvent(new win.InputEvent('input', {{
                                data: text, inputType: 'insertText', bubbles: true, cancelable: false, composed: true
                            }}));
                        }} catch(e) {{
                            el.dispatchEvent(new win.Event('input', {{bubbles: true}}));
                        }}
                        el.dispatchEvent(new win.Event('change', {{bubbles: true}}));
                        method = el.value !== valueBefore ? 'direct_value' : 'all_failed';
                    }}
                }}
                var result = el.value || '';
                return method + ':' + result;";
        }

        // Delete last character from input field
        private static string BackspaceScript(int index)
        {
            return FindElementFunc + $@"
                var el = findEl({index});
                if (!el) return 'not_found';
                el.focus();
                var doc = el.ownerDocument;
                var win = doc.defaultView || window;
                var valueBefore = el.value || '';
                try {{ el.selectionStart = el.selectionEnd = valueBefore.length; }} catch(e) {{}}

                // Approach 1: execCommand
                var ok = false;
                try {{ ok = doc.execCommand('delete', false); }} catch(e) {{}}
                if (ok && el.value !== valueBefore) {{
                    // Sync React/framework state (same as AppendTextScript)
                    try {{ if (el._valueTracker) el._valueTracker.setValue(valueBefore); }} catch(e2) {{}}
                    try {{ el.dispatchEvent(new win.Event('input', {{bubbles: true}})); }} catch(e3) {{}}
                    try {{ el.dispatchEvent(new win.Event('change', {{bubbles: true}})); }} catch(e4) {{}}
                }} else {{
                    // Approach 2: keyboard event for Backspace
                    var bsInit = {{key: 'Backspace', code: 'Backspace', keyCode: 8, which: 8, bubbles: true, cancelable: true, composed: true}};
                    try {{
                        el.dispatchEvent(new win.KeyboardEvent('keydown', bsInit));
                        el.dispatchEvent(new win.InputEvent('input', {{
                            inputType: 'deleteContentBackward', bubbles: true, cancelable: false, composed: true
                        }}));
                        el.dispatchEvent(new win.KeyboardEvent('keyup', bsInit));
                    }} catch(e) {{}}
                    if (el.value === valueBefore) {{
                        // Approach 3: direct value trim
                        try {{ el.value = valueBefore.slice(0, -1); }} catch(e) {{}}
                        try {{
                            el.dispatchEvent(new win.InputEvent('input', {{
                                inputType: 'deleteContentBackward', bubbles: true, cancelable: false, composed: true
                            }}));
                        }} catch(e) {{
                            el.dispatchEvent(new win.Event('input', {{bubbles: true}}));
                        }}
                        el.dispatchEvent(new win.Event('change', {{bubbles: true}}));
                    }}
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

        // Installs a MutationObserver on document.body (and same-origin iframe bodies)
        // that sets window.__aa_domChanged = true when child nodes are added/removed.
        // Idempotent — skips if already installed.
        private const string InstallMutationObserverScript = @"
            if (!window.__aa_observer) {
                window.__aa_domChanged = false;
                var cb = function() { window.__aa_domChanged = true; };
                var opts = {childList: true, subtree: true};
                window.__aa_observer = new MutationObserver(cb);
                if (document.body) window.__aa_observer.observe(document.body, opts);
                // Also observe same-origin iframe bodies
                try {
                    var iframes = document.querySelectorAll('iframe');
                    for (var i = 0; i < iframes.length; i++) {
                        try {
                            var d = iframes[i].contentDocument;
                            if (d && d.body) {
                                var mo = new MutationObserver(cb);
                                mo.observe(d.body, opts);
                            }
                        } catch(e) {}
                    }
                } catch(e) {}
            }
            return 'ok';
        ";

        // Checks if DOM changed since last reset and resets the flag
        private const string PollMutationScript = @"
            var changed = window.__aa_domChanged || false;
            window.__aa_domChanged = false;
            return changed;
        ";

        // Detects cross-origin iframes (CAPTCHA signature: content is in unreachable iframe)
        private const string DetectCrossOriginIframesScript = @"
            var iframes = document.querySelectorAll('iframe');
            var crossOrigin = 0;
            var srcs = [];
            for (var i = 0; i < iframes.length; i++) {
                try {
                    var doc = iframes[i].contentDocument;
                    if (!doc) { crossOrigin++; srcs.push(iframes[i].src || '(no src)'); }
                } catch(e) {
                    crossOrigin++;
                    srcs.push(iframes[i].src || '(no src)');
                }
            }
            return JSON.stringify({crossOrigin: crossOrigin, srcs: srcs});
        ";

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
            _clickCooldownUntil = 0;
            _mutationObserverActive = false;
            _mutationPollTimer = 0;
            _mutationStableTime = 0;
            _hasInteractiveElements = false;
            _elements.Clear();

            // Find ZFBrowser.Browser component
            _browser = panel.GetComponentInChildren<Browser>(true);
            if (_browser == null)
            {
                MelonLogger.Msg("[WebBrowser] No Browser component found in panel");
                _announcer.AnnounceInterrupt(Strings.WebBrowser_NoBrowserFound);
                _isActive = false;
                return;
            }

            _isActive = true;

            // Block Escape from reaching the game (would open settings menu)
            KeyboardManagerPatch.BlockEscape = true;

            // Find "Back to Arena" button (Unity Button outside/alongside the browser)
            FindBackToArenaButton(panel);

            // Subscribe to page load events
            _browser.onLoad += OnPageLoad;

            MelonLogger.Msg($"[WebBrowser] Activated. Browser URL: {_browser.Url}, IsLoaded: {_browser.IsLoaded}");

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

            _browser = null;
            _browserPanel = null;
            _isActive = false;
            _isEditingField = false;
            _isLoading = false;
            _pendingRescan = false;
            _secondRescanTimer = 0;
            _emptyRescanCount = 0;
            _captchaDetected = false;
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
                    _browser.EvalJSCSP(SubmitScript(elem.Index))
                        .Catch(ex => MelonLogger.Msg($"[WebBrowser] Submit error: {ex.Message}"));
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
                    _browser.EvalJSCSP(SelectAllScript(elem.Index))
                        .Catch(ex => MelonLogger.Msg($"[WebBrowser] SelectAll error: {ex.Message}"));
                    _announcer.AnnounceInterrupt(Strings.AllSelected);
                }
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
                                _announcer.AnnounceInterrupt(Strings.WebBrowser_FieldNotFound);
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
                    string chars = filtered.ToString();
                    MelonLogger.Msg($"[WebBrowser] Typing '{chars}' into element {elem.Index}: {elem.Text}");
                    _browser.EvalJSCSP(AppendTextScript(elem.Index, chars))
                        .Then(result =>
                        {
                            string res = (string)result;
                            MelonLogger.Msg($"[WebBrowser] TypeText result: {res}");
                        })
                        .Catch(ex => MelonLogger.Msg($"[WebBrowser] TypeText error: {ex.Message}"));
                }
            }
        }

        private void ExitEditMode()
        {
            _isEditingField = false;
        }

        private void RefreshAndReadFieldValue(bool readFull, int cursorDelta = 0, int cursorJump = int.MinValue)
        {
            if (_currentIndex < 0 || _currentIndex >= _elements.Count) return;
            var elem = _elements[_currentIndex];

            _browser.EvalJSCSP(ReadValueScript(elem.Index))
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

            MelonLogger.Msg($"[WebBrowser] Extracted {_elements.Count} elements ({webElementCount} from page, interactive={foundInteractive})");

            // Layer 1: No web elements at all — iframes may still be loading
            if (webElementCount == 0 && !_pendingRescan)
            {
                _emptyRescanCount++;

                // After several failed attempts, check for cross-origin CAPTCHA iframes
                if (_emptyRescanCount >= MaxEmptyRescansBeforeCheck && !_captchaDetected)
                {
                    CheckForCaptcha();
                    return;
                }

                // If CAPTCHA already detected, don't keep rescanning
                if (_captchaDetected)
                {
                    return;
                }

                MelonLogger.Msg("[WebBrowser] No web elements found, scheduling rescan for iframe content");
                ScheduleRescan(1.5f);
                _announcer.AnnounceInterrupt(Strings.WebBrowser_ContextLoading(_contextLabel));
                return;
            }

            // Reset empty counter when we find elements
            _emptyRescanCount = 0;

            // Layer 2: Found text but no interactive elements — page skeleton loaded
            // but dynamic content (buttons, inputs) hasn't rendered yet.
            // Install MutationObserver to detect when they appear.
            if (!foundInteractive && !_mutationObserverActive)
            {
                MelonLogger.Msg("[WebBrowser] No interactive elements found, installing MutationObserver to watch for dynamic content");
                InstallMutationObserver();
                _announcer.AnnounceInterrupt(Strings.WebBrowser_ContextLoading(_contextLabel));
                return;
            }

            // If element count hasn't changed, this is likely a silent rescan — don't re-announce
            if (webElementCount == _lastWebElementCount)
            {
                MelonLogger.Msg("[WebBrowser] Element count unchanged, silent rescan");
                return;
            }

            _lastWebElementCount = webElementCount;

            // If we now have interactive elements, stop the MutationObserver
            if (foundInteractive && _mutationObserverActive)
            {
                MelonLogger.Msg("[WebBrowser] Interactive elements found, stopping MutationObserver");
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
            MelonLogger.Msg($"[WebBrowser] Extraction error: {ex.Message}");
            _announcer.AnnounceInterrupt(Strings.WebBrowser_CouldNotRead);
        }

        #endregion

        #region MutationObserver

        /// <summary>
        /// Inject a MutationObserver into the page that sets a flag when DOM nodes change.
        /// </summary>
        private void InstallMutationObserver()
        {
            if (_browser == null || !_browser.IsReady) return;

            _browser.EvalJSCSP(InstallMutationObserverScript)
                .Then(result =>
                {
                    _mutationObserverActive = true;
                    _mutationPollTimer = MutationPollInterval;
                    _mutationStableTime = 0;
                    MelonLogger.Msg("[WebBrowser] MutationObserver installed");
                })
                .Catch(ex =>
                {
                    MelonLogger.Msg($"[WebBrowser] MutationObserver install error: {ex.Message}");
                });
        }

        /// <summary>
        /// Poll the MutationObserver flag. If DOM changed, re-extract.
        /// If DOM has been stable long enough, stop polling.
        /// </summary>
        private void PollMutationObserver()
        {
            if (_browser == null || !_browser.IsReady || !_mutationObserverActive) return;

            _browser.EvalJSCSP(PollMutationScript)
                .Then(result =>
                {
                    if (!_mutationObserverActive) return; // Deactivated while polling

                    bool changed = result != null && (bool)result;
                    if (changed)
                    {
                        _mutationStableTime = 0;
                        MelonLogger.Msg("[WebBrowser] DOM changed, re-extracting");
                        ExtractElements();
                    }
                    else
                    {
                        _mutationStableTime += MutationPollInterval;
                        if (_mutationStableTime >= MutationStableTimeout)
                        {
                            MelonLogger.Msg("[WebBrowser] DOM stable, stopping MutationObserver polling");
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
                    MelonLogger.Msg($"[WebBrowser] MutationObserver poll error: {ex.Message}");
                });
        }

        #endregion

        #region Page Load Handling

        private void OnPageLoad(JSONNode loadData)
        {
            MelonLogger.Msg($"[WebBrowser] Page loaded: {_browser?.Url}");

            if (!_isActive) return;

            // Check for CAPTCHA / auth-failure URL patterns BEFORE resetting state.
            // PayPal does rapid redirect chains (login → CAPTCHA → back to login) that
            // complete in ~3 seconds. The old approach of waiting for 3 empty rescans
            // (~4.5s) never triggered because each OnPageLoad reset the counter.
            string url = _browser?.Url ?? "";
            if (IsCaptchaUrl(url))
            {
                MelonLogger.Msg("[WebBrowser] CAPTCHA/security URL detected on page load, announcing warning");
                _captchaDetected = true;
                _pendingRescan = false;
                _secondRescanTimer = 0;
                _mutationObserverActive = false;
                _isEditingField = false;
                _isLoading = false;
                _elements.Clear();
                _announcer.AnnounceInterrupt(Strings.WebBrowser_CaptchaWarning);
                return;
            }

            // If a previous page in the redirect chain already detected CAPTCHA,
            // don't reset — the redirect back to the login page should keep the warning active
            if (_captchaDetected)
            {
                MelonLogger.Msg("[WebBrowser] CAPTCHA already detected, ignoring redirect page load");
                return;
            }

            // Cancel any pending rescan timers — they were for the previous page
            _pendingRescan = false;
            _secondRescanTimer = 0;
            _emptyRescanCount = 0;
            _clickCooldownUntil = 0; // New page = new buttons, clear cooldown
            _mutationObserverActive = false; // Will be re-installed after extraction
            _hasInteractiveElements = false;

            _isEditingField = false;
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
                _browser.EvalJSCSP(ReadValueScript(elem.Index))
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

            MelonLogger.Msg($"[WebBrowser] Activating element {elem.Index}: {elem.Text} ({elem.Role})");

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

            // Focus the element in the browser
            _browser.EvalJSCSP(FocusScript(elem.Index))
                .Catch(ex => MelonLogger.Msg($"[WebBrowser] Focus error: {ex.Message}"));

            string fieldType = elem.InputType == "password" ? Strings.RolePasswordField : Strings.TextField;
            _announcer.AnnounceInterrupt(Strings.WebBrowser_Editing(elem.Text, fieldType));
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
                        _announcer.AnnounceInterrupt(Strings.WebBrowser_ElementNotFound);
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

        #region CAPTCHA Detection

        /// <summary>
        /// Quick URL-based check for CAPTCHA / security verification pages.
        /// Called on every page load for immediate detection.
        /// </summary>
        private static bool IsCaptchaUrl(string url)
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

            // Generic CAPTCHA / challenge page patterns (single keyword is too broad,
            // but these specific paths are reliable indicators)
            if (lower.Contains("/challenge") || lower.Contains("/captcha"))
                return true;

            return false;
        }

        private void CheckForCaptcha()
        {
            MelonLogger.Msg("[WebBrowser] Checking for CAPTCHA / security verification...");

            // Check the URL for known security step-up patterns
            string url = _browser?.Url?.ToLowerInvariant() ?? "";
            bool urlSuspicious = IsCaptchaUrl(url) ||
                                 url.Contains("authflow") || url.Contains("challenge") ||
                                 url.Contains("captcha") || url.Contains("stepup") ||
                                 url.Contains("security") || url.Contains("verify");

            // Also check for cross-origin iframes (CAPTCHA content is typically in one)
            _browser.EvalJSCSP(DetectCrossOriginIframesScript)
                .Then(result =>
                {
                    string json = (string)result;
                    bool hasCrossOriginIframes = false;
                    string iframeSrcs = "";

                    if (!string.IsNullOrEmpty(json))
                    {
                        try
                        {
                            // Simple parsing — look for crossOrigin count
                            int coIdx = json.IndexOf("\"crossOrigin\":");
                            if (coIdx >= 0)
                            {
                                int numStart = coIdx + "\"crossOrigin\":".Length;
                                string numStr = "";
                                while (numStart < json.Length && (char.IsDigit(json[numStart]) || json[numStart] == ' '))
                                {
                                    if (char.IsDigit(json[numStart]))
                                        numStr += json[numStart];
                                    numStart++;
                                }
                                int crossOriginCount;
                                if (int.TryParse(numStr, out crossOriginCount) && crossOriginCount > 0)
                                {
                                    hasCrossOriginIframes = true;
                                }
                            }
                            iframeSrcs = json;
                        }
                        catch { /* JSON parsing is best-effort; malformed response is non-fatal */ }
                    }

                    MelonLogger.Msg($"[WebBrowser] CAPTCHA check: urlSuspicious={urlSuspicious}, crossOriginIframes={hasCrossOriginIframes}, details={iframeSrcs}");

                    if (urlSuspicious || hasCrossOriginIframes)
                    {
                        _captchaDetected = true;
                        MelonLogger.Msg("[WebBrowser] CAPTCHA detected! Stopping rescan loop.");
                        _announcer.AnnounceInterrupt(Strings.WebBrowser_CaptchaWarning);
                    }
                    else
                    {
                        // Not a CAPTCHA — keep retrying a few more times
                        MelonLogger.Msg("[WebBrowser] No CAPTCHA indicators found, continuing rescans");
                        ScheduleRescan(1.5f);
                        _announcer.AnnounceInterrupt(Strings.WebBrowser_ContextLoading(_contextLabel));
                    }
                })
                .Catch(ex =>
                {
                    MelonLogger.Msg($"[WebBrowser] CAPTCHA detection error: {ex.Message}");
                    // If the URL alone was suspicious, still warn
                    if (urlSuspicious)
                    {
                        _captchaDetected = true;
                        _announcer.AnnounceInterrupt(Strings.WebBrowser_CaptchaWarning);
                    }
                    else
                    {
                        ScheduleRescan(1.5f);
                        _announcer.AnnounceInterrupt(Strings.WebBrowser_ContextLoading(_contextLabel));
                    }
                });
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
                    MelonLogger.Msg($"[WebBrowser] Found Back to Arena button: {btn.gameObject.name}, label: {_backToArenaLabel}");
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
                    MelonLogger.Msg($"[WebBrowser] Using fallback Back button: {btn.gameObject.name}, label: {_backToArenaLabel}");
                    break;
                }
            }
        }

        private void ClickBackToArena()
        {
            if (_backToArenaButton != null)
            {
                MelonLogger.Msg("[WebBrowser] Clicking Back to Arena");
                _announcer.AnnounceInterrupt(_backToArenaLabel ?? Strings.WebBrowser_PaymentPage);
                UIActivator.Activate(_backToArenaButton);
            }
            else
            {
                MelonLogger.Msg("[WebBrowser] No Back to Arena button found");
                _announcer.AnnounceInterrupt(Strings.WebBrowser_NoBackButton);
            }
        }

        #endregion
    }
}
