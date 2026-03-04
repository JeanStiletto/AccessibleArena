# MasteryNavigator.cs

## Summary
Standalone navigator for the MTGA Mastery/Rewards screen (RewardTrack scene). Navigates mastery levels with Up/Down, cycles reward tiers with Left/Right, and handles page sync automatically.

## Classes

### MasteryNavigator : BaseNavigator (line 19)
```
public class MasteryNavigator : BaseNavigator
  // Constants
  private const int MasteryPriority (line 23)
  private const int LevelsPerPageJump (line 24)

  // Mode
  private enum MasteryMode { Levels, PrizeWall } (line 30)
  private MasteryMode _mode (line 31)

  // Navigator Identity
  public override string NavigatorId => "Mastery" (line 37)
  public override string ScreenName => ... (line 38)
  public override int Priority => MasteryPriority (line 39)
  protected override bool SupportsCardNavigation => false (line 40)
  protected override bool AcceptSpaceKey => false (line 41)

  // Navigation State
  private int _currentLevelIndex (line 47)
  private int _currentTierIndex (line 48)
  private MonoBehaviour _prizeWallController (line 51)
  private GameObject _prizeWallGameObject (line 52)
  private List<(GameObject obj, string label)> _prizeWallItems (line 53)
  private int _prizeWallIndex (line 54)
  private string _sphereCount (line 55)
  private GameObject _prizeWallBackButton (line 56)

  // Cached Controller & Reflection
  private MonoBehaviour _controller (line 62)
  private GameObject _controllerGameObject (line 63)
  private Type _controllerType (line 66)
  private PropertyInfo _isOpenProp (line 67)
  private FieldInfo _activeViewField (line 68)
  private FieldInfo _backButtonField (line 69)
  // ... (many reflection fields for RewardTrackView, PageLevels, etc.)

  // Discovered Data
  private struct LevelData (line 145)
  private struct TierReward (line 156)
  private struct ActionButton (line 164)
  private readonly List<LevelData> _levelData (line 171)
  private readonly List<ActionButton> _actionButtons (line 172)
  private string _trackTitle (line 173)
  private int _totalLevels (line 174)
  private int _currentPlayerLevel (line 175)

  public MasteryNavigator(IAnnouncementService announcer) : base(announcer) (line 181)

  // Screen Detection
  protected override bool DetectScreen() (line 189)
  private MonoBehaviour FindLevelsController() (line 214)
  private MonoBehaviour FindPrizeWallController() (line 233)
  private bool IsControllerOpen(MonoBehaviour controller) (line 253)
  private bool IsPrizeWallOpen(MonoBehaviour controller) (line 271)
  protected override bool IsPopupExcluded(PanelInfo panel) (line 291)
  protected override void OnPopupClosed() (line 302)

  // Reflection Caching
  private void EnsureReflectionCached(Type controllerType) (line 315)
  private void EnsurePrizeWallReflectionCached(Type controllerType) (line 445)

  // Element Discovery
  protected override void DiscoverElements() (line 482)
  private void BuildLevelData() (line 505)
  private LevelData ExtractLevelData(object level, int listIndex, IList rewardDataList, int curLevelIndex) (line 591)
  private void BuildActionButtons() (line 680)
  private void TryAddButton(MonoBehaviour view, FieldInfo field, string label) (line 729)
  private void InsertStatusItem() (line 768)
  private void DiscoverPrizeWallItems() (line 820)
  private string ExtractStoreItemLabel(GameObject storeItemGo) (line 939)

  // Localization
  private string ResolveLocString(object mtgaLocString) (line 1002)
  private string GetLocalizedText(string key) (line 1034)
  private string ResolveTrackTitle(MonoBehaviour view) (line 1051)

  // Activation & Deactivation
  protected override void OnActivated() (line 1102)
  protected override void OnDeactivating() (line 1119)
  public override void OnSceneChanged(string sceneName) (line 1128)

  // Announcements
  protected override string GetActivationAnnouncement() (line 1144)
  protected override string GetElementAnnouncement(int index) (line 1168)
  private void AnnounceCurrentLevel() (line 1174)
  private void AnnounceCurrentTier() (line 1198)
  private void AnnounceLevelDetail() (line 1215)
  private string GetPrimaryRewardName(LevelData level) (line 1247)
  private string GetLevelStatus(LevelData level) (line 1259)

  // Page Sync
  private MonoBehaviour GetActiveView() (line 1270)
  private int GetCurrentPage() (line 1284)
  private int GetPagesCount() (line 1296)
  private void SyncPageForLevel() (line 1311)

  // Update Loop
  public override void Update() (line 1362)
  protected override bool ValidateElements() (line 1416)

  // Input Handling
  private void HandleMasteryInput() (line 1428)
  private void HandleLevelInput() (line 1443)
  private void CycleTier(int direction) (line 1559)
  private void ActivateBackButton() (line 1579)
  private void HandlePrizeWallInput() (line 1613)
  private void AnnouncePrizeWallItem() (line 1699)
  private void ActivatePrizeWallItem() (line 1708)
```
