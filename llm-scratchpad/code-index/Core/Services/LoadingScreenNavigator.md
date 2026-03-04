# LoadingScreenNavigator.cs

## Summary
Navigator for transitional/info screens with few buttons and optional dynamic content. Handles MatchEnd (victory/defeat), Matchmaking (queue), and GameLoading (startup) screens. Uses polling to handle UI that loads after the initial scan.

## Classes

### LoadingScreenNavigator : BaseNavigator (line 20)
```
public class LoadingScreenNavigator : BaseNavigator
  public override string NavigatorId => "LoadingScreen" (line 22)
  public override string ScreenName => GetScreenName() (line 23)
  public override int Priority => 65 (line 24)
  protected override bool SupportsCardNavigation => false (line 25)

  private enum ScreenMode { None, MatchEnd, PreGame, Matchmaking, GameLoading } (line 27)
  private ScreenMode _currentMode (line 28)

  // Polling state
  private float _pollTimer (line 31)
  private const float PollInterval (line 32)
  private const float GameLoadingPollInterval (line 33)
  private const float MaxPollDuration (line 34)
  private float _pollElapsed (line 35)
  private int _lastElementCount (line 36)
  private bool _polling (line 37)

  private string _matchResultText (line 40)
  private GameObject _continueButton (line 43)
  private GameObject _cancelButton (line 46)
  private TMP_Text _timerText (line 49)
  private TMP_Text _loadingInfoText (line 52)
  private string _lastLoadingStatusText (line 53)
  private bool _dumpedHierarchy (line 56)

  public LoadingScreenNavigator(IAnnouncementService announcer) : base(announcer) (line 58)
  private void Log(string message) (line 60)

  // Screen Name
  private string GetScreenName() (line 64)

  // Screen Detection
  protected override bool DetectScreen() (line 85)
  private bool DetectMatchEnd() (line 119)
  private bool DetectPreGame() (line 130)
  private bool DetectGameLoading() (line 140)
  private bool DetectMatchmaking() (line 146)

  // Element Discovery
  protected override void DiscoverElements() (line 163)
  private void DiscoverMatchEndElements() (line 184)
  private void DiscoverPreGameElements() (line 347)
  private void DiscoverMatchmakingElements() (line 481)
  private void DiscoverGameLoadingElements() (line 502)

  // Text Extraction
  private string ExtractMatchResultText(GameObject[] rootObjects) (line 538)

  // Filtering
  private bool IsVisibleByCanvasGroup(GameObject obj) (line 584)

  // Announcements
  protected override string GetActivationAnnouncement() (line 610)

  // Input Handling
  protected override bool HandleCustomInput() (line 643)
  protected override bool OnElementActivated(int index, GameObject element) (line 682)

  // Polling (Update)
  protected override void OnActivated() (line 713)
  private void StartPolling() (line 719)
  public override void Update() (line 733)

  // Validation & Lifecycle
  protected override bool ValidateElements() (line 826)
  public override void OnSceneChanged(string sceneName) (line 862)

  // Helpers
  private string CleanStatusText(string text) (line 890)
  private string FindTextByName(GameObject root, string name) (line 900)
  private GameObject FindChildRecursive(Transform parent, string name) (line 908)
  private void DumpHierarchy(Transform t, int depth, int maxDepth) (line 925)
```
