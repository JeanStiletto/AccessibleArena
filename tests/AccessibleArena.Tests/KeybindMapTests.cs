using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services;

namespace AccessibleArena.Tests
{
    [TestFixture]
    public class KeybindMapTests
    {
        private KeybindMap _map;

        [SetUp]
        public void SetUp()
        {
            _map = new KeybindMap();
        }

        [Test]
        public void Defaults_AreAllBound_AndConflictFree()
        {
            var occupied = new HashSet<KeyChord>();
            foreach (var def in KeybindMap.Definitions)
            {
                var chord = _map.GetChord(def.Action);
                Assert.That(chord.IsBound, Is.True, $"{def.Action} default should be bound");
                Assert.That(occupied.Add(chord), Is.True, $"{def.Action} default chord collides");
                if (def.HasShiftVariant)
                    Assert.That(occupied.Add(chord.WithShift()), Is.True,
                        $"{def.Action} shift variant collides");
            }
            Assert.That(_map.AnyCustomized, Is.False);
        }

        [Test]
        public void Validate_SimpleRebind_IsOk_AndApplies()
        {
            var result = _map.Validate(KeybindAction.Graveyard, new KeyChord(KeyCode.H), out _);
            Assert.That(result, Is.EqualTo(KeybindValidation.Ok));

            var stolen = _map.Apply(KeybindAction.Graveyard, new KeyChord(KeyCode.H));
            Assert.That(stolen, Is.Null);
            Assert.That(_map.GetChord(KeybindAction.Graveyard), Is.EqualTo(new KeyChord(KeyCode.H)));
            Assert.That(_map.IsCustomized(KeybindAction.Graveyard), Is.True);
            Assert.That(_map.AnyCustomized, Is.True);
        }

        [TestCase(KeyCode.Return)]
        [TestCase(KeyCode.Space)]
        [TestCase(KeyCode.UpArrow)]
        [TestCase(KeyCode.Backspace)]
        [TestCase(KeyCode.Escape)]
        [TestCase(KeyCode.Tab)]
        [TestCase(KeyCode.Alpha5)]
        [TestCase(KeyCode.F2)]
        [TestCase(KeyCode.Delete)]
        public void Validate_ReservedNavigationKeys_AreRejected(KeyCode key)
        {
            var result = _map.Validate(KeybindAction.Graveyard, new KeyChord(key), out _);
            Assert.That(result, Is.EqualTo(KeybindValidation.ReservedNavigation));
        }

        [TestCase(KeyCode.Y)]
        [TestCase(KeyCode.Q)]
        public void Validate_GameKeys_AreRejected(KeyCode key)
        {
            var result = _map.Validate(KeybindAction.Graveyard, new KeyChord(key), out _);
            Assert.That(result, Is.EqualTo(KeybindValidation.ReservedGameKey));
        }

        [Test]
        public void Validate_GlobalAction_RejectsPlainAndShiftedTypingKeys()
        {
            Assert.That(_map.Validate(KeybindAction.Help, new KeyChord(KeyCode.H), out _),
                Is.EqualTo(KeybindValidation.GlobalNeedsCtrlOrFunctionKey), "plain letter");
            Assert.That(_map.Validate(KeybindAction.Help, new KeyChord(KeyCode.H, shift: true), out _),
                Is.EqualTo(KeybindValidation.GlobalNeedsCtrlOrFunctionKey), "shifted letter");
            Assert.That(_map.Validate(KeybindAction.Help, new KeyChord(KeyCode.Keypad5), out _),
                Is.EqualTo(KeybindValidation.GlobalNeedsCtrlOrFunctionKey), "plain numpad");

            Assert.That(_map.Validate(KeybindAction.Help, new KeyChord(KeyCode.H, ctrl: true), out _),
                Is.EqualTo(KeybindValidation.Ok), "Ctrl+letter");
            Assert.That(_map.Validate(KeybindAction.Help, new KeyChord(KeyCode.F6), out _),
                Is.EqualTo(KeybindValidation.Ok), "function key");
        }

