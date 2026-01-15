using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// Utility class to extract readable text from Unity UI GameObjects.
    /// Checks various UI components in priority order to find the best text representation.
    /// </summary>
    public static class UITextExtractor
    {
        /// <summary>
        /// Checks if the GameObject has actual text content (not just object name fallback).
        /// Used to distinguish elements with real labels from those with only internal names.
        /// </summary>
        public static bool HasActualText(GameObject gameObject)
        {
            if (gameObject == null)
                return false;

            // Check for input fields with content
            var tmpInputField = gameObject.GetComponent<TMP_InputField>();
            if (tmpInputField != null && !string.IsNullOrWhiteSpace(tmpInputField.text))
                return true;

            var inputField = gameObject.GetComponent<InputField>();
            if (inputField != null && !string.IsNullOrWhiteSpace(inputField.text))
                return true;

            // Check for TMP text
            var tmpText = gameObject.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                string cleaned = CleanText(tmpText.text);
                if (!string.IsNullOrWhiteSpace(cleaned))
                    return true;
            }

            // Check for legacy Text
            var text = gameObject.GetComponentInChildren<Text>();
            if (text != null)
            {
                string cleaned = CleanText(text.text);
                if (!string.IsNullOrWhiteSpace(cleaned))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Extracts the most relevant text from a UI GameObject.
        /// </summary>
        /// <param name="gameObject">The GameObject to extract text from</param>
        /// <returns>The extracted text, or the GameObject name as fallback</returns>
        public static string GetText(GameObject gameObject)
        {
            if (gameObject == null)
                return string.Empty;

            // Check for special label overrides (buttons with misleading game labels)
            string overrideLabel = GetLabelOverride(gameObject.name);
            if (overrideLabel != null)
                return overrideLabel;

            // Check if this is a deck entry (MetaDeckView) - look for parent with input field containing deck name
            string deckName = TryGetDeckName(gameObject);
            if (!string.IsNullOrEmpty(deckName))
            {
                return deckName;
            }

            // Check for input fields FIRST (they contain text children that we don't want to read directly)
            var tmpInputField = gameObject.GetComponent<TMP_InputField>();
            if (tmpInputField != null)
            {
                return GetInputFieldText(tmpInputField);
            }

            var inputField = gameObject.GetComponent<InputField>();
            if (inputField != null)
            {
                return GetInputFieldText(inputField);
            }

            // Try Toggle (checkbox)
            var toggle = gameObject.GetComponent<Toggle>();
            if (toggle != null)
            {
                return GetToggleText(toggle);
            }

            // Try Scrollbar
            var scrollbar = gameObject.GetComponent<Scrollbar>();
            if (scrollbar != null)
            {
                return GetScrollbarText(scrollbar);
            }

            // Try Slider
            var slider = gameObject.GetComponent<Slider>();
            if (slider != null)
            {
                return GetSliderText(slider);
            }

            // Try Dropdown
            var tmpDropdown = gameObject.GetComponent<TMP_Dropdown>();
            if (tmpDropdown != null)
            {
                return GetDropdownText(tmpDropdown);
            }

            var dropdown = gameObject.GetComponent<Dropdown>();
            if (dropdown != null)
            {
                return GetDropdownText(dropdown);
            }

            // Try TextMeshPro text
            var tmpText = gameObject.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                string cleaned = CleanText(tmpText.text);
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    return cleaned;
                }
            }

            // Try legacy Unity UI Text
            var text = gameObject.GetComponentInChildren<Text>();
            if (text != null)
            {
                string cleaned = CleanText(text.text);
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    return cleaned;
                }
            }

            // No text found on element itself - check siblings for label text
            // This helps with UI patterns where buttons get labels from sibling elements
            string siblingText = TryGetSiblingLabel(gameObject);
            if (!string.IsNullOrEmpty(siblingText))
            {
                return siblingText;
            }

            // Fallback to GameObject name (cleaned up)
            return CleanObjectName(gameObject.name);
        }

        /// <summary>
        /// Returns a label override for elements with misleading game labels.
        /// Used to provide better accessibility labels for buttons like the match end "Continue" button.
        /// </summary>
        private static string GetLabelOverride(string objectName)
        {
            // ExitMatchOverlayButton on MatchEndScene shows "View Battlefield" but actually continues to home
            if (objectName == "ExitMatchOverlayButton")
                return "Continue";

            return null;
        }

        /// <summary>
        /// Tries to extract a deck name from a deck entry element (MetaDeckView).
        /// Deck entries have a TMP_InputField with the actual deck name, but buttons
        /// show "Enter deck name..." placeholder. This method finds the real name.
        /// </summary>
        private static string TryGetDeckName(GameObject gameObject)
        {
            // Skip elements that are clearly not deck entries
            string objName = gameObject.name.ToLower();
            if (objName.Contains("folder") ||
                objName.Contains("toggle") ||
                objName.Contains("bot") ||
                objName.Contains("match") ||
                objName.Contains("avatar") ||
                objName.Contains("scrollbar"))
            {
                return null;
            }

            // Walk up the hierarchy looking for deck entry indicators
            Transform current = gameObject.transform;
            int maxLevels = 3; // Only go up 3 levels - deck entries are compact

            while (current != null && maxLevels > 0)
            {
                string name = current.gameObject.name;

                // Skip if we hit a container that's too high up (not a single deck entry)
                if (name.Contains("Content") ||
                    name.Contains("Viewport") ||
                    name.Contains("Scroll") ||
                    name.Contains("Folder"))
                {
                    return null; // Went too far up, not a deck entry
                }

                // Check for deck entry patterns - must be specific
                // Blade_ListItem_Base is the individual deck entry container
                if (name.Contains("Blade_ListItem") ||
                    name.Contains("DeckListItem") ||
                    (name.Contains("DeckView") && !name.Contains("Selector") && !name.Contains("Folder")))
                {
                    // Found a deck entry container - look for TMP_InputField with actual text
                    var inputFields = current.GetComponentsInChildren<TMP_InputField>(true);
                    foreach (var inputField in inputFields)
                    {
                        // Get the actual text value, not placeholder
                        string deckText = inputField.text;
                        if (!string.IsNullOrWhiteSpace(deckText))
                        {
                            // Remove zero-width spaces
                            deckText = deckText.Replace("\u200B", "").Trim();
                            if (!string.IsNullOrWhiteSpace(deckText) && deckText.Length > 1)
                            {
                                return $"{deckText}, deck";
                            }
                        }
                    }

                    // No valid deck name found in this entry
                    return null;
                }

                current = current.parent;
                maxLevels--;
            }

            return null;
        }

        /// <summary>
        /// Tries to get a label from sibling elements when the element itself has no text.
        /// This handles UI patterns where a button's label comes from a sibling element.
        /// Example: Color Challenge buttons have an "INFO" sibling with the color name.
        /// </summary>
        private static string TryGetSiblingLabel(GameObject gameObject)
        {
            if (gameObject == null) return null;

            var parent = gameObject.transform.parent;
            if (parent == null) return null;

            // Look through siblings for text content
            foreach (Transform sibling in parent)
            {
                // Skip self
                if (sibling.gameObject == gameObject) continue;

                // Skip decorative/structural elements
                string sibName = sibling.name.ToUpper();
                if (sibName.Contains("MASK") ||
                    sibName.Contains("SHADOW") ||
                    sibName.Contains("DIVIDER") ||
                    sibName.Contains("BACKGROUND") ||
                    sibName.Contains("INDICATION"))
                {
                    continue;
                }

                // Try to get text from this sibling
                var tmpText = sibling.GetComponentInChildren<TMP_Text>();
                if (tmpText != null)
                {
                    string cleaned = CleanText(tmpText.text);
                    // Must be meaningful text (not just single char or placeholder)
                    if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length > 1)
                    {
                        return cleaned;
                    }
                }

                var legacyText = sibling.GetComponentInChildren<Text>();
                if (legacyText != null)
                {
                    string cleaned = CleanText(legacyText.text);
                    if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length > 1)
                    {
                        return cleaned;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the type of UI element for additional context.
        /// </summary>
        public static string GetElementType(GameObject gameObject)
        {
            if (gameObject == null)
                return "unknown";

            // Check for card first (before button, since cards may have button-like components)
            if (CardDetector.IsCard(gameObject))
                return "card";

            if (gameObject.GetComponent<Button>() != null)
                return "button";

            if (gameObject.GetComponent<TMP_InputField>() != null || gameObject.GetComponent<InputField>() != null)
                return "text field";

            if (gameObject.GetComponent<Toggle>() != null)
                return "checkbox";

            if (gameObject.GetComponent<TMP_Dropdown>() != null || gameObject.GetComponent<Dropdown>() != null)
                return "dropdown";

            if (gameObject.GetComponent<Slider>() != null)
                return "slider";

            if (gameObject.GetComponent<Scrollbar>() != null)
                return "scrollbar";

            if (gameObject.GetComponent<Selectable>() != null)
                return "control";

            return "item";
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

        private static string GetInputFieldText(TMP_InputField inputField)
        {
            // If there's user input, report it
            string userText = CleanText(inputField.text);
            if (!string.IsNullOrWhiteSpace(userText))
            {
                // For password fields, don't read the actual text
                if (inputField.inputType == TMP_InputField.InputType.Password)
                    return "password field, contains text";

                return $"{userText}, text field";
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
                        return $"{placeholder}, text field, empty";
                    }
                }
            }

            // Fall back to field name
            string fieldName = CleanObjectName(inputField.gameObject.name);
            if (!string.IsNullOrWhiteSpace(fieldName))
            {
                return $"{fieldName}, text field, empty";
            }

            return "text field, empty";
        }

        private static string GetInputFieldText(InputField inputField)
        {
            if (!string.IsNullOrWhiteSpace(inputField.text))
            {
                if (inputField.inputType == InputField.InputType.Password)
                    return "password field, contains text";

                return $"{CleanText(inputField.text)}, text field";
            }

            if (inputField.placeholder != null)
            {
                var placeholderText = inputField.placeholder.GetComponent<Text>();
                if (placeholderText != null && !string.IsNullOrWhiteSpace(placeholderText.text))
                {
                    return $"{CleanText(placeholderText.text)}, text field, empty";
                }
            }

            return "text field, empty";
        }

        private static string GetToggleText(Toggle toggle)
        {
            // Only return the label text - UIElementClassifier handles adding "checkbox, checked/unchecked"
            // Try to find associated label
            var label = toggle.GetComponentInChildren<TMP_Text>();
            if (label != null && !string.IsNullOrWhiteSpace(label.text))
            {
                return CleanText(label.text);
            }

            var legacyLabel = toggle.GetComponentInChildren<Text>();
            if (legacyLabel != null && !string.IsNullOrWhiteSpace(legacyLabel.text))
            {
                return CleanText(legacyLabel.text);
            }

            // Return empty - UIElementClassifier will use object name as fallback
            return string.Empty;
        }

        private static string GetDropdownText(TMP_Dropdown dropdown)
        {
            string current = dropdown.options.Count > dropdown.value
                ? dropdown.options[dropdown.value].text
                : "unknown";

            return $"{CleanText(current)}, dropdown, {dropdown.value + 1} of {dropdown.options.Count}";
        }

        private static string GetDropdownText(Dropdown dropdown)
        {
            string current = dropdown.options.Count > dropdown.value
                ? dropdown.options[dropdown.value].text
                : "unknown";

            return $"{CleanText(current)}, dropdown, {dropdown.value + 1} of {dropdown.options.Count}";
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

        /// <summary>
        /// Cleans text by removing rich text tags, zero-width spaces, and normalizing whitespace.
        /// </summary>
        public static string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Remove zero-width space (common in MTGA empty fields)
            text = text.Replace("\u200B", "");

            // Remove rich text tags like <color>, <b>, etc.
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");

            // Normalize whitespace
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

            return text.Trim();
        }

        private static string CleanObjectName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "unknown";

            // Remove common Unity prefixes/suffixes
            name = name.Replace("(Clone)", "");
            name = name.Replace("Button", " button");
            name = name.Replace("Btn", " button");
            name = name.Replace("Toggle", " checkbox");
            name = name.Replace("InputField", " text field");
            name = name.Replace("Dropdown", " dropdown");

            // Convert PascalCase/camelCase to spaces
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");

            // Remove underscores
            name = name.Replace("_", " ");

            // Normalize whitespace
            name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+", " ");

            return name.Trim().ToLower();
        }
    }
}
