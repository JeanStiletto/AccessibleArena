# WelcomeGateNavigator.cs - Code Index

## File-level Comment
Navigator for the WelcomeGate (login/register choice) screen.
Uses image-based buttons that don't work with standard Unity UI navigation.

## Classes

### WelcomeGateNavigator (line 10)
```csharp
public class WelcomeGateNavigator : BaseNavigator
```

#### Fields
- private const string PANEL_NAME = "Panel - WelcomeGate_Desktop_16x9(Clone)" (line 12)
- private GameObject _panel (line 13)

#### Properties (Override)
- public override string NavigatorId (line 15) - Returns "WelcomeGate"
- public override string ScreenName (line 16) - Returns "Welcome screen"
- public override int Priority (line 17) - Returns 100 (high priority - check early in login flow)

#### Constructor
- public WelcomeGateNavigator(IAnnouncementService announcer) : base(announcer) (line 19)

#### Methods (Override)
- protected override bool DetectScreen() (line 21)
  - Looks for "Panel - WelcomeGate_Desktop_16x9(Clone)"
- protected override void DiscoverElements() (line 27)
  - Log In button, Register button, Need Help button
- protected override bool ValidateElements() (line 42)
- protected override string GetActivationAnnouncement() (line 48)
