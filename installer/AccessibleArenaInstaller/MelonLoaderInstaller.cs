using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace AccessibleArenaInstaller
{
    /// <summary>
    /// Handles MelonLoader download and installation via ZIP extraction.
    /// </summary>
    public class MelonLoaderInstaller
    {
        private readonly string _mtgaPath;
        private readonly GitHubClient _githubClient;

        // Files that go in the MTGA root folder
        private static readonly string[] RootFiles = new[]
        {
            "version.dll",  // Proxy DLL that bootstraps MelonLoader
            "dobby.dll"     // Required for Il2Cpp games (MTGA uses Il2Cpp)
        };

        // Folder that gets extracted to MTGA root
        private const string MelonLoaderFolder = "MelonLoader";

        public MelonLoaderInstaller(string mtgaPath, GitHubClient githubClient)
        {
            _mtgaPath = mtgaPath ?? throw new ArgumentNullException(nameof(mtgaPath));
            _githubClient = githubClient ?? throw new ArgumentNullException(nameof(githubClient));
        }

        /// <summary>
        /// Checks if MelonLoader is installed.
        /// </summary>
        public bool IsInstalled()
        {
            string melonLoaderFolder = Path.Combine(_mtgaPath, MelonLoaderFolder);
            string versionDll = Path.Combine(_mtgaPath, "version.dll");

            return Directory.Exists(melonLoaderFolder) && File.Exists(versionDll);
        }

        /// <summary>
        /// Gets the installed MelonLoader version by checking the Dependencies folder.
        /// Returns null if not installed or version cannot be determined.
        /// </summary>
        public string GetInstalledVersion()
        {
            if (!IsInstalled())
                return null;

            // MelonLoader stores version info, but it's not easily accessible
            // For now, just return "installed" if present
            return "installed";
        }

        /// <summary>
        /// Downloads and installs MelonLoader to the MTGA folder.
        /// </summary>
        /// <param name="progress">Progress callback (0-100)</param>
        public async Task InstallAsync(Action<int, string> progress = null)
        {
            string zipPath = null;

            try
            {
                // Step 1: Get latest version
                progress?.Invoke(0, "Checking latest MelonLoader version...");
                string version = await _githubClient.GetLatestMelonLoaderVersionAsync();

                if (string.IsNullOrEmpty(version))
                {
                    throw new Exception("Could not determine latest MelonLoader version");
                }

                Logger.Info($"Will install MelonLoader {version}");

                // Step 2: Download ZIP
                progress?.Invoke(10, $"Downloading MelonLoader {version}...");
                zipPath = await _githubClient.DownloadMelonLoaderAsync(version, p =>
                {
                    // Map download progress (0-100) to overall progress (10-70)
                    int overallProgress = 10 + (p * 60 / 100);
                    progress?.Invoke(overallProgress, $"Downloading MelonLoader {version}... {p}%");
                });

                // Step 3: Backup existing installation if present
                progress?.Invoke(70, "Preparing installation...");
                BackupExistingInstallation();

                // Step 4: Extract ZIP
                progress?.Invoke(75, "Extracting MelonLoader...");
                ExtractMelonLoader(zipPath);

                // Step 5: Verify installation
                progress?.Invoke(95, "Verifying installation...");
                if (!IsInstalled())
                {
                    throw new Exception("MelonLoader installation verification failed");
                }

                progress?.Invoke(100, "MelonLoader installed successfully!");
                Logger.Info("MelonLoader installation completed successfully");
            }
            finally
            {
                // Clean up downloaded ZIP
                if (zipPath != null && File.Exists(zipPath))
                {
                    try
                    {
                        File.Delete(zipPath);
                        Logger.Info("Cleaned up temporary ZIP file");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Could not delete temp file: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Backs up existing MelonLoader installation.
        /// </summary>
        private void BackupExistingInstallation()
        {
            string melonLoaderFolder = Path.Combine(_mtgaPath, MelonLoaderFolder);
            string backupFolder = Path.Combine(_mtgaPath, "MelonLoader.backup");

            // Backup MelonLoader folder
            if (Directory.Exists(melonLoaderFolder))
            {
                Logger.Info("Backing up existing MelonLoader folder...");

                if (Directory.Exists(backupFolder))
                {
                    Directory.Delete(backupFolder, recursive: true);
                }

                Directory.Move(melonLoaderFolder, backupFolder);
            }

            // Backup root DLLs
            foreach (var fileName in RootFiles)
            {
                string filePath = Path.Combine(_mtgaPath, fileName);
                if (File.Exists(filePath))
                {
                    string backupPath = filePath + ".backup";
                    Logger.Info($"Backing up {fileName}...");
                    File.Copy(filePath, backupPath, overwrite: true);
                }
            }
        }

        /// <summary>
        /// Extracts MelonLoader from the downloaded ZIP file.
        /// </summary>
        private void ExtractMelonLoader(string zipPath)
        {
            Logger.Info($"Extracting MelonLoader from: {zipPath}");

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    // Skip directories (they have empty names ending with /)
                    if (string.IsNullOrEmpty(entry.Name))
                        continue;

                    string destinationPath = Path.Combine(_mtgaPath, entry.FullName);

                    // Ensure directory exists
                    string destinationDir = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }

                    // Extract file
                    Logger.Info($"Extracting: {entry.FullName}");
                    entry.ExtractToFile(destinationPath, overwrite: true);
                }
            }

            Logger.Info("MelonLoader extraction complete");
        }

        /// <summary>
        /// Uninstalls MelonLoader from the MTGA folder.
        /// </summary>
        public void Uninstall()
        {
            Logger.Info("Uninstalling MelonLoader...");

            // Remove MelonLoader folder
            string melonLoaderFolder = Path.Combine(_mtgaPath, MelonLoaderFolder);
            if (Directory.Exists(melonLoaderFolder))
            {
                Logger.Info("Removing MelonLoader folder...");
                Directory.Delete(melonLoaderFolder, recursive: true);
            }

            // Remove root DLLs
            foreach (var fileName in RootFiles)
            {
                string filePath = Path.Combine(_mtgaPath, fileName);
                if (File.Exists(filePath))
                {
                    Logger.Info($"Removing {fileName}...");
                    File.Delete(filePath);
                }
            }

            // Also remove generated folders
            string[] generatedFolders = { "Mods", "Plugins", "UserData", "UserLibs" };
            foreach (var folderName in generatedFolders)
            {
                string folderPath = Path.Combine(_mtgaPath, folderName);
                if (Directory.Exists(folderPath))
                {
                    // Only remove if empty (don't delete user's mods)
                    if (Directory.GetFiles(folderPath).Length == 0 &&
                        Directory.GetDirectories(folderPath).Length == 0)
                    {
                        Logger.Info($"Removing empty {folderName} folder...");
                        Directory.Delete(folderPath);
                    }
                    else
                    {
                        Logger.Info($"Keeping {folderName} folder (not empty)");
                    }
                }
            }

            Logger.Info("MelonLoader uninstallation complete");
        }
    }
}
