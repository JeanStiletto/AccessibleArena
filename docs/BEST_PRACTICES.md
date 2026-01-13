# MTGA Accessibility Mod - Best Practices

## Game Architecture

### Unity/MTGA Basics
- Engine: Unity with Mono runtime
- Game location: `C:\Program Files\Wizards of the Coast\MTGA`
- Key assemblies in `MTGA_Data\Managed`:
  - `Core.dll` - Game interfaces and systems (10,632 types)
  - `Assembly-CSharp.dll` - Main game code (HasbroGo namespace, login menus)
  - `Wizards.Arena.Models.dll` - Data models

### Card Identifiers
- `grpId` (UInt32): Unique card identifier across the system
- `InstanceId` (UInt32): In-game card instance on battlefield

### DuelScene Zone Architecture
Discovered via game code analysis and log inspection. Zone holders are GameObjects with specific naming patterns.

**Zone Holder Names (from game code):**

**Your Hand**
- GameObject: `LocalHand_Desktop_16x9`
- ZoneId: #31
- Type: Hand

**Opponent Hand**
- GameObject: `OpponentHand_Desktop_16x9`
- ZoneId: #35
- Type: Hand

**Battlefield**
- GameObject: `BattlefieldCardHolder`
- ZoneId: #28
- Type: Battlefield

**Your Graveyard**
- GameObject: `LocalGraveyard`
- ZoneId: #33
- Type: Graveyard

**Opponent Graveyard**
- GameObject: `OpponentGraveyard`
- ZoneId: #37
- Type: Graveyard

**Exile**
- GameObject: `ExileCardHolder`
- ZoneId: #29
- Type: Exile

**Stack**
- GameObject: `StackCardHolder_Desktop_16x9`
- ZoneId: #27
- Type: Stack

**Your Library**
- GameObject: `LocalLibrary`
- ZoneId: #32
- Type: Library

**Opponent Library**
- GameObject: `OpponentLibrary`
- ZoneId: #36
- Type: Library

**Command**
- GameObject: `CommandCardHolder`
- ZoneId: #26
- Type: Command

**Zone metadata embedded in GameObject names:**
```
LocalHand_Desktop_16x9 ZoneId: #31 | Type: Hand | OwnerId: #1
```
Parse with regex: `ZoneId:\s*#(\d+)`, `OwnerId:\s*#(\d+)`

**Card Detection in Zones:**
Cards are children of zone holders. Detection patterns:
- Name prefix: `CDC #` (Card Display Controller)
- Name contains: `CardAnchor`
- Component types: `CDCMetaCardView`, `CardView`, `DuelCardView`, `Meta_CDC`

**Key Game Classes (from decompiled Core.dll):**
- `GameManager` - Central manager with `CardHolderManager`, `ViewManager`, `CardDatabase`
- `CDCMetaCardView` - Card view with `Card`, `VisualCard` properties
- `IdNameProvider.GetName(UInt32 entityId, Boolean formatted)` - Get card names
- `ICardView.InstanceId` - Card instance identifier

### Key Interfaces (for Harmony patches)
- `Wotc.Mtga.Cards.Text.ICardTextEntry.GetText()` - Card text
- `Wotc.Mtga.DuelScene.IPlayerPresenceController.SetHoveredCardId(UInt32)` - Hover events
- `Core.Code.Input.INextActionHandler.OnNext()` - Navigation
- `Wotc.Mtga.DuelScene.ITurnInfoProvider.EventTranslationTurnNumber` - Turn tracking

### UXEvent System (Used by DuelAnnouncer)
The game uses a UX event queue (`Wotc.Mtga.DuelScene.UXEvents.UXEventQueue`) to process game events.

**Key Event Types:**

**UpdateTurnUXEvent**
- Purpose: Turn changes
- Key Fields: `_turnNumber` (uint), `_activePlayer` (Player object)

**UpdateZoneUXEvent**
- Purpose: Zone state updates
- Key Fields: `_zone` (string like "Hand (PlayerPlayer: 1 (LocalPlayer), 6 cards)")

