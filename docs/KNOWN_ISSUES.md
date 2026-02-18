# Known Issues

Active bugs, limitations, and planned work for Accessible Arena.

For resolved issues and investigation history, see docs/old/RESOLVED_ISSUES.md.

## Active Bugs

### Space Key Pass Priority

Game's native Space keybinding doesn't work reliably after using mod navigation. HotHighlightNavigator now clicks the primary button directly as workaround.

---

### Spell Resolved Announcement Too Early or Repeated

"Spell resolved" announcement sometimes fires too early or multiple times for a single spell.

---

### Card Abilities With High IDs Not Resolving

Some cards have ability IDs greater than ~100000 that cannot be resolved correctly. These abilities fail to display proper names or descriptions.

---

### Battlefield Cards Splitting Into Two Stacks

Cards on the battlefield sometimes split into two separate stacks/rows when they should be grouped together.

---

### Store Not Closing Correctly

Store screen does not always close properly, which can leave navigation in an unexpected state.

## Needs Testing

### PlayBlade Queue Type Selection

The PlayBlade "Find Match" was restructured into three queue type tabs (Ranked, Open Play, Brawl) at the top tab level. Several aspects need further testing:

**Mode selection correctness:**
- Unclear if selecting a queue type tab (e.g., "Ranked") always correctly sets the game's internal mode
- The two-step activation (click FindMatch tab -> click queue type tab) relies on timing and rescans
- Edge case: switching between queue types rapidly may leave the game in an unexpected mode state

**BO3 toggle:**
- The "Best of 3" checkbox is now labeled correctly (was "POSITION" placeholder)
- Needs testing whether toggling it actually changes the match format

**Files:** `PlayBladeNavigationHelper.cs`, `GroupedNavigator.cs`, `ElementGroupAssigner.cs`, `GeneralMenuNavigator.cs`

---

### Unwanted Secondary Buttons in Tab Order

Sometimes secondary or irrelevant buttons appear in the Tab navigation order during duels. These buttons should be filtered out but occasionally slip through.

---

### Damage Assignment Browser

Damage assignment browser (e.g., when distributing combat damage across multiple blockers) needs testing for correct navigation and value assignment.

---

### London Mulligan With Very Low Card Counts

Mulliganing down to 3 or fewer cards may behave incorrectly. Needs testing whether card selection and bottom placement still works correctly at very low hand sizes.

## Technical Debt

### Code Archaeology

Accumulated defensive fallback code needs review:
- `ActivateBackButton()` has 4 activation methods - test which are needed
- `LogAvailableUIElements()` (~150 lines) only runs once per screen in DetectScreen (removed from PerformRescan)
- Extensive logging throughout - review what's still needed

**Priority:** Low

---

### WinAPI Fallback (UIActivator.cs)

~47 lines of commented WinAPI code. Test if still needed, remove if stable without it.

**Location:** `UIActivator.cs` lines 13-59

---

### Performance

- Multiple `FindObjectsOfType` calls in `DiscoverElements`
- Repeated `GameObject.Find` calls in back navigation

---

### Announcement Compaction for Zone Transitions

Zone change events can create redundant announcements when a creature dies and goes to graveyard:
- Current: "X died" followed by "X went to graveyard" (two announcements)
- Ideal: Single combined announcement

**Consideration:** Compact announcements when zone-leaving and zone-reaching events happen close together.

**Risk:** Events can occur between dying and zone change (e.g., death triggers, replacement effects). Compacting could cause missed information or incorrect announcements.

**Priority:** Low - current behavior is functional, just slightly verbose

---

### Dropdown Auto-Open Suppression Mechanics

MTGA auto-opens dropdowns when they receive EventSystem selection. When user navigates to a dropdown with arrow keys, we set EventSystem selection, game auto-opens it. Auto-opened dropdowns are immediately closed; user must press Enter to open.

**Announcement Ownership:**
- `UIFocusTracker.NavigatorHandlesAnnouncements` - When a navigator is active, UIFocusTracker skips announcements (navigators handle their own). Set each frame from `NavigatorManager.HasActiveNavigator`.
- Exception: When a dropdown is open (`DropdownStateManager.IsInDropdownMode`), UIFocusTracker still announces because Unity's native navigation controls dropdown item focus.

**Dropdown State (DropdownStateManager):**
- `IsInDropdownMode` - Queries actual `IsExpanded` property, accounts for suppression after auto-close
- `SuppressReentry()` - Prevents re-entering dropdown mode while `IsExpanded` is still true after `Hide()` call
- `UpdateAndCheckExitTransition()` - Detects dropdown close for navigator index sync (handles game auto-advance like Month->Day->Year)

**Files:** `UIFocusTracker.cs`, `DropdownStateManager.cs`, `AccessibleArenaMod.cs`

## Potential Issues (Monitor)

### Vault Progress Objects in Packs

Pack opening sometimes shows multiple identical "Alchemy Bonus Vault Progress +99" items alongside actual cards (e.g., 6 cards + 3 vault progress = 9 items). This appears to be game behavior, not a mod bug.

---

### NPE Overlay Exclusion for Objective_NPE Elements

Changed `ElementGroupAssigner.DetermineOverlayGroup()` to exclude `Objective_NPE` elements from NPE overlay classification. This allows SparkRank (Objective_NPEQuest) to be grouped with other Objectives instead of being treated as an NPE tutorial overlay element.

**Monitor for:** This might break NPE tutorial screens if any tutorial elements have "Objective_NPE" in their path.

**Files:** `ElementGroupAssigner.cs`

---

### Card Info Navigation (Up/Down Arrows)

Reported once: Up/Down arrows stopped reading card details during a duel. Could not reproduce. Diagnostic logging added.

**If it happens again:** Check log for `[CardInfo]` entries around the time of the issue.

