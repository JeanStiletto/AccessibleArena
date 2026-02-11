# Known Issues

Active bugs and limitations in the MTGA Accessibility Mod.

## Active Bugs

### Bot Match Not Accessible

Bot Match functionality is currently not accessible through the UI.

**Investigation Findings (February 2026):**

1. **No Bot Match button on HomePage:** The HomePage SafeZone only contains:
   - `SafeZone/MainButton` - The "Spiele" (Play) button
   - `SafeZone/Banners/Banner_Left` - Carousel banner
   - `SafeZone/Banners/NavDots` - Carousel navigation dots
   - `SafeZone/Banners/Banners_Right/HomeBanner_Right_Base` - Event feature banners (Starter Deck Duel, Ranked, etc.)

2. **Bot-Match in PlayBlade is NOT a bot match trigger:** The "Bot-Match" option visible in `Blade_ListItem` inside the FindMatch tab is just a format label/filter, NOT a trigger for actual bot games. Clicking it changes the tooltip/description but doesn't configure the queue for bot matches.

3. **Game code still has bot match support:**
   - `HomePageContentController.Coroutine_PlayBotGame` exists
   - `IssueAiBotMatchReq` request class exists
   - `EAiBotMatchType` enum with bot match types exists
   - But no visible UI element triggers these

4. **Documentation vs Reality:** Our docs (GAME_ARCHITECTURE.md, MENU_NAVIGATION.md) mention "Play, Bot Match" as separate buttons on HomePage, but current game version only shows ONE MainButton (Play).

**Likely Cause:** WotC appears to have removed the separate Bot Match button from the main HomePage UI. The `Coroutine_PlayBotGame` code still exists but may only be triggered through:
- New Player Experience (NPE/Sparky) flow
- Internal debug commands
- Some hidden/conditional UI path

**Workaround:** None currently available.

**Potential Solutions:**
1. Investigate if bot match is accessible through NPE/tutorial flow
2. Call `Coroutine_PlayBotGame` directly via reflection (would need to find the HomePageContentController instance)
3. Use `IssueAiBotMatchReq` to start a bot match programmatically

**Files investigated:**
- `GeneralMenuNavigator.cs` - UI element discovery
- `analysis_core_menus.txt` - HomePageContentController classes
- `analysis_models.txt` - AiBotMatch request/response classes

---

### Bot Match Button Not Visible - RESOLVED

Bot Match and Standard play mode buttons (inside `Blade_ListItem_Base`) were not visible when FindMatch PlayBlade was open.

**Root Cause:** `FindDeckViewParent()` in GeneralMenuNavigator incorrectly included `Blade_ListItem` in its pattern match alongside `DeckView_Base`. This caused play mode buttons to be treated as deck elements and added to `deckElementsToSkip`, filtering them from navigation.

**The Fix:** Remove `Blade_ListItem` from `FindDeckViewParent()` check - these are play mode options, not deck entries.

**Lesson Learned - Element Filtering Complexity:**
Making specific UI elements visible requires checking MULTIPLE filtering stages. For `Blade_ListItem` buttons, we had to trace through:

1. **ShouldShowElement()** (GeneralMenuNavigator) - Overlay detection filtering
   - Added `IsInsideBladeListItem()` bypass (turned out not needed after root cause found)

2. **UIElementClassifier.Classify()** - Element classification
   - `IsInternalElement()` checks: `IsHiddenByGameProperties()`, `IsFilteredByNamePattern()`, `IsFilteredByTextContent()`
   - `IsVisibleViaCanvasGroup()` - parent CanvasGroup alpha/interactable checks
   - Added `IsInsideBladeListItem()` bypass in `IsInternalElement()` (defensive)
   - Added meaningful content exception to parent alpha check

3. **Deck element filtering** (GeneralMenuNavigator) - THE ACTUAL CAUSE
   - `FindDeckViewParent()` was matching `Blade_ListItem` as deck parents
   - Elements added to `allDeckElements` then `deckElementsToSkip`
   - Fixed by removing `Blade_ListItem` from the pattern match

