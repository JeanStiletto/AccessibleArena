using System.Collections.Generic;
using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Utils;

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
        /// Set when a popup is being navigated in popup mode.
        /// Used by KeyboardManagerPatch to block Escape from reaching the game.
        /// </summary>
        public static bool PopupModeActive { get; set; }

        /// <summary>
        /// When true, the next Enter KeyUp event will be blocked from reaching the game.
        /// Used when our mod opens a popup via KeyDown (e.g., collection card → card viewer),
        /// to prevent the game's PopupManager.HandleKeyUp from calling OnEnter() on the
        /// newly opened popup, which would auto-trigger the craft button.
        /// Automatically resets after blocking one KeyUp.
        /// </summary>
        public static bool BlockNextEnterKeyUp { get; set; }

        /// <summary>
        /// When true, EventSystemPatch blocks Unity's Submit events.
        /// Set by navigators when the current element is a toggle or dropdown,
        /// cleared when moving to other element types.
        /// This persistent flag works around the timing issue where EventSystem.Update()
        /// runs BEFORE our MonoBehaviour.Update() can consume keys.
        /// Without this, pressing Enter on a dropdown triggers SendSubmitEventToSelectedObject
        /// before our Update can open the dropdown and set ShouldBlockEnterFromGame.
        /// </summary>
        private static bool _blockSubmitForToggle;
        public static bool BlockSubmitForToggle
        {
            get => _blockSubmitForToggle;
            set
            {
                if (_blockSubmitForToggle != value)
                {
                    Log.Msg("InputManager", $"BlockSubmitForToggle changed: {_blockSubmitForToggle} -> {value}");
                    _blockSubmitForToggle = value;
                }
            }
        }

        /// <summary>
        /// When true, patches allow the game's native Enter handling on the Login scene.
        /// Set by navigators when the focused element is the RegistrationPanel submit button.
        /// This lets the game's own ActionSystem → Panel.OnAccept() handle the button click
        /// instead of our mod, to ensure the post-registration ConnectToFrontDoor flow works.
        /// </summary>
        public static bool AllowNativeEnterOnLogin { get; set; }

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
            Log.Msg("InputManager", $"Consumed key: {key}");
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
            if (KeyInput.GetKeyDown(key))
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
            if (KeyInput.GetKeyDown(KeyCode.Return) || KeyInput.GetKeyDown(KeyCode.KeypadEnter))
            {
                ConsumeKey(KeyCode.Return);
                ConsumeKey(KeyCode.KeypadEnter);
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
            KeyCode.P,  // Full control toggle (P / Shift+P for lock)
            KeyCode.M,  // Land summary (M / Shift+M for opponent)
            KeyCode.K,  // Counter info

            // Number keys for phase stops (duel) and filters (collection)
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
            KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0,

            // Function keys (safe)
            KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F12,

            // With modifiers
            KeyCode.R,  // Ctrl+R for repeat
        };

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
                if (KeyInput.GetKeyDown(key))
                {
                    ProcessCustomKey(key);
                }
            }
        }

        private void ProcessCustomKey(KeyCode key)
        {
            bool shift = KeyInput.GetKey(KeyCode.LeftShift) || KeyInput.GetKey(KeyCode.RightShift);
            bool ctrl = KeyInput.GetKey(KeyCode.LeftControl) || KeyInput.GetKey(KeyCode.RightControl);
            bool alt = KeyInput.GetKey(KeyCode.LeftAlt) || KeyInput.GetKey(KeyCode.RightAlt);

            // Skip if any modifier conflicts with game controls
            // Alt is used by game for alt-view
            if (alt && key != KeyCode.F4) // Allow Alt+F4
                return;

            _shortcuts.ProcessKey(key, shift, ctrl, alt);
        }

    }
}
