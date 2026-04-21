# DebugConfig.cs
Path: src/Core/Services/DebugConfig.cs
Lines: 138

## Top-level comments
- Centralized debug configuration for the entire mod. Exposes a master DebugEnabled toggle plus per-category flags, and maintains a 20-entry ring buffer of recent log lines for screen-reader playback via Shift+F12.

## public static class DebugConfig (line 10)
### Fields
- private const int MaxRecentEntries = 20 (line 31)
- private static readonly string[] _recentEntries = new string[MaxRecentEntries] (line 32)
- private static int _recentWriteIndex (line 33)
- private static int _recentCount (line 34)
### Properties
- public static bool DebugEnabled { get; set; } = true (line 15)
- public static bool LogNavigation { get; set; } = true (line 18)
- public static bool LogPanelDetection { get; set; } = true (line 19)
- public static bool LogFocusTracking { get; set; } = true (line 20)
- public static bool LogCardInfo { get; set; } = true (line 21)
- public static bool LogActivation { get; set; } = true (line 22)
- public static bool LogAnnouncements { get; set; } = true (line 23)
- public static bool LogPatches { get; set; } = true (line 24)
- public static bool LogPanelOverlapDiagnostic { get; set; } = true (line 28)
### Methods
- public static void Log(string tag, string message) (line 41)
- public static void LogIf(bool categoryEnabled, string tag, string message) (line 57)
- private static void AddRecentEntry(string entry) (line 67)
- public static string[] GetRecentEntries(int count = 5) (line 78) — Note: returns oldest-first within the requested window
- public static void EnableAll() (line 98)
- public static void DisableAll() (line 113) — Note: only clears DebugEnabled, leaves category flags alone
- internal static void Reset() (line 121) — Note: test-only helper; clears ring buffer too
