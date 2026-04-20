# PlayerPortraitNavigator.Timer.cs (partial)
Path: src/Core/Services/PlayerPortraitNavigator/PlayerPortraitNavigator.Timer.cs
Lines: 540

## Top-level comments
- Timer partial. Owns match clock (MatchTimer._matchTimer → MtgTimer) + rope timer (LowTimeWarning._activeTimer), timeout pip counts, LowTimeWarning UnityEvent subscription, and both MtgTimer and LowTimeWarning reflection caches. AnnounceTimer is the public E/Shift+E entry point.

## public partial class PlayerPortraitNavigator (line 17)

### Timer element cache
- private GameObject _localTimerObj (line 20)
- private GameObject _opponentTimerObj (line 21)
- private MonoBehaviour _localMatchTimer (line 22)
- private MonoBehaviour _opponentMatchTimer (line 23)

### LowTimeWarning subscription
- private MonoBehaviour _localLowTimeWarning (line 26)
- private MonoBehaviour _opponentLowTimeWarning (line 27)
- private UnityAction<bool> _localRopeCallback (line 28)
- private UnityAction<bool> _opponentRopeCallback (line 29)

### MtgTimer reflection cache (shared by MatchTimer and LowTimeWarning)
- private static FieldInfo _matchTimerField (line 32) — MatchTimer._matchTimer (MtgTimer)
- private static FieldInfo _timeRunningField (line 33) — MatchTimer._timeRunning (float)
- private static PropertyInfo _remainingTimeProp (line 34) — MtgTimer.RemainingTime (float)
- private static FieldInfo _runningField (line 35) — MtgTimer.Running (bool)
- private static bool _mtgTimerReflectionInitialized (line 36)

### LowTimeWarning reflection cache
- private static FieldInfo _ltwActiveTimerField (line 39)
- private static FieldInfo _ltwTimeRunningField (line 40)
- private static FieldInfo _ltwTimeoutPipsField (line 41)
- private static FieldInfo _ltwIsVisibleField (line 42)
- private static bool _ltwReflectionInitialized (line 43)

### Methods
- private void DiscoverTimerElements() (line 45) — Note: scans MatchTimer MonoBehaviours; matches LocalPlayer/Opponent by GameObject name
- private string GetTimerText(GameObject timerObj) (line 82) — Note: reads the `Text` TMP child; currently only used internally
- private string FormatTimerText(string timerText) (line 108) — Note: converts "MM:SS" to "X minutes Y seconds"; currently only used internally
- private int GetTimeoutCount(string playerType) (line 140) — Note: reads "xN" from LocalPlayer/Opponent TimeoutDisplay TMP
- private string GetMatchTimerInfo(MonoBehaviour matchTimer) (line 164) — Note: reads IsLowTime/IsWarning via GetProperty<T>; currently only returns "low time warning" when set
- private T GetProperty<T>(System.Type type, object obj, string propName) (line 184) — Note: generic reflection helper; swallows exceptions, returns default. Type parameter T shadows the `using T = GameTypeNames` alias within the method body
- public void AnnounceTimer(bool opponent) (line 203) — Note: match-clock first (GetTimerFromModel), falls back to rope (GetRopeTimerFromModel), then "no match clock"
- private string GetTimerFromModel(bool isOpponent) (line 242) — Note: actualRemaining = MtgTimer.RemainingTime - MatchTimer._timeRunning (same formula as MatchTimer.LateUpdate)
- private (string timerText, int timeouts)? GetRopeTimerFromModel(bool isOpponent) (line 280) — Note: similar formula against LowTimeWarning._activeTimer; counts pips from _timeoutPips
- private static void InitializeLtwReflection(MonoBehaviour ltwComponent) (line 332)
- private static void InitializeMtgTimerFromLtw() (line 362) — Note: fallback path for games without MatchTimer (casual Brawl) — reads MtgTimer type from _ltwActiveTimerField.FieldType
- private static void InitializeMtgTimerReflection(MonoBehaviour matchTimerComponent) (line 386)
- private static string FormatSecondsToReadable(float totalSeconds) (line 426) — Note: "X minutes Y seconds" with zero-elision
- private void SubscribeLowTimeWarnings() (line 445) — Note: registers UnityEvent<bool> listeners that announce when rope becomes visible; determines local vs opponent by walking parent transform names
- private void UnsubscribeLowTimeWarnings() (line 513) — Note: removes listeners to prevent stale callbacks across scene changes
