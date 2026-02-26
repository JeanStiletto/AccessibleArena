# MTGA Game Architecture

Reference documentation for MTGA's internal structure, useful for modding and accessibility development.

## Game Structure

Engine: Unity with Mono runtime (MonoBleedingEdge)
Install Path: `C:\Program Files\Wizards of the Coast\MTGA`

### Key Directories

- `MTGA_Data\Managed` - .NET assemblies (modding target)
- `MTGA_Data\StreamingAssets` - Audio and configuration
- `MelonLoader` folder - Mod framework (after installation)

### Key Assemblies

**Assembly-CSharp.dll (314 KB)**
Main game code assembly. Contains HasbroGo namespace, login menus.
Heavy dependencies prevent full reflection analysis.

**Core.dll (11.8 MB)**
Game interfaces and systems. 10,632 types including:
- Card text interfaces
- Card view interfaces
- Input handler interfaces
- Game state interfaces
- Zone and battlefield interfaces

**Wizards.Arena.Models.dll (141 KB)**
550 types. Contains data models and DTOs for cards, decks, events, draft.

## Card Identifiers

- `grpId` (UInt32): Unique card identifier across the system (database ID)
- `InstanceId` (UInt32): In-game card instance on battlefield
- `playerId` (UInt32): Player identifier

## DuelScene Zone Architecture

Zone holders are GameObjects with specific naming patterns.

### Zone Holder Names

| Zone | GameObject | ZoneId | Type |
|------|------------|--------|------|
| Your Hand | `LocalHand_Desktop_16x9` | #31 | Hand |
| Opponent Hand | `OpponentHand_Desktop_16x9` | #35 | Hand |
| Battlefield | `BattlefieldCardHolder` | #28 | Battlefield |
| Your Graveyard | `LocalGraveyard` | #33 | Graveyard |
| Opponent Graveyard | `OpponentGraveyard` | #37 | Graveyard |
| Exile | `ExileCardHolder` | #29 | Exile |
| Stack | `StackCardHolder_Desktop_16x9` | #27 | Stack |
| Your Library | `LocalLibrary` | #32 | Library |
| Opponent Library | `OpponentLibrary` | #36 | Library |
| Command | `CommandCardHolder` | #26 | Command |

### Zone Metadata

Zone metadata embedded in GameObject names:
```
LocalHand_Desktop_16x9 ZoneId: #31 | Type: Hand | OwnerId: #1
```
Parse with regex: `ZoneId:\s*#(\d+)`, `OwnerId:\s*#(\d+)`

### Card Detection in Zones

Cards are children of zone holders. Detection patterns:
- Name prefix: `CDC #` (Card Display Controller)
- Name contains: `CardAnchor`
- Component types: `CDCMetaCardView`, `CardView`, `DuelCardView`, `Meta_CDC`

### HotHighlight (Card Targeting/Playability Indicator)

The game marks playable and targetable cards by adding a child GameObject with "HotHighlight" in its name. This is NOT a direct child of the card CDC — it is nested deeper in the card's hierarchy (e.g., `CDC #123 > SubContainer > HotHighlightBattlefield(Clone)`).

- Name pattern: `HotHighlight*` (e.g., `HotHighlightBattlefield(Clone)`, `HotHighlightHand(Clone)`)
- The HotHighlight object may exist but be **inactive** — existence (not active state) indicates the card is playable/targetable
- To find highlighted cards: scan for GameObjects named "HotHighlight", then walk up the parent chain to find the owning card (use `CardDetector.IsCard()`)
- The game adds/removes these dynamically as game state changes (priority, mana, targets)
- Player avatars do NOT use HotHighlight — they use `HighlightSystem` sprite swapping instead (see DuelScene_AvatarView section)

## Key Game Classes

From decompiled Core.dll:

- `GameManager` - Central manager with `CardHolderManager`, `ViewManager`, `CardDatabase`
- `CDCMetaCardView` - Card view with `Card`, `VisualCard` properties
- `IdNameProvider.GetName(UInt32 entityId, Boolean formatted)` - Get card names
- `ICardView.InstanceId` - Card instance identifier

## Key Interfaces (for Harmony patches)

- `Wotc.Mtga.Cards.Text.ICardTextEntry.GetText()` - Card text
- `Wotc.Mtga.DuelScene.IPlayerPresenceController.SetHoveredCardId(UInt32)` - Hover events
- `Core.Code.Input.INextActionHandler.OnNext()` - Navigation
- `Wotc.Mtga.DuelScene.ITurnInfoProvider.EventTranslationTurnNumber` - Turn tracking

### Card Text Interfaces

```
Wotc.Mtga.Cards.Text.ICardTextEntry
- Method: String GetText()

Wotc.Mtga.Cards.Text.ILoyaltyTextEntry
- Method: String GetCost()
```

### Card View Interfaces

```
Wotc.Mtga.DuelScene.ICardView
- Property: UInt32 InstanceId

ICardBrowserProvider
- Property: Boolean AllowKeyboardSelection
- Method: String GetCardHolderLayoutKey()
```

### Input Handler Interfaces

```
Core.Code.Input.IAcceptActionHandler - Void OnAccept()
Core.Code.Input.INextActionHandler - Void OnNext()
Core.Code.Input.IPreviousActionHandler - Void OnPrevious()
Core.Code.Input.IFindActionHandler - Void OnFind()
Core.Code.Input.IAltViewActionHandler - OnOpenAltView(), OnCloseAltView()
```

### Game State Interfaces

```
Wotc.Mtga.DuelScene.ITurnInfoProvider
- Property: UInt32 EventTranslationTurnNumber

Wotc.Mtga.DuelScene.IAvatarView
- Property: Boolean IsLocalPlayer
- Method: Void ShowPlayerNames(Boolean visible)
```

### Zone Interfaces

```
IBattlefieldStack
- Property: Boolean IsAttackStack
- Property: Boolean IsBlockStack
- Property: Int32 AttachmentCount
- Method: Void RefreshAbilitiesBasedOnStackPosition()
```

## UXEvent System

The game uses a UX event queue (`Wotc.Mtga.DuelScene.UXEvents.UXEventQueue`) to process game events.

### Key Event Types

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
- Key Fields: `<Phase>k__BackingField`, `<Step>k__BackingField`
- Phase/Step values (as seen in events):
  - `Beginning/Untap`, `Beginning/Upkeep`, `Beginning/Draw`
  - `Main1/None`, `Main2/None`
  - `Combat/None`, `Combat/DeclareAttack`, `Combat/DeclareBlock`, `Combat/CombatDamage`, `Combat/EndCombat`
  - `Ending/None` (= End Step), `Ending/Cleanup`
- Note: `Ending/None` is the End Step (there is no `Ending/End` or `Ending/EndStep` value)

**ToggleCombatUXEvent**
- Purpose: Combat start/end
- Key Fields: `_isEnabling`, `_CombatMode`

**AttackLobUXEvent**
- Purpose: Attack animation
- Key Fields: `_attackerId`

