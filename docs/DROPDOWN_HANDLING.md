# Dropdown Handling - Unified State Management

**Created:** 2026-02-03
**Updated:** 2026-02-19 (Enter blocking and silent selection)

---

## Overview

MTGA uses dropdowns for date selection (birthday, month/day/year pickers) and other settings. The accessibility mod needs to:

1. Detect when a dropdown is open (to hand off arrow key handling to Unity)
2. Handle explicit close (Escape/Backspace)
3. Sync navigator index after dropdown closes
4. Prevent re-entry when closing auto-opened dropdowns
5. Block Enter/Submit from the game while in dropdown mode
6. Select items without triggering onValueChanged (prevents chain auto-advance)

---

## Architecture

### Single Source of Truth: DropdownStateManager

All dropdown state is managed by `DropdownStateManager` (`src/Core/Services/DropdownStateManager.cs`).

**State:**
- `_wasInDropdownMode` - Tracks previous frame state for exit transition detection
- `_suppressReentry` - Prevents re-entry after closing auto-opened dropdowns
- `_activeDropdownObject` - Reference to currently active dropdown
- `_blockEnterFromGame` - Persistent flag blocking Enter from game's KeyboardManager and Unity's EventSystem Submit
- `_blockSubmitAfterFrame` - Frame-based Submit blocking window after dropdown item selection or close

**Public API:**
```csharp
// Query state
bool IsDropdownExpanded     // Real state from dropdown's IsExpanded property
bool IsInDropdownMode       // Takes suppression into account
bool ShouldBlockEnterFromGame // True while dropdown is open (persistent across frames)
GameObject ActiveDropdown   // Currently active dropdown

// Called by BaseNavigator each frame
bool UpdateAndCheckExitTransition()  // Returns true if just exited dropdown mode

// Called when user opens/closes dropdown
void OnDropdownOpened(GameObject dropdown)  // Sets _blockEnterFromGame = true
string OnDropdownClosed()            // Returns new focus element name, clears blocking

// Called after closing auto-opened dropdown
void SuppressReentry()

// Called when Enter selects a dropdown item
void OnDropdownItemSelected()        // Starts Submit-blocking window

// Post-selection Submit blocking
bool ShouldBlockSubmit()             // True for 3 frames after item selection

// Utility
void Reset()                         // Clear all state
```

### Enter/Submit Blocking

The mod fully handles Enter key presses in dropdown mode. The game never sees Enter while a dropdown is open:

1. **KeyboardManagerPatch** - Blocks Enter from `MTGA.KeyboardManager.PublishKeyDown` when `ShouldBlockEnterFromGame` is true
2. **EventSystemPatch** - Blocks `SendSubmitEventToSelectedObject` when `ShouldBlockEnterFromGame` is true
3. **Post-close blocking** - `ShouldBlockSubmit()` blocks Submit for 3 frames after dropdown close to prevent auto-clicking the next focused element

### Silent Item Selection (BaseNavigator)

When the user presses Enter on a dropdown item, the mod selects it without triggering `onValueChanged`:

- **`SelectDropdownItem()`** - Parses item index from the item name ("Item N: ..."), calls `SetDropdownValueSilent()`
- **`SetDropdownValueSilent()`** - Sets the value via reflection:
  - `TMP_Dropdown` / `Dropdown`: Uses `SetValueWithoutNotify()`
  - `cTMP_Dropdown` (MTGA custom): Sets `m_Value` field directly + calls `RefreshShownValue()` (no `SetValueWithoutNotify` available)
- The dropdown stays open after selection - the user must press Escape/Backspace to close

This prevents the chain auto-advance problem (Month -> Day -> Year) where `onValueChanged` would trigger the game to auto-open the next dropdown.

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
    AnnounceCurrentElement();
    return;
}
```

**BaseNavigator.HandleDropdownNavigation():**
- Enter: Calls `SelectDropdownItem()` (select without closing, announces "Selected")
- Escape/Backspace: Calls `CloseActiveDropdown()` (closes dropdown, syncs focus)
- All Enter key codes consumed via `InputManager.ConsumeKey()`

**BaseNavigator.CloseActiveDropdown():**
- Calls `DropdownStateManager.OnDropdownClosed()` after closing dropdown

**BaseNavigator.CloseDropdownOnElement():**
- Calls `DropdownStateManager.SuppressReentry()` after closing auto-opened dropdown

**EventSystemPatch:**
- `SendSubmitEventToSelectedObject_Prefix` returns false when `ShouldBlockEnterFromGame`

**KeyboardManagerPatch:**
- `ShouldBlockKey()` returns true for Enter when `ShouldBlockEnterFromGame`

**UIFocusTracker:**
- `EnterDropdownEditMode()` delegates to `DropdownStateManager.OnDropdownOpened()`
- `ExitDropdownEditMode()` delegates to `DropdownStateManager.OnDropdownClosed()`
- `IsAnyDropdownExpanded()` and `GetExpandedDropdown()` remain for querying real dropdown state

---

## State Machines

### Scenario 1: Normal Dropdown Flow (User Opens, Selects, Closes)

```
[Normal Navigation]
    |
    v User presses Enter on dropdown
