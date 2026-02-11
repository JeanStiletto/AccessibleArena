# Accessible Arena - Best Practices

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
- Enter / Space: Accept / Submit
- Escape: Cancel / Back
- Alt (hold): Alt view (card details)

### Mod Navigation Keys

**Menu Navigation:**
- Arrow Up/Down (or W/S): Navigate menu items
- Tab/Shift+Tab: Navigate menu items (same as Arrow Up/Down)
- Arrow Left/Right (or A/D): Carousel/stepper controls
- Home: Jump to first item
- End: Jump to last item

**Input Field Navigation:**
- Tab: Exit input field and move to next element
- Shift+Tab: Exit input field and move to previous element
- Escape: Exit input field (stay on current element)
- F4: Exit input field and toggle Friends panel (works even while editing)
- Arrow Up/Down: Read full input field content
- Arrow Left/Right: Read character at cursor

**Input Field Exit Behavior (January 2026):**
When exiting an input field (via Escape, Tab, or F4), the mod:
1. Deactivates the input field (stops text input)
2. Clears EventSystem selection (`SetSelectedGameObject(null)`)

This ensures `IsAnyInputFieldFocused()` returns false after exit, allowing subsequent
key presses (like Escape to close a popup) to work correctly. MTGA auto-selects input
fields, so clearing selection is required even when the field wasn't actively focused.

**Duel Navigation:**
- Tab/Shift+Tab: Cycle highlights (playable cards, targets)
- Arrow keys: Zone/card/battlefield navigation

### Safe Custom Shortcuts
Your Zones (Battle): C (Hand/Cards), B (Battlefield), G (Graveyard), X (Exile), S (Stack)
Opponent Zones: Shift+G (Graveyard), Shift+X (Exile)
Information: T (Turn/phase), L (Life totals), V (Player Info Zone)
Library Counts: D (Your Library), Shift+D (Opponent Library), Shift+C (Opponent Hand)
Card Details: Arrow Up/Down when focused on a card
Zone Navigation: Left/Right (Navigate cards), Home/End (Jump to first/last)
Deck Selection: Shift+Enter to edit deck name (Enter to select deck)
Global: F1 (Help), F3 (Current screen), Ctrl+R (Repeat last)

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

### Dropdown Handling (DropdownStateManager)

MTGA uses `TMP_Dropdown` and `cTMP_Dropdown` for dropdown menus. The mod tracks dropdown state centrally via `DropdownStateManager` (`src/Core/Services/DropdownStateManager.cs`).

**Key Concepts:**
- **Dropdown Edit Mode**: When a dropdown is expanded, the mod defers to Unity's built-in navigation
- **Detection**: Checks if EventSystem focus is on a dropdown item (name starts with "Item")
- **Exit Handling**: Tracks transitions out of dropdown mode for navigator index sync

**DropdownStateManager API:**
```csharp
// Check if currently in dropdown
if (DropdownStateManager.Instance.IsInDropdownMode)
    return; // Let Unity handle navigation

// Update state and check for exit transition (call in Update loop)
if (DropdownStateManager.Instance.UpdateAndCheckExitTransition())
{
    // Just exited dropdown mode - suppress announcement this frame
}

// Notify when closing dropdown programmatically
DropdownStateManager.Instance.OnDropdownClosed();

// Prevent re-entry for brief period (e.g., after closing)
DropdownStateManager.Instance.SuppressReentry();
```

**User Flow:**
1. Navigate to dropdown with arrow keys
2. Press Enter to open dropdown (or it auto-opens in registration flow)
3. Use Up/Down to navigate items (Unity handles this)
4. Press Enter to select item (closes dropdown)
5. Press Escape/Backspace to cancel (closes without selecting)

**Integration Points:**
- `BaseNavigator.HandleInput()` - Checks dropdown mode before custom navigation
- `UIFocusTracker` - Delegates dropdown state to DropdownStateManager
- `GeneralMenuNavigator` - Uses DropdownStateManager for overlay filtering

**Auto-Advance Dropdowns (Registration):**
Registration screen has dropdowns that auto-advance: selecting Month opens Day, selecting Day opens Year, etc. DropdownStateManager handles this by detecting the transition between dropdowns.

**See Also:** [DROPDOWN_HANDLING.md](DROPDOWN_HANDLING.md) for detailed architecture and state machine documentation.

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

### Primary/Secondary Button Pattern (Language-Agnostic)

**Key Insight:** MTGA uses two consistent prompt buttons throughout all game phases:
- **`PromptButton_Primary`** - Always the "proceed/confirm" action
- **`PromptButton_Secondary`** - Always the "cancel/skip" action

The button **GameObject names** never change regardless of language. Only the **text labels** change.

**Declare Attackers Phase:**
- Primary: "All Attack" / "X Attackers" (German: "Alle angreifen" / "X Angreifer")
- Secondary: "No Attacks" (German: "Keine Angriffe")

**Declare Blockers Phase:**
- Primary: "No Blocks" → "X Blocker" / "Next" / "Confirm"
- Secondary: "No Blocks" / "Cancel Blocks"

**Main Phase:**
- Primary: "Pass" / "Resolve" / "Next" / "End Turn"
- Secondary: (varies by context)

**Language-Agnostic Detection:**
```csharp
// CORRECT - Find by GameObject name (works in any language)
foreach (var selectable in FindObjectsOfType<Selectable>())
{
    if (selectable.gameObject.name.Contains("PromptButton_Primary"))
    {
        string buttonText = UITextExtractor.GetButtonText(selectable.gameObject);
        UIActivator.SimulatePointerClick(selectable.gameObject);
        _announcer.Announce(buttonText, AnnouncementPriority.Normal);
    }
}

// WRONG - Text matching breaks with localization
if (buttonText.Contains("Attack")) // Fails in German!
```

**Implementation Pattern:**
1. Find button by name (`PromptButton_Primary` or `PromptButton_Secondary`)
2. Extract localized text for announcement
3. Click the button
4. Announce the localized text to user

This pattern is used in:
- `CombatNavigator` - Space clicks Primary, Backspace clicks Secondary
- `BrowserNavigator` - Space clicks Primary (confirm), Backspace clicks Secondary (cancel)
- `DiscardNavigator` - Finds Primary button and extracts count from text

### Input Field Text
- Empty fields contain zero-width space (U+200B), not empty string
- Always check for TMP_InputField BEFORE checking TMP_Text children
- Password fields: announce character count, not content

**Input Field Navigation (BaseNavigator):**
BaseNavigator caches the editing input field in `_editingInputField` to avoid expensive `FindObjectsOfType` calls during navigation. The `GetFocusedInputFieldInfo()` helper consolidates TMP_InputField and legacy InputField handling:
```csharp
private struct InputFieldInfo
{
    public bool IsValid;
    public string Text;
    public int CaretPosition;
    public bool IsPassword;
    public GameObject GameObject;
}
```
This helper is used by `AnnounceCharacterAtCursor()` and `AnnounceCurrentInputFieldContent()`.

