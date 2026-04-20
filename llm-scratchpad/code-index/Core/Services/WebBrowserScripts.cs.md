# WebBrowserScripts.cs
Path: src/Core/Services/WebBrowserScripts.cs
Lines: 488

## Top-level comments
- JavaScript source constants and script builders for WebBrowserAccessibility. Extracted 2026-04-20 (split 9/12) so the navigator class can focus on orchestration while the JS payloads live in one place. Scripts are injected into embedded Chromium (ZFBrowser) pages. Large consts (ExtractionScript, InstallMutationObserverScript, etc.) use EvalJSCSP IIFE wrapping; short builders return function bodies.

## internal static class WebBrowserScripts (line 12)

### Constants (JS source text)
- internal const string ExtractionScript (line 17) — Note: scans main doc + same-origin iframes for `button, a, input, select, textarea, h1-6, p, label, span, div, [role="button"/"link"/"checkbox"/"tab"/"menuitem"]`; annotates each with data-aa-idx
- internal const string FindElementFunc (line 206) — Note: helper JS function `findEl(idx)` used as prefix by ClickScript, FocusScript, SelectAllScript, ReadValueScript, AppendTextScript, BackspaceScript, SubmitScript, GetBoundingBoxScript; searches main doc + same-origin iframes for `[data-aa-idx="N"]`
- internal const string InstallMutationObserverScript (line 440) — Note: idempotent; observes `document.body` + same-origin iframe bodies; sets `window.__aa_domChanged = true` on add/remove
- internal const string PollMutationScript (line 465) — Note: reads and clears `window.__aa_domChanged`
- internal const string DetectCrossOriginIframesScript (line 472) — Note: counts cross-origin iframes (CAPTCHA signature); returns JSON `{crossOrigin, srcs}`

### Static helpers (script builders, one per JS operation)
- internal static string ClickScript(int index) (line 222) — builds JS that finds element by idx and calls `.click()`
- internal static string FocusScript(int index) (line 228) — builds JS that finds element by idx and calls `.focus()`
- internal static string GetBoundingBoxScript(int index) (line 236) — returns CSV `"cx,cy,vw,vh"` in CSS pixels, walks iframe offsets
- internal static string SelectAllScript(int index) (line 259) — focuses and calls `.select()`
- internal static string ReadValueScript(int index) (line 265) — reads `.value` from input/textarea/contenteditable
- internal static string AppendTextScript(int index, string text) (line 278) — tries execCommand, keyboard events, then native value setter with React tracker reset
- internal static string BackspaceScript(int index) (line 371) — deletes character left of cursor; handles React tracker
- internal static string SubmitScript(int index) (line 418) — dispatches Enter keydown/keypress/keyup; falls back to form.submit if inside a form
