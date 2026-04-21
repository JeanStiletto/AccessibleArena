# ModSettings.cs
Path: src/Core/Services/ModSettings.cs
Lines: 209

## Top-level comments
- Mod settings with JSON file persistence. Settings are saved to UserData/AccessibleArena.json in the game directory.

## public class ModSettings (line 12)

### Fields
- private static readonly string SettingsPath = Path.Combine("UserData", "AccessibleArena.json") (line 14)
- public static readonly string[] LanguageCodes (line 17)
- private static readonly string[] LanguageKeys (line 20)

### Properties
- public string Language { get; set; } = "en" (line 23)
- public bool TutorialMessages { get; set; } = true (line 24)
- public bool VerboseAnnouncements { get; set; } = true (line 25)
- public bool BriefCastAnnouncements { get; set; } = true (line 26)
- public bool BriefOpponentAnnouncements { get; set; } = false (line 27)
- public bool PhaseSkipWarning { get; set; } = true (line 28)
- public bool PositionCounts { get; set; } = true (line 29)
- public bool ManaColorlessLabel { get; set; } = true (line 30)
- public bool ManaGroupColors { get; set; } = true (line 31)
- public bool CheckForUpdates { get; set; } = true (line 32)

### Events
- public event Action OnLanguageChanged (line 93)

### Methods
- public static ModSettings Load() (line 37)
- public void Save() (line 64)
- public string GetLanguageDisplayName() (line 87)
- public void SetLanguage(string code) (line 99)
- public void CycleLanguage() (line 111)
- public static int GetLanguageIndex(string code) (line 121)
- public static string GetLanguageDisplayName(int index) (line 130)
- private string ToJson() (line 137)
- private void ParseJson(string json) (line 154)
- private static string ReadJsonString(string json, string key) (line 169)
- private static bool? ReadJsonBool(string json, string key) (line 187)
- private static string EscapeJson(string value) (line 203)