### EventSystem Limitations
- `EventSystem.currentSelectedGameObject` is often null in MTGA
- Most screens use CustomButton/EventTrigger which don't register with EventSystem
- UIFocusTracker's OnFocusChanged event rarely fires due to this
- Navigation is handled by custom Navigator classes (GeneralMenuNavigator, DuelNavigator, etc.)
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
3. Toggle → toggle.isOn (with CustomButton handling if present)
4. **CustomButton → SimulatePointerClick + TryInvokeCustomButtonOnClick** (checked BEFORE Button)
5. Button → onClick.Invoke() (only if no CustomButton)
6. Child Button → onClick.Invoke()
7. Clickable in hierarchy → SimulatePointerClick on child
8. Fallback → SimulatePointerClick

**Why CustomButton before Button:** MTGA elements like Link_LogOut have BOTH Button and CustomButton components. The game logic responds to CustomButton pointer events, not Button.onClick. Checking CustomButton first ensures proper activation.

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
AccessibleArenaMod.Instance.ActivateCardDetails(element);
```

**Preparing for a card (used by navigators):**
```csharp
// Default (Hand zone)
AccessibleArenaMod.Instance.CardNavigator.PrepareForCard(element);
// With explicit zone
AccessibleArenaMod.Instance.CardNavigator.PrepareForCard(element, ZoneType.Battlefield);
```

**Info block order varies by zone:**
- Hand/Stack/Other: Name, Mana Cost, Power/Toughness, Type, Rules, Flavor, Artist
- Battlefield: Name, Power/Toughness, Type, Rules, Mana Cost, Flavor, Artist
- Selection browsers (SelectCards/SelectCardsMultiZone): Name, Rules, Mana Cost, Power/Toughness, Type, Flavor, Artist

On battlefield, mana cost is less important (card already in play), so it's shown after rules text.
In selection browsers, cards represent options with different effects, so rules text comes first. Left/Right navigation also announces rules text instead of card name.

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

**Blocker Phase Announcements (January 2026, enriched February 2026):**

The `CombatNavigator` tracks blocker selection and assignment during the declare blockers phase.
Uses **model-based detection** via `CardModelProvider` for attacking/blocking state, with UI fallback.
Resolves blocker-attacker relationships via `Instance.BlockingIds` / `Instance.BlockedByIds` fields.

*Two States Tracked:*
1. **Selected blockers** - Creatures clicked as potential blockers (have `SelectedHighlightBattlefield` + `CombatIcon_BlockerFrame`) - UI-only, no model equivalent
2. **Assigned blockers** - Creatures confirmed to block an attacker (model: `Instance.IsBlocking` property, UI fallback: active `IsBlocking` child)

*Announcements:*
- When selecting potential blockers: "3/4 blocking" (combined power/toughness)
- When assigning blockers to attackers: "Cat blocking Angel" (resolves `BlockingIds` to attacker name)
- Fallback if `BlockingIds` not yet populated: "Cat assigned"
- Navigating assigned blocker: "Cat, blocking Angel, 2 of 5"
- Navigating blocked attacker: "Angel, attacking, blocked by Cat, 3 of 5"
- Navigating unblocked attacker: "Angel, attacking, 3 of 5" (unchanged)

*Blocking Workflow:*
1. Click a potential blocker → "X/Y blocking" (combined P/T of selected blockers)
2. Click an attacker to assign the blocker(s) → "[Name] blocking [Attacker]"
3. Repeat for other blockers/attackers
4. Press Space to confirm all blocks

*Tracking Reset:*
- Selected blocker tracking clears when blockers are assigned (IsBlocking activates)
- Both trackers reset when entering/exiting the declare blockers phase
- This prevents the P/T announcement from persisting after assignment

*Model Fields on `MtgCardInstance` (accessed via reflection):*
- `IsAttacking` (property, bool) - true when declared as attacker
- `IsBlocking` (property, bool) - true when assigned as blocker
- `BlockingIds` (field, `List<uint>`) - InstanceIds of attackers this blocker is blocking
- `BlockedByIds` (field, `List<uint>`) - InstanceIds of blockers blocking this attacker
- Access chain: `GetDuelSceneCDC(card)` → `GetCardModel(cdc)` → `GetModelInstance(model)` → read prop/field

*Key Methods in CombatNavigator:*
```csharp
UpdateBlockerSelection()      // Called each frame, tracks both states
FindSelectedBlockers()        // Finds creatures with selection highlight + blocker frame
FindAssignedBlockers()        // Finds creatures with IsBlocking active
IsCreatureSelectedAsBlocker() // Checks selection highlight + blocker frame (UI-only)
IsCreatureAttacking()         // Model-first via CardModelProvider, UI fallback
IsCreatureBlocking()          // Model-first via CardModelProvider, UI fallback
GetBlockingText()             // Resolves BlockingIds → "blocking Angel"
GetBlockedByText()            // Resolves BlockedByIds → "blocked by Cat"
```

*UI Indicators (still used for "can block"/"can attack"/"selected to block"):*
- `CombatIcon_AttackerFrame` - creature can attack (during declare attackers)
- `CombatIcon_BlockerFrame` - creature can block (during declare blockers)
- `SelectedHighlightBattlefield` - creature is selected/highlighted
- `IsAttacking` child (active) - UI fallback for transitional states (Lobbed animations)
- `IsBlocking` child (active) - UI fallback for blocker detection
- `TappedIcon` - tapped state (non-attackers only)

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
3. **Combat Phase** (CombatNavigator) - Space during declare attackers/blockers
4. **Normal Mode** - Zone navigation

**Input Priority in DuelNavigator.HandleCustomInput():**
```
1. BrowserNavigator       → Scry/Surveil/Mulligan browsers
2. DiscardNavigator       → Enter/Space during discard mode
3. CombatNavigator        → Space during declare attackers/blockers
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
- Used by CombatNavigator for Space shortcut

**Combat Button Handling (Language-Agnostic):**
CombatNavigator uses the Primary/Secondary Button Pattern (see above):
- **Space** → Clicks `PromptButton_Primary` (confirm attackers/blockers)
- **Backspace** → Clicks `PromptButton_Secondary` (no attacks/cancel blocks)

The button text changes dynamically based on game state, but the function stays the same:
- Attackers: Primary cycles "All Attack" → "X Attackers" as you select
- Blockers: Primary shows "No Blocks" → "X Blocker" → "Next" as you assign

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

2. Register in `AccessibleArenaMod.InitializeServices()`:
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

**Index Validation:**
Use the `IsValidIndex` property instead of manual bounds checking:
```csharp
// Use this:
if (!IsValidIndex) return;

// Instead of:
if (_currentIndex < 0 || _currentIndex >= _elements.Count) return;
```

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

### Utility Classes for Navigators (February 2026)

When creating new navigators, **always use existing utility classes** instead of reimplementing functionality:

**UIActivator** - Element activation:
```csharp
// ALWAYS use UIActivator for clicking/activating UI elements
UIActivator.Activate(buttonObject);  // Proper game event triggering

// DON'T directly manipulate toggle state - game events won't fire
toggle.isOn = !toggle.isOn;  // BAD - filter logic won't trigger
```