**Debug approach that worked:** Add logging at each stage to trace exactly where elements are filtered:
- `Blade_ListItem ADDED` - element passed classification
- `Final loop processing Blade_ListItem` - element in discoveredElements
- `Blade_ListItem SKIPPED by deckElementsToSkip!` - found the culprit

**Files modified:**
- `GeneralMenuNavigator.cs` - `FindDeckViewParent()`, `ShouldShowElement()`, `IsInsideBladeListItem()`
- `UIElementClassifier.cs` - `IsInternalElement()`, `IsVisibleViaCanvasGroup()`, `IsInsideBladeListItem()`

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

### Rewards Popup - RESOLVED

Quest reward popups (`ContentController - Rewards_Desktop_16x9(Clone)`) are now fully accessible with card info navigation.

**The Fix (February 2026):**
1. **Overlay Detection:** `OverlayDetector.IsRewardsPopupOpen()` checks for active ContentController with "Rewards" under `Canvas - Screenspace Popups`
2. **Disabled Grouped Navigation:** Rewards popup uses flat Left/Right navigation (not hierarchical groups)
3. **Card Info Navigation:** Up/Down arrows read card details via `CardInfoNavigator` for card rewards
4. **Multi-Page Support:** "Mehr" (More) button advances to next page of rewards - triggers rescan to pick up new content
5. **Overlay Change Detection:** Automatic rescan when popup opens/closes, ensuring navigation stays in sync

**User Flow:**
1. Claim rewards from mail → Rewards popup appears
2. Left/Right arrows navigate through reward items (packs, cards, sleeves, gold, etc.)
3. Up/Down arrows read card details when focused on a card reward
4. "Mehr" button reveals more rewards (may need multiple presses to show all)
5. Backspace clicks background to progress/dismiss

**Technical Details:**
- `ElementGroup.RewardsPopup` overlay group filters navigation to popup elements only
- `DiscoverRewardElements()` finds `RewardPrefab_*` elements and extracts labels
- `FindCardObjectInReward()` locates card components (BoosterMetaCardView, PagesMetaCardView, etc.)
- `CardDetector.IsCard()` recognizes reward cards for CardInfoNavigator integration
- Rescan triggered after "Mehr" press and on overlay state changes

**Implementation Files:**
- `ElementGroup.cs` - Added `RewardsPopup` enum value
- `OverlayDetector.cs` - Detection and filtering methods
- `GeneralMenuNavigator.cs` - Discovery, navigation, backspace handler, overlay change tracking
- `CardDetector.cs` - Added PagesMetaCardView/MetaCardView component recognition

---

### Activated Abilities with Mana Costs - RESOLVED

Creatures with activated abilities that cost mana (e.g., `{3}{G}: Do something`) can now be activated via keyboard.

**The Fix (February 2026):**

The `WorkflowBrowser` UI element that appears when clicking a creature with an activated ability has no clickable component itself. The actual clickable button (`ConfirmWidgetButton(Clone)`) is a **sibling** of WorkflowBrowser, not a child.

**UI Hierarchy:**
```
UnderStack_ScaledCorrectly/
├── WorkflowBrowser          ← Just audio (AkGameObj), no click handler
├── ViewDismissBrowser
├── ConfirmWidget(Clone)
│   └── ConfirmWidgetButton(Clone)  ← THE ACTUAL BUTTON (Button + EventTrigger)
```

**Solution:** Modified `FindClickableChild()` in `BrowserDetector.cs` to search sibling hierarchies for `ConfirmWidgetButton` when no clickable child is found in the container itself.

**User Flow:**
1. Navigate to creature with activated ability (B key for creatures row)
2. Press Enter to click the creature
3. "Choose action" announced, shows "Fähigkeit aktivieren" (Activate ability)
4. Press Space/Enter to activate
5. Game auto-taps lands and puts ability on stack

**Debug Tools Available:**
For future browser fixes, comprehensive debug logging tools exist (disabled by default):
- `BrowserDetector.DumpWorkflowBrowserDebug()` - UI structure dump
- `MenuDebugHelper.DumpWorkflowSystemDebug()` - Full workflow system dump
- Enable via `BrowserDetector.EnableDebugForBrowser(browserType)` for targeted debugging

