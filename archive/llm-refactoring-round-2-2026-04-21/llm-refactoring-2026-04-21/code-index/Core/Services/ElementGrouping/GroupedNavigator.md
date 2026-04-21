# GroupedNavigator.cs
Path: src/Core/Services/ElementGrouping/GroupedNavigator.cs
Lines: 2167

## Top-level comments
- No file header comment. Namespace-level doc comments introduce each public type.

## public enum NavigationLevel (line 15)
- GroupList (line 18)
- InsideGroup (line 20)

## public struct GroupedElement (line 26)
### Properties
- public GameObject GameObject { get; set; } (line 28)
- public string Label { get; set; } (line 29)
- public UIElementClassifier.ElementRole Role { get; set; } (line 30)
- public ElementGroup Group { get; set; } (line 31)
- public string FolderName { get; set; } (line 36) — Note: null for non-deck elements; set for deck folder membership.
- public ElementGroup? SubgroupType { get; set; } (line 41) — Note: when set, element represents a virtual subgroup entry.

## public struct ElementGroupInfo (line 47)
### Properties
- public ElementGroup Group { get; set; } (line 49)
- public string DisplayName { get; set; } (line 50)
- public List<GroupedElement> Elements { get; set; } (line 51)
- public int Count => Elements?.Count ?? 0; (line 52)
- public GameObject FolderToggle { get; set; } (line 56)
- public bool IsFolderGroup { get; set; } (line 60)
- public bool IsStandaloneElement { get; set; } (line 65) — Note: standalone entries act like group-level buttons, not containers.

## public class GroupedNavigator (line 73)
### Fields
- private readonly IAnnouncementService _announcer (line 75)
- private readonly ElementGroupAssigner _groupAssigner (line 76)
- private List<ElementGroupInfo> _groups (line 78)
- private int _currentGroupIndex (line 79)
- private int _currentElementIndex (line 80)
- private NavigationLevel _navigationLevel (line 81)
- private string _pendingFolderEntry (line 87)
- private bool _pendingPlayBladeTabsEntry (line 93)
- private bool _pendingPlayBladeContentEntry (line 99)
- private int _pendingPlayBladeContentEntryIndex (line 100)
- private bool _pendingFirstFolderEntry (line 106)
- private bool _pendingFoldersEntry (line 112)
- private string _pendingFoldersEntryRestoreFolder (line 113)
- private string _pendingSpecificFolderEntry (line 119)
- private bool _isPlayBladeContext (line 126)
- private bool _isChallengeContext (line 133)
- private bool _pendingChallengeMainEntry (line 139)
- private int _pendingChallengeMainEntryIndex (line 145)
- private GameObject _findMatchTabObject (line 151)
- private string _pendingQueueTypeActivation (line 157)
- private int _lastQueueTypeTabIndex (line 168)
- private int _pendingPlayBladeTabsEntryIndex (line 174)
- private ElementGroup? _pendingGroupRestore (line 180)
- private NavigationLevel _pendingLevelRestore (line 185)
- private string _pendingGroupRestoreDisplayName (line 191)
- private int _pendingElementIndexRestore (line 197)
- private ElementGroup? _pendingSubgroupRestore (line 202)
- private int _pendingSubgroupElementIndexRestore (line 207)
- private Dictionary<ElementGroup, List<GroupedElement>> _subgroupElements (line 213)
- private ElementGroup? _currentSubgroup (line 219)
- private int _subgroupParentIndex (line 224)

### Properties
- public bool NeedsFollowUpRescan { get; private set; } (line 163)
- public bool IsActive => _groups.Count > 0; (line 229)
- public NavigationLevel Level => _navigationLevel; (line 234)
- public int CurrentGroupIndex => _currentGroupIndex; (line 239)
- public int CurrentElementIndex => _currentElementIndex; (line 244)
- public ElementGroupInfo? CurrentGroup (line 249)
- public GroupedElement? CurrentElement => GetCurrentElement(); (line 258)
- public bool IsCurrentGroupStandalone (line 303)
- public int GroupCount => _groups.Count; (line 320)
- public bool IsPlayBladeContext => _isPlayBladeContext; (line 470)
- public bool IsChallengeContext => _isChallengeContext; (line 475)
- public bool HasPendingRestore => _pendingGroupRestore.HasValue; (line 575)
- public bool PositionWasRestored { get; private set; } (line 581) — Note: set by OrganizeIntoGroups to signal rescan-position restoration.
- public bool IsInsideSubgroup => _currentSubgroup.HasValue; (line 1668)
- public bool HasElements => _groups.Any(g => g.Count > 0); (line 1907)

