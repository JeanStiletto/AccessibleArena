# UnityInputStubs.cs
Path: tests/stubs/UnityInputLegacy/UnityInputStubs.cs
Lines: 23

## Top-level comments
- Stub for UnityEngine.InputLegacyModule.dll providing a controllable Input class with SimulateKey* helpers for tests.

## public static class Input (line 7)
### Fields
- private static readonly HashSet<KeyCode> _held (line 9)
- private static readonly HashSet<KeyCode> _down (line 11)
### Methods
- public static bool GetKey(KeyCode key) (line 14)
- public static bool GetKeyDown(KeyCode key) (line 15)
- public static void SimulateKeyDown(KeyCode key) (line 18) — Note: test helper; adds to both _down and _held
- public static void SimulateKeyHeld(KeyCode key) (line 19) — Note: test helper; simulates key remaining held (removes from _down, keeps in _held)
- public static void SimulateKeyReleased(KeyCode key) (line 20) — Note: test helper; clears from both sets
- public static void ClearAll() (line 21) — Note: test helper; resets all state
