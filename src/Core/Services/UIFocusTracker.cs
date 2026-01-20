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
        private const string LOG_PREFIX = "[FocusTracker]";
        private const int MAX_SELECTABLE_LOG_COUNT = 10;
        private const int MAX_HIERARCHY_DEPTH = 3;

        private readonly IAnnouncementService _announcer;
        private GameObject _lastSelected;
        private string _lastAnnouncedText;
        private readonly bool _debugMode = true;

        // Input field edit mode - only true when user explicitly activated (Enter) the field
        private static bool _inputFieldEditMode;
        private static GameObject _activeInputFieldObject;

        // Dropdown edit mode - true when a dropdown is open and user is navigating its items
        private static bool _dropdownEditMode;
        private static GameObject _activeDropdownObject;

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
        /// Returns true if the EventSystem selection is an input field.
        /// This is true when user has navigated to an input field (Tab or arrow keys),
        /// regardless of whether the caret is visible (isFocused).
        /// Use this to avoid intercepting keys like Backspace/Escape that should go to the input field.
        /// </summary>
        public static bool IsAnyInputFieldFocused()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
                return false;

            var selected = eventSystem.currentSelectedGameObject;

            // Check if selection IS an input field (not just if it's "focused" for editing)
            // MTGA auto-activates input fields when selected, so any input field selection
            // should be treated as "in input mode" for key blocking purposes
            var tmpInput = selected.GetComponent<TMPro.TMP_InputField>();
            if (tmpInput != null && tmpInput.interactable)
                return true;

            var legacyInput = selected.GetComponent<UnityEngine.UI.InputField>();
            if (legacyInput != null && legacyInput.interactable)
                return true;

            return false;
        }

        /// <summary>
        /// Enter edit mode for an input field. Called when user presses Enter on an input field.
        /// </summary>
        public static void EnterInputFieldEditMode(GameObject inputFieldObject)
        {
            _inputFieldEditMode = true;
            _activeInputFieldObject = inputFieldObject;
            MelonLogger.Msg($"[FocusTracker] Entered input field edit mode: {inputFieldObject?.name}");
        }

        /// <summary>
        /// Exit edit mode. Called when user presses Escape/Tab or focus leaves the field.
        /// </summary>
        public static void ExitInputFieldEditMode()
        {
            if (_inputFieldEditMode)
            {
                MelonLogger.Msg($"[FocusTracker] Exited input field edit mode");
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
                MelonLogger.Msg($"[FocusTracker] Deactivated TMP_InputField: {selected.name} (wasFocused={tmpInput.isFocused})");
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
                MelonLogger.Msg($"[FocusTracker] Deactivated InputField: {selected.name} (wasFocused={legacyInput.isFocused})");
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
        /// Returns true if user is navigating inside an open dropdown.
        /// When true, arrow keys control dropdown selection. When false, arrows navigate between elements.
        /// </summary>
        public static bool IsEditingDropdown()
        {
            // Only return true if we're in dropdown mode AND focus is still on a dropdown item
            if (!_dropdownEditMode)
                return false;

            // Verify focus is still on a dropdown item
            var eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
            {
                ExitDropdownEditMode();
                return false;
            }

            // If focus moved away from dropdown items, exit mode
            if (!IsDropdownItem(eventSystem.currentSelectedGameObject))
            {
                ExitDropdownEditMode();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Enter dropdown edit mode. Called when user presses Enter on a dropdown to open it.
        /// </summary>
        public static void EnterDropdownEditMode(GameObject dropdownObject)
        {
            _dropdownEditMode = true;
            _activeDropdownObject = dropdownObject;
            MelonLogger.Msg($"[FocusTracker] Entered dropdown edit mode: {dropdownObject?.name}");
        }

        /// <summary>
        /// Exit dropdown edit mode. Called when dropdown closes (focus leaves dropdown items).
        /// Returns the name of the element that now has focus (so navigator can sync its index).
        /// </summary>
        public static string ExitDropdownEditMode()
        {
            string newFocusName = null;
            if (_dropdownEditMode)
            {
                // Get the element that now has focus so navigator can sync to it
                var eventSystem = UnityEngine.EventSystems.EventSystem.current;
                if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
                {
                    newFocusName = eventSystem.currentSelectedGameObject.name;
                }

                MelonLogger.Msg($"[FocusTracker] Exited dropdown edit mode, new focus: {newFocusName ?? "null"}");
                _dropdownEditMode = false;
                _activeDropdownObject = null;
            }
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
            if (_debugMode)
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

            // Track dropdown edit mode based on where focus is
            // If focus is on a dropdown item, we're in dropdown mode (game is handling input)
            // If focus is elsewhere, we're not in dropdown mode
            bool focusOnDropdownItem = selected != null && IsDropdownItem(selected);

            if (focusOnDropdownItem && !_dropdownEditMode)
            {
                // Entering dropdown mode (game opened a dropdown)
                _dropdownEditMode = true;
                _activeDropdownObject = selected; // Track the item for reference
                MelonLogger.Msg($"[FocusTracker] Entered dropdown mode (focus on item: {selected.name})");
            }
            else if (!focusOnDropdownItem && _dropdownEditMode)
            {
                // Exiting dropdown mode (focus left dropdown items)
                ExitDropdownEditMode();
            }

            OnFocusChanged?.Invoke(previousSelected, selected);

            if (selected == null)
                return;

            AnnounceElement(selected);
        }

        private void AnnounceElement(GameObject element)
        {
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
            if (_debugMode)
            {
                MelonLogger.Msg($"{LOG_PREFIX} {message}");
            }
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
                Log($"Currently selected: {GetGameObjectPath(selected)}");
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
                    Log($"Found focused TMP_InputField: {GetGameObjectPath(field.gameObject)}");
                    return;
                }
            }

            var legacyInputFields = GameObject.FindObjectsOfType<UnityEngine.UI.InputField>();
            foreach (var field in legacyInputFields)
            {
                if (field.isFocused)
                {
                    Log($"Found focused InputField: {GetGameObjectPath(field.gameObject)}");
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

        private string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            int depth = 0;

            while (parent != null && depth < MAX_HIERARCHY_DEPTH)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
                depth++;
            }

            return path;
        }

        #endregion
    }
}
