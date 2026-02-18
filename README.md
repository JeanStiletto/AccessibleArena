# Accessible Arena

Accessibility mod for Magic: The Gathering Arena that enables blind and visually impaired players to play using the NVDA screen reader. Full keyboard navigation, screen reader announcements for all game states, and 12-language localization.

**Status:** Public beta. Core gameplay is functional. Some edge cases and minor bugs remain. See Known Issues below.

## Features

- Full keyboard navigation for all screens (home, store, mastery, deck builder, duels)
- NVDA screen reader integration via Tolk library
- Card information reading with arrow keys (name, mana cost, type, power/toughness, rules text, flavor text, rarity, artist)
- Complete duel support: zone navigation, combat, targeting, stack, browsers (scry, surveil, mulligan)
- Attachment and combat relationship announcements (enchanted by, blocking, targeted by)
- Accessible store with purchase options and payment dialog support
- Bot match support for practice games
- Settings menu (F2) and help menu (F1) available everywhere
- 12 languages: English, German, French, Spanish, Italian, Portuguese (BR), Japanese, Korean, Russian, Polish, Chinese Simplified, Chinese Traditional

## Requirements

- Windows 10 or later
- Magic: The Gathering Arena (installed via the official installer or Epic Games Store)
- NVDA screen reader (https://www.nvaccess.org/download/)
- MelonLoader 0.6.x (the installer handles this automatically)

## Installation

### Using the installer (recommended)

1. Download `AccessibleArenaInstaller.exe` from the latest release on GitHub: https://github.com/JeanStiletto/AccessibleArena/releases
2. Close MTG Arena if it is running
3. Run the installer. It will detect your MTGA installation, install MelonLoader if needed, and deploy the mod
4. Launch MTG Arena. You should hear "Accessible Arena v... launched" through NVDA

### Manual installation

1. Install MelonLoader 0.6.x into your MTGA folder (https://github.com/LavaGang/MelonLoader)
2. Download `AccessibleArena.dll` from the latest release
3. Copy the DLL to: `C:\Program Files\Wizards of the Coast\MTGA\Mods\`
4. Ensure `Tolk.dll` and `nvdaControllerClient64.dll` are in the MTGA root folder
5. Launch MTG Arena

## Quick start

After installation, launch MTG Arena. The mod announces the current screen through NVDA.

- Press **F1** at any time for a navigable help menu listing all keyboard shortcuts
- Press **F2** for the settings menu (language, verbosity, tutorial messages)
- Press **F3** to hear the name of the current screen
- Use **Arrow Up/Down** or **Tab/Shift+Tab** to navigate menus
- Press **Enter** or **Space** to activate elements
- Press **Backspace** to go back

## Keyboard shortcuts

### Menus

- Arrow Up/Down (or W/S): Navigate items
- Tab/Shift+Tab: Navigate items (same as Up/Down)
- Arrow Left/Right (or A/D): Carousel and stepper controls
- Home/End: Jump to first/last item
- Page Up/Page Down: Previous/next page in collection
- Enter/Space: Activate
- Backspace: Go back

### Duels - Zones

- C: Your hand
- G / Shift+G: Your graveyard / Opponent graveyard
- X / Shift+X: Your exile / Opponent exile
- S: Stack
- B / Shift+B: Your creatures / Opponent creatures
- A / Shift+A: Your lands / Opponent lands
- R / Shift+R: Your non-creatures / Opponent non-creatures

### Duels - Within zones

- Left/Right: Navigate cards
- Home/End: Jump to first/last card
- Arrow Up/Down: Read card details when focused on a card
- I: Extended card info (keyword descriptions, other faces)
- Shift+Up/Down: Switch battlefield rows

### Duels - Information

- T: Current turn and phase
- L: Life totals
- V: Player info zone (Left/Right to switch player, Up/Down for properties)
- D / Shift+D: Your library count / Opponent library count
- Shift+C: Opponent hand count

### Duels - Actions

- Space: Confirm (pass priority, confirm attackers/blockers, next phase)
- Backspace: Cancel / decline
- Tab: Cycle targets or highlighted elements
- Ctrl+Tab: Cycle opponent targets only
- Enter: Select target

### Duels - Browsers (Scry, Surveil, Mulligan)

- Tab: Navigate all cards
- C/D: Jump to top/bottom zone
- Left/Right: Navigate within zone
- Enter: Toggle card placement
- Space: Confirm selection
- Backspace: Cancel

### Global

- F1: Help menu
- F2: Settings menu
- F3: Announce current screen
- Ctrl+R: Repeat last announcement
- Backspace: Universal back/dismiss/cancel

## Reporting bugs

If you find a bug, please open an issue on GitHub: https://github.com/JeanStiletto/AccessibleArena/issues

Include the following information:

- What you were doing when the bug occurred
- What you expected to happen
- What actually happened
- Your NVDA version
- Attach the MelonLoader log file from: `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`

## Known issues

- Space key pass priority is not always reliable (the mod clicks the button directly as fallback)
- Deck builder deck list cards show only name and quantity, not full card details
- PlayBlade queue type selection (Ranked, Open Play, Brawl) may not always set the correct game mode

For the full list, see docs/KNOWN_ISSUES.md.

## Troubleshooting

**No speech output after launching the game**
- Make sure NVDA is running before launching MTG Arena
- Check that `Tolk.dll` and `nvdaControllerClient64.dll` are in the MTGA root folder (the installer places them automatically)
- Check the MelonLoader log at `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log` for errors

**Game crashes on startup or mod not loading**
- Make sure MelonLoader 0.6.x is installed. Other versions may not be compatible.
- If the game updated recently, MelonLoader or the mod may need to be reinstalled. Run the installer again.
- Check that `AccessibleArena.dll` is in `C:\Program Files\Wizards of the Coast\MTGA\Mods\`

**Mod was working but stopped after a game update**
- MTG Arena updates can overwrite MelonLoader files. Run the installer again to reinstall both MelonLoader and the mod.
- If the game changed its internal structure significantly, the mod may need an update. Check for new releases on GitHub.

**Keyboard shortcuts not working**
- Make sure the game window is focused (click on it or Alt+Tab to it)
- Press F1 to check if the mod is active. If you hear the help menu, the mod is running.
- Some shortcuts only work in specific contexts (duel shortcuts only work during a duel)

**Wrong language**
- Press F2 to open the settings menu, then use Enter to cycle through languages

## Building from source

Requirements: .NET SDK (any version that supports targeting net472)

```
git clone https://github.com/JeanStiletto/AccessibleArena.git
cd AccessibleArena
dotnet build src/AccessibleArena.csproj
```

The built DLL will be at `src/bin/Debug/net472/AccessibleArena.dll`.

Game assembly references are expected in the `libs/` folder. Copy these DLLs from your MTGA installation (`MTGA_Data/Managed/`):
- Assembly-CSharp.dll
- Core.dll
- UnityEngine.dll, UnityEngine.CoreModule.dll, UnityEngine.UI.dll, UnityEngine.UIModule.dll, UnityEngine.InputLegacyModule.dll
- Unity.TextMeshPro.dll, Unity.InputSystem.dll
- Wizards.Arena.Models.dll, Wizards.Arena.Enums.dll, Wizards.Mtga.Metadata.dll, Wizards.Mtga.Interfaces.dll
- ZFBrowser.dll

MelonLoader DLLs (`MelonLoader.dll`, `0Harmony.dll`) come from your MelonLoader installation.

## License

This project is licensed under the GNU General Public License v3.0. See the LICENSE file for details.

## Links

- GitHub: https://github.com/JeanStiletto/AccessibleArena
- NVDA screen reader: https://www.nvaccess.org/download/
- MelonLoader: https://github.com/LavaGang/MelonLoader
- MTG Arena: https://magic.wizards.com/mtgarena
