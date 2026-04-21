# DropdownEditHelper.cs
Path: src/Core/Services/DropdownEditHelper.cs
Lines: 242

## Top-level comments
- Shared dropdown edit-mode helper used by BaseNavigator popup mode. Opens the dropdown, tracks edit state, focuses the first item when value=-1, handles Tab/Escape/Backspace/Enter/arrow keys, and special-cases single-item dropdowns so arrow keys don't escape focus.

## public class DropdownEditHelper (line 16)
### Fields
- private readonly IAnnouncementService _announcer (line 18)
- private readonly string _navigatorId (line 19)
- private GameObject _editingDropdown (line 21)
- private bool _needsInitialFocus (line 22)
- private int _itemCount = -1 (line 25)
- private GameObject _firstItemObject (line 26)
### Properties
- public bool IsEditing (line 28)
- public GameObject EditingDropdown (line 29)
### Methods
- public DropdownEditHelper(IAnnouncementService announcer, string navigatorId) (line 31)
- public void EnterEditMode(GameObject dropdown) (line 40)
- public bool HandleEditing(Action<int> onTabNavigate) (line 57) — Note: auto-exits when DropdownStateManager detects external close; Tab invokes onTabNavigate with direction, Backspace is consumed so popup doesn't dismiss
- public void Clear() (line 148)
- private void ClearState() (line 157)
- private void TryFocusFirstItem() (line 171) — Note: retried every frame until Unity's dropdown list spawns its Toggle items; also caches item count
- private void CountItems() (line 214)
- private static string ExtractItemText(string itemName) (line 235) — Note: strips "Item N: " prefix from Unity dropdown child names
