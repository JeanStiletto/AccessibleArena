# LocaleManager.cs

## Summary
Singleton that loads and resolves localized strings from JSON files. Fallback chain: active language -> English -> key name.

## Classes

### LocaleManager (line 12)
```
public class LocaleManager
  private static LocaleManager _instance (line 14)
  public static LocaleManager Instance => _instance (line 15)

  private Dictionary<string, string> _activeStrings (line 17)
  private Dictionary<string, string> _fallbackStrings (line 18)
  private string _activeLanguage (line 19)
  public event Action OnLanguageChanged (line 22)

  private static readonly string LangDir (line 24)
  private enum PluralRule { OneOther, Slavic, NoPluralForm } (line 27)
  private static readonly Dictionary<string, PluralRule> PluralRules (line 29)

  public static void Initialize(string languageCode) (line 49)
  public void SetLanguage(string code) (line 58)
  public string Get(string key) (line 68)
  public string Format(string key, params object[] args) (line 80)
  public string Plural(int count, string baseKey, params object[] extraArgs) (line 99)
  public bool HasKey(string key) (line 138)
  public int TryParseNumberWord(string text) (line 149)

  private Dictionary<string, int> _numberWords (line 175)
  private Dictionary<string, int> BuildNumberWordMap() (line 177)
  private void LoadLanguage(string code) (line 197)
  private static Dictionary<string, string> LoadJsonFile(string path) (line 221)
  private static void ParseFlatJson(string json, Dictionary<string, string> dict) (line 248)
  private static string ParseJsonString(string json, ref int i) (line 287)
  public static void EnsureDefaultLocaleFiles() (line 348)
```
