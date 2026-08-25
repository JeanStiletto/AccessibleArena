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
using AccessibleArena.Core.Utils;

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

        // Double-click guard for the click fallback path. DraftContentController treats two
        // clicks on the same card within 0.5s as a lock-in, which submits the pick outright
        // once the quota is full. Only used when ToggleCardReservation is unavailable.
        private GameObject _lastToggledCard;
        private float _lastToggleTime;
        private const float DoubleClickGuardSeconds = 0.6f;

        private sealed class DraftHandles
        {
            public FieldInfo DraftDeckManager;
            public MethodInfo GetDeck;
            public MethodInfo GetReservedCards;
            public MethodInfo IsCardAlreadyReserved;
            public PropertyInfo DeckMain;
            public PropertyInfo DeckSideboard;
            public MethodInfo CardCollectionQuantity;
            public PropertyInfo UseButtonOverlay;
            public PropertyInfo CurrentCard;
            public PropertyInfo MetaCardViewCard;
            public PropertyInfo CardDataGrpId;
            // Pick timer (human draft only) — read off the controller's DraftPod (IDraftPod)
            public PropertyInfo DraftPodProp;       // DraftContentController.DraftPod
            public PropertyInfo PodDraftMode;       // IDraftPod.DraftMode (DraftModes enum)
            public PropertyInfo PodPickRemaining;   // IDraftPod.PickSecondsRemaining (live)
            public PropertyInfo PodPickTotal;       // IDraftPod.PickSecondsTotal

            // --- Multi-pick drafts ("Pick Two" and friends) ---
            // The pick quota is data-driven: DraftDeckManager.NumCardsToPick forwards
            // IDraftPod.PickNumCardsToTake, which is 1 in an ordinary draft and 2 in a
            // Pick Two event. Nothing else about the screen changes.
            public PropertyInfo NumCardsToPick;     // DraftDeckManager.NumCardsToPick (int)
            public MethodInfo ReservedCardCount;    // DraftDeckManager.ReservedCardCount()

            // Reserve toggle. This is the exact method the game's own single-click path calls
            // (DraftContentController.HandleOnCardClicked -> ToggleCardReservation). Calling it
            // directly avoids the controller's private 0.5s double-click detector: a second
            // simulated click on the same card within that window routes to ReserveCardAndLockIn,
            // which submits the whole pick outright once the quota is full and locked — with no
            // confirm button and no undo.
            public MethodInfo ToggleCardReservation; // DraftContentController.ToggleCardReservation(DraftPackCardView)
            public FieldInfo PackHolder;             // DraftContentController._draftPackHolder
            public PropertyInfo PackHolderAnimating; // DraftPackHolder.IsAnimating

            // Confirm button, resolved structurally rather than by label text. Its caption is
            // "Confirm Pick" only once the quota is full; below that the game shows
            // "Select (n/m)", which no substring match for "confirm" can find.
            public FieldInfo ActiveDeckView;         // DraftContentController._activeDeckView (DraftDeckView)
            public FieldInfo ConfirmPickButton;      // DraftDeckView._confirmPickButton (CustomButton)
            public PropertyInfo ButtonInteractable;  // CustomButton.Interactable

            // --- Pack / pick position ---
            // The two pod implementations store it differently: the bot pod keeps 0-based
            // counters, the human pod keeps a PickInfo whose SelfPack/SelfPick are already
            // 1-based (BotDraftPod adds +1 when building the same visual data struct).
            public FieldInfo BotCurrentPack;         // BotDraftPod._currentPack (0-based)
            public FieldInfo BotCurrentPick;         // BotDraftPod._currentPick (0-based)
            public FieldInfo HumanPickInfo;          // HumanDraftPod._currentPickInfo (PickInfo)
            public FieldInfo PickInfoSelfPack;       // PickInfo.SelfPack (1-based)
            public FieldInfo PickInfoSelfPick;       // PickInfo.SelfPick (1-based)
        }

        private static readonly ReflectionCache<DraftHandles> _draftCache = new ReflectionCache<DraftHandles>(
            builder: _ =>
            {
                var h = new DraftHandles();

                // Resolved first: ToggleCardReservation is overload-selected on this type.
                var draftCardViewType = FindType(T.DraftPackCardView);
                if (draftCardViewType != null)
                {
                    h.UseButtonOverlay = draftCardViewType.GetProperty("UseButtonOverlay", PublicInstance);
                    h.CurrentCard = draftCardViewType.GetProperty("CurrentCard", PublicInstance);
                }

                var controllerType = FindType(T.DraftContentControllerFQ);
                if (controllerType != null)
                {
                    h.DraftDeckManager = controllerType.GetField("_draftDeckManager", PrivateInstance);
                    h.DraftPodProp = controllerType.GetProperty("DraftPod", PublicInstance);
                    h.ActiveDeckView = controllerType.GetField("_activeDeckView", PrivateInstance);
                    h.PackHolder = controllerType.GetField("_draftPackHolder", PrivateInstance);
                    h.ToggleCardReservation = draftCardViewType != null
                        ? controllerType.GetMethod("ToggleCardReservation", PrivateInstance, null,
                            new[] { draftCardViewType }, null)
                        : null;
                }

                var packHolderType = FindType(T.DraftPackHolderFQ);
                if (packHolderType != null)
                    h.PackHolderAnimating = packHolderType.GetProperty("IsAnimating", PublicInstance);

                var deckViewType = FindType(T.DraftDeckViewFQ);
                if (deckViewType != null)
                    h.ConfirmPickButton = deckViewType.GetField("_confirmPickButton", PrivateInstance);

                var customButtonType = FindType(T.CustomButton);
                if (customButtonType != null)
                    h.ButtonInteractable = customButtonType.GetProperty("Interactable", PublicInstance);

                var podType = FindType(T.IDraftPodFQ);
                if (podType != null)
                {
                    h.PodDraftMode = podType.GetProperty("DraftMode", PublicInstance);
                    h.PodPickRemaining = podType.GetProperty("PickSecondsRemaining", PublicInstance);
                    h.PodPickTotal = podType.GetProperty("PickSecondsTotal", PublicInstance);
                }

                var botPodType = FindType(T.BotDraftPodFQ);
                if (botPodType != null)
                {
                    h.BotCurrentPack = botPodType.GetField("_currentPack", PrivateInstance);
                    h.BotCurrentPick = botPodType.GetField("_currentPick", PrivateInstance);
                }

                var humanPodType = FindType(T.HumanDraftPodFQ);
                if (humanPodType != null)
                    h.HumanPickInfo = humanPodType.GetField("_currentPickInfo", PrivateInstance);

                var pickInfoType = FindType("Wizards.Unification.Models.Draft.PickInfo");
                if (pickInfoType != null)
                {
                    h.PickInfoSelfPack = pickInfoType.GetField("SelfPack", PublicInstance);
                    h.PickInfoSelfPick = pickInfoType.GetField("SelfPick", PublicInstance);
                }

                var managerType = FindType(T.DraftDeckManagerFQ);
                if (managerType != null)
                {
                    h.GetDeck = managerType.GetMethod("GetDeck", PublicInstance);
                    h.GetReservedCards = managerType.GetMethod("GetReservedCards", PublicInstance);
                    h.IsCardAlreadyReserved = managerType.GetMethod("IsCardAlreadyReserved", PublicInstance);
                    h.NumCardsToPick = managerType.GetProperty("NumCardsToPick", PublicInstance);
                    h.ReservedCardCount = managerType.GetMethod("ReservedCardCount", PublicInstance);
                }

                var deckType = FindType("Deck");
                if (deckType != null)
                {
                    h.DeckMain = deckType.GetProperty("Main", PublicInstance);
                    h.DeckSideboard = deckType.GetProperty("Sideboard", PublicInstance);
                }

                var cardDataType = FindType("GreClient.CardData.CardData");
                var cardCollectionType = FindType("CardCollection");
                if (cardCollectionType != null && cardDataType != null)
                {
                    h.CardCollectionQuantity = cardCollectionType.GetMethod("Quantity", PublicInstance, null, new[] { cardDataType }, null);
                }

                var metaCardViewType = FindType("MetaCardView");
                if (metaCardViewType != null)
                    h.MetaCardViewCard = metaCardViewType.GetProperty("Card", PublicInstance);

                if (cardDataType != null)
                    h.CardDataGrpId = cardDataType.GetProperty("GrpId", PublicInstance);

                return h;
            },
            validator: h => h.DraftDeckManager != null && h.UseButtonOverlay != null,
            logTag: "Draft",
            logSubject: "DraftContentController");

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

            string name = _totalCards > 0
                ? Strings.ScreenDraftPickCount(_totalCards)
                : Strings.ScreenDraftPick;

            // Where we are in the draft. Sighted players read this off the pack header.
            var (pack, pick) = GetPackAndPick();
            if (pack > 0 && pick > 0)
                name += $", {Strings.DraftPackPick(pack, pick)}";

            // Only announced when it diverges from the usual one-card-per-pick rule.
            // "Take 1 card" on every pack of every normal draft would be dead information.
            int toTake = GetNumCardsToPick();
            if (toTake > 1)
                name += $", {Strings.DraftTakeCards(toTake)}";

            return name;
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
                                Log.Msg("{NavigatorId}", $"DraftContentController is open but no pack cards found (deck building mode?)");
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
            _draftCache.EnsureInitialized(typeof(DraftNavigator));
        }

        /// <summary>
        /// Get the DraftContentController MonoBehaviour from the controller GameObject.
        /// </summary>
        private MonoBehaviour GetControllerComponent()
        {
            if (_draftControllerObject == null) return null;

            foreach (var mb in _draftControllerObject.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (mb.GetType().Name == T.DraftContentController)
                    return mb;
            }

            return null;
        }

        /// <summary>
        /// Get the DraftDeckManager instance from the controller MonoBehaviour.
        /// </summary>
        private object GetDraftDeckManager()
        {
            EnsureReflectionInitialized();
            if (_draftCache.Handles?.DraftDeckManager == null) return null;

            try
            {
                var controller = GetControllerComponent();
                if (controller != null)
                    return _draftCache.Handles.DraftDeckManager.GetValue(controller);
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"Error getting DraftDeckManager: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the active IDraftPod from the controller (null if unavailable).
        /// The pod exposes the live pick timer (human draft only).
        /// </summary>
        private object GetDraftPod()
        {
            EnsureReflectionInitialized();
            if (_draftCache.Handles?.DraftPodProp == null) return null;

            try
            {
                var controller = GetControllerComponent();
                if (controller != null)
                    return _draftCache.Handles.DraftPodProp.GetValue(controller);
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"Error getting DraftPod: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// True only when this pod runs the auto-pick countdown (DraftModes.HumanDraft).
        /// Bot/quick drafts are untimed, so timer announcements are suppressed for them.
        /// </summary>
        private bool IsHumanDraftPod(object pod)
        {
            if (pod == null || _draftCache.Handles.PodDraftMode == null) return false;

            try
            {
                var mode = _draftCache.Handles.PodDraftMode.GetValue(pod);
                return mode != null && mode.ToString() == "HumanDraft";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Live seconds remaining before the game auto-picks. Negative/zero once elapsed.
        /// Returns float.NaN if it can't be read.
        /// </summary>
        private float GetPickSecondsRemaining(object pod)
        {
            if (pod == null || _draftCache.Handles.PodPickRemaining == null) return float.NaN;

            try
            {
                var val = _draftCache.Handles.PodPickRemaining.GetValue(pod);
                if (val is float f) return f;
            }
            catch { /* best effort */ }

            return float.NaN;
        }

        /// <summary>
        /// Total seconds allotted for the current pick (0 before the first pick window).
        /// </summary>
        private float GetPickSecondsTotal(object pod)
        {
            if (pod == null || _draftCache.Handles.PodPickTotal == null) return 0f;

            try
            {
                var val = _draftCache.Handles.PodPickTotal.GetValue(pod);
                if (val is float f) return f;
            }
            catch { /* best effort */ }

            return 0f;
        }

        #region Pick quota, reservation and pack position

        /// <summary>
        /// How many cards this pick takes. 1 for an ordinary draft, 2 in a "Pick Two" event.
        /// Falls back to 1 when the quota can't be read, which keeps every announcement in the
        /// quiet single-pick form rather than inventing numbers.
        /// </summary>
        private int GetNumCardsToPick()
        {
            var manager = GetDraftDeckManager();
            if (manager == null || _draftCache.Handles?.NumCardsToPick == null) return 1;

            try
            {
                var val = _draftCache.Handles.NumCardsToPick.GetValue(manager);
                if (val is int n && n > 0) return n;
            }
            catch { /* best effort */ }

            return 1;
        }

        /// <summary>How many cards are currently reserved for this pick (not yet submitted).</summary>
        private int GetReservedCount()
        {
            var manager = GetDraftDeckManager();
            if (manager == null || _draftCache.Handles?.ReservedCardCount == null) return 0;

            try
            {
                var val = _draftCache.Handles.ReservedCardCount.Invoke(manager, null);
                if (val is int n) return n;
            }
            catch { /* best effort */ }

            return 0;
        }

        /// <summary>
        /// Selection progress suffix, but only when it carries information. In a normal draft
        /// the quota is 1 and "1 of 1 selected" after every single pick is pure noise, so this
        /// returns null there. Multi-pick drafts (Pick Two) get the count, because "how many
        /// more do I still owe" is exactly what the player cannot see otherwise.
        /// </summary>
        private string GetSelectionProgressIfMeaningful()
        {
            int toTake = GetNumCardsToPick();
            if (toTake <= 1) return null;

            // Bare "1 of 2" rather than "1 of 2 selected" — the caller has already said
            // "selected" or "deselected", and saying it twice is worse than not at all.
            // Forced past the position-count setting: this is the pick quota, not navigation
            // chatter, and it is the number a Pick Two draft is otherwise silent about.
            return Strings.PositionOf(GetReservedCount(), toTake, force: true);
        }

        /// <summary>
        /// True while the pack holder is animating cards in or out. The game refuses card
        /// clicks in that window (HandleOnCardClicked checks the same flag), so we mirror it
        /// instead of firing a reservation the controller would drop.
        /// </summary>
        private bool IsPackAnimating()
        {
            EnsureReflectionInitialized();
            var h = _draftCache.Handles;
            if (h?.PackHolder == null || h.PackHolderAnimating == null) return false;

            try
            {
                var controller = GetControllerComponent();
                if (controller == null) return false;
                var holder = h.PackHolder.GetValue(controller);
                if (holder == null) return false;
                var val = h.PackHolderAnimating.GetValue(holder);
                return val is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Toggle a card's reservation through the controller's own single-click handler.
        ///
        /// Deliberately NOT a simulated click: DraftContentController.HandleOnCardClicked runs a
        /// private 0.5s double-click detector, and a second click on the same card inside that
        /// window goes to ReserveCardAndLockIn, which submits the entire pick the moment the
        /// quota is full and locked. A screen-reader user pressing Enter twice — once to select,
        /// once to change their mind — would silently commit the pick instead. Calling
        /// ToggleCardReservation directly bypasses the detector entirely.
        ///
        /// Returns false when the method is unavailable, so the caller can fall back to a
        /// debounced click.
        /// </summary>
        private bool TryToggleCardReservation(MonoBehaviour cardViewMb)
        {
            EnsureReflectionInitialized();
            var h = _draftCache.Handles;
            if (h?.ToggleCardReservation == null || cardViewMb == null) return false;

            try
            {
                var controller = GetControllerComponent();
                if (controller == null) return false;

                h.ToggleCardReservation.Invoke(controller, new object[] { cardViewMb });
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"ToggleCardReservation failed, falling back to click: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// The confirm button, resolved from DraftDeckView._confirmPickButton on the deck view
        /// the controller currently shows. Label matching cannot find it: the caption is
        /// "Confirm Pick" only once the quota is full, and "Select (n/m)" below that.
        /// </summary>
        private GameObject GetConfirmPickButton()
        {
            EnsureReflectionInitialized();
            var h = _draftCache.Handles;
            if (h?.ActiveDeckView == null || h.ConfirmPickButton == null) return null;

            try
            {
                var controller = GetControllerComponent();
                if (controller == null) return null;

                var deckView = h.ActiveDeckView.GetValue(controller) as Component;
                if (deckView == null) return null;

                var button = h.ConfirmPickButton.GetValue(deckView) as Component;
                if (button == null) return null;

                return button.gameObject;
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"Could not resolve confirm button: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Whether a CustomButton GameObject is currently clickable. Returns true when the
        /// state can't be read, so an unreadable button still gets its click attempt rather
        /// than being silently swallowed.
        /// </summary>
        private bool IsButtonInteractable(GameObject buttonObj)
        {
            if (buttonObj == null) return false;
            EnsureReflectionInitialized();
            if (_draftCache.Handles?.ButtonInteractable == null) return true;

            try
            {
                foreach (var mb in buttonObj.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    string typeName = mb.GetType().Name;
                    if (typeName != T.CustomButton && typeName != T.CustomButtonWithTooltip) continue;

                    var prop = mb.GetType().GetProperty("Interactable", PublicInstance);
                    if (prop == null) continue;
                    var val = prop.GetValue(mb);
                    if (val is bool b) return b;
                }
            }
            catch { /* best effort */ }

            return true;
        }

        /// <summary>
        /// Current pack and pick number, 1-based, or (0, 0) when unavailable.
        /// The bot pod stores 0-based counters; the human pod stores a PickInfo whose
        /// SelfPack/SelfPick are already display values.
        /// </summary>
        private (int pack, int pick) GetPackAndPick()
        {
            var pod = GetDraftPod();
            if (pod == null) return (0, 0);

            var h = _draftCache.Handles;
            if (h == null) return (0, 0);

            try
            {
                string podTypeName = pod.GetType().Name;

                if (podTypeName == "BotDraftPod" && h.BotCurrentPack != null && h.BotCurrentPick != null)
                {
                    var packVal = h.BotCurrentPack.GetValue(pod);
                    var pickVal = h.BotCurrentPick.GetValue(pod);
                    if (packVal is int p && pickVal is int q)
                        return (p + 1, q + 1);
                    return (0, 0);
                }

                if (h.HumanPickInfo != null && h.PickInfoSelfPack != null && h.PickInfoSelfPick != null)
                {
                    var pickInfo = h.HumanPickInfo.GetValue(pod);
                    if (pickInfo == null) return (0, 0);

                    var packVal = h.PickInfoSelfPack.GetValue(pickInfo);
                    var pickVal = h.PickInfoSelfPick.GetValue(pickInfo);
                    if (packVal is int p && pickVal is int q)
                        return (p, q);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"Could not read pack/pick position: {ex.Message}");
            }

            return (0, 0);
        }

        #endregion

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
                if (_draftCache.Handles.GetDeck != null)
                {
                    var deck = _draftCache.Handles.GetDeck.Invoke(draftDeckManager, null);
                    if (deck != null)
                    {
                        // Get card data from the card view for Quantity lookup
                        object cardData = null;
                        if (_draftCache.Handles.MetaCardViewCard != null)
                        {
                            cardData = _draftCache.Handles.MetaCardViewCard.GetValue(cardViewMb);
                        }

                        if (cardData != null && _draftCache.Handles.CardCollectionQuantity != null)
                        {
                            var main = _draftCache.Handles.DeckMain?.GetValue(deck);
                            var sideboard = _draftCache.Handles.DeckSideboard?.GetValue(deck);

                            if (main != null)
                            {
                                var mainQty = _draftCache.Handles.CardCollectionQuantity.Invoke(main, new[] { cardData });
                                if (mainQty is int mq) count += mq;
                            }

                            if (sideboard != null)
                            {
                                var sideQty = _draftCache.Handles.CardCollectionQuantity.Invoke(sideboard, new[] { cardData });
                                if (sideQty is int sq) count += sq;
                            }
                        }
                    }
                }

                // Count reserved cards with same GrpId (current pack selections not yet committed)
                if (_draftCache.Handles.GetReservedCards != null)
                {
                    var reserved = _draftCache.Handles.GetReservedCards.Invoke(draftDeckManager, new object[] { false });
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
                            if (_draftCache.Handles.MetaCardViewCard != null && _draftCache.Handles.CardDataGrpId != null)
                            {
                                var reservedCardData = _draftCache.Handles.MetaCardViewCard.GetValue(reservedView);
                                if (reservedCardData != null)
                                {
                                    var reservedGrpId = _draftCache.Handles.CardDataGrpId.GetValue(reservedCardData);
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
                Log.Warn("{NavigatorId}", $"Error counting drafted copies: {ex.Message}");
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
                if (_draftCache.Handles.CurrentCard != null)
                {
                    var currentCard = _draftCache.Handles.CurrentCard.GetValue(cardViewMb);
                    if (currentCard != null)
                    {
                        // ICardCollectionItem.Card → CardData
                        var cardProp = currentCard.GetType().GetProperty("Card", PublicInstance);
                        if (cardProp != null)
                        {
                            var cardData = cardProp.GetValue(currentCard);
                            if (cardData != null && _draftCache.Handles.CardDataGrpId != null)
                            {
                                var grpId = _draftCache.Handles.CardDataGrpId.GetValue(cardData);
                                if (grpId is uint id) return id;
                            }
                        }
                    }
                }

                // Fallback: MetaCardView.Card.GrpId
                if (_draftCache.Handles.MetaCardViewCard != null && _draftCache.Handles.CardDataGrpId != null)
                {
                    var cardData = _draftCache.Handles.MetaCardViewCard.GetValue(cardViewMb);
                    if (cardData != null)
                    {
                        var grpId = _draftCache.Handles.CardDataGrpId.GetValue(cardData);
                        if (grpId is uint id) return id;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"Error getting GrpId: {ex.Message}");
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

            Log.Msg("{NavigatorId}", $"Found {cardEntries.Count} draft cards");

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

            Log.Msg("{NavigatorId}", $"Total: {_totalCards} cards");
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
                    if (_draftCache.Handles.UseButtonOverlay != null)
                    {
                        var overlay = _draftCache.Handles.UseButtonOverlay.GetValue(cardViewMb);
                        if (overlay is bool isOverlay && isOverlay)
                        {
                            return Strings.Confirmed;
                        }
                    }

                    // Check IsCardAlreadyReserved → selected but not yet confirmed
                    if (_draftCache.Handles.IsCardAlreadyReserved != null)
                    {
                        var result = _draftCache.Handles.IsCardAlreadyReserved.Invoke(draftDeckManager, new object[] { cardViewMb });
                        if (result is bool isReserved && isReserved)
                        {
                            return Strings.Selected;
                        }
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    Log.Warn("{NavigatorId}", $"Reflection selection check failed, using fallback: {ex.Message}");
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

            // Structural lookup first. In a multi-pick draft the confirm button is captioned
            // "Select (n/m)" until the quota is met, so the caption match below never sees it
            // and the button would be unreachable exactly when the player needs to check it.
            var confirmButton = GetConfirmPickButton();
            if (confirmButton != null && confirmButton.activeInHierarchy && !addedObjects.Contains(confirmButton))
            {
                AddElement(confirmButton, BuildConfirmButtonLabel(confirmButton));
                addedObjects.Add(confirmButton);
                Log.Msg("{NavigatorId}", $"Found confirm button (structural): {confirmButton.name}");
            }

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
                    string label = !string.IsNullOrEmpty(buttonText) ? buttonText : Strings.DraftConfirmSelection;
                    AddElement(button, $"{label}, {Strings.RoleButton}");
                    addedObjects.Add(button);
                    Log.Msg("{NavigatorId}", $"Found confirm button: {name} -> {label}");
                }
            }
        }

        /// <summary>
        /// Label for the confirm button. The game's own caption already carries the numbers
        /// where they matter ("Select (1/2)" below the quota, "Confirm Pick" once it is met),
        /// so we only add the role and, when it is not clickable, say so.
        /// </summary>
        private string BuildConfirmButtonLabel(GameObject button)
        {
            string text = UITextExtractor.GetButtonText(button, null);
            if (string.IsNullOrEmpty(text)) text = Strings.DraftConfirmSelection;

            string label = $"{text}, {Strings.RoleButton}";
            if (!IsButtonInteractable(button))
                label += $", {Strings.DeckStatusUnavailable}";

            return label;
        }

        public override string GetTutorialHint() => LocaleManager.Instance.Get("DraftHint");

        protected override string GetActivationAnnouncement()
        {
            // ScreenName already carries the localized name, the card count, the pack/pick
            // position and (only when it is not 1) the pick quota.
            return Strings.WithHint(GetScreenName(), "DraftHint");
        }

        protected override void HandleInput()
        {
            // Handle custom input first (F1 help, etc.)
            if (HandleCustomInput()) return;

            // Extended info key: keyword descriptions + other faces
            if (Keybinds.Down(KeybindAction.ExtendedInfo))
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

            // Timer key: Announce remaining pick time (human draft auto-pick countdown)
            if (Keybinds.DownAny(KeybindAction.Timer))
            {
                AnnouncePickTimer();
                return;
            }

            // F11: Dump current card details for debugging
            if (KeyInput.GetKeyDown(KeyCode.F11))
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
            if (KeyInput.GetKeyDown(KeyCode.Home))
            {
                MoveFirst();
                return;
            }

            if (KeyInput.GetKeyDown(KeyCode.End))
            {
                MoveLast();
                return;
            }

            // Tab/Shift+Tab also navigates
            if (KeyInput.GetKeyDown(KeyCode.Tab))
            {
                bool shiftTab = KeyInput.GetKey(KeyCode.LeftShift) || KeyInput.GetKey(KeyCode.RightShift);
                if (shiftTab)
                    MovePrevious();
                else
                    MoveNext();
                return;
            }

            // Enter selects/toggles a card, or confirms if on the confirm button
            if (KeyInput.GetKeyDown(KeyCode.Return) || KeyInput.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (IsValidIndex)
                {
                    var elem = _elements[_currentIndex];
                    Log.Msg("{NavigatorId}", $"Enter pressed on: {elem.GameObject?.name ?? "null"} (Label: {elem.Label})");

                    // The confirm button shares the element list with the cards. Route by
                    // component/identity rather than by caption — below the pick quota the
                    // caption reads "Select (n/m)", so no substring match for "confirm" would
                    // recognise it.
                    if (GetDraftPackCardView(elem.GameObject) == null)
                    {
                        var confirmButton = GetConfirmPickButton();
                        // Either this is the confirm button, or we could not resolve one at all
                        // and ClickConfirmButton's own fallback is the better guess.
                        if (confirmButton == null || elem.GameObject == confirmButton)
                            ClickConfirmButton();
                        else
                            UIActivator.Activate(elem.GameObject);
                        return;
                    }

                    ToggleCurrentCard(elem.GameObject);
                }
                return;
            }

            // Space confirms the current selection (clicks confirm button)
            if (KeyInput.GetKeyDown(KeyCode.Space))
            {
                ClickConfirmButton();
                return;
            }

            // Backspace to go back
            if (KeyInput.GetKeyDown(KeyCode.Backspace))
            {
                Log.Msg("{NavigatorId}", $"Backspace pressed");
                ClickBackButton();
                return;
            }
        }

        /// <summary>
        /// Find and click the confirm/submit button.
        /// </summary>
        private void ClickConfirmButton()
        {
            // Structural lookup first (DraftDeckView._confirmPickButton). Caption matching is
            // unreliable: below the pick quota the game relabels this button "Select (n/m)",
            // so a Pick Two draft has no button reading "Confirm" until the second card is in.
            var button = GetConfirmPickButton();

            if (button == null || !button.activeInHierarchy)
                button = FindConfirmButtonByLabel();

            if (button == null)
            {
                _announcer?.Announce(Strings.DraftNoConfirmButton, AnnouncementPriority.Normal);
                return;
            }

            // Not yet clickable: say how many cards are still owed instead of firing a click
            // the game silently drops. Only reachable in a multi-pick draft, where the quota
            // really is the missing information.
            if (!IsButtonInteractable(button))
            {
                int toTake = GetNumCardsToPick();
                int reserved = GetReservedCount();
                int missing = toTake - reserved;

                if (missing > 0)
                    _announcer?.AnnounceInterrupt(Strings.DraftSelectMore(missing));
                else
                    _announcer?.AnnounceInterrupt(Strings.ItemDisabled);
                return;
            }

            Log.Msg("{NavigatorId}", $"Clicking confirm button: {button.name}");
            UIActivator.Activate(button);

            _rescanPending = true;
            _rescanFrameCounter = 0;
            _isToggleRescan = false;
            _isConfirmRescan = true;
            _currentRescanDelay = ConfirmRescanDelayFrames;
        }

        /// <summary>
        /// Last-ditch confirm-button lookup by name/caption, used only when the structural
        /// path fails (game update renamed the field, deck view not yet built). Kept as a
        /// fallback because a draft with no reachable confirm button is unplayable.
        /// </summary>
        private GameObject FindConfirmButtonByLabel()
        {
            if (_draftControllerObject == null) return null;

            foreach (var mb in _draftControllerObject.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                string typeName = mb.GetType().Name;
                if (typeName != T.CustomButton && typeName != T.CustomButtonWithTooltip) continue;

                string name = mb.gameObject.name;
                string text = UITextExtractor.GetButtonText(mb.gameObject, null);
                if (name.Contains("Confirm") || name.Contains("MainButton") ||
                    (!string.IsNullOrEmpty(text) && (text.Contains("bestätigen") || text.Contains("Confirm"))))
                {
                    Log.Msg("{NavigatorId}", $"Confirm button found by label fallback: {name}");
                    return mb.gameObject;
                }
            }

            return null;
        }

        /// <summary>
        /// Select or deselect the focused card. Routes through the controller's own
        /// ToggleCardReservation so a fast second Enter cannot be read as a double-click
        /// (which locks the card in and can submit the whole pick unconfirmed).
        /// Falls back to a debounced click when that method cannot be resolved.
        /// </summary>
        /// <summary>
        /// The card views currently reserved for this pick, or null when they can't be read.
        /// </summary>
        private List<MonoBehaviour> GetReservedCardViews()
        {
            var manager = GetDraftDeckManager();
            if (manager == null || _draftCache.Handles?.GetReservedCards == null) return null;

            try
            {
                var reserved = _draftCache.Handles.GetReservedCards.Invoke(manager, new object[] { false });
                if (!(reserved is IEnumerable entries)) return null;

                var views = new List<MonoBehaviour>();
                foreach (var kvp in entries)
                {
                    if (kvp == null) continue;
                    var keyProp = kvp.GetType().GetProperty("Key", PublicInstance);
                    if (keyProp?.GetValue(kvp) is MonoBehaviour view)
                        views.Add(view);
                }
                return views;
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"Could not read reserved cards: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Report a reservation that the game dropped to make room for the card just selected.
        /// Only called for multi-pick drafts — in a one-card pick the swap is self-evident.
        /// </summary>
        private void AnnounceEvictedReservation(List<MonoBehaviour> before, MonoBehaviour justToggled)
        {
            if (before == null || before.Count == 0) return;

            var after = GetReservedCardViews();
            if (after == null) return;

            foreach (var view in before)
            {
                if (view == null || view == justToggled) continue;
                if (after.Contains(view)) continue;

                string name = ExtractCardName(view.gameObject);
                if (string.IsNullOrEmpty(name)) continue;

                _announcer?.Announce(Strings.DraftReplacedCard(name), AnnouncementPriority.Normal);
                return;
            }
        }

        /// <summary>The DraftPackCardView on a GameObject, or null when it is not a pack card.</summary>
        private static MonoBehaviour GetDraftPackCardView(GameObject obj)
        {
            if (obj == null) return null;

            foreach (var mb in obj.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == T.DraftPackCardView)
                    return mb;
            }

            return null;
        }

        private void ToggleCurrentCard(GameObject cardObj)
        {
            if (cardObj == null) return;

            var cardViewMb = GetDraftPackCardView(cardObj);

            // Not a card (stray button): keep the plain activation path.
            if (cardViewMb == null)
            {
                UIActivator.Activate(cardObj);
                ScheduleToggleRescan();
                return;
            }

            // The controller ignores card clicks while the pack animates; mirror that so a
            // dropped input is reported rather than silently doing nothing.
            if (IsPackAnimating())
            {
                _announcer?.AnnounceInterrupt(Strings.DraftPackNotReady);
                return;
            }

            // Selecting past the quota silently evicts the oldest unlocked reservation
            // (DraftDeckManager.TryAddReservedCard). In a one-card draft that is the obvious
            // "you swapped your pick"; in a multi-pick draft a card you already chose vanishes
            // with no feedback at all, so snapshot and report it.
            bool trackEviction = GetNumCardsToPick() > 1;
            var before = trackEviction ? GetReservedCardViews() : null;

            if (TryToggleCardReservation(cardViewMb))
            {
                AnnounceEvictedReservation(before, cardViewMb);
                ScheduleToggleRescan();
                return;
            }

            // Fallback path: simulate the click, but never twice on the same card inside the
            // controller's 0.5s double-click window.
            float now = Time.unscaledTime;
            if (cardObj == _lastToggledCard && now - _lastToggleTime < DoubleClickGuardSeconds)
            {
                Log.Msg("{NavigatorId}", $"Enter debounced on {cardObj.name} (double-click guard)");
                _announcer?.AnnounceInterrupt(Strings.DraftPickTooFast);
                return;
            }

            _lastToggledCard = cardObj;
            _lastToggleTime = now;
            UIActivator.Activate(cardObj);
            ScheduleToggleRescan();
        }

        private void ScheduleToggleRescan()
        {
            _rescanPending = true;
            _rescanFrameCounter = 0;
            _isToggleRescan = true;
            _isConfirmRescan = false;
            _currentRescanDelay = ToggleRescanDelayFrames;
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
                    Log.Msg("{NavigatorId}", $"Clicking back button: {name} ({text})");
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

                    Log.Msg("{NavigatorId}", $"Initial rescan (current count: {oldCount})");
                    ForceRescan();

                    if (_totalCards > oldCount)
                    {
                        Log.Msg("{NavigatorId}", $"Found {_totalCards - oldCount} additional cards, {_totalCards} total");
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
                        Log.Msg("{NavigatorId}", $"Quiet rescan after toggle");
                        QuietRescan(announceSelectionOnly: true);
                        _isToggleRescan = false;
                    }
                    else if (_isConfirmRescan)
                    {
                        // After Space confirm: full rescan if card count changed (new pack)
                        Log.Msg("{NavigatorId}", $"Rescan after confirm (current count: {oldCount})");
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
                        Log.Msg("{NavigatorId}", $"Quiet rescan after action");
                        QuietRescan();
                    }

                    if (_totalCards != oldCount)
                    {
                        Log.Msg("{NavigatorId}", $"Card count changed: {oldCount} -> {_totalCards}");
                        if (_totalCards == 0)
                        {
                            Log.Msg("{NavigatorId}", $"No more cards - pack may be complete");
                        }
                    }
                }
            }

            // 0 cards, no popup: our pack was submitted and we're waiting for the next pack
            // (other players still picking). Announce the wait once, poll for the new pack so
            // we auto-advance without a manual rescan, and deactivate only if the screen is gone.
            if (_isActive && !IsInPopupMode && _initialRescanDone && !_rescanPending && _totalCards == 0)
            {
                // Announce the waiting state once so the screen isn't silent / repeating the confirm button.
                // Gate on DetectScreen so we don't announce "waiting" when the draft has actually ended.
                if (!_announcedWaiting)
                {
                    _announcedWaiting = true;
                    if (DetectScreen())
                        _announcer?.Announce(Strings.DraftWaitingForPlayers, AnnouncementPriority.Normal);
                }

                // Poll for the next pack arriving. Require two consecutive polls with the same
                // non-zero count so we don't rescan mid-instantiation and announce a partial pack.
                _nextPackPollCounter++;
                if (_nextPackPollCounter >= NextPackPollFrames)
                {
                    _nextPackPollCounter = 0;
                    int peek = PeekCardCount();
                    if (peek > 0 && peek == _lastNonZeroPeek)
                    {
                        Log.Msg("{NavigatorId}", $"Next pack arrived during wait ({peek} cards), rescanning");
                        _announcedWaiting = false;
                        ForceRescan(); // announces "Draft Pick. N cards."
                    }
                    _lastNonZeroPeek = peek;
                }

                // Deactivation check: if still empty after extended time and the screen is gone, deactivate.
                _emptyCardCounter++;
                if (_emptyCardCounter >= EmptyCardDeactivateFrames)
                {
                    _emptyCardCounter = 0;
                    if (!DetectScreen())
                    {
                        Log.Msg("{NavigatorId}", $"Draft picking no longer active after timeout, deactivating");
                        Deactivate();
                        return;
                    }
                }
            }
            else
            {
                _emptyCardCounter = 0;
                _nextPackPollCounter = 0;
                _lastNonZeroPeek = 0;
                _announcedWaiting = false;
            }

            // Poll the human-draft pick timer so we can warn the player before auto-pick.
            // Only while a pack is actually shown (cards present, no popup owning the screen).
            if (_isActive && !IsInPopupMode && _totalCards > 0)
            {
                _timerPollCounter++;
                if (_timerPollCounter >= TimerPollFrames)
                {
                    _timerPollCounter = 0;
                    PollPickTimer();
                }
            }
            else
            {
                _timerPollCounter = 0;
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
                        Log.Msg("{NavigatorId}", $"Draft screen closed, deactivating navigator");
                        Deactivate();
                    }
                    else
                    {
                        Log.Msg("{NavigatorId}", $"Still on draft screen, rescanning");
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
        ///
        /// In an ordinary one-card-per-pick draft this is a bare "selected" / "deselected":
        /// the count is always 1 of 1 and saying so every time is dead information. In a
        /// multi-pick draft the running total is appended, because how many cards the pick
        /// still owes is exactly what is otherwise invisible.
        /// </summary>
        private void AnnounceSelectionStatus()
        {
            if (!IsValidIndex) return;

            var elem = _elements[_currentIndex];
            if (elem.GameObject == null) return;

            var cardViewMb = GetDraftPackCardView(elem.GameObject);
            if (cardViewMb == null)
            {
                // Not a card (e.g., confirm button) - announce full label
                AnnounceCurrentElement();
                return;
            }

            var draftDeckManager = GetDraftDeckManager();
            // Null status means the card is not reserved — i.e. the Enter just deselected it.
            // Previously that produced silence, leaving the user unsure whether it registered.
            string status = GetCardSelectedStatus(draftDeckManager, cardViewMb) ?? Strings.Deselected;

            string progress = GetSelectionProgressIfMeaningful();
            if (!string.IsNullOrEmpty(progress))
                status += $", {progress}";

            _announcer.AnnounceInterrupt(status);
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

        /// <summary>
        /// Poll the live pick timer and emit threshold warnings (human draft only).
        /// Detects a fresh pick window (timer jumps back up) and re-arms the warnings.
        /// </summary>
        private void PollPickTimer()
        {
            var pod = GetDraftPod();
            if (!IsHumanDraftPod(pod)) return; // bot/quick drafts are untimed

            float total = GetPickSecondsTotal(pod);
            float remaining = GetPickSecondsRemaining(pod);
            if (total <= 0f || float.IsNaN(remaining)) return;

            // New pick window? The timer resets to (roughly) total when the next pack arrives,
            // so the remaining value jumps up. Also treat a changed total or the first reading
            // as a new window. Re-arm thresholds relative to the new starting time.
            bool newWindow = _lastPickRemaining < 0f
                             || remaining > _lastPickRemaining + 2f
                             || (_lastPickTotal > 0f && Math.Abs(total - _lastPickTotal) > 0.5f);
            if (newWindow)
                ResetPickWarnings(remaining);

            _lastPickRemaining = remaining;
            _lastPickTotal = total;

            if (remaining <= 0f) return; // already elapsed; auto-pick handled by the game

            // Fire the most urgent un-announced threshold that has been crossed. Setting the
            // lower flags too keeps lighter warnings from firing after a more urgent one.
            if (!_warned5 && remaining <= Warn5)
            {
                _warned5 = _warned10 = _warned30 = true;
                _announcer?.AnnounceInterrupt(Strings.DraftTimerHurry);
            }
            else if (!_warned10 && remaining <= Warn10)
            {
                // 10s left: interrupt card reading — queuing it could arrive after auto-pick.
                _warned10 = _warned30 = true;
                _announcer?.AnnounceInterrupt(Strings.DraftTimerWarning(10));
            }
            else if (!_warned30 && remaining <= Warn30)
            {
                // 30s left: gentle, non-interrupting nudge.
                _warned30 = true;
                _announcer?.Announce(Strings.DraftTimerWarning(30), AnnouncementPriority.Normal);
            }
        }

        /// <summary>
        /// Re-arm threshold warnings for a new pick window. Thresholds already passed at the
        /// window's start (e.g. when rejoining a draft mid-pick) are suppressed.
        /// </summary>
        private void ResetPickWarnings(float remaining)
        {
            _warned30 = remaining <= Warn30;
            _warned10 = remaining <= Warn10;
            _warned5 = remaining <= Warn5;
        }

        /// <summary>
        /// On-demand readout of the remaining pick time (E key).
        /// </summary>
        private void AnnouncePickTimer()
        {
            var pod = GetDraftPod();
            if (pod == null)
            {
                _announcer?.AnnounceInterrupt(Strings.DraftTimerUnavailable);
                return;
            }

            if (!IsHumanDraftPod(pod))
            {
                _announcer?.AnnounceInterrupt(Strings.DraftTimerNoLimit);
                return;
            }

            float total = GetPickSecondsTotal(pod);
            float remaining = GetPickSecondsRemaining(pod);
            if (total <= 0f || float.IsNaN(remaining) || remaining <= 0f)
            {
                _announcer?.AnnounceInterrupt(Strings.DraftTimerTimeUp);
                return;
            }

            int secs = Mathf.Max(1, Mathf.CeilToInt(remaining));
            _announcer?.AnnounceInterrupt(Strings.DraftTimerRemaining(secs));
        }

        private bool _closeTriggered;
        private int _closeRescanCounter;
        private int _emptyCardCounter; // Frames with 0 cards and no popup
        private const int EmptyCardDeactivateFrames = 300; // ~5 seconds at 60fps

        // Waiting-for-next-pack polling: when our pack is submitted the cards disappear
        // (0 cards) until other players finish picking and the next pack is passed to us.
        // The game fires no panel event for this, so poll for the new pack ourselves.
        private int _nextPackPollCounter;
        private int _lastNonZeroPeek; // Last peeked card count (used to confirm the pack is fully loaded)
        private bool _announcedWaiting; // True once the "waiting for other players" message was announced for this empty state
        private const int NextPackPollFrames = 30; // ~0.5 seconds at 60fps

        // Human-draft pick timer: the game auto-picks when the per-pick timer expires and gives
        // no sound, so we poll the live PickSecondsRemaining and announce threshold warnings.
        private int _timerPollCounter;
        private const int TimerPollFrames = 15; // ~0.25s at 60fps — enough for second-granularity warnings
        private float _lastPickRemaining = -1f; // last polled remaining seconds (-1 = no reading yet)
        private float _lastPickTotal = -1f;     // last polled total seconds (detects a new pick window)
        // Per-window "already announced" flags for the 30s / 10s / 5s thresholds.
        private bool _warned30, _warned10, _warned5;
        private const float Warn30 = 30f;
        private const float Warn10 = 10f;
        private const float Warn5 = 5f;

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
            _nextPackPollCounter = 0;
            _lastNonZeroPeek = 0;
            _announcedWaiting = false;
            _timerPollCounter = 0;
            _lastPickRemaining = -1f;
            _lastPickTotal = -1f;
            _warned30 = _warned10 = _warned5 = false;
            _lastToggledCard = null;
            _lastToggleTime = 0f;
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
                Log.Msg("{NavigatorId}", $"Draft controller no longer active");
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
            _draftCache.Clear();
        }
    }
}
