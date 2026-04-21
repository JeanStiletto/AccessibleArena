# ManaColorPickerNavigator.cs
Path: src/Core/Services/ManaColorPickerNavigator.cs
Lines: 595

## Top-level comments
- Detects and navigates the ManaColorSelector popup that appears when a mana source produces "any color" (e.g., Ilysian Caryatid). Polled from DuelNavigator at highest priority (before browser detection). Number keys 1-6 select colors, Backspace cancels.

## public class ManaColorPickerNavigator (line 18)

### Fields
- private readonly IAnnouncementService _announcer (line 20)
- private static Type _selectorType (line 23)
- private static PropertyInfo _isOpenProp (line 24)
- private static FieldInfo _selectionProviderField (line 25)
- private static MethodInfo _selectColorMethod (line 26)
- private static MethodInfo _tryCloseSelectorMethod (line 27)
- private static Type _manaColorEnum (line 28)
- private static bool _reflectionInitialized (line 29)
- private static bool _reflectionFailed (line 30)
- private static PropertyInfo _validSelectionsCountProp (line 33)
- private static MethodInfo _getElementAtMethod (line 34)
- private static PropertyInfo _maxSelectionsProp (line 35)
- private static PropertyInfo _allSelectionsCompleteProp (line 36)
- private static PropertyInfo _currentSelectionProp (line 37)
- private static Type _providerType (line 38)
- private static bool _providerReflectionInitialized (line 39)
- private static FieldInfo _primaryColorField (line 42)
- private static PropertyInfo _primaryColorProp (line 43)
- private static bool _primaryColorIsField (line 44)
- private bool _isActive (line 47)
- private bool _hasAnnounced (line 48)
- private UnityEngine.Object _selectorInstance (line 49)
- private object _selectionProvider (line 50)
- private List<(int index, int manaColorValue, string displayName)> _availableColors (line 51)
- private int _cursorIndex (line 52)
- private int _currentSelection (line 53)
- private int _maxSelections (line 54)
- private float _lastScanTime (line 57)
- private const float ScanInterval = 0.1f (line 58)

### Properties
- public bool IsActive => _isActive (line 60)

### Methods
- public ManaColorPickerNavigator(IAnnouncementService announcer) (line 62)
- public void Update() (line 71) — Polls at ScanInterval for active ManaColorSelector; enters/exits on state change
- public bool HandleInput() (line 124)
- private void Enter(UnityEngine.Object selector) (line 207)
- private void Exit() (line 237)
- private void ReadAvailableColors() (line 249)
- private void ReadSelectionState() (line 296)
- private void AnnounceAvailableColors() (line 311)
- private void AnnounceCurrent() (line 336)
- private void SelectColor(int listIndex) (line 346)
- private void TryCancel() (line 386)
- private static string GetColorDisplayName(int manaColorValue) (line 399)
- private static void InitializeReflection() (line 413)
- private static void InitializeProviderReflection(Type providerInstanceType) (line 505)
- private static PropertyInfo FindProperty(Type type, string name) (line 554) — Searches type itself then all implemented interfaces
- private static MethodInfo FindMethod(Type type, string name) (line 581)
