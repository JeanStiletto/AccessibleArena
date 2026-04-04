# Known Issues

Active bugs, limitations, and planned work for Accessible Arena.

For resolved issues and investigation history, see docs/old/RESOLVED_ISSUES.md.

## Active Bugs

### Blocking Sometimes Announces "0 0"

During Declare Blockers, the mod sometimes announces "0 0" instead of meaningful blocker information. Theory: this may happen when a single creature tries to block an attacker with menace (which requires at least two blockers), and the game rejects the illegal block assignment.

---

### Escape in Input Fields Wipes Text Content

Pressing Escape while editing an input field (e.g. registration form) clears the text the user just typed. The user must press Enter or Tab to "save" the text before pressing Escape, which is unintuitive. Tab works correctly because the mod explicitly fires `onEndEdit` on the old field with the correct text before deactivating.

**Root cause:** Unity's `TMP_InputField` natively handles Escape by reverting text to the original value from when the field was activated. This happens in Unity's internal processing *before* our `HandleInputFieldNavigation` runs. By the time we call `DeactivateFocusedInputField`, the field's `.text` has already been reverted.

**Possible fix:** Consume the Escape key before it reaches Unity's `TMP_InputField` — save the current text, call `ExitEditMode()`, then restore the text. This would preserve content on Escape, matching Tab/Enter behavior.

**On hold:** This would further extend our custom input field handling, which already significantly overrides Unity's standard behavior (custom edit mode, Tab interception, onEndEdit management, field reactivation). Before investing more in this pattern, evaluating how other game accessibility mods (e.g. Hearthstone Access) handle input fields to decide whether to continue with our approach or adopt a different paradigm.

