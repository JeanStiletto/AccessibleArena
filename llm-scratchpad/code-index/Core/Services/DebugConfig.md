# DebugConfig.cs

Centralized debug configuration for the entire mod. Toggle DebugEnabled to control all debug output.

```
public static class DebugConfig (line 9)
  public static bool DebugEnabled { get; set; } (line 14)
  public static bool LogNavigation { get; set; } (line 17)
  public static bool LogPanelDetection { get; set; } (line 18)
  public static bool LogFocusTracking { get; set; } (line 19)
  public static bool LogCardInfo { get; set; } (line 20)
  public static bool LogActivation { get; set; } (line 21)
  public static bool LogAnnouncements { get; set; } (line 22)
  public static bool LogPatches { get; set; } (line 23)
  public static bool LogPanelOverlapDiagnostic { get; set; } (line 27)
  public static void Log(string tag, string message) (line 34)
  public static void LogIf(bool categoryEnabled, string tag, string message) (line 46)
  public static void EnableAll() (line 55)
  public static void DisableAll() (line 70)
```
