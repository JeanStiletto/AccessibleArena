# PanelInfo.cs
Path: src/Core/Services/PanelDetection/PanelInfo.cs
Lines: 259

## Top-level comments
- Immutable record holding info about an active panel. Contains static utility methods for classification, priority, display name, navigation filtering, and ignore decisions.

## public class PanelInfo (line 13)
### Fields
- private static readonly HashSet<string> IgnoredPanels (line 147)
### Properties
- public string Name { get; } (line 177)
- public PanelType Type { get; } (line 182)
- public GameObject GameObject { get; } (line 188)
- public int Priority { get; } (line 194)
- public bool FiltersNavigation { get; } (line 200)
- public string DisplayName { get; } (line 206)
- public PanelDetectionMethod DetectedBy { get; } (line 212)
- public string RawGameObjectName => GameObject?.name ?? "null" (line 217)
- public bool IsValid => GameObject != null && GameObject.activeInHierarchy (line 237)
### Methods
- public static PanelType ClassifyPanel(string panelName) (line 19)
- public static int GetPriority(PanelType type) (line 46)
- public static bool ShouldFilterNavigation(PanelType type, string panelName) (line 65)
- public static string GetDisplayName(string panelName, PanelType type) (line 95)
- private static string AddSpacesToCamelCase(string text) (line 127)
- public static bool ShouldIgnorePanel(string panelName) (line 157)
- public PanelInfo(string name, PanelType type, GameObject gameObject, PanelDetectionMethod detectedBy) (line 219)
- public override string ToString() (line 239)
- public override bool Equals(object obj) (line 244)
- public override int GetHashCode() (line 252)
