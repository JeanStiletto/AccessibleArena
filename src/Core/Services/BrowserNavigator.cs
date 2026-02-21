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

        // Zone name constant
        private const string ZoneLocalHand = "LocalHand";

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

            // Zone-based browsers: delegate C/D/arrows/Enter to zone navigator
            if (_browserInfo.IsZoneBased)
            {
                if (_zoneNavigator.HandleInput())
                {
                    return true;
                }
            }

            // Tab / Shift+Tab - cycle through items (generic navigation)
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (_browserCards.Count > 0)
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
                    if (_browserCards.Count > 0) NavigateToPreviousCard();
                    else if (_browserButtons.Count > 0) NavigateToPreviousButton();
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    if (_browserCards.Count > 0) NavigateToNextCard();
                    else if (_browserButtons.Count > 0) NavigateToNextButton();
                    return true;
                }
            }

            // Up/Down arrows - card details (delegate to CardInfoNavigator)
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
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

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (!child.gameObject.activeInHierarchy) continue;
                if (!BrowserDetector.HasClickableComponent(child.gameObject)) continue;
                if (_browserButtons.Contains(child.gameObject)) continue;
                if (BrowserDetector.MatchesButtonPattern(child.name, BrowserDetector.ButtonPatterns)) continue;

                choiceButtons.Add(child.gameObject);
                string choiceText = UITextExtractor.GetButtonText(child.gameObject, child.name);
                MelonLogger.Msg($"[BrowserNavigator] Found LargeScrollList choice: '{choiceText}'");
            }

            if (choiceButtons.Count > 0)
            {
                // Reorder: choices first, then existing scaffold buttons (MainButton etc.)
                var scaffoldButtons = new List<GameObject>(_browserButtons);
                _browserButtons.Clear();
                _browserButtons.AddRange(choiceButtons);
                _browserButtons.AddRange(scaffoldButtons);
                MelonLogger.Msg($"[BrowserNavigator] LargeScrollList: {choiceButtons.Count} choices + {scaffoldButtons.Count} scaffold buttons");
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

            // Special announcement for London mulligan
            if (_browserInfo.IsLondon)
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
            if (cardCount > 0)
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

            // Build announcement
            string announcement;
            if (_browserCards.Count == 1)
            {
                announcement = string.IsNullOrEmpty(selectionState)
                    ? cardName
                    : $"{cardName}, {selectionState}";
            }
            else
            {
                string position = $"{_currentCardIndex + 1} of {_browserCards.Count}";
                announcement = string.IsNullOrEmpty(selectionState)
                    ? $"{cardName}, {position}"
                    : $"{cardName}, {selectionState}, {position}";
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

            // Fallback: PromptButton_Primary (scene search)
            if (TryClickPromptButton(BrowserDetector.PromptButtonPrimaryPrefix, out clickedLabel))
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
            if (TryClickPromptButton(BrowserDetector.PromptButtonSecondaryPrefix, out clickedLabel))
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