**DropdownStateManager** - Dropdown mode tracking:
```csharp
// Call each frame to update dropdown state
DropdownStateManager.UpdateAndCheckExitTransition();

// Check if dropdown is open (blocks navigation)
if (DropdownStateManager.IsInDropdownMode)
{
    // Only handle close keys, let Unity handle dropdown navigation
    if (Input.GetKeyDown(KeyCode.Backspace))
    {
        CloseDropdown();
    }
    return; // Block all other navigation
}

// Notify when opening a dropdown
UIActivator.Activate(dropdownObject);
DropdownStateManager.OnDropdownOpened(dropdownObject);
```

**InputManager** - Key consumption:
```csharp
// Consume keys to prevent other navigators/game from processing them
InputManager.ConsumeKey(KeyCode.Backspace);
InputManager.ConsumeKey(KeyCode.Return);

// Check if key was consumed by another navigator
if (InputManager.IsKeyConsumed(KeyCode.Backspace))
{
    return true; // Already handled
}
```

**UITextExtractor** - Text extraction:
```csharp
string text = UITextExtractor.GetText(element);
string buttonText = UITextExtractor.GetButtonText(button, null);
```

**CardDetector** - Card detection:
```csharp
if (CardDetector.IsCard(element))
{
    var cardInfo = CardDetector.ExtractCardInfo(element);
}
```

**Example: AdvancedFiltersNavigator pattern**
```csharp
protected override void HandleInput()
{
    // 1. Update dropdown state each frame
    DropdownStateManager.UpdateAndCheckExitTransition();

    // 2. Block navigation while dropdown is open
    if (DropdownStateManager.IsInDropdownMode)
    {
        if (Input.GetKeyDown(KeyCode.Backspace))
            CloseActiveDropdown();
        return;
    }

    // 3. Consume keys when activating items that close the navigator
    if (Input.GetKeyDown(KeyCode.Return))
    {
        InputManager.ConsumeKey(KeyCode.Return);
        ActivateCurrentItem();
        return;
    }

    // 4. Use UIActivator for all activations
    UIActivator.Activate(item.GameObject);
}
```

### Overlay Navigator Pattern (January 2026)

Overlay navigators handle UI that can appear on top of ANY scene (e.g., Settings menu during duels).
They require special integration with lower-priority navigators to ensure proper handoff.

**Key Concept:** Higher-priority navigators take control when overlays appear. Lower-priority navigators
must explicitly yield by checking overlay state in both `DetectScreen()` and `ValidateElements()`.

**Implementation Pattern:**

1. **Create the overlay navigator** with high priority:
```csharp
public class SettingsMenuNavigator : BaseNavigator
{
    public override int Priority => 90;  // Higher than DuelNavigator (70), GeneralMenuNavigator (15)

    protected override bool DetectScreen()
    {
        // Use Harmony-tracked state from PanelStateManager for precise timing
        if (PanelStateManager.Instance?.IsSettingsMenuOpen != true)
            return false;
        // ... find content panel for element discovery
        return true;
    }
}
```

2. **Update lower-priority navigators** to yield when overlay is open:
```csharp
// In GeneralMenuNavigator and DuelNavigator:
protected override bool DetectScreen()
{
    // Don't activate when overlay is open
    if (PanelStateManager.Instance?.IsSettingsMenuOpen == true)
        return false;
    // ... normal detection logic
}

protected override bool ValidateElements()
{
    // Deactivate if overlay opens while we're active
    if (PanelStateManager.Instance?.IsSettingsMenuOpen == true)
    {
        LogDebug($"[{NavigatorId}] Settings menu detected - deactivating");
        return false;
    }
    return base.ValidateElements();
}
```

3. **Handle 0-element activation** - Overlays may need to activate before elements are discovered:
```csharp
public override void Update()
{
    if (!_isActive)
    {
        if (DetectScreen())
        {
            _elements.Clear();
            _currentIndex = -1;
            DiscoverElements();
            _isActive = true;  // Allow activation with 0 elements
            _currentIndex = _elements.Count > 0 ? 0 : -1;
            if (_elements.Count == 0)
                TriggerRescan();  // Schedule rescan to find elements
        }
        return;
    }
    base.Update();
}
```

**Why Both DetectScreen() AND ValidateElements():**
- `DetectScreen()` prevents activation when overlay is open
- `ValidateElements()` causes deactivation if overlay opens while navigator is active
- Without both, flip-flopping between navigators can occur during frame timing edge cases

**Using PanelStateManager for Detection:**
Always prefer `PanelStateManager.IsSettingsMenuOpen` over polling `GameObject.Find()`:
- PanelStateManager uses Harmony patches for precise event-driven detection
- No timing issues from polling during animations
- Single source of truth for panel state

**Files to modify when adding a new overlay navigator:**
1. Create `NewOverlayNavigator.cs` extending BaseNavigator
2. Add property to PanelStateManager (e.g., `IsNewOverlayOpen`)
3. Update lower-priority navigators to check the new property in both methods
4. Register navigator in AccessibleArenaMod.cs with appropriate priority

### Extracting Navigators from Existing Code (February 2026)

When extracting functionality from an existing navigator into a new dedicated navigator
(e.g., extracting rewards popup handling from GeneralMenuNavigator into RewardPopupNavigator),
follow these guidelines to avoid common pitfalls:

**Key Lessons Learned:**

1. **Copy code exactly first, then modify**
   - Copy working methods verbatim from the source navigator
   - Only after confirming the copied code works, make incremental changes
   - Resist the urge to "improve" or "simplify" the detection/discovery logic

2. **Timing issues with popup content**
   - Popup UI containers may exist before their content is populated
   - Detection may succeed but discovery finds 0 elements
   - Solution: Add automatic rescan mechanism with frame delay:
   ```csharp
   // Rescan mechanism for timing issues
   private int _rescanFrameCounter;
   private const int RescanDelayFrames = 30; // ~0.5 seconds at 60fps
   private const int MaxRescanAttempts = 10;
   private int _rescanAttempts;

   public override void Update()
   {
       // Check if we need to rescan (found 0 elements but popup still active)
       if (_isActive && _elementCount == 0 && _rescanAttempts < MaxRescanAttempts)
       {
           _rescanFrameCounter++;
           if (_rescanFrameCounter >= RescanDelayFrames)
           {
               _rescanFrameCounter = 0;
               _rescanAttempts++;
               ForceRescan();
           }
       }
       base.Update();
   }
   ```

3. **Navigator preemption**
   - New navigator needs higher priority to take over from existing one
   - NavigatorManager must support preemption (check higher-priority navigators even when one is active)
   - Example: RewardPopupNavigator (86) preempts GeneralMenuNavigator (15)
   ```csharp
   // In NavigatorManager.Update():
   if (_activeNavigator != null)
   {
       // Check if higher-priority navigator should take over
       foreach (var navigator in _navigators)
       {
           if (navigator.Priority <= _activeNavigator.Priority)
               break; // Sorted by priority, can stop early
           navigator.Update();
           if (navigator.IsActive)
           {
               _activeNavigator.Deactivate();
               _activeNavigator = navigator;
               return;
           }
       }
       // Continue with active navigator...
   }
   ```

4. **Search scope matters**
   - Original code may rely on being called in a specific context
   - When moving to standalone navigator, search scope may need adjustment
   - Example: Search entire popup instead of just RewardsCONTAINER

