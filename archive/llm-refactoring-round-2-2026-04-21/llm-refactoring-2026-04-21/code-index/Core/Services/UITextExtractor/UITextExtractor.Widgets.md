# UITextExtractor.Widgets.cs
Path: src/Core/Services/UITextExtractor/UITextExtractor.Widgets.cs
Lines: 478

## Top-level comments
- Feature partial for Unity UI widget text extraction: input fields (TMP and legacy), toggles, dropdowns, scrollbars, sliders, and SystemMessageView popup bodies.

## public static partial class UITextExtractor (line 9)

### Fields
(no fields declared in this partial)

### Methods
- public static string GetInputFieldLabel(GameObject inputField) (line 17) — label for edit-mode announcements; uses name patterns (" - ", "_") then placeholder text
- public static string GetButtonText(GameObject buttonObj, string fallback = null) (line 70) — scans all TMP_Text (and legacy Text) children including inactive; skips single-char content
- private static string TryGetInputFieldLabel(GameObject inputFieldObj) (line 108) — walks parent hierarchy for "Something_inputField" / "inputField_Something" patterns
- private static string GetInputFieldText(TMP_InputField inputField) (line 160) — handles password masking, placeholder fallback, "field, empty" variants
- private static string GetInputFieldText(InputField inputField) (line 219) — legacy Unity InputField variant
- private static string GetToggleText(Toggle toggle) (line 248) — prefers Localize; special-cases "POSITION" placeholder for the BO3 toggle
- private static string GetDropdownText(TMP_Dropdown d) (line 299) — delegates to FormatDropdownText
- private static string GetDropdownText(Dropdown d) (line 304) — delegates to FormatDropdownText
- private static string FormatDropdownText(int value, int optionCount, string optionText, string captionText, string objectName) (line 309) — appends "position X of N" when a selection exists, else "no selection"
- private static string GetScrollbarText(Scrollbar scrollbar) (line 327) — reports "vertical/horizontal, at top/at bottom/N percent"
- private static string GetSliderText(Slider slider) (line 350) — percentage based on min/max range with associated label when available
- public static string GetPopupBodyText(GameObject popupGameObject) (line 384) — walks SystemMessageView MessageArea/Scroll View/Viewport/Content paths; filters button labels ("ok"/"cancel"/"weiterbearbeiten"/…); fallback skips ButtonLayout texts
