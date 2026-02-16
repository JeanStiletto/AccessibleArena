using AccessibleArena.Core.Services;

namespace AccessibleArena.Core.Models
{
    /// <summary>
    /// Centralized storage for all user-facing announcement strings.
    /// All strings are resolved through LocaleManager for localization.
    /// </summary>
    public static class Strings
    {
        // Shorthand for locale manager
        private static LocaleManager L => LocaleManager.Instance;

        // Category filters
        private static bool ShowHints => AccessibleArenaMod.Instance?.Settings?.TutorialMessages ?? true;
        private static bool ShowVerbose => AccessibleArenaMod.Instance?.Settings?.VerboseAnnouncements ?? true;

        /// <summary>
        /// Appends a tutorial hint to a core message if TutorialMessages is enabled.
        /// </summary>
        public static string WithHint(string core, string hintKey) =>
            ShowHints ? $"{core}. {L.Get(hintKey)}" : core;

        /// <summary>
        /// Appends verbose detail to a core message if VerboseAnnouncements is enabled.
        /// </summary>
        public static string WithDetail(string core, string detail) =>
            ShowVerbose ? $"{core}. {detail}" : core;

        // ===========================================
        // GENERAL / SYSTEM
        // ===========================================
        public static string ModLoaded => L.Get("ModLoaded");
        public static string Back => L.Get("Back");
        public static string NoSelection => L.Get("NoSelection");
        public static string NoAlternateAction => L.Get("NoAlternateAction");
        public static string NoNextItem => L.Get("NoNextItem");
        public static string NoPreviousItem => L.Get("NoPreviousItem");
        public static string ItemDisabled => L.Get("ItemDisabled");

        // ===========================================
        // ACTIVATION
        // ===========================================
        public static string Activating(string name) => L.Format("Activating_Format", name);
        public static string CannotActivate(string name) => L.Format("CannotActivate_Format", name);
        public static string CouldNotPlay(string name) => L.Format("CouldNotPlay_Format", name);
        public static string NoAbilityAvailable(string name) => L.Format("NoAbilityAvailable_Format", name);
        public static string NoCardSelected => L.Get("NoCardSelected");

        // ===========================================
        // MENU NAVIGATION
        // ===========================================
        public static string NavigateWithArrows => L.Get("NavigateWithArrows");
        public static string BeginningOfList => L.Get("BeginningOfList");
        public static string EndOfList => L.Get("EndOfList");
        public static string OpeningPlayModes => L.Get("OpeningPlayModes");
        public static string OpeningDeckManager => L.Get("OpeningDeckManager");
        public static string OpeningStore => L.Get("OpeningStore");
        public static string OpeningMastery => L.Get("OpeningMastery");
        public static string OpeningProfile => L.Get("OpeningProfile");
        public static string OpeningSettings => L.Get("OpeningSettings");
        public static string QuittingGame => L.Get("QuittingGame");
        public static string CannotNavigateHome => L.Get("CannotNavigateHome");
        public static string HomeNotAvailable => L.Get("HomeNotAvailable");
        public static string ReturningHome => L.Get("ReturningHome");
        public static string OpeningColorChallenges => L.Get("OpeningColorChallenges");
        public static string NavigatingBack => L.Get("NavigatingBack");
        public static string ClosingSettings => L.Get("ClosingSettings");
        public static string ClosingPlayBlade => L.Get("ClosingPlayBlade");
        public static string ExitingDeckBuilder => L.Get("ExitingDeckBuilder");

        // ===========================================
        // DECK BUILDER INFO
        // ===========================================
        public static string DeckInfoCardCount => L.Get("DeckInfoCardCount");
        public static string DeckInfoManaCurve => L.Get("DeckInfoManaCurve");
        public static string DeckInfoTypeBreakdown => L.Get("DeckInfoTypeBreakdown");

        // ===========================================
        // LOGIN / ACCOUNT
        // ===========================================
        public static string BirthYearField => L.Get("BirthYearField");
        public static string BirthMonthField => L.Get("BirthMonthField");
        public static string BirthDayField => L.Get("BirthDayField");
        public static string CountryField => L.Get("CountryField");
        public static string EmailField => L.Get("EmailField");
        public static string PasswordField => L.Get("PasswordField");
        public static string ConfirmPasswordField => L.Get("ConfirmPasswordField");
        public static string AcceptTermsCheckbox => L.Get("AcceptTermsCheckbox");
        public static string LoggingIn => L.Get("LoggingIn");
        public static string CreatingAccount => L.Get("CreatingAccount");
        public static string SubmittingPasswordReset => L.Get("SubmittingPasswordReset");
        public static string CheckingQueuePosition => L.Get("CheckingQueuePosition");
        public static string OpeningSupportWebsite => L.Get("OpeningSupportWebsite");
        public static string NoTermsContentFound => L.Get("NoTermsContentFound");