        [Test]
        public void Validate_DuelAction_AllowsPlainLetterAndNumpad()
        {
            Assert.That(_map.Validate(KeybindAction.Graveyard, new KeyChord(KeyCode.H), out _),
                Is.EqualTo(KeybindValidation.Ok));
            Assert.That(_map.Validate(KeybindAction.TurnInfo, new KeyChord(KeyCode.Keypad7), out _),
                Is.EqualTo(KeybindValidation.Ok));
        }

        [Test]
        public void Validate_TwoModifiers_IsRejected()
        {
            var result = _map.Validate(KeybindAction.TurnInfo,
                new KeyChord(KeyCode.H, ctrl: true, shift: true), out _);
            Assert.That(result, Is.EqualTo(KeybindValidation.TwoModifiers));
        }

        [Test]
        public void Validate_ShiftChord_OnShiftPairedAction_IsRejected()
        {
            var result = _map.Validate(KeybindAction.Graveyard,
                new KeyChord(KeyCode.H, shift: true), out _);
            Assert.That(result, Is.EqualTo(KeybindValidation.ShiftReservedForVariant));
        }

        [Test]
        public void Validate_SameAsCurrent_IsReported()
        {
            var result = _map.Validate(KeybindAction.Graveyard, new KeyChord(KeyCode.G), out _);
            Assert.That(result, Is.EqualTo(KeybindValidation.SameAsCurrent));
        }

        [Test]
        public void Validate_Conflict_NamesTheHolder_AndApplyStealsIt()
        {
            var result = _map.Validate(KeybindAction.Timer, new KeyChord(KeyCode.G), out var conflict);
            Assert.That(result, Is.EqualTo(KeybindValidation.Conflict));
            Assert.That(conflict, Is.EqualTo(KeybindAction.Graveyard));

            var stolen = _map.Apply(KeybindAction.Timer, new KeyChord(KeyCode.G));
            Assert.That(stolen, Is.EqualTo(KeybindAction.Graveyard));
            Assert.That(_map.GetChord(KeybindAction.Timer), Is.EqualTo(new KeyChord(KeyCode.G)));
            Assert.That(_map.GetChord(KeybindAction.Graveyard).IsBound, Is.False);
        }

        [Test]
        public void Validate_ShiftVariantOccupancy_ConflictsWithUnpairedShiftChord()
        {
            // Graveyard (paired) on G also occupies Shift+G, so an unpaired action
            // cannot take Shift+G.
            var result = _map.Validate(KeybindAction.TurnInfo,
                new KeyChord(KeyCode.G, shift: true), out var conflict);
            Assert.That(result, Is.EqualTo(KeybindValidation.Conflict));
            Assert.That(conflict, Is.EqualTo(KeybindAction.Graveyard));
        }

        [Test]
        public void Reset_ReclaimsDefaultKey_FromTheActionThatTookIt()
        {
            _map.Apply(KeybindAction.Timer, new KeyChord(KeyCode.G)); // Graveyard now unbound
            var stolen = _map.Reset(KeybindAction.Graveyard);
            Assert.That(stolen, Is.EqualTo(KeybindAction.Timer));
            Assert.That(_map.GetChord(KeybindAction.Graveyard), Is.EqualTo(new KeyChord(KeyCode.G)));
            Assert.That(_map.GetChord(KeybindAction.Timer).IsBound, Is.False);
        }

        [Test]
        public void ResetAll_RestoresEveryDefault()
        {
            _map.Apply(KeybindAction.Graveyard, new KeyChord(KeyCode.H));
            _map.Apply(KeybindAction.Help, new KeyChord(KeyCode.F6));
            _map.ResetAll();
            Assert.That(_map.AnyCustomized, Is.False);
        }

        [Test]
        public void CopyAnnouncement_DefaultUsesReservedBaseKey_ButResetStillWorks()
        {
            _map.Apply(KeybindAction.CopyAnnouncement, new KeyChord(KeyCode.C, ctrl: true));
            Assert.That(_map.IsCustomized(KeybindAction.CopyAnnouncement), Is.True);

            // The default (Ctrl+RightArrow) bypasses the per-chord rules
            var result = _map.Validate(KeybindAction.CopyAnnouncement,
                new KeyChord(KeyCode.RightArrow, ctrl: true), out _);
            Assert.That(result, Is.EqualTo(KeybindValidation.Ok));

            _map.Reset(KeybindAction.CopyAnnouncement);
            Assert.That(_map.IsCustomized(KeybindAction.CopyAnnouncement), Is.False);
        }

