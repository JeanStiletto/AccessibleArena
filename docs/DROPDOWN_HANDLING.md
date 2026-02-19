# Dropdown Handling - Unified State Management

**Created:** 2026-02-03
**Updated:** 2026-02-19 (onValueChanged suppression to prevent form auto-advance)

---

## Overview

MTGA uses dropdowns for date selection (birthday, month/day/year pickers) and other settings. The accessibility mod needs to:

1. Detect when a dropdown is open (to hand off arrow key handling to Unity)
2. Handle explicit close (Escape/Backspace) and Tab-away
3. Sync navigator index after dropdown closes
4. Prevent re-entry when closing auto-opened dropdowns
5. Block Enter/Submit from the game while in dropdown mode
6. Select items without triggering onValueChanged (prevents chain auto-advance)
7. Handle Tab navigation: close current dropdown and move to next element
8. Suppress onValueChanged while dropdown is open (prevents form auto-advance)

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
- `_savedOnValueChanged` - Saved `m_OnValueChanged` event, replaced with empty event while dropdown is open
- `_suppressedDropdownComponent` - The component whose onValueChanged was suppressed
- `_cachedOnValueChangedField` - Cached FieldInfo for cTMP_Dropdown.m_OnValueChanged

**Public API:**
```csharp
// Query state
bool IsDropdownExpanded     // Real state from dropdown's IsExpanded property
bool IsInDropdownMode       // Takes suppression into account
bool ShouldBlockEnterFromGame // True while dropdown is open (persistent across frames)
bool IsSuppressed           // True when reentry is suppressed (old dropdown still closing)
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

### onValueChanged Suppression

When a dropdown is opened by the mod, `DropdownStateManager` temporarily replaces the dropdown's `m_OnValueChanged` event with an empty event. This prevents the game's form validation from detecting value changes while the user is browsing items.

**Problem:** On forms with multiple required dropdowns (e.g., registration page with Month/Day/Year/Country/Experience), the game monitors `onValueChanged` to detect when all fields are filled and auto-advances to the next page. When the last dropdown is opened and an item receives focus, `cTMP_Dropdown` internally sets its value and fires `onValueChanged`, causing the form to auto-submit before the user confirms their selection.

**Solution:** `SuppressOnValueChanged()` saves the original event and replaces it with an empty one on open. `RestoreOnValueChanged()` restores it on close. Restore is called from all close paths:
- `OnDropdownClosed()` - explicit user close
- `UpdateAndCheckExitTransition()` - dropdown closed unexpectedly by the game
- `SuppressReentry()` - auto-opened dropdown closed
- `Reset()` - navigator deactivates or scene changes

Supports all dropdown types: `cTMP_Dropdown` (via reflection on `m_OnValueChanged` field), `TMP_Dropdown`, and legacy `Dropdown` (via `onValueChanged` property).

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
- Tab/Shift+Tab: Calls `CloseActiveDropdown(silent: true)`, suppresses reentry, then navigates to next/previous element
- Enter: Calls `SelectDropdownItem()` (select without closing, announces "Selected")
- Escape/Backspace: Calls `CloseActiveDropdown()` (closes dropdown, announces "closed", syncs focus)
- All Enter key codes consumed via `InputManager.ConsumeKey()`

**BaseNavigator.CloseActiveDropdown(bool silent = false):**
- Calls `DropdownStateManager.OnDropdownClosed()` after closing dropdown
- When `silent` is true, skips "dropdown closed" announcement (used by Tab handler)

**BaseNavigator.CloseDropdownOnElement():**
- Calls `DropdownStateManager.SuppressReentry()` after closing auto-opened dropdown

**BaseNavigator.UpdateEventSystemSelection() - Dropdown branch:**
- Sets EventSystem selection on dropdown element
- If dropdown auto-opens (`IsAnyDropdownExpanded()`):
  - If `_lastNavigationWasTab` AND suppression is NOT active: Calls `OnDropdownOpened()` to keep the dropdown open (Tab auto-open behavior)
  - Otherwise (arrow navigation, or Tab from inside an open dropdown): Calls `CloseDropdownOnElement()` to suppress

**EventSystemPatch:**
- `SendSubmitEventToSelectedObject_Prefix` returns false when `ShouldBlockEnterFromGame`
- `SendMoveEventToSelectedObject_Prefix` returns false when Tab key is pressed (blocks Unity's Tab navigation)

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
    | onValueChanged suppressed (replaced with empty event)
    v
[Dropdown Mode]
    | Arrow keys handled by Unity's dropdown
    | Enter: SelectDropdownItem() sets value silently, announces "Selected"
    |        Dropdown stays open for further browsing
    v User presses Escape/Backspace
[Explicit Close]
    | CloseActiveDropdown() calls dropdown.Hide()
    | DropdownStateManager.OnDropdownClosed()
    | onValueChanged restored
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

### Scenario 3: Tab Between Closed Dropdowns (Auto-Open)

```
[Normal Navigation]
    |
    v User presses Tab (not in dropdown mode)
[Tab Handler in HandleInput]
    | _lastNavigationWasTab = true
    | MoveNext() calls UpdateEventSystemSelection()
    v
