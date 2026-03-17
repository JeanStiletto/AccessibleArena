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

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for the actual duel/gameplay in DuelScene.
    /// Handles UI element discovery and Tab navigation.
    /// Delegates zone navigation to ZoneNavigator.
    /// Delegates target selection to TargetNavigator.
    /// Delegates playable card cycling to HighlightNavigator.
    /// Activates DuelAnnouncer for game event announcements.
    /// </summary>
    public class DuelNavigator : BaseNavigator
    {
        // WinAPI for centering mouse cursor once when duel starts
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        private bool _isWatching;
        private bool _hasCenteredMouse;
        private ZoneNavigator _zoneNavigator;
        private HotHighlightNavigator _hotHighlightNavigator;  // Unified navigator for Tab, cards, targets, selection mode
        private CombatNavigator _combatNavigator;
        private BattlefieldNavigator _battlefieldNavigator;
        private BrowserNavigator _browserNavigator;
        private ManaColorPickerNavigator _manaColorPicker;
        private ChooseXNavigator _chooseXNavigator;
        private PlayerPortraitNavigator _portraitNavigator;
        private PriorityController _priorityController;
        private DuelAnnouncer _duelAnnouncer;

        public override string NavigatorId => "Duel";
        public override string ScreenName => Strings.ScreenDuel;
        public override int Priority => 70; // Lower than PreBattle so it activates after

        // Let game handle Space natively (for Submit/Confirm, discard, etc.)
        protected override bool AcceptSpaceKey => false;

        // Duel uses letters as zone shortcuts (C, G, X, S, W, etc.) - disable letter navigation
        protected override bool SupportsLetterNavigation => false;

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
            _portraitNavigator = new PlayerPortraitNavigator(announcer);
            _priorityController = new PriorityController();
            _duelAnnouncer = new DuelAnnouncer(announcer);
            _combatNavigator = new CombatNavigator(announcer, _duelAnnouncer);
            _battlefieldNavigator = new BattlefieldNavigator(announcer, _zoneNavigator);

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

        }

        /// <summary>
        /// Called by AccessibleArenaMod when DuelScene loads.
        /// </summary>
        public void OnDuelSceneLoaded()
        {
            MelonLogger.Msg($"[{NavigatorId}] DuelScene loaded - starting to watch for duel elements");
            _isWatching = true;
            _hasCenteredMouse = false; // Reset so mouse gets centered when duel activates

            // Clear stale EventSystem selection from pre-game screen.
            // Without this, the first Tab press navigates from the stale "Button" (Settings)
            // to NavArrowNextbutton (emote panel) via Unity's Selectable chain.
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(null);
                MelonLogger.Msg($"[{NavigatorId}] Cleared EventSystem selection");
            }
        }

        /// <summary>
        /// Called when DuelNavigator becomes active. Centers mouse once for card playing.
        /// </summary>
        protected override void OnActivated()
        {
            base.OnActivated();

            // Center mouse cursor once when duel starts
            // This ensures card play clicks hit screen center correctly
            if (!_hasCenteredMouse)
            {
                int centerX = Screen.width / 2;
                int centerY = Screen.height / 2;
                SetCursorPos(centerX, centerY);
                MelonLogger.Msg($"[{NavigatorId}] Centered mouse cursor at ({centerX}, {centerY})");
                _hasCenteredMouse = true;
            }
        }

        public override void OnSceneChanged(string sceneName)
        {
            if (sceneName != DuelScene)
            {
                _isWatching = false;
                _hasCenteredMouse = false; // Reset for next duel
                _zoneNavigator.Deactivate();
                _hotHighlightNavigator.Deactivate();
                _battlefieldNavigator.Deactivate();
                _portraitNavigator.Deactivate();
                _duelAnnouncer.Deactivate();
                _priorityController.ClearCache();
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
                MelonLogger.Msg($"[{NavigatorId}] Settings menu detected - deactivating to let SettingsMenuNavigator take over");
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

            MelonLogger.Msg($"[{NavigatorId}] === DUEL UI DISCOVERY START ===");

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

            MelonLogger.Msg($"[{NavigatorId}] === DUEL UI DISCOVERY END - Found {_elements.Count} elements ===");
        }

        private void DiscoverSelectables(HashSet<GameObject> addedObjects)
        {
            MelonLogger.Msg($"[{NavigatorId}] Searching Selectables...");

            foreach (var selectable in GameObject.FindObjectsOfType<Selectable>())
            {
                if (selectable == null || !selectable.gameObject.activeInHierarchy || !selectable.interactable)
                    continue;

                if (addedObjects.Contains(selectable.gameObject))
                    continue;

                string name = selectable.gameObject.name;
                string typeName = selectable.GetType().Name;
                string label = GetButtonText(selectable.gameObject, name);
                var (elementType, elementRole) = GetSelectableTypeAndRole(selectable);

                MelonLogger.Msg($"[{NavigatorId}] Selectable ({typeName}): {name} - Text: '{label}'");

                AddElement(selectable.gameObject, BuildLabel(label, elementType, elementRole), default, null, null, elementRole);
                addedObjects.Add(selectable.gameObject);
            }
        }

        private void DiscoverCustomButtons(HashSet<GameObject> addedObjects)
        {
            MelonLogger.Msg($"[{NavigatorId}] Searching CustomButtons...");

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy)
                    continue;

                if (mb.GetType().Name != T.CustomButton)
                    continue;

                if (addedObjects.Contains(mb.gameObject))
                    continue;

                string name = mb.gameObject.name;
                string label = GetButtonText(mb.gameObject, name);

                MelonLogger.Msg($"[{NavigatorId}] CustomButton: {name} - Text: '{label}'");

                AddElement(mb.gameObject, BuildLabel(label, Models.Strings.RoleButton, UIElementClassifier.ElementRole.Button), default, null, null, UIElementClassifier.ElementRole.Button);
                addedObjects.Add(mb.gameObject);
            }
        }

        private void DiscoverEventTriggers(HashSet<GameObject> addedObjects)
        {
            MelonLogger.Msg($"[{NavigatorId}] Searching EventTriggers...");

            foreach (var trigger in GameObject.FindObjectsOfType<EventTrigger>())
            {
                if (trigger == null || !trigger.gameObject.activeInHierarchy)
                    continue;

                if (addedObjects.Contains(trigger.gameObject))
                    continue;

                string name = trigger.gameObject.name;

                // Skip "Stop" buttons - timer controls that flood the element list
                if (name == "Stop")
                    continue;

                string label = GetButtonText(trigger.gameObject, CleanName(name));

                MelonLogger.Msg($"[{NavigatorId}] EventTrigger: {name} - Text: '{label}'");

                AddElement(trigger.gameObject, label);
                addedObjects.Add(trigger.gameObject);
            }
        }

        private void DiscoverDuelSpecificElements(HashSet<GameObject> addedObjects)
        {
            MelonLogger.Msg($"[{NavigatorId}] Searching duel-specific elements...");

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
                    MelonLogger.Msg($"[{NavigatorId}] Named element: {name} - Text: '{label}'");

                    AddElement(obj, BuildLabel(label, Models.Strings.RoleButton, UIElementClassifier.ElementRole.Button), default, null, null, UIElementClassifier.ElementRole.Button);
                    addedObjects.Add(obj);
                }
            }
        }

        protected override string GetActivationAnnouncement()
        {
            int handCards = _zoneNavigator.HandCardCount;

            string core = Models.Strings.Duel_Started(handCards);
            return Strings.WithHint(core, "DuelKeybindingsHint");
        }

        protected override bool OnElementActivated(int index, GameObject element)
        {
            string name = element.name;

            if (name.Contains("PromptButton") || name.Contains("Styled") ||
                HasComponent(element, T.CustomButton) || HasComponent(element, "StyledButton"))
            {
                MelonLogger.Msg($"[{NavigatorId}] Using pointer click for: {name}");
                UIActivator.SimulatePointerClick(element);
                return true;
            }

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
            _hotHighlightNavigator.MonitorPromptButtons(_duelAnnouncer.TimeSinceLastPhaseChange);

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

            // Next, check for browser UI (scry, mulligan, damage assignment, etc.)
            // Browsers take high priority as they represent modal interactions
            _browserNavigator.Update();
            if (_browserNavigator.HandleInput())
                return true;

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
                MelonLogger.Msg($"[{NavigatorId}] P key pressed (shift={shift})");
                if (shift)
                {
                    var result = _priorityController.ToggleLockFullControl();
                    MelonLogger.Msg($"[{NavigatorId}] ToggleLockFullControl result: {result}");
                    if (result.HasValue)
                    {
                        _announcer.AnnounceInterrupt(result.Value ? Strings.FullControl_Locked : Strings.FullControl_Unlocked);
                    }
                }
                else
                {
                    var result = _priorityController.ToggleFullControl();
                    MelonLogger.Msg($"[{NavigatorId}] ToggleFullControl result: {result}");
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
                if (_browserNavigator.IsActive)
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

        #region Helper Methods

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
