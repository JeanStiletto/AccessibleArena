# StoreNavigator.SetFilter.cs
Path: src/Core/Services/StoreNavigator/StoreNavigator.SetFilter.cs
Lines: 230

## Top-level comments
- Feature partial for the Packs tab set filter level: discovers StoreSetFilterToggles models, handles fully-custom Left/Right/Up/Down/Tab/Home/End cycling through sets, triggers set change via OnValueSelected reflection, and transitions to Items level on Enter/Space.

## public partial class StoreNavigator (line 8)

### Fields
(no fields declared in this partial)

### Methods
- private void DiscoverSetFilters() (line 12) — reads _setFilters list and SelectedIndex from StoreSetFilterToggles component
- private string GetSetFilterName(int index) (line 56) — resolves SetSymbol via UITextExtractor.MapSetCodeToName
- private void HandleSetFilterInput() (line 78) — hold-to-repeat arrow/Tab cycling, Home/End jumps, Enter/Space to enter Items, Backspace to tabs
- private void CycleSetFilter(int direction) (line 135) — announces beginning/end at boundaries
- private void SelectSetFilter(int index) (line 156) — invokes StoreSetFilterToggles.OnValueSelected, sets _waitingForSetChange flag
- private void AnnounceSetFilter() (line 185)
- private void EnterItemsFromSetFilter() (line 195) — transitions to Items level, falls back to SetFilter if no items
- private void ReturnToSetFilter() (line 215) — clears details state and re-announces set filter
