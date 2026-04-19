# PriorityController.cs Code Index

## Summary
Reflection wrapper for GameManager.AutoRespManager and ButtonPhaseLadder. Provides full control toggle and phase stop toggle functionality.

## Classes

### class PriorityController (line 14)
```
private MonoBehaviour _gameManager (line 17)
private int _gameManagerSearchFrame (line 18)

private object _autoRespManager (line 21)
private MethodInfo _toggleFullControl (line 22)
private MethodInfo _toggleLockedFullControl (line 23)
private PropertyInfo _fullControlEnabled (line 24)
private PropertyInfo _fullControlLocked (line 25)

private MonoBehaviour _phaseLadder (line 28)
private int _phaseLadderSearchFrame (line 29)
private FieldInfo _phaseIconsField (line 30)

private FieldInfo _playerStopTypesField (line 33)
private PropertyInfo _stopStateProp (line 34)

private MethodInfo _toggleTransientStop (line 37)
  // NOTE: Cached ToggleTransientStop on ButtonPhaseLadder (bypasses AllowStop guard)

private Dictionary<int, List<object>> _phaseStopMap (line 40)
  // NOTE: Maps key index (0-9) to PhaseLadderButton(s)

private static readonly string[] PhaseNames (line 43)
private static readonly string[] PhaseLocaleKeys (line 58)
private static readonly string[] StopTypeNames (line 73)
private const string CombatDamageStopType = "CombatDamageStep" (line 88)

private MonoBehaviour FindGameManager() (line 90)
private object GetAutoRespManager() (line 112)
public bool? ToggleFullControl() (line 151)
  // NOTE: Toggle temporary full control (resets on phase change)
public bool? ToggleLockFullControl() (line 176)
  // NOTE: Toggle locked full control (permanent until toggled off)
public bool IsFullControlEnabled() (line 200)
public bool IsFullControlLocked() (line 218)
private MonoBehaviour FindPhaseLadder() (line 233)
private static FieldInfo GetFieldInHierarchy(Type type, string name) (line 266)
  // NOTE: Get private FieldInfo by walking type hierarchy (GetField with NonPublic doesn't search base classes)
private void BuildPhaseStopMap() (line 282)
  // NOTE: Build mapping from key index (0-9) to PhaseLadderButton objects. Key 7 maps to two buttons (FirstStrike + CombatDamage)
public (string phaseName, bool isSet)? TogglePhaseStop(int keyIndex) (line 365)
  // NOTE: Toggle phase stop by key index (0-9), returns (phaseName, isNowSet) or null if failed
private bool IsPhaseStopSet(object button) (line 420)
  // NOTE: Check if phase stop button is currently set using StopState property (SettingStatus enum)
public string GetPhaseName(int keyIndex) (line 442)
public void ClearCache() (line 453)
  // NOTE: Clear all cached references, call on scene change
```
