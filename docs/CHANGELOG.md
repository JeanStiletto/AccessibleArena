# Changelog

All notable changes to Accessible Arena.

## v0.7.3-dev

### New: Invalid Deck Status in Deck Picker
- Deck announcements now include validity status: "invalid deck", "N invalid cards", "missing cards", "missing cards, craftable", "invalid companion", or "unavailable"
- Press Right arrow on an invalid deck to hear the detailed reason (localized tooltip with banned card counts, wildcard costs, companion issues, etc.)
- Reads DeckView's pre-computed validation state and tooltip text directly — no scene scanning or i18n keyword matching
- Files: UIActivator.cs, GeneralMenuNavigator.cs, BaseNavigator.cs

### New: Multi-Zone Browser Support (First Iteration)
- Cards that select from multiple zones (e.g., Kronleuchter targeting both graveyards) now have navigable zone selection
- Up/Down arrows cycle between zones, Tab moves to cards, Shift+Tab returns to zone selector
- Zone buttons with real localized names (e.g., "Dein Friedhof", "Friedhof des Gegners") are included; generic unnamed zones are filtered out
- False positive multi-zone scaffolds (e.g., Tiefste Epoche selecting from a single zone) are correctly detected and treated as single-zone browsers
- Invisible scaffold layout buttons (SingleButton, 2Button_Left/Right) filtered via shared IsVisibleViaCanvasGroup check
- CardInfoNavigator deactivated while on zone selector to prevent arrow key conflicts
- Files: BrowserNavigator.cs, HotHighlightNavigator.cs

### Fixed: Input Fields in Popups
- Input fields in popups (e.g., invite friend, challenge invite) now use the same full implementation as menu navigators
- Arrow Up/Down reads field content, Left/Right reads character at cursor, Backspace announces deleted character
- Tab navigates to next/previous popup item (consumed properly, no longer leaks to game)
- Legacy Unity InputField support added alongside TMP_InputField
- Shared `InputFieldEditHelper` class eliminates code duplication between BaseNavigator and PopupHandler
- Files: InputFieldEditHelper.cs (new), PopupHandler.cs, BaseNavigator.cs

### Fixed: Localize Mana Colors and Action Strings
- Mana color names (White, Blue, Black, Red, Green, Colorless, Snow, etc.) now use locale keys instead of hardcoded English
- Hybrid mana (e.g., "White or Blue") and Phyrexian mana use localized format strings
- "Activated" and "selected" announcements now use locale keys
- Color filter toggle labels in deck builder are now localized
- Added new locale keys: ManaGeneric, ManaPhyrexian (bare), Activated (bare)
- Updated: CardDetector.cs, CardModelProvider.cs, StoreNavigator.cs, UIActivator.cs, DraftNavigator.cs, GeneralMenuNavigator.cs, UIElementClassifier.cs

### Fixed: Gold Label in Top Nav
- Top-nav gold button now announces as `Gold: {amount}` instead of amount-only text
- Updated: UITextExtractor.cs

### Changed: Announcement Order
- Item count and position are now read last instead of first in all menu and screen announcements
- Content (label, hints, instructions) is announced before "X of Y" position info
- Updated across all navigators and all 12 locale files

### New: Extended Card Info (I Key) in All Screens
- The I key now works outside of duels — in deck builder, collection, store, draft, and other card screens
- Shows individual ability texts as separate navigable items (Up/Down to cycle)
- Extracts abilities from card model directly when duel-only AbilityHangerProvider is unavailable
- Files: BaseNavigator.cs, CardModelProvider.cs

### Improved: Challenge Screen Accessibility
- Main button now includes challenge status text (e.g., waiting for opponent)
- Polls for opponent join/leave and status text changes at 1-second intervals
- Detects match countdown start/cancel from status text
- Icon-only enemy buttons labeled: Kick, Block, Add Friend
- Spinners prefixed with "Locked" when settings are controlled by host
- Tournament parameters announced after mode spinner change
- Files: ChallengeNavigationHelper.cs, GeneralMenuNavigator.cs

### Fixed: Popup Leaving State
- Fixed getting stuck on empty screen after pressing buttons in popups that trigger server actions (e.g., sending invite with invalid name)
- Popup validation now checks if popup GameObject still exists before consuming input
- Uses `HandleEarlyInput()` hook to route popup input before BaseNavigator's auto-focus logic can intercept it
- Files: BaseNavigator.cs, GeneralMenuNavigator.cs, SettingsMenuNavigator.cs

## v0.7.2 - 2026-02-26

### New: Land Summary Shortcut (M / Shift+M)
- Press **M** to hear a summary of your lands: total count and untapped lands grouped by name
- Press **Shift+M** for the opponent's land summary
- Example announcements: "7 lands, 2 Island, 1 Mountain, 1 Azorius Gate untapped" or "5 lands, all tapped"
- Uses existing battlefield land rows and tap state detection
- Files: BattlefieldNavigator.cs, DuelNavigator.cs, InputManager.cs, Strings.cs, HelpNavigator.cs, en.json

### Fixed: Card Type Lines Now Correctly Localized
- Card type lines (e.g. "Legendary Creature - Elf Warrior") now display in the game's selected language instead of always showing English
- Uses `TypeTextId`/`SubtypeTextId` localization IDs via `GreLocProvider`, same system used for flavor text
- Applies to all card contexts: duel, deck builder, collection, store, booster opening, rewards, drafts, other card faces
- Files: CardModelProvider.cs, HotHighlightNavigator.cs, UIActivator.cs

### Improved: Card Name Localization
- Card names now use `TitleId` via `GreLocProvider` as primary lookup to avoid cases where names could appear in English while other card text was correctly localized
- Falls back to previous `CardTitleProvider` lookup when `TitleId` is not available
- Files: CardModelProvider.cs

