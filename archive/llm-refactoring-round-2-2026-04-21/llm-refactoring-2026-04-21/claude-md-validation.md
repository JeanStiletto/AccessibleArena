# CLAUDE.md Validation Report

Validated: 2026-04-19
Previous validation: 2026-03-05

---

## 1. Verifiable Factoids List

### Game & Framework

| Claim | Status | Notes |
|---|---|---|
| Game: Magic: The Gathering Arena (Unity, .NET 4.7.2) | OK | `src/AccessibleArena.csproj` targets `net472` |
| Mod loader: MelonLoader, entry point `AccessibleArenaMod : MelonMod` | OK | Confirmed in `src/AccessibleArenaMod.cs` line 16 |
| Patching: Harmony 2.x for IL interception (**4** patch classes in `src/Patches/`) | **STALE** | There are **5** patch classes: `EventSystemPatch`, `KeyboardManagerPatch`, `PanelStatePatch`, `TimerPatch`, `UXEventQueuePatch` |
| Screen reader: Tolk library (P/Invoke to native DLL) | OK | Confirmed via `[DllImport("Tolk.dll")]` in `src/ScreenReaderOutput.cs` |
| Tolk supports NVDA/JAWS/Narrator | UNCERTAIN | Tolk's supported reader list depends on its native DLL version; can't verify without game install, but the claim is consistent with the library's known capabilities |
| Game assemblies at `<game>/MTGA_Data/Managed/` — Core.dll most types, Assembly-CSharp.dll some UI types | OK | Matches `src/AccessibleArena.csproj` `MtgaManagedPath` |

### Documentation Directories

| Claim | Status | Notes |
|---|---|---|
| `docs/GAME_ARCHITECTURE.md` | OK | File exists |
| `docs/MOD_STRUCTURE.md` | OK | File exists |
| `docs/BEST_PRACTICES.md` | OK | File exists |
| `docs/SCREENS.md` | OK | File exists |
| `docs/CHANGELOG.md` | OK | File exists |
| `docs/KNOWN_ISSUES.md` | OK | File exists |
| `docs/old/` exists | OK | Directory exists with archived planning files |
| `llm-docs/architecture-overview.md` | OK | File exists |
| `llm-docs/source-inventory.md` | OK | File exists |
| `llm-docs/framework-reference.md` | OK | File exists |
| `llm-docs/type-index.md` | OK | File exists |
| `llm-docs/decompiled/` | OK | Directory exists (gitignored, populated) |
| **IGNORE:** `arena accessibility backlog.txt` | **STALE** | File no longer exists in the repo root; the IGNORE note is harmless but refers to a deleted file |

Note: The docs/ listing in CLAUDE.md is a PARTIAL enumeration of only 6 files. The actual `docs/` directory contains approximately 30+ files (e.g., `EVENTS.md`, `SOCIAL_SYSTEM.md`, `LOCALIZATION.md`, `DROPDOWN_HANDLING.md`, etc.). This is a mild anti-pattern (see Section 2).

### Quick Reference

