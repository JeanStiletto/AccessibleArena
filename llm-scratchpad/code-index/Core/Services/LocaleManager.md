# LocaleManager.cs
Path: src/Core/Services/LocaleManager.cs
Lines: 402

## Top-level comments
- Singleton that loads and resolves localized strings from JSON files. Fallback chain: active language, then English, then key name.

## public class LocaleManager (line 12)

### Fields
- private static LocaleManager _instance (line 14)
- private Dictionary<string, string> _activeStrings = new Dictionary<string, string>() (line 17)
- private Dictionary<string, string> _fallbackStrings = new Dictionary<string, string>() (line 18)
- private string _activeLanguage = "en" (line 19)
- private static readonly string LangDir = Path.Combine("UserData", "AccessibleArena", "lang") (line 24)
- private static readonly Dictionary<string, PluralRule> PluralRules (line 29)
- private Dictionary<string, int> _numberWords (line 190)

### Properties
- public static LocaleManager Instance => _instance (line 15)

### Events
- public event Action OnLanguageChanged (line 22)

### Nested Types
- private enum PluralRule { OneOther, Slavic, NoPluralForm } (line 27)

### Methods
- public static void Initialize(string languageCode) (line 49)
- internal static LocaleManager CreateForTesting(Dictionary<string, string> activeStrings, Dictionary<string, string> fallbackStrings = null, string language = "en") (line 58)
- public void SetLanguage(string code) (line 73)
- public string Get(string key) (line 83)
- public string Format(string key, params object[] args) (line 95)
- public string Plural(int count, string baseKey, params object[] extraArgs) (line 114) — Picks singular or plural form; uses Slavic rules for ru/pl, none for ja/ko/zh
- public bool HasKey(string key) (line 153)
- public int TryParseNumberWord(string text) (line 164)
- private Dictionary<string, int> BuildNumberWordMap() (line 192)
- private void LoadLanguage(string code) (line 212)
- private static Dictionary<string, string> LoadJsonFile(string path) (line 236)
- internal static void ParseFlatJson(string json, Dictionary<string, string> dict) (line 263)
- private static string ParseJsonString(string json, ref int i) (line 302)
- public static void EnsureDefaultLocaleFiles() (line 363) — Extracts embedded lang resources to UserData, overwrites on every call
