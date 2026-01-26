using UnityEngine;
using MelonLoader;

namespace AccessibleArena.Core.Services.ElementGrouping
{
    /// <summary>
    /// Minimal helper for PlayBlade navigation.
    /// Determines PlayBlade state from GroupedNavigator's current group - no separate state tracking.
    /// </summary>
    public class PlayBladeNavigationHelper
    {
        private readonly GroupedNavigator _groupedNavigator;

        /// <summary>
        /// Whether currently in a PlayBlade group (tabs, content, or folder inside PlayBlade).
        /// Determined directly from GroupedNavigator's state.
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
                       (group.Value.IsFolderGroup && IsInsidePlayBlade());
            }
        }

        public PlayBladeNavigationHelper(GroupedNavigator groupedNavigator)
        {
            _groupedNavigator = groupedNavigator;
        }

        /// <summary>
        /// Check if folder groups are inside PlayBlade context.
        /// </summary>
        private bool IsInsidePlayBlade()
        {
            // If we have PlayBladeTabs or PlayBladeContent groups, we're in PlayBlade
            // This is a simple heuristic - if those groups exist, folders are PlayBlade folders
            return _groupedNavigator.GroupCount > 0;
        }

        /// <summary>
        /// Called when PlayBlade opens - request tabs entry.
        /// </summary>
        public void OnPlayBladeOpened(string bladeViewName)
        {
            _groupedNavigator.RequestPlayBladeTabsEntry();
            MelonLogger.Msg($"[PlayBladeHelper] Blade opened, requesting tabs entry");
        }

        /// <summary>
        /// Called when PlayBlade closes - no-op since we don't track state.
        /// </summary>
        public void OnPlayBladeClosed()
        {
            MelonLogger.Msg($"[PlayBladeHelper] Blade closed");
        }

        /// <summary>
        /// Called when an element is activated.
        /// Requests content entry when a tab is activated.
        /// </summary>
        public bool OnElementActivated(GameObject element, ElementGroup elementGroup)
        {
            // Tab activated - request content entry for next rescan
            if (elementGroup == ElementGroup.PlayBladeTabs)
            {
                _groupedNavigator.RequestPlayBladeContentEntry();
                MelonLogger.Msg($"[PlayBladeHelper] Tab activated, requesting content entry");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Handle backspace - returns true if should stay in PlayBlade.
        /// </summary>
        public bool HandleBackspace()
        {
            var currentGroup = _groupedNavigator.CurrentGroup;
            if (!currentGroup.HasValue) return false;

            // If inside a group, let GroupedNavigator.ExitGroup handle it first
            if (_groupedNavigator.Level == NavigationLevel.InsideGroup)
            {
                return false;
            }

            // At group level - check what group we're in
            var group = currentGroup.Value.Group;

            // If at PlayBladeContent or folder group level, go back to tabs
            if (group == ElementGroup.PlayBladeContent || currentGroup.Value.IsFolderGroup)
            {
                _groupedNavigator.RequestPlayBladeTabsEntry();
                MelonLogger.Msg($"[PlayBladeHelper] Backspace at {group} group level, requesting tabs entry");
                return true;
            }

            // At PlayBladeTabs level - close the blade
            if (group == ElementGroup.PlayBladeTabs)
            {
                MelonLogger.Msg($"[PlayBladeHelper] Backspace at tabs level, closing blade");
                return false;
            }

            return false;
        }

        /// <summary>
        /// Reset - no-op since we don't track state.
        /// </summary>
        public void Reset() { }

        // Legacy methods - no-ops
        public void SyncToContentState() { }
        public void SyncToTabsState() { }
    }
}
