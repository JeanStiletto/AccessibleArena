# Known Issues

Active bugs and limitations in the MTGA Accessibility Mod.

## Potential Issues (Monitor)

### Container Element Filtering

**Filter: Elements with "Container" in name + 0x0 sizeDelta are skipped**

Added to filter wrapper objects like `NPE-Rewards_Container` that have CustomButton but aren't real interactive buttons. These have 0x0 sizeDelta and text inherited from children.

**Risk:** Some legitimate clickable containers might use anchor-based sizing (sizeDelta=0) and would be incorrectly filtered.

**If a button stops working:** Check if its name contains "Container" and if the element uses anchor-based sizing. May need to revert or refine the filter in `UIElementClassifier.ShouldBeFiltered()`.

**Location:** `UIElementClassifier.cs` - search for "container" in `ShouldBeFiltered` method.

---

## Active Bugs

### Panel Detection System (Jan 2026) - SIMPLIFIED

**Current Architecture: Direct Detector Ownership (Simplified Jan 2026)**

Panel detection was simplified by removing the plugin architecture:

1. **PanelStateManager** - Owns all 3 detectors directly, called from main mod Update()
   - No more IPanelDetector interface or PanelDetectorManager
   - Detectors initialized in constructor, Update() called directly

2. **HarmonyPanelDetector** - Event-driven via Harmony patches
   - Self-contained patterns: playblade, settings, socialui, blades
   - CRITICAL for PlayBlade (uses SLIDE animation, alpha stays 1.0)

3. **ReflectionPanelDetector** - Polls IsOpen properties
   - Self-contained patterns: Login panels, PopupBase
   - Fallback for panels not handled by Harmony or Alpha

4. **AlphaPanelDetector** - Watches CanvasGroup alpha
   - Self-contained patterns: SystemMessageView, Dialog, Modal, Popups

**Key Design (Simplified):**

- Each detector has its own inline pattern list (no central PanelRegistry)
- PanelStateManager owns detectors directly (no plugin abstraction)
- Panel metadata methods (ClassifyPanel, GetPriority, etc.) moved to PanelInfo class
- Archived files: IPanelDetector.cs, PanelDetectorManager.cs, PanelRegistry.cs

**Unified Foreground/Backspace System (Jan 2026):**

GeneralMenuNavigator uses a unified `ForegroundLayer` enum for both element filtering AND backspace navigation:
- `GetCurrentForeground()` - Single source of truth for what's "in front"
- `ShouldShowElement()` - Element filtering derived from foreground layer
- `HandleBackNavigation()` - Back navigation derived from foreground layer

This ensures filtering and back navigation can never get out of sync.

---

### Failed Experiment: Alpha-Only Detection (Jan 2026)

**Goal:** Unify all panel detection on alpha-based approach to eliminate race conditions.

**Fundamental Issue: PlayBlade uses SLIDE animation, not ALPHA fade**

PlayBlade slides in from the side while alpha stays at 1.0 the entire time. Alpha-based detection can NEVER work for it because there's no alpha change to detect. This is why Harmony patches on `PlayBladeVisualState` are essential.

**What We Tried:**
1. Disabled `CheckForPanelChanges()` entirely in GeneralMenuNavigator.Update()
2. Added PlayBlade/Blade patterns to UnifiedPanelDetector
3. Reduced cache refresh interval from 5 seconds to 1 second
4. Removed ExcludedNamePatterns that were blocking ContentController panels
5. Tried stability-based detection (wait for alpha to settle before reporting)

**Why It Failed:**

1. **PlayBlade not detected properly**
   - `ContentController - PlayBladeV3_Desktop_16x9(Clone)` wasn't being registered
   - Child elements like `Blade_FilterListItem_Base(Clone)` were detected instead
   - Result: Wrong foreground panel (SocialUI instead of PlayBlade)
   - Elements from main menu mixed with PlayBlade elements

2. **Stability tracking caused delays**
   - Waiting for alpha to be stable (3 consecutive checks with < 2% change)
   - Made home menu load slowly
   - Blade opening/closing unreliable
   - Some panels never reached "stable" state

