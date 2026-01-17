using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
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
        #region Configuration Constants

        // Scenes where this navigator should NOT activate (handled by other navigators)
        private static readonly HashSet<string> ExcludedScenes = new HashSet<string>
        {
            "Bootstrap", "AssetPrep", "Login", "DuelScene", "DraftScene", "SealedScene"
        };

        // Minimum CustomButtons needed to consider this a menu
        private const int MinButtonsForMenu = 2;

        // Blade container name patterns (used for element filtering when blade is active)
        private static readonly string[] BladePatterns = new[]
        {
            "Blade",
            "FindMatch",
            "CampaignGraph"
        };

        #endregion

        #region Timing Constants

        private const float ActivationDelaySeconds = 0.5f;
        private const float RescanDelaySeconds = 0.5f;
        private const float RescanDebounceSeconds = 1.0f;
        private const float BladeAutoExpandDelay = 0.8f;

        #endregion

        #region State Fields

        protected string _currentScene;
        protected string _detectedMenuType;
        private bool _hasLoggedUIOnce;
        private float _activationDelay;

        // Helper instances
        private readonly MenuScreenDetector _screenDetector;
        private readonly MenuPanelTracker _panelTracker;

        // Rescan timing
        private float _rescanDelay;
        private float _lastRescanTime;
        private bool _forceRescan;

        // Element tracking
        private int _elementCountAtActivation;
        private float _bladeAutoExpandDelay;

        // Blade state tracking
        private bool _playBladeActive;
        private string _playBladeState;

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get all active CustomButton GameObjects in the scene.
        /// Consolidates the common pattern of finding CustomButton/CustomButtonWithTooltip components.
        /// </summary>
        private IEnumerable<GameObject> GetActiveCustomButtons()
        {
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.gameObject.activeInHierarchy && IsCustomButtonType(mb.GetType().Name))
                    yield return mb.gameObject;
            }
        }

        /// <summary>
        /// Check if a type name is a CustomButton variant (CustomButton or CustomButtonWithTooltip).
        /// </summary>
        private static bool IsCustomButtonType(string typeName)
        {
            return typeName == "CustomButton" || typeName == "CustomButtonWithTooltip";
        }

        #endregion

        public override string NavigatorId => "GeneralMenu";
        public override string ScreenName => GetMenuScreenName();
        public override int Priority => 15; // Low priority - fallback after specific navigators

        /// <summary>
        /// Check if Settings menu is currently open.
        /// </summary>
        protected bool CheckSettingsMenuOpen() => _screenDetector.CheckSettingsMenuOpen();

        /// <summary>
        /// Cached Settings content panel.
        /// </summary>
        protected GameObject SettingsContentPanel => _screenDetector.SettingsContentPanel;

        /// <summary>
        /// Check if the Social/Friends panel is currently open.
        /// </summary>
        protected bool IsSocialPanelOpen() => _screenDetector.IsSocialPanelOpen();

        // Convenience properties to access helper state
        private string _activeContentController => _screenDetector.ActiveContentController;
        private GameObject _activeControllerGameObject => _screenDetector.ActiveControllerGameObject;
        private GameObject _navBarGameObject => _screenDetector.NavBarGameObject;
        private GameObject _foregroundPanel
        {
            get => _panelTracker.ForegroundPanel;
            set => _panelTracker.ForegroundPanel = value;
        }
        private HashSet<string> _activePanels => _panelTracker.ActivePanels;

        public GeneralMenuNavigator(IAnnouncementService announcer) : base(announcer)
        {
            _screenDetector = new MenuScreenDetector();
            _panelTracker = new MenuPanelTracker(announcer, NavigatorId);
        }

        protected virtual string GetMenuScreenName()
        {
            // Check if Settings menu is open (highest priority overlay)
            if (CheckSettingsMenuOpen())
            {
                return "Settings";
            }

            // Check if Social/Friends panel is open
            if (IsSocialPanelOpen())
            {
                return "Friends";
            }

            // Check for PlayBlade state (deck selection, play mode)
            var playBladeState = GetPlayBladeStateName();
            if (!string.IsNullOrEmpty(playBladeState))
            {
                return playBladeState;
            }

            // Detect active content controller
            DetectActiveContentController();

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
            _activationDelay = ActivationDelaySeconds;
            _playBladeActive = false;
            _playBladeState = null;

            // Reset helpers
            _screenDetector.Reset();
            _panelTracker.Reset();

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
            _playBladeActive = false;
            _playBladeState = null;

            // Reset helpers
            _screenDetector.Reset();
            _panelTracker.Reset();
        }

        /// <summary>
        /// Called when navigator activates.
        /// </summary>
        protected override void OnActivated()
        {
            base.OnActivated();
            _elementCountAtActivation = _elements.Count;
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

            // Check for new popups - this detects popups that appear after button clicks
            // (e.g., InviteFriendPopup appearing after clicking Add Friend)
            if (_rescanDelay <= 0 && _panelTracker.CheckForNewPopups())
            {
                MelonLogger.Msg($"[{NavigatorId}] New popup detected, triggering rescan");
                TriggerRescan(force: true);
            }

            // Call base Update for normal navigation handling
            base.Update();
        }

        /// <summary>
        /// Handle custom input keys. Backspace navigates back to Home when in a content panel.
        /// F4 toggles the Friends panel.
        /// </summary>
        protected override bool HandleCustomInput()
        {
            // F4: Toggle Friends panel
            if (Input.GetKeyDown(KeyCode.F4))
            {
                MelonLogger.Msg($"[{NavigatorId}] F4 pressed - toggling Friends panel");
                ToggleFriendsPanel();
                return true;
            }

            // Backspace: Navigate back to Home (main menu) or close Friends panel
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                // If Friends panel is open, close it first
                if (IsSocialPanelOpen())
                {
                    ToggleFriendsPanel();
                    return true;
                }

                bool isInContentPanel = !string.IsNullOrEmpty(_activeContentController) &&
                                        _activeContentController != "HomePageContentController";
                if (isInContentPanel)
                {
                    return NavigateToHome();
                }
            }

            return false;
        }

        /// <summary>
        /// Toggle the Friends/Social panel by calling SocialUI methods directly.
        /// </summary>
        private void ToggleFriendsPanel()
        {
            MelonLogger.Msg($"[{NavigatorId}] ToggleFriendsPanel called");

            var socialPanel = GameObject.Find("SocialUI_V2_Desktop_16x9(Clone)");
            if (socialPanel == null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Social UI panel not found");
                return;
            }

            // Get the SocialUI component
            var socialUI = socialPanel.GetComponent("SocialUI");
            if (socialUI == null)
            {
                MelonLogger.Msg($"[{NavigatorId}] SocialUI component not found");
                return;
            }

            bool isOpen = IsSocialPanelOpen();
            MelonLogger.Msg($"[{NavigatorId}] Toggling Friends panel (isOpen: {isOpen})");

            try
            {
                if (isOpen)
                {
                    // Close the panel
                    var closeMethod = socialUI.GetType().GetMethod("CloseFriendsWidget",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (closeMethod != null)
                    {
                        closeMethod.Invoke(socialUI, null);
                        MelonLogger.Msg($"[{NavigatorId}] Called SocialUI.CloseFriendsWidget()");
                    }
                }
                else
                {
                    // Open the panel
                    var showMethod = socialUI.GetType().GetMethod("ShowSocialEntitiesList",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (showMethod != null)
                    {
                        showMethod.Invoke(socialUI, null);
                        MelonLogger.Msg($"[{NavigatorId}] Called SocialUI.ShowSocialEntitiesList()");
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[{NavigatorId}] Error toggling Friends panel: {ex.Message}");
            }
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
                _announcer.Announce(Models.Strings.CannotNavigateHome, Models.AnnouncementPriority.High);
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
                _announcer.Announce(Models.Strings.HomeNotAvailable, Models.AnnouncementPriority.High);
                return true;
            }

            MelonLogger.Msg($"[{NavigatorId}] Navigating to Home via Backspace");
            _announcer.Announce(Models.Strings.ReturningHome, Models.AnnouncementPriority.High);

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
                    if (CheckSettingsMenuOpen())
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
        /// </summary>
        private List<(string name, GameObject obj)> GetActivePanelsWithObjects() =>
            _panelTracker.GetActivePanelsWithObjects(_screenDetector);

        /// <summary>
        /// Check if a panel name represents an overlay that should filter elements.
        /// </summary>
        private static bool IsOverlayPanel(string panelName) =>
            MenuPanelTracker.IsOverlayPanel(panelName);

        /// <summary>
        /// Check if element should be shown based on current panel state.
        /// Uses the active content controller to determine visibility.
        /// </summary>
        private bool IsInForegroundPanel(GameObject obj)
        {
            // Settings overlay takes highest priority - only show Settings elements
            if (_foregroundPanel != null && CheckSettingsMenuOpen())
            {
                return IsChildOf(obj, _foregroundPanel);
            }

            // Social panel overlay - only show Social elements when open
            if (_foregroundPanel != null && IsSocialPanelOpen())
            {
                return IsChildOf(obj, _foregroundPanel);
            }

            // If no active controller detected, show all elements
            if (_activeControllerGameObject == null)
                return true;

            // Special case: HomePage with PlayBlade active
            // Only show blade elements, hide carousel/other HomePage content and NavBar
            if (_activeContentController == "HomePageContentController" && _playBladeActive)
            {
                return IsInsideBlade(obj);
            }

            // HomePage without PlayBlade: show HomePage content + NavBar
            if (_activeContentController == "HomePageContentController")
            {
                if (IsChildOf(obj, _activeControllerGameObject))
                    return true;
                if (_navBarGameObject != null && IsChildOf(obj, _navBarGameObject))
                    return true;
                return false;
            }

            // Special case: Color Challenge (CampaignGraphContentController)
            if (_activeContentController == "CampaignGraphContentController")
            {
                if (IsChildOf(obj, _activeControllerGameObject))
                    return true;
                if (IsInsideBlade(obj))
                    return true;
                if (IsMainButton(obj))
                    return true;
                return false;
            }

            // Any other content controller (Decks, Store, Profile, etc.):
            // Show ONLY that controller's elements, hide NavBar
            return IsChildOf(obj, _activeControllerGameObject);
        }

        /// <summary>
        /// Check if a GameObject is a child of (or the same as) a parent GameObject.
        /// </summary>
        private static bool IsChildOf(GameObject child, GameObject parent) =>
            MenuPanelTracker.IsChildOf(child, parent);

        /// <summary>
        /// Check if element is inside a Blade (PlayBlade, EventBlade, FindMatchBlade, etc.)
        /// Used for filtering HomePage when PlayBlade is active.
        /// </summary>
        private bool IsInsideBlade(GameObject obj)
        {
            Transform current = obj.transform;

            while (current != null)
            {
                string name = current.gameObject.name;

                foreach (var pattern in BladePatterns)
                {
                    if (name.Contains(pattern))
                        return true;
                }

                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// Check if element is the HomePage's MainButton (Play button).
        /// </summary>
        private bool IsMainButton(GameObject obj)
        {
            if (obj == null) return false;

            string name = obj.name;

            // MainButtonOutline is the play button in Color Challenge mode
            if (name == "MainButtonOutline")
                return true;

            // MainButton is the normal play button with MainButton component
            if (name == "MainButton")
            {
                var components = obj.GetComponents<MonoBehaviour>();
                foreach (var comp in components)
                {
                    if (comp != null && comp.GetType().Name == "MainButton")
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Schedule a rescan after a short delay to let UI settle.
        /// </summary>
        /// <param name="force">If true, bypasses the debounce check (used for toggle activations)</param>
        private void TriggerRescan(bool force = false)
        {
            _rescanDelay = RescanDelaySeconds;
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
            if (!_forceRescan && currentTime - _lastRescanTime < RescanDebounceSeconds)
            {
                MelonLogger.Msg($"[{NavigatorId}] Skipping rescan - debounce active");
                return;
            }
            _lastRescanTime = currentTime;
            _forceRescan = false; // Reset force flag

            // Detect active controller BEFORE discovering elements so filtering works correctly
            DetectActiveContentController();
            MelonLogger.Msg($"[{NavigatorId}] Rescanning elements after panel change (controller: {_activeContentController ?? "none"})");

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
            return GetActiveCustomButtons().Count();
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
            foreach (var buttonObj in GetActiveCustomButtons())
            {
                string objName = buttonObj.name;
                string text = UITextExtractor.GetText(buttonObj);

                foreach (var pattern in patterns)
                {
                    if (objName.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return buttonObj;
                    if (!string.IsNullOrEmpty(text) && text.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return buttonObj;
                }
            }
            return null;
        }

        protected virtual void LogAvailableUIElements()
        {
            MelonLogger.Msg($"[{NavigatorId}] === UI DUMP FOR {_currentScene} ===");

            // Find all CustomButtons
            var customButtons = GetActiveCustomButtons()
                .Select(obj => (obj, text: UITextExtractor.GetText(obj) ?? "(no text)", path: GetGameObjectPath(obj)))
                .ToList();

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

            // Find CustomToggle components (game-specific toggle type)
            var customToggles = GameObject.FindObjectsOfType<MonoBehaviour>()
                .Where(mb => mb != null && mb.gameObject.activeInHierarchy &&
                       mb.GetType().Name.Contains("CustomToggle"))
                .ToList();
            MelonLogger.Msg($"[{NavigatorId}] Found {customToggles.Count} CustomToggle components:");
            foreach (var ct in customToggles)
            {
                string text = UITextExtractor.GetText(ct.gameObject);
                string path = GetGameObjectPath(ct.gameObject);
                var toggle = ct.gameObject.GetComponent<Toggle>();
                string toggleState = toggle != null ? (toggle.isOn ? "ON" : "OFF") : "no Toggle component";
                MelonLogger.Msg($"[{NavigatorId}]   {path} - '{text ?? "(no text)"}' - {toggleState}");
            }

            // Find Scrollbars
            var scrollbars = GameObject.FindObjectsOfType<Scrollbar>()
                .Where(sb => sb != null && sb.gameObject.activeInHierarchy)
                .ToList();
            MelonLogger.Msg($"[{NavigatorId}] Found {scrollbars.Count} Scrollbars:");
            foreach (var sb in scrollbars)
            {
                string path = GetGameObjectPath(sb.gameObject);
                MelonLogger.Msg($"[{NavigatorId}]   {path} - value: {sb.value:F2}, size: {sb.size:F2}, interactable: {sb.interactable}");
            }

            // Find ScrollRect components
            var scrollRects = GameObject.FindObjectsOfType<ScrollRect>()
                .Where(sr => sr != null && sr.gameObject.activeInHierarchy)
                .ToList();
            MelonLogger.Msg($"[{NavigatorId}] Found {scrollRects.Count} ScrollRect components:");
            foreach (var sr in scrollRects)
            {
                string path = GetGameObjectPath(sr.gameObject);
                MelonLogger.Msg($"[{NavigatorId}]   {path} - vertical: {sr.vertical}, horizontal: {sr.horizontal}");
            }

            // Find standard Unity Toggles
            var toggles = GameObject.FindObjectsOfType<Toggle>()
                .Where(t => t != null && t.gameObject.activeInHierarchy)
                .ToList();
            MelonLogger.Msg($"[{NavigatorId}] Found {toggles.Count} standard Unity Toggles:");
            foreach (var t in toggles)
            {
                string text = UITextExtractor.GetText(t.gameObject);
                string path = GetGameObjectPath(t.gameObject);
                MelonLogger.Msg($"[{NavigatorId}]   {path} - '{text ?? "(no text)"}' - {(t.isOn ? "ON" : "OFF")} - interactable: {t.interactable}");
            }

            MelonLogger.Msg($"[{NavigatorId}] === END UI DUMP ===");
        }

        protected override void DiscoverElements()
        {
            // Detect active controller first so filtering works correctly
            DetectActiveContentController();

            var addedObjects = new HashSet<GameObject>();
            var discoveredElements = new List<(GameObject obj, UIElementClassifier.ClassificationResult classification, float sortOrder)>();

            // Log panel filter state
            if (_foregroundPanel != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Filtering to panel: {_foregroundPanel.name}");
            }
            else if (_activeControllerGameObject != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Filtering to controller: {_activeContentController}");
            }

            // Helper to process and classify a UI element
            void TryAddElement(GameObject obj)
            {
                if (obj == null || !obj.activeInHierarchy) return;
                if (addedObjects.Contains(obj)) return;
                if (!IsInForegroundPanel(obj)) return;

                var classification = UIElementClassifier.Classify(obj);
                if (classification.IsNavigable && classification.ShouldAnnounce)
                {
                    var pos = obj.transform.position;
                    float sortOrder = -pos.y * 1000 + pos.x;
                    discoveredElements.Add((obj, classification, sortOrder));
                    addedObjects.Add(obj);
                }
            }

            // Find CustomButtons (MTGA's primary button component)
            foreach (var buttonObj in GetActiveCustomButtons())
            {
                TryAddElement(buttonObj);
            }

            // Find standard Unity UI elements
            foreach (var btn in GameObject.FindObjectsOfType<Button>())
            {
                if (btn != null && btn.interactable)
                    TryAddElement(btn.gameObject);
            }

            foreach (var trigger in GameObject.FindObjectsOfType<EventTrigger>())
            {
                if (trigger != null)
                    TryAddElement(trigger.gameObject);
            }

            foreach (var toggle in GameObject.FindObjectsOfType<Toggle>())
            {
                if (toggle != null && toggle.interactable)
                    TryAddElement(toggle.gameObject);
            }

            foreach (var slider in GameObject.FindObjectsOfType<Slider>())
            {
                if (slider != null && slider.interactable)
                    TryAddElement(slider.gameObject);
            }

            // Find TMP_InputField elements (text input fields)
            foreach (var inputField in GameObject.FindObjectsOfType<TMP_InputField>())
            {
                if (inputField != null && inputField.interactable)
                    TryAddElement(inputField.gameObject);
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

            // On MatchEndScene, auto-focus the Continue button (ExitMatchOverlayButton)
            AutoFocusContinueButton();
        }

        /// <summary>
        /// Auto-focus the Continue button on MatchEndScene for better UX.
        /// The Continue button should be the first thing focused after a match ends.
        /// </summary>
        private void AutoFocusContinueButton()
        {
            // Find the ExitMatchOverlayButton (Continue) in our elements
            for (int i = 0; i < _elements.Count; i++)
            {
                if (_elements[i].GameObject != null &&
                    _elements[i].GameObject.name == "ExitMatchOverlayButton")
                {
                    // Move this element to the front by swapping with index 0
                    if (i > 0)
                    {
                        var continueButton = _elements[i];
                        _elements.RemoveAt(i);
                        _elements.Insert(0, continueButton);
                        MelonLogger.Msg($"[{NavigatorId}] Moved Continue button to first position");
                    }
                    _currentIndex = 0;
                    break;
                }
            }
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

            // Check if this is an input field - may cause UI changes (e.g., Send button appearing)
            bool isInputField = element.GetComponent<TMP_InputField>() != null;

            // Use UIActivator for CustomButtons
            var result = UIActivator.Activate(element);
            _announcer.Announce(result.Message, Models.AnnouncementPriority.Normal);

            // For Toggle activations (checkboxes), force a rescan after a delay
            // This handles deck folder filtering where Selectable count may not change
            // but the visible decks do change
            // Use force=true to bypass debounce since user explicitly toggled a filter
            if (isToggle)
            {
                MelonLogger.Msg($"[{NavigatorId}] Toggle activated - forcing rescan in {RescanDelaySeconds}s (bypassing debounce)");
                TriggerRescan(force: true);
                return true;
            }

            // For input field activations, trigger a rescan after a short delay
            // The UI might change (e.g., Send button appearing) when entering an input field
            if (isInputField)
            {
                MelonLogger.Msg($"[{NavigatorId}] Input field activated - scheduling rescan");
                TriggerRescan(force: true);
                return true;
            }

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

            // Route to appropriate handler based on panel type
            if (IsContentPanelType(panelTypeName))
            {
                HandleContentPanelChange(panelTypeName, isOpen);
            }
            else if (IsBladeType(panelTypeName))
            {
                HandleBladeChange(panelTypeName, isOpen);
            }

            // Trigger rescan when panel opens or closes
            TriggerRescan();
        }

        /// <summary>
        /// Check if the panel type is a content panel (non-HomePage content controllers, Settings, etc.)
        /// </summary>
        private bool IsContentPanelType(string panelTypeName)
        {
            // Check for known content panel patterns (excluding HomePage which is always there)
            if (panelTypeName.Contains("HomePage") || panelTypeName.Contains("HomeContent"))
                return false;

            // Check for content controller patterns
            return MenuScreenDetector.IsContentControllerType(panelTypeName) ||
                   panelTypeName.Contains("SettingsMenu") ||
                   panelTypeName.Contains("DeckSelectBlade") ||
                   panelTypeName.Contains("SocialUI");
        }

        /// <summary>
        /// Check if the panel type is a blade (PlayBlade, EventBlade, etc.)
        /// </summary>
        private bool IsBladeType(string panelTypeName)
        {
            return panelTypeName.Contains("PlayBlade") ||
                   panelTypeName.Contains("Blade:") ||
                   panelTypeName.Contains("EventBlade") ||
                   panelTypeName.Contains("DirectChallengeBlade");
        }

        /// <summary>
        /// Handle content panel open/close events.
        /// </summary>
        private void HandleContentPanelChange(string panelTypeName, bool isOpen)
        {
            if (isOpen)
            {
                _activePanels.Add($"ContentPanel:{panelTypeName}");
                MelonLogger.Msg($"[{NavigatorId}] Content panel opened: {panelTypeName}");

                // For SettingsMenu, set foreground panel to filter elements correctly
                if (panelTypeName.Contains("SettingsMenu"))
                {
                    // Update cached Settings panel reference and set as foreground
                    if (CheckSettingsMenuOpen() && SettingsContentPanel != null)
                    {
                        _foregroundPanel = SettingsContentPanel;
                        MelonLogger.Msg($"[{NavigatorId}] Set foreground panel to Settings: {_foregroundPanel.name}");
                    }
                }

                // For SocialUI (friends panel), find and set as foreground
                if (panelTypeName.Contains("SocialUI"))
                {
                    var socialPanel = GameObject.Find("SocialUI_V2_Desktop_16x9(Clone)");
                    if (socialPanel != null)
                    {
                        _foregroundPanel = socialPanel;
                        MelonLogger.Msg($"[{NavigatorId}] Set foreground panel to Social: {_foregroundPanel.name}");
                    }
                }

                // Auto-expand blade for Color Challenge menu
                if (panelTypeName.Contains("CampaignGraphContentController"))
                {
                    _bladeAutoExpandDelay = BladeAutoExpandDelay;
                    MelonLogger.Msg($"[{NavigatorId}] Scheduling blade auto-expand for Color Challenge");
                }
            }
            else
            {
                _activePanels.RemoveWhere(p => p.StartsWith("ContentPanel:"));
                _foregroundPanel = null;
                MelonLogger.Msg($"[{NavigatorId}] Content panel closed: {panelTypeName}");
            }
        }

        /// <summary>
        /// Handle blade open/close/state change events.
        /// </summary>
        private void HandleBladeChange(string panelTypeName, bool isOpen)
        {
            if (isOpen)
            {
                string panelKey = panelTypeName.Contains("EventBlade") || panelTypeName.Contains("DirectChallenge")
                    ? $"EventBlade:External:{panelTypeName}"
                    : $"PlayBlade:External:{panelTypeName}";

                _activePanels.Add(panelKey);
                _playBladeActive = true;

                // Determine screen name state
                if (panelTypeName.Contains("DirectChallenge"))
                    _playBladeState = "PlayBlade:DirectChallenge";
                else if (panelTypeName.Contains("EventBlade"))
                    _playBladeState = "PlayBlade:Events";
                else
                    _playBladeState = panelTypeName;

                MelonLogger.Msg($"[{NavigatorId}] Blade opened: {panelTypeName}");
            }
            else
            {
                // Only fully close if it's a Hide/Hidden state, otherwise it's a state transition
                bool isClosing = panelTypeName.Contains("Hidden") || panelTypeName.Contains("Hide");

                if (isClosing)
                {
                    _activePanels.RemoveWhere(p => p.Contains("Blade:"));
                    _playBladeActive = false;
                    _playBladeState = null;
                    _foregroundPanel = null;
                    MelonLogger.Msg($"[{NavigatorId}] Blade closed: {panelTypeName}");
                }
                else
                {
                    // State transition within blade - keep blade active
                    _playBladeState = panelTypeName;
                    MelonLogger.Msg($"[{NavigatorId}] Blade state change: {panelTypeName}");
                }
            }
        }

        /// <summary>
        /// Auto-expand the play blade when it's in collapsed state.
        /// Used for panels like Color Challenge where the blade starts collapsed.
        /// </summary>
        private void AutoExpandBlade()
        {
            MelonLogger.Msg($"[{NavigatorId}] Attempting blade auto-expand");

            // Find the blade expand button (Btn_BladeIsClosed or its arrow child)
            var bladeButton = GetActiveCustomButtons()
                .FirstOrDefault(obj => obj.name.Contains("BladeHoverClosed") || obj.name.Contains("Btn_BladeIsClosed"));

            if (bladeButton != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Found blade expand button: {bladeButton.name}");
            }

            if (bladeButton != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Auto-expanding blade via {bladeButton.name}");
                _announcer.Announce(Models.Strings.OpeningColorChallenges, Models.AnnouncementPriority.High);
                UIActivator.Activate(bladeButton);

                // Schedule a rescan after the blade opens
                TriggerRescan();
            }
            else
            {
                MelonLogger.Msg($"[{NavigatorId}] Could not find blade expand button");
            }
        }

        #region Screen Detection Helpers

        /// <summary>
        /// Detect which content controller is currently active.
        /// Delegates to MenuScreenDetector.
        /// </summary>
        private string DetectActiveContentController() => _screenDetector.DetectActiveContentController();

        /// <summary>
        /// Check if the promotional carousel is visible on the home screen.
        /// </summary>
        private bool HasVisibleCarousel()
        {
            bool hasCarouselElement = _elements.Any(e => e.Carousel.HasArrowNavigation);
            return _screenDetector.HasVisibleCarousel(hasCarouselElement);
        }

        /// <summary>
        /// Check if Color Challenge content is visible.
        /// </summary>
        private bool HasColorChallengeVisible() =>
            _screenDetector.HasColorChallengeVisible(GetActiveCustomButtons, GetGameObjectPath);

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
        private string GetContentControllerDisplayName(string controllerTypeName) =>
            _screenDetector.GetContentControllerDisplayName(controllerTypeName);

        #endregion
    }
}
