# CardModelProvider.cs
Path: src/Core/Services/CardModelProvider.cs
Lines: 2052

## Top-level comments
- Provides access to card Model data via reflection: property access, name lookups, and card info extraction. Use CardDetector for card detection; related providers include CardTextProvider, CardStateProvider, DeckCardProvider, ExtendedCardInfoProvider. Mana/text formatting helpers live in `ManaTextFormatter` (split out 2026-04-20, split 8/12).

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
- private static FieldInfo _lastDisplayInfoField (line 1037)
- private static bool _lastDisplayInfoFieldSearched (line 1038)
- private static FieldInfo _availableTitleCountField (line 1039)
- private static FieldInfo _usedTitleCountField (line 1040)
- private static bool _cardObjNameLogged (line 1099)
- private static PropertyInfo _instancePropCached (line 1641)
- private static bool _instancePropSearched (line 1642)

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
- private static string FormatRarityName(string rawRarity) (line 987) — Note: returns null for "None"/"0"; expands "MythicRare" to "Mythic Rare"
- internal static string GetStringBackedIntValue(object stringBackedInt) (line 996) — Note: prefers RawText (handles "*"), falls back to Value
- public static void ExtractCollectionQuantity(GameObject cardObj, ref CardInfo info) (line 1046) — Note: only applies to PagesMetaCardView; sets OwnedCount and UsedInDeckCount via reflection on _lastDisplayInfo
- public static CardInfo? ExtractCardInfoFromModel(GameObject cardObj) (line 1101) — Note: tries DuelScene_CDC first, then MetaCardView (walks up parents up to 5 levels)
- public static CardInfo ExtractCardInfoFromObject(object dataObj) (line 1173) — Note: shared extraction logic; also populates _abilityParentCache and applies RulesTextOverride fallback; calls ManaTextFormatter.ParseManaQuantityArray/ParseManaSymbolsInText/MergeClassLevelLines
- internal static object GetModelInstance(object model) (line 1647) — Note: caches PropertyInfo, falls back to per-type lookup if the cached type doesn't match
- public static CardInfo? GetCardInfoFromGrpId(uint grpId) (line 1677) — Note: cascades menu-scene lookup, duel-scene lookup, then PAPA fallback
- internal static object GetCardDataFromGrpId(uint grpId) (line 1704) — Note: relies on DeckCardProvider.CachedDeckHolder (populates it on demand); tries GetCardPrintingById then GetCardRecordById
- public static CardInfo ExtractCardInfoFromCardData(object cardData, uint grpId) (line 1797) — Note: guards P/T by creature/vehicle check; handles planeswalker loyalty; logs artist extraction; calls ManaTextFormatter.ParseManaQuantityArray/ParseManaSymbolsInText/MergeClassLevelLines
