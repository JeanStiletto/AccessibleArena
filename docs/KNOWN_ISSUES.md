# Known Issues

Active bugs, limitations, and planned work for Accessible Arena.

 
## Active Bugs

### Sphere Buy Button Announced Twice

The buy button for Spheres (mastery/cosmetics store) is announced twice when focused. Likely a duplicate element being picked up by the scan.

---

### Steam Overlay Warning Shows With Overlay Disabled

The startup warning that flags the Steam overlay as conflicting with the mod's Shift+Tab navigation appears even when the user has actually disabled the overlay (right-click MTGA → Properties → uncheck "Enable Steam Overlay while in-game"). Detection currently triggers on Steam-launched runs regardless of the overlay's real state — it should check whether the overlay is actually enabled before warning.

**Files:** `SteamOverlayBlocker.cs`

---

### Priority Alarm Cue Sound Design Not Finalized

The repeating priority alarm (F2 → "Priority alarm sound", off by default) works correctly — it arms on meaningful priority, respects the grace period, and stops on the first key press. But the *sound itself* is a placeholder we're not happy with. It's a synthesized 440 Hz sine chime (`ToneCue`, single tone with raised-cosine attack, exponential decay, and cosine release). It needs a proper redesign to be pleasant and unobtrusive rather than beep-like — candidates worth exploring: a softer waveform or multi-partial timbre, a gentle two-note motif, or a short bundled sample.

Note: we deliberately do NOT reuse the game's own `sfx_ui_gain_priority` (Wwise event `sfx_priority_on`) — it posts without error but is silent in the shipped build, which is why the native priority cue is never heard.

**Files:** `Core/Utils/ToneCue.cs` (shape constants at the top), `Core/Services/PriorityAlarm.cs`

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

### Monitor How Stacks and Entries Shift With Change of Selection or Tapped State

When HotHighlightNavigator delegates to BattlefieldNavigator for battlefield cards (via `NavigateToSpecificCard`), the position announced ("X von Y") reflects the card's position within its BattlefieldNavigator row (e.g., PlayerLands), not its position in the HotHighlightNavigator's Tab order. This can cause confusing announcements — e.g., two consecutive Tab presses both announce "2 von 3" for different cards if one card was selected and the row re-scanned.

**Root cause:** Two navigation systems with different indices. HotHighlightNavigator owns the Tab order (sorted by ownership group), but BattlefieldNavigator announces the row position when focus delegates to it. After a card changes state (selected/deselected), the BattlefieldNavigator row may recount, shifting positions.

**Observed in:** Hastige Suche (Frantic Search) untap phase — selecting 3 lands from a mixed battlefield. Own cards correctly grouped before opponent cards, selection worked, but row position announcements were inconsistent.

**Partly addressed:** `BattlefieldNavigator` now anchors focus by card InstanceId (`_anchorId` / `RestoreAnchor`) instead of trusting the raw index across rebuilds, so a row rebuild no longer silently moves the user onto a different card. Rows are still rebuilt and re-sorted by `transform.position.x` on every refresh, so the *position number* a card is announced with can still change between presses — the anchor keeps you on the right card, it does not freeze the numbering.

