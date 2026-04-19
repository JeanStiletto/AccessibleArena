# Architecture Overview

High-level view of how Accessible Arena is structured and how systems interact.

## Entry Point

`AccessibleArenaMod` (inherits `MelonMod`) is the entry point. Initialization order:

1. **Screen reader** — Tolk library loads, detects NVDA/JAWS/Narrator
2. **Core services** — AnnouncementService, ShortcutRegistry, InputManager, UIFocusTracker, CardInfoNavigator, ModSettings, LocaleManager
3. **Panel detection** — PanelStateManager (owns Harmony, Reflection, and Alpha detectors)
4. **Navigator manager** — Manages screen navigator lifecycle
5. **Harmony patches** — UXEventQueuePatch, PanelStatePatch, KeyboardManagerPatch, EventSystemPatch, TimerPatch
6. **Global shortcuts** — F1 (Help), F2 (Mod Settings), F3 (Screen), F4 (Friends / Duel Chat), F5 (Update check), Ctrl+R (Repeat)

## Scene Handling

When scenes change (`OnSceneWasLoaded`):
- All caches clear (CardDetector, DeckInfoProvider, RecentPlayAccessor, EventAccessor)
- Panel state and detectors reset
- NavigatorManager activates the appropriate screen navigator
- DuelScene gets special handling via `DuelNavigator.OnDuelSceneLoaded()`

## Update Loop Priority

`OnUpdate()` processes input in strict priority order. Four modal layers run before the active screen navigator gets its turn — each blocks everything below it:
1. Help menu (F1)
2. Mod Settings menu (F2)
3. Extended card info (I)
4. Game log modal (O in duels)
5. Card detail navigator (arrow keys on focused card)
6. Active screen navigator (via NavigatorManager)
7. Focus tracking and panel state updates

`PhaseSkipGuard` runs alongside the Duel navigator. It watches Space-key timing to prevent an accidental pass on the same frame as a rapid combat-confirm, and can veto the key before it reaches the game.

## Navigator Architecture

All navigators inherit from `BaseNavigator`, which provides:
- Popup detection and handling
- Element focus and announcement
- Input field / dropdown editing modes
- Back navigation (Backspace)
- Shared shortcut infrastructure

(Line counts are intentionally omitted here because they drift quickly — see `source-inventory.md` for the current sizes.)

Screen navigators are selected by `NavigatorManager` based on scene / active panel:
- **GeneralMenuNavigator** — Main menu, deck builder, collection, store fronts
- **DuelNavigator** — Orchestrates a live duel: zones, battlefield, combat, targeting, browsers
- **BrowserNavigator** — Scry, Surveil, London Mulligan, other card-selection workflows
- **GroupedNavigator** — Hierarchical menu navigation with element grouping
- Other screen navigators include ProfileNavigator, AchievementsNavigator, MasteryNavigator, StoreNavigator, BoosterOpenNavigator, SideboardNavigator, NPERewardNavigator, DraftNavigator, PreBattleNavigator, and several more.

### Sub-Navigators (managed by a parent, not NavigatorManager)

Some overlay-style navigators are owned by a specific parent navigator so that their state survives the parent remaining active:
- **DuelChatNavigator** — F4 in duels; opened/closed by `DuelNavigator`, uses `HandleEarlyInput` so no duel action leaks through while chat is open.
- **ChatNavigator** / **ChatMessageWatcher** — Chat window and a global watcher that announces incoming messages when no chat UI is visible.
- **CardInfoNavigator**, **ExtendedInfoNavigator**, **GameLogNavigator** — Modal readouts attached to whichever navigator currently has focus.

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

All five live in `src/Patches/`. See `framework-reference.md` for per-patch method/target details.

- **UXEventQueuePatch** — Target: `UXEventQueue.EnqueuePending`. Read-only duel-event interception forwarded to DuelAnnouncer.
- **PanelStatePatch** — Target: NavContentController, SettingsMenu, blades, SocialUI, etc. Panel open/close detection and Tab/Enter blocking.
- **KeyboardManagerPatch** — Target: `KeyboardManager.PublishKeyDown/Up`. Scene- and context-based key blocking.
- **EventSystemPatch** — Target: `StandaloneInputModule`, `Input.GetKeyDown`. Blocks Unity EventSystem from stealing focus or submitting while the mod is handling input.
- **TimerPatch** — Target: `GameManager.Update_TimerNotification`. Surfaces duel-timer state changes so the mod can announce timeout warnings.

## Reflection Patterns

The mod uses extensive reflection to access game internals:
- Private fields: `GetField(name, NonPublic | Instance)` — must walk `BaseType` chain for inherited privates
- Cached via static `FieldInfo`/`MethodInfo`/`PropertyInfo` variables
- Cleared on scene change (ClearCache pattern)
- Key gotchas: `AttachedToId`, `IsTapped` are FIELDS not properties; `cTMP_Dropdown` extends `Selectable` not `TMP_Dropdown`

## Card Data

Card data is split across 5 focused static providers (originally one oversized `CardModelProvider`):
- **CardModelProvider** — Core: component access, name lookup, mana parsing, card info extraction
- **CardTextProvider** — Ability text, flavor text, artist names, localized text lookups (internal)
- **CardStateProvider** — Attachments, combat state, targeting, counters, card categorization
- **DeckCardProvider** — Deck list cards, sideboard cards, read-only deck cards
- **ExtendedCardInfoProvider** — Keyword descriptions, linked face info

Key patterns:
- Localized text via `GreLocProvider.GetLocalizedText(locId)` — never use enum `.ToString()`
- Card type detection via `CardStateProvider.GetCardCategory()` — never string-match type lines
- Supports duel cards (CDC-based) and collection cards (MetaCardView-based)

## Screen Reader Output

`ScreenReaderOutput` wraps Tolk library via P/Invoke:
- `Tolk_Output(text)` — Speak and braille
- `Tolk_Speak(text)` — Speak only (interrupts previous)
- `Tolk_Silence()` — Stop speaking
- Gracefully degrades if no screen reader detected
