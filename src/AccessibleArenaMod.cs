using System.IO;
using MelonLoader;
using UnityEngine;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services;
using AccessibleArena.Core.Services.PanelDetection;
using AccessibleArena.Patches;

[assembly: MelonInfo(typeof(AccessibleArena.AccessibleArenaMod), "Accessible Arena", VersionInfo.Value, "Accessible Arena Team")]
[assembly: MelonGame("Wizards Of The Coast", "MTGA")]

namespace AccessibleArena
{
    public class AccessibleArenaMod : MelonMod
    {
        public static AccessibleArenaMod Instance { get; private set; }

        private IAnnouncementService _announcer;
        private IShortcutRegistry _shortcuts;
        private IInputHandler _inputHandler;
        private UIFocusTracker _focusTracker;
        private CardInfoNavigator _cardInfoNavigator;
        private NavigatorManager _navigatorManager;
        private HelpNavigator _helpNavigator;
        private ModSettingsNavigator _settingsNavigator;
        private ExtendedInfoNavigator _extendedInfoNavigator;
        private ModSettings _settings;
        private PanelAnimationDiagnostic _panelDiagnostic;
        private PanelStateManager _panelStateManager;

        private bool _initialized;
        private string _lastActiveNavigatorId;

        public IAnnouncementService Announcer => _announcer;
        public CardInfoNavigator CardNavigator => _cardInfoNavigator;
        public ExtendedInfoNavigator ExtendedInfoNavigator => _extendedInfoNavigator;
        public ModSettings Settings => _settings;

        public override void OnInitializeMelon()
        {
            Instance = this;
            LoggerInstance.Msg("Accessible Arena initializing...");

            if (!ScreenReaderOutput.Initialize())
            {
                LoggerInstance.Warning("Screen reader not available - mod will run in silent mode");
            }
            else
            {
                LoggerInstance.Msg($"Screen reader detected: {ScreenReaderOutput.GetActiveScreenReader()}");
            }

            InitializeServices();
            RegisterGlobalShortcuts();
            InitializeHarmonyPatches();

            _initialized = true;

            LoggerInstance.Msg("Accessible Arena initialized");
            _announcer.Announce(Strings.ModLoaded(Info.Version), AnnouncementPriority.High);
        }

        private void InitializeHarmonyPatches()
        {
            // Initialize the UXEventQueue patch for duel event announcements
            // This patch intercepts game events and passes them to DuelAnnouncer
            UXEventQueuePatch.Initialize();

            // PanelStatePatch for Harmony-based panel detection (PlayBlade, Settings, etc.)
            // Used alongside UnifiedPanelDetector for hybrid detection
            PanelStatePatch.Initialize();

            LoggerInstance.Msg("Harmony patches initialized");
        }

        private void InitializeServices()
        {
            _announcer = new AnnouncementService();
            _shortcuts = new ShortcutRegistry();
            _inputHandler = new InputManager(_shortcuts, _announcer);
            _focusTracker = new UIFocusTracker(_announcer);
            _cardInfoNavigator = new CardInfoNavigator(_announcer);

            // Load settings first so we know the language
            _settings = ModSettings.Load();

            // Initialize locale system before any Strings.* usage
            LocaleManager.EnsureDefaultLocaleFiles();
            LocaleManager.Initialize(_settings.Language);

            _helpNavigator = new HelpNavigator(_announcer);
            _settingsNavigator = new ModSettingsNavigator(_announcer, _settings);
            _extendedInfoNavigator = new ExtendedInfoNavigator(_announcer);

            // Rebuild help items when language changes
            _settings.OnLanguageChanged += () => _helpNavigator.RebuildItems();
            _panelDiagnostic = new PanelAnimationDiagnostic();

            // Initialize panel state manager (single source of truth for panel state)
            // PanelStateManager now owns all detectors directly (simplified from plugin system)
            _panelStateManager = new PanelStateManager();
            _panelStateManager.Initialize();

            // Initialize navigator manager with all screen navigators
            // LoginPanelNavigator removed - GeneralMenuNavigator now handles Login scene with password masking
            _navigatorManager = new NavigatorManager();
            // WelcomeGateNavigator removed - GeneralMenuNavigator handles Login scene
            _navigatorManager.RegisterAll(
                new AdvancedFiltersNavigator(_announcer), // Advanced Filters popup in Collection/Deck Builder (priority 87)
                new RewardPopupNavigator(_announcer),   // Rewards popup from mail/store (priority 86)
                new OverlayNavigator(_announcer),
                new SettingsMenuNavigator(_announcer),  // Settings menu - works everywhere including duels (priority 90)
                new BoosterOpenNavigator(_announcer),  // Pack opening card list (priority 80)
                new NPERewardNavigator(_announcer),    // NPE reward screen - card unlocked (priority 75)
                // PreBattleNavigator removed - game auto-transitions to duel without needing button click
                new DuelNavigator(_announcer),
                new LoadingScreenNavigator(_announcer),  // MatchEnd/Matchmaking transitional screens (priority 65)
                new MasteryNavigator(_announcer),            // Mastery/Rewards screen - levels and rewards (priority 60)
                new StoreNavigator(_announcer),           // Store screen - tabs and items (priority 55)
                // CodeOfConductNavigator removed - default navigation handles this screen
                new GeneralMenuNavigator(_announcer),
                // EventTriggerNavigator removed - GeneralMenuNavigator now handles NPE screens
                new AssetPrepNavigator(_announcer)  // Download screen - low priority, fails gracefully
            );

            // Subscribe to focus changes for automatic card navigation
            _focusTracker.OnFocusChanged += HandleFocusChanged;
        }

