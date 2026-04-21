# SpinnerNavigator.cs
Path: src/Core/Services/SpinnerNavigator.cs
Lines: 580

## Top-level comments
- Detects and navigates SpinnerAnimated widgets used for counter distribution (e.g. Crashing Wave distributing stun counters). Polled from DuelNavigator between ChooseXNavigator and BrowserNavigator. Left/Right navigate between spinners, Up/Down adjust values, Enter submits, Backspace cancels. Zone shortcuts pass through; Tab reclaims spinner focus (BrowserNavigator pattern).

## public class SpinnerNavigator (line 23)
### Fields
- private readonly IAnnouncementService _announcer (line 25)
- private static Type _spinnerAnimatedType (line 28)
- private static Type _spinnerGroupType (line 29)
- private static PropertyInfo _instanceIdProp (line 30)
- private static PropertyInfo _valueProp (line 31)
- private static FieldInfo _upButtonField (line 32)
- private static FieldInfo _downButtonField (line 33)
- private static PropertyInfo _groupMaxValueProp (line 34)
- private static FieldInfo _groupField (line 35) — Note: SpinnerAnimated._group backing field.
- private static bool _reflectionInitialized (line 36)
- private static bool _reflectionFailed (line 37)
- private bool _isActive (line 40)
- private bool _hasAnnounced (line 41)
- private readonly List<MonoBehaviour> _spinners (line 42)
- private int _currentIndex (line 43)
- private int _totalMax (line 44)
- private bool _hasFocus (line 45) — Note: whether the spinner has input focus (vs passive zone navigation).
- private float _lastScanTime (line 48)
- private const float ScanInterval = 0.1f (line 49)

### Properties
- public bool IsActive (line 51)

### Methods
- public SpinnerNavigator(IAnnouncementService announcer) (line 53)
- public void Update() (line 61)
- public bool HandleInput() (line 99)
- private bool IsZoneShortcut() (line 215)
- private List<MonoBehaviour> FindActiveSpinners() (line 225)
- private void Enter(List<MonoBehaviour> spinners) (line 259)
- private void Exit() (line 279)
- private void RefreshSpinners(List<MonoBehaviour> activeSpinners) (line 290)
- private void SortSpinnersByPosition() (line 302)
- private void Navigate(int direction) (line 312)
- private void AdjustValue(int direction) (line 322)
- private void Submit() (line 358)
- private void Cancel() (line 385)
- private void AnnounceEntry() (line 415)
- private void AnnounceCurrentSpinner() (line 426)
- private string GetSpinnerCardName(MonoBehaviour spinner) (line 437)
- private int GetTotalDistributed() (line 451)
- private int ReadGroupMaxValue() (line 465)
- private void ClearEventSystemSelection() (line 512)
- private static void InitializeReflection() (line 521)
