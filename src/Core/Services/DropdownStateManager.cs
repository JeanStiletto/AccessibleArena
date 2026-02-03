using UnityEngine;
using UnityEngine.EventSystems;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Unified dropdown state management. Single source of truth for dropdown mode tracking.
    /// Consolidates the previously separate flags from BaseNavigator and UIFocusTracker.
    ///
    /// The real dropdown state is determined by IsExpanded property on the dropdown component.
    /// This manager handles:
    /// - Tracking if we're in dropdown mode (for input handling)
    /// - Suppressing re-entry after closing auto-opened dropdowns
    /// - Detecting dropdown exit transitions (for index syncing)
    /// </summary>
    public static class DropdownStateManager
    {
        #region State

        /// <summary>
        /// True if we were in dropdown mode last frame. Used to detect exit transitions
        /// so the navigator can sync its index after dropdown closes (handles auto-advance).
        /// </summary>
        private static bool _wasInDropdownMode;

        /// <summary>
        /// Suppresses dropdown mode entry AND _wasInDropdownMode tracking.
        /// Set after closing an auto-opened dropdown to prevent re-entry before
        /// the dropdown's IsExpanded property updates to false.
        /// </summary>
        private static bool _suppressReentry;

        /// <summary>
        /// Reference to the currently active dropdown object.
        /// </summary>
        private static GameObject _activeDropdownObject;

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
                // Check actual dropdown state
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
        /// The currently active dropdown object, if any.
        /// </summary>
        public static GameObject ActiveDropdown => _activeDropdownObject;

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
        /// Called when user explicitly activates a dropdown (Enter key).
        /// Note: The real state is still determined by IsExpanded property.
        /// </summary>
        public static void OnDropdownOpened(GameObject dropdown)
        {
            _activeDropdownObject = dropdown;
            _wasInDropdownMode = true;
            DebugConfig.LogIf(DebugConfig.LogFocusTracking, "DropdownState",
                $"User opened dropdown: {dropdown?.name}");
        }

        /// <summary>
        /// Called when user explicitly closes a dropdown (Escape/Backspace).
        /// Returns the name of the element that now has focus (for navigator sync).
        /// </summary>
        public static string OnDropdownClosed()
        {
            string newFocusName = null;

            // Get the element that now has focus
            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
            {
                newFocusName = eventSystem.currentSelectedGameObject.name;
            }

            DebugConfig.LogIf(DebugConfig.LogFocusTracking, "DropdownState",
                $"User closed dropdown, new focus: {newFocusName ?? "null"}");

            // Don't clear _wasInDropdownMode here - UpdateAndCheckExitTransition handles the transition
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
            DebugConfig.LogIf(DebugConfig.LogFocusTracking, "DropdownState",
                "Suppressing dropdown re-entry (auto-opened dropdown closed)");
        }

        /// <summary>
        /// Check if dropdown mode entry should be suppressed.
        /// Used by UIFocusTracker to decide whether to enter dropdown mode.
        /// </summary>
        public static bool ShouldSuppressEntry()
        {
            if (_suppressReentry)
            {
                // Clear the flag once checked (it's consumed by this call)
                // Note: _suppressReentry is also cleared when dropdown actually closes,
                // so this is just an additional consumption point for UIFocusTracker
                return true;
            }
            return false;
        }

        /// <summary>
        /// Reset all state. Called when navigator deactivates or during cleanup.
        /// </summary>
        public static void Reset()
        {
            _wasInDropdownMode = false;
            _suppressReentry = false;
            _activeDropdownObject = null;
            DebugConfig.LogIf(DebugConfig.LogFocusTracking, "DropdownState", "State reset");
        }

        #endregion
    }
}
