namespace MTGAAccessibility.Core.Models
{
    /// <summary>
    /// Centralized storage for all user-facing announcement strings.
    /// This enables future localization support.
    /// </summary>
    public static class Strings
    {
        // ===========================================
        // GENERAL / SYSTEM
        // ===========================================
        public const string ModLoaded = "MTGA Accessibility Mod loaded";
        public const string Back = "Back";
        public const string NoSelection = "No selection";
        public const string NoAlternateAction = "No alternate action available";
        public const string NoNextItem = "No next item";
        public const string NoPreviousItem = "No previous item";
        public const string ItemDisabled = "Item is disabled";

        // ===========================================
        // ACTIVATION
        // ===========================================
        public static string Activating(string name) => $"Activating {name}";
        public static string CannotActivate(string name) => $"Cannot activate {name}";
        public static string CouldNotPlay(string name) => $"Could not play {name}";
        public static string NoAbilityAvailable(string name) => $"{name} has no activatable ability";
        public const string NoCardSelected = "No card selected";

        // ===========================================
        // MENU NAVIGATION
        // ===========================================
        public const string OpeningPlayModes = "Opening play modes...";
        public const string OpeningDeckManager = "Opening deck manager...";
        public const string OpeningStore = "Opening store...";
        public const string OpeningMastery = "Opening mastery...";
        public const string OpeningProfile = "Opening profile...";
        public const string OpeningSettings = "Opening settings...";
        public const string QuittingGame = "Quitting game...";
        public const string CannotNavigateHome = "Cannot navigate to Home";
        public const string HomeNotAvailable = "Home button not available";
        public const string ReturningHome = "Returning to Home";
        public const string OpeningColorChallenges = "Opening color challenges";
        public const string NavigatingBack = "Back";
        public const string ClosingSettings = "Closing settings";
        public const string ClosingPlayBlade = "Closing play menu";

        // ===========================================
        // LOGIN / ACCOUNT
        // ===========================================
        public const string BirthYearField = "Birth year field. Use arrow keys to select year.";
        public const string BirthMonthField = "Birth month field. Use arrow keys to select month.";
        public const string BirthDayField = "Birth day field. Use arrow keys to select day.";
        public const string CountryField = "Country field. Use arrow keys to select country.";
        public const string EmailField = "Email field. Type your email address.";
        public const string PasswordField = "Password field. Type your password.";
        public const string ConfirmPasswordField = "Confirm password field. Retype your password.";
        public const string AcceptTermsCheckbox = "Accept terms checkbox. Press Enter to toggle.";
        public const string LoggingIn = "Logging in...";
        public const string CreatingAccount = "Creating account...";
        public const string SubmittingPasswordReset = "Submitting password reset request...";
        public const string CheckingQueuePosition = "Checking queue position...";
        public const string OpeningSupportWebsite = "Opening support website...";
        public const string NoTermsContentFound = "No terms content found";

        // ===========================================
        // BATTLEFIELD NAVIGATION
        // ===========================================
        public const string EndOfBattlefield = "End of battlefield";
        public const string BeginningOfBattlefield = "Beginning of battlefield";
        public const string EndOfRow = "End of row";
        public const string BeginningOfRow = "Beginning of row";
        public static string RowEmpty(string rowName) => $"{rowName} is empty";
        public static string RowWithCount(string rowName, int count) => $"{rowName}, {count} card{(count != 1 ? "s" : "")}";
        public static string RowEmptyShort(string rowName) => $"{rowName}, empty";

        // ===========================================
        // ZONE NAVIGATION
        // ===========================================
        public const string EndOfZone = "End of zone";
        public const string BeginningOfZone = "Beginning of zone";
        public static string ZoneNotFound(string zoneName) => $"{zoneName} not found";
        public static string ZoneEmpty(string zoneName) => $"{zoneName}, empty";
        public static string ZoneWithCount(string zoneName, int count) => $"{zoneName}, {count} card{(count != 1 ? "s" : "")}";

