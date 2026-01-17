using UnityEngine;
using MelonLoader;
using MTGAAccessibility.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// Tracks panel state changes, popup appearances, and overlay management
    /// for the MTGA menu navigation system.
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

        #endregion

        #region State

        private readonly HashSet<string> _activePanels = new HashSet<string>();
        private readonly HashSet<int> _knownPopupIds = new HashSet<int>();
        private GameObject _foregroundPanel;
        private readonly IAnnouncementService _announcer;
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
        /// <param name="announcer">Announcement service for popup notifications.</param>
        /// <param name="logPrefix">Prefix for log messages (e.g., navigator ID).</param>
        public MenuPanelTracker(IAnnouncementService announcer, string logPrefix = "PanelTracker")
        {
            _announcer = announcer;
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
            _knownPopupIds.Clear();
            _foregroundPanel = null;
        }

        /// <summary>
        /// Check if any new popup GameObjects have appeared.
        /// Announces the popup name when a new popup is detected.
        /// </summary>
        /// <returns>True if a new popup was found and rescan should be triggered.</returns>
        public bool CheckForNewPopups()
        {
            bool foundNewPopup = false;
            string newPopupName = null;

            // Find all active GameObjects with "Popup" in their name
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;
                if (!go.name.Contains("Popup")) continue;

                int id = go.GetInstanceID();
                if (!_knownPopupIds.Contains(id))
                {
                    _knownPopupIds.Add(id);
                    MelonLogger.Msg($"[{_logPrefix}] New popup detected: {go.name}");
                    foundNewPopup = true;
                    newPopupName = go.name;
                }
            }

            // Announce the popup name if a new one was found
            if (foundNewPopup && !string.IsNullOrEmpty(newPopupName))
            {
                string cleanName = CleanPopupName(newPopupName);
                _announcer?.AnnounceInterrupt($"{cleanName} opened.");
            }

            // Clean up IDs of popups that no longer exist
            _knownPopupIds.RemoveWhere(id =>
            {
                var go = FindObjectFromInstanceID(id);
                return go == null || !go.activeInHierarchy;
            });

            return foundNewPopup;
        }

        /// <summary>
        /// Get currently active panels by checking game's internal menu controllers.
        /// Uses two-pass approach: first find all open controllers, then apply priority.
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

            return activePanels;
        }

        /// <summary>
        /// Check if a MonoBehaviour has IsOpen = true AND is ready (animation complete).
        /// Uses reflection to check various properties/methods on game controller types.
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
                        return false; // Not ready yet, animation still playing
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
                        return false; // Main panel not active (might be in sub-menu or closing)
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
        /// Check if a panel name represents an overlay that should filter elements.
        /// Only Settings and Popups are overlays. NavContentController (HomePage, etc.) are not.
        /// </summary>
        public static bool IsOverlayPanel(string panelName)
        {
            return panelName.StartsWith("SettingsMenu:") || panelName.StartsWith("PopupBase:");
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
        /// Clean up a popup name for announcement.
        /// E.g., "InviteFriendPopup(Clone)" -> "Invite Friend"
        /// </summary>
        public static string CleanPopupName(string popupName)
        {
            if (string.IsNullOrEmpty(popupName)) return "Popup";

            // Remove common suffixes
            string clean = popupName
                .Replace("(Clone)", "")
                .Replace("Popup", "")
                .Replace("_Desktop_16x9", "")
                .Replace("_", " ")
                .Trim();

            // Add spaces before capital letters (InviteFriend -> Invite Friend)
            clean = Regex.Replace(clean, "([a-z])([A-Z])", "$1 $2");

            // Handle empty result
            if (string.IsNullOrWhiteSpace(clean))
                return "Popup";

            return clean;
        }

        /// <summary>
        /// Find a GameObject by its instance ID.
        /// </summary>
        public static GameObject FindObjectFromInstanceID(int instanceId)
        {
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go.GetInstanceID() == instanceId)
                    return go;
            }
            return null;
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
