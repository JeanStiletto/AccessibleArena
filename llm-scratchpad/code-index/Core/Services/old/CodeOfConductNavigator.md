# CodeOfConductNavigator.cs
Path: src/Core/Services/old/CodeOfConductNavigator.cs
Lines: 255

## Top-level comments
- Navigator for terms/consent screens with multiple checkboxes. Adds custom "C" key to read scrollable content.

## public class CodeOfConductNavigator (line 14)
Extends BaseNavigator.

### Properties
- public override string NavigatorId => "CodeOfConduct" (line 16)
- public override string ScreenName => "Terms screen" (line 17)
- public override int Priority => 50 (line 18)

### Methods
- public CodeOfConductNavigator(IAnnouncementService announcer) (line 20)
- protected override bool DetectScreen() (line 22)
- private bool IsDeckSelectionScreen() (line 42)
- private bool IsSettingsMenuOpen() (line 61)
- protected override void DiscoverElements() (line 94)
- protected override string GetActivationAnnouncement() (line 119)
- protected override bool HandleCustomInput() (line 130) — Note: handles C key for ReadScrollableContent
- private List<Toggle> FindValidToggles() (line 140)
- private string FindToggleLabel(Toggle toggle, int index) (line 155)
- private void ReadScrollableContent() (line 207) — Note: announces via _announcer.AnnounceInterrupt, truncates at 500 chars
