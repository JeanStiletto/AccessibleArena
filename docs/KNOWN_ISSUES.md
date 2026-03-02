# Known Issues

Active bugs, limitations, and planned work for Accessible Arena.

For resolved issues and investigation history, see docs/old/RESOLVED_ISSUES.md.

## Active Bugs

### Resolution Dropdown Shows Native Display Resolution Until Changed (Game Bug)

The resolution dropdown in Settings > Graphics always shows the native display resolution (e.g., "2880x1800") instead of the game's actual render resolution (e.g., "1920x1080") until the user interacts with it and selects a value.

**Root cause:** The game initializes the dropdown with `value=0` (first option = highest resolution) and never updates `m_Value` or `captionText` to reflect the actual internal render resolution. When the dropdown opens, the game internally corrects the focused item (scrolls to the correct option), but this correction is never synced back to the closed dropdown's display.

**Why it can't be fixed by the mod:** All Unity APIs (`Screen.width`, `Screen.height`, `Screen.currentResolution`, `Camera.main.pixelWidth/Height`) return the native display resolution in borderless fullscreen mode. The game's internal render resolution is stored in a game-specific setting not exposed to standard Unity APIs.

**Impact:** Only affects the first announcement of the resolution dropdown before user interaction. All other dropdowns (language, display mode, etc.) work correctly.

**Files:** `UIElementClassifier.cs` (CorrectStaleDropdownValue — attempts Screen.width correction but cannot fix borderless fullscreen case)

---

### Spell Resolved Announcement Too Early or Repeated

"Spell resolved" announcement sometimes fires too early or multiple times for a single spell.

---

### Card Abilities With High IDs Not Resolving

Some cards have ability IDs greater than ~100000 that cannot be resolved correctly. These abilities fail to display proper names or descriptions.

---

### Token Attack Selection Uses Game's Internal Order (Not a Mod Bug)

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

### Battlefield Cards Splitting Into Two Stacks

Cards on the battlefield sometimes split into two separate stacks/rows when they should be grouped together.

---

### Bot Match Not Working From Recent Played

Starting a bot match from the "Recent Played" section does not work properly.

---

### Color Challenge Deck Name Not Refreshing

When selecting a different color in Color Challenge, the announced deck name does not update to reflect the newly selected color's deck.

---

### ~~Deck Renaming Causes Bad Mod State~~ (Fixed v0.7.4)

Fixed: Popup cancel button matching now uses word-boundary detection (`ContainsWord`) and includes "back" pattern, so DeckDetailsPopup closes correctly after editing deck name.

---

### Weekly Progress Enter Locks Mod

Pressing Enter on the Weekly Progress element puts the mod in a locked state requiring screen transition to recover.

---

### Some Progress Items Open Buggy Screens

Pressing Enter on specific Progress items for certain modes opens the corresponding screen but may create buggy navigation states.

---

### Stack Abilities Missing Rules Text

Sometimes abilities on the stack only show the Name and Type blocks but not the rules text block.

---

---

## Under Investigation

### Craft Confirmation Popup - Incomplete, Needs Visual Verification

**Status:** Initial implementation exists but is not working correctly. Paused pending visual confirmation of game behavior.

**Goal:** Show a confirmation popup before spending a wildcard to craft a card.

**Current implementation:**
- `CraftConfirmationPopup.cs`: Custom Unity UI popup with body text, OK, and Cancel buttons
- `BaseNavigator.cs`: Virtual hook `OnCollectionCardActivating()` called before collection card activation
- `GeneralMenuNavigator.cs`: Overrides hook, shows popup, defers activation until user confirms
- Localized strings in all `lang/*.json` files

**Problem 1 - Craft toggle is not a crafting mode:**
The `filterButton_Craft` toggle only filters the collection view (showing craftable cards). It does NOT control whether clicking a card crafts it. Currently the popup only appears when the craft toggle is ON, but crafting happens on ANY collection card click regardless of toggle state. The check needs to be removed or replaced.

**Problem 2 - Game's CardViewerPopup appears after crafting:**
After our confirmation and `UIActivator.Activate()`, the game opens its own `CardViewerPopup_Desktop_16x9(Clone)` (detected as Popup by PanelStateManager). This popup shows the card, resources (Gold, Gems), craft pips, wildcard count, and a "Herstellen" (Craft) button. PopupHandler picks it up and announces items like "Gold: 17,925", which is confusing. This popup needs to be auto-dismissed.

**Problem 3 - Unclear if crafting is caused by our activation or game behavior:**
Our activation path for collection cards:
1. `UIActivator.TryActivateCollectionCard()` tries `OnAddClicked` property (Strategy 1) - returns null
2. Falls through to `IPointerClickHandler.OnPointerClick` (Strategy 3) - this is what fires
3. Single activation, no double-activation bug confirmed

After `OnPointerClick`, the game both crafts the card AND opens the CardViewerPopup. Wildcard count drops immediately (observed: 35 -> 34 -> 33 in log). It's unclear whether:
- The game's `OnPointerClick` always crafts (even for owned cards with spare copies), OR
- Our `OnPointerClick` invocation behaves differently than a real mouse click, OR
- The game has a separate confirmation flow that we're bypassing

**Log evidence (from MelonLoader/Latest.log):**
- Card "Fluchtroute" had Owned: 1, InDeck: 0 (spare copy available), craft toggle OFF
- After `OnPointerClick`: Owned became 2, wildcard dropped 35 -> 34
- Game's `CardViewerPopup` opened showing "x35" wildcard count (before dismiss), but rescan after dismiss showed 34
- Inside the popup: Gold, Gems, craft pips, wildcard count, "Herstellen" button are all discovered as navigable items

