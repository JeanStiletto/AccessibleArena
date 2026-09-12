using System.Text.RegularExpressions;

namespace AccessibleArena.Core.Utils
{
    /// <summary>
    /// Pulls the vault progress percentage out of the game's tooltip text for the navbar's
    /// open-vault label. Both sources the mod reads (the vault's own VaultProgress_Tooltip and the
    /// wildcard tooltip's vault line) format the value with the culture's percent pattern, so
    /// "100.0%", "100,0 %" and "123.4%" all have to parse; the last percentage in the text wins
    /// because the wildcard tooltip lists the per-rarity counts before the vault line.
    ///
    /// Pure logic: no Unity/game dependencies, unit-tested in VaultLabelFormatterTests.
    /// </summary>
    public static class VaultLabelFormatter
    {
        private static readonly Regex PercentagePattern = new Regex(
            @"(?<!\d)(\d+(?:[.,]\d+)?)\s*%",
            RegexOptions.Compiled);

        /// <summary>
        /// Returns the last percentage in <paramref name="tooltipText"/> with any space before
        /// the percent sign removed (for example "100,0%"), or null when there is none.
        /// </summary>
        public static string ExtractVaultProgress(string tooltipText)
        {
            if (string.IsNullOrWhiteSpace(tooltipText)) return null;

            var matches = PercentagePattern.Matches(tooltipText);
            return matches.Count > 0 ? matches[matches.Count - 1].Value.Replace(" ", "") : null;
        }
    }
}
