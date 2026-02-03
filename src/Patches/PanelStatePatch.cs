using HarmonyLib;
using MelonLoader;
using System;
using System.Linq;
using System.Reflection;
using AccessibleArena.Core.Services;

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

                // Try to patch BladeContentView base class (Events, FindMatch, LastPlayed blades)
                PatchBladeContentView(harmony);

                // Try to patch SocialUI (friends panel)
                PatchSocialUI(harmony);

                // Try to patch ContentControllerPlayerInbox (mailbox)
                PatchMailboxController(harmony);

                _patchApplied = true;
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Harmony patches applied successfully");

                // Discover other potential controller types for future patching
                DiscoverPanelTypes();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[PanelStatePatch] Initialization error: {ex}");
            }
        }

        private static void PatchNavContentController(HarmonyLib.Harmony harmony)
        {
            var controllerType = FindType("Wotc.Mtga.Wrapper.NavContentController");
            if (controllerType == null)
            {
                // Try alternative names
                controllerType = FindType("NavContentController");
            }

            if (controllerType == null)
            {
                MelonLogger.Warning("[PanelStatePatch] Could not find NavContentController type");
                return;
            }

            DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Found NavContentController: {controllerType.FullName}");

            // Log available methods/properties for debugging
            LogTypeMembers(controllerType);

            // NavContentController uses BeginOpen/FinishOpen/BeginClose/FinishClose lifecycle methods
            // FinishOpen/FinishClose are best - they fire after animations complete

            // Patch FinishOpen - called when panel finishes opening
            var finishOpenMethod = controllerType.GetMethod("FinishOpen",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (finishOpenMethod != null)
            {
                var postfix = typeof(PanelStatePatch).GetMethod(nameof(ShowPostfix),
                    BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(finishOpenMethod, postfix: new HarmonyMethod(postfix));
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched NavContentController.FinishOpen()");
            }
            else
            {
                MelonLogger.Warning("[PanelStatePatch] Could not find NavContentController.FinishOpen()");
            }

            // Patch FinishClose - called when panel finishes closing
            var finishCloseMethod = controllerType.GetMethod("FinishClose",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (finishCloseMethod != null)
            {
                var postfix = typeof(PanelStatePatch).GetMethod(nameof(HidePostfix),
                    BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(finishCloseMethod, postfix: new HarmonyMethod(postfix));
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched NavContentController.FinishClose()");
            }
            else
            {
                MelonLogger.Warning("[PanelStatePatch] Could not find NavContentController.FinishClose()");
            }

            // Also patch BeginOpen/BeginClose for earlier notification
            var beginOpenMethod = controllerType.GetMethod("BeginOpen",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (beginOpenMethod != null)
            {
                var postfix = typeof(PanelStatePatch).GetMethod(nameof(BeginOpenPostfix),
                    BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(beginOpenMethod, postfix: new HarmonyMethod(postfix));
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched NavContentController.BeginOpen()");
            }

            var beginCloseMethod = controllerType.GetMethod("BeginClose",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (beginCloseMethod != null)
            {
                var postfix = typeof(PanelStatePatch).GetMethod(nameof(BeginClosePostfix),
                    BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(beginCloseMethod, postfix: new HarmonyMethod(postfix));
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched NavContentController.BeginClose()");
            }

            // Keep IsOpen setter patch as backup
            var isOpenSetter = controllerType.GetProperty("IsOpen",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetSetMethod(true);

            if (isOpenSetter != null)
            {
                var postfix = typeof(PanelStatePatch).GetMethod(nameof(IsOpenSetterPostfix),
                    BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(isOpenSetter, postfix: new HarmonyMethod(postfix));
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched NavContentController.IsOpen setter");
            }
        }

        /// <summary>
        /// Discovers and logs all types that might be panel controllers.
        /// Called once after patches are applied to find what else might need patching.
        /// </summary>
        public static void DiscoverPanelTypes()
        {
            DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"=== Discovering potential panel controller types ===");

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
                        var hasIsOpen = type.GetProperty("IsOpen", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) != null;
                        var hasShow = type.GetMethod("Show", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) != null;
                        var hasHide = type.GetMethod("Hide", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) != null;

                        if (hasIsOpen || hasShow || hasHide)
                        {
                            DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Found: {type.FullName} - IsOpen:{hasIsOpen} Show:{hasShow} Hide:{hasHide}");
                        }
                    }
                }
                catch
                {
                    // Ignore assembly load errors
                }
            }

            DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"=== Discovery complete ===");
        }

        private static void PatchSettingsMenu(HarmonyLib.Harmony harmony)
        {
            var settingsType = FindType("Wotc.Mtga.Wrapper.SettingsMenu");
            if (settingsType == null)
            {
                settingsType = FindType("SettingsMenu");
            }

            if (settingsType == null)
            {
                MelonLogger.Warning("[PanelStatePatch] Could not find SettingsMenu type");
                return;
            }

            DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Found SettingsMenu: {settingsType.FullName}");

            // Log all methods to discover correct signatures
            LogTypeMembers(settingsType);

            // Try various Show/Open methods - search without parameter constraint first
            var showMethods = settingsType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name == "Show" || m.Name == "Open" || m.Name == "FinishOpen" || m.Name == "BeginOpen")
                .ToArray();

            foreach (var method in showMethods)
            {
                try
                {
                    var postfix = typeof(PanelStatePatch).GetMethod(nameof(SettingsShowPostfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched SettingsMenu.{method.Name}()");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PanelStatePatch] Failed to patch SettingsMenu.{method.Name}: {ex.Message}");
                }
            }

            // Try various Hide/Close methods
            var hideMethods = settingsType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name == "Hide" || m.Name == "Close" || m.Name == "FinishClose" || m.Name == "BeginClose")
                .ToArray();

            foreach (var method in hideMethods)
            {
                try
                {
                    var postfix = typeof(PanelStatePatch).GetMethod(nameof(SettingsHidePostfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched SettingsMenu.{method.Name}()");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PanelStatePatch] Failed to patch SettingsMenu.{method.Name}: {ex.Message}");
                }
            }

            // Also try IsOpen/IsMainPanelActive setters
            var isOpenSetter = settingsType.GetProperty("IsOpen",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetSetMethod(true);
            if (isOpenSetter != null)
            {
                try
                {
                    var postfix = typeof(PanelStatePatch).GetMethod(nameof(SettingsIsOpenPostfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(isOpenSetter, postfix: new HarmonyMethod(postfix));
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched SettingsMenu.IsOpen setter");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PanelStatePatch] Failed to patch SettingsMenu.IsOpen setter: {ex.Message}");
                }
            }

            var isMainPanelActiveSetter = settingsType.GetProperty("IsMainPanelActive",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetSetMethod(true);
            if (isMainPanelActiveSetter != null)
            {
                try
                {
                    var postfix = typeof(PanelStatePatch).GetMethod(nameof(SettingsMainPanelPostfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(isMainPanelActiveSetter, postfix: new HarmonyMethod(postfix));
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched SettingsMenu.IsMainPanelActive setter");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PanelStatePatch] Failed to patch SettingsMenu.IsMainPanelActive setter: {ex.Message}");
                }
            }
        }

        private static void PatchDeckSelectController(HarmonyLib.Harmony harmony)
        {
            // DeckSelectBlade has Show/Hide methods which are called when deck selection opens/closes
            var deckBladeType = FindType("DeckSelectBlade");
            if (deckBladeType == null)
            {
                MelonLogger.Warning("[PanelStatePatch] Could not find DeckSelectBlade type");
                return;
            }

            DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Found DeckSelectBlade: {deckBladeType.FullName}");

            // Log available methods/properties for debugging
            LogTypeMembers(deckBladeType);

            // Find all Show methods (may have parameters like Show(EventContext, DeckFormat, Action))
            var showMethods = deckBladeType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name == "Show")
                .ToArray();

            foreach (var method in showMethods)
            {
                try
                {
                    var postfix = typeof(PanelStatePatch).GetMethod(nameof(DeckSelectShowPostfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                    var paramStr = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched DeckSelectBlade.Show({paramStr})");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PanelStatePatch] Failed to patch DeckSelectBlade.Show: {ex.Message}");
                }
            }

            // Patch the Hide method
            var hideMethod = deckBladeType.GetMethod("Hide",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (hideMethod != null)
            {
                try
                {
                    var postfix = typeof(PanelStatePatch).GetMethod(nameof(DeckSelectHidePostfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(hideMethod, postfix: new HarmonyMethod(postfix));
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched DeckSelectBlade.Hide()");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PanelStatePatch] Failed to patch DeckSelectBlade.Hide: {ex.Message}");
                }
            }
            else
            {
                MelonLogger.Warning("[PanelStatePatch] Could not find Hide method on DeckSelectBlade");
            }

            // Also patch IsShowing setter if available
            var isShowingSetter = deckBladeType.GetProperty("IsShowing",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetSetMethod(true);
            if (isShowingSetter != null)
            {
                try
                {
                    var postfix = typeof(PanelStatePatch).GetMethod(nameof(DeckSelectIsShowingPostfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(isShowingSetter, postfix: new HarmonyMethod(postfix));
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched DeckSelectBlade.IsShowing setter");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PanelStatePatch] Failed to patch DeckSelectBlade.IsShowing setter: {ex.Message}");
                }
            }
        }

        private static void PatchPlayBladeController(HarmonyLib.Harmony harmony)
        {
            var playBladeType = FindType("PlayBladeController");
            if (playBladeType == null)
            {
                MelonLogger.Warning("[PanelStatePatch] Could not find PlayBladeController type");
                return;
            }

            DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Found PlayBladeController: {playBladeType.FullName}");

            // Patch PlayBladeVisualState setter - this changes when play blade opens/closes
            var visualStateSetter = playBladeType.GetProperty("PlayBladeVisualState",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetSetMethod(true);

            if (visualStateSetter != null)
            {
                try
                {
                    var postfix = typeof(PanelStatePatch).GetMethod(nameof(PlayBladeVisualStatePostfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(visualStateSetter, postfix: new HarmonyMethod(postfix));
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched PlayBladeController.PlayBladeVisualState setter");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PanelStatePatch] Failed to patch PlayBladeController.PlayBladeVisualState setter: {ex.Message}");
                }
            }
            else
            {
                MelonLogger.Warning("[PanelStatePatch] Could not find PlayBladeController.PlayBladeVisualState setter");
            }

            // Also patch IsDeckSelected setter
            var isDeckSelectedSetter = playBladeType.GetProperty("IsDeckSelected",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetSetMethod(true);

            if (isDeckSelectedSetter != null)
            {
                try
                {
                    var postfix = typeof(PanelStatePatch).GetMethod(nameof(PlayBladeIsDeckSelectedPostfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(isDeckSelectedSetter, postfix: new HarmonyMethod(postfix));
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched PlayBladeController.IsDeckSelected setter");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PanelStatePatch] Failed to patch PlayBladeController.IsDeckSelected setter: {ex.Message}");
                }
            }
        }

        private static void PatchHomePageBladeStates(HarmonyLib.Harmony harmony)
        {
            var homePageType = FindType("HomePageContentController");
            if (homePageType == null)
            {
                MelonLogger.Warning("[PanelStatePatch] Could not find HomePageContentController type");
                return;
            }

            DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Found HomePageContentController: {homePageType.FullName}");

            // Patch IsEventBladeActive setter
            var isEventBladeActiveSetter = homePageType.GetProperty("IsEventBladeActive",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetSetMethod(true);

            if (isEventBladeActiveSetter != null)
            {
                try
                {
                    var postfix = typeof(PanelStatePatch).GetMethod(nameof(IsEventBladeActivePostfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(isEventBladeActiveSetter, postfix: new HarmonyMethod(postfix));
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched HomePageContentController.IsEventBladeActive setter");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PanelStatePatch] Failed to patch HomePageContentController.IsEventBladeActive setter: {ex.Message}");
                }
            }
            else
            {
                MelonLogger.Warning("[PanelStatePatch] Could not find HomePageContentController.IsEventBladeActive setter");
            }

            // Patch IsDirectChallengeBladeActive setter
            var isDirectChallengeBladeActiveSetter = homePageType.GetProperty("IsDirectChallengeBladeActive",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetSetMethod(true);

            if (isDirectChallengeBladeActiveSetter != null)
            {
                try
                {
                    var postfix = typeof(PanelStatePatch).GetMethod(nameof(IsDirectChallengeBladeActivePostfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(isDirectChallengeBladeActiveSetter, postfix: new HarmonyMethod(postfix));
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched HomePageContentController.IsDirectChallengeBladeActive setter");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PanelStatePatch] Failed to patch HomePageContentController.IsDirectChallengeBladeActive setter: {ex.Message}");
                }
            }
            else
            {
                MelonLogger.Warning("[PanelStatePatch] Could not find HomePageContentController.IsDirectChallengeBladeActive setter");
            }
        }

        private static void PatchBladeContentView(HarmonyLib.Harmony harmony)
        {
            // Try to patch the base BladeContentView class for Show/Hide
            var bladeContentViewType = FindType("Wizards.Mtga.PlayBlade.BladeContentView");
            if (bladeContentViewType == null)
            {
                bladeContentViewType = FindType("BladeContentView");
            }

            if (bladeContentViewType != null)
            {
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Found BladeContentView: {bladeContentViewType.FullName}");

                var showMethod = bladeContentViewType.GetMethod("Show",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (showMethod != null)
                {
                    try
                    {
                        var postfix = typeof(PanelStatePatch).GetMethod(nameof(BladeContentViewShowPostfix),
                            BindingFlags.Static | BindingFlags.Public);
                        harmony.Patch(showMethod, postfix: new HarmonyMethod(postfix));
                        DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched BladeContentView.Show()");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[PanelStatePatch] Failed to patch BladeContentView.Show: {ex.Message}");
                    }
                }

                var hideMethod = bladeContentViewType.GetMethod("Hide",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (hideMethod != null)
                {
                    try
                    {
                        var postfix = typeof(PanelStatePatch).GetMethod(nameof(BladeContentViewHidePostfix),
                            BindingFlags.Static | BindingFlags.Public);
                        harmony.Patch(hideMethod, postfix: new HarmonyMethod(postfix));
                        DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched BladeContentView.Hide()");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[PanelStatePatch] Failed to patch BladeContentView.Hide: {ex.Message}");
                    }
                }
            }
            else
            {
                MelonLogger.Warning("[PanelStatePatch] Could not find BladeContentView type");
            }

            // Also try to patch EventBladeContentView directly (has Show/Hide)
            var eventBladeType = FindType("Wizards.Mtga.PlayBlade.EventBladeContentView");
            if (eventBladeType == null)
            {
                eventBladeType = FindType("EventBladeContentView");
            }

            if (eventBladeType != null)
            {
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Found EventBladeContentView: {eventBladeType.FullName}");

                var showMethod = eventBladeType.GetMethod("Show",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (showMethod != null)
                {
                    try
                    {
                        var postfix = typeof(PanelStatePatch).GetMethod(nameof(EventBladeShowPostfix),
                            BindingFlags.Static | BindingFlags.Public);
                        harmony.Patch(showMethod, postfix: new HarmonyMethod(postfix));
                        DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched EventBladeContentView.Show()");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[PanelStatePatch] Failed to patch EventBladeContentView.Show: {ex.Message}");
                    }
                }

                var hideMethod = eventBladeType.GetMethod("Hide",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (hideMethod != null)
                {
                    try
                    {
                        var postfix = typeof(PanelStatePatch).GetMethod(nameof(EventBladeHidePostfix),
                            BindingFlags.Static | BindingFlags.Public);
                        harmony.Patch(hideMethod, postfix: new HarmonyMethod(postfix));
                        DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched EventBladeContentView.Hide()");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[PanelStatePatch] Failed to patch EventBladeContentView.Hide: {ex.Message}");
                    }
                }
            }
            else
            {
                MelonLogger.Warning("[PanelStatePatch] Could not find EventBladeContentView type");
            }
        }

        private static void PatchSocialUI(HarmonyLib.Harmony harmony)
        {
            var socialUIType = FindType("SocialUI");
            if (socialUIType == null)
            {
                MelonLogger.Warning("[PanelStatePatch] Could not find SocialUI type");
                return;
            }

            DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Found SocialUI: {socialUIType.FullName}");

            // Patch ShowSocialEntitiesList - called when friends list opens
            // Use both prefix (to block Tab-triggered opens) and postfix (for notifications)
            var showMethod = socialUIType.GetMethod("ShowSocialEntitiesList",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (showMethod != null)
            {
                try
                {
                    var prefix = typeof(PanelStatePatch).GetMethod(nameof(SocialUIShowPrefix),
                        BindingFlags.Static | BindingFlags.Public);
                    var postfix = typeof(PanelStatePatch).GetMethod(nameof(SocialUIShowPostfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(showMethod, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched SocialUI.ShowSocialEntitiesList()");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PanelStatePatch] Failed to patch SocialUI.ShowSocialEntitiesList: {ex.Message}");
                }
            }

            // Patch CloseFriendsWidget - called when friends list closes
            // Add prefix to block closing when Tab is pressed (our mod uses Tab for navigation)
            var closeMethod = socialUIType.GetMethod("CloseFriendsWidget",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (closeMethod != null)
            {
                try
                {
                    var prefix = typeof(PanelStatePatch).GetMethod(nameof(SocialUIClosePrefix),
                        BindingFlags.Static | BindingFlags.Public);
                    var postfix = typeof(PanelStatePatch).GetMethod(nameof(SocialUIHidePostfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(closeMethod, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched SocialUI.CloseFriendsWidget()");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PanelStatePatch] Failed to patch SocialUI.CloseFriendsWidget: {ex.Message}");
                }
            }

            // Also patch Minimize - another way to close
            // Add prefix to block minimizing when Tab is pressed
            var minimizeMethod = socialUIType.GetMethod("Minimize",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (minimizeMethod != null)
            {
                try
                {
                    var prefix = typeof(PanelStatePatch).GetMethod(nameof(SocialUIClosePrefix),
                        BindingFlags.Static | BindingFlags.Public);
                    var postfix = typeof(PanelStatePatch).GetMethod(nameof(SocialUIHidePostfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(minimizeMethod, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched SocialUI.Minimize()");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PanelStatePatch] Failed to patch SocialUI.Minimize: {ex.Message}");
                }
            }

            // Patch SetVisible - general visibility control (with Tab blocking)
            var setVisibleMethod = socialUIType.GetMethod("SetVisible",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (setVisibleMethod != null)
            {
                try
                {
                    var prefix = typeof(PanelStatePatch).GetMethod(nameof(SocialUISetVisiblePrefix),
                        BindingFlags.Static | BindingFlags.Public);
                    var postfix = typeof(PanelStatePatch).GetMethod(nameof(SocialUISetVisiblePostfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(setVisibleMethod, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched SocialUI.SetVisible()");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PanelStatePatch] Failed to patch SocialUI.SetVisible: {ex.Message}");
                }
            }

            // Patch HandleKeyDown - block Tab from toggling social panel
            var handleKeyDownMethod = socialUIType.GetMethod("HandleKeyDown",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (handleKeyDownMethod != null)
            {
                try
                {
                    var prefix = typeof(PanelStatePatch).GetMethod(nameof(SocialUIHandleKeyDownPrefix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(handleKeyDownMethod, prefix: new HarmonyMethod(prefix));
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched SocialUI.HandleKeyDown()");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PanelStatePatch] Failed to patch SocialUI.HandleKeyDown: {ex.Message}");
                }
            }
        }

        private static void PatchMailboxController(HarmonyLib.Harmony harmony)
        {
            // Mailbox is controlled by NavBarController, not a dedicated content controller
            // NavBarController has MailboxButton_OnClick() to open and HideInboxIfActive() to close
            var navBarType = FindType("NavBarController");
            if (navBarType == null)
            {
                MelonLogger.Warning("[PanelStatePatch] Could not find NavBarController type for mailbox patching");
                return;
            }

            DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Found NavBarController for mailbox: {navBarType.FullName}");

            // Patch MailboxButton_OnClick - called when mailbox opens
            var openMethod = navBarType.GetMethod("MailboxButton_OnClick",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (openMethod != null)
            {
                try
                {
                    var postfix = typeof(PanelStatePatch).GetMethod(nameof(MailboxOpenPostfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(openMethod, postfix: new HarmonyMethod(postfix));
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched NavBarController.MailboxButton_OnClick()");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PanelStatePatch] Failed to patch NavBarController.MailboxButton_OnClick: {ex.Message}");
                }
            }
            else
            {
                MelonLogger.Warning("[PanelStatePatch] NavBarController.MailboxButton_OnClick not found");
            }

            // Patch HideInboxIfActive - called when mailbox closes
            var closeMethod = navBarType.GetMethod("HideInboxIfActive",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (closeMethod != null)
            {
                try
                {
                    var postfix = typeof(PanelStatePatch).GetMethod(nameof(MailboxClosePostfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(closeMethod, postfix: new HarmonyMethod(postfix));
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched NavBarController.HideInboxIfActive()");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PanelStatePatch] Failed to patch NavBarController.HideInboxIfActive: {ex.Message}");
                }
            }
            else
            {
                MelonLogger.Warning("[PanelStatePatch] NavBarController.HideInboxIfActive not found");
            }

            // Patch ContentControllerPlayerInbox.OnLetterSelected - called when a mail is opened
            PatchMailLetterSelected(harmony);
        }

        private static void PatchMailLetterSelected(HarmonyLib.Harmony harmony)
        {
            var inboxType = FindType("Wotc.Mtga.Wrapper.Mailbox.ContentControllerPlayerInbox");
            if (inboxType == null)
            {
                MelonLogger.Warning("[PanelStatePatch] Could not find ContentControllerPlayerInbox type");
                return;
            }

            DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Found ContentControllerPlayerInbox: {inboxType.FullName}");

            // OnLetterSelected(PlayerInboxBladeItemDisplay selectedLetter, Boolean isRead, Guid selectedLetterId)
            var onLetterSelectedMethod = inboxType.GetMethod("OnLetterSelected",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (onLetterSelectedMethod != null)
            {
                try
                {
                    var postfix = typeof(PanelStatePatch).GetMethod(nameof(MailLetterSelectedPostfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(onLetterSelectedMethod, postfix: new HarmonyMethod(postfix));
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Patched ContentControllerPlayerInbox.OnLetterSelected()");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PanelStatePatch] Failed to patch OnLetterSelected: {ex.Message}");
                }
            }
            else
            {
                MelonLogger.Warning("[PanelStatePatch] ContentControllerPlayerInbox.OnLetterSelected not found");
            }
        }

        public static void MailboxOpenPostfix(object __instance)
        {
            try
            {
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Mailbox opened via NavBarController.MailboxButton_OnClick");
                OnPanelStateChanged?.Invoke(__instance, true, "Mailbox");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in MailboxOpenPostfix: {ex.Message}");
            }
        }

        public static void MailboxClosePostfix(object __instance)
        {
            try
            {
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Mailbox closed via NavBarController.HideInboxIfActive");
                OnPanelStateChanged?.Invoke(__instance, false, "Mailbox");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in MailboxClosePostfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for ContentControllerPlayerInbox.OnLetterSelected
        /// Parameters: selectedLetter (PlayerInboxBladeItemDisplay), isRead (bool), selectedLetterId (Guid)
        /// </summary>
        public static void MailLetterSelectedPostfix(object __instance, object selectedLetter, bool isRead, Guid selectedLetterId)
        {
            try
            {
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Mail letter selected: {selectedLetterId}, isRead: {isRead}");

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
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (viewModelField != null)
                    {
                        var viewModel = viewModelField.GetValue(selectedLetter);
                        if (viewModel != null)
                        {
                            var vmType = viewModel.GetType();

                            // Get Title field
                            var titleField = vmType.GetField("Title", BindingFlags.Public | BindingFlags.Instance);
                            if (titleField != null)
                                title = titleField.GetValue(viewModel) as string ?? "";

                            // Get Body field
                            var bodyField = vmType.GetField("Body", BindingFlags.Public | BindingFlags.Instance);
                            if (bodyField != null)
                                body = bodyField.GetValue(viewModel) as string ?? "";

                            // Get Attachments field (List)
                            var attachmentsField = vmType.GetField("Attachments", BindingFlags.Public | BindingFlags.Instance);
                            if (attachmentsField != null)
                            {
                                var attachments = attachmentsField.GetValue(viewModel) as System.Collections.IList;
                                hasAttachments = attachments != null && attachments.Count > 0;
                            }

                            // Get IsClaimed field
                            var isClaimedField = vmType.GetField("IsClaimed", BindingFlags.Public | BindingFlags.Instance);
                            if (isClaimedField != null)
                                isClaimed = (bool)isClaimedField.GetValue(viewModel);

                            DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Letter data - Title: {title}, Body length: {body?.Length ?? 0}, HasAttachments: {hasAttachments}, IsClaimed: {isClaimed}");
                        }
                        else
                        {
                            MelonLogger.Warning("[PanelStatePatch] viewModel is null");
                        }
                    }
                    else
                    {
                        MelonLogger.Warning($"[PanelStatePatch] _clientBladeItemViewModel field not found on {selectedLetterType.Name}");
                    }
                }

                OnMailLetterSelected?.Invoke(selectedLetterId, title, body, hasAttachments, isClaimed);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in MailLetterSelectedPostfix: {ex.Message}");
            }
        }

        private static void LogTypeMembers(Type type)
        {
            DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Methods on {type.Name}:");
            foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (m.Name.Contains("Show") || m.Name.Contains("Hide") || m.Name.Contains("Open") || m.Name.Contains("Close"))
                {
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"  - {m.Name}({string.Join(", ", Array.ConvertAll(m.GetParameters(), p => p.ParameterType.Name))})");
                }
            }

            DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Properties on {type.Name}:");
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (p.Name.Contains("Open") || p.Name.Contains("Ready") || p.Name.Contains("Visible"))
                {
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"  - {p.Name} ({p.PropertyType.Name})");
                }
            }
        }

        /// <summary>
        /// Finds a type by full name across all loaded assemblies.
        /// </summary>
        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName);
                    if (type != null)
                        return type;

                    // Also try to find by name only (without namespace)
                    foreach (var t in assembly.GetTypes())
                    {
                        if (t.Name == fullName || t.FullName == fullName)
                            return t;
                    }
                }
                catch
                {
                    // Ignore assembly load errors
                }
            }
            return null;
        }

        // === Postfix Methods ===

        public static void ShowPostfix(object __instance)
        {
            try
            {
                var typeName = __instance?.GetType().Name ?? "Unknown";
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Panel Show: {typeName}");
                OnPanelStateChanged?.Invoke(__instance, true, typeName);

                // Check if this is a PlayBlade controller by GameObject name
                if (__instance is UnityEngine.MonoBehaviour mb && mb.gameObject.name.Contains("PlayBlade"))
                {
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Detected PlayBlade Show via GameObject name: {mb.gameObject.name}");
                    OnPanelStateChanged?.Invoke(__instance, true, "PlayBlade:Generic");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in ShowPostfix: {ex.Message}");
            }
        }

        public static void HidePostfix(object __instance)
        {
            try
            {
                var typeName = __instance?.GetType().Name ?? "Unknown";
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Panel Hide: {typeName}");
                OnPanelStateChanged?.Invoke(__instance, false, typeName);

                // Check if this is a PlayBlade controller by GameObject name
                if (__instance is UnityEngine.MonoBehaviour mb && mb.gameObject.name.Contains("PlayBlade"))
                {
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Detected PlayBlade Hide via GameObject name: {mb.gameObject.name}");
                    OnPanelStateChanged?.Invoke(__instance, false, "PlayBlade:Generic");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in HidePostfix: {ex.Message}");
            }
        }

        public static void IsOpenSetterPostfix(object __instance, bool value)
        {
            try
            {
                var typeName = __instance?.GetType().Name ?? "Unknown";
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Panel IsOpen = {value}: {typeName}");
                OnPanelStateChanged?.Invoke(__instance, value, typeName);

                // Check if this is a PlayBlade controller by GameObject name
                if (__instance is UnityEngine.MonoBehaviour mb && mb.gameObject.name.Contains("PlayBlade"))
                {
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Detected PlayBlade IsOpen={value} via GameObject name: {mb.gameObject.name}");
                    OnPanelStateChanged?.Invoke(__instance, value, "PlayBlade:Generic");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in IsOpenSetterPostfix: {ex.Message}");
            }
        }

        public static void SettingsShowPostfix(object __instance)
        {
            try
            {
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"SettingsMenu Show");
                OnPanelStateChanged?.Invoke(__instance, true, "SettingsMenu");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in SettingsShowPostfix: {ex.Message}");
            }
        }

        public static void SettingsHidePostfix(object __instance)
        {
            try
            {
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"SettingsMenu Hide");
                OnPanelStateChanged?.Invoke(__instance, false, "SettingsMenu");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in SettingsHidePostfix: {ex.Message}");
            }
        }

        public static void DeckSelectShowPostfix(object __instance)
        {
            try
            {
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"DeckSelectBlade Show");
                OnPanelStateChanged?.Invoke(__instance, true, "DeckSelectBlade");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in DeckSelectShowPostfix: {ex.Message}");
            }
        }

        public static void DeckSelectHidePostfix(object __instance)
        {
            try
            {
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"DeckSelectBlade Hide");
                OnPanelStateChanged?.Invoke(__instance, false, "DeckSelectBlade");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in DeckSelectHidePostfix: {ex.Message}");
            }
        }

        public static void BeginOpenPostfix(object __instance)
        {
            try
            {
                var typeName = __instance?.GetType().Name ?? "Unknown";
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Panel BeginOpen: {typeName}");
                // Don't fire event yet - wait for FinishOpen when UI is ready

                // But for PlayBlade, fire early so we track blade state
                if (__instance is UnityEngine.MonoBehaviour mb && mb.gameObject.name.Contains("PlayBlade"))
                {
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Detected PlayBlade BeginOpen via GameObject name: {mb.gameObject.name}");
                    OnPanelStateChanged?.Invoke(__instance, true, "PlayBlade:Generic");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in BeginOpenPostfix: {ex.Message}");
            }
        }

        public static void BeginClosePostfix(object __instance)
        {
            try
            {
                var typeName = __instance?.GetType().Name ?? "Unknown";
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Panel BeginClose: {typeName}");
                // Fire event early so navigator knows panel is closing
                OnPanelStateChanged?.Invoke(__instance, false, typeName);

                // Check if this is a PlayBlade controller by GameObject name
                if (__instance is UnityEngine.MonoBehaviour mb && mb.gameObject.name.Contains("PlayBlade"))
                {
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Detected PlayBlade BeginClose via GameObject name: {mb.gameObject.name}");
                    OnPanelStateChanged?.Invoke(__instance, false, "PlayBlade:Generic");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in BeginClosePostfix: {ex.Message}");
            }
        }

        public static void SettingsIsOpenPostfix(object __instance, bool value)
        {
            try
            {
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"SettingsMenu IsOpen = {value}");
                OnPanelStateChanged?.Invoke(__instance, value, "SettingsMenu");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in SettingsIsOpenPostfix: {ex.Message}");
            }
        }

        public static void SettingsMainPanelPostfix(object __instance, bool value)
        {
            try
            {
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"SettingsMenu IsMainPanelActive = {value}");
                // IsMainPanelActive changing means submenu navigation
                OnPanelStateChanged?.Invoke(__instance, value, "SettingsMenu:MainPanel");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in SettingsMainPanelPostfix: {ex.Message}");
            }
        }

        public static void DeckSelectIsShowingPostfix(object __instance, bool value)
        {
            try
            {
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"DeckSelectBlade IsShowing = {value}");
                OnPanelStateChanged?.Invoke(__instance, value, "DeckSelectBlade");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in DeckSelectIsShowingPostfix: {ex.Message}");
            }
        }

        public static void PlayBladeVisualStatePostfix(object __instance, object value)
        {
            try
            {
                // value is PlayBladeVisualStates enum (Hidden=0, Events=1, DirectChallenge=2, FriendChallenge=3)
                var stateValue = Convert.ToInt32(value);
                var stateName = value?.ToString() ?? "Unknown";
                bool isOpen = stateValue != 0; // 0 = Hidden

                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"PlayBladeController.PlayBladeVisualState = {stateName} (isOpen: {isOpen})");
                OnPanelStateChanged?.Invoke(__instance, isOpen, $"PlayBlade:{stateName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in PlayBladeVisualStatePostfix: {ex.Message}");
            }
        }

        public static void PlayBladeIsDeckSelectedPostfix(object __instance, bool value)
        {
            try
            {
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"PlayBladeController.IsDeckSelected = {value}");
                OnPanelStateChanged?.Invoke(__instance, value, "PlayBlade:DeckSelected");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in PlayBladeIsDeckSelectedPostfix: {ex.Message}");
            }
        }

        public static void IsEventBladeActivePostfix(object __instance, bool value)
        {
            try
            {
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"HomePageContentController.IsEventBladeActive = {value}");
                OnPanelStateChanged?.Invoke(__instance, value, "EventBlade");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in IsEventBladeActivePostfix: {ex.Message}");
            }
        }

        public static void IsDirectChallengeBladeActivePostfix(object __instance, bool value)
        {
            try
            {
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"HomePageContentController.IsDirectChallengeBladeActive = {value}");
                OnPanelStateChanged?.Invoke(__instance, value, "DirectChallengeBlade");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in IsDirectChallengeBladeActivePostfix: {ex.Message}");
            }
        }

        public static void BladeContentViewShowPostfix(object __instance)
        {
            try
            {
                var typeName = __instance?.GetType().Name ?? "Unknown";
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"BladeContentView.Show: {typeName}");
                OnPanelStateChanged?.Invoke(__instance, true, $"Blade:{typeName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in BladeContentViewShowPostfix: {ex.Message}");
            }
        }

        public static void BladeContentViewHidePostfix(object __instance)
        {
            try
            {
                var typeName = __instance?.GetType().Name ?? "Unknown";
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"BladeContentView.Hide: {typeName}");
                OnPanelStateChanged?.Invoke(__instance, false, $"Blade:{typeName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in BladeContentViewHidePostfix: {ex.Message}");
            }
        }

        public static void EventBladeShowPostfix(object __instance)
        {
            try
            {
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"EventBladeContentView.Show");
                OnPanelStateChanged?.Invoke(__instance, true, "EventBladeContentView");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in EventBladeShowPostfix: {ex.Message}");
            }
        }

        public static void EventBladeHidePostfix(object __instance)
        {
            try
            {
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"EventBladeContentView.Hide");
                OnPanelStateChanged?.Invoke(__instance, false, "EventBladeContentView");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in EventBladeHidePostfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Prefix for SocialUI.ShowSocialEntitiesList - blocks opening if Tab key is pressed.
        /// This prevents Tab from toggling the friends panel (our mod uses Tab for navigation).
        /// </summary>
        public static bool SocialUIShowPrefix(object __instance)
        {
            // Block if Tab is currently pressed - this means the game is trying to open
            // the social panel via Tab, which we want to prevent
            if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.Tab))
            {
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Blocked SocialUI.ShowSocialEntitiesList (Tab pressed)");
                return false; // Skip the original method
            }
            return true; // Allow the method to run
        }

        public static void SocialUIShowPostfix(object __instance)
        {
            try
            {
                // Skip if Tab is pressed - means prefix blocked the call but Harmony still runs postfix
                if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.Tab))
                {
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Skipping SocialUI.ShowSocialEntitiesList postfix (Tab pressed)");
                    return;
                }

                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"SocialUI.ShowSocialEntitiesList");
                OnPanelStateChanged?.Invoke(__instance, true, "SocialUI");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in SocialUIShowPostfix: {ex.Message}");
            }
        }

        public static void SocialUIHidePostfix(object __instance)
        {
            try
            {
                // Skip notification if Tab is pressed - prefix should have blocked the call
                if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.Tab))
                {
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Skipping SocialUI Hide postfix (Tab pressed)");
                    return;
                }

                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"SocialUI Hide");
                OnPanelStateChanged?.Invoke(__instance, false, "SocialUI");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in SocialUIHidePostfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Prefix for SocialUI.CloseFriendsWidget and Minimize - blocks closing if Tab key is pressed.
        /// Our mod uses Tab for navigation within the Friends panel, so we don't want Tab to close it.
        /// </summary>
        public static bool SocialUIClosePrefix(object __instance)
        {
            // Block if Tab is pressed - our mod uses Tab for navigation, not closing
            if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.Tab))
            {
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Blocked SocialUI close (Tab pressed)");
                return false; // Skip the original method
            }
            return true; // Allow the method to run
        }

        /// <summary>
        /// Prefix for SocialUI.SetVisible - blocks showing if Tab key is pressed.
        /// </summary>
        public static bool SocialUISetVisiblePrefix(object __instance, bool visible)
        {
            // Block if Tab is pressed and trying to show the panel
            if (visible && UnityEngine.Input.GetKey(UnityEngine.KeyCode.Tab))
            {
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Blocked SocialUI.SetVisible(true) (Tab pressed)");
                return false; // Skip the original method
            }
            return true;
        }

        public static void SocialUISetVisiblePostfix(object __instance, bool visible)
        {
            try
            {
                // Skip if Tab is pressed and trying to show - means prefix blocked the call
                if (visible && UnityEngine.Input.GetKey(UnityEngine.KeyCode.Tab))
                {
                    DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Skipping SocialUI.SetVisible postfix (Tab pressed)");
                    return;
                }

                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"SocialUI.SetVisible({visible})");
                OnPanelStateChanged?.Invoke(__instance, visible, "SocialUI");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in SocialUISetVisiblePostfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Prefix for SocialUI.HandleKeyDown - blocks Tab from toggling the social panel.
        /// Our mod uses Tab for navigation, so we don't want it to open/close the friends panel.
        /// </summary>
        public static bool SocialUIHandleKeyDownPrefix(object __instance, UnityEngine.KeyCode curr)
        {
            // Block Tab key - our mod handles Tab for navigation
            if (curr == UnityEngine.KeyCode.Tab)
            {
                DebugConfig.LogIf(DebugConfig.LogPatches, "PanelStatePatch", $"Blocked Tab from SocialUI.HandleKeyDown");
                return false; // Skip the original method
            }
            return true; // Let other keys through
        }
    }
}
