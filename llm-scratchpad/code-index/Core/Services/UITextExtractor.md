# UITextExtractor.cs
Path: src/Core/Services/UITextExtractor.cs
Lines: 2760

## Top-level comments
- Utility class to extract readable text from Unity UI GameObjects; checks various UI components in priority order to find the best text representation.

## public static class UITextExtractor (line 17)
### Fields
- private static readonly Dictionary<string, System.Func<string>> FallbackLabels (line 24) — Note: locale-resolving funcs, checked before CleanObjectName fallback
- private static readonly Regex RichTextTagPattern (line 34)
- private static readonly Regex WhitespacePattern (line 35)
- private static Type _localizeType (line 317)
- private static FieldInfo _textTargetField (line 318)
- private static FieldInfo _serializedCmpField (line 319)
- private static FieldInfo _locKeyField (line 320)
- private static bool _localizeReflectionResolved (line 321)
- private static FieldInfo _activeLocProviderField (line 790)
- private static MethodInfo _getLocalizedTextMethod (line 791)
- private static bool _locReflectionInitialized (line 792)
- private static Type _npeObjectiveType (line 1402)
- private static FieldInfo _npeCircleTextField (line 1403)
- private static FieldInfo _npeAnimatorField (line 1404)
- private static bool _npeFieldsCached (line 1405)
- private static MethodInfo _animGetStateInfo (line 1483)
- private static MethodInfo _stateInfoIsName (line 1484)

### Methods
- public static string StripRichText(string text) (line 40)
- public static bool HasActualText(GameObject gameObject) (line 50)
- public static string GetText(GameObject gameObject) (line 90) — Note: large dispatcher that tries many specialized extractors in priority order
- private static string GetLabelOverride(string objectName) (line 295) — Note: hardcoded overrides for buttons with misleading game labels
- private static string TryGetLocalizeText(GameObject gameObject) (line 330) — Note: reflects into Wotc.Mtga.Loc.Localize; caches Type/Field info on first call
- public static string ResolveLocKey(string locKey) (line 395) — Note: returns null when resolved text equals the key (game's "not found" behavior)
- private static string TryGetCurrencyLabel(GameObject gameObject) (line 426)
- private static string GetWildcardTooltipText(GameObject gameObject) (line 476) — Note: strips <style> rich-text tags and joins lines with ", "
- private static string TryGetNavTokenLabel(GameObject gameObject) (line 519)
- private static string TryGetDeckName(GameObject gameObject) (line 559)
- private static string TryGetBoosterPackName(GameObject gameObject) (line 628)
- private static void EnsureLocReflectionCached() (line 794)
- private static string GetLocalizedSetName(string setCode) (line 822)
- public static string MapSetCodeToName(string setCode) (line 832)
- private static string TryGetPlayModeTabText(GameObject gameObject) (line 848)
- private static string TryGetEventTileLabel(GameObject gameObject) (line 912)
- private static string TryGetPacketLabel(GameObject gameObject) (line 938)
- private static string TryGetDeckManagerButtonText(GameObject gameObject) (line 971)
- private static string TryGetObjectiveText(GameObject gameObject) (line 1081)
- private static string TryGetWildcardProgressText(GameObject gameObject, string parentName) (line 1326)
- private static string TryGetNPEObjectiveText(GameObject gameObject) (line 1407)
- private static string GetNPEObjectiveStatus(object animator) (line 1486) — Note: tries animator state names first, then falls back to GetBool parameters
- private static string TryGetSiblingLabel(GameObject gameObject) (line 1552)
- private static string TryGetFriendsWidgetLabel(GameObject gameObject) (line 1607)
- private static string TryGetMailboxItemTitle(GameObject gameObject) (line 1678) — Note: prepends unread badge ("Neu"/"New") to the title when present
- private static string TryGetTooltipText(GameObject gameObject) (line 1747)
- private static string TryGetTooltipTextFromObject(GameObject gameObject) (line 1767)
- private static string TryGetObjectiveBubblePopupText(GameObject gameObject, string fieldName) (line 1795) — Note: logs via MelonLogger on reflection exception
- private static string GetParentPath(GameObject gameObject) (line 1850)
- public static string GetElementType(GameObject gameObject) (line 1867)
- public static string GetInputFieldLabel(GameObject inputField) (line 1906)
- public static string TryGetStoreItemLabel(GameObject gameObject) (line 1956) — Note: name begins with "Try" but is public; walks up 3 parent levels to find StoreItemBase
- private static bool IsPriceText(string text) (line 2062)
- public static string GetButtonText(GameObject buttonObj, string fallback = null) (line 2078)
- private static string TryGetInputFieldLabel(GameObject inputFieldObj) (line 2116)
- private static string GetInputFieldText(TMP_InputField inputField) (line 2168) — Note: masks text for password InputType
- private static string GetInputFieldText(InputField inputField) (line 2227) — Note: masks text for password InputType
- private static string GetToggleText(Toggle toggle) (line 2256) — Note: remaps "POSITION" placeholder to Bo3Toggle string
- private static string GetDropdownText(TMP_Dropdown d) (line 2307)
- private static string GetDropdownText(Dropdown d) (line 2312)
- private static string FormatDropdownText(int value, int optionCount, string optionText, string captionText, string objectName) (line 2317)
- private static string GetScrollbarText(Scrollbar scrollbar) (line 2335)
- private static string GetSliderText(Slider slider) (line 2358)
- public static string CleanText(string text) (line 2389)
- public static string GetPopupBodyText(GameObject popupGameObject) (line 2412) — Note: filters out common button-label strings in several languages
- public static MailContentParts GetMailContentParts() (line 2523)
- public static string GetMailContentText() (line 2624)
- private static Transform FindChildRecursive(Transform parent, string name) (line 2704) — Note: matches by Contains, not exact name
- private static bool IsInsideButtonContainer(Transform transform) (line 2721)
- private static string CleanObjectName(string name) (line 2734) — Note: returns lowercase result

## public struct MailContentParts (line 2509) — nested in UITextExtractor
### Fields
- public string Title (line 2511)
- public string Date (line 2512)
- public string Body (line 2513)
- public GameObject TitleObject (line 2514)
- public GameObject DateObject (line 2515)
- public GameObject BodyObject (line 2516)
### Properties
- public bool HasContent { get; } (line 2517)
