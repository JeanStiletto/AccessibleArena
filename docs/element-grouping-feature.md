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

### Decisions Made/Changed

| Decision | Original | Current |
|----------|----------|---------|
| Secondary group | Planned | **Removed** - elements fall to Content or Navigation |
| Primary group | Auto-enter first | **Standalone items** - shown directly at group level, not inside a group |
| Content group | Grouped together | **Standalone items** - each element shown directly at group level (like Primary) |
| Auto-enter | Primary + single-item groups | **Only when 1 total group** exists |
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
5. Press Arrow Down: "Navigation, 14 items."

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

### Overlay Handling
- Popup dialogs suppress all other groups
- Settings menu handled by SettingsMenuNavigator
- Play blade shows only PlayBlade group elements
- Social panel shows only Social group elements

---

## ElementGroup Enum (Current)

```csharp
public enum ElementGroup
{
    Unknown = 0,      // Hidden in grouped mode
    Primary,          // Main actions: Submit, Continue (shown as standalone)
    Play,             // Play-related: Play button, Direct Challenge, Rankings, Events (grouped together)
    Navigation,       // Nav bar, tabs, back buttons
    Filters,          // Search, sort, filter toggles
    Content,          // Deck entries, cards, list items, dropdowns, buttons (shown as standalone)
    Settings,         // Settings controls (when not full overlay)
    Secondary,        // (REMOVED - not used)

    // Overlay groups (only one visible at a time)
    Popup,            // Modal dialog elements
    Social,           // Friends panel elements
    PlayBlade,        // Play blade elements (inside play menu)
    SettingsMenu,     // Settings menu overlay
    NPE,              // New Player Experience overlay
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

3. **Auto-enter behavior** - Only triggers when exactly 1 group. Single-item groups within multiple groups require Enter to activate.

4. ~~**Color filters classification**~~ - Fixed: Added `DeckColorFilters` pattern to `IsFilterElement()`.

5. ~~**Folder toggle on already-visible folder**~~ - Fixed: Now checks `toggle.isOn` before activating. Entering an already-visible folder no longer toggles it off.

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
