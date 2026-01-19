# Known Issues

Active bugs and limitations in the MTGA Accessibility Mod.

## Active Bugs

### Confirmation Dialogs (SystemMessageView)

**Cancel button navigation issue**

Confirmation dialogs (e.g., Exit Game confirmation) are now detected and navigable with OK/Cancel buttons. However, after pressing Cancel, the mod doesn't properly return to the previous menu (Settings). The popup closes visually (confirmed via OCR), but the mod gets confused about panel hierarchy (Home → Settings → Popup) and enters a loop.

Attempted fixes included: popup detection cooldowns, saving/restoring previous foreground panel, skipping panel detection during cooldown. The underlying issue is complex interaction between multiple panel tracking systems (popup detection, Settings overlay detection, foreground panel management) that need refactoring.

**Workaround:** Press OK to confirm, or use mouse to click Cancel.

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

### Friends Panel (Social UI)

**Broken after GeneralMenuNavigator code quality refactoring (Jan 2026)**

Previously working, now shows only 2 elements ("Freunde" header, "social corner icon") instead of friend list.

**Symptoms:**
- F4 opens panel, announces "Friends. 2 items"
- Actual friend entries (Backer_Hitbox with names like "jean stiletto") are filtered out
- Debug log shows: `Friends element REJECTED by Classifier: Backer_Hitbox IsNavigable=False ShouldAnnounce=False`

**Root cause investigation:**
The Friends panel uses non-standard UI where clickable elements (Backer_Hitbox) don't have direct TMP_Text children. The text comes from parent/sibling elements and is extracted via special `GetText()` logic, but `HasActualText()` returns false.

Multiple filters in `UIElementClassifier` check `HasActualText()` and filter out elements without it:
1. `IsHiddenByGameProperties()` - filters non-interactable CustomButtons without actual text
2. `IsVisibleViaCanvasGroup()` - filters non-interactable CanvasGroup elements without actual text
3. `IsFilteredByNamePattern()` - has exceptions for "backer"/"hitbox" inside FriendsWidget

**Fixes attempted (all still result in filtering):**
1. Added `IsInsideFriendsWidget()` exception to `IsFilteredByNamePattern()` for "dismiss", "backer", "hitbox"
2. Added `IsInsideFriendsWidget()` exception to `IsHiddenByGameProperties()`
3. Added `IsInsideFriendsWidget()` exception to `IsVisibleViaCanvasGroup()`
4. Changed `hasMeaningfulContent` check to:
   ```csharp
   bool hasMeaningfulContent = UITextExtractor.HasActualText(obj)
       || (IsInsideFriendsWidget(obj) && !string.IsNullOrEmpty(UITextExtractor.GetText(obj)));
   ```

**UI structure of Friends panel elements:**
```
SocialUI_V2_Desktop_16x9(Clone)/MobileSafeArea/FriendsWidget_Desktop_16x9(Clone)/BottomTabBar/Backer_Hitbox
  Text: 'jean stiletto' | HasActualText: False | HasImage: True | Size: -50x0
  Components: RectTransform, CanvasRenderer, Image, CustomButton, RolloverAudioPlayer

SocialUI_V2_Desktop_16x9(Clone)/MobileSafeArea/FriendsWidget_Desktop_16x9(Clone)/Button_AddFriend/Backer_Hitbox
  Text: 'add friend' | HasActualText: False | HasImage: True | Size: 0x0
```

**Key observations:**
- `GetText()` returns friend names correctly (e.g., "jean stiletto")
- `HasActualText()` returns false (no TMP_Text child component)
- Elements have invalid/zero size (-50x0, 0x0)
- Elements have CustomButton but may not be "interactable" per game's definition

**Next steps to investigate:**
1. Add debug logging to `IsInternalElement()` to see exactly which sub-check is filtering
2. Check if `IsInsideFriendsWidget()` is returning true correctly
3. Check if there's a parent CanvasGroup filter catching them
4. Consider bypassing ALL filtering for FriendsWidget elements with extractable text
5. Look at git history before UI rework (commit 6ec5bf9) to see what was different

**Relevant code locations:**
- `UIElementClassifier.cs`: `IsInternalElement()`, `IsHiddenByGameProperties()`, `IsVisibleViaCanvasGroup()`, `IsFilteredByNamePattern()`, `IsInsideFriendsWidget()`
- `GeneralMenuNavigator.cs`: `TryAddElement()` (has debug logging for Friends elements)
- `MenuScreenDetector.cs`: `IsSocialPanelOpen()`

**Working features (unchanged):**
- Add Friend popup detected and announced
- Input field fully accessible (arrow keys, typing)
- F4 toggle works, Backspace closes

## Technical Debt

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

## Recently Completed

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
