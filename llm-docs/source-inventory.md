# Source File Inventory

Generated: 2026-04-20 (updated after large-file-handling split round 2)
Total src files: 127 (excluding obj/ and bin/)
Total test files: 11 (8 test files + 3 stubs, excluding obj/)
Total src LOC: 94,939
Total test LOC: 1,303
Combined LOC: 96,242
Large files (over 2000 lines): 8 after splits of GeneralMenuNavigator (6148→3427, 6 new partials), BrowserNavigator (4528→2220, 6 new partials), BaseNavigator (4085→1600, 6 new partials), and DuelAnnouncer (3245→788, 6 new partials)

---

## Root (src/)

- **AccessibleArenaMod.cs** (442 lines) — MelonLoader entry point; wires all services, navigators, and patches.
- **AssemblyInfo.cs** (3 lines) — Grants InternalsVisibleTo the test project.
- **ScreenReaderOutput.cs** (106 lines) — P/Invoke wrapper for Tolk.dll (NVDA/JAWS/Narrator speech output).

---

## src/Core/Constants/

- **GameTypeNames.cs** (123 lines) — String constants for game type names used in reflection lookups.
- **SceneNames.cs** (18 lines) — String constants for Unity scene names used by MTGA.

---

## src/Core/Interfaces/

- **IAnnouncementService.cs** (20 lines) — Contract for announcing text to the screen reader with priority control.
- **IInputHandler.cs** (7 lines) — Minimal contract for per-frame input processing.
- **IScreenNavigator.cs** (54 lines) — Contract for screen-specific navigators (Tab/Enter, element lists, activation).
- **IScreenReaderOutput.cs** (12 lines) — Abstraction over Tolk; injected into AnnouncementService for testability.
- **IShortcutRegistry.cs** (18 lines) — Contract for registering and processing global keyboard shortcuts.

---

## src/Core/Models/

- **AnnouncementPriority.cs** (11 lines) — Enum: Low, Normal, High, Immediate, Critical.
- **ShortcutDefinition.cs** (36 lines) — Data class for a registered shortcut (key, modifier, action, description).
- **Strings.cs** (1638 lines) — Centralized, localized user-facing announcement strings for all navigators.
- **TargetInfo.cs** (72 lines) — Data class holding target info (GameObject, name, InstanceId) for targeting UI.

---

## src/Core/Utils/

- **KeyHoldRepeater.cs** (90 lines) — Fires repeated actions after initial hold delay; used for arrow-key hold-repeat.
- **ReflectionUtils.cs** (52 lines) — Shared BindingFlags constants and FindType helper for reflection across the mod.

---

## src/Core/Services/

