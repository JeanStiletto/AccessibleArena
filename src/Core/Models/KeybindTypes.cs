using System;
using UnityEngine;

namespace AccessibleArena.Core.Models
{
    /// <summary>
    /// Every mod shortcut the user can rebind. Navigation primitives (arrows,
    /// Enter, Space, Backspace, Escape, Tab, Home/End, page keys, digits) and
    /// the F2 settings key are deliberately NOT in this list — they can neither
    /// be rebound nor assigned, so the user can never lock themselves out of
    /// basic navigation, and F2 always remains as the recovery hatch.
    /// </summary>
    public enum KeybindAction
    {
        // Global
        Help,
        CurrentScreen,
        FriendsChat,
        Update,
        RepeatAnnouncement,
        TutorialHint,
        CopyAnnouncement,

        // Duel zones
        Hand,
        Graveyard,
        Exile,
        StackZone,
        CommandZone,
        Library,

        // Battlefield rows
        Creatures,
        Lands,
        NonCreatures,

        // Duel information
        TurnInfo,
        LifeTotals,
        PlayerInfo,
        ExtendedInfo,
        CounterInfo,
        JumpRelated,
        LandSummary,
        GameLog,
        StackTop,
        Timer,
        FullControl
    }

    /// <summary>Keybind menu categories, mirroring the F1 help structure.</summary>
    public enum KeybindCategory
    {
        Global,
        DuelZones,
        Battlefield,
        DuelInfo
    }

    /// <summary>
    /// A key plus optional single modifier (Ctrl or Shift, never both — the
    /// ShortcutRegistry dispatch only supports one modifier, and single
    /// modifiers keep the spoken UI simple).
    /// </summary>
    public struct KeyChord : IEquatable<KeyChord>
    {
        public KeyCode Key;
        public bool Ctrl;
        public bool Shift;

        public KeyChord(KeyCode key, bool ctrl = false, bool shift = false)
        {
            Key = key;
            Ctrl = ctrl;
            Shift = shift;
        }

        public static KeyChord Unbound => new KeyChord(KeyCode.None);

        public bool IsBound => Key != KeyCode.None;

        /// <summary>The Shift variant of this chord (opponent/lock pairing).</summary>
        public KeyChord WithShift() => new KeyChord(Key, Ctrl, true);

        public bool Equals(KeyChord other) =>
            Key == other.Key && Ctrl == other.Ctrl && Shift == other.Shift;

        public override bool Equals(object obj) => obj is KeyChord other && Equals(other);

        public override int GetHashCode() =>
            ((int)Key * 4) + (Ctrl ? 2 : 0) + (Shift ? 1 : 0);

        /// <summary>
        /// Invariant serial form for the keybinds JSON: "G", "Ctrl+G", "Shift+F4",
        /// "" for unbound. Not user-facing (speech uses Strings.KeyChordLabel).
        /// </summary>
        public string ToSerial()
        {
            if (!IsBound) return "";
            string prefix = Ctrl ? "Ctrl+" : Shift ? "Shift+" : "";
            return prefix + Key;
        }

        /// <summary>Parses the ToSerial format. Returns false on unknown key names.</summary>
        public static bool TryParse(string serial, out KeyChord chord)
        {
            chord = Unbound;
            if (string.IsNullOrEmpty(serial))
                return true; // explicit unbound

            bool ctrl = false, shift = false;
            string keyPart = serial;
            if (keyPart.StartsWith("Ctrl+", StringComparison.OrdinalIgnoreCase))
            {
                ctrl = true;
                keyPart = keyPart.Substring(5);
            }
            else if (keyPart.StartsWith("Shift+", StringComparison.OrdinalIgnoreCase))
            {
                shift = true;
                keyPart = keyPart.Substring(6);
            }

            KeyCode key;
            try
            {
                key = (KeyCode)Enum.Parse(typeof(KeyCode), keyPart, ignoreCase: true);
            }
            catch
            {
                return false;
            }

            if (key == KeyCode.None)
                return false;

            chord = new KeyChord(key, ctrl, shift);
            return true;
        }
    }

    /// <summary>Outcome of validating a chord for an action.</summary>
    public enum KeybindValidation
    {
        Ok,
        /// <summary>Chord equals the action's current binding.</summary>
        SameAsCurrent,
        /// <summary>Base key is a protected navigation primitive.</summary>
        ReservedNavigation,
        /// <summary>Base key belongs to the game itself (Y undo, Q float mana).</summary>
        ReservedGameKey,
        /// <summary>Global actions may not use plain or shifted letters/numpad keys
        /// (menus jump by letter; text fields type them).</summary>
        GlobalNeedsCtrlOrFunctionKey,
        /// <summary>Ctrl and Shift together are not supported.</summary>
        TwoModifiers,
        /// <summary>Shift chord requested on an action whose Shift variant is the
        /// opponent/lock pairing.</summary>
        ShiftReservedForVariant,
        /// <summary>Chord collides with another action (see conflict out param).</summary>
        Conflict
    }
}
