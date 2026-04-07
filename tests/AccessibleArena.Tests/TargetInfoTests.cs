using NUnit.Framework;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Tests
{
    [TestFixture]
    public class TargetInfoTests
    {
        [Test]
        public void GetAnnouncement_NameOnly_ReturnsName()
        {
            var t = new TargetInfo { Name = "Dragon" };
            Assert.That(t.GetAnnouncement(), Is.EqualTo("Dragon"));
        }

        [Test]
        public void GetAnnouncement_NameAndDetails_ReturnsCombined()
        {
            var t = new TargetInfo { Name = "Dragon", Details = "5/5 flying" };
            Assert.That(t.GetAnnouncement(), Is.EqualTo("Dragon, 5/5 flying"));
        }

        [Test]
        public void GetAnnouncement_EmptyDetails_ReturnsNameOnly()
        {
            var t = new TargetInfo { Name = "Dragon", Details = "" };
            Assert.That(t.GetAnnouncement(), Is.EqualTo("Dragon"));
        }

        [Test]
        public void ToString_NonOpponent_NoOpponentTag()
        {
            var t = new TargetInfo { Name = "Dragon", Type = CardTargetType.Creature, IsOpponent = false };
            Assert.That(t.ToString(), Is.EqualTo("Dragon (Creature)"));
        }

        [Test]
        public void ToString_Opponent_HasOpponentTag()
        {
            var t = new TargetInfo { Name = "Dragon", Type = CardTargetType.Creature, IsOpponent = true };
            Assert.That(t.ToString(), Is.EqualTo("Dragon (Creature) [Opponent]"));
        }

        [Test]
        public void ToString_PlayerType_Works()
        {
            var t = new TargetInfo { Name = "You", Type = CardTargetType.Player, IsOpponent = false };
            Assert.That(t.ToString(), Is.EqualTo("You (Player)"));
        }

        [Test]
        public void ToString_UnknownType_Works()
        {
            var t = new TargetInfo { Name = "Something", Type = CardTargetType.Unknown, IsOpponent = false };
            Assert.That(t.ToString(), Is.EqualTo("Something (Unknown)"));
        }

        [Test]
        public void GetAnnouncement_NullDetails_ReturnsName()
        {
            var t = new TargetInfo { Name = "Goblin", Details = null };
            Assert.That(t.GetAnnouncement(), Is.EqualTo("Goblin"));
        }
    }
}
