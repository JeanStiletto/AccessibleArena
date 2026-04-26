using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Core.Services
{
    public static partial class UITextExtractor
    {
        /// <summary>
        /// Get a readable label for an input field from its name or placeholder text.
        /// Used for edit mode announcements. Checks name patterns and placeholder content.
        /// </summary>
        /// <param name="inputField">The input field GameObject</param>
        /// <returns>A readable label extracted from the field</returns>
        public static string GetInputFieldLabel(GameObject inputField)
        {
            if (inputField == null) return "text field";

            // Prefer a localized label found via a Localize/TMP_Text sibling inside the
            // wrapping "*_inputField" container. Covers registration-panel inputs whose own
            // name is a non-localized "Input Field - Password 1" / "Input Field - Displayname".
            string parentLabel = TryGetInputFieldLabel(inputField);
            if (!string.IsNullOrEmpty(parentLabel))
                return parentLabel;

            string name = inputField.name;

            // Try to extract meaningful label from name
            // Common patterns: "Input Field - Email", "InputField_Username", etc.
            if (name.Contains(" - "))
            {
                var parts = name.Split(new[] { " - " }, System.StringSplitOptions.None);
                if (parts.Length > 1)
                    return parts[1].Trim();
            }

            if (name.Contains("_"))
            {
                var parts = name.Split('_');
                if (parts.Length > 1)
                    return CleanObjectName(parts[parts.Length - 1].Trim());
            }

            // Check placeholder text
            var tmpInput = inputField.GetComponent<TMP_InputField>();
            if (tmpInput != null && tmpInput.placeholder != null)
            {
                var placeholderText = tmpInput.placeholder.GetComponent<TMP_Text>();
                if (placeholderText != null && !string.IsNullOrEmpty(placeholderText.text))
                    return CleanText(placeholderText.text);
            }

            // Check legacy InputField placeholder
            var legacyInput = inputField.GetComponent<InputField>();
            if (legacyInput != null && legacyInput.placeholder != null)
            {
                var placeholderText = legacyInput.placeholder.GetComponent<Text>();
                if (placeholderText != null && !string.IsNullOrEmpty(placeholderText.text))
                    return CleanText(placeholderText.text);
            }

            // Fallback: clean up the name
            string cleaned = name.Replace("Input Field", "").Replace("InputField", "").Trim();
            return string.IsNullOrEmpty(cleaned) ? "text field" : cleaned;
        }

        /// <summary>
        /// Extracts text specifically from button elements.
        /// Searches all TMP_Text children (including inactive) and returns the first valid text found.
        /// More thorough than GetText() for buttons with multiple text children.
        /// </summary>
        /// <param name="buttonObj">The button GameObject</param>
        /// <param name="fallback">Fallback text if no valid text found (null returns null)</param>
        /// <returns>The button text or fallback</returns>
        public static string GetButtonText(GameObject buttonObj, string fallback = null)
        {
            if (buttonObj == null) return fallback;

            // Search all TMP_Text children including inactive ones
            var texts = buttonObj.GetComponentsInChildren<TMP_Text>(true);
            foreach (var text in texts)
            {
                if (text == null) continue;

                string content = CleanText(text.text);
                // Skip empty or single-character content (often icons)
                if (string.IsNullOrEmpty(content) || content.Length <= 1)
                    continue;

                return content;
            }

            // Try legacy Text components
            var legacyTexts = buttonObj.GetComponentsInChildren<Text>(true);
            foreach (var text in legacyTexts)
            {
                if (text == null) continue;

                string content = CleanText(text.text);
                if (string.IsNullOrEmpty(content) || content.Length <= 1)
                    continue;

                return content;
            }

            return fallback;
        }

        /// <summary>
        /// Try to get a meaningful label for an input field from its parent hierarchy.
        /// Looks for patterns like "OpponentName_inputField" → "Opponent Name"
        /// </summary>
        private static string TryGetInputFieldLabel(GameObject inputFieldObj)
        {
            if (inputFieldObj == null) return null;

            // Check parent and grandparent for meaningful names
            Transform current = inputFieldObj.transform.parent;
            int maxLevels = 3;

            while (current != null && maxLevels > 0)
            {
                string name = current.name;
                bool isInputFieldContainer =
                    name.IndexOf("_inputField", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.StartsWith("inputField_", System.StringComparison.OrdinalIgnoreCase);

                // Registration panel wrappers like "Login_inputField Password 1" don't carry the
                // localized label in their name (we'd extract "Password 1"). Prefer a localized
                // label from a sibling TMP_Text / Localize component inside the wrapper — skipping
                // the input field's own subtree so we don't pick up placeholder or user text.
                // Do NOT widen to the wrapper's parent: the RegistrationPanel places multiple
                // wrappers inside a shared "Group - Content"/"Group - PasswordObjects" and the
                // parent subtree contains the panel heading plus the neighbouring field — which
                // cross-pollinates labels (Password 1 ↔ Password 2) and leaks the heading.
                if (isInputFieldContainer)
                {
                    string localized = FindLocalizedLabelInContainer(current.gameObject, inputFieldObj);
                    if (!string.IsNullOrEmpty(localized))
                        return localized;
                }

                // Pattern: Something_inputField → "Something"
                if (name.EndsWith("_inputField", System.StringComparison.OrdinalIgnoreCase))
                {
                    string label = name.Substring(0, name.Length - 11); // Remove "_inputField"
                    return CleanObjectName(label);
                }

                // Pattern: inputField_Something → "Something"
                if (name.StartsWith("inputField_", System.StringComparison.OrdinalIgnoreCase))
                {
                    string label = name.Substring(11); // Remove "inputField_"
                    return CleanObjectName(label);
                }

                // Pattern: Login_inputField Something → extract "Something"
                if (name.Contains("_inputField"))
                {
                    int idx = name.IndexOf("_inputField");
                    // Check for text after _inputField
                    if (idx + 11 < name.Length)
                    {
                        string label = name.Substring(idx + 11).Trim();
                        if (!string.IsNullOrEmpty(label))
                            return CleanObjectName(label);
                    }
                    // Otherwise use text before _inputField
                    string prefix = name.Substring(0, idx);
                    // Remove common prefixes like "Login_"
                    if (prefix.Contains("_"))
                        prefix = prefix.Substring(prefix.LastIndexOf('_') + 1);
                    return CleanObjectName(prefix);
                }

                current = current.parent;
                maxLevels--;
            }

            return null;
        }

        /// <summary>
        /// Search a container's subtree for a localized label (Localize component first, then
        /// TMP_Text fallback), skipping anything inside the excluded subtree. Used to pick up
        /// sibling label TMP_Texts that wrap input fields in the RegistrationPanel.
        /// </summary>
        private static string FindLocalizedLabelInContainer(GameObject container, GameObject excludeSubtree)
        {
            if (container == null) return null;

            string localized = TryGetLocalizeTextExcluding(container, excludeSubtree);
            if (!string.IsNullOrEmpty(localized))
                return localized;

            foreach (var tmp in container.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp == null) continue;
                if (excludeSubtree != null && tmp.transform.IsChildOf(excludeSubtree.transform)) continue;
                string cleaned = CleanText(tmp.text);
                if (string.IsNullOrWhiteSpace(cleaned) || cleaned.Length <= 1 || cleaned.Length > 60) continue;
                return cleaned;
            }

            return null;
        }

        private static string GetInputFieldText(TMP_InputField inputField)
        {
            // Try to get a meaningful label from the parent hierarchy
            string fieldLabel = TryGetInputFieldLabel(inputField.gameObject);

            // If there's user input, report it
            // Try .text first, then fall back to textComponent.text (displayed text)
            string userText = CleanText(inputField.text);
            if (string.IsNullOrWhiteSpace(userText) && inputField.textComponent != null)
            {
                userText = CleanText(inputField.textComponent.text);
            }
            if (!string.IsNullOrWhiteSpace(userText))
            {
                // For password fields, don't read the actual text
                if (inputField.inputType == TMP_InputField.InputType.Password)
                {
                    if (!string.IsNullOrEmpty(fieldLabel))
                        return $"{fieldLabel}, has text";
                    return "password field, has text";
                }

                // Show label and content
                if (!string.IsNullOrEmpty(fieldLabel))
                    return $"{fieldLabel}: {userText}";
                return userText;
            }

            // Try to get placeholder text
            if (inputField.placeholder != null)
            {
                var placeholderText = inputField.placeholder.GetComponent<TMP_Text>();
                if (placeholderText != null)
                {
                    string placeholder = CleanText(placeholderText.text);
                    if (!string.IsNullOrWhiteSpace(placeholder))
                    {
                        string empty = Models.Strings.InputFieldEmpty;
                        if (!string.IsNullOrEmpty(fieldLabel))
                            return $"{fieldLabel}, {empty}";
                        return $"{placeholder}, {empty}";
                    }
                }
            }

            // Use derived label if we have one
            if (!string.IsNullOrEmpty(fieldLabel))
                return $"{fieldLabel}, {Models.Strings.InputFieldEmpty}";

            // Fall back to field name
            string fieldName = CleanObjectName(inputField.gameObject.name);
            if (!string.IsNullOrWhiteSpace(fieldName))
            {
                return $"{fieldName}, {Models.Strings.InputFieldEmpty}";
            }

            return Models.Strings.InputFieldEmpty;
        }

        private static string GetInputFieldText(InputField inputField)
        {
            // Try .text first, then fall back to textComponent.text (displayed text)
            string text = inputField.text;
            if (string.IsNullOrWhiteSpace(text) && inputField.textComponent != null)
            {
                text = inputField.textComponent.text;
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                if (inputField.inputType == InputField.InputType.Password)
                    return "password field, contains text";

                return $"{CleanText(text)}, {Models.Strings.TextField}";
            }

            if (inputField.placeholder != null)
            {
                var placeholderText = inputField.placeholder.GetComponent<Text>();
                if (placeholderText != null && !string.IsNullOrWhiteSpace(placeholderText.text))
                {
                    return $"{CleanText(placeholderText.text)}, {Models.Strings.TextField}, {Models.Strings.InputFieldEmpty}";
                }
            }

            return $"{Models.Strings.TextField}, {Models.Strings.InputFieldEmpty}";
        }

        private static string GetToggleText(Toggle toggle)
        {
            // Only return the label text - UIElementClassifier handles adding "checkbox, checked/unchecked"

            // Try Localize component first — ensures localized text for toggles like Sideboard
            string localizeText = TryGetLocalizeText(toggle.gameObject);
            if (!string.IsNullOrEmpty(localizeText))
            {
                if (localizeText.Contains("POSITION"))
                    return Models.Strings.Bo3Toggle();
                return localizeText;
            }

            // Try to find associated label
            var label = toggle.GetComponentInChildren<TMP_Text>();
            if (label != null && !string.IsNullOrWhiteSpace(label.text))
            {
                string text = CleanText(label.text);

                // Fix BO3 toggle label: game uses "POSITION" as placeholder text
                if (text.Contains("POSITION"))
                    return Models.Strings.Bo3Toggle();

                return text;
            }

            var legacyLabel = toggle.GetComponentInChildren<Text>();
            if (legacyLabel != null && !string.IsNullOrWhiteSpace(legacyLabel.text))
            {
                string text = CleanText(legacyLabel.text);

                // Fix BO3 toggle label for legacy text too
                if (text.Contains("POSITION"))
                    return Models.Strings.Bo3Toggle();

                return text;
            }

            // Fallback: check all child TMP_Text for "POSITION" placeholder
            // The first GetComponentInChildren may find a different text child
            var allTexts = toggle.GetComponentsInChildren<TMP_Text>(true);
            foreach (var tmp in allTexts)
            {
                if (tmp != null && tmp.text != null && tmp.text.Contains("POSITION"))
                    return Models.Strings.Bo3Toggle();
            }

            // Parent-subtree fallback: the label TMP_Text for toggles like the RegistrationPanel
            // checkboxes (OffersToggle, TermsConditionsToggle, ...) is a sibling of the inner
            // "Toggle" GO, not a descendant. ONLY walk into the parent's subtree when the parent
            // is a true 1:1 wrapper around a single Toggle — otherwise the parent is a row /
            // container that holds many siblings (search input, tooltips, neighbour toggles)
            // whose text leaks into the wrong toggle (e.g. deck-builder color filters picked up
            // the search input's "Suche ..." placeholder, then an event-pool tooltip). The
            // 1:1-wrapper test rules those out: registration wrappers contain exactly one Toggle,
            // multi-toggle filter rows do not.
            var parent = toggle.transform.parent;
            if (parent != null && IsSingleToggleWrapper(parent, toggle))
            {
                string parentLocalizeText = TryGetLocalizeTextExcluding(parent.gameObject, toggle.gameObject);
                if (!string.IsNullOrEmpty(parentLocalizeText))
                {
                    if (parentLocalizeText.Contains("POSITION"))
                        return Models.Strings.Bo3Toggle();
                    return parentLocalizeText;
                }

                foreach (var tmp in parent.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (tmp == null) continue;
                    if (tmp.transform.IsChildOf(toggle.transform)) continue;
                    string cleaned = CleanText(tmp.text);
                    if (string.IsNullOrWhiteSpace(cleaned) || cleaned.Length <= 1) continue;
                    if (cleaned.Contains("POSITION"))
                        return Models.Strings.Bo3Toggle();
                    return cleaned;
                }
            }

            // Return empty - UIElementClassifier will use object name as fallback
            return string.Empty;
        }

        /// <summary>
        /// True when <paramref name="parent"/> is a 1:1 wrapper around <paramref name="toggle"/>:
        /// the only Toggle in the parent's subtree is this one. Registration-panel wrappers like
        /// OffersToggle satisfy this; multi-toggle filter rows (e.g. deck-builder color filters)
        /// do not.
        /// </summary>
        private static bool IsSingleToggleWrapper(Transform parent, Toggle toggle)
        {
            if (parent == null) return false;
            var toggles = parent.GetComponentsInChildren<Toggle>(true);
            if (toggles == null || toggles.Length != 1) return false;
            return toggles[0] == toggle;
        }

        private static string GetDropdownText(TMP_Dropdown d) =>
            FormatDropdownText(d.value, d.options?.Count ?? 0,
                d.value >= 0 && d.value < (d.options?.Count ?? 0) ? d.options[d.value].text : null,
                d.captionText?.text, d.gameObject.name);

        private static string GetDropdownText(Dropdown d) =>
            FormatDropdownText(d.value, d.options?.Count ?? 0,
                d.value >= 0 && d.value < (d.options?.Count ?? 0) ? d.options[d.value].text : null,
                d.captionText?.text, d.gameObject.name);

        private static string FormatDropdownText(int value, int optionCount, string optionText, string captionText, string objectName)
        {
            if (value >= 0 && value < optionCount && optionText != null)
            {
                string pos = Strings.PositionOf(value + 1, optionCount);
                return $"{CleanText(optionText)}, dropdown" + (pos != "" ? $", {pos}" : "");
            }

            string label = null;
            if (!string.IsNullOrWhiteSpace(captionText))
                label = CleanText(captionText);

            if (string.IsNullOrWhiteSpace(label) || label.ToLower().Contains("select") || label.ToLower().Contains("choose"))
                label = CleanObjectName(objectName);

            return $"{label}, dropdown, no selection";
        }

        private static string GetScrollbarText(Scrollbar scrollbar)
        {
            // Scrollbar value is 0-1, convert to percentage
            int percent = Mathf.RoundToInt(scrollbar.value * 100);

            // Determine direction
            string direction = scrollbar.direction == Scrollbar.Direction.TopToBottom ||
                              scrollbar.direction == Scrollbar.Direction.BottomToTop
                ? "vertical" : "horizontal";

            // For vertical scrollbars, 0 = top, 1 = bottom (or vice versa)
            // Announce position relative to content
            string position;
            if (percent <= 5)
                position = "at top";
            else if (percent >= 95)
                position = "at bottom";
            else
                position = $"{percent} percent";

            return $"scrollbar, {direction}, {position}";
        }

        private static string GetSliderText(Slider slider)
        {
            // Calculate percentage based on slider range
            float range = slider.maxValue - slider.minValue;
            int percent = range > 0
                ? Mathf.RoundToInt((slider.value - slider.minValue) / range * 100)
                : 0;

            // Try to find an associated label
            var label = slider.GetComponentInChildren<TMP_Text>();
            string labelText = null;
            if (label != null)
            {
                labelText = CleanText(label.text);
            }
            else
            {
                var legacyLabel = slider.GetComponentInChildren<Text>();
                if (legacyLabel != null)
                    labelText = CleanText(legacyLabel.text);
            }

            if (!string.IsNullOrWhiteSpace(labelText))
                return $"{labelText}, slider, {percent} percent";

            return $"slider, {percent} percent";
        }
    }
}
