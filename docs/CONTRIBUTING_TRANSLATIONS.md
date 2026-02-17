# Contributing Translations

How to translate the Accessible Arena mod into your language.

## Quick Start

1. Open `lang/en.json` (English reference) and the target `lang/{code}.json`
2. Translate the values (right side of the colon). Never change the keys (left side).
3. Keep all `{0}`, `{1}` placeholders in your translation - they get replaced with dynamic values at runtime
4. Submit a pull request

## File Format

Each locale file is flat JSON with key-value pairs:

```json
{
  "KeyName": "Translated text",
  "Greeting_Format": "Hello {0}, you have {1} items"
}
```

Rules:
- Keys (left side) must stay exactly as-is in English
- Values (right side) are what you translate
- Placeholders like `{0}`, `{1}` must appear in your translation (order can change to fit your grammar)
- Use `\"` for quotes inside strings, `\n` for line breaks

## Supported Languages

| Code  | Language             |
|-------|----------------------|
| en    | English (reference)  |
| de    | German               |
| fr    | French               |
| es    | Spanish              |
| it    | Italian              |
| pt-BR | Portuguese (Brazil)  |
| ja    | Japanese             |
| ko    | Korean               |
| ru    | Russian              |
| pl    | Polish               |
| zh-CN | Chinese (Simplified) |
| zh-TW | Chinese (Traditional)|

## Pluralization

Some strings come in sets with suffixes. The mod picks the right form based on the count.

**Standard languages** (en, de, fr, es, it, pt-BR):
- `_One` - used when count is exactly 1
- `_Format` - used for all other counts (0, 2, 3, ...)

**Slavic languages** (ru, pl):
- `_One` - count is 1
- `_Few` - count ends in 2-4 but not 12-14 (e.g., 2, 3, 4, 22, 23, 24)
- `_Format` - everything else (0, 5-20, 25-30, ...)

**East Asian languages** (ja, ko, zh-CN, zh-TW):
- Only `_Format` is used (no singular/plural distinction)
- You can include `_One` for completeness, but it won't be used

Example set:
```json
"ZoneWithCount_One": "{0}, 1 card",
"ZoneWithCount_Few": "{0}, {1} karty",
"ZoneWithCount_Format": "{0}, {1} cards"
```

If a `_Few` key is missing for Russian/Polish, the mod falls back to `_Format`.

## String Reference

Below is every key in `en.json` with context about where it appears and what the placeholders mean.

### General UI

| Key | English | Context |
|-----|---------|---------|
| `ModLoaded` | "MTGA Accessibility Mod loaded" | Announced once when the game starts |
| `Back` | "Back" | Announced when navigating back |
| `NoSelection` | "No selection" | When nothing is focused |
| `NoAlternateAction` | "No alternate action available" | When user tries an alternate action on an element that doesn't have one |
| `NoNextItem` | "No next item" | Reached end of a list |
| `NoPreviousItem` | "No previous item" | Reached beginning of a list |
| `ItemDisabled` | "Item is disabled" | When trying to activate a disabled element |

### Card Actions

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `Activating_Format` | "Activating {0}" | {0} = element name | Announced when clicking a UI element |
| `CannotActivate_Format` | "Cannot activate {0}" | {0} = element name | Element couldn't be activated |
| `CouldNotPlay_Format` | "Could not play {0}" | {0} = card name | Card play failed |
| `NoAbilityAvailable_Format` | "{0} has no activatable ability" | {0} = card name | Right-click on card with no ability |
| `NoCardSelected` | "No card selected" | | Trying to play when nothing is focused |

### Navigation Hints

| Key | English | Context |
|-----|---------|---------|
| `NavigateWithArrows` | "Arrow keys to navigate" | Appended to screen announcements as a hint |
| `BeginningOfList` | "Beginning of list" | Navigated past the first item |
| `EndOfList` | "End of list" | Navigated past the last item |
| `NavigateHint` | "Arrow keys to navigate, Enter to select" | General navigation hint |
| `BrowserHint` | "Tab to see card, Enter to keep on top" | Scry/Surveil browser hint |
| `EnterToSelect` | "Enter to select" | Short action hint |
| `TabToNavigate` | "Tab to navigate" | Short action hint |

### Menu Actions

| Key | English | Context |
|-----|---------|---------|
| `OpeningPlayModes` | "Opening play modes..." | Navigating to play mode screen |
| `OpeningDeckManager` | "Opening deck manager..." | Navigating to deck screen |
| `OpeningStore` | "Opening store..." | Navigating to store |
| `OpeningMastery` | "Opening mastery..." | Navigating to mastery/battle pass |
| `OpeningProfile` | "Opening profile..." | Navigating to player profile |
| `OpeningSettings` | "Opening settings..." | Navigating to settings |
| `QuittingGame` | "Quitting game..." | Player is quitting |
| `CannotNavigateHome` | "Cannot navigate to Home" | Home navigation failed |
| `HomeNotAvailable` | "Home button not available" | Home button not found |
| `ReturningHome` | "Returning to Home" | Going back to home screen |
| `OpeningColorChallenges` | "Opening color challenges" | Navigating to color challenge |
| `NavigatingBack` | "Back" | Going back one screen |
| `ClosingSettings` | "Closing settings" | Leaving settings screen |
| `ClosingPlayBlade` | "Closing play menu" | Closing the play mode panel |
| `ExitingDeckBuilder` | "Exiting deck builder" | Leaving deck builder |

### Deck Info

| Key | English | Context |
|-----|---------|---------|
| `DeckInfoCardCount` | "Card Count" | Deck statistics section label |
| `DeckInfoManaCurve` | "Mana Curve" | Deck statistics section label |
| `DeckInfoTypeBreakdown` | "Types" | Deck statistics section label |

### Login and Account Fields

| Key | English | Context |
|-----|---------|---------|
| `BirthYearField` | "Birth year field. Use arrow keys to select year." | Account creation form |
| `BirthMonthField` | "Birth month field. Use arrow keys to select month." | Account creation form |
| `BirthDayField` | "Birth day field. Use arrow keys to select day." | Account creation form |
| `CountryField` | "Country field. Use arrow keys to select country." | Account creation form |
| `EmailField` | "Email field. Type your email address." | Login/account form |
| `PasswordField` | "Password field. Type your password." | Login/account form |
| `ConfirmPasswordField` | "Confirm password field. Retype your password." | Account creation form |
| `AcceptTermsCheckbox` | "Accept terms checkbox. Press Enter to toggle." | Account creation form |
| `LoggingIn` | "Logging in..." | During login |
| `CreatingAccount` | "Creating account..." | During account creation |
| `SubmittingPasswordReset` | "Submitting password reset request..." | Password reset |
| `CheckingQueuePosition` | "Checking queue position..." | Server queue |
| `OpeningSupportWebsite` | "Opening support website..." | Help link |
| `NoTermsContentFound` | "No terms content found" | Terms of service empty |

### Battlefield Navigation

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `EndOfBattlefield` | "End of battlefield" | | Scrolled past last row |
| `BeginningOfBattlefield` | "Beginning of battlefield" | | Scrolled past first row |
| `EndOfRow` | "End of row" | | Last card in a battlefield row |
| `BeginningOfRow` | "Beginning of row" | | First card in a battlefield row |
| `RowEmpty_Format` | "{0} is empty" | {0} = row name (e.g., "Creatures") | Empty battlefield row |
| `RowWithCount_One` | "{0}, 1 card" | {0} = row name | Row with one card |
| `RowWithCount_Format` | "{0}, {1} cards" | {0} = row name, {1} = count | Row with multiple cards |
| `RowEmptyShort_Format` | "{0}, empty" | {0} = row name | Short form for empty row |