5. **Clean up the source**
   - After new navigator is working, remove duplicate code from source
   - Update overlay detection to note that navigation is handled elsewhere
   - Keep detection for overlay filtering (IsInsideActiveOverlay)

**Common Mistakes to Avoid:**
- Inventing new detection logic instead of copying working code
- Removing "unnecessary" fallbacks that handle edge cases
- Not handling the timing gap between popup appearance and content loading
- Forgetting to add preemption support in NavigatorManager

**Files typically modified:**
1. New `*Navigator.cs` file with copied methods
2. `NavigatorManager.cs` for preemption support
3. `AccessibleArenaMod.cs` to register new navigator
4. Source navigator to remove duplicate code
5. `OverlayDetector.cs` to update comments (detection kept for filtering)

### Adding Support for New Screens

MTGA has two main types of screens that need different implementation approaches:

#### 1. Content Screens (Full-Page Navigation)

Content screens are full-page views that replace the main content area (e.g., Home, Store, Decks, Rewards/Mastery, Profile).

**Characteristics:**
- Controlled by a `*ContentController` class (e.g., `HomePageContentController`, `ProgressionTracksContentController`)
- NavBar remains visible
- Backspace should return to Home
- Elements are filtered to only show content panel elements (not NavBar)

**Implementation Steps:**

1. **Add controller to ContentControllerTypes** in `MenuScreenDetector.cs`:
```csharp
private static readonly string[] ContentControllerTypes = new[]
{
    "HomePageContentController",
    // ... existing controllers ...
    "YourNewContentController"  // Add here
};
```

2. **Add display name mapping** in `MenuScreenDetector.GetContentControllerDisplayName()`:
```csharp
"YourNewContentController" => "Your Screen Name",
```

3. **Test backspace navigation** - should automatically work via `HandleContentPanelBack()` → `NavigateToHome()`

**Example:** Rewards/Mastery screen (ProgressionTracksContentController)
- Added to ContentControllerTypes array
- Added display name "Rewards"
- Backspace automatically closes screen via NavigateToHome()

#### 2. Overlay Panels (Slide-In/Popup Navigation)

Overlay panels appear on top of existing content without replacing it (e.g., Mailbox, Friends panel, Settings, PlayBlade).

**Characteristics:**
- Panel slides in from side or fades in as popup
- Background content remains visible but should be non-navigable
- Backspace should close the overlay (not navigate to Home)
- Elements should be filtered to only show overlay content

**Implementation Steps:**

1. **Add overlay detection** in `OverlayDetector.cs`:
```csharp
public bool IsInsideYourOverlay(GameObject element)
{
    return IsChildOf(element, "YourOverlay");
}
```

2. **Add to DetermineOverlayGroup** in `ElementGroupAssigner.cs`:
```csharp
if (_overlayDetector.IsInsideYourOverlay(element))
    return (ElementGroup.YourOverlay, true);
```

3. **Add ElementGroup enum** in `ElementGroup.cs`:
```csharp
YourOverlay,
```

4. **Add backspace handler** in `GeneralMenuNavigator.HandleBackNavigation()`:
```csharp
ElementGroup.YourOverlay => CloseYourOverlay(),
```

5. **Add close method** in `GeneralMenuNavigator`:
```csharp
private bool CloseYourOverlay()
{
    // Find and click close button, or invoke controller method
    return true;
}
```

6. **If button activation doesn't work**, add special handling in `UIActivator`:
```csharp
if (elementName == "Nav_YourButton")
{
    // Use reflection to invoke controller method directly
    var controller = FindController("YourController");
    controller.OpenMethod();
    return new ActivationResult(true, "YourButton", ActivationType.Button);
}
```

7. **Add Harmony patches** in `PanelStatePatch.cs` if needed for open/close detection

**Example:** Mailbox overlay
- Added `IsInsideMailbox()` in OverlayDetector
- Added `ElementGroup.Mailbox`
- Added `CloseMailbox()` handler for backspace
- Added special UIActivator handling for Nav_Mail (onClick had no listeners)
- Added Harmony patches for `NavBarController.MailboxButton_OnClick()` and `HideInboxIfActive()`

#### Decision Tree: Content Screen vs Overlay Panel vs Transitional Screen

```
Is the new screen...
├── Replacing the main content area entirely?
│   └── YES → Content Screen
│       - Add to ContentControllerTypes
│       - Add display name mapping
│       - Backspace → NavigateToHome()
│
├── Appearing on top of existing content?
│   └── YES → Overlay Panel
│       - Add overlay detection
│       - Add ElementGroup
│       - Add custom backspace handler
│       - May need UIActivator special handling
│       - May need Harmony patches
│
└── Short-lived scene between game states? (loading, matchmaking, results)
    └── YES → Transitional Screen (LoadingScreenNavigator)
        - Add new ScreenMode enum value
        - Add scene detection + element discovery methods
        - Wire into 5 switch statements
        - Add to GeneralMenuNavigator ExcludedScenes
        - See "Transitional/Loading Screens" section above
```

#### Common Pitfalls

1. **Button onClick has no listeners**: Some NavBar buttons (like Nav_Mail) have empty onClick events. The actual logic is in a separate controller method that must be invoked via reflection.

2. **Content controller not in list**: If backspace doesn't work on a content screen, check if the controller is in `ContentControllerTypes` array.

3. **Overlay elements appearing when closed**: Check overlay detection logic - the panel GameObject may exist but be inactive.

4. **Double announcements**: Ensure only ONE detection method (Harmony OR reflection OR alpha) is used per panel type.

#### 3. Transitional/Loading Screens (LoadingScreenNavigator)

Transitional screens are short-lived scenes that appear between major game states (e.g., match end, matchmaking queue, loading). They differ from content screens and overlays because they use additive scene loading and have late-loading UI.

**Characteristics:**
- Loaded as additive scenes (not replacing MainNavigation content)
- UI elements appear after the scene loads (animations, network responses)
- Short-lived (seconds to tens of seconds)
- Few interactive elements (1-3 buttons) plus info text
- No NavBar content panels - standalone scenes

**When to use LoadingScreenNavigator:**
Add a new `ScreenMode` to `LoadingScreenNavigator` instead of creating a separate navigator. This keeps all transitional screen logic in one place with shared polling infrastructure.

**Implementation Steps for a New Loading Screen Mode:**

1. **Identify the scene**: Check MelonLoader logs for scene names during the transition. Look for `Scene loaded: SceneName` entries.

2. **Add ScreenMode enum value** in LoadingScreenNavigator:
```csharp
private enum ScreenMode { None, MatchEnd, PreGame, Matchmaking, YourNewMode }
```

3. **Add detection method**:
```csharp
private bool DetectYourNewMode()
{
    // Iterate additive scenes to find yours
    for (int i = 0; i < SceneManager.sceneCount; i++)
    {
        if (SceneManager.GetSceneAt(i).name == "YourSceneName")
            return true;
    }
    return false;
}
```

