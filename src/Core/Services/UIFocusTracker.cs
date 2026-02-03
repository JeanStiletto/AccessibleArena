using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MelonLoader;
using AccessibleArena.Core.Interfaces;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Tracks UI focus changes using Unity's EventSystem and announces them via screen reader.
    /// Polls EventSystem.current.currentSelectedGameObject each frame to detect selection changes.
    /// Also provides Tab navigation fallback when Unity's navigation is broken (menu scenes only).
    /// </summary>
    public class UIFocusTracker
    {
        private const int MAX_SELECTABLE_LOG_COUNT = 10;

        private readonly IAnnouncementService _announcer;
        private GameObject _lastSelected;
        private string _lastAnnouncedText;

        // Input field edit mode - only true when user explicitly activated (Enter) the field
        private static bool _inputFieldEditMode;
        private static GameObject _activeInputFieldObject;

        // Dropdown edit mode - kept for explicit close tracking via Escape/Backspace
        // The actual "is dropdown open" state is queried from the dropdown's IsExpanded property
        private static bool _dropdownEditMode;
        private static GameObject _activeDropdownObject;

        // Cache for IsExpanded property lookup (reflection is expensive)
        private static System.Reflection.PropertyInfo _cachedIsExpandedProperty;
        private static System.Type _cachedDropdownType;

        // Flag to suppress focus announcements when navigator is handling the focus change
        private static bool _suppressNextFocusAnnouncement;

        // Flag to suppress dropdown mode re-entry after closing an auto-opened dropdown
        private static bool _suppressDropdownModeEntry;

        /// <summary>
        /// Suppress the next focus change announcement. Called by navigators before they
        /// change EventSystem selection, since they handle their own announcements.
        /// </summary>
        public static void SuppressNextFocusAnnouncement()
        {
            _suppressNextFocusAnnouncement = true;
        }

        /// <summary>
        /// Suppress dropdown mode entry. Called after closing an auto-opened dropdown
        /// to prevent FocusTracker from re-entering dropdown mode before IsExpanded updates.
        /// </summary>
        public static void SuppressDropdownModeEntry()
        {
            _suppressDropdownModeEntry = true;
            DebugConfig.LogIf(DebugConfig.LogFocusTracking, "FocusTracker", "Suppressing dropdown mode entry");
        }

        /// <summary>
        /// Fired when focus changes. Parameters: (oldElement, newElement)
        /// </summary>
        public event Action<GameObject, GameObject> OnFocusChanged;

        /// <summary>
        /// Returns true if user is actively editing an input field (pressed Enter to activate).
        /// When true, arrow keys control cursor. When false, arrows navigate between elements.
        /// </summary>
        public static bool IsEditingInputField()
        {
            // Only return true if user explicitly entered edit mode AND the field is still focused
            if (!_inputFieldEditMode || _activeInputFieldObject == null)
                return false;

            // Verify the input field is still actually focused
            var tmpInput = _activeInputFieldObject.GetComponent<TMPro.TMP_InputField>();
            if (tmpInput != null && tmpInput.isFocused)
                return true;

            var legacyInput = _activeInputFieldObject.GetComponent<UnityEngine.UI.InputField>();
            if (legacyInput != null && legacyInput.isFocused)
                return true;

            // Field lost focus, exit edit mode
            ExitInputFieldEditMode();
            return false;
        }

        /// <summary>
        /// Returns true if any input field is focused (caret visible, user can type).
        /// This handles both cases:
        /// 1. User navigated to input field via Tab (EventSystem selection is the field)
        /// 2. User clicked on input field with mouse (field.isFocused is true but EventSystem may differ)
        /// Use this to avoid intercepting keys like Backspace/Escape that should go to the input field.
        /// </summary>
        public static bool IsAnyInputFieldFocused()
        {
            // First check: EventSystem selection is an input field
            // Use interactable (not isFocused) so KeyboardManagerPatch can block Escape
            // even before the field is fully focused
            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
            {
                var selected = eventSystem.currentSelectedGameObject;

                var tmpInput = selected.GetComponent<TMPro.TMP_InputField>();
                if (tmpInput != null && tmpInput.interactable)
                    return true;

                var legacyInput = selected.GetComponent<UnityEngine.UI.InputField>();
                if (legacyInput != null && legacyInput.interactable)
                    return true;
            }

            // Second check: Any input field has isFocused = true (caret visible)
            // This catches mouse-clicked input fields where EventSystem selection may differ
            var tmpInputFields = GameObject.FindObjectsOfType<TMPro.TMP_InputField>();
            foreach (var field in tmpInputFields)
            {
                if (field.isFocused)
                    return true;
            }

            var legacyInputFields = GameObject.FindObjectsOfType<UnityEngine.UI.InputField>();
            foreach (var field in legacyInputFields)
            {
                if (field.isFocused)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Enter edit mode for an input field. Called when user presses Enter on an input field.
        /// </summary>
        public static void EnterInputFieldEditMode(GameObject inputFieldObject)
        {
            _inputFieldEditMode = true;
            _activeInputFieldObject = inputFieldObject;
            DebugConfig.LogIf(DebugConfig.LogFocusTracking, "FocusTracker", $"Entered input field edit mode: {inputFieldObject?.name}");
        }

        /// <summary>
        /// Exit edit mode. Called when user presses Escape/Tab or focus leaves the field.
        /// </summary>
        public static void ExitInputFieldEditMode()
        {
            if (_inputFieldEditMode)
            {
                DebugConfig.LogIf(DebugConfig.LogFocusTracking, "FocusTracker", "Exited input field edit mode");
                _inputFieldEditMode = false;
                _activeInputFieldObject = null;
            }
        }

        /// <summary>
        /// Deactivate any currently focused input field.
        /// Called when user presses Escape to exit an input field they clicked into.
        /// Also clears EventSystem selection so IsAnyInputFieldFocused() returns false.
        /// </summary>
        public static void DeactivateFocusedInputField()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
                return;

            var selected = eventSystem.currentSelectedGameObject;

            var tmpInput = selected.GetComponent<TMPro.TMP_InputField>();
            if (tmpInput != null)
            {
                // Deactivate if focused (caret visible)
                if (tmpInput.isFocused)
                {
                    tmpInput.DeactivateInputField();
                }
                // Always clear selection so IsAnyInputFieldFocused() returns false
                // MTGA auto-activates input fields on selection, so we need to clear
                // even if isFocused is false (caret not visible but still selected)
                eventSystem.SetSelectedGameObject(null);
                DebugConfig.LogIf(DebugConfig.LogFocusTracking, "FocusTracker", $"Deactivated TMP_InputField: {selected.name} (wasFocused={tmpInput.isFocused})");
                return;
            }

            var legacyInput = selected.GetComponent<UnityEngine.UI.InputField>();
            if (legacyInput != null)
            {
                if (legacyInput.isFocused)
                {
                    legacyInput.DeactivateInputField();
                }
                eventSystem.SetSelectedGameObject(null);
                DebugConfig.LogIf(DebugConfig.LogFocusTracking, "FocusTracker", $"Deactivated InputField: {selected.name} (wasFocused={legacyInput.isFocused})");
                return;
            }
        }

        /// <summary>
        /// Check if a GameObject is an input field (TMP or legacy).
        /// </summary>
        public static bool IsInputField(GameObject obj)
        {
            if (obj == null) return false;
            return obj.GetComponent<TMPro.TMP_InputField>() != null ||
                   obj.GetComponent<UnityEngine.UI.InputField>() != null;
        }

        /// <summary>
        /// Returns true if any dropdown is currently expanded (open).
        /// Uses the actual IsExpanded property from the dropdown component - no assumptions.
        /// When true, arrow keys control dropdown selection. When false, arrows navigate between elements.
        /// </summary>
        public static bool IsEditingDropdown()
        {
            // Check the REAL state: is any dropdown actually expanded?
            return IsAnyDropdownExpanded();
        }

        /// <summary>
        /// Check if any dropdown in the scene has IsExpanded == true.
        /// This queries the actual dropdown state, not assumptions based on focus.
        /// </summary>
        public static bool IsAnyDropdownExpanded()
        {
            // Check cTMP_Dropdown (MTGA's custom dropdown) - most common in MTGA
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;
                var type = mb.GetType();
                if (type.Name == "cTMP_Dropdown")
                {
                    bool isExpanded = GetIsExpandedProperty(mb);
                    if (isExpanded)
                    {
                        return true;
                    }
                }
            }

            // Check standard TMP_Dropdown
            foreach (var dropdown in GameObject.FindObjectsOfType<TMPro.TMP_Dropdown>())
            {
                if (dropdown == null) continue;
                // TMP_Dropdown has a public IsExpanded property
                if (IsDropdownExpanded(dropdown))
                {
                    return true;
                }
            }

            // Check legacy Unity Dropdown
            foreach (var dropdown in GameObject.FindObjectsOfType<UnityEngine.UI.Dropdown>())
            {
                if (dropdown == null) continue;
                // Legacy Dropdown - check if dropdown list exists
                if (IsLegacyDropdownExpanded(dropdown))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Get IsExpanded property value from a cTMP_Dropdown via reflection.
        /// </summary>
        private static bool GetIsExpandedProperty(MonoBehaviour dropdown)
        {
            if (dropdown == null) return false;

            try
            {
                var type = dropdown.GetType();

                // Use cached property if same type
                if (_cachedDropdownType != type)
                {
                    _cachedDropdownType = type;
                    _cachedIsExpandedProperty = type.GetProperty("IsExpanded",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                }

                if (_cachedIsExpandedProperty != null)
                {
                    return (bool)_cachedIsExpandedProperty.GetValue(dropdown);
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[FocusTracker] Error getting IsExpanded: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Check if a TMP_Dropdown is expanded by checking for its dropdown list child.
        /// TMP_Dropdown creates a "Dropdown List" child when expanded.
        /// </summary>
        private static bool IsDropdownExpanded(TMPro.TMP_Dropdown dropdown)
        {
            if (dropdown == null) return false;

            // TMP_Dropdown creates a child named "Dropdown List" when expanded
            var dropdownList = dropdown.transform.Find("Dropdown List");
            return dropdownList != null && dropdownList.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Check if a legacy Unity Dropdown is expanded.
        /// </summary>
        private static bool IsLegacyDropdownExpanded(UnityEngine.UI.Dropdown dropdown)
        {
            if (dropdown == null) return false;

            // Legacy Dropdown also creates a "Dropdown List" child when expanded
            var dropdownList = dropdown.transform.Find("Dropdown List");
            return dropdownList != null && dropdownList.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Get the currently expanded dropdown GameObject, if any.
        /// Returns null if no dropdown is expanded.
        /// </summary>
        public static GameObject GetExpandedDropdown()
        {
            // Check cTMP_Dropdown first (most common in MTGA)
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;
                var type = mb.GetType();
                if (type.Name == "cTMP_Dropdown")
                {
                    if (GetIsExpandedProperty(mb))
                    {
                        return mb.gameObject;
                    }
                }
            }

            // Check standard TMP_Dropdown
            foreach (var dropdown in GameObject.FindObjectsOfType<TMPro.TMP_Dropdown>())
            {
                if (dropdown != null && IsDropdownExpanded(dropdown))
                {
                    return dropdown.gameObject;
                }
            }

            // Check legacy Unity Dropdown
            foreach (var dropdown in GameObject.FindObjectsOfType<UnityEngine.UI.Dropdown>())
            {
                if (dropdown != null && IsLegacyDropdownExpanded(dropdown))
                {
                    return dropdown.gameObject;
                }
            }

            return null;
        }

        /// <summary>
        /// Enter dropdown edit mode. Called when user explicitly activates a dropdown (Enter key).
        /// Note: The real dropdown state is determined by IsExpanded property, this is for explicit tracking.
        /// </summary>
        public static void EnterDropdownEditMode(GameObject dropdownObject)
        {
            _dropdownEditMode = true;
            _activeDropdownObject = dropdownObject;
            DebugConfig.LogIf(DebugConfig.LogFocusTracking, "FocusTracker", $"User explicitly opened dropdown: {dropdownObject?.name}");
        }

        /// <summary>
        /// Exit dropdown edit mode. Called when user explicitly closes dropdown (Escape/Backspace).
        /// Returns the name of the element that now has focus (so navigator can sync its index).
        /// Note: The real dropdown state is determined by IsExpanded property.
        /// </summary>
        public static string ExitDropdownEditMode()
        {
            string newFocusName = null;

            // Get the element that now has focus so navigator can sync to it
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
            {
                newFocusName = eventSystem.currentSelectedGameObject.name;
            }

            if (_dropdownEditMode)
            {
                DebugConfig.LogIf(DebugConfig.LogFocusTracking, "FocusTracker", $"User explicitly closed dropdown, new focus: {newFocusName ?? "null"}");
            }

            _dropdownEditMode = false;
            _activeDropdownObject = null;

            return newFocusName;
        }

        /// <summary>
        /// Check if a GameObject is a dropdown item (inside an open dropdown list).
        /// Dropdown items have names starting with "Item " followed by index.
        /// </summary>
        public static bool IsDropdownItem(GameObject obj)
        {
            if (obj == null) return false;
            // Dropdown items are named "Item 0: ...", "Item 1: ...", etc.
            return obj.name.StartsWith("Item ");
        }

        /// <summary>
        /// Check if a GameObject is a dropdown (TMP_Dropdown, Dropdown, or cTMP_Dropdown).
        /// </summary>
        public static bool IsDropdown(GameObject obj)
        {
            if (obj == null) return false;
            if (obj.GetComponent<TMPro.TMP_Dropdown>() != null) return true;
            if (obj.GetComponent<UnityEngine.UI.Dropdown>() != null) return true;
            // Check for game's custom cTMP_Dropdown
            foreach (var component in obj.GetComponents<Component>())
            {
                if (component != null && component.GetType().Name == "cTMP_Dropdown")
                    return true;
            }
            return false;
        }

        public UIFocusTracker(IAnnouncementService announcer)
        {
            _announcer = announcer;
        }
        #region Public Methods

        /// <summary>
        /// Call this from OnUpdate to check for focus changes.
        /// </summary>
        public void Update()
        {
            if (DebugConfig.DebugEnabled && DebugConfig.LogFocusTracking)
            {
                DebugLogKeyPresses();
            }

            CheckFocusChange();
        }

        #endregion

        #region Core Focus Tracking

        private void CheckFocusChange()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return;

            var selected = eventSystem.currentSelectedGameObject;

            if (selected == _lastSelected)
                return;

            HandleFocusChange(selected);
        }

        private void HandleFocusChange(GameObject selected)
        {
            Log($"Focus changed: {GetName(_lastSelected)} -> {GetName(selected)}");

            var previousSelected = _lastSelected;
            _lastSelected = selected;

            // Check the REAL dropdown state using IsExpanded property
            bool anyDropdownExpanded = IsAnyDropdownExpanded();
            bool focusOnDropdownItem = selected != null && IsDropdownItem(selected);

            // Log state changes for debugging
            if (anyDropdownExpanded && !_dropdownEditMode)
            {
                // Check if we should suppress dropdown mode entry (after closing auto-opened dropdown)
                if (_suppressDropdownModeEntry)
                {
                    _suppressDropdownModeEntry = false;
                    DebugConfig.LogIf(DebugConfig.LogFocusTracking, "FocusTracker", "Dropdown mode entry suppressed (IsExpanded=true but was auto-closed)");
                }
                else
                {
                    _dropdownEditMode = true;
                    _activeDropdownObject = selected;
                    DebugConfig.LogIf(DebugConfig.LogFocusTracking, "FocusTracker", $"Dropdown is now expanded (IsExpanded=true), focus: {selected?.name ?? "null"}");
                }
            }
            else if (!anyDropdownExpanded && _dropdownEditMode)
            {
                DebugConfig.LogIf(DebugConfig.LogFocusTracking, "FocusTracker", $"Dropdown closed (IsExpanded=false), focus: {selected?.name ?? "null"}");
                _dropdownEditMode = false;
                _activeDropdownObject = null;
                _suppressDropdownModeEntry = false; // Clear flag when dropdown actually closes
            }
            else if (!anyDropdownExpanded)
            {
                // Dropdown is not expanded, clear any pending suppress flag
                _suppressDropdownModeEntry = false;
            }

            OnFocusChanged?.Invoke(previousSelected, selected);

            if (selected == null)
                return;

            AnnounceElement(selected);
        }

        private void AnnounceElement(GameObject element)
        {
            // Check if navigator suppressed this announcement (it handles its own)
            if (_suppressNextFocusAnnouncement)
            {
                _suppressNextFocusAnnouncement = false;
                Log($"Skipping announcement (suppressed by navigator): {element.name}");
                return;
            }

            string text = UITextExtractor.GetText(element);
            Log($"Extracted text: '{text}' from {element.name}");

            if (text == _lastAnnouncedText)
            {
                Log("Skipping duplicate announcement");
                return;
            }

            _lastAnnouncedText = text;

            if (!string.IsNullOrWhiteSpace(text))
            {
                Log($"Announcing: {text}");
                _announcer.Announce(text, Models.AnnouncementPriority.Normal);
            }
            else
            {
                Log("Text was empty, not announcing");
            }
        }

        private static string GetName(GameObject obj)
        {
            return obj != null ? obj.name : "null";
        }

        #endregion

        #region Debug Logging

        private void Log(string message)
        {
            DebugConfig.LogIf(DebugConfig.LogFocusTracking, "FocusTracker", message);
        }

        private void DebugLogKeyPresses()
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                Log(shift ? "Shift+Tab pressed" : "Tab pressed");
                DebugLogCurrentSelection();
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Log("Enter pressed");
                DebugLogCurrentSelection();
            }
            else if (Input.GetKeyDown(KeyCode.Space))
            {
                Log("Space pressed");
            }
        }

        private void DebugLogCurrentSelection()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                Log("EventSystem is null");
                return;
            }

            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null)
            {
                Log($"Currently selected: {MenuDebugHelper.GetGameObjectPath(selected)}");
            }
            else
            {
                Log("No object selected in EventSystem");
                DebugScanForFocusedElements();
            }
        }

        /// <summary>
        /// Scans scene for focused elements when EventSystem has no selection.
        /// Useful for debugging MTGA's custom UI elements that don't use EventSystem.
        /// </summary>
        private void DebugScanForFocusedElements()
        {
            DebugScanInputFields();
            DebugScanSelectables();
            DebugScanEventTriggers();
        }

        private void DebugScanInputFields()
        {
            var tmpInputFields = GameObject.FindObjectsOfType<TMPro.TMP_InputField>();
            foreach (var field in tmpInputFields)
            {
                if (field.isFocused)
                {
                    Log($"Found focused TMP_InputField: {MenuDebugHelper.GetGameObjectPath(field.gameObject)}");
                    return;
                }
            }

            var legacyInputFields = GameObject.FindObjectsOfType<UnityEngine.UI.InputField>();
            foreach (var field in legacyInputFields)
            {
                if (field.isFocused)
                {
                    Log($"Found focused InputField: {MenuDebugHelper.GetGameObjectPath(field.gameObject)}");
                    return;
                }
            }
        }

        private void DebugScanSelectables()
        {
            var selectables = GameObject.FindObjectsOfType<UnityEngine.UI.Selectable>();
            Log($"Found {selectables.Length} Selectable components in scene");

            int activeCount = 0;
            foreach (var sel in selectables)
            {
                if (!sel.isActiveAndEnabled || !sel.interactable)
                    continue;

                activeCount++;
                if (activeCount <= MAX_SELECTABLE_LOG_COUNT)
                {
                    string typeName = sel.GetType().Name;
                    string text = UITextExtractor.GetText(sel.gameObject);
                    Log($"  Selectable: {typeName} - {sel.gameObject.name} - Text: {text}");
                }
            }

            Log($"Total active/interactable: {activeCount}");
        }

        private void DebugScanEventTriggers()
        {
            var eventTriggers = GameObject.FindObjectsOfType<EventTrigger>();
            if (eventTriggers.Length == 0)
                return;

            Log($"Found {eventTriggers.Length} EventTrigger components:");
            foreach (var trigger in eventTriggers)
            {
                if (trigger.isActiveAndEnabled)
                {
                    string text = UITextExtractor.GetText(trigger.gameObject);
                    Log($"  EventTrigger: {trigger.gameObject.name} - Text: {text}");
                }
            }
        }

        #endregion
    }
}
