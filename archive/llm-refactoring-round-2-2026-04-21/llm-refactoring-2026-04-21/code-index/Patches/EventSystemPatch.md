# EventSystemPatch.cs
Path: src/Patches/EventSystemPatch.cs
Lines: 311

## Top-level comments
- Harmony patches for blocking Enter key when on toggles and arrow keys when editing input fields. MTGA has multiple Enter detection paths (Unity EventSystem Submit, Input.GetKeyDown, ActionSystem via NewInputHandler.OnAccept), all of which are intercepted when BlockSubmitForToggle is set by navigators.

## public static class EventSystemPatch (line 29)
### Methods
- public static void ApplyRuntimePatches(HarmonyLib.Harmony harmony) (line 35) — Note: patches NewInputHandler.OnAccept/OnNext/OnPrevious and RegistrationPanel.OnButton_SubmitRegistration via reflection at runtime
- public static bool NewInputHandlerOnAccept_Prefix() (line 111) — Note: returns false (blocks) on Login scene unless AllowNativeEnterOnLogin
- public static bool NewInputHandlerOnNextPrevious_Prefix() (line 127) — Note: returns false on Login scene
- public static void SubmitRegistrationDiagnostic_Prefix() (line 141) — Note: logs stack trace and starts guidance coroutine
- private static IEnumerator AnnounceRegistrationGuidanceAfterDelay() (line 155) — Note: waits 8s then announces guidance if still on Login scene
- public static bool SendMoveEventToSelectedObject_Prefix() (line 193) — Note: Harmony prefix; blocks Unity move/tab navigation while editing fields, during Tab presses, or when BlockSubmitForToggle is set
- public static bool SendSubmitEventToSelectedObject_Prefix() (line 237) — Note: Harmony prefix; blocks submit for PhaseSkipGuard, active browser, toggle state, dropdown state, and post-dropdown window
- public static void GetKeyDown_Postfix(KeyCode key, ref bool __result) (line 286) — Note: also sets InputManager.EnterPressedWhileBlocked flag as side-effect
