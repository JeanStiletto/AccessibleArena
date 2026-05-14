# Known Issues

Active bugs, limitations, and planned work for Accessible Arena.

 
## Active Bugs

### Phase Stop System Breaks During Mulligan

Phase stops (a game feature made accessible by the mod, toggled with keys 1-0) break if you try to use them while still in the mulligan phase. Once broken, they stay broken for the rest of the match. Workaround: wait until turn 1 begins before touching phase stop keys.

---

### Home/End Not Working in Mastery Navigator

Home and End keys do not jump to the first/last item in the Mastery Navigator. Other navigators handle them correctly — Mastery needs the same wiring.

---

### Sphere Buy Button Announced Twice

The buy button for Spheres (mastery/cosmetics store) is announced twice when focused. Likely a duplicate element being picked up by the scan.

---

### Steam Overlay Warning Shows With Overlay Disabled

The startup warning that flags the Steam overlay as conflicting with the mod's Shift+Tab navigation appears even when the user has actually disabled the overlay (right-click MTGA → Properties → uncheck "Enable Steam Overlay while in-game"). Detection currently triggers on Steam-launched runs regardless of the overlay's real state — it should check whether the overlay is actually enabled before warning.

**Files:** `SteamOverlayBlocker.cs`

---

## Game Behavior (Not Fixable by Mod)

### Steam Overlay Inaccessible to Screen Readers

The Steam overlay is not accessible to screen readers. This causes two problems for blind Steam users:

1. **Shift+Tab conflict:** Steam's overlay hotkey (Shift+Tab) intercepts the mod's backward navigation. Users must disable the overlay in Steam (right-click MTGA → Properties → uncheck "Enable Steam Overlay while in-game") or rebind the overlay hotkey in Steam settings.

2. **Real-money purchases require the overlay:** Buying gems, bundles, or other real-money items opens a payment dialog inside the Steam overlay. With the overlay disabled, the purchase silently hangs. With the overlay enabled, the dialog is inaccessible. Either way, blind users need OCR or sighted assistance to complete real-money purchases on Steam.

**Mitigations:** The mod warns on startup if the overlay is active (Shift+Tab conflict), announces that "Change payment method" is managed through Steam, and shows a Critical-priority warning before any real-money purchase explaining the overlay limitation.

**Files:** `SteamOverlayBlocker.cs`, `StoreNavigator.cs`, `AccessibleArenaMod.cs`

---

### Set Filter + Text Search Combination Returns Empty Results

Combining set filters (Advanced Filters) with a text search in the deck builder collection returns 0 results, even when matching cards exist. For example: searching "brand" finds Brandende Welle normally, but adding the Avatar set filter produces 0 results despite the card having the correct Avatar set code (TLA). Clearing the search shows Avatar cards; clearing the filter shows search results. The combination fails.

**Root cause:** The game's internal card pool filtering logic does not correctly intersect set filter and text search criteria. Debug logging confirmed Brandende Welle has `ExpansionCode='TLA'` — identical to other Avatar cards that appear when only the set filter is active.

**Why it can't be fixed by the mod:** The game returns an empty card pool to `CardPoolHolder` before the mod reads it. The mod accurately reports what the game provides (`Search rescan pool: 0 -> 0`).

**Workaround:** Use set filters or text search individually, not both at the same time.

**Investigation:** [docs/investigations/card-filter-search-bug.md](investigations/card-filter-search-bug.md)

---

## Under Investigation

### Codex "How to Play" Category Completeness

The "How to Play" category in the Codex of the Multiverse may be missing entries or have sections that don't read fully. Needs a pass to verify all subsections are reachable and read correctly.

---

## Monitoring

### Tab Navigation Index Mismatch During Battlefield Selection

When HotHighlightNavigator delegates to BattlefieldNavigator for battlefield cards (via `NavigateToSpecificCard`), the position announced ("X von Y") reflects the card's position within its BattlefieldNavigator row (e.g., PlayerLands), not its position in the HotHighlightNavigator's Tab order. This can cause confusing announcements — e.g., two consecutive Tab presses both announce "2 von 3" for different cards if one card was selected and the row re-scanned.

