# StoreNavigator.Utility.cs
Path: src/Core/Services/StoreNavigator/StoreNavigator.Utility.cs
Lines: 174

## Top-level comments
- Feature partial for the utility entries appended after the tab list: payment button, redeem code input, drop rates link, and pack progress meter. Handles discovery + activation (including Steam payment no-op and info-only pack progress).

## public partial class StoreNavigator (line 9)

### Fields
(no fields declared in this partial)

### Methods
- private void DiscoverUtilityElements() (line 13) — appends payment, redeem code, drop rates, and pack progress entries to _tabs
- private void AddPackProgressElement() (line 54) — finds PackProgressMeter_Desktop_16x9(Clone) by name, extracts Text_GoalNumber + Text_Title
- private void AddUtilityElement(FieldInfo field, string fallbackName) (line 101)
- private void ActivateUtilityElement(TabInfo tab) (line 132) — Steam check for payment button, re-announce for pack progress, UIActivator for others
