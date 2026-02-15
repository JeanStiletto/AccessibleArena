using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Zone type for browser navigation.
    /// </summary>
    public enum BrowserZoneType
    {
        None,
        Top,      // Scry: keep on top / London: keep pile (hand)
        Bottom    // Scry: put on bottom / London: bottom pile (library)
    }

    /// <summary>
    /// Handles two-zone navigation for Scry/Surveil and London mulligan browsers.
    /// Both browser types use the same navigation pattern (C/D for zones, Left/Right for cards)
    /// but have different activation APIs.
    /// </summary>
    public class BrowserZoneNavigator
    {
        private readonly IAnnouncementService _announcer;

        // State
        private bool _isActive;
        private string _browserType;
        private BrowserZoneType _currentZone = BrowserZoneType.None;
        private int _cardIndex = -1;

        // Zone card lists (Top = keep/hand, Bottom = dismiss/library)
        private List<GameObject> _topCards = new List<GameObject>();
        private List<GameObject> _bottomCards = new List<GameObject>();

        // London-specific tracking
        private int _mulliganCount = 0;

        public BrowserZoneNavigator(IAnnouncementService announcer)
        {
            _announcer = announcer;
        }

        #region Public Properties

        public bool IsActive => _isActive;
        public BrowserZoneType CurrentZone => _currentZone;
        public int CurrentCardIndex => _cardIndex;
        public int MulliganCount => _mulliganCount;

        public GameObject CurrentCard
        {
            get
            {
                var list = GetCurrentZoneCards();
                if (_cardIndex >= 0 && _cardIndex < list.Count)
                    return list[_cardIndex];
                return null;
            }
        }

        public int TopCardCount => _topCards.Count;
        public int BottomCardCount => _bottomCards.Count;

        #endregion

        #region Lifecycle

        /// <summary>
        /// Activates zone navigation for a browser.
        /// </summary>
        public void Activate(BrowserInfo browserInfo)
        {
            _isActive = true;
            _browserType = browserInfo.BrowserType;
            _currentZone = BrowserZoneType.None;
            _cardIndex = -1;
            _topCards.Clear();
            _bottomCards.Clear();

            MelonLogger.Msg($"[BrowserZoneNavigator] Activated for {_browserType}");
        }

        /// <summary>
        /// Deactivates zone navigation.
        /// </summary>
        public void Deactivate()
        {
            // Reset mulligan count when London phase ends
            if (BrowserDetector.IsLondonBrowser(_browserType))
            {
                MelonLogger.Msg($"[BrowserZoneNavigator] London phase complete, resetting mulligan count");
                _mulliganCount = 0;
            }

            _isActive = false;
            _browserType = null;
            _currentZone = BrowserZoneType.None;
            _cardIndex = -1;
            _topCards.Clear();
            _bottomCards.Clear();

            MelonLogger.Msg($"[BrowserZoneNavigator] Deactivated");
        }

        /// <summary>
        /// Increments mulligan count (called when Mulligan button is clicked).
        /// </summary>
        public void IncrementMulliganCount()
        {
            _mulliganCount++;
            MelonLogger.Msg($"[BrowserZoneNavigator] Mulligan taken, count now: {_mulliganCount}");
        }

        /// <summary>
        /// Resets mulligan state for a new game.
        /// </summary>
        public void ResetMulliganState()
        {
            _mulliganCount = 0;
            MelonLogger.Msg("[BrowserZoneNavigator] Mulligan state reset for new game");
        }

        #endregion

        #region Input Handling

        /// <summary>
        /// Handles input for zone-based browsers.
        /// Returns true if input was consumed.
        /// </summary>
        public bool HandleInput()
        {
            if (!_isActive) return false;

            // C key - Enter top/keep zone
            if (Input.GetKeyDown(KeyCode.C))
            {
                EnterZone(BrowserZoneType.Top);
                return true;
            }

            // D key - Enter bottom zone
            if (Input.GetKeyDown(KeyCode.D))
            {
                EnterZone(BrowserZoneType.Bottom);
                return true;
            }

            // Left/Right arrows - navigate within zone (only if in a zone)
            if (_currentZone != BrowserZoneType.None && _cardIndex >= 0)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    NavigatePrevious();
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    NavigateNext();
                    return true;
                }

                // Home/End for jumping to first/last card in zone
                if (Input.GetKeyDown(KeyCode.Home))
                {
                    NavigateFirst();
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.End))
                {
                    NavigateLast();
                    return true;
                }

                // Enter - activate current card (toggle between zones)
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    ActivateCurrentCard();
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Zone Navigation

        /// <summary>
        /// Enters a zone and announces its contents.
        /// </summary>
        public void EnterZone(BrowserZoneType zone)
        {
            _currentZone = zone;
            _cardIndex = -1;

            // Refresh card lists
            RefreshCardLists();

            var currentList = GetCurrentZoneCards();
            string zoneName = GetZoneName(zone);

            if (currentList.Count == 0)
            {
                _announcer.Announce($"{zoneName}: empty", AnnouncementPriority.High);
            }
            else
            {
                // Navigate to first card
                _cardIndex = 0;
                var firstCard = currentList[0];
                var cardName = CardDetector.GetCardName(firstCard);
                _announcer.Announce($"{zoneName}: {currentList.Count} cards. {cardName}, 1 of {currentList.Count}", AnnouncementPriority.High);

                // Update CardInfoNavigator with this card
                var cardNav = AccessibleArenaMod.Instance?.CardNavigator;
                cardNav?.PrepareForCard(firstCard, ZoneType.Library);
            }

            MelonLogger.Msg($"[BrowserZoneNavigator] Entered zone: {zoneName}, {currentList.Count} cards");
        }

        /// <summary>
        /// Navigates to the next card in the current zone.
        /// Stops at the end without wrapping.
        /// </summary>
        public void NavigateNext()
        {
            var currentList = GetCurrentZoneCards();
            if (currentList.Count == 0)
            {
                string zoneName = GetZoneName(_currentZone);
                _announcer.Announce($"{zoneName}: empty", AnnouncementPriority.Normal);
                return;
            }

            if (_cardIndex < currentList.Count - 1)
            {
                _cardIndex++;
                AnnounceCurrentCard();
            }
            else
            {
                _announcer.AnnounceInterrupt(Strings.EndOfZone);
            }
        }

        /// <summary>
        /// Navigates to the previous card in the current zone.
        /// Stops at the beginning without wrapping.
        /// </summary>
        public void NavigatePrevious()
        {
            var currentList = GetCurrentZoneCards();
            if (currentList.Count == 0)
            {
                string zoneName = GetZoneName(_currentZone);
                _announcer.Announce($"{zoneName}: empty", AnnouncementPriority.Normal);
                return;
            }

            if (_cardIndex > 0)
            {
                _cardIndex--;
                AnnounceCurrentCard();
            }
            else
            {
                _announcer.AnnounceInterrupt(Strings.BeginningOfZone);
            }
        }

        /// <summary>
        /// Jumps to the first card in the current zone.
        /// </summary>
        public void NavigateFirst()
        {
            var currentList = GetCurrentZoneCards();
            if (currentList.Count == 0)
            {
                string zoneName = GetZoneName(_currentZone);
                _announcer.Announce($"{zoneName}: empty", AnnouncementPriority.Normal);
                return;
            }

            if (_cardIndex == 0)
            {
                _announcer.AnnounceInterrupt(Strings.BeginningOfZone);
                return;
            }

            _cardIndex = 0;
            AnnounceCurrentCard();
        }

        /// <summary>
        /// Jumps to the last card in the current zone.
        /// </summary>
        public void NavigateLast()
        {
            var currentList = GetCurrentZoneCards();
            if (currentList.Count == 0)
            {
                string zoneName = GetZoneName(_currentZone);
                _announcer.Announce($"{zoneName}: empty", AnnouncementPriority.Normal);
                return;
            }

            int lastIndex = currentList.Count - 1;
            if (_cardIndex == lastIndex)
            {
                _announcer.AnnounceInterrupt(Strings.EndOfZone);
                return;
            }

            _cardIndex = lastIndex;
            AnnounceCurrentCard();
        }

        /// <summary>
        /// Announces the current card in zone navigation.
        /// </summary>
        private void AnnounceCurrentCard()
        {
            var currentList = GetCurrentZoneCards();
            if (_cardIndex < 0 || _cardIndex >= currentList.Count) return;

            var card = currentList[_cardIndex];
            var cardName = CardDetector.GetCardName(card);
            string zoneName = GetShortZoneName(_currentZone);

            _announcer.Announce($"{cardName}, {zoneName}, {_cardIndex + 1} of {currentList.Count}", AnnouncementPriority.High);

            // Update CardInfoNavigator
            var cardNav = AccessibleArenaMod.Instance?.CardNavigator;
            cardNav?.PrepareForCard(card, ZoneType.Library);
        }

        #endregion

        #region Card Activation

        /// <summary>
        /// Activates (toggles) the current card, moving it to the other zone.
        /// </summary>
        public void ActivateCurrentCard()
        {
            var currentList = GetCurrentZoneCards();
            if (_cardIndex < 0 || _cardIndex >= currentList.Count)
            {
                _announcer.Announce("No card selected", AnnouncementPriority.Normal);
                return;
            }

            var card = currentList[_cardIndex];
            var cardName = CardDetector.GetCardName(card);

            MelonLogger.Msg($"[BrowserZoneNavigator] Activating card: {cardName} in {_browserType}");

            bool success = TryActivateCardViaDragSimulation(card, cardName);

            if (success)
            {
                // Refresh after delay
                MelonCoroutines.Start(RefreshZoneAfterDelay(cardName));
            }
            else
            {
                _announcer.Announce(Strings.CouldNotMove(cardName), AnnouncementPriority.High);
            }
        }

        /// <summary>
        /// Refreshes zone after card activation with a short delay.
        /// </summary>
        private IEnumerator RefreshZoneAfterDelay(string movedCardName)
        {
            yield return new WaitForSeconds(0.2f);

            RefreshCardLists();

            var currentList = GetCurrentZoneCards();
            string zoneName = GetZoneName(_currentZone);

            // Adjust index if needed
            if (_cardIndex >= currentList.Count)
                _cardIndex = currentList.Count - 1;

            if (currentList.Count == 0)
            {
                // Card moved to the other zone
                string newZone = GetZoneName(_currentZone == BrowserZoneType.Top ? BrowserZoneType.Bottom : BrowserZoneType.Top);
                string announcement = $"{movedCardName} moved to {newZone}. {zoneName}: empty";

                // Add London progress info
                if (BrowserDetector.IsLondonBrowser(_browserType) && _mulliganCount > 0)
                {
                    announcement += $". {_bottomCards.Count} of {_mulliganCount} selected for bottom";
                }

                _announcer.Announce(announcement, AnnouncementPriority.Normal);
            }
            else if (_cardIndex >= 0)
            {
                var currentCard = currentList[_cardIndex];
                var currentCardName = CardDetector.GetCardName(currentCard);
                string newZone = GetZoneName(_currentZone == BrowserZoneType.Top ? BrowserZoneType.Bottom : BrowserZoneType.Top);

                string announcement = $"{movedCardName} moved to {newZone}. Now: {currentCardName}, {_cardIndex + 1} of {currentList.Count}";

                // Add London progress info
                if (BrowserDetector.IsLondonBrowser(_browserType) && _mulliganCount > 0)
                {
                    announcement += $". {_bottomCards.Count} of {_mulliganCount} selected for bottom";
                }

                _announcer.Announce(announcement, AnnouncementPriority.Normal);

                // Update CardInfoNavigator
                var cardNav = AccessibleArenaMod.Instance?.CardNavigator;
                cardNav?.PrepareForCard(currentCard, ZoneType.Library);
            }
        }

        #endregion

        #region Card List Refresh

        /// <summary>
        /// Refreshes the card lists from the browser.
        /// </summary>
        private void RefreshCardLists()
        {
            if (BrowserDetector.IsLondonBrowser(_browserType))
            {
                RefreshLondonCardLists();
            }
            else if (GetBrowserController() != null)
            {
                // Surveil: has CardGroupProvider, uses two separate holders
                RefreshSurveilCardLists();
            }
            else
            {
                // Scry: no CardGroupProvider, single holder with placeholder divider
                RefreshScryCardLists();
            }
        }

        /// <summary>
        /// Refreshes card lists for Surveil browsers from two separate holders.
        /// </summary>
        private void RefreshSurveilCardLists()
        {
            _topCards.Clear();
            _bottomCards.Clear();

            // Find cards in BrowserCardHolder_Default (top/keep)
            var defaultHolder = BrowserDetector.FindActiveGameObject(BrowserDetector.HolderDefault);
            if (defaultHolder != null)
            {
                foreach (Transform child in defaultHolder.GetComponentsInChildren<Transform>(true))
                {
                    if (!child.gameObject.activeInHierarchy) continue;
                    if (!CardDetector.IsCard(child.gameObject)) continue;

                    string cardName = CardDetector.GetCardName(child.gameObject);
                    if (BrowserDetector.IsValidCardName(cardName) && !BrowserDetector.IsDuplicateCard(child.gameObject, _topCards))
                    {
                        _topCards.Add(child.gameObject);
                    }
                }
            }

            // Find cards in BrowserCardHolder_ViewDismiss (bottom)
            var dismissHolder = BrowserDetector.FindActiveGameObject(BrowserDetector.HolderViewDismiss);
            if (dismissHolder != null)
            {
                foreach (Transform child in dismissHolder.GetComponentsInChildren<Transform>(true))
                {
                    if (!child.gameObject.activeInHierarchy) continue;
                    if (!CardDetector.IsCard(child.gameObject)) continue;

                    string cardName = CardDetector.GetCardName(child.gameObject);
                    if (BrowserDetector.IsValidCardName(cardName) && !BrowserDetector.IsDuplicateCard(child.gameObject, _bottomCards))
                    {
                        _bottomCards.Add(child.gameObject);
                    }
                }
            }

            // Sort by horizontal position (left to right)
            _topCards.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
            _bottomCards.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

            MelonLogger.Msg($"[BrowserZoneNavigator] Refreshed Surveil lists - Top: {_topCards.Count}, Bottom: {_bottomCards.Count}");
        }

        /// <summary>
        /// Refreshes card lists for Scry browsers by reading the holder's CardViews
        /// and splitting at the placeholder (InstanceId == 0).
        /// Cards before placeholder = top (keep), cards after = bottom (put on bottom).
        /// </summary>
        private void RefreshScryCardLists()
        {
            _topCards.Clear();
            _bottomCards.Clear();

            var defaultHolder = BrowserDetector.FindActiveGameObject(BrowserDetector.HolderDefault);
            if (defaultHolder == null)
            {
                MelonLogger.Warning("[BrowserZoneNavigator] Default holder not found for Scry refresh");
                return;
            }

            var holderComp = GetCardBrowserHolderComponent(defaultHolder);
            if (holderComp == null)
            {
                MelonLogger.Warning("[BrowserZoneNavigator] CardBrowserCardHolder not found for Scry refresh");
                return;
            }

            // Read the ordered CardViews list from the holder
            var cardViewsProp = holderComp.GetType().GetProperty("CardViews",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            var cardViewsList = cardViewsProp?.GetValue(holderComp) as System.Collections.IList;

            if (cardViewsList == null || cardViewsList.Count == 0)
            {
                MelonLogger.Msg("[BrowserZoneNavigator] Scry CardViews list empty");
                return;
            }

            // Split cards at the placeholder (InstanceId == 0)
            bool pastPlaceholder = false;
            foreach (var item in cardViewsList)
            {
                var cdc = item as Component;
                if (cdc == null) continue;

                // Check if this is the placeholder
                var instanceIdProp = cdc.GetType().GetProperty("InstanceId",
                    BindingFlags.Public | BindingFlags.Instance);
                if (instanceIdProp != null)
                {
                    var id = instanceIdProp.GetValue(cdc);
                    bool isPlaceholder = (id is uint uid && uid == 0) || (id is int iid && iid == 0);
                    if (isPlaceholder)
                    {
                        pastPlaceholder = true;
                        continue;
                    }
                }

                var go = cdc.gameObject;
                if (!go.activeInHierarchy) continue;

                string cardName = CardDetector.GetCardName(go);
                if (!BrowserDetector.IsValidCardName(cardName)) continue;

                if (!pastPlaceholder)
                {
                    if (!BrowserDetector.IsDuplicateCard(go, _topCards))
                        _topCards.Add(go);
                }
                else
                {
                    if (!BrowserDetector.IsDuplicateCard(go, _bottomCards))
                        _bottomCards.Add(go);
                }
            }

            MelonLogger.Msg($"[BrowserZoneNavigator] Refreshed Scry lists - Top: {_topCards.Count}, Bottom: {_bottomCards.Count}");
        }

        /// <summary>
        /// Refreshes card lists for London mulligan from the LondonBrowser.
        /// </summary>
        private void RefreshLondonCardLists()
        {
            _topCards.Clear();  // Hand/keep
            _bottomCards.Clear();  // Library/bottom

            try
            {
                var londonBrowser = GetBrowserController();
                if (londonBrowser == null) return;

                // Get hand cards (keep pile) -> _topCards
                var getHandCardsMethod = londonBrowser.GetType().GetMethod("GetHandCards",
                    BindingFlags.Public | BindingFlags.Instance);
                if (getHandCardsMethod != null)
                {
                    var handCards = getHandCardsMethod.Invoke(londonBrowser, null) as System.Collections.IList;
                    if (handCards != null)
                    {
                        MelonLogger.Msg($"[BrowserZoneNavigator] GetHandCards returned {handCards.Count} items");
                        foreach (var cardCDC in handCards)
                        {
                            if (cardCDC is Component comp && comp.gameObject != null)
                            {
                                var go = comp.gameObject;
                                var cardName = CardDetector.GetCardName(go);

                                // Filter out placeholder cards
                                if (!string.IsNullOrEmpty(cardName) && cardName != "Unknown card" && !go.name.Contains("CDC #0"))
                                {
                                    _topCards.Add(go);
                                }
                            }
                        }
                    }
                }

                // Get library cards (bottom pile) -> _bottomCards
                var getLibraryCardsMethod = londonBrowser.GetType().GetMethod("GetLibraryCards",
                    BindingFlags.Public | BindingFlags.Instance);
                if (getLibraryCardsMethod != null)
                {
                    var libraryCards = getLibraryCardsMethod.Invoke(londonBrowser, null) as System.Collections.IList;
                    if (libraryCards != null)
                    {
                        MelonLogger.Msg($"[BrowserZoneNavigator] GetLibraryCards returned {libraryCards.Count} items");
                        foreach (var cardCDC in libraryCards)
                        {
                            if (cardCDC is Component comp && comp.gameObject != null)
                            {
                                var go = comp.gameObject;
                                var cardName = CardDetector.GetCardName(go);

                                // Filter out placeholder cards
                                if (!string.IsNullOrEmpty(cardName) && cardName != "Unknown card" && !go.name.Contains("CDC #0"))
                                {
                                    _bottomCards.Add(go);
                                }
                            }
                        }
                    }
                }

                MelonLogger.Msg($"[BrowserZoneNavigator] Refreshed London lists - Hand: {_topCards.Count}, Library: {_bottomCards.Count}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BrowserZoneNavigator] Error refreshing London card lists: {ex.Message}");
            }
        }

        #endregion

        #region Browser-Specific Activation

        /// <summary>
        /// Activates a card by moving it to the opposite zone.
        /// London/Surveil: uses drag simulation via CardGroupProvider's HandleDrag/OnDragRelease.
        /// Scry: uses card reordering around a placeholder divider (InstanceId == 0).
        /// </summary>
        private bool TryActivateCardViaDragSimulation(GameObject card, string cardName)
        {
            MelonLogger.Msg($"[BrowserZoneNavigator] Attempting card move for: {cardName} (browser: {_browserType})");

            try
            {
                var cardCDC = CardDetector.GetDuelSceneCDC(card);
                if (cardCDC == null)
                {
                    MelonLogger.Warning("[BrowserZoneNavigator] DuelScene_CDC component not found on card");
                    return false;
                }

                // Get browser controller from CardGroupProvider (London and Surveil set this)
                var browser = GetBrowserController();
                if (browser != null)
                {
                    return TryActivateViaDragSimulation(browser, card, cardCDC);
                }

                // No CardGroupProvider = Scry browser (uses card reorder around placeholder)
                return TryActivateViaScryReorder(card, cardName, cardCDC);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserZoneNavigator] Error in TryActivateCardViaDragSimulation: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Drag simulation for London/Surveil browsers.
        /// Positions card at the target zone, then calls HandleDrag + OnDragRelease.
        /// </summary>
        private bool TryActivateViaDragSimulation(object browser, GameObject card, Component cardCDC)
        {
            // Position card at the target zone so HandleDrag moves it correctly
            bool positioned = PositionCardAtTargetZone(browser, card, cardCDC);
            if (!positioned)
            {
                MelonLogger.Warning("[BrowserZoneNavigator] Could not position card at target zone");
                return false;
            }

            var browserType = browser.GetType();
            var handleDragMethod = browserType.GetMethod("HandleDrag", BindingFlags.Public | BindingFlags.Instance);
            if (handleDragMethod == null)
            {
                MelonLogger.Warning("[BrowserZoneNavigator] HandleDrag method not found");
                return false;
            }
            handleDragMethod.Invoke(browser, new object[] { cardCDC });

            var onDragReleaseMethod = browserType.GetMethod("OnDragRelease", BindingFlags.Public | BindingFlags.Instance);
            if (onDragReleaseMethod == null)
            {
                MelonLogger.Warning("[BrowserZoneNavigator] OnDragRelease method not found");
                return false;
            }
            onDragReleaseMethod.Invoke(browser, new object[] { cardCDC });

            MelonLogger.Msg($"[BrowserZoneNavigator] Card moved successfully via drag simulation");
            return true;
        }

        /// <summary>
        /// Scry browser activation: reorders cards around a placeholder divider.
        /// The ScryWorkflow.Submit() reads card order from the browser and splits at the
        /// placeholder (InstanceId == 0): cards before it go to top, cards after go to bottom.
        /// ShiftCards moves the card past/before the placeholder to toggle its zone.
        /// After reordering, syncs the browser's internal list via OnDragRelease.
        /// </summary>
        private bool TryActivateViaScryReorder(GameObject card, string cardName, Component cardCDC)
        {
            MelonLogger.Msg($"[BrowserZoneNavigator] Using Scry reorder for: {cardName}");

            var defaultHolder = BrowserDetector.FindActiveGameObject(BrowserDetector.HolderDefault);
            if (defaultHolder == null)
            {
                MelonLogger.Warning("[BrowserZoneNavigator] Default holder not found for Scry reorder");
                return false;
            }

            var holderComp = GetCardBrowserHolderComponent(defaultHolder);
            if (holderComp == null)
            {
                MelonLogger.Warning("[BrowserZoneNavigator] CardBrowserCardHolder not found for Scry reorder");
                return false;
            }

            // Get CardViews list from the holder
            var cardViewsProp = holderComp.GetType().GetProperty("CardViews",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (cardViewsProp == null)
            {
                MelonLogger.Warning("[BrowserZoneNavigator] CardViews property not found");
                return false;
            }

            var cardViewsList = cardViewsProp.GetValue(holderComp) as System.Collections.IList;
            if (cardViewsList == null || cardViewsList.Count == 0)
            {
                MelonLogger.Warning("[BrowserZoneNavigator] CardViews list is null or empty");
                return false;
            }

            // Find card index and placeholder index (InstanceId == 0)
            int cardIndex = -1;
            int placeholderIndex = -1;

            for (int i = 0; i < cardViewsList.Count; i++)
            {
                var cdc = cardViewsList[i] as Component;
                if (cdc == null) continue;

                if (cdc == cardCDC)
                {
                    cardIndex = i;
                }

                // Check InstanceId for placeholder
                var instanceIdProp = cdc.GetType().GetProperty("InstanceId",
                    BindingFlags.Public | BindingFlags.Instance);
                if (instanceIdProp != null)
                {
                    var id = instanceIdProp.GetValue(cdc);
                    if (id is uint uid && uid == 0)
                    {
                        placeholderIndex = i;
                    }
                    else if (id is int iid && iid == 0)
                    {
                        placeholderIndex = i;
                    }
                }
            }

            if (cardIndex == -1)
            {
                MelonLogger.Warning($"[BrowserZoneNavigator] Card not found in holder CardViews for Scry reorder");
                return false;
            }

            if (placeholderIndex == -1)
            {
                MelonLogger.Warning($"[BrowserZoneNavigator] Placeholder not found in holder CardViews for Scry reorder");
                return false;
            }

            MelonLogger.Msg($"[BrowserZoneNavigator] Scry reorder: cardIndex={cardIndex}, placeholderIndex={placeholderIndex}");

            // ShiftCards moves card to placeholder position, pushing placeholder aside
            var shiftMethod = holderComp.GetType().GetMethod("ShiftCards",
                BindingFlags.Public | BindingFlags.Instance);
            if (shiftMethod == null)
            {
                MelonLogger.Warning("[BrowserZoneNavigator] ShiftCards method not found");
                return false;
            }

            shiftMethod.Invoke(holderComp, new object[] { cardIndex, placeholderIndex });

            // Sync the browser's internal cardViews list by calling OnDragRelease
            // on the current browser (accessible via GameManager.BrowserManager.CurrentBrowser)
            SyncBrowserCardViews(cardCDC);

            MelonLogger.Msg($"[BrowserZoneNavigator] Card reordered successfully via Scry mechanism");
            return true;
        }

        /// <summary>
        /// Syncs the browser's cardViews list from the holder's CardViews after a reorder.
        /// Calls OnDragRelease on the current browser which does:
        /// cardViews = new List(cardHolder.CardViews)
        /// </summary>
        private void SyncBrowserCardViews(Component cardCDC)
        {
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
                    MelonLogger.Warning("[BrowserZoneNavigator] GameManager not found for browser sync");
                    return;
                }

                var bmProp = gameManager.GetType().GetProperty("BrowserManager",
                    BindingFlags.Public | BindingFlags.Instance);
                var browserManager = bmProp?.GetValue(gameManager);
                if (browserManager == null)
                {
                    MelonLogger.Warning("[BrowserZoneNavigator] BrowserManager not found");
                    return;
                }

                var currentBrowserProp = browserManager.GetType().GetProperty("CurrentBrowser",
                    BindingFlags.Public | BindingFlags.Instance);
                var currentBrowser = currentBrowserProp?.GetValue(browserManager);
                if (currentBrowser == null)
                {
                    MelonLogger.Warning("[BrowserZoneNavigator] CurrentBrowser not found");
                    return;
                }

                // Call OnDragRelease to sync: cardViews = new List(cardHolder.CardViews)
                var onDragRelease = currentBrowser.GetType().GetMethod("OnDragRelease",
                    BindingFlags.Public | BindingFlags.Instance);
                if (onDragRelease != null)
                {
                    onDragRelease.Invoke(currentBrowser, new object[] { cardCDC });
                    MelonLogger.Msg("[BrowserZoneNavigator] Browser cardViews synced via OnDragRelease");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BrowserZoneNavigator] Error syncing browser: {ex.Message}");
            }
        }

        /// <summary>
        /// Positions a card at the target zone's center point so that HandleDrag
        /// will detect it as belonging to that zone.
        /// For London: uses HandScreenSpace/LibraryScreenSpace (screen-space positions).
        /// For Surveil: uses _graveyardCenterPoint/_libraryCenterPoint (local-space positions).
        /// </summary>
        private bool PositionCardAtTargetZone(object browser, GameObject card, Component cardCDC)
        {
            var browserType = browser.GetType();

            if (BrowserDetector.IsLondonBrowser(_browserType))
            {
                // London uses screen-space position properties
                var isInHandMethod = browserType.GetMethod("IsInHand", BindingFlags.Public | BindingFlags.Instance);
                bool isInHand = isInHandMethod != null && (bool)isInHandMethod.Invoke(browser, new object[] { cardCDC });

                string targetPropName = isInHand ? "LibraryScreenSpace" : "HandScreenSpace";
                var targetPosProp = browserType.GetProperty(targetPropName, BindingFlags.Public | BindingFlags.Instance);
                if (targetPosProp == null)
                {
                    MelonLogger.Warning($"[BrowserZoneNavigator] {targetPropName} property not found on LondonBrowser");
                    return false;
                }

                var targetPos = (Vector2)targetPosProp.GetValue(browser);
                Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(targetPos.x, targetPos.y, 10f));
                card.transform.position = worldPos;
                return true;
            }
            else
            {
                // Surveil uses local-space center points for graveyard and library zones
                // Top zone = library (keep), Bottom zone = graveyard (dismiss)
                // Moving from Top → target is graveyard; from Bottom → target is library
                string targetFieldName = _currentZone == BrowserZoneType.Top
                    ? "_graveyardCenterPoint"
                    : "_libraryCenterPoint";

                var centerField = browserType.GetField(targetFieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                if (centerField == null)
                {
                    MelonLogger.Warning($"[BrowserZoneNavigator] {targetFieldName} field not found on SurveilBrowser");
                    return false;
                }

                Vector3 targetCenter = (Vector3)centerField.GetValue(browser);

                // Convert local-space center to world-space using the card's Root.parent
                // (same transform chain the game's HandleDrag uses internally)
                var rootProp = cardCDC.GetType().GetProperty("Root", BindingFlags.Public | BindingFlags.Instance);
                if (rootProp != null)
                {
                    var root = rootProp.GetValue(cardCDC) as Transform;
                    if (root != null && root.parent != null)
                    {
                        Vector3 worldPos = root.parent.TransformPoint(targetCenter);
                        card.transform.position = worldPos;
                        return true;
                    }
                }

                // Fallback: use the card holder's transform for coordinate conversion
                var defaultHolder = BrowserDetector.FindActiveGameObject(BrowserDetector.HolderDefault);
                if (defaultHolder != null)
                {
                    Vector3 worldPos = defaultHolder.transform.TransformPoint(targetCenter);
                    card.transform.position = worldPos;
                    return true;
                }

                MelonLogger.Warning("[BrowserZoneNavigator] Could not determine world position for target zone");
                return false;
            }
        }

        /// <summary>
        /// Gets the browser controller from CardGroupProvider on the default card holder.
        /// Both LondonBrowser and SurveilBrowser set themselves as the CardGroupProvider.
        /// </summary>
        private object GetBrowserController()
        {
            var defaultHolder = BrowserDetector.FindActiveGameObject(BrowserDetector.HolderDefault);
            if (defaultHolder == null) return null;

            var cardBrowserHolder = GetCardBrowserHolderComponent(defaultHolder);
            if (cardBrowserHolder == null) return null;

            var providerProp = cardBrowserHolder.GetType().GetProperty("CardGroupProvider",
                BindingFlags.Public | BindingFlags.Instance);
            return providerProp?.GetValue(cardBrowserHolder);
        }

        #endregion

        #region Helpers

        private List<GameObject> GetCurrentZoneCards()
        {
            return _currentZone == BrowserZoneType.Top ? _topCards : _bottomCards;
        }

        private string GetZoneName(BrowserZoneType zone)
        {
            if (BrowserDetector.IsLondonBrowser(_browserType))
            {
                return zone == BrowserZoneType.Top ? "Keep pile" : "Bottom pile";
            }
            else
            {
                // Descriptive names for zone entry announcements
                return zone == BrowserZoneType.Top ? "Keep on top" : "Put on bottom";
            }
        }

        private string GetShortZoneName(BrowserZoneType zone)
        {
            if (BrowserDetector.IsLondonBrowser(_browserType))
            {
                return zone == BrowserZoneType.Top ? "keep" : "bottom";
            }
            else
            {
                // Short names for card navigation announcements (matches Strings constants)
                return zone == BrowserZoneType.Top ? Strings.KeepOnTop : Strings.PutOnBottom;
            }
        }

        /// <summary>
        /// Gets the CardBrowserCardHolder component from a holder GameObject.
        /// </summary>
        private Component GetCardBrowserHolderComponent(GameObject holder)
        {
            if (holder == null) return null;

            foreach (var comp in holder.GetComponents<Component>())
            {
                if (comp != null && comp.GetType().Name == "CardBrowserCardHolder")
                {
                    return comp;
                }
            }
            return null;
        }

        #endregion

        #region State for BrowserNavigator

        /// <summary>
        /// Gets the initial announcement for London mulligan.
        /// </summary>
        public string GetLondonEntryAnnouncement(int cardCount)
        {
            if (_mulliganCount > 0)
            {
                string cardWord = _mulliganCount == 1 ? "card" : "cards";
                return $"Select {_mulliganCount} {cardWord} to put on bottom. {cardCount} cards. Enter to toggle, Space when done";
            }
            return null;
        }

        /// <summary>
        /// Gets card selection state for announcement (which zone a card is in).
        /// </summary>
        public string GetCardSelectionState(GameObject card)
        {
            if (card == null) return null;

            var zone = DetectCardZone(card);
            if (zone == BrowserZoneType.Top)
            {
                return BrowserDetector.IsLondonBrowser(_browserType) ? "keep" : Strings.KeepOnTop;
            }
            if (zone == BrowserZoneType.Bottom)
            {
                return BrowserDetector.IsLondonBrowser(_browserType) ? "bottom" : Strings.PutOnBottom;
            }

            return null;
        }

        /// <summary>
        /// Detects which browser zone a card is in.
        /// For Scry: checks parent hierarchy for holder names.
        /// For London: uses LondonBrowser's IsInHand/IsInLibrary methods.
        /// </summary>
        private BrowserZoneType DetectCardZone(GameObject card)
        {
            if (card == null) return BrowserZoneType.None;

            // Check if card is in our tracked lists first
            if (_topCards.Contains(card)) return BrowserZoneType.Top;
            if (_bottomCards.Contains(card)) return BrowserZoneType.Bottom;

            // For London browser, use LondonBrowser API
            if (BrowserDetector.IsLondonBrowser(_browserType))
            {
                return DetectLondonCardZone(card);
            }

            // For Scry/other: check parent hierarchy
            Transform parent = card.transform.parent;
            while (parent != null)
            {
                if (parent.name == BrowserDetector.HolderDefault)
                {
                    return BrowserZoneType.Top;
                }
                if (parent.name == BrowserDetector.HolderViewDismiss)
                {
                    return BrowserZoneType.Bottom;
                }
                parent = parent.parent;
            }

            return BrowserZoneType.None;
        }

        /// <summary>
        /// Detects zone for a card in London browser using LondonBrowser API.
        /// </summary>
        private BrowserZoneType DetectLondonCardZone(GameObject card)
        {
            try
            {
                var londonBrowser = GetBrowserController();
                if (londonBrowser == null) return BrowserZoneType.None;

                var cardCDC = CardDetector.GetDuelSceneCDC(card);
                if (cardCDC == null) return BrowserZoneType.None;

                // Check IsInHand (keep pile = Top)
                var isInHandMethod = londonBrowser.GetType().GetMethod("IsInHand",
                    BindingFlags.Public | BindingFlags.Instance);
                if (isInHandMethod != null)
                {
                    bool isInHand = (bool)isInHandMethod.Invoke(londonBrowser, new object[] { cardCDC });
                    if (isInHand) return BrowserZoneType.Top;
                }

                // Check IsInLibrary (bottom pile = Bottom)
                var isInLibraryMethod = londonBrowser.GetType().GetMethod("IsInLibrary",
                    BindingFlags.Public | BindingFlags.Instance);
                if (isInLibraryMethod != null)
                {
                    bool isInLibrary = (bool)isInLibraryMethod.Invoke(londonBrowser, new object[] { cardCDC });
                    if (isInLibrary) return BrowserZoneType.Bottom;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BrowserZoneNavigator] Error detecting London card zone: {ex.Message}");
            }

            return BrowserZoneType.None;
        }

        /// <summary>
        /// Activates a card from generic (Tab) navigation.
        /// Detects which zone the card is in and moves it to the other zone.
        /// Called by BrowserNavigator when user presses Enter during Tab navigation.
        /// </summary>
        public bool ActivateCardFromGenericNavigation(GameObject card)
        {
            if (card == null) return false;
            if (!_isActive) return false;

            var cardName = CardDetector.GetCardName(card) ?? "card";
            var cardZone = DetectCardZone(card);

            if (cardZone == BrowserZoneType.None)
            {
                MelonLogger.Warning($"[BrowserZoneNavigator] Could not detect zone for card: {cardName}");
                return false;
            }

            MelonLogger.Msg($"[BrowserZoneNavigator] Generic activation for {cardName}, detected zone: {cardZone}");

            // Temporarily set the current zone so activation methods work correctly
            var previousZone = _currentZone;
            _currentZone = cardZone;

            bool success = TryActivateCardViaDragSimulation(card, cardName);

            // Restore previous zone (or keep new zone if user wasn't in zone navigation)
            if (previousZone != BrowserZoneType.None)
            {
                _currentZone = previousZone;
            }

            if (success)
            {
                // Announce the move
                string newZoneName = cardZone == BrowserZoneType.Top
                    ? GetZoneName(BrowserZoneType.Bottom)
                    : GetZoneName(BrowserZoneType.Top);
                _announcer.Announce(Strings.MovedTo(cardName, newZoneName), AnnouncementPriority.Normal);

                // Refresh card lists
                MelonCoroutines.Start(RefreshAfterGenericActivation());
            }

            return success;
        }

        /// <summary>
        /// Refreshes card lists after generic activation.
        /// </summary>
        private IEnumerator RefreshAfterGenericActivation()
        {
            yield return new WaitForSeconds(0.2f);
            RefreshCardLists();
        }

        #endregion
    }
}
