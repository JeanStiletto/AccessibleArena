using MelonLoader;
using MTGAAccessibility.Core.Interfaces;
using MTGAAccessibility.Core.Models;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// Announces duel events to the screen reader.
    /// Receives events from the Harmony patch on UXEventQueue.EnqueuePending.
    ///
    /// PRIVACY RULES:
    /// - Only announce publicly visible information
    /// - Never reveal opponent's hand contents
    /// - Never reveal library contents (top of deck, etc.)
    /// - Only announce what a sighted player could see
    /// </summary>
    public class DuelAnnouncer
    {
        public static DuelAnnouncer Instance { get; private set; }

        private readonly IAnnouncementService _announcer;
        private bool _isActive;

        // Event type detection - cached for performance
        private readonly Dictionary<string, DuelEventType> _eventTypeMap;

        // Track recent announcements to avoid spam
        private string _lastAnnouncement;
        private DateTime _lastAnnouncementTime;
        private const float DUPLICATE_THRESHOLD_SECONDS = 0.5f;

        // Debug: track seen event types
        private readonly HashSet<string> _seenEventTypes = new HashSet<string>();

        // Track zone card counts to detect changes
        private readonly Dictionary<string, int> _zoneCounts = new Dictionary<string, int>();

        // Track local player ID for filtering events
        private uint _localPlayerId;

        // Reference to TargetNavigator for target selection handling
        private TargetNavigator _targetNavigator;

        // Track when a spell was cast or resolved (for skipping targeting mode on non-targeted cards)
        private DateTime _lastSpellCastTime = DateTime.MinValue;
        private DateTime _lastSpellResolvedTime = DateTime.MinValue;

        /// <summary>
        /// Returns true if a spell was cast (went on stack) within the last specified milliseconds.
        /// Used to skip targeting mode - if spell went on stack, it will resolve on its own.
        /// </summary>
        public bool DidSpellCastRecently(int withinMs = 500)
        {
            return (DateTime.Now - _lastSpellCastTime).TotalMilliseconds < withinMs;
        }

        /// <summary>
        /// Returns true if a spell resolved or a permanent entered battlefield within the last specified milliseconds.
        /// Used to skip targeting mode for lands and non-targeted cards.
        /// </summary>
        public bool DidSpellResolveRecently(int withinMs = 500)
        {
            return (DateTime.Now - _lastSpellResolvedTime).TotalMilliseconds < withinMs;
        }

        /// <summary>
        /// Sets the TargetNavigator reference for target selection events.
        /// </summary>
        public void SetTargetNavigator(TargetNavigator navigator)
        {
            _targetNavigator = navigator;
        }

        public DuelAnnouncer(IAnnouncementService announcer)
        {
            _announcer = announcer;
            _eventTypeMap = BuildEventTypeMap();
            Instance = this;

            MelonLogger.Msg("[DuelAnnouncer] Initialized");
        }

        /// <summary>
        /// Activates the announcer when entering a duel.
        /// </summary>
        public void Activate(uint localPlayerId)
        {
            _isActive = true;
            _localPlayerId = localPlayerId;
            _zoneCounts.Clear();
            _seenEventTypes.Clear();
            MelonLogger.Msg($"[DuelAnnouncer] Activated for player #{localPlayerId}");
        }

        /// <summary>
        /// Deactivates the announcer when leaving a duel.
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
            MelonLogger.Msg("[DuelAnnouncer] Deactivated");
        }

        /// <summary>
        /// Called by the Harmony patch when a game event is enqueued.
        /// </summary>
        public void OnGameEvent(object uxEvent)
        {
            if (!_isActive || uxEvent == null) return;

            try
            {
                var typeName = uxEvent.GetType().Name;

                // Temporarily log ALL event types to discover what's available
                if (!_seenEventTypes.Contains(typeName))
                {
                    _seenEventTypes.Add(typeName);
                    MelonLogger.Msg($"[DuelAnnouncer] NEW EVENT TYPE: {typeName}");
                }

                var eventType = ClassifyEvent(uxEvent);
                if (eventType == DuelEventType.Ignored) return;

                var announcement = BuildAnnouncement(eventType, uxEvent);
                if (string.IsNullOrEmpty(announcement)) return;

                // Avoid duplicate announcements
                if (IsDuplicateAnnouncement(announcement)) return;

                var priority = GetPriority(eventType);
                _announcer.Announce(announcement, priority);

                _lastAnnouncement = announcement;
                _lastAnnouncementTime = DateTime.Now;

                MelonLogger.Msg($"[DuelAnnouncer] {eventType}: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error processing event: {ex.Message}");
            }
        }

        /// <summary>
        /// Classifies a UXEvent into our DuelEventType categories.
        /// </summary>
        private DuelEventType ClassifyEvent(object uxEvent)
        {
            var typeName = uxEvent.GetType().Name;

            if (_eventTypeMap.TryGetValue(typeName, out var eventType))
                return eventType;

            // Log unknown event types for debugging (only once per type)
            // MelonLogger.Msg($"[DuelAnnouncer] Unknown event type: {typeName}");

            return DuelEventType.Ignored;
        }

        /// <summary>
        /// Builds the announcement text for an event.
        /// </summary>
        private string BuildAnnouncement(DuelEventType eventType, object uxEvent)
        {
            switch (eventType)
            {
                case DuelEventType.TurnChange:
                    return BuildTurnChangeAnnouncement(uxEvent);

                case DuelEventType.ZoneTransfer:
                    return BuildZoneTransferAnnouncement(uxEvent);

                case DuelEventType.LifeChange:
                    return BuildLifeChangeAnnouncement(uxEvent);

                case DuelEventType.DamageDealt:
                    return BuildDamageAnnouncement(uxEvent);

                case DuelEventType.ManaProduced:
                    return BuildManaAnnouncement(uxEvent);

                case DuelEventType.PhaseChange:
                    return BuildPhaseChangeAnnouncement(uxEvent);

                case DuelEventType.CardRevealed:
                    return BuildRevealAnnouncement(uxEvent);

                case DuelEventType.CountersChanged:
                    return BuildCountersAnnouncement(uxEvent);

                case DuelEventType.GameEnd:
                    return BuildGameEndAnnouncement(uxEvent);

                case DuelEventType.Combat:
                    return BuildCombatAnnouncement(uxEvent);

                case DuelEventType.TargetSelection:
                    return HandleTargetSelectionEvent(uxEvent);

                case DuelEventType.TargetConfirmed:
                    return HandleTargetConfirmedEvent(uxEvent);

                default:
                    return null;
            }
        }

        #region Announcement Builders

        private string BuildTurnChangeAnnouncement(object uxEvent)
        {
            try
            {
                var type = uxEvent.GetType();

                // Get _turnNumber field - could be int, uint, or other numeric type
                var turnNumberField = type.GetField("_turnNumber", BindingFlags.NonPublic | BindingFlags.Instance);
                int turnNumber = 0;
                if (turnNumberField != null)
                {
                    var turnValue = turnNumberField.GetValue(uxEvent);
                    if (turnValue != null)
                    {
                        // Convert whatever numeric type it is to int
                        turnNumber = Convert.ToInt32(turnValue);
                    }
                }

                // Get _activePlayer field - it's a Player object, we need to parse its string representation
                // Format: "Player: 1 (LocalPlayer)" or "Player: 2 (Opponent)"
                var activePlayerField = type.GetField("_activePlayer", BindingFlags.NonPublic | BindingFlags.Instance);
                bool isYourTurn = false;
                if (activePlayerField != null)
                {
                    var playerObj = activePlayerField.GetValue(uxEvent);
                    if (playerObj != null)
                    {
                        string playerStr = playerObj.ToString();
                        // Check if it contains "LocalPlayer"
                        isYourTurn = playerStr.Contains("LocalPlayer");
                    }
                }

                string turnText = isYourTurn ? "Your turn" : "Opponent's turn";

                if (turnNumber > 0)
                    return $"Turn {turnNumber}. {turnText}";
                else
                    return turnText;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] TurnChange error: {ex.Message}");
                return "Turn changed";
            }
        }

        private string BuildZoneTransferAnnouncement(object uxEvent)
        {
            try
            {
                var typeName = uxEvent.GetType().Name;

                // Handle UpdateZoneUXEvent - has _zone field with zone state
                if (typeName == "UpdateZoneUXEvent")
                {
                    return HandleUpdateZoneEvent(uxEvent);
                }

                // Handle ZoneTransferGroup - has _reasonZonePairs with transfer info
                if (typeName == "ZoneTransferGroup")
                {
                    return HandleZoneTransferGroup(uxEvent);
                }

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] ZoneTransfer error: {ex.Message}");
                return null;
            }
        }

        private string HandleUpdateZoneEvent(object uxEvent)
        {
            // Get _zone field which contains string like "Hand (PlayerPlayer: 1 (LocalPlayer), 6 cards)"
            var zoneField = uxEvent.GetType().GetField("_zone", BindingFlags.NonPublic | BindingFlags.Instance);
            if (zoneField == null) return null;

            var zoneObj = zoneField.GetValue(uxEvent);
            if (zoneObj == null) return null;

            string zoneStr = zoneObj.ToString();

            // Parse zone info - track changes
            // Format: "Hand (PlayerPlayer: 1 (LocalPlayer), 6 cards)" or "Battlefield (Player, 2 cards)"
            bool isLocal = zoneStr.Contains("LocalPlayer") || (!zoneStr.Contains("Opponent") && zoneStr.Contains("Player,"));
            bool isOpponent = zoneStr.Contains("Opponent");

            // Extract zone name and card count
            var zoneMatch = System.Text.RegularExpressions.Regex.Match(zoneStr, @"^(\w+)\s*\(");
            var countMatch = System.Text.RegularExpressions.Regex.Match(zoneStr, @"(\d+)\s*cards?\)");

            if (!zoneMatch.Success) return null;

            string zoneName = zoneMatch.Groups[1].Value;
            int cardCount = countMatch.Success ? int.Parse(countMatch.Groups[1].Value) : 0;

            // Track zone counts and detect changes
            string zoneKey = (isOpponent ? "Opp_" : "Local_") + zoneName;

            if (_zoneCounts.TryGetValue(zoneKey, out int previousCount))
            {
                int diff = cardCount - previousCount;
                _zoneCounts[zoneKey] = cardCount;

                if (diff == 0) return null;

                // Detect meaningful changes
                if (zoneName == "Hand")
                {
                    if (diff > 0)
                    {
                        if (isLocal)
                            return $"Drew {diff} card{(diff > 1 ? "s" : "")}";
                        else
                            return $"Opponent drew {diff} card{(diff > 1 ? "s" : "")}";
                    }
                    else if (diff < 0 && isOpponent)
                    {
                        // Opponent played a card (we'll see what via battlefield)
                        return $"Opponent played a card";
                    }
                    // Local hand decreasing = we played something, no need to announce (we know)
                }
                else if (zoneName == "Battlefield")
                {
                    if (diff > 0)
                    {
                        if (isOpponent)
                            return $"Opponent: {diff} permanent{(diff > 1 ? "s" : "")} entered battlefield";
                        // Local battlefield increase - we played it, track as resolved
                        _lastSpellResolvedTime = DateTime.Now;
                    }
                    else if (diff < 0)
                    {
                        int removed = Math.Abs(diff);
                        if (isOpponent)
                            return $"Opponent lost {removed} permanent{(removed > 1 ? "s" : "")}";
                        else
                            return $"{removed} of your permanent{(removed > 1 ? "s" : "")} left battlefield";
                    }
                }
                else if (zoneName == "Graveyard" && diff > 0)
                {
                    if (isOpponent)
                        return $"Card went to opponent's graveyard";
                    else
                        return $"Card went to your graveyard";
                }
                else if (zoneName == "Stack")
                {
                    if (diff > 0)
                    {
                        if (isOpponent)
                            return "Opponent cast a spell";
                        // Local player cast a spell - track for targeting mode detection
                        _lastSpellCastTime = DateTime.Now;
                        MelonLogger.Msg("[DuelAnnouncer] Local spell cast (stack increased)");
                    }
                    else if (diff < 0)
                    {
                        // Track resolution time for targeting mode detection
                        _lastSpellResolvedTime = DateTime.Now;
                        return "Spell resolved";
                    }
                }
                else if (zoneName == "Exile" && diff > 0)
                {
                    return $"Card exiled";
                }
            }
            else
            {
                // First time seeing this zone, just record it
                _zoneCounts[zoneKey] = cardCount;
            }

            return null;
        }

        private string HandleZoneTransferGroup(object uxEvent)
        {
            try
            {
                // Get _reasonZonePairs field
                var reasonPairsField = uxEvent.GetType().GetField("_reasonZonePairs", BindingFlags.NonPublic | BindingFlags.Instance);
                if (reasonPairsField == null) return null;

                var reasonPairs = reasonPairsField.GetValue(uxEvent) as System.Collections.IList;
                if (reasonPairs == null || reasonPairs.Count == 0) return null;

                // Log the reason pairs for debugging
                foreach (var pair in reasonPairs)
                {
                    MelonLogger.Msg($"[DuelAnnouncer] ReasonZonePair: {pair}");
                }

                // For now just log - we'll parse this properly once we see the format
                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] ZoneTransferGroup error: {ex.Message}");
                return null;
            }
        }

        private string ClassifyZoneTransfer(string sourceZone, string destZone, bool isLocal, object uxEvent)
        {
            // PRIVACY: Never announce card names for opponent's hidden zones
            string cardName = isLocal ? TryGetCardName(uxEvent) : null;

            // Library -> Hand = Draw
            if (sourceZone == "Library" && destZone == "Hand")
            {
                if (isLocal)
                    return cardName != null ? $"Drew {cardName}" : "Drew a card";
                else
                    return "Opponent drew a card";
            }

            // Hand -> Battlefield = Play (creature/permanent)
            if (sourceZone == "Hand" && destZone == "Battlefield")
            {
                // Card name is public once on battlefield
                cardName = TryGetCardName(uxEvent);
                if (isLocal)
                    return cardName != null ? $"Played {cardName}" : "Played a card";
                else
                    return cardName != null ? $"Opponent played {cardName}" : "Opponent played a card";
            }

            // Hand -> Stack = Cast spell
            if (sourceZone == "Hand" && destZone == "Stack")
            {
                cardName = TryGetCardName(uxEvent);
                if (isLocal)
                    return cardName != null ? $"Cast {cardName}" : "Cast a spell";
                else
                    return cardName != null ? $"Opponent cast {cardName}" : "Opponent cast a spell";
            }

            // Battlefield -> Graveyard = Creature died
            if (sourceZone == "Battlefield" && destZone == "Graveyard")
            {
                cardName = TryGetCardName(uxEvent);
                return cardName != null ? $"{cardName} died" : "Creature died";
            }

            // Battlefield -> Exile = Exiled
            if (sourceZone == "Battlefield" && destZone == "Exile")
            {
                cardName = TryGetCardName(uxEvent);
                return cardName != null ? $"{cardName} exiled" : "Permanent exiled";
            }

            // Stack -> Graveyard = Spell resolved or countered
            if (sourceZone == "Stack" && destZone == "Graveyard")
            {
                cardName = TryGetCardName(uxEvent);
                return cardName != null ? $"{cardName} resolved" : "Spell resolved";
            }

            // Graveyard -> Hand = Returned to hand
            if (sourceZone == "Graveyard" && destZone == "Hand")
            {
                if (isLocal)
                {
                    cardName = TryGetCardName(uxEvent);
                    return cardName != null ? $"{cardName} returned to hand" : "Card returned to hand";
                }
                else
                {
                    return "Opponent returned a card to hand";
                }
            }

            // Graveyard -> Battlefield = Reanimated
            if (sourceZone == "Graveyard" && destZone == "Battlefield")
            {
                cardName = TryGetCardName(uxEvent);
                string who = isLocal ? "" : "Opponent ";
                return cardName != null ? $"{who}Reanimated {cardName}" : $"{who}Reanimated a creature";
            }

            // Other zone transfers - don't announce to avoid spam
            return null;
        }

        private string BuildLifeChangeAnnouncement(object uxEvent)
        {
            try
            {
                var playerId = GetPropertyValue<uint>(uxEvent, "PlayerId");
                var newLife = GetPropertyValue<int>(uxEvent, "NewLifeTotal");
                var change = GetPropertyValue<int>(uxEvent, "LifeChange");

                bool isLocal = playerId == _localPlayerId;
                string who = isLocal ? "You" : "Opponent";

                if (change != 0)
                {
                    string direction = change > 0 ? "gained" : "lost";
                    return $"{who} {direction} {Math.Abs(change)} life. Now at {newLife}";
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private string BuildDamageAnnouncement(object uxEvent)
        {
            try
            {
                var damage = GetPropertyValue<int>(uxEvent, "DamageAmount");
                if (damage <= 0) return null;

                // Try to identify target
                var targetName = TryGetTargetName(uxEvent);

                if (!string.IsNullOrEmpty(targetName))
                    return $"{damage} damage to {targetName}";

                return null;
            }
            catch
            {
                return null;
            }
        }

        private string BuildManaAnnouncement(object uxEvent)
        {
            // Mana production is very frequent - might want to skip or batch
            // For now, skip to avoid spam
            return null;
        }

        private string BuildPhaseChangeAnnouncement(object uxEvent)
        {
            try
            {
                var type = uxEvent.GetType();
                string phase = null;
                string step = null;

                // Get Phase from auto-property backing field: <Phase>k__BackingField
                var phaseField = type.GetField("<Phase>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                if (phaseField != null)
                {
                    var phaseObj = phaseField.GetValue(uxEvent);
                    if (phaseObj != null)
                        phase = phaseObj.ToString();
                }

                // Get Step from auto-property backing field: <Step>k__BackingField
                var stepField = type.GetField("<Step>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                if (stepField != null)
                {
                    var stepObj = stepField.GetValue(uxEvent);
                    if (stepObj != null)
                        step = stepObj.ToString();
                }

                // Announce based on phase and step combinations
                // Phase values: Beginning, Main1, Combat, Main2, Ending
                // Step values: None, Untap, Upkeep, Draw, DeclareAttack, DeclareBlock, CombatDamage, EndCombat, End, Cleanup

                if (phase == "Main1")
                    return "First main phase";

                if (phase == "Main2")
                    return "Second main phase";

                if (phase == "Combat")
                {
                    if (step == "DeclareAttack")
                        return "Declare attackers";
                    if (step == "DeclareBlock")
                        return "Declare blockers";
                    if (step == "CombatDamage")
                        return "Combat damage";
                    if (step == "EndCombat")
                        return "End of combat";
                    if (step == "None")
                        return "Combat phase";
                }

                if (phase == "Ending")
                {
                    if (step == "End")
                        return "End step";
                    // Skip cleanup to reduce spam
                }

                // Skip Beginning phase (Untap, Upkeep, Draw) to reduce spam
                // Turn change announcement already tells us it's our turn

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] PhaseChange error: {ex.Message}");
                return null;
            }
        }

        private string BuildRevealAnnouncement(object uxEvent)
        {
            try
            {
                // Cards revealed are public information
                var cardName = TryGetCardName(uxEvent);
                if (!string.IsNullOrEmpty(cardName))
                    return $"Revealed {cardName}";

                return null;
            }
            catch
            {
                return null;
            }
        }

        private string BuildCountersAnnouncement(object uxEvent)
        {
            try
            {
                var counterType = GetPropertyValue<string>(uxEvent, "CounterType");
                var change = GetPropertyValue<int>(uxEvent, "Change");
                var cardName = TryGetCardName(uxEvent);

                if (change == 0) return null;

                string action = change > 0 ? "gained" : "lost";
                string target = !string.IsNullOrEmpty(cardName) ? cardName : "creature";

                return $"{target} {action} {Math.Abs(change)} {counterType} counter{(Math.Abs(change) != 1 ? "s" : "")}";
            }
            catch
            {
                return null;
            }
        }

        private string BuildGameEndAnnouncement(object uxEvent)
        {
            try
            {
                var winnerId = GetPropertyValue<uint>(uxEvent, "WinnerId");
                bool youWon = winnerId == _localPlayerId;

                return youWon ? "Victory!" : "Defeat";
            }
            catch
            {
                return "Game ended";
            }
        }

        private string BuildCombatAnnouncement(object uxEvent)
        {
            try
            {
                var type = uxEvent.GetType();
                var typeName = type.Name;

                if (typeName == "ToggleCombatUXEvent")
                {
                    // Check _CombatMode field: None, CombatBegun, CreaturesActive
                    var combatModeField = type.GetField("_CombatMode", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (combatModeField != null)
                    {
                        var modeValue = combatModeField.GetValue(uxEvent)?.ToString();
                        if (modeValue == "CombatBegun")
                            return "Combat begins";
                        // CreaturesActive and None - skip to avoid spam
                    }
                    return null;
                }

                if (typeName == "AttackLobUXEvent")
                {
                    // Get attacker ID for potential future use
                    var attackerIdField = type.GetField("_attackerId", BindingFlags.NonPublic | BindingFlags.Instance);
                    // For now just announce the attack
                    return "Attacker declared";
                }

                if (typeName == "AttackDecrementUXEvent")
                {
                    return "Attacker removed";
                }

                // CombatFrame and others - skip to avoid spam
                return null;
            }
            catch
            {
                return null;
            }
        }

        private string HandleTargetSelectionEvent(object uxEvent)
        {
            try
            {
                MelonLogger.Msg("[DuelAnnouncer] Target selection event detected");

                // Notify TargetNavigator to enter targeting mode
                if (_targetNavigator != null)
                {
                    _targetNavigator.EnterTargetMode();
                    // TargetNavigator will handle the announcement
                    return null;
                }
                else
                {
                    // Fallback if TargetNavigator not set
                    return "Select a target";
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] TargetSelection error: {ex.Message}");
                return "Select a target";
            }
        }

        private string HandleTargetConfirmedEvent(object uxEvent)
        {
            try
            {
                MelonLogger.Msg("[DuelAnnouncer] Target confirmed event detected");

                // Notify TargetNavigator to exit targeting mode
                _targetNavigator?.ExitTargetMode();

                // Don't announce - the target selection already announced
                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] TargetConfirmed error: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Helper Methods

        private string TryGetCardName(object uxEvent)
        {
            try
            {
                // Try various property names that might contain card info
                var cardName = GetPropertyValue<string>(uxEvent, "CardName");
                if (!string.IsNullOrEmpty(cardName)) return cardName;

                var grpId = GetPropertyValue<uint>(uxEvent, "GrpId");
                if (grpId > 0)
                {
                    // TODO: Look up card name from grpId using game's card database
                    // For now, return null - we'll implement card lookup later
                    return null;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private string TryGetTargetName(object uxEvent)
        {
            try
            {
                var targetId = GetPropertyValue<uint>(uxEvent, "TargetId");
                if (targetId == _localPlayerId)
                    return "you";

                // Could be opponent or a creature - try to get more info
                var targetName = GetPropertyValue<string>(uxEvent, "TargetName");
                if (!string.IsNullOrEmpty(targetName))
                    return targetName;

                return "opponent";
            }
            catch
            {
                return null;
            }
        }

        private string GetZoneType(object uxEvent, string propertyName)
        {
            try
            {
                var zone = GetPropertyValue<object>(uxEvent, propertyName);
                return zone?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private T GetPropertyValue<T>(object obj, string propertyName)
        {
            if (obj == null) return default;

            var type = obj.GetType();

            // Try property
            var prop = type.GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null)
            {
                var value = prop.GetValue(obj);
                if (value is T typed)
                    return typed;
            }

            // Try field
            var field = type.GetField(propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                var value = field.GetValue(obj);
                if (value is T typed)
                    return typed;
            }

            return default;
        }

        private bool IsDuplicateAnnouncement(string announcement)
        {
            if (string.IsNullOrEmpty(_lastAnnouncement))
                return false;

            if (announcement != _lastAnnouncement)
                return false;

            var elapsed = (DateTime.Now - _lastAnnouncementTime).TotalSeconds;
            return elapsed < DUPLICATE_THRESHOLD_SECONDS;
        }

        private AnnouncementPriority GetPriority(DuelEventType eventType)
        {
            switch (eventType)
            {
                case DuelEventType.GameEnd:
                    return AnnouncementPriority.Immediate;

                case DuelEventType.TurnChange:
                case DuelEventType.DamageDealt:
                case DuelEventType.LifeChange:
                    return AnnouncementPriority.High;

                case DuelEventType.ZoneTransfer:
                case DuelEventType.CardRevealed:
                    return AnnouncementPriority.Normal;

                default:
                    return AnnouncementPriority.Low;
            }
        }

        private Dictionary<string, DuelEventType> BuildEventTypeMap()
        {
            return new Dictionary<string, DuelEventType>
            {
                // Turn and phase
                { "UpdateTurnUXEvent", DuelEventType.TurnChange },
                { "GamePhaseChangeUXEvent", DuelEventType.PhaseChange },
                { "UXEventUpdatePhase", DuelEventType.PhaseChange },

                // Zone transfers (card movement)
                { "ZoneTransferGroup", DuelEventType.ZoneTransfer },
                { "UpdateZoneUXEvent", DuelEventType.ZoneTransfer },

                // Life and damage
                { "LifeTotalUpdateUXEvent", DuelEventType.LifeChange },
                { "UXEventDamageDealt", DuelEventType.DamageDealt },

                // Mana (currently ignored)
                { "ManaProducedUXEvent", DuelEventType.ManaProduced },
                { "UpdateManaPoolUXEvent", DuelEventType.ManaProduced },

                // Card reveals
                { "RevealCardsUXEvent", DuelEventType.CardRevealed },
                { "UpdateRevealedCardUXEvent", DuelEventType.CardRevealed },

                // Counters
                { "CountersChangedUXEvent", DuelEventType.CountersChanged },

                // Game end
                { "GameEndUXEvent", DuelEventType.GameEnd },
                { "DeletePlayerUXEvent", DuelEventType.GameEnd },

                // Combat events
                { "ToggleCombatUXEvent", DuelEventType.Combat },
                { "CombatFrame", DuelEventType.Combat },
                { "AttackLobUXEvent", DuelEventType.Combat },
                { "AttackDecrementUXEvent", DuelEventType.Combat },

                // Target selection events
                { "PlayerSelectingTargetsEventTranslator", DuelEventType.TargetSelection },
                { "PlayerSubmittedTargetsEventTranslator", DuelEventType.TargetConfirmed },

                // Events we want to ignore
                { "WaitForSecondsUXEvent", DuelEventType.Ignored },
                { "CallbackUXEvent", DuelEventType.Ignored },
                { "ParallelPlaybackUXEvent", DuelEventType.Ignored },
                { "CardViewImmediateUpdateUXEvent", DuelEventType.Ignored },
                { "GameStatePlaybackCommencedUXEvent", DuelEventType.Ignored },
                { "GameStatePlaybackCompletedUXEvent", DuelEventType.Ignored },
                { "GrePromptUXEvent", DuelEventType.Ignored },
                { "QuarryHaloUXEvent", DuelEventType.Ignored },
                { "UpdateCardModelUXEvent", DuelEventType.Ignored },
                { "HandShuffleUxEvent", DuelEventType.Ignored },
                { "UserActionTakenUXEvent", DuelEventType.Ignored },
                { "HypotheticalActionsUXChangedEvent", DuelEventType.Ignored },
                { "NPEPauseUXEvent", DuelEventType.Ignored },
                { "NPEDialogUXEvent", DuelEventType.Ignored },
                { "NPEReminderUXEvent", DuelEventType.Ignored },
                { "NPEShowBattlefieldHangerUXEvent", DuelEventType.Ignored },
                { "NPETooltipBumperUXEvent", DuelEventType.Ignored },
                { "UXEventUpdateDecider", DuelEventType.Ignored },
                { "AddCardDecoratorUXEvent", DuelEventType.Ignored },
                { "ResolutionEventStartedUXEvent", DuelEventType.Ignored },
                { "ResolutionEventEndedUXEvent", DuelEventType.Ignored },
            };
        }

        #endregion
    }

    /// <summary>
    /// Categories of duel events for announcement purposes.
    /// </summary>
    public enum DuelEventType
    {
        Ignored,        // Don't announce
        TurnChange,     // Turn number and active player
        PhaseChange,    // Combat, main phase, etc.
        ZoneTransfer,   // Card draws, plays, deaths
        LifeChange,     // Life gain/loss
        DamageDealt,    // Combat/spell damage
        ManaProduced,   // Mana (usually ignored)
        CardRevealed,   // Scry, reveal effects
        CountersChanged,// +1/+1 counters, etc.
        GameEnd,        // Win/loss
        Combat,         // Combat events (attacks, blocks)
        TargetSelection,// Player needs to select targets
        TargetConfirmed // Player submitted targets
    }
}
