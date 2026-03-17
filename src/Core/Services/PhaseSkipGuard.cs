using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Guards against accidental phase-skip when player has untapped lands in main phase.
    ///
    /// Intercepts at TWO levels:
    /// 1. SendSubmitEventToSelectedObject prefix — blocks Unity EventSystem's Submit dispatch.
    ///    This is the method that actually clicks the pass button. MTGA's input module may
    ///    NOT use Input.GetButtonDown, so patching Input methods alone is insufficient.
    /// 2. Input.GetKeyDown(Space) postfix — blocks KeyboardManager and direct callers.
    ///
    /// Uses release-tracking to prevent oscillation: after showing warning, blocks all
    /// frames until Space is released. Next press after release confirms pass.
    /// </summary>
    public static class PhaseSkipGuard
    {
        private static bool _warningShown;
        private static string _warningPhase;
        private static bool _waitingForRelease;
        private static bool _confirmed;       // Pass was confirmed — suppress until phase changes
        private static string _confirmedPhase;
        private static PriorityController _priorityController;

        // Frame-cached decision so multiple calls per frame are consistent
        private static int _lastDecisionFrame = -1;
        private static bool _blockThisFrame;

        public static void SetPriorityController(PriorityController pc) => _priorityController = pc;

        /// <summary>
        /// Called from SendSubmitEventToSelectedObject prefix and GetKeyDown postfix.
        /// Returns true if Space should be blocked (warning shown or key still held).
        /// Returns false if Space should pass through (not applicable, or confirming press).
        /// </summary>
        public static bool ShouldBlock()
        {
            // Cache decision per frame — called from multiple hooks
            int frame = Time.frameCount;
            if (frame == _lastDecisionFrame) return _blockThisFrame;
            _lastDecisionFrame = frame;
            _blockThisFrame = false;

            // Track key release: once Space is released after warning, allow next press.
            // Block on the release frame too — the confirm must be a NEW key-down.
            if (_waitingForRelease)
            {
                if (!Input.GetKey(KeyCode.Space))
                {
                    _waitingForRelease = false;
                    MelonLogger.Msg("[PhaseSkipGuard] Space released — next press will confirm");
                }
                // Block whether still held OR just released (require new press to confirm)
                _blockThisFrame = true;
                return true;
            }

            var duelAnnouncer = DuelAnnouncer.Instance;
            if (duelAnnouncer == null) return false;

            string phase = duelAnnouncer.CurrentPhase;

            // After user confirmed pass, suppress until phase actually changes.
            // The server takes ~200ms to process the pass — lands are still untapped
            // during that window, which would re-trigger the warning.
            if (_confirmed)
            {
                if (phase != _confirmedPhase)
                    _confirmed = false; // Phase changed, allow future warnings
                else
                    return false; // Same phase, don't re-warn
            }

            // Auto-clear warning if phase changed since it was shown
            if (_warningShown && phase != _warningPhase)
            {
                _warningShown = false;
                _waitingForRelease = false;
            }

            if (phase != "Main1" && phase != "Main2") return false;
            if (!duelAnnouncer.IsUserTurn) return false;

            if (_warningShown)
            {
                // Second press after release — confirm pass, suppress re-evaluation
                _warningShown = false;
                _warningPhase = null;
                _confirmed = true;
                _confirmedPhase = phase;
                MelonLogger.Msg("[PhaseSkipGuard] Confirmed — allowing pass");
                return false;
            }

            if (!HasUntappedPlayerLands()) return false;

            // Don't warn when full control is already active
            if (_priorityController != null &&
                (_priorityController.IsFullControlEnabled() || _priorityController.IsFullControlLocked()))
                return false;

            // First press with untapped lands — warn and block until released
            _warningShown = true;
            _warningPhase = phase;
            _waitingForRelease = true;
            _blockThisFrame = true;

            var announcer = AccessibleArenaMod.Instance?.Announcer;
            announcer?.Announce("Mana available. Press Space again to pass.", AnnouncementPriority.High);
            MelonLogger.Msg("[PhaseSkipGuard] Warning shown — blocking until Space released and pressed again");
            return true;
        }

        /// <summary>
        /// Reset state on duel end or deactivation.
        /// </summary>
        public static void Reset()
        {
            _warningShown = false;
            _warningPhase = null;
            _waitingForRelease = false;
            _confirmed = false;
            _confirmedPhase = null;
            _blockThisFrame = false;
            _lastDecisionFrame = -1;
        }

        private static bool HasUntappedPlayerLands()
        {
            var battlefieldHolder = DuelHolderCache.GetHolder("BattlefieldCardHolder");
            if (battlefieldHolder == null) return false;

            foreach (Transform child in battlefieldHolder.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                var go = child.gameObject;
                if (!CardDetector.IsCard(go)) continue;
                var (_, isLand, isOpponent) = CardDetector.GetCardCategory(go);
                if (!isLand || isOpponent) continue;
                if (!CardStateProvider.GetIsTappedFromCard(go)) return true;
            }
            return false;
        }
    }
}
