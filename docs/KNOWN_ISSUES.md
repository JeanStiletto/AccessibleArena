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

**Partially working**

- F4 toggles panel, Tab navigates, Backspace closes
- Add Friend popup detected and announced
- Input field fully accessible:
  - Left/Right arrows navigate cursor and announce character
  - Up/Down arrows announce full content
  - Typing works without mod interference
- Friend list not yet navigable
- Friend status/online indicators not announced

## Technical Debt

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
