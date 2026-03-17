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
        public bool BriefCastAnnouncements { get; set; } = true;
        public bool PhaseSkipWarning { get; set; } = true;

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
                MelonLogger.Msg($"[ModSettings] Loaded settings: Language={settings.Language}, Tutorial={settings.TutorialMessages}, Verbose={settings.VerboseAnnouncements}, BriefCast={settings.BriefCastAnnouncements}, PhaseSkipWarning={settings.PhaseSkipWarning}");
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

        private string ToJson()
        {
            // Simple hand-written JSON (no external dependencies)
            return "{\n" +
                   $"  \"Language\": \"{EscapeJson(Language)}\",\n" +
                   $"  \"TutorialMessages\": {(TutorialMessages ? "true" : "false")},\n" +
                   $"  \"VerboseAnnouncements\": {(VerboseAnnouncements ? "true" : "false")},\n" +
                   $"  \"BriefCastAnnouncements\": {(BriefCastAnnouncements ? "true" : "false")},\n" +
                   $"  \"PhaseSkipWarning\": {(PhaseSkipWarning ? "true" : "false")}\n" +
                   "}";
        }

        private void ParseJson(string json)
        {
            // Simple key-value parser for our flat JSON structure
            Language = ReadJsonString(json, "Language") ?? Language;
            TutorialMessages = ReadJsonBool(json, "TutorialMessages") ?? TutorialMessages;
            VerboseAnnouncements = ReadJsonBool(json, "VerboseAnnouncements") ?? VerboseAnnouncements;
            BriefCastAnnouncements = ReadJsonBool(json, "BriefCastAnnouncements") ?? BriefCastAnnouncements;
            PhaseSkipWarning = ReadJsonBool(json, "PhaseSkipWarning") ?? PhaseSkipWarning;
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
