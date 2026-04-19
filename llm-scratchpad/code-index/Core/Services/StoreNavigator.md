# StoreNavigator.cs
Path: src/Core/Services/StoreNavigator.cs
Lines: 2773

## Top-level comments
- Standalone navigator for the MTGA Store screen with two-level navigation (tabs and items with purchase options). Accesses `ContentController_StoreCarousel` via reflection for tab state, loading detection, and item data.

## public class StoreNavigator : BaseNavigator (line 20)

### Nested Types
- private enum NavigationLevel (line 41) — see below
- private struct DetailCardEntry (line 84) — see below
- private struct TabInfo (line 203) — see below
- private struct ItemInfo (line 212) — see below
- private struct PurchaseOption (line 222) — see below

### Fields
- private const int StorePriority = 55 (line 24)
- private const float TabLoadCheckInterval = 0.1f (line 25)
- private NavigationLevel _navLevel (line 48)
- private int _currentPurchaseOptionIndex (line 49)
- private bool _waitingForTabLoad (line 50)
- private float _loadCheckTimer (line 51)
- private List&lt;object&gt; _setFilterModels (line 54)
- private int _currentSetFilterIndex (line 55)
- private MonoBehaviour _setFilterTogglesComponent (line 56)
- private bool _waitingForSetChange (line 57)
- private bool _wasConfirmationModalOpen (line 60)
- private bool _isConfirmationModalActive (line 61)
- private MonoBehaviour _confirmationModalMb (line 62)
- private List&lt;(GameObject obj, string label)&gt; _modalElements (line 64)
- private int _modalElementIndex (line 65)
- private readonly WebBrowserAccessibility _webBrowser (line 68)
- private bool _isWebBrowserActive (line 69)
- private GameObject _pendingBattlePassPopup (line 72)
- private float _battlePassPopupTimer (line 73)
- private const float BattlePassPopupMaxWait = 1.5f (line 74)
- private bool _isDetailsViewActive (line 77)
- private string _detailsDescription (line 78)
- private List&lt;DetailCardEntry&gt; _detailsCards (line 79)
- private int _detailsCardIndex (line 80)
- private List&lt;CardInfoBlock&gt; _detailsCardBlocks (line 81)
- private int _detailsBlockIndex (line 82)
- private MonoBehaviour _controller (line 97)
- private GameObject _controllerGameObject (line 98)
- private static readonly string[] TabFieldNames (line 101)
- private static readonly string[] TabDisplayNames (line 107)
- private Type _controllerType (line 114)
- private FieldInfo _itemDisplayQueueField (line 115)
- private FieldInfo _readyToShowField (line 116)
- private FieldInfo _currentTabField (line 117)
- private FieldInfo _confirmationModalField (line 118)
- private PropertyInfo _isOpenProp (line 119)
- private PropertyInfo _isReadyToShowProp (line 120)
- private FieldInfo[] _tabFields (line 121)
- private FieldInfo _storeTabTypeLookupField (line 122)
- private FieldInfo _paymentInfoButtonField (line 125)
- private FieldInfo _redeemCodeInputField (line 126)
- private FieldInfo _dropRatesLinkField (line 127)
- private Type _storeItemBaseType (line 130)
- private FieldInfo _storeItemField (line 131)
- private FieldInfo _blueButtonField (line 132)
- private FieldInfo _orangeButtonField (line 133)
- private FieldInfo _clearButtonField (line 134)
- private FieldInfo _greenButtonField (line 135)
- private FieldInfo _descriptionField (line 136)
- private FieldInfo _tooltipTriggerField (line 137)
- private Type _purchaseButtonType (line 140)
- private FieldInfo _pbButtonField (line 141)
- private FieldInfo _pbContainerField (line 142)
- private Type _tabType (line 145)
- private FieldInfo _tabTextField (line 146)
- private MethodInfo _tabOnClickedMethod (line 147)
- private Type _storeItemType (line 150)
- private PropertyInfo _storeItemIdProp (line 151)
- private MethodInfo _onButtonPaymentSetupMethod (line 154)
- private FieldInfo _setFiltersComponentField (line 157)
- private Type _setFilterTogglesType (line 158)
- private FieldInfo _setFilterListField (line 159)
- private PropertyInfo _selectedIndexProp (line 160)
- private MethodInfo _onValueSelectedMethod (line 161)
- private Type _setFilterModelType (line 162)
- private FieldInfo _setSymbolField (line 163)
- private Type _storeItemDisplayType (line 166)
- private Type _storeDisplayPreconDeckType (line 167)
- private Type _storeDisplayCardViewBundleType (line 168)
- private FieldInfo _itemDisplayField (line 169)
- private PropertyInfo _preconCardDataProp (line 170)
- private PropertyInfo _bundleCardViewsProp (line 171)
- private Type _cardDataForTileType (line 172)
- private PropertyInfo _cardDataForTileCardProp (line 173)
- private PropertyInfo _cardDataForTileQuantityProp (line 174)
- private Type _cardDataType (line 175)
- private PropertyInfo _cardDataGrpIdProp (line 176)
- private PropertyInfo _cardDataTitleIdProp (line 177)
- private PropertyInfo _cardDataManaTextProp (line 178)
- private Type _localizedStringType (line 180)
- private FieldInfo _locStringField (line 181)
- private FieldInfo _locStringMTermField (line 182)
- private MethodInfo _locStringToStringMethod (line 183)
- private Type _confirmationModalType (line 186)
- private static readonly string[] ModalPurchaseButtonFields (line 187)
- private FieldInfo[] _modalButtonFields (line 191)
- private Type _modalPurchaseButtonType (line 192)
- private FieldInfo _modalPbButtonField (line 193)
- private FieldInfo _modalPbLabelField (line 194)
- private MethodInfo _modalCloseMethod (line 195)
- private bool _reflectionInitialized (line 197)
- private readonly List&lt;TabInfo&gt; _tabs (line 229)
- private readonly List&lt;ItemInfo&gt; _items (line 230)
- private static readonly string[] PackProgressTextNames (line 812)
- private const int PacksTabFieldIndex = 2 (line 896)

