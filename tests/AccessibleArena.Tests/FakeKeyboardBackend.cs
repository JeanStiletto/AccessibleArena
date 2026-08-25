using System.Collections.Generic;
using UnityEngine;
using AccessibleArena.Core.Interfaces;

namespace AccessibleArena.Tests
{
    /// <summary>
    /// Test double for KeyInput's backend. The Simulate* helpers mirror the old
    /// UnityEngine.Input stub so key-driven logic can be tested without a device.
    /// </summary>
    public class FakeKeyboardBackend : IKeyboardBackend
    {
        private readonly HashSet<KeyCode> _held = new HashSet<KeyCode>();
        private readonly HashSet<KeyCode> _down = new HashSet<KeyCode>();

        public bool IsPressed(KeyCode key) => _held.Contains(key);
        public bool WasPressedThisFrame(KeyCode key) => _down.Contains(key);
        public bool AnyKeyDownThisFrame => _down.Count > 0;
        public string TextThisFrame { get; set; } = string.Empty;

        // ---- Test helpers ----
        public void SimulateKeyDown(KeyCode key) { _down.Add(key); _held.Add(key); }
        public void SimulateKeyHeld(KeyCode key) { _held.Add(key); _down.Remove(key); }
        public void SimulateKeyReleased(KeyCode key) { _held.Remove(key); _down.Remove(key); }
        public void ClearAll() { _held.Clear(); _down.Clear(); }
    }
}
