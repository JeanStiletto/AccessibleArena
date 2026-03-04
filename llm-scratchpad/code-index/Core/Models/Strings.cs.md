# Strings.cs Code Index

## File Overview
Centralized storage for all user-facing announcement strings. All strings are resolved through LocaleManager for localization.

## Static Class: Strings (line 9)

### Private Properties
- private static LocaleManager L => LocaleManager.Instance (line 12)
  // Shorthand for locale manager

- private static bool ShowHints => AccessibleArenaMod.Instance?.Settings?.TutorialMessages ?? true (line 15)
  // Whether to show tutorial hints

- private static bool ShowVerbose => AccessibleArenaMod.Instance?.Settings?.VerboseAnnouncements ?? true (line 16)
  // Whether to show verbose announcements

### Public Methods

#### Utility Methods
- public static string WithHint(string core, string hintKey) (line 21)
  // Appends a tutorial hint to a core message if TutorialMessages is enabled

- public static string WithDetail(string core, string detail) (line 27)
  // Appends verbose detail to a core message if VerboseAnnouncements is enabled

#### General/System (lines 33-49)
- public static string ModLoaded(string version) (line 33)
- public static string Back (line 34)
- public static string NoSelection (line 35)
- public static string NoAlternateAction (line 36)
- public static string NoNextItem (line 37)
- public static string NoPreviousItem (line 38)
- public static string ItemDisabled (line 39)

#### Activation (lines 44-48)
- public static string Activating(string name) (line 44)
- public static string CannotActivate(string name) (line 45)
- public static string CouldNotPlay(string name) (line 46)
- public static string NoAbilityAvailable(string name) (line 47)
- public static string NoCardSelected (line 48)

#### Menu Navigation (lines 53-70)
- Various navigation announcement methods

#### Deck Builder Info (lines 75-77)
- Deck info related strings

#### Login/Account (lines 82-95)
- Login field and action strings

#### Battlefield Navigation (lines 100-115)
- Battlefield navigation and land summary strings

#### Zone Navigation (lines 120-125)
- Zone-related strings with count formatting

#### Targeting (lines 130-135)
- Target selection strings

#### Zone Names (lines 140-171)
- public static string GetZoneName(Services.ZoneType zone) (line 153)
  // Returns localized zone name for given ZoneType

#### Battlefield Row Names (lines 176-195)
- public static string GetRowName(Services.BattlefieldRow row) (line 183)
  // Returns localized row name for given BattlefieldRow

#### Browser Zone Names (lines 200-211)
- Browser-specific zone strings

#### Browser Type Names (lines 216-244)
- public static string GetFriendlyBrowserName(string typeName) (line 216)
  // Returns human-readable browser name based on type

#### Damage Assignment Browser (lines 249-255)
- Damage assignment strings

#### Combat States (lines 260-274)
- Combat-related announcements

#### Card Relationship Patterns (lines 278-285)
- Enchantment, targeting, attachment strings

#### Duel Announcements (lines 290-445)
- Extensive duel event announcements including:
  - Turn/phase changes
  - Card draws
  - Life changes
  - Damage
  - Counters
  - Combat events
  - Zone transfers

#### Combat (lines 450-451)
- Combat button activation note

#### Card Actions (lines 454-463)
- Spell casting and ability strings

#### Discard (lines 468-479)
- Discard selection strings

#### Card Info (lines 484-514)
- Card detail navigation strings

#### Position/Counts (lines 519-533)
- Card position and count strings

#### Player Info Zone (lines 538-560)
- Player properties and emotes

#### Input Field Navigation (lines 565-665)
- Input field editing and character name methods
- public static string GetCharacterName(char c) (line 619)
  // Returns speakable name for a character (handles spaces, punctuation, etc.)

#### Currency Labels (lines 670-672)
- Gold, gems, wildcards

#### Mana Symbols (lines 677-691)
- Mana color and symbol strings

#### Mana Color Picker (lines 697-702)
- Any-color mana source popup strings

#### Settings Menu (lines 707-717)
- Settings navigation strings

#### Help Menu (lines 722-808)
- Help categories and shortcut descriptions

#### Browser (lines 814-831)
- Scry/Surveil/Mulligan browser strings

#### Mastery Screen (lines 836-860)
- Mastery progression strings

#### Prize Wall (lines 865-871)
- Prize wall navigation

#### Inline String Migrations (lines 876-943)
- Various UI element strings

#### Screen Titles (lines 948-1024)
- Screen name strings for all navigators

#### Element Groups (lines 1029-1084)
- public static string GroupName(Services.ElementGrouping.ElementGroup group) (line 1029)
  // Returns localized group name for given ElementGroup

#### Event/Packet Accessibility (lines 1089-1097)
- Event tile strings

#### Full Control & Phase Stops (lines 1102-1123)
- Full control toggle and phase stop strings

#### UI Role Labels (lines 1128-1156)
- Screen reader role announcements (button, checkbox, slider, etc.)
- Formatted role methods for compound states

#### Codex (lines 1161-1169)
- Learn to Play / Codex strings

#### Friend Actions (lines 1174-1181)
- Friend management action strings
