using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Patches
{
    /// <summary>
    /// Harmony patches on the game's AudioManager that log music-related audio calls.
    ///
    /// The booster music is layered: a base track per screen (Wwise state group "music"),
    /// per-set pack themes gated by "boosterpack_&lt;set&gt;" RTPCs, and a card-reveal layer
    /// gated by "booster_card_rollover". Each layer plays whenever its RTPC is above 0, so
    /// two layers left at 100 at the same time means two songs playing at once. Since the
    /// mod drives hover state with simulated pointer events, a missing PointerExit shows up
    /// as exactly that. This patch makes the layer state audible in the log:
    /// - every SetRTPCValue call whose name starts with "booster"
    /// - every SetState call (music/ambience state changes)
    /// - every PostEvent whose event name mentions music (Music_Play, Music_Stop, ...)
    /// - a warning whenever more than one music layer is active simultaneously
    /// </summary>
    public static class AudioLogPatch
    {
        private static bool _patchApplied = false;

        // Last known value per music-layer RTPC (boosterpack_* and booster_card_rollover)
        private static readonly Dictionary<string, float> _layers =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        // Overlap tracking: the deliberate click+exit pair around a card reveal turns a
        // second layer on for a few milliseconds, which is inaudible and expected. Only
        // warn when an overlap persists — checked on the next tracked call, and
        // retroactively when the overlap ends.
        private static bool _overlapActive;
        private static bool _overlapWarned;
        private static DateTime _overlapStart;
        private const int OverlapWarnMs = 250;

        public static void Initialize()
        {
            if (_patchApplied) return;

            try
            {
                var audioManagerType = FindType("AudioManager");
                if (audioManagerType == null)
                {
                    Log.Warn("AudioLog", "Could not find AudioManager type - audio logging disabled");
                    return;
                }

                var harmony = new HarmonyLib.Harmony("com.accessibility.mtga.audiologpatch");
                var flags = BindingFlags.Public | BindingFlags.Static;

                int patched = 0;
                foreach (var method in audioManagerType.GetMethods(flags))
                {
                    var pars = method.GetParameters();
                    if (method.Name == "SetRTPCValue")
                    {
                        // All overloads start with (string name, float value, ...)
                        harmony.Patch(method, prefix: new HarmonyMethod(
                            typeof(AudioLogPatch).GetMethod(nameof(RtpcPrefix), flags)));
                        patched++;
                    }
                    else if (method.Name == "SetState" &&
                             pars.Length == 2 && pars[0].ParameterType == typeof(string))
                    {
                        harmony.Patch(method, prefix: new HarmonyMethod(
                            typeof(AudioLogPatch).GetMethod(nameof(StatePrefix), flags)));
                        patched++;
                    }
                    else if (method.Name == "PostEvent" &&
                             pars.Length > 0 && pars[0].ParameterType == typeof(string))
                    {
                        harmony.Patch(method, prefix: new HarmonyMethod(
                            typeof(AudioLogPatch).GetMethod(nameof(PostEventPrefix), flags)));
                        patched++;
                    }
                }

                _patchApplied = patched > 0;
                if (_patchApplied)
                    Log.Msg("AudioLog", $"Audio logging patches applied ({patched} methods)");
                else
                    Log.Warn("AudioLog", "No AudioManager methods matched - audio logging disabled");
            }
            catch (Exception ex)
            {
                Log.Error("AudioLog", $"Initialization error: {ex}");
            }
        }

        /// <summary>
        /// Prefix for AudioManager.SetRTPCValue(string name, float value, ...).
        /// Parameter names match the target so Harmony binds them across all overloads.
        /// </summary>
        public static void RtpcPrefix(string name, float value)
        {
            try
            {
                if (string.IsNullOrEmpty(name) ||
                    !name.StartsWith("booster", StringComparison.OrdinalIgnoreCase))
                    return;

                Log.Msg("AudioLog", $"RTPC {name} = {value}");

                // Music layers: per-set pack themes and the card-reveal layer.
                // booster_packrollover is the master duck, not a layer of its own.
                if (name.StartsWith("boosterpack_", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("booster_card_rollover", StringComparison.OrdinalIgnoreCase))
                {
                    _layers[name] = value;
                    var active = _layers.Where(kv => kv.Value > 0f).Select(kv => kv.Key).ToList();
                    if (active.Count > 1)
                    {
                        if (!_overlapActive)
                        {
                            _overlapActive = true;
                            _overlapWarned = false;
                            _overlapStart = DateTime.UtcNow;
                        }
                        else if (!_overlapWarned &&
                                 (DateTime.UtcNow - _overlapStart).TotalMilliseconds > OverlapWarnMs)
                        {
                            _overlapWarned = true;
                            Log.Warn("AudioLog", $"MULTIPLE music layers active since " +
                                $"{(int)(DateTime.UtcNow - _overlapStart).TotalMilliseconds} ms: {string.Join(", ", active)}");
                        }
                    }
                    else if (_overlapActive)
                    {
                        _overlapActive = false;
                        double ms = (DateTime.UtcNow - _overlapStart).TotalMilliseconds;
                        if (ms > OverlapWarnMs && !_overlapWarned)
                            Log.Warn("AudioLog", $"Music layers overlapped for {(int)ms} ms");
                    }
                }
            }
            catch { }
        }

        /// <summary>Prefix for AudioManager.SetState(string stateGroup, string state).</summary>
        public static void StatePrefix(string stateGroup, string state)
        {
            try
            {
                Log.Msg("AudioLog", $"State {stateGroup} = {state}");
            }
            catch { }
        }

        /// <summary>Prefix for AudioManager.PostEvent(string eventName, ...).</summary>
        public static void PostEventPrefix(string eventName)
        {
            try
            {
                if (!string.IsNullOrEmpty(eventName) &&
                    eventName.IndexOf("music", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Log.Msg("AudioLog", $"Event {eventName}");
                }
            }
            catch { }
        }
    }
}