### Parsing UXEvent Data

Player identification from `_activePlayer`:
```csharp
string playerStr = activePlayerObj.ToString();
// Returns: "Player: 1 (LocalPlayer)" or "Player: 2 (Opponent)"
bool isYourTurn = playerStr.Contains("LocalPlayer");
```

Zone parsing from `_zone`:
```csharp
// Format: "Hand (PlayerPlayer: 1 (LocalPlayer), 6 cards)"
var zoneMatch = Regex.Match(zoneStr, @"^(\w+)\s*\(");     // Zone name
var countMatch = Regex.Match(zoneStr, @"(\d+)\s*cards?\)"); // Card count
bool isLocal = zoneStr.Contains("LocalPlayer");
```

## Main Menu Architecture

The main menu uses a different architecture than DuelScene.

**Important:** There is ONE main menu screen with dynamic content.
The "Home" button and "Return to Arena" button both navigate within the same system.

### NavContentController

Base class for all menu screens (MonoBehaviour):
- Properties: `NavContentType`, `IsOpen`, `IsReadyToShow`, `SkipScreen`
- Methods: `BeginOpen()`, `BeginClose()`, `FinishOpen()`, `FinishClose()`, `Activate(bool)`

### NavContentController Implementations

- `HomePageContentController` - Main home screen with Play button, carousel (Note: Bot Match button no longer visible - see KNOWN_ISSUES.md)
- `DeckManagerController` - Deck building/management
- `ProfileContentController` - Player profile
- `ContentController_StoreCarousel` - Store page
- `MasteryContentController` - Mastery tree progression
- `AchievementsContentController` - Achievements/rewards
- `LearnToPlayControllerV2` - Tutorial content
- `PackOpeningController` - Pack opening animations
- `CampaignGraphContentController` - Color Challenge menu
- `WrapperDeckBuilder` - Deck builder/editor
- `ProgressionTracksContentController` - Rewards/Mastery Pass screen (RewardTrack scene)

### SettingsMenu

- `IsOpen` property/method to check if settings is active
- `Open()` / `Close()` for panel control
- `IsMainPanelActive` - main settings vs submenu

### Input Priority (highest to lowest)

1. Debug
2. SystemMessage
3. SettingsMenu
4. DuelScene
5. Wrapper
6. NPE

## NavBar Structure

The NavBar is a persistent UI bar visible across all menu screens.

**GameObject:** `NavBar_Desktop_16x9(Clone)`

**Key Elements:**
- `Base/Nav_Home` - Home button (CustomButton)
- `Base/Nav_Profile` - Profile button
- `Base/Nav_Decks` - Decks button
- `Base/Nav_Packs` - Packs button
- `Base/Nav_Store` - Store button
- `Base/Nav_Mastery` - Mastery button
- `Nav_Achievements` - Achievements (appears after HomePage loads)
- `MainButtonOutline` - "Return to Arena" button (EventTrigger, NOT CustomButton)

**Navigation Flow:**
1. NavBar loads first (~13 elements)
2. Content controller loads after (~6 seconds for HomePage)
3. Clicking NavBar buttons swaps the active content controller
4. Overlays (Settings, PlayBlade) float on top of content

## PlayBlade Architecture

When clicking the Play button on the HomePage, the PlayBlade system opens. (Note: Bot Match button no longer visible on HomePage - see KNOWN_ISSUES.md)

**PlayBladeController** - Controls the sliding blade panel
- Property: `PlayBladeVisualState` (enum: Hidden, Events, DirectChallenge, FriendChallenge)
- Property: `IsDeckSelected` - Whether a deck is selected

**Blade Views** (inherit from BladeContentView)
- `EventBladeContentView` - Shows game modes (Ranked, Play, Brawl)
- `FindMatchBladeContentView` - Shows deck selection and match finding
- `LastPlayedBladeContentView` - Recently played entries (tiles with deck + play button)

**LastPlayedBladeContentView (Recent Tab) Internals:**
- `_tiles` (private List\<LastGamePlayedTile\>) - Up to 3 tile instances (count - 1 of available models)
- `_models` (private List\<RecentlyPlayedInfo\>) - Model data for each tile
- `RecentlyPlayedInfo` has public fields: `EventInfo` (BladeEventInfo), `SelectedDeckInfo` (DeckViewInfo), `IsQueueEvent` (bool)
- `BladeEventInfo` has public fields: `LocTitle` (localization key), `EventName`, `IsInProgress`, etc.
- Each `LastGamePlayedTile` contains:
  - `_playButton` (CustomButton) - NOT wired to any action (onPlaySelected is null)
  - `_secondaryButton` (CustomButton) - The actual play/continue button (triggers OnPlayButtonClicked)
  - `_eventTitleText` (Localize) - Event title, resolved text readable from child TMP_Text
  - A `DeckView` created via `DeckViewBuilder` inside `_deckBoxParent`
- Tile GameObjects named: `LastPlayedTile - (EventName)`

**Key Flow:**
1. Click Play -> `PlayBladeVisualState` changes to Events
2. `FindMatchBladeContentView.Show()` called
3. Deck selection UI appears
4. User selects deck -> `IsDeckSelected` = true
5. Click Find Match -> Game starts matchmaking

**Important:** Deck selection in PlayBlade does NOT use `DeckSelectBlade.Show()`.
The deck list is embedded directly in the blade views.

## Panel Lifecycle Methods and Harmony Timing

**Critical for accessibility mod**: Understanding when Harmony patches fire relative to animations.

### Classes WITH Post-Animation Events

**NavContentController** (and descendants like HomePageContentController, DeckManagerController):
- `BeginOpen()` - Fires at animation START
- `FinishOpen()` - Fires AFTER animation completes, `IsReadyToShow` becomes true
- `BeginClose()` - Fires at animation START
- `FinishClose()` - Fires AFTER animation completes, panel fully invisible
- `IsReadyToShow` property - True when animation complete and UI ready for interaction

**Timing implication**: Patch `FinishOpen`/`FinishClose` for post-animation events. Current code patches `BeginClose` for early notification.

### Classes WITHOUT Post-Animation Events

**PopupBase**:
- Properties: `IsShowing`
- Methods: `OnEscape()`, `OnEnter()`, `Activate(bool)`
- **NO FinishOpen/FinishClose** - Only know when popup activates, not when animation done

**SystemMessageView** (confirmation dialogs):
- Properties: `IsOpen`, `Priority`
- Methods: `Show()`, `Hide()`, `HandleKeyDown()`
- **NO FinishShow/FinishHide** - `Show()`/`Hide()` fire at animation START

**BladeContentView** (EventBladeContentView, FindMatchBladeContentView, etc.):
- Properties: `Type` (BladeType enum)
- Methods: `Show()`, `Hide()`, `TickUpdate()`
- **NO FinishShow/FinishHide** - `Show()`/`Hide()` fire at animation START

