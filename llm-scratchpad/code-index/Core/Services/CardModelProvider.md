# CardModelProvider.cs
Path: src/Core/Services/CardModelProvider.cs
Lines: 2374

## Top-level comments
- Provides access to card Model data via reflection: property access, name lookups, mana parsing, and card info extraction. Use CardDetector for card detection; related providers include CardTextProvider, CardStateProvider, DeckCardProvider, ExtendedCardInfoProvider.

## public static class CardModelProvider (line 21)
### Fields
- private static Type _cachedModelType (line 24)
- private static readonly Dictionary<string, PropertyInfo> _modelPropertyCache (line 25)
- private static bool _modelPropertiesLogged (line 26)
- private static bool _abilityPropertiesLogged (line 27)
- private static bool _listMetaCardHolderLogged (line 28)
- private static object _idNameProvider (line 31)
- private static MethodInfo _getNameMethod (line 32)
- private static bool _idNameProviderSearched (line 33)
- private static readonly Dictionary<uint, (uint cardGrpId, uint[] abilityIds, uint cardTitleId)> _abilityParentCache (line 37)
- private static bool _metaCardViewComponentsLogged (line 102)
- private static PropertyInfo _cdcModelProp (line 143)
- private static Type _cdcModelPropType (line 144)
- private static bool _metaCardViewPropertiesLogged (line 221)
- private static FieldInfo _lastDisplayInfoField (line 1360)
- private static bool _lastDisplayInfoFieldSearched (line 1361)
- private static FieldInfo _availableTitleCountField (line 1362)
- private static FieldInfo _usedTitleCountField (line 1363)
- private static bool _cardObjNameLogged (line 1422)
- private static PropertyInfo _instancePropCached (line 1964)
- private static bool _instancePropSearched (line 1965)

### Methods
- public static void ClearCache() (line 43) — Note: also clears caches on CardTextProvider, CardStateProvider, DeckCardProvider, ExtendedCardInfoProvider sub-providers
- public static Component GetDuelSceneCDC(GameObject card) (line 71) — Note: returns DuelScene_CDC or Meta_CDC; also searches children for Meta_CDC
- public static Component GetMetaCardView(GameObject card) (line 108) — Note: matches PagesMetaCardView, MetaCardView, BoosterMetaCardView, DraftPackCardView, any "ListMetaCardView"-containing name, ListCommanderView
- public static object GetCardModel(Component cdcComponent) (line 146) — Note: caches CDC Model PropertyInfo; returns null on exception
- public static object GetMetaCardModel(Component metaCardView) (line 171) — Note: tries Model, CardData, Data, Card properties in order
- public static void LogMetaCardViewProperties(Component metaCardView) (line 227) — Note: one-shot (gated by _metaCardViewPropertiesLogged)
- private static void LogListMetaCardHolderProperties(MonoBehaviour holder, Type holderType) (line 258) — Note: debug dump; also enumerates filtered methods and fields
- private static void LogCollectionContents(object collection, string collectionName) (line 378) — Note: only logs first 3 items
- private static void FindIdNameProvider() (line 452) — Note: tries GameManager.CardDatabase.CardTitleProvider / CardNameTextProvider / LocManager, IdNameProvider component, WrapperController.Instance.CardDatabase, and a broad scene scan. Also triggers LogListMetaCardHolderProperties on first discovery.
- public static string GetNameFromGrpId(uint grpId) (line 708) — Note: filters out "$" prefix and "Unknown Card Title" results
- public static void LogModelProperties(object model) (line 774) — Note: one-shot
- public static void LogAbilityProperties(object ability) (line 804) — Note: one-shot; also invokes parameterless string-returning methods
- private static PropertyInfo GetCachedProperty(Type modelType, string propertyName) (line 860) — Note: clears cache on model type change
- internal static object GetModelPropertyValue(object model, Type modelType, params string[] propertyNames) (line 880)
- private static string GetModelStringProperty(object model, Type modelType, params string[] propertyNames) (line 902)
- private static string TryExtractRulesTextOverride(object dataObj, Type objType, uint cardGrpId, uint cardTitleId) (line 913) — Note: handles modal-spell RulesTextOverride; iterates sourceGrpIds to find resolving context
- public static string ParseManaSymbolsInText(string text) (line 989) — Note: runs 4 regex passes including {oX}, bare "2oW:" costs, inline sequences, and standard {X} notation
- private static string ParseBareManaSequence(string sequence) (line 1036)
- private static string ConvertManaSymbolToText(string symbol) (line 1066)
- private static string ConvertSingleManaSymbol(string symbol) (line 1103)
- private static string ParseManaQuantityArray(IEnumerable manaQuantities) (line 1142) — Note: respects ManaColorlessLabel and ManaGroupColors settings; groups simple colors, keeps hybrid/phyrexian individually
- internal static string ConvertManaColorToName(string colorEnum) (line 1260)
- private static void MergeClassLevelLines(List<string> lines) (line 1291) — Note: merges class level-up cost lines with following effect lines based on mana/colon heuristic
- private static string FormatRarityName(string rawRarity) (line 1310) — Note: returns null for "None"/"0"; expands "MythicRare" to "Mythic Rare"
- internal static string GetStringBackedIntValue(object stringBackedInt) (line 1319) — Note: prefers RawText (handles "*"), falls back to Value
- public static void ExtractCollectionQuantity(GameObject cardObj, ref CardInfo info) (line 1369) — Note: only applies to PagesMetaCardView; sets OwnedCount and UsedInDeckCount via reflection on _lastDisplayInfo
- public static CardInfo? ExtractCardInfoFromModel(GameObject cardObj) (line 1424) — Note: tries DuelScene_CDC first, then MetaCardView (walks up parents up to 5 levels)
- public static CardInfo ExtractCardInfoFromObject(object dataObj) (line 1496) — Note: shared extraction logic; also populates _abilityParentCache and applies RulesTextOverride fallback
- internal static object GetModelInstance(object model) (line 1970) — Note: caches PropertyInfo, falls back to per-type lookup if the cached type doesn't match
- public static CardInfo? GetCardInfoFromGrpId(uint grpId) (line 2000) — Note: cascades menu-scene lookup, duel-scene lookup, then PAPA fallback
- internal static object GetCardDataFromGrpId(uint grpId) (line 2027) — Note: relies on DeckCardProvider.CachedDeckHolder (populates it on demand); tries GetCardPrintingById then GetCardRecordById
- public static CardInfo ExtractCardInfoFromCardData(object cardData, uint grpId) (line 2120) — Note: guards P/T by creature/vehicle check; handles planeswalker loyalty; logs artist extraction
