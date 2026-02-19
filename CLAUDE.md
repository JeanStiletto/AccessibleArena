# Accessible Arena

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

## Permissions
- NEVER add broad PowerShell wildcard permissions like `Bash(powershell -Command:*)` or `Bash(powershell:*)` to settings.local.json - always use specific, scoped commands only

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
```bash
# Build
dotnet build "C:\Users\fabia\arena\src\AccessibleArena.csproj"

# Deploy (game must be closed)
powershell -Command "Copy-Item -Path 'C:\Users\fabia\arena\src\bin\Debug\net472\AccessibleArena.dll' -Destination 'C:\Program Files\Wizards of the Coast\MTGA\Mods\AccessibleArena.dll' -Force"
```

### MelonLoader Logs
- Latest: `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`
- All logs: `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Logs\`
- Use the Read tool to read log files (permission pre-configured in settings.local.json)

### Deployment Paths
- Mod DLL: `C:\Program Files\Wizards of the Coast\MTGA\Mods\AccessibleArena.dll`
- Tolk DLLs in game root

### Key Utilities (always use these)
- `UIActivator.Activate(element)` - Element activation
- `CardDetector.IsCard(element)` - Card detection
- `UITextExtractor.GetText(element)` - Text extraction

### Browser Debug Tools
Enable detailed debug logging for investigating browser activation issues:
```csharp
// Enable debug for a specific browser type
BrowserDetector.EnableDebugForBrowser(BrowserDetector.BrowserTypeWorkflow);
BrowserDetector.EnableDebugForBrowser(BrowserDetector.BrowserTypeScry);

// Disable when done
BrowserDetector.DisableDebugForBrowser(BrowserDetector.BrowserTypeWorkflow);
BrowserDetector.DisableAllDebug();
```
When enabled, dumps comprehensive info: UI structure, clickable components, workflow state, siblings.

### Safe Custom Shortcuts

**Menu Navigation:**
Arrow Up/Down (or W/S): Navigate menu items
Tab/Shift+Tab: Navigate menu items (same as Arrow Up/Down)
Arrow Left/Right (or A/D): Carousel/stepper controls
Home: Jump to first item
End: Jump to last item
Page Up/Page Down: Previous/next page in collection
Enter/Space: Activate
Backspace: Back one level

**Input Fields (Login, Search, etc.):**
Enter: Start editing text field
Escape: Stop editing, return to navigation
Tab: Stop editing and move to next element
Shift+Tab: Stop editing and move to previous element
Arrow Left/Right (while editing): Read character at cursor
Arrow Up/Down (while editing): Read full content

**Duel - Zone Navigation:**
Your Zones: C (Hand/Cards), G (Graveyard), X (Exile), S (Stack)
Opponent Zones: Shift+G (Graveyard), Shift+X (Exile)
Within Zone: Left/Right (Navigate cards), Home/End (Jump to first/last)
Card Details: Arrow Up/Down when focused on a card

**Duel - Battlefield:**
Your side: B (Creatures), A (Lands), R (Non-creatures)
Enemy side: Shift+B (Creatures), Shift+A (Lands), Shift+R (Non-creatures)
Row Switching: Shift+Up (Previous row), Shift+Down (Next row)
Within Row: Left/Right (Navigate cards), Home/End (Jump to first/last)

**Duel - Info:**
T (Turn), L (Life), V (Player Info Zone), I (Extended Card Info: keyword descriptions + other faces)
D (Your Library Count), Shift+D (Opponent Library Count), Shift+C (Opponent Hand Count)
Player Info Zone: Left/Right (Switch player), Up/Down (Cycle properties), Enter (Emotes), Backspace (Exit)

**Duel - Combat:**
Declare Attackers: Space (All Attack / X Attack), Backspace (No Attacks)
Declare Blockers: Space (Confirm Blocks / Next), Backspace (No Blocks / Cancel Blocks)
Main Phase: Space (Next / To Combat / Pass - clicks primary button)

**Duel - Targeting:**
Tab (Cycle targets), Ctrl+Tab (Cycle opponent targets only), Enter (Select), Backspace (Cancel)

**Duel - Browser (Scry/Surveil/etc.):**
Tab (Navigate all cards), C/D (Jump to top/bottom zone)
Within Zone: Left/Right (Navigate), Home/End (Jump to first/last)
Enter (Toggle card), Space (Confirm), Backspace (Cancel)

**Duel - London Mulligan:**
C (Keep pile), D (Bottom pile)
Left/Right (Navigate), Home/End (Jump to first/last)
Enter (Toggle card), Space (Submit)

**Duel - Mana Color Picker (any-color mana sources):**
Tab/Right (Next color), Shift+Tab/Left (Previous color)
Home/End (Jump to first/last)
Enter (Select color), 1-6 (Direct select by number)
Backspace (Cancel)

**Global:**
F1 (Help Menu - navigable with Up/Down, close with Escape, Backspace, or F1)
F2 (Settings Menu - navigable with Up/Down, Enter to change, close with Escape, Backspace, or F2)
F3 (Current screen), Ctrl+R (Repeat)
Backspace (Back/Dismiss/Cancel - universal)

Do NOT override: Enter, Escape
Note: Tab works for both menu navigation and duel highlights
Note: Arrow keys used differently in menus (navigation) vs duels (zone/card navigation)
Note: Space used contextually during duels (main phase pass, combat confirmations)
Note: Backspace is the universal back/dismiss/cancel key