**PlayBladeController**:
- Properties: `PlayBladeVisualState` (enum), `IsDeckSelected`
- **NO animation lifecycle methods** - Only property setters
- Uses SLIDE animation (position change), not alpha fade

### Timing Summary Table

| Class | Open Event | Close Event | Post-Animation? |
|-------|------------|-------------|-----------------|
| NavContentController | BeginOpen/FinishOpen | BeginClose/FinishClose | YES |
| PopupBase | Activate(true) | Activate(false) | NO |
| SystemMessageView | Show() | Hide() | NO |
| BladeContentView | Show() | Hide() | NO |
| PlayBladeController | PlayBladeVisualState setter | PlayBladeVisualState=Hidden | NO |
| SettingsMenu | Open() | Close() | NO (but has IsOpen) |

### Workaround for Missing Post-Animation Events

For classes without post-animation events, use alpha detection to confirm visual state:
1. Harmony patch fires (animation starting)
2. Poll CanvasGroup alpha until it crosses threshold (>= 0.5 visible, < 0.5 hidden)
3. Only then update navigation

**Alternative**: Use fixed delay after event (less reliable, animation durations vary).

### PopupManager (Potential Alternative Hook)

The game has a `PopupManager` singleton:
- `RegisterPopup(PopupBase popup)` - Called when popup registers
- `UnregisterPopup(PopupBase popup)` - Called when popup unregisters

These could be patched for popup lifecycle, but timing relative to animation is unknown.

## Deck Entry Structure (MetaDeckView)

Each deck entry in selection lists uses `MetaDeckView`:

**Properties:**
- `TMP_InputField NameText` - Editable deck name field
- `TMP_Text DescriptionText` - Description text
- `CustomButton Button` - Main selection button
- `Boolean IsValid` - Whether deck is valid for format

**UI Hierarchy:**
```
DeckView_Base(Clone)
â””â”€â”€ UI (CustomButton) <- Main selection button
    â””â”€â”€ TextBox <- Name edit area
```

## Button Types

**CustomButton** - MTGA's custom component (most menu buttons, NOT Unity Selectable)
**CustomButtonWithTooltip** - Variant with tooltip support
**Button** - Unity standard (some overlay elements)
**EventTrigger** - Special interactive elements (including "Return to Arena")
**StyledButton** - Prompt buttons in duel screens (Continue, Cancel)

## TooltipTrigger Component

Many UI elements have a `TooltipTrigger` component that displays hover tooltips.

### Key Fields

| Field | Type | Purpose |
|-------|------|---------|
| `LocString` | LocalizedString | **The tooltip text** (localized) |
| `TooltipData` | TooltipData | Additional tooltip configuration |
| `TooltipProperties` | TooltipProperties | Display settings |
| `tooltipContext` | TooltipContext | Context type (usually "Default") |
| `IsActive` | Boolean | Whether tooltip is currently active |
| `_clickThrough` | Boolean | Click behavior setting |

### Usage Examples

From observed elements:
- **Options_Button**: `LocString = "Optionen anpassen"` (Adjust options)
- **Nav_Settings**: `LocString = "Optionen anpassen"` (Adjust options)
- **MainButton** (Play): `LocString = ""` (empty - no tooltip)

### Notes

- Tooltip text is stored in `LocString` field as a LocalizedString
- `LocalizedString.ToString()` returns the localized text (e.g., "Optionen anpassen")
- The longer contextual text sometimes seen (e.g., "Complete tutorial to unlock 5 decks") comes from **sibling text elements**, not the TooltipTrigger itself
- TooltipTrigger implements IPointerClickHandler but should be excluded from activation logic (it just shows tooltips)
- **Used as last-resort fallback** by `UITextExtractor.TryGetTooltipText()` for image-only buttons (no TMP_Text, no sibling labels)
- Only used when tooltip text is under 60 chars (avoids verbose descriptions like "Verdiene Gold, indem du spielst...")

## Native Keybinds

MTGA uses Unity's Input System with `Core.Code.Input.Generated.MTGAInput` class.

### Input System Architecture

Key handling goes through `MTGA.KeyboardManager.KeyboardManager`:
- `PublishKeyUp(KeyCode key)`
- `PublishKeyDown(KeyCode key)`
- `PublishKeyHeld(KeyCode key, Single holdDuration)`

### Native Keys

- **Space** - Pass priority / Resolve
- **Enter** - Pass turn / Confirm
- **Shift+Enter** - Pass until end of turn
- **Ctrl** - Full control (temporary)
- **Shift+Ctrl** - Full control (locked)
- **Z** - Undo
- **Tab** - Float all lands (tap for mana)
- **Arrow keys** - Navigation

### Key Classes

- `Core.Code.Input.Generated.MTGAInput` - Main input handler
- `MTGA.KeyboardManager.KeyboardManager` - Key event distribution
- `Wotc.Mtga.DuelScene.Interactions.IKeybindingWorkflow` - Duel keybind interface

### Binding Storage

Key-to-action mappings stored in Unity InputActionAsset inside `globalgamemanagers.assets` (binary).

## Mana Payment Workflows

The game uses different workflows for mana payment depending on the context. Understanding these is critical for keyboard accessibility.

### Decompilation Method

To analyze game classes, use ILSpyCMD (command-line ILSpy):

```powershell
# Install ILSpyCMD via dotnet tools
dotnet tool install -g ilspycmd --version 8.2.0.7535

# Decompile a specific class
ilspycmd "C:\Program Files\Wizards of the Coast\MTGA\MTGA_Data\Managed\Core.dll" -t "Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.BatchManaSubmission"

# List all types in assembly
ilspycmd "C:\Program Files\Wizards of the Coast\MTGA\MTGA_Data\Managed\Core.dll" -l
```

Note: Version 8.2.0.7535 works with .NET 6.0. Newer versions may require .NET 8.0.

### IKeybindingWorkflow Interface

Located in `Wotc.Mtga.DuelScene.Interactions`:

```csharp
interface IKeybindingWorkflow
{
    bool CanKeyUp(KeyCode key);
    void OnKeyUp(KeyCode key);
    bool CanKeyDown(KeyCode key);
    void OnKeyDown(KeyCode key);
    bool CanKeyHeld(KeyCode key, float holdDuration);
    void OnKeyHeld(KeyCode key, float holdDuration);
}
```

Only `BatchManaSubmission` implements this interface.

### BatchManaSubmission (Batch Mana Selection)

**Full Path:** `Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.BatchManaSubmission`

Used for batch mana selection when multiple lands can be tapped together. Implements `IKeybindingWorkflow`.

**Key Bindings (from decompiled code):**
```csharp
public void OnKeyUp(KeyCode key)
{
    if (key == KeyCode.Q)
    {
        Submitted?.Invoke();  // Q = Submit mana payment
    }
}

public void OnKeyDown(KeyCode key)
{
    if (key == KeyCode.Escape)
    {
        Cancelled?.Invoke();  // Escape = Cancel
    }
}
```