### Methods
- private GroupedElement? GetCurrentElement() (line 263) — Note: returns subgroup element when inside one.
- private int GetCurrentElementCount() (line 288)
- public GameObject GetStandaloneElement() (line 309)
- public GroupedNavigator(IAnnouncementService announcer, ElementGroupAssigner groupAssigner) (line 322)
- public void RequestPlayBladeTabsEntry() (line 334) — Note: skips request when content entry already pending; restores last queue-type tab index.
- public void RequestPlayBladeContentEntry() (line 352)
- public void RequestPlayBladeContentEntryAtIndex(int elementIndex) (line 364)
- public void RequestFirstFolderEntry() (line 376)
- public void RequestFoldersEntry(string restoreToFolder = null) (line 388)
- public void RequestSpecificFolderEntry(string folderName) (line 402)
- public void SetPendingQueueTypeActivation(string queueType) (line 416)
- public GameObject GetFindMatchTabObject() (line 426)
- public void StoreLastQueueTypeTabIndex() (line 435)
- public void SetPlayBladeContext(bool isActive) (line 445) — Note: on deactivation clears pending group restore but keeps auto-entry flags.
- public void SetChallengeContext(bool isActive) (line 481) — Note: clears ChallengeMain pending state when deactivating.
- public void RequestChallengeMainEntry() (line 505)
- public void RequestChallengeMainEntryAtIndex(int elementIndex) (line 519)
- public void SaveCurrentGroupForRestore() (line 533) — Note: captures group, display name, level, element and subgroup indices.
- public void ResetPendingElementIndex() (line 587)
- public void ClearPendingGroupRestore() (line 595)
- public void OrganizeIntoGroups(IEnumerable<(GameObject obj, string label, UIElementClassifier.ElementRole role)> elements) (line 610) — Note: main rebuild; handles folder/subgroup extraction, auto-entry flags, and group-restore matching.
- private void PostProcessPlayBladeTabs() (line 1252) — Note: injects virtual queue-type entries or marks real queue tabs as subgroup entries; sets NeedsFollowUpRescan side effect elsewhere.
- private static string GetQueueTypeFromTabName(string tabName) (line 1365)
- private static int FindQueueTypeInsertionIndex(List<GroupedElement> elements) (line 1394)
- private static GameObject FindMatchTabInHierarchy() (line 1414) — Note: uses FindObjectsOfType<RectTransform>(true) each call — expensive.
- public void AddVirtualGroup(ElementGroup group, List<GroupedElement> elements, ElementGroup? insertAfter = null, bool isStandalone = false, string displayName = null) (line 1436)
- public void AppendElementToGroup(ElementGroup groupType, string label) (line 1475)
- public void UpdateElementLabel(ElementGroup groupType, int elementIndex, string newLabel) (line 1497) — Note: writes struct back to list to preserve value-type semantics.
- public void Clear() (line 1514)
- private void AutoEnterIfSingleItem() (line 1525) — Note: also auto-enters Primary group regardless of count.
- public bool MoveNext() (line 1550)
- public bool MovePrevious() (line 1562)
- public void MoveFirst() (line 1573)
- public void MoveLast() (line 1589)
- public bool EnterGroup() (line 1608) — Note: sets _pendingFolderEntry for folder groups; allows entering empty folder groups because toggle activation may populate them.
- public bool ExitGroup() (line 1643)
- public bool IsCurrentElementSubgroupEntry() (line 1656)
- public bool EnterSubgroup() (line 1674)
- public bool ExitSubgroup() (line 1700) — Note: restores parent group and refocuses the subgroup entry element.
- private List<GroupedElement> GetCurrentSubgroupElements() (line 1729)
- public string GetCurrentAnnouncement() (line 1741)
- public string GetActivationAnnouncement(string screenName) (line 1752)
- private bool MoveNextGroup() (line 1774) — Note: re-announces when only one group exists before announcing end-of-list.
- private bool MovePreviousGroup() (line 1790)
- private bool MoveNextElement() (line 1806)
- private bool MovePreviousElement() (line 1825)
- private string GetGroupAnnouncement() (line 1842) — Note: refreshes label from live UI for standalone groups.
- private string GetElementAnnouncement() (line 1863)
- private void AnnounceCurrentGroup() (line 1873)
- private void AnnounceCurrentElement() (line 1878)
- public void FilterToOverlay(ElementGroup overlayGroup) (line 1887) — Note: mutates _groups by LINQ filter, discarding non-matching groups.
- public IEnumerable<GroupedElement> GetAllElements() (line 1912)
- public bool JumpToGroup(ElementGroup groupType) (line 1922)
- public bool JumpToGroupByName(string displayName) (line 1943)
- public bool JumpToGroupAndEnter(ElementGroup groupType) (line 1963)
- public bool JumpToGroupByIndex(int index) (line 1982)
- public bool JumpToElementByIndex(int index) (line 1994)
- public List<string> GetGroupDisplayNames() (line 2006)
- public List<string> GetCurrentGroupElementLabels() (line 2017)
- public ElementGroupInfo? GetGroupByType(ElementGroup groupType) (line 2031)
- public ElementGroupInfo? GetGroupByName(string displayName) (line 2039)
- public GameObject GetElementFromGroup(ElementGroup groupType, int index) (line 2048)
- public int GetGroupElementCount(ElementGroup groupType) (line 2061)
- public IEnumerable<ElementGroupInfo> GetAllGroupsOfType(ElementGroup groupType) (line 2070)
- public IEnumerable<ElementGroupInfo> FindGroups(System.Func<ElementGroupInfo, bool> predicate) (line 2078)
- public bool CycleToNextGroup(params ElementGroup[] allowedGroups) (line 2089) — Note: skips standalone groups and wraps; auto-enters the new group.
- public bool CycleToPreviousGroup(params ElementGroup[] allowedGroups) (line 2130) — Note: skips standalone groups and wraps; auto-enters the new group.
