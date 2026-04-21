using UnityEngine;
using UnityEngine.Events;
using MelonLoader;
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

        // MtgTimer model reflection cache (shared by MatchTimer and LowTimeWarning)
        private static FieldInfo _matchTimerField; // MatchTimer._matchTimer (MtgTimer)
        private static FieldInfo _timeRunningField; // MatchTimer._timeRunning (float)
        private static PropertyInfo _remainingTimeProp; // MtgTimer.RemainingTime (float)
        private static FieldInfo _runningField; // MtgTimer.Running (bool)
        private static bool _mtgTimerReflectionInitialized;

        // LowTimeWarning rope timer reflection cache
        private static FieldInfo _ltwActiveTimerField; // LowTimeWarning._activeTimer (MtgTimer)
        private static FieldInfo _ltwTimeRunningField; // LowTimeWarning._timeRunning (float)
        private static FieldInfo _ltwTimeoutPipsField; // LowTimeWarning._timeoutPips (List<TimeoutPip>)
        private static bool _ltwReflectionInitialized;

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

            if (!_mtgTimerReflectionInitialized)
                InitializeMtgTimerReflection(matchTimer);
            if (!_mtgTimerReflectionInitialized) return null;

            try
            {
                // Read private _matchTimer field (MtgTimer) from MatchTimer component
                var mtgTimer = _matchTimerField.GetValue(matchTimer);
                if (mtgTimer == null) return null;

                bool running = (bool)_runningField.GetValue(mtgTimer);
                if (!running) return null;

                float remainingTime = (float)_remainingTimeProp.GetValue(mtgTimer);
                float timeRunning = (float)_timeRunningField.GetValue(matchTimer);

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

            if (!_ltwReflectionInitialized)
                InitializeLtwReflection(ltw);
            if (!_ltwReflectionInitialized) return null;

            // Ensure MtgTimer reflection is ready (for RemainingTime/Running).
            // Try from MatchTimer first; fall back to LowTimeWarning's field type.
            if (!_mtgTimerReflectionInitialized)
            {
                var matchTimer = isOpponent ? _opponentMatchTimer : _localMatchTimer;
                if (matchTimer != null)
                    InitializeMtgTimerReflection(matchTimer);
            }
            if (!_mtgTimerReflectionInitialized)
                InitializeMtgTimerFromLtw();
            if (!_mtgTimerReflectionInitialized) return null;

            try
            {
                var activeTimer = _ltwActiveTimerField.GetValue(ltw);
                if (activeTimer == null) return null;

                bool running = (bool)_runningField.GetValue(activeTimer);
                if (!running) return null;

                float remainingTime = (float)_remainingTimeProp.GetValue(activeTimer);
                float timeRunning = (float)_ltwTimeRunningField.GetValue(ltw);

                float actualRemaining = remainingTime - timeRunning;
                if (actualRemaining < 0f) actualRemaining = 0f;

                // Get timeout count from pip list
                int timeouts = 0;
                var pipList = _ltwTimeoutPipsField.GetValue(ltw) as System.Collections.IList;
                if (pipList != null) timeouts = pipList.Count;

                return (FormatSecondsToReadable(actualRemaining), timeouts);
            }
            catch (Exception ex)
            {
                Log.Nav("PlayerPortrait", $"Error reading rope timer: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Initializes reflection cache for reading rope timer from LowTimeWarning.
        /// </summary>
        private static void InitializeLtwReflection(MonoBehaviour ltwComponent)
        {
            try
            {
                var ltwType = ltwComponent.GetType();

                _ltwActiveTimerField = ltwType.GetField("_activeTimer", PrivateInstance);
                _ltwTimeRunningField = ltwType.GetField("_timeRunning", PrivateInstance);
                _ltwTimeoutPipsField = ltwType.GetField("_timeoutPips", PrivateInstance);

                if (_ltwActiveTimerField == null || _ltwTimeRunningField == null)
                {
                    Log.Warn("PlayerPortrait", "Could not find _activeTimer or _timeRunning on LowTimeWarning");
                    return;
                }

                _ltwReflectionInitialized = true;
                Log.Msg("PlayerPortrait", "LowTimeWarning reflection initialized");
            }
            catch (Exception ex)
            {
                Log.Error("PlayerPortrait", $"Failed to initialize LowTimeWarning reflection: {ex.Message}");
            }
        }

        /// <summary>
        /// Fallback: initialize MtgTimer reflection from LowTimeWarning._activeTimer field type
        /// when no MatchTimer component is available (e.g., casual Brawl games).
        /// </summary>
        private static void InitializeMtgTimerFromLtw()
        {
            if (_ltwActiveTimerField == null) return;
            try
            {
                var mtgTimerType = _ltwActiveTimerField.FieldType;
                _remainingTimeProp = mtgTimerType.GetProperty("RemainingTime", PublicInstance);
                _runningField = mtgTimerType.GetField("Running", PublicInstance);

                if (_remainingTimeProp != null && _runningField != null)
                {
                    _mtgTimerReflectionInitialized = true;
                    Log.Msg("PlayerPortrait", "MtgTimer reflection initialized from LowTimeWarning field type");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("PlayerPortrait", $"Failed to init MtgTimer from LTW: {ex.Message}");
            }
        }

        /// <summary>
        /// Initializes reflection cache for reading MtgTimer from MatchTimer component.
        /// </summary>
        private static void InitializeMtgTimerReflection(MonoBehaviour matchTimerComponent)
        {
            try
            {
                var matchTimerType = matchTimerComponent.GetType();

                // MatchTimer._matchTimer is a private MtgTimer field
                _matchTimerField = matchTimerType.GetField("_matchTimer", PrivateInstance);
                // MatchTimer._timeRunning is a private float field
                _timeRunningField = matchTimerType.GetField("_timeRunning", PrivateInstance);

                if (_matchTimerField == null || _timeRunningField == null)
                {
                    Log.Warn("PlayerPortrait", "Could not find _matchTimer or _timeRunning fields on MatchTimer");
                    return;
                }

                // MtgTimer fields (accessed from the _matchTimer value)
                var mtgTimerType = _matchTimerField.FieldType;
                _remainingTimeProp = mtgTimerType.GetProperty("RemainingTime", PublicInstance);
                _runningField = mtgTimerType.GetField("Running", PublicInstance);

                if (_remainingTimeProp == null || _runningField == null)
                {
                    Log.Warn("PlayerPortrait", "Could not find RemainingTime/Running on MtgTimer");
                    return;
                }

                _mtgTimerReflectionInitialized = true;
                Log.Msg("PlayerPortrait", "MtgTimer reflection initialized");
            }
            catch (Exception ex)
            {
                Log.Error("PlayerPortrait", $"Failed to initialize MtgTimer reflection: {ex.Message}");
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
