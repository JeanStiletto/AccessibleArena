using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Utils;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for the profile's Favorite Stores tab (Wizards Play Network, MTGA 2026.63.10).
    ///
    /// The tab lists the local game stores the player favorited in the Store and Event Locator
    /// or the Companion app, and holds the two account-wide store settings (store messages in
    /// the inbox, store events in the home carousel). Each store row shows name, address, a
    /// premium-store badge, a bell that toggles that store's messages and a website button.
    ///
    /// Stores are read from FavoriteStoreManager, the same data the list cells are filled from,
    /// and listed first. Each row's buttons become attached actions (Left/Right), so one stop
    /// per store. The rest of the panel is walked in layout order: info texts become text
    /// blocks, the toggles and buttons stay activatable controls.
    /// </summary>
    public class FavoriteStoresNavigator : BaseNavigator
    {
        #region Constants

        // Same tier as SetCollectionNavigator: both preempt ProfileNavigator (56) for one of the
        // profile's sub-screens, and the game never shows the two at once.
        private const int FavoriteStoresPriority = 58;

        // DetectScreen runs every frame while any lower-priority navigator is active, so the
        // scene search for the profile controller is throttled.
        private const float DetectIntervalSeconds = 0.25f;

        // Store messages and the store list are written and read through the server.
        private const float ServerChangeTimeoutSeconds = 5f;
        private const float StoreListPollSeconds = 0.5f;

        private const string ActionMessages = "Messages";
        private const string ActionWebsite = "Website";
        private const string ActionMap = "Map";

        // The panel's heading repeats the screen name.
        private const string PanelTitleObject = "text_Title";
        private const string WebsiteButtonObject = "Website Icon";

        #endregion

        #region Navigator Identity

        public override string NavigatorId => "FavoriteStores";
        public override string ScreenName => Strings.ScreenFavoriteStores;
        public override int Priority => FavoriteStoresPriority;
        protected override bool SupportsCardNavigation => false;

        #endregion

        #region State

        private MonoBehaviour _profileController;   // ProfileContentController
        private MonoBehaviour _panel;               // FavoriteStorePanel
        private object _storeManager;               // FavoriteStoreManager
        private float _detectTimer;

        private string _storeSignature;             // listed store ids, to notice list changes
        private float _storeListPollTimer;

        // A store-messages change in flight (the bell writes to the server asynchronously).
        private string _pendingStoreId;
        private string _pendingStoreName;
        private bool _pendingWasOn;
        private float _pendingElapsed;

        // A refresh in flight (Refresh button).
        private bool _refreshPending;
        private float _refreshElapsed;

        private struct StoreInfo
        {
            public string Id;
            public string Name;
            public string Address;
            public bool IsPremium;
            public bool HasWebsite;
        }

        #endregion

        #region Reflection Cache

        private sealed class FavoriteStoreHandles
        {
            // ProfileContentController
            public FieldInfo Panel;                     // _favoriteStoreScreenView (FavoriteStorePanel)
            public MethodInfo GoToProfileDetails;       // selects the Overview tab

            // FavoriteStoreListDataSource → FavoriteStoreManager
            public Type DataSourceType;
            public FieldInfo DataSourceManager;         // _favoriteStoreManager
            public PropertyInfo Stores;                 // IReadOnlyList<PhysicalStore>
            public PropertyInfo IsRefreshing;           // IsRefreshingStoreList
            public MethodInfo GetNotificationEnabled;   // GetNotificationEnabled(string storeId)

            // PhysicalStore (struct)
            public PropertyInfo StoreId;
            public PropertyInfo StoreName;
            public PropertyInfo StoreAddress;
            public PropertyInfo StoreType;              // PhysicalStoreType: Unknown, Regular, Featured
            public PropertyInfo StoreWebsite;

            // FavoriteStoreListItem (one recycled list cell)
            public Type ListItemType;
            public PropertyInfo ItemStoreData;          // PhysicalStore?
            public FieldInfo ItemNotificationButton;    // _notificationButton (bell)
            public FieldInfo ItemAddressLink;           // _favoriteStoreAddressLink (map link)

            public Type RefreshType;                    // FavoriteStoreRefresh (header Refresh button)
        }

        private const string ProfileNamespace = "Core.Meta.MainNavigation.Profile.";

        private static readonly ReflectionCache<FavoriteStoreHandles> _cache = new ReflectionCache<FavoriteStoreHandles>(
            builder: controllerType =>
            {
                var h = new FavoriteStoreHandles
                {
                    Panel = controllerType.GetField("_favoriteStoreScreenView", PrivateInstance),
                    GoToProfileDetails = controllerType.GetMethod("GoToProfileDetails", PublicInstance, null, Type.EmptyTypes, null),
                };

                h.DataSourceType = FindType(ProfileNamespace + "FavoriteStoreListDataSource");
                h.DataSourceManager = h.DataSourceType?.GetField("_favoriteStoreManager", PrivateInstance);

                var managerType = FindType(ProfileNamespace + "FavoriteStoreManager");
                if (managerType != null)
                {
                    h.Stores = managerType.GetProperty("Stores", PublicInstance);
                    h.IsRefreshing = managerType.GetProperty("IsRefreshingStoreList", PublicInstance);
                    h.GetNotificationEnabled = managerType.GetMethod("GetNotificationEnabled", PublicInstance,
                        null, new[] { typeof(string) }, null);
                }

                var storeType = FindType(ProfileNamespace + "PhysicalStore");
                if (storeType != null)
                {
                    h.StoreId = storeType.GetProperty("ID", PublicInstance);
                    h.StoreName = storeType.GetProperty("Name", PublicInstance);
                    h.StoreAddress = storeType.GetProperty("Address", PublicInstance);
                    h.StoreType = storeType.GetProperty("Type", PublicInstance);
                    h.StoreWebsite = storeType.GetProperty("WebsiteUrl", PublicInstance);
                }

                h.ListItemType = FindType(ProfileNamespace + "FavoriteStoreListItem");
                if (h.ListItemType != null)
                {
                    h.ItemStoreData = h.ListItemType.GetProperty("StoreData", PublicInstance);
                    h.ItemNotificationButton = h.ListItemType.GetField("_notificationButton", PrivateInstance);
                    h.ItemAddressLink = h.ListItemType.GetField("_favoriteStoreAddressLink", PrivateInstance);
                }

                h.RefreshType = FindType(ProfileNamespace + "FavoriteStoreRefresh");
                return h;
            },
            validator: h => h.Panel != null && h.GoToProfileDetails != null && h.DataSourceManager != null
                         && h.Stores != null && h.StoreId != null && h.StoreName != null,
            logTag: "FavoriteStores",
            logSubject: "ProfileContentController (Favorite Stores)");

        private static FavoriteStoreHandles H => _cache.Handles;

        private static Type _profileControllerType;

        #endregion

        #region Constructor

        public FavoriteStoresNavigator(IAnnouncementService announcer) : base(announcer) { }

        #endregion

        #region Screen Detection

        protected override bool DetectScreen()
        {
            var controller = FindProfileController();
            if (controller == null) return false;
            if (!_cache.EnsureInitialized(controller.GetType())) return false;

            var panel = H.Panel.GetValue(controller) as MonoBehaviour;
            if (panel == null || !panel.gameObject.activeInHierarchy) return false;

            _profileController = controller;
            _panel = panel;
            return true;
        }

        private MonoBehaviour FindProfileController()
        {
            if (_profileController != null && _profileController.gameObject.activeInHierarchy)
                return _profileController;
            _profileController = null;

            _detectTimer -= Time.deltaTime;
            if (_detectTimer > 0) return null;
            _detectTimer = DetectIntervalSeconds;

            if (_profileControllerType == null)
                _profileControllerType = FindType("Core.Meta.MainNavigation.Profile.ProfileContentController");
            if (_profileControllerType == null) return null;

            return UnityEngine.Object.FindAnyObjectByType(_profileControllerType) as MonoBehaviour;
        }

        #endregion

        #region Element Discovery

        protected override void DiscoverElements()
        {
            _elements.Clear();
            if (_panel == null) return;

            _storeManager = ResolveStoreManager();
            var stores = ReadStores();
            _storeSignature = BuildStoreSignature(stores);

            var cells = FindStoreCells();
            foreach (var store in stores)
            {
                cells.TryGetValue(store.Id, out var cell);
                AddStoreRow(store, cell);
            }

            var storeList = FindStoreListRoot();
            AddPanelContent(_panel.transform, storeList);

            // BaseNavigator deactivates a navigator with no elements; the panel always has its
            // settings, but keep one stop should the layout change.
            if (_elements.Count == 0)
                AddElement(_panel.gameObject, ScreenName);

            Log.Msg("{NavigatorId}", $"Discovered {stores.Count} stores ({cells.Count} with a live cell), {_elements.Count} elements");
        }

        /// <summary>
        /// The manager is a Pantry singleton; the list's data source holds the reference, and
        /// it has run Awake by the time the panel is showing.
        /// </summary>
        private object ResolveStoreManager()
        {
            if (H.DataSourceType == null) return null;
            try
            {
                var dataSource = _panel.GetComponentInChildren(H.DataSourceType, true);
                return dataSource != null ? H.DataSourceManager.GetValue(dataSource) : null;
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"Could not read the favorite store manager: {ex.Message}");
                return null;
            }
        }

        private Transform FindStoreListRoot()
        {
            if (H.DataSourceType == null) return null;
            var dataSource = _panel.GetComponentInChildren(H.DataSourceType, true);
            return dataSource != null ? dataSource.transform : null;
        }

        private List<StoreInfo> ReadStores()
        {
            var result = new List<StoreInfo>();
            if (_storeManager == null) return result;

            try
            {
                if (!(H.Stores.GetValue(_storeManager) is IEnumerable stores)) return result;
                foreach (var store in stores)
                {
                    if (store == null) continue;
                    string id = H.StoreId.GetValue(store) as string;
                    if (string.IsNullOrEmpty(id)) continue;
                    result.Add(new StoreInfo
                    {
                        Id = id,
                        Name = H.StoreName.GetValue(store) as string ?? id,
                        Address = FormatAddress(H.StoreAddress?.GetValue(store) as string),
                        IsPremium = H.StoreType?.GetValue(store)?.ToString() == "Featured",
                        HasWebsite = !string.IsNullOrEmpty(H.StoreWebsite?.GetValue(store) as string),
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"Reading favorite stores failed: {ex.Message}");
            }
            return result;
        }

        /// <summary>The service sends the address as lines, some of them blank.</summary>
        private static string FormatAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return null;
            var lines = new List<string>();
            foreach (var line in address.Split('\n', '\r'))
            {
                string trimmed = UITextExtractor.CleanText(line).Trim(' ', ',');
                if (trimmed.Length > 0) lines.Add(trimmed);
            }
            return string.Join(", ", lines);
        }

        private static string BuildStoreSignature(List<StoreInfo> stores)
        {
            var ids = new List<string>();
            foreach (var store in stores) ids.Add(store.Id);
            return string.Join("|", ids);
        }

        /// <summary>
        /// The list is a recycling scroll view: cells exist for the rows on screen and are
        /// reassigned as it scrolls, so each one is matched to its store by the data it shows.
        /// </summary>
        private Dictionary<string, MonoBehaviour> FindStoreCells()
        {
            var cells = new Dictionary<string, MonoBehaviour>();
            if (H.ListItemType == null || H.ItemStoreData == null) return cells;

            foreach (var component in _panel.GetComponentsInChildren(H.ListItemType, false))
            {
                var cell = component as MonoBehaviour;
                var data = cell != null ? H.ItemStoreData.GetValue(cell) : null;
                if (data == null) continue;
                string id = H.StoreId.GetValue(data) as string;
                if (!string.IsNullOrEmpty(id) && !cells.ContainsKey(id))
                    cells[id] = cell;
            }
            return cells;
        }

        private bool ReadMessagesEnabled(string storeId)
        {
            if (_storeManager == null || H.GetNotificationEnabled == null) return true;
            try { return H.GetNotificationEnabled.Invoke(_storeManager, new object[] { storeId }) as bool? ?? true; }
            catch { return true; }
        }

        /// <summary>
        /// One stop per store. Its buttons (bell, website, map link) are attached actions on
        /// Left/Right; a store scrolled out of the list has no cell, so it is read-only.
        /// </summary>
        private void AddStoreRow(StoreInfo store, MonoBehaviour cell)
        {
            bool messagesOn = ReadMessagesEnabled(store.Id);
            string label = Strings.FavoriteStoreEntry(store.Name, store.Address, store.IsPremium, messagesOn);

            if (cell == null)
            {
                AddTextBlock(label);
                return;
            }

            var actions = new List<AttachedAction>();

            var bell = (H.ItemNotificationButton?.GetValue(cell) as Component)?.gameObject;
            if (bell != null)
                actions.Add(new AttachedAction { Id = ActionMessages, Label = Strings.FavoriteStoreTurnMessages(!messagesOn), TargetButton = bell });

            var website = store.HasWebsite ? FindChildByName(cell.transform, WebsiteButtonObject) : null;
            if (website != null)
                actions.Add(new AttachedAction { Id = ActionWebsite, Label = Strings.FavoriteStoreWebsite, TargetButton = website });

            // The address link ships disabled in the prefab; offer it only if the game shows it.
            var map = (H.ItemAddressLink?.GetValue(cell) as Component)?.gameObject;
            if (map != null && map.activeInHierarchy)
                actions.Add(new AttachedAction { Id = ActionMap, Label = Strings.FavoriteStoreMap, TargetButton = map });

            AddElement(cell.gameObject, label, default, null, actions, UIElementClassifier.ElementRole.Label);
        }

        /// <summary>
        /// Walks the rest of the panel in layout order. A container with several texts and no
        /// controls (the "Find A Store / Play Events / Get Rewards" tiles: heading plus
        /// description) is read as one block; other texts become one block each.
        /// </summary>
        private void AddPanelContent(Transform node, Transform storeList)
        {
            if (!node.gameObject.activeInHierarchy || node == storeList) return;

            var selectable = node.GetComponent<Selectable>();
            if (selectable != null && !(selectable is Scrollbar))
            {
                AddControl(node.gameObject, selectable);
                return;
            }

            var tmp = node.GetComponent<TMP_Text>();
            if (tmp != null && node.name != PanelTitleObject)
                AddTextBlock(UITextExtractor.CleanText(tmp.text));

            var tileTexts = node.GetComponentInChildren<Selectable>() == null ? GetDirectChildTexts(node) : null;
            if (tileTexts != null && tileTexts.Count >= 2)
            {
                var parts = new List<string>();
                foreach (var text in tileTexts) parts.Add(UITextExtractor.CleanText(text.text));
                AddTextBlock(string.Join(". ", parts));
            }

            foreach (Transform child in node)
            {
                if (tileTexts != null && tileTexts.Count >= 2 && child.GetComponent<TMP_Text>() != null)
                    continue;
                AddPanelContent(child, storeList);
            }
        }

        private static List<TMP_Text> GetDirectChildTexts(Transform node)
        {
            var texts = new List<TMP_Text>();
            foreach (Transform child in node)
            {
                if (!child.gameObject.activeInHierarchy) continue;
                var tmp = child.GetComponent<TMP_Text>();
                if (tmp != null && !string.IsNullOrEmpty(UITextExtractor.CleanText(tmp.text)))
                    texts.Add(tmp);
            }
            return texts;
        }

        private void AddControl(GameObject go, Selectable selectable)
        {
            if (H.RefreshType != null && go.GetComponent(H.RefreshType) != null)
            {
                AddElement(go, BuildLabel(Strings.FavoriteStoresRefresh, Strings.RoleButton, UIElementClassifier.ElementRole.Button),
                    default, null, null, UIElementClassifier.ElementRole.Button);
                return;
            }

            var classification = UIElementClassifier.Classify(go);
            string label = classification.Label;

            // The two store settings explain themselves in a hover tooltip.
            if (selectable is Toggle)
            {
                string tooltip = UITextExtractor.GetTooltipTriggerText(go);
                if (!string.IsNullOrEmpty(tooltip) && tooltip != label)
                    label = $"{label}. {tooltip}";
            }

            AddElement(go, BuildLabel(label, classification.RoleLabel, classification.Role),
                default, null, null, classification.Role);
        }

        #endregion

        #region Activation

        protected override bool OnElementActivated(int index, GameObject element)
        {
            // A store row is information; its buttons are the attached actions.
            if (H.ListItemType != null && element.GetComponent(H.ListItemType) != null)
            {
                _announcer.AnnounceInterrupt(_elements[index].Label);
                return true;
            }

            if (H.RefreshType != null && element.GetComponent(H.RefreshType) != null)
            {
                UIActivator.Activate(element);
                _refreshPending = true;
                _refreshElapsed = 0f;
                _announcer.AnnounceInterrupt(Strings.FavoriteStoresRefreshing);
                return true;
            }

            return false;
        }

        protected override bool HandleAttachedAction(AttachedAction action)
        {
            if (action.Id != ActionMessages || !IsValidIndex) return false;

            var cell = _elements[_currentIndex].GameObject;
            var data = cell != null && H.ItemStoreData != null && H.ListItemType != null
                ? H.ItemStoreData.GetValue(cell.GetComponent(H.ListItemType))
                : null;
            string storeId = data != null ? H.StoreId.GetValue(data) as string : null;
            if (string.IsNullOrEmpty(storeId)) return false;

            _pendingStoreId = storeId;
            _pendingStoreName = H.StoreName.GetValue(data) as string ?? storeId;
            _pendingWasOn = ReadMessagesEnabled(storeId);
            _pendingElapsed = 0f;

            // The bell's own click: FavoriteStoreListItem.ToggleNotifications, a server write.
            UIActivator.Activate(action.TargetButton);
            return true;
        }

        #endregion

        #region Update

        public override void Update()
        {
            base.Update();
            if (!_isActive) return;

            PollPendingMessagesChange();
            PollRefresh();
            PollStoreList();
        }

        private void PollPendingMessagesChange()
        {
            if (_pendingStoreId == null) return;

            _pendingElapsed += Time.deltaTime;
            bool nowOn = ReadMessagesEnabled(_pendingStoreId);
            if (nowOn != _pendingWasOn)
            {
                _announcer.AnnounceInterrupt(Strings.FavoriteStoreMessagesChanged(_pendingStoreName, nowOn));
                _pendingStoreId = null;
                RefreshKeepingFocus();
            }
            else if (_pendingElapsed > ServerChangeTimeoutSeconds)
            {
                _announcer.AnnounceInterrupt(Strings.FavoriteStoreMessagesUnchanged);
                _pendingStoreId = null;
            }
        }

        private void PollRefresh()
        {
            if (!_refreshPending) return;

            _refreshElapsed += Time.deltaTime;
            bool refreshing = _storeManager != null && H.IsRefreshing?.GetValue(_storeManager) as bool? == true;
            // Give the request a frame or two to start before trusting "not refreshing".
            if ((refreshing || _refreshElapsed < 0.2f) && _refreshElapsed < ServerChangeTimeoutSeconds) return;

            _refreshPending = false;
            RefreshKeepingFocus();
            _announcer.Announce(Strings.FavoriteStoresCount(ReadStores().Count), AnnouncementPriority.Normal);
        }

        /// <summary>
        /// Stores can be added or removed from outside (Companion app, store locator) and the
        /// panel refreshes itself when the game regains focus, rebuilding its cells.
        /// </summary>
        private void PollStoreList()
        {
            if (_refreshPending) return;

            _storeListPollTimer -= Time.deltaTime;
            if (_storeListPollTimer > 0) return;
            _storeListPollTimer = StoreListPollSeconds;

            var stores = ReadStores();
            if (BuildStoreSignature(stores) == _storeSignature) return;

            Log.Msg("{NavigatorId}", "Favorite store list changed");
            RefreshKeepingFocus();
            _announcer.Announce(Strings.FavoriteStoresCount(stores.Count), AnnouncementPriority.Normal);
        }

        /// <summary>Rebuilds the element list and stays on the same control where it still exists.</summary>
        private void RefreshKeepingFocus()
        {
            var focused = IsValidIndex ? _elements[_currentIndex].GameObject : null;
            int oldIndex = _currentIndex;

            DiscoverElements();

            _currentIndex = -1;
            for (int i = 0; focused != null && i < _elements.Count; i++)
            {
                if (_elements[i].GameObject == focused) { _currentIndex = i; break; }
            }
            if (_currentIndex < 0)
                _currentIndex = Math.Max(0, Math.Min(oldIndex, _elements.Count - 1));
            _currentActionIndex = 0;
        }

        #endregion

        #region Input

        protected override bool HandleCustomInput()
        {
            if (KeyInput.GetKeyDown(KeyCode.Backspace))
            {
                ReturnToProfile();
                return true;
            }
            return false;
        }

        /// <summary>Selects the Overview tab, the same way the game's own tab strip does.</summary>
        private void ReturnToProfile()
        {
            try
            {
                H.GoToProfileDetails.Invoke(_profileController, null);
                Log.Msg("{NavigatorId}", "Returned to profile overview");
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"GoToProfileDetails failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        #endregion

        #region Announcements

        protected override string GetActivationAnnouncement() => Strings.FavoriteStoresActivation(ReadStores().Count);

        public override string GetTutorialHint() => LocaleManager.Instance.Get("FavoriteStoresHint");

        #endregion

        #region Lifecycle

        protected override void OnActivated()
        {
            // Opening a store website can raise the game's leaving-Arena prompt.
            EnablePopupDetection();
        }

        protected override void OnDeactivating()
        {
            DisablePopupDetection();
            _panel = null;
            _storeManager = null;
            _storeSignature = null;
            _pendingStoreId = null;
            _refreshPending = false;
        }

        protected override bool ValidateElements()
        {
            if (_panel == null || _panel.gameObject == null || !_panel.gameObject.activeInHierarchy)
            {
                Log.Msg("{NavigatorId}", "Favorite Stores panel no longer active");
                return false;
            }
            return base.ValidateElements();
        }

        public override void OnSceneChanged(string sceneName)
        {
            if (_isActive) Deactivate();
            _profileController = null;
            _panel = null;
            _storeManager = null;
        }

        #endregion
    }
}
