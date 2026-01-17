# Known Issues

Active bugs and limitations in the MTGA Accessibility Mod.

## Active Bugs

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

**London Mulligan (putting card back): NOT WORKING**
- After mulliganing, you must put 1 card on bottom of library per mulligan taken
- BrowserScaffold_London detected, SubmitButton ("Fertig"/"Done") found
- Card selection requires DRAG interaction - keyboard simulation doesn't work
- **Current workaround:** Use mouse to drag card to bottom pile, then Space to confirm

**What was tried for London card selection:**
1. Clicking the card directly (CardInput.OnPointerClick) - no effect
2. Clicking BrowserCardHolder_ViewDismiss - state stays "keep"
3. ProcessInteraction(SimpleInteractionType) - no effect
4. Simulating drag via OnBeginDrag/OnDrag/OnEndDrag - no effect
5. Dragging to LocalLibrary zone - no effect
6. Dragging to LibraryContainer - no effect

**Why drag simulation fails:**
- Unity's PointerEventData needs raycast info and screen coordinates
- Game validates drop targets via raycast, not just position
- Fake event data lacks the context the game's drag handlers expect

**Future investigation directions:**
- Search game DLLs for methods that move cards in London mulligan
- Look for `CardBrowserCardHolder` methods like `MoveCard`, `TransferCard`, `SelectCard`
- Check if `BrowserScaffold_London` has a `SelectCardForBottom` or similar API
- Look for `LondonMulligan` class or related interaction handlers
- Check `WorkflowController` for London mulligan workflow
- Search for `BottomOfLibrary` or `PutOnBottom` methods
- Investigate what message the game sends to server when card is selected

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
