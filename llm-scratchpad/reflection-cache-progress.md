# Reflection Cache Migration — Progress

Prompt: `llm-mod-refactoring-prompts/prompts/reflection-cache.md`
Started: 2026-04-21

## Scope — measured, not estimated

Actual counts from grep on `src/`:

- **19** methods matching `Initialize\w*Reflection\s*\(`
- **12** methods matching `Ensure\w*(Reflect|Cached)\s*\(`
- **428** private `FieldInfo`/`PropertyInfo`/`MethodInfo` fields across **44 files**
- **16** files with a `_reflectionInitialized`-style flag

So: ~30 init methods across ~25 reflection-heavy files. Matches the prompt's "~30 reflection-heavy services" estimate.

Explicit exclusions (per prompt):
- `src/Patches/` (Harmony, different lifecycle) — `TimerPatch.cs` has 2 FieldInfo fields
- `AccessibleArenaMod.OnInitializeMelon()` one-time reflection
- `ReflectionUtils.FindType(fullName)` (type lookup, not field lookup)
- Uncached per-frame `GetField` calls (different prompt)

## Phase 1 — Design Decisions (approved by user 2026-04-21)

All four open questions from the draft proposal resolved. Fresh session starting
Phase 2 should treat these as frozen unless the pilot reveals a gap.

### Decision 1 — API style: strongly-typed `THandles` class per cache

Each cache declares a small private class that names its handles. The cache is
generic over that class. IDE autocomplete + compile-time name checking + rename
refactor safety. User accepted the ~5-lines-per-cache overhead explicitly
because it produces compile errors instead of runtime nulls when game fields
get renamed between patches — critical for debugging game-version drift.

Rejected: fluent builder with string-keyed handle lookup.

### Decision 2 — One cache per file unless multiple independent init methods today

Do NOT over-split. Mastery.Levels's 115-line `EnsureReflectionCached` stays
as ONE cache with ~30-field Handles class grouped by type cluster via
section comments (`// Controller`, `// View`, `// PageLevels`, `// TrackLevel`,
`// Reward`, `// LocString`, `// DataProvider`). Splitting into 6 caches
would scatter the chained type-discovery logic (view type from
`controller.ActiveView.FieldType`, reward type from `FindType(...)`, etc.)
across multiple read sites — making it harder to follow, not easier.

**Rule of thumb for the sweep:** split a cache only when the existing file
has multiple independent init methods writing to separate handle sets
(e.g., PPN.Timer has Ltw + MatchTimer + MtgTimer — three separate caches).
Do not split a single cache that is already coherent.

Applies to the 12 `EnsureReflectionCached` files too: `Store`, `Codex`,
`Profile`, `ChatMessageWatcher`, `ChatNavigator`, `DuelChatNavigator`,
`Achievements`, `MasteryNavigator.PrizeWall`, `MasteryNavigator.ConfirmationModal`,
`AchievementsNavigator`, etc. Keep one cache each.

### Decision 3 — Multi-source MtgTimer split (the one genuine multi-source case)

PPN.Timer's current `InitializeMtgTimerReflection` + `InitializeMtgTimerFromLtw`
both write to the same static fields today. Migrate as TWO separate caches
that don't share state:

- `MatchTimerHandles`: `_matchTimer`, `_timeRunning` (read off a MatchTimer instance)
- `MtgTimerHandles`: `RemainingTime`, `Running` (read off an MtgTimer instance —
  regardless of how we obtained the Type)

Fallback at read site becomes:

```csharp
if (!_mtgTimerCache.IsInitialized && matchTimer != null
    && _matchTimerCache.EnsureInitialized(matchTimer.GetType()))
    _mtgTimerCache.EnsureInitialized(_matchTimerCache.Handles.MatchTimer.FieldType);
if (!_mtgTimerCache.IsInitialized && _ltwCache.IsInitialized)
    _mtgTimerCache.EnsureInitialized(_ltwCache.Handles.ActiveTimer.FieldType);
if (!_mtgTimerCache.IsInitialized) return null;
```

Fallback is now obvious, not buried in a second init method.

### Decision 4 — Walk-hierarchy helper: `ReflectionWalk` companion

Add `src/Core/Utils/ReflectionWalk.cs` with three static methods:

```csharp
public static FieldInfo    FindField   (Type t, string name, BindingFlags f);
public static PropertyInfo FindProperty(Type t, string name, BindingFlags f);
public static MethodInfo   FindMethod  (Type t, string name, BindingFlags f);
```

Each walks `type.BaseType` chain until it finds a match (or null).

Needed because `type.GetField(name, NonPublic | Instance)` does NOT inherit —
private base-class fields return null without a walk. Currently only
`PlayerPortraitNavigator.Life.cs` walks a hierarchy (MtgEntity base,
MtgPlayer derived), but Phase 3 will likely uncover more. If at Phase 4
fewer than 2 callers actually use it, delete the companion.

Replaces the current 10-line manual `while (type != null)` loop with one
line per walked field inside the builder lambda.

### Decision 5 — Log shape: preserve existing + enumerate null handles on failure

Three log lines emitted by the helper:

- **Success:** `Log.Msg(tag, $"{subject} reflection initialized")` — exactly matches
  current `[Tag] <subject> reflection initialized` shape the prompt requires preserved.
