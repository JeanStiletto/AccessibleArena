using UnityEngine;
using MelonLoader;

namespace AccessibleArena.Core.Services.ElementGrouping
{
    /// <summary>
    /// Handles all PlayBlade-specific navigation behavior.
    /// Centralizes custom logic for PlayBlade tabs, content, and state transitions.
    ///
    /// Navigation flow:
    /// 1. PlayBlade opens → Tabs state (Events, Find Match, Recent)
    /// 2. Tab selected → Content state for that tab
    /// 3. In Find Match: Mode selected → DeckSelection state
    /// 4. Backspace: DeckSelection→Modes, Content→Tabs, Tabs→Close blade
    /// </summary>
    public class PlayBladeNavigationHelper
    {
        /// <summary>
        /// Navigation states within PlayBlade.
        /// </summary>
        public enum PlayBladeState
        {
            /// <summary>Not in PlayBlade.</summary>
            None,

            /// <summary>Viewing tab list (Events, Find Match, Recent).</summary>
            Tabs,

            /// <summary>Inside Events tab - viewing event tiles (Color Challenge, Ranked, etc.).</summary>
            EventsContent,

            /// <summary>Inside Find Match tab - viewing play modes.</summary>
            FindMatchModes,

            /// <summary>Inside Find Match tab - viewing deck list after selecting a mode.</summary>
            FindMatchDecks,

            /// <summary>Inside Recent tab - viewing recently played modes.</summary>
            RecentContent
        }

        private readonly GroupedNavigator _groupedNavigator;
        private PlayBladeState _currentState = PlayBladeState.None;

        /// <summary>
        /// Current navigation state within PlayBlade.
        /// </summary>
        public PlayBladeState CurrentState => _currentState;

        /// <summary>
        /// Whether we're currently navigating inside PlayBlade.
        /// </summary>
        public bool IsActive => _currentState != PlayBladeState.None;

        public PlayBladeNavigationHelper(GroupedNavigator groupedNavigator)
        {
            _groupedNavigator = groupedNavigator;
        }

        /// <summary>
        /// Called when PlayBlade opens. Enters Tabs state.
        /// </summary>
        /// <param name="bladeViewName">Name of the blade view that opened (e.g., "EventBladeContentView").</param>
        public void OnPlayBladeOpened(string bladeViewName)
        {
            // Determine initial state based on which view opened
            _currentState = DetermineStateFromViewName(bladeViewName);

            if (_currentState == PlayBladeState.Tabs)
            {
                // Fresh blade open - show tabs first
                _groupedNavigator.RequestPlayBladeTabsEntry();
                MelonLogger.Msg($"[PlayBladeHelper] Blade opened ({bladeViewName}) → Tabs state");
            }
            else
            {
                // Direct entry to a specific view (e.g., from home screen shortcut)
                _groupedNavigator.RequestPlayBladeContentEntry();
                MelonLogger.Msg($"[PlayBladeHelper] Blade opened directly to {_currentState}");
            }
        }

        /// <summary>
        /// Called when an element is activated (Enter pressed).
        /// Handles state transitions based on what was activated.
        /// </summary>
        /// <param name="element">The activated element.</param>
        /// <param name="elementGroup">The element's group.</param>
        /// <returns>True if this helper handled the activation and triggered a state change.</returns>
        public bool OnElementActivated(GameObject element, ElementGroup elementGroup)
        {
            MelonLogger.Msg($"[PlayBladeHelper] OnElementActivated: element={element?.name}, group={elementGroup}, currentState={_currentState}, IsActive={IsActive}");

            if (!IsActive) return false;

            // Tab activated - transition to content state
            if (elementGroup == ElementGroup.PlayBladeTabs)
            {
                var newState = DetermineContentStateFromTab(element);
                _currentState = newState;
                _groupedNavigator.RequestPlayBladeContentEntry();
                MelonLogger.Msg($"[PlayBladeHelper] Tab activated → {newState}");
                return true;
            }

            // In FindMatchModes, selecting a mode advances to deck selection
            if (_currentState == PlayBladeState.FindMatchModes && elementGroup == ElementGroup.PlayBladeContent)
            {
                // Check if this is a play mode (not a filter or other control)
                if (IsPlayModeElement(element))
                {
                    _currentState = PlayBladeState.FindMatchDecks;
                    _groupedNavigator.RequestPlayBladeContentEntry();
                    MelonLogger.Msg($"[PlayBladeHelper] Mode selected → FindMatchDecks");
                    return true;
                }
                else
                {
                    MelonLogger.Msg($"[PlayBladeHelper] Element not recognized as play mode: {element?.name}");
                }
            }
            else
            {
                MelonLogger.Msg($"[PlayBladeHelper] No state transition: state={_currentState}, group={elementGroup}");
            }

            return false;
        }

