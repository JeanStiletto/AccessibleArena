using UnityEngine;
using AccessibleArena.Core.Interfaces;

namespace AccessibleArena.Core.Utils
{
    /// <summary>
    /// The mod's single point of keyboard access, mirroring the legacy
    /// UnityEngine.Input API shape so call sites stay familiar. Backed by
    /// <see cref="InputSystemKeyboardBackend"/> in the game; tests swap in a
    /// fake via <see cref="Backend"/>. Do not call UnityEngine.Input anywhere —
    /// the game runs with legacy input disabled and every call throws.
    /// </summary>
    public static class KeyInput
    {
        private static IKeyboardBackend _backend;

        /// <summary>Active backend. Settable for tests.</summary>
        public static IKeyboardBackend Backend
        {
            get => _backend ?? (_backend = new InputSystemKeyboardBackend());
            set => _backend = value;
        }

        /// <summary>Key is currently held down.</summary>
        public static bool GetKey(KeyCode key) => Backend.IsPressed(key);

        /// <summary>Key went down this frame.</summary>
        public static bool GetKeyDown(KeyCode key) => Backend.WasPressedThisFrame(key);

        /// <summary>Any keyboard key went down this frame (keyboard only, unlike legacy Input.anyKeyDown).</summary>
        public static bool AnyKeyDown => Backend.AnyKeyDownThisFrame;

        /// <summary>Characters typed this frame (legacy Input.inputString semantics).</summary>
        public static string InputString => Backend.TextThisFrame;
    }
}
