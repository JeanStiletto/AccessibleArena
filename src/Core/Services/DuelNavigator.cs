using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.PanelDetection;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

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
        private PlayerPortraitNavigator _portraitNavigator;
        private DuelAnnouncer _duelAnnouncer;

        // DEPRECATED: Old separate navigators replaced by unified HotHighlightNavigator
        // TargetNavigator - Handled Tab cycling through valid spell targets (creatures, players)
        //                   Auto-entered when spell on stack had HotHighlight on battlefield
        //                   Used separate _isTargeting flag and zone scanning
        // HighlightNavigator - Handled Tab cycling through playable cards in hand
        //                      Scanned LocalHand and BattlefieldCardHolder for HotHighlight
        //                      Provided GetPrimaryButtonText() for game state when no cards playable
        // Both moved to: src/Core/Services/old/

        public override string NavigatorId => "Duel";
        public override string ScreenName => Strings.ScreenDuel;
        public override int Priority => 70; // Lower than PreBattle so it activates after

        // Let game handle Space natively (for Submit/Confirm, discard, etc.)
        protected override bool AcceptSpaceKey => false;

        public ZoneNavigator ZoneNavigator => _zoneNavigator;
        public HotHighlightNavigator HotHighlightNavigator => _hotHighlightNavigator;
        public BattlefieldNavigator BattlefieldNavigator => _battlefieldNavigator;
        public BrowserNavigator BrowserNavigator => _browserNavigator;
        public DuelAnnouncer DuelAnnouncer => _duelAnnouncer;
        public PlayerPortraitNavigator PortraitNavigator => _portraitNavigator;
        // DEPRECATED: public TargetNavigator TargetNavigator => _targetNavigator;
        // DEPRECATED: public HighlightNavigator HighlightNavigator => _highlightNavigator;

        public DuelNavigator(IAnnouncementService announcer) : base(announcer)
        {
            _zoneNavigator = new ZoneNavigator(announcer);

            // Unified highlight navigator - handles Tab for both playable cards AND targets
            // Trusts game's HotHighlight system to show correct items based on game state
            _hotHighlightNavigator = new HotHighlightNavigator(announcer, _zoneNavigator);

            _browserNavigator = new BrowserNavigator(announcer);
            _portraitNavigator = new PlayerPortraitNavigator(announcer);
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

            // DEPRECATED connections (were for old TargetNavigator):
            // _duelAnnouncer.SetTargetNavigator() - Was used to auto-enter targeting mode on spell cast
            // _zoneNavigator.SetTargetNavigator() - Was used to enter targeting after card plays
            // _battlefieldNavigator.SetTargetNavigator() - Was used for row navigation during targeting
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
            if (sceneName != "DuelScene")
            {
                _isWatching = false;
                _hasCenteredMouse = false; // Reset for next duel
                _zoneNavigator.Deactivate();
                _hotHighlightNavigator.Deactivate();
                _battlefieldNavigator.Deactivate();
                _portraitNavigator.Deactivate();
                _duelAnnouncer.Deactivate();
            }

            base.OnSceneChanged(sceneName);
        }

        protected override bool DetectScreen()
        {
            if (!_isWatching) return false;
            // HasDuelElements checks for Stop EventTriggers and duel-phase button text,
            // which only exist during actual gameplay (not during pre-game matchmaking).
            // Do NOT check HasPreGameCancelButton here - the in-duel Cancel button
            // also matches that check, which prevents re-activation after preemption.
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

            // 10. Explore player portrait elements (for accessibility development)
            ExplorePlayerPortraits();

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
                string elementType = GetSelectableType(selectable);

                MelonLogger.Msg($"[{NavigatorId}] Selectable ({typeName}): {name} - Text: '{label}'");

                AddElement(selectable.gameObject, $"{label}, {elementType}");
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

                if (mb.GetType().Name != "CustomButton")
                    continue;

                if (addedObjects.Contains(mb.gameObject))
                    continue;

                string name = mb.gameObject.name;
                string label = GetButtonText(mb.gameObject, name);

                MelonLogger.Msg($"[{NavigatorId}] CustomButton: {name} - Text: '{label}'");

                AddElement(mb.gameObject, $"{label}, button");
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

                    AddElement(obj, $"{label}, button");
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
                HasComponent(element, "CustomButton") || HasComponent(element, "StyledButton"))
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

            // NOTE: Ctrl key for full control investigated but not working in Color Challenge mode
            // See docs/AUTOSKIP_MODE_INVESTIGATION.md for details and attempted solutions

            // First, check for browser UI (scry, mulligan, damage assignment, etc.)
            // Browsers take highest priority as they represent modal interactions
            _browserNavigator.Update();
            if (_browserNavigator.HandleInput())
                return true;

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
                    _announcer.AnnounceInterrupt(Strings.NoCardToInspect);
                }
                return true;
            }

            // Battlefield navigation (A/R/B shortcuts and row-based navigation)
            if (_battlefieldNavigator.HandleInput())
                return true;

            // Delegate zone input handling to ZoneNavigator (C, G, X, S shortcuts)
            if (_zoneNavigator.HandleInput())
                return true;

            return base.HandleCustomInput();
        }

        #region Helper Methods

        private bool HasPreGameCancelButton()
        {
            foreach (var selectable in GameObject.FindObjectsOfType<Selectable>())
            {
                if (selectable == null || !selectable.gameObject.activeInHierarchy)
                    continue;

                string name = selectable.gameObject.name;
                if (name.Contains("PromptButton_Secondary"))
                {
                    string text = GetButtonText(selectable.gameObject, "");
                    if (text?.ToLowerInvariant().Contains("cancel") == true)
                        return true;
                }
            }
            return false;
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

        private string GetSelectableType(Selectable selectable)
        {
            if (selectable is Button) return "button";
            if (selectable is Toggle) return "checkbox";
            if (selectable is Slider) return "slider";
            if (selectable is Scrollbar) return "scrollbar";
            if (selectable is Dropdown) return "dropdown";
            if (selectable is InputField) return "text field";

            string typeName = selectable.GetType().Name.ToLower();
            if (typeName.Contains("button")) return "button";
            if (typeName.Contains("toggle")) return "checkbox";

            return "control";
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

        /// <summary>
        /// Explores player portrait UI elements to understand their structure.
        /// This is for development/debugging to make portraits accessible.
        /// </summary>
        private void ExplorePlayerPortraits()
        {
            MelonLogger.Msg($"[{NavigatorId}] === EXPLORING PLAYER PORTRAITS ===");

            // 1. Find MatchTimer components and explore their data
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy)
                    continue;

                string typeName = mb.GetType().Name;
                if (typeName == "MatchTimer")
                {
                    MelonLogger.Msg($"[{NavigatorId}] [Portrait] Found MatchTimer: {mb.gameObject.name}");

                    // Log all children recursively
                    MelonLogger.Msg($"[{NavigatorId}] [Portrait]   Full hierarchy:");
                    LogFullHierarchy(mb.gameObject, 4, "    ");

                    // Try to get timer properties
                    var type = mb.GetType();
                    foreach (var prop in type.GetProperties())
                    {
                        try
                        {
                            var val = prop.GetValue(mb);
                            MelonLogger.Msg($"[{NavigatorId}] [Portrait]   Property {prop.Name}: {val}");
                        }
                        catch { }
                    }
                }
            }

            // 2. Find Timer_Opponent and Timer_Player and explore full hierarchy
            var timerNames = new[] { "Timer_Opponent", "Timer_Player" };
            foreach (var timerName in timerNames)
            {
                var timerObj = GameObject.Find(timerName);
                if (timerObj != null)
                {
                    MelonLogger.Msg($"[{NavigatorId}] [Portrait] Found {timerName}:");
                    MelonLogger.Msg($"[{NavigatorId}] [Portrait]   Full hierarchy:");
                    LogFullHierarchy(timerObj, 6, "    ");

                    // Look for Text components in children
                    var texts = timerObj.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                    foreach (var text in texts)
                    {
                        MelonLogger.Msg($"[{NavigatorId}] [Portrait]   Text '{text.gameObject.name}': '{text.text}'");
                    }
                }
            }

            // 3. Search for Emotes container and children
            var emotesObj = GameObject.Find("Emotes");
            if (emotesObj != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] [Portrait] Found Emotes container:");
                MelonLogger.Msg($"[{NavigatorId}] [Portrait]   Full hierarchy:");
                LogFullHierarchy(emotesObj, 6, "    ");
            }

            // 4. Search for life-related text anywhere in scene
            MelonLogger.Msg($"[{NavigatorId}] [Portrait] Searching for life/health text...");
            foreach (var text in GameObject.FindObjectsOfType<UnityEngine.UI.Text>())
            {
                if (text == null || !text.gameObject.activeInHierarchy)
                    continue;

                string textContent = text.text?.Trim() ?? "";
                string objName = text.gameObject.name.ToLower();

                // Look for numeric values (could be life totals) or life-related names
                if (objName.Contains("life") || objName.Contains("health") ||
                    objName.Contains("point") || objName.Contains("damage") ||
                    (textContent.Length > 0 && textContent.Length <= 3 && int.TryParse(textContent, out _)))
                {
                    MelonLogger.Msg($"[{NavigatorId}] [Portrait]   Found text '{text.gameObject.name}': '{textContent}' " +
                        $"(parent: {text.transform.parent?.name ?? "none"})");
                }
            }

            // 5. Search for TextMeshPro elements (game might use TMP instead of UI.Text)
            MelonLogger.Msg($"[{NavigatorId}] [Portrait] Searching for TextMeshPro elements...");
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy)
                    continue;

                string typeName = mb.GetType().Name;
                if (typeName.Contains("TextMesh") || typeName.Contains("TMP"))
                {
                    var textProp = mb.GetType().GetProperty("text");
                    string textContent = "";
                    if (textProp != null)
                    {
                        try { textContent = textProp.GetValue(mb)?.ToString() ?? ""; } catch { }
                    }

                    string objName = mb.gameObject.name.ToLower();
                    string parentName = mb.transform.parent?.name ?? "none";

                    // Log all TMP texts for now to see what's available
                    if (parentName.ToLower().Contains("timer") || parentName.ToLower().Contains("player") ||
                        objName.Contains("life") || objName.Contains("health") ||
                        (textContent.Length > 0 && textContent.Length <= 3))
                    {
                        MelonLogger.Msg($"[{NavigatorId}] [Portrait]   TMP '{mb.gameObject.name}': '{textContent}' " +
                            $"(parent: {parentName}, type: {typeName})");
                    }
                }
            }

            // 6. Explore GameManager for player life data
            ExploreGameManagerForLife();

            // 7. Search for life counter UI elements on screen
            ExploreLifeCounterUI();

            MelonLogger.Msg($"[{NavigatorId}] === END PORTRAIT EXPLORATION ===");
        }

        private void ExploreGameManagerForLife()
        {
            MelonLogger.Msg($"[{NavigatorId}] [Life] Searching for GameManager and player life data...");

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;

                var type = mb.GetType();
                string typeName = type.Name;

                if (typeName == "GameManager")
                {
                    MelonLogger.Msg($"[{NavigatorId}] [Life] Found GameManager: {mb.gameObject.name}");

                    // Log all properties
                    foreach (var prop in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    {
                        try
                        {
                            var val = prop.GetValue(mb);
                            var valStr = val?.ToString() ?? "null";
                            if (valStr.Length > 100) valStr = valStr.Substring(0, 100) + "...";
                            MelonLogger.Msg($"[{NavigatorId}] [Life]   Property {prop.Name} ({prop.PropertyType.Name}): {valStr}");

                            // If it's Players or similar, dig deeper
                            if (prop.Name.Contains("Player") || prop.Name.Contains("Life") || prop.Name.Contains("State"))
                            {
                                ExploreObjectForLife(val, prop.Name, 2);
                            }
                        }
                        catch { }
                    }

                    // Also check fields
                    foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                    {
                        if (field.Name.Contains("player") || field.Name.Contains("Player") ||
                            field.Name.Contains("life") || field.Name.Contains("Life"))
                        {
                            try
                            {
                                var val = field.GetValue(mb);
                                MelonLogger.Msg($"[{NavigatorId}] [Life]   Field {field.Name}: {val}");
                                ExploreObjectForLife(val, field.Name, 2);
                            }
                            catch { }
                        }
                    }
                    break;
                }
            }
        }

        private void ExploreObjectForLife(object obj, string context, int maxDepth, int depth = 0)
        {
            if (obj == null || depth >= maxDepth) return;

            var type = obj.GetType();
            string indent = new string(' ', (depth + 1) * 2);

            // Check if it's enumerable (like a list of players)
            if (obj is System.Collections.IEnumerable enumerable && !(obj is string))
            {
                int index = 0;
                foreach (var item in enumerable)
                {
                    if (item == null) continue;
                    MelonLogger.Msg($"[{NavigatorId}] [Life] {indent}{context}[{index}]: {item}");
                    ExploreObjectForLife(item, $"{context}[{index}]", maxDepth, depth + 1);
                    index++;
                    if (index >= 5) break; // Limit to first 5 items
                }
                return;
            }

            // Look for life-related properties
            foreach (var prop in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (prop.Name.Contains("Life") || prop.Name.Contains("Health") ||
                    prop.Name.Contains("Player") || prop.Name.Contains("Id") ||
                    prop.Name == "Name" || prop.Name == "DisplayName")
                {
                    try
                    {
                        var val = prop.GetValue(obj);
                        MelonLogger.Msg($"[{NavigatorId}] [Life] {indent}{context}.{prop.Name}: {val}");
                    }
                    catch { }
                }
            }
        }

        private void ExploreLifeCounterUI()
        {
            MelonLogger.Msg($"[{NavigatorId}] [LifeUI] Searching for life counter UI elements...");

            // Search for GameObjects with life-related names
            string[] lifePatterns = { "Life", "Counter", "Score", "Health", "Point", "Total" };
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                string name = go.name;
                foreach (var pattern in lifePatterns)
                {
                    if (name.Contains(pattern))
                    {
                        MelonLogger.Msg($"[{NavigatorId}] [LifeUI] Found '{pattern}' object: {name}");
                        MelonLogger.Msg($"[{NavigatorId}] [LifeUI]   Path: {MenuDebugHelper.GetGameObjectPath(go)}");
                        LogFullHierarchy(go, 3, "    ");

                        // Check for text components
                        var tmps = go.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
                        foreach (var tmp in tmps)
                        {
                            MelonLogger.Msg($"[{NavigatorId}] [LifeUI]   TMP: '{tmp.gameObject.name}' = '{tmp.text}'");
                        }
                        break;
                    }
                }
            }

            // Search all TextMeshPro for numeric values that could be life (10-40 range typically)
            MelonLogger.Msg($"[{NavigatorId}] [LifeUI] Searching for numeric TMP text (potential life totals)...");
            foreach (var tmp in GameObject.FindObjectsOfType<TMPro.TextMeshProUGUI>())
            {
                if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;

                string text = tmp.text?.Trim() ?? "";
                if (int.TryParse(text, out int num) && num >= 0 && num <= 99)
                {
                    string path = MenuDebugHelper.GetGameObjectPath(tmp.gameObject);
                    MelonLogger.Msg($"[{NavigatorId}] [LifeUI] Numeric TMP: '{tmp.gameObject.name}' = {num} (path: {path})");
                }
            }

            // Search for components with "Life" or "Counter" in their type name
            MelonLogger.Msg($"[{NavigatorId}] [LifeUI] Searching for life-related components...");
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                string typeName = mb.GetType().Name;
                if (typeName.Contains("Life") || typeName.Contains("Counter") || typeName.Contains("Score") || typeName.Contains("Health"))
                {
                    MelonLogger.Msg($"[{NavigatorId}] [LifeUI] Found component: {typeName} on {mb.gameObject.name}");

                    // Log its properties
                    foreach (var prop in mb.GetType().GetProperties())
                    {
                        try
                        {
                            var val = prop.GetValue(mb);
                            MelonLogger.Msg($"[{NavigatorId}] [LifeUI]   {prop.Name}: {val}");
                        }
                        catch { }
                    }
                }
            }
        }

        private void LogFullHierarchy(GameObject obj, int maxDepth, string baseIndent, int currentDepth = 0)
        {
            if (currentDepth >= maxDepth) return;

            string indent = baseIndent + new string(' ', currentDepth * 2);
            foreach (Transform child in obj.transform)
            {
                var components = child.GetComponents<Component>();
                var componentNames = components.Where(c => c != null).Select(c => c.GetType().Name);
                MelonLogger.Msg($"[{NavigatorId}] [Portrait] {indent}- {child.name} [{string.Join(", ", componentNames)}]");
                LogFullHierarchy(child.gameObject, maxDepth, baseIndent, currentDepth + 1);
            }
        }

        #endregion
    }
}
