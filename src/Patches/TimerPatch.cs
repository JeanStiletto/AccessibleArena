using HarmonyLib;
using MelonLoader;
using System;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Patches
{
    /// <summary>
    /// Harmony patch for intercepting timeout notifications from GameManager.
    /// When a player uses a timeout extension, the game calls Update_TimerNotification
    /// with a TimeoutNotification containing who triggered it and remaining timeout count.
    /// We postfix this to announce the event to the screen reader.
    /// </summary>
    public static class TimerPatch
    {
        private static bool _patchApplied = false;

        // Cached reflection for reading TimeoutNotification fields
        private static FieldInfo _triggeredByLocalField;
        private static FieldInfo _timeoutCountField;

        public static void Initialize()
        {
            if (_patchApplied) return;

            try
            {
                // GameManager has no namespace (root level), lives in Core.dll
                var gameManagerType = FindType("GameManager");
                if (gameManagerType == null)
                {
                    Log.Warn("TimerPatch", "Could not find GameManager type - timeout announcements disabled");
                    return;
                }

                // Find the private Update_TimerNotification method
                var targetMethod = gameManagerType.GetMethod("Update_TimerNotification", PrivateInstance);
                if (targetMethod == null)
                {
                    Log.Warn("TimerPatch", "Could not find Update_TimerNotification method - timeout announcements disabled");
                    return;
                }

                // Cache TimeoutNotification field accessors
                var tnType = FindType("GreClient.Rules.TimeoutNotification");
                if (tnType != null)
                {
                    _triggeredByLocalField = tnType.GetField("TriggeredByLocaPlayer", PublicInstance);
                    _timeoutCountField = tnType.GetField("CurrentTimeoutCountForPlayer", PublicInstance);
                }

                if (_triggeredByLocalField == null || _timeoutCountField == null)
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
        /// Postfix for GameManager.Update_TimerNotification(TimeoutNotification tn).
        /// __0 is the TimeoutNotification parameter.
        /// </summary>
        public static void TimerNotificationPostfix(object __0)
        {
            try
            {
                if (__0 == null) return;

                bool isLocal = (bool)_triggeredByLocalField.GetValue(__0);
                uint timeoutCount = (uint)_timeoutCountField.GetValue(__0);

                Log.Msg("TimerPatch", $"Timeout: isLocal={isLocal}, remainingTimeouts={timeoutCount}");

                var announcer = Core.Services.DuelAnnouncer.Instance;
                announcer?.OnTimerTimeout(isLocal, timeoutCount);
            }
            catch (Exception ex)
            {
                Log.Warn("TimerPatch", $"Error processing timeout notification: {ex.Message}");
            }
        }
    }
}
