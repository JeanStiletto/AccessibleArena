using System;
using NUnit.Framework;
using UnityEngine;
using AccessibleArena.Core.Services;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Tests
{
    [TestFixture]
    public class ShortcutRegistryTests
    {
        private ShortcutRegistry _registry;

        [SetUp]
        public void SetUp() => _registry = new ShortcutRegistry();

        [Test]
        public void RegisterAndProcessKey_NoModifier_InvokesAction()
        {
            bool invoked = false;
            _registry.RegisterShortcut(KeyCode.F3, () => invoked = true, "Test");
            bool handled = _registry.ProcessKey(KeyCode.F3, shift: false, ctrl: false, alt: false);
            Assert.IsTrue(handled);
            Assert.IsTrue(invoked);
        }

        [Test]
        public void ProcessKey_UnregisteredKey_ReturnsFalse()
        {
            bool handled = _registry.ProcessKey(KeyCode.F3, false, false, false);
            Assert.IsFalse(handled);
        }

        [Test]
        public void UnregisterShortcut_SubsequentProcessKey_ReturnsFalse()
        {
            _registry.RegisterShortcut(KeyCode.R, () => { }, "Test");
            _registry.UnregisterShortcut(KeyCode.R);
            Assert.IsFalse(_registry.ProcessKey(KeyCode.R, false, false, false));
        }

        [Test]
        public void CtrlModifier_FiresOnlyWhenCtrlHeld()
        {
            bool invoked = false;
            _registry.RegisterShortcut(KeyCode.R, KeyCode.LeftControl, () => invoked = true, "Ctrl+R");

            // Exact match
            Assert.IsTrue(_registry.ProcessKey(KeyCode.R, shift: false, ctrl: true, alt: false));
            Assert.IsTrue(invoked);

            // No modifier — should not fire
            invoked = false;
            Assert.IsFalse(_registry.ProcessKey(KeyCode.R, false, false, false));
            Assert.IsFalse(invoked);
        }

        [Test]
        public void CtrlModifier_DoesNotFireWhenShiftAlsoHeld()
        {
            bool invoked = false;
            _registry.RegisterShortcut(KeyCode.R, KeyCode.LeftControl, () => invoked = true, "Ctrl+R");
            bool handled = _registry.ProcessKey(KeyCode.R, shift: true, ctrl: true, alt: false);
            Assert.IsFalse(handled);
            Assert.IsFalse(invoked);
        }

        [Test]
        public void UnmodifiedShortcut_DoesNotFireWhenModifierHeld()
        {
            bool invoked = false;
            _registry.RegisterShortcut(KeyCode.R, () => invoked = true, "R");
            Assert.IsFalse(_registry.ProcessKey(KeyCode.R, shift: true, ctrl: false, alt: false));
            Assert.IsFalse(_registry.ProcessKey(KeyCode.R, shift: false, ctrl: true, alt: false));
            Assert.IsFalse(_registry.ProcessKey(KeyCode.R, shift: false, ctrl: false, alt: true));
            Assert.IsFalse(invoked);
        }

        [Test]
        public void ShiftModifier_FiresOnlyWhenShiftHeld()
        {
            bool invoked = false;
            _registry.RegisterShortcut(KeyCode.G, KeyCode.LeftShift, () => invoked = true, "Shift+G");
            Assert.IsTrue(_registry.ProcessKey(KeyCode.G, shift: true, ctrl: false, alt: false));
            Assert.IsTrue(invoked);
        }

        [Test]
        public void RegisterSameKeyTwice_BothActionsRetained_FirstWins()
        {
            int count = 0;
            _registry.RegisterShortcut(KeyCode.A, () => count += 10, "First");
            _registry.RegisterShortcut(KeyCode.A, () => count += 1, "Second");

            // First match wins — only the first action fires
            _registry.ProcessKey(KeyCode.A, false, false, false);
            Assert.AreEqual(10, count);
        }

        [Test]
        public void SameKeyDifferentModifiers_EachFiresIndependently()
        {
            bool plain = false, ctrl = false;
            _registry.RegisterShortcut(KeyCode.D, () => plain = true, "D");
            _registry.RegisterShortcut(KeyCode.D, KeyCode.LeftControl, () => ctrl = true, "Ctrl+D");

            _registry.ProcessKey(KeyCode.D, false, false, false);
            Assert.IsTrue(plain);
            Assert.IsFalse(ctrl);

            _registry.ProcessKey(KeyCode.D, false, true, false);
            Assert.IsTrue(ctrl);
        }

        [Test]
        public void GetAllShortcuts_ReturnsAllRegistered()
        {
            _registry.RegisterShortcut(KeyCode.F1, () => { }, "Help");
            _registry.RegisterShortcut(KeyCode.F2, () => { }, "Settings");
            _registry.RegisterShortcut(KeyCode.F3, KeyCode.LeftShift, () => { }, "Shift+F3");

            var all = new System.Collections.Generic.List<ShortcutDefinition>(_registry.GetAllShortcuts());
            Assert.AreEqual(3, all.Count);
        }

        [Test]
        public void ProcessKey_ActionIsNullSafe_DoesNotThrow()
        {
            _registry.RegisterShortcut(KeyCode.Z, null, "Null action");
            Assert.DoesNotThrow(() => _registry.ProcessKey(KeyCode.Z, false, false, false));
        }

        [Test]
        public void GetKeyString_NoModifier_ReturnsKeyName()
        {
            var def = new ShortcutDefinition(KeyCode.R, () => { }, "Test");
            Assert.AreEqual("R", def.GetKeyString());
        }

        [Test]
        public void GetKeyString_WithCtrlModifier_ReturnsCtrlPlusKey()
        {
            var def = new ShortcutDefinition(KeyCode.R, () => { }, "Test", KeyCode.LeftControl);
            Assert.AreEqual("Ctrl+R", def.GetKeyString());
        }
    }
}
