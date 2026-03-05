# PlayerPortraitNavigator.cs Code Index

## Summary
Navigator for player portrait/timer interactions during duels. Provides V key zone for player info, property cycling, and emotes.

## Classes

### class PlayerPortraitNavigator (line 16)
```
private readonly IAnnouncementService _announcer (line 18)
private bool _isActive (line 21)

private enum NavigationState { Inactive, PlayerNavigation, EmoteNavigation } (line 24)
private NavigationState _navigationState (line 25)
private int _currentPlayerIndex (line 26)
private int _currentPropertyIndex (line 27)

private enum PlayerProperty { Life, Timer, Timeouts, Wins, Rank } (line 30)
private const int PropertyCount = 5 (line 31)

private System.Collections.Generic.List<GameObject> _emoteButtons (line 34)
private int _currentEmoteIndex (line 35)

private GameObject _localTimerObj (line 38)
private GameObject _opponentTimerObj (line 39)
private MonoBehaviour _localMatchTimer (line 40)
private MonoBehaviour _opponentMatchTimer (line 41)

private static readonly BindingFlags PrivateInstance (line 44)
private static readonly BindingFlags PublicInstance (line 46)
private static System.Type _avatarViewType (line 48)
private static PropertyInfo _isLocalPlayerProp (line 49)
private static FieldInfo _portraitButtonField (line 50)
private static bool _avatarReflectionInitialized (line 51)

private static PropertyInfo _matchManagerProp (line 54)
private static PropertyInfo _localPlayerInfoProp (line 55)
private static PropertyInfo _opponentInfoProp (line 56)
private static FieldInfo _rankingClassField (line 57)
private static FieldInfo _rankingTierField (line 58)
private static FieldInfo _mythicPercentileField (line 59)
private static FieldInfo _mythicPlacementField (line 60)
private static bool _rankReflectionInitialized (line 61)

private GameObject _previousFocus (line 64)
private GameObject _playerZoneFocusElement (line 65)

public PlayerPortraitNavigator(IAnnouncementService announcer) (line 67)
public void Activate() (line 73)
public void Deactivate() (line 79)
public bool IsInPlayerInfoZone => _navigationState != NavigationState.Inactive (line 95)
public void OnFocusChanged(GameObject newFocus) (line 102)
  // NOTE: Auto-exit player info zone when focus moves elsewhere
private bool IsPlayerZoneElement(GameObject obj) (line 118)
public bool HandleInput() (line 153)
private void EnterPlayerInfoZone() (line 190)
public void ExitPlayerInfoZone() (line 219)
private GameObject FindPlayerZoneFocusElement() (line 241)
private bool HandlePlayerNavigation() (line 268)
private bool HandleEmoteNavigation() (line 386)
private string GetPropertyValue(PlayerProperty property) (line 437)
private int GetWinCount(bool isOpponent) (line 481)
private string GetPlayerRank(bool isOpponent) (line 493)
  // NOTE: Gets rank from GameManager.MatchManager player info
private string GetPlayerUsername(bool isOpponent) (line 570)
private void OpenEmoteWheel() (line 620)
private void CloseEmoteWheel() (line 644)
private void DiscoverEmoteButtons() (line 656)
private void SearchForEmoteButtons(Transform parent, int depth) (line 703)
  // NOTE: Recursively searches for emote buttons in transform hierarchy
private string ExtractEmoteNameFromTransform(Transform t) (line 745)
private void AnnounceCurrentEmote() (line 761)
private string ExtractEmoteName(GameObject emoteObj) (line 773)
private void SelectCurrentEmote() (line 799)
private void DiscoverTimerElements() (line 825)
private void AnnounceLifeTotals() (line 862)
private (int localLife, int opponentLife) GetLifeTotals() (line 891)
  // NOTE: Gets life totals from GameManager's game state
private int GetPlayerLife(object player) (line 978)
  // NOTE: Extracts life total from an MtgPlayer object
private string GetPlayerInfo(GameObject timerObj, MonoBehaviour matchTimer, string playerLabel) (line 1054)
private string GetTimerText(GameObject timerObj) (line 1092)
private string FormatTimerText(string timerText) (line 1118)
private int GetTimeoutCount(string playerType) (line 1150)
private string GetMatchTimerInfo(MonoBehaviour matchTimer) (line 1174)
private T GetProperty<T>(System.Type type, object obj, string propName) (line 1194)
private static void InitializeAvatarReflection(System.Type avatarType) (line 1212)
  // NOTE: Initializes reflection cache for DuelScene_AvatarView fields
private MonoBehaviour FindAvatarView(bool isLocal) (line 1244)
private static void InitializeRankReflection(object gameManager) (line 1265)
  // NOTE: Initializes reflection cache for rank data from MatchManager
public void TriggerEmoteMenu(bool opponent = false) (line 1342)
  // NOTE: Clicks the local player's PortraitButton to open/close emote wheel
```
