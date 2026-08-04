using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    public partial class SetCollectionNavigator
    {
        #region Filter State

        private enum FilterKind { Sort, Format, Toggle }

        private struct FilterControl
        {
            public string Label;
            public FilterKind Kind;
            public TMP_Dropdown Dropdown;   // Sort / Format
            public Toggle Toggle;           // quick filters
        }

        private readonly List<FilterControl> _filters = new List<FilterControl>();
        private int _filterIndex;

        #endregion

        #region Filter Discovery

        private void DiscoverFilters()
        {
            _filters.Clear();
            if (_screenView == null || H == null) return;

            AddDropdownFilter(H.SortDropdown, Strings.SetCollectionFilterSort, FilterKind.Sort);
            AddDropdownFilter(H.FilterDropdown, Strings.SetCollectionFilterFormat, FilterKind.Format);
            AddToggleFilter(H.StandardToggle, Strings.SetCollectionFilterStandard);
            AddToggleFilter(H.HistoricToggle, Strings.SetCollectionFilterHistoric);
            AddToggleFilter(H.AlchemyToggle, Strings.SetCollectionFilterAlchemy);
        }

        private void AddDropdownFilter(System.Reflection.FieldInfo field, string label, FilterKind kind)
        {
            if (field == null) return;
            try
            {
                var dropdown = field.GetValue(_screenView) as TMP_Dropdown;
                if (dropdown == null || !dropdown.gameObject.activeInHierarchy) return;
                if (dropdown.options == null || dropdown.options.Count == 0) return;

                _filters.Add(new FilterControl { Label = label, Kind = kind, Dropdown = dropdown });
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"Filter '{label}' unavailable: {ex.Message}");
            }
        }

        private void AddToggleFilter(System.Reflection.FieldInfo field, string label)
        {
            if (field == null) return;
            try
            {
                var toggle = field.GetValue(_screenView) as Toggle;
                if (toggle == null || !toggle.gameObject.activeInHierarchy) return;

                _filters.Add(new FilterControl { Label = label, Kind = FilterKind.Toggle, Toggle = toggle });
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"Filter '{label}' unavailable: {ex.Message}");
            }
        }

        #endregion

        #region Filter Input

        private void HandleFiltersInput()
        {
            if (_filters.Count == 0)
            {
                // Nothing to filter with — fall straight through to the sets.
                if (InputManager.GetEnterAndConsume()) EnterSetsLevel();
                else if (Input.GetKeyDown(KeyCode.Backspace)) LeaveScreen();
                return;
            }

            // Up/Down: walk the filter controls
            if (_holdRepeater.Check(KeyCode.UpArrow, () =>
            {
                if (_filterIndex <= 0)
                {
                    _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                    return false;
                }
                _filterIndex--;
                AnnounceCurrentFilter();
                return true;
            })) return;

            if (_holdRepeater.Check(KeyCode.DownArrow, () =>
            {
                if (_filterIndex >= _filters.Count - 1)
                {
                    _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                    return false;
                }
                _filterIndex++;
                AnnounceCurrentFilter();
                return true;
            })) return;

            // Left/Right: change the current control's value in place. Deliberately never opens
            // MTGA's real dropdown — the game auto-opens dropdowns on EventSystem selection and
            // fighting that costs more than setting the value directly (see BaseNavigator.Dropdowns).
            if (_holdRepeater.Check(KeyCode.LeftArrow, () => ChangeCurrentFilter(-1))) return;
            if (_holdRepeater.Check(KeyCode.RightArrow, () => ChangeCurrentFilter(1))) return;

            // Home/End: first/last filter control
            if (Input.GetKeyDown(KeyCode.Home))
            {
                _filterIndex = 0;
                AnnounceCurrentFilter();
                return;
            }
            if (Input.GetKeyDown(KeyCode.End))
            {
                _filterIndex = _filters.Count - 1;
                AnnounceCurrentFilter();
                return;
            }

            // Space: activate a quick-filter toggle (same as Right, but matches the usual toggle key)
            if (InputManager.GetKeyDownAndConsume(KeyCode.Space))
            {
                if (_filters[_filterIndex].Kind == FilterKind.Toggle)
                    ChangeCurrentFilter(1);
                else
                    AnnounceCurrentFilter();
                return;
            }

            // Enter: drill into the set list
            if (InputManager.GetEnterAndConsume())
            {
                EnterSetsLevel();
                return;
            }

            // Backspace: leave the screen entirely
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                LeaveScreen();
                return;
            }
        }

        /// <summary>
        /// Applies a value change to the focused filter and announces the result together with
        /// the new number of visible sets — the count is the only feedback that a filter did
        /// anything, since the badge grid itself is silent.
        /// </summary>
        private bool ChangeCurrentFilter(int direction)
        {
            if (_filterIndex < 0 || _filterIndex >= _filters.Count) return false;
            var filter = _filters[_filterIndex];

            switch (filter.Kind)
            {
                case FilterKind.Sort:
                case FilterKind.Format:
                    return ChangeDropdownFilter(filter, direction);
                case FilterKind.Toggle:
                    return ActivateToggleFilter(filter);
                default:
                    return false;
            }
        }

        private bool ChangeDropdownFilter(FilterControl filter, int direction)
        {
            var dropdown = filter.Dropdown;
            if (dropdown == null || dropdown.options == null || dropdown.options.Count == 0) return false;

            int newValue = dropdown.value + direction;
            if (newValue < 0)
            {
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return false;
            }
            if (newValue >= dropdown.options.Count)
            {
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                return false;
            }

            // Set silently, then drive the game's own handler. Going through onValueChanged
            // would depend on prefab-wired listeners (the sort dropdown has none in code).
            dropdown.SetValueWithoutNotify(newValue);
            dropdown.RefreshShownValue();

            try
            {
                if (filter.Kind == FilterKind.Sort)
                    H.SortBadges?.Invoke(_screenView, new object[] { newValue });
                else
                    H.FilterBadges?.Invoke(_screenView, new object[] { newValue });
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"Applying filter '{filter.Label}' failed: {ex.Message}");
            }

            BuildSetList();
            _setIndex = 0;
            _announcer.AnnounceInterrupt(Strings.SetCollectionFilterApplied(
                filter.Label, GetDropdownOptionText(dropdown, newValue), _sets.Count));
            return true;
        }

        /// <summary>
        /// The three quick filters behave as a radio group in the game: turning one off
        /// immediately re-enables it (OnStandardToggle re-sets the value when passed false),
        /// so the only meaningful action is turning one on.
        /// </summary>
        private bool ActivateToggleFilter(FilterControl filter)
        {
            var toggle = filter.Toggle;
            if (toggle == null) return false;

            if (toggle.isOn)
            {
                // Already the active quick filter — restate it rather than pretending to toggle.
                _announcer.AnnounceInterrupt(Strings.SetCollectionFilterApplied(
                    filter.Label, Strings.SetCollectionToggleOn, _sets.Count));
                return true;
            }

            toggle.isOn = true;   // fires the game's listener, which hides the non-matching badges

            BuildSetList();
            _setIndex = 0;
            _announcer.AnnounceInterrupt(Strings.SetCollectionFilterApplied(
                filter.Label, Strings.SetCollectionToggleOn, _sets.Count));
            return true;
        }

        private void AnnounceCurrentFilter()
        {
            if (_filterIndex < 0 || _filterIndex >= _filters.Count) return;
            var filter = _filters[_filterIndex];

            string value;
            if (filter.Kind == FilterKind.Toggle)
                value = filter.Toggle != null && filter.Toggle.isOn
                    ? Strings.SetCollectionToggleOn
                    : Strings.SetCollectionToggleOff;
            else
                value = GetDropdownOptionText(filter.Dropdown, filter.Dropdown?.value ?? 0);

            _announcer.AnnounceInterrupt(Strings.SetCollectionFilterValue(
                filter.Label, value, _filterIndex + 1, _filters.Count));
        }

        private static string GetDropdownOptionText(TMP_Dropdown dropdown, int index)
        {
            if (dropdown?.options == null) return "";
            if (index < 0 || index >= dropdown.options.Count) return "";
            return UITextExtractor.CleanText(dropdown.options[index].text ?? "");
        }

        #endregion
    }
}