### New: Deck Builder Sideboard Group
- Pool holder cards (available cards to add) are now classified as **Sideboard** instead of **Collection** when editing a deck. Applies to draft, sealed, and normal deck builder.
- New `DeckBuilderSideboard` group with "Sideboard" label (Tab-cyclable between Collection, Sideboard, Deck List, Deck Info, Filters)
- Sideboard cards use quantity-prefixed labels ("1x Card Name") matching deck list format
- Card detail navigation (Up/Down) works for sideboard cards
- Files: ElementGroup.cs, ElementGroupAssigner.cs, GroupedNavigator.cs, GeneralMenuNavigator.cs, CardModelProvider.cs, CardDetector.cs, Strings.cs, en.json, de.json

### Fixed: Single-Card Groups Shown as Standalone Items
- Deck builder card groups (Collection, Sideboard, Deck List) now always appear as proper groups even when they contain only 1 card
- Previously a single card would appear as a standalone item in the list without the group name, losing context about which section it belongs to
- Files: ElementGroup.cs, GroupedNavigator.cs

### Improved: PopupHandler Rework
- **Input field support**: Popups with input fields (e.g., InviteFriendPopup, ChallengeInviteWindow) are now fully navigable. Enter activates edit mode, Escape exits, Tab navigates to next item, Up/Down reads content, Left/Right reads character at cursor, Backspace announces deleted character.
- **Button filtering**: Dismiss overlays (background, overlay, backdrop, dismiss) automatically filtered. Duplicate button labels deduplicated. Buttons nested inside input fields or other buttons skipped.
- **Text block filtering**: Text inside input fields (placeholder, typed text) no longer appears as separate text blocks.
- **Rescan suppression**: GeneralMenuNavigator's delayed rescan (0.5s) now skips while popup is active, preventing PopupHandler's items from being overwritten.
- Files: PopupHandler.cs, GeneralMenuNavigator.cs

### New: Full Control Toggle (P / Shift+P)
- P key toggles temporary full control (resets on phase change)
- Shift+P toggles locked full control (permanent until toggled off)
- Announces "Full control on/off" and "Full control locked/unlocked"
- Uses GameManager.AutoRespManager reflection for toggle and state read

### New: Phase Stop Hotkeys (1-0)
- Number keys 1-0 toggle phase stops during duels
- 1=Upkeep, 2=Draw, 3=First Main, 4=Begin Combat, 5=Declare Attackers, 6=Declare Blockers, 7=Combat Damage, 8=End Combat, 9=Second Main, 0=End Step
- Announces "[Phase] stop set" / "[Phase] stop cleared" with localized phase names
- Note: Phase stops are "also stop here" markers. The game still stops at any phase where you have playable actions (this is standard MTGA behavior, not a mod limitation). Phase stops ensure the game also stops at the marked phase even when you have nothing to play.
- Ctrl key blocked from reaching the game in duels (prevents accidental full control toggle when silencing NVDA with Ctrl)
- Files: PriorityController.cs, DuelNavigator.cs, KeyboardManagerPatch.cs

### Fixed: End Step Phase Not Announced
- End step was never announced during duels
- Game sends `Ending/None` for the end step, but code checked for `Ending/End` which never occurs
- Without the match, the debounce timer from Second Main Phase would fire instead, incorrectly announcing "Second main phase" even when the game stopped at the end step
- Fix: Match `Ending/None` as the end step event

### New: Event System Accessibility
- Event tiles on Play Blade enriched with title, ranked/Bo3 indicators, progress pips, and in-progress status
- Event page shows "Event: {title}" with win progress summary
- Improved reading of event informational text (description, rules, rewards)
- Jump In packet selection fully navigable: Up/Down for packets, Left/Right for info blocks (name, colors, description)
- Packet activation via reflection (ClickPacket) since UIActivator can't reach PacketInput on parent GO
- Confirm button and rescan after packet selection/confirmation for async GO rebuilds
- Tested with Jump Start, Starter Duel, and Draft events
- EventAccessor static class for all reflection-based event/packet data access

### New: Draft Navigator
- Full keyboard navigation for the draft card picking screen
- Navigate available cards, select picks with Enter
- Draft popup handling for pack transitions
- Files: DraftNavigator.cs

### New: OptionalAction Browser Navigation
- Shockland-style choice prompts (e.g. "Pay 2 life?") now navigable as a browser
- Tab to navigate options, Enter to select
- Files: BrowserNavigator.cs

### Fixed: Player Target Selection (Enter Key)
- Enter on player targets (e.g. "Choose a player to draw two cards") was activating a hand card instead of selecting the player
- Root cause: HotHighlightNavigator didn't claim zone ownership when navigating to player/button targets
- Now properly claims ownership so Enter activates the correct target

## v0.7.1 - 2026-02-23

### New: Damage Assignment Browser
- Full keyboard navigation for the damage assignment browser (when your attacker is blocked by multiple creatures)
- Up/Down arrows adjust damage spinner on current blocker
- Left/Right arrows navigate between blockers
- Each blocker announced with name, P/T, current damage assigned, and lethal status
- Entry announcement: "Assign damage. [AttackerName], [Power] damage. [N] blockers"
- Lethal damage indicated when spinner value text turns gold
- Space submits damage assignment (via DoneAction reflection)
- Backspace undoes last assignment (via UndoAction reflection)
- Multiple damage assigners in one combat announced as "X of Y"
- Total damage cached from workflow's MtgDamageAssigner struct

### New: Library Zone Navigation
- D key navigates your library, Shift+D for opponent's library
- Only shows cards visible to sighted players (anti-cheat protection):
  - Cards with HotHighlight (playable from library via Future Sight, Vizier of the Menagerie, etc.)
  - Cards displayed face-up (revealed by Courser of Kruphix, Experimental Frenzy, etc.)
  - Hidden face-down cards are never exposed
- If no revealed/playable cards exist, announces library count without entering navigation
- Left/Right navigates revealed cards, Enter plays playable cards via two-click
- Full card info via Up/Down arrows on revealed library cards
- Uses `IsDisplayedFaceDown` model property for reliable reveal detection

### New: Read-Only Deck Builder Accessibility
- Starter and precon decks now navigable when opened in read-only mode
- Cards listed with quantity and name (e.g. "2x Lightning Bolt")
- Up/Down card details work on read-only deck cards
- Enter on a card announces "This deck is read only. To edit, open it from My Decks."
- Screen announces "Deck Builder, Read Only" to distinguish from editable mode
- Back button (Backspace) works as expected to return to deck list