**Key Methods:**
- `Open()` / `Close()` - Workflow lifecycle
- `OnClick(IEntityView entity, ...)` - Handles clicking lands to select/deselect
- `CanStack()` - Determines card stacking during selection
- `Submitted` / `Cancelled` - Events invoked by keybindings

**Important:** The `Submitted` event must be connected by the parent workflow. If null, Q key does nothing.

### AutoTapActionsWorkflow (Simple Ability Activation)

**Full Path:** `Wotc.Mtga.DuelScene.Interactions.AutoTapActionsWorkflow`

Used for simple activated abilities with mana costs. Does NOT implement `IKeybindingWorkflow` - only uses buttons.

**Key Characteristics:**
- Creates `PromptButtonData` objects for each mana payment option
- Primary button = main mana payment action with callback to `_request.SubmitSolution()`
- Cancel button = calls `_request.Cancel()`
- No keyboard shortcuts - must click buttons

**Button Setup (from decompiled code):**
```csharp
promptButtonData.ButtonCallback = delegate
{
    _request.SubmitSolution(CheckSanitizedSolution(autoTapSolution));
};
```

### Workflow Activation

When you click a creature with an activated ability:
1. If ability has simple mana cost → `AutoTapActionsWorkflow` (buttons only, no keyboard)
2. If ability requires selecting specific mana sources → `BatchManaSubmission` (Q to submit)

### Native Q Key Behavior

The Q key has dual behavior:
1. **Global:** Floats all lands (taps all mana sources for mana pool)
2. **In BatchManaSubmission:** Submits mana payment (if workflow active)

When `AutoTapActionsWorkflow` is active (not `BatchManaSubmission`), pressing Q triggers the global "float all lands" action instead of submitting.

### Related Classes

- `ManaColorSelection` - Handles hybrid mana color choices
- `ActionSourceSelection` - Selects ability source before mana payment
- `AutoTapSolution` - Represents a mana payment solution
- `ManaPaymentCondition` - Conditions for mana payment (color, type, etc.)

## Browser Types (Card Selection UI)

Browsers are overlay UIs for selecting/arranging cards (mulligan, scry, surveil, etc.). Different browser types use different APIs for card manipulation.

### Browser Architecture

All browsers use two card holders:
- `BrowserCardHolder_Default` - Cards staying on top / being kept
- `BrowserCardHolder_ViewDismiss` - Cards going to bottom / being dismissed

Both holders have a `CardBrowserCardHolder` component with:
- `CardViews` - List of DuelScene_CDC card objects
- `RemoveCard(DuelScene_CDC)` - Remove card from holder
- `AddCard(DuelScene_CDC)` - Add card to holder (base class method)
- `CardGroupProvider` - Optional browser controller (null for most browsers)

### London Mulligan Browser

Uses a central `LondonBrowser` controller accessed via `CardGroupProvider` property.

**Card Manipulation:**
```
1. Get LondonBrowser from holder.CardGroupProvider
2. Position card at target zone (LibraryScreenSpace / HandScreenSpace)
3. Call HandleDrag(cardCDC)
4. Call OnDragRelease(cardCDC)
```

**Key Methods on LondonBrowser:**
- `GetHandCards()` / `GetLibraryCards()` - Get card lists
- `IsInHand(cardCDC)` / `IsInLibrary(cardCDC)` - Check card position
- `HandleDrag(cardCDC)` - Start drag operation
- `OnDragRelease(cardCDC)` - Complete drag and move card
- `LibraryScreenSpace` / `HandScreenSpace` - Target positions (Vector2)

### Scry-like Browsers (Scry, Surveil, Read Ahead)

Uses a `SurveilBrowser` controller, also accessed via `CardGroupProvider` (same as London).
The SurveilBrowser maintains its own internal card lists (`graveyardGroup`, `libraryGroup`)
which are read on submission to determine which cards were dismissed.

**Card Manipulation (drag simulation, same pattern as London):**
```
1. Get SurveilBrowser from holder.CardGroupProvider
2. Position card at target zone (_graveyardCenterPoint / _libraryCenterPoint)
3. Call HandleDrag(cardCDC)  - moves card between internal lists
4. Call OnDragRelease(cardCDC)
```

**Key Methods on SurveilBrowser:**
- `GetGraveyardCards()` / `GetLibraryCards()` - Get card lists
- `HandleDrag(cardCDC)` - Check card position, move between internal lists
- `OnDragRelease(cardCDC)` - Complete drag operation
- `_graveyardCenterPoint` / `_libraryCenterPoint` - Zone centers (private, local-space Vector3)

**IMPORTANT:** Do NOT use RemoveCard/AddCard on the holders directly - those only move
cards visually without updating the browser's internal lists. The submission workflow
reads from the browser's internal lists, not the holders.

### Scry Browser (Scry, Scryish)

Uses a `ScryBrowser` with NO `CardGroupProvider`. Cards are displayed in a single ordered
list with a placeholder divider (InstanceId == 0). Card order determines the result:
cards before the placeholder go to top of library, cards after go to bottom.

**Card Manipulation (reorder around placeholder):**
```
1. Get CardViews list from the holder
2. Find card index and placeholder index (InstanceId == 0)
3. Call ShiftCards(cardIndex, placeholderIndex) on the holder
4. Call OnDragRelease(cardCDC) on the current browser to sync its cardViews list
```

**How Submit works (ScryWorkflow.Submit):**
- Iterates cardViews in order
- Cards before the placeholder (InstanceId == 0) go to SubZoneType.Top
- Cards after the placeholder go to SubZoneType.Bottom
- Calls _request.SubmitGroups(groups)

### Detection Pattern

```csharp
var holder = FindGameObject("BrowserCardHolder_Default");
var holderComp = holder.GetComponent("CardBrowserCardHolder");
var provider = holderComp.CardGroupProvider;

if (provider != null && provider.GetType().Name == "LondonBrowser")
    // London: drag simulation with HandleDrag/OnDragRelease
else if (provider != null && provider.GetType().Name == "SurveilBrowser")
    // Surveil: drag simulation with HandleDrag/OnDragRelease
else
    // Scry: reorder cards around placeholder via ShiftCards
```

## Modding Tools

### Required

1. **MelonLoader** - Mod loader framework
2. **HarmonyX** (0Harmony.dll) - Method patching (included with MelonLoader)
3. **Tolk** - Screen reader communication library

### Assembly Analysis

Use dnSpy or ILSpy to decompile assemblies in `MTGA_Data\Managed`:
- `Core.dll` - Main interfaces
- `Assembly-CSharp.dll` - Game logic
- `Wizards.Arena.Models.dll` - Data models

Analysis files generated by AssemblyAnalyzer tool (in libs folder):
- `analysis_core.txt`
- `analysis_models.txt`

### Patching Strategy

Use Harmony Postfix/Prefix patches on:
- `SetHoveredCardId` - Announce hovered cards
- `GetText` - Capture card text
- `OnNext/OnPrevious` - Announce navigation
- `UXEventQueue.EnqueuePending` - Intercept game events

