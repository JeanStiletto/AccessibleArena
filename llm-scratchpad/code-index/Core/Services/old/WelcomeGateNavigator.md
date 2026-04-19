# WelcomeGateNavigator.cs
Path: src/Core/Services/old/WelcomeGateNavigator.cs
Lines: 53

## Top-level comments
- Navigator for the WelcomeGate (login/register choice) screen. Uses image-based buttons that don't work with standard Unity UI navigation.

## public class WelcomeGateNavigator (line 10)
Extends BaseNavigator.

### Fields
- private const string PANEL_NAME = "Panel - WelcomeGate_Desktop_16x9(Clone)" (line 12)
- private GameObject _panel (line 13)

### Properties
- public override string NavigatorId => "WelcomeGate" (line 15)
- public override string ScreenName => "Welcome screen" (line 16)
- public override int Priority => 100 (line 17)

### Methods
- public WelcomeGateNavigator(IAnnouncementService announcer) (line 19)
- protected override bool DetectScreen() (line 21)
- protected override void DiscoverElements() (line 27)
- protected override bool ValidateElements() (line 42)
- protected override string GetActivationAnnouncement() (line 47)
