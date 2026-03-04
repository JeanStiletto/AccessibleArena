# Known Issues

Active bugs, limitations, and planned work for Accessible Arena.

For resolved issues and investigation history, see docs/old/RESOLVED_ISSUES.md.

## Active Bugs

### Spell Resolved Announcement Too Early or Repeated

"Spell resolved" announcement sometimes fires too early or multiple times for a single spell.

---

### Battlefield Cards Splitting Into Two Stacks

Cards on the battlefield sometimes split into two separate stacks/rows when they should be grouped together.

---

### Bot Match Not Working From Recent Played

Starting a bot match from the "Recent Played" section does not work properly.

---

### Color Challenge Deck Name Not Refreshing

When selecting a different color in Color Challenge, the announced deck name does not update to reflect the newly selected color's deck.

---

### Weekly Progress Enter Locks Mod

Pressing Enter on the Weekly Progress element puts the mod in a locked state requiring screen transition to recover.

---

### Some Progress Items Open Buggy Screens

Pressing Enter on specific Progress items for certain modes opens the corresponding screen but may create buggy navigation states.

---

### Workflow Browser Cannot Cancel Mana-Costing Activated Abilities

When activating a mana-costing ability on a battlefield card, the workflow browser opens but Backspace does not cancel it.

---

## Game Behavior (Not Fixable by Mod)

### Resolution Dropdown Shows Native Display Resolution Until Changed

The resolution dropdown in Settings > Graphics always shows the native display resolution (e.g., "2880x1800") instead of the game's actual render resolution (e.g., "1920x1080") until the user interacts with it and selects a value.

**Root cause:** The game initializes the dropdown with `value=0` (first option = highest resolution) and never updates `m_Value` or `captionText` to reflect the actual internal render resolution. When the dropdown opens, the game internally corrects the focused item (scrolls to the correct option), but this correction is never synced back to the closed dropdown's display.

**Why it can't be fixed by the mod:** All Unity APIs (`Screen.width`, `Screen.height`, `Screen.currentResolution`, `Camera.main.pixelWidth/Height`) return the native display resolution in borderless fullscreen mode. The game's internal render resolution is stored in a game-specific setting not exposed to standard Unity APIs.

**Impact:** Only affects the first announcement of the resolution dropdown before user interaction. All other dropdowns (language, display mode, etc.) work correctly.

**Files:** `UIElementClassifier.cs` (CorrectStaleDropdownValue — attempts Screen.width correction but cannot fix borderless fullscreen case)

---

### Token Attack Selection Uses Game's Internal Order

When clicking a non-attacking token during declare attackers, the game always selects the first available token in its internal order, regardless of which specific token CDC was clicked. This is game behavior for identical tokens (e.g., Goblin tokens). Clicking an already-attacking token correctly deselects that specific one.

**Confirmed not a position issue:** BattlefieldNavigator now sends each card's actual screen position via `Camera.main.WorldToScreenPoint`, and tokens at different positions (e.g., 127px apart) still exhibit this behavior. The game intentionally ignores which token object receives the click event.

**For sighted users:** Tokens are visually stacked; clicking the stack selects them in order. This is by design.

**Workaround:** Use Space ("All Attack") then deselect specific tokens, or accept that tokens are selected in the game's internal order.

**Investigation history:**
- Failed fix 1: Setting `pointerCurrentRaycast`/`pointerPressRaycast` in CreatePointerEventData - broke all card plays (incomplete RaycastResult struct)
- Failed fix 2: `Camera.main.WorldToScreenPoint` in generic `GetScreenPosition` - broke hand card playing (hand cards are also 3D objects)
- Fix 3 (kept): Battlefield-specific position override in `BattlefieldNavigator.ActivateCurrentCard()` - correct positions but game ignores them for tokens. Kept for potential benefit with non-token overlapping cards.

**Files:** `UIActivator.cs` (SimulatePointerClick overload), `BattlefieldNavigator.cs` (ActivateCurrentCard)

---

## Under Investigation

### Auto-Craft on Collection Card Activation

**Status:** Fixed (two-part fix).

**Symptom:** Pressing Enter on a collection card in the deck builder opened the craft popup AND immediately crafted 1 copy.

