# CardDetector.cs
Path: src/Core/Services/CardDetector.cs
Lines: 816

## public static class CardDetector (line 20)

Static utility for detecting card GameObjects and basic card operations. Delegates model access to CardModelProvider.

### Fields
- private static readonly Dictionary<int, bool> _isCardCache (line 23)
- private static readonly Dictionary<int, GameObject> _cardRootCache (line 24)
- private static System.Reflection.PropertyInfo _faceDownProp (line 219)
- private static Type _faceDownPropType (line 220)
- private static string _cachedSceneName = "" (line 294)
- private static int _cachedSceneFrame = -1 (line 295)

### Methods
- public static bool IsCard(GameObject obj) (line 33)
- private static bool IsCardInternal(GameObject obj) (line 46)
- public static GameObject GetCardRoot(GameObject obj) (line 100)
- private static GameObject GetCardRootInternal(GameObject obj) (line 113)
- public static bool HasValidTargetsOnBattlefield() (line 143) — scans battlefield, stack, player portraits for HotHighlight
- public static bool HasHotHighlight(GameObject obj) (line 193) — checks existence of HotHighlight child regardless of active state
- public static bool IsDisplayedFaceUp(GameObject obj) (line 222) — uses Model.IsDisplayedFaceDown
- public static void ClearCache() (line 262) — also clears CardModelProvider and CardPoolAccessor
- public static Component GetDuelSceneCDC(GameObject card) (line 283) — delegates to CardModelProvider
- public static object GetCardModel(Component cdcComponent) (line 290) — delegates to CardModelProvider
- private static bool IsInDuelScene() (line 297) — cached per frame
- public static CardInfo ExtractCardInfo(GameObject cardObj) (line 313) — tries deck/commander/read-only extraction, then Model, then UI fallback
- private static CardInfo ExtractCardInfoFromUI(GameObject cardObj) (line 372)
- private static bool IsUILabelText(string text) (line 466)
- public static string GetCardName(GameObject cardObj) (line 497)
- public static List<CardInfoBlock> GetInfoBlocks(GameObject cardObj, ZoneType zone = ZoneType.Hand) (line 508) — order varies by zone (Browser/Battlefield/Hand)
- public static List<CardInfoBlock> BuildInfoBlocks(CardInfo info) (line 587)
- private static void AddSetAndArtistBlock(List<CardInfoBlock> blocks, CardInfo info) (line 615)
- public static (bool isCreature, bool isLand, bool isOpponent) GetCardCategory(GameObject card) (line 640) — delegates to CardStateProvider
- public static bool IsCreatureCard(GameObject card) (line 646)
- public static bool IsLandCard(GameObject card) (line 652)
- public static bool IsOpponentCard(GameObject card) (line 658)
- internal static string ParseManaCost(string rawManaCost) (line 665)
- internal static string ConvertManaSymbol(string symbol) (line 682) — handles W/U/B/R/G/C/S/X/T/Q/E, hybrid pairs, Phyrexian
- internal static string ReplaceSpriteTagsWithText(string text) (line 727)
- private static string CleanText(string text) (line 743)

## public struct CardInfo (line 757)

Contains extracted card information.

### Fields
- public bool IsValid (line 759)
- public string Name (line 760)
- public string ManaCost (line 761)
- public string TypeLine (line 762)
- public string PowerToughness (line 763)
- public string RulesText (line 764)
- public List<string> RulesLines (line 769) — individual parsed ability lines for multi-ability cards
- public string FlavorText (line 770)
- public string Rarity (line 771)
- public string SetName (line 772)
- public string Artist (line 773)
- public int Quantity (line 777) — deck list quantity, 0 if not applicable
- public int OwnedCount (line 781) — collection cards
- public int UsedInDeckCount (line 785) — copies already used in current deck
- public bool IsUnowned (line 789)
- public bool IsCommander (line 793)
- public bool IsCompanion (line 797)

## public class CardInfoBlock (line 803)

A single navigable block of card information.

### Properties
- public string Label { get; } (line 805)
- public string Content { get; } (line 806)
- public bool IsVerbose { get; } (line 807)

### Methods
- public CardInfoBlock(string label, string content, bool isVerbose = true) (line 809)
