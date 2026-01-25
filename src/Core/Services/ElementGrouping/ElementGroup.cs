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
        /// Main actions: Play button, Submit, Continue, primary CTA buttons.
        /// Single-item groups auto-enter directly to the element.
        /// </summary>
        Primary,

        /// <summary>
        /// Navigation elements: Nav bar items, tabs, back buttons.
        /// </summary>
        Navigation,

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
        /// Social/Friends panel elements. Suppresses all other groups when active.
        /// </summary>
        Social,

        /// <summary>
        /// Play blade elements. Suppresses all other groups when active.
        /// </summary>
        PlayBlade,

        /// <summary>
        /// Settings menu elements. Suppresses all other groups when active.
        /// </summary>
        SettingsMenu,

        /// <summary>
        /// New Player Experience overlay elements. Suppresses all other groups when active.
        /// </summary>
        NPE
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
                || group == ElementGroup.Social
                || group == ElementGroup.PlayBlade
                || group == ElementGroup.SettingsMenu
                || group == ElementGroup.NPE;
        }

        /// <summary>
        /// Returns a screen-reader friendly name for the group.
        /// </summary>
        public static string GetDisplayName(this ElementGroup group)
        {
            switch (group)
            {
                case ElementGroup.Primary: return "Primary Actions";
                case ElementGroup.Navigation: return "Navigation";
                case ElementGroup.Filters: return "Filters";
                case ElementGroup.Content: return "Content";
                case ElementGroup.Settings: return "Settings";
                case ElementGroup.Secondary: return "Secondary Actions";
                case ElementGroup.Popup: return "Dialog";
                case ElementGroup.Social: return "Social";
                case ElementGroup.PlayBlade: return "Play Options";
                case ElementGroup.SettingsMenu: return "Settings Menu";
                case ElementGroup.NPE: return "Tutorial";
                default: return "Other";
            }
        }
    }
}
