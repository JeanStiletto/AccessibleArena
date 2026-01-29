# Element Grouping Feature Plan

## Goal
Break long menu lists into smaller, contextual groups for better blind user navigation. Instead of navigating 20-40+ elements linearly, users navigate a hierarchy: groups → elements within groups.

**Secondary goal:** Simplify the existing complex filtering architecture by consolidating multiple filtering layers into a unified grouping model.

---

## Current Status

### Completed (Phase 1-4)

- **Infrastructure Created:**
  - `ElementGroup.cs` - Group enum with overlay groups
  - `OverlayDetector.cs` - Simplified overlay detection
  - `ElementGroupAssigner.cs` - Parent pattern matching for group assignment
  - `GroupedNavigator.cs` - Two-level hierarchical navigation with folder support

- **Integration Completed:**
  - OverlayDetector integrated into GeneralMenuNavigator for ShouldShowElement and HandleBackNavigation
  - ElementGroupAssigner integrated into DiscoverElements()
  - GroupedNavigator navigation overrides (MoveNext, MovePrevious, MoveFirst, MoveLast)
  - HandleGroupedEnter and HandleGroupedBackspace for Enter/Backspace handling

- **Features Implemented:**
  - Hierarchical navigation (groups → elements)
  - Folder-based grouping for Decks screen (each folder becomes its own group)
  - Folder toggle activation through normal element path (triggers rescan properly)
  - Pending folder entry mechanism (preserves navigation across rescans)
  - Auto-deactivate folder toggle when exiting folder group
  - Primary elements as standalone items at group level (directly activatable)
  - Auto-enter only when exactly 1 group exists
  - Content group always available (even when folders have decks)
  - Objectives subgroup within Progress with full text extraction (quests show description + progress + reward)
  - Progress indicators show type + progress (e.g., "Daily: 0/15 wins, 250 gold")

### Decisions Made/Changed

| Decision | Original | Current |
|----------|----------|---------|
| Secondary group | Planned | **Removed** - elements fall to Content or Navigation |
| Primary group | Auto-enter first | **Standalone items** - shown directly at group level, not inside a group |
| Content group | Grouped together | **Standalone items** - each element shown directly at group level (like Primary) |
| Auto-enter | Primary + single-item groups | **Only when 1 total group** exists |
| Single-element groups | Created as groups | **Standalone** - groups with exactly 1 element become standalone items |
| ForegroundLayer | Remove entirely | **Kept for now** - still used for ContentPanel/Home filtering |
| Folder grouping | Not planned | **Added** - Decks screen folders become their own groups |
| Content with folders | Skip Content group | **Always show** - Content has filters/buttons even when inside folders |

---

## Current Behavior

### Home Screen Navigation
1. Enter screen: "Home. X groups. Play, X items." (Play group with all play-related elements)
2. Press Enter: "1 of X. Play, button."
3. Navigate: Direct Challenge, Rankings, Events inside the group
4. Press Backspace: "Play, X items." (back to group level)
5. Press Arrow Down: "Progress, X items." (Mastery, Gems, Wildcards, Objectives subgroup)
6. Enter Progress, navigate to "Objectives, X items", press Enter to see quests
7. Backspace from Objectives returns to Progress group

### Decks Screen Navigation
1. Enter screen: "Decks. 4 groups. Filters, 2 items."
2. Navigate to folder: "Meine Decks, 0 items." (folder with collapsed decks)
3. Press Enter:
   - Toggle activated through normal element path (triggers rescan)
   - Pending folder entry set
   - After rescan: auto-enters folder with discovered decks
   - "1 of 10. Deck Name, deck."
4. Navigate within folder: Left/Right arrows
5. Press Backspace:
   - Folder toggle deactivated
   - "Meine Decks, X items." (back to group level)
6. Content group available: dropdowns, color filters, buttons accessible

### Folder Entry Flow (Technical)
1. `HandleGroupedEnter()` detects folder group
2. Finds folder toggle in `_elements` list
3. Calls `ActivateCurrentElement()` → `OnElementActivated()` → `TriggerRescan()`
4. Calls `EnterGroup()` which sets `_pendingFolderEntry = folderName`
5. Rescan runs `OrganizeIntoGroups()`
6. `OrganizeIntoGroups()` checks `_pendingFolderEntry`, auto-enters folder
7. User is inside folder with decks visible

### Group State Restoration (January 2026)

When UI changes trigger a rescan (e.g., page navigation in collection), the group navigation state needs to be preserved so the user doesn't get kicked back to the group list.

**Problem:** `OrganizeIntoGroups()` resets `_currentGroupIndex = -1` and rebuilds all groups. User loses their position.

