using HarmonyLib;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace MTGAAccessibility.Patches
{
    /// <summary>
    /// Harmony patch for intercepting game events from the UXEventQueue.
    /// This allows us to announce game events (draws, plays, damage, etc.) to the screen reader.
    ///
    /// IMPORTANT: This patch only READS events - it does not modify game state.
    /// We only announce publicly visible information (no hidden info like opponent's hand contents).
    /// </summary>
    public static class UXEventQueuePatch
    {
        private static bool _patchApplied = false;
        private static int _eventCount = 0;

        /// <summary>
        /// Manually applies the Harmony patch after game assemblies are loaded.
        /// Called during mod initialization.
        /// </summary>
        public static void Initialize()
        {
            if (_patchApplied) return;

            try
            {
                // Find the UXEventQueue type
                var uxEventQueueType = FindType("Wotc.Mtga.DuelScene.UXEvents.UXEventQueue");
                var uxEventType = FindType("Wotc.Mtga.DuelScene.UXEvents.UXEvent");

                if (uxEventQueueType == null)
                {
                    MelonLogger.Warning("[UXEventQueuePatch] Could not find UXEventQueue type - duel announcements disabled");
                    return;
                }

                if (uxEventType == null)
                {
                    MelonLogger.Warning("[UXEventQueuePatch] Could not find UXEvent type - duel announcements disabled");
                    return;
                }

                MelonLogger.Msg("[UXEventQueuePatch] Found UXEventQueue and UXEvent types");

                // Create Harmony instance
                var harmony = new HarmonyLib.Harmony("com.accessibility.mtga.uxeventpatch");

                // List all methods on UXEventQueue for debugging
                MelonLogger.Msg("[UXEventQueuePatch] Available methods on UXEventQueue:");
                foreach (var m in uxEventQueueType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    MelonLogger.Msg($"  - {m.Name}({string.Join(", ", Array.ConvertAll(m.GetParameters(), p => p.ParameterType.Name))})");
                }

                // Patch the single-event EnqueuePending method
                var singleEventMethod = uxEventQueueType.GetMethod("EnqueuePending",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new Type[] { uxEventType },
                    null);

                if (singleEventMethod != null)
                {
                    var postfixSingle = typeof(UXEventQueuePatch).GetMethod(nameof(EnqueuePendingSinglePostfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(singleEventMethod, postfix: new HarmonyMethod(postfixSingle));
                    MelonLogger.Msg($"[UXEventQueuePatch] Patched single-event EnqueuePending");
                }
                else
                {
                    MelonLogger.Warning("[UXEventQueuePatch] Could not find single-event EnqueuePending");
                }

                // Patch the multi-event EnqueuePending method (IEnumerable<UXEvent>)
                var iEnumerableType = typeof(IEnumerable<>).MakeGenericType(uxEventType);
                var multiEventMethod = uxEventQueueType.GetMethod("EnqueuePending",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new Type[] { iEnumerableType },
                    null);

                if (multiEventMethod != null)
                {
                    var postfixMulti = typeof(UXEventQueuePatch).GetMethod(nameof(EnqueuePendingMultiPostfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(multiEventMethod, postfix: new HarmonyMethod(postfixMulti));
                    MelonLogger.Msg($"[UXEventQueuePatch] Patched multi-event EnqueuePending");
                }
                else
                {
                    MelonLogger.Warning("[UXEventQueuePatch] Could not find multi-event EnqueuePending");
                }

                _patchApplied = true;
                MelonLogger.Msg("[UXEventQueuePatch] Harmony patches applied successfully");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[UXEventQueuePatch] Initialization error: {ex}");
            }
        }

        /// <summary>
        /// Finds a type by full name across all loaded assemblies.
        /// </summary>
        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName);
                    if (type != null)
                        return type;
                }
                catch
                {
                    // Ignore assembly load errors
                }
            }
            return null;
        }

        /// <summary>
        /// Postfix for single-event EnqueuePending(UXEvent evt)
        /// </summary>
        public static void EnqueuePendingSinglePostfix(object __0) // __0 is the UXEvent parameter
        {
            try
            {
                if (__0 == null) return;

                _eventCount++;
                // Log every 100th event to avoid spam
                if (_eventCount % 100 == 1)
                {
                    MelonLogger.Msg($"[UXEventQueuePatch] Single event #{_eventCount}: {__0.GetType().Name}");
                }

                // Pass to DuelAnnouncer for processing
                var announcer = Core.Services.DuelAnnouncer.Instance;
                announcer?.OnGameEvent(__0);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[UXEventQueuePatch] Error processing single event: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for multi-event EnqueuePending(IEnumerable<UXEvent> evts)
        /// </summary>
        public static void EnqueuePendingMultiPostfix(object __0) // __0 is IEnumerable<UXEvent>
        {
            try
            {
                if (__0 == null) return;

                // __0 is IEnumerable<UXEvent>, iterate through it
                var enumerable = __0 as System.Collections.IEnumerable;
                if (enumerable == null) return;

                foreach (var evt in enumerable)
                {
                    if (evt == null) continue;

                    _eventCount++;
                    // Log every 100th event to avoid spam
                    if (_eventCount % 100 == 1)
                    {
                        MelonLogger.Msg($"[UXEventQueuePatch] Multi event #{_eventCount}: {evt.GetType().Name}");
                    }

                    // Pass to DuelAnnouncer for processing
                    var announcer = Core.Services.DuelAnnouncer.Instance;
                    announcer?.OnGameEvent(evt);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[UXEventQueuePatch] Error processing multi event: {ex.Message}");
            }
        }
    }
}
