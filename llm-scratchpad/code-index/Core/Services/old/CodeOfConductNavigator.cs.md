# CodeOfConductNavigator.cs - Code Index

## File-level Comment
Navigator for terms/consent screens with multiple checkboxes.
Adds custom "C" key to read scrollable content.

## Classes

### CodeOfConductNavigator (line 14)
```csharp
public class CodeOfConductNavigator : BaseNavigator
```

#### Properties (Override)
- public override string NavigatorId (line 16) - Returns "CodeOfConduct"
- public override string ScreenName (line 17) - Returns "Terms screen"
- public override int Priority (line 18) - Returns 50 (medium priority)

#### Constructor
- public CodeOfConductNavigator(IAnnouncementService announcer) : base(announcer) (line 20)

#### Methods (Override)
- protected override bool DetectScreen() (line 22)
  - Skip if other screens are present (WelcomeGate, Login, Settings, Deck selection)
  - Detect by presence of multiple toggles (>= 2)
- protected override void DiscoverElements() (line 94)
  - Find toggles and accept/continue button
- protected override string GetActivationAnnouncement() (line 119)
  - Includes "Press C to read terms" instruction
- protected override bool HandleCustomInput() (line 131)
  - C key to read scrollable content

#### Methods - Helper
- private bool IsDeckSelectionScreen() (line 42)
  - Check if this is a deck selection screen (Play menu with deck folders)
- private bool IsSettingsMenuOpen() (line 61)
  - Check if the Settings menu is currently open
- private List<Toggle> FindValidToggles() (line 140)
  - Find all valid toggles (exclude dropdowns/items)
- private string FindToggleLabel(Toggle toggle, int index) (line 155)
  - Extract label for a toggle (multiple strategies: sibling text, children, grandparent)
- private void ReadScrollableContent() (line 207)
  - Read scrollable content from ScrollRect or fallback to any long TMP_Text
