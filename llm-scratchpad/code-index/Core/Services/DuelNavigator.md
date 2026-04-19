# DuelNavigator.cs
Path: src/Core/Services/DuelNavigator.cs
Lines: 910

## Top-level comments
- Navigator for the actual duel/gameplay in DuelScene. Discovers Selectables/CustomButtons/EventTriggers, delegates zone navigation, target/playable cycling, combat, browser, mana picker, choose-X, spinner, portrait, and chat to sub-navigators. Disables SocialUI selectables during duel so EventSystem can't steal focus.

## public class DuelNavigator : BaseNavigator (line 23)
### Fields
- [DllImport("user32.dll")] private static extern bool SetCursorPos(int X, int Y) (line 26)
- private bool _isWatching (line 29)
- private bool _hasCenteredMouse (line 30)
- private bool _wasPreemptedForChat (line 31)
- private float _playerNameAnnounceDelay = -1f (line 32) — Note: one-shot; announces matchup after HUD settles
- private ZoneNavigator _zoneNavigator (line 33)
- private HotHighlightNavigator _hotHighlightNavigator (line 34) — Note: unified navigator for Tab, cards, targets, selection mode
- private CombatNavigator _combatNavigator (line 35)
- private BattlefieldNavigator _battlefieldNavigator (line 36)
- private BrowserNavigator _browserNavigator (line 37)
- private ManaColorPickerNavigator _manaColorPicker (line 38)
- private ChooseXNavigator _chooseXNavigator (line 39)
- private SpinnerNavigator _spinnerNavigator (line 40)
- private PlayerPortraitNavigator _portraitNavigator (line 41)
- private PriorityController _priorityController (line 42)
- private DuelAnnouncer _duelAnnouncer (line 43)
- private DuelChatNavigator _duelChatNavigator (line 44)
- private List<GameObject> _deactivatedSocialObjects = new List<GameObject>() (line 45)
### Properties
- public override string NavigatorId (line 47)
- public override string ScreenName (line 48)
- public override int Priority (line 49) — Note: 70, lower than PreBattle so it activates after
- protected override bool AcceptSpaceKey (line 52) — Note: false; game handles Space natively for Submit/Confirm/discard
- protected override bool SupportsLetterNavigation (line 55) — Note: false; duel uses letter zone shortcuts (C, G, X, S, W)
- public ZoneNavigator ZoneNavigator (line 63)
- public HotHighlightNavigator HotHighlightNavigator (line 64)
- public BattlefieldNavigator BattlefieldNavigator (line 65)
- public BrowserNavigator BrowserNavigator (line 66)
- public DuelAnnouncer DuelAnnouncer (line 67)
- public PlayerPortraitNavigator PortraitNavigator (line 68)
### Methods
- public void MarkPreemptedForChat() (line 58) — Note: next activation announces "Returned to duel"
- public void RestoreSocialUIBeforeChat() (line 61)
- public DuelNavigator(IAnnouncementService announcer) (line 69)
- public void OnDuelSceneLoaded() (line 115) — Note: starts watching, clears announcement history, clears stale EventSystem selection
- protected override void OnActivated() (line 138) — Note: disables SocialUI selectables, centers mouse once via SetCursorPos, schedules matchup announcement
- protected override void OnDeactivating() (line 163)
- public override void Update() (line 169) — Note: handles delayed matchup announcement and NPE tutorial hover simulation
- public override void OnSceneChanged(string sceneName) (line 195)
- protected override bool DetectScreen() (line 215)
- protected override bool ValidateElements() (line 223) — Note: deactivates if settings menu is open
- protected override void UpdateEventSystemSelection() (line 241) — Note: clears EventSystem selection instead of setting it so first Tab doesn't chain to NavArrowNextbutton
- protected override void DiscoverElements() (line 250)
- private void DiscoverSelectables(HashSet<GameObject> addedObjects) (line 288)
- private void DiscoverCustomButtons(HashSet<GameObject> addedObjects) (line 315)
- private void DiscoverEventTriggers(HashSet<GameObject> addedObjects) (line 343) — Note: skips "Stop" timer-control triggers that flood the element list
- private void DiscoverDuelSpecificElements(HashSet<GameObject> addedObjects) (line 373)
- public override string GetTutorialHint() (line 403)
- protected override string GetActivationAnnouncement() (line 406)
- protected override bool OnElementActivated(int index, GameObject element) (line 418) — Note: uses SimulatePointerClick for PromptButton/Styled/CustomButton/StyledButton
- protected override bool HandleEarlyInput() (line 438) — Note: DuelChatNavigator takes full input priority when active
- protected override bool HandleCustomInput() (line 451) — Note: priority: mana picker > choose X > spinner > browser > F4 chat > Shift/Ctrl+Backspace > combat > HotHighlight > portrait > P > number keys > T/I/O/K/E/M > battlefield > zone; consumes Enter/Up/Down to prevent fall-through
- private void OpenDuelChat() (line 696)
- private void OnDuelChatClosed() (line 730) — Note: re-disables SocialUI selectables
- private void DisableSocialUISelectables() (line 745) — Note: SetActive(false) on SocialUI Selectable GOs so EventSystem can't auto-navigate to them during friend-challenge duels
- private void RestoreSocialUISelectables() (line 767)
- private bool HandlePhaseStopKeys() (line 791) — Note: Alpha1-9 -> index 0-8, Alpha0 -> index 9 (Upkeep/Draw/First Main/Begin Combat/Declare Attackers/Declare Blockers/Combat Damage/End Combat/Second Main/End Step)
- private bool HasDuelElements() (line 820) — Note: checks for PromptButton_Primary text and "Stop" EventTriggers to distinguish live gameplay from matchmaking
- private (string roleLabel, UIElementClassifier.ElementRole role) GetSelectableTypeAndRole(Selectable selectable) (line 855)
- private static bool IsSocialUIElement(GameObject obj) (line 876) — Note: walks up 15 ancestors looking for "SocialUI" in name
- private bool HasComponent(GameObject obj, string componentName) (line 889)
- private string CleanName(string name) (line 899)
