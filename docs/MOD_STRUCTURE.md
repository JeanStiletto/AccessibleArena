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

      Models\                    - GameContext, AnnouncementPriority, etc.

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

        # Navigator Infrastructure
        BaseNavigator.cs         - Abstract base for screen navigators
        NavigatorManager.cs      - Manages navigator lifecycle and priority

        # Screen Navigators (all extend BaseNavigator)
        UIFocusTracker.cs            - EventSystem focus polling (fallback)
        WelcomeGateNavigator.cs      - Welcome/login choice screen
        LoginPanelNavigator.cs       - Email/password login screen
        CodeOfConductNavigator.cs    - Terms/consent checkboxes screen
        PreBattleNavigator.cs        - Pre-battle VS screen (DuelScene)
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
- [x] PreBattleNavigator - Pre-battle VS screen
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

### UI Utilities
- [x] UIElementClassifier - Element role detection and filtering
  - Detects: Button, Link, Toggle, Slider, ProgressBar, Navigation, Internal
  - Filters internal elements (blockers, tooltips, gradients)

### Card System
- [x] CardDetector - Universal detection with caching
- [x] CardInfoNavigator - Arrow up/down through card details
- [x] Automatic card navigation - No Enter required, just Tab to card and use arrows
- [x] Lazy loading - Card info only extracted on first arrow press (performance)
- [x] Mana cost parsing (sprite tags to readable text)

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
- [x] Stack announcements - "Cast [card name]" when spell goes on stack (delayed detection)
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

### Highlight Navigator System (Working)
- [x] HighlightNavigator service - Tab cycles playable cards outside targeting mode
- [x] HotHighlight detection - Finds cards with active highlight (playable indicator)
- [x] Integration with DuelNavigator - Priority: TargetNavigator > HighlightNavigator > ZoneNavigator
- [x] Tab/Shift+Tab cycling - Cycles through all playable cards (hand + battlefield)
- [x] Enter to play - Activates currently highlighted card
- [x] Replaces default Tab behavior - No more cycling through useless UI buttons

### Discard Navigator System (Working)
- [x] DiscardNavigator service - Handles forced discard selection
- [x] Submit button detection - Detects "Submit X" button to identify discard mode
- [x] Required count parsing - Parses "Discard a card" or "Discard X cards" from prompt
- [x] Entry announcement - Announces "Discard X card(s)" when mode detected
- [x] Selection state in announcements - Cards announce "selected for discard" or "not selected"
- [x] Enter to toggle - Clicks card to select/deselect for discard
- [x] Selection count announcement - Announces "X cards selected" after toggle (0.2s delay)
- [x] Space to submit - Validates count matches required, submits or announces error
- [x] Integration with DuelNavigator - Priority: TargetNavigator > DiscardNavigator > HighlightNavigator
- [x] Integration with ZoneNavigator - Adds selection state to card announcements

### Combat Navigator System (Work in Progress)
- [x] CombatNavigator service - Handles declare attackers/blockers phases
- [x] Phase tracking in DuelAnnouncer - `IsInDeclareAttackersPhase`, `IsInDeclareBlockersPhase` properties
- [x] Integration with DuelNavigator - Priority: TargetNavigator > DiscardNavigator > CombatNavigator > HighlightNavigator
- [x] Integration with ZoneNavigator - `GetCombatStateText()` adds combat state to card announcements

**Declare Attackers Phase:**
- [x] Space key handling - Clicks "All Attack" or "X Attack" button
- [x] Button detection - Finds `PromptButton_Primary` with text containing "Attack"
- [x] Opponent turn filtering - Ignores "Opponent's Turn" button text
- [x] Blocker state announcements - Cards announce ", blocking" or ", not blocking" (working)
- [ ] **Attacker state detection (WIP)** - Need to distinguish "can attack" vs "declared as attacker"
  - `CombatIcon_AttackerFrame(Clone)` appears on creatures that CAN attack
  - `IsAttacking` child may indicate actual declared state (needs verification)
  - Current detection shows all attackable creatures as "attacking"

**Declare Blockers Phase:**
- [x] Space key handling - Attempts to click confirm/done button
- [x] Button detection - Looks for "Done", "Confirm", "Block", "OK" in button text
- [x] Blocker state announcements - Cards announce ", blocking" or ", not blocking"
- [ ] **Blocker state detection (WIP)** - Looking for `CombatIcon_BlockerFrame` or similar indicator

**Debug Logging (Temporary):**
- Attacker debug: Logs relevant children (Combat, Attack, Select, Declare, Lob, Tap, Is)
- Blocker debug: Logs all card children during declare blockers phase
- Button logging: Logs available buttons when Space pressed in blockers phase

## Known Issues

### Combat Navigator
- **Attacker detection incorrect**: `CombatIcon_AttackerFrame` appears on all creatures that CAN attack, not just those declared as attackers. Need to find the indicator that distinguishes "selected/declared as attacker" from "can attack but not selected".
- **Blocker detection unverified**: Looking for `CombatIcon_BlockerFrame` but not yet confirmed from logs.

### DiscardNavigator
- **Log flooding fixed**: Removed per-frame logging from `GetSubmitButtonInfo()` that was flooding logs when "Submit X" pattern matched.

### Card Playing
- **Rapid Enter presses**: Multiple rapid Enter presses can trigger card play sequence multiple times, potentially causing issues if game enters targeting mode.

### Debug Logging
- CombatNavigator has temporary debug logging enabled for attacker/blocker detection development. Should be removed once detection patterns are finalized.

## Next Steps

### Immediate - Combat System
1. **Fix attacker state detection** - Find correct indicator for "declared as attacker" (possibly `IsAttacking` child being active, or different indicator)
2. **Verify blocker detection** - Check logs during declare blockers to find blocker indicator
3. Remove debug logging once detection is finalized

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
