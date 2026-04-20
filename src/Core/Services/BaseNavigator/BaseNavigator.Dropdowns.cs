using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    public partial class BaseNavigator
    {
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
                Log.Msg("{NavigatorId}", $"Closing auto-opened dropdown (not user-initiated)");
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
                Log.Msg("{callerId}", $"Could not parse dropdown item index from: {itemName}");
                return;
            }

            var activeDropdown = DropdownStateManager.ActiveDropdown;
            if (activeDropdown == null)
            {
                Log.Msg("{callerId}", $"No active dropdown to select item on");
                return;
            }

            // Set value without triggering onValueChanged (it's suppressed while dropdown is open).
            // The pending value is stored so OnDropdownClosed can fire onValueChanged after
            // restoring the callback - this notifies the game so changes persist.
            if (SetDropdownValueSilent(activeDropdown, itemIndex))
            {
                DropdownStateManager.OnDropdownItemSelected(itemIndex);
                Log.Msg("{callerId}", $"Selected dropdown item {itemIndex}");
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
                    Log.Msg("{callerId}", $"Closing {kind} dropdown via ActiveDropdown reference");
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
                    Log.Msg("{callerId}", $"Closing {kind} dropdown via hierarchy walk");
                    DropdownStateManager.OnDropdownClosed();
                    if (!silent) announcer?.Announce(Strings.DropdownClosed, AnnouncementPriority.Normal);
                    return;
                }
                transform = transform.parent;
            }

            // Couldn't find dropdown - just exit edit mode
            Log.Msg("{callerId}", $"Could not find dropdown to close, exiting edit mode");
            DropdownStateManager.OnDropdownClosed();
        }
    }
}
