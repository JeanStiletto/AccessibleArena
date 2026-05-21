using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services.ElementGrouping
{
    /// <summary>
    /// Result of PlayBlade navigation handling.
    /// </summary>
    public enum PlayBladeResult
    {
        /// <summary>Not a PlayBlade context - let normal handling proceed.</summary>
        NotHandled,
        /// <summary>Helper handled it, no further action needed.</summary>
        Handled,
        /// <summary>Helper handled it, trigger a rescan to update navigation.</summary>
        RescanNeeded,
        /// <summary>Close the PlayBlade.</summary>
        CloseBlade
    }

    /// <summary>
    /// Centralized helper for PlayBlade navigation.
    /// Handles all PlayBlade-specific Enter and Backspace logic.
    /// GeneralMenuNavigator just calls this and acts on the result.
    /// </summary>
    public class PlayBladeNavigationHelper
    {
        private readonly GroupedNavigator _groupedNavigator;

        /// <summary>
        /// Whether currently in a PlayBlade context.
        /// Uses the context flag set by OnPlayBladeOpened/OnPlayBladeClosed.
        /// </summary>
        public bool IsActive => _groupedNavigator.IsPlayBladeContext;

        /// <summary>
        /// Whether the user selected Bot-Match mode in PlayBlade.
        /// When true, JoinMatchMaking will be patched to use "AIBotMatch" event name.
        /// Static so the Harmony patch can access it.
        /// </summary>
        public static bool IsBotMatchMode { get; private set; }

        public PlayBladeNavigationHelper(GroupedNavigator groupedNavigator)
        {
            _groupedNavigator = groupedNavigator;
        }

        /// <summary>
        /// Set Bot-Match mode. Called when user activates a PlayBlade mode button.
        /// </summary>
        public static void SetBotMatchMode(bool value)
        {
            if (IsBotMatchMode != value)
            {
                IsBotMatchMode = value;
                Log.Msg("PlayBladeHelper", $"Bot Match mode: {value}");
            }
        }

        /// <summary>
        /// Handle Enter key press on an element.
        /// Called BEFORE UIActivator.Activate so we can set up pending entries.
        /// </summary>
        /// <param name="element">The element being activated.</param>
        /// <param name="elementGroup">The element's group type (from DetermineGroup, based on parent hierarchy).</param>
        /// <returns>Result indicating what action to take.</returns>
        public PlayBladeResult HandleEnter(GameObject element, ElementGroup elementGroup)
        {
            // PlayBlade tab activation (Events, Find Match, Recent)
            // -> Events tab drills into filter chips first (Tabs → Filters → Tiles);
            //    other tabs drill straight into content (Tabs → Content / Folders).
            if (elementGroup == ElementGroup.PlayBladeTabs)
            {
                // Remember which tab the user is leaving so Backspace from a deeper level
                // lands back on it. (Despite the name, this stores the last tab index of
                // any kind — not only queue-type tabs.)
                _groupedNavigator.StoreLastQueueTypeTabIndex();
                // The Unity tab object is named e.g. "Blade_Tab_Nav (Events)" — non-localized.
                bool isEventsTab = element != null && element.name.Contains("(Events)");
                if (isEventsTab)
                {
                    _groupedNavigator.RequestPlayBladeEventFiltersEntry();
                    Log.Msg("PlayBladeHelper", $"Events tab activated -> requesting filter chip entry");
                }
                else
                {
                    _groupedNavigator.RequestPlayBladeContentEntry();
                    Log.Msg("PlayBladeHelper", $"Tab activated -> requesting content entry");
                }
                // No rescan needed here - blade Hide/Show will trigger it
                return PlayBladeResult.Handled;
            }

            // Event filter chip activation (Alle / In Arbeit / Neu / Limited / Constructed / …)
            // -> Game rebuilds tile container in place; explicit rescan picks up the new tiles.
            //    Save chip index so a later Backspace from the tile list lands back on the chip.
            if (elementGroup == ElementGroup.PlayBladeEventFilters)
            {
                _groupedNavigator.StoreLastEventFilterIndex();
                _groupedNavigator.RequestPlayBladeContentEntry();
                Log.Msg("PlayBladeHelper", $"Filter chip activated -> requesting tile list entry");
                // Filter clicks do NOT fire BladeContentView.Hide/Show — need explicit rescan.
                return PlayBladeResult.RescanNeeded;
            }

            // PlayBlade content activation
            // - Event tile (inside _eventTileContainer): blade closes to open event page;
            //   panel detection will rescan automatically — nothing to request.
            // - Mode/queue type button: Navigate to folders list.
            if (elementGroup == ElementGroup.PlayBladeContent)
            {
                if (EventAccessor.IsInsideEventTileContainer(element))
                {
                    Log.Msg("PlayBladeHelper", $"Event tile activated -> letting panel detection handle navigation");
                    return PlayBladeResult.Handled;
                }

                _groupedNavigator.RequestFoldersEntry();
                Log.Msg("PlayBladeHelper", $"Mode activated -> requesting folders list entry");
                // Rescan needed since mode selection doesn't cause panel changes
                return PlayBladeResult.RescanNeeded;
            }

            // Folder handling is done by HandleGroupedEnter in GeneralMenuNavigator
            // using default folder group entry logic - no special handling needed here

            return PlayBladeResult.NotHandled;
        }

        /// <summary>
        /// Handle Enter on a queue type subgroup entry (Ranked, Open Play, Brawl).
        /// These entries may be virtual (FindMatch not active) or real (FindMatch active).
        /// Virtual entries require a two-step activation: click FindMatch tab, then queue type tab.
        /// </summary>
        public PlayBladeResult HandleQueueTypeEntry(GroupedElement element)
        {
            string queueType = element.FolderName; // "Ranked", "OpenPlay", "Brawl"

            // Store the tab index for Backspace position restore
            _groupedNavigator.StoreLastQueueTypeTabIndex();

            if (element.GameObject != null)
            {
                // Real tab (FindMatch is active) → direct click
                UIActivator.Activate(element.GameObject);
                _groupedNavigator.RequestPlayBladeContentEntry();
                Log.Msg("PlayBladeHelper", $"Queue type '{queueType}' activated directly");
                return PlayBladeResult.RescanNeeded;
            }
            else
            {
                // Virtual entry → two-step activation: click FindMatch tab first
                _groupedNavigator.SetPendingQueueTypeActivation(queueType);
                var findMatchTab = _groupedNavigator.GetFindMatchTabObject();
                if (findMatchTab != null)
                {
                    UIActivator.Activate(findMatchTab);
                    Log.Msg("PlayBladeHelper", $"Queue type '{queueType}' pending, clicking FindMatch tab");
                }
                else
                {
                    Log.Warn("PlayBladeHelper", $"Queue type '{queueType}' pending but FindMatch tab not found!");
                }
                return PlayBladeResult.Handled; // blade switch triggers automatic rescan
            }
        }

        /// <summary>
        /// Handle Backspace key press.
        /// Called BEFORE generic grouped navigation handling.
        /// Navigation hierarchy: Tabs -> Content -> Folders -> Folder (decks)
        /// Backspace goes up the hierarchy.
        /// </summary>
        /// <returns>Result indicating what action to take.</returns>
        public PlayBladeResult HandleBackspace()
        {
            // Determine if we're in a PlayBlade group by checking the group type directly.
            // Don't gate on IsPlayBladeContext - it can be stale due to debounce
            // during blade Hide/Show cycles when switching tabs.
            var currentGroup = _groupedNavigator.CurrentGroup;
            if (!currentGroup.HasValue)
                return PlayBladeResult.NotHandled;

            var groupType = currentGroup.Value.Group;
            // IsFolderGroup covers PlayBlade deck-folders, but DeckManager also uses folder groups.
            // Only treat folder groups as PlayBlade if IsPlayBladeContext is set; that flag is
            // not stale here because DeckManager never enters PlayBlade context.
            bool isPlayBladeGroup = groupType == ElementGroup.PlayBladeTabs ||
                                    groupType == ElementGroup.PlayBladeContent ||
                                    groupType == ElementGroup.PlayBladeFolders ||
                                    groupType == ElementGroup.PlayBladeEventFilters ||
                                    (currentGroup.Value.IsFolderGroup && _groupedNavigator.IsPlayBladeContext);

            if (!isPlayBladeGroup)
                return PlayBladeResult.NotHandled;

            // Inside a PlayBlade group
            if (_groupedNavigator.Level == NavigationLevel.InsideGroup)
            {
                if (currentGroup.Value.IsFolderGroup)
                {
                    // Folder group exit handled by HandleGroupedBackspace in GeneralMenuNavigator
                    // It will toggle the folder OFF, exit group, and call RequestFoldersEntry
                    // DON'T call ExitGroup here - let HandleGroupedBackspace do it
                    return PlayBladeResult.NotHandled;
                }

                // Exit the group for non-folder cases
                _groupedNavigator.ExitGroup();

                if (groupType == ElementGroup.PlayBladeFolders)
                {
                    // Was inside Folders list -> go back to content (play modes)
                    _groupedNavigator.RequestPlayBladeContentEntry();
                    Log.Msg("PlayBladeHelper", $"Backspace: exited folders list, going to content");
                    return PlayBladeResult.RescanNeeded;
                }
                else if (groupType == ElementGroup.PlayBladeContent)
                {
                    // Was in content (event tiles or play modes).
                    // Events tab: a filter chip row sits above the tiles — go back to it.
                    // Other tabs: no filter row — go back to tabs.
                    if (_groupedNavigator.GetGroupByType(ElementGroup.PlayBladeEventFilters).HasValue)
                    {
                        _groupedNavigator.RequestPlayBladeEventFiltersEntry();
                        Log.Msg("PlayBladeHelper", $"Backspace: exited tile list, going to filter chips");
                    }
                    else
                    {
                        _groupedNavigator.RequestPlayBladeTabsEntry();
                        Log.Msg("PlayBladeHelper", $"Backspace: exited content, going to tabs");
                    }
                    return PlayBladeResult.RescanNeeded;
                }
                else if (groupType == ElementGroup.PlayBladeEventFilters)
                {
                    // Was in event filter chips -> go back to tabs
                    _groupedNavigator.RequestPlayBladeTabsEntry();
                    Log.Msg("PlayBladeHelper", $"Backspace: exited filter chips, going to tabs");
                    return PlayBladeResult.RescanNeeded;
                }
                else if (groupType == ElementGroup.PlayBladeTabs)
                {
                    // Was in tabs -> close the blade
                    Log.Msg("PlayBladeHelper", $"Backspace: exited tabs, closing blade");
                    return PlayBladeResult.CloseBlade;
                }
            }
            else
            {
                // At group level in PlayBlade (navigating between groups)
                if (currentGroup.Value.IsFolderGroup)
                {
                    // At folder group level -> go to folders list
                    _groupedNavigator.RequestFoldersEntry();
                    Log.Msg("PlayBladeHelper", $"Backspace: at folder group level, going to folders list");
                    return PlayBladeResult.RescanNeeded;
                }
                else if (groupType == ElementGroup.PlayBladeFolders)
                {
                    // At folders list level -> go to content (play modes)
                    _groupedNavigator.RequestPlayBladeContentEntry();
                    Log.Msg("PlayBladeHelper", $"Backspace: at folders list level, going to content");
                    return PlayBladeResult.RescanNeeded;
                }
                else if (groupType == ElementGroup.PlayBladeContent)
                {
                    // At content group level -> go to filter chips (Events tab) or tabs (others)
                    if (_groupedNavigator.GetGroupByType(ElementGroup.PlayBladeEventFilters).HasValue)
                    {
                        _groupedNavigator.RequestPlayBladeEventFiltersEntry();
                        Log.Msg("PlayBladeHelper", $"Backspace: at content group level, going to filter chips");
                    }
                    else
                    {
                        _groupedNavigator.RequestPlayBladeTabsEntry();
                        Log.Msg("PlayBladeHelper", $"Backspace: at content group level, going to tabs");
                    }
                    return PlayBladeResult.RescanNeeded;
                }
                else if (groupType == ElementGroup.PlayBladeEventFilters)
                {
                    // At filter chips group level -> go to tabs
                    _groupedNavigator.RequestPlayBladeTabsEntry();
                    Log.Msg("PlayBladeHelper", $"Backspace: at filter chips group level, going to tabs");
                    return PlayBladeResult.RescanNeeded;
                }
                else if (groupType == ElementGroup.PlayBladeTabs)
                {
                    // At tabs group level -> close the blade
                    Log.Msg("PlayBladeHelper", $"Backspace: at tabs group level, closing blade");
                    return PlayBladeResult.CloseBlade;
                }
            }

            return PlayBladeResult.NotHandled;
        }

        /// <summary>
        /// Called when PlayBlade opens. Sets context and requests tabs entry.
        /// </summary>
        public void OnPlayBladeOpened()
        {
            _groupedNavigator.SetPlayBladeContext(true);
            _groupedNavigator.RequestPlayBladeTabsEntry();
            Log.Msg("PlayBladeHelper", $"Blade opened, set context and requesting tabs entry");
        }

        /// <summary>
        /// Called when PlayBlade closes. Clears the PlayBlade context.
        /// </summary>
        public void OnPlayBladeClosed()
        {
            _groupedNavigator.SetPlayBladeContext(false);
            SetBotMatchMode(false);
            Log.Msg("PlayBladeHelper", $"Blade closed, cleared context");
        }
    }
}