**Solution:** `SaveCurrentGroupForRestore()` / restore logic in `OrganizeIntoGroups()`:

1. Before rescan: `PerformRescan()` calls `_groupedNavigator.SaveCurrentGroupForRestore()`
2. Saves: current `ElementGroup` type and `NavigationLevel` (GroupList vs InsideGroup)
3. After groups rebuilt: `OrganizeIntoGroups()` finds matching group by type and restores position
4. If group no longer exists: falls back to first group

**Key Fields:**
- `_pendingGroupRestore` - ElementGroup to restore
- `_pendingLevelRestore` - NavigationLevel to restore

**Used by:**
- Page Up/Down in collection - changes page without losing position in Collection group
- Any rescan that should preserve user's current group context

### Subgroups (Nested Groups)

Some groups are nested within other groups for better organization. Currently:
- **Objectives** is a subgroup within **Progress**

**Objectives Subgroup Contents (7 items typical):**
- 3 Quests - shown with description + progress + reward (e.g., "Cast 20 spells, 14/20, 500 gold")
- Daily - shown with wins progress + gold reward (e.g., "Daily: 0/15 wins, 250 gold")
- Weekly - shown with wins progress (e.g., "Weekly: 5/15")
- Battle Pass Level - shown with level + EP progress (e.g., "Battle Pass Level: 7, 400/1000 EP")
- Spark Rank - shown with progress (e.g., "Spark Rank: 0/1")

**Text Extraction:** `UITextExtractor.TryGetObjectiveText()` extracts full info from objective elements:
- For quests: finds TextLine (description), Text_GoalProgress (progress), Circle (reward)
- For progress bars: extracts type from parent name + progress from Text_GoalProgress

**Subgroup Navigation Flow:**
1. Navigate to Progress group
2. Enter Progress - see elements like Mastery, Gems, Wildcards, and "Objectives, 7 items"
3. Navigate to "Objectives, 7 items" entry
4. Press Enter - enter the Objectives subgroup, see individual objectives
5. Navigate through objectives with Up/Down arrows
6. Press Backspace - return to Progress group (not to group list)
7. Press Backspace again - exit to group list

**Technical Implementation:**
- `GroupedElement.SubgroupType` field marks an element as a subgroup entry (no physical GameObject)
- `_subgroupElements` dictionary stores subgroup elements separately
- `_currentSubgroup` tracks when inside a subgroup
- `EnterSubgroup()` / `ExitSubgroup()` handle navigation
- `GetCurrentElementCount()` and `GetCurrentElement()` return subgroup data when inside a subgroup

**Adding New Subgroups:**
1. Create the ElementGroup enum value (e.g., `ElementGroup.NewSubgroup`)
2. Add assignment logic in `ElementGroupAssigner.DetermineGroup()`
3. In `GroupedNavigator.OrganizeIntoGroups()`:
   - Extract subgroup elements into `_subgroupElements`
   - Add a subgroup entry element to the parent group with `SubgroupType` set
4. The navigation logic (enter/exit/backspace) handles it automatically

### Overlay Handling
- Popup dialogs suppress all other groups
- Settings menu handled by SettingsMenuNavigator
- Play blade shows only PlayBlade group elements
- Social panel shows only Social group elements

### PlayBlade Navigation (via PlayBladeNavigationHelper)

The PlayBlade has a custom navigation hierarchy handled by `PlayBladeNavigationHelper`:

**Groups in PlayBlade:**
- `PlayBladeTabs` (display: "Tabs") - Events, Find Match, Recent tabs
- `PlayBladeContent` (display: "Play Options") - Ranked, Play, Brawl mode selectors
- `PlayBladeFolders` (display: "Folders") - Container showing all deck folders as selectable items
- Folder groups - Meine Decks, Starterdecks (individual deck folder contents)

**Navigation Flow:**
1. PlayBlade opens → auto-enter Tabs group
2. Enter on "Find Match" tab → auto-enter Play Options group (Ranked/Play/Brawl)
3. Enter on "Ranked" mode → auto-enter Folders group (list of all deck folders)
4. Navigate folders: "1 of 2: Meine Decks, 8 decks", "2 of 2: Starterdecks, 15 decks"
5. Enter on "Meine Decks" → enter that folder group, see decks
6. Enter on a deck → deck selected AND match automatically starts (auto-play)
7. Backspace in folder → back to Folders list
8. Backspace in Folders → back to Play Options (Content)
9. Backspace in Content → back to Tabs
10. Backspace in Tabs → close blade

