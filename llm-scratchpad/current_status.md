# LLM Refactoring Session Status — Round 2

## Branch
`claude-mod-cleanup-round2` (based off `main` at commit dfb040d)

## Game
Magic: The Gathering Arena (Unity, .NET 4.7.2, MelonLoader mod)

## Context From Round 1
Round 1 ran 2026-03-05 and completed all prompts except finalization.
Artifacts archived at `archive/llm-refactoring-round-1-2026-03-05/`.
Notable round-1 outcomes (still in the codebase, do not redo):
- `CardModelProvider.cs` split into 5 files (CardModelProvider, CardTextProvider,
  CardStateProvider, DeckCardProvider, ExtendedCardInfoProvider)
- `ReflectionUtils` / `GameTypeNames` / `SceneNames` constant modules introduced
- `UITextExtractor.GetButtonText()` consolidated
- `llm-docs/` created with architecture-overview, source-inventory,
  framework-reference, type-index + decompiled/
- Empty catch blocks annotated / logged across ~22 files
- Scene-scan caching added (IsAnyInputFieldFocused, dropdown state,
  DetectActiveContentController)

## Prompts Completed
- [x] sanity-checks-setup.md  (this file proves it)
- [x] information-gathering-and-checking.md  (2026-04-19)
- [x] code-directory-construction.md  (2026-04-19)

## Prompts Pre-Marked Complete (no-op by user direction)
User confirmed both checks were completed successfully in round 1 and the
answers have not changed. Do NOT re-run these analyses; go straight to
"Up Next" in both prompts.

- [x] input-handling.md — **Determination: mature sub-navigator architecture
      already in place. Not an input-redesign mod.**
      The mod already has: BaseNavigator + NavigatorManager dispatch,
      sub-navigator pattern (BrowserNavigator, DuelChatNavigator, etc.),
      ShortcutRegistry + ShortcutDefinition, InputFieldEditHelper /
      DropdownEditHelper, Harmony patches on KeyboardManager / EventSystem /
      UXEventQueue / PanelState. No redesign is warranted.

- [x] string-builder.md — **Determination: not a string-builder mod.**
      Mod output is structured announcements (Tolk speech + info blocks
      traversed via arrow keys), not concatenated multi-field messages.
      Round 1 confirmed this via subagent file-scan pass. Skip.

## Information-Gathering Findings (2026-04-19)
Drift fixes applied:
- `CLAUDE.md`: patch count 4→5 + list; added GRE/SharedClientCore DLLs;
  decompile command now shows `-Dll` flag + mentions `decompile-all.ps1`;
  removed dead `arena accessibility backlog.txt` IGNORE note; added
  missing shortcut entries (F4 Friends/DuelChat, F5 update, Ctrl+F1
  tutorial hint, O Game Log, E/Shift+E timers); noted docs/ listing as
  "primary entry points" not exhaustive.
- `llm-docs/architecture-overview.md`: dropped volatile line-counts; added
  TimerPatch; Update-loop priority expanded to 4 modal layers including
  `GameLogNavigator`; added `PhaseSkipGuard` note; added Sub-Navigators
  section (DuelChatNavigator, ChatNavigator, ChatMessageWatcher, etc.);
  Harmony patch table converted from markdown table to bullets.
- `llm-docs/framework-reference.md`: ilspycmd version 8.2→9.1; added
  TimerPatch and SharedClientCore.dll; decompile section points at
  `decompile.ps1` / `decompile-all.ps1` helpers.
- `llm-docs/source-inventory.md`: full regeneration. 115 src files /
  94,716 LOC (was 91 / 55,635). 12 files >2000 lines tagged `[LARGE]`
  for the large-file-handling prompt.
- `llm-docs/decompiled/`: removed scratch file `_core_types_order.txt`
  (four other expected scratch names were already gone).

Findings not yet acted on (flag for future prompts):
- `GeneralMenuNavigator.cs` is 6,148 lines — candidate #1 for
  large-file-handling.md even though round 1 said it was single-concern.
  The file has grown 29% since then; worth re-evaluating.
