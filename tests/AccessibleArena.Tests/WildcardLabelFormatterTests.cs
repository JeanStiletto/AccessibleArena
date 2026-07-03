using System;
using NUnit.Framework;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Tests
{
    [TestFixture]
    public class WildcardLabelFormatterTests
    {
        // ---- NormalizeRarity ----

        [TestCase("Common", "Common")]
        [TestCase("Uncommon", "Uncommon")]
        [TestCase("Rare", "Rare")]
        [TestCase("MythicRare", "MythicRare")]
        [TestCase("Mythic Rare", "MythicRare")]   // spaced variant
        [TestCase("Mythic", "MythicRare")]         // parent-name token (WildcardProgress)
        [TestCase("rare", "Rare")]                 // case-insensitive
        public void NormalizeRarity_KnownRarities_ReturnsCanonicalToken(string input, string expected)
        {
            Assert.AreEqual(expected, WildcardLabelFormatter.NormalizeRarity(input));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("None")]
        [TestCase("0")]
        [TestCase("Land")]
        [TestCase("Bogus")]
        public void NormalizeRarity_UnknownOrNone_ReturnsNull(string input)
        {
            Assert.IsNull(WildcardLabelFormatter.NormalizeRarity(input));
        }

        // ---- Format: English fallback (no loc provider) ----

        [TestCase("Common", "Common Wildcard")]
        [TestCase("Uncommon", "Uncommon Wildcard")]
        [TestCase("Rare", "Rare Wildcard")]
        [TestCase("MythicRare", "Mythic Rare Wildcard")]
        [TestCase("Mythic", "Mythic Rare Wildcard")]
        public void Format_NoResolver_ReturnsEnglishLabel(string rarity, string expected)
        {
            Assert.AreEqual(expected, WildcardLabelFormatter.Format(rarity, null));
        }

        [TestCase(null)]
        [TestCase("None")]
        [TestCase("0")]
        [TestCase("Land")]
        public void Format_NoRarity_ReturnsBareWildcard(string rarity)
        {
            Assert.AreEqual("Wildcard", WildcardLabelFormatter.Format(rarity, null));
        }

        // ---- Format: localized path ----

        [Test]
        public void Format_ResolverProvidesLocalizedTerm_UsesIt()
        {
            Func<string, string> resolver = key =>
                key == "MainNav/General/RareWildcard" ? "Carta comodín rara" : null;

            Assert.AreEqual("Carta comodín rara", WildcardLabelFormatter.Format("Rare", resolver));
        }

        [Test]
        public void Format_ResolverUsesMythicRareKey()
        {
            string requestedKey = null;
            Func<string, string> resolver = key => { requestedKey = key; return null; };

            WildcardLabelFormatter.Format("Mythic", resolver);

            Assert.AreEqual("MainNav/General/MythicRareWildcard", requestedKey);
        }

        [Test]
        public void Format_ResolverReturnsNull_FallsBackToEnglish()
        {
            Func<string, string> resolver = _ => null;
            Assert.AreEqual("Rare Wildcard", WildcardLabelFormatter.Format("Rare", resolver));
        }

        [Test]
        public void Format_ResolverReturnsEmpty_FallsBackToEnglish()
        {
            Func<string, string> resolver = _ => "";
            Assert.AreEqual("Rare Wildcard", WildcardLabelFormatter.Format("Rare", resolver));
        }

        [Test]
        public void Format_NoRarity_DoesNotInvokeResolver()
        {
            bool invoked = false;
            Func<string, string> resolver = _ => { invoked = true; return "x"; };

            Assert.AreEqual("Wildcard", WildcardLabelFormatter.Format("None", resolver));
            Assert.IsFalse(invoked);
        }
    }
}
