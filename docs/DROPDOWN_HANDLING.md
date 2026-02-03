# Dropdown Handling - State Management Analysis

**Created:** 2026-02-03
**Purpose:** Document dropdown flag behavior for Stage 4.1 of Code Quality Plan

---

## Overview

MTGA uses dropdowns for date selection (birthday, month/day/year pickers) and other settings. The accessibility mod needs to:

1. Detect when a dropdown is open (to hand off arrow key handling to Unity)
2. Handle explicit close (Escape/Backspace)
3. Sync navigator index after dropdown closes (game auto-advances to next field)
4. Prevent re-entry when closing auto-opened dropdowns

---

## The 4 Dropdown Flags

### BaseNavigator Flags

**`_wasInDropdownMode`** (`BaseNavigator.cs:987`)
- **Type:** `bool` (instance)
- **Purpose:** Track dropdown mode from previous frame to detect exit transition
- **Set to `true`:** When `IsEditingDropdown()` returns true (unless `_skipDropdownModeTracking` is set)
- **Set to `false`:**
  - After exit transition triggers `SyncIndexToFocusedElement()` (line 1081)
  - In `CloseDropdownOnElement()` when auto-opened dropdown is closed (line 1545)
- **Used in:** `HandleInput()` to detect dropdown-to-normal transition

**`_skipDropdownModeTracking`** (`BaseNavigator.cs:991`)
- **Type:** `bool` (instance)
- **Purpose:** Prevent `_wasInDropdownMode` from being re-set after closing auto-opened dropdown
- **Set to `true`:** In `CloseDropdownOnElement()` after closing auto-opened dropdown (line 1546)
- **Set to `false`:** Once dropdown is actually closed (line 1075)
- **Problem solved:** Dropdown's `IsExpanded` property may still be `true` briefly after calling `Hide()`, which would re-set `_wasInDropdownMode` without this guard

### UIFocusTracker Flags

**`_suppressNextFocusAnnouncement`** (`UIFocusTracker.cs:40`)
- **Type:** `static bool`
- **Purpose:** Suppress FocusTracker's announcement when navigator handles its own
- **Set to `true`:** Via `SuppressNextFocusAnnouncement()` called from `UpdateEventSystemSelection()` (line 1415)
- **Set to `false`:** In `AnnounceElement()` after checking the flag (line 524)
- **Note:** This flag is not dropdown-specific but is used during dropdown handling

**`_suppressDropdownModeEntry`** (`UIFocusTracker.cs:43`)
- **Type:** `static bool`
- **Purpose:** Suppress dropdown mode re-entry after closing auto-opened dropdown
- **Set to `true`:** Via `SuppressDropdownModeEntry()` called from `CloseDropdownOnElement()` (line 1544)
- **Set to `false`:**
  - In `HandleFocusChange()` when flag is used (line 488)
  - When dropdown actually closes - `IsExpanded` is false (lines 503, 508)
- **Problem solved:** Prevent FocusTracker from entering `_dropdownEditMode` when it sees `IsExpanded=true` after BaseNavigator closed an auto-opened dropdown

---

## Additional State Variables

These are not "flags" but are part of the dropdown state management:

**`_dropdownEditMode`** (`UIFocusTracker.cs:32`)
- **Type:** `static bool`
- **Purpose:** Tracks if user is in dropdown edit mode (dropdown is expanded)
- **Set in:** `HandleFocusChange()` when `IsAnyDropdownExpanded()` returns true
- **Cleared in:** `ExitDropdownEditMode()` or when `IsAnyDropdownExpanded()` returns false

**`_activeDropdownObject`** (`UIFocusTracker.cs:33`)
- **Type:** `static GameObject`
- **Purpose:** Reference to the currently active dropdown
- **Set in:** `EnterDropdownEditMode()` and `HandleFocusChange()`
- **Cleared in:** `ExitDropdownEditMode()` and `HandleFocusChange()`

---

## State Machines

### Scenario 1: Normal Dropdown Flow (User Opens/Closes)

```
[Normal Navigation]
    │
    ▼ User presses Enter on dropdown OR game auto-opens dropdown
[Dropdown Opens]
    │ IsAnyDropdownExpanded() = true
    │ UIFocusTracker: _dropdownEditMode = true
    │ BaseNavigator: _wasInDropdownMode = true
    ▼
[Dropdown Mode]
    │ HandleDropdownNavigation() handles Escape/Backspace
    │ Arrow keys handled by Unity's dropdown
    ▼ User presses Escape/Backspace
[Explicit Close]
    │ CloseActiveDropdown() calls dropdown.Hide()
    │ UIFocusTracker.ExitDropdownEditMode() clears _dropdownEditMode
    ▼
[Next Frame]
    │ IsAnyDropdownExpanded() = false
    │ _wasInDropdownMode = true triggers SyncIndexToFocusedElement()
    │ _wasInDropdownMode = false
    ▼
[Normal Navigation]
```

### Scenario 2: Auto-Advance Flow (Date Picker)

