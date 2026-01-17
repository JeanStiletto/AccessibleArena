# Navigator and Zone Issues - Fix Plan

This document captures all identified issues with the duel navigation system, including architectural problems, inconsistencies, and bugs observed in logs. Use this as a roadmap for systematic fixes.

---

## Status Overview (Updated 2026-01-17 - Session 4 Complete)

| Issue | Status | Notes |
|-------|--------|-------|
| 1.1 Targeting exit passive | **FIXED** | ExitTargetMode() added after target selection - tested with Shock |
| 1.2 Enter key priority | **FIXED** | Zone shortcuts (C/G/X/S) clear HotHighlightNavigator state |
| 1.3 Zone state conflicts | **FIXED** | ZoneOwner enum added, ownership tracked in SetCurrentZone |
| 1.4 Wrong zone for targets | **FIXED** | DetermineTargetZone() detects Stack, Graveyard, Exile, Hand |
| 2.1 Two entry points | **FIXED** | TryEnterTargetMode unified method created |
| 2.2 HotHighlight methods | **FIXED** | CardDetector.HasHotHighlight() used everywhere |
| 2.3 Rescan loop | ADDRESSED | Uncommitted changes add attempt counter |
| 2.4 Combat rapid transitions | SKIPPED | Not needed - unified navigator has no auto-detect logic |
| 2.5 Deactivation cleanup | **FIXED** | All navigators reset indices in Deactivate() |
| 2.6 EventSystem conflicts | **FIXED** | All navigators use SetFocusedGameObject wrapper |
| 3.1 Excessive rediscovery | SKIPPED | Caching could cause stale data, not needed |
| 3.2 Stack count timing | **FIXED** | GetFreshStackCount() for timing-sensitive checks |
| 3.3 CardInfo mismatch | **FIXED** | PrepareForCard logs card name for correlation |
| 4.1 Targeting logging | **FIXED** | TryEnterTargetMode logs entry/exit with target count |
| 4.2 Zone change logging | **FIXED** | SetCurrentZone logs zone + owner changes |
| 4.3 Focus change logging | **FIXED** | SetFocusedGameObject wrapper logs all focus changes |

### Session 4 Completed (2026-01-17)

**Changes made:**

1. **Issue 1.2 FIXED** - ZoneNavigator.cs, HotHighlightNavigator.cs, DuelNavigator.cs
   - Added `ClearState()` method to HotHighlightNavigator
   - ZoneNavigator calls `ClearState()` when zone shortcuts (C/G/X/S) are pressed
   - Prevents stale highlight state from interfering with zone navigation

2. **Issue 2.5 FIXED** - ZoneNavigator.cs, BattlefieldNavigator.cs
   - ZoneNavigator.Deactivate() now resets `_cardIndexInZone` and `_zoneOwner`
   - BattlefieldNavigator.Deactivate() now resets `_currentRow` and `_currentIndex`
   - Prevents stale indices from persisting between duel sessions

3. **Issue 3.3 FIXED** - CardInfoNavigator.cs
   - PrepareForCard() now logs card name: `Prepared for 'Mountain' (CDC #160) in zone: Hand`
   - Enables correlation between CardInfo and announcements in logs

**Issues skipped (not needed with unified navigator):**

- **Issue 2.4** - Combat phase debounce: The old auto-detect targeting logic that was affected by rapid phase changes has been removed. HotHighlightNavigator trusts the game's highlight system.
- **Issue 3.1** - Zone discovery caching: Could cause stale data on fast navigation. Current implementation only discovers on explicit user input (C/G/X/S), which is acceptable.

**Testing performed:**
- Cast Shock, Tab to cycle targets, Enter to select - works
- Zone navigation (C/G/X/S) - works
- Battlefield navigation (A/B/R) - works
- CardInfo logging shows card names in logs

---

### Session 3 Completed (2026-01-16)

**Changes made:**

1. **Issue 2.2 FIXED** - CardDetector.cs
   - Added `HasHotHighlight(GameObject obj)` static method
   - Updated `HasValidTargetsOnBattlefield()` to use unified method
   - Removed duplicate implementations from TargetNavigator and HighlightNavigator

