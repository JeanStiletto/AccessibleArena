using HarmonyLib;
using MelonLoader;
using System;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Patches
{
    /// <summary>
    /// Harmony patch for intercepting timeout notifications.
    /// When a player uses a timeout extension, the game hands a TimeoutNotification (seat ID of
    /// the player who triggered it, plus that player's remaining timeout count) to
    /// TimeoutNotificationHandler.Handle. We postfix this to announce the event to the screen reader.
    /// 2026.63 moved this out of GameManager.Update_TimerNotification and replaced the
    /// TriggeredByLocaPlayer bool with the TriggeredBy seat ID.
    /// </summary>
    public static class TimerPatch
    {
        private static bool _patchApplied = false;

        // Cached reflection for reading TimeoutNotification fields
        private static FieldInfo _triggeredByField;
        private static FieldInfo _timeoutCountField;

        public static void Initialize()
        {
            if (_patchApplied) return;

            try
            {
                var handlerType = FindType("Wotc.Mtga.DuelScene.TimeoutNotificationHandler");
                var targetMethod = handlerType?.GetMethod("Handle", BindingFlags.Instance | BindingFlags.Public);
                if (targetMethod == null)
                {
                    Log.Warn("TimerPatch", "Could not find TimeoutNotificationHandler.Handle - timeout announcements disabled");
                    return;
                }

                // Cache TimeoutNotification field accessors
                var tnType = FindType("GreClient.Rules.TimeoutNotification");
                if (tnType != null)
                {
                    _triggeredByField = tnType.GetField("TriggeredBy", PublicInstance);
                    _timeoutCountField = tnType.GetField("TimeoutCount", PublicInstance);
                }

                if (_triggeredByField == null || _timeoutCountField == null)
                {
                    Log.Warn("TimerPatch", "Could not find TimeoutNotification fields - timeout announcements disabled");
                    return;
                }

                // Apply Harmony postfix
                var harmony = new HarmonyLib.Harmony("com.accessibility.mtga.timerpatch");
                var postfix = typeof(TimerPatch).GetMethod(nameof(TimerNotificationPostfix),
                    BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(targetMethod, postfix: new HarmonyMethod(postfix));

                _patchApplied = true;
                Log.Msg("TimerPatch", "Harmony patch applied successfully");
            }
            catch (Exception ex)
            {
                Log.Error("TimerPatch", $"Initialization error: {ex}");
            }
        }

        /// <summary>
        /// Postfix for TimeoutNotificationHandler.Handle(TimeoutNotification tn).
        /// __0 is the TimeoutNotification parameter.
        /// </summary>
        public static void TimerNotificationPostfix(object __0)
        {
            try
            {
                if (__0 == null) return;

                uint triggeredBy = (uint)_triggeredByField.GetValue(__0);
                uint timeoutCount = (uint)_timeoutCountField.GetValue(__0);

                Log.Msg("TimerPatch", $"Timeout: triggeredBy={triggeredBy}, remainingTimeouts={timeoutCount}");

                var announcer = Core.Services.DuelAnnouncer.Instance;
                announcer?.OnTimerTimeout(triggeredBy, timeoutCount);
            }
            catch (Exception ex)
            {
                Log.Warn("TimerPatch", $"Error processing timeout notification: {ex.Message}");
            }
        }
    }
}
