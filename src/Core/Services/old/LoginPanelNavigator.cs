using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for the Login panel (email/password entry).
    /// Handles input fields with proper password masking and toggle state.
    /// </summary>
    public class LoginPanelNavigator : BaseNavigator
    {
        private const string PANEL_NAME = "Panel - Log In_Desktop_16x9(Clone)";
        private GameObject _panel;

        public override string NavigatorId => "LoginPanel";
        public override string ScreenName => "Login screen";
        public override int Priority => 90; // High priority, after WelcomeGate

        public LoginPanelNavigator(IAnnouncementService announcer) : base(announcer) { }

        protected override bool DetectScreen()
        {
            _panel = GameObject.Find(PANEL_NAME);
            return _panel != null && _panel.activeInHierarchy;
        }

        protected override void DiscoverElements()
        {
            // Email field
            var emailField = FindChildByPath(_panel.transform, "InputsBox/Login_inputField Email/Input Field - E-mail");
            if (emailField != null)
                AddElement(emailField, "E-mail");

            // Password field
            var passwordField = FindChildByPath(_panel.transform, "InputsBox/Login_inputField Password/Input Field - PW");
            if (passwordField != null)
                AddElement(passwordField, "Password");

            // Remember me toggle
            var allToggles = _panel.GetComponentsInChildren<Toggle>(true);
            foreach (var toggle in allToggles)
            {
                if (toggle.gameObject.activeInHierarchy)
                {
                    AddElement(toggle.gameObject, "Remember me");
                    break;
                }
            }

            // Login button
            var loginButton = FindChildByName(_panel.transform, "MainButton_Login");
            if (loginButton != null)
                AddButton(loginButton, "Log In");

            // Back button
            var backButton = FindChildByName(_panel.transform, "Button_Back");
            if (backButton == null)
                backButton = FindChildByName(_panel.transform, "BackButton");
            if (backButton != null)
                AddButton(backButton, "Back");
        }

        protected override bool ValidateElements()
        {
            return _panel != null && _panel.activeInHierarchy && base.ValidateElements();
        }

        protected override string GetActivationAnnouncement()
        {
            return $"{ScreenName}. {Models.Strings.NavigateWithArrows}. {_elements.Count} fields.";
        }

        protected override string GetElementAnnouncement(int index)
        {
            if (index < 0 || index >= _elements.Count) return "";

            var navElement = _elements[index];
            var element = navElement.GameObject;
            string label = navElement.Label;

            // Handle input fields specially
            var tmpInput = element.GetComponent<TMP_InputField>();
            if (tmpInput != null)
            {
                string content = tmpInput.text;

                // Password field - mask content
                if (tmpInput.inputType == TMP_InputField.InputType.Password)
                {
                    if (string.IsNullOrEmpty(content))
                        label = $"{label}, empty";
                    else
                        label = $"{label}, has {content.Length} characters";
                }
                else
                {
                    // Regular field - show content
                    if (string.IsNullOrEmpty(content))
                        label = $"{label}, empty";
                    else
                        label = $"{label}: {content}";
                }

                return $"{label}, {index + 1} of {_elements.Count}";
            }

            // Handle toggles
            var toggle = element.GetComponent<Toggle>();
            if (toggle != null)
            {
                string state = toggle.isOn ? "checked" : "unchecked";
                return $"{label}, checkbox, {state}, {index + 1} of {_elements.Count}";
            }

            // Default for buttons
            return $"{label}, {index + 1} of {_elements.Count}";
        }

        protected override void AnnounceCurrentElement()
        {
            var element = _elements[_currentIndex].GameObject;

            // For toggles, don't call SetSelectedGameObject (it triggers them)
            var toggle = element?.GetComponent<Toggle>();
            if (toggle == null)
            {
                var selectable = element?.GetComponent<Selectable>();
                if (selectable != null && selectable.interactable)
                {
                    EventSystem.current?.SetSelectedGameObject(element);
                }
            }

            base.AnnounceCurrentElement();
        }

        protected override bool OnElementActivated(int index, GameObject element)
        {
            var result = UIActivator.Activate(element);

            // Toggles get interrupt announcement for immediate feedback
            if (result.Type == ActivationType.Toggle)
            {
                _announcer.AnnounceInterrupt(result.Message);
                return true;
            }

            _announcer.Announce(result.Message, AnnouncementPriority.Normal);
            return true;
        }
    }
}
