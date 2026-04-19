# DropdownStateManager.cs
Path: src/Core/Services/DropdownStateManager.cs
Lines: 498

## Top-level comments
- Single source of truth for dropdown mode tracking. Detects dropdown open/close transitions via IsExpanded, suppresses re-entry, blocks Submit/Enter during dropdown navigation, and temporarily replaces onValueChanged with an empty event to stop form auto-advance while the user browses items.

## public static class DropdownStateManager (line 24)
### Fields
- private static bool _wasInDropdownMode (line 32)
- private static bool _suppressReentry (line 39)
- private static GameObject _activeDropdownObject (line 44)
- private static float _blockSubmitUntilTime = -1f (line 51)
- private static bool _blockEnterFromGame (line 60)
- private static object _savedOnValueChanged (line 67)
- private static Component _suppressedDropdownComponent (line 72)
- private static FieldInfo _cachedOnValueChangedField (line 77)
- private static int _pendingNotifyValue = -1 (line 84)
### Properties
- public static bool IsDropdownExpanded (line 94)
- public static bool IsInDropdownMode (line 100) — Note: returns false during suppression even if IsExpanded is still true
- public static bool ShouldBlockEnterFromGame (line 121)
- public static GameObject ActiveDropdown (line 126)
- public static bool IsSuppressed (line 133)
### Methods
- public static bool UpdateAndCheckExitTransition() (line 143) — Note: fires saved onValueChanged with pending value when user confirmed via Enter; clears suppression once IsExpanded is false
- public static bool ShouldBlockSubmit() (line 200) — Note: time-based (500ms) not frame-based; prevents auto-click of element receiving focus after dropdown close
- public static void OnDropdownItemSelected(int selectedValue) (line 212) — Note: records pending value to be fired after onValueChanged is restored
- public static void OnDropdownOpened(GameObject dropdown) (line 224) — Note: hands Enter-blocking from BlockSubmitForToggle to ShouldBlockEnterFromGame; invalidates dropdown scan cache
- public static string OnDropdownClosed() (line 253)
- public static void SuppressReentry() (line 297) — Note: used after closing auto-opened dropdowns whose IsExpanded lags
- public static void Reset() (line 312)
- private static void SuppressOnValueChanged(GameObject dropdownObj) (line 334) — Note: handles cTMP_Dropdown, TMP_Dropdown, and legacy Dropdown types
- private static void RestoreOnValueChanged() (line 387)
- private static void FireOnValueChanged(Component dropdownComponent, int value) (line 438)
- private static FieldInfo GetOnValueChangedField(System.Type type) (line 485) — Note: caches reflection for cTMP_Dropdown.m_OnValueChanged
