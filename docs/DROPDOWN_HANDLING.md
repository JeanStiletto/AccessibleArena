# Dropdown Handling - Unified State Management

**Created:** 2026-02-03
**Updated:** 2026-02-03 (Stage 4.2 Complete)

---

## Overview

MTGA uses dropdowns for date selection (birthday, month/day/year pickers) and other settings. The accessibility mod needs to:

1. Detect when a dropdown is open (to hand off arrow key handling to Unity)
2. Handle explicit close (Escape/Backspace)
3. Sync navigator index after dropdown closes (game auto-advances to next field)
4. Prevent re-entry when closing auto-opened dropdowns

---

## Architecture (Post Stage 4.2)

### Single Source of Truth: DropdownStateManager

All dropdown state is now managed by `DropdownStateManager` (`src/Core/Services/DropdownStateManager.cs`).

**State:**
- `_wasInDropdownMode` - Tracks previous frame state for exit transition detection
- `_suppressReentry` - Prevents re-entry after closing auto-opened dropdowns
- `_activeDropdownObject` - Reference to currently active dropdown

**Public API:**
```csharp
// Query state
bool IsDropdownExpanded     // Real state from dropdown's IsExpanded property
bool IsInDropdownMode       // Takes suppression into account
GameObject ActiveDropdown   // Currently active dropdown

// Called by BaseNavigator each frame
bool UpdateAndCheckExitTransition()  // Returns true if just exited dropdown mode

// Called when user opens/closes dropdown
void OnDropdownOpened(GameObject dropdown)
string OnDropdownClosed()            // Returns new focus element name

// Called after closing auto-opened dropdown
void SuppressReentry()

// Utility
void Reset()                         // Clear all state
```

### Integration Points

**BaseNavigator.HandleInput():**
```csharp
// Check dropdown state and detect exit transitions
bool justExitedDropdown = DropdownStateManager.UpdateAndCheckExitTransition();

if (DropdownStateManager.IsInDropdownMode)
{
    HandleDropdownNavigation();
    return;
}

if (justExitedDropdown)
{
    SyncIndexToFocusedElement();
    return;
}
```

**BaseNavigator.CloseActiveDropdown():**
- Calls `DropdownStateManager.OnDropdownClosed()` after closing dropdown

**BaseNavigator.CloseDropdownOnElement():**
- Calls `DropdownStateManager.SuppressReentry()` after closing auto-opened dropdown

**UIFocusTracker:**
- `EnterDropdownEditMode()` delegates to `DropdownStateManager.OnDropdownOpened()`
- `ExitDropdownEditMode()` delegates to `DropdownStateManager.OnDropdownClosed()`
- `IsAnyDropdownExpanded()` and `GetExpandedDropdown()` remain for querying real dropdown state

---

## State Machines

### Scenario 1: Normal Dropdown Flow (User Opens/Closes)

```
[Normal Navigation]
    |
    v User presses Enter on dropdown OR game auto-opens dropdown
[Dropdown Opens]
    | IsDropdownExpanded = true
    | DropdownStateManager.IsInDropdownMode = true
    v
[Dropdown Mode]
    | HandleDropdownNavigation() handles Escape/Backspace
    | Arrow keys handled by Unity's dropdown
    v User presses Escape/Backspace
[Explicit Close]
    | CloseActiveDropdown() calls dropdown.Hide()
    | DropdownStateManager.OnDropdownClosed()
    v
[Next Frame]
    | UpdateAndCheckExitTransition() returns true
    | SyncIndexToFocusedElement() called
    v
[Normal Navigation]
```

### Scenario 2: Auto-Advance Flow (Date Picker)

```
[Normal Navigation]
    |
    v User selects Month dropdown value
[Value Selected]
    | Unity's dropdown handles Enter
    | Game auto-advances focus to Day dropdown
    | Game auto-opens Day dropdown
    v
[Next Frame]
    | IsDropdownExpanded = true (Day dropdown)
    | DropdownStateManager.IsInDropdownMode = true
    v
[Dropdown Mode - Day]
    | ... same flow continues
```

### Scenario 3: Auto-Opened Dropdown Suppression

```
[Normal Navigation]
    |
    v Navigator calls UpdateEventSystemSelection() to focus dropdown
[EventSystem Selection Set]
    | MTGA auto-opens the dropdown (side effect)
    | IsDropdownExpanded = true
    v
[Navigator Detects Auto-Open]
    | Navigator checks IsAnyDropdownExpanded() in UpdateEventSystemSelection()
    | Calls CloseDropdownOnElement()
    v
[CloseDropdownOnElement()]
    | dropdown.Hide()
    | DropdownStateManager.SuppressReentry()
    | _suppressReentry = true, _wasInDropdownMode = false
    v
[Next Frame(s)]
    | IsDropdownExpanded might STILL be true briefly
    | DropdownStateManager.IsInDropdownMode = false (suppression active)
    | UpdateAndCheckExitTransition() does not set _wasInDropdownMode
    v
[Eventually]
    | IsDropdownExpanded = false
    | _suppressReentry cleared
    v
[Normal Navigation]
```

---

## Key Design Decisions

### Why a Separate Manager Class?

Before Stage 4.2, dropdown state was tracked in two places:
- `BaseNavigator` - `_wasInDropdownMode`, `_skipDropdownModeTracking`
- `UIFocusTracker` - `_dropdownEditMode`, `_activeDropdownObject`, `_suppressDropdownModeEntry`

This caused:
1. Dual state tracking requiring coordinated suppression
2. Two parallel suppression mechanisms for the same problem
3. Complex coordination between classes

The unified `DropdownStateManager` solves this by:
1. Single source of truth for all dropdown state
2. One suppression mechanism (`_suppressReentry`)
3. Clear API for querying and modifying state

### Suppression Mechanism

The dropdown's `IsExpanded` property doesn't update immediately after `Hide()` is called.
Without suppression, this would cause:
1. Navigator closes auto-opened dropdown
2. Next frame: `IsExpanded` still true
3. System incorrectly enters dropdown mode
4. `SyncIndexToFocusedElement()` triggered when dropdown eventually closes

The `SuppressReentry()` method prevents this by:
1. Setting `_suppressReentry = true`
2. `IsInDropdownMode` returns false while suppression active (even if `IsExpanded` is true)
3. `_wasInDropdownMode` not set, so no exit transition detected
4. Suppression clears once `IsExpanded` actually becomes false

---

## Test Scenarios

1. **Normal dropdown navigation**
   - Arrow keys navigate dropdown items
   - Escape/Backspace closes dropdown
   - No double announcements

2. **Auto-advance (Month -> Day -> Year)**
   - Select Month value -> Day dropdown opens automatically
   - Navigator index syncs to new dropdown
   - Complete date selection works

3. **Auto-opened dropdown suppression**
   - Navigate to dropdown with arrow keys
   - Dropdown should NOT auto-open
   - Enter key should open dropdown

4. **Rapid navigation**
   - Quick navigation through multiple dropdowns
   - No stuck states or missed transitions

---

## File References

- `src/Core/Services/DropdownStateManager.cs` - Unified state manager (NEW)
- `src/Core/Services/BaseNavigator.cs` - HandleInput uses DropdownStateManager
- `src/Core/Services/UIFocusTracker.cs` - Delegates to DropdownStateManager, provides IsAnyDropdownExpanded()
