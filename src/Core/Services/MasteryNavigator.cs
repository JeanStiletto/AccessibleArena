using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.PanelDetection;
using System;
using System.Collections;
using System.Collections.Generic;
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

        #region Navigator Identity

        public override string NavigatorId => "Mastery";
        public override string ScreenName => "Mastery";
        public override int Priority => MasteryPriority;
        protected override bool SupportsCardNavigation => false;
        protected override bool AcceptSpaceKey => false;

        #endregion

        #region Navigation State

        private enum Section
        {
            Levels,
            Buttons
        }

        private Section _section = Section.Levels;
        private int _currentLevelIndex;    // Index into _levelData list
        private int _currentTierIndex;     // Which reward tier within level (0=Free, 1=Premium, 2=Renewal)
        private int _currentButtonIndex;   // Index into _actionButtons list

        // Popup overlay tracking
        private GameObject _activePopup;
        private bool _isPopupActive;
        private List<(GameObject obj, string label)> _popupElements = new List<(GameObject, string)>();
        private int _popupElementIndex;

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

        // Reflection: MTGALocalizedString
        private Type _mtgaLocStringType;
        private FieldInfo _locStringKeyField;      // string Key
        private FieldInfo _locStringParamsField;   // Dictionary<string,string> Parameters

        // Reflection: Localization
        private Type _languagesType;
        private PropertyInfo _activeLocProviderProp; // static IClientLocProvider ActiveLocProvider
        private MethodInfo _getLocalizedTextMethod;   // IClientLocProvider.GetLocalizedText(string)

        private bool _reflectionInitialized;

        #endregion

        #region Discovered Data

        private struct LevelData
        {
            public int LevelNumber;          // Display level (1-based, from Index field)
            public int ExpProgress;          // Current XP (if current level)
            public int XpToComplete;         // XP needed
            public bool IsComplete;
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
            var controller = FindController();
            if (controller == null) return false;

            if (!IsControllerOpen(controller)) return false;

            _controller = controller;
            _controllerGameObject = controller.gameObject;

            return true;
        }

        private MonoBehaviour FindController()
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
                MelonLogger.Msg("[Mastery] Popup closed, returning to mastery");
                _activePopup = null;
                _isPopupActive = false;
                _popupElements.Clear();
                // Re-announce current position
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
                $"PageLevels={_pageLevelsType != null}");
        }

        #endregion

        #region Element Discovery

        protected override void DiscoverElements()
        {
            // Build level data from the view
            BuildLevelData();
            BuildActionButtons();

            if (_levelData.Count > 0 || _actionButtons.Count > 0)
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

            for (int i = 0; i < levelsList.Count; i++)
            {
                var level = levelsList[i];
                if (level == null) continue;

                var levelData = ExtractLevelData(level, i, rewardDataList);
                _levelData.Add(levelData);

                // Find current player level
                if (!levelData.IsComplete && _currentPlayerLevel < 0)
                    _currentPlayerLevel = i;
            }

            // If all complete, set to last
            if (_currentPlayerLevel < 0)
                _currentPlayerLevel = _levelData.Count - 1;

            MelonLogger.Msg($"[Mastery] Built {_levelData.Count} levels, current={_currentPlayerLevel}, track={_trackTitle}");
        }

        private LevelData ExtractLevelData(object level, int listIndex, IList rewardDataList)
        {
            int levelNum = 1;
            int expProgress = 0;
            int xpToComplete = 0;
            bool isComplete = false;
            bool isRepeatable = false;

            try
            {
                if (_levelIndexField != null)
                    levelNum = (int)_levelIndexField.GetValue(level);
                if (_levelExpField != null)
                    expProgress = (int)_levelExpField.GetValue(level);
                if (_levelCompleteField != null)
                    isComplete = (bool)_levelCompleteField.GetValue(level);
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
                LevelNumber = levelNum,
                ExpProgress = expProgress,
                XpToComplete = xpToComplete,
                IsComplete = isComplete,
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
            _section = Section.Levels;
            _currentTierIndex = 0;

            // Start at current player level
            _currentLevelIndex = _currentPlayerLevel >= 0 ? _currentPlayerLevel : 0;
            _currentButtonIndex = 0;

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

            // Unsubscribe from panel changes
            if (PanelStateManager.Instance != null)
                PanelStateManager.Instance.OnPanelChanged -= OnPanelChanged;
        }

        public override void OnSceneChanged(string sceneName)
        {
            _controller = null;
            _controllerGameObject = null;
            _reflectionInitialized = false;

            base.OnSceneChanged(sceneName);
        }

        #endregion

        #region Announcements

        protected override string GetActivationAnnouncement()
        {
            if (_levelData.Count == 0)
                return $"{_trackTitle}. No levels found.";

            // Build XP string for current level
            string xpStr = "";
            if (_currentLevelIndex >= 0 && _currentLevelIndex < _levelData.Count)
            {
                var level = _levelData[_currentLevelIndex];
                if (level.XpToComplete > 0 && !level.IsComplete)
                    xpStr = $"{level.ExpProgress}/{level.XpToComplete} XP";
                else if (level.IsComplete)
                    xpStr = "completed";
            }

            return Strings.MasteryActivation(_trackTitle, _currentLevelIndex + 1, _totalLevels, xpStr);
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
            string reward = GetPrimaryRewardName(level);
            string status = GetLevelStatus(level);

            _announcer.AnnounceInterrupt(
                $"{_currentLevelIndex + 1} of {_totalLevels}: " +
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

        private void AnnounceCurrentButton()
        {
            if (_currentButtonIndex < 0 || _currentButtonIndex >= _actionButtons.Count) return;

            var button = _actionButtons[_currentButtonIndex];
            _announcer.AnnounceInterrupt(
                $"{_currentButtonIndex + 1} of {_actionButtons.Count}: {button.Label}");
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
            if (_currentPlayerLevel >= 0 && level.LevelNumber == _levelData[_currentPlayerLevel].LevelNumber)
                return Strings.MasteryCurrentLevel;
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

            // Verify controller is still valid
            if (_controller == null || _controllerGameObject == null || !_controllerGameObject.activeInHierarchy)
            {
                Deactivate();
                return;
            }

            // Check if mastery screen is still open
            if (!IsControllerOpen(_controller))
            {
                Deactivate();
                return;
            }

            // Check if popup is still valid
            if (_isPopupActive && (_activePopup == null || !_activePopup.activeInHierarchy))
            {
                MelonLogger.Msg("[Mastery] Popup became invalid, returning to mastery");
                _activePopup = null;
                _isPopupActive = false;
                _popupElements.Clear();
            }

            HandleMasteryInput();
        }

        protected override bool ValidateElements()
        {
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

            switch (_section)
            {
                case Section.Levels:
                    HandleLevelInput();
                    break;
                case Section.Buttons:
                    HandleButtonInput();
                    break;
            }
        }

        private void HandleLevelInput()
        {
            // Up/Down: Navigate levels
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
                else if (_actionButtons.Count > 0)
                {
                    // Move to action buttons
                    _section = Section.Buttons;
                    _currentButtonIndex = _actionButtons.Count - 1;
                    AnnounceCurrentButton();
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

            // Left/Right: Cycle reward tiers
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

            // Home/End: Jump to first/last level
            if (Input.GetKeyDown(KeyCode.Home))
            {
                _currentLevelIndex = 0;
                _currentTierIndex = 0;
                SyncPageForLevel();
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

            // Enter: Announce detailed level info
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
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

        private void HandleButtonInput()
        {
            // Up/Down: Navigate buttons
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) ||
                Input.GetKeyDown(KeyCode.Tab) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            {
                if (_currentButtonIndex > 0)
                {
                    _currentButtonIndex--;
                    AnnounceCurrentButton();
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
                if (_currentButtonIndex < _actionButtons.Count - 1)
                {
                    _currentButtonIndex++;
                    AnnounceCurrentButton();
                }
                else
                {
                    // Move back to levels
                    _section = Section.Levels;
                    _currentLevelIndex = 0;
                    _currentTierIndex = 0;
                    SyncPageForLevel();
                    AnnounceCurrentLevel();
                }
                return;
            }

            // Enter/Space: Activate current button
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space))
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
                ActivateCurrentButton();
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

        private void ActivateCurrentButton()
        {
            if (_currentButtonIndex < 0 || _currentButtonIndex >= _actionButtons.Count) return;

            var button = _actionButtons[_currentButtonIndex];
            _announcer.AnnounceInterrupt(Strings.Activating(button.Label));
            UIActivator.Activate(button.GameObject);
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

        #endregion

        #region Popup Handling

        private void DiscoverPopupElements()
        {
            _popupElements.Clear();
            _popupElementIndex = 0;

            if (_activePopup == null) return;

            // Find buttons and text in the popup
            var buttons = _activePopup.GetComponentsInChildren<UnityEngine.UI.Button>(false);
            foreach (var btn in buttons)
            {
                if (btn == null || !btn.gameObject.activeInHierarchy || !btn.interactable) continue;
                string label = UITextExtractor.GetText(btn.gameObject);
                if (string.IsNullOrEmpty(label)) label = btn.gameObject.name;
                _popupElements.Add((btn.gameObject, label));
            }

            // Also check for CustomButton-type components
            foreach (var mb in _activePopup.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == "CustomButton")
                {
                    // Don't add if we already have it as a Button
                    bool exists = false;
                    foreach (var elem in _popupElements)
                    {
                        if (elem.obj == mb.gameObject) { exists = true; break; }
                    }
                    if (!exists)
                    {
                        string label = UITextExtractor.GetText(mb.gameObject);
                        if (string.IsNullOrEmpty(label)) label = mb.gameObject.name;
                        _popupElements.Add((mb.gameObject, label));
                    }
                }
            }

            MelonLogger.Msg($"[Mastery] Popup has {_popupElements.Count} elements");
        }

        private void AnnouncePopup()
        {
            if (_popupElements.Count == 0)
            {
                // Try to read popup text
                string text = UITextExtractor.GetText(_activePopup);
                if (!string.IsNullOrEmpty(text))
                    _announcer.AnnounceInterrupt($"Popup: {text}");
                else
                    _announcer.AnnounceInterrupt("Popup");
                return;
            }

            string label = _popupElements[0].label;
            _announcer.AnnounceInterrupt($"Popup. {_popupElements.Count} options. 1 of {_popupElements.Count}: {label}");
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
                    _announcer.AnnounceInterrupt(Strings.Activating(elem.label));
                    UIActivator.Activate(elem.obj);
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                // Try to dismiss popup via last element (often a close/cancel button)
                if (_popupElements.Count > 0)
                {
                    var lastElem = _popupElements[_popupElements.Count - 1];
                    UIActivator.Activate(lastElem.obj);
                }
                return;
            }
        }

        #endregion
    }
}
