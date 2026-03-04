# PanelRegistry.cs - Code Index

## File-level Comment
Centralized knowledge about MTGA panels.
Consolidates all special-case handling that was previously scattered throughout the code.

## Static Classes

### PanelRegistry (line 9)
```csharp
public static class PanelRegistry
```

#### Static Methods - Detection Method Assignment
- public static PanelDetectionMethod GetDetectionMethod(string panelName) (line 17)
  - Determines which detection system should handle a panel
  - Each panel should be detected by exactly ONE system to prevent duplicates

#### Static Methods - Panel Type Classification
- public static PanelType ClassifyPanel(string panelName) (line 56)
  - Classifies a panel name into a PanelType

#### Static Methods - Priority
- public static int GetPriority(PanelType type) (line 99)
  - Priority for panel stacking (higher priority panels overlay lower ones)

#### Static Methods - Navigation Filtering
- public static bool FiltersNavigation(PanelType type, string panelName) (line 123)
  - Determines if a panel should filter navigation to only its children

#### Static Methods - Display Names
- public static string GetDisplayName(string panelName, PanelType type) (line 165)
  - User-friendly display name for announcements
- private static string AddSpacesToCamelCase(string text) (line 201)
  - Helper: adds spaces before capitals for readability

#### Static Methods - Panel Name Extraction
- public static string ExtractCanonicalName(string gameObjectName) (line 226)
  - Extracts canonical panel name from a GameObject name (removes suffixes)

#### Static Methods - Ignore Lists
- private static readonly HashSet<string> IgnoredPanels (line 254)
  - Panels that should never trigger rescans or be tracked (NavBar, TopBar)
- public static bool ShouldIgnorePanel(string panelName) (line 264)
  - Check if a panel should be ignored entirely
