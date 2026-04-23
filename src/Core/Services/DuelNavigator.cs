using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.PanelDetection;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using static AccessibleArena.Core.Constants.SceneNames;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for the actual duel/gameplay in DuelScene.
    /// Handles UI element discovery and Tab navigation.
    /// Delegates zone navigation to ZoneNavigator, Tab highlights / target cycling / selection
    /// mode to HotHighlightNavigator, and game-event announcements to DuelAnnouncer.
    /// </summary>
    public class DuelNavigator : BaseNavigator
    {
        // WinAPI for centering mouse cursor once when duel starts
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        private bool _isWatching;
        private bool _hasCenteredMouse;
        private bool _wasPreemptedForChat;
        private float _playerNameAnnounceDelay = -1f; // One-shot: announces matchup after HUD settles
        private ZoneNavigator _zoneNavigator;
        private HotHighlightNavigator _hotHighlightNavigator;  // Unified navigator for Tab, cards, targets, selection mode
        private CombatNavigator _combatNavigator;
        private BattlefieldNavigator _battlefieldNavigator;
        private BrowserNavigator _browserNavigator;
        private ManaColorPickerNavigator _manaColorPicker;
        private ChooseXNavigator _chooseXNavigator;
        private SpinnerNavigator _spinnerNavigator;
        private PlayerPortraitNavigator _portraitNavigator;
        private PriorityController _priorityController;
        private DuelAnnouncer _duelAnnouncer;
        private DuelChatNavigator _duelChatNavigator;
        private List<GameObject> _deactivatedSocialObjects = new List<GameObject>();

        // J key cycle state. The cycle keeps running while focus is on the original
        // source or on the last card we jumped to. Any other focus change rebuilds it.
        private uint _jumpCycleSourceId;
        private uint _jumpCycleLastDestId;
        private List<uint> _jumpCycleIds;
        private int _jumpCycleIndex;

        public override string NavigatorId => "Duel";
        public override string ScreenName => Strings.ScreenDuel;
        public override int Priority => 70; // Lower than PreBattle so it activates after

        // Let game handle Space natively (for Submit/Confirm, discard, etc.)
        protected override bool AcceptSpaceKey => false;

        // Duel uses letters as zone shortcuts (C, G, X, S, W, etc.) - disable letter navigation
        protected override bool SupportsLetterNavigation => false;

        /// <summary>Mark that this navigator was preempted for chat (F4). Next activation will say "Returned to duel".</summary>
        public void MarkPreemptedForChat() => _wasPreemptedForChat = true;

        /// <summary>Restore deactivated SocialUI GOs before opening chat via F4.</summary>
        public void RestoreSocialUIBeforeChat() => RestoreSocialUISelectables();

        public ZoneNavigator ZoneNavigator => _zoneNavigator;
        public HotHighlightNavigator HotHighlightNavigator => _hotHighlightNavigator;
        public BattlefieldNavigator BattlefieldNavigator => _battlefieldNavigator;
        public BrowserNavigator BrowserNavigator => _browserNavigator;
        public DuelAnnouncer DuelAnnouncer => _duelAnnouncer;
        public PlayerPortraitNavigator PortraitNavigator => _portraitNavigator;
        public DuelNavigator(IAnnouncementService announcer) : base(announcer)
        {
            _zoneNavigator = new ZoneNavigator(announcer);

            // Unified highlight navigator - handles Tab for both playable cards AND targets
            // Trusts game's HotHighlight system to show correct items based on game state
            _hotHighlightNavigator = new HotHighlightNavigator(announcer, _zoneNavigator);

            _browserNavigator = new BrowserNavigator(announcer, _zoneNavigator);
            _manaColorPicker = new ManaColorPickerNavigator(announcer);
            _chooseXNavigator = new ChooseXNavigator(announcer);
            _spinnerNavigator = new SpinnerNavigator(announcer);
            _portraitNavigator = new PlayerPortraitNavigator(announcer);
            _priorityController = new PriorityController();
            _duelAnnouncer = new DuelAnnouncer(announcer);
            _combatNavigator = new CombatNavigator(announcer, _duelAnnouncer);
            _battlefieldNavigator = new BattlefieldNavigator(announcer, _zoneNavigator);
            _duelChatNavigator = new DuelChatNavigator(announcer, OnDuelChatClosed);

            // Connect DuelAnnouncer to ZoneNavigator for stack checks and dirty marking
            _duelAnnouncer.SetZoneNavigator(_zoneNavigator);

            // Connect DuelAnnouncer to BattlefieldNavigator for dirty marking on zone changes
            _duelAnnouncer.SetBattlefieldNavigator(_battlefieldNavigator);

            // Connect ZoneNavigator to CombatNavigator for attacker state announcements
            _zoneNavigator.SetCombatNavigator(_combatNavigator);

            // Connect BattlefieldNavigator to CombatNavigator for combat state announcements
            _battlefieldNavigator.SetCombatNavigator(_combatNavigator);

            // Connect ZoneNavigator to HotHighlightNavigator for clearing state on zone navigation
            _zoneNavigator.SetHotHighlightNavigator(_hotHighlightNavigator);

            // Connect HotHighlightNavigator to BattlefieldNavigator for syncing position on Tab
            _hotHighlightNavigator.SetBattlefieldNavigator(_battlefieldNavigator);

            // Connect PriorityController to PhaseSkipGuard for phase skip warning
            PhaseSkipGuard.SetPriorityController(_priorityController);
            PhaseSkipGuard.SetModalNavigatorCheck(() => BrowserNavigator.IsActive || _chooseXNavigator.IsActive || _spinnerNavigator.IsActive);

        }

        /// <summary>
        /// Called by AccessibleArenaMod when DuelScene loads.
        /// </summary>
        public void OnDuelSceneLoaded()
        {
            Log.Msg("{NavigatorId}", $"DuelScene loaded - starting to watch for duel elements");
            _isWatching = true;
            _hasCenteredMouse = false; // Reset so mouse gets centered when duel activates

            // Clear announcement history for the new duel
            _announcer.ClearHistory();

            // Clear stale EventSystem selection from pre-game screen.
            // Without this, the first Tab press navigates from the stale "Button" (Settings)
            // to NavArrowNextbutton (emote panel) via Unity's Selectable chain.
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(null);
                Log.Msg("{NavigatorId}", $"Cleared EventSystem selection");
            }
        }

        /// <summary>
        /// Called when DuelNavigator becomes active. Centers mouse once for card playing.
        /// </summary>
        protected override void OnActivated()
        {
            base.OnActivated();
            EnablePopupDetection();

            // Disable SocialUI selectables so Unity's EventSystem doesn't auto-navigate to them.
            // During friend challenge duels, the SocialUI (DontDestroyOnLoad) persists with an
            // InputField and Button that constantly steal EventSystem focus, eating Space/key input.
            DisableSocialUISelectables();

            // Center mouse cursor once when duel starts
            // This ensures card play clicks hit screen center correctly
            if (!_hasCenteredMouse)
            {
                int centerX = Screen.width / 2;
                int centerY = Screen.height / 2;
                SetCursorPos(centerX, centerY);
                Log.Msg("{NavigatorId}", $"Centered mouse cursor at ({centerX}, {centerY})");
                _hasCenteredMouse = true;

                // Schedule a matchup announcement once the HUD has fully settled
                _playerNameAnnounceDelay = 1.5f;
            }
        }

        protected override void OnDeactivating()
        {
            RestoreSocialUISelectables();
            base.OnDeactivating();
        }

        public override void Update()
        {
            base.Update();

            // One-shot matchup announcement after HUD settles (only while active)
            if (_isActive && _playerNameAnnounceDelay > 0f)
            {
                _playerNameAnnounceDelay -= Time.deltaTime;
                if (_playerNameAnnounceDelay <= 0f)
                {
                    _playerNameAnnounceDelay = -1f;
                    string matchup = _portraitNavigator.GetMatchupText();
                    if (!string.IsNullOrEmpty(matchup))
                    {
                        Log.Msg("{NavigatorId}", $"Matchup: {matchup}");
                        _announcer.Announce(matchup, Models.AnnouncementPriority.Normal);
                    }
                }
            }

            // NPE tutorial: simulate hover on focused CDC so the tutorial progresses
            // when the player navigates to the card Sparky is pointing at
            if (_isActive)
                _duelAnnouncer.UpdateNPEHoverSimulation();
        }

        public override void OnSceneChanged(string sceneName)
        {
            if (sceneName != DuelScene)
            {
                _isWatching = false;
                _hasCenteredMouse = false; // Reset for next duel
                _playerNameAnnounceDelay = -1f;
                _zoneNavigator.Deactivate();
                _hotHighlightNavigator.Deactivate();
                _battlefieldNavigator.Deactivate();
                _portraitNavigator.Deactivate();
                _duelAnnouncer.Deactivate();
                _duelChatNavigator.OnSceneChanged();
                _priorityController.ClearCache();
                _deactivatedSocialObjects.Clear();
            }

            base.OnSceneChanged(sceneName);
        }

        protected override bool DetectScreen()
        {
            if (!_isWatching) return false;
            // HasDuelElements checks for Stop EventTriggers and duel-phase button text,
            // which only exist during actual gameplay (not during pre-game matchmaking).
            return HasDuelElements();
        }

        protected override bool ValidateElements()
        {
            // Deactivate if settings menu is open - let SettingsMenuNavigator handle it
            if (PanelStateManager.Instance?.IsSettingsMenuOpen == true)
            {
                Log.Msg("{NavigatorId}", $"Settings menu detected - deactivating to let SettingsMenuNavigator take over");
                return false;
            }

            return base.ValidateElements();
        }

        /// <summary>
        /// Override to clear EventSystem selection instead of setting it.
        /// DuelNavigator uses its own highlight/zone systems for navigation,
        /// and setting EventSystem selection to the first discovered element (Settings button)
        /// causes the first Tab to navigate to NavArrowNextbutton via Unity's Selectable chain.
        /// </summary>
        protected override void UpdateEventSystemSelection()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(null);
            }
        }

        protected override void DiscoverElements()
        {
            var addedObjects = new HashSet<GameObject>();

            Log.Msg("{NavigatorId}", $"=== DUEL UI DISCOVERY START ===");

            // 1. Activate zone navigator and discover zones
            _zoneNavigator.Activate();
            _zoneNavigator.LogZoneSummary();

            // 2. Activate battlefield navigator for row-based navigation
            _battlefieldNavigator.Activate();

            // 3. Activate unified highlight navigator for Tab cycling (playable cards AND targets)
            _hotHighlightNavigator.Activate();

            // 4. Activate duel announcer with local player ID from ZoneNavigator
            uint localPlayerId = _zoneNavigator.GetLocalPlayerId();
            _duelAnnouncer.Activate(localPlayerId);

            // 5. Find all Selectables (StyledButton, Button, Toggle, etc.)
            DiscoverSelectables(addedObjects);

            // 6. Find all CustomButtons
            DiscoverCustomButtons(addedObjects);

            // 7. Find all EventTriggers (skip non-useful ones like "Stop")
            DiscoverEventTriggers(addedObjects);

            // 8. Find specific duel elements by name patterns
            DiscoverDuelSpecificElements(addedObjects);

            // 9. Activate portrait navigator for P key timer/portrait info
            _portraitNavigator.Activate();

            Log.Msg("{NavigatorId}", $"=== DUEL UI DISCOVERY END - Found {_elements.Count} elements ===");
        }

        private void DiscoverSelectables(HashSet<GameObject> addedObjects)
        {
            Log.Msg("{NavigatorId}", $"Searching Selectables...");

            foreach (var selectable in GameObject.FindObjectsOfType<Selectable>())
            {
                if (selectable == null || !selectable.gameObject.activeInHierarchy || !selectable.interactable)
                    continue;

                if (IsSocialUIElement(selectable.gameObject))
                    continue;

                if (addedObjects.Contains(selectable.gameObject))
                    continue;

                string name = selectable.gameObject.name;
                string typeName = selectable.GetType().Name;
                string label = GetButtonText(selectable.gameObject, name);
                var (elementType, elementRole) = GetSelectableTypeAndRole(selectable);

                Log.Msg("{NavigatorId}", $"Selectable ({typeName}): {name} - Text: '{label}'");

                AddElement(selectable.gameObject, BuildLabel(label, elementType, elementRole), default, null, null, elementRole);
                addedObjects.Add(selectable.gameObject);
            }
        }

        private void DiscoverCustomButtons(HashSet<GameObject> addedObjects)
        {
            Log.Msg("{NavigatorId}", $"Searching CustomButtons...");

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy)
                    continue;

                if (mb.GetType().Name != T.CustomButton)
                    continue;

                if (IsSocialUIElement(mb.gameObject))
                    continue;

                if (addedObjects.Contains(mb.gameObject))
                    continue;

                string name = mb.gameObject.name;
                string label = GetButtonText(mb.gameObject, name);

                Log.Msg("{NavigatorId}", $"CustomButton: {name} - Text: '{label}'");

                AddElement(mb.gameObject, BuildLabel(label, Models.Strings.RoleButton, UIElementClassifier.ElementRole.Button), default, null, null, UIElementClassifier.ElementRole.Button);
                addedObjects.Add(mb.gameObject);
            }
        }

        private void DiscoverEventTriggers(HashSet<GameObject> addedObjects)
        {
            Log.Msg("{NavigatorId}", $"Searching EventTriggers...");

            foreach (var trigger in GameObject.FindObjectsOfType<EventTrigger>())
            {
                if (trigger == null || !trigger.gameObject.activeInHierarchy)
                    continue;

                if (IsSocialUIElement(trigger.gameObject))
                    continue;

                if (addedObjects.Contains(trigger.gameObject))
                    continue;

                string name = trigger.gameObject.name;

                // Skip "Stop" buttons - timer controls that flood the element list
                if (name == "Stop")
                    continue;

                string label = GetButtonText(trigger.gameObject, CleanName(name));

                Log.Msg("{NavigatorId}", $"EventTrigger: {name} - Text: '{label}'");

                AddElement(trigger.gameObject, label);
                addedObjects.Add(trigger.gameObject);
            }
        }

        private void DiscoverDuelSpecificElements(HashSet<GameObject> addedObjects)
        {
            Log.Msg("{NavigatorId}", $"Searching duel-specific elements...");

            string[] duelElementNames = new[]
            {
                "Nav_Settings",
                "SocialCornerIcon",
                "PassButton",
                "ResolveButton",
                "UndoButton",
                "ConcedeButton",
                "FullControlToggle",
                "AutoTapToggle"
            };

            foreach (var name in duelElementNames)
            {
                var obj = GameObject.Find(name);
                if (obj != null && obj.activeInHierarchy && !addedObjects.Contains(obj))
                {
                    string label = GetButtonText(obj, CleanName(name));
                    Log.Msg("{NavigatorId}", $"Named element: {name} - Text: '{label}'");

                    AddElement(obj, BuildLabel(label, Models.Strings.RoleButton, UIElementClassifier.ElementRole.Button), default, null, null, UIElementClassifier.ElementRole.Button);
                    addedObjects.Add(obj);
                }
            }
        }

        public override string GetTutorialHint() =>
            BrowserNavigator.IsActive ? _browserNavigator.GetTutorialHint() : LocaleManager.Instance.Get("DuelKeybindingsHint");

        protected override string GetActivationAnnouncement()
        {
            if (_wasPreemptedForChat)
            {
                _wasPreemptedForChat = false;
                return Strings.ReturnedToDuel;
            }

            string core = Models.Strings.Duel_Started;
            return Strings.WithHint(core, "DuelKeybindingsHint");
        }

        protected override bool OnElementActivated(int index, GameObject element)
        {
            string name = element.name;

            if (name.Contains("PromptButton") || name.Contains("Styled") ||
                HasComponent(element, T.CustomButton) || HasComponent(element, "StyledButton"))
            {
                Log.Msg("{NavigatorId}", $"Using pointer click for: {name}");
                UIActivator.SimulatePointerClick(element);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Early input hook: DuelChatNavigator takes full input priority when active.
        /// Runs before BaseNavigator's edit mode detection, preventing the chat input field
        /// from being handled by BaseNavigator's HandleInputFieldNavigation.
        /// </summary>
        protected override bool HandleEarlyInput()
        {
            _duelChatNavigator.Update();
            if (_duelChatNavigator.HandleInput())
                return true;
            return false;
        }

        /// <summary>
        /// Handles zone navigation, target selection, and playable card cycling input.
        /// Uses unified HotHighlightNavigator for Tab/Enter on playable cards and targets.
        /// Priority: Browser > Discard > Combat > HotHighlight > Portrait > Battlefield > Zone > base
        /// </summary>
        protected override bool HandleCustomInput()
        {
            // Flush debounced phase announcements
            _duelAnnouncer.Update();

            // Monitor prompt buttons for meaningful choice announcements
            _hotHighlightNavigator.MonitorPromptButtons(
                _duelAnnouncer.TimeSinceLastPhaseChange,
                _duelAnnouncer.IsInDeclareAttackersPhase || _duelAnnouncer.IsInDeclareBlockersPhase);

            // NOTE: Ctrl key for full control investigated but not working in Color Challenge mode
            // See docs/AUTOSKIP_MODE_INVESTIGATION.md for details and attempted solutions

            // Mana color picker (any-color mana sources) - highest priority modal
            _manaColorPicker.Update();
            if (_manaColorPicker.HandleInput())
                return true;

            // Choose X (X-cost spells, choose amount, die roll) - high priority modal
            _chooseXNavigator.Update();
            if (_chooseXNavigator.HandleInput())
                return true;

            // Spinner (counter distribution, e.g. Crashing Wave stun counters) - high priority modal
            _spinnerNavigator.Update();
            if (_spinnerNavigator.HandleInput())
                return true;

            // Next, check for browser UI (scry, mulligan, damage assignment, etc.)
            // Browsers take high priority as they represent modal interactions
            _browserNavigator.Update();
            if (_browserNavigator.HandleInput())
                return true;

            // F4: Open chat (checked after modals so chat can't open during
            // mana picking, browser, spinner, or X-cost interactions).
            // When DuelChatNavigator is already active, HandleEarlyInput consumes F4 above.
            if (Input.GetKeyDown(KeyCode.F4))
            {
                OpenDuelChat();
                return true;
            }

            // Shift+Backspace: Pass until opponent action, Ctrl+Backspace: Skip turn
            // Must be checked before CombatNavigator which also consumes Backspace
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

                if (ctrl)
                {
                    var result = _priorityController.ToggleSkipTurn();
                    if (result.HasValue)
                        _announcer.AnnounceInterrupt(result.Value ? Strings.SkipTurn_On : Strings.SkipTurn_Off);
                    return true;
                }

                if (shift)
                {
                    var result = _priorityController.TogglePassUntilResponse();
                    if (result.HasValue)
                        _announcer.AnnounceInterrupt(result.Value ? Strings.PassUntilResponse_On : Strings.PassUntilResponse_Off);
                    return true;
                }
            }

            // Next, let CombatNavigator handle Space during declare attackers/blockers
            if (_combatNavigator.HandleInput())
                return true;

            // NEW: Unified HotHighlightNavigator handles Tab/Enter for both playable cards and targets
            // No auto-detect/auto-exit needed - we trust the game's highlight management
            if (_hotHighlightNavigator.HandleInput())
                return true;

            // Portrait/timer info (V key zone) - BEFORE battlefield so arrow keys
            // work correctly when in player info zone
            if (_portraitNavigator.HandleInput())
                return true;

            // P key: Full control toggle
            if (Input.GetKeyDown(KeyCode.P))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                Log.Msg("{NavigatorId}", $"P key pressed (shift={shift})");
                if (shift)
                {
                    var result = _priorityController.ToggleLockFullControl();
                    Log.Msg("{NavigatorId}", $"ToggleLockFullControl result: {result}");
                    if (result.HasValue)
                    {
                        _announcer.AnnounceInterrupt(result.Value ? Strings.FullControl_Locked : Strings.FullControl_Unlocked);
                    }
                }
                else
                {
                    var result = _priorityController.ToggleFullControl();
                    Log.Msg("{NavigatorId}", $"ToggleFullControl result: {result}");
                    if (result.HasValue)
                    {
                        _announcer.AnnounceInterrupt(result.Value ? Strings.FullControl_On : Strings.FullControl_Off);
                    }
                }
                return true;
            }

            // Number keys 1-0: Phase stop toggles (only when mana picker is NOT active)
            if (HandlePhaseStopKeys())
                return true;

            // T key: Announce browser name if active, otherwise turn and phase info
            if (Input.GetKeyDown(KeyCode.T))
            {
                if (BrowserNavigator.IsActive)
                {
                    string browserName = BrowserDetector.GetFriendlyBrowserName(_browserNavigator.ActiveBrowserType);
                    _announcer.AnnounceInterrupt(browserName);
                }
                else
                {
                    string turnPhaseInfo = _duelAnnouncer.GetTurnPhaseInfo();
                    _announcer.AnnounceInterrupt(turnPhaseInfo);
                }
                return true;
            }

            // I key: Extended card info (navigable menu with keyword descriptions + linked face)
            if (Input.GetKeyDown(KeyCode.I))
            {
                var extInfoNav = AccessibleArenaMod.Instance?.ExtendedInfoNavigator;
                var cardNav = AccessibleArenaMod.Instance?.CardNavigator;
                if (extInfoNav != null && cardNav != null && cardNav.IsActive && cardNav.CurrentCard != null)
                {
                    extInfoNav.Open(cardNav.CurrentCard);
                }
                else
                {
                    // Fallback: try browser's current card (e.g., AssignDamage skips PrepareForCard)
                    var browserCard = _browserNavigator.GetCurrentCard();
                    if (extInfoNav != null && browserCard != null)
                    {
                        extInfoNav.Open(browserCard);
                    }
                    else
                    {
                        _announcer.AnnounceInterrupt(Strings.NoCardToInspect);
                    }
                }
                return true;
            }

            // O key: Game log (review all duel announcements)
            if (Input.GetKeyDown(KeyCode.O))
            {
                var logNav = AccessibleArenaMod.Instance?.GameLogNavigator;
                if (logNav != null)
                    logNav.Open();
                return true;
            }

            // K key: Counter info on focused card
            if (Input.GetKeyDown(KeyCode.K))
            {
                GameObject card = null;
                var cardNav = AccessibleArenaMod.Instance?.CardNavigator;
                if (cardNav != null && cardNav.IsActive && cardNav.CurrentCard != null)
                    card = cardNav.CurrentCard;
                else if (_battlefieldNavigator.GetCurrentCard() != null)
                    card = _battlefieldNavigator.GetCurrentCard();
                else
                    card = _zoneNavigator.GetCurrentCard() ?? _browserNavigator.GetCurrentCard();

                if (card != null)
                {
                    var counters = CardStateProvider.GetCountersFromCard(card);
                    if (counters.Count > 0)
                    {
                        var parts = new List<string>();
                        foreach (var (typeName, count) in counters)
                            parts.Add(Strings.Duel_CounterEntry(count, typeName));
                        _announcer.AnnounceInterrupt(string.Join(", ", parts));
                    }
                    else
                    {
                        _announcer.AnnounceInterrupt(Strings.Duel_NoCounters);
                    }
                }
                else
                {
                    _announcer.AnnounceInterrupt(Strings.NoCardToInspect);
                }
                return true;
            }

            // J key: Jump focus to attachment parent / attachments / targets / targeted-by.
            // Cycles through the related-card list; resets when the source card changes.
            if (Input.GetKeyDown(KeyCode.J))
            {
                JumpToRelatedCard();
                return true;
            }

            // E key: Timer and timeouts (E = your timer, Shift+E = opponent timer)
            if (Input.GetKeyDown(KeyCode.E))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                _portraitNavigator.AnnounceTimer(opponent: shift);
                return true;
            }

            // M key: Land summary (M = your lands, Shift+M = opponent lands)
            if (Input.GetKeyDown(KeyCode.M))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                var landRow = shift ? BattlefieldRow.EnemyLands : BattlefieldRow.PlayerLands;
                string summary = _battlefieldNavigator.GetLandSummary(landRow);
                _announcer.AnnounceInterrupt(summary);
                return true;
            }

            // Battlefield navigation (A/R/B shortcuts and row-based navigation)
            if (_battlefieldNavigator.HandleInput())
                return true;

            // Delegate zone input handling to ZoneNavigator (C, G, X, S shortcuts)
            if (_zoneNavigator.HandleInput())
                return true;

            // Consume Enter so it never falls through to BaseNavigator.HandleNavigation().
            // All duel Enter actions are handled by sub-navigators above (browser, combat,
            // highlight, etc.). Without this guard, unhandled Enter activates whatever
            // Selectable is at _currentIndex (e.g. the settings button).
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                return true;

            // Consume Up/Down so it never falls through to BaseNavigator element navigation.
            // All duel Up/Down actions are handled by sub-navigators above (browser, card info,
            // battlefield, zone, portrait). Without this guard, unhandled Up/Down navigates
            // internal UI elements (PromptButtons, Nav_Settings, etc.) - e.g. during the gap
            // between Informational browser exit and Mulligan browser open.
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                return true;

            return base.HandleCustomInput();
        }

        #region Related-card jump (J key)

        /// <summary>
        /// Returns the card the user is currently focused on, using the same lookup
        /// order as the K-key counter handler: extended card info → battlefield →
        /// zone → browser.
        /// </summary>
        private GameObject GetFocusedDuelCard()
        {
            var cardNav = AccessibleArenaMod.Instance?.CardNavigator;
            if (cardNav != null && cardNav.IsActive && cardNav.CurrentCard != null)
                return cardNav.CurrentCard;
            var bfCard = _battlefieldNavigator.GetCurrentCard();
            if (bfCard != null) return bfCard;
            return _zoneNavigator.GetCurrentCard() ?? _browserNavigator.GetCurrentCard();
        }

        private void JumpToRelatedCard()
        {
            var focus = GetFocusedDuelCard();
            if (focus == null)
            {
                _announcer.AnnounceInterrupt(Strings.NoCardToInspect);
                return;
            }

            uint focusId = CardStateProvider.GetCardInstanceId(focus);

            // Continue the existing cycle only if focus is still on the source or
            // on the last destination we jumped to. Any other card means the user
            // navigated away, so start fresh.
            bool continueCycle = _jumpCycleSourceId != 0
                                 && _jumpCycleIds != null
                                 && _jumpCycleIds.Count > 0
                                 && focusId != 0
                                 && (focusId == _jumpCycleSourceId || focusId == _jumpCycleLastDestId);

            if (!continueCycle)
            {
                _jumpCycleSourceId = focusId;
                _jumpCycleIds = CardJumpService.GetRelatedInstanceIds(focus);
                _jumpCycleIndex = 0;
                _jumpCycleLastDestId = 0;

                if (focusId == 0 || _jumpCycleIds.Count == 0)
                {
                    _jumpCycleSourceId = 0;
                    _announcer.AnnounceInterrupt(Strings.Duel_NoJumpTarget);
                    return;
                }
            }

            int idx = _jumpCycleIndex % _jumpCycleIds.Count;
            _jumpCycleIndex = (idx + 1) % _jumpCycleIds.Count;
            uint targetId = _jumpCycleIds[idx];

            var location = CardJumpService.ResolveInstanceId(targetId);
            if (location == null)
            {
                string missingName = CardStateProvider.ResolveInstanceIdToNameExtended(targetId);
                if (!string.IsNullOrEmpty(missingName))
                    _announcer.AnnounceInterrupt(Strings.Duel_JumpTargetGone(missingName));
                else
                    _announcer.AnnounceInterrupt(Strings.Duel_JumpTargetGoneUnknown);
                return;
            }

            bool navigated = location.Value.Zone == ZoneType.Battlefield
                ? _battlefieldNavigator.NavigateToSpecificCard(location.Value.Card, announceRowChange: true)
                : _zoneNavigator.NavigateToSpecificCard(location.Value.Zone, location.Value.Card, announceZoneChange: true);

            if (navigated)
            {
                _jumpCycleLastDestId = CardStateProvider.GetCardInstanceId(location.Value.Card);
            }
            else
            {
                string name = CardDetector.GetCardName(location.Value.Card);
                string zoneName = Strings.GetZoneName(location.Value.Zone);
                _announcer.AnnounceInterrupt(Strings.Duel_JumpUnreachable(name, zoneName));
            }
        }

        #endregion

        #region Chat

        /// <summary>
        /// Open the chat window via DuelChatNavigator (sub-navigator approach).
        /// Restores SocialUI selectables, finds SocialUI component, and initiates chat.
        /// </summary>
        private void OpenDuelChat()
        {
            var socialPanel = GameObject.Find("SocialUI_V2_Desktop_16x9(Clone)");
            if (socialPanel == null)
            {
                _announcer.AnnounceInterrupt(Strings.ChatUnavailable);
                return;
            }

            MonoBehaviour socialUI = null;
            foreach (var comp in socialPanel.GetComponents<MonoBehaviour>())
            {
                if (comp != null && comp.GetType().Name == T.SocialUI)
                {
                    socialUI = comp;
                    break;
                }
            }
            if (socialUI == null)
            {
                _announcer.AnnounceInterrupt(Strings.ChatUnavailable);
                return;
            }

            // Restore SocialUI selectables so the chat window elements are active
            RestoreSocialUISelectables();

            _duelChatNavigator.Open(socialUI);
        }

        /// <summary>
        /// Called when DuelChatNavigator closes (for any reason: user close, timeout, external).
        /// Re-disables SocialUI selectables to prevent EventSystem focus stealing.
        /// </summary>
        private void OnDuelChatClosed()
        {
            DisableSocialUISelectables();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Deactivate SocialUI Selectable GameObjects so Unity's EventSystem cannot
        /// auto-navigate to them during duel. The game can re-enable interactable flags
        /// on Selectables, but won't reactivate deactivated GameObjects.
        /// Only targets active Selectable GOs (InputField, Button_Send, etc.).
        /// </summary>
        private void DisableSocialUISelectables()
        {
            _deactivatedSocialObjects.Clear();
            var socialPanel = GameObject.Find("SocialUI_V2_Desktop_16x9(Clone)");
            if (socialPanel == null) return;

            foreach (var selectable in socialPanel.GetComponentsInChildren<Selectable>(false))
            {
                if (selectable != null && selectable.gameObject.activeInHierarchy)
                {
                    selectable.gameObject.SetActive(false);
                    _deactivatedSocialObjects.Add(selectable.gameObject);
                }
            }

            if (_deactivatedSocialObjects.Count > 0)
                Log.Msg("{NavigatorId}", $"Deactivated {_deactivatedSocialObjects.Count} SocialUI selectable GOs");
        }

        /// <summary>
        /// Reactivate SocialUI GameObjects that were deactivated during duel.
        /// </summary>
        private void RestoreSocialUISelectables()
        {
            if (_deactivatedSocialObjects.Count == 0) return;

            int restored = 0;
            foreach (var go in _deactivatedSocialObjects)
            {
                if (go != null)
                {
                    go.SetActive(true);
                    restored++;
                }
            }
            _deactivatedSocialObjects.Clear();

            if (restored > 0)
                Log.Msg("{NavigatorId}", $"Reactivated {restored} SocialUI selectable GOs");
        }

        /// <summary>
        /// Handle number keys 1-0 for toggling phase stops.
        /// Maps: 1=Upkeep, 2=Draw, 3=First Main, 4=Begin Combat, 5=Declare Attackers,
        /// 6=Declare Blockers, 7=Combat Damage, 8=End Combat, 9=Second Main, 0=End Step.
        /// </summary>
        private bool HandlePhaseStopKeys()
        {
            // Map KeyCode.Alpha1-Alpha9 to index 0-8, Alpha0 to index 9
            int keyIndex = -1;

            if (Input.GetKeyDown(KeyCode.Alpha1)) keyIndex = 0;
            else if (Input.GetKeyDown(KeyCode.Alpha2)) keyIndex = 1;
            else if (Input.GetKeyDown(KeyCode.Alpha3)) keyIndex = 2;
            else if (Input.GetKeyDown(KeyCode.Alpha4)) keyIndex = 3;
            else if (Input.GetKeyDown(KeyCode.Alpha5)) keyIndex = 4;
            else if (Input.GetKeyDown(KeyCode.Alpha6)) keyIndex = 5;
            else if (Input.GetKeyDown(KeyCode.Alpha7)) keyIndex = 6;
            else if (Input.GetKeyDown(KeyCode.Alpha8)) keyIndex = 7;
            else if (Input.GetKeyDown(KeyCode.Alpha9)) keyIndex = 8;
            else if (Input.GetKeyDown(KeyCode.Alpha0)) keyIndex = 9;

            if (keyIndex < 0) return false;

            var result = _priorityController.TogglePhaseStop(keyIndex);
            if (result.HasValue)
            {
                string announcement = result.Value.isSet
                    ? Strings.PhaseStop_Set(result.Value.phaseName)
                    : Strings.PhaseStop_Cleared(result.Value.phaseName);
                _announcer.AnnounceInterrupt(announcement);
            }
            return true;
        }

        private bool HasDuelElements()
        {
            foreach (var selectable in GameObject.FindObjectsOfType<Selectable>())
            {
                if (selectable == null || !selectable.gameObject.activeInHierarchy)
                    continue;

                string name = selectable.gameObject.name;
                if (name.Contains("PromptButton_Primary"))
                {
                    string text = GetButtonText(selectable.gameObject, "");
                    if (text != null)
                    {
                        string lower = text.ToLowerInvariant();
                        if (lower.Contains("end") || lower.Contains("main") ||
                            lower.Contains("pass") || lower.Contains("resolve") ||
                            lower.Contains("combat") || lower.Contains("attack") ||
                            lower.Contains("block") || lower.Contains("done"))
                            return true;
                    }
                }
            }

            foreach (var trigger in GameObject.FindObjectsOfType<EventTrigger>())
            {
                if (trigger == null || !trigger.gameObject.activeInHierarchy)
                    continue;

                if (trigger.gameObject.name == "Stop")
                    return true;
            }

            return false;
        }

        private (string roleLabel, UIElementClassifier.ElementRole role) GetSelectableTypeAndRole(Selectable selectable)
        {
            if (selectable is Button) return (Models.Strings.RoleButton, UIElementClassifier.ElementRole.Button);
            if (selectable is Toggle) return (Models.Strings.RoleCheckbox, UIElementClassifier.ElementRole.Toggle);
            if (selectable is Slider) return (Models.Strings.RoleSlider, UIElementClassifier.ElementRole.Slider);
            if (selectable is Scrollbar) return (Models.Strings.RoleScrollbar, UIElementClassifier.ElementRole.Scrollbar);
            if (selectable is Dropdown) return (Models.Strings.RoleDropdown, UIElementClassifier.ElementRole.Dropdown);
            if (selectable is InputField) return (Models.Strings.TextField, UIElementClassifier.ElementRole.TextField);

            string typeName = selectable.GetType().Name.ToLower();
            if (typeName.Contains("button")) return (Models.Strings.RoleButton, UIElementClassifier.ElementRole.Button);
            if (typeName.Contains("toggle")) return (Models.Strings.RoleCheckbox, UIElementClassifier.ElementRole.Toggle);

            return (Models.Strings.RoleControl, UIElementClassifier.ElementRole.Unknown);
        }

        /// <summary>
        /// Check if a GameObject is inside the SocialUI hierarchy (DontDestroyOnLoad).
        /// During friend challenge duels, SocialUI elements (chat input, send button, etc.)
        /// leak into element discovery. Filter them out.
        /// </summary>
        private static bool IsSocialUIElement(GameObject obj)
        {
            if (obj == null) return false;
            Transform current = obj.transform;
            for (int i = 0; i < 15 && current != null; i++)
            {
                if (current.name.Contains("SocialUI"))
                    return true;
                current = current.parent;
            }
            return false;
        }

        private bool HasComponent(GameObject obj, string componentName)
        {
            foreach (var component in obj.GetComponents<Component>())
            {
                if (component != null && component.GetType().Name == componentName)
                    return true;
            }
            return false;
        }

        private string CleanName(string name)
        {
            name = name.Replace("_", " ").Replace("(Clone)", "").Trim();
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
            name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+", " ");
            return name;
        }

        #endregion
    }
}
