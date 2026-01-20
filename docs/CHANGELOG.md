# Changelog

All notable changes to the MTGA Accessibility Mod.

## January 2026

### Zone Transfer Announcements - Land Plays and More

Enhanced the DuelAnnouncer to parse individual zone transfer events and announce specific game state changes with card names.

**Land Play Announcements:**
- "You played [land name]" when you play a land
- "Opponent played [land name]" when opponent plays a land
- Ownership detection uses zone strings (FromZone contains player ownership info)

**Additional Zone Transfer Announcements (when GrpId available):**
- **Battlefield entries:**
  - Token creation: "You/Opponent created [name] token"
  - From graveyard: "[name] returned from graveyard"
  - From exile: "[name] returned from exile"
  - From library: "[name] enters battlefield from library"
- **Graveyard entries:**
  - From battlefield: "[name] died" / "was destroyed" / "was sacrificed"
  - From hand: "[name] was discarded"
  - From stack: "[name] was countered"
  - From library: "[name] was milled"
- **Exile entries:** "[name] was exiled" (with source zone context)
- **Hand entries (bounce):** "[name] returned to hand"

**Technical Details:**
- Parses `ZoneTransferGroup` event's `_zoneTransfers` list for individual `ZoneTransferUXEvent` items
- Extracts card GrpId from `NewInstance.Printing.GrpId` or direct `NewInstance.GrpId`
- Uses `Reason` field for specific language (Died, Destroyed, Sacrificed, Countered, Discarded, Milled)
- Ownership detection: checks `FromZone` string for "LocalPlayer" or "Opponent" patterns
- Land detection: checks `IsBasicLand`, `IsLandButNotBasic` properties, or matches basic land names

**Known Limitation:**
Creature deaths from combat don't generate `ZoneTransferGroup` events (battlefield → graveyard transfers not included). These still use generic count-based announcements from `UpdateZoneUXEvent`.

**Files:** `DuelAnnouncer.cs`

---

### Friends Panel Fixes

Fixed Friends panel navigation that was broken after code quality refactoring.

**Bug 1: "Backer" elements filtered by "back" button filter**
- The back button filter (`ContainsIgnoreCase(name, "back")`) was matching "Backer_Hitbox" elements
- Fix: Added exclusion for "backer" in the back button filter

**Bug 2: Popup detection not matching resolution-suffixed names**
- Popup detection used `EndsWith("Popup(Clone)")` which didn't match `InviteFriendPopup_Desktop_16x9(Clone)`
- Fix: Changed to `Contains("Popup") && EndsWith("(Clone)")`

**Bug 3: Popup elements filtered to Social panel instead of popup**
- When InviteFriendPopup opened, elements were still filtered to the Friends panel underneath
- Fix: Moved popup priority check before social panel check in `IsInForegroundPanel()`

**Now Working:**
- Friends panel shows all elements (friend list, add friend button, header)
- Add Friend popup opens and shows input field + invite button
- Full input field editing with Enter to submit
- Escape closes popup, Backspace closes Friends panel

**Files:** `UIElementClassifier.cs`, `MenuPanelTracker.cs`, `GeneralMenuNavigator.cs`

---

### BaseNavigator Code Quality Refactoring

Refactored BaseNavigator for improved code quality, reduced duplication, and better performance.

**New Helpers:**
- Added `IsValidIndex` property - eliminates repetitive `_currentIndex >= 0 && _currentIndex < _elements.Count` checks (was duplicated 6 times)
- Added `InputFieldInfo` struct and `GetFocusedInputFieldInfo()` helper - consolidates TMP_InputField and legacy InputField handling
- Added `_editingInputField` cache - avoids expensive `FindObjectsOfType` during input field navigation

**Consolidated Patterns:**
- Renamed `PrepareCardNavigationForCurrentElement()` to `UpdateCardNavigation()` - now includes `SupportsCardNavigation` check internally, callers don't need to wrap
- Refactored `AnnounceCharacterAtCursor()` - uses cached field instead of searching all fields, fixed redundant condition
- Refactored `AnnounceCurrentInputFieldContent()` - reduced from 56 lines to 23 lines by using shared helper

