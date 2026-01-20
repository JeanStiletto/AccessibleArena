using MelonLoader;
using UnityEngine;
using MTGAAccessibility.Core.Interfaces;
using MTGAAccessibility.Core.Models;
using MTGAAccessibility.Core.Services;
using MTGAAccessibility.Contexts.Login;
using MTGAAccessibility.Contexts.MainMenu;
using MTGAAccessibility.Patches;

[assembly: MelonInfo(typeof(MTGAAccessibility.MTGAAccessibilityMod), "MTGA Accessibility", "0.1.0", "Accessibility Mod Team")]
[assembly: MelonGame("Wizards Of The Coast", "MTGA")]

namespace MTGAAccessibility
{
    public class MTGAAccessibilityMod : MelonMod
    {
        public static MTGAAccessibilityMod Instance { get; private set; }

        private IAnnouncementService _announcer;
        private IContextManager _contextManager;
        private IShortcutRegistry _shortcuts;
        private IInputHandler _inputHandler;
        private UIFocusTracker _focusTracker;
        private CardInfoNavigator _cardInfoNavigator;
        private NavigatorManager _navigatorManager;
        private HelpNavigator _helpNavigator;

        private bool _initialized;

        public IAnnouncementService Announcer => _announcer;
        public CardInfoNavigator CardNavigator => _cardInfoNavigator;

        public override void OnInitializeMelon()
        {
            Instance = this;
            LoggerInstance.Msg("MTGA Accessibility Mod initializing...");

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
            RegisterContexts();
            InitializeHarmonyPatches();

            _initialized = true;

            LoggerInstance.Msg("MTGA Accessibility Mod initialized");
            _announcer.Announce(Strings.ModLoaded, AnnouncementPriority.High);
        }

        private void InitializeHarmonyPatches()
        {
            // Initialize the UXEventQueue patch for duel event announcements
            // This patch intercepts game events and passes them to DuelAnnouncer
            UXEventQueuePatch.Initialize();

            // Initialize the PanelState patch for menu navigation
            // This patch intercepts panel Show/Hide to trigger navigator rescans
            PanelStatePatch.Initialize();

            LoggerInstance.Msg("Harmony patches initialized");
        }

        private void InitializeServices()
        {
            _announcer = new AnnouncementService();
            _contextManager = new ContextManager();
            _shortcuts = new ShortcutRegistry(_contextManager);
            _inputHandler = new InputManager(_shortcuts, _contextManager, _announcer);
            _focusTracker = new UIFocusTracker(_announcer);
            _cardInfoNavigator = new CardInfoNavigator(_announcer);
            _helpNavigator = new HelpNavigator(_announcer);

            // Initialize navigator manager with all screen navigators
            // LoginPanelNavigator removed - GeneralMenuNavigator now handles Login scene with password masking
            _navigatorManager = new NavigatorManager();
            // WelcomeGateNavigator removed - GeneralMenuNavigator handles Login scene
            _navigatorManager.RegisterAll(
                new OverlayNavigator(_announcer),
                // PreBattleNavigator removed - game auto-transitions to duel without needing button click
                new DuelNavigator(_announcer),
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
            LoggerInstance.Msg($"[FocusChanged] Old: {oldName}, New: {newName}");

            // If focus moved away from current card, deactivate card navigation
            if (_cardInfoNavigator.IsActive && _cardInfoNavigator.CurrentCard != newElement)
            {
                LoggerInstance.Msg("[FocusChanged] Deactivating card navigator");
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
            _shortcuts.RegisterShortcut(KeyCode.R, KeyCode.LeftControl, RepeatLastAnnouncement, "Repeat last announcement");
            _shortcuts.RegisterShortcut(KeyCode.F2, AnnounceCurrentContext, "Announce current screen");
        }

        private void ToggleHelpMenu()
        {
            _helpNavigator?.Toggle();
        }

        private void RegisterContexts()
        {
            var loginContext = new LoginContext(_announcer, _contextManager);
            var mainMenuContext = new MainMenuContext(_announcer, _contextManager, _shortcuts);

            _contextManager.RegisterContext(GameContext.Login, loginContext);
            _contextManager.RegisterContext(GameContext.MainMenu, mainMenuContext);

            LoggerInstance.Msg("Contexts registered: Login, MainMenu");
        }

        private void RepeatLastAnnouncement()
        {
            if (_announcer is AnnouncementService announcementService)
            {
                announcementService.RepeatLastAnnouncement();
            }
        }

        private void AnnounceCurrentContext()
        {
            var context = _contextManager.ActiveContext;
            if (context != null)
            {
                var position = context.CurrentIndex >= 0
                    ? $"Item {context.CurrentIndex + 1} of {context.ItemCount}"
                    : "No items";
                _announcer.AnnounceInterrupt($"{context.ContextName}. {position}");
            }
            else
            {
                _announcer.AnnounceInterrupt($"Current screen: {_contextManager.CurrentGameContext}");
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            LoggerInstance.Msg($"Scene loaded: {sceneName} (index: {buildIndex})");

            // Clear card detection cache on scene change
            CardDetector.ClearCache();

            // Notify navigator manager of scene change
            _navigatorManager?.OnSceneChanged(sceneName);

            // DuelNavigator activates on DuelScene - game auto-transitions to duel
            if (sceneName == "DuelScene")
            {
                var duelNav = _navigatorManager?.GetNavigator<DuelNavigator>();
                duelNav?.OnDuelSceneLoaded();
            }

            var detectedContext = DetectContextFromScene(sceneName);
            if (detectedContext != GameContext.Unknown)
            {
                _contextManager.SetContext(detectedContext);
                LoggerInstance.Msg($"Context set to: {detectedContext}");
            }
        }

        private GameContext DetectContextFromScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return GameContext.Unknown;

            var lowerName = sceneName.ToLowerInvariant();

            if (lowerName.Contains("login") || lowerName.Contains("auth"))
                return GameContext.Login;

            if (lowerName.Contains("home") || lowerName.Contains("main") || lowerName.Contains("frontend"))
                return GameContext.MainMenu;

            if (lowerName == "pregamescene")
                return GameContext.PreGame;

            if (lowerName == "duelscene")
                return GameContext.Duel;

            if (lowerName.Contains("deck"))
                return GameContext.DeckBuilder;

            if (lowerName.Contains("store") || lowerName.Contains("shop"))
                return GameContext.Shop;

            if (lowerName.Contains("draft"))
                return GameContext.Draft;

            if (lowerName.Contains("loading"))
                return GameContext.Loading;

            return GameContext.Unknown;
        }

        public override void OnUpdate()
        {
            if (!_initialized)
                return;

            // Help menu has highest priority - blocks all other input when active
            if (_helpNavigator != null && _helpNavigator.IsActive)
            {
                _helpNavigator.HandleInput();
                return;
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

            _inputHandler?.OnUpdate();

            // Always track focus changes for card navigation
            _focusTracker?.Update();

            // NavigatorManager handles all screen navigators
            _navigatorManager?.Update();
        }

        public override void OnApplicationQuit()
        {
            LoggerInstance.Msg("MTGA Accessibility Mod shutting down...");
            ScreenReaderOutput.Shutdown();
        }
    }
}
