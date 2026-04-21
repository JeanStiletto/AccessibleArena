# AdvancedFiltersNavigator.cs
Path: src/Core/Services/AdvancedFiltersNavigator.cs
Lines: 798

## public class AdvancedFiltersNavigator : BaseNavigator (line 17)

### Nested Types
- private struct FilterRow (line 37) — Name (string), Items (List<FilterItem>)
- private struct FilterItem (line 43) — GameObject, Label, IsToggle, IsDropdown, ToggleComponent (Toggle)

### Fields
- private readonly List<FilterRow> _rows (line 24)
- private int _currentRowIndex = -1 (line 25)
- private int _currentItemIndex = -1 (line 26)
- private GameObject _popup (line 29)
- private bool _lastPopupState = false (line 32)
- private bool _wasInDropdownMode = false (line 35)

### Properties
- public override string NavigatorId => "AdvancedFilters" (line 19)
- public override string ScreenName => Strings.ScreenAdvancedFilters (line 20)
- public override int Priority => 87 (line 21) — higher than RewardPopup (86), below SettingsMenu (90)

### Methods
- public AdvancedFiltersNavigator(IAnnouncementService announcer) : base(announcer) (line 52)
- protected override bool DetectScreen() (line 56)
- protected override void DiscoverElements() (line 85)
- private void SortRowByPosition(FilterRow row) (line 280)
- private string GetPath(Transform t) (line 288)
- private string GetToggleLabel(Toggle toggle) (line 299)
- private string GetButtonLabel(GameObject button) (line 315)
- public override string GetTutorialHint() (line 336)
- protected override string GetActivationAnnouncement() (line 338)
- protected override void HandleInput() (line 359)
- private void MoveToPreviousRow() (line 445)
- private void MoveToNextRow() (line 461)
- private void MoveToPreviousItem() (line 477)
- private void MoveToNextItem() (line 493)
- private void MoveToFirstItemInRow() (line 509)
- private void MoveToLastItemInRow() (line 517)
- protected override bool HandleLetterNavigation(KeyCode key) (line 529) — searches within current row's items
- private bool IsValidPosition() (line 556)
- private void AnnounceCurrentPosition(bool includeRowName) (line 564)
- private void ActivateCurrentItem() (line 591)
- private void ClosePopup() (line 640)
- private GameObject FindBlockerOrBackground() (line 682)
- private GameObject FindCloseButton() (line 720)
- private GameObject FindOkButton() (line 742)
- protected override void OnActivated() (line 768)
- protected override bool ValidateElements() (line 773)
- public override void OnSceneChanged(string sceneName) (line 784)
