using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using AccessibleArena.Core.Interfaces;

namespace AccessibleArena.Core.Utils
{
    /// <summary>
    /// Keyboard backend on the Input System package. The game's Unity 6 build ships
    /// with legacy input handling disabled, so every UnityEngine.Input call throws;
    /// all key reads go through this class instead (via <see cref="KeyInput"/>).
    ///
    /// Letter keys resolve by the character they type on the current OS layout,
    /// which is how the legacy Input class behaved on Windows: on a German QWERTZ
    /// layout the key that types 'z' is KeyCode.Z regardless of physical position.
    /// All other keys map to their fixed physical control. The letter map is
    /// rebuilt when the OS reports a keyboard configuration (layout) change.
    /// </summary>
    public class InputSystemKeyboardBackend : IKeyboardBackend
    {
        private readonly Dictionary<KeyCode, KeyControl> _controls = new Dictionary<KeyCode, KeyControl>();
        private readonly HashSet<KeyCode> _warned = new HashSet<KeyCode>();
        private Keyboard _keyboard;
        private bool _deviceChangeHooked;

        // Characters typed in the frame they arrived in (legacy Input.inputString semantics)
        private readonly StringBuilder _typed = new StringBuilder();
        private int _typedFrame = -1;

        public bool IsPressed(KeyCode key)
        {
            var control = Resolve(key);
            return control != null && control.isPressed;
        }

        public bool WasPressedThisFrame(KeyCode key)
        {
            var control = Resolve(key);
            return control != null && control.wasPressedThisFrame;
        }

        public bool AnyKeyDownThisFrame
        {
            get
            {
                var keyboard = CurrentKeyboard();
                return keyboard != null && keyboard.anyKey.wasPressedThisFrame;
            }
        }

        public string TextThisFrame
        {
            get
            {
                CurrentKeyboard(); // keep the onTextInput subscription on the live device
                return _typedFrame == Time.frameCount ? _typed.ToString() : string.Empty;
            }
        }

        private Keyboard CurrentKeyboard()
        {
            var keyboard = Keyboard.current;
            if (!ReferenceEquals(keyboard, _keyboard))
            {
                if (_keyboard != null)
                    _keyboard.onTextInput -= OnTextInput;
                _keyboard = keyboard;
                _controls.Clear();
                if (keyboard != null)
                {
                    keyboard.onTextInput += OnTextInput;
                    Log.Msg("KeyInput", $"Keyboard device: {keyboard.displayName}");
                }
                if (!_deviceChangeHooked)
                {
                    InputSystem.onDeviceChange += OnDeviceChange;
                    _deviceChangeHooked = true;
                }
            }
            return keyboard;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            // ConfigurationChanged fires when the OS switches keyboard layout;
            // letter keys must re-resolve against the new layout.
            if (device is Keyboard && change == InputDeviceChange.ConfigurationChanged)
            {
                _controls.Clear();
                Log.Msg("KeyInput", "Keyboard configuration changed — key map reset");
            }
        }

        private void OnTextInput(char character)
        {
            int frame = Time.frameCount;
            if (frame != _typedFrame)
            {
                _typed.Length = 0;
                _typedFrame = frame;
            }
            _typed.Append(character);
        }

        private KeyControl Resolve(KeyCode key)
        {
            var keyboard = CurrentKeyboard();
            if (keyboard == null)
                return null;
            if (_controls.TryGetValue(key, out var control))
                return control;
            control = Map(keyboard, key);
            _controls[key] = control; // nulls cached too — no re-search per frame
            if (control == null && key != KeyCode.None && _warned.Add(key))
                Log.Msg("KeyInput", $"No Input System control mapped for {key}");
            return control;
        }

