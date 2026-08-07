using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MelonLoader;
using UnityEngine;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Handles checking for mod updates on GitHub and performing auto-update via batch script.
    /// Version check runs in background on startup. F5 triggers download + replace + relaunch.
    /// </summary>
    public static class UpdateChecker
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/JeanStiletto/AccessibleArena/releases/latest";
        private const string ModDllAssetName = "AccessibleArena.dll";

        // Speech runtime, published as a release asset since v1.4.6. An update that replaced only
        // the mod DLL would leave a pre-v1.4.6 install with Tolk and no prism.dll, and the mod
        // would come up unable to speak — with no way to say so.
        private const string PrismDllAssetName = "prism.dll";

        private const int CheckTimeoutMs = 5000;
        private const int DownloadTimeoutMs = 30000;

        // Background check state
        private static volatile bool _updateAvailable;
        private static volatile string _latestVersion;
        private static volatile bool _checkComplete;
        private static volatile bool _announced;
        private static volatile bool _checking;

        // Download state
        private static Task<string> _downloadTask;
        private static volatile bool _downloadComplete;
        private static volatile bool _downloadFailed;
        private static volatile string _downloadedPath;

        /// <summary>Downloaded speech runtime, or null when the installed one is already current.</summary>
        private static volatile string _downloadedPrismPath;

        // Cached release JSON for extracting asset URL during download
        private static volatile string _releaseJson;

        public static bool IsUpdateAvailable => _updateAvailable;
        public static string LatestVersion => _latestVersion;

        /// <summary>
        /// Start a background version check against GitHub releases.
        /// Call once from OnInitializeMelon.
        /// </summary>
        public static void CheckInBackground(string currentVersion)
        {
            if (_checking) return;
            _checking = true;

            Task.Run(() =>
            {
                try
                {
                    CheckVersion(currentVersion);
                }
                catch (Exception ex)
                {
                    Log.Warn("UpdateChecker", $"Version check failed: {ex.Message}");
                }
                finally
                {
                    _checkComplete = true;
                    _checking = false;
                }
            });
        }

        private static void CheckVersion(string currentVersion)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMilliseconds(CheckTimeoutMs);
                client.DefaultRequestHeaders.Add("User-Agent", "AccessibleArena-Mod");
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

                var task = client.GetStringAsync(GitHubApiUrl);
                task.Wait();
                string json = task.Result;

                // Cache for later download
                _releaseJson = json;

                // Extract tag_name
                var match = Regex.Match(json, @"""tag_name""\s*:\s*""v?([^""]+)""");
                if (!match.Success)
                {
                    Log.Msg("UpdateChecker", "Could not parse version from GitHub response");
                    return;
                }

                string remoteVersion = match.Groups[1].Value;
                var latest = NormalizeVersion(remoteVersion);
                var current = NormalizeVersion(currentVersion);

                if (latest > current)
                {
                    _updateAvailable = true;
                    _latestVersion = remoteVersion;
                    Log.Msg("UpdateChecker", $"Update available: v{remoteVersion} (current: v{currentVersion})");
                }
                else
                {
                    Log.Msg("UpdateChecker", $"Up to date (current: v{currentVersion}, latest: v{remoteVersion})");
                }
            }
        }

        /// <summary>
        /// Poll background tasks and announce results. Call every frame from OnUpdate.
        /// </summary>
        public static void Update(IAnnouncementService announcer)
        {
            // One-time announcement when version check completes
            if (_checkComplete && _updateAvailable && !_announced)
            {
                _announced = true;
                announcer.Announce(Strings.UpdateAvailable(_latestVersion), AnnouncementPriority.High);
            }

            // Handle download completion
            if (_downloadTask != null && _downloadTask.IsCompleted)
            {
                if (_downloadTask.IsFaulted || _downloadFailed)
                {
                    Log.Warn("UpdateChecker", $"Download failed: {_downloadTask.Exception?.InnerException?.Message}");
                    announcer.AnnounceInterrupt(Strings.UpdateFailed);
                    _downloadTask = null;
                }
                else if (_downloadComplete && _downloadedPath != null)
                {
                    announcer.AnnounceInterrupt(Strings.UpdateDownloaded);
                    PerformUpdate(_downloadedPath, _downloadedPrismPath);
                    _downloadTask = null;
                }
            }
        }

        /// <summary>
        /// Handle F5 press. Returns true if the key was consumed.
        /// </summary>
        public static bool HandleF5(IAnnouncementService announcer)
        {
            // Already downloading
            if (_downloadTask != null && !_downloadTask.IsCompleted)
            {
                announcer.AnnounceInterrupt(Strings.UpdateDownloading);
                return true;
            }

            if (!_updateAvailable)
            {
                announcer.AnnounceInterrupt(Strings.UpdateNotAvailable(VersionInfo.Value));
                return true;
            }

            // Start download
            announcer.AnnounceInterrupt(Strings.UpdateDownloading);
            StartDownload();
            return true;
        }

        private static void StartDownload()
        {
            _downloadComplete = false;
            _downloadFailed = false;
            _downloadedPath = null;
            _downloadedPrismPath = null;

            _downloadTask = Task.Run(() =>
            {
                try
                {
                    return DownloadAssets();
                }
                catch (Exception ex)
                {
                    Log.Warn("UpdateChecker", $"Download error: {ex.Message}");
                    _downloadFailed = true;
                    return (string)null;
                }
            });
        }

        /// <summary>
        /// Fetches the release's mod DLL, plus its speech runtime when the installed one is not
        /// already the same build. Throws to fail the whole update rather than leaving a mod DLL
        /// on disk that has nothing to speak through.
        /// </summary>
        private static string DownloadAssets()
        {
            string json = FetchReleaseJson();

            string modUrl = FindAssetUrl(json, ModDllAssetName);
            if (modUrl == null)
            {
                throw new Exception($"Asset '{ModDllAssetName}' not found in latest release");
            }

            string tempPath = DownloadAsset(modUrl, ModDllAssetName);
            _downloadedPrismPath = DownloadPrismIfNeeded(json);

            _downloadedPath = tempPath;
            _downloadComplete = true;
            return tempPath;
        }

        /// <summary>
        /// Returns the downloaded speech runtime, or null when the game already carries that exact
        /// build and the copy would be a no-op. Throws when the game has no <c>prism.dll</c> at all
        /// and the release cannot supply one — finishing the update in that state would install a
        /// mod that cannot say a word, including that anything went wrong.
        /// </summary>
        private static string DownloadPrismIfNeeded(string json)
        {
            string installedPrism = Path.Combine(GetGameRoot(), PrismDllAssetName);
            bool haveInstalled = File.Exists(installedPrism);

            string url = FindAssetUrl(json, PrismDllAssetName);
            if (url == null)
            {
                if (!haveInstalled)
                    throw new Exception($"Asset '{PrismDllAssetName}' not in the release and none installed");

                Log.Msg("UpdateChecker", $"Release carries no {PrismDllAssetName}; keeping the installed one");
                return null;
            }

            string downloaded = DownloadAsset(url, PrismDllAssetName);

            if (haveInstalled && FilesAreIdentical(downloaded, installedPrism))
            {
                Log.Msg("UpdateChecker", $"Installed {PrismDllAssetName} already matches the release; no copy needed");
                return null;
            }

            Log.Msg("UpdateChecker", haveInstalled
                ? $"{PrismDllAssetName} differs from the installed one; it will be replaced"
                : $"No {PrismDllAssetName} installed; it will be added");
            return downloaded;
        }

        private static string FetchReleaseJson()
        {
            string json = _releaseJson;
            if (!string.IsNullOrEmpty(json))
                return json;

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMilliseconds(DownloadTimeoutMs);
                client.DefaultRequestHeaders.Add("User-Agent", "AccessibleArena-Mod");
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

                var task = client.GetStringAsync(GitHubApiUrl);
                task.Wait();
                return task.Result;
            }
        }

        /// <summary>Picks an asset's download URL out of the release JSON, or null if absent.</summary>
        private static string FindAssetUrl(string json, string assetName)
        {
            string pattern = $"\"browser_download_url\"\\s*:\\s*\"([^\"]*{Regex.Escape(assetName)}[^\"]*)\"";
            var match = Regex.Match(json, pattern);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string DownloadAsset(string url, string assetName)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), assetName);

            Log.Msg("UpdateChecker", $"Downloading {assetName} from: {url}");

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMilliseconds(DownloadTimeoutMs);
                client.DefaultRequestHeaders.Add("User-Agent", "AccessibleArena-Mod");

                var responseTask = client.GetByteArrayAsync(url);
                responseTask.Wait();
                File.WriteAllBytes(tempPath, responseTask.Result);
            }

            Log.Msg("UpdateChecker", $"Downloaded to: {tempPath}");
            return tempPath;
        }

        /// <summary>Byte comparison; a read failure counts as "different" so the copy still happens.</summary>
        private static bool FilesAreIdentical(string pathA, string pathB)
        {
            try
            {
                var a = new FileInfo(pathA);
                var b = new FileInfo(pathB);
                if (a.Length != b.Length)
                    return false;

                using (var streamA = File.OpenRead(pathA))
                using (var streamB = File.OpenRead(pathB))
                {
                    var bufferA = new byte[64 * 1024];
                    var bufferB = new byte[64 * 1024];
                    int read;

                    while ((read = streamA.Read(bufferA, 0, bufferA.Length)) > 0)
                    {
                        int filled = 0;
                        while (filled < read)
                        {
                            int chunk = streamB.Read(bufferB, filled, read - filled);
                            if (chunk <= 0) return false;
                            filled += chunk;
                        }

                        for (int i = 0; i < read; i++)
                        {
                            if (bufferA[i] != bufferB[i])
                                return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("UpdateChecker", $"Could not compare {pathA} with {pathB}: {ex.Message}");
                return false;
            }
        }

        /// <summary>The MTGA root — the parent of the Mods folder this assembly was loaded from.</summary>
        private static string GetGameRoot()
        {
            string modsDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.GetDirectoryName(modsDir);
        }

        private static void PerformUpdate(string downloadedDllPath, string downloadedPrismPath)
        {
            try
            {
                // Determine paths from the running assembly location
                string modDllPath = Assembly.GetExecutingAssembly().Location;
                string modsDir = Path.GetDirectoryName(modDllPath);
                string gameRoot = Path.GetDirectoryName(modsDir); // Mods\ parent = MTGA root
                string targetPath = Path.Combine(modsDir, ModDllAssetName);

                // Find the launcher executable
                string launcherPath = FindLauncher(gameRoot);

                // Elevated copy batch — minimal, only does the file copies
                string batchPath = Path.Combine(Path.GetTempPath(), "aa_update.bat");
                var batchLines = new List<string>
                {
                    "@echo off",
                    ":wait",
                    "tasklist /fi \"imagename eq MTGA.exe\" 2>nul | find /i \"MTGA.exe\" >nul",
                    "if not errorlevel 1 (",
                    "    timeout /t 2 /nobreak >nul",
                    "    goto wait",
                    ")",
                    $"copy /y \"{downloadedDllPath}\" \"{targetPath}\"",
                    "if errorlevel 1 (",
                    "    echo Update failed. Press any key to close.",
                    "    pause >nul",
                    "    exit /b 1",
                    ")"
                };

                // The speech runtime goes to the game root, where the mod's preload looks for it.
                // It lands in the same batch on purpose: the wait above is the only moment
                // prism.dll is not loaded into the game process and can be replaced at all.
                if (downloadedPrismPath != null)
                {
                    string prismTarget = Path.Combine(gameRoot, PrismDllAssetName);
                    batchLines.Add($"copy /y \"{downloadedPrismPath}\" \"{prismTarget}\"");
                    batchLines.Add("if errorlevel 1 (");
                    batchLines.Add("    echo Speech library update failed. Press any key to close.");
                    batchLines.Add("    pause >nul");
                    batchLines.Add("    exit /b 1");
                    batchLines.Add(")");
                    Log.Msg("UpdateChecker", $"Batch will also install {PrismDllAssetName} to {prismTarget}");
                }

                batchLines.Add($"del \"{batchPath}\"");
                File.WriteAllLines(batchPath, batchLines);

                Log.Msg("UpdateChecker", $"Batch script written to: {batchPath}");

                // Launch elevated batch for the copy
                var copyPsi = new ProcessStartInfo
                {
                    FileName = batchPath,
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(copyPsi);

                // Launch game relaunch as non-elevated (inherits our non-elevated token)
                // Delay 8 seconds to give the copy batch time to finish
                var relaunchPsi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c timeout /t 8 /nobreak >nul & start \"\" \"{launcherPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(relaunchPsi);

                Log.Msg("UpdateChecker", $"Relaunch scheduled, quitting game...");

                // Quit the game so the batch can replace the DLL
                Application.Quit();
            }
            catch (Exception ex)
            {
                Log.Error("UpdateChecker", $"Failed to launch update: {ex.Message}");
            }
        }

        private static string FindLauncher(string gameRoot)
        {
            // Try MTGALauncher subfolder first (WotC install)
            string launcherDir = Path.Combine(gameRoot, "MTGALauncher");
            string launcherExe = Path.Combine(launcherDir, "MTGALauncher.exe");
            if (File.Exists(launcherExe))
                return launcherExe;

            // Fall back to MTGA.exe in root
            string mtgaExe = Path.Combine(gameRoot, "MTGA.exe");
            if (File.Exists(mtgaExe))
                return mtgaExe;

            // Last resort
            return mtgaExe;
        }

        /// <summary>
        /// Normalize a version string to a comparable Version object.
        /// Same logic as installer's NormalizeVersion.
        /// </summary>
        private static Version NormalizeVersion(string version)
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

            string[] parts = version.Trim().Split('.');
            int major = parts.Length > 0 && int.TryParse(parts[0], out int m) ? m : 0;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out int n) ? n : 0;
            int build = parts.Length > 2 && int.TryParse(parts[2], out int b) ? b : 0;
            int revision = parts.Length > 3 && int.TryParse(parts[3], out int r) ? r : 0;

            return new Version(major, minor, build, revision);
        }
    }
}