4. **Add element discovery method** (scene-scoped search):
```csharp
private void DiscoverYourNewModeElements()
{
    var scene = SceneManager.GetSceneByName("YourSceneName");
    if (!scene.IsValid() || !scene.isLoaded) return;

    var rootObjects = scene.GetRootGameObjects();

    // Dump hierarchy on first poll for debugging
    if (!_dumpedHierarchy)
    {
        _dumpedHierarchy = true;
        foreach (var root in rootObjects)
            DumpHierarchy(root.transform, 0, 4);
    }

    // Search by specific element names (not generic TMP_Text sweep)
    foreach (var root in rootObjects)
    {
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            switch (text.gameObject.name)
            {
                case "your_text_element":
                    if (text.gameObject.activeInHierarchy)
                        AddElement(text.gameObject, text.text?.Trim());
                    break;
            }
        }
    }

    // Always add Nav_Settings if visible (global element in NavBar scene)
    var settings = GameObject.Find("Nav_Settings");
    if (settings != null && settings.activeInHierarchy)
        AddElement(settings, "Settings, button");
}
```

5. **Wire into existing switch statements** (5 places):
   - `DetectScreen()` - call your detection method, set `_currentMode`
   - `DiscoverElements()` - call your discovery method
   - `GetScreenName()` - return screen name string
   - `GetActivationAnnouncement()` - return activation message
   - `ValidateElements()` - call your detection method to verify still active

6. **Add to GeneralMenuNavigator ExcludedScenes**:
```csharp
private static readonly HashSet<string> ExcludedScenes = new HashSet<string>
{
    "Bootstrap", "AssetPrep", "DuelScene", ..., "YourSceneName"
};
```

7. **Add Backspace handler** if the mode has a primary action (cancel, continue):
```csharp
case ScreenMode.YourNewMode:
    if (_yourButton != null && _yourButton.activeInHierarchy)
    {
        UIActivator.SimulatePointerClick(_yourButton);
        return true;
    }
    break;
```

8. **Add OnElementActivated handler** if buttons need special activation:
```csharp
if (_currentMode == ScreenMode.YourNewMode)
{
    UIActivator.SimulatePointerClick(element);
    return true;
}
```

**Key Patterns for Loading Screens:**
- **Always use scene-scoped search**: `scene.GetRootGameObjects()` then `GetComponentsInChildren` within each root. Never use `FindObjectsOfType` which crosses scene boundaries.
- **Search by name, not generic sweep**: Target specific element names to avoid picking up decorative/internal text.
- **CanvasGroup filtering**: Check `alpha > 0 && interactable` to filter invisible elements from other scenes.
- **Preserve navigation index on poll**: Save and restore `_currentIndex` during element rebuild.
- **Combine related text**: Merge label + value elements (e.g. "Wait time:" + "0:05") into single navigable items.
- **Filter useless text**: Player names, placeholder text, duplicate elements.

**Debugging a New Loading Screen:**
1. Enable `DumpHierarchy` (set `_dumpedHierarchy = false` before activation)
2. Check MelonLoader log for the full hierarchy dump
3. Identify element names, which are INACTIVE, which have useful text
4. Note which elements appear late (INACTIVE on first dump, active on later polls)

### Special Activation Cases
Some elements (NPE chest/deck boxes) need controller reflection:
- Find controller via `GameObject.FindObjectOfType<NPEContentControllerRewards>()`
- Call methods like `Coroutine_UnlockAnimation()`, `OnClaimClicked_Unity()`
- See `GeneralMenuNavigator.FindNPERewardCards()` for NPE reward handling

## Card Handling in Navigators

### Automatic Card Navigation on Tab
BaseNavigator provides `UpdateCardNavigation()` which handles card navigation automatically:
```csharp
// Called internally by Move(), MoveFirst(), MoveLast(), and TryActivate()
// Checks SupportsCardNavigation internally - no need to wrap the call
private void UpdateCardNavigation()
{
    if (!SupportsCardNavigation) return;

    var cardNavigator = AccessibleArenaMod.Instance?.CardNavigator;
    if (cardNavigator == null) return;

    if (!IsValidIndex)
    {
        cardNavigator.Deactivate();
        return;
    }

    var element = _elements[_currentIndex].GameObject;
    if (element != null && CardDetector.IsCard(element))
    {
        cardNavigator.PrepareForCard(element);
    }
    else if (cardNavigator.IsActive)
    {
        cardNavigator.Deactivate();
    }
}
```

This is called automatically by BaseNavigator's navigation methods. Subclasses don't need to call it manually.

### Manual Card Activation on Enter (legacy)
When activating an element with Enter:
```csharp
private void ActivateElement(int index)
{
    var element = _elements[index].GameObject;

    // Check if card - delegate to central CardInfoNavigator
    if (CardDetector.IsCard(element))
    {
        if (AccessibleArenaMod.Instance.ActivateCardDetails(element))
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

1. When Harmony fires (e.g., `SettingsMenu.Open()` postfix), PanelStateManager tracks the state.

2. During element discovery, `ShouldShowElement()` uses a unified foreground layer system:
   ```csharp
   private bool ShouldShowElement(GameObject obj)
   {
       var layer = GetCurrentForeground();  // Single source of truth
       return layer switch
       {
           ForegroundLayer.Settings => IsChildOfSettings(obj),
           ForegroundLayer.Popup => IsChildOfPopup(obj),
           ForegroundLayer.Social => IsChildOfSocialPanel(obj),
           ForegroundLayer.PlayBlade => IsInsideBlade(obj),
           ForegroundLayer.NPE => IsInsideNPEOverlay(obj),
           ForegroundLayer.ContentPanel => IsChildOfContentPanel(obj),
           _ => true  // Home, None - show all
       };
   }
   ```

3. Backspace navigation uses the same `GetCurrentForeground()` to determine what to close.

**Unified Foreground/Backspace System (January 2026):**

The `ForegroundLayer` enum defines all overlay states in priority order:
- Settings (highest) > Popup > Social > PlayBlade > NPE > ContentPanel > Home

Both element filtering AND backspace navigation derive from `GetCurrentForeground()`:
- Adding a new screen type: add to enum + GetCurrentForeground() + both switch statements
- Filtering and navigation can never get out of sync

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

## Panel Detection Strategy (Updated February 2026)

MTGA's UI is inconsistent - different panels were built by different developers with different
patterns. There is no single detection method that works for everything. This section documents
the architecture and decision tree for choosing the right detection approach.

### Architecture Overview

**Central Coordinator: `PanelStateManager`** (`src/Core/Services/PanelDetection/PanelStateManager.cs`)

All panel detection flows through PanelStateManager, which:
- Owns and coordinates three specialized detectors
- Maintains a priority-sorted stack of active panels
- Fires events that navigators subscribe to (`OnPanelChanged`, `OnAnyPanelOpened`)
- Tracks PlayBlade state separately for blade-specific handling
- Provides `GetFilterPanel()` for element filtering

**Three Specialized Detectors:**

```
┌─────────────────────────────────────────────────────────────────────┐
│                        PanelStateManager                            │
│                     (Single Source of Truth)                        │
├─────────────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐     │
│  │ HarmonyDetector │  │ReflectionDetect │  │  AlphaDetector  │     │
│  │  (Event-driven) │  │   (Polling)     │  │   (Polling)     │     │
│  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘     │
│           │                    │                    │               │
│           └────────────────────┼────────────────────┘               │
│                                │                                    │
│                    ReportPanelOpened/Closed()                       │
└─────────────────────────────────────────────────────────────────────┘
```

### Available Detection Methods

**HarmonyPanelDetector** (`HarmonyPanelDetector.cs`)
- Trigger: Event-driven via Harmony patches on property setters
- Best for: Panels with patchable Show/Hide methods or property setters
- Handles: PlayBlade, Settings, Blades, SocialUI, NavContentController

**ReflectionPanelDetector** (`ReflectionPanelDetector.cs`)
- Trigger: Polling IsOpen properties every 10 frames
- Best for: Controller-based panels, Login scene panels
- Handles: PopupBase descendants, Login panels (Panel - WelcomeGate, etc.)

**AlphaPanelDetector** (`AlphaPanelDetector.cs`)
- Trigger: Polling CanvasGroup alpha every 10 frames
- Best for: Fade-in popups without IsOpen property
- Handles: SystemMessageView, Dialog, Modal, InviteFriend, Popup (not PopupBase)

### Panel Properties Reference

**Panel Type** | **Detection** | **Handled By**
--- | --- | ---
PlayBladeController | PlayBladeVisualState setter | HarmonyDetector
BladeContentView | Show/Hide methods | HarmonyDetector
EventBladeContentView | Show/Hide methods | HarmonyDetector
SocialUI/FriendsWidget | Various methods | HarmonyDetector
SettingsMenu | Harmony patches | HarmonyDetector
NavContentController | FinishOpen/FinishClose | HarmonyDetector
PopupBase | IsOpen polling | ReflectionDetector
Login panels | Name pattern + active | ReflectionDetector
SystemMessageView | Alpha polling | AlphaDetector
Dialog/Modal | Alpha polling | AlphaDetector
InviteFriend | Alpha polling | AlphaDetector

### Decision Tree for New Panels

When adding support for a new panel/screen, follow this decision tree:

```
1. Does it have Show/Hide methods or property setters we can patch?
   └── YES → Use HarmonyDetector
   │         - Add patch in src/Patches/PanelStatePatch.cs
   │         - Add pattern to HarmonyPatterns in HarmonyPanelDetector.cs
   └── NO → Continue to step 2

