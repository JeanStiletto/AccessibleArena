# UIElementClassifier.cs

## Overview
Classifies UI elements by their role and determines navigability.
Used by navigators to properly label elements for screen readers.

## Class: UIElementClassifier (static) (line 16)

### Constants
- private const float MinVisibleAlpha (line 21)
- private const int MinDecorativeSize (line 22)
- private const int MaxParentSearchDepth (line 25)
- private const int MaxFriendsWidgetSearchDepth (line 26)
- private const int MaxDropdownSearchDepth (line 27)
- private const int MaxLabelLength (line 30)
- private static readonly string[] FilteredContainsPatterns (line 33)
- private static readonly HashSet<string> FilteredExactNames (line 40)

### Compiled Patterns
- private static readonly Regex ProgressFractionPattern (line 50)
- private static readonly Regex ProgressPercentPattern (line 51)
- private static readonly Regex HtmlTagPattern (line 52)
- private static readonly Regex WhitespacePattern (line 53)
- private static readonly Regex CamelCasePattern (line 54)
- private static readonly Regex SliderSuffixPattern (line 55)
- private static readonly Regex ResolutionPattern (line 56)
- private static readonly System.StringComparison IgnoreCase (line 59)

### Helper Methods
- private static bool ContainsIgnoreCase(string source, string value) (line 66)
- private static bool EqualsIgnoreCase(string source, string value) (line 74)
- private static string SplitCamelCase(string text) (line 83)
- private static string CleanSettingLabel(string label) (line 92)

### Enums
- public enum ElementRole (line 102)
  - Button, Link, Toggle, Slider, Stepper, Carousel, Dropdown, TextField, ProgressBar, Label, Navigation, Scrollbar, Card, TextBlock, Internal, Unknown

### Result Type
- public class ClassificationResult (line 125)
  - ElementRole Role
  - string Label
  - string RoleLabel
  - bool IsNavigable
  - bool ShouldAnnounce
  - bool HasArrowNavigation
  - GameObject PreviousControl
  - GameObject NextControl
  - Slider SliderComponent
  - bool UseHoverActivation

### Main Classification
- public static ClassificationResult Classify(GameObject obj) (line 164)
  - Uses chain of TryClassifyAs* methods

### Classification Methods
- private static ClassificationResult TryClassifyAsInternal(GameObject obj, string objName, string text) (line 193)
- private static ClassificationResult TryClassifyAsCard(GameObject obj) (line 201)
- private static ClassificationResult TryClassifyAsStepperControl(GameObject obj, string objName) (line 209)
- private static ClassificationResult TryClassifyAsStepperNavControl(GameObject obj, string objName) (line 245)
- private static ClassificationResult TryClassifyAsPopoutControl(GameObject obj, string objName) (line 257)
- private static ClassificationResult TryClassifyAsSettingsDropdown(GameObject obj, string objName) (line 299)
- private static ClassificationResult TryClassifyAsToggle(GameObject obj, string objName, string text) (line 311)
- private static ClassificationResult TryClassifyAsSlider(GameObject obj, string objName) (line 331)
- private static ClassificationResult TryClassifyAsDropdown(GameObject obj, string text) (line 350)
- private static ClassificationResult TryClassifyAsTextField(GameObject obj, string text) (line 377)
- private static ClassificationResult TryClassifyAsScrollbar(GameObject obj, string text) (line 385)
- private static ClassificationResult TryClassifyAsProgressIndicator(GameObject obj, string objName, string text) (line 393)
- private static ClassificationResult TryClassifyAsNavigationArrow(GameObject obj, string objName, string text) (line 401)
- private static ClassificationResult TryClassifyAsClickable(GameObject obj, string objName, string text) (line 409)
- private static ClassificationResult TryClassifyAsLabel(GameObject obj, string objName, string text) (line 445)
- private static ClassificationResult CreateResult(ElementRole role, string label, string roleLabel, bool navigable, bool announce) (line 456)
- private static string GetDropdownSelectedValue(TMP_Dropdown tmpDropdown, Dropdown unityDropdown, Component customDropdown) (line 471)
- private static void CorrectStaleDropdownValue(TMP_Dropdown dropdown) (line 504)

