# BrowserZoneNavigator.cs

## Enum: BrowserZoneType (line 15)
- None (line 17)
- Top (line 18) - Scry: keep on top / London: keep pile (hand)
- Bottom (line 19) - Scry: put on bottom / London: bottom pile (library)

## Class: BrowserZoneNavigator (line 27)
Handles two-zone navigation for Scry/Surveil and London mulligan browsers.
Both browser types use the same navigation pattern (C/D for zones, Left/Right for cards)
but have different activation APIs.

### Fields
- readonly IAnnouncementService _announcer (line 29)
- bool _isActive (line 32)
- string _browserType (line 33)
- BrowserZoneType _currentZone (line 34)
- int _cardIndex (line 35)
- List<GameObject> _topCards (line 38)
- List<GameObject> _bottomCards (line 39)
- int _mulliganCount (line 42)

### Properties (line 49-69)
- bool IsActive (line 51)
- BrowserZoneType CurrentZone (line 52)
- int CurrentCardIndex (line 53)
- int MulliganCount (line 54)
- GameObject CurrentCard (line 56)
- int TopCardCount (line 68)
- int BottomCardCount (line 69)

### Constructor
- BrowserZoneNavigator(IAnnouncementService announcer) (line 45)

### Lifecycle Methods (line 73-128)
- void Activate(BrowserInfo browserInfo) (line 77)
- void Deactivate() (line 92)
- void IncrementMulliganCount() (line 114)
- void ResetMulliganState() (line 123)

### Input Handling (line 131-191)
- bool HandleInput() (line 137)

### Zone Navigation (line 194-345)
- void EnterZone(BrowserZoneType zone) (line 199)
- void NavigateNext() (line 234)
- void NavigatePrevious() (line 259)
- void NavigateFirst() (line 283)
- void NavigateLast() (line 306)
- void AnnounceCurrentCard() (line 330)

### Card Activation (line 348-431)
- void ActivateCurrentCard() (line 353)
- IEnumerator RefreshZoneAfterDelay(string movedCardName) (line 383)

### Card List Refresh (line 434-661)
- void RefreshCardLists() (line 439)
- void RefreshSurveilCardLists() (line 460)
- void RefreshScryCardLists() (line 511)
  Note: Splits cards at placeholder (InstanceId == 0)
- void RefreshLondonCardLists() (line 586)

### Browser-Specific Activation (line 665-974)
- bool TryActivateCardViaDragSimulation(GameObject card, string cardName) (line 672)
- bool TryActivateViaDragSimulation(object browser, GameObject card, Component cardCDC) (line 706)
- bool TryActivateViaScryReorder(GameObject card, string cardName, Component cardCDC) (line 744)
  Note: Scry uses card reordering around placeholder divider
- void SyncBrowserCardViews(Component cardCDC) (line 847)
- bool PositionCardAtTargetZone(object browser, GameObject card, Component cardCDC) (line 907)
- object GetBrowserController() (line 980)

### Helper Methods (line 996-1042)
- List<GameObject> GetCurrentZoneCards() (line 997)
- string GetZoneName(BrowserZoneType zone) (line 1002)
- string GetShortZoneName(BrowserZoneType zone) (line 1014)
- Component GetCardBrowserHolderComponent(GameObject holder) (line 1029)

### State for BrowserNavigator (line 1045-1212)
- string GetLondonEntryAnnouncement(int cardCount) (line 1050)
- string GetCardSelectionState(GameObject card) (line 1062)
- BrowserZoneType DetectCardZone(GameObject card) (line 1084)
- BrowserZoneType DetectLondonCardZone(GameObject card) (line 1119)
- bool ActivateCardFromGenericNavigation(GameObject card) (line 1160)
  Note: Called by BrowserNavigator when user presses Enter during Tab navigation
- IEnumerator RefreshAfterGenericActivation() (line 1206)
