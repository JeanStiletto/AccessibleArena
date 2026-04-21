# UIFocusTracker.cs
Path: src/Core/Services/UIFocusTracker.cs
Lines: 798

## Top-level comments
- Tracks UI focus changes via Unity's EventSystem and announces them through the screen reader. Polls EventSystem.current.currentSelectedGameObject each frame to detect selection changes. Also provides Tab-navigation fallback when Unity's navigation is broken (menu scenes only).

## public class UIFocusTracker (line 20)
### Fields
- private const int MAX_SELECTABLE_LOG_COUNT = 10 (line 22)
- private readonly IAnnouncementService _announcer (line 24)
- private GameObject _lastSelected (line 25)
- private string _lastAnnouncedText (line 26)
- private static bool _inputFieldEditMode (line 29)
- private static GameObject _activeInputFieldObject (line 30)
- private static System.Reflection.PropertyInfo _cachedIsExpandedProperty (line 33)
- private static System.Type _cachedDropdownType (line 34)
- private static System.Type _customDropdownScanType (line 38)
- private static bool _customDropdownTypeResolved (line 39)
- private static bool _cachedInputFieldFallback (line 42)
- private static float _cachedInputFieldFallbackTime (line 43)
- private const float InputFieldCacheExpiry = 2.0f (line 46)
- private static bool _cachedDropdownExpanded (line 49)
- private static GameObject _cachedExpandedDropdown (line 50)
- private static float _cachedDropdownTime (line 51)
- private const float DropdownCacheExpiry = 0.5f (line 53)

### Properties
- public static bool NavigatorHandlesAnnouncements { get; set; } (line 61) — Note: set each frame by AccessibleArenaMod based on NavigatorManager.HasActiveNavigator; skipped when dropdown mode is active.

### Events
- public event Action<GameObject, GameObject> OnFocusChanged (line 66)

### Methods
- public static bool IsEditingInputField() (line 76) — Note: relies on the explicit `_inputFieldEditMode` flag rather than isFocused because TMP_InputField deactivates the field on Up/Down arrows before mod code runs.
- public static bool IsAnyInputFieldFocused() (line 98)
- private static bool ScanForFocusedInputFields() (line 128)
- public static void EnterInputFieldEditMode(GameObject inputFieldObject) (line 148)
- public static void ExitInputFieldEditMode() (line 158)
- public static void DeactivateFocusedInputField(GameObject expectedField = null) (line 178)
- public static bool IsInputField(GameObject obj) (line 317)
- public static bool IsEditingDropdown() (line 329)
- public static bool IsAnyDropdownExpanded() (line 339)
- private static bool GetIsExpandedProperty(MonoBehaviour dropdown) (line 348) — Note: caches PropertyInfo per type to avoid repeated reflection lookups.
- private static bool IsDropdownExpanded(TMPro.TMP_Dropdown dropdown) (line 381)
- private static bool IsLegacyDropdownExpanded(UnityEngine.UI.Dropdown dropdown) (line 393)
- public static GameObject GetExpandedDropdown() (line 406)
- public static void InvalidateDropdownCache() (line 416)
- public static void ClearScanCaches() (line 424)
- private static void RefreshDropdownCache() (line 435)
- private static GameObject ScanForExpandedDropdown() (line 458)
- public static void EnterDropdownEditMode(GameObject dropdownObject) (line 501)
- public static string ExitDropdownEditMode() (line 510)
- public static bool IsDropdownItem(GameObject obj) (line 519)
- public static bool IsDropdown(GameObject obj) (line 529)
- public UIFocusTracker(IAnnouncementService announcer) (line 543)
- public void Update() (line 552)
- private void CheckFocusChange() (line 566)
- private void HandleFocusChange(GameObject selected) (line 580)
- private void AnnounceElement(GameObject element) (line 615)
- private static string GetName(GameObject obj) (line 667)
- private void Log(string message) (line 676)
- private void DebugLogKeyPresses() (line 681)
- private void DebugLogCurrentSelection() (line 701)
- private void DebugScanForFocusedElements() (line 726)
- private void DebugScanInputFields() (line 733)
- private void DebugScanSelectables() (line 756)
- private void DebugScanEventTriggers() (line 779)
