# Game Update Checklist

What to run after MTG Arena updates, so regressions surface here instead of in a user report.

WotC publishes no developer changelog. The support-site patch notes are player-facing and
never mention the UI internals the mod binds to, so the real signal has to come from the
game's own assemblies.

## Why things break silently

The mod binds to the game two ways:

- **Compile-time references** (`Core.dll`, `Assembly-CSharp.dll`, ...). A removed or renamed
  public API breaks the build, so `dotnet build` already catches these.
- **Reflection by string name** — around 780 member names and 200 type names. Nothing catches
  a rename here. The affected feature just stops working, usually without an exception.

The second group is what this checklist covers.

## The run order

### 1. Build and test

```
dotnet build src/AccessibleArena.csproj
dotnet test tests/AccessibleArena.Tests
```

A build failure is the cheapest possible signal: it means a game API the mod calls directly
changed shape.

### 2. Regression check against the stored baseline

```
powershell -NoProfile -File tools\check-game-update.ps1
```

Reads the game assemblies with Mono.Cecil (no game launch needed) and reports, in order:

- **0. Resolution failures in the last game session** — lines the mod itself logged when a
  reflection handle would not resolve. Runtime-only types show up here and nowhere else.
- **1. Names that used to resolve and no longer do** — the core regression list, with every
  call site in `src/`.
- **2. New names in the mod that do not resolve** — usually a typo in code written since the
  last baseline.
- **3. Names that started resolving** — informational.
- **4. Tracked game types that disappeared.**
- **5. Tracked game types whose signatures changed** — added and removed members per type.
  This is the section that catches a method gaining a parameter, which the name checks miss.

Exit code is 1 when something needs attention, so it can gate a release script.

### 3. First run after an update, when the baseline is older than the update

The baseline records the *last known good* game version. If Arena updated before the check ran,
compare against the decompiled sources instead — they were captured from the previous build:

```
powershell -NoProfile -File tools\check-game-update.ps1 -FromDecompiled
```

This is type-scoped rather than name-global, so it is the sharpest mode available. Each hit is
annotated with where the member lives now:

- `gone from every game type` — a real removal, fix the call site.
- `still exists on: A, B, C` — the decompiled file carried more than one type, or the member
  moved. Check the call site before changing anything.

### 4. Fix, then re-baseline

Once the mod is healthy again:

```
powershell -NoProfile -File tools\decompile-all.ps1        # refresh the decompiled reference
powershell -NoProfile -File tools\check-game-update.ps1 -UpdateBaseline
```

Both write to `llm-docs/api-baseline/` and `llm-docs/decompiled/`, which are gitignored:
they are derived game metadata and stay local. A fresh clone has no baseline until the first
`-UpdateBaseline` run.

### 5. Optional context: player-facing patch notes

```
powershell -NoProfile -File tools\patch-notes.ps1              # list recent releases
powershell -NoProfile -File tools\patch-notes.ps1 -Latest      # print the newest in full
powershell -NoProfile -File tools\patch-notes.ps1 -Version 2026.62.10
```

The "Notable Bug Fixes" and "Known Issues" sections occasionally explain a behaviour change
that would otherwise look like a mod bug. Notes are published days after the client build, and
sometimes not at all for a hotfix, so never wait on them.

## Known limits

- The name check is global: a member counts as present if *any* game type declares it. A field
  moved from one type to another still reads as fine. `-FromDecompiled` and the section 5
  signature diff cover that gap for types that are tracked.
- Short type names collide. The checker prefers the top-level type over a nested one, but a
  bare name that matches several top-level types resolves to the first match.
- Coverage stops where reflection becomes dynamic — `GetType().GetProperty(someVariable)` is
  invisible to a static scan. Section 0's log scan is the backstop for those.
