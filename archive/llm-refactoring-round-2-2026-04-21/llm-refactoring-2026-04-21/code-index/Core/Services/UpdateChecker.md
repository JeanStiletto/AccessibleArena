# UpdateChecker.cs
Path: src/Core/Services/UpdateChecker.cs
Lines: 354

## Top-level comments
- Handles checking for mod updates on GitHub and performing auto-update via batch script. Version check runs in background on startup; F5 triggers download + replace + relaunch.

## public static class UpdateChecker (line 19)
### Fields
- private const string GitHubApiUrl = "https://api.github.com/repos/JeanStiletto/AccessibleArena/releases/latest" (line 21)
- private const string ModDllAssetName = "AccessibleArena.dll" (line 22)
- private const int CheckTimeoutMs = 5000 (line 23)
- private const int DownloadTimeoutMs = 30000 (line 24)
- private static volatile bool _updateAvailable (line 27)
- private static volatile string _latestVersion (line 28)
- private static volatile bool _checkComplete (line 29)
- private static volatile bool _announced (line 30)
- private static volatile bool _checking (line 31)
- private static Task<string> _downloadTask (line 34)
- private static volatile bool _downloadComplete (line 35)
- private static volatile bool _downloadFailed (line 36)
- private static volatile string _downloadedPath (line 37)
- private static volatile string _releaseJson (line 40)

### Properties
- public static bool IsUpdateAvailable (line 42)
- public static string LatestVersion (line 43)

### Methods
- public static void CheckInBackground(string currentVersion) (line 49)
- private static void CheckVersion(string currentVersion) (line 72)
- public static void Update(IAnnouncementService announcer) (line 115) — Note: poll loop; announces when version check completes, drives download completion handling (announce + PerformUpdate).
- public static bool HandleF5(IAnnouncementService announcer) (line 145)
- private static void StartDownload() (line 166)
- private static string DownloadDll() (line 187)
- private static void PerformUpdate(string downloadedDllPath) (line 234) — Note: writes batch file, launches elevated copy process, schedules non-elevated relaunch, then calls Application.Quit().
- private static string FindLauncher(string gameRoot) (line 302)
- private static Version NormalizeVersion(string version) (line 323) — Note: treats .NET default 1.0.0.0 as 0.0.0.0 so unversioned builds never compare as "newer".