        // ===========================================
        // BATTLEFIELD NAVIGATION
        // ===========================================
        public static string EndOfBattlefield => L.Get("EndOfBattlefield");
        public static string BeginningOfBattlefield => L.Get("BeginningOfBattlefield");
        public static string EndOfRow => L.Get("EndOfRow");
        public static string BeginningOfRow => L.Get("BeginningOfRow");
        public static string RowEmpty(string rowName) => L.Format("RowEmpty_Format", rowName);
        public static string RowWithCount(string rowName, int count) =>
            count == 1 ? L.Format("RowWithCount_One", rowName) : L.Format("RowWithCount_Format", rowName, count);
        public static string RowEmptyShort(string rowName) => L.Format("RowEmptyShort_Format", rowName);

        // ===========================================
        // ZONE NAVIGATION
        // ===========================================
        public static string EndOfZone => L.Get("EndOfZone");
        public static string BeginningOfZone => L.Get("BeginningOfZone");
        public static string ZoneNotFound(string zoneName) => L.Format("ZoneNotFound_Format", zoneName);
        public static string ZoneEmpty(string zoneName) => L.Format("ZoneEmpty_Format", zoneName);
        public static string ZoneWithCount(string zoneName, int count) =>
            count == 1 ? L.Format("ZoneWithCount_One", zoneName) : L.Format("ZoneWithCount_Format", zoneName, count);

        // ===========================================
        // TARGETING
        // ===========================================
        public static string NoValidTargets => L.Get("NoValidTargets");
        public static string NoTargetSelected => L.Get("NoTargetSelected");
        public static string TargetingCancelled => L.Get("TargetingCancelled");
        public static string SelectTargetNoValid => L.Get("SelectTargetNoValid");
        public static string Targeted(string name) => L.Format("Targeted_Format", name);
        public static string CouldNotTarget(string name) => L.Format("CouldNotTarget_Format", name);

        // ===========================================
        // COMBAT
        // ===========================================
        // Combat button activation uses language-agnostic detection (by button name, not text)

        // ===========================================
        // CARD ACTIONS
        // ===========================================
        public static string NoPlayableCards => L.Get("NoPlayableCards");
        public static string SpellCast => L.Get("SpellCast");
        public static string SpellCastPrefix => L.Get("SpellCastPrefix");
        public static string SpellUnknown => L.Get("SpellUnknown");
        public static string ResolveStackFirst => L.Get("ResolveStackFirst");

        // Ability announcements (for triggered/activated abilities on stack)
        public static string AbilityTriggered => L.Get("AbilityTriggered");
        public static string AbilityActivated => L.Get("AbilityActivated");
        public static string AbilityUnknown => L.Get("AbilityUnknown");

        // ===========================================
        // DISCARD
        // ===========================================
        public static string NoSubmitButtonFound => L.Get("NoSubmitButtonFound");
        public static string CouldNotSubmitDiscard => L.Get("CouldNotSubmitDiscard");
        public static string DiscardCount(int count) =>
            count == 1 ? L.Get("DiscardCount_One") : L.Format("DiscardCount_Format", count);
        public static string CardsSelected(int count) =>
            count == 1 ? L.Get("CardsSelected_One") : L.Format("CardsSelected_Format", count);
        public static string NeedHaveSelected(int required, int selected) =>
            L.Format("NeedHaveSelected_Format", required, selected);
        public static string SubmittingDiscard(int count) => L.Format("SubmittingDiscard_Format", count);
        public static string CouldNotSelect(string name) => L.Format("CouldNotSelect_Format", name);

        // ===========================================
        // CARD INFO
        // ===========================================
        public static string EndOfCard => L.Get("EndOfCard");
        public static string BeginningOfCard => L.Get("BeginningOfCard");

