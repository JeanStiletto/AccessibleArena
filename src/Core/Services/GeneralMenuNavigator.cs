using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MelonLoader;
using MTGAAccessibility.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// General-purpose navigator for menu screens that use CustomButton components.
    /// Acts as a fallback for any menu-like screen not handled by a specific navigator.
    /// Can also serve as a base class for specific menu navigators.
    ///
    /// MTGA uses CustomButton (not standard Unity Button/Selectable) for most menus,
    /// which is why standard Unity navigation doesn't work.
    /// </summary>
    public class GeneralMenuNavigator : BaseNavigator
    {
        // Scenes where this navigator should NOT activate (handled by other navigators)
        private static readonly HashSet<string> ExcludedScenes = new HashSet<string>
        {
            "Bootstrap", "AssetPrep", "Login", "DuelScene", "DraftScene", "SealedScene"
        };

        // Minimum CustomButtons needed to consider this a menu
        private const int MinButtonsForMenu = 2;

        // Types to check for open state (game's internal menu controllers)
        private static readonly string[] MenuControllerTypes = new[]
        {
            "NavContentController",      // Base class for all menu screens
            "SettingsMenu",              // Settings panel
            "SettingsMenuHost",          // Settings host
            "PopupBase"                  // Modal popups
        };

        protected string _currentScene;
        protected string _detectedMenuType;
        private bool _hasLoggedUIOnce;
        private float _activationDelay;
        private const float ACTIVATION_DELAY_SECONDS = 0.5f; // Wait for UI to settle

        // Panel state tracking
        private HashSet<string> _activePanels = new HashSet<string>();
        private GameObject _foregroundPanel = null; // The topmost panel to filter elements
        private float _rescanDelay = 0f;
        private const float RESCAN_DELAY_SECONDS = 0.5f; // Wait for panel UI to load
        private float _lastRescanTime = 0f;
        private const float RESCAN_DEBOUNCE_SECONDS = 1.0f; // Don't rescan again within this time

        // One-time rescan after activation to catch late-loading elements (e.g., HomePage)
        private float _postActivationRescanDelay = 0f;
        private const float POST_ACTIVATION_RESCAN_DELAY = 1.0f; // Check 1 second after activation
        private int _elementCountAtActivation = 0;

        // Auto-expand blade for certain panels (e.g., Color Challenge)
        private float _bladeAutoExpandDelay = 0f;
        private const float BLADE_AUTO_EXPAND_DELAY = 0.8f; // Wait for UI to load before expanding

        // Force rescan flag - bypasses debounce when set (used for toggle activations)
        private bool _forceRescan = false;

        // Threshold for Selectable count change to trigger rescan.
        // Value of 0 means any change triggers rescan - needed because some panels
        // (like deck selection) only change count by 1 and don't trigger Harmony patches.
        private const int SELECTABLE_COUNT_CHANGE_THRESHOLD = 0;

        public override string NavigatorId => "GeneralMenu";
        public override string ScreenName => GetMenuScreenName();
        public override int Priority => 15; // Low priority - fallback after specific navigators

        // Cached Settings panel reference (refreshed each check)
        private GameObject _settingsContentPanel;

        /// <summary>
        /// Check if Settings menu is currently open. Also caches the panel reference.
        /// Checks for any Settings content panel (MainMenu, Gameplay, Graphics, Audio, etc.)
        /// </summary>
        protected bool IsInSettingsMenu
        {
            get
            {
                // Check for any Settings content panel (main menu or submenus)
                string[] settingsPanels = { "Content - MainMenu", "Content - Gameplay", "Content - Graphics", "Content - Audio" };
                foreach (var panelName in settingsPanels)
                {
                    var panel = GameObject.Find(panelName);
                    if (panel != null && panel.activeInHierarchy)
                    {
                        _settingsContentPanel = panel;
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Get the cached Settings content panel (call IsInSettingsMenu first to refresh)
        /// </summary>
        protected GameObject SettingsContentPanel => _settingsContentPanel;

        public GeneralMenuNavigator(IAnnouncementService announcer) : base(announcer) { }

        protected virtual string GetMenuScreenName()
        {
            // Check if Settings menu is open (highest priority overlay)
            if (IsInSettingsMenu)
            {
                return "Settings";
            }

            // Check for PlayBlade state (deck selection, play mode)
            var playBladeState = GetPlayBladeStateName();
            if (!string.IsNullOrEmpty(playBladeState))
            {
                return playBladeState;
            }

            // Detect active content controller
            _activeContentController = DetectActiveContentController();

            if (!string.IsNullOrEmpty(_activeContentController))
            {
                string baseName = GetContentControllerDisplayName(_activeContentController);

                // For Home screen, add context about what's visible
                if (_activeContentController == "HomePageContentController")
                {
                    bool hasCarousel = HasVisibleCarousel();
                    bool hasColorChallenge = HasColorChallengeVisible();

                    if (hasCarousel && hasColorChallenge)
                        return "Home";
                    else if (hasCarousel)
                        return "Home with Events";
                    else if (hasColorChallenge)
                        return "Home with Color Challenge";
                    else
                        return "Home";
                }

                return baseName;
            }

            // Fall back to detected menu type from button patterns
            if (!string.IsNullOrEmpty(_detectedMenuType))
                return _detectedMenuType;

            // Last resort: use scene name
            return _currentScene switch
            {
                "HomePage" => "Home",
                "NavBar" => "Navigation Bar",
                "Store" => "Store",
                "Collection" => "Collection",
                "Decks" => "Decks",
                "Profile" => "Profile",
                "Settings" => "Settings",
                _ => "Menu"
            };
        }

        public override void OnSceneChanged(string sceneName)
        {
            _currentScene = sceneName;
            _hasLoggedUIOnce = false;
            _activationDelay = ACTIVATION_DELAY_SECONDS;
            _activePanels.Clear();
            _foregroundPanel = null; // Clear panel filter on scene change
            _playBladeActive = false;
            _playBladeState = null;
            _contentPanelActive = false;
            _activeContentController = null;

            if (_isActive)
            {
                // Check if we should stay active
                if (ExcludedScenes.Contains(sceneName))
                {
                    Deactivate();
                }
            }
        }

        /// <summary>
        /// Called when navigator is deactivating - clean up panel state
        /// </summary>
        protected override void OnDeactivating()
        {
            base.OnDeactivating();
            _foregroundPanel = null;
            _activePanels.Clear();
            _postActivationRescanDelay = 0f;
            _playBladeActive = false;
            _playBladeState = null;
            _contentPanelActive = false;
            _activeContentController = null;
        }

        /// <summary>
        /// Called when navigator activates - set up post-activation rescan check
        /// </summary>
        protected override void OnActivated()
        {
            base.OnActivated();
            // Schedule a one-time check for late-loading elements (e.g., HomePage loads after NavBar)
            _elementCountAtActivation = _elements.Count;
            _postActivationRescanDelay = POST_ACTIVATION_RESCAN_DELAY;
        }

        public override void Update()
        {
            // Handle rescan delay after button activation
            if (_rescanDelay > 0)
            {
                _rescanDelay -= Time.deltaTime;
                if (_rescanDelay <= 0)
                {
                    PerformRescan();
                }
                // Don't return early - still process input during rescan delay
            }

            // Handle blade auto-expand for panels like Color Challenge
            if (_bladeAutoExpandDelay > 0)
            {
                _bladeAutoExpandDelay -= Time.deltaTime;
                if (_bladeAutoExpandDelay <= 0)
                {
                    AutoExpandBlade();
                }
            }

            // One-time check after activation to detect unknown panels/overlays
            // (Known panels like Settings are handled directly in OnElementActivated)
            if (_postActivationRescanDelay > 0 && _isActive)
            {
                _postActivationRescanDelay -= Time.deltaTime;
                if (_postActivationRescanDelay <= 0)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Post-activation check running (stored count: {_elementCountAtActivation})");

                    // First try game state detection
                    CheckForPanelChanges();

                    // If that didn't trigger a rescan, fall back to Selectable count
                    // This catches overlays that aren't NavContentController/SettingsMenu/PopupBase
                    // Uses same approach as FocusTracker - count all active Selectables
                    if (_rescanDelay <= 0)
                    {
                        int currentCount = CountActiveSelectables();
                        int difference = System.Math.Abs(currentCount - _elementCountAtActivation);
                        MelonLogger.Msg($"[{NavigatorId}] Selectable count: {_elementCountAtActivation} -> {currentCount} (diff: {difference})");
                        if (difference > SELECTABLE_COUNT_CHANGE_THRESHOLD)
                        {
                            MelonLogger.Msg($"[{NavigatorId}] Selectable count changed significantly (>{SELECTABLE_COUNT_CHANGE_THRESHOLD}), rescanning");
                            PerformRescan();
                        }
                    }
                }
            }

            // Call base Update for normal navigation handling
            base.Update();
        }

        /// <summary>
        /// Handle custom input keys. Backspace navigates back to Home when in a content panel.
        /// </summary>
        protected override bool HandleCustomInput()
        {
            // Backspace: Navigate back to Home (main menu)
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                if (_contentPanelActive)
                {
                    return NavigateToHome();
                }
            }

            return false;
        }

        /// <summary>
        /// Find and activate the Home button to return to the main menu.
        /// The Home button may be filtered out of navigation but is still in the scene.
        /// </summary>
        private bool NavigateToHome()
        {
            // Find NavBar and its Home button
            var navBar = GameObject.Find("NavBar_Desktop_16x9(Clone)");
            if (navBar == null)
            {
                navBar = GameObject.Find("NavBar");
            }

            if (navBar == null)
            {
                MelonLogger.Msg($"[{NavigatorId}] NavBar not found for Home navigation");
                _announcer.Announce("Cannot navigate to Home", Models.AnnouncementPriority.High);
                return true;
            }

            // Find Nav_Home button
            var homeButtonTransform = navBar.transform.Find("Base/Nav_Home");
            GameObject homeButton = homeButtonTransform?.gameObject;
            if (homeButton == null)
            {
                // Try alternative paths
                homeButton = FindChildByName(navBar.transform, "Nav_Home");
            }

            if (homeButton == null || !homeButton.activeInHierarchy)
            {
                MelonLogger.Msg($"[{NavigatorId}] Home button not found or inactive");
                _announcer.Announce("Home button not available", Models.AnnouncementPriority.High);
                return true;
            }

            MelonLogger.Msg($"[{NavigatorId}] Navigating to Home via Backspace");
            _announcer.Announce("Returning to Home", Models.AnnouncementPriority.High);

            // Activate the Home button
            UIActivator.Activate(homeButton);

            return true;
        }

        /// <summary>
        /// Check if any panel containers have appeared or disappeared.
        /// Also detects content panel changes within overlays (e.g., Settings main → Gameplay submenu).
        /// </summary>
        private void CheckForPanelChanges()
        {
            // Check if current foreground panel became inactive OR changed to a different panel
            if (_foregroundPanel != null)
            {
                if (!_foregroundPanel.activeInHierarchy)
                {
                    // Panel deactivated - check if there's a new content panel (submenu navigation)
                    // or if the overlay actually closed
                    if (IsInSettingsMenu)
                    {
                        // Navigated to a different Settings submenu
                        MelonLogger.Msg($"[{NavigatorId}] Settings content panel changed: {_foregroundPanel.name} -> {SettingsContentPanel?.name}");
                        _foregroundPanel = SettingsContentPanel;
                        TriggerRescan();
                        return;
                    }
                    else
                    {
                        // Overlay actually closed
                        MelonLogger.Msg($"[{NavigatorId}] Foreground panel closed: {_foregroundPanel.name}");
                        _foregroundPanel = null;
                        _activePanels.Clear();
                        TriggerRescan();
                        return;
                    }
                }
            }

            var currentPanels = GetActivePanelsWithObjects();

            // Check for new panels
            foreach (var (panelName, panelObj) in currentPanels)
            {
                if (!_activePanels.Contains(panelName))
                {
                    MelonLogger.Msg($"[{NavigatorId}] Panel opened: {panelName}");
                    _activePanels.Add(panelName);

                    // Only set foreground filter for overlay panels (Settings, Popups)
                    // NavContentController descendants (HomePage, etc.) should NOT filter
                    if (IsOverlayPanel(panelName))
                    {
                        _foregroundPanel = panelObj;
                        MelonLogger.Msg($"[{NavigatorId}] Filtering to panel: {panelObj?.name}");
                    }
                    TriggerRescan();
                    return;
                }
            }

            // Check for closed panels
            var currentPanelNames = currentPanels.Select(p => p.name).ToHashSet();
            var closedPanels = _activePanels.Where(p => !currentPanelNames.Contains(p)).ToList();
            if (closedPanels.Count > 0)
            {
                foreach (var panel in closedPanels)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Panel closed: {panel}");
                    _activePanels.Remove(panel);
                }

                // Check if any remaining panel is an overlay that should filter
                var overlayPanel = currentPanels.LastOrDefault(p => IsOverlayPanel(p.name));
                _foregroundPanel = overlayPanel.obj; // Will be null if no overlay

                TriggerRescan();
            }
        }

        /// <summary>
        /// Get currently active panels by checking game's internal menu controllers.
        /// Uses two-pass approach: first find all open controllers, then apply priority.
        /// </summary>
        private List<(string name, GameObject obj)> GetActivePanelsWithObjects()
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

            // Check if Settings menu is open (uses cached property)
            bool settingsMenuOpen = IsInSettingsMenu;

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
                    GameObject panelObj = (typeName == "SettingsMenu" && SettingsContentPanel != null)
                        ? SettingsContentPanel
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
        private bool CheckIsOpen(MonoBehaviour mb, System.Type type)
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
                    MelonLogger.Warning($"[{NavigatorId}] Failed to read IsOpen property on {typeName}: {ex.Message}");
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
                        MelonLogger.Warning($"[{NavigatorId}] Failed to invoke IsOpen() method on {typeName}: {ex.Message}");
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
                    MelonLogger.Warning($"[{NavigatorId}] Failed to read IsReadyToShow on {typeName}: {ex.Message}");
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
                    MelonLogger.Warning($"[{NavigatorId}] Failed to read IsMainPanelActive on {typeName}: {ex.Message}");
                }
            }

            return true;
        }

        /// <summary>
        /// Check if a panel name represents an overlay that should filter elements.
        /// Only Settings and Popups are overlays. NavContentController (HomePage, etc.) are not.
        /// </summary>
        private bool IsOverlayPanel(string panelName)
        {
            // Panel names are formatted as "TypeName:GameObjectName"
            return panelName.StartsWith("SettingsMenu:") || panelName.StartsWith("PopupBase:");
        }

        /// <summary>
        /// Check if element should be shown based on current overlay state.
        /// Uses inverse filtering: when in overlay, exclude background elements rather than
        /// requiring elements to be children of a specific panel.
        /// This is more robust because overlay content can be anywhere in hierarchy.
        /// </summary>
        private bool IsInForegroundPanel(GameObject obj)
        {
            // Check if any overlay is active (either detected via panel or via Harmony flag)
            // _playBladeActive kept separate due to special blade-inside filtering logic
            bool overlayActive = _foregroundPanel != null || _playBladeActive || _contentPanelActive;

            if (!overlayActive)
                return true; // No overlay active, show all

            // When overlay is active, use inverse filtering:
            // Exclude elements from known background contexts
            return !IsInBackgroundPanel(obj);
        }

        /// <summary>
        /// Check if element belongs to a known background panel that should be hidden
        /// when an overlay is active.
        /// </summary>
        private bool IsInBackgroundPanel(GameObject obj)
        {
            Transform current = obj.transform;
            bool isInsideHomePage = false;
            bool isInsideBlade = false;

            while (current != null)
            {
                string name = current.gameObject.name;

                // Check if element is inside a Blade (PlayBlade, EventBlade, FindMatchBlade, etc.)
                // or CampaignGraph area (Color Challenge)
                // These should NOT be filtered even when inside HomePage
                if (name.Contains("Blade") ||
                    name.Contains("PlayBlade") ||
                    name.Contains("FindMatch") ||
                    name.Contains("EventBlade") ||
                    name.Contains("CampaignGraph"))
                {
                    isInsideBlade = true;
                }

                // Known background panel indicators
                // HomePage and its content - the main menu content area
                if (name.Contains("HomePage") ||
                    name.Contains("HomeContent") ||
                    name.Contains("Home_Desktop"))
                {
                    isInsideHomePage = true;
                }

                // NavBar elements - always background
                if (name == "NavBar" || name.StartsWith("NavBar_"))
                {
                    // NavBar is always background unless we're in a blade inside it
                    if (!isInsideBlade)
                        return true;
                }

                current = current.parent;
            }

            // If PlayBlade is active and element is inside a Blade, don't filter it
            if (_playBladeActive && isInsideBlade)
            {
                return false; // Not background - show this element
            }

            // If inside HomePage but not inside a Blade, it's background
            if (isInsideHomePage && !isInsideBlade)
            {
                return true; // Background - filter this element
            }

            return false;
        }

        /// <summary>
        /// Schedule a rescan after a short delay to let UI settle
        /// </summary>
        /// <param name="force">If true, bypasses the debounce check (used for toggle activations)</param>
        private void TriggerRescan(bool force = false)
        {
            _rescanDelay = RESCAN_DELAY_SECONDS;
            if (force)
            {
                _forceRescan = true;
            }
        }

        /// <summary>
        /// Force rescan of available elements
        /// </summary>
        private void PerformRescan()
        {
            // Debounce: skip if we just rescanned recently (unless forced)
            float currentTime = Time.time;
            if (!_forceRescan && currentTime - _lastRescanTime < RESCAN_DEBOUNCE_SECONDS)
            {
                MelonLogger.Msg($"[{NavigatorId}] Skipping rescan - debounce active");
                return;
            }
            _lastRescanTime = currentTime;
            _forceRescan = false; // Reset force flag

            // Cancel any pending post-activation check since we're rescanning now.
            // This prevents double rescans when both Harmony patch and post-activation
            // check detect the same panel change.
            _postActivationRescanDelay = 0f;

            MelonLogger.Msg($"[{NavigatorId}] Rescanning elements after panel change");

            // Remember the navigator's current selection before clearing
            GameObject previousSelection = null;
            if (_currentIndex >= 0 && _currentIndex < _elements.Count)
            {
                previousSelection = _elements[_currentIndex].GameObject;
            }

            // Log UI elements during rescan to help debug component differences
            LogAvailableUIElements();

            // Clear and rediscover
            _elements.Clear();
            _currentIndex = 0;

            DiscoverElements();

            // Try to find the previously selected object in the new element list
            if (previousSelection != null)
            {
                for (int i = 0; i < _elements.Count; i++)
                {
                    if (_elements[i].GameObject == previousSelection)
                    {
                        _currentIndex = i;
                        MelonLogger.Msg($"[{NavigatorId}] Preserved selection at index {i}: {previousSelection.name}");
                        break;
                    }
                }
            }

            // Update menu type based on new state
            _detectedMenuType = DetectMenuType();

            // Announce the change
            string announcement = GetActivationAnnouncement();
            _announcer.Announce(announcement, Models.AnnouncementPriority.High);
        }

        protected override bool DetectScreen()
        {
            // Don't activate on excluded scenes
            if (ExcludedScenes.Contains(_currentScene))
                return false;

            // Wait for UI to settle after scene change
            if (_activationDelay > 0)
            {
                _activationDelay -= Time.deltaTime;
                return false;
            }

            // Check if there's an overlay blocking us
            if (IsOverlayActive())
            {
                return false;
            }

            // Don't activate on NPE screens - let EventTriggerNavigator handle those
            if (IsNPEScreenActive())
            {
                return false;
            }

            // Count CustomButtons to determine if this is a menu
            int customButtonCount = CountActiveCustomButtons();

            if (customButtonCount < MinButtonsForMenu)
            {
                return false;
            }

            // Log UI elements once for debugging
            if (!_hasLoggedUIOnce)
            {
                LogAvailableUIElements();
                _hasLoggedUIOnce = true;
            }

            // Determine menu type based on what we find
            _detectedMenuType = DetectMenuType();

            MelonLogger.Msg($"[{NavigatorId}] Detected menu: {_detectedMenuType} with {customButtonCount} CustomButtons");
            return true;
        }

        protected virtual bool IsOverlayActive()
        {
            // Check for common overlay indicators
            var overlayPatterns = new[] { "Background_ClickBlocker", "ModalBlocker", "PopupBlocker" };

            foreach (var pattern in overlayPatterns)
            {
                var blocker = GameObject.Find(pattern);
                if (blocker != null && blocker.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if we're on an NPE (New Player Experience) screen that should be
        /// handled by EventTriggerNavigator with its specialized logic.
        /// </summary>
        protected virtual bool IsNPEScreenActive()
        {
            // NPE containers that EventTriggerNavigator handles specially
            var npeContainers = new[] {
                "DeckInspection_Container",
                "NPE-Rewards_Container",
                "NPE_RewardChest",
                "NPEContentController"
            };

            foreach (var containerName in npeContainers)
            {
                var container = GameObject.Find(containerName);
                if (container != null && container.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        protected int CountActiveCustomButtons()
        {
            int count = 0;
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == "CustomButton")
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Count active Selectables (same as FocusTracker) - includes Buttons, Toggles, etc.
        /// </summary>
        protected int CountActiveSelectables()
        {
            int count = 0;
            foreach (var sel in GameObject.FindObjectsOfType<UnityEngine.UI.Selectable>())
            {
                if (sel != null && sel.isActiveAndEnabled && sel.interactable)
                    count++;
            }
            return count;
        }

        protected virtual string DetectMenuType()
        {
            // Try to identify the menu type by looking for specific elements

            // Check for main menu indicators
            var playButton = FindButtonByPattern("Play", "Battle", "Start");
            if (playButton != null) return "Main Menu";

            // Check for store
            var storeIndicator = FindButtonByPattern("Purchase", "Buy", "Pack", "Bundle");
            if (storeIndicator != null) return "Store";

            // Check for collection/decks
            var deckIndicator = FindButtonByPattern("Deck", "Collection", "Card");
            if (deckIndicator != null) return "Collection";

            // Check for settings
            var settingsIndicator = FindButtonByPattern("Settings", "Options", "Audio", "Graphics");
            if (settingsIndicator != null) return "Settings";

            return "Menu";
        }

        protected GameObject FindButtonByPattern(params string[] patterns)
        {
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name != "CustomButton") continue;

                string objName = mb.gameObject.name;
                string text = UITextExtractor.GetText(mb.gameObject);

                foreach (var pattern in patterns)
                {
                    if (objName.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return mb.gameObject;
                    if (!string.IsNullOrEmpty(text) && text.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return mb.gameObject;
                }
            }
            return null;
        }

        protected virtual void LogAvailableUIElements()
        {
            MelonLogger.Msg($"[{NavigatorId}] === UI DUMP FOR {_currentScene} ===");

            // Find all CustomButtons
            var customButtons = new List<(GameObject obj, string text, string path)>();
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name != "CustomButton") continue;

                string text = UITextExtractor.GetText(mb.gameObject);
                string path = GetGameObjectPath(mb.gameObject);
                customButtons.Add((mb.gameObject, text ?? "(no text)", path));
            }

            MelonLogger.Msg($"[{NavigatorId}] Found {customButtons.Count} CustomButtons:");
            foreach (var (obj, text, path) in customButtons.OrderBy(x => x.path).Take(40))
            {
                // Get detailed component info for analysis
                bool hasActualText = UITextExtractor.HasActualText(obj);
                var componentTypes = obj.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name)
                    .ToList();
                string components = string.Join(", ", componentTypes);

                // Get size from RectTransform
                string sizeInfo = "";
                var rectTransform = obj.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    sizeInfo = $" | Size: {rectTransform.sizeDelta.x:F0}x{rectTransform.sizeDelta.y:F0}";
                }

                // Check for Image components
                bool hasImage = obj.GetComponent<Image>() != null || obj.GetComponent<RawImage>() != null;
                bool hasTextChild = obj.GetComponentInChildren<TMPro.TMP_Text>() != null;

                // Get sprite name if this is a Color Challenge button
                string spriteInfo = "";
                if (path.Contains("ColorMastery") || path.Contains("PlayBlade_Item"))
                {
                    var image = obj.GetComponent<Image>();
                    if (image != null && image.sprite != null)
                    {
                        spriteInfo = $" | Sprite: {image.sprite.name}";
                    }
                    // Also check parent for color info
                    var parent = obj.transform.parent;
                    if (parent != null)
                    {
                        spriteInfo += $" | Parent: {parent.name}";
                        // Check siblings for text
                        foreach (Transform sibling in parent)
                        {
                            if (sibling.gameObject != obj)
                            {
                                var sibText = UITextExtractor.GetText(sibling.gameObject);
                                if (!string.IsNullOrEmpty(sibText) && sibText.Length > 1)
                                {
                                    spriteInfo += $" | Sibling[{sibling.name}]: {sibText}";
                                }
                            }
                        }
                    }
                }

                MelonLogger.Msg($"[{NavigatorId}]   {path}");
                MelonLogger.Msg($"[{NavigatorId}]     Text: '{text}' | HasActualText: {hasActualText} | HasImage: {hasImage} | HasTextChild: {hasTextChild}{sizeInfo}{spriteInfo}");
                MelonLogger.Msg($"[{NavigatorId}]     Components: {components}");
            }

            // Find EventTriggers
            var eventTriggers = GameObject.FindObjectsOfType<UnityEngine.EventSystems.EventTrigger>()
                .Where(e => e.gameObject.activeInHierarchy)
                .ToList();
            MelonLogger.Msg($"[{NavigatorId}] Found {eventTriggers.Count} EventTriggers:");
            foreach (var et in eventTriggers.Take(15))
            {
                string text = UITextExtractor.GetText(et.gameObject);
                MelonLogger.Msg($"[{NavigatorId}]   {et.gameObject.name} - '{text ?? "(no text)"}'");
            }

            // Find standard Buttons
            var buttons = GameObject.FindObjectsOfType<Button>()
                .Where(b => b.gameObject.activeInHierarchy && b.interactable)
                .ToList();
            MelonLogger.Msg($"[{NavigatorId}] Found {buttons.Count} standard Buttons:");
            foreach (var btn in buttons.Take(15))
            {
                string text = UITextExtractor.GetText(btn.gameObject);
                MelonLogger.Msg($"[{NavigatorId}]   {btn.gameObject.name} - '{text ?? "(no text)"}'");
            }

            MelonLogger.Msg($"[{NavigatorId}] === END UI DUMP ===");
        }

        protected override void DiscoverElements()
        {
            var addedObjects = new HashSet<GameObject>();
            var discoveredElements = new List<(GameObject obj, UIElementClassifier.ClassificationResult classification, float sortOrder)>();

            // Log panel filter state (validation done in CheckForPanelChanges)
            if (_foregroundPanel != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Filtering to panel: {_foregroundPanel.name}");
            }

            // Find all CustomButtons
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name != "CustomButton") continue;
                if (addedObjects.Contains(mb.gameObject)) continue;

                // Filter by foreground panel if one is active
                if (!IsInForegroundPanel(mb.gameObject)) continue;

                var classification = UIElementClassifier.Classify(mb.gameObject);
                if (classification.IsNavigable && classification.ShouldAnnounce)
                {
                    var pos = mb.transform.position;
                    float sortOrder = -pos.y * 1000 + pos.x;
                    discoveredElements.Add((mb.gameObject, classification, sortOrder));
                    addedObjects.Add(mb.gameObject);
                }
            }

            // Find standard Buttons
            foreach (var btn in GameObject.FindObjectsOfType<Button>())
            {
                if (btn == null || !btn.gameObject.activeInHierarchy || !btn.interactable) continue;
                if (addedObjects.Contains(btn.gameObject)) continue;

                // Filter by foreground panel if one is active
                if (!IsInForegroundPanel(btn.gameObject)) continue;

                var classification = UIElementClassifier.Classify(btn.gameObject);
                if (classification.IsNavigable && classification.ShouldAnnounce)
                {
                    var pos = btn.transform.position;
                    float sortOrder = -pos.y * 1000 + pos.x;
                    discoveredElements.Add((btn.gameObject, classification, sortOrder));
                    addedObjects.Add(btn.gameObject);
                }
            }

            // Find EventTrigger elements
            foreach (var trigger in GameObject.FindObjectsOfType<UnityEngine.EventSystems.EventTrigger>())
            {
                if (trigger == null || !trigger.gameObject.activeInHierarchy) continue;
                if (addedObjects.Contains(trigger.gameObject)) continue;

                // Filter by foreground panel if one is active
                if (!IsInForegroundPanel(trigger.gameObject)) continue;

                var classification = UIElementClassifier.Classify(trigger.gameObject);
                if (classification.IsNavigable && classification.ShouldAnnounce)
                {
                    var pos = trigger.transform.position;
                    float sortOrder = -pos.y * 1000 + pos.x;
                    discoveredElements.Add((trigger.gameObject, classification, sortOrder));
                    addedObjects.Add(trigger.gameObject);
                }
            }

            // Find Toggle elements (e.g., Settings checkboxes in Gameplay/Audio menus)
            foreach (var toggle in GameObject.FindObjectsOfType<Toggle>())
            {
                if (toggle == null || !toggle.gameObject.activeInHierarchy || !toggle.interactable) continue;
                if (addedObjects.Contains(toggle.gameObject)) continue;

                // Filter by foreground panel if one is active
                if (!IsInForegroundPanel(toggle.gameObject)) continue;

                var classification = UIElementClassifier.Classify(toggle.gameObject);
                if (classification.IsNavigable && classification.ShouldAnnounce)
                {
                    var pos = toggle.transform.position;
                    float sortOrder = -pos.y * 1000 + pos.x;
                    discoveredElements.Add((toggle.gameObject, classification, sortOrder));
                    addedObjects.Add(toggle.gameObject);
                }
            }

            // Find Slider elements (e.g., Audio volume sliders)
            foreach (var slider in GameObject.FindObjectsOfType<Slider>())
            {
                if (slider == null || !slider.gameObject.activeInHierarchy || !slider.interactable) continue;
                if (addedObjects.Contains(slider.gameObject)) continue;

                // Filter by foreground panel if one is active
                if (!IsInForegroundPanel(slider.gameObject)) continue;

                var classification = UIElementClassifier.Classify(slider.gameObject);
                if (classification.IsNavigable && classification.ShouldAnnounce)
                {
                    var pos = slider.transform.position;
                    float sortOrder = -pos.y * 1000 + pos.x;
                    discoveredElements.Add((slider.gameObject, classification, sortOrder));
                    addedObjects.Add(slider.gameObject);
                }
            }

            // Process deck entries: pair main buttons with their TextBox edit buttons
            // Each deck entry has UI (CustomButton for selection) and TextBox (for editing name)
            var deckElements = discoveredElements.Where(x => x.classification.Label.Contains(", deck")).ToList();
            var deckPairs = new Dictionary<Transform, (GameObject mainButton, GameObject editButton)>();

            foreach (var (obj, classification, _) in deckElements)
            {
                // Find the DeckView_Base parent to group elements by deck entry
                Transform deckViewParent = FindDeckViewParent(obj.transform);
                if (deckViewParent == null) continue;

                if (!deckPairs.ContainsKey(deckViewParent))
                {
                    deckPairs[deckViewParent] = (null, null);
                }

                var pair = deckPairs[deckViewParent];

                // UI element (CustomButton) is the main selection button
                // TextBox element is for editing the deck name
                if (obj.name == "UI")
                {
                    deckPairs[deckViewParent] = (obj, pair.editButton);
                }
                else if (obj.name == "TextBox")
                {
                    deckPairs[deckViewParent] = (pair.mainButton, obj);
                }
            }

            // Build set of TextBox objects to skip (they'll be added as alternate actions)
            var textBoxesToSkip = new HashSet<GameObject>(deckPairs.Values
                .Where(p => p.editButton != null)
                .Select(p => p.editButton));

            // Map main deck buttons to their edit buttons for alternate action
            var deckEditButtons = deckPairs
                .Where(p => p.Value.mainButton != null && p.Value.editButton != null)
                .ToDictionary(p => p.Value.mainButton, p => p.Value.editButton);

            // Sort by position and add elements with proper labels
            foreach (var (obj, classification, _) in discoveredElements.OrderBy(x => x.sortOrder))
            {
                // Skip TextBox elements - they're added as alternate actions on their main button
                if (textBoxesToSkip.Contains(obj))
                    continue;

                string announcement = BuildAnnouncement(classification);

                // Build carousel info if this element supports arrow navigation
                CarouselInfo carouselInfo = classification.HasArrowNavigation
                    ? new CarouselInfo
                    {
                        HasArrowNavigation = true,
                        PreviousControl = classification.PreviousControl,
                        NextControl = classification.NextControl
                    }
                    : default;

                // Check if this deck button has an associated edit button
                GameObject alternateAction = null;
                if (deckEditButtons.TryGetValue(obj, out var editButton))
                {
                    alternateAction = editButton;
                }

                AddElement(obj, announcement, carouselInfo, alternateAction);
            }

            MelonLogger.Msg($"[{NavigatorId}] Discovered {_elements.Count} navigable elements");
        }

        /// <summary>
        /// Build the announcement string based on classification
        /// </summary>
        protected virtual string BuildAnnouncement(UIElementClassifier.ClassificationResult classification)
        {
            if (string.IsNullOrEmpty(classification.RoleLabel))
                return classification.Label;

            return $"{classification.Label}, {classification.RoleLabel}";
        }

        /// <summary>
        /// Find the DeckView_Base parent for a deck entry element.
        /// Used to group UI and TextBox elements that belong to the same deck.
        /// </summary>
        private Transform FindDeckViewParent(Transform element)
        {
            Transform current = element;
            int maxLevels = 5;

            while (current != null && maxLevels > 0)
            {
                if (current.name.Contains("DeckView_Base") ||
                    current.name.Contains("Blade_ListItem"))
                {
                    return current;
                }
                current = current.parent;
                maxLevels--;
            }

            return null;
        }

        protected string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            int depth = 0;
            while (parent != null && depth < 4)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
                depth++;
            }
            if (parent != null)
                path = ".../" + path;
            return path;
        }

        protected override string GetActivationAnnouncement()
        {
            string menuName = GetMenuScreenName();
            if (_elements.Count == 0)
            {
                return $"{menuName}. No navigable items found.";
            }
            return $"{menuName}. {_elements.Count} items. Tab to navigate, Enter to select.";
        }

        protected override bool OnElementActivated(int index, GameObject element)
        {
            // Store Selectable count BEFORE activation to detect changes caused by the activation
            _elementCountAtActivation = CountActiveSelectables();

            // Check if this is a Toggle (checkbox) - we'll force rescan for these
            bool isToggle = element.GetComponent<Toggle>() != null;

            // Use UIActivator for CustomButtons
            var result = UIActivator.Activate(element);
            _announcer.Announce(result.Message, Models.AnnouncementPriority.Normal);

            // For Toggle activations (checkboxes), force a rescan after a delay
            // This handles deck folder filtering where Selectable count may not change
            // but the visible decks do change
            // Use force=true to bypass debounce since user explicitly toggled a filter
            if (isToggle)
            {
                MelonLogger.Msg($"[{NavigatorId}] Toggle activated - forcing rescan in {RESCAN_DELAY_SECONDS}s (bypassing debounce)");
                TriggerRescan(force: true);
                return true;
            }

            // For non-toggle elements, schedule a post-activation check to detect panel changes
            // This generalized approach works for all menus, not just Settings:
            // - Settings submenus (Gameplay, Graphics, Audio)
            // - Deck selection panel
            // - Any other overlay or submenu
            // The check uses CheckForPanelChanges() which handles:
            // - New panels opening (sets _foregroundPanel)
            // - Panels closing (clears _foregroundPanel if no overlay remains)
            // - Panel transitions within overlays (updates _foregroundPanel to new content)
            _postActivationRescanDelay = POST_ACTIVATION_RESCAN_DELAY;
            MelonLogger.Msg($"[{NavigatorId}] Scheduled post-activation check in {POST_ACTIVATION_RESCAN_DELAY}s (pre-activation Selectables: {_elementCountAtActivation})");

            return true;
        }

        /// <summary>
        /// Called by NavigatorManager when Harmony patch detects a panel state change.
        /// This is event-driven - no polling needed.
        /// </summary>
        public void OnPanelStateChangedExternal(string panelTypeName, bool isOpen)
        {
            if (!_isActive) return;

            MelonLogger.Msg($"[{NavigatorId}] External panel state change: {panelTypeName} isOpen={isOpen}");

            // Ignore HomePage content controller - it's always there, not an overlay
            if (panelTypeName.Contains("HomePage") || panelTypeName.Contains("HomeContent"))
            {
                MelonLogger.Msg($"[{NavigatorId}] Ignoring HomePage content controller");
                return;
            }

            // Handle known overlay types immediately - don't wait for panel detection
            // This ensures filtering is set up before rescan happens

            // Content panels and overlays that should filter NavBar (not HomePageContentController)
            // Includes: menu pages, settings, deck selection, deck builder, etc.
            bool isContentPanel = panelTypeName.Contains("DeckManagerController") ||
                                  panelTypeName.Contains("ProfileContentController") ||
                                  panelTypeName.Contains("ContentController_StoreCarousel") ||
                                  panelTypeName.Contains("LearnToPlayController") ||
                                  panelTypeName.Contains("WrapperDeckBuilder") ||
                                  panelTypeName.Contains("MasteryContentController") ||
                                  panelTypeName.Contains("AchievementsContentController") ||
                                  panelTypeName.Contains("PackOpeningController") ||
                                  panelTypeName.Contains("SettingsMenu") ||
                                  panelTypeName.Contains("DeckSelectBlade") ||
                                  panelTypeName.Contains("CampaignGraphContentController");

            if (isContentPanel)
            {
                if (isOpen)
                {
                    _activePanels.Add($"ContentPanel:{panelTypeName}");
                    _contentPanelActive = true;
                    MelonLogger.Msg($"[{NavigatorId}] Content panel opened: {panelTypeName} (from Harmony)");

                    // Auto-expand blade for CampaignGraphContentController (Color Challenge menu)
                    // The blade starts collapsed, requiring an extra click - auto-expand for better UX
                    if (panelTypeName.Contains("CampaignGraphContentController"))
                    {
                        _bladeAutoExpandDelay = BLADE_AUTO_EXPAND_DELAY;
                        MelonLogger.Msg($"[{NavigatorId}] Scheduling blade auto-expand for Color Challenge");
                    }
                }
                else
                {
                    _activePanels.RemoveWhere(p => p.StartsWith("ContentPanel:"));
                    _contentPanelActive = false;
                    _foregroundPanel = null;
                    MelonLogger.Msg($"[{NavigatorId}] Content panel closed: {panelTypeName} (from Harmony)");
                }
            }
            // Handle PlayBlade and related blade views (kept separate due to special blade-inside logic)
            else if (panelTypeName.Contains("PlayBlade") || panelTypeName.Contains("Blade:"))
            {
                if (isOpen)
                {
                    _activePanels.Add($"PlayBlade:External:{panelTypeName}");
                    _playBladeActive = true;
                    // Track the state for screen name detection (e.g., "PlayBlade:Events")
                    _playBladeState = panelTypeName;
                    MelonLogger.Msg($"[{NavigatorId}] Play blade opened (from Harmony): {panelTypeName}");
                }
                else
                {
                    // Only close if it's the main PlayBlade closing (Hidden state)
                    // Sub-blade changes (Events -> DirectChallenge) shouldn't close
                    if (panelTypeName.Contains("Hidden") || panelTypeName.Contains("Hide"))
                    {
                        _activePanels.RemoveWhere(p => p.StartsWith("PlayBlade:"));
                        _playBladeActive = false;
                        _playBladeState = null;
                        _foregroundPanel = null;
                        MelonLogger.Msg($"[{NavigatorId}] Play blade closed (from Harmony): {panelTypeName}");
                    }
                    else
                    {
                        // Update state but keep blade active (state transition)
                        _playBladeState = panelTypeName;
                        MelonLogger.Msg($"[{NavigatorId}] Play blade state change (not closing): {panelTypeName}");
                    }
                }
            }
            // Handle EventBlade and DirectChallengeBlade from HomePageContentController
            else if (panelTypeName.Contains("EventBlade") || panelTypeName.Contains("DirectChallengeBlade"))
            {
                if (isOpen)
                {
                    _activePanels.Add($"EventBlade:External:{panelTypeName}");
                    _playBladeActive = true;
                    // Track for screen name - translate to PlayBlade state format
                    _playBladeState = panelTypeName.Contains("DirectChallenge") ? "PlayBlade:DirectChallenge" : "PlayBlade:Events";
                    MelonLogger.Msg($"[{NavigatorId}] Event/Challenge blade opened (from Harmony): {panelTypeName}");
                }
                else
                {
                    _activePanels.RemoveWhere(p => p.Contains("EventBlade:") || p.Contains("ChallengeBlade:"));
                    _playBladeActive = false;
                    _playBladeState = null;
                    _foregroundPanel = null;
                    MelonLogger.Msg($"[{NavigatorId}] Event/Challenge blade closed (from Harmony): {panelTypeName}");
                }
            }

            // Trigger rescan when any panel opens or closes
            TriggerRescan();
        }

        /// <summary>
        /// Auto-expand the play blade when it's in collapsed state.
        /// Used for panels like Color Challenge where the blade starts collapsed.
        /// </summary>
        private void AutoExpandBlade()
        {
            MelonLogger.Msg($"[{NavigatorId}] Attempting blade auto-expand");

            // Find the blade expand button (Btn_BladeIsClosed or its arrow child)
            GameObject bladeButton = null;

            // First try to find the arrow button (more reliable)
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name != "CustomButton") continue;

                string name = mb.gameObject.name;
                if (name.Contains("BladeHoverClosed") || name.Contains("Btn_BladeIsClosed"))
                {
                    bladeButton = mb.gameObject;
                    MelonLogger.Msg($"[{NavigatorId}] Found blade expand button: {name}");
                    break;
                }
            }

            if (bladeButton != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Auto-expanding blade via {bladeButton.name}");
                _announcer.Announce("Opening color challenges", Models.AnnouncementPriority.High);
                UIActivator.Activate(bladeButton);

                // Schedule a rescan after the blade opens
                TriggerRescan();
            }
            else
            {
                MelonLogger.Msg($"[{NavigatorId}] Could not find blade expand button");
            }
        }

        // Flags set by Harmony events to indicate overlay is active
        // Used by IsInForegroundPanel when _foregroundPanel hasn't been found yet
        private bool _playBladeActive = false; // PlayBlade kept separate due to special blade-inside filtering logic
        private bool _contentPanelActive = false; // Any content panel/overlay that should filter NavBar

        // Active content controller tracking for screen name detection
        private string _activeContentController = null;
        private string _playBladeState = null; // Hidden, Events, DirectChallenge, FriendChallenge

        #region Screen Detection Helpers

        /// <summary>
        /// Detect which content controller is currently active.
        /// Returns the type name of the active controller, or null if none detected.
        /// </summary>
        private string DetectActiveContentController()
        {
            // Check for specific content controllers by looking for their IsOpen state
            var contentControllerTypes = new[]
            {
                "HomePageContentController",
                "DeckManagerController",
                "ProfileContentController",
                "ContentController_StoreCarousel",
                "MasteryContentController",
                "AchievementsContentController",
                "LearnToPlayControllerV2",
                "PackOpeningController",
                "CampaignGraphContentController",
                "WrapperDeckBuilder"
            };

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                var type = mb.GetType();
                string typeName = type.Name;

                if (!contentControllerTypes.Contains(typeName)) continue;

                // Check IsOpen property
                var isOpenProp = type.GetProperty("IsOpen",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (isOpenProp != null && isOpenProp.PropertyType == typeof(bool))
                {
                    try
                    {
                        bool isOpen = (bool)isOpenProp.GetValue(mb);
                        if (isOpen)
                        {
                            // Also check IsReadyToShow if available
                            var isReadyProp = type.GetProperty("IsReadyToShow",
                                System.Reflection.BindingFlags.Public |
                                System.Reflection.BindingFlags.NonPublic |
                                System.Reflection.BindingFlags.Instance);

                            if (isReadyProp != null && isReadyProp.PropertyType == typeof(bool))
                            {
                                bool isReady = (bool)isReadyProp.GetValue(mb);
                                if (!isReady) continue; // Skip if not ready yet
                            }

                            return typeName;
                        }
                    }
                    catch { /* Ignore reflection errors */ }
                }
            }

            return null;
        }

        /// <summary>
        /// Check if the promotional carousel is visible on the home screen.
        /// </summary>
        private bool HasVisibleCarousel()
        {
            // Look for carousel navigation elements (Previous/Next buttons)
            var carouselPatterns = new[] { "Carousel", "NavGradient_Previous", "NavGradient_Next", "WelcomeBundle", "EventBlade_Item" };

            foreach (var pattern in carouselPatterns)
            {
                var obj = GameObject.Find(pattern);
                if (obj != null && obj.activeInHierarchy)
                {
                    return true;
                }
            }

            // Also check for carousel by finding nav controls in the elements
            foreach (var element in _elements)
            {
                if (element.Carousel.HasArrowNavigation)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Check if Color Challenge content is visible.
        /// </summary>
        private bool HasColorChallengeVisible()
        {
            // Check for CampaignGraph content controller being open
            if (_activeContentController == "CampaignGraphContentController")
                return true;

            // Also check for Color Challenge buttons directly
            var colorChallengePatterns = new[] { "ColorMastery", "CampaignGraph", "Color Challenge" };

            foreach (var pattern in colorChallengePatterns)
            {
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                    if (mb.GetType().Name != "CustomButton") continue;

                    if (mb.gameObject.name.Contains(pattern) ||
                        GetGameObjectPath(mb.gameObject).Contains(pattern))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Get a human-readable name for the current PlayBlade state.
        /// </summary>
        private string GetPlayBladeStateName()
        {
            if (!_playBladeActive || string.IsNullOrEmpty(_playBladeState))
                return null;

            // Extract state from "PlayBlade:Events" format
            if (_playBladeState.Contains(":"))
            {
                var parts = _playBladeState.Split(':');
                if (parts.Length >= 2)
                {
                    return parts[1] switch
                    {
                        "Events" => "Play Mode Selection",
                        "DirectChallenge" => "Direct Challenge",
                        "FriendChallenge" => "Friend Challenge",
                        "DeckSelected" => "Deck Selected",
                        _ => parts[1]
                    };
                }
            }

            return null;
        }

        /// <summary>
        /// Map content controller type name to user-friendly screen name.
        /// </summary>
        private string GetContentControllerDisplayName(string controllerTypeName)
        {
            return controllerTypeName switch
            {
                "HomePageContentController" => "Home",
                "DeckManagerController" => "Decks",
                "ProfileContentController" => "Profile",
                "ContentController_StoreCarousel" => "Store",
                "MasteryContentController" => "Mastery",
                "AchievementsContentController" => "Achievements",
                "LearnToPlayControllerV2" => "Learn to Play",
                "PackOpeningController" => "Pack Opening",
                "CampaignGraphContentController" => "Color Challenge",
                "WrapperDeckBuilder" => "Deck Builder",
                _ => controllerTypeName?.Replace("ContentController", "").Replace("Controller", "").Trim()
            };
        }

        #endregion
    }
}