3. **Home menu loading issues**
   - Initial load very slow
   - Elements appearing inconsistently

**Key Learning:**

Alpha detection works well for true popups (SystemMessageView, dialogs) that:
- Fade in/out with clear alpha transitions
- Are independent GameObjects with their own CanvasGroup
- Don't have complex controller hierarchies

Alpha detection is unreliable for:
- PlayBlade - nested inside HomePageContentController, uses visual state enum
- Content controllers - may not have CanvasGroup at root level
- Panels that appear instantly (alpha=1 from start)

**Code Changes (Reverted/Kept):**

Kept from experiment:
- `TrackedPanelPatterns` renamed from `PopupPatterns` (clearer naming)
- `IsTrackedPanel()` renamed from `IsPopupPanel()`
- Removed `ExcludedNamePatterns` - was blocking legitimate panels
- Added "PlayBlade", "Blade" to tracked patterns (helps with some detection)

Reverted:
- Re-enabled `PanelStatePatch.Initialize()` in AccessibleArenaMod.cs
- Re-enabled event subscription in NavigatorManager.cs
- Re-enabled `CheckForPanelChanges()` in GeneralMenuNavigator.cs
- Removed stability tracking - caused more problems than it solved

**Current State (Jan 2026 - Restored):**

Hybrid approach with Harmony patches is working:
- **Harmony patches** provide immediate event-driven detection for PlayBlade, Settings, Blades
- **CheckForPanelChanges** handles controller-based panels via reflection (backup)
- **UnifiedPanelDetector** handles alpha-based panels (popups, dialogs, social)

The key insight: PlayBlade REQUIRES Harmony patches because it uses slide animation (alpha stays 1.0).

**Files:**
- `src/Patches/PanelStatePatch.cs` - Harmony patches (CRITICAL for PlayBlade)
- `src/Core/Services/NavigatorManager.cs` - Event subscription (OnPanelStateChanged)
- `src/Core/Services/GeneralMenuNavigator.cs` - OnPanelStateChangedExternal, CheckForPanelChanges
- `src/Core/Services/UnifiedPanelDetector.cs` - Alpha detection for popups/dialogs
- `src/Core/Services/MenuPanelTracker.cs` - Controller reflection utilities

---

### Confirmation Dialogs (SystemMessageView)

**Cancel button requires double-click to activate (Jan 2026)**

Confirmation dialogs (e.g., Logout confirmation from Settings) are detected and navigable with OK/Cancel buttons. However, pressing Enter on the Cancel (Abbrechen) button requires two presses - the first press doesn't close the popup, but the mod correctly reports this (shows popup still active). The second press closes it successfully.

**Technical details:**
- SystemMessageButtonView component has both CustomButton and SystemMessageButtonView
- UIActivator tries direct SystemMessageButtonView.OnClick first, which fails
- Falls back to pointer simulation which may require two attempts
- The OK button appears to work on first click

**Workaround:** Press Enter twice on Cancel, or press Escape to close the popup.

### Login Screen Back Button

**Back button does not respond to keyboard activation**

The back button on the login panel (Button_Back) cannot be activated via keyboard (Enter or Backspace). The button has CustomButton and Animator components but doesn't respond to:
- Pointer event simulation
- CustomButton.onClick invocation
- Animator triggers (Pressed, Click, Selected)
- Submit events

The button likely uses a mechanism we haven't discovered (possibly parent panel controller or input system integration).

**Workaround:** Use mouse to click the back button, or restart the game to return to Welcome screen.

### Registration Screen

**Registration not fully working**

The registration screen dropdowns (birth date, country, experience) are navigable and selectable. However, the screen has auto-advance behavior where selecting a value automatically opens the next dropdown and moves focus. The mod tracks this via focus-based dropdown mode detection, but:

- The auto-advance can cause brief navigation confusion
- After completing all dropdowns, the final submit flow is untested
- Some edge cases with rapid navigation may not be handled

**Current status:** Basic dropdown navigation works, but full registration flow needs more testing.

### HotHighlightNavigator

**Activatable creatures take priority over playable cards**

