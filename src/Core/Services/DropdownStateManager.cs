using UnityEngine;
using UnityEngine.EventSystems;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Unified dropdown state management. Single source of truth for dropdown mode tracking.
    ///
    /// The real dropdown state is determined by IsExpanded property on the dropdown component.
    /// This manager handles:
    /// - Tracking if we're in dropdown mode (for input handling)
    /// - Suppressing re-entry after closing auto-opened dropdowns
    /// - Detecting dropdown exit transitions (for index syncing)
    /// - Blocking Enter/Submit from the game while in dropdown mode
    ///
    /// Item selection is handled by BaseNavigator.SelectDropdownItemAndClose() which
    /// sets the value via reflection (bypassing onValueChanged) to prevent chain auto-advance.
    /// </summary>
    public static class DropdownStateManager
    {
        #region State

        /// <summary>
        /// True if we were in dropdown mode last frame. Used to detect exit transitions
        /// so the navigator can sync its index after dropdown closes.
        /// </summary>
        private static bool _wasInDropdownMode;

        /// <summary>
        /// Suppresses dropdown mode entry.
        /// Set after closing an auto-opened dropdown to prevent re-entry before
        /// the dropdown's IsExpanded property updates to false.
        /// </summary>
        private static bool _suppressReentry;

        /// <summary>
        /// Reference to the currently active dropdown object.
        /// </summary>
        private static GameObject _activeDropdownObject;

        /// <summary>
        /// Frame on which a dropdown item was selected via Enter. Submit events are blocked
        /// for a few frames after this to prevent MTGA from auto-clicking Continue.
        /// </summary>
        private static int _blockSubmitAfterFrame = -1;

        /// <summary>
        /// When true, Enter key is blocked from the game's KeyboardManager and
        /// SendSubmitEventToSelectedObject is blocked from Unity's EventSystem.
        /// Set when entering dropdown mode, cleared when dropdown mode exits.
        /// This persistent flag survives the dropdown closing during EventSystem.Process()
        /// (which runs before our Update).
        /// </summary>
        private static bool _blockEnterFromGame;

        #endregion

        #region Public Properties

        /// <summary>
        /// Returns true if any dropdown is currently expanded (open).
        /// Queries the actual IsExpanded property - this is the real source of truth.
        /// </summary>
        public static bool IsDropdownExpanded => UIFocusTracker.IsAnyDropdownExpanded();

        /// <summary>
        /// Returns true if we should be in dropdown mode.
        /// Takes into account suppression after closing auto-opened dropdowns.
        /// </summary>
        public static bool IsInDropdownMode
        {
            get
            {
                bool anyExpanded = IsDropdownExpanded;

                // If suppressing and dropdown is still showing as expanded,
                // return false (we're in the brief delay after Hide() was called)
                if (_suppressReentry && anyExpanded)
                {
                    return false;
                }

                return anyExpanded;
            }
        }

        /// <summary>
        /// Returns true if Enter/Submit should be blocked from the game.
        /// Stays true from when a dropdown opens until our Update processes the exit.
        /// </summary>
        public static bool ShouldBlockEnterFromGame => _blockEnterFromGame;

        /// <summary>
        /// The currently active dropdown object, if any.
        /// </summary>
        public static GameObject ActiveDropdown => _activeDropdownObject;

        /// <summary>
        /// True when reentry is being suppressed (a dropdown was just closed and may still
        /// report IsExpanded for a frame). Used by UpdateEventSystemSelection to avoid
        /// entering dropdown mode for a new dropdown when the old one is still closing.
        /// </summary>
        public static bool IsSuppressed => _suppressReentry;

        #endregion

        #region Public API

        /// <summary>
        /// Called each frame by BaseNavigator to update state and detect transitions.
        /// Returns true if we just exited dropdown mode (for index sync trigger).
        /// </summary>
        public static bool UpdateAndCheckExitTransition()
        {
            bool currentlyInDropdownMode = IsInDropdownMode;
            bool justExited = false;

            // Detect exit transition (was in dropdown mode, now not)
            if (_wasInDropdownMode && !currentlyInDropdownMode)
            {
                justExited = true;
                _blockEnterFromGame = false;
                DebugConfig.LogIf(DebugConfig.LogFocusTracking, "DropdownState",
                    "Dropdown mode exit transition detected");
            }

            // Clear suppression once dropdown is actually closed
            if (_suppressReentry && !IsDropdownExpanded)
            {
                _suppressReentry = false;
                DebugConfig.LogIf(DebugConfig.LogFocusTracking, "DropdownState",
                    "Suppression cleared - dropdown actually closed");
            }

            // Update tracking for next frame (only if not suppressing)
            if (!_suppressReentry)
            {
                _wasInDropdownMode = currentlyInDropdownMode;
            }

            // Update active dropdown reference
            if (currentlyInDropdownMode)
            {
                _activeDropdownObject = UIFocusTracker.GetExpandedDropdown();
            }
            else if (!IsDropdownExpanded)
            {
                _activeDropdownObject = null;
            }

            return justExited;
        }

        /// <summary>
        /// Returns true if Submit events should be blocked.
        /// After a dropdown item is selected, we block Submit for a few frames to prevent
        /// MTGA from auto-clicking Continue (or other buttons that receive focus after dropdown closes).
        /// </summary>
        public static bool ShouldBlockSubmit()
        {
            if (_blockSubmitAfterFrame < 0) return false;
            int frame = UnityEngine.Time.frameCount;
            return frame > _blockSubmitAfterFrame && frame <= _blockSubmitAfterFrame + 3;
        }

        /// <summary>
        /// Called when the user presses Enter to select a dropdown item.
        /// Starts the Submit-blocking window to prevent auto-activation of the next focused element.
        /// </summary>
        public static void OnDropdownItemSelected()
        {
            _blockSubmitAfterFrame = UnityEngine.Time.frameCount;
            DebugConfig.LogIf(DebugConfig.LogFocusTracking, "DropdownState",
                $"Dropdown item selected on frame {_blockSubmitAfterFrame}, blocking Submit for next 3 frames");
        }

        /// <summary>
        /// Called when user explicitly activates a dropdown (Enter key).
        /// Note: The real state is still determined by IsExpanded property.
        /// </summary>
        public static void OnDropdownOpened(GameObject dropdown)
        {
            _activeDropdownObject = dropdown;
            _wasInDropdownMode = true;
            _blockEnterFromGame = true;
            _suppressReentry = false; // Clear suppression from previous dropdown close
            DebugConfig.LogIf(DebugConfig.LogFocusTracking, "DropdownState",
                $"User opened dropdown: {dropdown?.name}");
        }

        /// <summary>
        /// Called when user explicitly closes a dropdown (Escape/Backspace).
        /// Returns the name of the element that now has focus (for navigator sync).
        /// </summary>
        public static string OnDropdownClosed()
        {
            _blockEnterFromGame = false;
            // Block Submit for a few frames to prevent MTGA from auto-clicking
            // the element that receives focus after dropdown closes
            _blockSubmitAfterFrame = UnityEngine.Time.frameCount;
            string newFocusName = null;

            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
            {
                newFocusName = eventSystem.currentSelectedGameObject.name;
            }

            DebugConfig.LogIf(DebugConfig.LogFocusTracking, "DropdownState",
                $"User closed dropdown, new focus: {newFocusName ?? "null"}");

            _activeDropdownObject = null;

            return newFocusName;
        }

        /// <summary>
        /// Called after closing an auto-opened dropdown to prevent re-entry.
        /// The dropdown's IsExpanded property may not update immediately after Hide(),
        /// so this suppresses dropdown mode until it actually closes.
        /// </summary>
        public static void SuppressReentry()
        {
            _suppressReentry = true;
            _wasInDropdownMode = false;
            _activeDropdownObject = null;
            _blockEnterFromGame = false;
            DebugConfig.LogIf(DebugConfig.LogFocusTracking, "DropdownState",
                "Suppressing dropdown re-entry (auto-opened dropdown closed)");
        }

        /// <summary>
        /// Reset all state. Called when navigator deactivates or during cleanup.
        /// </summary>
        public static void Reset()
        {
            _wasInDropdownMode = false;
            _suppressReentry = false;
            _activeDropdownObject = null;
            _blockSubmitAfterFrame = -1;
            _blockEnterFromGame = false;
            DebugConfig.LogIf(DebugConfig.LogFocusTracking, "DropdownState", "State reset");
        }

        #endregion
    }
}
