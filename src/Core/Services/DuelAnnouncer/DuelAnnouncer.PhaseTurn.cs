using MelonLoader;
using AccessibleArena.Core.Utils;
using AccessibleArena.Core.Models;
using System;
using System.Collections.Generic;
using UnityEngine;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    public partial class DuelAnnouncer
    {
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

        // Track time of last phase change for external consumers
        private float _lastPhaseChangeTime;
        public float TimeSinceLastPhaseChange => UnityEngine.Time.time - _lastPhaseChangeTime;

        /// <summary>
        /// Returns the current phase string ("Main1", "Main2", "Combat", etc.).
        /// </summary>
        public string CurrentPhase => _currentPhase;

        /// <summary>
        /// Returns true if it is currently the local player's turn.
        /// </summary>
        public bool IsUserTurn => _isUserTurn;

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

        private string BuildTurnChangeAnnouncement(object uxEvent)
        {
            try
            {
                var type = uxEvent.GetType();

                var activePlayerField = type.GetField("_activePlayer", PrivateInstance);
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

        private string BuildLifeChangeAnnouncement(object uxEvent)
        {
            try
            {
                // Log all fields/properties for discovery (once per session)
                LogEventFieldsOnce(uxEvent, "LIFE EVENT");

                // Field names from discovery: AffectedId, Change (property)
                var affectedId = GetFieldValue<uint>(uxEvent, "AffectedId");
                var change = GetNestedPropertyValue<int>(uxEvent, "Change");

                Log.Announce("DuelAnnouncer", $"Life event: affectedId={affectedId}, change={change}, localPlayer={_localPlayerId}");

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
                    Log.Announce("DuelAnnouncer", $"Life event avatar: {avatarStr}");

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
                Log.Warn("DuelAnnouncer", "Life announcement error", ex);
                return null;
            }
        }

        private string BuildPhaseChangeAnnouncement(object uxEvent)
        {
            try
            {
                var type = uxEvent.GetType();

                var phaseField = type.GetField("<Phase>k__BackingField", PrivateInstance);
                string phase = phaseField?.GetValue(uxEvent)?.ToString();

                var stepField = type.GetField("<Step>k__BackingField", PrivateInstance);
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
                        Log.Announce("DuelAnnouncer", $"Leaving declare attackers: {attackerAnnouncement}");
                    }
                }

                // Track current phase/step for combat navigation
                _currentPhase = phase;
                _currentStep = step;
                _lastPhaseChangeTime = UnityEngine.Time.time;
                if (_isNPETutorial && step == "DeclareBlock")
                    _shownBlockingReminderThisStep = false;
                Log.Announce("DuelAnnouncer", $"Phase change: {phase}/{step}");

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
                else if (phase == "Ending" && step == "None") phaseAnnouncement = Strings.Duel_Phase_EndStep;

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
    }
}
