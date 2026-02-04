# Accessible Arena - Structure & Status

## Project Layout

```
C:\Users\fabia\arena\
  src\
    AccessibleArena.csproj
    AccessibleArenaMod.cs      - MelonLoader entry point, holds central services
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
        CardModelProvider.cs     - Card data from game models (deck list, collection)

        # Central Services (held by main mod)
        AnnouncementService.cs   - Speech output management
        ContextManager.cs        - Game context tracking
        InputManager.cs          - Custom shortcut handling
        ShortcutRegistry.cs      - Keybind registration
        DebugConfig.cs           - Centralized debug logging flags (NEW Phase 2)
        DropdownStateManager.cs  - Unified dropdown state tracking (NEW Phase 4)
        CardInfoNavigator.cs     - Card detail navigation (arrow up/down)
        ZoneNavigator.cs         - Zone navigation in duel (C/B/G/X/S + arrows)
        BattlefieldNavigator.cs  - Battlefield row navigation (B/A/R keys)
        DuelAnnouncer.cs         - Game event announcements via Harmony
        HotHighlightNavigator.cs - Unified Tab navigation for playable cards, targets, AND selection mode
        CombatNavigator.cs       - Combat phase navigation (declare attackers/blockers)
        PlayerPortraitNavigator.cs - Player info zone (V key, life/timer/emotes)
        HelpNavigator.cs         - F1 help menu with navigable keybind list

        # Browser Navigation (library manipulation - scry, surveil, mulligan)
        BrowserDetector.cs       - Static browser detection and caching
        BrowserNavigator.cs      - Browser orchestration and generic navigation
        BrowserZoneNavigator.cs  - Two-zone navigation (Scry/London mulligan)

        old/                     - Deprecated navigators (kept for reference/revert)
          TargetNavigator.cs     - OLD: Separate target selection (replaced by HotHighlightNavigator)
          HighlightNavigator.cs  - OLD: Separate playable card cycling (replaced by HotHighlightNavigator)
          LoginPanelNavigator.cs - OLD: Login screen (replaced by GeneralMenuNavigator, Jan 2026)
          EventTriggerNavigator.cs - OLD: NPE screens (replaced by GeneralMenuNavigator, Jan 2026)
          DiscardNavigator.cs    - OLD: Discard selection (consolidated into HotHighlightNavigator, Jan 2026)

        PanelDetection/          - Panel state tracking system
          PanelStateManager.cs   - Single source of truth, owns all detectors directly
          PanelInfo.cs           - Panel data model + static metadata methods
          PanelType.cs           - Panel type enum
          HarmonyPanelDetector.cs   - Event-driven detection (PlayBlade, Settings, Blades)
          ReflectionPanelDetector.cs - IsOpen property polling (Login, PopupBase)
          AlphaPanelDetector.cs     - CanvasGroup alpha watching (Dialogs, Popups)
          old/detector-plugin-system/ - Archived: IPanelDetector, PanelDetectorManager, PanelRegistry

        # Navigator Infrastructure
        BaseNavigator.cs         - Abstract base for screen navigators
        NavigatorManager.cs      - Manages navigator lifecycle and priority

        # Menu Navigation Helpers
        MenuScreenDetector.cs    - Content controller detection, screen name mapping
        MenuPanelTracker.cs      - Panel/popup state tracking, overlay management

        ElementGrouping/         - Hierarchical menu navigation system
          ElementGroup.cs        - Group enum (Primary, Play, Content, PlayBladeTabs, etc.)
          ElementGroupAssigner.cs - Assigns elements to groups based on hierarchy
          GroupedNavigator.cs    - Two-level navigation (groups → elements)
          OverlayDetector.cs     - Detects active overlay (popup, social, PlayBlade, etc.)
          PlayBladeNavigationHelper.cs - State machine for PlayBlade tab/content navigation

        # Screen Navigators (all extend BaseNavigator)
        UIFocusTracker.cs            - EventSystem focus polling (fallback)
        AssetPrepNavigator.cs        - Download screen on fresh install (UNTESTED)
        PreBattleNavigator.cs        - VS screen before duel starts
        BoosterOpenNavigator.cs      - Pack contents after opening
        NPERewardNavigator.cs        - NPE reward screens
        RewardPopupNavigator.cs      - Rewards popup from mail claims, store purchases (NEW)
        DuelNavigator.cs             - Duel gameplay screen (delegates to ZoneNavigator)
        OverlayNavigator.cs          - Modal overlays (What's New, announcements)
        SettingsMenuNavigator.cs     - Settings menu (works in all scenes including duels)
        GeneralMenuNavigator.cs      - Main menu, login, NPE, and general menu screens

        # Deprecated navigators (in old/ folder)
        # WelcomeGateNavigator, LoginPanelNavigator, CodeOfConductNavigator
        # EventTriggerNavigator, DiscardNavigator, TargetNavigator, HighlightNavigator

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
  archive\                       - Archived files (from Phase 1 cleanup)
    analysis\                    - Old analysis text files
    backups\                     - Old backup folders
  installer\                     - AccessibleArenaInstaller source code
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
- [x] F1 Help Menu - Navigable keybind list (Up/Down navigation, Backspace/F1 to close)

### Screen Navigators
- [?] AssetPrepNavigator - Download screen on fresh install (UNTESTED, fail-safe design)
- [x] Login scene - Handled by GeneralMenuNavigator with password masking
- [x] Code of Conduct - Default navigation works correctly
- [x] PreBattleNavigator - VS screen before duel (Continue/Cancel buttons)
- [x] BoosterOpenNavigator - Pack contents after opening packs
- [x] RewardPopupNavigator - Rewards popup from mail/store (cards, packs, currency)
- [x] DuelNavigator - Duel gameplay (zone navigation, combat, targeting)
- [~] OverlayNavigator - Modal overlays (What's New carousel) - basic implementation
- [x] SettingsMenuNavigator - Settings menu accessible in all scenes including duels
- [x] GeneralMenuNavigator - Main menu, login, NPE screens, and general navigation
- [x] NPERewardNavigator - NPE reward screens (chest, deck boxes)

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
- [x] NavBarController.MailboxButton_OnClick - Mailbox open detection
- [x] NavBarController.HideInboxIfActive - Mailbox close detection
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
- [~] **Deck selection - PARTIALLY WORKING**
  - EventSystem selects deck correctly
  - Pointer events sent but `IsDeckSelected` not triggered
  - May require starter deck cloning first (see BEST_PRACTICES.md)

### Friends Panel (Social UI)
- [~] Friends panel accessibility - **PARTIALLY WORKING**
  - [x] F4 key toggles Friends panel open/closed
  - [x] Tab navigation within Friends panel
  - [x] Popup detection and automatic rescan
  - [x] Popup name announcements ("Invite Friend opened.")
  - [x] Input field support for friend invite
  - [x] Backspace closes Friends panel
  - [ ] Full input field change detection (partial)
  - [ ] Friend list navigation
  - [ ] Friend status announcements

### UI Utilities
- [x] UIElementClassifier - Element role detection and filtering
  - Detects: Button, Link, Toggle, Slider, ProgressBar, Navigation, Internal
  - Filters internal elements (blockers, tooltips, gradients)
  - Filters decorative Background elements without text
  - Special handling for FriendsWidget elements (hitbox/backer allowed)

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
- [x] Hidden zone counts - D (your library), Shift+D (opponent library), Shift+C (opponent hand)

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

### Unified HotHighlight Navigator System (Working)

**Replaced separate TargetNavigator + HighlightNavigator with unified HotHighlightNavigator.**

Key insight: The game's HotHighlight system correctly manages what's highlighted based on game state.
When in targeting mode, only valid targets have HotHighlight. When not targeting, only playable cards
have HotHighlight. We trust the game and scan ALL zones, letting the zone determine behavior.

- [x] HotHighlightNavigator service - Unified Tab navigation for playable cards AND targets
- [x] **Trusts game's highlight system** - No separate mode tracking needed
- [x] Scans ALL zones - Hand, Battlefield, Stack, Player portraits
- [x] Zone-based announcements:
  - Hand: "Shock, in hand, 1 of 2"
  - Battlefield: "Goblin, 2/2, opponent's Creature, 1 of 3"
  - Stack: "Lightning Bolt, on stack, 1 of 2"
  - Player: "Opponent, player, 3 of 3"
- [x] Zone-based activation:
  - Hand cards: Two-click to play
  - All other targets: Single-click to select
- [x] Player target detection - Scans MatchTimer objects for player portrait highlights
- [x] Primary button text - When no highlights, announces game state ("Pass", "Resolve", "Next")
- [x] Backspace to cancel - Available when targets are highlighted

**Old navigators moved to `src/Core/Services/old/` for reference/revert:**
- `TargetNavigator.cs` - Had separate _isTargeting mode, auto-enter/exit logic, zone scanning
- `HighlightNavigator.cs` - Had separate playable card cycling, rescan delay logic

**To revert to old navigators:**
1. Move files back from `old/` folder
2. Restore connections in DuelNavigator constructor
3. Replace HotHighlightNavigator.HandleInput() with old TargetNavigator + HighlightNavigator calls
4. Restore auto-detect/auto-exit logic in DuelNavigator.HandleCustomInput()

### Selection Mode (Discard, etc.) - Consolidated into HotHighlightNavigator
**January 2026:** DiscardNavigator was consolidated into HotHighlightNavigator for simpler architecture.

HotHighlightNavigator now detects "selection mode" (discard, choose cards to exile, etc.) by checking for a Submit button with a count AND no valid targets on battlefield. In selection mode, hand cards use single-click to toggle selection instead of two-click to play.

- [x] Selection mode detection - `IsSelectionModeActive()` checks for Submit button + no battlefield targets
- [x] Language-agnostic button detection - Matches any number in button text (works with "Submit 2", "2 abwerfen", "0 bestätigen")
- [x] Enter to toggle - Single-click on hand cards in selection mode
- [x] Selection count announcement - Announces "X cards selected" after toggle

**Old DiscardNavigator moved to:** `src/Core/Services/old/DiscardNavigator.cs`

### Combat Navigator System (Complete)
- [x] CombatNavigator service - Handles declare attackers/blockers phases
- [x] Phase tracking in DuelAnnouncer - `IsInDeclareAttackersPhase`, `IsInDeclareBlockersPhase` properties
- [x] Integration with DuelNavigator - Priority: BrowserNavigator > CombatNavigator > HotHighlightNavigator
- [x] Integration with ZoneNavigator - `GetCombatStateText()` adds combat state to card announcements
- [x] **Language-agnostic button detection** - Uses button GameObject names, not localized text

**Language-Agnostic Button Pattern:**
The mod detects buttons by their **GameObject names** which never change regardless of game language:
- `PromptButton_Primary` - Always the proceed/confirm action
- `PromptButton_Secondary` - Always the cancel/skip action

Button **text** is only used for announcements, not for routing decisions. This works with German, English, or any other language.

**Combat State Detection:**
Cards announce their current state based on active UI indicators:
- "attacking" - `IsAttacking` child is active (creature declared as attacker)
- "blocking" - `IsBlocking` child is active (creature assigned to block)
- "selected to block" - Has `CombatIcon_BlockerFrame` + `SelectedHighlightBattlefield` (clicked, not yet assigned)
- "can block" - Has `CombatIcon_BlockerFrame` only (during declare blockers phase)
- "tapped" - Has `TappedIcon` (shown for non-attackers only, since attackers are always tapped)

**Declare Attackers Phase:**
- [x] F/Space key handling - Clicks `PromptButton_Primary` (whatever text: "All Attack", "X Attackers", etc.)
- [x] Backspace key handling - Clicks `PromptButton_Secondary` (whatever text: "No Attacks", etc.)
- [x] Attacker state detection - `IsAttacking` child indicates declared attacker state

**Note:** Game requires two presses to complete attack declaration:
1. First F/Space: Selects attackers (button shows "All Attack")
2. Second F/Space: Confirms attackers (button shows "X Attackers")

**Declare Blockers Phase:**
- [x] F/Space key handling - Clicks `PromptButton_Primary` (whatever text: "X Blocker", "Next", "Confirm", etc.)
- [x] Backspace key handling - Clicks `PromptButton_Secondary` (whatever text: "No Blocks", "Cancel Blocks", etc.)
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
- `FindPromptButton(isPrimary)` - Language-agnostic button finder by GameObject name
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
- [x] **Enter key blocking - WORKING**
  - KeyboardManagerPatch blocks Enter entirely in DuelScene
  - Game never sees Enter, so "Pass until response" never triggers
  - Our navigators handle all Enter presses
- [x] **Input priority fix**
  - PortraitNavigator now runs BEFORE BattlefieldNavigator
  - Arrow keys work correctly when in player info zone
- [x] **Focus-based zone management**
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

**Files:**
- `src/Core/Services/PlayerPortraitNavigator.cs` - Main navigator with focus management
- `src/Core/Services/InputManager.cs` - Key consumption infrastructure
- `src/Patches/KeyboardManagerPatch.cs` - Harmony patch for game's KeyboardManager

### Element Grouping System (Menu Navigation)

Hierarchical navigation for menu screens. Elements are organized into groups for two-level navigation: first navigate between groups, then enter a group to navigate its elements.

**Architecture:**
- `ElementGroup.cs` - Enum defining group types (Primary, Play, Content, PlayBladeTabs, PlayBladeContent, etc.)
- `ElementGroupAssigner.cs` - Assigns elements to groups based on parent hierarchy and naming patterns
- `GroupedNavigator.cs` - Two-level navigation state machine (GroupList ↔ InsideGroup)
- `OverlayDetector.cs` - Detects which overlay is active (popup, social, PlayBlade, settings)
- `PlayBladeNavigationHelper.cs` - State machine for PlayBlade-specific navigation flow

**Navigation Flow:**
- Arrow keys navigate at current level (groups or elements)
- Enter enters a group or activates an element
- Backspace exits a group or closes an overlay

**Group Types:**
- Standard groups: Primary, Play, Progress, Navigation, Filters, Content, Settings, Secondary
- Overlay groups (suppress others): Popup, Social, PlayBladeTabs, PlayBladeContent, PlayBladeFolders, SettingsMenu, NPE, DeckBuilderCollection, Mailbox
- Single-element groups become "standalone" (directly activatable at group level)
- Folder groups for deck folders (auto-expand toggle on Enter)
- DeckBuilderCollection group for cards in deck builder's PoolHolder (card collection grid)

**PlayBlade Navigation:**
PlayBladeNavigationHelper handles all PlayBlade-specific Enter/Backspace logic:
- Derives state from `GroupedNavigator.CurrentGroup` (no separate state machine)
- `HandleEnter(element, group)` → returns `PlayBladeResult` (NotHandled/Handled/RescanNeeded/CloseBlade)
- `HandleBackspace()` → returns `PlayBladeResult`

Navigation flow:
- Tabs → Play Options (tab selected) → Deck Folders (mode activated)
- Backspace: Folders/Content → Tabs → Close blade

**Integration:**
- GeneralMenuNavigator creates GroupedNavigator and PlayBladeNavigationHelper
- DiscoverElements calls `_groupedNavigator.OrganizeIntoGroups()`
- Navigation methods (MoveNext, MovePrevious, etc.) delegate to GroupedNavigator when active
- Backspace handling: calls `_playBladeHelper.HandleBackspace()` first, acts on result
- Enter handling: calls `_playBladeHelper.HandleEnter()` before activation, triggers rescan if needed

---

### Browser Navigator System (Complete)

Refactored into 3 files following the CardDetector/DuelNavigator/ZoneNavigator pattern.

**Architecture:**
- `BrowserDetector.cs` - Static browser detection and caching (like CardDetector)
- `BrowserNavigator.cs` - Orchestrator for browser lifecycle and generic navigation (like DuelNavigator)
- `BrowserZoneNavigator.cs` - Two-zone navigation for Scry/Surveil and London mulligan (like ZoneNavigator)

**Features:**
- [x] Browser scaffold detection - Finds `BrowserScaffold_*` GameObjects (Scry, Surveil, London, etc.)
- [x] Card holder detection - `BrowserCardHolder_Default` (keep) and `BrowserCardHolder_ViewDismiss` (dismiss)
- [x] Tab navigation - Cycles through all cards in browser
- [x] Zone navigation (C/D keys) - C for top/keep zone, D for bottom zone
- [x] Card movement - Enter toggles card between zones
- [x] Card state announcements - Zone-based ("Keep on top", "Put on bottom")
- [x] Space to confirm - Clicks `PromptButton_Primary`
- [x] Backspace to cancel - Clicks `PromptButton_Secondary`
- [x] Scry/Surveil - Full keyboard support with two-zone navigation
- [x] London Mulligan - Full keyboard support for putting cards on bottom

**Browser Types Supported:**
- Scry - View top card(s), choose keep on top or put on bottom
- Surveil - Similar to scry, dismissed cards go to graveyard
- Read Ahead - Saga chapter selection
- London Mulligan - Select cards to put on bottom after mulliganing
- Opening Hand/Mulligan - View starting hand, keep or mulligan
- Generic browsers - YesNo, Dungeon, SelectCards, etc. (Tab + Enter)

**Zone Navigation:**
- **C** - Enter top/keep zone (Scry: "Keep on top", London: "Keep pile")
- **D** - Enter bottom zone (Scry: "Put on bottom", London: "Bottom of library")
- **Left/Right** - Navigate within current zone
- **Enter** - Toggle card between zones
- **Tab** - Navigate all cards (detects zone automatically for activation)

**Technical Implementation:**
- BrowserDetector caches scan results for performance (100ms interval)
- Only detects CardBrowserCardHolder from DEFAULT holder (ViewDismiss without scaffold is animation remnant)
- Zone detection from parent hierarchy (Scry) or API methods (London: `IsInHand`/`IsInLibrary`)
- Card movement via reflection: Scry uses `RemoveCard`/`AddCard`, London uses `HandleDrag`/`OnDragRelease`

## Known Issues

See [KNOWN_ISSUES.md](KNOWN_ISSUES.md) for active bugs, limitations, and planned features.

## Deployment

### File Locations
- Mod: `C:\Program Files\Wizards of the Coast\MTGA\Mods\AccessibleArena.dll`
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

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for recent changes and update history.
