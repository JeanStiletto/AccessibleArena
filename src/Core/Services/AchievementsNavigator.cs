using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using SceneNames = AccessibleArena.Core.Constants.SceneNames;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for the MTGA Achievements screen.
    /// Multi-level navigation:
    ///   Level 0 (Overview) - Summary achievements (tracked, up next) + set tabs
    ///   Level 1 (Groups)   - Achievement groups within a selected set tab
    ///   Level 2 (Achievements) - Individual achievements within a selected group
    /// </summary>
    public class AchievementsNavigator : BaseNavigator
    {
        #region Constants

        private const int AchievementsPriority = 57;

        #endregion

        #region Navigator Identity

        public override string NavigatorId => "Achievements";
        public override string ScreenName => Strings.ScreenAchievements;
        public override int Priority => AchievementsPriority;
        protected override bool SupportsCardNavigation => false;
        protected override bool AcceptSpaceKey => true;

        #endregion

        #region Navigation Levels & Entries

        private enum NavigationLevel
        {
            Overview,
            Groups,
            Achievements
        }

        private enum OverviewEntryType
        {
            SectionLabel,
            Achievement,
            SetTab
        }

        private struct OverviewEntry
        {
            public string Label;
            public GameObject GameObject;
            public OverviewEntryType Type;
            // Achievement-specific (only for Type == Achievement)
            public bool IsClaimable;
            public bool IsFavorite;
            public int ActionCount;
            public int ClaimActionIndex;
            public int TrackActionIndex;
        }

        private struct GroupEntry
        {
            public string Label;
            public GameObject GameObject;
            public string Title;
        }

        private struct AchievementItem
        {
            public string Label;
            public GameObject GameObject;
            public bool IsClaimable;
            public bool IsFavorite;
            public int ActionCount;
            public int ClaimActionIndex;
            public int TrackActionIndex;
        }

        #endregion

        #region State

        private MonoBehaviour _controller;
        private string _currentScene;

        // Navigation level
        private NavigationLevel _navLevel = NavigationLevel.Overview;
        private NavigationLevel? _pendingLevel; // Level to transition to after rescan

        // Per-level entries
        private readonly List<OverviewEntry> _overviewEntries = new List<OverviewEntry>();
        private readonly List<GroupEntry> _groupEntries = new List<GroupEntry>();
        private readonly List<AchievementItem> _achievementItems = new List<AchievementItem>();

        // Per-level indices
        private int _overviewIndex;
        private int _groupIndex;
        private int _achievementIndex;

        // Saved indices for back navigation
        private int _savedOverviewIndex;
        private int _savedGroupIndex;

        // Context for level transitions
        private string _selectedTabName;
        private string _selectedGroupName;
        private GameObject _selectedGroupObject;

        // Sub-action state (for achievement entries at Overview and Achievements levels)
        private int _actionSubIndex;
        private float _rescanTimer;

        #endregion

        #region Reflection Cache

        private sealed class AchievementsHandles
        {
            // AchievementsContentController
            public PropertyInfo IsOpen;

            // AchievementCard
            public Type AchievementCardType;
            public FieldInfo AchievementData;
            public FieldInfo FavoriteToggle;

            // IClientAchievement
            public PropertyInfo Title;
            public PropertyInfo Description;
            public PropertyInfo CurrentCount;
            public PropertyInfo MaxCount;
            public PropertyInfo IsCompleted;
            public PropertyInfo IsClaimed;
            public PropertyInfo IsClaimable;
            public PropertyInfo IsFavorite;
            public MethodInfo SetFavorite;

            // AchievementGroupDisplay
            public Type GroupDisplayType;
            public FieldInfo AchievementGroup;

            // IClientAchievementGroup
            public PropertyInfo GroupTitle;
            public PropertyInfo GroupDescription;
            public PropertyInfo GroupCompletedCount;
            public PropertyInfo GroupTotalCount;
            public PropertyInfo GroupClaimableCount;

            // AchievementSetItem
            public Type SetItemType;
            public FieldInfo ClientSet;
            public FieldInfo CurrentlySelected;
            public MethodInfo SelectSet;

            // IClientAchievementSet
            public PropertyInfo SetTitle;

            // IAchievementManager
            public Type AchievementManagerType;
            public PropertyInfo FavoriteAchievements;
            public PropertyInfo UpNextAchievements;
        }

        private static readonly ReflectionCache<AchievementsHandles> _achievementsCache = new ReflectionCache<AchievementsHandles>(
            builder: t =>
            {
                var h = new AchievementsHandles();
                h.IsOpen = t.GetProperty("IsOpen", AllInstanceFlags | BindingFlags.FlattenHierarchy);

                h.AchievementCardType = FindType("AchievementCard");
                if (h.AchievementCardType != null)
                {
                    h.AchievementData = h.AchievementCardType.GetField("_achievementData", AllInstanceFlags);
                    h.FavoriteToggle = h.AchievementCardType.GetField("_favoriteToggle", AllInstanceFlags);
                }

                var achievementInterface = FindType("IClientAchievement");
                if (achievementInterface != null)
                {
                    h.Title = achievementInterface.GetProperty("Title", PublicInstance);
                    h.Description = achievementInterface.GetProperty("Description", PublicInstance);
                    h.CurrentCount = achievementInterface.GetProperty("CurrentCount", PublicInstance);
                    h.MaxCount = achievementInterface.GetProperty("MaxCount", PublicInstance);
                    h.IsCompleted = achievementInterface.GetProperty("IsCompleted", PublicInstance);
                    h.IsClaimed = achievementInterface.GetProperty("IsClaimed", PublicInstance);
                    h.IsClaimable = achievementInterface.GetProperty("IsClaimable", PublicInstance);
                    h.IsFavorite = achievementInterface.GetProperty("IsFavorite", PublicInstance);
                    h.SetFavorite = achievementInterface.GetMethod("SetFavorite", PublicInstance);
                }

                h.GroupDisplayType = FindType("AchievementGroupDisplay");
                if (h.GroupDisplayType != null)
                    h.AchievementGroup = h.GroupDisplayType.GetField("_achievementGroup", AllInstanceFlags);

                var groupInterface = FindType("IClientAchievementGroup");
                if (groupInterface != null)
                {
                    h.GroupTitle = groupInterface.GetProperty("Title", PublicInstance);
                    h.GroupDescription = groupInterface.GetProperty("Description", PublicInstance);
                    h.GroupCompletedCount = groupInterface.GetProperty("CompletedAchievementCount", PublicInstance);
                    h.GroupTotalCount = groupInterface.GetProperty("TotalAchievementCount", PublicInstance);
                    h.GroupClaimableCount = groupInterface.GetProperty("ClaimableAchievementCount", PublicInstance);
                }

                h.SetItemType = FindType("AchievementSetItem");
                if (h.SetItemType != null)
                {
                    h.ClientSet = h.SetItemType.GetField("_clientAchievementSet", AllInstanceFlags);
                    h.CurrentlySelected = h.SetItemType.GetField("_currentlySelected", BindingFlags.Static | BindingFlags.NonPublic);
                    h.SelectSet = h.SetItemType.GetMethod("SelectSet", PublicInstance);
                }

                var setInterface = FindType("IClientAchievementSet");
                if (setInterface != null)
                    h.SetTitle = setInterface.GetProperty("Title", PublicInstance);

                h.AchievementManagerType = FindType("IAchievementManager");
                if (h.AchievementManagerType != null)
                {
                    h.FavoriteAchievements = h.AchievementManagerType.GetProperty("FavoriteAchievements", PublicInstance);
                    h.UpNextAchievements = h.AchievementManagerType.GetProperty("UpNextAchievements", PublicInstance);
                }

                return h;
            },
            validator: h => h.AchievementCardType != null && h.AchievementData != null
                         && h.Title != null && h.GroupDisplayType != null
                         && h.SetItemType != null,
            logTag: "Achievements",
            logSubject: "AchievementsContentController");

        #endregion

        #region Constructor

        public AchievementsNavigator(IAnnouncementService announcer) : base(announcer)
        {
        }

        #endregion

        #region Scene Tracking

        public override void OnSceneChanged(string sceneName)
        {
            _currentScene = sceneName;
            base.OnSceneChanged(sceneName);
        }

        #endregion

        #region Screen Detection

        protected override bool DetectScreen()
        {
            if (_currentScene != SceneNames.Achievements)
                return false;

            var controller = FindController();
            if (controller == null) return false;

            EnsureReflectionCached();

            if (_achievementsCache.Handles.IsOpen != null)
            {
                try
                {
                    if (!(bool)_achievementsCache.Handles.IsOpen.GetValue(controller))
                        return false;
                }
                catch { return false; }
            }

            _controller = controller;
            return true;
        }

        private MonoBehaviour FindController()
        {
            if (_controller != null && _controller.gameObject != null && _controller.gameObject.activeInHierarchy)
                return _controller;

            _controller = null;

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == T.AchievementsContentController)
                    return mb;
            }

            return null;
        }

        #endregion

        #region Reflection Caching

        private void EnsureReflectionCached()
        {
            var controllerType = FindType(T.AchievementsContentController);
            if (controllerType != null)
                _achievementsCache.EnsureInitialized(controllerType);
        }

        #endregion

        #region Element Discovery

        protected override void DiscoverElements()
        {
            _overviewEntries.Clear();
            _navLevel = NavigationLevel.Overview;
            _pendingLevel = null;

            DiscoverOverview();

            // Add a placeholder for base class (needs at least 1 element to activate)
            if (_overviewEntries.Count > 0)
                AddElement(_controller.gameObject, "Achievements");

            Log.Msg("{NavigatorId}", $"Discovered {_overviewEntries.Count} overview entries");
        }

        private void DiscoverOverview()
        {
            // Part 1: Set tabs (excluding Summary tab)
            DiscoverSetTabs();

            // Part 2: Summary achievements (tracked + up next) from IAchievementManager
            DiscoverSummaryAchievements();
        }

        private void DiscoverSummaryAchievements()
        {
            var managerType = FindType("IAchievementManager");
            if (managerType == null || _achievementsCache.Handles.AchievementManagerType == null) return;

            object manager = null;
            try
            {
                var pantryType = FindType("Pantry");
                var getMethod = pantryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Get" && m.IsGenericMethodDefinition);
                if (getMethod != null)
                    manager = getMethod.MakeGenericMethod(_achievementsCache.Handles.AchievementManagerType).Invoke(null, null);
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"Pantry.Get<IAchievementManager> failed: {ex.InnerException?.Message ?? ex.Message}");
                return;
            }

            if (manager == null) return;

            AddSummarySection(Strings.AchievementSectionTracked, _achievementsCache.Handles.FavoriteAchievements, manager);
            AddSummarySection(Strings.AchievementSectionUpNext, _achievementsCache.Handles.UpNextAchievements, manager);
        }

        private void AddSummarySection(string sectionName, PropertyInfo collectionProp, object manager)
        {
            if (collectionProp == null) return;

            System.Collections.IEnumerable collection = null;
            try { collection = collectionProp.GetValue(manager) as System.Collections.IEnumerable; }
            catch { return; }

            if (collection == null) return;

            bool any = false;
            foreach (var item in collection)
            {
                if (!any)
                {
                    _overviewEntries.Add(new OverviewEntry
                    {
                        Label = $"--- {sectionName} ---",
                        GameObject = _controller.gameObject,
                        Type = OverviewEntryType.SectionLabel
                    });
                    any = true;
                }

                string title       = StripRichText(SafeGetString(_achievementsCache.Handles.Title, item) ?? "Unknown");
                string description = SafeGetString(_achievementsCache.Handles.Description, item) ?? "";
                int current        = SafeGetInt(_achievementsCache.Handles.CurrentCount, item);
                int max            = SafeGetInt(_achievementsCache.Handles.MaxCount, item);
                bool isCompleted   = SafeGetBool(_achievementsCache.Handles.IsCompleted, item);
                bool isClaimed     = SafeGetBool(_achievementsCache.Handles.IsClaimed, item);
                bool isClaimable   = SafeGetBool(_achievementsCache.Handles.IsClaimable, item);
                bool isFavorite    = SafeGetBool(_achievementsCache.Handles.IsFavorite, item);

                string status = isClaimed ? Strings.AchievementClaimed
                              : isClaimable ? Strings.AchievementReadyToClaim
                              : isCompleted ? Strings.AchievementCompleted
                              : $"{current}/{max}";

                string label = Strings.AchievementEntry(title, description, status, isFavorite);

                int actionIdx  = 0;
                int claimIdx   = isClaimable ? ++actionIdx : 0;
                int trackIdx   = ++actionIdx;

                var cardGo = FindAchievementCardByTitle(title);

                _overviewEntries.Add(new OverviewEntry
                {
                    Label         = label,
                    GameObject    = cardGo ?? _controller.gameObject,
                    Type          = OverviewEntryType.Achievement,
                    IsClaimable   = isClaimable,
                    IsFavorite    = isFavorite,
                    ActionCount   = cardGo != null ? actionIdx : 0,
                    ClaimActionIndex = cardGo != null ? claimIdx : 0,
                    TrackActionIndex = cardGo != null ? trackIdx : 0
                });
            }

            if (!any)
            {
                _overviewEntries.Add(new OverviewEntry
                {
                    Label = $"--- {sectionName}: {Strings.AchievementSectionEmpty} ---",
                    GameObject = _controller.gameObject,
                    Type = OverviewEntryType.SectionLabel
                });
            }
        }

        private void DiscoverSetTabs()
        {
            if (_achievementsCache.Handles.SetItemType == null || _achievementsCache.Handles.ClientSet == null) return;

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType() != _achievementsCache.Handles.SetItemType) continue;

                var clientSet = _achievementsCache.Handles.ClientSet.GetValue(mb);
                if (clientSet == null) continue; // Skip Summary tab

                string title = StripRichText(SafeGetString(_achievementsCache.Handles.SetTitle, clientSet) ?? "Unknown Set");

                _overviewEntries.Add(new OverviewEntry
                {
                    Label = title,
                    GameObject = mb.gameObject,
                    Type = OverviewEntryType.SetTab
                });
            }
        }

        private void DiscoverGroups()
        {
            if (_achievementsCache.Handles.GroupDisplayType == null)
            {
                Log.Warn("{NavigatorId}", $"AchievementGroupDisplay type not found");
                return;
            }

            var groupDisplays = new List<MonoBehaviour>();
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType() == _achievementsCache.Handles.GroupDisplayType)
                    groupDisplays.Add(mb);
            }

            Log.Msg("{NavigatorId}", $"Found {groupDisplays.Count} group displays");

            foreach (var groupDisplay in groupDisplays)
            {
                var groupData = _achievementsCache.Handles.AchievementGroup?.GetValue(groupDisplay);
                if (groupData == null) continue;

                string groupTitle = StripRichText(SafeGetString(_achievementsCache.Handles.GroupTitle, groupData) ?? "Unknown Group");
                string groupDesc = StripRichText(SafeGetString(_achievementsCache.Handles.GroupDescription, groupData) ?? "");
                int completed = SafeGetInt(_achievementsCache.Handles.GroupCompletedCount, groupData);
                int total = SafeGetInt(_achievementsCache.Handles.GroupTotalCount, groupData);
                int claimable = SafeGetInt(_achievementsCache.Handles.GroupClaimableCount, groupData);

                _groupEntries.Add(new GroupEntry
                {
                    Label = Strings.AchievementGroup(groupTitle, groupDesc, completed, total, claimable),
                    GameObject = groupDisplay.gameObject,
                    Title = groupTitle
                });
            }
        }

        private void DiscoverAchievementsInGroup(GameObject groupObject)
        {
            if (_achievementsCache.Handles.AchievementCardType == null || _achievementsCache.Handles.AchievementData == null || groupObject == null) return;

            var cards = new List<MonoBehaviour>();
            foreach (var mb in groupObject.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb == null) continue;
                if (mb.GetType() == _achievementsCache.Handles.AchievementCardType)
                    cards.Add(mb);
            }

            foreach (var card in cards)
            {
                var achievementData = _achievementsCache.Handles.AchievementData.GetValue(card);
                if (achievementData == null) continue;

                string title = StripRichText(SafeGetString(_achievementsCache.Handles.Title, achievementData) ?? "Unknown");
                string description = SafeGetString(_achievementsCache.Handles.Description, achievementData) ?? "";
                int current = SafeGetInt(_achievementsCache.Handles.CurrentCount, achievementData);
                int max = SafeGetInt(_achievementsCache.Handles.MaxCount, achievementData);
                bool isCompleted = SafeGetBool(_achievementsCache.Handles.IsCompleted, achievementData);
                bool isClaimed = SafeGetBool(_achievementsCache.Handles.IsClaimed, achievementData);
                bool isClaimable = SafeGetBool(_achievementsCache.Handles.IsClaimable, achievementData);
                bool isFavorite = SafeGetBool(_achievementsCache.Handles.IsFavorite, achievementData);

                string status = isClaimed ? Strings.AchievementClaimed
                              : isClaimable ? Strings.AchievementReadyToClaim
                              : isCompleted ? Strings.AchievementCompleted
                              : $"{current}/{max}";

                string label = Strings.AchievementEntry(title, description, status, isFavorite);

                int actionIdx = 0;
                int claimIdx = isClaimable ? ++actionIdx : 0;
                int trackIdx = ++actionIdx;

                _achievementItems.Add(new AchievementItem
                {
                    Label = label,
                    GameObject = card.gameObject,
                    IsClaimable = isClaimable,
                    IsFavorite = isFavorite,
                    ActionCount = actionIdx,
                    ClaimActionIndex = claimIdx,
                    TrackActionIndex = trackIdx
                });
            }
        }

        private GameObject FindAchievementCardByTitle(string strippedTitle)
        {
            if (_achievementsCache.Handles.AchievementCardType == null || _achievementsCache.Handles.AchievementData == null) return null;
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType() != _achievementsCache.Handles.AchievementCardType) continue;
                var data = _achievementsCache.Handles.AchievementData.GetValue(mb);
                if (data == null) continue;
                if (StripRichText(SafeGetString(_achievementsCache.Handles.Title, data) ?? "") == strippedTitle)
                    return mb.gameObject;
            }
            return null;
        }

        #endregion

        #region Custom Navigation

        protected override bool HandleCustomInput()
        {
            // Block input during pending level transitions
            if (_pendingLevel != null)
                return true;

            switch (_navLevel)
            {
                case NavigationLevel.Overview:
                    return HandleOverviewInput();
                case NavigationLevel.Groups:
                    return HandleGroupsInput();
                case NavigationLevel.Achievements:
                    return HandleAchievementsLevelInput();
            }
            return false;
        }

        #endregion

        #region Overview Input (Level 0)

        private bool HandleOverviewInput()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow)) { MoveOverview(-1); return true; }
            if (Input.GetKeyDown(KeyCode.DownArrow)) { MoveOverview(1); return true; }

            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                MoveOverview(shift ? -1 : 1);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Home))
            {
                if (_overviewEntries.Count > 0)
                {
                    _overviewIndex = 0;
                    _actionSubIndex = 0;
                    AnnounceCurrentOverview();
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                if (_overviewEntries.Count > 0)
                {
                    _overviewIndex = _overviewEntries.Count - 1;
                    _actionSubIndex = 0;
                    AnnounceCurrentOverview();
                }
                return true;
            }

            bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            bool space = InputManager.GetKeyDownAndConsume(KeyCode.Space);
            if (enter || space)
            {
                ActivateOverviewEntry();
                return true;
            }

            // Left/Right: sub-action cycling for achievement entries
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (_overviewIndex >= 0 && _overviewIndex < _overviewEntries.Count)
                {
                    var entry = _overviewEntries[_overviewIndex];
                    if (entry.Type == OverviewEntryType.Achievement && entry.ActionCount > 0)
                    {
                        CycleSubAction(Input.GetKeyDown(KeyCode.RightArrow),
                            entry.ActionCount, entry.ClaimActionIndex, entry.TrackActionIndex,
                            entry.IsFavorite, entry.Label);
                    }
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                NavigateToHome();
                return true;
            }

            return false;
        }

        private void MoveOverview(int direction)
        {
            if (_overviewEntries.Count == 0) return;
            int newIndex = _overviewIndex + direction;
            if (newIndex < 0) { _announcer.AnnounceVerbose(Strings.BeginningOfList); return; }
            if (newIndex >= _overviewEntries.Count) { _announcer.AnnounceVerbose(Strings.EndOfList); return; }
            _overviewIndex = newIndex;
            _actionSubIndex = 0;
            AnnounceCurrentOverview();
        }

        private void ActivateOverviewEntry()
        {
            if (_overviewIndex < 0 || _overviewIndex >= _overviewEntries.Count) return;
            var entry = _overviewEntries[_overviewIndex];

            switch (entry.Type)
            {
                case OverviewEntryType.SetTab:
                    EnterSetTab();
                    break;
                case OverviewEntryType.Achievement:
                    ActivateAchievementAction(
                        entry.ClaimActionIndex, entry.TrackActionIndex,
                        entry.Label, entry.GameObject);
                    break;
                case OverviewEntryType.SectionLabel:
                    _announcer.AnnounceInterrupt(entry.Label);
                    break;
            }
        }

        #endregion

        #region Groups Input (Level 1)

        private bool HandleGroupsInput()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow)) { MoveGroup(-1); return true; }
            if (Input.GetKeyDown(KeyCode.DownArrow)) { MoveGroup(1); return true; }

            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                MoveGroup(shift ? -1 : 1);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Home))
            {
                if (_groupEntries.Count > 0) { _groupIndex = 0; AnnounceCurrentGroup(); }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                if (_groupEntries.Count > 0) { _groupIndex = _groupEntries.Count - 1; AnnounceCurrentGroup(); }
                return true;
            }

            bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            bool space = InputManager.GetKeyDownAndConsume(KeyCode.Space);
            if (enter || space)
            {
                EnterGroup();
                return true;
            }

            // Consume Left/Right (no action at group level)
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
                return true;

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                ReturnToOverview();
                return true;
            }

            return false;
        }

        private void MoveGroup(int direction)
        {
            if (_groupEntries.Count == 0) return;
            int newIndex = _groupIndex + direction;
            if (newIndex < 0) { _announcer.AnnounceVerbose(Strings.BeginningOfList); return; }
            if (newIndex >= _groupEntries.Count) { _announcer.AnnounceVerbose(Strings.EndOfList); return; }
            _groupIndex = newIndex;
            AnnounceCurrentGroup();
        }

        #endregion

        #region Achievements Input (Level 2)

        private bool HandleAchievementsLevelInput()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow)) { MoveAchievement(-1); return true; }
            if (Input.GetKeyDown(KeyCode.DownArrow)) { MoveAchievement(1); return true; }

            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                MoveAchievement(shift ? -1 : 1);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Home))
            {
                if (_achievementItems.Count > 0) { _achievementIndex = 0; _actionSubIndex = 0; AnnounceCurrentAchievement(); }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                if (_achievementItems.Count > 0) { _achievementIndex = _achievementItems.Count - 1; _actionSubIndex = 0; AnnounceCurrentAchievement(); }
                return true;
            }

            bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            bool space = InputManager.GetKeyDownAndConsume(KeyCode.Space);
            if (enter || space)
            {
                if (_achievementIndex >= 0 && _achievementIndex < _achievementItems.Count)
                {
                    var item = _achievementItems[_achievementIndex];
                    ActivateAchievementAction(item.ClaimActionIndex, item.TrackActionIndex,
                        item.Label, item.GameObject);
                }
                return true;
            }

            // Left/Right: sub-action cycling
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (_achievementIndex >= 0 && _achievementIndex < _achievementItems.Count)
                {
                    var item = _achievementItems[_achievementIndex];
                    if (item.ActionCount > 0)
                    {
                        CycleSubAction(Input.GetKeyDown(KeyCode.RightArrow),
                            item.ActionCount, item.ClaimActionIndex, item.TrackActionIndex,
                            item.IsFavorite, item.Label);
                    }
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                ReturnToGroups();
                return true;
            }

            return false;
        }

        private void MoveAchievement(int direction)
        {
            if (_achievementItems.Count == 0) return;
            int newIndex = _achievementIndex + direction;
            if (newIndex < 0) { _announcer.AnnounceVerbose(Strings.BeginningOfList); return; }
            if (newIndex >= _achievementItems.Count) { _announcer.AnnounceVerbose(Strings.EndOfList); return; }
            _achievementIndex = newIndex;
            _actionSubIndex = 0;
            AnnounceCurrentAchievement();
        }

        #endregion

        #region Shared Achievement Actions

        private void CycleSubAction(bool forward, int actionCount, int claimIdx, int trackIdx,
            bool isFavorite, string label)
        {
            int newIdx = _actionSubIndex + (forward ? 1 : -1);
            if (newIdx < 0) { _announcer.AnnounceVerbose(Strings.BeginningOfList); return; }
            if (newIdx > actionCount) { _announcer.AnnounceVerbose(Strings.EndOfList); return; }
            _actionSubIndex = newIdx;
            _announcer.AnnounceInterrupt(FormatActionAnnouncement(label, isFavorite, actionCount, claimIdx, trackIdx));
        }

        private void ActivateAchievementAction(int claimIdx, int trackIdx, string label, GameObject go)
        {
            if (_actionSubIndex == 0)
            {
                _announcer.AnnounceInterrupt(label);
            }
            else if (_actionSubIndex == claimIdx)
            {
                ActivateCollectButton(go);
            }
            else if (_actionSubIndex == trackIdx)
            {
                ToggleTracking(go);
            }
        }

        private string FormatActionAnnouncement(string label, bool isFavorite, int actionCount, int claimIdx, int trackIdx)
        {
            if (_actionSubIndex == 0)
                return label;

            string actionLabel;
            if (_actionSubIndex == claimIdx)
                actionLabel = Strings.AchievementActionClaim;
            else if (_actionSubIndex == trackIdx)
                actionLabel = isFavorite ? Strings.AchievementActionUntrack : Strings.AchievementActionTrack;
            else
                actionLabel = Strings.AchievementActionGeneric;

            return Strings.AchievementActionPosition(actionLabel, _actionSubIndex, actionCount);
        }

        #endregion

        #region Level Transitions

        private void EnterSetTab()
        {
            if (_overviewIndex < 0 || _overviewIndex >= _overviewEntries.Count) return;
            var entry = _overviewEntries[_overviewIndex];
            if (entry.Type != OverviewEntryType.SetTab) return;

            var setItem = entry.GameObject.GetComponent(_achievementsCache.Handles.SetItemType) as MonoBehaviour;
            if (setItem == null) return;

            _selectedTabName = entry.Label;
            _savedOverviewIndex = _overviewIndex;

            // Check if this tab is already selected in the game
            var currentlySelected = _achievementsCache.Handles.CurrentlySelected?.GetValue(null) as UnityEngine.Object;
            bool alreadyActive = currentlySelected != null && setItem.Equals(currentlySelected);

            if (!alreadyActive)
            {
                try
                {
                    _achievementsCache.Handles.SelectSet.Invoke(setItem, new object[] { true });
                }
                catch (Exception ex)
                {
                    Log.Warn("{NavigatorId}", $"SelectSet error: {ex.InnerException?.Message ?? ex.Message}");
                    return;
                }
            }

            // Try immediate discovery — content may already be loaded
            _groupEntries.Clear();
            DiscoverGroups();

            if (_groupEntries.Count > 0)
            {
                TransitionToGroupsImmediate();
            }
            else
            {
                // Content not yet loaded, poll after short delay
                _pendingLevel = NavigationLevel.Groups;
                ScheduleRescan(0.5f);
            }
        }

        private void EnterGroup()
        {
            if (_groupIndex < 0 || _groupIndex >= _groupEntries.Count) return;
            var entry = _groupEntries[_groupIndex];

            _selectedGroupName = entry.Title;
            _selectedGroupObject = entry.GameObject;
            _savedGroupIndex = _groupIndex;

            // Open foldout and wait for cards to appear
            _pendingLevel = NavigationLevel.Achievements;
            ToggleFoldout(entry.GameObject);
            ScheduleRescan(0.8f);
        }

        private void TransitionToGroups()
        {
            _groupEntries.Clear();
            DiscoverGroups();

            if (_groupEntries.Count == 0)
            {
                _announcer.AnnounceInterrupt(Strings.NoItemsAvailable(_selectedTabName));
                return;
            }

            TransitionToGroupsImmediate();
        }

        /// <summary>
        /// Enters Level 1. Assumes _groupEntries is already populated and non-empty.
        /// </summary>
        private void TransitionToGroupsImmediate()
        {
            _navLevel = NavigationLevel.Groups;
            _groupIndex = 0;
            _actionSubIndex = 0;

            string msg = Strings.AchievementsGroups(_selectedTabName, _groupEntries.Count);
            msg += " " + FormatGroupAnnouncement(0);
            _announcer.AnnounceInterrupt(msg);
        }

        private void TransitionToAchievements()
        {
            _achievementItems.Clear();
            DiscoverAchievementsInGroup(_selectedGroupObject);

            if (_achievementItems.Count == 0)
            {
                _announcer.AnnounceInterrupt(Strings.NoItemsAvailable(_selectedGroupName));
                _pendingLevel = null;
                return;
            }

            _navLevel = NavigationLevel.Achievements;
            _achievementIndex = 0;
            _actionSubIndex = 0;

            string msg = Strings.AchievementsInGroup(_selectedGroupName, _achievementItems.Count);
            msg += " " + FormatAchievementAnnouncement(0);
            _announcer.AnnounceInterrupt(msg);
        }

        private void ReturnToOverview()
        {
            _navLevel = NavigationLevel.Overview;
            _groupEntries.Clear();

            // Re-discover overview for fresh data
            _overviewEntries.Clear();
            DiscoverOverview();

            // Restore position to the tab we came from
            _overviewIndex = FindOverviewIndexByLabel(_selectedTabName, OverviewEntryType.SetTab);
            if (_overviewIndex < 0)
                _overviewIndex = Math.Min(_savedOverviewIndex, Math.Max(0, _overviewEntries.Count - 1));
            _actionSubIndex = 0;

            int tabCount = _overviewEntries.Count(e => e.Type == OverviewEntryType.SetTab);
            string msg = Strings.TabsCount(tabCount);
            if (_overviewIndex >= 0 && _overviewIndex < _overviewEntries.Count)
                msg += " " + FormatOverviewAnnouncement(_overviewIndex);
            _announcer.AnnounceInterrupt(msg);
        }

        private void ReturnToGroups()
        {
            // Close the foldout we opened
            if (_selectedGroupObject != null)
                ToggleFoldout(_selectedGroupObject);

            _navLevel = NavigationLevel.Groups;
            _achievementItems.Clear();

            // Re-discover groups for fresh data
            _groupEntries.Clear();
            DiscoverGroups();

            _groupIndex = Math.Min(_savedGroupIndex, Math.Max(0, _groupEntries.Count - 1));
            _actionSubIndex = 0;

            string msg = Strings.AchievementsGroups(_selectedTabName, _groupEntries.Count);
            if (_groupIndex >= 0 && _groupIndex < _groupEntries.Count)
                msg += " " + FormatGroupAnnouncement(_groupIndex);
            _announcer.AnnounceInterrupt(msg);
        }

        private int FindOverviewIndexByLabel(string label, OverviewEntryType type)
        {
            for (int i = 0; i < _overviewEntries.Count; i++)
            {
                if (_overviewEntries[i].Type == type && _overviewEntries[i].Label == label)
                    return i;
            }
            return -1;
        }

        #endregion

        #region Game Actions

        private void ActivateCollectButton(GameObject cardObject)
        {
            if (_achievementsCache.Handles.AchievementCardType == null) return;

            var collectField = _achievementsCache.Handles.AchievementCardType.GetField("_collectButton", AllInstanceFlags);
            if (collectField == null) return;

            var card = cardObject.GetComponent(_achievementsCache.Handles.AchievementCardType) as MonoBehaviour;
            if (card == null) return;

            var collectButton = collectField.GetValue(card);
            if (collectButton == null) return;

            var buttonObj = (collectButton as MonoBehaviour)?.gameObject;
            if (buttonObj != null && buttonObj.activeInHierarchy)
            {
                UIActivator.Activate(buttonObj);
                _announcer.Announce(Strings.ActivatedBare);
                ScheduleRescan(0.5f);
            }
        }

        private void ToggleTracking(GameObject cardObject)
        {
            if (_achievementsCache.Handles.AchievementCardType == null || _achievementsCache.Handles.AchievementData == null || _achievementsCache.Handles.SetFavorite == null) return;

            var card = cardObject.GetComponent(_achievementsCache.Handles.AchievementCardType) as MonoBehaviour;
            if (card == null) return;

            var achievementData = _achievementsCache.Handles.AchievementData.GetValue(card);
            if (achievementData == null) return;

            bool current = SafeGetBool(_achievementsCache.Handles.IsFavorite, achievementData);
            bool newValue = !current;

            try
            {
                _achievementsCache.Handles.SetFavorite.Invoke(achievementData, new object[] { newValue });
                string announcement = newValue ? Strings.AchievementTracked : Strings.AchievementUntracked;
                _announcer.AnnounceInterrupt(announcement);
                ScheduleRescan(0.3f);
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"SetFavorite error: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        private void ToggleFoldout(GameObject groupObject)
        {
            foreach (var comp in groupObject.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                var toggleMethod = comp.GetType().GetMethod("ToggleFoldout", BindingFlags.Public | BindingFlags.Instance);
                if (toggleMethod != null)
                {
                    Log.Msg("{NavigatorId}", $"ToggleFoldout via {comp.GetType().FullName}");
                    try { toggleMethod.Invoke(comp, null); }
                    catch (Exception ex) { Log.Warn("{NavigatorId}", $"ToggleFoldout error: {ex.InnerException?.Message ?? ex.Message}"); }
                    return;
                }
            }
            Log.Warn("{NavigatorId}", $"No ToggleFoldout found on {groupObject.name}");
        }

        private void ScheduleRescan(float delay)
        {
            _rescanTimer = delay;
        }

        #endregion

        #region Announcements

        public override string GetTutorialHint() => LocaleManager.Instance.Get("AchievementsHint");

        protected override string GetActivationAnnouncement()
        {
            int tabCount = _overviewEntries.Count(e => e.Type == OverviewEntryType.SetTab);
            string core = Strings.AchievementsActivation(tabCount);
            return Strings.WithHint(core, "AchievementsHint");
        }

        protected override string GetElementAnnouncement(int index)
        {
            // Not used directly — we handle announcements per-level
            return "";
        }

        private void AnnounceCurrentOverview()
        {
            if (_overviewIndex < 0 || _overviewIndex >= _overviewEntries.Count) return;
            _announcer.AnnounceInterrupt(FormatOverviewAnnouncement(_overviewIndex));
        }

        private void AnnounceCurrentGroup()
        {
            if (_groupIndex < 0 || _groupIndex >= _groupEntries.Count) return;
            _announcer.AnnounceInterrupt(FormatGroupAnnouncement(_groupIndex));
        }

        private void AnnounceCurrentAchievement()
        {
            if (_achievementIndex < 0 || _achievementIndex >= _achievementItems.Count) return;
            _announcer.AnnounceInterrupt(FormatAchievementAnnouncement(_achievementIndex));
        }

        private string FormatOverviewAnnouncement(int index)
        {
            if (index < 0 || index >= _overviewEntries.Count) return "";
            var entry = _overviewEntries[index];

            if (entry.Type == OverviewEntryType.SetTab)
            {
                int tabCount = _overviewEntries.Count(e => e.Type == OverviewEntryType.SetTab);
                int tabPos = 0;
                for (int i = 0; i <= index; i++)
                {
                    if (_overviewEntries[i].Type == OverviewEntryType.SetTab)
                        tabPos++;
                }
                return Strings.TabPositionOf(tabPos, tabCount, entry.Label);
            }

            return entry.Label;
        }

        private string FormatGroupAnnouncement(int index)
        {
            if (index < 0 || index >= _groupEntries.Count) return "";
            return Strings.ItemPositionOf(index + 1, _groupEntries.Count, _groupEntries[index].Label);
        }

        private string FormatAchievementAnnouncement(int index)
        {
            if (index < 0 || index >= _achievementItems.Count) return "";
            return Strings.ItemPositionOf(index + 1, _achievementItems.Count, _achievementItems[index].Label);
        }

        #endregion

        #region Update Override

        public override void Update()
        {
            if (!_isActive)
            {
                base.Update();
                return;
            }

            // Handle pending rescan
            if (_rescanTimer > 0)
            {
                _rescanTimer -= Time.deltaTime;
                if (_rescanTimer <= 0)
                {
                    HandleRescanComplete();
                }
            }

            // Check if controller is still valid
            if (_controller == null || _controller.gameObject == null || !_controller.gameObject.activeInHierarchy)
            {
                Deactivate();
                return;
            }

            // Check IsOpen
            if (_achievementsCache.Handles.IsOpen != null)
            {
                try
                {
                    if (!(bool)_achievementsCache.Handles.IsOpen.GetValue(_controller))
                    {
                        Deactivate();
                        return;
                    }
                }
                catch
                {
                    Deactivate();
                    return;
                }
            }

            base.Update();
        }

        private void HandleRescanComplete()
        {
            if (_pendingLevel == NavigationLevel.Groups)
            {
                _pendingLevel = null;
                TransitionToGroups();
            }
            else if (_pendingLevel == NavigationLevel.Achievements)
            {
                _pendingLevel = null;
                TransitionToAchievements();
            }
            else
            {
                // Regular rescan: refresh current level, preserve position
                RefreshCurrentLevel();
            }
        }

        private void RefreshCurrentLevel()
        {
            switch (_navLevel)
            {
                case NavigationLevel.Overview:
                    int savedOv = _overviewIndex;
                    _overviewEntries.Clear();
                    DiscoverOverview();
                    _overviewIndex = Math.Min(savedOv, Math.Max(0, _overviewEntries.Count - 1));
                    if (_overviewIndex >= 0 && _overviewIndex < _overviewEntries.Count)
                        AnnounceCurrentOverview();
                    break;

                case NavigationLevel.Groups:
                    int savedGr = _groupIndex;
                    _groupEntries.Clear();
                    DiscoverGroups();
                    _groupIndex = Math.Min(savedGr, Math.Max(0, _groupEntries.Count - 1));
                    if (_groupIndex >= 0 && _groupIndex < _groupEntries.Count)
                        AnnounceCurrentGroup();
                    break;

                case NavigationLevel.Achievements:
                    int savedAch = _achievementIndex;
                    _achievementItems.Clear();
                    DiscoverAchievementsInGroup(_selectedGroupObject);
                    _achievementIndex = Math.Min(savedAch, Math.Max(0, _achievementItems.Count - 1));
                    if (_achievementIndex >= 0 && _achievementIndex < _achievementItems.Count)
                        AnnounceCurrentAchievement();
                    break;
            }
        }

        protected override void OnDeactivating()
        {
            _controller = null;
            _overviewEntries.Clear();
            _groupEntries.Clear();
            _achievementItems.Clear();
            _rescanTimer = 0;
            _actionSubIndex = 0;
            _navLevel = NavigationLevel.Overview;
            _pendingLevel = null;
            _selectedTabName = null;
            _selectedGroupName = null;
            _selectedGroupObject = null;
        }

        #endregion

        #region Reflection Helpers

        private static string StripRichText(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return Regex.Replace(s, @"<[^>]+>", "").Trim();
        }

        private static string SafeGetString(PropertyInfo prop, object obj)
        {
            if (prop == null || obj == null) return null;
            try { return prop.GetValue(obj) as string; }
            catch { return null; }
        }

        private static int SafeGetInt(PropertyInfo prop, object obj)
        {
            if (prop == null || obj == null) return 0;
            try { return (int)prop.GetValue(obj); }
            catch { return 0; }
        }

        private static bool SafeGetBool(PropertyInfo prop, object obj)
        {
            if (prop == null || obj == null) return false;
            try { return (bool)prop.GetValue(obj); }
            catch { return false; }
        }

        #endregion
    }
}
