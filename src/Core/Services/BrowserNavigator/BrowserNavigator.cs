using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for browser UIs in the duel scene.
    /// Orchestrates browser detection and navigation:
    /// - Uses BrowserDetector for finding active browsers
    /// - Delegates zone-based navigation (Scry/London) to BrowserZoneNavigator
    /// - Handles generic browsers (YesNo, Dungeon, etc.) directly
    /// </summary>
    public partial class BrowserNavigator
    {
        private readonly IAnnouncementService _announcer;
        private readonly BrowserZoneNavigator _zoneNavigator;
        private readonly ZoneNavigator _duelZoneNavigator;

        // Browser state (static so EventSystemPatch can block Submit while browser is active)
        private static bool _isActive;
        private bool _hasAnnouncedEntry;
        private float _announceSettleTimer; // Delay announcement for scaffold-less browsers to avoid transient states
        private BrowserInfo _browserInfo;

        // Generic browser navigation (non-zone browsers)
        private List<GameObject> _browserCards = new List<GameObject>();
        private List<GameObject> _browserButtons = new List<GameObject>();
        private int _currentCardIndex = -1;
        private int _currentButtonIndex = -1;

        // ViewDismiss auto-dismiss tracking
        private bool _viewDismissDismissed;

        // Post-confirm rescan: force re-entry when scaffold is reused for a new interaction
        private bool _pendingRescan;

        // Choice-list browser state (LargeScrollList/SelectNCounters with text choices)
        private bool _isChoiceList;

        // Highlight-filtered Tab: skip non-selectable cards in selection browsers
        private bool _isHighlightFilteredBrowser;

        // Zone name constant
        private const string ZoneLocalHand = "LocalHand";

        public BrowserNavigator(IAnnouncementService announcer, ZoneNavigator duelZoneNavigator)
        {
            _announcer = announcer;
            _zoneNavigator = new BrowserZoneNavigator(announcer);
            _duelZoneNavigator = duelZoneNavigator;
        }

        #region Public Properties

        public static bool IsActive => _isActive;
        public string ActiveBrowserType => _browserInfo?.BrowserType;
        public BrowserZoneNavigator ZoneNavigator => _zoneNavigator;

        /// <summary>Return the tutorial hint for the current browser type (used by Ctrl+F1 via DuelNavigator)</summary>
        public string GetTutorialHint()
        {
            return LocaleManager.Instance.Get(GetBrowserHintKey());
        }

        /// <summary>Maps the current browser type to a locale key for its specific hint text.</summary>
        private string GetBrowserHintKey()
        {
            if (_browserInfo == null) return "BrowserHint";

            string type = _browserInfo.BrowserType;

            // Zone-based browsers
            if (type != null && type.ToLower().Contains("surveil"))
                return "BrowserHint_Surveil";
            if (type == "Scry") return "BrowserHint_Scry";
            if (type == "ReadAhead") return "BrowserHint_ReadAhead";
            if (type == "SplitCards" || type == "SplitGroup")
                return "BrowserHint_SplitCards";
            if (_browserInfo.IsLondon) return "BrowserHint_London";
            if (_browserInfo.IsMulligan) return "BrowserHint_Mulligan";

            // Special browsers
            if (_isAssignDamage) return "BrowserHint_AssignDamage";
            if (_isKeywordSelection) return "BrowserHint_KeywordSelection";
            if (_isSelectGroup) return "BrowserHint_SelectGroup";
            if (_isMultiZone) return "BrowserHint_SelectCardsMultiZone";
            if (type == "RepeatSelection") return "BrowserHint_RepeatSelection";
            if (type == "SelectMana") return "BrowserHint_SelectMana";
            if (type == "Informational") return "BrowserHint_Informational";
            if (type == "SelectNCounters") return "BrowserHint_SelectNCounters";
            if (_browserInfo.IsWorkflow) return "BrowserHint_Workflow";

            // OrderCards browsers (library reorder / trigger reorder)
            if (_isOrderCards) return "BrowserHint_OrderCards";

            // SelectCards and similar card selection browsers
            if (type == "SelectCards" || type == "LibrarySideboard")
                return "BrowserHint_SelectCards";

            // OptionalAction (shocklands, etc.): direct-choice, Space = Enter
            if (_browserInfo.IsOptionalAction) return "BrowserHint_OptionalAction";

            // Simple button browsers (YesNo, Dungeon, Optional*, Mutate, LargeScrollList)
            return "BrowserHint";
        }

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
                    Log.Msg("BrowserNavigator", $"Browser type changed: {_browserInfo?.BrowserType} -> {browserInfo.BrowserType}");
                    ExitBrowserMode();
                    EnterBrowserMode(browserInfo);
                }
                // Re-enter if same type but different scaffold instance
                else if (browserInfo.BrowserGameObject != _browserInfo?.BrowserGameObject)
                {
                    Log.Msg("BrowserNavigator", $"Browser scaffold instance changed for: {browserInfo.BrowserType}");
                    ExitBrowserMode();
                    EnterBrowserMode(browserInfo);
                }
                // Re-enter after confirm when scaffold is reused with new content
                else if (_pendingRescan)
                {
                    _pendingRescan = false;
                    // Preserve mulligan count across rescan so London phase keeps correct state
                    int savedMulliganCount = _zoneNavigator.MulliganCount;
                    Log.Msg("BrowserNavigator", $"Re-scanning browser after confirm: {browserInfo.BrowserType}");
                    ExitBrowserMode();
                    EnterBrowserMode(browserInfo);
                    if (savedMulliganCount > 0 && BrowserDetector.IsLondonBrowser(browserInfo.BrowserType))
                    {
                        _zoneNavigator.RestoreMulliganCount(savedMulliganCount);
                    }
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
            // Scaffold-detected browsers have stable UI — announce immediately.
            // Generic CardBrowserCardHolder may be a transient pre-mulligan state — settle first.
            _announceSettleTimer = browserInfo.BrowserType == T.CardBrowserCardHolder ? 0.5f : 0f;
            _currentCardIndex = -1;
            _currentButtonIndex = -1;
            _browserCards.Clear();
            _browserButtons.Clear();

            Log.Msg("BrowserNavigator", $"Entering browser: {browserInfo.BrowserType}");

            // Claim Browser zone ownership so other navigators yield Left/Right/Enter
            _duelZoneNavigator?.SetCurrentZone(ZoneType.Browser, "BrowserNavigator");

            // Activate zone navigator for zone-based browsers
            if (browserInfo.IsZoneBased)
            {
                _zoneNavigator.Activate(browserInfo);
            }

            // Detect multi-zone browser
            _isMultiZone = browserInfo.BrowserType == "SelectCardsMultiZone";

            // Selection browsers: Tab skips non-selectable cards (highlight filtering)
            _isHighlightFilteredBrowser = browserInfo.BrowserType == "SelectCards"
                || browserInfo.BrowserType == "SelectCardsMultiZone";

            // Detect AssignDamage browser
            if (browserInfo.BrowserType == "AssignDamage")
            {
                _isAssignDamage = true;
                CacheAssignDamageState();
            }

            // Detect SelectGroup browser (Fact or Fiction pile selection)
            if (browserInfo.BrowserType == "SelectGroup")
            {
                _isSelectGroup = true;
                CacheSelectGroupState();
            }

            // Detect KeywordSelection browser (creature type picker)
            if (browserInfo.BrowserType == "KeywordSelection")
            {
                _isKeywordSelection = true;
                CacheKeywordFilterState();
            }

            // Detect OrderCards browser (library card reorder / trigger reorder)
            if (browserInfo.BrowserType == "OrderCards" || browserInfo.BrowserType == "TriggerOrderCards")
            {
                _isOrderCards = true;
                _orderGrabbedIndex = -1;
            }

            // Discover elements
            DiscoverBrowserElements();
        }

        /// <summary>
        /// Exits browser mode.
        /// </summary>
        private void ExitBrowserMode()
        {
            Log.Msg("BrowserNavigator", $"Exiting browser: {_browserInfo?.BrowserType}");

            // Deactivate zone navigator
            if (_browserInfo?.IsZoneBased == true)
            {
                _zoneNavigator.Deactivate();
            }

            _isActive = false;
            _browserInfo = null;
            _hasAnnouncedEntry = false;
            _pendingRescan = false;
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

            // Clear SelectGroup state
            _isSelectGroup = false;
            _selectGroupBrowserRef = null;
            _pile1CDCs.Clear();
            _pile2CDCs.Clear();
            _selectGroupCardMap.Clear();

            // Clear choice-list state
            _isChoiceList = false;

            // Clear KeywordSelection state
            _isKeywordSelection = false;
            _keywordFilterRef = null;
            _currentKeywordIndex = -1;
            _keywordLetterSearch.Clear();

            // Clear highlight filter state
            _isHighlightFilteredBrowser = false;
            _browserConfirmWarning = false;
            _browserConfirmWaitRelease = false;

            // Clear OrderCards state
            _isOrderCards = false;
            _orderGrabbedIndex = -1;

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
            Log.Msg("BrowserNavigator", $"Auto-dismissing ViewDismiss card preview");

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
                        Log.Msg("BrowserNavigator", $"Activated dismiss button '{name}': {result.Success} ({result.Type})");
                        BrowserDetector.InvalidateCache();
                        return;
                    }
                }
            }

            Log.Msg("BrowserNavigator", $"No dismiss button found in ViewDismiss scaffold");
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

            // Announce browser state once settled
            if (!_hasAnnouncedEntry)
            {
                if (_announceSettleTimer > 0f)
                {
                    _announceSettleTimer -= Time.deltaTime;
                    return true; // Consume input while settling
                }
                AnnounceBrowserState();
                _hasAnnouncedEntry = true;
            }

            // Track Space release for browser confirm guard (double-press in selection browsers)
            if (_browserConfirmWaitRelease && !Input.GetKey(KeyCode.Space))
                _browserConfirmWaitRelease = false;

            // AssignDamage browser: Up/Down controls spinner, Left/Right navigates blockers
            if (_isAssignDamage)
            {
                if (HandleAssignDamageInput())
                    return true;
            }

            // KeywordSelection browser: custom keyword navigation
            if (_isKeywordSelection)
            {
                if (HandleKeywordSelectionInput())
                    return true;
            }

            // Zone-based browsers: delegate C/D/arrows/Enter to zone navigator
            if (_browserInfo.IsZoneBased)
            {
                // C/D always reclaim Browser ownership (browser zone hotkeys)
                if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.D))
                {
                    _duelZoneNavigator?.SetCurrentZone(ZoneType.Browser, "BrowserNavigator");
                }

                // Only delegate arrows/Enter when Browser zone owns focus
                bool browserOwnsForZone = _duelZoneNavigator == null || _duelZoneNavigator.CurrentZone == ZoneType.Browser;
                if (browserOwnsForZone && _zoneNavigator.HandleInput())
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
                            int firstIdx = FindFirstSelectableCard();
                            _currentCardIndex = firstIdx; // -1 if none selectable; guard in AnnounceCurrentCard handles it
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
                            int lastIdx = FindLastSelectableCard();
                            _currentCardIndex = lastIdx; // -1 if none selectable; guard in AnnounceCurrentCard handles it
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

                // Reclaim Browser zone ownership (user may have navigated to graveyard/battlefield)
                _duelZoneNavigator?.SetCurrentZone(ZoneType.Browser, "BrowserNavigator");

                // OrderCards: Tab also uses clamped navigation (same as Left/Right)
                if (_isOrderCards && _browserCards.Count > 0)
                {
                    ClearEventSystemSelection();
                    if (shift)
                    {
                        if (_currentCardIndex > 0) { _currentCardIndex--; AnnounceCurrentCard(); }
                        else _announcer.AnnounceInterruptVerbose(Strings.BeginningOfZone);
                    }
                    else
                    {
                        if (_currentCardIndex < _browserCards.Count - 1) { _currentCardIndex++; AnnounceCurrentCard(); }
                        else _announcer.AnnounceInterruptVerbose(Strings.EndOfZone);
                    }
                    return true;
                }

                // Multi-zone: Tab wraps back to zone selector at boundaries
                if (_isMultiZone && _zoneButtons.Count > 0)
                {
                    if (shift)
                    {
                        // Shift+Tab backwards: cards/buttons → zone selector
                        if (_currentButtonIndex > 0)
                        {
                            NavigateToPreviousButton();
                        }
                        else if (_currentButtonIndex == 0 && _browserCards.Count > 0)
                        {
                            // From first button → last selectable card
                            _currentButtonIndex = -1;
                            int lastIdx = FindLastSelectableCard();
                            if (lastIdx >= 0)
                            {
                                _currentCardIndex = lastIdx;
                                AnnounceCurrentCard();
                            }
                            else
                            {
                                EnterZoneSelector();
                                _currentCardIndex = -1;
                                AnnounceMultiZoneSelector();
                            }
                        }
                        else if (_currentButtonIndex == 0)
                        {
                            // No cards → zone selector
                            EnterZoneSelector();
                            _currentButtonIndex = -1;
                            AnnounceMultiZoneSelector();
                        }
                        else if (_currentCardIndex >= 0)
                        {
                            // On a card → try previous selectable
                            int prevIdx = FindPreviousSelectableCard(_currentCardIndex);
                            if (prevIdx >= 0)
                            {
                                _currentCardIndex = prevIdx;
                                AnnounceCurrentCard();
                            }
                            else
                            {
                                // At first selectable card → zone selector
                                EnterZoneSelector();
                                _currentCardIndex = -1;
                                AnnounceMultiZoneSelector();
                            }
                        }
                        else
                        {
                            EnterZoneSelector();
                            _currentCardIndex = -1;
                            _currentButtonIndex = -1;
                            AnnounceMultiZoneSelector();
                        }
                        return true;
                    }
                    else
                    {
                        // Tab forward: selectable cards → buttons → zone selector
                        if (_currentCardIndex >= 0)
                        {
                            int nextIdx = FindNextSelectableCard(_currentCardIndex);
                            if (nextIdx >= 0)
                            {
                                _currentCardIndex = nextIdx;
                                AnnounceCurrentCard();
                            }
                            else if (_browserButtons.Count > 0)
                            {
                                _currentCardIndex = -1;
                                _currentButtonIndex = 0;
                                AnnounceCurrentButton();
                            }
                            else
                            {
                                EnterZoneSelector();
                                _currentCardIndex = -1;
                                _currentButtonIndex = -1;
                                AnnounceMultiZoneSelector();
                            }
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

                // SelectGroup: Tab cycles buttons only (cards are Left/Right)
                if (_isSelectGroup && _browserButtons.Count > 0)
                {
                    if (shift) NavigateToPreviousButton();
                    else NavigateToNextButton();
                }
                // OptionalAction: unified cycle through cards → choice buttons → wrap
                else if (_browserInfo.IsOptionalAction && _browserCards.Count > 0 && _browserButtons.Count > 0)
                {
                    if (shift) NavigateToPreviousItem();
                    else NavigateToNextItem();
                }
                else if (_browserCards.Count > 0)
                {
                    if (shift) TabToPreviousCard();
                    else TabToNextCard();
                }
                else if (_browserButtons.Count > 0)
                {
                    if (shift) NavigateToPreviousButton();
                    else NavigateToNextButton();
                }
                return true;
            }

            // Left/Right arrows - card/button navigation (for non-zone browsers)
            // Only when Browser zone owns focus (not when user navigated to graveyard/battlefield)
            bool browserOwnsZone = _duelZoneNavigator == null || _duelZoneNavigator.CurrentZone == ZoneType.Browser;
            if (browserOwnsZone && (!_browserInfo.IsZoneBased || _zoneNavigator.CurrentZone == BrowserZoneType.None))
            {
                // OrderCards: clamp navigation (no wrapping) with verbose boundary announcements
                if (_isOrderCards && _browserCards.Count > 0)
                {
                    if (Input.GetKeyDown(KeyCode.LeftArrow))
                    {
                        ClearEventSystemSelection();
                        if (_currentCardIndex > 0)
                        {
                            _currentCardIndex--;
                            AnnounceCurrentCard();
                        }
                        else
                        {
                            _announcer.AnnounceInterruptVerbose(Strings.BeginningOfZone);
                        }
                        return true;
                    }
                    if (Input.GetKeyDown(KeyCode.RightArrow))
                    {
                        ClearEventSystemSelection();
                        if (_currentCardIndex < _browserCards.Count - 1)
                        {
                            _currentCardIndex++;
                            AnnounceCurrentCard();
                        }
                        else
                        {
                            _announcer.AnnounceInterruptVerbose(Strings.EndOfZone);
                        }
                        return true;
                    }
                }

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
            // Only when Browser zone owns focus
            if (browserOwnsZone && !_isAssignDamage && (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)))
            {
                if (_browserCards.Count > 0 && _currentCardIndex >= 0)
                {
                    var cardNav = AccessibleArenaMod.Instance?.CardNavigator;
                    if (cardNav != null && cardNav.IsActive)
                    {
                        return false; // Let CardInfoNavigator handle it
                    }
                }
                // Consume Up/Down when browser has no cards (e.g. Informational browser
                // during coin flip / waiting) to prevent BaseNavigator from navigating
                // internal UI elements like 16x9 prompt buttons
                return _browserCards.Count == 0;
            }

            // Enter - activate current card or button
            // Only when Browser zone owns focus
            if (browserOwnsZone && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                // OrderCards: pick up or place card
                if (_isOrderCards)
                {
                    HandleOrderCardsActivation();
                    return true;
                }

                // Zone navigator handles Enter only when actually navigating in a zone (with selected card)
                if (_browserInfo.IsZoneBased && _zoneNavigator.CurrentZone != BrowserZoneType.None
                    && _zoneNavigator.CurrentCardIndex >= 0)
                {
                    return false; // Already handled by zone navigator
                }

                // SelectGroup: Enter activates the focused pile button (choosing that pile)
                // Must check before generic card activation since _currentCardIndex stays >= 0
                // after Tab moves focus to buttons
                if (_isSelectGroup && _currentButtonIndex >= 0 && _browserButtons.Count > 0)
                {
                    ActivateCurrentButton();
                }
                // Use generic activation for non-zone browsers or when zone has no selected card
                else if (_browserCards.Count > 0 && _currentCardIndex >= 0)
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
                // OrderCards: if a card is grabbed, place it instead of submitting
                if (_isOrderCards && _orderGrabbedIndex >= 0)
                {
                    PlaceOrderCard();
                    return true;
                }

                // MultiZone only: require double-press to prevent accidental decline
                // (regular SelectCards browsers like "choose a creature" are fine with single press)
                if (_isMultiZone)
                {
                    if (_browserConfirmWaitRelease)
                        return true; // Still held from first press — ignore

                    if (!_browserConfirmWarning)
                    {
                        // First press: warn and block
                        _browserConfirmWarning = true;
                        _browserConfirmWaitRelease = true;
                        _announcer.Announce(Strings.BrowserConfirmGuard, AnnouncementPriority.High);
                        return true;
                    }
                    // Second press after release: reset guard and proceed to submit
                    _browserConfirmWarning = false;
                }

                ClickConfirmButton();
                return true;
            }

            // Backspace - cancel
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                // OrderCards: if a card is grabbed, cancel the grab
                if (_isOrderCards && _orderGrabbedIndex >= 0)
                {
                    _orderGrabbedIndex = -1;
                    _announcer.Announce(Strings.OrderCardsCancelled, AnnouncementPriority.Normal);
                    return true;
                }
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
                        Log.Msg("BrowserNavigator", $"Workflow button: '{buttonText}'");
                    }
                }
                Log.Msg("BrowserNavigator", $"Found {_browserButtons.Count} workflow action buttons");
                return;
            }

            // SelectGroup: discover cards from cached pile CDCs (includes face-down cards)
            if (_isSelectGroup && (_pile1CDCs.Count > 0 || _pile2CDCs.Count > 0))
            {
                DiscoverSelectGroupCards();
                // Discover buttons in scaffold
                if (_browserInfo.BrowserGameObject != null)
                    FindButtonsInContainer(_browserInfo.BrowserGameObject);
                if (_browserButtons.Count == 0)
                    DiscoverPromptButtons();
                // Filter invisible buttons
                _browserButtons.RemoveAll(b =>
                    !UIElementClassifier.IsVisibleViaCanvasGroup(b) &&
                    !UITextExtractor.HasActualText(b));
                Log.Msg("BrowserNavigator", $"SelectGroup: {_pile1CDCs.Count} pile 1, {_pile2CDCs.Count} pile 2, {_browserCards.Count} cards, {_browserButtons.Count} buttons");
                return;
            }

            // KeywordSelection: keywords are in InfiniteScroll, not regular cards.
            // Only discover scaffold buttons (Show All, confirm, etc.).
            if (_isKeywordSelection && _keywordFilterRef != null)
            {
                if (_browserInfo.BrowserGameObject != null)
                    FindButtonsInContainer(_browserInfo.BrowserGameObject);
                if (_browserButtons.Count == 0)
                    DiscoverPromptButtons();
                int kwCount = GetKeywordCount();
                Log.Msg("BrowserNavigator", $"KeywordSelection: {kwCount} keywords, {_browserButtons.Count} buttons");
                return;
            }

            // Discover based on browser type
            if (_browserInfo.IsMulligan)
            {
                DiscoverMulliganCards();
            }

            DiscoverCardsInHolders();

            // OrderCards: filter out placeholder cards (InstanceId == 0) used as library boundary markers,
            // then reverse so that position 1 (leftmost) = top of stack = resolves first.
            // The game's CardViews[0] is bottom of stack (resolves last); we invert for LIFO display.
            if (_isOrderCards)
            {
                FilterPlaceholderCards();
                _browserCards.Reverse();
            }

            // Scope button discovery to the scaffold when available.
            // Global search picks up unrelated duel UI buttons (PromptButton_Primary/Secondary)
            // that show phase info like "Opponent's turn" or "Cancel attacks".
            if (_browserInfo.BrowserGameObject != null)
            {
                FindButtonsInContainer(_browserInfo.BrowserGameObject);
            }

            // For scaffold browsers with scrollable option lists, discover clickable text
            // choices that don't match standard ButtonPatterns (e.g. "Eine Karte abwerfen",
            // color choices like "Blau"/"Schwarz" from SelectColorWorkflow).
            // SelectNCounters scaffold is reused for both counter and color selection.
            if (_browserCards.Count == 0 && _browserInfo.BrowserGameObject != null
                && (_browserInfo.BrowserType == "LargeScrollList"
                    || _browserInfo.BrowserType == "SelectNCounters"))
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
                    Log.Msg("BrowserNavigator", $"Multi-zone: {_zoneButtons.Count} zone buttons, {_browserCards.Count} cards, active index: {_currentZoneButtonIndex}");
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

            Log.Msg("BrowserNavigator", $"Found {_browserCards.Count} cards, {_browserButtons.Count} buttons");
        }

        /// <summary>
        /// Discovers cards for mulligan/opening hand browsers.
        /// </summary>
        private void DiscoverMulliganCards()
        {
            Log.Msg("BrowserNavigator", $"Searching for opening hand cards");

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

            Log.Msg("BrowserNavigator", $"After opening hand search: {_browserCards.Count} cards found");
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
                    Log.Msg("BrowserNavigator", $"Found card in {holder.name}: {child.name} -> {cardName}");
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
                    Log.Msg("BrowserNavigator", $"Added mulligan button: {button.name}");
                }
            }
        }

        /// <summary>
        /// Discovers PromptButton_Primary/Secondary as fallback.
        /// </summary>
        private void DiscoverPromptButtons()
        {
            Log.Msg("BrowserNavigator", $"No buttons found, searching for PromptButtons...");

            var promptButtons = BrowserDetector.FindActiveGameObjects(go =>
                go.name.StartsWith(BrowserDetector.PromptButtonPrimaryPrefix) ||
                go.name.StartsWith(BrowserDetector.PromptButtonSecondaryPrefix));

            foreach (var button in promptButtons)
            {
                if (!_browserButtons.Contains(button))
                {
                    _browserButtons.Add(button);
                    string buttonText = UITextExtractor.GetButtonText(button, button.name);
                    Log.Msg("BrowserNavigator", $"Found prompt button: {button.name} -> '{buttonText}'");
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
                Log.Msg("BrowserNavigator", $"Found button: {child.name}");
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

            // Find the localized ViewBattlefield text from the scaffold.
            // ViewBattlefield is a separate GO branch (with MainButton child) but the scroll
            // list duplicates it as a choice entry with localized text (e.g. "Spielfeld betrachten").
            // We match by text to filter it regardless of locale.
            string viewBattlefieldText = null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("ViewBattlefield", StringComparison.Ordinal))
                {
                    viewBattlefieldText = UITextExtractor.GetButtonText(t.gameObject, null);
                    if (!string.IsNullOrEmpty(viewBattlefieldText)) break;
                }
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (!child.gameObject.activeInHierarchy) continue;
                if (!BrowserDetector.HasClickableComponent(child.gameObject)) continue;
                if (_browserButtons.Contains(child.gameObject)) continue;
                if (BrowserDetector.MatchesButtonPattern(child.name, BrowserDetector.ButtonPatterns)) continue;

                // Use null fallback: choice container GOs are named with their text content
                // (e.g. "Eine Karte abwerfen") and are the correct click targets with CustomButton.
                // Internal backing elements (Secondary_Base, Primary_Base) have no TMP_Text
                // children of their own and return null here.
                // DFS order ensures parents are found before children, and dedup skips children.
                string choiceText = UITextExtractor.GetButtonText(child.gameObject, null);
                if (string.IsNullOrEmpty(choiceText)) continue;

                // Skip ViewBattlefield choice (matches the scaffold's ViewBattlefield text)
                if (viewBattlefieldText != null &&
                    string.Equals(choiceText, viewBattlefieldText, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Deduplicate by text (parent+child both have clickable components)
                if (!seenTexts.Add(choiceText))
                    continue;

                choiceButtons.Add(child.gameObject);
                Log.Msg("BrowserNavigator", $"Found LargeScrollList choice: '{choiceText}'");
            }

            if (choiceButtons.Count > 0)
            {
                // Replace scaffold buttons entirely - choices are the only navigable items
                _browserButtons.Clear();
                _browserButtons.AddRange(choiceButtons);
                _isChoiceList = true;
                Log.Msg("BrowserNavigator", $"LargeScrollList: {choiceButtons.Count} choices");
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
                Log.Msg("BrowserNavigator", $"Container {containerName} had {foundCount} cards");
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
                string modelZone = CardStateProvider.GetCardZoneTypeName(card);
                if (!string.IsNullOrEmpty(modelZone) && modelZone != "Hand")
                {
                    Log.Msg("BrowserNavigator", $"Skipping {cardInfo.Name} - actual zone: {modelZone}");
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

            Log.Msg("BrowserNavigator", $"LocalHand search: found {foundCards.Count} valid cards");
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

            // Special announcement for SelectGroup (Fact or Fiction)
            if (_isSelectGroup)
            {
                message = Strings.SelectGroupEntry(_pile1CDCs.Count, _pile2CDCs.Count);
            }
            // Special announcement for KeywordSelection
            else if (_isKeywordSelection)
            {
                int kwCount = GetKeywordCount();
                message = Strings.KeywordSelectionEntry(kwCount);
            }
            // Special announcement for AssignDamage
            else if (_isAssignDamage)
            {
                message = GetAssignDamageEntryAnnouncement(cardCount, browserName);
            }
            // Mulligan keep/mulligan decision: include grouped hand summary in the entry message
            else if (_browserInfo.BrowserType == BrowserDetector.BrowserTypeMulligan)
            {
                string handSummary = BuildGroupedHandSummary();
                if (!string.IsNullOrEmpty(handSummary))
                    message = Strings.MulliganEntry(handSummary);
                else
                    message = Strings.BrowserCards(cardCount, browserName);
            }
            // Special announcement for London mulligan (zone-based drag phase)
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
            // Special announcement for RepeatSelection (modal spell modes)
            else if (_browserInfo.BrowserType == "RepeatSelection")
            {
                // Count option cards vs selected copies
                int optionCount = 0;
                int selectedCount = 0;
                foreach (var c in _browserCards)
                {
                    if (IsInRepeatSelectionsHolder(c))
                        selectedCount++;
                    else
                        optionCount++;
                }

                // Try to extract header/subheader text from the scaffold
                string headerText = ExtractBrowserHeaderText();
                message = Strings.RepeatSelectionEntry(browserName, optionCount, selectedCount, headerText);
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

            string hintKey = GetBrowserHintKey();
            message = Strings.WithHint(message, hintKey);
            _announcer.Announce(message, AnnouncementPriority.High);

            // Auto-navigate to first item
            if (_isKeywordSelection && GetKeywordCount() > 0)
            {
                _currentKeywordIndex = 0;
                _currentButtonIndex = -1;
                AnnounceCurrentKeyword();
            }
            else if (_isMultiZone && _zoneButtons.Count > 0)
            {
                // Multi-zone: start on zone selector
                EnterZoneSelector();
                _currentCardIndex = -1;
                _currentButtonIndex = -1;
                AnnounceMultiZoneSelector();
            }
            else if (cardCount > 0)
            {
                // Start on first selectable card (skips non-selectable in filtered browsers)
                int firstIdx = FindFirstSelectableCard();
                _currentCardIndex = firstIdx; // -1 if none selectable; guard in AnnounceCurrentCard handles it
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

            // SelectGroup: custom announcement with pile membership
            if (_isSelectGroup)
            {
                AnnounceSelectGroupCard(card);
                return;
            }

            var info = CardDetector.ExtractCardInfo(card);
            bool isSelectionBrowser = _browserInfo?.BrowserType == "SelectCards" || _browserInfo?.BrowserType == "SelectCardsMultiZone";
            bool isRepeatSelection = _browserInfo?.BrowserType == "RepeatSelection";

            // Virtual option cards (InstanceId == 0) are modal choices (Warp, Adventure, MDFC, etc.)
            // Real cards (InstanceId > 0) are actual game cards from library/graveyard/etc.
            bool isVirtualOption = (isSelectionBrowser || isRepeatSelection) && GetCardInstanceId(card) == 0;

            string cardName;
            if (isVirtualOption && !string.IsNullOrEmpty(info.RulesText))
            {
                // For virtual option cards, show rules text as the primary text
                cardName = info.RulesText;
            }
            else
            {
                cardName = info.Name ?? "Unknown card";
            }

            // Get selection state from zone navigator for zone-based browsers
            string selectionState = null;
            if (_browserInfo.IsZoneBased)
            {
                selectionState = _zoneNavigator.GetCardSelectionState(card);
            }
            else if (isRepeatSelection)
            {
                // For RepeatSelection: check if this card is in the selections holder
                selectionState = GetRepeatSelectionState(card);
            }
            else if (!_browserInfo.IsMulligan && _browserButtons.Count > 0)
            {
                // Check CDC highlight for non-zone browsers (SelectCards, etc.)
                selectionState = GetCardCDCSelectionState(card);
            }

            // For multi-zone browsers with multiple zones, append the card's zone
            // (e.g., "Your graveyard", "Opponent's exile"). Skip when only one zone — redundant.
            string zoneSuffix = "";
            if (_browserInfo?.BrowserType == "SelectCardsMultiZone" && _zoneButtons.Count > 1)
            {
                zoneSuffix = GetMultiZoneCardZoneName(card);
            }

            // Build announcement
            // For zone-based browsers during Tab, use per-zone position if available
            string position = null;
            if (_browserInfo.IsZoneBased && _zoneNavigator.TryGetCardZonePosition(card, out int zoneIdx, out int zoneTotal) && zoneTotal > 0)
            {
                position = Strings.PositionOf(zoneIdx, zoneTotal, force: true); if (position == "") position = null;
            }
            else if (isRepeatSelection)
            {
                // For RepeatSelection: show position among options only (exclude selected copies)
                int optionIndex, optionTotal;
                GetRepeatSelectionPosition(card, out optionIndex, out optionTotal);
                position = Strings.PositionOf(optionIndex, optionTotal, force: true); if (position == "") position = null;
            }
            else
            {
                position = Strings.PositionOf(_currentCardIndex + 1, _browserCards.Count, force: true); if (position == "") position = null;
            }

            string announcement;
            if (position == null)
            {
                announcement = string.IsNullOrEmpty(selectionState)
                    ? $"{cardName}{zoneSuffix}"
                    : $"{cardName}{zoneSuffix}, {selectionState}";
            }
            else
            {
                announcement = string.IsNullOrEmpty(selectionState)
                    ? $"{cardName}{zoneSuffix}, {position}"
                    : $"{cardName}{zoneSuffix}, {selectionState}, {position}";
            }

            _announcer.Announce(announcement, AnnouncementPriority.High);

            // Virtual option cards: rules-first ordering; real cards: name-first ordering
            var zone = isVirtualOption
                ? ZoneType.Browser
                : ZoneType.Library;
            AccessibleArenaMod.Instance?.CardNavigator?.PrepareForCard(card, zone);
        }

        // CDC HighlightType enum values
        private const int HighlightTypeHot = 3;      // selectable target
        private const int HighlightTypeSelected = 5;  // already toggled/selected
        // Reflection cache: MethodInfo bound to the CDC type, not an instance, so it survives
        // scene changes. Intentionally not reset in any OnSceneChanged hook.
        private static MethodInfo _currentHighlightMethod;
        private static bool _currentHighlightSearched;

        /// <summary>
        /// Returns the CDC HighlightType int for a card, or -1 if unavailable.
        /// </summary>
        private int GetCardHighlightValue(GameObject card)
        {
            if (card == null) return -1;

            var cdc = CardDetector.GetDuelSceneCDC(card);
            if (cdc == null) return -1;

            if (!_currentHighlightSearched)
            {
                _currentHighlightSearched = true;
                _currentHighlightMethod = cdc.GetType().GetMethod("CurrentHighlight", PublicInstance);
            }

            if (_currentHighlightMethod == null) return -1;

            try
            {
                var highlight = _currentHighlightMethod.Invoke(cdc, null);
                return highlight != null ? (int)highlight : -1;
            }
            catch (Exception ex)
            {
                Log.Warn("BrowserNavigator", $"Error reading CurrentHighlight: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Gets card selection state from the CDC's CurrentHighlight() method.
        /// Returns "selected" when HighlightType == Selected (5), null otherwise.
        /// </summary>
        private string GetCardCDCSelectionState(GameObject card)
        {
            return GetCardHighlightValue(card) == HighlightTypeSelected ? Strings.Selected : null;
        }

        /// <summary>
        /// Returns true if a card is selectable (Hot or Selected highlight),
        /// or if we're not in a highlight-filtered browser.
        /// </summary>
        private bool IsCardSelectable(GameObject card)
        {
            if (!_isHighlightFilteredBrowser) return true;
            int hl = GetCardHighlightValue(card);
            return hl == HighlightTypeHot || hl == HighlightTypeSelected;
        }

        /// <summary>
        /// Finds the next selectable card index after fromIndex (no wrapping).
        /// Returns -1 if none found.
        /// </summary>
        private int FindNextSelectableCard(int fromIndex)
        {
            for (int i = fromIndex + 1; i < _browserCards.Count; i++)
            {
                if (IsCardSelectable(_browserCards[i]))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Finds the previous selectable card index before fromIndex (no wrapping).
        /// Returns -1 if none found.
        /// </summary>
        private int FindPreviousSelectableCard(int fromIndex)
        {
            for (int i = fromIndex - 1; i >= 0; i--)
            {
                if (IsCardSelectable(_browserCards[i]))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Finds the first selectable card index, or -1 if none.
        /// </summary>
        private int FindFirstSelectableCard()
        {
            return FindNextSelectableCard(-1);
        }

        /// <summary>
        /// Finds the last selectable card index, or -1 if none.
        /// </summary>
        private int FindLastSelectableCard()
        {
            return FindPreviousSelectableCard(_browserCards.Count);
        }

        // RepeatSelection holder name for selected copies
        private const string RepeatSelectionsHolder = "Repeat_Selections";

        /// <summary>
        /// Gets the selection state for a card in a RepeatSelection browser.
        /// Cards in the selections holder are "selected", options have no state.
        /// </summary>
        private string GetRepeatSelectionState(GameObject card)
        {
            if (card == null) return null;

            Transform parent = card.transform.parent;
            while (parent != null)
            {
                if (parent.name.Contains(RepeatSelectionsHolder))
                    return Strings.RepeatSelectionSelected;
                parent = parent.parent;
            }
            return null;
        }

        /// <summary>
        /// Gets the position of a card among options in a RepeatSelection browser.
        /// Options are in BrowserCardHolder_Default, selected copies are in Repeat_Selections.
        /// </summary>
        private void GetRepeatSelectionPosition(GameObject card, out int index, out int total)
        {
            // Count only option cards (in Default holder), excluding selected copies
            int optionIndex = 0;
            int optionTotal = 0;
            bool found = false;

            foreach (var browserCard in _browserCards)
            {
                bool isOption = IsInDefaultHolder(browserCard);
                bool isSelected = !isOption;

                // If we can't determine holder, count all cards
                if (!isOption && !IsInRepeatSelectionsHolder(browserCard))
                    isOption = true;

                if (isOption)
                {
                    optionTotal++;
                    if (!found)
                        optionIndex++;
                }

                if (browserCard == card)
                    found = true;
            }

            if (!found || optionTotal == 0)
            {
                // Card is a selected copy or not found - use overall position
                index = _currentCardIndex + 1;
                total = _browserCards.Count;
            }
            else
            {
                index = optionIndex;
                total = optionTotal;
            }
        }

        private bool IsInDefaultHolder(GameObject card)
        {
            Transform parent = card?.transform.parent;
            while (parent != null)
            {
                if (parent.name == BrowserDetector.HolderDefault)
                    return true;
                if (parent.name.Contains(RepeatSelectionsHolder))
                    return false;
                parent = parent.parent;
            }
            return false;
        }

        private bool IsInRepeatSelectionsHolder(GameObject card)
        {
            Transform parent = card?.transform.parent;
            while (parent != null)
            {
                if (parent.name.Contains(RepeatSelectionsHolder))
                    return true;
                parent = parent.parent;
            }
            return false;
        }

        /// <summary>
        /// Builds a grouped, comma-separated list of card names from the current browser cards.
        /// Duplicates are collapsed: "3x Ebene, Giada, 2x Sternenfeld-Hirtin".
        /// Preserves order of first appearance. Returns null if no cards are available.
        /// </summary>
        private string BuildGroupedHandSummary()
        {
            if (_browserCards == null || _browserCards.Count == 0) return null;

            var orderedNames = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (var card in _browserCards)
            {
                string name = CardDetector.GetCardName(card);
                if (string.IsNullOrEmpty(name)) continue;
                if (counts.ContainsKey(name))
                    counts[name]++;
                else
                {
                    orderedNames.Add(name);
                    counts[name] = 1;
                }
            }

            if (orderedNames.Count == 0) return null;

            var parts = new List<string>(orderedNames.Count);
            foreach (var name in orderedNames)
            {
                int count = counts[name];
                parts.Add(count > 1 ? $"{count}x {name}" : name);
            }
            return string.Join(", ", parts);
        }

        // BrowserHeader reflection cache
        private static FieldInfo _browserHeaderSubheaderField;
        private static bool _browserHeaderReflectionInit;

        /// <summary>
        /// Extracts the header/subheader text from the browser scaffold.
        /// Used for RepeatSelection to get the remaining selections count text.
        /// Finds the BrowserHeader component and reads its protected 'subheader' TMP field.
        /// </summary>
        private string ExtractBrowserHeaderText()
        {
            if (_browserInfo?.BrowserGameObject == null) return null;

            try
            {
                // Find BrowserHeader component on the scaffold or its children
                var components = _browserInfo.BrowserGameObject.GetComponentsInChildren<Component>(true);
                Component browserHeader = null;
                foreach (var comp in components)
                {
                    if (comp != null && comp.GetType().Name == "BrowserHeader")
                    {
                        browserHeader = comp;
                        break;
                    }
                }

                if (browserHeader == null) return null;

                // Cache the reflection lookup for the 'subheader' field
                if (!_browserHeaderReflectionInit)
                {
                    _browserHeaderReflectionInit = true;
                    _browserHeaderSubheaderField = browserHeader.GetType().GetField("subheader", PrivateInstance);
                    if (_browserHeaderSubheaderField == null)
                        Log.Warn("BrowserNavigator", "BrowserHeader.subheader field not found");
                }

                if (_browserHeaderSubheaderField == null) return null;

                var subheaderTMP = _browserHeaderSubheaderField.GetValue(browserHeader) as TMPro.TMP_Text;
                if (subheaderTMP == null) return null;

                string content = subheaderTMP.text?.Trim();
                if (string.IsNullOrEmpty(content)) return null;

                return UITextExtractor.StripRichText(content);
            }
            catch (Exception ex)
            {
                Log.Warn("BrowserNavigator", $"ExtractBrowserHeaderText error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Announces the current button.
        /// </summary>
        private void AnnounceCurrentButton()
        {
            if (_currentButtonIndex < 0 || _currentButtonIndex >= _browserButtons.Count) return;

            var button = _browserButtons[_currentButtonIndex];

            if (button == null)
            {
                Log.Warn("BrowserNavigator", "Button at index was destroyed, refreshing buttons");
                RefreshBrowserButtons();
                return;
            }

            // SelectGroup: override GroupA/GroupB button labels with pile name and count
            string label;
            if (_isSelectGroup && button.name == "GroupAButton")
            {
                label = Strings.SelectGroupChoosePile(Strings.SelectGroupPile1, _pile1CDCs.Count);
            }
            else if (_isSelectGroup && button.name == "GroupBButton")
            {
                label = Strings.SelectGroupChoosePile(Strings.SelectGroupPile2, _pile2CDCs.Count);
            }
            else
            {
                label = UITextExtractor.GetButtonText(button, button.name);
            }
            string pos = Strings.PositionOf(_currentButtonIndex + 1, _browserButtons.Count, force: true);
            string position = pos != "" ? $", {pos}" : "";

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

        /// <summary>
        /// Tab-navigates to the next selectable card (wrapping).
        /// In highlight-filtered browsers, skips non-selectable cards.
        /// </summary>
        private void TabToNextCard()
        {
            if (_browserCards.Count == 0)
            {
                _announcer.Announce(Strings.NoCards, AnnouncementPriority.Normal);
                return;
            }

            if (!_isHighlightFilteredBrowser)
            {
                NavigateToNextCard();
                return;
            }

            int nextIdx = FindNextSelectableCard(_currentCardIndex);
            if (nextIdx < 0) nextIdx = FindFirstSelectableCard(); // wrap
            if (nextIdx >= 0 && nextIdx != _currentCardIndex)
            {
                _currentCardIndex = nextIdx;
                AnnounceCurrentCard();
            }
        }

        /// <summary>
        /// Tab-navigates to the previous selectable card (wrapping).
        /// In highlight-filtered browsers, skips non-selectable cards.
        /// </summary>
        private void TabToPreviousCard()
        {
            if (_browserCards.Count == 0)
            {
                _announcer.Announce(Strings.NoCards, AnnouncementPriority.Normal);
                return;
            }

            if (!_isHighlightFilteredBrowser)
            {
                NavigateToPreviousCard();
                return;
            }

            int prevIdx = FindPreviousSelectableCard(_currentCardIndex);
            if (prevIdx < 0) prevIdx = FindLastSelectableCard(); // wrap
            if (prevIdx >= 0 && prevIdx != _currentCardIndex)
            {
                _currentCardIndex = prevIdx;
                AnnounceCurrentCard();
            }
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

            Log.Msg("BrowserNavigator", $"Activating card: {cardName}");

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

            // Capture selection state BEFORE click to detect toggle direction
            bool isRepeat = _browserInfo?.BrowserType == "RepeatSelection";
            bool wasSelected = isRepeat
                ? GetRepeatSelectionState(card) != null
                : GetCardCDCSelectionState(card) != null;

            // For non-zone browsers, use generic click
            var result = UIActivator.SimulatePointerClick(card);
            if (!result.Success)
            {
                _announcer.Announce(Strings.CouldNotSelect(cardName), AnnouncementPriority.High);
                return;
            }

            // Wait for game state to update
            if (isRepeat)
                MelonCoroutines.Start(AnnounceRepeatSelectionAfterDelay(wasSelected));
            else
                MelonCoroutines.Start(AnnounceStateChangeAfterDelay(cardName, wasSelected));
        }

        /// <summary>
        /// Announces selection state for RepeatSelection browsers.
        /// Reads the updated subheader text (remaining count) from the BrowserHeader component.
        /// Does not re-find the card — option cards stay in place in RepeatSelection.
        /// </summary>
        private IEnumerator AnnounceRepeatSelectionAfterDelay(bool wasSelected)
        {
            yield return new WaitForSeconds(0.2f);

            string stateText = wasSelected ? Strings.Deselected : Strings.Selected;

            // Read the updated subheader (e.g. "4 remaining options")
            string subheader = ExtractBrowserHeaderText();
            if (!string.IsNullOrEmpty(subheader))
                stateText += ". " + subheader;

            _announcer.Announce(stateText, AnnouncementPriority.High);
        }

        /// <summary>
        /// Waits for UI update then announces the new state.
        /// Uses pre-click selection state to determine toggle direction.
        /// </summary>
        private IEnumerator AnnounceStateChangeAfterDelay(string cardName, bool wasSelected)
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
                // If the card was Selected before click, this is a toggle-off → "deselected".
                // Otherwise (was Hot/Cold/None before) this is a selection → "selected".
                _announcer.Announce(
                    wasSelected ? Strings.Deselected : Strings.Selected,
                    AnnouncementPriority.Normal);

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

            Log.Msg("BrowserNavigator", $"Activating button: {label}");

            // Choice-list buttons are parent GOs with CustomButton whose Click()
            // must be called directly (OnPointerUp requires _mouseOver state).
            var result = _isChoiceList
                ? UIActivator.ActivateViaCustomButtonClick(button)
                : UIActivator.SimulatePointerClick(button);
            if (result.Success)
            {
                _announcer.Announce(label, AnnouncementPriority.Normal);
            }
            else
            {
                _announcer.Announce(Strings.CouldNotClick(label), AnnouncementPriority.High);
            }
        }

        #endregion

        #region Button Clicking

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

        private void ClearEventSystemSelection() => global::AccessibleArena.Core.Services.ZoneNavigator.ClearFocus("BrowserNavigator");

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

        #region Reflection Helpers

        /// <summary>
        /// Walks GameManager → BrowserManager → CurrentBrowser via reflection.
        /// Logs the specific step that failed under "[BrowserNavigator] {logPrefix}: ...".
        /// Callers are expected to filter by the concrete browser type name themselves
        /// (e.g. "AssignDamage") once this helper returns true.
        /// </summary>
        private static bool TryGetCurrentBrowser(string logPrefix, out object currentBrowser)
        {
            currentBrowser = null;

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
                Log.Msg("BrowserNavigator", $"{logPrefix}: GameManager not found");
                return false;
            }

            var bmProp = gameManager.GetType().GetProperty("BrowserManager", AllInstanceFlags);
            var browserManager = bmProp?.GetValue(gameManager);
            if (browserManager == null)
            {
                Log.Msg("BrowserNavigator", $"{logPrefix}: BrowserManager not found");
                return false;
            }

            var cbProp = browserManager.GetType().GetProperty("CurrentBrowser", AllInstanceFlags);
            currentBrowser = cbProp?.GetValue(browserManager);
            return currentBrowser != null;
        }

        #endregion
    }
}
