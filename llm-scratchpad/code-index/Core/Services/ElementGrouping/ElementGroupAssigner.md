# ElementGroupAssigner.cs
Path: src/Core/Services/ElementGrouping/ElementGroupAssigner.cs
Lines: 796

## Top-level comments
- Assigns UI elements to ElementGroup values by inspecting element name and parent hierarchy path string. Replaces scattered IsChildOf methods. Used by GroupedNavigator during element scans.

## public class ElementGroupAssigner (line 12)
### Fields
- private readonly OverlayDetector _overlayDetector (line 13)
- private int _profileButtonInstanceId (line 14)
### Methods
- public ElementGroupAssigner(OverlayDetector overlayDetector) (line 16)
- public void SetProfileButtonId(int instanceId) (line 25)
- public void ClearProfileButtonId() (line 26)
- public ElementGroup DetermineGroup(GameObject element) (line 33) — Note: Nav_Home always returns Unknown; overlay groups checked first; social panel elements not matched to a sub-group return Unknown
- private ElementGroup DetermineOverlayGroup(GameObject element, string name, string parentPath) (line 98)
- private bool IsPlayElement(GameObject element, string name, string parentPath) (line 236)
- private bool IsProgressElement(string name, string parentPath) (line 285)
- private bool IsObjectiveElement(string name, string parentPath) (line 326)
- private bool IsSocialElement(string name, string parentPath) (line 349)
- private bool IsPrimaryAction(GameObject element, string name, string parentPath) (line 373)
- private bool IsFilterElement(string name, string parentPath) (line 396)
- private bool IsSettingsControl(string name, string parentPath) (line 455)
- private ElementGroup DetermineFriendPanelGroup(GameObject element, string name, string parentPath) (line 478)
- private static bool IsPrimarySocialTileElement(GameObject element) (line 566)
- private static bool IsPrimaryChallengeTileElement(GameObject element) (line 596)
- private bool IsPlayBladeTab(string name, string parentPath) (line 629)
- private bool IsInsidePlayBlade(string parentPath, string name) (line 655) — Note: returns false for Mailbox and CampaignGraph paths even if they match blade name patterns
- private static bool IsChallengeContainer(string parentPath, string name) (line 685)
- private static string GetParentPath(GameObject element) (line 707)
- public static string GetFolderNameForDeck(GameObject element) (line 728)
- public static bool IsDeckElement(GameObject element, string label) (line 758)
- public static bool IsFolderToggle(GameObject element) (line 780)
- public static string GetFolderNameFromToggle(GameObject folderToggle) (line 790)
