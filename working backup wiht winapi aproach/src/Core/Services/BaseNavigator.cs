using UnityEngine;
using UnityEngine.UI;
using MelonLoader;
using MTGAAccessibility.Core.Interfaces;
using MTGAAccessibility.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// Base class for screen navigators. Handles common Tab/Enter navigation,
    /// element management, and announcements. Subclasses implement screen detection
    /// and element discovery.
    /// </summary>
    public abstract class BaseNavigator : IScreenNavigator
    {
        #region Fields

        protected readonly IAnnouncementService _announcer;
        protected readonly List<GameObject> _elements = new List<GameObject>();
        protected readonly List<string> _labels = new List<string>();
        protected int _currentIndex = -1;
        protected bool _isActive;

        #endregion

        #region Abstract Members (subclasses must implement)

        /// <summary>Unique ID for logging</summary>
        public abstract string NavigatorId { get; }

        /// <summary>Screen name announced to user (e.g., "Login screen")</summary>
        public abstract string ScreenName { get; }

        /// <summary>
        /// Check if this screen is currently displayed.
        /// Return true if this navigator should activate.
        /// Called only when navigator is not active.
        /// </summary>
        protected abstract bool DetectScreen();

        /// <summary>
        /// Populate _elements and _labels with navigable items.
        /// Called after DetectScreen() returns true.
        /// Use helper methods: AddElement(), AddButton(), AddToggle(), AddInputField()
        /// </summary>
        protected abstract void DiscoverElements();

        #endregion

        #region Virtual Members (subclasses can override)

        /// <summary>Priority for activation order. Higher = checked first.</summary>
        public virtual int Priority => 0;

        /// <summary>Additional keys this navigator handles (beyond Tab/Enter)</summary>
        protected virtual bool HandleCustomInput() => false;

        /// <summary>Called after activation, before first announcement</summary>
        protected virtual void OnActivated() { }

        /// <summary>Called when deactivating</summary>
        protected virtual void OnDeactivating() { }

        /// <summary>Called after element is activated. Return true to suppress default behavior.</summary>
        protected virtual bool OnElementActivated(int index, GameObject element) => false;

        /// <summary>Build the initial screen announcement</summary>
        protected virtual string GetActivationAnnouncement()
        {
            string countInfo = _elements.Count > 1 ? $"{_elements.Count} items. " : "";
            return $"{ScreenName}. {countInfo}Tab to navigate, Enter to select.";
        }

        /// <summary>Build announcement for current element</summary>
        protected virtual string GetElementAnnouncement(int index)
        {
            if (index < 0 || index >= _labels.Count) return "";

            var element = _elements[index];
            string label = _labels[index];

            // Add state info for toggles
            var toggle = element?.GetComponent<Toggle>();
            if (toggle != null)
            {
                string state = toggle.isOn ? "checked" : "unchecked";
                label = $"{label}, checkbox, {state}";
            }

            return $"{index + 1} of {_elements.Count}: {label}";
        }

        /// <summary>Whether to integrate with CardInfoNavigator</summary>
        protected virtual bool SupportsCardNavigation => true;

        /// <summary>Whether to accept Space key for activation (in addition to Enter)</summary>
        protected virtual bool AcceptSpaceKey => true;

        #endregion

        #region IScreenNavigator Implementation

        public bool IsActive => _isActive;
        public int ElementCount => _elements.Count;
        public int CurrentIndex => _currentIndex;

        public virtual void OnSceneChanged(string sceneName)
        {
            // Default: deactivate on scene change
            if (_isActive)
            {
                Deactivate();
            }
        }

        #endregion

        #region Constructor

        protected BaseNavigator(IAnnouncementService announcer)
        {
            _announcer = announcer;
        }

        #endregion

        #region Core Update Loop

        public virtual void Update()
        {
            // If not active, try to detect and activate
            if (!_isActive)
            {
                TryActivate();
                return;
            }

            // Verify elements still exist
            if (!ValidateElements())
            {
                Deactivate();
                return;
            }

            // Handle input
            HandleInput();
        }

        private void TryActivate()
        {
            if (!DetectScreen()) return;

            // Clear previous state
            _elements.Clear();
            _labels.Clear();
            _currentIndex = -1;

            // Discover elements
            DiscoverElements();

            if (_elements.Count == 0)
            {
                MelonLogger.Msg($"[{NavigatorId}] DetectScreen passed but no elements found");
                return;
            }

            // Activate
            _isActive = true;
            _currentIndex = 0;

            MelonLogger.Msg($"[{NavigatorId}] Activated with {_elements.Count} elements");

            OnActivated();

            // Announce screen
            _announcer.AnnounceInterrupt(GetActivationAnnouncement());

            // Prepare card navigation if supported
            if (SupportsCardNavigation)
            {
                PrepareCardNavigationForCurrentElement();
            }
        }

        protected virtual bool ValidateElements()
        {
            // Check if first element still exists (quick validation)
            return _elements.Count > 0 && _elements[0] != null;
        }

        public virtual void Deactivate()
        {
            if (!_isActive) return;

            MelonLogger.Msg($"[{NavigatorId}] Deactivating");

            OnDeactivating();

            _isActive = false;
            _elements.Clear();
            _labels.Clear();
            _currentIndex = -1;
        }

        #endregion

        #region Input Handling

        protected virtual void HandleInput()
        {
            // Custom input first (subclass-specific keys)
            if (HandleCustomInput()) return;

            // Standard navigation
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (shift)
                    MovePrevious();
                else
                    MoveNext();
                return;
            }

            // Activation
            bool enterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            bool spacePressed = AcceptSpaceKey && Input.GetKeyDown(KeyCode.Space);

            if (enterPressed || spacePressed)
            {
                ActivateCurrentElement();
            }
        }

        protected virtual void MoveNext()
        {
            if (_elements.Count == 0) return;

            _currentIndex = (_currentIndex + 1) % _elements.Count;
            AnnounceCurrentElement();

            if (SupportsCardNavigation)
            {
                PrepareCardNavigationForCurrentElement();
            }
        }

        protected virtual void MovePrevious()
        {
            if (_elements.Count == 0) return;

            _currentIndex = (_currentIndex - 1 + _elements.Count) % _elements.Count;
            AnnounceCurrentElement();

            if (SupportsCardNavigation)
            {
                PrepareCardNavigationForCurrentElement();
            }
        }

        protected virtual void AnnounceCurrentElement()
        {
            string announcement = GetElementAnnouncement(_currentIndex);
            if (!string.IsNullOrEmpty(announcement))
            {
                _announcer.AnnounceInterrupt(announcement);
            }
        }

        protected virtual void ActivateCurrentElement()
        {
            if (_currentIndex < 0 || _currentIndex >= _elements.Count) return;

            var element = _elements[_currentIndex];
            if (element == null) return;

            MelonLogger.Msg($"[{NavigatorId}] Activating: {element.name}");

            // Check if this is a card - delegate to CardInfoNavigator
            if (SupportsCardNavigation && CardDetector.IsCard(element))
            {
                if (MTGAAccessibilityMod.Instance?.ActivateCardDetails(element) == true)
                {
                    return; // Card navigation took over
                }
            }

            // Let subclass handle special activation
            if (OnElementActivated(_currentIndex, element))
            {
                return;
            }

            // Standard activation
            var result = UIActivator.Activate(element);

            // Announce result
            if (result.Type == ActivationType.Toggle)
            {
                _announcer.AnnounceInterrupt(result.Message);
            }
            else
            {
                _announcer.Announce(result.Message, AnnouncementPriority.Normal);
            }
        }

        #endregion

        #region Card Navigation Integration

        protected void PrepareCardNavigationForCurrentElement()
        {
            var cardNavigator = MTGAAccessibilityMod.Instance?.CardNavigator;
            if (cardNavigator == null) return;

            if (_currentIndex < 0 || _currentIndex >= _elements.Count)
            {
                cardNavigator.Deactivate();
                return;
            }

            var element = _elements[_currentIndex];
            if (element != null && CardDetector.IsCard(element))
            {
                cardNavigator.PrepareForCard(element);
            }
            else if (cardNavigator.IsActive)
            {
                cardNavigator.Deactivate();
            }
        }

        #endregion

        #region Element Discovery Helpers

        /// <summary>Add an element with a label</summary>
        protected void AddElement(GameObject element, string label)
        {
            if (element == null) return;
            _elements.Add(element);
            _labels.Add(label);
            MelonLogger.Msg($"[{NavigatorId}] Added: {label}");
        }

        /// <summary>Add a button, auto-extracting label from text</summary>
        protected void AddButton(GameObject buttonObj, string fallbackLabel = "Button")
        {
            if (buttonObj == null) return;

            string label = UITextExtractor.GetText(buttonObj);
            if (string.IsNullOrWhiteSpace(label) || label == "item")
            {
                label = fallbackLabel;
            }
            label = $"{label}, button";

            AddElement(buttonObj, label);
        }

        /// <summary>Add a toggle with label (state is added dynamically)</summary>
        protected void AddToggle(Toggle toggle, string label)
        {
            if (toggle == null) return;
            // State is added dynamically in GetElementAnnouncement
            AddElement(toggle.gameObject, label);
        }

        /// <summary>Add an input field</summary>
        protected void AddInputField(GameObject inputObj, string fieldName)
        {
            if (inputObj == null) return;
            AddElement(inputObj, $"{fieldName}, text field");
        }

        /// <summary>Find child by name recursively</summary>
        protected GameObject FindChildByName(Transform parent, string name)
        {
            if (parent == null) return null;

            var direct = parent.Find(name);
            if (direct != null) return direct.gameObject;

            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child.gameObject;

                var found = FindChildByName(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>Find child by path (e.g., "Parent/Child/Grandchild")</summary>
        protected GameObject FindChildByPath(Transform parent, string path)
        {
            if (parent == null || string.IsNullOrEmpty(path)) return null;

            var parts = path.Split('/');
            Transform current = parent;

            foreach (var part in parts)
            {
                current = current.Find(part);
                if (current == null) return null;
            }

            return current.gameObject;
        }

        /// <summary>Get cleaned button text</summary>
        protected string GetButtonText(GameObject buttonObj, string fallback = null)
        {
            var texts = buttonObj.GetComponentsInChildren<TMPro.TMP_Text>(true);
            foreach (var text in texts)
            {
                if (text == null) continue;
                string content = text.text?.Trim();
                if (string.IsNullOrEmpty(content) || content == "\u200B" || content.Length <= 1)
                    continue;

                // Remove rich text tags
                content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", "").Trim();
                if (!string.IsNullOrEmpty(content))
                    return content;
            }
            return fallback;
        }

        /// <summary>Truncate a label to reasonable length</summary>
        protected string TruncateLabel(string text, int maxLength = 80)
        {
            if (string.IsNullOrEmpty(text)) return text;

            text = System.Text.RegularExpressions.Regex.Replace(text.Trim(), "<[^>]+>", "");
            text = text.Trim();

            if (text.Length > maxLength)
                return text.Substring(0, maxLength - 3) + "...";

            return text;
        }

        #endregion
    }
}
