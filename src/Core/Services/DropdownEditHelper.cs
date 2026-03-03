using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Shared dropdown editing logic used by BaseNavigator popup mode and other contexts.
    /// Thin wrapper: manages edit state and routes keys to BaseNavigator's static dropdown methods.
    /// Follows the same pattern as InputFieldEditHelper.
    /// </summary>
    public class DropdownEditHelper
    {
        private readonly IAnnouncementService _announcer;
        private readonly string _navigatorId;

        private GameObject _editingDropdown;

        public bool IsEditing => _editingDropdown != null;
        public GameObject EditingDropdown => _editingDropdown;

        public DropdownEditHelper(IAnnouncementService announcer, string navigatorId)
        {
            _announcer = announcer;
            _navigatorId = navigatorId;
        }

        /// <summary>
        /// Enter dropdown edit mode: activate the dropdown (opens it), register with DropdownStateManager.
        /// </summary>
        public void EnterEditMode(GameObject dropdown)
        {
            _editingDropdown = dropdown;
            UIActivator.Activate(dropdown);
            DropdownStateManager.OnDropdownOpened(dropdown);
            _announcer?.Announce(Strings.DropdownOpened, AnnouncementPriority.Normal);
            MelonLogger.Msg($"[{_navigatorId}] DropdownEditHelper: entered edit mode for {dropdown.name}");
        }

        /// <summary>
        /// Handle keys while a dropdown is open. Returns true if key was consumed.
        /// Arrow keys pass through to Unity for item browsing.
        /// </summary>
        /// <param name="onTabNavigate">Called with direction (-1 or 1) when Tab exits dropdown</param>
        public bool HandleEditing(Action<int> onTabNavigate)
        {
            // Auto-exit if dropdown closed itself (e.g., user clicked outside)
            if (!DropdownStateManager.IsInDropdownMode)
            {
                MelonLogger.Msg($"[{_navigatorId}] DropdownEditHelper: dropdown closed externally, exiting edit mode");
                _editingDropdown = null;
                return false;
            }

            // Tab: close dropdown and navigate to next/previous item
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                BaseNavigator.CloseDropdown(_navigatorId, _announcer, silent: true);
                DropdownStateManager.SuppressReentry();
                _editingDropdown = null;

                int direction = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? -1 : 1;
                onTabNavigate?.Invoke(direction);
                return true;
            }

            // Escape: close dropdown, announce
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                BaseNavigator.CloseDropdown(_navigatorId, _announcer, silent: false);
                _editingDropdown = null;
                return true;
            }

            // Backspace: close dropdown, consume so popup mode doesn't dismiss the popup
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                BaseNavigator.CloseDropdown(_navigatorId, _announcer, silent: false);
                _editingDropdown = null;
                return true;
            }

            // Enter: select the focused item and close
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
                BaseNavigator.SelectCurrentDropdownItem(_navigatorId);
                BaseNavigator.CloseDropdown(_navigatorId, _announcer, silent: true);
                _editingDropdown = null;
                return true;
            }

            // Arrow keys pass through to Unity's dropdown navigation
            // (UIFocusTracker announces focused items as they change)
            return false;
        }

        /// <summary>
        /// Full reset. Call when popup closes or navigator deactivates.
        /// </summary>
        public void Clear()
        {
            if (IsEditing)
            {
                BaseNavigator.CloseDropdown(_navigatorId, _announcer, silent: true);
                _editingDropdown = null;
            }
        }
    }
}
