# Known Issues

Active bugs and limitations in the MTGA Accessibility Mod.

## Active Bugs

### Bot Match Button Not Visible

Bot Match button not visible when PlayBlade is open.

**Likely cause:** May not match PlayBlade parent path patterns in `IsInsidePlayBlade()`

**Files:** `OverlayDetector.cs`, `ElementGroupAssigner.cs`

---

### Confirmation Dialog Cancel Button

Cancel button in confirmation dialogs (e.g., Logout) requires two Enter presses. First press doesn't close the popup, second press works.

**Workaround:** Press Enter twice, or use Escape to close.

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

## Potential Issues (Monitor)

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

1. Deck building interface
2. Collection browser
3. Draft/sealed events
