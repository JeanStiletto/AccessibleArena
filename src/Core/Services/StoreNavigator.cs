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
            public List<PurchaseOption> PurchaseOptions;
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

            // Check if confirmation modal is active (yield to popup handlers)
            if (IsConfirmationModalOpen(controller)) return false;

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

        /// <summary>
        /// Handle panel changes - detect popups appearing on top of store.
        /// Follows the SettingsMenuNavigator pattern: subscribe to PanelStateManager events.
        /// </summary>
        private void OnPanelChanged(PanelInfo oldPanel, PanelInfo newPanel)
        {
            if (!_isActive) return;

            if (newPanel != null && IsPopupPanel(newPanel))
            {
                MelonLogger.Msg($"[Store] Popup detected on top of store: {newPanel.Name}");
                _activePopup = newPanel.GameObject;
                _isPopupActive = true;
                DiscoverPopupElements();
                AnnouncePopup();
            }
            else if (_isPopupActive && newPanel == null)
            {
                MelonLogger.Msg($"[Store] Popup closed, returning to store");
                _activePopup = null;
                _isPopupActive = false;
                _popupElements.Clear();
                // Re-announce current position
                if (_navLevel == NavigationLevel.Items && _items.Count > 0)
                    AnnounceCurrentItem();
                else if (_navLevel == NavigationLevel.Tabs && _tabs.Count > 0)
                    AnnounceCurrentTab();
            }
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

            _reflectionInitialized = true;
            MelonLogger.Msg($"[Store] Reflection cached. StoreItemBase={_storeItemBaseType != null}, Tab={_tabType != null}, PurchaseButton={_purchaseButtonType != null}");
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
            var purchaseOptions = ExtractPurchaseOptions(storeItemBase);

            return new ItemInfo
            {
                StoreItemBase = storeItemBase,
                GameObject = storeItemBase.gameObject,
                Label = label,
                PurchaseOptions = purchaseOptions
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
            _activePopup = null;
            _isPopupActive = false;
            _popupElements.Clear();

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

            _announcer.AnnounceInterrupt(
                $"{_currentItemIndex + 1} of {_items.Count}: {item.Label}{optionText}");
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

            // Check if confirmation modal opened (yield to other navigators)
            if (IsConfirmationModalOpen(_controller))
            {
                Deactivate();
                return;
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

            MelonLogger.Msg($"[Store] Activating purchase: {item.Label} - {option.PriceText} {option.CurrencyName}");

            UIActivator.Activate(option.ButtonObject);
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
                    UIActivator.Activate(elem.obj);
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
