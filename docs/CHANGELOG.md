# Changelog

All notable changes to Accessible Arena.

## v0.5 - 2026-02-03

### Mailbox Accessibility
- Full keyboard navigation for Mailbox/Inbox screen
- Two-level navigation: mail list and mail content views
- When viewing mail list: navigate between mails with Up/Down
- When viewing mail content: see title, body text, and action buttons (Claim, More Info)
- Backspace in mail content returns to mail list
- Backspace in mail list closes Mailbox and returns to Home
- Mailbox items announce with proper title extraction via `TryGetMailboxItemTitle()`
- Fixed Nav_Mail button activation (onClick had no listeners, now invokes `NavBarController.MailboxButton_OnClick()`)

### Rewards/Mastery Screen
- Added `ProgressionTracksContentController` to content controller detection
- Backspace navigation now closes Rewards screen and returns to Home
- Screen displays as "Rewards" in announcements

### Technical
- Split `ElementGroup.Mailbox` into `MailboxList` and `MailboxContent` for proper filtering
- Added `IsMailContentVisible()` to detect when a specific mail is opened
- Added `IsInsideMailboxList()` and `IsInsideMailboxContent()` filters
- Added `CloseMailDetailView()` to close mail and return to list
- Harmony patch for `ContentControllerPlayerInbox.OnLetterSelected()` to detect mail opening
- Fixed PlayBlade bypass to only apply to actual PlayBlade elements (not Mailbox)
- Added `IsInsidePlayBladeContainer()` check to prevent bypass affecting other panels
- PanelStatePatch patches `NavBarController.MailboxButton_OnClick()` and `HideInboxIfActive()`
- Added screen name mapping for ProgressionTracksContentController in MenuScreenDetector

---

## v0.4 - 2026-02-02

### New Features

#### NPE Tutorial Accessibility
- NPE objective stages (Stage I, II, III, etc.) now readable with completion status
- Automatic detection of "Completed" status when stage checkmark is shown
- Dynamic button detection on NPE screen (e.g., "Play" button appearing after dialogue)
- GeneralMenuNavigator yields to NPERewardNavigator when reward popup opens

#### Mana Pool Tracking
- Press A key during duels to announce current floating mana
- Mana announced as "2 Green, 1 Blue" format
- Tracks mana production events in real-time

#### Deck Builder Improvements
- Card add/remove functionality with Enter key
- Deck list navigation with cards properly detected
- Fixed deck list cards not appearing when MainDeck_MetaCardHolder is inactive
- Fixed deck folder navigation: Enter expands folders, Backspace collapses
- Icon button labels extracted from element names (Edit, Delete, Export)

#### Collection Navigation
- Page Up/Page Down for collection page navigation
- Filter to show only newly added cards after page switch
- Group state preserved across page changes
- Fixed flavor text lookup for collection cards

#### PlayBlade Navigation
- Fixed Blade_ListItem buttons (Bot-Match, Standard) not visible in FindMatch
- Event page Play button now included in navigation
- Fixed navigation hierarchy after tab activation

### Bug Fixes

#### PlayBlade Folder Navigation
- Fixed Enter key on folder toggles activating wrong element (e.g., Bot-Match instead of folder)
- Root cause: `GetEnterAndConsume()` didn't check `EnterPressedWhileBlocked` flag, bypassing grouped navigation

#### Toggle/Checkbox Activation
- Fixed double-toggle on Enter key with frame-aware flag
- Fixed Enter key on toggles closing UpdatePolicies panel
- Fixed toggle activation and input field navigation sync issues
- Documented hybrid navigation approach for checkboxes

#### Input Fields
- Fixed input field auto-focus: Tab enters edit mode, arrows don't
- Improved edit mode detection for login forms

#### Popups
- Fixed popup Cancel button closing via SystemMessageManager
- Simplified popup dismissal logic

#### Other Fixes
- Fixed Settings menu showing 0 elements on Login scene
- Extract play mode names from element names instead of generic translations
- Improved objectives text extraction with full progress and reward info

### Technical
- Added TryGetNPEObjectiveText in UITextExtractor for NPE stage extraction
- Added periodic CustomButton count monitoring for NPE scene
- Added ValidateElements check for NPE rewards screen detection

---

## v0.3 - 2026-01-29

### New Features

#### Objectives & Progress Groups (Home Screen)
- New Objectives subgroup within Progress group for quests and daily/weekly wins
- Progress indicators (objectives, battle pass, daily wins) now navigable
- Subgroup navigation: Enter to drill into Objectives, Backspace to return to Progress
- Quest text displayed with progress (e.g., "Cast 20 black or red spells, 5/15")

