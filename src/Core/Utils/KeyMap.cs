using UnityEngine;
using UnityEngine.InputSystem;

namespace AccessibleArena.Core.Utils
{
    /// <summary>
    /// Maps Input System physical <see cref="Key"/> codes to the legacy-style
    /// <see cref="KeyCode"/> values the mod uses internally. Needed at the game
    /// boundary: since the Unity 6 update the game's KeyboardManager publishes
    /// physical Key values, while the mod's own state (consumed keys, shortcut
    /// tables) is KeyCode-based.
    ///
    /// Letters translate by the character the key types on the current layout
    /// (physical Key.Z on QWERTZ types 'y' → KeyCode.Y), mirroring
    /// InputSystemKeyboardBackend's forward mapping. Unmapped keys → KeyCode.None.
    /// </summary>
    public static class KeyMap
    {
        public static KeyCode ToKeyCode(Key key)
        {
            if (key >= Key.A && key <= Key.Z)
                return LetterToKeyCode(key);

            switch (key)
            {
                case Key.Enter: return KeyCode.Return;
                case Key.NumpadEnter: return KeyCode.KeypadEnter;
                case Key.Space: return KeyCode.Space;
                case Key.Backspace: return KeyCode.Backspace;
                case Key.Escape: return KeyCode.Escape;
                case Key.Tab: return KeyCode.Tab;
                case Key.UpArrow: return KeyCode.UpArrow;
                case Key.DownArrow: return KeyCode.DownArrow;
                case Key.LeftArrow: return KeyCode.LeftArrow;
                case Key.RightArrow: return KeyCode.RightArrow;
                case Key.Home: return KeyCode.Home;
                case Key.End: return KeyCode.End;
                case Key.PageUp: return KeyCode.PageUp;
                case Key.PageDown: return KeyCode.PageDown;
                case Key.Insert: return KeyCode.Insert;
                case Key.Delete: return KeyCode.Delete;
                case Key.LeftShift: return KeyCode.LeftShift;
                case Key.RightShift: return KeyCode.RightShift;
                case Key.LeftCtrl: return KeyCode.LeftControl;
                case Key.RightCtrl: return KeyCode.RightControl;
                case Key.LeftAlt: return KeyCode.LeftAlt;
                case Key.RightAlt: return KeyCode.RightAlt;
                case Key.Digit0: return KeyCode.Alpha0;
                case Key.Digit1: return KeyCode.Alpha1;
                case Key.Digit2: return KeyCode.Alpha2;
                case Key.Digit3: return KeyCode.Alpha3;
                case Key.Digit4: return KeyCode.Alpha4;
                case Key.Digit5: return KeyCode.Alpha5;
                case Key.Digit6: return KeyCode.Alpha6;
                case Key.Digit7: return KeyCode.Alpha7;
                case Key.Digit8: return KeyCode.Alpha8;
                case Key.Digit9: return KeyCode.Alpha9;
                case Key.Numpad0: return KeyCode.Keypad0;
                case Key.Numpad1: return KeyCode.Keypad1;
                case Key.Numpad2: return KeyCode.Keypad2;
                case Key.Numpad3: return KeyCode.Keypad3;
                case Key.Numpad4: return KeyCode.Keypad4;
                case Key.Numpad5: return KeyCode.Keypad5;
                case Key.Numpad6: return KeyCode.Keypad6;
                case Key.Numpad7: return KeyCode.Keypad7;
                case Key.Numpad8: return KeyCode.Keypad8;
                case Key.Numpad9: return KeyCode.Keypad9;
                case Key.F1: return KeyCode.F1;
                case Key.F2: return KeyCode.F2;
                case Key.F3: return KeyCode.F3;
                case Key.F4: return KeyCode.F4;
                case Key.F5: return KeyCode.F5;
                case Key.F6: return KeyCode.F6;
                case Key.F7: return KeyCode.F7;
                case Key.F8: return KeyCode.F8;
                case Key.F9: return KeyCode.F9;
                case Key.F10: return KeyCode.F10;
                case Key.F11: return KeyCode.F11;
                case Key.F12: return KeyCode.F12;
                default: return KeyCode.None;
            }
        }

