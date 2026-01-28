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
- **Cancel buttons**: `Click()` fires callback but popup stays visible - requires Escape key to dismiss

**Why OK Works But Cancel Doesn't:**
The OK button's callback triggers a game state change (e.g., logout) which causes the game to automatically close the popup as part of that state transition. The Cancel button's callback just signals "don't do the action" - it doesn't trigger any state change that would close the popup.

**Current Solution:**
- OK buttons: `Click()` triggers action, game handles popup close automatically
- Cancel buttons: User presses **Escape** key to dismiss (game handles it natively)
- Popup announcement includes "Escape to cancel" hint

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

**What Would Potentially Fix This (Future Investigation):**
- Use Windows API (`user32.dll SendInput`) to simulate actual mouse click at button screen coordinates
- Find and call the game's popup manager close method directly
- Hook into the game's input system at a lower level
- Find the correct way to invoke HandleOnClick with proper ButtonData

**Current Workaround:**
- OK/Confirm: Press Enter - works correctly
- Cancel/Dismiss: Press **Escape** key (game handles it natively)
- Popup announcement includes "Escape to cancel" instruction

**Files:** `UIActivator.cs`, `SettingsMenuNavigator.cs`, `PanelStateManager.cs`

---

### Login Screen Back Button

Back button on login panel doesn't respond to keyboard activation (Enter or Backspace). Likely uses undiscovered activation mechanism.

**Workaround:** Use mouse, or restart game to return to Welcome screen.

---

### Registration Screen

Dropdown auto-advance causes navigation confusion. Basic dropdown navigation works, but full registration flow needs more testing.

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

### Deck Builder Collection Card Reading

Collection cards (cards you can add to your deck) are now navigable but card info extraction is incomplete.

**What's Working:**
- **Navigation:** Arrow Left/Right navigates between collection cards
- **Card Name:** Extracted from Model via GrpId → localization lookup
- **Type Line:** Extracted from Model.Types
- **Power/Toughness:** Extracted from Model.Power/Model.Toughness
- **Artist:** Extracted from Model.Artist

**Partially Working:**
- **Mana Cost:** Shows as "3 White" instead of "{3}{W}" - needs symbol formatting

**Not Working:**
- **Rules Text:** AbilityTextProvider not found in Meta scenes. Need to find the provider instance used by Meta canvases
- **Flavor Text:** FlavorTextId lookup returns empty string. Different lookup mechanism than duels

**Technical Details:**
- Collection cards use `PagesMetaCardView` component (similar to `BoosterMetaCardView`)
- Located in `PoolHolder` canvas when browsing deck builder collection
- Card data accessed via `Meta_CDC` component (analogous to `DuelScene_CDC` in duels)
- Meta_CDC found on CardView child object, not on PagesMetaCardView itself
- CardModelProvider updated to search for Meta_CDC in children
- Cards with GrpId = 0 are unloaded and show as "Unknown"

**Where to Look:**
- Rules Text: Find AbilityTextProvider instance in Meta assembly (try `AssetBundleAssetLoader` or `Meta_CardObjectVisual`)
- Flavor Text: Check if Meta cards use different FlavorText property or lookup method
- Mana Cost Symbols: Format ManaCost as MTG symbols like "{2}{B}{B}" instead of "4 Black"

**Files:**
- `CardModelProvider.cs` - `GetDuelSceneCDC()`, `ExtractCardInfoFromModel()`, `FindIdNameProvider()`
- `GeneralMenuNavigator.cs` - `FindPoolHolderCards()`, `IsInCollectionCardContext()`
- `CardDetector.cs` - `IsUILabelText()` filter for UI noise
- `ElementGroupAssigner.cs` - `DeckBuilderCollection` group detection

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

## Potential Issues (Monitor)

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

1. Mana pool info
2. Stepper control redesign

### Upcoming

1. Creature death/exile/graveyard announcements with card names
2. Emote menu
3. Player username announcements
4. Game wins display (WinPips)
5. Player rank information

### Future

1. Draft/sealed events
2. Full deck editing workflow (add/remove cards, save deck)
