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

        // Cooldown for dismissed popups to prevent re-detection during close animation
        private float _popupDismissCooldown;
        private const float PopupDismissCooldownDuration = 1.0f; // 1 second cooldown

        // Remember the panel that was active before popup opened
        private GameObject _panelBeforePopup;

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
        /// True if popup dismiss cooldown is active (skip panel detection during this time).
        /// </summary>
        public bool IsPopupCooldownActive => _popupDismissCooldown > 0;

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
            _popupDismissCooldown = 0;
        }

        /// <summary>
        /// Start cooldown to prevent re-detection of a popup during close animation.
        /// Call this when a popup button (OK/Cancel) is activated.
        /// Restores the foreground to the panel that was active before the popup opened.
        /// </summary>
        public void StartPopupDismissCooldown()
        {
            _popupDismissCooldown = PopupDismissCooldownDuration;
            // Restore foreground to the panel that was active before popup
            if (_panelBeforePopup != null && _panelBeforePopup.activeInHierarchy)
            {
                MelonLogger.Msg($"[{_logPrefix}] Restoring foreground to: {_panelBeforePopup.name}");
                _foregroundPanel = _panelBeforePopup;
            }
            else
            {
                MelonLogger.Msg($"[{_logPrefix}] No panel to restore, clearing foreground");
                _foregroundPanel = null;
            }
            _panelBeforePopup = null; // Clear saved panel
            _activePanels.RemoveWhere(p => p.StartsWith("Popup:"));
            MelonLogger.Msg($"[{_logPrefix}] Popup dismiss cooldown started ({PopupDismissCooldownDuration}s)");
        }

        /// <summary>
        /// Check for active popups and manage foreground panel.
        /// Detects new popups, tracks reopened popups, and clears when closed.
        /// </summary>
        /// <returns>True if popup state changed and rescan should be triggered.</returns>
        public bool CheckForNewPopups()
        {
            // Update cooldown timer
            if (_popupDismissCooldown > 0)
            {
                _popupDismissCooldown -= Time.deltaTime;
                if (_popupDismissCooldown > 0)
                {
                    // During cooldown, don't detect popups - let the close animation finish
                    return false;
                }
                // Cooldown just ended - trigger a rescan to update the UI
                MelonLogger.Msg($"[{_logPrefix}] Popup dismiss cooldown ended, triggering rescan");
                _activePanels.RemoveWhere(p => p.StartsWith("Popup:"));
                return true; // Signal rescan needed
            }

            bool stateChanged = false;
            GameObject activePopup = null;
            string activePopupName = null;

            // Find the first active popup/dialog
            // Use whitelist approach - only detect specific modal dialog patterns
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                // Only detect actual modal dialogs:
                // 1. SystemMessageView - confirmation dialogs (Exit Game, etc.)
                // 2. Names ending with "Popup(Clone)" - actual popup prefabs
                bool isModalPopup = false;

                if (go.name.Contains("SystemMessageView"))
                {
                    // For SystemMessageView, verify it has active button children
                    // The container may stay active but hide buttons when closed
                    if (HasActiveButtonChild(go, "SystemMessageButton"))
                    {
                        isModalPopup = true;
                    }
                }
                else if (go.name.EndsWith("Popup(Clone)"))
                {
                    // Actual popup prefab instances, but skip certain types
                    if (!go.name.Contains("Wildcard"))  // WildcardPopup is a tooltip, not modal
                        isModalPopup = true;
                }

                if (!isModalPopup) continue;

                activePopup = go;
                activePopupName = go.name;
                break; // Take the first one found
            }

            // Check if we found an active popup that should be foreground
            if (activePopup != null)
            {
                int id = activePopup.GetInstanceID();
                bool isNewPopup = !_knownPopupIds.Contains(id);

                // If popup is active but not currently the foreground, set it
                if (_foregroundPanel != activePopup)
                {
                    // Save the current foreground before switching to popup
                    // (only if current foreground is not already a popup)
                    if (_foregroundPanel != null && !IsPopupName(_foregroundPanel.name))
                    {
                        _panelBeforePopup = _foregroundPanel;
                        MelonLogger.Msg($"[{_logPrefix}] Saved panel before popup: {_panelBeforePopup.name}");
                    }

                    _foregroundPanel = activePopup;
                    _activePanels.RemoveWhere(p => p.StartsWith("Popup:"));
                    _activePanels.Add($"Popup:{activePopupName}");
                    stateChanged = true;

                    if (isNewPopup)
                    {
                        _knownPopupIds.Add(id);
                        MelonLogger.Msg($"[{_logPrefix}] New popup detected: {activePopupName}");
                        string cleanName = CleanPopupName(activePopupName);
                        _announcer?.AnnounceInterrupt($"{cleanName} opened.");
                    }
                    else
                    {
                        MelonLogger.Msg($"[{_logPrefix}] Popup reopened: {activePopupName}");
                    }

                    MelonLogger.Msg($"[{_logPrefix}] Set foreground panel to popup: {activePopupName}");
                }
            }
            else
            {
                // No active popup - clear foreground if it was a popup
                if (_foregroundPanel != null && IsPopupName(_foregroundPanel.name))
                {
                    MelonLogger.Msg($"[{_logPrefix}] Popup closed: {_foregroundPanel.name}");
                    _activePanels.RemoveWhere(p => p.StartsWith("Popup:"));
                    _foregroundPanel = null;
                    stateChanged = true;
                }
            }

            // Clean up IDs of popups that no longer exist
            _knownPopupIds.RemoveWhere(id =>
            {
                var go = FindObjectFromInstanceID(id);
                return go == null || !go.activeInHierarchy;
            });

            return stateChanged;
        }

        /// <summary>
        /// Check if a name indicates a popup/dialog.
        /// </summary>
        private static bool IsPopupName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.Contains("Popup") || name.Contains("SystemMessageView");
        }

        /// <summary>
        /// Check if a GameObject has an active child containing the specified name pattern.
        /// Used to verify popups are actually showing content, not just hidden containers.
        /// </summary>
        private static bool HasActiveButtonChild(GameObject parent, string namePattern)
        {
            if (parent == null) return false;

            // Check all descendants recursively
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(false))
            {
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                if (child.name.Contains(namePattern))
                    return true;
            }
            return false;
        }

        // Login scene panel name patterns (these are simple prefabs without controllers)
        private static readonly string[] LoginPanelPatterns = new[]
        {
            "Panel - WelcomeGate",
            "Panel - Log In",
            "Panel - Register",
            "Panel - ForgotCredentials",
            "Panel - AgeGate"
        };

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
            // These panels don't have IsOpen/Show/Hide, just GameObject activation
            DetectLoginPanels(activePanels);

            return activePanels;
        }

        /// <summary>
        /// Detect Login scene panels by GameObject name patterns.
        /// These are simple prefab instances without controller classes.
        /// </summary>
        private void DetectLoginPanels(List<(string name, GameObject obj)> activePanels)
        {
            // Find PanelParent which contains Login scene panels
            var panelParent = GameObject.Find("Canvas - Camera/PanelParent");
            if (panelParent == null) return;

            foreach (Transform child in panelParent.transform)
            {
                if (child == null || !child.gameObject.activeInHierarchy) continue;

                string childName = child.name;

                // Check if this matches a Login panel pattern
                foreach (var pattern in LoginPanelPatterns)
                {
                    if (childName.StartsWith(pattern))
                    {
                        string panelId = $"LoginPanel:{childName}";
                        if (!activePanels.Any(p => p.name == panelId))
                        {
                            activePanels.Add((panelId, child.gameObject));
                            MelonLogger.Msg($"[{_logPrefix}] Detected Login panel: {childName}");
                        }
                        break;
                    }
                }
            }
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
        /// Settings, Popups, SystemMessage dialogs, and Login panels are overlays.
        /// NavContentController (HomePage, etc.) are not.
        /// </summary>
        public static bool IsOverlayPanel(string panelName)
        {
            return panelName.StartsWith("SettingsMenu:") ||
                   panelName.StartsWith("PopupBase:") ||
                   panelName.StartsWith("LoginPanel:") ||
                   panelName.Contains("SystemMessageView");
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
        /// E.g., "SystemMessageView_Desktop_16x9(Clone)" -> "Confirmation"
        /// </summary>
        public static string CleanPopupName(string popupName)
        {
            if (string.IsNullOrEmpty(popupName)) return "Popup";

            // Special case for SystemMessageView - it's a confirmation dialog
            if (popupName.Contains("SystemMessageView"))
                return "Confirmation";

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
