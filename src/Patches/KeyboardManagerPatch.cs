using HarmonyLib;
using UnityEngine;
using MelonLoader;
using MTGAAccessibility.Core.Services;

namespace MTGAAccessibility.Patches
{
    /// <summary>
    /// Harmony patch for MTGA.KeyboardManager.KeyboardManager to block keys
    /// that have been consumed by the accessibility mod.
    ///
    /// When the mod handles a key (e.g., Enter in player info zone), it calls
    /// InputManager.ConsumeKey(). This patch checks IsKeyConsumed() and skips
    /// publishing the key event to the game if it was consumed.
    /// </summary>
    [HarmonyPatch]
    public static class KeyboardManagerPatch
    {
        /// <summary>
        /// Target the PublishKeyDown method on KeyboardManager.
        /// This is called when a key is pressed and needs to notify subscribers.
        /// </summary>
        [HarmonyPatch("MTGA.KeyboardManager.KeyboardManager", "PublishKeyDown")]
        [HarmonyPrefix]
        public static bool PublishKeyDown_Prefix(KeyCode key)
        {
            // Check if this key was consumed by our mod
            if (InputManager.IsKeyConsumed(key))
            {
                MelonLogger.Msg($"[KeyboardManagerPatch] Blocked key from game: {key}");
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
            // Block key up events for consumed keys too
            if (InputManager.IsKeyConsumed(key))
            {
                return false;
            }
            return true;
        }
    }
}