When you have both activatable creatures on battlefield (like mana creatures) and playable cards in hand, the game only highlights the activatable creatures. This is game behavior, not a mod bug - the game wants you to tap mana first. After activating the creature's ability, hand cards become highlighted.

Example: Ilysian Caryatid highlighted but Forest in hand not highlighted until Caryatid is tapped.

**Space key pass priority - manual button click required (Jan 2026)**

The game's native Space keybinding for passing priority doesn't work reliably after using mod navigation (C key to hand, arrow keys, etc.). Even clearing EventSystem focus doesn't help - the game checks something else internally.

**Fix:** HotHighlightNavigator now clicks the primary button directly when Space is pressed with no highlights, bypassing the game's native handler entirely.

**Strange behavior observed:** This needs more testing in real duels. The issue appeared suddenly and the root cause is unclear - could be related to game state, UI focus, or internal game keybinding system. The direct button click works reliably but we don't fully understand why the native handler stopped working.

### Combat

**Blocker selection after targeting**

Strange interactions may occur after selecting a target for blocks. Needs further testing.

### Card Playing

**Rapid Enter presses**

Multiple rapid Enter presses can trigger card play sequence multiple times, potentially causing issues if game enters targeting mode.

### Mulligan

**Regular Mulligan: Working**
- BrowserScaffold_Mulligan detected properly
- Space → clicks KeepButton ("X behalten" / "Keep X")
- Backspace → clicks MulliganButton to take new hand
- Tab navigates through cards in opening hand
- Fallback detection waits for mulligan buttons before entering browser mode

**London Mulligan (putting cards on bottom): WORKING (Jan 2026)**
- After mulliganing, you must put 1 card on bottom of library per mulligan taken
- BrowserScaffold_London detected, cards navigable in two zones

