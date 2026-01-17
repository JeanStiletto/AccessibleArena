using MelonLoader;
using MTGAAccessibility.Core.Interfaces;
using MTGAAccessibility.Core.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

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

        private readonly Dictionary<string, DuelEventType> _eventTypeMap;
        private readonly Dictionary<string, int> _zoneCounts = new Dictionary<string, int>();

        private string _lastAnnouncement;
        private DateTime _lastAnnouncementTime;
        private const float DUPLICATE_THRESHOLD_SECONDS = 0.5f;

        private uint _localPlayerId;
        // DEPRECATED: TargetNavigator was used to auto-enter targeting mode on spell cast
        // Now HotHighlightNavigator handles targeting via game's HotHighlight system
        // private TargetNavigator _targetNavigator;
        private ZoneNavigator _zoneNavigator;
        private DateTime _lastSpellResolvedTime = DateTime.MinValue;

        // Track user's turn count (game turn number counts each half-turn, we want full cycles)
        private int _userTurnCount = 0;

        // Current combat phase tracking
        private string _currentPhase;
        private string _currentStep;

        /// <summary>
        /// Returns true if currently in Declare Attackers phase.
        /// </summary>
        public bool IsInDeclareAttackersPhase => _currentPhase == "Combat" && _currentStep == "DeclareAttack";

        /// <summary>
        /// Returns true if currently in Declare Blockers phase.
        /// </summary>
        public bool IsInDeclareBlockersPhase => _currentPhase == "Combat" && _currentStep == "DeclareBlock";

        /// <summary>
        /// Returns true if a spell resolved or a permanent entered battlefield within the last specified milliseconds.
        /// Used to skip targeting mode for lands and non-targeted cards.
        /// </summary>
        public bool DidSpellResolveRecently(int withinMs = 500)
        {
            return (DateTime.Now - _lastSpellResolvedTime).TotalMilliseconds < withinMs;
        }

        // DEPRECATED: SetTargetNavigator was used to connect announcer to targeting system
        // public void SetTargetNavigator(TargetNavigator navigator)
        // {
        //     _targetNavigator = navigator;
        // }

        public void SetZoneNavigator(ZoneNavigator navigator)
        {
            _zoneNavigator = navigator;
        }

        public DuelAnnouncer(IAnnouncementService announcer)
        {
            _announcer = announcer;
            _eventTypeMap = BuildEventTypeMap();
            Instance = this;
        }

        public void Activate(uint localPlayerId)
        {
            _isActive = true;
            _localPlayerId = localPlayerId;
            _zoneCounts.Clear();
            _userTurnCount = 0;
        }

        public void Deactivate()
        {
            _isActive = false;
            _currentPhase = null;
            _currentStep = null;
        }

        #region Zone Count Accessors

        /// <summary>
        /// Gets the current card count for opponent's hand.
        /// Returns -1 if not yet tracked.
        /// </summary>
        public int GetOpponentHandCount()
        {
            return _zoneCounts.TryGetValue("Opp_Hand", out int count) ? count : -1;
        }

        /// <summary>
        /// Gets the current card count for local player's library.
        /// Returns -1 if not yet tracked.
        /// </summary>
        public int GetLocalLibraryCount()
        {
            return _zoneCounts.TryGetValue("Local_Library", out int count) ? count : -1;
        }

        /// <summary>
        /// Gets the current card count for opponent's library.
        /// Returns -1 if not yet tracked.
        /// </summary>
        public int GetOpponentLibraryCount()
        {
            return _zoneCounts.TryGetValue("Opp_Library", out int count) ? count : -1;
        }

        #endregion

        // Track event types we've seen for discovery
        private static HashSet<string> _loggedEventTypes = new HashSet<string>();

        /// <summary>
        /// Called by the Harmony patch when a game event is enqueued.
        /// </summary>
        public void OnGameEvent(object uxEvent)
        {
            if (!_isActive || uxEvent == null) return;

            try
            {
                // Log ALL event types we see (once per type) for discovery
                var typeName = uxEvent.GetType().Name;
                if (!_loggedEventTypes.Contains(typeName))
                {
                    _loggedEventTypes.Add(typeName);
                    MelonLogger.Msg($"[DuelAnnouncer] NEW EVENT TYPE SEEN: {typeName}");
                }

                var eventType = ClassifyEvent(uxEvent);
                if (eventType == DuelEventType.Ignored) return;

                var announcement = BuildAnnouncement(eventType, uxEvent);
                if (string.IsNullOrEmpty(announcement)) return;

                if (IsDuplicateAnnouncement(announcement)) return;

                _announcer.Announce(announcement, GetPriority(eventType));
                _lastAnnouncement = announcement;
                _lastAnnouncementTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error processing event: {ex.Message}");
            }
        }

        private DuelEventType ClassifyEvent(object uxEvent)
        {
            var typeName = uxEvent.GetType().Name;
            return _eventTypeMap.TryGetValue(typeName, out var eventType) ? eventType : DuelEventType.Ignored;
        }

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
                case DuelEventType.ResolutionStarted:
                    return HandleResolutionStarted(uxEvent);
                case DuelEventType.ResolutionEnded:
                    return null; // Just tracking, no announcement
                case DuelEventType.CardModelUpdate:
                    return HandleCardModelUpdate(uxEvent);
                case DuelEventType.ZoneTransferGroup:
                    return HandleZoneTransferGroup(uxEvent);
                case DuelEventType.CombatFrame:
                    return HandleCombatFrame(uxEvent);
                case DuelEventType.MultistepEffect:
                    return HandleMultistepEffect(uxEvent);
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

                var activePlayerField = type.GetField("_activePlayer", BindingFlags.NonPublic | BindingFlags.Instance);
                bool isYourTurn = false;
                if (activePlayerField != null)
                {
                    var playerObj = activePlayerField.GetValue(uxEvent);
                    if (playerObj != null)
                        isYourTurn = playerObj.ToString().Contains("LocalPlayer");
                }

                // Track our own turn count (game counts each half-turn, we want full cycles)
                if (isYourTurn)
                {
                    _userTurnCount++;
                    return $"Turn {_userTurnCount}";
                }
                else
                {
                    return "Opponent's turn";
                }
            }
            catch
            {
                return "Turn changed";
            }
        }

        private string BuildZoneTransferAnnouncement(object uxEvent)
        {
            var typeName = uxEvent.GetType().Name;
            if (typeName == "UpdateZoneUXEvent")
                return HandleUpdateZoneEvent(uxEvent);
            return null;
        }

        private string HandleUpdateZoneEvent(object uxEvent)
        {
            var zoneField = uxEvent.GetType().GetField("_zone", BindingFlags.NonPublic | BindingFlags.Instance);
            if (zoneField == null) return null;

            var zoneObj = zoneField.GetValue(uxEvent);
            if (zoneObj == null) return null;

            string zoneStr = zoneObj.ToString();

            bool isLocal = zoneStr.Contains("LocalPlayer") || (!zoneStr.Contains("Opponent") && zoneStr.Contains("Player,"));
            bool isOpponent = zoneStr.Contains("Opponent");

            var zoneMatch = System.Text.RegularExpressions.Regex.Match(zoneStr, @"^(\w+)\s*\(");
            var countMatch = System.Text.RegularExpressions.Regex.Match(zoneStr, @"(\d+)\s*cards?\)");

            if (!zoneMatch.Success) return null;

            string zoneName = zoneMatch.Groups[1].Value;
            int cardCount = countMatch.Success ? int.Parse(countMatch.Groups[1].Value) : 0;
            string zoneKey = (isOpponent ? "Opp_" : "Local_") + zoneName;

            if (_zoneCounts.TryGetValue(zoneKey, out int previousCount))
            {
                int diff = cardCount - previousCount;
                _zoneCounts[zoneKey] = cardCount;

                if (diff == 0) return null;

                if (zoneName == "Hand")
                {
                    if (diff > 0)
                    {
                        return isLocal
                            ? $"Drew {diff} card{(diff > 1 ? "s" : "")}"
                            : $"Opponent drew {diff} card{(diff > 1 ? "s" : "")}";
                    }
                    else if (diff < 0 && isOpponent)
                    {
                        return "Opponent played a card";
                    }
                }
                else if (zoneName == "Battlefield")
                {
                    if (diff > 0)
                    {
                        if (isOpponent)
                            return $"Opponent: {diff} permanent{(diff > 1 ? "s" : "")} entered battlefield";
                        _lastSpellResolvedTime = DateTime.Now;
                    }
                    else if (diff < 0)
                    {
                        int removed = Math.Abs(diff);
                        // Battlefield is a shared zone - can't determine ownership from zone string
                        // Graveyard/Exile announcements will specify correct ownership
                        return $"{removed} permanent{(removed > 1 ? "s" : "")} left battlefield";
                    }
                }
                else if (zoneName == "Graveyard" && diff > 0)
                {
                    return isOpponent ? "Card went to opponent's graveyard" : "Card went to your graveyard";
                }
                else if (zoneName == "Stack")
                {
                    if (diff > 0)
                    {
                        MelonCoroutines.Start(AnnounceStackCardDelayed());
                        return null;
                    }
                    else if (diff < 0)
                    {
                        _lastSpellResolvedTime = DateTime.Now;
                        return "Spell resolved";
                    }
                }
            }
            else
            {
                _zoneCounts[zoneKey] = cardCount;

                if (zoneName == "Stack" && cardCount > 0)
                {
                    MelonCoroutines.Start(AnnounceStackCardDelayed());
                    return null;
                }
            }

            return null;
        }

        // Track if we've logged life event fields (only once for discovery)
        private static bool _lifeEventFieldsLogged = false;

        private string BuildLifeChangeAnnouncement(object uxEvent)
        {
            try
            {
                // Log all fields/properties for discovery (once per session)
                if (!_lifeEventFieldsLogged)
                {
                    _lifeEventFieldsLogged = true;
                    LogEventFields(uxEvent, "LIFE EVENT");
                }

                // Field names from discovery: AffectedId, Change (property)
                var affectedId = GetFieldValue<uint>(uxEvent, "AffectedId");
                var change = GetNestedPropertyValue<int>(uxEvent, "Change");

                MelonLogger.Msg($"[DuelAnnouncer] Life event: affectedId={affectedId}, change={change}, localPlayer={_localPlayerId}");

                if (change == 0) return null;

                // AffectedId of 0 might mean local player - check avatar field
                bool isLocal = affectedId == _localPlayerId || affectedId == 0;

                // Double-check by looking at avatar field
                var avatar = GetFieldValue<object>(uxEvent, "_avatar");
                if (avatar != null)
                {
                    var avatarStr = avatar.ToString();
                    // Check for local player ID - don't hardcode Player #1 as local could be #2
                    isLocal = avatarStr.Contains("Player #" + _localPlayerId) || avatarStr.Contains("#" + _localPlayerId);
                }

                string who = isLocal ? "You" : "Opponent";
                string direction = change > 0 ? "gained" : "lost";
                return $"{who} {direction} {Math.Abs(change)} life";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Life announcement error: {ex.Message}");
                return null;
            }
        }

        // Track if we've logged damage event fields (only once for discovery)
        private static bool _damageEventFieldsLogged = false;

        private string BuildDamageAnnouncement(object uxEvent)
        {
            try
            {
                // Log all fields/properties for discovery (once per session)
                LogDamageEventFields(uxEvent);

                var damage = GetFieldValue<int>(uxEvent, "DamageAmount");
                if (damage <= 0) return null;

                // Get target info
                var targetId = GetFieldValue<uint>(uxEvent, "TargetId");
                var targetInstanceId = GetFieldValue<uint>(uxEvent, "TargetInstanceId");
                string targetName = GetDamageTargetName(targetId, targetInstanceId);

                // Get source info - try multiple possible field names
                string sourceName = GetDamageSourceName(uxEvent);

                // Get damage flags
                var damageFlags = GetDamageFlags(uxEvent);

                // Build announcement
                var parts = new List<string>();

                if (!string.IsNullOrEmpty(sourceName))
                {
                    parts.Add($"{sourceName} deals {damage}");
                }
                else
                {
                    parts.Add(damage.ToString());
                }

                // Add damage type modifiers
                if (!string.IsNullOrEmpty(damageFlags))
                {
                    parts.Add(damageFlags);
                }

                parts.Add($"to {targetName}");

                return string.Join(" ", parts);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error building damage announcement: {ex.Message}");
                return null;
            }
        }

        private void LogEventFields(object uxEvent, string label)
        {
            if (uxEvent == null) return;

            var type = uxEvent.GetType();
            MelonLogger.Msg($"[DuelAnnouncer] === {label} TYPE: {type.FullName} ===");

            // Log all fields
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                try
                {
                    var value = field.GetValue(uxEvent);
                    string valueStr = value?.ToString() ?? "null";
                    if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                    MelonLogger.Msg($"[DuelAnnouncer] Field: {field.Name} = {valueStr} ({field.FieldType.Name})");
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[DuelAnnouncer] Field: {field.Name} = [Error: {ex.Message}]");
                }
            }

            // Log all properties
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var prop in props)
            {
                try
                {
                    var value = prop.GetValue(uxEvent);
                    string valueStr = value?.ToString() ?? "null";
                    if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                    MelonLogger.Msg($"[DuelAnnouncer] Property: {prop.Name} = {valueStr} ({prop.PropertyType.Name})");
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[DuelAnnouncer] Property: {prop.Name} = [Error: {ex.Message}]");
                }
            }

            MelonLogger.Msg($"[DuelAnnouncer] === END {label} FIELDS ===");
        }

        private void LogDamageEventFields(object uxEvent)
        {
            if (_damageEventFieldsLogged || uxEvent == null) return;
            _damageEventFieldsLogged = true;

            var type = uxEvent.GetType();
            MelonLogger.Msg($"[DuelAnnouncer] === DAMAGE EVENT TYPE: {type.FullName} ===");

            // Log all fields
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                try
                {
                    var value = field.GetValue(uxEvent);
                    string valueStr = value?.ToString() ?? "null";
                    if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                    MelonLogger.Msg($"[DuelAnnouncer] Field: {field.Name} = {valueStr} ({field.FieldType.Name})");
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[DuelAnnouncer] Field: {field.Name} = [Error: {ex.Message}]");
                }
            }

            // Log all properties
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var prop in props)
            {
                try
                {
                    var value = prop.GetValue(uxEvent);
                    string valueStr = value?.ToString() ?? "null";
                    if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                    MelonLogger.Msg($"[DuelAnnouncer] Property: {prop.Name} = {valueStr} ({prop.PropertyType.Name})");
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[DuelAnnouncer] Property: {prop.Name} = [Error: {ex.Message}]");
                }
            }

            MelonLogger.Msg($"[DuelAnnouncer] === END DAMAGE EVENT FIELDS ===");
        }

        private string GetDamageTargetName(uint targetPlayerId, uint targetInstanceId)
        {
            // If target is a player
            if (targetPlayerId == _localPlayerId)
                return "you";
            if (targetPlayerId != 0)
                return "opponent";

            // Try to find target card by InstanceId
            if (targetInstanceId != 0)
            {
                string cardName = FindCardNameByInstanceId(targetInstanceId);
                if (!string.IsNullOrEmpty(cardName))
                    return cardName;
            }

            return "target";
        }

        private string GetDamageSourceName(object uxEvent)
        {
            // Try various field names for source identification
            string[] sourceInstanceFields = { "SourceInstanceId", "InstigatorInstanceId", "SourceId", "DamageSourceInstanceId" };
            string[] sourceGrpFields = { "SourceGrpId", "InstigatorGrpId", "GrpId", "DamageSourceGrpId" };

            // First try InstanceId-based lookup (finds the actual card on battlefield)
            foreach (var fieldName in sourceInstanceFields)
            {
                var instanceId = GetFieldValue<uint>(uxEvent, fieldName);
                if (instanceId != 0)
                {
                    string name = FindCardNameByInstanceId(instanceId);
                    if (!string.IsNullOrEmpty(name))
                    {
                        MelonLogger.Msg($"[DuelAnnouncer] Found source from {fieldName}: {name}");
                        return name;
                    }
                }
            }

            // Then try GrpId-based lookup (card database ID)
            foreach (var fieldName in sourceGrpFields)
            {
                var grpId = GetFieldValue<uint>(uxEvent, fieldName);
                if (grpId != 0)
                {
                    string name = CardModelProvider.GetNameFromGrpId(grpId);
                    if (!string.IsNullOrEmpty(name))
                    {
                        MelonLogger.Msg($"[DuelAnnouncer] Found source from {fieldName} (GrpId): {name}");
                        return name;
                    }
                }
            }

            // Check if damage is from combat (during CombatDamage step)
            if (_currentPhase == "Combat" && _currentStep == "CombatDamage")
            {
                return "Combat damage";
            }

            return null;
        }

        private string GetDamageFlags(object uxEvent)
        {
            var flags = new List<string>();

            // Try to detect damage types/flags
            bool isLifelink = GetFieldValue<bool>(uxEvent, "IsLifelink") || GetFieldValue<bool>(uxEvent, "Lifelink");
            bool isTrample = GetFieldValue<bool>(uxEvent, "IsTrample") || GetFieldValue<bool>(uxEvent, "Trample");
            bool isDeathtouch = GetFieldValue<bool>(uxEvent, "IsDeathtouch") || GetFieldValue<bool>(uxEvent, "Deathtouch");
            bool isInfect = GetFieldValue<bool>(uxEvent, "IsInfect") || GetFieldValue<bool>(uxEvent, "Infect");
            bool isCombat = GetFieldValue<bool>(uxEvent, "IsCombatDamage") || GetFieldValue<bool>(uxEvent, "CombatDamage");

            if (isLifelink) flags.Add("lifelink");
            if (isTrample) flags.Add("trample");
            if (isDeathtouch) flags.Add("deathtouch");
            if (isInfect) flags.Add("infect");
            if (isCombat && !(_currentPhase == "Combat" && _currentStep == "CombatDamage"))
                flags.Add("combat");

            return flags.Count > 0 ? $"({string.Join(", ", flags)})" : null;
        }

        private string FindCardNameByInstanceId(uint instanceId)
        {
            if (instanceId == 0) return null;

            try
            {
                // Search for cards on battlefield/stack with matching InstanceId
                foreach (var go in GameObject.FindObjectsOfType<GameObject>())
                {
                    if (go == null || !go.activeInHierarchy) continue;

                    // Only check card holders
                    Transform current = go.transform;
                    bool inCardZone = false;
                    while (current != null)
                    {
                        if (current.name.Contains("CardHolder") || current.name.Contains("StackCard"))
                        {
                            inCardZone = true;
                            break;
                        }
                        current = current.parent;
                    }
                    if (!inCardZone) continue;

                    // Check for CDC component with matching InstanceId
                    var cdcComponent = CardModelProvider.GetDuelSceneCDC(go);
                    if (cdcComponent != null)
                    {
                        var model = CardModelProvider.GetCardModel(cdcComponent);
                        if (model != null)
                        {
                            var modelType = model.GetType();
                            var instanceIdProp = modelType.GetProperty("InstanceId");
                            if (instanceIdProp != null)
                            {
                                var cardInstanceId = instanceIdProp.GetValue(model);
                                if (cardInstanceId is uint cid && cid == instanceId)
                                {
                                    // Found matching card, get its name
                                    var grpIdProp = modelType.GetProperty("GrpId");
                                    if (grpIdProp != null)
                                    {
                                        var grpId = grpIdProp.GetValue(model);
                                        if (grpId is uint gid)
                                        {
                                            return CardModelProvider.GetNameFromGrpId(gid);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error finding card by InstanceId {instanceId}: {ex.Message}");
            }

            return null;
        }

        private string BuildPhaseChangeAnnouncement(object uxEvent)
        {
            try
            {
                var type = uxEvent.GetType();

                var phaseField = type.GetField("<Phase>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                string phase = phaseField?.GetValue(uxEvent)?.ToString();

                var stepField = type.GetField("<Step>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                string step = stepField?.GetValue(uxEvent)?.ToString();

                // Check if we're leaving Declare Attackers phase - announce attacker count
                string attackerAnnouncement = null;
                if (_currentStep == "DeclareAttack" && step != "DeclareAttack")
                {
                    int attackerCount = CountAttackingCreatures();
                    if (attackerCount > 0)
                    {
                        attackerAnnouncement = $"{attackerCount} attacker{(attackerCount != 1 ? "s" : "")}";
                        MelonLogger.Msg($"[DuelAnnouncer] Leaving declare attackers: {attackerAnnouncement}");
                    }
                }

                // Track current phase/step for combat navigation
                _currentPhase = phase;
                _currentStep = step;

                string phaseAnnouncement = null;
                if (phase == "Main1") phaseAnnouncement = "First main phase";
                else if (phase == "Main2") phaseAnnouncement = "Second main phase";
                else if (phase == "Combat")
                {
                    if (step == "DeclareAttack") phaseAnnouncement = "Declare attackers";
                    else if (step == "DeclareBlock") phaseAnnouncement = "Declare blockers";
                    else if (step == "CombatDamage") phaseAnnouncement = "Combat damage";
                    else if (step == "EndCombat") phaseAnnouncement = "End of combat";
                    else if (step == "None") phaseAnnouncement = "Combat phase";
                }
                else if (phase == "Ending" && step == "End") phaseAnnouncement = "End step";

                // Combine attacker count with phase announcement
                if (attackerAnnouncement != null && phaseAnnouncement != null)
                    return $"{attackerAnnouncement}. {phaseAnnouncement}";
                if (attackerAnnouncement != null)
                    return attackerAnnouncement;
                return phaseAnnouncement;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Counts creatures currently declared as attackers.
        /// Looks for cards with active "IsAttacking" child indicator.
        /// </summary>
        private int CountAttackingCreatures()
        {
            int count = 0;
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy)
                    continue;

                // Only check CDC (card) objects
                if (!go.name.StartsWith("CDC "))
                    continue;

                // Check if this card has an active "IsAttacking" indicator
                foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                {
                    if (child.gameObject.activeInHierarchy && child.name == "IsAttacking")
                    {
                        count++;
                        break;
                    }
                }
            }
            return count;
        }

        private string BuildRevealAnnouncement(object uxEvent)
        {
            try
            {
                var cardName = GetFieldValue<string>(uxEvent, "CardName");
                return !string.IsNullOrEmpty(cardName) ? $"Revealed {cardName}" : null;
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
                var counterType = GetFieldValue<string>(uxEvent, "CounterType");
                var change = GetFieldValue<int>(uxEvent, "Change");
                var cardName = GetFieldValue<string>(uxEvent, "CardName");

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
                var winnerId = GetFieldValue<uint>(uxEvent, "WinnerId");
                return winnerId == _localPlayerId ? "Victory!" : "Defeat";
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
                    var combatModeField = type.GetField("_CombatMode", BindingFlags.NonPublic | BindingFlags.Instance);
                    var modeValue = combatModeField?.GetValue(uxEvent)?.ToString();
                    if (modeValue == "CombatBegun") return "Combat begins";
                    return null;
                }

                if (typeName == "AttackLobUXEvent")
                {
                    // Debug: Log all fields once to discover available data
                    if (!_attackLobFieldsLogged)
                    {
                        _attackLobFieldsLogged = true;
                        LogEventFields(uxEvent, "AttackLobUXEvent");
                    }
                    return BuildAttackerDeclaredAnnouncement(uxEvent);
                }
                if (typeName == "AttackDecrementUXEvent") return "Attacker removed";

                return null;
            }
            catch
            {
                return null;
            }
        }

        // Track if we've logged AttackLobUXEvent fields (one-time debug)
        private static bool _attackLobFieldsLogged = false;

        private string BuildAttackerDeclaredAnnouncement(object uxEvent)
        {
            try
            {
                // Get attacker InstanceId from _attackerId field
                var attackerId = GetFieldValue<uint>(uxEvent, "_attackerId");

                string cardName = null;
                string powerToughness = null;

                // Look up card by InstanceId
                if (attackerId != 0)
                {
                    cardName = FindCardNameByInstanceId(attackerId);

                    // Get P/T from the card model
                    var (power, toughness) = GetCardPowerToughnessByInstanceId(attackerId);
                    if (power >= 0 && toughness >= 0)
                    {
                        powerToughness = $"{power}/{toughness}";
                    }
                }

                // Build announcement
                if (!string.IsNullOrEmpty(cardName))
                {
                    if (!string.IsNullOrEmpty(powerToughness))
                        return $"{cardName} {powerToughness} attacking";
                    return $"{cardName} attacking";
                }

                return "Attacker declared";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error building attacker announcement: {ex.Message}");
                return "Attacker declared";
            }
        }

        private (int power, int toughness) GetCardPowerToughnessByInstanceId(uint instanceId)
        {
            if (instanceId == 0) return (-1, -1);

            try
            {
                foreach (var go in GameObject.FindObjectsOfType<GameObject>())
                {
                    if (go == null || !go.activeInHierarchy) continue;
                    if (!go.name.StartsWith("CDC ")) continue;

                    var cdcComponent = CardModelProvider.GetDuelSceneCDC(go);
                    if (cdcComponent == null) continue;

                    var model = CardModelProvider.GetCardModel(cdcComponent);
                    if (model == null) continue;

                    var modelType = model.GetType();
                    var instanceIdProp = modelType.GetProperty("InstanceId");
                    if (instanceIdProp == null) continue;

                    var cardInstanceId = instanceIdProp.GetValue(model);
                    if (!(cardInstanceId is uint cid) || cid != instanceId) continue;

                    // Found the card, get P/T
                    var powerProp = modelType.GetProperty("Power");
                    var toughnessProp = modelType.GetProperty("Toughness");

                    if (powerProp != null && toughnessProp != null)
                    {
                        var power = powerProp.GetValue(model);
                        var toughness = toughnessProp.GetValue(model);

                        if (power is int p && toughness is int t)
                            return (p, t);
                    }
                    break;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error getting P/T by InstanceId: {ex.Message}");
            }

            return (-1, -1);
        }

        private string HandleTargetSelectionEvent(object uxEvent)
        {
            // DEPRECATED: Old targeting mode entry - now HotHighlightNavigator handles targeting
            // via Tab cycling through highlighted targets. The game's HotHighlight system shows
            // valid targets, and users Tab to cycle through them.
            // if (_targetNavigator != null)
            // {
            //     bool hasSpellOnStack = _zoneNavigator?.GetFreshStackCount() > 0;
            //     if (hasSpellOnStack)
            //     {
            //         _targetNavigator.TryEnterTargetMode(requireValidTargets: false);
            //     }
            //     return null;
            // }
            return null; // No announcement needed - Tab will discover targets
        }

        private string HandleTargetConfirmedEvent(object uxEvent)
        {
            // DEPRECATED: _targetNavigator?.ExitTargetMode();
            // HotHighlightNavigator automatically discovers new highlight state on next Tab
            return null;
        }

        // Track if we've logged various event fields (once per type for discovery)
        private static bool _resolutionEventFieldsLogged = false;
        private static bool _cardModelUpdateFieldsLogged = false;
        private static bool _zoneTransferFieldsLogged = false;
        private static bool _combatFrameFieldsLogged = false;

        // Track previous damage values to detect changes
        private Dictionary<uint, uint> _creatureDamage = new Dictionary<uint, uint>();

        private string HandleCardModelUpdate(object uxEvent)
        {
            try
            {
                // Log fields for discovery (once)
                if (!_cardModelUpdateFieldsLogged)
                {
                    _cardModelUpdateFieldsLogged = true;
                    LogEventFields(uxEvent, "CARD MODEL UPDATE");
                }

                // Try to extract card instance and damage info
                var instanceId = GetFieldValue<uint>(uxEvent, "InstanceId");
                var damage = GetFieldValue<uint>(uxEvent, "Damage");
                var grpId = GetFieldValue<uint>(uxEvent, "GrpId");

                // Check if damage changed
                if (instanceId != 0 && damage > 0)
                {
                    uint previousDamage = 0;
                    _creatureDamage.TryGetValue(instanceId, out previousDamage);

                    if (damage != previousDamage)
                    {
                        _creatureDamage[instanceId] = damage;
                        uint damageDealt = damage - previousDamage;

                        if (damageDealt > 0)
                        {
                            string cardName = grpId != 0 ? CardModelProvider.GetNameFromGrpId(grpId) : null;
                            if (!string.IsNullOrEmpty(cardName))
                            {
                                MelonLogger.Msg($"[DuelAnnouncer] Creature damage: {cardName} now has {damage} damage (dealt: {damageDealt})");

                                // Try to correlate with last resolving card
                                if (!string.IsNullOrEmpty(_lastResolvingCardName))
                                {
                                    return $"{_lastResolvingCardName} deals {damageDealt} to {cardName}";
                                }
                                return $"{damageDealt} damage to {cardName}";
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error handling card model update: {ex.Message}");
                return null;
            }
        }

        private string HandleZoneTransferGroup(object uxEvent)
        {
            try
            {
                // Log fields for discovery (once)
                if (!_zoneTransferFieldsLogged)
                {
                    _zoneTransferFieldsLogged = true;
                    LogEventFields(uxEvent, "ZONE TRANSFER GROUP");
                }

                // Look for cards moving to graveyard (deaths)
                // Try to get the transfers list
                var transfers = GetFieldValue<object>(uxEvent, "Transfers");
                var children = GetFieldValue<object>(uxEvent, "_children");

                if (transfers != null)
                {
                    MelonLogger.Msg($"[DuelAnnouncer] ZoneTransfer has Transfers: {transfers.GetType().Name}");
                }
                if (children != null)
                {
                    var childList = children as System.Collections.IEnumerable;
                    if (childList != null)
                    {
                        int count = 0;
                        foreach (var child in childList)
                        {
                            count++;
                            if (count <= 3) // Log first 3
                            {
                                MelonLogger.Msg($"[DuelAnnouncer] ZoneTransfer child: {child?.GetType().Name}");
                            }
                        }
                        MelonLogger.Msg($"[DuelAnnouncer] ZoneTransfer total children: {count}");
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error handling zone transfer: {ex.Message}");
                return null;
            }
        }

        private string HandleCombatFrame(object uxEvent)
        {
            try
            {
                // Log fields for discovery (once)
                if (!_combatFrameFieldsLogged)
                {
                    _combatFrameFieldsLogged = true;
                    LogEventFields(uxEvent, "COMBAT FRAME");
                }

                var announcements = new List<string>();

                // Log total damage for analysis
                var opponentDamage = GetFieldValue<int>(uxEvent, "OpponentDamageDealt");
                MelonLogger.Msg($"[DuelAnnouncer] CombatFrame: OpponentDamageDealt={opponentDamage}");

                // Check both branch lists - _branches and _runningBranches
                var branches = GetFieldValue<object>(uxEvent, "_branches");
                var runningBranches = GetFieldValue<object>(uxEvent, "_runningBranches");

                // Log counts for investigation
                int branchCount = 0;
                int runningCount = 0;
                if (branches is System.Collections.IEnumerable bList)
                    foreach (var _ in bList) branchCount++;
                if (runningBranches is System.Collections.IEnumerable rList)
                    foreach (var _ in rList) runningCount++;
                MelonLogger.Msg($"[DuelAnnouncer] Branch counts: _branches={branchCount}, _runningBranches={runningCount}");
                if (branches != null)
                {
                    var branchList = branches as System.Collections.IEnumerable;
                    if (branchList != null)
                    {
                        int branchIndex = 0;
                        foreach (var branch in branchList)
                        {
                            if (branch == null) continue;

                            // Get damage chain from this branch (attacker + blocker if present)
                            var damageChain = ExtractDamageChain(branch);

                            // Log for debugging
                            foreach (var dmg in damageChain)
                            {
                                MelonLogger.Msg($"[DuelAnnouncer] Branch[{branchIndex}]: {dmg.SourceName} -> {dmg.TargetName}, Amount={dmg.Amount}");
                            }

                            // Build grouped announcement for this combat pair
                            if (damageChain.Count == 1)
                            {
                                // Single damage (unblocked or one-sided)
                                var dmg = damageChain[0];
                                if (dmg.Amount > 0 && !string.IsNullOrEmpty(dmg.SourceName) && !string.IsNullOrEmpty(dmg.TargetName))
                                {
                                    announcements.Add($"{dmg.SourceName} deals {dmg.Amount} to {dmg.TargetName}");
                                }
                            }
                            else if (damageChain.Count >= 2)
                            {
                                // Combat trade - group attacker and blocker damage together
                                var parts = new List<string>();
                                foreach (var dmg in damageChain)
                                {
                                    if (dmg.Amount > 0 && !string.IsNullOrEmpty(dmg.SourceName) && !string.IsNullOrEmpty(dmg.TargetName))
                                    {
                                        parts.Add($"{dmg.SourceName} deals {dmg.Amount} to {dmg.TargetName}");
                                    }
                                }
                                if (parts.Count > 0)
                                {
                                    announcements.Add(string.Join(", ", parts));
                                }
                            }
                            branchIndex++;
                        }
                        MelonLogger.Msg($"[DuelAnnouncer] Total branches: {branchIndex}");
                    }
                }

                if (announcements.Count > 0)
                {
                    return string.Join(". ", announcements);
                }

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error handling combat frame: {ex.Message}");
                return null;
            }
        }

        // Track if we've logged multistep effect fields (once for discovery)
        private static bool _multistepEffectFieldsLogged = false;

        /// <summary>
        /// Tracks if a library manipulation browser (scry, surveil, etc.) is active.
        /// Set to true when MultistepEffectStartedUXEvent fires.
        /// </summary>
        public bool IsLibraryBrowserActive { get; private set; }

        /// <summary>
        /// Info about the current library manipulation effect.
        /// </summary>
        public string CurrentEffectType { get; private set; }
        public int CurrentEffectCount { get; private set; }

        private string HandleMultistepEffect(object uxEvent)
        {
            try
            {
                // Log fields for discovery (once)
                if (!_multistepEffectFieldsLogged)
                {
                    _multistepEffectFieldsLogged = true;
                    LogEventFields(uxEvent, "MULTISTEP EFFECT");
                }

                // Extract effect information using correct property names from logs:
                // - AbilityCategory (AbilitySubCategory enum): Scry, Surveil, etc.
                // - Affector (MtgCardInstance): source card
                // - Affected (MtgPlayer): target player
                var abilityCategory = GetFieldValue<object>(uxEvent, "AbilityCategory");
                var affector = GetFieldValue<object>(uxEvent, "Affector");
                var affected = GetFieldValue<object>(uxEvent, "Affected");

                string effectName = abilityCategory?.ToString() ?? "unknown";
                MelonLogger.Msg($"[DuelAnnouncer] MultistepEffect: AbilityCategory={effectName}, Affector={affector}, Affected={affected}");

                // Determine effect type and description
                string effectDescription;
                CurrentEffectType = effectName;

                switch (effectName.ToLower())
                {
                    case "scry":
                        effectDescription = "Scry";
                        break;
                    case "surveil":
                        effectDescription = "Surveil";
                        break;
                    case "look":
                    case "lookat":
                        effectDescription = "Look at top card";
                        break;
                    case "mill":
                        effectDescription = "Mill";
                        break;
                    default:
                        effectDescription = effectName;
                        break;
                }

                IsLibraryBrowserActive = true;

                // Get card name from affector if available
                string cardName = null;
                if (affector != null)
                {
                    // Try to get GrpId from the affector's Printing property
                    var printingProp = affector.GetType().GetProperty("Printing");
                    if (printingProp != null)
                    {
                        var printing = printingProp.GetValue(affector);
                        if (printing != null)
                        {
                            var grpIdProp = printing.GetType().GetProperty("GrpId");
                            if (grpIdProp != null)
                            {
                                var grpId = grpIdProp.GetValue(printing);
                                if (grpId is uint gid && gid != 0)
                                {
                                    cardName = CardModelProvider.GetNameFromGrpId(gid);
                                }
                                else if (grpId is int gidInt && gidInt != 0)
                                {
                                    cardName = CardModelProvider.GetNameFromGrpId((uint)gidInt);
                                }
                            }
                        }
                    }

                    // Fallback: try direct GrpId on affector
                    if (string.IsNullOrEmpty(cardName))
                    {
                        var directGrpId = GetFieldValue<uint>(affector, "GrpId");
                        if (directGrpId != 0)
                        {
                            cardName = CardModelProvider.GetNameFromGrpId(directGrpId);
                        }
                    }
                }

                // Build announcement based on effect type
                string announcement;
                if (effectName.ToLower() == "scry")
                {
                    announcement = $"{effectDescription}. Tab to see card, Enter to keep on top, Space to put on bottom";
                }
                else if (effectName.ToLower() == "surveil")
                {
                    announcement = $"{effectDescription}. Tab to see card, Enter to keep on top, Space to put in graveyard";
                }
                else
                {
                    announcement = $"{effectDescription}. Tab to navigate, Enter to select";
                }

                if (!string.IsNullOrEmpty(cardName))
                {
                    MelonLogger.Msg($"[DuelAnnouncer] Library browser active: {effectDescription} from {cardName}");
                }
                else
                {
                    MelonLogger.Msg($"[DuelAnnouncer] Library browser active: {effectDescription}");
                }

                return announcement;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error handling multistep effect: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Called when library browser is closed (effect resolved).
        /// </summary>
        public void OnLibraryBrowserClosed()
        {
            IsLibraryBrowserActive = false;
            CurrentEffectType = null;
            CurrentEffectCount = 0;
        }

        // Simple class to hold damage info extracted from a damage event
        private class DamageInfo
        {
            public string SourceName { get; set; }
            public string TargetName { get; set; }
            public int Amount { get; set; }
        }

        // Extract damage info from a single damage event
        private DamageInfo ExtractDamageInfo(object damageEvent)
        {
            if (damageEvent == null) return null;

            var info = new DamageInfo();
            info.Amount = GetFieldValue<int>(damageEvent, "Amount");

            // Get source info
            var source = GetFieldValue<object>(damageEvent, "Source");
            if (source != null)
            {
                var sourceGrpId = GetNestedPropertyValue<uint>(source, "GrpId");
                if (sourceGrpId != 0)
                {
                    info.SourceName = CardModelProvider.GetNameFromGrpId(sourceGrpId);
                }
            }

            // Get target info
            var target = GetFieldValue<object>(damageEvent, "Target");
            if (target != null)
            {
                var targetStr = target.ToString();
                if (targetStr.Contains("LocalPlayer"))
                {
                    info.TargetName = "you";
                }
                else if (targetStr.Contains("Opponent"))
                {
                    info.TargetName = "opponent";
                }
                else
                {
                    var targetGrpId = GetNestedPropertyValue<uint>(target, "GrpId");
                    if (targetGrpId != 0)
                    {
                        info.TargetName = CardModelProvider.GetNameFromGrpId(targetGrpId);
                    }
                }
            }

            return info;
        }

        // Extract all damage in a branch chain (follows _nextBranch for blocker damage)
        private List<DamageInfo> ExtractDamageChain(object branch)
        {
            var chain = new List<DamageInfo>();
            var currentBranch = branch;
            int depth = 0;

            // Log BranchDepth from first branch
            var branchDepth = GetNestedPropertyValue<int>(branch, "BranchDepth");
            MelonLogger.Msg($"[DuelAnnouncer] Chain BranchDepth={branchDepth}");

            while (currentBranch != null)
            {
                var damageEvent = GetFieldValue<object>(currentBranch, "_damageEvent");
                if (damageEvent != null)
                {
                    var info = ExtractDamageInfo(damageEvent);
                    if (info != null)
                    {
                        chain.Add(info);
                    }
                }

                // Check what _nextBranch contains
                var nextBranch = GetFieldValue<object>(currentBranch, "_nextBranch");
                MelonLogger.Msg($"[DuelAnnouncer] Chain depth {depth}: _nextBranch={(nextBranch != null ? "exists" : "null")}");

                // Follow the chain to get blocker damage
                currentBranch = nextBranch;
                depth++;

                // Safety limit
                if (depth > 10) break;
            }

            MelonLogger.Msg($"[DuelAnnouncer] Chain total depth: {depth}");
            return chain;
        }

        private T GetNestedPropertyValue<T>(object obj, string propertyName)
        {
            if (obj == null) return default(T);
            try
            {
                var prop = obj.GetType().GetProperty(propertyName);
                if (prop != null)
                {
                    var value = prop.GetValue(obj);
                    if (value is T typedValue) return typedValue;
                }
            }
            catch { }
            return default(T);
        }

        // Cache for instance ID to card name lookup
        private Dictionary<uint, string> _instanceIdToName = new Dictionary<uint, string>();

        private string GetCardNameByInstanceId(uint instanceId)
        {
            if (instanceId == 0) return null;

            // Check cache first
            if (_instanceIdToName.TryGetValue(instanceId, out string cachedName))
                return cachedName;

            // Try to find card in battlefield/zones
            try
            {
                foreach (var go in GameObject.FindObjectsOfType<GameObject>())
                {
                    if (go == null || !go.activeInHierarchy) continue;
                    if (!go.name.StartsWith("CDC #")) continue;

                    var cdcComponent = CardModelProvider.GetDuelSceneCDC(go);
                    if (cdcComponent != null)
                    {
                        var model = CardModelProvider.GetCardModel(cdcComponent);
                        if (model != null)
                        {
                            var modelType = model.GetType();
                            var instIdProp = modelType.GetProperty("InstanceId");
                            if (instIdProp != null)
                            {
                                var cardInstId = instIdProp.GetValue(model);
                                if (cardInstId is uint cid && cid == instanceId)
                                {
                                    var grpIdProp = modelType.GetProperty("GrpId");
                                    if (grpIdProp != null)
                                    {
                                        var grpId = grpIdProp.GetValue(model);
                                        if (grpId is uint gid)
                                        {
                                            string name = CardModelProvider.GetNameFromGrpId(gid);
                                            if (!string.IsNullOrEmpty(name))
                                            {
                                                _instanceIdToName[instanceId] = name;
                                                return name;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error looking up card {instanceId}: {ex.Message}");
            }

            return null;
        }

        // Track the last resolving card for damage correlation
        private string _lastResolvingCardName = null;
        private uint _lastResolvingInstanceId = 0;

        private string HandleResolutionStarted(object uxEvent)
        {
            try
            {
                // Log fields for discovery (once)
                if (!_resolutionEventFieldsLogged)
                {
                    _resolutionEventFieldsLogged = true;
                    LogEventFields(uxEvent, "RESOLUTION EVENT");
                }

                // Try to get the instigator (source card) info
                var instigatorInstanceId = GetFieldValue<uint>(uxEvent, "InstigatorInstanceId");

                // Try to get card name from various possible fields
                string cardName = null;

                // Try Instigator property (might be a card object)
                var instigator = GetFieldValue<object>(uxEvent, "Instigator");
                if (instigator != null)
                {
                    var instigatorType = instigator.GetType();
                    var grpIdProp = instigatorType.GetProperty("GrpId");
                    if (grpIdProp != null)
                    {
                        var grpId = grpIdProp.GetValue(instigator);
                        if (grpId is uint gid && gid != 0)
                        {
                            cardName = CardModelProvider.GetNameFromGrpId(gid);
                        }
                    }
                }

                // Store for later correlation with life/damage events
                if (!string.IsNullOrEmpty(cardName))
                {
                    _lastResolvingCardName = cardName;
                    _lastResolvingInstanceId = instigatorInstanceId;
                    MelonLogger.Msg($"[DuelAnnouncer] Resolution started: {cardName} (InstanceId: {instigatorInstanceId})");
                }

                return null; // Don't announce resolution start, just track it
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error handling resolution: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Helper Methods

        private IEnumerator AnnounceStackCardDelayed()
        {
            yield return null;
            yield return null;
            yield return null;

            GameObject stackCard = GetTopStackCard();

            if (stackCard != null)
            {
                _announcer.Announce(BuildCastAnnouncement(stackCard), AnnouncementPriority.High);
            }
            else
            {
                yield return new WaitForSeconds(0.2f);
                stackCard = GetTopStackCard();

                if (stackCard != null)
                    _announcer.Announce(BuildCastAnnouncement(stackCard), AnnouncementPriority.High);
                else
                    _announcer.Announce(Strings.SpellCast, AnnouncementPriority.High);
            }
        }

        private string BuildCastAnnouncement(GameObject cardObj)
        {
            var info = CardDetector.ExtractCardInfo(cardObj);
            var parts = new List<string>();

            parts.Add($"Cast {info.Name ?? "unknown spell"}");

            if (!string.IsNullOrEmpty(info.PowerToughness))
                parts.Add(info.PowerToughness);

            if (!string.IsNullOrEmpty(info.RulesText))
                parts.Add(info.RulesText);

            return string.Join(", ", parts);
        }

        private GameObject GetTopStackCard()
        {
            try
            {
                foreach (var go in GameObject.FindObjectsOfType<GameObject>())
                {
                    if (go == null || !go.activeInHierarchy) continue;

                    if (go.name.Contains("StackCardHolder"))
                    {
                        foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                        {
                            if (child == null || !child.gameObject.activeInHierarchy) continue;

                            if (child.name.Contains("CDC #"))
                                return child.gameObject;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private T GetFieldValue<T>(object obj, string fieldName)
        {
            if (obj == null) return default;

            var type = obj.GetType();
            var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                var value = field.GetValue(obj);
                if (value is T typed) return typed;
            }

            var prop = type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null)
            {
                var value = prop.GetValue(obj);
                if (value is T typed) return typed;
            }

            return default;
        }

        private bool IsDuplicateAnnouncement(string announcement)
        {
            if (string.IsNullOrEmpty(_lastAnnouncement)) return false;
            if (announcement != _lastAnnouncement) return false;
            return (DateTime.Now - _lastAnnouncementTime).TotalSeconds < DUPLICATE_THRESHOLD_SECONDS;
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

                // Zone transfers
                { "UpdateZoneUXEvent", DuelEventType.ZoneTransfer },

                // Life and damage
                { "LifeTotalUpdateUXEvent", DuelEventType.LifeChange },
                { "UXEventDamageDealt", DuelEventType.DamageDealt },

                // Card reveals
                { "RevealCardsUXEvent", DuelEventType.CardRevealed },
                { "UpdateRevealedCardUXEvent", DuelEventType.CardRevealed },

                // Counters
                { "CountersChangedUXEvent", DuelEventType.CountersChanged },

                // Game end
                { "GameEndUXEvent", DuelEventType.GameEnd },
                { "DeletePlayerUXEvent", DuelEventType.GameEnd },

                // Combat
                { "ToggleCombatUXEvent", DuelEventType.Combat },
                { "AttackLobUXEvent", DuelEventType.Combat },
                { "AttackDecrementUXEvent", DuelEventType.Combat },

                // Target selection
                { "PlayerSelectingTargetsEventTranslator", DuelEventType.TargetSelection },
                { "PlayerSubmittedTargetsEventTranslator", DuelEventType.TargetConfirmed },

                // Resolution events (track spell/ability source for damage announcements)
                { "ResolutionEventStartedUXEvent", DuelEventType.ResolutionStarted },
                { "ResolutionEventEndedUXEvent", DuelEventType.ResolutionEnded },

                // Card model updates (might contain damage info)
                { "UpdateCardModelUXEvent", DuelEventType.CardModelUpdate },

                // Zone transfers (creature deaths)
                { "ZoneTransferGroup", DuelEventType.ZoneTransferGroup },

                // Combat events
                { "CombatFrame", DuelEventType.CombatFrame },

                // Multistep effects (scry, surveil, library manipulation)
                { "MultistepEffectStartedUXEvent", DuelEventType.MultistepEffect },

                // Ignored events
                { "WaitForSecondsUXEvent", DuelEventType.Ignored },
                { "CallbackUXEvent", DuelEventType.Ignored },
                { "ParallelPlaybackUXEvent", DuelEventType.Ignored },
                { "CardViewImmediateUpdateUXEvent", DuelEventType.Ignored },
                { "GameStatePlaybackCommencedUXEvent", DuelEventType.Ignored },
                { "GameStatePlaybackCompletedUXEvent", DuelEventType.Ignored },
                { "GrePromptUXEvent", DuelEventType.Ignored },
                { "QuarryHaloUXEvent", DuelEventType.Ignored },
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
                { "ManaProducedUXEvent", DuelEventType.Ignored },
                { "UpdateManaPoolUXEvent", DuelEventType.Ignored },
            };
        }

        #endregion
    }

    public enum DuelEventType
    {
        Ignored,
        TurnChange,
        PhaseChange,
        ZoneTransfer,
        LifeChange,
        DamageDealt,
        CardRevealed,
        CountersChanged,
        GameEnd,
        Combat,
        TargetSelection,
        TargetConfirmed,
        ResolutionStarted,
        ResolutionEnded,
        CardModelUpdate,
        ZoneTransferGroup,
        CombatFrame,
        MultistepEffect
    }
}
