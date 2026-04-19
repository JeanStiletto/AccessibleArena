# ChallengeNavigationHelper.cs - Code Index

## File-level Comment
Centralized helper for Challenge screen navigation (Direct Challenge / Friend Challenge).
Handles two-level navigation: ChallengeMain (spinners + buttons) and Deck Selection (folders).
GeneralMenuNavigator calls this and acts on the PlayBladeResult.

## Classes

### ChallengeNavigationHelper (line 16)
```csharp
public class ChallengeNavigationHelper
```

#### Fields - Dependencies
- private readonly GroupedNavigator _groupedNavigator (line 18)
- private readonly IAnnouncementService _announcer (line 19)

#### Fields - Cached Reflection
- private static Type _challengeDisplayType (line 22)
- private static Type _playerDisplayType (line 23)
- private static Type _playBladeControllerType (line 24)
- private static Type _bladeWidgetType (line 25)
- private static MethodInfo _hideDeckSelectorMethod (line 26)
- private static FieldInfo _deckSelectorField (line 27)
- private static FieldInfo _localPlayerField, _enemyPlayerField, _playerNameField, _noPlayerField, _playerInvitedField (lines 29-32)
- private static PropertyInfo _playerIdProp (line 34)
- private static FieldInfo _challengeStatusTextField, _isChallengeSettingsLockedField (lines 37-38)
- private static PropertyInfo _isChallengeSettingsLockedProp (line 39)
- private static bool _reflectionInitialized (line 40)

#### Fields - Polling State
- private enum EnemyState (line 43) - NotInvited, Invited, Joined
- private EnemyState _lastEnemyState (line 44)
- private string _lastEnemyName (line 45)
- private string _lastStatusText (line 46)
- private bool _wasCountdownActive (line 47)
- private float _pollTimer (line 48)
- private const float PollIntervalSeconds = 1.0f (line 49)
- private bool _pollingInitialized (line 50)

#### Properties
- public bool IsActive (line 56) - Whether currently in a challenge context

#### Constructor
- public ChallengeNavigationHelper(GroupedNavigator groupedNavigator, IAnnouncementService announcer) (line 58)

#### Methods - Navigation Handling
- public PlayBladeResult HandleEnter(GameObject element, ElementGroup elementGroup) (line 71)
  - Handle Enter key press on an element (called BEFORE UIActivator.Activate)
- public PlayBladeResult HandleBackspace() (line 92)
  - Handle Backspace key press (Navigation: ChallengeMain -> close, Folders -> ChallengeMain)
- public void OnChallengeOpened() (line 161)
  - Called when challenge screen opens, sets context and requests ChallengeMain entry
- public void OnChallengeClosed() (line 177)
  - Called when challenge screen closes, clears the challenge context and polling state
- public void HandleDeckSelected() (line 188)
  - Called when a deck is selected in the challenge deck picker
- private static bool IsDeckSelectionButton(GameObject element) (line 202)
  - Check if element is "Select Deck" or "NoDeck" button
- private static bool IsContextDisplayDeck(GameObject element) (line 220)
  - Check if element is the deck display inside ContextDisplay
- public void Reset() (line 239)

#### Methods - Label Enhancement
- public string EnhanceButtonLabel(GameObject element, string label) (line 250)
  - Enhance button label for challenge screen elements
- private static bool IsSpinnerElement(GameObject element) (line 282)
  - Check if element is a spinner/stepper

#### Methods - Challenge Status Text
- public string GetChallengeStatusText() (line 310)
  - Get status text from _challengeStatusText on UnifiedChallengeBladeWidget

#### Methods - Settings Lock Detection
- public bool IsSettingsLocked() (line 340)
  - Check if challenge settings are locked (joining someone else's challenge)

#### Methods - Tournament Parameters
- public string GetTournamentParametersSummary() (line 371)
  - Get summary of tournament parameters when in tournament mode

#### Methods - Polling / Update
- public void Update(float deltaTime) (line 414)
  - Poll for player status changes (call from GeneralMenuNavigator.Update())
- private void InitializePollingState() (line 435)
  - Initialize polling state silently (read current state without announcing)
- private void PollPlayerStatusChanges() (line 469)
  - Internal polling implementation
- private EnemyState GetEnemyState(object enemyDisplay) (line 540)
- private string GetEnemyName(object enemyDisplay) (line 555)
- private static bool IsCountdownText(string text) (line 569)
  - Detect if status text indicates a countdown is active

#### Methods - Player Status
- public string GetLocalPlayerName() (line 586)
  - Get the local player's display name (stripped of rich text tags)
- public static void CloseDeckSelectBlade() (line 616)
  - Close the DeckSelectBlade via PlayBladeController.HideDeckSelector()
- public string GetPlayerStatusSummary() (line 679)
  - Get summary of player status for the challenge screen announcement
- private static void InitReflection() (line 720)
  - Initialize reflection info for player status
- private static UnityEngine.Object FindChallengeDisplay() (line 774)
  - Find the active UnifiedChallengeDisplay in the scene
- private static UnityEngine.Object FindBladeWidget() (line 792)
  - Find the active UnifiedChallengeBladeWidget in the scene
- private static string GetPlayerInfo(object playerDisplay, bool isLocal) (line 806)
  - Get player info from a player display object
- private static string StripRichTextTags(string text) (line 864)
  - Remove rich text tags from player name