2. Does it have IsOpen property (or similar boolean state)?
   └── YES → Use ReflectionDetector
   │         - Add type to ControllerTypes in ReflectionPanelDetector.cs
   │         - Or add pattern to LoginPanelPatterns if login-related
   └── NO → Continue to step 3

3. Does it use alpha fade for visibility (CanvasGroup)?
   └── YES → Use AlphaDetector
   │         - Add pattern to AlphaPatterns in AlphaPanelDetector.cs
   └── NO → Continue to step 4

4. Create a custom navigator with Direct detection
   └── Use GameObject.Find() with specific name patterns
   └── Check for unique child elements to confirm state
```

### Detector Ownership (Panel → Detector Mapping)

**HarmonyDetector owns** (event-driven, patterns in `HarmonyPatterns`):
- `playblade` - PlayBladeController and PlayBlade variants
- `settings` - SettingsMenu, SettingsMenuHost
- `socialui` - Social panel
- `friendswidget` - Friends widget
- `eventblade` - Event blade content
- `findmatchblade` - Find match blade
- `deckselectblade` - Deck select blade
- `bladecontentview` - All blade content views

**ReflectionDetector owns** (polling, patterns in `ControllerTypes` + `LoginPanelPatterns`):
- `PopupBase` descendants - Popups with IsOpen property
- Login panels: `Panel - WelcomeGate`, `Panel - Log In`, `Panel - Register`,
  `Panel - ForgotCredentials`, `Panel - AgeGate`, `Panel - Language`,
  `Panel - Consent`, `Panel - EULA`, `Panel - Marketing`, `Panel - Terms`,
  `Panel - Privacy`, `Panel - UpdatePolicies`

**AlphaDetector owns** (polling, patterns in `AlphaPatterns`):
- `systemmessageview` - Confirmation dialogs
- `dialog` - Dialog popups
- `modal` - Modal popups
- `invitefriend` - Friend invite popup
- `popup` (but NOT `popupbase`) - Generic popup overlays

### Critical Rules

1. **ONE detector per panel** - Each `HandlesPanel()` method excludes panels owned by other detectors. Never detect the same panel with multiple methods (causes double announcements).

2. **Harmony for PlayBlade is mandatory** - PlayBlade uses slide animation (alpha stays 1.0), so alpha detection cannot work.

3. **Alpha thresholds at extremes** - AlphaDetector uses 0.99 for "visible" and 0.01 for "hidden" to detect only when animations are complete, not mid-animation.

4. **Detectors self-exclude** - ReflectionDetector's `HandlesPanel()` explicitly excludes Harmony patterns and Alpha patterns. AlphaDetector's `HandlesPanel()` explicitly includes only its patterns.

5. **Single announcement source** - Let the navigation system's `GetActivationAnnouncement()` be the single source of screen announcements. Don't announce from detection callbacks.

### Adding a New Panel

1. **Analyze the panel** - Check what properties it has:
   ```csharp
   // Use PanelAnimationDiagnostic (F11) or manual inspection
   var type = panel.GetType();
   bool hasIsOpen = type.GetProperty("IsOpen") != null;
   bool hasShowHide = type.GetMethod("Show") != null && type.GetMethod("Hide") != null;
   var canvasGroup = panel.GetComponent<CanvasGroup>();
   ```

2. **Choose detection method** using decision tree above

3. **Register with ONE detector only:**
   - Harmony: Add patch in `src/Patches/PanelStatePatch.cs`, add pattern to `HarmonyPatterns` in `HarmonyPanelDetector.cs`
   - Reflection: Add type to `ControllerTypes` in `ReflectionPanelDetector.cs`, or pattern to `LoginPanelPatterns`
   - Alpha: Add pattern to `AlphaPatterns` in `AlphaPanelDetector.cs`
   - Direct: Create custom navigator extending `BaseNavigator`

4. **Update exclusion lists** if needed - Ensure other detectors' `HandlesPanel()` methods exclude the new panel

5. **Document in Detector Ownership section** - Add entry above

6. **Test for double announcements** - Ensure only ONE detector reports the panel

### Legacy Files (Reference Only)

These older files are still present but less actively used:
- `MenuPanelTracker.cs` - Provides `IsChildOf()` utility method used by OverlayDetector
- `UnifiedPanelDetector.cs` - Older alpha-based detector (functionality now in AlphaPanelDetector)

## Debugging Tips

1. **Scan for panel by name:** `GameObject.Find("Panel - Name(Clone)")`
2. **Check EventSystem:** Log `currentSelectedGameObject` on Tab
3. **Identify element types:** Button vs CustomButton vs Image
4. **Test Toggle behavior:** Does selecting trigger state change?
5. **Find elements by path:** More reliable than name search
6. **Log all components:** On problematic elements to understand structure

### DebugConfig - Centralized Debug Logging

`DebugConfig` (`src/Core/Services/DebugConfig.cs`) provides centralized control over debug logging. All debug output should use this system instead of direct MelonLogger calls.

**Usage:**
```csharp
// Check if debug logging is enabled for a category
if (DebugConfig.LogFocusTracking)
    MelonLogger.Msg($"[Focus] {message}");

