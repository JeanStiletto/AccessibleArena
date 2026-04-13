using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.PanelDetection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Standalone navigator for the MTGA Mastery/Rewards screen (RewardTrack scene).
    /// Navigates mastery levels with Up/Down, cycles reward tiers with Left/Right,
    /// and handles page sync automatically.
    /// </summary>
    public class MasteryNavigator : BaseNavigator
    {
        #region Constants

        private const int MasteryPriority = 60;
        private const int LevelsPerPageJump = 10;

        #endregion

        #region Mode

        private enum MasteryMode { Levels, PrizeWall }
        private MasteryMode _mode;

        #endregion

        #region Navigator Identity

        public override string NavigatorId => "Mastery";
        public override string ScreenName => _mode == MasteryMode.PrizeWall ? Strings.ScreenPrizeWall : Strings.ScreenMastery;
        public override int Priority => MasteryPriority;
        protected override bool SupportsCardNavigation => false;
        protected override bool AcceptSpaceKey => false;

        #endregion

        #region Navigation State

        private int _currentLevelIndex;    // Index into _levelData list (0 = status item, 1+ = levels)
        private int _currentTierIndex;     // Which reward tier within level (0=Free, 1=Premium, 2=Renewal)

        // PrizeWall state
        private MonoBehaviour _prizeWallController;
        private GameObject _prizeWallGameObject;
        private List<PrizeWallItemData> _prizeWallItems = new List<PrizeWallItemData>();
        private int _prizeWallIndex;
        private string _sphereCount;
        private GameObject _prizeWallBackButton;

        private struct PrizeWallItemData
        {
            public GameObject Obj;
            public string Label;       // Item name from UITextExtractor
            public string Cost;        // e.g., "2" (sphere count on button)
            public bool IsOwned;       // Button not interactable or item claimed
        }

        #endregion

        #region Cached Controller & Reflection

        private MonoBehaviour _controller;
        private GameObject _controllerGameObject;

        // Reflection: ProgressionTracksContentController
        private Type _controllerType;
        private PropertyInfo _isOpenProp;
        private FieldInfo _activeViewField;
        private FieldInfo _backButtonField;

        // Reflection: RewardTrackView
        private Type _viewType;
        private FieldInfo _levelsField;          // List<ProgressionTrackLevel>
        private FieldInfo _levelRewardDataField;  // List<RewardDisplayData[]>
        private FieldInfo _pagesField;            // List<PageLevels>
        private PropertyInfo _currentPageProp;    // int CurrentPage { get; set; }
        private PropertyInfo _pagesCountProp;     // int PagesCount { get; }
        private FieldInfo _trackNameField;        // string TrackName
        private FieldInfo _trackLabelField;       // MTGALocalizedString TrackLabel
        private FieldInfo _masteryTreeButtonField;
        private FieldInfo _previousTreeButtonField;
        private FieldInfo _purchaseButtonField;
        private FieldInfo _purchaseCenterField;    // GameObject _purchaseCenter

        // Reflection: PageLevels nested class
        private Type _pageLevelsType;
        private FieldInfo _pageLevelStartField;   // int LevelStart
        private PropertyInfo _pageLevelEndProp;    // int LevelEnd { get; }

        // Reflection: ProgressionTrackLevel
        private Type _trackLevelType;
        private FieldInfo _levelIndexField;       // int Index
        private FieldInfo _levelExpField;         // int EXPProgressIfIsCurrent
        private FieldInfo _levelCompleteField;    // bool IsProgressionComplete
        private FieldInfo _levelRepeatableField;  // bool IsRepeatable
        private FieldInfo _serverLevelField;      // ClientTrackLevelInfo ServerLevel

        // Reflection: ClientTrackLevelInfo
        private Type _clientLevelInfoType;
        private FieldInfo _xpToCompleteField;     // int xpToComplete

        // Reflection: RewardDisplayData
        private Type _rewardDisplayType;
        private FieldInfo _rewardMainTextField;    // MTGALocalizedString MainText
        private FieldInfo _rewardQuantityField;    // int Quantity
        private FieldInfo _rewardDescTextField;    // MTGALocalizedString DescriptionText
        private FieldInfo _rewardSecondaryField;   // MTGALocalizedString SecondaryText

        // Reflection: SetMasteryDataProvider (for current level / XP progress)
        private PropertyInfo _masteryPassProviderProp; // private prop on view: _masteryPassProvider
        private Type _setMasteryDataProviderType;
        private MethodInfo _getCurrentLevelIndexMethod;  // GetCurrentLevelIndex(string) -> int
        private MethodInfo _getCurrentXpProgressMethod;  // GetCurrentXpProgress(string) -> int
        private MethodInfo _playerHitPremiumTierMethod;  // PlayerHitPremiumRewardTier(string) -> bool

        // Reflection: MTGALocalizedString
        private Type _mtgaLocStringType;
        private FieldInfo _locStringKeyField;      // string Key
        private FieldInfo _locStringParamsField;   // Dictionary<string,string> Parameters

        // Reflection: Localization
        private Type _languagesType;
        private PropertyInfo _activeLocProviderProp; // static IClientLocProvider ActiveLocProvider
        private MethodInfo _getLocalizedTextMethod;   // IClientLocProvider.GetLocalizedText(string)

        private bool _reflectionInitialized;

        // PrizeWall reflection
        private Type _prizeWallControllerType;
        private PropertyInfo _prizeWallIsOpenProp;
        private FieldInfo _prizeWallCurrencyField;          // PrizeWallCurrency _currencyPrizeWall
        private FieldInfo _prizeWallBackButtonField;         // CustomButton _prizeWallBackButton
        private FieldInfo _prizeWallContentsField;           // GameObject _contentsContainer
        private FieldInfo _prizeWallLayoutGroupField;        // HorizontalLayoutGroup _storeButtonLayoutGroup
        private FieldInfo _prizeWallConfirmModalField;       // StoreConfirmationModal _confirmationModal
        private Type _prizeWallCurrencyType;
        private FieldInfo _currencyQuantityField;            // TMP_Text _currencyQuantity
        private bool _prizeWallReflectionInitialized;
        private GameObject _confirmationModalGameObject;     // Cached modal GO for polling

        // Confirmation modal custom handling (follows StoreNavigator pattern)
        private bool _wasModalOpen;
        private bool _isConfirmationModalActive;
        private MonoBehaviour _confirmationModalMb;
        private List<(GameObject obj, string label)> _modalElements = new List<(GameObject, string)>();
        private int _modalElementIndex;

        // StoreItemBase purchase button reflection (for extracting cost + owned status)
        private Type _storeItemBaseType;
        private FieldInfo _siBlueButtonField;     // BlueButton (PurchaseButton struct)
        private FieldInfo _siOrangeButtonField;   // OrangeButton
        private FieldInfo _siClearButtonField;    // ClearButton
        private FieldInfo _siGreenButtonField;    // GreenButton
        private Type _purchaseButtonType;
        private FieldInfo _pbButtonField;         // Button (CustomButton)
        private FieldInfo _pbContainerField;      // ButtonContainer (GameObject)
        private bool _storeItemReflectionInitialized;

        // StoreConfirmationModal reflection (for custom modal handling)
        private Type _confirmationModalType;
        private static readonly string[] ModalPurchaseButtonFields = new[]
        {
            "_buttonGemPurchase", "_buttonCoinPurchase", "_buttonCashPurchase", "_buttonFreePurchase"
        };
        private FieldInfo[] _modalButtonFields;
        private Type _modalPurchaseButtonType;
        private FieldInfo _modalPbButtonField;     // Button (CustomButton)
        private FieldInfo _modalPbLabelField;      // Label (TMP_Text)
        private MethodInfo _modalCloseMethod;
        private FieldInfo _modalLabelField;        // _label (Localize) for item name
        private FieldInfo _modalItemContainerField;  // _storeItemContainer (Transform) for card preview
        private FieldInfo _modalProductListField;    // _productListContainer (Transform) for card tiles
        private bool _modalReflectionCached;

        #endregion

        #region Discovered Data

        private struct LevelData
        {
            public int LevelNumber;          // Display level (1-based)
            public int ExpProgress;          // Current XP (if current level)
            public int XpToComplete;         // XP needed
            public bool IsComplete;
            public bool IsCurrent;           // Is this the player's current in-progress level
            public bool IsRepeatable;
            public List<TierReward> Tiers;   // Free, Premium, etc.
        }

        private struct TierReward
        {
            public string TierName;          // "Free", "Premium", "Renewal"
            public string RewardName;        // Resolved localized text
            public int Quantity;
            public string Description;       // Secondary/description text
        }

        private struct ActionButton
        {
            public MonoBehaviour Button;     // CustomButton MonoBehaviour
            public GameObject GameObject;
            public string Label;
        }

        private readonly List<LevelData> _levelData = new List<LevelData>();
        private readonly List<ActionButton> _actionButtons = new List<ActionButton>();
        private string _trackTitle;
        private int _totalLevels;
        private int _currentPlayerLevel;   // The player's current level index

        #endregion

        #region Constructor

        public MasteryNavigator(IAnnouncementService announcer) : base(announcer)
        {
        }

        #endregion

        #region Screen Detection

        protected override bool DetectScreen()
        {
            // Check for ProgressionTracksContentController (Levels mode)
            var levelsController = FindLevelsController();
            if (levelsController != null && IsControllerOpen(levelsController))
            {
                _mode = MasteryMode.Levels;
                _controller = levelsController;
                _controllerGameObject = levelsController.gameObject;
                return true;
            }

            // Check for ContentController_PrizeWall (PrizeWall mode)
            var prizeWall = FindPrizeWallController();
            if (prizeWall != null && IsPrizeWallOpen(prizeWall))
            {
                _mode = MasteryMode.PrizeWall;
                _prizeWallController = prizeWall;
                _prizeWallGameObject = prizeWall.gameObject;
                return true;
            }

            return false;
        }

        private MonoBehaviour FindLevelsController()
        {
            // Use cached reference if still valid
            if (_controller != null && _controller.gameObject != null && _controller.gameObject.activeInHierarchy)
                return _controller;

            _controller = null;
            _controllerGameObject = null;

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == "ProgressionTracksContentController")
                    return mb;
            }

            return null;
        }

        private MonoBehaviour FindPrizeWallController()
        {
            // Use cached reference if still valid
            if (_prizeWallController != null && _prizeWallController.gameObject != null &&
                _prizeWallController.gameObject.activeInHierarchy)
                return _prizeWallController;

            _prizeWallController = null;
            _prizeWallGameObject = null;

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == "ContentController_PrizeWall")
                    return mb;
            }

            return null;
        }

        private bool IsControllerOpen(MonoBehaviour controller)
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

            return true;
        }

        private bool IsPrizeWallOpen(MonoBehaviour controller)
        {
            EnsurePrizeWallReflectionCached(controller.GetType());

            if (_prizeWallIsOpenProp != null)
            {
                try
                {
                    return (bool)_prizeWallIsOpenProp.GetValue(controller);
                }
                catch { return false; }
            }

            return true;
        }

        /// <summary>
        /// Check if a panel should be excluded from popup handling.
        /// These are benign game overlays that aren't real popups.
        /// </summary>
        protected override bool IsPopupExcluded(PanelInfo panel)
        {
            if (base.IsPopupExcluded(panel)) return true;
            string name = panel.Name;
            // ObjectivePopup: daily quest overlay
            // FullscreenZFBrowser: embedded browser canvas
            // Rewards: empty Rewards controller active alongside Prize Wall confirmation modal
            return name.Contains("ObjectivePopup") || name.Contains("FullscreenZFBrowser") ||
                   name.Contains("Rewards");
        }

        protected override void OnPopupClosed()
        {
            // Re-announce current position based on mode
            if (_mode == MasteryMode.PrizeWall)
                AnnouncePrizeWallItem();
            else
                AnnounceCurrentLevel();
        }

        #endregion

        #region Reflection Caching

        private void EnsureReflectionCached(Type controllerType)
        {
            if (_reflectionInitialized && _controllerType == controllerType) return;

            _controllerType = controllerType;
            var flags = AllInstanceFlags;

            // Controller properties/fields
            _isOpenProp = controllerType.GetProperty("IsOpen", flags | BindingFlags.FlattenHierarchy);
            _activeViewField = controllerType.GetField("_activeView", flags);
            _backButtonField = controllerType.GetField("_backButton", flags);

            // RewardTrackView type from _activeView field
            if (_activeViewField != null)
            {
                _viewType = _activeViewField.FieldType;
                _levelsField = _viewType.GetField("_levels", flags);
                _levelRewardDataField = _viewType.GetField("_levelRewardData", flags);
                _pagesField = _viewType.GetField("_pages", flags);
                _currentPageProp = _viewType.GetProperty("CurrentPage", PublicInstance);
                _pagesCountProp = _viewType.GetProperty("PagesCount", PublicInstance);
                _trackNameField = _viewType.GetField("TrackName", PublicInstance);
                _trackLabelField = _viewType.GetField("TrackLabel", PublicInstance);
                _masteryTreeButtonField = _viewType.GetField("_masteryTreeButton", flags);
                _previousTreeButtonField = _viewType.GetField("_previousTreeButton", flags);
                _purchaseButtonField = _viewType.GetField("_purchaseButton", flags);
                _purchaseCenterField = _viewType.GetField("_purchaseCenter", flags);

                // SetMasteryDataProvider via view's _masteryPassProvider property
                _masteryPassProviderProp = _viewType.GetProperty("_masteryPassProvider", flags);
                if (_masteryPassProviderProp != null)
                {
                    _setMasteryDataProviderType = _masteryPassProviderProp.PropertyType;
                    var pubInstance = PublicInstance;
                    _getCurrentLevelIndexMethod = _setMasteryDataProviderType.GetMethod("GetCurrentLevelIndex",
                        pubInstance, null, new[] { typeof(string) }, null);
                    _getCurrentXpProgressMethod = _setMasteryDataProviderType.GetMethod("GetCurrentXpProgress",
                        pubInstance, null, new[] { typeof(string) }, null);
                    _playerHitPremiumTierMethod = _setMasteryDataProviderType.GetMethod("PlayerHitPremiumRewardTier",
                        pubInstance, null, new[] { typeof(string) }, null);
                }

                // PageLevels nested class
                _pageLevelsType = _viewType.GetNestedType("PageLevels", BindingFlags.NonPublic);
                if (_pageLevelsType != null)
                {
                    _pageLevelStartField = _pageLevelsType.GetField("LevelStart", PublicInstance);
                    _pageLevelEndProp = _pageLevelsType.GetProperty("LevelEnd", PublicInstance);
                }
            }

            // Find types from assemblies
            _trackLevelType = FindType("Core.MainNavigation.RewardTrack.ProgressionTrackLevel");
            _clientLevelInfoType = FindType("Core.MainNavigation.RewardTrack.ClientTrackLevelInfo");
            _rewardDisplayType = FindType("RewardDisplayData");
            _mtgaLocStringType = FindType("MTGALocalizedString");
            _languagesType = FindType("Wotc.Mtga.Loc.Languages");

            // ProgressionTrackLevel fields
            if (_trackLevelType != null)
            {
                _levelIndexField = _trackLevelType.GetField("Index", PublicInstance);
                _levelExpField = _trackLevelType.GetField("EXPProgressIfIsCurrent", PublicInstance);
                _levelCompleteField = _trackLevelType.GetField("IsProgressionComplete", PublicInstance);
                _levelRepeatableField = _trackLevelType.GetField("IsRepeatable", PublicInstance);
                _serverLevelField = _trackLevelType.GetField("ServerLevel", PublicInstance);
            }

            // ClientTrackLevelInfo fields
            if (_clientLevelInfoType == null && _serverLevelField != null)
            {
                _clientLevelInfoType = _serverLevelField.FieldType;
            }
            if (_clientLevelInfoType != null)
            {
                _xpToCompleteField = _clientLevelInfoType.GetField("xpToComplete", PublicInstance);
            }

            // RewardDisplayData fields
            if (_rewardDisplayType != null)
            {
                _rewardMainTextField = _rewardDisplayType.GetField("MainText", PublicInstance);
                _rewardQuantityField = _rewardDisplayType.GetField("Quantity", PublicInstance);
                _rewardDescTextField = _rewardDisplayType.GetField("DescriptionText", PublicInstance);
                _rewardSecondaryField = _rewardDisplayType.GetField("SecondaryText", PublicInstance);
            }

            // MTGALocalizedString fields
            if (_mtgaLocStringType != null)
            {
                _locStringKeyField = _mtgaLocStringType.GetField("Key", PublicInstance);
                _locStringParamsField = _mtgaLocStringType.GetField("Parameters", PublicInstance);
            }

            // Languages.ActiveLocProvider
            if (_languagesType != null)
            {
                _activeLocProviderProp = _languagesType.GetProperty("ActiveLocProvider",
                    BindingFlags.Public | BindingFlags.Static);
                if (_activeLocProviderProp != null)
                {
                    var locProviderType = _activeLocProviderProp.PropertyType;
                    _getLocalizedTextMethod = locProviderType.GetMethod("GetLocalizedText",
                        new[] { typeof(string) });
                }
            }

            _reflectionInitialized = true;
            MelonLogger.Msg($"[Mastery] Reflection cached. View={_viewType != null}, " +
                $"Level={_trackLevelType != null}, RewardData={_rewardDisplayType != null}, " +
                $"LocString={_mtgaLocStringType != null}, Languages={_languagesType != null}, " +
                $"PageLevels={_pageLevelsType != null}, " +
                $"DataProvider={_setMasteryDataProviderType != null}, " +
                $"GetCurrentLevelIndex={_getCurrentLevelIndexMethod != null}");
        }

        private void EnsurePrizeWallReflectionCached(Type controllerType)
        {
            if (_prizeWallReflectionInitialized && _prizeWallControllerType == controllerType) return;

            _prizeWallControllerType = controllerType;
            var flags = AllInstanceFlags;

            // IsOpen from NavContentController base
            _prizeWallIsOpenProp = controllerType.GetProperty("IsOpen", flags | BindingFlags.FlattenHierarchy);

            // Key fields on ContentController_PrizeWall
            _prizeWallCurrencyField = controllerType.GetField("_currencyPrizeWall", flags);
            _prizeWallBackButtonField = controllerType.GetField("_prizeWallBackButton", flags);
            _prizeWallContentsField = controllerType.GetField("_contentsContainer", flags);
            _prizeWallLayoutGroupField = controllerType.GetField("_storeButtonLayoutGroup", flags);
            _prizeWallConfirmModalField = controllerType.GetField("_confirmationModal", flags);

            // PrizeWallCurrency type -> _currencyQuantity TMP field
            if (_prizeWallCurrencyField != null)
            {
                _prizeWallCurrencyType = _prizeWallCurrencyField.FieldType;
                _currencyQuantityField = _prizeWallCurrencyType?.GetField("_currencyQuantity", flags);
            }

            _prizeWallReflectionInitialized = true;
            MelonLogger.Msg($"[Mastery] PrizeWall reflection cached. Currency={_prizeWallCurrencyField != null}, " +
                $"BackButton={_prizeWallBackButtonField != null}, " +
                $"Contents={_prizeWallContentsField != null}, " +
                $"Layout={_prizeWallLayoutGroupField != null}, " +
                $"CurrencyQty={_currencyQuantityField != null}, " +
                $"ConfirmModal={_prizeWallConfirmModalField != null}");
        }

        private void EnsureStoreItemReflectionCached(Type storeItemType)
        {
            if (_storeItemReflectionInitialized && _storeItemBaseType == storeItemType) return;

            _storeItemBaseType = storeItemType;
            var flags = AllInstanceFlags;

            _siBlueButtonField = storeItemType.GetField("BlueButton", flags);
            _siOrangeButtonField = storeItemType.GetField("OrangeButton", flags);
            _siClearButtonField = storeItemType.GetField("ClearButton", flags);
            _siGreenButtonField = storeItemType.GetField("GreenButton", flags);

            // PurchaseButton struct type from BlueButton field
            if (_siBlueButtonField != null)
            {
                _purchaseButtonType = _siBlueButtonField.FieldType;
                _pbButtonField = _purchaseButtonType.GetField("Button", flags);
                _pbContainerField = _purchaseButtonType.GetField("ButtonContainer", flags);
            }

            _storeItemReflectionInitialized = true;
            MelonLogger.Msg($"[Mastery] StoreItemBase reflection cached. PurchaseButton={_purchaseButtonType != null}, " +
                $"PbButton={_pbButtonField != null}");
        }

        private void EnsureConfirmationModalReflectionCached()
        {
            if (_modalReflectionCached) return;

            _confirmationModalType = FindType("StoreConfirmationModal");
            if (_confirmationModalType != null)
            {
                var flags = AllInstanceFlags;
                _modalButtonFields = new FieldInfo[ModalPurchaseButtonFields.Length];
                for (int i = 0; i < ModalPurchaseButtonFields.Length; i++)
                    _modalButtonFields[i] = _confirmationModalType.GetField(ModalPurchaseButtonFields[i], flags);

                _modalCloseMethod = _confirmationModalType.GetMethod("Close", PublicInstance);
                _modalLabelField = _confirmationModalType.GetField("_label", flags);
                _modalItemContainerField = _confirmationModalType.GetField("_storeItemContainer", flags);
                _modalProductListField = _confirmationModalType.GetField("_productListContainer", flags);

                if (_modalButtonFields[0] != null)
                {
                    _modalPurchaseButtonType = _modalButtonFields[0].FieldType;
                    _modalPbButtonField = _modalPurchaseButtonType.GetField("Button", flags);
                    _modalPbLabelField = _modalPurchaseButtonType.GetField("Label", flags);
                }
            }

            _modalReflectionCached = true;
            MelonLogger.Msg($"[Mastery] ConfirmationModal reflection cached. Type={_confirmationModalType != null}, " +
                $"Close={_modalCloseMethod != null}, PbButton={_modalPbButtonField != null}");
        }

        #endregion

        #region Element Discovery

        protected override void DiscoverElements()
        {
            if (_mode == MasteryMode.PrizeWall)
            {
                DiscoverPrizeWallItems();
                return;
            }

            // Levels mode: Build level data and action buttons from the view
            BuildLevelData();
            BuildActionButtons();

            // Insert virtual status item at position 0 with XP info + action buttons as tiers
            InsertStatusItem();

            if (_levelData.Count > 0)
            {
                // Add a dummy element for BaseNavigator validation
                AddElement(_controllerGameObject, "Mastery");
            }
        }

        private void BuildLevelData()
        {
            _levelData.Clear();
            _currentPlayerLevel = -1;

            var view = GetActiveView();
            if (view == null)
            {
                MelonLogger.Msg("[Mastery] No active view found");
                return;
            }

            // Get track name/title
            _trackTitle = ResolveTrackTitle(view);

            // Get levels list
            var levelsObj = _levelsField?.GetValue(view);
            if (levelsObj == null)
            {
                MelonLogger.Msg("[Mastery] No levels data found");
                return;
            }
            var levelsList = levelsObj as IList;
            if (levelsList == null || levelsList.Count == 0) return;

            // Get reward data list
            var rewardDataObj = _levelRewardDataField?.GetValue(view);
            var rewardDataList = rewardDataObj as IList;

            _totalLevels = levelsList.Count;

            // Get current level from data provider (the level the player is working on)
            // curLevelIndex is the Index field value of the current in-progress level
            int curLevelIndex = -1;
            try
            {
                if (_masteryPassProviderProp != null && _getCurrentLevelIndexMethod != null)
                {
                    var provider = _masteryPassProviderProp.GetValue(view);
                    if (provider != null)
                    {
                        string trackName = _trackNameField?.GetValue(view) as string;
                        if (!string.IsNullOrEmpty(trackName))
                        {
                            curLevelIndex = (int)_getCurrentLevelIndexMethod.Invoke(provider, new object[] { trackName });
                            MelonLogger.Msg($"[Mastery] Data provider: curLevelIndex={curLevelIndex}, trackName={trackName}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[Mastery] Error getting current level from provider: {ex.Message}");
            }

            for (int i = 0; i < levelsList.Count; i++)
            {
                var level = levelsList[i];
                if (level == null) continue;

                var levelData = ExtractLevelData(level, i, rewardDataList, curLevelIndex);
                _levelData.Add(levelData);
            }

            // Find current player level in our list (the in-progress level)
            // curLevelIndex is the Index field of the current level, which equals list position
            if (curLevelIndex >= 0)
            {
                // Find the level data entry matching curLevelIndex
                for (int i = 0; i < _levelData.Count; i++)
                {
                    if (_levelData[i].IsCurrent)
                    {
                        _currentPlayerLevel = i;
                        break;
                    }
                }
            }

            // Fallback: if no current level found, default to last level
            if (_currentPlayerLevel < 0)
                _currentPlayerLevel = _levelData.Count - 1;

            MelonLogger.Msg($"[Mastery] Built {_levelData.Count} levels, current={_currentPlayerLevel}, " +
                $"curLevelIdx={curLevelIndex}, track={_trackTitle}");
        }

        private LevelData ExtractLevelData(object level, int listIndex, IList rewardDataList, int curLevelIndex)
        {
            int levelIndex = 0;  // The Index field (0-based, matches list position)
            int expProgress = 0;
            int xpToComplete = 0;
            bool isRepeatable = false;

            try
            {
                if (_levelIndexField != null)
                    levelIndex = (int)_levelIndexField.GetValue(level);
                if (_levelExpField != null)
                    expProgress = (int)_levelExpField.GetValue(level);
                if (_levelRepeatableField != null)
                    isRepeatable = (bool)_levelRepeatableField.GetValue(level);

                if (_serverLevelField != null && _xpToCompleteField != null)
                {
                    var serverLevel = _serverLevelField.GetValue(level);
                    if (serverLevel != null)
                        xpToComplete = (int)_xpToCompleteField.GetValue(serverLevel);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[Mastery] Error reading level data: {ex.Message}");
            }

            // Determine completion status from data provider's current level index
            // Game logic: levels with (Index + 1) <= curLevelIndex are completed
            // The level with Index == curLevelIndex is the current in-progress level
            bool isComplete = curLevelIndex >= 0 && levelIndex < curLevelIndex;
            bool isCurrent = curLevelIndex >= 0 && levelIndex == curLevelIndex;

            // Extract reward tiers
            var tiers = new List<TierReward>();
            if (rewardDataList != null && listIndex < rewardDataList.Count)
            {
                try
                {
                    var rewardsArray = rewardDataList[listIndex] as Array;
                    if (rewardsArray != null)
                    {
                        string[] tierNames = { Strings.MasteryFree, Strings.MasteryPremium, Strings.MasteryRenewal };
                        for (int t = 0; t < rewardsArray.Length; t++)
                        {
                            var reward = rewardsArray.GetValue(t);
                            if (reward == null) continue;

                            string tierName = t < tierNames.Length ? tierNames[t] : $"Tier {t + 1}";
                            string rewardName = ResolveLocString(_rewardMainTextField?.GetValue(reward));
                            int quantity = 0;
                            if (_rewardQuantityField != null)
                                quantity = (int)_rewardQuantityField.GetValue(reward);
                            string description = ResolveLocString(_rewardSecondaryField?.GetValue(reward));

                            if (string.IsNullOrEmpty(rewardName) || rewardName.StartsWith("$"))
                                rewardName = ResolveLocString(_rewardDescTextField?.GetValue(reward));
                            if (string.IsNullOrEmpty(rewardName) || rewardName.StartsWith("$"))
                                rewardName = Strings.MasteryNoReward;

                            tiers.Add(new TierReward
                            {
                                TierName = tierName,
                                RewardName = rewardName,
                                Quantity = quantity,
                                Description = description
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[Mastery] Error reading reward tiers at index {listIndex}: {ex.Message}");
                }
            }

            return new LevelData
            {
                LevelNumber = listIndex + 1,  // 1-based display number
                ExpProgress = expProgress,
                XpToComplete = xpToComplete,
                IsComplete = isComplete,
                IsCurrent = isCurrent,
                IsRepeatable = isRepeatable,
                Tiers = tiers
            };
        }

        private void BuildActionButtons()
        {
            _actionButtons.Clear();

            var view = GetActiveView();
            if (view == null) return;

            // Mastery Tree / Spend Orbs button
            TryAddButton(view, _masteryTreeButtonField, "Mastery Tree");

            // Previous Season button (only if visible)
            TryAddButton(view, _previousTreeButtonField, "Previous Season");

            // Purchase button (only if purchase center is visible)
            if (_purchaseCenterField != null && _purchaseButtonField != null)
            {
                try
                {
                    var purchaseCenter = _purchaseCenterField.GetValue(view) as GameObject;
                    if (purchaseCenter != null && purchaseCenter.activeInHierarchy)
                    {
                        TryAddButton(view, _purchaseButtonField, "Purchase");
                    }
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            // Back button (from controller, not view)
            if (_backButtonField != null && _controller != null)
            {
                try
                {
                    var backBtn = _backButtonField.GetValue(_controller) as MonoBehaviour;
                    if (backBtn != null && backBtn.gameObject != null && backBtn.gameObject.activeInHierarchy)
                    {
                        _actionButtons.Add(new ActionButton
                        {
                            Button = backBtn,
                            GameObject = backBtn.gameObject,
                            Label = Strings.Back
                        });
                    }
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            MelonLogger.Msg($"[Mastery] Found {_actionButtons.Count} action buttons");
        }

        private void TryAddButton(MonoBehaviour view, FieldInfo field, string label)
        {
            if (field == null) return;

            try
            {
                var btn = field.GetValue(view) as MonoBehaviour;
                if (btn == null || btn.gameObject == null) return;

                // Check if the button's parent is active (some buttons are hidden via parent)
                var parent = btn.transform.parent;
                if (parent != null && !parent.gameObject.activeInHierarchy) return;
                if (!btn.gameObject.activeInHierarchy) return;

                // Try to get label text from sibling Localize component
                string resolvedLabel = label;
                var localize = btn.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (localize != null && !string.IsNullOrEmpty(localize.text))
                {
                    string cleaned = System.Text.RegularExpressions.Regex.Replace(
                        localize.text, @"<[^>]+>", "").Trim();
                    if (!string.IsNullOrEmpty(cleaned))
                        resolvedLabel = cleaned;
                }

                _actionButtons.Add(new ActionButton
                {
                    Button = btn,
                    GameObject = btn.gameObject,
                    Label = resolvedLabel
                });
            }
            catch { /* Reflection may fail on different game versions */ }
        }

        /// <summary>
        /// Inserts a virtual "Status" item at position 0 containing XP info and action buttons as tiers.
        /// This allows the user to re-read XP status and access buttons via Left/Right cycling.
        /// </summary>
        private void InsertStatusItem()
        {
            // Build XP info from the current player level
            string xpInfo = "";
            int displayLevel = 1;
            if (_currentPlayerLevel >= 0 && _currentPlayerLevel < _levelData.Count)
            {
                var curLevel = _levelData[_currentPlayerLevel];
                displayLevel = curLevel.LevelNumber;
                if (curLevel.XpToComplete > 0 && !curLevel.IsComplete)
                    xpInfo = $"{curLevel.ExpProgress}/{curLevel.XpToComplete} XP";
                else if (curLevel.IsComplete)
                    xpInfo = Strings.MasteryCompleted;
            }

            // Build tiers: XP status first, then action buttons
            var tiers = new List<TierReward>();

            // Tier 0: XP status
            tiers.Add(new TierReward
            {
                TierName = Strings.MasteryStatus,
                RewardName = Strings.MasteryStatusInfo(displayLevel, _totalLevels, xpInfo),
                Quantity = 0,
                Description = ""
            });

            // Tier 1+: action buttons
            foreach (var btn in _actionButtons)
            {
                tiers.Add(new TierReward
                {
                    TierName = btn.Label,
                    RewardName = btn.Label,
                    Quantity = 0,
                    Description = ""
                });
            }

            _levelData.Insert(0, new LevelData
            {
                LevelNumber = 0, // marker for virtual status item
                Tiers = tiers
            });

            // Shift current player level index to account for inserted item
            if (_currentPlayerLevel >= 0)
                _currentPlayerLevel++;

            MelonLogger.Msg($"[Mastery] Inserted status item with {tiers.Count} tiers ({_actionButtons.Count} buttons)");
        }

        private void DiscoverPrizeWallItems()
        {
            _prizeWallItems.Clear();
            _prizeWallIndex = 0;
            _sphereCount = "0";
            _prizeWallBackButton = null;

            if (_prizeWallController == null) return;

            EnsurePrizeWallReflectionCached(_prizeWallController.GetType());

            // Get sphere count from PrizeWallCurrency._currencyQuantity
            if (_prizeWallCurrencyField != null && _currencyQuantityField != null)
            {
                try
                {
                    var currency = _prizeWallCurrencyField.GetValue(_prizeWallController);
                    if (currency != null)
                    {
                        var tmpText = _currencyQuantityField.GetValue(currency) as TMPro.TMP_Text;
                        if (tmpText != null && !string.IsNullOrEmpty(tmpText.text))
                            _sphereCount = tmpText.text.Trim();
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[Mastery] Error reading sphere count: {ex.Message}");
                }
            }

            // Get back button
            if (_prizeWallBackButtonField != null)
            {
                try
                {
                    var backBtn = _prizeWallBackButtonField.GetValue(_prizeWallController) as MonoBehaviour;
                    if (backBtn != null && backBtn.gameObject != null && backBtn.gameObject.activeInHierarchy)
                        _prizeWallBackButton = backBtn.gameObject;
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            // Find StoreItemBase components under the layout group (the purchasable items)
            Transform layoutParent = null;
            if (_prizeWallLayoutGroupField != null)
            {
                try
                {
                    var layoutGroup = _prizeWallLayoutGroupField.GetValue(_prizeWallController) as Component;
                    if (layoutGroup != null)
                        layoutParent = layoutGroup.transform;
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            // Fallback: search under contents container
            if (layoutParent == null && _prizeWallContentsField != null)
            {
                try
                {
                    var contents = _prizeWallContentsField.GetValue(_prizeWallController) as GameObject;
                    if (contents != null)
                        layoutParent = contents.transform;
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            if (layoutParent != null)
            {
                // Find all active StoreItemBase children - these are the purchasable items
                var discovered = new List<(PrizeWallItemData data, float sortOrder)>();

                foreach (var mb in layoutParent.GetComponentsInChildren<MonoBehaviour>(false))
                {
                    if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                    if (mb.GetType().Name != "StoreItemBase") continue;

                    EnsureStoreItemReflectionCached(mb.GetType());
                    var itemData = ExtractPrizeWallItemData(mb);

                    var pos = mb.transform.position;
                    // Sort by Y descending (top first), then X ascending (left first)
                    discovered.Add((itemData, -pos.y * 1000 + pos.x));
                }

                foreach (var (data, _) in discovered.OrderBy(x => x.sortOrder))
                {
                    _prizeWallItems.Add(data);
                }
            }

            // Insert virtual sphere status item at position 0
            _prizeWallItems.Insert(0, new PrizeWallItemData
            {
                Obj = null,
                Label = Strings.PrizeWallSphereStatus(_sphereCount)
            });

            // Cache confirmation modal GameObject for polling
            _confirmationModalGameObject = null;
            _wasModalOpen = false;
            if (_prizeWallConfirmModalField != null)
            {
                try
                {
                    var modal = _prizeWallConfirmModalField.GetValue(_prizeWallController) as MonoBehaviour;
                    if (modal != null)
                    {
                        _confirmationModalGameObject = modal.gameObject;
                        _wasModalOpen = modal.gameObject.activeInHierarchy;
                    }
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            if (_prizeWallItems.Count > 0 || _prizeWallGameObject != null)
            {
                // Add dummy element for BaseNavigator validation
                AddElement(_prizeWallGameObject ?? _prizeWallController.gameObject, "PrizeWall");
            }

            MelonLogger.Msg($"[Mastery] PrizeWall: {_prizeWallItems.Count} items (incl. status), spheres={_sphereCount}, " +
                $"backButton={_prizeWallBackButton != null}, modal={_confirmationModalGameObject != null}");
        }

        /// <summary>
        /// Extract rich item data from a StoreItemBase: label, cost, and owned status.
        /// Uses UITextExtractor.TryGetStoreItemLabel() for the name, then reads purchase
        /// buttons via reflection (same pattern as StoreNavigator).
        /// </summary>
        private PrizeWallItemData ExtractPrizeWallItemData(MonoBehaviour storeItemBase)
        {
            var data = new PrizeWallItemData { Obj = storeItemBase.gameObject };

            // 1. Get item name via centralized UITextExtractor
            string label = UITextExtractor.TryGetStoreItemLabel(storeItemBase.gameObject);
            if (string.IsNullOrEmpty(label))
            {
                label = UITextExtractor.GetText(storeItemBase.gameObject);
                if (string.IsNullOrEmpty(label))
                    label = storeItemBase.gameObject.name;
                label = UITextExtractor.StripRichText(label).Trim();
            }
            data.Label = label;

            // 2. Extract cost and owned status from purchase buttons
            data.Cost = null;
            data.IsOwned = true; // Assume owned unless we find an active purchase button

            FieldInfo[] buttonFields = { _siGreenButtonField, _siBlueButtonField, _siOrangeButtonField, _siClearButtonField };
            foreach (var bf in buttonFields)
            {
                if (bf == null || _purchaseButtonType == null) continue;

                try
                {
                    var buttonStruct = bf.GetValue(storeItemBase);
                    if (buttonStruct == null) continue;

                    // Check container visibility
                    if (_pbContainerField != null)
                    {
                        var container = _pbContainerField.GetValue(buttonStruct) as GameObject;
                        if (container != null && !container.activeInHierarchy)
                            continue;
                    }

                    // Get the CustomButton
                    var customButton = _pbButtonField?.GetValue(buttonStruct) as MonoBehaviour;
                    if (customButton == null || customButton.gameObject == null ||
                        !customButton.gameObject.activeInHierarchy)
                        continue;

                    // Found an active purchase button — item is NOT owned
                    data.IsOwned = false;

                    // Extract price text
                    if (data.Cost == null)
                    {
                        var tmpText = customButton.GetComponentInChildren<TMPro.TMP_Text>(true);
                        if (tmpText != null)
                        {
                            string price = UITextExtractor.StripRichText(tmpText.text)?.Trim();
                            if (!string.IsNullOrEmpty(price))
                                data.Cost = price;
                        }
                    }
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            // 3. Fallback: if no purchase button fields found, check for any CustomButton
            if (!_storeItemReflectionInitialized || _purchaseButtonType == null)
            {
                foreach (var mb in storeItemBase.GetComponentsInChildren<MonoBehaviour>(false))
                {
                    if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                    if (mb.GetType().Name != "CustomButton") continue;

                    // Found an active button — not owned
                    data.IsOwned = false;

                    if (data.Cost == null)
                    {
                        var tmpText = mb.GetComponentInChildren<TMPro.TMP_Text>(true);
                        if (tmpText != null)
                        {
                            string price = UITextExtractor.StripRichText(tmpText.text)?.Trim();
                            if (!string.IsNullOrEmpty(price))
                                data.Cost = price;
                        }
                    }
                    break;
                }
            }

            return data;
        }

        #endregion

        #region Localization

        private string ResolveLocString(object mtgaLocString)
        {
            if (mtgaLocString == null) return null;

            try
            {
                // Check Key first - skip empty strings
                if (_locStringKeyField != null)
                {
                    string key = _locStringKeyField.GetValue(mtgaLocString) as string;
                    if (string.IsNullOrEmpty(key) || key == "MainNav/General/Empty_String")
                        return null;
                }

                // MTGALocalizedString.ToString() resolves loc key + parameters automatically
                string resolved = mtgaLocString.ToString();

                // Clean rich text tags
                if (!string.IsNullOrEmpty(resolved))
                {
                    resolved = UITextExtractor.StripRichText(resolved).Trim();
                }

                return (!string.IsNullOrEmpty(resolved) && !resolved.StartsWith("$")) ? resolved : null;
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[Mastery] Error resolving loc string: {ex.Message}");
                return null;
            }
        }

        private string GetLocalizedText(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_activeLocProviderProp == null || _getLocalizedTextMethod == null) return key;

            try
            {
                var locProvider = _activeLocProviderProp.GetValue(null);
                if (locProvider == null) return key;
                return _getLocalizedTextMethod.Invoke(locProvider, new object[] { key }) as string;
            }
            catch
            {
                return key;
            }
        }

        private string ResolveTrackTitle(MonoBehaviour view)
        {
            // Try TrackLabel (MTGALocalizedString) first - has ToString() that resolves automatically
            if (_trackLabelField != null)
            {
                try
                {
                    var label = _trackLabelField.GetValue(view);
                    if (label != null)
                    {
                        string resolved = label.ToString();
                        if (!string.IsNullOrEmpty(resolved) && !resolved.StartsWith("$"))
                        {
                            resolved = UITextExtractor.StripRichText(resolved).Trim();
                            if (!string.IsNullOrEmpty(resolved)) return resolved;
                        }
                    }
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            // Fall back to TrackName (raw string) and localize it
            if (_trackNameField != null)
            {
                try
                {
                    var trackName = _trackNameField.GetValue(view) as string;
                    if (!string.IsNullOrEmpty(trackName))
                    {
                        // Try to resolve "MainNav/BattlePass/{trackName}" like the game does
                        var setName = GetLocalizedText("MainNav/BattlePass/" + trackName);
                        if (!string.IsNullOrEmpty(setName) && !setName.StartsWith("$"))
                        {
                            var masteryTitle = GetLocalizedText("MainNav/BattlePass/SetXMastery");
                            if (!string.IsNullOrEmpty(masteryTitle) && masteryTitle.Contains("{setName}"))
                                return masteryTitle.Replace("{setName}", setName);
                            return setName + " Mastery";
                        }
                        return trackName;
                    }
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            return "Mastery";
        }

        #endregion

        #region Activation & Deactivation

        protected override void OnActivated()
        {
            _currentTierIndex = 0;

            if (_mode == MasteryMode.PrizeWall)
            {
                _prizeWallIndex = 0;
            }
            else
            {
                // Start at current player level (skips status item at 0)
                _currentLevelIndex = _currentPlayerLevel >= 0 ? _currentPlayerLevel : 0;
            }

            EnablePopupDetection();
        }

        protected override void OnDeactivating()
        {
            DisablePopupDetection();
            _levelData.Clear();
            _actionButtons.Clear();
            _prizeWallItems.Clear();
            _confirmationModalGameObject = null;
            _wasModalOpen = false;
            _isConfirmationModalActive = false;
            _modalElements.Clear();
            _confirmationModalMb = null;
        }

        public override void OnSceneChanged(string sceneName)
        {
            _controller = null;
            _controllerGameObject = null;
            _reflectionInitialized = false;
            _prizeWallController = null;
            _prizeWallGameObject = null;
            _prizeWallReflectionInitialized = false;

            base.OnSceneChanged(sceneName);
        }

        #endregion

        #region Announcements

        public override string GetTutorialHint() => LocaleManager.Instance.Get("MasteryHint");

        protected override string GetActivationAnnouncement()
        {
            if (_mode == MasteryMode.PrizeWall)
            {
                string prizeCore = Strings.PrizeWallActivation(_prizeWallItems.Count, _sphereCount);
                return Strings.WithHint(prizeCore, "MasteryHint");
            }

            if (_levelData.Count == 0)
                return $"{_trackTitle}. No levels found.";

            // Build XP string for current level
            string xpStr = "";
            if (_currentPlayerLevel >= 0 && _currentPlayerLevel < _levelData.Count)
            {
                var level = _levelData[_currentPlayerLevel];
                if (level.XpToComplete > 0 && !level.IsComplete)
                    xpStr = $"{level.ExpProgress}/{level.XpToComplete} XP";
                else if (level.IsComplete)
                    xpStr = "completed";
            }

            string core = Strings.MasteryActivation(_trackTitle, _levelData[_currentPlayerLevel].LevelNumber, _totalLevels, xpStr);
            return Strings.WithHint(core, "MasteryHint");
        }

        protected override string GetElementAnnouncement(int index)
        {
            // Not used - we handle our own announcements
            return "";
        }

        private void AnnounceCurrentLevel()
        {
            if (_currentLevelIndex < 0 || _currentLevelIndex >= _levelData.Count) return;

            var level = _levelData[_currentLevelIndex];

            // Virtual status item at index 0
            if (level.LevelNumber == 0)
            {
                string statusText = level.Tiers != null && level.Tiers.Count > 0
                    ? level.Tiers[0].RewardName : Strings.MasteryStatus;
                _announcer.AnnounceInterrupt(statusText);
                return;
            }

            string reward = GetPrimaryRewardName(level);
            string status = GetLevelStatus(level);

            // _currentLevelIndex starts at 1 for real levels (0 is status item)
            string pos = Strings.PositionOf(_currentLevelIndex, _totalLevels);
            _announcer.AnnounceInterrupt(
                (pos != "" ? $"{pos}: " : "") +
                Strings.MasteryLevel(level.LevelNumber, reward, status));
        }

        private void AnnounceCurrentTier()
        {
            if (_currentLevelIndex < 0 || _currentLevelIndex >= _levelData.Count) return;

            var level = _levelData[_currentLevelIndex];
            if (level.Tiers == null || level.Tiers.Count == 0) return;

            if (_currentTierIndex < 0 || _currentTierIndex >= level.Tiers.Count) return;

            var tier = level.Tiers[_currentTierIndex];
            string announcement = Strings.MasteryTier(tier.TierName, tier.RewardName, tier.Quantity);
            if (level.Tiers.Count > 1)
            {
                string tierPos = Strings.PositionOf(_currentTierIndex + 1, level.Tiers.Count);
                if (tierPos != "") announcement += $", {tierPos}";
            }

            _announcer.AnnounceInterrupt(announcement);
        }

        private void AnnounceLevelDetail()
        {
            if (_currentLevelIndex < 0 || _currentLevelIndex >= _levelData.Count) return;

            var level = _levelData[_currentLevelIndex];
            var parts = new List<string>();

            // All tiers
            if (level.Tiers != null)
            {
                foreach (var tier in level.Tiers)
                {
                    parts.Add(Strings.MasteryTier(tier.TierName, tier.RewardName, tier.Quantity));
                }
            }

            string tiers = parts.Count > 0 ? string.Join(". ", parts) : Strings.MasteryNoReward;
            string status = GetLevelStatus(level);

            // Add XP info if current level
            if (!level.IsComplete && level.XpToComplete > 0)
            {
                string xpInfo = $"{level.ExpProgress}/{level.XpToComplete} XP";
                if (!string.IsNullOrEmpty(status))
                    status += $", {xpInfo}";
                else
                    status = xpInfo;
            }

            _announcer.AnnounceInterrupt(Strings.MasteryLevelDetail(level.LevelNumber, tiers, status));
        }

        private string GetPrimaryRewardName(LevelData level)
        {
            if (level.Tiers == null || level.Tiers.Count == 0) return Strings.MasteryNoReward;

            // Show Free tier reward by default
            var tier = level.Tiers[0];
            string name = tier.RewardName;
            if (tier.Quantity > 1)
                name = $"{tier.Quantity}x {name}";
            return name;
        }

        private string GetLevelStatus(LevelData level)
        {
            if (level.IsComplete) return Strings.MasteryCompleted;
            if (level.IsCurrent) return Strings.MasteryCurrentLevel;
            return "";
        }

        #endregion

        #region Page Sync

        private MonoBehaviour GetActiveView()
        {
            if (_controller == null || _activeViewField == null) return null;

            try
            {
                return _activeViewField.GetValue(_controller) as MonoBehaviour;
            }
            catch
            {
                return null;
            }
        }

        private int GetCurrentPage()
        {
            var view = GetActiveView();
            if (view == null || _currentPageProp == null) return 0;

            try
            {
                return (int)_currentPageProp.GetValue(view);
            }
            catch { return 0; }
        }

        private int GetPagesCount()
        {
            var view = GetActiveView();
            if (view == null || _pagesCountProp == null) return 1;

            try
            {
                return (int)_pagesCountProp.GetValue(view);
            }
            catch { return 1; }
        }

        /// <summary>
        /// Sync the visual page to show the level at _currentLevelIndex.
        /// </summary>
        private void SyncPageForLevel()
        {
            // Skip for virtual status item (no game page to sync)
            if (_currentLevelIndex <= 0) return;

            var view = GetActiveView();
            if (view == null || _pagesField == null || _currentPageProp == null) return;

            try
            {
                var pages = _pagesField.GetValue(view) as IList;
                if (pages == null || pages.Count == 0) return;

                int currentPage = (int)_currentPageProp.GetValue(view);
                int targetLevel = _currentLevelIndex < _levelData.Count
                    ? _levelData[_currentLevelIndex].LevelNumber
                    : _currentLevelIndex + 1;

                // Find which page contains this level
                for (int i = 0; i < pages.Count; i++)
                {
                    var page = pages[i];
                    if (page == null) continue;

                    int start = 0, end = 0;
                    if (_pageLevelStartField != null)
                        start = (int)_pageLevelStartField.GetValue(page);
                    if (_pageLevelEndProp != null)
                        end = (int)_pageLevelEndProp.GetValue(page);

                    if (targetLevel >= start && targetLevel <= end)
                    {
                        if (i != currentPage)
                        {
                            _currentPageProp.SetValue(view, i);
                            _announcer.Announce(Strings.MasteryPage(i + 1, pages.Count));
                        }
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[Mastery] Error syncing page: {ex.Message}");
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

            if (_mode == MasteryMode.PrizeWall)
            {
                // Verify PrizeWall controller is still valid
                if (_prizeWallController == null || _prizeWallGameObject == null ||
                    !_prizeWallGameObject.activeInHierarchy)
                {
                    Deactivate();
                    return;
                }

                if (!IsPrizeWallOpen(_prizeWallController))
                {
                    Deactivate();
                    return;
                }
            }
            else
            {
                // Verify levels controller is still valid
                if (_controller == null || _controllerGameObject == null || !_controllerGameObject.activeInHierarchy)
                {
                    Deactivate();
                    return;
                }

                if (!IsControllerOpen(_controller))
                {
                    Deactivate();
                    return;
                }
            }

            // Check confirmation modal state transitions (PrizeWall mode).
            // Modal is reused (not re-instantiated), so PanelStateManager doesn't fire events
            // after the first time. We poll activeInHierarchy and handle with custom elements
            // (same pattern as StoreNavigator — not base popup mode).
            if (_mode == MasteryMode.PrizeWall && _confirmationModalGameObject != null)
            {
                bool modalOpen = _confirmationModalGameObject.activeInHierarchy;
                if (modalOpen && !_wasModalOpen)
                {
                    _wasModalOpen = true;
                    MelonLogger.Msg("[Mastery] Confirmation modal opened");
                    _isConfirmationModalActive = true;
                    _confirmationModalMb = GetConfirmationModalMb();
                    DiscoverConfirmationModalElements();
                    AnnounceConfirmationModal();
                    return;
                }
                else if (!modalOpen && _wasModalOpen)
                {
                    _wasModalOpen = false;
                    if (_isConfirmationModalActive)
                    {
                        MelonLogger.Msg("[Mastery] Confirmation modal closed");
                        _isConfirmationModalActive = false;
                        _modalElements.Clear();
                        _confirmationModalMb = null;
                        AnnouncePrizeWallItem();
                    }
                }
            }

            // Base popup mode for other popups (not confirmation modal)
            if (IsInPopupMode)
            {
                base.Update();
                return;
            }

            HandleMasteryInput();
        }

        protected override bool ValidateElements()
        {
            if (_mode == MasteryMode.PrizeWall)
                return _prizeWallController != null && _prizeWallGameObject != null && _prizeWallGameObject.activeInHierarchy;

            return _controller != null && _controllerGameObject != null && _controllerGameObject.activeInHierarchy;
        }

        #endregion

        #region Input Handling

        private void HandleMasteryInput()
        {
            if (_mode == MasteryMode.PrizeWall)
            {
                HandlePrizeWallInput();
                return;
            }

            HandleLevelInput();
        }

        private void HandleLevelInput()
        {
            // Up/Down: Navigate levels (index 0 = status item, 1+ = real levels)
            if (Input.GetKeyDown(KeyCode.UpArrow) ||
                Input.GetKeyDown(KeyCode.Tab) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            {
                if (_currentLevelIndex > 0)
                {
                    _currentLevelIndex--;
                    _currentTierIndex = 0;
                    SyncPageForLevel();
                    AnnounceCurrentLevel();
                }
                else
                {
                    _announcer.AnnounceInterruptVerbose(Strings.BeginningOfList);
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) ||
                (Input.GetKeyDown(KeyCode.Tab) && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)))
            {
                if (_currentLevelIndex < _levelData.Count - 1)
                {
                    _currentLevelIndex++;
                    _currentTierIndex = 0;
                    SyncPageForLevel();
                    AnnounceCurrentLevel();
                }
                else
                {
                    _announcer.AnnounceInterruptVerbose(Strings.EndOfList);
                }
                return;
            }

            // Left/Right: Cycle reward tiers (or buttons on status item)
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                CycleTier(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                CycleTier(1);
                return;
            }

            // Home/End: Jump to first/last
            if (Input.GetKeyDown(KeyCode.Home))
            {
                _currentLevelIndex = 0;
                _currentTierIndex = 0;
                AnnounceCurrentLevel();
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                _currentLevelIndex = _levelData.Count - 1;
                _currentTierIndex = 0;
                SyncPageForLevel();
                AnnounceCurrentLevel();
                return;
            }

            // PageUp/PageDown: Jump ~10 levels
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                _currentLevelIndex = Math.Max(0, _currentLevelIndex - LevelsPerPageJump);
                _currentTierIndex = 0;
                SyncPageForLevel();
                AnnounceCurrentLevel();
                return;
            }

            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                _currentLevelIndex = Math.Min(_levelData.Count - 1, _currentLevelIndex + LevelsPerPageJump);
                _currentTierIndex = 0;
                SyncPageForLevel();
                AnnounceCurrentLevel();
                return;
            }

            // Enter: On status item, activate button tier or announce detail. On levels, announce detail.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);

                // Status item: activate button if a button tier is selected
                if (_currentLevelIndex == 0 && _currentTierIndex > 0 &&
                    _currentTierIndex - 1 < _actionButtons.Count)
                {
                    var btn = _actionButtons[_currentTierIndex - 1];
                    _announcer.AnnounceInterrupt(Strings.Activating(btn.Label));
                    UIActivator.Activate(btn.GameObject);
                    return;
                }

                AnnounceLevelDetail();
                return;
            }

            // Backspace: Leave mastery screen and return home
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                NavigateToHome();
                return;
            }
        }

        private void CycleTier(int direction)
        {
            if (_currentLevelIndex < 0 || _currentLevelIndex >= _levelData.Count) return;

            var level = _levelData[_currentLevelIndex];
            if (level.Tiers == null || level.Tiers.Count <= 1)
            {
                // Only one tier or none - announce current
                if (level.Tiers != null && level.Tiers.Count == 1)
                    AnnounceCurrentTier();
                return;
            }

            _currentTierIndex += direction;
            if (_currentTierIndex < 0) _currentTierIndex = level.Tiers.Count - 1;
            if (_currentTierIndex >= level.Tiers.Count) _currentTierIndex = 0;

            AnnounceCurrentTier();
        }

        private void ActivateBackButton()
        {
            // Find Back button from action buttons
            foreach (var btn in _actionButtons)
            {
                if (btn.Label == Strings.Back)
                {
                    _announcer.AnnounceInterruptVerbose(Strings.NavigatingBack);
                    UIActivator.Activate(btn.GameObject);
                    return;
                }
            }

            // Fallback: try the _backButton field directly
            if (_backButtonField != null && _controller != null)
            {
                try
                {
                    var backBtn = _backButtonField.GetValue(_controller) as MonoBehaviour;
                    if (backBtn != null && backBtn.gameObject != null)
                    {
                        _announcer.AnnounceInterruptVerbose(Strings.NavigatingBack);
                        UIActivator.Activate(backBtn.gameObject);
                        return;
                    }
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            // No in-screen back button found — navigate home as final fallback
            if (!NavigateToHome())
                _announcer.AnnounceInterrupt("No back button found");
        }

        private void HandlePrizeWallInput()
        {
            // Confirmation modal takes priority (custom handler, not base popup mode)
            if (_isConfirmationModalActive)
            {
                HandleConfirmationModalInput();
                return;
            }

            if (_prizeWallItems.Count == 0) return;

            // Up/Shift+Tab: Previous item
            if (Input.GetKeyDown(KeyCode.UpArrow) ||
                (Input.GetKeyDown(KeyCode.Tab) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))))
            {
                if (_prizeWallIndex > 0)
                {
                    _prizeWallIndex--;
                    AnnouncePrizeWallItem();
                }
                else
                {
                    _announcer.AnnounceInterruptVerbose(Strings.BeginningOfList);
                }
                return;
            }

            // Down/Tab: Next item
            if (Input.GetKeyDown(KeyCode.DownArrow) ||
                (Input.GetKeyDown(KeyCode.Tab) && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)))
            {
                if (_prizeWallIndex < _prizeWallItems.Count - 1)
                {
                    _prizeWallIndex++;
                    AnnouncePrizeWallItem();
                }
                else
                {
                    _announcer.AnnounceInterruptVerbose(Strings.EndOfList);
                }
                return;
            }

            // Home: Jump to first
            if (Input.GetKeyDown(KeyCode.Home))
            {
                _prizeWallIndex = 0;
                AnnouncePrizeWallItem();
                return;
            }

            // End: Jump to last
            if (Input.GetKeyDown(KeyCode.End))
            {
                _prizeWallIndex = _prizeWallItems.Count - 1;
                AnnouncePrizeWallItem();
                return;
            }

            // Enter: Activate selected item (find its purchase button)
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
                ActivatePrizeWallItem();
                return;
            }

            // Backspace: Go back (returns to mastery levels, or home if no back button)
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                if (_prizeWallBackButton != null)
                {
                    _announcer.AnnounceInterruptVerbose(Strings.NavigatingBack);
                    UIActivator.Activate(_prizeWallBackButton);
                }
                else
                {
                    NavigateToHome();
                }
                return;
            }

            // F3/Ctrl+R: Re-announce current position
            if (Input.GetKeyDown(KeyCode.F3) ||
                ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.R)))
            {
                AnnouncePrizeWallItem();
                return;
            }
        }

        private void AnnouncePrizeWallItem()
        {
            if (_prizeWallIndex < 0 || _prizeWallIndex >= _prizeWallItems.Count) return;

            var item = _prizeWallItems[_prizeWallIndex];

            // Build rich description: "Label, 2 spheres, owned"
            string description = item.Label;
            if (!string.IsNullOrEmpty(item.Cost))
                description += $", {item.Cost} {Strings.PrizeWallSphereCost}";
            if (item.IsOwned && item.Obj != null) // Obj null = virtual status item
                description += $", {Strings.ProfileItemOwned}";

            _announcer.AnnounceInterrupt(
                Strings.PrizeWallItem(_prizeWallIndex + 1, _prizeWallItems.Count, description));
        }

        private void ActivatePrizeWallItem()
        {
            if (_prizeWallIndex < 0 || _prizeWallIndex >= _prizeWallItems.Count) return;

            var item = _prizeWallItems[_prizeWallIndex];

            // Virtual status item (Obj=null) - just re-announce
            if (item.Obj == null)
            {
                AnnouncePrizeWallItem();
                return;
            }

            // Skip activation for already-owned items
            if (item.IsOwned)
            {
                _announcer.AnnounceInterrupt($"{item.Label}, {Strings.ProfileItemOwned}");
                return;
            }

            // Find the first active CustomButton under this StoreItemBase (the purchase button)
            GameObject buttonToClick = null;
            foreach (var mb in item.Obj.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == "CustomButton")
                {
                    buttonToClick = mb.gameObject;
                    break;
                }
            }

            if (buttonToClick != null)
            {
                _announcer.AnnounceInterrupt(Strings.Activating(item.Label));
                UIActivator.Activate(buttonToClick);
            }
            else
            {
                // Fallback: activate the item itself
                _announcer.AnnounceInterrupt(Strings.Activating(item.Label));
                UIActivator.Activate(item.Obj);
            }
        }

        #endregion

        #region Confirmation Modal (follows StoreNavigator pattern)

        private MonoBehaviour GetConfirmationModalMb()
        {
            if (_prizeWallConfirmModalField == null || _prizeWallController == null) return null;
            try
            {
                return _prizeWallConfirmModalField.GetValue(_prizeWallController) as MonoBehaviour;
            }
            catch { return null; }
        }

        private void DiscoverConfirmationModalElements()
        {
            _modalElements.Clear();
            _modalElementIndex = 0;

            if (_confirmationModalMb == null) return;

            EnsureConfirmationModalReflectionCached();
            if (_modalButtonFields == null) return;

            // Phase 1: Text blocks from known content containers only.
            // StoreConfirmationModal has _storeItemContainer (reparented item widget with card
            // preview) and _productListContainer (card tiles for bundles). Scanning only these
            // avoids picking up structural UI text like the cancel label.
            var contentContainers = new List<Transform>();
            try
            {
                if (_modalItemContainerField != null)
                {
                    var c = _modalItemContainerField.GetValue(_confirmationModalMb) as Transform;
                    if (c != null && c.gameObject.activeInHierarchy) contentContainers.Add(c);
                }
                if (_modalProductListField != null)
                {
                    var c = _modalProductListField.GetValue(_confirmationModalMb) as Transform;
                    if (c != null && c.gameObject.activeInHierarchy) contentContainers.Add(c);
                }
            }
            catch { /* Reflection may fail on different game versions */ }

            var seenTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var container in contentContainers)
            {
                foreach (var tmp in container.GetComponentsInChildren<TMPro.TMP_Text>(true))
                {
                    if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;

                    string text = UITextExtractor.StripRichText(tmp.text)?.Trim();
                    if (string.IsNullOrEmpty(text) || text.Length < 3) continue;

                    var lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.Length < 3) continue;
                        if (!seenTexts.Add(trimmed)) continue;

                        _modalElements.Add((null, trimmed));
                    }
                }
            }

            // Phase 2: Purchase buttons
            foreach (var field in _modalButtonFields)
            {
                if (field == null) continue;
                try
                {
                    var buttonStruct = field.GetValue(_confirmationModalMb);
                    if (buttonStruct == null) continue;

                    var customButton = _modalPbButtonField?.GetValue(buttonStruct) as MonoBehaviour;
                    if (customButton == null || !customButton.gameObject.activeInHierarchy) continue;

                    // Check Interactable
                    var interactableProp = customButton.GetType().GetProperty("Interactable", AllInstanceFlags);
                    if (interactableProp != null && !(bool)interactableProp.GetValue(customButton)) continue;

                    var labelTmp = _modalPbLabelField?.GetValue(buttonStruct) as TMPro.TMP_Text;
                    string priceText = labelTmp?.text?.Trim() ?? "";

                    if (string.IsNullOrEmpty(priceText))
                        priceText = UITextExtractor.GetText(customButton.gameObject) ?? customButton.gameObject.name;

                    _modalElements.Add((customButton.gameObject, $"{priceText}, {Strings.RoleButton}"));
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            // Phase 3: Cancel option (virtual element with null GameObject)
            _modalElements.Add((null, Strings.PopupCancel));

            MelonLogger.Msg($"[Mastery] Found {_modalElements.Count} confirmation modal elements");
        }

        private void AnnounceConfirmationModal()
        {
            // Extract item name from modal's _label field
            string labelText = null;
            if (_confirmationModalMb != null && _modalLabelField != null)
            {
                try
                {
                    var localize = _modalLabelField.GetValue(_confirmationModalMb) as MonoBehaviour;
                    if (localize != null)
                    {
                        var tmp = localize.GetComponentInChildren<TMPro.TMP_Text>();
                        if (tmp != null && !string.IsNullOrEmpty(tmp.text?.Trim()))
                            labelText = tmp.text.Trim();
                    }
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            // Fallback to generic text extraction
            if (string.IsNullOrEmpty(labelText))
                labelText = UITextExtractor.GetText(_confirmationModalMb?.gameObject);

            string announcement = "Confirm purchase";
            if (!string.IsNullOrEmpty(labelText))
                announcement += $": {labelText}";
            announcement += $". {_modalElements.Count} options.";

            _announcer.AnnounceInterrupt(announcement);

            if (_modalElements.Count > 0)
            {
                _modalElementIndex = 0;
                _announcer.Announce(
                    Strings.ItemPositionOf(1, _modalElements.Count, _modalElements[0].label),
                    AnnouncementPriority.Normal);
            }
        }

        private void HandleConfirmationModalInput()
        {
            if (_modalElements.Count == 0) return;

            // Up/Down navigate modal elements
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveModalElement(-1);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveModalElement(1);
                return;
            }
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                MoveModalElement(shift ? -1 : 1);
                return;
            }

            // Enter/Space activates current element
            bool enterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            bool spacePressed = InputManager.GetKeyDownAndConsume(KeyCode.Space);
            if (enterPressed || spacePressed)
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
                if (_modalElementIndex >= 0 && _modalElementIndex < _modalElements.Count)
                {
                    var elem = _modalElements[_modalElementIndex];
                    MelonLogger.Msg($"[Mastery] Activating modal element: {elem.label}");
                    if (elem.obj == null && _modalElementIndex == _modalElements.Count - 1)
                    {
                        // Last element with null obj = synthetic cancel option
                        DismissConfirmationModal();
                    }
                    else if (elem.obj != null)
                    {
                        // Purchase button
                        UIActivator.Activate(elem.obj);
                    }
                    // else: text block — Enter re-reads it (no action)
                }
                return;
            }

            // Backspace/Escape dismisses modal
            if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Escape))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                InputManager.ConsumeKey(KeyCode.Escape);
                DismissConfirmationModal();
                return;
            }
        }

        private void MoveModalElement(int direction)
        {
            int newIndex = _modalElementIndex + direction;
            if (newIndex < 0)
            {
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }
            if (newIndex >= _modalElements.Count)
            {
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }
            _modalElementIndex = newIndex;
            _announcer.AnnounceInterrupt(
                Strings.ItemPositionOf(_modalElementIndex + 1, _modalElements.Count, _modalElements[_modalElementIndex].label));
        }

        private void DismissConfirmationModal()
        {
            if (_confirmationModalMb != null && _modalCloseMethod != null)
            {
                try
                {
                    MelonLogger.Msg("[Mastery] Closing confirmation modal via Close()");
                    _modalCloseMethod.Invoke(_confirmationModalMb, null);
                    _announcer.Announce(Strings.Cancelled, AnnouncementPriority.High);
                    return;
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[Mastery] Error calling Close(): {ex.Message}");
                }
            }

            MelonLogger.Msg("[Mastery] Could not close confirmation modal");
        }

        #endregion
    }
}
