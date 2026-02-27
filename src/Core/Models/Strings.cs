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
        public static string ModLoaded(string version) => L.Format("ModLoaded_Format", version);
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

        // Land summary (M key)
        public static string LandSummaryEmpty(string rowName) => L.Format("LandSummary_Empty_Format", rowName);
        public static string LandSummaryTotal(int count) =>
            count == 1 ? L.Get("LandSummary_Total_One") : L.Format("LandSummary_Total_Format", count);
        public static string LandSummaryAllTapped(string totalPart) => L.Format("LandSummary_AllTapped_Format", totalPart);
        public static string LandSummaryAllUntapped(string totalPart, string untappedList) => L.Format("LandSummary_AllUntapped_Format", totalPart, untappedList);
        public static string LandSummaryMixed(string totalPart, string untappedList) => L.Format("LandSummary_Mixed_Format", totalPart, untappedList);

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
        // ZONE NAMES
        // ===========================================
        public static string Zone_Hand => L.Get("Zone_Hand");
        public static string Zone_Battlefield => L.Get("Zone_Battlefield");
        public static string Zone_Graveyard => L.Get("Zone_Graveyard");
        public static string Zone_Exile => L.Get("Zone_Exile");
        public static string Zone_Stack => L.Get("Zone_Stack");
        public static string Zone_Library => L.Get("Zone_Library");
        public static string Zone_Command => L.Get("Zone_Command");
        public static string Zone_OpponentHand => L.Get("Zone_OpponentHand");
        public static string Zone_OpponentGraveyard => L.Get("Zone_OpponentGraveyard");
        public static string Zone_OpponentLibrary => L.Get("Zone_OpponentLibrary");
        public static string Zone_OpponentExile => L.Get("Zone_OpponentExile");
        public static string Zone_OpponentCommand => L.Get("Zone_OpponentCommand");

        public static string GetZoneName(Services.ZoneType zone)
        {
            switch (zone)
            {
                case Services.ZoneType.Hand: return Zone_Hand;
                case Services.ZoneType.Battlefield: return Zone_Battlefield;
                case Services.ZoneType.Graveyard: return Zone_Graveyard;
                case Services.ZoneType.Exile: return Zone_Exile;
                case Services.ZoneType.Stack: return Zone_Stack;
                case Services.ZoneType.Library: return Zone_Library;
                case Services.ZoneType.Command: return Zone_Command;
                case Services.ZoneType.OpponentHand: return Zone_OpponentHand;
                case Services.ZoneType.OpponentGraveyard: return Zone_OpponentGraveyard;
                case Services.ZoneType.OpponentLibrary: return Zone_OpponentLibrary;
                case Services.ZoneType.OpponentExile: return Zone_OpponentExile;
                case Services.ZoneType.OpponentCommand: return Zone_OpponentCommand;
                default: return zone.ToString();
            }
        }

        // ===========================================
        // BATTLEFIELD ROW NAMES
        // ===========================================
        public static string Row_PlayerCreatures => L.Get("Row_PlayerCreatures");
        public static string Row_PlayerNonCreatures => L.Get("Row_PlayerNonCreatures");
        public static string Row_PlayerLands => L.Get("Row_PlayerLands");
        public static string Row_EnemyCreatures => L.Get("Row_EnemyCreatures");
        public static string Row_EnemyNonCreatures => L.Get("Row_EnemyNonCreatures");
        public static string Row_EnemyLands => L.Get("Row_EnemyLands");

        public static string GetRowName(Services.BattlefieldRow row)
        {
            return row switch
            {
                Services.BattlefieldRow.PlayerCreatures => Row_PlayerCreatures,
                Services.BattlefieldRow.PlayerNonCreatures => Row_PlayerNonCreatures,
                Services.BattlefieldRow.PlayerLands => Row_PlayerLands,
                Services.BattlefieldRow.EnemyCreatures => Row_EnemyCreatures,
                Services.BattlefieldRow.EnemyNonCreatures => Row_EnemyNonCreatures,
                Services.BattlefieldRow.EnemyLands => Row_EnemyLands,
                _ => row.ToString()
            };
        }

        // ===========================================
        // BROWSER ZONE NAMES
        // ===========================================
        public static string BrowserZone_KeepOnTop => L.Get("BrowserZone_KeepOnTop");
        public static string BrowserZone_PutOnBottom => L.Get("BrowserZone_PutOnBottom");
        public static string BrowserZone_KeepPile => L.Get("BrowserZone_KeepPile");
        public static string BrowserZone_BottomPile => L.Get("BrowserZone_BottomPile");
        public static string BrowserZone_KeepShort => L.Get("BrowserZone_KeepShort");
        public static string BrowserZone_BottomShort => L.Get("BrowserZone_BottomShort");
        public static string BrowserZoneEmpty(string zoneName) => L.Format("BrowserZone_Empty_Format", zoneName);
        public static string BrowserZoneEntry(string zoneName, int count, string cardName) =>
            L.Format("BrowserZone_Entry_Format", zoneName, count, cardName, count);
        public static string BrowserZoneCard(string cardName, string zoneName, int index, int total) =>
            L.Format("BrowserZone_Card_Format", cardName, zoneName, index, total);
        public static string BrowserZone_NoCardSelected => L.Get("BrowserZone_NoCardSelected");

        // ===========================================
        // BROWSER TYPE NAMES
        // ===========================================
        public static string GetFriendlyBrowserName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return L.Get("Browser_Default");

            if (typeName.Contains("Scryish") || typeName.Contains("Scry")) return L.Get("Browser_Scry");
            if (typeName.Contains("Surveil")) return L.Get("Browser_Surveil");
            if (typeName.Contains("ReadAhead")) return L.Get("Browser_ReadAhead");
            if (typeName.Contains("LibrarySideboard")) return L.Get("Browser_SearchLibrary");
            if (typeName.Contains("London")) return L.Get("Browser_Mulligan");
            if (typeName.Contains("Mulligan")) return L.Get("Browser_Mulligan");
            if (typeName.Contains("OpeningHand")) return L.Get("Browser_OpeningHand");
            if (typeName.Contains("OrderCards")) return L.Get("Browser_OrderCards");
            if (typeName.Contains("SplitCards")) return L.Get("Browser_SplitCards");
            if (typeName.Contains("AssignDamage")) return L.Get("Browser_AssignDamage");
            if (typeName.Contains("Attachment")) return L.Get("Browser_ViewAttachments");
            if (typeName.Contains("LargeScrollList")) return L.Get("Browser_ChooseFromList");
            if (typeName.Contains("SelectCards")) return L.Get("Browser_SelectCards");
            if (typeName.Contains("SelectGroup")) return L.Get("Browser_SelectGroup");
            if (typeName.Contains("SelectMana")) return L.Get("Browser_ChooseManaType");
            if (typeName.Contains("Keyword")) return L.Get("Browser_ChooseKeyword");
            if (typeName.Contains("Dungeon")) return L.Get("Browser_ChooseDungeonRoom");
            if (typeName.Contains("Mutate")) return L.Get("Browser_MutateChoice");
            if (typeName.Contains("YesNo")) return L.Get("Browser_ChooseYesOrNo");
            if (typeName.Contains("Optional")) return L.Get("Browser_OptionalAction");
            if (typeName.Contains("Informational")) return L.Get("Browser_Information");
            if (typeName.Contains("Workflow")) return L.Get("Browser_ChooseAction");

            return L.Get("Browser_Default");
        }

        // ===========================================
        // DAMAGE ASSIGNMENT BROWSER
        // ===========================================
        public static string DamageAssignEntry(string attackerName, int damage, int blockerCount) =>
            blockerCount == 1
                ? L.Format("DamageAssign_Entry_One_Format", attackerName, damage)
                : L.Format("DamageAssign_Entry_Format", attackerName, damage, blockerCount);
        public static string DamageAssigned(int assigned, int total) =>
            L.Format("DamageAssign_Assigned_Format", assigned, total);
        public static string DamageAssignLethal => L.Get("DamageAssign_Lethal");

        // ===========================================
        // COMBAT STATES
        // ===========================================
        public static string Combat_Attacking => L.Get("Combat_Attacking");
        public static string Combat_CanAttack => L.Get("Combat_CanAttack");
        public static string Combat_Blocking(string target) => L.Format("Combat_Blocking_Format", target);
        public static string Combat_BlockingSimple => L.Get("Combat_Blocking");
        public static string Combat_BlockedBy(string blockers) => L.Format("Combat_BlockedBy_Format", blockers);
        public static string Combat_SelectedToBlock => L.Get("Combat_SelectedToBlock");
        public static string Combat_CanBlock => L.Get("Combat_CanBlock");
        public static string Combat_Tapped => L.Get("Combat_Tapped");
        public static string Combat_PTBlocking(int power, int toughness) => L.Format("Combat_PTBlocking_Format", power, toughness);
        public static string Combat_Assigned => L.Get("Combat_Assigned");

        // Target/selection actions
        public static string Target_Targeted(string name) => L.Format("Target_Targeted_Format", name);
        public static string Target_Selected(string name) => L.Format("Target_Selected_Format", name);

        // ===========================================
        // CARD RELATIONSHIP PATTERNS
        // ===========================================
        public static string Card_EnchantedBy(string names) => L.Format("Card_EnchantedBy_One_Format", names);
        public static string Card_AttachedTo(string name) => L.Format("Card_AttachedTo_Format", name);
        public static string Card_Targeting(string name) => L.Format("Card_Targeting_One_Format", name);
        public static string Card_TargetingTwo(string name1, string name2) => L.Format("Card_Targeting_Two_Format", name1, name2);
        public static string Card_TargetingMany(string names) => L.Format("Card_Targeting_Many_Format", names);
        public static string Card_TargetedBy(string name) => L.Format("Card_TargetedBy_One_Format", name);
        public static string Card_TargetedByTwo(string name1, string name2) => L.Format("Card_TargetedBy_Two_Format", name1, name2);
        public static string Card_TargetedByMany(string names) => L.Format("Card_TargetedBy_Many_Format", names);

        // ===========================================
        // DUEL ANNOUNCEMENTS
        // ===========================================
        public static string Duel_Started(int handCards) => L.Format("Duel_Started_Format", handCards);
        public static string Duel_YourTurn(int turnNum) => L.Format("Duel_YourTurn_Format", turnNum);
        public static string Duel_OpponentTurn => L.Get("Duel_OpponentTurn");
        public static string Duel_TurnChanged => L.Get("Duel_TurnChanged");

        public static string Duel_Drew(int count) =>
            count == 1 ? L.Get("Duel_Drew_One") : L.Format("Duel_Drew_Format", count);
        public static string Duel_OpponentDrew(int count) =>
            count == 1 ? L.Get("Duel_OpponentDrew_One") : L.Format("Duel_OpponentDrew_Format", count);
        public static string Duel_OpponentPlayedCard => L.Get("Duel_OpponentPlayedCard");
        public static string Duel_OpponentEnteredBattlefield(int count) =>
            count == 1 ? L.Get("Duel_OpponentEnteredBattlefield_One") : L.Format("Duel_OpponentEnteredBattlefield_Format", count);
        public static string Duel_LeftBattlefield(int count) =>
            count == 1 ? L.Get("Duel_LeftBattlefield_One") : L.Format("Duel_LeftBattlefield_Format", count);
        public static string Duel_CardToYourGraveyard => L.Get("Duel_CardToYourGraveyard");
        public static string Duel_CardToOpponentGraveyard => L.Get("Duel_CardToOpponentGraveyard");
        public static string Duel_SpellResolved => L.Get("Duel_SpellResolved");

        // Phase announcement strings
        public static string Duel_Phase_FirstMain => L.Get("Duel_Phase_FirstMain");
        public static string Duel_Phase_SecondMain => L.Get("Duel_Phase_SecondMain");
        public static string Duel_Phase_DeclareAttackers => L.Get("Duel_Phase_DeclareAttackers");
        public static string Duel_Phase_DeclareBlockers => L.Get("Duel_Phase_DeclareBlockers");
        public static string Duel_Phase_CombatDamage => L.Get("Duel_Phase_CombatDamage");
        public static string Duel_Phase_EndOfCombat => L.Get("Duel_Phase_EndOfCombat");
        public static string Duel_Phase_Combat => L.Get("Duel_Phase_Combat");
        public static string Duel_Phase_Upkeep => L.Get("Duel_Phase_Upkeep");
        public static string Duel_Phase_Draw => L.Get("Duel_Phase_Draw");
        public static string Duel_Phase_EndStep => L.Get("Duel_Phase_EndStep");

        // Phase descriptions (lowercase for "Your first main phase, turn 5")
        public static string Duel_PhaseDesc_Turn => L.Get("Duel_PhaseDesc_Turn");
        public static string GetPhaseDescription(string phase, string step)
        {
            if (string.IsNullOrEmpty(phase)) return null;

            switch (phase)
            {
                case "Main1": return L.Get("Duel_PhaseDesc_FirstMain");
                case "Main2": return L.Get("Duel_PhaseDesc_SecondMain");
                case "Combat":
                    switch (step)
                    {
                        case "DeclareAttack": return L.Get("Duel_PhaseDesc_DeclareAttackers");
                        case "DeclareBlock": return L.Get("Duel_PhaseDesc_DeclareBlockers");
                        case "CombatDamage": return L.Get("Duel_PhaseDesc_CombatDamage");
                        case "EndCombat": return L.Get("Duel_PhaseDesc_EndOfCombat");
                        default: return L.Get("Duel_PhaseDesc_Combat");
                    }
                case "Beginning":
                    if (step == "Upkeep") return L.Get("Duel_PhaseDesc_Upkeep");
                    if (step == "Draw") return L.Get("Duel_PhaseDesc_Draw");
                    return L.Get("Duel_PhaseDesc_Beginning");
                case "Ending":
                    if (step == "End") return L.Get("Duel_PhaseDesc_EndStep");
                    return L.Get("Duel_PhaseDesc_Ending");
                default: return phase.ToLower();
            }
        }

        public static string Duel_TurnPhase(string owner, string phaseDesc, int turnCount) =>
            turnCount > 0 ? L.Format("Duel_TurnPhase_Format", owner, phaseDesc, turnCount)
                          : L.Format("Duel_TurnPhaseNoCount_Format", owner, phaseDesc);
        public static string Duel_Your => L.Get("Duel_Your");
        public static string Duel_Opponents => L.Get("Duel_Opponents");
        public static string Duel_You => L.Get("Duel_You");
        public static string Duel_Opponent => L.Get("Duel_Opponent");

        // Life changes
        public static string Duel_LifeGained(string who, int amount) => L.Format("Duel_LifeGained_Format", who, amount);
        public static string Duel_LifeLost(string who, int amount) => L.Format("Duel_LifeLost_Format", who, amount);

        // Damage
        public static string Duel_DamageDeals(string source, int amount, string target) => L.Format("Duel_DamageDeals_Format", source, amount, target);
        public static string Duel_DamageAmount(int amount, string target) => L.Format("Duel_DamageAmount_Format", amount, target);
        public static string Duel_DamageToYou => L.Get("Duel_DamageToYou");
        public static string Duel_DamageToOpponent => L.Get("Duel_DamageToOpponent");
        public static string Duel_DamageTarget => L.Get("Duel_DamageTarget");
        public static string Duel_CombatDamageSource => L.Get("Duel_CombatDamageSource");

        // Reveals and counters
        public static string Duel_Revealed(string name) => L.Format("Duel_Revealed_Format", name);
        public static string Duel_CounterChanged(string target, int absChange, string counterType, bool gained) =>
            gained
                ? (absChange == 1 ? L.Format("Duel_CounterGained_Format", target, absChange, counterType)
                                  : L.Format("Duel_CounterGainedPlural_Format", target, absChange, counterType))
                : (absChange == 1 ? L.Format("Duel_CounterLost_Format", target, absChange, counterType)
                                  : L.Format("Duel_CounterLostPlural_Format", target, absChange, counterType));
        public static string Duel_CounterCreature => L.Get("Duel_CounterCreature");

        // Game end
        public static string Duel_Victory => L.Get("Duel_Victory");
        public static string Duel_Defeat => L.Get("Duel_Defeat");
        public static string Duel_GameEnded => L.Get("Duel_GameEnded");

        // Combat events
        public static string Duel_CombatBegins => L.Get("Duel_CombatBegins");
        public static string Duel_AttackerDeclared => L.Get("Duel_AttackerDeclared");
        public static string Duel_OpponentAttackerDeclared => L.Get("Duel_OpponentAttackerDeclared");
        public static string Duel_Attacking(string name) => L.Format("Duel_Attacking_Format", name);
        public static string Duel_AttackingPT(string name, string pt) => L.Format("Duel_AttackingPT_Format", name, pt);
        public static string Duel_AttackerRemoved => L.Get("Duel_AttackerRemoved");
        public static string Duel_Attackers(int count) =>
            count == 1 ? L.Get("Duel_Attackers_One") : L.Format("Duel_Attackers_Format", count);

        // Zone transfers - battlefield entry
        public static string Duel_TokenCreated(string name) => L.Format("Duel_TokenCreated_Format", name);
        public static string Duel_Played(string owner, string name) => L.Format("Duel_Played_Format", owner, name);
        public static string Duel_Enchanted(string name, string target) => L.Format("Duel_Enchanted_Format", name, target);
        public static string Duel_EntersBattlefield(string name) => L.Format("Duel_EntersBattlefield_Format", name);
        public static string Duel_ReturnedFromGraveyard(string name) => L.Format("Duel_ReturnedFromGraveyard_Format", name);
        public static string Duel_ReturnedFromGraveyardEnchanting(string name, string target) => L.Format("Duel_ReturnedFromGraveyardEnchanting_Format", name, target);
        public static string Duel_ReturnedFromExile(string name) => L.Format("Duel_ReturnedFromExile_Format", name);
        public static string Duel_ReturnedFromExileEnchanting(string name, string target) => L.Format("Duel_ReturnedFromExileEnchanting_Format", name, target);
        public static string Duel_EntersBattlefieldFromLibrary(string name) => L.Format("Duel_EntersBattlefieldFromLibrary_Format", name);
        public static string Duel_EntersBattlefieldFromLibraryEnchanting(string name, string target) => L.Format("Duel_EntersBattlefieldFromLibraryEnchanting_Format", name, target);

        // Zone transfers - graveyard
        public static string Duel_Died(string owner, string name) => L.Format("Duel_Died_Format", owner, name);
        public static string Duel_Destroyed(string owner, string name) => L.Format("Duel_Destroyed_Format", owner, name);
        public static string Duel_Sacrificed(string owner, string name) => L.Format("Duel_Sacrificed_Format", owner, name);
        public static string Duel_Countered(string owner, string name) => L.Format("Duel_Countered_Format", owner, name);
        public static string Duel_Discarded(string owner, string name) => L.Format("Duel_Discarded_Format", owner, name);
        public static string Duel_Milled(string owner, string name) => L.Format("Duel_Milled_Format", owner, name);
        public static string Duel_WentToGraveyard(string owner, string name) => L.Format("Duel_WentToGraveyard_Format", owner, name);

        // Zone transfers - exile
        public static string Duel_Exiled(string owner, string name) => L.Format("Duel_Exiled_Format", owner, name);
        public static string Duel_ExiledFromGraveyard(string owner, string name) => L.Format("Duel_ExiledFromGraveyard_Format", owner, name);
        public static string Duel_ExiledFromHand(string owner, string name) => L.Format("Duel_ExiledFromHand_Format", owner, name);
        public static string Duel_ExiledFromLibrary(string owner, string name) => L.Format("Duel_ExiledFromLibrary_Format", owner, name);
        public static string Duel_CounteredAndExiled(string owner, string name) => L.Format("Duel_CounteredAndExiled_Format", owner, name);

        // Zone transfers - hand (bounce)
        public static string Duel_ReturnedToHand(string owner, string name) => L.Format("Duel_ReturnedToHand_Format", owner, name);
        public static string Duel_ReturnedToHandFromGraveyard(string owner, string name) => L.Format("Duel_ReturnedToHandFromGraveyard_Format", owner, name);
        public static string Duel_ReturnedToHandFromExile(string owner, string name) => L.Format("Duel_ReturnedToHandFromExile_Format", owner, name);

        // Library effects
        public static string Duel_ScryHint => L.Get("Duel_ScryHint");
        public static string Duel_SurveilHint => L.Get("Duel_SurveilHint");
        public static string Duel_EffectHint(string name) => L.Format("Duel_EffectHint_Format", name);
        public static string Duel_LookAtTopCard => L.Get("Duel_LookAtTopCard");

        // London mulligan
        public static string Duel_SelectForBottom(int count, int cardCount) =>
            count == 1 ? L.Format("Duel_SelectForBottom_One", cardCount) : L.Format("Duel_SelectForBottom_Format", count, cardCount);
        public static string Duel_SelectedForBottom(int selected, int required) => L.Format("Duel_SelectedForBottom_Format", selected, required);

        public static string Duel_OwnerPrefix_Opponent => L.Get("Duel_OwnerPrefix_Opponent");

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
        public static string SelectionProgress(int selected, int total) =>
            L.Format("SelectionProgress_Format", selected, total);
        public static string NeedHaveSelected(int required, int selected) =>
            L.Format("NeedHaveSelected_Format", required, selected);
        public static string SubmittingDiscard(int count) => L.Format("SubmittingDiscard_Format", count);
        public static string CouldNotSelect(string name) => L.Format("CouldNotSelect_Format", name);

        // ===========================================
        // CARD INFO
        // ===========================================
        public static string EndOfCard => L.Get("EndOfCard");
        public static string BeginningOfCard => L.Get("BeginningOfCard");

        // Extended card info (I key - navigable menu)
        public static string ExtendedInfoTitle => L.Get("ExtendedInfoTitle");
        public static string ExtendedInfoClosed => L.Get("ExtendedInfoClosed");
        public static string CardInfoKeywords => L.Get("CardInfoKeywords");
        public static string NoExtendedCardInfo => L.Get("NoExtendedCardInfo");
        public static string LinkedFaceOtherFace => L.Get("LinkedFace_OtherFace");
        public static string LinkedFaceOtherHalf => L.Get("LinkedFace_OtherHalf");
        public static string LinkedFaceAdventure => L.Get("LinkedFace_Adventure");
        public static string LinkedFaceOtherRoom => L.Get("LinkedFace_OtherRoom");
        public static string HelpIExtendedInfo => L.Get("HelpIExtendedInfo");

        // Card info block labels
        public static string CardInfoName => L.Get("CardInfoName");
        public static string CardInfoQuantity => L.Get("CardInfoQuantity");
        public static string CardInfoCollection => L.Get("CardInfoCollection");
        public static string CardInfoManaCost => L.Get("CardInfoManaCost");

        // Card info content (localized values inside blocks)
        public static string CardQuantityMissing(int qty) => L.Format("CardQuantityMissing_Format", qty);
        public static string CardOwned(int count) => L.Format("CardOwned_Format", count);
        public static string CardOwnedInDeck(int owned, int inDeck) => L.Format("CardOwnedInDeck_Format", owned, inDeck);
        public static string CardInfoPowerToughness => L.Get("CardInfoPowerToughness");
        public static string CardInfoType => L.Get("CardInfoType");
        public static string CardInfoRules => L.Get("CardInfoRules");
        public static string CardInfoFlavor => L.Get("CardInfoFlavor");
        public static string CardInfoRarity => L.Get("CardInfoRarity");
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
        public static string TextField => L.Get("TextField");
        public static string InputFieldHint => L.Get("InputFieldHint");
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
        public static string ManaGeneric => L.Get("ManaGeneric");
        public static string ManaPhyrexianBare => L.Get("ManaPhyrexian");
        public static string ManaPhyrexian(string color) => L.Format("ManaPhyrexian_Format", color);
        public static string ManaHybrid(string color1, string color2) => L.Format("ManaHybrid_Format", color1, color2);

        // ===========================================
        // MANA COLOR PICKER (any-color mana source popup)
        // ===========================================
        public static string ManaColorPickerFormat(string colorList) => L.Format("ManaColorPicker_Format", colorList);
        public static string ManaColorPickerOptionFormat(string number, string colorName) => L.Format("ManaColorPicker_Option_Format", number, colorName);
        public static string ManaColorPickerSelectedFormat(string colorName, string current, string total) => L.Format("ManaColorPicker_Selected_Format", colorName, current, total);
        public static string ManaColorPickerSelectionProgress(int current, int total) => L.Format("ManaColorPicker_SelectionProgress_Format", current, total);
        public static string ManaColorPickerDoneFormat(string colorName) => L.Format("ManaColorPicker_Done_Format", colorName);
        public static string ManaColorPickerCancelled => L.Get("ManaColorPicker_Cancelled");
        public static string ManaColorPickerInvalidKey => L.Get("ManaColorPicker_InvalidKey");

        // ===========================================
        // SETTINGS MENU
        // ===========================================
        public static string SettingsMenuTitle => L.Get("SettingsMenuTitle");
        public static string SettingsMenuInstructions => L.Get("SettingsMenuInstructions");
        public static string SettingsMenuClosed => L.Get("SettingsMenuClosed");
        public static string SettingLanguage => L.Get("SettingLanguage");
        public static string SettingTutorialMessages => L.Get("SettingTutorialMessages");
        public static string SettingVerboseAnnouncements => L.Get("SettingVerboseAnnouncements");
        public static string SettingBriefCastAnnouncements => L.Get("SettingBriefCastAnnouncements");
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
        public static string HelpMLandSummary => L.Get("HelpMLandSummary");

        // Card navigation
        public static string HelpLeftRightCards => L.Get("HelpLeftRightCards");
        public static string HelpHomeEndCards => L.Get("HelpHomeEndCards");
        public static string HelpEnterPlay => L.Get("HelpEnterPlay");
        public static string HelpTabTargets => L.Get("HelpTabTargets");

        // Card details
        public static string HelpUpDownDetails => L.Get("HelpUpDownDetails");

        // General duel commands
        public static string HelpCategoryDuelGeneral => L.Get("HelpCategoryDuelGeneral");
        public static string HelpSpaceAdvance => L.Get("HelpSpaceAdvance");
        public static string HelpBackspaceCancel => L.Get("HelpBackspaceCancel");
        public static string HelpEnterSelect => L.Get("HelpEnterSelect");
        public static string HelpYUndo => L.Get("HelpYUndo");
        public static string HelpQQFloatMana => L.Get("HelpQQFloatMana");

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
        public static string ActivatedBare => L.Get("Activated");
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
        // SCREEN TITLES
        // ===========================================
        public static string ScreenHome => L.Get("ScreenHome");
        public static string ScreenDecks => L.Get("ScreenDecks");
        public static string ScreenProfile => L.Get("ScreenProfile");
        public static string ScreenStore => L.Get("ScreenStore");
        public static string ScreenMastery => L.Get("ScreenMastery");
        public static string ScreenAchievements => L.Get("ScreenAchievements");
        public static string ScreenLearnToPlay => L.Get("ScreenLearnToPlay");
        public static string ScreenPackOpening => L.Get("ScreenPackOpening");
        public static string ScreenColorChallenge => L.Get("ScreenColorChallenge");
        public static string ScreenDeckBuilder => L.Get("ScreenDeckBuilder");
        public static string ScreenDeckBuilderReadOnly => L.Get("ScreenDeckBuilderReadOnly");
        public static string ReadOnlyDeckWarning => L.Get("ReadOnlyDeckWarning");
        public static string ScreenDeckSelection => L.Get("ScreenDeckSelection");
        public static string ScreenEvent => L.Get("ScreenEvent");
        public static string ScreenRewards => L.Get("ScreenRewards");
        public static string ScreenPacks => L.Get("ScreenPacks");
        public static string ScreenCardUnlocked => L.Get("ScreenCardUnlocked");
        public static string ScreenCardUnlockedCount(int count) =>
            count == 1 ? L.Get("ScreenCardUnlocked_One") : L.Format("ScreenCardUnlocked_Format", count);
        public static string ScreenDecksUnlocked => L.Get("ScreenDecksUnlocked");
        public static string ScreenDecksUnlockedCount(int count) =>
            count == 1 ? L.Get("ScreenDecksUnlocked_One") : L.Format("ScreenDecksUnlocked_Format", count);
        public static string DeckNumber(int n) => L.Format("Deck_Format", n);
        public static string ScreenPackContents => L.Get("ScreenPackContents");
        public static string ScreenPackContentsCount(int count) =>
            count == 1 ? L.Get("ScreenPackContents_One") : L.Format("ScreenPackContents_Format", count);
        public static string ScreenDraft => L.Get("ScreenDraft");
        public static string ScreenDraftPick => L.Get("ScreenDraftPick");
        public static string ScreenDraftPickCount(int count) =>
            count == 1 ? L.Get("ScreenDraftPick_One") : L.Format("ScreenDraftPick_Format", count);
        public static string ScreenDraftPopup => L.Get("ScreenDraftPopup");
        public static string UpDownForMore(int count) => L.Format("UpDownForMore_Format", count);
        public static string ScreenFriends => L.Get("ScreenFriends");
        public static string ScreenHomeWithEvents => L.Get("ScreenHomeWithEvents");
        public static string ScreenHomeWithColorChallenge => L.Get("ScreenHomeWithColorChallenge");
        public static string ScreenNavigationBar => L.Get("ScreenNavigationBar");
        public static string ScreenCollection => L.Get("ScreenCollection");
        public static string ScreenSettings => L.Get("ScreenSettings");
        public static string ScreenMenu => L.Get("ScreenMenu");
        public static string ScreenPlayModeSelection => L.Get("ScreenPlayModeSelection");
        public static string ScreenDirectChallenge => L.Get("ScreenDirectChallenge");
        public static string ScreenFriendChallenge => L.Get("ScreenFriendChallenge");
        public static string ChallengeYou => L.Get("ChallengeYou");
        public static string ChallengeOpponent => L.Get("ChallengeOpponent");
        public static string ChallengeNotInvited => L.Get("ChallengeNotInvited");
        public static string ChallengeInvited => L.Get("ChallengeInvited");
        public static string ChallengeSettingsLocked => L.Get("ChallengeSettingsLocked");
        public static string ChallengeLocked(string label) => L.Format("ChallengeLocked_Format", label);
        public static string ChallengeOpponentJoined(string name) => L.Format("ChallengeOpponentJoined_Format", name);
        public static string ChallengeOpponentLeft => L.Get("ChallengeOpponentLeft");
        public static string ChallengeMatchStarting => L.Get("ChallengeMatchStarting");
        public static string ChallengeCountdownCancelled => L.Get("ChallengeCountdownCancelled");
        public static string ChallengeKickOpponent => L.Get("ChallengeKickOpponent");
        public static string ChallengeBlockOpponent => L.Get("ChallengeBlockOpponent");
        public static string ChallengeAddFriend => L.Get("ChallengeAddFriend");
        public static string ScreenConfirmation => L.Get("ScreenConfirmation");
        public static string ScreenInviteFriend => L.Get("ScreenInviteFriend");
        public static string ScreenSocial => L.Get("ScreenSocial");
        public static string ScreenPlay => L.Get("ScreenPlay");
        public static string ScreenEvents => L.Get("ScreenEvents");
        public static string ScreenFindMatch => L.Get("ScreenFindMatch");
        public static string ScreenMatchEnded => L.Get("ScreenMatchEnded");
        public static string ScreenSearchingForMatch => L.Get("ScreenSearchingForMatch");
        public static string ScreenLoading => L.Get("ScreenLoading");
        public static string ScreenSettingsGameplay => L.Get("ScreenSettingsGameplay");
        public static string ScreenSettingsGraphics => L.Get("ScreenSettingsGraphics");
        public static string ScreenSettingsAudio => L.Get("ScreenSettingsAudio");
        public static string ScreenDownload => L.Get("ScreenDownload");
        public static string ScreenAdvancedFilters => L.Get("ScreenAdvancedFilters");
        public static string ScreenPrizeWall => L.Get("ScreenPrizeWall");
        public static string ScreenDuel => L.Get("ScreenDuel");
        public static string ScreenPreGame => L.Get("ScreenPreGame");
        public static string ScreenWhatsNew => L.Get("ScreenWhatsNew");
        public static string ScreenAnnouncement => L.Get("ScreenAnnouncement");
        public static string ScreenRewardPopup => L.Get("ScreenRewardPopup");
        public static string ScreenOverlay => L.Get("ScreenOverlay");
        public static string WaitingForServer => L.Get("WaitingForServer");

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
                case Services.ElementGrouping.ElementGroup.DeckBuilderSideboard: return L.Get("GroupSideboard");
                case Services.ElementGrouping.ElementGroup.DeckBuilderInfo: return L.Get("GroupDeckInfo");
                case Services.ElementGrouping.ElementGroup.EventInfo: return L.Get("GroupEventInfo");
                case Services.ElementGrouping.ElementGroup.MailboxList: return L.Get("GroupMailList");
                case Services.ElementGrouping.ElementGroup.MailboxContent: return L.Get("GroupMail");
                case Services.ElementGrouping.ElementGroup.RewardsPopup: return L.Get("GroupRewards");
                case Services.ElementGrouping.ElementGroup.FriendsPanelChallenge: return L.Get("GroupFriendsPanelChallenge");
                case Services.ElementGrouping.ElementGroup.FriendsPanelAddFriend: return L.Get("GroupFriendsPanelAddFriend");
                case Services.ElementGrouping.ElementGroup.FriendSectionFriends: return L.Get("GroupFriendSectionFriends");
                case Services.ElementGrouping.ElementGroup.FriendSectionIncoming: return L.Get("GroupFriendSectionIncoming");
                case Services.ElementGrouping.ElementGroup.FriendSectionOutgoing: return L.Get("GroupFriendSectionOutgoing");
                case Services.ElementGrouping.ElementGroup.FriendSectionBlocked: return L.Get("GroupFriendSectionBlocked");
                case Services.ElementGrouping.ElementGroup.FriendsPanelProfile: return L.Get("GroupFriendsPanelProfile");
                case Services.ElementGrouping.ElementGroup.ChallengeMain: return L.Get("GroupChallengeMain");
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
        public static string Bo3Toggle() => L.Get("Bo3Toggle");

        // ===========================================
        // EVENT / PACKET ACCESSIBILITY
        // ===========================================
        public static string ScreenPacketSelect => L.Get("ScreenPacketSelect");
        public static string EventTileRanked => L.Get("EventTileRanked");
        public static string EventTileBo3 => L.Get("EventTileBo3");
        public static string EventTileInProgress => L.Get("EventTileInProgress");
        public static string EventTileProgress(int wins, int maxWins) => L.Format("EventTileProgress_Format", wins, maxWins);
        public static string EventPageSummary(int wins, int maxWins) => L.Format("EventPageSummary_Format", wins, maxWins);
        public static string EventScreenTitle(string eventName) => L.Format("EventScreenTitle_Format", eventName);
        public static string PacketOf(int current, int total) => L.Format("PacketOf_Format", current, total);
        public static string EventInfoLabel => L.Get("EventInfoLabel");

        // ===========================================
        // FULL CONTROL & PHASE STOPS
        // ===========================================
        public static string FullControl_On => L.Get("FullControl_On");
        public static string FullControl_Off => L.Get("FullControl_Off");
        public static string FullControl_Locked => L.Get("FullControl_Locked");
        public static string FullControl_Unlocked => L.Get("FullControl_Unlocked");
        public static string PhaseStop_Set(string phase) => L.Format("PhaseStop_Set_Format", phase);
        public static string PhaseStop_Cleared(string phase) => L.Format("PhaseStop_Cleared_Format", phase);

        // Phase stop names (for announcements)
        public static string PhaseStop_Upkeep => L.Get("PhaseStop_Upkeep");
        public static string PhaseStop_Draw => L.Get("PhaseStop_Draw");
        public static string PhaseStop_FirstMain => L.Get("PhaseStop_FirstMain");
        public static string PhaseStop_BeginCombat => L.Get("PhaseStop_BeginCombat");
        public static string PhaseStop_DeclareAttackers => L.Get("PhaseStop_DeclareAttackers");
        public static string PhaseStop_DeclareBlockers => L.Get("PhaseStop_DeclareBlockers");
        public static string PhaseStop_CombatDamage => L.Get("PhaseStop_CombatDamage");
        public static string PhaseStop_EndCombat => L.Get("PhaseStop_EndCombat");
        public static string PhaseStop_SecondMain => L.Get("PhaseStop_SecondMain");
        public static string PhaseStop_EndStep => L.Get("PhaseStop_EndStep");

        // Help entries for full control and phase stops
        public static string HelpPFullControl => L.Get("HelpPFullControl");
        public static string HelpNumberPhaseStops => L.Get("HelpNumberPhaseStops");

        // ===========================================
        // FRIEND ACTIONS
        // ===========================================
        public static string FriendActionChat => L.Get("FriendActionChat");
        public static string FriendActionChallenge => L.Get("FriendActionChallenge");
        public static string FriendActionUnfriend => L.Get("FriendActionUnfriend");
        public static string FriendActionBlock => L.Get("FriendActionBlock");
        public static string FriendActionAccept => L.Get("FriendActionAccept");
        public static string FriendActionDecline => L.Get("FriendActionDecline");
        public static string FriendActionRevoke => L.Get("FriendActionRevoke");
        public static string FriendActionUnblock => L.Get("FriendActionUnblock");
    }
}
