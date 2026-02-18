# Resolved Issues

Issues that have been fixed. Kept for reference and investigation history.

---

## Bot Match Button Not Visible

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

## Confirmation Dialog Buttons (Popup Buttons) - Settings Logout

Popup buttons in Settings menu (e.g., Logout confirmation with Abbrechen/OK) don't close properly or corrupt game state.

**The Core Problem:**
- `SystemMessageButtonView.Click()` invokes the button's callback but doesn't close the popup
- The game's popup system tracks internal state that we can't properly update via reflection
- Forcibly hiding popups with `SetActive(false)` corrupts game state, preventing popup reopen

**Attempted Approaches (January 2025):**

1. **Click() alone**
   - Result: Callback invoked but popup stays open, no visual change

2. **Click() + SetActive(false)** - BEST VISUAL RESULT
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

6. **Click() + SetActive(false) + ReportPanelClosedByName()** - BEST MOD INTEGRATION
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

**Root Cause Theory:**
The popup close mechanism is tied to the game's internal popup manager/queue system. When a popup button is clicked normally with a mouse:
1. Unity's input system detects the click
2. GraphicRaycaster determines what was clicked
3. CustomButton receives pointer events and updates internal state (`_mouseDown = true`)
4. Button callback fires
5. Something in the click chain notifies the popup manager
6. Popup manager handles close animation and state cleanup

Our reflection-based calls can trigger step 4 (callback fires, action happens) but miss steps 1-3 and 5-6. The popup manager never gets notified that a button was clicked, so it doesn't close the popup.

**Implementation:** See `UIActivator.TryDismissViaSystemMessageManager()`

**Files:** `UIActivator.cs`, `SettingsMenuNavigator.cs`, `PanelStateManager.cs`

---

## Login Screen Back Button

Back button on login panel now works with keyboard.

---

## Registration Screen

Dropdown auto-advance issue resolved. Registration flow works.

---

## Rewards Popup

Quest reward popups (`ContentController - Rewards_Desktop_16x9(Clone)`) are now fully accessible with card info navigation.

**The Fix (February 2026):**
1. **Overlay Detection:** `OverlayDetector.IsRewardsPopupOpen()` checks for active ContentController with "Rewards" under `Canvas - Screenspace Popups`
2. **Disabled Grouped Navigation:** Rewards popup uses flat Left/Right navigation (not hierarchical groups)
3. **Card Info Navigation:** Up/Down arrows read card details via `CardInfoNavigator` for card rewards
4. **Multi-Page Support:** "Mehr" (More) button advances to next page of rewards - triggers rescan to pick up new content
5. **Overlay Change Detection:** Automatic rescan when popup opens/closes, ensuring navigation stays in sync

**Implementation Files:**
- `ElementGroup.cs` - Added `RewardsPopup` enum value
- `OverlayDetector.cs` - Detection and filtering methods
- `GeneralMenuNavigator.cs` - Discovery, navigation, backspace handler, overlay change tracking
- `CardDetector.cs` - Added PagesMetaCardView/MetaCardView component recognition

---

## Activated Abilities with Mana Costs

Creatures with activated abilities that cost mana (e.g., `{3}{G}: Do something`) can now be activated via keyboard.

**The Fix (February 2026):**

The `WorkflowBrowser` UI element that appears when clicking a creature with an activated ability has no clickable component itself. The actual clickable button (`ConfirmWidgetButton(Clone)`) is a **sibling** of WorkflowBrowser, not a child.

**Solution:** Modified `FindClickableChild()` in `BrowserDetector.cs` to search sibling hierarchies for `ConfirmWidgetButton` when no clickable child is found in the container itself.

**Files:** `BrowserDetector.cs`

---

## Rapid Card Play

Multiple rapid Enter presses no longer trigger card play sequence multiple times.

---

## Combat Blocker Selection

Blocker selection interactions tested and working correctly.

---

## Deck Folder Navigation with Dropdowns

Deck folder navigation (Enter to expand, Backspace to collapse) now works correctly alongside dropdown elements.

**Files:** `GeneralMenuNavigator.cs`, `GroupedNavigator.cs`

---

## NPE Rewards Button

NullClaimButton ("Take reward") now correctly added to navigation.

---

## Targeting Mode

All targeting modes working:
- Player targets via `DuelScene_AvatarView.HighlightSystem._currentHighlightType` reflection
- "Any target" spells
- Stack spell targets
- Triggered abilities requiring targets

---

## Deck Builder - Deck List Card Info

Deck list cards now show full card info (name, quantity, mana cost, type, rules text, etc.) when reading with Up/Down arrows.

**Files:** `CardModelProvider.cs`

---

## PlayBlade Backspace Navigation

Backspace from PlayBladeContent group now correctly navigates back to PlayBladeTabs.

**The Fix (February 2026):**
- PlayBladeNavigationHelper refactored to derive state from `GroupedNavigator.CurrentGroup` instead of maintaining a separate state machine
- HandleBackspace checks group type directly instead of relying on `IsPlayBladeContext` flag (which could be stale during blade Hide/Show cycles)
- Full hierarchy: Tabs -> Content -> Folders -> Folder (decks). Backspace goes up the hierarchy.

