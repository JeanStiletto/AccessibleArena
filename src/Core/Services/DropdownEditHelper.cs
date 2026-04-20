using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System;
using AccessibleArena.Core.Utils;

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
        private bool _needsInitialFocus;

        // Cached after TryFocusFirstItem discovers items
        private int _itemCount = -1;
        private GameObject _firstItemObject;

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
            _needsInitialFocus = true;
            _itemCount = -1;
            _firstItemObject = null;
            UIActivator.Activate(dropdown);
            DropdownStateManager.OnDropdownOpened(dropdown);
            _announcer?.Announce(Strings.DropdownOpened, AnnouncementPriority.Normal);
            Log.Msg("{_navigatorId}", $"DropdownEditHelper: entered edit mode for {dropdown.name}");
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
                Log.Msg("{_navigatorId}", $"DropdownEditHelper: dropdown closed externally, exiting edit mode");
                ClearState();
                return false;
            }

            // If no dropdown item has focus yet (value=-1 case), try to focus Item 0
            if (_needsInitialFocus)
            {
                TryFocusFirstItem();
            }

            // Tab: close dropdown and navigate to next/previous item
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                BaseNavigator.CloseDropdown(_navigatorId, _announcer, silent: true);
                DropdownStateManager.SuppressReentry();
                ClearState();

                int direction = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? -1 : 1;
                onTabNavigate?.Invoke(direction);
                return true;
            }

            // Escape: close dropdown, announce
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                BaseNavigator.CloseDropdown(_navigatorId, _announcer, silent: false);
                ClearState();
                return true;
            }

            // Backspace: close dropdown, consume so popup mode doesn't dismiss the popup
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                BaseNavigator.CloseDropdown(_navigatorId, _announcer, silent: false);
                ClearState();
                return true;
            }

            // Enter: select the focused item and close
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);

                // For single-item dropdowns, focus may have escaped — re-focus before selecting
                if (_itemCount == 1 && _firstItemObject != null)
                {
                    var es = EventSystem.current;
                    if (es != null) es.SetSelectedGameObject(_firstItemObject);
                }

                BaseNavigator.SelectCurrentDropdownItem(_navigatorId);
                BaseNavigator.CloseDropdown(_navigatorId, _announcer, silent: true);
                ClearState();
                return true;
            }

            // Single-item dropdown: consume arrow keys and re-announce instead of
            // passing through to Unity (which would escape focus out of the dropdown).
            // Recount first — cTMP_Dropdown (e.g. challenge invite) may create items
            // asynchronously, so the initial count from TryFocusFirstItem can be stale.
            if (_itemCount == 1)
                CountItems();
            if (_itemCount == 1 && (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)))
            {
                if (_firstItemObject != null)
                {
                    var es = EventSystem.current;
                    if (es != null) es.SetSelectedGameObject(_firstItemObject);

                    string itemText = ExtractItemText(_firstItemObject.name);
                    _announcer?.AnnounceInterrupt(itemText);
                }
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
            }
            ClearState();
        }

        private void ClearState()
        {
            _editingDropdown = null;
            _needsInitialFocus = false;
            _itemCount = -1;
            _firstItemObject = null;
        }

        /// <summary>
        /// When a dropdown opens with value=-1, no item gets initial focus.
        /// Find the first Toggle in the dropdown list and set EventSystem focus to it.
        /// Also counts items for single-item handling.
        /// Called each frame until successful or dropdown closes.
        /// </summary>
        private void TryFocusFirstItem()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            // Check if an item already has focus (normal case where value >= 0)
            var current = eventSystem.currentSelectedGameObject;
            if (current != null && UIFocusTracker.IsDropdownItem(current))
            {
                _needsInitialFocus = false;
                // Still count items if we haven't yet
                if (_itemCount < 0) CountItems();
                return;
            }

            // Search the dropdown's children for the item list (created by Unity when dropdown opens)
            if (_editingDropdown == null) return;

            // Unity creates a "Dropdown List" child Canvas with Toggle items
            int count = 0;
            GameObject firstItem = null;
            var toggles = _editingDropdown.GetComponentsInChildren<Toggle>(true);
            foreach (var toggle in toggles)
            {
                if (toggle == null || !toggle.gameObject.activeInHierarchy) continue;
                if (!toggle.gameObject.name.StartsWith("Item ")) continue;
                if (count == 0) firstItem = toggle.gameObject;
                count++;
            }

            if (firstItem == null) return; // Items not created yet — retry next frame

            _itemCount = count;
            _firstItemObject = firstItem;
            _needsInitialFocus = false;

            eventSystem.SetSelectedGameObject(firstItem);
            Log.Msg("{_navigatorId}", $"DropdownEditHelper: focused first item '{firstItem.name}' ({count} total)");
        }

        /// <summary>
        /// Count items when an item already had focus (normal dropdowns with value >= 0).
        /// </summary>
        private void CountItems()
        {
            if (_editingDropdown == null) return;

            int count = 0;
            GameObject firstItem = null;
            var toggles = _editingDropdown.GetComponentsInChildren<Toggle>(true);
            foreach (var toggle in toggles)
            {
                if (toggle == null || !toggle.gameObject.activeInHierarchy) continue;
                if (!toggle.gameObject.name.StartsWith("Item ")) continue;
                if (count == 0) firstItem = toggle.gameObject;
                count++;
            }
            _itemCount = count;
            _firstItemObject = firstItem;
        }

        /// <summary>
        /// Extract display text from an item name like "Item 0: Alfi#80856" → "Alfi#80856".
        /// </summary>
        private static string ExtractItemText(string itemName)
        {
            int colonPos = itemName.IndexOf(": ");
            return colonPos >= 0 ? itemName.Substring(colonPos + 2) : itemName;
        }
    }
}
