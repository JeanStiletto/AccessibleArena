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
        /// Group type to restore after rescan. Set by SaveCurrentGroupForRestore(),
        /// cleared after OrganizeIntoGroups attempts restoration.
        /// </summary>
        private ElementGroup? _pendingGroupRestore = null;

        /// <summary>
        /// Navigation level to restore after rescan.
        /// </summary>
        private NavigationLevel _pendingLevelRestore = NavigationLevel.GroupList;

        /// <summary>
        /// Card labels from the collection before a page switch.
        /// Used to filter out unchanged cards after paging.
        /// </summary>
        private HashSet<string> _previousCollectionCards = null;

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
        /// When true, filter DeckBuilderCollection to only show new cards after next OrganizeIntoGroups.
        /// Set when user navigates to next/previous page in collection view.
        /// </summary>
        private bool _pendingCollectionPageFilter = false;

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
            MelonLogger.Msg("[GroupedNavigator] Requested PlayBladeTabs auto-entry");
        }

        /// <summary>
        /// Request auto-entry into PlayBladeContent group after next rescan.
        /// Call when a PlayBlade tab is activated.
        /// </summary>
        public void RequestPlayBladeContentEntry()
        {
            _pendingPlayBladeContentEntry = true;
            _pendingPlayBladeTabsEntry = false; // Clear tabs flag
            MelonLogger.Msg("[GroupedNavigator] Requested PlayBladeContent auto-entry");
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
        /// Save current collection card labels for filtering after a page switch.
        /// Call before activating Next/Previous button in collection view.
        /// </summary>
        public void SaveCollectionCardsForPageFilter()
        {
            _previousCollectionCards = new HashSet<string>();

            // Find the DeckBuilderCollection group and save all card labels
            foreach (var group in _groups)
            {
                if (group.Group == ElementGroup.DeckBuilderCollection)
                {
                    foreach (var element in group.Elements)
                    {
                        if (!string.IsNullOrEmpty(element.Label))
                        {
                            _previousCollectionCards.Add(element.Label);
                        }
                    }
                    MelonLogger.Msg($"[GroupedNavigator] Saved {_previousCollectionCards.Count} collection cards for page filter");
                    break;
                }
            }
        }

        /// <summary>
        /// Request filtering of DeckBuilderCollection to only show new cards after next rescan.
        /// Call after SaveCollectionCardsForPageFilter and before TriggerRescan.
        /// </summary>
        public void RequestCollectionPageFilter()
        {
            if (_previousCollectionCards != null && _previousCollectionCards.Count > 0)
            {
                _pendingCollectionPageFilter = true;
                MelonLogger.Msg("[GroupedNavigator] Requested collection page filter");
            }
        }

        /// <summary>
        /// Apply the collection page filter to show only new cards.
        /// Called by OrganizeIntoGroups when _pendingCollectionPageFilter is set.
        /// </summary>
        private void ApplyCollectionPageFilter()
        {
            // Find the DeckBuilderCollection group
            for (int i = 0; i < _groups.Count; i++)
            {
                if (_groups[i].Group == ElementGroup.DeckBuilderCollection)
                {
                    var group = _groups[i];
                    var originalCount = group.Elements.Count;

                    // Filter to only keep cards that weren't in the previous list
                    var newCards = group.Elements
                        .Where(e => !string.IsNullOrEmpty(e.Label) && !_previousCollectionCards.Contains(e.Label))
                        .ToList();

                    if (newCards.Count > 0 && newCards.Count < originalCount)
                    {
                        // Update the group with only new cards
                        _groups[i] = new ElementGroupInfo
                        {
                            Group = group.Group,
                            DisplayName = group.DisplayName,
                            Elements = newCards,
                            IsFolderGroup = group.IsFolderGroup,
                            FolderToggle = group.FolderToggle,
                            IsStandaloneElement = group.IsStandaloneElement
                        };

                        MelonLogger.Msg($"[GroupedNavigator] Collection page filter: {originalCount} -> {newCards.Count} cards (showing only new)");
                    }
                    else if (newCards.Count == 0)
                    {
                        // All cards are the same - page didn't actually change or we're at the edge
                        MelonLogger.Msg($"[GroupedNavigator] Collection page filter: no new cards found (same page?)");
                    }
                    else
                    {
                        // All cards are new - complete page change (unlikely but handle it)
                        MelonLogger.Msg($"[GroupedNavigator] Collection page filter: all {originalCount} cards are new");
                    }

                    break;
                }
            }

            // Clear the previous cards set
            _previousCollectionCards = null;
        }

        /// <summary>
        /// Set whether we're in a PlayBlade context.
        /// Call when PlayBlade opens (true) or closes (false).
        /// </summary>
        public void SetPlayBladeContext(bool isActive)
        {
            _isPlayBladeContext = isActive;
            MelonLogger.Msg($"[GroupedNavigator] PlayBlade context set to: {isActive}");
        }

        /// <summary>
        /// Whether we're currently in a PlayBlade context.
        /// </summary>
        public bool IsPlayBladeContext => _isPlayBladeContext;

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
                MelonLogger.Msg($"[GroupedNavigator] Saved group for restore: {_pendingGroupRestore}, level: {_pendingLevelRestore}");
            }
            else
            {
                _pendingGroupRestore = null;
                _pendingLevelRestore = NavigationLevel.GroupList;
            }
        }

        /// <summary>
        /// Clear the pending group restore (use when you don't want to restore after rescan).
        /// </summary>
        public void ClearPendingGroupRestore()
        {
            _pendingGroupRestore = null;
            _pendingLevelRestore = NavigationLevel.GroupList;
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

                var group = _groupAssigner.DetermineGroup(obj);

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
                ElementGroup.PlayBladeTabs,
                ElementGroup.PlayBladeContent,
                ElementGroup.SettingsMenu,
                ElementGroup.NPE,
                ElementGroup.DeckBuilderCollection,
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
                    else if (elementList.Count == 1)
                    {
                        // Single element - show standalone instead of creating a group
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
                                Label = $"Objectives, {objectives.Count} {(objectives.Count == 1 ? "item" : "items")}",
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
            if (_isPlayBladeContext && folderToggles.Count > 0)
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
                        DisplayName = "Folders",
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

            // Apply collection page filter if pending
            // This filters DeckBuilderCollection to only show cards that weren't visible before the page switch
            if (_pendingCollectionPageFilter && _previousCollectionCards != null)
            {
                _pendingCollectionPageFilter = false;
                ApplyCollectionPageFilter();
            }

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
                        MelonLogger.Msg($"[GroupedNavigator] Auto-entered pending folder: {_pendingFolderEntry} with {_groups[i].Count} items");
                        break;
                    }
                }
                _pendingFolderEntry = null; // Clear after processing
            }

            // Check for pending PlayBlade tabs entry (set when PlayBlade opens)
            if (_pendingPlayBladeTabsEntry)
            {
                _pendingPlayBladeTabsEntry = false;
                // Find PlayBladeTabs group and auto-enter it
                for (int i = 0; i < _groups.Count; i++)
                {
                    if (_groups[i].Group == ElementGroup.PlayBladeTabs && _groups[i].Count > 0)
                    {
                        _currentGroupIndex = i;
                        _navigationLevel = NavigationLevel.InsideGroup;
                        _currentElementIndex = 0;
                        MelonLogger.Msg($"[GroupedNavigator] Auto-entered PlayBladeTabs with {_groups[i].Count} items");
                        break;
                    }
                }
            }

            // Check for pending PlayBlade content entry (set when a tab is activated)
            if (_pendingPlayBladeContentEntry)
            {
                _pendingPlayBladeContentEntry = false;
                // Find PlayBladeContent group and auto-enter it
                for (int i = 0; i < _groups.Count; i++)
                {
                    if (_groups[i].Group == ElementGroup.PlayBladeContent && _groups[i].Count > 0)
                    {
                        _currentGroupIndex = i;
                        _navigationLevel = NavigationLevel.InsideGroup;
                        _currentElementIndex = 0;
                        MelonLogger.Msg($"[GroupedNavigator] Auto-entered PlayBladeContent with {_groups[i].Count} items");
                        break;
                    }
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
                for (int i = 0; i < _groups.Count; i++)
                {
                    if (_groups[i].Group == ElementGroup.PlayBladeFolders && _groups[i].Count > 0)
                    {
                        _currentGroupIndex = i;
                        _navigationLevel = NavigationLevel.InsideGroup;
                        _currentElementIndex = 0;
                        MelonLogger.Msg($"[GroupedNavigator] Auto-entered PlayBladeFolders with {_groups[i].Count} folders");
                        break;
                    }
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
            // This runs after specific auto-enters (PlayBlade, folders) but should override the default _currentGroupIndex = 0
            if (_pendingGroupRestore.HasValue)
            {
                var groupToRestore = _pendingGroupRestore.Value;
                var levelToRestore = _pendingLevelRestore;
                _pendingGroupRestore = null;
                _pendingLevelRestore = NavigationLevel.GroupList;

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
                            _currentElementIndex = 0;
                            MelonLogger.Msg($"[GroupedNavigator] Restored into group '{_groups[i].DisplayName}' with {_groups[i].Count} items");
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

            MelonLogger.Msg($"[GroupedNavigator] Organized into {_groups.Count} groups");
            foreach (var g in _groups)
            {
                string folderInfo = g.IsFolderGroup ? " (folder)" : "";
                MelonLogger.Msg($"  - {g.DisplayName}: {g.Count} items{folderInfo}");
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
                return $"{screenName}. No items found.";

            if (_groups.Count == 1)
            {
                // Single group - auto-entered, announce first element
                var group = _groups[0];
                var firstElement = group.Elements.Count > 0 ? group.Elements[0].Label : "";
                return $"{screenName}. {group.Count} items. 1 of {group.Count}: {firstElement}";
            }

            return $"{screenName}. {_groups.Count} groups. {GetCurrentAnnouncement()}";
        }

        private bool MoveNextGroup()
        {
            if (_currentGroupIndex >= _groups.Count - 1)
            {
                _announcer.Announce(Strings.EndOfList, AnnouncementPriority.Normal);
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
                _announcer.Announce(Strings.BeginningOfList, AnnouncementPriority.Normal);
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
                _announcer.Announce(Strings.EndOfList, AnnouncementPriority.Normal);
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
                _announcer.Announce(Strings.BeginningOfList, AnnouncementPriority.Normal);
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

            return $"{group.DisplayName}, {group.Count} {(group.Count == 1 ? "item" : "items")}";
        }

        private string GetElementAnnouncement()
        {
            var element = CurrentElement;
            if (!element.HasValue) return "";

            int count = GetCurrentElementCount();
            return $"{_currentElementIndex + 1} of {count}: {element.Value.Label}";
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
