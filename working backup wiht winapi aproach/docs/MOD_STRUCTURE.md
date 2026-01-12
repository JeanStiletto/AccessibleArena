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
        UITextExtractor.cs       - Text extraction + element type detection
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

    Contexts\
      Base\                      - BaseNavigableContext, BaseMenuContext
      Login\                     - LoginContext (login flow panels)
      MainMenu\                  - MainMenuContext

    Patches\
      UXEventQueuePatch.cs       - Harmony patch for game event interception (NEW)

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
- [x] Zone change tracking - Tracks card counts per zone to detect changes
- [x] Spell cast tracking - `_lastSpellCastTime` set on stack increase (for targeting detection)
- [x] Spell resolve tracking - `_lastSpellResolvedTime` set on stack decrease
- [ ] Phase announcements - Partially implemented, needs field name fixes
- [ ] Combat announcements - Basic structure, needs testing
- [ ] Opponent plays - "Opponent played a card" (hand count decrease detection)
- [ ] Code cleanup - Remove debug logging, optimize event filtering

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

### Working Features
- Tab/Shift+Tab navigation on all login flow screens
- Enter/Space to activate buttons, toggle checkboxes, focus inputs
- Field content announcements
- Dynamic state announcements for checkboxes
- NPE reward chest opening and deck box display
- Card reveal with full info navigation
- Automatic card detail navigation (Arrow Up/Down on any focused card)
- Duel zone navigation (C/B/G/X/S shortcuts)
- Card navigation within zones (Left/Right arrows)
- Zone card counts announced on zone entry
- **Card playing from hand** (Enter key, announces "Played {cardName}")
- **Target selection** (Tab cycles valid targets, Enter selects, Escape cancels)
- **Playable card cycling** (Tab cycles highlighted/playable cards, Enter plays)
- **Turn announcements** (via Harmony) - Announces turn number and whose turn
- **Draw announcements** (via Harmony) - Announces when you or opponent draws
- **Spell resolution** (via Harmony) - Announces when spells resolve from stack

## Next Steps

### Immediate - DuelAnnouncer Cleanup
1. Remove debug logging from DuelAnnouncer (NEW EVENT TYPE logs, property dumps)
2. Fix phase change announcements (find correct field names)
3. Test and fix opponent action announcements
4. Optimize event filtering to reduce unnecessary processing

### Immediate - Gameplay
1. Test attacking with creatures (tap/attack actions)
2. Add life total announcements (L shortcut)
3. Add mana pool info (A shortcut already mapped)

### Upcoming
1. Combat damage announcements
2. Creature death announcements with card names
3. Proper target selection with Tab navigation

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
- `ZoneTransferGroup` - Card movements (field: `_reasonZonePairs`)
- `UXEventUpdatePhase` - Phase changes (needs field investigation)

Privacy protection: Never reveals opponent's hidden information (hand contents, library).
