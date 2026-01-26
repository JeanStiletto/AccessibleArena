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
