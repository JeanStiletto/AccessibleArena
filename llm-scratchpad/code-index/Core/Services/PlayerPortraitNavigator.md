# PlayerPortraitNavigator.cs
Path: src/Core/Services/PlayerPortraitNavigator.cs
Lines: 2151

## Top-level comments
- Navigator for player portrait/timer interactions during duels. Provides V key zone for player info, property cycling, and emotes.

## public class PlayerPortraitNavigator (line 22)
### Nested types
- private enum NavigationState (line 28) — nested in PlayerPortraitNavigator. Members: Inactive, PlayerNavigation, EmoteNavigation
- private enum PlayerProperty (line 34) — nested in PlayerPortraitNavigator. Members: Life, Effects, Timer, Timeouts, Wins, Rank

### Fields
- private readonly IAnnouncementService _announcer (line 24)
- private bool _isActive (line 25)
- private NavigationState _navigationState (line 29)
- private int _currentPlayerIndex (line 30)
- private int _currentPropertyIndex (line 31)
- private const int PropertyCount = 6 (line 35)
- private System.Collections.Generic.List<GameObject> _emoteButtons (line 38)
- private int _currentEmoteIndex (line 39)
- private GameObject _localTimerObj (line 42)
- private GameObject _opponentTimerObj (line 43)
- private MonoBehaviour _localMatchTimer (line 44)
- private MonoBehaviour _opponentMatchTimer (line 45)
- private MonoBehaviour _localLowTimeWarning (line 48)
- private MonoBehaviour _opponentLowTimeWarning (line 49)
- private UnityAction<bool> _localRopeCallback (line 50)
- private UnityAction<bool> _opponentRopeCallback (line 51)
- private static FieldInfo _matchTimerField (line 54)
- private static FieldInfo _timeRunningField (line 55)
- private static PropertyInfo _remainingTimeProp (line 56)
- private static FieldInfo _runningField (line 57)
- private static bool _mtgTimerReflectionInitialized (line 58)
- private static FieldInfo _ltwActiveTimerField (line 61)
- private static FieldInfo _ltwTimeRunningField (line 62)
- private static FieldInfo _ltwTimeoutPipsField (line 63)
- private static FieldInfo _ltwIsVisibleField (line 64)
- private static bool _ltwReflectionInitialized (line 65)
- private static System.Type _avatarViewType (line 68)
- private static PropertyInfo _isLocalPlayerProp (line 69)
- private static FieldInfo _portraitButtonField (line 70)
- private static bool _avatarReflectionInitialized (line 71)
- private static PropertyInfo _matchManagerProp (line 74)
- private static PropertyInfo _localPlayerInfoProp (line 75)
- private static PropertyInfo _opponentInfoProp (line 76)
- private static FieldInfo _rankingClassField (line 77)
- private static FieldInfo _rankingTierField (line 78)
- private static FieldInfo _mythicPercentileField (line 79)
- private static FieldInfo _mythicPlacementField (line 80)
- private static bool _rankReflectionInitialized (line 81)
- private static FieldInfo _countersField (line 84)
- private static FieldInfo _designationsField (line 85)
- private static FieldInfo _abilitiesField (line 86)
- private static FieldInfo _dungeonStateField (line 87)
- private static bool _entityReflectionInitialized (line 88)
- private GameObject _previousFocus (line 91)
- private GameObject _playerZoneFocusElement (line 92)

### Properties
- public bool IsInPlayerInfoZone { get; } (line 126) — expression-bodied: returns `_navigationState != NavigationState.Inactive`