### CustomButton Utilities
- public static bool HasCustomButton(GameObject obj) (line 538)
- public static MonoBehaviour GetCustomButton(GameObject obj) (line 546)
- private static bool IsCustomButtonType(string typeName) (line 555)
- public static bool HasMainButtonComponent(GameObject obj) (line 563)
- public static bool IsCustomButtonInteractable(GameObject obj) (line 572)

### Visibility
- public static bool IsVisibleViaCanvasGroup(GameObject obj, bool debugLog = false) (line 618)

### Public Utilities
- public static string GetAnnouncement(GameObject obj) (line 693)

### Detection Helpers
- private static bool IsInsideFriendsWidget(GameObject obj) (line 712)
- private static bool IsInsideBoosterCarousel(GameObject obj) (line 735)
- private static bool IsInsideEventTile(GameObject obj) (line 758)
- private static bool IsInsidePreferredPrintingTag(GameObject obj) (line 781)
- private static bool IsInsideVSScreen(GameObject obj) (line 804)
- private static bool IsInsideNavBarRightSide(GameObject obj) (line 827)
- private static bool IsInsideBladeListItem(GameObject obj) (line 850)
- private static bool IsSmallImageOnlyButton(GameObject obj) (line 876)
- private static bool IsInternalElement(GameObject obj, string name, string text) (line 903)
- private static bool IsHiddenByGameProperties(GameObject obj) (line 940)
- private static bool IsDecorativeGraphicalElement(GameObject obj) (line 1009)
- private static bool IsFilteredByNamePattern(GameObject obj, string name) (line 1039)
- private static bool IsMatchEndScene() (line 1172)
- private static bool IsDuelPromptElement(GameObject obj, string name) (line 1187)
- private static bool IsFilteredByTextContent(GameObject obj, string name, string text) (line 1222)
- private static bool IsNumericOnly(string text) (line 1249)
- private static bool IsCarouselNavControl(GameObject obj, string name) (line 1263)
- private static bool IsStepperNavControl(GameObject obj, string name) (line 1290)
- private static bool IsSettingsStepperControl(GameObject obj, string name, out string label, out string currentValue, out GameObject incrementControl, out GameObject decrementControl) (line 1326)
- public static bool IsCarouselElement(GameObject obj, out GameObject previousControl, out GameObject nextControl) (line 1398)
- private static string GetSettingsDropdownLabel(Transform transform) (line 1446)
- private static bool IsSettingsDropdownControl(GameObject obj, string name, out string label, out string currentValue) (line 1479)
- private static string FindValueInControl(Transform controlParent, string settingName) (line 1555)
- private static bool IsProgressIndicator(GameObject obj, string name, string text) (line 1595)
- private static bool IsNavigationArrow(GameObject obj, string name, string text) (line 1617)
- private static bool IsLinkElement(string name, string text) (line 1627)
- private static bool IsLabelElement(GameObject obj, string name) (line 1641)
- private static string GetNavigationLabel(string name) (line 1659)

### Label Extraction
- private static readonly HashSet<string> GenericElementNames (line 1669)
- private static readonly string[] ParentLabelPrefixes (line 1675)
- private static string GetEffectiveElementName(GameObject obj, string objName) (line 1686)
- private static string GetEffectiveToggleName(GameObject obj, string objName) (line 1729)
- private static string GetEffectiveButtonName(GameObject obj, string objName) (line 1733)
- private static string GetCleanLabel(string text, string objName) (line 1736)
- private static string CleanObjectName(string name) (line 1765)
- private static string GetSliderLabel(GameObject sliderObj, string fallbackName) (line 1805)
- private static int CalculateSliderPercent(Slider slider) (line 1896)

### Custom Dropdown
- private static Component GetCustomDropdownComponent(GameObject obj) (line 1907)
- private static string GetCustomDropdownSelectedValue(Component dropdown) (line 1925)