        [Test]
        public void SerializeOverrides_RoundTrips()
        {
            _map.Apply(KeybindAction.Graveyard, new KeyChord(KeyCode.H));
            _map.Apply(KeybindAction.Help, new KeyChord(KeyCode.H, ctrl: true));
            _map.Apply(KeybindAction.Timer, new KeyChord(KeyCode.G)); // steals nothing (G free? no - Graveyard moved to H, so G is free)

            string json = _map.SerializeOverrides();
            var loaded = new KeybindMap();
            loaded.LoadOverrides(json);

            foreach (var def in KeybindMap.Definitions)
                Assert.That(loaded.GetChord(def.Action), Is.EqualTo(_map.GetChord(def.Action)),
                    def.Action.ToString());
        }

        [Test]
        public void SerializeOverrides_PersistsUnboundActions()
        {
            _map.Apply(KeybindAction.Timer, new KeyChord(KeyCode.G)); // Graveyard becomes unbound

            var loaded = new KeybindMap();
            loaded.LoadOverrides(_map.SerializeOverrides());

            Assert.That(loaded.GetChord(KeybindAction.Graveyard).IsBound, Is.False);
            Assert.That(loaded.GetChord(KeybindAction.Timer), Is.EqualTo(new KeyChord(KeyCode.G)));
        }

        [Test]
        public void LoadOverrides_KeySwapBetweenActions_LoadsBothSides()
        {
            // Graveyard takes T, TurnInfo takes G — order-dependent validation would
            // wrongly reject this; the two-phase loader must accept it.
            _map.LoadOverrides("{ \"Graveyard\": \"T\", \"TurnInfo\": \"G\" }");
            Assert.That(_map.GetChord(KeybindAction.Graveyard), Is.EqualTo(new KeyChord(KeyCode.T)));
            Assert.That(_map.GetChord(KeybindAction.TurnInfo), Is.EqualTo(new KeyChord(KeyCode.G)));
        }

        [Test]
        public void LoadOverrides_HandEditedConflict_UnbindsTheLaterAction()
        {
            // Graveyard takes T while TurnInfo still defaults to T: the earlier
            // definition (Graveyard) wins, TurnInfo is unbound.
            _map.LoadOverrides("{ \"Graveyard\": \"T\" }");
            Assert.That(_map.GetChord(KeybindAction.Graveyard), Is.EqualTo(new KeyChord(KeyCode.T)));
            Assert.That(_map.GetChord(KeybindAction.TurnInfo).IsBound, Is.False);
        }

        [Test]
        public void LoadOverrides_InvalidEntries_FallBackToDefaults()
        {
            _map.LoadOverrides("{ \"Graveyard\": \"Return\", \"Exile\": \"NotAKey\", \"Help\": \"H\" }");
            Assert.That(_map.GetChord(KeybindAction.Graveyard), Is.EqualTo(new KeyChord(KeyCode.G)),
                "reserved key entry must fall back to default");
            Assert.That(_map.GetChord(KeybindAction.Exile), Is.EqualTo(new KeyChord(KeyCode.X)),
                "unparsable entry must fall back to default");
            Assert.That(_map.GetChord(KeybindAction.Help), Is.EqualTo(new KeyChord(KeyCode.F1)),
                "global plain letter must fall back to default");
        }

        [Test]
        public void KeyChord_SerialFormat_RoundTrips()
        {
            var chords = new[]
            {
                new KeyChord(KeyCode.G),
                new KeyChord(KeyCode.H, ctrl: true),
                new KeyChord(KeyCode.F4, shift: true),
                new KeyChord(KeyCode.Keypad5),
                KeyChord.Unbound
            };
            foreach (var chord in chords)
            {
                Assert.That(KeyChord.TryParse(chord.ToSerial(), out var parsed), Is.True);
                Assert.That(parsed, Is.EqualTo(chord));
            }
            Assert.That(KeyChord.TryParse("Bogus+X", out _), Is.False);
        }
    }
}
