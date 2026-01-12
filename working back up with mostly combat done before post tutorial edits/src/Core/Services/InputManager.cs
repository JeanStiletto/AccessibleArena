using System;
using System.Collections.Generic;
using UnityEngine;
using MTGAAccessibility.Core.Interfaces;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// Input manager that works alongside the game's existing KeyboardManager.
    ///
    /// Game's built-in keybinds (DO NOT OVERRIDE):
    /// - Arrow keys: Navigation (Up, Down, Left, Right)
    /// - Tab / Shift+Tab: Next / Previous
    /// - Enter / Space: Accept / Submit
    /// - Escape: Cancel / Back
    /// - F: Find
    /// - Alt (hold): Alt view
    ///
    /// Our custom shortcuts (safe to use):
    /// - C: Hand zone (Cards)
    /// - B: Battlefield zone
    /// - G: Graveyard zone
    /// - X: Exile zone
    /// - S: Stack zone
    /// - T: Turn/phase info
    /// - L: Life totals
    /// - A: Your mana pool
    /// - Shift+A: Opponent mana pool
    /// - Shift+G: Opponent graveyard
    /// - Shift+X: Opponent exile
    /// - P: Pass priority
    /// - F1: Help
    /// - F2: Current context info
    /// - Ctrl+R: Repeat last announcement
    ///
    /// The game uses MTGA.KeyboardManager for input handling.
    /// We poll for our custom keys only and let the game handle navigation.
    /// </summary>
    public class InputManager : IInputHandler
    {
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
