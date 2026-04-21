# KeyHoldRepeaterTests.cs
Path: tests/AccessibleArena.Tests/KeyHoldRepeaterTests.cs
Lines: 180

## public class KeyHoldRepeaterTests (line 8)
### Fields
- private KeyHoldRepeater _rep (line 10)
### Methods
- public void SetUp() (line 13)
- public void TearDown() (line 21)
- public void InitialPress_FiresAction_ReturnsTrue() (line 27)
- public void NoKeyState_ReturnsFalse_ActionNotCalled() (line 37)
- public void HeldBeforeInitialDelay_DoesNotRepeat() (line 46)
- public void HeldAfterInitialDelay_Repeats() (line 62)
- public void HoldRepeat_ActionReturnsFalse_StopsRepeating() (line 78)
- public void KeyRelease_StopsHolding_ReturnsFalse() (line 99)
- public void Reset_ClearsHoldState() (line 111)
- public void DifferentKeyPressed_PreviousHoldCleared() (line 128)
- public void ActionOverload_AlwaysRepeats() (line 150)
- public void InitialPress_ActionReturnsFalse_NoHoldTracked() (line 164)
