# PriorityController.cs
Path: src/Core/Services/PriorityController.cs
Lines: 586

## Top-level comments
- Reflection wrapper for GameManager.AutoRespManager and ButtonPhaseLadder. Provides full-control toggle, auto-pass modes, and phase-stop toggle functionality.

## public class PriorityController (line 15)
### Fields
- private MonoBehaviour _gameManager (line 18)
- private int _gameManagerSearchFrame (line 19)
- private object _autoRespManager (line 22)
- private MethodInfo _toggleFullControl (line 23)
- private MethodInfo _toggleLockedFullControl (line 24)
- private PropertyInfo _fullControlEnabled (line 25)
- private PropertyInfo _fullControlLocked (line 26)
- private MethodInfo _setAutoPassOption (line 29)
- private PropertyInfo _autoPassEnabled (line 30)
- private Type _autoPassOptionType (line 31)
- private object _optionUnlessOpponentAction (line 32)
- private object _optionTurn (line 33)
- private object _optionResolveMyStackEffects (line 34)
- private MonoBehaviour _phaseLadder (line 37)
- private int _phaseLadderSearchFrame (line 38)
- private FieldInfo _phaseIconsField (line 39)
- private FieldInfo _playerStopTypesField (line 42)
- private PropertyInfo _stopStateProp (line 43)
- private MethodInfo _toggleTransientStop (line 46)
- private Dictionary<int, List<object>> _phaseStopMap (line 49)
- private static readonly string[] PhaseNames (line 52) — Note: display names indexed 0-9 mapping to keys 1,2,3,4,5,6,7,8,9,0.
- private static readonly string[] PhaseLocaleKeys (line 67) — Note: locale keys parallel to PhaseNames.
- private static readonly string[] StopTypeNames (line 82) — Note: GRE StopType names parallel to PhaseNames; index 6 (key 7) pairs with CombatDamageStopType.
- private const string CombatDamageStopType = "CombatDamageStep" (line 97)

### Methods
- private MonoBehaviour FindGameManager() (line 99) — Note: throttled to once per frame.
- private object GetAutoRespManager() (line 120)
- public bool? ToggleFullControl() (line 160) — Note: returns the new state, or null on failure.
- public bool? ToggleLockFullControl() (line 185)
- public bool IsFullControlEnabled() (line 209)
- public bool IsFullControlLocked() (line 227)
- private bool EnsureAutoPassCached() (line 245)
- public bool IsAutoPassActive() (line 282)
- public bool? TogglePassUntilResponse() (line 295)
- public bool? ToggleSkipTurn() (line 320)
- private MonoBehaviour FindPhaseLadder() (line 341)
- private static FieldInfo GetFieldInHierarchy(Type type, string name) (line 374) — Note: walks base types; GetField with NonPublic does not search base classes.
- private void BuildPhaseStopMap() (line 390) — Note: skips AvatarPhaseIcon buttons; key index 6 may contain two buttons (FirstStrikeDamage + CombatDamage).
- public (string phaseName, bool isSet)? TogglePhaseStop(int keyIndex) (line 473) — Note: uses ButtonPhaseLadder.ToggleTransientStop to bypass PhaseLadderButton.ToggleStop's AllowStop guard.
- private bool IsPhaseStopSet(object button) (line 528)
- public string GetPhaseName(int keyIndex) (line 550)
- public void ClearCache() (line 561)
