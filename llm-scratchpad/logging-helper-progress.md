# Logging Helper Migration — Progress Tracker

Branch: `claude-mod-cleanup-round2`
Prompt: `llm-mod-refactoring-prompts/prompts/logging-helper.md`

## Phase checklist
- [ ] Phase 1: API design (awaiting user approval)
- [ ] Phase 2: helper + unit tests
- [ ] Phase 3: migrate files (list populated after Phase 1 lands)
- [ ] Phase 4: survivor audit + final count

## Phase 1 — shape analysis (done 2026-04-21)

### Inventory
- 1,883 call sites across 122 files match `MelonLogger.Msg|Warning|Error|DebugConfig.LogIf`.
- `Patches/` accounts for 111 sites — **skipped per prompt** ("leave alone").
- `src/Core/Services/old/` accounts for ~40 sites — excluded from compile; skipped.
- Migration target: ~1,700 sites across ~110 files.

### Existing structures (critical — do not duplicate)
- **`DebugConfig`** (`src/Core/Services/DebugConfig.cs`): already owns the logging surface
  - `DebugConfig.Log(tag, message)` — unconditional info, formats `[tag] message`, pushes to ring buffer
  - `DebugConfig.LogIf(flag, tag, message)` — gated info, same formatting + ring buffer
  - Category flags: `LogNavigation`, `LogPanelDetection`, `LogFocusTracking`, `LogCardInfo`,
    `LogActivation`, `LogAnnouncements`, `LogPatches`, `LogPanelOverlapDiagnostic`
  - Master toggle: `DebugEnabled` (true in production — kill switch only)
  - 20-entry ring buffer → `GetRecentEntries()` feeds Shift+F12 playback
  - **Missing:** no `Warn` / `Error` severity. ~250 `MelonLogger.Warning` and ~200 `MelonLogger.Error`
    call sites bypass it entirely today.
- **`MenuDebugHelper`** — already routes through `DebugConfig.LogIf`. Not a logging API; a UI-tree dumper. No change needed.

### Shape distribution across the 1,700 migratable sites
1. **Prose + interpolation** — dominant shape.
   - `$"[BattlefieldNavigator] Refreshed {row}: {before} -> {after} cards"`
   - `$"[BrowserNavigator] Clicked {buttonName}: '{clickedLabel}'"`
   - `$"[Mastery] Built {count} levels, current={cur}, curLevelIdx={idx}, track={name}"`
2. **Pure prose** — ~30% of sites.
   - `"[BrowserNavigator] AssignDamage: No browser ref for submit"`
   - `"[PlayerPortrait] MtgTimer reflection initialized"`
3. **Error-with-message** — ~250 sites, all identical shape.
   - `MelonLogger.Error($"[X] Y failed: {ex.Message}")` — `ex.Message` only, **never** `StackTrace`.
4. **True key=value** — <5% of sites.
   - `$"[Mastery] Data provider: curLevelIndex={i}, trackName={t}"`

### Verdict on the prompt's strawman API
The strawman `Log.Event(tag, verb, params (string,object)[] kvs)` **does not match the codebase shape**.
- 95% of call sites are prose; forcing them into `verb` + key/value would either mangle messages
  into awkward keys (`("row", row), ("before", before), ("after", after)`) or jam the whole
  message into one `("msg", "...")` pair which defeats the helper.