**ZoneTransferGroup**
- Purpose: Card movements
- Key Fields: `_zoneTransfers`, `_reasonZonePairs`

**UXEventUpdatePhase**
- Purpose: Phase changes
- Key Fields: `_phase` or `_step` (needs investigation)

**ToggleCombatUXEvent**
- Purpose: Combat start/end
- Key Fields: `_isEnabling`

**AttackLobUXEvent**
- Purpose: Attack animation
- Key Fields: none

**Player identification from _activePlayer:**
```csharp
string playerStr = activePlayerObj.ToString();
// Returns: "Player: 1 (LocalPlayer)" or "Player: 2 (Opponent)"
bool isYourTurn = playerStr.Contains("LocalPlayer");
```

**Zone parsing from _zone:**
```csharp
// Format: "Hand (PlayerPlayer: 1 (LocalPlayer), 6 cards)"
var zoneMatch = Regex.Match(zoneStr, @"^(\w+)\s*\(");     // Zone name
var countMatch = Regex.Match(zoneStr, @"(\d+)\s*cards?\)"); // Card count
bool isLocal = zoneStr.Contains("LocalPlayer");
bool isOpponent = zoneStr.Contains("Opponent");
```

### Main Menu Architecture

The main menu uses a different architecture than DuelScene. Key classes in `Core.Meta.MainNavigation`:

**NavContentController** - Base class for all menu screens (MonoBehaviour)
- Properties: `NavContentType`, `IsOpen`, `IsReadyToShow`, `SkipScreen`
- Methods: `BeginOpen()`, `BeginClose()`, `FinishOpen()`, `FinishClose()`, `Activate(bool)`
- Implementations: `HomePageContentController`, `ProfileContentController`, `AchievementsContentController`, etc.

**SettingsMenu / SettingsMenuHost** - Settings panel management
- `IsOpen` property/method to check if settings is active
- `Open()` / `Close()` for panel control
- `IsMainPanelActive` - main settings vs submenu

**WrapperSceneManagement** - Scene/menu loader
- `LoadScene()`, `UnloadScene()`, `IsSceneLoaded()`
- Manages multiple scenes loaded simultaneously

**PopupManager** - Modal dialogs
- `RegisterPopup()`, `UnregisterPopup()`, `ToggleMenu()`
- Has `Priority` property for input handling

**Button Types in Menus:**
- `CustomButton` - MTGA's custom component (most menu buttons, NOT Unity Selectable)
- `Button` - Unity standard (some overlay elements)
- `EventTrigger` - Special interactive elements

**Input Priority (highest to lowest):**
1. Debug, 2. SystemMessage, 3. SettingsMenu, 4. DuelScene, 5. Wrapper, 6. NPE

See `docs/MENU_NAVIGATION.md` for detailed documentation.

### PlayBlade Architecture (Play/Bot Match Screens)

When clicking Play or Bot Match on the HomePage, the PlayBlade system opens:

**PlayBladeController** - Controls the sliding blade panel
- Property: `PlayBladeVisualState` (enum: Hidden, Events, DirectChallenge, FriendChallenge)
- Property: `IsDeckSelected` - Whether a deck is selected
- Lives inside `HomePageContentController`

**Blade Views** (inherit from BladeContentView)
- `EventBladeContentView` - Shows game modes (Ranked, Play, Brawl)
- `FindMatchBladeContentView` - Shows deck selection and match finding
- `LastPlayedBladeContentView` - Quick replay last mode

**HomePageContentController blade properties:**
- `IsEventBladeActive` - Event blade is showing
- `IsDirectChallengeBladeActive` - Direct challenge is showing
- `ChallengeBladeController` - Reference to PlayBladeController

**Key Flow:**
1. Click Play → `PlayBladeVisualState` changes to Events
2. `FindMatchBladeContentView.Show()` called
3. Deck selection UI appears (NOT via DeckSelectBlade.Show())
4. User selects deck → `IsDeckSelected` = true
5. Click Find Match → Game starts matchmaking

