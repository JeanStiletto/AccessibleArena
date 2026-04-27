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
        public const string CommanderSlotCardHolder = "CommanderSlotCardHolder";
        public const string ListCommanderHolder = "ListCommanderHolder";

        // --- UI Component Types ---
        public const string CustomButton = "CustomButton";
        public const string CustomButtonWithTooltip = "CustomButtonWithTooltip";
        public const string SystemMessageButtonView = "SystemMessageButtonView";
        public const string CustomTMPDropdown = "cTMP_Dropdown";

        // --- Booster Chamber Types ---
        public const string BoosterOpenToScrollListController = "BoosterOpenToScrollListController";
        public const string BoosterCardHolder = "BoosterCardHolder";
        public const string SealedBoosterOpenAnimation = "SealedBoosterOpenAnimation";

        // --- Card Holder Types ---
        public const string CardPoolHolder = "CardPoolHolder";
        public const string ScrollCardPoolHolder = "ScrollCardPoolHolder";
        public const string CardBrowserCardHolder = "CardBrowserCardHolder";
        public const string ListMetaCardHolder = "ListMetaCardHolder";

        // --- Content Controllers ---
        public const string HomePageContentController = "HomePageContentController";
        public const string EventPageContentController = "EventPageContentController";
        public const string FactionalizedEventTemplate = "FactionalizedEventTemplate";
        public const string MainButtonComponent = "MainButtonComponent";
        public const string PacketSelectContentController = "PacketSelectContentController";
        public const string CampaignGraphContentController = "CampaignGraphContentController";
        public const string LearnToPlayControllerV2 = "LearnToPlayControllerV2";
        public const string DeckManagerController = "DeckManagerController";
        public const string DraftContentController = "DraftContentController";
        public const string ProgressionTracksContentController = "ProgressionTracksContentController";
        public const string ContentControllerStoreCarousel = "ContentController_StoreCarousel";
        public const string WrapperDeckBuilder = "WrapperDeckBuilder";
        public const string BoosterChamberController = "BoosterChamberController";
        public const string AchievementsContentController = "AchievementsContentController";
        public const string MasteryContentController = "MasteryContentController";
        public const string PackOpeningController = "PackOpeningController";
        public const string ConstructedDeckSelectController = "ConstructedDeckSelectController";
        public const string SealedBoosterOpenController = "SealedBoosterOpenController";

        // --- Color Challenge Types ---
        public const string CampaignGraphTrackModule = "CampaignGraphTrackModule";
        public const string CampaignGraphObjectiveBubble = "CampaignGraphObjectiveBubble";

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

        // --- Sideboard Types ---
        public const string SideboardInterface = "SideboardInterface";
        public const string SideboardNavBar = "SideboardNavBar";
        public const string DeckBuilderWidget = "DeckBuilderWidget";

        // --- Spinner Types (counter distribution) ---
        public const string SpinnerAnimated = "SpinnerAnimated";
        public const string SpinnerGroup = "SpinnerGroup";

        // --- Game / Duel Types ---
        public const string GameManager = "GameManager";
        public const string MatchManager = "MatchManager";
        public const string MatchTimer = "MatchTimer";
        public const string LowTimeWarning = "LowTimeWarning";

        // --- Social Types ---
        public const string FriendTile = "FriendTile";
        public const string InviteOutgoingTile = "InviteOutgoingTile";
        public const string InviteIncomingTile = "InviteIncomingTile";
        public const string BlockTile = "BlockTile";
        public const string IncomingChallengeRequestTile = "IncomingChallengeRequestTile";
        public const string CurrentChallengeTile = "CurrentChallengeTile";
        public const string ChallengeInviteWindowPopup = "ChallengeInviteWindowPopup";
        public const string ChallengeInviteWindowEntityPlayer = "ChallengeInviteWindowEntityPlayer";
        public const string ChallengeInviteWindowEntityInvited = "ChallengeInviteWindowEntityInvited";
        public const string FriendsWidget = "FriendsWidget";
        public const string ChatWindow = "ChatWindow";
        public const string ChatManager = "ChatManager";
        public const string SocialMessagesView = "SocialMessagesView";
        public const string MessageTile = "MessageTile";
        public const string Conversation = "Conversation";
        public const string SocialMessage = "SocialMessage";

        // --- Profile Types ---
        public const string ProfileContentController = "ProfileContentController";
        public const string ProfileDetailsPanel = "ProfileDetailsPanel";
        public const string RankDisplay = "RankDisplay";
        public const string CosmeticSelectorController = "CosmeticSelectorController";
        public const string AvatarSelectPanel = "AvatarSelectPanel";
        public const string EmoteSelectionScreenView = "EmoteSelectionScreenView";
        public const string DisplayItemAvatar = "DisplayItemAvatar";
        public const string DisplayItemTitle = "DisplayItemTitle";
        public const string DisplayItemEmote = "DisplayItemEmote";
        public const string DisplayItemPet = "DisplayItemPet";
        public const string DisplayItemSleeve = "DisplayItemSleeve";
        public const string ObjectiveBubble = "ObjectiveBubble";
        public const string SetBadge = "SetBadge";

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
