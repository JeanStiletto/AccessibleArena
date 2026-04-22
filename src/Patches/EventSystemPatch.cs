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

                // OnEscape fires Back?.Invoke() → Panel.OnBack → OnButton_GoBack, which
                // closes the panel and navigates back. On Login scene our mod owns Escape
                // (exits input-field edit mode, closes dropdowns), so block the game path.
                var onEscapeMethod = newInputType.GetMethod("OnEscape", PublicInstance);
                if (onEscapeMethod != null)
                {
                    var prefix = typeof(EventSystemPatch).GetMethod(nameof(NewInputHandlerOnEscape_Prefix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(onEscapeMethod, prefix: new HarmonyMethod(prefix));
                    Log.Msg("EventSystemPatch", "Patched NewInputHandler.OnEscape()");
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

                // Patch RegistrationPanel.OnRegisterError to announce server-side validation
                // failures (invalid email, password rejected, display name taken, etc.).
                // Without this, the user only sees visual feedback text on the input field,
                // which is unreadable by screen readers.
                var onRegisterErrorMethod = regPanelType.GetMethod("OnRegisterError", PrivateInstance);
                if (onRegisterErrorMethod != null)
                {
                    var postfix = typeof(EventSystemPatch).GetMethod(nameof(OnRegisterError_Postfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(onRegisterErrorMethod, postfix: new HarmonyMethod(postfix));
                    Log.Msg("EventSystemPatch", "Patched RegistrationPanel.OnRegisterError()");
                }
                else
                {
                    Log.Warn("EventSystemPatch", "Could not find RegistrationPanel.OnRegisterError method");
                }

                // Patch RegistrationPanel.OnRegisterSuccess to announce the "submitted,
                // check inbox" guidance when ConnectToFrontDoor hangs on 403 (known
                // MelonLoader issue — account was created, but auto-login fails, so the
                // user stays on Login and must validate via email + log in manually).
                var onRegisterSuccessMethod = regPanelType.GetMethod("OnRegisterSuccess", PrivateInstance);
                if (onRegisterSuccessMethod != null)
                {
                    var postfix = typeof(EventSystemPatch).GetMethod(nameof(OnRegisterSuccess_Postfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(onRegisterSuccessMethod, postfix: new HarmonyMethod(postfix));
                    Log.Msg("EventSystemPatch", "Patched RegistrationPanel.OnRegisterSuccess()");
                }
                else
                {
                    Log.Warn("EventSystemPatch", "Could not find RegistrationPanel.OnRegisterSuccess method");
                }
            }

            // Patch UIWidget_InputField_Registration.SetFeedbackText (both overloads) to
            // announce client-side validation errors. RegistrationPanel writes these
            // directly onto the field's FeedbackText (password too short, emails don't
            // match, password contains email, display name length, etc.) and disables
            // the submit button — the failure never reaches OnRegisterError, so without
            // this patch the user hears nothing and the submit button just silently
            // refuses to activate.
            var regFieldType = FindType("UIWidget_InputField_Registration");
            if (regFieldType != null)
            {
                var postfix = typeof(EventSystemPatch).GetMethod(nameof(SetFeedbackText_Postfix),
                    BindingFlags.Static | BindingFlags.Public);
                foreach (var m in regFieldType.GetMethods(PublicInstance))
                {
                    if (m.Name != "SetFeedbackText") continue;
                    harmony.Patch(m, postfix: new HarmonyMethod(postfix));
                    Log.Msg("EventSystemPatch",
                        $"Patched UIWidget_InputField_Registration.SetFeedbackText({m.GetParameters().Length} args)");
                }

                // Reset the dedup state when the field clears feedback (user re-selects
                // or successfully validates). Without this, the user would be silenced
                // on a legitimate re-occurrence of the same error after a fix attempt.
                var clearMethod = regFieldType.GetMethod("ClearFeedbackText", PublicInstance);
                if (clearMethod != null)
                {
                    var clearPostfix = typeof(EventSystemPatch).GetMethod(nameof(ClearFeedbackText_Postfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(clearMethod, postfix: new HarmonyMethod(clearPostfix));
                }
            }
            else
            {
                Log.Warn("EventSystemPatch", "Could not find UIWidget_InputField_Registration type");
            }

            // Patch cTMP_Dropdown.OnTextInput and OnNavigate to ignore keystrokes when
            // the dropdown is not visually expanded. cTMP_Dropdown.Hide() only destroys
            // its list after a 0.15s coroutine and only if IsActive() at the time.
            // If a containing panel hides (e.g. BirthLanguagePanel -> RegistrationPanel)
            // before the coroutine runs, the destroy is aborted — the DropdownList
            // GameObject lingers, parented to the root Canvas, with m_Items still alive.
            // Its alphabet-jump handler fires on any typed letter anywhere in the scene,
            // stealing EventSystem focus (e.g. 'n' jumps to "November", hijacking typing
            // into the Displayname input field on the Registration panel).
            var customDropdownType = FindType("cTMP.cTMP_Dropdown");
            if (customDropdownType != null)
            {
                var onTextInputMethod = customDropdownType.GetMethod("OnTextInput", PublicInstance);
                if (onTextInputMethod != null)
                {
                    var prefix = typeof(EventSystemPatch).GetMethod(nameof(CustomDropdownOnTextInput_Prefix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(onTextInputMethod, prefix: new HarmonyMethod(prefix));
                    Log.Msg("EventSystemPatch", "Patched cTMP_Dropdown.OnTextInput()");
                }

                var onNavigateMethod = customDropdownType.GetMethod("OnNavigate", PublicInstance);
                if (onNavigateMethod != null)
                {
                    var prefix = typeof(EventSystemPatch).GetMethod(nameof(CustomDropdownOnNavigate_Prefix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(onNavigateMethod, prefix: new HarmonyMethod(prefix));
                    Log.Msg("EventSystemPatch", "Patched cTMP_Dropdown.OnNavigate()");
                }
            }
            else
            {
                Log.Warn("EventSystemPatch", "Could not find cTMP.cTMP_Dropdown type");
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
        /// Block NewInputHandler.OnEscape() on the Login scene when the user is editing
        /// an input field, open on a dropdown, or otherwise in a mod-owned UI mode.
        /// Without this, Escape closes the active Panel via Panel.OnBack instead of
        /// just exiting edit mode.
        /// </summary>
        public static bool NewInputHandlerOnEscape_Prefix()
        {
            if (SceneManager.GetActiveScene().name != SceneNames.Login)
                return true;

            if (UIFocusTracker.IsAnyInputFieldFocused()
                || UIFocusTracker.IsEditingInputField()
                || UIFocusTracker.IsEditingDropdown()
                || InputManager.ModMenuActive
                || InputManager.PopupModeActive)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Prefix for RegistrationPanel.OnButton_SubmitRegistration().
        /// Diagnostic log only. The actual user-facing announcement is deferred until
        /// the promise resolves: OnRegisterSuccess_Postfix or OnRegisterError_Postfix.
        /// </summary>
        public static void SubmitRegistrationDiagnostic_Prefix()
        {
            Log.Msg("EventSystemPatch", ">>> OnButton_SubmitRegistration CALLED <<<");
            Log.Msg("EventSystemPatch", $"Stack: {System.Environment.StackTrace}");
        }

        /// <summary>
        /// Postfix for RegistrationPanel.OnRegisterSuccess(string).
        /// Account was created on the server. The game now calls ConnectToFrontDoor to
        /// auto-login — which 403s under MelonLoader, leaving the user stuck on the
        /// Login scene. Start a short timer: if we haven't left Login, announce the
        /// "submitted, validate via email, go back and log in" guidance.
        /// </summary>
        public static void OnRegisterSuccess_Postfix()
        {
            Log.Msg("EventSystemPatch", "OnRegisterSuccess fired — account created. Starting 403-detection timer.");
            MelonCoroutines.Start(AnnounceRegistrationGuidanceAfterDelay());
        }

        /// <summary>
        /// Postfix for RegistrationPanel.OnRegisterError(AccountError).
        /// Reads the server-localized error string from AccountError.LocalizedErrorMessage
        /// — the same text the game writes onto the input field's feedback label — and
        /// announces it. Falls back to a per-error-type mapping if the server string is
        /// missing. Also flags the guidance coroutine to suppress the "submitted" message.
        /// </summary>
        public static void OnRegisterError_Postfix(object error)
        {
            string errorTypeName = "Generic";
            string serverMessage = null;
            int httpCode = 0;
            try
            {
                if (error != null)
                {
                    var errorType = error.GetType();
                    errorTypeName = ReadMember(error, errorType, "ErrorType")?.ToString() ?? "Generic";
                    serverMessage = ReadMember(error, errorType, "LocalizedErrorMessage") as string;
                    var httpCodeObj = ReadMember(error, errorType, "HttpCode");
                    if (httpCodeObj is int hc) httpCode = hc;
                }
            }
            catch (System.Exception ex)
            {
                Log.Warn("EventSystemPatch", $"OnRegisterError: failed to read AccountError: {ex.Message}");
            }

            Log.Msg("EventSystemPatch",
                $"Registration error from server: ErrorType={errorTypeName}, HttpCode={httpCode}, Message='{serverMessage}'");

            var announcer = AccessibleArenaMod.Instance?.Announcer;
            if (announcer == null)
                return;

            // Known MelonLoader quirk: the RegisterAsFullAccount HTTP call returns 403
            // and routes down the Promise's error branch, but the server actually
            // created the account (validation email goes out). The localized message
            // the game shows ("unexpected problem, error code 403") is misleading —
            // announce the "submitted, check your inbox" guidance instead.
            // HttpCode is often 0 at runtime (WASUtils.ToAccountError doesn't populate
            // it), so also fall back to matching "403" in the localized message —
            // the digits are the same across all locales.
            bool is403 = httpCode == 403
                || (!string.IsNullOrEmpty(serverMessage) && serverMessage.Contains("403"));
            if (is403)
            {
                Log.Msg("EventSystemPatch",
                    "403 on RegisterAsFullAccount — account likely created despite error path. Announcing submitted guidance.");
                string guidance = LocaleManager.Instance?.Get("RegistrationSubmitted")
                    ?? "Registration submitted. Please check your email to activate your account, then press Backspace to return to the login screen.";
                announcer.Announce(guidance, AnnouncementPriority.High);
                return;
            }

            string announcement;
            if (!string.IsNullOrWhiteSpace(serverMessage))
            {
                // Prefix the real game message so the user knows registration failed
                // and hears the exact localized reason the game shows on screen.
                string prefix = LocaleManager.Instance?.Get("RegistrationErrorPrefix")
                    ?? "Registration failed";
                announcement = $"{prefix}: {serverMessage}";
            }
            else
            {
                string localeKey = "RegistrationError_" + errorTypeName;
                string mapped = LocaleManager.Instance?.Get(localeKey);
                announcement = (!string.IsNullOrEmpty(mapped) && mapped != localeKey)
                    ? mapped
                    : (LocaleManager.Instance?.Get("RegistrationError_Generic")
                        ?? "Registration failed. Please check your input and try again.");
            }

            announcer.Announce(announcement, AnnouncementPriority.High);
        }

        /// <summary>
        /// Last feedback text announced, to suppress duplicates when RegistrationPanel
        /// calls ClearFeedbackText + SetFeedbackText with the same message on every
        /// onEndEdit for an unchanged invalid field.
        /// </summary>
        private static string _lastAnnouncedFeedbackText;

        /// <summary>
        /// Postfix for UIWidget_InputField_Registration.SetFeedbackText(string[, bool]).
        /// Reads the trimmed feedback text the game just wrote and announces it with
        /// an "input error" prefix. The message is already localized by the game.
        /// </summary>
        public static void ClearFeedbackText_Postfix()
        {
            _lastAnnouncedFeedbackText = null;
        }

        public static void SetFeedbackText_Postfix(object __instance)
        {
            if (__instance == null) return;
            try
            {
                var feedbackProp = __instance.GetType().GetProperty("FeedbackText", PublicInstance);
                var tmpText = feedbackProp?.GetValue(__instance);
                if (tmpText == null) return;

                var textProp = tmpText.GetType().GetProperty("text");
                string text = textProp?.GetValue(tmpText) as string;
                if (string.IsNullOrWhiteSpace(text)) return;
                if (text == _lastAnnouncedFeedbackText) return;
                _lastAnnouncedFeedbackText = text;

                var announcer = AccessibleArenaMod.Instance?.Announcer;
                if (announcer == null) return;

                string prefix = LocaleManager.Instance?.Get("InputErrorPrefix")
                    ?? "Input error";
                announcer.Announce($"{prefix}: {text}", AnnouncementPriority.High);
            }
            catch (System.Exception ex)
            {
                Log.Warn("EventSystemPatch", $"SetFeedbackText_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Cached FieldInfo for cTMP_Dropdown._expanded (resolved lazily on first use,
        /// then reused). Null means the field was not found (prefix will fall through).
        /// </summary>
        private static FieldInfo _customDropdownExpandedField;
        private static bool _customDropdownExpandedResolved;

        /// <summary>
        /// Returns true if the cTMP_Dropdown instance is currently visually expanded.
        /// Reads the private _expanded field. If the field can't be resolved, returns
        /// true (safe fallback — lets the game's handler run normally).
        /// </summary>
        private static bool IsCustomDropdownExpanded(object dropdown)
        {
            if (dropdown == null) return false;
            if (!_customDropdownExpandedResolved)
            {
                _customDropdownExpandedField = dropdown.GetType().GetField("_expanded", PrivateInstance);
                _customDropdownExpandedResolved = true;
            }
            if (_customDropdownExpandedField == null) return true;
            try { return (bool)_customDropdownExpandedField.GetValue(dropdown); }
            catch { return true; }
        }

        /// <summary>
        /// Prefix for cTMP_Dropdown.OnTextInput(char). Skips the alphabet-jump handler
        /// when the dropdown list is not visually expanded. Prevents lingering (invisible)
        /// dropdown lists from hijacking keystrokes on unrelated UI (e.g. typing into an
        /// input field on a different panel).
        /// </summary>
        public static bool CustomDropdownOnTextInput_Prefix(object __instance)
        {
            return IsCustomDropdownExpanded(__instance);
        }

        /// <summary>
        /// Prefix for cTMP_Dropdown.OnNavigate(Direction). Same guard as OnTextInput —
        /// arrow-key navigation through a non-visible dropdown's items would also steal
        /// focus from whatever the user is actually interacting with.
        /// </summary>
        public static bool CustomDropdownOnNavigate_Prefix(object __instance)
        {
            return IsCustomDropdownExpanded(__instance);
        }

        /// <summary>
        /// Read a member (property or field) from an object via reflection, preferring
        /// property over field. Returns null if missing or on failure.
        /// </summary>
        private static object ReadMember(object target, System.Type type, string name)
        {
            var prop = type.GetProperty(name, AllInstanceFlags);
            if (prop != null && prop.CanRead)
                return prop.GetValue(target);
            var field = type.GetField(name, AllInstanceFlags);
            return field?.GetValue(target);
        }

        /// <summary>
        /// Started after OnRegisterSuccess fires (account created). If ConnectToFrontDoor
        /// succeeds, the scene changes and we stay quiet. If it 403s under MelonLoader,
        /// the scene stays on Login — announce the guidance so the user knows registration
        /// actually worked and what to do next (validate via email, go back, log in).
        /// </summary>
        private static IEnumerator AnnounceRegistrationGuidanceAfterDelay()
        {
            yield return new WaitForSeconds(8f);

            // Game auto-advanced → ConnectToFrontDoor succeeded → nothing to say
            if (SceneManager.GetActiveScene().name != SceneNames.Login)
                yield break;

            var announcer = AccessibleArenaMod.Instance?.Announcer;
            if (announcer == null)
                yield break;

            string message = LocaleManager.Instance?.Get("RegistrationSubmitted")
                ?? "Registration submitted. Please check your email to activate your account, then press Backspace to return to the login screen.";

            Log.Msg("EventSystemPatch", "ConnectToFrontDoor did not auto-advance (likely 403), announcing submitted guidance");
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