2. **Issue 2.1 FIXED** - TargetNavigator.cs
   - Added `TryEnterTargetMode(bool requireValidTargets = true)` unified entry point
   - Checks if already targeting, discovers targets, logs entry with target count
   - DuelNavigator calls with `requireValidTargets: true` (auto-detect)
   - DuelAnnouncer calls with `requireValidTargets: false` (event-based)

3. **Issue 3.2 FIXED** - ZoneNavigator.cs
   - Added `GetFreshStackCount()` method for timing-sensitive stack checks
   - Directly scans scene for stack cards instead of using cached `_zones`
   - DuelNavigator and DuelAnnouncer now use fresh count for targeting decisions

4. **Combat targeting fix** - DuelNavigator.cs, DuelAnnouncer.cs
   - Removed `!inCombatPhase` check from targeting mode entry
   - Now enters targeting mode whenever spell on stack, regardless of combat phase
   - Tested: Cast Shock during Declare Attackers, Tab cycles 3 targets, Enter selects

**Testing performed:**
- Cast Shock during Declare Attackers phase
- Targeting mode auto-entered with 3 valid targets
- Tab cycling worked correctly
- Enter selected target, Shock resolved
- Logs confirmed: `[TargetNavigator] TryEnterTargetMode: entered with 3 targets`

---

### Session 2 Completed (2026-01-16)

**Changes made:**
1. **Issue 1.3 FIXED** - ZoneNavigator.cs
   - Added `ZoneOwner` enum (None, ZoneNavigator, BattlefieldNavigator, HighlightNavigator, TargetNavigator)
   - Added `_zoneOwner` field and `CurrentZoneOwner` property
   - `SetCurrentZone()` now tracks owner and logs ownership changes
   - `ParseZoneOwner()` method determines owner from caller string

2. **Issue 1.4 FIXED** - TargetNavigator.cs:213-262
   - Added `DetermineTargetZone()` method that checks parent hierarchy
   - Detects Stack, Graveyard, OpponentGraveyard, Exile, Hand zones
   - Logs when targets are in non-battlefield zones
   - Updated `AnnounceCurrentTarget()` to use zone detection

3. **Issue 2.6 VERIFIED** - Already fixed in Session 1
   - ZoneNavigator, BattlefieldNavigator, TargetNavigator, HighlightNavigator all use wrapper
   - No direct EventSystem.SetSelectedGameObject calls remain

**Testing performed:**
- Zone ownership tracking: Verified - logs show correct owner transitions (ZoneNavigator <-> BattlefieldNavigator)
- Focus change logging: Verified - all navigators log focus changes with caller name
- Mixed navigation (Tab, C, B keys): Working correctly
- **UNTESTED:** Targeting spells on stack (Issue 1.4) - needs counterspell scenario to verify DetermineTargetZone()

---

### Session 1 Completed (2026-01-16)

**Changes made:**
1. **Issue 1.1 FIXED** - TargetNavigator.cs:152-161
   - Added `ExitTargetMode()` call after successful target selection
   - Added comment about multi-target spell behavior (relies on auto-detect re-entry)
   - Tested with Shock spell - works correctly

2. **Issue 4.2 FIXED** - ZoneNavigator.cs:61-67
   - Added optional `caller` parameter to `SetCurrentZone()`
   - Logs zone changes: `[ZoneNavigator] Zone change: Hand -> Battlefield (by TargetNavigator)`
   - Updated all callers: BattlefieldNavigator, TargetNavigator, HighlightNavigator

3. **Issue 4.3 FIXED** - ZoneNavigator.cs:62-75
   - Added static `SetFocusedGameObject(GameObject, string caller)` wrapper
   - Logs focus changes: `[Navigation] Focus change: CardA -> CardB (by Navigator)`
   - Updated all navigators to use wrapper instead of direct EventSystem calls

---

### Changes We Made That Created Problems

**Commit 651c3d8** ("Unify focus management across all zone navigators"):
- Added `SetSelectedGameObject` to ZoneNavigator, BattlefieldNavigator, HighlightNavigator
- **Intention**: Enable zone exit detection when focus changes
- **Consequence**: Created Issue 2.6 (race conditions) and contributed to Issue 1.3 (zone conflicts)
- **Keep**: The focus tracking approach is sound
- **Fix differently**: Add coordination/ownership model instead of reverting