**Root cause:** Two navigation systems with different indices. HotHighlightNavigator owns the Tab order (sorted by ownership group), but BattlefieldNavigator announces the row position when focus delegates to it. After a card changes state (selected/deselected), the BattlefieldNavigator row may recount, shifting positions.

**Observed in:** Hastige Suche (Frantic Search) untap phase — selecting 3 lands from a mixed battlefield. Own cards correctly grouped before opponent cards, selection worked, but row position announcements were inconsistent.

**Monitor for:**
- Whether the index mismatch causes actual navigation confusion (wrong card activated) vs. just confusing announcements
- Effects with large numbers of selectable targets (5+) where the discrepancy becomes more noticeable
- Whether a unified position announcement (from HotHighlightNavigator's own index) would be clearer

**Files:** `HotHighlightNavigator.cs` (AnnounceCurrentItem — delegates to BattlefieldNavigator), `BattlefieldNavigator.cs` (NavigateToSpecificCard)

---

### SelectGroup Browser Pile Selection (Fact or Fiction)

In the SelectGroup browser (e.g. Curator of Destinies / Fact or Fiction pile selection), Enter and Space now activate the focused pile button. Previously, Enter activated a face-down card instead of the pile button, and Space fell through to PromptButton_Primary ("Opponent's Turn"), accidentally passing the turn.

**Fix applied:** Unified direct-choice early return in `ClickConfirmButton` handles SelectGroup, ChoiceList, and OptionalAction browsers identically — Space activates the focused button/card (same as Enter), or announces "No button selected" if nothing is focused. PromptButton fallbacks are excluded for all three browser types.

**Files:** `BrowserNavigator.cs` (ClickConfirmButton, ClickCancelButton, GetBrowserHintKey)

---

### SelectCards Browser Confirm with 2-Button Layout

SelectCards browsers that require explicit confirmation (e.g. choosing which counterspell to cast from 4 options) may use a `2Button_Left`/`2Button_Right` scaffold layout instead of `SubmitButton`/`SingleButton`. The 2-button names don't match `ConfirmPatterns`. A workflow reflection fallback was added to handle this by submitting via `WorkflowController.CurrentInteraction` — monitor whether this reliably confirms in all SelectCards scenarios or causes issues in other scaffold types.

**Observed in:** Casting Zauberschlinge (Spell Snare) with 4 valid counterspell targets on the stack. The scaffold used 2-button layout, ConfirmPatterns failed, and Space fell through to PromptButton_Primary ("Zug des Gegners") which did nothing.

**Fix applied:** `ClickConfirmButton` now tries `TrySubmitWorkflowViaReflection()` after ConfirmPatterns fail but before the PromptButton_Primary fallback.

**Files:** `BrowserNavigator.cs` (ClickConfirmButton), `BrowserDetector.cs` (ScanForBrowser priority order)

---

### RepeatSelection Modal Spell Remaining Choices Announcement

Fixed `ExtractBrowserHeaderText()` to read the subheader from the `BrowserHeader` component via reflection instead of searching by GO name (which never matched). Added dedicated `AnnounceRepeatSelectionAfterDelay()` that announces "selected/deselected" plus the remaining count after each mode selection. Monitor whether:
- The initial entry announcement now includes the remaining count (e.g., "Modus wählen. 3 Modi. 5 verbleibende Optionen")
- Each selection announces remaining (e.g., "ausgewählt. 4 verbleibende Optionen")
- Deselecting a selected copy announces correctly
- Auto-submit after reaching max selections doesn't cause issues

**Observed in:** Zeit des Webens (choose modes up to 5 times). Previously no remaining count was spoken.

**Files:** `BrowserNavigator.cs` (ExtractBrowserHeaderText, AnnounceRepeatSelectionAfterDelay, ActivateCurrentCard)

---

### Season Rewards Popup (Monthly Reset)

Season end rewards popup now uses content-gated detection (NPE-style): the navigator stays inactive until actual content is loaded, and activates once with a clean announcement. Season rank display phases (old rank, new rank) extract title, subtitle, and per-format rank details from `SeasonEndRankDisplay` components. ForceRescan suppresses duplicate announcements by tracking element count. Monitor whether:
- Old rank phase announces correctly (e.g., "Season Rankings. Final Results. Constructed: Gold Tier 2. Limited: Silver Tier 4")
- Rewards phase activates only after reward prefabs appear (no repeated "Rewards." during loading)
- New rank phase announces the new season name and placement ranks
- Transitions between phases work smoothly (navigator deactivates during empty transitions, reactivates for next phase)
- Enter/Backspace still advance through phases via the game's click blocker
- Pack fallback label includes quantity (e.g., "Booster Pack x3") when set name data is unavailable

**Testable:** May 2025 (next monthly season reset)

**Files:** `RewardPopupNavigator.cs` (CheckRewardsPopupOpenInternal, GetSeasonEndState, HasActiveSeasonDisplay, ExtractSeasonRankText, DiscoverSeasonRankElements, ForceRescan override)

---

## Needs Testing

### Other Windows Versions and Screen Readers

Tested on Windows 10 and Windows 11 with NVDA and JAWS. Other Windows versions and other screen readers (Narrator, etc.) may work via Tolk but are untested.

---

### Emblems in Command Zone

Emblems created by planeswalkers live in the Command zone (`ZoneType.Command`) as `GameObjectType.Emblem`. It is unclear whether the current W key (Command Zone navigator) already picks them up, or if additional detection is needed. Needs in-game testing with emblem-producing planeswalkers.

---

## Not Reproducible Yet

### Event-Specific Quests Show English Text

Event-specific quests (e.g. special event objectives) reportedly display English text instead of the user's localized language. Standard daily/weekly quests are localized correctly. Exact reproduction steps unknown — needs a specific event with localized objectives to confirm.

---

### Game Assets Loading Problem

Intermittent issue during game asset loading. Exact symptoms and reproduction steps unknown.

---


## Planned Features

### Upcoming
 
1. Display emblems in command zone and modified player properties (max hand size, extra turns, etc.)
2. Sylvan Library support — the card has a unique draw-then-choose UI that needs accessible navigation. Planned for when Strixhaven Remastered releases on Arena. Reference: https://magic.wizards.com/en/news/mtg-arena/dev-diary-sylvan-library

### Polish

1. Unify E and T shortcut announcements — make the announcement style consistent between the two shortcuts.
2. Battlefield row categorization for land creatures — effects that turn lands into creatures (e.g. Nissa animating lands) cause them to appear in the Lands row (A/Shift+A) instead of the Creatures row (B/Shift+B). Conversely, effects that turn non-land permanents into lands (e.g. certain commander abilities) may miscategorize them. The categorization logic needs to handle cards with multiple types (Creature Land) more intelligently, potentially prioritizing the creature type for combat relevance.
3. Make extended card menu accessible in deck screens — the right-click/long-press context menu on cards (craft, add to deck, view details, etc.) is currently not accessible via keyboard or screen reader.
4. Cube and other draft event accessibility — make Cube drafts and similar special draft events fully accessible (pick screens, pack navigation, deck building within event).
5. Ctrl+key shortcuts for navigating opponent's cards — additional Ctrl-modified zone shortcuts for quick opponent board access. Highly speculative; unlikely to be implemented unless requested by users.
6. Replace Tolk with Prism library — Tolk covers the major Western screen readers (NVDA, JAWS, Narrator) but lacks support for several Asian ones. A switch to Prism may be considered if Asian screen reader users request it. Two blockers remain: the official Prism .NET binding currently targets .NET 10, while this mod runs on .NET Framework 4.7.2; and the mod would need to be confirmed portable to macOS, which requires a contributor with access to a Mac (the maintainer does not have one).
7. Commander display improvements — properly announce commanders in Brawl/Commander: show mana cost, display commander tax on the commander card (not just on cast), handle partner commanders correctly. PR #76 has initial work on cast-time tax announcements but needs a broader approach for on-demand cost checking.
8. Endure option dialogue must be improved — the Endure prompt (choose +1/+1 counters vs. token) needs clearer announcements and better keyboard flow so blind players can reliably pick the option they intend.
9. Confirmation guard for "cancel all blocks" — pressing Backspace during declare blockers to cancel all assigned blocks is easy to trigger accidentally and wipes the entire block assignment with no undo. Add a confirmation step (e.g. press twice, or announce a warning on first press) to prevent accidental skipping.

