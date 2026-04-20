using UnityEngine;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Models;
using System;
using System.Reflection;

namespace AccessibleArena.Core.Services
{
    public partial class BaseNavigator
    {
        // Shared input field editing helper (announcements, field info, reactivation, character detection)
        private InputFieldEditHelper _inputFieldHelper;

        // Counter for pending search rescan (decrements each frame, rescans when reaches 0)
        private int _pendingSearchRescanFrames = 0;

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
                _inputFieldHelper.PreserveTextOnEscape();
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
    }
}
