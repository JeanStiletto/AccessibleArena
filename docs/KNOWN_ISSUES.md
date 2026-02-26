# Known Issues

Active bugs, limitations, and planned work for Accessible Arena.

For resolved issues and investigation history, see docs/old/RESOLVED_ISSUES.md.

## Active Bugs

### Space Key Pass Priority

Game's native Space keybinding doesn't work reliably after using mod navigation. HotHighlightNavigator now clicks the primary button directly as workaround.

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

### Friends Panel "add friend" / "add challenge" Buttons Show English Labels

The "add friend" and "add challenge" buttons in the FriendsWidget show English GameObject names instead of localized text. Our text extraction falls back to the GameObject name (`Button_AddFriend`, `Button_AddChallenge`) when it can't find the localized TextMeshProUGUI label. The actual localized text likely lives on a child object or uses a different text component that we miss.

**Path:** `FriendsWidget_Desktop_16x9(Clone)/FriendWidget_Base/Button_AddFriend/Backer_Hitbox`

---

### Haide Land Browser Broken (Leicht Gepanzert Deck)

The Haide land (from the "Leicht Gepanzert" Brawl deck) opens a color picker browser to choose a mana color except white. This browser is not handled correctly and breaks navigation.

---

### Color Challenge Deck Name Not Refreshing

When selecting a different color in Color Challenge, the announced deck name does not update to reflect the newly selected color's deck.

---

### Deck Renaming Causes Bad Mod State

Renaming a deck breaks the mod's navigation state. After renaming, the mod may lose track of elements, fail to announce correctly, or require a screen transition to recover.

---

## Needs Testing

### Other Windows Versions and Screen Readers

Only tested on Windows 11 with NVDA. Other Windows versions (Windows 10) and other screen readers (JAWS, Narrator, etc.) may work via Tolk but are untested.

---

### PlayBlade Queue Type Selection

The PlayBlade "Find Match" was restructured into three queue type tabs (Ranked, Open Play, Brawl) at the top tab level. Several aspects need further testing:

**Mode selection correctness:**
- Unclear if selecting a queue type tab (e.g., "Ranked") always correctly sets the game's internal mode
- The two-step activation (click FindMatch tab -> click queue type tab) relies on timing and rescans
- Edge case: switching between queue types rapidly may leave the game in an unexpected mode state

**BO3 toggle:**
- The "Best of 3" checkbox is now labeled correctly (was "POSITION" placeholder)
- Needs testing whether toggling it actually changes the match format

**Files:** `PlayBladeNavigationHelper.cs`, `GroupedNavigator.cs`, `ElementGroupAssigner.cs`, `GeneralMenuNavigator.cs`

---

### Unwanted Secondary Buttons in Tab Order

Sometimes secondary or irrelevant buttons appear in the Tab navigation order during duels. These buttons should be filtered out but occasionally slip through.

---

### Damage Assignment Browser Opens Twice

The damage assignment browser sometimes opens twice in sequence for the same attacker (same blockers, same TotalDamage). This causes the game to request two separate damage assignments. Observed with a 5/4 creature blocked by 4 creatures — no first strike on any attacker or blocker. A creature with first strike (Halana und Alena) was on the battlefield but not in combat. Unclear whether this is caused by:
- A first strike damage step being created by a non-combat creature with first strike
- A game-internal behavior we don't understand yet
- Something else on the battlefield granting first strike

Currently mitigated with "1 of N" announcement so the user knows multiple rounds are expected.

**Files:** `BrowserNavigator.cs` (GetAssignDamageEntryAnnouncement, EnsureTotalDamageCached)

---

### Damage Assignment Submit via SimulatePointerClick

The damage assignment browser submit currently uses direct DoneAction invocation via reflection. Before our AssignDamage accessibility changes, the generic SimulatePointerClick on SubmitButton worked for confirmation. It's unclear whether SimulatePointerClick still works or if our input routing changes broke it. If DoneAction ever fails, test whether reverting to SimulatePointerClick on the SubmitButton is a viable alternative.

