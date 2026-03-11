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

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for the MTGA Achievements screen.
    /// Reads achievement data (title, description, progress, status) from
    /// AchievementCard and AchievementGroupDisplay components via reflection,
    /// providing meaningful announcements instead of raw UI element text.
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

        #region State

        private MonoBehaviour _controller;
        private string _currentScene;

        // Achievement data extracted via reflection
        private readonly List<AchievementEntry> _achievementEntries = new List<AchievementEntry>();

        private struct AchievementEntry
        {
            public string Label;           // Full announcement text
            public GameObject GameObject;  // The AchievementCard or group header GameObject
            public EntryType Type;
            public bool IsClaimable;
            public bool IsFavorite;
            public int ActionCount;        // Total number of sub-actions available
            // Action indices (1-based; 0 = not applicable)
            public int ClaimActionIndex;
            public int TrackActionIndex;
        }

        private enum EntryType
        {
            SetHeader,
            GroupHeader,
            Achievement,
            SectionLabel    // Read-only summary section divider — re-announces on Enter, no other action
        }

        #endregion

        #region Reflection Cache

        private bool _reflectionInitialized;

        // AchievementsContentController
        private PropertyInfo _isOpenProp;

        // AchievementCard -> _achievementData (IClientAchievement)
        private Type _achievementCardType;
        private FieldInfo _achievementDataField;

        // IClientAchievement properties
        private PropertyInfo _titleProp;
        private PropertyInfo _descriptionProp;
        private PropertyInfo _currentCountProp;
        private PropertyInfo _maxCountProp;
        private PropertyInfo _isCompletedProp;
        private PropertyInfo _isClaimedProp;
        private PropertyInfo _isClaimableProp;
        private PropertyInfo _isFavoriteProp;

        // AchievementGroupDisplay -> _achievementGroup (IClientAchievementGroup)
        private Type _groupDisplayType;
        private FieldInfo _achievementGroupField;

        // IClientAchievementGroup properties
        private PropertyInfo _groupTitleProp;
        private PropertyInfo _groupCompletedCountProp;
        private PropertyInfo _groupTotalCountProp;
        private PropertyInfo _groupClaimableCountProp;

        // AchievementSetItem -> _clientAchievementSet (IClientAchievementSet)
        private Type _setItemType;
        private FieldInfo _clientSetField;
        private PropertyInfo _setTitleProp;

        // AchievementSetItem tab selection
        private FieldInfo _currentlySelectedField;
        private MethodInfo _selectSetMethod;

        // IClientAchievement.SetFavorite
        private MethodInfo _setFavoriteMethod;

        // IAchievementManager — for Summary tab
        private Type _achievementManagerType;
        private PropertyInfo _favoriteAchievementsProp;
        private PropertyInfo _upNextAchievementsProp;

        // Toggle for favorite/tracking
        private FieldInfo _favoriteToggleField;

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
            // The Achievements screen loads as scene "Achievements" with AchievementsContentController
            if (_currentScene != "Achievements")
                return false;

            var controller = FindController();
            if (controller == null) return false;

            EnsureReflectionCached();

            if (_isOpenProp != null)
            {
                try
                {
                    if (!(bool)_isOpenProp.GetValue(controller))
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
                if (mb.GetType().Name == "AchievementsContentController")
                    return mb;
            }

            return null;
        }

        #endregion

        #region Reflection Caching

        private void EnsureReflectionCached()
        {
            if (_reflectionInitialized) return;

            var flags = AllInstanceFlags;

            // AchievementsContentController inherits from NavContentController which has IsOpen
            _isOpenProp = FindType("AchievementsContentController")
                ?.GetProperty("IsOpen", flags | BindingFlags.FlattenHierarchy);

            // AchievementCard._achievementData
            _achievementCardType = FindType("AchievementCard");
            if (_achievementCardType != null)
            {
                _achievementDataField = _achievementCardType.GetField("_achievementData", flags);
                _favoriteToggleField = _achievementCardType.GetField("_favoriteToggle", flags);
            }

            // IClientAchievement properties (public interface)
            var achievementInterface = FindType("IClientAchievement");
            if (achievementInterface != null)
            {
                var pubFlags = BindingFlags.Public | BindingFlags.Instance;
                _titleProp = achievementInterface.GetProperty("Title", pubFlags);
                _descriptionProp = achievementInterface.GetProperty("Description", pubFlags);
                _currentCountProp = achievementInterface.GetProperty("CurrentCount", pubFlags);
                _maxCountProp = achievementInterface.GetProperty("MaxCount", pubFlags);
                _isCompletedProp = achievementInterface.GetProperty("IsCompleted", pubFlags);
                _isClaimedProp = achievementInterface.GetProperty("IsClaimed", pubFlags);
                _isClaimableProp = achievementInterface.GetProperty("IsClaimable", pubFlags);
                _isFavoriteProp = achievementInterface.GetProperty("IsFavorite", pubFlags);
                _setFavoriteMethod = achievementInterface.GetMethod("SetFavorite", pubFlags);
            }

            // AchievementGroupDisplay._achievementGroup
            _groupDisplayType = FindType("AchievementGroupDisplay");
            if (_groupDisplayType != null)
            {
                _achievementGroupField = _groupDisplayType.GetField("_achievementGroup", flags);
            }

            // IClientAchievementGroup properties
            var groupInterface = FindType("IClientAchievementGroup");
            if (groupInterface != null)
            {
                var pubFlags = BindingFlags.Public | BindingFlags.Instance;
                _groupTitleProp = groupInterface.GetProperty("Title", pubFlags);
                _groupCompletedCountProp = groupInterface.GetProperty("CompletedAchievementCount", pubFlags);
                _groupTotalCountProp = groupInterface.GetProperty("TotalAchievementCount", pubFlags);
                _groupClaimableCountProp = groupInterface.GetProperty("ClaimableAchievementCount", pubFlags);
            }

            // AchievementSetItem._clientAchievementSet
            _setItemType = FindType("AchievementSetItem");
            if (_setItemType != null)
            {
                _clientSetField = _setItemType.GetField("_clientAchievementSet", flags);
            }

            // IClientAchievementSet.Title
            var setInterface = FindType("IClientAchievementSet");
            if (setInterface != null)
            {
                _setTitleProp = setInterface.GetProperty("Title", BindingFlags.Public | BindingFlags.Instance);
            }

            // AchievementSetItem tab selection
            _currentlySelectedField = _setItemType?.GetField("_currentlySelected", BindingFlags.Static | BindingFlags.NonPublic);
            _selectSetMethod = _setItemType?.GetMethod("SelectSet", BindingFlags.Public | BindingFlags.Instance);

            // IAchievementManager — Summary tab data
            _achievementManagerType = FindType("IAchievementManager");
            if (_achievementManagerType != null)
            {
                var pubFlags = BindingFlags.Public | BindingFlags.Instance;
                _favoriteAchievementsProp = _achievementManagerType.GetProperty("FavoriteAchievements", pubFlags);
                _upNextAchievementsProp   = _achievementManagerType.GetProperty("UpNextAchievements", pubFlags);
            }

            _reflectionInitialized = true;
            MelonLogger.Msg($"[{NavigatorId}] Reflection cached: " +
                $"Card={_achievementCardType != null}, " +
                $"Group={_groupDisplayType != null}, " +
                $"Set={_setItemType != null}, " +
                $"AchievementData={_achievementDataField != null}");
        }

        #endregion

        #region Element Discovery

        protected override void DiscoverElements()
        {
            _achievementEntries.Clear();

            // Discover the active set (selected tab on the left blade)
            bool isSummary = DiscoverActiveSet();

            if (isSummary)
                DiscoverSummaryAchievements();
            else
                DiscoverGroupsAndAchievements();

            // Convert to navigable elements
            foreach (var entry in _achievementEntries)
            {
                AddElement(entry.GameObject, entry.Label);
            }

            MelonLogger.Msg($"[{NavigatorId}] Discovered {_achievementEntries.Count} entries");
        }

        // Returns true if the currently selected tab is Summary (clientSet == null)
        private bool DiscoverActiveSet()
        {
            if (_setItemType == null || _clientSetField == null) return false;

            var currentlySelected = _currentlySelectedField?.GetValue(null) as UnityEngine.Object;
            bool selectedIsSummary = false;

            var setItems = new List<MonoBehaviour>();
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType() == _setItemType)
                    setItems.Add(mb);
            }

            MelonLogger.Msg($"[{NavigatorId}] Found {setItems.Count} set tabs");

            foreach (var setItem in setItems)
            {
                var clientSet = _clientSetField.GetValue(setItem);
                bool isSummaryTab = clientSet == null;
                string title = isSummaryTab
                    ? "Summary"
                    : StripRichText(SafeGetString(_setTitleProp, clientSet) ?? "Unknown Set");

                bool isSelected = currentlySelected != null && setItem.Equals(currentlySelected);
                if (isSelected && isSummaryTab) selectedIsSummary = true;

                string label = isSelected ? $"Tab: {title} (selected)" : $"Tab: {title}";

                _achievementEntries.Add(new AchievementEntry
                {
                    Label = label,
                    GameObject = setItem.gameObject,
                    Type = EntryType.SetHeader,
                    IsClaimable = false
                });
            }

            return selectedIsSummary;
        }

        private void DiscoverSummaryAchievements()
        {
            // Read FavoriteAchievements and UpNextAchievements from IAchievementManager
            // via Pantry (service locator) rather than parsing the hub prefab UI.
            var managerType = FindType("IAchievementManager");
            if (managerType == null || _achievementManagerType == null) return;

            // Pantry.Get<IAchievementManager>()
            object manager = null;
            try
            {
                var pantryType = FindType("Pantry");
                var getMethod = pantryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Get" && m.IsGenericMethodDefinition);
                if (getMethod != null)
                    manager = getMethod.MakeGenericMethod(_achievementManagerType).Invoke(null, null);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[{NavigatorId}] Pantry.Get<IAchievementManager> failed: {ex.InnerException?.Message ?? ex.Message}");
                return;
            }

            if (manager == null) return;

            var pubFlags = BindingFlags.Public | BindingFlags.Instance;

            // Tracked (favorites) section
            AddSummarySection("Tracked", _favoriteAchievementsProp, manager);

            // Up Next section
            AddSummarySection("Up Next", _upNextAchievementsProp, manager);
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
                    // Section header as a read-only label (no GameObject needed; use controller as placeholder)
                    _achievementEntries.Add(new AchievementEntry
                    {
                        Label = $"--- {sectionName} ---",
                        GameObject = _controller.gameObject,
                        Type = EntryType.SectionLabel,
                        IsClaimable = false
                    });
                    any = true;
                }

                string title       = StripRichText(SafeGetString(_titleProp, item) ?? "Unknown");
                string description = SafeGetString(_descriptionProp, item) ?? "";
                int current        = SafeGetInt(_currentCountProp, item);
                int max            = SafeGetInt(_maxCountProp, item);
                bool isCompleted   = SafeGetBool(_isCompletedProp, item);
                bool isClaimed     = SafeGetBool(_isClaimedProp, item);
                bool isClaimable   = SafeGetBool(_isClaimableProp, item);
                bool isFavorite    = SafeGetBool(_isFavoriteProp, item);

                string status = isClaimed ? Strings.AchievementClaimed
                              : isClaimable ? Strings.AchievementReadyToClaim
                              : isCompleted ? Strings.AchievementCompleted
                              : $"{current}/{max}";

                string label = Strings.AchievementEntry(title, description, status, isFavorite);

                int actionIdx  = 0;
                int claimIdx   = isClaimable ? ++actionIdx : 0;
                int trackIdx   = ++actionIdx;

                // Summary achievements have no card GameObject — find by matching title in scene
                var cardGo = FindAchievementCardByTitle(title);

                _achievementEntries.Add(new AchievementEntry
                {
                    Label         = label,
                    GameObject    = cardGo ?? _controller.gameObject,
                    Type          = EntryType.Achievement,
                    IsClaimable   = isClaimable,
                    IsFavorite    = isFavorite,
                    ActionCount   = cardGo != null ? actionIdx : 0,
                    ClaimActionIndex = cardGo != null ? claimIdx : 0,
                    TrackActionIndex = cardGo != null ? trackIdx : 0
                });
            }

            if (!any)
            {
                _achievementEntries.Add(new AchievementEntry
                {
                    Label = $"--- {sectionName}: none ---",
                    GameObject = _controller.gameObject,
                    Type = EntryType.GroupHeader
                });
            }
        }

        private GameObject FindAchievementCardByTitle(string strippedTitle)
        {
            if (_achievementCardType == null || _achievementDataField == null) return null;
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType() != _achievementCardType) continue;
                var data = _achievementDataField.GetValue(mb);
                if (data == null) continue;
                if (StripRichText(SafeGetString(_titleProp, data) ?? "") == strippedTitle)
                    return mb.gameObject;
            }
            return null;
        }

        private void DiscoverGroupsAndAchievements()
        {
            if (_groupDisplayType == null)
            {
                MelonLogger.Warning($"[{NavigatorId}] AchievementGroupDisplay type not found");
                return;
            }

            // Find all AchievementGroupDisplay components
            var groupDisplays = new List<MonoBehaviour>();
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType() == _groupDisplayType)
                    groupDisplays.Add(mb);
            }

            MelonLogger.Msg($"[{NavigatorId}] Found {groupDisplays.Count} group displays");

            foreach (var groupDisplay in groupDisplays)
            {
                var groupData = _achievementGroupField?.GetValue(groupDisplay);
                if (groupData == null) continue;

                // Extract group info
                string groupTitle = StripRichText(SafeGetString(_groupTitleProp, groupData) ?? "Unknown Group");
                int completed = SafeGetInt(_groupCompletedCountProp, groupData);
                int total = SafeGetInt(_groupTotalCountProp, groupData);
                int claimable = SafeGetInt(_groupClaimableCountProp, groupData);

                string groupLabel = Strings.AchievementGroup(groupTitle, completed, total, claimable);

                _achievementEntries.Add(new AchievementEntry
                {
                    Label = groupLabel,
                    GameObject = groupDisplay.gameObject,
                    Type = EntryType.GroupHeader,
                    IsClaimable = false
                });

                // Find AchievementCard children within this group
                DiscoverCardsInGroup(groupDisplay.gameObject);
            }
        }

        private void DiscoverCardsInGroup(GameObject groupObject)
        {
            if (_achievementCardType == null || _achievementDataField == null) return;

            // Find all AchievementCard components that are children of this group
            var cards = new List<MonoBehaviour>();
            foreach (var mb in groupObject.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb == null) continue;
                if (mb.GetType() == _achievementCardType)
                    cards.Add(mb);
            }

            foreach (var card in cards)
            {
                var achievementData = _achievementDataField.GetValue(card);
                if (achievementData == null) continue;

                string title = StripRichText(SafeGetString(_titleProp, achievementData) ?? "Unknown");
                string description = SafeGetString(_descriptionProp, achievementData) ?? "";
                int current = SafeGetInt(_currentCountProp, achievementData);
                int max = SafeGetInt(_maxCountProp, achievementData);
                bool isCompleted = SafeGetBool(_isCompletedProp, achievementData);
                bool isClaimed = SafeGetBool(_isClaimedProp, achievementData);
                bool isClaimable = SafeGetBool(_isClaimableProp, achievementData);
                bool isFavorite = SafeGetBool(_isFavoriteProp, achievementData);

                string status;
                if (isClaimed)
                    status = Strings.AchievementClaimed;
                else if (isClaimable)
                    status = Strings.AchievementReadyToClaim;
                else if (isCompleted)
                    status = Strings.AchievementCompleted;
                else
                    status = $"{current}/{max}";

                string label = Strings.AchievementEntry(title, description, status, isFavorite);

                int actionIdx = 0;
                int claimIdx = isClaimable ? ++actionIdx : 0;
                int trackIdx = ++actionIdx;

                _achievementEntries.Add(new AchievementEntry
                {
                    Label = label,
                    GameObject = card.gameObject,
                    Type = EntryType.Achievement,
                    IsClaimable = isClaimable,
                    IsFavorite = isFavorite,
                    ActionCount = actionIdx,
                    ClaimActionIndex = claimIdx,
                    TrackActionIndex = trackIdx
                });
            }
        }

        #endregion

        #region Custom Navigation

        protected override bool HandleCustomInput()
        {
            // Reset sub-action index when the focused element changes
            if (_currentIndex != _lastNavigatedIndex)
            {
                _achievementActionIndex = 0;
                _lastNavigatedIndex = _currentIndex;
            }

            // Backspace: navigate back to home
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                NavigateToHome();
                return true;
            }

            if (_currentIndex < 0 || _currentIndex >= _achievementEntries.Count)
                return false;

            var entry = _achievementEntries[_currentIndex];

            // --- Achievement cards: Left/Right cycles sub-actions, Enter fires them ---
            if (entry.Type == EntryType.Achievement)
            {
                bool isLeft  = Input.GetKeyDown(KeyCode.LeftArrow)  || Input.GetKeyDown(KeyCode.A);
                bool isRight = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
                bool isEnter = Input.GetKeyDown(KeyCode.Return)      || Input.GetKeyDown(KeyCode.KeypadEnter);

                if (isLeft || isRight)
                {
                    int newIdx = _achievementActionIndex + (isRight ? 1 : -1);
                    if (newIdx < 0)
                    {
                        _announcer.AnnounceVerbose(Strings.BeginningOfList);
                        return true;
                    }
                    if (newIdx > entry.ActionCount)
                    {
                        _announcer.AnnounceVerbose(Strings.EndOfList);
                        return true;
                    }
                    _achievementActionIndex = newIdx;
                    _announcer.AnnounceInterrupt(GetActionAnnouncement(entry));
                    return true;
                }

                if (isEnter)
                {
                    if (_achievementActionIndex == 0)
                    {
                        // No default action on the card itself — re-announce for orientation
                        _announcer.AnnounceInterrupt(entry.Label);
                    }
                    else if (_achievementActionIndex == entry.ClaimActionIndex)
                    {
                        ActivateCollectButton(entry.GameObject);
                    }
                    else if (_achievementActionIndex == entry.TrackActionIndex)
                    {
                        ToggleTracking(entry.GameObject);
                    }
                    return true;
                }

                return false;
            }

            // --- Set tab headers: Enter switches tab ---
            if (entry.Type == EntryType.SetHeader)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    ActivateSetTab(entry.GameObject);
                    return true;
                }
            }

            // --- Group headers: Enter toggles foldout ---
            if (entry.Type == EntryType.GroupHeader)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    ToggleGroupFoldout(entry.GameObject);
                    return true;
                }
            }

            return false;
        }

        private string GetActionAnnouncement(AchievementEntry entry)
        {
            if (_achievementActionIndex == 0)
                return entry.Label;

            string label;
            if (_achievementActionIndex == entry.ClaimActionIndex)
                label = "Claim";
            else if (_achievementActionIndex == entry.TrackActionIndex)
                label = entry.IsFavorite ? "Untrack" : "Track";
            else
                label = "Action";

            return $"{label}, option {_achievementActionIndex} of {entry.ActionCount}";
        }

        private void ActivateCollectButton(GameObject cardObject)
        {
            if (_achievementCardType == null) return;

            var collectField = _achievementCardType.GetField("_collectButton", AllInstanceFlags);
            if (collectField == null) return;

            var card = cardObject.GetComponent(_achievementCardType) as MonoBehaviour;
            if (card == null) return;

            var collectButton = collectField.GetValue(card);
            if (collectButton == null) return;

            var buttonObj = (collectButton as MonoBehaviour)?.gameObject;
            if (buttonObj != null && buttonObj.activeInHierarchy)
            {
                UIActivator.Activate(buttonObj);
                _announcer.Announce(Strings.ActivatedBare);
                // Trigger rescan after a short delay to update status
                ScheduleRescan(0.5f);
            }
        }

        private void ActivateSetTab(GameObject tabObject)
        {
            if (_selectSetMethod == null) return;
            var setItem = tabObject.GetComponent(_setItemType) as MonoBehaviour;
            if (setItem == null) return;

            MelonLogger.Msg($"[{NavigatorId}] Selecting set tab: {tabObject.name}");
            try
            {
                _selectSetMethod.Invoke(setItem, new object[] { true });
                ScheduleRescan(1.0f);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[{NavigatorId}] SelectSet error: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        private void ToggleTracking(GameObject cardObject)
        {
            if (_achievementCardType == null || _achievementDataField == null || _setFavoriteMethod == null) return;

            var card = cardObject.GetComponent(_achievementCardType) as MonoBehaviour;
            if (card == null) return;

            var achievementData = _achievementDataField.GetValue(card);
            if (achievementData == null) return;

            bool current = SafeGetBool(_isFavoriteProp, achievementData);
            bool newValue = !current;

            try
            {
                _setFavoriteMethod.Invoke(achievementData, new object[] { newValue });
                string announcement = newValue ? Strings.AchievementTracked : Strings.AchievementUntracked;
                _announcer.AnnounceInterrupt(announcement);
                ScheduleRescan(0.3f);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[{NavigatorId}] SetFavorite error: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        private void ToggleGroupFoldout(GameObject groupObject)
        {
            // Scan all child components for one that has ToggleFoldout() — avoids needing to know the Foldout type/assembly
            foreach (var comp in groupObject.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                var toggleMethod = comp.GetType().GetMethod("ToggleFoldout", BindingFlags.Public | BindingFlags.Instance);
                if (toggleMethod != null)
                {
                    MelonLogger.Msg($"[{NavigatorId}] ToggleFoldout via {comp.GetType().FullName} on {groupObject.name}");
                    try { toggleMethod.Invoke(comp, null); }
                    catch (Exception ex) { MelonLogger.Warning($"[{NavigatorId}] ToggleFoldout error: {ex.InnerException?.Message ?? ex.Message}"); }
                    ScheduleRescan(0.8f);
                    return;
                }
            }
            MelonLogger.Warning($"[{NavigatorId}] No ToggleFoldout found on {groupObject.name}");
        }

        private float _rescanTimer;
        private int _achievementActionIndex;  // 0 = element itself, 1+ = sub-actions
        private int _lastNavigatedIndex = -1;

        private void ScheduleRescan(float delay)
        {
            _rescanTimer = delay;
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
                    int savedIndex = _currentIndex;
                    _elements.Clear();
                    _achievementEntries.Clear();
                    DiscoverElements();

                    if (_elements.Count > 0)
                    {
                        _currentIndex = Math.Min(savedIndex, _elements.Count - 1);
                        if (_currentIndex >= 0)
                            _announcer.AnnounceInterrupt(GetElementAnnouncement(_currentIndex));
                    }
                }
            }

            // Check if controller is still valid
            if (_controller == null || _controller.gameObject == null || !_controller.gameObject.activeInHierarchy)
            {
                Deactivate();
                return;
            }

            // Check IsOpen
            if (_isOpenProp != null)
            {
                try
                {
                    if (!(bool)_isOpenProp.GetValue(_controller))
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

        protected override void OnDeactivating()
        {
            _controller = null;
            _achievementEntries.Clear();
            _rescanTimer = 0;
            _achievementActionIndex = 0;
            _lastNavigatedIndex = -1;
        }

        #endregion

        #region Announcements

        protected override string GetActivationAnnouncement()
        {
            int achievementCount = _achievementEntries.Count(e => e.Type == EntryType.Achievement);
            int claimableCount = _achievementEntries.Count(e => e.IsClaimable);

            return Strings.AchievementsActivation(achievementCount, claimableCount);
        }

        protected override string GetElementAnnouncement(int index)
        {
            if (index < 0 || index >= _achievementEntries.Count) return "";

            var entry = _achievementEntries[index];
            return $"{entry.Label}, {index + 1} of {_achievementEntries.Count}";
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
