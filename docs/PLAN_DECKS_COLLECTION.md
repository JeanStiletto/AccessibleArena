# Plan: Decks and Collection Screen Accessibility

## Overview

This plan addresses accessibility issues in the **Decks** (DeckManagerController) and **Collection** (WrapperDeckBuilder) screens.

---

## Completed (Phase 1)

### 1.1 Fix Deck Entry Duplicates - DONE
**Location**: `GeneralMenuNavigator.cs`, `DiscoverElements()` method

**Solution implemented**: Track ALL elements inside deck entries (`allDeckElements`), identify only the "UI" main buttons to keep (`deckMainButtons`), skip everything else. This filters out:
- TextBox elements (deck name edit fields)
- Any other duplicate elements inside DeckView_Base

### 1.2 Improve Color Filter Names - DONE
**Location**: `UIElementClassifier.cs`, `CleanObjectName()` method

**Solution implemented**: Detect `CardFilterView Color_*` and `CardFilterView Multicolor` patterns, extract just the color name:
- "Card Filter View Color White" → "White"
- "Card Filter View Multicolor" → "Multicolor"

### 1.3 Rename Clear Search Button - DONE
**Location**: `UIElementClassifier.cs`, `GetCleanLabel()` and `CleanObjectName()` methods

**Solution implemented**:
- Special case in `GetCleanLabel` to skip extracted text for "Clear Search Button" (was picking up placeholder "Suche ..." from sibling)
- Explicit rename in `CleanObjectName` to return "Clear search"

### 1.4 Filter Expand Buttons - DONE
**Location**: `UIElementClassifier.cs`, `IsFilteredByNamePattern()` method

**Solution implemented**:
- Added `IsInsidePreferredPrintingTag()` helper to detect elements inside `TAG_PreferredPrinting`
- Filter "ExpandButton"/"expand button" when inside preferred printing tags
- Removes card style/printing selection buttons that flooded collection view

---

## Deck Folder Structure (Investigation Complete)

### Hierarchy

```
DeckViewSelector_Base(Clone)
└── Viewport
    └── Container
        ├── DeckFolder_Base(Clone)  [Meine Decks]
        │   ├── Folder_Toggle (checkbox - "Meine Decks")
        │   └── Toggle_CONTAINER
        │       └── Content - Grid - Valid Decks
        │           ├── NewDeckButton_Base(Clone)  [only in user folder]
        │           ├── DeckView_Base(Clone)
        │           │   ├── UI (CustomButton - deck selection)
        │           │   └── TextBox (TMP_InputField - deck name edit)
        │           └── ... more DeckView_Base entries
        │
        └── DeckFolder_Base(Clone)  [Starterdecks]
            ├── Folder_Toggle (checkbox - "Starterdecks")
            └── Toggle_CONTAINER
                └── Content - Grid - Valid Decks
                    ├── DeckView_Base(Clone)
                    └── ... more DeckView_Base entries
```

### Key Findings

1. **Separate folder containers** - Each folder type has its own `DeckFolder_Base(Clone)` instance

2. **Folder toggle controls visibility** - `Folder_Toggle` is the checkbox that expands/collapses the folder's content (not a filter)

3. **No deduplication by game** - When both folders are checked, sighted users see BOTH sections with their respective decks. Same deck name can appear in both folders (once per folder section)

4. **Visual separation for sighted users** - Folders have headers (the toggle checkboxes), decks are grouped under their respective headers

5. **Current accessibility gap** - We flatten everything into one list without folder context, so blind users hear duplicate deck names without knowing which folder they belong to

### Folder Pattern Reusable

This folder pattern (`DeckFolder_Base` with `Folder_Toggle` and `Toggle_CONTAINER`) likely appears in:
- Deck Manager screen
- Deck selection in Play menu
- Possibly other places with collapsible sections

---

## Remaining Work

### Phase 2: Folder-Aware Navigation

#### 2.1 Add Folder Context to Deck Names
**Priority**: High

**Options**:
1. Prefix deck names with folder: "Meine Decks: Arkane Luftakrobaten, deck"
2. Announce folder transitions when navigating between folders
3. Group navigation strictly by folder with folder headers announced

**Implementation**: Walk up from each DeckView_Base to find parent DeckFolder_Base, get Folder_Toggle's text label.

#### 2.2 Handle Duplicate "Sammlung" Buttons
**Priority**: Medium

Two "Sammlung, button" entries still appear. Need to filter one based on path or keep only first by position.

### Phase 3: Collection Screen

#### 3.1 Detect Cards in Collection
**Priority**: High

Cards in collection aren't being added to navigation. Need to:
- Find cards in `PoolHolder_Desktop_16x9(Clone)`
- Use `CardDetector.IsCard()` for detection
- Handle virtualized list (only visible cards in hierarchy)

#### 3.2 Add Pagination Controls
**Priority**: Medium

"Next, navigation" appears but could be clearer:
- "Next page, button"
- "Previous page, button"

### Phase 4: Structural Organization

#### 4.1 Element Ordering for Decks Screen
Ideal order:
1. Dropdowns (filter, sort)
2. Search field
3. Color filters (W, U, B, R, G)
4. Folder toggles with their decks grouped
5. Action buttons (Sammlung, Deck verändern)

#### 4.2 Consider Dedicated Navigators
If complexity grows, consider:
- `DeckManagerNavigator` for Decks screen
- `CollectionNavigator` for Collection screen

---

## Detection Patterns

### Deck Folder Detection
- Parent pattern: `DeckFolder_Base(Clone)`
- Toggle: `Folder_Toggle` child with text label
- Content: `Toggle_CONTAINER/Content - Grid - Valid Decks`
- Deck entries: `DeckView_Base(Clone)` with `UI` and `TextBox` children

### Color Filter Detection
- Name pattern: `CardFilterView Color_*` or `CardFilterView Multicolor`
- Component: Unity `Toggle`
- Colors: White, Blue, Black, Red, Green, Colorless, Multicolor

### Collection Screen Detection
- Scene: `DeckBuilder`
- Panel: `WrapperDeckBuilder` IsOpen
- Cards: Inside `PoolHolder_Desktop_16x9(Clone)`
- Filters: `CardFilterView` toggles in header
