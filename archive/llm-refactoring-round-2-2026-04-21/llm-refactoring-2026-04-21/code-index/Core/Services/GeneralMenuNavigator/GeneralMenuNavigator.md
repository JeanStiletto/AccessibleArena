# GeneralMenuNavigator.cs
Path: src/Core/Services/GeneralMenuNavigator/GeneralMenuNavigator.cs
Lines: 3427

## Top-level comments
- General-purpose navigator for menu screens using MTGA CustomButton components; acts as a fallback for menus not handled by specific navigators, and can serve as a base class for specific menu navigators.
- Core partial. Other partials (Mail, Booster, Social, DeckBuilder, BackNavigation, Collection) extend this class with feature-specific state + methods.

## public partial class GeneralMenuNavigator : BaseNavigator (line 30)
### Fields
- private static readonly HashSet<string> ExcludedScenes (line 35)
- private const int MinButtonsForMenu (line 41)
- private static readonly string[] BladePatterns (line 44)
- private const float ActivationDelaySeconds (line 107)
- private const float RescanDelaySeconds (line 108)
- private const float BladeAutoExpandDelay (line 109)
- private static readonly bool DebugLogging (line 118)
- protected string _currentScene (line 120)
- protected string _detectedMenuType (line 121)
- private bool _hasLoggedUIOnce (line 122)
- private float _activationDelay (line 123)
- private bool _announcedServerLoading (line 124)
- private readonly MenuScreenDetector _screenDetector (line 127)
- private readonly OverlayDetector _overlayDetector (line 130)
- private readonly ElementGroupAssigner _groupAssigner (line 131)
- private readonly GroupedNavigator _groupedNavigator (line 132)
- private readonly PlayBladeNavigationHelper _playBladeHelper (line 133)
- private readonly ChallengeNavigationHelper _challengeHelper (line 134)
- private bool _groupedNavigationEnabled (line 140)
- private float _rescanDelay (line 143)
- private bool _suppressRescanAnnouncement (line 144)
- private float _bladeAutoExpandDelay (line 147)
- private const float NPEButtonCheckInterval (line 150)
- private float _npeButtonCheckTimer (line 151)
- private int _lastNPEButtonCount (line 152)
- private ElementGroup? _lastKnownOverlay (line 155)
- private static PropertyInfo _loadingPanelIsShowingProp (line 240)
- private static bool _loadingPanelReflectionResolved (line 241)

### Properties
- public override string NavigatorId (line 228)
- public override string ScreenName (line 229)
- public override int Priority (line 230)
- private string _activeContentController (line 264)
- private GameObject _activeControllerGameObject (line 265)
- private GameObject _navBarGameObject (line 266)
- private GameObject _foregroundPanel (line 267)

