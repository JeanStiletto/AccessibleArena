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
        IScreenNavigator.cs      - Screen navigator interface
        ...

      Models\                    - AnnouncementPriority, Strings (localization), etc.

      Models\
        TargetInfo.cs            - Target data model and CardTargetType enum

      Services\
        # UI Utilities (static, use everywhere)
        UIActivator.cs           - Centralized UI element activation
        UITextExtractor.cs       - Text extraction (GetText, GetButtonText, CleanText)
        CardDetector.cs          - Card detection + card info extraction
        CardModelProvider.cs     - Card data from game models (deck list, collection, attachments, targeting)
        CardPoolAccessor.cs      - Reflection wrapper for CardPoolHolder (collection page API)
        RecentPlayAccessor.cs    - Reflection wrapper for LastPlayedBladeContentView (Recent tab tiles)

        # Central Services (held by main mod)
        AnnouncementService.cs   - Speech output management
        InputManager.cs          - Custom shortcut handling
        ShortcutRegistry.cs      - Keybind registration
        DebugConfig.cs           - Centralized debug logging flags (NEW Phase 2)
        DropdownStateManager.cs  - Dropdown state tracking, Enter blocking, suppression
        CardInfoNavigator.cs     - Card detail navigation (arrow up/down)
        ZoneNavigator.cs         - Zone navigation in duel (C/B/G/X/S + arrows)
        BattlefieldNavigator.cs  - Battlefield row navigation (B/A/R keys)
        DuelAnnouncer.cs         - Game event announcements via Harmony
        HotHighlightNavigator.cs - Unified Tab navigation for playable cards, targets, AND selection mode
        CombatNavigator.cs       - Combat phase navigation (declare attackers/blockers)
        PlayerPortraitNavigator.cs - Player info zone (V key, life/timer/emotes)
        HelpNavigator.cs         - F1 help menu with navigable keybind list
        ExtendedInfoNavigator.cs - I key extended card info (navigable keyword/face menu)

        # Browser Navigation (library manipulation - scry, surveil, mulligan)
        BrowserDetector.cs       - Static browser detection and caching
        BrowserNavigator.cs      - Browser orchestration and generic navigation
        BrowserZoneNavigator.cs  - Two-zone navigation (Scry/London mulligan)

        ManaColorPickerNavigator.cs - Mana color selection popup (any-color mana sources)

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
        LoadingScreenNavigator.cs    - Transitional screens (MatchEnd, PreGame/matchmaking, GameLoading)
        BoosterOpenNavigator.cs      - Pack contents after opening
        NPERewardNavigator.cs        - NPE reward screens
        RewardPopupNavigator.cs      - Rewards popup from mail claims, store purchases
        AdvancedFiltersNavigator.cs  - Advanced Filters popup in Collection/Deck Builder (grid navigation)
        DuelNavigator.cs             - Duel gameplay screen (delegates to ZoneNavigator)
        OverlayNavigator.cs          - Modal overlays (What's New, announcements)
        SettingsMenuNavigator.cs     - Settings menu (works in all scenes including duels)
        GeneralMenuNavigator.cs      - Main menu, login, NPE, and general menu screens
        StoreNavigator.cs            - Store screen (tabs, items, purchase options, details view, popups)
        MasteryNavigator.cs          - Mastery/Rewards screen (levels, tiers, XP progress, action buttons)

        # Deprecated navigators (in old/ folder)
        # WelcomeGateNavigator, LoginPanelNavigator, CodeOfConductNavigator
        # EventTriggerNavigator, DiscardNavigator, TargetNavigator, HighlightNavigator

        # UI Classification
        UIElementClassifier.cs       - Element role detection (button, progress, etc.)

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
  - Note: Legacy context system (ContextManager, GameContext, INavigableContext, Contexts/) was removed February 2026 — fully superseded by the navigator system
- [x] Scene detection (Bootstrap, AssetPrep, Login)
- [x] UI Focus tracking via EventSystem polling
- [x] F1 Help Menu - Navigable keybind list (Up/Down navigation, Backspace/F1 to close)
- [x] I Key Extended Info Menu - Navigable keyword descriptions and linked face info (Up/Down, close with I/Backspace/Escape)

### Screen Navigators
- [?] AssetPrepNavigator - Download screen on fresh install (UNTESTED, fail-safe design)
- [x] Login scene - Handled by GeneralMenuNavigator with password masking
- [x] Code of Conduct - Default navigation works correctly
- [x] LoadingScreenNavigator - Transitional screens: game loading (startup), match end (victory/defeat), PreGame (matchmaking queue)
- [x] BoosterOpenNavigator - Pack contents after opening packs
- [x] RewardPopupNavigator - Rewards popup from mail/store (cards, packs, currency)
- [x] AdvancedFiltersNavigator - Advanced Filters popup in Collection/Deck Builder (grid navigation)
- [x] DuelNavigator - Duel gameplay (zone navigation, combat, targeting)
- [~] OverlayNavigator - Modal overlays (What's New carousel) - basic implementation
- [x] SettingsMenuNavigator - Settings menu accessible in all scenes including duels
- [x] GeneralMenuNavigator - Main menu, login, NPE screens, and general navigation
- [x] NPERewardNavigator - NPE reward screens (chest, deck boxes)
- [x] StoreNavigator - Store screen with tab/item/purchase navigation, card details view, popup handling
- [x] MasteryNavigator - Mastery/Rewards screen with level navigation, reward tiers, XP progress, action buttons

### Menu Panel Detection (Harmony Patches)
- [x] PanelStatePatch - Harmony patches for panel state changes
- [x] NavContentController lifecycle - FinishOpen/FinishClose/BeginOpen/BeginClose patched
- [x] NavContentController.IsOpen setter - Backup detection
- [x] SettingsMenu.Open/Close - Patched, works correctly (Open has 7 bool params)
- [x] SettingsMenu.IsOpen setter - Backup detection
- [x] DeckSelectBlade.Show/Hide - Patched (Show takes EventContext, DeckFormat, Action)
- [x] DeckSelectBlade.IsShowing setter - Backup detection
- [x] PlayBladeController.PlayBladeVisualState setter - Detects play blade state changes
- [ ] ~~PlayBladeController.IsDeckSelected setter~~ - Removed (GET-ONLY property, no setter to patch)
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
- [x] **Deck selection** - `DeckView.OnDeckClick()` via reflection, auto-plays after selection

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
- [x] Unified extraction - `ExtractCardInfoFromObject` works with any card data object (Model, CardData, CardPrintingData)
- [x] Rules text from Model - Abilities array parsing via AbilityTextProvider lookup
- [x] Info block building - `CardDetector.BuildInfoBlocks(CardInfo)` creates navigable blocks without a GameObject

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
- [x] Phase announcements - Upkeep, draw, main phases, combat steps (declare attackers/blockers, damage, end combat)
- [x] Phase debounce (100ms) - Prevents announcement spam during auto-skip, only final phase is spoken
- [x] Combat announcements - "Combat begins", "Attacker declared", "Attacker removed"
- [x] Opponent plays - "Opponent played a card" (hand count decrease detection)
- [x] Full localization - All ~80 announcement strings use `Strings.*` properties, translated to 12 languages
- [x] Code cleanup - Debug logging removed, dead code removed, file optimized

### Unified HotHighlight Navigator System (Working)

**Replaced separate TargetNavigator + HighlightNavigator with unified HotHighlightNavigator.**

Key insight: The game's HotHighlight system correctly manages what's highlighted based on game state.
When in targeting mode, only valid targets have HotHighlight. When not targeting, only playable cards
have HotHighlight. We trust the game and scan ALL zones, letting the zone determine behavior.

- [x] HotHighlightNavigator service - Unified Tab navigation for playable cards AND targets
- [x] **Trusts game's highlight system** - No separate mode tracking needed
- [x] Scans ALL zones - Hand, Battlefield, Stack, Player portraits
- [x] Syncs with zone/battlefield navigators so Left/Right works after Tab
- [x] Zone change announcements on Tab: "Hand, Shock, 1 of 2" (same format as zone shortcuts)
  - Player: "Opponent, player, 3 of 3"
- [x] Zone-based activation:
  - Hand cards: Two-click to play
  - All other targets: Single-click to select
- [x] Player target detection - Uses `DuelScene_AvatarView.HighlightSystem._currentHighlightType` via reflection
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
Cards announce their current state using **model data first** (via `CardModelProvider`), with UI child scan as fallback:
- "attacking" - `Model.Instance.IsAttacking` property (model-based), UI fallback: `IsAttacking` child active
- "attacking, blocked by Cat" - When attacker has `Instance.BlockedByIds` populated, resolves blocker names
- "blocking Angel" - `Model.Instance.IsBlocking` property + `Instance.BlockingIds` resolved to attacker names
- "blocking" - Fallback when `BlockingIds` not yet populated
- "selected to block" - Has `CombatIcon_BlockerFrame` + `SelectedHighlightBattlefield` (UI-only, no model equivalent)
- "can block" - Has `CombatIcon_BlockerFrame` only (during declare blockers phase, UI-only)
- "can attack" - Has `CombatIcon_AttackerFrame` (during declare attackers phase, UI-only)
- "tapped" - Has `TappedIcon` (shown for non-attackers only, since attackers are always tapped)

*Model fields on `MtgCardInstance`:*
- `IsAttacking` (property, bool) - true when declared as attacker
- `IsBlocking` (property, bool) - true when assigned as blocker
- `BlockingIds` (field, `List<uint>`) - InstanceIds of attackers this blocker is blocking
- `BlockedByIds` (field, `List<uint>`) - InstanceIds of blockers blocking this attacker
- Access chain: `GetDuelSceneCDC(card)` → `GetCardModel(cdc)` → `GetModelInstance(model)` → read prop/field

**Targeting State Detection:**
Cards announce targeting relationships using model data:
- "targeting Angel" - When card has `Instance.TargetIds` populated, resolves target names
- "targeting Angel and Dragon" - Multiple targets joined with "and" (2) or ", " (3+)
- "targeted by Lightning Bolt" - When card has `Instance.TargetedByIds` populated, resolves source names
- Shown on both battlefield and stack cards

*Model fields on `MtgCardInstance` / `MtgEntity`:*
- `TargetIds` (field, `List<uint>`) - InstanceIds of what this card targets
- `TargetedByIds` (field, `List<uint>`) - InstanceIds of what is targeting this card
- `ResolveInstanceIdToNameExtended()` scans both battlefield and stack for name resolution

**Declare Attackers Phase:**
- [x] Space key handling - Clicks `PromptButton_Primary` (whatever text: "All Attack", "X Attackers", etc.)
- [x] Backspace key handling - Clicks `PromptButton_Secondary` (whatever text: "No Attacks", etc.)
- [x] Attacker state detection - `IsAttacking` child indicates declared attacker state

**Note:** Game requires two presses to complete attack declaration:
1. First Space: Selects attackers (button shows "All Attack")
2. Second Space: Confirms attackers (button shows "X Attackers")

**Declare Blockers Phase:**
- [x] Space key handling - Clicks `PromptButton_Primary` (whatever text: "X Blocker", "Next", "Confirm", etc.)
- [x] Backspace key handling - Clicks `PromptButton_Secondary` (whatever text: "No Blocks", "Cancel Blocks", etc.)
- [x] **Full blocker state announcements** - "can block", "selected to block", "blocking" (see Combat State Detection above)
- [x] **Attacker announcements during blockers** - Enemy attackers announce ", attacking" (or ", attacking, blocked by Cat")
- [x] **Blocker state detection** - Model-based via `CardModelProvider.GetIsBlockingFromCard()`, UI fallback for active `IsBlocking` child
- [x] **Blocker-attacker relationships** - Resolves `BlockingIds`/`BlockedByIds` to card names for rich announcements
- [x] **Blocker selection tracking** - Tracks selected blockers via `SelectedHighlightBattlefield` + `CombatIcon_BlockerFrame`
- [x] **Combined P/T announcements** - Announces "X/Y blocking" when selection changes
- [x] **Blocker assignment announcements** - "Cat blocking Angel" (or "Cat assigned" if BlockingIds not yet populated)

**Blocker Selection System:**
- `IsCreatureSelectedAsBlocker(card)` - Checks for both `CombatIcon_BlockerFrame` AND `SelectedHighlightBattlefield` active (UI-only, no model equivalent)
- `FindSelectedBlockers()` - Returns all CDC cards currently selected as blockers
- `GetPowerToughness(card)` - Extracts P/T from card using `CardDetector.ExtractCardInfo()`
- `CalculateCombinedStats(blockers)` - Sums power and toughness across all selected blockers
- `UpdateBlockerSelection()` - Called each frame, detects selection changes, announces combined stats and blocker-attacker pairings
- `GetBlockingText(card)` / `GetBlockedByText(card)` - Resolve combat relationships to card names via `CardModelProvider`
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

### Mana Color Picker Navigator (Complete)
- [x] ManaColorPickerNavigator service - Detects ManaColorSelector popup via reflection
- [x] Integration with DuelNavigator - Highest priority (before BrowserNavigator)
- [x] Tab/Shift+Tab and Left/Right navigation through available colors
- [x] Enter selects focused color, number keys 1-6 for direct selection
- [x] Multi-pick support - Sequential picks with re-announcement after each
- [x] Backspace cancels via TryCloseSelector
- [x] Localized in all 12 languages

**Detection:**
- Polls for `ManaColorSelector` instances via `FindObjectsOfType` (100ms interval)
- Checks `IsOpen` property on found instances
- Reads `_selectionProvider` (protected field) for available colors and selection state

**Reflection Access:**
- `ManaColorSelector.IsOpen` (public property) - detection
- `ManaColorSelector._selectionProvider` (protected field) - color data
- `ManaColorSelector.SelectColor(ManaColor)` (protected method) - selection
- `ManaColorSelector.TryCloseSelector()` (public method) - cancel
- `IManaSelectorProvider.ValidSelectionCount`, `GetElementAt`, `MaxSelections`, `AllSelectionsComplete`, `CurrentSelection`
- `ManaProducedData.PrimaryColor` (field or property) - color enum value

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
- `HandleQueueTypeEntry(element)` → handles queue type subgroup entry (virtual or real tab activation)
- `HandleBackspace()` → returns `PlayBladeResult`

**Queue Type Tab Structure (February 2026):**
The original "Find Match" tab was replaced by three queue type tabs at the same level as Events and Recent:
- **PlayBladeTabs:** [Events, Ranked, Open Play, Brawl, Recent]
- FindMatch nav tab is excluded from navigation (hidden via `ElementGroupAssigner`)
- Queue type tabs (`Blade_Tab_Ranked`, `Blade_Tab_Deluxe`) are promoted to PlayBladeTabs group

**Why this restructure:**
The original Find Match tab opened a mixed view containing queue type tabs, queue items, and a BO3 toggle. This was confusing for screen reader users who had to navigate through nested tabs. By promoting queue types to the top level, users get a flatter, more predictable navigation: pick a queue type → see its queue items → select and play.

**Virtual vs Real Entries:**
Queue type tabs only exist as GameObjects when the FindMatch blade view is active. When Events/Recent is showing, virtual subgroup entries (`GameObject = null`) are injected into PlayBladeTabs by `GroupedNavigator.PostProcessPlayBladeTabs()`. This uses the same subgroup pattern as Objectives within Progress.

**Two-Step Activation:**
When entering a virtual queue type entry (FindMatch not active):
1. Helper stores pending queue type, clicks FindMatch game tab → blade switch → rescan
2. Post-organize finds real queue type tab, clicks it → `NeedsFollowUpRescan` → rescan
3. Auto-enters PlayBladeContent with queue items

When entering a real queue type entry (FindMatch already active):
1. Helper clicks tab directly → rescan → auto-enters PlayBladeContent

**Position Restore:**
`_lastQueueTypeTabIndex` preserves cursor position when navigating back from content to tabs via Backspace. User returns to the queue type entry they came from.

**BO3 Toggle Fix:**
Game uses placeholder text "POSITION" for the Best of 3 toggle label. Fixed at two levels: `UITextExtractor.GetToggleText()` replaces "POSITION" with localized "Best of 3", and `UIElementClassifier.TryClassifyAsToggle()` provides a safety net.

**Element Exclusions:**
- FindMatch nav tab → `ElementGroup.Unknown` (replaced by queue type entries)
- MainButton (Play/Spielen) inside blade → `ElementGroup.Unknown` (global button, not blade content)
- NewDeck/CreateDeck inside blade → `ElementGroup.Unknown` (not relevant in FindMatch context)
- Elements with `ElementGroup.Unknown` are skipped entirely in `OrganizeIntoGroups()`

Navigation flow:
- Tabs → Queue Type Entry → Content (queue items) → Deck Folders → Folder (decks)
- Backspace: Folders/Content → Tabs (landing on last queue type) → Close blade

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
- ViewDismiss - Card preview popup (auto-dismissed, see below)
- Generic browsers - YesNo, Dungeon, SelectCards, etc. (Tab + Enter)

**ViewDismiss Auto-Dismiss:**
- The game opens a `BrowserScaffold_ViewDismiss` popup when clicking on graveyard/exile cards
- This popup traps Unity focus on `BrowserCardHolder_ViewDismiss`, blocking keyboard navigation
- BrowserNavigator detects the ViewDismiss scaffold but does NOT enter browser mode
- Instead, it immediately clicks the `DismissButton` via `UIActivator.Activate()` to close the popup
- A `_viewDismissDismissed` flag prevents repeated clicks while the scaffold is still closing
- The flag resets when no browser is detected (scaffold gone)

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

### Store Navigator (February 2026)

Dedicated navigator for the Store screen. Uses a three-level navigation model (Tabs, Items, Purchase Options) with popup handling and a card details view.

**Architecture:**
- `StoreNavigator.cs` - Standalone navigator with custom store-specific navigation
- Priority 85 (above GeneralMenuNavigator at 15)
- Subscribes to `PanelStateManager.OnPanelChanged` for popup detection

**Navigation Model:**
- **Tab level (Up/Down)**: Navigate store tabs (Featured, Packs, Decks, etc.)
- **Item level (Up/Down)**: Navigate items within current tab
- **Purchase options (Left/Right)**: Cycle between Details, Gems, Gold purchase options
- **Enter/Space**: Activate current purchase option or open details view
- **Backspace**: Go back one level (Items → Tabs → Exit store)

**Details View (Card List):**
Store items that contain decks or bundles offer a "Details" option as the first purchase option. When activated:
- Extracts card list from `StoreDisplayPreconDeck.CardData` or `StoreDisplayCardViewBundle.BundleCardViews`
- Left/Right navigates between cards (announced as "CardName, times Qty, ManaCost, X of N")
- Up/Down navigates card info blocks (same as all other card contexts via `CardDetector.BuildInfoBlocks`)
- Card info extracted via `CardModelProvider.ExtractCardInfoFromObject(cardDataObj)` using the stored CardData object
- Backspace closes details view, returns to item navigation

**Popup Handling:**
StoreNavigator uses a reusable popup pattern with dual detection:
- **PanelStateManager events**: Subscribes to `OnPanelChanged` to detect system popups (SystemMessageView, Dialog, etc.)
- **Confirmation modal polling**: Polls `_confirmationModalField` on the store controller to detect the store's own confirmation modal
- When popup is active, navigation switches to popup elements (Up/Down navigate, Enter activates, Backspace dismisses)
- Dismissal tries: modal `Close()` method, cancel button pattern matching, `SystemMessageView.OnBack()`

**Key Implementation Details:**
- Uses reflection to access `ContentController_StoreCarousel` internals (tabs, items, confirmation modal)
- Item descriptions extracted from active tag text and tooltip LocString
- Purchase options extracted from `PurchaseButton` structs on `StoreItemBase` components
- Details view stores raw `CardData` objects in `DetailCardEntry.CardDataObj` for direct extraction (avoids dependency on `_cachedDeckHolder` which doesn't exist in Store scene)

**Files:**
- `src/Core/Services/StoreNavigator.cs` - Main navigator implementation

---

### Mastery Navigator (February 2026)

Dedicated navigator for the Mastery/Rewards screen. Replaces GeneralMenuNavigator's flat element list with structured level-based navigation.

**Architecture:**
- `MasteryNavigator.cs` - Standalone navigator with custom level/tier navigation
- Priority 60 (above StoreNavigator at 55, below LoadingScreenNavigator at 65)
- Subscribes to `PanelStateManager.OnPanelChanged` for popup detection

**Navigation Model:**
- **Virtual status item (index 0)**: XP progress info + action buttons as tiers
  - Left/Right cycles: Status info, Mastery Tree, Previous Season, Purchase, Back
  - Enter activates the selected button tier
- **Levels (Up/Down)**: Navigate mastery levels with reward announcements
- **Tiers (Left/Right)**: Cycle between Free, Premium, Renewal reward tiers within a level
- **Enter**: Announce detailed level info (all tiers, quantities, completion status)
- **PageUp/PageDown**: Jump ~10 levels with page sync
- **Home/End**: Jump to status item / last level
- **Backspace**: Return to Home screen

**Data Access:**
- `ProgressionTracksContentController` detection via MonoBehaviour scan + `IsOpen` check
- `RewardTrackView._levels` and `_levelRewardData` for level/reward data
- `SetMasteryDataProvider.GetCurrentLevelIndex()` for completion status
- `MTGALocalizedString` key resolution via `Languages.ActiveLocProvider`

**Files:**
- `src/Core/Services/MasteryNavigator.cs` - Main navigator implementation

---

### Advanced Filters Navigator (February 2026)

Dedicated navigator for the Advanced Filters popup in Collection/Deck Builder screens. Uses grid-based navigation instead of flat list navigation for better accessibility.

**Architecture:**
- `AdvancedFiltersNavigator.cs` - Standalone navigator with grid-based row/column navigation
- Priority 87 (above RewardPopupNavigator at 86, below SettingsMenuNavigator at 90)

**Navigation Model:**
- **Up/Down (W/S)**: Switch between rows (Types, Rarity, Actions)
- **Left/Right (A/D)**: Navigate within current row
- **Home/End**: Jump to first/last item in row
- **Enter/Space**: Toggle checkbox or activate button/dropdown
- **Backspace**: Dismiss popup without applying filters

**Discovered Rows:**
1. **Types row** - Card type filters (Creature, Planeswalker, Instant, etc.)
2. **Rarity row** - Rarity filters (Common, Uncommon, Rare, Mythic Rare, Basic Lands)
3. **Actions row** - Collection filters, Format dropdown, OK button

**Key Implementation Details:**
- Stores Toggle component references during discovery for consistent state reading
- Uses `DropdownStateManager` for dropdown mode detection and blocking navigation while dropdown is open
- Enter blocked from game in dropdown mode; items selected silently via reflection (dropdown stays open)
- Calls `DropdownStateManager.UpdateAndCheckExitTransition()` each frame to track dropdown state
- Uses `UIActivator.Activate()` for proper game event triggering (not direct toggle.isOn manipulation)
- Consumes Enter/Space/Backspace keys to prevent GeneralMenuNavigator from processing them after popup closes
- Tries to click background blocker on Backspace to dismiss without applying filters

**Game Behavior Notes:**
- Game enforces "at least one type must be selected" - toggling the last selected type will be reset by game
- Creature and Planeswalker toggles may appear linked due to game's filter logic
- Closing with OK button applies filters and may trigger game's deck validation dialog

**Files:**
- `src/Core/Services/AdvancedFiltersNavigator.cs` - Main navigator implementation

---

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
