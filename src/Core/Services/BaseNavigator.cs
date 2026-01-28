using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace AccessibleArena.Core.Services
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
        /// Current action index for elements with attached actions.
        /// 0 = the element itself, 1+ = attached actions.
        /// Reset to 0 when navigating to a different element.
        /// </summary>
        protected int _currentActionIndex = 0;

        /// <summary>Whether current index points to a valid element</summary>
        protected bool IsValidIndex => _currentIndex >= 0 && _currentIndex < _elements.Count;

        // Delayed stepper value announcement (game needs a frame to update value after button click)
        private float _stepperAnnounceDelay;
        private const float StepperAnnounceDelaySeconds = 0.1f;

        // Cached input field being edited (set when entering edit mode)
        private GameObject _editingInputField;

        // Track previous frame's input field state for Backspace character announcement
        // (By the time we detect Backspace, Unity has already deleted the character and moved caret)
        private string _prevInputFieldText = "";
        private int _prevInputFieldCaretPos = 0;

        /// <summary>
        /// Represents a virtual action attached to an element (e.g., Delete, Edit for decks).
        /// These are cycled through with left/right arrows.
        /// </summary>
        protected struct AttachedAction
        {
            /// <summary>Display name announced to user (e.g., "Delete", "Edit")</summary>
            public string Label { get; set; }
            /// <summary>The actual button to activate when this action is triggered</summary>
            public GameObject TargetButton { get; set; }
        }

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
            /// <summary>Virtual actions that can be cycled through with left/right arrows</summary>
            public List<AttachedAction> AttachedActions { get; set; }
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

        /// <summary>
        /// Info about a focused input field for navigation announcements
        /// </summary>
        private struct InputFieldInfo
        {
            public bool IsValid;
            public string Text;
            public int CaretPosition;
            public bool IsPassword;
            public GameObject GameObject;
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
            return $"{ScreenName}. {countInfo}{Strings.NavigateWithArrows}, Enter to select.";
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

                // Update content for input fields - re-read current text with password masking
                var tmpInput = navElement.GameObject.GetComponent<TMPro.TMP_InputField>();
                if (tmpInput != null)
                {
                    string fieldLabel = GetInputFieldLabel(navElement.GameObject);

                    // Try .text first, then fall back to textComponent.text (displayed text)
                    string content = tmpInput.text;
                    if (string.IsNullOrEmpty(content) && tmpInput.textComponent != null)
                    {
                        content = tmpInput.textComponent.text;
                    }

                    // Handle password fields - show character count instead of content
                    if (tmpInput.inputType == TMPro.TMP_InputField.InputType.Password)
                    {
                        label = string.IsNullOrEmpty(content)
                            ? $"{fieldLabel}, empty"
                            : $"{fieldLabel}, has {content.Length} characters";
                    }
                    else
                    {
                        // Regular field - show content or empty state
                        label = string.IsNullOrEmpty(content)
                            ? $"{fieldLabel}, empty"
                            : $"{fieldLabel}: {content}";
                    }
                    label += ", text field";
                }
                else
                {
                    // Also handle legacy InputField
                    var legacyInput = navElement.GameObject.GetComponent<InputField>();
                    if (legacyInput != null)
                    {
                        string fieldLabel = GetInputFieldLabel(navElement.GameObject);

                        // Try .text first, then fall back to textComponent.text (displayed text)
                        string content = legacyInput.text;
                        if (string.IsNullOrEmpty(content) && legacyInput.textComponent != null)
                        {
                            content = legacyInput.textComponent.text;
                        }

                        // Handle password fields - show character count instead of content
                        if (legacyInput.inputType == InputField.InputType.Password)
                        {
                            label = string.IsNullOrEmpty(content)
                                ? $"{fieldLabel}, empty"
                                : $"{fieldLabel}, has {content.Length} characters";
                        }
                        else
                        {
                            // Regular field - show content or empty state
                            label = string.IsNullOrEmpty(content)
                                ? $"{fieldLabel}, empty"
                                : $"{fieldLabel}: {content}";
                        }
                        label += ", text field";
                    }
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

        /// <summary>
        /// Gets the GameObjects of all navigable elements in order.
        /// Used by Tab navigation fallback to use the same elements as arrow key navigation.
        /// </summary>
        public IReadOnlyList<GameObject> GetNavigableGameObjects()
        {
            return _elements
                .Where(e => e.GameObject != null)
                .Select(e => e.GameObject)
                .ToList();
        }

        public virtual void OnSceneChanged(string sceneName)
        {
            // Default: deactivate on scene change
            if (_isActive)
            {
                Deactivate();
            }
        }

        /// <summary>
        /// Force element rediscovery. Called by NavigatorManager after scene change
        /// if the navigator stayed active in the new scene.
        /// </summary>
        public virtual void ForceRescan()
        {
            if (!_isActive) return;

            MelonLogger.Msg($"[{NavigatorId}] ForceRescan triggered");

            // Clear and rediscover elements
            _elements.Clear();
            _currentIndex = -1;

            DiscoverElements();

            if (_elements.Count > 0)
            {
                _currentIndex = 0;
                MelonLogger.Msg($"[{NavigatorId}] Rescan found {_elements.Count} elements");

                // Update EventSystem selection to match our current element
                UpdateEventSystemSelection();

                _announcer.AnnounceInterrupt(GetActivationAnnouncement());
            }
            else
            {
                MelonLogger.Msg($"[{NavigatorId}] Rescan found no elements");
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

            // Handle input (uses _prevInputFieldText from last frame for Backspace)
            HandleInput();

            // Track input field text for NEXT frame's Backspace character announcement
            // Must be done AFTER HandleInput so we capture current state for next frame
            // (By the time we detect Backspace, Unity has already processed it)
            TrackInputFieldState();
        }

        /// <summary>
        /// Track current input field state for next frame's Backspace detection.
        /// Called each frame to maintain previous state.
        /// </summary>
        private void TrackInputFieldState()
        {
            if (!UIFocusTracker.IsAnyInputFieldFocused() && !UIFocusTracker.IsEditingInputField())
            {
                _prevInputFieldText = "";
                _prevInputFieldCaretPos = 0;
                return;
            }

            var info = GetAnyFocusedInputFieldInfo();
            if (info.IsValid)
            {
                _prevInputFieldText = info.Text ?? "";
                _prevInputFieldCaretPos = info.CaretPosition;
            }
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

            // Update EventSystem selection to match our current element
            UpdateEventSystemSelection();

            // Announce screen
            _announcer.AnnounceInterrupt(GetActivationAnnouncement());

            UpdateCardNavigation();
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
        /// Escape exits edit mode and returns to menu navigation.
        /// </summary>
        protected virtual void HandleInputFieldNavigation()
        {
            // F4 should work even in input fields (toggle Friends panel)
            // Exit edit mode and let HandleCustomInput process it
            if (Input.GetKeyDown(KeyCode.F4))
            {
                _editingInputField = null;
                UIFocusTracker.ExitInputFieldEditMode();
                UIFocusTracker.DeactivateFocusedInputField();
                HandleCustomInput();
                return;
            }

            // Escape exits edit mode by deactivating the input field
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _editingInputField = null;
                UIFocusTracker.ExitInputFieldEditMode();
                UIFocusTracker.DeactivateFocusedInputField();
                _announcer.Announce("Exited edit mode", AnnouncementPriority.Normal);
                return;
            }

            // Tab exits edit mode and navigates to next/previous element
            // Consume Tab so game doesn't interfere
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                _editingInputField = null;
                UIFocusTracker.ExitInputFieldEditMode();
                UIFocusTracker.DeactivateFocusedInputField();
                bool shiftTab = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (shiftTab)
                    MovePrevious();
                else
                    MoveNext();
                return;
            }

            // Backspace: announce the character being deleted, then let it pass through
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                AnnounceDeletedCharacter();
                // Don't return - let key pass through to input field for actual deletion
            }
            // Up or Down arrow: announce the current input field content
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
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
        /// Handle navigation while a dropdown is open.
        /// Arrow keys and Enter are handled by Unity's dropdown.
        /// Escape and Backspace close the dropdown without triggering back navigation.
        /// Edit mode exits automatically when focus leaves dropdown items (detected by UIFocusTracker).
        /// </summary>
        protected virtual void HandleDropdownNavigation()
        {
            // Escape or Backspace: Close the dropdown explicitly
            // We must intercept these because the game handles Escape as "back" which
            // navigates to the previous screen instead of just closing the dropdown
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            {
                CloseActiveDropdown();
                return;
            }

            // All other keys pass through to Unity's dropdown handling
            // - Arrow keys navigate items (FocusTracker announces them)
            // - Enter selects item and closes dropdown (focus leaves items, we auto-exit edit mode)
        }

        /// <summary>
        /// Close the currently active dropdown by finding its parent TMP_Dropdown and calling Hide().
        /// </summary>
        private void CloseActiveDropdown()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
            {
                UIFocusTracker.ExitDropdownEditMode();
                return;
            }

            var currentItem = eventSystem.currentSelectedGameObject;

            // Find the TMP_Dropdown in parent hierarchy
            var transform = currentItem.transform;
            while (transform != null)
            {
                // Check for standard TMP_Dropdown
                var tmpDropdown = transform.GetComponent<TMPro.TMP_Dropdown>();
                if (tmpDropdown != null)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Closing TMP_Dropdown via Escape/Backspace");
                    tmpDropdown.Hide();
                    UIFocusTracker.ExitDropdownEditMode();
                    _announcer.Announce("Dropdown closed", AnnouncementPriority.Normal);
                    return;
                }

                // Check for Unity legacy Dropdown
                var legacyDropdown = transform.GetComponent<Dropdown>();
                if (legacyDropdown != null)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Closing legacy Dropdown via Escape/Backspace");
                    legacyDropdown.Hide();
                    UIFocusTracker.ExitDropdownEditMode();
                    _announcer.Announce("Dropdown closed", AnnouncementPriority.Normal);
                    return;
                }

                // Check for game's custom cTMP_Dropdown
                foreach (var component in transform.GetComponents<Component>())
                {
                    if (component != null && component.GetType().Name == "cTMP_Dropdown")
                    {
                        // Try to call Hide() via reflection
                        var hideMethod = component.GetType().GetMethod("Hide",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (hideMethod != null)
                        {
                            MelonLogger.Msg($"[{NavigatorId}] Closing cTMP_Dropdown via Escape/Backspace");
                            hideMethod.Invoke(component, null);
                            UIFocusTracker.ExitDropdownEditMode();
                            _announcer.Announce("Dropdown closed", AnnouncementPriority.Normal);
                            return;
                        }
                    }
                }

                transform = transform.parent;
            }

            // Couldn't find dropdown - just exit edit mode
            MelonLogger.Msg($"[{NavigatorId}] Could not find dropdown to close, exiting edit mode");
            UIFocusTracker.ExitDropdownEditMode();
        }

        /// <summary>
        /// Sync the navigator's index to the currently focused element.
        /// Called after exiting dropdown mode to follow game's auto-advance (Month -> Day -> Year).
        /// </summary>
        protected virtual void SyncIndexToFocusedElement()
        {
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null) return;

            var focused = eventSystem.currentSelectedGameObject;
            if (focused == null) return;

            string focusedName = focused.name;

            // Find element in our list by name
            for (int i = 0; i < _elements.Count; i++)
            {
                if (_elements[i].GameObject != null && _elements[i].GameObject.name == focusedName)
                {
                    if (_currentIndex != i)
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Synced index {_currentIndex} -> {i} ({focusedName})");
                        _currentIndex = i;
                    }
                    AnnounceCurrentElement();
                    return;
                }
            }

            MelonLogger.Msg($"[{NavigatorId}] Could not sync to focused element: {focusedName}");
        }

        /// <summary>
        /// Get info about the currently focused input field from cache or current element.
        /// Uses cached field when available, avoids expensive FindObjectsOfType.
        /// </summary>
        private InputFieldInfo GetFocusedInputFieldInfo()
        {
            var result = new InputFieldInfo { IsValid = false };

            // Try cached editing field first
            GameObject fieldObj = _editingInputField;
            if (fieldObj == null && IsValidIndex)
            {
                fieldObj = _elements[_currentIndex].GameObject;
            }

            if (fieldObj == null) return result;

            // Check TMP_InputField
            var tmpInput = fieldObj.GetComponent<TMPro.TMP_InputField>();
            if (tmpInput != null && tmpInput.isFocused)
            {
                result.IsValid = true;
                result.Text = tmpInput.text;
                result.CaretPosition = tmpInput.stringPosition;
                result.IsPassword = tmpInput.inputType == TMPro.TMP_InputField.InputType.Password;
                result.GameObject = fieldObj;
                return result;
            }

            // Check legacy InputField
            var legacyInput = fieldObj.GetComponent<UnityEngine.UI.InputField>();
            if (legacyInput != null && legacyInput.isFocused)
            {
                result.IsValid = true;
                result.Text = legacyInput.text;
                result.CaretPosition = legacyInput.caretPosition;
                result.IsPassword = legacyInput.inputType == UnityEngine.UI.InputField.InputType.Password;
                result.GameObject = fieldObj;
                return result;
            }

            return result;
        }

        /// <summary>
        /// Get info about any focused input field in the scene.
        /// Used when the field might have been activated by mouse click rather than our navigation.
        /// </summary>
        private InputFieldInfo GetAnyFocusedInputFieldInfo()
        {
            // First try the cached/navigated field
            var result = GetFocusedInputFieldInfo();
            if (result.IsValid) return result;

            // Scan scene for any focused input field (handles mouse-clicked fields)
            var tmpInputFields = GameObject.FindObjectsOfType<TMPro.TMP_InputField>();
            foreach (var field in tmpInputFields)
            {
                if (field.isFocused)
                {
                    return new InputFieldInfo
                    {
                        IsValid = true,
                        Text = field.text,
                        CaretPosition = field.stringPosition,
                        IsPassword = field.inputType == TMPro.TMP_InputField.InputType.Password,
                        GameObject = field.gameObject
                    };
                }
            }

            var legacyInputFields = GameObject.FindObjectsOfType<UnityEngine.UI.InputField>();
            foreach (var field in legacyInputFields)
            {
                if (field.isFocused)
                {
                    return new InputFieldInfo
                    {
                        IsValid = true,
                        Text = field.text,
                        CaretPosition = field.caretPosition,
                        IsPassword = field.inputType == UnityEngine.UI.InputField.InputType.Password,
                        GameObject = field.gameObject
                    };
                }
            }

            return new InputFieldInfo { IsValid = false };
        }

        /// <summary>
        /// Announce the character being deleted by Backspace.
        /// Called BEFORE Unity processes the deletion so character is still in the text.
        /// </summary>
        private void AnnounceDeletedCharacter()
        {
            var info = GetAnyFocusedInputFieldInfo();
            if (!info.IsValid) return;

            string currentText = info.Text ?? "";
            string prevText = _prevInputFieldText ?? "";

            // Compare previous and current text to find deleted character
            // By the time we detect Backspace, Unity has already processed it
            if (prevText.Length <= currentText.Length)
            {
                // Text didn't get shorter - nothing was deleted (or text was added)
                return;
            }

            // Handle password fields - don't reveal characters
            if (info.IsPassword)
            {
                _announcer.AnnounceInterrupt(Strings.InputFieldStar);
                return;
            }

            // Find the deleted character by comparing strings
            // Typically it's at the caret position in the previous text
            char deletedChar = FindDeletedCharacter(prevText, currentText, _prevInputFieldCaretPos);
            string charName = GetCharacterName(deletedChar);
            _announcer.AnnounceInterrupt(charName);
        }

        /// <summary>
        /// Find the character that was deleted by comparing previous and current text.
        /// </summary>
        private char FindDeletedCharacter(string prevText, string currentText, int prevCaretPos)
        {
            // Backspace deletes the character before the caret
            // So if caret was at position N, the deleted char was at N-1
            int deletedIndex = prevCaretPos - 1;

            // Sanity check
            if (deletedIndex >= 0 && deletedIndex < prevText.Length)
            {
                return prevText[deletedIndex];
            }

            // Fallback: find first difference between strings
            for (int i = 0; i < currentText.Length; i++)
            {
                if (i >= prevText.Length || prevText[i] != currentText[i])
                {
                    // Found difference - the deleted char was at position i in prevText
                    if (i < prevText.Length)
                        return prevText[i];
                    break;
                }
            }

            // If current is a prefix of prev, the deleted char is the one after current ends
            if (currentText.Length < prevText.Length)
            {
                return prevText[currentText.Length];
            }

            // Couldn't determine - return placeholder
            return '?';
        }

        /// <summary>
        /// Announce the character at the current cursor position in the focused input field.
        /// </summary>
        private void AnnounceCharacterAtCursor()
        {
            var info = GetFocusedInputFieldInfo();
            if (!info.IsValid) return;

            bool isLeftArrow = Input.GetKeyDown(KeyCode.LeftArrow);
            bool isRightArrow = Input.GetKeyDown(KeyCode.RightArrow);
            string text = info.Text;
            int caretPos = info.CaretPosition;

            // Handle empty field
            if (string.IsNullOrEmpty(text))
            {
                _announcer.AnnounceInterrupt(Strings.InputFieldEmpty);
                return;
            }

            // Handle password fields - don't reveal characters
            if (info.IsPassword)
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
            // Normal position - announce character (caretPos < text.Length implied by above)
            else if (caretPos >= 0 && caretPos < text.Length)
            {
                char c = text[caretPos];
                string charName = GetCharacterName(c);
                _announcer.AnnounceInterrupt(charName);
            }
            // At end position (caretPos >= text.Length, left arrow) - announce end
            else
            {
                _announcer.AnnounceInterrupt(Strings.InputFieldEnd);
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
        /// Announce the content of the currently focused input field.
        /// </summary>
        private void AnnounceCurrentInputFieldContent()
        {
            var info = GetFocusedInputFieldInfo();
            if (!info.IsValid) return;

            string label = GetInputFieldLabel(info.GameObject);
            string content = info.Text;

            if (info.IsPassword)
            {
                string announcement = string.IsNullOrEmpty(content)
                    ? Strings.InputFieldEmptyWithLabel(label)
                    : Strings.InputFieldPasswordWithCount(label, content.Length);
                _announcer.AnnounceInterrupt(announcement);
            }
            else
            {
                string announcement = string.IsNullOrEmpty(content)
                    ? Strings.InputFieldEmptyWithLabel(label)
                    : Strings.InputFieldContent(label, content);
                _announcer.AnnounceInterrupt(announcement);
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

        // Track if we were in dropdown mode last frame
        private bool _wasInDropdownMode;

        // Skip dropdown mode tracking after closing an auto-opened dropdown
        // (prevents _wasInDropdownMode from being re-set while IsExpanded is still true)
        private bool _skipDropdownModeTracking;

        protected virtual void HandleInput()
        {
            // Check if we're in explicit edit mode (user activated field or game focused it)
            if (UIFocusTracker.IsEditingInputField())
            {
                HandleInputFieldNavigation();
                return;
            }

            // Check if user is ON an input field (selected but maybe not focused yet)
            // This handles MTGA's auto-activating input fields when selected via Tab
            if (UIFocusTracker.IsAnyInputFieldFocused())
            {
                // Check if field is actually focused (isFocused = caret visible)
                var info = GetAnyFocusedInputFieldInfo();
                if (info.IsValid && info.GameObject != null)
                {
                    // Field is focused - enter edit mode
                    _editingInputField = info.GameObject;
                    UIFocusTracker.EnterInputFieldEditMode(info.GameObject);
                    HandleInputFieldNavigation();
                    return;
                }

                // Field is selected but not focused - handle Escape/Tab to exit, but don't enter full edit mode
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    SyncIndexToFocusedElement();
                    UIFocusTracker.DeactivateFocusedInputField();
                    AnnounceCurrentElement();
                    return;
                }
                if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
                {
                    UIFocusTracker.DeactivateFocusedInputField();
                    bool shiftTab = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    if (shiftTab)
                        MovePrevious();
                    else
                        MoveNext();
                    return;
                }
                // Let other keys pass through (typing will focus the field)
                return;
            }

            // Clear edit mode when no input field is focused
            if (_editingInputField != null)
            {
                _editingInputField = null;
                UIFocusTracker.ExitInputFieldEditMode();
            }

            // When a dropdown is open, let Unity handle arrow key navigation
            bool inDropdownMode = UIFocusTracker.IsEditingDropdown();
            if (inDropdownMode)
            {
                // Only track dropdown mode if we're not skipping (i.e., we didn't just close an auto-opened dropdown)
                if (!_skipDropdownModeTracking)
                {
                    _wasInDropdownMode = true;
                }
                HandleDropdownNavigation();
                return;
            }

            // Clear the skip flag once dropdown is actually closed
            _skipDropdownModeTracking = false;

            // If we just exited dropdown mode, sync our index to where focus went
            // This handles game's auto-advance (Month -> Day -> Year)
            if (_wasInDropdownMode)
            {
                _wasInDropdownMode = false;
                SyncIndexToFocusedElement();
                // Don't process any more input this frame to avoid double-activation
                return;
            }

            // Custom input first (subclass-specific keys)
            if (HandleCustomInput()) return;

            // Menu navigation with Arrow Up/Down, W/S alternatives, and Tab/Shift+Tab
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

            // Tab/Shift+Tab navigation - same as arrow down/up
            // Use GetKeyDownAndConsume to prevent game from also processing Tab
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool shiftTab = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (shiftTab)
                    MovePrevious();
                else
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
            if (!IsValidIndex) return;

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
        /// Handle left/right arrow keys for carousel/stepper/slider elements or attached actions.
        /// Returns true if the current element supports arrow navigation and the key was handled.
        /// </summary>
        protected virtual bool HandleCarouselArrow(bool isNext)
        {
            if (!IsValidIndex)
                return false;

            var element = _elements[_currentIndex];

            // Check for attached actions first (e.g., deck actions: Delete, Edit, Export)
            if (element.AttachedActions != null && element.AttachedActions.Count > 0)
            {
                return HandleAttachedActionArrow(element, isNext);
            }

            var info = element.Carousel;
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
        /// Handle left/right arrow keys for cycling through attached actions.
        /// Action index 0 = the element itself, 1+ = attached actions.
        /// </summary>
        private bool HandleAttachedActionArrow(NavigableElement element, bool isNext)
        {
            int actionCount = element.AttachedActions.Count;
            int totalOptions = 1 + actionCount; // Element itself + attached actions

            int newActionIndex = _currentActionIndex + (isNext ? 1 : -1);

            // Clamp to valid range (no wrapping)
            if (newActionIndex < 0)
            {
                _announcer.Announce(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return true;
            }
            if (newActionIndex >= totalOptions)
            {
                _announcer.Announce(Strings.EndOfList, AnnouncementPriority.Normal);
                return true;
            }

            _currentActionIndex = newActionIndex;

            // Announce current action
            string announcement;
            if (_currentActionIndex == 0)
            {
                // Back to the element itself
                announcement = element.Label;
            }
            else
            {
                // Attached action
                var action = element.AttachedActions[_currentActionIndex - 1];
                announcement = action.Label;
            }

            MelonLogger.Msg($"[{NavigatorId}] Action cycle: index {_currentActionIndex}, announcing: {announcement}");
            _announcer.AnnounceInterrupt(announcement);
            return true;
        }

        /// <summary>
        /// Announce the current stepper/carousel value after a delay.
        /// Called from Update() when the delay expires.
        /// </summary>
        private void AnnounceStepperValue()
        {
            if (!IsValidIndex)
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

            // Single element: re-announce it instead of saying "end/beginning of list"
            if (_elements.Count == 1)
            {
                AnnounceCurrentElement();
                return;
            }

            int newIndex = _currentIndex + direction;

            // Check boundaries - no wrapping
            if (newIndex < 0)
            {
                _announcer.Announce(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }

            if (newIndex >= _elements.Count)
            {
                _announcer.Announce(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            _currentIndex = newIndex;
            _currentActionIndex = 0; // Reset action index when moving to new element

            // Update EventSystem selection to match our navigation
            // This ensures Unity's Submit events go to the correct element
            UpdateEventSystemSelection();

            AnnounceCurrentElement();
            UpdateCardNavigation();
        }

        /// <summary>
        /// Update EventSystem.current.SetSelectedGameObject to match our current element.
        /// This ensures that when Enter/Submit is pressed, Unity targets the correct element.
        /// </summary>
        protected virtual void UpdateEventSystemSelection()
        {
            if (!IsValidIndex) return;

            var element = _elements[_currentIndex].GameObject;
            if (element == null || !element.activeInHierarchy) return;

            var eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                // Suppress FocusTracker's announcement since we handle our own via AnnounceCurrentElement()
                UIFocusTracker.SuppressNextFocusAnnouncement();

                eventSystem.SetSelectedGameObject(element);

                // MTGA auto-opens dropdowns when they receive EventSystem selection.
                // If we just navigated to a dropdown (not activated via Enter), close it.
                // We check IsExpanded via the real property, not assumptions.
                if (UIFocusTracker.IsDropdown(element) && UIFocusTracker.IsAnyDropdownExpanded())
                {
                    CloseDropdownOnElement(element);
                }
            }
        }

        /// <summary>
        /// Close a dropdown on the specified element without entering edit mode.
        /// Used to counteract MTGA's auto-open behavior when navigating to dropdowns.
        /// </summary>
        private void CloseDropdownOnElement(GameObject element)
        {
            if (element == null) return;

            bool closed = false;

            // Try TMP_Dropdown
            var tmpDropdown = element.GetComponent<TMPro.TMP_Dropdown>();
            if (tmpDropdown != null)
            {
                tmpDropdown.Hide();
                MelonLogger.Msg($"[{NavigatorId}] Closed auto-opened TMP_Dropdown: {element.name}");
                closed = true;
            }

            // Try legacy Dropdown
            if (!closed)
            {
                var legacyDropdown = element.GetComponent<Dropdown>();
                if (legacyDropdown != null)
                {
                    legacyDropdown.Hide();
                    MelonLogger.Msg($"[{NavigatorId}] Closed auto-opened legacy Dropdown: {element.name}");
                    closed = true;
                }
            }

            // Try cTMP_Dropdown via reflection
            if (!closed)
            {
                foreach (var component in element.GetComponents<Component>())
                {
                    if (component != null && component.GetType().Name == "cTMP_Dropdown")
                    {
                        var hideMethod = component.GetType().GetMethod("Hide",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (hideMethod != null)
                        {
                            hideMethod.Invoke(component, null);
                            MelonLogger.Msg($"[{NavigatorId}] Closed auto-opened cTMP_Dropdown: {element.name}");
                            closed = true;
                            break;
                        }
                    }
                }
            }

            // Reset dropdown mode tracking - the dropdown's IsExpanded property may not
            // update immediately after Hide(), so we need to manually clear the state
            // to prevent SyncIndexToFocusedElement from being triggered later.
            // Also suppress dropdown mode re-entry since FocusTracker will see IsExpanded=true
            // briefly before the dropdown fully closes.
            if (closed)
            {
                UIFocusTracker.ExitDropdownEditMode();
                UIFocusTracker.SuppressDropdownModeEntry();
                _wasInDropdownMode = false;
                _skipDropdownModeTracking = true; // Prevent _wasInDropdownMode from being re-set
            }
        }

        protected virtual void MoveNext() => Move(1);
        protected virtual void MovePrevious() => Move(-1);

        /// <summary>Jump to first element</summary>
        protected virtual void MoveFirst()
        {
            if (_elements.Count == 0) return;

            // Single element or already at first: re-announce current
            if (_currentIndex == 0)
            {
                AnnounceCurrentElement();
                return;
            }

            _currentIndex = 0;
            _currentActionIndex = 0; // Reset action index
            AnnounceCurrentElement();
            UpdateCardNavigation();
        }

        /// <summary>Jump to last element</summary>
        protected virtual void MoveLast()
        {
            if (_elements.Count == 0) return;

            int lastIndex = _elements.Count - 1;
            // Single element or already at last: re-announce current
            if (_currentIndex == lastIndex)
            {
                AnnounceCurrentElement();
                return;
            }

            _currentIndex = lastIndex;
            _currentActionIndex = 0; // Reset action index
            AnnounceCurrentElement();
            UpdateCardNavigation();
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
            if (!IsValidIndex) return;

            var navElement = _elements[_currentIndex];
            var element = navElement.GameObject;
            if (element == null) return;

            // Check if we're on an attached action (not the element itself)
            if (_currentActionIndex > 0 && navElement.AttachedActions != null &&
                _currentActionIndex <= navElement.AttachedActions.Count)
            {
                var action = navElement.AttachedActions[_currentActionIndex - 1];
                if (action.TargetButton != null && action.TargetButton.activeInHierarchy)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Activating attached action: {action.Label} -> {action.TargetButton.name}");
                    var actionResult = UIActivator.Activate(action.TargetButton);
                    _announcer.Announce(actionResult.Message, AnnouncementPriority.Normal);
                    return;
                }
                else
                {
                    _announcer.Announce("Action not available", AnnouncementPriority.Normal);
                    return;
                }
            }

            MelonLogger.Msg($"[{NavigatorId}] Activating: {element.name} (ID:{element.GetInstanceID()}, Label:{navElement.Label})");

            // Check if this is an input field - enter edit mode
            if (UIFocusTracker.IsInputField(element))
            {
                _editingInputField = element;
                UIFocusTracker.EnterInputFieldEditMode(element);
                _announcer.Announce("Editing. Type to enter text, Escape to exit.", AnnouncementPriority.Normal);

                // Also activate the field so it receives keyboard input
                UIActivator.Activate(element);
                return;
            }

            // Check if this is a card - delegate to CardInfoNavigator
            if (SupportsCardNavigation && CardDetector.IsCard(element))
            {
                if (AccessibleArenaMod.Instance?.ActivateCardDetails(element) == true)
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

        /// <summary>
        /// Update card navigation state for current element.
        /// Checks SupportsCardNavigation internally - callers don't need to check.
        /// </summary>
        protected void UpdateCardNavigation()
        {
            if (!SupportsCardNavigation) return;

            var cardNavigator = AccessibleArenaMod.Instance?.CardNavigator;
            if (cardNavigator == null) return;

            if (!IsValidIndex)
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
            AddElement(element, label, carouselInfo, alternateAction, null);
        }

        /// <summary>Add an element with label, carousel info, alternate action, and attached actions (prevents duplicates)</summary>
        protected void AddElement(GameObject element, string label, CarouselInfo carouselInfo, GameObject alternateAction, List<AttachedAction> attachedActions)
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
                AlternateActionObject = alternateAction,
                AttachedActions = attachedActions
            });

            string altInfo = alternateAction != null ? $" [Alt: {alternateAction.name}]" : "";
            string actionsInfo = attachedActions != null && attachedActions.Count > 0 ? $" [Actions: {attachedActions.Count}]" : "";
            MelonLogger.Msg($"[{NavigatorId}] Added (ID:{instanceId}): {label}{altInfo}{actionsInfo}");
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
