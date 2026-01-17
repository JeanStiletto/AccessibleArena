# Changelog

All notable changes to the MTGA Accessibility Mod.

## January 2026

### GeneralMenuNavigator Refactoring

Refactored `GeneralMenuNavigator` for improved maintainability and code organization.

**New Helper Classes:**
- `MenuScreenDetector` - Handles content controller detection, screen name mapping, visibility checks (Settings, Social, Carousel, Color Challenge)
- `MenuPanelTracker` - Manages panel/popup state tracking, overlay detection, popup announcements

**Changes:**
- Extracted ~400 lines from GeneralMenuNavigator into reusable helper classes
- Removed dead code: disabled post-activation rescan block and related fields
- Fixed corrupted method formatting (IsMainButton)
- Consolidated repeated `FindObjectsOfType<MonoBehaviour>` patterns into `GetActiveCustomButtons()` helper
- Better separation of concerns between navigation logic and state tracking

**Files:** `GeneralMenuNavigator.cs`, `MenuScreenDetector.cs` (new), `MenuPanelTracker.cs` (new)

---

### Input Field Accessibility

Full keyboard navigation support for text input fields (Add Friend, login, etc.):

**Features:**
- Editing mode detection - mod stops intercepting navigation keys when input field is focused
- Left/Right arrows navigate within text and announce character at cursor
- Up/Down arrows announce full field content with label
- Password fields announce "star" for characters, never reveal content
- Boundary announcements: "start" when at beginning, "end" when at end position
- Character names for punctuation (space, at, underscore, dash, etc.)

**Technical Changes:**
- `UIFocusTracker`: Added `IsEditingInputField()` static method to detect focused input fields
- `BaseNavigator`: Added `HandleInputFieldNavigation()` for cursor navigation while editing
- `BaseNavigator`: Added `AnnounceCharacterAtCursor()` and `AnnounceCurrentInputFieldContent()`
- `BaseNavigator`: Added `GetCharacterName()` for speakable character names
- `BaseNavigator`: Added `GetInputFieldLabel()` to extract labels from field names/placeholders
- `KeyboardManagerPatch`: Skip key blocking when editing input fields
- `Strings.cs`: Added input field navigation strings for localization

**Files:** `UIFocusTracker.cs`, `BaseNavigator.cs`, `KeyboardManagerPatch.cs`, `Strings.cs`

---

### Friends Menu (Social Panel) Accessibility

Added partial accessibility for the Friends/Social panel:

**New Features:**
- F4 key toggles Friends panel open/closed
- Tab navigation within Friends panel (Add Friend, settings buttons)
- Popup detection - new popups (like "Invite Friend") trigger automatic rescan
- Popup name announcement - "Invite Friend opened." when popup appears
- Input field support for friend invite text entry
- Backspace closes Friends panel

**Technical Changes:**
- `UIElementClassifier`: Added `IsInsideFriendsWidget()` helper to allow hitbox/backer elements inside Friends panel
- `UIElementClassifier`: Added filter for decorative Background elements without text
- `UITextExtractor`: Added `TryGetFriendsWidgetLabel()` to extract labels from parent object names (Button_AddFriend → "Add Friend")
- `UITextExtractor`: Added `TryGetInputFieldLabel()` for input field labeling
- `PanelStatePatch`: Added `SocialUIClosePrefix` to block Tab from closing Friends panel
- `GeneralMenuNavigator`: Added popup detection (`CheckForNewPopups()`) with instance ID tracking
- `GeneralMenuNavigator`: Added `CleanPopupName()` for human-readable popup announcements
- `GeneralMenuNavigator`: Added TMP_InputField discovery in element scanning
- `BaseNavigator`: Dynamic input field content update in announcements

**Known Limitations:**
- Not all Friends panel features accessible yet (friend list, status indicators)