## Player Representation UI

### Battlefield Layout Hierarchy

```
BattleFieldStaticElementsLayout_Desktop_16x9(Clone)
â””â”€â”€ Base
    â”œâ”€â”€ LocalPlayer
    â”‚   â”œâ”€â”€ HandContainer/LifeFrameContainer
    â”‚   â”œâ”€â”€ LeftContainer/AvatarContainer
    â”‚   â”‚   â”œâ”€â”€ WinPipsAnchorPoint
    â”‚   â”‚   â”œâ”€â”€ UserNameContainer
    â”‚   â”‚   â”œâ”€â”€ MatchTimerContainer
    â”‚   â”‚   â””â”€â”€ RankAnchorPoint
    â”‚   â””â”€â”€ PrompButtonsContainer
    â””â”€â”€ Opponent
        â”œâ”€â”€ LifeFrameContainer
        â””â”€â”€ AvatarContainer
```

### Match Timer Structure

```
LocalPlayerMatchTimer_Desktop_16x9(Clone)
â”œâ”€â”€ Icon
â”‚   â”œâ”€â”€ Pulse
â”‚   â””â”€â”€ HoverArea <- Clickable for emotes
â”œâ”€â”€ Text <- Shows "00:00" format
â””â”€â”€ WarningPrompt
```

### DuelScene_AvatarView (Player Avatar)

`DuelScene_AvatarView` is a MonoBehaviour on each player avatar (local + opponent). Used for player targeting detection.

**Key Members:**
- `IsLocalPlayer` (public property) - True for local player, false for opponent
- `PortraitButton` (private SerializeField, `ClickAndHoldButton`) - Clickable portrait button
- `_highlightSystem` (private SerializeField, `HighlightSystem`) - Controls highlight sprites
- `Model` (public property, `MtgPlayer`) - Player model with `InstanceId`

**HighlightSystem (nested class):**
- `_currentHighlightType` (private field, `HighlightType` enum) - Current highlight state
- `Update(HighlightType)` - Changes highlight sprite

**HighlightType enum values:**
- `None` (0) - No highlight
- `Cold` (1) - Valid but risky target (shows confirmation)
- `Tepid` (2) - Mapped to Hot sprite client-side
- `Hot` (3) - Normal valid target
- `Selected` (5) - Already selected

**Click path:** `PortraitButton.OnPointerClick()` -> `AvatarInput.Clicked` -> `AvatarClicked.Execute()` -> `SelectTargetsWorkflow.OnClick(avatarView)`

**Important:** The game does NOT add HotHighlight child GameObjects to player avatars. It uses `HighlightSystem` sprite swapping instead.

### GameManager Properties

- `CurrentGameState` / `LatestGameState` - MtgGameState (populated during gameplay)
- `MatchManager` - Match state management
- `TimerManager` - Timer management
- `ViewManager` - Entity views
- `CardHolderManager` - Card holder management
- `CardDatabase` - Card data lookup

### MtgGameState Properties

- `LocalPlayer` / `Opponent` - MtgPlayer objects
- `LocalHand` / `OpponentHand` - Zone objects
- `Battlefield` / `Stack` / `Exile` - Zone objects
- `LocalPlayerBattlefieldCards` / `OpponentBattlefieldCards` - Direct card lists

## CardDatabase and Localization Providers

The `GameManager.CardDatabase` provides access to card data and localization.

### CardDatabase Properties

| Property | Type | Purpose |
|----------|------|---------|
| `VersionProvider` | IVersionProvider | Database version info |
| `CardDataProvider` | ICardDataProvider | Card data access |
| `AbilityDataProvider` | IAbilityDataProvider | Ability data access |
| `DynamicAbilityDataProvider` | IDynamicAbilityDataProvider | Dynamic ability data |
| `AbilityTextProvider` | IAbilityTextProvider | Ability text lookup |
| `GreLocProvider` | IGreLocProvider | GRE (Game Rules Engine) localization |
| `ClientLocProvider` | IClientLocProvider | Client-side localization |
| `PromptProvider` | IPromptProvider | Prompt text |
| `PromptEngine` | IPromptEngine | Prompt processing |
| `AltPrintingProvider` | IAltPrintingProvider | Alternate art info |
| `AltArtistCreditProvider` | IAltArtistCreditProvider | Alternate artist info |
| `AltFlavorTextKeyProvider` | IAltFlavorTextKeyProvider | Alternate flavor text keys |
| `CardTypeProvider` | ICardTypeProvider | Card type info |
| `CardTitleProvider` | ICardTitleProvider | Card name lookup |
| `CardNameTextProvider` | ICardNameTextProvider | Card name text |
| `DatabaseUtilities` | IDatabaseUtilities | Utility functions |

### Key Lookup Methods

**Card Names:**
```csharp
CardDatabase.CardTitleProvider.GetCardTitle(uint grpId, bool formatted, string overrideLang)
```

**Ability Text:**
```csharp
CardDatabase.AbilityTextProvider.GetAbilityTextByCardAbilityGrpId(
    uint cardGrpId, uint abilityGrpId, IEnumerable<uint> abilityIds,
    uint cardTitleId, string overrideLangCode, bool formatted)
```

**Flavor Text / Type Line Text (via GreLocProvider):**
```csharp
// GreLocProvider is SqlGreLocalizationProvider
CardDatabase.GreLocProvider.GetLocalizedText(uint locId, string overrideLangCode, bool formatted)
// Used for FlavorTextId, TypeTextId, SubtypeTextId
// FlavorTextId = 1 is a placeholder meaning "no flavor text"
```

**Card Type Line Text (via GreLocProvider or CardTypeProvider):**
```csharp
// Option 1: Direct loc ID lookup (used by mod for type lines)
CardDatabase.GreLocProvider.GetLocalizedText(printing.TypeTextId)   // e.g. "Legendary Creature"
CardDatabase.GreLocProvider.GetLocalizedText(printing.SubtypeTextId) // e.g. "Elf Warrior"

// Option 2: Translate individual enum values
CardDatabase.GreLocProvider.GetLocalizedTextForEnumValue("CardType", (int)cardType)
CardDatabase.GreLocProvider.GetLocalizedTextForEnumValue("SuperType", (int)superType)
CardDatabase.GreLocProvider.GetLocalizedTextForEnumValue("SubType", (int)subType)

// Option 3: Full type line via CardTypeProvider (handles modifications, color tags)
CardDatabase.CardTypeProvider.GetTypelineText(ICardDataAdapter, CardTextColorSettings, overrideLang)
```

### Card Model Properties

Cards have a `Model` object (type `GreClient.CardData.CardData`) with these key properties.
The same properties are available on CardData objects from store items, making unified extraction possible via `CardModelProvider.ExtractCardInfoFromObject()`.

**Identity:**
- `GrpId` (uint) - Card database ID
- `TitleId` (uint) - Localization key for name
- `FlavorTextId` (uint) - Localization key for flavor (1 = none)
- `InstanceId` (uint) - Unique instance in a duel (0 for non-duel cards)