        // Card info block labels
        public static string CardInfoName => L.Get("CardInfoName");
        public static string CardInfoQuantity => L.Get("CardInfoQuantity");
        public static string CardInfoCollection => L.Get("CardInfoCollection");
        public static string CardInfoManaCost => L.Get("CardInfoManaCost");
        public static string CardInfoPowerToughness => L.Get("CardInfoPowerToughness");
        public static string CardInfoType => L.Get("CardInfoType");
        public static string CardInfoRules => L.Get("CardInfoRules");
        public static string CardInfoFlavor => L.Get("CardInfoFlavor");
        public static string CardInfoArtist => L.Get("CardInfoArtist");

        // ===========================================
        // POSITION / COUNTS
        // ===========================================
        public static string CardPosition(string cardName, string state, int position, int total) =>
            L.Format("CardPosition_Format", cardName, state ?? "", position, total);

        // ===========================================
        // HIDDEN ZONE INFO (Library, Opponent Hand)
        // ===========================================
        public static string LibraryCount(int count) =>
            count == 1 ? L.Get("LibraryCount_One") : L.Format("LibraryCount_Format", count);
        public static string OpponentLibraryCount(int count) =>
            count == 1 ? L.Get("OpponentLibraryCount_One") : L.Format("OpponentLibraryCount_Format", count);
        public static string OpponentHandCount(int count) =>
            count == 1 ? L.Get("OpponentHandCount_One") : L.Format("OpponentHandCount_Format", count);
        public static string LibraryCountNotAvailable => L.Get("LibraryCountNotAvailable");
        public static string OpponentLibraryCountNotAvailable => L.Get("OpponentLibraryCountNotAvailable");
        public static string OpponentHandCountNotAvailable => L.Get("OpponentHandCountNotAvailable");

        // ===========================================
        // PLAYER INFO ZONE
        // ===========================================
        public static string PlayerInfo => L.Get("PlayerInfo");
        public static string You => L.Get("You");
        public static string Opponent => L.Get("Opponent");
        public static string EndOfProperties => L.Get("EndOfProperties");
        public static string PlayerType => L.Get("PlayerType");

        // Property announcements
        public static string Life(int amount) => L.Format("Life_Format", amount);
        public static string LifeNotAvailable => L.Get("LifeNotAvailable");
        public static string Timer(string formatted) => formatted;
        public static string TimerNotAvailable => L.Get("TimerNotAvailable");
        public static string Timeouts(int count) =>
            count == 1 ? L.Get("Timeouts_One") : L.Format("Timeouts_Format", count);
        public static string GamesWon(int count) =>
            count == 1 ? L.Get("GamesWon_One") : L.Format("GamesWon_Format", count);
        public static string WinsNotAvailable => L.Get("WinsNotAvailable");
        public static string Rank(string rank) => rank;
        public static string RankNotAvailable => L.Get("RankNotAvailable");

        // Emote menu
        public static string Emotes => L.Get("Emotes");
        public static string EmoteSent(string emoteName) => L.Format("EmoteSent_Format", emoteName);
        public static string EmotesNotAvailable => L.Get("EmotesNotAvailable");

        // ===========================================
        // INPUT FIELD NAVIGATION
        // ===========================================
        public static string InputFieldEmpty => L.Get("InputFieldEmpty");
        public static string InputFieldStart => L.Get("InputFieldStart");
        public static string InputFieldEnd => L.Get("InputFieldEnd");
        public static string InputFieldStar => L.Get("InputFieldStar");
        public static string InputFieldCharacterCount(int count) =>
            count == 1 ? L.Get("InputFieldCharacterCount_One") : L.Format("InputFieldCharacterCount_Format", count);
        public static string InputFieldContent(string label, string content) =>
            L.Format("InputFieldContent_Format", label, content);
        public static string InputFieldEmptyWithLabel(string label) =>
            L.Format("InputFieldEmptyWithLabel_Format", label);
        public static string InputFieldPasswordWithCount(string label, int count) =>
            L.Format("InputFieldPasswordWithCount_Format", label, count);

