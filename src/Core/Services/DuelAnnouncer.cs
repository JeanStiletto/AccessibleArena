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
        private TargetNavigator _targetNavigator;
        private DateTime _lastSpellResolvedTime = DateTime.MinValue;

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

        public void SetTargetNavigator(TargetNavigator navigator)
        {
            _targetNavigator = navigator;
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
        }

        public void Deactivate()
        {
            _isActive = false;
            _currentPhase = null;
            _currentStep = null;
        }

        /// <summary>
        /// Called by the Harmony patch when a game event is enqueued.
        /// </summary>
        public void OnGameEvent(object uxEvent)
        {
            if (!_isActive || uxEvent == null) return;

            try
            {
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

                var turnNumberField = type.GetField("_turnNumber", BindingFlags.NonPublic | BindingFlags.Instance);
                int turnNumber = 0;
                if (turnNumberField != null)
                {
                    var turnValue = turnNumberField.GetValue(uxEvent);
                    if (turnValue != null)
                        turnNumber = Convert.ToInt32(turnValue);
                }

                var activePlayerField = type.GetField("_activePlayer", BindingFlags.NonPublic | BindingFlags.Instance);
                bool isYourTurn = false;
                if (activePlayerField != null)
                {
                    var playerObj = activePlayerField.GetValue(uxEvent);
                    if (playerObj != null)
                        isYourTurn = playerObj.ToString().Contains("LocalPlayer");
                }

                string turnText = isYourTurn ? "Your turn" : "Opponent's turn";
                return turnNumber > 0 ? $"Turn {turnNumber}. {turnText}" : turnText;
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
                        return isOpponent
                            ? $"Opponent lost {removed} permanent{(removed > 1 ? "s" : "")}"
                            : $"{removed} of your permanent{(removed > 1 ? "s" : "")} left battlefield";
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

        private string BuildLifeChangeAnnouncement(object uxEvent)
        {
            try
            {
                var playerId = GetFieldValue<uint>(uxEvent, "PlayerId");
                var newLife = GetFieldValue<int>(uxEvent, "NewLifeTotal");
                var change = GetFieldValue<int>(uxEvent, "LifeChange");

                if (change == 0) return null;

                bool isLocal = playerId == _localPlayerId;
                string who = isLocal ? "You" : "Opponent";
                string direction = change > 0 ? "gained" : "lost";
                return $"{who} {direction} {Math.Abs(change)} life. Now at {newLife}";
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
                var damage = GetFieldValue<int>(uxEvent, "DamageAmount");
                if (damage <= 0) return null;

                var targetId = GetFieldValue<uint>(uxEvent, "TargetId");
                string targetName = targetId == _localPlayerId ? "you" : "opponent";

                return $"{damage} damage to {targetName}";
            }
            catch
            {
                return null;
            }
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

                // Track current phase/step for combat navigation
                _currentPhase = phase;
                _currentStep = step;

                if (phase == "Main1") return "First main phase";
                if (phase == "Main2") return "Second main phase";

                if (phase == "Combat")
                {
                    if (step == "DeclareAttack") return "Declare attackers";
                    if (step == "DeclareBlock") return "Declare blockers";
                    if (step == "CombatDamage") return "Combat damage";
                    if (step == "EndCombat") return "End of combat";
                    if (step == "None") return "Combat phase";
                }

                if (phase == "Ending" && step == "End") return "End step";

                return null;
            }
            catch
            {
                return null;
            }
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

                if (typeName == "AttackLobUXEvent") return "Attacker declared";
                if (typeName == "AttackDecrementUXEvent") return "Attacker removed";

                return null;
            }
            catch
            {
                return null;
            }
        }

        private string HandleTargetSelectionEvent(object uxEvent)
        {
            if (_targetNavigator != null)
            {
                _targetNavigator.EnterTargetMode();
                return null;
            }
            return "Select a target";
        }

        private string HandleTargetConfirmedEvent(object uxEvent)
        {
            _targetNavigator?.ExitTargetMode();
            return null;
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
                    _announcer.Announce("Spell cast", AnnouncementPriority.High);
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
                { "ZoneTransferGroup", DuelEventType.ZoneTransfer },
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
                { "CombatFrame", DuelEventType.Combat },
                { "AttackLobUXEvent", DuelEventType.Combat },
                { "AttackDecrementUXEvent", DuelEventType.Combat },

                // Target selection
                { "PlayerSelectingTargetsEventTranslator", DuelEventType.TargetSelection },
                { "PlayerSubmittedTargetsEventTranslator", DuelEventType.TargetConfirmed },

                // Ignored events
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
        TargetConfirmed
    }
}