        // ===========================================
        // TARGETING
        // ===========================================
        public const string NoValidTargets = "No valid targets";
        public const string NoTargetSelected = "No target selected";
        public const string TargetingCancelled = "Targeting cancelled";
        public const string SelectTargetNoValid = "Select a target. No valid targets found.";
        public static string Targeted(string name) => $"Targeted {name}";
        public static string CouldNotTarget(string name) => $"Could not target {name}";

        // ===========================================
        // COMBAT
        // ===========================================
        // Combat button activation uses language-agnostic detection (by button name, not text)

        // ===========================================
        // CARD ACTIONS
        // ===========================================
        public const string NoPlayableCards = "No playable cards";
        public const string SpellCast = "Spell cast";
        public const string ResolveStackFirst = "Resolve stack first. Press Space to resolve or Tab to select targets.";

        // ===========================================
        // DISCARD
        // ===========================================
        public const string NoSubmitButtonFound = "No submit button found";
        public const string CouldNotSubmitDiscard = "Could not submit discard";
        public static string DiscardCount(int count) => $"Discard {count} {(count == 1 ? "card" : "cards")}";
        public static string CardsSelected(int count) => count == 1 ? "1 card selected" : $"{count} cards selected";
        public static string NeedHaveSelected(int required, int selected) => $"Need {required}, have {selected} selected";
        public static string SubmittingDiscard(int count) => $"Submitting {count} cards for discard";
        public static string CouldNotSelect(string name) => $"Could not select {name}";

        // ===========================================
        // CARD INFO
        // ===========================================
        public const string EndOfCard = "End of card";
        public const string BeginningOfCard = "Beginning of card";

        // Card info block labels
        public const string CardInfoName = "Name";
        public const string CardInfoManaCost = "Mana Cost";
        public const string CardInfoPowerToughness = "Power and Toughness";
        public const string CardInfoType = "Type";
        public const string CardInfoRules = "Rules";
        public const string CardInfoFlavor = "Flavor";
        public const string CardInfoArtist = "Artist";

        // ===========================================
        // POSITION / COUNTS
        // ===========================================
        public static string CardPosition(string cardName, string state, int position, int total) =>
            string.IsNullOrEmpty(state)
                ? $"{cardName}, {position} of {total}"
                : $"{cardName}{state}, {position} of {total}";

        // ===========================================
        // HIDDEN ZONE INFO (Library, Opponent Hand)
        // ===========================================
        public static string LibraryCount(int count) => $"Library, {count} {(count == 1 ? "card" : "cards")}";
        public static string OpponentLibraryCount(int count) => $"Opponent's library, {count} {(count == 1 ? "card" : "cards")}";
        public static string OpponentHandCount(int count) => $"Opponent's hand, {count} {(count == 1 ? "card" : "cards")}";
        public const string LibraryCountNotAvailable = "Library count not available";
        public const string OpponentLibraryCountNotAvailable = "Opponent's library count not available";
        public const string OpponentHandCountNotAvailable = "Opponent's hand count not available";

        // ===========================================
        // PLAYER INFO ZONE
        // ===========================================
        public const string PlayerInfo = "Player info";
        public const string You = "You";
        public const string Opponent = "Opponent";
        public const string EndOfProperties = "End of properties";
        public const string PlayerType = "player";

        // Property announcements
        public static string Life(int amount) => $"{amount} life";
        public const string LifeNotAvailable = "Life not available";
        public static string Timer(string formatted) => formatted;
        public const string TimerNotAvailable = "Timer not available";
        public static string Timeouts(int count) => count == 1 ? "1 timeout" : $"{count} timeouts";
        public static string GamesWon(int count) => count == 1 ? "1 game won" : $"{count} games won";
        public const string WinsNotAvailable = "Wins not available";
        public static string Rank(string rank) => rank;
        public const string RankNotAvailable = "Rank not available";

