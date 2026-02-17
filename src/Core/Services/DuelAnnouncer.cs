using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AccessibleArena.Core.Services
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

        // Track whose turn it currently is
        private bool _isUserTurn = true;

        // Current combat phase tracking
        private string _currentPhase;
        private string _currentStep;

        // Phase announcement debounce (100ms) to avoid spam during auto-skip
        private string _pendingPhaseAnnouncement;
        private float _phaseDebounceTimer;
        private const float PHASE_DEBOUNCE_SECONDS = 0.1f;

        /// <summary>
        /// Returns true if currently in Declare Attackers phase.
        /// </summary>
        public bool IsInDeclareAttackersPhase => _currentPhase == "Combat" && _currentStep == "DeclareAttack";

        /// <summary>
        /// Returns true if currently in Declare Blockers phase.
        /// </summary>
        public bool IsInDeclareBlockersPhase => _currentPhase == "Combat" && _currentStep == "DeclareBlock";

        /// <summary>
        /// Gets the current turn and phase information for announcement (T key).
        /// Returns a formatted string like "Your first main phase, turn 5"
        /// </summary>
        public string GetTurnPhaseInfo()
        {
            string owner = _isUserTurn ? Strings.Duel_Your : Strings.Duel_Opponents;
            string phaseDescription = Strings.GetPhaseDescription(_currentPhase, _currentStep)
                                      ?? Strings.Duel_PhaseDesc_Turn;

            return Strings.Duel_TurnPhase(owner, phaseDescription, _userTurnCount);
        }

        /// <summary>
        /// Gets a human-readable description of the current phase and step.
        /// Delegates to Strings.GetPhaseDescription() for localization.
        /// </summary>
        private string GetPhaseDescription()
        {
            return Strings.GetPhaseDescription(_currentPhase, _currentStep);
        }

        /// <summary>
        /// Returns true if a spell resolved or a permanent entered battlefield within the last specified milliseconds.
        /// Used to skip targeting mode for lands and non-targeted cards.
        /// </summary>
        public bool DidSpellResolveRecently(int withinMs = 500)
        {
            return (DateTime.Now - _lastSpellResolvedTime).TotalMilliseconds < withinMs;
        }

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
            _isUserTurn = true;
            _pendingPhaseAnnouncement = null;
        }

        /// <summary>
        /// Call each frame to flush debounced phase announcements.
        /// </summary>
        public void Update()
        {
            if (_pendingPhaseAnnouncement == null) return;

            _phaseDebounceTimer -= UnityEngine.Time.deltaTime;
            if (_phaseDebounceTimer <= 0)
            {
                _announcer.Announce(_pendingPhaseAnnouncement, AnnouncementPriority.Low);
                _lastAnnouncement = _pendingPhaseAnnouncement;
                _lastAnnouncementTime = DateTime.Now;
                _pendingPhaseAnnouncement = null;
            }
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
                    DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"NEW EVENT TYPE SEEN: {typeName}");
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
                case DuelEventType.ManaPool:
                    return HandleManaPoolEvent(uxEvent);
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

                // Track whose turn it is
                _isUserTurn = isYourTurn;

                // Track our own turn count (game counts each half-turn, we want full cycles)
                if (isYourTurn)
                {
                    _userTurnCount++;
                    return Strings.Duel_YourTurn(_userTurnCount);
                }
                else
                {
                    return Strings.Duel_OpponentTurn;
                }
            }
            catch
            {
                return Strings.Duel_TurnChanged;
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

            // Try to auto-correct local player ID from zone strings containing "(LocalPlayer)"
            TryUpdateLocalPlayerIdFromZoneString(zoneStr);

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
                            ? Strings.Duel_Drew(diff)
                            : Strings.Duel_OpponentDrew(diff);
                    }
                    else if (diff < 0 && isOpponent)
                    {
                        return Strings.Duel_OpponentPlayedCard;
                    }
                }
                else if (zoneName == "Battlefield")
                {
                    if (diff > 0)
                    {
                        if (isOpponent)
                            return Strings.Duel_OpponentEnteredBattlefield(diff);
                        _lastSpellResolvedTime = DateTime.Now;
                    }
                    else if (diff < 0)
                    {
                        int removed = Math.Abs(diff);
                        // Battlefield is a shared zone - can't determine ownership from zone string
                        // Graveyard/Exile announcements will specify correct ownership
                        return Strings.Duel_LeftBattlefield(removed);
                    }
                }
                else if (zoneName == "Graveyard" && diff > 0)
                {
                    return isOpponent ? Strings.Duel_CardToOpponentGraveyard : Strings.Duel_CardToYourGraveyard;
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
                        return Strings.Duel_SpellResolved;
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

                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Life event: affectedId={affectedId}, change={change}, localPlayer={_localPlayerId}");

                if (change == 0) return null;

                // Determine ownership - prioritize avatar field with "LocalPlayer"/"Opponent" strings
                // This is the same pattern that works correctly for combat damage
                bool isLocal = false;
                bool ownershipDetermined = false;

                // First, check avatar field for explicit LocalPlayer/Opponent markers
                var avatar = GetFieldValue<object>(uxEvent, "_avatar");
                if (avatar != null)
                {
                    var avatarStr = avatar.ToString();
                    DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Life event avatar: {avatarStr}");

                    if (avatarStr.Contains("LocalPlayer"))
                    {
                        isLocal = true;
                        ownershipDetermined = true;
                    }
                    else if (avatarStr.Contains("Opponent"))
                    {
                        isLocal = false;
                        ownershipDetermined = true;
                    }
                }

                // Fallback to AffectedId comparison (only if not 0, since 0 is ambiguous)
                if (!ownershipDetermined && affectedId != 0)
                {
                    isLocal = affectedId == _localPlayerId;
                    ownershipDetermined = true;
                }

                // If still not determined, try to extract from avatar string with player ID
                if (!ownershipDetermined && avatar != null)
                {
                    var avatarStr = avatar.ToString();
                    // Check if avatar contains our local player ID
                    if (avatarStr.Contains($"#{_localPlayerId}") || avatarStr.Contains($"Player {_localPlayerId}"))
                    {
                        isLocal = true;
                    }
                    else
                    {
                        // If it contains any player reference that isn't ours, it's opponent
                        isLocal = false;
                    }
                }

                string who = isLocal ? Strings.Duel_You : Strings.Duel_Opponent;
                return change > 0
                    ? Strings.Duel_LifeGained(who, Math.Abs(change))
                    : Strings.Duel_LifeLost(who, Math.Abs(change));
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

                string damageText;
                if (!string.IsNullOrEmpty(sourceName))
                {
                    damageText = Strings.Duel_DamageDeals(sourceName, damage, targetName);
                }
                else
                {
                    damageText = Strings.Duel_DamageAmount(damage, targetName);
                }

                // Append damage type modifiers (lifelink, trample, etc.)
                if (!string.IsNullOrEmpty(damageFlags))
                {
                    damageText += " " + damageFlags;
                }

                return damageText;
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
            DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"=== {label} TYPE: {type.FullName} ===");

            // Log all fields
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                try
                {
                    var value = field.GetValue(uxEvent);
                    string valueStr = value?.ToString() ?? "null";
                    if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                    DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Field: {field.Name} = {valueStr} ({field.FieldType.Name})");
                }
                catch (Exception ex)
                {
                    DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Field: {field.Name} = [Error: {ex.Message}]");
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
                    DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Property: {prop.Name} = {valueStr} ({prop.PropertyType.Name})");
                }
                catch (Exception ex)
                {
                    DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Property: {prop.Name} = [Error: {ex.Message}]");
                }
            }

            DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"=== END {label} FIELDS ===");
        }

        private void LogDamageEventFields(object uxEvent)
        {
            if (_damageEventFieldsLogged || uxEvent == null) return;
            _damageEventFieldsLogged = true;

            var type = uxEvent.GetType();
            DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"=== DAMAGE EVENT TYPE: {type.FullName} ===");

            // Log all fields
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                try
                {
                    var value = field.GetValue(uxEvent);
                    string valueStr = value?.ToString() ?? "null";
                    if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                    DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Field: {field.Name} = {valueStr} ({field.FieldType.Name})");
                }
                catch (Exception ex)
                {
                    DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Field: {field.Name} = [Error: {ex.Message}]");
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
                    DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Property: {prop.Name} = {valueStr} ({prop.PropertyType.Name})");
                }
                catch (Exception ex)
                {
                    DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Property: {prop.Name} = [Error: {ex.Message}]");
                }
            }

            DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"=== END DAMAGE EVENT FIELDS ===");
        }

        private string GetDamageTargetName(uint targetPlayerId, uint targetInstanceId)
        {
            // If target is a player
            if (targetPlayerId == _localPlayerId)
                return Strings.Duel_DamageToYou;
            if (targetPlayerId != 0)
                return Strings.Duel_DamageToOpponent;

            // Try to find target card by InstanceId
            if (targetInstanceId != 0)
            {
                string cardName = FindCardNameByInstanceId(targetInstanceId);
                if (!string.IsNullOrEmpty(cardName))
                    return cardName;
            }

            return Strings.Duel_DamageTarget;
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
                        DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Found source from {fieldName}: {name}");
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
                        DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Found source from {fieldName} (GrpId): {name}");
                        return name;
                    }
                }
            }

            // Check if damage is from combat (during CombatDamage step)
            if (_currentPhase == "Combat" && _currentStep == "CombatDamage")
            {
                return Strings.Duel_CombatDamageSource;
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

                // Check if we're leaving Declare Attackers phase - announce attacker count and details
                string attackerAnnouncement = null;
                if (_currentStep == "DeclareAttack" && step != "DeclareAttack")
                {
                    var attackers = GetAttackingCreaturesInfo();
                    if (attackers.Count > 0)
                    {
                        // Build announcement: "X attackers. Name P/T. Name P/T."
                        var parts = new List<string>();
                        parts.Add(Strings.Duel_Attackers(attackers.Count));
                        parts.AddRange(attackers);
                        attackerAnnouncement = string.Join(". ", parts);
                        DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Leaving declare attackers: {attackerAnnouncement}");
                    }
                }

                // Track current phase/step for combat navigation
                _currentPhase = phase;
                _currentStep = step;
                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Phase change: {phase}/{step}");

                string phaseAnnouncement = null;
                if (phase == "Main1") phaseAnnouncement = Strings.Duel_Phase_FirstMain;
                else if (phase == "Main2") phaseAnnouncement = Strings.Duel_Phase_SecondMain;
                else if (phase == "Combat")
                {
                    if (step == "DeclareAttack") phaseAnnouncement = Strings.Duel_Phase_DeclareAttackers;
                    else if (step == "DeclareBlock") phaseAnnouncement = Strings.Duel_Phase_DeclareBlockers;
                    else if (step == "CombatDamage") phaseAnnouncement = Strings.Duel_Phase_CombatDamage;
                    else if (step == "EndCombat") phaseAnnouncement = Strings.Duel_Phase_EndOfCombat;
                    else if (step == "None") phaseAnnouncement = Strings.Duel_Phase_Combat;
                }
                else if (phase == "Beginning" && step == "Upkeep") phaseAnnouncement = Strings.Duel_Phase_Upkeep;
                else if (phase == "Beginning" && step == "Draw") phaseAnnouncement = Strings.Duel_Phase_Draw;
                else if (phase == "Ending" && step == "End") phaseAnnouncement = Strings.Duel_Phase_EndStep;

                // If we have attacker info, announce immediately (this is a real combat stop, not auto-skip)
                if (attackerAnnouncement != null)
                {
                    _pendingPhaseAnnouncement = null;
                    if (phaseAnnouncement != null)
                        return $"{attackerAnnouncement}. {phaseAnnouncement}";
                    return attackerAnnouncement;
                }

                // Queue phase announcement for debounce - only the last phase in a rapid sequence gets spoken
                if (phaseAnnouncement != null)
                {
                    _pendingPhaseAnnouncement = phaseAnnouncement;
                    _phaseDebounceTimer = PHASE_DEBOUNCE_SECONDS;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets all creatures currently declared as attackers with their name and P/T.
        /// Looks for cards with "IsAttacking" child indicator (existence, not active state).
        /// </summary>
        private List<string> GetAttackingCreaturesInfo()
        {
            var attackers = new List<string>();
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy)
                    continue;

                // Only check CDC (card) objects
                if (!go.name.StartsWith("CDC "))
                    continue;

                // Check if this card has an "IsAttacking" indicator
                // Note: The indicator may be inactive (activeInHierarchy=false) but still present,
                // which means the creature IS attacking. We count if the child EXISTS, not just if active.
                bool isAttacking = false;
                foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "IsAttacking")
                    {
                        isAttacking = true;
                        break;
                    }
                }

                if (isAttacking)
                {
                    // Get card name and P/T
                    var info = CardDetector.ExtractCardInfo(go);
                    string attackerInfo = info.Name ?? "Unknown";
                    if (!string.IsNullOrEmpty(info.PowerToughness))
                    {
                        attackerInfo += $" {info.PowerToughness}";
                    }
                    attackers.Add(attackerInfo);
                }
            }
            return attackers;
        }

        private string BuildRevealAnnouncement(object uxEvent)
        {
            try
            {
                var cardName = GetFieldValue<string>(uxEvent, "CardName");
                return !string.IsNullOrEmpty(cardName) ? Strings.Duel_Revealed(cardName) : null;
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

                string target = !string.IsNullOrEmpty(cardName) ? cardName : Strings.Duel_CounterCreature;

                return Strings.Duel_CounterChanged(target, Math.Abs(change), counterType, change > 0);
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
                return winnerId == _localPlayerId ? Strings.Duel_Victory : Strings.Duel_Defeat;
            }
            catch
            {
                return Strings.Duel_GameEnded;
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
                    if (modeValue == "CombatBegun") return Strings.Duel_CombatBegins;
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
                if (typeName == "AttackDecrementUXEvent") return Strings.Duel_AttackerRemoved;

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
                bool isOpponent = false;

                // Look up card by InstanceId
                if (attackerId != 0)
                {
                    cardName = FindCardNameByInstanceId(attackerId);

                    // Get P/T and ownership from the card model
                    var (power, toughness, isOpp) = GetCardPowerToughnessAndOwnerByInstanceId(attackerId);
                    if (power >= 0 && toughness >= 0)
                    {
                        powerToughness = $"{power}/{toughness}";
                    }
                    isOpponent = isOpp;
                }

                // Build announcement with ownership prefix for opponent's attackers
                string ownerPrefix = isOpponent ? Strings.Duel_OwnerPrefix_Opponent : "";

                if (!string.IsNullOrEmpty(cardName))
                {
                    if (!string.IsNullOrEmpty(powerToughness))
                        return Strings.Duel_AttackingPT($"{ownerPrefix}{cardName}", powerToughness);
                    return Strings.Duel_Attacking($"{ownerPrefix}{cardName}");
                }

                return isOpponent ? Strings.Duel_OpponentAttackerDeclared : Strings.Duel_AttackerDeclared;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error building attacker announcement: {ex.Message}");
                return Strings.Duel_AttackerDeclared;
            }
        }

        private (int power, int toughness) GetCardPowerToughnessByInstanceId(uint instanceId)
        {
            var (power, toughness, _) = GetCardPowerToughnessAndOwnerByInstanceId(instanceId);
            return (power, toughness);
        }

        private (int power, int toughness, bool isOpponent) GetCardPowerToughnessAndOwnerByInstanceId(uint instanceId)
        {
            if (instanceId == 0) return (-1, -1, false);

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
                    int power = -1;
                    int toughness = -1;
                    bool isOpponent = false;

                    var powerProp = modelType.GetProperty("Power");
                    var toughnessProp = modelType.GetProperty("Toughness");

                    if (powerProp != null && toughnessProp != null)
                    {
                        var powerVal = powerProp.GetValue(model);
                        var toughnessVal = toughnessProp.GetValue(model);

                        if (powerVal is int p && toughnessVal is int t)
                        {
                            power = p;
                            toughness = t;
                        }
                    }

                    // Check ownership from ControllerNum property
                    var controllerProp = modelType.GetProperty("ControllerNum");
                    if (controllerProp != null)
                    {
                        var controller = controllerProp.GetValue(model);
                        isOpponent = controller?.ToString() == "Opponent";
                    }

                    return (power, toughness, isOpponent);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error getting P/T and owner by InstanceId: {ex.Message}");
            }

            return (-1, -1, false);
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
                                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Creature damage: {cardName} now has {damage} damage (dealt: {damageDealt})");

                                // Try to correlate with last resolving card
                                if (!string.IsNullOrEmpty(_lastResolvingCardName))
                                {
                                    return Strings.Duel_DamageDeals(_lastResolvingCardName, (int)damageDealt, cardName);
                                }
                                return Strings.Duel_DamageAmount((int)damageDealt, cardName);
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

        // Track if we've logged ZoneTransferUXEvent fields (once for discovery)
        private static bool _zoneTransferUXEventFieldsLogged = false;

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

                // Get the _zoneTransfers list which contains individual ZoneTransferUXEvent items
                var zoneTransfers = GetFieldValue<object>(uxEvent, "_zoneTransfers");
                if (zoneTransfers == null) return null;

                var transferList = zoneTransfers as System.Collections.IEnumerable;
                if (transferList == null) return null;

                var announcements = new List<string>();

                foreach (var transfer in transferList)
                {
                    if (transfer == null) continue;

                    // Log ZoneTransferUXEvent fields once for discovery
                    if (!_zoneTransferUXEventFieldsLogged)
                    {
                        _zoneTransferUXEventFieldsLogged = true;
                        LogEventFields(transfer, "ZONE TRANSFER UX EVENT");
                    }

                    // Extract zone transfer details
                    var announcement = ProcessZoneTransfer(transfer);
                    if (!string.IsNullOrEmpty(announcement))
                    {
                        announcements.Add(announcement);
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
                MelonLogger.Warning($"[DuelAnnouncer] Error handling zone transfer: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Processes a single zone transfer event to announce game state changes.
        /// Handles: land plays, creatures dying, cards discarded, exiled, bounced, tokens created, etc.
        /// </summary>
        private string ProcessZoneTransfer(object transfer)
        {
            try
            {
                // Get zone types and reason
                var toZoneType = GetFieldValue<object>(transfer, "ToZoneType");
                var fromZoneType = GetFieldValue<object>(transfer, "FromZoneType");
                var toZone = GetFieldValue<object>(transfer, "ToZone");
                var fromZone = GetFieldValue<object>(transfer, "FromZone");
                var reason = GetFieldValue<object>(transfer, "Reason");

                string toZoneTypeStr = toZoneType?.ToString() ?? "";
                string fromZoneTypeStr = fromZoneType?.ToString() ?? "";
                string toZoneStr = toZone?.ToString() ?? "";
                string fromZoneStr = fromZone?.ToString() ?? "";
                string reasonStr = reason?.ToString() ?? "";

                // Get card instance - the NewInstance field contains the card data
                var newInstance = GetFieldValue<object>(transfer, "NewInstance");

                uint grpId = 0;
                bool isOpponent = false;

                if (newInstance != null)
                {
                    // Try to get GrpId from the card instance
                    var printing = GetNestedPropertyValue<object>(newInstance, "Printing");
                    if (printing != null)
                    {
                        grpId = GetNestedPropertyValue<uint>(printing, "GrpId");
                    }
                    if (grpId == 0)
                    {
                        grpId = GetNestedPropertyValue<uint>(newInstance, "GrpId");
                    }

                    // Check ownership via controller - try multiple property names
                    uint controllerId = GetNestedPropertyValue<uint>(newInstance, "ControllerSeatId");
                    if (controllerId == 0) controllerId = GetNestedPropertyValue<uint>(newInstance, "ControllerId");
                    if (controllerId == 0) controllerId = GetNestedPropertyValue<uint>(newInstance, "OwnerSeatId");
                    if (controllerId == 0) controllerId = GetNestedPropertyValue<uint>(newInstance, "OwnerNum");
                    if (controllerId == 0) controllerId = GetNestedPropertyValue<uint>(newInstance, "ControllerNum");

                    // Try Owner property which might be a player object
                    if (controllerId == 0)
                    {
                        var owner = GetNestedPropertyValue<object>(newInstance, "Owner");
                        if (owner != null)
                        {
                            controllerId = GetNestedPropertyValue<uint>(owner, "SeatId");
                            if (controllerId == 0) controllerId = GetNestedPropertyValue<uint>(owner, "Id");
                            if (controllerId == 0) controllerId = GetNestedPropertyValue<uint>(owner, "PlayerNumber");
                        }
                    }

                    DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"ControllerId={controllerId}, _localPlayerId={_localPlayerId}");
                    isOpponent = controllerId != 0 && controllerId != _localPlayerId;
                }

                // Check zone strings for ownership hints as fallback
                // Zone format example: "Library (PlayerPlayer: 1 (LocalPlayer), 0 cards)" or "Hand (OpponentPlayer: 2, 5 cards)"
                // For cards entering battlefield from hand, check FromZone (hand) for ownership
                // For cards leaving battlefield, check FromZone (battlefield area might not have owner)
                string zoneToCheck = fromZoneStr;
                if (string.IsNullOrEmpty(zoneToCheck) || !zoneToCheck.Contains("Player"))
                {
                    zoneToCheck = toZoneStr;
                }

                // Log zone strings for debugging ownership detection
                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Zone strings - From: '{fromZoneStr}', To: '{toZoneStr}', checking: '{zoneToCheck}'");

                // Try to auto-correct local player ID from zone strings containing "(LocalPlayer)"
                TryUpdateLocalPlayerIdFromZoneString(fromZoneStr);
                TryUpdateLocalPlayerIdFromZoneString(toZoneStr);

                // Check zone strings for ownership - use _localPlayerId dynamically, don't hardcode Player 1/2
                if (zoneToCheck.Contains("Opponent"))
                    isOpponent = true;
                else if (zoneToCheck.Contains("LocalPlayer"))
                    isOpponent = false;
                else if (zoneToCheck.Contains($"Player: {_localPlayerId}") || zoneToCheck.Contains($"Player:{_localPlayerId}"))
                    isOpponent = false;
                else if (zoneToCheck.Contains("Player: ") || zoneToCheck.Contains("Player:"))
                    isOpponent = true; // Contains a player reference but not our ID, so it's opponent

                // Log for debugging
                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"ZoneTransfer: {fromZoneTypeStr} -> {toZoneTypeStr}, Reason={reasonStr}, GrpId={grpId}, isOpponent={isOpponent}");

                // Skip if no card data
                if (grpId == 0)
                {
                    return null;
                }

                // Get card name
                string cardName = CardModelProvider.GetNameFromGrpId(grpId);
                if (string.IsNullOrEmpty(cardName))
                {
                    return null;
                }

                string ownerPrefix = isOpponent ? Strings.Duel_OwnerPrefix_Opponent : "";
                string announcement = null;

                // Determine announcement based on zone transfer type
                switch (toZoneTypeStr)
                {
                    case "Battlefield":
                        announcement = ProcessBattlefieldEntry(fromZoneTypeStr, reasonStr, cardName, grpId, newInstance, isOpponent);
                        if (announcement != null)
                            _lastSpellResolvedTime = DateTime.Now;
                        break;

                    case "Graveyard":
                        announcement = ProcessGraveyardEntry(fromZoneTypeStr, reasonStr, cardName, ownerPrefix);
                        break;

                    case "Exile":
                        announcement = ProcessExileEntry(fromZoneTypeStr, reasonStr, cardName, ownerPrefix);
                        break;

                    case "Hand":
                        announcement = ProcessHandEntry(fromZoneTypeStr, reasonStr, cardName, isOpponent);
                        break;

                    case "Stack":
                        // Spells on stack are announced via UpdateZoneUXEvent already
                        break;
                }

                if (!string.IsNullOrEmpty(announcement))
                {
                    DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Zone transfer announcement: {announcement}");
                }

                return announcement;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error processing zone transfer: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Process card entering battlefield - lands, tokens, creatures from stack
        /// </summary>
        private string ProcessBattlefieldEntry(string fromZone, string reason, string cardName, uint grpId, object cardInstance, bool isOpponent)
        {
            string owner = isOpponent ? Strings.Duel_Opponent : Strings.Duel_You;

            // Token creation (from None zone with CardCreated reason)
            // Note: Game doesn't provide ownership info for tokens, so we don't announce who created it
            if ((fromZone == "None" || string.IsNullOrEmpty(fromZone)) && reason == "CardCreated")
            {
                return Strings.Duel_TokenCreated(cardName);
            }

            // Check if this card is an aura/equipment attaching to another card
            string attachedToName = GetAttachedToName(cardInstance);

            // Land played (from Hand, not from Stack)
            if (fromZone == "Hand")
            {
                bool isLand = IsLandByGrpId(grpId, cardInstance);
                if (isLand)
                {
                    return Strings.Duel_Played(owner, cardName);
                }
                // Non-land from hand without going through stack (e.g., put onto battlefield effects)
                if (!string.IsNullOrEmpty(attachedToName))
                {
                    return Strings.Duel_Enchanted(cardName, attachedToName);
                }
                return Strings.Duel_EntersBattlefield(cardName);
            }

            // From stack = spell resolved (creature/artifact/enchantment)
            if (fromZone == "Stack")
            {
                // Check if it's an aura that attached to something
                if (!string.IsNullOrEmpty(attachedToName))
                {
                    return Strings.Duel_Enchanted(cardName, attachedToName);
                }
                // We already announce spell cast, so just note it entered
                // Could skip this to avoid double announcement, or make it brief
                return null; // Skip - UpdateZoneUXEvent handles "spell resolved"
            }

            // From graveyard = reanimation
            if (fromZone == "Graveyard")
            {
                if (!string.IsNullOrEmpty(attachedToName))
                {
                    return Strings.Duel_ReturnedFromGraveyardEnchanting(cardName, attachedToName);
                }
                return Strings.Duel_ReturnedFromGraveyard(cardName);
            }

            // From exile = returned from exile
            if (fromZone == "Exile")
            {
                if (!string.IsNullOrEmpty(attachedToName))
                {
                    return Strings.Duel_ReturnedFromExileEnchanting(cardName, attachedToName);
                }
                return Strings.Duel_ReturnedFromExile(cardName);
            }

            // From library = put onto battlefield from library
            if (fromZone == "Library")
            {
                if (!string.IsNullOrEmpty(attachedToName))
                {
                    return Strings.Duel_EntersBattlefieldFromLibraryEnchanting(cardName, attachedToName);
                }
                return Strings.Duel_EntersBattlefieldFromLibrary(cardName);
            }

            return null;
        }

        /// <summary>
        /// Gets the name of the card that this card is attached to (for auras/equipment).
        /// Returns null if not attached to anything.
        /// </summary>
        private string GetAttachedToName(object cardInstance)
        {
            if (cardInstance == null) return null;

            try
            {
                // Use AttachedToId (the game's real attachment tracking field)
                uint attachedToId = GetNestedPropertyValue<uint>(cardInstance, "AttachedToId");
                if (attachedToId == 0) return null;

                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Card has AttachedToId={attachedToId}, scanning battlefield for parent");

                // Find the parent card on the battlefield by scanning all CDCs
                foreach (var go in GameObject.FindObjectsOfType<GameObject>())
                {
                    if (go == null || !go.activeInHierarchy) continue;
                    if (!go.name.Contains("BattlefieldCardHolder")) continue;

                    foreach (var child in go.GetComponentsInChildren<Transform>(true))
                    {
                        if (child == null || !child.gameObject.activeInHierarchy) continue;

                        var cdc = CardModelProvider.GetDuelSceneCDC(child.gameObject);
                        if (cdc == null) continue;

                        var model = CardModelProvider.GetCardModel(cdc);
                        if (model == null) continue;

                        var instanceIdProp = model.GetType().GetProperty("InstanceId");
                        if (instanceIdProp == null) continue;

                        var idVal = instanceIdProp.GetValue(model);
                        if (!(idVal is uint instanceId) || instanceId != attachedToId) continue;

                        // Found matching card - get its name
                        var grpIdProp = model.GetType().GetProperty("GrpId");
                        if (grpIdProp != null)
                        {
                            var gidVal = grpIdProp.GetValue(model);
                            if (gidVal is uint grpId && grpId > 0)
                            {
                                string parentName = CardModelProvider.GetNameFromGrpId(grpId);
                                if (!string.IsNullOrEmpty(parentName))
                                {
                                    DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Card is attached to: {parentName} (InstanceId={attachedToId})");
                                    return parentName;
                                }
                            }
                        }
                    }
                    break; // Only one battlefield holder needed
                }

                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"AttachedToId={attachedToId} but parent not found on battlefield");
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Error getting attached-to name: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Process card entering graveyard - death, destruction, discard, mill, counter
        /// </summary>
        private string ProcessGraveyardEntry(string fromZone, string reason, string cardName, string ownerPrefix)
        {
            // Use reason for specific language if available
            switch (reason)
            {
                case "Died":
                    return Strings.Duel_Died(ownerPrefix, cardName);
                case "Destroyed":
                    return Strings.Duel_Destroyed(ownerPrefix, cardName);
                case "Sacrificed":
                    return Strings.Duel_Sacrificed(ownerPrefix, cardName);
                case "Countered":
                    return Strings.Duel_Countered(ownerPrefix, cardName);
                case "Discarded":
                    return Strings.Duel_Discarded(ownerPrefix, cardName);
                case "Milled":
                    return Strings.Duel_Milled(ownerPrefix, cardName);
            }

            // Fallback based on source zone
            switch (fromZone)
            {
                case "Battlefield":
                    return Strings.Duel_Died(ownerPrefix, cardName);
                case "Hand":
                    return Strings.Duel_Discarded(ownerPrefix, cardName);
                case "Stack":
                    // Don't announce "countered" as fallback - countering is only when reason == "Countered"
                    // Normal spell resolution (instant/sorcery) also goes Stack -> Graveyard
                    // "Spell resolved" is already announced via UpdateZoneUXEvent, so skip here
                    return null;
                case "Library":
                    return Strings.Duel_Milled(ownerPrefix, cardName);
                default:
                    return Strings.Duel_WentToGraveyard(ownerPrefix, cardName);
            }
        }

        /// <summary>
        /// Process card entering exile
        /// </summary>
        private string ProcessExileEntry(string fromZone, string reason, string cardName, string ownerPrefix)
        {
            // Check for countered spells that exile (e.g., Dissipate, Syncopate)
            if (reason == "Countered")
            {
                return Strings.Duel_CounteredAndExiled(ownerPrefix, cardName);
            }

            if (fromZone == "Battlefield")
            {
                return Strings.Duel_Exiled(ownerPrefix, cardName);
            }
            if (fromZone == "Graveyard")
            {
                return Strings.Duel_ExiledFromGraveyard(ownerPrefix, cardName);
            }
            if (fromZone == "Hand")
            {
                return Strings.Duel_ExiledFromHand(ownerPrefix, cardName);
            }
            if (fromZone == "Library")
            {
                return Strings.Duel_ExiledFromLibrary(ownerPrefix, cardName);
            }
            if (fromZone == "Stack")
            {
                // Spell from stack to exile without Countered reason - could be an effect
                // Skip announcement since "Spell resolved" handles the stack clearing
                return null;
            }
            return Strings.Duel_Exiled(ownerPrefix, cardName);
        }

        /// <summary>
        /// Process card entering hand - bounce, draw (draw handled elsewhere)
        /// </summary>
        private string ProcessHandEntry(string fromZone, string reason, string cardName, bool isOpponent)
        {
            string ownerPrefix = isOpponent ? Strings.Duel_OwnerPrefix_Opponent : "";

            // Bounce from battlefield
            if (fromZone == "Battlefield")
            {
                return Strings.Duel_ReturnedToHand(ownerPrefix, cardName);
            }

            // From library = draw, but we handle this via UpdateZoneUXEvent with count
            // Don't duplicate the announcement
            if (fromZone == "Library")
            {
                return null;
            }

            // From graveyard = returned to hand
            if (fromZone == "Graveyard")
            {
                return Strings.Duel_ReturnedToHandFromGraveyard(ownerPrefix, cardName);
            }

            // From exile = returned from exile to hand
            if (fromZone == "Exile")
            {
                return Strings.Duel_ReturnedToHandFromExile(ownerPrefix, cardName);
            }

            return null;
        }

        /// <summary>
        /// Checks if a card is a land based on its GrpId or card object.
        /// </summary>
        private bool IsLandByGrpId(uint grpId, object card)
        {
            // Try to get card types from the card object
            if (card != null)
            {
                // Check IsBasicLand property
                var isBasicLand = GetNestedPropertyValue<bool>(card, "IsBasicLand");
                if (isBasicLand) return true;

                // Check IsLandButNotBasic property
                var isLandNotBasic = GetNestedPropertyValue<bool>(card, "IsLandButNotBasic");
                if (isLandNotBasic) return true;

                // Check CardTypes collection
                var cardTypes = GetNestedPropertyValue<object>(card, "CardTypes");
                if (cardTypes is System.Collections.IEnumerable typeEnum)
                {
                    foreach (var ct in typeEnum)
                    {
                        if (ct?.ToString()?.Contains("Land") == true)
                        {
                            return true;
                        }
                    }
                }
            }

            // Fallback: check if card name is a basic land
            string cardName = CardModelProvider.GetNameFromGrpId(grpId);
            if (!string.IsNullOrEmpty(cardName))
            {
                // Common basic land names in various languages
                var basicLandNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    // English
                    "Plains", "Island", "Swamp", "Mountain", "Forest",
                    // German
                    "Ebene", "Insel", "Sumpf", "Gebirge", "Wald",
                    // French
                    "Plaine", "Île", "Marais", "Montagne", "Forêt",
                    // Spanish
                    "Llanura", "Isla", "Pantano", "Montaña", "Bosque",
                    // Italian
                    "Pianura", "Isola", "Palude", "Montagna", "Foresta",
                    // Portuguese
                    "Planície", "Ilha", "Pântano", "Montanha", "Floresta"
                };

                if (basicLandNames.Contains(cardName))
                {
                    return true;
                }
            }

            return false;
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
                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"CombatFrame: OpponentDamageDealt={opponentDamage}");

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
                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Branch counts: _branches={branchCount}, _runningBranches={runningCount}");
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
                                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Branch[{branchIndex}]: {dmg.SourceName} -> {dmg.TargetName}, Amount={dmg.Amount}");
                            }

                            // Build grouped announcement for this combat pair
                            if (damageChain.Count == 1)
                            {
                                // Single damage (unblocked or one-sided)
                                var dmg = damageChain[0];
                                if (dmg.Amount > 0 && !string.IsNullOrEmpty(dmg.SourceName) && !string.IsNullOrEmpty(dmg.TargetName))
                                {
                                    announcements.Add(Strings.Duel_DamageDeals(dmg.SourceName, dmg.Amount, dmg.TargetName));
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
                                        parts.Add(Strings.Duel_DamageDeals(dmg.SourceName, dmg.Amount, dmg.TargetName));
                                    }
                                }
                                if (parts.Count > 0)
                                {
                                    announcements.Add(string.Join(", ", parts));
                                }
                            }
                            branchIndex++;
                        }
                        DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Total branches: {branchIndex}");
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
                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"MultistepEffect: AbilityCategory={effectName}, Affector={affector}, Affected={affected}");

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
                        effectDescription = Strings.Duel_LookAtTopCard;
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
                    announcement = $"{effectDescription}. {Strings.Duel_ScryHint}";
                }
                else if (effectName.ToLower() == "surveil")
                {
                    announcement = $"{effectDescription}. {Strings.Duel_SurveilHint}";
                }
                else
                {
                    announcement = Strings.Duel_EffectHint(effectDescription);
                }

                if (!string.IsNullOrEmpty(cardName))
                {
                    DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Library browser active: {effectDescription} from {cardName}");
                }
                else
                {
                    DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Library browser active: {effectDescription}");
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
                    info.TargetName = Strings.Duel_DamageToYou;
                }
                else if (targetStr.Contains("Opponent"))
                {
                    info.TargetName = Strings.Duel_DamageToOpponent;
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
            DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Chain BranchDepth={branchDepth}");

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
                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Chain depth {depth}: _nextBranch={(nextBranch != null ? "exists" : "null")}");

                // Follow the chain to get blocker damage
                currentBranch = nextBranch;
                depth++;

                // Safety limit
                if (depth > 10) break;
            }

            DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Chain total depth: {depth}");
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
                    DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Resolution started: {cardName} (InstanceId: {instigatorInstanceId})");
                }

                return null; // Don't announce resolution start, just track it
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error handling resolution: {ex.Message}");
                return null;
            }
        }

        // Track if we've logged mana pool event fields (once per event type for discovery)
        // Store last known mana pool for on-demand queries
        private static string _lastManaPool = "";

        /// <summary>
        /// Gets the current floating mana pool (e.g., "2 Green, 1 Blue").
        /// Returns empty string if no mana floating.
        /// </summary>
        public static string CurrentManaPool => _lastManaPool;

        private string HandleManaPoolEvent(object uxEvent)
        {
            try
            {
                var typeName = uxEvent.GetType().Name;

                // DIAGNOSTIC: Log button state when mana is produced (ability activation mode)
                if (typeName == "ManaProducedUXEvent")
                {
                    DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"=== MANA PRODUCED - Logging button state ===");
                    LogAllPromptButtons();
                }

                // Only process UpdateManaPoolUXEvent for announcements
                if (typeName == "UpdateManaPoolUXEvent")
                {
                    string manaPoolString = ParseManaPool(uxEvent);
                    _lastManaPool = manaPoolString ?? "";

                    if (!string.IsNullOrEmpty(manaPoolString))
                    {
                        DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Mana pool: {manaPoolString}");
                        return Strings.ManaAmount(manaPoolString);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error handling mana event: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// DIAGNOSTIC: Logs all current prompt buttons and their text.
        /// Called during mana payment mode to understand button state.
        /// </summary>
        public static void LogAllPromptButtons()
        {
            try
            {
                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"=== PROMPT BUTTON DIAGNOSTIC ===");

                // Find all prompt buttons
                var allObjects = UnityEngine.GameObject.FindObjectsOfType<UnityEngine.GameObject>();
                int buttonCount = 0;

                foreach (var go in allObjects)
                {
                    if (go == null || !go.activeInHierarchy) continue;

                    // Check for PromptButton objects
                    if (go.name.Contains("PromptButton") || go.name.Contains("ButtonsLayout"))
                    {
                        string buttonText = UITextExtractor.GetText(go);
                        var button = go.GetComponent<UnityEngine.UI.Button>();
                        var selectable = go.GetComponent<UnityEngine.UI.Selectable>();

                        bool isInteractable = (button?.interactable ?? false) || (selectable?.interactable ?? false);

                        DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Button: {go.name}");
                        DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"  Text: '{buttonText}'");
                        DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"  Interactable: {isInteractable}");
                        DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"  Path: {MenuDebugHelper.GetGameObjectPath(go)}");

                        // Check for callbacks
                        if (button != null)
                        {
                            var onClick = button.onClick;
                            int listenerCount = onClick?.GetPersistentEventCount() ?? 0;
                            DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"  OnClick listeners: {listenerCount}");
                        }

                        buttonCount++;
                    }
                }

                // Also check for any active workflow buttons
                foreach (var go in allObjects)
                {
                    if (go == null || !go.activeInHierarchy) continue;

                    // Check for workflow-related UI
                    if (go.name.Contains("AutoTap") || go.name.Contains("ManaPayment") ||
                        go.name.Contains("Workflow") || go.name.Contains("ActionButton"))
                    {
                        string text = UITextExtractor.GetText(go);
                        DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Workflow UI: {go.name} - '{text}'");
                    }
                }

                // Check for any HotHighlight on lands
                int highlightedLands = 0;
                foreach (var go in allObjects)
                {
                    if (go == null || !go.activeInHierarchy) continue;

                    if (CardDetector.IsCard(go) && CardDetector.HasHotHighlight(go))
                    {
                        var (_, isLand, _) = CardDetector.GetCardCategory(go);
                        if (isLand)
                        {
                            highlightedLands++;
                            string cardName = CardDetector.GetCardName(go);
                            DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Highlighted land: {cardName}");
                        }
                    }
                }

                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Total prompt buttons found: {buttonCount}");
                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Total highlighted lands: {highlightedLands}");
                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"=== END PROMPT BUTTON DIAGNOSTIC ===");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error logging buttons: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses the mana pool from UpdateManaPoolUXEvent into a readable string like "2 Green, 1 Blue"
        /// </summary>
        private string ParseManaPool(object uxEvent)
        {
            try
            {
                // Get _newManaPool field (List<MtgMana>)
                var poolField = uxEvent.GetType().GetField("_newManaPool",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (poolField == null) return null;

                var poolObj = poolField.GetValue(uxEvent);
                if (poolObj == null) return null;

                var enumerable = poolObj as System.Collections.IEnumerable;
                if (enumerable == null) return null;

                // Count mana by color
                var manaByColor = new Dictionary<string, int>();

                foreach (var mana in enumerable)
                {
                    if (mana == null) continue;

                    // Try to get Color from property or field
                    var colorProp = mana.GetType().GetProperty("Color");
                    var colorField = mana.GetType().GetField("Color", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                  ?? mana.GetType().GetField("_color", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (colorProp == null && colorField == null) continue;

                    try
                    {
                        object colorValue = null;
                        if (colorProp != null)
                            colorValue = colorProp.GetValue(mana);
                        else if (colorField != null)
                            colorValue = colorField.GetValue(mana);

                        string colorName = colorValue?.ToString() ?? "Unknown";

                        // Convert enum name to readable name using existing function
                        string readableName = CardModelProvider.ConvertManaColorToName(colorName);

                        if (manaByColor.ContainsKey(readableName))
                            manaByColor[readableName]++;
                        else
                            manaByColor[readableName] = 1;
                    }
                    catch { }
                }

                if (manaByColor.Count == 0) return null;

                // Build readable string: "2 Green, 1 Blue, 3 Colorless"
                var parts = new List<string>();
                foreach (var kvp in manaByColor)
                {
                    parts.Add($"{kvp.Value} {kvp.Key}");
                }

                return string.Join(", ", parts);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error parsing mana pool: {ex.Message}");
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

            // Check if this is an ability rather than a spell
            var (isAbility, isTriggered) = CardModelProvider.IsAbilityOnStack(cardObj);

            if (isAbility)
            {
                // Format: "[Name] triggered, [rules text]" or "[Name] activated, [rules text]"
                string abilityVerb = isTriggered ? Strings.AbilityTriggered : Strings.AbilityActivated;
                parts.Add($"{info.Name ?? Strings.AbilityUnknown} {abilityVerb}");
            }
            else
            {
                // Original behavior for spells
                parts.Add($"{Strings.SpellCastPrefix} {info.Name ?? Strings.SpellUnknown}");

                if (!string.IsNullOrEmpty(info.PowerToughness))
                    parts.Add(info.PowerToughness);
            }

            // Brief mode: for own cards, just announce the name/header (skip rules text)
            bool briefMode = AccessibleArenaMod.Instance?.Settings?.BriefCastAnnouncements == true;
            if (briefMode && !CardModelProvider.IsOpponentCard(cardObj))
                return string.Join(", ", parts);

            // Rules text is relevant for both spells and abilities
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

        /// <summary>
        /// Extracts and updates the local player ID from zone strings containing "(LocalPlayer)" marker.
        /// Zone format example: "Library (PlayerPlayer: 2 (LocalPlayer), 0 cards)"
        /// This self-corrects the _localPlayerId if it was incorrectly detected at startup.
        /// </summary>
        private void TryUpdateLocalPlayerIdFromZoneString(string zoneStr)
        {
            if (string.IsNullOrEmpty(zoneStr) || !zoneStr.Contains("(LocalPlayer)"))
                return;

            // Extract player number from pattern like "Player: 2 (LocalPlayer)" or "PlayerPlayer: 2 (LocalPlayer)"
            var match = System.Text.RegularExpressions.Regex.Match(zoneStr, @"Player[^:]*:\s*(\d+)\s*\(LocalPlayer\)");
            if (match.Success && uint.TryParse(match.Groups[1].Value, out uint detectedId))
            {
                if (detectedId != _localPlayerId && detectedId > 0)
                {
                    DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Updating local player ID: {_localPlayerId} -> {detectedId} (detected from zone string)");
                    _localPlayerId = detectedId;
                }
            }
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
                { "ManaProducedUXEvent", DuelEventType.ManaPool },
                { "UpdateManaPoolUXEvent", DuelEventType.ManaPool },
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
        MultistepEffect,
        ManaPool
    }
}