        // Character names for cursor navigation
        public static string CharSpace => L.Get("CharSpace");
        public static string CharDot => L.Get("CharDot");
        public static string CharComma => L.Get("CharComma");
        public static string CharExclamation => L.Get("CharExclamation");
        public static string CharQuestion => L.Get("CharQuestion");
        public static string CharAt => L.Get("CharAt");
        public static string CharHash => L.Get("CharHash");
        public static string CharDollar => L.Get("CharDollar");
        public static string CharPercent => L.Get("CharPercent");
        public static string CharAnd => L.Get("CharAnd");
        public static string CharStar => L.Get("CharStar");
        public static string CharDash => L.Get("CharDash");
        public static string CharUnderscore => L.Get("CharUnderscore");
        public static string CharPlus => L.Get("CharPlus");
        public static string CharEquals => L.Get("CharEquals");
        public static string CharSlash => L.Get("CharSlash");
        public static string CharBackslash => L.Get("CharBackslash");
        public static string CharColon => L.Get("CharColon");
        public static string CharSemicolon => L.Get("CharSemicolon");
        public static string CharQuote => L.Get("CharQuote");
        public static string CharApostrophe => L.Get("CharApostrophe");
        public static string CharOpenParen => L.Get("CharOpenParen");
        public static string CharCloseParen => L.Get("CharCloseParen");
        public static string CharOpenBracket => L.Get("CharOpenBracket");
        public static string CharCloseBracket => L.Get("CharCloseBracket");
        public static string CharOpenBrace => L.Get("CharOpenBrace");
        public static string CharCloseBrace => L.Get("CharCloseBrace");
        public static string CharLessThan => L.Get("CharLessThan");
        public static string CharGreaterThan => L.Get("CharGreaterThan");
        public static string CharPipe => L.Get("CharPipe");
        public static string CharTilde => L.Get("CharTilde");
        public static string CharBacktick => L.Get("CharBacktick");
        public static string CharCaret => L.Get("CharCaret");

        /// <summary>
        /// Get a speakable name for a character (handles spaces, punctuation, etc.)
        /// Used for input field cursor navigation announcements.
        /// </summary>
        public static string GetCharacterName(char c)
        {
            if (char.IsWhiteSpace(c))
                return CharSpace;
            if (char.IsDigit(c))
                return c.ToString();
            if (char.IsLetter(c))
                return c.ToString();

            // Common punctuation - mapped to locale keys
            return c switch
            {
                '.' => CharDot,
                ',' => CharComma,
                '!' => CharExclamation,
                '?' => CharQuestion,
                '@' => CharAt,
                '#' => CharHash,
                '$' => CharDollar,
                '%' => CharPercent,
                '&' => CharAnd,
                '*' => CharStar,
                '-' => CharDash,
                '_' => CharUnderscore,
                '+' => CharPlus,
                '=' => CharEquals,
                '/' => CharSlash,
                '\\' => CharBackslash,
                ':' => CharColon,
                ';' => CharSemicolon,
                '"' => CharQuote,
                '\'' => CharApostrophe,
                '(' => CharOpenParen,
                ')' => CharCloseParen,
                '[' => CharOpenBracket,
                ']' => CharCloseBracket,
                '{' => CharOpenBrace,
                '}' => CharCloseBrace,
                '<' => CharLessThan,
                '>' => CharGreaterThan,
                '|' => CharPipe,
                '~' => CharTilde,
                '`' => CharBacktick,
                '^' => CharCaret,
                _ => c.ToString()
            };
        }

        // ===========================================
        // MANA SYMBOLS (for rules text parsing)
        // ===========================================
        public static string ManaTap => L.Get("ManaTap");
        public static string ManaUntap => L.Get("ManaUntap");
        public static string ManaWhite => L.Get("ManaWhite");
        public static string ManaBlue => L.Get("ManaBlue");
        public static string ManaBlack => L.Get("ManaBlack");
        public static string ManaRed => L.Get("ManaRed");
        public static string ManaGreen => L.Get("ManaGreen");
        public static string ManaColorless => L.Get("ManaColorless");
        public static string ManaX => L.Get("ManaX");
        public static string ManaSnow => L.Get("ManaSnow");
        public static string ManaEnergy => L.Get("ManaEnergy");
        public static string ManaPhyrexian(string color) => L.Format("ManaPhyrexian_Format", color);
        public static string ManaHybrid(string color1, string color2) => L.Format("ManaHybrid_Format", color1, color2);

        // ===========================================
        // SETTINGS MENU
        // ===========================================
        public static string SettingsMenuTitle => L.Get("SettingsMenuTitle");
        public static string SettingsMenuInstructions => L.Get("SettingsMenuInstructions");
        public static string SettingsMenuClosed => L.Get("SettingsMenuClosed");
        public static string SettingLanguage => L.Get("SettingLanguage");
        public static string SettingTutorialMessages => L.Get("SettingTutorialMessages");
        public static string SettingVerboseAnnouncements => L.Get("SettingVerboseAnnouncements");
        public static string SettingOn => L.Get("SettingOn");
        public static string SettingOff => L.Get("SettingOff");
        public static string SettingChanged(string name, string value) => L.Format("SettingChanged_Format", name, value);
        public static string SettingItemPosition(int index, int total, string text) => L.Format("SettingItemPosition_Format", index, total, text);

