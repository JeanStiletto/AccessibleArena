using NUnit.Framework;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Tests
{
    [TestFixture]
    public class VaultLabelFormatterTests
    {
        [TestCase("Vault progress: 100.0%", "100.0%")]           // VaultProgress_Tooltip, P1 format
        [TestCase("Tresorfortschritt: 100,0 %", "100,0%")]       // German percent pattern with space
        [TestCase("Vault: 123.4%", "123.4%")]                    // progress past 100%
        [TestCase("100%", "100%")]                               // no decimals
        [TestCase("宝库进度：100.0%", "100.0%")]                  // non-Latin surrounding text
        public void ExtractVaultProgress_TooltipWithPercentage_ReturnsIt(string tooltip, string expected)
        {
            Assert.AreEqual(expected, VaultLabelFormatter.ExtractVaultProgress(tooltip));
        }

        [Test]
        public void ExtractVaultProgress_WildcardTooltip_ReturnsLastPercentage()
        {
            // Wildcard tooltip: per-rarity counts first, vault line last (joined with ", " by the extractor).
            const string tooltip = "3 Common Wildcards, 2 Rare Wildcards, 50% to next Mythic, Vault: 100.0%";
            Assert.AreEqual("100.0%", VaultLabelFormatter.ExtractVaultProgress(tooltip));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("3 Common Wildcards, 2 Rare Wildcards")]      // no percentage at all
        [TestCase("Rate: %")]                                    // percent sign without a number
        public void ExtractVaultProgress_NoPercentage_ReturnsNull(string tooltip)
        {
            Assert.IsNull(VaultLabelFormatter.ExtractVaultProgress(tooltip));
        }
    }
}
