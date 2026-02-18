# Contributing to Accessible Arena

Thanks for your interest in making MTG Arena more accessible.

## Getting started

### Prerequisites

- .NET SDK (any version that supports targeting net472)
- MTG Arena installed
- NVDA screen reader for testing
- A text editor or IDE (Visual Studio, VS Code, Rider)

### Setting up the project

1. Clone the repository:
   ```
   git clone https://github.com/JeanStiletto/AccessibleArena.git
   ```

2. Copy game assemblies to the `libs/` folder. These are found in your MTGA installation under `MTGA_Data/Managed/`:
   - Assembly-CSharp.dll, Core.dll
   - UnityEngine.dll, UnityEngine.CoreModule.dll, UnityEngine.UI.dll, UnityEngine.UIModule.dll, UnityEngine.InputLegacyModule.dll
   - Unity.TextMeshPro.dll, Unity.InputSystem.dll
   - Wizards.Arena.Models.dll, Wizards.Arena.Enums.dll, Wizards.Mtga.Metadata.dll, Wizards.Mtga.Interfaces.dll
   - ZFBrowser.dll

3. Copy MelonLoader DLLs to `libs/`:
   - MelonLoader.dll (from `MelonLoader/net35/` in your MTGA folder)
   - 0Harmony.dll (from the same location)

4. Build:
   ```
   dotnet build src/AccessibleArena.csproj
   ```

5. Deploy for testing (game must be closed):
   ```
   copy src\bin\Debug\net472\AccessibleArena.dll "C:\Program Files\Wizards of the Coast\MTGA\Mods\"
   ```

6. Launch MTG Arena and check the MelonLoader log: `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`

## Project structure

```
src/
  AccessibleArenaMod.cs          Entry point (MelonLoader mod class)
  ScreenReaderOutput.cs          Tolk wrapper for NVDA speech

  Core/
    Interfaces/                  IScreenNavigator and other interfaces
    Models/                      Data models, localization strings
    Services/
      UIActivator.cs             Element activation (click simulation)
      UITextExtractor.cs         Text extraction from UI elements
      CardDetector.cs            Card detection and info extraction
      CardModelProvider.cs       Card data from game models via reflection
      InputManager.cs            Keyboard input handling
      AnnouncementService.cs     Speech output management

      BaseNavigator.cs           Abstract base for all screen navigators
      NavigatorManager.cs        Navigator lifecycle and priority

      GeneralMenuNavigator.cs    Menus, login, home screen
      DuelNavigator.cs           Duel gameplay (delegates to zone/combat navigators)
      StoreNavigator.cs          Store screen
      MasteryNavigator.cs        Mastery/rewards screen
      BrowserNavigator.cs        Scry, surveil, mulligan browsers
      ...                        Other screen-specific navigators

      ElementGrouping/           Hierarchical menu navigation
      PanelDetection/            Panel/popup state tracking

  Patches/                       Harmony patches for game event interception

installer/                       Installer source code
libs/                            Reference assemblies (not checked in)
lang/                            Localization JSON files (12 languages)
docs/                            Technical documentation
```

## Key concepts

**Navigators** are the core abstraction. Each screen or overlay has a navigator (extending `BaseNavigator`) that handles keyboard input and announces elements via the screen reader. `NavigatorManager` activates the highest-priority navigator whose conditions are met.

**Reflection** is used extensively because game internals are not public. Cache `FieldInfo`/`MethodInfo`/`PropertyInfo` objects and clear component references on scene changes.

**Harmony patches** intercept game methods for event detection (duel events, panel state changes, keyboard blocking).

**Utilities** - always use these instead of reimplementing:
- `UIActivator.Activate(element)` for clicking UI elements
- `CardDetector.IsCard(element)` for card detection
- `UITextExtractor.GetText(element)` for text extraction

## Documentation

Detailed technical docs are in `docs/`:
- **GAME_ARCHITECTURE.md** - Game internals, assemblies, key types
- **MOD_STRUCTURE.md** - Full project layout and implementation status
- **BEST_PRACTICES.md** - Coding patterns, input handling, common pitfalls
- **KNOWN_ISSUES.md** - Active bugs, design decisions, investigation history
- **LOCALIZATION.md** - Translation system and how to add languages

## Submitting changes

1. Fork the repository
2. Create a branch for your change
3. Make your changes and test with NVDA
4. Verify the build: `dotnet build src/AccessibleArena.csproj`
5. Open a pull request with a description of what you changed and why

## Reporting bugs

See the bug report template on GitHub Issues. The most helpful thing you can include is the MelonLoader log file.

## Adding translations

See `docs/LOCALIZATION.md` for the full guide. In short: copy `lang/en.json` to a new file, translate the values, and add the language code to the supported languages list in `Strings.cs`.
