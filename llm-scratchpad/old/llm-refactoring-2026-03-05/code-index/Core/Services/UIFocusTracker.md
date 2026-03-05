# UIFocusTracker.cs

## Overview
Tracks UI focus changes using Unity's EventSystem and announces them via screen reader.
Polls EventSystem.current.currentSelectedGameObject each frame to detect selection changes.
Also provides Tab navigation fallback when Unity's navigation is broken (menu scenes only).

## Class: UIFocusTracker (line 18)

### Constants
- private const int MAX_SELECTABLE_LOG_COUNT (line 20)

### State
- private readonly IAnnouncementService _announcer (line 22)
- private GameObject _lastSelected (line 23)
- private string _lastAnnouncedText (line 24)
- private static bool _inputFieldEditMode (line 27)
- private static GameObject _activeInputFieldObject (line 28)
- private static System.Reflection.PropertyInfo _cachedIsExpandedProperty (line 31)
- private static System.Type _cachedDropdownType (line 32)

### Static Properties
- public static bool NavigatorHandlesAnnouncements { get; set; } (line 40)

### Events
- public event Action<GameObject, GameObject> OnFocusChanged (line 45)

### Input Field Mode
- public static bool IsEditingInputField() (line 55)
  - Note: Does NOT check isFocused due to Unity deactivation timing
- public static bool IsAnyInputFieldFocused() (line 77)
  - Handles both Tab navigation and mouse click cases
- public static void EnterInputFieldEditMode(GameObject inputFieldObject) (line 118)
- public static void ExitInputFieldEditMode() (line 128)
- public static void DeactivateFocusedInputField() (line 143)
  - IMPORTANT: Invokes onEndEdit BEFORE deactivating
- public static bool IsInputField(GameObject obj) (line 207)

### Dropdown Mode
- public static bool IsEditingDropdown() (line 219)
- public static bool IsAnyDropdownExpanded() (line 229)
  - Queries actual dropdown state, not assumptions
- private static bool GetIsExpandedProperty(MonoBehaviour dropdown) (line 274)
  - Get IsExpanded via reflection for cTMP_Dropdown
- private static bool IsDropdownExpanded(TMPro.TMP_Dropdown dropdown) (line 307)
- private static bool IsLegacyDropdownExpanded(UnityEngine.UI.Dropdown dropdown) (line 319)
- public static GameObject GetExpandedDropdown() (line 332)
- public static void EnterDropdownEditMode(GameObject dropdownObject) (line 373)
- public static string ExitDropdownEditMode() (line 382)
  - Returns name of focused element for navigator sync
- public static bool IsDropdownItem(GameObject obj) (line 391)
- public static bool IsDropdown(GameObject obj) (line 401)

### Constructor
- public UIFocusTracker(IAnnouncementService announcer) (line 415)

### Public Methods
- public void Update() (line 424)

### Core Focus Tracking
- private void CheckFocusChange() (line 438)
- private void HandleFocusChange(GameObject selected) (line 452)
- private void AnnounceElement(GameObject element) (line 484)
- private static string GetName(GameObject obj) (line 536)

### Debug Logging
- private void Log(string message) (line 545)
- private void DebugLogKeyPresses() (line 550)
- private void DebugLogCurrentSelection() (line 570)
- private void DebugScanForFocusedElements() (line 595)
- private void DebugScanInputFields() (line 602)
- private void DebugScanSelectables() (line 625)
- private void DebugScanEventTriggers() (line 648)