**Monitor for:**
- Whether the remaining position-number churn is confusing on its own now that focus itself is stable
- Whether the index mismatch causes actual navigation confusion (wrong card activated) vs. just confusing announcements
- Effects with large numbers of selectable targets (5+) where the discrepancy becomes more noticeable
- Whether a unified position announcement (from HotHighlightNavigator's own index) would be clearer

**Also addressed:** `TryAdvanceToSameNameSibling` used to pick the first same-name entry that wasn't the just-clicked card, with no state check — so it could land the user back on a creature they had already declared as an attacker, where the next Enter un-declared it. (The v1.1 changelog described it as advancing to the next *unselected* copy; the code never had that filter.) It now compares state snapshots: pass 1 takes a copy still in the clicked card's pre-click state, pass 2 takes any copy not already in the post-click state, and if every copy is done focus stays put. Still gated behind the "Battlefield stacking" setting.

**Also addressed:** the per-card fallback behind Ctrl+Enter fired its clicks on a fixed 60 ms timer. Target selection is a GRE round trip — `SelectTargetsWorkflow.UpdateTarget` sets `_submitted`, `IsWaitingForRoundTrip` reports it, and `CanClick` returns false for as long as it is set — so clicks 2..N were refused and dropped. Observed with Caetus, Sea Tyrant of Segovia ("untap up to four creatures") on a stack of four Tentacles: all four clicks went out, the prompt then read "Confirm 1", and one Tentacle untapped. The sequence now waits for `IsWaitingForRoundTrip` to clear and asks the workflow's own `CanClick` before every dispatch, walks the stack oldest-first to match the game's `TryRerouteClick` → `OldestCard` reroute, and skips copies that already carry the picked indicator so it cannot un-pick a manual choice. Per-card wait 3 s, whole sequence 15 s.

**Untested in a real duel** — all of the above needs a longer play session before it can be called good.

**Files:** `HotHighlightNavigator.cs` (AnnounceCurrentItem — delegates to BattlefieldNavigator), `BattlefieldNavigator.cs` (RestoreAnchor, NavigateToSpecificCard, TryAdvanceToSameNameSibling, FindSibling, PumpStackClickSequence), `StackInteractionBridge.cs` (IsWaitingForRoundTrip, WorkflowAcceptsClick)

---

### SelectGroup Browser Pile Selection (Fact or Fiction)

In the SelectGroup browser (e.g. Curator of Destinies / Fact or Fiction pile selection), Enter and Space now activate the focused pile button. Previously, Enter activated a face-down card instead of the pile button, and Space fell through to PromptButton_Primary ("Opponent's Turn"), accidentally passing the turn.

**Fix applied:** Unified direct-choice early return in `ClickConfirmButton` handles SelectGroup, ChoiceList, and OptionalAction browsers identically — Space activates the focused button/card (same as Enter), or announces "No button selected" if nothing is focused. PromptButton fallbacks are excluded for all three browser types.

**Files:** `BrowserNavigator.cs` (ClickConfirmButton, ClickCancelButton, GetBrowserHintKey)

---

### Season Rewards Popup (Monthly Reset)

Reworked after a July 2026 user log showed the middle "rewards" phase reading as silent/unlabeled "Button" elements and the user getting stuck. Root cause: `_endOfSeasonDisplayState` read as `OldRankDisplay` for the entire sequence, so the rewards-reveal phase was routed through the rank-display path (which only reads a title) and never enumerated what was earned; the game also absorbs clicks during reveal animations (a "stuck counter"), so repeated presses appeared to do nothing.

Current behavior:
- Phase is derived from active display objects (`SeasonEndRankDisplay` active → rank phase; inactive within a season context → rewards reveal), NOT from `_endOfSeasonDisplayState`. See `DetermineSeasonPhase`.
- Rewards-reveal phase enumerates rewards via the standard path (`DiscoverRewardElements` → `DiscoverRewardsFromControllerData` fallback), with a guaranteed non-empty element so the screen always announces.
- Season input is a dedicated single-action flow (`HandleSeasonInput`): Enter/Space = advance (via `ClickBackgroundBlocker`, announced "Continuing"); Enter during an active reveal announces "Revealing, please wait" (`IsSeasonRevealBusy`); Backspace re-reads the current screen; Left/Right browse items. The junk coin-hitbox buttons are no longer added on season screens.
- Duplicate announcements suppressed by text (`_lastSeasonAnnouncement`), not element count.

Still to confirm at the next reset (monitor the log):
- Old rank, rewards, and new-placement phases each announce once with correct content.
- The rewards phase lists actual packs/cards earned (not just "Season Rewards").
- Advancing closes the final screen cleanly (no accidental matchmaking via "Play").
- The "Revealing, please wait" feedback fires instead of silent no-ops during animations.

**Testable:** next monthly season reset

**Files:** `RewardPopupNavigator.cs` (DetermineSeasonPhase, IsAnyRankDisplayActive, DiscoverSeasonRewardElements, HandleSeasonInput, AdvanceSeason, ClickBackgroundBlocker, IsSeasonRevealBusy, ForceRescan override)

---

## Needs Testing

### Other Windows Versions and Screen Readers

Tested on Windows 10 and Windows 11 with NVDA and JAWS. Speech goes through Prism, which also reaches Narrator/OneCore, UIA, ZDSR, PC-Talker, BoyPC Reader, Sense Reader, ZoomText and SAPI — those backends come from Prism and are untested here, so reports are welcome. Prism itself requires Windows 10 or later; older Windows versions are out of scope.

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
 
1. Display modified player properties (max hand size, extra turns, etc.)
2. Sylvan Library support — the card has a unique draw-then-choose UI that needs accessible navigation. Planned for when Strixhaven Remastered releases on Arena. Reference: https://magic.wizards.com/en/news/mtg-arena/dev-diary-sylvan-library

### Polish

1. Battlefield row categorization for land creatures — effects that turn lands into creatures (e.g. Nissa animating lands) cause them to appear in the Lands row (A/Shift+A) instead of the Creatures row (B/Shift+B). Conversely, effects that turn non-land permanents into lands (e.g. certain commander abilities) may miscategorize them. The categorization logic needs to handle cards with multiple types (Creature Land) more intelligently, potentially prioritizing the creature type for combat relevance.
2. Cube, Pick Two, Midweek Magic and paid events — implemented in v1.5; see `docs/investigations/unsupported-events.md` for the decompilation and `docs/EVENTS.md` for the resulting behaviour. Cube needed no separate code (it is an ordinary bot or human draft with a different pool). What remains untested by the maintainer, because it cannot be exercised without entering a paid event: the two-press entry-fee confirmation on gold and token entries, and the Arena Direct physical-prize popup guard. Both are built to fail safe — they only ever withhold a click — but a report from someone who has run an Arena Direct or Arena Open would turn "should work" into "known working".
3. Ctrl+key shortcuts for navigating opponent's cards — additional Ctrl-modified zone shortcuts for quick opponent board access. Highly speculative; unlikely to be implemented unless requested by users.
4. Confirm the Prism backends nobody here can test — the Tolk-to-Prism switch is done (v1.4.6), so ZDSR, PC-Talker, BoyPC Reader, Sense Reader, ZoomText, Narrator/OneCore and UIA are now reachable, but only NVDA and JAWS are tested by the maintainer. Reports from users of the other readers are what would turn "reachable" into "known working". The old .NET blocker is gone: the mod P/Invokes Prism's C ABI directly instead of using the official binding, which targets .NET 10. macOS remains out of reach, but the speech library is no longer the obstacle — the mod loader and the game client are, and confirming it would still need a contributor with a Mac.
5. Endure option dialogue must be improved — the Endure prompt (choose +1/+1 counters vs. token) needs clearer announcements and better keyboard flow so blind players can reliably pick the option they intend.
6. Confirmation guard for "cancel all blocks" — pressing Backspace during declare blockers to cancel all assigned blocks is easy to trigger accidentally and wipes the entire block assignment with no undo. Add a confirmation step (e.g. press twice, or announce a warning on first press) to prevent accidental skipping.

