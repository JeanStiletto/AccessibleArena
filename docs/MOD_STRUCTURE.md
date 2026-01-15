# MTGA Accessibility Mod - Structure & Status

## Project Layout

```
C:\Users\fabia\arena\
  src\
    MTGAAccessibility.csproj
    MTGAAccessibilityMod.cs      - MelonLoader entry point, holds central services
    ScreenReaderOutput.cs        - Tolk wrapper

    Core\
      Interfaces\
        INavigable.cs            - Navigable item interface
        INavigableContext.cs     - Context interface
        IScreenNavigator.cs      - Screen navigator interface (NEW)
        ...

      Models\                    - GameContext, AnnouncementPriority, Strings (localization), etc.

      Models\
        TargetInfo.cs            - Target data model and CardTargetType enum

      Services\
        # UI Utilities (static, use everywhere)
        UIActivator.cs           - Centralized UI element activation
        UITextExtractor.cs       - Text extraction (GetText, GetButtonText, CleanText)
        CardDetector.cs          - Card detection + card info extraction

        # Central Services (held by main mod)
        AnnouncementService.cs   - Speech output management
        ContextManager.cs        - Game context tracking
        InputManager.cs          - Custom shortcut handling
        ShortcutRegistry.cs      - Keybind registration
        CardInfoNavigator.cs     - Card detail navigation (arrow up/down)
        ZoneNavigator.cs         - Zone navigation in duel (H/B/G/X/S + arrows)
        DuelAnnouncer.cs         - Game event announcements via Harmony
        TargetNavigator.cs       - Target selection during spells/abilities
        HighlightNavigator.cs    - Tab cycling through playable/highlighted cards
        DiscardNavigator.cs      - Discard selection when forced to discard
        CombatNavigator.cs       - Combat phase navigation (declare attackers/blockers)
        BrowserNavigator.cs      - Library manipulation browsers (scry, surveil, mulligan)
        PlayerPortraitNavigator.cs - Player info zone (V key, life/timer/emotes)

        # Navigator Infrastructure
        BaseNavigator.cs         - Abstract base for screen navigators
        NavigatorManager.cs      - Manages navigator lifecycle and priority

        # Screen Navigators (all extend BaseNavigator)
        UIFocusTracker.cs            - EventSystem focus polling (fallback)
        WelcomeGateNavigator.cs      - Welcome/login choice screen
        LoginPanelNavigator.cs       - Email/password login screen
        CodeOfConductNavigator.cs    - Terms/consent checkboxes screen
        DuelNavigator.cs             - Duel gameplay screen (delegates to ZoneNavigator)
        EventTriggerNavigator.cs     - NPE rewards, pack opening, card reveal
        OverlayNavigator.cs          - Modal overlays (What's New, announcements)
        GeneralMenuNavigator.cs      - Main menu and general menu screens

        # UI Classification
        UIElementClassifier.cs       - Element role detection (button, progress, etc.)

    Contexts\
      Base\                      - BaseNavigableContext, BaseMenuContext
      Login\                     - LoginContext (login flow panels)
      MainMenu\                  - MainMenuContext

    Patches\
      UXEventQueuePatch.cs       - Harmony patch for duel game event interception
      PanelStatePatch.cs         - Harmony patch for menu panel state changes (partial success)
      KeyboardManagerPatch.cs    - Harmony patch to block consumed keys from game

  libs\                          - Reference assemblies and Tolk DLLs
  docs\                          - Documentation
  tools\                         - AssemblyAnalyzer and analysis scripts
```

## Implementation Status

### Completed
- [x] Project structure created
- [x] MelonLoader installed on game
- [x] Tolk library configured (NVDA communication working)
- [x] Assembly analysis completed
- [x] Core framework (interfaces, services, base classes)
- [x] Scene detection (Bootstrap, AssetPrep, Login)
- [x] UI Focus tracking via EventSystem polling