        /// <summary>
        /// Called when Backspace is pressed.
        /// Handles state-aware back navigation within PlayBlade.
        /// </summary>
        /// <returns>
        /// True if backspace was handled (stay in PlayBlade).
        /// False if blade should close (at Tabs level or not in PlayBlade).
        /// </returns>
        public bool HandleBackspace()
        {
            MelonLogger.Msg($"[PlayBladeHelper] HandleBackspace: currentState={_currentState}, IsActive={IsActive}");

            switch (_currentState)
            {
                case PlayBladeState.None:
                    // Not in PlayBlade - don't handle
                    return false;

                case PlayBladeState.Tabs:
                    // At tabs level - close the blade
                    _currentState = PlayBladeState.None;
                    return false;

                case PlayBladeState.FindMatchDecks:
                    // In deck selection - go back to mode selection
                    _currentState = PlayBladeState.FindMatchModes;
                    _groupedNavigator.RequestPlayBladeContentEntry();
                    MelonLogger.Msg($"[PlayBladeHelper] Backspace: FindMatchDecks → FindMatchModes");
                    return true;

                case PlayBladeState.EventsContent:
                case PlayBladeState.FindMatchModes:
                case PlayBladeState.RecentContent:
                    // In content - go back to tabs
                    _currentState = PlayBladeState.Tabs;
                    _groupedNavigator.RequestPlayBladeTabsEntry();
                    MelonLogger.Msg($"[PlayBladeHelper] Backspace: Content → Tabs");
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Called when PlayBlade closes. Resets state.
        /// </summary>
        public void OnPlayBladeClosed()
        {
            MelonLogger.Msg($"[PlayBladeHelper] Blade closed (was in state: {_currentState})");
            _currentState = PlayBladeState.None;
        }

        /// <summary>
        /// Reset state without triggering navigation changes.
        /// Use when navigator is deactivated or reset.
        /// </summary>
        public void Reset()
        {
            _currentState = PlayBladeState.None;
        }

        /// <summary>
        /// Sync state to indicate user is viewing content (not tabs).
        /// Called when GroupedNavigator shows user is at PlayBladeContent group
        /// even though they never pressed Enter on a tab (just arrow-navigated to the group).
        /// Uses FindMatchModes as a safe default content state.
        /// </summary>
        public void SyncToContentState()
        {
            // Only sync if we're currently in Tabs state
            // (don't override more specific states like FindMatchDecks)
            if (_currentState == PlayBladeState.Tabs)
            {
                _currentState = PlayBladeState.FindMatchModes;
                MelonLogger.Msg($"[PlayBladeHelper] Synced state: Tabs → FindMatchModes (user viewing content group)");
            }
        }

        /// <summary>
        /// Sync state to indicate user is viewing tabs.
        /// Called when GroupedNavigator shows user is at PlayBladeTabs group.
        /// </summary>
        public void SyncToTabsState()
        {
            // Sync to Tabs if we were in a content state
            if (_currentState != PlayBladeState.None && _currentState != PlayBladeState.Tabs)
            {
                _currentState = PlayBladeState.Tabs;
                MelonLogger.Msg($"[PlayBladeHelper] Synced state: → Tabs (user viewing tabs group)");
            }
        }

        #region State Detection Helpers

        /// <summary>
        /// Determine initial state from the blade view name.
        /// </summary>
        private PlayBladeState DetermineStateFromViewName(string viewName)
        {
            if (string.IsNullOrEmpty(viewName))
                return PlayBladeState.Tabs;

            // Check for direct entry views (bypassing tabs)
            // These are opened via shortcuts on home screen
            if (viewName.Contains("FindMatch") && !viewName.Contains("BladeContentView"))
                return PlayBladeState.FindMatchModes;

            // Normal blade opening - start at tabs
            return PlayBladeState.Tabs;
        }

        /// <summary>
        /// Determine which content state to enter based on the activated tab.
        /// </summary>
        private PlayBladeState DetermineContentStateFromTab(GameObject tabElement)
        {
            string name = tabElement?.name ?? "";

            if (name.Contains("Events") || name.Contains("Event"))
                return PlayBladeState.EventsContent;

            if (name.Contains("FindMatch") || name.Contains("Match"))
                return PlayBladeState.FindMatchModes;

            if (name.Contains("LastPlayed") || name.Contains("Recent"))
                return PlayBladeState.RecentContent;

            // Default to events content
            MelonLogger.Warning($"[PlayBladeHelper] Unknown tab: {name}, defaulting to EventsContent");
            return PlayBladeState.EventsContent;
        }

        /// <summary>
        /// Check if an element is a play mode selector (not a filter or other control).
        /// </summary>
        private bool IsPlayModeElement(GameObject element)
        {
            if (element == null) return false;

            string name = element.name;

            // Filter buttons are not play modes
            if (name.Contains("Filter") || name.Contains("Alle"))
                return false;

            // Tab elements are not play modes
            if (name.Contains("Blade_Tab"))
                return false;

            // Event tiles and mode buttons are play modes
            if (name.Contains("EventTile") || name.Contains("Hitbox") ||
                name.Contains("Mode") || name.Contains("Format"))
                return true;

            // Check parent hierarchy for EventTile
            Transform current = element.transform.parent;
            int depth = 0;
            while (current != null && depth < 5)
            {
                if (current.name.Contains("EventTile") || current.name.Contains("PlayOption"))
                    return true;
                current = current.parent;
                depth++;
            }

            // Assume content elements are play modes unless proven otherwise
            return true;
        }

        #endregion
    }
}
