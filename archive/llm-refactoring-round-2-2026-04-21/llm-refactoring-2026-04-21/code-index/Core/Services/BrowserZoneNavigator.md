# BrowserZoneNavigator.cs
Path: src/Core/Services/BrowserZoneNavigator.cs
Lines: 1385

## public enum BrowserZoneType (line 16)

- None (line 18)
- Top (line 19) — Scry: keep on top / London: keep pile / Split: pile 1
- Bottom (line 20) — Scry: put on bottom / London: bottom pile / Split: pile 2

## public class BrowserZoneNavigator (line 28)

Two-zone navigation for Scry/Surveil, Split, and London mulligan browsers. C/D for zones, Left/Right for cards.

### Fields
- private readonly IAnnouncementService _announcer (line 30)
- private bool _isActive (line 33)
- private string _browserType (line 34)
- private BrowserZoneType _currentZone = BrowserZoneType.None (line 35)
- private int _cardIndex = -1 (line 36)
- private List<GameObject> _topCards (line 39)
- private List<GameObject> _bottomCards (line 40)
- private int _mulliganCount = 0 (line 43) — London-specific

### Properties
- public bool IsActive => _isActive (line 52)
- public BrowserZoneType CurrentZone => _currentZone (line 53)
- public int CurrentCardIndex => _cardIndex (line 54)
- public int MulliganCount => _mulliganCount (line 55)
- public GameObject CurrentCard (line 57)
- public int TopCardCount => _topCards.Count (line 68)
- public int BottomCardCount => _bottomCards.Count (line 69)

### Methods
- public BrowserZoneNavigator(IAnnouncementService announcer) (line 45)
- public void Activate(BrowserInfo browserInfo) (line 78)
- private IEnumerator RefreshAfterActivation() (line 101)
- public void Deactivate() (line 110)
- public void IncrementMulliganCount() (line 132)
- public void ResetMulliganState() (line 141)
- public void RestoreMulliganCount(int count) (line 150)
- public bool HandleInput() (line 164)
- public void EnterZone(BrowserZoneType zone) (line 226)
- public void NavigateNext() (line 261)
- public void NavigatePrevious() (line 286)
- public void NavigateFirst() (line 310)
- public void NavigateLast() (line 333)
- private void AnnounceCurrentCard() (line 357)
- public void ActivateCurrentCard() (line 380)
- private IEnumerator RefreshZoneAfterDelay(string movedCardName) (line 410)
- private void RefreshCardLists() (line 472)
- private void RefreshSurveilCardLists() (line 498) — reads BrowserCardHolder_Default and _ViewDismiss
- private void RefreshSplitCardLists() (line 549) — reads _topSplit/_bottomSplit via reflection
- private void RefreshScryCardLists() (line 599) — splits CardViews at placeholder (InstanceId == 0)
- private void RefreshLondonCardLists() (line 674) — calls GetHandCards and GetLibraryCards
- private bool TryActivateCardViaDragSimulation(GameObject card, string cardName) (line 760)
- private bool TryActivateViaDragSimulation(object browser, GameObject card, Component cardCDC) (line 794) — London/Surveil
- private bool TryActivateViaScryReorder(GameObject card, string cardName, Component cardCDC) (line 832) — uses ShiftCards + placeholder
- private void SyncBrowserCardViews(Component cardCDC) (line 935) — calls OnDragRelease on GameManager.BrowserManager.CurrentBrowser
- private bool PositionCardAtTargetZone(object browser, GameObject card, Component cardCDC) (line 995) — London screen-space vs Surveil/Split local-space
- private object GetBrowserController() (line 1093) — reads CardGroupProvider from holder
- private List<GameObject> GetCurrentZoneCards() (line 1110)
- private string GetZoneName(BrowserZoneType zone) (line 1115)
- private string GetShortZoneName(BrowserZoneType zone) (line 1128)
- private Component GetCardBrowserHolderComponent(GameObject holder) (line 1144)
- public int GetRequiredPutbackCount() (line 1162) — reads RequiredPutbackCount from LondonBrowser
- public string GetLondonEntryAnnouncement(int cardCount) (line 1189)
- public bool TryGetCardZonePosition(GameObject card, out int indexInZone, out int zoneTotal) (line 1208)
- public string GetCardSelectionState(GameObject card) (line 1233)
- private BrowserZoneType DetectCardZone(GameObject card) (line 1255)
- private BrowserZoneType DetectLondonCardZone(GameObject card) (line 1290) — uses IsInHand/IsInLibrary
- public bool ActivateCardFromGenericNavigation(GameObject card) (line 1331) — called by BrowserNavigator on Tab+Enter
- private IEnumerator RefreshAfterGenericActivation() (line 1377)
