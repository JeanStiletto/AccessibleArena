# ExtendedCardInfoProvider.cs
Path: src/Core/Services/ExtendedCardInfoProvider.cs
Lines: 999

## Top-level comments
- Provides extended card information: keyword descriptions and linked face data. Extracted from CardModelProvider to keep that class focused on core model access.

## public static class ExtendedCardInfoProvider (line 17)
### Fields
- private static PropertyInfo _linkedFaceTypeProp (line 20)
- private static bool _linkedFaceTypePropSearched (line 21)
- private static PropertyInfo _linkedFaceGrpIdsProp (line 22)
- private static bool _linkedFaceGrpIdsPropSearched (line 23)
- private static object _abilityHangerProvider (line 26)
- private static MethodInfo _getHangerConfigsForCardMethod (line 27)
- private static MethodInfo _hangerProviderCleanupMethod (line 28)
- private static object _parameterizedHangerProvider (line 31)
- private static MethodInfo _getParamHangerConfigsMethod (line 32)
- private static object _duelCardDataProvider (line 35)
- private static MethodInfo _duelGetCardPrintingMethod (line 36)
- private static bool _duelCardDataProviderSearched (line 37)
- private static object _papaHangerProvider (line 40)
- private static MethodInfo _papaGetConfigsMethod (line 41)
- private static MethodInfo _papaCleanupMethod (line 42)
- private static bool _papaProviderSearched (line 43)
- private static object _papaCardDataProvider (line 46)
- private static MethodInfo _getCardPrintingByIdMethod (line 47)
- private static MethodInfo _createInstanceMethod (line 48)
- private static ConstructorInfo _cardDataCtor (line 49)
- private static object _holderTypeHand (line 50)
### Methods
- public static void ClearCache() (line 55)
- private static uint GetGrpIdFromCard(GameObject card) (line 84) — Note: tries CDC model first, then MetaCardView (walks up 5 levels if needed)
- public static List<string> GetKeywordDescriptions(GameObject card) (line 122) — Note: uses live scene's hanger provider in duel context, delegates to GrpId path otherwise
- public static List<string> GetKeywordDescriptions(uint grpId) (line 202) — Note: PAPA path with fallback to ability text extraction
- public static (string label, CardInfo faceInfo)? GetLinkedFaceInfo(uint grpId) (line 213)
- public static List<CardInfo> GetLinkedTokenInfos(uint grpId) (line 282)
- private static List<string> GetKeywordDescriptionsFromPAPA(uint grpId) (line 309)
- private static void ConstructProviderFromPAPA() (line 357) — Note: builds AbilityHangerBaseConfigProvider from PAPA singleton's CardDatabase/AssetLookupSystem/ObjectPool
- internal static object GetCardPrintingDataFromPAPA(uint grpId) (line 499) — Note: works even when CreateInstance or CardData ctor reflection lookups fail (e.g. store context)
- private static object CreateCardDataAdapter(uint grpId) (line 532) — Note: CardPrintingData.CreateInstance() then CardData(instance, printing)
- private static List<string> GetAbilityTextsFromCardData(uint grpId) (line 573)
- private static void FindAbilityHangerProvider() (line 623) — Note: uses Resources.FindObjectsOfTypeAll to include inactive GameObjects; also extracts _parameterizedHangers for Cycling/Plot
- private static object CreateCDCViewMetadata(Component cdc) (line 710) — Note: tries BASE_CDC-typed ctor first, falls back to 5-bool ctor with false defaults
- private static void CollectHangerConfigs(object configs, List<string> result, HashSet<string> seen) (line 748) — Note: strips trailing bare mana costs from header (e.g. "Umwandlung o2oW" -> "Umwandlung")
- private static void QueryParameterizedHangers(object model, List<string> result, HashSet<string> seen) (line 789)
- public static (string label, CardInfo faceInfo)? GetLinkedFaceInfo(GameObject card) (line 811)
- public static List<CardInfo> GetLinkedTokenInfos(GameObject card) (line 821)
- private static List<CardInfo> GetLinkedTokenInfosFromModel(object model) (line 830) — Note: reads AbilityIdToLinkedTokenPrinting from Printing sub-object or directly from model; deduplicates by GrpId
- private static string GetLinkedFaceLabel(int linkedFaceType) (line 901) — Note: maps LinkedFace enum int (DfcFront/Back, Split, Adventure, Mdfc, Room) to user-facing label
- internal static object GetCardDataFromGrpIdDuelScene(uint grpId) (line 929)
- private static void FindDuelCardDataProvider() (line 960)
