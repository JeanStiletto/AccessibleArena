using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using MelonLoader;
using AccessibleArena.Core.Services;

namespace AccessibleArena.Patches
{
    /// <summary>
    /// Harmony patch for MTGA.KeyboardManager.KeyboardManager to block keys
    /// from reaching the game in specific contexts.
    ///
    /// EXPERIMENTAL (January 2026):
    /// Key blocking strategy:
    /// 1. In DuelScene: Block Enter entirely - our mod handles all Enter presses
    ///    This prevents "Pass until response" from triggering unexpectedly
    /// 2. Other scenes: Use per-key consumption via InputManager.ConsumeKey()
    ///
    /// KNOWN ISSUE: Blocking Enter in DuelScene also blocks it during mulligan/opening hand.
    /// The BrowserNavigator needs to handle Space to confirm keep, and find mulligan cards.
    /// This needs more testing when mulligan screen is accessible again.
    /// </summary>
    [HarmonyPatch]
    public static class KeyboardManagerPatch
    {
        // Cache scene name to avoid repeated string allocations
        private static string _cachedSceneName = "";
        private static int _lastSceneCheck = -1;

        /// <summary>
        /// Check if we're currently in DuelScene (cached per frame).
        /// </summary>
        private static bool IsInDuelScene()
        {
            int currentFrame = Time.frameCount;
            if (currentFrame != _lastSceneCheck)
            {
                _cachedSceneName = SceneManager.GetActiveScene().name;
                _lastSceneCheck = currentFrame;
            }
            return _cachedSceneName == "DuelScene";
        }

        /// <summary>
        /// Check if we're in a menu scene (not DuelScene, not loading screens).
        /// </summary>
        private static bool IsInMenuScene()
        {
            int currentFrame = Time.frameCount;
            if (currentFrame != _lastSceneCheck)
            {
                _cachedSceneName = SceneManager.GetActiveScene().name;
                _lastSceneCheck = currentFrame;
            }
            // Menu scenes: MainNavigation, NavBar, or any scene that's not Duel/Draft/Sealed/Bootstrap/AssetPrep
            return _cachedSceneName != "DuelScene" &&
                   _cachedSceneName != "DraftScene" &&
                   _cachedSceneName != "SealedScene" &&
                   _cachedSceneName != "Bootstrap" &&
                   _cachedSceneName != "AssetPrep";
        }

        /// <summary>
        /// Check if this key should be blocked from the game.
        /// </summary>
        private static bool ShouldBlockKey(KeyCode key)
        {
            // When any input field is focused (regardless of how user got there),
            // block Escape from game so we can use it to exit the input field
            // without the game closing the menu/panel
            if (UIFocusTracker.IsAnyInputFieldFocused() || UIFocusTracker.IsEditingInputField())
            {
                if (key == KeyCode.Escape)
                {
                    return true; // Block Escape so game doesn't close menu
                }
                return false; // Let typing keys through
            }

            // In DuelScene, block Enter entirely - our mod handles all Enter presses
            // This prevents "Pass until response" from triggering when we press Enter
            // for card playing, target selection, player info zone, etc.
            if (IsInDuelScene())
            {
                if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
                {
                    return true;
                }
            }

            // In menu scenes, block Tab entirely - our mod handles Tab for navigation
            // This prevents the game from toggling the Friends panel when we Tab navigate
            if (IsInMenuScene())
            {
                if (key == KeyCode.Tab)
                {
                    return true;
                }
            }

            // For other keys/scenes, check if specifically consumed this frame
            return InputManager.IsKeyConsumed(key);
        }

        /// <summary>
        /// Target the PublishKeyDown method on KeyboardManager.
        /// This is called when a key is pressed and needs to notify subscribers.
        /// </summary>
        [HarmonyPatch("MTGA.KeyboardManager.KeyboardManager", "PublishKeyDown")]
        [HarmonyPrefix]
        public static bool PublishKeyDown_Prefix(KeyCode key)
        {
            if (ShouldBlockKey(key))
            {
                // Only log occasionally to avoid spam
                if (Time.frameCount % 60 == 0 || key != KeyCode.Return)
                {
                    MelonLogger.Msg($"[KeyboardManagerPatch] Blocked {key} from game (scene: {_cachedSceneName})");
                }
                return false; // Skip the original method - don't publish to game
            }
            return true; // Let the original method run
        }

        /// <summary>
        /// Also patch PublishKeyUp to be consistent.
        /// </summary>
        [HarmonyPatch("MTGA.KeyboardManager.KeyboardManager", "PublishKeyUp")]
        [HarmonyPrefix]
        public static bool PublishKeyUp_Prefix(KeyCode key)
        {
            // Block key up events for blocked keys too
            if (ShouldBlockKey(key))
            {
                return false;
            }
            return true;
        }
    }
}
