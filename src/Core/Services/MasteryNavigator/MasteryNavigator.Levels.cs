using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    public partial class MasteryNavigator
    {
        #region Constants

        private const int LevelsPerPageJump = 10;

        #endregion

        #region Navigation State

        private int _currentLevelIndex;    // Index into _levelData list (0 = status item, 1+ = levels)
        private int _currentTierIndex;     // Which reward tier within level (0=Free, 1=Premium, 2=Renewal)

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

        #region Screen Detection (Levels)

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

        #endregion

        #region Element Discovery (Levels)

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

        #region Announcements (Levels)

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

        #region Input Handling (Levels)

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

        #endregion
    }
}