// Or use the helper method
DebugConfig.LogIf(DebugConfig.LogFocusTracking, "Focus", message);
```

**Available Flags:**
- `LogFocusTracking` - UIFocusTracker focus changes
- `LogPanelDetection` - Panel open/close events
- `LogElementDiscovery` - Element scanning and filtering
- `LogNavigation` - Navigation state changes
- `LogActivation` - UI element activation
- `LogCardDetection` - Card detection and info extraction
- `LogDropdownState` - Dropdown mode tracking

**Enable/Disable:**
Edit `DebugConfig.cs` to enable flags during development:
```csharp
public static bool LogFocusTracking = true; // Enable for focus debugging
```

**Benefits:**
- Single place to enable/disable categories
- Consistent log tag format (`[Category] message`)
- No scattered boolean flags across navigators
- Easy to disable all debug logging for release

### MenuDebugHelper - UI Investigation Utilities

`MenuDebugHelper` (`src/Core/Services/MenuDebugHelper.cs`) provides reusable methods for investigating unknown UI elements. Use these instead of writing ad-hoc debug logging.

**DumpGameObjectDetails(tag, obj, maxDepth)** - Comprehensive element investigation
```csharp
// Dump full hierarchy of an unknown UI element
MenuDebugHelper.DumpGameObjectDetails("MyTag", someGameObject, 3);

// Output in log:
// [MyTag] === DUMP: ObjectiveGraphics ===
// [MyTag] Path: Home/SafeArea/ObjectivesLayout/Objective_Base(Clone)/ObjectiveGraphics
// [MyTag] Active: True
// [MyTag] [0] ObjectiveGraphics (RectTransform, CanvasRenderer, CustomButton)
// [MyTag]   [1] TextLine Text='Quest description' [Localize]
// [MyTag]     [2] Text_Description Text=(empty)
// [MyTag]   [1] Circle Text='500'
// [MyTag]   [1] Text_GoalProgress Text='14/20'
// [MyTag] === END DUMP ===
```

**What it logs for each element:**
- Object name, active state, full path
- All components (except Transform)
- Text content from TMP_Text or Text components (shows "(empty)" if text is empty string)
- `[Localize]` marker if element has Localize component (may have localized text)
- `[Tooltip]` marker if element has TooltipTrigger component
- `[INACTIVE]` marker for inactive elements
- Recursively logs children up to maxDepth (default: 3)

**When to use:**
- Investigating unknown UI elements to find where text content lives
- Understanding element hierarchy before writing extraction code
- Finding tooltip or localization components
- Debugging why text extraction returns unexpected results

**Example workflow (objectives investigation):**
```csharp
// 1. Add temporary debug call in navigator
if (buttonObj.name == "ObjectiveGraphics" && buttonObj.transform.parent.name.Contains("SparkRank"))
{
    MenuDebugHelper.DumpGameObjectDetails(NavigatorId, buttonObj, 4);
}

// 2. Build, deploy, run game, check log
// 3. Find the element structure (TextLine, Circle, Text_GoalProgress, etc.)
// 4. Write proper extraction code in UITextExtractor
// 5. Remove debug call
```

**LogTooltipTriggerDetails(tag, tooltipTrigger)** - Tooltip content extraction
```csharp
// Log tooltip content from a TooltipTrigger component
var tooltip = element.GetComponent<MonoBehaviour>();
if (tooltip?.GetType().Name == "TooltipTrigger")
{
    MenuDebugHelper.LogTooltipTriggerDetails("MyTag", tooltip);
}
```

**DumpFocusedCard(tag, card)** - Card-specific debugging (F11 key)
- Triggered by F11 during gameplay
- Logs card name, CDC component, Model properties
- Useful for debugging cards that fail text extraction

**GetFullPath(transform)** - Get full hierarchy path
```csharp
string path = MenuDebugHelper.GetFullPath(element.transform);
// Returns: "Home/SafeArea/ObjectivesLayout/Objective_Base(Clone)/ObjectiveGraphics"
```

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

**FriendsWidget Exception (January 2026):**
Elements inside `FriendsWidget` (detected via `IsInsideFriendsWidget()`) bypass several filters:
- Parent CanvasGroup interactable check - FriendsWidget uses non-standard interactable patterns
- Small image-only button filter - `Backer_Hitbox` elements have 0x0 size but are clickable
- Hitbox/backer name filters - These ARE the clickable friend items in FriendsWidget

The exception requires elements to have meaningful text via `GetText()` (not just be inside
FriendsWidget). Text is derived from parent object names (e.g., `Button_AddFriend` → "add friend").

**NavBar RightSideContainer Exception (January 2026):**
Elements inside `RightSideContainer` (detected via `IsInsideNavBarRightSide()`) bypass the small
image-only button filter. These are functional NavBar icon buttons:
- `Nav_Learn` - Tutorial/help system
- `Nav_Mail` - Inbox/messages
- `Nav_Settings` - Settings menu
- `Nav_DirectChallenge` - Challenge a friend

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
- Numeric-only text in mail/notification elements - Badge counts (but NOT if element has CustomButton, e.g., Nav_Mail showing unread count "21")

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

1. **Content Controller Filtering** (`ShouldShowElement` + `IsChildOfContentPanel`):
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

## Browser Card Interactions (CardGroupProvider Pattern)

**Added January 2026:**

Some browser UIs (like London mulligan) don't respond to standard click simulation. Their cards require
drag-based interaction. The solution is to access the browser's internal API via `CardGroupProvider`.

### Pattern Overview

1. Find the browser holder (e.g., `BrowserCardHolder_Default`)
2. Get `CardBrowserCardHolder` component
3. Access `CardGroupProvider` property (returns browser instance like `LondonBrowser`)
4. Use browser's internal methods for card manipulation

### Getting the Browser Instance

```csharp
// Find holder and get browser instance
var holder = FindActiveGameObject("BrowserCardHolder_Default");
Component cardBrowserHolder = holder.GetComponents<Component>()
    .FirstOrDefault(c => c.GetType().Name == "CardBrowserCardHolder");

var providerProp = cardBrowserHolder.GetType().GetProperty("CardGroupProvider",
    BindingFlags.Public | BindingFlags.Instance);
var browser = providerProp?.GetValue(cardBrowserHolder);
// browser is now LondonBrowser, ScryBrowser, etc.
```

### Common Browser Methods (via reflection)

**Card Lists:**
- `GetHandCards()` - Returns cards in "keep" pile (hand group)
- `GetLibraryCards()` - Returns cards in "bottom" pile (library group)

**Card Position Check:**
- `IsInHand(DuelScene_CDC card)` - True if card is in keep pile
- `IsInLibrary(DuelScene_CDC card)` - True if card is in bottom pile
- `CanChangeZones(DuelScene_CDC card)` - True if card can be moved

**Zone Positions:**
- `HandScreenSpace` (Vector2) - Screen position of keep pile
- `LibraryScreenSpace` (Vector2) - Screen position of bottom pile

**Card Movement (Drag Simulation):**
```csharp
// 1. Get target position (opposite of current zone)
var targetProp = browser.GetType().GetProperty(isInHand ? "LibraryScreenSpace" : "HandScreenSpace");
var targetPos = (Vector2)targetProp.GetValue(browser);