### Zone Navigation

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `EndOfZone` | "End of zone" | | Last card in hand/graveyard/exile |
| `BeginningOfZone` | "Beginning of zone" | | First card in zone |
| `ZoneNotFound_Format` | "{0} not found" | {0} = zone name | Zone doesn't exist |
| `ZoneEmpty_Format` | "{0}, empty" | {0} = zone name | Zone has no cards |
| `ZoneWithCount_One` | "{0}, 1 card" | {0} = zone name | Zone with one card |
| `ZoneWithCount_Format` | "{0}, {1} cards" | {0} = zone name, {1} = count | Zone with multiple cards |
| `ZoneEntry_Format` | "{0}: {1} cards. {2}, 1 of {3}" | {0} = zone name, {1} = total, {2} = first card, {3} = total | Entering a zone |
| `ZoneEntryEmpty_Format` | "{0}: empty" | {0} = zone name | Entering an empty zone |
| `CardInZone_Format` | "{0}, {1}, {2} of {3}" | {0} = card, {1} = zone label, {2} = position, {3} = total | Card position in zone |

### Targeting

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `NoValidTargets` | "No valid targets" | | No targets available |
| `NoTargetSelected` | "No target selected" | | Confirm without selecting |
| `TargetingCancelled` | "Targeting cancelled" | | User cancelled targeting |
| `SelectTargetNoValid` | "Select a target. No valid targets found." | | Targeting with none available |
| `Targeted_Format` | "Targeted {0}" | {0} = target name | Successfully targeted |
| `CouldNotTarget_Format` | "Could not target {0}" | {0} = target name | Target failed |

### Spells and Abilities

| Key | English | Context |
|-----|---------|---------|
| `NoPlayableCards` | "No playable cards" | No cards can be played right now |
| `SpellCast` | "Spell cast" | A spell was successfully cast |
| `SpellCastPrefix` | "Cast" | Prefix: "Cast Lightning Bolt" |
| `SpellUnknown` | "unknown spell" | Spell name couldn't be determined |
| `ResolveStackFirst` | "Resolve stack first. Press Space to resolve or Tab to select targets." | Stack needs resolving |
| `AbilityTriggered` | "triggered" | Label for triggered abilities on stack |
| `AbilityActivated` | "activated" | Label for activated abilities on stack |
| `AbilityUnknown` | "Ability" | Fallback ability name |
| `WaitingForPlayable` | "Waiting for playable cards..." | Auto-pass while no actions available |

### Discard

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `NoSubmitButtonFound` | "No submit button found" | | Internal error |
| `CouldNotSubmitDiscard` | "Could not submit discard" | | Discard submission failed |
| `DiscardCount_One` | "Discard 1 card" | | Discard prompt for 1 |
| `DiscardCount_Format` | "Discard {0} cards" | {0} = count | Discard prompt for multiple |
| `CardsSelected_One` | "1 card selected" | | Selection count |
| `CardsSelected_Format` | "{0} cards selected" | {0} = count | Selection count |
| `NeedHaveSelected_Format` | "Need {0}, have {1} selected" | {0} = required, {1} = current | How many more to select |
| `SubmittingDiscard_Format` | "Submitting {0} cards for discard" | {0} = count | Confirming discard |
| `CouldNotSelect_Format` | "Could not select {0}" | {0} = card name | Card selection failed |

### Card Details

| Key | English | Context |
|-----|---------|---------|
| `EndOfCard` | "End of card" | Scrolled past last detail line |
| `BeginningOfCard` | "Beginning of card" | Scrolled past first detail line |
| `CardInfoName` | "Name" | Card detail label |
| `CardInfoQuantity` | "Quantity" | How many copies owned |
| `CardInfoCollection` | "Collection" | Collection/set name |
| `CardInfoManaCost` | "Mana Cost" | Card's mana cost |
| `CardInfoPowerToughness` | "Power and Toughness" | Creature P/T |
| `CardInfoType` | "Type" | Card type line |
| `CardInfoRules` | "Rules" | Rules text |
| `CardInfoFlavor` | "Flavor" | Flavor text |
| `CardInfoArtist` | "Artist" | Card artist |
| `CardPosition_Format` | "{0}{1}, {2} of {3}" | {0} = card name, {1} = status suffix, {2} = position, {3} = total |

### Library, Hand, and Counts

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `LibraryCount_One` | "Library, 1 card" | | Your library has 1 card |
| `LibraryCount_Format` | "Library, {0} cards" | {0} = count | Your library count |
| `OpponentLibraryCount_One` | "Opponent's library, 1 card" | | Opponent's library has 1 card |
| `OpponentLibraryCount_Format` | "Opponent's library, {0} cards" | {0} = count | Opponent's library count |
| `OpponentHandCount_One` | "Opponent's hand, 1 card" | | Opponent has 1 card in hand |
| `OpponentHandCount_Format` | "Opponent's hand, {0} cards" | {0} = count | Opponent's hand count |
| `LibraryCountNotAvailable` | "Library count not available" | | Data unavailable |
| `OpponentLibraryCountNotAvailable` | "Opponent's library count not available" | | Data unavailable |
| `OpponentHandCountNotAvailable` | "Opponent's hand count not available" | | Data unavailable |

### Player Info

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `PlayerInfo` | "Player info" | | Player info zone label |
| `You` | "You" | | Label for your player |
| `Opponent` | "Opponent" | | Label for opponent |
| `EndOfProperties` | "End of properties" | | Scrolled past last property |
| `PlayerType` | "player" | | Word "player" for announcements |
| `Life_Format` | "{0} life" | {0} = life total | Life announcement |
| `LifeNotAvailable` | "Life not available" | | Life data unavailable |
| `TimerNotAvailable` | "Timer not available" | | Timer data unavailable |
| `Timeouts_One` | "1 timeout" | | Player has 1 timeout |
| `Timeouts_Format` | "{0} timeouts" | {0} = count | Player timeout count |
| `GamesWon_One` | "1 game won" | | Best-of-3 wins |
| `GamesWon_Format` | "{0} games won" | {0} = count | Best-of-3 wins |
| `WinsNotAvailable` | "Wins not available" | | Data unavailable |
| `RankNotAvailable` | "Rank not available" | | Data unavailable |

### Emotes

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `Emotes` | "Emotes" | | Emote menu label |
| `EmoteSent_Format` | "{0} sent" | {0} = emote name | Emote was sent |
| `EmotesNotAvailable` | "Emotes not available" | | Emote system unavailable |

### Input Fields

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `InputFieldEmpty` | "empty" | | Input field has no text |
| `InputFieldStart` | "start" | | Cursor at start |
| `InputFieldEnd` | "end" | | Cursor at end |
| `InputFieldStar` | "star" | | Star character for passwords |
| `InputFieldCharacterCount_One` | "1 character" | | Field has 1 character |
| `InputFieldCharacterCount_Format` | "{0} characters" | {0} = count | Character count |
| `InputFieldContent_Format` | "{0}: {1}" | {0} = label, {1} = content | Field with content |
| `InputFieldEmptyWithLabel_Format` | "{0}, empty" | {0} = label | Empty field with label |
| `InputFieldPasswordWithCount_Format` | "{0}, {1} characters" | {0} = label, {1} = count | Password field (content hidden) |
| `EditingTextField` | "Editing. Type to enter text, Escape to exit." | | Entered edit mode |
| `ExitedEditMode` | "Exited edit mode" | | Left edit mode |
| `TextField` | "text field" | | Generic label |
| `HasCharacters_Format` | "has {0} characters" | {0} = count | Character count info |
| `ExitedInputField` | "Exited input field" | | Left input field |

### Character Names (for reading input aloud)

These are the spoken names for special characters when reading input fields character by character.

