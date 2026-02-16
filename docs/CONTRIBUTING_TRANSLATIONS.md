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