### Screen Navigators
- [x] WelcomeGateNavigator - Login/Register choice screen
- [x] LoginPanelNavigator - Email/password entry
- [x] CodeOfConductNavigator - Terms/consent checkboxes
- [x] DuelNavigator - Duel gameplay (zone navigation working)
- [x] EventTriggerNavigator - NPE rewards, pack opening
- [~] OverlayNavigator - Modal overlays (What's New carousel) - basic implementation
- [x] GeneralMenuNavigator - Main menu navigation with reliable overlay detection

### Menu Panel Detection (Harmony Patches)
- [x] PanelStatePatch - Harmony patches for panel state changes
- [x] NavContentController lifecycle - FinishOpen/FinishClose/BeginOpen/BeginClose patched
- [x] NavContentController.IsOpen setter - Backup detection
- [x] SettingsMenu.Open/Close - Patched, works correctly (Open has 7 bool params)
- [x] SettingsMenu.IsOpen setter - Backup detection
- [x] DeckSelectBlade.Show/Hide - Patched (Show takes EventContext, DeckFormat, Action)
- [x] DeckSelectBlade.IsShowing setter - Backup detection
- [x] PlayBladeController.PlayBladeVisualState setter - Detects play blade state changes
- [x] PlayBladeController.IsDeckSelected setter - Detects deck selection
- [x] HomePageContentController.IsEventBladeActive setter - Detects event blade
- [x] HomePageContentController.IsDirectChallengeBladeActive setter - Detects direct challenge
- [x] BladeContentView.Show/Hide - Base class for all blade views
- [x] EventBladeContentView.Show/Hide - Specific event blade detection
- [x] Harmony flag approach - Overlay flags set immediately on Harmony events for reliable filtering

### PlayBlade/Deck Selection
- [x] PlayBlade detection - `_playBladeActive` flag set by Harmony patches
- [x] Blade element filtering - Shows blade elements, hides HomePage background
- [x] Deck name extraction - `TryGetDeckName()` extracts from TMP_InputField
- [x] Deck entry pairing - UI (select) and TextBox (edit) paired per deck
- [x] Alternate actions - Shift+Enter to edit deck name, Enter to select
- [x] Play button activation - Opens PlayBlade correctly
- [x] Find Match button - Activates and shows tooltip
- [x] Mode tabs (Play/Ranked/Brawl) - Produce activation sounds
- [~] **Deck selection - PARTIALLY WORKING** (January 2026)
  - EventSystem selects deck correctly
  - Pointer events sent but `IsDeckSelected` not triggered
  - May require starter deck cloning first (see BEST_PRACTICES.md)

### UI Utilities
- [x] UIElementClassifier - Element role detection and filtering
  - Detects: Button, Link, Toggle, Slider, ProgressBar, Navigation, Internal
  - Filters internal elements (blockers, tooltips, gradients)

### Card System
- [x] CardDetector - Universal detection with caching
- [x] CardInfoNavigator - Arrow up/down through card details
- [x] Automatic card navigation - No Enter required, just Tab to card and use arrows
- [x] Lazy loading - Card info only extracted on first arrow press (performance)
- [x] Mana cost parsing (sprite tags to readable text for UI, ManaQuantity[] for Model)
- [x] Model-based extraction - Uses game's internal Model data for battlefield cards
- [x] UI fallback - Falls back to TMP_Text extraction for Meta scene cards (rewards, deck building)
- [ ] Rules text from Model - Abilities array parsing not yet implemented (future enhancement)

### Zone System (Duel)
- [x] ZoneNavigator - Separate service for zone navigation
- [x] Zone discovery - Finds all zone holders (Hand, Battlefield, Graveyard, etc.)
- [x] Card discovery in zones - Detects CDC # cards as children
- [x] Zone shortcuts - C, B, G, X, S to jump to zones
- [x] Card navigation - Left/Right arrows within current zone
- [x] EventSystem conflict fix - Clears selection to prevent UI cycling
- [x] Card playing - Enter key plays cards from hand (double-click + center click approach)

### Card Playing (Working)
- [x] Lands - Play correctly, detected via card type before playing
- [x] Creatures - Play correctly, detected via stack increase event (DidSpellCastRecently)
- [x] Non-targeted spells - Play correctly, go on stack and resolve
- [x] Targeted spells - Tab targeting mode working (HotHighlight detection)

### Duel Announcer System
- [x] Harmony patch infrastructure - UXEventQueuePatch intercepts game events
- [x] UXEventQueue.EnqueuePending patched - Both single and multi-event versions
- [x] Turn announcements - "Turn X. Your turn" / "Turn X. Opponent's turn"
- [x] Card draw announcements - "Drew X card(s)" / "Opponent drew X card(s)"
- [x] Spell resolution - "Spell resolved" when stack empties
- [x] Stack announcements - "Cast [name], [P/T], [rules]" when spell goes on stack (full card info)
- [x] Zone change tracking - Tracks card counts per zone to detect changes
- [x] Spell resolve tracking - `_lastSpellResolvedTime` set on stack decrease
- [x] Phase announcements - Main phases, combat steps (declare attackers/blockers, damage, end combat)
- [x] Combat announcements - "Combat begins", "Attacker declared", "Attacker removed"
- [x] Opponent plays - "Opponent played a card" (hand count decrease detection)
- [x] Code cleanup - Debug logging removed, dead code removed, file optimized

### Target Selection System (Working)
- [x] TargetNavigator service - Tab/Shift+Tab/Enter/Escape handling (~340 lines)
- [x] TargetInfo model - CardTargetType enum, target data structure
- [x] Integration with DuelNavigator - TargetNavigator created and connected
- [x] Integration with ZoneNavigator - Passes TargetNavigator to UIActivator
- [x] Land detection - Skips targeting mode for lands (via card type check)
- [x] Submit button detection - Detects "Submit X" or "Cancel" button to enter targeting mode
- [x] **HotHighlight detection** - Valid targets have active `HotHighlightBattlefield(Clone)` child
- [x] Tab targeting for targeted spells - **Working** (uses HotHighlight detection)
- [x] High priority announcements - Always announces on Tab (bypasses duplicate check for single-target case)
- [x] Battlefield row navigation during targeting - Left/Right navigate within current row (Shift+B/etc to switch rows)

### Highlight Navigator System (Working)
- [x] HighlightNavigator service - Tab cycles playable cards outside targeting mode
- [x] HotHighlight detection - Finds cards with active highlight (playable indicator)
- [x] Integration with DuelNavigator - Priority: TargetNavigator > HighlightNavigator > ZoneNavigator
- [x] Tab/Shift+Tab cycling - Cycles through all playable cards (hand + battlefield)
- [x] Enter to play - Activates currently highlighted card
- [x] Replaces default Tab behavior - No more cycling through useless UI buttons
- [x] High priority announcements - Always announces on Tab (bypasses duplicate check)
- [x] Priority pass announcements - When no playable cards, announces primary button text (e.g., "Opponent's Turn", "Next") instead of generic "No playable cards"

### Discard Navigator System (Working)
- [x] DiscardNavigator service - Handles forced discard selection
- [x] Submit button detection - Detects "Submit X" button to identify discard mode
- [x] Required count parsing - Parses "Discard a card" or "Discard X cards" from prompt
- [x] Entry announcement - Announces "Discard X card(s)" when mode detected
- [x] Selection state in announcements - Cards announce "selected for discard" when selected (silent otherwise)
- [x] Enter to toggle - Clicks card to select/deselect for discard
- [x] Selection count announcement - Announces "X cards selected" after toggle (0.2s delay)
- [x] Space to submit - Validates count matches required, submits or announces error
- [x] Integration with DuelNavigator - Priority: TargetNavigator > DiscardNavigator > HighlightNavigator
- [x] Integration with ZoneNavigator - Adds selection state to card announcements

### Combat Navigator System (Complete)
- [x] CombatNavigator service - Handles declare attackers/blockers phases
- [x] Phase tracking in DuelAnnouncer - `IsInDeclareAttackersPhase`, `IsInDeclareBlockersPhase` properties
- [x] Integration with DuelNavigator - Priority: TargetNavigator > DiscardNavigator > CombatNavigator > HighlightNavigator
- [x] Integration with ZoneNavigator - `GetCombatStateText()` adds combat state to card announcements

**Combat State Detection:**
Cards announce their current state based on active UI indicators:
- "attacking" - `IsAttacking` child is active (creature declared as attacker)
- "blocking" - `IsBlocking` child is active (creature assigned to block)
- "selected to block" - Has `CombatIcon_BlockerFrame` + `SelectedHighlightBattlefield` (clicked, not yet assigned)
- "can block" - Has `CombatIcon_BlockerFrame` only (during declare blockers phase)
- "tapped" - Has `TappedIcon` (shown for non-attackers only, since attackers are always tapped)

**Declare Attackers Phase:**
- [x] F key handling - Clicks "All Attack" or "X Attackers" button (same as Space)
- [x] Shift+F key handling - Clicks "No Attacks" button to skip attacking
- [x] Space key handling - Clicks "All Attack" or "X Attackers" button (redundant with F)
- [x] Button detection - Finds `PromptButton_Primary` with text containing "Attack"
- [x] No Attack button detection - Finds `PromptButton_Secondary` with text containing "No"
- [x] Opponent turn filtering - Ignores "Opponent's Turn" button text
- [x] Attacker state detection - `IsAttacking` child indicates declared attacker state

**Note:** Game requires two presses to complete attack declaration:
1. First F/Space: Selects attackers (button shows "All Attack")
2. Second F/Space: Confirms attackers (button shows "X Attackers")

**Declare Blockers Phase:**
- [x] F key handling - Clicks "X Blocker" or "Next" button (confirm blocks)
- [x] Shift+F key handling - Clicks "No Blocks" or "Cancel Blocks" button
- [x] Space key handling - Same as F (confirm blocks)
- [x] Button detection - Primary button matches "Blocker", "Next", "Done", "Confirm", "OK" (excludes "No Blocks")
- [x] No block button detection - Secondary button matches "No " or "Cancel"
- [x] **Full blocker state announcements** - "can block", "selected to block", "blocking" (see Combat State Detection above)
- [x] **Attacker announcements during blockers** - Enemy attackers announce ", attacking"
- [x] **Blocker state detection** - Checks for active `IsBlocking` child (not just presence of BlockerFrame)
- [x] **Blocker selection tracking** - Tracks selected blockers via `SelectedHighlightBattlefield` + `CombatIcon_BlockerFrame`
- [x] **Combined P/T announcements** - Announces "X/Y blocking" when selection changes

**Blocker Selection System:**
- `IsCreatureSelectedAsBlocker(card)` - Checks for both `CombatIcon_BlockerFrame` AND `SelectedHighlightBattlefield` active
- `FindSelectedBlockers()` - Returns all CDC cards currently selected as blockers
- `GetPowerToughness(card)` - Extracts P/T from card using `CardDetector.ExtractCardInfo()`
- `CalculateCombinedStats(blockers)` - Sums power and toughness across all selected blockers
- `UpdateBlockerSelection()` - Called each frame, detects selection changes, announces combined stats
- Tracking uses `HashSet<int>` of instance IDs to detect changes efficiently
- Resets tracking when entering/exiting blockers phase

### Player Portrait Navigator System (Working)
- [x] PlayerPortraitNavigator service - V key enters player info zone
- [x] State machine - Inactive, PlayerNavigation, EmoteNavigation states
- [x] Player switching - Left/Right arrows switch between you and opponent
- [x] Property cycling - Up/Down arrows cycle through: Life, Timer, Timeouts, Games Won, Rank
- [x] Life includes username - "Username, X life" format
- [x] Life totals from game state - Uses GameManager.CurrentGameState for accurate values
- [x] Emote navigation entry - Enter on your portrait opens emote wheel
- [x] Emote wheel discovery - Finds PhraseTransformPosition buttons (filters NavArrow buttons)
- [x] Exit handling - Backspace exits the zone
- [x] **Enter key blocking - WORKING** (January 2026)
  - KeyboardManagerPatch blocks Enter entirely in DuelScene
  - Game never sees Enter, so "Pass until response" never triggers
  - Our navigators handle all Enter presses
- [x] **Input priority fix** (January 2026)
  - PortraitNavigator now runs BEFORE BattlefieldNavigator
  - Arrow keys work correctly when in player info zone
- [x] **Focus-based zone management** (January 2026)
  - Player zone now manages EventSystem focus like other zones
  - On entry: stores previous focus, sets focus to HoverArea
  - On exit: restores previous focus
  - Auto-exits when focus moves to non-player-zone element

**Player Info Zone Shortcuts:**
- V: Enter player info zone (starts on your info)
- Left/Right: Switch between you and opponent (preserves property index)
- Up/Down: Cycle properties (Life, Timer, Timeouts, Games Won, Rank)
- Enter: Open emote wheel (your portrait only)
- Backspace: Exit zone (restores previous focus)

**Confirmed Working (January 2026):**
- Life totals display correctly (not assumptions, reads from game state)
- Arrow navigation works (Left/Right to switch players, Up/Down for properties)
- Blocking Enter solves auto-skip problem
- Focus management prevents key consumption when not in zone

**Files:**
- `src/Core/Services/PlayerPortraitNavigator.cs` - Main navigator with focus management
- `src/Core/Services/InputManager.cs` - Key consumption infrastructure
- `src/Patches/KeyboardManagerPatch.cs` - Harmony patch for game's KeyboardManager

### Browser Navigator System (Partially Working)
- [x] BrowserNavigator service - Unified handler for browser-style UIs
- [x] Browser scaffold detection - Finds `BrowserScaffold_*` GameObjects (Scry, Surveil, etc.)
- [x] Card holder detection - `BrowserCardHolder_Default` (keep) and `BrowserCardHolder_ViewDismiss` (dismiss)
- [x] Tab navigation - Cycles through cards in browser
- [x] Card state announcements - "keep on top" or "put on bottom" based on holder (only when buttons present)
- [x] Integration with DuelNavigator - BrowserNavigator.Update() and HandleInput() called
- [x] Integration with DuelAnnouncer - `MultistepEffectStartedUXEvent` triggers browser entry announcement
- [x] Space to confirm - Clicks `PromptButton_Primary` (Keep, Done, etc.)
- [~] Mulligan browser - Partially working (see details below)
- [ ] Enter to toggle position - API discovery in progress (card doesn't move between holders yet)
- [ ] Surveil testing - Same UI pattern as scry, needs verification

**Browser Types Supported:**
- Scry - View top card(s), choose keep on top or put on bottom
- Surveil - Similar to scry, dismissed cards go to graveyard
- Mulligan/Opening Hand - View starting hand, keep or mulligan (partially working)

**Mulligan Implementation Status (January 2026):**

Mulligan has two distinct UI flows depending on play/draw:

1. **On the draw (opponent starts first):**
   - `BrowserScaffold_OpeningHand` appears for opponent's hand view
   - `BrowserScaffold_Mulligan` with `KeepButton`/`MulliganButton` for your decision
   - Detection via `IsMulliganBrowserVisible()` checks for these buttons

2. **On the play (you start first):**
   - No scaffold detected, falls back to `CardBrowserCardHolder`
   - Uses `PromptButton_Primary`/`PromptButton_Secondary` for actions
   - Cards found but shown differently

**Mulligan Phases:**
- Phase 1: View 7 cards, decide Keep or Mulligan
- Phase 2: Wait for opponent's decision
- Phase 3 (London Mulligan): If mulliganed, select card(s) to put on bottom (1 per mulligan taken)

**What Works:**
- Tab cycles through opening hand cards
- Cards announced without "keep on top" (viewing mode, no selection state)
- Space clicks PromptButton_Primary (announces button label like "Keep" or "Opponent's Turn")
- Browser exits properly when mulligan buttons disappear

**Known Issues/Bugs:**
- Some unspecified bugs reported during mulligan testing (need reproduction steps)
- Card count varies (sometimes 1 card, sometimes 6-7) depending on phase
- Button text varies based on game state ("Keep", "Opponent's Turn", "Done")
- Phase 3 (putting cards back after mulligan) needs more testing

**Current State:**
Mulligan is partially working. Tab navigation and Space confirmation work. Some bugs remain that need specific reproduction steps to investigate.

## Known Issues

### Combat Navigator
- **Blocker selection after targeting**: There may be strange interactions after selecting a target for blocks. Needs further testing.

### DiscardNavigator
- **Log flooding fixed**: Removed per-frame logging from `GetSubmitButtonInfo()` that was flooding logs when "Submit X" pattern matched.

### Card Playing
- **Rapid Enter presses**: Multiple rapid Enter presses can trigger card play sequence multiple times, potentially causing issues if game enters targeting mode.

## Next Steps

### Immediate - Gameplay
1. Add life total announcements (L shortcut)
2. Add mana pool info (A shortcut already mapped)

### Upcoming
1. Creature death announcements with card names
2. Exile announcements with card names
3. Graveyard card announcements with names

### Future
1. Deck building interface
2. Collection browser
3. Draft/sealed events
4. Main menu navigation

## Deployment

### File Locations
- Mod: `C:\Program Files\Wizards of the Coast\MTGA\Mods\MTGAAccessibility.dll`
- Tolk: `C:\Program Files\Wizards of the Coast\MTGA\Tolk.dll`
- NVDA client: `C:\Program Files\Wizards of the Coast\MTGA\nvdaControllerClient64.dll`
- Log: `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`

### Build & Deploy
Build outputs to game's Mods folder. Tolk DLLs must be in game root.

## Modding Stack

- MelonLoader - Mod loader
- HarmonyX (0Harmony.dll) - Method patching for game event interception
- Tolk - Screen reader communication
- Target: .NET Framework 4.7.2

## Technical Notes

### Harmony Patching (DuelAnnouncer)
The DuelAnnouncer uses manual Harmony patching (not attribute-based) because:
- MelonLoader's auto-patching runs before game assemblies are loaded
- Manual patching in OnInitializeMelon() ensures types are available

Patched method: `Wotc.Mtga.DuelScene.UXEvents.UXEventQueue.EnqueuePending()`
- Intercepts all UX events as they flow through the game's event system
- Postfix reads event data without modifying game state

Key event types detected:
- `UpdateTurnUXEvent` - Turn changes (fields: `_turnNumber`, `_activePlayer`)
- `UpdateZoneUXEvent` - Zone state changes (field: `_zone` with zone info string)
- `UXEventUpdatePhase` - Phase changes (fields: `<Phase>k__BackingField`, `<Step>k__BackingField`)
- `ToggleCombatUXEvent` - Combat state (field: `_CombatMode`)

Stack card detection: Uses delayed coroutine (3 frames + 0.2s retry) to allow card to appear in scene before reading name via `CardDetector.GetCardName()`.

Privacy protection: Never reveals opponent's hidden information (hand contents, library).

### Harmony Patching (PanelStatePatch - Menu Panels)
Detects menu panel state changes (open/close) via Harmony patches for reliable overlay handling.

Patched controllers: NavContentController, SettingsMenu, DeckSelectBlade

See `docs/BEST_PRACTICES.md` "Panel State Detection (Harmony Patches)" section for full technical details.

## Recent Changes

### January 2026 - Player Info Zone and Input System Investigation

**Player Info Zone (V Key):**

New navigation zone for accessing player information during duels:
- V key enters the zone, starting on your info
- Left/Right switches between you and opponent
- Up/Down cycles through properties: Life (with username), Timer, Timeouts, Games Won
- Enter opens emote wheel (your portrait only)
- Escape or Tab exits the zone

**Input System - Scene-Based Key Blocking:**

MTGA uses two parallel input systems:
1. **Legacy Input** (`UnityEngine.Input`) - Simple polling, used by our mod
2. **New InputSystem** (`Unity.InputSystem`) - Event-driven, used by game via KeyboardManager

**The Problem:**
When mod handles Enter, the game's KeyboardManager also sees it and triggers "Pass until response" - causing unintended turn passes and auto-skip issues.

**Solution - Block Enter in DuelScene:**
Instead of complex per-context key consumption, we use a simpler approach:
- `KeyboardManagerPatch` blocks Enter ENTIRELY when in DuelScene
- Our mod handles ALL Enter presses (card playing, targets, player info, etc.)
- Game never sees Enter, so "Pass until response" never triggers

This solves multiple problems at once:
- Auto-skip prevention (game can't pass without us knowing)
- Player info zone works correctly (Enter opens emotes, not pass priority)
- Card playing works correctly (no double-handling)

**Why NOT full InputSystem migration:**
- Current approach is simpler and works well
- Would require touching 16+ files
- Mod-only elements (player info zone) can't benefit from InputSystem
- Risk of breaking existing functionality

**Files Added/Changed:**
- `src/Core/Services/PlayerPortraitNavigator.cs` - Player info zone navigation
- `src/Patches/KeyboardManagerPatch.cs` - Harmony patch with scene-based blocking
- `src/Core/Services/InputManager.cs` - Per-key consumption for other scenes
- `src/MTGAAccessibility.csproj` - Added Unity.InputSystem.dll reference

### January 2026 - Model-Based Card Info Extraction (CardDetector Refactor)

**Problem:** Battlefield cards in "compacted" state have hidden/empty TMP_Text elements, causing
card info navigation (Arrow Up/Down) to fail for these cards.

**Solution:** CardDetector now extracts card info from the game's internal Model data (via
DuelScene_CDC component) instead of relying solely on UI text extraction.

**Implementation Details:**

1. **Name Lookup via GrpId**
   - Model stores card database ID as `GrpId` (not `TitleId` which is a localization key)
   - Uses `CardTitleProvider.GetCardTitle(grpId, false, null)` from `GameManager.CardDatabase`
   - Caches provider reference for performance

2. **Mana Cost via PrintedCastingCost**
   - Model has `PrintedCastingCost` as `ManaQuantity[]` array
   - Each `ManaQuantity` represents ONE mana symbol with properties:
     - `Color` (ManaColor enum: White, Blue, Black, Red, Green)
     - `IsGeneric` (bool for colorless mana)
     - `IsPhyrexian`, `Hybrid`, `AltColor` for special mana
   - Generic mana counted and shown as number (e.g., "2")
   - Colored mana listed individually (e.g., "2, White, Blue")

3. **Type Line from Model**
   - Built from `Supertypes` + `CardTypes` + `Subtypes` arrays
   - Each is an array of enum values (e.g., `Basic`, `Land`, `Plains`)
   - Format: "Basic Land - Plains"

4. **Power/Toughness from Model**
   - `Power` and `Toughness` properties use `StringBackedInt` type
   - `StringBackedInt` has `RawText` (for variable P/T like "*") and `Value` (numeric)
   - Only extracted for creatures (cards with "Creature" in CardTypes)
   - Handles animated lands, vehicles, creature-lands automatically

5. **Rules Text via AbilityTextProvider**
   - Model has `Abilities` array of `AbilityPrintingData` objects
   - Each ability has `Id` (uint) for lookup and `TextId` for localization
   - Uses `GameManager.CardDatabase.AbilityTextProvider.GetAbilityTextByCardAbilityGrpId()`
   - Method signature: `(cardGrpId, abilityId, abilityIds[], cardTitleId, languageCode, formatted)`
   - Extracts readable rules text for all abilities on the card

6. **Fallback Strategy**
   - `ExtractCardInfo()` tries Model extraction first
   - Falls back to UI text extraction if Model fails (no CDC component)
   - Meta scene cards (rewards, deck building) use UI extraction

**Shared Utilities Added to CardDetector:**
- `GetDuelSceneCDC(GameObject)` - Gets DuelScene_CDC component
- `GetCardModel(Component)` - Gets Model object from CDC
- `GetNameFromGrpId(uint)` - Looks up card name via CardTitleProvider
- `ParseManaQuantityArray(IEnumerable)` - Parses mana cost array
- `GetStringBackedIntValue(object)` - Extracts value from StringBackedInt (P/T)
- `GetAbilityTextFromProvider(...)` - Looks up ability text via AbilityTextProvider
- `FindAbilityTextProvider()` - Locates AbilityTextProvider in GameManager.CardDatabase

**BattlefieldNavigator Updated:**
- Now uses `CardDetector.GetDuelSceneCDC()` instead of local duplicate method

**Files Changed:**
- `src/Core/Services/CardDetector.cs` - Major refactor with Model extraction and ability text

### January 2026 - UIActivator CustomButton Fix

**Problem:** Play menu buttons (tabs, deck selection) weren't triggering game state changes.
Clicking produced sounds but didn't actually switch modes or select decks.

**Root Cause:** UIActivator was using `_onClick` reflection FIRST for CustomButtons. The
`_onClick` UnityEvent only handles secondary effects (sounds). The actual game logic lives
in `IPointerClickHandler.OnPointerClick()`.

**Changes to `UIActivator.cs`:**
1. Added `HasCustomButtonComponent()` helper method
2. For CustomButtons: Now tries pointer simulation FIRST, then onClick reflection
3. Added `EventSystem.SetSelectedGameObject()` before sending pointer events
4. Added Submit event (`ExecuteEvents.submitHandler`) for keyboard activation
5. Added logging for Toggle activation

**Current Status:**
- Play/Find Match buttons work correctly
- Mode tabs produce sounds (partial activation)
- Deck selection sends all events but game doesn't respond
- Investigation suggests starter decks may need cloning first

**Files Changed:**
- `src/Core/Services/UIActivator.cs` - Activation strategy reordered
- `docs/BEST_PRACTICES.md` - CustomButton pattern documented
- `docs/MOD_STRUCTURE.md` - Status updated

### January 2026 - Content Panel NavBar Filtering & Backspace Navigation (UNTESTED)

**Status: UNTESTED - May need to be reverted if issues occur**

**Problem:** When navigating to menu pages (Decks, Profile, Store, Learn, etc.), the NavBar
buttons (Home, Profile, Packs, Store, Mastery, etc.) were included in Tab navigation, making
it tedious to navigate through page-specific content.

**Solution Part 1 - Consolidated Content Panel Filtering:**

Previously had separate flags for different panel types:
- `_settingsOverlayActive` - Settings menu
- `_deckSelectOverlayActive` - Deck selection during play
- `_playBladeActive` - Play blade (kept separate - has special logic)
- `_deckManagerActive` - Decks page only

Consolidated into two flags:
- `_playBladeActive` - Play blade only (kept separate due to special blade-inside filtering)
- `_contentPanelActive` - ALL content panels that should filter NavBar

Content panels now handled by `_contentPanelActive`:
- `DeckManagerController` - Decks page
- `ProfileContentController` - Profile page
- `ContentController_StoreCarousel` - Store page
- `LearnToPlayControllerV2` - Learn to Play page
- `WrapperDeckBuilder` - Deck builder (editing a deck)
- `MasteryContentController` - Mastery page
- `AchievementsContentController` - Achievements page
- `PackOpeningController` - Pack opening
- `SettingsMenu` - Settings overlay
- `DeckSelectBlade` - Deck selection during play

**Solution Part 2 - Backspace to Home Navigation:**

Since NavBar buttons are filtered out, added Backspace key to navigate back to Home:
- When `_contentPanelActive` is true and Backspace is pressed
- Finds Home button directly in scene at `NavBar_Desktop_16x9(Clone)/Base/Nav_Home`
- Activates it to return to main menu
- Announces "Returning to Home"

**Key Code Changes in `GeneralMenuNavigator.cs`:**
1. Removed `_settingsOverlayActive` and `_deckSelectOverlayActive` flags
2. Renamed `_deckManagerActive` to `_contentPanelActive`
3. Updated `OnPanelStateChangedExternal()` to detect all content panel types
4. Updated `IsInForegroundPanel()` overlay check to use consolidated flags
5. Added `HandleCustomInput()` override for Backspace key
6. Added `NavigateToHome()` method to find and activate Home button

**How to Revert if Issues Occur:**
```bash
git revert HEAD
```
Or manually:
1. Restore separate flags: `_settingsOverlayActive`, `_deckSelectOverlayActive`, `_deckManagerActive`
2. Remove `_contentPanelActive` and its handlers
3. Remove `HandleCustomInput()` and `NavigateToHome()` methods
4. Restore individual panel handling in `OnPanelStateChangedExternal()`

**Files Changed:**
- `src/Core/Services/GeneralMenuNavigator.cs` - Content panel filtering and Backspace navigation

### January 2026 - Combat Shortcuts (F/Shift+F) and Bug Fixes

**New Combat Shortcuts:**
- F key: Press "All Attack" / "X Attackers" button during declare attackers phase
- Shift+F key: Press "No Attacks" button to skip attacking
- Space key: Same as F (kept for redundancy/user preference)

**Bug Fixes:**

1. **GeneralMenuNavigator not deactivating for DuelScene**
   - Problem: `ExcludedScenes` contained `"Duel"` but actual scene name is `"DuelScene"`
   - Fix: Updated to `"DuelScene"`, `"DraftScene"`, `"SealedScene"`
   - Result: DuelNavigator now properly activates when entering a duel

2. **NullReferenceException in BaseNavigator.GetElementAnnouncement**
   - Problem: Unity destroyed objects bypass C# `?.` null-conditional operator
   - Fix: Changed to explicit `if (navElement.GameObject != null)` check before `GetComponent`
   - Result: No more crashes when UI elements are destroyed during navigation

**Files Changed:**
- `src/Core/Services/CombatNavigator.cs` - Added F and Shift+F key handling, added `TryClickNoAttackButton()` and `FindNoAttackButton()` methods
- `src/Core/Services/GeneralMenuNavigator.cs` - Fixed ExcludedScenes to use correct scene names
- `src/Core/Services/BaseNavigator.cs` - Fixed Unity null check in GetElementAnnouncement
- `CLAUDE.md` - Updated shortcuts documentation

### January 2026 - Declare Blockers Phase Implementation

**New Features:**

1. **Blocker Shortcuts (F/Shift+F)**
   - F or Space: Clicks "X Blocker" or "Next" button to confirm blocks
   - Shift+F: Clicks "No Blocks" or "Cancel Blocks" button

2. **Improved Button Detection**
   - Primary button matches: "Blocker", "Next", "Done", "Confirm", "OK"
   - Explicitly skips buttons containing "No " to avoid matching "No Blocks"
   - Secondary button for no blocks matches: "No " or "Cancel"

3. **Fixed Blocker State Detection**
   - `IsCreatureBlocking()` now checks for active `IsBlocking` child
   - Previously incorrectly checked for `CombatIcon_BlockerFrame` (always present on potential blockers)

4. **Enemy Attacker Announcements During Blockers**
   - `GetCombatStateText()` now checks `IsCreatureAttacking()` during blockers phase
   - Enemy attackers announce ", attacking" so player knows what they're blocking

5. **Blocker Selection Tracking with Combined P/T**
   - Tracks creatures selected as blockers via `SelectedHighlightBattlefield` + `CombatIcon_BlockerFrame`
   - Calculates combined power/toughness of all selected blockers
   - Announces "X/Y blocking" whenever selection changes (add/remove blocker)
   - Uses `HashSet<int>` of instance IDs for efficient change detection
   - Resets tracking when entering/exiting blockers phase

**New Methods in CombatNavigator.cs:**
- `IsCreatureSelectedAsBlocker(card)` - Checks for selection highlight + blocker frame
- `FindSelectedBlockers()` - Returns all currently selected blocker cards
- `GetPowerToughness(card)` - Extracts P/T using `CardDetector.ExtractCardInfo()`
- `CalculateCombinedStats(blockers)` - Sums power and toughness
- `UpdateBlockerSelection()` - Called each frame, detects changes, announces stats
- `TryClickNoBlockButton()` - Clicks "No Blocks" or "Cancel Blocks" button

**Fields Added:**
- `_previousSelectedBlockerIds` - HashSet for tracking selection state
- `_wasInBlockersPhase` - Bool to detect phase transitions

**Files Changed:**
- `src/Core/Services/CombatNavigator.cs` - Full blockers phase implementation

### January 2026 - Improved Announcements and Navigation

**1. Removed Negative State Announcements**

Previously, cards announced "not selected", "not attacking", "not blocking" which cluttered screen reader output with useless information. Now only active states are announced:
- Cards only say "selected for discard" when selected, silent otherwise
- Creatures only announce combat states when active (attacking, blocking, etc.)

**Files Changed:**
- `src/Core/Services/DiscardNavigator.cs` - Removed "not selected" announcement
- `src/Core/Services/CombatNavigator.cs` - Removed "not attacking" and "not blocking" announcements

**2. Battlefield Row Navigation Changed to Shift+Arrow**

Changed battlefield row switching from Alt+Up/Down to Shift+Up/Down for consistency with other Shift-modified shortcuts (Shift+A/B/R for enemy rows).

**Shortcuts Updated:**
- Shift+Up: Previous row (towards enemy side)
- Shift+Down: Next row (towards player side)
- Left/Right: Navigate within row (unchanged)

**Files Changed:**
- `src/Core/Services/BattlefieldNavigator.cs` - Changed key detection from Alt to Shift
- `CLAUDE.md` - Updated shortcut documentation

**3. Enhanced Cast Announcements**

When spells are cast, announcements now include full card information:
- Card name
- Power/toughness (for creatures)
- Rules text

Example: "Cast Lightning Bolt, Lightning Bolt deals 3 damage to any target"

**Implementation:**
- `GetTopStackCard()` - Returns the top stack card GameObject (renamed from `GetTopStackCardName()`)
- `BuildCastAnnouncement(card)` - Extracts full info via `CardDetector.ExtractCardInfo()`

**Files Changed:**
- `src/Core/Services/DuelAnnouncer.cs` - Enhanced stack card announcements

**4. Complete Combat State Detection**

Replaced phase-specific hardcoded checks with universal state indicator detection. Cards now properly announce all combat-relevant states:

| State | Indicator | When Shown |
|-------|-----------|------------|
| "attacking" | `IsAttacking` child active | Creature declared as attacker |
| "blocking" | `IsBlocking` child active | Creature assigned to block |
| "selected to block" | `CombatIcon_BlockerFrame` + `SelectedHighlightBattlefield` | Creature clicked but not assigned |
| "can block" | `CombatIcon_BlockerFrame` only | During declare blockers phase |
| "tapped" | `TappedIcon` active | Non-attacking tapped creatures |

**Implementation:**
- `GetCombatStateText(card)` completely rewritten to scan for UI indicators
- Detects states by checking for specific active child GameObjects
- No longer relies on phase-specific logic for state detection

**Files Changed:**
- `src/Core/Services/CombatNavigator.cs` - Rewrote `GetCombatStateText()` method

### January 2026 - Menu Navigation Fixes (Checkbox & Position Preservation)

**Problem 1: Checkbox toggle rescans were being skipped**

When toggling deck folder checkboxes (My Decks, Starter Decks, etc.), the rescan to update
the deck list was sometimes skipped due to debounce. The debounce (1.0s) would skip rescans
if another rescan happened recently (e.g., from opening the blade).

**Solution:** Added `force` parameter to `TriggerRescan()` that bypasses the debounce check.
Toggle activations now call `TriggerRescan(force: true)` to ensure the rescan always happens.

**Problem 2: Position reset after rescan**

After any rescan, the navigator's position would reset to the first element instead of
staying on the element the user was on.

**Root Cause:** The position preservation code was using `EventSystem.current?.currentSelectedGameObject`
to remember the selection, but the EventSystem selection isn't synced with the navigator's
internal `_currentIndex`. This resulted in finding the wrong element (or no element) after rescan.

**Solution:** Changed to use `_elements[_currentIndex].GameObject` - the navigator's own
tracked position - to remember which element the user was on before the rescan.

**Implementation Details:**

```csharp
// In GeneralMenuNavigator.TriggerRescan():
private void TriggerRescan(bool force = false)
{
    _rescanDelay = RESCAN_DELAY_SECONDS;
    if (force)
    {
        _forceRescan = true;  // Bypasses debounce in PerformRescan()
    }
}

// In GeneralMenuNavigator.PerformRescan():
// Remember navigator's current selection (not EventSystem's)
GameObject previousSelection = null;
if (_currentIndex >= 0 && _currentIndex < _elements.Count)
{
    previousSelection = _elements[_currentIndex].GameObject;
}
// ... after rediscovering elements ...
// Find and restore position
```

**Files Changed:**
- `src/Core/Services/GeneralMenuNavigator.cs` - Added `_forceRescan` flag, updated `TriggerRescan()`
  with force parameter, fixed position preservation to use navigator's `_currentIndex`

### January 2026 - Centralized Announcement Strings (Localization-Ready)

**Problem:** Announcement strings were hardcoded throughout the codebase in 16+ files, making
future localization impossible and causing inconsistent messaging.

**Solution:** Created `Core/Models/Strings.cs` to centralize all user-facing announcement text.

**New File - `Strings.cs`:**
- Static class with constants for static strings
- Methods for dynamic strings with parameters
- Organized by category (General, Activation, Menu, Login, Battlefield, Zone, Targeting, Combat, etc.)

**Additional Change - Removed "Activated" Announcements:**
Successful activation announcements ("Activated {name}") were informational clutter. These have
been removed from all navigators. Only failure announcements are kept ("Cannot activate {name}").

**Files Changed:**
- `src/Core/Models/Strings.cs` - NEW: Centralized announcement strings
- `src/Core/Services/BattlefieldNavigator.cs` - Uses Strings, removed success announcements
- `src/Core/Services/ZoneNavigator.cs` - Uses Strings, removed success announcements
- `src/Core/Services/HighlightNavigator.cs` - Uses Strings, removed success announcements
- `src/Core/Services/TargetNavigator.cs` - Uses Strings
- `src/Core/Services/CombatNavigator.cs` - Uses Strings
- `src/Core/Services/DiscardNavigator.cs` - Uses Strings
- `src/Core/Services/CardInfoNavigator.cs` - Uses Strings
- `src/Core/Services/BaseNavigator.cs` - Uses Strings
- `src/Core/Services/GeneralMenuNavigator.cs` - Uses Strings
- `src/Core/Services/EventTriggerNavigator.cs` - Uses Strings, removed success announcements
- `src/Core/Services/PreBattleNavigator.cs` - Uses Strings, removed success announcements
- `src/Core/Services/DuelNavigator.cs` - Removed success announcements
- `src/Core/Services/CodeOfConductNavigator.cs` - Uses Strings
- `src/Core/Services/UIFocusTracker.cs` - Uses Strings
- `src/Core/Services/DuelAnnouncer.cs` - Uses Strings
- `src/MTGAAccessibilityMod.cs` - Uses Strings
- `src/Contexts/Base/BaseMenuContext.cs` - Uses Strings
- `src/Contexts/Base/BaseNavigableContext.cs` - Uses Strings
- `src/Contexts/MainMenu/MainMenuContext.cs` - Uses Strings
- `src/Contexts/Login/LoginContext.cs` - Uses Strings
- `docs/BEST_PRACTICES.md` - Added Centralized Strings section with usage guidelines
- `docs/MOD_STRUCTURE.md` - Updated project layout, added this changelog entry

### January 2026 - Browser Navigator for Library Manipulation (In Progress)

**Goal:** Make scry, surveil, and similar library manipulation effects accessible.

**New File - `BrowserNavigator.cs`:**
Unified navigator for browser-style UIs that appear during library manipulation effects.

**How It Works:**
1. `DuelAnnouncer` detects `MultistepEffectStartedUXEvent` with `AbilityCategory` (Scry, Surveil, etc.)
2. `BrowserNavigator.Update()` finds active `BrowserScaffold_*` GameObjects
3. Cards are discovered in `BrowserCardHolder_Default` (keep on top) and `BrowserCardHolder_ViewDismiss` (put on bottom)
4. Tab navigates between cards, announcing name and current position state
5. Enter should toggle card between holders (in progress)
6. Space confirms the choice

**UI Structure Discovered:**
- `BrowserScaffold_Scry_Desktop_16x9(Clone)` - Main container
- `BrowserCardHolder_Default` - Cards to keep on top of library
- `BrowserCardHolder_ViewDismiss` - Cards to put on bottom
- `CardBrowserCardHolder` component - Game's card holder logic
- `PromptButton_Primary` / `PromptButton_Secondary` - Confirm/cancel buttons

**Current Status:**
- Detection and navigation work
- Toggle mechanism needs API discovery (next testing session will log `CardBrowserCardHolder` methods)

**Files Changed:**
- `src/Core/Services/BrowserNavigator.cs` - NEW: Browser navigation
- `src/Core/Services/DuelNavigator.cs` - Integration with BrowserNavigator
- `src/Core/Services/DuelAnnouncer.cs` - MultistepEffectStartedUXEvent handling, IsLibraryBrowserActive flag
- `src/Core/Models/Strings.cs` - Added browser-related strings

### January 2026 - Mulligan Browser and Player Info Zone Fixes

**Mulligan/Opening Hand Browser:**

1. **Different UI flows discovered:**
   - On the draw: `BrowserScaffold_Mulligan` with `KeepButton`/`MulliganButton`
   - On the play: Falls back to `CardBrowserCardHolder` with `PromptButton_Primary`/`Secondary`

2. **Fixes implemented:**
   - `IsMulliganBrowserVisible()` - Checks for mulligan buttons to detect active mulligan UI
   - Browser type change detection - Re-enters browser when scaffold type changes
   - `GetCardSelectionState()` - Returns null for Mulligan/OpeningHand browsers (no "keep on top" in viewing mode)
   - `GetCardSelectionState()` - Returns null when no buttons present (viewing mode vs scry mode)
   - `PromptButton_Primary`/`Secondary` fallback - Found when no other buttons detected
   - `ClickConfirmButton()` - Now finds `PromptButton_Primary_Desktop_16x9(Clone)` by prefix match
   - `RefreshBrowserButtons()` - Recovers when buttons are destroyed during UI updates
   - Scaffold discovery logging - Logs components/properties to find better APIs

3. **NullReferenceException fix:**
   - Added null check in `AnnounceCurrentButton()` for destroyed buttons

**Player Info Zone (V Key):**

1. **Input priority fix:**
   - Moved `PortraitNavigator.HandleInput()` BEFORE `BattlefieldNavigator.HandleInput()`
   - Arrow keys now work correctly when in player info zone

2. **Confirmed working:**
   - Life totals display correctly from `GameManager.CurrentGameState`
   - Enter key blocking solves auto-skip problem
   - Left/Right arrows switch between players
   - Up/Down arrows cycle properties

**Files Changed:**
- `src/Core/Services/BrowserNavigator.cs` - Mulligan detection, button fallbacks, null safety
- `src/Core/Services/DuelNavigator.cs` - Input priority reordering
- `src/Core/Models/Strings.cs` - Added `NoButtonsAvailable` string

### January 2026 - Focus Management Overhaul (Zone Navigation Consistency)

**Problem:** Player info zone (V key) was consuming keys even when focus had moved to other
elements (cards, buttons). This caused left/right arrows to stay "stuck" in player zone,
card info to display wrong zone context (Hand instead of Battlefield), and generally
inconsistent behavior compared to other zones.

**Root Cause:** Player info zone was a "virtual" overlay that didn't manage EventSystem focus.
Other navigators (ZoneNavigator, BattlefieldNavigator, HighlightNavigator) also didn't set
EventSystem focus when navigating to cards. This meant:
1. Player zone state persisted even when game focus moved elsewhere
2. Zone context for card info was lost when HandleFocusChanged overwrote it
3. No consistent focus chain for detecting when user left a zone

**Solution - Unified Focus Management:**

All navigators now manage EventSystem focus consistently:

1. **Player Zone Focus Management** (`PlayerPortraitNavigator.cs`)
   - `EnterPlayerInfoZone()` stores previous focus and sets focus to HoverArea
   - `ExitPlayerInfoZone()` restores the stored previous focus
   - `HandlePlayerNavigation()` checks if focus has moved away before processing keys
   - `OnFocusChanged()` auto-exits zone when focus moves to non-player element
   - `IsPlayerZoneElement()` helper identifies player zone UI elements

2. **Card Navigator Focus Management** (`ZoneNavigator.cs`, `BattlefieldNavigator.cs`, `HighlightNavigator.cs`)
   - All `AnnounceCurrentCard()` methods now call `EventSystem.SetSelectedGameObject(card)`
   - This triggers `OnFocusChanged` event, allowing other navigators to detect focus changes
   - Zone context is preserved because navigators call `PrepareForCard(card, zone)` BEFORE focus change

3. **HandleFocusChanged Cleanup** (`MTGAAccessibilityMod.cs`)
   - Removed `PrepareForCard()` call that was overwriting zone context
   - Navigators now handle card preparation with correct zone context
   - Added `GetSafeGameObjectName()` helper for Unity destroyed object safety
   - Added Unity destroyed object checks (`&& object`) throughout

4. **Emote Button Filtering** (`PlayerPortraitNavigator.cs`)
   - `SearchForEmoteButtons()` now skips NavArrow buttons and utility buttons
   - Only actual emote buttons (with emote text) are added to navigation list

5. **Combat Button Filtering** (`CombatNavigator.cs`)
   - Added `IsInEmotePanel()` helper to detect emote panel buttons
   - `LogBlockerPhaseButtons()` excludes emote panel buttons from debug output

**How It Works Now:**

The player info zone now behaves like card zones:
1. Press V → focus moves to HoverArea, previous focus stored
2. Navigate with arrows → virtual navigation within zone, focus stays on HoverArea
3. Press Backspace → focus restored to previous element, zone exited
4. Press C/G/B/Tab → other navigator sets focus to card, OnFocusChanged fires
5. Player zone detects non-player focus → auto-exits, keys go to other handlers

**Key Insight:** Focus changes are detected in two ways:
1. `OnFocusChanged` event (next frame) - for most focus changes
2. Direct focus check in `HandlePlayerNavigation()` (same frame) - catches Tab/key presses that change focus during input handling

**Files Changed:**
- `src/Core/Services/PlayerPortraitNavigator.cs` - Focus management, emote filtering
- `src/Core/Services/ZoneNavigator.cs` - EventSystem focus on card navigation
- `src/Core/Services/BattlefieldNavigator.cs` - EventSystem focus on card navigation
- `src/Core/Services/HighlightNavigator.cs` - EventSystem focus on card navigation
- `src/Core/Services/CombatNavigator.cs` - Emote panel button filtering
- `src/Core/Services/DuelNavigator.cs` - Exposed PortraitNavigator property
- `src/MTGAAccessibilityMod.cs` - Removed PrepareForCard override, added safe name helper
