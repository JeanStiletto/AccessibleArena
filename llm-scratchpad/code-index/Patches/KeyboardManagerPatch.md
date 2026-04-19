# KeyboardManagerPatch.cs
Path: src/Patches/KeyboardManagerPatch.cs
Lines: 225

## Top-level comments
- Harmony patch for MTGA.KeyboardManager.KeyboardManager that blocks keys from reaching the game in specific contexts (DuelScene Enter blocking, per-key consumption via InputManager, known mulligan-screen limitation).

## public static class KeyboardManagerPatch (line 26)
### Fields
- private static string _cachedSceneName (line 29)
- private static int _lastSceneCheck (line 30)
### Properties
- public static bool BlockEscape { get; set; } (line 36)
### Methods
- private static bool IsInDuelScene() (line 41) — Note: caches scene name per frame
- private static bool IsInMenuScene() (line 55) — Note: caches scene name per frame
- private static bool ShouldBlockKey(KeyCode key) (line 74) — Note: centralised policy mixing scene, input-field focus, dropdown, popup and mod-menu state
- public static bool PublishKeyDown_Prefix(KeyCode key) (line 184) — Note: Harmony prefix; logs blocked keys occasionally to avoid spam
- public static bool PublishKeyUp_Prefix(KeyCode key) (line 203) — Note: Harmony prefix; specially blocks Enter KeyUp in popup mode and clears BlockNextEnterKeyUp flag
