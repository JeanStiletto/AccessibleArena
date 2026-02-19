# MTGA Screen Reference

Quick reference for special screens requiring custom navigation.

## Global Overlays

### Settings Menu
**Navigator:** `SettingsMenuNavigator`
**Priority:** 90 (highest - works in all scenes including duels)

Settings panel accessible from any screen via NavBar settings button or in-duel menu.

**Submenus:**
- Content - MainMenu
- Content - Gameplay
- Content - Graphics
- Content - Audio

**Navigation:**
- Up/Down arrows: Navigate settings items
- Left/Right arrows: Adjust sliders/steppers
- Enter: Activate buttons, toggle checkboxes
- Backspace: Close settings / go back to previous submenu

**Technical Notes:**
- Detects via `PanelStateManager.IsSettingsMenuOpen`
- Works in both Login scene (no content panels) and main game
- Higher priority than DuelNavigator ensures settings work during gameplay

### Help Menu
**Navigator:** `HelpNavigator`
**Trigger:** F1 key (toggle)

Modal overlay that blocks all other input while active. Displays navigable list of all keyboard shortcuts organized by category.

**Navigation:**
- Up/Down arrows (or W/S): Navigate through help items
- Home/End: Jump to first/last item
- Backspace or F1: Close menu

**Categories:**
- Global shortcuts
- Menu navigation
- Zones in duel (combined yours/opponent entries)
- Duel information
- Card navigation in zone
- Card details
- Combat
- Browser (Scry, Surveil, Mulligan)

All strings are localization-ready in `Core/Models/Strings.cs`.

### Modal Overlays (What's New, Announcements)
**Navigator:** `OverlayNavigator`
**Priority:** 85

Handles modal popups that appear over other content, blocking interaction until dismissed.

**Detected Overlay Types:**
- What's New carousel (game updates)
- Announcements

Note: Reward popups are handled by `RewardPopupNavigator` (see below).

**Detection:** Looks for `Background_ClickBlocker` GameObject

