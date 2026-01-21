using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Tracks active menu panels and provides content controller detection.
    /// Note: Popup detection has been moved to UnifiedPanelDetector which uses
    /// alpha-based visibility tracking instead of cooldowns/timers.
    /// </summary>
    public class MenuPanelTracker
    {
        #region Configuration

        // Base menu controller types for panel state detection
        private static readonly string[] MenuControllerTypes = new[]
        {
            "NavContentController",
            "SettingsMenu",
            "SettingsMenuHost",
            "PopupBase"
        };

        // Login scene panel name patterns (these are simple prefabs without controllers)
        private static readonly string[] LoginPanelPatterns = new[]
        {
            "Panel - WelcomeGate",
            "Panel - Log In",
            "Panel - Register",
            "Panel - ForgotCredentials",
            "Panel - AgeGate",
            "Panel - Language",
            "Panel - Consent",
            "Panel - EULA",
            "Panel - Marketing",
            "Panel - Terms",
            "Panel - Privacy",
            "Panel - UpdatePolicies"
        };

        #endregion

        #region State

        private readonly HashSet<string> _activePanels = new HashSet<string>();
        private GameObject _foregroundPanel;
        private readonly string _logPrefix;

        #endregion

        #region Public Properties

        /// <summary>
        /// The current foreground panel that should filter navigation elements.
        /// </summary>
        public GameObject ForegroundPanel
        {
            get => _foregroundPanel;
            set => _foregroundPanel = value;
        }

        /// <summary>
        /// Set of currently active panel identifiers.
        /// </summary>
        public HashSet<string> ActivePanels => _activePanels;

        #endregion

        #region Constructor

        /// <summary>
        /// Create a new MenuPanelTracker.
        /// </summary>
        /// <param name="announcer">Announcement service (kept for API compatibility).</param>
        /// <param name="logPrefix">Prefix for log messages.</param>
        public MenuPanelTracker(IAnnouncementService announcer, string logPrefix = "PanelTracker")
        {
            _logPrefix = logPrefix;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Clear all tracked state. Call on scene change or deactivation.
        /// </summary>
        public void Reset()
        {
            _activePanels.Clear();
            _foregroundPanel = null;
        }

        /// <summary>
        /// Get currently active panels by checking game's internal menu controllers.
        /// Uses two-pass approach: first find all open controllers, then apply priority.
        /// Also detects Login scene panels which don't have controllers.
        /// </summary>
        /// <param name="screenDetector">Screen detector for Settings menu state.</param>
        public List<(string name, GameObject obj)> GetActivePanelsWithObjects(MenuScreenDetector screenDetector)
        {
            var activePanels = new List<(string name, GameObject obj)>();
            var openControllers = new List<(MonoBehaviour mb, string typeName)>();

            // PASS 1: Find all open menu controllers
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                var type = mb.GetType();
                string typeName = type.Name;

                // Check if this is a menu controller type or inherits from one
                bool isMenuController = false;
                var checkType = type;
                while (checkType != null)
                {
                    if (MenuControllerTypes.Contains(checkType.Name))
                    {
                        isMenuController = true;
                        break;
                    }
                    checkType = checkType.BaseType;
                }

                if (!isMenuController) continue;

                // Check IsOpen state
                bool isOpen = CheckIsOpen(mb, type);
                if (isOpen)
                {
                    openControllers.Add((mb, typeName));
                }
            }

            // Check if Settings menu is open
            bool settingsMenuOpen = screenDetector?.CheckSettingsMenuOpen() ?? false;

            // PASS 2: Build panel list with priority filtering
            foreach (var (mb, typeName) in openControllers)
            {
                // When SettingsMenu is open, skip other controllers (HomePage, etc.)
                // Settings overlays them and should have exclusive focus
                if (settingsMenuOpen && typeName != "SettingsMenu" && typeName != "PopupBase")
                {
                    continue;
                }

                string panelId = $"{typeName}:{mb.gameObject.name}";
                if (!activePanels.Any(p => p.name == panelId))
                {
                    // For SettingsMenu, use the content panel where buttons are
                    GameObject panelObj = (typeName == "SettingsMenu" && screenDetector?.SettingsContentPanel != null)
                        ? screenDetector.SettingsContentPanel
                        : mb.gameObject;
                    activePanels.Add((panelId, panelObj));
                }
            }

            // PASS 3: Detect Login scene panels (simple prefabs without controllers)
            DetectLoginPanels(activePanels);

            return activePanels;
        }

        /// <summary>
        /// Detect Login scene panels by GameObject name patterns.
        /// </summary>
        private void DetectLoginPanels(List<(string name, GameObject obj)> activePanels)
        {
            var panelParent = GameObject.Find("Canvas - Camera/PanelParent");
            if (panelParent == null) return;

            foreach (Transform child in panelParent.transform)
            {
                if (child == null || !child.gameObject.activeInHierarchy) continue;

                string childName = child.name;

                foreach (var pattern in LoginPanelPatterns)
                {
                    if (childName.StartsWith(pattern))
                    {
                        string panelId = $"LoginPanel:{childName}";
                        if (!activePanels.Any(p => p.name == panelId))
                        {
                            activePanels.Add((panelId, child.gameObject));
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Check if a MonoBehaviour has IsOpen = true AND is ready (animation complete).
        /// </summary>
        public bool CheckIsOpen(MonoBehaviour mb, System.Type type)
        {
            bool isOpen = false;
            string typeName = type.Name;

            // Try IsOpen property
            var isOpenProp = type.GetProperty("IsOpen",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (isOpenProp != null && isOpenProp.PropertyType == typeof(bool))
            {
                try
                {
                    isOpen = (bool)isOpenProp.GetValue(mb);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Warning($"[{_logPrefix}] Failed to read IsOpen property on {typeName}: {ex.Message}");
                }
            }

            // Try IsOpen() method if property didn't work
            if (!isOpen)
            {
                var isOpenMethod = type.GetMethod("IsOpen",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance,
                    null, new System.Type[0], null);
                if (isOpenMethod != null && isOpenMethod.ReturnType == typeof(bool))
                {
                    try
                    {
                        isOpen = (bool)isOpenMethod.Invoke(mb, null);
                    }
                    catch (System.Exception ex)
                    {
                        MelonLogger.Warning($"[{_logPrefix}] Failed to invoke IsOpen() method on {typeName}: {ex.Message}");
                    }
                }
            }

            if (!isOpen) return false;

            // Check if animation is complete (IsReadyToShow for NavContentController)
            var isReadyProp = type.GetProperty("IsReadyToShow",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (isReadyProp != null && isReadyProp.PropertyType == typeof(bool))
            {
                try
                {
                    bool isReady = (bool)isReadyProp.GetValue(mb);
                    if (!isReady)
                    {
                        return false;
                    }
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Warning($"[{_logPrefix}] Failed to read IsReadyToShow on {typeName}: {ex.Message}");
                }
            }

            // Check IsMainPanelActive for SettingsMenu
            var isMainPanelActiveProp = type.GetProperty("IsMainPanelActive",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (isMainPanelActiveProp != null && isMainPanelActiveProp.PropertyType == typeof(bool))
            {
                try
                {
                    bool isMainActive = (bool)isMainPanelActiveProp.GetValue(mb);
                    if (!isMainActive)
                    {
                        return false;
                    }
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Warning($"[{_logPrefix}] Failed to read IsMainPanelActive on {typeName}: {ex.Message}");
                }
            }

            return true;
        }

        /// <summary>
        /// Add a panel to the active panels set.
        /// </summary>
        public void AddActivePanel(string panelId)
        {
            _activePanels.Add(panelId);
        }

        /// <summary>
        /// Remove a panel from the active panels set.
        /// </summary>
        public void RemoveActivePanel(string panelId)
        {
            _activePanels.Remove(panelId);
        }

        /// <summary>
        /// Remove all panels matching a predicate.
        /// </summary>
        public void RemovePanelsWhere(System.Predicate<string> predicate)
        {
            _activePanels.RemoveWhere(predicate);
        }

        /// <summary>
        /// Check if a panel is in the active panels set.
        /// </summary>
        public bool ContainsPanel(string panelId)
        {
            return _activePanels.Contains(panelId);
        }

        #endregion

        #region Static Utility Methods

        /// <summary>
        /// Check if a panel name represents an overlay that should filter elements.
        /// </summary>
        public static bool IsOverlayPanel(string panelName)
        {
            return panelName.StartsWith("SettingsMenu:") ||
                   panelName.StartsWith("PopupBase:") ||
                   panelName.StartsWith("LoginPanel:") ||
                   panelName.Contains("SystemMessageView");
        }

        /// <summary>
        /// Clean up a popup name for announcement.
        /// </summary>
        public static string CleanPopupName(string popupName)
        {
            if (string.IsNullOrEmpty(popupName)) return "Popup";

            if (popupName.Contains("SystemMessageView"))
                return "Confirmation";

            string clean = popupName
                .Replace("(Clone)", "")
                .Replace("Popup", "")
                .Replace("_Desktop_16x9", "")
                .Replace("_", " ")
                .Trim();

            clean = Regex.Replace(clean, "([a-z])([A-Z])", "$1 $2");

            if (string.IsNullOrWhiteSpace(clean))
                return "Popup";

            return clean;
        }

        /// <summary>
        /// Check if a GameObject is a child of (or the same as) a parent GameObject.
        /// </summary>
        public static bool IsChildOf(GameObject child, GameObject parent)
        {
            if (child == null || parent == null)
                return false;

            Transform current = child.transform;
            Transform parentTransform = parent.transform;

            while (current != null)
            {
                if (current == parentTransform)
                    return true;
                current = current.parent;
            }

            return false;
        }

        #endregion
    }
}
