# DeckCardProvider.cs
Path: src/Core/Services/DeckCardProvider.cs
Lines: 916

## Top-level comments
- Provides reflection-based access to deck list, sideboard, read-only (precon) deck, and commander/companion card holders. Caches results per frame and exposes extraction helpers producing CardInfo with quantity and unowned flag.

## public struct DeckListCardInfo (line 24, nested in DeckCardProvider)
### Fields
- public uint GrpId (line 26)
- public int Quantity (line 27)
- public GameObject TileButton (line 28)
- public GameObject TagButton (line 29)
- public GameObject CardTileBase (line 30)
- public GameObject ViewGameObject (line 31)
### Properties
- public bool IsValid (line 32)

## public struct ReadOnlyDeckCardInfo (line 450, nested in DeckCardProvider)
### Fields
- public uint GrpId (line 452)
- public int Quantity (line 453)
- public GameObject CardGameObject (line 454)
### Properties
- public bool IsValid (line 455)

## public struct CommanderCardInfo (line 640, nested in DeckCardProvider)
### Fields
- public uint GrpId (line 642)
- public int Quantity (line 643)
- public GameObject CardGameObject (line 644)
- public GameObject TileButton (line 645)
- public GameObject TagButton (line 646)
- public bool IsCompanion (line 647)
- public bool IsPartner (line 648)
### Properties
- public bool IsValid (line 649)

## public static class DeckCardProvider (line 17)
### Fields
- private static List<DeckListCardInfo> _cachedDeckListCards = new List<DeckListCardInfo>() (line 36)
- private static GameObject _cachedDeckHolder = null (line 37)
- private static int _cachedDeckListFrame = -1 (line 39)
- private static List<DeckListCardInfo> _cachedSideboardCards = new List<DeckListCardInfo>() (line 42)
- private static int _cachedSideboardFrame = -1 (line 43)
- private static FieldInfo _showUnCollectedField = null (line 366)
- private static bool _showUnCollectedFieldSearched = false (line 367)
- private static List<ReadOnlyDeckCardInfo> _cachedReadOnlyDeckCards = new List<ReadOnlyDeckCardInfo>() (line 459)
- private static int _cachedReadOnlyDeckFrame = -1 (line 460)
- private static List<CommanderCardInfo> _cachedCommanderCards = new List<CommanderCardInfo>() (line 653)
- private static int _cachedCommanderFrame = -1 (line 654)
### Properties
- internal static GameObject CachedDeckHolder (line 38)
### Methods
- public static void ClearDeckListCache() (line 49)
- private static string GetTransformPath(Transform t) (line 58) — Note: debug helper producing a slash-separated hierarchy path
- public static List<DeckListCardInfo> GetDeckListCards() (line 74) — Note: force-activates an inactive MainDeck_MetaCardHolder so its components are reachable
- public static List<DeckListCardInfo> GetSideboardCards() (line 157)
- public static DeckListCardInfo? GetDeckListCardInfo(GameObject element) (line 255)
- public static bool IsDeckListCard(GameObject element) (line 292)
- public static bool IsSideboardCard(GameObject element) (line 300)
- public static DeckListCardInfo? GetSideboardCardInfo(GameObject element) (line 309)
- public static CardInfo? ExtractSideboardCardInfo(GameObject element) (line 337)
- private static bool CheckDeckListCardUnowned(GameObject viewGameObject) (line 373) — Note: reads MetaCardView.ShowUnCollectedTreatment field set by game's SetDisplayInformation
- public static CardInfo? ExtractDeckListCardInfo(GameObject element) (line 407)
- public static void ClearReadOnlyDeckCache() (line 465)
- public static List<ReadOnlyDeckCardInfo> GetReadOnlyDeckCards() (line 476) — Note: searches all StaticColumnMetaCardHolder components (the "StaticColumnManager" GO has no component of that name)
- public static ReadOnlyDeckCardInfo? GetReadOnlyDeckCardInfo(GameObject element) (line 582)
- public static CardInfo? ExtractReadOnlyDeckCardInfo(GameObject element) (line 603)
- public static List<CommanderCardInfo> GetCommanderCards() (line 660) — Note: reads private _cardInstance (ListCommanderView) from each ListCommanderHolder
- public static CommanderCardInfo? GetCommanderCardInfo(GameObject element) (line 779)
- public static CardInfo? ExtractCommanderCardInfo(GameObject element) (line 803) — Note: sets IsCommander = !IsCompanion on returned CardInfo
- public static void ClearCommanderCache() (line 840)
- public static void ClearCache() (line 852)
- private static void ExtractCardViewsInto(System.Collections.IEnumerable cardViews, List<DeckListCardInfo> target) (line 865) — Note: shared between deck list and sideboard extraction
