using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Services.PanelDetection;
using AccessibleArena.Core.Services.ElementGrouping;
using AccessibleArena.Patches;
using System;
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
        private readonly PlayBladeNavigationHelper _playBladeHelper;

        /// <summary>
        /// Whether grouped (hierarchical) navigation is enabled.
        /// When true, elements are organized into groups for two-level navigation.
        /// </summary>
        private bool _groupedNavigationEnabled = true;

        // Rescan timing
        private float _rescanDelay;

        // Mail detail view tracking
        private bool _isInMailDetailView;
        private Guid _currentMailLetterId;

        // Mail content field navigation
        private UITextExtractor.MailContentParts _mailContentParts;

        // Element tracking
        private float _bladeAutoExpandDelay;

        // NPE scene: periodic button check for dynamic buttons (e.g., "Play" appearing after dialogue)
        private const float NPEButtonCheckInterval = 0.5f;
        private float _npeButtonCheckTimer;
        private int _lastNPEButtonCount;

        // Overlay state tracking - detect when overlays open/close to trigger rescans
        private ElementGroup? _lastKnownOverlay;

        // Booster carousel state - treated as single carousel element with left/right navigation
        private List<GameObject> _boosterPackHitboxes = new List<GameObject>();
        private int _boosterCarouselIndex = 0;
        private bool _isBoosterCarouselActive = false;

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
            _playBladeHelper = new PlayBladeNavigationHelper(_groupedNavigator);

            // Subscribe to PanelStateManager for rescan triggers
            if (PanelStateManager.Instance != null)
            {
                PanelStateManager.Instance.OnPanelChanged += OnPanelStateManagerActiveChanged;
                PanelStateManager.Instance.OnAnyPanelOpened += OnPanelStateManagerAnyOpened;
            }

            // Subscribe to mail letter selection events
            PanelStatePatch.OnMailLetterSelected += OnMailLetterSelected;
        }

        /// <summary>
        /// Handler for PanelStateManager.OnPanelChanged - fires when active (filtering) panel changes.
        /// </summary>
        private void OnPanelStateManagerActiveChanged(PanelInfo oldPanel, PanelInfo newPanel)
        {
            if (!_isActive) return;

            // ALWAYS check PlayBlade state first, before any early returns
            // PlayBlade state can change even when panel source is SocialUI
            CheckAndInitPlayBladeHelper("ActiveChanged");

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

            // Reset mail detail view state when mailbox closes
            if (oldPanel?.Name?.Contains("Mailbox") == true && newPanel?.Name?.Contains("Mailbox") != true)
            {
                if (_isInMailDetailView)
                {
                    LogDebug($"[{NavigatorId}] Mailbox closed - resetting mail detail view state");
                    _isInMailDetailView = false;
                    _currentMailLetterId = Guid.Empty;
                    ResetMailFieldNavigation();
                }
            }

            LogDebug($"[{NavigatorId}] PanelStateManager active changed: {oldPanel?.Name ?? "none"} -> {newPanel?.Name ?? "none"}");

            TriggerRescan();
        }

        /// <summary>
        /// Check if PlayBlade became active and initialize the helper if needed.
        /// Called from multiple event handlers to ensure we catch the blade opening.
        /// </summary>
        private void CheckAndInitPlayBladeHelper(string source)
        {
            bool isPlayBladeNowActive = PanelStateManager.Instance?.IsPlayBladeActive == true;
            bool helperIsActive = _playBladeHelper.IsActive;

            MelonLogger.Msg($"[{NavigatorId}] CheckPlayBlade({source}): IsPlayBladeActive={isPlayBladeNowActive}, HelperIsActive={helperIsActive}");

            if (isPlayBladeNowActive && !helperIsActive)
            {
                MelonLogger.Msg($"[{NavigatorId}] PlayBlade became active - initializing helper");
                _playBladeHelper.OnPlayBladeOpened("PlayBlade");
            }
            else if (!isPlayBladeNowActive && helperIsActive)
            {
                MelonLogger.Msg($"[{NavigatorId}] PlayBlade became inactive - resetting helper");
                _playBladeHelper.OnPlayBladeClosed();
            }
        }

        /// <summary>
        /// Handler for PanelStateManager.OnAnyPanelOpened - fires when ANY panel opens.
        /// Used for triggering rescans on screens that don't filter (e.g., HomePage).
        /// Also handles special behaviors like Color Challenge blade auto-expand.
        /// </summary>
        private void OnPanelStateManagerAnyOpened(PanelInfo panel)
        {
            if (!_isActive) return;

            // ALWAYS check PlayBlade state first, before any early returns
            CheckAndInitPlayBladeHelper("AnyOpened");

            // SocialUI events: only rescan if the Friends panel is actually open
            // The corner social button triggers SocialUI events but shouldn't cause rescans
            // Note: Mailbox is detected separately via ContentControllerPlayerInbox patch
            if (panel?.Name?.Contains("SocialUI") == true)
            {
                if (!_screenDetector.IsSocialPanelOpen())
                {
                    LogDebug($"[{NavigatorId}] Ignoring SocialUI event - Friends panel not open (just corner icon)");
                    return;
                }
                LogDebug($"[{NavigatorId}] SocialUI event with Friends panel open - allowing rescan");
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

            // Announce popup body text when a popup/dialog opens
            if (panel?.GameObject != null && IsPopupOverlay(panel.GameObject))
            {
                AnnouncePopupBodyText(panel.GameObject);
            }

            TriggerRescan();
        }

        /// <summary>
        /// Handler for PanelStatePatch.OnMailLetterSelected - fires when a mail is opened in the mailbox.
        /// </summary>
        private void OnMailLetterSelected(Guid letterId, string title, string body, bool hasAttachments, bool isClaimed)
        {
            if (!_isActive) return;

            LogDebug($"[{NavigatorId}] Mail letter selected: {letterId}");

            // Track that we're now in the mail detail view
            _isInMailDetailView = true;
            _currentMailLetterId = letterId;

            // Trigger rescan to discover mail content fields and buttons
            // The mail content fields will be added during element discovery
            TriggerRescan();
        }

        /// <summary>
        /// Add mail content fields (title, date, body) as navigable elements.
        /// These are actual TMP_Text GameObjects from the mail UI, inserted before buttons.
        /// </summary>
        private void AddMailContentFieldsAsElements()
        {
            // Extract mail content from UI (includes actual GameObjects)
            _mailContentParts = UITextExtractor.GetMailContentParts();

            LogDebug($"[{NavigatorId}] Mail content - Title: '{_mailContentParts.Title}' (obj: {_mailContentParts.TitleObject?.name}), Date: '{_mailContentParts.Date}', Body length: {_mailContentParts.Body?.Length ?? 0}");

            if (!_mailContentParts.HasContent)
                return;

            // Create list of mail field elements (to be inserted at the beginning)
            var mailFieldElements = new List<NavigableElement>();

            if (_mailContentParts.TitleObject != null && !string.IsNullOrEmpty(_mailContentParts.Title))
            {
                mailFieldElements.Add(new NavigableElement
                {
                    GameObject = _mailContentParts.TitleObject,
                    Label = $"Title: {_mailContentParts.Title}"
                });
            }

            if (_mailContentParts.DateObject != null && !string.IsNullOrEmpty(_mailContentParts.Date))
            {
                mailFieldElements.Add(new NavigableElement
                {
                    GameObject = _mailContentParts.DateObject,
                    Label = $"Date: {_mailContentParts.Date}"
                });
            }

            if (_mailContentParts.BodyObject != null && !string.IsNullOrEmpty(_mailContentParts.Body))
            {
                mailFieldElements.Add(new NavigableElement
                {
                    GameObject = _mailContentParts.BodyObject,
                    Label = $"Body: {_mailContentParts.Body}"
                });
            }

            // Insert mail fields at the beginning
            if (mailFieldElements.Count > 0)
            {
                _elements.InsertRange(0, mailFieldElements);
                LogDebug($"[{NavigatorId}] Added {mailFieldElements.Count} mail content fields as navigable elements");
            }
        }

        /// <summary>
        /// Reset mail field navigation state.
        /// </summary>
        private void ResetMailFieldNavigation()
        {
            _mailContentParts = default;
        }

        #region Booster Carousel

        /// <summary>
        /// Check if an element is inside a CarouselBooster parent.
        /// </summary>
        private bool IsInsideCarouselBooster(GameObject obj)
        {
            if (obj == null) return false;

            Transform current = obj.transform.parent;
            int maxLevels = 6;

            while (current != null && maxLevels > 0)
            {
                if (current.name.Contains("CarouselBooster"))
                    return true;
                current = current.parent;
                maxLevels--;
            }

            return false;
        }

        /// <summary>
        /// Add a single carousel element representing all booster packs.
        /// Uses the current index to show the selected pack name.
        /// </summary>
        private void AddBoosterCarouselElement()
        {
            if (_boosterPackHitboxes.Count == 0) return;

            // Sort packs by X position (left to right)
            _boosterPackHitboxes = _boosterPackHitboxes
                .OrderBy(p => p.transform.position.x)
                .ToList();

            // Clamp index to valid range
            if (_boosterCarouselIndex >= _boosterPackHitboxes.Count)
                _boosterCarouselIndex = 0;
            if (_boosterCarouselIndex < 0)
                _boosterCarouselIndex = _boosterPackHitboxes.Count - 1;

            // Get the current pack and its name
            var currentPack = _boosterPackHitboxes[_boosterCarouselIndex];
            string packName = UITextExtractor.GetText(currentPack);
            if (string.IsNullOrEmpty(packName))
                packName = "Pack";

            // Build the carousel label with position info
            string label = $"{packName}, {_boosterCarouselIndex + 1} of {_boosterPackHitboxes.Count}, use left and right arrows";

            // Add as navigable element with carousel info
            var carouselInfo = new CarouselInfo
            {
                HasArrowNavigation = true,
                PreviousControl = null, // We handle navigation ourselves
                NextControl = null
            };

            AddElement(currentPack, label, carouselInfo);
            LogDebug($"[{NavigatorId}] Added booster carousel: {label}");
        }

        /// <summary>
        /// Navigate the booster carousel (left/right).
        /// Clicks the target pack to center it in the carousel.
        /// </summary>
        /// <param name="isNext">True for right/next, false for left/previous</param>
        /// <returns>True if navigation was handled</returns>
        private bool HandleBoosterCarouselNavigation(bool isNext)
        {
            if (!_isBoosterCarouselActive || _boosterPackHitboxes.Count == 0)
                return false;

            // Calculate new index
            int newIndex = _boosterCarouselIndex + (isNext ? 1 : -1);

            // Bounds check
            if (newIndex < 0)
            {
                _announcer.Announce("First pack", Models.AnnouncementPriority.Normal);
                return true;
            }
            if (newIndex >= _boosterPackHitboxes.Count)
            {
                _announcer.Announce("Last pack", Models.AnnouncementPriority.Normal);
                return true;
            }

            // Get the old pack before updating index
            var oldPack = _boosterPackHitboxes[_boosterCarouselIndex];

            // Update index
            _boosterCarouselIndex = newIndex;

            // Get the new pack
            var targetPack = _boosterPackHitboxes[_boosterCarouselIndex];

            // Send PointerExit to old pack to stop its music/effects
            UIActivator.SimulatePointerExit(oldPack);

            // Click the new pack to center it (game's own centering behavior)
            UIActivator.Activate(targetPack);

            // Get pack name and announce
            string packName = UITextExtractor.GetText(targetPack);
            if (string.IsNullOrEmpty(packName))
                packName = "Pack";

            string announcement = $"{packName}, {_boosterCarouselIndex + 1} of {_boosterPackHitboxes.Count}";
            _announcer.Announce(announcement, Models.AnnouncementPriority.High);

            // Update the element label
            UpdateBoosterCarouselElement();

            return true;
        }

        /// <summary>
        /// Update the booster carousel element label after navigation.
        /// </summary>
        private void UpdateBoosterCarouselElement()
        {
            if (_boosterPackHitboxes.Count == 0) return;

            var currentPack = _boosterPackHitboxes[_boosterCarouselIndex];
            string packName = UITextExtractor.GetText(currentPack);
            if (string.IsNullOrEmpty(packName))
                packName = "Pack";

            string label = $"{packName}, {_boosterCarouselIndex + 1} of {_boosterPackHitboxes.Count}, use left and right arrows";

            // Find and update the carousel element in our list
            for (int i = 0; i < _elements.Count; i++)
            {
                if (_boosterPackHitboxes.Contains(_elements[i].GameObject))
                {
                    var element = _elements[i];
                    element.GameObject = currentPack;
                    element.Label = label;
                    _elements[i] = element;
                    break;
                }
            }
        }

        /// <summary>
        /// Open the currently selected booster pack.
        /// </summary>
        /// <returns>True if a pack was opened</returns>
        private bool OpenSelectedBoosterPack()
        {
            if (!_isBoosterCarouselActive || _boosterPackHitboxes.Count == 0)
                return false;

            var currentPack = _boosterPackHitboxes[_boosterCarouselIndex];

            // Click the pack to open it (should already be centered)
            UIActivator.Activate(currentPack);

            LogDebug($"[{NavigatorId}] Opening booster pack at index {_boosterCarouselIndex}");
            return true;
        }

        /// <summary>
        /// Override carousel arrow handling to support booster carousel navigation.
        /// </summary>
        protected override bool HandleCarouselArrow(bool isNext)
        {
            // Check if current element is in the booster carousel
            if (_isBoosterCarouselActive && _boosterPackHitboxes.Count > 0)
            {
                // Get current element from grouped navigator or base elements
                GameObject currentObj = null;
                if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
                {
                    var currentElement = _groupedNavigator.CurrentElement;
                    currentObj = currentElement?.GameObject;
                }
                else if (IsValidIndex)
                {
                    currentObj = _elements[_currentIndex].GameObject;
                }

                if (currentObj != null && _boosterPackHitboxes.Contains(currentObj))
                {
                    return HandleBoosterCarouselNavigation(isNext);
                }
            }

            // Fall back to base implementation for other carousels
            return base.HandleCarouselArrow(isNext);
        }

        #endregion

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

            // Reset NPE button tracking for new scene
            _npeButtonCheckTimer = NPEButtonCheckInterval;
            _lastNPEButtonCount = 0;

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
        /// Override search rescan to announce only Collection card count, not all elements.
        /// Does a quiet rescan without the full activation announcement.
        /// Also handles the suppressed navigation announcement after Tab from search field.
        /// </summary>
        protected override void ForceRescanAfterSearch()
        {
            if (!IsActive) return;

            // Check if we need to announce position after rescan (Tab from search field)
            bool needsPositionAnnouncement = _suppressNavigationAnnouncement;
            _suppressNavigationAnnouncement = false; // Clear the flag

            // Get old collection count before rescan (if grouped navigation is active)
            int oldCollectionCount = 0;
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                oldCollectionCount = _groupedNavigator.GetGroupElementCount(ElementGrouping.ElementGroup.DeckBuilderCollection);
            }

            // Do the rescan WITHOUT announcement (copy logic from base, skip announce)
            _elements.Clear();
            _currentIndex = -1;
            DiscoverElements();

            if (_elements.Count > 0)
            {
                _currentIndex = 0;
                UpdateEventSystemSelection();
            }

            // After rescan, announce the results
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                int newCollectionCount = _groupedNavigator.GetGroupElementCount(ElementGrouping.ElementGroup.DeckBuilderCollection);
                MelonLogger.Msg($"[{NavigatorId}] Search rescan collection: {oldCollectionCount} -> {newCollectionCount} cards");

                // If we suppressed the navigation announcement, now announce current position
                if (needsPositionAnnouncement)
                {
                    // Announce the current element in the group we navigated to
                    string currentAnnouncement = _groupedNavigator.GetCurrentAnnouncement();
                    if (!string.IsNullOrEmpty(currentAnnouncement))
                    {
                        _announcer.AnnounceInterrupt(currentAnnouncement);
                    }
                }
                else if (newCollectionCount != oldCollectionCount)
                {
                    // Normal case (Escape from search) - just announce count change
                    if (newCollectionCount == 0)
                    {
                        _announcer.AnnounceInterrupt("No search results");
                    }
                    else
                    {
                        _announcer.AnnounceInterrupt($"Search results: {newCollectionCount} cards");
                    }
                }
            }
            else
            {
                // Fallback for non-grouped contexts: use total element count
                MelonLogger.Msg($"[{NavigatorId}] Search rescan (non-grouped): {_elements.Count} elements");
                if (_elements.Count == 0)
                {
                    _announcer.AnnounceInterrupt("No search results");
                }
            }
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

            // Deactivate if NPE rewards popup becomes active - let NPERewardNavigator handle it
            if (_screenDetector.IsNPERewardsScreenActive())
            {
                LogDebug($"[{NavigatorId}] NPE rewards screen detected - deactivating to let NPERewardNavigator take over");
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

            // NPE scene: periodically check for new buttons (e.g., "Play" button appearing after dialogue)
            if (_currentScene == "NPEStitcher" && _rescanDelay <= 0)
            {
                _npeButtonCheckTimer -= Time.deltaTime;
                if (_npeButtonCheckTimer <= 0)
                {
                    _npeButtonCheckTimer = NPEButtonCheckInterval;
                    CheckForNewNPEButtons();
                }
            }

            // Overlay state change detection - trigger rescan when overlays open/close
            // This ensures navigation is refreshed when e.g. rewards popup closes
            var currentOverlay = _overlayDetector.GetActiveOverlay();
            if (currentOverlay != _lastKnownOverlay)
            {
                MelonLogger.Msg($"[{NavigatorId}] Overlay changed: {_lastKnownOverlay?.ToString() ?? "none"} -> {currentOverlay?.ToString() ?? "none"}");
                _lastKnownOverlay = currentOverlay;

                // Only trigger rescan if we're not already waiting for one
                if (_rescanDelay <= 0)
                {
                    TriggerRescan();
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

            // F11: Dump booster pack details (when in BoosterChamber)
            if (Input.GetKeyDown(KeyCode.F11) && _activeContentController == "BoosterChamber")
            {
                GameObject currentElement = null;
                if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
                {
                    currentElement = _groupedNavigator.CurrentElement?.GameObject;
                }
                else if (IsValidIndex)
                {
                    currentElement = _elements[_currentIndex].GameObject;
                }

                if (currentElement != null)
                {
                    MenuDebugHelper.DumpBoosterPackDetails(NavigatorId, currentElement, _announcer);
                }
                else
                {
                    _announcer.Announce("No element selected for pack investigation.", Models.AnnouncementPriority.High);
                }
                return true;
            }

            // Enter: In grouped navigation mode, handle both group entry and element activation
            // Use GetEnterAndConsume to prevent game from also processing Enter on EventSystem selected object
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive && InputManager.GetEnterAndConsume())
            {
                if (HandleGroupedEnter())
                    return true;
            }

            // Backspace: Universal back - goes back one level in menus
            // But NOT when an input field is focused - let Backspace delete characters
            // Also skip if key was consumed by another navigator (e.g., AdvancedFiltersNavigator)
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                if (InputManager.IsKeyConsumed(KeyCode.Backspace))
                {
                    LogDebug($"[{NavigatorId}] Backspace pressed but already consumed - skipping");
                    return true; // Key was handled elsewhere
                }

                if (UIFocusTracker.IsAnyInputFieldFocused())
                {
                    LogDebug($"[{NavigatorId}] Backspace pressed but input field focused - passing through");
                    return false; // Let it pass through to the input field
                }

                // PlayBlade gets priority - helper handles all PlayBlade navigation
                var playBladeResult = _playBladeHelper.HandleBackspace();
                switch (playBladeResult)
                {
                    case PlayBladeResult.CloseBlade:
                        return ClosePlayBlade();
                    case PlayBladeResult.RescanNeeded:
                        TriggerRescan();
                        return true;
                    case PlayBladeResult.Handled:
                        return true;
                }

                // Not PlayBlade - use grouped navigation if inside a group
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

            // Arrow Left/Right: Handle special navigation contexts
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                bool isRight = Input.GetKeyDown(KeyCode.RightArrow);

                // Booster carousel navigation (packs screen)
                if (_isBoosterCarouselActive && _boosterPackHitboxes.Count > 0)
                {
                    if (HandleBoosterCarouselNavigation(isRight))
                        return true;
                }

                // Collection card navigation (deck builder)
                // Note: Rewards popup is now handled by RewardPopupNavigator
                if (IsInCollectionCardContext())
                {
                    if (isRight)
                        MoveNext();
                    else
                        MovePrevious();
                    return true;
                }
            }

            // Page Up/Down: Navigate collection pages (activate Previous/Next buttons)
            if (Input.GetKeyDown(KeyCode.PageUp) || Input.GetKeyDown(KeyCode.PageDown))
            {
                if (_activeContentController == "WrapperDeckBuilder")
                {
                    bool isPageDown = Input.GetKeyDown(KeyCode.PageDown);
                    if (ActivateCollectionPageButton(isPageDown))
                        return true;
                }
            }

            // Number keys 1-0: Activate filter options in deck builder
            // 1-9 = options 1-9, 0 = option 10
            if (_activeContentController == "WrapperDeckBuilder" && _groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                int filterIndex = -1;
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) filterIndex = 0;
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) filterIndex = 1;
                else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) filterIndex = 2;
                else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) filterIndex = 3;
                else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) filterIndex = 4;
                else if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) filterIndex = 5;
                else if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) filterIndex = 6;
                else if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) filterIndex = 7;
                else if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)) filterIndex = 8;
                else if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) filterIndex = 9;

                if (filterIndex >= 0)
                {
                    if (ActivateFilterByIndex(filterIndex))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Activates the Next or Previous page button in the collection view.
        /// After page switch, only newly visible cards will be shown (cards that weren't visible before).
        /// </summary>
        private bool ActivateCollectionPageButton(bool next)
        {
            string targetLabel = next ? "Next" : "Previous";
            LogDebug($"[{NavigatorId}] Looking for '{targetLabel}' page button...");

            // Save current collection cards before page switch so we can filter to show only new cards
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                _groupedNavigator.SaveCollectionCardsForPageFilter();
            }

            // Search through elements for the navigation button
            foreach (var element in _elements)
            {
                if (element.GameObject == null) continue;

                string label = element.Label?.ToLower() ?? "";
                string objName = element.GameObject.name.ToLower();

                // Check if this is the Next/Previous navigation button
                bool isTarget = next
                    ? (label.Contains("next") || objName.Contains("next"))
                    : (label.Contains("previous") || label.Contains("prev") || objName.Contains("previous") || objName.Contains("prev"));

                if (isTarget && (label.Contains("navigation") || objName.Contains("navigation") || objName.Contains("arrow") || objName.Contains("page")))
                {
                    LogDebug($"[{NavigatorId}] Found page button: {element.Label} ({element.GameObject.name})");

                    // Activate the button
                    var result = UIActivator.Activate(element.GameObject);
                    if (result.Success)
                    {
                        _announcer.Announce($"{targetLabel} page", Models.AnnouncementPriority.Normal);
                        // Request filter to show only new cards after rescan
                        if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
                        {
                            _groupedNavigator.RequestCollectionPageFilter();
                        }
                        // Rescan to refresh card list - group state will be restored automatically
                        TriggerRescan();
                        return true;
                    }
                }
            }

            // Also try finding by GameObject.Find for common patterns
            var buttonNames = next
                ? new[] { "NextPageButton", "Next_Button", "ArrowRight", "NextArrow" }
                : new[] { "PreviousPageButton", "Previous_Button", "Prev_Button", "ArrowLeft", "PrevArrow" };

            foreach (var btnName in buttonNames)
            {
                var btn = GameObject.Find(btnName);
                if (btn != null && btn.activeInHierarchy)
                {
                    LogDebug($"[{NavigatorId}] Found page button by name: {btnName}");
                    var result = UIActivator.Activate(btn);
                    if (result.Success)
                    {
                        _announcer.Announce($"{targetLabel} page", Models.AnnouncementPriority.Normal);
                        // Request filter to show only new cards after rescan
                        if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
                        {
                            _groupedNavigator.RequestCollectionPageFilter();
                        }
                        // Rescan to refresh card list - group state will be restored automatically
                        TriggerRescan();
                        return true;
                    }
                }
            }

            LogDebug($"[{NavigatorId}] No '{targetLabel}' page button found");
            return false;
        }

        /// <summary>
        /// Check if we're in a context where cards should navigate with Left/Right arrows.
        /// This includes DeckBuilderCollection, DeckBuilderDeckList and similar card-grid contexts.
        /// </summary>
        private bool IsInCollectionCardContext()
        {
            // Check if current element is in DeckBuilderCollection or DeckBuilderDeckList group
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                var currentGroup = _groupedNavigator.CurrentGroup;
                if (currentGroup?.Group == ElementGroup.DeckBuilderCollection ||
                    currentGroup?.Group == ElementGroup.DeckBuilderDeckList)
                    return true;
            }

            // Also check if current element is a card (for ungrouped mode)
            if (IsValidIndex && _elements[_currentIndex].GameObject != null)
            {
                var currentElement = _elements[_currentIndex].GameObject;
                if (CardDetector.IsCard(currentElement))
                {
                    // Check if parent hierarchy contains PoolHolder (collection cards)
                    Transform t = currentElement.transform;
                    while (t != null)
                    {
                        if (t.name.Contains("PoolHolder"))
                            return true;
                        t = t.parent;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Activate a filter option by index (0-9 for options 1-10).
        /// Number keys 1-9 activate options 1-9, 0 activates option 10.
        /// </summary>
        private bool ActivateFilterByIndex(int index)
        {
            var filterElement = _groupedNavigator.GetElementFromGroup(ElementGroup.Filters, index);
            if (filterElement != null)
            {
                // Get the element label for announcement
                var filterGroup = _groupedNavigator.GetGroupByType(ElementGroup.Filters);
                string label = "Filter";
                if (filterGroup.HasValue && index < filterGroup.Value.Count)
                {
                    label = filterGroup.Value.Elements[index].Label ?? "Filter";
                }

                LogDebug($"[{NavigatorId}] Activating filter {index + 1}: {label}");

                // Activate the filter
                var result = UIActivator.Activate(filterElement);
                if (result.Success)
                {
                    // Check if it's a toggle to announce the new state
                    var toggle = filterElement.GetComponent<UnityEngine.UI.Toggle>();
                    if (toggle != null)
                    {
                        // Toggle state will be inverted after activation
                        string state = toggle.isOn ? "off" : "on"; // Inverted because it hasn't changed yet
                        _announcer.Announce($"{label}: {state}", Models.AnnouncementPriority.High);
                    }
                    else
                    {
                        _announcer.Announce($"Activated {label}", Models.AnnouncementPriority.High);
                    }

                    // Trigger rescan to update UI state
                    TriggerRescan();
                    return true;
                }
            }
            else
            {
                // No filter at this index
                int filterCount = _groupedNavigator.GetGroupElementCount(ElementGroup.Filters);
                if (filterCount > 0)
                {
                    _announcer.Announce($"No filter {index + 1}. {filterCount} filters available.", Models.AnnouncementPriority.Normal);
                }
                else
                {
                    _announcer.Announce("No filters available", Models.AnnouncementPriority.Normal);
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
                    ElementGroup.FriendsPanel => CloseSocialPanel(),
                    ElementGroup.MailboxContent => CloseMailDetailView(), // Close mail, return to list
                    ElementGroup.MailboxList => CloseMailbox(), // Close mailbox entirely
                    // RewardsPopup handled by RewardPopupNavigator
                    ElementGroup.PlayBladeTabs => HandlePlayBladeBackspace(),
                    ElementGroup.PlayBladeContent => HandlePlayBladeBackspace(),
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
            // Refresh content controller detection to ensure we have current state
            DetectActiveContentController();
            LogDebug($"[{NavigatorId}] HandleContentPanelBack: controller = {_activeContentController ?? "null"}");

            // Special case: Deck Builder uses MainButton (Fertig/Done) to exit
            if (_activeContentController == "WrapperDeckBuilder")
            {
                return HandleDeckBuilderBack();
            }

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
        /// Handle back navigation in the deck builder.
        /// The deck builder uses MainButton (labeled "Fertig"/"Done") to exit editing mode.
        /// </summary>
        private bool HandleDeckBuilderBack()
        {
            LogDebug($"[{NavigatorId}] Deck Builder detected, looking for Done button");

            // Find MainButton within DeckBuilderWidget (not the Home page's MainButton)
            // The deck builder's button is at: DeckBuilderWidget_Desktop_16x9/BottomRight/Buttons/MainButton
            var deckBuilderWidget = GameObject.Find("DeckBuilderWidget_Desktop_16x9");
            if (deckBuilderWidget != null)
            {
                var mainButton = deckBuilderWidget.transform.Find("BottomRight/Buttons/MainButton");
                if (mainButton != null && mainButton.gameObject.activeInHierarchy)
                {
                    LogDebug($"[{NavigatorId}] Found deck builder Done button: {mainButton.name}");
                    _announcer.Announce(Models.Strings.ExitingDeckBuilder, Models.AnnouncementPriority.High);
                    UIActivator.Activate(mainButton.gameObject);
                    TriggerRescan();
                    return true;
                }
                LogDebug($"[{NavigatorId}] DeckBuilderWidget found but MainButton not at expected path");
            }
            else
            {
                LogDebug($"[{NavigatorId}] DeckBuilderWidget_Desktop_16x9 not found");
            }

            // Fallback: navigate to Home if MainButton not found
            LogDebug($"[{NavigatorId}] Deck Builder Done button not found, navigating to Home");
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
        /// Announce the body text of a popup/dialog when it opens.
        /// This helps users understand what the popup is asking before navigating to buttons.
        /// </summary>
        private void AnnouncePopupBodyText(GameObject popupGameObject)
        {
            if (popupGameObject == null) return;

            string bodyText = UITextExtractor.GetPopupBodyText(popupGameObject);

            if (!string.IsNullOrWhiteSpace(bodyText))
            {
                LogDebug($"[{NavigatorId}] Popup body text: {bodyText}");
                _announcer.Announce(bodyText, Models.AnnouncementPriority.High);
            }
            else
            {
                LogDebug($"[{NavigatorId}] No popup body text found for: {popupGameObject.name}");
            }
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
        /// Close the Mailbox panel or close current mail detail view.
        /// If viewing a mail, closes the mail and returns to the mail list.
        /// If viewing the mail list, closes the entire mailbox panel.
        /// </summary>
        private bool CloseMailbox()
        {
            // If we're in the mail detail view, close just the mail (not the whole mailbox)
            if (_isInMailDetailView)
            {
                return CloseMailDetailView();
            }

            LogDebug($"[{NavigatorId}] Closing Mailbox panel");

            // Find NavBarController and invoke HideInboxIfActive()
            var navBar = GameObject.Find("NavBar_Desktop_16x9(Clone)");
            if (navBar != null)
            {
                foreach (var mb in navBar.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == "NavBarController")
                    {
                        var method = mb.GetType().GetMethod("HideInboxIfActive",
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance);

                        if (method != null)
                        {
                            try
                            {
                                LogDebug($"[{NavigatorId}] Invoking NavBarController.HideInboxIfActive()");
                                method.Invoke(mb, null);
                                _announcer.Announce(Models.Strings.NavigatingBack, Models.AnnouncementPriority.High);
                                TriggerRescan();
                                return true;
                            }
                            catch (System.Exception ex)
                            {
                                LogDebug($"[{NavigatorId}] HideInboxIfActive() error: {ex.Message}");
                            }
                        }
                        break;
                    }
                }
            }

            // Fallback: try to find and click dismiss button in mailbox panel
            var mailboxPanel = GameObject.Find("ContentController - Mailbox_Base(Clone)");
            if (mailboxPanel != null)
            {
                var closeButton = FindCloseButtonInPanel(mailboxPanel);
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
        /// Close the current mail detail view and return to the mail list.
        /// </summary>
        private bool CloseMailDetailView()
        {
            LogDebug($"[{NavigatorId}] Closing mail detail view");

            // Find ContentControllerPlayerInbox and invoke CloseCurrentLetter()
            var mailboxPanel = GameObject.Find("ContentController - Mailbox_Base(Clone)");
            if (mailboxPanel != null)
            {
                foreach (var mb in mailboxPanel.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == "ContentControllerPlayerInbox")
                    {
                        var method = mb.GetType().GetMethod("CloseCurrentLetter",
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance);

                        if (method != null)
                        {
                            try
                            {
                                LogDebug($"[{NavigatorId}] Invoking ContentControllerPlayerInbox.CloseCurrentLetter()");
                                method.Invoke(mb, null);
                                _isInMailDetailView = false;
                                _currentMailLetterId = Guid.Empty;
                                ResetMailFieldNavigation();
                                _announcer.Announce("Back to mail list", Models.AnnouncementPriority.High);
                                TriggerRescan();
                                return true;
                            }
                            catch (System.Exception ex)
                            {
                                LogDebug($"[{NavigatorId}] CloseCurrentLetter() error: {ex.Message}");
                            }
                        }
                        break;
                    }
                }
            }

            // Fallback: reset flag anyway and try generic back
            _isInMailDetailView = false;
            _currentMailLetterId = Guid.Empty;
            ResetMailFieldNavigation();
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
        /// Handle Backspace within PlayBlade when detected via overlay.
        /// Note: Most PlayBlade backspace cases are handled earlier by the helper.
        /// This is a fallback for overlay detection - just close the blade.
        /// </summary>
        private bool HandlePlayBladeBackspace()
        {
            return ClosePlayBlade();
        }

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
            _playBladeHelper.OnPlayBladeClosed();
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
            // Exclude Options_Button globally - it's the settings gear icon, always reachable via Escape
            if (obj.name == "Options_Button")
                return false;

            // In mail content view, filter out buttons that have no actual text content
            // (e.g., "SecondaryButton_v2" which only shows its object name)
            if (_isInMailDetailView && _overlayDetector.GetActiveOverlay() == ElementGroup.MailboxContent)
            {
                if (IsMailContentButtonWithNoText(obj))
                {
                    LogDebug($"[{NavigatorId}] Filtering mail button with no text: {obj.name}");
                    return false;
                }
            }

            // Include Blade_ListItem elements ONLY for PlayBlade context (play mode options like Bot Match, Standard)
            // This bypass should NOT apply to:
            // - Mailbox items (need proper list/content filtering)
            // - Any other panel that uses Blade_ListItem naming
            // Check: must be inside Blade_ListItem AND inside actual PlayBlade container
            if (IsInsideBladeListItem(obj) && IsInsidePlayBladeContainer(obj))
            {
                LogDebug($"[{NavigatorId}] Blade_ListItem bypass for PlayBlade: {obj.name}");
                return true;
            }

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
        /// Also includes Objectives panel which is a separate content controller on Home.
        /// </summary>
        private bool IsChildOfHomeOrNavBar(GameObject obj)
        {
            if (_activeControllerGameObject != null && IsChildOf(obj, _activeControllerGameObject))
                return true;
            if (_navBarGameObject != null && IsChildOf(obj, _navBarGameObject))
                return true;

            // Include Objectives panel - it's a separate content controller shown on Home page
            if (IsInsideObjectivesPanel(obj))
                return true;

            return false;
        }

        /// <summary>
        /// Check if element is inside the Objectives panel.
        /// </summary>
        private static bool IsInsideObjectivesPanel(GameObject obj)
        {
            Transform current = obj.transform;
            while (current != null)
            {
                if (current.name.Contains("Objectives_Desktop") || current.name.Contains("Objective_Base") ||
                    current.name.Contains("Objective_BattlePass") || current.name.Contains("Objective_NPE"))
                    return true;
                current = current.parent;
            }
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
        /// Check if element is inside a Blade_ListItem (play mode options like Bot Match, Standard).
        /// These elements need special handling as they may not be detected by the overlay system.
        /// </summary>
        private static bool IsInsideBladeListItem(GameObject obj)
        {
            if (obj == null) return false;

            Transform current = obj.transform;
            int levels = 0;
            const int maxLevels = 5;

            while (current != null && levels < maxLevels)
            {
                if (current.name.Contains("Blade_ListItem"))
                    return true;
                current = current.parent;
                levels++;
            }

            return false;
        }

        /// <summary>
        /// Check if element is inside the Mailbox panel.
        /// </summary>
        private static bool IsInsideMailboxPanel(GameObject obj)
        {
            if (obj == null) return false;

            Transform current = obj.transform;
            while (current != null)
            {
                if (current.name.Contains("Mailbox") || current.name.Contains("PlayerInbox"))
                    return true;
                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// Check if element is inside the actual PlayBlade container.
        /// This is more specific than IsInsideBladeListItem - it checks for PlayBlade specifically,
        /// not just any Blade_ListItem (which Mailbox and other panels also use).
        /// </summary>
        private static bool IsInsidePlayBladeContainer(GameObject obj)
        {
            if (obj == null) return false;

            Transform current = obj.transform;
            while (current != null)
            {
                string name = current.name;
                // PlayBlade specific containers
                if (name.Contains("PlayBlade") || name.Contains("Play_Blade"))
                    return true;
                // Exclude other panels that use Blade naming
                if (name.Contains("Mailbox") || name.Contains("PlayerInbox") ||
                    name.Contains("Settings") || name.Contains("Social"))
                    return false;
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
        /// Check if element is a button in mail content that has no actual text content.
        /// Filters out buttons like "SecondaryButton_v2" which only show their object name.
        /// </summary>
        private bool IsMailContentButtonWithNoText(GameObject obj)
        {
            if (obj == null) return false;

            // Check if inside mail content view
            Transform current = obj.transform;
            bool inMailContent = false;
            while (current != null)
            {
                if (current.name.Contains("Mailbox_ContentView") || current.name.Contains("CONTENT_Mailbox_Letter"))
                {
                    inMailContent = true;
                    break;
                }
                current = current.parent;
            }

            if (!inMailContent)
                return false;

            // Check if this is a button
            var customButton = obj.GetComponent<MonoBehaviour>();
            bool isButton = false;
            if (customButton != null)
            {
                foreach (var comp in obj.GetComponents<MonoBehaviour>())
                {
                    if (comp != null && comp.GetType().Name == "CustomButton")
                    {
                        isButton = true;
                        break;
                    }
                }
            }

            if (!isButton)
                return false;

            // Check if the button has actual text content
            bool hasActualText = UITextExtractor.HasActualText(obj);

            // If no actual text, this is a button with only its object name - filter it out
            if (!hasActualText)
                return true;

            // Also filter if the extracted text is just the object name cleaned up
            string extractedText = UITextExtractor.GetText(obj);
            string objNameCleaned = obj.name.ToLowerInvariant().Replace("_", " ").Replace("-", " ");
            string extractedCleaned = extractedText?.ToLowerInvariant().Replace(", button", "").Trim() ?? "";

            // If the extracted text matches the object name (with minor variations), filter it
            if (!string.IsNullOrEmpty(extractedCleaned) && objNameCleaned.Contains(extractedCleaned))
                return true;

            return false;
        }

        /// <summary>
        /// Auto-press the Play button in PlayBlade after deck selection.
        /// Finds the MainButton (named "MainButton" without MainButton component) and activates it.
        /// </summary>
        private void AutoPressPlayButtonInPlayBlade()
        {
            // Find the PlayBlade Play button - it's named "MainButton" but does NOT have the MainButton component
            // (The Home page Play button has the MainButton component, PlayBlade's doesn't)
            foreach (var elem in _elements)
            {
                if (elem.GameObject != null && elem.GameObject.name == "MainButton")
                {
                    // Verify it's NOT the Home page MainButton (which has MainButton component)
                    bool hasMainButtonComponent = false;
                    var components = elem.GameObject.GetComponents<MonoBehaviour>();
                    foreach (var comp in components)
                    {
                        if (comp != null && comp.GetType().Name == "MainButton")
                        {
                            hasMainButtonComponent = true;
                            break;
                        }
                    }

                    // PlayBlade's Play button doesn't have MainButton component
                    if (!hasMainButtonComponent)
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Auto-pressing Play button after deck selection");
                        UIActivator.Activate(elem.GameObject);
                        return;
                    }
                }
            }

            MelonLogger.Msg($"[{NavigatorId}] PlayBlade Play button not found for auto-press");
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
        /// Check for new CustomButtons on NPE scene (e.g., "Play" button appearing after dialogue).
        /// Triggers rescan if button count changed.
        /// </summary>
        private void CheckForNewNPEButtons()
        {
            int currentCount = CountActiveCustomButtons();
            if (currentCount != _lastNPEButtonCount)
            {
                LogDebug($"[{NavigatorId}] NPE button count changed: {_lastNPEButtonCount} -> {currentCount}, triggering rescan");
                _lastNPEButtonCount = currentCount;
                TriggerRescan();
            }
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
            // Store previous controller to detect screen transitions
            var previousController = _activeContentController;

            // Detect active controller BEFORE discovering elements so filtering works correctly
            DetectActiveContentController();
            LogDebug($"[{NavigatorId}] Rescanning elements after panel change (controller: {_activeContentController ?? "none"})");

            // Remember the navigator's current selection before clearing
            GameObject previousSelection = null;
            if (_currentIndex >= 0 && _currentIndex < _elements.Count)
            {
                previousSelection = _elements[_currentIndex].GameObject;
            }

            // Save current group state for restoration after rescan - but only if same screen
            // (don't restore old screen's state when transitioning to a new screen)
            if (_groupedNavigationEnabled && previousController == _activeContentController)
            {
                _groupedNavigator.SaveCurrentGroupForRestore();
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

            // Don't activate when NPE rewards popup is showing - let NPERewardNavigator handle it
            if (_screenDetector.IsNPERewardsScreenActive())
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
            // Disable grouped navigation for Login scene - it's a simple form with no groups needed
            // This ensures Tab and arrow keys navigate the same flat list of elements
            _groupedNavigationEnabled = _currentScene != "Login";

            // Note: RewardsPopup handled by RewardPopupNavigator (has its own flat navigation)

            // Detect active controller first so filtering works correctly
            DetectActiveContentController();

            // Disable grouped navigation for BoosterChamber - flat list is better for pack carousel
            if (_activeContentController == "BoosterChamber")
                _groupedNavigationEnabled = false;

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

            // Reset booster carousel state
            _boosterPackHitboxes.Clear();
            _isBoosterCarouselActive = _activeContentController == "BoosterChamber";
            if (!_isBoosterCarouselActive)
                _boosterCarouselIndex = 0;

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

                // Booster carousel: collect pack hitboxes separately instead of adding individually
                if (_isBoosterCarouselActive && obj.name == "Hitbox_BoosterMesh")
                {
                    // Check if inside CarouselBooster (valid pack element)
                    if (IsInsideCarouselBooster(obj))
                    {
                        _boosterPackHitboxes.Add(obj);
                        addedObjects.Add(obj);
                        return; // Don't add as individual element
                    }
                }

                // Debug: log objectives and Blade_ListItem filtering
                bool isObjective = obj.name.Contains("Objective") || GetParentPath(obj).Contains("Objective");
                bool isBladeListItem = GetParentPath(obj).Contains("Blade_ListItem");

                if (!ShouldShowElement(obj))
                {
                    if (isObjective)
                        MelonLogger.Msg($"[{NavigatorId}] Objective filtered by ShouldShowElement: {obj.name}");
                    if (isBladeListItem)
                        MelonLogger.Msg($"[{NavigatorId}] Blade_ListItem filtered by ShouldShowElement: {obj.name}, path={GetParentPath(obj)}");
                    return;
                }

                var classification = UIElementClassifier.Classify(obj);
                if (classification.IsNavigable && classification.ShouldAnnounce)
                {
                    var pos = obj.transform.position;
                    float sortOrder = -pos.y * 1000 + pos.x;
                    discoveredElements.Add((obj, classification, sortOrder));
                    addedObjects.Add(obj);
                    if (isBladeListItem)
                        MelonLogger.Msg($"[{NavigatorId}] Blade_ListItem ADDED: {obj.name}, label={classification.Label}");
                }
                else if (isObjective)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Objective not navigable: {obj.name}, IsNavigable={classification.IsNavigable}, ShouldAnnounce={classification.ShouldAnnounce}");
                }
                else if (isBladeListItem)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Blade_ListItem not navigable: {obj.name}, IsNavigable={classification.IsNavigable}, ShouldAnnounce={classification.ShouldAnnounce}");
                }
            }

            string GetParentPath(GameObject element)
            {
                var pathBuilder = new System.Text.StringBuilder();
                Transform current = element.transform.parent;
                while (current != null)
                {
                    if (pathBuilder.Length > 0) pathBuilder.Insert(0, "/");
                    pathBuilder.Insert(0, current.name);
                    current = current.parent;
                }
                return pathBuilder.ToString();
            }

            // Note: Rewards popup is now handled by RewardPopupNavigator

            // Find CustomButtons (MTGA's primary button component)
            int objCount = 0;
            int objGraphicsCount = 0;
            foreach (var buttonObj in GetActiveCustomButtons())
            {
                objCount++;
                // Debug: log objective elements explicitly
                if (buttonObj.name == "ObjectiveGraphics")
                {
                    objGraphicsCount++;
                    MelonLogger.Msg($"[{NavigatorId}] Processing ObjectiveGraphics #{objGraphicsCount}: parent={buttonObj.transform.parent?.name}");
                }

                TryAddElement(buttonObj);
            }
            MelonLogger.Msg($"[{NavigatorId}] Processed {objCount} CustomButtons, {objGraphicsCount} ObjectiveGraphics");

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
            MelonLogger.Msg($"[{NavigatorId}] Processing {discoveredElements.Count} discovered elements for final addition");
            foreach (var (obj, classification, _) in discoveredElements.OrderBy(x => x.sortOrder))
            {
                // Debug: trace Blade_ListItem elements through final addition
                bool isBladeListItem = obj.transform.parent?.name.Contains("Blade_ListItem") == true;
                if (isBladeListItem)
                    MelonLogger.Msg($"[{NavigatorId}] Final loop Blade_ListItem: {obj.name}, label={classification.Label}, obj null={obj == null}, active={obj?.activeInHierarchy}");

                // Skip deck elements that aren't the main UI button (TextBox, duplicates, etc.)
                if (deckElementsToSkip.Contains(obj))
                {
                    if (isBladeListItem)
                        MelonLogger.Msg($"[{NavigatorId}] Blade_ListItem SKIPPED by deckElementsToSkip!");
                    continue;
                }

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

                if (isBladeListItem)
                    MelonLogger.Msg($"[{NavigatorId}] Blade_ListItem calling AddElement: announcement={announcement}");
                AddElement(obj, announcement, carouselInfo, null, attachedActions);
                if (isBladeListItem)
                    MelonLogger.Msg($"[{NavigatorId}] Blade_ListItem AddElement returned, _elements.Count={_elements.Count}");
            }

            // Find NPE reward cards (displayed cards on reward screens)
            // These are not buttons but should be navigable to read card info
            FindNPERewardCards(addedObjects);

            // Find deck builder collection cards (PoolHolder canvas)
            // These are cards you can add to your deck
            FindPoolHolderCards(addedObjects);

            // Find deck list cards (MainDeck_MetaCardHolder)
            // These are cards currently in your deck
            FindDeckListCards(addedObjects);

            // In mail content view, add mail fields (title, date, body) as navigable elements
            // These appear before buttons in the navigation list
            if (_isInMailDetailView)
            {
                AddMailContentFieldsAsElements();
            }

            // Add booster carousel as a single navigable element (if packs were collected)
            if (_isBoosterCarouselActive && _boosterPackHitboxes.Count > 0)
            {
                AddBoosterCarouselElement();
            }

            LogDebug($"[{NavigatorId}] Discovered {_elements.Count} navigable elements");

            // Organize elements into groups for hierarchical navigation
            if (_groupedNavigationEnabled && _elements.Count > 0)
            {
                var elementsForGrouping = _elements.Select(e => (e.GameObject, e.Label));
                _groupedNavigator.OrganizeIntoGroups(elementsForGrouping);

                // Update EventSystem selection to match the initial grouped element
                UpdateEventSystemSelectionForGroupedElement();
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
        /// Find collection cards in the deck builder's PoolHolder canvas.
        /// These cards use PagesMetaCardView component and are navigable for accessibility.
        /// </summary>
        private void FindPoolHolderCards(HashSet<GameObject> addedObjects)
        {
            // Only active in deck builder
            if (_activeContentController != "WrapperDeckBuilder")
                return;

            LogDebug($"[{NavigatorId}] Deck Builder detected, searching for collection cards in PoolHolder...");

            // Find the PoolHolder canvas which contains collection cards
            var poolHolder = GameObject.Find("PoolHolder");
            if (poolHolder == null)
            {
                LogDebug($"[{NavigatorId}] PoolHolder not found");
                return;
            }

            LogDebug($"[{NavigatorId}] Found PoolHolder canvas");

            // Find all PagesMetaCardView components inside PoolHolder
            var cardViews = new List<(GameObject obj, float sortOrder)>();

            foreach (var mb in poolHolder.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                string typeName = mb.GetType().Name;
                if (typeName == "PagesMetaCardView" || typeName == "MetaCardView")
                {
                    var cardObj = mb.gameObject;
                    if (!addedObjects.Contains(cardObj))
                    {
                        // Sort by X position (left to right), then by Y (top to bottom)
                        float sortOrder = cardObj.transform.position.x + (-cardObj.transform.position.y * 1000);
                        cardViews.Add((cardObj, sortOrder));
                        addedObjects.Add(cardObj);
                    }
                }
            }

            // Also check by name patterns for cards
            foreach (var t in poolHolder.GetComponentsInChildren<Transform>(false))
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;

                string name = t.name;
                if (name.Contains("PagesMetaCardView") || name.Contains("MetaCardView"))
                {
                    if (!addedObjects.Contains(t.gameObject))
                    {
                        float sortOrder = t.position.x + (-t.position.y * 1000);
                        cardViews.Add((t.gameObject, sortOrder));
                        addedObjects.Add(t.gameObject);
                    }
                }
            }

            if (cardViews.Count == 0)
            {
                LogDebug($"[{NavigatorId}] No collection cards found in PoolHolder");
                return;
            }

            // Sort by position
            cardViews = cardViews.OrderBy(c => c.sortOrder).ToList();

            LogDebug($"[{NavigatorId}] Found {cardViews.Count} collection card(s) in PoolHolder");

            int cardNum = 1;
            int skippedCount = 0;
            foreach (var (cardObj, _) in cardViews)
            {
                // Extract card info using CardDetector
                var cardInfo = CardDetector.ExtractCardInfo(cardObj);

                // Skip unloaded/placeholder cards (GrpId = 0, typically named "CDC #0")
                // These are empty pool slots that haven't been populated with card data yet
                if (!cardInfo.IsValid)
                {
                    skippedCount++;
                    continue;
                }

                string cardName = cardInfo.Name;

                // Build label with card name
                string label = cardName;

                // Add type line if available
                if (!string.IsNullOrEmpty(cardInfo.TypeLine))
                {
                    label += $", {cardInfo.TypeLine}";
                }

                // Add mana cost if available
                if (!string.IsNullOrEmpty(cardInfo.ManaCost))
                {
                    label += $", {cardInfo.ManaCost}";
                }

                LogDebug($"[{NavigatorId}] Adding collection card {cardNum}: {label}");

                // Add as navigable element
                AddElement(cardObj, label);
                cardNum++;
            }
        }

        /// <summary>
        /// Find deck list cards in the deck builder's MainDeck_MetaCardHolder.
        /// These are cards currently in your deck, displayed as a compact list.
        /// Uses GrpId for language-agnostic card identification.
        /// </summary>
        private void FindDeckListCards(HashSet<GameObject> addedObjects)
        {
            // Only active in deck builder
            if (_activeContentController != "WrapperDeckBuilder")
                return;

            LogDebug($"[{NavigatorId}] Deck Builder detected, searching for deck list cards...");

            // Get deck list cards from CardModelProvider
            var deckCards = CardModelProvider.GetDeckListCards();
            if (deckCards.Count == 0)
            {
                LogDebug($"[{NavigatorId}] No deck list cards found");
                return;
            }

            LogDebug($"[{NavigatorId}] Found {deckCards.Count} deck list card(s)");

            int cardNum = 1;
            foreach (var deckCard in deckCards)
            {
                if (!deckCard.IsValid) continue;

                // Use the TileButton (card name button) as the navigable element
                var cardObj = deckCard.TileButton;
                if (cardObj == null || addedObjects.Contains(cardObj))
                    continue;

                // Get card name from GrpId
                string cardName = CardModelProvider.GetNameFromGrpId(deckCard.GrpId);
                if (string.IsNullOrEmpty(cardName))
                    cardName = $"Card #{deckCard.GrpId}";

                // Build label with quantity and card name
                string label = $"{deckCard.Quantity}x {cardName}";

                LogDebug($"[{NavigatorId}] Adding deck list card {cardNum}: {label}");

                // Add as navigable element
                AddElement(cardObj, label);
                addedObjects.Add(cardObj);
                cardNum++;
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
                // Only match DeckView_Base - NOT Blade_ListItem (those are play mode options, not decks)
                if (current.name.Contains("DeckView_Base"))
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

        // Group types to cycle between in the deck builder with Tab/Shift+Tab
        private static readonly ElementGroup[] DeckBuilderCycleGroups = new[]
        {
            ElementGroup.DeckBuilderCollection,  // Card pool (Collection)
            ElementGroup.DeckBuilderDeckList,    // Deck list cards (cards in your deck)
            ElementGroup.Filters,                // Filter controls
            ElementGroup.Content,                // Other deck builder controls
            ElementGroup.PlayBladeContent        // Play options
        };

        /// <summary>
        /// Override MoveNext to use GroupedNavigator when grouped navigation is enabled.
        /// In deck builder with Tab, cycles between Collection, Filters, and Deck groups only.
        /// </summary>
        protected override void MoveNext()
        {
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                // In deck builder with Tab key: cycle between main groups (Collection, Filters, Deck)
                // Only apply to Tab, not to arrow keys
                bool isTabPressed = Input.GetKey(KeyCode.Tab);
                if (_activeContentController == "WrapperDeckBuilder" && isTabPressed)
                {
                    if (_groupedNavigator.CycleToNextGroup(DeckBuilderCycleGroups))
                    {
                        // Skip announcement if suppressed (Tab from search field - will announce after rescan)
                        if (!_suppressNavigationAnnouncement)
                        {
                            _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                        }
                        UpdateCardNavigationForGroupedElement();
                        return;
                    }
                }

                // Default behavior: navigate all groups/elements
                _groupedNavigator.MoveNext();
                UpdateEventSystemSelectionForGroupedElement();
                UpdateCardNavigationForGroupedElement();
                return;
            }
            base.MoveNext();
        }

        /// <summary>
        /// Override MovePrevious to use GroupedNavigator when grouped navigation is enabled.
        /// In deck builder with Shift+Tab, cycles between Collection, Filters, and Deck groups only.
        /// </summary>
        protected override void MovePrevious()
        {
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                // In deck builder with Tab key: cycle between main groups (Collection, Filters, Deck)
                // Only apply to Tab, not to arrow keys
                bool isTabPressed = Input.GetKey(KeyCode.Tab);
                if (_activeContentController == "WrapperDeckBuilder" && isTabPressed)
                {
                    if (_groupedNavigator.CycleToPreviousGroup(DeckBuilderCycleGroups))
                    {
                        // Skip announcement if suppressed (Tab from search field - will announce after rescan)
                        if (!_suppressNavigationAnnouncement)
                        {
                            _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                        }
                        UpdateCardNavigationForGroupedElement();
                        return;
                    }
                }

                // Default behavior: navigate all groups/elements
                _groupedNavigator.MovePrevious();
                UpdateEventSystemSelectionForGroupedElement();
                UpdateCardNavigationForGroupedElement();
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
                UpdateEventSystemSelectionForGroupedElement();
                UpdateCardNavigationForGroupedElement();
                return;
            }
            base.MoveFirst();
        }

        /// <summary>
        /// Update CardInfoNavigator state for the current grouped element.
        /// Called after grouped navigation moves to prepare card reading with Up/Down arrows.
        /// </summary>
        private void UpdateCardNavigationForGroupedElement()
        {
            var cardNavigator = AccessibleArenaMod.Instance?.CardNavigator;
            if (cardNavigator == null) return;

            var currentElement = _groupedNavigator.CurrentElement;
            if (currentElement == null)
            {
                cardNavigator.Deactivate();
                return;
            }

            var gameObject = currentElement.Value.GameObject;
            if (gameObject == null)
            {
                if (cardNavigator.IsActive) cardNavigator.Deactivate();
                return;
            }

            // Check if it's a regular card (collection) or a deck list card
            bool isCard = CardDetector.IsCard(gameObject);
            bool isDeckListCard = CardModelProvider.IsDeckListCard(gameObject);

            if (isCard || isDeckListCard)
            {
                // Prepare card navigation for both collection cards and deck list cards
                cardNavigator.PrepareForCard(gameObject, ZoneType.Hand);
            }
            else if (cardNavigator.IsActive)
            {
                cardNavigator.Deactivate();
            }
        }

        /// <summary>
        /// Update EventSystem selection to match the current grouped element.
        /// This ensures Unity's Submit events go to the correct element when Enter is pressed.
        /// For toggles, we handle MTGA's OnSelect re-toggle behavior and set the blocking flag.
        /// (EventSystemPatch blocks Unity's Submit when BlockSubmitForToggle is true)
        /// </summary>
        private void UpdateEventSystemSelectionForGroupedElement()
        {
            var currentElement = _groupedNavigator.CurrentElement;
            if (currentElement == null) return;

            var gameObject = currentElement.Value.GameObject;
            if (gameObject == null || !gameObject.activeInHierarchy) return;

            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem != null)
            {
                // Check if this is a toggle - need to handle MTGA's OnSelect re-toggle
                var toggle = gameObject.GetComponent<Toggle>();
                bool isToggle = toggle != null;

                // Set submit blocking flag BEFORE any EventSystem interaction.
                // EventSystemPatch checks this flag to block Unity's Submit events for toggles.
                InputManager.BlockSubmitForToggle = isToggle;

                // Skip SetSelectedGameObject if EventSystem already has our element selected.
                // Calling it again would trigger OnSelect handlers unnecessarily, which can cause
                // issues with MTGA panels like UpdatePolicies (panel closes unexpectedly).
                if (eventSystem.currentSelectedGameObject == gameObject)
                {
                    return;
                }

                if (isToggle)
                {
                    bool stateBefore = toggle.isOn;
                    eventSystem.SetSelectedGameObject(gameObject);

                    // If MTGA's OnSelect handler re-toggled, revert to original state
                    if (toggle.isOn != stateBefore)
                    {
                        toggle.isOn = stateBefore;
                    }
                }
                else
                {
                    eventSystem.SetSelectedGameObject(gameObject);
                }
            }
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
                UpdateEventSystemSelectionForGroupedElement();
                UpdateCardNavigationForGroupedElement();
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
                    UpdateCardNavigationForGroupedElement();
                    return true;
                }

                // Normal group - enter it
                // Special handling for DeckBuilderDeckList: refresh deck cards immediately while UI is active
                if (currentGroup.HasValue && currentGroup.Value.Group == ElementGroup.DeckBuilderDeckList)
                {
                    CardModelProvider.ClearDeckListCache();
                    // Force immediate refresh while UI is in active state
                    var deckCards = CardModelProvider.GetDeckListCards();
                    MelonLogger.Msg($"[{NavigatorId}] Entering DeckBuilderDeckList - refreshed {deckCards.Count} deck cards");
                }

                if (_groupedNavigator.EnterGroup())
                {
                    _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                    UpdateCardNavigationForGroupedElement();
                    return true;
                }
            }
            else
            {
                // Inside a group - check for subgroup entry first
                if (_groupedNavigator.IsCurrentElementSubgroupEntry())
                {
                    if (_groupedNavigator.EnterSubgroup())
                    {
                        _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                        return true;
                    }
                }

                // Activate the current element
                var currentElement = _groupedNavigator.CurrentElement;
                if (currentElement.HasValue && currentElement.Value.GameObject != null)
                {
                    // Special case: Inside PlayBladeFolders with a folder toggle element
                    // Find and enter the corresponding folder GROUP instead of just toggling
                    var insideGroup = _groupedNavigator.CurrentGroup;
                    if (insideGroup.HasValue && insideGroup.Value.Group == ElementGroup.PlayBladeFolders)
                    {
                        string folderName = ElementGroupAssigner.GetFolderNameFromToggle(currentElement.Value.GameObject);
                        if (!string.IsNullOrEmpty(folderName))
                        {
                            // Find the matching folder group and enter it
                            // Unity's EventSystem may have already toggled the folder toggle
                            // If toggle is now OFF (was ON before Enter), we need to toggle it back ON
                            var toggle = currentElement.Value.GameObject.GetComponent<Toggle>();
                            if (toggle != null && !toggle.isOn)
                            {
                                // Folder was expanded but Unity toggled it OFF - toggle back ON
                                MelonLogger.Msg($"[{NavigatorId}] Folder toggle was OFF after Enter, re-toggling to expand");
                                UIActivator.Activate(currentElement.Value.GameObject);
                            }

                            _groupedNavigator.RequestSpecificFolderEntry(folderName);
                            TriggerRescan();
                            return true;
                        }
                    }

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
        /// PlayBlade is handled before this (by PlayBladeNavigationHelper).
        /// This handles non-PlayBlade groups: exits to group level.
        /// </summary>
        private bool HandleGroupedBackspace()
        {
            if (!_groupedNavigationEnabled || !_groupedNavigator.IsActive)
                return false;

            // Inside a subgroup - exit to parent group first
            if (_groupedNavigator.IsInsideSubgroup)
            {
                if (_groupedNavigator.ExitSubgroup())
                {
                    _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                    return true;
                }
            }

            // Inside a group - exit to group level
            if (_groupedNavigator.Level == NavigationLevel.InsideGroup)
            {
                // If exiting a folder group, toggle the folder OFF to collapse it
                var currentGroup = _groupedNavigator.CurrentGroup;
                bool wasFolderGroup = currentGroup.HasValue && currentGroup.Value.IsFolderGroup;
                // Capture PlayBlade context BEFORE toggle - UI changes might affect detection
                bool wasPlayBladeContext = _groupedNavigator.IsPlayBladeContext;

                if (wasFolderGroup && currentGroup.Value.FolderToggle != null)
                {
                    var toggle = currentGroup.Value.FolderToggle.GetComponent<Toggle>();
                    if (toggle != null && toggle.isOn)
                    {
                        LogDebug($"[{NavigatorId}] Toggling folder OFF to collapse: {currentGroup.Value.DisplayName}");
                        UIActivator.Activate(currentGroup.Value.FolderToggle);
                    }
                }

                if (_groupedNavigator.ExitGroup())
                {
                    if (wasFolderGroup)
                    {
                        // In PlayBlade context, go back to folders list after exiting a folder
                        if (wasPlayBladeContext)
                        {
                            _groupedNavigator.RequestFoldersEntry();
                            LogDebug($"[{NavigatorId}] PlayBlade folder exit - requesting folders list entry");
                        }

                        // Always rescan after folder collapse to update navigation state
                        TriggerRescan();
                    }

                    _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                    UpdateCardNavigationForGroupedElement(); // Deactivate card navigator when exiting group
                    return true;
                }
            }

            // At group level in deck builder - don't fall through to exit the deck builder
            // User must use the Done button explicitly to exit. This prevents accidental exits
            // when navigating groups with backspace.
            if (_activeContentController == "WrapperDeckBuilder")
            {
                LogDebug($"[{NavigatorId}] At group level in deck builder - backspace blocked");
                _announcer.Announce("At top level. Use Done button to exit.", Models.AnnouncementPriority.Normal);
                return true;
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

            // Handle PlayBlade activations BEFORE UIActivator.Activate
            // Helper sets up pending entry flags before blade Hide/Show events can interfere
            var elementGroup = _groupAssigner.DetermineGroup(element);
            var playBladeResult = _playBladeHelper.HandleEnter(element, elementGroup);
            // Note: Settings submenu button handling removed - handled by SettingsMenuNavigator

            // For toggles: Re-sync EventSystem selection before activating.
            // MTGA may have auto-moved selection (e.g., to submit button when form becomes valid).
            // We need to ensure EventSystem has our toggle selected so BlockSubmitForToggle works.
            // BUT: Skip if the element is no longer active (panel might have closed).
            if (isToggle && element.activeInHierarchy)
            {
                UpdateEventSystemSelectionForGroupedElement();
            }

            // Use UIActivator for CustomButtons
            var result = UIActivator.Activate(element);
            _announcer.Announce(result.Message, Models.AnnouncementPriority.Normal);

            // Note: Mailbox mail item selection is detected via Harmony patch on OnLetterSelected
            // which announces the mail content directly with actual letter data

            // Auto-play: When a deck is selected in PlayBlade, automatically press the Play button
            if (_playBladeHelper.IsActive && UIActivator.IsDeckEntry(element))
            {
                AutoPressPlayButtonInPlayBlade();
            }

            // Deck list card activated (removing card from deck) - trigger rescan to update both lists
            if (elementGroup == ElementGroup.DeckBuilderDeckList)
            {
                LogDebug($"[{NavigatorId}] Deck list card activated - scheduling rescan to update lists");
                TriggerRescan();
                return true;
            }

            // PlayBlade mode activation needs rescan (mode selection doesn't trigger panel changes)
            if (playBladeResult == PlayBladeResult.RescanNeeded)
            {
                TriggerRescan();
                return true;
            }

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

            // Dropdowns: activate and register with DropdownStateManager so we can find it when closing
            // (needed when focus goes to Blocker element instead of dropdown items)
            if (isDropdown)
            {
                LogDebug($"[{NavigatorId}] Dropdown activated ({element.name})");
                UIFocusTracker.EnterDropdownEditMode(element);
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

            // Note: Rewards popup ClaimButton handling moved to RewardPopupNavigator

            return true;
        }

        /// <summary>
        /// Called after a deck builder card is activated (collection or deck list).
        /// Triggers rescan to update the deck/collection display.
        /// </summary>
        protected override void OnDeckBuilderCardActivated()
        {
            if (_activeContentController == "WrapperDeckBuilder")
            {
                LogDebug($"[{NavigatorId}] Deck builder card activated - scheduling rescan to update lists");
                TriggerRescan();
            }
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