**Files:** `PlayBladeNavigationHelper.cs`, `GeneralMenuNavigator.cs`

---

## Enchantment/Attachment Announcements

**Implemented** using `Model.Instance.AttachedToId` (discovered via decompiling `UniversalBattlefieldStack`).
Previous approach using `Model.Parent`/`Model.Children` always returned null - those properties aren't used by the game.

`AttachedToId` is a field (not property) on `MtgCardInstance`, accessed via `GetField()`. Cached FieldInfo for performance.

**Files:** `CardModelProvider.cs` (GetAttachments, GetAttachedTo, GetAttachmentText), `DuelAnnouncer.cs` (GetAttachedToName)

---

## Browser Navigator

Browser toggle mechanism for scry/surveil working. LargeScrollList browser discovers choice buttons.

---

## Color Challenge (CampaignGraph)

- CampaignGraph is now treated as a regular content page, not a PlayBlade overlay
- Blade state buttons filtered from navigation
- Backspace navigates Home (game has no native back-to-PlayBlade mechanism)

---

## Bot Match via PlayBlade

Bot Match is now accessible through the PlayBlade UI: Open Play -> Bot-Match -> select deck -> Play starts a bot match.

**Root Cause:** The PlayBlade's Bot-Match button is just a format filter - it passes a regular event name (not "AIBotMatch") to `HomePageContentController.JoinMatchMaking`. The game only starts bot matches when `internalEventName == "AIBotMatch"`.

**Solution (February 2026):** Harmony prefix patch on `JoinMatchMaking` that replaces the event name with "AIBotMatch" when the mod detects Bot-Match mode was selected.

**How it works:**
1. User selects "Bot-Match" in PlayBlade Open Play tab
2. `GeneralMenuNavigator.OnElementActivated` detects "Bot" in element text -> sets `IsBotMatchMode = true`
3. User selects deck, auto-play clicks the MainButton
4. `JoinMatchMaking(regularEventName, deckGuid)` is called by PlayBlade signal dispatch
5. Harmony prefix intercepts: replaces event name with "AIBotMatch", clears flag
6. Game routes to `Coroutine_PlayBotGame` -> instant bot match

**Files:** `PlayBladeNavigationHelper.cs`, `GeneralMenuNavigator.cs`, `PanelStatePatch.cs`

---

## Deck Builder Search

Search field filtering and page navigation in deck builder now works correctly and fast.

**Solution (February 2026):**
- `CardPoolAccessor` reads only current visible page's cards via `_pages[1].CardViews`
- Page navigation via `ScrollNext()` / `ScrollPrevious()` instead of button searching
- Search rescan reduced to 12 frames (~645ms at ~18fps)
- Page rescan reduced to 8 frames with `IsScrolling()` short-circuit for early completion

**Files:** `CardPoolAccessor.cs`, `GeneralMenuNavigator.cs`, `BaseNavigator.cs`, `GroupedNavigator.cs`

---

## Collection Card Reading

Collection cards in deck builder are now fully accessible with complete card info extraction:
- Navigation: Left/Right to browse cards, Up/Down to read card details
- Page Navigation: Page Up/Page Down to change collection pages, announces "Page X of Y"
- All card properties: Name, mana cost, type line, power/toughness, rules text, flavor text, rarity, artist

**Files:** `CardPoolAccessor.cs`, `GeneralMenuNavigator.cs`

---

## PlayBlade Ranked Match Start

Deck selection was being lost when navigating away from the deck folder.
Fix: Stop collapsing deck folder toggle when exiting group - setting `toggle.isOn = false` was causing the game to deselect the deck.

Note: ModalFade button is NOT the play button (its onClick just calls Hide()). MainButton is the correct play button and respects the selected mode tab.

**Files:** `GroupedNavigator.cs`

---

## PlayBlade Folder Navigation Enter Key Fix

Pressing Enter on a deck folder toggle in PlayBlade was activating the wrong element.

**Root cause:** When `EventSystemPatch` blocks Enter key on a toggle (to prevent double-toggle), it sets `EnterPressedWhileBlocked` flag. However, `InputManager.GetEnterAndConsume()` only checked `Input.GetKeyDown()` and NOT the blocked flag.

**The fix:** Updated `GetEnterAndConsume()` to also check `EnterPressedWhileBlocked` and mark it as handled.

**Files:** `InputManager.cs`

---

## Booster Pack Navigation

Pack opening accessible via BoosterOpenNavigator. Left/Right to navigate cards, Enter for details, Backspace to close.

---

## Friends Panel

Fixed navigation after refactoring. F4 opens panel, Add Friend popup works with full keyboard workflow.

---

## Hidden Zone Card Counts

D (your library), Shift+D (opponent library), Shift+C (opponent hand).

---

## Mulligan

Both regular and London mulligan working. C/D for zone navigation, Enter to toggle cards, Space to confirm.
