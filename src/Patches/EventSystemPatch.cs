using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using MelonLoader;
using System.Collections;
using System.Reflection;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using SceneNames = AccessibleArena.Core.Constants.SceneNames;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Patches
{
    /// <summary>
    /// Harmony patches for blocking Enter key when on toggles and arrow keys when editing input fields.
    ///
    /// MTGA has multiple ways of detecting Enter:
    /// 1. Unity's EventSystem Submit - blocked by SendSubmitEventToSelectedObject patch
    /// 2. Direct Input.GetKeyDown calls - blocked by GetKeyDown patch
    /// 3. ActionSystem via NewInputHandler.OnAccept() - blocked by runtime patch (ApplyRuntimePatches)
    ///
    /// All patches check BlockSubmitForToggle flag set by navigators when on a toggle/login element.
    ///
    /// Arrow key navigation is blocked by SendMoveEventToSelectedObject patch when
    /// the user is editing an input field (prevents focus from leaving the field).
    /// </summary>
    [HarmonyPatch]
    public static class EventSystemPatch
    {
        /// <summary>
        /// Apply runtime Harmony patches that can't use attribute-based patching
        /// (because the target types are in game assemblies without compile-time references).
        /// </summary>
        public static void ApplyRuntimePatches(HarmonyLib.Harmony harmony)
        {
            // Patch NewInputHandler.OnAccept to block the new Input System's Enter detection.
            // MTGA uses Unity's new Input System (UnityEngine.InputSystem) when the
            // "use_new_unity_input" feature toggle is enabled. NewInputHandler registers
            // callbacks with the InputAction system — these fire INDEPENDENTLY of
            // Input.GetKeyDown (old system), so our GetKeyDown_Postfix doesn't block them.
            // Without this patch, pressing Enter fires BOTH our mod's activation AND
            // Panel.OnAccept() via ActionSystem, causing double registration submission.
            var newInputType = FindType("Core.Code.Input.NewInputHandler");
            if (newInputType != null)
            {
                var onAcceptMethod = newInputType.GetMethod("OnAccept", PublicInstance);
                if (onAcceptMethod != null)
                {
                    var prefix = typeof(EventSystemPatch).GetMethod(nameof(NewInputHandlerOnAccept_Prefix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(onAcceptMethod, prefix: new HarmonyMethod(prefix));
                    Log.Msg("EventSystemPatch", "Patched NewInputHandler.OnAccept()");
                }
                else
                {
                    Log.Warn("EventSystemPatch", "Could not find NewInputHandler.OnAccept method");
                }
            }
            else
            {
                Log.Warn("EventSystemPatch", "Could not find NewInputHandler type");
            }

            // Patch NewInputHandler.OnNext/OnPrevious to block the game's Tab/Shift+Tab
            // navigation on the Login scene. Without this, Panel.OnNext() detects the
            // Login button is selected and redirects focus to SelectOnLoad (the email field),
            // overriding our mod's Tab navigation.
            if (newInputType != null)
            {
                var onNextMethod = newInputType.GetMethod("OnNext", PublicInstance);
                if (onNextMethod != null)
                {
                    var prefix = typeof(EventSystemPatch).GetMethod(nameof(NewInputHandlerOnNextPrevious_Prefix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(onNextMethod, prefix: new HarmonyMethod(prefix));
                    Log.Msg("EventSystemPatch", "Patched NewInputHandler.OnNext()");
                }

                var onPreviousMethod = newInputType.GetMethod("OnPrevious", PublicInstance);
                if (onPreviousMethod != null)
                {
                    var prefix = typeof(EventSystemPatch).GetMethod(nameof(NewInputHandlerOnNextPrevious_Prefix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(onPreviousMethod, prefix: new HarmonyMethod(prefix));
                    Log.Msg("EventSystemPatch", "Patched NewInputHandler.OnPrevious()");
                }
            }

            // Patch RegistrationPanel.OnButton_SubmitRegistration to log calls and announce
            // guidance if the post-registration auto-login fails (known 403 with MelonLoader).
            var regPanelType = FindType("Wotc.Mtga.Login.RegistrationPanel");
            if (regPanelType != null)
            {
                var submitMethod = regPanelType.GetMethod("OnButton_SubmitRegistration", PublicInstance);
                if (submitMethod != null)
                {
                    var prefix = typeof(EventSystemPatch).GetMethod(nameof(SubmitRegistrationDiagnostic_Prefix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(submitMethod, prefix: new HarmonyMethod(prefix));
                    Log.Msg("EventSystemPatch", "Patched RegistrationPanel.OnButton_SubmitRegistration() (diagnostic)");
                }
            }
        }

        /// <summary>
        /// Block NewInputHandler.OnAccept() on the Login scene, unless the user is
        /// on the RegistrationPanel submit button (which needs the game's native path
        /// for the post-registration ConnectToFrontDoor flow).
        /// </summary>
        public static bool NewInputHandlerOnAccept_Prefix()
        {
            if (SceneManager.GetActiveScene().name == SceneNames.Login
                && !InputManager.AllowNativeEnterOnLogin)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Block NewInputHandler.OnNext()/OnPrevious() on the Login scene.
        /// Our mod handles all Tab/Shift+Tab navigation. Without this, Panel.OnNext()
        /// redirects focus from the Login button back to SelectOnLoad (the email field),
        /// trapping the user in a loop.
        /// </summary>
        public static bool NewInputHandlerOnNextPrevious_Prefix()
        {
            if (SceneManager.GetActiveScene().name == SceneNames.Login)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Prefix for RegistrationPanel.OnButton_SubmitRegistration().
        /// Logs the call for diagnostics and starts a delayed guidance announcement
        /// in case the post-registration auto-login fails (known 403 issue with MelonLoader).
        /// </summary>
        public static void SubmitRegistrationDiagnostic_Prefix()
        {
            Log.Msg("EventSystemPatch", ">>> OnButton_SubmitRegistration CALLED <<<");
            Log.Msg("EventSystemPatch", $"Stack: {System.Environment.StackTrace}");

            // Start a coroutine that waits and then announces guidance if the game
            // hasn't auto-advanced (ConnectToFrontDoor returns 403 with MelonLoader present).
            MelonCoroutines.Start(AnnounceRegistrationGuidanceAfterDelay());
        }

        /// <summary>
        /// Wait after registration submission. If the game hasn't left the Login scene
        /// (indicating the auto-login failed), announce guidance to the user.
        /// </summary>
        private static IEnumerator AnnounceRegistrationGuidanceAfterDelay()
        {
            yield return new WaitForSeconds(8f);

            // If the game auto-advanced to a different scene, no guidance needed
            if (SceneManager.GetActiveScene().name != SceneNames.Login)
                yield break;

            var announcer = AccessibleArenaMod.Instance?.Announcer;
            if (announcer == null)
                yield break;

            string message = LocaleManager.Instance?.Get("RegistrationSubmitted")
                ?? "Registration submitted. Please check your email to activate your account, then press Backspace to return to the login screen.";

            Log.Msg("EventSystemPatch", $"Registration did not auto-advance, announcing guidance");
            announcer.Announce(message, AnnouncementPriority.High);
        }

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
        ///
        /// Also blocks ALL move events when on a toggle, dropdown, or Login-scene element
        /// (BlockSubmitForToggle). Without this, Unity processes Move events independently
        /// of the mod's navigator — e.g. after toggling a checkbox, the EventSystem can
        /// navigate to an input field, triggering unwanted edit mode.
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

            // Block move events when on a toggle, dropdown, or Login-scene element.
            // The mod controls all navigation for these elements via its own element list.
            // Without this, EventSystem processes Move events independently and can
            // navigate to input fields after toggle state changes, causing FocusTracker
            // to enter edit mode for a field the user never intended to edit.
            if (InputManager.BlockSubmitForToggle)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Patch StandaloneInputModule.SendSubmitEventToSelectedObject to block Submit
        /// when our navigator is on a toggle element, when in dropdown mode,
        /// or when PhaseSkipGuard is warning before passing priority.
        ///
        /// This is the critical interception point for phase-skip warning. MTGA's
        /// input module may not call Input.GetButtonDown — it may use BaseInput or
        /// a custom override — so patching Input methods alone is insufficient.
        /// We must block here, where the actual submit dispatch happens.
        ///
        /// PhaseSkipGuard uses release-tracking to prevent oscillation: after showing
        /// the warning, it blocks every frame until Space is released. The next press
        /// after release confirms the pass.
        /// </summary>
        [HarmonyPatch(typeof(StandaloneInputModule), "SendSubmitEventToSelectedObject")]
        [HarmonyPrefix]
        public static bool SendSubmitEventToSelectedObject_Prefix()
        {
            // Block Submit when PhaseSkipGuard wants to warn before passing.
            // Input.GetKey(Space) distinguishes Space-triggered submit from Enter-triggered.
            // ShouldBlock() is frame-cached and handles release-tracking internally.
            if (Input.GetKey(KeyCode.Space) && PhaseSkipGuard.ShouldBlock())
            {
                return false;
            }

            // Block Submit when a browser is active - our mod handles Space via BrowserNavigator
            // Without this, Unity's EventSystem clicks the focused button (e.g., settings gear)
            // before our MelonLoader Update() can consume the key
            if (Input.GetKey(KeyCode.Space) && BrowserNavigator.IsActive)
            {
                return false;
            }

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
                Log.Msg("EventSystemPatch", "BLOCKED Submit - post-dropdown selection window");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Patch Input.GetKeyDown to block Enter key when on a toggle or dropdown.
        /// Also blocks Space in DuelScene when PhaseSkipGuard is active, to prevent
        /// KeyboardManager and direct callers from seeing Space.
        /// </summary>
        [HarmonyPatch(typeof(Input), nameof(Input.GetKeyDown), typeof(KeyCode))]
        [HarmonyPostfix]
        public static void GetKeyDown_Postfix(KeyCode key, ref bool __result)
        {
            // Only intercept when we're on a toggle/dropdown and the key is Enter
            if (InputManager.BlockSubmitForToggle && __result)
            {
                if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
                {
                    Log.Msg("EventSystemPatch", $"BLOCKED Input.GetKeyDown({key}) - on toggle/dropdown, setting EnterPressedWhileBlocked");
                    InputManager.EnterPressedWhileBlocked = true;
                    __result = false;
                }
            }

            // Block Space when PhaseSkipGuard is active (warning shown, waiting for release)
            if (key == KeyCode.Space && __result)
            {
                if (PhaseSkipGuard.ShouldBlock())
                {
                    __result = false;
                }
            }
        }

    }
}
