using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AccessibleArena.Core.Models;
using T = AccessibleArena.Core.Constants.GameTypeNames;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Provides access to deck list and read-only deck card data.
    /// Handles reflection-based property access for MainDeck, Sideboard,
    /// and StaticColumn (read-only) card holders.
    /// </summary>
    public static class DeckCardProvider
    {
        #region Deck List Card Support

        /// <summary>
        /// Information about a card in the deck list (MainDeck_MetaCardHolder).
        /// </summary>
        public struct DeckListCardInfo
        {
            public uint GrpId;
            public int Quantity;
            public GameObject TileButton;
            public GameObject TagButton;
            public GameObject CardTileBase;
            public GameObject ViewGameObject; // The ListMetaCardView_Expanding's gameObject
            public bool IsValid => GrpId != 0;
        }

        // Cache for deck list cards to avoid repeated reflection
        private static List<DeckListCardInfo> _cachedDeckListCards = new List<DeckListCardInfo>();
        private static GameObject _cachedDeckHolder = null;
        internal static GameObject CachedDeckHolder => _cachedDeckHolder;
        private static int _cachedDeckListFrame = -1;

        // Cache for sideboard cards (draft/sealed deck builder)
        private static List<DeckListCardInfo> _cachedSideboardCards = new List<DeckListCardInfo>();
        private static int _cachedSideboardFrame = -1;

        /// <summary>
        /// Clears the deck list card cache, forcing a fresh lookup on next call.
        /// Call this when entering the DeckBuilderDeckList group to ensure fresh data.
        /// </summary>
        public static void ClearDeckListCache()
        {
            _cachedDeckListCards.Clear();
            _cachedDeckHolder = null;
            _cachedDeckListFrame = -1;
            _cachedSideboardCards.Clear();
            _cachedSideboardFrame = -1;
        }

        /// <summary>
        /// Gets all cards from the MainDeck_MetaCardHolder with their GrpIds and quantities.
        /// Uses caching to avoid repeated reflection calls within the same frame.
        /// </summary>
        public static List<DeckListCardInfo> GetDeckListCards()
        {
            // Return cached result if same frame
            if (_cachedDeckListFrame == Time.frameCount && _cachedDeckListCards.Count > 0)
                return _cachedDeckListCards;

            _cachedDeckListCards.Clear();
            _cachedDeckListFrame = Time.frameCount;

            try
            {
                // Find MainDeck_MetaCardHolder
                // Note: GameObject.Find only finds active objects, but the deck holder may be inactive
                // when entering deck builder without a popup dialog. In that case, we search for it
                // including inactive objects and activate it.
                var deckHolder = GameObject.Find("MainDeck_MetaCardHolder");
                if (deckHolder == null)
                {
                    // Search for inactive holder
                    var allTransforms = GameObject.FindObjectsOfType<Transform>(true);
                    foreach (var t in allTransforms)
                    {
                        if (t.name == "MainDeck_MetaCardHolder")
                        {
                            deckHolder = t.gameObject;
                            // Activate the holder so we can access its components
                            deckHolder.SetActive(true);
                            break;
                        }
                    }

                    if (deckHolder == null)
                    {
                        return _cachedDeckListCards;
                    }
                }

                _cachedDeckHolder = deckHolder;

                // Find ListMetaCardHolder_Expanding component
                MonoBehaviour holderComponent = null;
                foreach (var mb in deckHolder.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name.Contains(T.ListMetaCardHolder))
                    {
                        holderComponent = mb;
                        break;
                    }
                }

                if (holderComponent == null)
                {
                    return _cachedDeckListCards;
                }

                // Get CardViews property
                var holderType = holderComponent.GetType();
                var cardViewsProp = holderType.GetProperty("CardViews");
                if (cardViewsProp == null)
                {
                    return _cachedDeckListCards;
                }

                var cardViews = cardViewsProp.GetValue(holderComponent) as System.Collections.IEnumerable;
                if (cardViews == null)
                {
                    return _cachedDeckListCards;
                }

                ExtractCardViewsInto(cardViews, _cachedDeckListCards);
            }
            catch (Exception ex)
            {
                Log.Card("DeckCardProvider", $"Error getting deck list cards: {ex.Message}");
            }

            return _cachedDeckListCards;
        }

        /// <summary>
        /// Gets all sideboard cards from non-MainDeck holders inside MetaCardHolders_Container.
        /// Used in draft/sealed deck building where sideboard cards are in a separate holder.
        /// </summary>
        public static List<DeckListCardInfo> GetSideboardCards()
        {
            // Return cached result if same frame
            if (_cachedSideboardFrame == Time.frameCount && _cachedSideboardCards.Count > 0)
                return _cachedSideboardCards;

            _cachedSideboardCards.Clear();
            _cachedSideboardFrame = Time.frameCount;

            try
            {
                // Find the MetaCardHolders_Container parent
                // Strategy: start from _cachedDeckHolder (MainDeck_MetaCardHolder) and walk up
                if (_cachedDeckHolder == null)
                    GetDeckListCards(); // Populate _cachedDeckHolder

                Transform holdersContainer = null;

                if (_cachedDeckHolder != null)
                {
                    // Walk up from MainDeck_MetaCardHolder to find MetaCardHolders_Container
                    Transform current = _cachedDeckHolder.transform.parent;
                    while (current != null)
                    {
                        if (current.name.Contains("MetaCardHolders_Container"))
                        {
                            holdersContainer = current;
                            break;
                        }
                        current = current.parent;
                    }
                }

                // Fallback: search scene for MetaCardHolders_Container
                if (holdersContainer == null)
                {
                    var containerObj = GameObject.Find("MetaCardHolders_Container");
                    if (containerObj != null)
                        holdersContainer = containerObj.transform;
                }

                if (holdersContainer == null)
                {
                    return _cachedSideboardCards;
                }

                // Search all children for ListMetaCardHolder components that aren't on MainDeck_MetaCardHolder
                foreach (Transform child in holdersContainer)
                {
                    if (child == null || child.name == "MainDeck_MetaCardHolder")
                        continue;

                    // Find ListMetaCardHolder component on this child
                    MonoBehaviour holderComponent = null;
                    foreach (var mb in child.GetComponents<MonoBehaviour>())
                    {
                        if (mb != null && mb.GetType().Name.Contains(T.ListMetaCardHolder))
                        {
                            holderComponent = mb;
                            break;
                        }
                    }

                    if (holderComponent == null)
                        continue;

                    MelonLogger.Msg($"[DeckCardProvider] Found sideboard holder: '{child.name}' with component {holderComponent.GetType().Name}");

                    // Get CardViews property (same pattern as GetDeckListCards)
                    var holderType = holderComponent.GetType();
                    var cardViewsProp = holderType.GetProperty("CardViews");
                    if (cardViewsProp == null)
                        continue;

                    var cardViews = cardViewsProp.GetValue(holderComponent) as System.Collections.IEnumerable;
                    if (cardViews == null)
                        continue;

                    ExtractCardViewsInto(cardViews, _cachedSideboardCards);
                }

                if (_cachedSideboardCards.Count > 0)
                {
                    MelonLogger.Msg($"[DeckCardProvider] Found {_cachedSideboardCards.Count} sideboard card(s)");
                }
            }
            catch (Exception ex)
            {
                Log.Card("DeckCardProvider", $"Error getting sideboard cards: {ex.Message}");
            }

            return _cachedSideboardCards;
        }

        /// <summary>
        /// Gets deck list card info for a specific UI element (TileButton, TagButton, or CardTileBase).
        /// Returns null if the element is not a deck list card.
        /// </summary>
        public static DeckListCardInfo? GetDeckListCardInfo(GameObject element)
        {
            if (element == null) return null;

            // Check if this element is part of a deck card tile
            // It could be the TileButton, TagButton, or CardTileBase itself
            var deckCards = GetDeckListCards();

            foreach (var card in deckCards)
            {
                if (card.TileButton == element ||
                    card.TagButton == element ||
                    card.CardTileBase == element ||
                    card.ViewGameObject == element)
                {
                    return card;
                }

                // Check if element is a child of ViewGameObject (the ListMetaCardView_Expanding's gameObject)
                if (card.ViewGameObject != null && element.transform.IsChildOf(card.ViewGameObject.transform))
                {
                    return card;
                }

                // Also check if element is a child of the CardTileBase (fallback)
                if (card.CardTileBase != null && element.transform.IsChildOf(card.CardTileBase.transform))
                {
                    return card;
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if an element is a deck list card (in MainDeck_MetaCardHolder).
        /// </summary>
        public static bool IsDeckListCard(GameObject element)
        {
            return GetDeckListCardInfo(element) != null;
        }

        /// <summary>
        /// Checks if an element is a sideboard card (in non-MainDeck holder).
        /// </summary>
        public static bool IsSideboardCard(GameObject element)
        {
            return GetSideboardCardInfo(element) != null;
        }

        /// <summary>
        /// Gets sideboard card info for a specific UI element.
        /// Returns null if the element is not a sideboard card.
        /// </summary>
        public static DeckListCardInfo? GetSideboardCardInfo(GameObject element)
        {
            if (element == null) return null;

            var sideboardCards = GetSideboardCards();
            foreach (var card in sideboardCards)
            {
                if (card.TileButton == element ||
                    card.TagButton == element ||
                    card.CardTileBase == element ||
                    card.ViewGameObject == element)
                {
                    return card;
                }

                if (card.ViewGameObject != null && element.transform.IsChildOf(card.ViewGameObject.transform))
                    return card;
                if (card.CardTileBase != null && element.transform.IsChildOf(card.CardTileBase.transform))
                    return card;
            }

            return null;
        }

        /// <summary>
        /// Extracts full card info from a sideboard card element.
        /// Uses the same logic as ExtractDeckListCardInfo.
        /// </summary>
        public static CardInfo? ExtractSideboardCardInfo(GameObject element)
        {
            var sideCardInfo = GetSideboardCardInfo(element);
            if (sideCardInfo == null || !sideCardInfo.Value.IsValid)
                return null;

            var info = sideCardInfo.Value;

            if (info.ViewGameObject != null)
            {
                var cardInfo = CardModelProvider.ExtractCardInfoFromModel(info.ViewGameObject);
                if (cardInfo.HasValue && cardInfo.Value.IsValid)
                {
                    var result = cardInfo.Value;
                    result.Quantity = info.Quantity;
                    return result;
                }
            }

            string name = CardModelProvider.GetNameFromGrpId(info.GrpId);
            return new CardInfo
            {
                Name = name ?? $"Card #{info.GrpId}",
                Quantity = info.Quantity,
                IsValid = true
            };
        }

        // Cache for ShowUnCollectedTreatment field (public field on MetaCardView base class)
        private static FieldInfo _showUnCollectedField = null;
        private static bool _showUnCollectedFieldSearched = false;

        /// <summary>
        /// Checks if a deck list card entry represents unowned (missing) copies
        /// by reading MetaCardView.ShowUnCollectedTreatment (set by SetDisplayInformation).
        /// </summary>
        private static bool CheckDeckListCardUnowned(GameObject viewGameObject)
        {
            if (viewGameObject == null) return false;

            try
            {
                var metaCardView = CardModelProvider.GetMetaCardView(viewGameObject);
                if (metaCardView == null) return false;

                // ShowUnCollectedTreatment is a public field on MetaCardView (base class)
                // It's set by SetDisplayInformation from displayInformation.Unowned
                if (!_showUnCollectedFieldSearched)
                {
                    _showUnCollectedFieldSearched = true;
                    _showUnCollectedField = metaCardView.GetType().GetField("ShowUnCollectedTreatment");
                }

                if (_showUnCollectedField != null)
                    return (bool)_showUnCollectedField.GetValue(metaCardView);
            }
            catch (Exception ex)
            {
                Log.Card("DeckCardProvider",
                    $"Error checking deck list card unowned: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Extracts full card info for a deck list card using its GrpId.
        /// Includes the quantity from the deck list.
        /// Uses ExtractCardInfoFromModel for consistent extraction with other card types.
        /// </summary>
        public static CardInfo? ExtractDeckListCardInfo(GameObject element)
        {
            var deckCardInfo = GetDeckListCardInfo(element);
            if (deckCardInfo == null || !deckCardInfo.Value.IsValid)
                return null;

            var info = deckCardInfo.Value;

            // Check if this deck list entry is unowned (missing copies)
            bool isUnowned = CheckDeckListCardUnowned(info.ViewGameObject);

            // Use ExtractCardInfoFromModel on the ViewGameObject for consistent extraction
            // This uses the same logic as collection cards, duel cards, etc.
            if (info.ViewGameObject != null)
            {
                var cardInfo = CardModelProvider.ExtractCardInfoFromModel(info.ViewGameObject);
                if (cardInfo.HasValue && cardInfo.Value.IsValid)
                {
                    var result = cardInfo.Value;
                    result.Quantity = info.Quantity;
                    result.IsUnowned = isUnowned;
                    return result;
                }
            }

            // Fallback: return minimal info with just name and quantity
            string name = CardModelProvider.GetNameFromGrpId(info.GrpId);
            return new CardInfo
            {
                Name = name ?? $"Card #{info.GrpId}",
                Quantity = info.Quantity,
                IsUnowned = isUnowned,
                IsValid = true
            };
        }

        #endregion

        #region ReadOnly Deck Card Support

        /// <summary>
        /// Information about a card in a read-only deck (StaticColumnMetaCardView in column view).
        /// </summary>
        public struct ReadOnlyDeckCardInfo
        {
            public uint GrpId;
            public int Quantity;
            public GameObject CardGameObject; // StaticColumnMetaCardView GameObject
            public bool IsValid => GrpId > 0 && CardGameObject != null;
        }

        // Cache for read-only deck cards
        private static List<ReadOnlyDeckCardInfo> _cachedReadOnlyDeckCards = new List<ReadOnlyDeckCardInfo>();
        private static int _cachedReadOnlyDeckFrame = -1;

        /// <summary>
        /// Clears the read-only deck card cache, forcing a fresh lookup on next call.
        /// </summary>
        public static void ClearReadOnlyDeckCache()
        {
            _cachedReadOnlyDeckCards.Clear();
            _cachedReadOnlyDeckFrame = -1;
        }

        /// <summary>
        /// Gets all cards from the read-only deck column view (StaticColumnMetaCardView).
        /// Used when DeckBuilderMode.ReadOnly is active (starter/precon decks).
        /// Uses caching to avoid repeated reflection calls within the same frame.
        /// </summary>
        public static List<ReadOnlyDeckCardInfo> GetReadOnlyDeckCards()
        {
            // Return cached result if same frame
            if (_cachedReadOnlyDeckFrame == Time.frameCount && _cachedReadOnlyDeckCards.Count > 0)
                return _cachedReadOnlyDeckCards;

            _cachedReadOnlyDeckCards.Clear();
            _cachedReadOnlyDeckFrame = Time.frameCount;

            try
            {
                // Find StaticColumnMetaCardHolder components directly in the scene
                // (The "StaticColumnManager" GO is just a container - the component is on child holders)
                var holders = new List<MonoBehaviour>();
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>(true))
                {
                    if (mb != null && mb.GetType().Name == T.StaticColumnMetaCardHolder)
                        holders.Add(mb);
                }

                if (holders.Count == 0)
                {
                    Log.Card("DeckCardProvider",
                        "No StaticColumnMetaCardHolder components found");
                    return _cachedReadOnlyDeckCards;
                }

                Log.Card("DeckCardProvider",
                    $"Found {holders.Count} StaticColumnMetaCardHolder(s)");

                // Extract card views from each holder
                foreach (var holder in holders)
                {
                    var holderType = holder.GetType();

                    // Get CardViews property (public, inherited)
                    var cardViewsProp = holderType.GetProperty("CardViews");
                    if (cardViewsProp == null)
                    {
                        Log.Card("DeckCardProvider",
                            $"CardViews property not found on {holderType.Name}");
                        continue;
                    }

                    var cardViews = cardViewsProp.GetValue(holder) as System.Collections.IEnumerable;
                    if (cardViews == null) continue;

                    foreach (var cardView in cardViews)
                    {
                        if (cardView == null) continue;

                        var viewType = cardView.GetType();
                        var info = new ReadOnlyDeckCardInfo();

                        // Store the view's gameObject
                        if (cardView is Component viewComponent)
                        {
                            info.CardGameObject = viewComponent.gameObject;
                        }

                        // Get Card property which has GrpId
                        var cardProp = viewType.GetProperty("Card");
                        if (cardProp != null)
                        {
                            var card = cardProp.GetValue(cardView);
                            if (card != null)
                            {
                                var cardType = card.GetType();
                                var grpIdProp = cardType.GetProperty("GrpId");
                                if (grpIdProp != null)
                                {
                                    info.GrpId = (uint)grpIdProp.GetValue(card);
                                }
                            }
                        }

                        // Get Quantity property
                        var qtyProp = viewType.GetProperty("Quantity");
                        if (qtyProp != null)
                        {
                            info.Quantity = (int)qtyProp.GetValue(cardView);
                        }

                        if (info.IsValid)
                        {
                            _cachedReadOnlyDeckCards.Add(info);
                        }
                    }
                }

                Log.Card("DeckCardProvider",
                    $"Found {_cachedReadOnlyDeckCards.Count} read-only deck card(s)");
            }
            catch (Exception ex)
            {
                Log.Card("DeckCardProvider",
                    $"Error getting read-only deck cards: {ex.Message}");
            }

            return _cachedReadOnlyDeckCards;
        }

        /// <summary>
        /// Gets read-only deck card info for a specific UI element.
        /// Returns null if the element is not a read-only deck card.
        /// </summary>
        public static ReadOnlyDeckCardInfo? GetReadOnlyDeckCardInfo(GameObject element)
        {
            if (element == null) return null;

            var cards = GetReadOnlyDeckCards();
            foreach (var card in cards)
            {
                if (card.CardGameObject == element)
                    return card;

                // Check if element is a child of the card view
                if (card.CardGameObject != null && element.transform.IsChildOf(card.CardGameObject.transform))
                    return card;
            }

            return null;
        }

        /// <summary>
        /// Extracts full card info for a read-only deck card using its GrpId.
        /// </summary>
        public static CardInfo? ExtractReadOnlyDeckCardInfo(GameObject element)
        {
            var readOnlyCard = GetReadOnlyDeckCardInfo(element);
            if (readOnlyCard == null || !readOnlyCard.Value.IsValid)
                return null;

            var info = readOnlyCard.Value;

            // Use ExtractCardInfoFromModel for consistent extraction
            if (info.CardGameObject != null)
            {
                var cardInfo = CardModelProvider.ExtractCardInfoFromModel(info.CardGameObject);
                if (cardInfo.HasValue && cardInfo.Value.IsValid)
                {
                    var result = cardInfo.Value;
                    result.Quantity = info.Quantity;
                    return result;
                }
            }

            // Fallback: return minimal info with just name and quantity
            string name = CardModelProvider.GetNameFromGrpId(info.GrpId);
            return new CardInfo
            {
                Name = name ?? $"Card #{info.GrpId}",
                Quantity = info.Quantity,
                IsValid = true
            };
        }

        #endregion

        #region Commander Card Support

        /// <summary>
        /// Information about a commander/companion card in the deck builder.
        /// </summary>
        public struct CommanderCardInfo
        {
            public uint GrpId;
            public int Quantity;
            public GameObject CardGameObject; // ListCommanderView gameObject (for card info extraction)
            public GameObject TileButton;     // CustomButton - Tile (navigable element)
            public GameObject TagButton;      // CustomButton - Tag (to be excluded from scan)
            public bool IsCompanion;
            public bool IsPartner;
            public bool IsValid => GrpId != 0 && CardGameObject != null;
        }

        // Cache for commander cards
        private static List<CommanderCardInfo> _cachedCommanderCards = new List<CommanderCardInfo>();
        private static int _cachedCommanderFrame = -1;

        /// <summary>
        /// Gets all commander/companion cards from ListCommanderHolder components (list view).
        /// Uses caching to avoid repeated reflection calls within the same frame.
        /// </summary>
        public static List<CommanderCardInfo> GetCommanderCards()
        {
            if (_cachedCommanderFrame == Time.frameCount && _cachedCommanderCards.Count > 0)
                return _cachedCommanderCards;

            _cachedCommanderCards.Clear();
            _cachedCommanderFrame = Time.frameCount;

            try
            {
                // Find ListCommanderHolder components (the list view holders, not the column view ones)
                var holders = new List<MonoBehaviour>();
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>(true))
                {
                    if (mb != null && mb.GetType().Name == T.ListCommanderHolder)
                        holders.Add(mb);
                }

                MelonLogger.Msg($"[DeckCardProvider] Found {holders.Count} ListCommanderHolder(s)");
                if (holders.Count == 0)
                    return _cachedCommanderCards;

                foreach (var holder in holders)
                {
                    var holderType = holder.GetType();
                    string holderName = holder.gameObject.name;
                    bool active = holder.gameObject.activeInHierarchy;

                    // Check IsEmpty property (public, private setter)
                    var isEmptyProp = holderType.GetProperty("IsEmpty");
                    bool isEmpty = true;
                    if (isEmptyProp != null)
                        isEmpty = (bool)isEmptyProp.GetValue(holder);

                    MelonLogger.Msg($"[DeckCardProvider] ListCommanderHolder '{holderName}' active={active} isEmpty={isEmpty}");

                    if (!active || isEmpty)
                        continue;

                    // Get _cardInstance field (private ListCommanderView)
                    var cardInstanceField = holderType.GetField("_cardInstance", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (cardInstanceField == null)
                    {
                        MelonLogger.Msg($"[DeckCardProvider]   _cardInstance field NOT FOUND");
                        continue;
                    }

                    var cardView = cardInstanceField.GetValue(holder);
                    if (cardView == null)
                    {
                        MelonLogger.Msg($"[DeckCardProvider]   _cardInstance is null");
                        continue;
                    }

                    MelonLogger.Msg($"[DeckCardProvider]   _cardInstance type={cardView.GetType().Name}");

                    // ListCommanderView extends ListMetaCardView_Expanding — same extraction as deck list
                    var info = new CommanderCardInfo();
                    var viewType = cardView.GetType();

                    if (cardView is Component viewComponent)
                        info.CardGameObject = viewComponent.gameObject;

                    // Card.GrpId
                    var cardProp = viewType.GetProperty("Card");
                    if (cardProp != null)
                    {
                        var card = cardProp.GetValue(cardView);
                        if (card != null)
                        {
                            var grpIdProp = card.GetType().GetProperty("GrpId");
                            if (grpIdProp != null)
                                info.GrpId = (uint)grpIdProp.GetValue(card);
                        }
                    }

                    // Quantity
                    var qtyProp = viewType.GetProperty("Quantity");
                    if (qtyProp != null)
                        info.Quantity = (int)qtyProp.GetValue(cardView);

                    // TileButton (CustomButton - Tile)
                    var tileBtnProp = viewType.GetProperty("TileButton");
                    if (tileBtnProp?.GetValue(cardView) is Component tileBtn)
                        info.TileButton = tileBtn.gameObject;

                    // TagButton (CustomButton - Tag)
                    var tagBtnProp = viewType.GetProperty("TagButton");
                    if (tagBtnProp?.GetValue(cardView) is Component tagBtn)
                        info.TagButton = tagBtn.gameObject;

                    // IsCompanion / IsPartner on holder
                    var companionField = holderType.GetField("IsCompanion", BindingFlags.Public | BindingFlags.Instance);
                    if (companionField != null)
                        info.IsCompanion = (bool)companionField.GetValue(holder);

                    var partnerField = holderType.GetField("IsPartner", BindingFlags.Public | BindingFlags.Instance);
                    if (partnerField != null)
                        info.IsPartner = (bool)partnerField.GetValue(holder);

                    MelonLogger.Msg($"[DeckCardProvider]   Extracted: GrpId={info.GrpId}, Qty={info.Quantity}, Tile={info.TileButton?.name}, Tag={info.TagButton?.name}, Valid={info.IsValid}");
                    if (info.IsValid)
                        _cachedCommanderCards.Add(info);
                }

                MelonLogger.Msg($"[DeckCardProvider] Commander cards found: {_cachedCommanderCards.Count}");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[DeckCardProvider] Error getting commander cards: {ex.Message}\n{ex.StackTrace}");
            }

            return _cachedCommanderCards;
        }

        /// <summary>
        /// Gets commander card info for a specific UI element.
        /// Returns null if the element is not a commander card.
        /// </summary>
        public static CommanderCardInfo? GetCommanderCardInfo(GameObject element)
        {
            if (element == null) return null;

            var cards = GetCommanderCards();
            foreach (var card in cards)
            {
                if (card.CardGameObject == element ||
                    card.TileButton == element ||
                    card.TagButton == element)
                    return card;

                // Check if element is a child of the card view
                if (card.CardGameObject != null && element.transform.IsChildOf(card.CardGameObject.transform))
                    return card;
            }

            return null;
        }

        /// <summary>
        /// Extracts full card info for a commander/companion card.
        /// Sets the IsCommander flag on the returned CardInfo.
        /// </summary>
        public static CardInfo? ExtractCommanderCardInfo(GameObject element)
        {
            var commanderCard = GetCommanderCardInfo(element);
            if (commanderCard == null || !commanderCard.Value.IsValid)
                return null;

            var cmdInfo = commanderCard.Value;

            // Use ExtractCardInfoFromModel for consistent extraction
            if (cmdInfo.CardGameObject != null)
            {
                var cardInfo = CardModelProvider.ExtractCardInfoFromModel(cmdInfo.CardGameObject);
                if (cardInfo.HasValue && cardInfo.Value.IsValid)
                {
                    var result = cardInfo.Value;
                    result.Quantity = cmdInfo.Quantity;
                    result.IsCommander = !cmdInfo.IsCompanion;
                    result.IsCompanion = cmdInfo.IsCompanion;
                    return result;
                }
            }

            // Fallback: return minimal info with just name and quantity
            string name = CardModelProvider.GetNameFromGrpId(cmdInfo.GrpId);
            return new CardInfo
            {
                Name = name ?? $"Card #{cmdInfo.GrpId}",
                Quantity = cmdInfo.Quantity,
                IsCommander = !cmdInfo.IsCompanion,
                IsCompanion = cmdInfo.IsCompanion,
                IsValid = true
            };
        }

        /// <summary>
        /// Clears the commander card cache.
        /// </summary>
        public static void ClearCommanderCache()
        {
            _cachedCommanderCards.Clear();
            _cachedCommanderFrame = -1;
        }

        #endregion

        /// <summary>
        /// Clears all caches (deck list and read-only deck).
        /// Call this on scene changes or when deck data may have changed.
        /// </summary>
        public static void ClearCache()
        {
            ClearDeckListCache();
            ClearReadOnlyDeckCache();
            ClearCommanderCache();
            _showUnCollectedField = null;
            _showUnCollectedFieldSearched = false;
        }

        /// <summary>
        /// Extracts DeckListCardInfo from each card view in an enumerable and appends valid entries to the target list.
        /// Shared by GetDeckListCards and GetSideboardCards.
        /// </summary>
        private static void ExtractCardViewsInto(System.Collections.IEnumerable cardViews, List<DeckListCardInfo> target)
        {
            foreach (var cardView in cardViews)
            {
                if (cardView == null) continue;

                var viewType = cardView.GetType();
                var info = new DeckListCardInfo();

                if (cardView is Component viewComponent)
                    info.ViewGameObject = viewComponent.gameObject;

                // Card.GrpId
                var cardProp = viewType.GetProperty("Card");
                if (cardProp != null)
                {
                    var card = cardProp.GetValue(cardView);
                    if (card != null)
                    {
                        var grpIdProp = card.GetType().GetProperty("GrpId");
                        if (grpIdProp != null)
                            info.GrpId = (uint)grpIdProp.GetValue(card);
                    }
                }

                // Quantity
                var qtyProp = viewType.GetProperty("Quantity");
                if (qtyProp != null)
                    info.Quantity = (int)qtyProp.GetValue(cardView);

                // TileButton (card name button)
                var tileBtnProp = viewType.GetProperty("TileButton");
                if (tileBtnProp?.GetValue(cardView) is Component tileBtn)
                    info.TileButton = tileBtn.gameObject;

                // TagButton (quantity button)
                var tagBtnProp = viewType.GetProperty("TagButton");
                if (tagBtnProp?.GetValue(cardView) is Component tagBtn)
                    info.TagButton = tagBtn.gameObject;

                // CardTile_Base via CanvasGroup
                var canvasGroupProp = viewType.GetProperty("CanvasGroup");
                if (canvasGroupProp?.GetValue(cardView) is Component canvasGroup)
                    info.CardTileBase = canvasGroup.gameObject;

                if (info.IsValid)
                    target.Add(info);
            }
        }
    }
}
