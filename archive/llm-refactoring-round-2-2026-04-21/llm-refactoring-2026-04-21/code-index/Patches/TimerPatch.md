# TimerPatch.cs
Path: src/Patches/TimerPatch.cs
Lines: 99

## Top-level comments
- Harmony patch for intercepting timeout notifications from GameManager.Update_TimerNotification so the mod can announce timeout-extension usage to the screen reader.

## public static class TimerPatch (line 15)
### Fields
- private static bool _patchApplied (line 17)
- private static FieldInfo _triggeredByLocalField (line 20)
- private static FieldInfo _timeoutCountField (line 21)
### Methods
- public static void Initialize() (line 23) — Note: caches reflection for TimeoutNotification fields and applies postfix
- public static void TimerNotificationPostfix(object __0) (line 78) — Note: reads TriggeredByLocaPlayer/CurrentTimeoutCountForPlayer and forwards to DuelAnnouncer