        private void HandleFocusChanged(UnityEngine.GameObject oldElement, UnityEngine.GameObject newElement)
        {
            // Use safe name access - Unity destroyed objects are not null but throw on property access
            string oldName = GetSafeGameObjectName(oldElement);
            string newName = GetSafeGameObjectName(newElement);
            DebugConfig.LogIf(DebugConfig.LogFocusTracking, "FocusChanged", $"Old: {oldName}, New: {newName}");

            // If focus moved away from current card, deactivate card navigation
            // Skip when CurrentCard is null (blocks-only mode, e.g. packet info) - owner manages lifecycle
            if (_cardInfoNavigator.IsActive && _cardInfoNavigator.CurrentCard != null && _cardInfoNavigator.CurrentCard != newElement)
            {
                DebugConfig.LogIf(DebugConfig.LogFocusTracking, "FocusChanged", "Deactivating card navigator");
                _cardInfoNavigator.Deactivate();
            }

            // Notify PlayerPortraitNavigator of focus change so it can exit if needed
            var duelNav = _navigatorManager?.GetNavigator<DuelNavigator>();
            if (duelNav?.PortraitNavigator != null && newElement != null && newElement)
            {
                duelNav.PortraitNavigator.OnFocusChanged(newElement);
            }

            // Note: We no longer call PrepareForCard here because navigators
            // (ZoneNavigator, BattlefieldNavigator, HighlightNavigator) now set EventSystem focus
            // and call PrepareForCard with the correct zone. Calling it here would overwrite
            // the correct zone with the default (Hand).
        }

        /// <summary>
        /// Activates card detail navigation for the given element.
        /// Called by navigators when user presses Enter on a card.
        /// Returns true if the element is a card and details were activated.
        /// </summary>
        public bool ActivateCardDetails(GameObject element)
        {
            if (element == null) return false;

            if (!CardDetector.IsCard(element)) return false;

            return _cardInfoNavigator.ActivateForCard(element);
        }

        /// <summary>
        /// Safely gets a GameObject's name, handling destroyed Unity objects.
        /// Unity destroyed objects are not null but throw on property access.
        /// </summary>
        private string GetSafeGameObjectName(UnityEngine.GameObject obj)
        {
            if (obj == null || !obj) return "null"; // Check both C# null and Unity destroyed
            try
            {
                return obj.name;
            }
            catch
            {
                return "destroyed";
            }
        }

        private void RegisterGlobalShortcuts()
        {
            _shortcuts.RegisterShortcut(KeyCode.F1, ToggleHelpMenu, "Help menu");
            _shortcuts.RegisterShortcut(KeyCode.F2, ToggleSettingsMenu, "Settings menu");
            _shortcuts.RegisterShortcut(KeyCode.R, KeyCode.LeftControl, RepeatLastAnnouncement, "Repeat last announcement");
            _shortcuts.RegisterShortcut(KeyCode.F3, AnnounceCurrentScreen, "Announce current screen");
        }

        private void ToggleHelpMenu()
        {
            _helpNavigator?.Toggle();
        }

        private void ToggleSettingsMenu()
        {
            _settingsNavigator?.Toggle();
        }

        private void RepeatLastAnnouncement()
        {
            if (_announcer is AnnouncementService announcementService)
            {
                announcementService.RepeatLastAnnouncement();
            }
        }

