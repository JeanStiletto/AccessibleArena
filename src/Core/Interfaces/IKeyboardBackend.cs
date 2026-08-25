using UnityEngine;

namespace AccessibleArena.Core.Interfaces
{
    /// <summary>
    /// Source of raw keyboard state for <see cref="Utils.KeyInput"/>.
    /// The game build uses InputSystemKeyboardBackend; tests substitute a fake
    /// so key-driven logic (e.g. KeyHoldRepeater) runs without an input device.
    /// </summary>
    public interface IKeyboardBackend
    {
        /// <summary>Key is currently held down.</summary>
        bool IsPressed(KeyCode key);

        /// <summary>Key went down this frame.</summary>
        bool WasPressedThisFrame(KeyCode key);

        /// <summary>Any keyboard key went down this frame.</summary>
        bool AnyKeyDownThisFrame { get; }

        /// <summary>Characters typed this frame (legacy Input.inputString semantics).</summary>
        string TextThisFrame { get; }
    }
}
