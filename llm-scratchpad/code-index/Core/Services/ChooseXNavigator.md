# ChooseXNavigator.cs
Path: src/Core/Services/ChooseXNavigator.cs
Lines: 505

## public class ChooseXNavigator (line 20)

Detects and navigates the View_ChooseXInterface popup (X-cost spells, choose-any-amount, die roll). Polled by DuelNavigator. Up/Down +/-1, PageUp/PageDown +/-5, Enter submits, Backspace cancels. Zone shortcuts (B/A/C/G/...) release focus, Tab reclaims it.

### Fields
- private readonly IAnnouncementService _announcer (line 22)
- private static Type _viewType (line 25)
- private static FieldInfo _rootField (line 26)
- private static FieldInfo _upArrowField (line 27)
- private static FieldInfo _downArrowField (line 28)
- private static FieldInfo _upFiveArrowField (line 29)
- private static FieldInfo _downFiveArrowField (line 30)
- private static FieldInfo _buttonLabelField (line 31)
- private static FieldInfo _confirmButtonField (line 32)
- private static bool _reflectionInitialized (line 33)
- private static bool _reflectionFailed (line 34)
- private bool _isActive (line 37)
- private bool _hasAnnounced (line 38)
- private bool _hasFocus (line 39) — whether ChooseX has input focus vs zone navigation
- private MonoBehaviour _viewInstance (line 40)
- private string _lastAnnouncedValue (line 41)
- private uint? _maxValue (line 42)
- private float _lastScanTime (line 45)
- private const float ScanInterval = 0.1f (line 46)

### Properties
- public bool IsActive => _isActive (line 48)

### Methods
- public ChooseXNavigator(IAnnouncementService announcer) (line 50)
- public void Update() (line 58) — polls every 100ms for active View_ChooseXInterface
- public bool HandleInput() (line 93)
- private bool IsZoneShortcut() (line 180) — B/A/R/C/G/X/S/W/D/L/V
- private MonoBehaviour FindActiveView() (line 190)
- private void Enter(MonoBehaviour view) (line 212)
- private void Exit() (line 227)
- private void AnnounceEntry() (line 238)
- private void AnnounceCurrentValue() (line 256)
- private void ClickButton(FieldInfo buttonField, int direction) (line 266) — announces at-min/at-max when button becomes non-interactable
- private void Submit() (line 308)
- private void Cancel() (line 332) — finds PromptButton_Secondary/Primary cancel button
- private string GetLabelText() (line 362)
- private bool IsButtonActive(FieldInfo buttonField) (line 383)
- private uint? FindMaxValue() (line 404) — walks ValueModified event invocation list to find workflow._max; filters out int.MaxValue "unlimited" sentinel
- private static void InitializeReflection() (line 444)