**Important Discovery:** The deck selection in PlayBlade does NOT use `DeckSelectBlade.Show()`.
The deck list is embedded directly in the blade views. This is why DeckSelectBlade patches
don't fire when using Play button - only when using dedicated deck manager.

### Deck Selection Status (January 2026 Investigation)

**What Works:**
- Play button → Opens PlayBlade correctly
- Tab navigation → Cycles through elements
- Mode tabs (Play/Ranked/Brawl) → Produce sounds when clicked (activation works partially)
- Find Match button → Shows tooltip about AI opponent
- EventSystem selection → Decks ARE being selected (`SetSelectedGameObject` works)

**Current Issue - Deck Selection:**
Clicking on a deck entry sends all activation events but the game doesn't respond:
- Pointer events sent (enter, down, up, click)
- Submit event sent
- EventSystem shows deck as selected
- BUT `IsDeckSelected` Harmony patch never fires

**Possible Causes (Under Investigation):**
1. **Starter Decks May Need Cloning** - Tooltip mentions "cloning a deck". Starter decks
   might be read-only templates that must be copied to "My Decks" before use.
2. **Deck Folders:**
   - "My Decks" - User's playable decks (often empty/unchecked)
   - "Starter Decks" - Pre-built template decks (valid for format)
   - "Invalid Decks" - Decks not valid for current format
3. **Activation May Need Mouse** - Some UI elements might check actual mouse position
   rather than event data position.

**Next Steps:**
- Test with mouse to confirm if deck selection works at all
- Investigate if starter decks need to be cloned via Decks menu first
- Check if "My Decks" folder needs populated decks

### Deck Entry Structure (MetaDeckView)

Each deck entry in selection lists uses `MetaDeckView`:

**Properties:**
- `TMP_InputField NameText` - Editable deck name field
- `TMP_Text DescriptionText` - Description text
- `CustomButton Button` - Main selection button
- `Boolean IsValid` - Whether deck is valid for format
- `Boolean IsCraftable` - Whether missing cards can be crafted

**Callbacks (via DeckFolderView/DeckViewSelector):**
- `onDeckSelected` - Single click selects deck
- `onDeckDoubleClicked` - Double click (starts game in some contexts)
- `onDeckNameEndEdit` - When renaming is finished

**UI Hierarchy:**
```
DeckView_Base(Clone)
└── UI (CustomButton) ← Main selection button (Enter)
    └── TextBox ← Name edit area (Shift+Enter)
```

Both elements share the same `TMP_InputField` for the deck name. The mod pairs these
elements and provides separate keyboard shortcuts:
- **Enter** - Activates UI (select deck)
- **Shift+Enter** - Activates TextBox (edit deck name)

## Input System

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
- Combat announcements: "Combat begins", "Attacker declared", "Attacker removed"
- Opponent plays: "Opponent played a card" (hand count decrease detection)
- Combat damage: "[Card] deals [N] to [target]" (see Combat Damage Announcements below)

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

*Correct Field Names:*
- `AffectedId` (uint) - NOT "PlayerId"
- `Change` (property, int) - Life change amount (positive=gain, negative=loss)
- `_avatar` - Avatar object, can check `.ToString()` for "Player #1" to determine local player

*Note:* There is no `NewLifeTotal` field. Announcements format: "You lost 3 life" / "Opponent gained 4 life"

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

### TargetNavigator
Handles target selection when playing spells that require targets. Detects valid targets via HotHighlight child objects.

**User Flow:**
1. Play targeted spell from hand (Enter)
2. "Select a target. N valid targets" announced
3. Tab cycles through valid targets
4. Enter selects current target
5. Escape cancels targeting

**Key Discovery - HotHighlight Detection:**
The game uses `HotHighlight` child objects to visually indicate valid targets:
- Valid targets: `Highlights - Default(Clone)` → `HotHighlightBattlefield(Clone)` (ACTIVE)
- Non-targets: `Highlights - Default(Clone)` with no HotHighlight child
- The spell on stack has `_GlowColor` green glow (NOT the targeting indicator)

