# Known Issues

Active bugs and limitations in the MTGA Accessibility Mod.

## Active Bugs

### Bot Match Button Not Visible

Bot Match button not visible when PlayBlade is open.

**Likely cause:** May not match PlayBlade parent path patterns in `IsInsidePlayBlade()`

**Files:** `OverlayDetector.cs`, `ElementGroupAssigner.cs`

---

### Confirmation Dialog Buttons (Popup Buttons) - Settings Logout

Popup buttons in Settings menu (e.g., Logout confirmation with Abbrechen/OK) don't close properly or corrupt game state.

**The Core Problem:**
- `SystemMessageButtonView.Click()` invokes the button's callback but doesn't close the popup
- The game's popup system tracks internal state that we can't properly update via reflection
- Forcibly hiding popups with `SetActive(false)` corrupts game state, preventing popup reopen

**Attempted Approaches (January 2025):**

1. **Click() alone**
   - Result: Callback invoked but popup stays open, no visual change

2. **Click() + SetActive(false)** ⭐ BEST VISUAL RESULT
   - Result: Popup closes visually
   - Problem: Game state corrupted - popup cannot be triggered again until game restart
   - The game thinks popup is still "active" internally

3. **OnBack(null) alone** (on SystemMessageView)
   - Result: Popup doesn't close at all

4. **Click() + OnBack(null)**
   - Result: Inconsistent behavior
   - First attempt: Popup closed but immediately reopened
   - Second attempt: Worked correctly
   - Third attempt: OnBack() had no effect
   - Theory: Click() may interfere with OnBack() state

5. **OnBack(null) for Cancel buttons only, Click()+OnBack() for OK**
   - Result: OnBack() alone doesn't close popup (needs Click() first for state setup)

6. **Click() + SetActive(false) + ReportPanelClosedByName()** ⭐ BEST MOD INTEGRATION
   - Result: Popup closes visually AND mod detects the close
   - Problem: Same as #2 - game state corrupted, popup can't reopen

7. **CustomButton._onClick.Invoke()** (directly invoke UnityEvent)
   - Result: Nothing happens - event invokes but no popup close

8. **CustomButton.OnPointerClick()** (simulate pointer click on inner button)
   - Result: Not yet fully tested

**Attempted Approaches (January 2026):**

9. **Click() + OnBack(null)** (re-test)
   - Result: First press closes popup visually but it immediately reopens (~100ms later)
   - Second press on the reopened popup works correctly
   - Confirms inconsistent behavior from attempt #4

10. **OnBack(null) alone for Cancel buttons** (no Click() call)
    - Result: Mod reports popup closed, but popup stays visible on screen
    - OnBack() updates internal state but doesn't trigger visual close

11. **SimulatePointerClick alone** (no Click(), no OnBack(), no SetActive)
    - Result: Nothing happens - popup stays open
    - Pointer events sent but no response

12. **Normal CustomButton path** (SimulatePointerClick + TryInvokeCustomButtonOnClick)
    - Result: Nothing happens - popup stays open
    - This exact code path works for ALL other buttons in the game
    - SystemMessageButtonView buttons somehow don't respond to standard activation

13. **OnPointerDown() + OnPointerUp() on CustomButton**
    - Result: Methods called successfully but no visual close
    - CustomButton's internal _mouseDown state doesn't trigger close

14. **_onClickDown + _onClick UnityEvents directly**
    - Result: Events invoked (sound plays!) but popup doesn't close
    - Confirms button receives input but close mechanism is elsewhere

15. **ExecuteEvents.Execute with pointerClickHandler**
    - Result: No effect on popup

16. **ExecuteEvents.Execute with submitHandler**
    - Result: No effect on popup

17. **Combined: _onClickDown + _onClick + ExecuteEvents + Click()**
    - Result: Sound plays (button responds) but popup stays open
    - Button definitely receives our input but close is handled differently

18. **HandleKeyDown(KeyCode.Escape) on SystemMessageView**
    - Result: Method invokes successfully but popup doesn't close
    - The game's HandleKeyDown likely checks actual Unity Input state internally
    - Real Escape key works because Unity's input system detects it