**Types (structured, always English enum names):**
- `Supertypes` (SuperType[]) - Legendary, Basic, etc.
- `CardTypes` (CardType[]) - Creature, Land, Instant, Sorcery, etc.
- `Subtypes` (SubType[]) - Goblin, Forest, Aura, etc.

**Type line localization IDs (on model or Printing sub-object):**
- `TypeTextId` (uint) - Localization key for type text (e.g. "Legendary Creature")
- `SubtypeTextId` (uint) - Localization key for subtype text (e.g. "Elf Warrior")
- Looked up via `GreLocProvider.GetLocalizedText(locId)` for localized display

**Stats:**
- `PrintedCastingCost` (ManaQuantity[]) - Mana cost array (structured)
- `Power` (StringBackedInt) - Creature power (use `GetStringBackedIntValue()`)
- `Toughness` (StringBackedInt) - Creature toughness

**Abilities:**
- `Abilities` (AbilityPrintingData[]) - Card abilities
- `AbilityIds` (uint[]) - Ability GRP IDs for text lookup

**Print-specific:**
- `Printing` (CardPrintingData) - Print-run specific data (artist, set)
- `Printing.ArtistCredit` (string) - Artist name
- `Printing.TypeTextId` (uint) - Type line localization key
- `Printing.SubtypeTextId` (uint) - Subtype localization key
- `ExpansionCode` (string) - Set code (e.g., "FDN")
- `Rarity` (CardRarity) - Common, Uncommon, Rare, MythicRare

**Fallback properties** (available on CardPrintingData but not structured):
- `TypeLine` / `TypeText` (string) - Full type line as string
- `ManaCost` / `CastingCost` (string) - Mana cost as string (e.g., "oU")
- `OldSchoolManaText` (string) - Mana cost in old format

`ExtractCardInfoFromObject` tries localized type line first (via TypeTextId/SubtypeTextId), then falls back to structured enum types (English), then string properties. This means it works with both full Model objects (duel, deck builder) and simpler CardData objects (store items), always producing localized output when possible.

**Important:** Structured enum types (Supertypes/CardTypes/Subtypes) always return English names via `.ToString()`. Use them only for internal type detection (isCreature, isLand), never for display. For display, use `TypeTextId`/`SubtypeTextId` via `GetLocalizedTextById()` or `GreLocProvider.GetLocalizedText()`.

### Mana Symbol Formats

Rules text uses these mana symbol formats:

**Curly Brace Format:** `{oX}` where X is:
- `T` = Tap, `Q` = Untap
- `W` = White, `U` = Blue, `B` = Black, `R` = Red, `G` = Green
- `C` = Colorless, `S` = Snow, `E` = Energy, `X` = Variable
- Numbers for generic mana: `{o1}`, `{o2}`, etc.
- Hybrid: `{oW/U}` = White or Blue
- Phyrexian: `{oW/P}` = Phyrexian White

**Bare Format (Activated Abilities):** `2oW:` at start of ability text
- Number followed by `oX` sequences, ending with colon
- Example: `2oW:` = "2, White:"

## Language & Localization System

The game has a comprehensive localization system. The current language is a static property accessible without instance references.

### Reading the Current Language

**Assembly:** `SharedClientCore.dll`
**Class:** `Wotc.Mtga.Loc.Languages` (static)

```csharp
// Get current language code (e.g., "de-DE", "en-US")
string lang = Wotc.Mtga.Loc.Languages.CurrentLanguage;
```

### Key Members of `Wotc.Mtga.Loc.Languages`

- `CurrentLanguage` (static string property) - Gets/sets the current language code. Setting it also updates `Thread.CurrentThread.CurrentCulture` and dispatches `LanguageChangedSignal`.
- `_currentLanguage` (private static string) - Backing field, defaults to `"en-US"`.
- `ActiveLocProvider` (static `IClientLocProvider`) - Active localization provider for translating loc keys to text.
- `LanguageChangedSignal` (static `ISignalListen`) - Signal dispatched when language changes. UI `Localize` components subscribe to this.
- `AllLanguages` / `ClientLanguages` / `ExternalLanguages` - String arrays of supported language codes.
- `Converter` - Dictionary mapping human-readable names ("English", "German") to codes ("en-US", "de-DE").
- `ShortLangCodes` - Maps "en-US" to "enUS", "de-DE" to "deDE" (used for SQL column lookups).
- `MTGAtoI2LangCode` - Maps MTGA codes to I2 loc codes ("en-US" -> "en", "de-DE" -> "de").
- `TriggerLocalizationRefresh()` - Dispatches the language changed signal to refresh all UI.

### Language Enum

**Assembly:** `Wizards.Arena.Enums.dll`
**Enum:** `Wizards.Arena.Enums.Language`

- `English` ("en-US")
- `Portuguese` ("pt-BR")
- `French` ("fr-FR")
- `Italian` ("it-IT")
- `German` ("de-DE")
- `Spanish` ("es-ES")
- `Russian` ("ru-RU") - in enum but not in ClientLanguages
- `Japanese` ("ja-JP")
- `Korean` ("ko-KR")
- `ChineseSimplified` ("zh-CN") - in enum but not in ClientLanguages
- `ChineseTraditional` ("zh-TW") - in enum but not in ClientLanguages

### Persistence

**Assembly:** `Core.dll`
**Class:** `MDNPlayerPrefs`

Stored in Unity PlayerPrefs under key `"ClientLanguage"`:
- Getter validates against `Languages.ExternalLanguages`, falls back to `"en-US"`
- Setter writes to `CachedPlayerPrefs` and calls `Save()`

### Initialization Flow

In `Wotc.Mtga.Loc.LocalizationManagerFactory.Create()`:
```csharp
Languages.CurrentLanguage = MDNPlayerPrefs.PLAYERPREFS_ClientLanguage;
// Then creates CompositeLocProvider as the ActiveLocProvider
```

### Localization Text Lookup

```csharp
// Get localized text for a key
string text = Languages.ActiveLocProvider?.GetLocalizedText("some/loc/key");
```

Backed by `SqlLocalizationManager` reading from SQLite database with columns per language (enUS, deDE, etc.). Listens to `LanguageChangedSignal` to clear its text cache.

### Language Change Detection

Subscribe to `Languages.LanguageChangedSignal` to detect language changes at runtime. All `Localize` MonoBehaviours already do this to refresh their text.

### UI Component: `Wotc.Mtga.Loc.Localize`

**Assembly:** `Core.dll`

MonoBehaviour attached to GameObjects with text. On enable, subscribes to `Languages.LanguageChangedSignal`. Uses `Pantry.Get<IClientLocProvider>()` and `Pantry.Get<IFontProvider>()` to localize text and font targets.

### Loc String Class: `MTGALocalizedString`

**Assembly:** `Core.dll`

