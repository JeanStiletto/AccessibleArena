# UITextExtractor.cs

## Overview
Utility class to extract readable text from Unity UI GameObjects.
Checks various UI components in priority order to find the best text representation.

## Class: UITextExtractor (static) (line 12)

### Fallback Labels
- private static readonly Dictionary<string, System.Func<string>> FallbackLabels (line 19)
  - Centralized fallback labels for buttons with no text/tooltips
  - Uses Func<string> for runtime locale resolution

### Main Methods
- public static bool HasActualText(GameObject gameObject) (line 32)
  - Checks if GameObject has real text content vs object name fallback
- public static string GetText(GameObject gameObject) (line 104)
  - Main entry point for text extraction
  - Priority order documented in method

### Component Extraction
- private static string GetTextFromInputField(GameObject gameObject) (line 296)
- private static string GetTextFromToggle(GameObject gameObject) (line 332)
- private static string GetTextFromDropdown(GameObject gameObject) (line 371)
- private static string GetTextFromSlider(GameObject gameObject) (line 429)
- private static string GetTextFromButton(GameObject gameObject) (line 464)

### Tooltip Support
- private static string GetTooltipText(GameObject gameObject) (line 504)
  - Reflection-based TooltipTrigger.TooltipData access
  - Note: TooltipData is a FIELD, not property

### TMPro Text Extraction
- private static string GetTextFromTMPComponents(GameObject gameObject) (line 545)

### Legacy Text Extraction
- private static string GetTextFromLegacyText(GameObject gameObject) (line 574)

### Child Text Aggregation
- private static string GetTextFromChildren(GameObject gameObject) (line 603)

### Fallback
- private static string GetFallbackLabel(GameObject gameObject) (line 644)

### Image Component Helpers
- private static bool HasImageComponent(GameObject gameObject) (line 668)

Note: File is large (2700+ lines). Contains extensive text extraction logic with multiple fallback paths, tooltip handling, TMP vs legacy UI support, and child text aggregation.