- `Strings.cs` is 1,638 lines and still growing — watch for LARGE.
- `type-index.md` vs `decompiled/` has ~66 types without decompiled files
  and ~160 orphan decompiled files. Not broken, just inconsistent. Could
  be a follow-up task if desired; not blocking.

False alarms verified:
- Source-inventory subagent wondered if `src/Core/Services/old/` was
  still compiled in. `src/AccessibleArena.csproj` line 5 has
  `<Compile Remove="**/old/**/*.cs" />`, so it is correctly excluded.
  Inventory labels the directory "(archived, not compiled)".

## Prompts Remaining
- [x] large-file-handling.md  (12/12 done 2026-04-20; see split details below)
- [x] input-handling.md          (pre-marked; no redesign warranted)
- [x] string-builder.md          (pre-marked; not a string-builder mod)
- [x] high-level-cleanup.md  (Q1–Q4 + M1–M4 done 2026-04-20; see section below)
- [x] low-level-cleanup.md  (Q1–Q10 + M1–M5 done 2026-04-20; see section below)
- [ ] finalization.md

## Large-File-Handling Progress (2026-04-20)

Split 1/12: **GeneralMenuNavigator.cs** (6148 → 3427 lines, -44%) via partial
class split into 6 topical files (Mail 299, Booster 262, Social 553,
DeckBuilder 1063, BackNavigation 361, Collection 318), moved into
`src/Core/Services/GeneralMenuNavigator/` subfolder (namespace unchanged).
Build: 0/0, tests: 105/105. User smoke-tested and confirmed working.

Split 2/12: **BrowserNavigator.cs** (4528 → 2220 lines, -51%) via partial
class split into 6 topical files (AssignDamage 581, Keyword 564,
Workflow 583, OrderCards 288, SelectGroup 208, MultiZone 172), moved into
`src/Core/Services/BrowserNavigator/` subfolder. Build: 0/0, tests: 105/105.

Split 3/12: **BaseNavigator.cs** (4085 → 1600 lines, -61%) via partial
class split into 6 topical files (Popup 1438, Dropdowns 388, Carousel 262,
ChallengeInvite 216, InputFields 215, Chat 98), moved into
`src/Core/Services/BaseNavigator/` subfolder. Core keeps `abstract` and
`: IScreenNavigator`; feature partials are plain `public partial class`.
Build: 0/0, tests: 105/105.

Split 4/12: **DuelAnnouncer.cs** (3245 → 788 lines, -76%) via partial
class split into 6 topical files (Zones 644, Combat 554, Resolution 478,
NPE 444, PhaseTurn 242, Commander 198), moved into
`src/Core/Services/DuelAnnouncer/` subfolder. Build: 0/0, tests: 105/105.

Split 5/12: **StoreNavigator.cs** (2773 → 1062 lines, -62%) via partial
class split into 6 topical files (Details 499, ConfirmationModal 312,
Tabs 309, Items 296, SetFilter 230, Utility 174), moved into
`src/Core/Services/StoreNavigator/` subfolder. Core keeps `: BaseNavigator`;
feature partials are plain `public partial class`. Build: 0/0, tests: 105/105.

Split 6/12: **UITextExtractor.cs** (2760 → 548 lines, -80%) via partial
class split into 5 topical files (ContextLabels 729, Objectives 546,
Widgets 478, Social 343, Localization 170), moved into
`src/Core/Services/UITextExtractor/` subfolder. Class is
`public static partial class` (static kept on all partials). Deck-specific
methods were folded into ContextLabels (not a separate partial — only 2
small methods) to avoid forcing the 6-partial convention onto a class that
didn't need it. Build: 0/0, tests: 105/105. **User note: 7-file output
count across splits 1-5 was coincidence, not convention — split count is
chosen per file to match natural cohesion.**

