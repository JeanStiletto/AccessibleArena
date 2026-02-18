using UnityEngine;
using MelonLoader;

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
                MelonLogger.Msg($"[PlayBladeHelper] Bot Match mode: {value}");
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
            // -> Navigate to content (play modes)
            if (elementGroup == ElementGroup.PlayBladeTabs)
            {
                _groupedNavigator.RequestPlayBladeContentEntry();
                MelonLogger.Msg($"[PlayBladeHelper] Tab activated -> requesting content entry");
                // No rescan needed here - blade Hide/Show will trigger it
                return PlayBladeResult.Handled;
            }

            // PlayBlade content activation (Ranked, Play, Brawl modes)
            // -> Navigate to folders list (not directly to first folder)
            if (elementGroup == ElementGroup.PlayBladeContent)
            {
                _groupedNavigator.RequestFoldersEntry();
                MelonLogger.Msg($"[PlayBladeHelper] Mode activated -> requesting folders list entry");
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
                MelonLogger.Msg($"[PlayBladeHelper] Queue type '{queueType}' activated directly");
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
                    MelonLogger.Msg($"[PlayBladeHelper] Queue type '{queueType}' pending, clicking FindMatch tab");
                }
                else
                {
                    MelonLogger.Warning($"[PlayBladeHelper] Queue type '{queueType}' pending but FindMatch tab not found!");
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
            bool isPlayBladeGroup = groupType == ElementGroup.PlayBladeTabs ||
                                    groupType == ElementGroup.PlayBladeContent ||
                                    groupType == ElementGroup.PlayBladeFolders ||
                                    currentGroup.Value.IsFolderGroup;

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
                    MelonLogger.Msg($"[PlayBladeHelper] Backspace: exited folders list, going to content");
                    return PlayBladeResult.RescanNeeded;
                }
                else if (groupType == ElementGroup.PlayBladeContent)
                {
                    // Was in content (play modes) -> go back to tabs
                    _groupedNavigator.RequestPlayBladeTabsEntry();
                    MelonLogger.Msg($"[PlayBladeHelper] Backspace: exited content, going to tabs");
                    return PlayBladeResult.RescanNeeded;
                }
                else if (groupType == ElementGroup.PlayBladeTabs)
                {
                    // Was in tabs -> close the blade
                    MelonLogger.Msg($"[PlayBladeHelper] Backspace: exited tabs, closing blade");
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
                    MelonLogger.Msg($"[PlayBladeHelper] Backspace: at folder group level, going to folders list");
                    return PlayBladeResult.RescanNeeded;
                }
                else if (groupType == ElementGroup.PlayBladeFolders)
                {
                    // At folders list level -> go to content (play modes)
                    _groupedNavigator.RequestPlayBladeContentEntry();
                    MelonLogger.Msg($"[PlayBladeHelper] Backspace: at folders list level, going to content");
                    return PlayBladeResult.RescanNeeded;
                }
                else if (groupType == ElementGroup.PlayBladeContent)
                {
                    // At content group level -> go to tabs
                    _groupedNavigator.RequestPlayBladeTabsEntry();
                    MelonLogger.Msg($"[PlayBladeHelper] Backspace: at content group level, going to tabs");
                    return PlayBladeResult.RescanNeeded;
                }
                else if (groupType == ElementGroup.PlayBladeTabs)
                {
                    // At tabs group level -> close the blade
                    MelonLogger.Msg($"[PlayBladeHelper] Backspace: at tabs group level, closing blade");
                    return PlayBladeResult.CloseBlade;
                }
            }

            return PlayBladeResult.NotHandled;
        }

        /// <summary>
        /// Called when PlayBlade opens. Sets context and requests tabs entry.
        /// </summary>
        public void OnPlayBladeOpened(string bladeViewName)
        {
            _groupedNavigator.SetPlayBladeContext(true);
            _groupedNavigator.RequestPlayBladeTabsEntry();
            MelonLogger.Msg($"[PlayBladeHelper] Blade opened, set context and requesting tabs entry");
        }

        /// <summary>
        /// Called when PlayBlade closes. Clears the PlayBlade context.
        /// </summary>
        public void OnPlayBladeClosed()
        {
            _groupedNavigator.SetPlayBladeContext(false);
            SetBotMatchMode(false);
            MelonLogger.Msg($"[PlayBladeHelper] Blade closed, cleared context");
        }

        /// <summary>
        /// Reset - no-op since we derive state from GroupedNavigator.
        /// </summary>
        public void Reset() { }
    }
}