**Navigation:**
- Left/Right arrows: Navigate carousel pages (What's New)
- Enter: Activate buttons
- Backspace: Dismiss overlay

## Login Flow

All Login scene panels are handled by `GeneralMenuNavigator` with automatic panel detection.

### WelcomeGate Screen
**Panel:** `Panel - WelcomeGate_Desktop_16x9(Clone)`
**Navigator:** `GeneralMenuNavigator` (WelcomeGateNavigator deprecated January 2026)

Elements discovered automatically:
- Settings button
- Register button (CustomButton)
- Login button (CustomButton)
- Help button

### Login Panel
**Panel:** `Panel - Log In_Desktop_16x9(Clone)`
**Navigator:** `GeneralMenuNavigator` (was LoginPanelNavigator, deprecated January 2026)

Elements discovered automatically:
- Settings button (icon, labeled from parent name)
- Email input field (TMP_InputField)
- Password input field (TMP_InputField with password masking)
- Remember me toggle (labeled from parent "Toggle - Remember Me")
- Log In button (CustomButton)
- Privacy links

### Registration Panel
**Panel:** `Panel - Register_Desktop_16x9(Clone)` (and related panels)
**Navigator:** `GeneralMenuNavigator`

Elements discovered automatically:
- Birth month dropdown (cTMP_Dropdown)
- Birth day dropdown (cTMP_Dropdown)
- Birth year dropdown (cTMP_Dropdown)
- Country dropdown (cTMP_Dropdown)
- Experience dropdown (cTMP_Dropdown)
- Language dropdown (TMP_Dropdown)
- Various buttons

**Note:** Registration has auto-advance behavior where selecting a dropdown value automatically opens the next dropdown. See KNOWN_ISSUES.md.

### Login Panel Detection
Login scene panels are detected by `MenuPanelTracker.DetectLoginPanels()` which looks for active GameObjects matching patterns like "Panel - WelcomeGate", "Panel - Log In", "Panel - Register", etc. under the PanelParent container.

### Input Field Navigation
- **Tab** navigates to input fields and auto-enters edit mode (traditional behavior)
- **Arrow keys** navigate to input fields but do NOT auto-enter edit mode (press Enter to edit)
- While editing: type normally, Arrow Up/Down reads content, Arrow Left/Right reads character at cursor
- Press **Escape** to stop editing and return to navigation (announces "Exited edit mode")
- Press **Tab** to stop editing and move to next element

Note: MTGA auto-focuses input fields when navigated to. Tab navigation allows this auto-focus, while arrow navigation deactivates it for dropdown-like behavior.

### Dropdown Navigation
Dropdowns (TMP_Dropdown and cTMP_Dropdown) are detected and classified automatically.

**Dropdown edit mode** is tracked by observing focus state:
- When focus is on a dropdown item ("Item X: ..."), the mod blocks its own navigation and lets Unity handle arrow keys
- When focus leaves dropdown items, normal navigation resumes
- This handles both manual dropdowns (Enter to open, Enter to select) and auto-advancing dropdowns

**Closing dropdowns:**
- Press **Escape** or **Backspace** to close a dropdown without navigating back
- Press **Enter** on an item to select it and close the dropdown

**Password Masking:**
Password fields announce "has X characters" instead of actual content for privacy.

**Tab Navigation Fallback:**
If Unity's Tab navigation gets stuck (broken selectOnDown links), UIFocusTracker provides fallback navigation to next Selectable.

**Known Issue:**
Back button (Button_Back) does not respond to keyboard activation. See KNOWN_ISSUES.md.

### Code of Conduct
**Navigator:** Default navigation (CodeOfConductNavigator deprecated January 2026)

Terms/consent checkboxes screen. Unity's native Tab navigation works correctly here.

## Booster Chamber (Packs Screen)

### Pack Selection Screen
**Controller:** `ContentController - BoosterChamber_v2_Desktop_16x9(Clone)`
**Navigator:** `GeneralMenuNavigator`

The Booster Chamber screen displays available booster packs in a horizontal carousel with wildcard vault progress indicators.

**Elements (flat navigation, no groups):**
- Wildcard progress bars (`ObjectiveGraphics` in `WildcardProgressUncommon`/`Wildcard Progress Rare`) - Shows vault fill percentage
- Open All button (`Button_OpenMultiple`) - Opens all packs at once
- Pack carousel (single element) - All packs combined into one navigable carousel element

**Pack Carousel:**
The pack carousel is a special element that combines all pack hitboxes into a single navigable unit:
- Announced as "PackName (count), X of Y, use left and right arrows"
- Left/Right arrows navigate between packs (centers the selected pack)
- Each pack has its own ambient music that plays when centered
- Enter opens the currently centered pack

**Navigation:**
- Up/Down arrows: Navigate between wildcard progress, Open All button, and pack carousel
- Left/Right arrows (on carousel): Navigate between packs
- Enter: Activate current element (open pack when on carousel)

**Technical Notes:**
- Grouped navigation is disabled for BoosterChamber (flat list navigation)
- Pack hitboxes (`Hitbox_BoosterMesh`) are collected into `_boosterPackHitboxes` list
- Single carousel element created with `CarouselInfo.HasArrowNavigation = true`
- `SimulatePointerExit()` sent to old pack before clicking new pack (stops music overlap)
- Pack names extracted via `UITextExtractor.TryGetBoosterPackName()` using `SealedBoosterView.SetCode`
- Set codes mapped to friendly names (e.g., "ACR" → "Aetherdrift")

### Pack Contents Screen
**Navigator:** `BoosterOpenNavigator`
**Priority:** 80

After clicking a pack, displays the cards you received in a scrollable list.

**Detection:**
- BoosterChamber controller active
- CardScroller element visible
- RevealAll button present

**Elements Detected:**
- Individual cards from opened pack
- "Reveal All" button
- Continue/Done button

**Navigation:**
- Left/Right arrows: Navigate between cards
- Up/Down arrows: Read card details
- Enter: Reveal card / Continue
- Home/End: Jump to first/last card

**Technical Notes:**
- Detects `BoosterOpenToScrollListController` pattern
- Cards are navigable via standard card detection
- "Reveal All" speeds up card reveal animation

## Deck Management

### Deck Management Screen
**Controller:** `DeckManagerController`
**Navigator:** `GeneralMenuNavigator`

The Deck Management screen shows all decks organized into folders (My Decks, Starter Decks, Brawl Sample Decks).

**Groups:**
- `New Deck` - Create a new deck (standalone)
- `Alle Decks` - Format filter dropdown (standalone)
- `Import Deck` - Import deck from clipboard (standalone)
- `Sammlung` - Open collection (standalone)
- `Filters` - Color checkboxes, search, sort order
- Folder groups (My Decks, Starter Decks, Brawl Sample Decks) - Expandable deck lists

**Navigation:**
- Arrow Up/Down: Navigate between groups
- Enter on folder: Open folder to browse decks inside
- Enter on deck: Select the deck (clicks it)
- Right Arrow on deck: Open actions menu (Rename, Edit, Details, Favorite, Clone, Export, Delete)
- Backspace: Exit folder or go back

**Deck Actions (Right Arrow menu):**
Deck-specific toolbar buttons (Edit, Delete, Export, Favorite, Clone, Details) are hidden from top-level navigation since they require a deck to be selected. They are accessible via the Right Arrow actions menu on each deck entry.

**Technical Notes:**
- Deck-specific buttons live in `DeckManager_Desktop_16x9(Clone)/SafeArea/MainButtons/`
- Standalone buttons (Import, Sammlung/Collection) are whitelisted and kept in top-level navigation
- All other MainButtons children are filtered from navigation and attached as actions on deck entries
- Deck entries are paired with their TextBox rename buttons for the Rename action

## Deck Builder

### Deck Builder Screen
**Controller:** `WrapperDeckBuilder`
**Navigator:** `GeneralMenuNavigator`

The Deck Builder screen allows editing deck contents with access to the card collection.

**Elements Detected:**
- Collection cards in `PoolHolder` - Cards available to add to deck (grid view)
- Deck list cards in `MainDeck_MetaCardHolder` - Cards currently in deck (compact list view)
- Filter controls (color checkboxes, type filters, search)
- "Fertig" (Done) button

**Groups:**
- `DeckBuilderCollection` - Collection card grid
- `DeckBuilderDeckList` - Deck list cards (compact list with quantities)
- `DeckBuilderInfo` - Deck statistics (card count, types, mana curve) with 2D sub-navigation
- `Filters` - Color checkboxes, type filters, advanced filters
- `Content` - Header controls (Sideboard, deck name, etc.)

**Navigation:**
- Arrow Up/Down: Navigate between groups and elements
- Tab/Shift+Tab: Cycle between groups (Collection, Deck List, Deck Info, Filters) and auto-enter
- Enter on group: Enter the group to navigate individual items
- Backspace: Exit current group, return to group list

**Collection Card Navigation (DeckBuilderCollection):**
- Left/Right arrows: Navigate between cards in collection grid
- Up/Down arrows: Read card details (name, mana cost, type, rules text, etc.)
- Enter: Add one copy of the card to deck (invokes OnAddClicked action)
- Home/End: Jump to first/last card
- Page Up/Down: Navigate collection pages via CardPoolAccessor (announces "Page X of Y")

**Deck List Navigation (DeckBuilderDeckList):**
- Left/Right arrows: Navigate between cards in deck list
- Up/Down arrows: Read card details (shows Quantity after Name)
- Enter: Remove one copy of the card from deck (click event removes one copy)
- Home/End: Jump to first/last card

**Deck Info Navigation (DeckBuilderInfo):**
Tab-cyclable group providing live deck statistics with 2D sub-navigation.
Two rows navigable with Up/Down, individual entries within each row navigable with Left/Right.

- Row 1 (Cards): Card count (e.g., "35 von 60"), then type entries (e.g., "20 Creatures (56%)", "12 Others (33%)", "24 Lands (11%)"). Zero-count types are omitted.
- Row 2 (Mana Curve): CMC buckets ("1 or less: 4", "2: 8", ..., "6 or more: 2") and "Average: 3.5"

Navigation within Deck Info:
- Up/Down arrows: Switch between rows (announces row name + first entry)
- Left/Right arrows: Navigate individual entries within the current row
- Home/End: Jump to first/last entry in current row
- Enter: Refresh all data from game UI and re-announce current entry
- Tab/Shift+Tab: Cycle to other deck builder groups (Collection, Deck List, Filters)
- Backspace: Exit Deck Info back to group level

Card count is also announced automatically whenever a card is added to or removed from the deck.

**Card Add/Remove Behavior:**
- Adding a card from collection increases its quantity in deck (or adds new entry)
- Removing a card from deck decreases its quantity (or removes entry when qty reaches 0)
- After add/remove, UI rescans to update both collection and deck list
- Card count (e.g., "35 von 60") is announced after each add/remove
- Position is preserved within the current group (stays on same card index or nearest valid)

**Card Info Reading:**
When focused on a card, Up/Down arrows cycle through card information blocks:
- Name
- Quantity (deck list cards only - shows "X, missing" for unowned copies)
- Collection (collection cards only - "Owned X" or "Owned X, In Deck Y")
- Mana Cost
- Type
- Power/Toughness (if creature)
- Rules Text
- Flavor Text
- Artist

**Technical Notes:**
- Collection cards use `PagesMetaCardView` with Model-based detection + `_lastDisplayInfo` for owned/used quantities
- Deck list cards use `ListMetaCardView_Expanding` with GrpId-based lookup via `CardDataProvider`
- Deck list unowned detection via `MetaCardView.ShowUnCollectedTreatment` field (set by `SetDisplayInformation`)
- Quantity buttons (`CustomButton - Tag` showing "4x", "2x") are filtered to Unknown group
- Deck header controls (Sideboard toggle, deck name field) are in Content group
- Tab cycling skips standalone elements, only cycles between actual groups
- `DeckInfoProvider` reads deck statistics via reflection on `DeckCostsDetails` and `DeckMainTitlePanel`
- Deck data populated via `Pantry.Get<DeckBuilderModelProvider>().Model.GetFilteredMainDeck()` reflection chain
- `DeckBuilderInfo` group uses virtual elements (GameObject=null) with 2D sub-navigation (see element-grouping-feature.md)
- `CardPoolAccessor` accesses game's `CardPoolHolder` via reflection for direct page control
- `_pages[1].CardViews` returns only current visible page's cards (no offscreen contamination)
- `ScrollNext()` / `ScrollPrevious()` navigate pages directly instead of searching for UI buttons
- Page boundary: announces "First page" / "Last page" at edges, "Page X of Y" on navigation
- Fallback to hierarchy-based card scan if CardPoolHolder component not found

**Card Activation Implementation:**
- Collection cards (`PagesMetaCardView`): Bypasses CardInfoNavigator on Enter, invokes `OnAddClicked` action via reflection
- Deck list cards (`CustomButton - Tile`): Uses pointer simulation only (not both pointer + onClick to avoid double removal)
- After activation, triggers UI rescan via `OnDeckBuilderCardActivated()` callback
- `GroupedNavigator.SaveCurrentGroupForRestore()` preserves group AND element index within group
- Position restoration clamps to valid range if group shrunk (e.g., last card removed)

**MainDeck_MetaCardHolder Activation:**
The `MainDeck_MetaCardHolder` GameObject (which contains deck list cards) may be inactive when entering the deck builder without a popup dialog appearing first. `GameObject.Find()` only finds active objects, so the holder would not be found.

The fix in `CardModelProvider.GetDeckListCards()`:
1. First tries `GameObject.Find("MainDeck_MetaCardHolder")` (fast, but only finds active objects)
2. If not found, searches ALL transforms including inactive ones via `FindObjectsOfType<Transform>(true)`
3. If found but inactive, activates it with `SetActive(true)`
4. Then proceeds to extract deck card data from the holder's components

This ensures deck list cards are always accessible regardless of the holder's initial active state.

**Known Limitations:**
- Quantity buttons may still appear in navigation (filter not fully working)
- Sideboard management not yet implemented

## NPE Screens

### Reward Chest Screen
**Container:** `NPE-Rewards_Container`
**Navigator:** `GeneralMenuNavigator`

Elements:
- `NPE_RewardChest` - Chest (needs controller reflection)
- `Deckbox_A` through `Deckbox_E` - Deck boxes (need controller reflection)
- `Hitbox_LidOpen` - Continue button (standard activation)

Special handling: Chest and deck boxes require `NPEContentControllerRewards` methods:
- Chest: `Coroutine_UnlockAnimation()` + `AwardAllKeys()`
- Deck box: `set_AutoFlipping(true)` + `OnClaimClicked_Unity()`

Note: `NPEMetaDeckView.Model` is null - no deck data available. These are placeholder boxes.

### Card Reveal Screen
Handled by `GeneralMenuNavigator`.

Cards detected via CardDetector. Enter on card activates CardInfoNavigator for detail browsing.

## Screen Detection

Navigators check for their screens in `TryActivate()`:
```csharp
var panel = GameObject.Find("Panel - Name(Clone)");
if (panel == null || !panel.activeInHierarchy)
    return false;
```

Only one navigator can be active. UIFocusTracker runs as fallback when no navigator is active.

## Transitional Screens

### Game Loading Screen (Startup)
**Navigator:** `LoadingScreenNavigator` (ScreenMode.GameLoading)
**Priority:** 65
**Scene:** `AssetPrep` (active scene during game startup/login)

The loading screen shown during game startup while connecting to servers. Displays status messages like "Starting..." or "Waiting for server...".

**Elements Detected:**
- InfoText status message from `AssetPrepScreen` component (dynamic, changes during loading)

**Navigation:**
- No interactive elements - status is read-only
- Status text changes are announced automatically as they occur

**Technical Notes:**
- Detects via `SceneManager.GetActiveScene().name == "AssetPrep"`
- Finds InfoText via reflection on `AssetPrepScreen.InfoText` field (TMP_Text)
- Caches TMP_Text reference across poll cycles (avoids repeated FindObjectsOfType)
- Polls every 0.5s with no timeout (keeps polling until scene changes)
- Announces text changes via AnnounceInterrupt when status text updates
- Rich text tags cleaned via regex before announcement
- Higher priority (65) than AssetPrepNavigator (5), handles normal loading
- AssetPrepNavigator only relevant for fresh-install download screens with active buttons
- Deactivates on scene change (e.g., MainNavigation after login)

### Server Loading Overlay (Main Menu)
**Navigator:** `GeneralMenuNavigator` (not a separate navigator)

When the game's loading panel overlay is active after scene transition (e.g., logging in, reconnecting), GeneralMenuNavigator announces "Waiting for server" once and defers activation until the overlay clears. Uses reflection on `MTGA.LoadingPanelShowing.IsShowing` static property.

### Match End Screen (Victory/Defeat)
**Navigator:** `LoadingScreenNavigator` (ScreenMode.MatchEnd)
**Priority:** 65
**Scene:** `MatchEndScene` (loaded additively after duel)

The victory/defeat screen shown after a duel ends. UI loads late (after animations).

**Elements Detected:**
- Match result text (Victory/Defeat/Draw keywords in TMP_Text)
- Rank info (Text_RankFormat + Text_Rank combined, e.g. "Constructed-Rang: Silber Stufe 4")
- View Battlefield button (`ViewBattlefieldButton`)
- ExitMatchOverlayButton (Continue - starts INACTIVE, appears after animation)
- Nav_Settings (global, from NavBar scene)

**Navigation:**
- Up/Down arrows: Navigate elements
- Enter: Activate buttons
- Backspace: Continue (clicks ExitMatchOverlayButton or simulates screen center click)
- F3: Announce current screen

**Technical Notes:**
- Scene-scoped search via `scene.GetRootGameObjects()` to avoid duel leftover elements
- CanvasGroup filtering (`alpha <= 0 || !interactable`) for invisible duel buttons
- Uses EventTrigger-based buttons (not Button/Selectable) - activated via `SimulatePointerClick`
- 0-element activation pattern with polling (UI loads after scene, not with it)
- Polls every 0.5s for up to 10s for late-loading elements
- MatchEndScene added to GeneralMenuNavigator's ExcludedScenes
- CardInfoNavigator deactivated on scene change to prevent stale card reading

### PreGame Screen (Matchmaking Queue)
**Navigator:** `LoadingScreenNavigator` (ScreenMode.PreGame)
**Priority:** 65
**Scene:** `PreGameScene` (loaded between clicking Play and DuelScene)

The VS/matchmaking screen shown while waiting for an opponent. Contains timer, gameplay hints, and cancel button.

**Elements Detected:**
- TipsLabel (cycling flavor text/gameplay hints via CyclingTipsView)
- Timer (text_queue_detail + text_timer combined, e.g. "Aktuelle Wartezeit: 0:05")
- text_MatchFound ("Ready!" when opponent found)
- Button_Cancel (CustomButton, becomes active after initial animation)
- Nav_Settings (global)

**Navigation:**
- Up/Down arrows: Navigate elements
- Enter: Activate buttons (Cancel, Settings)
- Backspace: Cancel matchmaking

**Technical Notes:**
- Scene-scoped search within PreGameScene root objects
- Targeted element discovery by name (not generic TMP_Text sweep)
- Timer label updates on each poll cycle (0.5s) without re-announcing
- Filters placeholder text ("Description description...") from initial load
- Filters player name text (text_playerName, text_playerDetails) as not useful
- TipsLabelSpecial filtered (duplicate of TipsLabel)
- Polling continues for entire PreGame duration (no timeout) for timer updates
- Navigation position preserved across poll cycles
- PreGameScene added to GeneralMenuNavigator's ExcludedScenes
- ~14-18 seconds typical wait time before DuelScene loads

## DuelScene

After the PreGame screen, the game transitions to active gameplay.

### Duel Gameplay
**Navigator:** `DuelNavigator` + `ZoneNavigator`

Active gameplay with zones and cards.

**Zone Navigation (via ZoneNavigator):**
- C - Your hand (Cards)
- G - Your graveyard
- X - Your exile
- S - Stack
- Shift+G - Opponent graveyard
- Shift+X - Opponent exile

**Battlefield Navigation (via BattlefieldNavigator):**
- B - Your creatures
- A - Your lands
- R - Your non-creatures (artifacts, enchantments, planeswalkers)
- Shift+B - Opponent creatures
- Shift+A - Opponent lands
- Shift+R - Opponent non-creatures
- Shift+Up/Down - Switch between battlefield rows

**Info Shortcuts:**
- T - Announce turn number and active player
- L - Announce life totals
- V - Enter player info zone (portrait navigation)
- D - Your library count
- Shift+D - Opponent library count
- Shift+C - Opponent hand count

**Card Navigation:**
- Left/Right arrows - Move between cards in current zone/row
- Up/Down arrows - Read card details (via CardInfoNavigator)
- Home/End - Jump to first/last card in zone

**Zone Holders (GameObjects):**
- `LocalHand_Desktop_16x9` - Your hand
- `BattlefieldCardHolder` - Battlefield (both players)
- `LocalGraveyard` / `OpponentGraveyard` - Graveyards
- `ExileCardHolder` - Exile zone
- `StackCardHolder_Desktop_16x9` - Stack
- `LocalLibrary` / `OpponentLibrary` - Libraries

**Card Detection:**
Cards are children of zone holders with names like `CDC #39` (Card Display Controller).
Components: `CDCMetaCardView`, `CardView`, `DuelCardView`

**UI Elements:**
- `PromptButton_Primary` - Main action (End Turn, Main, Attack, etc.)
- `PromptButton_Secondary` - Secondary action
- `Button` - Unlabeled button (timer-related)
- Stop EventTriggers - Timer controls (filtered out)

**EventSystem Conflict:**
Arrow keys trigger Unity's built-in navigation, cycling focus between UI buttons.
Fix: Clear `EventSystem.currentSelectedGameObject` before handling arrows.

Detection: Activates when `PromptButton_Primary` shows duel-related text
(End, Main, Pass, Resolve, Combat, Attack, Block, Done) or Stop EventTriggers exist.

### Duel Sub-Navigators

DuelNavigator delegates to specialized sub-navigators for different game phases:

**HotHighlightNavigator** (Unified Tab Navigation)
- Handles Tab cycling through ALL highlighted cards (playable cards AND targets)
- Trusts game's HotHighlight system - no separate mode tracking needed
- Tab syncs with zone/battlefield navigators so Left/Right works correctly after Tab
- Zone change on Tab announces: "Hand, Lightning Bolt, 1 of 3" (same format as zone shortcuts)
- Zone-based activation: hand cards use two-click, others use single-click
- Tab/Shift+Tab cycles all targets, Ctrl+Tab/Ctrl+Shift+Tab cycles opponent targets only
- Enter activates, Backspace cancels

**Selection Mode (in HotHighlightNavigator)**
- Detects Submit button with count
- Hand cards use single-click to toggle selection instead of two-click to play
- Works with both Tab navigation and zone shortcuts (C + Left/Right + Enter)
- Announces game's prompt instruction on entry (e.g. "Discard a card") via PromptText element
- Shows selected state when navigating hand via zone shortcuts
- Announces X cards selected after toggling
- Space submits selection (clicks primary button) even with items highlighted
- Tab index preserved after toggle so next Tab advances instead of resetting

**CombatNavigator**
- Handles declare attackers/blockers phases
- Space triggers attack/block actions, Backspace for no attacks/blocks
- Announces combat state for creatures (attacking, blocking, can block)

**BrowserNavigator**
- Handles library manipulation (scry, surveil, mulligan)
- Tab cycles through cards, Space confirms
- Detects via `BrowserScaffold_*` GameObjects

**PlayerPortraitNavigator**
- V key enters player info zone
- Left/Right switches players, Up/Down cycles properties (Life, Timer, Timeouts, Wins, Rank)
- Rank read from GameManager.MatchManager player info via reflection (e.g., "Gold Tier 2", "Mythic #1234")
- Enter opens emote wheel (your portrait only) via PortraitButton click on DuelScene_AvatarView
- Emotes discovered from EmoteView children in EmoteOptionsPanel, navigated with Up/Down, sent with Enter

**ManaColorPickerNavigator**
- Detects ManaColorSelector popup (any-color mana sources like Ilysian Caryatid)
- Tab/Right = next color, Shift+Tab/Left = previous, Home/End = jump
- Enter selects focused color, number keys 1-6 for direct selection
- Backspace cancels
- Multi-pick: re-announces after each selection
- Detects via `ManaColorSelector.IsOpen` property (reflection, 100ms poll)

**Priority Order:**
ManaColorPickerNavigator > BrowserNavigator > CombatNavigator > HotHighlightNavigator > PortraitNavigator > BattlefieldNavigator > ZoneNavigator

## Mailbox Screen

**Navigator:** `GeneralMenuNavigator`
**Trigger:** Click Nav_Mail button in NavBar

The Mailbox screen shows inbox messages with rewards. Uses two-level navigation:
- **Mail List** (left pane): List of mail items
- **Mail Content** (right pane): Opened mail details with navigable fields and buttons

**Navigation - Mail List:**
- Up/Down arrows: Navigate between mail items
- Enter: Open selected mail (switches to content view)
- Backspace: Close mailbox and return to Home

**Navigation - Mail Content:**
- Up/Down arrows: First cycles through mail fields (Title, Date, Body), then buttons
- Enter: On field - reads full content; On button - activates it (Claim rewards, More Info)
- Backspace: Close mail and return to mail list

**Mail Field Navigation:**
When a mail is opened, Up/Down arrows cycle through available fields:
1. Title - Mail subject
2. Date - When the mail was sent (if available)
3. Body - Full mail content
4. Buttons - Action buttons like "More Info", "Claim"

Fields without content are skipped. Pressing Down past the last field transitions to button navigation.

**Technical Notes:**
- Two overlay groups: `ElementGroup.MailboxList` and `ElementGroup.MailboxContent`
- `IsMailContentVisible()` detects when a mail is opened (checks for content buttons, ignores Viewport)
- `IsInsideMailboxList()` filters elements in BladeView_CONTAINER (mail list)
- `IsInsideMailboxContent()` filters elements in Mailbox_ContentView (mail details)
- Mail items get titles via `UITextExtractor.TryGetMailboxItemTitle()`
- Mail content fields extracted via `UITextExtractor.GetMailContentParts()`
- Nav_Mail button requires special activation via `NavBarController.MailboxButton_OnClick()`
- Harmony patch on `ContentControllerPlayerInbox.OnLetterSelected()` detects mail selection
- Buttons without actual text content (only object name) are filtered out

## Rewards Popup

**Controller:** `ContentControllerRewards`
**Navigator:** `RewardPopupNavigator` (NEW - February 2026)
**Priority:** 86 (preempts GeneralMenuNavigator)
**Path:** `Canvas - Screenspace Popups/ContentController - Rewards_Desktop_16x9(Clone)`

The Rewards Popup appears after claiming rewards from mail, store purchases, or other reward sources. It displays the actual rewards being granted (cards, card sleeves, gold, packs, etc.).

**Navigation:**
- Left/Right arrows: Navigate through reward items (flat navigation, no groups)
- Up/Down arrows: Read card details via CardInfoNavigator (when on a card reward)
- Enter: Activate reward item buttons (e.g., "Standard festlegen" / "Set as default")
- Backspace: Click through / dismiss the popup (progress to next screen)

**Elements:**
- `RewardPrefab_Pack` - Booster pack rewards with quantity
- `RewardPrefab_IndividualCard_Base` - Individual card rewards (navigable with card info)
- `RewardPrefab_CardSleeve_Base` - Card sleeve rewards with "Standard festlegen" button
- `ClaimButton` - "Mehr" (More) / "Nehmen" (Take) button to reveal additional rewards or claim them
- `Background_ClickBlocker` - Click-to-progress background (Continue button)

**Multi-Page Rewards:**
Some reward screens have multiple pages of rewards. The "Mehr" (More) button:
1. First presses reveal additional rewards or claim them
2. Final press closes the popup via Background_ClickBlocker
3. Each press triggers a rescan to discover newly visible rewards

**Technical Notes:**
- **Dedicated navigator** - `RewardPopupNavigator` handles all rewards popup functionality
- **Preemption** - Higher priority (86) preempts GeneralMenuNavigator (15) when rewards popup appears
- **Timing resilience** - Automatic rescan mechanism handles delayed reward loading (up to 10 retries)
- Detected via `CheckRewardsPopupOpenInternal()` - checks for active ContentController with "Rewards" in name
- Searches entire popup for `RewardPrefab_*` elements (not just RewardsCONTAINER)
- `FindCardObjectInReward()` locates card components (BoosterMetaCardView, PagesMetaCardView, etc.)
- Card rewards are recognized by `CardDetector.IsCard()` enabling CardInfoNavigator integration
- `DismissRewardsPopup()` clicks the `Background_ClickBlocker` as fallback dismiss method
- `OverlayDetector.IsRewardsPopupOpen()` still used for overlay filtering (IsInsideActiveOverlay)

## Rewards/Mastery Screen

**Controller:** `ProgressionTracksContentController`
**Navigator:** `MasteryNavigator` (standalone, not GeneralMenuNavigator)
**Priority:** 60 (preempts GeneralMenuNavigator)

The Mastery screen shows battle pass progression, level rewards, and XP progress.

**Navigation Model:**
The list begins with a virtual **Status item** (position 0) containing XP info and action buttons, followed by all mastery levels.

**Status Item (position 0):**
- Default announcement: current level and XP progress (e.g., "Level 15 of 80, 250/1000 XP")
- Left/Right arrows: Cycle through XP status and action buttons (Spend Orbs, Previous Season, Purchase, Back)
- Enter on button tier: Activates the button
- Enter on XP tier: Reads detailed status info

**Level Navigation:**
- Up/Down arrows (or W/S, Tab/Shift+Tab): Navigate levels
- Left/Right arrows (or A/D): Cycle reward tiers within level (Free, Premium, Renewal)
- Home/End: Jump to first (status item) / last level
- PageUp/PageDown: Jump ~10 levels
- Enter: Announce detailed level info (all tiers, XP, status)
- Backspace: Return to Home

**Announcements:**
- Activation: Track title, current level, XP progress
- Level navigation: "X of Y: Level N: reward. status" (completed / current level)
- Tier cycling: "Free: reward" / "Premium: reward, tier X of Y"
- Detail (Enter): All tiers with quantities, XP if current level

**Page Sync:**
Visual page automatically syncs when navigating past page boundaries. Announces "Page X of Y" on page change.

**Popup Handling:**
Subscribes to `PanelStateManager.OnPanelChanged` for popup detection (same pattern as StoreNavigator). Filters benign overlays: ObjectivePopup, FullscreenZFBrowser, RewardPopup3DIcon.

**Technical Notes:**
- Detected via `ProgressionTracksContentController` MonoBehaviour with `IsOpen` property
- Data provider accessed via `RewardTrackView._masteryPassProvider` (computed property: `Pantry.Get<SetMasteryDataProvider>()`)
- Current level determined by `SetMasteryDataProvider.GetCurrentLevelIndex(trackName)` - returns the Index of the in-progress level
- Level completion: levels with Index < curLevelIndex are completed
- Reward names resolved via `MTGALocalizedString.ToString()` (auto-resolves loc key + parameters)
- Page sync via `RewardTrackView.CurrentPage` property setter (triggers game page animation)
- `PageLevels` nested class on `RewardTrackView` maps level Index values to pages
- Action buttons discovered via reflection: `_masteryTreeButton`, `_previousTreeButton`, `_purchaseButton`, `_backButton`
- Button labels extracted from child `TMP_Text` components with rich text tag cleaning
- GeneralMenuNavigator suppressed when `ProgressionTracksContentController` is active

### PrizeWall Mode (Mastery Tree / Spend Spheres)

**Controller:** `ContentController_PrizeWall`
**Mode:** `MasteryMode.PrizeWall` (same MasteryNavigator, different mode)

When the user activates the "Mastery Tree" button from the mastery levels screen, the game closes `ProgressionTracksContentController` and opens `ContentController_PrizeWall` (the sphere-spending screen). MasteryNavigator detects this and reactivates in PrizeWall mode.

**Navigation Model:**
- Virtual **Sphere Status item** at position 0: announces available sphere count
- Remaining items: purchasable cosmetics from the PrizeWall layout group

**Controls:**
- Up/Down (or W/S, Tab/Shift+Tab): Navigate items
- Home/End: Jump to first/last item
- Enter: Activate selected item (opens purchase confirmation popup)
- Backspace: Return to mastery levels screen
- F3/Ctrl+R: Re-announce current position with sphere count

**Announcements:**
- Activation: "Prize Wall. N items. X spheres available. Arrow keys to navigate."
- Item navigation: "X of Y: ItemName, N spheres"
- Sphere status (position 0): "X spheres available"

**Confirmation Popup:**
- Triggered by Enter on a purchasable item
- Announces popup body text + available options
- Up/Down to navigate options, Enter to confirm
- Synthetic Cancel option appended (modal has no built-in cancel button)
- Dismiss via `StoreConfirmationModal.Close()` reflection call

**Technical Notes:**
- Detected via `ContentController_PrizeWall` MonoBehaviour with inherited `IsOpen` property
- Mode transitions happen naturally: controller closes -> MasteryNavigator deactivates -> next frame detects new controller -> reactivates in appropriate mode
- `ConfirmationModal` is reused (not re-instantiated), so polling `activeInHierarchy` is used instead of `PanelStateManager` events
- Item labels extracted from `TMP_Text` children of `StoreItemBase`, combining item name with cost
- Popup element discovery filters out `CustomButton`s that are children of `StoreItemBase` widgets (which get moved into the modal)
- Sphere count read from `PrizeWallCurrency._currencyQuantity` (TextMeshProUGUI)
- Back button accessed via `ContentController_PrizeWall._prizeWallBackButton`

## Store Screen

**Controller:** `ContentController_StoreCarousel`
**Navigator:** `StoreNavigator` (standalone, not GeneralMenuNavigator)
**Priority:** 55 (preempts GeneralMenuNavigator)

The Store screen provides two-level keyboard navigation for browsing and purchasing items.

**Two-Level Navigation:**
- **Tab Level**: Browse store tabs (Featured, Gems, Packs, Daily Deals, Bundles, Cosmetics, Decks, Prize Wall)
- **Item Level**: Browse items within a tab with purchase option cycling

**Navigation - Tab Level:**
- Up/Down arrows (or W/S): Navigate between tabs
- Tab/Shift+Tab: Navigate between tabs
- Enter/Space: Activate tab and enter items
- Home/End: Jump to first/last tab
- Backspace: Leave store (returns to GeneralMenuNavigator)

**Navigation - Item Level:**
- Up/Down arrows (or W/S): Navigate between store items
- Left/Right arrows (or A/D): Cycle purchase options within item (Gems/Gold/Real Money/Token)
- Tab/Shift+Tab: Navigate between items
- Enter/Space: Activate selected purchase option (opens confirmation modal)
- Home/End: Jump to first/last item
- Backspace: Return to tab level

**Announcements:**
- On activation: "Store, {TabName}. {N} items. Navigate with arrows, Enter to buy, Backspace for tabs."
- Tab navigation: "{Index} of {Total}: {TabName}, active" (if currently active tab)
- Item navigation: "{Index} of {Total}: {ItemLabel}, {Price} {Currency}, option {X} of {Y}"
- Purchase option cycling: "{Price} {Currency}, option {X} of {Y}"

**Technical Notes:**
- Standalone navigator because store items (`StoreItemBase`) are not standard CustomButtons
- Accesses `ContentController_StoreCarousel` via reflection for all game state
- Tab discovery via 8 named fields: `_featuredTab` through `_prizeWallTab`
- Item discovery via `GetComponentsInChildren<StoreItemBase>()` on controller
- Purchase options from `PurchaseCostUtils.PurchaseButton` structs: Blue=Gems, Orange=Gold, Clear=Real Money, Green=Token
- Async loading detection: polls `_itemDisplayQueue.Count` after tab switch (0.1s interval)
- Confirmation modal detection: yields (deactivates) when `_confirmationModal.gameObject.activeSelf`
- GeneralMenuNavigator suppressed when `ContentController_StoreCarousel` is active
- Tab activation via `Tab.OnClicked()` reflection call
- Item labels extracted from TMPro text on `_label` OptionalObject, with fallback to child TMP_Text

## Adding New Screens

For implementing accessibility for a new screen, see the "Adding Support for New Screens" section in BEST_PRACTICES.md which covers:
- Content screens (full-page screens like Rewards, Store, Decks)
- Overlay panels (slide-in panels like Mailbox, Friends, Settings)

**Quick steps:**
1. Identify panel name and key elements
2. Test if EventSystem works (log `currentSelectedGameObject` on Tab)
3. If needed, create navigator following existing patterns
4. Register in `AccessibleArenaMod.InitializeServices()` and `OnUpdate()`
5. Document here if screen has special requirements
