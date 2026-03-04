# ElementGroupAssigner.cs - Code Index

## File-level Comment
Assigns UI elements to groups based on their parent hierarchy and name patterns.
Replaces scattered IsChildOf... methods with unified pattern matching.

## Classes

### ElementGroupAssigner (line 10)
```csharp
public class ElementGroupAssigner
```

#### Fields
- private readonly OverlayDetector _overlayDetector (line 12)
- private int _profileButtonInstanceId (line 13)

#### Constructor
- public ElementGroupAssigner(OverlayDetector overlayDetector) (line 15)

#### Methods - Profile Button ID Management
- public void SetProfileButtonId(int instanceId) (line 24)
  - Register local player's StatusButton instance ID for FriendsPanelProfile group
- public void ClearProfileButtonId() (line 25)

#### Methods - Group Assignment
- public ElementGroup DetermineGroup(GameObject element) (line 32)
  - Determine which group an element belongs to based on name and parent hierarchy
- private ElementGroup DetermineOverlayGroup(GameObject element, string name, string parentPath) (line 97)
  - Check if element belongs to an overlay group
- private bool IsPlayElement(GameObject element, string name, string parentPath) (line 224)
  - Check if element is Play-related (Play button, events, direct challenge, rankings, learn)
- private bool IsProgressElement(string name, string parentPath) (line 274)
  - Check if element is Progress-related (boosters, mastery, gems, gold, wildcards, tokens)
- private bool IsObjectiveElement(string name, string parentPath) (line 315)
  - Check if element is an Objective element (daily wins, weekly wins, quests, battle pass)
- private bool IsSocialElement(string name, string parentPath) (line 337)
  - Check if element is a Social element (profile, achievements, mail/notifications)
- private bool IsPrimaryAction(GameObject element, string name, string parentPath) (line 361)
  - Check if element is a primary action button (main CTA, but not Play button)
- private bool IsFilterElement(string name, string parentPath) (line 383)
  - Check if element is a filter control
- private bool IsSettingsControl(string name, string parentPath) (line 443)
  - Check if element is a settings control
- private bool IsSecondaryAction(string name, string parentPath) (line 465)
  - Check if element is a secondary action

#### Methods - Friend Panel Group Assignment
- private ElementGroup DetermineFriendPanelGroup(GameObject element, string name, string parentPath) (line 490)
  - Determine which friend panel sub-group an element belongs to
- private static bool IsPrimarySocialTileElement(GameObject element) (line 566)
  - Check if element is the primary clickable for its social tile

#### Methods - PlayBlade Detection
- private bool IsPlayBladeTab(string name, string parentPath) (line 595)
  - Check if element is a PlayBlade tab
- private bool IsInsidePlayBlade(string parentPath, string name) (line 620)
  - Check if element is inside the Play Blade
- private static bool IsChallengeContainer(string parentPath, string name) (line 650)
  - Check if element is inside a challenge screen container

#### Methods - Utility
- private static string GetParentPath(GameObject element) (line 672)
  - Get the full parent path of an element as a concatenated string
- public static string GetFolderNameForDeck(GameObject element) (line 693)
  - For deck elements, extract the folder name from the parent hierarchy
- public static bool IsDeckElement(GameObject element, string label) (line 723)
  - Check if an element is a deck entry
- public static bool IsFolderToggle(GameObject element) (line 746)
  - Check if an element is a folder toggle
- public static string GetFolderNameFromToggle(GameObject folderToggle) (line 755)
  - Get the folder name from a folder toggle element
