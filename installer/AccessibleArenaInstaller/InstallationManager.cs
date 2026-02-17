using System;
using System.IO;
using System.Reflection;

namespace AccessibleArenaInstaller
{
    /// <summary>
    /// Handles the core installation logic: copying files, checking installations, etc.
    /// </summary>
    public class InstallationManager
    {
        private readonly string _mtgaPath;
        private readonly string _modsPath;

        // Embedded resource names for Tolk DLLs
        private static readonly string[] TolkDlls = new[]
        {
            "Tolk.dll",
            "nvdaControllerClient64.dll"
        };

        public InstallationManager(string mtgaPath)
        {
            _mtgaPath = mtgaPath ?? throw new ArgumentNullException(nameof(mtgaPath));
            _modsPath = Path.Combine(_mtgaPath, "Mods");
        }

        /// <summary>
        /// Copies embedded Tolk DLLs to the MTGA root folder.
        /// </summary>
        public void CopyTolkDlls()
        {
            Logger.Info("Copying Tolk DLLs to MTGA folder...");

            foreach (var dllName in TolkDlls)
            {
                CopyEmbeddedResource(dllName, _mtgaPath);
            }

            Logger.Info("Tolk DLLs copied successfully");
        }

        /// <summary>
        /// Extracts an embedded resource to the target folder.
        /// </summary>
        private void CopyEmbeddedResource(string resourceName, string targetFolder)
        {
            string targetPath = Path.Combine(targetFolder, resourceName);

            // Backup existing file if present
            if (File.Exists(targetPath))
            {
                string backupPath = targetPath + ".backup";
                Logger.Info($"Backing up existing {resourceName} to {backupPath}");
                File.Copy(targetPath, backupPath, overwrite: true);
            }

            // Find the embedded resource
            var assembly = Assembly.GetExecutingAssembly();
            string fullResourceName = FindResourceName(assembly, resourceName);

            if (fullResourceName == null)
            {
                // Resource not embedded - try to copy from Resources folder (development mode)
                string devPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", resourceName);
                if (File.Exists(devPath))
                {
                    Logger.Info($"Copying {resourceName} from development path");
                    File.Copy(devPath, targetPath, overwrite: true);
                    return;
                }

                throw new FileNotFoundException($"Embedded resource not found: {resourceName}");
            }

            Logger.Info($"Extracting {resourceName} to {targetPath}");

            using (var stream = assembly.GetManifestResourceStream(fullResourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException($"Could not open resource stream: {fullResourceName}");
                }

                using (var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fileStream);
                }
            }

            Logger.Info($"Successfully extracted {resourceName}");
        }

        /// <summary>
        /// Finds the full resource name for an embedded resource.
        /// </summary>
        private string FindResourceName(Assembly assembly, string shortName)
        {
            string[] resourceNames = assembly.GetManifestResourceNames();

            foreach (var name in resourceNames)
            {
                if (name.EndsWith(shortName, StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }

            // Log available resources for debugging
            Logger.Warning($"Resource '{shortName}' not found. Available resources:");
            foreach (var name in resourceNames)
            {
                Logger.Warning($"  - {name}");
            }

            return null;
        }

        /// <summary>
        /// Checks if MelonLoader is installed.
        /// </summary>
        public bool IsMelonLoaderInstalled()
        {
            string melonLoaderFolder = Path.Combine(_mtgaPath, "MelonLoader");
            string versionDll = Path.Combine(_mtgaPath, "version.dll");

            bool folderExists = Directory.Exists(melonLoaderFolder);
            bool dllExists = File.Exists(versionDll);

            Logger.Info($"MelonLoader check - Folder exists: {folderExists}, version.dll exists: {dllExists}");

            return folderExists && dllExists;
        }

        /// <summary>
        /// Gets the installed MelonLoader version, if available.
        /// </summary>
        public string GetMelonLoaderVersion()
        {
            // MelonLoader stores version info in MelonLoader/net35/MelonLoader.dll or similar
            // For now, just return "unknown" - can be enhanced later
            if (!IsMelonLoaderInstalled())
                return null;

            return "installed";
        }

        /// <summary>
        /// Ensures the Mods folder exists.
        /// </summary>
        public void EnsureModsFolderExists()
        {
            if (!Directory.Exists(_modsPath))
            {
                Logger.Info($"Creating Mods folder: {_modsPath}");
                Directory.CreateDirectory(_modsPath);
            }
            else
            {
                Logger.Info($"Mods folder already exists: {_modsPath}");
            }
        }

        /// <summary>
        /// Checks if the mod is already installed.
        /// </summary>
        public bool IsModInstalled()
        {
            string modPath = Path.Combine(_modsPath, Config.ModDllName);
            return File.Exists(modPath);
        }

        /// <summary>
        /// Gets the installed mod version, if available.
        /// </summary>
        public string GetInstalledModVersion()
        {
            string modPath = Path.Combine(_modsPath, Config.ModDllName);

            if (!File.Exists(modPath))
                return null;

            try
            {
                var assemblyName = AssemblyName.GetAssemblyName(modPath);
                return assemblyName.Version.ToString();
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not read mod version: {ex.Message}");
                return "unknown";
            }
        }

        /// <summary>
        /// Copies the mod DLL to the Mods folder.
        /// </summary>
        public void InstallModDll(string sourcePath)
        {
            EnsureModsFolderExists();

            string targetPath = Path.Combine(_modsPath, Config.ModDllName);

            // Backup existing
            if (File.Exists(targetPath))
            {
                string backupPath = targetPath + ".backup";
                Logger.Info($"Backing up existing mod to {backupPath}");
                File.Copy(targetPath, backupPath, overwrite: true);
            }

            Logger.Info($"Copying mod DLL from {sourcePath} to {targetPath}");
            File.Copy(sourcePath, targetPath, overwrite: true);
            Logger.Info("Mod DLL installed successfully");
        }

        /// <summary>
        /// Writes the mod settings file with the selected language.
        /// Creates UserData/AccessibleArena.json in the MTGA folder.
        /// If the file already exists, only updates the Language field.
        /// </summary>
        public void WriteModSettings(string language)
        {
            string userDataPath = Path.Combine(_mtgaPath, "UserData");
            string settingsPath = Path.Combine(userDataPath, "AccessibleArena.json");

            try
            {
                if (!Directory.Exists(userDataPath))
                {
                    Logger.Info($"Creating UserData folder: {userDataPath}");
                    Directory.CreateDirectory(userDataPath);
                }

                if (File.Exists(settingsPath))
                {
                    // Settings file exists - update only the Language field, preserve other settings
                    Logger.Info($"Updating language in existing settings to: {language}");
                    string json = File.ReadAllText(settingsPath);

                    // Replace the Language value in the JSON
                    var match = System.Text.RegularExpressions.Regex.Match(
                        json, @"""Language""\s*:\s*""[^""]*""");
                    if (match.Success)
                    {
                        json = json.Substring(0, match.Index)
                             + $"\"Language\": \"{language}\""
                             + json.Substring(match.Index + match.Length);
                    }

                    File.WriteAllText(settingsPath, json);
                }
                else
                {
                    // Create new settings file with defaults
                    Logger.Info($"Creating mod settings with language: {language}");
                    string json = "{\n"
                                + $"  \"Language\": \"{language}\",\n"
                                + "  \"TutorialMessages\": true,\n"
                                + "  \"VerboseAnnouncements\": true\n"
                                + "}";
                    File.WriteAllText(settingsPath, json);
                }

                Logger.Info("Mod settings written successfully");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not write mod settings: {ex.Message}");
            }
        }
    }
}
