using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Shared input field editing logic used by BaseNavigator and PopupHandler.
    /// Handles edit mode state, key navigation, character announcements, and field reactivation.
    /// Supports both TMP_InputField and legacy InputField.
    /// </summary>
    public class InputFieldEditHelper
    {
        private readonly IAnnouncementService _announcer;

        // Edit state
        private GameObject _editingField;
        private string _prevText = "";
        private int _prevCaretPos;

        public bool IsEditing => _editingField != null;
        public GameObject EditingField => _editingField;

        #region Field Info

        public struct FieldInfo
        {
            public bool IsValid;
            public string Text;
            public int CaretPosition;
            public bool IsPassword;
            public GameObject GameObject;
        }

        #endregion

        public InputFieldEditHelper(IAnnouncementService announcer)
        {
            _announcer = announcer;
        }

        #region Edit Mode Management

        /// <summary>
        /// Enter edit mode: activate field, announce, and start tracking state.
        /// </summary>
        public void EnterEditMode(GameObject field)
        {
            _editingField = field;
            UIFocusTracker.EnterInputFieldEditMode(field);
            UIActivator.Activate(field);
            _announcer?.Announce(Strings.EditingTextField, AnnouncementPriority.Normal);
            TrackState();
        }

        /// <summary>
        /// Exit edit mode: deactivate field and clear state.
        /// </summary>
        public void ExitEditMode()
        {
            _editingField = null;
            UIFocusTracker.ExitInputFieldEditMode();
            UIFocusTracker.DeactivateFocusedInputField();
        }

        /// <summary>
        /// Set editing field without activating or announcing.
        /// Used for auto-entry when Tab-navigating to an input field in BaseNavigator.
        /// </summary>
        public void SetEditingFieldSilently(GameObject field)
        {
            _editingField = field;
            UIFocusTracker.EnterInputFieldEditMode(field);
        }

        /// <summary>
        /// Clear editing field without calling UIFocusTracker deactivation.
        /// Used by BaseNavigator when clearing stale edit state detected by UIFocusTracker checks.
        /// </summary>
        public void ClearEditingFieldSilently()
        {
            _editingField = null;
        }

        /// <summary>
        /// Full reset of all state. Call when popup closes or navigator deactivates.
        /// </summary>
        public void Clear()
        {
            if (IsEditing)
                ExitEditMode();
            _prevText = "";
            _prevCaretPos = 0;
        }

        #endregion

        #region State Tracking

        /// <summary>
        /// Track current editing field state for next frame's Backspace detection.
        /// </summary>
        public void TrackState()
        {
            var info = GetEditingFieldInfo();
            if (info.IsValid)
            {
                _prevText = info.Text ?? "";
                _prevCaretPos = info.CaretPosition;
            }
            else
            {
                _prevText = "";
                _prevCaretPos = 0;
            }
        }

        /// <summary>
        /// Track state from externally provided field info.
        /// Used by BaseNavigator which does scene-wide scanning for mouse-clicked fields.
        /// </summary>
        public void TrackState(FieldInfo info)
        {
            if (info.IsValid)
            {
                _prevText = info.Text ?? "";
                _prevCaretPos = info.CaretPosition;
            }
            else
            {
                _prevText = "";
                _prevCaretPos = 0;
            }
        }

        #endregion

        #region Key Handling

        /// <summary>
        /// Handle keys while editing an input field. Returns true if key was consumed.
        /// Handles Escape, Tab, Backspace, arrow keys, and passes through typing keys.
        /// </summary>
        /// <param name="onTabNavigate">Called with direction (-1 or 1) when Tab exits edit mode</param>
        public bool HandleEditing(Action<int> onTabNavigate)
        {
            // Escape: exit edit mode
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ExitEditMode();
                _announcer?.Announce(Strings.ExitedEditMode, AnnouncementPriority.Normal);
                return true;
            }

            // Tab: consume to prevent game interference, exit edit mode, navigate
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                int direction = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? -1 : 1;
                ExitEditMode();
                onTabNavigate?.Invoke(direction);
                return true;
            }

            // Backspace: announce deleted char, pass through for actual deletion
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                AnnounceDeletedCharacter();
                return false;
            }

            // Up/Down: announce field content, reactivate field
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                AnnounceFieldContent();
                ReactivateField();
                return true;
            }

            // Left/Right: announce character at cursor
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                AnnounceCharacterAtCursor();
                return true;
            }

            // All other keys pass through for typing
            return false;
        }

        #endregion

        #region Field Info Retrieval

        /// <summary>
        /// Get info from the currently editing field. Supports TMP and legacy InputField.
        /// </summary>
        public FieldInfo GetEditingFieldInfo()
        {
            return GetFieldInfoFrom(_editingField, allowUnfocused: true);
        }

        /// <summary>
        /// Get info from a specific field, with optional fallback.
        /// Used by BaseNavigator which may fall back to the current navigated element.
        /// </summary>
        public FieldInfo GetEditingFieldInfo(GameObject fallback)
        {
            var result = GetFieldInfoFrom(_editingField, allowUnfocused: true);
            if (result.IsValid) return result;
            return GetFieldInfoFrom(fallback, allowUnfocused: true);
        }

        /// <summary>
        /// Get info from a specific GameObject. Supports TMP_InputField and legacy InputField.
        /// </summary>
        /// <param name="fieldObj">The field to query</param>
        /// <param name="allowUnfocused">If true, accept the field even if not isFocused (for edit mode after Up/Down deactivation)</param>
        public FieldInfo GetFieldInfoFrom(GameObject fieldObj, bool allowUnfocused = false)
        {
            var result = new FieldInfo { IsValid = false };
            if (fieldObj == null) return result;

            bool inEditMode = allowUnfocused && _editingField != null && UIFocusTracker.IsEditingInputField();

            // Check TMP_InputField
            var tmpInput = fieldObj.GetComponent<TMP_InputField>();
            if (tmpInput != null && (tmpInput.isFocused || inEditMode))
            {
                result.IsValid = true;
                result.Text = tmpInput.text;
                result.CaretPosition = tmpInput.isFocused ? tmpInput.stringPosition : (tmpInput.text?.Length ?? 0);
                result.IsPassword = tmpInput.inputType == TMP_InputField.InputType.Password;
                result.GameObject = fieldObj;
                return result;
            }

            // Check legacy InputField
            var legacyInput = fieldObj.GetComponent<InputField>();
            if (legacyInput != null && (legacyInput.isFocused || inEditMode))
            {
                result.IsValid = true;
                result.Text = legacyInput.text;
                result.CaretPosition = legacyInput.isFocused ? legacyInput.caretPosition : (legacyInput.text?.Length ?? 0);
                result.IsPassword = legacyInput.inputType == InputField.InputType.Password;
                result.GameObject = fieldObj;
                return result;
            }

            return result;
        }

        /// <summary>
        /// Scan scene for any focused input field (handles mouse-clicked fields).
        /// Tries the editing field first, then does a scene-wide scan.
        /// </summary>
        public FieldInfo ScanForAnyFocusedField()
        {
            return ScanForAnyFocusedField(null);
        }

        /// <summary>
        /// Scan scene for any focused input field, with a fallback object to check before scene scan.
        /// </summary>
        public FieldInfo ScanForAnyFocusedField(GameObject fallback)
        {
            // Try editing field first
            var result = GetEditingFieldInfo();
            if (result.IsValid) return result;

            // Try fallback
            if (fallback != null)
            {
                result = GetFieldInfoFrom(fallback, allowUnfocused: false);
                if (result.IsValid) return result;
            }

            // Scene-wide scan for TMP_InputField
            var tmpInputFields = GameObject.FindObjectsOfType<TMP_InputField>();
            foreach (var field in tmpInputFields)
            {
                if (field.isFocused)
                {
                    return new FieldInfo
                    {
                        IsValid = true,
                        Text = field.text,
                        CaretPosition = field.stringPosition,
                        IsPassword = field.inputType == TMP_InputField.InputType.Password,
                        GameObject = field.gameObject
                    };
                }
            }

            // Scene-wide scan for legacy InputField
            var legacyInputFields = GameObject.FindObjectsOfType<InputField>();
            foreach (var field in legacyInputFields)
            {
                if (field.isFocused)
                {
                    return new FieldInfo
                    {
                        IsValid = true,
                        Text = field.text,
                        CaretPosition = field.caretPosition,
                        IsPassword = field.inputType == InputField.InputType.Password,
                        GameObject = field.gameObject
                    };
                }
            }

            return new FieldInfo { IsValid = false };
        }

        #endregion

        #region Announcements

        /// <summary>
        /// Announce the character being deleted by Backspace.
        /// Uses tracked previous state since Unity has already processed the deletion.
        /// </summary>
        public void AnnounceDeletedCharacter()
        {
            var info = GetEditingFieldInfo();
            if (!info.IsValid) return;

            AnnounceDeletedCharacter(info);
        }

        /// <summary>
        /// Announce deleted character using provided field info (for callers with custom field retrieval).
        /// </summary>
        public void AnnounceDeletedCharacter(FieldInfo info)
        {
            if (!info.IsValid) return;

            string currentText = info.Text ?? "";
            string prevText = _prevText ?? "";

            if (prevText.Length <= currentText.Length)
                return;

            if (info.IsPassword)
            {
                _announcer?.AnnounceInterrupt(Strings.InputFieldStar);
                return;
            }

            char deletedChar = FindDeletedCharacter(prevText, currentText, _prevCaretPos);
            _announcer?.AnnounceInterrupt(Strings.GetCharacterName(deletedChar));
        }

        /// <summary>
        /// Announce the character at the current cursor position.
        /// </summary>
        public void AnnounceCharacterAtCursor()
        {
            var info = GetEditingFieldInfo();
            if (!info.IsValid) return;

            AnnounceCharacterAtCursor(info);
        }

        /// <summary>
        /// Announce character at cursor using provided field info.
        /// </summary>
        public void AnnounceCharacterAtCursor(FieldInfo info)
        {
            if (!info.IsValid) return;

            bool isLeft = Input.GetKeyDown(KeyCode.LeftArrow);
            bool isRight = Input.GetKeyDown(KeyCode.RightArrow);
            string text = info.Text;
            int caretPos = info.CaretPosition;

            if (string.IsNullOrEmpty(text))
            {
                _announcer?.AnnounceInterrupt(Strings.InputFieldEmpty);
                return;
            }

            if (info.IsPassword)
            {
                if (caretPos == 0 && isLeft)
                    _announcer?.AnnounceInterrupt(Strings.InputFieldStart);
                else if (caretPos >= text.Length && isRight)
                    _announcer?.AnnounceInterrupt(Strings.InputFieldEnd);
                else if (caretPos >= 0 && caretPos < text.Length)
                    _announcer?.AnnounceInterrupt(Strings.InputFieldStar);
                else
                    _announcer?.AnnounceInterrupt(caretPos == 0 ? Strings.InputFieldStart : Strings.InputFieldEnd);
                return;
            }

            if (caretPos == 0 && isLeft)
                _announcer?.AnnounceInterrupt(Strings.InputFieldStart);
            else if (caretPos >= text.Length && isRight)
                _announcer?.AnnounceInterrupt(Strings.InputFieldEnd);
            else if (caretPos >= 0 && caretPos < text.Length)
                _announcer?.AnnounceInterrupt(Strings.GetCharacterName(text[caretPos]));
            else
                _announcer?.AnnounceInterrupt(Strings.InputFieldEnd);
        }

        /// <summary>
        /// Announce the content of the currently editing input field.
        /// </summary>
        public void AnnounceFieldContent()
        {
            var info = GetEditingFieldInfo();
            if (!info.IsValid) return;

            AnnounceFieldContent(info);
        }

        /// <summary>
        /// Announce field content using provided field info.
        /// </summary>
        public void AnnounceFieldContent(FieldInfo info)
        {
            if (!info.IsValid) return;

            string label = UITextExtractor.GetInputFieldLabel(info.GameObject);
            string content = info.Text;

            if (info.IsPassword)
            {
                string announcement = string.IsNullOrEmpty(content)
                    ? Strings.InputFieldEmptyWithLabel(label)
                    : Strings.InputFieldPasswordWithCount(label, content.Length);
                _announcer?.AnnounceInterrupt(announcement);
            }
            else
            {
                string announcement = string.IsNullOrEmpty(content)
                    ? Strings.InputFieldEmptyWithLabel(label)
                    : Strings.InputFieldContent(label, content);
                _announcer?.AnnounceInterrupt(announcement);
            }
        }

        #endregion

        #region Field Reactivation

        /// <summary>
        /// Re-activate the input field after Unity deactivated it (e.g., Up/Down in single-line mode).
        /// Restores EventSystem selection and re-activates the field so typing can continue.
        /// Supports both TMP_InputField and legacy InputField.
        /// </summary>
        public void ReactivateField()
        {
            if (_editingField == null || !_editingField.activeInHierarchy) return;

            var eventSystem = EventSystem.current;

            var tmpInput = _editingField.GetComponent<TMP_InputField>();
            if (tmpInput != null && !tmpInput.isFocused)
            {
                if (eventSystem != null)
                    eventSystem.SetSelectedGameObject(_editingField);
                tmpInput.ActivateInputField();
                return;
            }

            var legacyInput = _editingField.GetComponent<InputField>();
            if (legacyInput != null && !legacyInput.isFocused)
            {
                if (eventSystem != null)
                    eventSystem.SetSelectedGameObject(_editingField);
                legacyInput.ActivateInputField();
            }
        }

        #endregion

        #region Character Detection

        /// <summary>
        /// Find the character that was deleted by comparing previous and current text.
        /// </summary>
        public static char FindDeletedCharacter(string prevText, string currentText, int prevCaretPos)
        {
            // Backspace deletes the character before the caret
            int deletedIndex = prevCaretPos - 1;

            if (deletedIndex >= 0 && deletedIndex < prevText.Length)
                return prevText[deletedIndex];

            // Fallback: find first difference between strings
            for (int i = 0; i < currentText.Length; i++)
            {
                if (i >= prevText.Length || prevText[i] != currentText[i])
                {
                    if (i < prevText.Length)
                        return prevText[i];
                    break;
                }
            }

            // If current is a prefix of prev, the deleted char is the one after current ends
            if (currentText.Length < prevText.Length)
                return prevText[currentText.Length];

            return '?';
        }

        #endregion
    }
}
