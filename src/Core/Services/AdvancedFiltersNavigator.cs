using UnityEngine;
using UnityEngine.UI;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System.Collections.Generic;
using System.Linq;
using TMPro;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for the Advanced Filters popup in Collection/Deck Builder.
    /// Provides grid-based navigation: Up/Down switches rows, Left/Right navigates within row.
    /// </summary>
    public class AdvancedFiltersNavigator : BaseNavigator
    {
        public override string NavigatorId => "AdvancedFilters";
        public override string ScreenName => Strings.ScreenAdvancedFilters;
        public override int Priority => 87; // Higher than RewardPopup (86), below SettingsMenu (90)

        // Row structure for grid navigation
        private readonly List<FilterRow> _rows = new List<FilterRow>();
        private int _currentRowIndex = -1;
        private int _currentItemIndex = -1;

        // Popup reference
        private GameObject _popup;

        // Cache to avoid logging spam
        private bool _lastPopupState = false;

        private struct FilterRow
        {
            public string Name;           // "Types", "Rarity", "Actions"
            public List<FilterItem> Items;
        }

        private struct FilterItem
        {
            public GameObject GameObject;
            public string Label;
            public bool IsToggle;
            public bool IsDropdown;
            public Toggle ToggleComponent;  // Store reference to avoid re-fetching wrong component
        }

        public AdvancedFiltersNavigator(IAnnouncementService announcer) : base(announcer) { }

        #region Detection

        protected override bool DetectScreen()
        {
            bool result = CheckAdvancedFiltersPopupOpen();

            // Only log when state changes
            if (result != _lastPopupState)
            {
                _lastPopupState = result;
                MelonLogger.Msg($"[{NavigatorId}] AdvancedFiltersPopup open: {result}");
            }

            return result;
        }

        private bool CheckAdvancedFiltersPopupOpen()
        {
            // Look for the popup in the scene
            _popup = FindAdvancedFiltersPopup();
            return _popup != null && _popup.activeInHierarchy;
        }

        private GameObject FindAdvancedFiltersPopup()
        {
            // Try to find AdvancedFiltersPopup(Clone) anywhere in the scene
            var allTransforms = Object.FindObjectsOfType<Transform>();
            foreach (var t in allTransforms)
            {
                if (t.name == "AdvancedFiltersPopup(Clone)" && t.gameObject.activeInHierarchy)
                {
                    return t.gameObject;
                }
            }
            return null;
        }

        #endregion

        #region Discovery - Build rows from elements

        protected override void DiscoverElements()
        {
            _rows.Clear();
            _elements.Clear();

            if (_popup == null)
            {
                MelonLogger.Msg($"[{NavigatorId}] No popup found during discovery");
                return;
            }

            MelonLogger.Msg($"[{NavigatorId}] Discovering elements in AdvancedFiltersPopup");

            // Build rows based on parent path patterns
            var typesRow = new FilterRow { Name = "Types", Items = new List<FilterItem>() };
            var rarityRow = new FilterRow { Name = "Rarity", Items = new List<FilterItem>() };
            var actionsRow = new FilterRow { Name = "Actions", Items = new List<FilterItem>() };

            // Find all interactable elements in the popup
            var toggles = _popup.GetComponentsInChildren<Toggle>(true);
            var dropdowns = _popup.GetComponentsInChildren<TMP_Dropdown>(true);
            var buttons = _popup.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(mb => mb.GetType().Name == "CustomButton" || mb.GetType().Name == "CustomButtonWithTooltip");

            // Process toggles
            foreach (var toggle in toggles)
            {
                if (!toggle.gameObject.activeInHierarchy) continue;
                if (!toggle.interactable) continue;

                string path = GetPath(toggle.transform);
                string label = GetToggleLabel(toggle);

                // Skip headers (they are toggles but expand/collapse sections)
                if (toggle.gameObject.name.StartsWith("Header_")) continue;

                FilterItem item = new FilterItem
                {
                    GameObject = toggle.gameObject,
                    Label = label,
                    IsToggle = true,
                    IsDropdown = false,
                    ToggleComponent = toggle  // Store direct reference
                };

                // Categorize by path
                if (path.Contains("Column_1/TYPES/"))
                {
                    typesRow.Items.Add(item);
                }
                else if (path.Contains("Column_1/RARITY/"))
                {
                    rarityRow.Items.Add(item);
                }
                else if (path.Contains("Column_1/COLLECTION/"))
                {
                    actionsRow.Items.Add(item);
                }

                MelonLogger.Msg($"[{NavigatorId}] Toggle: {label} - Path: {path}");
            }

            // Process dropdowns
            foreach (var dropdown in dropdowns)
            {
                if (!dropdown.gameObject.activeInHierarchy) continue;
                if (!dropdown.interactable) continue;

                string path = GetPath(dropdown.transform);
                string label = GetDropdownLabel(dropdown);

                FilterItem item = new FilterItem
                {
                    GameObject = dropdown.gameObject,
                    Label = label,
                    IsToggle = false,
                    IsDropdown = true
                };

                // Format dropdown goes in actions row
                if (path.Contains("FormatDropdown") || path.Contains("Column_2"))
                {
                    actionsRow.Items.Add(item);
                }

                MelonLogger.Msg($"[{NavigatorId}] Dropdown: {label} - Path: {path}");
            }

            // Process buttons (Reset, Apply/OK)
            foreach (var button in buttons)
            {
                if (!button.gameObject.activeInHierarchy) continue;

                string path = GetPath(button.transform);
                string buttonName = button.gameObject.name;

                // Only include buttons in ButtonCONTAINER
                if (!path.Contains("ButtonCONTAINER")) continue;

                string label = GetButtonLabel(button.gameObject);

                FilterItem item = new FilterItem
                {
                    GameObject = button.gameObject,
                    Label = label,
                    IsToggle = false,
                    IsDropdown = false
                };

                actionsRow.Items.Add(item);

                MelonLogger.Msg($"[{NavigatorId}] Button: {label} - Path: {path}");
            }

            // Sort items within rows by horizontal position (left to right)
            SortRowByPosition(typesRow);
            SortRowByPosition(rarityRow);
            SortRowByPosition(actionsRow);

            // Add non-empty rows
            if (typesRow.Items.Count > 0)
                _rows.Add(typesRow);
            if (rarityRow.Items.Count > 0)
                _rows.Add(rarityRow);
            if (actionsRow.Items.Count > 0)
                _rows.Add(actionsRow);

            // Also add all items to _elements for BaseNavigator compatibility
            foreach (var row in _rows)
            {
                foreach (var item in row.Items)
                {
                    AddElement(item.GameObject, item.Label);
                }
            }

            // Set initial position
            if (_rows.Count > 0)
            {
                _currentRowIndex = 0;
                _currentItemIndex = _rows[0].Items.Count > 0 ? 0 : -1;
            }
            else
            {
                _currentRowIndex = -1;
                _currentItemIndex = -1;
            }

            MelonLogger.Msg($"[{NavigatorId}] Discovered {_rows.Count} rows, {_elements.Count} total elements");
            foreach (var row in _rows)
            {
                MelonLogger.Msg($"[{NavigatorId}]   {row.Name}: {row.Items.Count} items");
            }
        }

        private void SortRowByPosition(FilterRow row)
        {
            // Sort by x position (left to right)
            row.Items = row.Items
                .OrderBy(item => item.GameObject.transform.position.x)
                .ToList();
        }

        private string GetPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null)
            {
                parts.Insert(0, t.name);
                t = t.parent;
            }
            return string.Join("/", parts);
        }

        private string GetToggleLabel(Toggle toggle)
        {
            // Try to get text from TMP_Text child
            var tmpText = toggle.GetComponentInChildren<TMP_Text>(true);
            if (tmpText != null && !string.IsNullOrEmpty(tmpText.text?.Trim()))
            {
                return tmpText.text.Trim();
            }

            // Fallback: extract from object name
            string name = toggle.gameObject.name;
            // Remove prefixes like "Type_", "Rarity_", "Toggle_"
            name = name.Replace("Type_", "").Replace("Rarity_", "").Replace("Toggle_", "").Replace("Collection_", "");
            return name;
        }

        private string GetDropdownLabel(TMP_Dropdown dropdown)
        {
            string selectedOption = dropdown.options.Count > dropdown.value
                ? dropdown.options[dropdown.value].text
                : "Unknown";
            return $"Format: {selectedOption}, dropdown";
        }

        private string GetButtonLabel(GameObject button)
        {
            // Try text extraction
            string text = UITextExtractor.GetButtonText(button, null);
            if (!string.IsNullOrEmpty(text))
                return $"{text}, button";

            // Fallback to name
            string name = button.name;
            if (name.Contains("Reset"))
                return "Reset, button";
            if (name.Contains("Main") || name.Contains("OK") || name.Contains("Apply"))
                return "OK, button";

            return "Button";
        }

        #endregion

        #region Navigation

        protected override string GetActivationAnnouncement()
        {
            int totalItems = _rows.Sum(r => r.Items.Count);
            return $"Advanced Filters. Up and Down to switch sections. Left and Right to navigate. Enter to toggle. {_rows.Count} sections, {totalItems} items.";
        }

        protected override void HandleInput()
        {
            // Don't use base input - we handle everything custom

            // Update dropdown state tracking each frame
            DropdownStateManager.UpdateAndCheckExitTransition();

            // Check if a dropdown is open - block navigation keys but allow closing
            if (DropdownStateManager.IsInDropdownMode)
            {
                // Only handle Backspace/Escape to close the dropdown
                if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Escape))
                {
                    CloseActiveDropdownInternal();
                    return;
                }
                // Block all other navigation - let Unity handle dropdown navigation (Up/Down for options)
                return;
            }

            // Row navigation (Up/Down)
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                MoveToPreviousRow();
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                MoveToNextRow();
                return;
            }

            // Item navigation within row (Left/Right)
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                MoveToPreviousItem();
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                MoveToNextItem();
                return;
            }

            // Home/End within current row
            if (Input.GetKeyDown(KeyCode.Home))
            {
                MoveToFirstItemInRow();
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                MoveToLastItemInRow();
                return;
            }

            // Enter or Space activates (toggle or click)
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                // Consume keys to prevent GeneralMenuNavigator from processing them after popup closes
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
                InputManager.ConsumeKey(KeyCode.Space);
                ActivateCurrentItem();
                return;
            }

            // Backspace closes popup - consume key to prevent deck builder from also closing
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                ClosePopup();
                return;
            }
        }

        private void MoveToPreviousRow()
        {
            if (_rows.Count == 0) return;

            if (_currentRowIndex > 0)
            {
                _currentRowIndex--;
                // Preserve horizontal position as much as possible
                _currentItemIndex = Mathf.Min(_currentItemIndex, _rows[_currentRowIndex].Items.Count - 1);
                if (_currentItemIndex < 0) _currentItemIndex = 0;
                AnnounceCurrentPosition(true);
            }
            else
            {
                _announcer.AnnounceVerbose(Strings.FirstSection, AnnouncementPriority.Normal);
            }
        }

        private void MoveToNextRow()
        {
            if (_rows.Count == 0) return;

            if (_currentRowIndex < _rows.Count - 1)
            {
                _currentRowIndex++;
                // Preserve horizontal position as much as possible
                _currentItemIndex = Mathf.Min(_currentItemIndex, _rows[_currentRowIndex].Items.Count - 1);
                if (_currentItemIndex < 0) _currentItemIndex = 0;
                AnnounceCurrentPosition(true);
            }
            else
            {
                _announcer.AnnounceVerbose(Strings.LastSection, AnnouncementPriority.Normal);
            }
        }

        private void MoveToPreviousItem()
        {
            if (!IsValidPosition()) return;

            var row = _rows[_currentRowIndex];
            if (_currentItemIndex > 0)
            {
                _currentItemIndex--;
                AnnounceCurrentPosition(false);
            }
            else
            {
                _announcer.AnnounceVerbose(Strings.StartOfRow, AnnouncementPriority.Normal);
            }
        }

        private void MoveToNextItem()
        {
            if (!IsValidPosition()) return;

            var row = _rows[_currentRowIndex];
            if (_currentItemIndex < row.Items.Count - 1)
            {
                _currentItemIndex++;
                AnnounceCurrentPosition(false);
            }
            else
            {
                _announcer.AnnounceVerbose(Strings.EndOfRowNav, AnnouncementPriority.Normal);
            }
        }

        private void MoveToFirstItemInRow()
        {
            if (!IsValidPosition()) return;

            _currentItemIndex = 0;
            AnnounceCurrentPosition(false);
        }

        private void MoveToLastItemInRow()
        {
            if (!IsValidPosition()) return;

            var row = _rows[_currentRowIndex];
            _currentItemIndex = row.Items.Count - 1;
            AnnounceCurrentPosition(false);
        }

        private bool IsValidPosition()
        {
            return _currentRowIndex >= 0 &&
                   _currentRowIndex < _rows.Count &&
                   _currentItemIndex >= 0 &&
                   _currentItemIndex < _rows[_currentRowIndex].Items.Count;
        }

        private void AnnounceCurrentPosition(bool includeRowName)
        {
            if (!IsValidPosition()) return;

            var row = _rows[_currentRowIndex];
            var item = row.Items[_currentItemIndex];

            string state = "";
            if (item.IsToggle && item.ToggleComponent != null)
            {
                state = item.ToggleComponent.isOn ? ", on" : ", off";
            }

            string position = $"{_currentItemIndex + 1} of {row.Items.Count}";

            if (includeRowName)
            {
                _announcer.Announce($"{row.Name}. {item.Label}{state}, {position}", AnnouncementPriority.High);
            }
            else
            {
                _announcer.Announce($"{item.Label}{state}, {position}", AnnouncementPriority.Normal);
            }
        }

        private void ActivateCurrentItem()
        {
            if (!IsValidPosition()) return;

            var item = _rows[_currentRowIndex].Items[_currentItemIndex];

            if (item.IsToggle)
            {
                // Use UIActivator to properly trigger game's event handlers
                UIActivator.Activate(item.GameObject);

                // Read back the state after activation using stored reference
                // Note: Game may asynchronously reset state if it requires at least one option selected
                if (item.ToggleComponent != null)
                {
                    string state = item.ToggleComponent.isOn ? "on" : "off";
                    _announcer.Announce($"{item.Label}, {state}", AnnouncementPriority.Normal);
                }
                else
                {
                    _announcer.Announce(Strings.Toggled(item.Label), AnnouncementPriority.Normal);
                }
            }
            else if (item.IsDropdown)
            {
                // Activate dropdown and notify state manager
                UIActivator.Activate(item.GameObject);
                DropdownStateManager.OnDropdownOpened(item.GameObject);
                _announcer.Announce(Strings.Opening(item.Label), AnnouncementPriority.Normal);
            }
            else
            {
                // Button - click it
                UIActivator.Activate(item.GameObject);

                // If it's the OK/Apply button, popup will close
                if (item.Label.Contains("OK") || item.Label.Contains("Apply"))
                {
                    _announcer.Announce(Strings.ApplyingFilters, AnnouncementPriority.Normal);
                }
                else if (item.Label.Contains("Reset"))
                {
                    _announcer.Announce(Strings.FiltersReset, AnnouncementPriority.Normal);
                    // Rescan to update toggle states
                    ForceRescan();
                }
            }
        }

        private void CloseActiveDropdownInternal()
        {
            var activeDropdown = DropdownStateManager.ActiveDropdown;
            if (activeDropdown == null) return;

            var tmpDropdown = activeDropdown.GetComponent<TMP_Dropdown>();
            if (tmpDropdown != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Closing TMP_Dropdown");
                tmpDropdown.Hide();
                DropdownStateManager.OnDropdownClosed();
                _announcer.Announce(Strings.DropdownClosed, AnnouncementPriority.Normal);
                return;
            }

            var legacyDropdown = activeDropdown.GetComponent<Dropdown>();
            if (legacyDropdown != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Closing legacy Dropdown");
                legacyDropdown.Hide();
                DropdownStateManager.OnDropdownClosed();
                _announcer.Announce(Strings.DropdownClosed, AnnouncementPriority.Normal);
            }
        }

        private void ClosePopup()
        {
            MelonLogger.Msg($"[{NavigatorId}] Closing Advanced Filters popup");

            if (_popup == null)
            {
                _announcer.Announce(Strings.PopupClosed, AnnouncementPriority.Normal);
                return;
            }

            // Try to find and click the close/cancel button or background blocker
            // Look for a close button first
            var closeButton = FindCloseButton();
            if (closeButton != null)
            {
                UIActivator.Activate(closeButton);
                _announcer.Announce(Strings.FiltersCancelled, AnnouncementPriority.Normal);
                return;
            }

            // Try clicking a background blocker to dismiss without applying
            var blocker = FindBlockerOrBackground();
            if (blocker != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Clicking blocker to dismiss popup");
                UIActivator.Activate(blocker);
                _announcer.Announce(Strings.FiltersDismissed, AnnouncementPriority.Normal);
                return;
            }

            // Last resort: Click the OK button (will apply filters)
            var okButton = FindOkButton();
            if (okButton != null)
            {
                UIActivator.Activate(okButton);
                _announcer.Announce(Strings.ApplyingFilters, AnnouncementPriority.Normal);
                return;
            }

            _announcer.Announce(Strings.CouldNotClosePopup, AnnouncementPriority.High);
        }

        private GameObject FindBlockerOrBackground()
        {
            if (_popup == null) return null;

            // Look for blocker in parent hierarchy or siblings
            var parent = _popup.transform.parent;
            if (parent != null)
            {
                // Check siblings for Blocker
                foreach (Transform sibling in parent)
                {
                    if (sibling.name.ToLower().Contains("blocker") ||
                        sibling.name.ToLower().Contains("background") ||
                        sibling.name.ToLower().Contains("overlay"))
                    {
                        var button = sibling.GetComponent<UnityEngine.UI.Button>();
                        if (button != null && button.interactable)
                        {
                            return sibling.gameObject;
                        }
                    }
                }
            }

            // Also check for Blocker as a direct child of popup root
            var popupRoot = _popup.transform.parent;
            if (popupRoot != null)
            {
                var blockerTransform = popupRoot.Find("Blocker");
                if (blockerTransform != null)
                {
                    return blockerTransform.gameObject;
                }
            }

            return null;
        }

        private GameObject FindCloseButton()
        {
            if (_popup == null) return null;

            // Look for close/cancel buttons
            foreach (var mb in _popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                string typeName = mb.GetType().Name;
                if (typeName != "CustomButton" && typeName != "CustomButtonWithTooltip") continue;

                string name = mb.gameObject.name.ToLower();
                if (name.Contains("close") || name.Contains("cancel") || name.Contains("back"))
                {
                    return mb.gameObject;
                }
            }

            return null;
        }

        private GameObject FindOkButton()
        {
            if (_popup == null) return null;

            // Look for OK/Apply/Main button
            foreach (var mb in _popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                string typeName = mb.GetType().Name;
                if (typeName != "CustomButton" && typeName != "CustomButtonWithTooltip") continue;

                string name = mb.gameObject.name.ToLower();
                if (name.Contains("main") || name.Contains("ok") || name.Contains("apply") || name.Contains("confirm"))
                {
                    return mb.gameObject;
                }
            }

            return null;
        }

        #endregion

        #region Lifecycle

        protected override void OnActivated()
        {
            base.OnActivated();

            // Announce initial position after activation announcement
            if (IsValidPosition())
            {
                // Defer initial position announcement slightly
                // The activation announcement from base class will play first
            }
        }

        protected override bool ValidateElements()
        {
            if (_popup == null || !_popup.activeInHierarchy)
            {
                MelonLogger.Msg($"[{NavigatorId}] Popup no longer active");
                return false;
            }

            return true;
        }

        public override void OnSceneChanged(string sceneName)
        {
            if (_isActive)
            {
                Deactivate();
            }
            _popup = null;
            _rows.Clear();
            _currentRowIndex = -1;
            _currentItemIndex = -1;
        }

        #endregion
    }
}