**Final Discovery (January 2026):**
- **OK/Confirm buttons**: `Click()` WORKS! The action fires AND the game closes the popup automatically
- **Cancel buttons**: `Click()` fires callback but popup stays visible

**SOLUTION FOUND (January 29, 2026):**
After `Click()`, call `SystemMessageManager.Instance.ClearMessageQueue()` to dismiss the popup.

**Working Approach:**
1. Call `Click()` on `SystemMessageButtonView` to trigger the button's callback
2. Find `SystemMessageManager` singleton via reflection (type in Core assembly)
3. Call `ClearMessageQueue()` method on the instance to dismiss the popup

**Why This Works:**
- `Click()` triggers the button callback (action for OK, no-op for Cancel)
- `ClearMessageQueue()` clears the popup manager's internal queue, which triggers the close
- This works for BOTH OK and Cancel buttons

**SystemMessageManager Key Members:**
- `Instance` (static property) - Singleton instance
- `ShowingMessage` (property) - Boolean indicating if a message is showing
- `ClearMessageQueue()` (method) - Clears message queue and closes popup
- `Close(SystemMessageHandle)` (method) - Closes specific message by handle
- `ShowOk()`, `ShowOkCancel()`, `ShowMessage()` - Methods to show popups

**SystemMessageView Internal Structure:**
- Has `_button` field pointing to inner CustomButton
- Has `HandleKeyDown(KeyCode, Modifiers)` method - but calling it via reflection doesn't work
- The real Escape key works because Unity's input system detects the actual keypress
- `HandleKeyDown` likely checks `Input.GetKeyDown()` internally, not just the parameter

**SystemMessageButtonView Internal Structure:**
- Has `_button (CustomButton)` field
- Has `Init(SystemMessageButtonData, Action<SystemMessageButtonData>)` - stores callback
- Has `Click()` method - invokes the stored callback
- NO stored reference to `handleOnClick` callback accessible via reflection
- The callback is likely stored in a compiler-generated closure class

**SystemMessageView Methods Available:**
- `Show()`, `Hide()` - Hide() doesn't close popup properly
- `OnBack(ActionContext)` - Inconsistent results, doesn't close visually
- `HandleKeyDown(KeyCode, Modifiers)` - Reflection call doesn't trigger close
- `HandleOnClick(SystemMessageButtonData)` - Couldn't find ButtonData on button
- `set_IsOpen(Boolean)` - Setting to false doesn't close popup

**CustomButton Fields Examined:**
- `_onClick (UnityEvent)` - Invoking plays sound but doesn't close popup
- `_onClickDown (UnityEvent)` - Invoking doesn't close popup
- `OnClickedReturnSource (Action)` - Was NULL, couldn't use
- `_mouseDown (Boolean)` - Stays false even after our pointer simulation

**PopupManager:**
- Has `Instance` property but couldn't access it
- Has `RegisterPopup/UnregisterPopup` methods
- Has `OnBack(ActionContext)` method

**Root Cause Theory:**
The popup close mechanism is tied to the game's internal popup manager/queue system. When a popup button is clicked normally with a mouse:
1. Unity's input system detects the click
2. GraphicRaycaster determines what was clicked
3. CustomButton receives pointer events and updates internal state (`_mouseDown = true`)
4. Button callback fires
5. Something in the click chain notifies the popup manager
6. Popup manager handles close animation and state cleanup

Our reflection-based calls can trigger step 4 (callback fires, action happens) but miss steps 1-3 and 5-6. The popup manager never gets notified that a button was clicked, so it doesn't close the popup.

For OK buttons, step 4 triggers a game state change (logout, etc.) which causes the entire UI to reset, effectively closing the popup. For Cancel buttons, there's no state change, so the popup stays.

**STATUS: RESOLVED**
Both OK and Cancel buttons now work with Enter key. The fix uses `SystemMessageManager.ClearMessageQueue()` after `Click()`.

**Implementation:** See `UIActivator.TryDismissViaSystemMessageManager()`

**Files:** `UIActivator.cs`, `SettingsMenuNavigator.cs`, `PanelStateManager.cs`

