# DropdownEditHelper.cs

Shared dropdown editing logic used by BaseNavigator popup mode and other contexts. Thin wrapper that manages edit state and routes keys to BaseNavigator's static dropdown methods.

## class DropdownEditHelper (line 16)

### Private Fields
- _announcer (IAnnouncementService) (line 18)
- _navigatorId (string) (line 19)
- _editingDropdown (GameObject) (line 21)
- _needsInitialFocus (bool) (line 22)
- _itemCount (int) (line 25) - Cached after TryFocusFirstItem discovers items
- _firstItemObject (GameObject) (line 26)

### Public Properties
- IsEditing → bool (line 28)
- EditingDropdown → GameObject (line 29)

### Constructor
- DropdownEditHelper(IAnnouncementService, string) (line 31)

### Public Methods
- EnterEditMode(GameObject) (line 40) - Activates dropdown, registers with DropdownStateManager
- HandleEditing(Action<int>) → bool (line 57) - Note: Arrow keys pass through to Unity
- Clear() (line 144) - Full reset when popup closes or navigator deactivates

### Private Methods
- ClearState() (line 153)
- TryFocusFirstItem() (line 167) - Note: handles value=-1 case, counts items for single-item handling
- CountItems() (line 210)
- ExtractItemText(string) → string (line 231) - Note: removes "Item 0: " prefix
