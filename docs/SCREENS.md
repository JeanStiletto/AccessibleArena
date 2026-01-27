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
- Arrow keys navigate between elements (including input fields)
- Press **Enter** on input field to start editing
- While editing: type normally, arrows read content/characters
- Press **Escape** to stop editing and return to navigation
- Press **Tab** to stop editing and move to next element

### Dropdown Navigation
Dropdowns (TMP_Dropdown and cTMP_Dropdown) are detected and classified automatically.

**Dropdown edit mode** is tracked by observing focus state:
- When focus is on a dropdown item ("Item X: ..."), the mod blocks its own navigation and lets Unity handle arrow keys
- When focus leaves dropdown items, normal navigation resumes
- This handles both manual dropdowns (Enter to open, Enter to select) and auto-advancing dropdowns

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
- Collection cards in `PoolHolder` - Cards available to add to deck
- Deck cards in `DeckHolder` - Cards currently in deck
- Filter controls (color checkboxes, type filters, search)
- "Fertig" (Done) button

**Navigation:**
- Arrow Up/Down: Navigate between groups (Collection, Filters, etc.)
- Enter on group: Enter the group to navigate individual items
- Backspace: Exit current group, return to group list

**Collection Card Navigation:**
- Left/Right arrows: Navigate between cards in collection
- Up/Down arrows: Read card details (name, type, mana cost, rules text, etc.)
- Enter: Add card to deck (activates the card)
- Home/End: Jump to first/last card

**Card Info Reading:**
When focused on a card, Up/Down arrows cycle through card information blocks:
- Name
- Mana Cost
- Type
- Power/Toughness (if creature)
- Rules Text
- Flavor Text
- Artist

**Technical Notes:**
- Collection cards are in `DeckBuilderCollection` group
- Cards are extracted using Model-based detection when available
- CardInfoNavigator is automatically prepared when navigating to cards
- Some cards may show as "Unknown card" if Model data unavailable (edge cases)

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

## Adding New Screens

1. Identify panel name and key elements
2. Test if EventSystem works (log `currentSelectedGameObject` on Tab)
3. If needed, create navigator following existing patterns
4. Register in `AccessibleArenaMod.InitializeServices()` and `OnUpdate()`
5. Document here if screen has special requirements
