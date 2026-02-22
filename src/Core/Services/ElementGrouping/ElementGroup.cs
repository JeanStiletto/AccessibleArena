using AccessibleArena.Core.Models;

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
        /// Challenge screen main settings. Flat list of spinners, buttons, and status elements.
        /// Used for Direct Challenge and Friend Challenge screens.
        /// </summary>
        ChallengeMain,

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
        DeckBuilderCollection,

        /// <summary>
        /// Deck Builder deck list cards (MainDeck_MetaCardHolder). Cards currently in your deck.
        /// Navigated horizontally with Left/Right, card details with Up/Down.
        /// </summary>
        DeckBuilderDeckList,

        /// <summary>
        /// Deck Builder info group (card count, mana curve, type breakdown, colors).
        /// Contains virtual elements with no GameObjects - purely informational text
        /// read from the game's own UI components via reflection.
        /// </summary>
        DeckBuilderInfo,

        /// <summary>
        /// Mailbox mail list (left pane). Shown when browsing mails.
        /// </summary>
        MailboxList,

        /// <summary>
        /// Mailbox mail content (right pane). Shown when viewing a specific mail.
        /// Contains title, body text, and action buttons (Claim, More Info).
        /// </summary>
        MailboxContent,

        /// <summary>
        /// Rewards popup overlay. Shown after claiming rewards from mail or other sources.
        /// Contains reward items (cards, sleeves, etc.) and a click-to-progress background.
        /// </summary>
        RewardsPopup,

        // --- Friends Panel Sub-Groups ---
        // These replace the single FriendsPanel group with per-section groups.

        /// <summary>
        /// Friends panel: Challenge action button. Single-element standalone group.
        /// </summary>
        FriendsPanelChallenge,

        /// <summary>
        /// Friends panel: Add Friend action button. Single-element standalone group.
        /// </summary>
        FriendsPanelAddFriend,

        /// <summary>
        /// Friends panel: Actual friends list section.
        /// Navigate with Up/Down, Left/Right for actions on each friend.
        /// </summary>
        FriendSectionFriends,

        /// <summary>
        /// Friends panel: Incoming friend requests section.
        /// </summary>
        FriendSectionIncoming,

        /// <summary>
        /// Friends panel: Outgoing/sent friend requests section.
        /// </summary>
        FriendSectionOutgoing,

        /// <summary>
        /// Friends panel: Blocked users section.
        /// </summary>
        FriendSectionBlocked
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
                || group == ElementGroup.FriendsPanelChallenge
                || group == ElementGroup.FriendsPanelAddFriend
                || group == ElementGroup.FriendSectionFriends
                || group == ElementGroup.FriendSectionIncoming
                || group == ElementGroup.FriendSectionOutgoing
                || group == ElementGroup.FriendSectionBlocked
                || group == ElementGroup.PlayBladeTabs
                || group == ElementGroup.PlayBladeContent
                || group == ElementGroup.PlayBladeFolders
                || group == ElementGroup.SettingsMenu
                || group == ElementGroup.NPE
                || group == ElementGroup.DeckBuilderCollection
                || group == ElementGroup.DeckBuilderDeckList
                || group == ElementGroup.DeckBuilderInfo
                || group == ElementGroup.MailboxList
                || group == ElementGroup.MailboxContent
                || group == ElementGroup.RewardsPopup
                || group == ElementGroup.ChallengeMain;
        }

        /// <summary>
        /// Returns true if this group is one of the friend panel sub-groups
        /// (challenge, add friend, or any friend section).
        /// </summary>
        public static bool IsFriendPanelGroup(this ElementGroup group)
        {
            return group == ElementGroup.FriendsPanelChallenge
                || group == ElementGroup.FriendsPanelAddFriend
                || group == ElementGroup.FriendSectionFriends
                || group == ElementGroup.FriendSectionIncoming
                || group == ElementGroup.FriendSectionOutgoing
                || group == ElementGroup.FriendSectionBlocked;
        }

        /// <summary>
        /// Returns true if this group is a friend section (not action buttons).
        /// These sections support left/right action sub-navigation.
        /// </summary>
        public static bool IsFriendSectionGroup(this ElementGroup group)
        {
            return group == ElementGroup.FriendSectionFriends
                || group == ElementGroup.FriendSectionIncoming
                || group == ElementGroup.FriendSectionOutgoing
                || group == ElementGroup.FriendSectionBlocked;
        }

        /// <summary>
        /// Returns true if this group is the challenge main group.
        /// </summary>
        public static bool IsChallengeGroup(this ElementGroup group)
        {
            return group == ElementGroup.ChallengeMain;
        }

        /// <summary>
        /// Returns a screen-reader friendly localized name for the group.
        /// </summary>
        public static string GetDisplayName(this ElementGroup group)
        {
            return Strings.GroupName(group);
        }
    }
}