Split 7/12: **UIActivator.cs** (2745 → 1834 lines, -33%) via REAL class
extraction (not partial) plus 468 lines of dead-code removal:
- Extracted **CardTileActivator.cs** (483 lines, new standalone class at
  `src/Core/Services/CardTileActivator.cs`, sits flat next to CardDetector
  etc.) — handles deck/collection card-tile identification + activation
  (IsCollectionCard, IsDeckEntry, TrySelectDeck, GetDeckInvalidStatus,
  etc.). Call sites updated in BaseNavigator + GeneralMenuNavigator + 7
  internal calls inside UIActivator.Activate().
- Deleted 343 lines of truly dead diagnostic code in UIActivator
  (DebugInspectNavMailButton, InspectUnityEvent, InspectDelegate,
  DebugInspectDeckView — all grep-confirmed zero callers).
- Deleted 3 more dead methods in CardTileActivator after extraction
  (FindMetaCardView, TryOpenCardViewerDirectly, FindCustomButtonInHierarchy
  — also zero callers; TryActivateCollectionCard goes through
  UIActivator.SimulatePointerClick instead).
- `UIActivator.IsCustomButton` promoted from private → internal so
  CardTileActivator can call it without duplication.
- UIActivator now below 2000 lines → exits the [LARGE] category.
Build: 0/0, tests: 105/105. **Approach note: for remaining candidates,
evaluate first whether real class extraction (separate concerns) would
be better than partial split. For coherent single-concern files, the
[LARGE] tag may be acceptable and "leave alone" is a valid choice.**

Subfolder convention (established 2026-04-20): when splitting a class into
partials, group all of them in a `src/Core/Services/<ClassName>/` subfolder.
Namespace stays `AccessibleArena.Core.Services` (not updated to match path,
to avoid ripple changes at consumer sites).

Split 8/12: **CardModelProvider.cs** (2374 → 2051 lines, -14%) via REAL class
extraction (not partial) of mana-text formatting helpers:
- Extracted **ManaTextFormatter.cs** (339 lines, new standalone
  `public static class` at `src/Core/Services/ManaTextFormatter.cs`, sits
  flat next to the other providers) — handles MTGA mana notation parsing
  (ParseManaSymbolsInText, ParseBareManaSequence, ConvertManaSymbolToText,
  ConvertSingleManaSymbol, ParseManaQuantityArray, ConvertManaColorToName,
  MergeClassLevelLines). Pure string processing — no reflection state,
  no caches, no fields. Depends only on Strings.Mana* + ModSettings.
- `ParseManaQuantityArray` bumped private → internal; `MergeClassLevelLines`
  bumped private → internal (CardModelProvider still calls them).
- Side effect: the deletion of the misplaced MergeClassLevelLines method
  (which sat inside the Power/Toughness region with a corrupted `<summary>`
  block) fixed the broken FormatRarityName/GetStringBackedIntValue summary.
- Call sites updated: 10 internal in CardModelProvider + 7 external
  (AdvancedFiltersNavigator, ExtendedCardInfoProvider×2, DuelAnnouncer,
  UIElementClassifier, StoreNavigator.Details×2).
- `System.Text.RegularExpressions` using removed from CardModelProvider
  (no longer references Regex).
- CardModelProvider stays [LARGE] (2051 > 2000) but is now focused on
  reflection + card-info extraction as one coherent unit.
Build: 0/0, tests: 105/105.

Split 9/12: **WebBrowserAccessibility.cs** (2236 → 1758 lines, -21%) via REAL
class extraction of JS source constants + static script builders:
- Extracted **WebBrowserScripts.cs** (488 lines, new `internal static class`
  at `src/Core/Services/WebBrowserScripts.cs`, sits flat next to
  WebBrowserAccessibility) — 5 JS const strings (ExtractionScript,
  FindElementFunc, InstallMutationObserverScript, PollMutationScript,
  DetectCrossOriginIframesScript) + 8 static `XxxScript(...)` builders
  (Click, Focus, GetBoundingBox, SelectAll, ReadValue, AppendText,
  Backspace, Submit). All visibility bumped private → internal.
