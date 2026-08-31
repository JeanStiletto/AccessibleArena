using NUnit.Framework;
using AccessibleArena.Core.Services;

namespace AccessibleArena.Tests
{
    [TestFixture]
    public class CombatInputGuardTests
    {
        [Test]
        public void ShouldActivatePrimary_SpaceBlocked_ReturnsFalse()
        {
            Assert.That(ConfirmationInputPolicy.ShouldActivateCombatPrimary(
                backspaceDown: false,
                spaceDown: true,
                spaceBlocked: true), Is.False);
        }

        [Test]
        public void ShouldActivatePrimary_SpaceAllowed_ReturnsTrue()
        {
            Assert.That(ConfirmationInputPolicy.ShouldActivateCombatPrimary(
                backspaceDown: false,
                spaceDown: true,
                spaceBlocked: false), Is.True);
        }

        [Test]
        public void ShouldActivatePrimary_BackspacePressed_ReturnsFalse()
        {
            Assert.That(ConfirmationInputPolicy.ShouldActivateCombatPrimary(
                backspaceDown: true,
                spaceDown: false,
                spaceBlocked: false), Is.False);
        }
    }
}