**Files:** `BrowserDetector.cs`

---

### Space Key Pass Priority

Game's native Space keybinding doesn't work reliably after using mod navigation. HotHighlightNavigator now clicks the primary button directly as workaround.

---

### Rapid Card Play

Multiple rapid Enter presses can trigger card play sequence multiple times.

---

### Combat Blocker Selection

Strange interactions may occur after selecting a target for blocks. Needs testing.

---

### Deck Folder Navigation with Dropdowns

Deck folder navigation (Enter to expand, Backspace to collapse) may have buggy behavior when dropdown elements are present on the same screen. The interaction between folder toggle activation and dropdown state tracking can cause unexpected navigation states.

**Symptoms:**
- Navigation position getting lost after folder operations
- Unexpected focus on dropdown elements

**Files:** `GeneralMenuNavigator.cs`, `GroupedNavigator.cs`

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

### PlayBlade Backspace Navigation - RESOLVED

Backspace from PlayBladeContent group now correctly navigates back to PlayBladeTabs.

**The Fix (February 2026):**
- PlayBladeNavigationHelper refactored to derive state from `GroupedNavigator.CurrentGroup` instead of maintaining a separate state machine
- HandleBackspace checks group type directly instead of relying on `IsPlayBladeContext` flag (which could be stale during blade Hide/Show cycles)
- Full hierarchy: Tabs → Content → Folders → Folder (decks). Backspace goes up the hierarchy.

**Files:** `PlayBladeNavigationHelper.cs`, `GeneralMenuNavigator.cs`

---

### Enchantment/Attachment Announcements

**Implemented** using `Model.Instance.AttachedToId` (discovered via decompiling `UniversalBattlefieldStack`).
Previous approach using `Model.Parent`/`Model.Children` always returned null - those properties aren't used by the game.

**Status:** Working. `AttachedToId` is a field (not property) on `MtgCardInstance`, accessed via `GetField()`. Cached FieldInfo for performance.

**Files:** `CardModelProvider.cs` (GetAttachments, GetAttachedTo, GetAttachmentText), `DuelAnnouncer.cs` (GetAttachedToName)

## Limitations

### Browser Navigator

Toggle mechanism for scry/surveil needs API discovery. Detection and navigation work.

LargeScrollList browser (keyword choice UI, e.g. Entstellender Künstler) now discovers choice buttons. The scaffold MainButton ("Spielfeld betrachten") is still included at the end of navigation but may not be useful - consider removing it from LargeScrollList browsers if testing confirms it serves no purpose.

### Color Challenge (CampaignGraph)

- **Navigation fixed (February 2026):** CampaignGraph is now treated as a regular content page, not a PlayBlade overlay
- Blade state buttons (Btn_BladeIsClosed, Btn_BladeHoverClosed, Btn_BladeIsOpen) filtered from navigation
- Backspace navigates Home (game has no native back-to-PlayBlade mechanism)
- No player targeting support
- MatchTimer UI disabled

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

**If implemented:**
- Track zone-leaving events with timestamp
- When zone-reaching event fires, check if matching zone-leaving event occurred within threshold (e.g., 100-200ms)
- Combine into single announcement: "X died and went to graveyard"
- Handle edge cases: exile instead of graveyard, mill vs draw vs discard, etc.

**Priority:** Low - current behavior is functional, just slightly verbose

---

### Dropdown Auto-Open Suppression Mechanics (January 2025)

MTGA auto-opens dropdowns when they receive EventSystem selection. When user navigates to a dropdown with arrow keys, we set EventSystem selection, game auto-opens it. Auto-opened dropdowns are immediately closed; user must press Enter to open.

**Announcement Ownership:**
- `UIFocusTracker.NavigatorHandlesAnnouncements` - When a navigator is active, UIFocusTracker skips announcements (navigators handle their own). Set each frame from `NavigatorManager.HasActiveNavigator`.
- Exception: When a dropdown is open (`DropdownStateManager.IsInDropdownMode`), UIFocusTracker still announces because Unity's native navigation controls dropdown item focus.

