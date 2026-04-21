using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using AccessibleArena.Core.Utils;

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

        private sealed class LevelsHandles
        {
            // Controller (ProgressionTracksContentController)
            public PropertyInfo IsOpen;
            public FieldInfo ActiveView;
            public FieldInfo BackButton;

            // View (RewardTrackView) — type derived from ActiveView.FieldType
            public FieldInfo Levels;            // List<ProgressionTrackLevel>
            public FieldInfo LevelRewardData;   // List<RewardDisplayData[]>
            public FieldInfo Pages;             // List<PageLevels>
            public PropertyInfo CurrentPage;    // int (get/set)
            public PropertyInfo PagesCount;     // int (get)
            public FieldInfo TrackName;         // string
            public FieldInfo TrackLabel;        // MTGALocalizedString
            public FieldInfo MasteryTreeButton;
            public FieldInfo PreviousTreeButton;
            public FieldInfo PurchaseButton;
            public FieldInfo PurchaseCenter;    // GameObject
            public PropertyInfo MasteryPassProvider;  // private _masteryPassProvider

            // PageLevels nested class — derived from View.GetNestedType("PageLevels")
            public FieldInfo PageLevelStart;    // int
            public PropertyInfo PageLevelEnd;   // int (get)

            // ProgressionTrackLevel — FindType
            public FieldInfo LevelIndex;        // int Index
            public FieldInfo LevelExp;          // int EXPProgressIfIsCurrent
            public FieldInfo LevelRepeatable;   // bool IsRepeatable
            public FieldInfo ServerLevel;       // ClientTrackLevelInfo

            // ClientTrackLevelInfo — derived from ServerLevel.FieldType (or FindType fallback)
            public FieldInfo XpToComplete;      // int xpToComplete

            // RewardDisplayData — FindType
            public FieldInfo RewardMainText;    // MTGALocalizedString
            public FieldInfo RewardQuantity;    // int
            public FieldInfo RewardDescText;    // MTGALocalizedString
            public FieldInfo RewardSecondary;   // MTGALocalizedString

            // SetMasteryDataProvider — derived from MasteryPassProvider.PropertyType
            public MethodInfo GetCurrentLevelIndex;  // (string) -> int

            // MTGALocalizedString — FindType (Key only; ToString() handles param substitution)
            public FieldInfo LocStringKey;      // string Key

            // Languages / LocProvider — FindType("Wotc.Mtga.Loc.Languages")
            public PropertyInfo ActiveLocProvider;  // static IClientLocProvider
            public MethodInfo GetLocalizedText;     // (string) -> string
        }

        private readonly ReflectionCache<LevelsHandles> _levelsCache = new ReflectionCache<LevelsHandles>(
            builder: controllerType =>
            {
                var h = new LevelsHandles
                {
                    IsOpen = controllerType.GetProperty("IsOpen", AllInstanceFlags | BindingFlags.FlattenHierarchy),
                    ActiveView = controllerType.GetField("_activeView", AllInstanceFlags),
                    BackButton = controllerType.GetField("_backButton", AllInstanceFlags),
                };

                // View (RewardTrackView) — chained off _activeView.FieldType
                Type viewType = h.ActiveView?.FieldType;
                if (viewType != null)
                {
                    h.Levels = viewType.GetField("_levels", AllInstanceFlags);
                    h.LevelRewardData = viewType.GetField("_levelRewardData", AllInstanceFlags);
                    h.Pages = viewType.GetField("_pages", AllInstanceFlags);
                    h.CurrentPage = viewType.GetProperty("CurrentPage", PublicInstance);
                    h.PagesCount = viewType.GetProperty("PagesCount", PublicInstance);
                    h.TrackName = viewType.GetField("TrackName", PublicInstance);
                    h.TrackLabel = viewType.GetField("TrackLabel", PublicInstance);
                    h.MasteryTreeButton = viewType.GetField("_masteryTreeButton", AllInstanceFlags);
                    h.PreviousTreeButton = viewType.GetField("_previousTreeButton", AllInstanceFlags);
                    h.PurchaseButton = viewType.GetField("_purchaseButton", AllInstanceFlags);
                    h.PurchaseCenter = viewType.GetField("_purchaseCenter", AllInstanceFlags);
                    h.MasteryPassProvider = viewType.GetProperty("_masteryPassProvider", AllInstanceFlags);

                    // SetMasteryDataProvider — chained off MasteryPassProvider.PropertyType
                    Type providerType = h.MasteryPassProvider?.PropertyType;
                    if (providerType != null)
                    {
                        h.GetCurrentLevelIndex = providerType.GetMethod("GetCurrentLevelIndex",
                            PublicInstance, null, new[] { typeof(string) }, null);
                    }

                    // PageLevels nested class
                    Type pageLevelsType = viewType.GetNestedType("PageLevels", BindingFlags.NonPublic);
                    if (pageLevelsType != null)
                    {
                        h.PageLevelStart = pageLevelsType.GetField("LevelStart", PublicInstance);
                        h.PageLevelEnd = pageLevelsType.GetProperty("LevelEnd", PublicInstance);
                    }
                }

                // ProgressionTrackLevel — FindType
                Type trackLevelType = FindType("Core.MainNavigation.RewardTrack.ProgressionTrackLevel");
                if (trackLevelType != null)
                {
                    h.LevelIndex = trackLevelType.GetField("Index", PublicInstance);
                    h.LevelExp = trackLevelType.GetField("EXPProgressIfIsCurrent", PublicInstance);
                    h.LevelRepeatable = trackLevelType.GetField("IsRepeatable", PublicInstance);
                    h.ServerLevel = trackLevelType.GetField("ServerLevel", PublicInstance);
                }

                // ClientTrackLevelInfo — prefer ServerLevel.FieldType, fall back to FindType
                Type clientLevelInfoType = h.ServerLevel?.FieldType
                    ?? FindType("Core.MainNavigation.RewardTrack.ClientTrackLevelInfo");
                if (clientLevelInfoType != null)
                    h.XpToComplete = clientLevelInfoType.GetField("xpToComplete", PublicInstance);

                // RewardDisplayData — FindType
                Type rewardDisplayType = FindType("RewardDisplayData");
                if (rewardDisplayType != null)
                {
                    h.RewardMainText = rewardDisplayType.GetField("MainText", PublicInstance);
                    h.RewardQuantity = rewardDisplayType.GetField("Quantity", PublicInstance);
                    h.RewardDescText = rewardDisplayType.GetField("DescriptionText", PublicInstance);
                    h.RewardSecondary = rewardDisplayType.GetField("SecondaryText", PublicInstance);
                }

                // MTGALocalizedString — FindType
                Type locStringType = FindType("MTGALocalizedString");
                if (locStringType != null)
                    h.LocStringKey = locStringType.GetField("Key", PublicInstance);

                // Languages.ActiveLocProvider (static) → GetLocalizedText(string)
                Type languagesType = FindType("Wotc.Mtga.Loc.Languages");
                if (languagesType != null)
                {
                    h.ActiveLocProvider = languagesType.GetProperty("ActiveLocProvider",
                        BindingFlags.Public | BindingFlags.Static);
                    Type locProviderType = h.ActiveLocProvider?.PropertyType;
                    if (locProviderType != null)
                        h.GetLocalizedText = locProviderType.GetMethod("GetLocalizedText",
                            new[] { typeof(string) });
                }

                return h;
            },
            validator: _ => true,  // All handles optional; every read site null-checks per graceful-degradation semantics
            logTag: "Mastery",
            logSubject: "Levels");

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
                if (mb.GetType().Name == T.ProgressionTracksContentController)
                    return mb;
            }

            return null;
        }

        private bool IsControllerOpen(MonoBehaviour controller)
        {
            if (!_levelsCache.EnsureInitialized(controller.GetType())) return true;
            var isOpen = _levelsCache.Handles.IsOpen;
            if (isOpen == null) return true;

            try { return (bool)isOpen.GetValue(controller); }
            catch { return false; }
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
                Log.Msg("Mastery", "No active view found");
                return;
            }

            // Get track name/title
            _trackTitle = ResolveTrackTitle(view);

            // Get levels list
            var levelsObj = _levelsCache.Handles.Levels?.GetValue(view);
            if (levelsObj == null)
            {
                Log.Msg("Mastery", "No levels data found");
                return;
            }
            var levelsList = levelsObj as IList;
            if (levelsList == null || levelsList.Count == 0) return;

            // Get reward data list
            var rewardDataObj = _levelsCache.Handles.LevelRewardData?.GetValue(view);
            var rewardDataList = rewardDataObj as IList;

            _totalLevels = levelsList.Count;

            // Get current level from data provider (the level the player is working on)
            // curLevelIndex is the Index field value of the current in-progress level
            int curLevelIndex = -1;
            try
            {
                if (_levelsCache.Handles.MasteryPassProvider != null && _levelsCache.Handles.GetCurrentLevelIndex != null)
                {
                    var provider = _levelsCache.Handles.MasteryPassProvider.GetValue(view);
                    if (provider != null)
                    {
                        string trackName = _levelsCache.Handles.TrackName?.GetValue(view) as string;
                        if (!string.IsNullOrEmpty(trackName))
                        {
                            curLevelIndex = (int)_levelsCache.Handles.GetCurrentLevelIndex.Invoke(provider, new object[] { trackName });
                            Log.Msg("Mastery", $"Data provider: curLevelIndex={curLevelIndex}, trackName={trackName}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Msg("Mastery", $"Error getting current level from provider: {ex.Message}");
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

            Log.Msg("Mastery", $"Built {_levelData.Count} levels, current={_currentPlayerLevel}, " +
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
                if (_levelsCache.Handles.LevelIndex != null)
                    levelIndex = (int)_levelsCache.Handles.LevelIndex.GetValue(level);
                if (_levelsCache.Handles.LevelExp != null)
                    expProgress = (int)_levelsCache.Handles.LevelExp.GetValue(level);
                if (_levelsCache.Handles.LevelRepeatable != null)
                    isRepeatable = (bool)_levelsCache.Handles.LevelRepeatable.GetValue(level);

                if (_levelsCache.Handles.ServerLevel != null && _levelsCache.Handles.XpToComplete != null)
                {
                    var serverLevel = _levelsCache.Handles.ServerLevel.GetValue(level);
                    if (serverLevel != null)
                        xpToComplete = (int)_levelsCache.Handles.XpToComplete.GetValue(serverLevel);
                }
            }
            catch (Exception ex)
            {
                Log.Msg("Mastery", $"Error reading level data: {ex.Message}");
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
                            string rewardName = ResolveLocString(_levelsCache.Handles.RewardMainText?.GetValue(reward));
                            int quantity = 0;
                            if (_levelsCache.Handles.RewardQuantity != null)
                                quantity = (int)_levelsCache.Handles.RewardQuantity.GetValue(reward);
                            string description = ResolveLocString(_levelsCache.Handles.RewardSecondary?.GetValue(reward));

                            if (string.IsNullOrEmpty(rewardName) || rewardName.StartsWith("$"))
                                rewardName = ResolveLocString(_levelsCache.Handles.RewardDescText?.GetValue(reward));
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
                    Log.Msg("Mastery", $"Error reading reward tiers at index {listIndex}: {ex.Message}");
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
            TryAddButton(view, _levelsCache.Handles.MasteryTreeButton, "Mastery Tree");

            // Previous Season button (only if visible)
            TryAddButton(view, _levelsCache.Handles.PreviousTreeButton, "Previous Season");

            // Purchase button (only if purchase center is visible)
            if (_levelsCache.Handles.PurchaseCenter != null && _levelsCache.Handles.PurchaseButton != null)
            {
                try
                {
                    var purchaseCenter = _levelsCache.Handles.PurchaseCenter.GetValue(view) as GameObject;
                    if (purchaseCenter != null && purchaseCenter.activeInHierarchy)
                    {
                        TryAddButton(view, _levelsCache.Handles.PurchaseButton, "Purchase");
                    }
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            // Back button (from controller, not view)
            if (_levelsCache.Handles.BackButton != null && _controller != null)
            {
                try
                {
                    var backBtn = _levelsCache.Handles.BackButton.GetValue(_controller) as MonoBehaviour;
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

            Log.Msg("Mastery", $"Found {_actionButtons.Count} action buttons");
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

            Log.Msg("Mastery", $"Inserted status item with {tiers.Count} tiers ({_actionButtons.Count} buttons)");
        }

        #endregion

        #region Localization

        private string ResolveLocString(object mtgaLocString)
        {
            if (mtgaLocString == null) return null;

            try
            {
                // Check Key first - skip empty strings
                if (_levelsCache.Handles.LocStringKey != null)
                {
                    string key = _levelsCache.Handles.LocStringKey.GetValue(mtgaLocString) as string;
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
                Log.Msg("Mastery", $"Error resolving loc string: {ex.Message}");
                return null;
            }
        }

        private string GetLocalizedText(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_levelsCache.Handles.ActiveLocProvider == null || _levelsCache.Handles.GetLocalizedText == null) return key;

            try
            {
                var locProvider = _levelsCache.Handles.ActiveLocProvider.GetValue(null);
                if (locProvider == null) return key;
                return _levelsCache.Handles.GetLocalizedText.Invoke(locProvider, new object[] { key }) as string;
            }
            catch
            {
                return key;
            }
        }

        private string ResolveTrackTitle(MonoBehaviour view)
        {
            // Try TrackLabel (MTGALocalizedString) first - has ToString() that resolves automatically
            if (_levelsCache.Handles.TrackLabel != null)
            {
                try
                {
                    var label = _levelsCache.Handles.TrackLabel.GetValue(view);
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
            if (_levelsCache.Handles.TrackName != null)
            {
                try
                {
                    var trackName = _levelsCache.Handles.TrackName.GetValue(view) as string;
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
            if (_controller == null || _levelsCache.Handles.ActiveView == null) return null;

            try
            {
                return _levelsCache.Handles.ActiveView.GetValue(_controller) as MonoBehaviour;
            }
            catch
            {
                return null;
            }
        }

        private int GetCurrentPage()
        {
            var view = GetActiveView();
            if (view == null || _levelsCache.Handles.CurrentPage == null) return 0;

            try
            {
                return (int)_levelsCache.Handles.CurrentPage.GetValue(view);
            }
            catch { return 0; }
        }

        private int GetPagesCount()
        {
            var view = GetActiveView();
            if (view == null || _levelsCache.Handles.PagesCount == null) return 1;

            try
            {
                return (int)_levelsCache.Handles.PagesCount.GetValue(view);
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
            if (view == null || _levelsCache.Handles.Pages == null || _levelsCache.Handles.CurrentPage == null) return;

            try
            {
                var pages = _levelsCache.Handles.Pages.GetValue(view) as IList;
                if (pages == null || pages.Count == 0) return;

                int currentPage = (int)_levelsCache.Handles.CurrentPage.GetValue(view);
                int targetLevel = _currentLevelIndex < _levelData.Count
                    ? _levelData[_currentLevelIndex].LevelNumber
                    : _currentLevelIndex + 1;

                // Find which page contains this level
                for (int i = 0; i < pages.Count; i++)
                {
                    var page = pages[i];
                    if (page == null) continue;

                    int start = 0, end = 0;
                    if (_levelsCache.Handles.PageLevelStart != null)
                        start = (int)_levelsCache.Handles.PageLevelStart.GetValue(page);
                    if (_levelsCache.Handles.PageLevelEnd != null)
                        end = (int)_levelsCache.Handles.PageLevelEnd.GetValue(page);

                    if (targetLevel >= start && targetLevel <= end)
                    {
                        if (i != currentPage)
                        {
                            _levelsCache.Handles.CurrentPage.SetValue(view, i);
                            _announcer.Announce(Strings.MasteryPage(i + 1, pages.Count));
                        }
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Msg("Mastery", $"Error syncing page: {ex.Message}");
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
            if (_levelsCache.Handles.BackButton != null && _controller != null)
            {
                try
                {
                    var backBtn = _levelsCache.Handles.BackButton.GetValue(_controller) as MonoBehaviour;
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
