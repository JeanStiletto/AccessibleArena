using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for the draft card picking screen.
    /// Detects DraftContentController and makes draft pack cards navigable.
    /// Cards are DraftPackCardView (extends CDCMetaCardView) inside a DraftPackHolder.
    /// Enter selects/toggles a card, Space confirms the pick.
    /// </summary>
    public class DraftNavigator : BaseNavigator
    {
        private GameObject _draftControllerObject;
        private int _totalCards;

        // Delayed rescan: initial activation + after card pick
        private bool _rescanPending;
        private int _rescanFrameCounter;
        private bool _initialRescanDone;
        private const int InitialRescanDelayFrames = 90; // ~1.5 seconds at 60fps
        private const int ToggleRescanDelayFrames = 20; // ~0.33s for selection toggle
        private const int ConfirmRescanDelayFrames = 90; // ~1.5s for confirm (new pack)
        private const int PopupRescanDelayFrames = 15; // ~0.25s after popup close
        private int _currentRescanDelay = InitialRescanDelayFrames;

        // Rescan type tracking: toggle vs confirm
        private bool _isToggleRescan; // true = quiet rescan after Enter, false = may need full rescan
        private bool _isConfirmRescan; // true = rescan after Space confirm

        // Reflection cache for DraftDeckManager access
        private static FieldInfo _draftDeckManagerField;
        private static MethodInfo _getDeckMethod;
        private static MethodInfo _getReservedCardsMethod;
        private static MethodInfo _isCardAlreadyReservedMethod;
        private static PropertyInfo _deckMainProp;
        private static PropertyInfo _deckSideboardProp;
        private static MethodInfo _cardCollectionQuantityMethod;
        private static PropertyInfo _useButtonOverlayProp;
        private static PropertyInfo _currentCardProp;
        private static PropertyInfo _metaCardViewCardProp;
        private static PropertyInfo _cardDataGrpIdProp;
        private static bool _reflectionInitialized;

        public override string NavigatorId => "Draft";
        public override string ScreenName => GetScreenName();
        public override int Priority => 78; // Below BoosterOpen (80), above General (15)

        public DraftNavigator(IAnnouncementService announcer) : base(announcer)
        {
        }

        private string GetScreenName()
        {
            if (IsInPopupMode)
                return Strings.ScreenDraftPopup;
            if (_totalCards > 0)
                return Strings.ScreenDraftPickCount(_totalCards);
            return Strings.ScreenDraftPick;
        }

        protected override bool DetectScreen()
        {
            // Look for active DraftContentController
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                var type = mb.GetType();
                if (type.Name != T.DraftContentController) continue;

                // Check IsOpen property
                var isOpenProp = type.GetProperty("IsOpen",
                    AllInstanceFlags);

                if (isOpenProp != null && isOpenProp.PropertyType == typeof(bool))
                {
                    try
                    {
                        bool isOpen = (bool)isOpenProp.GetValue(mb);
                        if (isOpen)
                        {
                            // Verify we're in card picking mode (not deck building)
                            // by checking for DraftPackHolder or DraftPackCardView
                            bool hasPackCards = false;
                            foreach (var child in mb.gameObject.GetComponentsInChildren<MonoBehaviour>(false))
                            {
                                if (child == null) continue;
                                string childType = child.GetType().Name;
                                if (childType == "DraftPackHolder" || childType == T.DraftPackCardView)
                                {
                                    hasPackCards = true;
                                    break;
                                }
                            }

                            if (!hasPackCards)
                            {
                                MelonLogger.Msg($"[{NavigatorId}] DraftContentController is open but no pack cards found (deck building mode?)");
                                _draftControllerObject = null;
                                return false;
                            }

                            _draftControllerObject = mb.gameObject;
                            return true;
                        }
                    }
                    catch { /* Ignore reflection errors */ }
                }
            }

            _draftControllerObject = null;
            return false;
        }

        #region Reflection setup

        private static void EnsureReflectionInitialized()
        {
            if (_reflectionInitialized) return;
            _reflectionInitialized = true;

            try
            {
                // DraftContentController._draftDeckManager (private field)
                var controllerType = FindType("Wotc.Mtga.Wrapper.Draft.DraftContentController");
                if (controllerType != null)
                {
                    _draftDeckManagerField = controllerType.GetField("_draftDeckManager", PrivateInstance);
                }

                // DraftDeckManager methods
                var managerType = FindType("Wotc.Mtga.Wrapper.Draft.DraftDeckManager");
                if (managerType != null)
                {
                    _getDeckMethod = managerType.GetMethod("GetDeck", PublicInstance);
                    _getReservedCardsMethod = managerType.GetMethod("GetReservedCards", PublicInstance);
                    _isCardAlreadyReservedMethod = managerType.GetMethod("IsCardAlreadyReserved", PublicInstance);
                }

                // Deck.Main / Deck.Sideboard
                var deckType = FindType("Deck");
                if (deckType != null)
                {
                    _deckMainProp = deckType.GetProperty("Main", PublicInstance);
                    _deckSideboardProp = deckType.GetProperty("Sideboard", PublicInstance);
                }

                // CardCollection.Quantity(CardData)
                var cardCollectionType = FindType("CardCollection");
                if (cardCollectionType != null)
                {
                    var cardDataType = FindType("GreClient.CardData.CardData");
                    if (cardDataType != null)
                    {
                        _cardCollectionQuantityMethod = cardCollectionType.GetMethod("Quantity", PublicInstance, null, new[] { cardDataType }, null);
                    }
                }

                // DraftPackCardView.UseButtonOverlay, .CurrentCard
                var draftCardViewType = FindType("DraftPackCardView");
                if (draftCardViewType != null)
                {
                    _useButtonOverlayProp = draftCardViewType.GetProperty("UseButtonOverlay", PublicInstance);
                    _currentCardProp = draftCardViewType.GetProperty("CurrentCard", PublicInstance);
                }

                // MetaCardView.Card (CardData)
                var metaCardViewType = FindType("MetaCardView");
                if (metaCardViewType != null)
                {
                    _metaCardViewCardProp = metaCardViewType.GetProperty("Card", PublicInstance);
                }

                // CardData.GrpId
                var cardDataType2 = FindType("GreClient.CardData.CardData");
                if (cardDataType2 != null)
                {
                    _cardDataGrpIdProp = cardDataType2.GetProperty("GrpId", PublicInstance);
                }

                MelonLogger.Msg($"[Draft] Reflection init: deckMgr={_draftDeckManagerField != null}, getDeck={_getDeckMethod != null}, " +
                    $"reserved={_getReservedCardsMethod != null}, isReserved={_isCardAlreadyReservedMethod != null}, " +
                    $"main={_deckMainProp != null}, qty={_cardCollectionQuantityMethod != null}, " +
                    $"overlay={_useButtonOverlayProp != null}, grpId={_cardDataGrpIdProp != null}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Draft] Reflection init error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the DraftDeckManager instance from the controller MonoBehaviour.
        /// </summary>
        private object GetDraftDeckManager()
        {
            if (_draftControllerObject == null || _draftDeckManagerField == null) return null;

            try
            {
                // Find the DraftContentController component
                foreach (var mb in _draftControllerObject.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    if (mb.GetType().Name == T.DraftContentController)
                    {
                        return _draftDeckManagerField.GetValue(mb);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[{NavigatorId}] Error getting DraftDeckManager: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Count how many copies of a card (by GrpId) have been drafted into the deck so far.
        /// Includes main deck + sideboard + currently reserved cards.
        /// </summary>
        private int GetDraftedCopies(object draftDeckManager, MonoBehaviour cardViewMb, uint grpId)
        {
            if (draftDeckManager == null || grpId == 0) return 0;

            int count = 0;

            try
            {
                // Count in deck (main + sideboard)
                if (_getDeckMethod != null)
                {
                    var deck = _getDeckMethod.Invoke(draftDeckManager, null);
                    if (deck != null)
                    {
                        // Get card data from the card view for Quantity lookup
                        object cardData = null;
                        if (_metaCardViewCardProp != null)
                        {
                            cardData = _metaCardViewCardProp.GetValue(cardViewMb);
                        }

                        if (cardData != null && _cardCollectionQuantityMethod != null)
                        {
                            var main = _deckMainProp?.GetValue(deck);
                            var sideboard = _deckSideboardProp?.GetValue(deck);

                            if (main != null)
                            {
                                var mainQty = _cardCollectionQuantityMethod.Invoke(main, new[] { cardData });
                                if (mainQty is int mq) count += mq;
                            }

                            if (sideboard != null)
                            {
                                var sideQty = _cardCollectionQuantityMethod.Invoke(sideboard, new[] { cardData });
                                if (sideQty is int sq) count += sq;
                            }
                        }
                    }
                }

                // Count reserved cards with same GrpId (current pack selections not yet committed)
                if (_getReservedCardsMethod != null)
                {
                    var reserved = _getReservedCardsMethod.Invoke(draftDeckManager, new object[] { false });
                    if (reserved is IEnumerable enumerable)
                    {
                        foreach (var kvp in enumerable)
                        {
                            var kvpType = kvp.GetType();
                            var keyProp = kvpType.GetProperty("Key");
                            if (keyProp == null) continue;

                            var reservedView = keyProp.GetValue(kvp) as MonoBehaviour;
                            if (reservedView == null) continue;

                            // Get GrpId from reserved card's CardData
                            if (_metaCardViewCardProp != null && _cardDataGrpIdProp != null)
                            {
                                var reservedCardData = _metaCardViewCardProp.GetValue(reservedView);
                                if (reservedCardData != null)
                                {
                                    var reservedGrpId = _cardDataGrpIdProp.GetValue(reservedCardData);
                                    if (reservedGrpId is uint rGrpId && rGrpId == grpId)
                                    {
                                        count++;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[{NavigatorId}] Error counting drafted copies: {ex.Message}");
            }

            return count;
        }

        /// <summary>
        /// Get the GrpId from a DraftPackCardView MonoBehaviour.
        /// </summary>
        private uint GetCardGrpId(MonoBehaviour cardViewMb)
        {
            if (cardViewMb == null) return 0;

            try
            {
                // Try DraftPackCardView.CurrentCard.Card.GrpId first
                if (_currentCardProp != null)
                {
                    var currentCard = _currentCardProp.GetValue(cardViewMb);
                    if (currentCard != null)
                    {
                        // ICardCollectionItem.Card → CardData
                        var cardProp = currentCard.GetType().GetProperty("Card", PublicInstance);
                        if (cardProp != null)
                        {
                            var cardData = cardProp.GetValue(currentCard);
                            if (cardData != null && _cardDataGrpIdProp != null)
                            {
                                var grpId = _cardDataGrpIdProp.GetValue(cardData);
                                if (grpId is uint id) return id;
                            }
                        }
                    }
                }

                // Fallback: MetaCardView.Card.GrpId
                if (_metaCardViewCardProp != null && _cardDataGrpIdProp != null)
                {
                    var cardData = _metaCardViewCardProp.GetValue(cardViewMb);
                    if (cardData != null)
                    {
                        var grpId = _cardDataGrpIdProp.GetValue(cardData);
                        if (grpId is uint id) return id;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[{NavigatorId}] Error getting GrpId: {ex.Message}");
            }

            return 0;
        }

        #endregion

        protected override void DiscoverElements()
        {
            _totalCards = 0;
            var addedObjects = new HashSet<GameObject>();

            // Find cards in the draft pack
            FindDraftPackCards(addedObjects);

            // Find action buttons (confirm, deck, sideboard)
            FindActionButtons(addedObjects);
        }

        private void FindDraftPackCards(HashSet<GameObject> addedObjects)
        {
            if (_draftControllerObject == null) return;

            EnsureReflectionInitialized();

            var cardEntries = new List<(GameObject obj, MonoBehaviour mb, float sortOrder)>();

            // Scan the controller hierarchy for DraftPackCardView components directly
            foreach (var mb in _draftControllerObject.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name != T.DraftPackCardView) continue;

                var cardObj = mb.gameObject;
                if (addedObjects.Contains(cardObj)) continue;

                float sortOrder = cardObj.transform.position.x;
                cardEntries.Add((cardObj, mb, sortOrder));
                addedObjects.Add(cardObj);
            }

            // Sort cards by position (left to right)
            cardEntries = cardEntries.OrderBy(x => x.sortOrder).ToList();

            MelonLogger.Msg($"[{NavigatorId}] Found {cardEntries.Count} draft cards");

            // Get DraftDeckManager for picked count and selection state
            var draftDeckManager = GetDraftDeckManager();

            // Add cards to navigation
            foreach (var (cardObj, cardViewMb, _) in cardEntries)
            {
                string cardName = ExtractCardName(cardObj);
                var cardInfo = CardDetector.ExtractCardInfo(cardObj);

                string displayName = !string.IsNullOrEmpty(cardName) ? cardName :
                                     (cardInfo.IsValid ? cardInfo.Name : "Unknown card");

                string label = displayName;
                if (cardInfo.IsValid && !string.IsNullOrEmpty(cardInfo.TypeLine))
                {
                    label += $", {cardInfo.TypeLine}";
                }

                // Check selection state via reflection
                string selectedStatus = GetCardSelectedStatus(draftDeckManager, cardViewMb);
                if (!string.IsNullOrEmpty(selectedStatus))
                {
                    label += $", {selectedStatus}";
                }

                // Show picked count (copies already drafted)
                uint grpId = GetCardGrpId(cardViewMb);
                int pickedCount = GetDraftedCopies(draftDeckManager, cardViewMb, grpId);
                if (pickedCount > 0)
                {
                    label += $", {Strings.DraftPickedCount(pickedCount)}";
                }

                AddElement(cardObj, label);
                _totalCards++;
            }

            MelonLogger.Msg($"[{NavigatorId}] Total: {_totalCards} cards");
        }

        private string ExtractCardName(GameObject cardObj)
        {
            // Try to find the Title text element (same pattern as BoosterOpenNavigator)
            var texts = cardObj.GetComponentsInChildren<TMPro.TMP_Text>(true);
            foreach (var text in texts)
            {
                if (text == null) continue;

                string objName = text.gameObject.name;
                if (objName == "Title")
                {
                    string content = text.text?.Trim();
                    if (!string.IsNullOrEmpty(content))
                    {
                        content = UITextExtractor.StripRichText(content).Trim();
                        if (!string.IsNullOrEmpty(content))
                            return content;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Check if a card is selected/reserved for picking using reflection on DraftDeckManager.
        /// Falls back to visual child-name check if reflection fails.
        /// </summary>
        private string GetCardSelectedStatus(object draftDeckManager, MonoBehaviour cardViewMb)
        {
            // Try reflection-based check first
            if (draftDeckManager != null && cardViewMb != null)
            {
                try
                {
                    // Check UseButtonOverlay → locked-in pick (confirmed)
                    if (_useButtonOverlayProp != null)
                    {
                        var overlay = _useButtonOverlayProp.GetValue(cardViewMb);
                        if (overlay is bool isOverlay && isOverlay)
                        {
                            return Strings.Confirmed;
                        }
                    }

                    // Check IsCardAlreadyReserved → selected but not yet confirmed
                    if (_isCardAlreadyReservedMethod != null)
                    {
                        var result = _isCardAlreadyReservedMethod.Invoke(draftDeckManager, new object[] { cardViewMb });
                        if (result is bool isReserved && isReserved)
                        {
                            return Strings.Selected;
                        }
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[{NavigatorId}] Reflection selection check failed, using fallback: {ex.Message}");
                }
            }

            // Fallback: visual child-name check
            return GetCardSelectedStatusFallback(cardViewMb?.gameObject);
        }

        /// <summary>
        /// Fallback selection detection via child GameObject names.
        /// </summary>
        private string GetCardSelectedStatusFallback(GameObject cardObj)
        {
            if (cardObj == null) return null;

            foreach (Transform child in cardObj.GetComponentsInChildren<Transform>(true))
            {
                if (child == null) continue;
                string name = child.name.ToLowerInvariant();

                if ((name.Contains("select") || name.Contains("highlight") || name.Contains("glow") ||
                     name.Contains("check") || name.Contains("reserved")) &&
                    child.gameObject.activeInHierarchy)
                {
                    if (name.Contains("selectframe") || name.Contains("selected") ||
                        name.Contains("checkmark") || name.Contains("reserved"))
                    {
                        return Strings.Selected;
                    }
                }
            }

            return null;
        }

        private void FindActionButtons(HashSet<GameObject> addedObjects)
        {
            if (_draftControllerObject == null) return;

            foreach (var mb in _draftControllerObject.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                string typeName = mb.GetType().Name;
                if (typeName != T.CustomButton && typeName != T.CustomButtonWithTooltip) continue;

                var button = mb.gameObject;
                if (addedObjects.Contains(button)) continue;

                string name = button.name;
                string buttonText = UITextExtractor.GetButtonText(button, null);

                if (name.Contains("Confirm") || name.Contains("MainButton_Play") ||
                    (!string.IsNullOrEmpty(buttonText) && (buttonText.Contains("bestätigen") ||
                     buttonText.Contains("Confirm") || buttonText.Contains("confirm"))))
                {
                    string label = !string.IsNullOrEmpty(buttonText) ? buttonText : "Confirm Selection";
                    AddElement(button, $"{label}, button");
                    addedObjects.Add(button);
                    MelonLogger.Msg($"[{NavigatorId}] Found confirm button: {name} -> {label}");
                }
            }
        }

        public override string GetTutorialHint() => LocaleManager.Instance.Get("DraftHint");

        protected override string GetActivationAnnouncement()
        {
            string countInfo = _totalCards > 0 ? $" {_totalCards} cards." : "";
            string core = $"Draft Pick.{countInfo}".TrimEnd();
            return Strings.WithHint(core, "DraftHint");
        }

        protected override void HandleInput()
        {
            // Handle custom input first (F1 help, etc.)
            if (HandleCustomInput()) return;

            // I key: Extended card info (keyword descriptions + other faces)
            if (Input.GetKeyDown(KeyCode.I))
            {
                var extInfoNav = AccessibleArenaMod.Instance?.ExtendedInfoNavigator;
                var cardNav = AccessibleArenaMod.Instance?.CardNavigator;
                if (extInfoNav != null && cardNav != null && cardNav.IsActive && cardNav.CurrentCard != null)
                {
                    extInfoNav.Open(cardNav.CurrentCard);
                }
                else
                {
                    _announcer.AnnounceInterrupt(Strings.NoCardToInspect);
                }
                return;
            }

            // F11: Dump current card details for debugging
            if (Input.GetKeyDown(KeyCode.F11))
            {
                if (IsValidIndex && _elements[_currentIndex].GameObject != null)
                {
                    MenuDebugHelper.DumpCardDetails(NavigatorId, _elements[_currentIndex].GameObject, _announcer);
                }
                else
                {
                    _announcer?.Announce(Strings.NoCardToInspect, AnnouncementPriority.High);
                }
                return;
            }

            // Left/Right arrows for navigation between cards (hold-to-repeat)
            if (_holdRepeater.Check(KeyCode.LeftArrow, () => MovePrevious())) return;
            if (_holdRepeater.Check(KeyCode.RightArrow, () => MoveNext())) return;

            // Home/End for quick jump to first/last
            if (Input.GetKeyDown(KeyCode.Home))
            {
                MoveFirst();
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                MoveLast();
                return;
            }

            // Tab/Shift+Tab also navigates
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bool shiftTab = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (shiftTab)
                    MovePrevious();
                else
                    MoveNext();
                return;
            }

            // Enter selects/toggles a card, or confirms if on the confirm button
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (IsValidIndex)
                {
                    var elem = _elements[_currentIndex];
                    MelonLogger.Msg($"[{NavigatorId}] Enter pressed on: {elem.GameObject?.name ?? "null"} (Label: {elem.Label})");

                    // Check if this is the confirm button — use confirm rescan path
                    string label = elem.Label?.ToLowerInvariant() ?? "";
                    bool isConfirmButton = label.Contains("confirm") || label.Contains("bestätigen");

                    UIActivator.Activate(elem.GameObject);

                    _rescanPending = true;
                    _rescanFrameCounter = 0;
                    if (isConfirmButton)
                    {
                        _isToggleRescan = false;
                        _isConfirmRescan = true;
                        _currentRescanDelay = ConfirmRescanDelayFrames;
                    }
                    else
                    {
                        _isToggleRescan = true;
                        _isConfirmRescan = false;
                        _currentRescanDelay = ToggleRescanDelayFrames;
                    }
                }
                return;
            }

            // Space confirms the current selection (clicks confirm button)
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ClickConfirmButton();
                return;
            }

            // Backspace to go back
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                MelonLogger.Msg($"[{NavigatorId}] Backspace pressed");
                ClickBackButton();
                return;
            }
        }

        /// <summary>
        /// Find and click the confirm/submit button.
        /// </summary>
        private void ClickConfirmButton()
        {
            foreach (var elem in _elements)
            {
                if (elem.GameObject == null) continue;
                string label = elem.Label?.ToLowerInvariant() ?? "";
                if (label.Contains("confirm") || label.Contains("bestätigen"))
                {
                    MelonLogger.Msg($"[{NavigatorId}] Clicking confirm button: {elem.GameObject.name}");
                    UIActivator.Activate(elem.GameObject);

                    _rescanPending = true;
                    _rescanFrameCounter = 0;
                    _isToggleRescan = false;
                    _isConfirmRescan = true;
                    _currentRescanDelay = ConfirmRescanDelayFrames;
                    return;
                }
            }

            // Fallback: search all CustomButtons in draft area for confirm
            if (_draftControllerObject != null)
            {
                foreach (var mb in _draftControllerObject.GetComponentsInChildren<MonoBehaviour>(false))
                {
                    if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                    if (mb.GetType().Name != T.CustomButton) continue;

                    string name = mb.gameObject.name;
                    string text = UITextExtractor.GetButtonText(mb.gameObject, null);
                    if (name.Contains("Confirm") || name.Contains("MainButton") ||
                        (!string.IsNullOrEmpty(text) && (text.Contains("bestätigen") || text.Contains("Confirm"))))
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Clicking confirm button (fallback): {name}");
                        UIActivator.Activate(mb.gameObject);
                        _rescanPending = true;
                        _rescanFrameCounter = 0;
                        _isToggleRescan = false;
                        _isConfirmRescan = true;
                        _currentRescanDelay = ConfirmRescanDelayFrames;
                        return;
                    }
                }
            }

            _announcer?.Announce("No confirm button found", AnnouncementPriority.Normal);
        }

        /// <summary>
        /// Find and click a back/exit button to leave draft.
        /// </summary>
        private void ClickBackButton()
        {
            if (_draftControllerObject == null) return;

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name != T.CustomButton) continue;

                string name = mb.gameObject.name;
                if (name.Contains("MainButtonOutline") || name.Contains("BackButton") ||
                    name.Contains("CloseButton"))
                {
                    string text = UITextExtractor.GetButtonText(mb.gameObject, null);
                    MelonLogger.Msg($"[{NavigatorId}] Clicking back button: {name} ({text})");
                    UIActivator.Activate(mb.gameObject);
                    TriggerCloseRescan();
                    return;
                }
            }
        }

        #region Delayed rescan

        public override void Update()
        {
            // Initial rescan after activation (~1.5 seconds for cards to load)
            if (_isActive && !_initialRescanDone)
            {
                _rescanFrameCounter++;
                if (_rescanFrameCounter >= InitialRescanDelayFrames)
                {
                    _initialRescanDone = true;
                    int oldCount = _totalCards;

                    MelonLogger.Msg($"[{NavigatorId}] Initial rescan (current count: {oldCount})");
                    ForceRescan();

                    if (_totalCards > oldCount)
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Found {_totalCards - oldCount} additional cards, {_totalCards} total");
                    }
                }
            }

            // Rescan after card selection or confirmation
            // Skip while popup is active - base popup mode owns element discovery
            if (_isActive && _rescanPending && !IsInPopupMode)
            {
                _rescanFrameCounter++;
                if (_rescanFrameCounter >= _currentRescanDelay)
                {
                    _rescanPending = false;
                    int oldCount = _totalCards;

                    if (_isToggleRescan)
                    {
                        // Quick quiet rescan after Enter (toggle selection)
                        // Only announce selection status, not full label with picked count
                        MelonLogger.Msg($"[{NavigatorId}] Quiet rescan after toggle");
                        QuietRescan(announceSelectionOnly: true);
                        _isToggleRescan = false;
                    }
                    else if (_isConfirmRescan)
                    {
                        // After Space confirm: full rescan if card count changed (new pack)
                        MelonLogger.Msg($"[{NavigatorId}] Rescan after confirm (current count: {oldCount})");
                        _isConfirmRescan = false;

                        // Peek at new card count to decide rescan type
                        int peekCount = PeekCardCount();
                        if (peekCount != oldCount && peekCount > 0)
                        {
                            // New pack loaded - full rescan with announcement
                            ForceRescan();
                        }
                        else
                        {
                            // Same pack (maybe confirm didn't go through) - quiet rescan
                            QuietRescan();
                        }
                    }
                    else
                    {
                        // Generic rescan (e.g., after popup close)
                        MelonLogger.Msg($"[{NavigatorId}] Quiet rescan after action");
                        QuietRescan();
                    }

                    if (_totalCards != oldCount)
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Card count changed: {oldCount} -> {_totalCards}");
                        if (_totalCards == 0)
                        {
                            MelonLogger.Msg($"[{NavigatorId}] No more cards - pack may be complete");
                        }
                    }
                }
            }

            // Deactivation check: if 0 cards and no popup for extended time, re-check screen
            if (_isActive && !IsInPopupMode && _initialRescanDone && !_rescanPending && _totalCards == 0)
            {
                _emptyCardCounter++;
                if (_emptyCardCounter >= EmptyCardDeactivateFrames)
                {
                    _emptyCardCounter = 0;
                    if (!DetectScreen())
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Draft picking no longer active after timeout, deactivating");
                        Deactivate();
                        return;
                    }
                }
            }
            else
            {
                _emptyCardCounter = 0;
            }

            // Check for close after back button
            if (_isActive && _closeTriggered)
            {
                _closeRescanCounter++;
                if (_closeRescanCounter >= 60)
                {
                    _closeTriggered = false;
                    if (!DetectScreen())
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Draft screen closed, deactivating navigator");
                        Deactivate();
                    }
                    else
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Still on draft screen, rescanning");
                        ForceRescan();
                    }
                }
            }

            base.Update();
        }

        /// <summary>
        /// Quiet rescan: rediscover elements without full activation announcement.
        /// Restores cursor position by matching the current GameObject.
        /// </summary>
        /// <param name="announceSelectionOnly">If true, only announce selection status change (after Enter toggle).
        /// If false, announce the full element label (after confirm/popup).</param>
        private void QuietRescan(bool announceSelectionOnly = false)
        {
            if (!_isActive) return;

            // Remember current position
            var currentObj = IsValidIndex ? _elements[_currentIndex].GameObject : null;
            int oldCount = _totalCards;

            // Rediscover
            _elements.Clear();
            _currentIndex = -1;
            DiscoverElements();

            if (_elements.Count == 0) return;

            // Restore position by matching GameObject
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

            if (_currentIndex < 0) _currentIndex = 0;

            UpdateEventSystemSelection();
            UpdateCardNavigation();

            if (announceSelectionOnly)
            {
                // After Enter toggle: only announce selection status, not full label with picked count
                AnnounceSelectionStatus();
            }
            else
            {
                AnnounceCurrentElement();
            }
        }

        /// <summary>
        /// Announce just the selection status of the current card (selected/deselected).
        /// Used after Enter toggle so the player doesn't hear the picked count again.
        /// </summary>
        private void AnnounceSelectionStatus()
        {
            if (!IsValidIndex) return;

            var elem = _elements[_currentIndex];
            if (elem.GameObject == null) return;

            // Get the card view MonoBehaviour to check selection state
            MonoBehaviour cardViewMb = null;
            foreach (var mb in elem.GameObject.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == T.DraftPackCardView)
                {
                    cardViewMb = mb;
                    break;
                }
            }

            if (cardViewMb == null)
            {
                // Not a card (e.g., confirm button) - announce full label
                AnnounceCurrentElement();
                return;
            }

            var draftDeckManager = GetDraftDeckManager();
            string status = GetCardSelectedStatus(draftDeckManager, cardViewMb);

            if (!string.IsNullOrEmpty(status))
            {
                _announcer.AnnounceInterrupt(status);
            }
        }

        /// <summary>
        /// Quickly count how many DraftPackCardView objects are active (without full discovery).
        /// Used to decide between quiet vs full rescan after confirm.
        /// </summary>
        private int PeekCardCount()
        {
            if (_draftControllerObject == null) return 0;
            int count = 0;
            foreach (var mb in _draftControllerObject.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == T.DraftPackCardView) count++;
            }
            return count;
        }

        private bool _closeTriggered;
        private int _closeRescanCounter;
        private int _emptyCardCounter; // Frames with 0 cards and no popup
        private const int EmptyCardDeactivateFrames = 300; // ~5 seconds at 60fps

        protected override void OnActivated()
        {
            base.OnActivated();
            _initialRescanDone = false;
            _rescanFrameCounter = 0;
            _rescanPending = false;
            _isToggleRescan = false;
            _isConfirmRescan = false;
            _currentRescanDelay = InitialRescanDelayFrames;
            _closeTriggered = false;
            _closeRescanCounter = 0;
            _emptyCardCounter = 0;
            EnablePopupDetection();
        }

        protected override void OnDeactivating()
        {
            DisablePopupDetection();
        }

        protected override void OnPopupClosed()
        {
            // Quick quiet rescan after popup closes
            _rescanPending = true;
            _rescanFrameCounter = 0;
            _isToggleRescan = false;
            _isConfirmRescan = false;
            _currentRescanDelay = PopupRescanDelayFrames;
        }

        private void TriggerCloseRescan()
        {
            _closeTriggered = true;
            _closeRescanCounter = 0;
        }

        #endregion

        protected override bool ValidateElements()
        {
            if (_draftControllerObject == null || !_draftControllerObject.activeInHierarchy)
            {
                MelonLogger.Msg($"[{NavigatorId}] Draft controller no longer active");
                return false;
            }

            return base.ValidateElements();
        }

        public override void OnSceneChanged(string sceneName)
        {
            if (_isActive)
            {
                Deactivate();
            }
            _draftControllerObject = null;
            ClearReflectionCache();
        }

        private static void ClearReflectionCache()
        {
            _reflectionInitialized = false;
            _draftDeckManagerField = null;
            _getDeckMethod = null;
            _getReservedCardsMethod = null;
            _isCardAlreadyReservedMethod = null;
            _deckMainProp = null;
            _deckSideboardProp = null;
            _cardCollectionQuantityMethod = null;
            _useButtonOverlayProp = null;
            _currentCardProp = null;
            _metaCardViewCardProp = null;
            _cardDataGrpIdProp = null;
        }
    }
}