**Commit 33deeb9** ("Fix victory screen clutter and targeting mode for non-targeting abilities"):
- Added `_zoneNavigator.SetCurrentZone(ZoneType.Battlefield)` to TargetNavigator line 193-196
- Added zone update to HighlightNavigator when Tab cycling
- **Intention**: Make Left/Right arrows work during targeting
- **Consequence**: Created Issue 1.4 (wrong zone for non-battlefield targets)
- **Keep**: The zone update concept for arrow key navigation
- **Fix differently**: Detect actual zone from target's parent hierarchy

### Changes to Preserve

1. **Backspace handler in TargetNavigator** (line 100-104) - working correctly
2. **Auto-exit logic in DuelNavigator** (lines 341-365) - good safety net
3. **Stack check in both entry points** - prevents false targeting for mana abilities
4. **Rescan attempt counter in HighlightNavigator** - prevents infinite loops
5. **SetSelectedGameObject calls** - needed for focus tracking, just need coordination

### Documentation Confirms Our Analysis

**Issue 1.1 is a BUG - deviates from original design:**
`TARGET_SELECTION_IMPLEMENTATION_PLAN.md` (lines 309-319) shows the original design had `ExitTargetMode()` in `SelectCurrentTarget()`:
```csharp
_announcer.Announce($"Targeted {target.Name}");
ExitTargetMode();  // <-- IN ORIGINAL DESIGN, MISSING FROM IMPLEMENTATION
```

**Known bug pattern already documented:**
`BEST_PRACTICES.md` (lines 1115-1118) lists "Targeting mode doesn't exit" as a known bug pattern with the exact checks we need.

**HotHighlight multi-purpose is documented:**
`BEST_PRACTICES.md` (line 1032-1033): "CRITICAL: We cannot visually distinguish these! This is why phase detection is essential." HotHighlight shared for: targeting, playable cards, attackers, blockers.

**Game event is unreliable:**
`BEST_PRACTICES.md` (line 1063): "PlayerSubmittedTargetsEventTranslator is unreliable" - explains why explicit ExitTargetMode() is needed.

**Zone changes had known trade-offs:**
`MOD_STRUCTURE.md` (line 1067) NOTE: "HighlightNavigator change might need reverting if keeping battlefield row state is preferred"

**Recommended logging matches Issue 4.1:**
`BEST_PRACTICES.md` (lines 1134-1138) already recommends the exact debug logging we proposed.

---

## Priority 1: Critical - Causes Wrong Actions

### Issue 1.1: Targeting Mode Exit is Passive Only

**Status:** NOT FIXED - Critical priority (BUG - deviates from original design)

**File:** TargetNavigator.cs

**Problem:** SelectCurrentTarget() at line 139-161 clicks the target but does NOT call ExitTargetMode(). Exit only happens when auto-detect sees no more HotHighlight.

**Original design (TARGET_SELECTION_IMPLEMENTATION_PLAN.md) had ExitTargetMode() here - this is a regression.**

**Consequence:** If game delays removing HotHighlight after target selection:
- User presses C to go to hand
- User presses Enter to play a card
- TargetNavigator still has _isTargeting=true
- TargetNavigator intercepts Enter and clicks on a target instead

**Fix approach:**
- Add explicit ExitTargetMode() call after SelectCurrentTarget() succeeds
- The auto-exit in DuelNavigator is a safety net but shouldn't be primary mechanism
- Add logging to track targeting mode state changes

**Code change:**
```csharp
// In SelectCurrentTarget(), after successful click:
if (result.Success)
{
    _announcer.Announce(Strings.Targeted(target.Name), AnnouncementPriority.Normal);
    ExitTargetMode(); // ADD THIS
}
```

---

### Issue 1.2: Enter Key Priority When Stuck in Targeting Mode

**Status:** PARTIALLY FIXED

**File:** DuelNavigator.cs, HandleCustomInput() lines 321-404

**What's working:**
- Backspace handler in TargetNavigator allows manual exit (line 100-104)
- Auto-exit logic in DuelNavigator (lines 341-365) catches stale targeting

**What's still needed:**
- Consider: if user navigates to different zone (C, G, X), should targeting mode exit?
- This ties into the zone ownership model (Issue 1.3)

