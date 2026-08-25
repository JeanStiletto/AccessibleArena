using System;
using System.IO;
using UnityEngine;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Runtime facade over <see cref="KeybindMap"/>: loads/saves the user's
    /// overrides (own file, separate from the settings, so it survives a settings
    /// reset and can be backed up or shared as a profile) and answers the per-frame
    /// "is this action's key down" questions the navigators ask.
    /// </summary>
    public static class Keybinds
    {
        private static readonly string FilePath = Path.Combine("UserData", "AccessibleArenaKeybinds.json");

        public static KeybindMap Map { get; } = new KeybindMap();

        /// <summary>Fired after a binding changes (rebind, reset, reset-all).
        /// AccessibleArenaMod re-registers the global shortcuts off this.</summary>
        public static event Action Changed;

        /// <summary>Duel actions whose base-key press releases focus of the ChooseX /
        /// Spinner sub-navigators (parity with the old hardcoded letter list).</summary>
        private static readonly KeybindAction[] ZoneFocusReleaseActions =
        {
            KeybindAction.Hand, KeybindAction.Graveyard, KeybindAction.Exile,
            KeybindAction.StackZone, KeybindAction.CommandZone, KeybindAction.Library,
            KeybindAction.Creatures, KeybindAction.Lands, KeybindAction.NonCreatures,
            KeybindAction.LifeTotals, KeybindAction.PlayerInfo
        };

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    Log.Msg("Keybinds", "No keybinds file found, using defaults");
                    return;
                }

                Map.LoadOverrides(File.ReadAllText(FilePath));
                Log.Msg("Keybinds", $"Loaded keybinds (customized: {Map.AnyCustomized})");
            }
            catch (Exception ex)
            {
                Log.Warn("Keybinds", $"Failed to load keybinds, using defaults: {ex.Message}");
            }
        }

        public static void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(FilePath, Map.SerializeOverrides());
                Log.Msg("Keybinds", "Keybinds saved");
            }
            catch (Exception ex)
            {
                Log.Warn("Keybinds", $"Failed to save keybinds: {ex.Message}");
            }
        }

        /// <summary>Saves and notifies listeners. Call after every applied change so
        /// a crash can't lose a rebind.</summary>
        public static void SaveAndNotify()
        {
            Save();
            Changed?.Invoke();
        }

        public static KeyChord GetChord(KeybindAction action) => Map.GetChord(action);

        private static bool CtrlHeld =>
            KeyInput.GetKey(KeyCode.LeftControl) || KeyInput.GetKey(KeyCode.RightControl);
        private static bool ShiftHeld =>
            KeyInput.GetKey(KeyCode.LeftShift) || KeyInput.GetKey(KeyCode.RightShift);
        private static bool AltHeld =>
            KeyInput.GetKey(KeyCode.LeftAlt) || KeyInput.GetKey(KeyCode.RightAlt);

        /// <summary>
        /// Exact-chord press: base key went down this frame, Ctrl and Shift states
        /// match the binding, Alt not held (Alt combos belong to the game).
        /// Use for actions without a Shift-paired variant.
        /// </summary>
        public static bool Down(KeybindAction action)
        {
            var chord = Map.GetChord(action);
            if (!chord.IsBound) return false;
            if (!KeyInput.GetKeyDown(chord.Key)) return false;
            return CtrlHeld == chord.Ctrl && ShiftHeld == chord.Shift && !AltHeld;
        }

        /// <summary>
        /// Shift-agnostic press for shift-paired actions (opponent zones, locked
        /// full control): base key went down, Ctrl state matches; the call site
        /// branches on Shift itself.
        /// </summary>
        public static bool DownAny(KeybindAction action)
        {
            var chord = Map.GetChord(action);
            if (!chord.IsBound) return false;
            if (!KeyInput.GetKeyDown(chord.Key)) return false;
            return CtrlHeld == chord.Ctrl;
        }

        /// <summary>
        /// Text-input-safe variant of <see cref="Down"/> for handlers that run while
        /// the user is typing (duel chat, input fields): only fires when the chord
        /// cannot produce a character (Ctrl chord or function key).
        /// </summary>
        public static bool DownInTextInput(KeybindAction action)
        {
            var chord = Map.GetChord(action);
            if (!chord.IsBound) return false;
            bool nonTyping = chord.Ctrl || (chord.Key >= KeyCode.F1 && chord.Key <= KeyCode.F15);
            return nonTyping && Down(action);
        }

        /// <summary>Any zone/battlefield/info base key down this frame — the ChooseX
        /// and Spinner focus-release check.</summary>
        public static bool AnyZoneFocusReleaseKeyDown()
        {
            foreach (var action in ZoneFocusReleaseActions)
            {
                var chord = Map.GetChord(action);
                if (chord.IsBound && KeyInput.GetKeyDown(chord.Key))
                    return true;
            }
            return false;
        }
    }
}