**Namespace Normalization:**
- Changed all `Models.Strings` references to `Strings` for consistency
- Changed all `Models.AnnouncementPriority` references to `AnnouncementPriority`

**UIFocusTracker Dropdown Validation:**
- Added validation to `IsEditingDropdown()` to match `IsEditingInputField()` pattern
- Now verifies focus is still on a dropdown item before returning true
- Auto-exits dropdown mode if focus moved away (prevents stale state)

**Files:** `BaseNavigator.cs`, `UIFocusTracker.cs`

---

### UI Handling Code Quality Rework

Completed refactoring of UIElementClassifier and UIActivator for improved code quality, readability, and maintainability.

**UIActivator Changes:**
- Added caching to `IsTargetingModeActive()` with 0.1s timeout (avoids expensive `FindObjectsOfType` scans every call)
- Extracted `ScanForSubmitButton()` helper method
- Extracted `IsCustomButton()` helper (was duplicated 4+ times)
- Extracted timing constants (`CardSelectDelay`, `CardPickupDelay`, `CardDropDelay`)
- Added type name constants for reflection

**UIElementClassifier Changes:**
- **Major refactor:** Split 260-line `Classify()` method into 14 focused `TryClassifyAs*` methods
- Main method is now a clean null-coalescing chain showing classification priority at a glance
- Consolidated `GetEffectiveToggleName()` and `GetEffectiveButtonName()` into single `GetEffectiveElementName()` method
- Added `ParentLabelPrefixes` array for extensible prefix stripping
- Added `CreateResult()` helper to reduce boilerplate
- Added `GetDropdownSelectedValue()` to consolidate dropdown value extraction
- Extracted constants (visibility thresholds, search depth limits, filter patterns)
- Added `SplitCamelCase()` and `CleanSettingLabel()` helpers

**Code Quality Improvements:**
- Reduced code duplication
- Better separation of concerns
- Cleaner method signatures
- More readable classification flow

**Files:** `UIActivator.cs`, `UIElementClassifier.cs`, `KNOWN_ISSUES.md` (WinAPI technical debt note)

---

### Logout Button Fix (UIActivator Order)

Fixed logout button (Link_LogOut) and similar buttons not responding to Enter key.

**Problem:**
Buttons with BOTH `Button` and `CustomButton` components (like Link_LogOut, Link_Account) weren't activating properly. UIActivator found the standard Unity `Button` first and called `button.onClick.Invoke()`, but the actual action was on the `CustomButton` which responds to pointer events.

**Solution:**
Changed UIActivator to check for `CustomButton` BEFORE checking for standard `Button`. If an element has a CustomButton, it uses pointer simulation which properly triggers the game's action handlers.

**Files:** `UIActivator.cs`, `BEST_PRACTICES.md`

---

### Confirmation Dialogs (SystemMessageView) - Partial Support

Added detection and navigation for confirmation dialogs like "Exit Game" confirmation.

**Features:**
- SystemMessageView dialogs are detected and announced ("Confirmation opened.")
- OK and Cancel buttons are navigable with arrow keys
- Enter activates the selected button
- OK button works correctly (exits game)

**Known Issue:**
Cancel button closes the popup visually, but mod navigation doesn't properly return to Settings menu. See KNOWN_ISSUES.md for details. Needs refactoring of panel hierarchy management.

**Technical:**
- Popup detection uses whitelist: `SystemMessageView` and `*Popup(Clone)` patterns
- Verifies popup has active `SystemMessageButton` children before considering it open
- Added cooldown mechanism to prevent re-detection during close animation

**Files:** `MenuPanelTracker.cs`, `GeneralMenuNavigator.cs`, `UIActivator.cs`

---

### F1 Help Menu - Navigable Keybind List

Replaced the single-announcement F1 help with an interactive, navigable help menu.

**Features:**
- F1 toggles help menu open/closed
- Up/Down arrows (or W/S) navigate through keybind items
- Home/End jump to first/last item
- Backspace or F1 closes the menu
- Blocks all other input while open (modal overlay)

