using UnityEngine;
using UnityEngine.Events;
using AccessibleArena.Core.Utils;
using AccessibleArena.Core.Models;
using System;
using System.Reflection;
using TMPro;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Match clock + rope timer handling, LowTimeWarning subscription, timeout counts,
    /// and MtgTimer/LowTimeWarning reflection caching.
    /// </summary>
    public partial class PlayerPortraitNavigator
    {
        // Cached references to timer elements
        private GameObject _localTimerObj;
        private GameObject _opponentTimerObj;
        private MonoBehaviour _localMatchTimer;
        private MonoBehaviour _opponentMatchTimer;

        // LowTimeWarning (rope) subscription
        private MonoBehaviour _localLowTimeWarning;
        private MonoBehaviour _opponentLowTimeWarning;
        private UnityAction<bool> _localRopeCallback;
        private UnityAction<bool> _opponentRopeCallback;

        private sealed class LtwHandles
        {
            public FieldInfo ActiveTimer;   // LowTimeWarning._activeTimer (MtgTimer)
            public FieldInfo TimeRunning;   // LowTimeWarning._timeRunning (float)
            public FieldInfo TimeoutPips;   // LowTimeWarning._timeoutPips (List<TimeoutPip>), optional
        }

        private sealed class MatchTimerHandles
        {
            public FieldInfo MatchTimer;    // MatchTimer._matchTimer (MtgTimer)
            public FieldInfo TimeRunning;   // MatchTimer._timeRunning (float)
        }

        private sealed class MtgTimerHandles
        {
            public PropertyInfo RemainingTime;
            public FieldInfo Running;
        }

        private static readonly ReflectionCache<LtwHandles> _ltwCache = new ReflectionCache<LtwHandles>(
            builder: t => new LtwHandles
            {
                ActiveTimer = t.GetField("_activeTimer", PrivateInstance),
                TimeRunning = t.GetField("_timeRunning", PrivateInstance),
                TimeoutPips = t.GetField("_timeoutPips", PrivateInstance),
            },
            validator: h => h.ActiveTimer != null && h.TimeRunning != null,
            logTag: "PlayerPortrait",
            logSubject: "LowTimeWarning");

        private static readonly ReflectionCache<MatchTimerHandles> _matchTimerCache = new ReflectionCache<MatchTimerHandles>(
            builder: t => new MatchTimerHandles
            {
                MatchTimer = t.GetField("_matchTimer", PrivateInstance),
                TimeRunning = t.GetField("_timeRunning", PrivateInstance),
            },
            validator: h => h.MatchTimer != null && h.TimeRunning != null,
            logTag: "PlayerPortrait",
            logSubject: "MatchTimer");

        private static readonly ReflectionCache<MtgTimerHandles> _mtgTimerCache = new ReflectionCache<MtgTimerHandles>(
            builder: t => new MtgTimerHandles
            {
                RemainingTime = t.GetProperty("RemainingTime", PublicInstance),
                Running = t.GetField("Running", PublicInstance),
            },
            validator: h => h.RemainingTime != null && h.Running != null,
            logTag: "PlayerPortrait",
            logSubject: "MtgTimer");

        private bool _loggedTimerPlayer;
        private bool _loggedTimerOpponent;

        private void DiscoverTimerElements()
        {
            // Find MatchTimer components. Only log on transitions from null → found,
            // since DiscoverTimerElements runs on every V-press and the refs don't
            // change after first discovery.
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy)
                    continue;

                string typeName = mb.GetType().Name;
                if (typeName == T.MatchTimer)
                {
                    string objName = mb.gameObject.name;
                    if (objName.Contains("LocalPlayer"))
                    {
                        bool wasMissing = _localTimerObj == null;
                        _localTimerObj = mb.gameObject;
                        _localMatchTimer = mb;
                        if (wasMissing) Log.Nav("PlayerPortrait", $"Found local timer: {objName}");
                    }
                    else if (objName.Contains("Opponent"))
                    {
                        bool wasMissing = _opponentTimerObj == null;
                        _opponentTimerObj = mb.gameObject;
                        _opponentMatchTimer = mb;
                        if (wasMissing) Log.Nav("PlayerPortrait", $"Found opponent timer: {objName}");
                    }
                }
            }

            // Also find the Timer_Player and Timer_Opponent for timeout pips
            var timerPlayer = GameObject.Find("Timer_Player");
            var timerOpponent = GameObject.Find("Timer_Opponent");

            if (timerPlayer != null && !_loggedTimerPlayer)
            {
                _loggedTimerPlayer = true;
                Log.Nav("PlayerPortrait", $"Found Timer_Player for timeouts");
            }
            if (timerOpponent != null && !_loggedTimerOpponent)
            {
                _loggedTimerOpponent = true;
                Log.Nav("PlayerPortrait", $"Found Timer_Opponent for timeouts");
            }
        }

        private int GetTimeoutCount(string playerType)
        {
            // Find the TimeoutDisplay for this player
            var displayName = playerType == "LocalPlayer"
                ? "LocalPlayerTimeoutDisplay_Desktop_16x9(Clone)"
                : "OpponentTimeoutDisplay_Desktop_16x9(Clone)";

            var displayObj = GameObject.Find(displayName);
            if (displayObj == null) return -1;

            // Find the Text child with timeout count (shows "x0", "x1", etc.)
            var tmpComponents = displayObj.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var tmp in tmpComponents)
            {
                var text = tmp.text?.Trim() ?? "";
                if (text.StartsWith("x") && int.TryParse(text.Substring(1), out int count))
                {
                    return count;
                }
            }

            return -1;
        }

        /// <summary>
        /// Public method for E/Shift+E shortcut. Reads match clock first,
        /// falls back to rope (turn) timer if no match clock exists.
        /// </summary>
        public void AnnounceTimer(bool opponent)
        {
            if (!_isActive) return;

            DiscoverTimerElements();

            // Try match clock first (Bo3, timed events)
            string matchClockText = GetTimerFromModel(opponent);
            if (matchClockText != null)
            {
                int timeouts = GetTimeoutCount(opponent ? "Opponent" : "LocalPlayer");
                if (timeouts < 0) timeouts = 0;

                string message = opponent
                    ? Strings.TimerOpponentAnnounce(matchClockText, timeouts)
                    : Strings.TimerAnnounce(matchClockText, timeouts);
                _announcer.AnnounceInterrupt(message);
                return;
            }

            // Fall back to rope timer (turn timer from LowTimeWarning)
            var ropeResult = GetRopeTimerFromModel(opponent);
            if (ropeResult != null)
            {
                string message = opponent
                    ? Strings.TimerOpponentRopeAnnounce(ropeResult.Value.timerText, ropeResult.Value.timeouts)
                    : Strings.TimerRopeAnnounce(ropeResult.Value.timerText, ropeResult.Value.timeouts);
                _announcer.AnnounceInterrupt(message);
                return;
            }

            // No timer info at all
            _announcer.AnnounceInterrupt(Strings.TimerNoMatchClock);
        }

        /// <summary>
        /// Reads remaining time from MtgTimer model via reflection.
        /// Returns formatted time string or null if unavailable.
        /// </summary>
        private string GetTimerFromModel(bool isOpponent)
        {
            var matchTimer = isOpponent ? _opponentMatchTimer : _localMatchTimer;
            if (matchTimer == null) return null;

            if (!_matchTimerCache.EnsureInitialized(matchTimer.GetType())) return null;
            var mt = _matchTimerCache.Handles;
            if (!_mtgTimerCache.EnsureInitialized(mt.MatchTimer.FieldType)) return null;
            var mtg = _mtgTimerCache.Handles;

            try
            {
                // Read private _matchTimer field (MtgTimer) from MatchTimer component
                var mtgTimer = mt.MatchTimer.GetValue(matchTimer);
                if (mtgTimer == null) return null;

                bool running = (bool)mtg.Running.GetValue(mtgTimer);
                if (!running) return null;

                float remainingTime = (float)mtg.RemainingTime.GetValue(mtgTimer);
                float timeRunning = (float)mt.TimeRunning.GetValue(matchTimer);

                // Same formula as MatchTimer.LateUpdate: actual = RemainingTime - _timeRunning
                float actualRemaining = remainingTime - timeRunning;
                if (actualRemaining < 0f) actualRemaining = 0f;

                return FormatSecondsToReadable(actualRemaining);
            }
            catch (Exception ex)
            {
                Log.Nav("PlayerPortrait", $"Error reading MtgTimer: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads rope (turn) timer from LowTimeWarning._activeTimer.
        /// Returns formatted time + timeout count, or null if rope is not active.
        /// </summary>
        private (string timerText, int timeouts)? GetRopeTimerFromModel(bool isOpponent)
        {
            var ltw = isOpponent ? _opponentLowTimeWarning : _localLowTimeWarning;
            if (ltw == null) return null;

            if (!_ltwCache.EnsureInitialized(ltw.GetType())) return null;
            var lh = _ltwCache.Handles;

            // Ensure MtgTimer reflection is ready (for RemainingTime/Running).
            // Try from MatchTimer first; fall back to LowTimeWarning._activeTimer field type
            // (needed for casual Brawl games where no MatchTimer component exists).
            if (!_mtgTimerCache.IsInitialized)
            {
                var matchTimer = isOpponent ? _opponentMatchTimer : _localMatchTimer;
                if (matchTimer != null && _matchTimerCache.EnsureInitialized(matchTimer.GetType()))
                    _mtgTimerCache.EnsureInitialized(_matchTimerCache.Handles.MatchTimer.FieldType);
            }
            if (!_mtgTimerCache.IsInitialized)
                _mtgTimerCache.EnsureInitialized(lh.ActiveTimer.FieldType);
            if (!_mtgTimerCache.IsInitialized) return null;
            var mtg = _mtgTimerCache.Handles;

            try
            {
                var activeTimer = lh.ActiveTimer.GetValue(ltw);
                if (activeTimer == null) return null;

                bool running = (bool)mtg.Running.GetValue(activeTimer);
                if (!running) return null;

                float remainingTime = (float)mtg.RemainingTime.GetValue(activeTimer);
                float timeRunning = (float)lh.TimeRunning.GetValue(ltw);

                float actualRemaining = remainingTime - timeRunning;
                if (actualRemaining < 0f) actualRemaining = 0f;

                // Get timeout count from pip list (optional handle)
                int timeouts = 0;
                if (lh.TimeoutPips != null)
                {
                    var pipList = lh.TimeoutPips.GetValue(ltw) as System.Collections.IList;
                    if (pipList != null) timeouts = pipList.Count;
                }

                return (FormatSecondsToReadable(actualRemaining), timeouts);
            }
            catch (Exception ex)
            {
                Log.Nav("PlayerPortrait", $"Error reading rope timer: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Formats seconds into "X minutes Y seconds" for screen reader.
        /// </summary>
        private static string FormatSecondsToReadable(float totalSeconds)
        {
            int total = (int)totalSeconds;
            int minutes = total / 60;
            int seconds = total % 60;

            if (minutes == 0 && seconds == 0)
                return "no time";
            if (minutes == 0)
                return $"{seconds} seconds";
            if (seconds == 0)
                return $"{minutes} minutes";
            return $"{minutes} minutes {seconds} seconds";
        }

        /// <summary>
        /// Discovers LowTimeWarning MonoBehaviours and subscribes to their OnVisibilityChanged events.
        /// Local vs opponent is determined by parent hierarchy containing "LocalPlayer" or "Opponent".
        /// </summary>
        private void SubscribeLowTimeWarnings()
        {
            UnsubscribeLowTimeWarnings();

            try
            {
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                    if (mb.GetType().Name != T.LowTimeWarning) continue;

                    // Determine local vs opponent by checking parent names
                    bool isLocal = false;
                    Transform current = mb.transform;
                    while (current != null)
                    {
                        if (current.name.Contains("LocalPlayer")) { isLocal = true; break; }
                        if (current.name.Contains("Opponent")) { isLocal = false; break; }
                        current = current.parent;
                    }

                    // Get the OnVisibilityChanged field (public LowTimeVisibilityChangedEvent)
                    var onVisField = mb.GetType().GetField("OnVisibilityChanged", PublicInstance);
                    if (onVisField == null)
                    {
                        Log.Nav("PlayerPortrait",
                            $"LowTimeWarning has no OnVisibilityChanged field");
                        continue;
                    }

                    var unityEvent = onVisField.GetValue(mb) as UnityEvent<bool>;
                    if (unityEvent == null) continue;

                    if (isLocal)
                    {
                        _localLowTimeWarning = mb;
                        _localRopeCallback = (visible) =>
                        {
                            if (visible && _isActive)
                                _announcer.Announce(Strings.TimerLowTime, AnnouncementPriority.High);
                        };
                        unityEvent.AddListener(_localRopeCallback);
                        Log.Nav("PlayerPortrait",
                            $"Subscribed to local LowTimeWarning");
                    }
                    else
                    {
                        _opponentLowTimeWarning = mb;
                        _opponentRopeCallback = (visible) =>
                        {
                            if (visible && _isActive)
                                _announcer.Announce(Strings.TimerOpponentLowTime, AnnouncementPriority.High);
                        };
                        unityEvent.AddListener(_opponentRopeCallback);
                        Log.Nav("PlayerPortrait",
                            $"Subscribed to opponent LowTimeWarning");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("PlayerPortrait", $"Error subscribing to LowTimeWarning: {ex.Message}");
            }
        }

        /// <summary>
        /// Unsubscribes from LowTimeWarning events to prevent stale callbacks.
        /// </summary>
        private void UnsubscribeLowTimeWarnings()
        {
            try
            {
                if (_localLowTimeWarning != null && _localRopeCallback != null)
                {
                    var onVisField = _localLowTimeWarning.GetType().GetField("OnVisibilityChanged", PublicInstance);
                    var unityEvent = onVisField?.GetValue(_localLowTimeWarning) as UnityEvent<bool>;
                    unityEvent?.RemoveListener(_localRopeCallback);
                }
                if (_opponentLowTimeWarning != null && _opponentRopeCallback != null)
                {
                    var onVisField = _opponentLowTimeWarning.GetType().GetField("OnVisibilityChanged", PublicInstance);
                    var unityEvent = onVisField?.GetValue(_opponentLowTimeWarning) as UnityEvent<bool>;
                    unityEvent?.RemoveListener(_opponentRopeCallback);
                }
            }
            catch (Exception ex)
            {
                Log.Nav("PlayerPortrait",
                    $"Error unsubscribing from LowTimeWarning: {ex.Message}");
            }

            _localRopeCallback = null;
            _opponentRopeCallback = null;
        }
    }
}
