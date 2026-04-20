using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Models;
using System;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    public partial class StoreNavigator
    {
        #region Set Filter Discovery

        private void DiscoverSetFilters()
        {
            _setFilterModels.Clear();
            _setFilterTogglesComponent = null;
            _currentSetFilterIndex = 0;

            if (_controller == null || _setFiltersComponentField == null) return;

            try
            {
                var togglesObj = _setFiltersComponentField.GetValue(_controller);
                _setFilterTogglesComponent = togglesObj as MonoBehaviour;
                if (_setFilterTogglesComponent == null) return;

                // Read the list of set filter models
                if (_setFilterListField != null)
                {
                    var list = _setFilterListField.GetValue(_setFilterTogglesComponent);
                    if (list is System.Collections.IList iList)
                    {
                        for (int i = 0; i < iList.Count; i++)
                        {
                            if (iList[i] != null)
                                _setFilterModels.Add(iList[i]);
                        }
                    }
                }

                // Read current selected index
                if (_selectedIndexProp != null)
                {
                    _currentSetFilterIndex = (int)_selectedIndexProp.GetValue(_setFilterTogglesComponent);
                    if (_currentSetFilterIndex < 0 || _currentSetFilterIndex >= _setFilterModels.Count)
                        _currentSetFilterIndex = 0;
                }

                Log.Msg("Store", $"Discovered {_setFilterModels.Count} set filters, selected index: {_currentSetFilterIndex}");
            }
            catch (Exception ex)
            {
                Log.Msg("Store", $"Error discovering set filters: {ex.Message}");
            }
        }

        private string GetSetFilterName(int index)
        {
            if (index < 0 || index >= _setFilterModels.Count) return "Unknown";

            try
            {
                if (_setSymbolField != null)
                {
                    string setCode = _setSymbolField.GetValue(_setFilterModels[index]) as string;
                    if (!string.IsNullOrEmpty(setCode))
                        return UITextExtractor.MapSetCodeToName(setCode);
                }
            }
            catch { /* Reflection may fail on different game versions */ }

            return "Unknown";
        }

        #endregion

        #region Set Filter Input

        private void HandleSetFilterInput()
        {
            // Left/Right or Up/Down: cycle sets (hold-to-repeat)
            Func<bool> cycleBack = () => { int b = _currentSetFilterIndex; CycleSetFilter(-1); return _currentSetFilterIndex != b; };
            Func<bool> cycleFwd = () => { int b = _currentSetFilterIndex; CycleSetFilter(1); return _currentSetFilterIndex != b; };
            if (_holdRepeater.Check(KeyCode.LeftArrow, cycleBack)) return;
            if (_holdRepeater.Check(KeyCode.UpArrow, cycleBack)) return;
            if (_holdRepeater.Check(KeyCode.RightArrow, cycleFwd)) return;
            if (_holdRepeater.Check(KeyCode.DownArrow, cycleFwd)) return;

            // Tab/Shift+Tab
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                CycleSetFilter(shift ? -1 : 1);
                return;
            }

            // Home/End: jump to first/last set
            if (Input.GetKeyDown(KeyCode.Home))
            {
                if (_setFilterModels.Count > 0 && _currentSetFilterIndex != 0)
                {
                    SelectSetFilter(0);
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                if (_setFilterModels.Count > 0 && _currentSetFilterIndex != _setFilterModels.Count - 1)
                {
                    SelectSetFilter(_setFilterModels.Count - 1);
                }
                return;
            }

            // Enter/Space: enter Items level for current set
            bool enterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            bool spacePressed = InputManager.GetKeyDownAndConsume(KeyCode.Space);
            if (enterPressed || spacePressed)
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
                EnterItemsFromSetFilter();
                return;
            }

            // Backspace: return to Tabs
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                ReturnToTabs();
                return;
            }
        }

        private void CycleSetFilter(int direction)
        {
            if (_setFilterModels.Count == 0) return;

            int newIndex = _currentSetFilterIndex + direction;

            if (newIndex < 0)
            {
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }

            if (newIndex >= _setFilterModels.Count)
            {
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            SelectSetFilter(newIndex);
        }

        private void SelectSetFilter(int index)
        {
            _currentSetFilterIndex = index;

            // Call OnValueSelected on the StoreSetFilterToggles to trigger set change
            if (_onValueSelectedMethod != null && _setFilterTogglesComponent != null)
            {
                try
                {
                    _onValueSelectedMethod.Invoke(_setFilterTogglesComponent,
                        new object[] { _setFilterModels[_currentSetFilterIndex] });

                    // Wait for the set change to reload items (result announced in OnTabLoadComplete)
                    _waitingForTabLoad = true;
                    _waitingForSetChange = true;
                    _loadCheckTimer = TabLoadCheckInterval;
                }
                catch (Exception ex)
                {
                    Log.Msg("Store", $"Error selecting set filter: {ex.Message}");
                    AnnounceSetFilter();
                }
            }
            else
            {
                AnnounceSetFilter();
            }
        }

        private void AnnounceSetFilter()
        {
            if (_setFilterModels.Count == 0) return;

            string setName = GetSetFilterName(_currentSetFilterIndex);
            string announcement = Strings.StoreSetFilterPosition(setName,
                _currentSetFilterIndex + 1, _setFilterModels.Count);
            _announcer.AnnounceInterrupt(announcement);
        }

        private void EnterItemsFromSetFilter()
        {
            _navLevel = NavigationLevel.Items;
            DiscoverItems();

            if (_items.Count > 0)
            {
                _currentPurchaseOptionIndex = 0;
                PopulateLevelElements();
                AnnounceCurrentElement();
            }
            else
            {
                string setName = GetSetFilterName(_currentSetFilterIndex);
                _announcer.AnnounceInterrupt(Strings.NoItemsAvailable(setName));
                _navLevel = NavigationLevel.SetFilter;
                PopulateLevelElements();
            }
        }

        private void ReturnToSetFilter()
        {
            _isDetailsViewActive = false;
            _detailsCards.Clear();
            _detailsCardBlocks.Clear();
            _detailsDescription = null;
            _navLevel = NavigationLevel.SetFilter;
            _items.Clear();
            PopulateLevelElements();

            AnnounceSetFilter();
        }

        #endregion
    }
}
