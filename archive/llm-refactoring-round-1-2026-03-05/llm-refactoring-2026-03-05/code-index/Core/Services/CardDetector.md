# CardDetector.cs

Static utility for detecting card GameObjects and basic card operations.
For card data extraction and model access, use CardModelProvider directly.

Detection: IsCard, GetCardRoot, HasValidTargetsOnBattlefield
Model access: Delegated to CardModelProvider (pass-through methods provided for compatibility)

## Class: CardDetector (static) (line 17)

### Cache Fields (line 20-21)
- static readonly Dictionary<int, bool> _isCardCache (line 20)
- static readonly Dictionary<int, GameObject> _cardRootCache (line 21)

### Card Detection Methods (line 23-250)
- static bool IsCard(GameObject obj) (line 30)
  Note: Uses fast name-based checks first, component checks only as fallback
- static bool IsCardInternal(GameObject obj) (line 43)
- static GameObject GetCardRoot(GameObject obj) (line 97)
  Note: Gets root card prefab from any card-related GameObject
- static GameObject GetCardRootInternal(GameObject obj) (line 110)
- static bool HasValidTargetsOnBattlefield() (line 140)
  Note: Scans battlefield, stack, AND player portraits for "any target" spells
- static bool HasHotHighlight(GameObject obj) (line 190)
  Note: Checks for EXISTENCE of HotHighlight child, not active state
- static bool IsDisplayedFaceUp(GameObject obj) (line 219)
  Note: Checks Model.IsDisplayedFaceDown property for revealed library cards

### Diagnostic Methods (line 256-458)
- static void LogAllHotHighlights() (line 257)
  Note: Logs ALL GameObjects with HotHighlight across ALL zones
- static string GetHotHighlightType(GameObject obj) (line 306)
- static (bool hasActive, bool hasInactive, string childName) GetHotHighlightDiagnostic(GameObject obj) (line 325)
- static string DetectZone(GameObject obj) (line 354)
- static string GetQuickCardName(GameObject obj) (line 382)
- static string GetShortPath(GameObject obj) (line 414)
- static void LogPlayerTargetHighlights() (line 431)

### Cache Management (line 462-476)
- static void ClearCache() (line 467)

### Model Access (Delegated to CardModelProvider) (line 479-548)
- static Component GetDuelSceneCDC(GameObject card) (line 488)
- static object GetCardModel(Component cdcComponent) (line 495)
- static CardInfo ExtractCardInfo(GameObject cardObj) (line 504)
  Note: For DuelScene cards, tries Model data first; for deck list cards, uses GrpId lookup
- static CardInfo ExtractCardInfoFromUI(GameObject cardObj) (line 554)
- static bool IsUILabelText(string text) (line 648)
- static string GetCardName(GameObject cardObj) (line 679)
- static List<CardInfoBlock> GetInfoBlocks(GameObject cardObj, ZoneType zone) (line 690)
  Note: Block order varies by zone: battlefield puts mana cost after rules text
- static List<CardInfoBlock> BuildInfoBlocks(CardInfo info) (line 762)
- static void AddSetAndArtistBlock(List<CardInfoBlock> blocks, CardInfo info) (line 790)

### Card Categorization (Delegated to CardModelProvider) (line 809-836)
- static (bool isCreature, bool isLand, bool isOpponent) GetCardCategory(GameObject card) (line 815)
- static bool IsCreatureCard(GameObject card) (line 821)
- static bool IsLandCard(GameObject card) (line 827)
- static bool IsOpponentCard(GameObject card) (line 833)

### Text Utilities (line 838-905)
- static string ParseManaCost(string rawManaCost) (line 840)
- static string ConvertManaSymbol(string symbol) (line 857)
- static string CleanText(string text) (line 897)

## Struct: CardInfo (line 911)
- bool IsValid (line 913)
- string Name (line 914)
- string ManaCost (line 915)
- string TypeLine (line 916)
- string PowerToughness (line 917)
- string RulesText (line 918)
- string FlavorText (line 919)
- string Rarity (line 920)
- string SetName (line 921)
- string Artist (line 922)
- int Quantity (line 926)
- int OwnedCount (line 930)
- int UsedInDeckCount (line 934)
- bool IsUnowned (line 938)

## Class: CardInfoBlock (line 944)
- string Label (line 946)
- string Content (line 947)
- bool IsVerbose (line 948)
- CardInfoBlock(string label, string content, bool isVerbose) (line 950)