- The entire `#region JavaScript` ... `#endregion` block was a clean
  boundary — mechanical lift-and-shift, no coupling to instance state.
- 13 call sites in WebBrowserAccessibility prefixed with
  `WebBrowserScripts.` (all via `_browser.EvalJSCSP(WebBrowserScripts.Xxx`).
- WebBrowserAccessibility.cs now below 2000 → exits [LARGE] category.
Build: 0/0, tests: 105/105. **Honesty note:** file still has cleanup
smells (parallel HandleEditModeInput/HandlePassthroughEditModeInput
strategy pair, ~30 instance fields including a static reflection cache,
hand-rolled timer state machine). Those are high-level-cleanup.md
territory, not large-file-handling. The JS extraction is orthogonal
and survives any later restructuring.

Split 10/12: **MasteryNavigator.cs** (2174 → 322 core + 1080 Levels + 543
PrizeWall + 326 ConfirmationModal lines) via partial split into a
`src/Core/Services/MasteryNavigator/` subfolder (same convention used for
DuelAnnouncer / BaseNavigator / BrowserNavigator):
- **MasteryNavigator.cs** (322, core) — mode routing, lifecycle
  (OnActivated/OnDeactivating/OnSceneChanged), Update loop with
  confirmation-modal polling, mode-aware announcements, HandleMasteryInput
  router (→ HandleLevelInput / HandlePrizeWallInput).
- **MasteryNavigator.Levels.cs** (1080) — Levels mode. Owns
  ProgressionTracksContentController + RewardTrackView reflection
  (~60 FieldInfo/PropertyInfo/MethodInfo handles), LevelData/TierReward/
  ActionButton nested types, BuildLevelData / ExtractLevelData /
  BuildActionButtons / InsertStatusItem, localization helpers
  (ResolveLocString / GetLocalizedText / ResolveTrackTitle), all level +
  tier announcements, page sync, HandleLevelInput.
- **MasteryNavigator.PrizeWall.cs** (543) — PrizeWall mode. PrizeWallItemData
  struct, ContentController_PrizeWall + StoreItemBase reflection,
  FindPrizeWallController / IsPrizeWallOpen, DiscoverPrizeWallItems /
  ExtractPrizeWallItemData, HandlePrizeWallInput, AnnouncePrizeWallItem,
  ActivatePrizeWallItem.
- **MasteryNavigator.ConfirmationModal.cs** (326) — StoreConfirmationModal.
  Modal state + reflection (4 purchase-button field names, close method,
  label/item/product-list containers), discovery of text blocks + purchase
  buttons + synthetic cancel option, AnnounceConfirmationModal,
  HandleConfirmationModalInput, MoveModalElement, DismissConfirmationModal.
  `_confirmationModalGameObject` set by PrizeWall partial in
  DiscoverPrizeWallItems, polled by core Update loop.
Partials share private state by virtue of being one class (no visibility
changes needed). Localization helpers stay in Levels (only consumer).
Mechanical split — no behavior changes. Build: 0/0, tests: 105/105.
MasteryNavigator now out of [LARGE] (largest partial 1080 < 1500).

Split 11/12 decision: **GroupedNavigator.cs** (2167 lines) — left alone per
user direction. Single-mode class with tightly-coupled state (_groups,
_currentGroupIndex, _currentElementIndex, _navigationLevel, subgroup
tracking); partial splits would be cosmetic only. 8% over threshold.

Split 12/12: **PlayerPortraitNavigator.cs** (2151 → 335 core + 429 Properties
+ 371 Emotes + 543 Life + 540 Timer lines) via partial split into a
`src/Core/Services/PlayerPortraitNavigator/` subfolder:
- **PlayerPortraitNavigator.cs** (335, core) — NavigationState state machine
  (Inactive/PlayerNavigation/EmoteNavigation), Activate/Deactivate,
  OnFocusChanged + IsPlayerZoneElement, HandleInput top-level router,
  EnterPlayerInfoZone/ExitPlayerInfoZone focus management,
  FindPlayerZoneFocusElement (uses FindAvatarView from Emotes),
  HandlePlayerNavigation.
