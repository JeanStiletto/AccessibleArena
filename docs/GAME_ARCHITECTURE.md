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

- `HomePageContentController` - Main home screen with Play, Bot Match, carousel
- `DeckManagerController` - Deck building/management
- `ProfileContentController` - Player profile
- `ContentController_StoreCarousel` - Store page
- `MasteryContentController` - Mastery tree progression
- `AchievementsContentController` - Achievements/rewards
- `LearnToPlayControllerV2` - Tutorial content
- `PackOpeningController` - Pack opening animations
- `CampaignGraphContentController` - Color Challenge menu
- `WrapperDeckBuilder` - Deck builder/editor

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

When clicking Play or Bot Match on the HomePage, the PlayBlade system opens.

**PlayBladeController** - Controls the sliding blade panel
- Property: `PlayBladeVisualState` (enum: Hidden, Events, DirectChallenge, FriendChallenge)
- Property: `IsDeckSelected` - Whether a deck is selected

**Blade Views** (inherit from BladeContentView)
- `EventBladeContentView` - Shows game modes (Ranked, Play, Brawl)
- `FindMatchBladeContentView` - Shows deck selection and match finding
- `LastPlayedBladeContentView` - Quick replay last mode

**Key Flow:**
1. Click Play -> `PlayBladeVisualState` changes to Events
2. `FindMatchBladeContentView.Show()` called
3. Deck selection UI appears
4. User selects deck -> `IsDeckSelected` = true
5. Click Find Match -> Game starts matchmaking

**Important:** Deck selection in PlayBlade does NOT use `DeckSelectBlade.Show()`.
The deck list is embedded directly in the blade views.

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
└── UI (CustomButton) <- Main selection button
    └── TextBox <- Name edit area
```

## Button Types

**CustomButton** - MTGA's custom component (most menu buttons, NOT Unity Selectable)
**CustomButtonWithTooltip** - Variant with tooltip support
**Button** - Unity standard (some overlay elements)
**EventTrigger** - Special interactive elements (including "Return to Arena")
**StyledButton** - Prompt buttons in duel screens (Continue, Cancel)

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
└── Base
    ├── LocalPlayer
    │   ├── HandContainer/LifeFrameContainer
    │   ├── LeftContainer/AvatarContainer
    │   │   ├── WinPipsAnchorPoint
    │   │   ├── UserNameContainer
    │   │   ├── MatchTimerContainer
    │   │   └── RankAnchorPoint
    │   └── PrompButtonsContainer
    └── Opponent
        ├── LifeFrameContainer
        └── AvatarContainer
```

### Match Timer Structure

```
LocalPlayerMatchTimer_Desktop_16x9(Clone)
├── Icon
│   ├── Pulse
│   └── HoverArea <- Clickable for emotes
├── Text <- Shows "00:00" format
└── WarningPrompt
```

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
