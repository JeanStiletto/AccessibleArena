using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for browser UIs in the duel scene.
    /// Orchestrates browser detection and navigation:
    /// - Uses BrowserDetector for finding active browsers
    /// - Delegates zone-based navigation (Scry/London) to BrowserZoneNavigator
    /// - Handles generic browsers (YesNo, Dungeon, etc.) directly
    /// </summary>
    public class BrowserNavigator
    {
        private readonly IAnnouncementService _announcer;
        private readonly BrowserZoneNavigator _zoneNavigator;

        // Browser state
        private bool _isActive;
        private bool _hasAnnouncedEntry;
        private BrowserInfo _browserInfo;

        // Generic browser navigation (non-zone browsers)
        private List<GameObject> _browserCards = new List<GameObject>();
        private List<GameObject> _browserButtons = new List<GameObject>();
        private int _currentCardIndex = -1;
        private int _currentButtonIndex = -1;

        // ViewDismiss auto-dismiss tracking
        private bool _viewDismissDismissed;

        // AssignDamage browser state
        private bool _isAssignDamage;
        private object _assignDamageBrowserRef;       // AssignDamageBrowser instance
        private System.Collections.IDictionary _spinnerMap; // InstanceId → SpinnerAnimated
        private uint _totalDamage;
        private bool _totalDamageCached;
        private int _assignerIndex;   // 1-based index of current assigner
        private int _assignerTotal;   // total number of damage assigners in this combat
        private static readonly BindingFlags ReflFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        // Multi-zone browser state (SelectCardsMultiZone)
        private bool _isMultiZone;
        private List<GameObject> _zoneButtons = new List<GameObject>();
        private int _currentZoneButtonIndex = -1;
        private bool _onZoneSelector; // true when focus is on the zone selector element

        // Zone name constant
        private const string ZoneLocalHand = "LocalHand";

        /// <summary>
        /// Enters zone selector mode and deactivates CardInfoNavigator
        /// so it doesn't intercept arrow keys meant for zone cycling.
        /// </summary>
        private void EnterZoneSelector()
        {
            _onZoneSelector = true;
            AccessibleArenaMod.Instance?.CardNavigator?.Deactivate();
        }

        public BrowserNavigator(IAnnouncementService announcer)
        {
            _announcer = announcer;
            _zoneNavigator = new BrowserZoneNavigator(announcer);
        }

        #region Public Properties

        public bool IsActive => _isActive;
        public string ActiveBrowserType => _browserInfo?.BrowserType;
        public BrowserZoneNavigator ZoneNavigator => _zoneNavigator;

        #endregion

        #region Lifecycle

        /// <summary>
        /// Resets mulligan tracking state. Call when entering a new duel.
        /// </summary>
        public void ResetMulliganState()
        {
            _zoneNavigator.ResetMulliganState();
        }

        /// <summary>
        /// Updates browser detection state. Call each frame from DuelNavigator.
        /// </summary>
        public void Update()
        {
            var browserInfo = BrowserDetector.FindActiveBrowser();

            if (browserInfo.IsActive)
            {
                // Auto-dismiss ViewDismiss card preview popups immediately.
                // These open when clicking graveyard/exile cards but serve no purpose
                // for accessibility. Dismiss to prevent focus from getting trapped.
                if (browserInfo.BrowserType == BrowserDetector.BrowserTypeViewDismiss)
                {
                    if (!_viewDismissDismissed)
                    {
                        _viewDismissDismissed = true;
                        AutoDismissViewDismiss(browserInfo);
                    }
                    return; // Don't enter browser mode for ViewDismiss
                }

                if (!_isActive)
                {
                    EnterBrowserMode(browserInfo);
                }
                // Re-enter if browser type changed (e.g., OpeningHand -> Mulligan)
                else if (browserInfo.BrowserType != _browserInfo?.BrowserType)
                {
                    MelonLogger.Msg($"[BrowserNavigator] Browser type changed: {_browserInfo?.BrowserType} -> {browserInfo.BrowserType}");
                    ExitBrowserMode();
                    EnterBrowserMode(browserInfo);
                }
            }
            else
            {
                _viewDismissDismissed = false; // Reset when no browser is active
                if (_isActive)
                {
                    ExitBrowserMode();
                }
            }
        }

        /// <summary>
        /// Enters browser mode.
        /// </summary>
        private void EnterBrowserMode(BrowserInfo browserInfo)
        {
            _isActive = true;
            _browserInfo = browserInfo;
            _hasAnnouncedEntry = false;
            _currentCardIndex = -1;
            _currentButtonIndex = -1;
            _browserCards.Clear();
            _browserButtons.Clear();

            MelonLogger.Msg($"[BrowserNavigator] Entering browser: {browserInfo.BrowserType}");

            // Activate zone navigator for zone-based browsers
            if (browserInfo.IsZoneBased)
            {
                _zoneNavigator.Activate(browserInfo);
            }

            // Detect multi-zone browser
            _isMultiZone = browserInfo.BrowserType == "SelectCardsMultiZone";

            // Detect AssignDamage browser
            if (browserInfo.BrowserType == "AssignDamage")
            {
                _isAssignDamage = true;
                CacheAssignDamageState();
            }

            // Discover elements
            DiscoverBrowserElements();
        }

        /// <summary>
        /// Exits browser mode.
        /// </summary>
        private void ExitBrowserMode()
        {
            MelonLogger.Msg($"[BrowserNavigator] Exiting browser: {_browserInfo?.BrowserType}");

            // Deactivate zone navigator
            if (_browserInfo?.IsZoneBased == true)
            {
                _zoneNavigator.Deactivate();
            }

            _isActive = false;
            _browserInfo = null;
            _hasAnnouncedEntry = false;
            _browserCards.Clear();
            _browserButtons.Clear();
            _currentCardIndex = -1;
            _currentButtonIndex = -1;

            // Clear multi-zone state
            _isMultiZone = false;
            _zoneButtons.Clear();
            _currentZoneButtonIndex = -1;
            _onZoneSelector = false;

            // Clear AssignDamage state
            _isAssignDamage = false;
            _assignDamageBrowserRef = null;
            _spinnerMap = null;
            _totalDamage = 0;
            _totalDamageCached = false;
            _assignerIndex = 0;
            _assignerTotal = 0;

            // Invalidate detector cache
            BrowserDetector.InvalidateCache();

            // Notify DuelAnnouncer
            DuelAnnouncer.Instance?.OnLibraryBrowserClosed();
        }

        /// <summary>
        /// Auto-dismisses a ViewDismiss card preview popup by clicking its dismiss button.
        /// </summary>
        private void AutoDismissViewDismiss(BrowserInfo browserInfo)
        {
            MelonLogger.Msg($"[BrowserNavigator] Auto-dismissing ViewDismiss card preview");

            if (browserInfo.BrowserGameObject == null) return;

            // Find and click the dismiss/done/close button within the scaffold.
            // Must use UIActivator.Activate() (not SimulatePointerClick) because
            // MTGA buttons use CustomButton which needs _onClick reflection invocation.
            foreach (Transform child in browserInfo.BrowserGameObject.GetComponentsInChildren<Transform>(true))
            {
                if (!child.gameObject.activeInHierarchy) continue;
                string name = child.name;
                if (name.Contains("Dismiss") || name.Contains("Done") || name.Contains("Close"))
                {
                    if (BrowserDetector.HasClickableComponent(child.gameObject))
                    {
                        var result = UIActivator.Activate(child.gameObject);
                        MelonLogger.Msg($"[BrowserNavigator] Activated dismiss button '{name}': {result.Success} ({result.Type})");
                        BrowserDetector.InvalidateCache();
                        return;
                    }
                }
            }

            MelonLogger.Msg($"[BrowserNavigator] No dismiss button found in ViewDismiss scaffold");
        }

        #endregion

        #region Input Handling

        /// <summary>
        /// Handles input during browser mode.
        /// Returns true if input was consumed.
        /// </summary>
        public bool HandleInput()
        {
            if (!_isActive) return false;

            // Announce browser state on first interaction
            if (!_hasAnnouncedEntry)
            {
                AnnounceBrowserState();
                _hasAnnouncedEntry = true;
            }

            // AssignDamage browser: Up/Down controls spinner, Left/Right navigates blockers
            if (_isAssignDamage)
            {
                if (HandleAssignDamageInput())
                    return true;
            }

            // Zone-based browsers: delegate C/D/arrows/Enter to zone navigator
            if (_browserInfo.IsZoneBased)
            {
                if (_zoneNavigator.HandleInput())
                {
                    return true;
                }
            }

            // Multi-zone browser: zone selector handles Up/Down and blocks other input
            if (_isMultiZone && _onZoneSelector && _zoneButtons.Count > 0)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    CycleMultiZone(next: false);
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
                {
                    CycleMultiZone(next: true);
                    return true;
                }
                // Tab from zone selector → first card (or first button if no cards)
                if (Input.GetKeyDown(KeyCode.Tab))
                {
                    bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    if (!shift)
                    {
                        _onZoneSelector = false;
                        if (_browserCards.Count > 0)
                        {
                            _currentCardIndex = 0;
                            _currentButtonIndex = -1;
                            AnnounceCurrentCard();
                        }
                        else if (_browserButtons.Count > 0)
                        {
                            _currentButtonIndex = 0;
                            AnnounceCurrentButton();
                        }
                    }
                    else
                    {
                        // Shift+Tab from zone selector → last button or last card (wrap)
                        _onZoneSelector = false;
                        if (_browserButtons.Count > 0)
                        {
                            _currentButtonIndex = _browserButtons.Count - 1;
                            _currentCardIndex = -1;
                            AnnounceCurrentButton();
                        }
                        else if (_browserCards.Count > 0)
                        {
                            _currentCardIndex = _browserCards.Count - 1;
                            AnnounceCurrentCard();
                        }
                    }
                    return true;
                }
                // Home/End: jump to first/last zone
                if (Input.GetKeyDown(KeyCode.Home))
                {
                    if (_currentZoneButtonIndex != 0)
                    {
                        _currentZoneButtonIndex = 0;
                        ActivateMultiZoneButton();
                    }
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.End))
                {
                    int lastIdx = _zoneButtons.Count - 1;
                    if (_currentZoneButtonIndex != lastIdx)
                    {
                        _currentZoneButtonIndex = lastIdx;
                        ActivateMultiZoneButton();
                    }
                    return true;
                }
                // Block other keys while on zone selector (except Space/Backspace for confirm/cancel)
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    ClickConfirmButton();
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.Backspace))
                {
                    ClickCancelButton();
                    return true;
                }
                return true;
            }

            // Tab / Shift+Tab - cycle through items (generic navigation)
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                // Multi-zone: Tab wraps back to zone selector at boundaries
                if (_isMultiZone && _zoneButtons.Count > 0)
                {
                    if (shift)
                    {
                        // Shift+Tab: if on first card → go to zone selector
                        if (_currentCardIndex == 0 && _currentButtonIndex < 0)
                        {
                            EnterZoneSelector();
                            _currentCardIndex = -1;
                            AnnounceMultiZoneSelector();
                            return true;
                        }
                        // Shift+Tab: if on first button and no cards → zone selector
                        if (_currentButtonIndex == 0 && _browserCards.Count == 0)
                        {
                            EnterZoneSelector();
                            _currentButtonIndex = -1;
                            AnnounceMultiZoneSelector();
                            return true;
                        }
                        // Otherwise, navigate backwards through cards/buttons
                        if (_currentButtonIndex > 0)
                        {
                            NavigateToPreviousButton();
                        }
                        else if (_currentButtonIndex == 0 && _browserCards.Count > 0)
                        {
                            _currentButtonIndex = -1;
                            _currentCardIndex = _browserCards.Count - 1;
                            AnnounceCurrentCard();
                        }
                        else if (_currentCardIndex > 0)
                        {
                            NavigateToPreviousCard();
                        }
                    }
                    else
                    {
                        // Tab forward: cards → buttons → zone selector
                        if (_currentCardIndex >= 0 && _currentCardIndex < _browserCards.Count - 1)
                        {
                            NavigateToNextCard();
                        }
                        else if (_currentCardIndex == _browserCards.Count - 1 && _browserButtons.Count > 0)
                        {
                            _currentCardIndex = -1;
                            _currentButtonIndex = 0;
                            AnnounceCurrentButton();
                        }
                        else if (_currentButtonIndex >= 0 && _currentButtonIndex < _browserButtons.Count - 1)
                        {
                            NavigateToNextButton();
                        }
                        else
                        {
                            // At end → wrap to zone selector
                            EnterZoneSelector();
                            _currentCardIndex = -1;
                            _currentButtonIndex = -1;
                            AnnounceMultiZoneSelector();
                        }
                    }
                    return true;
                }

                // OptionalAction: unified cycle through cards → choice buttons → wrap
                if (_browserInfo.IsOptionalAction && _browserCards.Count > 0 && _browserButtons.Count > 0)
                {
                    if (shift) NavigateToPreviousItem();
                    else NavigateToNextItem();
                }
                else if (_browserCards.Count > 0)
                {
                    if (shift) NavigateToPreviousCard();
                    else NavigateToNextCard();
                }
                else if (_browserButtons.Count > 0)
                {
                    if (shift) NavigateToPreviousButton();
                    else NavigateToNextButton();
                }
                return true;
            }

            // Left/Right arrows - card/button navigation (for non-zone browsers)
            if (!_browserInfo.IsZoneBased || _zoneNavigator.CurrentZone == BrowserZoneType.None)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    // OptionalAction: respect current focus type (card vs button)
                    if (_browserInfo.IsOptionalAction && _currentButtonIndex >= 0 && _browserButtons.Count > 0)
                        NavigateToPreviousButton();
                    else if (_browserCards.Count > 0) NavigateToPreviousCard();
                    else if (_browserButtons.Count > 0) NavigateToPreviousButton();
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    if (_browserInfo.IsOptionalAction && _currentButtonIndex >= 0 && _browserButtons.Count > 0)
                        NavigateToNextButton();
                    else if (_browserCards.Count > 0) NavigateToNextCard();
                    else if (_browserButtons.Count > 0) NavigateToNextButton();
                    return true;
                }
            }

            // Up/Down arrows - card details (delegate to CardInfoNavigator)
            // AssignDamage handles Up/Down in HandleAssignDamageInput above
            if (!_isAssignDamage && (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)))
            {
                if (_browserCards.Count > 0 && _currentCardIndex >= 0)
                {
                    var cardNav = AccessibleArenaMod.Instance?.CardNavigator;
                    if (cardNav != null && cardNav.IsActive)
                    {
                        return false; // Let CardInfoNavigator handle it
                    }
                }
                return false;
            }

            // Enter - activate current card or button
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                // Zone navigator handles Enter only when actually navigating in a zone (with selected card)
                if (_browserInfo.IsZoneBased && _zoneNavigator.CurrentZone != BrowserZoneType.None
                    && _zoneNavigator.CurrentCardIndex >= 0)
                {
                    return false; // Already handled by zone navigator
                }

                // Use generic activation for non-zone browsers or when zone has no selected card
                if (_browserCards.Count > 0 && _currentCardIndex >= 0)
                {
                    ActivateCurrentCard();
                }
                else if (_browserButtons.Count > 0 && _currentButtonIndex >= 0)
                {
                    ActivateCurrentButton();
                }
                return true;
            }

            // Space - confirm/submit
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ClickConfirmButton();
                return true;
            }

            // Backspace - cancel
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                ClickCancelButton();
                return true;
            }

            return false;
        }

        #endregion

        #region Element Discovery

        /// <summary>
        /// Discovers cards and buttons in the browser.
        /// </summary>
        private void DiscoverBrowserElements()
        {
            _browserCards.Clear();
            _browserButtons.Clear();

            if (_browserInfo == null) return;

            // Workflow browsers: use buttons already found by detector
            if (_browserInfo.IsWorkflow && _browserInfo.WorkflowButtons != null)
            {
                foreach (var button in _browserInfo.WorkflowButtons)
                {
                    if (button != null && button.activeInHierarchy)
                    {
                        _browserButtons.Add(button);
                        string buttonText = UITextExtractor.GetText(button);
                        MelonLogger.Msg($"[BrowserNavigator] Workflow button: '{buttonText}'");
                    }
                }
                MelonLogger.Msg($"[BrowserNavigator] Found {_browserButtons.Count} workflow action buttons");
                return;
            }

            // Discover based on browser type
            if (_browserInfo.IsMulligan)
            {
                DiscoverMulliganCards();
            }

            DiscoverCardsInHolders();

            // Scope button discovery to the scaffold when available.
            // Global search picks up unrelated duel UI buttons (PromptButton_Primary/Secondary)
            // that show phase info like "Opponent's turn" or "Cancel attacks".
            if (_browserInfo.BrowserGameObject != null)
            {
                FindButtonsInContainer(_browserInfo.BrowserGameObject);
            }

            // For LargeScrollList (keyword choice UI), discover actual choice buttons
            // that don't match standard ButtonPatterns (e.g. "Eine Karte abwerfen")
            if (_browserInfo.BrowserType == "LargeScrollList" && _browserInfo.BrowserGameObject != null)
            {
                DiscoverLargeScrollListChoices(_browserInfo.BrowserGameObject);
            }

            // Fallback to global search if no buttons found within scaffold
            if (_browserButtons.Count == 0)
            {
                DiscoverBrowserButtons();
            }

            // For mulligan, also search for mulligan-specific buttons
            if (_browserInfo.IsMulligan)
            {
                DiscoverMulliganButtons();
            }

            // Fallback: prompt buttons if no other buttons found
            if (_browserCards.Count > 0 && _browserButtons.Count == 0)
            {
                DiscoverPromptButtons();
            }

            // For multi-zone browsers: separate zone buttons from regular buttons
            if (_isMultiZone)
            {
                _zoneButtons.Clear();
                var regularButtons = new List<GameObject>();
                foreach (var button in _browserButtons)
                {
                    if (button != null && button.name.StartsWith("ZoneButton"))
                        _zoneButtons.Add(button);
                    else
                        regularButtons.Add(button);
                }
                // Filter invisible scaffold layout buttons without meaningful text
                regularButtons.RemoveAll(b =>
                    !UIElementClassifier.IsVisibleViaCanvasGroup(b) &&
                    !UITextExtractor.HasActualText(b));
                _browserButtons = regularButtons;

                // Sort zone buttons by name for consistent order
                _zoneButtons.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

                // Only keep zone buttons with real localized names (not generic "ZoneButtonN").
                // This filters spurious unnamed zones and detects false positive multi-zone
                // scaffolds (e.g. Tiefste Epoche) where all zones have generic names.
                _zoneButtons.RemoveAll(zb =>
                {
                    string label = UITextExtractor.GetButtonText(zb, zb.name);
                    return label.StartsWith("ZoneButton");
                });

                if (_zoneButtons.Count > 1)
                {
                    _currentZoneButtonIndex = FindActiveZoneButtonIndex();
                    EnterZoneSelector();
                    MelonLogger.Msg($"[BrowserNavigator] Multi-zone: {_zoneButtons.Count} zone buttons, {_browserCards.Count} cards, active index: {_currentZoneButtonIndex}");
                }
                else
                {
                    _zoneButtons.Clear();
                }
            }
            else
            {
                // Non-multi-zone: filter invisible scaffold buttons that have no meaningful text.
                // Keep buttons with real text even if alpha=0 (e.g. YesNo browser 2Button_Left/Right
                // are hidden via CanvasGroup but are the actual Yes/No action buttons).
                _browserButtons.RemoveAll(b =>
                    !UIElementClassifier.IsVisibleViaCanvasGroup(b) &&
                    !UITextExtractor.HasActualText(b));
            }

            // Filter "View Battlefield" button - no functionality for blind users
            _browserButtons.RemoveAll(b =>
            {
                if (b == null || b.name != "MainButton") return false;
                var t = b.transform.parent;
                for (int d = 0; t != null && d < 3; d++, t = t.parent)
                {
                    if (t.name.StartsWith("ViewBattlefield")) return true;
                }
                return false;
            });

            MelonLogger.Msg($"[BrowserNavigator] Found {_browserCards.Count} cards, {_browserButtons.Count} buttons");
        }

        /// <summary>
        /// Discovers cards for mulligan/opening hand browsers.
        /// </summary>
        private void DiscoverMulliganCards()
        {
            MelonLogger.Msg($"[BrowserNavigator] Searching for opening hand cards");

            // Search for LocalHand zone
            var localHandZones = BrowserDetector.FindActiveGameObjects(go => go.name.StartsWith(ZoneLocalHand));
            foreach (var zone in localHandZones)
            {
                SearchForCardsInLocalHand(zone);
            }

            // Also search within the browser scaffold
            if (_browserInfo.BrowserGameObject != null)
            {
                SearchForCardsInContainer(_browserInfo.BrowserGameObject, "Scaffold");
            }

            MelonLogger.Msg($"[BrowserNavigator] After opening hand search: {_browserCards.Count} cards found");
        }

        /// <summary>
        /// Discovers cards in BrowserCardHolder containers.
        /// </summary>
        private void DiscoverCardsInHolders()
        {
            var holders = BrowserDetector.FindActiveGameObjects(go =>
                go.name == BrowserDetector.HolderDefault || go.name == BrowserDetector.HolderViewDismiss);

            foreach (var holder in holders)
            {
                foreach (Transform child in holder.GetComponentsInChildren<Transform>(true))
                {
                    if (!child.gameObject.activeInHierarchy) continue;
                    if (!CardDetector.IsCard(child.gameObject)) continue;

                    string cardName = CardDetector.GetCardName(child.gameObject);

                    if (!BrowserDetector.IsValidCardName(cardName)) continue;
                    if (BrowserDetector.IsDuplicateCard(child.gameObject, _browserCards)) continue;

                    _browserCards.Add(child.gameObject);
                    MelonLogger.Msg($"[BrowserNavigator] Found card in {holder.name}: {child.name} -> {cardName}");
                }
            }
        }

        /// <summary>
        /// Discovers buttons in browser-related containers.
        /// </summary>
        private void DiscoverBrowserButtons()
        {
            var browserContainers = BrowserDetector.FindActiveGameObjects(go =>
                go.name.Contains("Browser") || go.name.Contains("Prompt"));

            foreach (var container in browserContainers)
            {
                FindButtonsInContainer(container);
            }
        }

        /// <summary>
        /// Discovers mulligan-specific buttons (Keep/Mulligan).
        /// </summary>
        private void DiscoverMulliganButtons()
        {
            var mulliganButtons = BrowserDetector.FindActiveGameObjects(go =>
                go.name == BrowserDetector.ButtonKeep || go.name == BrowserDetector.ButtonMulligan);

            foreach (var button in mulliganButtons)
            {
                if (!_browserButtons.Contains(button))
                {
                    _browserButtons.Add(button);
                    MelonLogger.Msg($"[BrowserNavigator] Added mulligan button: {button.name}");
                }
            }
        }

        /// <summary>
        /// Discovers PromptButton_Primary/Secondary as fallback.
        /// </summary>
        private void DiscoverPromptButtons()
        {
            MelonLogger.Msg($"[BrowserNavigator] No buttons found, searching for PromptButtons...");

            var promptButtons = BrowserDetector.FindActiveGameObjects(go =>
                go.name.StartsWith(BrowserDetector.PromptButtonPrimaryPrefix) ||
                go.name.StartsWith(BrowserDetector.PromptButtonSecondaryPrefix));

            foreach (var button in promptButtons)
            {
                if (!_browserButtons.Contains(button))
                {
                    _browserButtons.Add(button);
                    string buttonText = UITextExtractor.GetButtonText(button, button.name);
                    MelonLogger.Msg($"[BrowserNavigator] Found prompt button: {button.name} -> '{buttonText}'");
                }
            }
        }

        /// <summary>
        /// Finds clickable buttons in a container.
        /// </summary>
        private void FindButtonsInContainer(GameObject root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (!child.gameObject.activeInHierarchy) continue;
                if (!BrowserDetector.MatchesButtonPattern(child.name, BrowserDetector.ButtonPatterns)) continue;
                if (!BrowserDetector.HasClickableComponent(child.gameObject)) continue;
                if (_browserButtons.Contains(child.gameObject)) continue;

                _browserButtons.Add(child.gameObject);
                MelonLogger.Msg($"[BrowserNavigator] Found button: {child.name}");
            }
        }

        /// <summary>
        /// Discovers choice buttons in LargeScrollList browsers (keyword choice UI).
        /// These buttons don't match standard ButtonPatterns because they're named
        /// with their text content (e.g. "Eine Karte abwerfen" instead of "Button_Submit").
        /// Reorders so choices come first and scaffold controls (MainButton) come last.
        /// </summary>
        private void DiscoverLargeScrollListChoices(GameObject root)
        {
            var choiceButtons = new List<GameObject>();
            var seenTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (!child.gameObject.activeInHierarchy) continue;
                if (!BrowserDetector.HasClickableComponent(child.gameObject)) continue;
                if (_browserButtons.Contains(child.gameObject)) continue;
                if (BrowserDetector.MatchesButtonPattern(child.name, BrowserDetector.ButtonPatterns)) continue;

                string choiceText = UITextExtractor.GetButtonText(child.gameObject, child.name);

                // Skip internal UI elements (e.g. Primary_Base(Clone))
                if (string.IsNullOrEmpty(choiceText) || choiceText == child.name)
                    continue;

                // Deduplicate by text (parent+child both have clickable components)
                if (!seenTexts.Add(choiceText))
                    continue;

                choiceButtons.Add(child.gameObject);
                MelonLogger.Msg($"[BrowserNavigator] Found LargeScrollList choice: '{choiceText}'");
            }

            if (choiceButtons.Count > 0)
            {
                // Replace scaffold buttons entirely - choices are the only navigable items
                _browserButtons.Clear();
                _browserButtons.AddRange(choiceButtons);
                MelonLogger.Msg($"[BrowserNavigator] LargeScrollList: {choiceButtons.Count} choices");
            }
        }

        /// <summary>
        /// Searches for cards in a container.
        /// </summary>
        private void SearchForCardsInContainer(GameObject container, string containerName)
        {
            int foundCount = 0;
            foreach (Transform child in container.GetComponentsInChildren<Transform>(true))
            {
                if (!child.gameObject.activeInHierarchy) continue;
                if (!CardDetector.IsCard(child.gameObject)) continue;

                string cardName = CardDetector.GetCardName(child.gameObject);
                if (!BrowserDetector.IsValidCardName(cardName)) continue;
                if (BrowserDetector.IsDuplicateCard(child.gameObject, _browserCards)) continue;

                _browserCards.Add(child.gameObject);
                foundCount++;
            }

            if (foundCount > 0)
            {
                MelonLogger.Msg($"[BrowserNavigator] Container {containerName} had {foundCount} cards");
            }
        }

        /// <summary>
        /// Searches for cards in the LocalHand zone for mulligan/opening hand browsers.
        /// </summary>
        private void SearchForCardsInLocalHand(GameObject localHandZone)
        {
            var foundCards = new List<GameObject>();

            foreach (Transform child in localHandZone.GetComponentsInChildren<Transform>(true))
            {
                if (!child.gameObject.activeInHierarchy) continue;
                if (!CardDetector.IsCard(child.gameObject)) continue;

                var card = child.gameObject;
                string cardName = CardDetector.GetCardName(card);

                if (!BrowserDetector.IsValidCardName(cardName)) continue;

                // Additional filter: check if card has readable data
                var cardInfo = CardDetector.ExtractCardInfo(card);
                if (string.IsNullOrEmpty(cardInfo.Name)) continue;

                // Filter out cards from other zones (e.g., commander from Command zone)
                // that the game places visually in the hand holder
                string modelZone = CardModelProvider.GetCardZoneTypeName(card);
                if (!string.IsNullOrEmpty(modelZone) && modelZone != "Hand")
                {
                    MelonLogger.Msg($"[BrowserNavigator] Skipping {cardInfo.Name} - actual zone: {modelZone}");
                    continue;
                }

                if (!BrowserDetector.IsDuplicateCard(card, foundCards))
                {
                    foundCards.Add(card);
                }
            }

            // Sort cards by horizontal position (left to right)
            foundCards.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

            // Add to browser cards (avoiding duplicates)
            foreach (var card in foundCards)
            {
                if (!_browserCards.Contains(card))
                {
                    _browserCards.Add(card);
                }
            }

            MelonLogger.Msg($"[BrowserNavigator] LocalHand search: found {foundCards.Count} valid cards");
        }

        #endregion

        #region Announcements

        /// <summary>
        /// Announces the current browser state.
        /// </summary>
        private void AnnounceBrowserState()
        {
            string browserName = BrowserDetector.GetFriendlyBrowserName(_browserInfo.BrowserType);
            int cardCount = _browserCards.Count;
            int buttonCount = _browserButtons.Count;

            string message;

            // Special announcement for AssignDamage
            if (_isAssignDamage)
            {
                message = GetAssignDamageEntryAnnouncement(cardCount, browserName);
            }
            // Special announcement for London mulligan
            else if (_browserInfo.IsLondon)
            {
                var londonAnnouncement = _zoneNavigator.GetLondonEntryAnnouncement(cardCount);
                if (londonAnnouncement != null)
                {
                    message = londonAnnouncement;
                }
                else if (cardCount > 0)
                {
                    message = Strings.BrowserCards(cardCount, browserName);
                }
                else
                {
                    message = browserName;
                }
            }
            else if (cardCount > 0)
            {
                message = Strings.BrowserCards(cardCount, browserName);
            }
            else if (buttonCount > 0)
            {
                message = Strings.BrowserOptions(browserName);
            }
            else
            {
                message = browserName;
            }

            _announcer.Announce(message, AnnouncementPriority.High);

            // Auto-navigate to first item
            if (_isMultiZone && _zoneButtons.Count > 0)
            {
                // Multi-zone: start on zone selector
                EnterZoneSelector();
                _currentCardIndex = -1;
                _currentButtonIndex = -1;
                AnnounceMultiZoneSelector();
            }
            else if (cardCount > 0)
            {
                _currentCardIndex = 0;
                AnnounceCurrentCard();
            }
            else if (buttonCount > 0)
            {
                _currentButtonIndex = 0;
                AnnounceCurrentButton();
            }
        }

        /// <summary>
        /// Announces the current card.
        /// </summary>
        private void AnnounceCurrentCard()
        {
            if (_currentCardIndex < 0 || _currentCardIndex >= _browserCards.Count) return;

            var card = _browserCards[_currentCardIndex];

            // AssignDamage: custom announcement with P/T, lethal, position; skip PrepareForCard
            if (_isAssignDamage)
            {
                AnnounceAssignDamageCard(card);
                return;
            }

            var info = CardDetector.ExtractCardInfo(card);
            bool isSelectionBrowser = _browserInfo?.BrowserType == "SelectCards" || _browserInfo?.BrowserType == "SelectCardsMultiZone";
            string cardName = isSelectionBrowser && !string.IsNullOrEmpty(info.RulesText)
                ? info.RulesText
                : info.Name ?? "Unknown card";

            // Get selection state from zone navigator for zone-based browsers
            string selectionState = null;
            if (_browserInfo.IsZoneBased)
            {
                selectionState = _zoneNavigator.GetCardSelectionState(card);
            }
            else if (!_browserInfo.IsMulligan && _browserButtons.Count > 0)
            {
                // Check holder for non-zone browsers with buttons (might be scry-like)
                selectionState = GetCardHolderState(card);
            }

            // For multi-zone browsers, append the card's zone (e.g., "Your graveyard", "Opponent's exile")
            string zoneSuffix = "";
            if (_browserInfo?.BrowserType == "SelectCardsMultiZone")
            {
                zoneSuffix = GetMultiZoneCardZoneName(card);
            }

            // Build announcement
            string announcement;
            if (_browserCards.Count == 1)
            {
                announcement = string.IsNullOrEmpty(selectionState)
                    ? $"{cardName}{zoneSuffix}"
                    : $"{cardName}{zoneSuffix}, {selectionState}";
            }
            else
            {
                string position = $"{_currentCardIndex + 1} of {_browserCards.Count}";
                announcement = string.IsNullOrEmpty(selectionState)
                    ? $"{cardName}{zoneSuffix}, {position}"
                    : $"{cardName}{zoneSuffix}, {selectionState}, {position}";
            }

            _announcer.Announce(announcement, AnnouncementPriority.High);

            // Selection browsers show options, not cards - use Browser zone for rules-first ordering
            var zone = (_browserInfo?.BrowserType == "SelectCards" || _browserInfo?.BrowserType == "SelectCardsMultiZone")
                ? ZoneType.Browser
                : ZoneType.Library;
            AccessibleArenaMod.Instance?.CardNavigator?.PrepareForCard(card, zone);
        }

        /// <summary>
        /// Gets card state based on holder (for non-zone browsers).
        /// </summary>
        private string GetCardHolderState(GameObject card)
        {
            if (card == null) return null;

            Transform parent = card.transform.parent;
            while (parent != null)
            {
                if (parent.name == BrowserDetector.HolderDefault)
                    return Strings.KeepOnTop;
                if (parent.name == BrowserDetector.HolderViewDismiss)
                    return Strings.PutOnBottom;
                parent = parent.parent;
            }
            return null;
        }

        // Maps game zone names to mod ZoneType, with local/opponent variants
        private static readonly Dictionary<string, (ZoneType local, ZoneType opponent)> MultiZoneMap =
            new Dictionary<string, (ZoneType, ZoneType)>
        {
            { "Graveyard", (ZoneType.Graveyard, ZoneType.OpponentGraveyard) },
            { "Exile", (ZoneType.Exile, ZoneType.OpponentExile) },
            { "Library", (ZoneType.Library, ZoneType.OpponentLibrary) },
            { "Hand", (ZoneType.Hand, ZoneType.OpponentHand) },
            { "Command", (ZoneType.Command, ZoneType.OpponentCommand) }
        };

        /// <summary>
        /// Gets the localized zone name for a card in a SelectCardsMultiZone browser.
        /// Returns ", Your graveyard" or ", Opponent's graveyard" etc.
        /// </summary>
        private string GetMultiZoneCardZoneName(GameObject card)
        {
            string modelZone = CardModelProvider.GetCardZoneTypeName(card);
            if (string.IsNullOrEmpty(modelZone)) return "";

            if (MultiZoneMap.TryGetValue(modelZone, out var zonePair))
            {
                bool isOpponent = CardModelProvider.IsOpponentCard(card);
                var zoneType = isOpponent ? zonePair.opponent : zonePair.local;
                return $", {Strings.GetZoneName(zoneType)}";
            }

            return "";
        }

        #region Multi-Zone Navigation

        /// <summary>
        /// Announces the multi-zone selector element with current zone name and card count.
        /// </summary>
        private void AnnounceMultiZoneSelector()
        {
            string zoneName = GetCurrentZoneButtonLabel();
            string hint = _zoneButtons.Count > 1
                ? $", {_currentZoneButtonIndex + 1} of {_zoneButtons.Count}"
                : "";
            string cardInfo = _browserCards.Count > 0 ? $", {_browserCards.Count} cards" : "";
            string announcement = $"{Strings.ZoneChange}: {zoneName}{hint}{cardInfo}";
            _announcer.Announce(announcement, AnnouncementPriority.High);
        }

        /// <summary>
        /// Cycles to the next/previous zone in a multi-zone browser.
        /// Clicks the zone button and rediscovers cards after a delay.
        /// </summary>
        private void CycleMultiZone(bool next)
        {
            if (_zoneButtons.Count == 0) return;

            int newIndex = _currentZoneButtonIndex + (next ? 1 : -1);
            if (newIndex < 0)
            {
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }
            if (newIndex >= _zoneButtons.Count)
            {
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            _currentZoneButtonIndex = newIndex;
            ActivateMultiZoneButton();
        }

        /// <summary>
        /// Clicks the current zone button and schedules card rediscovery.
        /// </summary>
        private void ActivateMultiZoneButton()
        {
            var button = _zoneButtons[_currentZoneButtonIndex];
            MelonLogger.Msg($"[BrowserNavigator] Activating zone button: {button.name}");
            UIActivator.SimulatePointerClick(button);

            // Rediscover cards after game updates the holder
            MelonCoroutines.Start(RediscoverMultiZoneCards());
        }

        /// <summary>
        /// Waits for the game to update the card holder after a zone change,
        /// then rediscovers cards and announces the new zone.
        /// </summary>
        private IEnumerator RediscoverMultiZoneCards()
        {
            yield return new WaitForSeconds(0.3f);

            // Rediscover cards in holders
            _browserCards.Clear();
            _currentCardIndex = -1;
            DiscoverCardsInHolders();

            MelonLogger.Msg($"[BrowserNavigator] Multi-zone rediscovery: {_browserCards.Count} cards");
            AnnounceMultiZoneSelector();
        }

        /// <summary>
        /// Gets the display label for the currently selected zone button.
        /// </summary>
        private string GetCurrentZoneButtonLabel()
        {
            if (_currentZoneButtonIndex < 0 || _currentZoneButtonIndex >= _zoneButtons.Count)
                return "?";

            var button = _zoneButtons[_currentZoneButtonIndex];
            string label = UITextExtractor.GetButtonText(button, button.name);

            // If the button only has a generic name like "ZoneButton0", try to extract zone info
            if (label.StartsWith("ZoneButton"))
                label = $"Zone {_currentZoneButtonIndex + 1}";

            return label;
        }

        /// <summary>
        /// Finds which zone button is currently active/selected by checking visual state.
        /// Falls back to 0 if no active button can be determined.
        /// </summary>
        private int FindActiveZoneButtonIndex()
        {
            // Try to detect which zone button is visually active (selected state)
            for (int i = 0; i < _zoneButtons.Count; i++)
            {
                var button = _zoneButtons[i];
                // Check if button has a Toggle component that's on
                var toggle = button.GetComponent<Toggle>();
                if (toggle != null && toggle.isOn)
                {
                    MelonLogger.Msg($"[BrowserNavigator] Zone button {i} ({button.name}) is active (Toggle.isOn)");
                    return i;
                }
            }

            // Fallback: first button
            return 0;
        }

        #endregion

        /// <summary>
        /// Announces the current button.
        /// </summary>
        private void AnnounceCurrentButton()
        {
            if (_currentButtonIndex < 0 || _currentButtonIndex >= _browserButtons.Count) return;

            var button = _browserButtons[_currentButtonIndex];

            if (button == null)
            {
                MelonLogger.Warning("[BrowserNavigator] Button at index was destroyed, refreshing buttons");
                RefreshBrowserButtons();
                return;
            }

            string label = UITextExtractor.GetButtonText(button, button.name);
            string position = _browserButtons.Count > 1 ? $", {_currentButtonIndex + 1} of {_browserButtons.Count}" : "";

            _announcer.Announce($"{label}{position}", AnnouncementPriority.High);
        }

        #endregion

        #region Navigation

        private void NavigateToNextCard()
        {
            if (_browserCards.Count == 0)
            {
                _announcer.Announce(Strings.NoCards, AnnouncementPriority.Normal);
                return;
            }

            _currentCardIndex = (_currentCardIndex + 1) % _browserCards.Count;
            AnnounceCurrentCard();
        }

        private void NavigateToPreviousCard()
        {
            if (_browserCards.Count == 0)
            {
                _announcer.Announce(Strings.NoCards, AnnouncementPriority.Normal);
                return;
            }

            _currentCardIndex--;
            if (_currentCardIndex < 0) _currentCardIndex = _browserCards.Count - 1;
            AnnounceCurrentCard();
        }

        private void NavigateToNextButton()
        {
            if (_browserButtons.Count == 0) return;

            _currentButtonIndex = (_currentButtonIndex + 1) % _browserButtons.Count;
            AnnounceCurrentButton();
        }

        private void NavigateToPreviousButton()
        {
            if (_browserButtons.Count == 0) return;

            _currentButtonIndex--;
            if (_currentButtonIndex < 0) _currentButtonIndex = _browserButtons.Count - 1;
            AnnounceCurrentButton();
        }

        /// <summary>
        /// Navigates to the next item across both cards and buttons.
        /// Order: cards first, then buttons, then wraps back to first card.
        /// Maintains mutual exclusion: focusing a card clears button index and vice versa.
        /// </summary>
        private void NavigateToNextItem()
        {
            int totalCards = _browserCards.Count;
            int totalButtons = _browserButtons.Count;
            int totalItems = totalCards + totalButtons;
            if (totalItems == 0) return;

            // Determine current unified index
            int currentIndex;
            if (_currentCardIndex >= 0)
                currentIndex = _currentCardIndex;
            else if (_currentButtonIndex >= 0)
                currentIndex = totalCards + _currentButtonIndex;
            else
                currentIndex = -1;

            int nextIndex = (currentIndex + 1) % totalItems;

            if (nextIndex < totalCards)
            {
                _currentCardIndex = nextIndex;
                _currentButtonIndex = -1;
                AnnounceCurrentCard();
            }
            else
            {
                _currentButtonIndex = nextIndex - totalCards;
                _currentCardIndex = -1;
                AnnounceCurrentButton();
            }
        }

        /// <summary>
        /// Navigates to the previous item across both cards and buttons.
        /// Order: wraps from first card to last button, from first button to last card.
        /// Maintains mutual exclusion: focusing a card clears button index and vice versa.
        /// </summary>
        private void NavigateToPreviousItem()
        {
            int totalCards = _browserCards.Count;
            int totalButtons = _browserButtons.Count;
            int totalItems = totalCards + totalButtons;
            if (totalItems == 0) return;

            // Determine current unified index
            int currentIndex;
            if (_currentCardIndex >= 0)
                currentIndex = _currentCardIndex;
            else if (_currentButtonIndex >= 0)
                currentIndex = totalCards + _currentButtonIndex;
            else
                currentIndex = 0;

            int prevIndex = currentIndex - 1;
            if (prevIndex < 0) prevIndex = totalItems - 1;

            if (prevIndex < totalCards)
            {
                _currentCardIndex = prevIndex;
                _currentButtonIndex = -1;
                AnnounceCurrentCard();
            }
            else
            {
                _currentButtonIndex = prevIndex - totalCards;
                _currentCardIndex = -1;
                AnnounceCurrentButton();
            }
        }

        #endregion

        #region Activation

        /// <summary>
        /// Activates (clicks) the current card.
        /// For zone-based browsers (Scry/London), uses proper API to move cards between zones.
        /// For other browsers, uses generic click.
        /// </summary>
        private void ActivateCurrentCard()
        {
            if (_currentCardIndex < 0 || _currentCardIndex >= _browserCards.Count)
            {
                _announcer.Announce(Strings.NoCardSelected, AnnouncementPriority.Normal);
                return;
            }

            var card = _browserCards[_currentCardIndex];
            var cardName = CardDetector.GetCardName(card) ?? "card";

            MelonLogger.Msg($"[BrowserNavigator] Activating card: {cardName}");

            // For zone-based browsers (Scry/London), use zone navigator to move card properly
            if (_browserInfo.IsZoneBased)
            {
                bool success = _zoneNavigator.ActivateCardFromGenericNavigation(card);
                if (!success)
                {
                    _announcer.Announce(Strings.CouldNotSelect(cardName), AnnouncementPriority.High);
                }
                return;
            }

            // For non-zone browsers, use generic click
            var result = UIActivator.SimulatePointerClick(card);
            if (!result.Success)
            {
                _announcer.Announce(Strings.CouldNotSelect(cardName), AnnouncementPriority.High);
                return;
            }

            // Wait for game state to update
            MelonCoroutines.Start(AnnounceStateChangeAfterDelay(cardName));
        }

        /// <summary>
        /// Waits for UI update then announces the new state.
        /// </summary>
        private IEnumerator AnnounceStateChangeAfterDelay(string cardName)
        {
            yield return new WaitForSeconds(0.2f);

            // Re-find the card (it may have moved)
            GameObject card = null;
            var holders = BrowserDetector.FindActiveGameObjects(go =>
                go.name == BrowserDetector.HolderDefault || go.name == BrowserDetector.HolderViewDismiss);

            foreach (var holder in holders)
            {
                foreach (Transform child in holder.GetComponentsInChildren<Transform>(true))
                {
                    if (!child.gameObject.activeInHierarchy) continue;
                    if (CardDetector.IsCard(child.gameObject))
                    {
                        string name = CardDetector.GetCardName(child.gameObject);
                        if (name == cardName)
                        {
                            card = child.gameObject;
                            break;
                        }
                    }
                }
                if (card != null) break;
            }

            if (card != null)
            {
                string stateAfter = GetCardHolderState(card);
                if (!string.IsNullOrEmpty(stateAfter))
                {
                    _announcer.Announce(stateAfter, AnnouncementPriority.Normal);
                }
                else
                {
                    _announcer.Announce(Strings.Selected, AnnouncementPriority.Normal);
                }

                // Update the card reference
                if (_currentCardIndex >= 0 && _currentCardIndex < _browserCards.Count)
                {
                    _browserCards[_currentCardIndex] = card;
                }
            }
        }

        /// <summary>
        /// Activates (clicks) the current button.
        /// </summary>
        private void ActivateCurrentButton()
        {
            if (_currentButtonIndex < 0 || _currentButtonIndex >= _browserButtons.Count)
            {
                _announcer.Announce(Strings.NoButtonSelected, AnnouncementPriority.Normal);
                return;
            }

            var button = _browserButtons[_currentButtonIndex];
            var label = UITextExtractor.GetButtonText(button, button.name);

            MelonLogger.Msg($"[BrowserNavigator] Activating button: {label}");

            var result = UIActivator.SimulatePointerClick(button);
            if (result.Success)
            {
                _announcer.Announce(label, AnnouncementPriority.Normal);
            }
            else
            {
                _announcer.Announce(Strings.CouldNotClick(label), AnnouncementPriority.High);
            }
        }

        /// <summary>
        /// Tries to submit the current workflow via reflection by accessing GameManager.WorkflowController.
        /// This bypasses the need to click UI elements that may not have standard click handlers.
        /// </summary>
        /// <returns>True if workflow was successfully submitted</returns>
        private bool TrySubmitWorkflowViaReflection()
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            try
            {
                // Find GameManager
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
                    MelonLogger.Msg($"[BrowserNavigator] WorkflowReflection: GameManager not found");
                    if (BrowserDetector.IsDebugEnabled(BrowserDetector.BrowserTypeWorkflow))
                        MenuDebugHelper.DumpWorkflowSystemDebug("WorkflowDebug");
                    return false;
                }

                // Get WorkflowController
                var wcProp = gameManager.GetType().GetProperty("WorkflowController", flags);
                var workflowController = wcProp?.GetValue(gameManager);

                if (workflowController == null)
                {
                    MelonLogger.Msg($"[BrowserNavigator] WorkflowReflection: WorkflowController not found");
                    if (BrowserDetector.IsDebugEnabled(BrowserDetector.BrowserTypeWorkflow))
                        MenuDebugHelper.DumpWorkflowSystemDebug("WorkflowDebug");
                    return false;
                }

                // Get CurrentInteraction - try both property and field
                var wcType = workflowController.GetType();
                object currentInteraction = null;

                // Try property first
                var ciProp = wcType.GetProperty("CurrentInteraction", flags);
                if (ciProp != null)
                {
                    currentInteraction = ciProp.GetValue(workflowController);
                }

                // Try field if property didn't work
                if (currentInteraction == null)
                {
                    var ciField = wcType.GetField("_currentInteraction", flags)
                               ?? wcType.GetField("currentInteraction", flags)
                               ?? wcType.GetField("_current", flags);
                    if (ciField != null)
                    {
                        currentInteraction = ciField.GetValue(workflowController);
                    }
                }

                if (currentInteraction == null)
                {
                    MelonLogger.Msg($"[BrowserNavigator] WorkflowReflection: No active workflow found");
                    if (BrowserDetector.IsDebugEnabled(BrowserDetector.BrowserTypeWorkflow))
                        MenuDebugHelper.DumpWorkflowSystemDebug("WorkflowDebug");
                    return false;
                }

                var workflowType = currentInteraction.GetType();
                MelonLogger.Msg($"[BrowserNavigator] WorkflowReflection: Found workflow: {workflowType.Name}");

                // Try to submit via _request.SubmitSolution()
                var requestField = workflowType.GetField("_request", flags);
                if (requestField != null)
                {
                    var request = requestField.GetValue(currentInteraction);
                    if (request != null)
                    {
                        // Find solution field
                        var solutionField = workflowType.GetField("_autoTapSolution", flags)
                                         ?? workflowType.GetField("autoTapSolution", flags);
                        var solutionProp = workflowType.GetProperty("AutoTapSolution", flags)
                                        ?? workflowType.GetProperty("Solution", flags);

                        object solution = solutionField?.GetValue(currentInteraction)
                                       ?? solutionProp?.GetValue(currentInteraction);

                        // Try SubmitSolution
                        var submitMethod = request.GetType().GetMethod("SubmitSolution", flags);
                        if (submitMethod != null)
                        {
                            var parameters = submitMethod.GetParameters();
                            if (parameters.Length == 0)
                            {
                                submitMethod.Invoke(request, null);
                                MelonLogger.Msg($"[BrowserNavigator] WorkflowReflection: Called SubmitSolution()");
                                return true;
                            }
                            else if (parameters.Length == 1 && solution != null)
                            {
                                submitMethod.Invoke(request, new[] { solution });
                                MelonLogger.Msg($"[BrowserNavigator] WorkflowReflection: Called SubmitSolution(solution)");
                                return true;
                            }
                        }
                    }
                }

                // Try direct Submit/Confirm methods on workflow
                foreach (var methodName in new[] { "Submit", "Confirm", "Complete", "Accept", "Close" })
                {
                    var method = workflowType.GetMethod(methodName, flags);
                    if (method != null && method.GetParameters().Length == 0)
                    {
                        method.Invoke(currentInteraction, null);
                        MelonLogger.Msg($"[BrowserNavigator] WorkflowReflection: Called {methodName}()");
                        return true;
                    }
                }

                // If nothing worked, log failure
                MelonLogger.Msg($"[BrowserNavigator] WorkflowReflection: Could not find submit method");
                if (BrowserDetector.IsDebugEnabled(BrowserDetector.BrowserTypeWorkflow))
                    MenuDebugHelper.DumpWorkflowSystemDebug("WorkflowDebug");
                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] WorkflowReflection error: {ex.Message}");
                if (BrowserDetector.IsDebugEnabled(BrowserDetector.BrowserTypeWorkflow))
                    MenuDebugHelper.DumpWorkflowSystemDebug("WorkflowDebug");
                return false;
            }
        }

        #endregion

        #region Button Clicking

        /// <summary>
        /// Clicks the confirm/primary button.
        /// </summary>
        private void ClickConfirmButton()
        {
            MelonLogger.Msg($"[BrowserNavigator] ClickConfirmButton called. Browser: {_browserInfo?.BrowserType}");

            string clickedLabel;

            // Workflow browser: try reflection approach to submit via WorkflowController
            if (_browserInfo?.IsWorkflow == true)
            {
                // First try the reflection approach (access WorkflowController directly)
                if (TrySubmitWorkflowViaReflection())
                {
                    MelonLogger.Msg($"[BrowserNavigator] Workflow submitted via reflection");
                    _announcer.Announce(Strings.Confirmed, AnnouncementPriority.Normal);
                    BrowserDetector.InvalidateCache();
                    return;
                }

                // Fallback: try clicking the button if reflection failed
                MelonLogger.Msg($"[BrowserNavigator] Reflection approach failed, trying button click");
                if (_browserButtons.Count > 0 && _currentButtonIndex >= 0)
                {
                    ActivateCurrentButton();
                }
                else
                {
                    _announcer.Announce(Strings.NoButtonSelected, AnnouncementPriority.Normal);
                }
                return;
            }

            // London mulligan: click SubmitButton
            if (_browserInfo?.IsLondon == true)
            {
                if (TryClickButtonByName(BrowserDetector.ButtonSubmit, out clickedLabel))
                {
                    _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                    BrowserDetector.InvalidateCache(); // Force re-detection on next Update
                    return;
                }
            }

            // Mulligan/opening hand: prioritize KeepButton
            if (_browserInfo?.IsMulligan == true)
            {
                if (TryClickButtonByName(BrowserDetector.ButtonKeep, out clickedLabel))
                {
                    _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                    BrowserDetector.InvalidateCache(); // Force re-detection on next Update
                    return;
                }
            }

            // Try discovered buttons by name pattern (SubmitButton, ConfirmButton, etc.)
            if (TryClickButtonByPatterns(BrowserDetector.ConfirmPatterns, out clickedLabel))
            {
                _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                BrowserDetector.InvalidateCache(); // Force re-detection on next Update
                return;
            }

            // OptionalAction: try MainButton (shockland pay-life choices etc.)
            if (_browserInfo?.IsOptionalAction == true && TryClickButtonByName("MainButton", out clickedLabel))
            {
                _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                BrowserDetector.InvalidateCache();
                return;
            }

            // Fallback: PromptButton_Primary (scene search)
            // Skip for OptionalAction — its buttons are choices, not confirm/cancel,
            // and the global PromptButtons would click unrelated duel phase buttons
            if (!(_browserInfo?.IsOptionalAction == true) && TryClickPromptButton(BrowserDetector.PromptButtonPrimaryPrefix, out clickedLabel))
            {
                _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                BrowserDetector.InvalidateCache(); // Force re-detection on next Update
                return;
            }

            _announcer.Announce(Strings.NoConfirmButton, AnnouncementPriority.Normal);
        }

        /// <summary>
        /// Clicks the cancel/secondary button.
        /// </summary>
        private void ClickCancelButton()
        {
            MelonLogger.Msg($"[BrowserNavigator] ClickCancelButton called. Browser: {_browserInfo?.BrowserType}");

            string clickedLabel;

            // First priority: MulliganButton (doesn't close browser, starts new mulligan)
            if (TryClickButtonByName(BrowserDetector.ButtonMulligan, out clickedLabel))
            {
                // Track mulligan count for London phase
                _zoneNavigator.IncrementMulliganCount();
                _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                BrowserDetector.InvalidateCache(); // Browser will change to London
                return;
            }

            // Second priority: other cancel buttons by pattern
            if (TryClickButtonByPatterns(BrowserDetector.CancelPatterns, out clickedLabel))
            {
                _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                BrowserDetector.InvalidateCache(); // Force re-detection on next Update
                return;
            }

            // Third priority: PromptButton_Secondary
            // Skip for OptionalAction — would click unrelated duel phase buttons
            if (!(_browserInfo?.IsOptionalAction == true) && TryClickPromptButton(BrowserDetector.PromptButtonSecondaryPrefix, out clickedLabel))
            {
                _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                BrowserDetector.InvalidateCache(); // Force re-detection on next Update
                return;
            }

            // Not finding cancel is OK - some browsers don't have it
            MelonLogger.Msg("[BrowserNavigator] No cancel button found");
        }

        /// <summary>
        /// Tries to click a specific button by exact name.
        /// </summary>
        private bool TryClickButtonByName(string buttonName, out string clickedLabel)
        {
            clickedLabel = null;

            // Check discovered buttons first
            foreach (var button in _browserButtons)
            {
                if (button == null) continue;
                if (button.name == buttonName)
                {
                    clickedLabel = UITextExtractor.GetButtonText(button, button.name);
                    var result = UIActivator.SimulatePointerClick(button);
                    if (result.Success)
                    {
                        MelonLogger.Msg($"[BrowserNavigator] Clicked {buttonName}: '{clickedLabel}'");
                        return true;
                    }
                }
            }

            // Search scene as fallback
            var go = BrowserDetector.FindActiveGameObject(buttonName);
            if (go != null)
            {
                clickedLabel = UITextExtractor.GetButtonText(go, go.name);
                var result = UIActivator.SimulatePointerClick(go);
                if (result.Success)
                {
                    MelonLogger.Msg($"[BrowserNavigator] Clicked {buttonName} (scene): '{clickedLabel}'");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tries to click a button matching the given patterns.
        /// </summary>
        private bool TryClickButtonByPatterns(string[] patterns, out string clickedLabel)
        {
            clickedLabel = null;

            foreach (var button in _browserButtons)
            {
                if (button == null) continue;
                if (BrowserDetector.MatchesButtonPattern(button.name, patterns))
                {
                    clickedLabel = UITextExtractor.GetButtonText(button, button.name);
                    var result = UIActivator.SimulatePointerClick(button);
                    if (result.Success)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Tries to click a PromptButton (Primary or Secondary).
        /// </summary>
        private bool TryClickPromptButton(string prefix, out string clickedLabel)
        {
            clickedLabel = null;

            var buttons = BrowserDetector.FindActiveGameObjects(go => go.name.StartsWith(prefix));
            foreach (var go in buttons)
            {
                var selectable = go.GetComponent<Selectable>();
                if (selectable != null && !selectable.interactable) continue;

                clickedLabel = UITextExtractor.GetButtonText(go, go.name);

                // Skip keyboard hints
                if (prefix == BrowserDetector.PromptButtonSecondaryPrefix &&
                    clickedLabel.Length <= 4 && !clickedLabel.Contains(" "))
                {
                    continue;
                }

                var result = UIActivator.SimulatePointerClick(go);
                if (result.Success)
                {
                    MelonLogger.Msg($"[BrowserNavigator] Clicked {prefix}: '{clickedLabel}'");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Refreshes the button list, removing destroyed buttons.
        /// </summary>
        private void RefreshBrowserButtons()
        {
            _browserButtons.RemoveAll(b => b == null);

            if (_browserButtons.Count == 0 && _browserInfo?.BrowserGameObject != null)
            {
                FindButtonsInContainer(_browserInfo.BrowserGameObject);

                // Also search for mulligan buttons
                var mulliganButtons = BrowserDetector.FindActiveGameObjects(go =>
                    go.name == BrowserDetector.ButtonKeep || go.name == BrowserDetector.ButtonMulligan);
                foreach (var btn in mulliganButtons)
                {
                    if (!_browserButtons.Contains(btn))
                        _browserButtons.Add(btn);
                }

                // Also search for prompt buttons
                var promptButtons = BrowserDetector.FindActiveGameObjects(go =>
                    go.name.StartsWith(BrowserDetector.PromptButtonPrimaryPrefix) ||
                    go.name.StartsWith(BrowserDetector.PromptButtonSecondaryPrefix));
                foreach (var btn in promptButtons)
                {
                    if (!_browserButtons.Contains(btn))
                        _browserButtons.Add(btn);
                }
            }

            if (_currentButtonIndex >= _browserButtons.Count)
            {
                _currentButtonIndex = _browserButtons.Count - 1;
            }

            if (_browserButtons.Count > 0 && _currentButtonIndex >= 0)
            {
                AnnounceCurrentButton();
            }
            else if (_browserButtons.Count > 0)
            {
                _currentButtonIndex = 0;
                AnnounceCurrentButton();
            }
            else
            {
                _announcer.Announce(Strings.NoButtonsAvailable, AnnouncementPriority.Normal);
            }
        }

        #endregion

        #region AssignDamage Browser

        /// <summary>
        /// Caches state for the AssignDamage browser: browser ref, spinner map, total damage.
        /// </summary>
        private void CacheAssignDamageState()
        {
            try
            {
                // Find GameManager → BrowserManager → CurrentBrowser
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
                    MelonLogger.Msg("[BrowserNavigator] AssignDamage: GameManager not found");
                    return;
                }

                var bmProp = gameManager.GetType().GetProperty("BrowserManager", ReflFlags);
                var browserManager = bmProp?.GetValue(gameManager);
                if (browserManager == null)
                {
                    MelonLogger.Msg("[BrowserNavigator] AssignDamage: BrowserManager not found");
                    return;
                }

                var cbProp = browserManager.GetType().GetProperty("CurrentBrowser", ReflFlags);
                var currentBrowser = cbProp?.GetValue(browserManager);
                if (currentBrowser == null || !currentBrowser.GetType().Name.Contains("AssignDamage"))
                {
                    MelonLogger.Msg($"[BrowserNavigator] AssignDamage: CurrentBrowser is {currentBrowser?.GetType().Name ?? "null"}");
                    return;
                }

                _assignDamageBrowserRef = currentBrowser;
                MelonLogger.Msg($"[BrowserNavigator] AssignDamage: Found browser {currentBrowser.GetType().Name}");

                // Cache _idToSpinnerMap
                var spinnerField = currentBrowser.GetType().GetField("_idToSpinnerMap", ReflFlags);
                if (spinnerField != null)
                {
                    _spinnerMap = spinnerField.GetValue(currentBrowser) as System.Collections.IDictionary;
                    MelonLogger.Msg($"[BrowserNavigator] AssignDamage: Spinner map has {_spinnerMap?.Count ?? 0} entries");
                }
                else
                {
                    MelonLogger.Msg("[BrowserNavigator] AssignDamage: _idToSpinnerMap field not found");
                }

                // TotalDamage is cached lazily via EnsureTotalDamageCached()
                // because CurrentInteraction may not be set yet at browser open time
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] AssignDamage cache error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles input specific to the AssignDamage browser.
        /// Up/Down adjusts spinner, Left/Right navigates blockers.
        /// </summary>
        private bool HandleAssignDamageInput()
        {
            // Up arrow: increase damage on current blocker
            // Always consume to prevent EventSystem focus leak
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                AdjustDamageSpinner(true);
                return true;
            }

            // Down arrow: decrease damage on current blocker
            // Always consume to prevent EventSystem focus leak
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                AdjustDamageSpinner(false);
                return true;
            }

            // Enter: consume without action (cards aren't toggleable in damage assignment)
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                return true;
            }

            // Space: submit via DoneAction on browser
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SubmitAssignDamage();
                return true;
            }

            // Backspace: undo via UndoAction on browser
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                UndoAssignDamage();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the SpinnerAnimated for the currently focused card by InstanceId.
        /// </summary>
        /// <summary>
        /// Lazily caches TotalDamage from the workflow's MtgDamageAssigner.
        /// Called on first use because CurrentInteraction is null at browser open time.
        /// </summary>
        private void EnsureTotalDamageCached()
        {
            if (_totalDamageCached) return;

            try
            {
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
                    MelonLogger.Msg("[BrowserNavigator] EnsureTotalDamageCached: GameManager not found");
                    return;
                }

                var wcProp = gameManager.GetType().GetProperty("WorkflowController", ReflFlags);
                var workflowController = wcProp?.GetValue(gameManager);
                if (workflowController == null)
                {
                    MelonLogger.Msg($"[BrowserNavigator] EnsureTotalDamageCached: WorkflowController null (prop found: {wcProp != null})");
                    return;
                }

                var cwProp = workflowController.GetType().GetProperty("CurrentWorkflow", ReflFlags);
                var interaction = cwProp?.GetValue(workflowController);
                if (interaction == null)
                {
                    MelonLogger.Msg($"[BrowserNavigator] EnsureTotalDamageCached: CurrentWorkflow null (prop found: {cwProp != null}), WC type: {workflowController.GetType().Name}");
                    return;
                }

                MelonLogger.Msg($"[BrowserNavigator] EnsureTotalDamageCached: Interaction type: {interaction.GetType().Name}");

                // Walk type hierarchy to find _damageAssigner (declared on AssignDamageWorkflow, not base)
                FieldInfo daField = null;
                var searchType = interaction.GetType();
                while (searchType != null && daField == null)
                {
                    daField = searchType.GetField("_damageAssigner", ReflFlags);
                    searchType = searchType.BaseType;
                }
                if (daField == null)
                {
                    MelonLogger.Msg("[BrowserNavigator] EnsureTotalDamageCached: _damageAssigner field not found");
                    return;
                }

                var damageAssigner = daField.GetValue(interaction);
                if (damageAssigner == null)
                {
                    MelonLogger.Msg("[BrowserNavigator] EnsureTotalDamageCached: _damageAssigner value is null");
                    return;
                }

                MelonLogger.Msg($"[BrowserNavigator] EnsureTotalDamageCached: damageAssigner type: {damageAssigner.GetType().Name}");

                // TotalDamage is a public readonly field on the MtgDamageAssigner struct
                var tdField = damageAssigner.GetType().GetField("TotalDamage", BindingFlags.Public | BindingFlags.Instance);
                if (tdField != null)
                {
                    _totalDamage = (uint)tdField.GetValue(damageAssigner);
                    _totalDamageCached = true;
                    MelonLogger.Msg($"[BrowserNavigator] AssignDamage: TotalDamage = {_totalDamage}");
                }
                else
                {
                    MelonLogger.Msg("[BrowserNavigator] EnsureTotalDamageCached: TotalDamage field not found");
                }

                // Read assigner queue counts: _handledAssigners (List) + _unhandledAssigners (Queue)
                // Current = handledCount + 1, Total = handledCount + unhandledCount + 1
                var iType = interaction.GetType();
                var handledField = iType.GetField("_handledAssigners", ReflFlags);
                var unhandledField = iType.GetField("_unhandledAssigners", ReflFlags);
                if (handledField != null && unhandledField != null)
                {
                    var handled = handledField.GetValue(interaction) as ICollection;
                    var unhandled = unhandledField.GetValue(interaction) as ICollection;
                    int handledCount = handled?.Count ?? 0;
                    int unhandledCount = unhandled?.Count ?? 0;
                    _assignerIndex = handledCount + 1;
                    _assignerTotal = handledCount + unhandledCount + 1;
                    MelonLogger.Msg($"[BrowserNavigator] AssignDamage: Assigner {_assignerIndex} of {_assignerTotal}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] EnsureTotalDamageCached error: {ex.Message}");
            }
        }

        /// <summary>
        /// Submits the damage assignment by invoking DoneAction on the browser.
        /// Note: The generic SimulatePointerClick path on the SubmitButton may also work
        /// (it did before our AssignDamage changes), but DoneAction is the direct event
        /// the game wires to OnButtonCallback("DoneButton"), so we invoke it explicitly.
        /// If this ever breaks, try reverting to SimulatePointerClick on SubmitButton.
        /// </summary>
        private void SubmitAssignDamage()
        {
            if (_assignDamageBrowserRef == null)
            {
                MelonLogger.Msg("[BrowserNavigator] AssignDamage: No browser ref for submit");
                return;
            }

            try
            {
                var browserType = _assignDamageBrowserRef.GetType();
                var doneField = browserType.GetField("DoneAction", ReflFlags);
                if (doneField != null)
                {
                    var doneAction = doneField.GetValue(_assignDamageBrowserRef) as Action;
                    if (doneAction != null)
                    {
                        doneAction.Invoke();
                        _announcer.Announce(Strings.Confirmed, AnnouncementPriority.Normal);
                        MelonLogger.Msg("[BrowserNavigator] AssignDamage: Invoked DoneAction");
                        return;
                    }
                }

                // Fallback: try invoking OnButtonCallback("DoneButton") directly
                var callbackMethod = browserType.GetMethod("OnButtonCallback", ReflFlags);
                if (callbackMethod != null)
                {
                    callbackMethod.Invoke(_assignDamageBrowserRef, new object[] { "DoneButton" });
                    _announcer.Announce(Strings.Confirmed, AnnouncementPriority.Normal);
                    MelonLogger.Msg("[BrowserNavigator] AssignDamage: Called OnButtonCallback(DoneButton)");
                    return;
                }

                MelonLogger.Msg("[BrowserNavigator] AssignDamage: Could not find DoneAction or OnButtonCallback");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] SubmitAssignDamage error: {ex.Message}");
            }
        }

        /// <summary>
        /// Undoes the last damage assignment action via UndoAction on the browser.
        /// </summary>
        private void UndoAssignDamage()
        {
            if (_assignDamageBrowserRef == null)
            {
                MelonLogger.Msg("[BrowserNavigator] AssignDamage: No browser ref for undo");
                return;
            }

            try
            {
                var browserType = _assignDamageBrowserRef.GetType();
                var undoField = browserType.GetField("UndoAction", ReflFlags);
                if (undoField != null)
                {
                    var undoAction = undoField.GetValue(_assignDamageBrowserRef) as Action;
                    if (undoAction != null)
                    {
                        undoAction.Invoke();
                        _announcer.Announce(Strings.Cancelled, AnnouncementPriority.Normal);
                        MelonLogger.Msg("[BrowserNavigator] AssignDamage: Invoked UndoAction");
                        return;
                    }
                }

                MelonLogger.Msg("[BrowserNavigator] AssignDamage: UndoAction not available");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] UndoAssignDamage error: {ex.Message}");
            }
        }

        private object GetSpinnerForCurrentCard()
        {
            if (_spinnerMap == null || _currentCardIndex < 0 || _currentCardIndex >= _browserCards.Count)
                return null;

            var card = _browserCards[_currentCardIndex];
            uint instanceId = GetCardInstanceId(card);
            if (instanceId == 0) return null;

            if (_spinnerMap.Contains(instanceId))
                return _spinnerMap[instanceId];

            return null;
        }

        /// <summary>
        /// Extracts InstanceId from a card's CDC component.
        /// </summary>
        private uint GetCardInstanceId(GameObject card)
        {
            if (card == null) return 0;

            try
            {
                foreach (var mb in card.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    var type = mb.GetType();
                    // DuelScene_CDC or similar CDC types have InstanceId
                    if (type.Name.Contains("CDC"))
                    {
                        var idProp = type.GetProperty("InstanceId", ReflFlags);
                        if (idProp != null)
                            return (uint)idProp.GetValue(mb);

                        // Try via Model.Instance.InstanceId
                        var modelProp = type.GetProperty("Model", ReflFlags);
                        if (modelProp != null)
                        {
                            var model = modelProp.GetValue(mb);
                            if (model != null)
                            {
                                var instProp = model.GetType().GetProperty("Instance", ReflFlags);
                                var instance = instProp?.GetValue(model);
                                if (instance != null)
                                {
                                    var iidProp = instance.GetType().GetProperty("InstanceId", ReflFlags);
                                    if (iidProp != null)
                                        return (uint)iidProp.GetValue(instance);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] GetCardInstanceId error: {ex.Message}");
            }

            return 0;
        }

        /// <summary>
        /// Clicks the spinner up/down button and announces the new value.
        /// </summary>
        private void AdjustDamageSpinner(bool increase)
        {
            EnsureTotalDamageCached();

            var spinner = GetSpinnerForCurrentCard();
            if (spinner == null)
            {
                // No spinner = attacker card, not a blocker
                return;
            }

            try
            {
                var spinnerType = spinner.GetType();

                // Click _upButton or _downButton
                string buttonFieldName = increase ? "_upButton" : "_downButton";
                var buttonField = spinnerType.GetField(buttonFieldName, ReflFlags);
                if (buttonField == null)
                {
                    MelonLogger.Msg($"[BrowserNavigator] AssignDamage: {buttonFieldName} field not found on {spinnerType.Name}");
                    return;
                }

                var button = buttonField.GetValue(spinner) as Button;
                if (button == null)
                {
                    MelonLogger.Msg($"[BrowserNavigator] AssignDamage: {buttonFieldName} is null");
                    return;
                }

                button.onClick.Invoke();

                // Read new value from spinner
                var valueProp = spinnerType.GetProperty("Value", ReflFlags);
                int newValue = 0;
                if (valueProp != null)
                {
                    newValue = (int)valueProp.GetValue(spinner);
                }

                // Announce: "X of Total assigned"
                string announcement = Strings.DamageAssigned(newValue, (int)_totalDamage);

                // Check lethal
                if (IsSpinnerLethal(spinner))
                {
                    announcement = $"{Strings.DamageAssignLethal}, {announcement}";
                }

                _announcer.Announce(announcement, AnnouncementPriority.High);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] AdjustDamageSpinner error: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if the spinner's value text is gold (lethal damage reached).
        /// Lethal color: Color32(254, 176, 0, 255).
        /// </summary>
        private bool IsSpinnerLethal(object spinner)
        {
            try
            {
                var spinnerType = spinner.GetType();
                var textField = spinnerType.GetField("_valueText", ReflFlags);
                if (textField == null) return false;

                var textComponent = textField.GetValue(spinner);
                if (textComponent == null) return false;

                var colorProp = textComponent.GetType().GetProperty("color", ReflFlags);
                if (colorProp == null) return false;

                var color = (Color)colorProp.GetValue(textComponent);
                // Compare to lethal gold: approximately (254/255, 176/255, 0/255, 1)
                return color.r > 0.9f && color.g > 0.6f && color.g < 0.8f && color.b < 0.1f;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Announces a card in AssignDamage mode with name, P/T, lethal status, position.
        /// Does NOT call PrepareForCard so CardInfoNavigator stays inactive
        /// and Up/Down are free for spinner control.
        /// </summary>
        private void AnnounceAssignDamageCard(GameObject card)
        {
            var info = CardDetector.ExtractCardInfo(card);
            string cardName = info.Name ?? "Unknown card";

            var parts = new List<string>();
            parts.Add(cardName);

            // Add P/T
            if (!string.IsNullOrEmpty(info.PowerToughness))
            {
                parts.Add(info.PowerToughness);
            }

            // Check lethal state via spinner
            var spinner = GetSpinnerForCurrentCard();
            if (spinner != null && IsSpinnerLethal(spinner))
            {
                parts.Add(Strings.DamageAssignLethal);
            }

            // Add position
            if (_browserCards.Count > 1)
            {
                parts.Add($"{_currentCardIndex + 1} of {_browserCards.Count}");
            }

            _announcer.Announce(string.Join(", ", parts), AnnouncementPriority.High);
        }

        /// <summary>
        /// Gets the entry announcement for the AssignDamage browser.
        /// "Assign damage. [AttackerName], [Power] damage. [N] blockers"
        /// </summary>
        private string GetAssignDamageEntryAnnouncement(int cardCount, string fallbackName)
        {
            EnsureTotalDamageCached();

            if (_assignDamageBrowserRef == null)
                return Strings.DamageAssignEntry(fallbackName, (int)_totalDamage, cardCount);

            try
            {
                var browserType = _assignDamageBrowserRef.GetType();

                // Get _layout from the browser
                var layoutField = browserType.GetField("_layout", ReflFlags);
                var layout = layoutField?.GetValue(_assignDamageBrowserRef);
                if (layout == null)
                {
                    MelonLogger.Msg("[BrowserNavigator] AssignDamage: _layout not found");
                    return Strings.DamageAssignEntry(fallbackName, (int)_totalDamage, cardCount);
                }

                var layoutType = layout.GetType();

                // Get _attacker (DuelScene_CDC)
                var attackerField = layoutType.GetField("_attacker", ReflFlags);
                object attacker = attackerField?.GetValue(layout);
                string attackerName = "Attacker";
                int power = (int)_totalDamage;

                if (attacker != null)
                {
                    // Get attacker's GameObject and extract name
                    var attackerMb = attacker as MonoBehaviour;
                    if (attackerMb != null)
                    {
                        var attackerInfo = CardDetector.ExtractCardInfo(attackerMb.gameObject);
                        if (!string.IsNullOrEmpty(attackerInfo.Name))
                            attackerName = attackerInfo.Name;
                    }
                }

                // Get _blockers list for count
                var blockersField = layoutType.GetField("_blockers", ReflFlags);
                if (blockersField != null)
                {
                    var blockersList = blockersField.GetValue(layout) as IList;
                    if (blockersList != null)
                    {
                        cardCount = blockersList.Count;
                    }
                }

                string entry = Strings.DamageAssignEntry(attackerName, power, cardCount);
                if (_assignerTotal > 1)
                {
                    entry += $". {_assignerIndex} of {_assignerTotal}";
                }
                return entry;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] AssignDamage entry announcement error: {ex.Message}");
                return Strings.DamageAssignEntry(fallbackName, (int)_totalDamage, cardCount);
            }
        }

        #endregion

        #region External Access

        /// <summary>
        /// Gets the currently focused card (for external use).
        /// </summary>
        public GameObject GetCurrentCard()
        {
            // Check zone navigator first
            if (_browserInfo?.IsZoneBased == true && _zoneNavigator.CurrentCard != null)
            {
                return _zoneNavigator.CurrentCard;
            }

            // Fall back to generic navigation
            if (_currentCardIndex < 0 || _currentCardIndex >= _browserCards.Count)
                return null;
            return _browserCards[_currentCardIndex];
        }

        #endregion
    }
}
