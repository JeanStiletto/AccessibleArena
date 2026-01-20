using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace AccessibleArenaInstaller
{
    /// <summary>
    /// Manages Windows registry entries for Add/Remove Programs.
    /// </summary>
    public static class RegistryManager
    {
        private const string UninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        private const string AppKeyName = "AccessibleArena";

        /// <summary>
        /// Registers the application in Add/Remove Programs.
        /// </summary>
        /// <param name="installPath">The MTGA installation path</param>
        /// <param name="version">The mod version being installed</param>
        public static void Register(string installPath, string version)
        {
            try
            {
                Logger.Info("Registering in Add/Remove Programs...");

                string uninstallKeyFullPath = $@"{UninstallKeyPath}\{AppKeyName}";

                using (var key = Registry.LocalMachine.CreateSubKey(uninstallKeyFullPath))
                {
                    if (key == null)
                    {
                        Logger.Warning("Could not create registry key - may need admin rights");
                        return;
                    }

                    // Get the path to this installer executable
                    string installerPath = Assembly.GetExecutingAssembly().Location;

                    // Basic info
                    key.SetValue("DisplayName", Config.DisplayName);
                    key.SetValue("DisplayVersion", version ?? "1.0.0");
                    key.SetValue("Publisher", Config.Publisher);
                    key.SetValue("InstallLocation", installPath);
                    key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));

                    // Uninstall command - same exe with /uninstall flag
                    key.SetValue("UninstallString", $"\"{installerPath}\" /uninstall \"{installPath}\"");
                    key.SetValue("QuietUninstallString", $"\"{installerPath}\" /uninstall \"{installPath}\" /quiet");

                    // Additional info
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

                    // Estimated size in KB (rough estimate)
                    key.SetValue("EstimatedSize", 5000, RegistryValueKind.DWord);

                    // URLs
                    key.SetValue("URLInfoAbout", Config.ModRepositoryUrl);
                    key.SetValue("HelpLink", Config.ModRepositoryUrl + "/issues");

                    Logger.Info("Successfully registered in Add/Remove Programs");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Warning($"Could not register in Add/Remove Programs (access denied): {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to register in Add/Remove Programs", ex);
            }
        }

        /// <summary>
        /// Removes the application from Add/Remove Programs.
        /// </summary>
        public static void Unregister()
        {
            try
            {
                Logger.Info("Removing from Add/Remove Programs...");

                string uninstallKeyFullPath = $@"{UninstallKeyPath}\{AppKeyName}";

                using (var parentKey = Registry.LocalMachine.OpenSubKey(UninstallKeyPath, writable: true))
                {
                    if (parentKey == null)
                    {
                        Logger.Warning("Could not open Uninstall registry key");
                        return;
                    }

                    // Check if our key exists
                    using (var existingKey = parentKey.OpenSubKey(AppKeyName))
                    {
                        if (existingKey == null)
                        {
                            Logger.Info("Registry entry does not exist, nothing to remove");
                            return;
                        }
                    }

                    parentKey.DeleteSubKeyTree(AppKeyName);
                    Logger.Info("Successfully removed from Add/Remove Programs");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Warning($"Could not remove from Add/Remove Programs (access denied): {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to remove from Add/Remove Programs", ex);
            }
        }

        /// <summary>
        /// Checks if the application is registered in Add/Remove Programs.
        /// </summary>
        public static bool IsRegistered()
        {
            try
            {
                string uninstallKeyFullPath = $@"{UninstallKeyPath}\{AppKeyName}";

                using (var key = Registry.LocalMachine.OpenSubKey(uninstallKeyFullPath))
                {
                    return key != null;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the registered install location from the registry.
        /// </summary>
        public static string GetRegisteredInstallLocation()
        {
            try
            {
                string uninstallKeyFullPath = $@"{UninstallKeyPath}\{AppKeyName}";

                using (var key = Registry.LocalMachine.OpenSubKey(uninstallKeyFullPath))
                {
                    return key?.GetValue("InstallLocation") as string;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the registered version from the registry.
        /// </summary>
        public static string GetRegisteredVersion()
        {
            try
            {
                string uninstallKeyFullPath = $@"{UninstallKeyPath}\{AppKeyName}";

                using (var key = Registry.LocalMachine.OpenSubKey(uninstallKeyFullPath))
                {
                    return key?.GetValue("DisplayVersion") as string;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
