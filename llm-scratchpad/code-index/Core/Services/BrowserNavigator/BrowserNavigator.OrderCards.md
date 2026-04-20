# BrowserNavigator.OrderCards.cs
Path: src/Core/Services/BrowserNavigator/BrowserNavigator.OrderCards.cs
Lines: 288

## Top-level comments
- Feature partial for OrderCards / TriggerOrderCards browsers (library reorder, trigger stack reorder). Implements a two-step pick-up-and-place Enter model: first Enter grabs the current card, second Enter calls ShiftCards on the holder and syncs the browser via OnDragRelease. Filters placeholder cards (InstanceId==0) that serve as library boundary markers.

## public partial class BrowserNavigator (line 16)
### Fields
- private bool _isOrderCards (line 19)
- private int _orderGrabbedIndex (line 20) — index of card being moved (-1 = none grabbed)
- private static MethodInfo _shiftCardsMethod (line 23, static) — cached CardBrowserCardHolder.ShiftCards
- private static PropertyInfo _cardViewsProp (line 24, static)
- private static PropertyInfo _instanceIdProp (line 25, static)

### Methods
- private void FilterPlaceholderCards() (line 32) — removes cards with InstanceId==0 from _browserCards (library boundary markers)
- private void HandleOrderCardsActivation() (line 58) — first Enter picks up (_orderGrabbedIndex=currentIndex), second Enter calls PlaceOrderCard
- private void PlaceOrderCard() (line 85) — maps indices to holder CardViews indices via GetHolderCardViewIndex, calls ShiftCards, syncs browser via SyncOrderBrowserCardViews, refreshes list via RefreshOrderCardsFromHolder
- private int GetHolderCardViewIndex(Component holderComp, GameObject card) (line 178) — finds card's CDC in holder's CardViews list; caches _cardViewsProp
- private void SyncOrderBrowserCardViews(Component cardCDC) (line 202) — GameManager→BrowserManager→CurrentBrowser→OnDragRelease(cardCDC)
- private void RefreshOrderCardsFromHolder(Component holderComp) (line 255) — re-reads CardViews, skips InstanceId==0, reverses to LIFO order (position 1 = top of stack = resolves first)
