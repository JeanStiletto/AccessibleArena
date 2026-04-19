# LoginPanelNavigator.cs
Path: src/Core/Services/old/LoginPanelNavigator.cs
Lines: 158

## Top-level comments
- Navigator for the Login panel (email/password entry). Handles input fields with password masking and toggle state.

## public class LoginPanelNavigator (line 15)
Extends BaseNavigator.

### Fields
- private const string PANEL_NAME = "Panel - Log In_Desktop_16x9(Clone)" (line 17)
- private GameObject _panel (line 18)

### Properties
- public override string NavigatorId => "LoginPanel" (line 20)
- public override string ScreenName => "Login screen" (line 21)
- public override int Priority => 90 (line 22)

### Methods
- public LoginPanelNavigator(IAnnouncementService announcer) (line 24)
- protected override bool DetectScreen() (line 26)
- protected override void DiscoverElements() (line 32)
- protected override bool ValidateElements() (line 68)
- protected override string GetActivationAnnouncement() (line 73)
- protected override string GetElementAnnouncement(int index) (line 78) — Note: masks password fields by length, not content
- protected override void AnnounceCurrentElement() (line 124) — Note: skips SetSelectedGameObject for toggles to avoid triggering them
- protected override bool OnElementActivated(int index, GameObject element) (line 142)
