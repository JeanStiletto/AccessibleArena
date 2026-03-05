# UXEventQueuePatch.cs Code Index

## File Overview
Harmony patch for intercepting game events from the UXEventQueue. This allows announcing game events (draws, plays, damage, etc.) to the screen reader. Read-only patch - does not modify game state.

## Static Class: UXEventQueuePatch (line 16)

### Private Fields
- private static bool _patchApplied (line 18)
- private static int _eventCount (line 19)

### Public Methods
- public static void Initialize() (line 25)
  // Manually applies the Harmony patch after game assemblies are loaded

- public static void EnqueuePendingSinglePostfix(object __0) (line 131)
  // Postfix for single-event EnqueuePending(UXEvent evt)
  // __0 is the UXEvent parameter

- public static void EnqueuePendingMultiPostfix(object __0) (line 157)
  // Postfix for multi-event EnqueuePending(IEnumerable<UXEvent> evts)
  // __0 is IEnumerable<UXEvent>

### Private Methods
- private static Type FindType(string fullName) (line 110)
  // Finds a type by full name across all loaded assemblies
