using System.Text.RegularExpressions;

namespace AccessibleArena.Core.Utils
{
    public static class VaultLabelFormatter
    {
        private static readonly Regex PercentagePattern = new Regex(
            @"(?<!\d)(\d+(?:[.,]\d+)?)\s*%",
            RegexOptions.Compiled);

        public static string ExtractVaultProgress(string tooltipText)
        {
            if (string.IsNullOrWhiteSpace(tooltipText)) return null;

            var matches = PercentagePattern.Matches(tooltipText);
            return matches.Count > 0 ? matches[matches.Count - 1].Value.Replace(" ", "") : null;
        }
    }
}
