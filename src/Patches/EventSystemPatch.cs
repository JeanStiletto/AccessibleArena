using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using MelonLoader;
using AccessibleArena.Core.Services;

namespace AccessibleArena.Patches
{
    /// <summary>
    /// Harmony patches for blocking Enter key when on toggles.
    ///
    /// MTGA has multiple ways of detecting Enter:
    /// 1. Unity's EventSystem Submit - blocked by SendSubmitEventToSelectedObject patch
    /// 2. Direct Input.GetKeyDown calls - blocked by GetKeyDown patch
    ///
    /// Both patches check BlockSubmitForToggle flag set by navigators when on a toggle.
    /// </summary>
    [HarmonyPatch]
    public static class EventSystemPatch
    {
        /// <summary>
        /// Patch StandaloneInputModule.SendSubmitEventToSelectedObject to block Submit
        /// when our navigator is on a toggle element.
        /// </summary>
        [HarmonyPatch(typeof(StandaloneInputModule), "SendSubmitEventToSelectedObject")]
        [HarmonyPrefix]
        public static bool SendSubmitEventToSelectedObject_Prefix()
        {
            // Block Submit when we're on a toggle - our mod handles toggle activation directly
            if (InputManager.BlockSubmitForToggle)
            {
                return false;
            }

            // Block Submit for a few frames after dropdown item selection to prevent
            // MTGA from auto-clicking Continue (or other auto-advanced elements)
            if (DropdownStateManager.ShouldBlockSubmit())
            {
                MelonLogger.Msg("[EventSystemPatch] BLOCKED Submit - post-dropdown selection window");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Patch Input.GetKeyDown to block Enter key when on a toggle.
        /// This catches MTGA code that directly reads Input.GetKeyDown(KeyCode.Return)
        /// bypassing both KeyboardManager and EventSystem.
        /// Sets EnterPressedWhileBlocked flag so our code can still detect the press.
        /// </summary>
        [HarmonyPatch(typeof(Input), nameof(Input.GetKeyDown), typeof(KeyCode))]
        [HarmonyPostfix]
        public static void GetKeyDown_Postfix(KeyCode key, ref bool __result)
        {
            // Only intercept when we're on a toggle and the key is Enter
            if (InputManager.BlockSubmitForToggle && __result)
            {
                if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
                {
                    MelonLogger.Msg($"[EventSystemPatch] BLOCKED Input.GetKeyDown({key}) - on toggle, setting EnterPressedWhileBlocked");
                    InputManager.EnterPressedWhileBlocked = true;
                    __result = false;
                }
            }
        }
    }
}
