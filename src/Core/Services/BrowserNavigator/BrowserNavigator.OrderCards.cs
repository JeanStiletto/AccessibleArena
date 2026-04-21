using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Models;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    public partial class BrowserNavigator
    {
        // OrderCards browser state (card reordering for library/triggers)
        private bool _isOrderCards;
        private int _orderGrabbedIndex = -1; // Index of card being moved (-1 = none grabbed)

        // OrderCards reflection caches
        private sealed class HolderHandles
        {
            public MethodInfo ShiftCards;    // CardBrowserCardHolder.ShiftCards(int,int)
            public PropertyInfo CardViews;   // CardBrowserCardHolder.CardViews
        }

        private static readonly ReflectionCache<HolderHandles> _holderCache = new ReflectionCache<HolderHandles>(
            builder: holderType => new HolderHandles
            {
                ShiftCards = holderType.GetMethod("ShiftCards", PublicInstance),
                CardViews = holderType.GetProperty("CardViews", PublicInstance | BindingFlags.FlattenHierarchy),
            },
            validator: _ => true,
            logTag: "BrowserNavigator",
            logSubject: "CardBrowserCardHolder");

        private sealed class CdcHandles
        {
            public PropertyInfo InstanceId;  // DuelSceneCDC.InstanceId
        }

        private static readonly ReflectionCache<CdcHandles> _cdcCache = new ReflectionCache<CdcHandles>(
            builder: cdcType => new CdcHandles
            {
                InstanceId = cdcType.GetProperty("InstanceId", PublicInstance),
            },
            validator: _ => true,
            logTag: "BrowserNavigator",
            logSubject: "DuelSceneCDC");

        /// <summary>
        /// Filters out placeholder cards (InstanceId == 0) from the browser cards list.
        /// OrderCards browsers may include a library boundary placeholder that players
        /// should not interact with.
        /// </summary>
        private void FilterPlaceholderCards()
        {
            for (int i = _browserCards.Count - 1; i >= 0; i--)
            {
                var card = _browserCards[i];
                var cdc = CardDetector.GetDuelSceneCDC(card);
                if (cdc == null) continue;

                _cdcCache.EnsureInitialized(cdc.GetType());
                var instanceIdProp = _cdcCache.Handles?.InstanceId;
                if (instanceIdProp != null)
                {
                    var id = instanceIdProp.GetValue(cdc);
                    if ((id is uint uid && uid == 0) || (id is int iid && iid == 0))
                    {
                        Log.Msg("BrowserNavigator", $"Filtered placeholder card at index {i}");
                        _browserCards.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>
        /// Handles Enter key for OrderCards browsers: pick up or place a card.
        /// </summary>
        private void HandleOrderCardsActivation()
        {
            if (_currentCardIndex < 0 || _currentCardIndex >= _browserCards.Count)
            {
                _announcer.Announce(Strings.NoCardSelected, AnnouncementPriority.Normal);
                return;
            }

            if (_orderGrabbedIndex < 0)
            {
                // Pick up: record current index
                _orderGrabbedIndex = _currentCardIndex;
                var cardName = CardDetector.GetCardName(_browserCards[_currentCardIndex]) ?? "card";
                Log.Msg("BrowserNavigator", $"OrderCards: picked up '{cardName}' at index {_orderGrabbedIndex}");
                _announcer.Announce(Strings.OrderCardsPickedUp, AnnouncementPriority.Normal);
            }
            else
            {
                // Place: shift card from grabbed index to current index
                PlaceOrderCard();
            }
        }

        /// <summary>
        /// Places the grabbed card at the current navigation index.
        /// Calls ShiftCards on the holder and syncs the browser via OnDragRelease.
        /// </summary>
        private void PlaceOrderCard()
        {
            int fromIndex = _orderGrabbedIndex;
            int toIndex = _currentCardIndex;
            _orderGrabbedIndex = -1;

            if (fromIndex == toIndex)
            {
                // Same position — just announce placement
                _announcer.Announce(
                    Strings.OrderCardsPlaced(toIndex + 1, _browserCards.Count),
                    AnnouncementPriority.Normal);
                return;
            }

            var card = _browserCards[fromIndex];
            var cardName = CardDetector.GetCardName(card) ?? "card";
            var cdc = CardDetector.GetDuelSceneCDC(card);

            Log.Msg("BrowserNavigator", $"OrderCards: placing '{cardName}' from {fromIndex} to {toIndex}");

            // Find the holder and its CardBrowserCardHolder component
            var defaultHolder = BrowserDetector.FindActiveGameObject(BrowserDetector.HolderDefault);
            if (defaultHolder == null)
            {
                Log.Warn("BrowserNavigator", "OrderCards: default holder not found");
                _announcer.Announce(Strings.OrderCardsPlaced(toIndex + 1, _browserCards.Count), AnnouncementPriority.Normal);
                return;
            }

            Component holderComp = null;
            foreach (var comp in defaultHolder.GetComponents<Component>())
            {
                if (comp != null && comp.GetType().Name == T.CardBrowserCardHolder)
                {
                    holderComp = comp;
                    break;
                }
            }

            if (holderComp == null)
            {
                Log.Warn("BrowserNavigator", "OrderCards: CardBrowserCardHolder not found");
                _announcer.Announce(Strings.OrderCardsPlaced(toIndex + 1, _browserCards.Count), AnnouncementPriority.Normal);
                return;
            }

            // Map our filtered indices to holder CardViews indices (accounting for placeholders)
            int holderFromIndex = GetHolderCardViewIndex(holderComp, _browserCards[fromIndex]);
            int holderToIndex = GetHolderCardViewIndex(holderComp, _browserCards[toIndex]);

            if (holderFromIndex < 0 || holderToIndex < 0)
            {
                Log.Warn("BrowserNavigator", $"OrderCards: could not map indices (from={holderFromIndex}, to={holderToIndex})");
                _announcer.Announce(Strings.OrderCardsPlaced(toIndex + 1, _browserCards.Count), AnnouncementPriority.Normal);
                return;
            }

            // Call ShiftCards on the holder
            _holderCache.EnsureInitialized(holderComp.GetType());
            var shiftCards = _holderCache.Handles?.ShiftCards;
            if (shiftCards != null)
            {
                shiftCards.Invoke(holderComp, new object[] { holderFromIndex, holderToIndex });
                Log.Msg("BrowserNavigator", $"OrderCards: ShiftCards({holderFromIndex}, {holderToIndex})");
            }
            else
            {
                Log.Warn("BrowserNavigator", "OrderCards: ShiftCards method not found");
            }

            // Sync the browser's internal cardViews list via OnDragRelease
            if (cdc != null)
            {
                SyncOrderBrowserCardViews(cdc);
            }

            // Refresh our card list from the holder to match new order
            RefreshOrderCardsFromHolder(holderComp);

            // Keep cursor at the target position
            _currentCardIndex = toIndex;

            _announcer.Announce(
                Strings.OrderCardsPlaced(toIndex + 1, _browserCards.Count),
                AnnouncementPriority.Normal);
        }

        /// <summary>
        /// Maps a card GameObject to its index in the holder's CardViews list.
        /// Necessary because our _browserCards may skip placeholder cards (InstanceId==0).
        /// </summary>
        private int GetHolderCardViewIndex(Component holderComp, GameObject card)
        {
            _holderCache.EnsureInitialized(holderComp.GetType());
            var cardViewsList = _holderCache.Handles?.CardViews?.GetValue(holderComp) as System.Collections.IList;
            if (cardViewsList == null) return -1;

            var cdc = CardDetector.GetDuelSceneCDC(card);
            if (cdc == null) return -1;

            for (int i = 0; i < cardViewsList.Count; i++)
            {
                if (cardViewsList[i] as Component == cdc)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Syncs the browser's cardViews list from the holder after a reorder.
        /// Calls OnDragRelease on GameManager.BrowserManager.CurrentBrowser.
        /// </summary>
        private void SyncOrderBrowserCardViews(Component cardCDC)
        {
            try
            {
                if (!TryGetCurrentBrowser("OrderCards", out var currentBrowser))
                    return;

                var onDragRelease = currentBrowser.GetType().GetMethod("OnDragRelease", PublicInstance);
                if (onDragRelease != null)
                {
                    onDragRelease.Invoke(currentBrowser, new object[] { cardCDC });
                    Log.Msg("BrowserNavigator", "OrderCards: browser synced via OnDragRelease");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("BrowserNavigator", $"OrderCards: error syncing browser: {ex.Message}");
            }
        }

        /// <summary>
        /// Refreshes the browser cards list from the holder's CardViews after a reorder.
        /// Maintains order consistency between our navigation list and the actual holder order.
        /// </summary>
        private void RefreshOrderCardsFromHolder(Component holderComp)
        {
            _holderCache.EnsureInitialized(holderComp.GetType());
            var cardViewsList = _holderCache.Handles?.CardViews?.GetValue(holderComp) as System.Collections.IList;
            if (cardViewsList == null) return;

            _browserCards.Clear();
            foreach (var item in cardViewsList)
            {
                var cdcComp = item as Component;
                if (cdcComp == null) continue;

                // Skip placeholder cards (InstanceId == 0)
                _cdcCache.EnsureInitialized(cdcComp.GetType());
                var instanceIdProp = _cdcCache.Handles?.InstanceId;
                if (instanceIdProp != null)
                {
                    var id = instanceIdProp.GetValue(cdcComp);
                    if ((id is uint uid && uid == 0) || (id is int iid && iid == 0))
                        continue;
                }

                _browserCards.Add(cdcComp.gameObject);
            }

            // Reverse so position 1 (leftmost) = top of stack = resolves first
            _browserCards.Reverse();
        }
    }
}
