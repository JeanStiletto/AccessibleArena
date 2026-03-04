# KeyboardManagerPatch.cs Code Index

## File Overview
Harmony patch for MTGA.KeyboardManager.KeyboardManager to block keys from reaching the game in specific contexts.

Key blocking strategy:
1. In DuelScene: Block Enter entirely - mod handles all Enter presses
2. Other scenes: Use per-key consumption via InputManager.ConsumeKey()

## Static Class: KeyboardManagerPatch (line 24)

### Private Fields
- private static string _cachedSceneName (line 27)
  // Cache scene name to avoid repeated string allocations

- private static int _lastSceneCheck (line 28)
  // Frame number of last scene check

### Public Properties
- public static bool BlockEscape { get; set; } (line 34)
  // When true, Escape is blocked from reaching the game
  // Set by WebBrowserAccessibility to prevent settings menu from opening

### Private Methods
- private static bool IsInDuelScene() (line 39)
  // Check if we're currently in DuelScene (cached per frame)

- private static bool IsInMenuScene() (line 53)
  // Check if we're in a menu scene (not DuelScene, not loading screens)

- private static bool ShouldBlockKey(KeyCode key) (line 72)
  // Check if this key should be blocked from the game
  // Handles: Escape (when input field/dropdown/WebBrowser/mod menu active)
  //          Enter (in dropdown mode or DuelScene)
  //          Ctrl (in DuelScene - prevents accidental full control toggle)
  //          Tab (in menu scenes - mod handles navigation)
  //          Any key consumed via InputManager.ConsumeKey()

### Harmony Patch Methods

#### PublishKeyDown Patch
- [HarmonyPatch("MTGA.KeyboardManager.KeyboardManager", "PublishKeyDown")] (line 148)
- [HarmonyPrefix]
- public static bool PublishKeyDown_Prefix(KeyCode key) (line 150)
  // Blocks key from being published to game if ShouldBlockKey returns true

#### PublishKeyUp Patch
- [HarmonyPatch("MTGA.KeyboardManager.KeyboardManager", "PublishKeyUp")] (line 167)
- [HarmonyPrefix]
- public static bool PublishKeyUp_Prefix(KeyCode key) (line 169)
  // Blocks Enter KeyUp when popup was just opened by mod on KeyDown
  // Also blocks key up events for blocked keys
