using System;
using System.Collections.Generic;
using UnityEngine;
using MelonLoader;
using MTGAAccessibility.Core.Interfaces;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// Input manager that handles keyboard input with the ability to consume/block
    /// keys from reaching the game's KeyboardManager.
    ///
    /// Key consumption: When the mod handles a key (e.g., Enter in player info zone),
    /// we mark it as "consumed" so the KeyboardManagerPatch blocks it from the game.
    /// This prevents unintended game actions like "pass priority" when pressing Enter.
    /// </summary>
    public class InputManager : IInputHandler
    {
        // Static key consumption tracking - checked by KeyboardManagerPatch
        private static HashSet<KeyCode> _consumedKeysThisFrame = new HashSet<KeyCode>();
        private static int _lastConsumeFrame = -1;

        /// <summary>
        /// Marks a key as consumed this frame. The game's KeyboardManager will not
        /// receive this key press (blocked by Harmony patch).
        /// </summary>
        public static void ConsumeKey(KeyCode key)
        {
            // Clear consumed keys if this is a new frame
            int currentFrame = Time.frameCount;
            if (currentFrame != _lastConsumeFrame)
            {
                _consumedKeysThisFrame.Clear();
                _lastConsumeFrame = currentFrame;
            }

            _consumedKeysThisFrame.Add(key);
            MelonLogger.Msg($"[InputManager] Consumed key: {key}");
        }

        /// <summary>
        /// Checks if a key was consumed this frame by the mod.
        /// Called by KeyboardManagerPatch to decide whether to block the key.
        /// </summary>
        public static bool IsKeyConsumed(KeyCode key)
        {
            // If we're on a different frame, nothing is consumed
            if (Time.frameCount != _lastConsumeFrame)
            {
                return false;
            }
            return _consumedKeysThisFrame.Contains(key);
        }

        /// <summary>
        /// Checks if a key is pressed AND consumes it so the game doesn't see it.
        /// Use this instead of Input.GetKeyDown when you want to block the game.
        /// </summary>
        public static bool GetKeyDownAndConsume(KeyCode key)
        {
            if (Input.GetKeyDown(key))
            {
                ConsumeKey(key);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if Enter key is pressed and consumes it.
        /// </summary>
        public static bool GetEnterAndConsume()
        {
            bool pressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            if (pressed)
            {
                ConsumeKey(KeyCode.Return);
                ConsumeKey(KeyCode.KeypadEnter);
            }
            return pressed;
        }

        private readonly IShortcutRegistry _shortcuts;
        private readonly IContextManager _contextManager;
        private readonly IAnnouncementService _announcer;

        // Only monitor keys we use for custom shortcuts (not game navigation)
        private readonly HashSet<KeyCode> _customKeys = new HashSet<KeyCode>
        {
            // Zone shortcuts
            KeyCode.C,  // Hand (Cards)
            KeyCode.B,  // Battlefield
            KeyCode.G,  // Graveyard
            KeyCode.X,  // Exile
            KeyCode.S,  // Stack (only when not in text input)

            // Info shortcuts
            KeyCode.T,  // Turn info
            KeyCode.L,  // Life totals
            KeyCode.A,  // Mana pool (Shift+A for opponent)
            KeyCode.P,  // Pass priority

            // Function keys (safe)
            KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4,

            // With modifiers
            KeyCode.R,  // Ctrl+R for repeat
        };

        public event Action<KeyCode> OnKeyPressed;
        public event Action OnNavigateNext;
        public event Action OnNavigatePrevious;
        public event Action OnAccept;
        public event Action OnCancel;

        public InputManager(IShortcutRegistry shortcuts, IContextManager contextManager, IAnnouncementService announcer)
        {
            _shortcuts = shortcuts;
            _contextManager = contextManager;
            _announcer = announcer;
        }

        public void OnUpdate()
        {
            // Only check our custom shortcut keys
            // Let the game handle navigation keys (arrows, tab, enter, escape)
            foreach (var key in _customKeys)
            {
                if (Input.GetKeyDown(key))
                {
                    ProcessCustomKey(key);
                }
            }
        }

        private void ProcessCustomKey(KeyCode key)
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            // Skip if any modifier conflicts with game controls
            // Alt is used by game for alt-view
            if (alt && key != KeyCode.F4) // Allow Alt+F4
                return;

            // Process through shortcut registry
            if (_shortcuts.ProcessKey(key, shift, ctrl, alt))
            {
                OnKeyPressed?.Invoke(key);
            }
        }

        /// <summary>
        /// Called by Harmony patches when game navigation occurs.
        /// This allows us to announce what the game did.
        /// </summary>
        public void OnGameNavigateNext()
        {
            OnNavigateNext?.Invoke();
            var context = _contextManager.ActiveContext;
            context?.MoveNext();
        }

        /// <summary>
        /// Called by Harmony patches when game navigation occurs.
        /// </summary>
        public void OnGameNavigatePrevious()
        {
            OnNavigatePrevious?.Invoke();
            var context = _contextManager.ActiveContext;
            context?.MovePrevious();
        }

        /// <summary>
        /// Called by Harmony patches when game accept action occurs.
        /// </summary>
        public void OnGameAccept()
        {
            OnAccept?.Invoke();
            var context = _contextManager.ActiveContext;
            context?.Accept();
        }

        /// <summary>
        /// Called by Harmony patches when game cancel/back action occurs.
        /// </summary>
        public void OnGameCancel()
        {
            OnCancel?.Invoke();
            _announcer.Silence();
            var context = _contextManager.ActiveContext;
            context?.Cancel();
        }
    }
}
