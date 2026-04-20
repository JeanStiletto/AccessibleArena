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
- [ ] large-file-handling.md  (in progress — 7/12 done)
- [ ] input-handling.md          (pre-marked; just read "Up Next" and move on)
- [ ] string-builder.md          (pre-marked; just read "Up Next" and move on)
- [ ] high-level-cleanup.md
- [ ] low-level-cleanup.md
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

Remaining candidates (still >2000 lines):
- [ ] CardModelProvider.cs (2374)
- [ ] WebBrowserAccessibility.cs (2236)
- [ ] MasteryNavigator.cs  (2174)
- [ ] GroupedNavigator.cs  (2167)
- [ ] PlayerPortraitNavigator.cs (2151)

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