        private void AnnounceCurrentScreen()
        {
            var nav = _navigatorManager?.ActiveNavigator;
            if (nav != null)
            {
                _announcer.AnnounceInterrupt(nav.ScreenName);
            }
            else
            {
                _announcer.AnnounceInterrupt(Strings.NoActiveScreen);
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            LoggerInstance.Msg($"Scene loaded: {sceneName} (index: {buildIndex})");

            // Clear card detection and deck info caches on scene change
            CardDetector.ClearCache();
            DeckInfoProvider.ClearCache();
            RecentPlayAccessor.ClearCache();
            EventAccessor.ClearCache();

            // Deactivate card info navigator on scene change (prevents stale card reading)
            if (_cardInfoNavigator != null && _cardInfoNavigator.IsActive)
            {
                LoggerInstance.Msg("[SceneChange] Deactivating card navigator");
                _cardInfoNavigator.Deactivate();
            }

            // Reset panel state and detectors on scene change
            _panelStateManager?.Reset();

            // Notify navigator manager of scene change
            _navigatorManager?.OnSceneChanged(sceneName);

            // DuelNavigator activates on DuelScene - game auto-transitions to duel
            if (sceneName == "DuelScene")
            {
                var duelNav = _navigatorManager?.GetNavigator<DuelNavigator>();
                duelNav?.OnDuelSceneLoaded();
            }

        }

        public override void OnUpdate()
        {
            if (!_initialized)
                return;

            // F11: Panel animation diagnostic (for development)
            if (Input.GetKeyDown(KeyCode.F11))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    // Shift+F11: One-time dump of panel analysis
                    _panelDiagnostic?.DumpPanelAnalysis();
                }
                else
                {
                    // F11: Toggle animation tracking
                    _panelDiagnostic?.ToggleTracking();
                }
            }

            // Update diagnostic tracking if active
            _panelDiagnostic?.Update();

            // Tell KeyboardManagerPatch to block Escape from reaching the game
            // when a mod menu is open (persistent flag avoids timing issues with per-frame consume)
            InputManager.ModMenuActive = (_helpNavigator?.IsActive == true)
                || (_settingsNavigator?.IsActive == true)
                || (_extendedInfoNavigator?.IsActive == true);

            // Help menu has highest priority - blocks all other input when active
            if (_helpNavigator != null && _helpNavigator.IsActive)
            {
                _helpNavigator.HandleInput();
                return;
            }

            // Settings menu - second highest priority after help
            if (_settingsNavigator != null && _settingsNavigator.IsActive)
            {
                _settingsNavigator.HandleInput();
                return;
            }

            // Extended info menu - third highest priority
            if (_extendedInfoNavigator != null && _extendedInfoNavigator.IsActive)
            {
                _extendedInfoNavigator.HandleInput();
                return;
            }

            // Deactivate card info navigator when active navigator changes (e.g., settings menu preempts duel)
            var currentNavId = _navigatorManager?.ActiveNavigator?.NavigatorId;
            if (currentNavId != _lastActiveNavigatorId)
            {
                if (_cardInfoNavigator != null && _cardInfoNavigator.IsActive)
                {
                    DebugConfig.LogIf(DebugConfig.LogFocusTracking, "NavigatorChange", $"Deactivating card navigator (navigator changed: {_lastActiveNavigatorId} -> {currentNavId})");
                    _cardInfoNavigator.Deactivate();
                }
                _lastActiveNavigatorId = currentNavId;
            }

            // Card navigation handles arrow keys when active, but allows other input through
            if (_cardInfoNavigator != null && _cardInfoNavigator.IsActive)
            {
                if (_cardInfoNavigator.HandleInput())
                {
                    // Input was handled by card navigator (arrow keys)
                    return;
                }
                // Input not handled (e.g., Tab) - continue to let other handlers process
            }
            else if (_cardInfoNavigator != null &&
                     (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.UpArrow) ||
                      UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow)))
            {
                DebugConfig.LogIf(DebugConfig.LogFocusTracking, "CardInfo",
                    $"Up/Down pressed but CardInfoNavigator.IsActive={_cardInfoNavigator.IsActive}, CurrentCard={(_cardInfoNavigator.CurrentCard != null ? _cardInfoNavigator.CurrentCard.name : "null")}");
            }

            _inputHandler?.OnUpdate();

            // When a navigator is active, it handles announcements - UIFocusTracker stays silent
            UIFocusTracker.NavigatorHandlesAnnouncements = _navigatorManager?.HasActiveNavigator ?? false;

            // Always track focus changes for card navigation
            _focusTracker?.Update();

            // Update panel state manager (handles all detector updates)
            _panelStateManager?.Update();

            // NavigatorManager handles all screen navigators
            _navigatorManager?.Update();
        }

        public override void OnApplicationQuit()
        {
            LoggerInstance.Msg("Accessible Arena shutting down...");
            _settings?.Save();
            ScreenReaderOutput.Shutdown();
        }

    }
}
