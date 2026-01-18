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

        // Delayed stepper value announcement (game needs a frame to update value after button click)
        private float _stepperAnnounceDelay;
        private const float StepperAnnounceDelaySeconds = 0.1f;

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
            /// <summary>
            /// For sliders: direct reference to modify value via arrow keys
            /// </summary>
            public Slider SliderComponent { get; set; }
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
            return $"{ScreenName}. {countInfo}{Models.Strings.NavigateWithArrows}, Enter to select.";
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

                // Update content for input fields - re-read current text
                var tmpInput = navElement.GameObject.GetComponent<TMPro.TMP_InputField>();
                if (tmpInput != null)
                {
                    // Re-extract the label with current content
                    label = UITextExtractor.GetText(navElement.GameObject);
                    // Update the cached label too
                    navElement.Label = label;
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

            // Handle delayed stepper/carousel value announcement
            if (_stepperAnnounceDelay > 0)
            {
                _stepperAnnounceDelay -= Time.deltaTime;
                if (_stepperAnnounceDelay <= 0)
                {
                    AnnounceStepperValue();
                }
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

        /// <summary>
        /// Handle navigation while editing an input field.
        /// Up/Down arrows announce the field content.
        /// Left/Right arrows announce the character at cursor.
        /// </summary>
        protected virtual void HandleInputFieldNavigation()
        {
            // Up or Down arrow: announce the current input field content
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                AnnounceCurrentInputFieldContent();
            }
            // Left/Right arrows: announce character at cursor position
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                AnnounceCharacterAtCursor();
            }
            // All other keys pass through for typing
        }

        /// <summary>
        /// Announce the character at the current cursor position in the focused input field.
        /// </summary>
        private void AnnounceCharacterAtCursor()
        {
            bool isLeftArrow = Input.GetKeyDown(KeyCode.LeftArrow);
            bool isRightArrow = Input.GetKeyDown(KeyCode.RightArrow);

            // Find the focused TMP_InputField
            var tmpInputFields = GameObject.FindObjectsOfType<TMPro.TMP_InputField>();
            foreach (var field in tmpInputFields)
            {
                if (field != null && field.isFocused)
                {
                    string text = field.text;
                    int caretPos = field.stringPosition;

                    // Handle empty field
                    if (string.IsNullOrEmpty(text))
                    {
                        _announcer.AnnounceInterrupt(Strings.InputFieldEmpty);
                        return;
                    }

                    // Handle password fields - don't reveal characters
                    if (field.inputType == TMPro.TMP_InputField.InputType.Password)
                    {
                        if (caretPos == 0 && isLeftArrow)
                            _announcer.AnnounceInterrupt(Strings.InputFieldStart);
                        else if (caretPos >= text.Length && isRightArrow)
                            _announcer.AnnounceInterrupt(Strings.InputFieldEnd);
                        else if (caretPos >= 0 && caretPos < text.Length)
                            _announcer.AnnounceInterrupt(Strings.InputFieldStar);
                        else
                            _announcer.AnnounceInterrupt(caretPos == 0 ? Strings.InputFieldStart : Strings.InputFieldEnd);
                        return;
                    }

                    // At start and pressing left - can't go further
                    if (caretPos == 0 && isLeftArrow)
                    {
                        _announcer.AnnounceInterrupt(Strings.InputFieldStart);
                    }
                    // At end and pressing right - can't go further
                    else if (caretPos >= text.Length && isRightArrow)
                    {
                        _announcer.AnnounceInterrupt(Strings.InputFieldEnd);
                    }
                    // At end position (after last character)
                    else if (caretPos >= text.Length)
                    {
                        _announcer.AnnounceInterrupt(Strings.InputFieldEnd);
                    }
                    // Normal position - announce character
                    else if (caretPos >= 0 && caretPos < text.Length)
                    {
                        char c = text[caretPos];
                        string charName = GetCharacterName(c);
                        _announcer.AnnounceInterrupt(charName);
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// Get a speakable name for a character (handles spaces, punctuation, etc.)
        /// </summary>
        private string GetCharacterName(char c)
        {
            if (char.IsWhiteSpace(c))
                return Strings.CharSpace;
            if (char.IsDigit(c))
                return c.ToString();
            if (char.IsLetter(c))
                return c.ToString();

            // Common punctuation
            return c switch
            {
                '.' => Strings.CharDot,
                ',' => Strings.CharComma,
                '!' => Strings.CharExclamation,
                '?' => Strings.CharQuestion,
                '@' => Strings.CharAt,
                '#' => Strings.CharHash,
                '$' => Strings.CharDollar,
                '%' => Strings.CharPercent,
                '&' => Strings.CharAnd,
                '*' => Strings.CharStar,
                '-' => Strings.CharDash,
                '_' => Strings.CharUnderscore,
                '+' => Strings.CharPlus,
                '=' => Strings.CharEquals,
                '/' => Strings.CharSlash,
                '\\' => Strings.CharBackslash,
                ':' => Strings.CharColon,
                ';' => Strings.CharSemicolon,
                '"' => Strings.CharQuote,
                '\'' => Strings.CharApostrophe,
                '(' => Strings.CharOpenParen,
                ')' => Strings.CharCloseParen,
                '[' => Strings.CharOpenBracket,
                ']' => Strings.CharCloseBracket,
                '{' => Strings.CharOpenBrace,
                '}' => Strings.CharCloseBrace,
                '<' => Strings.CharLessThan,
                '>' => Strings.CharGreaterThan,
                '|' => Strings.CharPipe,
                '~' => Strings.CharTilde,
                '`' => Strings.CharBacktick,
                '^' => Strings.CharCaret,
                _ => c.ToString()
            };
        }

        /// <summary>
        /// Find the focused input field and announce its content.
        /// </summary>
        private void AnnounceCurrentInputFieldContent()
        {
            // Find the focused TMP_InputField
            var tmpInputFields = GameObject.FindObjectsOfType<TMPro.TMP_InputField>();
            foreach (var field in tmpInputFields)
            {
                if (field != null && field.isFocused)
                {
                    string label = GetInputFieldLabel(field.gameObject);
                    string content = field.text;

                    // Handle password fields
                    if (field.inputType == TMPro.TMP_InputField.InputType.Password)
                    {
                        if (string.IsNullOrEmpty(content))
                            _announcer.AnnounceInterrupt(Strings.InputFieldEmptyWithLabel(label));
                        else
                            _announcer.AnnounceInterrupt(Strings.InputFieldPasswordWithCount(label, content.Length));
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(content))
                            _announcer.AnnounceInterrupt(Strings.InputFieldEmptyWithLabel(label));
                        else
                            _announcer.AnnounceInterrupt(Strings.InputFieldContent(label, content));
                    }
                    return;
                }
            }

            // Check legacy InputField
            var legacyInputFields = GameObject.FindObjectsOfType<UnityEngine.UI.InputField>();
            foreach (var field in legacyInputFields)
            {
                if (field != null && field.isFocused)
                {
                    string label = GetInputFieldLabel(field.gameObject);
                    string content = field.text;

                    if (field.inputType == UnityEngine.UI.InputField.InputType.Password)
                    {
                        if (string.IsNullOrEmpty(content))
                            _announcer.AnnounceInterrupt(Strings.InputFieldEmptyWithLabel(label));
                        else
                            _announcer.AnnounceInterrupt(Strings.InputFieldPasswordWithCount(label, content.Length));
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(content))
                            _announcer.AnnounceInterrupt(Strings.InputFieldEmptyWithLabel(label));
                        else
                            _announcer.AnnounceInterrupt(Strings.InputFieldContent(label, content));
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// Get a readable label for an input field from its name or parent context.
        /// </summary>
        private string GetInputFieldLabel(GameObject inputField)
        {
            string name = inputField.name;

            // Try to extract meaningful label from name
            // Common patterns: "Input Field - Email", "InputField_Username", etc.
            if (name.Contains(" - "))
            {
                var parts = name.Split(new[] { " - " }, System.StringSplitOptions.None);
                if (parts.Length > 1)
                    return parts[1].Trim();
            }

            if (name.Contains("_"))
            {
                var parts = name.Split('_');
                if (parts.Length > 1)
                    return parts[parts.Length - 1].Trim();
            }

            // Check placeholder text
            var tmpInput = inputField.GetComponent<TMPro.TMP_InputField>();
            if (tmpInput != null && tmpInput.placeholder != null)
            {
                var placeholderText = tmpInput.placeholder.GetComponent<TMPro.TMP_Text>();
                if (placeholderText != null && !string.IsNullOrEmpty(placeholderText.text))
                    return placeholderText.text;
            }

            // Fallback: clean up the name
            return name.Replace("Input Field", "").Replace("InputField", "").Trim();
        }

        protected virtual void HandleInput()
        {
            // Special handling when user is editing an input field
            if (UIFocusTracker.IsEditingInputField())
            {
                HandleInputFieldNavigation();
                return;
            }

            // Custom input first (subclass-specific keys)
            if (HandleCustomInput()) return;

            // Menu navigation with Arrow Up/Down and W/S alternatives
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                MovePrevious();
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                MoveNext();
                return;
            }

            // Home/End for quick jump to first/last
            if (Input.GetKeyDown(KeyCode.Home))
            {
                MoveFirst();
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                MoveLast();
                return;
            }

            // Arrow Left/Right for carousel elements
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                if (HandleCarouselArrow(isNext: false))
                    return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
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
        /// Handle left/right arrow keys for carousel/stepper/slider elements.
        /// Returns true if the current element supports arrow navigation and the key was handled.
        /// </summary>
        protected virtual bool HandleCarouselArrow(bool isNext)
        {
            if (_currentIndex < 0 || _currentIndex >= _elements.Count)
                return false;

            var info = _elements[_currentIndex].Carousel;
            if (!info.HasArrowNavigation)
                return false;

            // Handle slider elements directly
            if (info.SliderComponent != null)
            {
                return HandleSliderArrow(info.SliderComponent, isNext);
            }

            // Handle carousel/stepper elements via control buttons
            GameObject control = isNext ? info.NextControl : info.PreviousControl;
            if (control == null || !control.activeInHierarchy)
            {
                _announcer.Announce(isNext ? Strings.NoNextItem : Strings.NoPreviousItem, AnnouncementPriority.Normal);
                return true;
            }

            // Activate the nav control (carousel nav button or stepper increment/decrement)
            MelonLogger.Msg($"[{NavigatorId}] Arrow nav {(isNext ? "next/increment" : "previous/decrement")}: {control.name}");
            UIActivator.Activate(control);

            // Schedule delayed announcement - game needs a frame to update the value
            _stepperAnnounceDelay = StepperAnnounceDelaySeconds;

            return true;
        }

        /// <summary>
        /// Handle slider value adjustment via arrow keys.
        /// Adjusts by 5% per keypress.
        /// </summary>
        private bool HandleSliderArrow(Slider slider, bool isNext)
        {
            if (slider == null || !slider.interactable)
                return false;

            float range = slider.maxValue - slider.minValue;
            float step = range * 0.05f;  // 5% step

            float newValue = isNext
                ? Mathf.Min(slider.value + step, slider.maxValue)
                : Mathf.Max(slider.value - step, slider.minValue);

            // Check if at boundary
            if ((isNext && slider.value >= slider.maxValue) ||
                (!isNext && slider.value <= slider.minValue))
            {
                int currentPercent = Mathf.RoundToInt((slider.value - slider.minValue) / range * 100);
                _announcer.Announce($"{currentPercent} percent", AnnouncementPriority.Normal);
                return true;
            }

            slider.value = newValue;

            // Announce new value
            int percent = Mathf.RoundToInt((newValue - slider.minValue) / range * 100);
            MelonLogger.Msg($"[{NavigatorId}] Slider {(isNext ? "increase" : "decrease")}: {percent}%");
            _announcer.AnnounceInterrupt($"{percent} percent");

            return true;
        }

        /// <summary>
        /// Announce the current stepper/carousel value after a delay.
        /// Called from Update() when the delay expires.
        /// </summary>
        private void AnnounceStepperValue()
        {
            if (_currentIndex < 0 || _currentIndex >= _elements.Count)
                return;

            var currentElement = _elements[_currentIndex].GameObject;
            if (currentElement != null)
            {
                // Re-classify to get the updated label with new value
                var classification = UIElementClassifier.Classify(currentElement);
                string newLabel = classification.Label;

                // Update cached label in our element list
                var updatedElement = _elements[_currentIndex];
                updatedElement.Label = BuildElementLabel(classification);
                _elements[_currentIndex] = updatedElement;

                _announcer.Announce(newLabel, AnnouncementPriority.High);
            }
        }

        /// <summary>
        /// Build the display label from a classification result.
        /// Subclasses may override this for custom label formatting.
        /// </summary>
        protected virtual string BuildElementLabel(UIElementClassifier.ClassificationResult classification)
        {
            if (string.IsNullOrEmpty(classification.RoleLabel))
                return classification.Label;
            return $"{classification.Label}, {classification.RoleLabel}";
        }

        /// <summary>Move to next (direction=1) or previous (direction=-1) element without wrapping</summary>
        protected virtual void Move(int direction)
        {
            if (_elements.Count == 0) return;

            int newIndex = _currentIndex + direction;

            // Check boundaries - no wrapping
            if (newIndex < 0)
            {
                _announcer.Announce(Models.Strings.BeginningOfList, Models.AnnouncementPriority.Normal);
                return;
            }

            if (newIndex >= _elements.Count)
            {
                _announcer.Announce(Models.Strings.EndOfList, Models.AnnouncementPriority.Normal);
                return;
            }

            _currentIndex = newIndex;
            AnnounceCurrentElement();

            if (SupportsCardNavigation)
            {
                PrepareCardNavigationForCurrentElement();
            }
        }

        protected virtual void MoveNext() => Move(1);
        protected virtual void MovePrevious() => Move(-1);

        /// <summary>Jump to first element</summary>
        protected virtual void MoveFirst()
        {
            if (_elements.Count == 0) return;

            if (_currentIndex == 0)
            {
                _announcer.Announce(Models.Strings.BeginningOfList, Models.AnnouncementPriority.Normal);
                return;
            }

            _currentIndex = 0;
            AnnounceCurrentElement();

            if (SupportsCardNavigation)
            {
                PrepareCardNavigationForCurrentElement();
            }
        }

        /// <summary>Jump to last element</summary>
        protected virtual void MoveLast()
        {
            if (_elements.Count == 0) return;

            int lastIndex = _elements.Count - 1;
            if (_currentIndex == lastIndex)
            {
                _announcer.Announce(Models.Strings.EndOfList, Models.AnnouncementPriority.Normal);
                return;
            }

            _currentIndex = lastIndex;
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
