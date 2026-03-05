# AccessibleArenaMod.cs Code Index

## File Overview
Main entry point for the Accessible Arena mod. Initializes all services, registers navigators, and handles global shortcuts and update loop.

## Assembly Attributes
- MelonInfo: "Accessible Arena", version from VersionInfo.Value (line 10)
- MelonGame: "Wizards Of The Coast", "MTGA" (line 11)

## Class: AccessibleArenaMod : MelonMod (line 15)

### Static Properties
- public static AccessibleArenaMod Instance { get; private set; } (line 17)

### Private Fields
- private IAnnouncementService _announcer (line 19)
- private IShortcutRegistry _shortcuts (line 20)
- private IInputHandler _inputHandler (line 21)
- private UIFocusTracker _focusTracker (line 22)
- private CardInfoNavigator _cardInfoNavigator (line 23)
- private NavigatorManager _navigatorManager (line 24)
- private HelpNavigator _helpNavigator (line 25)
- private ModSettingsNavigator _settingsNavigator (line 26)
- private ExtendedInfoNavigator _extendedInfoNavigator (line 27)
- private ModSettings _settings (line 28)
- private PanelAnimationDiagnostic _panelDiagnostic (line 29)
- private PanelStateManager _panelStateManager (line 30)
- private bool _initialized (line 32)
- private string _lastActiveNavigatorId (line 33)

### Public Properties
- public IAnnouncementService Announcer => _announcer (line 35)
- public CardInfoNavigator CardNavigator => _cardInfoNavigator (line 36)
- public ExtendedInfoNavigator ExtendedInfoNavigator => _extendedInfoNavigator (line 37)
- public ModSettings Settings => _settings (line 38)

### Public Methods
- public override void OnInitializeMelon() (line 40)
  // Main initialization - loads screen reader, initializes services, registers shortcuts, applies patches

- public bool ActivateCardDetails(GameObject element) (line 166)
  // Activates card detail navigation for the given element. Returns true if successful.

- public override void OnSceneWasLoaded(int buildIndex, string sceneName) (line 231)
  // Called by MelonLoader on scene change. Clears caches, resets state, notifies navigators.

- public override void OnUpdate() (line 263)
  // Main update loop - handles input priority (help > settings > extended info > card nav > general input)

- public override void OnApplicationQuit() (line 358)
  // Cleanup - saves settings, shuts down screen reader

### Private Methods
- private void InitializeHarmonyPatches() (line 64)
  // Initializes UXEventQueue and PanelState Harmony patches

- private void InitializeServices() (line 77)
  // Creates all service instances, loads settings, initializes localization, registers navigators

- private void HandleFocusChanged(UnityEngine.GameObject oldElement, UnityEngine.GameObject newElement) (line 133)
  // Deactivates card navigator when focus moves away, notifies portrait navigator

- private string GetSafeGameObjectName(UnityEngine.GameObject obj) (line 179)
  // Safely gets GameObject name, handling destroyed Unity objects

- private void RegisterGlobalShortcuts() (line 192)
  // Registers F1 (help), F2 (settings), Ctrl+R (repeat), F3 (current screen)

- private void ToggleHelpMenu() (line 200)
- private void ToggleSettingsMenu() (line 205)
- private void RepeatLastAnnouncement() (line 210)
- private void AnnounceCurrentScreen() (line 218)
