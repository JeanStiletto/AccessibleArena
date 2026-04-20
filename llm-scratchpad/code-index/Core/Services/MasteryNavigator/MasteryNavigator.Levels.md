# MasteryNavigator.Levels.cs (partial)
Path: src/Core/Services/MasteryNavigator/MasteryNavigator.Levels.cs
Lines: 1080

## Top-level comments
- Levels mode of MasteryNavigator. Reflects into Core.MainNavigation.RewardTrack types (ProgressionTracksContentController, RewardTrackView, ProgressionTrackLevel, ClientTrackLevelInfo, RewardDisplayData, SetMasteryDataProvider) to extract per-level XP, completion state, and tiered rewards (Free/Premium/Renewal). Also owns localization helpers (MTGALocalizedString.ToString, Languages.ActiveLocProvider.GetLocalizedText).

## public partial class MasteryNavigator (line 12)

### Fields
- private const int LevelsPerPageJump = 10 (line 16)

### Navigation State
- private int _currentLevelIndex (line 22) — Note: index into `_levelData` (0 = virtual status item, 1+ = real levels)
- private int _currentTierIndex (line 23) — Note: 0=Free, 1=Premium, 2=Renewal

### Cached Controller & Reflection (lines 29-93)
- private MonoBehaviour _controller (line 29)
- private GameObject _controllerGameObject (line 30)
- Reflection: controller (lines 33-36), view (lines 39-50), PageLevels (53-55), ProgressionTrackLevel (58-63), ClientTrackLevelInfo (66-67), RewardDisplayData (70-74), SetMasteryDataProvider (77-81), MTGALocalizedString (84-86), Languages (89-91)
- private bool _reflectionInitialized (line 93)

### Nested types
- private struct LevelData (line 99) — LevelNumber, ExpProgress, XpToComplete, IsComplete, IsCurrent, IsRepeatable, Tiers
- private struct TierReward (line 110) — TierName, RewardName, Quantity, Description
- private struct ActionButton (line 118) — Button, GameObject, Label

### Discovered Data
- private readonly List<LevelData> _levelData (line 125)
- private readonly List<ActionButton> _actionButtons (line 126)
- private string _trackTitle (line 127)
- private int _totalLevels (line 128)
- private int _currentPlayerLevel (line 129) — Note: index of player's in-progress level in `_levelData`

### Screen Detection (Levels)
- private MonoBehaviour FindLevelsController() (line 135)
- private bool IsControllerOpen(MonoBehaviour controller) (line 154)

### Reflection Caching
- private void EnsureReflectionCached(Type controllerType) (line 176) — Note: caches all view/level/reward/localization reflection in one pass

### Element Discovery (Levels)
- private void BuildLevelData() (line 296) — Note: calls SetMasteryDataProvider.GetCurrentLevelIndex(trackName) to locate current in-progress level
- private LevelData ExtractLevelData(object level, int listIndex, IList rewardDataList, int curLevelIndex) (line 383)
- private void BuildActionButtons() (line 472) — Note: adds Mastery Tree, Previous Season, Purchase, Back
- private void TryAddButton(MonoBehaviour view, FieldInfo field, string label) (line 521)
- private void InsertStatusItem() (line 560) — Note: creates virtual LevelNumber=0 with XP info + action buttons as tiers, shifts `_currentPlayerLevel` by 1

### Localization
- private string ResolveLocString(object mtgaLocString) (line 616) — Note: filters empty keys (MainNav/General/Empty_String) and "$"-prefixed raw keys
- private string GetLocalizedText(string key) (line 648) — Note: Languages.ActiveLocProvider.GetLocalizedText via cached reflection
- private string ResolveTrackTitle(MonoBehaviour view) (line 665) — Note: prefers TrackLabel (MTGALocalizedString), falls back to `MainNav/BattlePass/{trackName}` + `MainNav/BattlePass/SetXMastery` template

### Announcements (Levels)
- private void AnnounceCurrentLevel() (line 716) — Note: virtual status item (LevelNumber==0) announces status text; real levels get position + reward + status
- private void AnnounceCurrentTier() (line 741)
- private void AnnounceLevelDetail() (line 761)
- private string GetPrimaryRewardName(LevelData level) (line 793)
- private string GetLevelStatus(LevelData level) (line 805)

### Page Sync
- private MonoBehaviour GetActiveView() (line 816)
- private int GetCurrentPage() (line 830)
- private int GetPagesCount() (line 842)
- private void SyncPageForLevel() (line 857) — Note: walks PageLevels.LevelStart/LevelEnd to find the page containing current level, updates view.CurrentPage

### Input Handling (Levels)
- private void HandleLevelInput() (line 908) — Note: Up/Down/Tab navigate; Left/Right CycleTier; Home/End/PageUp/PageDown jumps; Enter activates status button tier or AnnounceLevelDetail; Backspace NavigateToHome
- private void CycleTier(int direction) (line 1024)
- private void ActivateBackButton(int direction) (line 1044) — Note: currently unused helper; looks up Back button from `_actionButtons` or `_backButtonField`
