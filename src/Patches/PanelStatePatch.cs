using HarmonyLib;
using MelonLoader;
using AccessibleArena.Core.Utils;
using System;
using System.Linq;
using System.Reflection;
using AccessibleArena.Core.Services;
using AccessibleArena.Core.Services.ElementGrouping;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;

namespace AccessibleArena.Patches
{
    /// <summary>
    /// Harmony patch for intercepting panel state changes from game controllers.
    /// This allows us to detect when panels open/close without polling.
    ///
    /// Patches NavContentController and similar classes to get notified
    /// when IsOpen changes or Show/Hide methods are called.
    /// </summary>
    public static class PanelStatePatch
    {
        private static bool _patchApplied = false;

        /// <summary>
        /// Event fired when a panel's open state changes.
        /// Parameters: (controller instance, isOpen, controllerTypeName)
        /// </summary>
        public static event Action<object, bool, string> OnPanelStateChanged;

        /// <summary>
        /// Event fired when a mail letter is selected/opened in the mailbox.
        /// Parameters: (letterId, title, body, hasAttachments, isClaimed)
        /// </summary>
        public static event Action<Guid, string, string, bool, bool> OnMailLetterSelected;

        /// <summary>
        /// Manually applies the Harmony patch after game assemblies are loaded.
        /// Called during mod initialization.
        /// </summary>
        public static void Initialize()
        {
            if (_patchApplied) return;

            try
            {
                var harmony = new HarmonyLib.Harmony("com.accessibility.mtga.panelstatepatch");

                // Try to patch NavContentController
                PatchNavContentController(harmony);

                // Try to patch SettingsMenu
                PatchSettingsMenu(harmony);

                // Try to patch ConstructedDeckSelectController (deck selection panel)
                PatchDeckSelectController(harmony);

                // Try to patch PlayBladeController (play mode selection)
                PatchPlayBladeController(harmony);

                // Try to patch HomePageContentController blade states
                PatchHomePageBladeStates(harmony);

                // Try to patch JoinMatchMaking for bot match interception
                PatchJoinMatchMaking(harmony);

                // Try to patch BladeContentView base class (Events, FindMatch, LastPlayed blades)
                PatchBladeContentView(harmony);

                // Try to patch SocialUI (friends panel)
                PatchSocialUI(harmony);

                // Try to patch ContentControllerPlayerInbox (mailbox)
                PatchMailboxController(harmony);

                _patchApplied = true;
                Log.Patch("PanelStatePatch", $"Harmony patches applied successfully");

                // Discover other potential controller types for future patching
                DiscoverPanelTypes();
            }
            catch (Exception ex)
            {
                Log.Error("PanelStatePatch", $"Initialization error: {ex}");
            }
        }

