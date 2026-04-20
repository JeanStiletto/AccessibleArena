using UnityEngine;
using UnityEngine.UI;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.PanelDetection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Standalone navigator for the MTGA Store screen.
    /// Two-level navigation: tabs (Up/Down) and items (Up/Down with Left/Right for purchase options).
    /// Accesses ContentController_StoreCarousel via reflection for tab state, loading detection, and item data.
    /// </summary>
    public partial class StoreNavigator : BaseNavigator
    {
        #region Constants

        private const int StorePriority = 55;
        private const float TabLoadCheckInterval = 0.1f;
        private const int PacksTabFieldIndex = 2; // _packsTab is at index 2 in TabFieldNames

        // Target children in PackProgressMeter that contain actual progress data
        private static readonly string[] PackProgressTextNames = new[]
        {
            "Text_GoalNumber",   // e.g. "0/10"
            "Text_Title",        // e.g. "Kaufe 10 weitere ... um einen Goldenen Booster zu erhalten."
        };

        #endregion

        #region Navigator Identity

        public override string NavigatorId => "Store";
        public override string ScreenName => Strings.ScreenStore;
        public override int Priority => StorePriority;
        protected override bool SupportsCardNavigation => false;
        protected override bool AcceptSpaceKey => true;

        #endregion

        #region Navigation State

        private enum NavigationLevel
        {
            Tabs,
            SetFilter,
            Items
        }

        private NavigationLevel _navLevel = NavigationLevel.Tabs;
        private int _currentPurchaseOptionIndex;
        private bool _waitingForTabLoad;
        private float _loadCheckTimer;

        // Set filter state (Packs tab set selection)
        private List<object> _setFilterModels = new List<object>();
        private int _currentSetFilterIndex;
        private MonoBehaviour _setFilterTogglesComponent;
        private bool _waitingForSetChange; // true when waiting for set change reload

        // Confirmation modal handling (custom element list, not base popup mode)
        private bool _wasConfirmationModalOpen; // Track modal transitions for confirmation modal
        private bool _isConfirmationModalActive; // Active state for custom modal input
        private MonoBehaviour _confirmationModalMb; // Reference for calling Close()
        // Confirmation modal uses its own element list (special handling with purchase buttons)
        private List<(GameObject obj, string label)> _modalElements = new List<(GameObject, string)>();
        private int _modalElementIndex;

        // Web browser accessibility (payment popup)
        private readonly WebBrowserAccessibility _webBrowser = new WebBrowserAccessibility();
        private bool _isWebBrowserActive;

        // BattlePass purchase confirmation popup (delayed activation)
        private GameObject _pendingBattlePassPopup;
        private float _battlePassPopupTimer;
        private const float BattlePassPopupMaxWait = 1.5f;

        // Details view state
        private bool _isDetailsViewActive;
        private string _detailsDescription;              // Tooltip description (announced on open, re-read with D)
        private List<DetailCardEntry> _detailsCards = new List<DetailCardEntry>();
        private int _detailsCardIndex;
        private List<CardInfoBlock> _detailsCardBlocks = new List<CardInfoBlock>();
        private int _detailsBlockIndex;

        private struct DetailCardEntry
        {
            public uint GrpId;
            public int Quantity;
            public string Name;       // Cached card name
            public string ManaCost;   // Screen-reader formatted mana
            public object CardDataObj; // Raw CardData for info block extraction
        }

        #endregion

        #region Cached Controller & Reflection

        private MonoBehaviour _controller;
        private GameObject _controllerGameObject;

        // Tab fields on ContentController_StoreCarousel
        private static readonly string[] TabFieldNames = new[]
        {
            "_featuredTab", "_gemsTab", "_packsTab", "_dailyDealsTab",
            "_bundlesTab", "_cosmeticsTab", "_decksTab", "_prizeWallTab"
        };

        private static readonly string[] TabDisplayNames = new[]
        {
            "Featured", "Gems", "Packs", "Daily Deals",
            "Bundles", "Cosmetics", "Decks", "Prize Wall"
        };

        // Cached reflection members
        private Type _controllerType;
        private FieldInfo _itemDisplayQueueField;
        private FieldInfo _readyToShowField;
        private FieldInfo _currentTabField;
        private FieldInfo _confirmationModalField;
        private PropertyInfo _isOpenProp;
        private PropertyInfo _isReadyToShowProp;
        private FieldInfo[] _tabFields;
        private FieldInfo _storeTabTypeLookupField;

        // Utility element fields on controller
        private FieldInfo _paymentInfoButtonField;
        private FieldInfo _redeemCodeInputField;
        private FieldInfo _dropRatesLinkField;

        // Cached StoreItemBase reflection
        private Type _storeItemBaseType;
        private FieldInfo _storeItemField;      // _storeItem (public)
        private FieldInfo _blueButtonField;      // BlueButton
        private FieldInfo _orangeButtonField;    // OrangeButton
        private FieldInfo _clearButtonField;     // ClearButton
        private FieldInfo _greenButtonField;     // GreenButton
        private FieldInfo _descriptionField;     // _description (OptionalObject)
        private FieldInfo _tooltipTriggerField;  // _tooltipTrigger (TooltipTrigger)

        // PurchaseButton struct fields
        private Type _purchaseButtonType;
        private FieldInfo _pbButtonField;        // Button (CustomButton)
        private FieldInfo _pbContainerField;     // ButtonContainer (GameObject)

        // Tab class
        private Type _tabType;
        private FieldInfo _tabTextField;          // _text (Localize)
        private MethodInfo _tabOnClickedMethod;   // OnClicked()

        // StoreItem properties
        private Type _storeItemType;
        private PropertyInfo _storeItemIdProp;

        // Controller utility methods
        private MethodInfo _onButtonPaymentSetupMethod;

        // Set filter reflection (StoreSetFilterToggles on controller)
        private FieldInfo _setFiltersComponentField;        // _setFilters on controller (StoreSetFilterToggles)
        private Type _setFilterTogglesType;
        private FieldInfo _setFilterListField;              // _setFilters on StoreSetFilterToggles (List<StoreSetFilterModel>)
        private PropertyInfo _selectedIndexProp;            // SelectedIndex
        private MethodInfo _onValueSelectedMethod;          // OnValueSelected(StoreSetFilterModel)
        private Type _setFilterModelType;
        private FieldInfo _setSymbolField;                  // SetSymbol (string)

        // Store display types for details view
        private Type _storeItemDisplayType;
        private Type _storeDisplayPreconDeckType;
        private Type _storeDisplayCardViewBundleType;
        private FieldInfo _itemDisplayField;              // StoreItemBase._itemDisplay (private field)
        private PropertyInfo _preconCardDataProp;       // StoreDisplayPreconDeck.CardData (public)
        private PropertyInfo _bundleCardViewsProp;      // StoreDisplayCardViewBundle.BundleCardViews (public)
        private Type _cardDataForTileType;
        private PropertyInfo _cardDataForTileCardProp;  // CardDataForTile.Card
        private PropertyInfo _cardDataForTileQuantityProp; // CardDataForTile.Quantity
        private Type _cardDataType;
        private PropertyInfo _cardDataGrpIdProp;        // CardData.GrpId
        private PropertyInfo _cardDataTitleIdProp;      // CardData.TitleId
        private PropertyInfo _cardDataManaTextProp;     // CardData.OldSchoolManaText
        // TooltipTrigger -> LocalizedString
        private Type _localizedStringType;
        private FieldInfo _locStringField;              // TooltipTrigger.LocString (public)
        private FieldInfo _locStringMTermField;         // LocalizedString.mTerm (public)
        private MethodInfo _locStringToStringMethod;    // LocalizedString.ToString()

        // Cached StoreConfirmationModal reflection
        private Type _confirmationModalType;
        private static readonly string[] ModalPurchaseButtonFields = new[]
        {
            "_buttonGemPurchase", "_buttonCoinPurchase", "_buttonCashPurchase", "_buttonFreePurchase"
        };
        private FieldInfo[] _modalButtonFields;
        private Type _modalPurchaseButtonType;
        private FieldInfo _modalPbButtonField;    // Button (CustomButton)
        private FieldInfo _modalPbLabelField;     // Label (TMP_Text)
        private MethodInfo _modalCloseMethod;

        private bool _reflectionInitialized;

        #endregion

        #region Discovered Data

        private struct TabInfo
        {
            public MonoBehaviour TabComponent; // null for utility entries
            public GameObject GameObject;
            public string DisplayName;
            public int FieldIndex;             // -1 for utility entries
            public bool IsUtility;             // true for non-tab entries (payment, redeem, drop rates)
        }

        private struct ItemInfo
        {
            public MonoBehaviour StoreItemBase;
            public GameObject GameObject;
            public string Label;
            public string Description;
            public List<PurchaseOption> PurchaseOptions;
            public bool HasDetails;
        }

        private struct PurchaseOption
        {
            public GameObject ButtonObject;
            public string PriceText;
            public string CurrencyName;
        }

        private readonly List<TabInfo> _tabs = new List<TabInfo>();
        private readonly List<ItemInfo> _items = new List<ItemInfo>();

        #endregion

        #region Constructor

        public StoreNavigator(IAnnouncementService announcer) : base(announcer)
        {
        }

        #endregion

        #region Screen Detection

        protected override bool DetectScreen()
        {
            // Find the store controller
            var controller = FindStoreController();
            if (controller == null) return false;

            // Check if open and ready
            if (!IsControllerOpenAndReady(controller)) return false;

            // Note: confirmation modal is handled as a popup while navigator stays active

            _controller = controller;
            _controllerGameObject = controller.gameObject;

            return true;
        }

        private MonoBehaviour FindStoreController()
        {
            // Use cached reference if still valid
            if (_controller != null && _controller.gameObject != null && _controller.gameObject.activeInHierarchy)
            {
                return _controller;
            }

            _controller = null;
            _controllerGameObject = null;

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == T.ContentControllerStoreCarousel)
                {
                    return mb;
                }
            }

            return null;
        }

        private bool IsControllerOpenAndReady(MonoBehaviour controller)
        {
            var type = controller.GetType();
            EnsureReflectionCached(type);

            if (_isOpenProp != null)
            {
                try
                {
                    bool isOpen = (bool)_isOpenProp.GetValue(controller);
                    if (!isOpen) return false;
                }
                catch { return false; }
            }

            if (_isReadyToShowProp != null)
            {
                try
                {
                    bool isReady = (bool)_isReadyToShowProp.GetValue(controller);
                    if (!isReady) return false;
                }
                catch { return false; }
            }

            return true;
        }

        /// <summary>
        /// Handle panel changes - detect web browser panels appearing on top of store.
        /// Generic popups are handled by base popup mode infrastructure.
        /// </summary>
        private void OnPanelChanged(PanelInfo oldPanel, PanelInfo newPanel)
        {
            if (!_isActive) return;

            if (newPanel != null && IsWebBrowserPanel(newPanel))
            {
                MelonLogger.Msg($"[Store] Web browser panel detected: {newPanel.Name}");
                _isWebBrowserActive = true;
                _webBrowser.Activate(newPanel.GameObject, _announcer);
            }
            else if (_isWebBrowserActive && newPanel == null)
            {
                MelonLogger.Msg("[Store] Web browser closed, returning to store");
                _webBrowser.Deactivate();
                _isWebBrowserActive = false;
                AnnounceCurrentElement();
            }
        }

        /// <summary>
        /// Exclude web browser panels from base popup handling (they use WebBrowserAccessibility instead).
        /// </summary>
        protected override bool IsPopupExcluded(PanelInfo panel)
        {
            if (IsWebBrowserPanel(panel)) return true;
            // Confirmation modal is handled with custom elements, not base popup mode
            if (panel.Name != null && panel.Name.Contains("ConfirmationModal")) return true;
            return false;
        }

        protected override void OnPopupClosed()
        {
            _pendingBattlePassPopup = null;
            if (_isConfirmationModalActive)
                AnnounceConfirmationModal();
            else if (_navLevel == NavigationLevel.SetFilter)
                AnnounceSetFilter();
            else
                AnnounceCurrentElement();
        }

        /// <summary>
        /// Delay popup mode for BattlePassPurchaseConfirmation — its content panes
        /// activate after a 0.5s coroutine, so immediate discovery finds nothing.
        /// </summary>
        protected override void OnPopupDetected(PanelInfo panel)
        {
            if (panel?.GameObject != null && HasBattlePassConfirmation(panel.GameObject))
            {
                MelonLogger.Msg("[Store] BattlePass popup detected, waiting for content to activate");
                _pendingBattlePassPopup = panel.GameObject;
                _battlePassPopupTimer = BattlePassPopupMaxWait;
                return;
            }

            base.OnPopupDetected(panel);
        }

        private static bool HasBattlePassConfirmation(GameObject go)
        {
            foreach (var mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "BattlePassPurchaseConfirmation")
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Check if the BattlePass popup's content pane has activated.
        /// The popup has Content_PurchasePass and Content_PurchaseXP children under CenterMenu.
        /// RecalculateVisuals() activates one of them after 0.5s.
        /// </summary>
        private static bool IsBattlePassContentReady(GameObject popup)
        {
            // Walk: SettingsPopup > Group - Vertical > Middle > CenterMenu
            var settingsPopup = popup.transform.Find("SettingsPopup");
            if (settingsPopup == null) return false;
            var group = settingsPopup.Find("Group - Vertical");
            if (group == null) return false;
            var middle = group.Find("Middle");
            if (middle == null) return false;
            var centerMenu = middle.Find("CenterMenu");
            if (centerMenu == null) return false;

            for (int i = 0; i < centerMenu.childCount; i++)
            {
                if (centerMenu.GetChild(i).gameObject.activeInHierarchy)
                    return true;
            }
            return false;
        }

        private static bool IsWebBrowserPanel(PanelInfo panel)
        {
            if (panel == null || panel.GameObject == null) return false;
            // Check if the panel contains a ZFBrowser.Browser component
            return panel.GameObject.GetComponentInChildren<ZenFulcrum.EmbeddedBrowser.Browser>(true) != null;
        }

        #endregion

        #region Reflection Caching

        private void EnsureReflectionCached(Type controllerType)
        {
            if (_reflectionInitialized && _controllerType == controllerType) return;

            _controllerType = controllerType;
            var flags = AllInstanceFlags;

            // Controller properties
            _isOpenProp = controllerType.GetProperty("IsOpen", flags);
            _isReadyToShowProp = controllerType.GetProperty("IsReadyToShow", flags);

            // Controller fields
            _itemDisplayQueueField = controllerType.GetField("_itemDisplayQueue", flags);
            _readyToShowField = controllerType.GetField("_readyToShow", flags);
            _currentTabField = controllerType.GetField("_currentTab", flags);
            _confirmationModalField = controllerType.GetField("_confirmationModal", flags);
            _storeTabTypeLookupField = controllerType.GetField("_storeTabTypeLookup", flags);

            // Utility element fields
            _paymentInfoButtonField = controllerType.GetField("_paymentInfoButton", flags);
            _redeemCodeInputField = controllerType.GetField("_redeemCodeInput", flags);
            _dropRatesLinkField = controllerType.GetField("_dropRatesLink", flags);

            // Controller utility methods
            _onButtonPaymentSetupMethod = controllerType.GetMethod("OnButton_PaymentSetup", PublicInstance);

            // Set filter reflection
            _setFiltersComponentField = controllerType.GetField("_setFilters", flags);
            if (_setFiltersComponentField != null)
            {
                _setFilterTogglesType = _setFiltersComponentField.FieldType;
                _setFilterListField = _setFilterTogglesType.GetField("_setFilters", flags);
                _selectedIndexProp = _setFilterTogglesType.GetProperty("SelectedIndex", PublicInstance);
                _onValueSelectedMethod = _setFilterTogglesType.GetMethod("OnValueSelected", PublicInstance);

                // Get StoreSetFilterModel type from the list's generic argument
                if (_setFilterListField != null && _setFilterListField.FieldType.IsGenericType)
                {
                    _setFilterModelType = _setFilterListField.FieldType.GetGenericArguments()[0];
                    _setSymbolField = _setFilterModelType?.GetField("SetSymbol", PublicInstance);
                }
            }

            // Tab fields
            _tabFields = new FieldInfo[TabFieldNames.Length];
            for (int i = 0; i < TabFieldNames.Length; i++)
            {
                _tabFields[i] = controllerType.GetField(TabFieldNames[i], flags);
            }

            // Tab class reflection
            if (_currentTabField != null)
            {
                _tabType = _currentTabField.FieldType;
                _tabTextField = _tabType.GetField("_text", flags);
                _tabOnClickedMethod = _tabType.GetMethod("OnClicked", PublicInstance);
            }

            // StoreItemBase type
            _storeItemBaseType = FindType("StoreItemBase");

            if (_storeItemBaseType != null)
            {
                _storeItemField = _storeItemBaseType.GetField("_storeItem", flags);
                _blueButtonField = _storeItemBaseType.GetField("BlueButton", flags);
                _orangeButtonField = _storeItemBaseType.GetField("OrangeButton", flags);
                _clearButtonField = _storeItemBaseType.GetField("ClearButton", flags);
                _greenButtonField = _storeItemBaseType.GetField("GreenButton", flags);
                _descriptionField = _storeItemBaseType.GetField("_description", flags);
                _tooltipTriggerField = _storeItemBaseType.GetField("_tooltipTrigger", flags);

                // PurchaseButton struct type from BlueButton field
                if (_blueButtonField != null)
                {
                    _purchaseButtonType = _blueButtonField.FieldType;
                    _pbButtonField = _purchaseButtonType.GetField("Button", flags);
                    _pbContainerField = _purchaseButtonType.GetField("ButtonContainer", flags);
                }
            }

            // StoreItem type
            if (_storeItemField != null)
            {
                _storeItemType = _storeItemField.FieldType;
                _storeItemIdProp = _storeItemType.GetProperty("Id", PublicInstance);
            }

            // StoreConfirmationModal type
            _confirmationModalType = FindType("StoreConfirmationModal");
            if (_confirmationModalType != null)
            {
                _modalButtonFields = new FieldInfo[ModalPurchaseButtonFields.Length];
                for (int i = 0; i < ModalPurchaseButtonFields.Length; i++)
                {
                    _modalButtonFields[i] = _confirmationModalType.GetField(ModalPurchaseButtonFields[i], flags);
                }
                _modalCloseMethod = _confirmationModalType.GetMethod("Close", PublicInstance);

                // Modal's PurchaseButton struct (different from StoreItemBase's PurchaseCostUtils.PurchaseButton)
                if (_modalButtonFields[0] != null)
                {
                    _modalPurchaseButtonType = _modalButtonFields[0].FieldType;
                    _modalPbButtonField = _modalPurchaseButtonType.GetField("Button", flags);
                    _modalPbLabelField = _modalPurchaseButtonType.GetField("Label", flags);
                }
            }

            // Store display types for details view
            _storeItemDisplayType = FindType("StoreItemDisplay");
            _storeDisplayPreconDeckType = FindType("Core.Meta.MainNavigation.Store.StoreDisplayPreconDeck");
            _storeDisplayCardViewBundleType = FindType("StoreDisplayCardViewBundle");
            _cardDataForTileType = FindType("Wizards.MDN.Store.CardDataForTile");
            _cardDataType = FindType("GreClient.CardData.CardData");
            _localizedStringType = FindType("Wotc.Mtga.Loc.LocalizedString");

            if (_storeItemBaseType != null)
            {
                _itemDisplayField = _storeItemBaseType.GetField("_itemDisplay", flags);
            }

            if (_storeDisplayPreconDeckType != null)
            {
                _preconCardDataProp = _storeDisplayPreconDeckType.GetProperty("CardData",
                    PublicInstance);
            }

            if (_storeDisplayCardViewBundleType != null)
            {
                _bundleCardViewsProp = _storeDisplayCardViewBundleType.GetProperty("BundleCardViews",
                    PublicInstance);
            }

            if (_cardDataForTileType != null)
            {
                _cardDataForTileCardProp = _cardDataForTileType.GetProperty("Card",
                    PublicInstance);
                _cardDataForTileQuantityProp = _cardDataForTileType.GetProperty("Quantity",
                    PublicInstance);
            }

            if (_cardDataType != null)
            {
                _cardDataGrpIdProp = _cardDataType.GetProperty("GrpId",
                    PublicInstance);
                _cardDataTitleIdProp = _cardDataType.GetProperty("TitleId",
                    PublicInstance);
                _cardDataManaTextProp = _cardDataType.GetProperty("OldSchoolManaText",
                    PublicInstance);
            }

            // TooltipTrigger fields: LocString is a public LocalizedString field
            if (_tooltipTriggerField != null)
            {
                var ttType = _tooltipTriggerField.FieldType;
                _locStringField = ttType.GetField("LocString",
                    PublicInstance);
            }

            if (_localizedStringType != null)
            {
                _locStringMTermField = _localizedStringType.GetField("mTerm",
                    PublicInstance);
                _locStringToStringMethod = _localizedStringType.GetMethod("ToString",
                    PublicInstance, null, Type.EmptyTypes, null);
            }

            _reflectionInitialized = true;
            MelonLogger.Msg($"[Store] Reflection cached. StoreItemBase={_storeItemBaseType != null}, Tab={_tabType != null}, PurchaseButton={_purchaseButtonType != null}, PreconDeck={_storeDisplayPreconDeckType != null}, CardViewBundle={_storeDisplayCardViewBundleType != null}");
        }

        #endregion

        #region Element Discovery (BaseNavigator requirement)

        protected override void DiscoverElements()
        {
            DiscoverTabs();
            if (_tabs.Count == 0) return;

            // Determine initial level based on active tab
            int activeTab = FindActiveTabIndex();
            if (activeTab < 0) activeTab = 0;

            // Packs tab → SetFilter level
            if (activeTab < _tabs.Count && IsPacksTab(_tabs[activeTab]))
            {
                DiscoverSetFilters();
                if (_setFilterModels.Count > 0)
                {
                    _navLevel = NavigationLevel.SetFilter;
                    // SetFilter is fully custom (HandleEarlyInput) — add a dummy element
                    // so BaseNavigator's _elements.Count > 0 check passes during TryActivate
                    _elements.Add(new NavigableElement
                    {
                        GameObject = _controllerGameObject,
                        Label = "SetFilter",
                        Role = UIElementClassifier.ElementRole.Unknown
                    });
                    return;
                }
            }

            // Try Items level
            DiscoverItems();
            if (_items.Count > 0)
            {
                _navLevel = NavigationLevel.Items;
                PopulateLevelElements();
                return;
            }

            // Fallback: Tabs level
            _navLevel = NavigationLevel.Tabs;
            PopulateLevelElements();
        }

        /// <summary>
        /// Clear _elements and populate for the current navigation level.
        /// Tabs and Items use base navigation; SetFilter is fully custom (HandleEarlyInput).
        /// </summary>
        private void PopulateLevelElements()
        {
            _elements.Clear();
            _currentIndex = -1;

            switch (_navLevel)
            {
                case NavigationLevel.Tabs:
                    foreach (var tab in _tabs)
                        _elements.Add(new NavigableElement
                        {
                            GameObject = tab.GameObject,
                            Label = tab.DisplayName,
                            Role = tab.IsUtility
                                ? UIElementClassifier.ElementRole.Button
                                : UIElementClassifier.ElementRole.Unknown
                        });
                    break;

                case NavigationLevel.Items:
                    foreach (var item in _items)
                        _elements.Add(new NavigableElement
                        {
                            GameObject = item.GameObject,
                            Label = item.Label,
                            Role = UIElementClassifier.ElementRole.Unknown
                        });
                    break;

                case NavigationLevel.SetFilter:
                    // SetFilter is handled by HandleEarlyInput, not base navigation.
                    // Add a placeholder so ValidateElements doesn't deactivate us.
                    _elements.Add(new NavigableElement
                    {
                        GameObject = _controllerGameObject,
                        Label = "SetFilter",
                        Role = UIElementClassifier.ElementRole.Unknown
                    });
                    break;
            }

            if (_elements.Count > 0)
                _currentIndex = 0;
        }

        #endregion

        #region Set Filter Discovery

        private bool IsPacksTab(TabInfo tab) => !tab.IsUtility && tab.FieldIndex == PacksTabFieldIndex;

        #endregion

        #region Activation & Deactivation

        protected override void OnActivated()
        {
            _waitingForTabLoad = false;
            _currentPurchaseOptionIndex = 0;
            _wasConfirmationModalOpen = false;

            // Adjust _currentIndex for the level determined by DiscoverElements
            if (_navLevel == NavigationLevel.Tabs)
            {
                int activeTab = FindActiveTabIndex();
                if (activeTab >= 0 && activeTab < _elements.Count)
                    _currentIndex = activeTab;
            }

            // Subscribe to panel changes for popup detection + web browser detection
            EnablePopupDetection();
            if (PanelStateManager.Instance != null)
                PanelStateManager.Instance.OnPanelChanged += OnPanelChanged;
        }

        protected override void OnDeactivating()
        {
            DisablePopupDetection();
            if (PanelStateManager.Instance != null)
                PanelStateManager.Instance.OnPanelChanged -= OnPanelChanged;

            _tabs.Clear();
            _items.Clear();
            _waitingForTabLoad = false;
            _waitingForSetChange = false;
            _setFilterModels.Clear();
            _setFilterTogglesComponent = null;
            _isDetailsViewActive = false;
            _detailsCards.Clear();
            _detailsCardBlocks.Clear();
            _detailsDescription = null;
            _modalElements.Clear();
            _wasConfirmationModalOpen = false;
            _isConfirmationModalActive = false;
            _confirmationModalMb = null;

            if (_isWebBrowserActive)
            {
                _webBrowser.Deactivate();
                _isWebBrowserActive = false;
            }
        }

        public override void OnSceneChanged(string sceneName)
        {
            // Clear cached controller on scene change
            _controller = null;
            _controllerGameObject = null;
            _reflectionInitialized = false;

            base.OnSceneChanged(sceneName);
        }

        #endregion

        #region Announcements

        public override string GetTutorialHint() =>
            _navLevel == NavigationLevel.Items
                ? LocaleManager.Instance.Get("StoreItemsHint")
                : LocaleManager.Instance.Get("NavigateHint");

        protected override string GetActivationAnnouncement()
        {
            switch (_navLevel)
            {
                case NavigationLevel.SetFilter:
                    string setName = GetSetFilterName(_currentSetFilterIndex);
                    return $"Store, Packs. {Strings.StoreSetFilterPosition(setName, _currentSetFilterIndex + 1, _setFilterModels.Count)}";

                case NavigationLevel.Items:
                    _currentPurchaseOptionIndex = 0;
                    int activeTab = FindActiveTabIndex();
                    string tabName = (activeTab >= 0 && activeTab < _tabs.Count)
                        ? _tabs[activeTab].DisplayName : "Store";
                    string core = $"Store, {tabName}. {_items.Count} items";
                    return Strings.WithHint(core, "StoreItemsHint");

                default: // Tabs
                    int realTabCount = _tabs.Count(t => !t.IsUtility);
                    string tabCore = $"Store. {realTabCount} tabs";
                    return Strings.WithHint(tabCore, "NavigateHint");
            }
        }

        protected override string GetElementAnnouncement(int index)
        {
            switch (_navLevel)
            {
                case NavigationLevel.Tabs:
                    return FormatTabAnnouncement(index);
                case NavigationLevel.Items:
                    return FormatItemAnnouncement(index);
                default:
                    return base.GetElementAnnouncement(index);
            }
        }

        #endregion

        #region Update Loop

        public override void Update()
        {
            if (!_isActive)
            {
                base.Update();
                return;
            }

            // Verify controller is still valid
            if (_controller == null || _controllerGameObject == null || !_controllerGameObject.activeInHierarchy)
            {
                Deactivate();
                return;
            }

            if (!IsControllerOpenAndReady(_controller))
            {
                Deactivate();
                return;
            }

            // Confirmation modal transitions (must run before base processes input)
            bool modalOpen = IsConfirmationModalOpen(_controller);
            if (modalOpen && !_wasConfirmationModalOpen)
            {
                _wasConfirmationModalOpen = true;
                var modalObj = GetConfirmationModalGameObject();
                if (modalObj != null)
                {
                    MelonLogger.Msg($"[Store] Confirmation modal opened, handling with custom elements");
                    _isConfirmationModalActive = true;
                    _confirmationModalMb = GetConfirmationModalMb();
                    DiscoverConfirmationModalElements();
                    AnnounceConfirmationModal();
                }
                return;
            }
            else if (!modalOpen && _wasConfirmationModalOpen)
            {
                _wasConfirmationModalOpen = false;
                if (_isConfirmationModalActive)
                {
                    MelonLogger.Msg("[Store] Confirmation modal closed, returning to store");
                    _isConfirmationModalActive = false;
                    _modalElements.Clear();
                    _confirmationModalMb = null;
                    AnnounceCurrentElement();
                }
            }

            // BattlePass popup: wait for content pane to activate before entering popup mode
            if (_pendingBattlePassPopup != null)
            {
                _battlePassPopupTimer -= UnityEngine.Time.deltaTime;
                if (_pendingBattlePassPopup == null || !_pendingBattlePassPopup.activeInHierarchy)
                {
                    // Popup was dismissed externally
                    MelonLogger.Msg("[Store] BattlePass popup gone while waiting");
                    _pendingBattlePassPopup = null;
                }
                else if (IsBattlePassContentReady(_pendingBattlePassPopup) || _battlePassPopupTimer <= 0)
                {
                    MelonLogger.Msg($"[Store] BattlePass popup content ready (timeout={_battlePassPopupTimer <= 0})");
                    var popup = _pendingBattlePassPopup;
                    _pendingBattlePassPopup = null;
                    EnterPopupMode(popup);
                }
                return; // Block input while waiting
            }

            // Web browser updates
            if (_isWebBrowserActive)
            {
                _webBrowser.Update();
                if (!_webBrowser.IsActive)
                {
                    MelonLogger.Msg("[Store] Web browser became inactive, returning to store");
                    _isWebBrowserActive = false;
                    AnnounceCurrentElement();
                }
            }

            // Confirmation modal active: handle input directly, skip base popup mode
            // (same pattern as MasteryNavigator — modal is custom, not base popup)
            if (_isConfirmationModalActive)
            {
                HandleConfirmationModalInput();
                return;
            }

            // Loading state suppresses all input
            if (_waitingForTabLoad)
            {
                _loadCheckTimer -= Time.deltaTime;
                if (_loadCheckTimer <= 0)
                {
                    _loadCheckTimer = TabLoadCheckInterval;
                    if (IsLoadingComplete())
                    {
                        OnTabLoadComplete();
                    }
                }
                return;
            }

            // Delegate to base for standard navigation (calls HandleInput → our hooks)
            base.Update();
        }

        protected override bool ValidateElements()
        {
            return _controller != null && _controllerGameObject != null && _controllerGameObject.activeInHierarchy;
        }

        #endregion

        #region Input Handling

        /// <summary>
        /// Intercept input before base navigation for custom modes:
        /// web browser, confirmation modal, details view, and set filter level.
        /// </summary>
        protected override bool HandleEarlyInput()
        {
            if (_isWebBrowserActive)
            {
                _webBrowser.HandleInput();
                return true;
            }

            if (_isConfirmationModalActive)
            {
                HandleConfirmationModalInput();
                return true;
            }

            if (_isDetailsViewActive)
            {
                HandleDetailsInput();
                return true;
            }

            // SetFilter level is fully custom (every move changes game state + triggers loading)
            if (_navLevel == NavigationLevel.SetFilter)
            {
                HandleSetFilterInput();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Handle keys that base doesn't cover: Backspace (back navigation)
        /// and Left/Right (purchase option cycling at Items level).
        /// Runs after base's Up/Down/Tab/Home/End but before Enter/Left/Right.
        /// </summary>
        protected override bool HandleCustomInput()
        {
            // Left/Right: cycle purchase options at Items level
            if (_navLevel == NavigationLevel.Items)
            {
                if (_holdRepeater.Check(KeyCode.LeftArrow, () => {
                    int b = _currentPurchaseOptionIndex; CyclePurchaseOption(-1); return _currentPurchaseOptionIndex != b;
                })) return true;

                if (_holdRepeater.Check(KeyCode.RightArrow, () => {
                    int b = _currentPurchaseOptionIndex; CyclePurchaseOption(1); return _currentPurchaseOptionIndex != b;
                })) return true;
            }

            // Backspace: back navigation per level
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);

                switch (_navLevel)
                {
                    case NavigationLevel.Tabs:
                        HandleBackFromStore();
                        break;
                    case NavigationLevel.Items:
                        int activeTab = FindActiveTabIndex();
                        if (activeTab >= 0 && activeTab < _tabs.Count &&
                            IsPacksTab(_tabs[activeTab]) && _setFilterModels.Count > 0)
                            ReturnToSetFilter();
                        else
                            ReturnToTabs();
                        break;
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Handle Enter activation per level. Return true to suppress base's default activation.
        /// </summary>
        protected override bool OnElementActivated(int index, GameObject element)
        {
            switch (_navLevel)
            {
                case NavigationLevel.Tabs:
                    ActivateCurrentTab();
                    return true;

                case NavigationLevel.Items:
                    ActivateCurrentPurchaseOption();
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Reset purchase option index when moving between items.
        /// </summary>
        protected override void Move(int direction)
        {
            if (_navLevel == NavigationLevel.Items)
                _currentPurchaseOptionIndex = 0;
            base.Move(direction);
        }

        protected override void MoveFirst()
        {
            if (_navLevel == NavigationLevel.Items)
                _currentPurchaseOptionIndex = 0;
            base.MoveFirst();
        }

        protected override void MoveLast()
        {
            if (_navLevel == NavigationLevel.Items)
                _currentPurchaseOptionIndex = 0;
            base.MoveLast();
        }

        #endregion

        #region Back Navigation

        private void HandleBackFromStore()
        {
            MelonLogger.Msg("[Store] Back from store - navigating home");
            NavigateToHome();
            Deactivate();
        }

        #endregion
    }
}
