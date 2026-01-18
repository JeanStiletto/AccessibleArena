using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using MTGAAccessibility.Core.Interfaces;
using MTGAAccessibility.Core.Models;

namespace MTGAAccessibility.Core.Services
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
                var cardNav = MTGAAccessibilityMod.Instance?.CardNavigator;
                cardNav?.PrepareForCard(firstCard, ZoneType.Library);
            }

            MelonLogger.Msg($"[BrowserZoneNavigator] Entered zone: {zoneName}, {currentList.Count} cards");
        }

        /// <summary>
        /// Navigates to the next card in the current zone.
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

            _cardIndex++;
            if (_cardIndex >= currentList.Count)
                _cardIndex = 0;

            AnnounceCurrentCard();
        }

        /// <summary>
        /// Navigates to the previous card in the current zone.
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

            _cardIndex--;
            if (_cardIndex < 0)
                _cardIndex = currentList.Count - 1;

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
            var cardNav = MTGAAccessibilityMod.Instance?.CardNavigator;
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

            bool success;
            if (BrowserDetector.IsLondonBrowser(_browserType))
            {
                success = TryActivateCardViaLondonBrowser(card, cardName);
            }
            else
            {
                success = TryActivateCardViaScryBrowser(card, cardName);
            }

            if (success)
            {
                // Refresh after delay
                MelonCoroutines.Start(RefreshZoneAfterDelay(cardName));
            }
            else
            {
                _announcer.Announce($"Could not move {cardName}", AnnouncementPriority.High);
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
                var cardNav = MTGAAccessibilityMod.Instance?.CardNavigator;
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
            else
            {
                RefreshScryCardLists();
            }
        }

        /// <summary>
        /// Refreshes card lists for Scry/Surveil browsers from holders.
        /// </summary>
        private void RefreshScryCardLists()
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
                var londonBrowser = GetLondonBrowser();
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
        /// Activates a card via the Scry/Surveil browser by moving it between holders.
        /// </summary>
        private bool TryActivateCardViaScryBrowser(GameObject card, string cardName)
        {
            MelonLogger.Msg($"[BrowserZoneNavigator] Attempting Scry card move for: {cardName}");

            try
            {
                // Find both holders
                var defaultHolder = BrowserDetector.FindActiveGameObject(BrowserDetector.HolderDefault);
                var dismissHolder = BrowserDetector.FindActiveGameObject(BrowserDetector.HolderViewDismiss);

                if (defaultHolder == null || dismissHolder == null)
                {
                    MelonLogger.Warning("[BrowserZoneNavigator] Could not find both browser holders");
                    return false;
                }

                // Get CardBrowserCardHolder components
                var defaultHolderComp = GetCardBrowserHolderComponent(defaultHolder);
                var dismissHolderComp = GetCardBrowserHolderComponent(dismissHolder);

                if (defaultHolderComp == null || dismissHolderComp == null)
                {
                    MelonLogger.Warning("[BrowserZoneNavigator] CardBrowserCardHolder components not found");
                    return false;
                }

                // Determine source and target based on current zone
                Component sourceHolderComp = _currentZone == BrowserZoneType.Top ? defaultHolderComp : dismissHolderComp;
                Component targetHolderComp = _currentZone == BrowserZoneType.Top ? dismissHolderComp : defaultHolderComp;

                // Get DuelScene_CDC component from card
                var cardCDC = CardDetector.GetDuelSceneCDC(card);
                if (cardCDC == null)
                {
                    MelonLogger.Warning("[BrowserZoneNavigator] DuelScene_CDC component not found on card");
                    return false;
                }

                // Remove card from source holder
                var sourceType = sourceHolderComp.GetType();
                var removeCardMethod = sourceType.GetMethod("RemoveCard", BindingFlags.Public | BindingFlags.Instance);
                if (removeCardMethod == null)
                {
                    MelonLogger.Warning("[BrowserZoneNavigator] RemoveCard method not found");
                    return false;
                }

                removeCardMethod.Invoke(sourceHolderComp, new object[] { cardCDC });

                // Add card to target holder
                var targetType = targetHolderComp.GetType();
                var addCardMethod = targetType.GetMethod("AddCard", BindingFlags.Public | BindingFlags.Instance);
                if (addCardMethod == null)
                {
                    // Try base class
                    var baseType = targetType.BaseType;
                    while (baseType != null && addCardMethod == null)
                    {
                        addCardMethod = baseType.GetMethod("AddCard", BindingFlags.Public | BindingFlags.Instance);
                        baseType = baseType.BaseType;
                    }
                }

                if (addCardMethod == null)
                {
                    MelonLogger.Warning("[BrowserZoneNavigator] AddCard method not found");
                    return false;
                }

                addCardMethod.Invoke(targetHolderComp, new object[] { cardCDC });
                MelonLogger.Msg($"[BrowserZoneNavigator] Card moved successfully via Scry browser");

                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserZoneNavigator] Error in TryActivateCardViaScryBrowser: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Activates a card via the London browser using drag simulation.
        /// </summary>
        private bool TryActivateCardViaLondonBrowser(GameObject card, string cardName)
        {
            MelonLogger.Msg($"[BrowserZoneNavigator] Attempting London card move for: {cardName}");

            try
            {
                var londonBrowser = GetLondonBrowser();
                if (londonBrowser == null)
                {
                    MelonLogger.Warning("[BrowserZoneNavigator] LondonBrowser not found");
                    return false;
                }

                // Get DuelScene_CDC component from card
                var cardCDC = CardDetector.GetDuelSceneCDC(card);
                if (cardCDC == null)
                {
                    MelonLogger.Warning("[BrowserZoneNavigator] DuelScene_CDC component not found on card");
                    return false;
                }

                // Check current position
                var isInHandMethod = londonBrowser.GetType().GetMethod("IsInHand", BindingFlags.Public | BindingFlags.Instance);
                bool isInHand = isInHandMethod != null && (bool)isInHandMethod.Invoke(londonBrowser, new object[] { cardCDC });

                // Get target position (opposite zone)
                string targetPropName = isInHand ? "LibraryScreenSpace" : "HandScreenSpace";
                var targetPosProp = londonBrowser.GetType().GetProperty(targetPropName, BindingFlags.Public | BindingFlags.Instance);

                if (targetPosProp != null)
                {
                    var targetPos = (Vector2)targetPosProp.GetValue(londonBrowser);
                    Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(targetPos.x, targetPos.y, 10f));
                    card.transform.position = worldPos;
                }

                // HandleDrag
                var handleDragMethod = londonBrowser.GetType().GetMethod("HandleDrag", BindingFlags.Public | BindingFlags.Instance);
                if (handleDragMethod == null)
                {
                    MelonLogger.Warning("[BrowserZoneNavigator] HandleDrag method not found");
                    return false;
                }
                handleDragMethod.Invoke(londonBrowser, new object[] { cardCDC });

                // OnDragRelease
                var onDragReleaseMethod = londonBrowser.GetType().GetMethod("OnDragRelease", BindingFlags.Public | BindingFlags.Instance);
                if (onDragReleaseMethod == null)
                {
                    MelonLogger.Warning("[BrowserZoneNavigator] OnDragRelease method not found");
                    return false;
                }
                onDragReleaseMethod.Invoke(londonBrowser, new object[] { cardCDC });

                MelonLogger.Msg($"[BrowserZoneNavigator] Card moved successfully via London browser");
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserZoneNavigator] Error in TryActivateCardViaLondonBrowser: {ex.Message}");
                return false;
            }
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
        /// Gets the LondonBrowser (CardGroupProvider) from the default browser holder.
        /// </summary>
        private object GetLondonBrowser()
        {
            var defaultHolder = BrowserDetector.FindActiveGameObject(BrowserDetector.HolderDefault);
            if (defaultHolder == null) return null;

            var cardBrowserHolder = GetCardBrowserHolderComponent(defaultHolder);
            if (cardBrowserHolder == null) return null;

            var providerProp = cardBrowserHolder.GetType().GetProperty("CardGroupProvider",
                BindingFlags.Public | BindingFlags.Instance);
            return providerProp?.GetValue(cardBrowserHolder);
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
                var londonBrowser = GetLondonBrowser();
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

            bool success;
            if (BrowserDetector.IsLondonBrowser(_browserType))
            {
                success = TryActivateCardViaLondonBrowser(card, cardName);
            }
            else
            {
                success = TryActivateCardViaScryBrowser(card, cardName);
            }

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
                _announcer.Announce($"{cardName} moved to {newZoneName}", AnnouncementPriority.Normal);

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
