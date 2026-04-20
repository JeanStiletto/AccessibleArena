using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.PanelDetection;
using AccessibleArena.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Base class for screen navigators. Handles common Tab/Enter navigation,
    /// element management, and announcements. Subclasses implement screen detection
    /// and element discovery.
    /// </summary>
    public abstract partial class BaseNavigator : IScreenNavigator
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

        // Track whether last navigation was via Tab (vs arrow keys)
        // Tab navigation should auto-enter input field edit mode, arrow keys should not
        private bool _lastNavigationWasTab;

        // Letter navigation handler (buffered jump with same-letter cycling)
        protected readonly LetterSearchHandler _letterSearch = new LetterSearchHandler();

        // Hold-to-repeat handler for arrow key navigation
        protected readonly KeyHoldRepeater _holdRepeater = new KeyHoldRepeater();

        /// <summary>
        /// Represents a virtual action attached to an element (e.g., Delete, Edit for decks).
        /// These are cycled through with left/right arrows.
        /// </summary>
        protected struct AttachedAction
        {
            /// <summary>Stable identifier for matching in HandleAttachedAction (e.g., "Rename", "Clone").
            /// Does not change with locale. When null, falls back to Label for matching.</summary>
            public string Id { get; set; }
            /// <summary>Display name announced to user (localized)</summary>
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
            public UIElementClassifier.ElementRole Role { get; set; }
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
            /// <summary>
            /// If true, activate controls via hover (pointer enter/exit) instead of full click.
            /// Used for Popout hover buttons that open submenus on click.
            /// </summary>
            public bool UseHoverActivation { get; set; }
            /// <summary>Action-based stepper: called on Right arrow (increment)</summary>
            public Action OnIncrement { get; set; }
            /// <summary>Action-based stepper: called on Left arrow (decrement)</summary>
            public Action OnDecrement { get; set; }
            /// <summary>Re-reads current value label after stepper change</summary>
            public Func<string> ReadLabel { get; set; }
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
        /// Use helper methods: AddElement(), AddButton()
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

        /// <summary>Called before a collection card click to capture deck count pre-activation.</summary>
        protected virtual void OnDeckBuilderCardCountCapture() { }

        /// <summary>Called after a deck builder card (collection or deck list) is activated. Subclasses can trigger rescan.</summary>
        protected virtual void OnDeckBuilderCardActivated() { }

        /// <summary>Called when a popup is detected via PanelStateManager. Override for custom filtering or behavior.</summary>
        protected virtual void OnPopupDetected(PanelInfo panel)
        {
            if (panel?.GameObject != null)
                EnterPopupMode(panel.GameObject);
        }

        /// <summary>Called when a popup closes. Override for custom cleanup or re-announcement.</summary>
        protected virtual void OnPopupClosed() { }

        /// <summary>
        /// Check if a panel name belongs to a decorative (non-interactive) overlay.
        /// Used by both popup mode handling and overlay element filtering.
        /// </summary>
        internal static bool IsDecorativePanel(string name)
        {
            // RewardPopup3DIcon: 3D reward preview (spinning coin/card animation).
            // Not interactive - only contains HitBox dismiss areas, no real buttons.
            return name.Contains("RewardPopup3DIcon");
        }

        /// <summary>
        /// Check if a panel should be excluded from popup handling.
        /// Override to add navigator-specific exclusions (base filters universal decorative overlays).
        /// </summary>
        protected virtual bool IsPopupExcluded(PanelInfo panel)
        {
            if (panel == null) return false;
            return IsDecorativePanel(panel.Name);
        }

        /// <summary>Return the tutorial hint for this navigator (used by Ctrl+F1)</summary>
        public virtual string GetTutorialHint() => LocaleManager.Instance.Get("NavigateHint");

        /// <summary>Build the initial screen announcement</summary>
        protected virtual string GetActivationAnnouncement()
        {
            string countInfo = _elements.Count > 1 ? $" {_elements.Count} items." : "";
            string core = $"{ScreenName}.{countInfo}".TrimEnd();
            return Strings.WithHint(core, "NavigateHint");
        }

        /// <summary>Build announcement for current element</summary>
        protected virtual string GetElementAnnouncement(int index)
        {
            if (index < 0 || index >= _elements.Count) return "";

            var navElement = _elements[index];
            string label = RefreshElementLabel(navElement.GameObject, navElement.Label, navElement.Role);

            return Strings.ItemPositionOf(index + 1, _elements.Count, label);
        }

        /// <summary>
        /// Refresh a cached element label with live state (toggle checked, input field content, dropdown value).
        /// Shared by BaseNavigator and GroupedNavigator to avoid duplicated logic.
        /// </summary>
        public static string RefreshElementLabel(GameObject obj, string label,
            UIElementClassifier.ElementRole role = UIElementClassifier.ElementRole.Unknown)
        {
            if (obj == null) return label;

            // Re-read objective text live — quest progress updates async after the home page
            // finishes its server round-trip, so cached labels from rescan time are stale.
            if (obj.name == "ObjectiveGraphics")
            {
                string freshText = UITextExtractor.GetText(obj);
                if (!string.IsNullOrEmpty(freshText))
                    return freshText;
            }

            // Update state for toggles - replace cached checkbox state with current state
            var toggle = obj.GetComponent<Toggle>();
            if (toggle != null && (role == UIElementClassifier.ElementRole.Toggle || role == UIElementClassifier.ElementRole.Unknown))
            {
                // Find the last occurrence of the checkbox role text and replace from there
                string checkboxRole = Strings.RoleCheckbox;
                int checkboxIdx = label.LastIndexOf($", {checkboxRole}");
                if (checkboxIdx >= 0)
                {
                    label = label.Substring(0, checkboxIdx) + $", {Strings.RoleCheckboxState(toggle.isOn)}";
                }
            }

            // Update content for input fields - re-read current text with password masking
            var tmpInput = obj.GetComponent<TMPro.TMP_InputField>();
            if (tmpInput != null)
            {
                string fieldLabel = UITextExtractor.GetInputFieldLabel(obj);
                string empty = Strings.InputFieldEmpty;

                string content = tmpInput.text;
                if (string.IsNullOrEmpty(content) && tmpInput.textComponent != null)
                    content = tmpInput.textComponent.text;

                if (tmpInput.inputType == TMPro.TMP_InputField.InputType.Password)
                {
                    label = string.IsNullOrEmpty(content)
                        ? $"{fieldLabel}, {empty}"
                        : $"{fieldLabel}, has {content.Length} characters";
                }
                else
                {
                    label = string.IsNullOrEmpty(content)
                        ? $"{fieldLabel}, {empty}"
                        : $"{fieldLabel}: {content}";
                }
                label = Strings.WithHint($"{label}, {Strings.TextField}", "InputFieldHint");
            }
            else
            {
                var legacyInput = obj.GetComponent<InputField>();
                if (legacyInput != null)
                {
                    string fieldLabel = UITextExtractor.GetInputFieldLabel(obj);
                    string empty = Strings.InputFieldEmpty;

                    string content = legacyInput.text;
                    if (string.IsNullOrEmpty(content) && legacyInput.textComponent != null)
                        content = legacyInput.textComponent.text;

                    if (legacyInput.inputType == InputField.InputType.Password)
                    {
                        label = string.IsNullOrEmpty(content)
                            ? $"{fieldLabel}, {empty}"
                            : $"{fieldLabel}, has {content.Length} characters";
                    }
                    else
                    {
                        label = string.IsNullOrEmpty(content)
                            ? $"{fieldLabel}, {empty}"
                            : $"{fieldLabel}: {content}";
                    }
                    label = Strings.WithHint($"{label}, {Strings.TextField}", "InputFieldHint");
                }
            }

            // Update content for dropdowns - re-read current selected value
            if (role == UIElementClassifier.ElementRole.Dropdown || role == UIElementClassifier.ElementRole.Unknown)
            {
                string dropdownRole = Strings.RoleDropdown;
                string dropdownSuffix = $", {dropdownRole}";
                string currentValue = GetDropdownDisplayValue(obj);
                if (!string.IsNullOrEmpty(currentValue))
                {
                    // Strip existing dropdown suffix to get base label
                    string baseLabel = label.EndsWith(dropdownSuffix)
                        ? label.Substring(0, label.Length - dropdownSuffix.Length)
                        : label;
                    // Strip old cached value (appended as ": oldValue" during discovery)
                    int lastColon = baseLabel.LastIndexOf(": ");
                    string labelName = lastColon >= 0 ? baseLabel.Substring(0, lastColon) : baseLabel;
                    if (labelName != currentValue)
                        label = $"{labelName}: {currentValue}{dropdownSuffix}";
                    else
                        label = $"{currentValue}{dropdownSuffix}";
                }
            }

            // Update slider labels - re-read current slider value
            if (role == UIElementClassifier.ElementRole.Slider)
            {
                var slider = obj.GetComponent<Slider>();
                if (slider == null) slider = obj.GetComponentInChildren<Slider>();
                if (slider != null)
                {
                    var classification = UIElementClassifier.Classify(obj);
                    if (classification != null && classification.IsNavigable)
                        label = BuildLabel(classification.Label, classification.RoleLabel, classification.Role);
                }
            }

            // Update stepper labels - re-read current value from text children
            if (role == UIElementClassifier.ElementRole.Stepper)
            {
                var classification = UIElementClassifier.Classify(obj);
                if (classification != null && classification.IsNavigable)
                    label = BuildLabel(classification.Label, classification.RoleLabel, classification.Role);
            }

            return label;
        }

        /// <summary>Whether to integrate with CardInfoNavigator</summary>
        protected virtual bool SupportsCardNavigation => true;

        /// <summary>Whether to accept Space key for activation (in addition to Enter)</summary>
        protected virtual bool AcceptSpaceKey => true;

        /// <summary>Whether letter keys (A-Z) trigger jump-to-element navigation. Disabled in duel navigators where letters are zone shortcuts.</summary>
        protected virtual bool SupportsLetterNavigation => true;

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

            Log.Msg("{NavigatorId}", $"ForceRescan triggered");

            // Clear and rediscover elements
            _elements.Clear();
            _currentIndex = -1;

            DiscoverElements();

            if (_elements.Count > 0)
            {
                _currentIndex = 0;
                Log.Msg("{NavigatorId}", $"Rescan found {_elements.Count} elements");

                // Update EventSystem selection to match our current element
                UpdateEventSystemSelection();

                _announcer.AnnounceInterrupt(GetActivationAnnouncement());
            }
            else
            {
                Log.Msg("{NavigatorId}", $"Rescan found no elements");
            }
        }

        /// <summary>
        /// Quiet rescan after exiting a search field. Updates elements without full activation announcement.
        /// Only announces the updated collection count if it changed.
        /// </summary>
        protected virtual void ForceRescanAfterSearch()
        {
            if (!_isActive) return;

            // Remember the old count for comparison
            int oldCount = _elements.Count;

            // Clear and rediscover elements
            _elements.Clear();
            _currentIndex = -1;

            DiscoverElements();

            if (_elements.Count > 0)
            {
                _currentIndex = 0;
                Log.Msg("{NavigatorId}", $"Search rescan: {oldCount} -> {_elements.Count} elements");

                // Update EventSystem selection
                UpdateEventSystemSelection();

                // Only announce if count changed (filter was applied)
                if (_elements.Count != oldCount)
                {
                    _announcer.AnnounceInterrupt(Strings.SearchResultsItems(_elements.Count));
                }
            }
            else
            {
                Log.Msg("{NavigatorId}", $"Search rescan found no elements");
                _announcer.AnnounceInterrupt(Strings.NoSearchResults);
            }
        }

        #endregion

        #region Constructor

        protected BaseNavigator(IAnnouncementService announcer)
        {
            _announcer = announcer;
            _inputFieldHelper = new InputFieldEditHelper(announcer);
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

            // Handle delayed search field rescan (after exiting search input)
            if (_pendingSearchRescanFrames > 0)
            {
                _pendingSearchRescanFrames--;
                if (_pendingSearchRescanFrames == 0)
                {
                    Log.Msg("{NavigatorId}", $"Executing delayed search rescan");
                    ForceRescanAfterSearch();
                }
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

            // Handle delayed re-scan after spinner value change
            if (_spinnerRescanDelay > 0)
            {
                _spinnerRescanDelay -= Time.deltaTime;
                if (_spinnerRescanDelay <= 0)
                {
                    RescanAfterSpinnerChange();
                }
            }

            // Verify elements still exist
            if (!ValidateElements())
            {
                Deactivate();
                return;
            }

            // Handle input (helper tracks prev state for Backspace character detection)
            HandleInput();

            // Track input field text for NEXT frame's Backspace character announcement
            // Must be done AFTER HandleInput so we capture current state for next frame
            // (By the time we detect Backspace, Unity has already processed it)
            TrackInputFieldState();
        }

        protected virtual void TryActivate()
        {
            if (!DetectScreen()) return;

            // Clear previous state
            _elements.Clear();
            _currentIndex = -1;

            // Discover elements
            DiscoverElements();

            if (_elements.Count == 0)
            {
                Log.Msg("{NavigatorId}", $"DetectScreen passed but no elements found");
                return;
            }

            // Activate
            _isActive = true;
            _currentIndex = 0;

            Log.Msg("{NavigatorId}", $"Activated with {_elements.Count} elements");

            OnActivated();

            // Update EventSystem selection to match our current element
            UpdateEventSystemSelection();

            // Announce screen
            _announcer.AnnounceInterrupt(GetActivationAnnouncement());

            UpdateCardNavigation();
        }

        protected virtual bool ValidateElements()
        {
            // In popup mode, validate the popup GameObject instead of elements
            if (_isInPopupMode)
            {
                if (_popupGameObject != null && _popupGameObject.activeInHierarchy)
                    return true;
                // Popup gone - exit properly so OnPopupClosed fires (e.g. craft confirmation)
                ExitPopupMode();
                OnPopupClosed();
                // Fall through to validate the restored underlying elements
            }

            // Check if first element still exists (quick validation)
            // Allow null GameObjects (TextBlock elements) - find first non-null
            if (_elements.Count == 0) return false;
            for (int i = 0; i < _elements.Count; i++)
            {
                if (_elements[i].GameObject != null)
                    return true;
            }
            // All elements have null GameObjects (all text blocks) - still valid
            return _elements.Count > 0;
        }

        public virtual void Deactivate()
        {
            if (!_isActive) return;

            Log.Msg("{NavigatorId}", $"Deactivating");

            // Clean up popup mode if active
            if (_isInPopupMode)
                ClearPopupModeState();

            DisablePopupDetection();

            _holdRepeater.Reset();

            OnDeactivating();

            _isActive = false;
            _elements.Clear();
            _currentIndex = -1;

            // Clear toggle submit blocking when navigator deactivates
            InputManager.BlockSubmitForToggle = false;
        }

        #endregion

        #region Input Handling

        // Flag to suppress navigation announcement until search rescan completes
        protected bool _suppressNavigationAnnouncement = false;

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
                        Log.Msg("{NavigatorId}", $"Synced index {_currentIndex} -> {i} ({focusedName})");
                        _currentIndex = i;
                    }
                    AnnounceCurrentElement();
                    return;
                }
            }

            Log.Msg("{NavigatorId}", $"Could not sync to focused element: {focusedName}");
        }

        /// <summary>
        /// Sync the navigator's index to a specific element (without announcing).
        /// Used before MoveNext/MovePrevious to ensure _currentIndex is correct.
        /// </summary>
        private void SyncIndexToElement(GameObject element)
        {
            if (element == null) return;

            for (int i = 0; i < _elements.Count; i++)
            {
                if (_elements[i].GameObject == element)
                {
                    if (_currentIndex != i)
                    {
                        Log.Msg("{NavigatorId}", $"Synced index {_currentIndex} -> {i} ({element.name})");
                        _currentIndex = i;
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// Early input hook called before any BaseNavigator input processing.
        /// Override to intercept input before auto-focus and navigation logic.
        /// Return true to consume input (skip all BaseNavigator handling).
        /// </summary>
        protected virtual bool HandleEarlyInput() => false;

        protected virtual void HandleInput()
        {


            // Popup mode: route all input through popup navigation
            if (_isInPopupMode)
            {
                if (!ValidatePopup())
                {
                    ExitPopupMode();
                    OnPopupClosed();
                    return;
                }
                HandlePopupInput();
                return;
            }

            // Early input hook - lets subclasses intercept before auto-focus logic
            if (HandleEarlyInput()) return;

            // Check if we're in explicit edit mode (user activated field or game focused it)
            if (UIFocusTracker.IsEditingInputField())
            {
                HandleInputFieldNavigation();
                return;
            }

            // INPUT FIELD NAVIGATION STRATEGY:
            // MTGA auto-focuses input fields when they receive EventSystem selection.
            // We handle this differently for Tab vs Arrow navigation:
            //
            // - Tab navigation: Auto-enter edit mode (traditional behavior)
            // - Arrow navigation: Deactivate auto-focus, require Enter to edit (dropdown-like)
            //
            // This block handles navigating FROM an input field (deactivates current field).
            // UpdateEventSystemSelection() handles navigating TO an input field (skips setting
            // EventSystem selection for arrow nav, preventing Unity's native navigation).
            if (UIFocusTracker.IsAnyInputFieldFocused())
            {
                GameObject fallback = IsValidIndex ? _elements[_currentIndex].GameObject : null;
                var info = _inputFieldHelper.ScanForAnyFocusedField(fallback);
                if (info.IsValid && info.GameObject != null)
                {
                    if (_lastNavigationWasTab)
                    {
                        _lastNavigationWasTab = false;

                        // Only auto-enter edit mode if our navigator's current element is
                        // actually an input field. On the Login screen, Tab to the Login button
                        // can cause the EventSystem to redirect focus to the email input field.
                        // Without this guard, we'd auto-enter edit mode on the wrong element.
                        bool currentIsInputField = IsValidIndex && UIFocusTracker.IsInputField(_elements[_currentIndex].GameObject);
                        if (currentIsInputField)
                        {
                            _inputFieldHelper.SetEditingFieldSilently(info.GameObject);
                            HandleInputFieldNavigation();
                            return;
                        }

                        // Not an input field — deactivate the rogue field and fall through
                        DeactivateInputFieldOnElement(info.GameObject);
                        var es = EventSystem.current;
                        if (es != null) es.SetSelectedGameObject(null);
                    }
                    else
                    {
                        // Arrow navigation FROM input field - deactivate and clear selection
                        // so Unity's native arrow navigation has no target
                        DeactivateInputFieldOnElement(info.GameObject);
                        var eventSystem = EventSystem.current;
                        if (eventSystem != null)
                        {
                            eventSystem.SetSelectedGameObject(null);
                        }

                        // Handle arrow keys here since Unity may have already processed them
                        if (Input.GetKeyDown(KeyCode.UpArrow))
                        {
                            MovePrevious();
                            return;
                        }
                        if (Input.GetKeyDown(KeyCode.DownArrow))
                        {
                            MoveNext();
                            return;
                        }
                        // Other keys (Enter to activate) fall through to normal handling
                    }
                }
            }
            _lastNavigationWasTab = false; // Clear flag if not used

            // Clear edit mode when no input field is focused
            if (_inputFieldHelper.IsEditing)
            {
                _inputFieldHelper.ClearEditingFieldSilently();
                UIFocusTracker.ExitInputFieldEditMode();
            }

            // Check dropdown state and detect exit transitions
            // DropdownStateManager handles all the state tracking and suppression logic
            bool justExitedDropdown = DropdownStateManager.UpdateAndCheckExitTransition();

            // When a dropdown is open, let Unity handle arrow key navigation
            if (DropdownStateManager.IsInDropdownMode)
            {
                HandleDropdownNavigation();
                return;
            }

            // If we just exited dropdown mode, sync focus and announce position
            if (justExitedDropdown)
            {
                // SyncIndexToFocusedElement already announces the current element
                SyncIndexToFocusedElement();

                // Clear EventSystem selection to prevent MTGA from auto-activating
                // the next element (e.g., Continue button via OnSelect handler).
                var eventSystem = EventSystem.current;
                if (eventSystem != null)
                {
                    eventSystem.SetSelectedGameObject(null);
                }

                return;
            }

            // Custom input first (subclass-specific keys)
            if (HandleCustomInput()) return;

            // F4: Open chat window (universal - works from any navigator)
            // Subclasses that handle F4 themselves (GeneralMenuNavigator → friends panel)
            // consume it in HandleCustomInput above, so it never reaches here.
            if (Input.GetKeyDown(KeyCode.F4))
            {
                OpenChat();
                return;
            }

            // I key: Extended card info (keyword descriptions + linked face)
            // Works in any context where a card is focused (deck builder, collection, store, draft, etc.)
            // DuelNavigator handles its own "I" key in HandleCustomInput() with browser fallback.
            if (Input.GetKeyDown(KeyCode.I))
            {
                var extInfoNav = AccessibleArenaMod.Instance?.ExtendedInfoNavigator;
                var cardNav = AccessibleArenaMod.Instance?.CardNavigator;
                if (extInfoNav != null && cardNav != null && cardNav.IsActive && cardNav.CurrentCard != null)
                {
                    extInfoNav.Open(cardNav.CurrentCard);
                }
                else
                {
                    _announcer.AnnounceInterrupt(Strings.NoCardToInspect);
                }
                return;
            }

            // Menu navigation with Arrow Up/Down (hold-to-repeat) and Tab/Shift+Tab
            if (_holdRepeater.Check(KeyCode.UpArrow, () => MovePrevious())) return;
            if (_holdRepeater.Check(KeyCode.DownArrow, () => MoveNext())) return;

            // Tab/Shift+Tab navigation - same as arrow down/up but auto-enters input fields
            // Use GetKeyDownAndConsume to prevent game from also processing Tab
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                _lastNavigationWasTab = true; // Track for input field auto-enter behavior
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
            if (_holdRepeater.Check(KeyCode.LeftArrow, () => HandleCarouselArrow(isNext: false))) return;
            if (_holdRepeater.Check(KeyCode.RightArrow, () => HandleCarouselArrow(isNext: true))) return;

            // Activation (Enter or Space)
            // Check EnterPressedWhileBlocked for when our Input.GetKeyDown patch blocked Enter on a toggle
            bool enterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || InputManager.EnterPressedWhileBlocked;
            if (InputManager.EnterPressedWhileBlocked)
            {
                InputManager.MarkEnterHandled(); // Mark as handled to prevent double-activation
            }
            bool spacePressed = AcceptSpaceKey && InputManager.GetKeyDownAndConsume(KeyCode.Space);
            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (enterPressed || spacePressed)
            {
                // Consume Enter for toggles/dropdowns so KeyboardManager.PublishKeyDown
                // doesn't pass it to the game's subscribers.
                if (IsValidIndex && enterPressed)
                {
                    var element = _elements[_currentIndex].GameObject;
                    if (element != null && (element.GetComponent<Toggle>() != null || UIFocusTracker.IsDropdown(element)))
                    {
                        InputManager.ConsumeKey(KeyCode.Return);
                        InputManager.ConsumeKey(KeyCode.KeypadEnter);
                    }
                }

                // On Login scene, let the game handle Enter for the RegistrationPanel
                // submit button natively. The game's own input system
                // (ActionSystem → Panel.OnAccept) handles that specific button.
                // All other Login buttons are still activated by our mod normally.
                if (enterPressed && IsValidIndex)
                {
                    var currentElement = _elements[_currentIndex].GameObject;
                    bool isLoginScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Login";
                    if (isLoginScene && currentElement != null
                        && currentElement.name == "MainButton_Register"
                        && IsInsideRegistrationPanel(currentElement))
                    {
                        // Don't activate — game handles it. Just return so we don't interfere.
                        return;
                    }
                }

                if (shiftHeld && enterPressed)
                {
                    // Shift+Enter activates alternate action (e.g., edit deck name)
                    ActivateAlternateAction();
                }
                else
                {
                    ActivateCurrentElement();
                }
                return;
            }

            // Letter navigation (A-Z): jump to element starting with typed letter(s)
            if (SupportsLetterNavigation && !UIFocusTracker.IsAnyInputFieldFocused())
            {
                for (KeyCode key = KeyCode.A; key <= KeyCode.Z; key++)
                {
                    if (Input.GetKeyDown(key))
                    {
                        HandleLetterNavigation(key);
                        return;
                    }
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
                Log.Msg("{NavigatorId}", $"Activating alternate action: {element.AlternateActionObject.name}");
                UIActivator.Activate(element.AlternateActionObject);
            }
            else
            {
                _announcer.AnnounceVerbose(Strings.NoAlternateAction, AnnouncementPriority.Normal);
            }
        }

        /// <summary>
        /// Build a display label from a text label, role label, and role enum.
        /// Suppresses the "button" role when tutorial messages are off,
        /// since it's purely informational. Other roles (checkbox, dropdown, slider)
        /// carry state information and are always included.
        /// </summary>
        public static string BuildLabel(string label, string roleLabel, UIElementClassifier.ElementRole role)
        {
            if (string.IsNullOrEmpty(roleLabel))
                return label;
            if (role == UIElementClassifier.ElementRole.Button &&
                AccessibleArenaMod.Instance?.Settings?.TutorialMessages == false)
                return label;
            return $"{label}, {roleLabel}";
        }

        /// <summary>
        /// Build the display label from a classification result.
        /// Subclasses may override this for custom label formatting.
        /// </summary>
        protected virtual string BuildElementLabel(UIElementClassifier.ClassificationResult classification)
        {
            return BuildLabel(classification.Label, classification.RoleLabel, classification.Role);
        }

        /// <summary>Move to next (direction=1) or previous (direction=-1) element without wrapping</summary>
        protected virtual void Move(int direction)
        {
            _letterSearch.Clear();
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
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }

            if (newIndex >= _elements.Count)
            {
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
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
        /// Handle a letter key press for jump-to-element navigation.
        /// Builds a label list from current elements and uses LetterSearchHandler.
        /// Override in subclasses with custom navigation (e.g., grouped, grid).
        /// </summary>
        /// <returns>true if the key was handled</returns>
        protected virtual bool HandleLetterNavigation(KeyCode key)
        {
            if (_elements.Count == 0) return false;

            char letter = (char)('A' + (key - KeyCode.A));

            var labels = new List<string>(_elements.Count);
            for (int i = 0; i < _elements.Count; i++)
                labels.Add(_elements[i].Label);

            int target = _letterSearch.HandleKey(letter, labels, _currentIndex);
            if (target >= 0 && target != _currentIndex)
            {
                _currentIndex = target;
                _currentActionIndex = 0;
                UpdateEventSystemSelection();
                AnnounceCurrentElement();
                UpdateCardNavigation();
            }
            else if (target == _currentIndex)
            {
                // Already on the match, re-announce
                AnnounceCurrentElement();
            }
            else
            {
                _announcer.AnnounceInterrupt(Strings.LetterSearchNoMatch(_letterSearch.Buffer));
            }
            return true;
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
                bool isInputField = UIFocusTracker.IsInputField(element);
                bool isArrowNavToInputField = isInputField && !_lastNavigationWasTab;
                bool isToggle = element.GetComponent<Toggle>() != null;
                bool isDropdown = UIFocusTracker.IsDropdown(element);

                // Set submit blocking flag BEFORE any EventSystem interaction.
                // EventSystemPatch checks this flag to block Unity's Submit events.
                // For dropdowns: prevents SendSubmitEventToSelectedObject from firing
                // before our Update opens the dropdown and sets ShouldBlockEnterFromGame.
                // On Login scene: block Enter for ALL elements except the RegistrationPanel
                // submit button, which needs the game's native path for ConnectToFrontDoor.
                bool isLoginScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Login";
                bool isRegSubmit = isLoginScene && element.name == "MainButton_Register" && IsInsideRegistrationPanel(element);
                InputManager.AllowNativeEnterOnLogin = isRegSubmit;
                InputManager.BlockSubmitForToggle = isToggle || isDropdown || (isLoginScene && !isRegSubmit);

                // INPUT FIELD HANDLING (arrow navigation):
                // Clear EventSystem selection when arrow-navigating to input fields.
                // Unity's native arrow navigation would move focus on the next frame.
                // This also prevents Enter from activating whatever was previously selected.
                if (isArrowNavToInputField)
                {
                    eventSystem.SetSelectedGameObject(null);
                }
                // INPUT FIELD HANDLING (Tab navigation):
                // Set selection but deactivate auto-focus. Tab will enter edit mode next frame.
                else if (isInputField)
                {
                    eventSystem.SetSelectedGameObject(element);
                    DeactivateInputFieldOnElement(element);
                }
                // TOGGLE HANDLING (all navigation methods):
                // Set EventSystem selection to the toggle.
                // MTGA's OnSelect handler may re-toggle the checkbox when selection changes,
                // so we track state and revert if needed.
                // (EventSystemPatch separately blocks Unity's Submit when we consume Enter/Space)
                else if (isToggle)
                {
                    // Skip SetSelectedGameObject if EventSystem already has our element selected.
                    // Calling it again would trigger OnSelect handlers unnecessarily, which can cause
                    // issues with MTGA panels like UpdatePolicies (panel closes unexpectedly).
                    if (eventSystem.currentSelectedGameObject == element)
                    {
                        return;
                    }

                    var toggle = element.GetComponent<Toggle>();
                    bool stateBefore = toggle.isOn;

                    eventSystem.SetSelectedGameObject(element);

                    // If MTGA's OnSelect handler re-toggled, revert to original state
                    if (toggle.isOn != stateBefore)
                    {
                        toggle.isOn = stateBefore;
                    }
                }
                // DROPDOWN HANDLING:
                // Set selection, then either keep open (Tab) or close (arrow keys).
                // Catches synchronous auto-opens; async auto-opens are caught by
                // HandleDropdownNavigation's !ShouldBlockEnterFromGame guard.
                else if (UIFocusTracker.IsDropdown(element))
                {
                    eventSystem.SetSelectedGameObject(element);
                    if (UIFocusTracker.IsAnyDropdownExpanded())
                    {
                        if (_lastNavigationWasTab && !DropdownStateManager.IsSuppressed)
                        {
                            // Tab from outside dropdown mode: keep dropdown open
                            // (standard screen reader behavior)
                            DropdownStateManager.OnDropdownOpened(element);
                        }
                        else
                        {
                            // Arrow navigation, or Tab from inside an open dropdown:
                            // close auto-opened dropdown (old dropdown may still be closing)
                            CloseDropdownOnElement(element);
                        }
                    }
                }
                // NORMAL ELEMENTS:
                // Just set EventSystem selection.
                else
                {
                    eventSystem.SetSelectedGameObject(element);
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

            var (kind, component) = ResolveDropdown(element);
            if (kind == DropdownKind.None) return;

            bool closed = HideDropdownComponent(kind, component);
            if (closed)
            {
                Log.Msg("{NavigatorId}", $"Closed auto-opened {kind} dropdown: {element.name}");
                // Suppress dropdown re-entry - the dropdown's IsExpanded property may not
                // update immediately after Hide(), so DropdownStateManager prevents re-entry
                // until the dropdown actually closes.
                DropdownStateManager.SuppressReentry();
            }
        }

        /// <summary>
        /// Override to handle an attached action specially instead of the default Activate call.
        /// Return true if handled; false to fall back to UIActivator.Activate.
        /// </summary>
        protected virtual bool HandleAttachedAction(AttachedAction action) => false;

        protected virtual void MoveNext() => Move(1);
        protected virtual void MovePrevious() => Move(-1);

        /// <summary>Jump to first element</summary>
        protected virtual void MoveFirst()
        {
            _letterSearch.Clear();
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
            _letterSearch.Clear();
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

            // TextBlock: re-announce the label instead of activating
            if (element == null)
            {
                if (navElement.Role == UIElementClassifier.ElementRole.TextBlock)
                {
                    _announcer?.Announce(navElement.Label, AnnouncementPriority.Normal);
                }
                return;
            }

            // Check if we're on an attached action (not the element itself)
            if (_currentActionIndex > 0 && navElement.AttachedActions != null &&
                _currentActionIndex <= navElement.AttachedActions.Count)
            {
                var action = navElement.AttachedActions[_currentActionIndex - 1];
                if (action.TargetButton != null && action.TargetButton.activeInHierarchy)
                {
                    Log.Msg("{NavigatorId}", $"Activating attached action: {action.Label} -> {action.TargetButton.name}");
                    if (!HandleAttachedAction(action))
                    {
                        var actionResult = UIActivator.Activate(action.TargetButton);
                        _announcer.Announce(actionResult.Message, AnnouncementPriority.Normal);
                    }
                    _currentActionIndex = 0;
                    return;
                }
                else if (action.TargetButton == null)
                {
                    // Info-only action: re-announce the label
                    _announcer.Announce(action.Label, AnnouncementPriority.Normal);
                    _currentActionIndex = 0;
                    return;
                }
                else
                {
                    _announcer.Announce(Strings.ActionNotAvailable, AnnouncementPriority.Normal);
                    return;
                }
            }

            Log.Msg("{NavigatorId}", $"Activating: {element.name} (ID:{element.GetInstanceID()}, Label:{navElement.Label})");

            // Capture deck count BEFORE any activation path clicks the element.
            // Game updates count synchronously during click, so this must happen first.
            OnDeckBuilderCardCountCapture();

            // Check if this is an input field - enter edit mode
            if (UIFocusTracker.IsInputField(element))
            {
                _inputFieldHelper.EnterEditMode(element);
                return;
            }

            // Check if this is a collection card in deck builder - left click adds to deck or opens craft popup
            if (CardTileActivator.IsCollectionCard(element))
            {
                Log.Msg("{NavigatorId}", $"Collection card detected - activating");
                var collectionResult = UIActivator.Activate(element);
                _announcer.Announce(collectionResult.Message, AnnouncementPriority.Normal);
                // Trigger rescan to update card count. If craft popup opens instead,
                // PerformRescan skips while popup mode is active.
                OnDeckBuilderCardActivated();
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

            // For toggles: Re-sync EventSystem selection before activating.
            // MTGA may have auto-moved selection (e.g., to submit button when form becomes valid).
            // We need to ensure EventSystem has our toggle selected so BlockSubmitForToggle works
            // and we toggle the correct element.
            // BUT: Skip if the element is no longer active (panel might have closed).
            var toggle = element.GetComponent<Toggle>();
            if (toggle != null && element.activeInHierarchy)
            {
                UpdateEventSystemSelection();
            }

            // Standard activation
            var result = UIActivator.Activate(element);

            // If a dropdown was just activated, register with DropdownStateManager
            // so _blockEnterFromGame prevents the opening Enter from also selecting an item
            if (UIFocusTracker.IsDropdown(element))
            {
                DropdownStateManager.OnDropdownOpened(element);
                _announcer.Announce(Strings.DropdownOpened, AnnouncementPriority.Normal);
                return;
            }

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

        /// <summary>
        /// Check if an element is inside a RegistrationPanel (not RegisterOrLoginPanel).
        /// Used to let the game handle Enter natively for the registration submit button only.
        /// </summary>
        protected static bool IsInsideRegistrationPanel(UnityEngine.GameObject element)
        {
            var t = element.transform.parent;
            while (t != null)
            {
                foreach (var comp in t.GetComponents<UnityEngine.MonoBehaviour>())
                {
                    if (comp != null && comp.GetType().Name == "RegistrationPanel")
                        return true;
                }
                t = t.parent;
            }
            return false;
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

            // Unity's overloaded == catches destroyed objects, but C#'s ?. does not.
            // Must check with == before accessing any properties on the object.
            if (element == null)
            {
                cardNavigator.Deactivate();
                return;
            }

            bool isCard = CardDetector.IsCard(element);
            Log.Msg("{NavigatorId}", $"UpdateCardNavigation: element={element.name}, IsCard={isCard}");
            if (isCard)
            {
                bool hidden = IsCurrentCardHidden(element);
                cardNavigator.PrepareForCard(element, isHidden: hidden);
            }
            else if (cardNavigator.IsActive)
            {
                cardNavigator.Deactivate();
            }
        }

        /// <summary>
        /// Whether the current card element should be treated as face-down/hidden.
        /// Override in navigators that support face-down cards (e.g., booster opening).
        /// </summary>
        protected virtual bool IsCurrentCardHidden(GameObject cardElement) => false;

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
        protected void AddElement(GameObject element, string label, CarouselInfo carouselInfo, GameObject alternateAction, List<AttachedAction> attachedActions, UIElementClassifier.ElementRole role = UIElementClassifier.ElementRole.Unknown)
        {
            if (element == null) return;

            // Prevent duplicates by instance ID
            int instanceId = element.GetInstanceID();
            if (_elements.Any(e => e.GameObject != null && e.GameObject.GetInstanceID() == instanceId))
            {
                Log.Msg("{NavigatorId}", $"Duplicate skipped (ID:{instanceId}): {label}");
                return;
            }

            _elements.Add(new NavigableElement
            {
                GameObject = element,
                Label = label,
                Role = role,
                Carousel = carouselInfo,
                AlternateActionObject = alternateAction,
                AttachedActions = attachedActions
            });

            string altInfo = alternateAction != null ? $" [Alt: {alternateAction.name}]" : "";
            string actionsInfo = attachedActions != null && attachedActions.Count > 0 ? $" [Actions: {attachedActions.Count}]" : "";
            Log.Msg("{NavigatorId}", $"Added (ID:{instanceId}): {label}{altInfo}{actionsInfo}");
        }

        /// <summary>Add a read-only text block (null GameObject, TextBlock role)</summary>
        protected void AddTextBlock(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _elements.Add(new NavigableElement
            {
                GameObject = null,
                Label = text,
                Role = UIElementClassifier.ElementRole.TextBlock
            });
        }

        /// <summary>Add a button, auto-extracting label from text</summary>
        protected void AddButton(GameObject buttonObj, string fallbackLabel = "Button")
        {
            if (buttonObj == null) return;

            string label = UITextExtractor.GetButtonText(buttonObj, fallbackLabel);
            AddElement(buttonObj, BuildLabel(label, Models.Strings.RoleButton, UIElementClassifier.ElementRole.Button), default, null, null, UIElementClassifier.ElementRole.Button);
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

        /// <summary>
        /// Find and activate the NavBar Home button to return to the main menu.
        /// </summary>
        protected bool NavigateToHome()
        {
            var navBar = GameObject.Find("NavBar_Desktop_16x9(Clone)");
            if (navBar == null)
                navBar = GameObject.Find("NavBar");

            if (navBar == null)
            {
                Log.Msg("{NavigatorId}", $"NavBar not found for Home navigation");
                _announcer.Announce(Models.Strings.CannotNavigateHome, Models.AnnouncementPriority.High);
                return false;
            }

            var homeButtonTransform = navBar.transform.Find("Base/Nav_Home");
            GameObject homeButton = homeButtonTransform?.gameObject;
            if (homeButton == null)
                homeButton = FindChildByName(navBar.transform, "Nav_Home");

            if (homeButton == null || !homeButton.activeInHierarchy)
            {
                Log.Msg("{NavigatorId}", $"Home button not found or inactive");
                _announcer.Announce(Models.Strings.HomeNotAvailable, Models.AnnouncementPriority.High);
                return false;
            }

            Log.Msg("{NavigatorId}", $"Navigating to Home");
            _announcer.Announce(Models.Strings.ReturningHome, Models.AnnouncementPriority.High);
            UIActivator.Activate(homeButton);
            return true;
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

            text = UITextExtractor.StripRichText(text.Trim());
            text = text.Trim();

            if (text.Length > maxLength)
                return text.Substring(0, maxLength - 3) + "...";

            return text;
        }

        #endregion

        #region Chat (F4)

        private static MethodInfo _showChatWindowMethod;
        private static bool _showChatWindowLookupDone;

        /// <summary>
        /// Open the chat window and switch to ChatNavigator.
        /// Called by F4 from any navigator (except GeneralMenuNavigator which uses F4 for friends panel).
        /// </summary>
        protected void OpenChat()
        {
            try
            {
                var socialPanel = GameObject.Find("SocialUI_V2_Desktop_16x9(Clone)");
                if (socialPanel == null)
                {
                    _announcer.AnnounceInterrupt(Strings.ChatUnavailable);
                    return;
                }

                MonoBehaviour socialUI = null;
                foreach (var comp in socialPanel.GetComponents<MonoBehaviour>())
                {
                    if (comp != null && comp.GetType().Name == T.SocialUI)
                    {
                        socialUI = comp;
                        break;
                    }
                }
                if (socialUI == null)
                {
                    _announcer.AnnounceInterrupt(Strings.ChatUnavailable);
                    return;
                }

                // Cache ShowChatWindow method
                if (!_showChatWindowLookupDone)
                {
                    _showChatWindowLookupDone = true;
                    _showChatWindowMethod = socialUI.GetType().GetMethod("ShowChatWindow", PublicInstance);
                }

                if (_showChatWindowMethod == null)
                {
                    _announcer.AnnounceInterrupt(Strings.ChatUnavailable);
                    return;
                }

                // Restore SocialUI elements before opening chat (DuelNavigator deactivates them)
                var duelNavForRestore = NavigatorManager.Instance?.GetNavigator<DuelNavigator>();
                duelNavForRestore?.RestoreSocialUIBeforeChat();

                // ShowChatWindow(SocialEntity chatFriend = null) - pass null to open last conversation
                _showChatWindowMethod.Invoke(socialUI, new object[] { null });

                // Request ChatNavigator activation
                bool activated = NavigatorManager.Instance?.RequestActivation("Chat") == true;
                if (activated)
                {
                    // Mark DuelNavigator so it shows "Returned to duel" instead of full announcement
                    duelNavForRestore?.MarkPreemptedForChat();
                }
                else
                {
                    _announcer.AnnounceInterrupt(Strings.ChatUnavailable);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("BaseNavigator", $"OpenChat failed: {ex.Message}");
                _announcer.AnnounceInterrupt(Strings.ChatUnavailable);
            }
        }

        #endregion
    }
}
