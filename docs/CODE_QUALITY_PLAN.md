# Accessible Arena - Code Quality Improvement Plan

**Created:** 2026-02-03
**Last Updated:** 2026-02-03

## Progress Summary

| Phase | Status | Completed |
|-------|--------|-----------|
| Phase 1: Inspection & Cleanup | COMPLETE | 2026-02-03 |
| Phase 2: Debug Infrastructure | PARTIAL | Stages 2.1, 2.2 done |
| Phase 3: Code Deduplication | COMPLETE | 2026-02-03 |
| Phase 4: Dropdown Flag Unification | PARTIAL | Stage 4.1 done |
| Phase 5: Panel Detection Cleanup | COMPLETE | 2026-02-03 |
| Phase 6: Utility Extraction | NOT STARTED | |
| Phase 7: Documentation Updates | NOT STARTED | |

---

## Overview

**Goal:** Improve maintainability, readability, efficiency, and documentation while reducing technical debt.

**Approach:** Incremental stages, risky/fundamental changes first, each stage independently testable.

**Key Decisions:**
- Keep `old/` folders as archive (move removed code there)
- Central debug toggle (single global flag)
- Extract utilities only from large files (don't split navigators)
- High priority on unifying panel detection and dropdown flags

---

## Phase 1: Inspection & Cleanup (LOW RISK) - COMPLETE

### Stage 1.1: Root Directory Cleanup - COMPLETE
**Scope:** Remove clutter from root directory

**Completed:**
- Created `archive/analysis/` folder
- Moved 9 `analysis_*.txt` files (~1.5MB) to archive
- Created `archive/backups/` folder
- Moved 3 "working back up" folders (~26MB) to archive
- Removed `nul` file (Windows artifact)
- Removed garbage file from failed command
- Kept German-named files (user preference)

---

## Phase 2: Debug Infrastructure (MEDIUM RISK, FUNDAMENTAL) - PARTIAL

### Stage 2.1: Create Global Debug Configuration - COMPLETE
**Scope:** Create centralized debug toggle system

**Completed:**
- Created `src/Core/Services/DebugConfig.cs` with:
  - `DebugEnabled` master toggle (default: false)
  - Category toggles: LogNavigation, LogPanelDetection, LogFocusTracking, LogCardInfo, LogActivation, LogAnnouncements, LogPatches
  - `Log(tag, message)` and `LogIf(category, tag, message)` methods
  - `EnableAll()` / `DisableAll()` convenience methods

---

### Stage 2.2: Migrate UIFocusTracker Debug Code - COMPLETE
**Scope:** Replace hardcoded `_debugMode = true` with DebugConfig

**Completed:**
- Removed `_debugMode` field and `LOG_PREFIX` constant
- Updated `Update()` to use `DebugConfig.DebugEnabled && DebugConfig.LogFocusTracking`
- Replaced `Log()` method to use `DebugConfig.LogIf`
- Replaced 12 direct `MelonLogger.Msg` calls with `DebugConfig.LogIf`
- Kept `MelonLogger.Warning` for errors (always visible)

---

### Stage 2.3: Migrate High-Volume Logging Files - NOT STARTED
**Scope:** Update top 5 files by MelonLogger.Msg call count

**Files (by call count):**
1. CardModelProvider.cs (123 calls)
2. PanelStatePatch.cs (85 calls)
3. MenuDebugHelper.cs (58 calls)
4. DuelAnnouncer.cs (51 calls)
5. PlayerPortraitNavigator.cs (53 calls)

**Tasks:**
- Replace debug logging with `DebugConfig.Log(tag, message)`
- Keep critical error/warning messages as direct MelonLogger calls
- Add appropriate category tags

**Testing:** Quieter logs when debug disabled, full detail when enabled

**Risk:** MEDIUM

**Dependencies:** Stage 2.1

---

## Phase 3: Code Deduplication (LOW RISK) - COMPLETE

### Stage 3.1: Consolidate GetGameObjectPath - COMPLETE
**Scope:** Remove 4 duplicate implementations, use MenuDebugHelper as single source

**Completed:**
- Kept `MenuDebugHelper.GetGameObjectPath()` as single source
- Removed duplicates from:
  - `UIFocusTracker.cs` (also removed MAX_HIERARCHY_DEPTH constant)
  - `DuelNavigator.cs`
  - `DuelAnnouncer.cs`
- `GeneralMenuNavigator.cs` already had thin wrapper (OK)

---

### Stage 3.2: Consolidate Log() Wrapper Methods - COMPLETE
**Scope:** Remove duplicate Log() implementations

**Completed:**
- Updated `UIActivator.cs` Log() to use `DebugConfig.LogIf(LogActivation, ...)`
- Updated `NPERewardNavigator.cs` Log() to use `DebugConfig.LogIf(LogNavigation, ...)`
- `UIFocusTracker.cs` already updated in Stage 2.2

**Tested:** Debug logging verified working with `DebugEnabled = true`

---

### Stage 3.3: Remove Commented Deprecated Code - COMPLETE
**Scope:** Clean up commented-out deprecated setters

**Completed:**
- Removed SetTargetNavigator comment from `BattlefieldNavigator.cs`
- Removed SetTargetNavigator + SetDiscardNavigator comments from `ZoneNavigator.cs`
- Removed SetTargetNavigator comment from `DuelAnnouncer.cs`
- ~20 lines of dead code removed

---

## Phase 4: Dropdown Flag Unification (HIGH PRIORITY, HIGH RISK) - NEXT

### Stage 4.1: Document Dropdown Flag Behavior (Exploration) - COMPLETE
**Scope:** Analysis only - map current behavior

**Current flags (4 total):**
- `BaseNavigator._wasInDropdownMode`
- `BaseNavigator._skipDropdownModeTracking`
- `UIFocusTracker._suppressNextFocusAnnouncement`
- `UIFocusTracker._suppressDropdownModeEntry`

**Completed:**
- Created `docs/DROPDOWN_HANDLING.md` documenting:
  - All 4 flags with their purposes, set/clear conditions, and locations
  - Additional state variables (`_dropdownEditMode`, `_activeDropdownObject`)
  - 3 state machine diagrams (normal flow, auto-advance, auto-open suppression)
  - Redundancy analysis: parallel suppression mechanisms
  - Recommendations for Stage 4.2 (unified DropdownStateManager)
  - Test scenarios for implementation phase

**Key Finding:**
- Two flags (`_skipDropdownModeTracking` and `_suppressDropdownModeEntry`) serve similar purposes
- Root cause: dual state tracking in BaseNavigator and UIFocusTracker
- Recommended approach for 4.2: unified DropdownStateManager

**Risk:** NONE (exploration only)

---

### Stage 4.2: Implement Unified Dropdown State - NOT STARTED
**Scope:** Consolidate 4 flags into single source of truth

**Tasks:**
1. Add to UIFocusTracker or create DropdownStateManager:
   - `SuppressMode` enum: None, FocusAnnouncement, ModeEntry
   - Clear query/set API
2. Update BaseNavigator to use new API
3. Update UIFocusTracker to use new API
4. Remove redundant flags

**Testing:**
- Dropdown navigation works (arrow keys)
- Escape/Backspace closes dropdown
- No double announcements
- Auto-advance after selection (Month -> Day -> Year)

**Risk:** HIGH (complex state machine)

**Dependencies:** Stage 4.1

---

## Phase 5: Panel Detection Cleanup (MEDIUM RISK)

### Stage 5.1: Document Panel Detection Strategy (Exploration) - COMPLETE
**Scope:** Create authoritative documentation

**Current methods (4):**
1. Harmony patches (event-driven) - `HarmonyPanelDetector.cs`
2. Reflection polling (IsOpen) - `ReflectionPanelDetector.cs`
3. Alpha detection (CanvasGroup) - `AlphaPanelDetector.cs`
4. Direct GameObject search - various navigators

**Completed:**
- Updated `docs/BEST_PRACTICES.md` "Panel Detection Strategy" section with:
  - Architecture overview with ASCII diagram showing PanelStateManager as central coordinator
  - Three detector descriptions (Harmony, Reflection, Alpha) with file locations
  - Panel → Detector mapping table showing which detector handles which panels
  - Detailed "Detector Ownership" section with exhaustive pattern lists
  - Updated decision tree for adding new panels
  - Updated "Adding a New Panel" steps with correct file paths
  - Added "Legacy Files" note about MenuPanelTracker and UnifiedPanelDetector

**Key Findings:**
- PanelStateManager is the single source of truth (created in AccessibleArenaMod.cs)
- Each detector has explicit `HandlesPanel()` method with exclusion logic
- ReflectionDetector explicitly excludes Harmony and Alpha patterns
- Legacy files (MenuPanelTracker, UnifiedPanelDetector) still exist but are supplementary

**Risk:** NONE

---

### Stage 5.2: Audit Panel Detection Overlaps - COMPLETE
**Scope:** Identify panels detected by multiple methods

**Completed:**
1. Added `LogPanelOverlapDiagnostic` flag to DebugConfig.cs
2. Added runtime overlap tracking to PanelStateManager:
   - `_panelDetectorAudit` dictionary tracks which detector first reports each panel
   - `_overlapAudit` dictionary tracks all detectors that try to report each panel
   - `TrackPanelDetectorAudit()` logs warnings when overlap detected
   - `DumpOverlapAudit()` dumps full audit results to log
3. Added static analysis method `RunStaticOverlapAnalysis()`:
   - Tests 25+ known panel names against all three detectors' `HandlesPanel()` methods
   - Identifies panels claimed by multiple detectors
   - Identifies unclaimed panels
   - Shows ownership summary by detector

**How to Use:**
1. Enable diagnostic mode: Set `DebugConfig.DebugEnabled = true` and `DebugConfig.LogPanelOverlapDiagnostic = true`
2. Run game through screens - overlaps logged as warnings in real-time
3. Call `PanelStateManager.Instance.DumpOverlapAudit()` for full report
4. Call `PanelStateManager.Instance.RunStaticOverlapAnalysis()` for static analysis

**Key Finding (Static Analysis):**
The current HandlesPanel() implementations appear to have proper mutual exclusion:
- HarmonyDetector has explicit patterns (playblade, settings, socialui, etc.)
- ReflectionDetector explicitly excludes Harmony and Alpha patterns in HandlesPanel()
- AlphaDetector only claims its specific patterns (systemmessageview, dialog, modal, etc.)

**Testing:** Build succeeds, audit tools ready for runtime testing

**Risk:** LOW (diagnostic only)

**Dependencies:** Stage 5.1

---

### Stage 5.3: Eliminate Panel Detection Overlaps - COMPLETE
**Scope:** Ensure each panel detected by exactly one method

**Completed:**
1. Added `OwnedPatterns` static arrays to HarmonyPanelDetector and AlphaPanelDetector
2. Added comprehensive ownership comments (Panel Ownership regions) to all three detectors:
   - HarmonyPanelDetector: Documents why Harmony (patchable methods, slide animations)
   - AlphaPanelDetector: Documents why Alpha (fade in/out, no IsOpen property)
   - ReflectionPanelDetector: Documents fallback role and explicit exclusions
3. Updated exclusion logic:
   - AlphaPanelDetector now defensively excludes HarmonyPanelDetector.OwnedPatterns
   - ReflectionPanelDetector now references OwnedPatterns from other detectors (DRY)
4. Updated `RunStaticOverlapAnalysis()` to use OwnedPatterns arrays instead of hardcoded values

**Key Changes:**
- `HarmonyPanelDetector.OwnedPatterns` - authoritative list for event-driven panels
- `AlphaPanelDetector.OwnedPatterns` - authoritative list for alpha-fade panels
- All detectors now have clear `#region Panel Ownership` documentation
- Mutual exclusion is enforced by referencing other detectors' OwnedPatterns

**Testing:** Build succeeds. Runtime testing recommended for:
- Each panel announced exactly once
- No missing detections
- Settings menu works in all scenes

**Risk:** MEDIUM

**Dependencies:** Stage 5.2

---

## Phase 6: Utility Extraction from Large Files (LOW RISK)

### Stage 6.1: Extract Shared Utilities from BaseNavigator - NOT STARTED
**Scope:** Move utility methods to appropriate service classes

**File:** `src/Core/Services/BaseNavigator.cs` (76K)

**Candidates for extraction:**
- Common element filtering logic
- Scroll handling utilities
- Announcement helper methods

**Testing:** Build succeeds, all navigators work

**Risk:** LOW

---

### Stage 6.2: Extract Shared Utilities from UIActivator - NOT STARTED
**Scope:** Move utility methods to MenuDebugHelper or new class

**File:** `src/Core/Services/UIActivator.cs` (101K)

**Candidates for extraction:**
- Debug logging methods
- Common type detection utilities

**Testing:** Build succeeds, element activation works

**Risk:** LOW

---

## Phase 7: Documentation Updates (NO RISK)

### Stage 7.1: Audit SCREENS.md Quick Reference - NOT STARTED
**Scope:** Verify screen reference documentation is accurate

**Tasks:**
1. Compare SCREENS.md against actual navigators in codebase
2. Verify all screen names and shortcuts are correct
3. Add any missing screens discovered since last update
4. Remove references to deprecated screens
5. Ensure shortcut key mappings match code

**Testing:** Walk through each screen in game and verify documentation matches

**Risk:** NONE

---

### Stage 7.2: Audit MOD_STRUCTURE.md Implementation Status - NOT STARTED
**Scope:** Verify all status markers ([+], [~], [?], [-]) are accurate

**Tasks:**
1. Review each feature's status marker against actual code state:
   - [+] WORKING - verify feature works end-to-end
   - [~] PARTIALLY WORKING - document what's missing
   - [?] UNTESTED - test and update status
   - [-] NOT WORKING - verify still broken or update
2. Update file listings to match current structure
3. Remove references to deleted/archived files
4. Add any new files/components not listed

**Testing:** Cross-reference with actual codebase structure

**Risk:** NONE

---

### Stage 7.3: Update BEST_PRACTICES.md - NOT STARTED
**Tasks:**
- Add DebugConfig usage patterns
- Update panel detection documentation
- Add GetGameObjectPath guidance (use MenuDebugHelper)
- Document dropdown handling

**Dependencies:** Phases 2, 4, 5

---

### Stage 7.4: Update MOD_STRUCTURE.md File Listings - NOT STARTED
**Tasks:**
- Add DebugConfig.cs to file listing
- Document archive/ folder structure
- Update outdated file references
- Sync with status audit from Stage 7.2

**Dependencies:** Stage 7.2, all previous phases

---

### Stage 7.5: Create CONTRIBUTING.md (Optional) - NOT STARTED
**Tasks:**
- Code style guidelines
- Debug logging conventions
- PR/testing checklist

---

## Recommended Session Order

| Session | Stage | Focus | Risk | Status |
|---------|-------|-------|------|--------|
| 1 | 1.1 | Root cleanup | VERY LOW | DONE |
| 2 | 2.1 | DebugConfig creation | LOW | DONE |
| 3 | 2.2 | UIFocusTracker migration | MEDIUM | DONE |
| 4 | 3.1 | GetGameObjectPath dedup | LOW | DONE |
| 5 | 3.2-3.3 | Log() dedup, remove comments | LOW | DONE |
| 6 | 4.1 | Dropdown analysis | NONE | DONE |
| 7 | 5.1 | Panel detection docs | NONE | DONE |
| 8 | 2.3 | High-volume logging | MEDIUM | |
| 9 | 4.2 | Dropdown unification | HIGH | |
| 10 | 5.2 | Panel detection audit | LOW | DONE |
| 11 | 5.3 | Panel detection cleanup | MEDIUM | DONE |
| 12 | 6.1-6.2 | Utility extraction | LOW | |
| 13 | 7.1-7.2 | SCREENS.md + status audit | NONE | |
| 14 | 7.3-7.4 | BEST_PRACTICES + MOD_STRUCTURE | NONE | |
| 15 | 7.5 | CONTRIBUTING.md (optional) | NONE | |

---

## Critical Files

**Debug Infrastructure:**
- `src/Core/Services/DebugConfig.cs` (created)
- `src/Core/Services/MenuDebugHelper.cs`
- `src/Core/Services/UIFocusTracker.cs`

**Dropdown Handling:**
- `src/Core/Services/BaseNavigator.cs`
- `src/Core/Services/UIFocusTracker.cs`

**Panel Detection:**
- `src/Core/Services/PanelDetection/PanelStateManager.cs`
- `src/Core/Services/PanelDetection/HarmonyPanelDetector.cs`
- `src/Core/Services/PanelDetection/ReflectionPanelDetector.cs`
- `src/Core/Services/PanelDetection/AlphaPanelDetector.cs`

**Large Files (utility extraction):**
- `src/Core/Services/BaseNavigator.cs` (76K)
- `src/Core/Services/UIActivator.cs` (101K)
- `src/Core/Services/GeneralMenuNavigator.cs` (145K)
- `src/Core/Services/DuelAnnouncer.cs` (108K)

---

## Testing Protocol

After each stage:
1. Build project: `dotnet build src/AccessibleArena.csproj`
2. Deploy: Copy DLL to Mods folder
3. Launch game
4. Test affected functionality
5. Check logs for errors: `Get-Content '...\Latest.log' -Tail 300`

---

## Success Criteria

- All stages complete without regressions
- Debug output controllable via single toggle
- No duplicate utility implementations
- Panel detection consistent (one method per panel)
- Dropdown handling simplified
- Documentation reflects current patterns
- Root directory clean
- SCREENS.md matches actual navigator implementations
- MOD_STRUCTURE.md status markers are verified accurate
- All listed files in docs exist in codebase