**Zone Navigation:**
- **C** - Enter keep pile (cards you're keeping)
- **D** - Enter bottom pile (cards going to bottom of library)
- **Left/Right** - Navigate between cards within current zone
- **Enter** - Toggle card to the other pile (keep ↔ bottom)
- **Space** - Confirm selection (click Submit button)

**Technical Implementation:**
Card selection uses the `LondonBrowser` API via reflection:
1. Get `LondonBrowser` instance from `BrowserCardHolder_Default.CardGroupProvider`
2. Check card position with `IsInHand()` / `IsInLibrary()`
3. Get target zone position from `LibraryScreenSpace` / `HandScreenSpace`
4. Move card transform to target position
5. Call `HandleDrag(cardCDC)` then `OnDragRelease(cardCDC)`

Card lists retrieved via `GetHandCards()` and `GetLibraryCards()` from LondonBrowser.
See BEST_PRACTICES.md "Browser Card Interactions" for reusable patterns.

## Active Bugs

### Backspace Navigation Gaps (Jan 2026)

**Profile screen:** Backspace doesn't navigate back from Profile screen.

**Opened pack screen:** Backspace doesn't close the pack opening view.

Both need investigation in HandleBackNavigation/GetCurrentForeground to add proper layer detection.

---

## Technical Debt

### GeneralMenuNavigator Improvements (Jan 2026)

Remaining improvements identified during code quality review:

**Medium Effort:**

1. **Conditional debug logging**
   - ~50+ `MelonLogger.Msg` calls throughout the file
   - Wrap verbose logs in a debug flag check: `if (DebugLogging) MelonLogger.Msg(...)`
   - Reduces log noise in production

2. **Button search pattern consolidation**
   - `FindCloseButtonInPanel()` and `FindGenericBackButton()` have similar loops
   - Could consolidate into a shared helper method

3. **Rescan debounce review**
   - Almost every `TriggerRescan` call uses `force: true`
   - The debounce mechanism is rarely effective
   - Review if debounce is needed or should be removed

**Efficiency Concerns (Lower Priority):**

4. **Multiple FindObjectsOfType in DiscoverElements**
   - 6 separate calls for Button, EventTrigger, Toggle, Slider, TMP_InputField, TMP_Dropdown
   - Could potentially optimize with single-pass collection

5. **Repeated GameObject.Find calls**
   - `FindGenericBackButton()`, `CloseSocialPanel()`, `ClosePlayBlade()` all use `GameObject.Find()`
   - These are O(n) scene searches each time

**Logic Review Needed:**

6. **IsOverlayActive() potentially disconnected**
   - Checks for blockers but may not align with `GetCurrentForeground()`
   - Could cause inconsistent behavior

**Location:** `src/Core/Services/GeneralMenuNavigator.cs`

---

### Code Archaeology Review Needed

The codebase has accumulated defensive fallback code and verbose patterns from iterative reverse-engineering of MTGA's custom UI. A cleanup pass should test and potentially remove:

**Defensive fallback chains:**
- `ActivateBackButton()` in GeneralMenuNavigator has 4 activation methods (Unity Button → IPointerClickHandler → Animator triggers → UIActivator). Test which are actually needed.
- Multiple detection fallbacks throughout navigators - some may be obsolete after we understood MTGA's patterns better.

**Debug/logging code:**
- `LogAvailableUIElements()` (~150 lines) - useful during development, could be behind a debug flag or removed.
- Extensive `MelonLogger.Msg()` calls throughout - review which are still needed for troubleshooting vs just noise.

**Potential dead patterns:**
- Settings control discovery methods were consolidated but may have edge cases we no longer encounter.
- Panel detection fallbacks that were added for specific bugs that may be fixed.

**AI-induced verbosity:**
- Overly verbose comments explaining obvious code
- Redundant null checks where nulls can't occur
- Long method names that could be shorter

**Approach:** Pick one file at a time, remove suspected dead code, test thoroughly in-game. If something breaks, restore it. Document which fallbacks are actually required.

**Priority:** Low - code works, this is optimization/maintainability.

### WinAPI Fallback Code (UIActivator.cs)

The UIActivator contains ~47 lines of commented WinAPI code (mouse_event, SetCursorPos, etc.) that was a working fallback when Unity pointer event simulation failed. This was kept because:
- Unity event approach stopped working at some point (unknown cause)
- WinAPI approach worked reliably at the time
- After PC restart, Unity events worked again

**TODO:** Test if this fallback is still needed:
1. Run prolonged tests with Unity events approach
2. Check if overlapping overlays or mouse positioning issues recur
3. If stable for several months, the WinAPI code can be removed

**Location:** `src/Core/Services/UIActivator.cs` lines 13-59

## Needs Testing

### NPE Rewards "Take Reward" Button (Jan 2026)

**Issue:** The NullClaimButton ("Take reward" button) on NPE rewards screen isn't being added to navigable elements, even though reward cards are found correctly.

**Investigation:** The original code used `Transform.Find("ActiveContainer")` then `Find("NullClaimButton")` which was failing silently (no logging for the failure case).

**Fix attempted:** Changed to search entire `npeContainer` hierarchy using `GetComponentsInChildren<Transform>` - the same pattern that successfully finds reward cards.

**Debug logging added:** Extra logging to identify which failure case occurs:
- "Adding NullClaimButton as 'Take reward' button (path: ...)" - success
- "NullClaimButton not found in NPE-Rewards_Container hierarchy" - button missing
- "NullClaimButton already in addedObjects" - duplicate detection issue
- "NPE-Rewards_Container not found" - container missing

**TODO:** Remove debug logging once fix is confirmed working.

**Location:** `src/Core/Services/GeneralMenuNavigator.cs` - `FindNPERewardCards()` method

### Menu Navigation

- Card name extraction for various reward types
- Rewards screen detection

### Targeting Mode

- Player targets (V key zone with targeting spells like Shock)
- "Any target" spells (creatures + players)
- Stack spell targets (Counterspell-type effects)
- Triggered abilities requiring targets

## Limitations

### Player Info Zone

- Life total query via L key may not work reliably - MtgPlayer doesn't expose Life property directly
- Life changes ARE announced via events, but on-demand query is inconsistent

**Investigation options for fixing life totals:**
1. Query `CurrentGameState.LocalPlayer` on-demand (GameState is null at startup but populated during gameplay)
2. Find 3D life counter component in `PlayerHpContainer` children at runtime
3. Extract absolute value from `LifeTotalUpdateUXEvent` fields
4. Search MtgPlayer for `GetLifeTotal()` method instead of property

### Browser Navigator

- Toggle mechanism for scry/surveil (moving cards between keep/bottom) needs API discovery
- Current status: detection and navigation work, toggle in progress

### Color Challenge (Tutorial)

- Does NOT support player targeting - only creatures receive HotHighlight
- MatchTimer UI is disabled in tutorial mode

## Design Decisions

### Tab and Arrow Navigation (Jan 2026)

**Current State:** Both Tab and Arrow keys navigate menu elements identically.

- Tab/Shift+Tab = Arrow Down/Up (same `MoveNext()`/`MovePrevious()` calls)
- Both use the navigator's curated element list
- No Unity EventSystem fallback - we handle Tab directly in BaseNavigator

**Why:** Unity's EventSystem Tab navigation was unreliable:
- Moved to wrong elements (especially buttons)
- Ignored Shift key (went forward instead of backward)
- Required complex fallback/correction logic

**Future Consideration:** May simplify to arrow-only navigation, removing Tab support in menus. Tab would remain for duel highlights (HotHighlightNavigator). This would:
- Reduce code complexity
- Avoid confusion between two navigation methods
- Match screen reader conventions (arrows for menus)

**Current Status:** Both work, keeping Tab for familiarity with standard form navigation.

## Recently Completed

### Booster Pack Card List (Jan 2026)

Pack opening card list is now accessible via BoosterOpenNavigator.

**Status:** Partially tested - ran out of booster packs during development.

**Known issues:**
- Some cards show as "Unknown card" - possibly planeswalkers or other special card types
- Card name extraction may pick up rules text instead of card name for some card layouts

**Features:**
- Detects when pack contents are displayed (via CardScroller or RevealAll button)
- Navigates through all revealed cards with Left/Right arrows
- Announces card name and type for each card
- Includes "Reveal All" button navigation
- Includes dismiss/continue button navigation
- Full CardDetector integration for detailed card info (press Enter on card)

**Keyboard shortcuts:**
- Left/Right arrows (or A/D): Navigate between cards
- Up/Down arrows: Read card details (handled by card navigator)
- Enter: View detailed card info for selected card
- Home/End: Jump to first/last item
- Backspace: Close pack view

**Location:** `src/Core/Services/BoosterOpenNavigator.cs`

### Friends Panel Fixes (Jan 2026)

Fixed Friends panel navigation that was broken after code quality refactoring.

**Root causes found and fixed:**
1. The "back" button filter was matching "Backer_Hitbox" elements (fixed by excluding "backer")
2. Popup detection pattern didn't match resolution-suffixed names like `InviteFriendPopup_Desktop_16x9(Clone)` (fixed pattern)
3. Popup elements were filtered to Social panel instead of the popup itself (fixed priority order)

**Now working:**
- F4 opens Friends panel with all elements (friend list, add friend, header)
- Add Friend popup with input field and invite button
- Full keyboard workflow: navigate to Add Friend → Enter → type name → Tab to invite button → Enter

### Hidden Zone Card Counts (Jan 2026)

Card counts for hidden zones are now accessible:
- **D key**: Your library card count
- **Shift+D**: Opponent's library card count
- **Shift+C**: Opponent's hand card count

Counts are tracked via UpdateZoneUXEvent and announced on demand.

## Planned Features

### Immediate

1. Life total announcements (L shortcut) - fix reliability
2. Mana pool info (A shortcut or from ManaPoolString)
3. Stepper control redesign - current increment/decrement buttons should be unified into a single navigable stepper with left/right arrows to change value

### Upcoming

1. Creature death announcements with card names
2. Exile announcements with card names
3. Graveyard card announcements with names
4. Emote menu trigger (click HoverArea on player portrait)
5. Player username announcements
6. Game wins display (WinPips) for best-of-3 matches
7. Player rank information

### Future

1. Deck building interface
2. Collection browser
3. Draft/sealed events
4. Detailed player breakdown mode (cycle through life, mana, timeouts, rank)