**Auto-Play Feature:**
When a deck is selected in PlayBlade, the Play button is automatically pressed to start the match.
This streamlines the workflow - user just navigates to a deck and presses Enter to play.

**Technical Implementation:**
- Helper uses `GroupedNavigator.IsPlayBladeContext` flag (set on blade open/close)
- In PlayBlade context, deck folders are wrapped in a `PlayBladeFolders` group
- `PlayBladeFolders` contains folder toggles as elements; entering uses default folder logic
- `HandleGroupedEnter` in GeneralMenuNavigator detects PlayBladeFolders context and enters the corresponding folder GROUP
- Outside PlayBlade (Decks screen), folders remain as individual groups at top level
- `HandleEnter(element, group)` - handles tab/mode activation, returns `PlayBladeResult`
- `HandleBackspace()` - handles all backspace in PlayBlade context, returns `PlayBladeResult`
- `PlayBladeResult` enum: `NotHandled`, `Handled`, `RescanNeeded`, `CloseBlade`
- GeneralMenuNavigator calls helper first, then acts on the result

**Why special handling needed:**
- Tab clicks cause rapid blade Hide/Show events that would reset normal state tracking
- Mode selection (Ranked/Play/Brawl) doesn't trigger panel changes, so manual rescan needed
- Backspace behavior differs from normal groups (hierarchical: folder→folders→content→tabs→close)
- Folder groups need to be presented as a list in PlayBlade, not as individual top-level groups

---

## ElementGroup Enum (Current)

```csharp
public enum ElementGroup
{
    Unknown = 0,      // Hidden in grouped mode
    Primary,          // Main actions: Submit, Continue (shown as standalone)
    Play,             // Play-related: Play button, Direct Challenge, Rankings, Events (grouped together)
    Progress,         // Progress-related: Boosters, Mastery, Gems, Gold, Wildcards + Objectives subgroup
    Objectives,       // Subgroup of Progress: Quests, Daily, Weekly, Battle Pass, Spark Rank
    Social,           // Social elements: Profile, Achievements, Mail
    Filters,          // Search, sort, filter toggles
    Content,          // Deck entries, cards, list items, dropdowns, buttons (shown as standalone)
    Settings,         // Settings controls (when not full overlay)
    Secondary,        // (REMOVED - not used)

    // Overlay groups (only one visible at a time)
    Popup,            // Modal dialog elements
    FriendsPanel,     // Friends panel elements
    PlayBladeTabs,    // Play blade tabs (Events, Find Match, Recent)
    PlayBladeContent, // Play blade mode selectors (Ranked, Play, Brawl)
    PlayBladeFolders, // Play blade folders container (groups deck folders)
    SettingsMenu,     // Settings menu overlay
    NPE,              // New Player Experience overlay (excludes Objective_NPE elements)
    DeckBuilderCollection, // Deck builder collection cards (PoolHolder grid)
    DeckBuilderDeckList,   // Deck builder deck list cards (MainDeck_MetaCardHolder compact list)
}
```

---

## Files Structure

```
src/Core/Services/ElementGrouping/
  ElementGroup.cs                 - Enum + extension methods
  OverlayDetector.cs              - Overlay detection (replaces some ForegroundLayer)
  ElementGroupAssigner.cs         - Group assignment via parent patterns
  GroupedNavigator.cs             - Two-level navigation state machine with folder support
  PlayBladeNavigationHelper.cs    - PlayBlade-specific Enter/Backspace handling
                                    (includes PlayBladeResult enum)

src/Core/Services/
  UITextExtractor.cs              - Text extraction including TryGetObjectiveText() for objectives
  MenuDebugHelper.cs              - Debug utilities including DumpGameObjectDetails()
```

---

## Next Steps

### Phase 4: Screen-Specific Tuning (MOSTLY COMPLETE)

**Home Screen:**
- [x] Play group containing Play button, Direct Challenge, Rankings, Events
- [x] Navigation group (navbar items)
- [x] Content group (carousel, other items)
- [ ] Review if any elements are misclassified

**Decks Screen:**
- [x] Folder-based grouping
- [x] Auto-toggle folder checkboxes on enter
- [x] Auto-deactivate folder toggle on exit (backspace)
- [x] Pending folder entry (preserves state across rescan)
- [x] Content group always available (filters, dropdowns, buttons)
- [x] Test with multiple folders
- [x] Color filters classified as Filters (added DeckColorFilters pattern)

**Collection Screen:**
- [ ] Tune mana color filters
- [ ] Card grid content grouping
- [ ] Test filter interactions

**Store Screen:**
- [ ] Review store content grouping
- [ ] Test pack/bundle navigation

