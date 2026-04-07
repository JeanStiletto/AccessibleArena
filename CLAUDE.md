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
- When fixing UI interaction bugs (e.g., keyboard event handling, dropdowns), always test edge cases where the fix might interfere with normal component behavior

## Game & Framework

- **Game:** Magic: The Gathering Arena (Unity, .NET 4.7.2)
- **Mod loader:** MelonLoader (entry point: `AccessibleArenaMod : MelonMod`)
- **Patching:** Harmony 2.x for IL interception (4 patch classes in `src/Patches/`)
- **Screen reader:** Tolk library (P/Invoke to native DLL, supports NVDA/JAWS/Narrator)
- **Game assemblies:** Located at `<game>/MTGA_Data/Managed/` — Core.dll has most types, Assembly-CSharp.dll has some UI types

## Documentation

Detailed documentation in `docs/`:
- **GAME_ARCHITECTURE.md** - Game internals, assemblies, zones, interfaces, modding tools
- **MOD_STRUCTURE.md** - Project layout, implementation status
- **BEST_PRACTICES.md** - Coding patterns, utilities, input handling
- **SCREENS.md** - Navigator quick reference
- **CHANGELOG.md** - Recent changes
- **KNOWN_ISSUES.md** - Bugs, limitations, planned features
- **old/** - Archived planning documents

LLM reference documentation in `llm-docs/`:
- **architecture-overview.md** - High-level mod architecture, entry point flow, system interactions
- **source-inventory.md** - Complete source file inventory with line counts
- **framework-reference.md** - MelonLoader/Harmony/Unity dependency details and patch inventory
- **type-index.md** - Game type → full namespace + DLL mapping (check this FIRST when investigating game types)
- **decompiled/** - Pre-decompiled game type sources (gitignored). Read these before running ilspycmd.

**Investigating game types:** Always check `llm-docs/decompiled/` and `llm-docs/type-index.md` first. To decompile a new type: `powershell -NoProfile -File tools\decompile.ps1 "TypeName"`

**IGNORE:** `arena accessibility backlog.txt` - outdated

## Quick Reference

### Game Location
- WotC (default): `C:\Program Files\Wizards of the Coast\MTGA`
- Steam: `C:\Program Files (x86)\Steam\steamapps\common\MTGA`

If you use the Steam version, create `src/local.props` (gitignored) to override the default:
```xml
<Project>
  <PropertyGroup>
    <MtgaPath>C:\Program Files (x86)\Steam\steamapps\common\MTGA</MtgaPath>
  </PropertyGroup>
</Project>
```

### Build & Deploy
```bash
# Build and auto-deploy to Mods folder (game must be closed)
dotnet build src/AccessibleArena.csproj
```
The build auto-copies the DLL to `$(MtgaPath)\Mods\`. Default is the WotC path; override with `src/local.props` (see above).

### Release
```bash
# Create a release (builds, tags, publishes to GitHub)
powershell -NoProfile -File installer/release.ps1
```
Before running: update `ModVersion` in `src/Directory.Build.props` and add a `## vX.Y` section to `docs/CHANGELOG.md`, then commit.

### MelonLoader Logs
- Latest: `<MtgaPath>\MelonLoader\Latest.log`
- All logs: `<MtgaPath>\MelonLoader\Logs\`
- Use the Read tool to read files in the MTGA folder (permission pre-configured in settings.local.json)

### Deployment Paths
- Mod DLL: `<MtgaPath>\Mods\AccessibleArena.dll`
- Tolk DLLs in game root

### Key Utilities (always use these)
- `UIActivator.Activate(element)` - Element activation
- `CardDetector.IsCard(element)` - Card detection
- `UITextExtractor.GetText(element)` - Text extraction
- `CardModelProvider` - Card data extraction, component access, name lookup, mana parsing
- `CardTextProvider` - Ability text, flavor text, localized text lookups
- `CardStateProvider` - Attachments, combat state, targeting, counters, categorization

### Browser Debug Tools
Enable detailed debug logging for investigating browser activation issues:
```csharp
// Enable debug for a specific browser type
BrowserDetector.EnableDebugForBrowser(BrowserDetector.BrowserTypeWorkflow);
BrowserDetector.EnableDebugForBrowser("Scry");

// Disable when done
BrowserDetector.DisableDebugForBrowser(BrowserDetector.BrowserTypeWorkflow);
BrowserDetector.DisableAllDebug();
```
When enabled, dumps comprehensive info: UI structure, clickable components, workflow state, siblings.

### Safe Custom Shortcuts

**Menu Navigation:**
Arrow Up/Down: Navigate menu items
Tab/Shift+Tab: Navigate menu items (same as Arrow Up/Down)
Arrow Left/Right: Carousel/stepper controls
Home: Jump to first item
End: Jump to last item
Page Up/Page Down: Previous/next page in collection
A-Z: Jump to item starting with letter (buffered: type "ST" for "Store", repeat same letter to cycle)
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
Your Zones: C (Hand/Cards), G (Graveyard), X (Exile), S (Stack), W (Command Zone)
Opponent Zones: Shift+G (Graveyard), Shift+X (Exile), Shift+W (Command Zone)
Within Zone: Left/Right (Navigate cards), Home/End (Jump to first/last)
Card Details: Arrow Up/Down when focused on a card

**Duel - Battlefield:**
Your side: B (Creatures), A (Lands), R (Non-creatures)
Enemy side: Shift+B (Creatures), Shift+A (Lands), Shift+R (Non-creatures)
Row Switching: Shift+Up (Previous row), Shift+Down (Next row)
Within Row: Left/Right (Navigate cards), Home/End (Jump to first/last)

**Duel - Info:**
T (Turn), L (Life), V (Player Info Zone), I (Extended Card Info: keyword descriptions + other faces)
K (Counter info on focused card)
M (Your Land Summary), Shift+M (Opponent Land Summary)
D (Your Library), Shift+D (Opponent Library), Shift+C (Opponent Hand Count)
Player Info Zone: Left/Right (Switch player), Up/Down (Cycle properties), Enter (Emotes), Backspace (Exit)

**Duel - Full Control & Phase Stops:**
P: Toggle full control (temporary, resets on phase change)
Shift+P: Toggle locked full control (permanent)
Shift+Backspace: Toggle pass until opponent action (soft skip)
Ctrl+Backspace: Toggle skip turn (force skip entire turn)
1-0: Toggle phase stops (1=Upkeep, 2=Draw, 3=First Main, 4=Begin Combat, 5=Declare Attackers, 6=Declare Blockers, 7=Combat Damage, 8=End Combat, 9=Second Main, 0=End Step)
Note: Ctrl is blocked from reaching the game in duels (prevents accidental full control toggle when silencing NVDA)

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
