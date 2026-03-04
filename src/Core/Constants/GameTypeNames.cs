namespace AccessibleArena.Core.Constants
{
    /// <summary>
    /// Constants for game type names used in reflection lookups and type detection.
    /// Organized by category. Only includes names referenced in 2+ locations.
    /// </summary>
    public static class GameTypeNames
    {
        // --- CDC / Card View Types ---
        public const string DuelSceneCDC = "DuelScene_CDC";
        public const string MetaCDC = "Meta_CDC";
        public const string MetaCardView = "MetaCardView";
        public const string PagesMetaCardView = "PagesMetaCardView";
        public const string BoosterMetaCardView = "BoosterMetaCardView";
        public const string DraftPackCardView = "DraftPackCardView";
        public const string CardView = "CardView";
        public const string DuelCardView = "DuelCardView";
        public const string RewardDisplayCard = "RewardDisplayCard";
        public const string CardRolloverZoomHandler = "CardRolloverZoomHandler";
        public const string StaticColumnMetaCardHolder = "StaticColumnMetaCardHolder";

        // --- UI Component Types ---
        public const string CustomButton = "CustomButton";
        public const string CustomButtonWithTooltip = "CustomButtonWithTooltip";
        public const string SystemMessageButtonView = "SystemMessageButtonView";
        public const string CustomTMPDropdown = "cTMP_Dropdown";

        // --- Card Holder Types ---
        public const string CardPoolHolder = "CardPoolHolder";
        public const string ScrollCardPoolHolder = "ScrollCardPoolHolder";
        public const string CardBrowserCardHolder = "CardBrowserCardHolder";
        public const string ListMetaCardHolder = "ListMetaCardHolder";

        // --- Content Controllers ---
        public const string HomePageContentController = "HomePageContentController";
        public const string EventPageContentController = "EventPageContentController";
        public const string PacketSelectContentController = "PacketSelectContentController";
        public const string LearnToPlayControllerV2 = "LearnToPlayControllerV2";
        public const string DeckManagerController = "DeckManagerController";

        // --- Panel / Blade Types ---
        public const string PlayBladeController = "PlayBladeController";
        public const string DeckSelectBlade = "DeckSelectBlade";
        public const string NavContentController = "NavContentController";
        public const string SettingsMenu = "SettingsMenu";
        public const string SocialUI = "SocialUI";
        public const string NavBarController = "NavBarController";
        public const string DeckMainTitlePanel = "DeckMainTitlePanel";
        public const string DeckCostsDetails = "DeckCostsDetails";
        public const string DeckTypesDetails = "DeckTypesDetails";

        // --- Game / Duel Types ---
        public const string GameManager = "GameManager";
        public const string MatchTimer = "MatchTimer";

        // --- Social Types ---
        public const string FriendTile = "FriendTile";
        public const string InviteOutgoingTile = "InviteOutgoingTile";
        public const string InviteIncomingTile = "InviteIncomingTile";
        public const string BlockTile = "BlockTile";
        public const string FriendsWidget = "FriendsWidget";

        // --- Fully-Qualified Type Names (for FindType lookups) ---
        public const string NavContentControllerFQ = "Wotc.Mtga.Wrapper.NavContentController";
        public const string SettingsMenuFQ = "Wotc.Mtga.Wrapper.SettingsMenu";
        public const string BladeContentViewFQ = "Wizards.Mtga.PlayBlade.BladeContentView";
        public const string BladeContentView = "BladeContentView";
        public const string EventBladeContentViewFQ = "Wizards.Mtga.PlayBlade.EventBladeContentView";
        public const string EventBladeContentView = "EventBladeContentView";
        public const string ContentControllerPlayerInboxFQ = "Wotc.Mtga.Wrapper.Mailbox.ContentControllerPlayerInbox";
        public const string UXEventQueueFQ = "Wotc.Mtga.DuelScene.UXEvents.UXEventQueue";
        public const string UXEventFQ = "Wotc.Mtga.DuelScene.UXEvents.UXEvent";
    }
}
