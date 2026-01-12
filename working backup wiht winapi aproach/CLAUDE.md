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

## Code Standards
- Modular, maintainable, efficient code
- Avoid redundancy
- Consistent naming
- Verify changes fit existing codebase before implementing
- Use existing utilities (UIActivator, CardDetector, UITextExtractor)

## Documentation

Detailed documentation in `docs/`:
- **BEST_PRACTICES.md** - Game architecture, input system, UI patterns, utility usage, debugging
- **MOD_STRUCTURE.md** - Project layout, implementation status, deployment, next steps
- **SCREENS.md** - Special screen handling reference
- **old/** - Archived analysis files

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
- `UITextExtractor.ExtractText(element)` - Text extraction

### Safe Custom Shortcuts
Your Zones: C (Hand/Cards), B (Battlefield), G (Graveyard), X (Exile), S (Stack)
Opponent Zones: Shift+G (Graveyard), Shift+X (Exile)
Info: T (Turn), L (Life), A (Mana), Shift+A (Opponent Mana)
Card Details: Arrow Up/Down when focused on a card
Global: F1 (Help), F2 (Context), Ctrl+R (Repeat)

Do NOT override: Arrow keys, Tab, Enter, Space, Escape, F, Alt
