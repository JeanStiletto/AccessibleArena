using HarmonyLib;
using MelonLoader;
using System;
using System.Linq;
using System.Reflection;

namespace MTGAAccessibility.Patches
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

                _patchApplied = true;
                MelonLogger.Msg("[PanelStatePatch] Harmony patches applied successfully");

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

            MelonLogger.Msg($"[PanelStatePatch] Found NavContentController: {controllerType.FullName}");

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
                MelonLogger.Msg("[PanelStatePatch] Patched NavContentController.FinishOpen()");
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
                MelonLogger.Msg("[PanelStatePatch] Patched NavContentController.FinishClose()");
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
                MelonLogger.Msg("[PanelStatePatch] Patched NavContentController.BeginOpen()");
            }

            var beginCloseMethod = controllerType.GetMethod("BeginClose",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (beginCloseMethod != null)
            {
                var postfix = typeof(PanelStatePatch).GetMethod(nameof(BeginClosePostfix),
                    BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(beginCloseMethod, postfix: new HarmonyMethod(postfix));
                MelonLogger.Msg("[PanelStatePatch] Patched NavContentController.BeginClose()");
            }

            // Keep IsOpen setter patch as backup
            var isOpenSetter = controllerType.GetProperty("IsOpen",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetSetMethod(true);

            if (isOpenSetter != null)
            {
                var postfix = typeof(PanelStatePatch).GetMethod(nameof(IsOpenSetterPostfix),
                    BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(isOpenSetter, postfix: new HarmonyMethod(postfix));
                MelonLogger.Msg("[PanelStatePatch] Patched NavContentController.IsOpen setter");
            }
        }

        /// <summary>
        /// Discovers and logs all types that might be panel controllers.
        /// Called once after patches are applied to find what else might need patching.
        /// </summary>
        public static void DiscoverPanelTypes()
        {
            MelonLogger.Msg("[PanelStatePatch] === Discovering potential panel controller types ===");

            var keywords = new[] { "Deck", "Selection", "Picker", "Panel", "Controller", "Modal", "Dialog", "Overlay", "Screen" };

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
                            MelonLogger.Msg($"[PanelStatePatch] Found: {type.FullName} - IsOpen:{hasIsOpen} Show:{hasShow} Hide:{hasHide}");
                        }
                    }
                }
                catch
                {
                    // Ignore assembly load errors
                }
            }

            MelonLogger.Msg("[PanelStatePatch] === Discovery complete ===");
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

            MelonLogger.Msg($"[PanelStatePatch] Found SettingsMenu: {settingsType.FullName}");

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
                    MelonLogger.Msg($"[PanelStatePatch] Patched SettingsMenu.{method.Name}()");
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
                    MelonLogger.Msg($"[PanelStatePatch] Patched SettingsMenu.{method.Name}()");
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
                    MelonLogger.Msg("[PanelStatePatch] Patched SettingsMenu.IsOpen setter");
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
                    MelonLogger.Msg("[PanelStatePatch] Patched SettingsMenu.IsMainPanelActive setter");
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

            MelonLogger.Msg($"[PanelStatePatch] Found DeckSelectBlade: {deckBladeType.FullName}");

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
                    MelonLogger.Msg($"[PanelStatePatch] Patched DeckSelectBlade.Show({paramStr})");
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
                    MelonLogger.Msg("[PanelStatePatch] Patched DeckSelectBlade.Hide()");
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
                    MelonLogger.Msg("[PanelStatePatch] Patched DeckSelectBlade.IsShowing setter");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PanelStatePatch] Failed to patch DeckSelectBlade.IsShowing setter: {ex.Message}");
                }
            }
        }

        private static void LogTypeMembers(Type type)
        {
            MelonLogger.Msg($"[PanelStatePatch] Methods on {type.Name}:");
            foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (m.Name.Contains("Show") || m.Name.Contains("Hide") || m.Name.Contains("Open") || m.Name.Contains("Close"))
                {
                    MelonLogger.Msg($"  - {m.Name}({string.Join(", ", Array.ConvertAll(m.GetParameters(), p => p.ParameterType.Name))})");
                }
            }

            MelonLogger.Msg($"[PanelStatePatch] Properties on {type.Name}:");
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (p.Name.Contains("Open") || p.Name.Contains("Ready") || p.Name.Contains("Visible"))
                {
                    MelonLogger.Msg($"  - {p.Name} ({p.PropertyType.Name})");
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
                MelonLogger.Msg($"[PanelStatePatch] Panel Show: {typeName}");
                OnPanelStateChanged?.Invoke(__instance, true, typeName);
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
                MelonLogger.Msg($"[PanelStatePatch] Panel Hide: {typeName}");
                OnPanelStateChanged?.Invoke(__instance, false, typeName);
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
                MelonLogger.Msg($"[PanelStatePatch] Panel IsOpen = {value}: {typeName}");
                OnPanelStateChanged?.Invoke(__instance, value, typeName);
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
                MelonLogger.Msg("[PanelStatePatch] SettingsMenu Show");
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
                MelonLogger.Msg("[PanelStatePatch] SettingsMenu Hide");
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
                MelonLogger.Msg("[PanelStatePatch] DeckSelectBlade Show");
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
                MelonLogger.Msg("[PanelStatePatch] DeckSelectBlade Hide");
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
                MelonLogger.Msg($"[PanelStatePatch] Panel BeginOpen: {typeName}");
                // Don't fire event yet - wait for FinishOpen when UI is ready
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
                MelonLogger.Msg($"[PanelStatePatch] Panel BeginClose: {typeName}");
                // Fire event early so navigator knows panel is closing
                OnPanelStateChanged?.Invoke(__instance, false, typeName);
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
                MelonLogger.Msg($"[PanelStatePatch] SettingsMenu IsOpen = {value}");
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
                MelonLogger.Msg($"[PanelStatePatch] SettingsMenu IsMainPanelActive = {value}");
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
                MelonLogger.Msg($"[PanelStatePatch] DeckSelectBlade IsShowing = {value}");
                OnPanelStateChanged?.Invoke(__instance, value, "DeckSelectBlade");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PanelStatePatch] Error in DeckSelectIsShowingPostfix: {ex.Message}");
            }
        }
    }
}
