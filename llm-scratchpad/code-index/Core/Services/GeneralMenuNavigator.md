# GeneralMenuNavigator.cs

General-purpose navigator for menu screens that use CustomButton components. Acts as a fallback for any menu-like screen not handled by a specific navigator. Can also serve as a base class for specific menu navigators.

MTGA uses CustomButton (not standard Unity Button/Selectable) for most menus, which is why standard Unity navigation doesn't work.

```
public class GeneralMenuNavigator : BaseNavigator (line 26)
  #region Configuration Constants (line 28)
  private static readonly HashSet<string> ExcludedScenes (line 31)
  private const int MinButtonsForMenu (line 37)
  private static readonly string[] BladePatterns (line 40)
  #endregion

  #region Foreground Layer System (line 48)
  // The layers that can be "in front", in priority order
  private enum ForegroundLayer (line 56)
    None (line 58)
    Home (line 59)
    ContentPanel (line 60)
    NPE (line 61)
    PlayBlade (line 62)
    Social (line 63)
    Popup (line 64)

  // Single source of truth: what's currently in foreground?
  private ForegroundLayer GetCurrentForeground() (line 73)
  #endregion

  #region Timing Constants (line 101)
  private const float ActivationDelaySeconds (line 103)
  private const float RescanDelaySeconds (line 104)
  private const float BladeAutoExpandDelay (line 105)
  #endregion

  #region State Fields (line 109)
  private static readonly bool DebugLogging (line 114)
  protected string _currentScene (line 116)
  protected string _detectedMenuType (line 117)
  private bool _hasLoggedUIOnce (line 118)
  private float _activationDelay (line 119)
  private bool _announcedServerLoading (line 120)

  // Helper instances
  private readonly MenuScreenDetector _screenDetector (line 123)
  private readonly OverlayDetector _overlayDetector (line 127)
  private readonly ElementGroupAssigner _groupAssigner (line 128)
  private readonly GroupedNavigator _groupedNavigator (line 129)
  private readonly PlayBladeNavigationHelper _playBladeHelper (line 130)
  private readonly ChallengeNavigationHelper _challengeHelper (line 131)

  private bool _groupedNavigationEnabled (line 136)
  private float _rescanDelay (line 139)
  private int _pendingPageRescanFrames (line 142)
  private bool _isInMailDetailView (line 145)
  private Guid _currentMailLetterId (line 146)
  private UITextExtractor.MailContentParts _mailContentParts (line 149)
  private GameObject _profileButtonGO (line 152)
  private string _profileLabel (line 153)
  private float _bladeAutoExpandDelay (line 156)
  private const float NPEButtonCheckInterval (line 159)
  private float _npeButtonCheckTimer (line 160)
  private int _lastNPEButtonCount (line 161)
  private ElementGroup? _lastKnownOverlay (line 164)

  // Booster carousel state
  private List<GameObject> _boosterPackHitboxes (line 167)
  private int _boosterCarouselIndex (line 168)
  private bool _isBoosterCarouselActive (line 169)

  private bool _announceDeckCountOnRescan (line 172)
  private bool _isDeckBuilderReadOnly (line 175)

  // 2D sub-navigation state for DeckBuilderInfo group
  private List<(string label, List<string> entries)> _deckInfoRows (line 179)
  private int _deckInfoEntryIndex (line 180)

  // Friend section sub-navigation state
  private List<(string label, string actionId)> _friendActions (line 183)
  private int _friendActionIndex (line 184)

  // Packet selection sub-navigation state
  private List<CardInfoBlock> _packetBlocks (line 187)
  private int _packetBlockIndex (line 188)
  #endregion

  #region Helper Methods (line 195)
  private void LogDebug(string message) (line 200)

  // Get all active CustomButton GameObjects in the scene
  private IEnumerable<GameObject> GetActiveCustomButtons() (line 210)

  // Check if a type name is a CustomButton variant
  private static bool IsCustomButtonType(string typeName) (line 222)

  // Report a panel opened to PanelStateManager
  private void ReportPanelOpened(string panelName, GameObject panelObj, PanelDetectionMethod detectedBy) (line 230)

  // Report a panel closed to PanelStateManager
  private void ReportPanelClosed(GameObject panelObj) (line 243)

  // Report a panel closed by name to PanelStateManager
  private void ReportPanelClosedByName(string panelName) (line 253)
  #endregion

  public override string NavigatorId => "GeneralMenu" (line 264)
  public override string ScreenName => GetMenuScreenName() (line 265)
  public override int Priority => 15 (line 266)

  // Check if Settings menu is currently open
  private bool IsSettingsMenuOpen() => PanelStateManager.Instance?.IsSettingsMenuOpen == true (line 273)

  // Cached reflection for LoadingPanelShowing.IsShowing
  private static PropertyInfo _loadingPanelIsShowingProp (line 276)
  private static bool _loadingPanelReflectionResolved (line 277)

  // Check if the game's loading panel overlay is currently showing
  private static bool IsLoadingPanelShowing() (line 283)

  // Check if the Social/Friends panel is currently open
  protected bool IsSocialPanelOpen() => _screenDetector.IsSocialPanelOpen() (line 304)

  // Convenience properties to access helper state
  private string _activeContentController => _screenDetector.ActiveContentController (line 307)
  private GameObject _activeControllerGameObject => _screenDetector.ActiveControllerGameObject (line 308)
  private GameObject _navBarGameObject => _screenDetector.NavBarGameObject (line 309)
  private GameObject _foregroundPanel => PanelStateManager.Instance?.GetFilterPanel() (line 310)

  public GeneralMenuNavigator(IAnnouncementService announcer) : base(announcer) (line 312)

  // Handler for PanelStateManager.OnPanelChanged - fires when active (filtering) panel changes
  private void OnPanelStateManagerActiveChanged(PanelInfo oldPanel, PanelInfo newPanel) (line 337)

  // Check if PlayBlade became active and initialize the helper if needed
  private void CheckAndInitPlayBladeHelper(string source) (line 388)

  // Detect challenge screen state changes and initialize/close ChallengeNavigationHelper
  private void CheckAndInitChallengeHelper(string source) (line 424)

  // Handler for PanelStateManager.OnAnyPanelOpened - fires when ANY panel opens
  private void OnPanelStateManagerAnyOpened(PanelInfo panel) (line 450)

  // Handler for PanelStatePatch.OnMailLetterSelected - fires when a mail is opened in the mailbox
  private void OnMailLetterSelected(Guid letterId, string title, string body, bool hasAttachments, bool isClaimed) (line 493)

  // ... additional methods continue from line 500 onward
```

Note: File is very large (29000+ tokens), only first 500 lines indexed. Contains extensive menu detection, element grouping, input handling, and navigation logic.
