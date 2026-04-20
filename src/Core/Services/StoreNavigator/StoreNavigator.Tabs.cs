using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Models;
using System;
using System.Linq;
using System.Reflection;

namespace AccessibleArena.Core.Services
{
    public partial class StoreNavigator
    {
        #region Tab Discovery

        private void DiscoverTabs()
        {
            _tabs.Clear();

            if (_controller == null || _tabFields == null) return;

            for (int i = 0; i < _tabFields.Length; i++)
            {
                if (_tabFields[i] == null) continue;

                try
                {
                    var tabObj = _tabFields[i].GetValue(_controller);
                    if (tabObj == null) continue;

                    var tabMb = tabObj as MonoBehaviour;
                    if (tabMb == null || tabMb.gameObject == null || !tabMb.gameObject.activeInHierarchy)
                        continue;

                    // Read localized tab name from the Localize component; fall back to English
                    string tabName = UITextExtractor.GetText(tabMb.gameObject);
                    if (string.IsNullOrWhiteSpace(tabName))
                        tabName = TabDisplayNames[i];

                    _tabs.Add(new TabInfo
                    {
                        TabComponent = tabMb,
                        GameObject = tabMb.gameObject,
                        DisplayName = tabName,
                        FieldIndex = i,
                        IsUtility = false
                    });
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[Store] Error reading tab {TabFieldNames[i]}: {ex.Message}");
                }
            }

            // Add utility elements after tabs (only if visible)
            DiscoverUtilityElements();

            MelonLogger.Msg($"[Store] Discovered {_tabs.Count} entries (tabs + utility)");
        }

        #endregion

        #region Tab Queries

        private int FindActiveTabIndex()
        {
            if (_controller == null || _currentTabField == null || _storeTabTypeLookupField == null)
                return -1;

            try
            {
                var currentTab = _currentTabField.GetValue(_controller);
                if (currentTab == null) return -1;

                for (int i = 0; i < _tabs.Count; i++)
                {
                    if (_tabs[i].TabComponent == (MonoBehaviour)currentTab)
                        return i;
                }
            }
            catch { /* Reflection may fail on different game versions */ }

            return -1;
        }

        private string FormatTabAnnouncement(int index)
        {
            if (index < 0 || index >= _tabs.Count) return "";

            var tab = _tabs[index];
            if (tab.IsUtility)
                return $"{tab.DisplayName}, button";

            int tabCount = _tabs.Count(t => !t.IsUtility);
            bool isActive = IsTabActive(tab);
            string activeIndicator = isActive ? ", active" : "";
            return Strings.TabPositionOf(index + 1, tabCount,
                $"{tab.DisplayName}{activeIndicator}");
        }

        private bool IsTabActive(TabInfo tab)
        {
            if (_currentTabField == null || _controller == null) return false;

            try
            {
                var currentTab = _currentTabField.GetValue(_controller);
                if (currentTab == null) return false;
                return (MonoBehaviour)currentTab == tab.TabComponent;
            }
            catch { return false; }
        }

        #endregion

        #region Tab Activation

        private void ActivateCurrentTab()
        {
            if (_currentIndex < 0 || _currentIndex >= _tabs.Count) return;

            var tab = _tabs[_currentIndex];

            // Utility entries are activated directly (not store tabs)
            if (tab.IsUtility)
            {
                MelonLogger.Msg($"[Store] Activating utility: {tab.DisplayName}");
                ActivateUtilityElement(tab);
                return;
            }

            // Check if this tab is already active - enter items or set filter directly
            if (IsTabActive(tab))
            {
                // Packs tab with set filters: enter SetFilter level
                if (IsPacksTab(tab))
                {
                    DiscoverSetFilters();
                    if (_setFilterModels.Count > 0)
                    {
                        _navLevel = NavigationLevel.SetFilter;
                        PopulateLevelElements();
                        _currentSetFilterIndex = 0;
                        AnnounceSetFilter();
                        return;
                    }
                }

                _navLevel = NavigationLevel.Items;
                DiscoverItems();

                if (_items.Count > 0)
                {
                    _currentPurchaseOptionIndex = 0;
                    PopulateLevelElements();
                    _announcer.AnnounceInterrupt(Strings.TabItems(tab.DisplayName, _items.Count));
                    AnnounceCurrentElement();
                }
                else
                {
                    _announcer.AnnounceInterrupt(Strings.NoItemsAvailable(tab.DisplayName));
                    _navLevel = NavigationLevel.Tabs;
                    PopulateLevelElements();
                }
                return;
            }

            // Activate the tab via OnClicked()
            MelonLogger.Msg($"[Store] Activating tab: {tab.DisplayName}");

            if (_tabOnClickedMethod != null)
            {
                try
                {
                    _tabOnClickedMethod.Invoke(tab.TabComponent, null);
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[Store] Error calling OnClicked: {ex.Message}");
                    // Fallback: try UIActivator
                    UIActivator.Activate(tab.GameObject);
                }
            }
            else
            {
                UIActivator.Activate(tab.GameObject);
            }

            _announcer.AnnounceInterrupt(Strings.Loading(tab.DisplayName));

            // Start waiting for items to load
            _waitingForTabLoad = true;
            _loadCheckTimer = TabLoadCheckInterval;
        }

