using HarmonyLib;
using MelonLoader;
using AccessibleArena.Core.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;

namespace AccessibleArena.Patches
{
    /// <summary>
    /// Harmony patch for intercepting game events from the UXEventQueue.
    /// This allows us to announce game events (draws, plays, damage, etc.) to the screen reader.
    ///
    /// IMPORTANT: This patch only READS events - it does not modify game state.
    /// We only announce publicly visible information (no hidden info like opponent's hand contents).
    ///
    /// Non-NPE events are intercepted at EnqueuePending time (immediate — correct for game state changes).
    /// NPE events are intercepted at Execute time (when the event actually plays — correct for
    /// dialog/reminder/tooltip timing that must match on-screen presentation).
    /// </summary>
    public static class UXEventQueuePatch
    {
        private static bool _patchApplied = false;
        private static int _eventCount = 0;

        // NPE event type names — these are processed at Execute() time, not EnqueuePending time
        private static readonly HashSet<string> _npeEventTypeNames = new HashSet<string>
        {
            "NPEDialogUXEvent",
            "NPEReminderUXEvent",
            "NPEDismissableDeluxeTooltipUXEvent",
            "NPEWarningUXEvent",
        };

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
                var uxEventQueueType = FindType(T.UXEventQueueFQ);
                var uxEventType = FindType(T.UXEventFQ);

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
                foreach (var m in uxEventQueueType.GetMethods(AllInstanceFlags))
                {
                    MelonLogger.Msg($"  - {m.Name}({string.Join(", ", Array.ConvertAll(m.GetParameters(), p => p.ParameterType.Name))})");
                }

                // Patch the single-event EnqueuePending method
                var singleEventMethod = uxEventQueueType.GetMethod("EnqueuePending",
                    PublicInstance,
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
                    PublicInstance,
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

                // Patch NPE event Execute() methods — these fire at the correct time
                // (when the event actually plays on screen), not at enqueue time
                PatchNPEExecuteMethods(harmony);

                _patchApplied = true;
                MelonLogger.Msg("[UXEventQueuePatch] Harmony patches applied successfully");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[UXEventQueuePatch] Initialization error: {ex}");
            }
        }

        /// <summary>
        /// Patches Execute() on NPE event types so we announce them when they actually play,
        /// not when they're batched into the pending queue.
        /// </summary>
        private static void PatchNPEExecuteMethods(HarmonyLib.Harmony harmony)
        {
            var npeTypeNames = new[]
            {
                "Wotc.Mtga.DuelScene.UXEvents.NPEDialogUXEvent",
                "Wotc.Mtga.DuelScene.UXEvents.NPEReminderUXEvent",
                "Wotc.Mtga.DuelScene.UXEvents.NPEDismissableDeluxeTooltipUXEvent",
                "Wotc.Mtga.DuelScene.UXEvents.NPEWarningUXEvent",
            };

            var postfix = typeof(UXEventQueuePatch).GetMethod(nameof(NPEExecutePostfix),
                BindingFlags.Static | BindingFlags.Public);

            int patchCount = 0;
            foreach (var typeName in npeTypeNames)
            {
                var type = FindType(typeName);
                if (type == null)
                {
                    MelonLogger.Warning($"[UXEventQueuePatch] Could not find NPE type: {typeName}");
                    continue;
                }

                var executeMethod = type.GetMethod("Execute", PublicInstance);
                if (executeMethod == null || executeMethod.DeclaringType != type)
                {
                    MelonLogger.Warning($"[UXEventQueuePatch] Could not find Execute() on {type.Name}");
                    continue;
                }

                harmony.Patch(executeMethod, postfix: new HarmonyMethod(postfix));
                patchCount++;
            }

            MelonLogger.Msg($"[UXEventQueuePatch] Patched {patchCount} NPE Execute() methods");
        }

        /// <summary>
        /// Postfix for NPE event Execute() methods. Fires when the event actually plays on screen.
        /// </summary>
        public static void NPEExecutePostfix(object __instance)
        {
            try
            {
                if (__instance == null) return;

                var announcer = Core.Services.DuelAnnouncer.Instance;
                announcer?.OnGameEvent(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[UXEventQueuePatch] Error processing NPE execute: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for single-event EnqueuePending(UXEvent evt).
        /// Skips NPE events (handled at Execute time instead).
        /// </summary>
        public static void EnqueuePendingSinglePostfix(object __0) // __0 is the UXEvent parameter
        {
            try
            {
                if (__0 == null) return;

                _eventCount++;
                if (_eventCount % 100 == 1)
                {
                    Core.Utils.Log.Patch(
                        "UXEventQueuePatch",
                        $"Single event #{_eventCount}: {__0.GetType().Name}");
                }

                // Skip NPE events — they are processed at Execute() time for correct timing
                if (_npeEventTypeNames.Contains(__0.GetType().Name)) return;

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
        /// Postfix for multi-event EnqueuePending(IEnumerable<UXEvent> evts).
        /// Skips NPE events (handled at Execute time instead).
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
                    if (_eventCount % 100 == 1)
                    {
                        Core.Utils.Log.Patch(
                            "UXEventQueuePatch",
                            $"Multi event #{_eventCount}: {evt.GetType().Name}");
                    }

                    // Skip NPE events — they are processed at Execute() time for correct timing
                    if (_npeEventTypeNames.Contains(evt.GetType().Name)) continue;

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