        // ===========================================
        // HELP MENU
        // ===========================================
        public static string HelpMenuTitle => L.Get("HelpMenuTitle");
        public static string HelpMenuInstructions => L.Get("HelpMenuInstructions");
        public static string HelpItemPosition(int index, int total, string text) => L.Format("HelpItemPosition_Format", index, total, text);
        public static string HelpMenuClosed => L.Get("HelpMenuClosed");

        // Help categories
        public static string HelpCategoryGlobal => L.Get("HelpCategoryGlobal");
        public static string HelpCategoryMenuNavigation => L.Get("HelpCategoryMenuNavigation");
        public static string HelpCategoryDuelZones => L.Get("HelpCategoryDuelZones");
        public static string HelpCategoryDuelInfo => L.Get("HelpCategoryDuelInfo");
        public static string HelpCategoryCardNavigation => L.Get("HelpCategoryCardNavigation");
        public static string HelpCategoryCardDetails => L.Get("HelpCategoryCardDetails");
        public static string HelpCategoryCombat => L.Get("HelpCategoryCombat");
        public static string HelpCategoryBrowser => L.Get("HelpCategoryBrowser");

        // Global shortcuts
        public static string HelpF1Help => L.Get("HelpF1Help");
        public static string HelpF2Settings => L.Get("HelpF2Settings");
        public static string HelpF3Context => L.Get("HelpF3Context");
        public static string HelpCtrlRRepeat => L.Get("HelpCtrlRRepeat");
        public static string HelpBackspace => L.Get("HelpBackspace");

        // Menu navigation
        public static string HelpArrowUpDown => L.Get("HelpArrowUpDown");
        public static string HelpTabNavigation => L.Get("HelpTabNavigation");
        public static string HelpArrowLeftRight => L.Get("HelpArrowLeftRight");
        public static string HelpHomeEnd => L.Get("HelpHomeEnd");
        public static string HelpPageUpDown => L.Get("HelpPageUpDown");
        public static string HelpNumberKeysFilters => L.Get("HelpNumberKeysFilters");
        public static string HelpEnterSpace => L.Get("HelpEnterSpace");

        // Input fields (text entry)
        public static string HelpCategoryInputFields => L.Get("HelpCategoryInputFields");
        public static string HelpEnterEditField => L.Get("HelpEnterEditField");
        public static string HelpEscapeExitField => L.Get("HelpEscapeExitField");
        public static string HelpTabNextField => L.Get("HelpTabNextField");
        public static string HelpShiftTabPrevField => L.Get("HelpShiftTabPrevField");
        public static string HelpArrowsInField => L.Get("HelpArrowsInField");

        // Zones (yours and opponent)
        public static string HelpCHand => L.Get("HelpCHand");
        public static string HelpBBattlefield => L.Get("HelpBBattlefield");
        public static string HelpALands => L.Get("HelpALands");
        public static string HelpRNonCreatures => L.Get("HelpRNonCreatures");
        public static string HelpGGraveyard => L.Get("HelpGGraveyard");
        public static string HelpXExile => L.Get("HelpXExile");
        public static string HelpSStack => L.Get("HelpSStack");
        public static string HelpDLibrary => L.Get("HelpDLibrary");

        // Duel info
        public static string HelpLLifeTotals => L.Get("HelpLLifeTotals");
        public static string HelpTTurnPhase => L.Get("HelpTTurnPhase");
        public static string HelpVPlayerInfo => L.Get("HelpVPlayerInfo");

        // Card navigation
        public static string HelpLeftRightCards => L.Get("HelpLeftRightCards");
        public static string HelpHomeEndCards => L.Get("HelpHomeEndCards");
        public static string HelpEnterPlay => L.Get("HelpEnterPlay");
        public static string HelpTabTargets => L.Get("HelpTabTargets");

        // Card details
        public static string HelpUpDownDetails => L.Get("HelpUpDownDetails");

