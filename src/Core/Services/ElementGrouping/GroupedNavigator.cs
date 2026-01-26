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
        /// </summary>
        public GroupedElement? CurrentElement
        {
            get
            {
                if (_navigationLevel != NavigationLevel.InsideGroup)
                    return null;
                var group = CurrentGroup;
                if (group == null || _currentElementIndex < 0 || _currentElementIndex >= group.Value.Count)
                    return null;
                return group.Value.Elements[_currentElementIndex];
            }
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

            // Build ordered group list
            // Note: PlayBladeTabs comes before PlayBladeContent so tabs are shown first
            var groupOrder = new[]
            {
                ElementGroup.Play,
                ElementGroup.Progress,
                ElementGroup.Primary,
                ElementGroup.Navigation,
                ElementGroup.Filters,
                ElementGroup.Content,
                ElementGroup.Settings,
                ElementGroup.Secondary,
                ElementGroup.Popup,
                ElementGroup.Social,
                ElementGroup.PlayBladeTabs,
                ElementGroup.PlayBladeContent,
                ElementGroup.SettingsMenu,
                ElementGroup.NPE,
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

            // Add folder groups (each folder becomes its own group)
            // NOTE: We create folder groups even when they appear empty, because the decks inside
            // may not be activeInHierarchy when the folder toggle is OFF (collapsed).
            // When user enters the folder (Enter key), we activate the toggle which makes decks visible.
            foreach (var kvp in folderDecks.OrderBy(x => x.Key))
            {
                string folderName = kvp.Key;
                var deckList = kvp.Value;

                // Always create folder group if we found the toggle, even if deck list is empty
                // (decks may be hidden because folder is collapsed)
                GameObject toggle = folderToggles.TryGetValue(folderName, out var t) ? t : null;
                if (toggle == null && deckList.Count == 0) continue; // Skip only if no toggle AND no decks

                _groups.Add(new ElementGroupInfo
                {
                    Group = ElementGroup.Content, // Use Content as base group type
                    DisplayName = folderName,
                    Elements = deckList,
                    IsFolderGroup = true,
                    FolderToggle = toggle,
                    IsStandaloneElement = false
                });

                MelonLogger.Msg($"[GroupedNavigator] Created folder group: {folderName} with {deckList.Count} decks (toggle: {(toggle != null ? "found" : "none")})");
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
                var group = CurrentGroup;
                if (group.HasValue && group.Value.Count > 0)
                    _currentElementIndex = group.Value.Count - 1;
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
            var group = CurrentGroup;
            if (!group.HasValue) return false;

            if (_currentElementIndex >= group.Value.Count - 1)
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

            var group = CurrentGroup;
            if (!group.HasValue) return "";

            return $"{_currentElementIndex + 1} of {group.Value.Count}: {element.Value.Label}";
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
    }
}
