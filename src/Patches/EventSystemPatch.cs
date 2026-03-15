using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using MelonLoader;
using AccessibleArena.Core.Services;

namespace AccessibleArena.Patches
{
    /// <summary>
    /// Harmony patches for blocking Enter key and arrow keys from reaching the game.
    ///
    /// MTGA has THREE independent input paths that can trigger actions:
    /// 1. Unity's EventSystem Submit - blocked by SendSubmitEventToSelectedObject patch
    /// 2. Direct Input.GetKeyDown calls (used by game's OldInputHandler/ActionSystem) - blocked by GetKeyDown patch
    /// 3. Game's KeyboardManager - blocked by KeyboardManagerPatch
    ///
    /// Path 2 is critical: the game's ActionSystem uses OldInputHandler.Update() which polls
    /// Input.GetKeyDown(Return) and fires Accept events to Panel.OnAccept(). Without blocking
    /// this path, Enter presses intended for our navigator (e.g., entering input field edit mode)
    /// also trigger Panel.OnAccept(), causing phantom button clicks (e.g., registration auto-submit).
    ///
    /// Arrow key navigation is blocked by SendMoveEventToSelectedObject patch when
    /// the user is editing an input field (prevents focus from leaving the field).
    /// </summary>
    [HarmonyPatch]
    public static class EventSystemPatch
    {
        /// <summary>
        /// Patch StandaloneInputModule.SendMoveEventToSelectedObject to block arrow key
        /// navigation when the user is editing an input field. Without this, pressing
        /// Up/Down in a single-line input field causes Unity to navigate to the next
        /// selectable element, leaving the field unexpectedly.
        ///
        /// Also blocks Tab navigation from Unity's EventSystem entirely. Unity processes
        /// Tab in EventSystem.Update() BEFORE our MelonLoader Update(), so without this
        /// block, Unity's Tab cycling auto-opens dropdowns and moves focus to elements
        /// in Unity's spatial navigation order (which differs from our element list order).
        /// Our mod handles all Tab navigation via BaseNavigator.HandleInput().
        /// </summary>
        [HarmonyPatch(typeof(StandaloneInputModule), "SendMoveEventToSelectedObject")]
        [HarmonyPrefix]
        public static bool SendMoveEventToSelectedObject_Prefix()
        {
            if (UIFocusTracker.IsEditingInputField())
            {
                return false;
            }

            // Block Tab from Unity's EventSystem navigation.
            // Our mod handles Tab exclusively - without this, Unity processes Tab first
            // and auto-opens dropdowns or cycles through selectables in the wrong order.
            if (Input.GetKey(KeyCode.Tab))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Patch StandaloneInputModule.SendSubmitEventToSelectedObject to block Submit
        /// when our navigator is active. This prevents the game from processing Enter
        /// through Unity's EventSystem when our mod is handling all keyboard input.
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

            // Block Submit when in dropdown mode - we handle item selection ourselves
            // to prevent the game's chain auto-advance (onValueChanged triggers next dropdown)
            if (DropdownStateManager.ShouldBlockEnterFromGame)
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

            // Block Submit when our navigator is active - our mod handles all Enter presses.
            // Without this, Unity's Submit event can trigger game actions (like Panel.OnAccept)
            // in parallel with our own activation handling.
            if (NavigatorManager.Instance?.HasActiveNavigator == true)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Patch Input.GetKeyDown to block Enter key from reaching game code.
        /// This catches MTGA code that directly reads Input.GetKeyDown(KeyCode.Return)
        /// bypassing both KeyboardManager and EventSystem — most critically the game's
        /// OldInputHandler.Update() which polls GetKeyDown(Return) and fires Accept
        /// events to Panel.OnAccept(), causing phantom button clicks.
        /// Sets EnterPressedWhileBlocked flag so our code can still detect the press.
        /// </summary>
        [HarmonyPatch(typeof(Input), nameof(Input.GetKeyDown), typeof(KeyCode))]
        [HarmonyPostfix]
        public static void GetKeyDown_Postfix(KeyCode key, ref bool __result)
        {
            if (!__result) return;
            if (key != KeyCode.Return && key != KeyCode.KeypadEnter) return;

            // Block Enter when on a toggle element - our mod handles toggle activation directly
            if (InputManager.BlockSubmitForToggle)
            {
                MelonLogger.Msg($"[EventSystemPatch] BLOCKED Input.GetKeyDown({key}) - on toggle/dropdown, setting EnterPressedWhileBlocked");
                InputManager.EnterPressedWhileBlocked = true;
                __result = false;
                return;
            }

            // Block Enter from game's ActionSystem when our navigator is active.
            // The game's OldInputHandler.Update() polls GetKeyDown(Return) and fires
            // Accept events to Panel.OnAccept(), causing phantom button clicks
            // (e.g., registration auto-submit when user presses Enter for input fields).
            // Exception: during dropdown item selection (ShouldBlockEnterFromGame=true),
            // BlockSubmitForToggle is already cleared so we reach here — but we must NOT
            // block because the dropdown needs GetKeyDown(Return) to work.
            if (NavigatorManager.Instance?.HasActiveNavigator == true && !DropdownStateManager.ShouldBlockEnterFromGame)
            {
                InputManager.EnterPressedWhileBlocked = true;
                __result = false;
            }
        }
    }
}