### Fixed: Stale Combat Button During Blockers Phase
- Combat prompt button was showing stale text after blocker assignment

### Fixed: Duplicate Entries in Incoming Friend Requests
- Sub-buttons (accept/reject) inside InviteIncomingTile were registered as separate navigable entries
- One friend request appeared as 3 identical entries instead of 1

### Fixed: Home Screen Carousel Navigation Broken by Group System
- Left/Right arrow keys on the promotional carousel stopped working after grouped navigation was introduced
- Standalone Content groups (like the carousel) are navigated at GroupList level where `CurrentElement` returns null
- `HandleCarouselArrow` now checks `IsCurrentGroupStandalone` to find the element directly from the group

### Fixed: Carousel and Navbar Buttons Showing Raw GO Names Instead of Content
- Promotional carousel banner showed "Banner Left" instead of actual content text (e.g., "Du kannst es kaum erwarten...")
- Root cause: `MaxLabelLength` was 80, banner text was 83 chars - just over the limit. Increased to 120.
- Image-only navbar buttons (Nav_Settings, Nav_Learn) showed cleaned GO names ("nav settings", "nav learn")
- Added `TryGetTooltipText()` fallback in `UITextExtractor` that reads `LocString` from `TooltipTrigger` via reflection
- Now shows localized labels: "Optionen anpassen", "Kodex des Multiversums"

## v0.7.0 - 2026-02-23

### New: Friends Panel Navigation
- Full keyboard navigation for the friends/social panel
- Hierarchical groups: Friends, Incoming Requests, Sent Requests, Blocked
- Per-friend actions: Challenge, Chat, Unfriend, Block, Accept, Decline, Revoke, Unblock
- Your Profile section with full username display
- Blocked users section forced open for accessibility

### New: Challenge Screen
- Dedicated ChallengeMain navigator for direct challenge setup
- Popout stepper navigation for format/scene/timer options (Left/Right to cycle)
- Deck selection via PlayBlade-style grouped navigation with folder support
- Player status announcements (invited, waiting, deck selected)
- Invite and Leave buttons with proper state tracking across spinner changes

### New: Command Zone Shortcuts
- W key to navigate your command zone (commander/companion)
- Shift+W for opponent's command zone
- Full card details for opponent's commander
- Commander filtered from mulligan hand display

### New: Origin Zone Display
- Cards playable from non-hand zones now show their origin (e.g. "Lightning Bolt, from graveyard")
- Works for flashback, escape, commander, and similar mechanics

### Improved: Card State Change Announcements
- Pressing Enter on a battlefield card announces the resulting state change (e.g. "attacking", "selected")
- Per-frame watcher detects combat and selection state changes reliably
- Blocker assignment on an attacker now announces just "blocked by Angel" instead of redundant "attacking, blocked by Angel"
- Works for attack/block selection, and non-combat selection (sacrifice, exile)

### Improved: Selection Mode Announcements (Discard, Exile, etc.)
- Toggle announcement now shows progress: "CardName, 1 of 2 selected"
- Deselecting a card says: "CardName deselected, 0 of 2 selected"
- Required count read from game's prompt text (e.g. "Discard 2 cards")
- Number word parsing for languages that spell out numbers (e.g. "zwei" in German)
- NumberWords mappings in language files - contributors can fix their language without code changes

### Improved: Combat Announcements
- Attacker selection detected via SelectedHighlightBattlefield during declare attackers phase
- Delayed attack eligibility now uses model-based fallback for newly created tokens
- Blocker deselection and unassignment announced with card name and "can block" state
- Blocker P/T announcement no longer includes redundant "blocking" word

### Improved: Duel Performance
- Cached reflection lookups for card model access (fields, properties, methods)
- Shared DuelHolderCache for zone and battlefield holder lookups, replacing per-frame FindObjectsOfType scans
- Compiled regex patterns for highlight discovery
- Precise battlefield click positions using card screen coordinates to avoid hitting wrong overlapping tokens

