using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace AccessibleArena.Core.Services
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
            "Bootstrap", "AssetPrep", "DuelScene", "DraftScene", "SealedScene"
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
        private float _bladeAutoExpandDelay;

        // Blade state tracking
        private bool _playBladeActive;
        private string _playBladeState;

        // Login panel waiting - track when panel became inactive (browser may be showing Terms)
        // Wait indefinitely for panel to return or different panel to appear
        private float _loginPanelInactiveTime;

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

            // Check for NPE rewards screen (card unlocked)
            // This is checked separately because we don't filter elements to it
            if (_screenDetector.IsNPERewardsScreenActive())
            {
                return "Card Unlocked";
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
            // (e.g., InviteFriendPopup appearing after clicking Add Friend, SystemMessageView for confirmations)
            if (_rescanDelay <= 0)
            {
                bool newPopup = _panelTracker.CheckForNewPopups();
                var trackerForeground = _panelTracker.ForegroundPanel;

                // Sync foreground panel from tracker if it changed (popup opened or closed)
                if (trackerForeground != _foregroundPanel && IsPopupOverlay(trackerForeground))
                {
                    _foregroundPanel = trackerForeground;
                    MelonLogger.Msg($"[{NavigatorId}] Popup foreground changed: {_foregroundPanel?.name ?? "null"}, triggering rescan");
                    TriggerRescan(force: true);
                }
                else if (newPopup)
                {
                    _foregroundPanel = trackerForeground;
                    MelonLogger.Msg($"[{NavigatorId}] New popup detected, foreground: {_foregroundPanel?.name ?? "null"}, triggering rescan");
                    TriggerRescan(force: true);
                }
                // Clear popup foreground if popup closed
                else if (_foregroundPanel != null && IsPopupOverlay(_foregroundPanel) && !_foregroundPanel.activeInHierarchy)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Popup closed: {_foregroundPanel.name}, clearing foreground");
                    _foregroundPanel = null;
                    TriggerRescan(force: true);
                }
            }

            // Check for panel changes (e.g., Settings submenu navigation)
            // This detects when foreground panel becomes inactive or changes
            // Skip during popup cooldown to avoid overriding restored foreground
            if (_rescanDelay <= 0 && !_panelTracker.IsPopupCooldownActive)
            {
                CheckForPanelChanges();
            }

            // Call base Update for normal navigation handling
            base.Update();
        }

        /// <summary>
        /// Handle custom input keys. Backspace goes back one level in menus.
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

            // F12: Debug dump of current UI hierarchy (for development)
            if (Input.GetKeyDown(KeyCode.F12))
            {
                DumpUIHierarchy();
                return true;
            }

            // Backspace: Universal back - goes back one level in menus
            // But NOT when an input field is focused - let Backspace delete characters
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                if (UIFocusTracker.IsAnyInputFieldFocused())
                {
                    MelonLogger.Msg($"[{NavigatorId}] Backspace pressed but input field focused - passing through");
                    return false; // Let it pass through to the input field
                }
                return HandleBackNavigation();
            }

            // Escape: Exit input field if one is focused (but user didn't enter via our navigation)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (UIFocusTracker.IsAnyInputFieldFocused())
                {
                    MelonLogger.Msg($"[{NavigatorId}] Escape pressed while input field focused - deactivating field");
                    UIFocusTracker.DeactivateFocusedInputField();
                    _announcer.Announce("Exited input field", Models.AnnouncementPriority.Normal);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Handle Backspace key - navigates back one level in the menu hierarchy.
        /// Priority order:
        /// 1. Settings submenu → Settings main menu
        /// 2. Settings main menu → Close settings
        /// 3. Friends panel open → Close it
        /// 4. PlayBlade open → Close it
        /// 5. In content panel (not Home) → Navigate to Home
        /// </summary>
        private bool HandleBackNavigation()
        {
            MelonLogger.Msg($"[{NavigatorId}] Backspace pressed - handling back navigation");

            // 1. If Settings is open, handle Settings navigation
            if (CheckSettingsMenuOpen())
            {
                return HandleSettingsBack();
            }

            // 2. If Friends panel is open, close it
            if (IsSocialPanelOpen())
            {
                MelonLogger.Msg($"[{NavigatorId}] Closing Friends panel");
                ToggleFriendsPanel();
                return true;
            }

            // 3. If PlayBlade is open, close it
            if (_playBladeActive)
            {
                return ClosePlayBlade();
            }

            // 4. If in a content panel (not Home), navigate to Home
            bool isInContentPanel = !string.IsNullOrEmpty(_activeContentController) &&
                                    _activeContentController != "HomePageContentController";
            if (isInContentPanel)
            {
                return NavigateToHome();
            }

            // 5. Try to find a generic back button on screen (icon buttons named *Back*)
            var genericBackButton = FindGenericBackButton();
            if (genericBackButton != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Found generic back button: {genericBackButton.name}");
                _announcer.Announce(Models.Strings.NavigatingBack, Models.AnnouncementPriority.High);
                ActivateBackButton(genericBackButton);
                return true;
            }

            // 6. Already at top level, nothing to do
            MelonLogger.Msg($"[{NavigatorId}] Already at top level, no back action");
            return false;
        }

        /// <summary>
        /// Find a generic back button on the current screen.
        /// Looks for icon buttons (no text) with "Back" in the name.
        /// </summary>
        private GameObject FindGenericBackButton()
        {
            // Look for CustomButtons with "Back" in name that have no actual text (icon buttons)
            // Note: GetActiveCustomButtons() already filters for activeInHierarchy
            foreach (var btn in GetActiveCustomButtons())
            {
                if (btn == null) continue;

                if (btn.name.IndexOf("back", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                    !UITextExtractor.HasActualText(btn))
                {
                    return btn;
                }
            }

            // Also check standard Unity Buttons
            foreach (var btn in GameObject.FindObjectsOfType<Button>())
            {
                if (btn == null || !btn.gameObject.activeInHierarchy || !btn.interactable) continue;

                string btnName = btn.gameObject.name;
                if (btnName.IndexOf("back", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                    !UITextExtractor.HasActualText(btn.gameObject))
                {
                    return btn.gameObject;
                }
            }

            return null;
        }

        /// <summary>
        /// Handle back navigation within Settings menu.
        /// If in a submenu (Audio, Graphics, Gameplay), navigate back to main menu.
        /// If in main menu, close Settings entirely.
        /// </summary>
        private bool HandleSettingsBack()
        {
            var settingsPanel = SettingsContentPanel;
            if (settingsPanel == null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Settings panel not found");
                return false;
            }

            string panelName = settingsPanel.name;
            MelonLogger.Msg($"[{NavigatorId}] Settings back from panel: {panelName}");

            // Check if we're in a submenu (not the main menu)
            bool isInSubmenu = panelName != "Content - MainMenu" &&
                              (panelName == "Content - Audio" ||
                               panelName == "Content - Graphics" ||
                               panelName == "Content - Gameplay");

            if (isInSubmenu)
            {
                // Navigate back to main Settings menu by clicking the back button
                // Find the BackButton in the current submenu
                var backButton = FindSettingsBackButton(settingsPanel);
                if (backButton != null)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Activating Settings submenu back button");
                    _announcer.Announce(Models.Strings.NavigatingBack, Models.AnnouncementPriority.High);

                    // Try multiple activation methods for better compatibility
                    ActivateBackButton(backButton);
                    return true;
                }
                else
                {
                    MelonLogger.Msg($"[{NavigatorId}] Could not find Settings submenu back button");
                }
            }

            // Close Settings entirely via SettingsMenu.Close()
            return CloseSettingsMenu();
        }

        /// <summary>
        /// Find the back button within a Settings panel.
        /// </summary>
        private GameObject FindSettingsBackButton(GameObject settingsPanel)
        {
            // BackButton is typically at: Content - X/Header/Back/BackButton
            var headerTransform = settingsPanel.transform.Find("Header");
            if (headerTransform == null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Header not found in {settingsPanel.name}");
                return null;
            }

            var backContainer = headerTransform.Find("Back");
            if (backContainer == null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Back container not found in Header");
                return null;
            }

            var backButton = backContainer.Find("BackButton");
            if (backButton != null && backButton.gameObject.activeInHierarchy)
            {
                return backButton.gameObject;
            }

            MelonLogger.Msg($"[{NavigatorId}] BackButton not found or not active");
            return null;
        }

        /// <summary>
        /// Activate a back button using multiple methods for better compatibility.
        /// Some buttons respond to pointer events, others to onClick, others to Button.onClick.
        /// </summary>
        private void ActivateBackButton(GameObject backButton)
        {
            // Method 1: Check for standard Unity Button component first
            var unityButton = backButton.GetComponent<Button>();
            if (unityButton != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Activating via Unity Button.onClick");
                unityButton.onClick.Invoke();
                return;
            }

            // Method 2: Try to find and invoke OnPointerClick directly
            var pointerClickHandlers = backButton.GetComponents<IPointerClickHandler>();
            if (pointerClickHandlers.Length > 0)
            {
                var pointer = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                    pointerPress = backButton
                };

                foreach (var handler in pointerClickHandlers)
                {
                    // Skip TooltipTrigger as it's just for tooltips
                    if (handler.GetType().Name != "TooltipTrigger")
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Invoking IPointerClickHandler: {handler.GetType().Name}");
                        handler.OnPointerClick(pointer);
                        return;
                    }
                }
            }

            // Method 3: Try Animator triggers via reflection (some buttons use animation-based navigation)
            foreach (var component in backButton.GetComponents<Component>())
            {
                if (component == null) continue;
                var typeName = component.GetType().Name;
                if (typeName == "Animator")
                {
                    MelonLogger.Msg($"[{NavigatorId}] Trying Animator triggers on: {backButton.name}");
                    var type = component.GetType();

                    // Try SetTrigger with common trigger names
                    var setTrigger = type.GetMethod("SetTrigger", new[] { typeof(string) });
                    if (setTrigger != null)
                    {
                        setTrigger.Invoke(component, new object[] { "Pressed" });
                        setTrigger.Invoke(component, new object[] { "Click" });
                        setTrigger.Invoke(component, new object[] { "Selected" });
                    }

                    // Try Play method
                    var play = type.GetMethod("Play", new[] { typeof(string) });
                    if (play != null)
                    {
                        play.Invoke(component, new object[] { "Pressed" });
                    }

                    MelonLogger.Msg($"[{NavigatorId}] Animator triggers sent");
                    break;
                }
            }

            // Method 4: Fall back to UIActivator (comprehensive approach)
            MelonLogger.Msg($"[{NavigatorId}] Falling back to UIActivator.Activate");
            UIActivator.Activate(backButton);
        }

        /// <summary>
        /// Close the Settings menu by calling SettingsMenu.Close() directly.
        /// </summary>
        private bool CloseSettingsMenu()
        {
            MelonLogger.Msg($"[{NavigatorId}] Attempting to close Settings menu via SettingsMenu.Close()");

            // Find the SettingsMenu component
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || mb.GetType().Name != "SettingsMenu")
                    continue;

                // Call the Close method
                var closeMethod = mb.GetType().GetMethod("Close",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (closeMethod != null)
                {
                    try
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Calling SettingsMenu.Close()");
                        _announcer.Announce(Models.Strings.ClosingSettings, Models.AnnouncementPriority.High);
                        closeMethod.Invoke(mb, null);
                        return true;
                    }
                    catch (System.Exception ex)
                    {
                        MelonLogger.Warning($"[{NavigatorId}] Error calling SettingsMenu.Close(): {ex.Message}");
                    }
                }
            }

            MelonLogger.Warning($"[{NavigatorId}] Could not find SettingsMenu.Close() method");
            return false;
        }

        /// <summary>
        /// Close the PlayBlade by finding and activating the dismiss button.
        /// Immediately clears blade state and triggers rescan so user can navigate
        /// Home elements right away, even while the blade animation is still playing.
        /// </summary>
        private bool ClosePlayBlade()
        {
            MelonLogger.Msg($"[{NavigatorId}] Attempting to close PlayBlade");

            // Method 1: Find the Blade_DismissButton
            var dismissButton = GameObject.Find("Blade_DismissButton");
            if (dismissButton != null && dismissButton.activeInHierarchy)
            {
                MelonLogger.Msg($"[{NavigatorId}] Found Blade_DismissButton, activating");
                _announcer.Announce(Models.Strings.ClosingPlayBlade, Models.AnnouncementPriority.High);
                UIActivator.Activate(dismissButton);

                // Immediately clear blade state and rescan - don't wait for animation
                ClearBladeStateAndRescan();
                return true;
            }

            // Method 2: Search for dismiss button in PlayBlade hierarchy
            var playBladeController = GameObject.Find("ContentController - PlayBladeV3_Desktop_16x9(Clone)");
            if (playBladeController != null)
            {
                var dismissTransform = playBladeController.transform.Find("SafeArea/Popout/BackButton_CONTAINER/Blade_DismissButton");
                if (dismissTransform != null && dismissTransform.gameObject.activeInHierarchy)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Found Blade_DismissButton via path, activating");
                    _announcer.Announce(Models.Strings.ClosingPlayBlade, Models.AnnouncementPriority.High);
                    UIActivator.Activate(dismissTransform.gameObject);

                    // Immediately clear blade state and rescan - don't wait for animation
                    ClearBladeStateAndRescan();
                    return true;
                }
            }

            // Method 3: Try to find BladeContentView and call Hide
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;

                string typeName = mb.GetType().Name;
                if (!typeName.Contains("BladeContentView") && !typeName.Contains("PlayBladeController"))
                    continue;

                // Try Hide method
                var hideMethod = mb.GetType().GetMethod("Hide",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (hideMethod != null)
                {
                    try
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Calling {typeName}.Hide()");
                        _announcer.Announce(Models.Strings.ClosingPlayBlade, Models.AnnouncementPriority.High);
                        hideMethod.Invoke(mb, null);

                        // Immediately clear blade state and rescan - don't wait for animation
                        ClearBladeStateAndRescan();
                        return true;
                    }
                    catch (System.Exception ex)
                    {
                        MelonLogger.Warning($"[{NavigatorId}] Error calling {typeName}.Hide(): {ex.Message}");
                    }
                }
            }

            MelonLogger.Warning($"[{NavigatorId}] Could not close PlayBlade");
            return false;
        }

        /// <summary>
        /// Clear blade state and trigger immediate rescan.
        /// Called when user presses Backspace to close PlayBlade.
        /// This allows navigation of Home elements while blade animation plays.
        /// </summary>
        private void ClearBladeStateAndRescan()
        {
            MelonLogger.Msg($"[{NavigatorId}] Clearing blade state for immediate Home navigation");
            _playBladeActive = false;
            _playBladeState = null;
            _activePanels.RemoveWhere(p => p.Contains("Blade:"));
            _foregroundPanel = null;
            TriggerRescan(force: true);
        }

        /// <summary>
        /// Debug: Dump current UI hierarchy to log for development.
        /// Press F12 to trigger this after opening a new UI overlay.
        /// </summary>
        private void DumpUIHierarchy()
        {
            MelonLogger.Msg($"[{NavigatorId}] === F12 DEBUG: UI HIERARCHY DUMP ===");

            // Find all active Canvases
            var canvases = GameObject.FindObjectsOfType<Canvas>();
            MelonLogger.Msg($"[{NavigatorId}] Found {canvases.Length} active Canvases");

            foreach (var canvas in canvases)
            {
                if (canvas == null || !canvas.gameObject.activeInHierarchy)
                    continue;

                MelonLogger.Msg($"[{NavigatorId}] Canvas: {canvas.name} (sortingOrder: {canvas.sortingOrder})");

                // Dump first 2 levels of children
                DumpGameObjectChildren(canvas.gameObject, 1, 3);
            }

            // Also check for any panel controllers that might be open
            MelonLogger.Msg($"[{NavigatorId}] === Checking Panel Controllers ===");
            var allMonoBehaviours = GameObject.FindObjectsOfType<MonoBehaviour>();
            foreach (var mb in allMonoBehaviours)
            {
                if (mb == null || !mb.gameObject.activeInHierarchy)
                    continue;

                string typeName = mb.GetType().Name;
                // Look for potential panel/controller types
                if (typeName.Contains("Controller") || typeName.Contains("Panel") ||
                    typeName.Contains("Overlay") || typeName.Contains("Browser") ||
                    typeName.Contains("Popup") || typeName.Contains("Viewer"))
                {
                    // Check if it has IsOpen property
                    var isOpenProp = mb.GetType().GetProperty("IsOpen");
                    string isOpenStr = "";
                    if (isOpenProp != null)
                    {
                        try
                        {
                            bool isOpen = (bool)isOpenProp.GetValue(mb);
                            isOpenStr = $" (IsOpen: {isOpen})";
                        }
                        catch { }
                    }

                    MelonLogger.Msg($"[{NavigatorId}]   {typeName} on {mb.gameObject.name}{isOpenStr}");
                }
            }

            MelonLogger.Msg($"[{NavigatorId}] === END DEBUG DUMP ===");
            _announcer.Announce("Debug dump complete. Check log.", Models.AnnouncementPriority.High);
        }

        private void DumpGameObjectChildren(GameObject parent, int currentDepth, int maxDepth)
        {
            if (currentDepth > maxDepth || parent == null)
                return;

            string indent = new string(' ', currentDepth * 2);

            foreach (Transform child in parent.transform)
            {
                if (child == null || !child.gameObject.activeInHierarchy)
                    continue;

                // Get component summary
                var components = child.GetComponents<Component>();
                var componentNames = new System.Collections.Generic.List<string>();
                foreach (var c in components)
                {
                    if (c != null && !(c is Transform))
                        componentNames.Add(c.GetType().Name);
                }

                string componentsStr = componentNames.Count > 0 ? $" [{string.Join(", ", componentNames)}]" : "";
                MelonLogger.Msg($"[{NavigatorId}] {indent}{child.name}{componentsStr}");

                DumpGameObjectChildren(child.gameObject, currentDepth + 1, maxDepth);
            }
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

                        // Clear foreground and rescan to return to Home elements
                        _foregroundPanel = null;
                        TriggerRescan(force: true);
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

                        // Set foreground to social panel so elements are filtered correctly
                        _foregroundPanel = socialPanel;
                        MelonLogger.Msg($"[{NavigatorId}] Set foreground to Social panel");
                        TriggerRescan(force: true);
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
                bool isLoginPanel = _foregroundPanel.name.StartsWith("Panel -");

                // Handle Login panel waiting for return - panel may be hidden while browser shows Terms
                if (isLoginPanel && _loginPanelInactiveTime > 0)
                {
                    if (_foregroundPanel.activeInHierarchy)
                    {
                        // Panel came back - rescan to refresh elements
                        MelonLogger.Msg($"[{NavigatorId}] Login panel returned after {Time.time - _loginPanelInactiveTime:F1}s");
                        _loginPanelInactiveTime = 0;
                        TriggerRescan();
                        return;
                    }

                    // Check if a DIFFERENT Login panel became active (user moved to next screen)
                    var panelParent = GameObject.Find("Canvas - Camera/PanelParent");
                    if (panelParent != null)
                    {
                        foreach (Transform child in panelParent.transform)
                        {
                            if (child != null && child.gameObject.activeInHierarchy &&
                                child.name.StartsWith("Panel -") && child.gameObject != _foregroundPanel)
                            {
                                MelonLogger.Msg($"[{NavigatorId}] Different Login panel detected: {child.name}");
                                _loginPanelInactiveTime = 0;
                                _foregroundPanel = child.gameObject;
                                TriggerRescan();
                                return;
                            }
                        }
                    }

                    // Still waiting for panel to return (browser may be open)
                    return;
                }

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

                    // For Login panels, log what's active in PanelParent to find the overlay
                    if (isLoginPanel)
                    {
                        // Check if LoadingPanel is active - loading in progress
                        var loadingPanel = GameObject.Find("Canvas - Camera/PanelParent/LoadingPanel_Desktop_16x9(Clone)");
                        if (loadingPanel != null && loadingPanel.activeInHierarchy)
                        {
                            return; // Loading in progress, wait
                        }

                        // Login panel became inactive - likely browser opened for Terms
                        // Start waiting for it to return (no timeout - wait indefinitely)
                        _loginPanelInactiveTime = Time.time;
                        MelonLogger.Msg($"[{NavigatorId}] Login panel inactive - waiting for return (browser may be showing Terms)");
                        return;
                    }

                    // Overlay actually closed (non-Login panels)
                    MelonLogger.Msg($"[{NavigatorId}] Foreground panel closed: {_foregroundPanel.name}");
                    _foregroundPanel = null;
                    _activePanels.Clear();
                    TriggerRescan();
                    return;
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

                // Don't overwrite popup foreground - popups are tracked separately
                // Only update foreground if current one is NOT a popup overlay
                if (_foregroundPanel == null || !IsPopupOverlay(_foregroundPanel))
                {
                    // Check if any remaining panel is an overlay that should filter
                    var overlayPanel = currentPanels.LastOrDefault(p => IsOverlayPanel(p.name));
                    _foregroundPanel = overlayPanel.obj; // Will be null if no overlay
                }
                else
                {
                    MelonLogger.Msg($"[{NavigatorId}] Preserving popup foreground: {_foregroundPanel.name}");
                }

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

            // Popup/dialog overlay - takes priority over Social panel
            // (e.g., InviteFriendPopup opens on top of Friends panel)
            if (_foregroundPanel != null && IsPopupOverlay(_foregroundPanel))
            {
                return IsChildOf(obj, _foregroundPanel);
            }

            // Social panel overlay - only show Social elements when open
            // Check IsSocialPanelOpen() directly since _foregroundPanel may not be set reliably
            bool socialOpen = IsSocialPanelOpen();
            if (socialOpen)
            {
                var socialPanel = GameObject.Find("SocialUI_V2_Desktop_16x9(Clone)");
                if (socialPanel != null)
                    return IsChildOf(obj, socialPanel);
            }

            // NPE (New Player Experience) overlay - show NPE elements when tutorial UI is active
            // This handles Sparky dialogue, reward chests, etc. that overlay other screens
            // But NOT when Settings is open - Settings takes priority as a modal overlay
            if (!CheckSettingsMenuOpen() && _screenDetector.IsNPERewardsScreenActive() && IsInsideNPEOverlay(obj))
            {
                return true;
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
        /// Check if a GameObject is a popup overlay (Popup or SystemMessageView).
        /// </summary>
        private static bool IsPopupOverlay(GameObject obj)
        {
            if (obj == null) return false;
            string name = obj.name;
            return name.Contains("Popup") || name.Contains("SystemMessageView");
        }

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
        /// Check if element is inside the NPE (New Player Experience) overlay UI.
        /// This includes Sparky dialogue, reward chests, and other tutorial elements.
        /// </summary>
        private static bool IsInsideNPEOverlay(GameObject obj)
        {
            if (obj == null) return false;

            Transform current = obj.transform;
            while (current != null)
            {
                string name = current.gameObject.name;

                // NPE containers and elements
                if (name.Contains("NPE") ||
                    name.Contains("StitcherSparky") ||
                    name.Contains("Sparky"))
                {
                    return true;
                }

                current = current.parent;
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

            // Update foreground panel for Settings menu - the content panel may have changed
            // (e.g., from MainMenu to Graphics submenu)
            // BUT don't overwrite popup foreground - popups take priority over Settings
            if (CheckSettingsMenuOpen() && SettingsContentPanel != null && !IsPopupOverlay(_foregroundPanel))
            {
                _foregroundPanel = SettingsContentPanel;
                MelonLogger.Msg($"[{NavigatorId}] Updated foreground panel to Settings: {_foregroundPanel.name}");
            }

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

        protected int CountActiveCustomButtons()
        {
            return GetActiveCustomButtons().Count();
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
            foreach (var btn in buttons.Take(20))
            {
                string text = UITextExtractor.GetText(btn.gameObject);
                string path = GetGameObjectPath(btn.gameObject);

                // For Increment/Decrement buttons, show parent info to understand stepper structure
                string parentInfo = "";
                if (btn.gameObject.name.Contains("Increment") || btn.gameObject.name.Contains("Decrement"))
                {
                    var parent = btn.transform.parent;
                    if (parent != null)
                    {
                        parentInfo = $" | Parent: {parent.name}";
                        // Look for sibling elements that might have the setting name
                        foreach (Transform sibling in parent)
                        {
                            if (sibling.gameObject != btn.gameObject)
                            {
                                var sibText = sibling.GetComponentInChildren<TMP_Text>();
                                if (sibText != null && !string.IsNullOrEmpty(sibText.text) && sibText.text.Length > 1)
                                {
                                    parentInfo += $" | Sibling[{sibling.name}]: '{sibText.text}'";
                                }
                            }
                        }
                    }
                }

                MelonLogger.Msg($"[{NavigatorId}]   {path} - '{text ?? "(no text)"}'{parentInfo}");
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

            // Find TMP_Dropdown components
            var tmpDropdowns = GameObject.FindObjectsOfType<TMP_Dropdown>()
                .Where(d => d != null && d.gameObject.activeInHierarchy)
                .ToList();
            MelonLogger.Msg($"[{NavigatorId}] Found {tmpDropdowns.Count} TMP_Dropdown components:");
            foreach (var d in tmpDropdowns)
            {
                string path = GetGameObjectPath(d.gameObject);
                string selectedText = d.options.Count > d.value ? d.options[d.value].text : "(no selection)";
                MelonLogger.Msg($"[{NavigatorId}]   {path} - selected: '{selectedText}' - interactable: {d.interactable}");
            }

            // Find any MonoBehaviour with "Dropdown" or "Selector" in the type name (custom dropdowns)
            var customDropdowns = GameObject.FindObjectsOfType<MonoBehaviour>()
                .Where(mb => mb != null && mb.gameObject.activeInHierarchy &&
                       (mb.GetType().Name.Contains("Dropdown") || mb.GetType().Name.Contains("Selector")))
                .ToList();
            MelonLogger.Msg($"[{NavigatorId}] Found {customDropdowns.Count} Custom Dropdown/Selector components:");
            foreach (var cd in customDropdowns)
            {
                string path = GetGameObjectPath(cd.gameObject);
                string typeName = cd.GetType().Name;
                string text = UITextExtractor.GetText(cd.gameObject);
                MelonLogger.Msg($"[{NavigatorId}]   {path} - Type: {typeName} - '{text ?? "(no text)"}'");
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

                // Debug: log Friends panel elements
                bool isFriendsElement = obj.name.Contains("Backer") || obj.name.Contains("AddFriend");

                if (!IsInForegroundPanel(obj))
                {
                    if (isFriendsElement)
                        MelonLogger.Msg($"[{NavigatorId}] Friends element REJECTED by IsInForegroundPanel: {obj.name}");
                    return;
                }

                var classification = UIElementClassifier.Classify(obj);
                if (classification.IsNavigable && classification.ShouldAnnounce)
                {
                    var pos = obj.transform.position;
                    float sortOrder = -pos.y * 1000 + pos.x;
                    discoveredElements.Add((obj, classification, sortOrder));
                    addedObjects.Add(obj);
                    if (isFriendsElement)
                        MelonLogger.Msg($"[{NavigatorId}] Friends element ADDED: {obj.name} -> {classification.Label}");
                }
                else if (isFriendsElement)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Friends element REJECTED by Classifier: {obj.name} IsNavigable={classification.IsNavigable} ShouldAnnounce={classification.ShouldAnnounce}");
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

            // Find TMP_Dropdown elements
            foreach (var dropdown in GameObject.FindObjectsOfType<TMP_Dropdown>())
            {
                if (dropdown != null && dropdown.interactable)
                    TryAddElement(dropdown.gameObject);
            }

            // Find Settings custom controls (dropdowns, steppers)
            // These are custom implementations in MTGA Settings that need special handling
            if (CheckSettingsMenuOpen())
            {
                FindSettingsCustomControls(TryAddElement);
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

                // Build carousel info if this element supports arrow navigation (including sliders)
                CarouselInfo carouselInfo = classification.HasArrowNavigation
                    ? new CarouselInfo
                    {
                        HasArrowNavigation = true,
                        PreviousControl = classification.PreviousControl,
                        NextControl = classification.NextControl,
                        SliderComponent = classification.SliderComponent
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

            // Find NPE reward cards (displayed cards on reward screens)
            // These are not buttons but should be navigable to read card info
            FindNPERewardCards(addedObjects);

            MelonLogger.Msg($"[{NavigatorId}] Discovered {_elements.Count} navigable elements");

            // On MatchEndScene, auto-focus the Continue button (ExitMatchOverlayButton)
            AutoFocusContinueButton();

            // On NPE rewards screen, auto-focus the unlocked card
            AutoFocusUnlockedCard();
        }

        /// <summary>
        /// Find NPE (New Player Experience) reward cards that are displayed on screen.
        /// These cards aren't buttons but should be navigable for accessibility.
        /// </summary>
        private void FindNPERewardCards(HashSet<GameObject> addedObjects)
        {
            // Don't add NPE cards if Settings menu is open - Settings takes priority
            if (CheckSettingsMenuOpen())
                return;

            // Check if we're on the NPE rewards screen
            if (!_screenDetector.IsNPERewardsScreenActive())
                return;

            MelonLogger.Msg($"[{NavigatorId}] NPE Rewards screen detected, searching for reward cards...");

            // Debug: Log the RewardsCONTAINER specifically (where the actual cards are)
            var npeContainer = GameObject.Find("NPE-Rewards_Container");
            if (npeContainer != null)
            {
                var activeContainer = npeContainer.transform.Find("ActiveContainer");
                if (activeContainer != null)
                {
                    var rewardsContainer = activeContainer.Find("RewardsCONTAINER");
                    if (rewardsContainer != null)
                    {
                        MelonLogger.Msg($"[{NavigatorId}] RewardsCONTAINER hierarchy (depth 6):");
                        LogHierarchy(rewardsContainer, "  ", 6);
                    }
                    else
                    {
                        MelonLogger.Msg($"[{NavigatorId}] ActiveContainer hierarchy (depth 5):");
                        LogHierarchy(activeContainer, "  ", 5);
                    }
                }
            }

            // Find NPE reward cards - ONLY inside NPE-Rewards_Container, not deck boxes
            var cardPrefabs = new List<GameObject>();

            if (npeContainer != null)
            {
                // Search within NPE-Rewards_Container only
                foreach (var transform in npeContainer.GetComponentsInChildren<Transform>(false))
                {
                    if (transform == null || !transform.gameObject.activeInHierarchy)
                        continue;

                    string name = transform.name;
                    string path = GetFullPath(transform);

                    // Skip deck box cards (background elements)
                    if (path.Contains("DeckBox") || path.Contains("Deckbox") || path.Contains("RewardChest"))
                        continue;

                    // NPE reward cards - try multiple patterns
                    if (name.Contains("NPERewardPrefab_IndividualCard") ||
                        name.Contains("CardReward") ||
                        name.Contains("CardAnchor") ||
                        name.Contains("RewardCard") ||
                        name.Contains("MetaCardView") ||
                        name.Contains("CDC"))
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Found potential NPE card element: {name} at {path}");
                        if (!addedObjects.Contains(transform.gameObject))
                        {
                            cardPrefabs.Add(transform.gameObject);
                        }
                    }
                }
            }

            if (cardPrefabs.Count == 0)
            {
                MelonLogger.Msg($"[{NavigatorId}] No NPE reward cards found in NPE-Rewards_Container");
                return;
            }

            // Sort cards by X position (left to right)
            cardPrefabs = cardPrefabs.OrderBy(c => c.transform.position.x).ToList();

            MelonLogger.Msg($"[{NavigatorId}] Found {cardPrefabs.Count} NPE reward card(s)");

            int cardNum = 1;
            foreach (var cardPrefab in cardPrefabs)
            {
                // Find the CardAnchor child which holds the actual card visuals
                var cardAnchor = cardPrefab.transform.Find("CardAnchor");
                GameObject cardObj = cardAnchor?.gameObject ?? cardPrefab;

                // Extract card info using CardDetector
                var cardInfo = CardDetector.ExtractCardInfo(cardPrefab);
                string cardName = cardInfo.IsValid ? cardInfo.Name : "Unknown card";

                // Build label with card number if multiple cards
                string label = cardPrefabs.Count > 1
                    ? $"Unlocked card {cardNum}: {cardName}"
                    : $"Unlocked card: {cardName}";

                // Add type line if available
                if (!string.IsNullOrEmpty(cardInfo.TypeLine))
                {
                    label += $", {cardInfo.TypeLine}";
                }

                MelonLogger.Msg($"[{NavigatorId}] Adding NPE reward card: {label}");

                // Add as navigable element (even though it's not a button)
                // Using the card prefab so arrow up/down can read card info blocks
                AddElement(cardObj, label);
                addedObjects.Add(cardPrefab);
                cardNum++;
            }

            // Add the NullClaimButton as "Take reward" button
            // This button has a CustomButton that dismisses the reward screen when clicked
            // NOTE: The button name starts with "Null" which is normally filtered out,
            // so we explicitly add it here to make NPE rewards accessible
            // Search within entire npeContainer hierarchy (more robust than Transform.Find)
            if (npeContainer != null)
            {
                Transform claimButton = null;
                foreach (var transform in npeContainer.GetComponentsInChildren<Transform>(false))
                {
                    if (transform != null && transform.name == "NullClaimButton" && transform.gameObject.activeInHierarchy)
                    {
                        claimButton = transform;
                        break;
                    }
                }

                if (claimButton != null && !addedObjects.Contains(claimButton.gameObject))
                {
                    MelonLogger.Msg($"[{NavigatorId}] Adding NullClaimButton as 'Take reward' button (path: {GetFullPath(claimButton)})");
                    AddElement(claimButton.gameObject, "Take reward, button");
                    addedObjects.Add(claimButton.gameObject);
                }
                else if (claimButton == null)
                {
                    MelonLogger.Msg($"[{NavigatorId}] NullClaimButton not found in NPE-Rewards_Container hierarchy");
                }
                else
                {
                    MelonLogger.Msg($"[{NavigatorId}] NullClaimButton already in addedObjects (ID:{claimButton.gameObject.GetInstanceID()})");
                }
            }
            else
            {
                MelonLogger.Msg($"[{NavigatorId}] NPE-Rewards_Container not found for NullClaimButton lookup");
            }
        }

        /// <summary>
        /// Log the hierarchy of a transform for debugging purposes.
        /// </summary>
        private void LogHierarchy(Transform parent, string indent, int maxDepth)
        {
            if (maxDepth <= 0) return;

            foreach (Transform child in parent)
            {
                if (child == null) continue;
                string active = child.gameObject.activeInHierarchy ? "" : " [INACTIVE]";
                string text = UITextExtractor.GetText(child.gameObject);
                string textInfo = string.IsNullOrEmpty(text) ? "" : $" - \"{text}\"";
                MelonLogger.Msg($"[{NavigatorId}] {indent}{child.name}{active}{textInfo}");
                LogHierarchy(child, indent + "  ", maxDepth - 1);
            }
        }

        /// <summary>
        /// Get the full path of a transform in the hierarchy.
        /// </summary>
        private string GetFullPath(Transform t)
        {
            if (t == null) return "null";
            if (t.parent == null) return t.name;
            return GetFullPath(t.parent) + "/" + t.name;
        }

        /// <summary>
        /// Find Settings custom controls (dropdowns and steppers) in a single iteration.
        /// - Dropdowns: "Control - *_Dropdown" pattern (only if no TMP_Dropdown child)
        /// - Steppers: "Control - Setting:*" or "Control - *_Selector" with Increment/Decrement buttons
        /// </summary>
        private void FindSettingsCustomControls(System.Action<GameObject> tryAddElement)
        {
            // Single iteration through all transforms for both control types
            foreach (var transform in GameObject.FindObjectsOfType<Transform>())
            {
                if (transform == null || !transform.gameObject.activeInHierarchy)
                    continue;

                string name = transform.name;

                // All Settings controls start with "Control - "
                if (!name.StartsWith("Control - ", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                // Check for dropdown pattern: "Control - *_Dropdown"
                if (name.EndsWith("_Dropdown", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Skip if there's a TMP_Dropdown child - those are detected separately
                    var tmpDropdown = transform.GetComponentInChildren<TMP_Dropdown>();
                    if (tmpDropdown != null)
                        continue;

                    // Find the clickable element inside this control
                    GameObject clickableElement = FindClickableInDropdownControl(transform.gameObject);
                    if (clickableElement != null)
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Found Settings dropdown (non-TMP): {name} -> clickable: {clickableElement.name}");
                        tryAddElement(clickableElement);
                    }
                    continue;
                }

                // Check for stepper pattern: "Control - Setting:*" or "Control - *_Selector"
                bool isSettingControl = name.StartsWith("Control - Setting:", System.StringComparison.OrdinalIgnoreCase);
                bool isSelectorControl = name.EndsWith("_Selector", System.StringComparison.OrdinalIgnoreCase);

                if (!isSettingControl && !isSelectorControl)
                    continue;

                // Check if this control has Increment/Decrement buttons (stepper pattern)
                bool hasIncrement = false;
                bool hasDecrement = false;

                foreach (Transform child in transform.GetComponentsInChildren<Transform>(true))
                {
                    if (child == transform || !child.gameObject.activeInHierarchy)
                        continue;

                    var button = child.GetComponent<Button>();
                    if (button != null)
                    {
                        string childName = child.name;
                        if (childName.IndexOf("increment", System.StringComparison.OrdinalIgnoreCase) >= 0)
                            hasIncrement = true;
                        else if (childName.IndexOf("decrement", System.StringComparison.OrdinalIgnoreCase) >= 0)
                            hasDecrement = true;
                    }

                    if (hasIncrement && hasDecrement)
                        break;
                }

                // Only add if it has stepper buttons
                if (hasIncrement || hasDecrement)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Found Settings stepper control: {name}");
                    tryAddElement(transform.gameObject);
                }
            }
        }

        /// <summary>
        /// Find the clickable element inside a Settings dropdown control.
        /// </summary>
        private GameObject FindClickableInDropdownControl(GameObject control)
        {
            // First check if the control itself has a CustomButton
            if (UIElementClassifier.HasCustomButton(control))
                return control;

            // Look for a Button component
            var button = control.GetComponent<Button>();
            if (button != null)
                return control;

            // Search children for a CustomButton or Button
            foreach (Transform child in control.GetComponentsInChildren<Transform>(true))
            {
                if (child.gameObject == control || !child.gameObject.activeInHierarchy)
                    continue;

                // Check for CustomButton
                if (UIElementClassifier.HasCustomButton(child.gameObject))
                    return child.gameObject;

                // Check for Button
                var childButton = child.GetComponent<Button>();
                if (childButton != null && childButton.interactable)
                    return child.gameObject;
            }

            // Fallback: return the control itself if it has meaningful text
            if (UITextExtractor.HasActualText(control))
                return control;

            return null;
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
        /// Auto-focus the unlocked card on NPE reward screens for better UX.
        /// The card should be the first thing focused so users can read its info.
        /// </summary>
        private void AutoFocusUnlockedCard()
        {
            // Only on NPE rewards screen
            if (!_screenDetector.IsNPERewardsScreenActive())
                return;

            // Find unlocked card elements (they have "Unlocked card" in their label)
            for (int i = 0; i < _elements.Count; i++)
            {
                if (_elements[i].Label != null &&
                    _elements[i].Label.StartsWith("Unlocked card"))
                {
                    // Move this element to the front
                    if (i > 0)
                    {
                        var cardElement = _elements[i];
                        _elements.RemoveAt(i);
                        _elements.Insert(0, cardElement);
                        MelonLogger.Msg($"[{NavigatorId}] Moved unlocked card to first position: {cardElement.Label}");
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
            return $"{menuName}. {_elements.Count} items. {Models.Strings.NavigateWithArrows}, Enter to select.";
        }

        protected override bool OnElementActivated(int index, GameObject element)
        {
            // Check if this is a Toggle (checkbox) - we'll force rescan for these
            bool isToggle = element.GetComponent<Toggle>() != null;

            // Check if this is an input field - may cause UI changes (e.g., Send button appearing)
            bool isInputField = element.GetComponent<TMP_InputField>() != null;

            // Check if this is a dropdown - need to enter edit mode so arrows navigate dropdown items
            bool isDropdown = UIFocusTracker.IsDropdown(element);

            // Check if this is a popup/dialog button (SystemMessageButton) - popup will close with animation
            bool isPopupButton = element.name.Contains("SystemMessageButton");

            // Check if we're in the Settings menu - submenu button clicks need rescan
            // because the content panel changes but no panel state event fires
            bool isInSettingsMenu = CheckSettingsMenuOpen();
            string currentSettingsPanel = SettingsContentPanel?.name;

            // Use UIActivator for CustomButtons
            var result = UIActivator.Activate(element);
            _announcer.Announce(result.Message, Models.AnnouncementPriority.Normal);

            // For Toggle activations (checkboxes), force a rescan after a delay
            // This handles deck folder filtering where Selectable count may not change
            // but the visible decks do change
            // Use force=true to bypass debounce since user explicitly toggled a filter
            // Skip rescan for Login panels (e.g., UpdatePolicies) - game hides panel briefly during server call
            // which would cause our foreground detection to think it closed
            if (isToggle)
            {
                bool isLoginPanel = _foregroundPanel != null && _foregroundPanel.name.StartsWith("Panel -");
                if (!isLoginPanel)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Toggle activated - forcing rescan in {RescanDelaySeconds}s (bypassing debounce)");
                    TriggerRescan(force: true);
                }
                else
                {
                    MelonLogger.Msg($"[{NavigatorId}] Toggle activated on Login panel - skipping rescan");
                }
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

            // Dropdowns: just activate, focus-based detection in UIFocusTracker handles edit mode
            // When focus goes to dropdown items, we automatically enter dropdown mode
            if (isDropdown)
            {
                MelonLogger.Msg($"[{NavigatorId}] Dropdown activated ({element.name})");
                return true;
            }

            // For popup/dialog button activations (OK, Cancel), the popup closes with animation
            // Start cooldown to prevent popup re-detection during close animation
            if (isPopupButton)
            {
                MelonLogger.Msg($"[{NavigatorId}] Popup button activated ({element.name}) - dismissing popup");
                // Start cooldown in tracker (restores tracker's foreground to panel before popup)
                _panelTracker.StartPopupDismissCooldown();
                // Sync our foreground from tracker (restored to panel before popup)
                _foregroundPanel = _panelTracker.ForegroundPanel;
                MelonLogger.Msg($"[{NavigatorId}] Synced foreground: {_foregroundPanel?.name ?? "null"}");
                // Trigger immediate rescan
                TriggerRescan(force: true);
                return true;
            }

            // For Settings menu button clicks (Audio, Gameplay, Graphics submenu buttons),
            // trigger a rescan to pick up the new content panel
            if (isInSettingsMenu && IsSettingsSubmenuButton(element))
            {
                MelonLogger.Msg($"[{NavigatorId}] Settings submenu button activated ({element.name}) - scheduling rescan");
                TriggerRescan(force: true);
                return true;
            }

            return true;
        }

        /// <summary>
        /// Check if element is a Settings submenu button (Audio, Gameplay, Graphics, etc.)
        /// </summary>
        private bool IsSettingsSubmenuButton(GameObject element)
        {
            if (element == null) return false;

            string name = element.name;

            // Settings submenu buttons follow the pattern "Button_*" in the CenterMenu
            // e.g., Button_Audio, Button_Gameplay, Button_Graphics
            if (name.StartsWith("Button_"))
            {
                // Verify it's in the Settings menu structure
                Transform parent = element.transform.parent;
                while (parent != null)
                {
                    if (parent.name == "CenterMenu" || parent.name.Contains("Settings"))
                        return true;
                    parent = parent.parent;
                }
            }

            return false;
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
                // Determine if this is a close event or just a state transition
                // - PlayBlade:Hidden = close (PlayBladeController visual state)
                // - Blade:* with isOpen=false = close (BladeContentView.Hide() was called)
                // - PlayBlade:<non-Hidden> with isOpen=false = state transition (e.g., Events -> DirectChallenge)
                bool isPlayBladeVisualState = panelTypeName.StartsWith("PlayBlade:");
                bool isBladeContentViewHide = panelTypeName.StartsWith("Blade:");
                bool isClosing = panelTypeName.Contains("Hidden") || isBladeContentViewHide;

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
