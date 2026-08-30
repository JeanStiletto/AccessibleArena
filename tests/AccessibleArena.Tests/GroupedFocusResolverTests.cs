using NUnit.Framework;
using AccessibleArena.Core.Services;

namespace AccessibleArena.Tests
{
    [TestFixture]
    public class GroupedFocusResolverTests
    {
        [Test]
        public void ResolveGroupedFocus_StandaloneGroup_ReturnsStandaloneElement()
        {
            var standalone = new object();

            Assert.That(ConfirmationInputPolicy.ResolveGroupedFocus(
                currentElement: null,
                isStandaloneGroup: true,
                standaloneElement: standalone), Is.SameAs(standalone));
        }

        [Test]
        public void ResolveGroupedFocus_InsideGroup_ReturnsCurrentElement()
        {
            var current = new object();
            var standalone = new object();

            Assert.That(ConfirmationInputPolicy.ResolveGroupedFocus(
                currentElement: current,
                isStandaloneGroup: true,
                standaloneElement: standalone), Is.SameAs(current));
        }

        [Test]
        public void ResolveGroupedFocus_NonStandaloneGroupList_ReturnsNull()
        {
            Assert.That(ConfirmationInputPolicy.ResolveGroupedFocus(
                currentElement: null,
                isStandaloneGroup: false,
                standaloneElement: new object()), Is.Null);
        }
    }
}
