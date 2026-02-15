using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Singleton that loads and resolves localized strings from JSON files.
    /// Fallback chain: active language -> English -> key name.
    /// </summary>
    public class LocaleManager
    {
        private static LocaleManager _instance;
        public static LocaleManager Instance => _instance;

        private Dictionary<string, string> _activeStrings = new Dictionary<string, string>();
        private Dictionary<string, string> _fallbackStrings = new Dictionary<string, string>();
        private string _activeLanguage = "en";

        /// <summary>Fired when the active language changes.</summary>
        public event Action OnLanguageChanged;

        private static readonly string LangDir = Path.Combine("UserData", "AccessibleArena", "lang");

        // Plural rules per language family
        private enum PluralRule { OneOther, Slavic, NoPluralForm }

        private static readonly Dictionary<string, PluralRule> PluralRules = new Dictionary<string, PluralRule>
        {
            { "en", PluralRule.OneOther },
            { "de", PluralRule.OneOther },
            { "fr", PluralRule.OneOther },
            { "es", PluralRule.OneOther },
            { "it", PluralRule.OneOther },
            { "pt-BR", PluralRule.OneOther },
            { "ru", PluralRule.Slavic },
            { "pl", PluralRule.Slavic },
            { "ja", PluralRule.NoPluralForm },
            { "ko", PluralRule.NoPluralForm },
            { "zh-CN", PluralRule.NoPluralForm },
            { "zh-TW", PluralRule.NoPluralForm }
        };

        /// <summary>
        /// Initialize the singleton and load the specified language.
        /// Must be called once early in mod startup.
        /// </summary>
        public static void Initialize(string languageCode)
        {
            _instance = new LocaleManager();
            _instance.LoadLanguage(languageCode);
        }

        /// <summary>
        /// Switch to a different language. Reloads strings and fires OnLanguageChanged.
        /// </summary>
        public void SetLanguage(string code)
        {
            if (code == _activeLanguage) return;
            LoadLanguage(code);
            OnLanguageChanged?.Invoke();
        }

        /// <summary>
        /// Get a localized string by key. Falls back to English, then to the key name itself.
        /// </summary>
        public string Get(string key)
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
        public string Format(string key, params object[] args)
        {
            string template = Get(key);
            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                // Template didn't have the right placeholders - return as-is
                return template;
            }
        }

        /// <summary>
        /// Pick singular or plural form based on count.
        /// Looks for Key_One (count==1) and Key_Format (plural/format string).
        /// For languages with no plural forms, always uses Key_Format.
        /// </summary>
        public string Plural(int count, string baseKey, params object[] extraArgs)
        {
            PluralRule rule = PluralRules.TryGetValue(_activeLanguage, out var r) ? r : PluralRule.OneOther;

            string chosenKey;
            switch (rule)
            {
                case PluralRule.NoPluralForm:
                    chosenKey = baseKey + "_Format";
                    break;
                case PluralRule.Slavic:
                    // Slavic: 1 = One, 2-4 (not 12-14) = Few, else = Format
                    int mod10 = count % 10;
                    int mod100 = count % 100;
                    if (count == 1)
                        chosenKey = baseKey + "_One";
                    else if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14))
                        chosenKey = HasKey(baseKey + "_Few") ? baseKey + "_Few" : baseKey + "_Format";
                    else
                        chosenKey = baseKey + "_Format";
                    break;
                default: // OneOther
                    chosenKey = count == 1 ? baseKey + "_One" : baseKey + "_Format";
                    break;
            }

            // Fall back to _Format if chosen key doesn't exist
            if (!HasKey(chosenKey))
                chosenKey = baseKey + "_Format";

            // Build args array: count first, then any extra args
            object[] allArgs = new object[1 + extraArgs.Length];
            allArgs[0] = count;
            Array.Copy(extraArgs, 0, allArgs, 1, extraArgs.Length);

            return Format(chosenKey, allArgs);
        }

        /// <summary>Check if a key exists in active or fallback dictionaries.</summary>
        public bool HasKey(string key)
        {
            return _activeStrings.ContainsKey(key) || _fallbackStrings.ContainsKey(key);
        }

        private void LoadLanguage(string code)
        {
            _activeLanguage = code;

            // Always load English as fallback
            _fallbackStrings = LoadJsonFile(Path.Combine(LangDir, "en.json"));

            if (code == "en")
            {
                _activeStrings = _fallbackStrings;
            }
            else
            {
                _activeStrings = LoadJsonFile(Path.Combine(LangDir, $"{code}.json"));
            }

            MelonLogger.Msg($"[LocaleManager] Loaded language: {code} ({_activeStrings.Count} active, {_fallbackStrings.Count} fallback strings)");
        }

        /// <summary>
        /// Hand-written JSON parser for flat key-value string files.
        /// No external dependencies.
        /// </summary>
        private static Dictionary<string, string> LoadJsonFile(string path)
        {
            var dict = new Dictionary<string, string>();

            try
            {
                if (!File.Exists(path))
                {
                    MelonLogger.Warning($"[LocaleManager] Locale file not found: {path}");
                    return dict;
                }

                string json = File.ReadAllText(path);
                ParseFlatJson(json, dict);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[LocaleManager] Error loading {path}: {ex.Message}");
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

            var sb = new System.Text.StringBuilder();
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

        /// <summary>
        /// Ensure the lang directory exists and write the default en.json if it's missing.
        /// Call this during mod initialization.
        /// </summary>
        public static void EnsureDefaultLocaleFile(string defaultEnJson)
        {
            try
            {
                if (!Directory.Exists(LangDir))
                {
                    Directory.CreateDirectory(LangDir);
                    MelonLogger.Msg($"[LocaleManager] Created lang directory: {LangDir}");
                }

                string enPath = Path.Combine(LangDir, "en.json");
                if (!File.Exists(enPath))
                {
                    File.WriteAllText(enPath, defaultEnJson);
                    MelonLogger.Msg($"[LocaleManager] Wrote default en.json");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[LocaleManager] Could not ensure default locale: {ex.Message}");
            }
        }
    }
}