        // Combat
        public static string HelpSpaceCombat => L.Get("HelpSpaceCombat");
        public static string HelpBackspaceCombat => L.Get("HelpBackspaceCombat");

        // Browser
        public static string HelpTabBrowser => L.Get("HelpTabBrowser");
        public static string HelpCDZones => L.Get("HelpCDZones");
        public static string HelpEnterToggle => L.Get("HelpEnterToggle");
        public static string HelpSpaceConfirm => L.Get("HelpSpaceConfirm");

        // Debug keys
        public static string HelpCategoryDebug => L.Get("HelpCategoryDebug");
        public static string HelpF4Refresh => L.Get("HelpF4Refresh");
        public static string HelpF11CardDump => L.Get("HelpF11CardDump");
        public static string HelpF12UIDump => L.Get("HelpF12UIDump");

        // ===========================================
        // BROWSER (Scry, Surveil, Mulligan, etc.)
        // ===========================================
        public static string NoCards => L.Get("NoCards");
        public static string NoButtonSelected => L.Get("NoButtonSelected");
        public static string NoButtonsAvailable => L.Get("NoButtonsAvailable");
        public static string CouldNotTogglePosition => L.Get("CouldNotTogglePosition");
        public static string Selected => L.Get("Selected");
        public static string Deselected => L.Get("Deselected");
        public static string InHand => L.Get("InHand");
        public static string OnStack => L.Get("OnStack");
        public static string Confirmed => L.Get("Confirmed");
        public static string Cancelled => L.Get("Cancelled");
        public static string NoConfirmButton => L.Get("NoConfirmButton");
        public static string KeepOnTop => L.Get("KeepOnTop");
        public static string PutOnBottom => L.Get("PutOnBottom");
        public static string CouldNotClick(string label) => L.Format("CouldNotClick_Format", label);
        public static string BrowserCards(int count, string browserName) =>
            count == 1 ? L.Format("BrowserCards_One", browserName) : L.Format("BrowserCards_Format", browserName, count);
        public static string BrowserOptions(string browserName) => L.Format("BrowserOptions_Format", browserName);

        // ===========================================
        // MASTERY SCREEN
        // ===========================================
        public static string MasteryActivation(string trackName, int level, int total, string xp) =>
            L.Format("MasteryActivation_Format", trackName, level, total, xp);
        public static string MasteryLevel(int level, string reward, string status) =>
            string.IsNullOrEmpty(status)
                ? L.Format("MasteryLevel_Format", level, reward)
                : L.Format("MasteryLevelWithStatus_Format", level, reward, status);
        public static string MasteryTier(string tierName, string reward, int quantity) =>
            quantity > 1 ? L.Format("MasteryTierWithQuantity_Format", tierName, quantity, reward) : L.Format("MasteryTier_Format", tierName, reward);
        public static string MasteryPage(int current, int total) => L.Format("MasteryPage_Format", current, total);
        public static string MasteryLevelDetail(int level, string tiers, string status) =>
            string.IsNullOrEmpty(status)
                ? L.Format("MasteryLevelDetail_Format", level, tiers)
                : L.Format("MasteryLevelDetailWithStatus_Format", level, tiers, status);
        public static string MasteryCompleted => L.Get("MasteryCompleted");
        public static string MasteryCurrentLevel => L.Get("MasteryCurrentLevel");
        public static string MasteryPremiumLocked => L.Get("MasteryPremiumLocked");
        public static string MasteryFree => L.Get("MasteryFree");
        public static string MasteryPremium => L.Get("MasteryPremium");
        public static string MasteryRenewal => L.Get("MasteryRenewal");
        public static string MasteryNoReward => L.Get("MasteryNoReward");
        public static string MasteryStatus => L.Get("MasteryStatus");
        public static string MasteryStatusInfo(int level, int total, string xp) =>
            string.IsNullOrEmpty(xp)
                ? L.Format("MasteryStatusInfo_Format", level, total)
                : L.Format("MasteryStatusInfoWithXP_Format", level, total, xp);

        // ===========================================
        // PRIZE WALL
        // ===========================================
        public static string PrizeWallActivation(int itemCount, string spheres) =>
            L.Format("PrizeWallActivation_Format", itemCount, spheres);
        public static string PrizeWallItem(int index, int total, string name) =>
            L.Format("PrizeWallItem_Format", index, total, name);
        public static string PrizeWallSphereStatus(string spheres) =>
            L.Format("PrizeWallSphereStatus_Format", spheres);
        public static string PopupCancel => L.Get("PopupCancel");