### Phase 5: Code Cleanup

**Remove unused code from GeneralMenuNavigator:**
- [ ] `IsChildOfSocialPanel()` - now handled by OverlayDetector
- [ ] `IsInsideNPEOverlay()` - now handled by OverlayDetector
- [ ] `IsInsideBlade()` - still used in ContentPanel filtering, review if can remove
- [ ] `IsMainButton()` - still used in ContentPanel filtering, review if can remove

**Note:** ForegroundLayer and `GetCurrentForeground()` are still needed for:
- ContentPanel filtering (Collection, Store, etc.)
- Home filtering (show only home content + navbar)

These can potentially be replaced with group-based filtering later, but require careful testing.

### Phase 6: Polish & Testing

- [ ] Update F1 help with group navigation instructions
- [ ] Test all overlay cases (popup, social, play blade, settings)
- [ ] Test screen transitions
- [ ] Test popup from Settings (layered overlays)
- [ ] Performance testing with many elements

---

## Critical Testing Checklist

### Overlay Suppression
- [x] Play blade suppresses other groups
- [x] Social panel suppresses other groups (F4 toggle)
- [x] Popup dialogs suppress other groups
- [x] Settings menu handled by SettingsMenuNavigator
- [ ] Nested overlays (popup over settings)

### Group Navigation
- [x] Arrow Up/Down navigates groups at group level
- [x] Arrow Up/Down navigates elements inside group
- [x] Enter on standalone element activates directly
- [x] Enter on normal group enters it
- [x] Backspace from inside group exits to group level
- [x] Backspace from group level does normal back navigation
- [x] Home/End work at both levels

### Folder Groups (Decks)
- [x] Folders become separate groups
- [x] Empty folders shown (toggle found, 0 decks until activated)
- [x] Entering folder group activates folder checkbox
- [x] Rescan discovers newly visible decks
- [x] Pending folder entry preserves navigation across rescan
- [x] Exiting folder group deactivates folder checkbox
- [x] Deck elements properly grouped by folder
- [x] Content group available while in folder view

### Screen-Specific
- [x] Home: Play group (Play button, Direct Challenge, Rankings, Events)
- [x] Home: Navigation and Content groups
- [x] Decks: Folder grouping with toggle activation
- [x] Decks: Content group with filters/buttons
- [ ] Collection: Card filters work
- [ ] Store: Content properly grouped

---

## Known Issues / Limitations

1. **ForegroundLayer still needed** - ContentPanel and Home filtering still use the old ForegroundLayer system. Full removal requires more work to handle these cases with group-based filtering.

2. **Secondary group removed** - Elements that would have been Secondary now fall to Content or Navigation. May need to revisit if this causes issues.

3. **Auto-enter behavior** - Only triggers when exactly 1 group. Standalone elements at group level are directly activatable.

4. **Single-element groups** - Non-folder groups with exactly 1 element become standalone (shown directly at group level). Folder groups always remain groups regardless of element count (0 elements allowed for collapsed folders, 1+ elements keep folder context and exit behavior).

5. ~~**Color filters classification**~~ - Fixed: Added `DeckColorFilters` pattern to `IsFilterElement()`.

6. ~~**Folder toggle on already-visible folder**~~ - Fixed: Now checks `toggle.isOn` before activating. Entering an already-visible folder no longer toggles it off.

7. **NPE overlay exclusion** - `Objective_NPE` elements (like Spark Rank) are excluded from NPE overlay classification so they can be grouped with Objectives. This might affect NPE tutorial screens if any tutorial elements have "Objective_NPE" in their path. See KNOWN_ISSUES.md for details.

8. **Deck list quantity button exclusion** - `CustomButton - Tag` elements (showing "4x", "2x" quantities) must be excluded in `DetermineGroup()` directly, NOT in `DetermineOverlayGroup()`. Returning `Unknown` from `DetermineOverlayGroup()` just means "not an overlay" and causes the element to fall through to Content group. The exclusion check must happen before the final `return ElementGroup.Content` in `DetermineGroup()`.

---

## Success Criteria

1. [x] Overlay cases work (popup, social, play blade)
2. [x] Hierarchical navigation works (groups → elements)
3. [x] Folder grouping works on Decks screen
4. [x] Folder toggle activation triggers proper rescan
5. [x] Pending folder entry preserves state across rescan
6. [x] Content group always available (even with folders)
7. [x] Primary elements are standalone and directly activatable
8. [ ] All main screens properly grouped (Home, Decks, Collection, Store)
9. [ ] No regressions in existing navigation
10. [ ] Code simplified (IsChildOf methods removed where possible)
