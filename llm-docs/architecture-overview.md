# Architecture Overview

High-level view of how Accessible Arena is structured and how systems interact.

## Entry Point

`AccessibleArenaMod` (inherits `MelonMod`) is the entry point. Initialization order:

1. **Screen reader** — Tolk library loads, detects NVDA/JAWS/Narrator
2. **Core services** — AnnouncementService, ShortcutRegistry, InputManager, UIFocusTracker, CardInfoNavigator, ModSettings, LocaleManager
3. **Panel detection** — PanelStateManager (owns Harmony, Reflection, and Alpha detectors)
4. **Navigator manager** — Manages screen navigator lifecycle
5. **Harmony patches** — UXEventQueuePatch, PanelStatePatch, KeyboardManagerPatch, EventSystemPatch
6. **Global shortcuts** — F1 (Help), F2 (Settings), F3 (Screen), Ctrl+R (Repeat)

## Scene Handling

When scenes change (`OnSceneWasLoaded`):
- All caches clear (CardDetector, DeckInfoProvider, RecentPlayAccessor, EventAccessor)
- Panel state and detectors reset
- NavigatorManager activates the appropriate screen navigator
- DuelScene gets special handling via `DuelNavigator.OnDuelSceneLoaded()`

## Update Loop Priority

`OnUpdate()` processes input in strict priority order:
1. Help menu (F1) — blocks everything below
2. Settings menu (F2) — blocks everything below
3. Extended info (I key) — blocks everything below
4. Card detail navigator (arrow keys on focused card)
5. Active screen navigator (via NavigatorManager)
6. Focus tracking and panel state updates

## Navigator Architecture

All navigators inherit from `BaseNavigator` (2,928 lines) which provides:
- Popup detection and handling
- Element focus and announcement
- Input field/dropdown editing modes
- Back navigation (Backspace)
- Shared shortcut infrastructure

Key navigators:
- **GeneralMenuNavigator** (4,766 lines) — Main menu, deck builder, collection, store fronts
- **DuelNavigator** — Orchestrates duel: zones, battlefield, combat, targeting, browsers
- **BrowserNavigator** (2,177 lines) — Scry, Surveil, London Mulligan, other card selection UIs
- **GroupedNavigator** (1,702 lines) — Hierarchical menu navigation with element grouping

## Input System (Two Layers)

**Layer 1: Unity Legacy Input** (`Input.GetKeyDown`)
- Used by the mod for key detection
- Cannot consume — all listeners see every keypress

**Layer 2: Game's KeyboardManager** (`PublishKeyDown`/`PublishKeyUp`)
- Harmony-patched via `KeyboardManagerPatch`
- Scene-based blocking: Enter blocked in DuelScene, Tab blocked in menus, Ctrl blocked in duels
- Context-based blocking: Enter during dropdown mode, Escape during input field editing
- Per-frame consumption via `InputManager.ConsumeKey()`

## Panel Detection (Three Systems)

1. **Harmony patches** (`PanelStatePatch`) — Event-driven, fires on open/close of NavContentController, SettingsMenu, blades, social UI
2. **Reflection polling** (`ReflectionPanelDetector`) — Checks IsOpen properties every frame for Login, PopupBase
3. **Alpha watching** (`AlphaPanelDetector`) — Monitors CanvasGroup.alpha for dialog visibility

All feed into `PanelStateManager` which navigators query.

## Harmony Patches

| Patch | Target | Purpose |
|-------|--------|---------|
| UXEventQueuePatch | UXEventQueue.EnqueuePending | Read-only game event interception for duel announcements |
| PanelStatePatch | NavContentController, SettingsMenu, blades, SocialUI | Panel open/close detection, Tab/Enter blocking |
| KeyboardManagerPatch | KeyboardManager.PublishKeyDown/Up | Scene/context key blocking |
| EventSystemPatch | StandaloneInputModule, Input.GetKeyDown | Block Unity EventSystem from interfering with mod navigation |

## Reflection Patterns

The mod uses extensive reflection to access game internals:
- Private fields: `GetField(name, NonPublic | Instance)` — must walk `BaseType` chain for inherited privates
- Cached via static `FieldInfo`/`MethodInfo`/`PropertyInfo` variables
- Cleared on scene change (ClearCache pattern)
- Key gotchas: `AttachedToId`, `IsTapped` are FIELDS not properties; `cTMP_Dropdown` extends `Selectable` not `TMP_Dropdown`

## Card Data

`CardModelProvider` (4,626 lines) handles all card data extraction:
- Localized text via `GreLocProvider.GetLocalizedText(locId)` — never use enum `.ToString()`
- Card type detection via `GetCardCategory()` — never string-match type lines
- Supports duel cards (CDC-based) and collection cards (MetaCardView-based)

## Screen Reader Output

`ScreenReaderOutput` wraps Tolk library via P/Invoke:
- `Tolk_Output(text)` — Speak and braille
- `Tolk_Speak(text)` — Speak only (interrupts previous)
- `Tolk_Silence()` — Stop speaking
- Gracefully degrades if no screen reader detected