        // ===========================================
        // INLINE STRING MIGRATIONS
        // ===========================================
        public static string SearchResults(int count) => L.Format("SearchResults_Format", count);
        public static string SearchResultsItems(int count) => L.Format("SearchResultsItems_Format", count);
        public static string ExitedEditMode => L.Get("ExitedEditMode");
        public static string DropdownClosed => L.Get("DropdownClosed");
        public static string PopupClosed => L.Get("PopupClosed");
        public static string Percent(int value) => L.Format("Percent_Format", value);
        public static string ActionNotAvailable => L.Get("ActionNotAvailable");
        public static string EditingTextField => L.Get("EditingTextField");
        public static string ManaAmount(string mana) => L.Format("Mana_Format", mana);
        public static string FirstSection => L.Get("FirstSection");
        public static string LastSection => L.Get("LastSection");
        public static string StartOfRow => L.Get("StartOfRow");
        public static string EndOfRowNav => L.Get("EndOfRowNav");
        public static string ApplyingFilters => L.Get("ApplyingFilters");
        public static string FiltersReset => L.Get("FiltersReset");
        public static string FiltersCancelled => L.Get("FiltersCancelled");
        public static string FiltersDismissed => L.Get("FiltersDismissed");
        public static string CouldNotClosePopup => L.Get("CouldNotClosePopup");
        public static string Opening(string name) => L.Format("Opening_Format", name);
        public static string Toggled(string label) => L.Format("Toggled_Format", label);
        public static string FirstPack => L.Get("FirstPack");
        public static string LastPack => L.Get("LastPack");
        public static string ExitedInputField => L.Get("ExitedInputField");
        public static string PageOf(int current, int total) => L.Format("Page_Format", current, total);
        public static string PageLabel(string label) => L.Format("PageLabel_Format", label);
        public static string FilterLabel(string label, string state) => L.Format("FilterLabel_Format", label, state);
        public static string Activated(string label) => L.Format("Activated_Format", label);
        public static string NoFilter(int index, int count) => L.Format("NoFilter_Format", index, count);
        public static string NoFiltersAvailable => L.Get("NoFiltersAvailable");
        public static string BackToMailList => L.Get("BackToMailList");
        public static string AtTopLevel => L.Get("AtTopLevel");
        public static string NoItemsAvailable(string name) => L.Format("NoItemsAvailable_Format", name);
        public static string Loading(string name) => L.Format("Loading_Format", name);
        public static string TabItems(string name, int count) => L.Format("TabItems_Format", name, count);
        public static string TabNoItems(string name) => L.Format("TabNoItems_Format", name);
        public static string NoPurchaseOption => L.Get("NoPurchaseOption");
        public static string NoDetailsAvailable => L.Get("NoDetailsAvailable");
        public static string NoCardDetails => L.Get("NoCardDetails");
        public static string TabsCount(int count) => L.Format("Tabs_Format", count);
        public static string OptionsAvailable(int count, string hint) => L.Format("OptionsAvailable_Format", count, hint);
        public static string Continuing => L.Get("Continuing");
        public static string FoundRewards(int count) => L.Format("FoundRewards_Format", count);
        public static string Characters(int count) => L.Format("Characters_Format", count);
        public static string PaymentPage(int count) => L.Format("PaymentPage_Format", count);
        public static string DropdownOpened => L.Get("DropdownOpened");
        public static string CouldNotMove(string name) => L.Format("CouldNotMove_Format", name);
        public static string MovedTo(string card, string zone) => L.Format("MovedTo_Format", card, zone);
        public static string ZoneEntry(string zoneName, int count, string cardName) =>
            L.Format("ZoneEntry_Format", zoneName, count, cardName, count);
        public static string ZoneEntryEmpty(string zoneName) => L.Format("ZoneEntryEmpty_Format", zoneName);
        public static string CardInZone(string cardName, string zoneName, int index, int total) =>
            L.Format("CardInZone_Format", cardName, zoneName, index, total);
        public static string CouldNotSend(string name) => L.Format("CouldNotSend_Format", name);
        public static string PortraitNotFound => L.Get("PortraitNotFound");
        public static string PortraitNotAvailable => L.Get("PortraitNotAvailable");
        public static string PortraitButtonNotFound => L.Get("PortraitButtonNotFound");
        public static string NoActiveScreen => L.Get("NoActiveScreen");
        public static string NoCardToInspect => L.Get("NoCardToInspect");
        public static string NoElementSelected => L.Get("NoElementSelected");
        public static string DebugDumpComplete => L.Get("DebugDumpComplete");
        public static string CardDetailsDumped => L.Get("CardDetailsDumped");
        public static string NoPackToInspect => L.Get("NoPackToInspect");
        public static string CouldNotFindPackParent => L.Get("CouldNotFindPackParent");
        public static string PackDetailsDumped => L.Get("PackDetailsDumped");
        public static string WaitingForPlayable => L.Get("WaitingForPlayable");
        public static string NoSearchResults => L.Get("NoSearchResults");
        public static string EnterToSelect => L.Get("EnterToSelect");

