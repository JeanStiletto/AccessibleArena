using MelonLoader;
using AccessibleArena.Core.Utils;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using static AccessibleArena.Core.Utils.ReflectionUtils;

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
    public partial class DuelAnnouncer
    {
        public static DuelAnnouncer Instance { get; private set; }

        private readonly IAnnouncementService _announcer;
        private bool _isActive;

        /// <summary>Announces and logs to game log history.</summary>
        private void AnnounceToLog(string message, AnnouncementPriority priority)
        {
            _announcer.Announce(message, priority);
            _announcer.LogToHistory(message);
        }

        private readonly Dictionary<string, DuelEventType> _eventTypeMap;
        // _zoneCounts lives in DuelAnnouncer.Zones.cs

        private string _lastAnnouncement;
        private DateTime _lastAnnouncementTime;
        private const float DUPLICATE_THRESHOLD_SECONDS = 0.5f;

        private uint _localPlayerId;
        private ZoneNavigator _zoneNavigator;
        private BattlefieldNavigator _battlefieldNavigator;
        private DateTime _lastSpellResolvedTime = DateTime.MinValue;

        // Track commander GrpIds for command zone access (Brawl/Commander)
        // Maps GrpId -> isOpponent, set when cards first enter the Command zone
        private readonly Dictionary<uint, bool> _commandZoneGrpIds = new Dictionary<uint, bool>();

        // Track which event labels have been field-logged (replaces per-type boolean flags)
        private static readonly HashSet<string> _fieldLoggedLabels = new HashSet<string>();

        // Pre-compiled regex patterns for zone event parsing
        private static readonly Regex ZoneNamePattern = new Regex(@"^(\w+)\s*\(", RegexOptions.Compiled);
        private static readonly Regex ZoneCountPattern = new Regex(@"(\d+)\s*cards?\)", RegexOptions.Compiled);
        private static readonly Regex LocalPlayerPattern = new Regex(@"Player[^:]*:\s*(\d+)\s*\(LocalPlayer\)", RegexOptions.Compiled);

        public void SetZoneNavigator(ZoneNavigator navigator)
        {
            _zoneNavigator = navigator;
        }

        public void SetBattlefieldNavigator(BattlefieldNavigator navigator)
        {
            _battlefieldNavigator = navigator;
        }

        /// <summary>
        /// Marks both zone and battlefield navigators as dirty so they refresh
        /// on the next user navigation input.
        /// </summary>
        private void MarkNavigatorsDirty()
        {
            _zoneNavigator?.MarkDirty();
            _battlefieldNavigator?.MarkDirty();
        }

        public DuelAnnouncer(IAnnouncementService announcer)
        {
            _announcer = announcer;
            _eventTypeMap = BuildEventTypeMap();
            Instance = this;
        }

        public void Activate(uint localPlayerId)
        {
            // Skip reset if already active (e.g. re-activation after settings menu close).
            // State (turn count, phase, zone counts) is still valid since Deactivate()
            // is only called on scene change, not on navigator preemption.
            if (_isActive && _localPlayerId == localPlayerId)
                return;

            _isActive = true;
            _localPlayerId = localPlayerId;
            _zoneCounts.Clear();
            _commandZoneGrpIds.Clear();
            _userTurnCount = 0;

            // Seed commander GrpIds from MatchManager (works for both local and opponent).
            // MTGA never fires zone transfer events for the opponent's commander,
            // so this is the only way to discover their commander identity.
            PopulateCommandersFromMatchManager();

            // Check if this is an NPE tutorial game (NpeDirector != null)
            _npeCheckDone = false;
            _isNPETutorial = false;
            CheckNPETutorial();
        }

        public void Deactivate()
        {
            _isActive = false;
            _currentPhase = null;
            _currentStep = null;
            _isUserTurn = true;
            _pendingPhaseAnnouncement = null;
            DuelHolderCache.Clear();
            _instanceIdToName.Clear();
            _creatureDamage.Clear();
        }

        /// <summary>
        /// Called from TimerPatch when a timeout notification fires.
        /// Announces that a timeout was used and remaining timeout count.
        /// </summary>
        public void OnTimerTimeout(bool isLocal, uint timeoutCount)
        {
            if (!_isActive) return;

            string message = isLocal
                ? Strings.TimerTimeoutUsed(timeoutCount)
                : Strings.TimerOpponentTimeout(timeoutCount);
            // High priority queues after current speech (e.g. card info) instead of interrupting
            AnnounceToLog(message, AnnouncementPriority.High);
        }

        /// <summary>
        /// Yields active CDC child GameObjects from a cached holder.
        /// Reads live children each call - no stale card data.
        /// </summary>
        private IEnumerable<GameObject> EnumerateCDCsInHolder(string nameContains)
        {
            var holder = DuelHolderCache.GetHolder(nameContains);
            if (holder == null) yield break;

            foreach (Transform child in holder.GetComponentsInChildren<Transform>(true))
            {
                if (child != null && child.gameObject.activeInHierarchy && child.name.StartsWith("CDC "))
                    yield return child.gameObject;
            }
        }

        // All zone holder names for cross-zone card lookups
        private static readonly string[] AllZoneHolders = {
            "BattlefieldCardHolder", "StackCardHolder", "LocalHand",
            "LocalGraveyard", "ExileCardHolder", "CommandCardHolder",
            "OpponentGraveyard", "OpponentExile"
        };

        /// <summary>
        /// Call each frame to flush debounced phase announcements.
        /// </summary>
        public void Update()
        {
            if (_pendingPhaseAnnouncement == null) return;

            _phaseDebounceTimer -= UnityEngine.Time.deltaTime;
            if (_phaseDebounceTimer <= 0)
            {
                AnnounceToLog(_pendingPhaseAnnouncement, AnnouncementPriority.Low);
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
                    Log.Announce("DuelAnnouncer", $"NEW EVENT TYPE SEEN: {typeName}");
                }

                var eventType = ClassifyEvent(uxEvent);
                if (eventType == DuelEventType.Ignored) return;

                var announcement = BuildAnnouncement(eventType, uxEvent);
                if (string.IsNullOrEmpty(announcement)) return;

                if (IsDuplicateAnnouncement(announcement)) return;

                var priority = GetPriority(eventType);
                AnnounceToLog(announcement, priority);
                _lastAnnouncement = announcement;
                _lastAnnouncementTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                Log.Warn("DuelAnnouncer", $"Error processing event: {ex.Message}");
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
                    return null; // HotHighlightNavigator handles targeting via Tab
                case DuelEventType.TargetConfirmed:
                    return null; // HotHighlightNavigator discovers new state on next Tab
                case DuelEventType.ResolutionStarted:
                    return HandleResolutionStarted(uxEvent);
                case DuelEventType.ResolutionEnded:
                    return HandleResolutionEnded(uxEvent);
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
                case DuelEventType.NPEDialog:
                    return HandleNPEDialog(uxEvent);
                case DuelEventType.NPEReminder:
                    return HandleNPEReminder(uxEvent);
                case DuelEventType.NPETooltip:
                    return HandleNPETooltip(uxEvent);
                case DuelEventType.NPEWarning:
                    return HandleNPEWarning(uxEvent);
                default:
                    return null;
            }
        }

        #region Announcement Builders

        // Track if we've logged life event fields (only once for discovery)


        /// <summary>
        /// Logs event fields/properties once per label (for discovery/debugging).
        /// Replaces per-event-type boolean flags with a single HashSet.
        /// </summary>
        private void LogEventFieldsOnce(object uxEvent, string label)
        {
            if (uxEvent == null || !_fieldLoggedLabels.Add(label)) return;
            LogEventFields(uxEvent, label);
        }

        private void LogEventFields(object uxEvent, string label)
        {
            if (uxEvent == null) return;

            var type = uxEvent.GetType();
            Log.Announce("DuelAnnouncer", $"=== {label} TYPE: {type.FullName} ===");

            // Log all fields. Skip compiler-generated auto-property backing fields
            // (<Name>k__BackingField) — the matching Property: line below carries the same value.
            var fields = type.GetFields(AllInstanceFlags);
            foreach (var field in fields)
            {
                if (field.Name.StartsWith("<") && field.Name.EndsWith("k__BackingField")) continue;
                try
                {
                    var value = field.GetValue(uxEvent);
                    string valueStr = value?.ToString() ?? "null";
                    if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                    Log.Announce("DuelAnnouncer", $"Field: {field.Name} = {valueStr} ({field.FieldType.Name})");
                }
                catch (Exception ex)
                {
                    Log.Announce("DuelAnnouncer", $"Field: {field.Name} = [Error: {ex.Message}]");
                }
            }

            // Log all properties
            var props = type.GetProperties(AllInstanceFlags);
            foreach (var prop in props)
            {
                try
                {
                    var value = prop.GetValue(uxEvent);
                    string valueStr = value?.ToString() ?? "null";
                    if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                    Log.Announce("DuelAnnouncer", $"Property: {prop.Name} = {valueStr} ({prop.PropertyType.Name})");
                }
                catch (Exception ex)
                {
                    Log.Announce("DuelAnnouncer", $"Property: {prop.Name} = [Error: {ex.Message}]");
                }
            }

            Log.Announce("DuelAnnouncer", $"=== END {label} FIELDS ===");
        }

        // LogDamageEventFields removed - use LogEventFieldsOnce

        // FindCardNameByInstanceId removed - use GetCardNameByInstanceId (cached)

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

        // Track if we've logged various event fields (once per type for discovery)

        // Track if we've logged multistep effect fields (once for discovery)

        private T GetNestedPropertyValue<T>(object obj, string propertyName)
        {
            if (obj == null) return default(T);
            try
            {
                var member = LookupMember(obj.GetType(), propertyName);
                if (member == null) return default(T);

                object value = member is FieldInfo fi ? fi.GetValue(obj) : ((PropertyInfo)member).GetValue(obj);
                return value is T typedValue ? typedValue : default(T);
            }
            catch { /* Reflection may fail on different game versions */ }
            return default(T);
        }

        private string GetCardNameByInstanceId(uint instanceId)
        {
            if (instanceId == 0) return null;

            // Check cache first
            if (_instanceIdToName.TryGetValue(instanceId, out string cachedName))
                return cachedName;

            try
            {
                foreach (var holderName in AllZoneHolders)
                {
                    foreach (var go in EnumerateCDCsInHolder(holderName))
                    {
                        var cdcComponent = CardModelProvider.GetDuelSceneCDC(go);
                        if (cdcComponent == null) continue;

                        var model = CardModelProvider.GetCardModel(cdcComponent);
                        if (model == null) continue;

                        var cid = GetFieldValue<uint>(model, "InstanceId");
                        if (cid == instanceId)
                        {
                            var gid = GetFieldValue<uint>(model, "GrpId");
                            if (gid != 0)
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
            catch (Exception ex)
            {
                Log.Warn("DuelAnnouncer", $"Error looking up card {instanceId}: {ex.Message}");
            }

            return null;
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

                // Only process UpdateManaPoolUXEvent for announcements
                if (typeName == "UpdateManaPoolUXEvent")
                {
                    string manaPoolString = ParseManaPool(uxEvent);
                    _lastManaPool = manaPoolString ?? "";

                    if (!string.IsNullOrEmpty(manaPoolString))
                    {
                        Log.Announce("DuelAnnouncer", $"Mana pool: {manaPoolString}");
                        return Strings.ManaAmount(manaPoolString);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.Warn("DuelAnnouncer", $"Error handling mana event: {ex.Message}");
                return null;
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
                    PrivateInstance);
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
                    var colorField = mana.GetType().GetField("Color", AllInstanceFlags)
                                  ?? mana.GetType().GetField("_color", AllInstanceFlags);

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
                        string readableName = ManaTextFormatter.ConvertManaColorToName(colorName);

                        if (manaByColor.ContainsKey(readableName))
                            manaByColor[readableName]++;
                        else
                            manaByColor[readableName] = 1;
                    }
                    catch { /* Mana color reflection may fail on unexpected types */ }
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
                Log.Warn("DuelAnnouncer", $"Error parsing mana pool: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Helper Methods

        // Reflection cache: maps (Type, memberName) -> MemberInfo (FieldInfo or PropertyInfo).
        // Static because types don't change at runtime. ConcurrentDictionary for thread safety.
        // A null value means "we looked and neither field nor property exists" (negative cache).
        private static readonly ConcurrentDictionary<(Type, string), MemberInfo> _reflectionCache
            = new ConcurrentDictionary<(Type, string), MemberInfo>();

        // AllInstanceFlags provided by ReflectionUtils via using static

        private static MemberInfo LookupMember(Type type, string name)
        {
            return _reflectionCache.GetOrAdd((type, name), key =>
                key.Item1.GetField(key.Item2, AllInstanceFlags) as MemberInfo
                ?? key.Item1.GetProperty(key.Item2, AllInstanceFlags) as MemberInfo);
        }

        private T GetFieldValue<T>(object obj, string fieldName)
        {
            if (obj == null) return default;

            var member = LookupMember(obj.GetType(), fieldName);
            if (member == null) return default;

            object value = member is FieldInfo fi ? fi.GetValue(obj) : ((PropertyInfo)member).GetValue(obj);
            return value is T typed ? typed : default;
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
            var match = LocalPlayerPattern.Match(zoneStr);
            if (match.Success && uint.TryParse(match.Groups[1].Value, out uint detectedId))
            {
                if (detectedId != _localPlayerId && detectedId > 0)
                {
                    Log.Announce("DuelAnnouncer", $"Updating local player ID: {_localPlayerId} -> {detectedId} (detected from zone string)");
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
                case DuelEventType.NPEDialog:
                case DuelEventType.NPEReminder:
                case DuelEventType.NPETooltip:
                case DuelEventType.NPEWarning:
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
                { "NPEDialogUXEvent", DuelEventType.NPEDialog },
                { "NPEReminderUXEvent", DuelEventType.NPEReminder },
                { "NPEDismissableDeluxeTooltipUXEvent", DuelEventType.NPETooltip },
                { "NPEPauseUXEvent", DuelEventType.Ignored },
                { "NPEShowBattlefieldHangerUXEvent", DuelEventType.Ignored },
                { "NPEWarningUXEvent", DuelEventType.NPEWarning },
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
        ManaPool,
        NPEDialog,
        NPEReminder,
        NPETooltip,
        NPEWarning
    }
}
