# BaseNavigator.Dropdowns.cs
Path: src/Core/Services/BaseNavigator/BaseNavigator.Dropdowns.cs
Lines: 388

## Top-level comments
- Dropdown navigation and state management. Handles open/close, item selection (silent to bypass onValueChanged), arrow key passthrough, Tab to navigate away.
- Detects auto-opened dropdowns (from arrow navigation) and closes them immediately; supports TMP_Dropdown, legacy Dropdown, and custom cTMP_Dropdown.

## public partial class BaseNavigator (line 20)
### Methods
- protected virtual void HandleDropdownNavigation() (line 29) — route Tab/Shift+Tab/Escape/Backspace/Enter
- private void SelectDropdownItem() (line 114)
- public static void SelectCurrentDropdownItem(string callerId) (line 120) — parse item index, set silent
- private enum DropdownKind { None, TMP, Legacy, Custom } (line 168)
- private static (DropdownKind kind, Component component) ResolveDropdown(GameObject obj) (line 174)
- private static bool HideDropdownComponent(DropdownKind kind, Component component) (line 196)
- public static bool SetDropdownValueSilent(GameObject dropdownObj, int itemIndex) (line 226)
- public static string GetDropdownDisplayValue(GameObject dropdownObj) (line 253) — read caption text
- private static string GetDropdownFirstOptionFallback(GameObject dropdownObj) (line 295)
- private void CloseActiveDropdown(bool silent) (line 336)
- public static void CloseDropdown(string callerId, IAnnouncementService announcer, bool silent) (line 342)