**Root cause (two issues):**
1. All click simulation paths (OnAddClicked, CustomButton, pointer click) called `DeckBuilderActionsHandler.OpenCardViewer()` with default `quantityToCraft=1`
2. The game's `PopupManager.HandleKeyUp(Return)` calls `_activePopup.OnEnter()` on the CardViewerController, which auto-calls `OnCraftClicked()` if the craft button is interactable. Our mod activates on KeyDown, popup opens, then the same Enter's KeyUp triggers OnEnter → craft.

**Fix:**
1. `UIActivator.TryActivateCollectionCard` calls `OpenCardViewer` directly via reflection with `quantityToCraft=0`, bypassing all click simulation
2. `InputManager.BlockNextEnterKeyUp` flag blocks the Enter KeyUp from reaching PopupManager after opening the popup

**Files:** `UIActivator.cs`, `InputManager.cs`, `KeyboardManagerPatch.cs`

---

## Needs Testing

### Other Windows Versions and Screen Readers

Only tested on Windows 11 with NVDA. Other Windows versions (Windows 10) and other screen readers (JAWS, Narrator, etc.) may work via Tolk but are untested.

---

### Jump In: Packet Order Chaotic

The packet tiles in Jump In appear in a chaotic/unpredictable order during navigation. The navigation order does not match the visual layout consistently.

**Files:** `GeneralMenuNavigator.cs`, `EventAccessor.cs`

---

### Challenge Screen

The direct challenge screen is mostly functional. Working: screen detection, stepper controls with Left/Right, deck selection via folder navigation, main button with challenge status text, opponent join/leave polling, match countdown detection, icon-only enemy button labels (Kick/Block/Add Friend), locked spinner prefix when opponent is host, tournament parameter announcements.

Remaining issues:
- **Deck selection timing**: DeckSelectBlade opens when spinner values change; rescan timing may occasionally miss
- **Leave/Invite buttons**: Become INACTIVE when DeckSelectBlade opens (game hides container)

**Files:** `ChallengeNavigationHelper.cs`, `GeneralMenuNavigator.cs`

---

### NPE Deck Reward Screen

After completing all 5 NPE tutorial stages, the game shows a deck reward screen with deck boxes instead of individual cards. NPERewardNavigator was extended to detect deck prefabs (children with `Hitbox_LidOpen`) and navigate them. Needs testing:
- Does "Decks Unlocked, N decks" announce correctly?
- Can Left/Right navigate between deck boxes?
- Does Enter open a deck box (clicks `Hitbox_LidOpen`)?
- Does Backspace activate the Continue button (`NullClaimButton`)?
- Does `UITextExtractor.GetText()` extract deck names, or does it fall back to "Deck 1", "Deck 2", etc.?

## Not Reproducible Yet

### Game Assets Loading Problem

Intermittent issue during game asset loading. Exact symptoms and reproduction steps unknown.

---

### NPE Screen Present During Tutorial Battles

The NPE (tutorial) screen elements appear to remain present or interfere during tutorial duel scenes. Exact symptoms and reproduction steps unknown.

---

### Settings Menu While Declared Attackers

Opening the settings menu (F2) during the declare attackers phase causes issues. Exact symptoms and reproduction steps unknown.

---

### Adding Cards to Deck Exits Collection Group

Adding cards to a deck reportedly moves the user out of the Collection group to the upper group level. Exact reproduction steps unknown.

---

### New Deck Button Not Visible in Deck Builder

The "New Deck" button in the deck builder sometimes isn't visible if certain other groups (e.g., Progress group) were used before navigating to the deck builder. Exact reproduction steps unknown.

---

### Color Challenge Broken

Color Challenge mode may be broken. Exact symptoms and reproduction steps unknown.

---

## Technical Debt

### Code Archaeology

Accumulated defensive fallback code needs review:
- `ActivateBackButton()` has 4 activation methods - test which are needed
- `LogAvailableUIElements()` (~150 lines) only runs once per screen in DetectScreen (removed from PerformRescan)
- Extensive logging throughout - review what's still needed

**Priority:** Low

---

### Announcement Compaction for Zone Transitions

Zone change events can create redundant announcements when a creature dies and goes to graveyard:
- Current: "X died" followed by "X went to graveyard" (two announcements)
- Ideal: Single combined announcement

**Consideration:** Compact announcements when zone-leaving and zone-reaching events happen close together.

