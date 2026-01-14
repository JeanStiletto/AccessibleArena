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
        protected readonly List<NavigableElement> _elements = new List<NavigableElement>();
        protected int _currentIndex = -1;
        protected bool _isActive;

        /// <summary>
        /// Represents a navigable UI element with its label and optional carousel info
        /// </summary>
        protected struct NavigableElement
        {
            public GameObject GameObject { get; set; }
            public string Label { get; set; }
            public CarouselInfo Carousel { get; set; }
            /// <summary>Optional alternate action object (e.g., edit button for deck entries, activated with Shift+Enter)</summary>
            public GameObject AlternateActionObject { get; set; }
        }

        /// <summary>
        /// Stores carousel navigation info for elements that support arrow key navigation
        /// </summary>
        protected struct CarouselInfo
        {
            public bool HasArrowNavigation { get; set; }
            public GameObject PreviousControl { get; set; }
            public GameObject NextControl { get; set; }
        }

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
        /// Populate _elements with navigable items.
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
            if (index < 0 || index >= _elements.Count) return "";

            var navElement = _elements[index];
            string label = navElement.Label;

            // Update state for toggles - replace cached state with current state
            // Label already contains "checkbox, checked/unchecked" from classifier
            if (navElement.GameObject != null)
            {
                var toggle = navElement.GameObject.GetComponent<Toggle>();
                if (toggle != null && label.Contains("checkbox"))
                {
                    // Replace the cached state with current state
                    string currentState = toggle.isOn ? "checked" : "unchecked";
                    label = System.Text.RegularExpressions.Regex.Replace(
                        label,
                        @"checkbox, (checked|unchecked)",
                        $"checkbox, {currentState}");
                }
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
            return _elements.Count > 0 && _elements[0].GameObject != null;
        }

        public virtual void Deactivate()
        {
            if (!_isActive) return;

            MelonLogger.Msg($"[{NavigatorId}] Deactivating");

            OnDeactivating();

            _isActive = false;
            _elements.Clear();
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

            // Arrow key navigation for carousel elements
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (HandleCarouselArrow(isNext: false))
                    return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (HandleCarouselArrow(isNext: true))
                    return;
            }

            // Activation
            bool enterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            bool spacePressed = AcceptSpaceKey && Input.GetKeyDown(KeyCode.Space);
            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (enterPressed || spacePressed)
            {
                if (shiftHeld && enterPressed)
                {
                    // Shift+Enter activates alternate action (e.g., edit deck name)
                    ActivateAlternateAction();
                }
                else
                {
                    ActivateCurrentElement();
                }
            }
        }

        /// <summary>
        /// Activate the alternate action for the current element (e.g., edit deck name).
        /// Called when Shift+Enter is pressed.
        /// </summary>
        protected virtual void ActivateAlternateAction()
        {
            if (_currentIndex < 0 || _currentIndex >= _elements.Count) return;

            var element = _elements[_currentIndex];
            if (element.AlternateActionObject != null && element.AlternateActionObject.activeInHierarchy)
            {
                MelonLogger.Msg($"[{NavigatorId}] Activating alternate action: {element.AlternateActionObject.name}");
                UIActivator.Activate(element.AlternateActionObject);
            }
            else
            {
                _announcer.Announce(Strings.NoAlternateAction, AnnouncementPriority.Normal);
            }
        }

        /// <summary>
        /// Handle left/right arrow keys for carousel elements.
        /// Returns true if the current element is a carousel and the key was handled.
        /// </summary>
        protected virtual bool HandleCarouselArrow(bool isNext)
        {
            if (_currentIndex < 0 || _currentIndex >= _elements.Count)
                return false;

            var info = _elements[_currentIndex].Carousel;
            if (!info.HasArrowNavigation)
                return false;

            GameObject control = isNext ? info.NextControl : info.PreviousControl;
            if (control == null || !control.activeInHierarchy)
            {
                _announcer.Announce(isNext ? Strings.NoNextItem : Strings.NoPreviousItem, AnnouncementPriority.Normal);
                return true;
            }

            // Activate the nav control
            MelonLogger.Msg($"[{NavigatorId}] Carousel {(isNext ? "next" : "previous")}: {control.name}");
            UIActivator.Activate(control);

            // After activation, re-read and announce updated content
            var currentElement = _elements[_currentIndex].GameObject;
            if (currentElement != null)
            {
                string newText = UITextExtractor.GetText(currentElement);
                _announcer.Announce(newText, AnnouncementPriority.High);
            }

            return true;
        }

        /// <summary>Move to next (direction=1) or previous (direction=-1) element</summary>
        protected virtual void Move(int direction)
        {
            if (_elements.Count == 0) return;

            _currentIndex = (_currentIndex + direction + _elements.Count) % _elements.Count;
            AnnounceCurrentElement();

            if (SupportsCardNavigation)
            {
                PrepareCardNavigationForCurrentElement();
            }
        }

        protected virtual void MoveNext() => Move(1);
        protected virtual void MovePrevious() => Move(-1);

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

            var element = _elements[_currentIndex].GameObject;
            if (element == null) return;

            MelonLogger.Msg($"[{NavigatorId}] Activating: {element.name} (ID:{element.GetInstanceID()}, Label:{_elements[_currentIndex].Label})");

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

            var element = _elements[_currentIndex].GameObject;
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

        /// <summary>Add an element with a label (prevents duplicates)</summary>
        protected void AddElement(GameObject element, string label)
        {
            AddElement(element, label, default, null);
        }

        /// <summary>Add an element with a label and optional carousel info (prevents duplicates)</summary>
        protected void AddElement(GameObject element, string label, CarouselInfo carouselInfo)
        {
            AddElement(element, label, carouselInfo, null);
        }

        /// <summary>Add an element with label, carousel info, and optional alternate action (prevents duplicates)</summary>
        protected void AddElement(GameObject element, string label, CarouselInfo carouselInfo, GameObject alternateAction)
        {
            if (element == null) return;

            // Prevent duplicates by instance ID
            int instanceId = element.GetInstanceID();
            if (_elements.Any(e => e.GameObject != null && e.GameObject.GetInstanceID() == instanceId))
            {
                MelonLogger.Msg($"[{NavigatorId}] Duplicate skipped (ID:{instanceId}): {label}");
                return;
            }

            _elements.Add(new NavigableElement
            {
                GameObject = element,
                Label = label,
                Carousel = carouselInfo,
                AlternateActionObject = alternateAction
            });

            string altInfo = alternateAction != null ? $" [Alt: {alternateAction.name}]" : "";
            MelonLogger.Msg($"[{NavigatorId}] Added (ID:{instanceId}): {label}{altInfo}");
        }

        /// <summary>Add a button, auto-extracting label from text</summary>
        protected void AddButton(GameObject buttonObj, string fallbackLabel = "Button")
        {
            if (buttonObj == null) return;

            string label = UITextExtractor.GetButtonText(buttonObj, fallbackLabel);
            AddElement(buttonObj, $"{label}, button");
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

            // Check direct children first
            var direct = parent.Find(name);
            if (direct != null) return direct.gameObject;

            // Recursively search grandchildren
            foreach (Transform child in parent)
            {
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

        /// <summary>Get cleaned button text (delegates to UITextExtractor)</summary>
        protected string GetButtonText(GameObject buttonObj, string fallback = null)
        {
            return UITextExtractor.GetButtonText(buttonObj, fallback);
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
