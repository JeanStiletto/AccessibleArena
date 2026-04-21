# ShortcutRegistryTests.cs
Path: tests/AccessibleArena.Tests/ShortcutRegistryTests.cs
Lines: 148

## public class ShortcutRegistryTests (line 10)
### Fields
- private ShortcutRegistry _registry (line 12)
### Methods
- public void SetUp() (line 15)
- public void RegisterAndProcessKey_NoModifier_InvokesAction() (line 18)
- public void ProcessKey_UnregisteredKey_ReturnsFalse() (line 28)
- public void UnregisterShortcut_SubsequentProcessKey_ReturnsFalse() (line 35)
- public void CtrlModifier_FiresOnlyWhenCtrlHeld() (line 43)
- public void CtrlModifier_DoesNotFireWhenShiftAlsoHeld() (line 59)
- public void UnmodifiedShortcut_DoesNotFireWhenModifierHeld() (line 69)
- public void ShiftModifier_FiresOnlyWhenShiftHeld() (line 80)
- public void RegisterSameKeyTwice_BothActionsRetained_FirstWins() (line 89)
- public void SameKeyDifferentModifiers_EachFiresIndependently() (line 101)
- public void GetAllShortcuts_ReturnsAllRegistered() (line 116)
- public void ProcessKey_ActionIsNullSafe_DoesNotThrow() (line 127)
- public void GetKeyString_NoModifier_ReturnsKeyName() (line 134)
- public void GetKeyString_WithCtrlModifier_ReturnsCtrlPlusKey() (line 141)
