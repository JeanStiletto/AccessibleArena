# PhaseSkipGuard.cs
Path: src/Core/Services/PhaseSkipGuard.cs
Lines: 204

## Top-level comments
- Guards against accidental phase-skip when player has untapped lands in main phase. Intercepts at SendSubmitEventToSelectedObject (Unity EventSystem submit dispatch) and Input.GetKeyDown(Space), with release-tracking to prevent oscillation.

## public static class PhaseSkipGuard (line 21)
### Fields
- private static bool _warningShown (line 23)
- private static string _warningPhase (line 24)
- private static bool _waitingForRelease (line 25)
- private static bool _confirmed (line 26)
- private static string _confirmedPhase (line 27)
- private static bool _modalActiveDuringPress (line 28)
- private static PriorityController _priorityController (line 29)
- private static Func<bool> _isModalNavigatorActive (line 30)
- private static int _lastDecisionFrame (line 33)
- private static bool _blockThisFrame (line 34)

### Methods
- public static void SetPriorityController(PriorityController pc) (line 36)
- public static void SetModalNavigatorCheck(Func<bool> check) (line 42)
- public static void Poll() (line 52) — Note: per-frame release poll; must run every frame so `_waitingForRelease` clears even when hook-driven paths don't fire on the release frame.
- public static bool ShouldBlock() (line 76) — Note: frame-cached decision shared across SendSubmit prefix and GetKeyDown postfix hooks; has side-effects (sets warning state, triggers announcements).
- public static void Reset() (line 172) — Note: does NOT clear `_isModalNavigatorActive` (wired once from DuelNavigator and must survive duel-end resets).
- private static bool HasUntappedPlayerLands() (line 187)
