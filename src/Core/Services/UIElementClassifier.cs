using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Text.RegularExpressions;
using MelonLoader;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Classifies UI elements by their role and determines navigability.
    /// Used by navigators to properly label elements for screen readers.
    /// </summary>
    public static class UIElementClassifier
    {
        #region Constants

        // Visibility thresholds
        private const float MinVisibleAlpha = 0.1f;
        private const int MinDecorativeSize = 10;

        // Hierarchy search depth limits
        private const int MaxParentSearchDepth = 5;
        private const int MaxFriendsWidgetSearchDepth = 10;
        private const int MaxDropdownSearchDepth = 3;

        // Label constraints
        private const int MaxLabelLength = 80;

        // Simple name patterns that always filter (case-insensitive Contains check)
        private static readonly string[] FilteredContainsPatterns = new[]
        {
            "blocker", "navpip", "pip_", "button_base", "viewport",
            "topfade", "bottomfade", "top_fade", "bottom_fade"
        };

        // Exact names that always filter (case-insensitive Equals check)
        private static readonly HashSet<string> FilteredExactNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "button base", "buttonbase", "new", "BUTTONS", "Button_NPE", "Stop"
        };

        #endregion

        #region Compiled Patterns

        // Compiled regex patterns for performance
        private static readonly Regex ProgressFractionPattern = new Regex(@"^\d+/\d+", RegexOptions.Compiled);
        private static readonly Regex ProgressPercentPattern = new Regex(@"^\d+%", RegexOptions.Compiled);
        private static readonly Regex HtmlTagPattern = new Regex(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex WhitespacePattern = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex CamelCasePattern = new Regex("([a-z])([A-Z])", RegexOptions.Compiled);
        private static readonly Regex SliderSuffixPattern = new Regex(@"\bslider\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ResolutionPattern = new Regex(@"\s*(Desktop|Mobile|Tablet)?\s*\d+x\d+\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // String comparison helper to avoid ToLower() allocations
        private static readonly System.StringComparison IgnoreCase = System.StringComparison.OrdinalIgnoreCase;

        #endregion

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
        /// Converts CamelCase or PascalCase text to space-separated words.
        /// Example: "MasterVolume" -> "Master Volume"
        /// </summary>
        private static string SplitCamelCase(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return CamelCasePattern.Replace(text, "$1 $2");
        }

        /// <summary>
        /// Cleans a setting/control label by splitting CamelCase and replacing underscores.
        /// </summary>
        private static string CleanSettingLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return label;
            label = SplitCamelCase(label);
            return label.Replace("_", " ").Trim();
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
            /// If true, this element supports left/right arrow navigation (e.g., carousel, slider)
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

            /// <summary>
            /// Reference to slider component for direct value adjustment via arrow keys
            /// </summary>
            public Slider SliderComponent { get; set; }
        }

        /// <summary>
        /// Classify a UI element and determine its role, label, and navigability.
        /// Uses a chain of TryClassifyAs* methods, each returning null if not applicable.
        /// </summary>
        public static ClassificationResult Classify(GameObject obj)
        {
            if (obj == null)
                return CreateResult(ElementRole.Unknown, null, "", false, false);

            string text = UITextExtractor.GetText(obj);
            string objName = obj.name;

            // Try each classification in priority order
            return TryClassifyAsInternal(obj, objName, text)
                ?? TryClassifyAsCard(obj)
                ?? TryClassifyAsStepperControl(obj, objName)
                ?? TryClassifyAsStepperNavControl(obj, objName)
                ?? TryClassifyAsSettingsDropdown(obj, objName)
                ?? TryClassifyAsToggle(obj, objName, text)
                ?? TryClassifyAsSlider(obj, objName)
                ?? TryClassifyAsDropdown(obj, text)
                ?? TryClassifyAsTextField(obj, text)
                ?? TryClassifyAsScrollbar(obj, text)
                ?? TryClassifyAsProgressIndicator(obj, objName, text)
                ?? TryClassifyAsNavigationArrow(obj, objName, text)
                ?? TryClassifyAsClickable(obj, objName, text)
                ?? TryClassifyAsLabel(obj, objName, text)
                ?? CreateResult(ElementRole.Unknown, text, "", false, false);
        }

        #region Classification Methods

        private static ClassificationResult TryClassifyAsInternal(GameObject obj, string objName, string text)
        {
            if (!IsInternalElement(obj, objName, text))
                return null;

            return CreateResult(ElementRole.Internal, null, "", false, false);
        }

        private static ClassificationResult TryClassifyAsCard(GameObject obj)
        {
            if (!CardDetector.IsCard(obj))
                return null;

            return CreateResult(ElementRole.Card, CardDetector.GetCardName(obj), "card", true, true);
        }

        private static ClassificationResult TryClassifyAsStepperControl(GameObject obj, string objName)
        {
            if (!IsSettingsStepperControl(obj, objName, out string label, out string value, out GameObject increment, out GameObject decrement))
                return null;

            return new ClassificationResult
            {
                Role = ElementRole.Button,
                Label = !string.IsNullOrEmpty(value) ? $"{label}: {value}" : label,
                RoleLabel = "stepper, use left and right arrows",
                IsNavigable = true,
                ShouldAnnounce = true,
                HasArrowNavigation = true,
                PreviousControl = decrement,
                NextControl = increment
            };
        }

        private static ClassificationResult TryClassifyAsStepperNavControl(GameObject obj, string objName)
        {
            if (!IsStepperNavControl(obj, objName))
                return null;

            return CreateResult(ElementRole.Internal, null, "", false, false);
        }

        private static ClassificationResult TryClassifyAsSettingsDropdown(GameObject obj, string objName)
        {
            if (!IsSettingsDropdownControl(obj, objName, out string label, out string value))
                return null;

            return CreateResult(
                ElementRole.Dropdown,
                !string.IsNullOrEmpty(value) ? $"{label}: {value}" : label,
                "dropdown",
                true, true);
        }

        private static ClassificationResult TryClassifyAsToggle(GameObject obj, string objName, string text)
        {
            var toggle = obj.GetComponent<Toggle>();
            if (toggle == null)
                return null;

            string effectiveName = GetEffectiveToggleName(obj, objName);
            return CreateResult(
                ElementRole.Toggle,
                GetCleanLabel(text, effectiveName),
                toggle.isOn ? "checkbox, checked" : "checkbox, unchecked",
                true, true);
        }

        private static ClassificationResult TryClassifyAsSlider(GameObject obj, string objName)
        {
            var slider = obj.GetComponent<Slider>();
            if (slider == null)
                return null;

            int percent = CalculateSliderPercent(slider);
            return new ClassificationResult
            {
                Role = ElementRole.Slider,
                Label = GetSliderLabel(obj, objName),
                RoleLabel = $"slider, {percent} percent, use left and right arrows",
                IsNavigable = true,
                ShouldAnnounce = true,
                HasArrowNavigation = true,
                SliderComponent = slider
            };
        }

        private static ClassificationResult TryClassifyAsDropdown(GameObject obj, string text)
        {
            var tmpDropdown = obj.GetComponent<TMP_Dropdown>();
            var unityDropdown = obj.GetComponent<Dropdown>();
            var customDropdown = GetCustomDropdownComponent(obj);

            if (tmpDropdown == null && unityDropdown == null && customDropdown == null)
                return null;

            // Get the current selected value
            string selectedValue = GetDropdownSelectedValue(tmpDropdown, unityDropdown, customDropdown);

            // Check if inside a Settings dropdown control
            string settingLabel = GetSettingsDropdownLabel(obj.transform);
            string label;
            if (!string.IsNullOrEmpty(settingLabel))
            {
                label = !string.IsNullOrEmpty(selectedValue) ? $"{settingLabel}: {selectedValue}" : settingLabel;
            }
            else
            {
                label = !string.IsNullOrEmpty(selectedValue) ? selectedValue : text;
            }

            return CreateResult(ElementRole.Dropdown, label, "dropdown", true, true);
        }

        private static ClassificationResult TryClassifyAsTextField(GameObject obj, string text)
        {
            if (obj.GetComponent<TMP_InputField>() == null && obj.GetComponent<InputField>() == null)
                return null;

            return CreateResult(ElementRole.TextField, text, "text field", true, true);
        }

        private static ClassificationResult TryClassifyAsScrollbar(GameObject obj, string text)
        {
            if (obj.GetComponent<Scrollbar>() == null)
                return null;

            return CreateResult(ElementRole.Scrollbar, text, "scrollbar", false, false);
        }

        private static ClassificationResult TryClassifyAsProgressIndicator(GameObject obj, string objName, string text)
        {
            if (!IsProgressIndicator(obj, objName, text))
                return null;

            return CreateResult(ElementRole.ProgressBar, text, "progress", false, false);
        }

        private static ClassificationResult TryClassifyAsNavigationArrow(GameObject obj, string objName, string text)
        {
            if (!IsNavigationArrow(obj, objName, text))
                return null;

            return CreateResult(ElementRole.Navigation, GetNavigationLabel(objName), "navigation", true, true);
        }

        private static ClassificationResult TryClassifyAsClickable(GameObject obj, string objName, string text)
        {
            bool hasCustomButton = HasCustomButton(obj);
            bool hasButton = obj.GetComponent<Button>() != null;
            bool hasEventTrigger = obj.GetComponent<UnityEngine.EventSystems.EventTrigger>() != null;

            if (!hasCustomButton && !hasButton && !hasEventTrigger)
                return null;

            // Check for carousel (has nav controls as children)
            if (IsCarouselElement(obj, out GameObject prevControl, out GameObject nextControl))
            {
                return new ClassificationResult
                {
                    Role = ElementRole.Button,
                    Label = GetCleanLabel(text, objName),
                    RoleLabel = "carousel, use left and right arrows",
                    IsNavigable = true,
                    ShouldAnnounce = true,
                    HasArrowNavigation = true,
                    PreviousControl = prevControl,
                    NextControl = nextControl
                };
            }

            // Determine if it's a link or button
            string effectiveName = GetEffectiveButtonName(obj, objName);
            bool isLink = IsLinkElement(objName, text);

            return CreateResult(
                isLink ? ElementRole.Link : ElementRole.Button,
                GetCleanLabel(text, effectiveName),
                isLink ? "link" : "button",
                true, true);
        }

        private static ClassificationResult TryClassifyAsLabel(GameObject obj, string objName, string text)
        {
            if (!IsLabelElement(obj, objName))
                return null;

            return CreateResult(ElementRole.Label, text, "", false, !string.IsNullOrEmpty(text));
        }

        /// <summary>
        /// Helper to create a simple ClassificationResult.
        /// </summary>
        private static ClassificationResult CreateResult(ElementRole role, string label, string roleLabel, bool navigable, bool announce)
        {
            return new ClassificationResult
            {
                Role = role,
                Label = label,
                RoleLabel = roleLabel,
                IsNavigable = navigable,
                ShouldAnnounce = announce
            };
        }

        /// <summary>
        /// Gets the selected value from any dropdown type.
        /// </summary>
        private static string GetDropdownSelectedValue(TMP_Dropdown tmpDropdown, Dropdown unityDropdown, Component customDropdown)
        {
            if (tmpDropdown != null && tmpDropdown.options != null && tmpDropdown.options.Count > tmpDropdown.value)
                return tmpDropdown.options[tmpDropdown.value].text;

            if (unityDropdown != null && unityDropdown.options != null && unityDropdown.options.Count > unityDropdown.value)
                return unityDropdown.options[unityDropdown.value].text;

            if (customDropdown != null)
                return GetCustomDropdownSelectedValue(customDropdown);

            return null;
        }

        #endregion

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
                // MTGA uses alpha < MinVisibleAlpha for hidden elements (see docs/MENU_NAVIGATION.md)
                if (canvasGroup.alpha < MinVisibleAlpha)
                {
                    if (debugLog) MelonLoader.MelonLogger.Msg($"[UIClassifier] {obj.name} hidden: own CanvasGroup alpha={canvasGroup.alpha}");
                    return false;
                }
                // For interactable check, skip if this is a MainButton (action buttons like Submit Deck)
                // These may have interactable=false temporarily but should still be visible for accessibility
                // Also skip for elements with meaningful text content (not just icon buttons)
                // FriendsWidget elements use non-standard UI where text comes from parent/sibling, so check GetText() too
                bool isMainButton = HasMainButtonComponent(obj);
                bool hasMeaningfulContent = UITextExtractor.HasActualText(obj)
                    || (IsInsideFriendsWidget(obj) && !string.IsNullOrEmpty(UITextExtractor.GetText(obj)));
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
                        if (parentCG.alpha < MinVisibleAlpha)
                        {
                            if (debugLog) MelonLoader.MelonLogger.Msg($"[UIClassifier] {obj.name} hidden: parent {parent.name} CanvasGroup alpha={parentCG.alpha}");
                            return false;
                        }
                        if (!parentCG.interactable && !parentCG.ignoreParentGroups)
                        {
                            // Allow FriendsWidget elements through - they use non-standard interactable patterns
                            // where parent CanvasGroups may be non-interactable but children should still be navigable
                            bool isFriendsElement = IsInsideFriendsWidget(obj)
                                && !string.IsNullOrEmpty(UITextExtractor.GetText(obj));
                            if (!isFriendsElement)
                            {
                                if (debugLog) MelonLoader.MelonLogger.Msg($"[UIClassifier] {obj.name} hidden: parent {parent.name} CanvasGroup interactable=false");
                                return false;
                            }
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
            int levels = 0;

            while (current != null && levels < MaxFriendsWidgetSearchDepth)
            {
                string name = current.name;
                if (ContainsIgnoreCase(name, "FriendsWidget"))
                    return true;
                current = current.parent;
                levels++;
            }

            return false;
        }

        /// <summary>
        /// Check if element is inside a VS_screen (NPE/pre-game screen).
        /// These contain decorative elements like NPC portraits that shouldn't be navigable.
        /// </summary>
        private static bool IsInsideVSScreen(GameObject obj)
        {
            if (obj == null) return false;

            Transform current = obj.transform;
            int levels = 0;

            while (current != null && levels < MaxParentSearchDepth)
            {
                string name = current.name;
                if (ContainsIgnoreCase(name, "VS_screen"))
                    return true;
                current = current.parent;
                levels++;
            }

            return false;
        }

        // Maximum size for small decorative buttons (pixels)
        private const int MaxSmallButtonSize = 80;

        /// <summary>
        /// Check if element is a small image-only button (decorative icon, NPC portrait).
        /// These have: CustomButton, no actual text, has image, small size.
        /// </summary>
        private static bool IsSmallImageOnlyButton(GameObject obj)
        {
            // Must have CustomButton
            if (!HasCustomButton(obj))
                return false;

            // Must have no actual text content
            if (UITextExtractor.HasActualText(obj))
                return false;

            // Must have an Image component (it's an icon/portrait)
            if (obj.GetComponent<Image>() == null && obj.GetComponent<RawImage>() == null)
                return false;

            // Must be small (both dimensions under threshold)
            var rectTransform = obj.GetComponent<RectTransform>();
            if (rectTransform == null)
                return false;

            Vector2 size = rectTransform.sizeDelta;
            if (size.x > MaxSmallButtonSize || size.y > MaxSmallButtonSize)
                return false;

            // All conditions met - this is a small decorative image button
            return true;
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
            // FriendsWidget elements use non-standard UI where text comes from parent/sibling, so check GetText() too
            if (HasCustomButton(obj) && !IsCustomButtonInteractable(obj))
            {
                bool isMainButton = HasMainButtonComponent(obj);
                bool hasMeaningfulContent = UITextExtractor.HasActualText(obj)
                    || (IsInsideFriendsWidget(obj) && !string.IsNullOrEmpty(UITextExtractor.GetText(obj)));
                if (!isMainButton && !hasMeaningfulContent)
                    return true;
            }

            // Check if CanvasGroup says it's invisible or non-interactable
            if (!IsVisibleViaCanvasGroup(obj))
                return true;

            // Filter container elements with 0x0 size - they're wrapper objects, not real buttons
            // NOTE: May need to revert if we find clickable containers (see KNOWN_ISSUES.md)
            if (ContainsIgnoreCase(obj.name, "container"))
            {
                var rectTransform = obj.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    Vector2 size = rectTransform.sizeDelta;
                    if (size.x <= 0 && size.y <= 0)
                        return true;
                }
            }

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

            // Must have zero or very small size (< MinDecorativeSize pixels in both dimensions)
            var rectTransform = obj.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Vector2 size = rectTransform.sizeDelta;
                if (size.x > MinDecorativeSize || size.y > MinDecorativeSize)
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
            // Check simple exact name matches first (fast HashSet lookup)
            if (FilteredExactNames.Contains(name))
                return true;

            // Check simple Contains patterns
            foreach (var pattern in FilteredContainsPatterns)
            {
                if (ContainsIgnoreCase(name, pattern))
                    return true;
            }

            // Conditional filters that require additional checks:

            // Fade overlays (but not nav fades)
            if (ContainsIgnoreCase(name, "fade") && !ContainsIgnoreCase(name, "nav"))
                return true;

            // Null-prefixed elements are placeholders (NullClaimButton, NullText_*, etc.)
            if (name.StartsWith("Null", System.StringComparison.OrdinalIgnoreCase))
                return true;

            // Dismiss buttons (internal UI close buttons)
            // BUT: Allow dismiss inside FriendsWidget (Button_TopBarDismiss is the panel header)
            if (ContainsIgnoreCase(name, "dismiss") && !IsInsideFriendsWidget(obj))
                return true;

            // Social corner icon - filter it entirely (F4 opens Friends panel directly)
            if (ContainsIgnoreCase(name, "socialcorner") || ContainsIgnoreCase(name, "social corner"))
                return true;

            // VS_screen elements (NPE/pre-game decorative elements like NPC portraits)
            if (IsInsideVSScreen(obj))
                return true;

            // Small image-only buttons without text (decorative icons, NPC portraits)
            // BUT: Allow inside FriendsWidget (Backer_Hitbox elements are clickable friend items)
            if (IsSmallImageOnlyButton(obj) && !IsInsideFriendsWidget(obj))
                return true;

            // Hitboxes without actual text content
            // BUT: Allow hitboxes inside FriendsWidget (they ARE the clickable friend items)
            if (ContainsIgnoreCase(name, "hitbox") && !UITextExtractor.HasActualText(obj) && !IsInsideFriendsWidget(obj))
                return true;

            // Backer elements from social panel (internal hitboxes)
            // BUT: Allow backer elements inside FriendsWidget (they ARE the clickable friend items)
            if (ContainsIgnoreCase(name, "backer") && !UITextExtractor.HasActualText(obj) && !IsInsideFriendsWidget(obj))
                return true;

            // "New" badge indicators (both "new" alone and "new...indicator" patterns)
            if (ContainsIgnoreCase(name, "new") && ContainsIgnoreCase(name, "indicator"))
                return true;

            // Scroll content containers
            if (EqualsIgnoreCase(name, "content") && obj.GetComponent<RectTransform>() != null)
                return true;

            // Gradient decorations (but not nav gradients which are handled separately)
            if (ContainsIgnoreCase(name, "gradient") && !ContainsIgnoreCase(name, "nav"))
                return true;

            // Navigation controls that are part of carousels - hide them, the parent handles arrow keys
            if (IsCarouselNavControl(obj, name))
                return true;

            // Background art/decorative elements (have CustomButton but are not interactive)
            if (name.StartsWith("Background", System.StringComparison.OrdinalIgnoreCase) && !UITextExtractor.HasActualText(obj))
                return true;

            // EventTriggers that contain TMP_InputField children are just containers
            if (obj.GetComponent<UnityEngine.EventSystems.EventTrigger>() != null &&
                obj.GetComponentInChildren<TMPro.TMP_InputField>() != null &&
                obj.GetComponent<TMPro.TMP_InputField>() == null)
                return true;

            // Back buttons (icon buttons for navigation) - handled via Backspace, not navigation list
            // BUT: Don't filter "backer" elements (FriendsWidget uses Backer_Hitbox for clickable items)
            if (ContainsIgnoreCase(name, "back") &&
                !ContainsIgnoreCase(name, "backer") &&
                (HasCustomButton(obj) || obj.GetComponent<Button>() != null) &&
                !UITextExtractor.HasActualText(obj))
                return true;

            // Stop buttons with prefix (e.g., "Stop Second Strike")
            if (name.StartsWith("Stop ", System.StringComparison.OrdinalIgnoreCase))
                return true;

            // Scene-specific filters for MatchEndScene (victory/defeat screen)
            if (IsMatchEndScene())
            {
                // Duel prompt buttons (leftover from duel)
                if (IsDuelPromptElement(obj, name))
                    return true;

                // Navigation arrows (leftover from duel)
                if (IsNavigationArrow(obj, name, null))
                    return true;
            }

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
        /// Check if this element is a stepper navigation control (Increment/Decrement button).
        /// These should be hidden from tab navigation - the parent stepper control handles arrow keys.
        /// </summary>
        private static bool IsStepperNavControl(GameObject obj, string name)
        {
            // Check if this is an Increment or Decrement button
            bool isIncrement = ContainsIgnoreCase(name, "increment");
            bool isDecrement = ContainsIgnoreCase(name, "decrement");

            if (!isIncrement && !isDecrement)
                return false;

            // Must have a Button component
            if (obj.GetComponent<Button>() == null)
                return false;

            // Verify it's inside a "Control - " parent (stepper control structure)
            Transform parent = obj.transform.parent;
            int levels = 0;
            while (parent != null && levels < MaxParentSearchDepth)
            {
                string parentName = parent.name;
                if (parentName.StartsWith("Control - ", System.StringComparison.OrdinalIgnoreCase) ||
                    parentName.StartsWith("Control_", System.StringComparison.OrdinalIgnoreCase))
                {
                    // This is a stepper nav control inside a Control parent
                    return true;
                }
                parent = parent.parent;
                levels++;
            }

            return false;
        }

        /// <summary>
        /// Check if this is a Settings stepper control parent (Control - Setting: X or Control - X_Selector).
        /// Returns true if this is a stepper control that should be navigable with arrow keys.
        /// </summary>
        private static bool IsSettingsStepperControl(GameObject obj, string name, out string label, out string currentValue, out GameObject incrementControl, out GameObject decrementControl)
        {
            label = null;
            currentValue = null;
            incrementControl = null;
            decrementControl = null;

            // Check if this matches "Control - Setting: X" or "Control - X_Selector" pattern
            if (!name.StartsWith("Control - ", System.StringComparison.OrdinalIgnoreCase) &&
                !name.StartsWith("Control_", System.StringComparison.OrdinalIgnoreCase))
                return false;

            // Skip dropdown controls - those are handled separately
            if (name.EndsWith("_Dropdown", System.StringComparison.OrdinalIgnoreCase))
                return false;

            // Search for Increment and Decrement buttons in children
            foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
            {
                if (child == obj.transform) continue;
                if (!child.gameObject.activeInHierarchy) continue;

                string childName = child.name;
                var button = child.GetComponent<Button>();

                if (button != null)
                {
                    if (ContainsIgnoreCase(childName, "increment") && incrementControl == null)
                    {
                        incrementControl = child.gameObject;
                    }
                    else if (ContainsIgnoreCase(childName, "decrement") && decrementControl == null)
                    {
                        decrementControl = child.gameObject;
                    }
                }

                if (incrementControl != null && decrementControl != null)
                    break;
            }

            // Must have at least one stepper button to be considered a stepper
            if (incrementControl == null && decrementControl == null)
                return false;

            // Extract setting name from control name
            // Pattern: "Control - Setting: SettingName" or "Control - SettingName_Selector"
            label = name;
            if (label.StartsWith("Control - Setting: ", System.StringComparison.OrdinalIgnoreCase))
                label = label.Substring(19);
            else if (label.StartsWith("Control - ", System.StringComparison.OrdinalIgnoreCase))
                label = label.Substring(10);
            else if (label.StartsWith("Control_", System.StringComparison.OrdinalIgnoreCase))
                label = label.Substring(8);

            // Remove suffix like "_Selector", "_Toggle"
            int underscoreIdx = label.LastIndexOf('_');
            if (underscoreIdx > 0)
                label = label.Substring(0, underscoreIdx);

            // Clean up the name
            label = CleanSettingLabel(label);

            // Find the current value from "Value" child
            currentValue = FindValueInControl(obj.transform, label);

            return true;
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
            // Walk up to find "Control - X_Dropdown" parent
            Transform current = transform;
            int levels = 0;
            while (current != null && levels < MaxDropdownSearchDepth)
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

                    // Clean up the name
                    label = CleanSettingLabel(label);

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

            // Walk up to find "Control - X_Dropdown" parent
            int levels = 0;
            while (controlTransform != null && levels < MaxDropdownSearchDepth)
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

            // Clean up the name
            label = CleanSettingLabel(label);

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

        // Generic element names that should check parent for better label
        private static readonly HashSet<string> GenericElementNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "Button", "Btn", "CustomButton", "MainButton", "Toggle", "Checkbox"
        };

        // Parent name prefixes to strip when extracting labels (e.g., "Toggle - Remember Me" -> "Remember Me")
        private static readonly string[] ParentLabelPrefixes = new[]
        {
            "Toggle - ",    // 9 chars
            "Checkbox - "   // 11 chars
        };

        /// <summary>
        /// Get an effective name for an element, checking parent if name is generic.
        /// For elements named just "Toggle", "Button", etc., the parent often has a descriptive name.
        /// Also handles parent patterns like "Toggle - Remember Me" by extracting the label part.
        /// </summary>
        private static string GetEffectiveElementName(GameObject obj, string objName)
        {
            // Clean the name for comparison (remove Unity clone suffix)
            string cleanName = objName.Replace("(Clone)", "").Trim();

            // If the name is descriptive (not generic), use it
            if (!GenericElementNames.Contains(cleanName))
            {
                return objName;
            }

            // Name is generic, check parent for a better name
            var parent = obj.transform.parent;
            if (parent == null)
            {
                return objName;
            }

            string parentName = parent.name;

            // Check for "Prefix - Label" patterns and extract the label
            foreach (var prefix in ParentLabelPrefixes)
            {
                if (parentName.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    return parentName.Substring(prefix.Length);
                }
            }

            // If parent has a meaningful name (not also generic), use it
            string cleanParentName = parentName.Replace("(Clone)", "").Trim();
            if (!string.IsNullOrEmpty(cleanParentName) &&
                !GenericElementNames.Contains(cleanParentName) &&
                cleanParentName.Length > 3)
            {
                return parentName;
            }

            // Fall back to original name
            return objName;
        }

        // Wrapper for toggle-specific calls (maintains API compatibility)
        private static string GetEffectiveToggleName(GameObject obj, string objName)
            => GetEffectiveElementName(obj, objName);

        // Wrapper for button-specific calls (maintains API compatibility)
        private static string GetEffectiveButtonName(GameObject obj, string objName)
            => GetEffectiveElementName(obj, objName);

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
                // Also reject generic button names - these are fallbacks from object name, not actual content
                if (!string.IsNullOrEmpty(text) &&
                    text != "\u200B" &&
                    !GenericElementNames.Contains(text))
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
            name = SplitCamelCase(name);
            name = ResolutionPattern.Replace(name, " ");  // Remove resolution suffixes
            name = WhitespacePattern.Replace(name, " ");
            name = name.Replace("Nav ", "");
            name = name.Replace("Button", "");
            name = name.Replace("Btn", "");

            return name.Trim();
        }

        /// <summary>
        /// Find the label for a slider from parent/sibling elements.
        /// MTGA sliders typically have a "Label" sibling or a parent "Control - X" container.
        /// </summary>
        private static string GetSliderLabel(GameObject sliderObj, string fallbackName)
        {
            var sliderTransform = sliderObj.transform;

            // First, check for a parent "Control - " container (Settings pattern)
            Transform parent = sliderTransform.parent;
            int levels = 0;
            while (parent != null && levels < MaxParentSearchDepth)
            {
                string parentName = parent.name;
                if (parentName.StartsWith("Control - ", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Extract label from parent name: "Control - MasterVolume" -> "Master Volume"
                    string label = parentName.Substring(10); // Remove "Control - "

                    // Remove common suffixes
                    if (label.EndsWith("_Slider", System.StringComparison.OrdinalIgnoreCase))
                        label = label.Substring(0, label.Length - 7);

                    // Clean up the name
                    label = CleanSettingLabel(label);

                    if (!string.IsNullOrEmpty(label))
                        return label;
                }
                parent = parent.parent;
                levels++;
            }

            // Second, look for a "Label" sibling or child in the parent container
            parent = sliderTransform.parent;
            if (parent != null)
            {
                foreach (Transform sibling in parent)
                {
                    if (sibling == sliderTransform) continue;

                    string siblingName = sibling.name;

                    // Look for elements named "Label", "Text", "Title", or similar
                    if (ContainsIgnoreCase(siblingName, "label") ||
                        EqualsIgnoreCase(siblingName, "Text") ||
                        EqualsIgnoreCase(siblingName, "Title"))
                    {
                        var tmpText = sibling.GetComponent<TMP_Text>();
                        if (tmpText != null && !string.IsNullOrEmpty(tmpText.text))
                        {
                            string labelText = tmpText.text.Trim();
                            // Make sure it's not the percentage value
                            if (!labelText.Contains("%") && labelText.Length < 50)
                                return labelText;
                        }

                        var legacyText = sibling.GetComponent<Text>();
                        if (legacyText != null && !string.IsNullOrEmpty(legacyText.text))
                        {
                            string labelText = legacyText.text.Trim();
                            if (!labelText.Contains("%") && labelText.Length < 50)
                                return labelText;
                        }
                    }
                }

                // Third, search deeper in the parent's hierarchy for any label
                foreach (var tmpText in parent.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (tmpText == null || tmpText.transform == sliderTransform) continue;

                    string textObjName = tmpText.gameObject.name;
                    if (ContainsIgnoreCase(textObjName, "label") ||
                        ContainsIgnoreCase(textObjName, "title"))
                    {
                        string labelText = tmpText.text?.Trim();
                        if (!string.IsNullOrEmpty(labelText) && !labelText.Contains("%") && labelText.Length < 50)
                            return labelText;
                    }
                }
            }

            // Fallback: try to extract from object name
            string cleanName = CleanObjectName(fallbackName);

            // Remove "slider" from the name if present
            cleanName = SliderSuffixPattern.Replace(cleanName, "").Trim();

            if (!string.IsNullOrEmpty(cleanName) && cleanName.Length > 1)
                return cleanName;

            return "Volume";  // Ultimate fallback for audio sliders
        }

        private static int CalculateSliderPercent(Slider slider)
        {
            float range = slider.maxValue - slider.minValue;
            if (range <= 0) return 0;
            return Mathf.RoundToInt((slider.value - slider.minValue) / range * 100);
        }

        /// <summary>
        /// Get game's custom cTMP_Dropdown component if present.
        /// This is used in Login/Registration screens for dropdowns like birthdate, country, etc.
        /// </summary>
        private static Component GetCustomDropdownComponent(GameObject obj)
        {
            foreach (var component in obj.GetComponents<Component>())
            {
                if (component == null) continue;
                string typeName = component.GetType().Name;
                if (typeName == "cTMP_Dropdown")
                {
                    return component;
                }
            }
            return null;
        }

        /// <summary>
        /// Get the selected value from a cTMP_Dropdown via reflection.
        /// cTMP_Dropdown inherits from TMP_Dropdown so it has similar properties.
        /// </summary>
        private static string GetCustomDropdownSelectedValue(Component dropdown)
        {
            if (dropdown == null) return null;

            try
            {
                var type = dropdown.GetType();

                // cTMP_Dropdown inherits from TMP_Dropdown, so it has 'value' and 'options' properties
                var valueProperty = type.GetProperty("value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var optionsProperty = type.GetProperty("options", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (valueProperty == null || optionsProperty == null) return null;

                int selectedIndex = (int)valueProperty.GetValue(dropdown);
                var options = optionsProperty.GetValue(dropdown) as System.Collections.IList;

                if (options != null && selectedIndex >= 0 && selectedIndex < options.Count)
                {
                    var option = options[selectedIndex];
                    // Each option has a 'text' property
                    var textProperty = option.GetType().GetProperty("text");
                    if (textProperty != null)
                    {
                        return textProperty.GetValue(option) as string;
                    }
                }
            }
            catch
            {
                // Ignore reflection errors
            }

            return null;
        }

        #endregion
    }
}