// 2. Move card transform to target position
Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(targetPos.x, targetPos.y, 10f));
card.transform.position = worldPos;

// 3. Invoke drag methods
var handleDrag = browser.GetType().GetMethod("HandleDrag");
var onDragRelease = browser.GetType().GetMethod("OnDragRelease");
handleDrag.Invoke(browser, new object[] { cardCDC });
onDragRelease.Invoke(browser, new object[] { cardCDC });
```

### Filtering Invalid Cards

Browser card lists may include placeholder cards (CDC #0). Filter these out:

```csharp
foreach (var cardCDC in handCards)
{
    if (cardCDC is Component comp && comp.gameObject != null)
    {
        var cardName = CardDetector.GetCardName(comp.gameObject);
        // Skip placeholders
        if (!string.IsNullOrEmpty(cardName) &&
            cardName != "Unknown card" &&
            !comp.gameObject.name.Contains("CDC #0"))
        {
            validCards.Add(comp.gameObject);
        }
    }
}
```

### When to Use This Pattern

Use the CardGroupProvider pattern when:
- Standard click simulation (`UIActivator.SimulatePointerClick`) doesn't work
- Cards are in a browser context (scry, surveil, London mulligan, etc.)
- Cards need to be moved between visual zones/piles

**Known Working:**
- London Mulligan (`LondonBrowser`) - keep/bottom pile selection

**Potentially Applicable (untested):**
- Scry/Surveil reordering
- Other card browsers with drag-based interaction

## Animation Detection Patterns

**Added January 2026:**

When waiting for UI animations to complete before scanning/interacting, there are two approaches:

### 1. CanvasGroup Alpha Check (RECOMMENDED for popups/fade animations)

```csharp
/// <summary>
/// Get the minimum alpha value from all CanvasGroups on an element.
/// </summary>
private float GetMinCanvasGroupAlpha(GameObject element)
{
    if (element == null) return -1f;

    float minAlpha = 1f;
    var canvasGroups = element.GetComponentsInChildren<CanvasGroup>();
    foreach (var cg in canvasGroups)
    {
        if (cg == null) continue;
        if (cg.alpha < minAlpha)
            minAlpha = cg.alpha;
    }

    return canvasGroups.Length > 0 ? minAlpha : -1f;
}

// Usage:
float alpha = GetMinCanvasGroupAlpha(popup);
if (alpha >= 0.5f)
{
    // Popup is visible - safe to scan/interact
}
else if (alpha < 0.1f)
{
    // Popup is closing/closed
}
```

**Characteristics:**
- Alpha changes quickly during fade animations (~0.2-0.3 seconds to reach 1.0)
- Reliable indicator of visual visibility
- Works for both fade-in (alpha rising) and fade-out (alpha dropping)
- Simple to implement, no reflection needed
- **Use for:** Popup open/close detection, fade animations, visibility checks

### 2. Animator IsInTransition Check (USE WITH CAUTION)

```csharp
/// <summary>
/// Check if any Animator on the element is currently transitioning.
/// Uses reflection to avoid requiring AnimationModule reference.
/// </summary>
private bool IsAnimatorTransitioning(GameObject element)
{
    if (element == null) return false;

    var components = element.GetComponentsInChildren<Component>();
    foreach (var component in components)
    {
        if (component == null) continue;
        var type = component.GetType();
        if (type.Name != "Animator") continue;

        // Check if animator is enabled
        var enabledProp = type.GetProperty("isActiveAndEnabled");
        if (enabledProp != null)
        {
            bool enabled = (bool)enabledProp.GetValue(component);
            if (!enabled) continue;
        }

        // Check if animator is in transition
        var isInTransitionMethod = type.GetMethod("IsInTransition");
        if (isInTransitionMethod != null)
        {
            try
            {
                bool inTransition = (bool)isInTransitionMethod.Invoke(component, new object[] { 0 });
                if (inTransition) return true;
            }
            catch { }
        }

        // Check normalizedTime for animation progress
        var getStateInfo = type.GetMethod("GetCurrentAnimatorStateInfo");
        if (getStateInfo != null)
        {
            try
            {
                var stateInfo = getStateInfo.Invoke(component, new object[] { 0 });
                var normalizedTimeProp = stateInfo.GetType().GetProperty("normalizedTime");
                if (normalizedTimeProp != null)
                {
                    float normalizedTime = (float)normalizedTimeProp.GetValue(stateInfo);
                    if (normalizedTime < 1.0f) return true;
                }
            }
            catch { }
        }
    }

    return false;
}
```

**Characteristics:**
- **UNRELIABLE for popup fade animations** - tested to return true for ~1.2 seconds even after popup is fully visible
- Popup animators have looping idle animations that keep "transitioning" state active
- Requires reflection to access Animator methods (AnimationModule not referenced in project)
- **May be useful for:** Button press animations, panel slide animations, discrete state transitions
- **Do NOT use for:** Popup open/close timing, fade-in detection

### Recommendation Summary

| Animation Type | Recommended Method | Why |
|----------------|-------------------|-----|
| Popup fade-in/fade-out | Alpha check | Fast, reliable, simple |
| Modal dialog visibility | Alpha check | Direct visibility indicator |
| Button press feedback | Animator check | Discrete state, no looping |
| Panel slide animations | Animator check | Position-based, not alpha |
| Element hover effects | Animator check | Short, discrete animations |

### Popup Detection with Alpha

When detecting if a popup is ready for interaction:

```csharp
// In popup detection (CheckForNewPopups)
if (HasActiveButtonChild(popup, "SystemMessageButton") && GetMinCanvasGroupAlpha(popup) >= 0.5f)
{
    // Popup is visible AND has interactive buttons
    isModalPopup = true;
}

// In animation wait completion check
float minAlpha = GetMinCanvasGroupAlpha(popup);
if (minAlpha >= 0.5f)
{
    // Popup visible - trigger rescan to find buttons
    CompleteAnimationWait();
}
else if (minAlpha < 0.1f && waitTimer > 0.5f)
{
    // Popup closing - cancel wait without rescan
    CancelAnimationWait();
}
```

## Common Gotchas

- MelonGame attribute is case sensitive: `"Wizards Of The Coast"`
- NPE reward chest/deck boxes need controller reflection, not pointer events
- Mana costs use sprite tags (`<sprite name="xW">`) - parse with regex for symbol names
- ManaCost text elements need special handling: CleanText() strips all tags leaving empty content,
  so skip the empty-content check for ManaCost to allow ParseManaCost() to process raw sprite tags
- CardDetector cache must be cleared on scene changes (stale references)
- CustomButton.OnClick may have 0 listeners - direct invocation does nothing
- EventSystem.currentSelectedGameObject is often null - game uses custom navigation
- Card navigation must be prepared by navigators (GeneralMenuNavigator, DuelNavigator), not UIFocusTracker
- CardInfoNavigator uses lazy loading - PrepareForCard() is fast, LoadBlocks() extracts info