        private static KeyControl Map(Keyboard kb, KeyCode key)
        {
            if (key >= KeyCode.A && key <= KeyCode.Z)
                return MapLetter(kb, key);

            switch (key)
            {
                case KeyCode.Return: return kb.enterKey;
                case KeyCode.KeypadEnter: return kb.numpadEnterKey;
                case KeyCode.Space: return kb.spaceKey;
                case KeyCode.Backspace: return kb.backspaceKey;
                case KeyCode.Escape: return kb.escapeKey;
                case KeyCode.Tab: return kb.tabKey;
                case KeyCode.UpArrow: return kb.upArrowKey;
                case KeyCode.DownArrow: return kb.downArrowKey;
                case KeyCode.LeftArrow: return kb.leftArrowKey;
                case KeyCode.RightArrow: return kb.rightArrowKey;
                case KeyCode.Home: return kb.homeKey;
                case KeyCode.End: return kb.endKey;
                case KeyCode.PageUp: return kb.pageUpKey;
                case KeyCode.PageDown: return kb.pageDownKey;
                case KeyCode.Insert: return kb.insertKey;
                case KeyCode.Delete: return kb.deleteKey;
                case KeyCode.LeftShift: return kb.leftShiftKey;
                case KeyCode.RightShift: return kb.rightShiftKey;
                case KeyCode.LeftControl: return kb.leftCtrlKey;
                case KeyCode.RightControl: return kb.rightCtrlKey;
                case KeyCode.LeftAlt: return kb.leftAltKey;
                case KeyCode.RightAlt: return kb.rightAltKey;
                case KeyCode.Alpha0: return kb.digit0Key;
                case KeyCode.Alpha1: return kb.digit1Key;
                case KeyCode.Alpha2: return kb.digit2Key;
                case KeyCode.Alpha3: return kb.digit3Key;
                case KeyCode.Alpha4: return kb.digit4Key;
                case KeyCode.Alpha5: return kb.digit5Key;
                case KeyCode.Alpha6: return kb.digit6Key;
                case KeyCode.Alpha7: return kb.digit7Key;
                case KeyCode.Alpha8: return kb.digit8Key;
                case KeyCode.Alpha9: return kb.digit9Key;
                case KeyCode.Keypad0: return kb.numpad0Key;
                case KeyCode.Keypad1: return kb.numpad1Key;
                case KeyCode.Keypad2: return kb.numpad2Key;
                case KeyCode.Keypad3: return kb.numpad3Key;
                case KeyCode.Keypad4: return kb.numpad4Key;
                case KeyCode.Keypad5: return kb.numpad5Key;
                case KeyCode.Keypad6: return kb.numpad6Key;
                case KeyCode.Keypad7: return kb.numpad7Key;
                case KeyCode.Keypad8: return kb.numpad8Key;
                case KeyCode.Keypad9: return kb.numpad9Key;
                case KeyCode.F1: return kb.f1Key;
                case KeyCode.F2: return kb.f2Key;
                case KeyCode.F3: return kb.f3Key;
                case KeyCode.F4: return kb.f4Key;
                case KeyCode.F5: return kb.f5Key;
                case KeyCode.F6: return kb.f6Key;
                case KeyCode.F7: return kb.f7Key;
                case KeyCode.F8: return kb.f8Key;
                case KeyCode.F9: return kb.f9Key;
                case KeyCode.F10: return kb.f10Key;
                case KeyCode.F11: return kb.f11Key;
                case KeyCode.F12: return kb.f12Key;
                default: return null;
            }
        }

        private static KeyControl MapLetter(Keyboard kb, KeyCode key)
        {
            char lower = (char)('a' + (key - KeyCode.A));
            try
            {
                // By typed character first — legacy KeyCode semantics, layout-aware
                var control = kb.FindKeyOnCurrentKeyboardLayout(lower.ToString())
                           ?? kb.FindKeyOnCurrentKeyboardLayout(char.ToUpperInvariant(lower).ToString());
                if (control != null)
                    return control;
            }
            catch (Exception ex)
            {
                Log.Msg("KeyInput", $"Layout lookup for '{lower}' failed: {ex.Message}");
            }
            // Character not on this layout — fall back to the US-layout physical position
            return kb[(Key)((int)Key.A + (key - KeyCode.A))];
        }
    }
}
