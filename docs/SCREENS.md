# MTGA Screen Reference

Quick reference for special screens requiring custom navigation.

## Global Overlay

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

### Pack Opening Screen
**Controller:** `ContentController - BoosterChamber_v2_Desktop_16x9(Clone)`
**Navigator:** `GeneralMenuNavigator`

The Booster Chamber screen displays available booster packs in a horizontal carousel.

**Elements Detected:**
- Pack hitboxes (`Hitbox_BoosterMesh`) - Clickable pack elements, labeled "Open x10 (count)"
- Open All button (`Button_OpenMultiple`) - Opens all packs at once

**Navigation:**
- Left/Right arrows: Navigate between packs
- Enter: Activate selected pack (opens card list)
- Home/End: Jump to first/last pack

**Technical Notes:**
- NavBar is filtered out when BoosterChamber is active
- Pack names (like "Foundations", "Aetherdrift") are rendered as 3D graphics on the pack model, not as UI text - OCR can read them but text extraction cannot
- Pack count is extracted from `Text_Quantity` child element
- "Open x10" label extracted from inactive `Text` child element

**Known Limitations:**
- The card list that appears after clicking a pack is not yet accessible (no panel state change detected)
- Pack set names cannot be extracted - only "Open x10 (count)" is announced

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
- `Filters` - Color checkboxes, type filters, advanced filters
- `Content` - Header controls (Sideboard, deck name, etc.)

**Navigation:**
- Arrow Up/Down: Navigate between groups and elements
- Tab/Shift+Tab: Cycle between groups (Collection, Deck List, Filters) and auto-enter
- Enter on group: Enter the group to navigate individual items
- Backspace: Exit current group, return to group list

**Collection Card Navigation (DeckBuilderCollection):**
- Left/Right arrows: Navigate between cards in collection grid
- Up/Down arrows: Read card details (name, mana cost, type, rules text, etc.)
- Enter: Add one copy of the card to deck (invokes OnAddClicked action)
- Home/End: Jump to first/last card
- Page Up/Down: Navigate collection pages (shows only new cards)

**Deck List Navigation (DeckBuilderDeckList):**
- Left/Right arrows: Navigate between cards in deck list
- Up/Down arrows: Read card details (shows Quantity after Name)
- Enter: Remove one copy of the card from deck (click event removes one copy)
- Home/End: Jump to first/last card

**Card Add/Remove Behavior:**
- Adding a card from collection increases its quantity in deck (or adds new entry)
- Removing a card from deck decreases its quantity (or removes entry when qty reaches 0)
- After add/remove, UI rescans to update both collection and deck list
- Position is preserved within the current group (stays on same card index or nearest valid)

**Card Info Reading:**
When focused on a card, Up/Down arrows cycle through card information blocks:
- Name
- Quantity (deck list cards only)
- Mana Cost
- Type
- Power/Toughness (if creature)
- Rules Text
- Flavor Text
- Artist

**Technical Notes:**
- Collection cards use `PagesMetaCardView` with Model-based detection
- Deck list cards use `ListMetaCardView_Expanding` with GrpId-based lookup via `CardDataProvider`
- Quantity buttons (`CustomButton - Tag` showing "4x", "2x") are filtered to Unknown group
- Deck header controls (Sideboard toggle, deck name field) are in Content group
- Tab cycling skips standalone elements, only cycles between actual groups
- Page navigation filters to show only newly visible cards (not entire 24-card page)

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

## DuelScene

The game auto-transitions from the VS screen to active gameplay without requiring user input.

### Duel Gameplay
**Navigator:** `DuelNavigator` + `ZoneNavigator`

Active gameplay with zones and cards.

**Zone Navigation (via ZoneNavigator):**
- C - Your hand (Cards)
- B - Battlefield
- G - Your graveyard
- X - Exile
- S - Stack
- Shift+G - Opponent graveyard
- Shift+X - Opponent exile

**Card Navigation:**
- Left/Right arrows - Move between cards in current zone
- Up/Down arrows - Card info details (via CardInfoNavigator)

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
- Zone-based announcements: "in hand", "opponent's Creature", "on stack"
- Zone-based activation: hand cards use two-click, others use single-click
- Tab/Shift+Tab cycles, Enter activates, Backspace cancels

**Selection Mode (in HotHighlightNavigator)**
- Detects Submit button with count AND no battlefield targets
- Hand cards use single-click to toggle selection instead of two-click to play
- Announces X cards selected after toggling

**CombatNavigator**
- Handles declare attackers/blockers phases
- F/Space triggers attack/block actions, Shift+F for no attacks/blocks
- Announces combat state for creatures (attacking, blocking, can block)

**BrowserNavigator**
- Handles library manipulation (scry, surveil, mulligan)
- Tab cycles through cards, Space confirms
- Detects via `BrowserScaffold_*` GameObjects

**PlayerPortraitNavigator**
- V key enters player info zone
- Left/Right switches players, Up/Down cycles properties
- Enter opens emote wheel (your portrait only)

**Priority Order:**
BrowserNavigator > CombatNavigator > HotHighlightNavigator > PortraitNavigator > BattlefieldNavigator > ZoneNavigator

## Mailbox Screen

**Navigator:** `GeneralMenuNavigator`
**Trigger:** Click Nav_Mail button in NavBar

The Mailbox screen shows inbox messages with rewards.

**Navigation:**
- Up/Down arrows: Navigate between mail items
- Enter: Activate/claim reward
- Backspace: Close mailbox and return to Home

**Technical Notes:**
- Mailbox is treated as an overlay via `OverlayDetector.IsInsideMailbox()`
- Mail items get titles via `UITextExtractor.TryGetMailboxItemTitle()`
- Nav_Mail button requires special activation via `NavBarController.MailboxButton_OnClick()` (onClick has no listeners)
- Elements are grouped as `ElementGroup.Mailbox`

## Rewards/Mastery Screen

**Controller:** `ProgressionTracksContentController`
**Navigator:** `GeneralMenuNavigator`
**Scene:** `RewardTrack`

The Rewards screen shows mastery pass progression and rewards.

**Navigation:**
- Up/Down arrows: Navigate between reward items
- Enter: Claim available rewards
- Backspace: Close and return to Home

**Technical Notes:**
- Detected as content panel via `ProgressionTracksContentController` in MenuScreenDetector
- Screen displays as "Rewards" in announcements
- Backspace triggers `NavigateToHome()` via content panel back handling

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
