using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using AccessibleArena.Core.Models;
using MelonLoader;
using Microsoft.Win32;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Detects if the game is running under Steam and warns the user to disable
    /// the Steam overlay to prevent Shift+Tab conflicts with mod navigation.
    ///
    /// The installer can programmatically disable the overlay by setting
    /// OverlayAppEnable=0 in Steam's localconfig.vdf. This class checks whether
    /// that was done and announces a warning if not.
    ///
    /// Note: A low-level keyboard hook (WH_KEYBOARD_LL) approach was tried but abandoned
    /// because it adds latency to ALL keystrokes, which disrupts NVDA's key capture timing
    /// (screen reader modifier keys leak through as bare letters). Steam also doesn't rely
    /// on the LL hook chain for overlay detection, so suppressing there doesn't help.
    /// </summary>
    public static class SteamOverlayBlocker
    {
        private const string MtgaAppId = "2141910";

        public static bool IsSteam { get; private set; }
        public static bool OverlayDisabled { get; private set; }

        /// <summary>
        /// Check if running under Steam and whether overlay is disabled. Call once during init.
        /// </summary>
        public static void Install()
        {
            IsSteam = IsSteamLoaded();
            if (!IsSteam)
            {
                MelonLogger.Msg("[SteamOverlayBlocker] Steam not detected, skipping");
                return;
            }

            OverlayDisabled = IsOverlayDisabledInVdf();
            if (OverlayDisabled)
            {
                MelonLogger.Msg("[SteamOverlayBlocker] Steam detected, overlay already disabled for MTGA");
            }
            else
            {
                MelonLogger.Warning("[SteamOverlayBlocker] Steam detected, overlay NOT disabled - Shift+Tab will open Steam overlay");
            }
        }

        /// <summary>
        /// No-op kept for API compatibility.
        /// </summary>
        public static void Uninstall() { }

        private static bool IsSteamLoaded()
        {
            try
            {
                foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
                {
                    string name = module.ModuleName;
                    if (name.Equals("steam_api64.dll", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("steam_api.dll", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("GameOverlayRenderer64.dll", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("GameOverlayRenderer.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SteamOverlayBlocker] Error checking modules: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Checks Steam's localconfig.vdf to see if the overlay is disabled for MTGA.
        /// </summary>
        private static bool IsOverlayDisabledInVdf()
        {
            try
            {
                string steamRoot = GetSteamRoot();
                if (steamRoot == null)
                {
                    MelonLogger.Msg("[SteamOverlayBlocker] Could not determine Steam root path");
                    return false;
                }

                string userDataPath = Path.Combine(steamRoot, "userdata");
                if (!Directory.Exists(userDataPath))
                    return false;

                foreach (var userDir in Directory.GetDirectories(userDataPath))
                {
                    string vdfPath = Path.Combine(userDir, "config", "localconfig.vdf");
                    if (!File.Exists(vdfPath))
                        continue;

                    string content = File.ReadAllText(vdfPath);
                    if (Regex.IsMatch(content,
                        $@"""{MtgaAppId}""[^{{}}]*\{{[^{{}}]*""OverlayAppEnable""\s+""0"""))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SteamOverlayBlocker] Error checking VDF: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Finds Steam root from the game's executable path or the registry.
        /// </summary>
        private static string GetSteamRoot()
        {
            // Try deriving from game executable path
            try
            {
                string gamePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (gamePath != null)
                {
                    string normalized = gamePath.Replace('/', '\\');
                    int idx = normalized.IndexOf(@"\steamapps\common\", StringComparison.OrdinalIgnoreCase);
                    if (idx > 0)
                        return normalized.Substring(0, idx);
                }
            }
            catch { }

            // Fallback: registry
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    string regPath = key?.GetValue("SteamPath") as string;
                    if (regPath != null && Directory.Exists(regPath))
                        return regPath.Replace('/', '\\');
                }
            }
            catch { }

            return null;
        }
    }
}