**Files:** `BrowserNavigator.cs` (SubmitAssignDamage method)

---

### London Mulligan With Very Low Card Counts

Mulliganing down to 3 or fewer cards may behave incorrectly. Needs testing whether card selection and bottom placement still works correctly at very low hand sizes.

---

### Sideboard Cards in Draft/Sealed Deck Building

Pool cards are now always classified as "Collection" (DeckBuilderCollection). Actual sideboard cards (non-MainDeck holders inside MetaCardHolders_Container) are detected separately. This works correctly for normal deck building, but in draft/sealed the pool cards may conceptually be the sideboard. Needs testing whether draft/sealed sideboard cards are still properly detected and navigable.

---

### Jump In: Featured Card Not Read Correctly

Each packet in Jump In displays a featured card (via `PacketDetails.LandGrpId`). The mod attempts to look up card info via `CardModelProvider.GetCardInfoFromGrpId()` but this fails in the packet selection context (`_cachedDeckHolder is null`, no localization method found). Only the packet name and colors are shown in the info blocks, not the featured card data.

Additionally, the featured card display does not update when selecting a different packet - it continues showing the same card data.

**Files:** `EventAccessor.cs` (GetPacketInfoBlocks, GetPacketLandGrpId), `CardModelProvider.cs`

---

### Jump In: Packet Order Chaotic

The packet tiles in Jump In appear in a chaotic/unpredictable order during navigation. The navigation order does not match the visual layout consistently.

**Files:** `GeneralMenuNavigator.cs`, `EventAccessor.cs`

---

### Event Start Tooltip Missing

When starting an event (entering the event page), the game shows a tooltip with event information. This tooltip is not detected or announced by the mod.

---

### Challenge Screen (Work In Progress)

The direct challenge screen (via friends panel "Challenge" button) is partially implemented. What works:

- **Screen detection**: PlayBladeState "Challenge" recognized as state 2 (DirectChallenge), screen announced as "Direct Challenge"
- **Stepper controls**: Popout options (Deck Type, Format, Coin Flip, Match Type) work as steppers with Left/Right arrows via Spinner_OptionSelector reflection
- **Overlay filtering**: Challenge containers (ChallengeOptions, UnifiedChallenges, Popout_Play, FriendChallengeBladeWidget) pass the PlayBlade overlay filter
- **Group assignment**: Challenge elements correctly classified as PlayBladeContent (bypassing Popup and FriendsPanel group conflicts via IsInsidePlayBlade guards)
- **Grouped navigation**: Enabled with PlayBladeContent auto-entry and position restore after spinner rescan

Known issues still to address:
- **Deck selection**: DeckSelectBlade opens when spinner values change, adding ~120 deck entries. Deck pairing (UI/TextBox dedup) and folder grouping should work via existing PlayBlade patterns but needs testing
- **Stepper carousel in grouped mode**: HandleCarouselArrow syncs _currentIndex with GroupedNavigator.CurrentElement, but this is new code and needs verification
- **Auto-entry after rescan**: RequestPlayBladeContentEntryAtIndex restores user position after spinner rescan, bypassing the SaveCurrentGroupForRestore mechanism which is skipped in PlayBlade context. Needs testing
- **Leave/Invite buttons**: MainButton_Leave and Invite Button become INACTIVE when DeckSelectBlade opens (game hides UnifiedChallengesCONTAINER). Alternative: UnifiedChallenge_MainButton ("Warten") and NoDeck ("Select Deck") stay active in Popout_Play/FriendChallengeBladeWidget

**Files:** `HarmonyPanelDetector.cs`, `OverlayDetector.cs`, `ElementGroupAssigner.cs`, `GroupedNavigator.cs`, `GeneralMenuNavigator.cs`, `BaseNavigator.cs`, `UIElementClassifier.cs`, `UIActivator.cs`

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

### Card Names in English Despite Non-English Game Language

Reported by some users: card names are read in English while the rest of the card text (rules text, type line) and the game UI are in the correct non-English language. Could not reproduce so far. May be related to game client language settings, account region, or a specific card data loading order.