### Fixed: Store and Mastery Backspace Not Returning Home
- Store tab-level Backspace now navigates home instead of silently deactivating
- Mastery screen Backspace falls back to home navigation when no in-screen back button is found
- Moved `NavigateToHome()` to BaseNavigator as shared utility for all navigators
- Bug originally reported and fix approach contributed by **@blindndangerous** (PR #1)

### Fixed
- German "milled" translation corrected from "wird gemahlen" to "wird gemillt"
- Deck builder popups now auto-enter Dialog group when opened
- Challenge invite popup navigation with per-navigator popup tracking
- Input field tutorial hint added and text field labels localized
- General duel commands section added to help menu (F1)

### Installer
- MelonLoader console window hidden by default during installation
- Unified version management via Directory.Build.props

## v0.6.9 - 2026-02-19

### New: Mana Color Picker Navigator
- Detects `ManaColorSelector` popup when activating any-color mana sources (e.g. Ilysian Caryatid, Chromatic Lantern)
- Announces available colors on open: "Choose mana color: 1 White, 2 Blue, 3 Black, 4 Red, 5 Green"
- Tab/Right and Shift+Tab/Left to navigate colors, Enter to select
- Number keys 1-6 for direct selection
- Backspace to cancel
- Multi-pick support for sources that produce multiple mana (sequential picks with re-announcement)
- Highest priority in DuelNavigator (before browser detection)
- Localized in all 12 languages

### Fixed: Duel Navigator Not Reactivating After Settings Menu
- Opening the game settings during a duel and closing it left the user with no active navigator
- Cause: `HasPreGameCancelButton()` matched the in-duel Cancel button, blocking duel re-detection
- Removed the redundant check; `HasDuelElements()` alone correctly distinguishes pre-game from duel

### Improved: Selection Mode (Discard, Exile Choices)
- Zone navigation (C + Left/Right + Enter) now toggles card selection instead of trying to play
- Selected state shown when navigating hand via zone shortcuts, not just Tab
- Game's prompt instruction announced on selection mode entry (e.g. "Discard a card")
- Tab index preserved after toggling so next Tab advances to next card
- Space submits selection even when hand cards are highlighted

### Fixed: Zone Contents Not Updating During Duels
- Zone card lists (hand, battlefield, graveyard, etc.) now refresh automatically when cards enter or leave
- Uses event-driven dirty flag: DuelAnnouncer marks navigators dirty on zone count changes
- Refresh is lazy (only on next user input), so no polling overhead
- Card index clamped after refresh to keep user at a valid position

### Installer: Open Getting Started Guide After Install
- New checkbox on the installer completion screen: "Open getting started guide in browser" (checked by default)
- Opens the README on GitHub in the user's language automatically
- English users get README.md, other languages get docs/README.{lang}.md

### Translated README Files
- Added translated README for all 11 non-English languages in docs/
- German, French, Spanish, Italian, Portuguese (BR), Japanese, Korean, Russian, Polish, Chinese Simplified, Chinese Traditional
- Full translation of installation guide, keyboard shortcuts, troubleshooting, and build instructions

### Improved: Unified Zone Announcements
- Zone/row entry now uses single announcement: "Hand, Lightning Bolt, 1 of 3" instead of separate "Hand, 3 cards" + "Lightning Bolt, 1 of 3"
- Tab navigation syncs with zone and battlefield navigators so Left/Right works correctly after Tab
- Tab to a card in a different zone triggers a proper zone change announcement
- Tab within the same zone announces just the card and position

### Improved: Dropdown Select and Close
- Enter on a dropdown item now selects it and closes the dropdown in one action
- Previously required Enter to select, then Escape/Backspace to close
- Closed dropdown displays current selected value (e.g. "Monat der Geburt: Januar, dropdown")
- Dynamic value re-read via GetDropdownDisplayValue handles TMP_Dropdown, Dropdown, and cTMP_Dropdown

### Fixed: ViewDismiss Browser Trapping Focus
- Pressing Enter on graveyard/exile cards opened a ViewDismiss card preview popup
- The popup trapped keyboard focus, leaving the user stuck with no way to dismiss
- Backspace/Space would accidentally click combat buttons (e.g. "No Attacks", "Cancel Blocks") instead of dismissing
- Root cause: "Dismiss" was missing from BrowserDetector's CancelPatterns, and PromptButton fallback hit combat buttons
- Fix: BrowserNavigator now auto-dismisses ViewDismiss popups immediately on detection via UIActivator.Activate()

### Other
- Added Wizards account creation link to README quick start section
- Added first letter navigation and rapid key-hold navigation to planned features

## v0.6.8 - 2026-02-19

### Fixed: Dropdown Navigation Overhaul
- Enter now selects dropdown items without closing the dropdown, keeping user in control
- Tab navigates between form elements in mod list order (matching arrow keys)
- Tabbing to a closed dropdown auto-opens it (screen reader standard)
- Tabbing from inside an open dropdown closes it silently and moves to next element
- Fixed registration page auto-advancing when opening the last required dropdown (Experience)
  - Game's cTMP_Dropdown fires onValueChanged on item focus, triggering premature form validation
  - DropdownStateManager now suppresses onValueChanged while dropdown is open
- Fixed dropdown-to-dropdown chain auto-advance on registration page
  - Submit events blocked for a few frames after dropdown selection
  - Auto-opened dropdowns detected and closed
- Enter/Submit blocked from reaching the game while in dropdown mode
- Selection sets value silently via reflection (bypasses onValueChanged)
- German translations: changed "Aufklappmenü" to "Dropdown" for consistency

### Fixed: UIActivator Double-Activation
- SimulatePointerClick was firing 3-4 overlapping activation methods per button press
- Now only the core pointer sequence fires
- Removed redundant TryInvokeCustomButtonOnClick after SimulatePointerClick
- Fixes NPE tutorial match failing to start on first press

### Fixed: Input Field Arrow Key Navigation
- Up/Down arrows no longer exit input field edit mode on single-line fields (registration, login)
- Unity's TMP_InputField treats Up/Down as "finish editing" in single-line mode via OnUpdateSelected, which runs before our code
- IsEditingInputField() now uses explicit edit mode flag instead of checking isFocused
- ReactivateInputField() restores field focus after Up/Down deactivation
- EventSystemPatch blocks SendMoveEventToSelectedObject during edit mode as defense-in-depth

### Other
- Added untested items to known issues: events, friends/direct challenge

## v0.6.7 - 2026-02-18

### New: Bot Match via PlayBlade
- Bot Match now accessible: Open Play → Bot-Match → select deck → Play starts a bot match
- Harmony prefix patch on `JoinMatchMaking` replaces event name with "AIBotMatch" when Bot-Match mode selected
- Bot-Match mode detected by checking element text for "Bot" on PlayBladeContent activation
- Flag auto-clears after match start and when PlayBlade closes

### New: PlayBlade Queue Type Tabs
- Replaced "Find Match" tab with three queue type tabs: Ranked, Open Play, Brawl
- Queue types appear at the same level as Events and Recent for flatter navigation
- Virtual entries injected when FindMatch is not active, real tabs used when active
- Two-step activation for virtual entries (clicks FindMatch tab, then queue type tab)
- Backspace from content returns to tabs, landing on the queue type entry
- BO3 toggle now labeled "Best of 3" (was showing "POSITION" placeholder)
- FindMatch nav tab, Play button, and New Deck button excluded from blade navigation

### New: Card Rarity Info Block
- Cards now display rarity as a navigable info block (Arrow Up/Down)
- Shown between Flavor Text and Artist in card details
- Values: Common, Uncommon, Rare, Mythic Rare, Land
- Works in all contexts: collection, deck builder, duel, store
- Localized label for all 12 supported languages

### New: Brief Cast Announcements Setting (untested)
- New setting to shorten cast announcements for your own spells

### New: NPE Deck Reward Screen Support (untested)
- NPERewardNavigator now handles deck box rewards after completing all 5 NPE tutorial stages
- Detects deck prefabs (children with `Hitbox_LidOpen`) when no card prefabs are found
- Left/Right navigates between deck boxes, Enter opens them
- Deck names extracted via UITextExtractor with fallback to "Deck 1", "Deck 2", etc.
- Localized "Decks Unlocked" screen name for all 12 languages

### Bug Fix: PlayBlade Auto-Play After Deck Selection
- Fixed auto-press Play button failing silently after deck selection
- Root cause: MainButton was classified as Unknown and excluded from navigator elements
- Fix: Search scene directly via FindObjectsOfType instead of iterating navigator elements

### Bug Fix: Selected Cards Disappearing in Selection Mode (untested)
- Fixed selected cards disappearing from Tab navigation in selection mode

### Bug Fix: German Translation Issues
- Fixed incorrect German translation strings

### Other
- README, license, and project cleanup for public beta

## v0.6.6 - 2026-02-17

### Bug Fix: Single-Item List Navigation
- Fixed: Up/Down navigation on menus/screens with only one entry would say "Beginning/End of list" without reading the entry, or say nothing at all
- Now re-announces the single entry before the boundary announcement
- Affects all navigator types: grouped menus, extended card info (I), help (F1), settings (F2)

### New: Navigable Extended Card Info (I Key)
- I key now opens a navigable modal menu instead of a one-shot announcement
- Each keyword description is a separate navigable entry (e.g., "Flying: This creature can't be blocked...")
- Linked face info split into individual entries (name, mana cost, type, P/T, rules text)
- Navigate with Up/Down arrows, Home/End to jump, close with I/Backspace/Escape
- Blocks all other input while open (same as F1 Help and F2 Settings)
- "No additional information" announced without opening menu when card has no extended info
- "No card selected to inspect" when no card is focused

### Mod Improvements
- Launch announcement now shows "Accessible Arena v0.6.6 launched" instead of generic "MTGA Accessibility Mod loaded"
- MelonInfo version updated from placeholder to actual release version

### Installer: Full Localization (12 Languages)
- All user-facing installer strings are now localized
- InstallerLocale static class loads embedded JSON resources with fallback chain (active -> English -> key name)
- Language auto-detected from OS, changeable live in welcome wizard
- Supported: English, German, French, Spanish, Italian, Portuguese (BR), Russian, Polish, Japanese, Korean, Chinese Simplified, Chinese Traditional

### Installer: Two-Page Welcome Wizard
- Page 1: Welcome message, mod version to install, language selector, Next button
- Page 2: MTGA download links (Direct Download + Download Page), Back and Install buttons

### Installer: Version Detection Fixes
- Installed version now read from registry first (stores GitHub release tag), falling back to DLL assembly version
- Prevents perpetual "update available" when DLL has stale assembly version
- New "Mod Up to Date" dialog when mod is current (shows version, offers Close or Full Reinstall)
- Removed redundant version check in MainForm after user already confirmed action
- Update mode now correctly fetches latest version from GitHub for registry storage

### CI/CD
- GitHub Actions workflow now patches MelonInfo version from git tag via `sed` before building
- Ensures runtime `Info.Version` always matches the release tag alongside the assembly version

---

## v0.6.5 - 2026-02-17

### New: Full Localization System
- Added JSON-based localization with 12 languages: English, German, French, Spanish, Italian, Portuguese (BR), Japanese, Korean, Russian, Polish, Chinese Simplified, Chinese Traditional
- All ~630 user-facing strings are now localizable, including all duel announcements, zone names, combat states, card relationships, phase names, browser types, and UI messages
- `Strings.cs` centralized accessor with properties and helper methods (`GetPhaseDescription`, `GetFriendlyBrowserName`, `GetZoneName`, `GetRowName`)
- Locale JSON files embedded in DLL for self-contained deployment - extracted to `UserData/AccessibleArena/lang/` on startup
- Fallback chain: active language -> English -> key name (never shows blank text)
- Pluralization support: OneOther (Western), Slavic 3-form (Russian/Polish), NoPluralForm (CJK)

### New: Mod Settings Menu (F2)
- Persistent settings menu accessible in all scenes including duels
- Language picker with dropdown-style cycling through all 12 languages
- Tutorial Messages toggle (On/Off) - controls keyboard hints appended to activation announcements
- Verbose Announcements toggle (On/Off) - controls extra detail like counts and positions
- Settings persist to `UserData/AccessibleArena/settings.json`
- Arrow Up/Down to navigate, Enter to change, Backspace or F2 to close

### Bug Fixes
- Escape now closes Help (F1) and Settings (F2) menus and is blocked from reaching the game while mod menus are open
- Fix false "Rewards" screen name announced during scene transitions
- Reduce NPEReward detection log spam to only log on state changes

### Documentation
- Added translation contribution guide with full string reference for all ~630 keys
- Updated LOCALIZATION.md, MOD_STRUCTURE.md, and GAME_ARCHITECTURE.md with localization system details

## v0.6 - 2026-02-14

### New Navigator: StoreNavigator
- Dedicated navigator for the Store screen (priority 55)
- Two-level navigation: tabs (Up/Down) and items within tabs (Up/Down after Enter)
- Left/Right cycles purchase options on store items (gems, gold, real money)
- Details view for deck/bundle items with navigable card list (Left/Right for cards, Up/Down for info blocks)
- Store item descriptions extracted from active tag text (discount badges, timers, limits, callouts)
- Confirmation modal handled as popup with Cancel option and proper button discovery via reflection
- Utility elements at tab level: payment method, redeem code, drop rates
- Pack progress info (goal count, description) shown as utility element
- GeneralMenuNavigator suppressed when store is active

### New: WebBrowserAccessibility
- Keyboard navigation for embedded ZFBrowser popups (Xsolla payment dialogs)
- Extracts page elements via JavaScript (EvalJSCSP to bypass CSP restrictions), recursively scans iframes
- Up/Down navigates elements, Enter activates buttons/links, Tab/Shift+Tab for form fields
- Text input support with 3-tier fallback: execCommand, keyboard events per char, direct value set
- Left/Right reads character-by-character in edit mode, Enter/Escape to start/stop editing
- PayPal CAPTCHA detection after 3 empty rescans with warning announcement
- Escape blocked from game while web browser is active
- Loading state detection via IsReady, delayed rescan after button clicks for slow page transitions
- Label detection: wrapping labels, preceding siblings, parent sibling labels, name attribute fallback
- Deduplicated repeated text elements

### New Navigator: MasteryNavigator
- Dedicated navigator for the Mastery/Rewards screen (priority 60)
- Replaces GeneralMenuNavigator's unusable 55-group flat list with structured level navigation
- Up/Down navigates mastery levels with reward name and completion status announced
- Left/Right cycles between reward tiers within a level (Free, Premium, Renewal)
- Virtual status item at position 0 with XP progress info and action buttons as tiers
- PageUp/PageDown jumps ~10 levels, Home/End jumps to status item/last level
- Enter on a level announces detailed info (all tiers, quantities, status)
- Backspace returns to Home screen
- Automatic page sync when navigating past page boundaries
- PrizeWall mode: "Mastery Tree" opens sphere-spending navigator with item navigation and purchase confirmation
- Level completion derived from game's `SetMasteryDataProvider.GetCurrentLevelIndex()` via reflection
- Reward names resolved via `MTGALocalizedString` localization system

### DeckManager Improvements
- Deck-specific toolbar buttons (Edit, Delete, Export, Favorite, Clone, Deck Details) hidden from top-level navigation
- These actions now only accessible via Right Arrow actions menu on each deck entry (expanded from 4 to 7 actions)
- Standalone buttons (Import, Collection) remain visible at top level

### Bug Fixes
- Fix tapped state not displayed for some battlefield cards
  - Was using unreliable UI scan for TappedIcon child element which the game doesn't render consistently
  - Now reads IsTapped field directly from game model via reflection (authoritative source)
- Exclude PackProgressMeter from panel detection (inherits PopupBase but isn't a real popup, was blocking payment popup detection)

---

## v0.5.1 - 2026-02-11

### Bug Fixes
- Fix card navigator staying active when opening settings menu during duel
  - Previously, pressing Escape with a card focused left CardInfoNavigator active
  - Up/Down arrows in settings would announce card details instead of navigating menu items
  - Now detects navigator transitions and deactivates CardInfoNavigator on switch
- Fix matchmaking cancel button not wired to Backspace shortcut
  - `_cancelButton` was never set in `DiscoverMatchmakingElements`, so Backspace did nothing

### Loading Screen Cleanup
- Removed Cancel and Settings buttons from loading screen navigation lists
  - Cancel accessible via Backspace, Settings via Escape — no need to navigate to them
  - Applies to MatchEnd, PreGame, and Matchmaking screens
  - Button references kept internally for shortcut functionality

---

## v0.5 - 2026-02-11

### Ctrl+Tab: Cycle Opponent Targets
- Ctrl+Tab cycles through only opponent targets during targeting (skips your own cards)
- Ctrl+Shift+Tab cycles opponent targets in reverse
- Does nothing silently when no opponent targets are highlighted
- Files: `HotHighlightNavigator.cs`

### Combat Cleanup
- Removed F key shortcut from combat phases (was redundant alias for Space)
- Space is now the only key for confirming attackers/blockers
- Removed experimental cancel-skip code from HotHighlightNavigator (mana payment handled by workflow browser)
- Removed commented-out Backspace handler from HotHighlightNavigator
- Files: `CombatNavigator.cs`, `HotHighlightNavigator.cs`, `Strings.cs`, `HelpNavigator.cs`

### Phase Announcement Debounce
- Added 100ms debounce to phase announcements to prevent spam during auto-skip
- When the game rapidly skips through phases (combat/ending/beginning), only the final phase is announced
- Previously auto-skip produced up to 6 rapid announcements ("Combat phase, Declare attackers, End of combat, Second main phase, First main phase")
- Now only the phase where the game actually stops is spoken (e.g., "First main phase")
- Attacker summary announcements bypass debounce (real combat stops, not auto-skip)
- Added Upkeep and Draw as announced phases (debounced away during auto-skip, announced when game gives priority)
- Files: `DuelAnnouncer.cs`, `DuelNavigator.cs`

### Player Info Zone: Emotes and Rank Fix
- Emote wheel now opens correctly via PortraitButton click on DuelScene_AvatarView (was clicking MatchTimer HoverArea which doesn't trigger emotes)
- Emote buttons discovered from EmoteView children (custom click handlers, not standard UI.Button)
- Player rank now reads from GameManager.MatchManager player info via reflection instead of searching for nonexistent text in RankAnchorPoint sprite
- Rank displayed as "Bronze Tier 2", "Mythic #1234", "Mythic 95%", or "Unranked"
- Player zone focus element uses PortraitButton.gameObject instead of timer HoverArea
- Removed dead code: FindCurrentPlayerAvatar, HasPlayerTargetingHighlight, IsChildOfEmoteView
- Files: `PlayerPortraitNavigator.cs`

### Player Targeting Fix
- Spells that can target players (e.g., Cracked Blitz, Lightning Bolt) now correctly discover player avatars as valid targets
- Previous approach searched for HotHighlight children on MatchTimer objects, which the game never adds
- New approach reads `DuelScene_AvatarView.HighlightSystem._currentHighlightType` via reflection
- Accepts Hot (3), Tepid (2), and Cold (1) highlight values as valid targets
- Click activates `PortraitButton` (private SerializeField) which triggers the same click path as a mouse click
- Files: `HotHighlightNavigator.cs`

### Targeting/Targeted-By Announcements
- Stack spells now announce their targets: "Lightning Bolt, targeting Grizzly Bears, 1 of 2"
- Battlefield cards announce what's targeting them: "Grizzly Bears, targeted by Lightning Bolt, 1 of 3"
- Multiple targets supported: "targeting Angel and Dragon"
- Resolves names from both battlefield and stack zones
- Uses `Model.Instance.TargetIds` / `TargetedByIds` fields via reflection
- Files: `CardModelProvider.cs`, `BattlefieldNavigator.cs`, `ZoneNavigator.cs`

### LargeScrollList Browser Choice Discovery
- Keyword choice browsers (e.g. Entstellender Künstler with 3+ choices) now discover actual choice buttons
- Previously only scaffold controls (2Button_Left, MainButton, etc.) were found because choice buttons don't match standard ButtonPatterns
- New `DiscoverLargeScrollListChoices` scans for all clickable elements that aren't standard scaffold buttons
- Choices ordered first in Tab navigation, scaffold controls last
- Files: `BrowserNavigator.cs`

### Blocker-Attacker Relationship Announcements
- Assigned blockers now announce what they're blocking: "Cat blocking Angel" instead of "Cat assigned"
- Navigating a blocker shows attacker name: "Cat, blocking Angel, 2 of 5"
- Navigating a blocked attacker shows blockers: "Angel, attacking, blocked by Cat, 3 of 5"
- Unblocked attackers unchanged: "Angel, attacking, 3 of 5"
- Uses `Model.Instance.BlockingIds` / `BlockedByIds` fields to resolve combat relationships
- Combat detection now model-first (`CardModelProvider.GetIsAttackingFromCard/GetIsBlockingFromCard`) with UI fallback
- New helpers in `CardModelProvider`: `GetBlockingIds`, `GetBlockedByIds`, `ResolveInstanceIdToName`
- Refactored `GetModelInstance` as shared cached helper (also used by `GetAttachedToId`)
- Files: `CardModelProvider.cs`, `CombatNavigator.cs`

### Attachment/Enchantment Announcements
- Battlefield cards now announce attachments: "Grizzly Bears, enchanted by Pacifism"
- Auras/equipment announce their target: "Pacifism, attached to Grizzly Bears"
- DuelAnnouncer announces enchantment resolution: "Pacifism enchanted Grizzly Bears"
- Uses `Model.Instance.AttachedToId` field on `MtgCardInstance` (discovered via decompiling `UniversalBattlefieldStack`)
- Files: `CardModelProvider.cs`, `DuelAnnouncer.cs`

### Recent Tab (Kürzlich gespielt) Improvements
- Deck labels enriched with event/mode name (e.g., "Friedhofsgeschenke, deck — Standard mit Rangliste")
- Event titles read from rendered UI text (localized) instead of raw localization keys
- Enter on a deck auto-presses the tile's play/continue button (starts match directly)
- Standalone play buttons hidden from navigation (redundant with auto-press on Enter)
- PlayBlade HandleEnter bypass for Recent tab decks (no folders to enter)
- New `RecentPlayAccessor.cs` — reflection wrapper for `LastPlayedBladeContentView` tile data

### Color Challenge (CampaignGraph) Fix
- CampaignGraph no longer misidentified as a PlayBlade overlay — treated as regular content page
- Blade state buttons (Btn_BladeIsClosed, Btn_BladeHoverClosed, Btn_BladeIsOpen) filtered from navigation
- PlayBladeHelper no longer intercepts Enter/Backspace on CampaignGraph
- Backspace exits CampaignGraph via NavigateToHome (game has no native back-to-PlayBlade path)
- Files: `PanelStateManager.cs`, `ElementGroupAssigner.cs`, `GeneralMenuNavigator.cs`

### PlayBlade Fixes
- Fixed stale PlayBladeState when blade GameObject is destroyed during page transition (entering events via Events tab showed empty screen)
- Fixed Backspace not working when game gets stuck in "Waiting for server" loading state
- Fixed PlayBlade auto-entry into content group being overridden by group restore during tab switch (debounce timing issue)
- Fixed Backspace from Events play options going to group level instead of back to tabs (HandleBackspace now checks group type directly instead of unreliable IsPlayBladeContext flag)

### Context System Removed
- Removed legacy context system (ContextManager, GameContext, INavigableContext, Contexts/) — fully superseded by the navigator system
- Removed stale "Main Menu menu. 7 options" announcements on scene change
- Removed unused P/D/S shortcut scoping and dead navigation forwarding code
- F2 "Announce current screen" moved to F3, reimplemented using navigator's ScreenName
- GeneralMenuNavigator now announces "Waiting for server" when the game's loading panel overlay is active

### Loading Screen Navigator
- New `LoadingScreenNavigator` for transitional screens (priority 65)
- **Game Loading**: Announces status messages during startup (e.g., "Starting...", "Waiting for server...")
- **Match End**: Announces victory/defeat result, rank info, navigable buttons (Continue, View Battlefield, Settings)
- **PreGame/Matchmaking**: Announces "Searching for match" with live timer, cycling hints, Cancel and Settings buttons
- Scene-scoped element discovery prevents cross-scene contamination from duel leftovers
- Polling-based element discovery handles late-loading UI (animations, network responses)
- Backspace shortcut: Continue (match end) or Cancel (matchmaking)
- Replaced broken PreBattleNavigator and fixed MatchEndScene handling in GeneralMenuNavigator
- CardInfoNavigator now deactivated on scene change (prevents stale card reading after duel)

### Browser Fixes (Scry, Surveil, London Mulligan)
- Fixed card activation in Scry/Surveil/London browsers using correct game APIs
- Browser card movement now uses drag simulation (HandleDrag/OnDragRelease) instead of RemoveCard/AddCard
- Scry uses card reordering around placeholder, matching how the game processes submissions

### Duel Navigation
- Tab/Enter navigation for prompt button choices (sacrifice, pay cost, etc.)
- Fixed first Tab in duel navigating to emote panel instead of game elements
- Fixed damage assignment browser by prioritizing discovered buttons over PromptButton
- Scoped browser button discovery to scaffold to fix villainous choice browser
- Replaced keyword-based WorkflowBrowser detection with structural check (language-independent)

### Deck Builder
- Card info refreshes after adding/removing cards (Owned/InDeck values update immediately)
- Fixed Tab from search field landing on wrong element instead of Collection
- Collection position resets to first card on page change
- Deck Info group with 2D sub-navigation for deck statistics (card counts, mana curve)

### Installer
- Fixed update check never detecting newer versions (assembly version was always 1.0.0.0)
- Fixed version comparison treating "0.4" and "0.4.0.0" as different versions

---

## v0.4.6 - 2026-02-06

### Card Info Improvements in Deck Builder
- Collection cards now show "Collection: Owned X, In Deck Y" info block (via `PagesMetaCardView._lastDisplayInfo` reflection)
- Deck list cards with unowned copies now announce "Quantity: X, missing" (via `MetaCardView.ShowUnCollectedTreatment` field)

## v0.4.5 - 2026-02-06

### CardPoolAccessor - Direct Collection Page API
- New `CardPoolAccessor` class wraps game's `CardPoolHolder` via reflection
- Collection cards now read from `_pages[1].CardViews` (only current visible page)
- Page navigation uses `ScrollNext()` / `ScrollPrevious()` directly instead of searching for UI buttons
- Page announcements: "Page X of Y" on navigation, "First page" / "Last page" at edges
- Eliminated label-based page filtering system (SaveCollectionCardsForPageFilter, ApplyCollectionPageFilter)
- Fallback to hierarchy scan if CardPoolHolder not found

### Performance Improvements
- Reduced search rescan delay from 30 frames to 12 frames (~645ms at ~18fps game rate)
- Reduced page rescan delay from 20 frames to 8 frames with IsScrolling() short-circuit
- Removed `LogAvailableUIElements()` call from PerformRescan (was 500-750ms debug dump on every rescan)
- UI dump now only runs once per screen in DetectScreen, not on every rescan

### Bug Fixes
- Collection search now shows correct cards (only current page, no offscreen contamination)
- Page navigation no longer requires finding page buttons by name/label matching

### Technical
- `CardPoolAccessor.cs` - Static class caching reflection members for CardPoolHolder
- `GeneralMenuNavigator.cs` - Rewritten `FindPoolHolderCards()`, `ActivateCollectionPageButton()`, page rescan mechanism
- `GroupedNavigator.cs` - Removed label-based filter fields and methods
- `CardDetector.cs` - Added `CardPoolAccessor.ClearCache()` in cache clearing
- `BaseNavigator.cs` - Reduced `_pendingSearchRescanFrames` from 30 to 12

---

## v0.4.4 - 2026-02-05

### Deck Builder Search Field Support
- Search input field in deck builder Filters group now triggers collection rescan after exiting
- Type search term, press Tab to navigate to Collection with filtered results
- Announces "Search results: X cards" after filter applies
- Uses delayed rescan (~500ms) to allow game's filter animation to complete

### Technical
- `ExitInputFieldEditMode()` detects search fields by name and schedules delayed rescan
- `_suppressNavigationAnnouncement` flag prevents announcing stale cards before rescan
- `ForceRescanAfterSearch()` override in GeneralMenuNavigator counts only Collection group items
- `onEndEdit` event invoked when deactivating input fields to trigger game callbacks

---

## v0.4.3 - 2026-02-05

### Booster Chamber (Packs Screen) Overhaul
- Pack carousel now treated as single navigable element with Left/Right arrow navigation
- Packs announced as "PackName (count), X of Y, use left and right arrows"
- Each pack plays its own ambient music when centered (proper music switching via PointerExit)
- Wildcard vault progress bars now visible as standalone elements
- Disabled grouped navigation for BoosterChamber (flat list is more appropriate)

### Pack Set Names
- Added set code extraction from `SealedBoosterView.SetCode` property
- Pack names now show actual set names (e.g., "Aetherdrift (3)") instead of generic labels
- Set code to name mapping for known sets (Foundations, Aetherdrift, Duskmourn, etc.)

### Technical
- Added `SimulatePointerExit()` to UIActivator for proper element deselection
- Booster carousel state tracking (`_boosterPackHitboxes`, `_boosterCarouselIndex`)
- `HandleBoosterCarouselNavigation()` handles Left/Right with music switching
- `HandleCarouselArrow()` override routes to booster carousel when appropriate

---

## v0.4.2 - 2026-02-04

### New Navigator: RewardPopupNavigator
- Created dedicated `RewardPopupNavigator` for rewards popup handling (mail claims, store purchases)
- Priority 86 - preempts GeneralMenuNavigator when rewards popup appears
- Automatic rescan mechanism handles delayed reward content loading
- Full navigation support for cards, packs, currency, card sleeves

### NavigatorManager Preemption
- Added preemption support in `NavigatorManager.Update()`
- Higher-priority navigators can now take over from active lower-priority ones
- Enables overlays/popups to properly intercept navigation

### Code Cleanup
- Removed duplicate rewards code from `GeneralMenuNavigator`
- Removed `GetRewardsContainer()` from `OverlayDetector` (now in RewardPopupNavigator)
- Updated documentation with lessons learned about extracting navigators

### Documentation
- Updated MOD_STRUCTURE.md with RewardPopupNavigator
- Updated SCREENS.md Rewards Popup section
- Added "Extracting Navigators from Existing Code" section to BEST_PRACTICES.md

---

## v0.4.1 - 2026-02-03

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
- Complete card info: name, mana cost, type, P/T, rules text, flavor text, rarity, artist
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
  - Dropdown suppression handled by DropdownStateManager
  - See Technical Debt section in KNOWN_ISSUES.md for details

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
- `UIFocusTracker.NavigatorHandlesAnnouncements` prevents duplicate announcements (set from NavigatorManager.HasActiveNavigator)
- `DropdownStateManager.SuppressReentry()` prevents dropdown mode re-entry after auto-close
- `BaseNavigator.HandleDropdownNavigation()` intercepts Enter (silent select) and Escape/Backspace (close)
- `BaseNavigator.SelectDropdownItem()` sets value via reflection without triggering `onValueChanged`
- `CloseActiveDropdown()` finds parent TMP_Dropdown/Dropdown/cTMP_Dropdown and calls `Hide()`
- Enter/Submit blocked from game in dropdown mode via `EventSystemPatch` and `KeyboardManagerPatch`
- `GetElementAnnouncement()` now handles legacy InputField and tries textComponent fallback
- Tab handling uses `InputManager.GetKeyDownAndConsume(KeyCode.Tab)` to block game processing
- `GeneralMenuNavigator.DiscoverElements()` disables grouped navigation when `_currentScene == "Login"`

**Files:** `BaseNavigator.cs`, `UIFocusTracker.cs`, `DropdownStateManager.cs`, `EventSystemPatch.cs`, `KeyboardManagerPatch.cs`, `GeneralMenuNavigator.cs`, `UITextExtractor.cs`, `SCREENS.md`

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
