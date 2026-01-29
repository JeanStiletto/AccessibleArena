namespace AccessibleArena.Core.Services.ElementGrouping
{
    /// <summary>
    /// Groups for categorizing UI elements in menu navigation.
    /// Elements are assigned to groups based on their parent hierarchy.
    /// </summary>
    public enum ElementGroup
    {
        /// <summary>
        /// Unclassified elements. Hidden in grouped mode, visible in flat navigation mode.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Main actions: Submit, Continue, primary CTA buttons (not Play - that has its own group).
        /// Single-item groups auto-enter directly to the element.
        /// </summary>
        Primary,

        /// <summary>
        /// Play-related elements: Play button, Direct Challenge, Rankings, Events, Learn/Tutorial.
        /// Grouped together for easy access to all play options.
        /// </summary>
        Play,

        /// <summary>
        /// Progress-related elements: Boosters, Mastery, Gems, Gold, Wildcards, currency buttons.
        /// Grouped together for easy access to progress/resource indicators.
        /// </summary>
        Progress,

        /// <summary>
        /// Objectives/Quests on home screen: Daily wins, weekly wins, quests, battle pass progress.
        /// Navigated as a submenu - Enter to view individual objectives, Backspace to exit.
        /// </summary>
        Objectives,

        /// <summary>
        /// Social elements on home screen: Profile, Achievements, Mail/Notifications.
        /// </summary>
        Social,

        /// <summary>
        /// Filter controls: Search fields, sort options, filter toggles, mana color filters.
        /// </summary>
        Filters,

        /// <summary>
        /// Main content items: Deck entries, cards in collection, list items, event entries.
        /// </summary>
        Content,

        /// <summary>
        /// Settings controls: Sliders, checkboxes, dropdowns within settings panels.
        /// </summary>
        Settings,

        /// <summary>
        /// Secondary actions: Help, info buttons, less common actions.
        /// </summary>
        Secondary,

        // --- Overlay Groups ---
        // Only one overlay group is visible at a time when active.
        // Overlay groups suppress all standard groups.

        /// <summary>
        /// Modal dialog/popup elements. Suppresses all other groups when active.
        /// </summary>
        Popup,

        /// <summary>
        /// Friends panel overlay elements. Suppresses all other groups when active.
        /// </summary>
        FriendsPanel,

        /// <summary>
        /// Play blade tabs (Events, Find Match, Recent). Shown first when PlayBlade is active.
        /// </summary>
        PlayBladeTabs,

        /// <summary>
        /// Play blade content elements (event tiles, decks, filters). Shown after selecting a tab.
        /// </summary>
        PlayBladeContent,

        /// <summary>
        /// Play blade folders container. Groups all deck folders when in PlayBlade context.
        /// User selects a folder from this group, then enters the folder to see decks.
        /// </summary>
        PlayBladeFolders,

        /// <summary>
        /// Settings menu elements. Suppresses all other groups when active.
        /// </summary>
        SettingsMenu,

        /// <summary>
        /// New Player Experience overlay elements. Suppresses all other groups when active.
        /// </summary>
        NPE,

        /// <summary>
        /// Deck Builder collection cards (PoolHolder). Cards available to add to your deck.
        /// </summary>
        DeckBuilderCollection
    }

    /// <summary>
    /// Extension methods for ElementGroup.
    /// </summary>
    public static class ElementGroupExtensions
    {
        /// <summary>
        /// Returns true if this group is an overlay group that suppresses other groups.
        /// </summary>
        public static bool IsOverlay(this ElementGroup group)
        {
            return group == ElementGroup.Popup
                || group == ElementGroup.FriendsPanel
                || group == ElementGroup.PlayBladeTabs
                || group == ElementGroup.PlayBladeContent
                || group == ElementGroup.PlayBladeFolders
                || group == ElementGroup.SettingsMenu
                || group == ElementGroup.NPE
                || group == ElementGroup.DeckBuilderCollection;
        }

        /// <summary>
        /// Returns a screen-reader friendly name for the group.
        /// </summary>
        public static string GetDisplayName(this ElementGroup group)
        {
            switch (group)
            {
                case ElementGroup.Primary: return "Primary Actions";
                case ElementGroup.Play: return "Play";
                case ElementGroup.Progress: return "Progress";
                case ElementGroup.Objectives: return "Objectives";
                case ElementGroup.Social: return "Social";
                case ElementGroup.Filters: return "Filters";
                case ElementGroup.Content: return "Content";
                case ElementGroup.Settings: return "Settings";
                case ElementGroup.Secondary: return "Secondary Actions";
                case ElementGroup.Popup: return "Dialog";
                case ElementGroup.FriendsPanel: return "Friends";
                case ElementGroup.PlayBladeTabs: return "Tabs";
                case ElementGroup.PlayBladeContent: return "Play Options";
                case ElementGroup.PlayBladeFolders: return "Folders";
                case ElementGroup.SettingsMenu: return "Settings Menu";
                case ElementGroup.NPE: return "Tutorial";
                case ElementGroup.DeckBuilderCollection: return "Collection";
                default: return "Other";
            }
        }
    }
}
