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
        public const string CouldNotActivateAttackButton = "Could not activate attack button";
        public const string CouldNotActivateNoAttackButton = "Could not activate no attack button";

        // ===========================================
        // CARD ACTIONS
        // ===========================================
        public const string NoPlayableCards = "No playable cards";
        public const string SpellCast = "Spell cast";

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

        // ===========================================
        // POSITION / COUNTS
        // ===========================================
        public static string CardPosition(string cardName, string state, int position, int total) =>
            string.IsNullOrEmpty(state)
                ? $"{cardName}, {position} of {total}"
                : $"{cardName}{state}, {position} of {total}";
    }
}
