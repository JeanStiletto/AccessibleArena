using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Core.Services.ElementGrouping
{
    /// <summary>
    /// Navigation level for hierarchical menu navigation.
    /// </summary>
    public enum NavigationLevel
    {
        /// <summary>Navigating between groups</summary>
        GroupList,
        /// <summary>Navigating within a single group</summary>
        InsideGroup
    }

    /// <summary>
    /// Represents a navigable UI element with its group assignment.
    /// </summary>
    public struct GroupedElement
    {
        public GameObject GameObject { get; set; }
        public string Label { get; set; }
        public ElementGroup Group { get; set; }
        /// <summary>
        /// For deck elements, the name of the folder they belong to (e.g., "Meine Decks", "Starterdecks").
        /// Null for non-deck elements.
        /// </summary>
        public string FolderName { get; set; }
        /// <summary>
        /// If set, this element represents a nested subgroup entry (e.g., Objectives within Progress).
        /// Entering this element will navigate into the subgroup.
        /// </summary>
        public ElementGroup? SubgroupType { get; set; }
    }

    /// <summary>
    /// Represents a group of elements for hierarchical navigation.
    /// </summary>
    public struct ElementGroupInfo
    {
        public ElementGroup Group { get; set; }
        public string DisplayName { get; set; }
        public List<GroupedElement> Elements { get; set; }
        public int Count => Elements?.Count ?? 0;
        /// <summary>
        /// For folder groups, the toggle GameObject to activate when entering this group.
        /// </summary>
        public GameObject FolderToggle { get; set; }
        /// <summary>
        /// Whether this is a dynamically created folder group.
        /// </summary>
        public bool IsFolderGroup { get; set; }
        /// <summary>
        /// Whether this is a standalone element shown at group level (e.g., Primary action buttons).
        /// Standalone elements are directly activatable without entering a group.
        /// </summary>
        public bool IsStandaloneElement { get; set; }
    }

    /// <summary>
    /// Provides two-level hierarchical navigation for menus.
    /// Groups → Elements within groups.
    /// Used by GeneralMenuNavigator to provide better accessibility.
    /// </summary>
    public class GroupedNavigator
    {
        private readonly IAnnouncementService _announcer;
        private readonly ElementGroupAssigner _groupAssigner;

        private List<ElementGroupInfo> _groups = new List<ElementGroupInfo>();
        private int _currentGroupIndex = -1;
        private int _currentElementIndex = -1;
        private NavigationLevel _navigationLevel = NavigationLevel.GroupList;

        /// <summary>
        /// Folder name to auto-enter after a rescan. Set when entering a folder group,
        /// checked and cleared by OrganizeIntoGroups after rebuilding.
        /// </summary>
        private string _pendingFolderEntry = null;

        /// <summary>
        /// When true, auto-enter PlayBladeTabs group after next OrganizeIntoGroups.
        /// Set when PlayBlade opens.
        /// </summary>
        private bool _pendingPlayBladeTabsEntry = false;

        /// <summary>
        /// When true, auto-enter PlayBladeContent group after next OrganizeIntoGroups.
        /// Set when a PlayBlade tab is activated.
        /// </summary>
        private bool _pendingPlayBladeContentEntry = false;
        private int _pendingPlayBladeContentEntryIndex = -1;

        /// <summary>
        /// When true, auto-enter first folder group after next OrganizeIntoGroups.
        /// Set when a play mode (Ranked/Play/Brawl) is activated.
        /// </summary>
        private bool _pendingFirstFolderEntry = false;

        /// <summary>
        /// When true, auto-enter PlayBladeFolders group after next OrganizeIntoGroups.
        /// Set when a play mode is activated in PlayBlade context.
        /// </summary>
        private bool _pendingFoldersEntry = false;

        /// <summary>
        /// Specific folder name to auto-enter after entering PlayBladeFolders.
        /// Set when user selects a folder from the folders list.
        /// </summary>
        private string _pendingSpecificFolderEntry = null;

        /// <summary>
        /// Whether we're currently in a PlayBlade context (blade is open).
        /// Set by PlayBladeNavigationHelper when blade opens/closes.
        /// Used to determine whether to create PlayBladeFolders wrapper group.
        /// </summary>
        private bool _isPlayBladeContext = false;

        /// <summary>
        /// Whether we're currently in a Challenge context (Direct/Friend Challenge screen).
        /// Set by ChallengeNavigationHelper when challenge opens/closes.
        /// Used to determine whether to create PlayBladeFolders wrapper group for deck selection.
        /// </summary>
        private bool _isChallengeContext = false;

        /// <summary>
        /// When true, auto-enter ChallengeMain group after next OrganizeIntoGroups.
        /// Set when challenge opens or when returning from deck selection.
        /// </summary>
        private bool _pendingChallengeMainEntry = false;

        /// <summary>
        /// Specific element index to restore when entering ChallengeMain.
        /// Used by spinner rescan to preserve position.
        /// </summary>
        private int _pendingChallengeMainEntryIndex = -1;

        /// <summary>
        /// Reference to the FindMatch game tab for proxy clicking when not active.
        /// Stored when building PlayBladeTabs so virtual queue type entries can activate it.
        /// </summary>
        private GameObject _findMatchTabObject = null;

        /// <summary>
        /// Pending queue type to activate after FindMatch becomes active.
        /// Set when user enters a virtual queue type entry and FindMatch blade is not yet open.
        /// </summary>
        private string _pendingQueueTypeActivation = null;

        /// <summary>
        /// Whether post-organize processing requires a follow-up rescan.
        /// Set when a pending queue type activation clicks a real tab that changes content.
        /// </summary>
        public bool NeedsFollowUpRescan { get; private set; }

        /// <summary>
        /// Last selected queue type tab index in PlayBladeTabs (for Backspace position restore).
        /// </summary>
        private int _lastQueueTypeTabIndex = -1;

        /// <summary>
        /// Initial element index to use when entering PlayBladeTabs (for position restore).
        /// When >= 0, overrides the default 0 index on PlayBladeTabs auto-entry.
        /// </summary>
        private int _pendingPlayBladeTabsEntryIndex = -1;

        /// <summary>
        /// Group type to restore after rescan. Set by SaveCurrentGroupForRestore(),
        /// cleared after OrganizeIntoGroups attempts restoration.
        /// </summary>
        private ElementGroup? _pendingGroupRestore = null;

        /// <summary>
        /// Navigation level to restore after rescan.
        /// </summary>
        private NavigationLevel _pendingLevelRestore = NavigationLevel.GroupList;

        /// <summary>
        /// Element index within group to restore after rescan.
        /// Used to preserve position when activating deck builder cards.
        /// </summary>
        private int _pendingElementIndexRestore = -1;

        /// <summary>
        /// Stores subgroup elements (e.g., Objectives) that are nested within another group.
        /// Key is the subgroup type, value is the list of elements in that subgroup.
        /// </summary>
        private Dictionary<ElementGroup, List<GroupedElement>> _subgroupElements = new Dictionary<ElementGroup, List<GroupedElement>>();

        /// <summary>
        /// When inside a subgroup, tracks which subgroup we're in.
        /// Null when not inside a subgroup.
        /// </summary>
        private ElementGroup? _currentSubgroup = null;

        /// <summary>
        /// When inside a subgroup, tracks the parent group index to return to on backspace.
        /// </summary>
        private int _subgroupParentIndex = -1;

        /// <summary>
        /// Whether grouped navigation is currently active.
        /// </summary>
        public bool IsActive => _groups.Count > 0;

        /// <summary>
        /// Current navigation level (groups or inside a group).
        /// </summary>
        public NavigationLevel Level => _navigationLevel;

        /// <summary>
        /// Current group index.
        /// </summary>
        public int CurrentGroupIndex => _currentGroupIndex;

        /// <summary>
        /// Current element index within the current group.
        /// </summary>
        public int CurrentElementIndex => _currentElementIndex;

        /// <summary>
        /// Gets the current group info, or null if invalid.
        /// </summary>
        public ElementGroupInfo? CurrentGroup =>
            _currentGroupIndex >= 0 && _currentGroupIndex < _groups.Count
                ? _groups[_currentGroupIndex]
                : null;

        /// <summary>
        /// Gets the current element, or null if not inside a group or invalid.
        /// Handles subgroups - returns subgroup element when inside a subgroup.
        /// </summary>
        public GroupedElement? CurrentElement => GetCurrentElement();

        /// <summary>
        /// Get the current element, handling subgroups.
        /// </summary>
        private GroupedElement? GetCurrentElement()
        {
            if (_navigationLevel != NavigationLevel.InsideGroup)
                return null;

            // If inside a subgroup, return subgroup element
            if (_currentSubgroup.HasValue)
            {
                var subElements = GetCurrentSubgroupElements();
                if (subElements == null || _currentElementIndex < 0 || _currentElementIndex >= subElements.Count)
                    return null;
                return subElements[_currentElementIndex];
            }

            // Normal group element
            var group = CurrentGroup;
            if (group == null || _currentElementIndex < 0 || _currentElementIndex >= group.Value.Count)
                return null;

            return group.Value.Elements[_currentElementIndex];
        }

        /// <summary>
        /// Get the count of elements at the current navigation level (handles subgroups).
        /// </summary>
        private int GetCurrentElementCount()
        {
            if (_currentSubgroup.HasValue)
            {
                var subElements = GetCurrentSubgroupElements();
                return subElements?.Count ?? 0;
            }

            var group = CurrentGroup;
            return group?.Count ?? 0;
        }

        /// <summary>
        /// Whether the current group is a standalone element (directly activatable at group level).
        /// </summary>
        public bool IsCurrentGroupStandalone =>
            CurrentGroup.HasValue && CurrentGroup.Value.IsStandaloneElement;

        /// <summary>
        /// Get the standalone element's GameObject if current group is standalone.
        /// </summary>
        public GameObject GetStandaloneElement()
        {
            if (!IsCurrentGroupStandalone) return null;
            var group = CurrentGroup;
            if (!group.HasValue || group.Value.Elements.Count == 0) return null;
            return group.Value.Elements[0].GameObject;
        }

        /// <summary>
        /// Total number of groups.
        /// </summary>
        public int GroupCount => _groups.Count;

        public GroupedNavigator(IAnnouncementService announcer, ElementGroupAssigner groupAssigner)
        {
            _announcer = announcer;
            _groupAssigner = groupAssigner;
        }

        /// <summary>
        /// Request auto-entry into PlayBladeTabs group after next rescan.
        /// Call when PlayBlade opens.
        /// Does NOT override a pending content entry (tab was just clicked).
        /// Optionally restores position to the last queue type tab index.
        /// </summary>
        public void RequestPlayBladeTabsEntry()
        {
            // Don't override content entry - it means a tab was just clicked
            if (_pendingPlayBladeContentEntry)
            {
                MelonLogger.Msg("[GroupedNavigator] Skipping PlayBladeTabs entry - content entry already pending");
                return;
            }
            _pendingPlayBladeTabsEntry = true;
            // Restore position to last queue type tab if available
            _pendingPlayBladeTabsEntryIndex = _lastQueueTypeTabIndex >= 0 ? _lastQueueTypeTabIndex : -1;
            MelonLogger.Msg($"[GroupedNavigator] Requested PlayBladeTabs auto-entry (index: {_pendingPlayBladeTabsEntryIndex})");
        }

        /// <summary>
        /// Request auto-entry into PlayBladeContent group after next rescan.
        /// Call when a PlayBlade tab is activated.
        /// </summary>
        public void RequestPlayBladeContentEntry()
        {
            _pendingPlayBladeContentEntry = true;
            _pendingPlayBladeContentEntryIndex = -1; // Start at 0
            _pendingPlayBladeTabsEntry = false; // Clear tabs flag
            MelonLogger.Msg("[GroupedNavigator] Requested PlayBladeContent auto-entry");
        }

        /// <summary>
        /// Request auto-entry into PlayBladeContent group at a specific element index.
        /// Used by spinner rescan to restore the user's position on their stepper.
        /// </summary>
        public void RequestPlayBladeContentEntryAtIndex(int elementIndex)
        {
            _pendingPlayBladeContentEntry = true;
            _pendingPlayBladeContentEntryIndex = elementIndex;
            _pendingPlayBladeTabsEntry = false;
            MelonLogger.Msg($"[GroupedNavigator] Requested PlayBladeContent auto-entry at index {elementIndex}");
        }

        /// <summary>
        /// Request auto-entry into first folder group after next rescan.
        /// Call when a play mode is activated (Ranked/Play/Brawl).
        /// </summary>
        public void RequestFirstFolderEntry()
        {
            _pendingFirstFolderEntry = true;
            _pendingPlayBladeContentEntry = false;
            _pendingPlayBladeTabsEntry = false;
            MelonLogger.Msg("[GroupedNavigator] Requested first folder auto-entry");
        }

        /// <summary>
        /// Request auto-entry into PlayBladeFolders group after next rescan.
        /// Call when a play mode is activated in PlayBlade context.
        /// </summary>
        public void RequestFoldersEntry()
        {
            _pendingFoldersEntry = true;
            _pendingFirstFolderEntry = false;
            _pendingPlayBladeContentEntry = false;
            _pendingPlayBladeTabsEntry = false;
            MelonLogger.Msg("[GroupedNavigator] Requested PlayBladeFolders auto-entry");
        }

        /// <summary>
        /// Request auto-entry into a specific folder after next rescan.
        /// Call when user selects a folder from the PlayBladeFolders list.
        /// </summary>
        public void RequestSpecificFolderEntry(string folderName)
        {
            _pendingSpecificFolderEntry = folderName;
            _pendingFoldersEntry = false;
            _pendingFirstFolderEntry = false;
            _pendingPlayBladeContentEntry = false;
            _pendingPlayBladeTabsEntry = false;
            MelonLogger.Msg($"[GroupedNavigator] Requested specific folder auto-entry: {folderName}");
        }

        /// <summary>
        /// Set pending queue type activation after FindMatch tab is clicked.
        /// The queue type tab will be clicked on the next rescan when it becomes available.
        /// </summary>
        public void SetPendingQueueTypeActivation(string queueType)
        {
            _pendingQueueTypeActivation = queueType;
            MelonLogger.Msg($"[GroupedNavigator] Set pending queue type activation: {queueType}");
        }

        /// <summary>
        /// Get the FindMatch tab GameObject for proxy clicking.
        /// Returns the stored reference from the last PlayBladeTabs build.
        /// </summary>
        public GameObject GetFindMatchTabObject()
        {
            return _findMatchTabObject;
        }

        /// <summary>
        /// Store the current element index as the last queue type tab position.
        /// Used for Backspace position restore from PlayBladeContent back to tabs.
        /// </summary>
        public void StoreLastQueueTypeTabIndex()
        {
            _lastQueueTypeTabIndex = _currentElementIndex;
            MelonLogger.Msg($"[GroupedNavigator] Stored last queue type tab index: {_lastQueueTypeTabIndex}");
        }

        /// <summary>
        /// Set whether we're in a PlayBlade context.
        /// Call when PlayBlade opens (true) or closes (false).
        /// </summary>
        public void SetPlayBladeContext(bool isActive)
        {
            _isPlayBladeContext = isActive;

            // When blade closes, clear stale restore state but NOT the auto-entry flags
            // The auto-entry flags (content, folders, etc.) may have just been set by a tab/mode activation
            // and are needed for the brief close/open cycle that happens during tab switching
            if (!isActive)
            {
                // Only clear the group restore - it causes stale state to overwrite auto-entries
                _pendingGroupRestore = null;
                _pendingLevelRestore = NavigationLevel.GroupList;
                _pendingElementIndexRestore = -1;
                MelonLogger.Msg($"[GroupedNavigator] PlayBlade context set to: {isActive} - cleared group restore");
            }
            else
            {
                MelonLogger.Msg($"[GroupedNavigator] PlayBlade context set to: {isActive}");
            }
        }

        /// <summary>
        /// Whether we're currently in a PlayBlade context.
        /// </summary>
        public bool IsPlayBladeContext => _isPlayBladeContext;

        /// <summary>
        /// Whether we're currently in a Challenge context.
        /// </summary>
        public bool IsChallengeContext => _isChallengeContext;

        /// <summary>
        /// Set whether we're in a Challenge context.
        /// Call when challenge screen opens (true) or closes (false).
        /// </summary>
        public void SetChallengeContext(bool isActive)
        {
            _isChallengeContext = isActive;

            if (!isActive)
            {
                _pendingChallengeMainEntry = false;
                _pendingChallengeMainEntryIndex = -1;
                _pendingGroupRestore = null;
                _pendingLevelRestore = NavigationLevel.GroupList;
                _pendingElementIndexRestore = -1;
                MelonLogger.Msg($"[GroupedNavigator] Challenge context set to: {isActive} - cleared pending state");
            }
            else
            {
                MelonLogger.Msg($"[GroupedNavigator] Challenge context set to: {isActive}");
            }
        }

        /// <summary>
        /// Request auto-entry into ChallengeMain group after next rescan.
        /// Call when challenge opens or when returning from deck selection.
        /// </summary>
        public void RequestChallengeMainEntry()
        {
            _pendingChallengeMainEntry = true;
            _pendingChallengeMainEntryIndex = -1;
            _pendingPlayBladeTabsEntry = false;
            _pendingPlayBladeContentEntry = false;
            _pendingFoldersEntry = false;
            MelonLogger.Msg("[GroupedNavigator] Requested ChallengeMain auto-entry");
        }

        /// <summary>
        /// Request auto-entry into ChallengeMain group at a specific element index.
        /// Used by spinner rescan to restore the user's position on their stepper.
        /// </summary>
        public void RequestChallengeMainEntryAtIndex(int elementIndex)
        {
            _pendingChallengeMainEntry = true;
            _pendingChallengeMainEntryIndex = elementIndex;
            _pendingPlayBladeTabsEntry = false;
            _pendingPlayBladeContentEntry = false;
            _pendingFoldersEntry = false;
            MelonLogger.Msg($"[GroupedNavigator] Requested ChallengeMain auto-entry at index {elementIndex}");
        }

        /// <summary>
        /// Save the current group state for restoration after rescan.
        /// Call this before triggering a rescan to preserve the user's position.
        /// </summary>
        public void SaveCurrentGroupForRestore()
        {
            if (_currentGroupIndex >= 0 && _currentGroupIndex < _groups.Count)
            {
                _pendingGroupRestore = _groups[_currentGroupIndex].Group;
                _pendingLevelRestore = _navigationLevel;
                _pendingElementIndexRestore = _currentElementIndex;
                MelonLogger.Msg($"[GroupedNavigator] Saved group for restore: {_pendingGroupRestore}, level: {_pendingLevelRestore}, elementIndex: {_pendingElementIndexRestore}");
            }
            else
            {
                _pendingGroupRestore = null;
                _pendingLevelRestore = NavigationLevel.GroupList;
                _pendingElementIndexRestore = -1;
            }
        }

        /// <summary>
        /// Whether a group restore is already pending.
        /// </summary>
        public bool HasPendingRestore => _pendingGroupRestore.HasValue;

        /// <summary>
        /// Reset the pending element index to 0 (start of group).
        /// Call after SaveCurrentGroupForRestore() when you want to restore the group but not the position.
        /// </summary>
        public void ResetPendingElementIndex()
        {
            _pendingElementIndexRestore = 0;
        }

        /// <summary>
        /// Clear the pending group restore (use when you don't want to restore after rescan).
        /// </summary>
        public void ClearPendingGroupRestore()
        {
            _pendingGroupRestore = null;
            _pendingLevelRestore = NavigationLevel.GroupList;
            _pendingElementIndexRestore = -1;
        }

        /// <summary>
        /// Organize discovered elements into groups.
        /// Call this after DiscoverElements() populates the raw element list.
        /// Supports folder-based grouping for Decks screen.
        /// </summary>
        public void OrganizeIntoGroups(IEnumerable<(GameObject obj, string label)> elements)
        {
            _groups.Clear();
            _currentGroupIndex = -1;
            _currentElementIndex = -1;
            _navigationLevel = NavigationLevel.GroupList;

            // First pass: identify folder toggles and their names
            var folderToggles = new Dictionary<string, GameObject>(); // folderName -> toggle GameObject
            var folderDecks = new Dictionary<string, List<GroupedElement>>(); // folderName -> decks in that folder
            var nonFolderElements = new Dictionary<ElementGroup, List<GroupedElement>>(); // standard groups

            foreach (var (obj, label) in elements)
            {
                if (obj == null) continue;

                // Skip Tag buttons (quantity indicators like "4x" in deck list)
                if (obj.name == "CustomButton - Tag")
                    continue;

                var group = _groupAssigner.DetermineGroup(obj);

                // Skip elements that should be hidden from navigation
                if (group == ElementGroup.Unknown)
                    continue;

                // Check if this is a folder toggle
                if (ElementGroupAssigner.IsFolderToggle(obj))
                {
                    string folderName = ElementGroupAssigner.GetFolderNameFromToggle(obj);
                    if (!string.IsNullOrEmpty(folderName) && !folderToggles.ContainsKey(folderName))
                    {
                        folderToggles[folderName] = obj;
                        folderDecks[folderName] = new List<GroupedElement>();
                        MelonLogger.Msg($"[GroupedNavigator] Found folder toggle: {folderName}");
                    }
                    continue; // Don't add folder toggles as navigable elements
                }

                // Check if this is a deck element
                if (ElementGroupAssigner.IsDeckElement(obj, label))
                {
                    string folderName = ElementGroupAssigner.GetFolderNameForDeck(obj);
                    if (!string.IsNullOrEmpty(folderName))
                    {
                        // Ensure folder exists in our tracking
                        if (!folderDecks.ContainsKey(folderName))
                            folderDecks[folderName] = new List<GroupedElement>();

                        folderDecks[folderName].Add(new GroupedElement
                        {
                            GameObject = obj,
                            Label = label,
                            Group = group,
                            FolderName = folderName
                        });
                        continue; // Don't add to standard Content group
                    }
                }

                // Standard element - add to its group
                var groupedElement = new GroupedElement
                {
                    GameObject = obj,
                    Label = label,
                    Group = group
                };

                if (!nonFolderElements.ContainsKey(group))
                    nonFolderElements[group] = new List<GroupedElement>();

                nonFolderElements[group].Add(groupedElement);
            }

            // Extract subgroups (e.g., Objectives) and store separately
            _subgroupElements.Clear();
            if (nonFolderElements.TryGetValue(ElementGroup.Objectives, out var objectivesElements) && objectivesElements.Count > 0)
            {
                _subgroupElements[ElementGroup.Objectives] = new List<GroupedElement>(objectivesElements);
                nonFolderElements.Remove(ElementGroup.Objectives);
                MelonLogger.Msg($"[GroupedNavigator] Stored {objectivesElements.Count} objectives as subgroup");
            }

            // Build ordered group list
            // Note: PlayBladeTabs comes before PlayBladeContent so tabs are shown first
            var groupOrder = new[]
            {
                ElementGroup.Play,
                ElementGroup.Progress,
                // Objectives is handled as a subgroup within Progress, not top-level
                ElementGroup.Social,
                ElementGroup.Primary,
                ElementGroup.Content,
                ElementGroup.Settings,
                ElementGroup.Filters,
                ElementGroup.Secondary,
                ElementGroup.Popup,
                ElementGroup.FriendsPanel,
                ElementGroup.FriendsPanelChallenge,
                ElementGroup.FriendsPanelAddFriend,
                ElementGroup.FriendSectionFriends,
                ElementGroup.FriendSectionIncoming,
                ElementGroup.FriendSectionOutgoing,
                ElementGroup.FriendSectionBlocked,
                ElementGroup.MailboxList,
                ElementGroup.MailboxContent,
                ElementGroup.PlayBladeTabs,
                ElementGroup.PlayBladeContent,
                ElementGroup.ChallengeMain,
                ElementGroup.SettingsMenu,
                ElementGroup.NPE,
                ElementGroup.DeckBuilderCollection,
                ElementGroup.DeckBuilderDeckList,
                ElementGroup.Unknown
            };

            // Add standard groups (except Content if we have folders)
            bool hasFolders = folderDecks.Values.Any(list => list.Count > 0);

            foreach (var groupType in groupOrder)
            {
                // Note: We no longer skip Content when hasFolders is true.
                // Deck elements in folders are already excluded from nonFolderElements (they hit 'continue' earlier).
                // Content may still have non-deck elements (dropdowns, color filters, buttons) that should be accessible.

                if (nonFolderElements.TryGetValue(groupType, out var elementList) && elementList.Count > 0)
                {
                    // Primary and Content elements become standalone items at group level
                    // Note: Play is a regular group (not standalone) containing all play-related elements
                    if (groupType == ElementGroup.Primary || groupType == ElementGroup.Content)
                    {
                        foreach (var element in elementList)
                        {
                            _groups.Add(new ElementGroupInfo
                            {
                                Group = groupType,
                                DisplayName = element.Label, // Use element's label as display name
                                Elements = new List<GroupedElement> { element },
                                IsFolderGroup = false,
                                FolderToggle = null,
                                IsStandaloneElement = true
                            });
                        }
                    }
                    else if (elementList.Count == 1 && !groupType.IsFriendSectionGroup())
                    {
                        // Single element - show standalone instead of creating a group
                        // Exception: Friend section groups always show as proper groups
                        // (section name provides context, left/right sub-navigation for actions)
                        _groups.Add(new ElementGroupInfo
                        {
                            Group = groupType,
                            DisplayName = elementList[0].Label,
                            Elements = elementList,
                            IsFolderGroup = false,
                            FolderToggle = null,
                            IsStandaloneElement = true
                        });
                    }
                    else
                    {
                        // For Progress group, add Objectives as a subgroup entry if we have objectives
                        if (groupType == ElementGroup.Progress && _subgroupElements.TryGetValue(ElementGroup.Objectives, out var objectives) && objectives.Count > 0)
                        {
                            // Create a copy of the element list and add Objectives subgroup entry
                            var elementsWithSubgroup = new List<GroupedElement>(elementList);
                            elementsWithSubgroup.Add(new GroupedElement
                            {
                                GameObject = null, // No physical object, this is a virtual entry
                                Label = Strings.ObjectivesEntry(Strings.ItemCount(objectives.Count)),
                                Group = ElementGroup.Progress,
                                SubgroupType = ElementGroup.Objectives
                            });

                            _groups.Add(new ElementGroupInfo
                            {
                                Group = groupType,
                                DisplayName = groupType.GetDisplayName(),
                                Elements = elementsWithSubgroup,
                                IsFolderGroup = false,
                                FolderToggle = null,
                                IsStandaloneElement = false
                            });
                        }
                        else
                        {
                            _groups.Add(new ElementGroupInfo
                            {
                                Group = groupType,
                                DisplayName = groupType.GetDisplayName(),
                                Elements = elementList,
                                IsFolderGroup = false,
                                FolderToggle = null,
                                IsStandaloneElement = false
                            });
                        }
                    }
                }
            }

            // Add folder groups
            // In PlayBlade context: Create a single PlayBladeFolders group containing folder selectors
            // Outside PlayBlade: Each folder becomes its own group (current behavior for Decks screen)
            if ((_isPlayBladeContext || _isChallengeContext) && folderToggles.Count > 0)
            {
                // Create folder selector elements for the PlayBladeFolders group
                var folderSelectors = new List<GroupedElement>();
                foreach (var kvp in folderToggles.OrderBy(x => x.Key))
                {
                    string folderName = kvp.Key;
                    var toggle = kvp.Value;
                    int deckCount = folderDecks.TryGetValue(folderName, out var decks) ? decks.Count : 0;

                    folderSelectors.Add(new GroupedElement
                    {
                        GameObject = toggle,
                        Label = $"{folderName}, {deckCount} {(deckCount == 1 ? "deck" : "decks")}",
                        Group = ElementGroup.PlayBladeFolders,
                        FolderName = folderName
                    });
                }

                if (folderSelectors.Count > 0)
                {
                    _groups.Add(new ElementGroupInfo
                    {
                        Group = ElementGroup.PlayBladeFolders,
                        DisplayName = ElementGroup.PlayBladeFolders.GetDisplayName(),
                        Elements = folderSelectors,
                        IsFolderGroup = false,
                        FolderToggle = null,
                        IsStandaloneElement = false
                    });
                    MelonLogger.Msg($"[GroupedNavigator] Created PlayBladeFolders group with {folderSelectors.Count} folders");
                }

                // Also create individual folder groups (hidden at top level, but needed for folder entry)
                foreach (var kvp in folderDecks.OrderBy(x => x.Key))
                {
                    string folderName = kvp.Key;
                    var deckList = kvp.Value;
                    GameObject toggle = folderToggles.TryGetValue(folderName, out var t) ? t : null;
                    if (toggle == null && deckList.Count == 0) continue;

                    _groups.Add(new ElementGroupInfo
                    {
                        Group = ElementGroup.Content,
                        DisplayName = folderName,
                        Elements = deckList,
                        IsFolderGroup = true,
                        FolderToggle = toggle,
                        IsStandaloneElement = false
                    });
                    MelonLogger.Msg($"[GroupedNavigator] Created folder group: {folderName} with {deckList.Count} decks");
                }
            }
            else
            {
                // Not in PlayBlade context: each folder becomes its own group at top level
                // NOTE: We create folder groups even when they appear empty, because the decks inside
                // may not be activeInHierarchy when the folder toggle is OFF (collapsed).
                foreach (var kvp in folderDecks.OrderBy(x => x.Key))
                {
                    string folderName = kvp.Key;
                    var deckList = kvp.Value;

                    GameObject toggle = folderToggles.TryGetValue(folderName, out var t) ? t : null;
                    if (toggle == null && deckList.Count == 0) continue;

                    _groups.Add(new ElementGroupInfo
                    {
                        Group = ElementGroup.Content,
                        DisplayName = folderName,
                        Elements = deckList,
                        IsFolderGroup = true,
                        FolderToggle = toggle,
                        IsStandaloneElement = false
                    });

                    MelonLogger.Msg($"[GroupedNavigator] Created folder group: {folderName} with {deckList.Count} decks (toggle: {(toggle != null ? "found" : "none")})");
                }
            }

            // Post-process PlayBladeTabs: inject queue type subgroup entries
            NeedsFollowUpRescan = false;
            PostProcessPlayBladeTabs();

            // Set initial position
            if (_groups.Count > 0)
            {
                _currentGroupIndex = 0;
                // Auto-enter only when there's a single group
                if (_groups.Count == 1)
                {
                    _navigationLevel = NavigationLevel.InsideGroup;
                    _currentElementIndex = 0;
                }
            }

            // Check for pending folder entry (set by EnterGroup before rescan)
            bool enteredPendingFolder = false;
            if (!string.IsNullOrEmpty(_pendingFolderEntry))
            {
                // Find the folder and auto-enter it
                for (int i = 0; i < _groups.Count; i++)
                {
                    if (_groups[i].IsFolderGroup && _groups[i].DisplayName == _pendingFolderEntry)
                    {
                        _currentGroupIndex = i;
                        _navigationLevel = NavigationLevel.InsideGroup;
                        _currentElementIndex = 0;
                        enteredPendingFolder = true;
                        MelonLogger.Msg($"[GroupedNavigator] Auto-entered pending folder: {_pendingFolderEntry} with {_groups[i].Count} items");
                        break;
                    }
                }
                _pendingFolderEntry = null; // Clear after processing
            }

            // Check for pending PlayBlade tabs entry (set when PlayBlade opens)
            bool playBladeAutoEntryPerformed = false;
            if (_pendingPlayBladeTabsEntry)
            {
                _pendingPlayBladeTabsEntry = false;
                int restoreIndex = _pendingPlayBladeTabsEntryIndex;
                _pendingPlayBladeTabsEntryIndex = -1;
                // Find PlayBladeTabs group and auto-enter it
                for (int i = 0; i < _groups.Count; i++)
                {
                    if (_groups[i].Group == ElementGroup.PlayBladeTabs && _groups[i].Count > 0)
                    {
                        _currentGroupIndex = i;
                        _navigationLevel = NavigationLevel.InsideGroup;
                        // Use restore index if valid, otherwise start at 0
                        int maxIdx = _groups[i].Count - 1;
                        _currentElementIndex = (restoreIndex >= 0 && restoreIndex <= maxIdx) ? restoreIndex : 0;
                        playBladeAutoEntryPerformed = true;
                        MelonLogger.Msg($"[GroupedNavigator] Auto-entered PlayBladeTabs with {_groups[i].Count} items at index {_currentElementIndex}");
                        break;
                    }
                }
            }

            // Check for pending PlayBlade content entry (set when a tab is activated or spinner rescan)
            if (_pendingPlayBladeContentEntry)
            {
                _pendingPlayBladeContentEntry = false;
                int requestedIndex = _pendingPlayBladeContentEntryIndex;
                _pendingPlayBladeContentEntryIndex = -1;
                // Find PlayBladeContent group and auto-enter it
                for (int i = 0; i < _groups.Count; i++)
                {
                    if (_groups[i].Group == ElementGroup.PlayBladeContent && _groups[i].Count > 0)
                    {
                        _currentGroupIndex = i;
                        _navigationLevel = NavigationLevel.InsideGroup;
                        // Use requested index if valid, otherwise start at 0
                        int maxIdx = _groups[i].Count - 1;
                        _currentElementIndex = (requestedIndex >= 0 && requestedIndex <= maxIdx) ? requestedIndex : 0;
                        playBladeAutoEntryPerformed = true;
                        MelonLogger.Msg($"[GroupedNavigator] Auto-entered PlayBladeContent with {_groups[i].Count} items at index {_currentElementIndex}");
                        break;
                    }
                }
            }

            // Check for pending ChallengeMain entry (set when challenge opens or returning from deck selection)
            bool challengeAutoEntryPerformed = false;
            if (_pendingChallengeMainEntry)
            {
                _pendingChallengeMainEntry = false;
                int requestedIndex = _pendingChallengeMainEntryIndex;
                _pendingChallengeMainEntryIndex = -1;
                // Find ChallengeMain group and auto-enter it
                for (int i = 0; i < _groups.Count; i++)
                {
                    if (_groups[i].Group == ElementGroup.ChallengeMain && _groups[i].Count > 0)
                    {
                        _currentGroupIndex = i;
                        _navigationLevel = NavigationLevel.InsideGroup;
                        int maxIdx = _groups[i].Count - 1;
                        _currentElementIndex = (requestedIndex >= 0 && requestedIndex <= maxIdx) ? requestedIndex : 0;
                        challengeAutoEntryPerformed = true;
                        MelonLogger.Msg($"[GroupedNavigator] Auto-entered ChallengeMain with {_groups[i].Count} items at index {_currentElementIndex}");
                        break;
                    }
                }
            }

            // Check for pending queue type activation (set when user enters a virtual queue type entry)
            if (_pendingQueueTypeActivation != null)
            {
                string queueType = _pendingQueueTypeActivation;
                _pendingQueueTypeActivation = null;

                // Find the real queue type tab in PlayBladeTabs
                for (int i = 0; i < _groups.Count; i++)
                {
                    if (_groups[i].Group != ElementGroup.PlayBladeTabs) continue;
                    for (int j = 0; j < _groups[i].Elements.Count; j++)
                    {
                        var elem = _groups[i].Elements[j];
                        if (elem.FolderName == queueType && elem.GameObject != null)
                        {
                            UIActivator.Activate(elem.GameObject);
                            _pendingPlayBladeContentEntry = true;
                            _lastQueueTypeTabIndex = j;
                            NeedsFollowUpRescan = true;
                            MelonLogger.Msg($"[GroupedNavigator] Pending queue type '{queueType}' found at index {j}, clicked and requesting content entry");
                            break;
                        }
                    }
                    break;
                }
            }

            // Check for pending first folder entry (set when a play mode is activated)
            if (_pendingFirstFolderEntry)
            {
                _pendingFirstFolderEntry = false;
                // Find first folder group and auto-enter it
                for (int i = 0; i < _groups.Count; i++)
                {
                    if (_groups[i].IsFolderGroup && _groups[i].Count > 0)
                    {
                        _currentGroupIndex = i;
                        _navigationLevel = NavigationLevel.InsideGroup;
                        _currentElementIndex = 0;
                        MelonLogger.Msg($"[GroupedNavigator] Auto-entered folder '{_groups[i].DisplayName}' with {_groups[i].Count} items");
                        break;
                    }
                }
            }

            // Check for pending PlayBladeFolders entry (set when a play mode is activated in PlayBlade)
            if (_pendingFoldersEntry)
            {
                _pendingFoldersEntry = false;
                // Find PlayBladeFolders group and auto-enter it
                bool foundFolders = false;
                for (int i = 0; i < _groups.Count; i++)
                {
                    if (_groups[i].Group == ElementGroup.PlayBladeFolders)
                    {
                        if (_groups[i].Count > 0)
                        {
                            _currentGroupIndex = i;
                            _navigationLevel = NavigationLevel.InsideGroup;
                            _currentElementIndex = 0;
                            MelonLogger.Msg($"[GroupedNavigator] Auto-entered PlayBladeFolders with {_groups[i].Count} folders");
                            foundFolders = true;
                        }
                        else
                        {
                            MelonLogger.Msg($"[GroupedNavigator] PlayBladeFolders group exists but is empty - cannot auto-enter");
                        }
                        break;
                    }
                }
                if (!foundFolders)
                {
                    MelonLogger.Msg($"[GroupedNavigator] PlayBladeFolders group not found - pending folders entry ignored");
                }
            }

            // Check for pending specific folder entry (set when user selects a folder from PlayBladeFolders)
            if (!string.IsNullOrEmpty(_pendingSpecificFolderEntry))
            {
                string folderName = _pendingSpecificFolderEntry;
                _pendingSpecificFolderEntry = null;
                // Find the specific folder group and auto-enter it
                for (int i = 0; i < _groups.Count; i++)
                {
                    if (_groups[i].IsFolderGroup && _groups[i].DisplayName == folderName)
                    {
                        _currentGroupIndex = i;
                        _navigationLevel = NavigationLevel.InsideGroup;
                        _currentElementIndex = 0;
                        MelonLogger.Msg($"[GroupedNavigator] Auto-entered specific folder '{folderName}' with {_groups[i].Count} items");
                        break;
                    }
                }
            }

            // Check for pending group restore (set by SaveCurrentGroupForRestore before rescan)
            // Skip group restore in PlayBlade context or when we just entered a pending folder
            // Restoring old state would interfere with the intended navigation flow
            if (_pendingGroupRestore.HasValue)
            {
                if (_isPlayBladeContext || _isChallengeContext || enteredPendingFolder || playBladeAutoEntryPerformed || challengeAutoEntryPerformed)
                {
                    // Clear stale restore state - auto-entries take precedence
                    string reason = challengeAutoEntryPerformed ? "Challenge auto-entry" : (playBladeAutoEntryPerformed ? "PlayBlade auto-entry" : (enteredPendingFolder ? "folder entry" : (_isChallengeContext ? "Challenge context" : "PlayBlade context")));
                    MelonLogger.Msg($"[GroupedNavigator] Skipping group restore due to {reason} (was: {_pendingGroupRestore.Value})");
                    _pendingGroupRestore = null;
                    _pendingLevelRestore = NavigationLevel.GroupList;
                    _pendingElementIndexRestore = -1;
                }
                else
                {
                    var groupToRestore = _pendingGroupRestore.Value;
                    var levelToRestore = _pendingLevelRestore;
                    var elementIndexToRestore = _pendingElementIndexRestore;
                    _pendingGroupRestore = null;
                    _pendingLevelRestore = NavigationLevel.GroupList;
                    _pendingElementIndexRestore = -1;

                    // Find the group by type
                    bool found = false;
                    for (int i = 0; i < _groups.Count; i++)
                    {
                        if (_groups[i].Group == groupToRestore)
                        {
                            _currentGroupIndex = i;
                            found = true;
                            if (levelToRestore == NavigationLevel.InsideGroup)
                            {
                                _navigationLevel = NavigationLevel.InsideGroup;
                                // Restore element index, clamped to valid range (in case group shrunk)
                                int maxIndex = _groups[i].Count - 1;
                                if (elementIndexToRestore >= 0 && maxIndex >= 0)
                                {
                                    _currentElementIndex = Math.Min(elementIndexToRestore, maxIndex);
                                }
                                else
                                {
                                    _currentElementIndex = 0;
                                }
                                MelonLogger.Msg($"[GroupedNavigator] Restored into group '{_groups[i].DisplayName}' at index {_currentElementIndex} (requested {elementIndexToRestore}, max {maxIndex})");
                            }
                            else
                            {
                                _navigationLevel = NavigationLevel.GroupList;
                                MelonLogger.Msg($"[GroupedNavigator] Restored to group list at '{_groups[i].DisplayName}'");
                            }
                            break;
                        }
                    }

                    if (!found)
                    {
                        MelonLogger.Msg($"[GroupedNavigator] Could not restore group {groupToRestore} - not found after rescan");
                    }
                }
            }

            MelonLogger.Msg($"[GroupedNavigator] Organized into {_groups.Count} groups");
            foreach (var g in _groups)
            {
                string folderInfo = g.IsFolderGroup ? " (folder)" : "";
                MelonLogger.Msg($"  - {g.DisplayName}: {g.Count} items{folderInfo}");
            }
        }

        /// <summary>
        /// Post-process PlayBladeTabs group after initial build:
        /// 1. Find and store the FindMatch nav tab reference (even though it's excluded from tabs)
        /// 2. Mark real queue type tabs as subgroup entries (if FindMatch is active)
        /// 3. Inject virtual subgroup entries for queue types (if FindMatch is not active)
        /// </summary>
        private void PostProcessPlayBladeTabs()
        {
            // Find the PlayBladeTabs group
            int tabsGroupIdx = -1;
            for (int i = 0; i < _groups.Count; i++)
            {
                if (_groups[i].Group == ElementGroup.PlayBladeTabs)
                {
                    tabsGroupIdx = i;
                    break;
                }
            }

            if (tabsGroupIdx < 0)
                return; // No PlayBladeTabs group - nothing to do

            var tabsGroup = _groups[tabsGroupIdx];

            // Try to find FindMatch tab object by scanning all discovered elements
            // (it was excluded from PlayBladeTabs by IsPlayBladeTab, but we still need its GameObject)
            if (_findMatchTabObject == null || !_findMatchTabObject.activeInHierarchy)
            {
                _findMatchTabObject = FindMatchTabInHierarchy();
            }

            // Check if real queue type tabs are present (FindMatch is active)
            bool hasRealQueueTabs = false;
            for (int j = 0; j < tabsGroup.Elements.Count; j++)
            {
                var elem = tabsGroup.Elements[j];
                if (elem.GameObject != null &&
                    (elem.GameObject.name.StartsWith("Blade_Tab_Ranked") ||
                     elem.GameObject.name.StartsWith("Blade_Tab_Deluxe")))
                {
                    hasRealQueueTabs = true;
                    break;
                }
            }

            if (hasRealQueueTabs)
            {
                // Mark real queue type tabs with SubgroupType and FolderName
                var updatedElements = new List<GroupedElement>(tabsGroup.Elements);
                for (int j = 0; j < updatedElements.Count; j++)
                {
                    var elem = updatedElements[j];
                    if (elem.GameObject == null) continue;

                    string queueType = GetQueueTypeFromTabName(elem.GameObject.name);
                    if (queueType != null)
                    {
                        elem.SubgroupType = ElementGroup.PlayBladeContent;
                        elem.FolderName = queueType;
                        updatedElements[j] = elem;
                        MelonLogger.Msg($"[GroupedNavigator] Marked real queue tab '{elem.Label}' as subgroup entry (FolderName={queueType})");
                    }
                }

                // Update the group with modified elements
                tabsGroup.Elements = updatedElements;
                _groups[tabsGroupIdx] = tabsGroup;
            }
            else
            {
                // No real queue tabs - inject virtual subgroup entries
                // Find insertion point: after last nav tab (Events) and before Recent
                var updatedElements = new List<GroupedElement>(tabsGroup.Elements);
                int insertIdx = FindQueueTypeInsertionIndex(updatedElements);

                var virtualEntries = new[]
                {
                    new GroupedElement
                    {
                        GameObject = null,
                        Label = "Ranked",
                        Group = ElementGroup.PlayBladeTabs,
                        SubgroupType = ElementGroup.PlayBladeContent,
                        FolderName = "Ranked"
                    },
                    new GroupedElement
                    {
                        GameObject = null,
                        Label = "Open Play",
                        Group = ElementGroup.PlayBladeTabs,
                        SubgroupType = ElementGroup.PlayBladeContent,
                        FolderName = "OpenPlay"
                    },
                    new GroupedElement
                    {
                        GameObject = null,
                        Label = "Brawl",
                        Group = ElementGroup.PlayBladeTabs,
                        SubgroupType = ElementGroup.PlayBladeContent,
                        FolderName = "Brawl"
                    }
                };

                for (int k = 0; k < virtualEntries.Length; k++)
                {
                    updatedElements.Insert(insertIdx + k, virtualEntries[k]);
                }

                // Update the group
                tabsGroup.Elements = updatedElements;
                _groups[tabsGroupIdx] = tabsGroup;
                MelonLogger.Msg($"[GroupedNavigator] Injected 3 virtual queue type entries at index {insertIdx}");
            }
        }

        /// <summary>
        /// Map a tab GameObject name to a queue type identifier.
        /// Returns null if the tab is not a queue type tab.
        /// </summary>
        private static string GetQueueTypeFromTabName(string tabName)
        {
            if (tabName.StartsWith("Blade_Tab_Ranked"))
                return "Ranked";
            if (tabName.StartsWith("Blade_Tab_Deluxe"))
            {
                // "Blade_Tab_Deluxe (OpenPlay)" or "Blade_Tab_Deluxe (Brawl)"
                int parenStart = tabName.IndexOf('(');
                int parenEnd = tabName.IndexOf(')');
                if (parenStart > 0 && parenEnd > parenStart)
                {
                    string mode = tabName.Substring(parenStart + 1, parenEnd - parenStart - 1);
                    switch (mode.ToLowerInvariant())
                    {
                        case "openplay": return "OpenPlay";
                        case "brawl": return "Brawl";
                        default: return mode;
                    }
                }
                return "OpenPlay"; // Default for unrecognized Deluxe tab
            }
            return null;
        }

        /// <summary>
        /// Find the insertion index for virtual queue type entries in the tabs list.
        /// Inserts after the first nav tab (Events) and before the last (Recent).
        /// If only one nav tab exists, inserts after it.
        /// </summary>
        private static int FindQueueTypeInsertionIndex(List<GroupedElement> elements)
        {
            // Find the last element that looks like a "non-Recent" nav tab
            // Recent is typically the last nav tab. Events is typically the first.
            // Insert after Events (index 1) if we have at least one tab.
            if (elements.Count == 0)
                return 0;

            // The first element should be Events. Insert after it.
            // If there are 2+ elements, the last is typically Recent - insert before it.
            if (elements.Count >= 2)
                return elements.Count - 1; // Before last element (Recent)

            return elements.Count; // After the only element
        }

        /// <summary>
        /// Try to find the FindMatch tab by walking the Unity hierarchy.
        /// Looks for inactive or excluded FindMatch nav tab GameObjects.
        /// </summary>
        private static GameObject FindMatchTabInHierarchy()
        {
            // Search for Blade_Tab_Nav objects that contain "FindMatch"
            var allObjects = UnityEngine.Object.FindObjectsOfType<RectTransform>(true);
            foreach (var rt in allObjects)
            {
                if (rt.gameObject.name.Contains("Blade_Tab_Nav") &&
                    rt.gameObject.name.Contains("FindMatch"))
                {
                    MelonLogger.Msg($"[GroupedNavigator] Found FindMatch tab: {rt.gameObject.name}");
                    return rt.gameObject;
                }
            }
            return null;
        }

        /// <summary>
        /// Add a virtual group (no physical GameObjects) after OrganizeIntoGroups.
        /// Virtual groups contain informational elements that exist only for
        /// screen reader navigation, not tied to any Unity GameObject.
        /// Inserts after the specified target group type, or at end if not found.
        /// </summary>
        public void AddVirtualGroup(ElementGroup group, List<GroupedElement> elements, ElementGroup? insertAfter = null)
        {
            if (elements == null || elements.Count == 0)
                return;

            var groupInfo = new ElementGroupInfo
            {
                Group = group,
                DisplayName = group.GetDisplayName(),
                Elements = elements,
                IsFolderGroup = false,
                FolderToggle = null,
                IsStandaloneElement = false
            };

            if (insertAfter.HasValue)
            {
                // Find the last group matching insertAfter type and insert after it
                for (int i = _groups.Count - 1; i >= 0; i--)
                {
                    if (_groups[i].Group == insertAfter.Value)
                    {
                        _groups.Insert(i + 1, groupInfo);
                        MelonLogger.Msg($"[GroupedNavigator] Added virtual group '{group.GetDisplayName()}' with {elements.Count} items after {insertAfter.Value.GetDisplayName()}");
                        return;
                    }
                }
            }

            // Fallback: append at end
            _groups.Add(groupInfo);
            MelonLogger.Msg($"[GroupedNavigator] Added virtual group '{group.GetDisplayName()}' with {elements.Count} items at end");
        }

        /// <summary>
        /// Update the label of an element within a group.
        /// Used to refresh virtual element labels with fresh data.
        /// Handles struct semantics by writing back to the list index.
        /// </summary>
        public void UpdateElementLabel(ElementGroup groupType, int elementIndex, string newLabel)
        {
            for (int i = 0; i < _groups.Count; i++)
            {
                if (_groups[i].Group == groupType && elementIndex >= 0 && elementIndex < _groups[i].Count)
                {
                    var element = _groups[i].Elements[elementIndex];
                    element.Label = newLabel;
                    _groups[i].Elements[elementIndex] = element;
                    return;
                }
            }
        }

        /// <summary>
        /// Clear all groups and reset state.
        /// </summary>
        public void Clear()
        {
            _groups.Clear();
            _currentGroupIndex = -1;
            _currentElementIndex = -1;
            _navigationLevel = NavigationLevel.GroupList;
        }

        /// <summary>
        /// Auto-enter single-item groups or the Primary group.
        /// </summary>
        private void AutoEnterIfSingleItem()
        {
            if (_currentGroupIndex < 0 || _currentGroupIndex >= _groups.Count)
                return;

            var group = _groups[_currentGroupIndex];

            // Auto-enter if single item in group
            if (group.Count == 1)
            {
                _navigationLevel = NavigationLevel.InsideGroup;
                _currentElementIndex = 0;
            }
            // Auto-enter Primary group (user likely wants the main action)
            else if (group.Group == ElementGroup.Primary)
            {
                _navigationLevel = NavigationLevel.InsideGroup;
                _currentElementIndex = 0;
            }
        }

        /// <summary>
        /// Move to next item (group or element depending on level).
        /// </summary>
        /// <returns>True if moved, false if at end.</returns>
        public bool MoveNext()
        {
            if (_navigationLevel == NavigationLevel.GroupList)
                return MoveNextGroup();
            else
                return MoveNextElement();
        }

        /// <summary>
        /// Move to previous item (group or element depending on level).
        /// </summary>
        /// <returns>True if moved, false if at beginning.</returns>
        public bool MovePrevious()
        {
            if (_navigationLevel == NavigationLevel.GroupList)
                return MovePreviousGroup();
            else
                return MovePreviousElement();
        }

        /// <summary>
        /// Move to first item at current level.
        /// </summary>
        public void MoveFirst()
        {
            if (_navigationLevel == NavigationLevel.GroupList)
            {
                if (_groups.Count > 0)
                    _currentGroupIndex = 0;
            }
            else
            {
                _currentElementIndex = 0;
            }
        }

        /// <summary>
        /// Move to last item at current level.
        /// </summary>
        public void MoveLast()
        {
            if (_navigationLevel == NavigationLevel.GroupList)
            {
                if (_groups.Count > 0)
                    _currentGroupIndex = _groups.Count - 1;
            }
            else
            {
                int count = GetCurrentElementCount();
                if (count > 0)
                    _currentElementIndex = count - 1;
            }
        }

        /// <summary>
        /// Enter the current group (start navigating its elements).
        /// </summary>
        /// <returns>True if entered, false if already inside or invalid.</returns>
        public bool EnterGroup()
        {
            if (_navigationLevel == NavigationLevel.InsideGroup)
                return false;

            if (_currentGroupIndex < 0 || _currentGroupIndex >= _groups.Count)
                return false;

            var group = _groups[_currentGroupIndex];

            // Note: Folder toggle activation is now handled by GeneralMenuNavigator.HandleGroupedEnter()
            // which activates the toggle through the normal element activation path (triggering rescans).

            // For folder groups, set pending entry so we auto-enter after the rescan
            // (the rescan resets navigation state, so we need to remember where to go)
            if (group.IsFolderGroup)
            {
                _pendingFolderEntry = group.DisplayName;
                MelonLogger.Msg($"[GroupedNavigator] Set pending folder entry: {_pendingFolderEntry}");
            }

            // Allow entering even if currently empty - folder toggle activation
            // may reveal elements that weren't visible before
            if (group.Count == 0 && !group.IsFolderGroup)
                return false; // Only block entry for non-folder empty groups

            _navigationLevel = NavigationLevel.InsideGroup;
            _currentElementIndex = 0;
            return true;
        }

        /// <summary>
        /// Activate a folder toggle to show only that folder's decks.
        /// This simulates the sighted user experience of clicking on a folder.
        /// Uses UIActivator to go through normal activation path which triggers rescans.
        /// </summary>
        private void ActivateFolderToggle(GameObject folderToggle, string folderName)
        {
            if (folderToggle == null) return;

            var toggle = folderToggle.GetComponent<UnityEngine.UI.Toggle>();
            if (toggle != null && !toggle.isOn)
            {
                // Use UIActivator to trigger normal activation path (which triggers rescans)
                UIActivator.Activate(folderToggle);
                MelonLogger.Msg($"[GroupedNavigator] Activated folder toggle: {folderName}");
            }
        }

        /// <summary>
        /// Exit the current group (return to group list).
        /// </summary>
        /// <returns>True if exited, false if already at group level.</returns>
        public bool ExitGroup()
        {
            if (_navigationLevel == NavigationLevel.GroupList)
                return false;

            // NOTE: We intentionally do NOT deactivate the folder toggle when exiting.
            // In PlayBlade, collapsing the deck folder (setting toggle.isOn = false)
            // causes the game to deselect the chosen deck, breaking the play workflow.
            // Keeping the folder expanded preserves the deck selection state.

            _navigationLevel = NavigationLevel.GroupList;
            _currentElementIndex = -1;
            return true;
        }

        /// <summary>
        /// Deactivate a folder toggle when exiting the folder group.
        /// </summary>
        private void DeactivateFolderToggle(GameObject folderToggle, string folderName)
        {
            if (folderToggle == null) return;

            var toggle = folderToggle.GetComponent<Toggle>();
            if (toggle != null && toggle.isOn)
            {
                toggle.isOn = false;
                MelonLogger.Msg($"[GroupedNavigator] Deactivated folder toggle: {folderName}");
            }
        }

        /// <summary>
        /// Check if the current element is a subgroup entry (e.g., Objectives within Progress).
        /// </summary>
        public bool IsCurrentElementSubgroupEntry()
        {
            if (_navigationLevel != NavigationLevel.InsideGroup)
                return false;

            var element = GetCurrentElement();
            return element.HasValue && element.Value.SubgroupType.HasValue;
        }

        /// <summary>
        /// Check if currently inside a subgroup.
        /// </summary>
        public bool IsInsideSubgroup => _currentSubgroup.HasValue;

        /// <summary>
        /// Enter a subgroup from the current element.
        /// </summary>
        /// <returns>True if entered subgroup, false otherwise.</returns>
        public bool EnterSubgroup()
        {
            if (_navigationLevel != NavigationLevel.InsideGroup)
                return false;

            var element = GetCurrentElement();
            if (!element.HasValue || !element.Value.SubgroupType.HasValue)
                return false;

            var subgroupType = element.Value.SubgroupType.Value;
            if (!_subgroupElements.TryGetValue(subgroupType, out var subgroupElements) || subgroupElements.Count == 0)
                return false;

            // Store parent state for returning
            _subgroupParentIndex = _currentGroupIndex;
            _currentSubgroup = subgroupType;
            _currentElementIndex = 0;

            MelonLogger.Msg($"[GroupedNavigator] Entered subgroup: {subgroupType.GetDisplayName()} with {subgroupElements.Count} items");
            return true;
        }

        /// <summary>
        /// Exit the current subgroup and return to the parent group.
        /// </summary>
        /// <returns>True if exited subgroup, false if not in a subgroup.</returns>
        public bool ExitSubgroup()
        {
            if (!_currentSubgroup.HasValue)
                return false;

            MelonLogger.Msg($"[GroupedNavigator] Exiting subgroup: {_currentSubgroup.Value.GetDisplayName()}");

            // Restore parent group state
            _currentGroupIndex = _subgroupParentIndex;
            _currentSubgroup = null;
            _subgroupParentIndex = -1;

            // Find the subgroup entry element index in the parent group
            var group = _groups[_currentGroupIndex];
            for (int i = 0; i < group.Elements.Count; i++)
            {
                if (group.Elements[i].SubgroupType.HasValue)
                {
                    _currentElementIndex = i;
                    break;
                }
            }

            return true;
        }

        /// <summary>
        /// Get the elements for the current subgroup.
        /// </summary>
        private List<GroupedElement> GetCurrentSubgroupElements()
        {
            if (!_currentSubgroup.HasValue)
                return null;

            _subgroupElements.TryGetValue(_currentSubgroup.Value, out var elements);
            return elements;
        }

        /// <summary>
        /// Get announcement for current position.
        /// </summary>
        public string GetCurrentAnnouncement()
        {
            if (_navigationLevel == NavigationLevel.GroupList)
                return GetGroupAnnouncement();
            else
                return GetElementAnnouncement();
        }

        /// <summary>
        /// Get the screen/menu announcement with group summary.
        /// </summary>
        public string GetActivationAnnouncement(string screenName)
        {
            if (_groups.Count == 0)
                return $"{screenName}. {Strings.NoItemsFound}";

            if (_groups.Count == 1)
            {
                // Single group - auto-entered, announce first element
                var group = _groups[0];
                var firstElem = group.Elements.Count > 0 ? group.Elements[0] : (GroupedElement?)null;
                string firstLabel = firstElem.HasValue
                    ? BaseNavigator.RefreshElementLabel(firstElem.Value.GameObject, firstElem.Value.Label)
                    : "";
                return Strings.ScreenItemsSummary(screenName, Strings.ItemCount(group.Count),
                    Strings.ItemPositionOf(1, group.Count, firstLabel));
            }

            return Strings.ScreenGroupsSummary(screenName, Strings.GroupCount(_groups.Count), GetCurrentAnnouncement());
        }

        private bool MoveNextGroup()
        {
            if (_currentGroupIndex >= _groups.Count - 1)
            {
                // Single group: re-announce it before saying end of list
                if (_groups.Count == 1)
                    AnnounceCurrentGroup();
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                return false;
            }

            _currentGroupIndex++;
            AnnounceCurrentGroup();
            return true;
        }

        private bool MovePreviousGroup()
        {
            if (_currentGroupIndex <= 0)
            {
                // Single group: re-announce it before saying beginning of list
                if (_groups.Count == 1)
                    AnnounceCurrentGroup();
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return false;
            }

            _currentGroupIndex--;
            AnnounceCurrentGroup();
            return true;
        }

        private bool MoveNextElement()
        {
            int count = GetCurrentElementCount();
            if (count == 0) return false;

            if (_currentElementIndex >= count - 1)
            {
                // Single element: re-announce it before saying end of list
                if (count == 1)
                    AnnounceCurrentElement();
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                return false;
            }

            _currentElementIndex++;
            AnnounceCurrentElement();
            return true;
        }

        private bool MovePreviousElement()
        {
            if (_currentElementIndex <= 0)
            {
                // Single element: re-announce it before saying beginning of list
                int count = GetCurrentElementCount();
                if (count == 1)
                    AnnounceCurrentElement();
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return false;
            }

            _currentElementIndex--;
            AnnounceCurrentElement();
            return true;
        }

        private string GetGroupAnnouncement()
        {
            if (_currentGroupIndex < 0 || _currentGroupIndex >= _groups.Count)
                return "";

            var group = _groups[_currentGroupIndex];

            // Standalone elements show just their label (they're directly activatable)
            if (group.IsStandaloneElement)
                return group.DisplayName;

            return Strings.GroupItemCount(group.DisplayName, Strings.ItemCount(group.Count));
        }

        private string GetElementAnnouncement()
        {
            var element = CurrentElement;
            if (!element.HasValue) return "";

            int count = GetCurrentElementCount();
            string label = BaseNavigator.RefreshElementLabel(element.Value.GameObject, element.Value.Label);
            return Strings.ItemPositionOf(_currentElementIndex + 1, count, label);
        }

        private void AnnounceCurrentGroup()
        {
            _announcer.AnnounceInterrupt(GetGroupAnnouncement());
        }

        private void AnnounceCurrentElement()
        {
            _announcer.AnnounceInterrupt(GetElementAnnouncement());
        }

        /// <summary>
        /// Filter groups to only show those for a specific overlay.
        /// Call when an overlay is detected.
        /// </summary>
        public void FilterToOverlay(ElementGroup overlayGroup)
        {
            _groups = _groups.Where(g => g.Group == overlayGroup).ToList();

            if (_groups.Count > 0)
            {
                _currentGroupIndex = 0;
                AutoEnterIfSingleItem();
            }
            else
            {
                _currentGroupIndex = -1;
                _currentElementIndex = -1;
                _navigationLevel = NavigationLevel.GroupList;
            }
        }

        /// <summary>
        /// Check if any group contains elements (for validation).
        /// </summary>
        public bool HasElements => _groups.Any(g => g.Count > 0);

        /// <summary>
        /// Get all elements flattened (for compatibility with existing code).
        /// </summary>
        public IEnumerable<GroupedElement> GetAllElements()
        {
            return _groups.SelectMany(g => g.Elements);
        }

        /// <summary>
        /// Jump to a specific group by ElementGroup type.
        /// Sets navigation to group level at the specified group.
        /// </summary>
        /// <returns>True if group was found and jumped to, false otherwise.</returns>
        public bool JumpToGroup(ElementGroup groupType)
        {
            for (int i = 0; i < _groups.Count; i++)
            {
                if (_groups[i].Group == groupType)
                {
                    _currentGroupIndex = i;
                    _navigationLevel = NavigationLevel.GroupList;
                    _currentElementIndex = -1;
                    MelonLogger.Msg($"[GroupedNavigator] Jumped to group: {_groups[i].DisplayName}");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Jump to a specific group by display name.
        /// Sets navigation to group level at the specified group.
        /// </summary>
        /// <returns>True if group was found and jumped to, false otherwise.</returns>
        public bool JumpToGroupByName(string displayName)
        {
            for (int i = 0; i < _groups.Count; i++)
            {
                if (_groups[i].DisplayName == displayName)
                {
                    _currentGroupIndex = i;
                    _navigationLevel = NavigationLevel.GroupList;
                    _currentElementIndex = -1;
                    MelonLogger.Msg($"[GroupedNavigator] Jumped to group by name: {displayName}");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Jump to a specific group and enter it (inside group level).
        /// </summary>
        /// <returns>True if group was found and entered, false otherwise.</returns>
        public bool JumpToGroupAndEnter(ElementGroup groupType)
        {
            for (int i = 0; i < _groups.Count; i++)
            {
                if (_groups[i].Group == groupType && _groups[i].Count > 0)
                {
                    _currentGroupIndex = i;
                    _navigationLevel = NavigationLevel.InsideGroup;
                    _currentElementIndex = 0;
                    MelonLogger.Msg($"[GroupedNavigator] Jumped and entered group: {_groups[i].DisplayName}");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Get group info by ElementGroup type.
        /// </summary>
        public ElementGroupInfo? GetGroupByType(ElementGroup groupType)
        {
            return _groups.FirstOrDefault(g => g.Group == groupType);
        }

        /// <summary>
        /// Get group info by display name.
        /// </summary>
        public ElementGroupInfo? GetGroupByName(string displayName)
        {
            return _groups.FirstOrDefault(g => g.DisplayName == displayName);
        }

        /// <summary>
        /// Get element at index from a specific group.
        /// </summary>
        /// <returns>The element's GameObject, or null if not found.</returns>
        public GameObject GetElementFromGroup(ElementGroup groupType, int index)
        {
            var group = GetGroupByType(groupType);
            if (group.HasValue && index >= 0 && index < group.Value.Count)
            {
                return group.Value.Elements[index].GameObject;
            }
            return null;
        }

        /// <summary>
        /// Get element count for a specific group type.
        /// </summary>
        public int GetGroupElementCount(ElementGroup groupType)
        {
            var group = GetGroupByType(groupType);
            return group?.Count ?? 0;
        }

        /// <summary>
        /// Get all groups of a specific type (there may be multiple, e.g., folders).
        /// </summary>
        public IEnumerable<ElementGroupInfo> GetAllGroupsOfType(ElementGroup groupType)
        {
            return _groups.Where(g => g.Group == groupType);
        }

        /// <summary>
        /// Find groups matching a predicate.
        /// </summary>
        public IEnumerable<ElementGroupInfo> FindGroups(System.Func<ElementGroupInfo, bool> predicate)
        {
            return _groups.Where(predicate);
        }

        /// <summary>
        /// Cycle to the next group from a list of allowed group types.
        /// Skips standalone elements (only cycles between actual groups).
        /// Auto-enters the group after cycling.
        /// </summary>
        /// <returns>True if moved to a new group, false if no valid groups found.</returns>
        public bool CycleToNextGroup(params ElementGroup[] allowedGroups)
        {
            if (allowedGroups == null || allowedGroups.Length == 0)
                return false;

            // Find indices of all allowed groups (skip standalone elements)
            var allowedIndices = new List<int>();
            for (int i = 0; i < _groups.Count; i++)
            {
                if (System.Array.IndexOf(allowedGroups, _groups[i].Group) >= 0 &&
                    !_groups[i].IsStandaloneElement && _groups[i].Count > 1)
                    allowedIndices.Add(i);
            }

            if (allowedIndices.Count == 0)
                return false;

            // Find current position in allowed groups
            int currentAllowedIndex = allowedIndices.IndexOf(_currentGroupIndex);

            // Move to next allowed group (wrap around)
            int nextAllowedIndex = (currentAllowedIndex + 1) % allowedIndices.Count;
            _currentGroupIndex = allowedIndices[nextAllowedIndex];

            // Auto-enter the group
            _navigationLevel = NavigationLevel.InsideGroup;
            _currentElementIndex = 0;

            MelonLogger.Msg($"[GroupedNavigator] Cycled to next group and entered: {_groups[_currentGroupIndex].DisplayName}");
            return true;
        }

        /// <summary>
        /// Cycle to the previous group from a list of allowed group types.
        /// Skips standalone elements (only cycles between actual groups).
        /// Auto-enters the group after cycling.
        /// </summary>
        /// <returns>True if moved to a new group, false if no valid groups found.</returns>
        public bool CycleToPreviousGroup(params ElementGroup[] allowedGroups)
        {
            if (allowedGroups == null || allowedGroups.Length == 0)
                return false;

            // Find indices of all allowed groups (skip standalone elements)
            var allowedIndices = new List<int>();
            for (int i = 0; i < _groups.Count; i++)
            {
                if (System.Array.IndexOf(allowedGroups, _groups[i].Group) >= 0 &&
                    !_groups[i].IsStandaloneElement && _groups[i].Count > 1)
                    allowedIndices.Add(i);
            }

            if (allowedIndices.Count == 0)
                return false;

            // Find current position in allowed groups
            int currentAllowedIndex = allowedIndices.IndexOf(_currentGroupIndex);

            // Move to previous allowed group (wrap around)
            int prevAllowedIndex = currentAllowedIndex <= 0
                ? allowedIndices.Count - 1
                : currentAllowedIndex - 1;
            _currentGroupIndex = allowedIndices[prevAllowedIndex];

            // Auto-enter the group
            _navigationLevel = NavigationLevel.InsideGroup;
            _currentElementIndex = 0;

            MelonLogger.Msg($"[GroupedNavigator] Cycled to previous group and entered: {_groups[_currentGroupIndex].DisplayName}");
            return true;
        }
    }
}