#### Subgroup Navigation System
- Groups can now contain nested subgroups for better organization
- Subgroup entries appear as "GroupName, X items" within parent group
- Enter on subgroup entry navigates into it
- Backspace from subgroup returns to parent group (not group list)
- Technical: SubgroupType field, _subgroupElements storage, enter/exit handling

#### NPE (New Player Experience) Reward Screen
- New dedicated NPERewardNavigator for card unlock screens
- Left/Right arrows navigate between unlocked cards and Take Reward button
- Up/Down arrows read card details (name, type, mana cost, rules text)
- Backspace activates Take Reward button for quick dismissal

#### PlayBlade & Match Finding
- Complete PlayBlade navigation with tabs, game modes, and deck folders
- Auto-play after deck selection in ranked/standard queues
- Deck selection properly preserved during match workflow
- Centralized PlayBlade logic with clear state management

#### Deck Builder & Collection
- Collection cards now navigable with Left/Right arrows
- Card info reading with Up/Down arrows in collection view
- Page Up/Page Down for collection page navigation
- Complete card info: name, mana cost, type, P/T, rules text, flavor text, artist
- Placeholder cards (GrpId = 0) filtered out from navigation
- Group state preserved across page changes
- Deck action navigation (Delete, Edit, Export) with arrow keys
- Fixed back navigation with Backspace in deck builder
- Tab/Shift+Tab cycles between Collection, Filters, and Deck groups (auto-enters)
- Number keys 1-0 activate filter options 1-10 directly
- Page navigation shows only newly added cards (not entire page)
- Color filters, advanced filters, and search controls grouped into Filters

#### Element Grouping System
- Hierarchical menu navigation with element groups
- Play and Progress groups on home screen
- Color filters as standalone groups
- Single-element groups display cleanly

#### Settings Menu Everywhere
- Settings menu accessible via Escape in all scenes
- Works in menus, duels, drafts, and sealed
- Dedicated SettingsMenuNavigator with popup support
- Logout confirmation and other popups fully navigable

### Bug Fixes

#### Input Fields & Dropdowns
- Fixed input field edit mode detection (selected vs focused states)
- Fixed Tab navigation skipping or announcing wrong fields
- Fixed dropdown auto-open when navigating with arrow keys
- Escape and Backspace properly close dropdowns
- Fixed double announcements when navigating to dropdowns
- Backspace in input fields announces deleted characters

#### Popup & Button Activation
- Fixed popup button activation and EventSystem selection sync
- Fixed Settings menu popup navigation (logout confirmation)
- Improved SystemMessageButtonView method detection

#### Duel Improvements
- Improved announcements for triggered/activated abilities
- Enhanced attacker announcements with names and P/T
- Fixed "countered" vs "resolved" detection for spells
- Fixed combat state display during Declare Attackers/Blockers

#### PlayBlade Navigation
- Fixed group restore overwriting PlayBlade auto-entries after tab activation
- Tab→Content→Folders hierarchy now works correctly during blade close/open cycles
- Group restore skipped in PlayBlade context to prevent interference with navigation flow

### Technical
- Exclude Options_Button from navigation (accessible via Escape)
- Add TooltipTrigger debug logging for future tooltip support
- Document TooltipTrigger component structure in GAME_ARCHITECTURE.md
- NullClaimButton activation via NPEContentControllerRewards.OnClaimClicked_Unity

---

## v0.2.7 - 2026-01-28

### Bug Fixes
- **Dropdown auto-open on navigation**: Fixed dropdowns auto-opening when navigating to them with arrow keys
  - MTGA auto-opens dropdowns when they receive EventSystem selection
  - Now uses actual `IsExpanded` property from dropdown components instead of focus-based assumptions
  - Auto-opened dropdowns are immediately closed, user must press Enter to open
  - Added multiple suppression flags to prevent double announcements and unwanted mode entry
  - See Technical Debt section in KNOWN_ISSUES.md for details on the flag mechanics

- **Dropdown closing**: Escape and Backspace now properly close dropdowns on login screens
  - Previously Escape triggered back navigation instead of closing the dropdown
  - Backspace now works as universal dismiss key for dropdowns
  - Announces "Dropdown closed" when dismissed