**Dropdown State (DropdownStateManager):**
- `IsInDropdownMode` - Queries actual `IsExpanded` property, accounts for suppression after auto-close
- `SuppressReentry()` - Prevents re-entering dropdown mode while `IsExpanded` is still true after `Hide()` call
- `UpdateAndCheckExitTransition()` - Detects dropdown close for navigator index sync (handles game auto-advance like Month→Day→Year)

**Files:** `UIFocusTracker.cs`, `DropdownStateManager.cs`, `AccessibleArenaMod.cs`

## Potential Issues (Monitor)

### Vault Progress Objects in Packs

Pack opening sometimes shows multiple identical "Alchemy Bonus Vault Progress +99" items alongside actual cards (e.g., 6 cards + 3 vault progress = 9 items). This appears to be game behavior, not a mod bug.

**Observations:**
- Occurs even on non-Alchemy packs (e.g., Final Fantasy/FIN packs show "Alchemy Bonus" vault progress)
- Multiple vault progress objects have identical labels because they share the same tags and quantity
- F11 dump confirms all vault objects have same text content (Progress: +99, Tags: Alchemy, Bonus)
- Digital-only sets (Final Fantasy, etc.) are classified as "Alchemy" internally by Arena

**Note:** The "First" and "New" tags (inactive) on vault progress may indicate new player bonuses rather than duplicate protection, but this is unconfirmed.

---

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

**Future consideration:**
A cleaner solution would be full EventSystem override (never use it), but some MTGA elements require EventSystem focus to work correctly (dropdowns, input field text entry). May need cleanup/simplification if this becomes too fragile.

**Files:** `BaseNavigator.cs`, `UIFocusTracker.cs`, `KeyboardManagerPatch.cs`

## Recently Completed

### Deck Builder Search - RESOLVED

Search field filtering and page navigation in deck builder now works correctly and fast.