- `Key` field (loc key like `"MainNav/Settings/LanguageNative_en"`)
- `Parameters` (optional)
- `ToString()` resolves via `Languages.ActiveLocProvider.GetLocalizedText(Key, ...)`

### Language Selection UI

**Class:** `Wotc.Mtga.Login.BirthLanguagePanel` (Core.dll)

Uses `TMP_Dropdown` populated from `Languages.ExternalLanguages`. Display text from loc keys like `"MainNav/Settings/LanguageNative_de"`. Initial language can come from PlayerPrefs, `MTGAUpdater.ini`, or Windows registry `ProductLanguage`.

## Event System Architecture

The game's event system (promotional events like Jump In, Standard Event, Draft, etc.) uses a layered architecture of data models, managers, and modular UI components.

### Event Data Layer

**EventManager** (`Wizards.MDN.EventManager`, implements `IEventManager`):
- `EventContexts` (List\<EventContext\>) - All loaded event contexts
- `EventsByInternalName` (Dictionary\<string, EventContext\>) - Lookup by internal name
- `GetEventContext(string internalEventName)` - Get specific event
- `Coroutine_GetEventsAndCourses()` fetches events from server

**EventContext** (`Wizards.MDN.EventContext`):
- `PlayerEvent` (IPlayerEvent) - Player's state in the event
- `DeckSelectContext` (DeckSelectSceneContext) - SelectDeck or InspectDeck
- `DeckIsFixed` - Whether deck is fixed format

**IEventInfo** (`Wotc.Mtga.Events.IEventInfo`) - Static event metadata:
- `EventId`, `InternalEventName`, `EventState` (MDNEventState enum)
- `FormatType` (MDNEFormatType), `StartTime`, `LockedTime`, `ClosedTime`
- `EntryFees` (List of EventEntryFeeInfo)
- `IsRanked`, `IsAiOpponent`, `IsPreconEvent`, `UpdateQuests`

**IPlayerEvent** (`Wotc.Mtga.Events.IPlayerEvent`) - Player's event state:
- `EventInfo` (IEventInfo), `EventUXInfo` (IEventUXInfo)
- `Format` (DeckFormat), `CourseData` (has `CurrentModule` enum)
- `CurrentWins`, `CurrentLosses`, `GamesPlayed`
- `MaxWins`, `MaxLosses`, `WinCondition`
- `HasUnclaimedRewards`, `MatchMakingName`
- `CurrentChoices`, `PacketsChosen`, `HistoricalChoices` (Jump In specific)
- Key methods: `JoinAndPay()`, `SubmitEventDeck()`, `ResignFromEvent()`, `ClaimPrize()`, `JoinNewMatchQueue()`, `SetChoice()`, `SubmitEventChoice()`

**IEventUXInfo** (`Wotc.Mtga.Events.IEventUXInfo`) - Display data:
- `PublicEventName`, `TitleLocKey`, `DisplayPriority`
- `Parameters` (Dictionary), `Group`
- `HasEventPage`, `DeckSelectFormat`, `OpenedFromPlayBlade`
- `EventComponentData` - Layout data for event page components

**PlayerEventModule enum** (state machine driving the entire event flow):
- `Join` / `Pay` / `PayEntry` -> Joining phase
- `Jumpstart` -> Packet selection (Jump In)
- `DeckSelect` / `Choice` -> Deck selection
- `Draft` / `HumanDraft` -> Draft phase
- `TransitionToMatches` / `WinLossGate` / `WinNoGate` -> Playing matches
- `ClaimPrize` -> Rewards
- `Complete` -> Done
- `NPEUpdate` -> New player experience

### Play Blade (Event Selection UI)

**EventBladeContentView** (`Wizards.Mtga.PlayBlade.EventBladeContentView`, extends `BladeContentView`):
- `_eventTileContainer` (RectTransform) - Parent container for tiles
- `_views` (List of PlayBladeEventTile) - All instantiated tiles
- `UpdateViewFromSelection()` creates tiles from filtered events
- Click handler dispatches `GoToEventPageSignal` with event name

**EventBladeView** (`Wizards.Mtga.PlayBlade.EventBladeView`, extends `BladeView`):
- `_optionsContainer` (Transform) - Container for filter items
- Filter items are `BladeFilterItem` -> `BladeListItem` with `CustomButton` + `Localize`

**PlayBladeEventTile** (`Wizards.Mtga.PlayBlade.PlayBladeEventTile`):
- `_customButton` (CustomButton) - Main click target
- `_titleText` (Localize) - Event name
- `_timerText` (TMP_Text) - Countdown/status timer
- `_rankImage` (Image) - Ranked indicator
- `_bestOf3Indicator` (RectTransform) - Bo3 indicator
- `_eventProgressPips` (RectTransform) - Win progress (1-3 pips)
- `_attractParent` (RectTransform) - Active when event is in progress
- GO naming: `"EventTile - (Jump_In_2024)"`, `"EventTile - (Standard_Event)"`
- Tile container path: `.../Viewport/Content - Grid - Event Tiles/`
- Clickable area: `.../EventTile - (name)/Container/Hitbox`

**BladeEventInfo** (data model per tile):
- `EventName`, `FormatName`, `LocTitle`, `LocShortTitle`, `LocDescription`
- `TimerType` enum: Invalid, Hidden, Preview, Unjoined_LockingSoon, Joined_ClosingSoon, ClosedAndCompleted
- `StartTime`, `LockTime`, `CloseTime` (DateTime)
- `IsInProgress`, `IsRanked`, `IsLimited`, `IsBotMatch`
- `WinCondition` (MatchWinCondition enum, includes BestOf3)
- `TotalProgressPips`, `PlayerProgress`

### Event Page (Event Detail)

**EventPageContentController** (`EventPage.EventPageContentController`, extends `NavContentController`):
- `NavContentType` = `NavContentType.EventLanding`
- `_currentEventContext` (EventContext) - Currently displayed event
- `_instantiatedEventPages` (Dictionary\<string, EventPage\>) - Cached pages keyed by `InternalEventName`
- `_factory` (EventPageComponentFactory) - Creates modular UI components
- `OnBeginOpen()` creates/caches `EventPageScaffolding` + `EventComponentManager`
- GO naming: root named after `InternalEventName` (e.g., "Jump_In_2024")
- Layout path: `.../Jump_In_2024/SafeArea/LowerRightVerticalLayoutGroup/`

**EventComponentManager** (`EventPage.Components.EventComponentManager`):
- Manages all `IComponentController` instances for an event page
- `OnEventPageOpen()` subscribes to keyboard/inventory, updates components
- `UpdateComponents()` determines state from `CurrentModule`
- `MainButton_OnPlayButtonClicked()` handles main action based on module state
- `MainButton_OnPayJoinButtonClicked()` handles payment flow