**Issue:** [#59](https://github.com/FabianWilworeit/accessible-arena/issues/59)

**Files:** `InputFieldEditHelper.cs` (HandleEditing — Escape handler), `UIFocusTracker.cs` (DeactivateFocusedInputField)

---

### Registration Does Not Auto-Advance After Account Confirmation

After successfully sending the account confirmation email during registration, the game does not automatically advance to the tutorial. Under investigation — requires deep investigation by project owner.

---

### Deck Builder Done Button Triggers "Deck Creation Failed" Popup

Pressing Backspace or the Done button in the deck building screen triggers a "Deck creation failed" popup even though the deck was created successfully. Most likely a double-click caused by too many fallback activation attempts on the Done button. Similar double-activation issues have been solved in other screens before.

---

### Adding Duplicate Cards in Deck Builder Causes Focus Glitch

Adding more of the same card to a deck causes focus to jump to the wrong card, resulting in adding an incorrect card. Most likely the first add changes the card pool indices, and the mod's focused index now points to a different card.

---

### Crafting Popup Missing Home/End Navigation

Home and End keys do not work in the crafting popup for jumping to first/last element.

---

### Index Announcements Inconsistent Across Menus

Some menus announce position indices (e.g., "2 of 7") and some do not. Index announcements should be available everywhere, gated behind the tutorial completion or the verbose announcements setting.

---

### Objectives Show Meaningless Progress Text

Objective progress text is read out but contains meaningless or placeholder content that provides no useful information to the player.

---

### Pack Opening Reads Flipped-Down Cards

During pack opening, cards that are still flipped face-down can be read with Up/Down arrow keys, revealing their content before the player flips them.

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

### Set Filter + Text Search Combination Returns Empty Results

Combining set filters (Advanced Filters) with a text search in the deck builder collection returns 0 results, even when matching cards exist. For example: searching "brand" finds Brandende Welle normally, but adding the Avatar set filter produces 0 results despite the card having the correct Avatar set code (TLA). Clearing the search shows Avatar cards; clearing the filter shows search results. The combination fails.

**Root cause:** The game's internal card pool filtering logic does not correctly intersect set filter and text search criteria. Debug logging confirmed Brandende Welle has `ExpansionCode='TLA'` — identical to other Avatar cards that appear when only the set filter is active.

**Why it can't be fixed by the mod:** The game returns an empty card pool to `CardPoolHolder` before the mod reads it. The mod accurately reports what the game provides (`Search rescan pool: 0 -> 0`).

**Workaround:** Use set filters or text search individually, not both at the same time.

**Investigation:** [docs/investigations/card-filter-search-bug.md](investigations/card-filter-search-bug.md)

---

## Under Investigation

### Challenge Invite Popup — Dropdown Only Shows 1 Friend

The "Invite Friend to Challenge" popup contains a `cTMP_Dropdown` (DropdownHitbox) that only has 1 option (a single friend name), even when the user has multiple friends. The dropdown genuinely contains only 1 Toggle item — this is how the game populates it, not a mod bug.

**Open questions:**
- Does the dropdown show only online friends? Only the most recent? Only 1 by design?
- Is the dropdown useful at all, or is the input field ("Opponent") the intended way to select a friend?
- Should the mod skip the dropdown in this popup and guide users to the input field instead?

**Current behavior:** The mod correctly handles it as a single-item dropdown (arrow keys re-announce the one item, Enter selects it). The input field works for typing any friend's display name.

**Files:** `DropdownEditHelper.cs`, `GeneralMenuNavigator.cs` (popup mode)

---

### Mailbox Close Restores to Wrong Group

After closing the mailbox with Backspace, the GroupedNavigator restores to "Decks" on the home screen instead of the group the user was in before opening the mailbox (e.g., "Social"). The user can then navigate with Up/Down to find their position.

**Root cause:** When navigating inside the mailbox, the GroupedNavigator saves the current mail item position as `ElementGroup.Content`. On the home screen, the first `ElementGroup.Content`-typed group happens to be "Decks", so the restore succeeds but in the wrong place. The pre-mailbox position (Social, InsideGroup) is overwritten by the mailbox's internal navigation saves.

**Fix needed:** Save the home screen GroupedNavigator state when the mailbox opens, then restore to it explicitly in `CloseMailbox()` rather than relying on the auto-restore mechanism.

**Files:** `GeneralMenuNavigator.cs` (CloseMailbox, OverlayChanged handler)

---

### Steam Overlay Hijacks Shift+Tab

Steam's default overlay hotkey (Shift+Tab) conflicts with the mod's backward navigation (Shift+Tab for previous item, previous color in mana picker, etc.). When pressed, the Steam overlay opens instead of navigating. The overlay is not accessible to screen readers, so blind users must dismiss it and lose their navigation context.

**Current mitigation:** `Application.isFocused` guard in `OnUpdate()` prevents the mod from processing phantom inputs while the overlay is active. This doesn't prevent the overlay from opening but avoids state corruption.

**Shelved approach — WH_KEYBOARD_LL hook:** A low-level Windows keyboard hook that intercepts Shift+Tab at the OS message level before Steam sees it, while Unity's `Input.GetKeyDown` still works via Raw Input (separate delivery path). Fully implemented and tested to compile. Shelved due to concerns:
- Global keyboard hook is a classic keylogger/malware signature — AV false positive risk
- Operates outside normal modding boundaries (Harmony/MelonLoader scope) — stands out to anti-cheat review
- Hook intercepts ALL keystrokes system-wide (filters most through, but the footprint is large)
- Stability edge cases: Windows silently removes hooks with slow callbacks (>300ms), leaked hooks on crash

**Shelved code:** `src/Core/Services/old/SteamOverlayBlocker.cs` — ready to restore if risks are deemed acceptable.

**To restore:** Move file back to `src/Core/Services/`, add `SteamOverlayBlocker.Install()` in `OnInitializeMelon()` after `InitializeHarmonyPatches()`, add `SteamOverlayBlocker.Uninstall()` in `OnApplicationQuit()` before `_settings?.Save()`.

**Alternative for users:** Disable Steam overlay for MTGA (Steam > right-click game > Properties > uncheck "Enable Steam Overlay") or rebind Steam's overlay key (Steam > Settings > In-Game > Overlay Shortcut Keys).

---

## Monitoring

### SelectGroup Browser Pile Selection (Fact or Fiction)

In the SelectGroup browser (e.g. Curator of Destinies / Fact or Fiction pile selection), Enter and Space now activate the focused pile button. Previously, Enter activated a face-down card instead of the pile button, and Space fell through to PromptButton_Primary ("Opponent's Turn"), accidentally passing the turn.

**Fix applied:** Added SelectGroup-specific checks: Enter activates the focused button when `_isSelectGroup && _currentButtonIndex >= 0`; Space activates the current pile button the same way; PromptButton_Primary fallback is now excluded for SelectGroup browsers.

**Files:** `BrowserNavigator.cs` (Enter handler, ClickConfirmButton)

---

### Wrong Group Restore After Closing Mailbox

After closing the mailbox, the navigator landed on "Decks" instead of the pre-mailbox group (e.g., "Social"), and no screen announcement fired so the user didn't know they had returned to the home screen.

**Root cause:** The group restore logic saved `ElementGroup.Content` + a mailbox display name. After closing, it rescanned the home screen, found no Content group with that display name, then fell back to the first Content group by type — which was "Decks". Setting `PositionWasRestored = true` on that fallback match suppressed the home-screen announcement.

**Fix applied:** Removed the type-only fallback. Restore now requires an exact type + display name match. If no match is found, `PositionWasRestored` stays false and the screen announcement fires normally.

**Files:** `GroupedNavigator.cs` (OrganizeIntoGroups restore block)

---

### NPE Tutorial Combat Cancel Lock

During the first NPE tutorial fight (Game01, Turn 4), pressing Backspace to cancel attacks after the NPE prompted "attack with your creatures" caused a locked state. The NPE script doesn't handle attack cancellation at this stage — no highlights reappear, the primary button has empty text, and the game becomes unresponsive until a system message popup (timeout/disconnect) appears.

**Root cause:** The CombatNavigator clicked buttons with empty text (Space on an empty primary button) which sent spurious pointer events and desynced the game state. The subsequent Backspace cancel then put the NPE into an unrecoverable state.

**Fix applied:** `HandleInput()` now checks `HasPrimaryButtonText()` before processing any Space/Backspace during Declare Attackers and Declare Blockers. If the primary button has no text (UI in transition), the key is consumed but no button is clicked. This prevents both the spurious clicks and the cancel-in-broken-state scenario. When the NPE later expects cancellation, buttons will have proper text and Backspace will work normally.

**Files:** `CombatNavigator.cs` (HandleInput, HasPrimaryButtonText)

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

### Summoning Sickness Announcement Verbosity

Summoning sickness is now announced on all creatures and vehicles in every duel phase, based on the game model's `HasSummoningSickness` field. Monitor whether this creates too much noise during gameplay, especially:
- On boards with many freshly played creatures (e.g. token generation turns)
- During combat phases where "can attack" already implies no summoning sickness
- Whether players find it useful or distracting overall

**Possible adjustments if too verbose:**
- Add a user setting to toggle summoning sickness announcements on/off
- Suppress during combat phases (where "can attack"/"tapped" already convey the relevant info)
- Only announce on first navigation to a card, not on repeated passes

**Files:** `CombatNavigator.cs` (GetCombatStateText), `CardStateProvider.cs` (IsCreatureOrVehicleCard), `lang/*.json` (Combat_SummoningSickness)

---

## Needs Testing

### Other Windows Versions and Screen Readers

Only tested on Windows 11 with NVDA. Other Windows versions (Windows 10) and other screen readers (JAWS, Narrator, etc.) may work via Tolk but are untested.

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

### Challenge Friends Sometimes Not Working

Challenging friends sometimes fails: deck selection not available and screen elements auto-change unexpectedly. Exact reproduction steps unknown.

---

### Targeting Planeswalker with Burn Spell May Not Work

Targeting a planeswalker with a burn spell (direct damage) may not work correctly. Exact reproduction steps unknown.

---

### Check Payment Method Browser Not Loading

For some users, the "Check Payment Method" browser does not load or display correctly. Exact symptoms and reproduction steps unknown.

---

## Technical Debt

### Code Archaeology

Accumulated defensive fallback code needs review:
- `ActivateBackButton()` has 4 activation methods - test which are needed
- `LogAvailableUIElements()` (~150 lines) only runs once per screen in DetectScreen (removed from PerformRescan)
- Extensive logging throughout - review what's still needed

**Priority:** Low

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

1. ~~Manual trigger ordering~~ — Implemented in v0.8.7 (OrderCards/TriggerOrderCards browser support)

2. Display counters on players - announce counters like poison, energy, experience, etc. on players
3. Saga support - announce current chapter, total chapters, and chapter abilities for Saga enchantments
4. Verbose "Big Card" announcements (inspired by Hearthstone Access) - option to include card details inline with action announcements, with user preference toggle for brief vs verbose
5. Improve mod settings - reorganize settings into useful categories with category splitting for better navigation
### Polish

1. Improve Challenge Friend screen workflow communication — currently unclear for blind users. Consider adding a dedicated Ready button, more contextual hints, or custom strings to guide through the challenge setup flow.
2. Draft navigator polish — reduce unnecessary rescans, add a key to check how many copies of the focused card are already in the player's collection, and correctly announce and read the selected/picked state of cards during drafting.

#### Tutorial Improvements
- Add mana cost explanation (how mana payment works, color picker for any-color sources)
- Explain that creatures heal at the end of each turn — damage doesn't persist across turns, which is non-obvious for new players and important for combat decisions
3. Multi-target attack announcements — when multiple attack targets exist (e.g. opponent has planeswalkers alongside their life total), the mod needs to announce available targets and which target each attacker is assigned to. Consider adding a safeguard to prevent accidentally sending all attackers to one target when the player intended to split them.
4. Auto-advancing browsers can silently decline options — in browsers where selecting a card immediately advances (e.g. Rebound triggers), pressing Space without first selecting a card clicks the confirm/decline button, silently skipping the option. Needs either a safeguard (e.g. "no card selected, press Space again to decline" warning, similar to the phase skip confirmation) or a structural redesign so these browsers follow the standard Enter-to-select, Space-to-confirm pattern more safely.
5. Battlefield row categorization for land creatures — effects that turn lands into creatures (e.g. Nissa animating lands) cause them to appear in the Lands row (A/Shift+A) instead of the Creatures row (B/Shift+B). Conversely, effects that turn non-land permanents into lands (e.g. certain commander abilities) may miscategorize them. The categorization logic needs to handle cards with multiple types (Creature Land) more intelligently, potentially prioritizing the creature type for combat relevance.

### Low Priority / v1.1

1. Auto version checking and auto update - check for new mod versions on launch and optionally auto-update. May be too problematic to implement reliably.
2. Cube and other draft event accessibility - make Cube drafts and similar special draft events fully accessible (pick screens, pack navigation, deck building within event)
3. Ctrl+key shortcuts for navigating opponent's cards - additional Ctrl-modified zone shortcuts for quick opponent board access. Highly speculative; unlikely to be implemented unless requested by users.
4. Replace Tolk with Prism library - Tolk is Windows-only (NVDA/JAWS/Narrator). Prism supports multiple platforms (macOS VoiceOver, Linux Orca, etc.), which would enable multi-OS accessibility if MTGA ever runs on other platforms or via Proton/Wine.


