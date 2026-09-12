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

## Monitoring

### SelectGroup Browser Pile Selection (Fact or Fiction)

In the SelectGroup browser (e.g. Curator of Destinies / Fact or Fiction pile selection), Enter and Space now activate the focused pile button. Previously, Enter activated a face-down card instead of the pile button, and Space fell through to PromptButton_Primary ("Opponent's Turn"), accidentally passing the turn.

**Fix applied:** Unified direct-choice early return in `ClickConfirmButton` handles SelectGroup, ChoiceList, and OptionalAction browsers identically — Space activates the focused button/card (same as Enter), or announces "No button selected" if nothing is focused. PromptButton fallbacks are excluded for all three browser types.

**Files:** `BrowserNavigator.cs` (ClickConfirmButton, ClickCancelButton, GetBrowserHintKey)

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
4. macOS and Linux support — the mod is Windows-only. Speech is no longer the obstacle (Prism is reachable from the mod); the mod loader and the game client are. Confirming anything on either platform needs a contributor with a Mac or a Linux box.
5. Endure option dialogue must be improved — the Endure prompt (choose +1/+1 counters vs. token) needs clearer announcements and better keyboard flow so blind players can reliably pick the option they intend.
6. Confirmation guard for "cancel all blocks" — pressing Backspace during declare blockers to cancel all assigned blocks is easy to trigger accidentally and wipes the entire block assignment with no undo. Add a confirmation step (e.g. press twice, or announce a warning on first press) to prevent accidental skipping.

