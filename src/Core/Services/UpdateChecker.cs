using System;
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
                    MelonLogger.Warning($"[UpdateChecker] Version check failed: {ex.Message}");
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
                    MelonLogger.Msg("[UpdateChecker] Could not parse version from GitHub response");
                    return;
                }

                string remoteVersion = match.Groups[1].Value;
                var latest = NormalizeVersion(remoteVersion);
                var current = NormalizeVersion(currentVersion);

                if (latest > current)
                {
                    _updateAvailable = true;
                    _latestVersion = remoteVersion;
                    MelonLogger.Msg($"[UpdateChecker] Update available: v{remoteVersion} (current: v{currentVersion})");
                }
                else
                {
                    MelonLogger.Msg($"[UpdateChecker] Up to date (current: v{currentVersion}, latest: v{remoteVersion})");
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
                    MelonLogger.Warning($"[UpdateChecker] Download failed: {_downloadTask.Exception?.InnerException?.Message}");
                    announcer.AnnounceInterrupt(Strings.UpdateFailed);
                    _downloadTask = null;
                }
                else if (_downloadComplete && _downloadedPath != null)
                {
                    announcer.AnnounceInterrupt(Strings.UpdateDownloaded);
                    PerformUpdate(_downloadedPath);
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

            _downloadTask = Task.Run(() =>
            {
                try
                {
                    return DownloadDll();
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[UpdateChecker] Download error: {ex.Message}");
                    _downloadFailed = true;
                    return (string)null;
                }
            });
        }

        private static string DownloadDll()
        {
            // If we don't have cached release JSON, fetch it again
            string json = _releaseJson;
            if (string.IsNullOrEmpty(json))
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMilliseconds(DownloadTimeoutMs);
                    client.DefaultRequestHeaders.Add("User-Agent", "AccessibleArena-Mod");
                    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

                    var task = client.GetStringAsync(GitHubApiUrl);
                    task.Wait();
                    json = task.Result;
                }
            }

            // Find the DLL asset download URL
            string pattern = $"\"browser_download_url\"\\s*:\\s*\"([^\"]*{Regex.Escape(ModDllAssetName)}[^\"]*)\"";
            var match = Regex.Match(json, pattern);
            if (!match.Success)
            {
                throw new Exception($"Asset '{ModDllAssetName}' not found in latest release");
            }

            string downloadUrl = match.Groups[1].Value;
            string tempPath = Path.Combine(Path.GetTempPath(), ModDllAssetName);

            MelonLogger.Msg($"[UpdateChecker] Downloading from: {downloadUrl}");

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMilliseconds(DownloadTimeoutMs);
                client.DefaultRequestHeaders.Add("User-Agent", "AccessibleArena-Mod");

                var responseTask = client.GetByteArrayAsync(downloadUrl);
                responseTask.Wait();
                File.WriteAllBytes(tempPath, responseTask.Result);
            }

            MelonLogger.Msg($"[UpdateChecker] Downloaded to: {tempPath}");
            _downloadedPath = tempPath;
            _downloadComplete = true;
            return tempPath;
        }

        private static void PerformUpdate(string downloadedDllPath)
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

                // Elevated copy batch — minimal, only does the file copy
                string batchPath = Path.Combine(Path.GetTempPath(), "aa_update.bat");
                var batchLines = new[]
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
                    ")",
                    $"del \"{batchPath}\""
                };
                File.WriteAllLines(batchPath, batchLines);

                MelonLogger.Msg($"[UpdateChecker] Batch script written to: {batchPath}");

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

                MelonLogger.Msg($"[UpdateChecker] Relaunch scheduled, quitting game...");

                // Quit the game so the batch can replace the DLL
                Application.Quit();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[UpdateChecker] Failed to launch update: {ex.Message}");
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

            var result = new Version(major, minor, build, revision);

            // 1.0.0.0 is the .NET default — treat as 0.0.0.0
            if (result == new Version(1, 0, 0, 0))
                return new Version(0, 0, 0, 0);

            return result;
        }
    }
}
