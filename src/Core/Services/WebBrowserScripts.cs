namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// JavaScript source constants and script builders for WebBrowserAccessibility.
    /// Extracted 2026-04-20 (split 9/12) so the navigator class can focus on
    /// orchestration while the JS payloads live in one place.
    ///
    /// Scripts are injected into embedded Chromium (ZFBrowser) pages.
    /// Large consts (ExtractionScript, InstallMutationObserverScript, etc.) use
    /// EvalJSCSP IIFE wrapping; short builders return function bodies.
    /// </summary>
    internal static class WebBrowserScripts
    {
        // Script injected into the page to extract all navigable elements.
        // Used with EvalJSCSP (which wraps in IIFE) to bypass CSP eval restrictions.
        // Scans main document AND same-origin iframes (recursively).
        internal const string ExtractionScript = @"
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

                    // Detect password-like fields even when the HTML type isn't 'password'.
                    // React-wrapped inputs (e.g. PayPal) often use type='text' with
                    // autocomplete='current-password'/'new-password' — treat those as passwords
                    // so edit mode switches to passthrough typing.
                    if (inputType !== 'password') {
                        var autoc = (el.getAttribute('autocomplete') || '').toLowerCase();
                        if (autoc === 'current-password' || autoc === 'new-password') {
                            inputType = 'password';
                        }
                    }
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
        internal const string FindElementFunc = @"
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
        internal static string ClickScript(int index)
        {
            return FindElementFunc + $" var el = findEl({index}); if (el) {{ el.click(); return 'ok'; }} return 'not_found';";
        }

        // Focus an element by its data-aa-idx
        internal static string FocusScript(int index)
        {
            return FindElementFunc + $" var el = findEl({index}); if (el) {{ el.focus(); return 'ok'; }} return 'not_found';";
        }

        // Get an element's center coordinates (normalized 0..1 relative to the top-level browser viewport).
        // Walks up iframe boundaries adding each frame's offset so coords map to the CEF surface.
        // Returns CSV "cx,cy,vw,vh" in CSS pixels, or '' if element missing.
        internal static string GetBoundingBoxScript(int index)
        {
            return FindElementFunc + $@"
                var el = findEl({index});
                if (!el) return '';
                var r = el.getBoundingClientRect();
                var x = r.left + r.width / 2;
                var y = r.top + r.height / 2;
                var w = el.ownerDocument.defaultView;
                while (w && w !== window.top) {{
                    try {{
                        var fe = w.frameElement;
                        if (!fe) break;
                        var fr = fe.getBoundingClientRect();
                        x += fr.left;
                        y += fr.top;
                        w = fe.ownerDocument.defaultView;
                    }} catch(e) {{ break; }}
                }}
                return x + ',' + y + ',' + window.top.innerWidth + ',' + window.top.innerHeight;";
        }

        // Select all text in an input field (Ctrl+A equivalent)
        internal static string SelectAllScript(int index)
        {
            return FindElementFunc + $" var el = findEl({index}); if (el) {{ el.focus(); try {{ el.select(); }} catch(e) {{}} return 'ok'; }} return 'not_found';";
        }

        // Read current value of a text input by its data-aa-idx
        internal static string ReadValueScript(int index)
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
        internal static string AppendTextScript(int index, string text)
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
                var isPassword = el.type === 'password';

                // Approach 1: execCommand. Skipped for password fields — PayPal's
                // React-controlled password input rejects the resulting state update
                // (DOM value updates but controlled-component state stays empty,
                // producing failedBecause=invalid_input on submit).
                var ok = false;
                if (!isPassword) {{
                    try {{ ok = doc.execCommand('insertText', false, '{escaped}'); }} catch(e) {{}}
                }}
                if (ok && el.value !== valueBefore) {{
                    method = 'execCommand';
                    // Sync React/framework state — execCommand modifies the DOM but
                    // bypasses React's value tracker, so onChange never fires.
                    // Reset the tracker and dispatch events so the framework sees the change.
                    try {{ if (el._valueTracker) el._valueTracker.setValue(valueBefore); }} catch(e2) {{}}
                    // Use InputEvent with data/inputType so React's synthetic event
                    // handler classifies it as a real insert and runs onChange.
                    try {{
                        el.dispatchEvent(new win.InputEvent('input', {{
                            data: '{escaped}', inputType: 'insertText', bubbles: true, cancelable: false, composed: true
                        }}));
                    }} catch(e3) {{
                        try {{ el.dispatchEvent(new win.Event('input', {{bubbles: true}})); }} catch(e3b) {{}}
                    }}
                    try {{ el.dispatchEvent(new win.Event('change', {{bubbles: true}})); }} catch(e4) {{}}
                    // Re-check: framework (e.g. PayPal React) may revert value during
                    // synchronous event handling after SPA navigation
                    if (el.value === valueBefore) method = 'none';
                }}
                if (method === 'none') {{
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
                        // Approach 3: native value setter + React tracker reset
                        // Use the native HTMLInputElement.prototype.value setter to bypass
                        // React's synthetic value interception on controlled components
                        var newVal = valueBefore + text;
                        try {{
                            var nativeSetter = Object.getOwnPropertyDescriptor(
                                win.HTMLInputElement.prototype, 'value').set;
                            nativeSetter.call(el, newVal);
                        }} catch(e) {{
                            try {{ el.value = newVal; }} catch(e2) {{}}
                        }}
                        try {{ if (el._valueTracker) el._valueTracker.setValue(valueBefore); }} catch(e) {{}}
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
        internal static string BackspaceScript(int index)
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
        internal static string SubmitScript(int index)
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
        internal const string InstallMutationObserverScript = @"
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
        internal const string PollMutationScript = @"
            var changed = window.__aa_domChanged || false;
            window.__aa_domChanged = false;
            return changed;
        ";

        // Detects cross-origin iframes (CAPTCHA signature: content is in unreachable iframe)
        internal const string DetectCrossOriginIframesScript = @"
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
    }
}