        // ===========================================
        // ELEMENT GROUPS
        // ===========================================
        public static string GroupName(Services.ElementGrouping.ElementGroup group)
        {
            switch (group)
            {
                case Services.ElementGrouping.ElementGroup.Primary: return L.Get("GroupPrimaryActions");
                case Services.ElementGrouping.ElementGroup.Play: return L.Get("GroupPlay");
                case Services.ElementGrouping.ElementGroup.Progress: return L.Get("GroupProgress");
                case Services.ElementGrouping.ElementGroup.Objectives: return L.Get("GroupObjectives");
                case Services.ElementGrouping.ElementGroup.Social: return L.Get("GroupSocial");
                case Services.ElementGrouping.ElementGroup.Filters: return L.Get("GroupFilters");
                case Services.ElementGrouping.ElementGroup.Content: return L.Get("GroupContent");
                case Services.ElementGrouping.ElementGroup.Settings: return L.Get("GroupSettings");
                case Services.ElementGrouping.ElementGroup.Secondary: return L.Get("GroupSecondaryActions");
                case Services.ElementGrouping.ElementGroup.Popup: return L.Get("GroupDialog");
                case Services.ElementGrouping.ElementGroup.FriendsPanel: return L.Get("GroupFriends");
                case Services.ElementGrouping.ElementGroup.PlayBladeTabs: return L.Get("GroupTabs");
                case Services.ElementGrouping.ElementGroup.PlayBladeContent: return L.Get("GroupPlayOptions");
                case Services.ElementGrouping.ElementGroup.PlayBladeFolders: return L.Get("GroupFolders");
                case Services.ElementGrouping.ElementGroup.SettingsMenu: return L.Get("GroupSettingsMenu");
                case Services.ElementGrouping.ElementGroup.NPE: return L.Get("GroupTutorial");
                case Services.ElementGrouping.ElementGroup.DeckBuilderCollection: return L.Get("GroupCollection");
                case Services.ElementGrouping.ElementGroup.DeckBuilderDeckList: return L.Get("GroupDeckList");
                case Services.ElementGrouping.ElementGroup.DeckBuilderInfo: return L.Get("GroupDeckInfo");
                case Services.ElementGrouping.ElementGroup.MailboxList: return L.Get("GroupMailList");
                case Services.ElementGrouping.ElementGroup.MailboxContent: return L.Get("GroupMail");
                case Services.ElementGrouping.ElementGroup.RewardsPopup: return L.Get("GroupRewards");
                default: return L.Get("GroupOther");
            }
        }

        public static string NoItemsFound => L.Get("NoItemsFound");
        public static string NoNavigableItemsFound => L.Get("NoNavigableItemsFound");
        public static string ItemCount(int count) =>
            count == 1 ? L.Get("ItemCount_One") : L.Format("ItemCount_Format", count);
        public static string GroupCount(int count) => L.Format("GroupCount_Format", count);
        public static string GroupItemCount(string groupName, string itemCount) =>
            L.Format("GroupItemCount_Format", groupName, itemCount);
        public static string ItemPositionOf(int index, int total, string label) =>
            L.Format("ItemPositionOf_Format", index, total, label);
        public static string ScreenGroupsSummary(string screenName, string groupCount, string currentAnnouncement) =>
            L.Format("ScreenGroupsSummary_Format", screenName, groupCount, currentAnnouncement);
        public static string ScreenItemsSummary(string screenName, string itemCount, string firstElement) =>
            L.Format("ScreenItemsSummary_Format", screenName, itemCount, firstElement);
        public static string ObjectivesEntry(string itemCount) =>
            L.Format("ObjectivesEntry_Format", itemCount);
    }
}
