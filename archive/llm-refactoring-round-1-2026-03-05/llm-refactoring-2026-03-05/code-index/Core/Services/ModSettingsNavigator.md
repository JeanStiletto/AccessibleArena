# ModSettingsNavigator.cs

## Summary
Modal settings menu navigator. When active, blocks all other input and allows navigating and toggling mod settings with Up/Down arrows. Language setting uses dropdown-like behavior (Enter to open, Left/Right to browse, Enter to confirm, Escape/Backspace to cancel). Closes with Backspace or F2.

## Classes

### ModSettingsNavigator (line 17)
```
public class ModSettingsNavigator
  private readonly IAnnouncementService _announcer (line 19)
  private readonly ModSettings _settings (line 20)
  private readonly List<SettingItem> _items (line 21)
  private int _currentIndex (line 22)
  private bool _isActive (line 23)

  // Dropdown state for language picker
  private bool _isInDropdownMode (line 26)
  private int _dropdownLanguageIndex (line 27)
  private string _originalLanguageCode (line 28)

  public bool IsActive => _isActive (line 30)

  public ModSettingsNavigator(IAnnouncementService announcer, ModSettings settings) (line 32)

  // Defines a single setting item in the menu
  private class SettingItem (line 49)
    public string Name { get; set; } (line 51)
    public Func<string> GetValue { get; set; } (line 52)
    public Action Toggle { get; set; } (line 53)
    public bool IsDropdown { get; set; } (line 55)

  private List<SettingItem> BuildSettingItems() (line 58)
  public void Toggle() (line 93)
  public void Open() (line 104)
  public void Close() (line 120)
  public bool HandleInput() (line 141)
  private void ActivateCurrentSetting() (line 198)

  // Dropdown Mode (Language Picker)
  private void EnterDropdownMode() (line 220)
  private void HandleDropdownInput() (line 234)
  private void CycleDropdown(int direction) (line 281)
  private void JumpDropdown(int index) (line 301)
  private void AnnounceDropdownItem() (line 313)
  private void ConfirmDropdown() (line 321)
  private void CancelDropdown() (line 334)

  // Navigation
  private void MoveNext() (line 345)
  private void MovePrevious() (line 360)
  private void MoveFirst() (line 375)
  private void MoveLast() (line 387)
  private void AnnounceCurrentItem() (line 400)
```
