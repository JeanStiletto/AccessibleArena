# Known Issues

Active bugs, limitations, and planned work for Accessible Arena.

 
## Active Bugs

### Phase Stop System Breaks During Mulligan

Phase stops (a game feature made accessible by the mod, toggled with keys 1-0) break if you try to use them while still in the mulligan phase. Once broken, they stay broken for the rest of the match. Workaround: wait until turn 1 begins before touching phase stop keys.

---

### First Card Not Recognized After Filtering in Deck Builder

After a filter is applied in the deck builder (collection or deck side), the first card in the results is not correctly recognized as a card. This causes issues when adding cards — the mod may not detect the card properly, leading to wrong card additions or focus glitches.

---

### Home/End Not Working in Mastery Navigator

Home and End keys do not jump to the first/last item in the Mastery Navigator. Other navigators handle them correctly — Mastery needs the same wiring.

---

### Sphere Buy Button Announced Twice

The buy button for Spheres (mastery/cosmetics store) is announced twice when focused. Likely a duplicate element being picked up by the scan.

---

### Room Cards: Per-Side Mana Costs Not Announced Correctly

Room cards (Duskmourn) have two independently unlockable sides, each with its own mana cost. The mod currently does not display the per-side mana costs correctly — both sides need to be announced with their individual costs so the player can decide which door to unlock.

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

### Battlefield Stacking (new in 1.0.1)

Optional setting (F2 → "Battlefield stacking") that groups identical battlefield cards into one navigable entry, announced as "N Cardname" (e.g. "5 Tentakel"). Matches the game's own visual stacking: the mod reads each `UniversalBattlefieldStack` / `BattlefieldLayout` region via reflection and filters stacked-behind copies out of the flat row list. Stacks carrying attachments or exile (`HasAttachmentOrExile`) stay expanded so attachment text still lines up.

Selection-aware behaviors layered on top:

- When a stack is split by targeting (e.g. 1 of 2 Kraken selected for an untap effect), the `IsSame` comparator diverges on `SelectedBy`, so the game splits it into two 1-stacks. The mod detects the state change, announces it, then auto-advances focus to the next same-name sibling so repeated Enter targets the next copy instead of toggling the just-clicked card back off.
- Navigation announcements include selection state (e.g. "Krake, getappt, ausgewählt, 1 von 2") so a user can tell the selected copy from the unselected one.
- Land Summary (M / Shift+M) counts the real physical card count, not collapsed entries.

**Monitor for:**
- Whether the position count ("X von Y") makes sense when stacks split mid-action (e.g. clicking one of 4 creatures to block should leave "3 remaining" as the next audible step; the current auto-advance announces the shrunk stack but with a fresh position count)
- Whether non-creature token stacks (Treasure, Food, Clue, Blood) group cleanly in PlayerNonCreatures — investigation predicted fine, needs real match exposure
- Whether `StackParent` reassignments (game's `Sort()` can promote a different CDC to parent after attachments drop) cause stale focus after re-scans
- HotHighlightNavigator's Tab order interacting with collapsed stacks — when only some members of a stack are valid targets, `SelectedBy` already forces a split, but edge cases around attachments + multi-target may still surface expansion the mod hasn't accounted for
- Stacking of lands with tapped/untapped mix — row entries multiply when tap state varies across turns; the noisier Lands row may warrant a setting to keep lands always flat

**Files:** `BattlefieldStackProvider.cs`, `BattlefieldNavigator.cs` (DiscoverAndCategorizeCards, AnnounceCurrentCard, CheckWatchedCardState, TryAdvanceToSameNameSibling, GetLandSummary, GetStackSizeForCard)

---

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

### SelectCardsMultiZone: Space Without Selection Dismisses Silently

In SelectCardsMultiZone browsers (e.g. Abprall/Rebound triggers from Ojer Pakpatiq), pressing Space without first selecting a card via Enter clicks SingleButton which is "Ablehnen" (Decline). This silently declines the ability to cast the exiled spell. The help hint correctly says "Enter to select, Space to confirm", but the UX is confusing because:
- The card is announced on browser entry, making it feel already selected when it isn't
- Space = confirm with nothing selected = decline is consistent with other duel phases (pass), but unexpected when you're presented with a card you want to cast
- No warning is given that you're about to decline without having selected anything

**Possible improvements:**
- Warn the user when Space would confirm an empty selection in a SelectCardsMultiZone browser (e.g. "Keine Karte ausgewählt. Leertaste erneut zum Ablehnen" / "No card selected. Space again to decline")
- Auto-select the card when there's only one option, so Space immediately casts it
- Announce "Ablehnen" more prominently before executing it

**Observed in:** Abprall (Rebound) triggers during upkeep with Ojer Pakpatiq, Tiefste Epoche on the battlefield. Two triggers (Gedankenwirbel, Abtauchen) both dismissed unintentionally.

**Files:** `BrowserNavigator.cs` (ClickConfirmButton), `BrowserDetector.cs` (ConfirmPatterns includes "Single")

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

### Off-Screen Cards in Open All Pack Opening May Lack Full Card Info

When opening many packs at once (Open All), the game's virtualized scroll list only renders ~12 physical card slots. Cards beyond the viewport are represented as text-only entries with name and type from GrpId lookup. Arrow Up/Down card info (mana cost, rules text, P/T, etc.) is now provided via GrpId-based lookup for these off-screen cards, but this has not been tested in-game yet.

**Monitor for:**
- Whether off-screen cards actually show full Arrow Up/Down info (mana cost, type, rules text, rarity, etc.)
- Whether scrolling the viewport (if possible) correctly transitions cards between text-only and full GO elements
- Whether the card info blocks match what on-screen cards show

**Files:** `BoosterOpenNavigator.cs` (UpdateCardInfoForOffScreenCard, Move/MoveFirst/MoveLast overrides)

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

### Parenless Hybrid Mana In Rules Text (PR #90 follow-up)

PR #90 rewrote the rules-text mana parser so any o-prefixed input is routed through a tokenizer that accepts three shapes: o plus digits, o plus a single color letter, or o plus a parenthesized inner expression. The original parser's comment listed an additional shape — a single hybrid inside o-notation written without parentheses around the slash group — as supported. In-game testing on Spree (Insatiable Avarice) and Evoke (Deceit) confirmed Arena emits the parenthesized form, but the parenless form was not exhaustively verified across all card sets and locales.

If any card or localization string produces the parenless shape, the new tokenizer silently matches only the leading o plus the first color letter and drops the alternative color. No error, no log entry — just a missing color in rules text.

**Monitor for:**
- Hybrid mana in rules text or activated cost lines reading as a single color when the card definitely shows a hybrid symbol (e.g. just "white" instead of "white or blue").
- Especially on older sets, less-tested locales, or unusual cost shapes.

If reports surface, the fix is to extend the tokenizer's single-letter shape to optionally accept a slash plus a second color letter.

**Files:** `ManaTextFormatter.cs` (`ParseBareManaSequence` token regex)

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
3. Emote system improvements — fix and improve the emote system so emotes can be sent and received correctly, with proper screen reader announcements for incoming opponent emotes. Add the ability to mute the opponent's emotes.

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
10. Damage state announcement on creatures — read marked damage on creatures so the player can tell which ones are at risk during combat (e.g. a 4/4 with 3 damage marked is one point from dying). Currently only base toughness is announced; damage taken this turn is not surfaced, making lethal-damage assignment decisions harder than necessary.