### Methods
- public PlayerPortraitNavigator(IAnnouncementService announcer) (line 94)
- public void Activate() (line 100)
- public void Deactivate() (line 108)
- public void OnFocusChanged(GameObject newFocus) (line 133) — Note: auto-exits player info zone if new focus is outside zone
- private bool IsPlayerZoneElement(GameObject obj) (line 149)
- public bool HandleInput() (line 184) — Note: entry point for V/L keys and delegates to state-specific handlers
- private void EnterPlayerInfoZone() (line 221) — Note: stores previous focus and sets focus to player zone element
- public void ExitPlayerInfoZone() (line 250) — Note: restores previous focus
- private GameObject FindPlayerZoneFocusElement() (line 272)
- private bool HandlePlayerNavigation() (line 299) — Note: auto-exits zone if focus moved off-zone mid-frame
- private bool HandleEmoteNavigation() (line 403) — Note: returns true for ALL keys while emote menu is open (modal)
- private string GetPropertyValue(PlayerProperty property) (line 454)
- private bool IsPropertyVisible(PlayerProperty property) (line 505)
- private bool HasEffectsContent(bool isOpponent) (line 539)
- private int FindNextVisibleProperty(int currentIndex, bool forward) (line 604)
- private int GetWinCount(bool isOpponent) (line 622) — Note: currently always returns 0 (Bo3 win detection not implemented)
- private string GetPlayerRank(bool isOpponent) (line 633)
- public string GetMatchupText() (line 711)
- private string GetPlayerUsername(bool isOpponent) (line 723) — Note: emits verbose debug logs for each NameView found
- private void OpenEmoteWheel() (line 773) — Note: triggers emote menu UI and switches state to EmoteNavigation
- private void CloseEmoteWheel() (line 797)
- private void DiscoverEmoteButtons() (line 809) — Note: sorts discovered emote buttons alphabetically by name
- private void SearchForEmoteButtons(Transform parent, int depth) (line 856) — Note: recursion capped at depth 5
- private string ExtractEmoteNameFromTransform(Transform t) (line 898)
- private void AnnounceCurrentEmote() (line 914)
- private string ExtractEmoteName(GameObject emoteObj) (line 926)
- private void SelectCurrentEmote() (line 952) — Note: also returns state to PlayerNavigation
- private void DiscoverTimerElements() (line 978)
- private void AnnounceLifeTotals() (line 1015) — Note: uses High priority to bypass duplicate suppression on repeated presses
- private string BuildLifeWithCounters(int life, bool isOpponent) (line 1048)
- private (int localLife, int opponentLife) GetLifeTotals() (line 1067)
- private int GetPlayerLife(object player) (line 1154) — Note: falls back to dumping all player properties/fields to debug log if life not found
- private object GetMtgPlayer(bool isOpponent) (line 1234)
- private static void InitializeEntityReflection(object player) (line 1269) — Note: walks base class hierarchy to find fields on MtgEntity
- private List<(string typeName, int count)> GetPlayerCounters(object player) (line 1308)
- private static string FormatCountersForLife(List<(string typeName, int count)> counters) (line 1354)
- private string GetPlayerEffects(bool isOpponent) (line 1369)
- private static string FormatDesignation(string typeName, object desig, System.Type desigType) (line 1505) — Note: returns null for card-level designations to filter them out
- private string GetTimerText(GameObject timerObj) (line 1532)
- private string FormatTimerText(string timerText) (line 1558)
- private int GetTimeoutCount(string playerType) (line 1590)
- private string GetMatchTimerInfo(MonoBehaviour matchTimer) (line 1614)
- private T GetProperty<T>(System.Type type, object obj, string propName) (line 1634) — Note: generic helper; swallows exceptions, returns default
- public void AnnounceTimer(bool opponent) (line 1653) — Note: match-clock first, falls back to rope timer, then "no match clock"
- private string GetTimerFromModel(bool isOpponent) (line 1692)
- private (string timerText, int timeouts)? GetRopeTimerFromModel(bool isOpponent) (line 1730)
- private static void InitializeLtwReflection(MonoBehaviour ltwComponent) (line 1782)
- private static void InitializeMtgTimerFromLtw() (line 1812) — Note: fallback path for games without MatchTimer (casual Brawl)
- private static void InitializeMtgTimerReflection(MonoBehaviour matchTimerComponent) (line 1836)
- private static string FormatSecondsToReadable(float totalSeconds) (line 1876)
- private void SubscribeLowTimeWarnings() (line 1895) — Note: registers UnityEvent listeners that announce when rope becomes visible
- private void UnsubscribeLowTimeWarnings() (line 1963)
- private static void InitializeAvatarReflection(System.Type avatarType) (line 1993)
- private MonoBehaviour FindAvatarView(bool isLocal) (line 2025)
- private static void InitializeRankReflection(object gameManager) (line 2046) — Note: falls back to logging all fields/properties if RankingClass field is missing
- public void TriggerEmoteMenu(bool opponent = false) (line 2123) — Note: simulates pointer click on PortraitButton