| Key | English | Context |
|-----|---------|---------|
| `CharSpace` | "space" | Space character |
| `CharDot` | "dot" | Period (.) |
| `CharComma` | "comma" | Comma (,) |
| `CharExclamation` | "exclamation" | Exclamation mark (!) |
| `CharQuestion` | "question" | Question mark (?) |
| `CharAt` | "at" | At sign (@) |
| `CharHash` | "hash" | Hash (#) |
| `CharDollar` | "dollar" | Dollar ($) |
| `CharPercent` | "percent" | Percent (%) |
| `CharAnd` | "and" | Ampersand (&) |
| `CharStar` | "star" | Asterisk (*) |
| `CharDash` | "dash" | Hyphen (-) |
| `CharUnderscore` | "underscore" | Underscore (_) |
| `CharPlus` | "plus" | Plus (+) |
| `CharEquals` | "equals" | Equals (=) |
| `CharSlash` | "slash" | Forward slash (/) |
| `CharBackslash` | "backslash" | Backslash (\) |
| `CharColon` | "colon" | Colon (:) |
| `CharSemicolon` | "semicolon" | Semicolon (;) |
| `CharQuote` | "quote" | Double quote (") |
| `CharApostrophe` | "apostrophe" | Single quote (') |
| `CharOpenParen` | "open paren" | Opening parenthesis |
| `CharCloseParen` | "close paren" | Closing parenthesis |
| `CharOpenBracket` | "open bracket" | Opening square bracket |
| `CharCloseBracket` | "close bracket" | Closing square bracket |
| `CharOpenBrace` | "open brace" | Opening curly brace |
| `CharCloseBrace` | "close brace" | Closing curly brace |
| `CharLessThan` | "less than" | Less than (<) |
| `CharGreaterThan` | "greater than" | Greater than (>) |
| `CharPipe` | "pipe" | Pipe (|) |
| `CharTilde` | "tilde" | Tilde (~) |
| `CharBacktick` | "backtick" | Backtick (`) |
| `CharCaret` | "caret" | Caret (^) |

### Mana Symbols

Spoken names for mana symbols on cards.

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `ManaTap` | "Tap" | | Tap symbol (T) |
| `ManaUntap` | "Untap" | | Untap symbol (Q) |
| `ManaWhite` | "White" | | White mana (W) |
| `ManaBlue` | "Blue" | | Blue mana (U) |
| `ManaBlack` | "Black" | | Black mana (B) |
| `ManaRed` | "Red" | | Red mana (R) |
| `ManaGreen` | "Green" | | Green mana (G) |
| `ManaColorless` | "Colorless" | | Colorless mana (C) |
| `ManaX` | "X" | | Variable mana (X) |
| `ManaSnow` | "Snow" | | Snow mana (S) |
| `ManaEnergy` | "Energy" | | Energy counter (E) |
| `ManaPhyrexian_Format` | "Phyrexian {0}" | {0} = color name | Phyrexian mana (e.g., "Phyrexian Red") |
| `ManaHybrid_Format` | "{0} or {1}" | {0} = color 1, {1} = color 2 | Hybrid mana (e.g., "White or Blue") |

### Settings Menu (F2)

| Key | English | Context |
|-----|---------|---------|
| `SettingsMenuTitle` | "Mod Settings" | Settings menu title |
| `SettingsMenuInstructions` | "Arrow Up and Down to navigate, Enter to change, Backspace or F2 to close" | Settings hint |
| `SettingsMenuClosed` | "Mod settings closed" | Leaving settings |
| `SettingLanguage` | "Language" | Language setting label |
| `SettingTutorialMessages` | "Tutorial messages" | Tutorial hints toggle |
| `SettingVerboseAnnouncements` | "Verbose announcements" | Verbose mode toggle |
| `SettingOn` | "On" | Toggle state |
| `SettingOff` | "Off" | Toggle state |
| `SettingChanged_Format` | "{0} set to {1}" | {0} = setting name, {1} = new value |
| `SettingItemPosition_Format` | "{0} of {1}: {2}" | {0} = position, {1} = total, {2} = item text |

### Help Menu (F1)

| Key | English | Context |
|-----|---------|---------|
| `HelpMenuTitle` | "Help Menu" | Help screen title |
| `HelpMenuInstructions` | "Arrow Up and Down to navigate, Backspace or F1 to close" | Help hint |
| `HelpItemPosition_Format` | "{0} of {1}: {2}" | {0} = position, {1} = total, {2} = help text |
| `HelpMenuClosed` | "Help closed" | Leaving help |

### Help Categories

| Key | English | Context |
|-----|---------|---------|
| `HelpCategoryGlobal` | "Global shortcuts" | Help section heading |
| `HelpCategoryMenuNavigation` | "Menu navigation" | Help section heading |
| `HelpCategoryDuelZones` | "Zones in duel" | Help section heading |
| `HelpCategoryDuelInfo` | "Duel information" | Help section heading |
| `HelpCategoryCardNavigation` | "Card navigation in zone" | Help section heading |
| `HelpCategoryCardDetails` | "Card details" | Help section heading |
| `HelpCategoryCombat` | "Combat" | Help section heading |
| `HelpCategoryBrowser` | "Browser (Scry, Surveil, Mulligan)" | Help section heading |
| `HelpCategoryInputFields` | "Input fields" | Help section heading |
| `HelpCategoryDebug` | "Debug keys (developers)" | Help section heading |

### Help Items

These are the individual shortcut descriptions shown in the help menu. Translate naturally - the key names describe the shortcut, but the value should read well as a spoken instruction.

| Key | English |
|-----|---------|
| `HelpF1Help` | "F1: Help menu" |
| `HelpF2Settings` | "F2: Settings menu" |
| `HelpF3Context` | "F3: Current screen" |
| `HelpCtrlRRepeat` | "Control plus R: Repeat last announcement" |
| `HelpBackspace` | "Backspace: Back, dismiss, or cancel" |
| `HelpArrowUpDown` | "Arrow Up or Down: Navigate menu items" |
| `HelpTabNavigation` | "Tab or Shift plus Tab: Navigate menu items, or switch groups in collection" |
| `HelpArrowLeftRight` | "Arrow Left or Right: Carousel and stepper controls" |
| `HelpHomeEnd` | "Home or End: Jump to first or last item" |
| `HelpPageUpDown` | "Page Up or Page Down: Previous or next page in collection" |
| `HelpNumberKeysFilters` | "Number keys 1 to 0: Activate filters 1 to 10 in collection" |
| `HelpEnterSpace` | "Enter or Space: Activate" |
| `HelpEnterEditField` | "Enter: Start editing text field" |
| `HelpEscapeExitField` | "Escape: Stop editing, stay on field" |
| `HelpTabNextField` | "Tab: Stop editing and move to next element" |
| `HelpShiftTabPrevField` | "Shift plus Tab: Stop editing and move to previous element" |
| `HelpArrowsInField` | "Arrows in field: Left or Right reads character, Up or Down reads content" |
| `HelpCHand` | "C: Your hand, Shift plus C: Opponent hand count" |
| `HelpBBattlefield` | "B: Your creatures, Shift plus B: Opponent creatures" |
| `HelpALands` | "A: Your lands, Shift plus A: Opponent lands" |
| `HelpRNonCreatures` | "R: Your non-creatures, Shift plus R: Opponent non-creatures" |
| `HelpGGraveyard` | "G: Your graveyard, Shift plus G: Opponent graveyard" |
| `HelpXExile` | "X: Your exile, Shift plus X: Opponent exile" |
| `HelpSStack` | "S: Stack" |
| `HelpDLibrary` | "D: Your library count, Shift plus D: Opponent library count" |
| `HelpLLifeTotals` | "L: Life totals" |
| `HelpTTurnPhase` | "T: Turn and phase" |
| `HelpVPlayerInfo` | "V: Player info zone" |
| `HelpLeftRightCards` | "Left or Right arrow: Previous or next card" |
| `HelpHomeEndCards` | "Home or End: First or last card" |
| `HelpEnterPlay` | "Enter: Play or activate card" |
| `HelpTabTargets` | "Tab: Cycle through targets or playable cards" |
| `HelpUpDownDetails` | "Up or Down arrow: Navigate card details" |
| `HelpSpaceCombat` | "Space: Confirm attackers or blockers" |
| `HelpBackspaceCombat` | "Backspace: No attacks or cancel blocks" |
| `HelpTabBrowser` | "Tab: Navigate all cards" |
| `HelpCDZones` | "C or D: Jump to keep or bottom zone" |
| `HelpEnterToggle` | "Enter: Toggle card between zones" |
| `HelpSpaceConfirm` | "Space: Confirm selection" |
| `HelpF4Refresh` | "F4: Refresh current navigator" |
| `HelpF11CardDump` | "F11: Dump card details to log (pack opening)" |
| `HelpF12UIDump` | "F12: Dump UI hierarchy to log" |

### Browser (Scry, Surveil, Mulligan)

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `NoCards` | "No cards" | | Browser has no cards |
| `NoButtonSelected` | "No button selected" | | No browser button focused |
| `NoButtonsAvailable` | "No buttons available" | | No browser buttons exist |
| `CouldNotTogglePosition` | "Could not toggle position" | | Card move failed |
| `Selected` | "selected" | | Card toggled to selected state |
| `Deselected` | "deselected" | | Card toggled to deselected state |
| `InHand` | "in hand" | | Card location label |
| `OnStack` | "on stack" | | Card location label |
| `Confirmed` | "Confirmed" | | Browser submission confirmed |
| `Cancelled` | "Cancelled" | | Browser submission cancelled |
| `NoConfirmButton` | "No confirm button found" | | Confirm button missing |
| `KeepOnTop` | "keep" | | Scry: card stays on top of library |
| `PutOnBottom` | "selected" | | Scry: card put on bottom of library |
| `CouldNotClick_Format` | "Could not click {0}" | {0} = button name | Button click failed |
| `BrowserCards_One` | "{0}. 1 card. Tab to navigate, Enter to select" | {0} = browser title | Browser with 1 card |
| `BrowserCards_Format` | "{0}. {1} cards. Tab to navigate, Enter to select" | {0} = browser title, {1} = count | Browser with multiple cards |
| `BrowserOptions_Format` | "{0}. Tab to navigate options" | {0} = browser title | Browser with options |

### Mastery / Battle Pass

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `MasteryActivation_Format` | "{0}. Level {1} of {2}, {3}. Arrow keys to navigate levels." | {0} = title, {1} = current, {2} = max, {3} = status | Mastery screen activation |
| `MasteryLevel_Format` | "Level {0}: {1}" | {0} = level, {1} = reward | Level description |
| `MasteryLevelWithStatus_Format` | "Level {0}: {1}. {2}" | {0} = level, {1} = reward, {2} = status | Level with status |
| `MasteryTier_Format` | "{0}: {1}" | {0} = tier name, {1} = reward | Tier description |
| `MasteryTierWithQuantity_Format` | "{0}: {1}x {2}" | {0} = tier, {1} = quantity, {2} = reward | Tier with quantity |
| `MasteryPage_Format` | "Page {0} of {1}" | {0} = current, {1} = total | Page navigation |
| `MasteryLevelDetail_Format` | "Level {0}. {1}" | {0} = level, {1} = details | Level detail view |
| `MasteryLevelDetailWithStatus_Format` | "Level {0}. {1}. {2}" | {0} = level, {1} = details, {2} = status | Level detail with status |
| `MasteryCompleted` | "completed" | | Level completed |
| `MasteryCurrentLevel` | "current level" | | Currently active level |
| `MasteryPremiumLocked` | "premium locked" | | Requires battle pass |
| `MasteryFree` | "Free" | | Free track |
| `MasteryPremium` | "Premium" | | Premium track |
| `MasteryRenewal` | "Renewal" | | Renewal track |
| `MasteryNoReward` | "no reward" | | Level has no reward |
| `MasteryStatus` | "Status" | | Status label |
| `MasteryStatusInfo_Format` | "Level {0} of {1}" | {0} = current, {1} = max | Status info |
| `MasteryStatusInfoWithXP_Format` | "Level {0} of {1}, {2}" | {0} = current, {1} = max, {2} = XP text | Status with XP |

### Prize Wall

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `PrizeWallActivation_Format` | "Prize Wall. {0} items. {1} spheres available. Arrow keys to navigate." | {0} = item count, {1} = sphere count | Prize wall activation |
| `PrizeWallItem_Format` | "{0} of {1}: {2}" | {0} = position, {1} = total, {2} = item | Prize wall item |
| `PrizeWallSphereStatus_Format` | "{0} spheres available" | {0} = count | Sphere count |
| `PopupCancel` | "Cancel" | | Cancel button label |

### UI Structure Announcements

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `ItemsCount_One` | "1 item" | | Generic item count |
| `ItemsCount_Format` | "{0} items" | {0} = count | Generic item count |
| `ItemCount_One` | "1 item" | | Same, alternate key |
| `ItemCount_Format` | "{0} items" | {0} = count | Same, alternate key |
| `ActivationWithItems_Format` | "{0}. {1}. {2}" | {0} = screen name, {1} = item count, {2} = hint | Screen activation |
| `GroupCount_Format` | "{0} groups" | {0} = count | Number of UI groups |
| `GroupItemCount_Format` | "{0}, {1}" | {0} = group name, {1} = item count text | Group with count |
| `ItemPositionOf_Format` | "{0} of {1}: {2}" | {0} = position, {1} = total, {2} = item text | Item position |
| `ScreenGroupsSummary_Format` | "{0}. {1}. {2}" | {0} = screen name, {1} = group summary, {2} = hint | Screen with groups |
| `ScreenItemsSummary_Format` | "{0}. {1}. {2}" | {0} = screen name, {1} = item summary, {2} = hint | Screen with items |
| `ObjectivesEntry_Format` | "Objectives, {0}" | {0} = count text | Objectives section |
| `PositionOf_Format` | "{0} of {1}" | {0} = position, {1} = total | Generic position |
| `LabelValue_Format` | "{0}: {1}" | {0} = label, {1} = value | Generic label: value pair |
| `NoItemsFound` | "No items found." | | Empty list |
| `NoNavigableItemsFound` | "No navigable items found." | | No interactive elements |

### Search and Filters

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `SearchResults_Format` | "Search results: {0} cards" | {0} = count | Card search results |
| `SearchResultsItems_Format` | "Search results: {0} items" | {0} = count | Generic search results |
| `NoSearchResults` | "No search results" | | Empty search |
| `ApplyingFilters` | "Applying filters" | | Filters being applied |
| `FiltersReset` | "Filters reset" | | Filters cleared |
| `FiltersCancelled` | "Filters cancelled" | | Filter dialog cancelled |
| `FiltersDismissed` | "Filters dismissed" | | Filter panel closed |
| `FilterLabel_Format` | "{0}: {1}" | {0} = filter name, {1} = value | Active filter |
| `NoFilter_Format` | "No filter {0}. {1} filters available." | {0} = number, {1} = total | Invalid filter number |
| `NoFiltersAvailable` | "No filters available" | | No filters on screen |

### Miscellaneous UI

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `DropdownClosed` | "Dropdown closed" | | Dropdown menu closed |
| `DropdownOpened` | "Dropdown opened. Use arrow keys to select, Enter to confirm." | | Dropdown opened |
| `PopupClosed` | "Popup closed" | | Popup dismissed |
| `CouldNotClosePopup` | "Could not close popup" | | Popup dismiss failed |
| `Percent_Format` | "{0} percent" | {0} = number | Percentage value |
| `ActionNotAvailable` | "Action not available" | | Generic action not possible |
| `Mana_Format` | "Mana: {0}" | {0} = mana symbols | Mana display |
| `FirstSection` | "First section" | | Navigated to first section |
| `LastSection` | "Last section" | | Navigated to last section |
| `StartOfRow` | "Start of row" | | Row boundary |
| `EndOfRowNav` | "End of row" | | Row boundary |
| `Opening_Format` | "Opening {0}" | {0} = target name | Opening something |
| `Toggled_Format` | "{0}, toggled" | {0} = element name | Element toggled |
| `FirstPack` | "First pack" | | First pack in opening |
| `LastPack` | "Last pack" | | Last pack in opening |
| `Page_Format` | "Page {0} of {1}" | {0} = current, {1} = total | Pagination |
| `PageLabel_Format` | "{0} page" | {0} = page name | Named page |
| `Activated_Format` | "Activated {0}" | {0} = element name | Element activated |
| `BackToMailList` | "Back to mail list" | | Returning to inbox |
| `AtTopLevel` | "At top level. Use Done button to exit." | | Top of navigation |
| `NoItemsAvailable_Format` | "{0}. No items available." | {0} = section name | Empty section |
| `Loading_Format` | "Loading {0}..." | {0} = what's loading | Loading state |
| `TabItems_Format` | "{0}. {1} items." | {0} = tab name, {1} = count | Tab with items |
| `TabNoItems_Format` | "{0}. No items available." | {0} = tab name | Empty tab |
| `NoPurchaseOption` | "No purchase option available" | | Store: can't buy |
| `NoDetailsAvailable` | "No details available" | | No details to show |
| `NoCardDetails` | "No card details available" | | Card info missing |
| `Tabs_Format` | "Tabs. {0} tabs." | {0} = count | Tab bar summary |
| `OptionsAvailable_Format` | "{0} options available. {1}" | {0} = count, {1} = hint | Options list |
| `Continuing` | "Continuing" | | Continuing past a screen |
| `FoundRewards_Format` | "Found {0} rewards" | {0} = count | Rewards found |
| `Characters_Format` | "{0} characters" | {0} = count | Character count |
| `PaymentPage_Format` | "Payment page. {0} elements." | {0} = count | Store payment screen |
| `CouldNotMove_Format` | "Could not move {0}" | {0} = card name | Card move failed |
| `MovedTo_Format` | "{0} moved to {1}" | {0} = card name, {1} = destination | Card moved |
| `CouldNotSend_Format` | "Could not send {0}" | {0} = emote name | Emote send failed |
| `PortraitNotFound` | "Portrait not found" | | Avatar not found |
| `PortraitNotAvailable` | "Portrait not available" | | Avatar unavailable |
| `PortraitButtonNotFound` | "Portrait button not found" | | Avatar button missing |
| `NoActiveScreen` | "No active screen" | | F3 with no screen |
| `NoCardToInspect` | "No card selected to inspect." | | Debug: no card |
| `NoElementSelected` | "No element selected for pack investigation." | | Debug: no element |
| `DebugDumpComplete` | "Debug dump complete. Check log." | | F12 debug dump |
| `CardDetailsDumped` | "Card details dumped to log." | | F11 debug dump |
| `NoPackToInspect` | "No pack to inspect." | | Debug: no pack |
| `CouldNotFindPackParent` | "Could not find pack parent." | | Debug: pack parent missing |
| `PackDetailsDumped` | "Pack details dumped to log." | | Debug: pack dump done |

### Language Names

These should be translated into the target language (so French users see "Fran\u00e7ais", not "French").

| Key | English | Context |
|-----|---------|---------|
| `LangEnglish` | "English" | Language picker |
| `LangGerman` | "German" | Language picker |
| `LangFrench` | "French" | Language picker |
| `LangSpanish` | "Spanish" | Language picker |
| `LangItalian` | "Italian" | Language picker |
| `LangPortuguese` | "Portuguese" | Language picker |
| `LangJapanese` | "Japanese" | Language picker |
| `LangKorean` | "Korean" | Language picker |
| `LangRussian` | "Russian" | Language picker |
| `LangPolish` | "Polish" | Language picker |
| `LangChineseSimplified` | "Chinese Simplified" | Language picker |
| `LangChineseTraditional` | "Chinese Traditional" | Language picker |

### Element Groups

Group names for UI sections announced when navigating between groups.

| Key | English | Context |
|-----|---------|---------|
| `GroupPrimaryActions` | "Primary Actions" | Main buttons |
| `GroupPlay` | "Play" | Play section |
| `GroupProgress` | "Progress" | Progress section |
| `GroupObjectives` | "Objectives" | Daily/weekly quests |
| `GroupSocial` | "Social" | Friends section |
| `GroupFilters` | "Filters" | Filter controls |
| `GroupContent` | "Content" | Main content area |
| `GroupSettings` | "Settings" | Settings section |
| `GroupSecondaryActions` | "Secondary Actions" | Secondary buttons |
| `GroupDialog` | "Dialog" | Dialog/popup buttons |
| `GroupFriends` | "Friends" | Friends list |
| `GroupTabs` | "Tabs" | Tab navigation |
| `GroupPlayOptions` | "Play Options" | Play mode options |
| `GroupFolders` | "Folders" | Deck folders |
| `GroupSettingsMenu` | "Settings Menu" | Settings menu section |
| `GroupTutorial` | "Tutorial" | Tutorial section |
| `GroupCollection` | "Collection" | Card collection |
| `GroupDeckList` | "Deck List" | Deck listing |
| `GroupDeckInfo` | "Deck Info" | Deck information |
| `GroupMailList` | "Mail List" | Inbox |
| `GroupMail` | "Mail" | Mail content |
| `GroupRewards` | "Rewards" | Rewards section |
| `GroupOther` | "Other" | Uncategorized elements |

### Screen Titles

These are announced when entering a screen. They should be short and descriptive.

| Key | English | Context |
|-----|---------|---------|
| `ScreenHome` | "Home" | Main menu |
| `ScreenDecks` | "Decks" | Deck manager |
| `ScreenProfile` | "Profile" | Player profile |
| `ScreenStore` | "Store" | In-game store |
| `ScreenMastery` | "Mastery" | Battle pass screen |
| `ScreenAchievements` | "Achievements" | Achievement list |
| `ScreenLearnToPlay` | "Learn to Play" | Tutorial section |
| `ScreenPackOpening` | "Pack Opening" | Opening packs |
| `ScreenColorChallenge` | "Color Challenge" | Tutorial challenges |
| `ScreenDeckBuilder` | "Deck Builder" | Building a deck |
| `ScreenDeckSelection` | "Deck Selection" | Choosing a deck |
| `ScreenEvent` | "Event" | Event details |
| `ScreenRewards` | "Rewards" | Reward screen |
| `ScreenPacks` | "Packs" | Pack selection |
| `ScreenCardUnlocked` | "Card Unlocked" | New card reward |
| `ScreenCardUnlocked_One` | "Card Unlocked, 1 card" | 1 card unlocked |
| `ScreenCardUnlocked_Format` | "Card Unlocked, {0} cards" | {0} = count of cards unlocked |
| `ScreenPackContents` | "Pack Contents" | Viewing pack cards |
| `ScreenPackContents_One` | "Pack Contents, 1 card" | Pack with 1 card |
| `ScreenPackContents_Format` | "Pack Contents, {0} cards" | {0} = count of cards |
| `ScreenFriends` | "Friends" | Friends list |
| `ScreenHomeWithEvents` | "Home with Events" | Home when events shown |
| `ScreenHomeWithColorChallenge` | "Home with Color Challenge" | Home during tutorial |
| `ScreenNavigationBar` | "Navigation Bar" | Bottom nav bar |
| `ScreenCollection` | "Collection" | Card collection |
| `ScreenSettings` | "Settings" | Settings menu |
| `ScreenMenu` | "Menu" | Generic menu |
| `ScreenPlayModeSelection` | "Play Mode Selection" | Choosing play mode |
| `ScreenDirectChallenge` | "Direct Challenge" | Direct challenge screen |
| `ScreenFriendChallenge` | "Friend Challenge" | Friend challenge screen |
| `ScreenConfirmation` | "Confirmation" | Confirmation dialog |
| `ScreenInviteFriend` | "Invite Friend" | Friend invite dialog |
| `ScreenSocial` | "Social" | Social features |
| `ScreenPlay` | "Play" | Play screen |
| `ScreenEvents` | "Events" | Events list |
| `ScreenFindMatch` | "Find Match" | Matchmaking |
| `ScreenMatchEnded` | "Match ended" | Post-match screen |
| `ScreenSearchingForMatch` | "Searching for match" | Matchmaking in progress |
| `ScreenLoading` | "Loading" | Loading screen |
| `ScreenSettingsGameplay` | "Settings, Gameplay" | Gameplay settings tab |
| `ScreenSettingsGraphics` | "Settings, Graphics" | Graphics settings tab |
| `ScreenSettingsAudio` | "Settings, Audio" | Audio settings tab |
| `ScreenDownload` | "Download screen" | Asset download |
| `ScreenAdvancedFilters` | "Advanced Filters" | Advanced filter panel |
| `ScreenPrizeWall` | "Prize Wall" | Prize wall screen |
| `ScreenDuel` | "Duel" | Active game |
| `ScreenPreGame` | "Pre-game screen" | Before match starts |
| `ScreenWhatsNew` | "What's New" | What's new overlay |
| `ScreenAnnouncement` | "Announcement" | Announcement overlay |
| `ScreenRewardPopup` | "Reward popup" | Reward popup overlay |
| `ScreenOverlay` | "Overlay" | Generic overlay |
| `WaitingForServer` | "Waiting for server" | Server response pending |

### Zone Names

Zone names announced during duel navigation when pressing zone shortcut keys.

| Key | English | Context |
|-----|---------|---------|
| `Zone_Hand` | "Your hand" | Announced when pressing C to enter your hand |
| `Zone_Battlefield` | "Battlefield" | General battlefield label |
| `Zone_Graveyard` | "Your graveyard" | Announced when pressing G for your graveyard |
| `Zone_Exile` | "Exile" | Announced when pressing X for exile zone |
| `Zone_Stack` | "Stack" | Announced when pressing S for the stack |
| `Zone_Library` | "Your library" | Your library zone |
| `Zone_Command` | "Command zone" | Commander format command zone |
| `Zone_OpponentHand` | "Opponent's hand" | Announced when pressing Shift+C for opponent hand count |
| `Zone_OpponentGraveyard` | "Opponent's graveyard" | Announced when pressing Shift+G |
| `Zone_OpponentLibrary` | "Opponent's library" | Announced when pressing Shift+D |
| `Zone_OpponentExile` | "Opponent's exile" | Announced when pressing Shift+X |

### Battlefield Rows

Battlefield row names announced when navigating rows with Shift+Up/Down or shortcut keys.

| Key | English | Context |
|-----|---------|---------|
| `Row_PlayerCreatures` | "Your creatures" | Announced when pressing B |
| `Row_PlayerNonCreatures` | "Your non-creatures" | Announced when pressing R |
| `Row_PlayerLands` | "Your lands" | Announced when pressing A |
| `Row_EnemyCreatures` | "Enemy creatures" | Announced when pressing Shift+B |
| `Row_EnemyNonCreatures` | "Enemy non-creatures" | Announced when pressing Shift+R |
| `Row_EnemyLands` | "Enemy lands" | Announced when pressing Shift+A |

### Browser Zone Names

Zone names used in Scry, Surveil, and London mulligan browser navigation. Announced when pressing C or D to jump between zones.

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `BrowserZone_KeepOnTop` | "Keep on top" | | Scry: zone label for cards staying on top of library |
| `BrowserZone_PutOnBottom` | "Put on bottom" | | Scry: zone label for cards going to bottom |
| `BrowserZone_KeepPile` | "Keep pile" | | London mulligan: zone label for cards kept in hand |
| `BrowserZone_BottomPile` | "Bottom pile" | | London mulligan: zone label for cards put on bottom |
| `BrowserZone_KeepShort` | "keep" | | Short zone label suffix for keep zone |
| `BrowserZone_BottomShort` | "bottom" | | Short zone label suffix for bottom zone |
| `BrowserZone_Empty_Format` | "{0}: empty" | {0} = zone name | Announced when entering an empty browser zone |
| `BrowserZone_Entry_Format` | "{0}: {1} cards. {2}, 1 of {3}" | {0} = zone name, {1} = total cards, {2} = first card name, {3} = total | Announced when entering a browser zone with cards |
| `BrowserZone_Card_Format` | "{0}, {1}, {2} of {3}" | {0} = card name, {1} = zone label, {2} = position, {3} = total | Card position within a browser zone |
| `BrowserZone_NoCardSelected` | "No card selected" | | No card is focused in browser |

### Browser Types

Friendly names for browser types, announced when a browser opens (e.g., "Scry. 2 cards. Tab to navigate...").

| Key | English | Context |
|-----|---------|---------|
| `Browser_Scry` | "Scry" | Scry effect browser |
| `Browser_Surveil` | "Surveil" | Surveil effect browser |
| `Browser_ReadAhead` | "Read ahead" | Read ahead (Saga) browser |
| `Browser_SearchLibrary` | "Search library" | Library search effect |
| `Browser_Mulligan` | "Mulligan" | London mulligan browser |
| `Browser_OpeningHand` | "Opening hand" | Opening hand display |
| `Browser_OrderCards` | "Order cards" | Card ordering effect |
| `Browser_SplitCards` | "Split cards into piles" | Fact or Fiction style pile splitting |
| `Browser_AssignDamage` | "Assign damage" | Damage assignment browser |
| `Browser_ViewAttachments` | "View attachments" | Viewing card attachments |
| `Browser_ChooseFromList` | "Choose from list" | Generic list choice |
| `Browser_SelectCards` | "Select cards" | Card selection browser |
| `Browser_SelectGroup` | "Select group" | Group selection browser |
| `Browser_ChooseManaType` | "Choose mana type" | Mana type choice |
| `Browser_ChooseKeyword` | "Choose keyword" | Keyword ability choice |
| `Browser_ChooseDungeonRoom` | "Choose dungeon room" | Dungeon room choice |
| `Browser_MutateChoice` | "Mutate choice" | Mutate positioning choice |
| `Browser_ChooseYesOrNo` | "Choose yes or no" | Yes/no decision |
| `Browser_OptionalAction` | "Optional action" | Optional ability trigger |
| `Browser_Information` | "Information" | Informational display |
| `Browser_ChooseAction` | "Choose action" | Action choice browser |
| `Browser_Default` | "Browser" | Fallback browser name for unknown types |

### Combat States

Status strings appended to card names during combat to describe their combat state.

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `Combat_Attacking` | "attacking" | | Card is declared as an attacker |
| `Combat_CanAttack` | "can attack" | | Card is eligible to attack |
| `Combat_Blocking_Format` | "blocking {0}" | {0} = attacker name | Card is blocking a specific attacker |
| `Combat_Blocking` | "blocking" | | Card is blocking (no specific attacker known) |
| `Combat_BlockedBy_Format` | "blocked by {0}" | {0} = blocker name(s) | Attacker is blocked by named creature(s) |
| `Combat_SelectedToBlock` | "selected to block" | | Card has been selected as a blocker |
| `Combat_CanBlock` | "can block" | | Card is eligible to block |
| `Combat_Tapped` | "tapped" | | Card is tapped |
| `Combat_PTBlocking_Format` | "{0}/{1} blocking" | {0} = power, {1} = toughness | Blocker with P/T shown |
| `Combat_Assigned` | "assigned" | | Blocker has been assigned to an attacker |

### Target Actions

Announcement strings for target and selection actions during spells and abilities.

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `Target_Targeted_Format` | "Targeted {0}" | {0} = target name | Announced when a target is selected via targeting mode |
| `Target_Selected_Format` | "Selected {0}" | {0} = target name | Announced when a target is selected via selection mode |

### Card Relationships

Strings describing card relationships (enchantments, attachments, targeting) shown in card details and announcements.

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `Card_EnchantedBy_One_Format` | "enchanted by {0}" | {0} = enchantment name | Card has one enchantment attached |
| `Card_EnchantedBy_Many_Format` | "enchanted by {0}" | {0} = comma-separated enchantment names | Card has multiple enchantments |
| `Card_AttachedTo_Format` | "attached to {0}" | {0} = host card name | Card is attached to another card (equipment, aura) |
| `Card_Targeting_One_Format` | "targeting {0}" | {0} = target name | Spell or ability targeting one card |
| `Card_Targeting_Two_Format` | "targeting {0} and {1}" | {0} = first target, {1} = second target | Spell targeting exactly two cards |
| `Card_Targeting_Many_Format` | "targeting {0}" | {0} = comma-separated target names | Spell targeting three or more cards |
| `Card_TargetedBy_One_Format` | "targeted by {0}" | {0} = source name | Card is targeted by one spell/ability |
| `Card_TargetedBy_Two_Format` | "targeted by {0} and {1}" | {0} = first source, {1} = second source | Card targeted by two sources |
| `Card_TargetedBy_Many_Format` | "targeted by {0}" | {0} = comma-separated source names | Card targeted by three or more sources |

### Duel Announcements

All duel event announcements. These are spoken automatically as game events occur.

**Game Start and Turns:**

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `Duel_Started_Format` | "Duel started. {0} cards in hand" | {0} = hand size | Announced at duel start |
| `Duel_YourTurn_Format` | "Turn {0}" | {0} = turn number | Announced at start of your turn |
| `Duel_OpponentTurn` | "Opponent's turn" | | Announced at start of opponent's turn |
| `Duel_TurnChanged` | "Turn changed" | | Generic turn change notification |
| `Duel_Your` | "Your" | | Possessive label for your phase announcements |
| `Duel_Opponents` | "Opponent's" | | Possessive label for opponent's phase announcements |
| `Duel_You` | "You" | | Subject label for your actions |
| `Duel_Opponent` | "Opponent" | | Subject label for opponent actions |

**Phases (capitalized, announced as phase transitions):**

| Key | English | Context |
|-----|---------|---------|
| `Duel_Phase_FirstMain` | "First main phase" | Phase announcement |
| `Duel_Phase_SecondMain` | "Second main phase" | Phase announcement |
| `Duel_Phase_DeclareAttackers` | "Declare attackers" | Phase announcement |
| `Duel_Phase_DeclareBlockers` | "Declare blockers" | Phase announcement |
| `Duel_Phase_CombatDamage` | "Combat damage" | Phase announcement |
| `Duel_Phase_EndOfCombat` | "End of combat" | Phase announcement |
| `Duel_Phase_Combat` | "Combat phase" | Phase announcement |
| `Duel_Phase_Upkeep` | "Upkeep" | Phase announcement |
| `Duel_Phase_Draw` | "Draw" | Phase announcement |
| `Duel_Phase_EndStep` | "End step" | Phase announcement |

**Phase Descriptions (lowercase, used in combined turn/phase strings):**

| Key | English | Context |
|-----|---------|---------|
| `Duel_PhaseDesc_FirstMain` | "first main phase" | Used in turn info via T key |
| `Duel_PhaseDesc_SecondMain` | "second main phase" | Used in turn info via T key |
| `Duel_PhaseDesc_DeclareAttackers` | "declare attackers" | Used in turn info via T key |
| `Duel_PhaseDesc_DeclareBlockers` | "declare blockers" | Used in turn info via T key |
| `Duel_PhaseDesc_CombatDamage` | "combat damage" | Used in turn info via T key |
| `Duel_PhaseDesc_EndOfCombat` | "end of combat" | Used in turn info via T key |
| `Duel_PhaseDesc_Combat` | "combat phase" | Used in turn info via T key |
| `Duel_PhaseDesc_Upkeep` | "upkeep" | Used in turn info via T key |
| `Duel_PhaseDesc_Draw` | "draw" | Used in turn info via T key |
| `Duel_PhaseDesc_EndStep` | "end step" | Used in turn info via T key |
| `Duel_PhaseDesc_Beginning` | "beginning phase" | Used in turn info via T key |
| `Duel_PhaseDesc_Ending` | "ending phase" | Used in turn info via T key |
| `Duel_PhaseDesc_Turn` | "turn" | Used in turn info via T key |

**Turn and Phase Combined Formats:**

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `Duel_TurnPhase_Format` | "{0} {1}, turn {2}" | {0} = "Your"/"Opponent's", {1} = phase desc, {2} = turn number | Full turn/phase info via T key |
| `Duel_TurnPhaseNoCount_Format` | "{0} {1}" | {0} = "Your"/"Opponent's", {1} = phase desc | Turn/phase when turn number unknown |

**Card Draws:**

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `Duel_Drew_One` | "Drew 1 card" | | You drew one card |
| `Duel_Drew_Format` | "Drew {0} cards" | {0} = count | You drew multiple cards |
| `Duel_OpponentDrew_One` | "Opponent drew 1 card" | | Opponent drew one card |
| `Duel_OpponentDrew_Format` | "Opponent drew {0} cards" | {0} = count | Opponent drew multiple cards |

**Life Changes:**

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `Duel_LifeGained_Format` | "{0} gained {1} life" | {0} = "You"/"Opponent", {1} = amount | Life gain event |
| `Duel_LifeLost_Format` | "{0} lost {1} life" | {0} = "You"/"Opponent", {1} = amount | Life loss event |

**Damage:**

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `Duel_DamageDeals_Format` | "{0} deals {1} to {2}" | {0} = source, {1} = amount, {2} = target | Damage event with named source |
| `Duel_DamageAmount_Format` | "{0} to {1}" | {0} = amount, {1} = target | Damage amount to target |
| `Duel_DamageToYou` | "you" | | Target label when damage goes to you |
| `Duel_DamageToOpponent` | "opponent" | | Target label when damage goes to opponent |
| `Duel_DamageTarget` | "target" | | Generic target label for damage |
| `Duel_CombatDamageSource` | "Combat damage" | | Source label for combat damage |
| `Duel_DamageTo_Format` | "{0} deals {1} to {2}" | {0} = source, {1} = amount, {2} = target | Alternate damage format |
| `Duel_DamageAmountTo_Format` | "{0} damage to {1}" | {0} = amount, {1} = target | Damage amount description |
| `Duel_CreatureDamage_Format` | "{0} deals {1} to {2}" | {0} = creature, {1} = amount, {2} = target | Creature dealing damage |

**Combat Events:**

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `Duel_CombatBegins` | "Combat begins" | | Combat phase started |
| `Duel_AttackerDeclared` | "Attacker declared" | | You declared an attacker |
| `Duel_OpponentAttackerDeclared` | "Opponent's attacker declared" | | Opponent declared an attacker |
| `Duel_Attacking_Format` | "{0} attacking" | {0} = creature name | Named creature is attacking |
| `Duel_AttackingPT_Format` | "{0} {1} attacking" | {0} = creature name, {1} = P/T | Named creature with P/T attacking |
| `Duel_AttackerRemoved` | "Attacker removed" | | An attacker was undeclared |
| `Duel_Attackers_One` | "1 attacker" | | Attacker count summary |
| `Duel_Attackers_Format` | "{0} attackers" | {0} = count | Multiple attackers count |

**Battlefield Enter/Leave:**

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `Duel_OpponentPlayedCard` | "Opponent played a card" | | Opponent played a card (name unknown) |
| `Duel_OpponentEnteredBattlefield_One` | "Opponent: 1 permanent entered battlefield" | | One opponent permanent entered |
| `Duel_OpponentEnteredBattlefield_Format` | "Opponent: {0} permanents entered battlefield" | {0} = count | Multiple opponent permanents entered |
| `Duel_LeftBattlefield_One` | "1 permanent left battlefield" | | One permanent left battlefield |
| `Duel_LeftBattlefield_Format` | "{0} permanents left battlefield" | {0} = count | Multiple permanents left |
| `Duel_EntersBattlefield_Format` | "{0} enters battlefield" | {0} = card name | Named card entering battlefield |
| `Duel_EntersBattlefieldFromLibrary_Format` | "{0} enters battlefield from library" | {0} = card name | Card entering from library |
| `Duel_EntersBattlefieldFromLibraryEnchanting_Format` | "{0} enters battlefield from library, enchanting {1}" | {0} = card name, {1} = target | Card entering from library attached to target |
| `Duel_TokenCreated_Format` | "{0} token created" | {0} = token name | A token was created |
| `Duel_Played_Format` | "{0} played {1}" | {0} = player, {1} = card name | Player played a card |
| `Duel_Enchanted_Format` | "{0} enchanted {1}" | {0} = aura name, {1} = target | Aura attached to a permanent |

**Graveyard Events:**

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `Duel_CardToYourGraveyard` | "Card went to your graveyard" | | Generic graveyard event (name unknown) |
| `Duel_CardToOpponentGraveyard` | "Card went to opponent's graveyard" | | Generic graveyard event for opponent |
| `Duel_Died_Format` | "{0}{1} died" | {0} = owner prefix, {1} = card name | Creature died (state-based action) |
| `Duel_Destroyed_Format` | "{0}{1} was destroyed" | {0} = owner prefix, {1} = card name | Permanent was destroyed |
| `Duel_Sacrificed_Format` | "{0}{1} was sacrificed" | {0} = owner prefix, {1} = card name | Permanent was sacrificed |
| `Duel_Countered_Format` | "{0}{1} was countered" | {0} = owner prefix, {1} = spell name | Spell was countered |
| `Duel_Discarded_Format` | "{0}{1} was discarded" | {0} = owner prefix, {1} = card name | Card was discarded |
| `Duel_Milled_Format` | "{0}{1} was milled" | {0} = owner prefix, {1} = card name | Card was milled from library |
| `Duel_WentToGraveyard_Format` | "{0}{1} went to graveyard" | {0} = owner prefix, {1} = card name | Generic card-to-graveyard |
| `Duel_SpellResolved` | "Spell resolved" | | A spell on the stack resolved |
| `Duel_ReturnedFromGraveyard_Format` | "{0} returned from graveyard" | {0} = card name | Card returned from graveyard to battlefield |
| `Duel_ReturnedFromGraveyardEnchanting_Format` | "{0} returned from graveyard, enchanting {1}" | {0} = card name, {1} = target | Card returned from graveyard attached to target |

**Exile Events:**

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `Duel_Exiled_Format` | "{0}{1} was exiled" | {0} = owner prefix, {1} = card name | Card was exiled from battlefield |
| `Duel_ExiledFromGraveyard_Format` | "{0}{1} was exiled from graveyard" | {0} = owner prefix, {1} = card name | Card exiled from graveyard |
| `Duel_ExiledFromHand_Format` | "{0}{1} was exiled from hand" | {0} = owner prefix, {1} = card name | Card exiled from hand |
| `Duel_ExiledFromLibrary_Format` | "{0}{1} was exiled from library" | {0} = owner prefix, {1} = card name | Card exiled from library |
| `Duel_CounteredAndExiled_Format` | "{0}{1} was countered and exiled" | {0} = owner prefix, {1} = spell name | Spell countered and exiled |
| `Duel_ReturnedFromExile_Format` | "{0} returned from exile" | {0} = card name | Card returned from exile to battlefield |
| `Duel_ReturnedFromExileEnchanting_Format` | "{0} returned from exile, enchanting {1}" | {0} = card name, {1} = target | Card returned from exile attached to target |

**Hand Events:**

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `Duel_ReturnedToHand_Format` | "{0}{1} returned to hand" | {0} = owner prefix, {1} = card name | Card bounced to hand from battlefield |
| `Duel_ReturnedToHandFromGraveyard_Format` | "{0}{1} returned to hand from graveyard" | {0} = owner prefix, {1} = card name | Card returned to hand from graveyard |
| `Duel_ReturnedToHandFromExile_Format` | "{0}{1} returned to hand from exile" | {0} = owner prefix, {1} = card name | Card returned to hand from exile |

**Counters:**

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `Duel_CounterGained_Format` | "{0} gained {1} {2} counter" | {0} = card name, {1} = count, {2} = counter type | Singular counter gained |
| `Duel_CounterGainedPlural_Format` | "{0} gained {1} {2} counters" | {0} = card name, {1} = count, {2} = counter type | Multiple counters gained |
| `Duel_CounterLost_Format` | "{0} lost {1} {2} counter" | {0} = card name, {1} = count, {2} = counter type | Singular counter lost |
| `Duel_CounterLostPlural_Format` | "{0} lost {1} {2} counters" | {0} = card name, {1} = count, {2} = counter type | Multiple counters lost |
| `Duel_CounterCreature` | "creature" | | Default counter type label |

**Reveals:**

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `Duel_Revealed_Format` | "Revealed {0}" | {0} = card name | A card was revealed |

**Game End:**

| Key | English | Context |
|-----|---------|---------|
| `Duel_Victory` | "Victory!" | You won the game |
| `Duel_Defeat` | "Defeat" | You lost the game |
| `Duel_GameEnded` | "Game ended" | Generic game end |

**Browser Hints:**

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `Duel_ScryHint` | "Scry. Tab to see card, Enter to keep on top, Space to put on bottom" | | Announced when scry browser opens |
| `Duel_SurveilHint` | "Surveil. Tab to see card, Enter to keep on top, Space to put in graveyard" | | Announced when surveil browser opens |
| `Duel_EffectHint_Format` | "{0}. Tab to navigate, Enter to select" | {0} = effect name | Announced for other browser-style effects |
| `Duel_LookAtTopCard` | "Look at top card" | | Announced for top-of-library peek effects |

**London Mulligan:**

| Key | English | Placeholders | Context |
|-----|---------|------------|---------|
| `Duel_SelectForBottom_One` | "Select 1 card to put on bottom. {0} cards. Enter to toggle, Space when done" | {0} = total cards in hand | Mulligan prompt for 1 card |
| `Duel_SelectForBottom_Format` | "Select {0} cards to put on bottom. {1} cards. Enter to toggle, Space when done" | {0} = cards to select, {1} = total cards | Mulligan prompt for multiple cards |
| `Duel_SelectedForBottom_Format` | "{0} of {1} selected for bottom" | {0} = currently selected, {1} = required | Selection progress during mulligan |

**Owner Prefix:**

| Key | English | Context |
|-----|---------|---------|
| `Duel_OwnerPrefix_Opponent` | "Opponent's " | Prepended to card names for opponent's cards in zone change announcements. Note the trailing space. |

## Translation Tips

- **Be concise.** These strings are read aloud by a screen reader. Short and clear is better than long and formal.
- **Use your language's MTG terminology.** For terms like "Library", "Graveyard", "Exile" - use whatever the official Magic: The Gathering translations use in your language.
- **Keep key names from help items.** Keyboard keys like "F1", "Tab", "Enter", "Backspace", "Space", "Escape" should stay as-is (they are the key labels on the keyboard). Translate the descriptions around them.
- **Test with a screen reader.** If possible, run the mod with your language selected and listen to how the translations sound when spoken aloud. Some phrasing that reads well on screen sounds awkward when spoken.
- **Placeholders can be reordered.** If your language puts the count before the noun, that's fine: `"{1} cartes dans {0}"` works just as well as `"{0}, {1} cards"`.

## How to Test

1. Build the mod: `dotnet build src/AccessibleArena.csproj`
2. Copy the DLL to the game's Mods folder
3. Launch the game, press F2 to open settings, change language
4. Navigate through screens and listen to announcements

If you can't build the mod, you can still submit translations - the maintainers will build and test.

## Checking for Missing Keys

To find keys that exist in `en.json` but are missing from your language file, you can compare the key lists. Every key in `en.json` should have a corresponding entry in your language file. Missing keys will automatically fall back to English, so partial translations are fine - but complete translations are better.
