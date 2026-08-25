using System.IO;
using MelonLoader;
using AccessibleArena.Core.Utils;
using UnityEngine;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services;
using AccessibleArena.Core.Services.PanelDetection;
using AccessibleArena.Patches;
using static AccessibleArena.Core.Constants.SceneNames;

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
        private GameLogNavigator _gameLogNavigator;
        private ModSettings _settings;
        private PanelStateManager _panelStateManager;
        private SocialNotificationWatcher _socialNotificationWatcher;
        private EmoteAnnouncer _emoteAnnouncer;

        private bool _initialized;
        private string _lastActiveNavigatorId;

        public IAnnouncementService Announcer => _announcer;
        public CardInfoNavigator CardNavigator => _cardInfoNavigator;
        public ExtendedInfoNavigator ExtendedInfoNavigator => _extendedInfoNavigator;
        public GameLogNavigator GameLogNavigator => _gameLogNavigator;
        public ModSettings Settings => _settings;

        public override void OnInitializeMelon()
        {
            Instance = this;
            LoggerInstance.Msg("Accessible Arena initializing...");

            // Settings pick the speech backend and the urgent-channel volume, so they have to
            // be on disk-loaded before Prism comes up.
            _settings = ModSettings.Load();

            if (!ScreenReaderOutput.Initialize(_settings.SpeechBackend, _settings.UrgentSpeechVolume))
            {
                LoggerInstance.Warning("No speech backend available - mod will run in silent mode");
            }
            else
            {
                LoggerInstance.Msg($"Speech backend: {ScreenReaderOutput.GetActiveScreenReader()}");
            }

            InitializeServices();
            RegisterGlobalShortcuts();
            InitializeHarmonyPatches();
            SteamOverlayBlocker.Install();

            _initialized = true;

            LoggerInstance.Msg("Accessible Arena initialized");
            _announcer.Announce(Strings.ModLoaded(Info.Version), AnnouncementPriority.High);

            if (_settings.CheckForUpdates)
                UpdateChecker.CheckInBackground(Info.Version);

            if (SteamOverlayBlocker.IsSteam && !SteamOverlayBlocker.OverlayDisabled)
                _announcer.Announce(Strings.SteamOverlayWarning, AnnouncementPriority.Critical);
        }

        private void InitializeHarmonyPatches()
        {
            // Initialize the UXEventQueue patch for duel event announcements
            // This patch intercepts game events and passes them to DuelAnnouncer
            UXEventQueuePatch.Initialize();

            // PanelStatePatch for Harmony-based panel detection (PlayBlade, Settings, etc.)
            // Used alongside alpha-based popup detection for hybrid detection
            PanelStatePatch.Initialize();

            // EventSystemPatch runtime patches (NewInputHandler.OnAccept)
            // Attribute-based patches are auto-applied by MelonLoader, but game types
            // in Core.dll need runtime patching via FindType + harmony.Patch.
            var harmony = new HarmonyLib.Harmony("com.accessibility.mtga.eventsystempatch");
            EventSystemPatch.ApplyRuntimePatches(harmony);

            // TimerPatch for intercepting timeout notifications (timeout used events)
            TimerPatch.Initialize();

            // AudioLogPatch logs music state/RTPC changes and warns when two music
            // layers play at once (diagnoses simulated-hover music leaks)
            AudioLogPatch.Initialize();

            LoggerInstance.Msg("Harmony patches initialized");
        }

        private void InitializeServices()
        {
            _announcer = new AnnouncementService();
            _shortcuts = new ShortcutRegistry();
            _inputHandler = new InputManager(_shortcuts, _announcer);
            _focusTracker = new UIFocusTracker(_announcer);
            _cardInfoNavigator = new CardInfoNavigator(_announcer);

            // Settings are already loaded in OnInitializeMelon (the speech backend choice needs
            // them before this point); guard anyway so the service order stays safe to change.
            if (_settings == null)
                _settings = ModSettings.Load();

            // Initialize locale system before any Strings.* usage
            LocaleManager.EnsureDefaultLocaleFiles();
            LocaleManager.Initialize(_settings.Language);

            // Keybind overrides live in their own file (UserData/AccessibleArenaKeybinds.json)
            // and must be loaded before RegisterGlobalShortcuts and before any navigator input.
            Keybinds.Load();
            Keybinds.Changed += RegisterGlobalShortcuts;

            _helpNavigator = new HelpNavigator(_announcer);
            _settingsNavigator = new ModSettingsNavigator(_announcer, _settings);
            _extendedInfoNavigator = new ExtendedInfoNavigator(_announcer);
            _gameLogNavigator = new GameLogNavigator(_announcer);

            // Rebuild help items when language changes
            _settings.OnLanguageChanged += () => _helpNavigator.RebuildItems();
            // Initialize panel state manager (single source of truth for panel state)
            // PanelStateManager now owns all detectors directly (simplified from plugin system)
            _panelStateManager = new PanelStateManager();
            _panelStateManager.Initialize();

            // Initialize navigator manager with all screen navigators
            _navigatorManager = new NavigatorManager();
            _navigatorManager.RegisterAll(
                new AdvancedFiltersNavigator(_announcer), // Advanced Filters popup in Collection/Deck Builder (priority 87)
                new RewardPopupNavigator(_announcer),   // Rewards popup from mail/store (priority 86)
                new OverlayNavigator(_announcer),
                new SettingsMenuNavigator(_announcer),  // Settings menu - works everywhere including duels (priority 90)
                new BoosterOpenNavigator(_announcer),  // Pack opening card list (priority 80)
                new DraftNavigator(_announcer),         // Draft card picking (priority 78)
                new NPERewardNavigator(_announcer),    // NPE reward screen - card unlocked (priority 75)
                new SideboardNavigator(_announcer),  // Bo3 sideboard (priority 72)
                new DuelNavigator(_announcer),
                new LoadingScreenNavigator(_announcer),  // MatchEnd/Matchmaking transitional screens (priority 65)
                new MasteryNavigator(_announcer),            // Mastery/Rewards screen - levels and rewards (priority 60)
                new AchievementsNavigator(_announcer),    // Achievements screen - groups and individual achievements (priority 57)
                new SetCollectionNavigator(_announcer),   // Set collection screen from Profile (priority 58)
                new ProfileNavigator(_announcer),         // Profile screen - info, rank, cosmetics (priority 56)
                new StoreNavigator(_announcer),           // Store screen - tabs and items (priority 55)
                new ChatNavigator(_announcer),            // Chat window - messages and input (priority 52)
                new CodexNavigator(_announcer),            // Codex/LearnToPlay screen (priority 50)
                new GeneralMenuNavigator(_announcer),
                new AssetPrepNavigator(_announcer)  // Download screen - low priority, fails gracefully
            );

            // Global social notification watcher: announces incoming chat messages,
            // friend invites, challenge invites, lobby invites/chat, and tournament-ready
            // notifications regardless of which screen is active.
            _socialNotificationWatcher = new SocialNotificationWatcher(_announcer);

            // Duel-only watcher: subscribes to UIMessageHandler.EmoteRecievedCallback once GameManager
            // exists, and reads incoming opponent emotes through the same announcement pipeline.
            _emoteAnnouncer = new EmoteAnnouncer(_announcer);

            // Subscribe to focus changes for automatic card navigation
            _focusTracker.OnFocusChanged += HandleFocusChanged;
        }

        private void HandleFocusChanged(UnityEngine.GameObject oldElement, UnityEngine.GameObject newElement)
        {
            // Use safe name access - Unity destroyed objects are not null but throw on property access
            string oldName = GetSafeGameObjectName(oldElement);
            string newName = GetSafeGameObjectName(newElement);
            Log.Focus("FocusChanged", $"Old: {oldName}, New: {newName}");

            // If focus moved to a DIFFERENT element, deactivate card navigation.
            // Skip when CurrentCard is null (blocks-only mode, e.g. packet info) - owner manages lifecycle.
            // Skip when newElement is null - that's a navigator clearing selection as an intermediate step
            // (e.g., ClearEventSystemSelection before Left/Right navigation), not the user leaving the card.
            if (_cardInfoNavigator.IsActive && _cardInfoNavigator.CurrentCard != null
                && newElement != null && _cardInfoNavigator.CurrentCard != newElement)
            {
                Log.Focus("FocusChanged", "Deactivating card navigator");
                _cardInfoNavigator.Deactivate();
            }

            // Notify PlayerPortraitNavigator of focus change so it can exit if needed
            var duelNav = _navigatorManager?.GetNavigator<DuelNavigator>();
            if (duelNav?.PortraitNavigator != null && newElement != null && newElement)
            {
                duelNav.PortraitNavigator.OnFocusChanged(newElement);
            }

            // Note: We no longer call PrepareForCard here because navigators
            // (ZoneNavigator, BattlefieldNavigator, HotHighlightNavigator) now set EventSystem
            // focus and call PrepareForCard with the correct zone. Calling it here would
            // overwrite the correct zone with the default (Hand).
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

        /// <summary>
        /// (Re)registers the global shortcuts from the current keybinds. Also runs
        /// on Keybinds.Changed so rebinding takes effect immediately. F2 (settings)
        /// and Shift+F12 (debug log) are deliberately fixed: F2 is the recovery
        /// hatch that always reaches the settings and keybinds menus.
        /// </summary>
        private void RegisterGlobalShortcuts()
        {
            _shortcuts.Clear();

            RegisterBoundShortcut(KeybindAction.Help, ToggleHelpMenu, "Help menu");
            _shortcuts.RegisterShortcut(KeyCode.F2, ToggleSettingsMenu, "Settings menu");
            RegisterBoundShortcut(KeybindAction.RepeatAnnouncement, RepeatLastAnnouncement, "Repeat last announcement");
            RegisterBoundShortcut(KeybindAction.TutorialHint, AnnounceTutorialHint, "Repeat tutorial hint");
            RegisterBoundShortcut(KeybindAction.CurrentScreen, AnnounceCurrentScreen, "Announce current screen");
            _shortcuts.RegisterShortcut(KeyCode.F12, KeyCode.LeftShift, SpeakDebugLog, "Speak recent debug log entries");
            RegisterBoundShortcut(KeybindAction.Update, HandleUpdateShortcut, "Check for update / start update");
        }

        private void RegisterBoundShortcut(KeybindAction action, System.Action handler, string description)
        {
            var chord = Keybinds.GetChord(action);
            if (!chord.IsBound)
                return;
            if (chord.Ctrl)
                _shortcuts.RegisterShortcut(chord.Key, KeyCode.LeftControl, handler, description);
            else if (chord.Shift)
                _shortcuts.RegisterShortcut(chord.Key, KeyCode.LeftShift, handler, description);
            else
                _shortcuts.RegisterShortcut(chord.Key, handler, description);
        }

        private void ToggleHelpMenu()
        {
            _helpNavigator?.Toggle();
        }

        private void ToggleSettingsMenu()
        {
            _settingsNavigator?.Toggle();
        }

        /// <summary>
        /// Opens the mod settings overlay. Used when the user activates the
        /// synthetic "Mod Settings" entry in the game's settings menu.
        /// </summary>
        public void OpenModSettings()
        {
            _settingsNavigator?.Open();
        }

        private void RepeatLastAnnouncement()
        {
            _announcer?.RepeatLastAnnouncement();
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

        /// <summary>
        /// Ctrl+Right: copy the current item — the most recent announcement — to the
        /// system clipboard so the user can paste it (e.g. their own name and ID from
        /// the friends list, a card name, or any focused element's text). Works on every
        /// screen because it reuses whatever was last spoken. Returns true when the
        /// shortcut was handled (so the caller skips this frame's other input).
        /// </summary>
        private bool HandleCopyCurrentItem()
        {
            // Exact chord match (default Ctrl+Right) — other modifier combos fall through.
            if (!Keybinds.Down(KeybindAction.CopyAnnouncement))
                return false;

            // Block the game/navigators from also seeing this key press.
            InputManager.ConsumeKey(Keybinds.GetChord(KeybindAction.CopyAnnouncement).Key);

            string text = _announcer?.LastAnnouncement;
            if (string.IsNullOrEmpty(text))
            {
                _announcer?.AnnounceInterrupt(Strings.NothingToCopy);
                return true;
            }

            if (ClipboardUtil.Copy(text))
                _announcer.AnnounceInterrupt(Strings.CopiedToClipboard(text));
            else
                _announcer.AnnounceInterrupt(Strings.CopyFailed);

            return true;
        }

        private void AnnounceTutorialHint()
        {
            var nav = _navigatorManager?.ActiveNavigator as BaseNavigator;
            if (nav != null)
            {
                _announcer.AnnounceInterrupt(nav.GetTutorialHint());
            }
        }

        private void HandleUpdateShortcut()
        {
            var active = _navigatorManager?.ActiveNavigator;
            // Allow on loading screens, general menu, or when no navigator is active yet (early boot)
            if (active != null && !(active is LoadingScreenNavigator || active is GeneralMenuNavigator || active is AssetPrepNavigator))
            {
                _announcer.AnnounceInterrupt(Strings.UpdateNotInMenu);
                return;
            }

            UpdateChecker.HandleF5(_announcer);
        }

        private void SpeakDebugLog()
        {
            var entries = Log.GetRecentEntries(5);
            if (entries.Length == 0)
            {
                _announcer.AnnounceInterrupt(Strings.DebugLogEmpty);
                return;
            }

            _announcer.AnnounceInterrupt(Strings.DebugLogHeader(entries.Length));
            foreach (var entry in entries)
            {
                ScreenReaderOutput.Speak(entry, false);
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            LoggerInstance.Msg($"Scene loaded: {sceneName} (index: {buildIndex})");

            // Clear caches on scene change
            CardDetector.ClearCache();
            CardPlayVerifier.ClearCache();
            DeckInfoProvider.ClearCache();
            RecentPlayAccessor.ClearCache();
            EventAccessor.ClearCache();
            UIFocusTracker.ClearScanCaches();

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

            // Re-subscribe the social notification watcher to the new scene's ChatManager
            _socialNotificationWatcher?.OnSceneChanged();

            // Drop the cached GameManager/UIMessageHandler so the announcer re-subscribes for the next duel.
            _emoteAnnouncer?.OnSceneChanged();
            EmoteService.ClearCache();
            PetService.ClearCache();

            // DuelNavigator and SideboardNavigator activate on DuelScene
            if (sceneName == DuelScene)
            {
                var duelNav = _navigatorManager?.GetNavigator<DuelNavigator>();
                duelNav?.OnDuelSceneLoaded();

                var sideboardNav = _navigatorManager?.GetNavigator<SideboardNavigator>();
                sideboardNav?.OnDuelSceneLoaded();
            }

        }

        public override void OnUpdate()
        {
            if (!_initialized)
                return;

            // Skip all input processing when game doesn't have focus (e.g., Alt+Tab)
            // Note: Steam overlay does NOT affect isFocused — it intercepts input at a lower
            // level before Unity sees it, so this guard only helps with Alt+Tab scenarios.
            if (!Application.isFocused)
                return;

            // Poll release every frame — the hook-driven paths miss the release frame
            // in edge cases, which would leave `_waitingForRelease` stuck until the
            // turn timer expires.
            PhaseSkipGuard.Poll();

            // Global: Ctrl+Right copies the current item's text (whatever was last
            // announced) to the clipboard. Handled here, before any navigator, so the
            // RightArrow doesn't also trigger card/menu navigation this frame.
            if (HandleCopyCurrentItem())
                return;

            // Tell KeyboardManagerPatch to block Escape from reaching the game
            // when a mod menu is open (persistent flag avoids timing issues with per-frame consume)
            InputManager.ModMenuActive = (_helpNavigator?.IsActive == true)
                || (_settingsNavigator?.IsActive == true)
                || (_extendedInfoNavigator?.IsActive == true)
                || (_gameLogNavigator?.IsActive == true);

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

            // Game log menu - same priority tier as extended info
            if (_gameLogNavigator != null && _gameLogNavigator.IsActive)
            {
                _gameLogNavigator.HandleInput();
                return;
            }

            // Deactivate card info navigator when active navigator changes (e.g., settings menu preempts duel)
            var currentNavId = _navigatorManager?.ActiveNavigator?.NavigatorId;
            if (currentNavId != _lastActiveNavigatorId)
            {
                if (_cardInfoNavigator != null && _cardInfoNavigator.IsActive)
                {
                    Log.Focus("NavigatorChange", $"Deactivating card navigator (navigator changed: {_lastActiveNavigatorId} -> {currentNavId})");
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
            else if (_cardInfoNavigator?.CurrentCard != null &&
                     (KeyInput.GetKeyDown(UnityEngine.KeyCode.UpArrow) ||
                      KeyInput.GetKeyDown(UnityEngine.KeyCode.DownArrow)))
            {
                Log.Focus("CardInfo",
                    $"Up/Down pressed but CardInfoNavigator.IsActive=False, CurrentCard={_cardInfoNavigator.CurrentCard.name}");
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

            // Poll for incoming chat messages (global, works from any screen)
            _socialNotificationWatcher?.Update();

            // Resolve UIMessageHandler once GameManager is up, then announce opponent emotes as they arrive.
            _emoteAnnouncer?.Update();

            // Poll auto-update background tasks (version check announcement, download completion)
            UpdateChecker.Update(_announcer);
        }

        public override void OnApplicationQuit()
        {
            LoggerInstance.Msg("Accessible Arena shutting down...");
            SteamOverlayBlocker.Uninstall();
            _settings?.Save();
            ScreenReaderOutput.Shutdown();
        }

    }
}
