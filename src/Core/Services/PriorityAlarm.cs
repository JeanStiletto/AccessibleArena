using UnityEngine;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Plays a repeating audio cue while the local player holds "free" priority and hasn't
    /// reacted yet — the audible companion to <see cref="PriorityAnnouncer"/>, for players who
    /// miss the one-time spoken announcement.
    ///
    /// It reuses the announcer's priority detection (GameManager.CurrentInteraction is an
    /// <c>ActionsAvailableWorkflow</c>) and the same "something on the stack" gate, so it only
    /// arms on meaningful priority — never on trivial land-drop / empty-stack windows, and never
    /// during modal workflows (browsers, mana picking, forced choices), where CurrentInteraction
    /// is a different workflow and detection returns false.
    ///
    /// The sound is a synthesized tone played through the Windows default device via
    /// <see cref="ToneCue"/> — independent of the game's Wwise mixer and its SFX volume slider,
    /// and nothing the Arena client can observe (local audio output only, no input injected, no
    /// game state touched). We originally reused the game's own <c>sfx_ui_gain_priority</c> cue,
    /// but that Wwise event is silent in the shipped build (posts without error, produces no
    /// audible output — which is why the native priority sound was never heard), so we route
    /// around it entirely.
    ///
    /// Lifecycle of one priority window:
    ///  - Rising edge of meaningful priority -> arm, start a grace countdown.
    ///  - Any key/mouse press (user is engaged) -> stop. This is the clean "understood" signal;
    ///    we only observe the key, never consume it, so normal navigation still works.
    ///  - Priority lost (passed / phase advanced / opponent acted) -> stop.
    ///  - Grace elapses with priority still held and no input -> begin the repeating cue.
    ///  - Hard cap reached (walked away) -> stop, so it can never run forever.
    /// Off by default; gated behind <c>ModSettings.PriorityAlarm</c>.
    /// </summary>
    public class PriorityAlarm
    {
        // Reuses this for the "do I hold local priority" check (single source of truth, no
        // duplicate GameManager reflection).
        private readonly PriorityAnnouncer _priorityDetector;
        private readonly ZoneNavigator _zoneNavigator;

        // Delay before the alarm starts sounding, so quick reactors never hear it.
        private const float GraceSeconds = 4f;
        // Gap between repeats once sounding.
        private const float RepeatIntervalSeconds = 2f;
        // Absolute cap on how long the alarm can run for a single priority window.
        private const float HardCapSeconds = 20f;

        private bool _hadPriority;
        private bool _armed;
        private bool _sounding;
        private float _graceRemaining;
        private float _repeatRemaining;
        private float _armedElapsed;

        public PriorityAlarm(PriorityAnnouncer priorityDetector, ZoneNavigator zoneNavigator)
        {
            _priorityDetector = priorityDetector;
            _zoneNavigator = zoneNavigator;
        }

        /// <summary>Called once per duel frame.</summary>
        public void Update()
        {
            bool enabled = AccessibleArenaMod.Instance?.Settings?.PriorityAlarm == true;
            if (!enabled)
            {
                if (_armed) Disarm("setting off");
                _hadPriority = false;
                return;
            }

            bool nowPriority = _priorityDetector != null && _priorityDetector.HasLocalPriority();

            if (nowPriority && !_hadPriority)
                OnPriorityGained();
            else if (!nowPriority && _hadPriority && _armed)
                Disarm("priority lost");

            _hadPriority = nowPriority;

            if (!_armed) return;

            // First interaction of any kind proves the user is engaged -> stop. We only observe
            // the key here; it still flows through to whatever navigator handles it this frame.
            if (Input.anyKeyDown)
            {
                Disarm("user acted");
                return;
            }

            _armedElapsed += Time.deltaTime;
            if (_armedElapsed >= HardCapSeconds)
            {
                Disarm("hard cap");
                return;
            }

            if (!_sounding)
            {
                _graceRemaining -= Time.deltaTime;
                if (_graceRemaining <= 0f)
                {
                    _sounding = true;
                    PlayCue();
                    _repeatRemaining = RepeatIntervalSeconds;
                }
            }
            else
            {
                _repeatRemaining -= Time.deltaTime;
                if (_repeatRemaining <= 0f)
                {
                    PlayCue();
                    _repeatRemaining = RepeatIntervalSeconds;
                }
            }
        }

        private void OnPriorityGained()
        {
            // Same gate as PriorityAnnouncer: only meaningful priority (something on the stack to
            // react to). An empty stack means trivial priority we stay silent on.
            int stackCount = _zoneNavigator?.GetFreshStackCount() ?? 0;
            if (stackCount <= 0)
            {
                Log.Msg("PriorityAlarm", "priority gained but stack empty -> not arming");
                return;
            }

            _armed = true;
            _sounding = false;
            _graceRemaining = GraceSeconds;
            _repeatRemaining = 0f;
            _armedElapsed = 0f;
            Log.Msg("PriorityAlarm", $"armed (stack={stackCount}), grace {GraceSeconds:0.0}s");
        }

        private void Disarm(string reason)
        {
            if (_armed)
                Log.Msg("PriorityAlarm", $"disarmed: {reason}");
            _armed = false;
            _sounding = false;
        }

        private void PlayCue()
        {
            bool ok = ToneCue.Play();
            Log.Msg("PriorityAlarm", $"cue played (ok={ok})");
        }

        /// <summary>Clear per-duel state. Call on scene change / navigator deactivation.</summary>
        public void Reset()
        {
            _hadPriority = false;
            _armed = false;
            _sounding = false;
        }
    }
}