        /// <summary>
        /// Reverse direction: the physical Key that means this KeyCode, for calling
        /// into game methods that take Key (e.g. simulated HandleKeyDown invokes).
        /// Letters resolve layout-aware; unmapped codes → Key.None.
        /// </summary>
        public static Key ToKey(KeyCode keyCode)
        {
            if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    char c = (char)('a' + (keyCode - KeyCode.A));
                    try
                    {
                        var control = keyboard.FindKeyOnCurrentKeyboardLayout(c.ToString());
                        if (control != null)
                            return control.keyCode;
                    }
                    catch { /* fall through to physical */ }
                }
                return Key.A + (keyCode - KeyCode.A);
            }

            switch (keyCode)
            {
                case KeyCode.Return: return Key.Enter;
                case KeyCode.KeypadEnter: return Key.NumpadEnter;
                case KeyCode.Space: return Key.Space;
                case KeyCode.Backspace: return Key.Backspace;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.Tab: return Key.Tab;
                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;
                case KeyCode.Home: return Key.Home;
                case KeyCode.End: return Key.End;
                case KeyCode.PageUp: return Key.PageUp;
                case KeyCode.PageDown: return Key.PageDown;
                case KeyCode.Insert: return Key.Insert;
                case KeyCode.Delete: return Key.Delete;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.RightShift: return Key.RightShift;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.RightControl: return Key.RightCtrl;
                case KeyCode.LeftAlt: return Key.LeftAlt;
                case KeyCode.RightAlt: return Key.RightAlt;
                case KeyCode.Alpha0: return Key.Digit0;
                case KeyCode.Alpha1: return Key.Digit1;
                case KeyCode.Alpha2: return Key.Digit2;
                case KeyCode.Alpha3: return Key.Digit3;
                case KeyCode.Alpha4: return Key.Digit4;
                case KeyCode.Alpha5: return Key.Digit5;
                case KeyCode.Alpha6: return Key.Digit6;
                case KeyCode.Alpha7: return Key.Digit7;
                case KeyCode.Alpha8: return Key.Digit8;
                case KeyCode.Alpha9: return Key.Digit9;
                case KeyCode.Keypad0: return Key.Numpad0;
                case KeyCode.Keypad1: return Key.Numpad1;
                case KeyCode.Keypad2: return Key.Numpad2;
                case KeyCode.Keypad3: return Key.Numpad3;
                case KeyCode.Keypad4: return Key.Numpad4;
                case KeyCode.Keypad5: return Key.Numpad5;
                case KeyCode.Keypad6: return Key.Numpad6;
                case KeyCode.Keypad7: return Key.Numpad7;
                case KeyCode.Keypad8: return Key.Numpad8;
                case KeyCode.Keypad9: return Key.Numpad9;
                case KeyCode.F1: return Key.F1;
                case KeyCode.F2: return Key.F2;
                case KeyCode.F3: return Key.F3;
                case KeyCode.F4: return Key.F4;
                case KeyCode.F5: return Key.F5;
                case KeyCode.F6: return Key.F6;
                case KeyCode.F7: return Key.F7;
                case KeyCode.F8: return Key.F8;
                case KeyCode.F9: return Key.F9;
                case KeyCode.F10: return Key.F10;
                case KeyCode.F11: return Key.F11;
                case KeyCode.F12: return Key.F12;
                default: return Key.None;
            }
        }

        private static KeyCode LetterToKeyCode(Key key)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                string typed = keyboard[key].displayName;
                if (!string.IsNullOrEmpty(typed))
                {
                    char c = char.ToLowerInvariant(typed[0]);
                    if (c >= 'a' && c <= 'z')
                        return KeyCode.A + (c - 'a');
                }
            }
            // No keyboard or non-letter label — fall back to the physical position
            return KeyCode.A + (key - Key.A);
        }
    }
}