        /// <summary>
        /// Wires a Harmony postfix by name onto <paramref name="method"/>, logs success under
        /// <paramref name="label"/>, and swallows/logs reflection errors so one broken patch
        /// can't abort the rest of Initialize().
        /// </summary>
        private static void TryPatchPostfix(HarmonyLib.Harmony harmony, MethodBase method, string postfixName, string label)
        {
            if (method == null) return;
            try
            {
                var postfix = typeof(PanelStatePatch).GetMethod(postfixName, BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                Log.Patch("PanelStatePatch", $"Patched {label}");
            }
            catch (Exception ex)
            {
                Log.Warn("PanelStatePatch", $"Failed to patch {label}: {ex.Message}");
            }
        }

        /// <summary>
        /// Same as <see cref="TryPatchPostfix"/> but wires both a prefix and a postfix.
        /// </summary>
        private static void TryPatchPrefixPostfix(HarmonyLib.Harmony harmony, MethodBase method, string prefixName, string postfixName, string label)
        {
            if (method == null) return;
            try
            {
                var prefix = typeof(PanelStatePatch).GetMethod(prefixName, BindingFlags.Static | BindingFlags.Public);
                var postfix = typeof(PanelStatePatch).GetMethod(postfixName, BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(method, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
                Log.Patch("PanelStatePatch", $"Patched {label}");
            }
            catch (Exception ex)
            {
                Log.Warn("PanelStatePatch", $"Failed to patch {label}: {ex.Message}");
            }
        }

        /// <summary>
        /// Same as <see cref="TryPatchPostfix"/> but wires only a prefix.
        /// </summary>
        private static void TryPatchPrefix(HarmonyLib.Harmony harmony, MethodBase method, string prefixName, string label)
        {
            if (method == null) return;
            try
            {
                var prefix = typeof(PanelStatePatch).GetMethod(prefixName, BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                Log.Patch("PanelStatePatch", $"Patched {label}");
            }
            catch (Exception ex)
            {
                Log.Warn("PanelStatePatch", $"Failed to patch {label}: {ex.Message}");
            }
        }

        /// <summary>
        /// Fires <see cref="OnPanelStateChanged"/> with debug logging, guarded by a try/catch so a
        /// subscriber exception can't leak back into the patched game method.
        /// </summary>
        private static void FirePanelStateChange(object instance, bool isOpen, string name, string logPrefix)
        {
            try
            {
                Log.Patch("PanelStatePatch", $"{logPrefix} (isOpen: {isOpen})");
                OnPanelStateChanged?.Invoke(instance, isOpen, name);
            }
            catch (Exception ex)
            {
                Log.Warn("PanelStatePatch", $"Error firing {logPrefix}: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns true when the Tab key is held, in which case the caller should skip the
        /// patched SocialUI method. Our mod uses Tab for navigation — we never want Tab to
        /// open, close, or toggle the friends panel / chat window.
        /// Pass log=false for postfix sites that always pair with a prefix site (the prefix
        /// already logged the block; the postfix would just duplicate the line).
        /// </summary>
        private static bool ShouldBlockSocialUITabToggle(string whatWasBlocked, bool log = true)
        {
            if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.Tab))
            {
                if (log) Log.Patch("PanelStatePatch", $"Blocked {whatWasBlocked} (Tab pressed)");
                return true;
            }
            return false;
        }

        private static void PatchNavContentController(HarmonyLib.Harmony harmony)
        {
            var controllerType = FindType(T.NavContentControllerFQ);
            if (controllerType == null)
            {
                // Try alternative names
                controllerType = FindType(T.NavContentController);
            }

            if (controllerType == null)
            {
                Log.Warn("PanelStatePatch", "Could not find NavContentController type");
                return;
            }

            Log.Patch("PanelStatePatch", $"Found NavContentController: {controllerType.FullName}");

            // Log available methods/properties for debugging
            LogTypeMembers(controllerType);

            // NavContentController uses BeginOpen/FinishOpen/BeginClose/FinishClose lifecycle methods.
            // FinishOpen/FinishClose fire after animations complete; BeginOpen/BeginClose fire earlier
            // for callers that need pre-animation notification; IsOpen setter is a backup.
            var finishOpen = controllerType.GetMethod("FinishOpen", AllInstanceFlags);
            if (finishOpen == null) Log.Warn("PanelStatePatch", "Could not find NavContentController.FinishOpen()");
            TryPatchPostfix(harmony, finishOpen, nameof(ShowPostfix), "NavContentController.FinishOpen()");

            var finishClose = controllerType.GetMethod("FinishClose", AllInstanceFlags);
            if (finishClose == null) Log.Warn("PanelStatePatch", "Could not find NavContentController.FinishClose()");
            TryPatchPostfix(harmony, finishClose, nameof(HidePostfix), "NavContentController.FinishClose()");

            TryPatchPostfix(harmony, controllerType.GetMethod("BeginOpen", AllInstanceFlags),
                nameof(BeginOpenPostfix), "NavContentController.BeginOpen()");
            TryPatchPostfix(harmony, controllerType.GetMethod("BeginClose", AllInstanceFlags),
                nameof(BeginClosePostfix), "NavContentController.BeginClose()");
            TryPatchPostfix(harmony, controllerType.GetProperty("IsOpen", AllInstanceFlags)?.GetSetMethod(true),
                nameof(IsOpenSetterPostfix), "NavContentController.IsOpen setter");
        }

        /// <summary>
        /// Discovers and logs all types that might be panel controllers.
        /// Called once after patches are applied to find what else might need patching.
        /// </summary>
        public static void DiscoverPanelTypes()
        {
            Log.Patch("PanelStatePatch", $"=== Discovering potential panel controller types ===");

            var keywords = new[] { "Deck", "Selection", "Picker", "Panel", "Controller", "Modal", "Dialog", "Overlay", "Screen", "Blade", "PlayBlade", "Login", "Welcome", "Register", "Gate" };

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    // Skip system assemblies
                    if (assembly.FullName.StartsWith("System") || assembly.FullName.StartsWith("mscorlib"))
                        continue;

                    foreach (var type in assembly.GetTypes())
                    {
                        // Check if type name contains any keyword
                        bool hasKeyword = false;
                        foreach (var kw in keywords)
                        {
                            if (type.Name.Contains(kw))
                            {
                                hasKeyword = true;
                                break;
                            }
                        }

                        if (!hasKeyword) continue;

                        // Check if type has IsOpen, Show, or Hide
                        var hasIsOpen = type.GetProperty("IsOpen", AllInstanceFlags) != null;
                        var hasShow = type.GetMethod("Show", AllInstanceFlags) != null;
                        var hasHide = type.GetMethod("Hide", AllInstanceFlags) != null;

                        if (hasIsOpen || hasShow || hasHide)
                        {
                            Log.Patch("PanelStatePatch", $"Found: {type.FullName} - IsOpen:{hasIsOpen} Show:{hasShow} Hide:{hasHide}");
                        }
                    }
                }
                catch
                {
                    // Ignore assembly load errors
                }
            }

            Log.Patch("PanelStatePatch", $"=== Discovery complete ===");
        }

        private static void PatchSettingsMenu(HarmonyLib.Harmony harmony)
        {
            var settingsType = FindType(T.SettingsMenuFQ);
            if (settingsType == null)
            {
                settingsType = FindType(T.SettingsMenu);
            }

            if (settingsType == null)
            {
                Log.Warn("PanelStatePatch", "Could not find SettingsMenu type");
                return;
            }

            Log.Patch("PanelStatePatch", $"Found SettingsMenu: {settingsType.FullName}");

            // Log all methods to discover correct signatures
            LogTypeMembers(settingsType);

            // Patch every Show/Open/FinishOpen/BeginOpen overload we can find - the class has
            // multiple entrypoints and we want to catch them all without guessing parameter lists.
            foreach (var method in settingsType.GetMethods(AllInstanceFlags)
                .Where(m => m.Name == "Show" || m.Name == "Open" || m.Name == "FinishOpen" || m.Name == "BeginOpen"))
            {
                TryPatchPostfix(harmony, method, nameof(SettingsShowPostfix), $"SettingsMenu.{method.Name}()");
            }
            foreach (var method in settingsType.GetMethods(AllInstanceFlags)
                .Where(m => m.Name == "Hide" || m.Name == "Close" || m.Name == "FinishClose" || m.Name == "BeginClose"))
            {
                TryPatchPostfix(harmony, method, nameof(SettingsHidePostfix), $"SettingsMenu.{method.Name}()");
            }

            TryPatchPostfix(harmony, settingsType.GetProperty("IsOpen", AllInstanceFlags)?.GetSetMethod(true),
                nameof(SettingsIsOpenPostfix), "SettingsMenu.IsOpen setter");
            TryPatchPostfix(harmony, settingsType.GetProperty("IsMainPanelActive", AllInstanceFlags)?.GetSetMethod(true),
                nameof(SettingsMainPanelPostfix), "SettingsMenu.IsMainPanelActive setter");
        }

        private static void PatchDeckSelectController(HarmonyLib.Harmony harmony)
        {
            // DeckSelectBlade has Show/Hide methods which are called when deck selection opens/closes
            var deckBladeType = FindType(T.DeckSelectBlade);
            if (deckBladeType == null)
            {
                Log.Warn("PanelStatePatch", "Could not find DeckSelectBlade type");
                return;
            }

            Log.Patch("PanelStatePatch", $"Found DeckSelectBlade: {deckBladeType.FullName}");

            // Log available methods/properties for debugging
            LogTypeMembers(deckBladeType);

            foreach (var method in deckBladeType.GetMethods(AllInstanceFlags).Where(m => m.Name == "Show"))
            {
                var paramStr = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
                TryPatchPostfix(harmony, method, nameof(DeckSelectShowPostfix), $"DeckSelectBlade.Show({paramStr})");
            }

            var hide = deckBladeType.GetMethod("Hide", AllInstanceFlags);
            if (hide == null) Log.Warn("PanelStatePatch", "Could not find Hide method on DeckSelectBlade");
            TryPatchPostfix(harmony, hide, nameof(DeckSelectHidePostfix), "DeckSelectBlade.Hide()");

            TryPatchPostfix(harmony, deckBladeType.GetProperty("IsShowing", AllInstanceFlags)?.GetSetMethod(true),
                nameof(DeckSelectIsShowingPostfix), "DeckSelectBlade.IsShowing setter");
        }

        private static void PatchPlayBladeController(HarmonyLib.Harmony harmony)
        {
            var playBladeType = FindType(T.PlayBladeController);
            if (playBladeType == null)
            {
                Log.Warn("PanelStatePatch", "Could not find PlayBladeController type");
                return;
            }

            Log.Patch("PanelStatePatch", $"Found PlayBladeController: {playBladeType.FullName}");

            // PlayBladeVisualState changes when play blade opens/closes. IsDeckSelected is get-only
            // (delegates to _activeBladeWidget.IsDeckSelected) - no patch needed, deck selection
            // is handled via DeckView.OnDeckClick().
            var visualStateSetter = playBladeType.GetProperty("PlayBladeVisualState", AllInstanceFlags)?.GetSetMethod(true);
            if (visualStateSetter == null)
                Log.Warn("PanelStatePatch", "Could not find PlayBladeController.PlayBladeVisualState setter");
            TryPatchPostfix(harmony, visualStateSetter, nameof(PlayBladeVisualStatePostfix),
                "PlayBladeController.PlayBladeVisualState setter");
        }

        private static void PatchHomePageBladeStates(HarmonyLib.Harmony harmony)
        {
            var homePageType = FindType(T.HomePageContentController);
            if (homePageType == null)
            {
                Log.Warn("PanelStatePatch", "Could not find HomePageContentController type");
                return;
            }

            Log.Patch("PanelStatePatch", $"Found HomePageContentController: {homePageType.FullName}");

            var eventSetter = homePageType.GetProperty("IsEventBladeActive", AllInstanceFlags)?.GetSetMethod(true);
            if (eventSetter == null)
                Log.Warn("PanelStatePatch", "Could not find HomePageContentController.IsEventBladeActive setter");
            TryPatchPostfix(harmony, eventSetter, nameof(IsEventBladeActivePostfix),
                "HomePageContentController.IsEventBladeActive setter");

            var directSetter = homePageType.GetProperty("IsDirectChallengeBladeActive", AllInstanceFlags)?.GetSetMethod(true);
            if (directSetter == null)
                Log.Warn("PanelStatePatch", "Could not find HomePageContentController.IsDirectChallengeBladeActive setter");
            TryPatchPostfix(harmony, directSetter, nameof(IsDirectChallengeBladeActivePostfix),
                "HomePageContentController.IsDirectChallengeBladeActive setter");
        }

        private static void PatchJoinMatchMaking(HarmonyLib.Harmony harmony)
        {
            var homePageType = FindType(T.HomePageContentController);
            if (homePageType == null)
            {
                Log.Warn("PanelStatePatch", "Could not find HomePageContentController for JoinMatchMaking patch");
                return;
            }

            var joinMethod = homePageType.GetMethod("JoinMatchMaking", PrivateInstance);
            if (joinMethod == null)
            {
                Log.Warn("PanelStatePatch", "Could not find HomePageContentController.JoinMatchMaking method");
                return;
            }
            TryPatchPrefix(harmony, joinMethod, nameof(JoinMatchMakingPrefix),
                "HomePageContentController.JoinMatchMaking (bot-match interception)");
        }

        /// <summary>
        /// Harmony prefix for HomePageContentController.JoinMatchMaking(string, Guid).
        /// When Bot Match mode is active, replaces the event name with "AIBotMatch"
        /// so the game routes to bot match instead of regular matchmaking.
        /// </summary>
        public static void JoinMatchMakingPrefix(ref string internalEventName)
        {
            if (PlayBladeNavigationHelper.IsBotMatchMode)
            {
                Log.Msg("PanelStatePatch", $"Bot Match mode active, replacing '{internalEventName}' with 'AIBotMatch'");
                internalEventName = "AIBotMatch";
                PlayBladeNavigationHelper.SetBotMatchMode(false);
            }
        }

        private static void PatchBladeContentView(HarmonyLib.Harmony harmony)
        {
            // Try to patch the base BladeContentView class for Show/Hide
            var bladeContentViewType = FindType(T.BladeContentViewFQ);
            if (bladeContentViewType == null)
            {
                bladeContentViewType = FindType(T.BladeContentView);
            }

            if (bladeContentViewType != null)
            {
                Log.Patch("PanelStatePatch", $"Found BladeContentView: {bladeContentViewType.FullName}");
                TryPatchPostfix(harmony, bladeContentViewType.GetMethod("Show", AllInstanceFlags),
                    nameof(BladeContentViewShowPostfix), "BladeContentView.Show()");
                TryPatchPostfix(harmony, bladeContentViewType.GetMethod("Hide", AllInstanceFlags),
                    nameof(BladeContentViewHidePostfix), "BladeContentView.Hide()");
            }
            else
            {
                Log.Warn("PanelStatePatch", "Could not find BladeContentView type");
            }

            // Also try to patch EventBladeContentView directly (has Show/Hide)
            var eventBladeType = FindType(T.EventBladeContentViewFQ) ?? FindType(T.EventBladeContentView);
            if (eventBladeType != null)
            {
                Log.Patch("PanelStatePatch", $"Found EventBladeContentView: {eventBladeType.FullName}");
                TryPatchPostfix(harmony, eventBladeType.GetMethod("Show", AllInstanceFlags),
                    nameof(EventBladeShowPostfix), "EventBladeContentView.Show()");
                TryPatchPostfix(harmony, eventBladeType.GetMethod("Hide", AllInstanceFlags),
                    nameof(EventBladeHidePostfix), "EventBladeContentView.Hide()");
            }
            else
            {
                Log.Warn("PanelStatePatch", "Could not find EventBladeContentView type");
            }
        }

        private static void PatchSocialUI(HarmonyLib.Harmony harmony)
        {
            var socialUIType = FindType(T.SocialUI);
            if (socialUIType == null)
            {
                Log.Warn("PanelStatePatch", "Could not find SocialUI type");
                return;
            }

            Log.Patch("PanelStatePatch", $"Found SocialUI: {socialUIType.FullName}");

            // Show/close methods: prefix blocks Tab-triggered calls, postfix fires panel-state event.
            TryPatchPrefixPostfix(harmony, socialUIType.GetMethod("ShowSocialEntitiesList", AllInstanceFlags),
                nameof(SocialUIShowPrefix), nameof(SocialUIShowPostfix), "SocialUI.ShowSocialEntitiesList()");
            TryPatchPrefixPostfix(harmony, socialUIType.GetMethod("CloseFriendsWidget", AllInstanceFlags),
                nameof(SocialUIClosePrefix), nameof(SocialUIHidePostfix), "SocialUI.CloseFriendsWidget()");
            TryPatchPrefixPostfix(harmony, socialUIType.GetMethod("Minimize", AllInstanceFlags),
                nameof(SocialUIClosePrefix), nameof(SocialUIHidePostfix), "SocialUI.Minimize()");
            TryPatchPrefixPostfix(harmony, socialUIType.GetMethod("SetVisible", AllInstanceFlags),
                nameof(SocialUISetVisiblePrefix), nameof(SocialUISetVisiblePostfix), "SocialUI.SetVisible()");

            // Prefix-only patches: block Tab from toggling social panel / opening chat via any code path.
            TryPatchPrefix(harmony, socialUIType.GetMethod("HandleKeyDown", AllInstanceFlags),
                nameof(SocialUIHandleKeyDownPrefix), "SocialUI.HandleKeyDown()");
            TryPatchPrefix(harmony, socialUIType.GetMethod("ShowChatWindow", AllInstanceFlags),
                nameof(SocialUIShowChatWindowPrefix), "SocialUI.ShowChatWindow()");
        }

        private static void PatchMailboxController(HarmonyLib.Harmony harmony)
        {
            // Mailbox is controlled by NavBarController, not a dedicated content controller
            // NavBarController has MailboxButton_OnClick() to open and HideInboxIfActive() to close
            var navBarType = FindType(T.NavBarController);
            if (navBarType == null)
            {
                Log.Warn("PanelStatePatch", "Could not find NavBarController type for mailbox patching");
                return;
            }

            Log.Patch("PanelStatePatch", $"Found NavBarController for mailbox: {navBarType.FullName}");

            var openMethod = navBarType.GetMethod("MailboxButton_OnClick", AllInstanceFlags);
            if (openMethod == null) Log.Warn("PanelStatePatch", "NavBarController.MailboxButton_OnClick not found");
            TryPatchPostfix(harmony, openMethod, nameof(MailboxOpenPostfix), "NavBarController.MailboxButton_OnClick()");

            var closeMethod = navBarType.GetMethod("HideInboxIfActive", AllInstanceFlags);
            if (closeMethod == null) Log.Warn("PanelStatePatch", "NavBarController.HideInboxIfActive not found");
            TryPatchPostfix(harmony, closeMethod, nameof(MailboxClosePostfix), "NavBarController.HideInboxIfActive()");

            // Patch ContentControllerPlayerInbox.OnLetterSelected - called when a mail is opened
            PatchMailLetterSelected(harmony);
        }

        private static void PatchMailLetterSelected(HarmonyLib.Harmony harmony)
        {
            var inboxType = FindType(T.ContentControllerPlayerInboxFQ);
            if (inboxType == null)
            {
                Log.Warn("PanelStatePatch", "Could not find ContentControllerPlayerInbox type");
                return;
            }

            Log.Patch("PanelStatePatch", $"Found ContentControllerPlayerInbox: {inboxType.FullName}");

            var onLetterSelectedMethod = inboxType.GetMethod("OnLetterSelected", AllInstanceFlags);
            if (onLetterSelectedMethod == null)
                Log.Warn("PanelStatePatch", "ContentControllerPlayerInbox.OnLetterSelected not found");
            TryPatchPostfix(harmony, onLetterSelectedMethod, nameof(MailLetterSelectedPostfix),
                "ContentControllerPlayerInbox.OnLetterSelected()");
        }

        public static void MailboxOpenPostfix(object __instance)
            => FirePanelStateChange(__instance, true, "Mailbox", "Mailbox opened");

        public static void MailboxClosePostfix(object __instance)
            => FirePanelStateChange(__instance, false, "Mailbox", "Mailbox closed");

        /// <summary>
        /// Postfix for ContentControllerPlayerInbox.OnLetterSelected
        /// Parameters: selectedLetter (PlayerInboxBladeItemDisplay), isRead (bool), selectedLetterId (Guid)
        /// </summary>
        public static void MailLetterSelectedPostfix(object __instance, object selectedLetter, bool isRead, Guid selectedLetterId)
        {
            try
            {
                Log.Patch("PanelStatePatch", $"Mail letter selected: {selectedLetterId}, isRead: {isRead}");

                // Get the ClientLetterViewModel from the selectedLetter (PlayerInboxBladeItemDisplay)
                // It has a _clientBladeItemViewModel property
                string title = "";
                string body = "";
                bool hasAttachments = false;
                bool isClaimed = false;

                if (selectedLetter != null)
                {
                    var selectedLetterType = selectedLetter.GetType();

                    // Try to get the view model field (it's a field, not a property)
                    var viewModelField = selectedLetterType.GetField("_clientBladeItemViewModel",
                        AllInstanceFlags);

                    if (viewModelField != null)
                    {
                        var viewModel = viewModelField.GetValue(selectedLetter);
                        if (viewModel != null)
                        {
                            var vmType = viewModel.GetType();

                            // Get Title field
                            var titleField = vmType.GetField("Title", PublicInstance);
                            if (titleField != null)
                                title = titleField.GetValue(viewModel) as string ?? "";

                            // Get Body field
                            var bodyField = vmType.GetField("Body", PublicInstance);
                            if (bodyField != null)
                                body = bodyField.GetValue(viewModel) as string ?? "";

                            // Get Attachments field (List)
                            var attachmentsField = vmType.GetField("Attachments", PublicInstance);
                            if (attachmentsField != null)
                            {
                                var attachments = attachmentsField.GetValue(viewModel) as System.Collections.IList;
                                hasAttachments = attachments != null && attachments.Count > 0;
                            }

                            // Get IsClaimed field
                            var isClaimedField = vmType.GetField("IsClaimed", PublicInstance);
                            if (isClaimedField != null)
                                isClaimed = (bool)isClaimedField.GetValue(viewModel);

                            Log.Patch("PanelStatePatch", $"Letter data - Title: {title}, Body length: {body?.Length ?? 0}, HasAttachments: {hasAttachments}, IsClaimed: {isClaimed}");
                        }
                        else
                        {
                            Log.Warn("PanelStatePatch", "viewModel is null");
                        }
                    }
                    else
                    {
                        Log.Warn("PanelStatePatch", $"_clientBladeItemViewModel field not found on {selectedLetterType.Name}");
                    }
                }

                OnMailLetterSelected?.Invoke(selectedLetterId, title, body, hasAttachments, isClaimed);
            }
            catch (Exception ex)
            {
                Log.Warn("PanelStatePatch", $"Error in MailLetterSelectedPostfix: {ex.Message}");
            }
        }

        private static void LogTypeMembers(Type type)
        {
            Log.Patch("PanelStatePatch", $"Methods on {type.Name}:");
            foreach (var m in type.GetMethods(AllInstanceFlags))
            {
                if (m.Name.Contains("Show") || m.Name.Contains("Hide") || m.Name.Contains("Open") || m.Name.Contains("Close"))
                {
                    Log.Patch("PanelStatePatch", $"  - {m.Name}({string.Join(", ", Array.ConvertAll(m.GetParameters(), p => p.ParameterType.Name))})");
                }
            }

            Log.Patch("PanelStatePatch", $"Properties on {type.Name}:");
            foreach (var p in type.GetProperties(AllInstanceFlags))
            {
                if (p.Name.Contains("Open") || p.Name.Contains("Ready") || p.Name.Contains("Visible"))
                {
                    Log.Patch("PanelStatePatch", $"  - {p.Name} ({p.PropertyType.Name})");
                }
            }
        }

        // FindType provided by ReflectionUtils via using static

        // === Postfix Methods ===

        public static void ShowPostfix(object __instance)
        {
            try
            {
                var typeName = __instance?.GetType().Name ?? "Unknown";
                Log.Patch("PanelStatePatch", $"Panel Show: {typeName}");
                OnPanelStateChanged?.Invoke(__instance, true, typeName);

                // Check if this is a PlayBlade controller by GameObject name
                if (__instance is UnityEngine.MonoBehaviour mb && mb.gameObject.name.Contains("PlayBlade"))
                {
                    Log.Patch("PanelStatePatch", $"Detected PlayBlade Show via GameObject name: {mb.gameObject.name}");
                    OnPanelStateChanged?.Invoke(__instance, true, "PlayBlade:Generic");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("PanelStatePatch", $"Error in ShowPostfix: {ex.Message}");
            }
        }

        public static void HidePostfix(object __instance)
        {
            try
            {
                var typeName = __instance?.GetType().Name ?? "Unknown";
                Log.Patch("PanelStatePatch", $"Panel Hide: {typeName}");
                OnPanelStateChanged?.Invoke(__instance, false, typeName);

                // Check if this is a PlayBlade controller by GameObject name
                if (__instance is UnityEngine.MonoBehaviour mb && mb.gameObject.name.Contains("PlayBlade"))
                {
                    Log.Patch("PanelStatePatch", $"Detected PlayBlade Hide via GameObject name: {mb.gameObject.name}");
                    OnPanelStateChanged?.Invoke(__instance, false, "PlayBlade:Generic");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("PanelStatePatch", $"Error in HidePostfix: {ex.Message}");
            }
        }

        public static void IsOpenSetterPostfix(object __instance, bool value)
        {
            try
            {
                var typeName = __instance?.GetType().Name ?? "Unknown";
                Log.Patch("PanelStatePatch", $"Panel IsOpen = {value}: {typeName}");
                OnPanelStateChanged?.Invoke(__instance, value, typeName);

                // Check if this is a PlayBlade controller by GameObject name
                if (__instance is UnityEngine.MonoBehaviour mb && mb.gameObject.name.Contains("PlayBlade"))
                {
                    Log.Patch("PanelStatePatch", $"Detected PlayBlade IsOpen={value} via GameObject name: {mb.gameObject.name}");
                    OnPanelStateChanged?.Invoke(__instance, value, "PlayBlade:Generic");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("PanelStatePatch", $"Error in IsOpenSetterPostfix: {ex.Message}");
            }
        }

        public static void SettingsShowPostfix(object __instance)
            => FirePanelStateChange(__instance, true, "SettingsMenu", "SettingsMenu Show");

        public static void SettingsHidePostfix(object __instance)
            => FirePanelStateChange(__instance, false, "SettingsMenu", "SettingsMenu Hide");

        public static void DeckSelectShowPostfix(object __instance)
            => FirePanelStateChange(__instance, true, "DeckSelectBlade", "DeckSelectBlade Show");

        public static void DeckSelectHidePostfix(object __instance)
            => FirePanelStateChange(__instance, false, "DeckSelectBlade", "DeckSelectBlade Hide");

        public static void BeginOpenPostfix(object __instance)
        {
            try
            {
                var typeName = __instance?.GetType().Name ?? "Unknown";
                Log.Patch("PanelStatePatch", $"Panel BeginOpen: {typeName}");
                // Don't fire event yet - wait for FinishOpen when UI is ready

                // But for PlayBlade, fire early so we track blade state
                if (__instance is UnityEngine.MonoBehaviour mb && mb.gameObject.name.Contains("PlayBlade"))
                {
                    Log.Patch("PanelStatePatch", $"Detected PlayBlade BeginOpen via GameObject name: {mb.gameObject.name}");
                    OnPanelStateChanged?.Invoke(__instance, true, "PlayBlade:Generic");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("PanelStatePatch", $"Error in BeginOpenPostfix: {ex.Message}");
            }
        }

        public static void BeginClosePostfix(object __instance)
        {
            try
            {
                var typeName = __instance?.GetType().Name ?? "Unknown";
                Log.Patch("PanelStatePatch", $"Panel BeginClose: {typeName}");
                // Fire event early so navigator knows panel is closing
                OnPanelStateChanged?.Invoke(__instance, false, typeName);

                // Check if this is a PlayBlade controller by GameObject name
                if (__instance is UnityEngine.MonoBehaviour mb && mb.gameObject.name.Contains("PlayBlade"))
                {
                    Log.Patch("PanelStatePatch", $"Detected PlayBlade BeginClose via GameObject name: {mb.gameObject.name}");
                    OnPanelStateChanged?.Invoke(__instance, false, "PlayBlade:Generic");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("PanelStatePatch", $"Error in BeginClosePostfix: {ex.Message}");
            }
        }

        public static void SettingsIsOpenPostfix(object __instance, bool value)
            => FirePanelStateChange(__instance, value, "SettingsMenu", "SettingsMenu IsOpen");

        // IsMainPanelActive toggles on submenu navigation within SettingsMenu — we route it under
        // a separate event name so consumers can distinguish a full open/close from a sub-panel swap.
        public static void SettingsMainPanelPostfix(object __instance, bool value)
            => FirePanelStateChange(__instance, value, "SettingsMenu:MainPanel", "SettingsMenu IsMainPanelActive");

        public static void DeckSelectIsShowingPostfix(object __instance, bool value)
            => FirePanelStateChange(__instance, value, "DeckSelectBlade", "DeckSelectBlade IsShowing");

        public static void PlayBladeVisualStatePostfix(object __instance, object value)
        {
            // value is PlayBladeVisualStates enum (Hidden=0, Events=1, DirectChallenge=2, FriendChallenge=3)
            int stateValue;
            try { stateValue = Convert.ToInt32(value); }
            catch (Exception ex) { Log.Warn("PanelStatePatch", $"PlayBladeVisualState convert failed: {ex.Message}"); return; }

            var stateName = value?.ToString() ?? "Unknown";
            FirePanelStateChange(__instance, stateValue != 0, $"PlayBlade:{stateName}",
                $"PlayBladeController.PlayBladeVisualState = {stateName}");
        }

        public static void IsEventBladeActivePostfix(object __instance, bool value)
            => FirePanelStateChange(__instance, value, "EventBlade", "HomePageContentController.IsEventBladeActive");

        public static void IsDirectChallengeBladeActivePostfix(object __instance, bool value)
            => FirePanelStateChange(__instance, value, "DirectChallengeBlade",
                "HomePageContentController.IsDirectChallengeBladeActive");

        public static void BladeContentViewShowPostfix(object __instance)
        {
            var typeName = __instance?.GetType().Name ?? "Unknown";
            FirePanelStateChange(__instance, true, $"Blade:{typeName}", $"BladeContentView.Show: {typeName}");
        }

        public static void BladeContentViewHidePostfix(object __instance)
        {
            var typeName = __instance?.GetType().Name ?? "Unknown";
            FirePanelStateChange(__instance, false, $"Blade:{typeName}", $"BladeContentView.Hide: {typeName}");
        }

        public static void EventBladeShowPostfix(object __instance)
            => FirePanelStateChange(__instance, true, "EventBladeContentView", "EventBladeContentView.Show");

        public static void EventBladeHidePostfix(object __instance)
            => FirePanelStateChange(__instance, false, "EventBladeContentView", "EventBladeContentView.Hide");

        /// <summary>
        /// Prefix for SocialUI.ShowSocialEntitiesList — blocks opening while Tab is held.
        /// Our mod uses Tab for navigation; we never want Tab to toggle the friends panel.
        /// </summary>
        public static bool SocialUIShowPrefix(object __instance)
            => !ShouldBlockSocialUITabToggle("SocialUI.ShowSocialEntitiesList");

        public static void SocialUIShowPostfix(object __instance)
        {
            if (ShouldBlockSocialUITabToggle("SocialUI.ShowSocialEntitiesList postfix", log: false)) return;
            FirePanelStateChange(__instance, true, "SocialUI", "SocialUI.ShowSocialEntitiesList");
        }

        public static void SocialUIHidePostfix(object __instance)
        {
            if (ShouldBlockSocialUITabToggle("SocialUI Hide postfix", log: false)) return;
            FirePanelStateChange(__instance, false, "SocialUI", "SocialUI Hide");
        }

        /// <summary>
        /// Prefix for SocialUI.CloseFriendsWidget / Minimize — blocks closing while Tab is held.
        /// Our mod uses Tab for navigation within the Friends panel, so Tab must not close it.
        /// </summary>
        public static bool SocialUIClosePrefix(object __instance)
            => !ShouldBlockSocialUITabToggle("SocialUI close");

        /// <summary>
        /// Prefix for SocialUI.SetVisible — blocks Show-while-Tab-held but always allows Hide.
        /// </summary>
        public static bool SocialUISetVisiblePrefix(object __instance, bool visible)
            => !(visible && ShouldBlockSocialUITabToggle("SocialUI.SetVisible(true)"));

        public static void SocialUISetVisiblePostfix(object __instance, bool visible)
        {
            if (visible && ShouldBlockSocialUITabToggle("SocialUI.SetVisible postfix", log: false)) return;
            FirePanelStateChange(__instance, visible, "SocialUI", $"SocialUI.SetVisible({visible})");
        }

        /// <summary>
        /// Prefix for SocialUI.HandleKeyDown — blocks Tab from toggling the social panel.
        /// Our mod uses Tab for navigation, so we don't want it to open/close the friends panel.
        /// </summary>
        public static bool SocialUIHandleKeyDownPrefix(object __instance, UnityEngine.KeyCode curr)
        {
            if (curr == UnityEngine.KeyCode.Tab)
            {
                Log.Patch("PanelStatePatch", $"Blocked Tab from SocialUI.HandleKeyDown");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Prefix for SocialUI.ShowChatWindow() — blocks chat from opening when Tab is held.
        /// Multiple code paths call ShowChatWindow (OnNext via action system, Show via focus gain,
        /// etc.). Patching the chokepoint catches all Tab-triggered chat opens.
        /// </summary>
        public static bool SocialUIShowChatWindowPrefix()
            => !ShouldBlockSocialUITabToggle("ShowChatWindow");
    }
}
