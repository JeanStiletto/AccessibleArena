using System;
using System.IO;
using System.Reflection;
using MelonLoader;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Mod settings with JSON file persistence.
    /// Settings are saved to UserData/AccessibleArena.json in the game directory.
    /// </summary>
    public class ModSettings
    {
        private static readonly string SettingsPath = Path.Combine("UserData", "AccessibleArena.json");

        // Available language codes
        public static readonly string[] LanguageCodes = { "en", "de", "fr", "es", "it", "pt-BR", "ja", "ko", "ru", "pl", "zh-CN", "zh-TW" };

        // Locale keys for language display names (translated per language)
        private static readonly string[] LanguageKeys = { "LangEnglish", "LangGerman", "LangFrench", "LangSpanish", "LangItalian", "LangPortuguese", "LangJapanese", "LangKorean", "LangRussian", "LangPolish", "LangChineseSimplified", "LangChineseTraditional" };

        // --- Settings ---
        public string Language { get; set; } = "en";
        public bool TutorialMessages { get; set; } = true;
        public bool VerboseAnnouncements { get; set; } = true;

        /// <summary>
        /// Load settings from disk. Returns defaults if file doesn't exist or is corrupt.
        /// </summary>
        public static ModSettings Load()
        {
            var settings = new ModSettings();

            try
            {
                if (!File.Exists(SettingsPath))
                {
                    MelonLogger.Msg("[ModSettings] No settings file found, using defaults");
                    return settings;
                }

                string json = File.ReadAllText(SettingsPath);
                settings.ParseJson(json);
                MelonLogger.Msg($"[ModSettings] Loaded settings: Language={settings.Language}, Tutorial={settings.TutorialMessages}, Verbose={settings.VerboseAnnouncements}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ModSettings] Failed to load settings, using defaults: {ex.Message}");
            }

            return settings;
        }

        /// <summary>
        /// Save current settings to disk.
        /// </summary>
        public void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = ToJson();
                File.WriteAllText(SettingsPath, json);
                MelonLogger.Msg("[ModSettings] Settings saved");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ModSettings] Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the display name for the current language setting.
        /// </summary>
        public string GetLanguageDisplayName()
        {
            return GetLanguageDisplayName(GetLanguageIndex(Language));
        }

        /// <summary>Fired when the language setting changes.</summary>
        public event Action OnLanguageChanged;

        /// <summary>
        /// Set a specific language by code.
        /// Updates LocaleManager and fires OnLanguageChanged.
        /// </summary>
        public void SetLanguage(string code)
        {
            if (code == Language) return;
            Language = code;
            LocaleManager.Instance?.SetLanguage(Language);
            OnLanguageChanged?.Invoke();
        }

        /// <summary>
        /// Cycle to the next language in the list.
        /// Updates LocaleManager and fires OnLanguageChanged.
        /// </summary>
        public void CycleLanguage()
        {
            int index = Array.IndexOf(LanguageCodes, Language);
            index = (index + 1) % LanguageCodes.Length;
            SetLanguage(LanguageCodes[index]);
        }

        /// <summary>
        /// Get the language index for a given code.
        /// </summary>
        public static int GetLanguageIndex(string code)
        {
            int index = Array.IndexOf(LanguageCodes, code);
            return index >= 0 ? index : 0;
        }

        /// <summary>
        /// Get the localized display name for a language at a given index.
        /// </summary>
        public static string GetLanguageDisplayName(int index)
        {
            if (index >= 0 && index < LanguageKeys.Length)
                return LocaleManager.Instance?.Get(LanguageKeys[index]) ?? LanguageCodes[index];
            return "Unknown";
        }

        /// <summary>
        /// Try to detect the game's active language via reflection.
        /// Returns the language code or "en" if detection fails.
        /// </summary>
        public static string DetectGameLanguage()
        {
            try
            {
                // Try to access Wotc.Mtga.Loc.Languages.ActiveLocProvider
                var locType = Type.GetType("Wotc.Mtga.Loc.Languages, Core");
                if (locType == null)
                {
                    // Try scanning loaded assemblies
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        locType = asm.GetType("Wotc.Mtga.Loc.Languages");
                        if (locType != null) break;
                    }
                }

                if (locType != null)
                {
                    var providerProp = locType.GetProperty("ActiveLocProvider", BindingFlags.Public | BindingFlags.Static);
                    if (providerProp != null)
                    {
                        var provider = providerProp.GetValue(null);
                        if (provider != null)
                        {
                            // The provider should have a language code or locale property
                            var langProp = provider.GetType().GetProperty("LanguageCode")
                                ?? provider.GetType().GetProperty("Locale")
                                ?? provider.GetType().GetProperty("Language");

                            if (langProp != null)
                            {
                                string lang = langProp.GetValue(provider)?.ToString();
                                if (!string.IsNullOrEmpty(lang))
                                {
                                    MelonLogger.Msg($"[ModSettings] Detected game language: {lang}");
                                    return lang;
                                }
                            }

                            // Try ToString as fallback
                            string provStr = provider.ToString();
                            if (!string.IsNullOrEmpty(provStr) && provStr.Length <= 10)
                            {
                                MelonLogger.Msg($"[ModSettings] Detected game language (ToString): {provStr}");
                                return provStr;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ModSettings] Language detection failed: {ex.Message}");
            }

            return "en";
        }

        private string ToJson()
        {
            // Simple hand-written JSON (no external dependencies)
            return "{\n" +
                   $"  \"Language\": \"{EscapeJson(Language)}\",\n" +
                   $"  \"TutorialMessages\": {(TutorialMessages ? "true" : "false")},\n" +
                   $"  \"VerboseAnnouncements\": {(VerboseAnnouncements ? "true" : "false")}\n" +
                   "}";
        }

        private void ParseJson(string json)
        {
            // Simple key-value parser for our flat JSON structure
            Language = ReadJsonString(json, "Language") ?? Language;
            TutorialMessages = ReadJsonBool(json, "TutorialMessages") ?? TutorialMessages;
            VerboseAnnouncements = ReadJsonBool(json, "VerboseAnnouncements") ?? VerboseAnnouncements;
        }

        private static string ReadJsonString(string json, string key)
        {
            string pattern = $"\"{key}\"";
            int keyIndex = json.IndexOf(pattern, StringComparison.Ordinal);
            if (keyIndex < 0) return null;

            int colonIndex = json.IndexOf(':', keyIndex + pattern.Length);
            if (colonIndex < 0) return null;

            int startQuote = json.IndexOf('"', colonIndex + 1);
            if (startQuote < 0) return null;

            int endQuote = json.IndexOf('"', startQuote + 1);
            if (endQuote < 0) return null;

            return json.Substring(startQuote + 1, endQuote - startQuote - 1);
        }

        private static bool? ReadJsonBool(string json, string key)
        {
            string pattern = $"\"{key}\"";
            int keyIndex = json.IndexOf(pattern, StringComparison.Ordinal);
            if (keyIndex < 0) return null;

            int colonIndex = json.IndexOf(':', keyIndex + pattern.Length);
            if (colonIndex < 0) return null;

            string remaining = json.Substring(colonIndex + 1).TrimStart();
            if (remaining.StartsWith("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (remaining.StartsWith("false", StringComparison.OrdinalIgnoreCase)) return false;

            return null;
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
