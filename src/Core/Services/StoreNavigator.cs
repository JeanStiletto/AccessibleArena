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

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Standalone navigator for the MTGA Store screen.
    /// Two-level navigation: tabs (Up/Down) and items (Up/Down with Left/Right for purchase options).
    /// Accesses ContentController_StoreCarousel via reflection for tab state, loading detection, and item data.
    /// </summary>
    public class StoreNavigator : BaseNavigator
    {
        #region Constants

        private const int StorePriority = 55;
        private const float TabLoadCheckInterval = 0.1f;

        #endregion

        #region Navigator Identity

        public override string NavigatorId => "Store";
        public override string ScreenName => "Store";
        public override int Priority => StorePriority;
        protected override bool SupportsCardNavigation => false;
        protected override bool AcceptSpaceKey => true;

        #endregion

        #region Navigation State

        private enum NavigationLevel
        {
            Tabs,
            Items
        }

        private NavigationLevel _navLevel = NavigationLevel.Tabs;
        private int _currentTabIndex;
        private int _currentItemIndex;
        private int _currentPurchaseOptionIndex;
        private bool _waitingForTabLoad;
        private float _loadCheckTimer;

        // Popup overlay tracking (follows SettingsMenuNavigator pattern)
        private GameObject _activePopup;
        private bool _isPopupActive;
        private List<(GameObject obj, string label)> _popupElements = new List<(GameObject, string)>();
        private int _popupElementIndex;
        private bool _wasConfirmationModalOpen; // Track modal transitions
        private MonoBehaviour _confirmationModalMb; // Reference for calling Close()

        // Web browser accessibility (payment popup)
        private readonly WebBrowserAccessibility _webBrowser = new WebBrowserAccessibility();
        private bool _isWebBrowserActive;

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

        public StoreNavigator(IAnnouncementService announcer) : base(announcer) { }

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
                if (mb.GetType().Name == "ContentController_StoreCarousel")
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

        private bool IsConfirmationModalOpen(MonoBehaviour controller)
        {
            if (_confirmationModalField == null) return false;

            try
            {
                var modal = _confirmationModalField.GetValue(controller);
                if (modal == null) return false;

                // StoreConfirmationModal is a MonoBehaviour - check gameObject.activeSelf
                var modalMb = modal as MonoBehaviour;
                if (modalMb != null && modalMb.gameObject != null)
                {
                    return modalMb.gameObject.activeSelf;
                }
            }
            catch { }

            return false;
        }

        private GameObject GetConfirmationModalGameObject()
        {
            if (_confirmationModalField == null || _controller == null) return null;

            try
            {
                var modal = _confirmationModalField.GetValue(_controller) as MonoBehaviour;
                if (modal != null && modal.gameObject != null)
                    return modal.gameObject;
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Handle panel changes - detect popups appearing on top of store.
        /// Follows the SettingsMenuNavigator pattern: subscribe to PanelStateManager events.
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
            else if (newPanel != null && IsPopupPanel(newPanel))
            {
                MelonLogger.Msg($"[Store] Popup detected on top of store: {newPanel.Name}");
                _activePopup = newPanel.GameObject;
                _isPopupActive = true;
                DiscoverPopupElements();
                AnnouncePopup();
            }
            else if ((_isPopupActive || _isWebBrowserActive) && newPanel == null)
            {
                if (_isWebBrowserActive)
                {
                    MelonLogger.Msg("[Store] Web browser closed, returning to store");
                    _webBrowser.Deactivate();
                    _isWebBrowserActive = false;
                }
                if (_isPopupActive)
                {
                    MelonLogger.Msg("[Store] Popup closed, returning to store");
                    _activePopup = null;
                    _isPopupActive = false;
                    _popupElements.Clear();
                }
                // Re-announce current position
                if (_navLevel == NavigationLevel.Items && _items.Count > 0)
                    AnnounceCurrentItem();
                else if (_navLevel == NavigationLevel.Tabs && _tabs.Count > 0)
                    AnnounceCurrentTab();
            }
        }

        private static bool IsWebBrowserPanel(PanelInfo panel)
        {
            if (panel == null || panel.GameObject == null) return false;
            // Check if the panel contains a ZFBrowser.Browser component
            return panel.GameObject.GetComponentInChildren<ZenFulcrum.EmbeddedBrowser.Browser>(true) != null;
        }

        private static bool IsPopupPanel(PanelInfo panel)
        {
            if (panel == null) return false;
            if (panel.Type == PanelType.Popup) return true;

            string name = panel.Name;
            return name.Contains("SystemMessageView") ||
                   name.Contains("Popup") ||
                   name.Contains("Dialog") ||
                   name.Contains("Modal");
        }

        #endregion

        #region Reflection Caching

        private void EnsureReflectionCached(Type controllerType)
        {
            if (_reflectionInitialized && _controllerType == controllerType) return;

            _controllerType = controllerType;
            var flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

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
            _onButtonPaymentSetupMethod = controllerType.GetMethod("OnButton_PaymentSetup", BindingFlags.Public | BindingFlags.Instance);

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
                _tabOnClickedMethod = _tabType.GetMethod("OnClicked", BindingFlags.Public | BindingFlags.Instance);
            }

            // StoreItemBase type
            _storeItemBaseType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                _storeItemBaseType = asm.GetType("StoreItemBase");
                if (_storeItemBaseType != null) break;
            }

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
                _storeItemIdProp = _storeItemType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            }

            // StoreConfirmationModal type
            _confirmationModalType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                _confirmationModalType = asm.GetType("StoreConfirmationModal");
                if (_confirmationModalType != null) break;
            }
            if (_confirmationModalType != null)
            {
                _modalButtonFields = new FieldInfo[ModalPurchaseButtonFields.Length];
                for (int i = 0; i < ModalPurchaseButtonFields.Length; i++)
                {
                    _modalButtonFields[i] = _confirmationModalType.GetField(ModalPurchaseButtonFields[i], flags);
                }
                _modalCloseMethod = _confirmationModalType.GetMethod("Close", BindingFlags.Public | BindingFlags.Instance);

                // Modal's PurchaseButton struct (different from StoreItemBase's PurchaseCostUtils.PurchaseButton)
                if (_modalButtonFields[0] != null)
                {
                    _modalPurchaseButtonType = _modalButtonFields[0].FieldType;
                    _modalPbButtonField = _modalPurchaseButtonType.GetField("Button", flags);
                    _modalPbLabelField = _modalPurchaseButtonType.GetField("Label", flags);
                }
            }

            // Store display types for details view
            _storeItemDisplayType = null;
            _storeDisplayPreconDeckType = null;
            _storeDisplayCardViewBundleType = null;
            _cardDataForTileType = null;
            _cardDataType = null;
            _localizedStringType = null;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (_storeItemDisplayType == null)
                    _storeItemDisplayType = asm.GetType("StoreItemDisplay");
                if (_storeDisplayPreconDeckType == null)
                    _storeDisplayPreconDeckType = asm.GetType("Core.Meta.MainNavigation.Store.StoreDisplayPreconDeck");
                if (_storeDisplayCardViewBundleType == null)
                    _storeDisplayCardViewBundleType = asm.GetType("StoreDisplayCardViewBundle");
                if (_cardDataForTileType == null)
                    _cardDataForTileType = asm.GetType("Wizards.MDN.Store.CardDataForTile");
                if (_cardDataType == null)
                    _cardDataType = asm.GetType("GreClient.CardData.CardData");
                if (_localizedStringType == null)
                    _localizedStringType = asm.GetType("Wotc.Mtga.Loc.LocalizedString");
            }

            if (_storeItemBaseType != null)
            {
                _itemDisplayField = _storeItemBaseType.GetField("_itemDisplay", flags);
            }

            if (_storeDisplayPreconDeckType != null)
            {
                _preconCardDataProp = _storeDisplayPreconDeckType.GetProperty("CardData",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            if (_storeDisplayCardViewBundleType != null)
            {
                _bundleCardViewsProp = _storeDisplayCardViewBundleType.GetProperty("BundleCardViews",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            if (_cardDataForTileType != null)
            {
                _cardDataForTileCardProp = _cardDataForTileType.GetProperty("Card",
                    BindingFlags.Public | BindingFlags.Instance);
                _cardDataForTileQuantityProp = _cardDataForTileType.GetProperty("Quantity",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            if (_cardDataType != null)
            {
                _cardDataGrpIdProp = _cardDataType.GetProperty("GrpId",
                    BindingFlags.Public | BindingFlags.Instance);
                _cardDataTitleIdProp = _cardDataType.GetProperty("TitleId",
                    BindingFlags.Public | BindingFlags.Instance);
                _cardDataManaTextProp = _cardDataType.GetProperty("OldSchoolManaText",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            // TooltipTrigger fields: LocString is a public LocalizedString field
            if (_tooltipTriggerField != null)
            {
                var ttType = _tooltipTriggerField.FieldType;
                _locStringField = ttType.GetField("LocString",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            if (_localizedStringType != null)
            {
                _locStringMTermField = _localizedStringType.GetField("mTerm",
                    BindingFlags.Public | BindingFlags.Instance);
                _locStringToStringMethod = _localizedStringType.GetMethod("ToString",
                    BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            }

            _reflectionInitialized = true;
            MelonLogger.Msg($"[Store] Reflection cached. StoreItemBase={_storeItemBaseType != null}, Tab={_tabType != null}, PurchaseButton={_purchaseButtonType != null}, PreconDeck={_storeDisplayPreconDeckType != null}, CardViewBundle={_storeDisplayCardViewBundleType != null}");
        }

        #endregion

        #region Element Discovery (BaseNavigator requirement)

        protected override void DiscoverElements()
        {
            // StoreNavigator manages its own element lists (_tabs, _items)
            // but we need at least one element in _elements for BaseNavigator validation
            DiscoverTabs();

            if (_tabs.Count > 0)
            {
                // Add a dummy element for BaseNavigator validation
                AddElement(_controllerGameObject, "Store");
            }
        }

        #endregion

        #region Tab Discovery

        private void DiscoverTabs()
        {
            _tabs.Clear();

            if (_controller == null || _tabFields == null) return;

            for (int i = 0; i < _tabFields.Length; i++)
            {
                if (_tabFields[i] == null) continue;

                try
                {
                    var tabObj = _tabFields[i].GetValue(_controller);
                    if (tabObj == null) continue;

                    var tabMb = tabObj as MonoBehaviour;
                    if (tabMb == null || tabMb.gameObject == null || !tabMb.gameObject.activeInHierarchy)
                        continue;

                    _tabs.Add(new TabInfo
                    {
                        TabComponent = tabMb,
                        GameObject = tabMb.gameObject,
                        DisplayName = TabDisplayNames[i],
                        FieldIndex = i,
                        IsUtility = false
                    });
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[Store] Error reading tab {TabFieldNames[i]}: {ex.Message}");
                }
            }

            // Add utility elements after tabs (only if visible)
            DiscoverUtilityElements();

            MelonLogger.Msg($"[Store] Discovered {_tabs.Count} entries (tabs + utility)");
        }

        private void DiscoverUtilityElements()
        {
            // Payment info button
            AddUtilityElement(_paymentInfoButtonField, "Change payment method");

            // Redeem code input
            if (_redeemCodeInputField != null)
            {
                try
                {
                    var redeemObj = _redeemCodeInputField.GetValue(_controller);
                    if (redeemObj != null)
                    {
                        var redeemMb = redeemObj as MonoBehaviour;
                        if (redeemMb != null && redeemMb.gameObject != null && redeemMb.gameObject.activeInHierarchy)
                        {
                            _tabs.Add(new TabInfo
                            {
                                TabComponent = null,
                                GameObject = redeemMb.gameObject,
                                DisplayName = "Redeem code",
                                FieldIndex = -1,
                                IsUtility = true
                            });
                        }
                    }
                }
                catch { }
            }

            // Drop rates link
            AddUtilityElement(_dropRatesLinkField, "Drop rates");

            // Pack progress meter (bonus pack progress info)
            AddPackProgressElement();
        }

        // Target children in PackProgressMeter that contain actual progress data
        private static readonly string[] PackProgressTextNames = new[]
        {
            "Text_GoalNumber",   // e.g. "0/10"
            "Text_Title",        // e.g. "Kaufe 10 weitere ... um einen Goldenen Booster zu erhalten."
        };

        private void AddPackProgressElement()
        {
            try
            {
                // Find PackProgressMeter by GameObject name (type is in platform-specific assembly)
                var go = GameObject.Find("PackProgressMeter_Desktop_16x9(Clone)");
                if (go == null || !go.activeInHierarchy) return;

                // Extract specific text fields by GameObject name
                string goal = null;
                string title = null;

                foreach (var tmp in go.GetComponentsInChildren<Component>(true))
                {
                    if (tmp == null || tmp.GetType().Name != "TextMeshProUGUI") continue;

                    string name = tmp.gameObject.name;
                    if (name == "Text_GoalNumber")
                        goal = UITextExtractor.GetText(tmp.gameObject);
                    else if (name == "Text_Title")
                        title = UITextExtractor.GetText(tmp.gameObject);
                }

                if (string.IsNullOrEmpty(goal) && string.IsNullOrEmpty(title)) return;

                // Format: "0/10: Kaufe 10 weitere..."
                string text = !string.IsNullOrEmpty(goal) && !string.IsNullOrEmpty(title)
                    ? $"{goal}: {title}"
                    : goal ?? title;

                _tabs.Add(new TabInfo
                {
                    TabComponent = null,
                    GameObject = go,
                    DisplayName = $"Pack progress: {text}",
                    FieldIndex = -1,
                    IsUtility = true
                });

                MelonLogger.Msg($"[Store] Found pack progress: {text}");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[Store] Error finding pack progress: {ex.Message}");
            }
        }

        private void AddUtilityElement(FieldInfo field, string displayName)
        {
            if (field == null || _controller == null) return;

            try
            {
                var obj = field.GetValue(_controller) as GameObject;
                if (obj != null && obj.activeInHierarchy)
                {
                    _tabs.Add(new TabInfo
                    {
                        TabComponent = null,
                        GameObject = obj,
                        DisplayName = displayName,
                        FieldIndex = -1,
                        IsUtility = true
                    });
                }
            }
            catch { }
        }

        #endregion

        #region Item Discovery

        private void DiscoverItems()
        {
            _items.Clear();

            if (_controller == null || _storeItemBaseType == null) return;

            // Find all active StoreItemBase children of the controller
            var storeItems = _controllerGameObject.GetComponentsInChildren(_storeItemBaseType, false);

            foreach (var item in storeItems)
            {
                var mb = item as MonoBehaviour;
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                var itemInfo = ExtractItemInfo(mb);
                if (itemInfo.HasValue)
                {
                    _items.Add(itemInfo.Value);
                }
            }

            // Sort by sibling index for visual order
            _items.Sort((a, b) => a.GameObject.transform.GetSiblingIndex().CompareTo(b.GameObject.transform.GetSiblingIndex()));

            MelonLogger.Msg($"[Store] Discovered {_items.Count} items");
        }

        private ItemInfo? ExtractItemInfo(MonoBehaviour storeItemBase)
        {
            string label = ExtractItemLabel(storeItemBase);
            string description = ExtractItemDescription(storeItemBase);
            var purchaseOptions = ExtractPurchaseOptions(storeItemBase);
            bool hasDetails = HasItemDetails(storeItemBase);

            // Prepend synthetic "Details" option when item has details
            if (hasDetails)
            {
                purchaseOptions.Insert(0, new PurchaseOption
                {
                    ButtonObject = null,
                    PriceText = "Details",
                    CurrencyName = ""
                });
            }

            return new ItemInfo
            {
                StoreItemBase = storeItemBase,
                GameObject = storeItemBase.gameObject,
                Label = label,
                Description = description,
                PurchaseOptions = purchaseOptions,
                HasDetails = hasDetails
            };
        }

        private string ExtractItemLabel(MonoBehaviour storeItemBase)
        {
            // Delegate to UITextExtractor which centralizes all special-case text extraction
            string label = UITextExtractor.TryGetStoreItemLabel(storeItemBase.gameObject);
            if (!string.IsNullOrEmpty(label))
                return TruncateLabel(label);

            // Final fallback: cleaned GameObject name
            string name = storeItemBase.gameObject.name;
            if (name.StartsWith("StoreItem - "))
                name = name.Substring("StoreItem - ".Length);
            return name;
        }

        private string ExtractItemDescription(MonoBehaviour storeItemBase)
        {
            // Collect active text elements that aren't the label or purchase buttons.
            // Known useful elements: Tag_Badge (discount), Tag_Ribbon (discount),
            // Tag_Header (promo), Tag_Footer (promo), Text_Timer (time-limited),
            // Text_ItemLimit (purchase limit), Text_FeatureCallout (callout),
            // Item Description (if active), Text_PriceSlash (original price)
            var parts = new List<string>();

            foreach (var t in storeItemBase.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;

                string raw = t.text?.Trim();
                if (string.IsNullOrEmpty(raw) || raw.Length < 2) continue;

                // Strip rich text tags
                string text = System.Text.RegularExpressions.Regex.Replace(raw, @"<[^>]+>", "").Trim();
                if (string.IsNullOrEmpty(text) || text.Length < 2) continue;

                string objName = t.gameObject.name;

                // Skip the item label (already announced separately)
                if (objName == "Text_ItemLabel") continue;

                // Skip purchase button prices (already announced as purchase options)
                bool isButton = false;
                var parent = t.transform.parent;
                while (parent != null && parent != storeItemBase.transform)
                {
                    if (parent.name.StartsWith("MainButton"))
                    {
                        isButton = true;
                        break;
                    }
                    parent = parent.parent;
                }
                if (isButton) continue;

                // Skip generic UI noise
                if (text == "?" || text == "!") continue;

                if (!parts.Contains(text))
                    parts.Add(text);
            }

            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        private List<PurchaseOption> ExtractPurchaseOptions(MonoBehaviour storeItemBase)
        {
            var options = new List<PurchaseOption>();

            AddPurchaseOption(options, storeItemBase, _blueButtonField, "Gems");
            AddPurchaseOption(options, storeItemBase, _orangeButtonField, "Gold");
            AddPurchaseOption(options, storeItemBase, _clearButtonField, "");
            AddPurchaseOption(options, storeItemBase, _greenButtonField, "Token");

            return options;
        }

        private void AddPurchaseOption(List<PurchaseOption> options, MonoBehaviour storeItemBase,
            FieldInfo buttonField, string currencyName)
        {
            if (buttonField == null || _purchaseButtonType == null) return;

            try
            {
                var buttonStruct = buttonField.GetValue(storeItemBase);
                if (buttonStruct == null) return;

                // Get the CustomButton from the PurchaseButton struct
                var customButton = _pbButtonField?.GetValue(buttonStruct);
                if (customButton == null) return;

                var buttonMb = customButton as MonoBehaviour;
                if (buttonMb == null || buttonMb.gameObject == null || !buttonMb.gameObject.activeInHierarchy)
                    return;

                // Get price text from button's TMP_Text child
                string priceText = "";
                var tmpText = buttonMb.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (tmpText != null)
                {
                    priceText = tmpText.text?.Trim() ?? "";
                }

                // Also check the ButtonContainer visibility
                var container = _pbContainerField?.GetValue(buttonStruct) as GameObject;
                if (container != null && !container.activeInHierarchy)
                    return;

                options.Add(new PurchaseOption
                {
                    ButtonObject = buttonMb.gameObject,
                    PriceText = priceText,
                    CurrencyName = currencyName
                });
            }
            catch { }
        }

        #endregion

        #region Activation & Deactivation

        protected override void OnActivated()
        {
            _navLevel = NavigationLevel.Tabs;
            _waitingForTabLoad = false;

            // Find which tab is currently active
            _currentTabIndex = FindActiveTabIndex();
            if (_currentTabIndex < 0 && _tabs.Count > 0)
                _currentTabIndex = 0;

            _currentItemIndex = 0;
            _currentPurchaseOptionIndex = 0;
            _wasConfirmationModalOpen = false;

            // Subscribe to panel changes to detect popups (follows SettingsMenuNavigator pattern)
            if (PanelStateManager.Instance != null)
            {
                PanelStateManager.Instance.OnPanelChanged += OnPanelChanged;
            }
        }

        protected override void OnDeactivating()
        {
            _tabs.Clear();
            _items.Clear();
            _waitingForTabLoad = false;
            _isDetailsViewActive = false;
            _detailsCards.Clear();
            _detailsCardBlocks.Clear();
            _detailsDescription = null;
            _activePopup = null;
            _isPopupActive = false;
            _popupElements.Clear();
            _wasConfirmationModalOpen = false;
            _confirmationModalMb = null;

            if (_isWebBrowserActive)
            {
                _webBrowser.Deactivate();
                _isWebBrowserActive = false;
            }

            // Unsubscribe from panel changes
            if (PanelStateManager.Instance != null)
            {
                PanelStateManager.Instance.OnPanelChanged -= OnPanelChanged;
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

        private int FindActiveTabIndex()
        {
            if (_controller == null || _currentTabField == null || _storeTabTypeLookupField == null)
                return -1;

            try
            {
                var currentTab = _currentTabField.GetValue(_controller);
                if (currentTab == null) return -1;

                for (int i = 0; i < _tabs.Count; i++)
                {
                    if (_tabs[i].TabComponent == (MonoBehaviour)currentTab)
                        return i;
                }
            }
            catch { }

            return -1;
        }

        #endregion

        #region Announcements

        protected override string GetActivationAnnouncement()
        {
            // Auto-enter items for the currently active tab
            _navLevel = NavigationLevel.Items;
            DiscoverItems();

            if (_items.Count > 0)
            {
                _currentItemIndex = 0;
                _currentPurchaseOptionIndex = 0;

                string tabName = (_currentTabIndex >= 0 && _currentTabIndex < _tabs.Count)
                    ? _tabs[_currentTabIndex].DisplayName
                    : "Store";

                return $"Store, {tabName}. {_items.Count} items. {Strings.NavigateWithArrows}, Enter to buy, Backspace for tabs.";
            }

            // No items - stay at tab level
            _navLevel = NavigationLevel.Tabs;
            return $"Store. {_tabs.Count} tabs. {Strings.NavigateWithArrows}, Enter to select.";
        }

        protected override string GetElementAnnouncement(int index)
        {
            // This won't be used directly since we handle announcements ourselves
            return "";
        }

        private void AnnounceCurrentTab()
        {
            if (_currentTabIndex < 0 || _currentTabIndex >= _tabs.Count) return;

            var tab = _tabs[_currentTabIndex];

            if (tab.IsUtility)
            {
                _announcer.AnnounceInterrupt(
                    $"{_currentTabIndex + 1} of {_tabs.Count}: {tab.DisplayName}");
            }
            else
            {
                bool isActive = IsTabActive(tab);
                string activeIndicator = isActive ? ", active" : "";
                _announcer.AnnounceInterrupt(
                    $"{_currentTabIndex + 1} of {_tabs.Count}: {tab.DisplayName}{activeIndicator}");
            }
        }

        private void AnnounceCurrentItem()
        {
            if (_currentItemIndex < 0 || _currentItemIndex >= _items.Count) return;

            var item = _items[_currentItemIndex];
            string optionText = "";

            if (item.PurchaseOptions.Count > 0)
            {
                _currentPurchaseOptionIndex = Math.Min(_currentPurchaseOptionIndex, item.PurchaseOptions.Count - 1);
                var option = item.PurchaseOptions[_currentPurchaseOptionIndex];
                optionText = $", {FormatPurchaseOption(option)}";

                if (item.PurchaseOptions.Count > 1)
                {
                    optionText += $", option {_currentPurchaseOptionIndex + 1} of {item.PurchaseOptions.Count}";
                }
            }

            string descText = !string.IsNullOrEmpty(item.Description) ? $". {item.Description}" : "";

            _announcer.AnnounceInterrupt(
                $"{_currentItemIndex + 1} of {_items.Count}: {item.Label}{descText}{optionText}");
        }

        private void AnnouncePurchaseOption()
        {
            if (_currentItemIndex < 0 || _currentItemIndex >= _items.Count) return;

            var item = _items[_currentItemIndex];
            if (_currentPurchaseOptionIndex < 0 || _currentPurchaseOptionIndex >= item.PurchaseOptions.Count)
                return;

            var option = item.PurchaseOptions[_currentPurchaseOptionIndex];
            _announcer.AnnounceInterrupt(
                $"{FormatPurchaseOption(option)}, option {_currentPurchaseOptionIndex + 1} of {item.PurchaseOptions.Count}");
        }

        private string FormatPurchaseOption(PurchaseOption option)
        {
            // Synthetic Details option
            if (option.ButtonObject == null && option.PriceText == "Details")
                return "Details";
            // If currency name is empty (real money), just show the price
            if (string.IsNullOrEmpty(option.CurrencyName))
                return option.PriceText;
            return $"{option.PriceText} {option.CurrencyName}";
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

            // Check if store is still open
            if (!IsControllerOpenAndReady(_controller))
            {
                Deactivate();
                return;
            }

            // Check if confirmation modal opened - handle as popup instead of deactivating
            bool modalOpen = IsConfirmationModalOpen(_controller);
            if (modalOpen && !_wasConfirmationModalOpen)
            {
                // Modal just opened - treat it as a popup
                _wasConfirmationModalOpen = true;
                var modalObj = GetConfirmationModalGameObject();
                if (modalObj != null)
                {
                    MelonLogger.Msg($"[Store] Confirmation modal opened, handling as popup");
                    _activePopup = modalObj;
                    _isPopupActive = true;
                    _confirmationModalMb = GetConfirmationModalMb();
                    DiscoverConfirmationModalElements();
                    AnnounceConfirmationModal();
                }
                return;
            }
            else if (!modalOpen && _wasConfirmationModalOpen)
            {
                // Modal just closed - return to store
                _wasConfirmationModalOpen = false;
                if (_isPopupActive)
                {
                    MelonLogger.Msg("[Store] Confirmation modal closed, returning to store");
                    _activePopup = null;
                    _isPopupActive = false;
                    _popupElements.Clear();
                    _confirmationModalMb = null;
                    // Re-announce current position
                    if (_navLevel == NavigationLevel.Items && _items.Count > 0)
                        AnnounceCurrentItem();
                    else if (_navLevel == NavigationLevel.Tabs && _tabs.Count > 0)
                        AnnounceCurrentTab();
                }
            }

            // Update web browser accessibility (handles rescan timer)
            if (_isWebBrowserActive)
            {
                _webBrowser.Update();
                if (!_webBrowser.IsActive)
                {
                    MelonLogger.Msg("[Store] Web browser became inactive, returning to store");
                    _isWebBrowserActive = false;
                }
            }

            // Check if popup is still valid
            if (_isPopupActive && (_activePopup == null || !_activePopup.activeInHierarchy))
            {
                MelonLogger.Msg("[Store] Popup became invalid, returning to store");
                _activePopup = null;
                _isPopupActive = false;
                _popupElements.Clear();
            }

            // Handle loading state
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
                return; // Don't process input while loading
            }

            HandleStoreInput();
        }

        protected override bool ValidateElements()
        {
            // Override to check our own state instead of _elements
            return _controller != null && _controllerGameObject != null && _controllerGameObject.activeInHierarchy;
        }

        #endregion

        #region Input Handling

        private void HandleStoreInput()
        {
            // Web browser takes full control when active
            if (_isWebBrowserActive)
            {
                _webBrowser.HandleInput();
                return;
            }

            // Let base handle input field editing if active
            if (UIFocusTracker.IsEditingInputField())
            {
                HandleInputFieldNavigation();
                return;
            }

            // If popup is active, handle popup input
            if (_isPopupActive)
            {
                HandlePopupInput();
                return;
            }

            switch (_navLevel)
            {
                case NavigationLevel.Tabs:
                    HandleTabInput();
                    break;
                case NavigationLevel.Items:
                    HandleItemInput();
                    break;
            }
        }

        private void HandleTabInput()
        {
            // Up/Down navigate tabs
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                MoveTab(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                MoveTab(1);
                return;
            }

            // Tab/Shift+Tab for navigation
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool shiftTab = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                MoveTab(shiftTab ? -1 : 1);
                return;
            }

            // Home/End
            if (Input.GetKeyDown(KeyCode.Home))
            {
                if (_tabs.Count > 0)
                {
                    _currentTabIndex = 0;
                    AnnounceCurrentTab();
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                if (_tabs.Count > 0)
                {
                    _currentTabIndex = _tabs.Count - 1;
                    AnnounceCurrentTab();
                }
                return;
            }

            // Enter activates tab
            bool enterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            bool spacePressed = InputManager.GetKeyDownAndConsume(KeyCode.Space);
            if (enterPressed || spacePressed)
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
                ActivateCurrentTab();
                return;
            }

            // Backspace goes back
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                HandleBackFromStore();
                return;
            }
        }

        private void HandleItemInput()
        {
            // Details view takes over input when active
            if (_isDetailsViewActive)
            {
                HandleDetailsInput();
                return;
            }

            // Up/Down navigate items
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                MoveItem(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                MoveItem(1);
                return;
            }

            // Tab/Shift+Tab for navigation
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool shiftTab = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                MoveItem(shiftTab ? -1 : 1);
                return;
            }

            // Left/Right cycle purchase options
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                CyclePurchaseOption(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                CyclePurchaseOption(1);
                return;
            }

            // Home/End
            if (Input.GetKeyDown(KeyCode.Home))
            {
                if (_items.Count > 0)
                {
                    _currentItemIndex = 0;
                    _currentPurchaseOptionIndex = 0;
                    AnnounceCurrentItem();
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                if (_items.Count > 0)
                {
                    _currentItemIndex = _items.Count - 1;
                    _currentPurchaseOptionIndex = 0;
                    AnnounceCurrentItem();
                }
                return;
            }

            // Enter activates purchase option
            bool enterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            bool spacePressed = InputManager.GetKeyDownAndConsume(KeyCode.Space);
            if (enterPressed || spacePressed)
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
                ActivateCurrentPurchaseOption();
                return;
            }

            // Backspace goes back to tabs
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                ReturnToTabs();
                return;
            }
        }

        // Override base HandleInput to do nothing (we handle everything in HandleStoreInput)
        protected override void HandleInput() { }

        // Override HandleCustomInput since base calls it
        protected override bool HandleCustomInput() => false;

        #endregion

        #region Tab Navigation

        private void MoveTab(int direction)
        {
            if (_tabs.Count == 0) return;

            int newIndex = _currentTabIndex + direction;

            if (newIndex < 0)
            {
                _announcer.Announce(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }

            if (newIndex >= _tabs.Count)
            {
                _announcer.Announce(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            _currentTabIndex = newIndex;
            AnnounceCurrentTab();
        }

        private void ActivateCurrentTab()
        {
            if (_currentTabIndex < 0 || _currentTabIndex >= _tabs.Count) return;

            var tab = _tabs[_currentTabIndex];

            // Utility entries are activated directly (not store tabs)
            if (tab.IsUtility)
            {
                MelonLogger.Msg($"[Store] Activating utility: {tab.DisplayName}");
                ActivateUtilityElement(tab);
                return;
            }

            // Check if this tab is already active - just enter items directly
            if (IsTabActive(tab))
            {
                _navLevel = NavigationLevel.Items;
                DiscoverItems();

                if (_items.Count > 0)
                {
                    _currentItemIndex = 0;
                    _currentPurchaseOptionIndex = 0;
                    _announcer.AnnounceInterrupt(
                        $"{tab.DisplayName}. {_items.Count} items.");
                    AnnounceCurrentItem();
                }
                else
                {
                    _announcer.AnnounceInterrupt($"{tab.DisplayName}. No items available.");
                    _navLevel = NavigationLevel.Tabs;
                }
                return;
            }

            // Activate the tab via OnClicked()
            MelonLogger.Msg($"[Store] Activating tab: {tab.DisplayName}");

            if (_tabOnClickedMethod != null)
            {
                try
                {
                    _tabOnClickedMethod.Invoke(tab.TabComponent, null);
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[Store] Error calling OnClicked: {ex.Message}");
                    // Fallback: try UIActivator
                    UIActivator.Activate(tab.GameObject);
                }
            }
            else
            {
                UIActivator.Activate(tab.GameObject);
            }

            _announcer.AnnounceInterrupt($"Loading {tab.DisplayName}...");

            // Start waiting for items to load
            _waitingForTabLoad = true;
            _loadCheckTimer = TabLoadCheckInterval;
        }

        private bool IsTabActive(TabInfo tab)
        {
            if (_currentTabField == null || _controller == null) return false;

            try
            {
                var currentTab = _currentTabField.GetValue(_controller);
                if (currentTab == null) return false;
                return (MonoBehaviour)currentTab == tab.TabComponent;
            }
            catch { return false; }
        }

        #endregion

        #region Loading Detection

        private bool IsLoadingComplete()
        {
            if (_controller == null || _itemDisplayQueueField == null) return true;

            try
            {
                var queue = _itemDisplayQueueField.GetValue(_controller);
                if (queue == null) return true;

                // Queue<T> has a Count property
                var countProp = queue.GetType().GetProperty("Count");
                if (countProp == null) return true;

                int count = (int)countProp.GetValue(queue);
                return count == 0;
            }
            catch
            {
                return true;
            }
        }

        private void OnTabLoadComplete()
        {
            _waitingForTabLoad = false;
            MelonLogger.Msg("[Store] Tab load complete");

            // Refresh tab discovery in case tabs changed
            DiscoverTabs();
            _currentTabIndex = FindActiveTabIndex();

            // Discover items for the new tab
            _navLevel = NavigationLevel.Items;
            DiscoverItems();

            if (_items.Count > 0)
            {
                _currentItemIndex = 0;
                _currentPurchaseOptionIndex = 0;

                string tabName = (_currentTabIndex >= 0 && _currentTabIndex < _tabs.Count)
                    ? _tabs[_currentTabIndex].DisplayName
                    : "Store";

                _announcer.AnnounceInterrupt($"{tabName}. {_items.Count} items.");
                AnnounceCurrentItem();
            }
            else
            {
                string tabName = (_currentTabIndex >= 0 && _currentTabIndex < _tabs.Count)
                    ? _tabs[_currentTabIndex].DisplayName
                    : "tab";

                _announcer.AnnounceInterrupt($"{tabName}. No items available.");
                _navLevel = NavigationLevel.Tabs;
            }
        }

        #endregion

        #region Item Navigation

        private void MoveItem(int direction)
        {
            if (_items.Count == 0) return;

            int newIndex = _currentItemIndex + direction;

            if (newIndex < 0)
            {
                _announcer.Announce(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }

            if (newIndex >= _items.Count)
            {
                _announcer.Announce(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            _currentItemIndex = newIndex;
            _currentPurchaseOptionIndex = 0;
            AnnounceCurrentItem();
        }

        private void CyclePurchaseOption(int direction)
        {
            if (_currentItemIndex < 0 || _currentItemIndex >= _items.Count) return;

            var item = _items[_currentItemIndex];
            if (item.PurchaseOptions.Count <= 1)
            {
                _announcer.Announce(
                    direction > 0 ? Strings.EndOfList : Strings.BeginningOfList,
                    AnnouncementPriority.Normal);
                return;
            }

            int newIndex = _currentPurchaseOptionIndex + direction;

            if (newIndex < 0)
            {
                _announcer.Announce(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }

            if (newIndex >= item.PurchaseOptions.Count)
            {
                _announcer.Announce(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            _currentPurchaseOptionIndex = newIndex;
            AnnouncePurchaseOption();
        }

        private void ActivateCurrentPurchaseOption()
        {
            if (_currentItemIndex < 0 || _currentItemIndex >= _items.Count) return;

            var item = _items[_currentItemIndex];
            if (item.PurchaseOptions.Count == 0)
            {
                _announcer.Announce("No purchase option available", AnnouncementPriority.Normal);
                return;
            }

            if (_currentPurchaseOptionIndex < 0 || _currentPurchaseOptionIndex >= item.PurchaseOptions.Count)
                _currentPurchaseOptionIndex = 0;

            var option = item.PurchaseOptions[_currentPurchaseOptionIndex];

            // Synthetic Details option - open details view instead of purchasing
            if (option.ButtonObject == null && option.PriceText == "Details")
            {
                OpenDetailsView(item);
                return;
            }

            MelonLogger.Msg($"[Store] Activating purchase: {item.Label} - {option.PriceText} {option.CurrencyName}");

            UIActivator.Activate(option.ButtonObject);
        }

        #endregion

        #region Details View

        private bool HasItemDetails(MonoBehaviour storeItemBase)
        {
            // Check tooltip mTerm
            if (HasTooltipText(storeItemBase))
                return true;

            // Check for StoreDisplayPreconDeck or StoreDisplayCardViewBundle child
            if (_storeItemDisplayType != null)
            {
                var display = GetItemDisplay(storeItemBase);
                if (display != null)
                {
                    var displayType = display.GetType();
                    if ((_storeDisplayPreconDeckType != null && _storeDisplayPreconDeckType.IsAssignableFrom(displayType)) ||
                        (_storeDisplayCardViewBundleType != null && _storeDisplayCardViewBundleType.IsAssignableFrom(displayType)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasTooltipText(MonoBehaviour storeItemBase)
        {
            if (_tooltipTriggerField == null || _locStringField == null || _locStringMTermField == null)
                return false;

            try
            {
                var tooltip = _tooltipTriggerField.GetValue(storeItemBase);
                if (tooltip == null) return false;

                var locString = _locStringField.GetValue(tooltip);
                if (locString == null) return false;

                string mTerm = _locStringMTermField.GetValue(locString) as string;
                return !string.IsNullOrEmpty(mTerm) && mTerm != "MainNav/General/Empty_String";
            }
            catch { return false; }
        }

        private MonoBehaviour GetItemDisplay(MonoBehaviour storeItemBase)
        {
            if (_itemDisplayField == null) return null;
            try
            {
                return _itemDisplayField.GetValue(storeItemBase) as MonoBehaviour;
            }
            catch { return null; }
        }

        private void OpenDetailsView(ItemInfo item)
        {
            _detailsCards.Clear();
            _detailsCardBlocks.Clear();
            _detailsCardIndex = 0;
            _detailsBlockIndex = 0;

            // Extract tooltip description
            _detailsDescription = ExtractTooltipDescription(item.StoreItemBase);

            // Extract card list from display
            ExtractCardEntries(item.StoreItemBase, _detailsCards);

            if (string.IsNullOrEmpty(_detailsDescription) && _detailsCards.Count == 0)
            {
                _announcer.Announce("No details available", AnnouncementPriority.Normal);
                return;
            }

            _isDetailsViewActive = true;

            // Build announcement
            var parts = new List<string>();
            parts.Add("Details");

            if (!string.IsNullOrEmpty(_detailsDescription))
                parts.Add(_detailsDescription);

            if (_detailsCards.Count > 0)
            {
                string cardCount = _detailsCards.Count == 1 ? "1 card" : $"{_detailsCards.Count} cards";
                parts.Add(cardCount);
                // Announce first card
                parts.Add(FormatCardAnnouncement(_detailsCards[0], 0));
            }

            _announcer.AnnounceInterrupt(string.Join(". ", parts));
            MelonLogger.Msg($"[Store] Opened details view: {_detailsCards.Count} cards, description={!string.IsNullOrEmpty(_detailsDescription)}");
        }

        private string ExtractTooltipDescription(MonoBehaviour storeItemBase)
        {
            if (_tooltipTriggerField == null || _locStringField == null ||
                _locStringMTermField == null || _locStringToStringMethod == null)
                return null;

            try
            {
                var tooltip = _tooltipTriggerField.GetValue(storeItemBase);
                if (tooltip == null) return null;

                var locString = _locStringField.GetValue(tooltip);
                if (locString == null) return null;

                string mTerm = _locStringMTermField.GetValue(locString) as string;
                if (string.IsNullOrEmpty(mTerm) || mTerm == "MainNav/General/Empty_String")
                    return null;

                // Call ToString() on the LocalizedString struct which resolves the term
                string resolved = _locStringToStringMethod.Invoke(locString, null) as string;
                if (!string.IsNullOrEmpty(resolved) && !resolved.StartsWith("$"))
                {
                    // Clean rich text tags
                    resolved = System.Text.RegularExpressions.Regex.Replace(resolved, @"<[^>]+>", "").Trim();
                    return resolved;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[Store] Error extracting tooltip description: {ex.Message}");
            }

            return null;
        }

        private void ExtractCardEntries(MonoBehaviour storeItemBase, List<DetailCardEntry> entries)
        {
            var display = GetItemDisplay(storeItemBase);
            if (display == null) return;

            var displayType = display.GetType();

            try
            {
                // PreconDeck path: CardData property returns List<CardDataForTile>
                if (_storeDisplayPreconDeckType != null &&
                    _storeDisplayPreconDeckType.IsAssignableFrom(displayType) &&
                    _preconCardDataProp != null)
                {
                    var cardDataList = _preconCardDataProp.GetValue(display);
                    if (cardDataList is System.Collections.IList list)
                        ExtractFromCardDataList(list, entries);
                }
                // CardViewBundle path: BundleCardViews returns List<StoreCardView>
                else if (_storeDisplayCardViewBundleType != null &&
                         _storeDisplayCardViewBundleType.IsAssignableFrom(displayType) &&
                         _bundleCardViewsProp != null)
                {
                    var cardViews = _bundleCardViewsProp.GetValue(display);
                    if (cardViews is System.Collections.IList viewList)
                        ExtractFromBundleCardViews(viewList, entries);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[Store] Error extracting card entries: {ex.Message}");
            }
        }

        private void ExtractFromCardDataList(System.Collections.IList list, List<DetailCardEntry> entries)
        {
            if (_cardDataForTileCardProp == null || _cardDataForTileQuantityProp == null || _cardDataGrpIdProp == null)
                return;

            foreach (var item in list)
            {
                try
                {
                    var cardData = _cardDataForTileCardProp.GetValue(item);
                    if (cardData == null) continue;

                    uint grpId = (uint)_cardDataGrpIdProp.GetValue(cardData);
                    if (grpId == 0) continue;

                    int quantity = (int)_cardDataForTileQuantityProp.GetValue(item);
                    string name = CardModelProvider.GetNameFromGrpId(grpId);
                    if (string.IsNullOrEmpty(name))
                        name = $"Card #{grpId}";

                    string manaCost = null;
                    if (_cardDataManaTextProp != null)
                    {
                        try { manaCost = _cardDataManaTextProp.GetValue(cardData) as string; }
                        catch { }
                    }

                    entries.Add(new DetailCardEntry
                    {
                        GrpId = grpId,
                        Quantity = quantity,
                        Name = name,
                        ManaCost = !string.IsNullOrEmpty(manaCost) ? FormatManaForScreenReader(manaCost) : null,
                        CardDataObj = cardData
                    });
                }
                catch { }
            }
        }

        private void ExtractFromBundleCardViews(System.Collections.IList viewList, List<DetailCardEntry> entries)
        {
            if (_cardDataGrpIdProp == null) return;

            foreach (var view in viewList)
            {
                try
                {
                    var viewMb = view as MonoBehaviour;
                    if (viewMb == null || !viewMb.gameObject.activeInHierarchy) continue;

                    var viewType = viewMb.GetType();
                    var cardProp = viewType.GetProperty("Card", BindingFlags.Public | BindingFlags.Instance);
                    if (cardProp == null) continue;

                    var cardData = cardProp.GetValue(viewMb);
                    if (cardData == null) continue;

                    uint grpId = (uint)_cardDataGrpIdProp.GetValue(cardData);
                    if (grpId == 0) continue;

                    string name = CardModelProvider.GetNameFromGrpId(grpId);
                    if (string.IsNullOrEmpty(name))
                        name = $"Card #{grpId}";

                    string manaCost = null;
                    if (_cardDataManaTextProp != null)
                    {
                        try { manaCost = _cardDataManaTextProp.GetValue(cardData) as string; }
                        catch { }
                    }

                    entries.Add(new DetailCardEntry
                    {
                        GrpId = grpId,
                        Quantity = 1,
                        Name = name,
                        ManaCost = !string.IsNullOrEmpty(manaCost) ? FormatManaForScreenReader(manaCost) : null,
                        CardDataObj = cardData
                    });
                }
                catch { }
            }
        }

        private string FormatCardAnnouncement(DetailCardEntry card, int index)
        {
            var parts = new List<string>();
            parts.Add(card.Name);
            if (card.Quantity > 1)
                parts.Add($"times {card.Quantity}");
            if (!string.IsNullOrEmpty(card.ManaCost))
                parts.Add(card.ManaCost);
            parts.Add($"{index + 1} of {_detailsCards.Count}");
            return string.Join(", ", parts);
        }

        private static string FormatManaForScreenReader(string manaText)
        {
            if (string.IsNullOrEmpty(manaText)) return "";

            // Convert "{2}{B}{B}" -> "2, B, B"
            var parts = new List<string>();
            int i = 0;
            while (i < manaText.Length)
            {
                if (manaText[i] == '{')
                {
                    int end = manaText.IndexOf('}', i);
                    if (end > i)
                    {
                        string symbol = manaText.Substring(i + 1, end - i - 1);
                        // Expand common shorthand
                        switch (symbol.ToUpper())
                        {
                            case "W": parts.Add("White"); break;
                            case "U": parts.Add("Blue"); break;
                            case "B": parts.Add("Black"); break;
                            case "R": parts.Add("Red"); break;
                            case "G": parts.Add("Green"); break;
                            case "C": parts.Add("Colorless"); break;
                            case "X": parts.Add("X"); break;
                            default: parts.Add(symbol); break;
                        }
                        i = end + 1;
                        continue;
                    }
                }
                i++;
            }

            return parts.Count > 0 ? string.Join(", ", parts) : manaText;
        }

        private void HandleDetailsInput()
        {
            // Left/Right: navigate between cards
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                MoveDetailsCard(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                MoveDetailsCard(1);
                return;
            }

            // Up/Down: navigate card info blocks
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                MoveDetailsBlock(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                MoveDetailsBlock(1);
                return;
            }

            // Home/End: jump to first/last card
            if (Input.GetKeyDown(KeyCode.Home))
            {
                if (_detailsCards.Count > 0 && _detailsCardIndex != 0)
                {
                    _detailsCardIndex = 0;
                    _detailsCardBlocks.Clear();
                    _detailsBlockIndex = 0;
                    _announcer.AnnounceInterrupt(FormatCardAnnouncement(_detailsCards[0], 0));
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                if (_detailsCards.Count > 0 && _detailsCardIndex != _detailsCards.Count - 1)
                {
                    _detailsCardIndex = _detailsCards.Count - 1;
                    _detailsCardBlocks.Clear();
                    _detailsBlockIndex = 0;
                    _announcer.AnnounceInterrupt(FormatCardAnnouncement(_detailsCards[_detailsCardIndex], _detailsCardIndex));
                }
                return;
            }

            // Tab/Shift+Tab: navigate cards like Left/Right
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                MoveDetailsCard(shift ? -1 : 1);
                return;
            }

            // Enter/Space: re-read current card or description
            bool enterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            bool spacePressed = InputManager.GetKeyDownAndConsume(KeyCode.Space);
            if (enterPressed || spacePressed)
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
                if (_detailsCards.Count > 0 && _detailsCardIndex >= 0 && _detailsCardIndex < _detailsCards.Count)
                    _announcer.AnnounceInterrupt(FormatCardAnnouncement(_detailsCards[_detailsCardIndex], _detailsCardIndex));
                else if (!string.IsNullOrEmpty(_detailsDescription))
                    _announcer.AnnounceInterrupt(_detailsDescription);
                return;
            }

            // Backspace: close details view
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                CloseDetailsView();
                return;
            }
        }

        private void MoveDetailsCard(int direction)
        {
            if (_detailsCards.Count == 0)
            {
                if (!string.IsNullOrEmpty(_detailsDescription))
                    _announcer.AnnounceInterrupt(_detailsDescription);
                return;
            }

            int newIndex = _detailsCardIndex + direction;

            if (newIndex < 0)
            {
                _announcer.Announce(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }

            if (newIndex >= _detailsCards.Count)
            {
                _announcer.Announce(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            _detailsCardIndex = newIndex;
            _detailsCardBlocks.Clear();
            _detailsBlockIndex = 0;
            _announcer.AnnounceInterrupt(FormatCardAnnouncement(_detailsCards[_detailsCardIndex], _detailsCardIndex));
        }

        private void MoveDetailsBlock(int direction)
        {
            if (_detailsCards.Count == 0) return;

            // Lazy-load card info blocks on first Up/Down press
            if (_detailsCardBlocks.Count == 0)
            {
                var card = _detailsCards[_detailsCardIndex];
                CardInfo info = default;
                if (card.CardDataObj != null)
                {
                    info = CardModelProvider.ExtractCardInfoFromObject(card.CardDataObj);
                }
                if (!info.IsValid)
                {
                    var cardInfo = CardModelProvider.GetCardInfoFromGrpId(card.GrpId);
                    if (cardInfo.HasValue)
                        info = cardInfo.Value;
                }

                if (info.IsValid)
                {
                    info.Quantity = card.Quantity;
                    _detailsCardBlocks = CardDetector.BuildInfoBlocks(info);
                }

                if (_detailsCardBlocks.Count == 0)
                {
                    _announcer.Announce("No card details available", AnnouncementPriority.Normal);
                    return;
                }

                // Start at first block for Down, last for Up
                _detailsBlockIndex = direction > 0 ? 0 : _detailsCardBlocks.Count - 1;
                AnnounceDetailsBlock();
                return;
            }

            int newIndex = _detailsBlockIndex + direction;

            if (newIndex < 0)
            {
                _announcer.Announce(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }

            if (newIndex >= _detailsCardBlocks.Count)
            {
                _announcer.Announce(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            _detailsBlockIndex = newIndex;
            AnnounceDetailsBlock();
        }

        private void AnnounceDetailsBlock()
        {
            if (_detailsBlockIndex < 0 || _detailsBlockIndex >= _detailsCardBlocks.Count) return;

            var block = _detailsCardBlocks[_detailsBlockIndex];
            _announcer.AnnounceInterrupt($"{block.Label}: {block.Content}");
        }

        private void CloseDetailsView()
        {
            _isDetailsViewActive = false;
            _detailsCards.Clear();
            _detailsCardBlocks.Clear();
            _detailsDescription = null;
            _detailsCardIndex = 0;
            _detailsBlockIndex = 0;

            // Re-announce current item
            AnnounceCurrentItem();
        }

        #endregion

        #region Utility Activation

        private void ActivateUtilityElement(TabInfo tab)
        {
            // Payment button: call OnButton_PaymentSetup() directly on controller
            // Note: UIActivator on TextButton_PaymentInfo also works - revert to UIActivator if reflection causes issues
            if (tab.DisplayName == "Change payment method" && _onButtonPaymentSetupMethod != null && _controller != null)
            {
                try
                {
                    MelonLogger.Msg("[Store] Calling OnButton_PaymentSetup() via reflection");
                    _onButtonPaymentSetupMethod.Invoke(_controller, null);
                    return;
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[Store] Error calling OnButton_PaymentSetup: {ex.Message}");
                }
            }

            // Pack progress: info-only, re-announce stored text
            if (tab.DisplayName.StartsWith("Pack progress:"))
            {
                _announcer.AnnounceInterrupt(tab.DisplayName);
                return;
            }

            // Default: use UIActivator for other utility elements
            UIActivator.Activate(tab.GameObject);
        }

        #endregion

        #region Popup Handling (follows SettingsMenuNavigator pattern)

        private MonoBehaviour GetConfirmationModalMb()
        {
            if (_confirmationModalField == null || _controller == null) return null;
            try
            {
                return _confirmationModalField.GetValue(_controller) as MonoBehaviour;
            }
            catch { return null; }
        }

        private void DiscoverConfirmationModalElements()
        {
            _popupElements.Clear();
            _popupElementIndex = 0;

            if (_confirmationModalMb == null || _modalButtonFields == null) return;

            MelonLogger.Msg($"[Store] Discovering confirmation modal elements");

            var flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

            // Get the modal's own purchase buttons (not the reparented item widget's)
            foreach (var field in _modalButtonFields)
            {
                if (field == null) continue;
                try
                {
                    var buttonStruct = field.GetValue(_confirmationModalMb);
                    if (buttonStruct == null) continue;

                    // Get CustomButton from the struct
                    var customButton = _modalPbButtonField?.GetValue(buttonStruct) as MonoBehaviour;
                    if (customButton == null || !customButton.gameObject.activeInHierarchy) continue;

                    // Check Interactable property
                    var interactableProp = customButton.GetType().GetProperty("Interactable", flags);
                    if (interactableProp != null)
                    {
                        bool interactable = (bool)interactableProp.GetValue(customButton);
                        if (!interactable) continue;
                    }

                    // Get price text from Label (TMP_Text)
                    var labelTmp = _modalPbLabelField?.GetValue(buttonStruct) as TMPro.TMP_Text;
                    string priceText = labelTmp?.text?.Trim() ?? "";

                    // Also try getting text from button children as fallback
                    if (string.IsNullOrEmpty(priceText))
                        priceText = UITextExtractor.GetText(customButton.gameObject) ?? customButton.gameObject.name;

                    _popupElements.Add((customButton.gameObject, $"{priceText}, button"));
                }
                catch { }
            }

            // Add Cancel option
            _popupElements.Add((null, "Cancel"));

            MelonLogger.Msg($"[Store] Found {_popupElements.Count} confirmation modal elements");
        }

        private void AnnounceConfirmationModal()
        {
            // Extract text from the modal's label and product description
            string labelText = null;
            string descText = null;

            if (_confirmationModalMb != null)
            {
                var flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

                // Get _label (Localize) -> TMP_Text
                var labelField = _confirmationModalType?.GetField("_label", flags);
                if (labelField != null)
                {
                    try
                    {
                        var localize = labelField.GetValue(_confirmationModalMb) as MonoBehaviour;
                        if (localize != null)
                        {
                            var tmp = localize.GetComponentInChildren<TMPro.TMP_Text>();
                            if (tmp != null && !string.IsNullOrEmpty(tmp.text?.Trim()))
                                labelText = tmp.text.Trim();
                        }
                    }
                    catch { }
                }

                // Get product list text from _productListContainer children
                var productField = _confirmationModalType?.GetField("_productListContainer", flags);
                if (productField != null)
                {
                    try
                    {
                        var container = productField.GetValue(_confirmationModalMb) as Transform;
                        if (container != null && container.gameObject.activeInHierarchy)
                        {
                            var texts = container.GetComponentsInChildren<TMPro.TMP_Text>(false);
                            foreach (var t in texts)
                            {
                                string text = t.text?.Trim();
                                if (!string.IsNullOrEmpty(text) && text.Length > 3)
                                {
                                    text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", "").Trim();
                                    if (text.Length > 3)
                                    {
                                        descText = text;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            // Fallback to generic text extraction
            if (string.IsNullOrEmpty(labelText))
                labelText = ExtractPopupMessage(_activePopup);

            string announcement = "Confirm purchase";
            if (!string.IsNullOrEmpty(labelText))
                announcement += $": {labelText}";
            if (!string.IsNullOrEmpty(descText))
                announcement += $". {descText}";
            announcement += $". {_popupElements.Count} options.";

            _announcer.AnnounceInterrupt(announcement);

            if (_popupElements.Count > 0)
            {
                _popupElementIndex = 0;
                _announcer.Announce($"1 of {_popupElements.Count}: {_popupElements[0].label}", AnnouncementPriority.Normal);
            }
        }

        private void DiscoverPopupElements()
        {
            _popupElements.Clear();
            _popupElementIndex = 0;

            if (_activePopup == null) return;

            MelonLogger.Msg($"[Store] Discovering popup elements in: {_activePopup.name}");

            var addedObjects = new HashSet<GameObject>();
            var discovered = new List<(GameObject obj, string label, float sortOrder)>();

            // Find SystemMessageButtonView buttons (MTGA's standard popup buttons)
            foreach (var mb in _activePopup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (addedObjects.Contains(mb.gameObject)) continue;

                string typeName = mb.GetType().Name;
                if (typeName == "SystemMessageButtonView" || typeName == "CustomButton" || typeName == "CustomButtonWithTooltip")
                {
                    string label = UITextExtractor.GetText(mb.gameObject);
                    if (string.IsNullOrEmpty(label)) label = mb.gameObject.name;

                    var pos = mb.gameObject.transform.position;
                    discovered.Add((mb.gameObject, $"{label}, button", -pos.y * 1000 + pos.x));
                    addedObjects.Add(mb.gameObject);
                }
            }

            // Also check standard Unity Buttons
            foreach (var button in _activePopup.GetComponentsInChildren<Button>(true))
            {
                if (button == null || !button.gameObject.activeInHierarchy || !button.interactable) continue;
                if (addedObjects.Contains(button.gameObject)) continue;

                string label = UITextExtractor.GetText(button.gameObject);
                if (string.IsNullOrEmpty(label)) label = button.gameObject.name;

                var pos = button.gameObject.transform.position;
                discovered.Add((button.gameObject, $"{label}, button", -pos.y * 1000 + pos.x));
                addedObjects.Add(button.gameObject);
            }

            foreach (var (obj, label, _) in discovered.OrderBy(x => x.sortOrder))
            {
                _popupElements.Add((obj, label));
            }

            MelonLogger.Msg($"[Store] Found {_popupElements.Count} popup elements");
        }

        private void AnnouncePopup()
        {
            // Try to extract popup message
            string message = ExtractPopupMessage(_activePopup);
            string announcement = !string.IsNullOrEmpty(message)
                ? $"Confirmation. {message}. {_popupElements.Count} options."
                : $"Confirmation. {_popupElements.Count} options.";

            _announcer.AnnounceInterrupt(announcement);

            if (_popupElements.Count > 0)
            {
                _popupElementIndex = 0;
                _announcer.Announce($"{_popupElements[0].label}", AnnouncementPriority.Normal);
            }
        }

        private string ExtractPopupMessage(GameObject popup)
        {
            if (popup == null) return null;

            var texts = popup.GetComponentsInChildren<TMPro.TMP_Text>(true)
                .Where(t => t != null && t.gameObject.activeInHierarchy)
                .OrderByDescending(t => t.fontSize)
                .ToList();

            foreach (var text in texts)
            {
                string content = text.text?.Trim();
                if (string.IsNullOrEmpty(content) || content.Length < 3) continue;

                // Skip button labels
                var parent = text.transform.parent;
                bool isButtonText = false;
                while (parent != null)
                {
                    if (parent.name.ToLower().Contains("button"))
                    {
                        isButtonText = true;
                        break;
                    }
                    parent = parent.parent;
                }

                if (!isButtonText && content.Length > 5)
                {
                    content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", "").Trim();
                    if (!string.IsNullOrEmpty(content))
                        return content;
                }
            }

            return null;
        }

        private void HandlePopupInput()
        {
            if (_popupElements.Count == 0) return;

            // Up/Down navigate popup elements
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                MovePopupElement(-1);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                MovePopupElement(1);
                return;
            }
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                MovePopupElement(shift ? -1 : 1);
                return;
            }

            // Enter activates current popup element
            bool enterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            bool spacePressed = InputManager.GetKeyDownAndConsume(KeyCode.Space);
            if (enterPressed || spacePressed)
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
                if (_popupElementIndex >= 0 && _popupElementIndex < _popupElements.Count)
                {
                    var elem = _popupElements[_popupElementIndex];
                    MelonLogger.Msg($"[Store] Activating popup element: {elem.label}");
                    if (elem.obj == null)
                    {
                        // Synthetic cancel option
                        DismissPopup();
                    }
                    else
                    {
                        UIActivator.Activate(elem.obj);
                    }
                }
                return;
            }

            // Backspace dismisses popup
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                DismissPopup();
                return;
            }
        }

        private void MovePopupElement(int direction)
        {
            int newIndex = _popupElementIndex + direction;
            if (newIndex < 0)
            {
                _announcer.Announce(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }
            if (newIndex >= _popupElements.Count)
            {
                _announcer.Announce(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }
            _popupElementIndex = newIndex;
            _announcer.AnnounceInterrupt(
                $"{_popupElementIndex + 1} of {_popupElements.Count}: {_popupElements[_popupElementIndex].label}");
        }

        private void DismissPopup()
        {
            if (_activePopup == null) return;

            // If this is the confirmation modal, call Close() directly
            if (_confirmationModalMb != null && _modalCloseMethod != null)
            {
                try
                {
                    MelonLogger.Msg("[Store] Closing confirmation modal via Close()");
                    _modalCloseMethod.Invoke(_confirmationModalMb, null);
                    _announcer.Announce("Cancelled", AnnouncementPriority.High);
                    return;
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[Store] Error calling Close(): {ex.Message}");
                }
            }

            // Look for cancel/close button
            string[] cancelPatterns = { "cancel", "close", "no", "abbrechen", "nein", "zurück" };

            foreach (var (obj, label) in _popupElements)
            {
                string lowerLabel = label.ToLower();
                foreach (var pattern in cancelPatterns)
                {
                    if (lowerLabel.Contains(pattern))
                    {
                        MelonLogger.Msg($"[Store] Dismissing popup via: {label}");
                        _announcer.Announce("Cancelled", AnnouncementPriority.High);
                        UIActivator.Activate(obj);
                        return;
                    }
                }
            }

            // Fallback: try SystemMessageView.OnBack()
            foreach (var mb in _activePopup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && mb.GetType().Name == "SystemMessageView")
                {
                    var onBack = mb.GetType().GetMethod("OnBack",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (onBack != null && onBack.GetParameters().Length == 1)
                    {
                        try
                        {
                            MelonLogger.Msg("[Store] Invoking SystemMessageView.OnBack()");
                            onBack.Invoke(mb, new object[] { null });
                            _announcer.Announce("Cancelled", AnnouncementPriority.High);
                            return;
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Msg($"[Store] Error invoking OnBack: {ex.Message}");
                        }
                    }
                }
            }

            MelonLogger.Msg("[Store] No cancel button found in popup");
        }

        #endregion

        #region Back Navigation

        private void ReturnToTabs()
        {
            _isDetailsViewActive = false;
            _detailsCards.Clear();
            _detailsCardBlocks.Clear();
            _detailsDescription = null;
            _navLevel = NavigationLevel.Tabs;
            _items.Clear();

            // Refresh tabs in case something changed
            DiscoverTabs();
            _currentTabIndex = FindActiveTabIndex();
            if (_currentTabIndex < 0 && _tabs.Count > 0)
                _currentTabIndex = 0;

            _announcer.AnnounceInterrupt($"Tabs. {_tabs.Count} tabs.");
            AnnounceCurrentTab();
        }

        private void HandleBackFromStore()
        {
            // At tab level, Backspace navigates home (standard back)
            MelonLogger.Msg("[Store] Back from store - navigating home");

            // Find and click the NavBar Home button via standard back mechanism
            // The game handles Escape as back navigation, but we use a softer approach:
            // Just deactivate ourselves and let GeneralMenuNavigator handle the back action
            Deactivate();

            // Simulate Escape which the game interprets as "back"
            // (The game's InputManager will process this next frame)
        }

        #endregion
    }
}
