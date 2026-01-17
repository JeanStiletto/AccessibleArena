using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Text.RegularExpressions;
using MelonLoader;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// Classifies UI elements by their role and determines navigability.
    /// Used by navigators to properly label elements for screen readers.
    /// </summary>
    public static class UIElementClassifier
    {
        // Compiled regex patterns for performance
        private static readonly Regex ProgressFractionPattern = new Regex(@"^\d+/\d+", RegexOptions.Compiled);
        private static readonly Regex ProgressPercentPattern = new Regex(@"^\d+%", RegexOptions.Compiled);
        private static readonly Regex HtmlTagPattern = new Regex(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex WhitespacePattern = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex CamelCasePattern = new Regex("([a-z])([A-Z])", RegexOptions.Compiled);

        // String comparison helper to avoid ToLower() allocations
        private static readonly System.StringComparison IgnoreCase = System.StringComparison.OrdinalIgnoreCase;

        /// <summary>
        /// Case-insensitive Contains check without string allocation
        /// </summary>
        private static bool ContainsIgnoreCase(string source, string value)
        {
            return source.IndexOf(value, IgnoreCase) >= 0;
        }

        /// <summary>
        /// Case-insensitive Equals check without string allocation
        /// </summary>
        private static bool EqualsIgnoreCase(string source, string value)
        {
            return string.Equals(source, value, IgnoreCase);
        }

        /// <summary>
        /// UI element roles for screen reader announcement
        /// </summary>
        public enum ElementRole
        {
            Button,
            Link,
            Toggle,
            Slider,
            Dropdown,
            TextField,
            ProgressBar,
            Label,
            Navigation,
            Scrollbar,
            Card,
            Internal,  // Hidden/internal elements that shouldn't be announced
            Unknown
        }

        /// <summary>
        /// Result of classifying a UI element
        /// </summary>
        public class ClassificationResult
        {
            public ElementRole Role { get; set; }
            public string Label { get; set; }
            public string RoleLabel { get; set; }  // "button", "progress", etc.
            public bool IsNavigable { get; set; }
            public bool ShouldAnnounce { get; set; }

            /// <summary>
            /// If true, this element supports left/right arrow navigation (e.g., carousel)
            /// </summary>
            public bool HasArrowNavigation { get; set; }

            /// <summary>
            /// Reference to the "previous" control for arrow navigation
            /// </summary>
            public GameObject PreviousControl { get; set; }

            /// <summary>
            /// Reference to the "next" control for arrow navigation
            /// </summary>
            public GameObject NextControl { get; set; }
        }

        /// <summary>
        /// Classify a UI element and determine its role, label, and navigability.
        /// </summary>
        public static ClassificationResult Classify(GameObject obj)
        {
            if (obj == null)
                return new ClassificationResult { Role = ElementRole.Unknown, IsNavigable = false, ShouldAnnounce = false };

            var result = new ClassificationResult();

            // Get the raw text content
            string text = UITextExtractor.GetText(obj);
            string objName = obj.name;

            // First check if this is an internal/hidden element
            if (IsInternalElement(obj, objName, text))
            {
                result.Role = ElementRole.Internal;
                result.IsNavigable = false;
                result.ShouldAnnounce = false;
                return result;
            }

            // Check for card (before button since cards may have button components)
            if (CardDetector.IsCard(obj))
            {
                result.Role = ElementRole.Card;
                // Use CardDetector for proper card name extraction
                // This handles reward cards where "+99" indicator might be first text found
                result.Label = CardDetector.GetCardName(obj);
                result.RoleLabel = "card";
                result.IsNavigable = true;
                result.ShouldAnnounce = true;
                return result;
            }

            // Check for Settings stepper controls (Increment/Decrement buttons)
            // These are standard Buttons inside "Control - X" parents for settings like Graphics quality
            if (IsSettingsStepperButton(obj, objName, out string stepperLabel, out string stepperRole))
            {
                result.Role = ElementRole.Button;
                result.Label = stepperLabel;
                result.RoleLabel = stepperRole;
                result.IsNavigable = true;
                result.ShouldAnnounce = true;
                return result;
            }

            // Check for Settings dropdown controls (Control - X_Dropdown pattern)
            if (IsSettingsDropdownControl(obj, objName, out string dropdownLabel, out string dropdownValue))
            {
                result.Role = ElementRole.Dropdown;
                result.Label = !string.IsNullOrEmpty(dropdownValue)
                    ? $"{dropdownLabel}: {dropdownValue}"
                    : dropdownLabel;
                result.RoleLabel = "dropdown";
                result.IsNavigable = true;
                result.ShouldAnnounce = true;
                return result;
            }

            // Check standard Unity components (cache GetComponent results)
            var toggle = obj.GetComponent<Toggle>();
            if (toggle != null)
            {
                result.Role = ElementRole.Toggle;
                result.Label = GetCleanLabel(text, objName);
                result.RoleLabel = toggle.isOn ? "checkbox, checked" : "checkbox, unchecked";
                result.IsNavigable = true;
                result.ShouldAnnounce = true;
                return result;
            }

            var slider = obj.GetComponent<Slider>();
            if (slider != null)
            {
                int percent = CalculateSliderPercent(slider);
                result.Role = ElementRole.Slider;
                result.Label = GetCleanLabel(text, objName);
                result.RoleLabel = $"slider, {percent} percent";
                result.IsNavigable = true;
                result.ShouldAnnounce = true;
                return result;
            }

            var tmpDropdown = obj.GetComponent<TMP_Dropdown>();
            var unityDropdown = obj.GetComponent<Dropdown>();
            if (tmpDropdown != null || unityDropdown != null)
            {
                result.Role = ElementRole.Dropdown;

                // Get the current selected value
                string selectedValue = null;
                if (tmpDropdown != null && tmpDropdown.options != null && tmpDropdown.options.Count > tmpDropdown.value)
                {
                    selectedValue = tmpDropdown.options[tmpDropdown.value].text;
                }
                else if (unityDropdown != null && unityDropdown.options != null && unityDropdown.options.Count > unityDropdown.value)
                {
                    selectedValue = unityDropdown.options[unityDropdown.value].text;
                }

                // Check if this is inside a Settings dropdown control (Control - X_Dropdown)
                string settingLabel = GetSettingsDropdownLabel(obj.transform);
                if (!string.IsNullOrEmpty(settingLabel))
                {
                    // Format as "Setting Name: Current Value"
                    result.Label = !string.IsNullOrEmpty(selectedValue)
                        ? $"{settingLabel}: {selectedValue}"
                        : settingLabel;
                }
                else
                {
                    // Use selected value or fallback to generic label
                    result.Label = !string.IsNullOrEmpty(selectedValue) ? selectedValue : text;
                }

                result.RoleLabel = "dropdown";
                result.IsNavigable = true;
                result.ShouldAnnounce = true;
                return result;
            }

            if (obj.GetComponent<TMP_InputField>() != null || obj.GetComponent<InputField>() != null)
            {
                result.Role = ElementRole.TextField;
                result.Label = text;
                result.RoleLabel = "text field";
                result.IsNavigable = true;
                result.ShouldAnnounce = true;
                return result;
            }

            if (obj.GetComponent<Scrollbar>() != null)
            {
                result.Role = ElementRole.Scrollbar;
                result.Label = text;
                result.RoleLabel = "scrollbar";
                result.IsNavigable = false;  // Usually not directly navigated
                result.ShouldAnnounce = false;
                return result;
            }

            // Check for progress indicators (before button check)
            // These are informational only, not meant for direct navigation
            if (IsProgressIndicator(obj, objName, text))
            {
                result.Role = ElementRole.ProgressBar;
                result.Label = text;
                result.RoleLabel = "progress";
                result.IsNavigable = false;  // Progress indicators are informational, not interactive
                result.ShouldAnnounce = false;
                return result;
            }

            // Check for navigation arrows
            if (IsNavigationArrow(obj, objName, text))
            {
                result.Role = ElementRole.Navigation;
                result.Label = GetNavigationLabel(objName);
                result.RoleLabel = "navigation";
                result.IsNavigable = true;
                result.ShouldAnnounce = true;
                return result;
            }

            // Check for CustomButton (MTGA-specific)
            bool hasCustomButton = HasCustomButton(obj);

            // Check for standard Button
            bool hasButton = obj.GetComponent<Button>() != null;

            // Check for EventTrigger (often used as clickable)
            bool hasEventTrigger = obj.GetComponent<UnityEngine.EventSystems.EventTrigger>() != null;

            if (hasCustomButton || hasButton || hasEventTrigger)
            {
                // Check if this is a carousel element (has nav controls as children)
                GameObject prevControl, nextControl;
                if (IsCarouselElement(obj, out prevControl, out nextControl))
                {
                    result.Role = ElementRole.Button;
                    result.Label = GetCleanLabel(text, objName);
                    result.RoleLabel = "carousel, use left and right arrows";
                    result.IsNavigable = true;
                    result.ShouldAnnounce = true;
                    result.HasArrowNavigation = true;
                    result.PreviousControl = prevControl;
                    result.NextControl = nextControl;
                    return result;
                }

                // Determine if it's a link or button based on content
                if (IsLinkElement(objName, text))
                {
                    result.Role = ElementRole.Link;
                    result.Label = GetCleanLabel(text, objName);
                    result.RoleLabel = "link";
                }
                else
                {
                    result.Role = ElementRole.Button;
                    result.Label = GetCleanLabel(text, objName);
                    result.RoleLabel = "button";
                }
                result.IsNavigable = true;
                result.ShouldAnnounce = true;
                return result;
            }

            // Check for labels (non-interactive text)
            if (IsLabelElement(obj, objName))
            {
                result.Role = ElementRole.Label;
                result.Label = text;
                result.RoleLabel = ""; // Labels don't need role announcement
                result.IsNavigable = false;
                result.ShouldAnnounce = !string.IsNullOrEmpty(text);
                return result;
            }

            // Default: Unknown
            result.Role = ElementRole.Unknown;
            result.Label = text;
            result.RoleLabel = "";
            result.IsNavigable = false;
            result.ShouldAnnounce = false;
            return result;
        }

        // CustomButton type names used in MTGA
        private const string CustomButtonTypeName = "CustomButton";
        private const string CustomButtonWithTooltipTypeName = "CustomButtonWithTooltip";

        /// <summary>
        /// Check if element has MTGA's CustomButton component
        /// </summary>
        public static bool HasCustomButton(GameObject obj)
        {
            return GetCustomButton(obj) != null;
        }

        /// <summary>
        /// Get CustomButton component from GameObject
        /// </summary>
        public static MonoBehaviour GetCustomButton(GameObject obj)
        {
            var components = obj.GetComponents<MonoBehaviour>();
            return components.FirstOrDefault(c => c != null && IsCustomButtonType(c.GetType().Name));
        }

        /// <summary>
        /// Check if type name matches a CustomButton type
        /// </summary>
        private static bool IsCustomButtonType(string typeName)
        {
            return typeName == CustomButtonTypeName || typeName == CustomButtonWithTooltipTypeName;
        }

        /// <summary>
        /// Check if element has MTGA's MainButton component (used for main action buttons like Play, Submit Deck)
        /// </summary>
        public static bool HasMainButtonComponent(GameObject obj)
        {
            var components = obj.GetComponents<MonoBehaviour>();
            return components.Any(c => c != null && c.GetType().Name == "MainButton");
        }

        /// <summary>
        /// Check if CustomButton is interactable using game's internal property
        /// </summary>
        public static bool IsCustomButtonInteractable(GameObject obj)
        {
            var customButton = GetCustomButton(obj);
            if (customButton == null) return true; // Not a CustomButton, assume interactable

            var type = customButton.GetType();

            // Check Interactable property
            var interactableProp = type.GetProperty("Interactable",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (interactableProp != null)
            {
                try
                {
                    bool interactable = (bool)interactableProp.GetValue(customButton);
                    if (!interactable) return false;
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Warning($"[UIElementClassifier] Failed to get Interactable property: {ex.Message}");
                }
            }

            // Check IsHidden() method (for CustomButtonWithTooltip)
            var isHiddenMethod = type.GetMethod("IsHidden",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                null, new System.Type[0], null);
            if (isHiddenMethod != null)
            {
                try
                {
                    bool isHidden = (bool)isHiddenMethod.Invoke(customButton, null);
                    if (isHidden) return false;
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Warning($"[UIElementClassifier] Failed to invoke IsHidden method: {ex.Message}");
                }
            }

            return true;
        }

        /// <summary>
        /// Check if element is visible via CanvasGroup
        /// </summary>
        public static bool IsVisibleViaCanvasGroup(GameObject obj, bool debugLog = false)
        {
            // Check own CanvasGroup
            var canvasGroup = obj.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                // MTGA uses alpha < 0.1 for hidden elements (see docs/MENU_NAVIGATION.md)
                if (canvasGroup.alpha < 0.1f)
                {
                    if (debugLog) MelonLoader.MelonLogger.Msg($"[UIClassifier] {obj.name} hidden: own CanvasGroup alpha={canvasGroup.alpha}");
                    return false;
                }
                // For interactable check, skip if this is a MainButton (action buttons like Submit Deck)
                // These may have interactable=false temporarily but should still be visible for accessibility
                // Also skip for elements with meaningful text content (not just icon buttons)
                bool isMainButton = HasMainButtonComponent(obj);
                bool hasMeaningfulContent = UITextExtractor.HasActualText(obj);
                if (!canvasGroup.interactable && !isMainButton && !hasMeaningfulContent)
                {
                    if (debugLog) MelonLoader.MelonLogger.Msg($"[UIClassifier] {obj.name} hidden: own CanvasGroup interactable=false");
                    return false;
                }
            }

            // Check parent CanvasGroups
            var parent = obj.transform.parent;
            while (parent != null)
            {
                var parentCG = parent.GetComponent<CanvasGroup>();
                if (parentCG != null)
                {
                    // Skip CanvasGroups that are named "CanvasGroup" - these are structural containers
                    // not actual visibility controls (e.g., "CanvasGroup - Overlay" in CampaignGraph)
                    bool isStructuralContainer = parent.name.StartsWith("CanvasGroup");

                    if (!isStructuralContainer)
                    {
                        if (parentCG.alpha < 0.1f)
                        {
                            if (debugLog) MelonLoader.MelonLogger.Msg($"[UIClassifier] {obj.name} hidden: parent {parent.name} CanvasGroup alpha={parentCG.alpha}");
                            return false;
                        }
                        if (!parentCG.interactable && !parentCG.ignoreParentGroups)
                        {
                            if (debugLog) MelonLoader.MelonLogger.Msg($"[UIClassifier] {obj.name} hidden: parent {parent.name} CanvasGroup interactable=false");
                            return false;
                        }
                    }
                }
                parent = parent.parent;
            }

            return true;
        }

        /// <summary>
        /// Get the full announcement string for an element
        /// </summary>
        public static string GetAnnouncement(GameObject obj)
        {
            var result = Classify(obj);
            if (!result.ShouldAnnounce)
                return null;

            if (string.IsNullOrEmpty(result.RoleLabel))
                return result.Label;

            return $"{result.Label}, {result.RoleLabel}";
        }

        #region Detection Helpers

        /// <summary>
        /// Check if element is inside the FriendsWidget (social panel friend list).
        /// Elements inside FriendsWidget should not be filtered by hitbox/backer patterns
        /// because they ARE the clickable friend list items.
        /// </summary>
        private static bool IsInsideFriendsWidget(GameObject obj)
        {
            if (obj == null) return false;

            Transform current = obj.transform;
            int maxLevels = 10;

            while (current != null && maxLevels > 0)
            {
                string name = current.name;
                if (ContainsIgnoreCase(name, "FriendsWidget"))
                    return true;
                current = current.parent;
                maxLevels--;
            }

            return false;
        }

        private static bool IsInternalElement(GameObject obj, string name, string text)
        {
            // Check game properties first (most reliable)
            if (IsHiddenByGameProperties(obj))
                return true;

            // Check name patterns
            if (IsFilteredByNamePattern(obj, name))
                return true;

            // Check text content
            if (IsFilteredByTextContent(name, text))
                return true;

            return false;
        }

        /// <summary>
        /// Check if element is hidden via game properties (CustomButton, CanvasGroup)
        /// </summary>
        private static bool IsHiddenByGameProperties(GameObject obj)
        {
            // Check if CustomButton says it's not interactable or hidden
            // But allow MainButton elements (like Submit Deck) and elements with actual text through
            // They may be temporarily disabled but should still show for accessibility
            if (HasCustomButton(obj) && !IsCustomButtonInteractable(obj))
            {
                bool isMainButton = HasMainButtonComponent(obj);
                bool hasMeaningfulContent = UITextExtractor.HasActualText(obj);
                if (!isMainButton && !hasMeaningfulContent)
                    return true;
            }

            // Check if CanvasGroup says it's invisible or non-interactable
            if (!IsVisibleViaCanvasGroup(obj))
                return true;

            // Filter decorative/graphical elements with no content and zero size
            if (IsDecorativeGraphicalElement(obj))
                return true;

            return false;
        }

        /// <summary>
        /// Check if element is a decorative/graphical element with no meaningful content.
        /// Filters elements that have: no actual text, no image, no text children, and zero/tiny size.
        /// Examples: avatar bust portraits, objective graphics, decorative icons.
        /// Functional icon buttons (like wildcard, social) have actual size and are not filtered.
        /// </summary>
        private static bool IsDecorativeGraphicalElement(GameObject obj)
        {
            // Must have no actual text content
            if (UITextExtractor.HasActualText(obj))
                return false;

            // Must have no Image component
            if (obj.GetComponent<Image>() != null || obj.GetComponent<RawImage>() != null)
                return false;

            // Must have no text children
            if (obj.GetComponentInChildren<TMP_Text>() != null)
                return false;

            // Must have zero or very small size (< 10 pixels in both dimensions)
            var rectTransform = obj.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Vector2 size = rectTransform.sizeDelta;
                if (size.x > 10 || size.y > 10)
                    return false; // Has meaningful size, keep it
            }

            // All conditions met - this is a decorative element
            return true;
        }

        /// <summary>
        /// Check if element should be filtered based on its name pattern
        /// </summary>
        private static bool IsFilteredByNamePattern(GameObject obj, string name)
        {
            // Click blockers (modal overlays)
            if (ContainsIgnoreCase(name, "blocker")) return true;

            // Navigation pips (carousel dots)
            if (ContainsIgnoreCase(name, "navpip") || ContainsIgnoreCase(name, "pip_")) return true;

            // Dismiss buttons (internal UI)
            if (ContainsIgnoreCase(name, "dismiss")) return true;

            // Internal button bases
            if (EqualsIgnoreCase(name, "button base") || EqualsIgnoreCase(name, "buttonbase")) return true;
            if (ContainsIgnoreCase(name, "button_base")) return true;

            // Fade overlays
            if (ContainsIgnoreCase(name, "fade") && !ContainsIgnoreCase(name, "nav")) return true;

            // Hitboxes without actual text content
            // BUT: Allow hitboxes inside FriendsWidget (they ARE the clickable friend items)
            if (ContainsIgnoreCase(name, "hitbox") && !UITextExtractor.HasActualText(obj))
            {
                if (!IsInsideFriendsWidget(obj))
                    return true;
            }

            // Backer elements from social panel (internal hitboxes)
            // BUT: Allow backer elements inside FriendsWidget (they ARE the clickable friend items)
            if (ContainsIgnoreCase(name, "backer") && !UITextExtractor.HasActualText(obj))
            {
                if (!IsInsideFriendsWidget(obj))
                    return true;
            }

            // Social corner icon without meaningful content
            // Filter this out - it's the small icon in the corner, not useful for navigation
            if (ContainsIgnoreCase(name, "socialcorner") || ContainsIgnoreCase(name, "social corner")) return true;

            // "New" badge indicators (appear on many elements)
            if (EqualsIgnoreCase(name, "new") || (ContainsIgnoreCase(name, "new") && ContainsIgnoreCase(name, "indicator"))) return true;

            // Viewport and scroll content
            if (ContainsIgnoreCase(name, "viewport")) return true;
            if (EqualsIgnoreCase(name, "content") && obj.GetComponent<RectTransform>() != null) return true;

            // Gradient decorations (but not nav gradients which are handled separately)
            if (ContainsIgnoreCase(name, "gradient") && !ContainsIgnoreCase(name, "nav")) return true;

            // Navigation controls that are part of carousels - hide them, the parent handles arrow keys
            if (IsCarouselNavControl(obj, name)) return true;

            // Top/bottom fades (settings menu decorations)
            if (ContainsIgnoreCase(name, "topfade") || ContainsIgnoreCase(name, "bottomfade")) return true;
            if (ContainsIgnoreCase(name, "top_fade") || ContainsIgnoreCase(name, "bottom_fade")) return true;

            // Background art/decorative elements (have CustomButton but are not interactive)
            // Pattern: Background_SubMenu_Art, BackgroundImage, etc.
            if (name.StartsWith("Background", System.StringComparison.OrdinalIgnoreCase) && !UITextExtractor.HasActualText(obj))
                return true;

            // BUTTONS container elements (EventTriggers that wrap actual buttons)
            // These appear in Color Challenge and similar screens as non-functional containers
            if (EqualsIgnoreCase(name, "BUTTONS")) return true;

            // Button_NPE overlay buttons (NPE = New Player Experience)
            // These are graphical overlays on objectives that duplicate the actual blade list buttons
            if (EqualsIgnoreCase(name, "Button_NPE")) return true;

            // Stop buttons (timer controls in duel/match end screens)
            // These are auto-pass timer controls, not meant for direct navigation
            // Includes "Stop", "Stop Second Strike", and similar variants
            if (EqualsIgnoreCase(name, "Stop") || name.StartsWith("Stop ", System.StringComparison.OrdinalIgnoreCase)) return true;

            // Duel prompt buttons that appear in MatchEndScene (leftover from duel)
            // These include "Pass Turn", "Cancel Attacks", broken "Ctrl" buttons
            if (IsMatchEndScene() && IsDuelPromptElement(obj, name)) return true;

            // Navigation arrows in MatchEndScene (leftover from duel)
            if (IsMatchEndScene() && IsNavigationArrow(obj, name, null)) return true;

            return false;
        }

        /// <summary>
        /// Check if we're currently in the MatchEndScene (victory/defeat screen)
        /// </summary>
        private static bool IsMatchEndScene()
        {
            // Check for MatchEndScene-specific elements
            var exitButton = GameObject.Find("ExitMatchOverlayButton");
            var viewButton = GameObject.Find("ViewBattlefieldButton");
            return (exitButton != null && exitButton.activeInHierarchy) ||
                   (viewButton != null && viewButton.activeInHierarchy);
        }

        /// <summary>
        /// Check if element is a duel prompt that shouldn't appear on MatchEndScene
        /// </summary>
        private static bool IsDuelPromptElement(GameObject obj, string name)
        {
            // Prompt buttons from duel
            if (ContainsIgnoreCase(name, "PromptButton")) return true;

            // End turn button container
            if (ContainsIgnoreCase(name, "EndTurnButton")) return true;

            // Button_Import inside EndTurnButton (the actual "Pass Turn" button)
            if (EqualsIgnoreCase(name, "Button_Import"))
            {
                var parent = obj.transform.parent;
                while (parent != null)
                {
                    if (ContainsIgnoreCase(parent.name, "EndTurnButton"))
                        return true;
                    parent = parent.parent;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if element should be filtered based on its text content
        /// </summary>
        private static bool IsFilteredByTextContent(string name, string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            string textTrimmed = text.Trim();

            // Placeholder and template text
            if (EqualsIgnoreCase(textTrimmed, "new")) return true;
            if (ContainsIgnoreCase(textTrimmed, "tooltip information")) return true;
            if (EqualsIgnoreCase(textTrimmed, "text text text")) return true;
            if (EqualsIgnoreCase(textTrimmed, "more information")) return true;

            // Numeric-only text in mail/notification elements = badge count, not real button
            if (ContainsIgnoreCase(name, "mail") || ContainsIgnoreCase(name, "notification") || ContainsIgnoreCase(name, "badge"))
            {
                if (IsNumericOnly(text))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Check if text contains only numeric characters (for filtering notification badges)
        /// </summary>
        private static bool IsNumericOnly(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string trimmed = text.Trim();
            foreach (char c in trimmed)
            {
                if (!char.IsDigit(c)) return false;
            }
            return trimmed.Length > 0;
        }

        /// <summary>
        /// Check if this element is a carousel navigation control (should be hidden, parent handles it)
        /// </summary>
        private static bool IsCarouselNavControl(GameObject obj, string name)
        {
            // Pattern: NavLeft_*, NavRight_*, *_NavLeft, *_NavRight
            if ((ContainsIgnoreCase(name, "navleft") || ContainsIgnoreCase(name, "navright") ||
                 ContainsIgnoreCase(name, "nav_left") || ContainsIgnoreCase(name, "nav_right")) &&
                ContainsIgnoreCase(name, "gradient"))
            {
                // Verify parent structure - should be inside a "Controls" container or similar
                var parent = obj.transform.parent;
                if (parent != null)
                {
                    string parentName = parent.name;
                    if (ContainsIgnoreCase(parentName, "control") || ContainsIgnoreCase(parentName, "nav") || ContainsIgnoreCase(parentName, "arrow"))
                    {
                        return true;
                    }
                }
                // Even without specific parent, nav gradients are carousel controls
                return true;
            }
            return false;
        }

        /// <summary>
        /// Check if an element is a carousel (has left/right navigation controls as children)
        /// </summary>
        public static bool IsCarouselElement(GameObject obj, out GameObject previousControl, out GameObject nextControl)
        {
            previousControl = null;
            nextControl = null;

            if (obj == null) return false;

            // Search for nav controls in children
            // Pattern: Look for children/descendants with NavLeft/NavRight patterns
            foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
            {
                if (child == obj.transform) continue;

                string childName = child.name;

                // Check for previous/left control
                if (previousControl == null &&
                    (ContainsIgnoreCase(childName, "navleft") || ContainsIgnoreCase(childName, "nav_left") ||
                     (ContainsIgnoreCase(childName, "left") && ContainsIgnoreCase(childName, "gradient")) ||
                     (ContainsIgnoreCase(childName, "previous") && !ContainsIgnoreCase(childName, "text"))))
                {
                    if (child.gameObject.activeInHierarchy)
                        previousControl = child.gameObject;
                }

                // Check for next/right control
                if (nextControl == null &&
                    (ContainsIgnoreCase(childName, "navright") || ContainsIgnoreCase(childName, "nav_right") ||
                     (ContainsIgnoreCase(childName, "right") && ContainsIgnoreCase(childName, "gradient")) ||
                     (ContainsIgnoreCase(childName, "next") && !ContainsIgnoreCase(childName, "text"))))
                {
                    if (child.gameObject.activeInHierarchy)
                        nextControl = child.gameObject;
                }

                // Found both, no need to continue
                if (previousControl != null && nextControl != null)
                    break;
            }

            // It's a carousel if we found at least one nav control
            return previousControl != null || nextControl != null;
        }

        /// <summary>
        /// Get the setting label from a Settings dropdown control parent (Control - X_Dropdown pattern).
        /// Returns null if not inside a Settings dropdown control.
        /// </summary>
        private static string GetSettingsDropdownLabel(Transform transform)
        {
            // Walk up to find "Control - X_Dropdown" parent (max 3 levels)
            Transform current = transform;
            int levels = 0;
            while (current != null && levels < 3)
            {
                string parentName = current.name;
                if (parentName.StartsWith("Control - ", System.StringComparison.OrdinalIgnoreCase) &&
                    parentName.EndsWith("_Dropdown", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Extract the setting name from "Control - X_Dropdown"
                    // Pattern: "Control - Quality_Dropdown" -> "Quality"
                    string label = parentName.Substring(10); // Remove "Control - "
                    int dropdownIdx = label.LastIndexOf("_Dropdown", System.StringComparison.OrdinalIgnoreCase);
                    if (dropdownIdx > 0)
                        label = label.Substring(0, dropdownIdx);

                    // Clean up the name (CamelCase to spaces)
                    label = CamelCasePattern.Replace(label, "$1 $2");
                    label = label.Replace("_", " ").Trim();

                    return label;
                }
                current = current.parent;
                levels++;
            }

            return null;
        }

        /// <summary>
        /// Check if this is a Settings dropdown control (Control - X_Dropdown pattern) and extract label/value.
        /// </summary>
        private static bool IsSettingsDropdownControl(GameObject obj, string name, out string label, out string currentValue)
        {
            label = null;
            currentValue = null;

            // Check if this object or its parent matches "Control - X_Dropdown" pattern
            Transform controlTransform = obj.transform;
            string controlName = null;

            // Walk up to find "Control - X_Dropdown" parent (max 3 levels)
            int levels = 0;
            while (controlTransform != null && levels < 3)
            {
                string parentName = controlTransform.name;
                if (parentName.StartsWith("Control - ", System.StringComparison.OrdinalIgnoreCase) &&
                    parentName.EndsWith("_Dropdown", System.StringComparison.OrdinalIgnoreCase))
                {
                    controlName = parentName;
                    break;
                }
                controlTransform = controlTransform.parent;
                levels++;
            }

            if (string.IsNullOrEmpty(controlName))
                return false;

            // Extract the setting name from "Control - X_Dropdown"
            // Pattern: "Control - Quality_Dropdown" -> "Quality"
            label = controlName.Substring(10); // Remove "Control - "
            int dropdownIdx = label.LastIndexOf("_Dropdown", System.StringComparison.OrdinalIgnoreCase);
            if (dropdownIdx > 0)
                label = label.Substring(0, dropdownIdx);

            // Clean up the name (CamelCase to spaces)
            label = CamelCasePattern.Replace(label, "$1 $2");
            label = label.Replace("_", " ").Trim();

            // Try to find the current selected value
            // First check if this object has a TMP_Dropdown - get value from selected option
            var tmpDropdown = obj.GetComponent<TMP_Dropdown>();
            if (tmpDropdown != null && tmpDropdown.options != null && tmpDropdown.options.Count > tmpDropdown.value)
            {
                currentValue = tmpDropdown.options[tmpDropdown.value].text;
                return true;
            }

            // Also check for Unity Dropdown
            var unityDropdown = obj.GetComponent<Dropdown>();
            if (unityDropdown != null && unityDropdown.options != null && unityDropdown.options.Count > unityDropdown.value)
            {
                currentValue = unityDropdown.options[unityDropdown.value].text;
                return true;
            }

            // Fallback: Look for "Value" child element (for non-dropdown controls)
            if (controlTransform != null)
            {
                currentValue = FindValueInControl(controlTransform, label);
            }

            return true;
        }

        /// <summary>
        /// Check if this is a Settings stepper button (Increment/Decrement) and extract proper label.
        /// These buttons are inside "Control - X" parents and need to show the setting name + current value.
        /// </summary>
        private static bool IsSettingsStepperButton(GameObject obj, string name, out string label, out string roleLabel)
        {
            label = null;
            roleLabel = null;

            // Check if this is an Increment or Decrement button
            bool isIncrement = ContainsIgnoreCase(name, "increment");
            bool isDecrement = ContainsIgnoreCase(name, "decrement");

            if (!isIncrement && !isDecrement)
                return false;

            // Must have a Button component
            if (obj.GetComponent<Button>() == null)
                return false;

            // Find the parent "Control - X" element
            Transform parent = obj.transform.parent;
            string settingName = null;
            string currentValue = null;
            Transform controlParent = null;

            // Walk up to find the Control parent (max 5 levels)
            int levels = 0;
            while (parent != null && levels < 5)
            {
                string parentName = parent.name;

                // Check if this is a "Control - X" parent
                if (parentName.StartsWith("Control - ", System.StringComparison.OrdinalIgnoreCase) ||
                    parentName.StartsWith("Control_", System.StringComparison.OrdinalIgnoreCase))
                {
                    controlParent = parent;

                    // Extract setting name from parent name
                    // Pattern: "Control - Setting: SettingName" or "Control - SettingName_Selector"
                    settingName = parentName;
                    if (settingName.StartsWith("Control - Setting: ", System.StringComparison.OrdinalIgnoreCase))
                        settingName = settingName.Substring(19); // Remove "Control - Setting: "
                    else if (settingName.StartsWith("Control - ", System.StringComparison.OrdinalIgnoreCase))
                        settingName = settingName.Substring(10);
                    else if (settingName.StartsWith("Control_", System.StringComparison.OrdinalIgnoreCase))
                        settingName = settingName.Substring(8);

                    // Remove suffix like "_Selector", "_Toggle"
                    int underscoreIdx = settingName.LastIndexOf('_');
                    if (underscoreIdx > 0)
                        settingName = settingName.Substring(0, underscoreIdx);

                    // Clean up the name (CamelCase to spaces)
                    settingName = CamelCasePattern.Replace(settingName, "$1 $2");
                    settingName = settingName.Replace("_", " ").Trim();

                    break;
                }

                parent = parent.parent;
                levels++;
            }

            // Now search WITHIN the control parent for value text
            // The value is typically in a child named "Value" or inside "Value BG"
            if (controlParent != null)
            {
                currentValue = FindValueInControl(controlParent, settingName);
            }

            // If we couldn't find a proper parent structure, fall back to simpler label
            if (string.IsNullOrEmpty(settingName))
            {
                // Try to get value from the button's own text
                currentValue = UITextExtractor.GetText(obj);
                label = currentValue ?? (isIncrement ? "Increase" : "Decrease");
                roleLabel = isIncrement ? "increment button" : "decrement button";
                return true;
            }

            // Build the label: "Setting Name: Current Value"
            if (!string.IsNullOrEmpty(currentValue))
                label = $"{settingName}: {currentValue}";
            else
                label = settingName;

            roleLabel = isIncrement ? "increment button" : "decrement button";
            return true;
        }

        /// <summary>
        /// Search within a Control element for the value text.
        /// The value is typically in a child named "Value" or inside "Value BG".
        /// </summary>
        private static string FindValueInControl(Transform controlParent, string settingName)
        {
            // Search all descendants for a "Value" element
            foreach (Transform child in controlParent.GetComponentsInChildren<Transform>(true))
            {
                string childName = child.name;

                // Look for elements named "Value" (not "Value BG" which is a container)
                if (EqualsIgnoreCase(childName, "Value"))
                {
                    var tmpText = child.GetComponent<TMP_Text>();
                    if (tmpText != null && !string.IsNullOrEmpty(tmpText.text))
                    {
                        string text = tmpText.text.Trim();
                        // Make sure it's not the setting name (label) but the actual value
                        if (!EqualsIgnoreCase(text, settingName) && text.Length < 30)
                        {
                            return text;
                        }
                    }
                }

                // Also check for "Text_Value" or similar patterns
                if (ContainsIgnoreCase(childName, "value") && !ContainsIgnoreCase(childName, "bg"))
                {
                    var tmpText = child.GetComponent<TMP_Text>();
                    if (tmpText != null && !string.IsNullOrEmpty(tmpText.text))
                    {
                        string text = tmpText.text.Trim();
                        if (!EqualsIgnoreCase(text, settingName) && text.Length < 30)
                        {
                            return text;
                        }
                    }
                }
            }

            return null;
        }

        private static bool IsProgressIndicator(GameObject obj, string name, string text)
        {
            // Check name patterns
            if (ContainsIgnoreCase(name, "progress")) return true;
            if (ContainsIgnoreCase(name, "objective")) return true;
            if (ContainsIgnoreCase(name, "battlepass")) return true;
            if (ContainsIgnoreCase(name, "mastery") && ContainsIgnoreCase(name, "level")) return true;

            // Check text patterns (e.g., "0/1000 XP", "5/10", "75%")
            if (!string.IsNullOrEmpty(text))
            {
                // Matches patterns like "0/1000", "5/10 XP", etc.
                if (ProgressFractionPattern.IsMatch(text))
                    return true;
                // Matches percentage patterns
                if (ProgressPercentPattern.IsMatch(text))
                    return true;
            }

            return false;
        }

        private static bool IsNavigationArrow(GameObject obj, string name, string text)
        {
            if (ContainsIgnoreCase(name, "navleft") || ContainsIgnoreCase(name, "nav_left")) return true;
            if (ContainsIgnoreCase(name, "navright") || ContainsIgnoreCase(name, "nav_right")) return true;
            if (ContainsIgnoreCase(name, "arrow") && (ContainsIgnoreCase(name, "left") || ContainsIgnoreCase(name, "right"))) return true;
            if (ContainsIgnoreCase(name, "previous") || ContainsIgnoreCase(name, "next")) return true;

            return false;
        }

        private static bool IsLinkElement(string name, string text)
        {
            // URLs or external links
            if (!string.IsNullOrEmpty(text))
            {
                if (ContainsIgnoreCase(text, "youtube")) return true;
                if (ContainsIgnoreCase(text, "subscribe")) return true;
                if (ContainsIgnoreCase(text, "http")) return true;
                if (ContainsIgnoreCase(text, "learn more")) return true;
            }

            return false;
        }

        private static bool IsLabelElement(GameObject obj, string name)
        {
            // Pure text elements without interactive components
            if (obj.GetComponent<TMPro.TMP_Text>() != null &&
                obj.GetComponent<Button>() == null &&
                !HasCustomButton(obj) &&
                obj.GetComponent<UnityEngine.EventSystems.EventTrigger>() == null)
            {
                return true;
            }

            if (ContainsIgnoreCase(name, "label")) return true;
            if (ContainsIgnoreCase(name, "title") && !ContainsIgnoreCase(name, "button")) return true;
            if (ContainsIgnoreCase(name, "header")) return true;

            return false;
        }

        private static string GetNavigationLabel(string name)
        {
            if (ContainsIgnoreCase(name, "left") || ContainsIgnoreCase(name, "previous"))
                return "Previous";
            if (ContainsIgnoreCase(name, "right") || ContainsIgnoreCase(name, "next"))
                return "Next";
            return "Navigate";
        }

        // Maximum label length - longer text is likely descriptive content, not a label
        private const int MaxLabelLength = 80;

        private static string GetCleanLabel(string text, string objName)
        {
            // Prefer text content if available and meaningful (not too short or too long)
            // Text over MaxLabelLength is likely paragraph content, fall back to object name
            if (!string.IsNullOrEmpty(text) && text.Length > 1 && text.Length < MaxLabelLength)
            {
                // Clean up the text using compiled regex
                text = HtmlTagPattern.Replace(text, "").Trim();
                text = WhitespacePattern.Replace(text, " ");

                // MTGA uses zero-width space for empty fields (see docs/BEST_PRACTICES.md)
                if (!string.IsNullOrEmpty(text) && text != "\u200B")
                    return text;
            }

            // Fall back to cleaned object name
            return CleanObjectName(objName);
        }

        private static string CleanObjectName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unknown";

            name = name.Replace("(Clone)", "");
            name = name.Replace("_", " ");
            name = CamelCasePattern.Replace(name, "$1 $2");
            name = WhitespacePattern.Replace(name, " ");
            name = name.Replace("Nav ", "");
            name = name.Replace("Button", "");
            name = name.Replace("Btn", "");

            return name.Trim();
        }

        private static int CalculateSliderPercent(Slider slider)
        {
            float range = slider.maxValue - slider.minValue;
            if (range <= 0) return 0;
            return Mathf.RoundToInt((slider.value - slider.minValue) / range * 100);
        }

        #endregion
    }
}
