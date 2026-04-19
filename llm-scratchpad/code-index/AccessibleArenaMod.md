# AccessibleArenaMod.cs
Path: src/AccessibleArenaMod.cs
Lines: 442

## Top-level comments
- No file header. Assembly attributes declare `MelonInfo` and `MelonGame("Wizards Of The Coast", "MTGA")`.

## class AccessibleArenaMod (line 16)
Inherits `MelonMod`. Mod entry point; wires services, panel state, navigators, and Harmony patches.

### Fields
- private IAnnouncementService _announcer (line 20)
- private IShortcutRegistry _shortcuts (line 21)
- private IInputHandler _inputHandler (line 22)
- private UIFocusTracker _focusTracker (line 23)
- private CardInfoNavigator _cardInfoNavigator (line 24)
- private NavigatorManager _navigatorManager (line 25)
- private HelpNavigator _helpNavigator (line 26)
- private ModSettingsNavigator _settingsNavigator (line 27)
- private ExtendedInfoNavigator _extendedInfoNavigator (line 28)
- private GameLogNavigator _gameLogNavigator (line 29)
- private ModSettings _settings (line 30)
- private PanelStateManager _panelStateManager (line 31)
- private ChatMessageWatcher _chatMessageWatcher (line 32)
- private bool _initialized (line 34)
- private string _lastActiveNavigatorId (line 35)

### Properties
- public static AccessibleArenaMod Instance { get; private set; } (line 18)
- public IAnnouncementService Announcer (line 37)
- public CardInfoNavigator CardNavigator (line 38)
- public ExtendedInfoNavigator ExtendedInfoNavigator (line 39)
- public GameLogNavigator GameLogNavigator (line 40)
- public ModSettings Settings (line 41)

### Methods
- public override void OnInitializeMelon() (line 43)
- private void InitializeHarmonyPatches() (line 74)
- private void InitializeServices() (line 96)
- private void HandleFocusChanged(GameObject oldElement, GameObject newElement) (line 153) — Note: deliberately ignores null-newElement (treated as intermediate clearing, not "user left card"); also notifies DuelNavigator's PortraitNavigator.
- public bool ActivateCardDetails(GameObject element) (line 189)
- private string GetSafeGameObjectName(GameObject obj) (line 202) — Note: handles Unity "destroyed-but-not-null" sentinels via try/catch.
- private void RegisterGlobalShortcuts() (line 215)
- private void ToggleHelpMenu() (line 226)
- private void ToggleSettingsMenu() (line 231)
- private void RepeatLastAnnouncement() (line 236)
- private void AnnounceCurrentScreen() (line 241)
- private void AnnounceTutorialHint() (line 254)
- private void HandleUpdateShortcut() (line 263) — Note: guarded — F5 only fires in loading/general-menu/asset-prep screens or when no navigator is active.
- private void SpeakDebugLog() (line 276)
- public override void OnSceneWasLoaded(int buildIndex, string sceneName) (line 292) — Note: clears 5 caches, deactivates CardInfoNavigator, resets panel state, dispatches to DuelNavigator/SideboardNavigator on DuelScene.
- public override void OnUpdate() (line 331) — Note: 4-layer modal-priority gate (Help → Settings → ExtendedInfo → GameLog) before screen navigator runs; also polls PhaseSkipGuard every frame.
- public override void OnApplicationQuit() (line 433)
