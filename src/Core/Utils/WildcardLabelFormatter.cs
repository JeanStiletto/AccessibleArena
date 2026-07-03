using System;

namespace AccessibleArena.Core.Utils
{
    /// <summary>
    /// Builds "{Rarity} Wildcard" labels for screen-reader announcements.
    /// Prefers the game's own localized wildcard terms (loc keys
    /// "MainNav/General/{Rarity}Wildcard" — the same strings the game's
    /// AbilityHanger uses, localized in some languages and English in others),
    /// falling back to English formatting when the loc provider is unavailable.
    ///
    /// Pure logic: the localization lookup is injected as a resolver delegate so
    /// this class stays free of Unity/game dependencies and unit-testable.
    /// </summary>
    public static class WildcardLabelFormatter
    {
        /// <summary>
        /// Normalizes a raw rarity string (enum ToString, parent-name token, etc.)
        /// to the canonical game token used in loc keys and English formatting.
        /// Returns null for none/empty/unknown rarities.
        /// </summary>
        public static string NormalizeRarity(string rarity)
        {
            if (string.IsNullOrWhiteSpace(rarity)) return null;

            switch (rarity.Replace(" ", "").ToLowerInvariant())
            {
                case "common": return "Common";
                case "uncommon": return "Uncommon";
                case "rare": return "Rare";
                case "mythic":
                case "mythicrare": return "MythicRare";
                default: return null; // None, Land, "0", etc.
            }
        }

        /// <summary>
        /// Builds a wildcard label for the given rarity. Tries the game's localized
        /// term first via <paramref name="locResolver"/> (may be null), then falls
        /// back to English.
        /// </summary>
        public static string Format(string rarity, Func<string, string> locResolver)
        {
            string normalized = NormalizeRarity(rarity);
            if (normalized == null)
                return "Wildcard";

            string localized = locResolver?.Invoke("MainNav/General/" + normalized + "Wildcard");
            if (!string.IsNullOrEmpty(localized))
                return localized;

            return EnglishRarity(normalized) + " Wildcard";
        }

        private static string EnglishRarity(string normalized)
            => normalized == "MythicRare" ? "Mythic Rare" : normalized;
    }
}
