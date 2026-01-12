using UnityEngine;
using MTGAAccessibility.Core.Interfaces;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// Navigator for the WelcomeGate (login/register choice) screen.
    /// Uses image-based buttons that don't work with standard Unity UI navigation.
    /// </summary>
    public class WelcomeGateNavigator : BaseNavigator
    {
        private const string PANEL_NAME = "Panel - WelcomeGate_Desktop_16x9(Clone)";
        private GameObject _panel;

        public override string NavigatorId => "WelcomeGate";
        public override string ScreenName => "Welcome screen";
        public override int Priority => 100; // High priority - check early in login flow

        public WelcomeGateNavigator(IAnnouncementService announcer) : base(announcer) { }

        protected override bool DetectScreen()
        {
            _panel = GameObject.Find(PANEL_NAME);
            return _panel != null && _panel.activeInHierarchy;
        }

        protected override void DiscoverElements()
        {
            var loginButton = FindChildByName(_panel.transform, "Button_Login");
            if (loginButton != null)
                AddButton(loginButton, "Log In");

            var registerButton = FindChildByName(_panel.transform, "Button_Register");
            if (registerButton != null)
                AddButton(registerButton, "Register");

            var helpButton = FindChildByName(_panel.transform, "Text_NeedHelp");
            if (helpButton != null)
                AddButton(helpButton, "Need Help");
        }

        protected override bool ValidateElements()
        {
            return _panel != null && _panel.activeInHierarchy && base.ValidateElements();
        }

        protected override string GetActivationAnnouncement()
        {
            return $"{ScreenName}. {_elements.Count} options. Press Tab to navigate, Enter to select.";
        }
    }
}
