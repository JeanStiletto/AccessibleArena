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

**Partially working**

- Tab navigation and Space confirmation work
- Card count varies (1 card vs 6-7) depending on phase
- Button text varies ("Keep", "Opponent's Turn", "Done")
- Phase 3 (putting cards back after mulligan) needs more testing
- Some unspecified bugs reported (need reproduction steps)

### Friends Panel (Social UI)

**Partially working**

- F4 toggles panel, Tab navigates, Backspace closes
- Add Friend popup detected and announced
- Input field typing works but content changes not immediately reflected
- Input field content only updates after panel reopen
- Friend list not yet navigable
- Friend status/online indicators not announced

## Needs Testing

### Menu Navigation

- Deck selection screen Submit Deck / View Deck buttons
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

## Planned Features

### Immediate

1. Life total announcements (L shortcut) - fix reliability
2. Mana pool info (A shortcut or from ManaPoolString)

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
