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
        public override string ScreenName => _mode == MasteryMode.PrizeWall ? "Prize Wall" : "Mastery";
        public override int Priority => MasteryPriority;
        protected override bool SupportsCardNavigation => false;
        protected override bool AcceptSpaceKey => false;

        #endregion

        #region Navigation State

        private int _currentLevelIndex;    // Index into _levelData list (0 = status item, 1+ = levels)
        private int _currentTierIndex;     // Which reward tier within level (0=Free, 1=Premium, 2=Renewal)

        // Popup overlay tracking
        private GameObject _activePopup;
        private bool _isPopupActive;
        private List<(GameObject obj, string label)> _popupElements = new List<(GameObject, string)>();
        private int _popupElementIndex;

        // PrizeWall state
        private MonoBehaviour _prizeWallController;
        private GameObject _prizeWallGameObject;
        private List<(GameObject obj, string label)> _prizeWallItems = new List<(GameObject, string)>();
        private int _prizeWallIndex;
        private string _sphereCount;
        private GameObject _prizeWallBackButton;

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

        public MasteryNavigator(IAnnouncementService announcer) : base(announcer) { }

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
        /// Handle panel changes - detect popups appearing on top of mastery.
        /// </summary>
        private void OnPanelChanged(PanelInfo oldPanel, PanelInfo newPanel)
        {
            if (!_isActive) return;

            if (newPanel != null && IsPopupPanel(newPanel))
            {
                MelonLogger.Msg($"[Mastery] Popup detected: {newPanel.Name}");
                _activePopup = newPanel.GameObject;
                _isPopupActive = true;
                DiscoverPopupElements();
                AnnouncePopup();
            }
            else if (_isPopupActive && newPanel == null)
            {
                MelonLogger.Msg("[Mastery] Popup closed, returning to navigation");
                _activePopup = null;
                _isPopupActive = false;
                _popupElements.Clear();
                // Re-announce current position based on mode
                if (_mode == MasteryMode.PrizeWall)
                    AnnouncePrizeWallItem();
                else
                    AnnounceCurrentLevel();
            }
        }

        private static bool IsPopupPanel(PanelInfo panel)
        {
            if (panel == null) return false;

            string name = panel.Name;

            // Ignore benign game overlays that aren't real popups:
            // - ObjectivePopup: daily quest overlay
            // - FullscreenZFBrowser: embedded browser canvas
            // - RewardPopup3DIcon: 3D reward preview shown on level hover (card art decoration)
            if (name.Contains("ObjectivePopup") || name.Contains("FullscreenZFBrowser") ||
                name.Contains("RewardPopup3DIcon"))
                return false;

            if (panel.Type == PanelType.Popup) return true;

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
                _currentPageProp = _viewType.GetProperty("CurrentPage", BindingFlags.Public | BindingFlags.Instance);
                _pagesCountProp = _viewType.GetProperty("PagesCount", BindingFlags.Public | BindingFlags.Instance);
                _trackNameField = _viewType.GetField("TrackName", BindingFlags.Public | BindingFlags.Instance);
                _trackLabelField = _viewType.GetField("TrackLabel", BindingFlags.Public | BindingFlags.Instance);
                _masteryTreeButtonField = _viewType.GetField("_masteryTreeButton", flags);
                _previousTreeButtonField = _viewType.GetField("_previousTreeButton", flags);
                _purchaseButtonField = _viewType.GetField("_purchaseButton", flags);
                _purchaseCenterField = _viewType.GetField("_purchaseCenter", flags);

                // SetMasteryDataProvider via view's _masteryPassProvider property
                _masteryPassProviderProp = _viewType.GetProperty("_masteryPassProvider", flags);
                if (_masteryPassProviderProp != null)
                {
                    _setMasteryDataProviderType = _masteryPassProviderProp.PropertyType;
                    var pubInstance = BindingFlags.Public | BindingFlags.Instance;
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
                    _pageLevelStartField = _pageLevelsType.GetField("LevelStart", BindingFlags.Public | BindingFlags.Instance);
                    _pageLevelEndProp = _pageLevelsType.GetProperty("LevelEnd", BindingFlags.Public | BindingFlags.Instance);
                }
            }

            // Find types from assemblies
            _trackLevelType = null;
            _clientLevelInfoType = null;
            _rewardDisplayType = null;
            _mtgaLocStringType = null;
            _languagesType = null;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (_trackLevelType == null)
                    _trackLevelType = asm.GetType("Core.MainNavigation.RewardTrack.ProgressionTrackLevel");
                if (_clientLevelInfoType == null)
                    _clientLevelInfoType = asm.GetType("Core.MainNavigation.RewardTrack.ClientTrackLevelInfo");
                if (_rewardDisplayType == null)
                    _rewardDisplayType = asm.GetType("RewardDisplayData");
                if (_mtgaLocStringType == null)
                    _mtgaLocStringType = asm.GetType("MTGALocalizedString");
                if (_languagesType == null)
                    _languagesType = asm.GetType("Wotc.Mtga.Loc.Languages");
            }

            // ProgressionTrackLevel fields
            if (_trackLevelType != null)
            {
                _levelIndexField = _trackLevelType.GetField("Index", BindingFlags.Public | BindingFlags.Instance);
                _levelExpField = _trackLevelType.GetField("EXPProgressIfIsCurrent", BindingFlags.Public | BindingFlags.Instance);
                _levelCompleteField = _trackLevelType.GetField("IsProgressionComplete", BindingFlags.Public | BindingFlags.Instance);
                _levelRepeatableField = _trackLevelType.GetField("IsRepeatable", BindingFlags.Public | BindingFlags.Instance);
                _serverLevelField = _trackLevelType.GetField("ServerLevel", BindingFlags.Public | BindingFlags.Instance);
            }

            // ClientTrackLevelInfo fields
            if (_clientLevelInfoType == null && _serverLevelField != null)
            {
                _clientLevelInfoType = _serverLevelField.FieldType;
            }
            if (_clientLevelInfoType != null)
            {
                _xpToCompleteField = _clientLevelInfoType.GetField("xpToComplete", BindingFlags.Public | BindingFlags.Instance);
            }

            // RewardDisplayData fields
            if (_rewardDisplayType != null)
            {
                _rewardMainTextField = _rewardDisplayType.GetField("MainText", BindingFlags.Public | BindingFlags.Instance);
                _rewardQuantityField = _rewardDisplayType.GetField("Quantity", BindingFlags.Public | BindingFlags.Instance);
                _rewardDescTextField = _rewardDisplayType.GetField("DescriptionText", BindingFlags.Public | BindingFlags.Instance);
                _rewardSecondaryField = _rewardDisplayType.GetField("SecondaryText", BindingFlags.Public | BindingFlags.Instance);
            }

            // MTGALocalizedString fields
            if (_mtgaLocStringType != null)
            {
                _locStringKeyField = _mtgaLocStringType.GetField("Key", BindingFlags.Public | BindingFlags.Instance);
                _locStringParamsField = _mtgaLocStringType.GetField("Parameters", BindingFlags.Public | BindingFlags.Instance);
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
            var flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

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
                catch { }
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
                catch { }
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
            catch { }
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
                catch { }
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
                catch { }
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
                catch { }
            }

            if (layoutParent != null)
            {
                // Find all active StoreItemBase children - these are the purchasable items
                var discovered = new List<(GameObject obj, string label, float sortOrder)>();

                foreach (var mb in layoutParent.GetComponentsInChildren<MonoBehaviour>(false))
                {
                    if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                    if (mb.GetType().Name != "StoreItemBase") continue;

                    string label = ExtractStoreItemLabel(mb.gameObject);

                    var pos = mb.transform.position;
                    // Sort by Y descending (top first), then X ascending (left first)
                    discovered.Add((mb.gameObject, label, -pos.y * 1000 + pos.x));
                }

                foreach (var (obj, label, _) in discovered.OrderBy(x => x.sortOrder))
                {
                    _prizeWallItems.Add((obj, label));
                }
            }

            // Insert virtual sphere status item at position 0
            _prizeWallItems.Insert(0, (null, Strings.PrizeWallSphereStatus(_sphereCount)));

            // Cache confirmation modal GameObject for polling
            _confirmationModalGameObject = null;
            if (_prizeWallConfirmModalField != null)
            {
                try
                {
                    var modal = _prizeWallConfirmModalField.GetValue(_prizeWallController) as MonoBehaviour;
                    if (modal != null)
                        _confirmationModalGameObject = modal.gameObject;
                }
                catch { }
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
        /// Extract a descriptive label from a StoreItemBase including item name and sphere cost.
        /// </summary>
        private string ExtractStoreItemLabel(GameObject storeItemGo)
        {
            // Collect all visible TMP_Text that aren't under purchase buttons
            var allTexts = storeItemGo.GetComponentsInChildren<TMPro.TMP_Text>(false);
            string itemName = null;
            string costText = null;

            foreach (var tmp in allTexts)
            {
                if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;
                string text = tmp.text?.Trim();
                if (string.IsNullOrEmpty(text)) continue;

                // Clean rich text tags
                text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", "").Trim();
                if (string.IsNullOrEmpty(text)) continue;

                // Check if this TMP is inside a purchase button (MainButtonGreen/Blue/Orange/Clear)
                bool isPurchaseButton = false;
                var parent = tmp.transform.parent;
                while (parent != null && parent != storeItemGo.transform)
                {
                    string pName = parent.name;
                    if (pName.Contains("MainButton") || pName.Contains("BlueButton") ||
                        pName.Contains("OrangeButton") || pName.Contains("ClearButton"))
                    {
                        isPurchaseButton = true;
                        break;
                    }
                    parent = parent.parent;
                }

                if (isPurchaseButton)
                {
                    // This is a cost label (e.g., "2" for 2 spheres)
                    if (costText == null)
                        costText = text;
                }
                else if (itemName == null && text.Length > 1)
                {
                    // First non-button text is the item name
                    itemName = text;
                }
            }

            if (string.IsNullOrEmpty(itemName))
            {
                itemName = UITextExtractor.GetText(storeItemGo);
                if (string.IsNullOrEmpty(itemName)) itemName = storeItemGo.name;
                itemName = System.Text.RegularExpressions.Regex.Replace(itemName, @"<[^>]+>", "").Trim();
            }

            // Append sphere cost if found
            if (!string.IsNullOrEmpty(costText))
                return $"{itemName}, {costText} spheres";

            return itemName;
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
                    resolved = System.Text.RegularExpressions.Regex.Replace(resolved, @"<[^>]+>", "").Trim();
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
                            resolved = System.Text.RegularExpressions.Regex.Replace(resolved, @"<[^>]+>", "").Trim();
                            if (!string.IsNullOrEmpty(resolved)) return resolved;
                        }
                    }
                }
                catch { }
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
                catch { }
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

            // Subscribe to panel changes for popup detection
            if (PanelStateManager.Instance != null)
                PanelStateManager.Instance.OnPanelChanged += OnPanelChanged;
        }

        protected override void OnDeactivating()
        {
            _levelData.Clear();
            _actionButtons.Clear();
            _activePopup = null;
            _isPopupActive = false;
            _popupElements.Clear();
            _prizeWallItems.Clear();
            _confirmationModalGameObject = null;

            // Unsubscribe from panel changes
            if (PanelStateManager.Instance != null)
                PanelStateManager.Instance.OnPanelChanged -= OnPanelChanged;
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

        protected override string GetActivationAnnouncement()
        {
            if (_mode == MasteryMode.PrizeWall)
            {
                return Strings.PrizeWallActivation(_prizeWallItems.Count, _sphereCount);
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

            return Strings.MasteryActivation(_trackTitle, _levelData[_currentPlayerLevel].LevelNumber, _totalLevels, xpStr);
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
            _announcer.AnnounceInterrupt(
                $"{_currentLevelIndex} of {_totalLevels}: " +
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
                announcement += $", tier {_currentTierIndex + 1} of {level.Tiers.Count}";

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

            // Poll for confirmation modal in PrizeWall mode (modal is reused, not re-instantiated,
            // so PanelStateManager doesn't fire events after the first time)
            if (_mode == MasteryMode.PrizeWall && !_isPopupActive && _confirmationModalGameObject != null)
            {
                if (_confirmationModalGameObject.activeInHierarchy)
                {
                    MelonLogger.Msg("[Mastery] Confirmation modal detected via polling");
                    _activePopup = _confirmationModalGameObject;
                    _isPopupActive = true;
                    DiscoverPopupElements();
                    AnnouncePopup();
                }
            }

            // Check if popup is still valid
            if (_isPopupActive && (_activePopup == null || !_activePopup.activeInHierarchy))
            {
                MelonLogger.Msg("[Mastery] Popup became invalid, returning to navigation");
                _activePopup = null;
                _isPopupActive = false;
                _popupElements.Clear();
                // Re-announce current position based on mode
                if (_mode == MasteryMode.PrizeWall)
                    AnnouncePrizeWallItem();
                else
                    AnnounceCurrentLevel();
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
            // Handle popup input when popup is active
            if (_isPopupActive)
            {
                HandlePopupInput();
                return;
            }

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
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) ||
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
                    _announcer.AnnounceInterrupt(Strings.BeginningOfList);
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) ||
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
                    _announcer.AnnounceInterrupt(Strings.EndOfList);
                }
                return;
            }

            // Left/Right: Cycle reward tiers (or buttons on status item)
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                CycleTier(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
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

            // Backspace: Go back
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                ActivateBackButton();
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
                    _announcer.AnnounceInterrupt(Strings.NavigatingBack);
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
                        _announcer.AnnounceInterrupt(Strings.NavigatingBack);
                        UIActivator.Activate(backBtn.gameObject);
                        return;
                    }
                }
                catch { }
            }

            _announcer.AnnounceInterrupt("No back button found");
        }

        private void HandlePrizeWallInput()
        {
            if (_prizeWallItems.Count == 0) return;

            // Up/W/Shift+Tab: Previous item
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) ||
                (Input.GetKeyDown(KeyCode.Tab) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))))
            {
                if (_prizeWallIndex > 0)
                {
                    _prizeWallIndex--;
                    AnnouncePrizeWallItem();
                }
                else
                {
                    _announcer.AnnounceInterrupt(Strings.BeginningOfList);
                }
                return;
            }

            // Down/S/Tab: Next item
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) ||
                (Input.GetKeyDown(KeyCode.Tab) && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)))
            {
                if (_prizeWallIndex < _prizeWallItems.Count - 1)
                {
                    _prizeWallIndex++;
                    AnnouncePrizeWallItem();
                }
                else
                {
                    _announcer.AnnounceInterrupt(Strings.EndOfList);
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

            // Backspace: Go back (returns to mastery levels)
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                if (_prizeWallBackButton != null)
                {
                    _announcer.AnnounceInterrupt(Strings.NavigatingBack);
                    UIActivator.Activate(_prizeWallBackButton);
                }
                else
                {
                    _announcer.AnnounceInterrupt("No back button found");
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
            _announcer.AnnounceInterrupt(
                Strings.PrizeWallItem(_prizeWallIndex + 1, _prizeWallItems.Count, item.label));
        }

        private void ActivatePrizeWallItem()
        {
            if (_prizeWallIndex < 0 || _prizeWallIndex >= _prizeWallItems.Count) return;

            var item = _prizeWallItems[_prizeWallIndex];

            // Virtual status item (obj=null) - just re-announce
            if (item.obj == null)
            {
                AnnouncePrizeWallItem();
                return;
            }

            // Find the first active CustomButton under this StoreItemBase (the purchase button)
            GameObject buttonToClick = null;
            foreach (var mb in item.obj.GetComponentsInChildren<MonoBehaviour>(false))
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
                _announcer.AnnounceInterrupt(Strings.Activating(item.label));
                UIActivator.Activate(buttonToClick);
            }
            else
            {
                // Fallback: activate the item itself
                _announcer.AnnounceInterrupt(Strings.Activating(item.label));
                UIActivator.Activate(item.obj);
            }
        }

        #endregion

        #region Popup Handling

        private void DiscoverPopupElements()
        {
            _popupElements.Clear();
            _popupElementIndex = 0;

            if (_activePopup == null) return;

            MelonLogger.Msg($"[Mastery] Discovering popup elements in: {_activePopup.name}");

            var addedObjects = new HashSet<GameObject>();
            var discovered = new List<(GameObject obj, string label, float sortOrder)>();

            // Collect StoreItemBase GameObjects so we can skip buttons inside item widgets
            var storeItemBases = new HashSet<Transform>();
            foreach (var sib in _activePopup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (sib != null && sib.GetType().Name == "StoreItemBase")
                    storeItemBases.Add(sib.transform);
            }

            // Find CustomButton and SystemMessageButtonView components
            foreach (var mb in _activePopup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (addedObjects.Contains(mb.gameObject)) continue;

                string typeName = mb.GetType().Name;
                if (typeName == "SystemMessageButtonView" || typeName == "CustomButton" || typeName == "CustomButtonWithTooltip")
                {
                    // Skip buttons that are children of a StoreItemBase (item preview widget)
                    if (IsChildOfAny(mb.transform, storeItemBases)) continue;

                    string label = UITextExtractor.GetText(mb.gameObject);
                    if (string.IsNullOrEmpty(label)) label = mb.gameObject.name;

                    var pos = mb.gameObject.transform.position;
                    discovered.Add((mb.gameObject, label, -pos.y * 1000 + pos.x));
                    addedObjects.Add(mb.gameObject);
                }
            }

            // Also check standard Unity Buttons
            foreach (var button in _activePopup.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                if (button == null || !button.gameObject.activeInHierarchy || !button.interactable) continue;
                if (addedObjects.Contains(button.gameObject)) continue;

                // Skip buttons inside item preview widget
                if (IsChildOfAny(button.transform, storeItemBases)) continue;

                string label = UITextExtractor.GetText(button.gameObject);
                if (string.IsNullOrEmpty(label)) label = button.gameObject.name;

                var pos = button.gameObject.transform.position;
                discovered.Add((button.gameObject, label, -pos.y * 1000 + pos.x));
                addedObjects.Add(button.gameObject);
            }

            // Sort by visual position: top-to-bottom, left-to-right
            foreach (var (obj, label, _) in discovered.OrderBy(x => x.sortOrder))
            {
                _popupElements.Add((obj, label));
            }

            // Add synthetic Cancel option (dismisses the modal)
            _popupElements.Add((null, Strings.PopupCancel));

            MelonLogger.Msg($"[Mastery] Popup has {_popupElements.Count} elements " +
                $"(skipped {storeItemBases.Count} item widgets)");
        }

        private static bool IsChildOfAny(Transform child, HashSet<Transform> parents)
        {
            var current = child.parent;
            while (current != null)
            {
                if (parents.Contains(current)) return true;
                current = current.parent;
            }
            return false;
        }

        private void AnnouncePopup()
        {
            // Extract popup body text (skipping button labels)
            string bodyText = ExtractPopupBodyText(_activePopup);
            string announcement;

            if (!string.IsNullOrEmpty(bodyText))
                announcement = $"Popup: {bodyText}. {_popupElements.Count} options.";
            else
                announcement = $"Popup. {_popupElements.Count} options.";

            _announcer.AnnounceInterrupt(announcement);

            if (_popupElements.Count > 0)
            {
                _popupElementIndex = 0;
                _announcer.Announce($"1 of {_popupElements.Count}: {_popupElements[0].label}",
                    AnnouncementPriority.Normal);
            }
        }

        private string ExtractPopupBodyText(GameObject popup)
        {
            if (popup == null) return null;

            var texts = popup.GetComponentsInChildren<TMPro.TMP_Text>(true)
                .Where(t => t != null && t.gameObject.activeInHierarchy)
                .OrderByDescending(t => t.fontSize)
                .ToList();

            foreach (var text in texts)
            {
                string content = text.text?.Trim();
                if (string.IsNullOrEmpty(content) || content.Length < 5) continue;

                // Skip text that lives under a button parent
                var parent = text.transform.parent;
                bool isButtonText = false;
                while (parent != null)
                {
                    string parentName = parent.name.ToLower();
                    if (parentName.Contains("button"))
                    {
                        isButtonText = true;
                        break;
                    }
                    parent = parent.parent;
                }

                if (!isButtonText)
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
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) ||
                (Input.GetKeyDown(KeyCode.Tab) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))))
            {
                if (_popupElements.Count > 0)
                {
                    _popupElementIndex--;
                    if (_popupElementIndex < 0) _popupElementIndex = _popupElements.Count - 1;
                    _announcer.AnnounceInterrupt(
                        $"{_popupElementIndex + 1} of {_popupElements.Count}: {_popupElements[_popupElementIndex].label}");
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) ||
                (Input.GetKeyDown(KeyCode.Tab) && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)))
            {
                if (_popupElements.Count > 0)
                {
                    _popupElementIndex++;
                    if (_popupElementIndex >= _popupElements.Count) _popupElementIndex = 0;
                    _announcer.AnnounceInterrupt(
                        $"{_popupElementIndex + 1} of {_popupElements.Count}: {_popupElements[_popupElementIndex].label}");
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space))
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);

                if (_popupElements.Count > 0 && _popupElementIndex < _popupElements.Count)
                {
                    var elem = _popupElements[_popupElementIndex];
                    if (elem.obj == null)
                    {
                        // Synthetic Cancel option - dismiss the modal
                        DismissPopup();
                    }
                    else
                    {
                        _announcer.AnnounceInterrupt(Strings.Activating(elem.label));
                        UIActivator.Activate(elem.obj);
                    }
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                DismissPopup();
                return;
            }
        }

        private void DismissPopup()
        {
            _announcer.AnnounceInterrupt(Strings.Cancelled);

            // For confirmation modal, call Close() via the StoreConfirmationModal
            if (_confirmationModalGameObject != null && _activePopup == _confirmationModalGameObject)
            {
                // Find the Close method on StoreConfirmationModal
                foreach (var mb in _confirmationModalGameObject.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    var closeMethod = mb.GetType().GetMethod("Close",
                        BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    if (closeMethod != null)
                    {
                        try
                        {
                            closeMethod.Invoke(mb, null);
                            MelonLogger.Msg("[Mastery] Dismissed confirmation modal via Close()");
                            return;
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Msg($"[Mastery] Error calling Close(): {ex.Message}");
                        }
                    }
                }
            }

            // Fallback: deactivate the popup
            if (_activePopup != null)
            {
                _activePopup.SetActive(false);
                MelonLogger.Msg("[Mastery] Dismissed popup via SetActive(false)");
            }
        }

        #endregion
    }
}
