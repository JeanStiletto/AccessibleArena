# ManaColorPickerNavigator.cs

## Summary
Detects and navigates the ManaColorSelector popup that appears when a mana source produces "any color". Polled from DuelNavigator at highest priority. Number keys 1-6 select colors, Backspace cancels.

## Classes

### ManaColorPickerNavigator (line 17)
```
public class ManaColorPickerNavigator
  private readonly IAnnouncementService _announcer (line 19)

  // Reflection cache
  private static Type _selectorType (line 22)
  private static PropertyInfo _isOpenProp (line 23)
  private static FieldInfo _selectionProviderField (line 24)
  private static MethodInfo _selectColorMethod (line 25)
  private static MethodInfo _tryCloseSelectorMethod (line 26)
  private static Type _manaColorEnum (line 27)
  private static bool _reflectionInitialized (line 28)
  private static bool _reflectionFailed (line 29)

  // IManaSelectorProvider reflection cache
  private static PropertyInfo _validSelectionsCountProp (line 32)
  private static MethodInfo _getElementAtMethod (line 33)
  private static PropertyInfo _maxSelectionsProp (line 34)
  private static PropertyInfo _allSelectionsCompleteProp (line 35)
  private static PropertyInfo _currentSelectionProp (line 36)
  private static Type _providerType (line 37)
  private static bool _providerReflectionInitialized (line 38)

  // ManaProducedData reflection cache
  private static FieldInfo _primaryColorField (line 41)
  private static PropertyInfo _primaryColorProp (line 42)
  private static bool _primaryColorIsField (line 43)

  // State
  private bool _isActive (line 46)
  private bool _hasAnnounced (line 47)
  private UnityEngine.Object _selectorInstance (line 48)
  private object _selectionProvider (line 49)
  private List<(int index, int manaColorValue, string displayName)> _availableColors (line 50)
  private int _cursorIndex (line 51)
  private int _currentSelection (line 52)
  private int _maxSelections (line 53)

  private float _lastScanTime (line 56)
  private const float ScanInterval (line 57)

  public bool IsActive => _isActive (line 59)

  public ManaColorPickerNavigator(IAnnouncementService announcer) (line 61)
  public void Update() (line 70)
  public bool HandleInput() (line 123)

  private void Enter(UnityEngine.Object selector) (line 206)
  private void Exit() (line 236)
  private void ReadAvailableColors() (line 248)
  private void ReadSelectionState() (line 295)
  private void AnnounceAvailableColors() (line 310)
  private void AnnounceCurrent() (line 335)
  private void SelectColor(int listIndex) (line 345)
  private void TryCancel() (line 385)
  private static string GetColorDisplayName(int manaColorValue) (line 398)
  private static void InitializeReflection() (line 412)
  private static void InitializeProviderReflection(Type providerInstanceType) (line 529)
  private static PropertyInfo FindProperty(Type type, string name) (line 578)
  private static MethodInfo FindMethod(Type type, string name) (line 605)
```
