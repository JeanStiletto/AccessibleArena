# DropdownStateManager.cs

Unified dropdown state management. Single source of truth for dropdown mode tracking. The real dropdown state is determined by IsExpanded property on the dropdown component.

## static class DropdownStateManager (line 22)

### Private Fields - State (line 24)
- _wasInDropdownMode (bool) (line 30)
- _suppressReentry (bool) (line 37)
- _activeDropdownObject (GameObject) (line 42)
- _blockSubmitAfterFrame (int) (line 48)
- _blockEnterFromGame (bool) (line 56)
- _savedOnValueChanged (object) (line 64)
- _suppressedDropdownComponent (Component) (line 68)
- _cachedOnValueChangedField (FieldInfo) (line 74)
- _pendingNotifyValue (int) (line 81) - Note: -1 means no pending notification

### Public Properties (line 85)
- IsDropdownExpanded → bool (line 91) - Note: queries actual IsExpanded property
- IsInDropdownMode → bool (line 97) - Note: takes into account suppression
- ShouldBlockEnterFromGame → bool (line 118)
- ActiveDropdown → GameObject (line 123)
- IsSuppressed → bool (line 130)

### Public Methods (line 134)
- UpdateAndCheckExitTransition() → bool (line 140) - Note: returns true if just exited dropdown mode
- ShouldBlockSubmit() → bool (line 196) - Note: blocks Submit for 3 frames after item selection
- OnDropdownItemSelected(int) (line 207) - Note: stores value for pending onValueChanged
- OnDropdownOpened(GameObject) (line 219)
- OnDropdownClosed() → string (line 237) - Note: returns name of element with focus
- SuppressReentry() (line 278) - Note: called after closing auto-opened dropdown
- Reset() (line 293)

### Private Methods - onValueChanged Suppression (line 307)
- SuppressOnValueChanged(GameObject) (line 315) - Note: replaces with empty event to prevent form auto-advance
- RestoreOnValueChanged() (line 368)
- FireOnValueChanged(Component, int) (line 419) - Note: invokes onValueChanged after restore
- GetOnValueChangedField(Type) → FieldInfo (line 466) - Note: cached by type
