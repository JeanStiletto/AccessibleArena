# DeckInfoProvider.cs
Path: src/Core/Services/DeckInfoProvider.cs
Lines: 1083

## Top-level comments
- Reflection-based provider that reads deck statistics (card count, mana curve, average cost, type/subtype breakdown) from the game's DeckMainTitlePanel, DeckCostsDetails and DeckTypesDetails UI components. Calls SetDeck on hidden panels so the text fields are populated even when the popup is closed.

## private struct TypeGroup (line 88, nested in DeckInfoProvider)
### Fields
- public string TypeName (line 90)
- public string TypeQuantity (line 91)
- public List<(string name, string quantity)> Subtypes (line 92)

## public static class DeckInfoProvider (line 22)
### Fields
- private static MonoBehaviour _cachedTitlePanel (line 26)
- private static MonoBehaviour _cachedCostsDetails (line 27)
- private static MonoBehaviour _cachedTypesDetails (line 28)
- private static FieldInfo _cardCountLabelField (line 31)
- private static bool _titlePanelReflectionInit (line 32)
- private static FieldInfo[] _costBarFields (line 35)
- private static FieldInfo _costBarQuantityLabelField (line 36)
- private static FieldInfo _averageTextField (line 37)
- private static FieldInfo _creaturesItemField (line 38)
- private static FieldInfo _othersItemField (line 39)
- private static FieldInfo _landsItemField (line 40)
- private static FieldInfo _typeLineQuantityField (line 41)
- private static FieldInfo _typeLinePercentField (line 42)
- private static MethodInfo _setDeckMethod (line 43)
- private static bool _costsDetailsReflectionInit (line 44)
- private static MethodInfo _pantryGetModelProviderMethod (line 47)
- private static PropertyInfo _modelProperty (line 48)
- private static MethodInfo _getFilteredMainDeckMethod (line 49)
- private static bool _pantryReflectionInit (line 50)
- private static FieldInfo _typesItemParentField (line 53)
- private static MethodInfo _typesSetDeckMethod (line 54)
- private static Type _lineItemType (line 55)
- private static FieldInfo _lineItemNameField (line 56)
- private static FieldInfo _lineItemQuantityField (line 57)
- private static bool _typesDetailsReflectionInit (line 58)
- private static MethodInfo _pantryGetCardDatabaseMethod (line 61)
- private static PropertyInfo _greLocProviderProperty (line 62)
- private static bool _cardDatabaseReflectionInit (line 63)
- private static readonly string[] CostBarFieldNames (line 66)
- private static readonly string[] CostBarLabels (line 70)
- private static readonly string[] TypeLineFieldNames (line 76)
- private static readonly string[] TypeLineDisplayNames (line 80)
### Methods
- public static string GetCardCountText() (line 99)
- public static List<(string label, string text)> GetDeckInfoElements() (line 140) — Note: two-entry list: "Cards" row and "Mana Curve" row
- public static List<(string label, List<string> entries)> GetDeckInfoRows() (line 169) — Note: 2D navigation variant; each row has individual Left/Right-navigable entries
- private static List<string> BuildCardInfoEntries(MonoBehaviour details, List<TypeGroup> typeGroups = null) (line 206)
- private static void ClassifyTypeGroups(MonoBehaviour costsDetails, List<TypeGroup> typeGroups, out TypeGroup? creatureGroup, out List<TypeGroup> othersGroups, out TypeGroup? landGroup) (line 280)
- private static int ReadTypeLineCount(MonoBehaviour costsDetails, FieldInfo typeLineField) (line 312)
- private static List<string> BuildManaCurveEntries(MonoBehaviour details) (line 330)
- private static string BuildCardInfoText(MonoBehaviour details) (line 368)
- private static string BuildManaCurveText(MonoBehaviour details) (line 411)
- private static string GetAverageCostText(MonoBehaviour details) (line 456) — Note: filters out "0"/"0.0" placeholders
- public static void ClearCache() (line 479)
- private static MonoBehaviour FindTitlePanel() (line 488)
- private static MonoBehaviour FindCostsDetails() (line 512) — Note: searches via GetComponentsInChildren(true) because popup may be closed
- private static void PopulateCostsDetails(MonoBehaviour costsDetails) (line 548) — Note: calls DeckCostsDetails.SetDeck with current filtered main deck to force text refresh
- private static MonoBehaviour FindTypesDetails() (line 582)
- private static void PopulateTypesDetails(MonoBehaviour typesDetails) (line 617) — Note: skips SetDeck when ItemParent already has children to avoid duplicate items (Unity defers Destroy)
- private static object GetGreLocProvider() (line 664)
- private static List<TypeGroup> ReadTypeGroups(MonoBehaviour typesDetails) (line 691) — Note: spacer children separate groups; first line item after spacer = type header, rest = subtypes
- private static bool IsValidCached(MonoBehaviour cached) (line 767)
- private static bool IsValidCachedAllowInactive(MonoBehaviour cached) (line 784)
- private static void InitializeTitlePanelReflection(Type type) (line 802)
- private static void InitializeCostsDetailsReflection(Type type) (line 818)
- private static void InitializePantryReflection() (line 887)
- private static void InitializeTypesDetailsReflection(Type type) (line 940)
- private static void InitializeCardDatabaseReflection() (line 984)
- private static object FindTmpTextOnObject(GameObject go) (line 1034) — Note: matches TextMeshProUGUI/TMP_Text/TextMeshPro by type name, also searches children
- private static string GetTmpTextValue(object tmpTextComponent) (line 1065)