---

### Login Screen Back Button

Back button on login panel doesn't respond to keyboard activation (Enter or Backspace). Likely uses undiscovered activation mechanism.

**Workaround:** Use mouse, or restart game to return to Welcome screen.

---

### Registration Screen

Dropdown auto-advance causes navigation confusion. Basic dropdown navigation works, but full registration flow needs more testing.

---

### Rewards Popup Not Tracked

Quest reward popups (`ContentController - Rewards_Desktop_16x9(Clone)`) are not detected as overlays. Navigation includes all navbar buttons instead of focusing on just the popup.

**The Problem:**
- Reward popup appears under `Canvas - Screenspace Popups/ContentController - Rewards_Desktop_16x9(Clone)`
- Has `ClaimButton` with text "Nehmen" (Claim) and reward cards
- PanelStateManager does NOT track this popup - no `FinishOpen()` or lifecycle events logged
- So `OverlayDetector.GetActiveOverlay()` never returns `Popup` for it
- Elements are not filtered, navbar buttons appear alongside popup content

**Why System Popups Work But This Doesn't:**
- SystemMessageView popups: Tracked by PanelStateManager (call `FinishOpen()` etc.)
- SocialUI: Has dedicated detection via `IsSocialPanelOpen()`
- Settings: Has dedicated detection via `CheckSettingsMenuOpen()`
- Rewards popup: Uses generic ContentController, doesn't call lifecycle methods

**What We Know:**
- Popup path: `Canvas - Screenspace Popups/ContentController - Rewards_Desktop_16x9(Clone)`
- Contains: `Container/Buttons/ClaimButton`, `Container/RewardsCONTAINER/RewardPrefab_*`
- Does NOT trigger `NavContentController.FinishOpen()` when appearing
- Likely just instantiated/enabled without calling Open() lifecycle

**Attempted Fix (Failed):**
Direct GameObject search for "ContentController" + "Rewards" broke home page detection (found something always active).

**Next Steps:**
1. Debug logging added to GeneralMenuNavigator to capture controller components when ClaimButton is discovered
2. Find the actual controller class type
3. Patch it in PanelStatePatch like other controllers

**Debug logging:** When ClaimButton with "Nehmen" text is found, logs all components on parent ContentController

**Files:** `OverlayDetector.cs`, `GeneralMenuNavigator.cs`, `PanelStatePatch.cs`

---

### Activated Abilities with Mana Costs Cannot Be Completed

Creatures with activated abilities that cost mana (e.g., `{3}{G}: Do something`) cannot be activated via keyboard. After clicking the creature, only "Abbrechen" (Cancel) buttons are available - no way to confirm mana payment.

**The Problem:**
- Click on creature with mana ability → game enters mana payment mode
- Tab shows 0 highlights - lands are NOT highlighted during mana payment
- Primary button is "Abbrechen" (Cancel)
- Secondary button shows "Strg" (Ctrl hint) - not an action button
- No "Confirm" or "Done" button exists
- Even after manually tapping lands, no way to finish activation

**Current Workaround (Partial):**
- Use A key (lands shortcut) + Left/Right to navigate lands
- Enter to tap individual lands for mana (works - sounds confirm)
- BUT: No way to confirm/submit mana payment after tapping

**EXPERIMENTAL Change (January 2026):**
- Space no longer clicks Cancel when primary button is "Abbrechen"
- Code passes Space through to game hoping for native mana confirmation
- Result: Space does nothing - game doesn't handle it either
- See `HotHighlightNavigator.cs` lines marked `// EXPERIMENTAL`

**Test Case (January 2026):**
- Card: Sanftmütige Bibliothekarin
- Ability: `{3}{G}: Transform, +1/+1 counters, draw card`
- Tapping lands works (sounds confirm mana added)
- Even with exact mana amount, game doesn't auto-confirm
- Result: Cannot complete activation

