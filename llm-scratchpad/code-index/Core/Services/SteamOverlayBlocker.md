# SteamOverlayBlocker.cs
Path: src/Core/Services/SteamOverlayBlocker.cs
Lines: 157

## Top-level comments
- Detects if the game is running under Steam and warns the user to disable the Steam overlay to prevent Shift+Tab conflicts with mod navigation. The installer can set OverlayAppEnable=0 in localconfig.vdf; this class checks whether that was done. Low-level keyboard hook approach was abandoned due to NVDA latency issues.

## public static class SteamOverlayBlocker (line 24)
### Fields
- private const string MtgaAppId = "2141910" (line 26)

### Properties
- public static bool IsSteam { get; private set; } (line 28)
- public static bool OverlayDisabled { get; private set; } (line 29)

### Methods
- public static void Install() (line 34) — Note: checks whether Steam is loaded and whether overlay is disabled in VDF; logs a warning if overlay is enabled.
- public static void Uninstall() (line 57) — Note: no-op kept for API compatibility.
- private static bool IsSteamLoaded() (line 59)
- private static bool IsOverlayDisabledInVdf() (line 86)
- private static string GetSteamRoot() (line 126)
