using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Windows.Forms;

namespace AccessibleArenaInstaller
{
    public enum UpdateChoice
    {
        Close,
        UpdateOnly,
        FullInstall
    }

    static class Program
    {
        public const string DefaultMtgaPath = @"C:\Program Files\Wizards of the Coast\MTGA";
        public const string MtgaExeName = "MTGA.exe";

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Logger.Info("Accessible Arena Installer starting...");
            Logger.Info($"Running as: {Environment.UserName}");
            Logger.Info($"Is Admin: {IsRunningAsAdmin()}");
            Logger.Info($"Arguments: {string.Join(" ", args)}");

            // Pre-flight check: Admin rights
            if (!IsRunningAsAdmin())
            {
                Logger.Error("Not running as administrator");
                MessageBox.Show(
                    "This installer requires administrator privileges.\n\nPlease right-click and select 'Run as administrator'.",
                    "Administrator Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // Pre-flight check: MTGA not running
            if (IsMtgaRunning())
            {
                Logger.Warning("MTGA is currently running");
                MessageBox.Show(
                    "Please close Magic: The Gathering Arena first.\n\nThe installer cannot modify files while the game is running.",
                    "MTGA Running",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Parse command line arguments
            bool uninstallMode = false;
            bool quietMode = false;
            string pathArg = null;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLowerInvariant();

                if (arg == "/uninstall" || arg == "-uninstall" || arg == "--uninstall")
                {
                    uninstallMode = true;
                }
                else if (arg == "/quiet" || arg == "-quiet" || arg == "--quiet" || arg == "/q" || arg == "-q")
                {
                    quietMode = true;
                }
                else if (!arg.StartsWith("/") && !arg.StartsWith("-"))
                {
                    // Assume it's a path argument
                    pathArg = args[i];
                }
            }

            if (uninstallMode)
            {
                // Uninstall mode
                Logger.Info("Running in uninstall mode");

                string mtgaPath = pathArg ?? RegistryManager.GetRegisteredInstallLocation() ?? DetectMtgaPath();

                if (string.IsNullOrEmpty(mtgaPath) || !IsValidMtgaPath(mtgaPath))
                {
                    if (!quietMode)
                    {
                        MessageBox.Show(
                            "Could not determine MTGA installation path.\n\nPlease run the installer normally to reinstall.",
                            "Uninstall Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                    Logger.Error("Uninstall failed: Could not determine MTGA path");
                    return;
                }

                if (quietMode)
                {
                    // Silent uninstall
                    PerformUninstall(mtgaPath, askAboutMelonLoader: false);
                }
                else
                {
                    // Show uninstall UI
                    Application.Run(new UninstallForm(mtgaPath));
                }
            }
            else
            {
                // Install mode - check for updates first
                string mtgaPath = pathArg ?? DefaultMtgaPath;
                string modPath = Path.Combine(mtgaPath, "Mods", Config.ModDllName);

                bool modExists = File.Exists(modPath);
                bool updateAvailable = false;
                string installedVersion = null;
                string latestVersion = null;

                if (modExists)
                {
                    Logger.Info("Mod found in default location, checking for updates...");

                    // Get installed version
                    try
                    {
                        var assemblyName = AssemblyName.GetAssemblyName(modPath);
                        installedVersion = assemblyName.Version.ToString();
                        Logger.Info($"Installed version: {installedVersion}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Could not read installed mod version: {ex.Message}");
                    }

                    // Get latest version from GitHub
                    if (installedVersion != null)
                    {
                        try
                        {
                            using (var client = new GitHubClient())
                            {
                                latestVersion = client.GetLatestModVersionAsync(Config.ModRepositoryUrl).GetAwaiter().GetResult();
                                Logger.Info($"Latest version: {latestVersion}");

                                if (latestVersion != null)
                                {
                                    updateAvailable = IsNewerVersion(latestVersion, installedVersion);
                                    Logger.Info($"Update available: {updateAvailable}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"Could not check for updates: {ex.Message}");
                        }
                    }
                }

                // Show appropriate dialog based on update check
                if (modExists && updateAvailable)
                {
                    // Show update dialog with three options
                    Logger.Info("Showing update dialog");
                    var updateForm = new UpdateAvailableForm(installedVersion, latestVersion);
                    Application.Run(updateForm);

                    switch (updateForm.UserChoice)
                    {
                        case UpdateChoice.UpdateOnly:
                            // Go directly to MainForm for update
                            Logger.Info("User chose to update mod only");
                            Application.Run(new MainForm(mtgaPath, updateOnly: true));
                            break;

                        case UpdateChoice.FullInstall:
                            // Continue with full install flow
                            Logger.Info("User chose full install");
                            var welcomeForm = new WelcomeForm();
                            Application.Run(welcomeForm);
                            if (welcomeForm.ProceedWithInstall)
                            {
                                mtgaPath = pathArg ?? DetectMtgaPath();
                                Application.Run(new MainForm(mtgaPath));
                            }
                            break;

                        case UpdateChoice.Close:
                        default:
                            Logger.Info("User cancelled from update dialog");
                            break;
                    }
                }
                else
                {
                    // No mod installed or no update available - show default welcome dialog
                    var welcomeResult = MessageBox.Show(
                        "Welcome to Accessible Arena!\n\n" +
                        "This will install the accessibility mod for playing Magic: The Gathering Arena on your computer.",
                        "Accessible Arena Installer",
                        MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Information);

                    if (welcomeResult != DialogResult.OK)
                    {
                        Logger.Info("Installation cancelled from welcome message");
                        return;
                    }

                    // Show main welcome form with options
                    var welcomeForm = new WelcomeForm();
                    Application.Run(welcomeForm);

                    if (!welcomeForm.ProceedWithInstall)
                    {
                        Logger.Info("Installation cancelled from welcome dialog");
                        return;
                    }

                    // Proceed with installation
                    mtgaPath = pathArg ?? DetectMtgaPath();
                    Logger.Info($"Detected MTGA path: {mtgaPath ?? "Not found"}");

                    Application.Run(new MainForm(mtgaPath));
                }
            }
        }

        /// <summary>
        /// Performs the uninstallation (used by both quiet mode and UninstallForm).
        /// </summary>
        public static void PerformUninstall(string mtgaPath, bool askAboutMelonLoader = true)
        {
            Logger.Info($"Uninstalling from: {mtgaPath}");

            try
            {
                string modsPath = Path.Combine(mtgaPath, "Mods");

                // Remove mod DLL
                string modDll = Path.Combine(modsPath, Config.ModDllName);
                if (File.Exists(modDll))
                {
                    Logger.Info($"Removing {Config.ModDllName}...");
                    File.Delete(modDll);
                }

                // Remove mod DLL backup
                string modDllBackup = modDll + ".backup";
                if (File.Exists(modDllBackup))
                {
                    Logger.Info("Removing mod backup...");
                    File.Delete(modDllBackup);
                }

                // Remove Tolk DLLs
                string[] tolkDlls = { "Tolk.dll", "nvdaControllerClient64.dll" };
                foreach (var dll in tolkDlls)
                {
                    string dllPath = Path.Combine(mtgaPath, dll);
                    if (File.Exists(dllPath))
                    {
                        Logger.Info($"Removing {dll}...");
                        File.Delete(dllPath);
                    }

                    // Also remove backup
                    string backupPath = dllPath + ".backup";
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                }

                // Remove empty Mods folder
                if (Directory.Exists(modsPath))
                {
                    var files = Directory.GetFiles(modsPath);
                    var dirs = Directory.GetDirectories(modsPath);

                    if (files.Length == 0 && dirs.Length == 0)
                    {
                        Logger.Info("Removing empty Mods folder...");
                        Directory.Delete(modsPath);
                    }
                    else
                    {
                        Logger.Info("Keeping Mods folder (contains other files)");
                    }
                }

                // Remove registry entry
                RegistryManager.Unregister();

                Logger.Info("Uninstallation complete");
                Logger.Flush();
            }
            catch (Exception ex)
            {
                Logger.Error("Uninstallation failed", ex);
                Logger.Flush();
                throw;
            }
        }

        /// <summary>
        /// Removes MelonLoader from the MTGA installation.
        /// </summary>
        public static void UninstallMelonLoader(string mtgaPath)
        {
            Logger.Info("Removing MelonLoader...");

            try
            {
                // Remove MelonLoader folder
                string melonLoaderFolder = Path.Combine(mtgaPath, "MelonLoader");
                if (Directory.Exists(melonLoaderFolder))
                {
                    Directory.Delete(melonLoaderFolder, recursive: true);
                    Logger.Info("Removed MelonLoader folder");
                }

                // Remove MelonLoader DLLs
                string[] mlDlls = { "version.dll", "dobby.dll" };
                foreach (var dll in mlDlls)
                {
                    string dllPath = Path.Combine(mtgaPath, dll);
                    if (File.Exists(dllPath))
                    {
                        File.Delete(dllPath);
                        Logger.Info($"Removed {dll}");
                    }
                }

                // Remove MelonLoader backup folder if exists
                string backupFolder = Path.Combine(mtgaPath, "MelonLoader.backup");
                if (Directory.Exists(backupFolder))
                {
                    Directory.Delete(backupFolder, recursive: true);
                }

                Logger.Info("MelonLoader removed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to remove MelonLoader", ex);
                throw;
            }
        }

        private static bool IsRunningAsAdmin()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool IsMtgaRunning()
        {
            try
            {
                var processes = Process.GetProcessesByName("MTGA");
                return processes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Attempts to detect the MTGA installation path.
        /// Returns null if not found.
        /// </summary>
        public static string DetectMtgaPath()
        {
            // First check registry for previously registered install
            string registeredPath = RegistryManager.GetRegisteredInstallLocation();
            if (IsValidMtgaPath(registeredPath))
            {
                return registeredPath;
            }

            // Check default location
            if (IsValidMtgaPath(DefaultMtgaPath))
            {
                return DefaultMtgaPath;
            }

            // Check alternate location (some users might have it elsewhere)
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string alternatePath = Path.Combine(programFilesX86, "Wizards of the Coast", "MTGA");
            if (IsValidMtgaPath(alternatePath))
            {
                return alternatePath;
            }

            return null;
        }

        /// <summary>
        /// Validates that the given path contains MTGA.exe
        /// </summary>
        public static bool IsValidMtgaPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string exePath = Path.Combine(path, MtgaExeName);
            return File.Exists(exePath);
        }

        /// <summary>
        /// Compares two version strings and returns true if the latest version is newer.
        /// Normalizes both versions to 4 components so "0.4" and "0.4.0.0" compare correctly.
        /// </summary>
        internal static bool IsNewerVersion(string latestVersion, string installedVersion)
        {
            try
            {
                var latest = NormalizeVersion(latestVersion);
                var installed = NormalizeVersion(installedVersion);

                Logger.Info($"Version comparison: latest={latest}, installed={installed}");
                return latest > installed;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Version comparison failed: {ex.Message}");
                return !string.Equals(latestVersion, installedVersion, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Normalizes a version string to a 4-component Version object.
        /// Strips 'v' prefix, pre-release suffixes, and pads missing components with 0.
        /// </summary>
        internal static Version NormalizeVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return new Version(0, 0, 0, 0);

            version = version.TrimStart('v', 'V');

            // Remove pre-release suffix (e.g., "0.4.0-beta")
            int dashIndex = version.IndexOf('-');
            if (dashIndex > 0)
                version = version.Substring(0, dashIndex);

            int spaceIndex = version.IndexOf(' ');
            if (spaceIndex > 0)
                version = version.Substring(0, spaceIndex);

            // Split and pad to 4 components
            string[] parts = version.Trim().Split('.');
            int major = parts.Length > 0 && int.TryParse(parts[0], out int m) ? m : 0;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out int n) ? n : 0;
            int build = parts.Length > 2 && int.TryParse(parts[2], out int b) ? b : 0;
            int revision = parts.Length > 3 && int.TryParse(parts[3], out int r) ? r : 0;

            return new Version(major, minor, build, revision);
        }
    }
}