- **Input field edit mode detection**: Fixed inconsistent edit mode behavior
  - Game auto-focuses input fields on navigation; mod now properly detects this
  - Separated "selected" state (field navigated to) from "focused" state (caret visible)
  - Escape now properly exits edit mode without triggering back navigation
  - Arrow key reading only activates when field is actually focused
  - KeyboardManagerPatch blocks Escape when on any input field (selected or focused)

- **Input field content reading**: Fixed input fields not announcing their content when navigating
  - Added fallback to `textComponent.text` when `.text` property is empty
  - Added support for legacy Unity `InputField` (not just TMP_InputField)
  - Content now reads correctly when Tab/Escape exits input field

- **Tab navigation on Login screens**: Tab and arrow keys now navigate the same list consistently
  - Tab key is now consumed to prevent game's Tab handling from interfering
  - Disabled grouped navigation on Login scene (not needed for simple forms)
  - Fixed double-navigation issue where game and mod both moved on Tab press

### Technical
- `UIFocusTracker.IsAnyDropdownExpanded()` queries actual `IsExpanded` property via reflection
- `UIFocusTracker.GetExpandedDropdown()` returns currently expanded dropdown for closing
- `BaseNavigator.CloseDropdownOnElement()` closes auto-opened dropdowns and sets suppression flags
- `BaseNavigator._skipDropdownModeTracking` prevents `_wasInDropdownMode` re-entry after auto-close
- `UIFocusTracker._suppressNextFocusAnnouncement` prevents duplicate announcements
- `UIFocusTracker._suppressDropdownModeEntry` prevents dropdown mode re-entry after auto-close
- `BaseNavigator.HandleDropdownNavigation()` now intercepts Escape/Backspace and calls `CloseActiveDropdown()`
- `CloseActiveDropdown()` finds parent TMP_Dropdown/Dropdown/cTMP_Dropdown and calls `Hide()`
- `GetElementAnnouncement()` now handles legacy InputField and tries textComponent fallback
- Tab handling uses `InputManager.GetKeyDownAndConsume(KeyCode.Tab)` to block game processing
- `GeneralMenuNavigator.DiscoverElements()` disables grouped navigation when `_currentScene == "Login"`

**Files:** `BaseNavigator.cs`, `UIFocusTracker.cs`, `GeneralMenuNavigator.cs`, `UITextExtractor.cs`, `SCREENS.md`

## v0.2.6 - 2026-01-27

### Bug Fixes
- Fix confirmation popups not navigable in Settings menu
  - Popups like logout confirmation now properly detected and announced
  - Popup message is read aloud (e.g., "Confirmation. Are you sure you want to log out?")
  - Popup buttons (OK/Cancel) navigable with arrow keys
  - Enter activates selected button
  - Backspace dismisses popup (finds cancel/close button)
  - After popup closes, navigation returns to Settings menu

### Technical
- SettingsMenuNavigator now subscribes to PanelStateManager.OnPanelChanged
- Added popup tracking state (_activePopup, _isPopupActive)
- DiscoverPopupElements() finds SystemMessageButtonView, CustomButton, and Button components
- ExtractPopupMessage() reads popup title/message for announcement
- DismissPopup() and FindPopupCancelButton() handle backspace dismissal

**Files:** `SettingsMenuNavigator.cs`

## v0.2.5 - 2026-01-27

### Bug Fixes (Popup Button - Still Not Working)

Three fixes attempted for popup button activation issue. Popup buttons (e.g., "Continue editing" / "Discard deck") still require two Enter presses despite these changes:

1. **Popup destruction detection during scene change**
   - `AlphaPanelDetector.CleanupDestroyedPanels()` now reports popups as closed when their GameObject is destroyed
   - Previously, destroyed popups were silently removed from tracking without notifying PanelStateManager
   - Uses `ReportPanelClosedByName()` since GameObject reference is null

2. **Enter key consumption in menu navigation**
   - `GeneralMenuNavigator` now uses `InputManager.GetEnterAndConsume()` instead of `Input.GetKeyDown()`
   - Prevents game from processing Enter on EventSystem's selected object (e.g., Nav_Decks) simultaneously
   - Enter key is marked as consumed so Harmony patch blocks it from game's KeyboardManager

3. **SystemMessageButtonView method name fix**
   - Changed from `OnClick()` to `Click()` - the actual method name on SystemMessageButtonView
   - Debug logging revealed available methods: `Init(SystemMessageButtonData, Action)` and `Click()`
   - `TryInvokeMethod` now tries `Click()` first, then `OnClick()`, then `OnButtonClicked()`

**Files:** `AlphaPanelDetector.cs`, `GeneralMenuNavigator.cs`, `UIActivator.cs`