[UpdateEventSystemSelection - Dropdown Element]
    | eventSystem.SetSelectedGameObject(dropdown)
    | MTGA auto-opens the dropdown (side effect)
    | IsAnyDropdownExpanded() = true
    | _lastNavigationWasTab = true AND !IsSuppressed
    v
[OnDropdownOpened()]
    | _blockEnterFromGame = true
    | _suppressReentry = false (cleared)
    | Dropdown stays open - user is now in dropdown mode
    v
[Dropdown Mode]
    | Arrow keys navigate items, Enter selects, Escape closes
```

### Scenario 4: Tab From Inside Open Dropdown

```
[Dropdown Mode]
    |
    v User presses Tab
[HandleDropdownNavigation - Tab]
    | CloseActiveDropdown(silent: true) - no "closed" announcement
    | DropdownStateManager.SuppressReentry() - suppression active
    | _lastNavigationWasTab = true
    | MoveNext() calls UpdateEventSystemSelection()
    v
[UpdateEventSystemSelection - Next Dropdown Element]
    | eventSystem.SetSelectedGameObject(nextDropdown)
    | MTGA auto-opens the dropdown (side effect)
    | IsAnyDropdownExpanded() = true
    | BUT: IsSuppressed = true (old dropdown still closing)
    v
[CloseDropdownOnElement()]
    | New auto-opened dropdown is closed
    | SuppressReentry() called again
    | Next dropdown is announced but NOT opened
    v
[Eventually]
    | Old dropdown's IsExpanded = false
    | _suppressReentry cleared
    v
[Normal Navigation]
    | User presses Enter to manually open the dropdown
```

**Known limitation:** Tab from inside an open dropdown does not auto-open the next dropdown. The old dropdown's `IsExpanded` lingers for a frame after `Hide()`, making it impossible to distinguish whether the new dropdown actually auto-opened or the old dropdown is still reporting expanded.

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

### Why Suppress onValueChanged While Open?

MTGA's `cTMP_Dropdown.value` setter always fires `m_OnValueChanged.Invoke()`. When a dropdown is open and the game internally changes the value (e.g., highlighting an item), `onValueChanged` fires. On multi-dropdown forms like registration, the game's form validation listens to these events. When the last required dropdown fires `onValueChanged`, the form detects all fields are filled and auto-advances to the next page - before the user has confirmed their selection.

Simply blocking `SetDropdownValueSilent()` (our own selection) is not enough because the game's own dropdown internals also set the value. By replacing `m_OnValueChanged` with an empty event for the duration the dropdown is open, no value change notifications escape to form validation. The original event is restored when the dropdown closes.

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
   - Escape/Backspace closes dropdown, announces "dropdown closed"
   - No double announcements

2. **Multiple selections**
   - Open dropdown, select item with Enter, use arrows to browse more, select again
   - Dropdown stays open throughout
   - Each selection announced

3. **Auto-opened dropdown suppression (arrow keys)**
   - Navigate to dropdown with arrow keys
   - Dropdown should NOT auto-open
   - Enter key should open dropdown

4. **Tab between closed dropdowns (auto-open)**
   - Tab from a non-dropdown element to a dropdown
   - Dropdown auto-opens, user is in dropdown mode
   - No "dropdown closed" announcement
   - Only the dropdown name is announced

5. **Tab from inside open dropdown**
   - Open a dropdown, browse items with arrows
   - Press Tab: current dropdown closes silently (no "closed" announcement)
   - Next element is announced with correct name
   - If next element is a dropdown, it is NOT auto-opened (known limitation)
   - No double announcements

6. **Shift+Tab from inside open dropdown**
   - Same as Tab but navigates to previous element

7. **Tab order matches arrow order**
   - Tab and arrow keys navigate elements in the same order
   - Tab uses the mod's element list, not Unity's spatial navigation

8. **Post-close navigation**
   - After closing dropdown (Escape/Backspace), Tab/arrow navigation works correctly
   - Navigator index syncs to the element that has focus
   - No Submit auto-click on next focused element

9. **Registration date pickers**
   - Selecting Month value does NOT auto-open Day dropdown
   - User must manually navigate to Day and press Enter
   - Tab from Month to Day auto-opens Day (if Month was closed)
   - Tab from inside Month dropdown to Day does NOT auto-open Day

10. **Form auto-advance prevention (registration page)**
    - Fill all dropdowns except the last one (Experience)
    - Open Experience dropdown with Enter
    - Navigate items with arrow keys
    - Page should NOT auto-advance to the Register page
    - Select item with Enter, close with Escape/Tab
    - Page only advances when user presses "Weiter" (Continue) button

---

## File References

- `src/Core/Services/DropdownStateManager.cs` - Unified state manager
- `src/Core/Services/BaseNavigator.cs` - HandleDropdownNavigation, SelectDropdownItem, SetDropdownValueSilent
- `src/Patches/EventSystemPatch.cs` - Blocks SendSubmitEventToSelectedObject in dropdown mode
- `src/Patches/KeyboardManagerPatch.cs` - Blocks Enter from game's KeyboardManager in dropdown mode
- `src/Core/Services/UIFocusTracker.cs` - Delegates to DropdownStateManager, provides IsAnyDropdownExpanded()
