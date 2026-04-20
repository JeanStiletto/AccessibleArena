# UITextExtractor.cs
Path: src/Core/Services/UITextExtractor/UITextExtractor.cs
Lines: 548

## Top-level comments
- File-level XML summary: "Utility class to extract readable text from Unity UI GameObjects. Checks various UI components in priority order to find the best text representation."
- Core partial. Feature partials (Localization, ContextLabels, Objectives, Widgets, Social) extend this class.

## public static partial class UITextExtractor (line 17)

### Fields
- private static readonly Dictionary<string, System.Func<string>> FallbackLabels (line 24, static) — centralized fallback labels for buttons with no text/tooltip; resolved at runtime via LocaleManager
- private static readonly Regex RichTextTagPattern (line 34, static)
- private static readonly Regex WhitespacePattern (line 35, static)

### Methods
- public static string StripRichText(string text) (line 40) — strips Unity rich text tags
- public static bool HasActualText(GameObject gameObject) (line 50) — distinguishes elements with real labels from those with only internal names
- public static string GetText(GameObject gameObject) (line 90) — main extraction pipeline; dispatches to feature-partial TryGet* helpers in priority order, then input/toggle/scrollbar/slider/dropdown/TMP/legacy/sibling/tooltip/friends/fallback/Localize
- private static string GetLabelOverride(string objectName) (line 295) — overrides misleading game labels (ExitMatchOverlayButton, NewDeckButton, TitlePanel_MainDeck, BackButton)
- private static string TryGetSiblingLabel(GameObject gameObject) (line 321) — reads sibling TMP/Text when self has no text (e.g., Color Challenge INFO sibling)
- private static string TryGetTooltipText(GameObject gameObject) (line 376) — walks up to 4 levels (self + 3 parents) reading TooltipTrigger.LocString
- private static string TryGetTooltipTextFromObject(GameObject gameObject) (line 396)
- private static string GetParentPath(GameObject gameObject) (line 419)
- public static string GetElementType(GameObject gameObject) (line 436) — returns "card"/"button"/"text field"/"checkbox"/"dropdown"/"slider"/"scrollbar"/"control"/"item"
- public static string CleanText(string text) (line 472) — removes rich text tags, zero-width spaces, normalizes whitespace
- private static Transform FindChildRecursive(Transform parent, string name) (line 492)
- private static bool IsInsideButtonContainer(Transform transform) (line 509)
- private static string CleanObjectName(string name) (line 522) — Unity prefix/suffix stripping, PascalCase splitting, lowercasing