**Risk:** Events can occur between dying and zone change (e.g., death triggers, replacement effects). Compacting could cause missed information or incorrect announcements.

**Priority:** Low - current behavior is functional, just slightly verbose

---

## Potential Issues (Monitor)

### Wrong Card Played

Sometimes the mod plays a different card than the one announced/focused. Root cause unclear.

---

### Damage Assignment Browser Opens Twice

The damage assignment browser sometimes opens twice in sequence for the same attacker (same blockers, same TotalDamage). This causes the game to request two separate damage assignments. Observed with a 5/4 creature blocked by 4 creatures — no first strike on any attacker or blocker. A creature with first strike (Halana und Alena) was on the battlefield but not in combat. Unclear whether this is caused by:
- A first strike damage step being created by a non-combat creature with first strike
- A game-internal behavior we don't understand yet
- Something else on the battlefield granting first strike

Currently mitigated with "1 of N" announcement so the user knows multiple rounds are expected.

**Files:** `BrowserNavigator.cs` (GetAssignDamageEntryAnnouncement, EnsureTotalDamageCached)

---

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
- **Dropdowns:** Unity handles internal arrow navigation; Enter is blocked from game and handled by mod (silent selection via reflection, dropdown stays open); index re-synced on close

**Files:** `BaseNavigator.cs`, `UIFocusTracker.cs`, `KeyboardManagerPatch.cs`, `EventSystemPatch.cs`, `DropdownStateManager.cs`

## Planned Features

### Upcoming

1. Unplayable card detection - detect and announce when a card cannot be played (e.g. insufficient mana) instead of silently failing or entering a broken state
2. X spell support - spells with variable costs (e.g., Fireball, Walking Ballista) require the player to choose a value for X. Currently no accessible way to set the X value. Needs investigation into how the game presents the X cost input and how to make it navigable.
3. Manual trigger ordering - allow players to manually choose the order of their triggered abilities when multiple triggers happen simultaneously
4. Auto-skip tracking and hotkeys - correct tracking and switching of auto-skip state, including a new hotkey for toggling auto-skip and full auto-skip modes
5. Rapid navigation by holding navigation keys - allow continuous scrolling through elements when arrow keys or other navigation keys are held down
6. Extended tutorial for mod users - explain Space/Backspace behavior (confirm/cancel), the blocking system during combat, and I shortcut for extended card info and keyword descriptions
7. Better handling of number announcements while tabbing - possibly change how Tab changes focus to reduce noisy or redundant number readouts
8. Player username announcements
9. Settings menu improvements - better sorting of options and clearer display of checkmarks/toggle states
10. Mulligan overview announcement - announce hand summary when mulligan opens (e.g., card count, notable cards)
11. Better group announcements - improve how element groups are announced when entering/switching groups
12. Loading screen announcement cleanup - reduce repetitive announcements during loading screens
13. Better combat announcements when multiple attackers - clearer announcement when two or more enemies are attackable
14. Ctrl+key shortcuts for navigating opponent's cards - additional Ctrl-modified zone shortcuts for quick opponent board access
15. Card crafting - wildcard crafting workflow accessibility
16. Phase skip warning - warn when passing priority would skip a phase where the player could still play cards (e.g., skipping main phase with mana open)
17. Pass entire turn shortcut - quick shortcut to pass priority for the whole turn (may already exist as Shift+Enter in the game, just needs to be enabled/announced)
18. Vehicle power and toughness - announce power/toughness for vehicle cards when not crewed
19. Saga support - announce current chapter, total chapters, and chapter abilities for Saga enchantments
20. Verbose "Big Card" announcements (inspired by Hearthstone Access) - option to include card details inline with action announcements, with user preference toggle for brief vs verbose

### Low Priority / v1.1

1. Auto version checking and auto update - check for new mod versions on launch and optionally auto-update. May be too problematic to implement reliably.
2. Pack expansion selection - allow changing which expansion packs are purchased from in the store
3. Card flipping during pack opening - allow flipping/revealing individual cards during pack opening for a more interactive experience
4. Cube and other draft event accessibility - make Cube drafts and similar special draft events fully accessible (pick screens, pack navigation, deck building within event)
5. Cosmetic handling support - accessible navigation and selection for emotes, avatars, card sleeves, card styles, and companions
6. Achievement screen - accessible navigation and reading of achievement progress and rewards

