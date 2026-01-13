# MTGA Screen Reference

Quick reference for special screens requiring custom navigation.

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

**HighlightNavigator**
- Handles Tab cycling through playable/highlighted cards during normal gameplay
- Detects cards with active `HotHighlight` child objects
- Tab/Shift+Tab cycles, Enter plays card

**TargetNavigator**
- Handles target selection when playing spells that require targets
- Detects valid targets via `HotHighlightBattlefield(Clone)` child objects
- Tab cycles targets, Enter selects, Escape cancels

**DiscardNavigator**
- Handles forced discard selection (e.g., opponent plays discard spell)
- Detects via "Submit X" button presence
- Enter toggles card selection, Space submits

**CombatNavigator**
- Handles declare attackers/blockers phases
- Space triggers attack/block actions
- Announces combat state for creatures

**Priority Order:**
TargetNavigator > DiscardNavigator > CombatNavigator > HighlightNavigator > ZoneNavigator

## Adding New Screens

1. Identify panel name and key elements
2. Test if EventSystem works (log `currentSelectedGameObject` on Tab)
3. If needed, create navigator following existing patterns
4. Register in `MTGAAccessibilityMod.InitializeServices()` and `OnUpdate()`
5. Document here if screen has special requirements
