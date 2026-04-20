using System.Reflection;
using MelonLoader;
using AccessibleArena.Core.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;

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
    /// - Suppressing onValueChanged while dropdown is open (prevents form auto-advance)
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
        /// Time until which Submit events are blocked after a dropdown selection.
        /// Uses realtime (not frame count) to be frame-rate independent.
        /// 500ms window covers the game's delayed form-advance (~376ms observed).
        /// </summary>
        private static float _blockSubmitUntilTime = -1f;

        /// <summary>
        /// When true, Enter key is blocked from the game's KeyboardManager and
        /// SendSubmitEventToSelectedObject is blocked from Unity's EventSystem.
        /// Set when entering dropdown mode, cleared when dropdown mode exits.
        /// This persistent flag survives the dropdown closing during EventSystem.Process()
        /// (which runs before our Update).
        /// </summary>
        private static bool _blockEnterFromGame;

        /// <summary>
        /// Saved m_OnValueChanged event from the dropdown component.
        /// Replaced with an empty event while dropdown is open to prevent the game's
        /// form validation from detecting value changes and auto-advancing pages.
        /// </summary>
        private static object _savedOnValueChanged;

        /// <summary>
        /// The dropdown component whose onValueChanged was suppressed.
        /// </summary>
        private static Component _suppressedDropdownComponent;

        /// <summary>
        /// Cached FieldInfo for cTMP_Dropdown.m_OnValueChanged.
        /// </summary>
        private static FieldInfo _cachedOnValueChangedField;

        /// <summary>
        /// When >= 0, the user confirmed a selection (Enter) and onValueChanged should be
        /// fired with this value after the callback is restored on close.
        /// -1 means no pending notification (cancel via Escape/Backspace).
        /// </summary>
        private static int _pendingNotifyValue = -1;

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
                // Capture pending value before restore
                int pendingValue = _pendingNotifyValue;
                var dropdownComponent = _suppressedDropdownComponent;
                _pendingNotifyValue = -1;
                // Restore onValueChanged in case dropdown was closed by the game
                // (not by our explicit OnDropdownClosed call)
                RestoreOnValueChanged();
                // Fire if user had confirmed a selection
                if (pendingValue >= 0 && dropdownComponent != null)
                    FireOnValueChanged(dropdownComponent, pendingValue);
                Log.Focus("DropdownState",
                    "Dropdown mode exit transition detected");
            }

            // Clear suppression once dropdown is actually closed
            if (_suppressReentry && !IsDropdownExpanded)
            {
                _suppressReentry = false;
                Log.Focus("DropdownState",
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
        /// After a dropdown item is selected, we block Submit for 500ms to prevent
        /// MTGA from auto-clicking Continue (or other buttons that receive focus after dropdown closes).
        /// Time-based rather than frame-based for consistent behavior across frame rates.
        /// </summary>
        public static bool ShouldBlockSubmit()
        {
            if (_blockSubmitUntilTime < 0f) return false;
            if (UnityEngine.Time.realtimeSinceStartup < _blockSubmitUntilTime) return true;
            _blockSubmitUntilTime = -1f;
            return false;
        }

        /// <summary>
        /// Called when the user presses Enter to select a dropdown item.
        /// Stores the selected value so onValueChanged can be fired after the callback is restored.
        /// </summary>
        public static void OnDropdownItemSelected(int selectedValue)
        {
            _blockSubmitUntilTime = UnityEngine.Time.realtimeSinceStartup + 0.5f;
            _pendingNotifyValue = selectedValue;
            Log.Focus("DropdownState",
                $"Dropdown item selected (value={selectedValue}), blocking Submit for 500ms");
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

            // Hand off Enter blocking from BlockSubmitForToggle to ShouldBlockEnterFromGame.
            // BlockSubmitForToggle was set when navigating TO the dropdown (before opening) to
            // prevent SendSubmitEventToSelectedObject from auto-advancing the form. Now that
            // the dropdown is open, ShouldBlockEnterFromGame takes over. We must clear
            // BlockSubmitForToggle so Input.GetKeyDown(Enter) works for item selection inside
            // the dropdown (the GetKeyDown_Postfix blocks Enter when BlockSubmitForToggle is true).
            InputManager.BlockSubmitForToggle = false;

            // Invalidate dropdown scan cache so queries get fresh state
            UIFocusTracker.InvalidateDropdownCache();

            // Suppress onValueChanged to prevent form auto-advance while browsing items
            SuppressOnValueChanged(dropdown);

            Log.Focus("DropdownState",
                $"User opened dropdown: {dropdown?.name}");
        }

        /// <summary>
        /// Called when user explicitly closes a dropdown (Escape/Backspace).
        /// Returns the name of the element that now has focus (for navigator sync).
        /// </summary>
        public static string OnDropdownClosed()
        {
            _blockEnterFromGame = false;
            // Block Submit for 500ms to prevent MTGA from auto-clicking
            // the element that receives focus after dropdown closes
            _blockSubmitUntilTime = UnityEngine.Time.realtimeSinceStartup + 0.5f;
            string newFocusName = null;

            // Capture pending value before restore (restore clears _suppressedDropdownComponent)
            int pendingValue = _pendingNotifyValue;
            var dropdownComponent = _suppressedDropdownComponent;
            _pendingNotifyValue = -1;

            // Restore onValueChanged before clearing active dropdown
            RestoreOnValueChanged();

            // Fire onValueChanged if user confirmed a selection (Enter, not Escape)
            if (pendingValue >= 0 && dropdownComponent != null)
            {
                FireOnValueChanged(dropdownComponent, pendingValue);
            }

            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
            {
                newFocusName = eventSystem.currentSelectedGameObject.name;
            }

            Log.Focus("DropdownState",
                $"User closed dropdown, new focus: {newFocusName ?? "null"}");

            _activeDropdownObject = null;

            // Invalidate dropdown scan cache so queries get fresh state
            UIFocusTracker.InvalidateDropdownCache();

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
            _pendingNotifyValue = -1;
            RestoreOnValueChanged();
            Log.Focus("DropdownState",
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
            _blockSubmitUntilTime = -1f;
            _blockEnterFromGame = false;
            _pendingNotifyValue = -1;
            RestoreOnValueChanged();
            Log.Focus("DropdownState", "State reset");
        }

        #endregion

        #region onValueChanged Suppression

        /// <summary>
        /// Temporarily replace a dropdown's m_OnValueChanged with an empty event.
        /// This prevents the game's form validation from detecting value changes
        /// while the user is browsing dropdown items, which would cause auto-advance
        /// to the next page before the user has made their selection.
        /// </summary>
        private static void SuppressOnValueChanged(GameObject dropdownObj)
        {
            if (dropdownObj == null) return;

            // Restore any previously suppressed callback first
            RestoreOnValueChanged();

            // Try cTMP_Dropdown (MTGA's custom dropdown - most common)
            foreach (var component in dropdownObj.GetComponents<Component>())
            {
                if (component != null && component.GetType().Name == T.CustomTMPDropdown)
                {
                    var type = component.GetType();
                    var field = GetOnValueChangedField(type);
                    if (field != null)
                    {
                        _savedOnValueChanged = field.GetValue(component);
                        _suppressedDropdownComponent = component;
                        // Replace with empty event of the same type
                        var emptyEvent = System.Activator.CreateInstance(field.FieldType);
                        field.SetValue(component, emptyEvent);
                        Log.Msg("DropdownState", "Suppressed onValueChanged on cTMP_Dropdown");
                    }
                    return;
                }
            }

            // Try standard TMP_Dropdown
            var tmpDropdown = dropdownObj.GetComponent<TMPro.TMP_Dropdown>();
            if (tmpDropdown != null)
            {
                _savedOnValueChanged = tmpDropdown.onValueChanged;
                _suppressedDropdownComponent = tmpDropdown;
                tmpDropdown.onValueChanged = new TMPro.TMP_Dropdown.DropdownEvent();
                Log.Msg("DropdownState", "Suppressed onValueChanged on TMP_Dropdown");
                return;
            }

            // Try legacy Dropdown
            var legacyDropdown = dropdownObj.GetComponent<UnityEngine.UI.Dropdown>();
            if (legacyDropdown != null)
            {
                _savedOnValueChanged = legacyDropdown.onValueChanged;
                _suppressedDropdownComponent = legacyDropdown;
                legacyDropdown.onValueChanged = new UnityEngine.UI.Dropdown.DropdownEvent();
                Log.Msg("DropdownState", "Suppressed onValueChanged on legacy Dropdown");
            }
        }

        /// <summary>
        /// Restore the dropdown's original m_OnValueChanged event.
        /// Called when the dropdown closes (user close, auto-close, or reset).
        /// </summary>
        private static void RestoreOnValueChanged()
        {
            if (_savedOnValueChanged == null || _suppressedDropdownComponent == null)
                return;

            try
            {
                // Check if the component is still alive (scene may have changed)
                if (_suppressedDropdownComponent == null || _suppressedDropdownComponent.gameObject == null)
                {
                    Log.Msg("DropdownState", "Suppressed dropdown was destroyed, skipping restore");
                    _savedOnValueChanged = null;
                    _suppressedDropdownComponent = null;
                    return;
                }

                var typeName = _suppressedDropdownComponent.GetType().Name;

                if (typeName == T.CustomTMPDropdown)
                {
                    var field = GetOnValueChangedField(_suppressedDropdownComponent.GetType());
                    if (field != null)
                    {
                        field.SetValue(_suppressedDropdownComponent, _savedOnValueChanged);
                        Log.Msg("DropdownState", "Restored onValueChanged on cTMP_Dropdown");
                    }
                }
                else if (_suppressedDropdownComponent is TMPro.TMP_Dropdown tmpDropdown)
                {
                    tmpDropdown.onValueChanged = (TMPro.TMP_Dropdown.DropdownEvent)_savedOnValueChanged;
                    Log.Msg("DropdownState", "Restored onValueChanged on TMP_Dropdown");
                }
                else if (_suppressedDropdownComponent is UnityEngine.UI.Dropdown legacyDropdown)
                {
                    legacyDropdown.onValueChanged = (UnityEngine.UI.Dropdown.DropdownEvent)_savedOnValueChanged;
                    Log.Msg("DropdownState", "Restored onValueChanged on legacy Dropdown");
                }
            }
            catch (System.Exception ex)
            {
                Log.Warn("DropdownState", $"Error restoring onValueChanged: {ex.Message}");
            }

            _savedOnValueChanged = null;
            _suppressedDropdownComponent = null;
        }

        /// <summary>
        /// Invoke onValueChanged on a dropdown component to notify the game of the new value.
        /// Called after RestoreOnValueChanged when the user confirmed a selection.
        /// </summary>
        private static void FireOnValueChanged(Component dropdownComponent, int value)
        {
            try
            {
                if (dropdownComponent == null || dropdownComponent.gameObject == null)
                    return;

                var typeName = dropdownComponent.GetType().Name;

                if (typeName == T.CustomTMPDropdown)
                {
                    var field = GetOnValueChangedField(dropdownComponent.GetType());
                    if (field != null)
                    {
                        var onValueChanged = field.GetValue(dropdownComponent);
                        if (onValueChanged != null)
                        {
                            var invokeMethod = onValueChanged.GetType().GetMethod("Invoke",
                                new System.Type[] { typeof(int) });
                            if (invokeMethod != null)
                            {
                                invokeMethod.Invoke(onValueChanged, new object[] { value });
                                Log.Msg("DropdownState", $"Fired onValueChanged on cTMP_Dropdown with value={value}");
                            }
                        }
                    }
                }
                else if (dropdownComponent is TMPro.TMP_Dropdown tmpDropdown)
                {
                    tmpDropdown.onValueChanged.Invoke(value);
                    Log.Msg("DropdownState", $"Fired onValueChanged on TMP_Dropdown with value={value}");
                }
                else if (dropdownComponent is UnityEngine.UI.Dropdown legacyDropdown)
                {
                    legacyDropdown.onValueChanged.Invoke(value);
                    Log.Msg("DropdownState", $"Fired onValueChanged on legacy Dropdown with value={value}");
                }
            }
            catch (System.Exception ex)
            {
                Log.Warn("DropdownState", $"Error firing onValueChanged: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the m_OnValueChanged FieldInfo for a cTMP_Dropdown type, with caching.
        /// </summary>
        private static FieldInfo GetOnValueChangedField(System.Type type)
        {
            if (_cachedOnValueChangedField != null && _cachedOnValueChangedField.DeclaringType == type)
                return _cachedOnValueChangedField;

            _cachedOnValueChangedField = type.GetField("m_OnValueChanged",
                PrivateInstance);
            return _cachedOnValueChangedField;
        }

        #endregion
    }
}
