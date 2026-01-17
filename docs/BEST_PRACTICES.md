# MTGA Accessibility Mod - Best Practices

Coding patterns and utilities for the accessibility mod. For game architecture and internals, see [GAME_ARCHITECTURE.md](GAME_ARCHITECTURE.md).

## Input System

### Two Input Systems in MTGA

MTGA uses TWO different input systems simultaneously:

**1. Unity Legacy Input System (`UnityEngine.Input`)**
- Used by: Our mod, simple key checks
- API: `Input.GetKeyDown(KeyCode.X)`
- Limitation: Cannot "consume" keys - all readers see the same input

**2. Unity New InputSystem (`Unity.InputSystem`)**
- Used by: MTGA's game logic via `MTGA.KeyboardManager.KeyboardManager`
- API: InputActions, callbacks, event-driven
- Features: Action maps, rebinding, proper consumption

**The Problem:**
When both systems read from the same physical keyboard:
- Our mod reads `Input.GetKeyDown(KeyCode.Return)` → true
- Game's KeyboardManager also reads Return → triggers game action (e.g., "Pass until response")
- Both happen in the same frame - no way to "consume" the key in Legacy Input

**Solution - Scene-Based Key Blocking (January 2026):**

Instead of complex per-context key consumption, we use a simpler approach:

1. `KeyboardManagerPatch` intercepts `MTGA.KeyboardManager.KeyboardManager.PublishKeyDown`
2. **In DuelScene: Block Enter entirely** - Our mod handles ALL Enter presses
3. **Other scenes: Per-key consumption** via `InputManager.ConsumeKey()` if needed

This solves multiple problems at once:
- **Auto-skip prevention**: Game can't trigger "Pass until response" because it never sees Enter
- **Player info zone**: Enter opens emote wheel instead of passing priority
- **Card playing**: Our navigators handle Enter, game doesn't interfere

**Files:**
- `Patches/KeyboardManagerPatch.cs` - Harmony prefix patch with scene detection
- `InputManager.cs` - `ConsumeKey()`, `IsKeyConsumed()` for other keys/scenes

**Why NOT migrate to InputSystem:**
- Current approach is simpler and works well
- Full migration would require touching 16+ files
- Mod-only elements (player info zone) can't benefit from InputSystem anyway
- Risk of breaking existing working functionality

### Game's Built-in Keybinds (DO NOT OVERRIDE)
- Arrow keys: Navigation
- Tab / Shift+Tab: Next / Previous item
- Enter / Space: Accept / Submit
- Escape: Cancel / Back
- F: Find
- Alt (hold): Alt view (card details)

### Safe Custom Shortcuts
Your Zones (Battle): C (Hand/Cards), B (Battlefield), G (Graveyard), X (Exile), S (Stack)
Opponent Zones: Shift+G (Graveyard), Shift+X (Exile)
Information: T (Turn/phase), L (Life totals), A (Your Mana), Shift+A (Opponent Mana)
Card Details: Arrow Up/Down when focused on a card
Deck Selection: Shift+Enter to edit deck name (Enter to select deck)
Global: F1 (Help), F2 (Context info), Ctrl+R (Repeat last)

### Keyboard Manager Architecture
- `MTGA.KeyboardManager.KeyboardManager`: Central keyboard handling
- Priority levels: See "Input Priority" under Main Menu Architecture above

## UI Interaction Patterns

### Critical: Toggle Behavior
**Problem:** `EventSystem.SetSelectedGameObject(toggleElement)` triggers the toggle, changing its state.

**Solution:** Never call SetSelectedGameObject for Toggle components:
```csharp
var toggle = element.GetComponent<Toggle>();
if (toggle == null)
{
    EventSystem.current?.SetSelectedGameObject(element);
}
```