**Files:** `UIElementClassifier.cs`, `UITextExtractor.cs`, `PanelStatePatch.cs`, `GeneralMenuNavigator.cs`, `BaseNavigator.cs`

---

### Unified HotHighlightNavigator

Replaced separate `TargetNavigator` + `HighlightNavigator` with unified `HotHighlightNavigator`.

**Key Discovery:** The game's HotHighlight system correctly manages what's highlighted based on game state - when targeting, only valid targets have HotHighlight; when not targeting, only playable cards have it.

**Changes:**
- Created `HotHighlightNavigator` that trusts the game's highlight system
- Scans ALL zones for HotHighlight (no hardcoded zone lists)
- Zone-based announcements and activation (hand: two-click, else: single-click)
- Removed ~40 lines of auto-detect/auto-exit logic
- Old navigators moved to `src/Core/Services/old/` for reference

**Files:** `HotHighlightNavigator.cs` (new), `DuelNavigator.cs`, `DuelAnnouncer.cs`, `ZoneNavigator.cs`, `BattlefieldNavigator.cs`

---

### Victory Screen & Targeting Mode Fixes

**Victory Screen:**
- Filter "Stop" buttons and duel prompt elements in MatchEndScene
- Rename `ExitMatchOverlayButton` to "Continue"
- Auto-focus Continue button

**Targeting Mode:**
- DuelNavigator auto-detection now requires both HotHighlight AND spell on stack
- Removed `EnterTargetMode()` call from UIActivator
- Fixed zone context not updating when Tab cycling through cards

**Files:** `UIElementClassifier.cs`, `UITextExtractor.cs`, `GeneralMenuNavigator.cs`, `DuelNavigator.cs`, `UIActivator.cs`

---

### Menu Accessibility: Deck Selection & Rewards Screens

**Deck Selection:**
- `IsHiddenByGameProperties()` now allows elements with `MainButton` component through even if disabled
- Added `HasMainButtonComponent()` helper

**Rewards:**
- Fixed reward cards showing as "+99" instead of card names
- Added fallback card name extraction that skips numeric indicators
- Added rewards overlay detection and "Rewards" screen announcement

**Files:** `UIElementClassifier.cs`, `CardDetector.cs`, `GeneralMenuNavigator.cs`

---

### Focus Management Overhaul

**Problem:** Player info zone consumed keys even when focus had moved elsewhere.

**Solution:** All navigators now manage EventSystem focus consistently:
- Player zone stores/restores previous focus on enter/exit
- Card navigators call `EventSystem.SetSelectedGameObject(card)` when navigating
- `HandleFocusChanged` no longer overwrites zone context
- Added emote button filtering and combat button filtering

**Files:** `PlayerPortraitNavigator.cs`, `ZoneNavigator.cs`, `BattlefieldNavigator.cs`, `HighlightNavigator.cs`, `CombatNavigator.cs`, `MTGAAccessibilityMod.cs`

---

### Mulligan Browser and Player Info Zone Fixes

**Mulligan:**
- Different UI flows for on-the-draw vs on-the-play
- Added `IsMulliganBrowserVisible()` detection
- `GetCardSelectionState()` returns null for viewing mode
- Added `PromptButton_Primary`/`Secondary` fallback

**Player Info Zone:**
- Fixed input priority (PortraitNavigator before BattlefieldNavigator)
- Life totals now read correctly from `GameManager.CurrentGameState`

**Files:** `BrowserNavigator.cs`, `DuelNavigator.cs`, `Strings.cs`

---

### Browser Navigator for Library Manipulation

New `BrowserNavigator` for scry, surveil, and similar effects:
- Detects `BrowserScaffold_*` GameObjects
- Cards in `BrowserCardHolder_Default` (keep) and `BrowserCardHolder_ViewDismiss` (bottom)
- Tab navigates, Space confirms

**Files:** `BrowserNavigator.cs` (new), `DuelNavigator.cs`, `DuelAnnouncer.cs`, `Strings.cs`

---

