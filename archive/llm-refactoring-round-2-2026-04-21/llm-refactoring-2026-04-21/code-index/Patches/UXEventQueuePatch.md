# UXEventQueuePatch.cs
Path: src/Patches/UXEventQueuePatch.cs
Lines: 255

## Top-level comments
- Harmony patch for intercepting game events from UXEventQueue (EnqueuePending for non-NPE events, Execute for NPE events) so the mod can announce game events (draws, plays, damage, etc.) to the screen reader. Read-only.

## public static class UXEventQueuePatch (line 22)
### Fields
- private static bool _patchApplied (line 24)
- private static int _eventCount (line 25)
- private static readonly HashSet<string> _npeEventTypeNames (line 28) — Note: NPE event types processed via Execute() instead of EnqueuePending
### Methods
- public static void Initialize() (line 40) — Note: patches single and multi-event EnqueuePending and calls PatchNPEExecuteMethods
- private static void PatchNPEExecuteMethods(HarmonyLib.Harmony harmony) (line 130)
- public static void NPEExecutePostfix(object __instance) (line 170) — Note: forwards to DuelAnnouncer.OnGameEvent at Execute time
- public static void EnqueuePendingSinglePostfix(object __0) (line 189) — Note: skips NPE events; logs every 100th event
- public static void EnqueuePendingMultiPostfix(object __0) (line 219) — Note: iterates IEnumerable<UXEvent>; skips NPE events; logs every 100th event