- The constraint "byte-identical output" is incompatible with the strawman anyway — prose
  `"Refreshed X: Y -> Z cards"` cannot be reproduced from `verb="refreshed"` + kvs without
  a per-site template string (at which point you've just built string interpolation again).
- The real redundancy to kill is the **`[Tag]` bracket framing**, not the message body.

### Proposed API — extend `DebugConfig` and rename it to `Log`

`src/Core/Services/DebugConfig.cs` → `src/Core/Utils/Log.cs`, class renamed `DebugConfig` → `Log`.
All existing functionality preserved. Category flags stay as public static props on `Log`
(`Log.LogNavigation`, etc.). Master toggle `DebugEnabled` → `Log.Enabled`.

Additions:
```csharp
public static class Log
{
    // Existing (renamed from DebugConfig):
    public static bool Enabled { get; set; } = true;
    public static bool LogNavigation { get; set; } = true;
    // ... other category flags unchanged ...

    // Unconditional info — current DebugConfig.Log
    public static void Msg(string tag, string message);

    // Gated info — current DebugConfig.LogIf
    public static void MsgIf(bool flag, string tag, string message);

    // NEW — routes through MelonLogger.Warning. Also adds to ring buffer.
    public static void Warn(string tag, string message);
    public static void Warn(string tag, string message, Exception ex);  // appends ": {ex.Message}"

    // NEW — routes through MelonLogger.Error. Also adds to ring buffer.
    public static void Error(string tag, string message);
    public static void Error(string tag, string message, Exception ex); // appends ": {ex.Message}"

    // Ring buffer (unchanged):
    public static string[] GetRecentEntries(int count = 5);
}
```

### Call-site before/after

#### Pure prose — info
```csharp
// Before:
MelonLogger.Msg("[PlayerPortrait] MtgTimer reflection initialized");
// After:
Log.Msg("PlayerPortrait", "MtgTimer reflection initialized");
```
Output unchanged: `[PlayerPortrait] MtgTimer reflection initialized`

#### Interpolated prose — info
```csharp
// Before:
MelonLogger.Msg($"[BattlefieldNavigator] Refreshed {_currentRow}: {oldCount} -> {newCount} cards");
// After:
Log.Msg("BattlefieldNavigator", $"Refreshed {_currentRow}: {oldCount} -> {newCount} cards");
```
Output unchanged.

#### Gated info (already uses DebugConfig.LogIf)
```csharp
// Before:
DebugConfig.LogIf(DebugConfig.LogNavigation, "PlayerPortrait", $"Found local timer: {objName}");
// After:
Log.MsgIf(Log.LogNavigation, "PlayerPortrait", $"Found local timer: {objName}");
```
Output unchanged.

#### Warning
```csharp
// Before:
MelonLogger.Warning($"[PlayerPortrait] Failed to init MtgTimer from LTW: {ex.Message}");
// After:
Log.Warn("PlayerPortrait", "Failed to init MtgTimer from LTW", ex);
```
Output unchanged (helper appends `": " + ex.Message`).

#### Error
```csharp
// Before:
MelonLogger.Error($"[BrowserNavigator] WorkflowReflection error: {ex.Message}");
// After:
Log.Error("BrowserNavigator", "WorkflowReflection error", ex);
```
Output unchanged.

#### Harmony patch site (for validating API works statically — NOT migrated per prompt)
```csharp
// Before (PanelStatePatch.cs:103):
MelonLogger.Warning($"[PanelStatePatch] Failed to patch {label}: {ex.Message}");
// Would become (but we don't migrate patches):
Log.Warn("PanelStatePatch", $"Failed to patch {label}", ex);
```
Works the same way — static API, no instance dependency. (Staying with MelonLogger per prompt.)

### Behavior changes to confirm with user
1. **Ring buffer coverage expands.** Today only `DebugConfig.Log`/`LogIf` feed the ring buffer
   (~450 sites). After migration, `Log.Msg`/`.Warn`/`.Error` all feed it (~1,700 sites).
   Shift+F12 playback will now include Warning/Error entries that previously went straight
   to MelonLogger. **This is information gain for the bug-report workflow.**
2. **Nothing else changes.** Output bytes identical. Gating preserved. Category flags unchanged.
   No stack traces added (current behavior: `ex.Message` only).

### Alternatives considered & rejected
- **New `Log` class alongside `DebugConfig`** — parallel structure. Rejected (user feedback).
- **Instance method `this.LogMsg(...)` on `BaseNavigator`** — 351 sites already use
  `[{NavigatorId}]`, so this saves ~12 chars per site there. But non-navigator callers
  (providers, accessors, patches) need the static API anyway. Adding an instance variant
  later is a non-breaking follow-up; not worth the API surface now.
- **`Log.Event(tag, verb, kvs)`** — doesn't match the prose-dominant shape. Would violate
  the byte-identical constraint. Rejected in shape analysis above.

## Phase 3 file list
_Populated after user approves API._