        #endregion

        #region Loading Detection

        private bool IsLoadingComplete()
        {
            if (_controller == null || _itemDisplayQueueField == null) return true;

            try
            {
                var queue = _itemDisplayQueueField.GetValue(_controller);
                if (queue == null) return true;

                // Queue<T> has a Count property
                var countProp = queue.GetType().GetProperty("Count");
                if (countProp == null) return true;

                int count = (int)countProp.GetValue(queue);
                return count == 0;
            }
            catch
            {
                return true;
            }
        }

        private void OnTabLoadComplete()
        {
            _waitingForTabLoad = false;
            MelonLogger.Msg("[Store] Tab load complete");

            // Refresh tab discovery in case tabs changed
            DiscoverTabs();
            int activeTab = FindActiveTabIndex();

            // If this was a set filter change, stay at SetFilter level
            if (_waitingForSetChange)
            {
                _waitingForSetChange = false;
                DiscoverItems();
                int itemCount = _items.Count;
                string setName = GetSetFilterName(_currentSetFilterIndex);
                _announcer.AnnounceInterrupt(Strings.StoreSetFilterItems(setName, itemCount));
                _navLevel = NavigationLevel.SetFilter;
                PopulateLevelElements();
                _items.Clear();
                return;
            }

            // Packs tab: enter SetFilter level instead of Items
            if (activeTab >= 0 && activeTab < _tabs.Count && IsPacksTab(_tabs[activeTab]))
            {
                DiscoverSetFilters();
                if (_setFilterModels.Count > 0)
                {
                    _navLevel = NavigationLevel.SetFilter;
                    PopulateLevelElements();
                    _currentSetFilterIndex = 0;
                    AnnounceSetFilter();
                    return;
                }
            }

            // Discover items for the new tab
            _navLevel = NavigationLevel.Items;
            DiscoverItems();

            if (_items.Count > 0)
            {
                _currentPurchaseOptionIndex = 0;
                PopulateLevelElements();

                string tabName = (activeTab >= 0 && activeTab < _tabs.Count)
                    ? _tabs[activeTab].DisplayName : "Store";

                _announcer.AnnounceInterrupt(Strings.TabItems(tabName, _items.Count));
                AnnounceCurrentElement();
            }
            else
            {
                string tabName = (activeTab >= 0 && activeTab < _tabs.Count)
                    ? _tabs[activeTab].DisplayName : "tab";

                _announcer.AnnounceInterrupt(Strings.TabNoItems(tabName));
                _navLevel = NavigationLevel.Tabs;
                PopulateLevelElements();
                _currentIndex = activeTab >= 0 ? activeTab : 0;
            }
        }

        #endregion

        #region Back Navigation

        private void ReturnToTabs()
        {
            _isDetailsViewActive = false;
            _detailsCards.Clear();
            _detailsCardBlocks.Clear();
            _detailsDescription = null;
            _navLevel = NavigationLevel.Tabs;
            _items.Clear();

            // Refresh tabs in case something changed
            DiscoverTabs();
            PopulateLevelElements();
            int activeTab = FindActiveTabIndex();
            _currentIndex = (activeTab >= 0 && activeTab < _elements.Count) ? activeTab : 0;

            _announcer.AnnounceInterrupt(Strings.TabsCount(_tabs.Count(t => !t.IsUtility)));
            AnnounceCurrentElement();
        }

        #endregion
    }
}