## v0.2.4 - 2026-01-27

### New Features
- Add deck builder collection card accessibility
  - Collection cards now appear in navigable "Collection" group
  - Left/Right arrows navigate between cards
  - Up/Down arrows read card details (name, type, mana cost, rules text, etc.)
  - CardInfoNavigator automatically activated when focusing on cards

### Bug Fixes
- Fix DeckBuilderCollection group not appearing in group list
  - Added DeckBuilderCollection to groupOrder in GroupedNavigator
- Fix CardInfoNavigator not prepared for collection cards
  - Added UpdateCardNavigationForGroupedElement() helper method
  - Called after grouped navigation moves to prepare card reading

### Technical
- Changed UpdateCardNavigation() from private to protected in BaseNavigator
- Added card navigation integration to grouped navigation methods (MoveNext, MovePrevious, MoveFirst, MoveLast, HandleGroupedEnter)

## v0.2.3 - 2026-01-25

### New Features
- Add Settings menu accessibility during duels
  - Press Escape to open Settings menu in any scene (menus, duels, drafts, sealed)
  - New dedicated SettingsMenuNavigator handles all Settings navigation
  - Settings code removed from GeneralMenuNavigator for cleaner separation

### Architecture
- New overlay navigator integration pattern
  - Higher-priority navigators take control when overlays appear
  - Lower-priority navigators (DuelNavigator, GeneralMenuNavigator) yield via ValidateElements()
  - Uses Harmony-based PanelStateManager.IsSettingsMenuOpen for precise timing
  - Pattern documented in BEST_PRACTICES.md for future similar integrations

## v0.2.2 - 2026-01-25

### Bug Fixes
- Fix false "countered" announcements for resolving spells
  - Instants/sorceries going to graveyard after normal resolution no longer announced as "countered"
  - Only actual counterspells trigger "was countered" announcement
  - Added "countered and exiled" for exile-on-counter effects (Dissipate, etc.)

### Improvements
- Distinguish triggered/activated abilities from cast spells on stack
  - Abilities now announced as "[Name] triggered, [rules]" instead of "Cast [Name]"
  - Spells still announced as "Cast [Name], [P/T], [rules]"
- Enhanced attacker announcements when leaving Declare Attackers phase
  - Now announces each attacker with name and P/T
  - Example: "2 attackers. Sheoldred 4/5. Graveyard Trespasser 3/4. Declare blockers"

## v0.2.1 - 2026-01-25

### Bug Fixes
- Fix combat state display during Declare Attackers/Blockers phases
  - Creatures no longer incorrectly show as "attacking" or "blocking" before being assigned
  - Game pre-creates IsBlocking on all potential blockers (inactive) - now correctly checks active state
  - Display checks active state while internal counting checks existence (for token compatibility)

### Improvements
- Add "can attack" state during Declare Attackers phase (matches "can block" pattern)
- Combat states now correctly show:
  - Attackers: "can attack" → "attacking"
  - Blockers: "can block" → "selected to block" → "blocking"
- Remove unused debug field

## v0.2 - 2026-01-24

### Bug Fixes
- Fix attacker and blocker counting for tokens
  - Tokens with inactive visual indicators are now correctly counted
  - Previously "5 attackers" could be announced as "2 attackers"
- Fix life announcement ownership ("you gained" vs "opponent gained")
- Remove redundant ownership from token creation announcements
- Fix backspace navigation in content panels (BoosterChamber, Profile, Store, etc.)
- Fix rescan not triggering when popups close
- Fix popup detection and double announcements

### New Features
- Add T key to announce current turn and phase
- Add vault progress display in pack openings
  - Shows "Vault Progress +99" instead of "Unknown card" for duplicate protection
- Add debug tools: F11 (card details), F12 (UI hierarchy) - documented in help menu

### Improvements
- Major panel detection overhaul with unified alpha-based system
- New PanelStateManager for centralized panel state tracking
- Simplified GeneralMenuNavigator: removed plugin architecture, extracted debug helpers
- Improved PlayBlade detection and Color Challenge navigation
- Improved button activation reliability

### Documentation
- Updated debug keys in help menu
- Documented architecture improvements
- Updated known issues

## v0.1.4.1 - 2026-01-21

- Fix NPE rewards "Take Reward" button not appearing in navigation

## v0.1.3 - 2026-01-21

- Add BoosterOpenNavigator for pack opening accessibility
- Fix blocker tracking reset during multiple blocker assignments
- Fix arrow keys navigating menu buttons in empty zones