- **PlayerPortraitNavigator.Properties.cs** (429) — PlayerProperty enum
  (Life/Effects/Timer/Timeouts/Wins/Rank) + PropertyCount, GetPropertyValue
  dispatch, IsPropertyVisible + HasEffectsContent filtering,
  FindNextVisibleProperty, GetWinCount (stub=0), GetPlayerRank (handles
  Mythic placement + tier-based ranks), GetMatchupText, GetPlayerUsername,
  rank reflection cache + InitializeRankReflection.
- **PlayerPortraitNavigator.Emotes.cs** (371) — _emoteButtons + avatar
  reflection cache. HandleEmoteNavigation (modal, blocks all keys),
  OpenEmoteWheel / CloseEmoteWheel, DiscoverEmoteButtons +
  SearchForEmoteButtons (recursion depth-5), ExtractEmoteName variants,
  SelectCurrentEmote (UIActivator.SimulatePointerClick),
  InitializeAvatarReflection, FindAvatarView, TriggerEmoteMenu.
- **PlayerPortraitNavigator.Life.cs** (543) — MtgEntity/MtgPlayer reflection
  cache (Counters/Designations/Abilities/DungeonState). AnnounceLifeTotals
  (L key), BuildLifeWithCounters, GetLifeTotals + GetPlayerLife (via
  GameManager → CurrentGameState/LatestGameState → LocalPlayer/Opponent),
  GetMtgPlayer, InitializeEntityReflection (walks base type hierarchy for
  MtgEntity fields), GetPlayerCounters + FormatCountersForLife,
  GetPlayerEffects (designations + abilities + dungeon), FormatDesignation.
- **PlayerPortraitNavigator.Timer.cs** (540) — Timer element cache +
  LowTimeWarning subscription state + MtgTimer and LowTimeWarning
  reflection caches. DiscoverTimerElements, GetTimerText/FormatTimerText,
  GetTimeoutCount, GetMatchTimerInfo, GetProperty<T> generic helper
  (type param T shadows the `using T = GameTypeNames` alias in method body
  — legal but worth noting), AnnounceTimer (E/Shift+E entry),
  GetTimerFromModel (MatchTimer path), GetRopeTimerFromModel (LTW path),
  InitializeLtwReflection / InitializeMtgTimerFromLtw / InitializeMtgTimerReflection
  (LTW fallback for casual Brawl), FormatSecondsToReadable,
  Subscribe/UnsubscribeLowTimeWarnings (UnityEvent<bool> listeners).
Partials share private state by virtue of being one class. Mechanical split
— no behavior changes. Build: 0/0, tests: 105/105.
PlayerPortraitNavigator now out of [LARGE] (largest partial 543 < 1500).

Remaining [LARGE] files:
- [ ] CardModelProvider.cs (2051) — above 2000 but now coherent; may be "leave alone"
- [ ] BrowserNavigator.cs (2220) — already split in round 2, core partial stayed [LARGE]
- [ ] GroupedNavigator.cs (2167) — left alone (split 11 user decision)

## High-Level Cleanup (2026-04-20) — Done

All 8 items (Q1–Q4 quick wins, M1–M4 medium items) completed. Each landed
as a single commit. Build 0/0, test suite grew from 105 → 124 (all green).

- **Q1** `refactor(constants)`: centralized hardcoded controller type-name
  strings into `GameTypeNames` (e.g. `T.CustomButton`, `T.DuelSceneCDC`).
- **Q2** `refactor(focus)`: extracted `ZoneNavigator.ClearEventSystemSelection`
  helper. Property-name-shadowing bug fixed via `global::` qualifier.
- **Q3** `refactor(patches)`: UXEventQueuePatch 100th-event log lines now
  gated behind `DebugConfig.LogIf(LogPatches, ...)`.
- **Q4** `refactor(card-state)`: collapsed 7 per-type accessors in
  `CardStateProvider` into two generics (`GetValueFromInstance<T>` and
  `GetListFromInstance<T>`).