**Integration (via UIActivator):**
```csharp
// UIActivator.PlayCardViaTwoClick checks for targeting mode after playing
bool needsTargeting = IsTargetingModeActive(); // Checks for Submit/Cancel buttons
if (needsTargeting)
{
    targetNavigator.EnterTargetMode();
}
```

**Target Discovery:**
```csharp
// TargetNavigator.HasTargetingHighlight()
foreach (Transform child in card.GetComponentsInChildren<Transform>(true))
{
    if (child.gameObject.activeInHierarchy && child.name.Contains("HotHighlight"))
        return true; // This card is a valid target
}
```

### HighlightNavigator
Handles Tab cycling through playable/highlighted cards during normal gameplay. Replaces the default Tab behavior that cycles through UI buttons.

**How It Works:**
The game uses `HotHighlight` child objects to visually indicate playable cards (same system used for targeting). HighlightNavigator detects these and allows Tab navigation through them.

**User Flow:**
1. During your turn, press Tab
2. "Card Name, in hand, 1 of 3 playable" announced
3. Tab/Shift+Tab cycles through all playable cards
4. Enter plays/activates the current card
5. Selection resets after activation

**Input Priority (in DuelNavigator):**
```
TargetNavigator    → Tab during targeting mode (spell targets)
HighlightNavigator → Tab during normal play (playable cards)
ZoneNavigator      → Zone shortcuts (C/B/G/X/S) and arrows
Base behavior      → Fallback (rarely reached)
```

**Key Methods:**
```csharp
highlightNavigator.Activate();              // Enable on duel start
highlightNavigator.Deactivate();            // Disable on scene change
highlightNavigator.HandleInput();           // Process Tab/Enter
```

**Detection Logic:**
Uses same HotHighlight detection as TargetNavigator (see "Key Discovery - HotHighlight Detection" above).

**Integration with DuelNavigator:**
```csharp
// In DuelNavigator.HandleCustomInput()
if (_targetNavigator.HandleInput())     // Targeting takes priority
    return true;
if (_highlightNavigator.HandleInput())  // Then playable card cycling
    return true;
if (_zoneNavigator.HandleInput())       // Then zone navigation
    return true;
```

### Mode Interactions and Auto-Detection (January 2026)

The duel scene has multiple "modes" that affect input handling. Understanding their interactions is critical for debugging.

**Modes:**
1. **Targeting Mode** (TargetNavigator) - Selecting targets for spells/abilities
2. **Discard Mode** (DiscardNavigator) - Selecting cards to discard
3. **Combat Phase** (CombatNavigator) - Declare attackers/blockers
4. **Normal Mode** - Default zone navigation

**Input Priority in DuelNavigator.HandleCustomInput():**
```
1. TargetNavigator    → Tab/Enter/Escape during targeting
2. DiscardNavigator   → Enter/Space during discard mode
3. CombatNavigator    → F/Space during declare attackers/blockers
4. HighlightNavigator → Tab to cycle playable cards
5. BattlefieldNavigator → A/R/B shortcuts, row navigation
6. ZoneNavigator      → C/G/X/S shortcuts, Left/Right in zones
```

**HotHighlight - Shared Visual Indicator:**
The game uses `HotHighlight` child objects for MULTIPLE purposes:
- Valid spell targets (targeting mode)
- Playable cards (highlight mode)
- Creatures that can attack (declare attackers)
- Creatures that can block (declare blockers)

**CRITICAL: We cannot visually distinguish these!** This is why phase detection is essential.

**Auto-Detection Logic (DuelNavigator):**

*Entering Targeting Mode:*
```csharp
// Only auto-enter when NOT in combat phase
bool inCombatPhase = IsInDeclareAttackersPhase || IsInDeclareBlockersPhase;
if (!_targetNavigator.IsTargeting && !inCombatPhase && HasValidTargetsOnBattlefield())
{
    _targetNavigator.EnterTargetMode();
}
```
- Checks for HotHighlight on battlefield/stack cards
- EXCLUDES combat phases (HotHighlight = attackers/blockers there)
- During combat, UIActivator explicitly calls EnterTargetMode() for instants

