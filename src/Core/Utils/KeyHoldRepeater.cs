using System;
using UnityEngine;

namespace AccessibleArena.Core.Utils
{
    /// <summary>
    /// Tracks a single held key and fires repeated actions after an initial delay.
    /// Used by navigators to enable hold-to-repeat for arrow key navigation.
    /// </summary>
    public class KeyHoldRepeater
    {
        private const float InitialDelay = 0.5f;
        private const float RepeatInterval = 0.1f;

        private KeyCode _heldKey;
        private float _holdTimer;
        private bool _isHolding;

        /// <summary>
        /// Check if a key should fire its action (initial press or hold-repeat).
        /// Returns true if the key event was consumed (caller should return).
        /// The action returns false to stop repeating (e.g., at a boundary).
        /// </summary>
        public bool Check(KeyCode key, Func<bool> action)
        {
            // Key released — stop tracking
            if (_isHolding && _heldKey == key && !KeyInput.GetKey(key))
            {
                _isHolding = false;
                return false;
            }

            // Initial key press
            if (KeyInput.GetKeyDown(key))
            {
                // Clear any previous hold (different key)
                _isHolding = false;

                bool moved = action();
                // Start hold tracking — even if action returned false (boundary),
                // we consume the initial press
                _heldKey = key;
                _holdTimer = 0f;
                _isHolding = moved; // Only track hold if action succeeded
                return true;
            }

            // Sustained hold — only for the tracked key
            if (_isHolding && _heldKey == key && KeyInput.GetKey(key))
            {
                _holdTimer += Time.unscaledDeltaTime;
                if (_holdTimer >= InitialDelay)
                {
                    // After initial delay, fire at repeat interval
                    _holdTimer -= RepeatInterval;
                    // Clamp so we don't fire multiple repeats if a frame was very long
                    if (_holdTimer < InitialDelay - RepeatInterval)
                        _holdTimer = InitialDelay - RepeatInterval;

                    if (!action())
                    {
                        // Action returned false (boundary) — stop repeating
                        _isHolding = false;
                    }
                }
                return true; // Key is held, consume it
            }

            return false;
        }

        /// <summary>
        /// Check overload for actions that always repeat (no boundary stop).
        /// </summary>
        public bool Check(KeyCode key, Action action)
        {
            return Check(key, () => { action(); return true; });
        }

        /// <summary>
        /// Clear all hold state. Call when navigator deactivates or mode changes.
        /// </summary>
        public void Reset()
        {
            _isHolding = false;
            _heldKey = KeyCode.None;
            _holdTimer = 0f;
        }
    }
}
