using AccessibleArena.Contexts.Base;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Contexts.Login
{
    /// <summary>
    /// Context for the login scene. Handles multiple panels:
    /// - RegisterOrLoginPanel: First choice (login or register)
    /// - BirthLanguagePanel: Age gate / language selection
    /// - LoginPanel: Email/password entry
    /// - RegistrationPanel: Account creation
    /// - ForgotCredentialsPanel: Password reset
    /// - LoginQueuePanel: Server queue
    /// - HelpPanel: Support
    ///
    /// Game classes: Wotc.Mtga.Login namespace (LoginScene, LoginPanel, etc.)
    /// </summary>
    public class LoginContext : BaseMenuContext
    {
        private readonly IContextManager _contextManager;
        private LoginPanelType _currentPanel = LoginPanelType.RegisterOrLogin;

        public override string ContextName => GetPanelName();

        public LoginContext(IAnnouncementService announcer, IContextManager contextManager)
            : base(announcer)
        {
            _contextManager = contextManager;
        }

        public override void Refresh()
        {
            ClearItems();

            switch (_currentPanel)
            {
                case LoginPanelType.RegisterOrLogin:
                    BuildRegisterOrLoginPanel();
                    break;
                case LoginPanelType.AgeGate:
                    BuildAgeGatePanel();
                    break;
                case LoginPanelType.Login:
                    BuildLoginPanel();
                    break;
                case LoginPanelType.Registration:
                    BuildRegistrationPanel();
                    break;
                case LoginPanelType.ForgotCredentials:
                    BuildForgotCredentialsPanel();
                    break;
                case LoginPanelType.Queue:
                    BuildQueuePanel();
                    break;
                case LoginPanelType.Help:
                    BuildHelpPanel();
                    break;
            }
        }

        private string GetPanelName()
        {
            return _currentPanel switch
            {
                LoginPanelType.RegisterOrLogin => "Welcome",
                LoginPanelType.AgeGate => "Age Verification",
                LoginPanelType.Login => "Login",
                LoginPanelType.Registration => "Create Account",
                LoginPanelType.ForgotCredentials => "Password Reset",
                LoginPanelType.Queue => "Login Queue",
                LoginPanelType.Help => "Help",
                _ => "Login"
            };
        }

        private void BuildRegisterOrLoginPanel()
        {
            AddMenuItem("Log In", OnLoginSelected, "Sign in with existing account");
            AddMenuItem("Create Account", OnRegisterSelected, "Create a new Wizards account");
            AddMenuItem("Help", OnHelpSelected, "Get help and support");
        }

        private void BuildAgeGatePanel()
        {
            AddMenuItem("Birth Year", OnBirthYearSelected, "Select your birth year");
            AddMenuItem("Birth Month", OnBirthMonthSelected, "Select your birth month");
            AddMenuItem("Birth Day", OnBirthDaySelected, "Select your birth day");
            AddMenuItem("Country", OnCountrySelected, "Select your country");
            AddMenuItem("Continue", OnAgeGateContinue, "Proceed after entering information");
        }

        private void BuildLoginPanel()
        {
            AddMenuItem("Email", OnEmailFieldSelected, "Enter your email address");
            AddMenuItem("Password", OnPasswordFieldSelected, "Enter your password");
            AddMenuItem("Log In", OnSubmitLogin, "Sign in to your account");
            AddMenuItem("Forgot Password", OnForgotPasswordSelected, "Reset your password");
            AddMenuItem("Back", OnBackToWelcome, "Return to welcome screen");
        }

        private void BuildRegistrationPanel()
        {
            AddMenuItem("Email", OnEmailFieldSelected, "Enter your email address");
            AddMenuItem("Password", OnPasswordFieldSelected, "Create a password");
            AddMenuItem("Confirm Password", OnConfirmPasswordSelected, "Confirm your password");
            AddMenuItem("Accept Terms", OnAcceptTermsSelected, "Read and accept terms of service");
            AddMenuItem("Create Account", OnSubmitRegistration, "Create your account");
            AddMenuItem("Back", OnBackToWelcome, "Return to welcome screen");
        }

        private void BuildForgotCredentialsPanel()
        {
            AddMenuItem("Email", OnEmailFieldSelected, "Enter your email address");
            AddMenuItem("Submit", OnSubmitPasswordReset, "Request password reset");
            AddMenuItem("Back", OnBackToLogin, "Return to login");
        }

        private void BuildQueuePanel()
        {
            AddMenuItem("Queue Position", OnQueuePositionAnnounce, "Hear your position in queue");
            AddMenuItem("Cancel", OnCancelQueue, "Leave the queue");
        }

        private void BuildHelpPanel()
        {
            AddMenuItem("Support Website", OnSupportWebsite, "Open support website");
            AddMenuItem("Back", OnBackToWelcome, "Return to welcome screen");
        }

        public void SetPanel(LoginPanelType panelType)
        {
            _currentPanel = panelType;
            if (IsActive)
            {
                Refresh();
                AnnounceContext();
            }
        }

        private void OnLoginSelected()
        {
            SetPanel(LoginPanelType.Login);
        }

        private void OnRegisterSelected()
        {
            SetPanel(LoginPanelType.AgeGate);
        }

        private void OnHelpSelected()
        {
            SetPanel(LoginPanelType.Help);
        }

        private void OnBirthYearSelected()
        {
            Announcer.Announce(Core.Models.Strings.BirthYearField);
        }

        private void OnBirthMonthSelected()
        {
            Announcer.Announce(Core.Models.Strings.BirthMonthField);
        }

        private void OnBirthDaySelected()
        {
            Announcer.Announce(Core.Models.Strings.BirthDayField);
        }

        private void OnCountrySelected()
        {
            Announcer.Announce(Core.Models.Strings.CountryField);
        }

        private void OnAgeGateContinue()
        {
            SetPanel(LoginPanelType.Registration);
        }

        private void OnEmailFieldSelected()
        {
            Announcer.Announce(Core.Models.Strings.EmailField);
        }

        private void OnPasswordFieldSelected()
        {
            Announcer.Announce(Core.Models.Strings.PasswordField);
        }

        private void OnConfirmPasswordSelected()
        {
            Announcer.Announce(Core.Models.Strings.ConfirmPasswordField);
        }

        private void OnAcceptTermsSelected()
        {
            Announcer.Announce(Core.Models.Strings.AcceptTermsCheckbox);
        }

        private void OnSubmitLogin()
        {
            Announcer.Announce(Core.Models.Strings.LoggingIn);
        }

        private void OnSubmitRegistration()
        {
            Announcer.Announce(Core.Models.Strings.CreatingAccount);
        }

        private void OnForgotPasswordSelected()
        {
            SetPanel(LoginPanelType.ForgotCredentials);
        }

        private void OnSubmitPasswordReset()
        {
            Announcer.Announce(Core.Models.Strings.SubmittingPasswordReset);
        }

        private void OnQueuePositionAnnounce()
        {
            Announcer.Announce(Core.Models.Strings.CheckingQueuePosition);
        }

        private void OnCancelQueue()
        {
            SetPanel(LoginPanelType.RegisterOrLogin);
        }

        private void OnSupportWebsite()
        {
            Announcer.Announce(Core.Models.Strings.OpeningSupportWebsite);
        }

        private void OnBackToWelcome()
        {
            SetPanel(LoginPanelType.RegisterOrLogin);
        }

        private void OnBackToLogin()
        {
            SetPanel(LoginPanelType.Login);
        }

        public override void Cancel()
        {
            switch (_currentPanel)
            {
                case LoginPanelType.Login:
                case LoginPanelType.Registration:
                case LoginPanelType.Help:
                    SetPanel(LoginPanelType.RegisterOrLogin);
                    break;
                case LoginPanelType.AgeGate:
                    SetPanel(LoginPanelType.RegisterOrLogin);
                    break;
                case LoginPanelType.ForgotCredentials:
                    SetPanel(LoginPanelType.Login);
                    break;
                default:
                    base.Cancel();
                    break;
            }
        }
    }

    public enum LoginPanelType
    {
        RegisterOrLogin,
        AgeGate,
        Login,
        Registration,
        ForgotCredentials,
        Queue,
        Help
    }
}
