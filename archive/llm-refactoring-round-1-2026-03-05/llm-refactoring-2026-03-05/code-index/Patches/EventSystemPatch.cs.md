# EventSystemPatch.cs Code Index

## File Overview
Harmony patches for blocking Enter key when on toggles and arrow keys when editing input fields. Also blocks Tab from Unity's EventSystem navigation entirely.

## Static Class: EventSystemPatch (line 22)

### Harmony Patch Methods

#### SendMoveEventToSelectedObject Patch
- [HarmonyPatch(typeof(StandaloneInputModule), "SendMoveEventToSelectedObject")] (line 36)
- [HarmonyPrefix]
- public static bool SendMoveEventToSelectedObject_Prefix() (line 38)
  // Blocks arrow key navigation when editing input field
  // Blocks Tab from Unity's EventSystem navigation entirely

#### SendSubmitEventToSelectedObject Patch
- [HarmonyPatch(typeof(StandaloneInputModule), "SendSubmitEventToSelectedObject")] (line 60)
- [HarmonyPrefix]
- public static bool SendSubmitEventToSelectedObject_Prefix() (line 62)
  // Blocks Submit when on toggle or in dropdown mode
  // Blocks Submit for a few frames after dropdown item selection

#### GetKeyDown Patch
- [HarmonyPatch(typeof(Input), nameof(Input.GetKeyDown), typeof(KeyCode))] (line 94)
- [HarmonyPostfix]
- public static void GetKeyDown_Postfix(KeyCode key, ref bool __result) (line 96)
  // Blocks Enter key when on toggle
  // Sets EnterPressedWhileBlocked flag so mod code can still detect the press