### CustomButton Pattern
- Game uses `CustomButton` component (not Unity's standard `Button`)
- CustomButton has TWO activation mechanisms:
  1. `_onClick` UnityEvent field - Secondary effects (sounds, animations)
  2. `IPointerClickHandler` - Primary game logic (state changes, navigation)
- Use `UIActivator.Activate()` which handles both automatically

**Critical Discovery (January 2026):**
CustomButton's `_onClick` UnityEvent is NOT where the main game logic lives. The actual
functionality (tab switching, deck selection, button actions) is implemented in
`IPointerClickHandler.OnPointerClick()`. Invoking only `_onClick` via reflection produces
sounds but doesn't trigger state changes.

**Current UIActivator Strategy for CustomButtons:**
1. Detect CustomButton component via `HasCustomButtonComponent()`
2. Set element as selected in EventSystem (`SetSelectedGameObject`)
3. Send pointer events (enter, down, up, click)
4. Send Submit event (keyboard Enter activation)
5. Also invoke `_onClick` via reflection for secondary effects

**Activation Sequence in `SimulatePointerClick()`:**
```csharp
// 1. Select in EventSystem
eventSystem.SetSelectedGameObject(element);

// 2. Pointer event sequence
ExecuteEvents.Execute(element, pointer, ExecuteEvents.pointerEnterHandler);
ExecuteEvents.Execute(element, pointer, ExecuteEvents.pointerDownHandler);
ExecuteEvents.Execute(element, pointer, ExecuteEvents.pointerUpHandler);
ExecuteEvents.Execute(element, pointer, ExecuteEvents.pointerClickHandler);

// 3. Submit event (keyboard activation)
ExecuteEvents.Execute(element, baseEventData, ExecuteEvents.submitHandler);

// 4. Direct IPointerClickHandler invocation
foreach (var handler in element.GetComponents<IPointerClickHandler>())
    handler.OnPointerClick(pointer);
```

**Why onClick Reflection Alone Doesn't Work:**
- Tabs (Play/Ranked/Brawl): onClick plays sound, but IPointerClickHandler changes mode
- Deck buttons: onClick may be empty, IPointerClickHandler handles selection
- The Harmony patches for `PlayBladeVisualState` and `IsDeckSelected` only fire when
  the actual pointer handlers execute, not when onClick is invoked

### StyledButton Pattern
- Used for prompt buttons (Continue, Cancel) in pre-battle and duel screens
- Inherits from `Selectable` (found via `FindObjectsOfType<Selectable>()`)
- Implements `IPointerClickHandler` - use `UIActivator.SimulatePointerClick()` directly
- Does NOT respond to `onClick.Invoke()` or reflection-based method calls

### Input Field Text
- Empty fields contain zero-width space (U+200B), not empty string
- Always check for TMP_InputField BEFORE checking TMP_Text children
- Password fields: announce character count, not content

### EventSystem Limitations
- `EventSystem.currentSelectedGameObject` is often null in MTGA
- Most screens use CustomButton/EventTrigger which don't register with EventSystem
- UIFocusTracker's OnFocusChanged event rarely fires due to this
- Navigation is handled by custom Navigator classes (EventTriggerNavigator, etc.)
- Card navigation preparation must happen in navigators, not via focus events

## Centralized Strings (Localization-Ready)

All user-facing announcement strings are centralized in `Core/Models/Strings.cs`. This enables future localization and ensures consistency.

### Using Strings

**Always use the Strings class for announcements:**
```csharp
// Static strings (constants)
_announcer.Announce(Strings.NoCardSelected, AnnouncementPriority.High);
_announcer.Announce(Strings.EndOfZone, AnnouncementPriority.Normal);

// Dynamic strings (methods)
_announcer.Announce(Strings.CannotActivate(cardName), AnnouncementPriority.High);
_announcer.Announce(Strings.ZoneWithCount(zoneName, count), AnnouncementPriority.High);
```

**Never hardcode announcement strings:**
```csharp
// BAD - hardcoded string
_announcer.Announce("No card selected", AnnouncementPriority.High);

// GOOD - centralized string
_announcer.Announce(Strings.NoCardSelected, AnnouncementPriority.High);
```

### String Categories in Strings.cs

- **General/System** - ModLoaded, Back, NoSelection, NoAlternateAction, etc.
- **Activation** - CannotActivate(), CouldNotPlay(), NoCardSelected
- **Menu Navigation** - OpeningPlayModes, ReturningHome, etc.
- **Login/Account** - Field prompts, action confirmations
- **Battlefield Navigation** - Row announcements, boundaries
- **Zone Navigation** - Zone announcements, boundaries
- **Targeting** - Target selection messages
- **Combat** - Attack/block button errors
- **Card Actions** - NoPlayableCards, SpellCast
- **Discard** - Selection counts, submission messages
- **Card Info** - Navigation boundaries

### Adding New Strings

1. Add the string to the appropriate category in `Strings.cs`
2. Use a constant for static strings, a method for dynamic strings with parameters
3. Use the new string in your code via `Strings.YourNewString`

**Example - Adding a static string:**
```csharp
// In Strings.cs
public const string NewFeatureMessage = "New feature activated";

// In your code
_announcer.Announce(Strings.NewFeatureMessage, AnnouncementPriority.Normal);
```

**Example - Adding a dynamic string:**
```csharp
// In Strings.cs
public static string DamageDealt(string source, int amount, string target) =>
    $"{source} deals {amount} to {target}";

// In your code
_announcer.Announce(Strings.DamageDealt("Lightning Bolt", 3, "opponent"), AnnouncementPriority.High);
```

### Activation Announcements

**Do NOT announce successful activations** - they are informational clutter. Only announce failures:

```csharp
var result = UIActivator.SimulatePointerClick(card);
if (!result.Success)
{
    _announcer.Announce(Strings.CannotActivate(cardName), AnnouncementPriority.High);
}
// No announcement on success - the game's response is the feedback
```

## General Utilities

These utilities are used throughout the mod for UI interaction, text extraction, and card detection.

### UIActivator (Always Use for Activation)
Handles all element types automatically:
```csharp
var result = UIActivator.Activate(element);
// result.Success, result.Message, result.Type
```

**Activation Order (in `Activate()`):**
1. TMP_InputField → ActivateInputField()
2. InputField → Select()
3. Toggle → toggle.isOn = !toggle.isOn
4. Button → onClick.Invoke()
5. Child Button → onClick.Invoke()
6. Clickable in hierarchy → SimulatePointerClick on child
7. **CustomButton → SimulatePointerClick + TryInvokeCustomButtonOnClick**
8. Fallback → SimulatePointerClick

**SimulatePointerClick() sequence (updated January 2026):**
1. SetSelectedGameObject in EventSystem
2. Pointer events: enter → down → up → click
3. Submit event (keyboard Enter)
4. Click on immediate children
5. Direct IPointerClickHandler invocation

Handles: Button, Toggle, TMP_InputField, InputField, CustomButton (via pointer simulation + onClick)

**Card Playing from Hand:**
```csharp
UIActivator.PlayCardViaTwoClick(card, (success, message) =>
{
    if (success)
        announcer.Announce($"Played {cardName}", AnnouncementPriority.Normal);
    else
        announcer.Announce($"Could not play {cardName}", AnnouncementPriority.High);
});
```

Uses double-click + center click approach (see `docs/CARD_PLAY_IMPLEMENTATION.md`).

### UITextExtractor
Extracts text and detects element types:
```csharp
string text = UITextExtractor.GetText(element);
string type = UITextExtractor.GetElementType(element); // "button", "card", etc.
```

**Button Text Extraction (use for buttons):**
```csharp
// Searches ALL TMP_Text children (including inactive), skips icons
string label = UITextExtractor.GetButtonText(buttonObj, "Fallback");
```
Use `GetButtonText()` instead of `GetText()` for buttons because:
- Searches all text children, not just the first
- Includes inactive children (important for MTGA's button structure)
- Skips single-character content (often icons)
- Handles zero-width spaces automatically

**Text Cleaning (public utility):**
```csharp
string clean = UITextExtractor.CleanText(rawText);
```
Removes: zero-width spaces (`\u200B`), rich text tags, normalizes whitespace.

**Element Type Fallback:**
`GetElementType()` returns "item" when no specific type is detected. This is the default fallback - check for it if you need to handle unknown elements specially.

### CardDetector
Card detection utilities (cached for performance). Delegates to CardModelProvider for model access:
```csharp
// Detection (CardDetector's core responsibility)
bool isCard = CardDetector.IsCard(element);
GameObject root = CardDetector.GetCardRoot(element);
bool hasTargets = CardDetector.HasValidTargetsOnBattlefield();
CardDetector.ClearCache(); // Call on scene change (clears both caches)

// Card info extraction (delegates to CardModelProvider, falls back to UI)
CardInfo info = CardDetector.ExtractCardInfo(element);
List<CardInfoBlock> blocks = CardDetector.GetInfoBlocks(element);

// Card categorization (delegates to CardModelProvider)
var (isCreature, isLand, isOpponent) = CardDetector.GetCardCategory(card);
bool creature = CardDetector.IsCreatureCard(card);
bool land = CardDetector.IsLandCard(card);
bool opponent = CardDetector.IsOpponentCard(card);
```

### CardModelProvider
Direct access to card Model data. Use when you already have a card and need its properties:
```csharp
// Component access
Component cdc = CardModelProvider.GetDuelSceneCDC(card);
object model = CardModelProvider.GetCardModel(cdc);

// Card info from Model (DuelScene cards only)
CardInfo? info = CardModelProvider.ExtractCardInfoFromModel(card);

// Card categorization (efficient single Model lookup)
var (isCreature, isLand, isOpponent) = CardModelProvider.GetCardCategory(card);
bool creature = CardModelProvider.IsCreatureCard(card);
bool land = CardModelProvider.IsLandCard(card);
bool opponent = CardModelProvider.IsOpponentCard(card);

// Name lookup from database
string name = CardModelProvider.GetNameFromGrpId(grpId);
```

**When to use which:**
- **CardDetector**: When you need to check if something IS a card, or need UI fallback
- **CardModelProvider**: When you already know it's a card and only need Model data

**Mana Cost Parsing:**
The Model's `PrintedCastingCost` is a `ManaQuantity[]` array. Each ManaQuantity has:
- `Count` field (UInt32): How many mana of this type (e.g., 2 for {2})
- `Colors` field (ManaColor[]): Color(s) of the mana
- `IsGeneric` property: True for colorless/generic mana
- `IsHybrid` property: True for hybrid mana (e.g., {W/U})
- `IsPhyrexian` property: True for Phyrexian mana

Example for {2}{U}{U}:
- Entry 1: Count=2, IsGeneric=true → "2"
- Entry 2: Count=2, Color=Blue → "Blue, Blue"
- Result: "2, Blue, Blue"

**Detection Priority** (fast to slow):
1. Object name patterns: CardAnchor, NPERewardPrefab_IndividualCard, MetaCardView, CDC #
2. Parent name patterns (one level up)
3. Component names: BoosterMetaCardView, RewardDisplayCard, Meta_CDC, CardView

**Target Detection:**
`HasValidTargetsOnBattlefield()` scans battlefield and stack for cards with active `HotHighlight` children.
Used by DuelNavigator and DiscardNavigator to detect targeting mode vs other game states.

### CardInfoNavigator
Handles Arrow Up/Down navigation through card info blocks.

**Automatic Activation:** Card navigation activates automatically when Tab focuses a card.
No Enter required - just press Arrow Down to hear card details.

**Lazy Loading:** For performance, card info is NOT extracted when focus changes.
Info blocks are only loaded on first Arrow press. This allows fast Tab navigation
through many cards without performance impact.

**Manual Activation (legacy):**
```csharp
MTGAAccessibilityMod.Instance.ActivateCardDetails(element);
```

**Preparing for a card (used by navigators):**
```csharp
// Default (Hand zone)
MTGAAccessibilityMod.Instance.CardNavigator.PrepareForCard(element);
// With explicit zone
MTGAAccessibilityMod.Instance.CardNavigator.PrepareForCard(element, ZoneType.Battlefield);
```

**Info block order varies by zone:**
- Hand/Stack/Other: Name, Mana Cost, Power/Toughness, Type, Rules, Flavor, Artist
- Battlefield: Name, Power/Toughness, Type, Rules, Mana Cost, Flavor, Artist

On battlefield, mana cost is less important (card already in play), so it's shown after rules text.

## Duel Services

These services are specific to the DuelScene and handle in-game events and navigation.

### DuelAnnouncer
Announces game events via Harmony patch on `UXEventQueue.EnqueuePending()`.

**Working Announcements:**
- Turn changes: "Turn X. Your turn" / "Turn X. Opponent's turn"
- Card draws: "Drew X card(s)" / "Opponent drew X card(s)"
- Spell resolution: "Spell resolved" (when stack empties)
- Stack announcements: "Cast [card name]" when spell goes on stack
- Phase announcements: Main phases, combat steps (declare attackers/blockers, damage)
- Combat announcements: "Combat begins", "[Name] [P/T] attacking", "Attacker removed"
- Attacker count: "X attackers" when leaving declare attackers phase (summary)
- Opponent plays: "Opponent played a card" (hand count decrease detection)
- Combat damage: "[Card] deals [N] to [target]" (see Combat Damage Announcements below)

**Individual Attacker Announcements (January 2026):**

Each creature declared as an attacker is announced individually with name and power/toughness.
This matches the visual feedback sighted players see when creatures are tapped to attack.

*Implementation:*
- Triggered by `AttackLobUXEvent` for each attacking creature
- Uses `_attackerId` field to get the creature's InstanceId
- Looks up card name via `FindCardNameByInstanceId()` and P/T via `GetCardPowerToughnessByInstanceId()`

*Example Announcements:*
- "Goblin Bruiser 3/3 attacking"
- "Serra Angel 4/4 attacking"

**Attacker Count Summary (January 2026):**

When the declare attackers phase ends, a summary count is announced before the next phase.
This gives an overview when multiple attackers were declared.

*Implementation:*
- Detected in `BuildPhaseChangeAnnouncement()` when `_currentStep` was "DeclareAttack" and new step differs
- Uses `CountAttackingCreatures()` which scans for cards with active "IsAttacking" child indicator

*Example Announcements:*
- "2 attackers. Declare blockers" (transitioning to blockers phase)
- "1 attacker. Declare blockers"

**Blocker Phase Announcements (January 2026):**

The `CombatNavigator` tracks blocker selection and assignment during the declare blockers phase.

*Two States Tracked:*
1. **Selected blockers** - Creatures clicked as potential blockers (have `SelectedHighlightBattlefield` + `CombatIcon_BlockerFrame`)
2. **Assigned blockers** - Creatures confirmed to block an attacker (have `IsBlocking` indicator active)

*Announcements:*
- When selecting potential blockers: "3/4 blocking" (combined power/toughness)
- When assigning blockers to attackers: "Spiritual Guardian assigned"
- Multiple blockers assigned: "Spiritual Guardian, Llanowar Elves assigned"

*Blocking Workflow:*
1. Click a potential blocker → "X/Y blocking" (combined P/T of selected blockers)
2. Click an attacker to assign the blocker(s) → "[Name] assigned"
3. Repeat for other blockers/attackers
4. Press Space or F to confirm all blocks

*Tracking Reset:*
- Selected blocker tracking clears when blockers are assigned (IsBlocking activates)
- Both trackers reset when entering/exiting the declare blockers phase
- This prevents the P/T announcement from persisting after assignment

*Key Methods in CombatNavigator:*
```csharp
UpdateBlockerSelection()      // Called each frame, tracks both states
FindSelectedBlockers()        // Finds creatures with selection highlight + blocker frame
FindAssignedBlockers()        // Finds creatures with IsBlocking active
IsCreatureSelectedAsBlocker() // Checks selection highlight + blocker frame
IsCreatureBlocking()          // Checks for active IsBlocking child
```

**Combat Damage Announcements (January 2026):**

Combat damage is announced via `CombatFrame` events which contain `DamageBranch` objects.

*Announcement Queue Fix:*
Changed `AnnouncementService` to only interrupt for `Immediate` priority. Previously, `High` priority announcements would interrupt each other, causing rapid damage events to overwrite. Now Tolk's internal queue handles sequencing for all non-Immediate announcements.

*Event Structure:*
```
CombatFrame
├── OpponentDamageDealt (int) - Total unblocked damage to opponent (YOUR damage to them)
├── DamageType (enum) - Combat, Spell, etc.
├── _branches (List<DamageBranch>) - Individual damage events
│   └── DamageBranch
│       ├── _damageEvent (UXEventDamageDealt)
│       │   ├── Source (MtgEntity) - Creature dealing damage
│       │   ├── Target (MtgEntity) - Creature or Player
│       │   └── Amount (int) - Damage amount
│       ├── _nextBranch (DamageBranch or null) - Chained damage (e.g., blocker's return damage)
│       └── BranchDepth (int) - Number of damage events in chain
└── _runningBranches (List<DamageBranch>) - Always empty in testing
```

*Damage Chain (_nextBranch):*
When creatures trade damage in combat, the structure can be:
- `_damageEvent`: Attacker's damage to blocker
- `_nextBranch._damageEvent`: Blocker's damage back to attacker

The code follows `_nextBranch` chain to extract all damage and groups them together for announcement:
- Single damage: "Cat deals 3 to opponent"
- Trade damage: "Cat deals 3 to Bear, Bear deals 2 to Cat"

**KNOWN LIMITATION:** Blocker's return damage is NOT reliably included in `_nextBranch`.
The game client inconsistently populates this field. Sometimes `_nextBranch=null` even when
the blocker dealt damage. This appears to be a game client behavior we cannot control.

*Potential Future Solutions:*
1. Track damage via `UpdateCardModelUXEvent` when creatures get damage markers
2. Infer blocker damage from combat state (attacker P/T vs blocker P/T)
3. Accept limitation - attacker damage is always announced, blocker damage sometimes missing

*Key Fields in UXEventDamageDealt:*
- `Source`: MtgEntity with `InstanceId` and `GrpId` properties
- `Target`: Either `"Player: 1 (LocalPlayer)"`, `"Player: 2 (Opponent)"`, or MtgEntity with GrpId
- `Amount`: Integer damage amount

*Target Detection Logic:*
```csharp
var targetStr = target.ToString();
if (targetStr.Contains("LocalPlayer"))
    targetName = "you";
else if (targetStr.Contains("Opponent"))
    targetName = "opponent";
else
    targetName = CardModelProvider.GetNameFromGrpId(target.GrpId);
```

*Announcement Examples:*
- Creature to player: "Shrine Keeper deals 2 to you"
- Creature to opponent: "Shrine Keeper deals 4 to opponent"
- Creature to creature: "Shrine Keeper deals 3 to Nimble Pilferer"
- Combat trade (when _nextBranch exists): "Shrine Keeper deals 3 to Pilferer, Pilferer deals 2 to Shrine Keeper"

*OpponentDamageDealt Field:*
This tracks YOUR total unblocked damage to the opponent. It is NOT damage dealt BY the opponent.
When opponent attacks you, `OpponentDamageDealt=0`. Damage to you must be extracted from branches.

*InvolvedIds Pattern:*
The `InvolvedIds` list in DamageBranch contains: `[SourceInstanceId, TargetId]`
- Player IDs: 1 = LocalPlayer, 2 = Opponent
- Card IDs: InstanceId of the card

**Life Change Events (LifeTotalUpdateUXEvent):**

*Status:* Working (January 2026) - needs broader testing with various life gain/loss sources.

*Correct Field Names:*
- `AffectedId` (uint) - NOT "PlayerId"
- `Change` (property, int) - Life change amount (positive=gain, negative=loss)
- `_avatar` - Avatar object, can check `.ToString()` for "Player #1" to determine local player

*Example Announcements:*
- "You lost 3 life"
- "Opponent gained 4 life"

*Note:* There is no `NewLifeTotal` field.

**Privacy Protection:**
- NEVER reveals opponent's hidden info (hand contents, library)
- Only announces publicly visible information
- Opponent draws: "Opponent drew a card" (not card name)

**Integration:**
```csharp
// Created by DuelNavigator
_duelAnnouncer = new DuelAnnouncer(announcer);

// Activated when duel starts
_duelAnnouncer.Activate(localPlayerId);

// Receives events via Harmony patch automatically
// UXEventQueuePatch.EnqueuePendingPostfix() calls:
DuelAnnouncer.Instance?.OnGameEvent(uxEvent);
```

### HotHighlightNavigator (Unified - January 2026)

**REPLACED:** Separate `TargetNavigator` + `HighlightNavigator` unified into single `HotHighlightNavigator`.
Old files moved to `src/Core/Services/old/` for reference/revert.

**Key Discovery - Game Manages Highlights Correctly:**
Through diagnostic logging, we verified the game's HotHighlight system correctly updates:
- When targeting: Only valid targets have HotHighlight (hand cards LOSE highlight)
- When not targeting: Only playable cards have HotHighlight (battlefield cards LOSE highlight)
- No overlap - the game switches highlights when game state changes

This means we can trust the game and scan ALL zones, letting the zone determine behavior.

**User Flow (Unified):**
1. Press Tab at any time
2. Navigator discovers ALL items with HotHighlight across all zones
3. Announcement based on zone:
   - Hand: "Shock, in hand, 1 of 2"
   - Battlefield: "Goblin, 2/2, opponent's Creature, 1 of 3"
   - Stack: "Lightning Bolt, on stack, 1 of 2"
   - Player: "Opponent, player, 3 of 3"
4. Tab/Shift+Tab cycles through all highlighted items
5. Enter activates (based on zone):
   - Hand cards: Two-click to play
   - Everything else: Single-click to select
6. Backspace cancels (if targets highlighted)
7. When no highlights: Announces primary button text ("Pass", "Resolve", "Next")

**Key Discovery - HotHighlight Detection:**
The game uses `HotHighlight` child objects to visually indicate valid targets/playable cards:
- `HotHighlightBattlefield(Clone)` - Targeting mode targets
- `HotHighlightDefault(Clone)` - Playable cards in hand
- The highlight TYPE tells us the context

**No Mode Tracking Needed:**
```csharp
// Old approach: Separate mode tracking
if (_targetNavigator.IsTargeting) { ... }
else if (_highlightNavigator.IsActive) { ... }

// New approach: Zone determines behavior
if (item.Zone == "Hand")
    UIActivator.PlayCardViaTwoClick(...);  // Two-click to play
else
    UIActivator.SimulatePointerClick(...); // Single-click to select
```

**Input Priority (in DuelNavigator):**
```
HotHighlightNavigator → Tab/Enter/Backspace for highlights
BattlefieldNavigator  → A/R/B shortcuts, row navigation
ZoneNavigator         → C/G/X/S shortcuts, Left/Right in zones
```

**Battlefield Row Navigation:**
Still works during targeting - battlefield navigation is independent of highlight navigation.

**Known Bug - Activatable Creatures Priority:**
The game sometimes highlights only activatable creatures (like mana creatures) even when playable
lands are in hand. This appears to be game behavior - it wants you to tap mana first. After
activating the creature's ability, hand cards become highlighted.

**Old Navigators (Deprecated - in `src/Core/Services/old/`):**
- `TargetNavigator.cs` - Had separate _isTargeting mode, auto-enter/exit logic
- `HighlightNavigator.cs` - Had separate playable card cycling, rescan delay logic

### Mode Interactions (January 2026 - Updated)

The duel scene has multiple "modes" that affect input handling. With unified HotHighlightNavigator,
mode tracking is simpler - we trust the game's highlight system.

**Modes (Simplified):**
1. **Highlight Mode** (HotHighlightNavigator) - Tab cycles whatever game highlights (targets OR playable cards)
2. **Discard Mode** (DiscardNavigator) - Enter/Space during discard
3. **Combat Phase** (CombatNavigator) - F/Space during declare attackers/blockers
4. **Normal Mode** - Zone navigation

**Input Priority in DuelNavigator.HandleCustomInput():**
```
1. BrowserNavigator       → Scry/Surveil/Mulligan browsers
2. DiscardNavigator       → Enter/Space during discard mode
3. CombatNavigator        → F/Space during declare attackers/blockers
4. HotHighlightNavigator  → Tab/Enter/Backspace for highlights (UNIFIED)
5. BattlefieldNavigator   → A/R/B shortcuts, row navigation
6. ZoneNavigator          → C/G/X/S shortcuts, Left/Right in zones
```

**Key Simplification:**
Old approach required complex auto-detect/auto-exit logic to track targeting mode.
New approach trusts game highlights - whatever is highlighted is what Tab cycles through.

**HotHighlight - Shared Visual Indicator:**
The game uses `HotHighlight` child objects for MULTIPLE purposes:
- Valid spell targets (targeting mode)
- Playable cards (highlight mode)

**Key Discovery (January 2026):** Through diagnostic logging we verified the game CORRECTLY
manages highlights - when targeting mode starts, hand cards LOSE their highlight. When targeting
ends, battlefield cards LOSE their highlight. There is NO overlap.

**What about attackers/blockers?** Testing showed attackers/blockers do NOT use HotHighlight.
They use different indicators (`CombatIcon_AttackerFrame`, `SelectedHighlightBattlefield`, etc.).

**No Auto-Detection Needed (Unified Navigator):**
With HotHighlightNavigator, we removed all auto-detect/auto-exit logic:
```csharp
// OLD (removed):
if (!_targetNavigator.IsTargeting && HasValidTargetsOnBattlefield())
    _targetNavigator.EnterTargetMode();

// NEW:
// Just scan for highlights - game manages what's highlighted
DiscoverAllHighlights(); // Finds whatever game highlights
```

**DiscardNavigator Detection:**
```csharp
public bool IsDiscardModeActive()
{
    if (GetSubmitButtonInfo() == null)  // No "Submit X" button
        return false;
    return true;
}
```
- Looks for "Submit X" button (e.g., "Submit 0", "Submit 1")
- NOTE: Cancel button alone is NOT reliable (appears in combat too)

**Combat Phase Detection:**
```csharp
// In DuelAnnouncer
public bool IsInDeclareAttackersPhase { get; private set; }
public bool IsInDeclareBlockersPhase { get; private set; }
```
- Set via `ToggleCombatUXEvent` and phase tracking
- Used by CombatNavigator for F/Space shortcuts

**BattlefieldNavigator Zone Coordination:**
```csharp
// Only handle Left/Right if in battlefield zone
bool inBattlefield = _zoneNavigator.CurrentZone == ZoneType.Battlefield;
if (inBattlefield && Input.GetKeyDown(KeyCode.LeftArrow)) { ... }
```
- Prevents stealing Left/Right from other zones (hand, graveyard)
- Zone state shared via `ZoneNavigator.SetCurrentZone()`

**Common Bug Patterns (Simplified):**

1. **Activatable creatures take priority (KNOWN BUG):**
   - Game highlights mana creatures before showing playable lands
   - This appears to be game behavior, not a mod bug
   - User must activate the creature, then hand cards become highlighted

2. **Can't play cards (stuck in mode):**
   - Check which navigator is consuming Enter key
   - Check discard mode flags
   - Look for leftover Submit/Cancel buttons

3. **Left/Right stolen by battlefield:**
   - Check `ZoneNavigator.CurrentZone`
   - Verify zone shortcuts update zone state

**Debug Logging:**
```csharp
// Use CardDetector.LogAllHotHighlights() to see all highlighted items
// Called automatically on Tab in diagnostic mode
MelonLogger.Msg($"[Mode] discard={_discardNavigator.IsDiscardModeActive()}, " +
    $"combat={inCombatPhase}, highlights={_hotHighlightNavigator.ItemCount}");
```

### PlayerPortraitNavigator
Handles V key player info zone navigation. Provides access to player life, timer, timeouts, and emote wheel.

**State Machine:**
- `Inactive` - Not in player info zone
- `PlayerNavigation` - Navigating between players and properties
- `EmoteNavigation` - Navigating emote wheel (your portrait only)

**User Flow:**
1. Press V to enter player info zone (starts on your info)
2. Up/Down cycles through properties (Life, Timer, Timeouts, Games Won)
3. Left/Right switches between you and opponent (preserves property index)
4. Enter opens emote wheel (your portrait only)
5. Escape or Tab exits zone

**Key Properties:**
- `IsInPlayerInfoZone` - True when in any non-Inactive state
- Used by DuelNavigator to give portrait navigator priority for Enter key

**Input Priority:**
PortraitNavigator runs BEFORE BattlefieldNavigator in the input chain. Arrow keys work correctly
when in player info zone. HotHighlightNavigator handles Tab/Enter separately.

**Current Workaround Attempts:**
1. `InputManager.GetEnterAndConsume()` marks Enter as consumed
2. `KeyboardManagerPatch` blocks consumed keys from game's KeyboardManager
3. Input chain ordering in DuelNavigator

**Integration:**
```csharp
// DuelNavigator creates PlayerPortraitNavigator
_portraitNavigator = new PlayerPortraitNavigator(_announcer, _targetNavigator);

// In HandleCustomInput, portrait navigator handles V and when active
if (_portraitNavigator.HandleInput())
    return true;
```

### ZoneNavigator
Handles zone navigation in DuelScene. Separate service following same pattern as CardInfoNavigator.

**Zone Shortcuts:** See "Safe Custom Shortcuts" in Input System section above.

**Card Navigation within Zones:**
- Left Arrow - Previous card in current zone
- Right Arrow - Next card in current zone
- Enter - Play/activate current card (uses PlayCardViaTwoClick for hand cards)

**Usage (via DuelNavigator):**
```csharp
// DuelNavigator creates and owns ZoneNavigator
_zoneNavigator = new ZoneNavigator(announcer);

// In HandleCustomInput, delegate to ZoneNavigator
if (_zoneNavigator.HandleInput())
    return true;
```

**Key Methods:**
```csharp
zoneNavigator.Activate();           // Discover zones on duel start
zoneNavigator.NavigateToZone(zone); // Jump to zone with shortcut
zoneNavigator.NextCard();           // Navigate within zone
zoneNavigator.GetCurrentCard();     // Get current card for CardInfoNavigator
zoneNavigator.ActivateCurrentCard(); // Play card (hand) or activate (battlefield)
```

**EventSystem Conflict Resolution:**
Arrow keys also trigger Unity's EventSystem navigation, causing focus cycling between UI buttons.
ZoneNavigator clears EventSystem selection before handling arrow keys:
```csharp
private void ClearEventSystemSelection()
{
    var eventSystem = EventSystem.current;
    if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
    {
        eventSystem.SetSelectedGameObject(null);
    }
}
```

## Navigator Patterns

### When to Create a Navigator
- EventSystem doesn't work on the screen
- Screen has special activation requirements (like NPE chest)
- Screen needs custom Tab order

### Creating a New Navigator (BaseNavigator Pattern)
New navigators should extend `BaseNavigator` for consistency and reduced duplication.

1. Create a class extending `BaseNavigator`:
```csharp
public class MyScreenNavigator : BaseNavigator
{
    public override string NavigatorId => "MyScreen";
    public override string ScreenName => "My Screen";
    public override int Priority => 50; // Higher = checked first

    public MyScreenNavigator(IAnnouncementService announcer) : base(announcer) { }

    protected override bool DetectScreen()
    {
        // Return true if this screen is currently displayed
        var panel = GameObject.Find("MyPanel(Clone)");
        return panel != null && panel.activeInHierarchy;
    }

    protected override void DiscoverElements()
    {
        // Use helper methods to populate _elements list
        AddButton(FindChildByName(panel, "Button1"), "Button 1");
        AddToggle(toggle, "Checkbox label");
        AddInputField(inputField, "Email");
    }
}
```

2. Register in `MTGAAccessibilityMod.InitializeServices()`:
```csharp
_navigatorManager.RegisterAll(
    new MyScreenNavigator(_announcer),
    // ... other navigators
);
```

### BaseNavigator Data Structure
Elements are stored in a single list using the `NavigableElement` struct:
```csharp
protected struct NavigableElement
{
    public GameObject GameObject;       // The UI element
    public string Label;                // Announcement text
    public CarouselInfo Carousel;       // Arrow key navigation info (optional)
    public GameObject AlternateActionObject; // Secondary action (e.g., edit button for decks)
}
```
Access via `_elements[index].GameObject`, `_elements[index].Label`, etc.

**Alternate Actions (Shift+Enter):**
Some elements have a secondary action accessible via Shift+Enter. For example:
- Deck entries: Enter selects deck, Shift+Enter edits deck name
- The alternate action object is stored in `AlternateActionObject` field
- `ActivateAlternateAction()` is called when Shift+Enter is pressed

### BaseNavigator Features
- **Common input handling**: Tab/Shift+Tab/Enter/Space built-in
- **Card navigation integration**: Automatic PrepareForCard() calls
- **Helper methods**: AddButton(), AddToggle(), AddInputField(), FindChildByName(), GetButtonText()
- **Override points**:
  - `HandleCustomInput()` - Add custom keys (return true if handled)
  - `OnElementActivated()` - Special activation logic (return true if handled)
  - `OnActivated()` / `OnDeactivating()` - Lifecycle hooks
  - `GetActivationAnnouncement()` - Custom screen announcement
  - `ValidateElements()` - Custom element validity check
  - `AcceptSpaceKey` - Whether Space triggers activation (default: true)
  - `SupportsCardNavigation` - Whether to integrate with CardInfoNavigator

### Special Activation Cases
Some elements (NPE chest/deck boxes) need controller reflection:
- Find controller via `GameObject.FindObjectOfType<NPEContentControllerRewards>()`
- Call methods like `Coroutine_UnlockAnimation()`, `OnClaimClicked_Unity()`
- See `EventTriggerNavigator.HandleSpecialNPEElement()` for example

## Card Handling in Navigators

### Automatic Card Navigation on Tab
When Tab changes the current element, prepare card navigation:
```csharp
private void PrepareCardNavigationForCurrentElement()
{
    var cardNavigator = MTGAAccessibilityMod.Instance?.CardNavigator;
    if (cardNavigator == null) return;

    var element = _elements[_currentIndex].GameObject;
    if (element != null && CardDetector.IsCard(element))
    {
        // Prepare (lazy) - info extracted only when user presses Arrow
        cardNavigator.PrepareForCard(element);
    }
    else if (cardNavigator.IsActive)
    {
        // Not a card - deactivate card navigation
        cardNavigator.Deactivate();
    }
}
```

Call this method:
1. After Tab changes `_currentIndex`
2. In `FinalizeActivation()` for the initial element

### Manual Card Activation on Enter (legacy)
When activating an element with Enter:
```csharp
private void ActivateElement(int index)
{
    var element = _elements[index].GameObject;

    // Check if card - delegate to central CardInfoNavigator
    if (CardDetector.IsCard(element))
    {
        if (MTGAAccessibilityMod.Instance.ActivateCardDetails(element))
            return;
    }

    // Not a card - normal activation
    UIActivator.Activate(element);
}
```

## Harmony Patches

### Panel State Detection (PanelStatePatch.cs)

We use Harmony patches to get event-driven notifications when panels open/close.
This provides reliable overlay detection for Settings, DeckSelect, and other menus.

**Successfully Patched Methods:**

NavContentController (base class for menu screens):
- `FinishOpen()` - Fires when panel finishes opening animation
- `FinishClose()` - Fires when panel finishes closing animation
- `BeginOpen()` / `BeginClose()` - Fires at start of open/close (logged only)
- `IsOpen` setter - Backup detection

SettingsMenu:
- `Open()` - Fires when settings opens (has 7 boolean parameters)
- `Close()` - Fires when settings closes
- `IsOpen` setter - Backup detection

DeckSelectBlade:
- `Show(EventContext, DeckFormat, Action)` - Fires when deck selection opens
- `Hide()` - Fires when deck selection closes
- `IsShowing` setter - Backup detection

PlayBladeController:
- `PlayBladeVisualState` setter - Fires when play blade state changes (Hidden/Events/DirectChallenge/FriendChallenge)
- `IsDeckSelected` setter - Fires when deck selection state changes

HomePageContentController:
- `IsEventBladeActive` setter - Fires when event blade opens/closes
- `IsDirectChallengeBladeActive` setter - Fires when direct challenge blade opens/closes

BladeContentView (base class):
- `Show()` - Fires when any blade view shows (EventBlade, FindMatchBlade, etc.)
- `Hide()` - Fires when any blade view hides

EventBladeContentView:
- `Show()` / `Hide()` - Specific patches for event blade

**Key Architecture - Harmony Flag Approach:**

The critical insight: Harmony events are 100% reliable (method was definitely called),
but our reflection-based panel detection during rescan was unreliable. Solution:

1. When Harmony fires (e.g., `SettingsMenu.Open()` postfix), immediately set overlay flags:
   ```csharp
   _settingsOverlayActive = true;  // Set in OnPanelStateChangedExternal()
   ```

2. During element discovery, `IsInForegroundPanel()` checks these flags:
   ```csharp
   bool overlayActive = _foregroundPanel != null || _settingsOverlayActive || _deckSelectOverlayActive || _playBladeActive;
   if (!overlayActive) return true;  // No overlay, show all elements
   return !IsInBackgroundPanel(obj); // Filter out NavBar, HomePage elements
   ```

3. This ensures background elements are filtered even before the content panel is detected.

**PlayBlade Filtering Logic:**

When `_playBladeActive` is true, `IsInBackgroundPanel()` uses special logic:
- Elements inside "Blade", "PlayBlade", "FindMatch", "EventBlade", "CampaignGraph" hierarchies are NOT filtered
- Elements inside "HomePage", "HomeContent" but NOT inside a Blade ARE filtered
- This shows only the blade's deck selection elements, hiding the HomePage buttons behind it

**CampaignGraph (Color Challenge) - Added January 2026:**
- `CampaignGraph` added to non-background list for Color Challenge panel support
- NOTE: This is potentially overspecific - may need generalization later

**Discovered Controller Types (via DiscoverPanelTypes()):**
- `NavContentController` - Base class, lifecycle methods patched
- `HomePageContentController` - Inherits from NavContentController, blade state setters patched
- `SettingsMenu` - Open/Close methods + IsOpen setter patched
- `DeckSelectBlade` - Show/Hide methods + IsShowing setter patched
- `PlayBladeController` - PlayBladeVisualState + IsDeckSelected setters patched
- `BladeContentView` - Base class for blade views, Show/Hide patched
- `EventBladeContentView` - Show/Hide patched
- `ConstructedDeckSelectController` - IsOpen getter only (no setter to patch)
- `DeckManagerController` - IsOpen getter only

## Debugging Tips

1. **Scan for panel by name:** `GameObject.Find("Panel - Name(Clone)")`
2. **Check EventSystem:** Log `currentSelectedGameObject` on Tab
3. **Identify element types:** Button vs CustomButton vs Image
4. **Test Toggle behavior:** Does selecting trigger state change?
5. **Find elements by path:** More reliable than name search
6. **Log all components:** On problematic elements to understand structure

## UI Element Filtering (UIElementClassifier)

The `UIElementClassifier` filters out non-navigable elements to keep the navigation clean.

### Filtering Methods

**1. Game Properties (`IsHiddenByGameProperties`)**
- CustomButton.Interactable = false
- CustomButton.IsHidden() = true
- CanvasGroup.alpha < 0.1
- CanvasGroup.interactable = false
- Decorative graphical elements (see below)

**CanvasGroup Visibility - Structural Container Exception (January 2026):**
Parent CanvasGroups named "CanvasGroup..." (e.g., "CanvasGroup - Overlay") are skipped during
visibility checks. MTGA uses these as structural containers with alpha=0, but their children
are still visible. Without this exception, buttons like "Return to Arena" would be incorrectly
filtered.
- NOTE: This is a broad exception - may show elements that shouldn't be visible. May need
  tightening if unwanted elements appear.

**2. Name Patterns (`IsFilteredByNamePattern`)**
- `blocker` - Modal click blockers
- `navpip`, `pip_` - Carousel dots
- `dismiss` - Dismiss buttons
- `button_base`, `buttonbase` - Internal button bases
- `fade` (except nav) - Fade overlays
- `hitbox`, `backer` (without text) - Hitboxes
- `socialcorner` - Social corner icon
- `new`, `indicator` - Badge indicators
- `viewport`, `content` - Scroll containers
- `gradient` (except nav) - Decorative gradients
- Nav controls inside carousels - Handled by parent
- `BUTTONS` (exact match) - Container EventTriggers wrapping actual buttons (Color Challenge)
- `Button_NPE` (exact match) - NPE overlay buttons that duplicate blade list items

**3. Text Content (`IsFilteredByTextContent`)**
- `new`, `tooltip information`, `text text text` - Placeholder text
- Numeric-only text in mail/notification elements - Badge counts

**4. Decorative Graphical Elements (`IsDecorativeGraphicalElement`)**
Filters elements that are purely graphical with no meaningful content:

```csharp
// Element is filtered if ALL conditions are true:
- HasActualText: false      // No text content
- HasImage: false           // No Image/RawImage component
- HasTextChild: false       // No TMP_Text children
- Size < 10x10 pixels       // Zero or very small size
```

**Examples filtered:**
- Avatar bust select buttons (deck list portraits)
- Objective graphics placeholders
- Decorative icons without function

**Examples NOT filtered (have meaningful size):**
- `nav wild card` button (80x75)
- `social corner icon` button (100x100)

This approach distinguishes between:
- **Decorative elements**: No content, zero size → Filter
- **Functional icon buttons**: No text but have size → Keep

### Adding New Filters

To filter a new type of element, choose the appropriate method:
1. **Name-based**: Add to `IsFilteredByNamePattern()` for consistent naming patterns
2. **Component-based**: Add to `IsHiddenByGameProperties()` for specific component checks
3. **Content-based**: Add to `IsFilteredByTextContent()` for text patterns

## Sibling Label Detection (UITextExtractor)

**Added January 2026:**

When an element has no text of its own, `UITextExtractor.GetText()` checks sibling elements for
labels via `TryGetSiblingLabel()`. This handles UI patterns where a button's label comes from
a sibling element.

**Example - Color Challenge buttons:**
- Button element has no text
- Sibling element "INFO" contains the color name ("White", "Blue", etc.)
- `TryGetSiblingLabel()` returns the sibling's text

**Skipped siblings:**
- MASK, SHADOW, DIVIDER, BACKGROUND, INDICATION - decorative elements

**NOTE:** This is a general feature that applies to all elements, not just Color Challenge.
May extract unintended sibling text in some cases.

## Color Challenge Panel (Working - January 2026)

**Current State:**
- `CampaignGraphContentController` recognized as content panel (filters NavBar)
- Auto-expand blade when Color Challenge opens (0.8s delay in `AutoExpandBlade()`)
- Color buttons (White, Blue, Black, Red, Green) show correct labels via sibling label detection
- Play button detected and functional (uses `CustomButtonWithTooltip` component)
- "Return to Arena" button visible (general back button)

**Key Implementation Details:**

1. **Content Controller Filtering** (`IsInForegroundPanel`):
   - Special case for `CampaignGraphContentController` to include:
     - Elements inside the controller
     - Elements inside the PlayBlade
     - The MainButton/MainButtonOutline (Play button)

2. **Play Button Detection** (`IsMainButton`):
   - Detects both `MainButton` (normal play) and `MainButtonOutline` (back button)
   - The Color Challenge Play button is at path:
     `.../CampaignGraphMainButtonModule(Clone)/MainButton_Play`
   - Uses `CustomButtonWithTooltip` component (not regular CustomButton)

3. **Button Type Support**:
   - Added `IsCustomButtonType()` helper to detect both CustomButton and CustomButtonWithTooltip
   - This enables detection of the Play button which uses CustomButtonWithTooltip

**User Flow:**
1. Navigate to Color Challenge (via Play button on home screen)
2. Tab through color options (White, Blue, Black, Red, Green)
3. Press Enter to select a color - blade collapses
4. Tab to find "Play" button and deck selection
5. Press Enter on Play to start the match

## Common Gotchas

- MelonGame attribute is case sensitive: `"Wizards Of The Coast"`
- NPE reward chest/deck boxes need controller reflection, not pointer events
- Mana costs use sprite tags (`<sprite name="xW">`) - parse with regex for symbol names
- ManaCost text elements need special handling: CleanText() strips all tags leaving empty content,
  so skip the empty-content check for ManaCost to allow ParseManaCost() to process raw sprite tags
- CardDetector cache must be cleared on scene changes (stale references)
- CustomButton.OnClick may have 0 listeners - direct invocation does nothing
- EventSystem.currentSelectedGameObject is often null - game uses custom navigation
- Card navigation must be prepared by navigators (EventTriggerNavigator), not UIFocusTracker
- CardInfoNavigator uses lazy loading - PrepareForCard() is fast, LoadBlocks() extracts info