**Event Page Components** (all in `EventPage.Components` namespace):
- `MainButtonComponent` / `MainButtonComponentController` - The main action button
  - GO: `EventComponent_MainButton_Desktop_16x9(Clone)/MainButton_Play`
  - 5 button states: Play, Start, PayWithGems, PayWithGold, PayWithEventToken
  - Text varies by module: "Choose Packets", "Select Deck", "Play Match", "Claim Prize", etc.
- `ObjectiveTrackComponent` / `ObjectiveTrackComponentController` - Win/loss progress
  - GO: `Objective_CumulativeEvent(Clone)`
- `TimerComponent` / `TimerComponentController` - Event countdown
- `TextComponent` / `DescriptionComponentController` / `SubtitleComponentController` - Event text
- `SelectedDeckComponent` / `SelectedDeckComponentController` - Selected deck display
- `ResignComponent` / `ResignComponentController` - Resign button
- `PrizeWallComponent` / `PrizeWallComponentController` - Prize tier display
- `LossDetailsComponent` / `LossDetailsComponentController` - Loss tracking
- `ViewCardPoolComponent` / `ViewCardPoolComponentController` - Card pool button
- `InspectPreconDecksComponent` / `InspectSingleDeckComponent` - Deck preview
- `EmblemComponent` / `EmblemComponentController` - Event emblem display
- `EventButtonComponent` / `EventButtonComponentController` - Additional buttons

**IComponentController** interface:
- `Update(IPlayerEvent)` - Refresh with current data
- `OnEventPageOpen(EventContext)` - Called when page opens
- `OnEventPageStateChanged(IPlayerEvent, EventPageStates)` - On state change
- States: `DisplayQuest`, `ClaimQuestRewards`, `DisplayEvent`, `ClaimEventRewards`

### Packet Selection (Jump In)

**PacketSelectContentController** (`Wotc.Mtga.Wrapper.PacketSelect.PacketSelectContentController`, extends `NavContentController`):
- `NavContentType` = `NavContentType.PacketSelect`
- `_confirmSelectionButton` (CustomButton) - "Confirm selection" button
- `_headerText` (Localize) - Header showing "First Packet" / "Second Packet"
- `_packPrefab` (JumpStartPacket) - Prefab for instantiating packet tiles
- `_packetOptions` (List\<JumpStartPacket\>) - Currently displayed selectable packet instances
- `_submittedPacks` (List\<JumpStartPacket\>) - Already submitted packet instances
- `_packetToId` (Dictionary\<JumpStartPacket, string\>) - Maps packet MonoBehaviour to packet ID string
- `_idToPacket` (Dictionary\<string, JumpStartPacket\>) - Reverse mapping
- `_selectedPackId` (string) - Currently selected packet ID (empty string = none)
- `_currentState` (ServiceState readonly struct) - Canonical state from service
- `SetServiceState()` recreates packet instances from `_currentState.PacketOptions`
- Header loc keys: `"Events/Packets/Event_Header_First_Packet"` / `"Events/Packets/Event_Header_Second_Packet"`
- Single click = toggle selection, double click = select + submit

**ServiceState** (`Wotc.Mtga.Wrapper.PacketSelect.ServiceState`, readonly struct):
- `SubmittedPackets` (PacketDetails[]) - Already submitted packets (readonly field)
- `PacketOptions` (PacketDetails[]) - Available packet options (readonly field)
- `SubmissionCount()` returns **uint** (not int) - counts non-default entries in SubmittedPackets
- `AllPacketsSubmitted()` - True if no default entries remain in SubmittedPackets
- `CanSubmit(string packetId)` - True if not all submitted and option exists
- `GetDetailsById(string packetId)` - Checks submissions first, then options
- `GetOptionById(string packetId)` / `GetSubmissionById(string packetId)`

**PacketDetails** (`Wotc.Mtga.Wrapper.PacketSelect.PacketDetails`, readonly struct):
- `Name` (string) - Internal packet name (e.g., "Azorius"), used as loc key via `"Events/Packets/" + Name`
- `PacketId` (string) - Unique identifier for submission
- `LandGrpId` (uint) - GRP ID of the included basic land
- `ArtId` (uint) - Asset ID for packet card back art
- `RawColors` (string[]) - Array of single-char color codes (e.g., `["W", "U"]`), sorted by `PacketColorCore.SortColors()`

**JumpStartPacket** (`Wotc.Mtga.Wrapper.PacketSelect.JumpStartPacket`, MonoBehaviour):
- Instantiated from `_packPrefab` per packet option; each is a 3D card-like object
- `_input` (PacketInput) - Click/hover handler (delegates Clicked, DoubleClicked, MouseEntered, MouseExit)
- `_packTitle` (Localize) - Localized display name, set via `SetName(MTGALocalizedString)`
- `_cardBack` (MeshRenderer) - Card back with "ArtInFrame" material for packet art
- `_hoverHighlight` (GameObject) - Visual highlight on hover
- `_colorDisplayView` (ColorDisplayView) - Mana color display, set via `SetPacketColors(string[])`
- `_bluePickTab` / `_bluePickTabText` - Blue banner for submitted packet number
- `_orangePickTab` / `_orangePickTabText` - Orange banner for selected (pending) packet number
- `Root` property returns `transform` (used for movement system positioning)
- Banner loc key: `"Events/Packets/Banner_Text"` with parameter `packetNum`
- **Note:** The clickable element inside each packet is via PacketInput, not a CustomButton directly on the JumpStartPacket. The buttons discovered by GeneralMenuNavigator are CustomButtons inside the PacketInput hierarchy.

### Home Page Carousel

**HomeCarouselController** (root namespace):
- `MainButton` (CustomButton) - Click banner to navigate
- `NavLeftButton` / `NavRightButton` (CustomButton) - Arrow navigation
- `TitleLoc` (Localize) - Banner title
- `DescriptionLoc` (Localize) - Banner description
- `_visibleItems` (List\<Client_CarouselItem\>) - Carousel data
- `Client_CarouselItem` has: `TitleKey`, `DescriptionKey`, `Name`, `Actions`
- Auto-rotates every 15 seconds

**CarouselActionType enum:**
- `GoToEvent`, `GoToColorChallenge`, `GoToStoreItem`, `GoToExternalUrl`
- `OpenPlayBlade`, `OpenStoreTab`, `GoToScreen`, `GoToDynamicFilter`

### Asset Lookup Tree (Event Visuals)

Event visual assets are resolved via `AssetLookupTree` with extractors:
- `Event_Type`, `Event_State`, `Event_InternalName`, `Event_MatchmakingName`
- `Event_PublicName`, `Event_TimerState`, `Event_GamesWon`

Payload types for event visuals: `BackgroundPayload`, `BannerPayload`, `BladePayload`, `EmblemPayload`, etc.

### Factionalized Events (Newer System)

**Namespace:** `Core.Meta.MainNavigation.EventPageV2`
- `FactionalizedEventBlade` / `FactionalizedEventBladeItem` - Faction selection UI
- `FactionEventContext` - Extends EventContext with faction data
- `FactionalizedEventTemplate` / `FactionalizedEventUtils` - Template system
