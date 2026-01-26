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
        /// Whether currently in a PlayBlade context (any PlayBlade group).
        /// </summary>
        public bool IsActive
        {
            get
            {
                var group = _groupedNavigator.CurrentGroup;
                if (!group.HasValue) return false;

                var groupType = group.Value.Group;
                return groupType == ElementGroup.PlayBladeTabs ||
                       groupType == ElementGroup.PlayBladeContent ||
                       group.Value.IsFolderGroup;
            }
        }

        public PlayBladeNavigationHelper(GroupedNavigator groupedNavigator)
        {
            _groupedNavigator = groupedNavigator;
        }

        /// <summary>
        /// Handle Enter key press on an element.
        /// Called BEFORE UIActivator.Activate so we can set up pending entries.
        /// </summary>
        /// <param name="element">The element being activated.</param>
        /// <param name="elementGroup">The element's group type.</param>
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
            // -> Navigate to first deck folder
            if (elementGroup == ElementGroup.PlayBladeContent)
            {
                _groupedNavigator.RequestFirstFolderEntry();
                MelonLogger.Msg($"[PlayBladeHelper] Mode activated -> requesting folder entry");
                // Rescan needed since mode selection doesn't cause panel changes
                return PlayBladeResult.RescanNeeded;
            }

            return PlayBladeResult.NotHandled;
        }

        /// <summary>
        /// Handle Backspace key press.
        /// Called BEFORE generic grouped navigation handling.
        /// </summary>
        /// <returns>Result indicating what action to take.</returns>
        public PlayBladeResult HandleBackspace()
        {
            var currentGroup = _groupedNavigator.CurrentGroup;
            if (!currentGroup.HasValue)
                return PlayBladeResult.NotHandled;

            var groupType = currentGroup.Value.Group;
            bool isPlayBladeGroup = groupType == ElementGroup.PlayBladeTabs ||
                                    groupType == ElementGroup.PlayBladeContent ||
                                    currentGroup.Value.IsFolderGroup;

            if (!isPlayBladeGroup)
                return PlayBladeResult.NotHandled;

            // Inside a PlayBlade group
            if (_groupedNavigator.Level == NavigationLevel.InsideGroup)
            {
                // Exit the group first
                _groupedNavigator.ExitGroup();

                if (groupType == ElementGroup.PlayBladeContent || currentGroup.Value.IsFolderGroup)
                {
                    // Was in content or folder -> go back to tabs
                    _groupedNavigator.RequestPlayBladeTabsEntry();
                    MelonLogger.Msg($"[PlayBladeHelper] Backspace: exited {groupType}, going to tabs");
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
                // At group level in PlayBlade
                if (groupType == ElementGroup.PlayBladeContent || currentGroup.Value.IsFolderGroup)
                {
                    // At content/folder group level -> go to tabs
                    _groupedNavigator.RequestPlayBladeTabsEntry();
                    MelonLogger.Msg($"[PlayBladeHelper] Backspace: at {groupType} group level, going to tabs");
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
        /// Called when PlayBlade opens. Requests tabs entry for initial navigation.
        /// </summary>
        public void OnPlayBladeOpened(string bladeViewName)
        {
            _groupedNavigator.RequestPlayBladeTabsEntry();
            MelonLogger.Msg($"[PlayBladeHelper] Blade opened, requesting tabs entry");
        }

        /// <summary>
        /// Called when PlayBlade closes. No-op since we don't track state.
        /// </summary>
        public void OnPlayBladeClosed()
        {
            MelonLogger.Msg($"[PlayBladeHelper] Blade closed");
        }

        /// <summary>
        /// Reset - no-op since we derive state from GroupedNavigator.
        /// </summary>
        public void Reset() { }
    }
}