### Methods
- private ForegroundLayer GetCurrentForeground() (line 77) — Note: single source of truth for which layer is on top (Home/ContentPanel/NPE/PlayBlade/Social/Popup)
- private void LogDebug(string message) (line 164)
- private IEnumerable<GameObject> GetActiveCustomButtons() (line 174)
- private static bool IsCustomButtonType(string typeName) (line 186)
- private void ReportPanelOpened(string panelName, GameObject panelObj, PanelDetectionMethod detectedBy) (line 194)
- private void ReportPanelClosed(GameObject panelObj) (line 207)
- private void ReportPanelClosedByName(string panelName) (line 218)
- private bool IsSettingsMenuOpen() (line 237)
- private static bool IsLoadingPanelShowing() (line 247)
- public GeneralMenuNavigator(IAnnouncementService announcer) (line 269) — Note: subscribes to PanelStateManager events and PanelStatePatch.OnMailLetterSelected
- private void OnPanelStateManagerActiveChanged(PanelInfo oldPanel, PanelInfo newPanel) (line 295) — Note: ignores SocialUI-as-source changes and SettingsMenu changes; resets mail detail state when mailbox closes
- private void CheckAndInitPlayBladeHelper(string source) (line 346) — Note: skips normal PlayBlade init when blade state >= 2 (challenge screens)
- private void CheckAndInitChallengeHelper(string source) (line 382)
- private void OnPanelStateManagerAnyOpened(PanelInfo panel) (line 408) — Note: schedules blade auto-expand for Color Challenge panels
- private void OnPanelStateManagerPlayBladeChanged(int state) (line 453)
- protected virtual string GetMenuScreenName() (line 468) — Note: returns context-aware screen name (PlayBlade state, active content controller, NPE, etc.)
- public override void OnSceneChanged(string sceneName) (line 556)
- protected override void OnActivated() (line 588) — Note: name suggests init only, but also enables popup detection
- protected override void OnPopupDetected(PanelInfo panel) (line 594) — Note: saves grouped navigation state before entering popup mode
- protected override void OnDeactivating() (line 605)
- protected override void ForceRescanAfterSearch() (line 626) — Note: quiet rescan announcing only pool card-count change (or position if Tab from search field)
- protected override bool ValidateElements() (line 716) — Note: deactivates when Settings/NPE rewards are detected so dedicated navigators take over
- public override void Update() (line 742) — Note: drives rescan delays, page rescan, blade auto-expand, NPE button polling, overlay change detection, and challenge helper polling
- protected override bool HandleCustomInput() (line 838) — Note: F4 toggles Friends; F12 debug hierarchy; F11 booster pack dump; Backspace routes through challenge/PlayBlade helpers then grouped back then HandleBackNavigation
- private void DumpUIHierarchy() (line 1052)
- private bool ShouldShowElement(GameObject obj) (line 1067) — Note: excludes Options_Button, MainButton_Leave, Clicker globally; applies overlay filtering first, then content panel filtering
- private bool IsChildOfContentPanel(GameObject obj) (line 1132) — Note: special case for CampaignGraph allows blade content + main button; Nav_WildCard exception for deck builder/booster
- private bool IsChildOfHomeOrNavBar(GameObject obj) (line 1164)
- private static bool IsInsideObjectivesPanel(GameObject obj) (line 1181)
- private static bool IsChildOf(GameObject child, GameObject parent) (line 1197)
- private static bool IsPopupOverlay(GameObject obj) (line 1218)
- private bool IsInsideBlade(GameObject obj) (line 1229)
- private static bool IsInsideBladeListItem(GameObject obj) (line 1253)
- private static bool IsInsidePlayBladeContainer(GameObject obj) (line 1277)
- private bool IsMainButton(GameObject obj) (line 1301)
- private void AutoPressPlayButtonInPlayBlade() (line 1329) — Note: finds PlayBlade's MainButton (without MainButton component) and clicks it after deck selection
- private static bool IsInsideNPEOverlay(GameObject obj) (line 1371)
- private void CheckForNewNPEButtons() (line 1398)
- protected override void RescanAfterSpinnerChange() (line 1415) — Note: restores Challenge/PlayBlade grouped state via explicit entry-at-index; closes DeckSelectBlade that auto-opens on spinner change
- private void TriggerRescan() (line 1517)
- private void PerformRescan() (line 1525) — Note: preserves selection, injects DeckInfo/EventInfo/ChallengeStatus groups, announces deck card count change when flagged
- protected override bool DetectScreen() (line 1624) — Note: blocks activation for Settings/NPE/Store/Mastery/Codex/Achievements screens and during loading panel
- protected int CountActiveCustomButtons() (line 1700)
- protected virtual string DetectMenuType() (line 1705)
- protected GameObject FindButtonByPattern(params string[] patterns) (line 1728)
- protected virtual void LogAvailableUIElements() (line 1746)
- protected override void DiscoverElements() (line 1751) — Note: sorts by (X, -Y), packet-root override for JumpStart tiles, Recent tab reverses order and hides standalone play buttons, ReadOnly deck fallback
- private void LogHierarchy(Transform parent, string indent, int maxDepth) (line 2250)
- private string GetFullPath(Transform t) (line 2258)
- protected virtual string BuildAnnouncement(UIElementClassifier.ClassificationResult classification) (line 2266)
- protected override void ActivateCurrentElement() (line 2275) — Note: read-only deck cards show warning instead of activating; packet tiles route through EventAccessor.ClickPacket
- protected string GetGameObjectPath(GameObject obj) (line 2303)
- public override string GetTutorialHint() (line 2305)
- protected override string GetActivationAnnouncement() (line 2310)
- protected override void MoveNext() (line 2338) — Note: DeckBuilderInfo 2D sub-nav, Friend section, DeckBuilder Tab cycle, default grouped navigation
- protected override void MovePrevious() (line 2413)
- protected override void MoveFirst() (line 2486)
- private void UpdateCardNavigationForGroupedElement() (line 2518) — Note: activates CardInfoNavigator for cards/deck cards/sideboard/commander; refreshes packet blocks for JumpStart packets
- private void UpdateEventSystemSelectionForGroupedElement() (line 2569) — Note: also manipulates InputManager flags (AllowNativeEnterOnLogin, BlockSubmitForToggle) and reverts MTGA's OnSelect re-toggle on toggles
- protected override void MoveLast() (line 2628)
- protected override bool HandleLetterNavigation(KeyCode key) (line 2666) — Note: at GroupList level searches group display names; at InsideGroup level searches element labels
- private bool HandleGroupedEnter() (line 2723) — Note: folder groups activate toggle + enter; PlayBladeFolders re-toggles if Unity toggled off; DeckBuilderDeckList force-refreshes cache on entry
- private bool HandleGroupedBackspace() (line 2957) — Note: exits subgroup/group; challenge folder exit requests ChallengeMain entry; PlayBlade folder exit requests folders-list entry
- protected override bool OnElementActivated(int index, GameObject element) (line 3021) — Note: dispatches to Challenge/PlayBlade helpers first; auto-plays after deck selection; triggers rescans for toggles/input fields/packets/Color Challenge
- private void InjectChallengeStatusElement() (line 3208)
- private void InjectEventInfoGroup() (line 3259) — Note: each event info block becomes its own standalone virtual group
- private void EnrichColorChallengeLabels() (line 3309)
- private void AutoExpandBlade() (line 3352)
- private string DetectActiveContentController() (line 3381)
- private bool HasVisibleCarousel() (line 3386)
- private bool HasColorChallengeVisible() (line 3395)
- private string GetPlayBladeStateName() (line 3401)
- private string GetContentControllerDisplayName(string controllerTypeName) (line 3422)

## private enum ForegroundLayer (nested in GeneralMenuNavigator) (line 60)
- None (line 62)
- Home (line 63)
- ContentPanel (line 64)
- NPE (line 65)
- PlayBlade (line 66)
- Social (line 67)
- Popup (line 68)