        // Emote menu
        public const string Emotes = "Emotes";
        public static string EmoteSent(string emoteName) => $"{emoteName} sent";
        public const string EmotesNotAvailable = "Emotes not available";

        // ===========================================
        // INPUT FIELD NAVIGATION
        // ===========================================
        public const string InputFieldEmpty = "empty";
        public const string InputFieldStart = "start";
        public const string InputFieldEnd = "end";
        public const string InputFieldStar = "star"; // For password characters
        public static string InputFieldCharacterCount(int count) => $"{count} characters";
        public static string InputFieldContent(string label, string content) => $"{label}: {content}";
        public static string InputFieldEmptyWithLabel(string label) => $"{label}, empty";
        public static string InputFieldPasswordWithCount(string label, int count) => $"{label}, {count} characters";

        // Character names for cursor navigation
        public const string CharSpace = "space";
        public const string CharDot = "dot";
        public const string CharComma = "comma";
        public const string CharExclamation = "exclamation";
        public const string CharQuestion = "question";
        public const string CharAt = "at";
        public const string CharHash = "hash";
        public const string CharDollar = "dollar";
        public const string CharPercent = "percent";
        public const string CharAnd = "and";
        public const string CharStar = "star";
        public const string CharDash = "dash";
        public const string CharUnderscore = "underscore";
        public const string CharPlus = "plus";
        public const string CharEquals = "equals";
        public const string CharSlash = "slash";
        public const string CharBackslash = "backslash";
        public const string CharColon = "colon";
        public const string CharSemicolon = "semicolon";
        public const string CharQuote = "quote";
        public const string CharApostrophe = "apostrophe";
        public const string CharOpenParen = "open paren";
        public const string CharCloseParen = "close paren";
        public const string CharOpenBracket = "open bracket";
        public const string CharCloseBracket = "close bracket";
        public const string CharOpenBrace = "open brace";
        public const string CharCloseBrace = "close brace";
        public const string CharLessThan = "less than";
        public const string CharGreaterThan = "greater than";
        public const string CharPipe = "pipe";
        public const string CharTilde = "tilde";
        public const string CharBacktick = "backtick";
        public const string CharCaret = "caret";

        // ===========================================
        // MANA SYMBOLS (for rules text parsing)
        // ===========================================
        // Tap/Untap
        public const string ManaTap = "Tap";
        public const string ManaUntap = "Untap";

        // Colors
        public const string ManaWhite = "White";
        public const string ManaBlue = "Blue";
        public const string ManaBlack = "Black";
        public const string ManaRed = "Red";
        public const string ManaGreen = "Green";
        public const string ManaColorless = "Colorless";

        // Special
        public const string ManaX = "X";
        public const string ManaSnow = "Snow";
        public const string ManaEnergy = "Energy";

        // Phyrexian format
        public static string ManaPhyrexian(string color) => $"Phyrexian {color}";

        // Hybrid mana format (e.g., "White or Blue")
        public static string ManaHybrid(string color1, string color2) => $"{color1} or {color2}";

        // ===========================================
        // BROWSER (Scry, Surveil, Mulligan, etc.)
        // ===========================================
        public const string NoCards = "No cards";
        public const string NoButtonSelected = "No button selected";
        public const string NoButtonsAvailable = "No buttons available";
        public const string CouldNotTogglePosition = "Could not toggle position";
        public const string Selected = "selected";
        public const string Confirmed = "Confirmed";
        public const string Cancelled = "Cancelled";
        public const string NoConfirmButton = "No confirm button found";
        public const string KeepOnTop = "keep";
        public const string PutOnBottom = "selected";
        public static string CouldNotClick(string label) => $"Could not click {label}";
        public static string BrowserCards(int count, string browserName) =>
            $"{browserName}. {count} {(count == 1 ? "card" : "cards")}. Tab to navigate, Enter to select";
        public static string BrowserOptions(string browserName) =>
            $"{browserName}. Tab to navigate options";
    }
}