- **M1** `docs(browser-nav)`: added a clarifier comment on
  `_currentHighlightMethod` (scene-permanent, MethodInfo bound to type
  not instance — intentionally not reset in OnSceneChanged).
- **M2** `test(navigator-manager)`: 16 new NSubstitute-based tests covering
  priority-descending sort, preemption, self-deactivation, OnSceneChanged
  notification, RequestActivation failure-rollback.
- **M3** `test(locales)`: 3 audits catching en.json↔code drift and
  locale↔locale drift. Test-found cleanup: 6 dead keys removed,
  13 player-zone/dungeon keys back-filled into 10 locales. Follow-up
  commit replaced the English placeholders with real translations (WotC
  official MTG terms: Monarch, Day/Night, City's Blessing, Dungeon, Speed).
- **M4** `docs`: `_pending*` producer/consumer/lifetime block on
  `GroupedNavigator`; scope boundary on `UIActivator` vs `CardTileActivator`;
  3-step "how to add a Strings key" workflow on `Strings.cs`.

Branch state: `claude-mod-cleanup-round2` has 26 commits ahead of main;
working tree clean. Ready to merge or proceed to `low-level-cleanup.md`.

## Low-Level Cleanup (2026-04-20) — Done

All 15 items (Q1–Q10 quick wins, M1–M5 medium items) completed. Each
landed as a single commit. Build 0/0, tests 122/122.

Quick wins (Q1–Q10) — primarily dead-code removal and tiny-scope cleanups:
- **Q1** `refactor(ui-text)`: removed three dead public `UITextExtractor`
  methods (grep-confirmed zero callers).
- **Q2** `refactor(cleanup)`: removed dead navigator helper methods across
  several navigators.
- **Q3** `refactor(cleanup)`: removed dead card/deck accessor methods.
- **Q4** `refactor(general-nav)`: dropped dead helpers plus a write-only
  mail-letter id field.
- **Q5** `refactor`: dropped write-only fields and the unused `Reset`
  helper across navigators.
- **Q6** `refactor(player-portrait-timer)`: dropped dead timer helpers.
- **Q7** `refactor`: removed dead Tolk/Debug/Locale helpers.
- **Q8** `refactor(partials)`: trimmed inherited `using` directives across
  the 24 partial-class files produced by round-2 splits.
- **Q9** `refactor`: removed stranded `// DEPRECATED` comments still
  referencing classes that had been deleted.
- **Q10** `refactor(browser-nav)`: extracted `TryGetCurrentBrowser` helper
  (duplicate fetch pattern consolidated).

Medium items (M1–M5) — reflection consolidation and topical extraction:
- **M1** `refactor(panel-state-patch)`: extracted Harmony wire-up and the
  `FirePanelStateChange` event helper. Kept dual-dispatch postfixes
  untouched (their secondary PlayBlade-name dispatch doesn't map to the
  single helper — noted in commit message).
- **M2** `refactor(panel-state-patch)`: extracted SocialUI Tab-block
  prefix/postfix pattern via `ShouldBlockSocialUITabToggle`. File
  shrank 1023 → 217 lines.
- **M3** `refactor(grouped-navigator)`: consolidated restore/cycle helpers
  (`ComputeCyclableGroupIndices`, `CycleGroup`), routed 5
  pending-restore-clearing sites through `ClearPendingGroupRestore()`,
  dropped dead `JumpToGroupByName`/`GetGroupByName`/`FindGroups` API.
- **M4** `refactor(card-text-provider)`: deduped `Find*Provider`
  reflection walks via `FindStringFromUintMethod`,
  `SearchCardDatabaseProviders`, `ExtractProviderFromCardDb`. File
  shrank 294 lines.
- **M5** `refactor(extended-info-navigator)`: collapsed `Open(GameObject)`
  and `Open(uint)` into thin dispatchers over shared `BuildAndOpen`.
  Normalized rules-line dedupe + keyword-dedupe across both paths
  (PAPA-fallback concern applies to both since `GetKeywordDescriptions`
  delegates GameObject→grpId).

Branch state: `claude-mod-cleanup-round2` now at 40+ commits ahead of main;
working tree clean. Ready to merge or proceed to `finalization.md`.

## Round 3 — AI-Bloat Audit (ongoing, 2026-04-21)

New ad-hoc prompt `prompts/ai-bloat-audit.md` added to the upstream repo —
targets AI-authored bloat patterns: dead public method + helper cascades,
stale reflection init, near-duplicate methods, tiny single-method partials,
wrapper indirection, defensive checks for impossible conditions.

Done before this session (ad-hoc commits):
- **5dc1398** MenuDebugHelper — dropped unused `DumpGameObjectDetails` pair.
- **2de03e0** EventAccessor — dropped dead Color Challenge methods,
  deduped `Find*Controller` into `FindCachedController` helper.
- **12f1ca4** Folded single-method partials back into parents
  (BaseNavigator.Chat, BrowserNavigator.Reflection).

Audit run 2026-04-21 — subagents swept Accessors, CardModelProvider /
CardStateProvider / CardTextProvider / ExtendedCardInfoProvider,
GroupedNavigator, all 8 partial-class subfolders (BaseNavigator,
BrowserNavigator, DuelAnnouncer, GeneralMenuNavigator, StoreNavigator,
UITextExtractor, MasteryNavigator, PlayerPortraitNavigator), plus
WebBrowserAccessibility, UIActivator, CardTileActivator, AdvancedFilters,
Draft, PreBattle, ExtendedInfo, Overlay, Sideboard, DeckInfoProvider,
DeckCardProvider, SettingsMenuNavigator, UIElementClassifier.

Raw subagent findings validated via grep; dropped:
- UIElementClassifier `HasMainButtonComponent` / `IsCustomButtonInteractable`
  / `IsCarouselElement` — all have real internal callers
  (UIElementClassifier.cs:424, 640, 842, 844).
- DeckCardProvider `GetDeckListCardInfo` / `GetSideboardCardInfo` — called
  by their own Is* wrappers + other methods.

Items landed this session:
- **c0638e8** RecentPlayAccessor — dropped dead `GetIsInProgress` (38 lines).
- **07d1117** GroupedNavigator — dropped 7 dead public methods +
  `_pendingFirstFolderEntry` field + reader branch (lines 1062-1079) +
  `AutoEnterIfSingleItem` (only caller was dead `FilterToOverlay`).
  146 lines total.

Items deferred (open design questions):
- BrowserDetector `EnableDebugForBrowser` / `DisableDebugForBrowser` /
  `DisableAllDebug` — zero static callers but documented in CLAUDE.md as
  a developer instrumentation API meant for manual invocation from console
  or debugger. User decides whether to keep as documented API or drop as
  YAGNI. 15 lines if removed.

Files audited and flagged as clean (no material bloat):
CardPoolAccessor, CardModelProvider, CardStateProvider, CardTextProvider,
ExtendedCardInfoProvider, all 8 partial-class subfolders,
WebBrowserAccessibility, AdvancedFiltersNavigator, UIActivator,
CardTileActivator, DraftNavigator, PreBattleNavigator,
ExtendedInfoNavigator, OverlayNavigator, SideboardNavigator,
DeckInfoProvider, SettingsMenuNavigator.

## Scratchpad Files
- `current_status.md` — this file
- `code-index/` — one markdown-per-source-file index built 2026-04-19.
  Covers all 115 src/ and 11 tests/ .cs files + 7 `src/Core/Services/old/`
  archived files. Commit 633bee0. Downstream prompts (`large-file-handling`,
  `high-level-cleanup`) should query this index to find declarations without
  paging full sources through context.

## Refactoring Prompts Repo
Cloned at `./llm-mod-refactoring-prompts/` (gitignored). At commit `abe0259
Integrate claude feedback`, synced with origin/main on 2026-04-19 — no
upstream changes since round 1.