**Content Organization:**
- Global shortcuts
- Menu navigation
- Zones in duel (combined entries: "C: Your hand, Shift+C: Opponent hand count")
- Duel information
- Card navigation in zone
- Card details
- Combat
- Browser (Scry, Surveil, Mulligan)

**Localization:**
All help strings are in `Core/Models/Strings.cs` ready for future translation.

**Files:** `HelpNavigator.cs` (new), `Strings.cs`, `MTGAAccessibilityMod.cs`

---

### Unified Navigation Paradigm - Arrow Keys for Menus

Changed menu navigation from Tab/Shift+Tab to Arrow Up/Down with WASD alternatives for improved consistency with screen reader conventions.

**Menu Navigation (New):**
- Arrow Up / W = Previous item
- Arrow Down / S = Next item
- Arrow Left/Right / A/D = Carousel/stepper controls
- Home = Jump to first item
- End = Jump to last item
- No wrapping at boundaries (announces "Beginning of list" / "End of list")

**Duel Navigation (Unchanged - keeps Tab):**
- Tab/Shift+Tab for cycling through highlights (playable cards, targets)
- Arrow keys for zone/card/battlefield navigation

**Zone/Battlefield/Browser Navigation (Enhanced):**
- Added Home/End support for jumping to first/last card
- ZoneNavigator: Home/End in hand, graveyard, exile, stack
- BattlefieldNavigator: Home/End within battlefield rows
- BrowserZoneNavigator: Home/End in scry/surveil/mulligan zones
- BrowserZoneNavigator: Changed from wrapping to non-wrapping for consistency

**Design Rationale:**
- Menus are linear lists → Arrow navigation is intuitive (like desktop apps)
- Duel highlights are scattered across screen → Tab as "next action" is appropriate
- All zone navigation now consistently non-wrapping with boundary announcements

**Files:** `BaseNavigator.cs`, `ZoneNavigator.cs`, `BattlefieldNavigator.cs`, `BrowserZoneNavigator.cs`, `Strings.cs`, plus announcement updates in 5 derived navigators

---

### Browser Navigator Architecture Refactoring

Refactored the monolithic `BrowserNavigator.cs` (~2465 lines) into 3 well-organized files following the established CardDetector/DuelNavigator/ZoneNavigator pattern.

**New Architecture:**
- `BrowserDetector.cs` - Static browser detection and caching (like CardDetector)
- `BrowserNavigator.cs` - Orchestrator for browser lifecycle and generic navigation (like DuelNavigator)
- `BrowserZoneNavigator.cs` - Two-zone navigation for Scry/Surveil and London mulligan (like ZoneNavigator)

**Benefits:**
- Clear separation of concerns (detection vs orchestration vs zone navigation)
- Easier maintenance (London bugs → BrowserZoneNavigator, detection issues → BrowserDetector)
- Consistent with existing codebase patterns
- Better testability

**Bug Fixes During Refactoring:**
- Fixed browser not exiting after Scry closed (ViewDismiss holder cards were animation remnants)
- Fixed Tab+Enter not moving cards in zone-based browsers (now detects zone from parent hierarchy)
- Fixed zone names showing "selected" instead of descriptive names

**Technical Details:**
- BrowserDetector only detects CardBrowserCardHolder from DEFAULT holder (not ViewDismiss)
- Zone detection uses parent hierarchy for Scry, API methods (`IsInHand`/`IsInLibrary`) for London
- Cache invalidation on button clicks forces immediate re-detection

**Files:** `BrowserDetector.cs` (new), `BrowserNavigator.cs` (rewritten), `BrowserZoneNavigator.cs` (new)

---

### Scry/Surveil Browser - Full Keyboard Support

Implemented full keyboard accessibility for Scry, Surveil, and similar card selection browsers.

**User Experience:**
- **C** - Enter "keep on top" zone, navigate with Left/Right
- **D** - Enter "put on bottom" zone, navigate with Left/Right
- **Enter** - Toggle current card between zones (moves card)
- **Up/Down** - Card details (as usual)
- **Space** - Confirm selection

