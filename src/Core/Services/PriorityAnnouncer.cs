using System;
using System.Reflection;
using UnityEngine;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Utils;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Announces when the local player gains priority during a duel, but ONLY in the
    /// "silent" cases where no other announcement already told the user they can act.
    ///
    /// The game's own signal: MTGA wraps each free-priority window in an
    /// <c>ActionsAvailableWorkflow</c> (see llm-docs/decompiled/ActionsAvailableWorkflow.cs).
    /// Its <c>ApplyInteractionInternal()</c> calls <c>_logger.PriorityReceived()</c> and plays
    /// <c>sfx_ui_gain_priority</c>; <c>CleanUp()</c> plays <c>sfx_ui_lose_priority</c>. That
    /// workflow is ONLY the "you may act or pass" window — forced decisions (choose targets,
    /// declare attackers/blockers, discard, …) are different workflows that already come with
    /// their own prompts. So "<c>GameManager.CurrentInteraction</c> is an
    /// <c>ActionsAvailableWorkflow</c> in the Applied state" is exactly "the local player holds
    /// priority and may respond or pass" — no guessing.
    ///
    /// We poll that workflow each duel frame and announce on the rising edge, but ONLY when there
    /// is something on the stack to react to (your just-cast spell, or the opponent's spell/
    /// ability) — an empty stack means trivial priority (after a land, start of your phase, or
    /// passed to you with nothing pending), which we skip. Further suppression:
    ///  - Not when a phase/turn change was just announced (it already implies you can act).
    ///  - Not when a prompt was just announced (it already told you to act).
    ///  - Debounced so rapid lose/gain cycles can't double-speak.
    /// Fires on both your turn and the opponent's turn.
    ///
    /// This is the gap the user described: the opponent does something, you gain priority to
    /// respond with an instant/ability, but nothing announced it.
    /// </summary>
    public class PriorityAnnouncer
    {
        private readonly IAnnouncementService _announcer;
        private readonly ZoneNavigator _zoneNavigator;

        // Within this window after a phase/turn change or a prompt announcement, treat priority
        // as "already announced" and stay silent (avoids echoing "First main" with "you have
        // priority", or double-speaking a prompt).
        private const float SuppressWindowSeconds = 2.0f;

        // Don't speak priority twice within this gap, even if the workflow flickers off/on.
        private const float MinReannounceGapSeconds = 3.0f;

        // Reflection handles (resolved lazily; types don't change between scenes).
        private bool _reflectionTried;
        private PropertyInfo _currentInteractionProp;   // GameManager.CurrentInteraction
        private MonoBehaviour _cachedGameManager;

        private bool _hadPriority;
        private float _lastAnnounceTime = -100f;

        public PriorityAnnouncer(IAnnouncementService announcer, ZoneNavigator zoneNavigator)
        {
            _announcer = announcer;
            _zoneNavigator = zoneNavigator;
        }

        /// <summary>
        /// Called once per duel frame. <paramref name="timeSinceLastPhaseChange"/> and
        /// <paramref name="timeSinceLastPrompt"/> come from DuelAnnouncer / HotHighlightNavigator
        /// so suppression stays in sync with what was actually spoken. <paramref name="isUserTurn"/>
        /// is used for logging only.
        /// </summary>
        public void Update(bool isUserTurn, float timeSinceLastPhaseChange, float timeSinceLastPrompt)
        {
            bool nowPriority = HasLocalPriority();

            // Rising edge only.
            if (nowPriority && !_hadPriority)
                OnPriorityGained(isUserTurn, timeSinceLastPhaseChange, timeSinceLastPrompt);

            _hadPriority = nowPriority;
        }

        private void OnPriorityGained(bool isUserTurn, float timeSinceLastPhaseChange, float timeSinceLastPrompt)
        {
            if (AccessibleArenaMod.Instance?.Settings?.PriorityAnnouncements == false)
            {
                Log.Msg("PriorityAnnouncer", "priority gained but setting is off");
                return;
            }

            // Only announce when there is something on the stack to react to — your own spell you
            // just cast (so you can protect/chain), or the opponent's spell/ability. An empty
            // stack means trivial priority (after a land, at the start of your phase, or passed to
            // you with nothing pending), which we stay silent on. GetFreshStackCount() scans the
            // live stack holder (the team's timing-safe count) rather than a cached/stale value.
            int stackCount = _zoneNavigator?.GetFreshStackCount() ?? 0;

            // Then debounce double-speak: when something else just told the user they can act, or
            // we just announced.
            string suppressReason = null;
            if (stackCount <= 0)
                suppressReason = "empty stack";
            else if (timeSinceLastPhaseChange < SuppressWindowSeconds)
                suppressReason = $"phase/turn change {timeSinceLastPhaseChange:0.0}s ago";
            else if (timeSinceLastPrompt < SuppressWindowSeconds)
                suppressReason = $"prompt {timeSinceLastPrompt:0.0}s ago";
            else if (Time.time - _lastAnnounceTime < MinReannounceGapSeconds)
                suppressReason = $"debounce ({Time.time - _lastAnnounceTime:0.0}s)";

            if (suppressReason != null)
            {
                Log.Msg("PriorityAnnouncer", $"priority gained (userTurn={isUserTurn}, stack={stackCount}) suppressed: {suppressReason}");
                return;
            }

            _lastAnnounceTime = Time.time;
            Log.Announce("PriorityAnnouncer", $"priority gained (userTurn={isUserTurn}, stack={stackCount}) -> announcing");
            _announcer.Announce(Strings.Duel_PriorityReceived, AnnouncementPriority.High);
        }

        /// <summary>
        /// True when GameManager.CurrentInteraction is an ActionsAvailableWorkflow. Note
        /// CurrentInteraction maps to MutableWorkflowProvider.GetCurrentWorkflow(), which returns
        /// non-null ONLY when the workflow is in the Applied state — so reaching here already
        /// means "applied" and no separate AppliedState check is needed.
        /// </summary>
        private bool HasLocalPriority()
        {
            if (!EnsureReflection()) return false;

            if (_cachedGameManager == null || !_cachedGameManager)
            {
                _cachedGameManager = FindGameManager();
                if (_cachedGameManager == null) return false;
            }

            object current;
            try { current = _currentInteractionProp.GetValue(_cachedGameManager); }
            catch (Exception ex)
            {
                Log.Msg("PriorityAnnouncer", $"CurrentInteraction read failed: {ex.Message}");
                return false;
            }

            // Runtime type-name check avoids needing a direct reference to ActionsAvailableWorkflow,
            // whose assembly throws ReflectionTypeLoadException on GetTypes() (see
            // HotHighlightNavigator.InitInteractionLookup).
            return current != null && current.GetType().Name == "ActionsAvailableWorkflow";
        }

        private bool EnsureReflection()
        {
            if (_reflectionTried) return _currentInteractionProp != null;
            _reflectionTried = true;

            var gmType = FindType("GameManager");
            if (gmType != null)
                _currentInteractionProp = gmType.GetProperty("CurrentInteraction", PublicInstance);

            Log.Msg("PriorityAnnouncer",
                _currentInteractionProp != null
                    ? "Priority detection ready (GameManager.CurrentInteraction)"
                    : "GameManager.CurrentInteraction not resolvable — priority announcements disabled");
            return _currentInteractionProp != null;
        }

        private static MonoBehaviour FindGameManager()
        {
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "GameManager")
                    return mb;
            }
            return null;
        }

        /// <summary>Clear per-duel state. Call on scene change / navigator deactivation.</summary>
        public void Reset()
        {
            _hadPriority = false;
            _lastAnnounceTime = -100f;
            _cachedGameManager = null;
        }
    }
}