**Files:** `CardInfoNavigator.cs`, `AccessibleArenaMod.cs`

---

### Container Element Filtering

Elements with "Container" in name + 0x0 sizeDelta are skipped. Some legitimate containers might be incorrectly filtered.

**If a button stops working:** Check `UIElementClassifier.ShouldBeFiltered()`.

## Design Decisions

### Panel Detection Architecture

Hybrid approach using three detectors:
- **HarmonyPanelDetector** - Event-driven for PlayBlade, Settings, Blades (critical for PlayBlade which uses SLIDE animation, not alpha fade)
- **ReflectionPanelDetector** - Polls IsOpen properties for Login panels, PopupBase
- **AlphaPanelDetector** - Watches CanvasGroup alpha for dialogs, modals, popups

`GetCurrentForeground()` is single source of truth for both element filtering and backspace navigation.

---

### Tab and Arrow Navigation

Both Tab and Arrow keys navigate menu elements identically. Unity's EventSystem was unreliable. May simplify to arrow-only in future.

---

### Hybrid Navigation System (EventSystem Management)

We run a parallel navigation system alongside Unity's EventSystem, selectively managing when Unity is allowed to participate. This creates complexity but is necessary for screen reader support.

**What we do:**
- Maintain our own navigation state (`_currentIndex`, `_elements` list)
- Announce elements via screen reader (Unity doesn't do this)
- Consume/block keys to prevent Unity/MTGA from also processing them
- Clear or set `EventSystem.currentSelectedGameObject` strategically

**Why it's necessary:**
1. Unity's navigation has no screen reader support
2. MTGA's navigation is inconsistent and has gaps
3. Some elements auto-activate when selected (input fields, toggles)
4. MTGA's KeyboardManager intercepts keys for game shortcuts

**Problem areas requiring special handling:**
- **Input fields:** MTGA auto-focuses them asynchronously, so we deactivate or clear EventSystem selection for arrow navigation
- **Toggles:** Unity/MTGA re-toggles when EventSystem selection is set, so we clear selection for arrow navigation
- **Dropdowns:** We let Unity handle internal arrow navigation, then re-sync our index

**Files:** `BaseNavigator.cs`, `UIFocusTracker.cs`, `KeyboardManagerPatch.cs`

## Planned Features

### Immediate

1. Mana pool info - expand to show color breakdown
2. Stepper control redesign

---

### Mana Pool Info (Research Notes)

The mana pool UI exists and is readable, but only shows total count, not color breakdown.

**What We Found (January 2026):**
- **UI Path:** `UIElements/ManaPool/Content/ManaPoolButton(Clone)`
- **How it's read:** FocusTracker extracts text when focused (via Tab during mana payment)
- **Current output:** Just a number (e.g., "1")
- **Missing:** Color breakdown (e.g., "1 Green, 2 Blue")

**Next Steps:**
1. Investigate ManaPool children - may have per-color elements
2. Check if game has ManaPool data accessible via reflection
3. Look for mana symbols/icons that indicate colors
4. Consider adding a dedicated shortcut to read mana pool during duels

### Upcoming

1. Creature death/exile/graveyard announcements with card names
2. Player username announcements
3. Game wins display (WinPips)
4. Brawl accessibility - Brawl game mode navigation and format-specific handling
5. Token state on cards - announce token/copy status when reading card info
6. Smart mana announcement - announce available mana with color breakdown from game state
7. Settings menu improvements - better sorting of options and clearer display of checkmarks/toggle states
8. Browser announcements - shorter, less verbose; only announce when it is the player's browser (not opponent's)
9. Mulligan overview announcement - announce hand summary when mulligan opens (e.g., card count, notable cards)
10. Better group announcements - improve how element groups are announced when entering/switching groups
11. Loading screen announcement cleanup - reduce repetitive announcements during loading screens
12. Loading screen announcements - less repetition, cleaner output during screen transitions
13. Better combat announcements when multiple attackers - clearer announcement when two or more enemies are attackable
14. K hotkey for mark/counter information on cards - announce +1/+1 counters, damage marks, and other markers
15. Ctrl+key shortcuts for navigating opponent's cards - additional Ctrl-modified zone shortcuts for quick opponent board access
16. Card crafting - wildcard crafting workflow accessibility
17. Planeswalker support - loyalty abilities, activation, and loyalty counter announcements
18. Phase skip warning - warn when passing priority would skip a phase where the player could still play cards (e.g., skipping main phase with mana open)
19. Pass entire turn shortcut - quick shortcut to pass priority for the whole turn (may already exist as Shift+Enter in the game, just needs to be enabled/announced)

### Future

1. Draft/sealed events
2. Full deck editing workflow (add/remove cards, save deck)
3. Single-key info shortcuts (inspired by Hearthstone Access)
   - Quick status queries without navigation
   - Benefits: Faster information access, less navigation needed

   **Priority shortcuts to implement:**

   **M / Shift+M - Mana Announcement**
   - M: Announce "X mana available of Y total" (your mana)
   - Shift+M: Announce opponent's mana
   - Requires: Finding mana data from game state (not currently extracted)

   **K - Keyword Explanation**
   - When focused on a card, K announces keyword abilities with definitions
   - Example: "Flying. This creature can only be blocked by creatures with flying or reach."
   - Requires: Keyword detection from card rules text + keyword definition database

   **O - Game Log (Play History)**
   - O: Announce recent game events (last 5-10 actions)
   - Example: "Opponent played Mountain. You drew Lightning Bolt. Opponent attacked with Goblin."
   - Requires: Tracking game events in DuelAnnouncer and storing history

4. Verbose "Big Card" announcements (inspired by Hearthstone Access)
   - Option to include card details inline with action announcements
   - User preference toggle: brief vs verbose announcements
