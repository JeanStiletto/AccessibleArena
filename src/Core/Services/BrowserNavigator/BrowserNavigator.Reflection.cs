using UnityEngine;
using MelonLoader;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    public partial class BrowserNavigator
    {
        /// <summary>
        /// Walks GameManager → BrowserManager → CurrentBrowser via reflection.
        /// Logs the specific step that failed under "[BrowserNavigator] {logPrefix}: ...".
        /// Callers are expected to filter by the concrete browser type name themselves
        /// (e.g. "AssignDamage") once this helper returns true.
        /// </summary>
        private static bool TryGetCurrentBrowser(string logPrefix, out object currentBrowser)
        {
            currentBrowser = null;

            MonoBehaviour gameManager = null;
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "GameManager")
                {
                    gameManager = mb;
                    break;
                }
            }

            if (gameManager == null)
            {
                MelonLogger.Msg($"[BrowserNavigator] {logPrefix}: GameManager not found");
                return false;
            }

            var bmProp = gameManager.GetType().GetProperty("BrowserManager", AllInstanceFlags);
            var browserManager = bmProp?.GetValue(gameManager);
            if (browserManager == null)
            {
                MelonLogger.Msg($"[BrowserNavigator] {logPrefix}: BrowserManager not found");
                return false;
            }

            var cbProp = browserManager.GetType().GetProperty("CurrentBrowser", AllInstanceFlags);
            currentBrowser = cbProp?.GetValue(browserManager);
            return currentBrowser != null;
        }
    }
}
