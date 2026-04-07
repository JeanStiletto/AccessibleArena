// Stub for UnityEngine.InputLegacyModule.dll — minimal types needed by testable code.
namespace UnityEngine
{
    /// <summary>
    /// Stub for UnityEngine.Input. Test helpers allow simulating key state directly.
    /// </summary>
    public static class Input
    {
        private static readonly System.Collections.Generic.HashSet<KeyCode> _held
            = new System.Collections.Generic.HashSet<KeyCode>();
        private static readonly System.Collections.Generic.HashSet<KeyCode> _down
            = new System.Collections.Generic.HashSet<KeyCode>();

        public static bool GetKey(KeyCode key) => _held.Contains(key);
        public static bool GetKeyDown(KeyCode key) => _down.Contains(key);

        // ---- Test helpers ----
        public static void SimulateKeyDown(KeyCode key) { _down.Add(key); _held.Add(key); }
        public static void SimulateKeyHeld(KeyCode key) { _held.Add(key); _down.Remove(key); }
        public static void SimulateKeyReleased(KeyCode key) { _held.Remove(key); _down.Remove(key); }
        public static void ClearAll() { _held.Clear(); _down.Clear(); }
    }
}