*Exiting Targeting Mode:*
```csharp
// Auto-exit when:
// 1. No more HotHighlight (spell resolved)
// 2. Combat started without spell on stack
if (_targetNavigator.IsTargeting)
{
    if (!hasValidTargets)
        _targetNavigator.ExitTargetMode(); // Spell resolved
    else if (inCombatPhase && _zoneNavigator.StackCardCount == 0)
        _targetNavigator.ExitTargetMode(); // Combat, no spell
}
```
- Game event `PlayerSubmittedTargetsEventTranslator` is unreliable
- Fallback: check if HotHighlight disappeared or combat started

**DiscardNavigator Detection:**
```csharp
public bool IsDiscardModeActive()
{
    if (GetSubmitButtonInfo() == null)  // No "Submit X" button
        return false;
    if (HasValidTargetsOnBattlefield()) // HotHighlight = targeting, not discard
        return false;
    return true;
}
```
- Looks for "Submit X" button (e.g., "Submit 0", "Submit 1")
- Yields to targeting mode if HotHighlight exists
- NOTE: Cancel button alone is NOT reliable (appears in combat too)

**Combat Phase Detection:**
```csharp
// In DuelAnnouncer
public bool IsInDeclareAttackersPhase { get; private set; }
public bool IsInDeclareBlockersPhase { get; private set; }
```
- Set via `ToggleCombatUXEvent` and phase tracking
- Used to suppress targeting auto-detection during combat

**BattlefieldNavigator Zone Coordination:**
```csharp
// Only handle Left/Right if in battlefield zone
bool inBattlefield = _zoneNavigator.CurrentZone == ZoneType.Battlefield;
if (!alt && inBattlefield && Input.GetKeyDown(KeyCode.LeftArrow)) { ... }

// A/R/B shortcuts set zone to Battlefield
if (Input.GetKeyDown(KeyCode.B))
{
    _zoneNavigator.SetCurrentZone(ZoneType.Battlefield);
    NavigateToRow(BattlefieldRow.PlayerCreatures);
}
```
- Prevents stealing Left/Right from other zones (hand, graveyard)
- Zone state shared via `ZoneNavigator.SetCurrentZone()`

**Targeting Mode During Combat (Instant Spells):**
When playing an instant during combat:
1. User presses Enter on instant in hand
2. UIActivator plays the card
3. UIActivator calls `targetNavigator.EnterTargetMode()` if targeting needed
4. Auto-detection is SKIPPED (inCombatPhase = true)
5. After target selected, auto-exit detects "no more HotHighlight"

**Common Bug Patterns:**

1. **Targeting mode doesn't exit:**
   - Check if `PlayerSubmittedTargetsEventTranslator` fired
   - Check if HotHighlight still exists
   - Add auto-exit fallback

2. **Targeting activates during combat:**
   - Check `inCombatPhase` flag
   - Verify `IsInDeclareAttackersPhase`/`IsInDeclareBlockersPhase`

3. **Can't play cards (stuck in mode):**
   - Check which navigator is consuming Enter key
   - Check targeting/discard mode flags
   - Look for leftover Submit/Cancel buttons

4. **Left/Right stolen by battlefield:**
   - Check `ZoneNavigator.CurrentZone`
   - Verify zone shortcuts update zone state

**Debug Logging to Add:**
```csharp
MelonLogger.Msg($"[Mode] targeting={_targetNavigator.IsTargeting}, " +
    $"discard={_discardNavigator.IsDiscardModeActive()}, " +
    $"combat={inCombatPhase}, hotHighlight={hasValidTargets}, " +
    $"stack={_zoneNavigator.StackCardCount}");
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
- Elements inside "Blade", "PlayBlade", "FindMatch", "EventBlade" hierarchies are NOT filtered
- Elements inside "HomePage", "HomeContent" but NOT inside a Blade ARE filtered
- This shows only the blade's deck selection elements, hiding the HomePage buttons behind it

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
