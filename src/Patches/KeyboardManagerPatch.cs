using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using MelonLoader;
using MTGAAccessibility.Core.Services;

namespace MTGAAccessibility.Patches
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
        /// Check if this key should be blocked from the game.
        /// </summary>
        private static bool ShouldBlockKey(KeyCode key)
        {
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