---

### Game Assets Loading Problem

Intermittent issue during game asset loading. Exact symptoms and reproduction steps unknown.

---

### NPE Screen Present During Tutorial Battles

The NPE (tutorial) screen elements appear to remain present or interfere during tutorial duel scenes. Exact symptoms and reproduction steps unknown.

---

### Settings Menu While Declared Attackers

Opening the settings menu (F2) during the declare attackers phase causes issues. Exact symptoms and reproduction steps unknown.

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

### Vault Progress Objects in Packs

Pack opening sometimes shows multiple identical "Alchemy Bonus Vault Progress +99" items alongside actual cards (e.g., 6 cards + 3 vault progress = 9 items). This appears to be game behavior, not a mod bug.

---

### NPE Overlay Exclusion for Objective_NPE Elements

Changed `ElementGroupAssigner.DetermineOverlayGroup()` to exclude `Objective_NPE` elements from NPE overlay classification. This allows SparkRank (Objective_NPEQuest) to be grouped with other Objectives instead of being treated as an NPE tutorial overlay element.

**Monitor for:** This might break NPE tutorial screens if any tutorial elements have "Objective_NPE" in their path.

**Files:** `ElementGroupAssigner.cs`

---

### Targeting Spells With Non-Battlefield Objects in Highlight List

Monitor whether clicking activates the wrong target during duels, especially when targeting spells while non-battlefield objects (e.g., UI buttons, zone elements) are present in the highlight list.

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

1. Stepper control redesign
2. Unplayable card detection - detect and announce when a card cannot be played (e.g. insufficient mana) instead of silently failing or entering a broken state
3. X spell support - spells with variable costs (e.g., Fireball, Walking Ballista) require the player to choose a value for X. Currently no accessible way to set the X value. Needs investigation into how the game presents the X cost input and how to make it navigable.

---

### Upcoming

1. Manual trigger ordering - allow players to manually choose the order of their triggered abilities when multiple triggers happen simultaneously
2. Auto-skip tracking and hotkeys - correct tracking and switching of auto-skip state, including a new hotkey for toggling auto-skip and full auto-skip modes
4. First letter navigation - press a letter key to jump to the next element starting with that letter in menus and lists
5. Rapid navigation by holding navigation keys - allow continuous scrolling through elements when arrow keys or other navigation keys are held down
6. Extended tutorial for mod users - explain Space/Backspace behavior (confirm/cancel), the blocking system during combat, and I shortcut for extended card info and keyword descriptions
7. Better handling of number announcements while tabbing - possibly change how Tab changes focus to reduce noisy or redundant number readouts
8. Creature death/exile/graveyard announcements with card names
9. Player username announcements
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
22. Mana color picker confirmation step - add an artificial confirmation step (Tab to navigate, Enter to stage, Space to confirm) for consistency with browser/selection patterns. Currently each Enter immediately submits the color choice.

### Low Priority / v1.1

1. Auto version checking and auto update - check for new mod versions on launch and optionally auto-update. May be too problematic to implement reliably.
2. Pack expansion selection - allow changing which expansion packs are purchased from in the store

### Future

1. Single-key info shortcuts (inspired by Hearthstone Access)
   - Quick status queries without navigation
   - Benefits: Faster information access, less navigation needed

   **Priority shortcuts to implement:**

   **K - Keyword Explanation**
   - When focused on a card, K announces keyword abilities with definitions
   - Example: "Flying. This creature can only be blocked by creatures with flying or reach."
   - Requires: Keyword detection from card rules text + keyword definition database

   **O - Game Log (Play History)**
   - O: Announce recent game events (last 5-10 actions)
   - Example: "Opponent played Mountain. You drew Lightning Bolt. Opponent attacked with Goblin."
   - Requires: Tracking game events in DuelAnnouncer and storing history

4. Verbose "Big Card" announcements (inspired by Hearthstone Access)
   - Option to include card details inline with action announcements
   - User preference toggle: brief vs verbose announcements