| Claim | Status | Notes |
|---|---|---|
| `dotnet build src/AccessibleArena.csproj` | OK | `src/AccessibleArena.csproj` exists |
| Build auto-copies to `$(MtgaPath)\Mods\` | OK | Confirmed in csproj `CopyToMods` target |
| Default path `C:\Program Files\Wizards of the Coast\MTGA` | OK | Matches csproj default for `MtgaPath` |
| Steam path `C:\Program Files (x86)\Steam\steamapps\common\MTGA` | UNCERTAIN | Policy statement; correct per Steam defaults but can't verify against actual game install |
| `src/local.props` (gitignored) to override path | OK | `.gitignore` line 15 confirms `src/local.props` is gitignored |
| `dotnet test tests/AccessibleArena.Tests` | OK | Directory exists; uses NUnit 3.14.0 (confirmed in `.csproj`) |
| Tests use NUnit | OK | Confirmed |
| Tests use stub assemblies for MelonLoader and UnityEngine | OK | Confirmed via `tests/stubs/` subdirectories |
| `powershell -NoProfile -File installer/release.ps1` | OK | `installer/release.ps1` exists |
| Update `ModVersion` in `src/Directory.Build.props` before release | OK | Field confirmed at line 3: `<ModVersion>0.9.4</ModVersion>` |
| Add `## vX.Y` section to `docs/CHANGELOG.md` | OK | CHANGELOG.md exists |
| `<MtgaPath>\MelonLoader\Latest.log` / `Logs\` | UNCERTAIN | Policy statement about game install location; not verifiable without game |
| Mod DLL: `<MtgaPath>\Mods\AccessibleArena.dll` | OK | Consistent with csproj `CopyToMods` target |
| Tolk DLLs in game root | UNCERTAIN | Policy statement; can't verify without game install |

### Key Utilities

| Claim | Status | Notes |
|---|---|---|
| `UIActivator.Activate(element)` | OK | Signature: `public static ActivationResult Activate(GameObject element)` in `src/Core/Services/UIActivator.cs` |
| `CardDetector.IsCard(element)` | OK | Signature: `public static bool IsCard(GameObject obj)` in `src/Core/Services/CardDetector.cs` |
| `UITextExtractor.GetText(element)` | OK | Signature: `public static string GetText(GameObject gameObject)` in `src/Core/Services/UITextExtractor.cs` |
| `CardModelProvider` | OK | Static class at `src/Core/Services/CardModelProvider.cs` |
| `CardTextProvider` | OK | Static class at `src/Core/Services/CardTextProvider.cs` |
| `CardStateProvider` | OK | Static class at `src/Core/Services/CardStateProvider.cs` |

### Browser Debug Tools

| Claim | Status | Notes |
|---|---|---|
| `BrowserDetector.EnableDebugForBrowser(string browserType)` | OK | Method exists, line 95 |
| `BrowserDetector.DisableDebugForBrowser(string browserType)` | OK | Method exists, line 104 |
| `BrowserDetector.DisableAllDebug()` | OK | Method exists, line 121 |
| `BrowserDetector.BrowserTypeWorkflow` constant | OK | `public const string BrowserTypeWorkflow = "Workflow"` at line 62 |

### Type Decompilation

| Claim | Status | Notes |
|---|---|---|
| `powershell -NoProfile -File tools\decompile.ps1 "TypeName"` | OK | Script exists and matches usage |
| `-Dll Core\|Asm\|Gre\|Auto` flag | **STALE/INCOMPLETE** | CLAUDE.md mentions this nowhere in the decompile command. The actual `-Dll` flag also supports `Shared` (for `SharedClientCore.dll`) but that isn't shown. The llm-docs/CLAUDE.md covers this; main CLAUDE.md does not. |
| `decompile-all.ps1` for batch refresh | UNCERTAIN | Script exists at `tools/decompile-all.ps1` but main CLAUDE.md does not mention it (the llm-docs/CLAUDE.md does). Not stale, just undocumented in main CLAUDE.md. |

---

## 2. Safe Custom Shortcuts — Sampled Keybind Verification

Sampled ~12 bindings across categories. All verified against source files.

### Menu Navigation
- Arrow Up/Down: Navigate menu items — OK (BaseNavigator)
- Home/End: First/last item — OK
- A-Z letter jump — OK (LetterSearchHandler)
- Backspace: Back one level — OK

### Duel - Zone Navigation
- C: Hand, G: Graveyard, X: Exile, S: Stack, W: Command — OK (ZoneNavigator.cs lines 290-342)
- Shift+G/X/W: Opponent zones — OK
- D: Your Library, Shift+D: Opponent Library — OK (ZoneNavigator.cs line 346)
- Shift+C: Opponent Hand Count — OK (ZoneNavigator.cs line 295)

### Duel - Battlefield
- B/A/R: Creature/Land/Non-creature rows — OK (BattlefieldNavigator.cs lines 143-177)
- Shift+B/A/R: Enemy rows — OK
- Shift+Up/Down: Row switching — OK (BattlefieldNavigator.cs lines 191-197)

### Duel - Info
- T: Turn — OK (DuelNavigator.cs line 563)
- I: Extended Card Info — OK (DuelNavigator.cs line 579)
- K: Counter info — OK (DuelNavigator.cs line 613)
- M / Shift+M: Land Summary — OK (DuelNavigator.cs line 655)
- V: Player Info Zone — OK (PlayerPortraitNavigator.cs line 190)
- L: Life totals — OK (PlayerPortraitNavigator.cs line 197)

### Duel - Full Control & Phase Stops
- P / Shift+P: Full control — OK (DuelNavigator.cs line 533)
- Shift+Backspace: Soft skip — OK (DuelNavigator.cs)
- Ctrl+Backspace: Force skip — OK (DuelNavigator.cs)
- 1-0: Phase stops — OK (DuelNavigator.cs lines 796-805)

### Duel - Combat
- Space: All Attack / Confirm Blocks — OK (CombatNavigator.cs line 598/618)
- Backspace: No Attacks / Cancel Blocks — OK
- Main Phase: Space (clicks primary button) — OK — NOTE: handled in HotHighlightNavigator (not CombatNavigator), but the user-facing description is accurate

### Duel - Targeting (HotHighlightNavigator)
- Tab: Cycle targets — OK (HotHighlightNavigator.cs line 183)
- Ctrl+Tab: Opponent targets only — OK (HotHighlightNavigator.cs line 147)
- Enter: Select — OK
- Backspace: Cancel — OK

### Global
- F1: Help Menu — OK (AccessibleArenaMod.cs line 217, HelpNavigator confirmed)
- F2: Settings Menu — OK (AccessibleArenaMod.cs line 218, opens ModSettingsNavigator i.e. **mod's own settings**, not game settings)
- F3: Current screen — OK (AccessibleArenaMod.cs line 221)
- Ctrl+R: Repeat — OK (AccessibleArenaMod.cs line 219: `RegisterShortcut(KeyCode.R, KeyCode.LeftControl, ...)`)

### Missing From CLAUDE.md Shortcuts (undocumented bindings found in code)
- **F4**: Opens Friends panel (from menu navigators) or Duel Chat (from DuelNavigator) — not documented in CLAUDE.md
- **F5**: Check for update / start update — not documented
- **Shift+F12**: Speak recent debug log entries — not documented (debug tool, may intentionally be omitted)
- **Ctrl+F1**: Repeat tutorial hint — not documented
- **O** (duel): Open Game Log navigator — not documented
- **E / Shift+E** (duel): Announce player timer / opponent timer — not documented

---

## 3. Anti-Patterns to Fix

### 3.1 Partial Directory Listing (docs/)

CLAUDE.md lists 6 specific files in `docs/` but the directory actually contains ~30 files. This is a mild "snapshot listing" anti-pattern: any new docs file added to `docs/` won't be mentioned and any deleted file will appear as a stale reference. However, the 6 listed files are all primary reference files that survived — the listing appears intentionally curated, not exhaustive.

**Recommendation:** Either remove the per-file enumeration and replace with a one-line purpose description (e.g., "docs/ contains reference documentation on game architecture, mod structure, code patterns, screens, and known issues"), or keep the intentional curation but add a note like "(selected highlights — see docs/ for full list)".

### 3.2 Stale Patch Count

`src/Patches/` now contains **5** patch classes, not 4:
- EventSystemPatch
- KeyboardManagerPatch
- PanelStatePatch
- TimerPatch (added after previous validation)
- UXEventQueuePatch

### 3.3 Missing Shortcut Entries

The "Safe Custom Shortcuts" section is missing several active keybinds:
- F4 (Friends panel / Duel Chat)
- F5 (Update check)
- O / Duel Info (Game Log)
- E / Shift+E / Duel Info (Player timers)
- Ctrl+F1 (Repeat tutorial hint)
- (Shift+F12 is intentionally a debug tool and may stay undocumented)

### 3.4 Missing Decompile Script Options

The decompile command shown (`powershell -NoProfile -File tools\decompile.ps1 "TypeName"`) omits the useful `-Dll Core|Asm|Gre|Shared|Auto` flag. The `decompile-all.ps1` script is also not mentioned. These are covered in `llm-docs/CLAUDE.md` but not the main CLAUDE.md.

### 3.5 Stale IGNORE Notice

`**IGNORE:** arena accessibility backlog.txt - outdated` points to a file that no longer exists in the repo. The notice is harmless but can be removed.

### 3.6 Game Assemblies — SharedClientCore Not Listed

CLAUDE.md mentions only `Core.dll` and `Assembly-CSharp.dll` as game assemblies. There are actually 4 relevant DLLs used in decompilation:
- `Core.dll` — most game types
- `Assembly-CSharp.dll` — some UI types
- `Wizards.MDN.GreProtobuf.dll` — GRE protocol enums
- `SharedClientCore.dll` — card database types

The `type-index.md` documents all four but `CLAUDE.md` only lists two.

---

## 4. Mandatory Sections Check

| Section | Present? | Notes |
|---|---|---|
| Build directions (build + deploy) | YES | Under "Build & Deploy" |
| Mod loader identified | YES | "MelonLoader" in Game & Framework |
| Game name (MTGA) | YES | First line of Purpose section |
| Basic UI paradigms / conceptual overview | YES | Key Utilities, Code Standards, Browser Debug Tools |
| Screen reader technology | YES | Tolk library described in Game & Framework |
| Test instructions | YES | Under "Tests" |
| Release instructions | YES | Under "Release" |

All mandatory sections are present.

---

## 5. Recommendations (ordered by priority)

1. **Fix patch class count**: Change "4 patch classes" to "5 patch classes" in the Game & Framework section.

2. **Add missing shortcut entries**: Add to "Safe Custom Shortcuts":
   - Under **Global**: `F4 (Friends panel from menus; Duel Chat from duels)`, `F5 (Check for update)`, `Ctrl+F1 (Repeat tutorial hint)`
   - Under **Duel - Info**: `O (Game log - review duel announcements)`, `E (Your timer), Shift+E (Opponent timer)`

3. **Expand game assemblies list**: Change the game assemblies bullet to name all 4 DLLs: Core.dll (most game types), Assembly-CSharp.dll (some UI types), Wizards.MDN.GreProtobuf.dll (GRE protocol enums), SharedClientCore.dll (card database types).

4. **Improve decompile command**: Add `-Dll Core|Asm|Gre|Shared|Auto` flag to the example command, and mention `decompile-all.ps1` for batch refresh after game updates.

5. **Remove stale IGNORE notice**: Delete the `**IGNORE:** arena accessibility backlog.txt` line since that file no longer exists.

6. **Docs listing**: Consider replacing the 6-file enumeration with a brief prose description plus a note like "(see docs/ for full list)" — avoids partial-listing rot without losing the orientation value of the listed files.

7. **Low priority**: The F2 description "Settings Menu" is technically the mod's own settings (ModSettingsNavigator), not the game's built-in Settings — this is fine as user-facing labeling but could be clarified as "Mod Settings" to distinguish it from MTGA's native settings UI.
