# BaseNavigator.InputFields.cs
Path: src/Core/Services/BaseNavigator/BaseNavigator.InputFields.cs
Lines: 215

## Top-level comments
- Input field edit mode: tracking state, announcing characters and content, handling Escape/Tab/Backspace/arrows, search field rescan scheduling.
- Integrates InputFieldEditHelper for field state, character-at-cursor announcement, Backspace handling, and field reactivation.

## public partial class BaseNavigator (line 20)
### Fields
- private InputFieldEditHelper _inputFieldHelper (line 23)
- private int _pendingSearchRescanFrames (line 26)

### Methods
- private void TrackInputFieldState() (line 33) — maintain previous state for Backspace detection
- private bool ExitInputFieldEditMode(bool suppressNextAnnouncement) (line 51) — clear, deactivate, schedule rescan
- private void ScheduleSearchRescan() (line 80) — set frame counter for filter delay
- protected virtual void HandleInputFieldNavigation() (line 94) — Up/Down announce, Left/Right character, Escape exit, Tab navigate
- protected void EnterInputFieldEditModeDirectly(GameObject field, string announcement) (line 159)
- protected string GetEditingFieldText() (line 171)
- protected void ForceExitFieldEditMode() (line 183) — exit without search rescan
- private void DeactivateInputFieldOnElement(GameObject element) (line 193) — counteract auto-focus
