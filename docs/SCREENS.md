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

### WelcomeGate Screen
**Panel:** `Panel - WelcomeGate_Desktop_16x9(Clone)`
**Navigator:** `WelcomeGateNavigator`

Elements:
- `Button_Login` - CustomButton (needs pointer simulation)
- `Text_NeedHelp` - Standard Button

Issue: EventSystem shows no selected object on Tab.

### Login Panel
**Panel:** `Panel - Log In_Desktop_16x9(Clone)`
**Navigator:** `LoginPanelNavigator`

Elements:
- `InputsBox/.../Input Field - E-mail` - TMP_InputField
- `InputsBox/.../Input Field - PW` - TMP_InputField (password)
- `Toggle` - First Toggle in panel
- `MainButton_Login` - Standard Button
- `Button_Back` - CustomButton

Issue: Tab gets stuck after Login button. Toggle changes state when selected.

### Code of Conduct
**Navigator:** `CodeOfConductNavigator`

Handles terms/consent checkboxes screen with multiple toggles.

## NPE Screens

### Reward Chest Screen
**Container:** `NPE-Rewards_Container`
**Navigator:** `EventTriggerNavigator`

Elements:
- `NPE_RewardChest` - Chest (needs controller reflection)
- `Deckbox_A` through `Deckbox_E` - Deck boxes (need controller reflection)
- `Hitbox_LidOpen` - Continue button (standard activation)

Special handling: Chest and deck boxes require `NPEContentControllerRewards` methods:
- Chest: `Coroutine_UnlockAnimation()` + `AwardAllKeys()`
- Deck box: `set_AutoFlipping(true)` + `OnClaimClicked_Unity()`

Note: `NPEMetaDeckView.Model` is null - no deck data available. These are placeholder boxes.

### Card Reveal Screen
Handled by `EventTriggerNavigator`.

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

**DiscardNavigator**
- Handles forced discard selection (e.g., opponent plays discard spell)
- Detects via "Submit X" button presence
- Enter toggles card selection, Space submits

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
HotHighlightNavigator > DiscardNavigator > CombatNavigator > BrowserNavigator > ZoneNavigator

## Adding New Screens

1. Identify panel name and key elements
2. Test if EventSystem works (log `currentSelectedGameObject` on Tab)
3. If needed, create navigator following existing patterns
4. Register in `MTGAAccessibilityMod.InitializeServices()` and `OnUpdate()`
5. Document here if screen has special requirements
