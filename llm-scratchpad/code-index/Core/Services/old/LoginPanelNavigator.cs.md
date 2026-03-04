# LoginPanelNavigator.cs - Code Index

## File-level Comment
Navigator for the Login panel (email/password entry).
Handles input fields with proper password masking and toggle state.

## Classes

### LoginPanelNavigator (line 15)
```csharp
public class LoginPanelNavigator : BaseNavigator
```

#### Fields
- private const string PANEL_NAME = "Panel - Log In_Desktop_16x9(Clone)" (line 17)
- private GameObject _panel (line 18)

#### Properties (Override)
- public override string NavigatorId (line 20) - Returns "LoginPanel"
- public override string ScreenName (line 21) - Returns "Login screen"
- public override int Priority (line 22) - Returns 90 (high priority, after WelcomeGate)

#### Constructor
- public LoginPanelNavigator(IAnnouncementService announcer) : base(announcer) (line 24)

#### Methods (Override)
- protected override bool DetectScreen() (line 26)
  - Looks for "Panel - Log In_Desktop_16x9(Clone)"
- protected override void DiscoverElements() (line 33)
  - Email field, Password field, Remember me toggle, Login button, Back button
- protected override bool ValidateElements() (line 68)
- protected override string GetActivationAnnouncement() (line 73)
- protected override string GetElementAnnouncement(int index) (line 78)
  - Note: Handles input fields specially (password masking)
- protected override void AnnounceCurrentElement() (line 124)
  - Note: For toggles, don't call SetSelectedGameObject (it triggers them)
- protected override bool OnElementActivated(int index, GameObject element) (line 142)
  - Note: Toggles get interrupt announcement for immediate feedback
