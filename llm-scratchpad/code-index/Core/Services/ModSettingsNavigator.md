# ModSettingsNavigator.cs
Path: src/Core/Services/ModSettingsNavigator.cs
Lines: 456

## Top-level comments
- Modal settings menu navigator. When active, blocks all other input and allows navigating and toggling mod settings with Up/Down arrows. Language setting uses dropdown-like behavior (Enter opens, Left/Right browse, Enter confirms, Escape/Backspace cancels). Closes with Backspace or F2.

## public class ModSettingsNavigator (line 17)

### Fields
- private readonly IAnnouncementService _announcer (line 19)
- private readonly ModSettings _settings (line 20)
- private readonly List<SettingItem> _items (line 21)
- private int _currentIndex (line 22)
- private bool _isActive (line 23)
- private bool _isInDropdownMode (line 26)
- private int _dropdownLanguageIndex (line 27)
- private string _originalLanguageCode (line 28)

### Properties
- public bool IsActive => _isActive (line 30)

### Nested Types
- private class SettingItem (line 49)
  - public string Name { get; set; } (line 51)
  - public Func<string> GetValue { get; set; } (line 52)
  - public Action Toggle { get; set; } (line 53)
  - public bool IsDropdown { get; set; } (line 55)
  - public string Description { get; set; } (line 57)

### Methods
- public ModSettingsNavigator(IAnnouncementService announcer, ModSettings settings) (line 32)
- private List<SettingItem> BuildSettingItems() (line 60)
- public void Toggle() (line 139)
- public void Open() (line 150)
- public void Close() (line 166) — Saves settings on close; cancels dropdown first if open
- public bool HandleInput() (line 187)
- private void ActivateCurrentSetting() (line 244)
- private void EnterDropdownMode() (line 266)
- private void HandleDropdownInput() (line 280)
- private void CycleDropdown(int direction) (line 325)
- private void JumpDropdown(int index) (line 345)
- private void AnnounceDropdownItem() (line 357)
- private void ConfirmDropdown() (line 365) — Applies language change via SetLanguage
- private void CancelDropdown() (line 378) — Restores original language index without applying
- private void MoveNext() (line 389)
- private void MovePrevious() (line 404)
- private void MoveFirst() (line 419)
- private void MoveLast() (line 431)
- private void AnnounceCurrentItem() (line 444)