### Centralized Announcement Strings

Created `Core/Models/Strings.cs` to centralize all user-facing announcement text for future localization.

Also removed "Activated {name}" success announcements as clutter - only failures are announced now.

**Files:** `Strings.cs` (new), 20+ navigator and context files updated

---

### Menu Navigation Fixes

**Checkbox Toggle:**
- Added `force` parameter to `TriggerRescan()` to bypass debounce for toggle activations

**Position Preservation:**
- Fixed rescan position reset by using navigator's `_currentIndex` instead of EventSystem selection

**Files:** `GeneralMenuNavigator.cs`

---

### Improved Announcements and Navigation

**Removed Negative State Announcements:**
- Cards no longer announce "not selected", "not attacking", "not blocking"

**Battlefield Row Navigation:**
- Changed from Alt+Up/Down to Shift+Up/Down

**Enhanced Cast Announcements:**
- Now includes full card info (name, P/T, rules text)

**Complete Combat State Detection:**
- Detects all states via UI indicators: attacking, blocking, selected to block, can block, tapped

**Files:** `DiscardNavigator.cs`, `CombatNavigator.cs`, `BattlefieldNavigator.cs`, `DuelAnnouncer.cs`

---

### Declare Blockers Phase Implementation

- F/Space confirms blocks, Shift+F for no blocks
- Improved button detection (matches "Blocker", "Next", "Done", etc.)
- Fixed `IsCreatureBlocking()` to check active `IsBlocking` child
- Enemy attackers announce ", attacking" during blockers phase
- Blocker selection tracking with combined P/T announcements

**Files:** `CombatNavigator.cs`

---

### Combat Shortcuts and Bug Fixes

**New Shortcuts:**
- F key: "All Attack" / "X Attackers" button
- Shift+F: "No Attacks" button

**Bug Fixes:**
- Fixed `ExcludedScenes` using wrong scene name ("Duel" -> "DuelScene")
- Fixed NullReferenceException in `BaseNavigator.GetElementAnnouncement` for destroyed Unity objects

**Files:** `CombatNavigator.cs`, `GeneralMenuNavigator.cs`, `BaseNavigator.cs`

---

### Content Panel NavBar Filtering & Backspace Navigation

**NavBar Filtering:**
- Consolidated separate overlay flags into `_contentPanelActive`
- All content panels now filter NavBar from Tab navigation

**Backspace to Home:**
- Added Backspace key to navigate back to Home when in content panels

**Files:** `GeneralMenuNavigator.cs`

---

### UIActivator CustomButton Fix

**Problem:** Menu buttons produced sounds but didn't trigger state changes.

**Solution:** For CustomButtons, now tries pointer simulation FIRST (IPointerClickHandler), then onClick reflection.

**Files:** `UIActivator.cs`

---

### Model-Based Card Info Extraction

**Problem:** Battlefield cards in "compacted" state had hidden TMP_Text elements.

**Solution:** CardDetector now extracts from game's internal Model data:
- Name via `CardTitleProvider.GetCardTitle(grpId)`
- Mana cost via `PrintedCastingCost` ManaQuantity array
- Type line from `Supertypes` + `CardTypes` + `Subtypes`
- P/T via `StringBackedInt` properties
- Rules text via `AbilityTextProvider`
- Falls back to UI extraction for meta scene cards

**Files:** `CardDetector.cs`, `BattlefieldNavigator.cs`

---

### Player Info Zone and Input System

**Player Info Zone (V Key):**
- Enter zone, Left/Right switch players, Up/Down cycle properties
- Enter opens emote wheel, Escape/Tab exits

**Scene-Based Key Blocking:**
- `KeyboardManagerPatch` blocks Enter entirely in DuelScene
- Solves auto-skip, player info zone, and card playing issues

**Files:** `PlayerPortraitNavigator.cs` (new), `KeyboardManagerPatch.cs` (new), `InputManager.cs`
