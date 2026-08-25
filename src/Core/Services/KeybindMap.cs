using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Pure keybind logic: the default table, the user's overrides, validation
    /// (reserved keys, conflicts, pairing rules) and (de)serialization of the
    /// overrides file. No input reading and no file IO — that lives in
    /// <see cref="Keybinds"/> — so this class is fully unit-testable.
    ///
    /// Conflict model: one flat namespace. A shift-paired action (e.g. Graveyard,
    /// whose Shift variant is the opponent graveyard) occupies both its chord and
    /// the chord's Shift variant.
    /// </summary>
    public class KeybindMap
    {
        public class Definition
        {
            public KeybindAction Action;
            public KeybindCategory Category;
            public KeyChord Default;
            /// <summary>Shift+key is the paired variant (opponent zone / locked
            /// full control), so Shift can't be part of the binding itself.</summary>
            public bool HasShiftVariant;

            public Definition(KeybindAction action, KeybindCategory category, KeyChord def, bool shiftVariant)
            {
                Action = action;
                Category = category;
                Default = def;
                HasShiftVariant = shiftVariant;
            }
        }

        private static KeyChord Plain(KeyCode k) => new KeyChord(k);
        private static KeyChord CtrlChord(KeyCode k) => new KeyChord(k, ctrl: true);

        /// <summary>Definition table in menu order (order inside a category is the
        /// order the keybind menu lists them).</summary>
        public static readonly Definition[] Definitions =
        {
            new Definition(KeybindAction.Help,               KeybindCategory.Global,      Plain(KeyCode.F1), false),
            new Definition(KeybindAction.CurrentScreen,      KeybindCategory.Global,      Plain(KeyCode.F3), false),
            new Definition(KeybindAction.FriendsChat,        KeybindCategory.Global,      Plain(KeyCode.F4), false),
            new Definition(KeybindAction.Update,             KeybindCategory.Global,      Plain(KeyCode.F5), false),
            new Definition(KeybindAction.RepeatAnnouncement, KeybindCategory.Global,      CtrlChord(KeyCode.R), false),
            new Definition(KeybindAction.TutorialHint,       KeybindCategory.Global,      CtrlChord(KeyCode.F1), false),
            new Definition(KeybindAction.CopyAnnouncement,   KeybindCategory.Global,      CtrlChord(KeyCode.RightArrow), false),

            new Definition(KeybindAction.Hand,               KeybindCategory.DuelZones,   Plain(KeyCode.C), true),
            new Definition(KeybindAction.Graveyard,          KeybindCategory.DuelZones,   Plain(KeyCode.G), true),
            new Definition(KeybindAction.Exile,              KeybindCategory.DuelZones,   Plain(KeyCode.X), true),
            new Definition(KeybindAction.StackZone,          KeybindCategory.DuelZones,   Plain(KeyCode.S), false),
            new Definition(KeybindAction.CommandZone,        KeybindCategory.DuelZones,   Plain(KeyCode.W), true),
            new Definition(KeybindAction.Library,            KeybindCategory.DuelZones,   Plain(KeyCode.D), true),

            new Definition(KeybindAction.Creatures,          KeybindCategory.Battlefield, Plain(KeyCode.B), true),
            new Definition(KeybindAction.Lands,              KeybindCategory.Battlefield, Plain(KeyCode.A), true),
            new Definition(KeybindAction.NonCreatures,       KeybindCategory.Battlefield, Plain(KeyCode.R), true),

            new Definition(KeybindAction.TurnInfo,           KeybindCategory.DuelInfo,    Plain(KeyCode.T), false),
            new Definition(KeybindAction.LifeTotals,         KeybindCategory.DuelInfo,    Plain(KeyCode.L), false),
            new Definition(KeybindAction.PlayerInfo,         KeybindCategory.DuelInfo,    Plain(KeyCode.V), false),
            new Definition(KeybindAction.ExtendedInfo,       KeybindCategory.DuelInfo,    Plain(KeyCode.I), false),
            new Definition(KeybindAction.CounterInfo,        KeybindCategory.DuelInfo,    Plain(KeyCode.K), false),
            new Definition(KeybindAction.JumpRelated,        KeybindCategory.DuelInfo,    Plain(KeyCode.J), false),
            new Definition(KeybindAction.LandSummary,        KeybindCategory.DuelInfo,    Plain(KeyCode.M), true),
            new Definition(KeybindAction.GameLog,            KeybindCategory.DuelInfo,    Plain(KeyCode.O), false),
            new Definition(KeybindAction.StackTop,           KeybindCategory.DuelInfo,    Plain(KeyCode.N), false),
            new Definition(KeybindAction.Timer,              KeybindCategory.DuelInfo,    Plain(KeyCode.E), true),
            new Definition(KeybindAction.FullControl,        KeybindCategory.DuelInfo,    Plain(KeyCode.P), true),
        };

        /// <summary>
        /// Base keys that can never be assigned: the mod's navigation primitives,
        /// the digits (phase stops, collection filters, mana picker), F2 (the
        /// settings recovery hatch) and the debug F-keys.
        /// </summary>
        private static readonly HashSet<KeyCode> ReservedNavigationKeys = new HashSet<KeyCode>
        {
            KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
            KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Space, KeyCode.Escape,
            KeyCode.Backspace, KeyCode.Tab, KeyCode.Home, KeyCode.End,
            KeyCode.PageUp, KeyCode.PageDown, KeyCode.Insert, KeyCode.Delete,
            KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
            KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9,
            KeyCode.F2, KeyCode.F11, KeyCode.F12
        };

        /// <summary>Keys the game itself acts on during duels (Y = undo, Q Q = float
        /// all mana). Binding over them would double-trigger game actions.</summary>
        private static readonly HashSet<KeyCode> ReservedGameKeys = new HashSet<KeyCode>
        {
            KeyCode.Y, KeyCode.Q
        };

        // Current bindings: action -> chord. Always fully populated.
        private readonly Dictionary<KeybindAction, KeyChord> _current =
            new Dictionary<KeybindAction, KeyChord>();

        private static readonly Dictionary<KeybindAction, Definition> _byAction = BuildIndex();

        private static Dictionary<KeybindAction, Definition> BuildIndex()
        {
            var index = new Dictionary<KeybindAction, Definition>();
            foreach (var def in Definitions)
                index[def.Action] = def;
            return index;
        }

        public KeybindMap()
        {
            ResetAll();
        }

        public static Definition GetDefinition(KeybindAction action) => _byAction[action];

        public KeyChord GetChord(KeybindAction action) => _current[action];

        public bool IsCustomized(KeybindAction action) =>
            !_current[action].Equals(_byAction[action].Default);

        public bool AnyCustomized
        {
            get
            {
                foreach (var def in Definitions)
                    if (IsCustomized(def.Action))
                        return true;
                return false;
            }
        }

        /// <summary>Actions of a category, in definition (menu) order.</summary>
        public static List<KeybindAction> ActionsInCategory(KeybindCategory category)
        {
            var list = new List<KeybindAction>();
            foreach (var def in Definitions)
                if (def.Category == category)
                    list.Add(def.Action);
            return list;
        }

        /// <summary>True when the base key can be offered for assignment at all.</summary>
        public static bool IsAssignableBaseKey(KeyCode key)
        {
            if (ReservedNavigationKeys.Contains(key) || ReservedGameKeys.Contains(key))
                return false;
            return (key >= KeyCode.A && key <= KeyCode.Z)
                || (key >= KeyCode.F1 && key <= KeyCode.F10)
                || (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9);
        }

        public static bool IsReservedNavigationKey(KeyCode key) => ReservedNavigationKeys.Contains(key);
        public static bool IsReservedGameKey(KeyCode key) => ReservedGameKeys.Contains(key);

        private static bool IsLetter(KeyCode key) => key >= KeyCode.A && key <= KeyCode.Z;
        private static bool IsFunctionKey(KeyCode key) => key >= KeyCode.F1 && key <= KeyCode.F15;

        /// <summary>
        /// Chords a binding occupies: the chord itself plus, for shift-paired
        /// actions, its Shift variant.
        /// </summary>
        private static IEnumerable<KeyChord> OccupiedChords(Definition def, KeyChord chord)
        {
            if (!chord.IsBound)
                yield break;
            yield return chord;
            if (def.HasShiftVariant)
                yield return chord.WithShift();
        }

        /// <summary>
        /// Validates assigning <paramref name="chord"/> to <paramref name="action"/>.
        /// On <see cref="KeybindValidation.Conflict"/>, <paramref name="conflict"/>
        /// names the action currently holding the chord.
        /// </summary>
        public KeybindValidation Validate(KeybindAction action, KeyChord chord, out KeybindAction conflict)
        {
            conflict = default;
            var def = _byAction[action];

            if (chord.Equals(_current[action]))
                return KeybindValidation.SameAsCurrent;

            // An action's own default always passes the per-chord rules (the copy
            // shortcut's default Ctrl+RightArrow uses a base key nothing else may
            // take), but still goes through the conflict scan below.
            if (!chord.Equals(def.Default))
            {
                var ruleResult = ValidateChordRules(def, chord);
                if (ruleResult != KeybindValidation.Ok)
                    return ruleResult;
            }

            // Conflict scan against every other action's occupied chords
            foreach (var mine in OccupiedChords(def, chord))
            {
                foreach (var other in Definitions)
                {
                    if (other.Action == action) continue;
                    foreach (var theirs in OccupiedChords(other, _current[other.Action]))
                    {
                        if (mine.Equals(theirs))
                        {
                            conflict = other.Action;
                            return KeybindValidation.Conflict;
                        }
                    }
                }
            }

            return KeybindValidation.Ok;
        }

        /// <summary>
        /// Applies a chord that has passed validation (or a conflict the user
        /// confirmed). Returns the action that lost its key, if any.
        /// </summary>
        public KeybindAction? Apply(KeybindAction action, KeyChord chord)
        {
            KeybindAction? stolen = null;
            if (Validate(action, chord, out var conflict) == KeybindValidation.Conflict)
            {
                _current[conflict] = KeyChord.Unbound;
                stolen = conflict;
            }
            _current[action] = chord;
            return stolen;
        }

        /// <summary>Resets one action to its default chord. Steals the default's key
        /// back if another action has taken it. Returns the action that lost its key.</summary>
        public KeybindAction? Reset(KeybindAction action) =>
            Apply(action, _byAction[action].Default);

        public void ResetAll()
        {
            foreach (var def in Definitions)
                _current[def.Action] = def.Default;
        }

        #region Serialization (overrides only)

        /// <summary>JSON with one entry per customized action; "" means unbound.</summary>
        public string SerializeOverrides()
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            bool first = true;
            foreach (var def in Definitions)
            {
                if (!IsCustomized(def.Action)) continue;
                if (!first) sb.Append(",\n");
                first = false;
                sb.Append($"  \"{def.Action}\": \"{_current[def.Action].ToSerial()}\"");
            }
            sb.Append("\n}");
            return sb.ToString();
        }

        /// <summary>
        /// Loads overrides from JSON. Two phases, because entries reference each
        /// other: a file that moves a key from one action to another must not
        /// self-conflict just because of load order. Per-chord rules (reserved
        /// keys, pairing, global restrictions) fall back to the action's default;
        /// then a single ordered conflict sweep unbinds any later action whose
        /// chord collides with an earlier one. Files written by the in-game menu
        /// are always conflict-free and load unchanged; only hand-edited files
        /// can lose entries, each with a log line.
        /// </summary>
        public void LoadOverrides(string json)
        {
            ResetAll();
            if (string.IsNullOrEmpty(json))
                return;

            // Phase 1: parse and apply per-chord rules (order-independent).
            foreach (var def in Definitions)
            {
                string serial = ReadJsonString(json, def.Action.ToString());
                if (serial == null)
                    continue; // not customized

                if (!KeyChord.TryParse(serial, out var chord))
                {
                    Log.Warn("KeybindMap", $"Ignoring unparsable binding for {def.Action}: '{serial}'");
                    continue;
                }

                if (!chord.IsBound || chord.Equals(def.Default))
                {
                    _current[def.Action] = chord;
                    continue;
                }

                var ruleResult = ValidateChordRules(def, chord);
                if (ruleResult != KeybindValidation.Ok)
                {
                    Log.Warn("KeybindMap", $"Ignoring invalid binding for {def.Action}: '{serial}' ({ruleResult})");
                    continue;
                }

                _current[def.Action] = chord;
            }

            // Phase 2: ordered conflict sweep — first definition wins, later
            // colliding actions become unbound.
            var occupied = new Dictionary<KeyChord, KeybindAction>();
            foreach (var def in Definitions)
            {
                var chord = _current[def.Action];
                bool collides = false;
                foreach (var c in OccupiedChords(def, chord))
                {
                    if (occupied.TryGetValue(c, out var holder))
                    {
                        Log.Warn("KeybindMap",
                            $"Binding conflict in keybinds file: {def.Action} ({chord.ToSerial()}) collides with {holder}; {def.Action} is now unbound");
                        collides = true;
                        break;
                    }
                }

                if (collides)
                {
                    _current[def.Action] = KeyChord.Unbound;
                    continue;
                }

                foreach (var c in OccupiedChords(def, chord))
                    occupied[c] = def.Action;
            }
        }

        /// <summary>
        /// Order-independent per-chord rules shared by Validate and the file loader.
        /// Global shortcuts fire on every screen: a plain or shifted letter would
        /// collide with first-letter menu jumping, and letters and numpad keys type
        /// into text fields — hence the Ctrl-or-function-key requirement.
        /// </summary>
        private static KeybindValidation ValidateChordRules(Definition def, KeyChord chord)
        {
            if (chord.Ctrl && chord.Shift)
                return KeybindValidation.TwoModifiers;
            if (ReservedGameKeys.Contains(chord.Key))
                return KeybindValidation.ReservedGameKey;
            if (!IsAssignableBaseKey(chord.Key))
                return KeybindValidation.ReservedNavigation;
            if (def.HasShiftVariant && chord.Shift)
                return KeybindValidation.ShiftReservedForVariant;
            if (def.Category == KeybindCategory.Global && !chord.Ctrl && !IsFunctionKey(chord.Key))
                return KeybindValidation.GlobalNeedsCtrlOrFunctionKey;
            return KeybindValidation.Ok;
        }

        private static string ReadJsonString(string json, string key)
        {
            string pattern = $"\"{key}\"";
            int keyIndex = json.IndexOf(pattern, StringComparison.Ordinal);
            if (keyIndex < 0) return null;

            int colonIndex = json.IndexOf(':', keyIndex + pattern.Length);
            if (colonIndex < 0) return null;

            int startQuote = json.IndexOf('"', colonIndex + 1);
            if (startQuote < 0) return null;

            int endQuote = json.IndexOf('"', startQuote + 1);
            if (endQuote < 0) return null;

            return json.Substring(startQuote + 1, endQuote - startQuote - 1);
        }

        #endregion
    }
}
