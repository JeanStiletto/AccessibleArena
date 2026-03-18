using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.PanelDetection;
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

        // Delayed re-scan after spinner value change (game needs time to update UI visibility)
        private float _spinnerRescanDelay;
        private const float SpinnerRescanDelaySeconds = 0.5f;

        // Shared input field editing helper (announcements, field info, reactivation, character detection)
        private InputFieldEditHelper _inputFieldHelper;

        // Track whether last navigation was via Tab (vs arrow keys)
        // Tab navigation should auto-enter input field edit mode, arrow keys should not
        private bool _lastNavigationWasTab;

        // Letter navigation handler (buffered jump with same-letter cycling)
        protected readonly LetterSearchHandler _letterSearch = new LetterSearchHandler();

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

        #region Popup Mode Fields

        private bool _isInPopupMode;
        private GameObject _popupGameObject;
        private List<NavigableElement> _savedElements;
        private int _savedIndex;
        private InputFieldEditHelper _popupInputHelper;
        private DropdownEditHelper _popupDropdownHelper;

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

            return $"{label}, {index + 1} of {_elements.Count}";
        }

        /// <summary>
        /// Refresh a cached element label with live state (toggle checked, input field content, dropdown value).
        /// Shared by BaseNavigator and GroupedNavigator to avoid duplicated logic.
        /// </summary>
        public static string RefreshElementLabel(GameObject obj, string label,
            UIElementClassifier.ElementRole role = UIElementClassifier.ElementRole.Unknown)
        {
            if (obj == null) return label;

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
                MelonLogger.Msg($"[{NavigatorId}] Search rescan: {oldCount} -> {_elements.Count} elements");

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
                MelonLogger.Msg($"[{NavigatorId}] Search rescan found no elements");
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
                    MelonLogger.Msg($"[{NavigatorId}] Executing delayed search rescan");
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

        /// <summary>
        /// Track current input field state for next frame's Backspace detection.
        /// Called each frame to maintain previous state.
        /// Uses scene-wide scan to handle mouse-clicked fields.
        /// </summary>
        private void TrackInputFieldState()
        {
            if (!UIFocusTracker.IsAnyInputFieldFocused() && !UIFocusTracker.IsEditingInputField())
            {
                _inputFieldHelper.TrackState(new InputFieldEditHelper.FieldInfo { IsValid = false });
                return;
            }

            GameObject fallback = IsValidIndex ? _elements[_currentIndex].GameObject : null;
            var info = _inputFieldHelper.ScanForAnyFocusedField(fallback);
            _inputFieldHelper.TrackState(info);
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

            MelonLogger.Msg($"[{NavigatorId}] Deactivating");

            // Clean up popup mode if active
            if (_isInPopupMode)
                ClearPopupModeState();

            DisablePopupDetection();

            OnDeactivating();

            _isActive = false;
            _elements.Clear();
            _currentIndex = -1;

            // Clear toggle submit blocking when navigator deactivates
            InputManager.BlockSubmitForToggle = false;
        }

        #endregion

        #region Input Handling

        /// <summary>
        /// Exit input field edit mode: clears cached field, notifies UIFocusTracker, and deactivates the field.
        /// </summary>
        /// <param name="suppressNextAnnouncement">If true (for search fields with Tab), suppress navigation announcement until rescan</param>
        /// <returns>True if this was a search field</returns>
        private bool ExitInputFieldEditMode(bool suppressNextAnnouncement = false)
        {
            // Check if we're exiting a search field - need to rescan to pick up filtered results
            bool wasSearchField = _inputFieldHelper.EditingField != null &&
                _inputFieldHelper.EditingField.name.IndexOf("Search", StringComparison.OrdinalIgnoreCase) >= 0;

            _inputFieldHelper.ExitEditMode();

            // If this was a search field, schedule delayed rescan
            if (wasSearchField)
            {
                MelonLogger.Msg($"[{NavigatorId}] Exited search field - scheduling delayed rescan");
                ScheduleSearchRescan();

                // If navigating away (Tab), suppress announcement until rescan completes
                if (suppressNextAnnouncement)
                {
                    _suppressNavigationAnnouncement = true;
                    MelonLogger.Msg($"[{NavigatorId}] Suppressing navigation announcement until rescan");
                }
            }

            return wasSearchField;
        }

        // Flag to suppress navigation announcement until search rescan completes
        protected bool _suppressNavigationAnnouncement = false;

        /// <summary>
        /// Schedule a delayed rescan after exiting a search field.
        /// Uses frame counter to wait for game's filter system to update.
        /// </summary>
        private void ScheduleSearchRescan()
        {
            // Use a flag to trigger rescan on next frame(s)
            // This avoids coroutine complexity while giving the game time to filter
            // The game's filtering and card pool updates take significant time
            _pendingSearchRescanFrames = 12; // Wait ~645ms at ~18fps game rate for filter to apply
        }

        // Counter for pending search rescan (decrements each frame, rescans when reaches 0)
        private int _pendingSearchRescanFrames = 0;

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
                ExitInputFieldEditMode();
                HandleCustomInput();
                return;
            }

            // Escape exits edit mode by deactivating the input field
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ExitInputFieldEditMode();
                _announcer.Announce(Strings.ExitedEditMode, AnnouncementPriority.Normal);
                return;
            }

            // Tab exits edit mode and navigates to next/previous element
            // Consume Tab so game doesn't interfere
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                // For search fields, suppress the navigation announcement until rescan completes
                // This prevents announcing old/stale cards before the filter has applied
                ExitInputFieldEditMode(suppressNextAnnouncement: true);
                _lastNavigationWasTab = true; // Track for consistent behavior in UpdateEventSystemSelection
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
                // Use scene-wide scan for Backspace since field may have been mouse-clicked
                GameObject fallback = IsValidIndex ? _elements[_currentIndex].GameObject : null;
                var info = _inputFieldHelper.ScanForAnyFocusedField(fallback);
                _inputFieldHelper.AnnounceDeletedCharacter(info);
                // Don't return - let key pass through to input field for actual deletion
            }
            // Up or Down arrow: announce the current input field content
            // TMP_InputField deactivates on Up/Down in single-line mode (via OnUpdateSelected
            // running before our code), so we must re-activate the field afterwards.
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                _inputFieldHelper.AnnounceFieldContent();
                _inputFieldHelper.ReactivateField();
            }
            // Left/Right arrows: announce character at cursor position
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                _inputFieldHelper.AnnounceCharacterAtCursor();
            }
            // All other keys pass through for typing
        }

        /// <summary>
        /// Handle navigation while a dropdown is open.
        /// Arrow keys and Enter are handled by Unity's dropdown.
        /// Tab/Shift+Tab closes the dropdown and navigates to the next/previous element.
        /// Escape and Backspace close the dropdown without triggering back navigation.
        /// Edit mode exits automatically when focus leaves dropdown items (detected by UIFocusTracker).
        /// </summary>
        protected virtual void HandleDropdownNavigation()
        {
            // Detect auto-opened dropdown: MTGA auto-opens dropdowns when they receive
            // EventSystem selection via arrow key navigation. If the user didn't press
            // Enter to open it (ShouldBlockEnterFromGame is false), close it immediately
            // so arrow keys return to normal element navigation instead of getting stuck
            // cycling through dropdown items.
            if (!DropdownStateManager.ShouldBlockEnterFromGame)
            {
                // Exception: if Enter is pressed, this is the user intentionally opening
                // the dropdown. Unity's EventSystem processed the Enter before our Update
                // ran, so the same keypress arrives here. Register as user-opened.
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    InputManager.ConsumeKey(KeyCode.Return);
                    InputManager.ConsumeKey(KeyCode.KeypadEnter);
                    var active = DropdownStateManager.ActiveDropdown;
                    if (active != null)
                        DropdownStateManager.OnDropdownOpened(active);
                    _announcer.Announce(Strings.DropdownOpened, AnnouncementPriority.Normal);
                    return;
                }

                // Auto-opened dropdown (arrow navigation triggered MTGA's OnSelect).
                // Close it and return - next frame normal navigation will proceed.
                MelonLogger.Msg($"[{NavigatorId}] Closing auto-opened dropdown (not user-initiated)");
                var dropdown = DropdownStateManager.ActiveDropdown;
                if (dropdown != null)
                    CloseDropdownOnElement(dropdown);
                else
                    DropdownStateManager.SuppressReentry();
                return;
            }

            // Tab/Shift+Tab: Close current dropdown and navigate to next/previous element.
            // Uses our element list order rather than Unity's spatial navigation order.
            // If the next element is also a dropdown, it auto-opens (standard screen reader behavior).
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                // Close silently - the next element's announcement is sufficient feedback
                CloseActiveDropdown(silent: true);
                // Suppress reentry so the old closing dropdown doesn't keep us in dropdown mode.
                // If the next element is a dropdown, OnDropdownOpened will clear the suppression.
                DropdownStateManager.SuppressReentry();

                _lastNavigationWasTab = true;
                bool shiftTab = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (shiftTab)
                    MovePrevious();
                else
                    MoveNext();
                return;
            }

            // Escape or Backspace: Close the dropdown explicitly
            // We must intercept these because the game handles Escape as "back" which
            // navigates to the previous screen instead of just closing the dropdown
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            {
                CloseActiveDropdown();
                return;
            }

            // Enter: select the currently focused dropdown item and close the dropdown.
            // We block SendSubmitEventToSelectedObject (via EventSystemPatch) so Unity's
            // normal Submit path never fires. This prevents the game's onValueChanged
            // callback from triggering chain auto-advance to the next dropdown.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
                SelectDropdownItem();
                CloseActiveDropdown(silent: true);
                return;
            }

            // Arrow keys pass through to Unity's dropdown handling
            // (FocusTracker announces focused items as they change)
        }

        /// <summary>
        /// Manually select the currently focused dropdown item.
        /// Sets the value via reflection to bypass onValueChanged, preventing the game's
        /// chain auto-advance mechanism. The caller is responsible for closing the dropdown.
        /// </summary>
        private void SelectDropdownItem() => SelectCurrentDropdownItem(NavigatorId);

        /// <summary>
        /// Select the currently focused dropdown item (static, reusable by DropdownEditHelper).
        /// Parses item index from EventSystem selection name, sets value silently on the active dropdown.
        /// </summary>
        public static void SelectCurrentDropdownItem(string callerId)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var selectedItem = eventSystem.currentSelectedGameObject;
            if (selectedItem == null) return;

            // Parse item index from name (format: "Item N: ...")
            int itemIndex = -1;
            string itemName = selectedItem.name;
            if (itemName.StartsWith("Item "))
            {
                string indexStr = itemName.Substring(5);
                int colonPos = indexStr.IndexOf(':');
                if (colonPos > 0)
                    indexStr = indexStr.Substring(0, colonPos);
                int.TryParse(indexStr.Trim(), out itemIndex);
            }

            if (itemIndex < 0)
            {
                MelonLogger.Msg($"[{callerId}] Could not parse dropdown item index from: {itemName}");
                return;
            }

            var activeDropdown = DropdownStateManager.ActiveDropdown;
            if (activeDropdown == null)
            {
                MelonLogger.Msg($"[{callerId}] No active dropdown to select item on");
                return;
            }

            // Set value without triggering onValueChanged (it's suppressed while dropdown is open).
            // The pending value is stored so OnDropdownClosed can fire onValueChanged after
            // restoring the callback - this notifies the game so changes persist.
            if (SetDropdownValueSilent(activeDropdown, itemIndex))
            {
                DropdownStateManager.OnDropdownItemSelected(itemIndex);
                MelonLogger.Msg($"[{callerId}] Selected dropdown item {itemIndex}");
            }
        }

        #region Dropdown Dispatch Helper

        /// <summary>
        /// Identifies what kind of dropdown component is on a GameObject.
        /// </summary>
        private enum DropdownKind { None, TMP, Legacy, Custom }

        /// <summary>
        /// Resolves the dropdown type and component on a GameObject.
        /// Returns the kind and the component (which may be TMP_Dropdown, Dropdown, or cTMP_Dropdown).
        /// </summary>
        private static (DropdownKind kind, Component component) ResolveDropdown(GameObject obj)
        {
            if (obj == null) return (DropdownKind.None, null);

            var tmp = obj.GetComponent<TMPro.TMP_Dropdown>();
            if (tmp != null) return (DropdownKind.TMP, tmp);

            var legacy = obj.GetComponent<Dropdown>();
            if (legacy != null) return (DropdownKind.Legacy, legacy);

            foreach (var c in obj.GetComponents<Component>())
            {
                if (c != null && c.GetType().Name == T.CustomTMPDropdown)
                    return (DropdownKind.Custom, c);
            }

            return (DropdownKind.None, null);
        }

        /// <summary>
        /// Calls Hide() on a resolved dropdown component.
        /// </summary>
        private static bool HideDropdownComponent(DropdownKind kind, Component component)
        {
            switch (kind)
            {
                case DropdownKind.TMP:
                    ((TMPro.TMP_Dropdown)component).Hide();
                    return true;
                case DropdownKind.Legacy:
                    ((Dropdown)component).Hide();
                    return true;
                case DropdownKind.Custom:
                    var hideMethod = component.GetType().GetMethod("Hide", PublicInstance);
                    if (hideMethod != null)
                    {
                        hideMethod.Invoke(component, null);
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        #endregion

        /// <summary>
        /// Set a dropdown's value without triggering onValueChanged callback.
        /// For TMP_Dropdown: uses SetValueWithoutNotify.
        /// For cTMP_Dropdown: uses reflection to set m_Value + RefreshShownValue.
        /// </summary>
        public static bool SetDropdownValueSilent(GameObject dropdownObj, int itemIndex)
        {
            var (kind, component) = ResolveDropdown(dropdownObj);
            switch (kind)
            {
                case DropdownKind.TMP:
                    ((TMPro.TMP_Dropdown)component).SetValueWithoutNotify(itemIndex);
                    return true;
                case DropdownKind.Legacy:
                    ((Dropdown)component).SetValueWithoutNotify(itemIndex);
                    return true;
                case DropdownKind.Custom:
                    var type = component.GetType();
                    type.GetField("m_Value", PrivateInstance)?.SetValue(component, itemIndex);
                    type.GetMethod("RefreshShownValue", AllInstanceFlags)?.Invoke(component, null);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Get the currently displayed text value of a dropdown (works for TMP_Dropdown, Dropdown, and cTMP_Dropdown).
        /// Reads the caption text child component which shows the localized display value.
        /// Do NOT call RefreshShownValue() as it overwrites captionText from m_Value,
        /// which may be stale (game may set captionText directly without updating m_Value).
        /// </summary>
        public static string GetDropdownDisplayValue(GameObject dropdownObj)
        {
            var (kind, component) = ResolveDropdown(dropdownObj);
            switch (kind)
            {
                case DropdownKind.TMP:
                {
                    var tmp = (TMPro.TMP_Dropdown)component;
                    if (tmp.captionText != null) return tmp.captionText.text;
                    break;
                }
                case DropdownKind.Legacy:
                {
                    var legacy = (Dropdown)component;
                    if (legacy.captionText != null) return legacy.captionText.text;
                    break;
                }
                case DropdownKind.Custom:
                {
                    var type = component.GetType();
                    var captionField = type.GetField("m_CaptionText", PrivateInstance);
                    if (captionField != null)
                    {
                        var captionText = captionField.GetValue(component) as TMPro.TMP_Text;
                        if (captionText != null) return captionText.text;
                    }
                    var captionProp = type.GetProperty("captionText", PublicInstance);
                    if (captionProp != null)
                    {
                        var captionText = captionProp.GetValue(component) as TMPro.TMP_Text;
                        if (captionText != null) return captionText.text;
                    }
                    break;
                }
            }

            return GetDropdownFirstOptionFallback(dropdownObj);
        }

        /// <summary>
        /// Fallback for dropdowns with value=-1 and empty caption: return options[0].text if available.
        /// </summary>
        private static string GetDropdownFirstOptionFallback(GameObject dropdownObj)
        {
            var (kind, component) = ResolveDropdown(dropdownObj);
            switch (kind)
            {
                case DropdownKind.TMP:
                {
                    var tmp = (TMPro.TMP_Dropdown)component;
                    if (tmp.value < 0 && tmp.options?.Count > 0) return tmp.options[0].text;
                    break;
                }
                case DropdownKind.Legacy:
                {
                    var legacy = (Dropdown)component;
                    if (legacy.value < 0 && legacy.options?.Count > 0) return legacy.options[0].text;
                    break;
                }
                case DropdownKind.Custom:
                {
                    var type = component.GetType();
                    var valueProp = type.GetProperty("value", PublicInstance);
                    int value = valueProp != null ? (int)valueProp.GetValue(component) : 0;
                    if (value >= 0) break;
                    var optionsProp = type.GetProperty("options", PublicInstance);
                    var options = optionsProp?.GetValue(component) as System.Collections.IList;
                    if (options?.Count > 0)
                    {
                        var textProp = options[0]?.GetType().GetProperty("text", PublicInstance);
                        return textProp?.GetValue(options[0]) as string;
                    }
                    break;
                }
            }

            return null;
        }

        /// <summary>
        /// Close the currently active dropdown by finding its parent TMP_Dropdown and calling Hide().
        /// </summary>
        /// <param name="silent">If true, skip "dropdown closed" announcement (used when Tab navigates away)</param>
        private void CloseActiveDropdown(bool silent = false) => CloseDropdown(NavigatorId, _announcer, silent);

        /// <summary>
        /// Close the currently active dropdown (static, reusable by DropdownEditHelper).
        /// Finds the dropdown via DropdownStateManager.ActiveDropdown or hierarchy walk, calls Hide().
        /// </summary>
        public static void CloseDropdown(string callerId, IAnnouncementService announcer, bool silent)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
            {
                DropdownStateManager.OnDropdownClosed();
                return;
            }

            var currentItem = eventSystem.currentSelectedGameObject;

            // First try DropdownStateManager.ActiveDropdown - this is set when dropdown opens
            // and works even when focus is on a Blocker element (modal backdrop)
            var activeDropdown = DropdownStateManager.ActiveDropdown;
            if (activeDropdown != null)
            {
                var (kind, comp) = ResolveDropdown(activeDropdown);
                if (kind != DropdownKind.None && HideDropdownComponent(kind, comp))
                {
                    MelonLogger.Msg($"[{callerId}] Closing {kind} dropdown via ActiveDropdown reference");
                    DropdownStateManager.OnDropdownClosed();
                    if (!silent) announcer?.Announce(Strings.DropdownClosed, AnnouncementPriority.Normal);
                    return;
                }
            }

            // Fallback: Find dropdown in parent hierarchy of current selection
            var transform = currentItem.transform;
            while (transform != null)
            {
                var (kind, comp) = ResolveDropdown(transform.gameObject);
                if (kind != DropdownKind.None && HideDropdownComponent(kind, comp))
                {
                    MelonLogger.Msg($"[{callerId}] Closing {kind} dropdown via hierarchy walk");
                    DropdownStateManager.OnDropdownClosed();
                    if (!silent) announcer?.Announce(Strings.DropdownClosed, AnnouncementPriority.Normal);
                    return;
                }
                transform = transform.parent;
            }

            // Couldn't find dropdown - just exit edit mode
            MelonLogger.Msg($"[{callerId}] Could not find dropdown to close, exiting edit mode");
            DropdownStateManager.OnDropdownClosed();
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
                        MelonLogger.Msg($"[{NavigatorId}] Synced index {_currentIndex} -> {i} ({element.name})");
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
                        // Tab navigation - enter edit mode immediately
                        _lastNavigationWasTab = false;
                        _inputFieldHelper.SetEditingFieldSilently(info.GameObject);
                        HandleInputFieldNavigation();
                        return;
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

            // Menu navigation with Arrow Up/Down and Tab/Shift+Tab
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
                MelonLogger.Msg($"[{NavigatorId}] Activating alternate action: {element.AlternateActionObject.name}");
                UIActivator.Activate(element.AlternateActionObject);
            }
            else
            {
                _announcer.AnnounceVerbose(Strings.NoAlternateAction, AnnouncementPriority.Normal);
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

            // Handle action-based steppers (e.g., popup craft count via reflection)
            if (info.OnIncrement != null || info.OnDecrement != null)
            {
                var action = isNext ? info.OnIncrement : info.OnDecrement;
                if (action != null)
                {
                    action();
                    _stepperAnnounceDelay = StepperAnnounceDelaySeconds;
                }
                return true;
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
            if (info.UseHoverActivation)
            {
                UIActivator.SimulateHover(control, isNext);
                // Schedule delayed re-scan - spinner value change may show/hide UI elements
                _spinnerRescanDelay = SpinnerRescanDelaySeconds;
            }
            else
            {
                UIActivator.Activate(control);
            }

            // Schedule delayed announcement - game needs a frame to update the value
            _stepperAnnounceDelay = StepperAnnounceDelaySeconds;

            return true;
        }

        /// <summary>
        /// Handle slider arrow keys by letting Unity's built-in Slider.OnMove do the work (10% steps).
        /// We just ensure the slider is selected and schedule a delayed announcement.
        /// </summary>
        private bool HandleSliderArrow(Slider slider, bool isNext)
        {
            if (slider == null || !slider.interactable)
                return false;

            // Ensure slider is selected in Unity's EventSystem so its built-in
            // OnMove handles the value change (10% steps per arrow key press)
            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != slider.gameObject)
            {
                eventSystem.SetSelectedGameObject(slider.gameObject);
            }

            // Schedule delayed announcement — Unity needs a frame to process the value change
            _stepperAnnounceDelay = StepperAnnounceDelaySeconds;

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
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return true;
            }
            if (newActionIndex >= totalOptions)
            {
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
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
        /// Quiet re-scan after a spinner value change. The game may show/hide UI elements
        /// depending on the selected option (e.g. tournament vs challenge match types).
        /// Preserves focus on the current stepper element if it still exists.
        /// </summary>
        protected virtual void RescanAfterSpinnerChange()
        {
            if (!_isActive || !IsValidIndex) return;

            // Remember what we're focused on
            var currentObj = _elements[_currentIndex].GameObject;
            int oldCount = _elements.Count;

            // Re-discover elements
            _elements.Clear();
            _currentIndex = -1;
            DiscoverElements();

            if (_elements.Count == 0) return;

            // Try to restore focus to the same element
            if (currentObj != null)
            {
                for (int i = 0; i < _elements.Count; i++)
                {
                    if (_elements[i].GameObject == currentObj)
                    {
                        _currentIndex = i;
                        break;
                    }
                }
            }

            if (_currentIndex < 0)
                _currentIndex = 0;

            // Only announce if element count changed
            if (_elements.Count != oldCount)
            {
                MelonLogger.Msg($"[{NavigatorId}] Spinner rescan: {oldCount} -> {_elements.Count} elements");
                string posAnnouncement = Strings.ItemPositionOf(_currentIndex + 1, _elements.Count, _elements[_currentIndex].Label);
                _announcer.Announce(posAnnouncement, AnnouncementPriority.Normal);
            }
        }

        /// <summary>
        /// Announce the current stepper/carousel value after a delay.
        /// Called from Update() when the delay expires.
        /// </summary>
        private void AnnounceStepperValue()
        {
            if (!IsValidIndex)
                return;

            // For elements with ReadLabel (e.g., popup craft count), re-read directly
            var carousel = _elements[_currentIndex].Carousel;
            if (carousel.ReadLabel != null)
            {
                string newLabel = carousel.ReadLabel();
                if (!string.IsNullOrEmpty(newLabel))
                {
                    var updated = _elements[_currentIndex];
                    updated.Label = newLabel;
                    _elements[_currentIndex] = updated;
                    MelonLogger.Msg($"[{NavigatorId}] Stepper value (ReadLabel): {newLabel}");
                    _announcer.AnnounceInterrupt(newLabel);
                }
                return;
            }

            var currentElement = _elements[_currentIndex].GameObject;
            if (currentElement != null)
            {
                // Re-classify to get the updated label with new value
                var classification = UIElementClassifier.Classify(currentElement);

                // Update cached label and role in our element list
                var updatedElement = _elements[_currentIndex];
                updatedElement.Label = BuildElementLabel(classification);
                updatedElement.Role = classification.Role;
                _elements[_currentIndex] = updatedElement;

                // For sliders, announce just the percent value
                if (classification.Role == UIElementClassifier.ElementRole.Slider && classification.SliderComponent != null)
                {
                    var slider = classification.SliderComponent;
                    float range = slider.maxValue - slider.minValue;
                    int percent = range > 0 ? Mathf.RoundToInt((slider.value - slider.minValue) / range * 100) : 0;
                    MelonLogger.Msg($"[{NavigatorId}] Slider value: {percent}%");
                    _announcer.AnnounceInterrupt(Strings.Percent(percent));
                }
                else
                {
                    MelonLogger.Msg($"[{NavigatorId}] Stepper value updated: {classification.Label}");
                    _announcer.Announce(classification.Label, AnnouncementPriority.High);
                }
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
                MelonLogger.Msg($"[{NavigatorId}] Closed auto-opened {kind} dropdown: {element.name}");
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

        /// <summary>
        /// Enter edit mode on an input field with a custom announcement.
        /// Use this instead of EnterEditMode when you want to supply your own announcement text.
        /// </summary>
        protected void EnterInputFieldEditModeDirectly(GameObject field, string announcement)
        {
            _inputFieldHelper.SetEditingFieldSilently(field);
            UIActivator.Activate(field);
            _inputFieldHelper.TrackState();
            _announcer?.Announce(announcement, AnnouncementPriority.Normal);
        }

        /// <summary>
        /// Gets the text of the currently active input field in edit mode, or null if not editing.
        /// Subclasses can use this in their HandleInputFieldNavigation overrides.
        /// </summary>
        protected string GetEditingFieldText()
        {
            var info = _inputFieldHelper.GetEditingFieldInfo();
            return info.IsValid ? info.Text : null;
        }

        /// <summary>
        /// Exit input field edit mode directly, bypassing the search-field rescan logic.
        /// Deactivating the field fires onEndEdit, so the game processes any pending submit
        /// (e.g. a rename) through its normal event handler.
        /// Use this when a subclass handles submission itself (e.g. Enter in rename mode).
        /// </summary>
        protected void ForceExitFieldEditMode()
        {
            _inputFieldHelper.ExitEditMode();
        }

        /// <summary>
        /// Deactivate an input field on the specified element if it was auto-focused.
        /// Used to counteract MTGA's auto-focus behavior when navigating to input fields.
        /// User must press Enter to explicitly activate the field.
        /// </summary>
        private void DeactivateInputFieldOnElement(GameObject element)
        {
            if (element == null) return;

            // Check TMP_InputField
            var tmpInput = element.GetComponent<TMPro.TMP_InputField>();
            if (tmpInput != null && tmpInput.isFocused)
            {
                tmpInput.DeactivateInputField();
                MelonLogger.Msg($"[{NavigatorId}] Deactivated auto-focused TMP_InputField: {element.name}");
                return;
            }

            // Check legacy InputField
            var legacyInput = element.GetComponent<UnityEngine.UI.InputField>();
            if (legacyInput != null && legacyInput.isFocused)
            {
                legacyInput.DeactivateInputField();
                MelonLogger.Msg($"[{NavigatorId}] Deactivated auto-focused InputField: {element.name}");
            }
        }

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
                    MelonLogger.Msg($"[{NavigatorId}] Activating attached action: {action.Label} -> {action.TargetButton.name}");
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

            MelonLogger.Msg($"[{NavigatorId}] Activating: {element.name} (ID:{element.GetInstanceID()}, Label:{navElement.Label})");

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
            if (UIActivator.IsCollectionCard(element))
            {
                MelonLogger.Msg($"[{NavigatorId}] Collection card detected - activating");
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
            MelonLogger.Msg($"[{NavigatorId}] UpdateCardNavigation: element={element.name}, IsCard={isCard}");
            if (isCard)
            {
                cardNavigator.PrepareForCard(element);
            }
            else if (cardNavigator.IsActive)
            {
                cardNavigator.Deactivate();
            }
        }

        #endregion

        #region Popup Mode

        /// <summary>Whether popup mode is currently active</summary>
        protected bool IsInPopupMode => _isInPopupMode;

        /// <summary>The current popup's GameObject</summary>
        protected GameObject PopupGameObject => _popupGameObject;

        /// <summary>
        /// Subscribe to PanelStateManager for popup detection.
        /// Call in OnActivated(). Automatically unsubscribed on deactivation.
        /// </summary>
        protected void EnablePopupDetection()
        {
            if (PanelStateManager.Instance != null)
                PanelStateManager.Instance.OnPanelChanged += OnPopupPanelChanged;
        }

        /// <summary>
        /// Unsubscribe from PanelStateManager popup detection.
        /// </summary>
        protected void DisablePopupDetection()
        {
            if (PanelStateManager.Instance != null)
                PanelStateManager.Instance.OnPanelChanged -= OnPopupPanelChanged;
        }

        /// <summary>
        /// PanelStateManager callback for popup detection.
        /// </summary>
        private void OnPopupPanelChanged(PanelInfo oldPanel, PanelInfo newPanel)
        {
            if (!_isActive) return;

            if (newPanel != null && !IsPopupExcluded(newPanel) && IsPopupPanel(newPanel))
            {
                if (!_isInPopupMode)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Popup detected: {newPanel.Name}");
                    OnPopupDetected(newPanel);
                }
            }
            else if (_isInPopupMode)
            {
                // Popup closed: active panel reverted to the underlying panel (or null)
                MelonLogger.Msg($"[{NavigatorId}] Popup closed");
                ExitPopupMode();
                OnPopupClosed();
            }
        }

        /// <summary>
        /// Check if a panel is a popup/dialog that should be handled.
        /// </summary>
        public static bool IsPopupPanel(PanelInfo panel)
        {
            if (panel == null) return false;
            if (panel.Type == PanelType.Popup) return true;
            string name = panel.Name;
            return name.Contains("SystemMessageView") ||
                   name.Contains("Popup") ||
                   name.Contains("Dialog") ||
                   name.Contains("Modal") ||
                   name.Contains("ChallengeInvite");
        }

        /// <summary>
        /// Enter popup mode: save current elements, discover popup elements, announce.
        /// </summary>
        protected void EnterPopupMode(GameObject popup)
        {
            if (popup == null) return;

            MelonLogger.Msg($"[{NavigatorId}] Entering popup mode: {popup.name}");

            // Deactivate card info navigator so Up/Down navigates popup items, not card blocks
            AccessibleArenaMod.Instance?.CardNavigator?.Deactivate();

            // Save current state
            _savedElements = new List<NavigableElement>(_elements);
            _savedIndex = _currentIndex;

            // Switch to popup mode
            _isInPopupMode = true;
            _popupGameObject = popup;
            _elements.Clear();
            _currentIndex = -1;

            // Clear toggle submit blocking - popup elements are independent of the underlying screen.
            // The previous element might have been a toggle, leaving BlockSubmitForToggle=true,
            // which would block Enter on popup buttons via EventSystemPatch.
            InputManager.BlockSubmitForToggle = false;

            // Create helpers for popup input fields and dropdowns
            _popupInputHelper = new InputFieldEditHelper(_announcer);
            _popupDropdownHelper = new DropdownEditHelper(_announcer, NavigatorId);

            // Discover popup elements
            DiscoverPopupElements(popup);

            MelonLogger.Msg($"[{NavigatorId}] Popup mode: {_elements.Count} items discovered");

            // Auto-focus first actionable item (input field, dropdown, or button), otherwise first item
            int firstActionable = _elements.FindIndex(e =>
                e.Role == UIElementClassifier.ElementRole.Button ||
                e.Role == UIElementClassifier.ElementRole.TextField ||
                e.Role == UIElementClassifier.ElementRole.Dropdown);
            _currentIndex = firstActionable >= 0 ? firstActionable : (_elements.Count > 0 ? 0 : -1);

            AnnouncePopupOpen();
        }

        /// <summary>
        /// Exit popup mode: restore saved elements and index.
        /// </summary>
        protected void ExitPopupMode()
        {
            if (!_isInPopupMode) return;

            MelonLogger.Msg($"[{NavigatorId}] Exiting popup mode");
            ClearPopupModeState();
        }

        /// <summary>
        /// Clear all popup mode state and restore saved elements.
        /// </summary>
        private void ClearPopupModeState()
        {
            _popupInputHelper?.Clear();
            _popupDropdownHelper?.Clear();
            _popupInputHelper = null;
            _popupDropdownHelper = null;

            _isInPopupMode = false;

            // Restore saved elements
            if (_savedElements != null)
            {
                _elements.Clear();
                _elements.AddRange(_savedElements);
                _currentIndex = _savedIndex;
                _savedElements = null;

                // Refresh labels to pick up changes made while popup was open
                // (e.g., deck name edited in DeckDetailsPopup).
                // RefreshElementLabel re-reads live input field text, toggle state, dropdown values.
                for (int i = 0; i < _elements.Count; i++)
                {
                    var elem = _elements[i];
                    if (elem.GameObject != null)
                    {
                        string refreshed = RefreshElementLabel(elem.GameObject, elem.Label, elem.Role);
                        if (refreshed != elem.Label)
                        {
                            elem.Label = refreshed;
                            _elements[i] = elem;
                        }
                    }
                }
            }

            _popupGameObject = null;
        }

        /// <summary>
        /// Validate that the popup is still active.
        /// </summary>
        private bool ValidatePopup()
        {
            return _popupGameObject != null && _popupGameObject.activeInHierarchy;
        }

        /// <summary>
        /// Dismiss the popup using a 4-level chain:
        /// 1. Find cancel button by pattern
        /// 2. Click dismiss overlay (game's click-outside-to-close button)
        /// 3. SystemMessageView.OnBack(null) via reflection
        /// 4. SetActive(false) as last resort
        /// </summary>
        protected void DismissPopup()
        {
            if (!_isInPopupMode || _popupGameObject == null) return;

            // Level 1: Find cancel button
            var cancelButton = FindPopupCancelButton(_popupGameObject);
            if (cancelButton != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Popup: clicking cancel button: {cancelButton.name}");
                _announcer?.Announce(Strings.Cancelled, AnnouncementPriority.High);

                // Try invoking CustomButton.OnClick directly first - bypasses CanvasGroup
                // interactable checks that block pointer simulation during popup animations
                if (TryInvokeCustomButtonOnClick(cancelButton))
                {
                    MelonLogger.Msg($"[{NavigatorId}] Popup: dismissed via CustomButton.OnClick.Invoke()");
                    return;
                }

                UIActivator.Activate(cancelButton);
                return;
            }

            // Level 1.5: FriendInvitePanel has no cancel button or dismiss overlay.
            // The game closes it via SocialUI.HandleKeyDown(Escape) -> _friendInvitePanel.Close().
            // Call Close() directly via reflection - it destroys the GO and fires Callback_OnClose.
            if (TryCloseFriendInvitePanel(_popupGameObject))
            {
                MelonLogger.Msg($"[{NavigatorId}] Popup: dismissed FriendInvitePanel via Close()");
                _announcer?.Announce(Strings.Cancelled, AnnouncementPriority.High);
                ExitPopupMode();
                OnPopupClosed();
                return;
            }

            // Level 2: Click dismiss overlay (e.g., Blade_DismissButton, BackgroundImage)
            // These are the game's click-outside-to-close buttons. Clicking them closes
            // the popup through the game's own mechanism, so detectors pick up the close.
            var dismissOverlay = FindDismissOverlay(_popupGameObject);
            if (dismissOverlay != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Popup: clicking dismiss overlay: {dismissOverlay.name}");
                _announcer?.Announce(Strings.Cancelled, AnnouncementPriority.High);
                UIActivator.Activate(dismissOverlay);
                return;
            }

            // Level 3: SystemMessageView.OnBack(null)
            MelonLogger.Msg($"[{NavigatorId}] Popup: no cancel button or dismiss overlay found, trying OnBack()");
            var systemMessageView = FindSystemMessageViewInPopup(_popupGameObject);
            if (systemMessageView != null && TryInvokeOnBack(systemMessageView))
            {
                MelonLogger.Msg($"[{NavigatorId}] Popup: dismissed via OnBack()");
                _announcer?.Announce(Strings.Cancelled, AnnouncementPriority.High);
                ExitPopupMode();
                OnPopupClosed();
                return;
            }

            // Level 4: SetActive(false) fallback
            MelonLogger.Warning($"[{NavigatorId}] Popup: using SetActive(false) fallback");
            _popupGameObject.SetActive(false);
            _announcer?.Announce(Strings.Cancelled, AnnouncementPriority.High);
            ExitPopupMode();
            OnPopupClosed();
        }

        /// <summary>
        /// Handle input while in popup mode.
        /// Up/Down: navigate, Enter: activate, Backspace: dismiss.
        /// </summary>
        private void HandlePopupInput()
        {
            // Dropdown edit mode intercepts all keys first
            if (_popupDropdownHelper != null && _popupDropdownHelper.IsEditing)
            {
                _popupDropdownHelper.HandleEditing(dir => NavigatePopupItem(dir));
                return;
            }

            // Input field edit mode intercepts all keys first
            if (_popupInputHelper != null && _popupInputHelper.IsEditing)
            {
                _popupInputHelper.HandleEditing(dir => NavigatePopupItem(dir));
                _popupInputHelper.TrackState();
                return;
            }

            // Up/Shift+Tab: previous item
            if (Input.GetKeyDown(KeyCode.UpArrow) ||
                (Input.GetKeyDown(KeyCode.Tab) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))))
            {
                NavigatePopupItem(-1);
                return;
            }

            // Down/Tab: next item
            if (Input.GetKeyDown(KeyCode.DownArrow) ||
                (Input.GetKeyDown(KeyCode.Tab) && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)))
            {
                NavigatePopupItem(1);
                return;
            }

            // Enter/Space: activate current item
            // Use GetEnterAndConsume which also checks EnterPressedWhileBlocked
            // (defensive: in case BlockSubmitForToggle becomes stale)
            if (InputManager.GetEnterAndConsume() || Input.GetKeyDown(KeyCode.Space))
            {
                InputManager.ConsumeKey(KeyCode.Space);
                ActivatePopupItem();
                return;
            }

            // Left/Right: stepper (e.g., craft count)
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                HandleCarouselArrow(false);
                return;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                HandleCarouselArrow(true);
                return;
            }

            // Backspace: dismiss popup
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                DismissPopup();
                return;
            }

            // Letter navigation (A-Z) in popups
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

            // F4: toggle Friends panel (works even during popups)
            if (Input.GetKeyDown(KeyCode.F4))
                HandleCustomInput();
        }

        private void NavigatePopupItem(int direction)
        {
            _letterSearch.Clear();
            if (_elements.Count == 0) return;

            int newIndex = _currentIndex + direction;

            if (newIndex < 0)
            {
                _announcer?.AnnounceInterruptVerbose(Strings.BeginningOfList);
                return;
            }
            if (newIndex >= _elements.Count)
            {
                _announcer?.AnnounceInterruptVerbose(Strings.EndOfList);
                return;
            }

            _currentIndex = newIndex;
            AnnouncePopupCurrentItem();
        }

        private void ActivatePopupItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _elements.Count) return;

            var elem = _elements[_currentIndex];

            if (elem.Role == UIElementClassifier.ElementRole.TextBlock)
            {
                // Re-read text block
                AnnouncePopupCurrentItem();
                return;
            }

            if (elem.Role == UIElementClassifier.ElementRole.TextField && elem.GameObject != null)
            {
                _popupInputHelper?.EnterEditMode(elem.GameObject);
                return;
            }

            if (elem.Role == UIElementClassifier.ElementRole.Dropdown && elem.GameObject != null)
            {
                _popupDropdownHelper?.EnterEditMode(elem.GameObject);
                return;
            }

            if (elem.GameObject != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Popup: activating: {elem.Label}");
                _announcer?.AnnounceInterrupt(Strings.Activating(elem.Label));
                UIActivator.Activate(elem.GameObject);
            }
        }

        #region Popup Announcements

        private void AnnouncePopupOpen()
        {
            string title = ExtractPopupTitle(_popupGameObject);

            // Fall back to first text block
            if (string.IsNullOrEmpty(title))
            {
                foreach (var elem in _elements)
                {
                    if (elem.Role == UIElementClassifier.ElementRole.TextBlock)
                    {
                        title = elem.Label;
                        break;
                    }
                }
            }

            string announcement = !string.IsNullOrEmpty(title)
                ? $"Popup: {title}. {_elements.Count} items."
                : $"Popup. {_elements.Count} items.";

            _announcer?.AnnounceInterrupt(announcement);

            // Auto-announce focused item
            if (_currentIndex >= 0 && _currentIndex < _elements.Count)
                AnnouncePopupCurrentItem();
        }

        private void AnnouncePopupCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _elements.Count) return;

            var elem = _elements[_currentIndex];
            string label = elem.Label;

            // Refresh dynamic labels
            if (elem.Role == UIElementClassifier.ElementRole.TextField && elem.GameObject != null)
            {
                label = RefreshElementLabel(elem.GameObject, label, UIElementClassifier.ElementRole.TextField);
            }
            else if (elem.Role == UIElementClassifier.ElementRole.Dropdown && elem.GameObject != null)
            {
                string currentValue = GetDropdownDisplayValue(elem.GameObject);
                if (!string.IsNullOrEmpty(currentValue))
                    label = $"{currentValue}, {Strings.RoleDropdown}";
            }
            else if (elem.Role == UIElementClassifier.ElementRole.Button)
            {
                label = BuildLabel(label, Strings.RoleButton, UIElementClassifier.ElementRole.Button);
            }

            _announcer?.Announce(
                $"{label}, {_currentIndex + 1} of {_elements.Count}",
                AnnouncementPriority.Normal);
        }

        #endregion

        #region Popup Element Discovery

        /// <summary>
        /// Discover navigable elements in a popup.
        /// Override for custom discovery logic.
        /// </summary>
        protected virtual void DiscoverPopupElements(GameObject popup)
        {
            if (popup == null) return;

            var addedObjects = new HashSet<GameObject>();

            // Check for DeckCostsDetails for structured deck info
            bool hasDeckCosts = HasComponentInChildren(popup, "DeckCostsDetails");

            var skipTransforms = new List<Transform>();
            if (hasDeckCosts)
            {
                CollectWidgetContentTransforms(popup, "DeckTypesDetails", "ItemParent", skipTransforms);
                CollectWidgetContentTransforms(popup, "DeckColorsDetails", null, skipTransforms);
                CollectWidgetContentTransforms(popup, "CosmeticSelectorController", null, skipTransforms);
            }

            // Phase 1: Discover text blocks
            DiscoverPopupTextBlocks(popup, hasDeckCosts, skipTransforms);

            // Phase 2: Discover input fields
            DiscoverPopupInputFields(popup, addedObjects);

            // Phase 3: Discover dropdowns
            DiscoverPopupDropdowns(popup, addedObjects);

            // Phase 4: Discover buttons
            // Skip for FriendInvitePanel - it has a broken send button; user submits via Enter
            // in the input field instead (which triggers onSubmit -> HandleSubmitInput correctly)
            if (!HasComponentInChildren(popup, "FriendInvitePanel"))
            {
                DiscoverPopupButtons(popup, addedObjects);

                // Phase 5: Remove text blocks duplicating button labels
                DeduplicateTextBlocksAgainstButtons();
            }

            // Phase 6: Detect stepper elements (e.g., craft quantity in CardViewerPopup)
            DiscoverPopupSteppers(popup);
        }

        private void DiscoverPopupTextBlocks(GameObject popup, bool hasDeckCosts, List<Transform> skipTransforms)
        {
            var seenTexts = new HashSet<string>();

            foreach (var tmp in popup.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;

                if (IsInsideButton(tmp.transform, popup.transform)) continue;
                if (IsInsideInputField(tmp.transform, popup.transform)) continue;
                if (IsInsideDropdown(tmp.transform, popup.transform)) continue;
                if (IsInsideTitleContainer(tmp.transform, popup.transform)) continue;

                if (hasDeckCosts && IsInsideComponentByName(tmp.transform, popup.transform, "DeckCostsDetails"))
                    continue;
                if (skipTransforms.Count > 0 && IsChildOfAny(tmp.transform, skipTransforms))
                    continue;

                string text = UITextExtractor.CleanText(tmp.text);
                if (string.IsNullOrWhiteSpace(text) || text.Length < 3) continue;

                var lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length < 3) continue;
                    if (seenTexts.Contains(trimmed)) continue;

                    seenTexts.Add(trimmed);
                    AddTextBlock(trimmed);
                    MelonLogger.Msg($"[{NavigatorId}] Popup: text block: {trimmed}");
                }
            }

            // Inject structured deck info if applicable
            if (hasDeckCosts)
            {
                var deckInfo = DeckInfoProvider.GetDeckInfoElements();
                if (deckInfo != null)
                {
                    foreach (var (label, text) in deckInfo)
                    {
                        string combined = $"{label}: {text}";
                        AddTextBlock(combined);
                        MelonLogger.Msg($"[{NavigatorId}] Popup: deck info: {combined}");
                    }
                }
            }
        }

        private void DiscoverPopupInputFields(GameObject popup, HashSet<GameObject> addedObjects)
        {
            var discovered = new List<(GameObject obj, string label, float sortOrder)>();

            foreach (var field in popup.GetComponentsInChildren<TMP_InputField>(true))
            {
                if (field == null || !field.gameObject.activeInHierarchy || !field.interactable) continue;
                if (addedObjects.Contains(field.gameObject)) continue;

                string label = UITextExtractor.GetInputFieldLabel(field.gameObject);
                var pos = field.gameObject.transform.position;
                discovered.Add((field.gameObject, label, -pos.y * 1000 + pos.x));
                addedObjects.Add(field.gameObject);
            }

            foreach (var (obj, label, _) in discovered.OrderBy(x => x.sortOrder))
            {
                _elements.Add(new NavigableElement
                {
                    GameObject = obj,
                    Label = label,
                    Role = UIElementClassifier.ElementRole.TextField
                });
                MelonLogger.Msg($"[{NavigatorId}] Popup: input field: {label}");
            }
        }

        private void DiscoverPopupDropdowns(GameObject popup, HashSet<GameObject> addedObjects)
        {
            var discovered = new List<(GameObject obj, string label, float sortOrder)>();

            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (addedObjects.Contains(mb.gameObject)) continue;
                if (IsInsideInputField(mb.transform, popup.transform)) continue;

                string typeName = mb.GetType().Name;
                bool isDropdown = typeName == T.CustomTMPDropdown ||
                                  mb is TMP_Dropdown ||
                                  mb is Dropdown;
                if (!isDropdown) continue;

                string displayValue = GetDropdownDisplayValue(mb.gameObject);
                string label = !string.IsNullOrEmpty(displayValue)
                    ? $"{displayValue}, {Strings.RoleDropdown}"
                    : $"{mb.gameObject.name}, {Strings.RoleDropdown}";

                var pos = mb.gameObject.transform.position;
                discovered.Add((mb.gameObject, label, -pos.y * 1000 + pos.x));
                addedObjects.Add(mb.gameObject);
            }

            foreach (var (obj, label, _) in discovered.OrderBy(x => x.sortOrder))
            {
                _elements.Add(new NavigableElement
                {
                    GameObject = obj,
                    Label = label,
                    Role = UIElementClassifier.ElementRole.Dropdown
                });
                MelonLogger.Msg($"[{NavigatorId}] Popup: dropdown: {label}");
            }
        }

        private void DiscoverPopupButtons(GameObject popup, HashSet<GameObject> addedObjects)
        {
            var discovered = new List<(GameObject obj, string label, float sortOrder)>();

            // Pass 1: SystemMessageButtonView
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (addedObjects.Contains(mb.gameObject)) continue;
                if (IsInsideInputField(mb.transform, popup.transform)) continue;
                if (IsInsideDropdown(mb.transform, popup.transform)) continue;

                if (mb.GetType().Name == T.SystemMessageButtonView)
                {
                    string label = UITextExtractor.GetText(mb.gameObject);
                    if (string.IsNullOrEmpty(label)) label = mb.gameObject.name;
                    var pos = mb.gameObject.transform.position;
                    discovered.Add((mb.gameObject, label, -pos.y * 1000 + pos.x));
                    addedObjects.Add(mb.gameObject);
                }
            }

            // Pass 2: CustomButton / CustomButtonWithTooltip
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (addedObjects.Contains(mb.gameObject)) continue;
                if (IsInsideInputField(mb.transform, popup.transform)) continue;
                if (IsInsideDropdown(mb.transform, popup.transform)) continue;
                if (IsInsideButton(mb.transform, popup.transform)) continue;

                string typeName = mb.GetType().Name;
                if (typeName == T.CustomButton || typeName == T.CustomButtonWithTooltip)
                {
                    string label = UITextExtractor.GetText(mb.gameObject);
                    if (string.IsNullOrEmpty(label)) label = mb.gameObject.name;
                    var pos = mb.gameObject.transform.position;
                    discovered.Add((mb.gameObject, label, -pos.y * 1000 + pos.x));
                    addedObjects.Add(mb.gameObject);
                }
            }

            // Pass 3: Standard Unity Buttons
            foreach (var button in popup.GetComponentsInChildren<Button>(true))
            {
                if (button == null || !button.gameObject.activeInHierarchy || !button.interactable) continue;
                if (addedObjects.Contains(button.gameObject)) continue;
                if (IsInsideInputField(button.transform, popup.transform)) continue;
                if (IsInsideDropdown(button.transform, popup.transform)) continue;
                if (IsInsideButton(button.transform, popup.transform)) continue;

                string label = UITextExtractor.GetText(button.gameObject);
                if (string.IsNullOrEmpty(label)) label = button.gameObject.name;
                var pos = button.gameObject.transform.position;
                discovered.Add((button.gameObject, label, -pos.y * 1000 + pos.x));
                addedObjects.Add(button.gameObject);
            }

            // Sort, filter dismiss overlays, deduplicate
            var seenLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (obj, label, _) in discovered.OrderBy(x => x.sortOrder))
            {
                if (IsDismissOverlay(obj))
                {
                    MelonLogger.Msg($"[{NavigatorId}] Popup: skipping dismiss overlay: {obj.name}");
                    continue;
                }
                if (!seenLabels.Add(label))
                {
                    MelonLogger.Msg($"[{NavigatorId}] Popup: skipping duplicate button: {label}");
                    continue;
                }

                _elements.Add(new NavigableElement
                {
                    GameObject = obj,
                    Label = label,
                    Role = UIElementClassifier.ElementRole.Button
                });
                MelonLogger.Msg($"[{NavigatorId}] Popup: button: {label}");
            }
        }

        private void DeduplicateTextBlocksAgainstButtons()
        {
            var buttonLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var elem in _elements)
            {
                if (elem.Role == UIElementClassifier.ElementRole.Button)
                    buttonLabels.Add(elem.Label);
            }
            if (buttonLabels.Count == 0) return;

            int removed = _elements.RemoveAll(e =>
                e.Role == UIElementClassifier.ElementRole.TextBlock && buttonLabels.Contains(e.Label));
            if (removed > 0)
                MelonLogger.Msg($"[{NavigatorId}] Popup: removed {removed} text blocks duplicating button labels");
        }

        /// <summary>
        /// Detect stepper elements in the popup via reflection (e.g., craft quantity in CardViewerPopup).
        /// Finds controllers with known increment/decrement methods and a count label.
        /// </summary>
        private void DiscoverPopupSteppers(GameObject popup)
        {
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var type = mb.GetType();
                if (type.Name != "CardViewerController") continue;

                // Get pip objects (shared between stepper and no-stepper paths)
                var pipsField = type.GetField("_CraftPips",
                    PrivateInstance);
                var pipObjects = new HashSet<GameObject>();
                if (pipsField != null)
                {
                    var pips = pipsField.GetValue(mb) as System.Collections.IList;
                    if (pips != null)
                    {
                        foreach (var pip in pips)
                        {
                            var pipMb = pip as MonoBehaviour;
                            if (pipMb != null) pipObjects.Add(pipMb.gameObject);
                        }
                    }
                }

                // Read actual owned count from controller (not pip count which is always 4)
                int ownedCount = 0;
                var collectedQtyField = type.GetField("_collectedQuantity",
                    PrivateInstance);
                if (collectedQtyField != null)
                {
                    ownedCount = (int)collectedQtyField.GetValue(mb);
                }

                // Find the craft count label
                var countLabelField = type.GetField("_craftCountLabel",
                    PrivateInstance);
                var countLabel = countLabelField?.GetValue(mb) as TMP_Text;

                bool hasStepper = countLabel != null && countLabel.gameObject.activeInHierarchy;

                if (hasStepper)
                {
                    // Find increment/decrement methods
                    var increaseMethod = type.GetMethod("Unity_OnCraftIncrease",
                        PublicInstance);
                    var decreaseMethod = type.GetMethod("Unity_OnCraftDecrease",
                        PublicInstance);
                    if (increaseMethod == null || decreaseMethod == null) continue;

                    string countText = UITextExtractor.CleanText(countLabel.text);
                    if (string.IsNullOrEmpty(countText)) countText = "0";

                    // Remove any text block that duplicates the count label
                    _elements.RemoveAll(e =>
                        e.Role == UIElementClassifier.ElementRole.TextBlock &&
                        e.Label == countText);

                    // Find first pip index before removing, so we can insert at that position
                    int pipInsertIndex = _elements.FindIndex(e =>
                        e.GameObject != null && pipObjects.Contains(e.GameObject));

                    // Replace craft pip buttons with single owned count
                    int removedPips = _elements.RemoveAll(e =>
                        e.GameObject != null && pipObjects.Contains(e.GameObject));
                    if (pipInsertIndex < 0 || pipInsertIndex > _elements.Count)
                        pipInsertIndex = _elements.Count;

                    // Insert owned count first, then stepper after it
                    if (removedPips > 0)
                    {
                        GameObject firstPip = null;
                        foreach (var go in pipObjects) { firstPip = go; break; }
                        _elements.Insert(pipInsertIndex, new NavigableElement
                        {
                            GameObject = firstPip,
                            Label = Models.Strings.CardOwned(ownedCount),
                            Role = UIElementClassifier.ElementRole.TextBlock
                        });
                        pipInsertIndex++; // stepper goes after owned
                    }

                    string label = $"{countText}, {Models.Strings.RoleStepperHint}";

                    _elements.Insert(pipInsertIndex, new NavigableElement
                    {
                        GameObject = countLabel.gameObject,
                        Label = label,
                        Role = UIElementClassifier.ElementRole.Stepper,
                        Carousel = new CarouselInfo
                        {
                            HasArrowNavigation = true,
                            OnIncrement = () =>
                            {
                                try { increaseMethod.Invoke(mb, null); }
                                catch (Exception ex) { MelonLogger.Warning($"[{NavigatorId}] Craft increment failed: {ex.Message}"); }
                            },
                            OnDecrement = () =>
                            {
                                try { decreaseMethod.Invoke(mb, null); }
                                catch (Exception ex) { MelonLogger.Warning($"[{NavigatorId}] Craft decrement failed: {ex.Message}"); }
                            },
                            ReadLabel = () =>
                            {
                                try { return UITextExtractor.CleanText(countLabel.text); }
                                catch { return null; }
                            }
                        }
                    });

                    MelonLogger.Msg($"[{NavigatorId}] Popup: craft stepper: {countText}, owned: {ownedCount}");
                }
                else if (pipObjects.Count > 0)
                {
                    // No stepper (fully owned) — replace individual pips with single ownership text
                    int pipInsertIndex = _elements.FindIndex(e =>
                        e.GameObject != null && pipObjects.Contains(e.GameObject));

                    _elements.RemoveAll(e =>
                        e.GameObject != null && pipObjects.Contains(e.GameObject));

                    if (pipInsertIndex < 0 || pipInsertIndex > _elements.Count)
                        pipInsertIndex = _elements.Count;

                    GameObject firstPip = null;
                    foreach (var go in pipObjects) { firstPip = go; break; }

                    _elements.Insert(pipInsertIndex, new NavigableElement
                    {
                        GameObject = firstPip,
                        Label = Models.Strings.CardOwned(ownedCount),
                        Role = UIElementClassifier.ElementRole.TextBlock
                    });

                    MelonLogger.Msg($"[{NavigatorId}] Popup: owned: {ownedCount} (no stepper)");
                }

                break;
            }
        }

        #endregion

        #region Popup Helpers

        private string ExtractPopupTitle(GameObject popup)
        {
            if (popup == null) return null;

            foreach (var tmp in popup.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;
                if (!IsInsideTitleContainer(tmp.transform, popup.transform)) continue;

                string text = UITextExtractor.CleanText(tmp.text);
                if (!string.IsNullOrWhiteSpace(text) && text.Length >= 3)
                    return text.Trim();
            }
            return null;
        }

        private static bool IsInsideTitleContainer(Transform child, Transform stopAt)
        {
            Transform current = child;
            while (current != null && current != stopAt)
            {
                string name = current.name;
                if (name.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Header", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                current = current.parent;
            }
            return false;
        }

        private static bool IsInsideButton(Transform child, Transform stopAt)
        {
            Transform current = child.parent;
            while (current != null && current != stopAt)
            {
                foreach (var mb in current.GetComponents<MonoBehaviour>())
                {
                    if (mb != null)
                    {
                        string typeName = mb.GetType().Name;
                        if (typeName == T.CustomButton || typeName == T.CustomButtonWithTooltip ||
                            typeName == T.SystemMessageButtonView)
                            return true;
                    }
                }
                if (current.GetComponent<Button>() != null)
                    return true;
                current = current.parent;
            }
            return false;
        }

        private static bool IsInsideDropdown(Transform child, Transform stopAt)
        {
            Transform current = child.parent;
            while (current != null && current != stopAt)
            {
                if (UIFocusTracker.IsDropdown(current.gameObject))
                    return true;
                current = current.parent;
            }
            return false;
        }

        private static bool IsInsideInputField(Transform child, Transform stopAt)
        {
            Transform current = child.parent;
            while (current != null && current != stopAt)
            {
                if (current.GetComponent<TMP_InputField>() != null)
                    return true;
                current = current.parent;
            }
            return false;
        }

        private static bool IsInsideComponentByName(Transform child, Transform stopAt, string typeName)
        {
            Transform current = child;
            while (current != null && current != stopAt)
            {
                foreach (var mb in current.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == typeName)
                        return true;
                }
                current = current.parent;
            }
            return false;
        }

        private static bool HasComponentInChildren(GameObject go, string typeName)
        {
            if (go == null) return false;
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && mb.GetType().Name == typeName)
                    return true;
            }
            return false;
        }

        private static bool IsDismissOverlay(GameObject obj)
        {
            string name = obj.name.ToLower();
            return name.Contains("background") || name.Contains("overlay") ||
                   name.Contains("backdrop") || name.Contains("dismiss");
        }

        /// <summary>
        /// Find a dismiss overlay button in the popup (e.g., Blade_DismissButton).
        /// Searches both standard Unity Buttons and CustomButton/AdvancedButton components.
        /// Prefers buttons with "dismiss" in the name over generic "background" ones.
        /// </summary>
        private static GameObject FindDismissOverlay(GameObject popup)
        {
            if (popup == null) return null;

            GameObject fallback = null;

            // Scan all MonoBehaviours to catch CustomButton, AdvancedButton, and standard Button
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                string typeName = mb.GetType().Name;
                if (typeName != T.CustomButton && typeName != "AdvancedButton" &&
                    typeName != "Button") continue;
                if (!IsDismissOverlay(mb.gameObject)) continue;

                // Prefer "dismiss" buttons over generic "background" ones
                if (mb.gameObject.name.ToLower().Contains("dismiss"))
                    return mb.gameObject;

                if (fallback == null)
                    fallback = mb.gameObject;
            }

            return fallback;
        }

        private static bool IsChildOfAny(Transform child, List<Transform> parents)
        {
            foreach (var parent in parents)
            {
                if (child.IsChildOf(parent))
                    return true;
            }
            return false;
        }

        private static void CollectWidgetContentTransforms(GameObject popup, string componentTypeName,
            string fieldName, List<Transform> skipTransforms)
        {
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.GetType().Name != componentTypeName) continue;

                if (fieldName != null)
                {
                    var field = mb.GetType().GetField(fieldName, PublicInstance);
                    if (field != null)
                    {
                        var transform = field.GetValue(mb) as Transform;
                        if (transform != null)
                            skipTransforms.Add(transform);
                    }
                }
                else
                {
                    skipTransforms.Add(mb.transform);
                }
                break;
            }
        }

        /// <summary>
        /// Find the cancel/close/no button in a popup using pattern matching + reflection fallback.
        /// </summary>
        private GameObject FindPopupCancelButton(GameObject popup)
        {
            if (popup == null) return null;

            string[] cancelPatterns = { "cancel", "close", "back", "no", "abbrechen", "nein", "zurück" };

            // Pass 1: SystemMessageButtonView
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == T.SystemMessageButtonView && MatchesCancelPattern(mb.gameObject, cancelPatterns))
                    return mb.gameObject;
            }

            // Pass 2: CustomButton / CustomButtonWithTooltip
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                string typeName = mb.GetType().Name;
                if ((typeName == T.CustomButton || typeName == T.CustomButtonWithTooltip) &&
                    MatchesCancelPattern(mb.gameObject, cancelPatterns))
                    return mb.gameObject;
            }

            // Pass 3: Standard Unity Buttons
            foreach (var button in popup.GetComponentsInChildren<Button>(true))
            {
                if (button == null || !button.gameObject.activeInHierarchy || !button.interactable) continue;
                if (MatchesCancelPattern(button.gameObject, cancelPatterns))
                    return button.gameObject;
            }

            // Pass 4: _cancelButton via reflection
            foreach (var mb in popup.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                var field = mb.GetType().GetField("_cancelButton",
                    PrivateInstance);
                if (field == null) continue;

                if (field.GetValue(mb) is MonoBehaviour cancelMb && cancelMb != null && cancelMb.gameObject != null)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Popup: found _cancelButton via reflection on {mb.GetType().Name}");
                    return cancelMb.gameObject;
                }
            }

            return null;
        }

        /// <summary>
        /// Try to close a FriendInvitePanel popup via its Close() method.
        /// This popup has no cancel button or dismiss overlay - the game only closes it via Escape.
        /// </summary>
        private static bool TryCloseFriendInvitePanel(GameObject popup)
        {
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && mb.GetType().Name == "FriendInvitePanel")
                {
                    var closeMethod = mb.GetType().GetMethod("Close", PublicInstance);
                    if (closeMethod != null)
                    {
                        closeMethod.Invoke(mb, null);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Find a named button field via reflection on the popup's components and invoke its OnClick.
        /// Returns true if the button was found and invoked.
        /// </summary>
        protected bool TryInvokePopupButtonByFieldName(string fieldName)
        {
            if (_popupGameObject == null) return false;

            foreach (var mb in _popupGameObject.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                var field = mb.GetType().GetField(fieldName,
                    PrivateInstance);
                if (field == null) continue;

                if (field.GetValue(mb) is MonoBehaviour buttonMb && buttonMb != null)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Found {fieldName} via reflection on {mb.GetType().Name}");
                    if (TryInvokeCustomButtonOnClick(buttonMb.gameObject))
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Invoked {fieldName}.OnClick successfully");
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Try to invoke a CustomButton's OnClick event directly via reflection.
        /// Bypasses CanvasGroup/Selectable interactable checks that block pointer simulation.
        /// </summary>
        private bool TryInvokeCustomButtonOnClick(GameObject buttonObj)
        {
            if (buttonObj == null) return false;

            foreach (var mb in buttonObj.GetComponents<MonoBehaviour>())
            {
                if (mb == null || mb.GetType().Name != T.CustomButton) continue;

                // CustomButton.OnClick is a public property returning a UnityEvent-like type
                var onClickProp = mb.GetType().GetProperty("OnClick",
                    PublicInstance);
                if (onClickProp == null)
                {
                    // Try as field
                    var onClickField = mb.GetType().GetField("_onClick",
                        PrivateInstance);
                    if (onClickField == null) continue;

                    var onClickVal = onClickField.GetValue(mb);
                    if (onClickVal == null) continue;

                    var invokeMethod = onClickVal.GetType().GetMethod("Invoke", Type.EmptyTypes);
                    if (invokeMethod != null)
                    {
                        invokeMethod.Invoke(onClickVal, null);
                        return true;
                    }
                    continue;
                }

                var onClick = onClickProp.GetValue(mb);
                if (onClick == null) continue;

                var invoke = onClick.GetType().GetMethod("Invoke", Type.EmptyTypes);
                if (invoke != null)
                {
                    invoke.Invoke(onClick, null);
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesCancelPattern(GameObject obj, string[] patterns)
        {
            string buttonText = UITextExtractor.GetText(obj)?.ToLower() ?? "";
            string buttonName = obj.name.ToLower();

            foreach (var pattern in patterns)
            {
                if (ContainsCancelWord(buttonText, pattern) || ContainsCancelWord(buttonName, pattern))
                    return true;
            }
            return false;
        }

        private static bool ContainsCancelWord(string text, string word)
        {
            if (string.IsNullOrEmpty(text)) return false;
            int idx = 0;
            while ((idx = text.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
            {
                bool startOk = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
                bool endOk = idx + word.Length >= text.Length || !char.IsLetterOrDigit(text[idx + word.Length]);
                if (startOk && endOk) return true;
                idx += word.Length;
            }
            return false;
        }

        private MonoBehaviour FindSystemMessageViewInPopup(GameObject popup)
        {
            if (popup == null) return null;

            // Search children
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && mb.GetType().Name == "SystemMessageView")
                    return mb;
            }

            // Search up hierarchy
            var current = popup.transform.parent;
            while (current != null)
            {
                foreach (var mb in current.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == "SystemMessageView")
                        return mb;
                }
                current = current.parent;
            }

            // Scene-wide fallback
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "SystemMessageView" && mb.gameObject.activeInHierarchy)
                    return mb;
            }

            return null;
        }

        private bool TryInvokeOnBack(MonoBehaviour component)
        {
            if (component == null) return false;

            var type = component.GetType();
            foreach (var method in type.GetMethods(AllInstanceFlags))
            {
                if (method.Name == "OnBack" && method.GetParameters().Length == 1)
                {
                    try
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Popup: invoking {type.Name}.OnBack(null)");
                        method.Invoke(component, new object[] { null });
                        return true;
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[{NavigatorId}] Popup: OnBack error: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
            }

            return false;
        }

        #endregion

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
                MelonLogger.Msg($"[{NavigatorId}] Duplicate skipped (ID:{instanceId}): {label}");
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
            MelonLogger.Msg($"[{NavigatorId}] Added (ID:{instanceId}): {label}{altInfo}{actionsInfo}");
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
                MelonLogger.Msg($"[{NavigatorId}] NavBar not found for Home navigation");
                _announcer.Announce(Models.Strings.CannotNavigateHome, Models.AnnouncementPriority.High);
                return false;
            }

            var homeButtonTransform = navBar.transform.Find("Base/Nav_Home");
            GameObject homeButton = homeButtonTransform?.gameObject;
            if (homeButton == null)
                homeButton = FindChildByName(navBar.transform, "Nav_Home");

            if (homeButton == null || !homeButton.activeInHierarchy)
            {
                MelonLogger.Msg($"[{NavigatorId}] Home button not found or inactive");
                _announcer.Announce(Models.Strings.HomeNotAvailable, Models.AnnouncementPriority.High);
                return false;
            }

            MelonLogger.Msg($"[{NavigatorId}] Navigating to Home");
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
    }
}
