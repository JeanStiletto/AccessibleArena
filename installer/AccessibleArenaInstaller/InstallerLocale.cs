using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace AccessibleArenaInstaller
{
    /// <summary>
    /// Static localization class for the installer.
    /// Loads flat JSON files from embedded resources.
    /// Fallback chain: active language -> English -> key name.
    /// </summary>
    public static class InstallerLocale
    {
        private static Dictionary<string, string> _activeStrings = new Dictionary<string, string>();
        private static Dictionary<string, string> _fallbackStrings = new Dictionary<string, string>();
        private static string _activeLanguage = "en";

        /// <summary>Fired when the active language changes.</summary>
        public static event Action OnLanguageChanged;

        /// <summary>
        /// Initialize the locale system and load the specified language.
        /// Must be called once early in startup before any UI strings are needed.
        /// </summary>
        public static void Initialize(string languageCode)
        {
            LoadLanguage(languageCode);
        }

        /// <summary>
        /// Switch to a different language. Reloads strings and fires OnLanguageChanged.
        /// </summary>
        public static void SetLanguage(string code)
        {
            if (code == _activeLanguage) return;
            LoadLanguage(code);
            OnLanguageChanged?.Invoke();
        }

        /// <summary>
        /// Get a localized string by key. Falls back to English, then to the key name itself.
        /// </summary>
        public static string Get(string key)
        {
            if (_activeStrings.TryGetValue(key, out string val))
                return val;
            if (_fallbackStrings.TryGetValue(key, out string fallback))
                return fallback;
            return key;
        }

        /// <summary>
        /// Get a localized string and apply string.Format with the given args.
        /// </summary>
        public static string Format(string key, params object[] args)
        {
            string template = Get(key);
            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }

        private static void LoadLanguage(string code)
        {
            _activeLanguage = code;

            // Always load English as fallback
            _fallbackStrings = LoadEmbeddedLocale("en");

            if (code == "en")
            {
                _activeStrings = _fallbackStrings;
            }
            else
            {
                _activeStrings = LoadEmbeddedLocale(code);
            }

            Logger.Info($"[InstallerLocale] Loaded language: {code} ({_activeStrings.Count} active, {_fallbackStrings.Count} fallback strings)");
        }

        /// <summary>
        /// Load a locale JSON file from embedded resources.
        /// Resource names follow the pattern "locale.{code}.json".
        /// </summary>
        private static Dictionary<string, string> LoadEmbeddedLocale(string code)
        {
            var dict = new Dictionary<string, string>();

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = $"locale.{code}.json";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        Logger.Warning($"[InstallerLocale] Locale resource not found: {resourceName}");
                        return dict;
                    }

                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string json = reader.ReadToEnd();
                        ParseFlatJson(json, dict);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[InstallerLocale] Error loading locale '{code}': {ex.Message}");
            }

            return dict;
        }

        /// <summary>
        /// Parse a flat JSON object { "key": "value", ... } into the dictionary.
        /// Handles escaped characters in values.
        /// </summary>
        private static void ParseFlatJson(string json, Dictionary<string, string> dict)
        {
            int i = 0;
            int len = json.Length;

            // Skip to opening brace
            while (i < len && json[i] != '{') i++;
            i++; // skip '{'

            while (i < len)
            {
                // Skip whitespace and commas
                while (i < len && (char.IsWhiteSpace(json[i]) || json[i] == ',')) i++;

                if (i >= len || json[i] == '}') break;

                // Parse key
                string key = ParseJsonString(json, ref i);
                if (key == null) break;

                // Skip to colon
                while (i < len && json[i] != ':') i++;
                i++; // skip ':'

                // Skip whitespace
                while (i < len && char.IsWhiteSpace(json[i])) i++;

                // Parse value
                string value = ParseJsonString(json, ref i);
                if (value == null) break;

                dict[key] = value;
            }
        }

        /// <summary>
        /// Parse a JSON string literal starting at position i (expects opening quote).
        /// Handles \n, \t, \\, \", \uXXXX escape sequences.
        /// </summary>
        private static string ParseJsonString(string json, ref int i)
        {
            int len = json.Length;

            // Skip whitespace to find opening quote
            while (i < len && char.IsWhiteSpace(json[i])) i++;
            if (i >= len || json[i] != '"') return null;
            i++; // skip opening quote

            var sb = new StringBuilder();
            while (i < len)
            {
                char c = json[i];
                if (c == '"')
                {
                    i++; // skip closing quote
                    return sb.ToString();
                }
                if (c == '\\' && i + 1 < len)
                {
                    i++;
                    char esc = json[i];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 < len)
                            {
                                string hex = json.Substring(i + 1, 4);
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int code))
                                {
                                    sb.Append((char)code);
                                    i += 4;
                                }
                            }
                            break;
                        default:
                            sb.Append(esc);
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
                i++;
            }

            return sb.ToString();
        }
    }
}
