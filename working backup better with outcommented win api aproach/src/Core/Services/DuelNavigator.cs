using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MelonLoader;
using MTGAAccessibility.Core.Interfaces;
using MTGAAccessibility.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace MTGAAccessibility.Core.Services
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
        private bool _isWatching;
        private ZoneNavigator _zoneNavigator;
        private TargetNavigator _targetNavigator;
        private HighlightNavigator _highlightNavigator;
        private DuelAnnouncer _duelAnnouncer;

        public override string NavigatorId => "Duel";
        public override string ScreenName => "Duel";
        public override int Priority => 70; // Lower than PreBattle so it activates after

        public ZoneNavigator ZoneNavigator => _zoneNavigator;
        public TargetNavigator TargetNavigator => _targetNavigator;
        public HighlightNavigator HighlightNavigator => _highlightNavigator;
        public DuelAnnouncer DuelAnnouncer => _duelAnnouncer;

        public DuelNavigator(IAnnouncementService announcer) : base(announcer)
        {
            _zoneNavigator = new ZoneNavigator(announcer);
            _targetNavigator = new TargetNavigator(announcer, _zoneNavigator);
            _highlightNavigator = new HighlightNavigator(announcer, _zoneNavigator);
            _duelAnnouncer = new DuelAnnouncer(announcer);

            // Connect DuelAnnouncer to TargetNavigator for event handling
            _duelAnnouncer.SetTargetNavigator(_targetNavigator);

            // Connect ZoneNavigator to TargetNavigator for targeting mode after card plays
            _zoneNavigator.SetTargetNavigator(_targetNavigator);
        }

        /// <summary>
        /// Called by MTGAAccessibilityMod when DuelScene loads.
        /// </summary>
        public void OnDuelSceneLoaded()
        {
            MelonLogger.Msg($"[{NavigatorId}] DuelScene loaded - starting to watch for duel elements");
            _isWatching = true;
        }

        public override void OnSceneChanged(string sceneName)
        {
            if (sceneName != "DuelScene")
            {
                _isWatching = false;
                _zoneNavigator.Deactivate();
                _targetNavigator.ExitTargetMode();
                _highlightNavigator.Deactivate();
                _duelAnnouncer.Deactivate();
            }

            base.OnSceneChanged(sceneName);
        }

        protected override bool DetectScreen()
        {
            if (!_isWatching) return false;
            if (HasPreGameCancelButton()) return false;
            return HasDuelElements();
        }

        protected override void DiscoverElements()
        {
            var addedObjects = new HashSet<GameObject>();

            MelonLogger.Msg($"[{NavigatorId}] === DUEL UI DISCOVERY START ===");

            // 1. Activate zone navigator and discover zones
            _zoneNavigator.Activate();
            _zoneNavigator.LogZoneSummary();

            // 2. Activate highlight navigator for Tab cycling through playable cards
            _highlightNavigator.Activate();

            // 3. Activate duel announcer with local player ID
            // We detect local player by finding LocalHand zone and getting its OwnerId
            uint localPlayerId = DetectLocalPlayerId();
            _duelAnnouncer.Activate(localPlayerId);

            // 2. Find all Selectables (StyledButton, Button, Toggle, etc.)
            DiscoverSelectables(addedObjects);

            // 3. Find all CustomButtons
            DiscoverCustomButtons(addedObjects);

            // 4. Find all EventTriggers (skip non-useful ones like "Stop")
            DiscoverEventTriggers(addedObjects);

            // 5. Find specific duel elements by name patterns
            DiscoverDuelSpecificElements(addedObjects);

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

            return $"Duel started. {handCards} cards in hand. " +
                   $"Tab cycles playable cards. " +
                   $"C for hand, B for battlefield, G for graveyard, X for exile, S for stack. " +
                   $"Arrows navigate cards in zone.";
        }

        protected override bool OnElementActivated(int index, GameObject element)
        {
            string name = element.name;

            if (name.Contains("PromptButton") || name.Contains("Styled") ||
                HasComponent(element, "CustomButton") || HasComponent(element, "StyledButton"))
            {
                MelonLogger.Msg($"[{NavigatorId}] Using pointer click for: {name}");
                UIActivator.SimulatePointerClick(element);

                string label = GetButtonText(element, CleanName(name));
                _announcer.Announce($"{label} activated", AnnouncementPriority.Normal);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Handles zone navigation, target selection, and playable card cycling input.
        /// Priority: TargetNavigator (targeting mode) > HighlightNavigator (Tab for playable cards)
        ///           > ZoneNavigator (zone shortcuts and arrows) > base behavior
        /// </summary>
        protected override bool HandleCustomInput()
        {
            // First, let TargetNavigator handle input if in targeting mode
            // This allows Tab to cycle targets during spell targeting
            if (_targetNavigator.HandleInput())
                return true;

            // Next, let HighlightNavigator handle Tab to cycle playable cards
            // This replaces the default button-cycling Tab behavior
            if (_highlightNavigator.HandleInput())
                return true;

            // Delegate zone input handling to ZoneNavigator
            if (_zoneNavigator.HandleInput())
                return true;

            return base.HandleCustomInput();
        }

        #region Helper Methods

        /// <summary>
        /// Detects the local player ID by finding the LocalHand zone.
        /// The OwnerId of LocalHand is always the local player.
        /// </summary>
        private uint DetectLocalPlayerId()
        {
            // Look for LocalHand or LocalLibrary zone holders
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy)
                    continue;

                string name = go.name;
                if (name.Contains("LocalHand") || name.Contains("LocalLibrary"))
                {
                    // Parse OwnerId from zone metadata in name
                    // Format: "LocalHand_Desktop_16x9 ZoneId: #31 | Type: Hand | OwnerId: #1"
                    var match = System.Text.RegularExpressions.Regex.Match(name, @"OwnerId:\s*#(\d+)");
                    if (match.Success && uint.TryParse(match.Groups[1].Value, out uint ownerId))
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Detected local player ID: #{ownerId}");
                        return ownerId;
                    }
                }
            }

            // Default to 1 if not found (usually correct)
            MelonLogger.Warning($"[{NavigatorId}] Could not detect local player ID, defaulting to #1");
            return 1;
        }

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

        #endregion
    }
}