**Game UI During Mana Payment:**
- `PromptButton_Primary`: "Abbrechen" (Cancel)
- `PromptButton_Secondary`: "Strg" (Ctrl hint, not actionable)
- No confirm/done button exists
- Game expects auto-confirm when exact mana is paid (doesn't work)

**Possible Causes:**
1. Game expects exact mana match for auto-confirm but doesn't trigger
2. Our mod intercepts key that game needs for confirmation
3. Game has hidden confirm mechanism we haven't found
4. BatchManaSubmission class may need to be triggered manually

**Files:** `HotHighlightNavigator.cs`, `DuelAnnouncer.cs`

---

### Space Key Pass Priority

Game's native Space keybinding doesn't work reliably after using mod navigation. HotHighlightNavigator now clicks the primary button directly as workaround.

---

### Rapid Card Play

Multiple rapid Enter presses can trigger card play sequence multiple times.

---

### Combat Blocker Selection

Strange interactions may occur after selecting a target for blocks. Needs testing.

## Needs Testing

### NPE Rewards Button

NullClaimButton ("Take reward") not being added to navigation. Fix attempted - searching entire hierarchy instead of specific path.

**Location:** `GeneralMenuNavigator.cs` - `FindNPERewardCards()`

---

### Targeting Mode

- Player targets (V key zone)
- "Any target" spells
- Stack spell targets
- Triggered abilities requiring targets

## In Progress

### Deck Builder - Deck List Card Info Incomplete

Deck list cards (cards in your deck shown in the compact list view) only display Name and Quantity when reading card info with Up/Down arrows. Other properties (mana cost, type, rules text) are not shown.

**Root cause:** The `CardDataProvider.GetCardPrintingById(uint id, string skinCode)` method is being called with GrpId and null skinCode, but it's not returning the full card data. The CardPrinting object may require a different lookup method or the skinCode parameter.

**What works:**
- Deck list card navigation with Left/Right
- Card name extraction from button label
- Quantity extraction from `ListMetaCardView_Expanding.Quantity`
- Quantity displayed in card info blocks after Name

**What doesn't work:**
- Full card info (mana cost, type, rules text, etc.) not returned from GrpId lookup

**Files:** `CardModelProvider.cs` - `GetCardDataFromGrpId()`, `GetDeckListCardInfo()`

---

---

### PlayBlade Backspace Navigation Not Working

Backspace from PlayBladeContent group should navigate back to PlayBladeTabs, but instead closes the blade entirely.

**Expected flow:**
1. Open PlayBlade → auto-enters PlayBladeTabs group (WORKING)
2. Press Enter on a tab → auto-enters PlayBladeContent group (WORKING)
3. Press Backspace → should return to PlayBladeTabs (NOT WORKING - closes blade)

**Root cause investigation:**
The PlayBladeNavigationHelper tracks state (Tabs, FindMatchModes, FindMatchDecks, etc.) but the state gets out of sync with what the user is viewing.

**What we've tried:**
1. **Initial implementation:** PlayBladeNavigationHelper with state machine tracking Enter presses on tabs/modes
2. **Helper initialization fix:** Changed from checking panel name to `PanelStateManager.Instance?.IsPlayBladeActive`
3. **Event handler ordering:** Moved `CheckAndInitPlayBladeHelper()` before early returns in panel event handlers
4. **State sync on backspace:** Added `SyncPlayBladeHelperWithCurrentGroup()` to sync helper state with GroupedNavigator's current group before handling backspace

**Current state:**
- Auto-entry into tabs group on blade open: WORKING
- Auto-entry into content group on tab activation: WORKING
- Backspace from content to tabs: NOT WORKING
- State sync mechanism added but not effective

**Hypothesis:**
The sync mechanism may not be detecting the correct state. Need to investigate:
1. Is `_groupedNavigator.CurrentGroup` returning the correct group?
2. Is the sync happening before or after the backspace decision?
3. Are there multiple code paths handling backspace that bypass the sync?

**Files:**
- `GeneralMenuNavigator.cs` - `HandlePlayBladeBackspace()`, `SyncPlayBladeHelperWithCurrentGroup()`
- `PlayBladeNavigationHelper.cs` - `HandleBackspace()`, `SyncToContentState()`, `SyncToTabsState()`
- `GroupedNavigator.cs` - `CurrentGroup`, navigation level tracking

---

### Enchantment/Attachment Announcements

Code added but `Model.Parent` and `Model.Children` properties always return null/empty.

**Research completed:** The game uses `UniversalBattlefieldStack` system instead of Model properties. See `docs/ATTACHMENT_RESEARCH.md` for implementation plan.

**Key insight:** Access `IBattlefieldStack` from card's CDC component to get `StackParent`, `StackedCards`, and `AttachmentCount`.

**Files:** `CardModelProvider.cs`, `BattlefieldNavigator.cs`, `ZoneNavigator.cs`, `DuelAnnouncer.cs`

## Limitations

### Browser Navigator

Toggle mechanism for scry/surveil needs API discovery. Detection and navigation work.

### Color Challenge (Tutorial)

- No player targeting support
- MatchTimer UI disabled

## Technical Debt

### Code Archaeology

Accumulated defensive fallback code needs review:
- `ActivateBackButton()` has 4 activation methods - test which are needed
- `LogAvailableUIElements()` (~150 lines) could be behind debug flag
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

### Dropdown Auto-Open Suppression Mechanics (January 2025)

Complex flag-based system to prevent unwanted behavior when MTGA auto-opens dropdowns during navigation. May need simplification in future refactoring.

**The Problem:**
MTGA auto-opens dropdowns when they receive EventSystem selection. When user navigates to a dropdown with arrow keys, we set EventSystem selection, game auto-opens it, and this caused:
1. Unwanted dropdown mode entry
2. Double announcements (from navigator + sync logic)

**Solution - Multiple Suppression Flags:**

**UIFocusTracker.cs:**
- `_suppressNextFocusAnnouncement` - Prevents FocusTracker from announcing when navigator handles the focus change (navigator does its own announcement)
- `_suppressDropdownModeEntry` - Prevents FocusTracker from entering dropdown mode when we just closed an auto-opened dropdown (IsExpanded may still be true briefly)

**BaseNavigator.cs:**
- `_wasInDropdownMode` - Tracks if we were in dropdown mode to trigger `SyncIndexToFocusedElement()` after exiting (for game's auto-advance like Month→Day→Year)
- `_skipDropdownModeTracking` - Prevents `_wasInDropdownMode` from being re-set while IsExpanded is still true after closing auto-opened dropdown

**Flow for closing auto-opened dropdown:**
1. `CloseDropdownOnElement()` calls `Hide()` on dropdown
2. Sets `_wasInDropdownMode = false`
3. Sets `_skipDropdownModeTracking = true`
4. Calls `UIFocusTracker.SuppressDropdownModeEntry()`
5. Next frames: `IsExpanded` may still be true, but flags prevent unwanted mode entry
6. Once `IsExpanded` becomes false, `_skipDropdownModeTracking` is cleared

**Files:** `BaseNavigator.cs`, `UIFocusTracker.cs`

**Future Simplification Ideas:**
- Query dropdown state less frequently (cache with short TTL)
- Single unified "dropdown operation in progress" flag
- Consider if all this complexity is needed or if simpler approach exists

---

### Input Field Auto-Focus on Arrow Navigation (January 2026)

When navigating to an input field with arrow keys, MTGA auto-focuses the field (sets `isFocused=true`), which causes the mod to enter edit mode automatically.

**Current Behavior:**
- Arrow navigating TO an input field auto-enters edit mode
- User must press Escape to exit edit mode and continue navigating
- Tab navigation works correctly (navigates through all elements)
- Up/Down arrows read field content when in edit mode

**Why It's Different From Dropdowns:**
- Dropdowns: `IsExpanded` is set immediately when selected, so we can check and close in `UpdateEventSystemSelection()`
- Input fields: `isFocused` is set asynchronously by the game (next frame), so our deactivation check doesn't catch it

**Attempted Fix:**
Added `DeactivateInputFieldOnElement()` in `UpdateEventSystemSelection()` (mirrors dropdown pattern), but it runs before the game sets `isFocused=true`.

**Potential Future Fix:**
Modify `HandleInput()` to not auto-enter edit mode when `IsAnyInputFieldFocused()` is true. Instead, require explicit Enter to activate the field. This would require:
1. In `HandleInput()`, when field is focused but not in explicit edit mode, deactivate it
2. Only enter edit mode when user presses Enter on an input field
3. Risk: May break mouse-click behavior where user clicks directly on input field

**Files:** `BaseNavigator.cs` (HandleInput, UpdateEventSystemSelection)

## Potential Issues (Monitor)

### NPE Overlay Exclusion for Objective_NPE Elements

Changed `ElementGroupAssigner.DetermineOverlayGroup()` to exclude `Objective_NPE` elements from NPE overlay classification. This allows SparkRank (Objective_NPEQuest) to be grouped with other Objectives instead of being treated as an NPE tutorial overlay element.

**The change:**
```csharp
// Before: parentPath.Contains("NPE") would match Objective_NPEQuest
// After: Added && !parentPath.Contains("Objective_NPE") exclusion
```

**Why:** SparkRank objective was getting its own standalone group instead of being in the Objectives subgroup because its parent path contains "NPE" which matched the NPE overlay check.

**Monitor for:** This might break NPE tutorial screens if any tutorial elements have "Objective_NPE" in their path. Test NPE/tutorial flows when possible.

**Files:** `ElementGroupAssigner.cs`

---

### Card Info Navigation (Up/Down Arrows)

Reported once: Up/Down arrows stopped reading card details during a duel. Cards were announced correctly when navigating with Left/Right, but pressing Up/Down to read card properties (mana cost, type, rules text, etc.) did nothing.

**Could not reproduce.** Diagnostic logging added to track if issue recurs:
- Logs when Up/Down pressed but CardInfoNavigator is not active
- Logs when HandleInput fails due to null card or modifier keys held

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

## Recently Completed

### Collection Card Reading

Collection cards in deck builder are now fully accessible with complete card info extraction:
- Navigation: Left/Right to browse cards, Up/Down to read card details
- Page Navigation: Page Up/Page Down to change collection pages
- All card properties: Name, mana cost, type line, power/toughness, rules text, flavor text, artist

**Technical notes:**
- Cards use `PagesMetaCardView` with `Meta_CDC` component
- Providers found via `ListMetaCardHolder_Expanding.CardDatabase` in Meta scenes
- Placeholder cards (GrpId = 0, "CDC #0") are filtered out - these are empty pool slots from game's virtualization
- Group state is preserved across page changes using `SaveCurrentGroupForRestore()` mechanism

---

### PlayBlade Ranked Match Start

Deck selection was being lost when navigating away from the deck folder.
Fix: Stop collapsing deck folder toggle when exiting group - setting `toggle.isOn = false` was causing the game to deselect the deck.

Note: ModalFade button is NOT the play button (its onClick just calls Hide()). MainButton is the correct play button and respects the selected mode tab.

**Files:** `GroupedNavigator.cs`

---

### Booster Pack Navigation

Pack opening accessible via BoosterOpenNavigator. Left/Right to navigate cards, Enter for details, Backspace to close.

### Friends Panel

Fixed navigation after refactoring. F4 opens panel, Add Friend popup works with full keyboard workflow.

### Hidden Zone Card Counts

D (your library), Shift+D (opponent library), Shift+C (opponent hand).

### Mulligan

Both regular and London mulligan working. C/D for zone navigation, Enter to toggle cards, Space to confirm.

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

**How it gets focused:**
During mana payment mode, Tab can navigate to the ManaPoolButton. FocusTracker announces the extracted text.

**Next Steps:**
1. Investigate ManaPool children - may have per-color elements
2. Check if game has ManaPool data accessible via reflection
3. Look for mana symbols/icons that indicate colors
4. Consider adding a dedicated shortcut to read mana pool during duels

### Upcoming

1. Creature death/exile/graveyard announcements with card names
2. Emote menu
3. Player username announcements
4. Game wins display (WinPips)
5. Player rank information

### Future

1. Draft/sealed events
2. Full deck editing workflow (add/remove cards, save deck)