**Previous Issues:**
- Wrong cards shown after search (offscreen page cards contaminating results)
- Slow rescan (~1.6s at game's ~18fps rate due to 30-frame delay designed for 60fps)
- `LogAvailableUIElements()` debug dump running on every rescan (500-750ms)

**Solution (February 2026):**
- `CardPoolAccessor` reads only current visible page's cards via `_pages[1].CardViews`
- Page navigation via `ScrollNext()` / `ScrollPrevious()` instead of button searching
- Search rescan reduced to 12 frames (~645ms at ~18fps)
- Page rescan reduced to 8 frames with `IsScrolling()` short-circuit for early completion
- Removed redundant `LogAvailableUIElements()` from PerformRescan (still runs once per screen in DetectScreen)

**Files:** `CardPoolAccessor.cs`, `GeneralMenuNavigator.cs`, `BaseNavigator.cs`, `GroupedNavigator.cs`

---

### Collection Card Reading

Collection cards in deck builder are now fully accessible with complete card info extraction:
- Navigation: Left/Right to browse cards, Up/Down to read card details
- Page Navigation: Page Up/Page Down to change collection pages, announces "Page X of Y"
- All card properties: Name, mana cost, type line, power/toughness, rules text, flavor text, artist

**Technical notes:**
- `CardPoolAccessor` accesses game's `CardPoolHolder` via reflection for direct page control
- `_pages[1].CardViews` returns only the current visible page's cards (no offscreen contamination)
- `ScrollNext()` / `ScrollPrevious()` called directly instead of searching for UI buttons
- Page boundary feedback: "First page" / "Last page" announced at edges
- Placeholder cards (GrpId = 0, "CDC #0") are filtered out - empty pool slots from game's virtualization
- Group state preserved across page changes using `SaveCurrentGroupForRestore()` mechanism
- Fallback to hierarchy scan if CardPoolHolder component not found

---

### PlayBlade Ranked Match Start

Deck selection was being lost when navigating away from the deck folder.
Fix: Stop collapsing deck folder toggle when exiting group - setting `toggle.isOn = false` was causing the game to deselect the deck.

Note: ModalFade button is NOT the play button (its onClick just calls Hide()). MainButton is the correct play button and respects the selected mode tab.

**Files:** `GroupedNavigator.cs`

---

### PlayBlade Folder Navigation Enter Key Fix

Pressing Enter on a deck folder toggle in PlayBlade was activating the wrong element (e.g., "Bot-Match" button instead of the folder toggle).

**Root cause:** When `EventSystemPatch` blocks Enter key on a toggle (to prevent double-toggle), it sets `EnterPressedWhileBlocked` flag. However, `InputManager.GetEnterAndConsume()` (used by grouped navigation) only checked `Input.GetKeyDown()` and NOT the blocked flag. This caused:
1. `GetEnterAndConsume()` returns false (because `Input.GetKeyDown` was blocked)
2. `HandleGroupedEnter()` never runs
3. `BaseNavigator.HandleInput()` sees the blocked flag and activates `_elements[_currentIndex]` (wrong element from flat list)

**The fix:** Updated `GetEnterAndConsume()` to also check `EnterPressedWhileBlocked` and mark it as handled, so grouped navigation properly intercepts Enter presses on toggles.

**Files:** `InputManager.cs`

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
3. Single-key info shortcuts (inspired by Hearthstone Access)
   - Quick status queries without navigation
   - Benefits: Faster information access, less navigation needed

   **Priority shortcuts to implement:**

   **M / Shift+M - Mana Announcement**
   - M: Announce "X mana available of Y total" (your mana)
   - Shift+M: Announce opponent's mana
   - Requires: Finding mana data from game state (not currently extracted)
   - Hearthstone Access equivalent: A/Shift+A
   - Research needed: Locate mana values in GameManager or player state objects

   **K - Keyword Explanation**
   - When focused on a card, K announces keyword abilities with definitions
   - Example: "Flying. This creature can only be blocked by creatures with flying or reach."
   - Example: "Deathtouch. Any amount of damage this deals to a creature is enough to destroy it."
   - Requires: Keyword detection from card rules text + keyword definition database
   - Research needed:
     - Parse keywords from Model.Abilities or rules text
     - Build keyword definition lookup table
     - Handle multiple keywords per card
   - Hearthstone Access equivalent: I (keyword info) + K (minion stats)

   **O - Game Log (Play History)**
   - O: Announce recent game events (last 5-10 actions)
   - Example: "Opponent played Mountain. You drew Lightning Bolt. Opponent attacked with Goblin."
   - Requires: Tracking game events in DuelAnnouncer and storing history
   - Research needed:
     - Store announced events in a circular buffer
     - Format history for reading (most recent first or chronological?)
     - Determine how many events to announce
   - Hearthstone Access equivalent: Y (play history)

   **Other potential shortcuts (lower priority):**
   - P: Phase and turn info (partially covered by T)
   - Hearthstone Access reference:
     - C/Shift+C: Navigates to hand (same as our C)
     - A/Shift+A: Mana (we'll use M to avoid conflict with Lands row)
     - B/G: Board state
     - V/F: Hero/player status
     - Y: Play history (we'll use O)

4. Verbose "Big Card" announcements (inspired by Hearthstone Access)
   - Option to include card details inline with action announcements
   - User preference toggle: brief vs verbose announcements

   **Current behavior:**
   - "Opponent played Lightning Bolt" (action only)
   - Player navigates to card and uses Up/Down to read details

   **Verbose "big card" option:**
   - "Opponent played Lightning Bolt; Lightning Bolt deals 3 damage to any target"
   - Full card info announced with the action

   **Hearthstone Access patterns:**
   - Spells: "{player} played {name}; {rules text}"
   - Creatures: "{player} summoned {name}; {power} {toughness} {rules text}"
   - Triggered abilities: "{player}'s {name} triggered; {effect text}"

   **Implementation considerations:**
   - Add setting toggle (brief/verbose) in mod config
   - Modify DuelAnnouncer event handlers to optionally append card text
   - Reuse CardModelProvider to get rules text for announced cards
   - Consider length limits - some cards have very long text
   - May want separate toggles per event type (plays, triggers, deaths, etc.)

   **Trade-offs:**
   - Verbose: More info upfront, less navigation needed
   - Brief: Less audio clutter, player reads details when interested
   - Could be overwhelming during complex board states with many triggers
