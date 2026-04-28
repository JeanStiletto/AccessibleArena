using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.PanelDetection;
using AccessibleArena.Core.Services.ElementGrouping;
using AccessibleArena.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using static AccessibleArena.Core.Constants.SceneNames;
using SceneNames = AccessibleArena.Core.Constants.SceneNames;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using AccessibleArena.Core.Utils;

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
    public partial class GeneralMenuNavigator : BaseNavigator
    {
        #region Configuration Constants

        // Scenes where this navigator should NOT activate (handled by other navigators)
        private static readonly HashSet<string> ExcludedScenes = new HashSet<string>
        {
            SceneNames.Bootstrap, AssetPrep, DuelScene, DraftScene, SealedScene, SceneNames.MatchEndScene, SceneNames.PreGameScene
        };

        // Minimum CustomButtons needed to consider this a menu
        private const int MinButtonsForMenu = 2;

        // Blade container name patterns (used for element filtering when blade is active)
        private static readonly string[] BladePatterns = new[]
        {
            "Blade",
            "FindMatch"
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

        protected string _currentScene;
        protected string _detectedMenuType;
        private bool _hasLoggedUIOnce;
        private float _activationDelay;
        private bool _announcedServerLoading;

        // Helper instances
        private readonly MenuScreenDetector _screenDetector;

        // Element grouping infrastructure (Phase 2 of element grouping feature)
        private readonly OverlayDetector _overlayDetector;
        private readonly ElementGroupAssigner _groupAssigner;
        private readonly GroupedNavigator _groupedNavigator;
        private readonly PlayBladeNavigationHelper _playBladeHelper;
        private readonly ChallengeNavigationHelper _challengeHelper;

        /// <summary>
        /// Whether grouped (hierarchical) navigation is enabled.
        /// When true, elements are organized into groups for two-level navigation.
        /// </summary>
        private bool _groupedNavigationEnabled = true;

        // Rescan timing
        private float _rescanDelay;
        private bool _suppressRescanAnnouncement;

        // Element tracking
        private float _bladeAutoExpandDelay;

        // NPE scene: periodic button check for dynamic buttons (e.g., "Play" appearing after dialogue)
        private const float NPEButtonCheckInterval = 0.5f;
        private float _npeButtonCheckTimer;
        private int _lastNPEButtonCount;

        // Overlay state tracking - detect when overlays open/close to trigger rescans
        private ElementGroup? _lastKnownOverlay;

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

        // Cached reflection for LoadingPanelShowing.IsShowing (static property on game type)
        private static PropertyInfo _loadingPanelIsShowingProp;
        private static bool _loadingPanelReflectionResolved;

        /// <summary>
        /// Check if the game's loading panel overlay is currently showing.
        /// Uses reflection to access MTGA.LoadingPanelShowing.IsShowing static property.
        /// </summary>
        private static bool IsLoadingPanelShowing()
        {
            if (!_loadingPanelReflectionResolved)
            {
                _loadingPanelReflectionResolved = true;
                var type = FindType("MTGA.LoadingPanelShowing");
                if (type != null)
                    _loadingPanelIsShowingProp = type.GetProperty("IsShowing", BindingFlags.Public | BindingFlags.Static);
            }

            if (_loadingPanelIsShowingProp == null)
                return false;

            return (bool)_loadingPanelIsShowingProp.GetValue(null);
        }

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
            _challengeHelper = new ChallengeNavigationHelper(_groupedNavigator, announcer);
            // Subscribe to PanelStateManager for rescan triggers
            // (popup detection is enabled/disabled in OnActivated/OnDeactivating)
            if (PanelStateManager.Instance != null)
            {
                PanelStateManager.Instance.OnPanelChanged += OnPanelStateManagerActiveChanged;
                PanelStateManager.Instance.OnAnyPanelOpened += OnPanelStateManagerAnyOpened;
                PanelStateManager.Instance.OnPlayBladeStateChanged += OnPanelStateManagerPlayBladeChanged;
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
            CheckAndInitChallengeHelper("ActiveChanged");

            // Ignore SocialUI as SOURCE of change - it's just the corner icon, causes spurious rescans
            // But DO rescan when something closes and falls back to SocialUI (e.g., popup closes)
            if (oldPanel?.Name?.Contains("SocialUI") == true)
            {
                Log.Nav(NavigatorId, $"Ignoring SocialUI as source of change");
                return;
            }

            // Ignore Settings panel changes - handled by SettingsMenuNavigator
            if (oldPanel?.Name?.Contains("SettingsMenu") == true || newPanel?.Name?.Contains("SettingsMenu") == true)
            {
                Log.Nav(NavigatorId, $"Ignoring SettingsMenu panel change");
                return;
            }

            // Popup transitions are handled by base popup mode infrastructure
            // (via EnablePopupDetection + OnPopupDetected/OnPopupClosed overrides)
            // Skip rescan when a popup is active or just opened - base popup mode owns navigation
            if (IsInPopupMode || (newPanel != null && IsPopupPanel(newPanel)))
                return;

            // Reset mail detail view state when mailbox closes
            if (oldPanel?.Name?.Contains("Mailbox") == true && newPanel?.Name?.Contains("Mailbox") != true)
            {
                if (_isInMailDetailView)
                {
                    Log.Nav(NavigatorId, $"Mailbox closed - resetting mail detail view state");
                    _isInMailDetailView = false;
                    ResetMailFieldNavigation();
                }
            }

            Log.Nav(NavigatorId, $"PanelStateManager active changed: {oldPanel?.Name ?? "none"} -> {newPanel?.Name ?? "none"}");

            TriggerRescan();
        }

        /// <summary>
        /// Check if PlayBlade became active and initialize the helper if needed.
        /// Called from multiple event handlers to ensure we catch the blade opening.
        /// </summary>
        private void CheckAndInitPlayBladeHelper(string source)
        {
            // Challenge screen uses PlayBladeState >= 2; skip normal PlayBlade init for those
            int bladeState = PanelStateManager.Instance?.PlayBladeState ?? 0;
            if (bladeState >= 2)
            {
                // If PlayBlade helper was active for regular blade, close it
                if (_playBladeHelper.IsActive)
                {
                    Log.Msg("{NavigatorId}", $"CheckPlayBlade({source}): Challenge state {bladeState} - closing PlayBlade helper");
                    _playBladeHelper.OnPlayBladeClosed();
                }
                return; // Challenge handled by CheckAndInitChallengeHelper
            }

            bool isPlayBladeNowActive = PanelStateManager.Instance?.IsPlayBladeActive == true;
            bool helperIsActive = _playBladeHelper.IsActive;

            Log.Msg("{NavigatorId}", $"CheckPlayBlade({source}): IsPlayBladeActive={isPlayBladeNowActive}, HelperIsActive={helperIsActive}");

            if (isPlayBladeNowActive && !helperIsActive)
            {
                Log.Msg("{NavigatorId}", $"PlayBlade became active - initializing helper");
                _playBladeHelper.OnPlayBladeOpened();
            }
            else if (!isPlayBladeNowActive && helperIsActive)
            {
                Log.Msg("{NavigatorId}", $"PlayBlade became inactive - resetting helper");
                _playBladeHelper.OnPlayBladeClosed();
            }
        }

        /// <summary>
        /// Detect challenge screen state changes and initialize/close ChallengeNavigationHelper.
        /// PlayBladeState 2 = DirectChallenge, 3 = FriendChallenge.
        /// </summary>
        private void CheckAndInitChallengeHelper(string source)
        {
            int bladeState = PanelStateManager.Instance?.PlayBladeState ?? 0;
            bool isChallengeNow = bladeState >= 2;
            bool helperIsActive = _challengeHelper.IsActive;

            if (isChallengeNow && !helperIsActive)
            {
                Log.Msg("{NavigatorId}", $"Challenge screen became active (state={bladeState}) - initializing helper ({source})");
                _challengeHelper.OnChallengeOpened();
                // Rescan so EnhanceButtonLabel can apply challenge-specific labels.
                // The initial scan may have run before the challenge context was set.
                TriggerRescan();
            }
            else if (!isChallengeNow && helperIsActive)
            {
                Log.Msg("{NavigatorId}", $"Challenge screen became inactive - closing helper ({source})");
                _challengeHelper.OnChallengeClosed();
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
            CheckAndInitChallengeHelper("AnyOpened");

            // SocialUI events: only rescan if the Friends panel is actually open
            // The corner social button triggers SocialUI events but shouldn't cause rescans
            // Note: Mailbox is detected separately via ContentControllerPlayerInbox patch
            if (panel?.Name?.Contains("SocialUI") == true)
            {
                if (!_screenDetector.IsSocialPanelOpen())
                {
                    Log.Nav(NavigatorId, $"Ignoring SocialUI event - Friends panel not open (just corner icon)");
                    return;
                }
                Log.Nav(NavigatorId, $"SocialUI event with Friends panel open - allowing rescan");
            }

            // Ignore Settings panel - handled by SettingsMenuNavigator
            if (panel?.Name?.Contains("SettingsMenu") == true)
            {
                Log.Nav(NavigatorId, $"Ignoring SettingsMenu panel opened");
                return;
            }

            Log.Nav(NavigatorId, $"PanelStateManager panel opened: {panel?.Name ?? "none"}");

            // Auto-expand blade for Color Challenge menu
            if (panel?.Name?.Contains("CampaignGraph") == true)
            {
                _bladeAutoExpandDelay = BladeAutoExpandDelay;
                Log.Nav(NavigatorId, $"Scheduling blade auto-expand for Color Challenge");
            }

            TriggerRescan();
        }

        /// <summary>
        /// Handler for PanelStateManager.OnPlayBladeStateChanged - fires when blade state changes.
        /// This is a direct signal that bypasses the panel debounce, ensuring the blade helper
        /// is initialized even when OnAnyPanelOpened is debounced away.
        /// </summary>
        private void OnPanelStateManagerPlayBladeChanged(int state)
        {
            if (!_isActive) return;

            Log.Msg("{NavigatorId}", $"PlayBladeStateChanged: state={state}");
            CheckAndInitPlayBladeHelper("BladeStateChanged");
            CheckAndInitChallengeHelper("BladeStateChanged");

            TriggerRescan();
        }

        #region Booster Carousel

        #endregion

        protected virtual string GetMenuScreenName()
        {
            // Note: Settings check removed - handled by SettingsMenuNavigator

            // Mail detail view: label the screen as Mail, not the underlying Home page.
            // Otherwise reactivations (e.g. when a reward popup closes) announce
            // "Home with Color Challenge" over a mail screen.
            if (_isInMailDetailView && _overlayDetector != null &&
                _overlayDetector.GetActiveOverlay() == ElementGroup.MailboxContent)
            {
                return LocaleManager.Instance.Get("GroupMail");
            }

            // Check if Social/Friends panel is open
            if (IsSocialPanelOpen())
            {
                return Strings.ScreenFriends;
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
                        return Strings.ScreenHome;
                    else if (hasCarousel)
                        return Strings.ScreenHomeWithEvents;
                    else if (hasColorChallenge)
                        return Strings.ScreenHomeWithColorChallenge;
                    else
                        return Strings.ScreenHome;
                }

                // ReadOnly deck builder gets distinct screen name
                if (_isDeckBuilderReadOnly && _activeContentController == T.WrapperDeckBuilder)
                    return Strings.ScreenDeckBuilderReadOnly;

                // Event page: append event title (e.g., "Event: Jump In")
                if (IsEventPageController(_activeContentController))
                {
                    string eventTitle = EventAccessor.GetEventPageTitle();
                    if (!string.IsNullOrEmpty(eventTitle))
                        return Strings.EventScreenTitle(eventTitle);
                }

                // Packet selection: append packet number (e.g., "Packet Selection, Packet 1 of 2")
                if (_activeContentController == "PacketSelectContentController")
                {
                    string packetSummary = EventAccessor.GetPacketScreenSummary();
                    if (!string.IsNullOrEmpty(packetSummary))
                        return $"{baseName}, {packetSummary}";
                }

                return baseName;
            }

            // Check for NPE rewards screen (card unlocked)
            // This is checked separately because we don't filter elements to it
            if (_screenDetector.IsNPERewardsScreenActive())
            {
                return Strings.ScreenCardUnlocked;
            }

            // Fall back to detected menu type from button patterns
            if (!string.IsNullOrEmpty(_detectedMenuType))
                return _detectedMenuType;

            // Last resort: use scene name
            return _currentScene switch
            {
                "HomePage" => Strings.ScreenHome,
                "NavBar" => Strings.ScreenNavigationBar,
                "Store" => Strings.ScreenStore,
                "Collection" => Strings.ScreenCollection,
                "Decks" => Strings.ScreenDecks,
                "Profile" => Strings.ScreenProfile,
                "Settings" => Strings.ScreenSettings,
                _ => Strings.ScreenMenu
            };
        }

        public override void OnSceneChanged(string sceneName)
        {
            _currentScene = sceneName;
            _hasLoggedUIOnce = false;
            _activationDelay = ActivationDelaySeconds;
            _isDeckBuilderReadOnly = false;

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
        protected override void OnActivated()
        {
            base.OnActivated();
            EnablePopupDetection();
        }

        protected override void OnPopupDetected(PanelInfo panel)
        {
            // Save grouped navigation state before entering popup mode so we can
            // restore the user's group and element position when the popup closes.
            // Base EnterPopupMode only saves the flat element list, not group state.
            // Skip when already in popup mode (stacked popup) — we'd overwrite the original save.
            if (!IsInPopupMode && _groupedNavigationEnabled && _groupedNavigator.IsActive)
                _groupedNavigator.SaveCurrentGroupForRestore();

            base.OnPopupDetected(panel);
        }

        protected override void DiscoverPopupElements(GameObject popup)
        {
            base.DiscoverPopupElements(popup);

            // Replace generic tile labels ("Avatare", "Begleiter", "Kartenhüllen", ...) with
            // value-rich ones ("Avatar: Standard, press Enter to change") on the deck-details popup.
            // Re-runs on stacked-popup pop-back so a freshly-picked avatar is reflected immediately.
            if (HasComponentInChildren(popup, "DeckDetailsPopup"))
                EnrichDeckCosmeticTileLabels();
        }

        protected override void OnDeactivating()
        {
            // Save grouped navigation state when being preempted by another navigator
            // (e.g., ChatNavigator) so we can restore position when we reactivate.
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive && !_groupedNavigator.HasPendingRestore)
                _groupedNavigator.SaveCurrentGroupForRestore();

            base.OnDeactivating();
            DisablePopupDetection();

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

            // Get old collection/sideboard count before rescan (if grouped navigation is active)
            int oldPoolCount = 0;
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                // Check both collection and sideboard (draft uses sideboard for pool cards)
                oldPoolCount = _groupedNavigator.GetGroupElementCount(ElementGrouping.ElementGroup.DeckBuilderCollection)
                             + _groupedNavigator.GetGroupElementCount(ElementGrouping.ElementGroup.DeckBuilderSideboard);

                // Save current group position so rescan restores it
                // (Tab already cycled to Collection, don't lose that)
                if (!_groupedNavigator.HasPendingRestore)
                {
                    _groupedNavigator.SaveCurrentGroupForRestore();
                }
            }

            // Do the rescan WITHOUT announcement (copy logic from base, skip announce)
            _elements.Clear();
            _currentIndex = -1;
            DiscoverElements();

            // Inject virtual groups/elements based on context
            if (_activeContentController == T.WrapperDeckBuilder)
            {
                InjectDeckInfoGroup();
            }
            else if (IsEventPageController(_activeContentController))
            {
                InjectEventInfoGroup();
            }
            InjectChallengeStatusElement();
            if (_elements.Count > 0)
            {
                _currentIndex = 0;
                UpdateEventSystemSelection();
            }

            // After rescan, announce the results
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                int newPoolCount = _groupedNavigator.GetGroupElementCount(ElementGrouping.ElementGroup.DeckBuilderCollection)
                               + _groupedNavigator.GetGroupElementCount(ElementGrouping.ElementGroup.DeckBuilderSideboard);
                Log.Msg("{NavigatorId}", $"Search rescan pool: {oldPoolCount} -> {newPoolCount} cards");

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
                else if (newPoolCount != oldPoolCount)
                {
                    // Normal case (Escape from search) - just announce count change
                    if (newPoolCount == 0)
                    {
                        _announcer.AnnounceInterrupt("No search results");
                    }
                    else
                    {
                        _announcer.AnnounceInterrupt(Models.Strings.SearchResults(newPoolCount));
                    }
                }
            }
            else
            {
                // Fallback for non-grouped contexts: use total element count
                Log.Msg("{NavigatorId}", $"Search rescan (non-grouped): {_elements.Count} elements");
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
                Log.Nav(NavigatorId, $"Settings menu detected - deactivating to let SettingsMenuNavigator take over");
                return false;
            }

            // Deactivate if NPE rewards popup becomes active - let NPERewardNavigator handle it
            if (_screenDetector.IsNPERewardsScreenActive())
            {
                Log.Nav(NavigatorId, $"NPE rewards screen detected - deactivating to let NPERewardNavigator take over");
                return false;
            }

            // Note: We no longer deactivate on IsLoadingPanelShowing().
            // DetectScreen() still prevents initial activation during loading.
            // If already active and a loading panel appears during a real transition,
            // elements will be destroyed and base.ValidateElements() handles deactivation.
            // If the game gets stuck in a loading state, this keeps the navigator active
            // so the user can still use Backspace to navigate back.

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

            // Handle delayed page rescan (after collection page scroll animation)
            if (_pendingPageRescanFrames > 0)
            {
                _pendingPageRescanFrames--;

                // Short-circuit: if animation finished early, rescan immediately
                if (_pendingPageRescanFrames > 2 && !CardPoolAccessor.IsScrolling())
                {
                    _pendingPageRescanFrames = 0;
                }

                if (_pendingPageRescanFrames == 0)
                {
                    Log.Nav(NavigatorId, $"Executing delayed page rescan");
                    PerformRescan();
                }
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
                Log.Msg("{NavigatorId}", $"Overlay changed: {_lastKnownOverlay?.ToString() ?? "none"} -> {currentOverlay?.ToString() ?? "none"}");
                _lastKnownOverlay = currentOverlay;

                // When the overlay changes to a PlayBlade state, also initialize the play blade helper.
                // This catches the case where the blade is opened from a home screen objective
                // (the Harmony panel events may fire before Btn_BladeIsOpen is active, causing
                // CheckAndInitPlayBladeHelper to miss it; the overlay poll detects it reliably).
                if (currentOverlay == ElementGroup.PlayBladeTabs || currentOverlay == ElementGroup.ChallengeMain)
                {
                    CheckAndInitPlayBladeHelper("OverlayChange");
                    CheckAndInitChallengeHelper("OverlayChange");
                }

                // Only trigger rescan if we're not already waiting for one
                if (_rescanDelay <= 0)
                {
                    TriggerRescan();
                }
            }

            // Challenge screen: poll for opponent status changes
            if (_challengeHelper.IsActive)
            {
                _challengeHelper.Update(Time.deltaTime);
                if (_challengeHelper.RescanRequested)
                {
                    _challengeHelper.ClearRescanRequest();
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
            // Shift+Enter on a focused deck-builder card opens the card-viewer popup.
            // Must run BEFORE the grouped-Enter handler below, which calls GetEnterAndConsume()
            // and routes activation to ActivateCurrentElement (without checking Shift).
            if (_activeContentController == T.WrapperDeckBuilder
                && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            {
                if (TryOpenCardViewerForFocusedCard())
                {
                    InputManager.ConsumeKey(KeyCode.Return);
                    InputManager.ConsumeKey(KeyCode.KeypadEnter);
                    return true;
                }
            }

            // F4: Toggle Friends panel
            if (Input.GetKeyDown(KeyCode.F4))
            {
                Log.Nav(NavigatorId, $"F4 pressed - toggling Friends panel");
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
            if (Input.GetKeyDown(KeyCode.F11) && _activeContentController == T.BoosterChamberController)
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
                    _announcer.Announce(Models.Strings.NoElementSelected, Models.AnnouncementPriority.High);
                }
                return true;
            }

            // Enter/Space: In grouped navigation mode, handle both group entry and element activation
            // Use GetEnterAndConsume to prevent game from also processing Enter on EventSystem selected object
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive && InputManager.GetEnterAndConsume())
            {
                if (HandleGroupedEnter())
                    return true;
            }

            // Space: Route through grouped activation when grouped navigation is active.
            // Without this, Space falls through to BaseNavigator which uses stale _currentIndex.
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive
                && AcceptSpaceKey && InputManager.GetKeyDownAndConsume(KeyCode.Space))
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
                    Log.Nav(NavigatorId, $"Backspace pressed but already consumed - skipping");
                    return true; // Key was handled elsewhere
                }

                if (UIFocusTracker.IsAnyInputFieldFocused())
                {
                    Log.Nav(NavigatorId, $"Backspace pressed but input field focused - passing through");
                    return false; // Let it pass through to the input field
                }

                // Challenge helper gets priority over PlayBlade
                if (_challengeHelper.IsActive)
                {
                    var challengeResult = _challengeHelper.HandleBackspace();
                    switch (challengeResult)
                    {
                        case PlayBladeResult.CloseBlade:
                            return ClosePlayBlade();
                        case PlayBladeResult.RescanNeeded:
                            TriggerRescan();
                            return true;
                        case PlayBladeResult.Handled:
                            return true;
                    }
                }

                // PlayBlade navigation
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

                // Not PlayBlade/Challenge - use grouped navigation if inside a group
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
                    Log.Nav(NavigatorId, $"Escape pressed while input field focused - deactivating field");
                    UIFocusTracker.DeactivateFocusedInputField();
                    _announcer.Announce(Models.Strings.ExitedInputField, Models.AnnouncementPriority.Normal);
                    return true;
                }
            }

            // Arrow Left/Right: Handle special navigation contexts
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                bool isRight = Input.GetKeyDown(KeyCode.RightArrow);

                // DeckBuilderInfo 2D sub-navigation: Left/Right navigates entries within current row
                if (IsDeckInfoSubNavActive())
                {
                    HandleDeckInfoEntryNavigation(isRight);
                    return true;
                }

                // Friend section sub-navigation: Left/Right cycles available actions
                if (IsFriendSectionActive() && _friendActions != null && _friendActions.Count > 0)
                {
                    HandleFriendActionNavigation(isRight);
                    return true;
                }

                // Packet selection: Left/Right navigates info blocks within current packet
                if (IsInPacketSelectionContext() && _packetBlocks != null && _packetBlocks.Count > 0)
                {
                    HandlePacketBlockNavigation(isRight);
                    return true;
                }

                // Booster carousel navigation (packs screen) - only when focused on a carousel element
                if (_isBoosterCarouselActive && _boosterPackHitboxes.Count > 0
                    && IsValidIndex && _boosterPackHitboxes.Contains(_elements[_currentIndex].GameObject))
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
                if (_activeContentController == T.WrapperDeckBuilder)
                {
                    bool isPageDown = Input.GetKeyDown(KeyCode.PageDown);
                    if (ActivateCollectionPageButton(isPageDown))
                        return true;
                }
            }

            // Number keys 1-0: Activate filter options in deck builder
            // 1-9 = options 1-9, 0 = option 10
            if (_activeContentController == T.WrapperDeckBuilder && _groupedNavigationEnabled && _groupedNavigator.IsActive)
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

        // Note: HandleSettingsBack(), FindSettingsBackButton(), CloseSettingsMenu() removed
        // Settings navigation is now handled by SettingsMenuNavigator

        /// <summary>
        /// Debug: Dump current UI hierarchy to log for development.
        /// Press F12 to trigger this after opening a new UI overlay.
        /// </summary>
        private void DumpUIHierarchy()
        {
            // If challenge screen is active, do a deep challenge blade dump instead
            if (_challengeHelper.IsActive)
            {
                MenuDebugHelper.DumpChallengeBlade(NavigatorId, _announcer);
                return;
            }
            MenuDebugHelper.DumpUIHierarchy(NavigatorId, _announcer);
        }

        /// <summary>
        /// Check if element should be shown based on current foreground layer.
        /// Uses OverlayDetector for overlay detection, falls back to content panel filtering.
        /// </summary>
        private bool ShouldShowElement(GameObject obj)
        {
            // Exclude the settings-gear icon globally - always reachable via F2 / Escape.
            // Historical MainNav prefab names it "Options_Button"; the Login-scene variant is
            // a plain "Button" inside SettingsButton_Desktop_*(Clone)/SettingsButtonRoot.
            if (obj.name == "Options_Button" || IsSettingsGearButton(obj))
                return false;

            // Exclude Leave button in challenge screen - Backspace shortcut handles this
            if (obj.name == "MainButton_Leave")
                return false;

            // Exclude unlabeled enemy player card background in challenge screen (no opponent yet)
            if (obj.name == "Clicker")
                return false;

            // In mail content view, filter out buttons that have no actual text content
            // (e.g., "SecondaryButton_v2" which only shows its object name)
            if (_isInMailDetailView && _overlayDetector.GetActiveOverlay() == ElementGroup.MailboxContent)
            {
                if (IsMailContentButtonWithNoText(obj))
                {
                    Log.Nav(NavigatorId, $"Filtering mail button with no text: {obj.name}");
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
                Log.Nav(NavigatorId, $"Blade_ListItem bypass for PlayBlade: {obj.name}");
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
        /// Check if element is inside the current content panel.
        /// Special handling for CampaignGraph which allows blade elements.
        /// </summary>
        private bool IsChildOfContentPanel(GameObject obj)
        {
            if (_activeControllerGameObject == null)
                return true;

            // Special case: Color Challenge - content page with embedded blade layout
            // Allow blade content (color buttons) and main buttons, but filter blade state buttons
            if (_activeContentController == T.CampaignGraphContentController)
            {
                // Filter blade state buttons - internal chrome, not user actions
                if (obj.name.StartsWith("Btn_Blade"))
                    return false;

                if (IsChildOf(obj, _activeControllerGameObject)) return true;
                if (IsInsideBlade(obj)) return true;
                if (IsMainButton(obj)) return true;
                return false;
            }

            // Allow Nav_WildCard in deck builder and booster screens so blind users can check wildcard counts
            if (obj.name == "Nav_WildCard" &&
                (_activeContentController == T.WrapperDeckBuilder || _activeContentController == T.BoosterChamberController))
                return true;

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
        private static bool IsChildOf(GameObject child, GameObject parent)
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

        /// <summary>
        /// Check if a GameObject is a popup overlay (Popup or SystemMessageView).
        /// </summary>
        private static bool IsPopupOverlay(GameObject obj)
        {
            if (obj == null) return false;
            string name = obj.name;
            return name.Contains("Popup") || name.Contains("SystemMessageView") || name.Contains("ChallengeInviteWindow");
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
        /// Detect the scene-level settings-gear button. Login-scene variant lives under
        /// SettingsButtonRoot/SettingsButton_Desktop_*(Clone) and has the bare GO name
        /// "Button", so matching on name alone is insufficient. Walk up a few levels.
        /// </summary>
        private static bool IsSettingsGearButton(GameObject obj)
        {
            if (obj == null) return false;

            Transform current = obj.transform;
            int levels = 0;
            const int maxLevels = 4;
            while (current != null && levels < maxLevels)
            {
                string name = current.name;
                if (name.StartsWith("SettingsButton_", StringComparison.Ordinal)
                    || name == "SettingsButtonRoot")
                    return true;
                current = current.parent;
                levels++;
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
        /// Auto-press the Play button in PlayBlade after deck selection.
        /// Finds the MainButton (named "MainButton" without MainButton component) and activates it.
        /// </summary>
        private void AutoPressPlayButtonInPlayBlade()
        {
            // Find the PlayBlade Play button by searching the scene directly.
            // Cannot use _elements because MainButton is classified as Unknown and excluded.
            // The PlayBlade Play button is named "MainButton" but does NOT have the MainButton component
            // (the Home page Play button has the MainButton component, PlayBlade's doesn't).
            foreach (var t in GameObject.FindObjectsOfType<Transform>())
            {
                if (t == null || t.name != "MainButton" || !t.gameObject.activeInHierarchy)
                    continue;

                // Must be inside PlayBlade hierarchy
                if (!IsInsidePlayBladeContainer(t.gameObject))
                    continue;

                // Verify it's NOT the Home page MainButton (which has MainButton component)
                bool hasMainButtonComponent = false;
                var components = t.gameObject.GetComponents<MonoBehaviour>();
                foreach (var comp in components)
                {
                    if (comp != null && comp.GetType().Name == "MainButton")
                    {
                        hasMainButtonComponent = true;
                        break;
                    }
                }

                if (!hasMainButtonComponent)
                {
                    Log.Msg("{NavigatorId}", $"Auto-pressing Play button after deck selection");
                    UIActivator.Activate(t.gameObject);
                    return;
                }
            }

            Log.Msg("{NavigatorId}", $"PlayBlade Play button not found for auto-press");
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
                Log.Nav(NavigatorId, $"NPE button count changed: {_lastNPEButtonCount} -> {currentCount}, triggering rescan");
                _lastNPEButtonCount = currentCount;
                TriggerRescan();
            }
        }

        /// <summary>
        /// Override spinner rescan to handle grouped navigation state.
        /// In PlayBlade context (challenge mode), SaveCurrentGroupForRestore is skipped
        /// by GroupedNavigator (by design). Instead we use RequestPlayBladeContentEntryAtIndex
        /// to auto-enter the PlayBladeContent group at the user's current stepper position.
        /// </summary>
        protected override void RescanAfterSpinnerChange()
        {
            if (!IsActive || !IsValidIndex) return;

            // Save state for grouped navigation restoration
            bool usePlayBladeContentRestore = false;
            bool useChallengeMainRestore = false;
            int groupedElementIndex = -1;

            if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                var currentGroup = _groupedNavigator.CurrentGroup;
                if (_groupedNavigator.IsChallengeContext &&
                    currentGroup.HasValue &&
                    currentGroup.Value.Group == ElementGroup.ChallengeMain)
                {
                    // Challenge context: use explicit ChallengeMain entry with element index.
                    groupedElementIndex = _groupedNavigator.CurrentElementIndex;
                    useChallengeMainRestore = true;
                }
                else if (_groupedNavigator.IsPlayBladeContext &&
                    currentGroup.HasValue &&
                    currentGroup.Value.Group == ElementGroup.PlayBladeContent)
                {
                    // PlayBlade context: SaveCurrentGroupForRestore is skipped (by design).
                    // Use explicit content entry with element index instead.
                    groupedElementIndex = _groupedNavigator.CurrentElementIndex;
                    usePlayBladeContentRestore = true;
                }
                else if (!_groupedNavigator.HasPendingRestore)
                {
                    _groupedNavigator.SaveCurrentGroupForRestore();
                }
            }

            var currentObj = _elements[_currentIndex].GameObject;
            int oldCount = _elements.Count;

            // Request auto-entry BEFORE rescan so OrganizeIntoGroups processes it
            if (useChallengeMainRestore)
            {
                _groupedNavigator.RequestChallengeMainEntryAtIndex(groupedElementIndex);
            }
            else if (usePlayBladeContentRestore)
            {
                _groupedNavigator.RequestPlayBladeContentEntryAtIndex(groupedElementIndex);
            }

            // Challenge context: close DeckSelectBlade that auto-opens on spinner change
            // This prevents inconsistent element counts (8 -> 3 -> 6 fluctuation)
            if (_challengeHelper.IsActive)
                ChallengeNavigationHelper.CloseDeckSelectBlade();

            // Re-discover elements (rebuilds groups via OrganizeIntoGroups)
            _elements.Clear();
            _currentIndex = -1;
            DiscoverElements();

            if (_elements.Count == 0) return;

            // Try to restore focus to the same element in flat list
            if (currentObj != null)
            {
                for (int i = 0; i < _elements.Count; i++)
                {
                    if (_elements[i].GameObject == currentObj)
                    {
                        _currentIndex = i;
                        break;
                    }
                }
            }

            if (_currentIndex < 0)
                _currentIndex = 0;

            // Announce count changes - but only in flat navigation mode
            // In grouped mode, the stepper value was already announced and
            // the group state is restored by auto-entry or SaveCurrentGroupForRestore
            if (_elements.Count != oldCount)
            {
                Log.Msg("{NavigatorId}", $"Spinner rescan: {oldCount} -> {_elements.Count} elements");
                if (!_groupedNavigationEnabled || !_groupedNavigator.IsActive)
                {
                    string posAnnouncement = Models.Strings.ItemPositionOf(
                        _currentIndex + 1, _elements.Count, _elements[_currentIndex].Label);
                    _announcer.Announce(posAnnouncement, Models.AnnouncementPriority.Normal);
                }
            }

            // Challenge context: announce tournament parameters after mode spinner change
            if (_challengeHelper.IsActive)
            {
                var tournamentSummary = _challengeHelper.GetTournamentParametersSummary();
                if (!string.IsNullOrEmpty(tournamentSummary))
                    _announcer.Announce(tournamentSummary, Models.AnnouncementPriority.Normal);
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
            // Skip rescan while popup is active - base popup mode owns discovery
            if (IsInPopupMode)
            {
                Log.Nav(NavigatorId, $"Skipping rescan - popup active");
                return;
            }

            // Store previous controller to detect screen transitions
            var previousController = _activeContentController;

            // Use the card count captured before activation (before game processed add/remove)
            string previousCardCount = _deckCountBeforeActivation;

            // Detect active controller BEFORE discovering elements so filtering works correctly
            DetectActiveContentController();
            Log.Nav(NavigatorId, $"Rescanning elements after panel change (controller: {_activeContentController ?? "none"})");

            // Remember the navigator's current selection before clearing
            GameObject previousSelection = null;
            if (_currentIndex >= 0 && _currentIndex < _elements.Count)
            {
                previousSelection = _elements[_currentIndex].GameObject;
            }

            // Save current group state for restoration after rescan - but only if same screen
            // (don't restore old screen's state when transitioning to a new screen)
            // Skip if a restore is already pending (e.g., page navigation set it with reset index)
            if (_groupedNavigationEnabled && previousController == _activeContentController && !_groupedNavigator.HasPendingRestore)
            {
                _groupedNavigator.SaveCurrentGroupForRestore();
            }

            // Clear and rediscover
            _elements.Clear();
            _currentIndex = 0;

            DiscoverElements();

            // Inject virtual groups/elements based on context
            if (_activeContentController == T.WrapperDeckBuilder)
            {
                InjectDeckInfoGroup();
            }
            else if (IsEventPageController(_activeContentController))
            {
                InjectEventInfoGroup();
            }
            InjectChallengeStatusElement();
            // Try to find the previously selected object in the new element list
            if (previousSelection != null)
            {
                for (int i = 0; i < _elements.Count; i++)
                {
                    if (_elements[i].GameObject == previousSelection)
                    {
                        _currentIndex = i;
                        Log.Nav(NavigatorId, $"Preserved selection at index {i}: {previousSelection.name}");
                        break;
                    }
                }
            }

            // Update menu type based on new state
            _detectedMenuType = DetectMenuType();

            // When a card was added/removed, announce just the card count if it changed
            if (_announceDeckCountOnRescan && _activeContentController == T.WrapperDeckBuilder)
            {
                _announceDeckCountOnRescan = false;
                _deckCountBeforeActivation = null;
                string cardCount = DeckInfoProvider.GetCardCountText();
                if (!string.IsNullOrEmpty(cardCount) && cardCount != previousCardCount)
                {
                    _announcer.AnnounceInterrupt(cardCount);
                }
                // Re-prepare card navigation after rescan so card detail
                // navigation works immediately (e.g., after popup close)
                UpdateCardNavigationForGroupedElement();
                return;
            }

            // Skip full screen announcement if position was restored (same screen, same context)
            if (_groupedNavigationEnabled && _groupedNavigator.PositionWasRestored)
                return;

            // Skip announcement when HandleGroupedBackspace already announced (e.g., folder exit)
            if (_suppressRescanAnnouncement)
            {
                _suppressRescanAnnouncement = false;
                return;
            }

            // Announce the change
            string announcement = GetActivationAnnouncement();
            _announcer.Announce(announcement, Models.AnnouncementPriority.High);

            // Non-grouped screens (Login/Register/Birth/Mail/Home) would otherwise stop at
            // just the menu name — follow up with the first element so the user knows where
            // focus landed without having to press an arrow key. Grouped navigation already
            // includes the first element in its activation announcement.
            if (!(_groupedNavigationEnabled && _groupedNavigator.IsActive))
            {
                string first = GetElementAnnouncement(_currentIndex);
                if (!string.IsNullOrEmpty(first))
                    _announcer.Announce(first, Models.AnnouncementPriority.High);
            }
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

            // Don't activate when Store content controller is active - let StoreNavigator handle it
            // Refresh detection to ensure we have current state
            if (DetectActiveContentController() == T.ContentControllerStoreCarousel)
                return false;

            // Don't activate when Mastery/Rewards screen is active - let MasteryNavigator handle it
            if (DetectActiveContentController() == T.ProgressionTracksContentController)
                return false;

            // Don't activate when Codex/LearnToPlay is active - let CodexNavigator handle it
            if (DetectActiveContentController() == "LearnToPlayControllerV2")
                return false;

            // Don't activate when Achievements screen is active - let AchievementsNavigator handle it
            if (DetectActiveContentController() == T.AchievementsContentController)
                return false;

            // Don't activate when game loading panel overlay is showing (e.g. after scene transition)
            if (IsLoadingPanelShowing())
            {
                if (!_announcedServerLoading)
                {
                    _announcedServerLoading = true;
                    _announcer.AnnounceInterrupt(Strings.WaitingForServer);
                }
                return false;
            }
            _announcedServerLoading = false;

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

            Log.Nav(NavigatorId, $"Detected menu: {_detectedMenuType} with {customButtonCount} CustomButtons");
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
            if (playButton != null) return Strings.ScreenHome;

            // Check for store
            var storeIndicator = FindButtonByPattern("Purchase", "Buy", "Pack", "Bundle");
            if (storeIndicator != null) return Strings.ScreenStore;

            // Check for collection/decks
            var deckIndicator = FindButtonByPattern("Deck", "Collection", "Card");
            if (deckIndicator != null) return Strings.ScreenCollection;

            // Check for settings
            var settingsIndicator = FindButtonByPattern("Settings", "Options", "Audio", "Graphics");
            if (settingsIndicator != null) return Strings.ScreenSettings;

            return Strings.ScreenMenu;
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
            if (_activeContentController == T.BoosterChamberController)
                _groupedNavigationEnabled = false;

            // Debug: Dump DeckFolder hierarchy on DeckManager screen
            if (Log.LogNavigation && _activeContentController == "DeckManagerController")
            {
                Log.Nav(NavigatorId, $"=== DECK FOLDER HIERARCHY ===");
                var deckFolders = GameObject.FindObjectsOfType<Transform>()
                    .Where(t => t.name.Contains("DeckFolder_Base"))
                    .ToArray();
                Log.Nav(NavigatorId, $"Found {deckFolders.Length} DeckFolder_Base instances");
                foreach (var folder in deckFolders)
                {
                    if (folder.gameObject.activeInHierarchy)
                    {
                        Log.Nav(NavigatorId, $"DeckFolder: {folder.name} (active)");
                        LogHierarchy(folder, "  ", 5);
                    }
                }
                Log.Nav(NavigatorId, $"=== END DECK FOLDER HIERARCHY ===");
            }

            var addedObjects = new HashSet<GameObject>();
            var discoveredElements = new List<(GameObject obj, UIElementClassifier.ClassificationResult classification, float sortOrder)>();

            // Reset booster carousel state
            _boosterPackHitboxes.Clear();
            _isBoosterCarouselActive = _activeContentController == T.BoosterChamberController;
            if (!_isBoosterCarouselActive)
                _boosterCarouselIndex = 0;

            // Log panel filter state
            if (_foregroundPanel != null)
            {
                Log.Nav(NavigatorId, $"Filtering to panel: {_foregroundPanel.name}");
            }
            else if (_activeControllerGameObject != null)
            {
                Log.Nav(NavigatorId, $"Filtering to controller: {_activeContentController}");
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

                // Booster chamber: filter out pack-opening overlay buttons that can remain
                // active in the hierarchy after a pack is dismissed (stale RevealAll/Dismiss)
                if (_isBoosterCarouselActive &&
                    (obj.name.Contains("RevealAll") || obj.name.Contains("Dismiss_MainButton")))
                {
                    addedObjects.Add(obj);
                    return;
                }

                // Debug: log objectives and Blade_ListItem filtering
                bool isObjective = obj.name.Contains("Objective") || GetParentPath(obj).Contains("Objective");
                bool isBladeListItem = GetParentPath(obj).Contains("Blade_ListItem");

                if (!ShouldShowElement(obj))
                {
                    if (isObjective)
                        Log.Msg("{NavigatorId}", $"Objective filtered by ShouldShowElement: {obj.name}");
                    if (isBladeListItem)
                        Log.Msg("{NavigatorId}", $"Blade_ListItem filtered by ShouldShowElement: {obj.name}, path={GetParentPath(obj)}");
                    return;
                }

                var classification = UIElementClassifier.Classify(obj);
                if (classification.IsNavigable && classification.ShouldAnnounce)
                {
                    var pos = obj.transform.position;
                    float sortOrder = -pos.y * 1000 + pos.x;

                    // Jump In packet selection: child elements (e.g. MainButton) may have
                    // positions offset from their tile root, causing chaotic sort order.
                    // Use the parent JumpStartPacket tile's position for a consistent grid order.
                    if (_activeContentController == "PacketSelectContentController")
                    {
                        var packetRoot = EventAccessor.GetJumpStartPacketRoot(obj);
                        if (packetRoot != null)
                        {
                            var tilePos = packetRoot.transform.position;
                            sortOrder = -tilePos.y * 1000 + tilePos.x;
                        }
                    }

                    discoveredElements.Add((obj, classification, sortOrder));
                    addedObjects.Add(obj);
                    if (isBladeListItem)
                        Log.Msg("{NavigatorId}", $"Blade_ListItem ADDED: {obj.name}, label={classification.Label}");
                }
                else if (isObjective)
                {
                    Log.Msg("{NavigatorId}", $"Objective not navigable: {obj.name}, IsNavigable={classification.IsNavigable}, ShouldAnnounce={classification.ShouldAnnounce}");
                }
                else if (isBladeListItem)
                {
                    Log.Msg("{NavigatorId}", $"Blade_ListItem not navigable: {obj.name}, IsNavigable={classification.IsNavigable}, ShouldAnnounce={classification.ShouldAnnounce}");
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
                    Log.Msg("{NavigatorId}", $"Processing ObjectiveGraphics #{objGraphicsCount}: parent={buttonObj.transform.parent?.name}");
                }

                TryAddElement(buttonObj);
            }
            Log.Msg("{NavigatorId}", $"Processed {objCount} CustomButtons, {objGraphicsCount} ObjectiveGraphics");

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

            // Social panel: discover tile entries that may not have CustomButton components.
            // BlockTile has no CustomButton (only a Button), so it needs fallback discovery.
            // Also forces creation of blocked tiles (section is collapsed by default).
            if (_overlayDetector.GetActiveOverlay() == ElementGroup.FriendsPanel)
            {
                DiscoverSocialTileEntries(addedObjects, discoveredElements, GetParentPath);
            }

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

            // Commander/Companion cards: add their sub-elements to deckElementsToSkip
            // so the final loop doesn't add them with generic labels.
            // FindCommanderCards() (called after the loop) adds them back with proper labels.
            // Also remove from addedObjects so FindCommanderCards can re-add them.
            if (_activeContentController == T.WrapperDeckBuilder)
            {
                var commanderCards = DeckCardProvider.GetCommanderCards();
                foreach (var cmdCard in commanderCards)
                {
                    if (!cmdCard.IsValid) continue;
                    if (cmdCard.TileButton != null) { deckElementsToSkip.Add(cmdCard.TileButton); addedObjects.Remove(cmdCard.TileButton); }
                    if (cmdCard.TagButton != null) { deckElementsToSkip.Add(cmdCard.TagButton); addedObjects.Remove(cmdCard.TagButton); }
                    if (cmdCard.CardGameObject != null) { deckElementsToSkip.Add(cmdCard.CardGameObject); addedObjects.Remove(cmdCard.CardGameObject); }
                }
            }

            // Map main deck buttons to their edit buttons for alternate action
            var deckEditButtons = deckPairs
                .Where(p => p.Value.mainButton != null && p.Value.editButton != null)
                .ToDictionary(p => p.Value.mainButton, p => p.Value.editButton);

            // Recent tab: build mapping of tile elements to event titles and play buttons
            // So we can enrich deck labels and hide standalone play buttons
            var recentTilePlayButtons = new HashSet<GameObject>();
            var recentDeckEventTitles = new Dictionary<GameObject, string>();
            RecentPlayAccessor.FindContentView();
            if (RecentPlayAccessor.IsActive)
            {
                int tileCount = RecentPlayAccessor.GetTileCount();
                Log.Msg("{NavigatorId}", $"Recent tab active with {tileCount} tiles");
                foreach (var (obj, classification, _) in discoveredElements)
                {
                    int tileIdx = RecentPlayAccessor.FindTileIndexForElement(obj);
                    if (tileIdx < 0) continue;

                    // Find ALL non-deck buttons in this tile and mark them for skipping
                    foreach (var btn in RecentPlayAccessor.FindAllButtonsInTile(tileIdx))
                        recentTilePlayButtons.Add(btn);

                    // If this is a deck entry, map it to the event title
                    if (deckMainButtons.Contains(obj))
                    {
                        string eventTitle = RecentPlayAccessor.GetEventTitle(tileIdx);
                        if (!string.IsNullOrEmpty(eventTitle))
                            recentDeckEventTitles[obj] = eventTitle;
                    }
                }
                // Also hide any MainButton not inside a tile (the PlayBlade's own play button)
                // On Recent tab it's redundant — each tile has its own play button
                foreach (var (obj2, classification2, _) in discoveredElements)
                {
                    if (obj2.name == "MainButton" && RecentPlayAccessor.FindTileIndexForElement(obj2) < 0)
                    {
                        recentTilePlayButtons.Add(obj2);
                    }
                }
                Log.Msg("{NavigatorId}", $"Recent tab: {recentDeckEventTitles.Count} deck-event mappings, {recentTilePlayButtons.Count} play buttons to hide");

                // Reverse sort order for recently played decks (most recent first)
                if (recentDeckEventTitles.Count > 1)
                {
                    var pairs = new List<(int idx, float order)>();
                    for (int i = 0; i < discoveredElements.Count; i++)
                    {
                        if (recentDeckEventTitles.ContainsKey(discoveredElements[i].obj))
                            pairs.Add((i, discoveredElements[i].sortOrder));
                    }
                    // Sort by current order, then assign reversed orders
                    pairs.Sort((a, b) => a.order.CompareTo(b.order));
                    var reversed = pairs.Select(p => p.order).Reverse().ToList();
                    for (int j = 0; j < pairs.Count; j++)
                    {
                        var (o, c, _) = discoveredElements[pairs[j].idx];
                        discoveredElements[pairs[j].idx] = (o, c, reversed[j]);
                    }
                }
            }

            // Sort by position and add elements with proper labels
            Log.Msg("{NavigatorId}", $"Processing {discoveredElements.Count} discovered elements for final addition");
            foreach (var (obj, classification, _) in discoveredElements.OrderBy(x => x.sortOrder))
            {
                // Debug: trace Blade_ListItem elements through final addition
                bool isBladeListItem = obj.transform.parent?.name.Contains("Blade_ListItem") == true;
                if (isBladeListItem)
                    Log.Msg("{NavigatorId}", $"Final loop Blade_ListItem: {obj.name}, label={classification.Label}, obj null={obj == null}, active={obj?.activeInHierarchy}");

                // Skip deck elements that aren't the main UI button (TextBox, duplicates, etc.)
                if (deckElementsToSkip.Contains(obj))
                {
                    if (isBladeListItem)
                        Log.Msg("{NavigatorId}", $"Blade_ListItem SKIPPED by deckElementsToSkip!");
                    continue;
                }

                // Skip deck-specific toolbar buttons (they're available as attached actions on each deck)
                if (deckToolbarButtons.AllDeckSpecificButtons != null && deckToolbarButtons.AllDeckSpecificButtons.Contains(obj))
                    continue;

                // Recent tab: skip standalone play buttons (they're auto-pressed on deck Enter)
                if (recentTilePlayButtons.Contains(obj))
                    continue;

                string announcement = BuildAnnouncement(classification);

                // Friends panel: override profile button label with full username#number
                if (_profileButtonGO != null && obj == _profileButtonGO && _profileLabel != null)
                    announcement = _profileLabel;

                // Challenge context: prefix player name on challenge status buttons
                if (_challengeHelper.IsActive)
                    announcement = _challengeHelper.EnhanceButtonLabel(obj, announcement);

                // Recent tab: enrich deck labels with event/mode name
                if (recentDeckEventTitles.TryGetValue(obj, out var eventTitle))
                    announcement += " — " + eventTitle;

                // Build carousel info if this element supports arrow navigation (including sliders)
                CarouselInfo carouselInfo = classification.HasArrowNavigation
                    ? new CarouselInfo
                    {
                        HasArrowNavigation = true,
                        PreviousControl = classification.PreviousControl,
                        NextControl = classification.NextControl,
                        SliderComponent = classification.SliderComponent,
                        UseHoverActivation = classification.UseHoverActivation
                    }
                    : default;

                // Check if this is a deck button - attach actions for left/right cycling
                List<AttachedAction> attachedActions = null;
                if (deckMainButtons.Contains(obj))
                {
                    // Check if deck is selected and add to announcement
                    if (CardTileActivator.IsDeckSelected(obj))
                    {
                        announcement += $", {Models.Strings.DeckSelected}";
                    }

                    // Append short invalid status (Level 1)
                    string invalidStatus = CardTileActivator.GetDeckInvalidStatus(obj);
                    if (!string.IsNullOrEmpty(invalidStatus))
                    {
                        announcement += $", {invalidStatus}";
                    }

                    // Recent tab decks: skip attached actions (Enter auto-plays; deck toolbar not applicable here)
                    bool isRecentDeck = RecentPlayAccessor.IsActive;
                    if (!isRecentDeck)
                    {
                        // Get the rename button (TextBox) for this deck
                        GameObject renameButton = deckEditButtons.TryGetValue(obj, out var editBtn) ? editBtn : null;
                        attachedActions = BuildDeckAttachedActions(deckToolbarButtons, renameButton);

                        // Insert detailed tooltip as first virtual info item (Level 2)
                        string invalidTooltip = CardTileActivator.GetDeckInvalidTooltip(obj);
                        if (!string.IsNullOrEmpty(invalidTooltip))
                        {
                            attachedActions.Insert(0, new AttachedAction { Label = invalidTooltip, TargetButton = null });
                        }

                        if (attachedActions.Count > 0)
                        {
                            Log.Nav(NavigatorId, $"Deck '{announcement}' has {attachedActions.Count} attached actions");
                        }
                    }
                }

                if (isBladeListItem)
                    Log.Msg("{NavigatorId}", $"Blade_ListItem calling AddElement: announcement={announcement}");
                AddElement(obj, announcement, carouselInfo, null, attachedActions, classification.Role);
                if (isBladeListItem)
                    Log.Msg("{NavigatorId}", $"Blade_ListItem AddElement returned, _elements.Count={_elements.Count}");
            }

            // Find NPE reward cards (displayed cards on reward screens)
            // These are not buttons but should be navigable to read card info
            FindNPERewardCards(addedObjects);

            // Find deck builder collection cards (PoolHolder canvas)
            // Pool cards are always collection. Actual sideboard cards are in MetaCardHolders_Container,
            // detected separately by FindSideboardCards().
            FindPoolHolderCards(addedObjects);

            // Find commander/companion cards (CommanderSlotCardHolder)
            // Must be before FindDeckListCards so they get proper labels and are excluded from generic scan
            FindCommanderCards(addedObjects);

            // Find deck list cards (MainDeck_MetaCardHolder)
            // These are cards currently in your deck
            FindDeckListCards(addedObjects);

            // Find sideboard cards (non-MainDeck holders in MetaCardHolders_Container)
            // These are cards available to add to deck in draft/sealed
            FindSideboardCards(addedObjects);

            // ReadOnly deck builder: find cards in column view when list view is empty
            FindReadOnlyDeckCards(addedObjects);

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

            Log.Nav(NavigatorId, $"Discovered {_elements.Count} navigable elements");

            // Enrich color button labels before grouping (so grouped navigator gets enriched labels)
            if (_activeContentController == T.CampaignGraphContentController)
            {
                EnrichColorChallengeLabels();
            }

            // V2 faction event: append "selected" to the active faction tile so users can hear
            // which faction the selection currently sits on.
            if (_activeContentController == T.FactionalizedEventTemplate)
            {
                EnrichFactionEventLabels();
            }

            // Organize elements into groups for hierarchical navigation
            if (_groupedNavigationEnabled && _elements.Count > 0)
            {
                var elementsForGrouping = _elements.Select(e => (e.GameObject, e.Label, e.Role));
                _groupedNavigator.OrganizeIntoGroups(elementsForGrouping);

                // Queue type activation may have clicked a real tab — need another rescan
                if (_groupedNavigator.NeedsFollowUpRescan)
                {
                    Log.Msg("{NavigatorId}", $"Follow-up rescan needed after queue type activation");
                    TriggerRescan();
                    return;
                }

                // Update EventSystem selection to match the initial grouped element
                UpdateEventSystemSelectionForGroupedElement();
            }

            // On NPE rewards screen, auto-focus the unlocked card
            AutoFocusUnlockedCard();
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
        /// Build the announcement string based on classification
        /// </summary>
        protected virtual string BuildAnnouncement(UIElementClassifier.ClassificationResult classification)
        {
            return BuildLabel(classification.Label, classification.RoleLabel, classification.Role);
        }

        /// <summary>
        /// Override to intercept Enter on read-only deck cards.
        /// Shows a warning instead of trying to activate (which would do nothing useful).
        /// </summary>
        protected override void ActivateCurrentElement()
        {
            if (_isDeckBuilderReadOnly && IsValidIndex)
            {
                var element = _elements[_currentIndex].GameObject;
                if (element != null && CardDetector.IsCard(element))
                {
                    _announcer.AnnounceInterrupt(Strings.ReadOnlyDeckWarning);
                    return;
                }
            }

            // Packet elements need special click handling (CustomTouchButton on parent GO)
            if (IsInPacketSelectionContext())
            {
                var currentElement = _groupedNavigator.CurrentElement;
                if (currentElement != null && EventAccessor.ClickPacket(currentElement.Value.GameObject))
                {
                    _announcer.Announce(Models.Strings.ActivatedBare, AnnouncementPriority.Normal);
                    // Game rebuilds packet GOs asynchronously after click - schedule rescan
                    TriggerRescan();
                    return;
                }
            }

            base.ActivateCurrentElement();
        }

        protected string GetGameObjectPath(GameObject obj) => MenuDebugHelper.GetGameObjectPath(obj);

        public override string GetTutorialHint() =>
            _activeContentController == T.WrapperDeckBuilder
                ? LocaleManager.Instance.Get("DeckBuilderHint")
                : LocaleManager.Instance.Get("NavigateHint");

        protected override string GetActivationAnnouncement()
        {
            string menuName = GetMenuScreenName();
            if (_elements.Count == 0)
            {
                return $"{menuName}. {Models.Strings.NoNavigableItemsFound}";
            }

            // Use grouped navigator announcement when enabled
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                string groupAnnouncement = _groupedNavigator.GetActivationAnnouncement(menuName);
                if (_activeContentController == T.WrapperDeckBuilder)
                    return Models.Strings.WithHint(groupAnnouncement, "DeckBuilderHint");
                return groupAnnouncement;
            }

            Log.Msg("MenuNavigator", $"Screen '{menuName}': {Models.Strings.ItemCount(_elements.Count)}");
            return menuName;
        }

        #region Grouped Navigation Overrides

        /// <summary>
        /// Override MoveNext to use GroupedNavigator when grouped navigation is enabled.
        /// In deck builder with Tab, cycles between Collection, Filters, and Deck groups only.
        /// In DeckBuilderInfo group, Down arrow switches to next row with custom announcement.
        /// </summary>
        protected override void MoveNext()
        {
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                // DeckBuilderInfo 2D navigation: Down arrow switches to next row
                // Skip when Tab is pressed - let Tab cycling handle group switching
                if (IsDeckInfoSubNavActive() && !Input.GetKey(KeyCode.Tab))
                {
                    bool moved = _groupedNavigator.MoveNext();
                    if (moved)
                    {
                        _deckInfoEntryIndex = 0;
                        AnnounceDeckInfoEntry(includeRowName: true);
                    }
                    // If !moved, GroupedNavigator already announced "End of list"
                    return;
                }

                // Friend section navigation: Up/Down navigates between friends
                // Skip when Tab is pressed - let Tab handle group cycling
                if (IsFriendSectionActive() && !Input.GetKey(KeyCode.Tab))
                {
                    bool moved = _groupedNavigator.MoveNext();
                    if (moved)
                    {
                        RefreshFriendActions();
                        AnnounceFriendEntry();
                        AnnounceFirstFriendAction();
                    }
                    return;
                }

                // In deck builder with Tab key: cycle between main groups (Collection, Filters, Deck)
                // Only apply to Tab, not to arrow keys
                bool isTabPressed = Input.GetKey(KeyCode.Tab);
                if (_activeContentController == T.WrapperDeckBuilder && isTabPressed)
                {
                    if (_groupedNavigator.CycleToNextGroup(DeckBuilderCycleGroups))
                    {
                        // Initialize 2D sub-nav if we cycled into DeckBuilderInfo
                        var cycledGroup = _groupedNavigator.CurrentGroup;
                        if (cycledGroup.HasValue && cycledGroup.Value.Group == ElementGroup.DeckBuilderInfo)
                        {
                            InitializeDeckInfoSubNav();
                            if (!_suppressNavigationAnnouncement)
                                AnnounceDeckInfoEntry(includeRowName: true);
                        }
                        else
                        {
                            _deckInfoRows = null;
                            // Skip announcement if suppressed (Tab from search field - will announce after rescan)
                            if (!_suppressNavigationAnnouncement)
                            {
                                _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                            }
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
        /// In DeckBuilderInfo group, Up arrow switches to previous row with custom announcement.
        /// </summary>
        protected override void MovePrevious()
        {
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                // DeckBuilderInfo 2D navigation: Up arrow switches to previous row
                // Skip when Tab is pressed - let Tab cycling handle group switching
                if (IsDeckInfoSubNavActive() && !Input.GetKey(KeyCode.Tab))
                {
                    bool moved = _groupedNavigator.MovePrevious();
                    if (moved)
                    {
                        _deckInfoEntryIndex = 0;
                        AnnounceDeckInfoEntry(includeRowName: true);
                    }
                    // If !moved, GroupedNavigator already announced "Beginning of list"
                    return;
                }

                // Friend section navigation: Up/Down navigates between friends
                if (IsFriendSectionActive() && !Input.GetKey(KeyCode.Tab))
                {
                    bool moved = _groupedNavigator.MovePrevious();
                    if (moved)
                    {
                        RefreshFriendActions();
                        AnnounceFriendEntry();
                        AnnounceFirstFriendAction();
                    }
                    return;
                }

                // In deck builder with Tab key: cycle between main groups (Collection, Filters, Deck)
                // Only apply to Tab, not to arrow keys
                bool isTabPressed = Input.GetKey(KeyCode.Tab);
                if (_activeContentController == T.WrapperDeckBuilder && isTabPressed)
                {
                    if (_groupedNavigator.CycleToPreviousGroup(DeckBuilderCycleGroups))
                    {
                        // Initialize 2D sub-nav if we cycled into DeckBuilderInfo
                        var cycledGroup = _groupedNavigator.CurrentGroup;
                        if (cycledGroup.HasValue && cycledGroup.Value.Group == ElementGroup.DeckBuilderInfo)
                        {
                            InitializeDeckInfoSubNav();
                            if (!_suppressNavigationAnnouncement)
                                AnnounceDeckInfoEntry(includeRowName: true);
                        }
                        else
                        {
                            _deckInfoRows = null;
                            // Skip announcement if suppressed (Tab from search field - will announce after rescan)
                            if (!_suppressNavigationAnnouncement)
                            {
                                _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                            }
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
        /// In DeckBuilderInfo, Home jumps to first entry in current row.
        /// </summary>
        protected override void MoveFirst()
        {
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                if (IsDeckInfoSubNavActive())
                {
                    _deckInfoEntryIndex = 0;
                    AnnounceDeckInfoEntry(includeRowName: false);
                    return;
                }
                _groupedNavigator.MoveFirst();
                if (IsFriendSectionActive())
                {
                    RefreshFriendActions();
                    AnnounceFriendEntry();
                    AnnounceFirstFriendAction();
                }
                else
                {
                    _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                }
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

            // Check if it's a regular card (collection), deck list card, sideboard card, or commander card
            bool isCard = CardDetector.IsCard(gameObject);
            bool isDeckListCard = DeckCardProvider.IsDeckListCard(gameObject);
            bool isSideboardCard = DeckCardProvider.IsSideboardCard(gameObject);
            bool isCommanderCard = DeckCardProvider.GetCommanderCardInfo(gameObject) != null;

            if (isCard || isDeckListCard || isSideboardCard || isCommanderCard)
            {
                // Prepare card navigation for both collection cards and deck list cards
                cardNavigator.PrepareForCard(gameObject, ZoneType.Hand);
            }
            else if (EventAccessor.IsInsideJumpStartPacket(gameObject))
            {
                // Packet element: refresh Left/Right info blocks
                RefreshPacketBlocks(gameObject);
                if (cardNavigator.IsActive)
                    cardNavigator.Deactivate();
            }
            else
            {
                _packetBlocks = null;
                if (cardNavigator.IsActive)
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
                bool isDropdown = UIFocusTracker.IsDropdown(gameObject);

                // Set submit blocking flag BEFORE any EventSystem interaction.
                // EventSystemPatch checks this flag to block Unity's Submit events.
                // For dropdowns: prevents SendSubmitEventToSelectedObject from firing
                // before our Update opens the dropdown and sets ShouldBlockEnterFromGame.
                // On Login scene: block Enter for ALL elements except the RegistrationPanel
                // submit button, which needs the game's native path for ConnectToFrontDoor.
                bool isLoginScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == SceneNames.Login;
                bool isRegSubmit = isLoginScene && gameObject.name == "MainButton_Register" && IsInsideRegistrationPanel(gameObject);
                InputManager.AllowNativeEnterOnLogin = isRegSubmit;
                InputManager.BlockSubmitForToggle = isToggle || isDropdown || (isLoginScene && !isRegSubmit);

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
        /// <summary>
        /// Override MoveLast. In DeckBuilderInfo, End jumps to last entry in current row.
        /// </summary>
        protected override void MoveLast()
        {
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                if (IsDeckInfoSubNavActive())
                {
                    int rowIndex = _groupedNavigator.CurrentElementIndex;
                    if (rowIndex >= 0 && rowIndex < _deckInfoRows.Count)
                    {
                        _deckInfoEntryIndex = _deckInfoRows[rowIndex].entries.Count - 1;
                        AnnounceDeckInfoEntry(includeRowName: false);
                    }
                    return;
                }
                _groupedNavigator.MoveLast();
                if (IsFriendSectionActive())
                {
                    RefreshFriendActions();
                    AnnounceFriendEntry();
                    AnnounceFirstFriendAction();
                }
                else
                {
                    _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                }
                UpdateEventSystemSelectionForGroupedElement();
                UpdateCardNavigationForGroupedElement();
                return;
            }
            base.MoveLast();
        }

        /// <summary>
        /// Override letter navigation to work with grouped navigation.
        /// At GroupList level: search group display names.
        /// At InsideGroup level: search element labels within current group.
        /// When grouped navigation is inactive: fall through to base.
        /// </summary>
        protected override bool HandleLetterNavigation(KeyCode key)
        {
            if (!_groupedNavigationEnabled || !_groupedNavigator.IsActive)
                return base.HandleLetterNavigation(key);

            char letter = (char)('A' + (key - KeyCode.A));

            if (_groupedNavigator.Level == NavigationLevel.GroupList)
            {
                var labels = _groupedNavigator.GetGroupDisplayNames();
                int target = _letterSearch.HandleKey(letter, labels, _groupedNavigator.CurrentGroupIndex);
                if (target >= 0 && target != _groupedNavigator.CurrentGroupIndex)
                {
                    _groupedNavigator.JumpToGroupByIndex(target);
                    _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                    UpdateEventSystemSelectionForGroupedElement();
                    UpdateCardNavigationForGroupedElement();
                }
                else if (target == _groupedNavigator.CurrentGroupIndex)
                {
                    _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                }
                else
                {
                    _announcer.AnnounceInterrupt(Strings.LetterSearchNoMatch(_letterSearch.Buffer));
                }
                return true;
            }
            else // InsideGroup
            {
                var labels = _groupedNavigator.GetCurrentGroupElementLabels();
                int target = _letterSearch.HandleKey(letter, labels, _groupedNavigator.CurrentElementIndex);
                if (target >= 0 && target != _groupedNavigator.CurrentElementIndex)
                {
                    _groupedNavigator.JumpToElementByIndex(target);
                    _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                    UpdateEventSystemSelectionForGroupedElement();
                    UpdateCardNavigationForGroupedElement();
                }
                else if (target == _groupedNavigator.CurrentElementIndex)
                {
                    _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                }
                else
                {
                    _announcer.AnnounceInterrupt(Strings.LetterSearchNoMatch(_letterSearch.Buffer));
                }
                return true;
            }
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
                    // Virtual standalone element (no GameObject) - re-announce on Enter
                    var standaloneObj = _groupedNavigator.GetStandaloneElement();
                    if (standaloneObj == null)
                    {
                        _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                        return true;
                    }

                    // Real standalone element - activate directly without entering group
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
                    _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                    UpdateCardNavigationForGroupedElement();
                    return true;
                }

                // Normal group - enter it
                // Special handling for DeckBuilderDeckList: refresh deck cards immediately while UI is active
                if (currentGroup.HasValue && currentGroup.Value.Group == ElementGroup.DeckBuilderDeckList)
                {
                    DeckCardProvider.ClearDeckListCache();
                    DeckCardProvider.ClearReadOnlyDeckCache();
                    // Force immediate refresh while UI is in active state
                    var deckCards = DeckCardProvider.GetDeckListCards();
                    if (deckCards.Count == 0 && _isDeckBuilderReadOnly)
                    {
                        var roCards = DeckCardProvider.GetReadOnlyDeckCards();
                        Log.Msg("{NavigatorId}", $"Entering DeckBuilderDeckList (read-only) - refreshed {roCards.Count} deck cards");
                    }
                    else
                    {
                        Log.Msg("{NavigatorId}", $"Entering DeckBuilderDeckList - refreshed {deckCards.Count} deck cards");
                    }
                }

                if (_groupedNavigator.EnterGroup())
                {
                    // DeckBuilderInfo: initialize 2D sub-navigation and announce first entry
                    var enteredGroup = _groupedNavigator.CurrentGroup;
                    if (enteredGroup.HasValue && enteredGroup.Value.Group == ElementGroup.DeckBuilderInfo)
                    {
                        InitializeDeckInfoSubNav();
                        AnnounceDeckInfoEntry(includeRowName: true);
                    }
                    else if (enteredGroup.HasValue && enteredGroup.Value.Group.IsFriendSectionGroup())
                    {
                        // Friend section: initialize actions and announce first friend + default action
                        RefreshFriendActions();
                        AnnounceFriendEntry();
                        AnnounceFirstFriendAction();
                    }
                    else
                    {
                        _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                    }
                    UpdateCardNavigationForGroupedElement();
                    return true;
                }
            }
            else
            {
                // Inside a group - check for subgroup entry first
                if (_groupedNavigator.IsCurrentElementSubgroupEntry())
                {
                    var currentElem = _groupedNavigator.CurrentElement;

                    // PlayBlade queue type subgroup: content loaded dynamically via tab activation
                    // Don't use EnterSubgroup() — handle via PlayBlade navigation instead
                    if (currentElem.HasValue
                        && currentElem.Value.SubgroupType == ElementGroup.PlayBladeContent
                        && _groupedNavigator.CurrentGroup?.Group == ElementGroup.PlayBladeTabs)
                    {
                        var result = _playBladeHelper.HandleQueueTypeEntry(currentElem.Value);
                        if (result == PlayBladeResult.RescanNeeded)
                            TriggerRescan();
                        return true;
                    }

                    // Standard subgroup (Objectives) - enter pre-stored elements
                    if (_groupedNavigator.EnterSubgroup())
                    {
                        _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                        return true;
                    }
                }

                // Friend section: Enter activates the currently selected action
                if (IsFriendSectionActive() && _friendActions != null && _friendActions.Count > 0
                    && _friendActionIndex >= 0 && _friendActionIndex < _friendActions.Count)
                {
                    var friendElem = _groupedNavigator.CurrentElement;
                    if (friendElem.HasValue && friendElem.Value.GameObject != null)
                    {
                        var (actionLabel, actionId) = _friendActions[_friendActionIndex];
                        if (FriendInfoProvider.ActivateFriendAction(friendElem.Value.GameObject, actionId))
                        {
                            _announcer.Announce(Strings.Activated(actionLabel), AnnouncementPriority.High);
                            TriggerRescan();
                        }
                        return true;
                    }
                }

                // Activate the current element
                var currentElement = _groupedNavigator.CurrentElement;

                // Virtual elements (no GameObject) - re-announce on Enter
                var currentGroupInfo = _groupedNavigator.CurrentGroup;
                if (currentElement.HasValue && currentElement.Value.GameObject == null
                    && currentGroupInfo.HasValue)
                {
                    if (currentGroupInfo.Value.Group == ElementGroup.DeckBuilderInfo)
                    {
                        RefreshDeckInfoSubNav();
                        AnnounceDeckInfoEntry(includeRowName: true);
                        return true;
                    }

                    // ChallengeMain virtual elements: forward activation to the active challenge button.
                    // The game swaps MainButton/SecondaryButton visibility on state changes
                    // without triggering a rescan, so the active button may not be in _elements.
                    // Find it directly in the scene and activate via UIActivator.
                    if (currentGroupInfo.Value.Group == ElementGroup.ChallengeMain
                        && _challengeHelper.IsActive)
                    {
                        var activeButton = ChallengeNavigationHelper.FindActiveChallengeButton();
                        if (activeButton != null)
                        {
                            var result = UIActivator.Activate(activeButton);
                            if (result.Success)
                                _announcer.Announce(result.Message, Models.AnnouncementPriority.High);
                            return true;
                        }
                    }

                    // Generic virtual element - re-announce
                    _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                    return true;
                }

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
                                Log.Msg("{NavigatorId}", $"Folder toggle was OFF after Enter, re-toggling to expand");
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
                var currentGroup = _groupedNavigator.CurrentGroup;
                bool wasFolderGroup = currentGroup.HasValue && currentGroup.Value.IsFolderGroup;
                bool wasPlayBladeContext = _groupedNavigator.IsPlayBladeContext;
                string folderName = wasFolderGroup ? currentGroup.Value.DisplayName : null;

                if (_groupedNavigator.ExitGroup())
                {
                    // Clear 2D sub-nav state when exiting DeckBuilderInfo
                    _deckInfoRows = null;
                    // Clear friend action state when exiting friend section
                    _friendActions = null;
                    _friendActionIndex = 0;

                    if (wasFolderGroup)
                    {
                        // In challenge context, go back to ChallengeMain after exiting a folder
                        if (_challengeHelper.IsActive)
                        {
                            _groupedNavigator.RequestChallengeMainEntry();
                            Log.Nav(NavigatorId, $"Challenge folder exit - requesting ChallengeMain entry");
                            _suppressRescanAnnouncement = true;
                            TriggerRescan();
                        }
                        // In PlayBlade context, go back to folders list after exiting a folder
                        else if (wasPlayBladeContext)
                        {
                            _groupedNavigator.RequestFoldersEntry(folderName);
                            Log.Nav(NavigatorId, $"PlayBlade folder exit - requesting folders list entry (restore to: {folderName})");
                            _suppressRescanAnnouncement = true;
                            TriggerRescan();
                        }
                        // DeckManager folders: no rescan needed, just exit to group level
                    }

                    _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
                    UpdateCardNavigationForGroupedElement(); // Deactivate card navigator when exiting group
                    return true;
                }
            }

            // At group level or exit failed - fall through to normal back navigation
            return false;
        }

        #endregion

        /// <summary>
        /// Shift+Enter on a focused deck-builder card opens the card-viewer popup
        /// (style picker / craft preview), mirroring the sighted right-click flow.
        /// Falls through to default behavior elsewhere (deck-row Rename via AlternateActionObject, etc.).
        /// </summary>
        protected override void ActivateAlternateAction()
        {
            if (_activeContentController == T.WrapperDeckBuilder
                && TryOpenCardViewerForFocusedCard())
            {
                return;
            }
            base.ActivateAlternateAction();
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

            // Handle Challenge/PlayBlade activations BEFORE UIActivator.Activate
            // Helper sets up pending entry flags before blade Hide/Show events can interfere
            var elementGroup = _groupAssigner.DetermineGroup(element);

            // Challenge helper handles ChallengeMain elements (Select Deck button)
            var challengeResult = _challengeHelper.IsActive
                ? _challengeHelper.HandleEnter(element, elementGroup)
                : PlayBladeResult.NotHandled;

            // Recent tab decks: skip PlayBlade helper (it would request folders entry, but
            // Recent tab has no folders — we handle auto-play directly below)
            bool isRecentTabDeck = elementGroup == ElementGroup.PlayBladeContent
                && RecentPlayAccessor.IsActive && CardTileActivator.IsDeckEntry(element);
            var playBladeResult = (isRecentTabDeck || _challengeHelper.IsActive)
                ? PlayBladeResult.NotHandled
                : _playBladeHelper.HandleEnter(element, elementGroup);

            // Track Bot-Match mode selection for JoinMatchMaking patch
            if (elementGroup == ElementGroup.PlayBladeContent && !CardTileActivator.IsDeckEntry(element))
            {
                var text = UITextExtractor.GetText(element);
                PlayBladeNavigationHelper.SetBotMatchMode(
                    text != null && text.IndexOf("Bot", System.StringComparison.OrdinalIgnoreCase) >= 0);
            }
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
            // Note: deck card count is already captured by OnDeckBuilderCardCountCapture()
            // which runs at the top of ActivateCurrentElement() before any click path.
            var result = UIActivator.Activate(element);
            // Don't announce "Deck Selected" when challenge helper is opening the deck selector
            // (challengeResult == RescanNeeded means we're opening the picker, not selecting a deck)
            if (challengeResult != PlayBladeResult.RescanNeeded)
                _announcer.Announce(result.Message, Models.AnnouncementPriority.Normal);

            // Color Challenge: activating a color button changes the track, so rescan
            // to refresh info blocks and deck name
            if (_activeContentController == T.CampaignGraphContentController)
            {
                TriggerRescan();
            }

            // V2 Sealed/Faction: clicking a faction tile updates SelectedFaction; rescan so
            // the tile labels announce the new selection state.
            if (_activeContentController == T.FactionalizedEventTemplate &&
                EventAccessor.GetFactionInternalNameForElement(element) != null)
            {
                TriggerRescan();
            }

            // Brawl deck builder: activating the commander empty slot toggles the Commanders
            // filter, which changes the collection card pool. Rescan so the collection group
            // picks up the newly filtered cards instead of showing stale entries.
            if (element.name == "CustomButton - EmptySlot" && CardTileActivator.IsInCommanderContainer(element))
            {
                TriggerRescan();
            }

            // Note: Mailbox mail item selection is detected via Harmony patch on OnLetterSelected
            // which announces the mail content directly with actual letter data

            // Challenge deck selection: activate deck, return to ChallengeMain (no auto-play)
            // Skip if HandleEnter already handled this (e.g., deck display in ContextDisplay
            // which opens the deck selector rather than selecting a deck)
            if (challengeResult == PlayBladeResult.NotHandled &&
                _challengeHelper.IsActive && CardTileActivator.IsDeckEntry(element))
            {
                Log.Msg("{NavigatorId}", $"Challenge deck selected - returning to ChallengeMain");
                _challengeHelper.HandleDeckSelected();
                TriggerRescan();
                return true;
            }

            // Auto-play: When a deck is selected in PlayBlade, automatically press the Play button
            // For recent tab decks, the element is destroyed during Activate (blade switches from
            // LastPlayed to FindMatch), so IsDeckEntry(element) would fail. Use isRecentTabDeck
            // (captured before Activate) as an alternate entry condition.
            if (!_challengeHelper.IsActive &&
                (isRecentTabDeck || (_playBladeHelper.IsActive && CardTileActivator.IsDeckEntry(element))))
            {
                AutoPressPlayButtonInPlayBlade();
            }

            // Deck list card activated (removing card from deck) - trigger rescan to update both lists
            if (elementGroup == ElementGroup.DeckBuilderDeckList)
            {
                Log.Nav(NavigatorId, $"Deck list card activated - scheduling rescan to update lists");
                // _deckCountBeforeActivation already captured before UIActivator.Activate above
                _announceDeckCountOnRescan = true;
                TriggerRescan();
                AccessibleArenaMod.Instance?.CardNavigator?.InvalidateBlocks();
                return true;
            }

            // Challenge or PlayBlade activation needs rescan
            if (challengeResult == PlayBladeResult.RescanNeeded || playBladeResult == PlayBladeResult.RescanNeeded)
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
                    Log.Nav(NavigatorId, $"Toggle activated - forcing rescan in {RescanDelaySeconds}s (bypassing debounce)");
                    TriggerRescan();
                }
                else
                {
                    Log.Nav(NavigatorId, $"Toggle activated on Login panel - skipping rescan");
                }
                return true;
            }

            // For input field activations, trigger a rescan after a short delay
            // The UI might change (e.g., Send button appearing) when entering an input field
            if (isInputField)
            {
                Log.Nav(NavigatorId, $"Input field activated - scheduling rescan");
                TriggerRescan();
                return true;
            }

            // Dropdowns: activate and register with DropdownStateManager so we can find it when closing
            // (needed when focus goes to Blocker element instead of dropdown items)
            if (isDropdown)
            {
                Log.Nav(NavigatorId, $"Dropdown activated ({element.name})");
                UIFocusTracker.EnterDropdownEditMode(element);
                return true;
            }

            // For popup/dialog button activations (OK, Cancel), the popup closes with animation
            // Popup button click - unified detector will handle the visibility change
            // No cooldown needed - alpha state comparison will detect when popup closes
            if (isPopupButton)
            {
                Log.Nav(NavigatorId, $"Popup button activated ({element.name})");
                // Unified detector will detect popup close via alpha change
                return true;
            }

            // Note: Rewards popup ClaimButton handling moved to RewardPopupNavigator

            // Packet selection: confirm button or any activation rebuilds GOs asynchronously
            if (_activeContentController == "PacketSelectContentController")
            {
                Log.Nav(NavigatorId, $"Packet selection activation - scheduling rescan");
                TriggerRescan();
            }

            return true;
        }

        #region Friend Section Sub-Navigation

        #endregion

        #region Event Page Info Virtual Group

        /// <summary>
        /// Appends the challenge status text as a virtual element at the end of ChallengeMain.
        /// Shows contextual guidance like "Waiting for opponent" or "Select a deck".
        /// </summary>
        private void InjectChallengeStatusElement()
        {
            if (_challengeHelper == null || !_challengeHelper.IsActive)
                return;

            // Find main button index before appending virtual elements
            int mainButtonIdx = -1;
            var group = _groupedNavigator.GetGroupByType(ElementGrouping.ElementGroup.ChallengeMain);
            if (group.HasValue)
            {
                for (int i = 0; i < group.Value.Count; i++)
                {
                    var go = group.Value.Elements[i].GameObject;
                    if (go != null && (go.name == "UnifiedChallenge_MainButton" ||
                        go.name == "UnifiedChallenge_SecondaryButton"))
                    {
                        mainButtonIdx = i;
                        break;
                    }
                }
            }

            // Append opponent status virtual element
            string opponentLabel = _challengeHelper.GetOpponentStatusLabel();
            int opponentIdx = _groupedNavigator.GetGroupElementCount(
                ElementGrouping.ElementGroup.ChallengeMain);
            _groupedNavigator.AppendElementToGroup(
                ElementGrouping.ElementGroup.ChallengeMain,
                opponentLabel
            );

            // Append challenge status text virtual element
            int statusIdx = -1;
            string statusText = _challengeHelper.GetChallengeStatusText();
            if (!string.IsNullOrEmpty(statusText))
            {
                statusIdx = _groupedNavigator.GetGroupElementCount(
                    ElementGrouping.ElementGroup.ChallengeMain);
                _groupedNavigator.AppendElementToGroup(
                    ElementGrouping.ElementGroup.ChallengeMain,
                    statusText
                );
            }

            _challengeHelper.SetElementIndices(opponentIdx, mainButtonIdx, statusIdx);
        }

        /// Injects event info blocks as standalone virtual elements into the grouped navigator.
        /// Each block becomes its own standalone group, directly navigable with Up/Down
        /// without needing to enter a subgroup.
        /// </summary>
        private void InjectEventInfoGroup()
        {
            if (!_groupedNavigationEnabled || !_groupedNavigator.IsActive)
                return;

            var blocks = EventAccessor.GetEventPageInfoBlocks();
            if (blocks == null || blocks.Count == 0)
            {
                Log.Msg("{NavigatorId}", $"EventAccessor returned no info blocks");
                return;
            }
            Log.Msg("{NavigatorId}", $"Injecting {blocks.Count} event info elements");

            // Insert each block as a standalone virtual element after the previous one.
            // First block appends at end, subsequent blocks insert after the last EventInfo.
            ElementGroup? insertAfter = null;
            foreach (var block in blocks)
            {
                string label = string.IsNullOrEmpty(block.Label)
                    ? block.Content
                    : $"{block.Label}: {block.Content}";

                var elements = new List<ElementGrouping.GroupedElement>
                {
                    new ElementGrouping.GroupedElement
                    {
                        GameObject = null,
                        Label = label,
                        Group = ElementGrouping.ElementGroup.EventInfo
                    }
                };

                _groupedNavigator.AddVirtualGroup(
                    ElementGrouping.ElementGroup.EventInfo,
                    elements,
                    insertAfter: insertAfter,
                    isStandalone: true,
                    displayName: label
                );

                // Subsequent blocks insert after the last EventInfo
                insertAfter = ElementGrouping.ElementGroup.EventInfo;
            }
        }

        /// <summary>
        /// Enrich color button labels with track progress from the Color Challenge strategy.
        /// Runs after element discovery — matches already-extracted element labels against
        /// localized color names and appends progress summaries.
        /// </summary>
        private void EnrichColorChallengeLabels()
        {
            var trackSummaries = EventAccessor.GetAllTrackSummaries();
            if (trackSummaries == null || trackSummaries.Count == 0)
            {
                Log.Nav(NavigatorId, $"No Color Challenge track summaries available");
                return;
            }

            int enriched = 0;
            for (int i = 0; i < _elements.Count; i++)
            {
                var elem = _elements[i];
                if (string.IsNullOrEmpty(elem.Label)) continue;

                // Try exact match first, then try the label prefix before the role suffix
                // (e.g. "Weiß, Schalter" → try "Weiß" when TutorialMessages appends role labels)
                string matchKey = elem.Label;
                if (!trackSummaries.ContainsKey(matchKey))
                {
                    int commaIdx = elem.Label.IndexOf(',');
                    matchKey = commaIdx > 0 ? elem.Label.Substring(0, commaIdx).Trim() : null;
                }

                if (matchKey != null && trackSummaries.TryGetValue(matchKey, out string summary))
                {
                    elem.Label = $"{elem.Label}, {summary}";
                    _elements[i] = elem;
                    enriched++;
                }
            }

            Log.Msg("{NavigatorId}", $"EnrichColorChallengeLabels: enriched {enriched} of {_elements.Count} elements");
        }

        /// <summary>
        /// Append ", selected" to the faction-tile button whose internal faction name matches the
        /// currently selected faction on a V2 Sealed/Faction event page. The game stores the
        /// selection in <c>FactionEventContext.SelectedFaction</c>; without this enrichment the
        /// tiles all sound the same when navigated.
        /// </summary>
        private void EnrichFactionEventLabels()
        {
            string selected = EventAccessor.GetSelectedFactionInternalName();
            if (string.IsNullOrEmpty(selected)) return;

            int enriched = 0;
            for (int i = 0; i < _elements.Count; i++)
            {
                var elem = _elements[i];
                string factionName = EventAccessor.GetFactionInternalNameForElement(elem.GameObject);
                if (factionName != selected) continue;
                elem.Label = string.IsNullOrEmpty(elem.Label)
                    ? Strings.Selected
                    : $"{elem.Label}, {Strings.Selected}";
                _elements[i] = elem;
                enriched++;
            }
            Log.Msg("{NavigatorId}", $"EnrichFactionEventLabels: marked {enriched} tile(s) as selected ({selected})");
        }

        #endregion

        // Note: IsSettingsSubmenuButton() removed - handled by SettingsMenuNavigator

        /// <summary>
        /// Auto-expand the play blade when it's in collapsed state.
        /// Used for panels like Color Challenge where the blade starts collapsed.
        /// </summary>
        private void AutoExpandBlade()
        {
            Log.Nav(NavigatorId, $"Attempting blade auto-expand");

            // Find the blade expand button (Btn_BladeIsClosed or its arrow child)
            var bladeButton = GetActiveCustomButtons()
                .FirstOrDefault(obj => obj.name.Contains("BladeHoverClosed") || obj.name.Contains("Btn_BladeIsClosed"));

            if (bladeButton != null)
            {
                Log.Nav(NavigatorId, $"Auto-expanding blade via {bladeButton.name}");
                _announcer.Announce(Models.Strings.OpeningColorChallenges, Models.AnnouncementPriority.High);
                UIActivator.Activate(bladeButton);

                // Schedule a rescan after the blade opens
                TriggerRescan();
            }
            else
            {
                Log.Nav(NavigatorId, $"Could not find blade expand button");
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
        /// True for either the classic <c>EventPageContentController</c> or the V2
        /// <c>FactionalizedEventTemplate</c> (Sealed/Faction events).
        /// </summary>
        private static bool IsEventPageController(string controllerName) =>
            controllerName == T.EventPageContentController || controllerName == T.FactionalizedEventTemplate;

        /// <summary>
        /// Get a human-readable name for the current PlayBlade state.
        /// </summary>
        private string GetPlayBladeStateName()
        {
            if (PanelStateManager.Instance == null || !PanelStateManager.Instance.IsPlayBladeActive)
                return null;

            // PlayBlade states: 0=Hidden, 1=Events, 2=DirectChallenge, 3=FriendChallenge
            var state = PanelStateManager.Instance.PlayBladeState;
            string baseName = state switch
            {
                1 => Strings.ScreenPlayModeSelection,
                2 => Strings.ScreenDirectChallenge,
                3 => Strings.ScreenFriendChallenge,
                _ => null
            };

            return baseName;
        }

        /// <summary>
        /// Map content controller type name to user-friendly screen name.
        /// </summary>
        private string GetContentControllerDisplayName(string controllerTypeName) =>
            _screenDetector.GetContentControllerDisplayName(controllerTypeName);

        #endregion
    }
}
