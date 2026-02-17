using System.Collections.Generic;
using System.Globalization;

namespace AccessibleArenaInstaller
{
    /// <summary>
    /// Detects the Windows display language and maps it to a supported mod language code.
    /// </summary>
    public static class LanguageDetector
    {
        /// <summary>
        /// Language codes supported by the mod, matching the lang/*.json filenames.
        /// </summary>
        public static readonly string[] SupportedLanguages = new[]
        {
            "en", "de", "fr", "es", "it", "pt-BR",
            "ja", "ko", "zh-CN", "zh-TW", "ru", "pl"
        };

        /// <summary>
        /// Display names for the dropdown. Native name first, then English name for recognition.
        /// </summary>
        public static readonly Dictionary<string, string> DisplayNames = new Dictionary<string, string>
        {
            { "en", "English" },
            { "de", "Deutsch (German)" },
            { "fr", "Fran\u00e7ais (French)" },
            { "es", "Espa\u00f1ol (Spanish)" },
            { "it", "Italiano (Italian)" },
            { "pt-BR", "Portugu\u00eas (Brazilian Portuguese)" },
            { "ja", "\u65e5\u672c\u8a9e (Japanese)" },
            { "ko", "\ud55c\uad6d\uc5b4 (Korean)" },
            { "zh-CN", "\u7b80\u4f53\u4e2d\u6587 (Chinese Simplified)" },
            { "zh-TW", "\u7e41\u9ad4\u4e2d\u6587 (Chinese Traditional)" },
            { "ru", "\u0420\u0443\u0441\u0441\u043a\u0438\u0439 (Russian)" },
            { "pl", "Polski (Polish)" }
        };

        // Maps Windows two-letter ISO codes and specific culture names to mod language codes.
        // Order matters for lookup: specific cultures (e.g. "pt-BR") are checked first via GetBestLanguage.
        private static readonly Dictionary<string, string> LanguageMap = new Dictionary<string, string>
        {
            { "en", "en" },
            { "de", "de" },
            { "fr", "fr" },
            { "es", "es" },
            { "it", "it" },
            { "pt-BR", "pt-BR" },
            { "pt", "pt-BR" },  // Portuguese (any) -> Brazilian Portuguese
            { "ja", "ja" },
            { "ko", "ko" },
            { "zh-CN", "zh-CN" },
            { "zh-TW", "zh-TW" },
            { "zh-Hans", "zh-CN" },
            { "zh-Hant", "zh-TW" },
            { "zh", "zh-CN" },  // Chinese (unspecified) -> Simplified
            { "ru", "ru" },
            { "pl", "pl" }
        };

        /// <summary>
        /// Detects the best matching mod language code from the Windows UI culture.
        /// Returns "en" if no match is found.
        /// </summary>
        public static string DetectLanguage()
        {
            var culture = CultureInfo.CurrentUICulture;
            string result = GetBestLanguage(culture);
            Logger.Info($"Windows UI culture: {culture.Name} ({culture.DisplayName}), detected mod language: {result}");
            return result;
        }

        /// <summary>
        /// Resolves a CultureInfo to the best matching mod language code.
        /// Tries full name (e.g. "pt-BR"), then two-letter code (e.g. "pt"), then falls back to "en".
        /// </summary>
        private static string GetBestLanguage(CultureInfo culture)
        {
            // Try full culture name first (e.g. "zh-TW", "pt-BR")
            if (LanguageMap.TryGetValue(culture.Name, out string match))
                return match;

            // Try two-letter ISO code (e.g. "de", "fr")
            if (LanguageMap.TryGetValue(culture.TwoLetterISOLanguageName, out string isoMatch))
                return isoMatch;

            // Try parent culture (e.g. "de-AT" -> "de")
            if (!culture.IsNeutralCulture && culture.Parent != null && culture.Parent != CultureInfo.InvariantCulture)
            {
                if (LanguageMap.TryGetValue(culture.Parent.Name, out string parentMatch))
                    return parentMatch;
            }

            return "en";
        }
    }
}
