# MTGA Accessibility Mod

## Purpose
Accessibility mod for Magic: The Gathering Arena enabling blind players to play using NVDA screen reader.

## Accessibility Goals
- Well-structured text output (no tables, no graphics)
- Linear, readable format for screen readers
- Tolk library for NVDA communication
- Full keyboard navigation support

## Communication
- Output plain text optimized for screen readers
- Announce context changes, focused elements, and game state
- Provide card information in navigable blocks (arrow up/down)

## Claude Response Formatting
- Never use markdown tables (| symbols are read aloud by screen readers)
- Use headings and bullet lists for comparisons
- Present information linearly, one item per line
- Group related info under clear labels

Example - instead of tables, format like this:
**Item Name**
- Property: Value
- Property: Value

## Code Standards
- Modular, maintainable, efficient code
- Avoid redundancy
- Consistent naming
- Verify changes fit existing codebase before implementing
- Use existing utilities (UIActivator, CardDetector, UITextExtractor)

## Documentation

Detailed documentation in `docs/`:
- **GAME_ARCHITECTURE.md** - Game internals, assemblies, zones, interfaces, modding tools
- **MOD_STRUCTURE.md** - Project layout, implementation status
- **BEST_PRACTICES.md** - Coding patterns, utilities, input handling
- **SCREENS.md** - Navigator quick reference
- **CHANGELOG.md** - Recent changes
- **KNOWN_ISSUES.md** - Bugs, limitations, planned features
- **old/** - Archived planning documents

**IGNORE:** `arena accessibility backlog.txt` - outdated

## Quick Reference

### Game Location
`C:\Program Files\Wizards of the Coast\MTGA`

### Build & Deploy
```powershell
# Build
dotnet build "C:\Users\fabia\arena\src\MTGAAccessibility.csproj"

# Deploy (game must be closed)
Copy-Item -Path 'C:\Users\fabia\arena\src\bin\Debug\net472\MTGAAccessibility.dll' -Destination 'C:\Program Files\Wizards of the Coast\MTGA\Mods\MTGAAccessibility.dll' -Force
```

### MelonLoader Logs
- Latest: `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`
- All logs: `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Logs\`
- Read last 300 lines: `Get-Content '...\Latest.log' -Tail 300`

### Deployment Paths
- Mod DLL: `C:\Program Files\Wizards of the Coast\MTGA\Mods\MTGAAccessibility.dll`
- Tolk DLLs in game root

### Key Utilities (always use these)
- `UIActivator.Activate(element)` - Element activation
- `CardDetector.IsCard(element)` - Card detection
- `UITextExtractor.GetText(element)` - Text extraction

### Safe Custom Shortcuts
Your Zones: C (Hand/Cards), G (Graveyard), X (Exile), S (Stack)
Opponent Zones: Shift+G (Graveyard), Shift+X (Exile)
Battlefield (Your side): B (Creatures), A (Lands), R (Non-creatures)
Battlefield (Enemy side): Shift+B (Creatures), Shift+A (Lands), Shift+R (Non-creatures)
Battlefield Navigation: Shift+Up (Previous row), Shift+Down (Next row), Left/Right (Within row)
Info: T (Turn), L (Life), V (Player Info Zone)
Player Info Zone: Left/Right (Switch player), Up/Down (Cycle properties), Enter (Emotes), Backspace (Exit)
Card Details: Arrow Up/Down when focused on a card
Carousel: Arrow Left/Right when focused on a carousel element (e.g., promotional banners)
Combat (Declare Attackers): F (All Attack / X Attack), Backspace (No Attacks), Space (All Attack / X Attack)
Combat (Declare Blockers): F (Confirm Blocks / Next), Backspace (No Blocks / Cancel Blocks), Space (Confirm Blocks / Next)
Main Phase: Space (Next / To Combat / Pass - clicks primary button)
Targeting: Tab (Cycle targets), Enter (Select), Backspace (Cancel)
Browser (Scry/Surveil/etc.): Tab/Arrows (Navigate), Enter (Activate), Space (Confirm), Backspace (Cancel)
Global: F1 (Help), F2 (Context), Ctrl+R (Repeat), Backspace (Back/Dismiss/Cancel - universal)

Do NOT override: Tab, Enter, Escape
Note: Shift+Up/Down used for battlefield row switching
Note: Left/Right arrows used contextually (cards, carousels, battlefield rows)
Note: F key used contextually during combat phases (attackers/blockers)
Note: Space used contextually during duels (main phase pass, combat confirmations)
Note: Backspace is the universal back/dismiss/cancel key - in menus goes back one level (Settings submenu → Settings → Home, PlayBlade → Home), also handles combat no attacks/blocks, browser cancel, targeting cancel, exit zones
