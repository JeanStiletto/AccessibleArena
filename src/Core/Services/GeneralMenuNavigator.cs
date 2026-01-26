using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Services.PanelDetection;
using AccessibleArena.Core.Services.ElementGrouping;
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

        #region Foreground Layer System

        /// <summary>
        /// The layers that can be "in front", in priority order.
        /// Higher priority layers block lower ones.
        /// Used for both element filtering AND backspace navigation.
        /// Note: Settings is handled by dedicated SettingsMenuNavigator.
        /// </summary>
        private enum ForegroundLayer
        {
            None,           // No specific layer active
            Home,           // Home screen (base state)
            ContentPanel,   // A content page like Collection, Store
            NPE,            // New Player Experience overlay
            PlayBlade,      // Play blade expanded
            Social,         // Friends panel
            Popup           // Modal popup/dialog (highest priority for GeneralMenuNavigator)
        }

        /// <summary>
        /// Single source of truth: what's currently in foreground?
        /// Both element filtering (ShouldShowElement) and backspace navigation (HandleBackNavigation)
        /// derive their behavior from this method.
        /// Note: Settings is handled by dedicated SettingsMenuNavigator.
        /// </summary>
        private ForegroundLayer GetCurrentForeground()
        {
            // Priority order - first match wins
            // Note: Settings check removed - handled by SettingsMenuNavigator

            if (_foregroundPanel != null && IsPopupOverlay(_foregroundPanel))
                return ForegroundLayer.Popup;

            if (IsSocialPanelOpen())
                return ForegroundLayer.Social;

            if (PanelStateManager.Instance?.IsPlayBladeActive == true)
                return ForegroundLayer.PlayBlade;

            if (_screenDetector.IsNPERewardsScreenActive())
                return ForegroundLayer.NPE;

            if (_activeControllerGameObject != null && _activeContentController != "HomePageContentController")
                return ForegroundLayer.ContentPanel;

            if (_activeContentController == "HomePageContentController")
                return ForegroundLayer.Home;

            return ForegroundLayer.None;
        }

        #endregion

        #region Timing Constants

        private const float ActivationDelaySeconds = 0.5f;
        private const float RescanDelaySeconds = 0.5f;
        private const float BladeAutoExpandDelay = 0.8f;

        #endregion

        #region State Fields

        /// <summary>
        /// Enable verbose debug logging. Set to false to reduce log noise in production.
        /// </summary>
        private static readonly bool DebugLogging = false;

        protected string _currentScene;
        protected string _detectedMenuType;
        private bool _hasLoggedUIOnce;
        private float _activationDelay;

        // Helper instances
        private readonly MenuScreenDetector _screenDetector;

        // Element grouping infrastructure (Phase 2 of element grouping feature)
        private readonly OverlayDetector _overlayDetector;
        private readonly ElementGroupAssigner _groupAssigner;
        private readonly GroupedNavigator _groupedNavigator;

        /// <summary>
        /// Whether grouped (hierarchical) navigation is enabled.
        /// When true, elements are organized into groups for two-level navigation.
        /// </summary>
        private bool _groupedNavigationEnabled = true;

        // Rescan timing
        private float _rescanDelay;

        // Element tracking
        private float _bladeAutoExpandDelay;

        #endregion

        #region Helper Methods

        /// <summary>
        /// Log a debug message only if DebugLogging is enabled.
        /// </summary>
        private void LogDebug(string message)
        {
            if (DebugLogging)
                MelonLogger.Msg(message);
        }

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

        /// <summary>
        /// Report a panel opened to PanelStateManager.
        /// </summary>
        private void ReportPanelOpened(string panelName, GameObject panelObj, PanelDetectionMethod detectedBy)
        {
            if (PanelStateManager.Instance == null || panelObj == null)
                return;

            var panelType = PanelInfo.ClassifyPanel(panelName);
            var panelInfo = new PanelInfo(panelName, panelType, panelObj, detectedBy);
            PanelStateManager.Instance.ReportPanelOpened(panelInfo);
        }

        /// <summary>
        /// Report a panel closed to PanelStateManager.
        /// </summary>
        private void ReportPanelClosed(GameObject panelObj)
        {
            if (PanelStateManager.Instance == null || panelObj == null)
                return;

            PanelStateManager.Instance.ReportPanelClosed(panelObj);
        }

        /// <summary>
        /// Report a panel closed by name to PanelStateManager.
        /// </summary>
        private void ReportPanelClosedByName(string panelName)
        {
            if (PanelStateManager.Instance == null || string.IsNullOrEmpty(panelName))
                return;

            PanelStateManager.Instance.ReportPanelClosedByName(panelName);
        }

        #endregion

        public override string NavigatorId => "GeneralMenu";
        public override string ScreenName => GetMenuScreenName();
        public override int Priority => 15; // Low priority - fallback after specific navigators

        /// <summary>
        /// Check if Settings menu is currently open.
        /// Uses Harmony-tracked panel state for precise detection.
        /// Used to deactivate this navigator so SettingsMenuNavigator can take over.
        /// </summary>
        private bool IsSettingsMenuOpen() => PanelStateManager.Instance?.IsSettingsMenuOpen == true;

        /// <summary>
        /// Check if the Social/Friends panel is currently open.
        /// </summary>
        protected bool IsSocialPanelOpen() => _screenDetector.IsSocialPanelOpen();

        // Convenience properties to access helper state
        private string _activeContentController => _screenDetector.ActiveContentController;
        private GameObject _activeControllerGameObject => _screenDetector.ActiveControllerGameObject;
        private GameObject _navBarGameObject => _screenDetector.NavBarGameObject;
        private GameObject _foregroundPanel => PanelStateManager.Instance?.GetFilterPanel();

        public GeneralMenuNavigator(IAnnouncementService announcer) : base(announcer)
        {
            _screenDetector = new MenuScreenDetector();

            // Initialize element grouping infrastructure
            _overlayDetector = new OverlayDetector(_screenDetector);
            _groupAssigner = new ElementGroupAssigner(_overlayDetector);
            _groupedNavigator = new GroupedNavigator(announcer, _groupAssigner);

            // Subscribe to PanelStateManager for rescan triggers
            if (PanelStateManager.Instance != null)
            {
                PanelStateManager.Instance.OnPanelChanged += OnPanelStateManagerActiveChanged;
                PanelStateManager.Instance.OnAnyPanelOpened += OnPanelStateManagerAnyOpened;
            }
        }

        /// <summary>
        /// Handler for PanelStateManager.OnPanelChanged - fires when active (filtering) panel changes.
        /// </summary>
        private void OnPanelStateManagerActiveChanged(PanelInfo oldPanel, PanelInfo newPanel)
        {
            if (!_isActive) return;

            // Ignore SocialUI as SOURCE of change - it's just the corner icon, causes spurious rescans
            // But DO rescan when something closes and falls back to SocialUI (e.g., popup closes)
            if (oldPanel?.Name?.Contains("SocialUI") == true)
            {
                LogDebug($"[{NavigatorId}] Ignoring SocialUI as source of change");
                return;
            }

            // Ignore Settings panel changes - handled by SettingsMenuNavigator
            if (oldPanel?.Name?.Contains("SettingsMenu") == true || newPanel?.Name?.Contains("SettingsMenu") == true)
            {
                LogDebug($"[{NavigatorId}] Ignoring SettingsMenu panel change");
                return;
            }

            LogDebug($"[{NavigatorId}] PanelStateManager active changed: {oldPanel?.Name ?? "none"} -> {newPanel?.Name ?? "none"}");
            TriggerRescan();
        }

        /// <summary>
        /// Handler for PanelStateManager.OnAnyPanelOpened - fires when ANY panel opens.
        /// Used for triggering rescans on screens that don't filter (e.g., HomePage).
        /// Also handles special behaviors like Color Challenge blade auto-expand.
        /// </summary>
        private void OnPanelStateManagerAnyOpened(PanelInfo panel)
        {
            if (!_isActive) return;

            // Ignore SocialUI - it's always present as corner icon, causes spurious rescans during page load
            if (panel?.Name?.Contains("SocialUI") == true)
            {
                LogDebug($"[{NavigatorId}] Ignoring SocialUI panel opened");
                return;
            }

            // Ignore Settings panel - handled by SettingsMenuNavigator
            if (panel?.Name?.Contains("SettingsMenu") == true)
            {
                LogDebug($"[{NavigatorId}] Ignoring SettingsMenu panel opened");
                return;
            }

            LogDebug($"[{NavigatorId}] PanelStateManager panel opened: {panel?.Name ?? "none"}");

            // Auto-expand blade for Color Challenge menu
            if (panel?.Name?.Contains("CampaignGraph") == true)
            {
                _bladeAutoExpandDelay = BladeAutoExpandDelay;
                LogDebug($"[{NavigatorId}] Scheduling blade auto-expand for Color Challenge");
            }

            TriggerRescan();
        }

        protected virtual string GetMenuScreenName()
        {
            // Note: Settings check removed - handled by SettingsMenuNavigator

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

            _screenDetector.Reset();

            // Full reset PanelStateManager for scene change
            // This clears panel stack, announced panels, and blade state
            PanelStateManager.Instance?.Reset();

            if (_isActive)
            {
                // Check if we should stay active
                if (ExcludedScenes.Contains(sceneName))
                {
                    Deactivate();
                }
                // Note: ForceRescan is called by NavigatorManager after OnSceneChanged
                // if navigator stays active - no need to handle it here
            }
        }

        /// <summary>
        /// Called when navigator is deactivating - clean up panel state
        /// </summary>
        protected override void OnDeactivating()
        {
            base.OnDeactivating();

            _screenDetector.Reset();

            // Soft reset PanelStateManager (preserve tracking, clear announced state)
            PanelStateManager.Instance?.SoftReset();
        }

        /// <summary>
        /// Validate that we should remain active.
        /// Returns false if settings menu opens, allowing SettingsMenuNavigator to take over.
        /// </summary>
        protected override bool ValidateElements()
        {
            // Deactivate if settings menu is open - let SettingsMenuNavigator handle it
            if (IsSettingsMenuOpen())
            {
                LogDebug($"[{NavigatorId}] Settings menu detected - deactivating to let SettingsMenuNavigator take over");
                return false;
            }

            return base.ValidateElements();
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

            // Panel detection handled by PanelDetectorManager via events (OnPanelChanged/OnAnyPanelOpened)
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
                LogDebug($"[{NavigatorId}] F4 pressed - toggling Friends panel");
                ToggleFriendsPanel();
                return true;
            }

            // F12: Debug dump of current UI hierarchy (for development)
            if (Input.GetKeyDown(KeyCode.F12))
            {
                DumpUIHierarchy();
                return true;
            }

            // Enter: In grouped navigation mode, handle both group entry and element activation
            bool enterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            if (enterPressed && _groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                if (HandleGroupedEnter())
                    return true;
            }

            // Backspace: Universal back - goes back one level in menus
            // But NOT when an input field is focused - let Backspace delete characters
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                if (UIFocusTracker.IsAnyInputFieldFocused())
                {
                    LogDebug($"[{NavigatorId}] Backspace pressed but input field focused - passing through");
                    return false; // Let it pass through to the input field
                }

                // In grouped navigation mode inside a group, exit to group level first
                if (HandleGroupedBackspace())
                    return true;

                // Otherwise, use normal back navigation
                return HandleBackNavigation();
            }

            // Escape: Exit input field if one is focused (but user didn't enter via our navigation)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (UIFocusTracker.IsAnyInputFieldFocused())
                {
                    LogDebug($"[{NavigatorId}] Escape pressed while input field focused - deactivating field");
                    UIFocusTracker.DeactivateFocusedInputField();
                    _announcer.Announce("Exited input field", Models.AnnouncementPriority.Normal);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Handle Backspace key - navigates back one level in the menu hierarchy.
        /// Uses OverlayDetector for overlay cases, GetCurrentForeground() for content panel cases.
        /// </summary>
        private bool HandleBackNavigation()
        {
            // First check if an overlay is active using the new OverlayDetector
            var activeOverlay = _overlayDetector.GetActiveOverlay();
            LogDebug($"[{NavigatorId}] Backspace: activeOverlay = {activeOverlay?.ToString() ?? "none"}");

            if (activeOverlay != null)
            {
                return activeOverlay switch
                {
                    ElementGroup.Popup => DismissPopup(),
                    ElementGroup.Social => CloseSocialPanel(),
                    ElementGroup.PlayBlade => ClosePlayBlade(),
                    ElementGroup.NPE => HandleNPEBack(),
                    ElementGroup.SettingsMenu => false, // Settings handled by SettingsMenuNavigator
                    _ => false
                };
            }

            // No overlay - check content panel layer
            var layer = GetCurrentForeground();
            LogDebug($"[{NavigatorId}] Backspace: layer = {layer}");

            return layer switch
            {
                ForegroundLayer.ContentPanel => HandleContentPanelBack(),
                ForegroundLayer.Home => TryGenericBackButton(),
                ForegroundLayer.None => TryGenericBackButton(),
                _ => false
            };
        }

        /// <summary>
        /// Handle back navigation in content panels.
        /// First try generic back button, then navigate to Home.
        /// </summary>
        private bool HandleContentPanelBack()
        {
            // First try to find a dismiss button within the current content panel
            if (_activeControllerGameObject != null)
            {
                var dismissButton = FindDismissButtonInPanel(_activeControllerGameObject);
                if (dismissButton != null)
                {
                    LogDebug($"[{NavigatorId}] Found dismiss button in content panel: {dismissButton.name}");
                    _announcer.Announce(Models.Strings.NavigatingBack, Models.AnnouncementPriority.High);
                    UIActivator.Activate(dismissButton);
                    TriggerRescan();
                    return true;
                }
            }

            // Content panels (Store, Collection, BoosterChamber, etc.) don't have back buttons.
            // Navigate to Home instead. Don't use FindGenericBackButton() here as it finds
            // buttons from unrelated panels (e.g., FullscreenZFBrowserCanvas).
            LogDebug($"[{NavigatorId}] No dismiss button in content panel, navigating to Home");
            return NavigateToHome();
        }

        /// <summary>
        /// Find a dismiss/close button within a specific panel.
        /// Only matches explicit Close/Dismiss/Back buttons, not ModalFade
        /// (which is ambiguous - sometimes dismiss, sometimes other actions).
        /// </summary>
        private GameObject FindDismissButtonInPanel(GameObject panel)
        {
            if (panel == null) return null;

            // Look for explicit close/dismiss/back buttons within the panel
            // Note: ModalFade is NOT included - it's ambiguous (e.g., in BoosterChamber it's "Open x 10")
            foreach (var mb in panel.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name != "CustomButton") continue;

                string name = mb.gameObject.name;
                if (name.Contains("Close") ||
                    name.Contains("Dismiss") ||
                    (name.Contains("Back") && !name.Contains("Background")))
                {
                    return mb.gameObject;
                }
            }

            return null;
        }

        /// <summary>
        /// Try to find and click a generic back button.
        /// Returns false if no back button found (nothing to do).
        /// </summary>
        private bool TryGenericBackButton()
        {
            var backButton = FindGenericBackButton();
            if (backButton != null)
            {
                LogDebug($"[{NavigatorId}] Found back button: {backButton.name}");
                _announcer.Announce(Models.Strings.NavigatingBack, Models.AnnouncementPriority.High);
                UIActivator.Activate(backButton);
                TriggerRescan();
                return true;
            }

            LogDebug($"[{NavigatorId}] At top level, no back action");
            return false;
        }

        /// <summary>
        /// Dismiss the current popup/dialog.
        /// </summary>
        private bool DismissPopup()
        {
            LogDebug($"[{NavigatorId}] Dismissing popup");
            if (_foregroundPanel != null)
            {
                var closeButton = FindCloseButtonInPanel(_foregroundPanel);
                if (closeButton != null)
                {
                    _announcer.Announce(Models.Strings.NavigatingBack, Models.AnnouncementPriority.High);
                    UIActivator.Activate(closeButton);
                    TriggerRescan();
                    return true;
                }
            }

            return TryGenericBackButton();
        }

        /// <summary>
        /// Close the Social (Friends) panel.
        /// </summary>
        private bool CloseSocialPanel()
        {
            LogDebug($"[{NavigatorId}] Closing Social panel");
            var socialPanel = GameObject.Find("SocialUI_V2_Desktop_16x9(Clone)");
            if (socialPanel != null)
            {
                var closeButton = FindCloseButtonInPanel(socialPanel);
                if (closeButton != null)
                {
                    _announcer.Announce(Models.Strings.NavigatingBack, Models.AnnouncementPriority.High);
                    UIActivator.Activate(closeButton);
                    TriggerRescan();
                    return true;
                }
            }

            return TryGenericBackButton();
        }

        /// <summary>
        /// Handle back navigation in NPE (New Player Experience) screens.
        /// </summary>
        private bool HandleNPEBack()
        {
            LogDebug($"[{NavigatorId}] Handling NPE back");
            return TryGenericBackButton();
        }

        /// <summary>
        /// Find a button matching the given predicate within a panel or scene-wide.
        /// Searches both Unity Button and CustomButton components.
        /// </summary>
        /// <param name="panel">Panel to search within, or null for scene-wide search</param>
        /// <param name="namePredicate">Predicate to match button names</param>
        /// <param name="customButtonFilter">Optional extra filter for CustomButtons (e.g., no text)</param>
        private GameObject FindButtonByPredicate(
            GameObject panel,
            System.Func<string, bool> namePredicate,
            System.Func<GameObject, bool> customButtonFilter = null)
        {
            // Search Unity Buttons
            var buttons = panel != null
                ? panel.GetComponentsInChildren<Button>(true)
                : GameObject.FindObjectsOfType<Button>();

            foreach (var btn in buttons)
            {
                if (btn == null || !btn.gameObject.activeInHierarchy || !btn.interactable) continue;
                if (namePredicate(btn.gameObject.name))
                    return btn.gameObject;
            }

            // Search CustomButtons
            if (panel != null)
            {
                foreach (var mb in panel.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                    if (!IsCustomButtonType(mb.GetType().Name)) continue;
                    if (customButtonFilter != null && !customButtonFilter(mb.gameObject)) continue;
                    if (namePredicate(mb.gameObject.name))
                        return mb.gameObject;
                }
            }
            else
            {
                foreach (var btn in GetActiveCustomButtons())
                {
                    if (btn == null) continue;
                    if (customButtonFilter != null && !customButtonFilter(btn)) continue;
                    if (namePredicate(btn.name))
                        return btn;
                }
            }

            return null;
        }

        /// <summary>
        /// Find a close/dismiss button within a panel.
        /// </summary>
        private GameObject FindCloseButtonInPanel(GameObject panel)
        {
            return FindButtonByPredicate(panel, name =>
            {
                var lower = name.ToLowerInvariant();
                return lower.Contains("close") || lower.Contains("dismiss") ||
                       lower.Contains("cancel") || lower.Contains("back");
            });
        }

        /// <summary>
        /// Find a generic back/close button on the current screen.
        /// </summary>
        private GameObject FindGenericBackButton()
        {
            return FindButtonByPredicate(
                panel: null,
                namePredicate: name => !name.Contains("DismissButton") && IsBackButtonName(name),
                customButtonFilter: btn => !UITextExtractor.HasActualText(btn)
            );
        }

        /// <summary>
        /// Check if a button name matches back/close patterns.
        /// </summary>
        private static bool IsBackButtonName(string name)
        {
            return name.IndexOf("back", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name == "MainButtonOutline" ||
                   name.IndexOf("Blade_Close", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Blade_Arrow", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Note: HandleSettingsBack(), FindSettingsBackButton(), CloseSettingsMenu() removed
        // Settings navigation is now handled by SettingsMenuNavigator

        /// <summary>
        /// Close the PlayBlade by finding and activating the dismiss button.
        /// </summary>
        private bool ClosePlayBlade()
        {
            LogDebug($"[{NavigatorId}] Attempting to close PlayBlade");

            var bladeIsOpenButton = GameObject.Find("Btn_BladeIsOpen");
            if (bladeIsOpenButton != null && bladeIsOpenButton.activeInHierarchy)
            {
                LogDebug($"[{NavigatorId}] Found Btn_BladeIsOpen, activating");
                _announcer.Announce(Models.Strings.ClosingPlayBlade, Models.AnnouncementPriority.High);
                UIActivator.Activate(bladeIsOpenButton);
                ClearBladeStateAndRescan();
                return true;
            }

            var dismissButton = GameObject.Find("Blade_DismissButton");
            if (dismissButton != null && dismissButton.activeInHierarchy)
            {
                LogDebug($"[{NavigatorId}] Found Blade_DismissButton, activating");
                _announcer.Announce(Models.Strings.ClosingPlayBlade, Models.AnnouncementPriority.High);
                UIActivator.Activate(dismissButton);
                ClearBladeStateAndRescan();
                return true;
            }

            var bladeIsClosed = GameObject.Find("Btn_BladeIsClosed");
            if (bladeIsClosed != null && bladeIsClosed.activeInHierarchy)
            {
                var parent = bladeIsClosed.transform;
                while (parent != null)
                {
                    if (parent.name.Contains("CampaignGraphPage"))
                    {
                        LogDebug($"[{NavigatorId}] CampaignGraph blade detected, navigating Home");
                        ClearBladeStateAndRescan();
                        return NavigateToHome();
                    }
                    parent = parent.parent;
                }
            }

            LogDebug($"[{NavigatorId}] ClosePlayBlade called but no close button found - check foreground detection");
            return false;
        }

        /// <summary>
        /// Clear blade state and trigger immediate rescan.
        /// </summary>
        private void ClearBladeStateAndRescan()
        {
            LogDebug($"[{NavigatorId}] Clearing blade state for immediate Home navigation");
            ReportPanelClosedByName("PlayBlade");
            PanelStateManager.Instance?.SetPlayBladeState(0);
            TriggerRescan();
        }

        /// <summary>
        /// Debug: Dump current UI hierarchy to log for development.
        /// Press F12 to trigger this after opening a new UI overlay.
        /// </summary>
        private void DumpUIHierarchy()
        {
            MenuDebugHelper.DumpUIHierarchy(NavigatorId, _announcer);
        }

        /// <summary>
        /// Toggle the Friends/Social panel by calling SocialUI methods directly.
        /// </summary>
        private void ToggleFriendsPanel()
        {
            LogDebug($"[{NavigatorId}] ToggleFriendsPanel called");

            var socialPanel = GameObject.Find("SocialUI_V2_Desktop_16x9(Clone)");
            if (socialPanel == null)
            {
                LogDebug($"[{NavigatorId}] Social UI panel not found");
                return;
            }

            // Get the SocialUI component
            var socialUI = socialPanel.GetComponent("SocialUI");
            if (socialUI == null)
            {
                LogDebug($"[{NavigatorId}] SocialUI component not found");
                return;
            }

            bool isOpen = IsSocialPanelOpen();
            LogDebug($"[{NavigatorId}] Toggling Friends panel (isOpen: {isOpen})");

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
                        LogDebug($"[{NavigatorId}] Called SocialUI.CloseFriendsWidget()");
                        ReportPanelClosed(socialPanel);
                        TriggerRescan();
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
                        LogDebug($"[{NavigatorId}] Called SocialUI.ShowSocialEntitiesList()");
                        ReportPanelOpened("Social", socialPanel, PanelDetectionMethod.Reflection);
                        TriggerRescan();
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
        /// </summary>
        private bool NavigateToHome()
        {
            var navBar = GameObject.Find("NavBar_Desktop_16x9(Clone)");
            if (navBar == null)
            {
                navBar = GameObject.Find("NavBar");
            }

            if (navBar == null)
            {
                LogDebug($"[{NavigatorId}] NavBar not found for Home navigation");
                _announcer.Announce(Models.Strings.CannotNavigateHome, Models.AnnouncementPriority.High);
                return true;
            }

            var homeButtonTransform = navBar.transform.Find("Base/Nav_Home");
            GameObject homeButton = homeButtonTransform?.gameObject;
            if (homeButton == null)
            {
                homeButton = FindChildByName(navBar.transform, "Nav_Home");
            }

            if (homeButton == null || !homeButton.activeInHierarchy)
            {
                LogDebug($"[{NavigatorId}] Home button not found or inactive");
                _announcer.Announce(Models.Strings.HomeNotAvailable, Models.AnnouncementPriority.High);
                return true;
            }

            LogDebug($"[{NavigatorId}] Navigating to Home via Backspace");
            _announcer.Announce(Models.Strings.ReturningHome, Models.AnnouncementPriority.High);
            UIActivator.Activate(homeButton);

            return true;
        }

        /// <summary>
        /// Check if element should be shown based on current foreground layer.
        /// Uses OverlayDetector for overlay detection, falls back to content panel filtering.
        /// </summary>
        private bool ShouldShowElement(GameObject obj)
        {
            // Check if an overlay is active using the new OverlayDetector
            var activeOverlay = _overlayDetector.GetActiveOverlay();

            if (activeOverlay != null)
            {
                // An overlay is active - use OverlayDetector to filter
                return _overlayDetector.IsInsideActiveOverlay(obj);
            }

            // No overlay active - fall back to content panel filtering
            // This keeps existing behavior for ContentPanel and Home cases
            var layer = GetCurrentForeground();

            return layer switch
            {
                ForegroundLayer.ContentPanel => IsChildOfContentPanel(obj),

                ForegroundLayer.Home => IsChildOfHomeOrNavBar(obj),

                ForegroundLayer.None => true, // Show everything

                _ => true
            };
        }

        /// <summary>
        /// Check if element is inside the Social panel.
        /// </summary>
        private bool IsChildOfSocialPanel(GameObject obj)
        {
            var socialPanel = GameObject.Find("SocialUI_V2_Desktop_16x9(Clone)");
            return socialPanel != null && IsChildOf(obj, socialPanel);
        }

        /// <summary>
        /// Check if element is inside the current content panel.
        /// Special handling for CampaignGraph which allows blade elements.
        /// </summary>
        private bool IsChildOfContentPanel(GameObject obj)
        {
            if (_activeControllerGameObject == null)
                return true;

            // Special case: Color Challenge allows blade elements and main buttons
            if (_activeContentController == "CampaignGraphContentController")
            {
                if (IsChildOf(obj, _activeControllerGameObject)) return true;
                if (IsInsideBlade(obj)) return true;
                if (IsMainButton(obj)) return true;
                return false;
            }

            // Normal content panel: only show its elements
            return IsChildOf(obj, _activeControllerGameObject);
        }

        /// <summary>
        /// Check if element is inside Home page content or NavBar.
        /// </summary>
        private bool IsChildOfHomeOrNavBar(GameObject obj)
        {
            if (_activeControllerGameObject != null && IsChildOf(obj, _activeControllerGameObject))
                return true;
            if (_navBarGameObject != null && IsChildOf(obj, _navBarGameObject))
                return true;
            return false;
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
        private void TriggerRescan()
        {
            _rescanDelay = RescanDelaySeconds;
        }

        /// <summary>
        /// Perform rescan of available elements.
        /// </summary>
        private void PerformRescan()
        {
            // Detect active controller BEFORE discovering elements so filtering works correctly
            DetectActiveContentController();
            LogDebug($"[{NavigatorId}] Rescanning elements after panel change (controller: {_activeContentController ?? "none"})");

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
                        LogDebug($"[{NavigatorId}] Preserved selection at index {i}: {previousSelection.name}");
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

            // Don't activate when Settings is open - let SettingsMenuNavigator handle it
            if (IsSettingsMenuOpen())
                return false;

            // Wait for UI to settle after scene change
            if (_activationDelay > 0)
            {
                _activationDelay -= Time.deltaTime;
                return false;
            }

            // Note: We don't check for overlay blockers here because GetCurrentForeground()
            // handles all overlay filtering when we're active. If there are navigable
            // buttons, we should activate and let the foreground layer system handle filtering.

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

            LogDebug($"[{NavigatorId}] Detected menu: {_detectedMenuType} with {customButtonCount} CustomButtons");
            return true;
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
            MenuDebugHelper.LogAvailableUIElements(NavigatorId, _currentScene, GetActiveCustomButtons);
        }

        protected override void DiscoverElements()
        {
            // Detect active controller first so filtering works correctly
            DetectActiveContentController();

            // Debug: Dump DeckFolder hierarchy on DeckManager screen
            if (DebugLogging && _activeContentController == "DeckManagerController")
            {
                LogDebug($"[{NavigatorId}] === DECK FOLDER HIERARCHY ===");
                var deckFolders = GameObject.FindObjectsOfType<Transform>()
                    .Where(t => t.name.Contains("DeckFolder_Base"))
                    .ToArray();
                LogDebug($"[{NavigatorId}] Found {deckFolders.Length} DeckFolder_Base instances");
                foreach (var folder in deckFolders)
                {
                    if (folder.gameObject.activeInHierarchy)
                    {
                        LogDebug($"[{NavigatorId}] DeckFolder: {folder.name} (active)");
                        LogHierarchy(folder, "  ", 5);
                    }
                }
                LogDebug($"[{NavigatorId}] === END DECK FOLDER HIERARCHY ===");
            }

            var addedObjects = new HashSet<GameObject>();
            var discoveredElements = new List<(GameObject obj, UIElementClassifier.ClassificationResult classification, float sortOrder)>();

            // Log panel filter state
            if (_foregroundPanel != null)
            {
                LogDebug($"[{NavigatorId}] Filtering to panel: {_foregroundPanel.name}");
            }
            else if (_activeControllerGameObject != null)
            {
                LogDebug($"[{NavigatorId}] Filtering to controller: {_activeContentController}");
            }

            // Helper to process and classify a UI element
            void TryAddElement(GameObject obj)
            {
                if (obj == null || !obj.activeInHierarchy) return;
                if (addedObjects.Contains(obj)) return;

                if (!ShouldShowElement(obj))
                    return;

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

            // Find TMP_Dropdown elements
            foreach (var dropdown in GameObject.FindObjectsOfType<TMP_Dropdown>())
            {
                if (dropdown != null && dropdown.interactable)
                    TryAddElement(dropdown.gameObject);
            }

            // Note: Settings custom controls (dropdowns, steppers) now handled by SettingsMenuNavigator

            // Find deck toolbar buttons for attached actions (Delete, Edit, Export)
            // These are in DeckManager_Desktop_16x9(Clone)/SafeArea/MainButtons/
            var deckToolbarButtons = FindDeckToolbarButtons();

            // Process deck entries: pair main buttons with their TextBox edit buttons
            // Each deck entry has UI (CustomButton for selection) and TextBox (for editing name)
            // Multiple elements per deck may have ", deck" label - we only keep the "UI" one
            var deckPairs = new Dictionary<Transform, (GameObject mainButton, GameObject editButton)>();
            var allDeckElements = new HashSet<GameObject>(); // Track ALL elements inside deck entries

            foreach (var (obj, classification, _) in discoveredElements)
            {
                // Find the DeckView_Base parent to group elements by deck entry
                Transform deckViewParent = FindDeckViewParent(obj.transform);
                if (deckViewParent == null) continue;

                // Track all elements that are part of a deck entry
                allDeckElements.Add(obj);

                if (!deckPairs.ContainsKey(deckViewParent))
                {
                    deckPairs[deckViewParent] = (null, null);
                }

                var pair = deckPairs[deckViewParent];

                // UI element (CustomButton) is the main selection button - this is what we keep
                // TextBox element is for editing the deck name (has TMP_InputField)
                if (obj.name == "UI" && classification.Label.Contains(", deck"))
                {
                    deckPairs[deckViewParent] = (obj, pair.editButton);
                }
                else if (obj.name == "TextBox" && obj.GetComponentInChildren<TMP_InputField>() != null)
                {
                    deckPairs[deckViewParent] = (pair.mainButton, obj);
                }
            }

            // Build set of elements to keep (only the UI main buttons)
            var deckMainButtons = new HashSet<GameObject>(deckPairs.Values
                .Where(p => p.mainButton != null)
                .Select(p => p.mainButton));

            // Elements to skip = all deck elements EXCEPT the main buttons we're keeping
            var deckElementsToSkip = new HashSet<GameObject>(allDeckElements.Where(e => !deckMainButtons.Contains(e)));

            // Map main deck buttons to their edit buttons for alternate action
            var deckEditButtons = deckPairs
                .Where(p => p.Value.mainButton != null && p.Value.editButton != null)
                .ToDictionary(p => p.Value.mainButton, p => p.Value.editButton);

            // Sort by position and add elements with proper labels
            foreach (var (obj, classification, _) in discoveredElements.OrderBy(x => x.sortOrder))
            {
                // Skip deck elements that aren't the main UI button (TextBox, duplicates, etc.)
                if (deckElementsToSkip.Contains(obj))
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

                // Check if this is a deck button - attach actions for left/right cycling
                List<AttachedAction> attachedActions = null;
                if (deckMainButtons.Contains(obj))
                {
                    // Check if deck is selected and add to announcement
                    if (UIActivator.IsDeckSelected(obj))
                    {
                        announcement += ", selected";
                    }

                    // Get the rename button (TextBox) for this deck
                    GameObject renameButton = deckEditButtons.TryGetValue(obj, out var editBtn) ? editBtn : null;
                    attachedActions = BuildDeckAttachedActions(deckToolbarButtons, renameButton);

                    if (attachedActions.Count > 0)
                    {
                        LogDebug($"[{NavigatorId}] Deck '{announcement}' has {attachedActions.Count} attached actions");
                    }
                }

                AddElement(obj, announcement, carouselInfo, null, attachedActions);
            }

            // Find NPE reward cards (displayed cards on reward screens)
            // These are not buttons but should be navigable to read card info
            FindNPERewardCards(addedObjects);

            LogDebug($"[{NavigatorId}] Discovered {_elements.Count} navigable elements");

            // Organize elements into groups for hierarchical navigation
            if (_groupedNavigationEnabled && _elements.Count > 0)
            {
                var elementsForGrouping = _elements.Select(e => (e.GameObject, e.Label));
                _groupedNavigator.OrganizeIntoGroups(elementsForGrouping);
            }

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
            // Note: Settings check removed - SettingsMenuNavigator takes priority when settings is open

            // Check if we're on the NPE rewards screen
            if (!_screenDetector.IsNPERewardsScreenActive())
                return;

            LogDebug($"[{NavigatorId}] NPE Rewards screen detected, searching for reward cards...");

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
                        LogDebug($"[{NavigatorId}] RewardsCONTAINER hierarchy (depth 6):");
                        if (DebugLogging) LogHierarchy(rewardsContainer, "  ", 6);
                    }
                    else
                    {
                        LogDebug($"[{NavigatorId}] ActiveContainer hierarchy (depth 5):");
                        if (DebugLogging) LogHierarchy(activeContainer, "  ", 5);
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
                        LogDebug($"[{NavigatorId}] Found potential NPE card element: {name} at {path}");
                        if (!addedObjects.Contains(transform.gameObject))
                        {
                            cardPrefabs.Add(transform.gameObject);
                        }
                    }
                }
            }

            if (cardPrefabs.Count == 0)
            {
                LogDebug($"[{NavigatorId}] No NPE reward cards found in NPE-Rewards_Container");
                return;
            }

            // Sort cards by X position (left to right)
            cardPrefabs = cardPrefabs.OrderBy(c => c.transform.position.x).ToList();

            LogDebug($"[{NavigatorId}] Found {cardPrefabs.Count} NPE reward card(s)");

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

                LogDebug($"[{NavigatorId}] Adding NPE reward card: {label}");

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
                    LogDebug($"[{NavigatorId}] Adding NullClaimButton as 'Take reward' button (path: {GetFullPath(claimButton)})");
                    AddElement(claimButton.gameObject, "Take reward, button");
                    addedObjects.Add(claimButton.gameObject);
                }
                else if (claimButton == null)
                {
                    LogDebug($"[{NavigatorId}] NullClaimButton not found in NPE-Rewards_Container hierarchy");
                }
                else
                {
                    LogDebug($"[{NavigatorId}] NullClaimButton already in addedObjects (ID:{claimButton.gameObject.GetInstanceID()})");
                }
            }
            else
            {
                LogDebug($"[{NavigatorId}] NPE-Rewards_Container not found for NullClaimButton lookup");
            }
        }

        /// <summary>
        /// Log the hierarchy of a transform for debugging purposes.
        /// </summary>
        private void LogHierarchy(Transform parent, string indent, int maxDepth)
        {
            MenuDebugHelper.LogHierarchy(NavigatorId, parent, indent, maxDepth);
        }

        /// <summary>
        /// Get the full path of a transform in the hierarchy.
        /// </summary>
        private string GetFullPath(Transform t) => MenuDebugHelper.GetFullPath(t);

        // Note: FindSettingsCustomControls() and FindClickableInDropdownControl() removed
        // Settings controls are now handled by SettingsMenuNavigator

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
                        LogDebug($"[{NavigatorId}] Moved Continue button to first position");
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
                        LogDebug($"[{NavigatorId}] Moved unlocked card to first position: {cardElement.Label}");
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

        /// <summary>
        /// Structure to hold deck toolbar buttons for attached actions.
        /// </summary>
        private struct DeckToolbarButtons
        {
            public GameObject EditButton;   // EditDeck_MainButtonBlue
            public GameObject DeleteButton; // Delete_MainButton_Round
            public GameObject ExportButton; // Export_MainButton_Round
        }

        /// <summary>
        /// Find the deck toolbar buttons (Delete, Edit, Export) in the DeckManager screen.
        /// These buttons act on the currently selected deck.
        /// </summary>
        private DeckToolbarButtons FindDeckToolbarButtons()
        {
            var result = new DeckToolbarButtons();

            // Find MainButtons container in DeckManager
            var mainButtonsContainer = GameObject.FindObjectsOfType<Transform>()
                .FirstOrDefault(t => t.name == "MainButtons" &&
                                    t.parent != null &&
                                    t.parent.name == "SafeArea" &&
                                    t.gameObject.activeInHierarchy);

            if (mainButtonsContainer == null)
            {
                MelonLogger.Msg($"[{NavigatorId}] DeckManager MainButtons container not found");
                return result;
            }

            // Find each button by name
            foreach (Transform child in mainButtonsContainer)
            {
                if (!child.gameObject.activeInHierarchy) continue;

                if (child.name.Contains("Delete"))
                {
                    result.DeleteButton = child.gameObject;
                }
                else if (child.name.Contains("EditDeck"))
                {
                    result.EditButton = child.gameObject;
                }
                else if (child.name.Contains("Export"))
                {
                    result.ExportButton = child.gameObject;
                }
            }

            return result;
        }

        /// <summary>
        /// Build attached actions list for a deck element.
        /// </summary>
        private List<AttachedAction> BuildDeckAttachedActions(DeckToolbarButtons toolbarButtons, GameObject renameButton)
        {
            var actions = new List<AttachedAction>();

            // Rename (TextBox button on the deck)
            if (renameButton != null)
            {
                actions.Add(new AttachedAction { Label = "Rename", TargetButton = renameButton });
            }

            // Edit (open deck builder)
            if (toolbarButtons.EditButton != null)
            {
                actions.Add(new AttachedAction { Label = "Edit", TargetButton = toolbarButtons.EditButton });
            }

            // Export
            if (toolbarButtons.ExportButton != null)
            {
                actions.Add(new AttachedAction { Label = "Export", TargetButton = toolbarButtons.ExportButton });
            }

            // Delete (last, as it's destructive)
            if (toolbarButtons.DeleteButton != null)
            {
                actions.Add(new AttachedAction { Label = "Delete", TargetButton = toolbarButtons.DeleteButton });
            }

            return actions;
        }

        protected string GetGameObjectPath(GameObject obj) => MenuDebugHelper.GetGameObjectPath(obj);

        protected override string GetActivationAnnouncement()
        {
            string menuName = GetMenuScreenName();
            if (_elements.Count == 0)
            {
                return $"{menuName}. No navigable items found.";
            }

            // Use grouped navigator announcement when enabled
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                return _groupedNavigator.GetActivationAnnouncement(menuName);
            }

            return $"{menuName}. {_elements.Count} items. {Models.Strings.NavigateWithArrows}, Enter to select.";
        }

        #region Grouped Navigation Overrides

        /// <summary>
        /// Override MoveNext to use GroupedNavigator when grouped navigation is enabled.
        /// </summary>
        protected override void MoveNext()
        {
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                _groupedNavigator.MoveNext();
                return;
            }
            base.MoveNext();
        }

        /// <summary>
        /// Override MovePrevious to use GroupedNavigator when grouped navigation is enabled.
        /// </summary>
        protected override void MovePrevious()
        {
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                _groupedNavigator.MovePrevious();
                return;
            }
            base.MovePrevious();
        }

        /// <summary>
        /// Override MoveFirst to use GroupedNavigator when grouped navigation is enabled.
        /// </summary>
        protected override void MoveFirst()
        {
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                _groupedNavigator.MoveFirst();
                _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                return;
            }
            base.MoveFirst();
        }

        /// <summary>
        /// Override MoveLast to use GroupedNavigator when grouped navigation is enabled.
        /// </summary>
        protected override void MoveLast()
        {
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                _groupedNavigator.MoveLast();
                _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                return;
            }
            base.MoveLast();
        }

        /// <summary>
        /// Handle Enter key for grouped navigation.
        /// When at group level with standalone element, activates it directly.
        /// When at group level with normal group, enters the group.
        /// When inside a group, activates the element.
        /// </summary>
        private bool HandleGroupedEnter()
        {
            if (!_groupedNavigationEnabled || !_groupedNavigator.IsActive)
                return false;

            if (_groupedNavigator.Level == NavigationLevel.GroupList)
            {
                // Check if this is a standalone element (Primary action button)
                if (_groupedNavigator.IsCurrentGroupStandalone)
                {
                    // Standalone element - activate directly without entering group
                    var standaloneObj = _groupedNavigator.GetStandaloneElement();
                    if (standaloneObj != null)
                    {
                        // Find the element in our _elements list and activate it
                        for (int i = 0; i < _elements.Count; i++)
                        {
                            if (_elements[i].GameObject == standaloneObj)
                            {
                                _currentIndex = i;
                                ActivateCurrentElement();
                                return true;
                            }
                        }
                    }
                }

                // Check if this is a folder group - activate its toggle through normal path
                var currentGroup = _groupedNavigator.CurrentGroup;
                if (currentGroup.HasValue && currentGroup.Value.IsFolderGroup && currentGroup.Value.FolderToggle != null)
                {
                    var toggleObj = currentGroup.Value.FolderToggle;
                    var toggle = toggleObj.GetComponent<Toggle>();

                    // Only activate (toggle on) if not already checked
                    // This prevents toggling OFF a folder that's already visible
                    if (toggle != null && !toggle.isOn)
                    {
                        // Find the folder toggle in our _elements list and activate it normally
                        // This goes through OnElementActivated which triggers rescan for toggles
                        for (int i = 0; i < _elements.Count; i++)
                        {
                            if (_elements[i].GameObject == toggleObj)
                            {
                                _currentIndex = i;
                                ActivateCurrentElement();
                                break;
                            }
                        }
                    }

                    // Always enter the group (whether we toggled or not)
                    _groupedNavigator.EnterGroup();
                    return true;
                }

                // Normal group - enter it
                if (_groupedNavigator.EnterGroup())
                {
                    _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                    return true;
                }
            }
            else
            {
                // Inside a group - activate the current element
                var currentElement = _groupedNavigator.CurrentElement;
                if (currentElement.HasValue && currentElement.Value.GameObject != null)
                {
                    // Find the element in our _elements list and activate it
                    for (int i = 0; i < _elements.Count; i++)
                    {
                        if (_elements[i].GameObject == currentElement.Value.GameObject)
                        {
                            _currentIndex = i;
                            ActivateCurrentElement();
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Handle Backspace key for grouped navigation.
        /// When inside a group, exits to group level. Otherwise, uses normal back navigation.
        /// </summary>
        private bool HandleGroupedBackspace()
        {
            if (!_groupedNavigationEnabled || !_groupedNavigator.IsActive)
                return false;

            if (_groupedNavigator.Level == NavigationLevel.InsideGroup)
            {
                // Inside a group - exit to group level
                if (_groupedNavigator.ExitGroup())
                {
                    _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                    return true;
                }
            }

            // At group level or exit failed - fall through to normal back navigation
            return false;
        }

        #endregion

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


            // Note: Settings submenu button handling removed - handled by SettingsMenuNavigator

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
                    LogDebug($"[{NavigatorId}] Toggle activated - forcing rescan in {RescanDelaySeconds}s (bypassing debounce)");
                    TriggerRescan();
                }
                else
                {
                    LogDebug($"[{NavigatorId}] Toggle activated on Login panel - skipping rescan");
                }
                return true;
            }

            // For input field activations, trigger a rescan after a short delay
            // The UI might change (e.g., Send button appearing) when entering an input field
            if (isInputField)
            {
                LogDebug($"[{NavigatorId}] Input field activated - scheduling rescan");
                TriggerRescan();
                return true;
            }

            // Dropdowns: just activate, focus-based detection in UIFocusTracker handles edit mode
            // When focus goes to dropdown items, we automatically enter dropdown mode
            if (isDropdown)
            {
                LogDebug($"[{NavigatorId}] Dropdown activated ({element.name})");
                return true;
            }

            // For popup/dialog button activations (OK, Cancel), the popup closes with animation
            // Popup button click - unified detector will handle the visibility change
            // No cooldown needed - alpha state comparison will detect when popup closes
            if (isPopupButton)
            {
                LogDebug($"[{NavigatorId}] Popup button activated ({element.name})");
                // Unified detector will detect popup close via alpha change
                return true;
            }

            return true;
        }

        // Note: IsSettingsSubmenuButton() removed - handled by SettingsMenuNavigator

        /// <summary>
        /// Auto-expand the play blade when it's in collapsed state.
        /// Used for panels like Color Challenge where the blade starts collapsed.
        /// </summary>
        private void AutoExpandBlade()
        {
            LogDebug($"[{NavigatorId}] Attempting blade auto-expand");

            // Find the blade expand button (Btn_BladeIsClosed or its arrow child)
            var bladeButton = GetActiveCustomButtons()
                .FirstOrDefault(obj => obj.name.Contains("BladeHoverClosed") || obj.name.Contains("Btn_BladeIsClosed"));

            if (bladeButton != null)
            {
                LogDebug($"[{NavigatorId}] Auto-expanding blade via {bladeButton.name}");
                _announcer.Announce(Models.Strings.OpeningColorChallenges, Models.AnnouncementPriority.High);
                UIActivator.Activate(bladeButton);

                // Schedule a rescan after the blade opens
                TriggerRescan();
            }
            else
            {
                LogDebug($"[{NavigatorId}] Could not find blade expand button");
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
            if (PanelStateManager.Instance == null || !PanelStateManager.Instance.IsPlayBladeActive)
                return null;

            // PlayBlade states: 0=Hidden, 1=Events, 2=DirectChallenge, 3=FriendChallenge
            return PanelStateManager.Instance.PlayBladeState switch
            {
                1 => "Play Mode Selection",
                2 => "Direct Challenge",
                3 => "Friend Challenge",
                _ => null
            };
        }

        /// <summary>
        /// Map content controller type name to user-friendly screen name.
        /// </summary>
        private string GetContentControllerDisplayName(string controllerTypeName) =>
            _screenDetector.GetContentControllerDisplayName(controllerTypeName);

        #endregion
    }
}