**Fix approach:**
- When user explicitly navigates to a zone (C/G/X/S keys), exit targeting mode
- This makes navigation predictable: zone keys always switch context

---

### Issue 1.3: Zone State Written by Multiple Navigators

**Status:** CREATED BY OUR CHANGES - Needs architectural fix

**Origin:** Commits 651c3d8 and 33deeb9 added zone updates to multiple navigators

**Files:** ZoneNavigator.cs, BattlefieldNavigator.cs, TargetNavigator.cs, HighlightNavigator.cs

**Problem:** _currentZone in ZoneNavigator is modified by 4 different navigators without coordination:
- ZoneNavigator.NavigateToZone() line 279
- BattlefieldNavigator.HandleInput() lines 109, 120, 131
- TargetNavigator.AnnounceCurrentTarget() line 195
- HighlightNavigator.AnnounceCurrentCard() line 298

**Consequence:** Zone becomes inconsistent with user's actual location. CardInfo prepared for wrong zone.

**Log evidence:**
- CardInfo prepared for CDC #175 in zone: Battlefield
- Announcement says "Baloth Packhunter, in hand"
- CardInfo prepared for CDC #164 in zone: Hand (different card)

**Fix approach (zone ownership model):**
```csharp
// In ZoneNavigator, add ownership tracking:
public enum ZoneOwner { None, ZoneNavigator, BattlefieldNavigator, TargetNavigator, HighlightNavigator }

private ZoneOwner _zoneOwner = ZoneOwner.None;

public void SetCurrentZone(ZoneType zone, ZoneOwner owner)
{
    // Log the change for debugging
    if (_currentZone != zone || _zoneOwner != owner)
    {
        MelonLogger.Msg($"[ZoneNavigator] Zone change: {_currentZone} -> {zone}, owner: {_zoneOwner} -> {owner}");
    }
    _currentZone = zone;
    _zoneOwner = owner;
}

// Optional: method to check if caller should update zone
public bool CanSetZone(ZoneOwner requester)
{
    // Higher priority owners can always set
    // TargetNavigator > HighlightNavigator > BattlefieldNavigator > ZoneNavigator
    return true; // For now, allow all - logging will help diagnose
}
```

**Do NOT revert:** The zone updates in each navigator - they serve a purpose (enabling arrow navigation). Instead, add coordination.

---

### Issue 1.4: TargetNavigator Sets Zone to Battlefield for All Non-Player Targets

**Status:** CREATED BY COMMIT 33deeb9 - Has known limitation

**File:** TargetNavigator.cs, AnnounceCurrentTarget() lines 191-196

**Current code (with original NOTE):**
```csharp
// Update zone context so Left/Right arrows work for battlefield navigation
// NOTE: This might need adjustment - targets can be on battlefield or stack
if (target.Type != CardTargetType.Player)
{
    _zoneNavigator.SetCurrentZone(ZoneType.Battlefield);
}
```

All non-player targets assumed to be on battlefield. But targets can be:
- On stack (targeting spells)
- In graveyard (some effects)
- In exile (some effects)

**Fix approach:**
```csharp
private ZoneType DetermineTargetZone(TargetInfo target)
{
    if (target.Type == CardTargetType.Player) return ZoneType.None;
    if (target.GameObject == null) return ZoneType.Battlefield;

    // Check parent hierarchy for zone indicators
    var current = target.GameObject.transform;
    while (current != null)
    {
        var name = current.name;
        if (name.Contains("Stack")) return ZoneType.Stack;
        if (name.Contains("Graveyard")) return ZoneType.Graveyard;
        if (name.Contains("Exile")) return ZoneType.Exile;
        if (name.Contains("Hand")) return ZoneType.Hand;
        current = current.parent;
    }

    return ZoneType.Battlefield; // Default fallback
}
```

---

## Priority 2: High - Causes Confusion or Poor UX

### Issue 2.1: Two Different Targeting Mode Entry Points

**Status:** PARTIALLY FIXED - Both entry points now have same checks

**Files:** DuelNavigator.cs lines 335-339, DuelAnnouncer.cs lines 905-923

**Current state (after our changes):**

Entry A (DuelNavigator auto-detect):
- Checks: NOT targeting, NOT combat, has valid targets, has spell on stack