**Technical Discovery:**
Scry-like browsers work differently from London mulligan:
- `CardGroupProvider` is **null** (no central browser controller)
- Cards are managed directly by the `CardBrowserCardHolder` components
- Moving cards uses `RemoveCard()` on source holder + `AddCard()` on target holder

**Implementation:**
1. Detect Scry-like browser via `IsScryLikeBrowser()` (checks for "Scry", "Surveil", "ReadAhead" in browser type)
2. Get `CardBrowserCardHolder` components from both holders
3. Move card: `sourceHolder.RemoveCard(cardCDC)` then `targetHolder.AddCard(cardCDC)`

**Browser Type Comparison:**

| Browser | CardGroupProvider | Move Method |
|---------|-------------------|-------------|
| London Mulligan | LondonBrowser | HandleDrag + OnDragRelease |
| Scry/Surveil | null | RemoveCard + AddCard |

**Files:** `BrowserNavigator.cs`, `GAME_ARCHITECTURE.md`

---

### London Mulligan - Full Keyboard Support

Implemented full keyboard accessibility for London mulligan (putting cards on bottom of library after mulliganing).

**User Experience:**
- **C** - Enter keep pile (cards you're keeping), navigate with Left/Right
- **D** - Enter bottom pile (cards going to bottom), navigate with Left/Right
- **Enter** - Toggle current card between keep and bottom piles
- **Up/Down** - Card details (as usual)
- **Space** - Confirm selection

**Technical Discovery:**
The London mulligan browser uses internal drag-based card selection that doesn't respond to standard click simulation. Key discovery was the `LondonBrowser` instance accessible via `BrowserCardHolder_Default.CardGroupProvider`.

**Implementation:**
1. Get `LondonBrowser` from holder's `CardGroupProvider` property
2. Use `GetHandCards()` / `GetLibraryCards()` for card lists
3. Use `IsInHand()` / `IsInLibrary()` to check card position
4. Move card by: positioning at target zone (`LibraryScreenSpace` / `HandScreenSpace`), then calling `HandleDrag(cardCDC)` + `OnDragRelease(cardCDC)`

**New Pattern - Browser Card Interactions:**
This establishes a pattern for interacting with browser cards that don't respond to standard UI clicks. The browser's `CardGroupProvider` exposes the internal API for card manipulation. This pattern may be reusable for other browser types (scry, surveil reordering, etc.).

**Files:** `BrowserNavigator.cs`

**Note:** Code works but needs significant refactoring - investigation/debugging code still present, duplication between methods.

---

### Card Info: Mana Symbols, Flavor Text, and Artist

Enhanced card information display with proper mana symbol parsing and new metadata fields.

**Mana Symbol Parsing in Rules Text:**
- Mana symbols like `{oT}`, `{oR}`, `{oW}` now read as "Tap", "Red", "White"
- Hybrid mana like `{oW/U}` reads as "White or Blue"
- Phyrexian mana like `{oW/P}` reads as "Phyrexian White"
- Activated ability costs in bare format (e.g., `2oW:`) also parsed correctly
- All mana strings use localized constants from `Strings.cs`

**Flavor Text:**
- Cards now display flavor text as a navigable info block
- Uses `GreLocProvider.GetLocalizedText(flavorTextId, null, false)` for lookup
- FlavorTextId = 1 is a placeholder for cards without flavor text (ignored)

**Artist Credit:**
- Artist name extracted from `Printing.ArtistCredit` property
- Displayed as the last info block when navigating card details

**Technical Details:**
- `ParseManaSymbolsInText()` handles both `{oX}` and bare `2oW:` formats
- `ParseBareManaSequence()` extracts generic mana numbers and color symbols
- `FindFlavorTextProvider()` uses `CardDatabase.GreLocProvider` (SqlGreLocalizationProvider)
- Artist extracted directly from Printing object on card Model

**Files:** `CardModelProvider.cs`, `Strings.cs`, `CardDetector.cs`

---

### Hidden Zone Card Counts

Added shortcuts to query card counts for hidden zones that sighted players can see but couldn't be accessed via keyboard before.

**New Shortcuts:**
- **D**: Your library card count (e.g., "Library, 45 cards")
- **Shift+D**: Opponent's library card count (e.g., "Opponent's library, 52 cards")
- **Shift+C**: Opponent's hand card count (e.g., "Opponent's hand, 7 cards")

**Technical Details:**
- Counts retrieved from ZoneNavigator's zone discovery (counts actual cards in scene)
- Added `GetZoneCardCount()` helper that refreshes zones before returning count
- Shift key state checked before individual key handlers to properly differentiate C vs Shift+C

**Files:** `ZoneNavigator.cs`, `Strings.cs`, `DuelAnnouncer.cs`

---

### Deck Selection Fix

Fixed deck selection on the Deck Selection screen (ConstructedDeckSelectController).

**Problem:**
When pressing Enter on a deck entry, the standard pointer click simulation wasn't properly triggering the deck selection. The deck might visually highlight but the internal selection state wasn't being updated, preventing the "Submit Deck" button from working.

**Solution:**
Added specialized deck activation handling in `UIActivator.TrySelectDeck()`:
1. Detects deck entries by checking parent hierarchy for `DeckView_Base`
2. Finds the `DeckView` component on the parent GameObject
3. Invokes `DeckView.OnDeckClick()` via reflection

This method properly triggers the game's internal deck selection callback, enabling the "Submit Deck" button.

**Files:** `UIActivator.cs`

---

### Universal Backspace Navigation in Menus

Added Backspace as a universal "back one level" key for all accessible menus.

**User Experience:**
- Backspace now consistently navigates back one level in menu hierarchies
- Settings submenu (Audio/Graphics/Gameplay) → Settings main menu
- Settings main menu → Closes Settings entirely
- PlayBlade → Closes and returns to Home
- Friends panel → Closes panel
- Other content panels (Decks, Store, etc.) → Returns to Home

**Key Improvements:**
- PlayBlade closes immediately and shows Home elements while animation plays
- Settings menu closes via direct `SettingsMenu.Close()` call (bypasses problematic BackButton)
- Panel change detection now properly triggers rescans for submenu navigation

**Technical Changes:**
- `GeneralMenuNavigator`: Added `HandleBackNavigation()` for hierarchical back navigation
- `GeneralMenuNavigator`: Added `HandleSettingsBack()` to handle Settings submenu vs main menu
- `GeneralMenuNavigator`: Added `CloseSettingsMenu()` using reflection to call `SettingsMenu.Close()`
- `GeneralMenuNavigator`: Added `ClosePlayBlade()` with immediate state clearing via `ClearBladeStateAndRescan()`
- `GeneralMenuNavigator`: Added `FindSettingsBackButton()` and `ActivateBackButton()` helpers
- `GeneralMenuNavigator`: Fixed `CheckForPanelChanges()` not being called in Update loop
- `GeneralMenuNavigator`: Fixed blade close detection for `BladeContentView.Hide()` events
- `Strings.cs`: Added `NavigatingBack`, `ClosingSettings`, `ClosingPlayBlade` constants

**Files:** `GeneralMenuNavigator.cs`, `Strings.cs`

---

### Slider Accessibility with Arrow Key Control

Added full accessibility support for sliders (volume controls in Audio Settings).

**User Experience:**
- Sliders now announce their name and current value (e.g., "Master Volume, slider, 80 percent, use left and right arrows")
- Left arrow = decrease by 5%
- Right arrow = increase by 5%
- New value is announced immediately after each adjustment
- At min/max boundaries, current value is announced without change

**Label Detection:**
Sliders find their label from multiple sources:
1. Parent "Control - X" container name (Settings pattern)
2. Sibling "Label" or "Text" elements with TMP_Text
3. Cleaned object name as fallback

**Technical Changes:**
- `UIElementClassifier.ClassificationResult`: Added `SliderComponent` property for direct slider access
- `UIElementClassifier`: Added `GetSliderLabel()` method to find slider name from parent/sibling elements
- `UIElementClassifier`: Updated slider classification to use `GetSliderLabel()` instead of `UITextExtractor.GetText()` (which caused duplicate "slider, percent" announcements)
- `UIElementClassifier`: Sliders now set `HasArrowNavigation = true` and store `SliderComponent` reference
- `BaseNavigator.CarouselInfo`: Added `SliderComponent` property
- `BaseNavigator`: Added `HandleSliderArrow()` method for direct slider value adjustment
- `GeneralMenuNavigator`: Updated carousel info building to pass `SliderComponent`

**Files:** `UIElementClassifier.cs`, `BaseNavigator.cs`, `GeneralMenuNavigator.cs`

---

### Unified Stepper Controls with Arrow Navigation

Redesigned stepper controls (Increment/Decrement buttons) to use a single navigable element with left/right arrow keys, similar to carousel elements.

**User Experience:**
- Steppers now appear as a single item in the Tab navigation (e.g., "FPS Limit: VSync, stepper, use left and right arrows")
- Left arrow = decrement value
- Right arrow = increment value
- Value updates are announced after each arrow press with a slight delay to ensure accuracy

**How It Works:**
- Parent control (`Control - Setting: X`) is detected as the navigable element
- Individual Increment/Decrement buttons are filtered out from Tab navigation
- Arrow keys activate the hidden buttons and announce the updated value
- Uses 0.1s delay before re-reading value (game needs time to process button click)

**Technical Changes:**
- `UIElementClassifier`: Added `IsSettingsStepperControl()` to detect parent stepper controls
- `UIElementClassifier`: Added `IsStepperNavControl()` to filter out individual Increment/Decrement buttons
- `UIElementClassifier`: Removed obsolete `IsSettingsStepperButton()` method
- `GeneralMenuNavigator`: Added `FindSettingsStepperControls()` to discover stepper parent elements
- `BaseNavigator`: Added delayed announcement mechanism (`_stepperAnnounceDelay`) for accurate value reading
- `BaseNavigator`: Updated `HandleCarouselArrow()` to schedule delayed re-read instead of immediate

**Why Delay is Needed:**
- Carousels switch between pre-existing child elements (instant)
- Steppers update the same text element's content (requires game to process click first)

**Files:** `UIElementClassifier.cs`, `GeneralMenuNavigator.cs`, `BaseNavigator.cs`

---

### Graphics Settings Menu Accessibility

Added full accessibility support for the Graphics Settings submenu with new UI element types.

**Settings Submenu Navigation:**
- Fixed Settings submenu navigation (Audio, Gameplay, Graphics buttons)
- Settings menu now properly rescans when navigating between submenus
- Foreground panel tracking updated during Settings submenu transitions

**Dropdown Controls (TMP_Dropdown):**
- Detected via `Control - X_Dropdown` parent pattern containing TMP_Dropdown
- Extracts setting name from parent control name (e.g., "Quality", "Resolution", "Languages")
- Extracts current value from `TMP_Dropdown.options[value].text`
- Announced as: "Setting Name: Current Value, dropdown"
- Examples: "Quality: Benutzerdefiniert, dropdown", "Resolution: 2880x1800, dropdown"

**Technical Changes:**
- `GeneralMenuNavigator`: Added `FindSettingsDropdownControls()` for custom dropdown detection
- `GeneralMenuNavigator`: Added `FindClickableInDropdownControl()` helper
- `GeneralMenuNavigator`: Settings submenu button clicks trigger rescan
- `GeneralMenuNavigator`: `PerformRescan()` updates foreground panel for Settings
- `UIElementClassifier`: Added `IsSettingsDropdownControl()` for dropdown detection
- `UIElementClassifier`: Added `GetSettingsDropdownLabel()` helper
- `UIElementClassifier`: Added `FindValueInControl()` to search for value text in control hierarchy
- `UIElementClassifier`: Enhanced TMP_Dropdown classification with setting label extraction

**Files:** `GeneralMenuNavigator.cs`, `UIElementClassifier.cs`

---

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