- **AchievementsNavigator.cs** (1338 lines) — Multi-level navigator for the Achievements screen (overview, groups, items).
- **AdvancedFiltersNavigator.cs** (798 lines) — Grid navigator for the Advanced Filters popup in Collection/Deck Builder.
- **AnnouncementService.cs** (124 lines) — Routes text to IScreenReaderOutput with priority queuing and history.
- **AssetPrepNavigator.cs** (390 lines) — Low-priority navigator for the first-install asset download screen.
- **BaseNavigator/** (subfolder; class lives across 7 partial files, namespace stays `AccessibleArena.Core.Services`)
  - **BaseNavigator.cs** (1600 lines) — Core abstract partial: ctor/lifecycle, Update, TryActivate, Deactivate, HandleInput dispatch, Move/letter nav, SyncIndex, AddElement overloads, AddTextBlock/Button/Toggle/InputField, Find/Navigate helpers, RefreshElementLabel, AttachedAction/NavigableElement/CarouselInfo structs. Keeps `: IScreenNavigator`.
  - **BaseNavigator.Popup.cs** (1438 lines) — Popup detection, enter/exit, input handling, element discovery (text/title/input/dropdown/stepper/button), cancel-button finders, popup mode state.
  - **BaseNavigator.Dropdowns.cs** (388 lines) — Dropdown navigation + selection, silent value set, display-value readout, close routines, DropdownKind enum (TMP/Legacy/Custom).
  - **BaseNavigator.Carousel.cs** (262 lines) — Carousel/slider/stepper arrow handling, attached-action cycling, spinner rescan, stepper value announcement.
  - **BaseNavigator.ChallengeInvite.cs** (216 lines) — Challenge invite popup: tile discovery, player-name/toggle lookup, recent-players dropdown, toggle label refresh.
  - **BaseNavigator.InputFields.cs** (215 lines) — Input-field edit mode enter/exit/force-exit, search-rescan scheduling, TrackInputFieldState, field navigation.
  - **BaseNavigator.Chat.cs** (98 lines) — OpenChat (F4) with static reflection cache for ShowChatWindow.
- **BattlefieldNavigator.cs** (847 lines) — Navigates battlefield by 6 rows (your/enemy lands, creatures, non-creatures).
- **BoosterOpenNavigator.cs** (1494 lines) — Navigates the booster pack reveal card list after opening a pack.
- **BrowserDetector.cs** (806 lines) — Detects active in-duel browser type (Scry, London/Surveil, SelectCards, generic).
- **BrowserNavigator/** (subfolder; class lives across 7 partial files, namespace stays `AccessibleArena.Core.Services`)
  - **BrowserNavigator.cs** (2220 lines) [LARGE] — Core partial: lifecycle, input routing, discovery, navigation, announcements, activation, button scaffolding.
  - **BrowserNavigator.AssignDamage.cs** (581 lines) — Assign-damage browser: spinner adjustments, lethal check, submit/undo, entry/card announcements.
  - **BrowserNavigator.Keyword.cs** (564 lines) — ChoiceFilter/Keyword selection: reflection cache, filter state, toggle, letter-jump, input handling.
  - **BrowserNavigator.Workflow.cs** (583 lines) — Workflow reflection: submit/cancel routing through game workflow + button-pattern fallback.
  - **BrowserNavigator.OrderCards.cs** (288 lines) — Order-cards drag/drop: pickup, placement, holder sync via reflection.
  - **BrowserNavigator.SelectGroup.cs** (208 lines) — SelectGroup (two-pile) browser: state caching, pile discovery, pile-aware announcements.
  - **BrowserNavigator.MultiZone.cs** (172 lines) — Multi-zone selector: zone button cycling, active zone detection, zone-suffix labels.
- **BrowserZoneNavigator.cs** (1385 lines) — Zone-based card navigation inside Scry, Surveil, and London Mulligan browsers.
- **CardDetector.cs** (816 lines) — Static utilities: IsCard, GetCardRoot, HasValidTargetsOnBattlefield.
- **CardInfoNavigator.cs** (287 lines) — Lazy vertical navigation through card info blocks (name, cost, type, rules, etc.).
- **CardModelProvider.cs** (2374 lines) [LARGE] — Reflection-based access to card model data, name lookups, and mana parsing.
- **CardPoolAccessor.cs** (379 lines) — Reflection wrapper for CardPoolHolder (collection page navigation in deck builder).
- **CardStateProvider.cs** (1088 lines) — Attachment, combat state, targeting, and card categorization helpers.
- **CardTextProvider.cs** (614 lines) — Localized ability text, flavor text, and artist name lookups via game providers.
- **ChatMessageWatcher.cs** (317 lines) — Polls ChatManager for new messages and announces them when no chat navigator is open.
- **ChatNavigator.cs** (856 lines) — Navigator for the chat window; message list, input field, and send button.
- **ChooseXNavigator.cs** (505 lines) — Detects and navigates the ChooseX popup (X-cost spells, die rolls).
- **CodexNavigator.cs** (1278 lines) — Navigator for the Codex of the Multiverse (TOC drill-down and article content).
- **CombatNavigator.cs** (763 lines) — Handles Space/Backspace for attacker/blocker declaration and announces combat state.
- **DebugConfig.cs** (137 lines) — Master debug toggle and category flags; ring-buffer for playback via Shift+F12.
- **DeckCardProvider.cs** (915 lines) — Reflection access to MainDeck, Sideboard, and StaticColumn read-only card holders.
- **DeckInfoProvider.cs** (1082 lines) — Reads mana curve, average cost, and type breakdown from DeckCostsDetails UI.
- **DraftNavigator.cs** (1067 lines) — Navigator for draft pack picking (DraftPackHolder, Enter to select, Space to confirm).
- **DropdownEditHelper.cs** (241 lines) — Thin wrapper for dropdown edit-mode state; routes keys to BaseNavigator dropdown methods.
- **DropdownStateManager.cs** (497 lines) — Unified source of truth for dropdown open/close state and value-change suppression.
- **DuelAnnouncer/** (subfolder; class lives across 7 partial files, namespace stays `AccessibleArena.Core.Services`)
  - **DuelAnnouncer.cs** (788 lines) — Core partial: singleton, lifecycle, OnGameEvent dispatch, event classification, small builders (Reveal/Counters/GameEnd), mana pool, reflection helpers, counts accessors.
  - **DuelAnnouncer.Zones.cs** (644 lines) — Zone transfer events: HandleUpdateZoneEvent, HandleZoneTransferGroup, ProcessBattlefield/Graveyard/Exile/Hand entries, attach resolution, land detection.
  - **DuelAnnouncer.Combat.cs** (554 lines) — Combat + damage: attacker declarations, damage chain extraction, DamageInfo nested class, P/T lookup, CombatFrame, CardModelUpdate.
  - **DuelAnnouncer.Resolution.cs** (478 lines) — Spell/ability resolution: started/ended, library browser, countered, cast announcements, delayed coroutine announcements.
  - **DuelAnnouncer.NPE.cs** (444 lines) — NPE tutorial: dialog/reminder/tooltip/warning handlers, hover simulation, NPE director reflection.
  - **DuelAnnouncer.PhaseTurn.cs** (242 lines) — Phase/step/turn tracking, life changes, phase debounce.
  - **DuelAnnouncer.Commander.cs** (198 lines) — Commander format: grpId/info/name getters, commander reflection cache, match manager traversal.
- **DuelChatNavigator.cs** (949 lines) — Sub-navigator for in-duel chat (F4 toggle, preserves zone/card state in DuelNavigator).
- **DuelHolderCache.cs** (46 lines) — Static cache for duel card-holder GameObjects to avoid repeated scene scans.
- **DuelNavigator.cs** (909 lines) — Top-level duel navigator; delegates to ZoneNavigator, combat, browser, mana picker, etc.
- **EventAccessor.cs** (1515 lines) — Reflection access to event tiles, event page, and packet selection for UI labels.
- **ExtendedCardInfoProvider.cs** (999 lines) — Extended card info: keyword ability descriptions and linked-face data.
- **ExtendedInfoNavigator.cs** (326 lines) — Modal navigator for keyword and linked-face info (I key, Up/Down, Backspace to close).
- **FriendInfoProvider.cs** (651 lines) — Reads friend tile data (name, status, actions) from social panel tiles via reflection.
- **GameLogNavigator.cs** (178 lines) — Modal navigator for duel announcement history (O key, Up/Down, Backspace to close).
- **GeneralMenuNavigator/** (subfolder; class lives across 7 partial files, namespace stays `AccessibleArena.Core.Services`)
  - **GeneralMenuNavigator.cs** (3427 lines) [LARGE] — Core partial: scene/foreground detection, discovery, move nav, grouped enter/backspace.
  - **GeneralMenuNavigator.Mail.cs** (299 lines) — Mail letter open + field navigation, close mailbox/detail-view.
  - **GeneralMenuNavigator.Booster.cs** (262 lines) — Booster-pack carousel element + left/right/open handling.
  - **GeneralMenuNavigator.Social.cs** (553 lines) — Friends panel (open/close, tile discovery, blocked/challenge tiles, friend action sub-nav).
  - **GeneralMenuNavigator.DeckBuilder.cs** (1063 lines) — Deck builder back, toolbar actions, card-finding (pool/commander/deck/sideboard/NPE/read-only), rename mode, deck-info sub-nav.
  - **GeneralMenuNavigator.BackNavigation.cs** (361 lines) — Backspace routing (content panel, campaign graph, NPE, play-blade, generic fallback).
  - **GeneralMenuNavigator.Collection.cs** (318 lines) — Collection-page button activation + packet-block sub-nav.
- **HelpNavigator.cs** (306 lines) — Modal navigator for keybind help items (F1, Up/Down, Backspace to close).
- **HotHighlightNavigator.cs** (1484 lines) — Unified navigator for HotHighlight-based selection (targets, discard, highlights).
- **InputFieldEditHelper.cs** (582 lines) — Shared input-field edit-mode logic (TMP_InputField, legacy, key routing, character announce).
- **InputManager.cs** (251 lines) — Per-frame keyboard handler; key-consumption tracking to block keys from the game.
- **LetterSearchHandler.cs** (67 lines) — Buffered letter-key jump-to-item with prefix matching and cycling.
- **LoadingScreenNavigator.cs** (1422 lines) — Navigator for transitional screens (loading, match end, splash) with few buttons.
- **LocaleManager.cs** (402 lines) — Singleton that loads and resolves localized strings from JSON files with fallback chain.
- **ManaColorPickerNavigator.cs** (595 lines) — Detects and navigates the ManaColorSelector popup for any-color mana sources.
- **MasteryNavigator.cs** (2174 lines) [LARGE] — Navigator for the Mastery/Rewards (RewardTrack) screen with level and tier navigation.
- **MenuDebugHelper.cs** (1474 lines) — Debug/logging utilities for GeneralMenuNavigator; extracted to reduce file size.
- **MenuScreenDetector.cs** (469 lines) — Detects active content controllers and screen names in the MTGA menu system.
- **ModSettings.cs** (209 lines) — Mod settings with JSON file persistence (verbose, hints, language, etc.).
- **ModSettingsNavigator.cs** (456 lines) — Modal navigator for toggling mod settings (F2, Up/Down, Enter, Backspace to close).
- **NavigatorManager.cs** (209 lines) — Manages all IScreenNavigator instances; enforces single-active-at-a-time by priority.
- **NPERewardNavigator.cs** (733 lines) — Navigator for the NPE (New Player Experience) reward screen card list.
- **NPETutorialTextProvider.cs** (203 lines) — Maps NPE tutorial reminder keys to keyboard-focused replacement hint texts.
- **OverlayNavigator.cs** (494 lines) — Navigator for modal overlays (What's New, announcements, reward popups).
- **PhaseSkipGuard.cs** (204 lines) — Guards against accidental pass-priority when untapped lands exist in main phase.
- **PlayerPortraitNavigator.cs** (2151 lines) [LARGE] — V-key zone for player info, property cycling, and emote sending during duels.
- **PreBattleNavigator.cs** (189 lines) — Navigator for the pre-game VS screen Continue/Cancel prompt (PreGameScene).
- **PriorityController.cs** (586 lines) — Reflection wrapper for full-control toggle and phase-stop toggles via AutoRespManager.
- **ProfileNavigator.cs** (1889 lines) — Navigator for the Profile screen (username, rank, cosmetic sub-panels).
- **RecentPlayAccessor.cs** (497 lines) — Reflection access to LastPlayedBladeView for enriching Recent tab deck labels.
- **RewardPopupNavigator.cs** (1402 lines) — Navigator for rewards popup from mail claims and store purchases.
- **ScreenReaderAdapter.cs** (13 lines) — Production IScreenReaderOutput implementation; delegates to ScreenReaderOutput (Tolk).
- **SettingsMenuNavigator.cs** (1117 lines) — Dedicated navigator for the Settings menu; works in all scenes including duels.
- **ShortcutRegistry.cs** (63 lines) — IShortcutRegistry implementation storing and dispatching global shortcuts.
- **SideboardNavigator.cs** (1039 lines) — Navigator for the Bo3 sideboard screen (C=pool, D=deck, zone-based swap).
- **SpinnerNavigator.cs** (580 lines) — Detects and navigates SpinnerAnimated counter-distribution widgets in duels.
- **SteamOverlayBlocker.cs** (157 lines) — Detects Steam overlay and warns user to disable it to prevent Shift+Tab conflicts.
- **StoreNavigator.cs** (2773 lines) [LARGE] — Two-level Store navigator (tabs then items) with purchase option support.
- **UIActivator.cs** (2745 lines) [LARGE] — Centralized UI activation: click buttons, toggle checkboxes, focus fields, play cards.
- **UIElementClassifier.cs** (1857 lines) — Classifies UI elements by role and navigability for screen reader labeling.
- **UIFocusTracker.cs** (798 lines) — Polls EventSystem each frame for focus changes and announces them via screen reader.
- **UITextExtractor.cs** (2760 lines) [LARGE] — Extracts readable text from Unity GameObjects; checks UI components in priority order.
- **UpdateChecker.cs** (354 lines) — Checks GitHub for mod updates on startup; F5 triggers download and relaunch.
- **WebBrowserAccessibility.cs** (2236 lines) [LARGE] — Full keyboard navigation for embedded Chromium (ZFBrowser) popups via JavaScript.
- **ZoneNavigator.cs** (1214 lines) — Navigates duel zones (Hand, Graveyard, Exile, Stack, Command Zone) with zone-owner priority.

---

## src/Core/Services/ElementGrouping/

- **ChallengeNavigationHelper.cs** (1065 lines) — Two-level navigation helper for Direct/Friend Challenge screens (spinners, deck selection).
- **ElementGroup.cs** (303 lines) — Enum defining UI element groups (Primary, Secondary, Play, Filter, etc.) for grouped navigation.
- **ElementGroupAssigner.cs** (796 lines) — Assigns UI elements to ElementGroups based on parent hierarchy and name patterns.
- **GroupedNavigator.cs** (2167 lines) [LARGE] — Hierarchical group-level then item-level menu navigation used by GeneralMenuNavigator.
- **OverlayDetector.cs** (451 lines) — Simplified overlay detection; queries PanelStateManager to suppress non-overlay groups.
- **PlayBladeNavigationHelper.cs** (256 lines) — Enum and result type for PlayBlade navigation actions from GeneralMenuNavigator.

---

## src/Core/Services/PanelDetection/

- **AlphaPanelDetector.cs** (342 lines) — Polling detector for CanvasGroup alpha-based popup visibility (SystemMessageView, dialogs).
- **HarmonyPanelDetector.cs** (317 lines) — Event-driven detector using Harmony patches on Show/Hide methods (PlayBlade, Settings).
- **PanelInfo.cs** (259 lines) — Data class for active panel info; static ClassifyPanel() maps names to PanelType.
- **PanelStateManager.cs** (499 lines) — Singleton source of truth for panel state; detectors report here, consumers subscribe.
- **PanelType.cs** (49 lines) — Enum of panel types (None, Login, Settings, Blade, Overlay, Filter, etc.).
- **ReflectionPanelDetector.cs** (342 lines) — Polling detector using reflection on IsOpen properties (login panels, PopupBase).

---

## src/Core/Services/old/ (archived, not compiled)

- **CodeOfConductNavigator.cs** (254 lines) — Old navigator for terms/consent screens with checkboxes; superseded by GeneralMenuNavigator.
- **DiscardNavigator.cs** (314 lines) — Old discard-phase card selector; superseded by HotHighlightNavigator.
- **EventTriggerNavigator.cs** (765 lines) — Old EventTrigger/CustomButton navigator for NPE and rewards; superseded by GeneralMenuNavigator.
- **HighlightNavigator.cs** (611 lines) — Old HotHighlight playable-card navigator; superseded by HotHighlightNavigator.
- **LoginPanelNavigator.cs** (157 lines) — Old login panel (email/password) navigator; superseded by GeneralMenuNavigator.
- **TargetNavigator.cs** (553 lines) — Old target-selection navigator; superseded by HotHighlightNavigator.
- **WelcomeGateNavigator.cs** (52 lines) — Old WelcomeGate (login/register choice) navigator; superseded by GeneralMenuNavigator.

---

## src/Patches/

- **EventSystemPatch.cs** (310 lines) — Blocks Enter on toggles and arrow keys in input fields via Unity EventSystem patches.
- **KeyboardManagerPatch.cs** (224 lines) — Blocks keys from MTGA.KeyboardManager; blocks Enter in duels, uses ConsumeKey elsewhere.
- **PanelStatePatch.cs** (1445 lines) — Intercepts NavContentController and blade Show/Hide to notify HarmonyPanelDetector.
- **TimerPatch.cs** (98 lines) — Postfix patch on Update_TimerNotification to announce timeout events.
- **UXEventQueuePatch.cs** (254 lines) — Intercepts UXEventQueue to forward duel events to DuelAnnouncer (read-only, no state changes).

---

## tests/

- **AnnouncementServiceTests.cs** (190 lines) — NUnit tests for priority queuing, interruption, and history in AnnouncementService.
- **DebugConfigTests.cs** (116 lines) — NUnit tests for DebugConfig category toggles and reset behavior.
- **KeyHoldRepeaterTests.cs** (179 lines) — NUnit tests for hold-repeat timing and key-change reset.
- **LetterSearchHandlerTests.cs** (138 lines) — NUnit tests for buffered letter search, prefix building, and cycling.
- **LocaleManagerJsonTests.cs** (92 lines) — NUnit tests for LocaleManager's hand-written flat JSON parser.
- **LocaleManagerTests.cs** (275 lines) — NUnit tests for LocaleManager key resolution, fallback chain, and language switching.
- **ShortcutRegistryTests.cs** (147 lines) — NUnit tests for shortcut registration, dispatch, and modifier handling.
- **TargetInfoTests.cs** (65 lines) — NUnit tests for TargetInfo.GetAnnouncement formatting.

### tests/stubs/

- **MelonLoaderStubs.cs** (15 lines) — No-op MelonLoader stubs (MelonLogger, MelonMod, MelonPlugin) for test compilation.
- **UnityEngineStubs.cs** (63 lines) — Minimal UnityEngine stubs (KeyCode, GameObject, Vector2/3) for test compilation.
- **UnityInputStubs.cs** (23 lines) — Stub for UnityEngine.Input with helper to simulate key state in tests.

---

## Recently archived / removed

These files appeared in the previous inventory (2026-03-04) but no longer exist in the codebase.

- **PanelRegistry.cs** — Was the static dictionary mapping panel names to type and behavior metadata; merged into PanelInfo.cs.
- **PanelDetectorManager.cs** — Was the coordinator for multiple panel detector plugins; replaced by PanelStateManager.
- **IPanelDetector.cs** (under old/detector-plugin-system/) — Was the interface for detector plugins in the aborted plugin-system design.
- **UnifiedPanelDetector.cs** — Was an attempt to merge all detectors into one class before the current split design.
- **PanelAnimationDiagnostic.cs** — Was a debug helper for logging panel animation state changes; removed as no longer needed.
- **MenuPanelTracker.cs** — Was an early iteration tracking panel visibility by polling menu component state; replaced by HarmonyPanelDetector and ReflectionPanelDetector.