Entry B (DuelAnnouncer event handler) - NOW FIXED:
- Checks: NOT combat, has spell on stack (uncommitted change)
- Still does NOT check hasValidTargets (by design - event fires before targets visible)

**Remaining issue:** Entry B can still enter targeting mode before targets are discovered.

**Fix approach:**
- Create unified method in TargetNavigator:
```csharp
public bool TryEnterTargetMode(bool requireValidTargets = true)
{
    if (_isTargeting) return false;

    DiscoverValidTargets();

    if (requireValidTargets && _validTargets.Count == 0)
    {
        MelonLogger.Msg("[TargetNavigator] TryEnterTargetMode: no valid targets");
        return false;
    }

    EnterTargetMode();
    return true;
}
```
- DuelNavigator calls with requireValidTargets=true
- DuelAnnouncer calls with requireValidTargets=false (it's an event response)

---

### Issue 2.2: Three Different HotHighlight Detection Methods

**Status:** NOT FIXED

**Files:** TargetNavigator.cs:394-407, HighlightNavigator.cs:421-434, CardDetector.cs:166-173

**Problem:** All three check for HotHighlight but with subtle differences:

Methods A and B (TargetNavigator, HighlightNavigator):
```csharp
foreach child in card.GetComponentsInChildren<Transform>(true)
  if child != card AND activeInHierarchy AND name.Contains("HotHighlight")
```

Method C (CardDetector):
```csharp
foreach child in go.GetComponentsInChildren<Transform>(true)
  if activeInHierarchy AND name.Contains("HotHighlight")
```

**Difference:** Methods A/B skip the object itself (child != card), Method C does not.

**Fix approach:**
- Create single static method: CardDetector.HasHotHighlight(GameObject obj)
- All three locations call this single method
- Ensure consistent behavior across all usages

---

### Issue 2.3: HighlightNavigator Rescan Loop on Tab Press

**Status:** ADDRESSED in uncommitted changes

**File:** HighlightNavigator.cs

**Original problem:** When only battlefield cards have HotHighlight (like mana creatures):
1. Finds only battlefield cards
2. Schedules delayed rescan (attempt 1/5)
3. User presses Tab again
4. Attempt counter resets to 1
5. Loop continues indefinitely

**Current fix (uncommitted):**
- Added `_rescanAttempts` counter with `MaxRescanAttempts = 5`
- Added `_pendingRescan` flag
- Tab press resets `_rescanAttempts = 0` intentionally (fresh user action)
- After max attempts, accepts battlefield-only results

**Potential refinement:**
- The reset on Tab is intentional (user explicitly wants fresh discovery)
- Consider: don't announce "Waiting for playable cards..." if max attempts already reached
- Current implementation looks correct, monitor in testing

---

### Issue 2.4: Combat Phase Rapid Transitions

**Status:** NOT FIXED

**File:** DuelAnnouncer.cs

**Problem:** Multiple phase changes happen within milliseconds:
```
Combat phase -> Declare attackers -> End of combat -> Second main -> First main
```
All within 0.3 seconds during opponent's empty combat.

**Consequence:** IsInDeclareAttackersPhase / IsInDeclareBlockersPhase may be briefly true/false during rapid transitions, causing auto-detect logic to make wrong decisions.

**Fix approach:**
- Add debounce: don't change phase state until stable for N milliseconds
- Or: track "was recently in combat" with cooldown
- Ensure targeting auto-detect doesn't trigger during rapid phase changes

---

### Issue 2.5: Inconsistent Deactivation Cleanup

**Status:** PARTIALLY FIXED - HighlightNavigator done, others pending

**Files:** ZoneNavigator.cs, BattlefieldNavigator.cs, TargetNavigator.cs, HighlightNavigator.cs

**Current state:**

ZoneNavigator.Deactivate():
- Clears: _isActive, _zones
- Does NOT reset: _currentZone, _cardIndexInZone

BattlefieldNavigator.Deactivate():
- Clears: _isActive, row lists
- Does NOT reset: _currentRow, _currentIndex

TargetNavigator.ExitTargetMode():
- Clears: _isTargeting, _validTargets
- Resets: _currentIndex to -1 (GOOD)

HighlightNavigator.Deactivate() - FIXED in uncommitted:
- Clears: _isActive, _highlightedCards, _pendingRescan, _rescanAttempts
- Resets: _currentIndex to -1 (GOOD)

**Consequence:** Stale indices/zones persist across sessions.

**Fix approach:**
- Standardize: all Deactivate() methods reset all indices to -1 or 0
- ZoneNavigator: reset _cardIndexInZone = 0
- BattlefieldNavigator: reset _currentRow = default, _currentIndex = -1

---

### Issue 2.6: EventSystem Selection Set Without Coordination

**Status:** CREATED BY COMMIT 651c3d8

**Origin:** "Unify focus management" commit added SetSelectedGameObject to all navigators

**Files:** Multiple navigators

**Problem:** Four navigators independently call EventSystem.SetSelectedGameObject():
- ZoneNavigator.AnnounceCurrentCard() line 446
- BattlefieldNavigator.AnnounceCurrentCard() line 492
- TargetNavigator.AnnounceCurrentTarget() line 188
- HighlightNavigator.AnnounceCurrentCard() line 290

**Consequence:** Race conditions. One navigator sets selection, another immediately overwrites.

**Fix approach (ties into Issue 1.3):**
- Add central method in ZoneNavigator or new NavigationState class:
```csharp
public void SetFocusedCard(GameObject card, string callerName)
{
    var eventSystem = EventSystem.current;
    if (eventSystem != null && card != null)
    {
        MelonLogger.Msg($"[Navigation] Focus set by {callerName}: {card.name}");
        eventSystem.SetSelectedGameObject(card);
    }
}
```
- All navigators call this instead of direct EventSystem access
- Logging helps diagnose race conditions
- Future: add ownership check if needed

**Do NOT revert:** The SetSelectedGameObject calls serve a purpose (focus tracking for zone exit detection). Add coordination instead.

---

## Priority 3: Medium - Performance or Minor UX Issues

### Issue 3.1: Excessive Zone Rediscovery

**Status:** NOT FIXED

**File:** ZoneNavigator.cs

**Problem:** DiscoverZones() called multiple times per second during normal navigation.

**Log evidence:** 6+ calls within 2 seconds during Tab navigation.

**Consequence:** Performance overhead, potential inconsistency.

**Fix approach:**
- Add caching: only rediscover if last discovery was >500ms ago
- Or: only rediscover on explicit zone navigation (C, G, X, S keys)
- HighlightNavigator can use cached zones

---

### Issue 3.2: Stack Count Check Has Timing Issues

**Status:** NOT FIXED

**File:** DuelNavigator.cs line 333, ZoneNavigator.cs

**Problem:** hasSpellOnStack checks _zones[Stack].Cards.Count, but _zones is only updated during DiscoverZones().

**Consequence:** Between discoveries, stack count may be stale. Auto-detect targeting logic uses stale data.

**Fix approach:**
- Add lightweight stack-only check method
- Or: subscribe to stack change events from game
- Or: always call DiscoverZones() before checking stack count in auto-detect

---

### Issue 3.3: CardInfo Prepared vs Announcement Mismatch

**Status:** NOT FIXED

**File:** CardInfoNavigator.cs, various navigators

**Problem:** CardInfo.PrepareForCard() called for one card, but announcement is for different card.

**Log evidence:**
- Prepared CDC #43 in zone: Hand
- Announced: Ilysian Caryatid, in hand, 2 of 3
- These may be same or different cards

**Fix approach:**
- Ensure PrepareForCard() is called with same card that was announced
- Pass card reference directly rather than relying on index
- Add validation: PrepareForCard() logs card name for correlation

---

## Priority 4: Improvements - Better Debugging

### Issue 4.1: Missing Targeting Mode Logging

**Status:** PARTIALLY FIXED - Some logging exists

**Current logging:**
- EnterTargetMode: logs target count
- ExitTargetMode: logs "Exiting targeting mode"
- Auto-detect: logs when entering/exiting

**Still missing:**
- Which navigator handled Enter key
- Auto-detect condition values when checking

**Fix approach:** Add logging in:
- DuelNavigator.HandleCustomInput(): log which navigator handled Enter

---

### Issue 4.2: Missing Zone Change Logging

**Status:** NOT FIXED

**Problem:** No log when _currentZone changes.

**Fix approach:**
- In ZoneNavigator.SetCurrentZone(): log old zone, new zone, caller
- Track who set zone for debugging
- This is part of the zone ownership model (Issue 1.3)

---

### Issue 4.3: Missing Focus Change Source Logging

**Status:** NOT FIXED

**Problem:** EventSystem selection changes but no log of which code set it.

**Fix approach:**
- Create wrapper method: SetFocusedGameObject(obj, callerName)
- All navigators use wrapper instead of direct EventSystem call
- Log each change with caller name
- This is part of Issue 2.6 fix

---

## Updated Implementation Order

**Session 1: Critical targeting fix + logging foundation**
1. Issue 1.1 - Add explicit ExitTargetMode after target selection (CRITICAL)
2. Issue 4.2 - Add zone change logging (helps diagnose remaining issues)
3. Issue 4.3 - Add focus change logging wrapper

**Session 2: Zone state coordination**
1. Issue 1.3 - Add zone ownership tracking to SetCurrentZone
2. Issue 1.4 - Fix TargetNavigator zone detection (use DetermineTargetZone)
3. Issue 2.6 - Route all SetSelectedGameObject through wrapper

**Session 3: Detection consistency**
1. Issue 2.2 - Unify HotHighlight detection methods
2. Issue 2.1 - Create TryEnterTargetMode unified method
3. Issue 3.2 - Fix stack count timing

**Session 4: UX polish**
1. Issue 2.4 - Add combat phase debounce
2. Issue 2.5 - Standardize deactivation cleanup (ZoneNavigator, BattlefieldNavigator)
3. Issue 1.2 - Exit targeting mode on explicit zone navigation

**Session 5: Performance**
1. Issue 3.1 - Add zone discovery caching
2. Issue 3.3 - Fix CardInfo/announcement mismatch

**Already addressed (verify in testing):**
- Issue 2.3 - HighlightNavigator rescan loop (uncommitted changes)
- Issue 4.1 - Targeting mode logging (partially done)

---

## Files to Modify

**Session 1:**
- TargetNavigator.cs - Add ExitTargetMode() call in SelectCurrentTarget()
- ZoneNavigator.cs - Add logging to SetCurrentZone()

**Session 2:**
- ZoneNavigator.cs - Add zone ownership enum and tracking
- TargetNavigator.cs - Add DetermineTargetZone() method
- All navigators - Use new SetFocusedCard wrapper

**Session 3:**
- CardDetector.cs - Add unified HasHotHighlight method
- TargetNavigator.cs - Add TryEnterTargetMode method
- DuelNavigator.cs, DuelAnnouncer.cs - Call TryEnterTargetMode

**Session 4:**
- DuelAnnouncer.cs - Add phase debounce
- ZoneNavigator.cs, BattlefieldNavigator.cs - Reset indices in Deactivate()

**Session 5:**
- ZoneNavigator.cs - Add discovery caching
- CardInfoNavigator.cs - Sync with announcement

---

## Testing Scenarios

After fixes, test these scenarios:

**Targeting mode (Session 1 focus):**
1. Cast spell requiring target, select target, immediately press C then Enter
2. Cast spell, select target, cast another spell requiring target
3. During combat, cast instant requiring target
4. Press Backspace to cancel targeting, then play different card

**Zone navigation (Session 2 focus):**
1. Press Tab to highlight, then C to hand, then Enter
2. Navigate battlefield with A/B/R, then Tab, then Enter
3. Press G for graveyard while spell on stack
4. Target a spell on the stack (verify zone shows Stack, not Battlefield)

**Combat:**
1. Rapid pass through empty combat phases
2. Cast instant during declare blockers
3. Assign multiple blockers

**Edge cases:**
1. Only battlefield cards playable (mana creatures)
2. Spell on stack with no valid targets
3. Multiple spells on stack

---

## Notes

- All fixes should maintain backwards compatibility with existing keybinds
- Do NOT revert commits 651c3d8 or 33deeb9 - fix forward with coordination
- Add logging first to confirm fix effectiveness
- Test with NVDA to ensure announcements remain correct
- Consider adding unit tests for navigator state machine
- Commit uncommitted HighlightNavigator and DuelAnnouncer changes before starting