```
[Normal Navigation]
    │
    ▼ User selects Month dropdown value
[Value Selected]
    │ Unity's dropdown handles Enter
    │ Game auto-advances focus to Day dropdown
    │ Game auto-opens Day dropdown
    ▼
[Next Frame]
    │ IsAnyDropdownExpanded() = true (Day dropdown)
    │ UIFocusTracker: _dropdownEditMode = true
    │ BaseNavigator: _wasInDropdownMode = true
    ▼
[Dropdown Mode - Day]
    │ ... same flow continues
```

### Scenario 3: Auto-Opened Dropdown Suppression

```
[Normal Navigation]
    │
    ▼ Navigator calls UpdateEventSystemSelection() to focus dropdown
[EventSystem Selection Set]
    │ MTGA auto-opens the dropdown (side effect)
    │ IsAnyDropdownExpanded() = true
    ▼
[Navigator Detects Auto-Open]
    │ Navigator checks IsAnyDropdownExpanded() in UpdateEventSystemSelection()
    │ Calls CloseDropdownOnElement()
    ▼
[CloseDropdownOnElement()]
    │ dropdown.Hide()
    │ UIFocusTracker.ExitDropdownEditMode() → _dropdownEditMode = false
    │ UIFocusTracker.SuppressDropdownModeEntry() → _suppressDropdownModeEntry = true
    │ _wasInDropdownMode = false
    │ _skipDropdownModeTracking = true
    ▼
[Next Frame(s)]
    │ IsExpanded might STILL be true briefly
    │ HandleFocusChange() sees _suppressDropdownModeEntry, skips entering dropdown mode
    │ HandleInput() sees _skipDropdownModeTracking, skips setting _wasInDropdownMode
    ▼
[Eventually]
    │ IsExpanded = false
    │ _skipDropdownModeTracking = false
    │ _suppressDropdownModeEntry = false
    ▼
[Normal Navigation]
```

---

## Redundancy Analysis

### Parallel Suppression Mechanisms

Two flags serve similar purposes:

| Flag | Location | Suppresses |
|------|----------|------------|
| `_skipDropdownModeTracking` | BaseNavigator | Setting `_wasInDropdownMode = true` |
| `_suppressDropdownModeEntry` | UIFocusTracker | Setting `_dropdownEditMode = true` |

Both exist because:
1. Two separate systems track dropdown state independently
2. Both need to be notified to suppress re-entry
3. The dropdown's `IsExpanded` property has a delay after `Hide()` is called

### Root Cause

The fundamental issue is **dual state tracking**:
- `UIFocusTracker._dropdownEditMode` - tracks if in dropdown mode (via IsExpanded polling)
- `BaseNavigator._wasInDropdownMode` - tracks previous frame's state for transition detection

Both systems react to `IsAnyDropdownExpanded()`, creating a need for coordinated suppression.

---

## Recommendations for Stage 4.2

### Option A: Unified Dropdown State Manager

Create a single `DropdownStateManager` class:

```csharp
public static class DropdownStateManager
{
    // Canonical state
    public static bool IsInDropdownMode { get; private set; }

    // Suppress re-entry after closing auto-opened dropdown
    private static bool _suppressReentry;

    public static void EnterDropdownMode(GameObject dropdown) { ... }
    public static void ExitDropdownMode() { ... }
    public static void SuppressReentry() { _suppressReentry = true; }
    public static bool ShouldEnterDropdownMode()
    {
        if (_suppressReentry)
        {
            _suppressReentry = false;
            return false;
        }
        return IsAnyDropdownExpanded();
    }
}
```

**Benefits:**
- Single source of truth
- One suppression flag instead of two
- Clearer API

**Risks:**
- Changes to both BaseNavigator and UIFocusTracker
- Need to verify all dropdown scenarios still work

### Option B: Keep Structure, Document Better

Keep current implementation but:
1. Add clear comments explaining the coordination
2. Add invariant assertions (debug only)
3. Consider renaming flags for clarity

**Benefits:**
- Lower risk
- Known working behavior

**Drawbacks:**
- Technical debt remains
- Two parallel suppression mechanisms

### Recommended Approach

**Option A** - The redundancy is a symptom of poor separation of concerns. Stage 4.2 should unify the state management.

---

## Test Scenarios for Stage 4.2

1. **Normal dropdown navigation**
   - Arrow keys navigate dropdown items
   - Escape/Backspace closes dropdown
   - No double announcements

2. **Auto-advance (Month → Day → Year)**
   - Select Month value → Day dropdown opens automatically
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

- `src/Core/Services/BaseNavigator.cs:987-991` - Flag declarations
- `src/Core/Services/BaseNavigator.cs:1061-1085` - Dropdown mode handling in HandleInput
- `src/Core/Services/BaseNavigator.cs:516-597` - HandleDropdownNavigation and CloseActiveDropdown
- `src/Core/Services/BaseNavigator.cs:1536-1547` - CloseDropdownOnElement suppression logic
- `src/Core/Services/UIFocusTracker.cs:40-62` - Suppression flags and methods
- `src/Core/Services/UIFocusTracker.cs:218-265` - IsEditingDropdown and IsAnyDropdownExpanded
- `src/Core/Services/UIFocusTracker.cs:372-404` - EnterDropdownEditMode and ExitDropdownEditMode
- `src/Core/Services/UIFocusTracker.cs:478-509` - HandleFocusChange dropdown logic
