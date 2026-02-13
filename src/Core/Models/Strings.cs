namespace AccessibleArena.Core.Models
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
        public const string NavigateWithArrows = "Arrow keys to navigate";
        public const string BeginningOfList = "Beginning of list";
        public const string EndOfList = "End of list";
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
        public const string ExitingDeckBuilder = "Exiting deck builder";

        // ===========================================
        // DECK BUILDER INFO
        // ===========================================
        public const string DeckInfoCardCount = "Card Count";
        public const string DeckInfoManaCurve = "Mana Curve";
        public const string DeckInfoTypeBreakdown = "Types";

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
        public const string SpellCastPrefix = "Cast";
        public const string SpellUnknown = "unknown spell";
        public const string ResolveStackFirst = "Resolve stack first. Press Space to resolve or Tab to select targets.";

        // Ability announcements (for triggered/activated abilities on stack)
        public const string AbilityTriggered = "triggered";
        public const string AbilityActivated = "activated";
        public const string AbilityUnknown = "Ability";

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
        public const string CardInfoQuantity = "Quantity";
        public const string CardInfoCollection = "Collection";
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

            // Common punctuation
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
        // HELP MENU
        // ===========================================
        public const string HelpMenuTitle = "Help Menu";
        public const string HelpMenuInstructions = "Arrow Up and Down to navigate, Backspace or F1 to close";
        public static string HelpItemPosition(int index, int total, string text) => $"{index} of {total}: {text}";
        public const string HelpMenuClosed = "Help closed";

        // Help categories
        public const string HelpCategoryGlobal = "Global shortcuts";
        public const string HelpCategoryMenuNavigation = "Menu navigation";
        public const string HelpCategoryDuelZones = "Zones in duel";
        public const string HelpCategoryDuelInfo = "Duel information";
        public const string HelpCategoryCardNavigation = "Card navigation in zone";
        public const string HelpCategoryCardDetails = "Card details";
        public const string HelpCategoryCombat = "Combat";
        public const string HelpCategoryBrowser = "Browser (Scry, Surveil, Mulligan)";

        // Global shortcuts
        public const string HelpF1Help = "F1: Help menu";
        public const string HelpF3Context = "F3: Current screen";
        public const string HelpCtrlRRepeat = "Control plus R: Repeat last announcement";
        public const string HelpBackspace = "Backspace: Back, dismiss, or cancel";

        // Menu navigation
        public const string HelpArrowUpDown = "Arrow Up or Down: Navigate menu items";
        public const string HelpTabNavigation = "Tab or Shift plus Tab: Navigate menu items, or switch groups in collection";
        public const string HelpArrowLeftRight = "Arrow Left or Right: Carousel and stepper controls";
        public const string HelpHomeEnd = "Home or End: Jump to first or last item";
        public const string HelpPageUpDown = "Page Up or Page Down: Previous or next page in collection";
        public const string HelpNumberKeysFilters = "Number keys 1 to 0: Activate filters 1 to 10 in collection";
        public const string HelpEnterSpace = "Enter or Space: Activate";

        // Input fields (text entry)
        public const string HelpCategoryInputFields = "Input fields";
        public const string HelpEnterEditField = "Enter: Start editing text field";
        public const string HelpEscapeExitField = "Escape: Stop editing, stay on field";
        public const string HelpTabNextField = "Tab: Stop editing and move to next element";
        public const string HelpShiftTabPrevField = "Shift plus Tab: Stop editing and move to previous element";
        public const string HelpArrowsInField = "Arrows in field: Left or Right reads character, Up or Down reads content";

        // Zones (yours and opponent)
        public const string HelpCHand = "C: Your hand, Shift plus C: Opponent hand count";
        public const string HelpBBattlefield = "B: Your creatures, Shift plus B: Opponent creatures";
        public const string HelpALands = "A: Your lands, Shift plus A: Opponent lands";
        public const string HelpRNonCreatures = "R: Your non-creatures, Shift plus R: Opponent non-creatures";
        public const string HelpGGraveyard = "G: Your graveyard, Shift plus G: Opponent graveyard";
        public const string HelpXExile = "X: Your exile, Shift plus X: Opponent exile";
        public const string HelpSStack = "S: Stack";
        public const string HelpDLibrary = "D: Your library count, Shift plus D: Opponent library count";

        // Duel info
        public const string HelpLLifeTotals = "L: Life totals";
        public const string HelpTTurnPhase = "T: Turn and phase";
        public const string HelpVPlayerInfo = "V: Player info zone";

        // Card navigation
        public const string HelpLeftRightCards = "Left or Right arrow: Previous or next card";
        public const string HelpHomeEndCards = "Home or End: First or last card";
        public const string HelpEnterPlay = "Enter: Play or activate card";
        public const string HelpTabTargets = "Tab: Cycle through targets or playable cards";

        // Card details
        public const string HelpUpDownDetails = "Up or Down arrow: Navigate card details";

        // Combat
        public const string HelpSpaceCombat = "Space: Confirm attackers or blockers";
        public const string HelpBackspaceCombat = "Backspace: No attacks or cancel blocks";

        // Browser
        public const string HelpTabBrowser = "Tab: Navigate all cards";
        public const string HelpCDZones = "C or D: Jump to keep or bottom zone";
        public const string HelpEnterToggle = "Enter: Toggle card between zones";
        public const string HelpSpaceConfirm = "Space: Confirm selection";

        // Debug keys
        public const string HelpCategoryDebug = "Debug keys (developers)";
        public const string HelpF4Refresh = "F4: Refresh current navigator";
        public const string HelpF11CardDump = "F11: Dump card details to log (pack opening)";
        public const string HelpF12UIDump = "F12: Dump UI hierarchy to log";

        // ===========================================
        // BROWSER (Scry, Surveil, Mulligan, etc.)
        // ===========================================
        public const string NoCards = "No cards";
        public const string NoButtonSelected = "No button selected";
        public const string NoButtonsAvailable = "No buttons available";
        public const string CouldNotTogglePosition = "Could not toggle position";
        public const string Selected = "selected";
        public const string Deselected = "deselected";
        public const string InHand = "in hand";
        public const string OnStack = "on stack";
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

        // ===========================================
        // MASTERY SCREEN
        // ===========================================
        public static string MasteryActivation(string trackName, int level, int total, string xp) =>
            $"{trackName}. Level {level} of {total}, {xp}. Arrow keys to navigate levels.";
        public static string MasteryLevel(int level, string reward, string status) =>
            string.IsNullOrEmpty(status)
                ? $"Level {level}: {reward}"
                : $"Level {level}: {reward}. {status}";
        public static string MasteryTier(string tierName, string reward, int quantity) =>
            quantity > 1 ? $"{tierName}: {quantity}x {reward}" : $"{tierName}: {reward}";
        public static string MasteryPage(int current, int total) =>
            $"Page {current} of {total}";
        public static string MasteryLevelDetail(int level, string tiers, string status) =>
            string.IsNullOrEmpty(status)
                ? $"Level {level}. {tiers}"
                : $"Level {level}. {tiers}. {status}";
        public const string MasteryCompleted = "completed";
        public const string MasteryCurrentLevel = "current level";
        public const string MasteryPremiumLocked = "premium locked";
        public const string MasteryFree = "Free";
        public const string MasteryPremium = "Premium";
        public const string MasteryRenewal = "Renewal";
        public const string MasteryNoReward = "no reward";
        public const string MasteryStatus = "Status";
        public static string MasteryStatusInfo(int level, int total, string xp) =>
            string.IsNullOrEmpty(xp)
                ? $"Level {level} of {total}"
                : $"Level {level} of {total}, {xp}";
    }
}