[Dropdown Opens]
    | IsDropdownExpanded = true
    | DropdownStateManager.IsInDropdownMode = true
    | _blockEnterFromGame = true (Enter blocked from game)
    v
[Dropdown Mode]
    | Arrow keys handled by Unity's dropdown
    | Enter: SelectDropdownItem() sets value silently, announces "Selected"
    |        Dropdown stays open for further browsing
    v User presses Escape/Backspace
[Explicit Close]
    | CloseActiveDropdown() calls dropdown.Hide()
    | DropdownStateManager.OnDropdownClosed()
    | _blockEnterFromGame = false
    | Submit blocked for 3 frames
    v
[Next Frame]
    | UpdateAndCheckExitTransition() returns true
    | SyncIndexToFocusedElement() called
    v
[Normal Navigation]
```

### Scenario 2: Auto-Opened Dropdown Suppression

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

### Why Block Enter from the Game?

MTGA has multiple ways of detecting Enter:
1. Unity's EventSystem Submit (`SendSubmitEventToSelectedObject`)
2. Game's `KeyboardManager.PublishKeyDown`
3. Direct `Input.GetKeyDown` calls

All three must be blocked while in dropdown mode. The `_blockEnterFromGame` flag is set when entering dropdown mode and persists until our `Update()` processes the exit transition. This is necessary because `EventSystem.Process()` runs before our `Update()` and may close the dropdown before `PublishKeyDown` is called.

### Why Silent Value Setting?

MTGA's `cTMP_Dropdown` (custom dropdown class) has no `SetValueWithoutNotify()`. Its `value` setter always fires `onValueChanged`, which triggers the game's auto-advance chain (Month -> Day -> Year -> Country -> Experience). By setting `m_Value` directly via reflection and calling `RefreshShownValue()`, we update the visual state without triggering any callbacks.

### Why Keep Dropdown Open After Selection?

The user should control navigation flow. After selecting a dropdown item, they may want to:
- Verify the selection
- Change to a different item
- Close the dropdown on their own terms with Escape/Backspace

### Why a Separate Manager Class?

Before the unified manager, dropdown state was tracked in two places (BaseNavigator and UIFocusTracker). This caused dual state tracking, two parallel suppression mechanisms, and complex coordination. The unified `DropdownStateManager` provides a single source of truth.

### Suppression Mechanism

The dropdown's `IsExpanded` property doesn't update immediately after `Hide()` is called.
Without suppression, this would cause the system to incorrectly enter dropdown mode on the next frame. `SuppressReentry()` prevents this until `IsExpanded` actually becomes false.

---

## Test Scenarios

1. **Normal dropdown navigation**
   - Arrow keys navigate dropdown items
   - Enter selects item (announced as "Selected"), dropdown stays open
   - Escape/Backspace closes dropdown
   - No double announcements

2. **Multiple selections**
   - Open dropdown, select item with Enter, use arrows to browse more, select again
   - Dropdown stays open throughout
   - Each selection announced

3. **Auto-opened dropdown suppression**
   - Navigate to dropdown with arrow keys
   - Dropdown should NOT auto-open
   - Enter key should open dropdown

4. **Post-close navigation**
   - After closing dropdown, Tab/arrow navigation works correctly
   - Navigator index syncs to the element that has focus
   - No Submit auto-click on next focused element

5. **Registration date pickers**
   - Selecting Month value does NOT auto-open Day dropdown
   - User must manually navigate to Day and press Enter

---

## File References

- `src/Core/Services/DropdownStateManager.cs` - Unified state manager
- `src/Core/Services/BaseNavigator.cs` - HandleDropdownNavigation, SelectDropdownItem, SetDropdownValueSilent
- `src/Patches/EventSystemPatch.cs` - Blocks SendSubmitEventToSelectedObject in dropdown mode
- `src/Patches/KeyboardManagerPatch.cs` - Blocks Enter from game's KeyboardManager in dropdown mode
- `src/Core/Services/UIFocusTracker.cs` - Delegates to DropdownStateManager, provides IsAnyDropdownExpanded()