**Next steps (after visual confirmation):**
1. Determine if OnPointerClick is the correct activation method or if we should use a different API
2. Remove `IsCraftModeActive()` check - intercept ALL collection card activations instead
3. Detect whether a card actually needs crafting (compare OwnedCount vs available copies)
4. Auto-dismiss the game's `CardViewerPopup` after craft confirmation succeeds
5. Fix CardInfoNavigator not deactivating when popup opens (fix already committed but untested)

**Files:** `CraftConfirmationPopup.cs`, `BaseNavigator.cs`, `GeneralMenuNavigator.cs`, `Strings.cs`, `UIActivator.cs`

---

## Needs Testing

### Other Windows Versions and Screen Readers

Only tested on Windows 11 with NVDA. Other Windows versions (Windows 10) and other screen readers (JAWS, Narrator, etc.) may work via Tolk but are untested.

---

### Damage Assignment Browser Opens Twice

The damage assignment browser sometimes opens twice in sequence for the same attacker (same blockers, same TotalDamage). This causes the game to request two separate damage assignments. Observed with a 5/4 creature blocked by 4 creatures — no first strike on any attacker or blocker. A creature with first strike (Halana und Alena) was on the battlefield but not in combat. Unclear whether this is caused by:
- A first strike damage step being created by a non-combat creature with first strike
- A game-internal behavior we don't understand yet
- Something else on the battlefield granting first strike

Currently mitigated with "1 of N" announcement so the user knows multiple rounds are expected.

**Files:** `BrowserNavigator.cs` (GetAssignDamageEntryAnnouncement, EnsureTotalDamageCached)

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

### ~~Zones Not Updating When Cards Enter or Leave~~ (Fixed v0.6.9)

Zone card lists sometimes don't refresh when a card enters or leaves a zone (e.g., playing a card from hand, a creature dying to graveyard). The zone still shows the old card list until manually re-entered.

**Fix:** Event-driven dirty flag. DuelAnnouncer marks ZoneNavigator and BattlefieldNavigator dirty on zone count changes. Next card navigation input refreshes the active zone before navigating.

---

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

### Immediate

1. Unplayable card detection - detect and announce when a card cannot be played (e.g. insufficient mana) instead of silently failing or entering a broken state
3. X spell support - spells with variable costs (e.g., Fireball, Walking Ballista) require the player to choose a value for X. Currently no accessible way to set the X value. Needs investigation into how the game presents the X cost input and how to make it navigable.

---

### Upcoming

1. Manual trigger ordering - allow players to manually choose the order of their triggered abilities when multiple triggers happen simultaneously
2. Auto-skip tracking and hotkeys - correct tracking and switching of auto-skip state, including a new hotkey for toggling auto-skip and full auto-skip modes
4. First letter navigation - press a letter key to jump to the next element starting with that letter in menus and lists
5. Rapid navigation by holding navigation keys - allow continuous scrolling through elements when arrow keys or other navigation keys are held down
6. Extended tutorial for mod users - explain Space/Backspace behavior (confirm/cancel), the blocking system during combat, and I shortcut for extended card info and keyword descriptions
7. Better handling of number announcements while tabbing - possibly change how Tab changes focus to reduce noisy or redundant number readouts
8. Player username announcements
10. Game wins display (WinPips)
11. Token state on cards - announce token/copy status when reading card info
14. Settings menu improvements - better sorting of options and clearer display of checkmarks/toggle states
16. Browser announcements - shorter, less verbose; only announce when it is the player's browser (not opponent's)
17. Mulligan overview announcement - announce hand summary when mulligan opens (e.g., card count, notable cards)
18. Better group announcements - improve how element groups are announced when entering/switching groups
19. Loading screen announcement cleanup - reduce repetitive announcements during loading screens
20. Better combat announcements when multiple attackers - clearer announcement when two or more enemies are attackable
21. K hotkey for mark/counter information on cards - announce +1/+1 counters, damage marks, and other markers
22. Ctrl+key shortcuts for navigating opponent's cards - additional Ctrl-modified zone shortcuts for quick opponent board access
23. Card crafting - wildcard crafting workflow accessibility
24. Planeswalker support - loyalty abilities, activation, and loyalty counter announcements
25. Phase skip warning - warn when passing priority would skip a phase where the player could still play cards (e.g., skipping main phase with mana open)
26. Pass entire turn shortcut - quick shortcut to pass priority for the whole turn (may already exist as Shift+Enter in the game, just needs to be enabled/announced)
27. Hotkey to jump to attached card - when focused on an aura/equipment, press a key to navigate directly to the card it's attached to (and vice versa)
28. Vehicle power and toughness - announce power/toughness for vehicle cards when not crewed
29. Saga support - announce current chapter, total chapters, and chapter abilities for Saga enchantments

### Low Priority / v1.1

1. Auto version checking and auto update - check for new mod versions on launch and optionally auto-update. May be too problematic to implement reliably.
2. Pack expansion selection - allow changing which expansion packs are purchased from in the store
3. Card flipping during pack opening - allow flipping/revealing individual cards during pack opening for a more interactive experience
4. Cube and other draft event accessibility - make Cube drafts and similar special draft events fully accessible (pick screens, pack navigation, deck building within event)

### Future

1. Verbose "Big Card" announcements (inspired by Hearthstone Access)
   - Option to include card details inline with action announcements
   - User preference toggle: brief vs verbose announcements
