# Framework Reference

## Mod Loader

**MelonLoader** — Runtime mod injection for Unity games
- Mod DLL goes in `<game>/Mods/`
- Logs at `<game>/MelonLoader/Latest.log`
- Base class: `MelonMod` with lifecycle hooks (`OnInitializeMelon`, `OnSceneWasLoaded`, `OnUpdate`)

## Target Framework

- .NET Framework 4.7.2 (`net472`)
- Mod version: defined in `Directory.Build.props`

## Dependencies

### Modding Framework
- `MelonLoader.dll` — Mod loader runtime
- `0Harmony.dll` — Harmony 2.x for IL patching

### Unity Engine Modules
- UnityEngine (core)
- UnityEngine.CoreModule
- UnityEngine.UI
- UnityEngine.UIModule
- UnityEngine.InputLegacyModule (legacy `Input.GetKeyDown` API)
- Unity.TextMeshPro
- Unity.InputSystem (game uses internally)

### MTGA Game Assemblies

Located at `<game>/MTGA_Data/Managed/`:

- **Core.dll** (~11.8 MB, 10,600+ types) — Most game logic: zones, cards, UI controllers, browsers, events
- **Assembly-CSharp.dll** (~314 KB) — HasbroGo namespace, some UI types
- **Wizards.Arena.Models.dll** (~141 KB, 550 types) — DTOs for cards, decks, events, draft
- **Wizards.Arena.Enums.dll** — Game enumerations
- **Wizards.Mtga.Metadata.dll** — Card and game metadata
- **Wizards.Mtga.Interfaces.dll** — Game interfaces
- **Wizards.MDN.GreProtobuf.dll** — GRE protocol enums (StopType, Phase, Step, etc.)
- **ZFBrowser.dll** — Embedded web browser

### Screen Reader
- **Tolk** — P/Invoke to native Tolk.dll for NVDA/JAWS/Narrator communication

## Harmony Patch Details

### UXEventQueuePatch
- **Target:** `Wotc.Mtga.DuelScene.UXEvents.UXEventQueue`
- **Methods:** `EnqueuePending(UXEvent)`, `EnqueuePending(IEnumerable<UXEvent>)`
- **Type:** Postfix (read-only)
- **Purpose:** Forwards game events to DuelAnnouncer for speech output

### PanelStatePatch
- **Targets:** NavContentController, SettingsMenu, DeckSelectBlade, PlayBladeController, HomePageContentController, BladeContentView, SocialUI, NavBarController, ContentControllerPlayerInbox
- **Type:** Postfix + Prefix
- **Purpose:** Panel state change detection; blocks Tab in SocialUI, Enter during dropdown mode
- **Events:** `OnPanelStateChanged(controller, isOpen, typeName)`, `OnMailLetterSelected(...)`

### KeyboardManagerPatch
- **Target:** `MTGA.KeyboardManager.KeyboardManager`
- **Methods:** `PublishKeyDown`, `PublishKeyUp`
- **Type:** Prefix (can block)
- **Blocking rules:**
  - DuelScene: Block Enter entirely, block Ctrl (prevents accidental full control)
  - Menu scenes: Block Tab (mod handles navigation)
  - Dropdown mode: Block Enter
  - Input field focused: Block Escape and Tab
  - Mod menus open: Block Escape

### EventSystemPatch
- **Target:** `StandaloneInputModule`, `Input.GetKeyDown`
- **Type:** Prefix (can block)
- **Purpose:** Prevents Unity's EventSystem from moving focus or submitting when mod is handling input
- **Blocks:** Arrow keys during input field editing, Tab from EventSystem, Submit on toggles/dropdowns

## Decompilation

For investigating game internals:
```
ilspycmd "<game>/MTGA_Data/Managed/Core.dll" -t "Namespace.TypeName"
```
- Pipe through PowerShell for proper encoding
- Try Core.dll first, then Assembly-CSharp.dll
- `-l` flag does not work in ilspycmd v8.2.0.7535
