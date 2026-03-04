# AdvancedFiltersNavigator.cs

Navigator for the Advanced Filters popup in Collection/Deck Builder.
Provides grid-based navigation: Up/Down switches rows, Left/Right navigates within row.

## Class: AdvancedFiltersNavigator : BaseNavigator (line 17)

### Properties
- string NavigatorId => "AdvancedFilters" (line 19)
- string ScreenName => Strings.ScreenAdvancedFilters (line 20)
- int Priority => 87 (line 21)

### Fields
- readonly List<FilterRow> _rows (line 24)
- int _currentRowIndex (line 25)
- int _currentItemIndex (line 26)
- GameObject _popup (line 29)
- bool _lastPopupState (line 32)

### Structs
- struct FilterRow (line 34)
  - string Name (line 36)
  - List<FilterItem> Items (line 37)

- struct FilterItem (line 40)
  - GameObject GameObject (line 42)
  - string Label (line 43)
  - bool IsToggle (line 44)
  - bool IsDropdown (line 45)
  - Toggle ToggleComponent (line 46)

### Constructor
- AdvancedFiltersNavigator(IAnnouncementService announcer) (line 49)

### Detection Methods
- protected override bool DetectScreen() (line 53)

### Discovery Methods
- protected override void DiscoverElements() (line 82)
- void SortRowByPosition(FilterRow row) (line 237)
- string GetPath(Transform t) (line 245)
- string GetToggleLabel(Toggle toggle) (line 256)
- string GetDropdownLabel(TMP_Dropdown dropdown) (line 272)
- string GetButtonLabel(GameObject button) (line 280)

### Navigation Methods
- protected override string GetActivationAnnouncement() (line 301)
- protected override void HandleInput() (line 307)
- void MoveToPreviousRow() (line 386)
- void MoveToNextRow() (line 404)
- void MoveToPreviousItem() (line 422)
- void MoveToNextItem() (line 438)
- void MoveToFirstItemInRow() (line 454)
- void MoveToLastItemInRow() (line 462)
- bool IsValidPosition() (line 471)
- void AnnounceCurrentPosition(bool includeRowName) (line 479)
- void ActivateCurrentItem() (line 504)
- void CloseActiveDropdownInternal() (line 553)
- void ClosePopup() (line 578)

### Helper Methods
- GameObject FindBlockerOrBackground() (line 620)
- GameObject FindCloseButton() (line 658)
- GameObject FindOkButton() (line 680)

### Lifecycle Methods
- protected override void OnActivated() (line 706)
- protected override bool ValidateElements() (line 718)
- public override void OnSceneChanged(string sceneName) (line 729)
