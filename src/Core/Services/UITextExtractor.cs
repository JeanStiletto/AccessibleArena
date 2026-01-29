using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AccessibleArena.Core.Services
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

            // Check if this is a booster pack element - look for CarouselBooster parent with pack name
            string boosterName = TryGetBoosterPackName(gameObject);
            if (!string.IsNullOrEmpty(boosterName))
            {
                return boosterName;
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

            // For FriendsWidget elements, try to get label from parent object name
            // Pattern: Button_AddFriend/Backer_Hitbox -> "Add Friend"
            string friendsWidgetLabel = TryGetFriendsWidgetLabel(gameObject);
            if (!string.IsNullOrEmpty(friendsWidgetLabel))
            {
                return friendsWidgetLabel;
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
        /// Tries to extract a booster pack name from a CarouselBooster element.
        /// Booster pack hitboxes don't have text - the pack name is in a sibling TMP_Text.
        /// </summary>
        private static string TryGetBoosterPackName(GameObject gameObject)
        {
            // Only process elements that look like booster hitboxes
            string objName = gameObject.name.ToLower();
            if (!objName.Contains("hitbox") && !objName.Contains("booster"))
                return null;

            // Walk up the hierarchy looking for CarouselBooster parent
            Transform current = gameObject.transform;
            int maxLevels = 6; // CarouselBooster can be several levels up
            Transform carouselBooster = null;

            while (current != null && maxLevels > 0)
            {
                string name = current.gameObject.name;

                // Found the CarouselBooster container
                if (name.Contains("CarouselBooster"))
                {
                    carouselBooster = current;
                    break;
                }

                // Stop if we hit the main chamber controller (went too far)
                if (name.Contains("BoosterChamber") || name.Contains("ContentController"))
                    break;

                current = current.parent;
                maxLevels--;
            }

            if (carouselBooster == null)
                return null;

            // Search CarouselBooster's children for TMP_Text with pack name
            // Use includeInactive=true to find text in inactive children too
            string packName = null;
            string packCount = null;

            var allTmpTexts = carouselBooster.GetComponentsInChildren<TMP_Text>(true);

            foreach (var tmpText in allTmpTexts)
            {
                if (tmpText == null)
                    continue;

                string text = tmpText.text?.Trim();

                if (string.IsNullOrEmpty(text))
                    continue;

                // Skip if it's just a number (count) or too short
                if (text.Length <= 2 && int.TryParse(text, out _))
                {
                    packCount = text;
                    continue;
                }

                // Skip placeholder/internal text
                if (text.ToLower().Contains("hitbox") || text.ToLower().Contains("booster mesh"))
                    continue;

                // This is likely the pack name
                if (text.Length > 2 && packName == null)
                {
                    packName = text;
                }
            }

            if (!string.IsNullOrEmpty(packName))
            {
                // Include count if available
                if (!string.IsNullOrEmpty(packCount))
                    return $"{packName} ({packCount})";
                return packName;
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
        /// Tries to get a label from parent object names for FriendsWidget elements.
        /// The friends panel uses Backer_Hitbox children inside parent containers like Button_AddFriend.
        /// Pattern: Button_AddFriend/Backer_Hitbox -> "Add Friend"
        /// </summary>
        private static string TryGetFriendsWidgetLabel(GameObject gameObject)
        {
            if (gameObject == null) return null;

            // Check if we're inside FriendsWidget
            Transform current = gameObject.transform;
            bool insideFriendsWidget = false;
            int maxLevels = 10;

            while (current != null && maxLevels > 0)
            {
                if (current.name.Contains("FriendsWidget"))
                {
                    insideFriendsWidget = true;
                    break;
                }
                current = current.parent;
                maxLevels--;
            }

            if (!insideFriendsWidget) return null;

            // Get the immediate parent name and try to extract a label from it
            var parent = gameObject.transform.parent;
            if (parent == null) return null;

            string parentName = parent.name;

            // Pattern: Button_Something -> "Something"
            if (parentName.StartsWith("Button_"))
            {
                string label = parentName.Substring(7); // Remove "Button_"
                // Clean up: AddFriend -> "Add Friend"
                label = CleanObjectName(label);
                return label;
            }

            // Pattern: Something_Button -> "Something"
            if (parentName.EndsWith("_Button"))
            {
                string label = parentName.Substring(0, parentName.Length - 7); // Remove "_Button"
                label = CleanObjectName(label);
                return label;
            }

            // For other patterns, check if parent has meaningful TMP_Text children
            // that might not be direct children of our element
            var parentTmpText = parent.GetComponentInChildren<TMP_Text>();
            if (parentTmpText != null)
            {
                string text = CleanText(parentTmpText.text);
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 1)
                {
                    return text;
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
                        if (!string.IsNullOrEmpty(fieldLabel))
                            return $"{fieldLabel}, empty";
                        return $"{placeholder}, empty";
                    }
                }
            }

            // Use derived label if we have one
            if (!string.IsNullOrEmpty(fieldLabel))
                return $"{fieldLabel}, empty";

            // Fall back to field name
            string fieldName = CleanObjectName(inputField.gameObject.name);
            if (!string.IsNullOrWhiteSpace(fieldName))
            {
                return $"{fieldName}, empty";
            }

            return "empty";
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

                return $"{CleanText(text)}, text field";
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

        /// <summary>
        /// Extracts the body text from a popup/dialog (SystemMessageView).
        /// Searches for text content in the MessageArea/Scroll View hierarchy.
        /// </summary>
        /// <param name="popupGameObject">The popup root GameObject (e.g., SystemMessageView_Desktop_16x9(Clone))</param>
        /// <returns>The popup body text, or null if not found</returns>
        public static string GetPopupBodyText(GameObject popupGameObject)
        {
            if (popupGameObject == null)
                return null;

            // Search paths for the message content (in priority order)
            string[] searchPaths = new[]
            {
                "SystemMessageView_OK_Cancel/MessageArea/Scroll View/Viewport/Content",
                "SystemMessageView_OK/MessageArea/Scroll View/Viewport/Content",
                "MessageArea/Scroll View/Viewport/Content",
                "MessageArea/Content",
                "Content"
            };

            Transform contentTransform = null;

            foreach (var path in searchPaths)
            {
                contentTransform = popupGameObject.transform.Find(path);
                if (contentTransform != null)
                    break;
            }

            // If exact path not found, search recursively for MessageArea
            if (contentTransform == null)
            {
                contentTransform = FindChildRecursive(popupGameObject.transform, "MessageArea");
                if (contentTransform != null)
                {
                    // Look for text within MessageArea
                    var msgAreaText = contentTransform.GetComponentInChildren<TMP_Text>(true);
                    if (msgAreaText != null)
                    {
                        string text = CleanText(msgAreaText.text);
                        if (!string.IsNullOrWhiteSpace(text))
                            return text;
                    }
                }
            }

            // Try to get text from the content transform
            if (contentTransform != null)
            {
                // Get all TMP_Text components and concatenate meaningful text
                var texts = contentTransform.GetComponentsInChildren<TMP_Text>(true);
                var bodyParts = new System.Collections.Generic.List<string>();

                foreach (var tmpText in texts)
                {
                    if (tmpText == null) continue;

                    string text = CleanText(tmpText.text);

                    // Skip empty, single-char, or button-like text
                    if (string.IsNullOrWhiteSpace(text) || text.Length <= 1)
                        continue;

                    // Skip if it looks like a button label (common button texts)
                    string lower = text.ToLowerInvariant();
                    if (lower == "ok" || lower == "cancel" || lower == "yes" || lower == "no" ||
                        lower == "accept" || lower == "decline" || lower == "close" ||
                        lower == "weiterbearbeiten" || lower == "deck verwerfen" || lower == "abbrechen")
                        continue;

                    bodyParts.Add(text);
                }

                if (bodyParts.Count > 0)
                {
                    return string.Join(" ", bodyParts);
                }
            }

            // Fallback: search for any TMP_Text in popup that's not in ButtonLayout
            var allTexts = popupGameObject.GetComponentsInChildren<TMP_Text>(true);
            foreach (var tmpText in allTexts)
            {
                if (tmpText == null) continue;

                // Skip text inside button containers
                if (IsInsideButtonContainer(tmpText.transform))
                    continue;

                string text = CleanText(tmpText.text);

                // Must be substantial text (likely the message body)
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 10)
                    return text;
            }

            return null;
        }

        /// <summary>
        /// Finds a child transform by name recursively.
        /// </summary>
        private static Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Contains(name))
                    return child;

                var found = FindChildRecursive(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Checks if a transform is inside a button container.
        /// </summary>
        private static bool IsInsideButtonContainer(Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                string name = current.name.ToLowerInvariant();
                if (name.Contains("button") || name.Contains("btn"))
                    return true;
                current = current.parent;
            }
            return false;
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
