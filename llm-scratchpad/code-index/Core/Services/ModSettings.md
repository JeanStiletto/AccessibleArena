# ModSettings.cs

## Summary
Mod settings with JSON file persistence. Settings are saved to UserData/AccessibleArena.json in the game directory.

## Classes

### ModSettings (line 12)
```
public class ModSettings
  private static readonly string SettingsPath (line 14)
  public static readonly string[] LanguageCodes (line 17)
  private static readonly string[] LanguageKeys (line 20)

  // Settings
  public string Language { get; set; } (line 23)
  public bool TutorialMessages { get; set; } (line 24)
  public bool VerboseAnnouncements { get; set; } (line 25)
  public bool BriefCastAnnouncements { get; set; } (line 26)

  public static ModSettings Load() (line 31)
  public void Save() (line 58)
  public string GetLanguageDisplayName() (line 82)
  public event Action OnLanguageChanged (line 87)
  public void SetLanguage(string code) (line 93)
  public void CycleLanguage() (line 105)
  public static int GetLanguageIndex(string code) (line 115)
  public static string GetLanguageDisplayName(int index) (line 124)
  public static string DetectGameLanguage() (line 135)

  private string ToJson() (line 193)
  private void ParseJson(string json) (line 204)
  private static string ReadJsonString(string json, string key) (line 213)
  private static bool? ReadJsonBool(string json, string key) (line 231)
  private static string EscapeJson(string value) (line 247)
```
