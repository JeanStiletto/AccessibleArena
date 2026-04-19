# LoadingScreenNavigator.cs
Path: src/Core/Services/LoadingScreenNavigator.cs
Lines: 1422

## Top-level comments
- Navigator for transitional/info screens with few buttons and optional dynamic content. Handles MatchEnd (victory/defeat), PreGame, Matchmaking (queue), and GameLoading (startup) screens. Uses polling to handle UI that loads after initial scan.

## public class LoadingScreenNavigator : BaseNavigator (line 24)

### Fields
- private ScreenMode _currentMode = ScreenMode.None (line 32)
- private float _pollTimer (line 35)
- private const float PollInterval = 0.5f (line 36)
- private const float GameLoadingPollInterval = 1.0f (line 37)
- private const float MaxPollDuration = 10f (line 38)
- private float _pollElapsed (line 39)
- private int _lastElementCount (line 40)
- private bool _polling (line 41)
- private string _matchResultText = "" (line 44)
- private GameObject _continueButton (line 47)
- private GameObject _cancelButton (line 50)
- private TMP_Text _timerText (line 53)
- private TMP_Text _loadingInfoText (line 56)
- private string _lastLoadingStatusText = "" (line 57)
- private bool _isSurveyPopup (line 60)
- private GameObject _surveyUIContainer (line 61) — The "UI" CanvasGroup child; INACTIVE initially due to animator intro
- private float _surveyPollTimer (line 62)
- private bool _surveyElementsDiscovered (line 63)
- private GameObject _viewLogElement (line 66)
- private bool _dumpedHierarchy (line 69)

### Properties
- public override string NavigatorId => "LoadingScreen" (line 26)
- public override string ScreenName => GetScreenName() (line 27)
- public override int Priority => 65 (line 28)
- protected override bool SupportsCardNavigation => false (line 29)

### Nested Types
- private enum ScreenMode { None, MatchEnd, PreGame, Matchmaking, GameLoading } (line 31)

### Methods
- public LoadingScreenNavigator(IAnnouncementService announcer) (line 71)
- private void Log(string message) (line 73)
- private string GetScreenName() (line 77)
- protected override bool DetectScreen() (line 98) — Yields to settings menu; checks MatchEnd, PreGame, Matchmaking, GameLoading in priority order
- private bool DetectMatchEnd() (line 132)
- private bool DetectPreGame() (line 144)
- private bool DetectGameLoading() (line 155)
- private bool DetectMatchmaking() (line 161)
- protected override void DiscoverElements() (line 178)
- private void DiscoverMatchEndElements() (line 199)
- private void DiscoverPreGameElements() (line 340)
- private void DiscoverMatchmakingElements() (line 474)
- private void DiscoverGameLoadingElements() (line 495)
- private string ExtractMatchResultText(GameObject[] rootObjects) (line 531)
- private bool IsVisibleByCanvasGroup(GameObject obj) (line 577)
- protected override string GetActivationAnnouncement() (line 603)
- protected override bool HandleCustomInput() (line 636)
- protected override bool OnElementActivated(int index, GameObject element) (line 684)
- protected override void OnPopupDetected(PanelInfo panel) (line 721)
- protected override void DiscoverPopupElements(GameObject popup) (line 738)
- private void DiscoverSurveyElements(GameObject popup) (line 754)
- private void DiscoverActiveSurveyElements(GameObject popup, string titleText) (line 807)
- protected override void OnPopupClosed() (line 874)
- protected override void OnActivated() (line 904)
- private void StartPolling() (line 916)
- public override void Update() (line 930)
- protected override bool ValidateElements() (line 1076)
- public override void OnSceneChanged(string sceneName) (line 1112)
- private string CleanStatusText(string text) (line 1182)
- private string FindTextByName(GameObject root, string name) (line 1192)
- private GameObject FindChildRecursive(Transform parent, string name) (line 1200)
- private string ExtractRankProgress(GameObject root) (line 1219)
- private string ReadNewRankText(GameObject root, System.Type rdType, MonoBehaviour rankDisplay) (line 1314)
- private string ExtractMythicProgress(GameObject root) (line 1356)
- private int GetIntField(System.Type type, object obj, string fieldName) (line 1383)
- private void DumpHierarchy(Transform t, int depth, int maxDepth) (line 1395)