### Properties
- public override string NavigatorId (line 31)
- public override string ScreenName (line 32)
- public override int Priority (line 33)
- protected override bool SupportsCardNavigation (line 34)
- protected override bool AcceptSpaceKey (line 35)

### Methods
- public StoreNavigator(IAnnouncementService announcer) : base(announcer) (line 236)
- protected override bool DetectScreen() (line 244)
- private MonoBehaviour FindStoreController() (line 261)
- private bool IsControllerOpenAndReady(MonoBehaviour controller) (line 284)
- private bool IsConfirmationModalOpen(MonoBehaviour controller) (line 312)
- private GameObject GetConfirmationModalGameObject() (line 333)
- private void OnPanelChanged(PanelInfo oldPanel, PanelInfo newPanel) (line 352) — Note: detects web browser panels over store and activates/deactivates WebBrowserAccessibility.
- protected override bool IsPopupExcluded(PanelInfo panel) (line 374)
- protected override void OnPopupClosed() (line 382)
- protected override void OnPopupDetected(PanelInfo panel) (line 397) — Note: delays popup mode for BattlePassPurchaseConfirmation until its content pane activates.
- private static bool HasBattlePassConfirmation(GameObject go) (line 410)
- private static bool IsBattlePassContentReady(GameObject popup) (line 425)
- private static bool IsWebBrowserPanel(PanelInfo panel) (line 445)
- private void EnsureReflectionCached(Type controllerType) (line 456) — Note: populates ~50 cached reflection members; logs a message on completion.
- protected override void DiscoverElements() (line 630)
- private void PopulateLevelElements() (line 676) — Note: clears _elements and resets _currentIndex.
- private void DiscoverTabs() (line 725)
- private void DiscoverUtilityElements() (line 770)
- private void AddPackProgressElement() (line 818)
- private void AddUtilityElement(FieldInfo field, string fallbackName) (line 865)
- private bool IsPacksTab(TabInfo tab) (line 898)
- private void DiscoverSetFilters() (line 900)
- private string GetSetFilterName(int index) (line 944)
- private void DiscoverItems() (line 966)
- private ItemInfo? ExtractItemInfo(MonoBehaviour storeItemBase) (line 993)
- private string ExtractItemLabel(MonoBehaviour storeItemBase) (line 1022)
- private string ExtractItemDescription(MonoBehaviour storeItemBase) (line 1036)
- private List&lt;PurchaseOption&gt; ExtractPurchaseOptions(MonoBehaviour storeItemBase) (line 1085)
- private void AddPurchaseOption(List&lt;PurchaseOption&gt; options, MonoBehaviour storeItemBase, FieldInfo buttonField, string currencyName) (line 1097)
- protected override void OnActivated() (line 1142) — Note: also subscribes to PanelStateManager.OnPanelChanged.
- protected override void OnDeactivating() (line 1162) — Note: unsubscribes panel handler and clears all mode state.
- public override void OnSceneChanged(string sceneName) (line 1190) — Note: invalidates cached controller and reflection state.
- private int FindActiveTabIndex() (line 1200)
- public override string GetTutorialHint() (line 1225)
- protected override string GetActivationAnnouncement() (line 1230)
- protected override string GetElementAnnouncement(int index) (line 1253)
- private string FormatTabAnnouncement(int index) (line 1266)
- private string FormatItemAnnouncement(int index) (line 1281)
- private string FormatPurchaseOption(PurchaseOption option) (line 1306)
- public override void Update() (line 1321) — Note: handles confirmation-modal transitions, BattlePass popup wait, web browser updates, and load-check polling.
- protected override bool ValidateElements() (line 1430)
- protected override bool HandleEarlyInput() (line 1443) — Note: dispatches to web browser, confirmation modal, details view, or set filter input when those modes are active.
- protected override bool HandleCustomInput() (line 1478) — Note: Left/Right cycles purchase options; Backspace implements per-level back navigation.
- protected override bool OnElementActivated(int index, GameObject element) (line 1520)
- protected override void Move(int direction) (line 1539) — Note: resets _currentPurchaseOptionIndex when moving between items.
- protected override void MoveFirst() (line 1546)
- protected override void MoveLast() (line 1553)
- private void ActivateCurrentTab() (line 1564)
- private bool IsTabActive(TabInfo tab) (line 1642)
- private bool IsLoadingComplete() (line 1659)
- private void OnTabLoadComplete() (line 1681)
- private void HandleSetFilterInput() (line 1749)
- private void CycleSetFilter(int direction) (line 1806)
- private void SelectSetFilter(int index) (line 1827) — Note: triggers a game-side set change and enters waiting-for-load state.
- private void AnnounceSetFilter() (line 1856)
- private void EnterItemsFromSetFilter() (line 1866)
- private void ReturnToSetFilter() (line 1886)
- private void CyclePurchaseOption(int direction) (line 1903)
- private void ActivateCurrentPurchaseOption() (line 1939) — Note: synthetic Details option opens details view instead of purchasing.
- private bool HasItemDetails(MonoBehaviour storeItemBase) (line 1971)
- private bool HasTooltipText(MonoBehaviour storeItemBase) (line 1995)
- private MonoBehaviour GetItemDisplay(MonoBehaviour storeItemBase) (line 2014)
- private void OpenDetailsView(ItemInfo item) (line 2024)
- private string ExtractTooltipDescription(MonoBehaviour storeItemBase) (line 2064)
- private void ExtractCardEntries(MonoBehaviour storeItemBase, List&lt;DetailCardEntry&gt; entries) (line 2099)
- private void ExtractFromCardDataList(System.Collections.IList list, List&lt;DetailCardEntry&gt; entries) (line 2133)
- private void ExtractFromBundleCardViews(System.Collections.IList viewList, List&lt;DetailCardEntry&gt; entries) (line 2173)
- private string FormatCardAnnouncement(DetailCardEntry card, int index) (line 2218)
- private void HandleDetailsInput() (line 2231) — Note: delegates to ExtendedInfoNavigator when active; I opens it.
- private void MoveDetailsCard(int direction) (line 2330)
- private void MoveDetailsBlock(int direction) (line 2359) — Note: lazy-loads card info blocks on first Up/Down press.
- private void AnnounceDetailsBlock() (line 2415)
- private void CloseDetailsView() (line 2425) — Note: also closes ExtendedInfoNavigator if open.
- private void ActivateUtilityElement(TabInfo tab) (line 2445) — Note: special-cases Steam (payment not available) and pack progress (re-announce text).
- private MonoBehaviour GetConfirmationModalMb() (line 2489)
- private void DiscoverConfirmationModalElements() (line 2499) — Note: adds a synthetic Cancel entry at the end.
- private void AnnounceConfirmationModal() (line 2559)
- private void HandleConfirmationModalInput() (line 2639)
- private void MoveModalElement(int direction) (line 2700)
- private void DismissConfirmationModal() (line 2721) — Note: invokes the modal's native Close() method via reflection.
- private void ReturnToTabs() (line 2745)
- private void HandleBackFromStore() (line 2764) — Note: navigates home and deactivates the navigator.

## private enum NavigationLevel (line 41, nested in StoreNavigator)
- Tabs (line 43)
- SetFilter (line 44)
- Items (line 45)

## private struct DetailCardEntry (line 84, nested in StoreNavigator)
### Fields
- public uint GrpId (line 86)
- public int Quantity (line 87)
- public string Name (line 88)
- public string ManaCost (line 89)
- public object CardDataObj (line 90)

## private struct TabInfo (line 203, nested in StoreNavigator)
### Fields
- public MonoBehaviour TabComponent (line 205)
- public GameObject GameObject (line 206)
- public string DisplayName (line 207)
- public int FieldIndex (line 208)
- public bool IsUtility (line 209)

## private struct ItemInfo (line 212, nested in StoreNavigator)
### Fields
- public MonoBehaviour StoreItemBase (line 214)
- public GameObject GameObject (line 215)
- public string Label (line 216)
- public string Description (line 217)
- public List&lt;PurchaseOption&gt; PurchaseOptions (line 218)
- public bool HasDetails (line 219)

## private struct PurchaseOption (line 222, nested in StoreNavigator)
### Fields
- public GameObject ButtonObject (line 224)
- public string PriceText (line 225)
- public string CurrencyName (line 226)
