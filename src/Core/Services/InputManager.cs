using System;
using System.Collections.Generic;
using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;

namespace AccessibleArena.Core.Services
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
        /// When true, Escape is blocked from reaching the game.
        /// Set when mod overlay menus (Help, Settings) are active.
        /// </summary>
        public static bool ModMenuActive { get; set; }

        /// <summary>
        /// When true, EventSystemPatch blocks Unity's Submit events for toggles.
        /// Set by navigators when the current element is a toggle, cleared when moving away.
        /// This persistent flag works around the timing issue where EventSystem.Update()
        /// runs BEFORE our MonoBehaviour.Update() can consume keys.
        /// </summary>
        private static bool _blockSubmitForToggle;
        public static bool BlockSubmitForToggle
        {
            get => _blockSubmitForToggle;
            set
            {
                if (_blockSubmitForToggle != value)
                {
                    MelonLogger.Msg($"[InputManager] BlockSubmitForToggle changed: {_blockSubmitForToggle} -> {value}");
                    _blockSubmitForToggle = value;
                }
            }
        }

        /// <summary>
        /// Set by EventSystemPatch when Enter is pressed but blocked because we're on a toggle.
        /// Our HandleInput checks this flag to know Enter was pressed and should activate the toggle.
        /// Frame-aware to prevent double-activation from multiple GetKeyDown calls per frame.
        /// </summary>
        private static int _enterPressedWhileBlockedFrame = -1;
        private static int _enterPressedHandledFrame = -1;

        public static bool EnterPressedWhileBlocked
        {
            get
            {
                // Only return true if set THIS frame and not already handled this frame
                int currentFrame = Time.frameCount;
                return _enterPressedWhileBlockedFrame == currentFrame && _enterPressedHandledFrame != currentFrame;
            }
            set
            {
                if (value)
                {
                    // Only set if not already set this frame
                    int currentFrame = Time.frameCount;
                    if (_enterPressedWhileBlockedFrame != currentFrame)
                    {
                        _enterPressedWhileBlockedFrame = currentFrame;
                    }
                }
            }
        }

        /// <summary>
        /// Mark EnterPressedWhileBlocked as handled for this frame to prevent double-activation.
        /// </summary>
        public static void MarkEnterHandled()
        {
            _enterPressedHandledFrame = Time.frameCount;
        }

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
        /// Also checks EnterPressedWhileBlocked for when EventSystemPatch blocked Enter on a toggle.
        /// </summary>
        public static bool GetEnterAndConsume()
        {
            // Check both direct key press AND the blocked flag (for when Enter was blocked on a toggle)
            bool directPress = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            bool blockedPress = EnterPressedWhileBlocked;

            if (directPress)
            {
                ConsumeKey(KeyCode.Return);
                ConsumeKey(KeyCode.KeypadEnter);
                return true;
            }

            if (blockedPress)
            {
                // Mark as handled so BaseNavigator.HandleInput doesn't also process it
                MarkEnterHandled();
                return true;
            }

            return false;
        }

        private readonly IShortcutRegistry _shortcuts;
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

        public InputManager(IShortcutRegistry shortcuts, IAnnouncementService announcer)
        {
            _shortcuts = shortcuts;
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
        }

        public void OnGameNavigatePrevious()
        {
            OnNavigatePrevious?.Invoke();
        }

        public void OnGameAccept()
        {
            OnAccept?.Invoke();
        }

        public void OnGameCancel()
        {
            OnCancel?.Invoke();
            _announcer.Silence();
        }
    }
}