- **Validator failure (required handle null — typical game-version drift):**
  `Log.Warn(tag, $"Could not resolve required handles for {subject}: <names>")`.
  The `<names>` list is produced by reflecting over the `THandles` class
  after the builder runs and listing every FieldInfo/PropertyInfo/MethodInfo
  field whose current value is null. This is MORE informative than today's
  logs (most current init methods say "Could not find X or Y" with hardcoded
  names; Mastery uses a per-type boolean dump). The helper produces this
  uniformly for every cache without per-file code.
- **Builder exception:** `Log.Error(tag, $"Failed to initialize {subject} reflection: {ex.Message}")`.

Rationale: "Could not resolve required handles for Mastery: TrackLevelType,
ServerLevelField" tells a future debugger exactly what broke. Generic
"... for Mastery" would force them to open the builder and check each
field manually. 10 extra lines in the helper.

### Grep gap flagged for Phase 4

The prompt's suggested survivor pattern `Initialize.*Reflection` misses the
12 `Ensure.*Cached` methods. Phase 4 survivor audit MUST use both patterns:

```
rg -n 'private (static )?void (Initialize\w*Reflection|Ensure\w*(Reflect|Cached))' src/
```

Plus the raw-call audit: `GetField(|GetProperty(|GetMethod(` outside helper
builder lambdas.

## Starting notes for a fresh session (Phase 2)

Read in order:
1. `llm-mod-refactoring-prompts/prompts/reflection-cache.md` (the prompt)
2. `llm-scratchpad/reflection-cache-progress.md` (this file — design decisions above)
3. `llm-scratchpad/current_status.md` (round-2 overall status)
4. Then start Phase 2: implement `src/Core/Utils/ReflectionCache.cs` + `ReflectionWalk.cs`,
   write NUnit tests, pilot-migrate `PlayerPortraitNavigator.Timer.cs`, build+test,
   ask user to smoke-test E/Shift+E timer announcements. Commit pilot separately.

Do NOT re-do the enumeration of variations. Do NOT re-ask the four API questions.
Decisions above are final unless the pilot itself reveals an API gap.

## Checklist

- [x] Phase 1: API design + user approval (2026-04-21)
- [x] Phase 2: helper + tests + Timer pilot (2026-04-21)
  - Helper + tests: commit `37b22fc` (28 new tests, 150/150 green)
  - Timer pilot: 3 init methods → 3 caches (Ltw/MatchTimer/MtgTimer per
    Decision 3); file 459 → 407 lines (−52, −11%). Awaiting user smoke
    test of E / Shift+E timer announcements in a duel before Phase 3.
- [ ] Phase 3 files (batch order cheapest first):
  - [x] Accessors: `RecentPlayAccessor.cs`, `CardPoolAccessor.cs`, `EventAccessor.cs`
        (committed, smoke-tested OK 2026-04-21)
  - [x] Providers (partial): `DeckInfoProvider.cs` (commit f14c21a, −52),
        `CardStateProvider.cs` model-props cluster only (commit 3adcc1c, −8).
        SKIPPED — not a fit for `ReflectionCache<THandles>`:
        `CardTextProvider.cs` (runtime scene-discovered provider instance + method),
        `ExtendedCardInfoProvider.cs` (10 `_xxxProviderSearched` scene-discovery fields),
        `CardModelProvider.cs` (`_modelPropertyCache` dictionary, dynamic name-keyed),
        `DeckCardProvider.cs` (single `_showUnCollectedField` lazy one-off).
        CardStateProvider's `_attachedToIdField`, `_zoneTypePropCached`,
        and `_instanceMemberCache` left alone (different lazy patterns).
        Awaiting user smoke test of deck builder (card count / mana curve /
        type breakdown) + zone type detection before Phase 3 navigators.
  - [ ] Navigator partials (one subsystem each):
    - [x] `PlayerPortraitNavigator.Life.cs` (done earlier, entityCache + ReflectionWalk)
    - [x] `PlayerPortraitNavigator.Emotes.cs` (done earlier, avatarCache)
    - [x] `PlayerPortraitNavigator.Properties.cs` (pending commit — 3-chain rank cache)
    - [ ] `MasteryNavigator.Levels.cs` (~60 handles, chained discovery)
    - [x] `MasteryNavigator.PrizeWall.cs` (commit bdd1a0f)
    - [x] `MasteryNavigator.ConfirmationModal.cs` (commit bdd1a0f)
    - [x] `BrowserNavigator.Keyword.cs`, `BrowserNavigator.OrderCards.cs` (commit 02b2bf2)
    - [x] `DuelAnnouncer.Commander.cs`, `DuelAnnouncer.NPE.cs` (commit 6b7d1af)
    - [x] `UITextExtractor.Localization.cs`, `UITextExtractor.Objectives.cs` (commit 33c4809)
  - [ ] Other navigators: `ChooseXNavigator`, `HotHighlightNavigator`, `SpinnerNavigator`,
        `SpinnerNavigator`, `ManaColorPickerNavigator`, `ProfileNavigator`, `AchievementsNavigator`,
        `CodexNavigator`, `ChatMessageWatcher`, `ChatNavigator`, `DuelChatNavigator`,
        `DraftNavigator`, `SideboardNavigator`, `BoosterOpenNavigator`, `StoreNavigator`,
        `ChallengeNavigationHelper`, `PriorityController`
- [ ] Phase 4: survivor audit (grep `Initialize.*Reflection` + `Ensure.*Cached` + raw `GetField` etc.), final line count

## Notes

Survivor audit grep patterns must include BOTH `Initialize.*Reflection` AND
`Ensure.*(Reflect|Cached)` — the prompt's suggested pattern misses the 12 Ensure-style
methods. Add to Phase 4 checklist.
