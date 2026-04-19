# PanelInfo.cs - Code Index

## File-level Comment
Information about an active panel.
Provides canonical naming and behavior flags.
Also contains static utility methods for panel classification.

## Classes

### PanelInfo (line 12)
```csharp
public class PanelInfo
```

#### Static Methods - Panel Metadata
- public static PanelType ClassifyPanel(string panelName) (line 19)
  - Classifies a panel name into a PanelType
- public static int GetPriority(PanelType type) (line 46)
  - Priority for panel stacking (higher priority panels overlay lower ones)
- public static bool ShouldFilterNavigation(PanelType type, string panelName) (line 65)
  - Determines if a panel should filter navigation to only its children
- public static string GetDisplayName(string panelName, PanelType type) (line 95)
  - User-friendly display name for announcements
- private static string AddSpacesToCamelCase(string text) (line 127)
  - Helper: adds spaces before capitals for readability
- private static readonly HashSet<string> IgnoredPanels (line 147)
  - Panels that should never trigger rescans or be tracked (NavBar, TopBar)
- public static bool ShouldIgnorePanel(string panelName) (line 157)
  - Check if a panel should be ignored entirely

#### Properties
- public string Name { get; } (line 177) - Canonical name for the panel
- public PanelType Type { get; } (line 182) - The type of panel
- public GameObject GameObject { get; } (line 188) - The actual Unity GameObject
- public int Priority { get; } (line 194) - Priority for panel stacking
- public bool FiltersNavigation { get; } (line 200) - Whether this panel should filter navigation
- public string DisplayName { get; } (line 206) - User-friendly name for announcements
- public PanelDetectionMethod DetectedBy { get; } (line 212) - Which detection method discovered this panel
- public string RawGameObjectName (line 217) - Raw GameObject name (for debugging)

#### Constructor
- public PanelInfo(string name, PanelType type, GameObject gameObject, PanelDetectionMethod detectedBy) (line 219)

#### Methods
- public bool IsValid (line 237) - Check if panel is still valid (GameObject exists and is active)
- public override string ToString() (line 239)
- public override bool Equals(object obj) (line 244)
- public override int GetHashCode() (line 254)
