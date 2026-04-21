# PlayerPortraitNavigator.Properties.cs (partial)
Path: src/Core/Services/PlayerPortraitNavigator/PlayerPortraitNavigator.Properties.cs
Lines: 429

## Top-level comments
- Properties partial. Owns the PlayerProperty enum + cycling logic (GetPropertyValue dispatch, IsPropertyVisible filtering, FindNextVisibleProperty navigation). Also owns rank lookup and username extraction from PlayerNameView. Rank reflection cache lives here.

## public partial class PlayerPortraitNavigator (line 17)

### Nested types
- private enum PlayerProperty (line 20) — Life, Effects, Timer, Timeouts, Wins, Rank
- private const int PropertyCount = 6 (line 21)

### Rank reflection cache
- private static PropertyInfo _matchManagerProp (line 24)
- private static PropertyInfo _localPlayerInfoProp (line 25)
- private static PropertyInfo _opponentInfoProp (line 26)
- private static FieldInfo _rankingClassField (line 27)
- private static FieldInfo _rankingTierField (line 28)
- private static FieldInfo _mythicPercentileField (line 29)
- private static FieldInfo _mythicPlacementField (line 30)
- private static bool _rankReflectionInitialized (line 31)

### Methods
- private string GetPropertyValue(PlayerProperty property) (line 36) — Note: switches on property. Life → GetLifeTotals + BuildLifeWithCounters + GetPlayerUsername merge; Effects/Timer/Timeouts/Wins/Rank delegate to per-concern helpers in other partials
- private bool IsPropertyVisible(PlayerProperty property) (line 87) — Note: Life always visible. Effects visible if HasEffectsContent; Timer visible if match clock or rope exists; Wins visible if > 0 for either player; Rank visible if non-empty
- private bool HasEffectsContent(bool isOpponent) (line 121) — Note: cheaper than building full effects string; reads Life-partial's entity reflection (_designationsField/_abilitiesField/_dungeonStateField)
- private int FindNextVisibleProperty(int currentIndex, bool forward) (line 186) — Note: returns -1 if no visible property in given direction
- private int GetWinCount(bool isOpponent) (line 204) — Note: currently always returns 0 (Bo3 win detection not implemented)
- private string GetPlayerRank(bool isOpponent) (line 215) — Note: walks GameManager → MatchManager → PlayerInfo → RankingClass/Tier/MythicPercentile/MythicPlacement; handles Mythic placement and tier-based ranks (Bronze through Master)
- public string GetMatchupText() (line 293) — Note: returns "local vs opponent" string or null if either username missing
- private string GetPlayerUsername(bool isOpponent) (line 305) — Note: scans for LocalPlayerNameView/OpponentNameView GameObjects; emits verbose debug logs for each NameView
- private static void InitializeRankReflection(object gameManager) (line 355) — Note: lazy-init on first call; falls back to logging all fields/properties if RankingClass field is missing
