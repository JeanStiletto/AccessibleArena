using NUnit.Framework;
using UnityEngine;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Tests
{
    [TestFixture]
    public class KeyHoldRepeaterTests
    {
        private KeyHoldRepeater _rep;
        private FakeKeyboardBackend _keys;

        [SetUp]
        public void SetUp()
        {
            _rep = new KeyHoldRepeater();
            _keys = new FakeKeyboardBackend();
            KeyInput.Backend = _keys;
            UnityEngine.Time.unscaledDeltaTime = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            KeyInput.Backend = null;
        }

        [Test]
        public void InitialPress_FiresAction_ReturnsTrue()
        {
            int fired = 0;
            _keys.SimulateKeyDown(KeyCode.DownArrow);
            bool result = _rep.Check(KeyCode.DownArrow, () => { fired++; return true; });
            Assert.That(result, Is.True);
            Assert.That(fired, Is.EqualTo(1));
        }

        [Test]
        public void NoKeyState_ReturnsFalse_ActionNotCalled()
        {
            int fired = 0;
            bool result = _rep.Check(KeyCode.DownArrow, () => { fired++; return true; });
            Assert.That(result, Is.False);
            Assert.That(fired, Is.EqualTo(0));
        }

        [Test]
        public void HeldBeforeInitialDelay_DoesNotRepeat()
        {
            int fired = 0;
            // Initial press
            _keys.SimulateKeyDown(KeyCode.DownArrow);
            _rep.Check(KeyCode.DownArrow, () => { fired++; return true; });
            fired = 0;

            // Hold, but not enough time elapsed
            _keys.SimulateKeyHeld(KeyCode.DownArrow);
            UnityEngine.Time.unscaledDeltaTime = 0.1f;
            _rep.Check(KeyCode.DownArrow, () => { fired++; return true; });
            Assert.That(fired, Is.EqualTo(0));
        }

        [Test]
        public void HeldAfterInitialDelay_Repeats()
        {
            int fired = 0;
            // Initial press
            _keys.SimulateKeyDown(KeyCode.DownArrow);
            _rep.Check(KeyCode.DownArrow, () => { fired++; return true; });
            fired = 0;

            // Hold with delta that pushes timer past InitialDelay (0.5f)
            _keys.SimulateKeyHeld(KeyCode.DownArrow);
            UnityEngine.Time.unscaledDeltaTime = 0.6f;
            _rep.Check(KeyCode.DownArrow, () => { fired++; return true; });
            Assert.That(fired, Is.EqualTo(1));
        }

        [Test]
        public void HoldRepeat_ActionReturnsFalse_StopsRepeating()
        {
            int fired = 0;
            _keys.SimulateKeyDown(KeyCode.DownArrow);
            _rep.Check(KeyCode.DownArrow, () => { fired++; return true; });
            fired = 0;

            // First repeat fires, returns false (boundary)
            _keys.SimulateKeyHeld(KeyCode.DownArrow);
            UnityEngine.Time.unscaledDeltaTime = 0.6f;
            _rep.Check(KeyCode.DownArrow, () => { fired++; return false; });
            Assert.That(fired, Is.EqualTo(1));

            // Second frame — hold tracking stopped, no more fires
            fired = 0;
            UnityEngine.Time.unscaledDeltaTime = 0.2f;
            _rep.Check(KeyCode.DownArrow, () => { fired++; return false; });
            Assert.That(fired, Is.EqualTo(0));
        }

        [Test]
        public void KeyRelease_StopsHolding_ReturnsFalse()
        {
            _keys.SimulateKeyDown(KeyCode.DownArrow);
            _rep.Check(KeyCode.DownArrow, () => true);

            // Key released
            _keys.SimulateKeyReleased(KeyCode.DownArrow);
            bool result = _rep.Check(KeyCode.DownArrow, () => true);
            Assert.That(result, Is.False);
        }

        [Test]
        public void Reset_ClearsHoldState()
        {
            int fired = 0;
            _keys.SimulateKeyDown(KeyCode.DownArrow);
            _rep.Check(KeyCode.DownArrow, () => { fired++; return true; });
            fired = 0;

            _rep.Reset();

            // After reset, a held key should not fire the repeat
            _keys.SimulateKeyHeld(KeyCode.DownArrow);
            UnityEngine.Time.unscaledDeltaTime = 0.6f;
            _rep.Check(KeyCode.DownArrow, () => { fired++; return true; });
            Assert.That(fired, Is.EqualTo(0));
        }

        [Test]
        public void DifferentKeyPressed_PreviousHoldCleared()
        {
            int firedDown = 0, firedUp = 0;
            // Press Down
            _keys.SimulateKeyDown(KeyCode.DownArrow);
            _rep.Check(KeyCode.DownArrow, () => { firedDown++; return true; });

            // Now press Up (different key) — Down hold should be abandoned
            _keys.SimulateKeyHeld(KeyCode.DownArrow);
            _keys.SimulateKeyDown(KeyCode.UpArrow);
            firedDown = 0;
            _rep.Check(KeyCode.UpArrow, () => { firedUp++; return true; });
            Assert.That(firedUp, Is.EqualTo(1));

            // Down key hold no longer fires
            _keys.SimulateKeyHeld(KeyCode.DownArrow);
            UnityEngine.Time.unscaledDeltaTime = 0.6f;
            _rep.Check(KeyCode.DownArrow, () => { firedDown++; return true; });
            Assert.That(firedDown, Is.EqualTo(0));
        }

        [Test]
        public void ActionOverload_AlwaysRepeats()
        {
            int fired = 0;
            _keys.SimulateKeyDown(KeyCode.DownArrow);
            _rep.Check(KeyCode.DownArrow, (System.Action)(() => fired++));
            fired = 0;

            _keys.SimulateKeyHeld(KeyCode.DownArrow);
            UnityEngine.Time.unscaledDeltaTime = 0.6f;
            _rep.Check(KeyCode.DownArrow, (System.Action)(() => fired++));
            Assert.That(fired, Is.EqualTo(1));
        }

        [Test]
        public void InitialPress_ActionReturnsFalse_NoHoldTracked()
        {
            int fired = 0;
            _keys.SimulateKeyDown(KeyCode.DownArrow);
            // Action returns false (boundary on first press)
            _rep.Check(KeyCode.DownArrow, () => { fired++; return false; });
            fired = 0;

            // Hold — should not repeat since initial press returned false
            _keys.SimulateKeyHeld(KeyCode.DownArrow);
            UnityEngine.Time.unscaledDeltaTime = 0.6f;
            _rep.Check(KeyCode.DownArrow, () => { fired++; return true; });
            Assert.That(fired, Is.EqualTo(0));
        }
    }
}
