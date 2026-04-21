# StoreNavigator.Tabs.cs
Path: src/Core/Services/StoreNavigator/StoreNavigator.Tabs.cs
Lines: 309

## Top-level comments
- Feature partial for tab-level navigation: discovers store tabs from controller fields, queries/activates the current tab, monitors tab-load state via the controller's item display queue, and handles return-to-tabs back navigation.

## public partial class StoreNavigator (line 10)

### Fields
(no fields declared in this partial)

### Methods
- private void DiscoverTabs() (line 14) — reads each tab field on controller, builds TabInfo entries with localized names; calls DiscoverUtilityElements afterward
- private int FindActiveTabIndex() (line 63) — compares _currentTab field on controller against discovered TabInfo.TabComponent
- private string FormatTabAnnouncement(int index) (line 84) — appends ", active" for active tab; utility entries formatted as "{name}, button"
- private bool IsTabActive(TabInfo tab) (line 99)
- private void ActivateCurrentTab() (line 116) — handles utility activation, re-entering current tab → SetFilter/Items, or clicking new tab via OnClicked reflection
- private bool IsLoadingComplete() (line 198) — checks _itemDisplayQueue.Count == 0 on controller
- private void OnTabLoadComplete() (line 220) — post-load handler: routes to SetFilter for Packs tab, or discovers items; handles _waitingForSetChange path
- private void ReturnToTabs() (line 288) — clears details state, rebuilds tabs, announces tab count
