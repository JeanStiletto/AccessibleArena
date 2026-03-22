# Known Issues

Active bugs, limitations, and planned work for Accessible Arena.

For resolved issues and investigation history, see docs/old/RESOLVED_ISSUES.md.

## Active Bugs

### Blocking Sometimes Announces "0 0"

During Declare Blockers, the mod sometimes announces "0 0" instead of meaningful blocker information.

---

### Command Zone Shows Wrong Cards for Opponent

When the opponent's commander is played, the command zone sometimes displays random other cards or shows your own commander in the opponent's command zone.

---

### Registration Does Not Auto-Advance After Account Confirmation

After successfully sending the account confirmation email during registration, the game does not automatically advance to the tutorial. Under investigation — requires deep investigation by project owner.

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

### Challenge Invite Popup — Dropdown Only Shows 1 Friend

The "Invite Friend to Challenge" popup contains a `cTMP_Dropdown` (DropdownHitbox) that only has 1 option (a single friend name), even when the user has multiple friends. The dropdown genuinely contains only 1 Toggle item — this is how the game populates it, not a mod bug.

**Open questions:**
- Does the dropdown show only online friends? Only the most recent? Only 1 by design?
- Is the dropdown useful at all, or is the input field ("Opponent") the intended way to select a friend?
- Should the mod skip the dropdown in this popup and guide users to the input field instead?

**Current behavior:** The mod correctly handles it as a single-item dropdown (arrow keys re-announce the one item, Enter selects it). The input field works for typing any friend's display name.

**Files:** `DropdownEditHelper.cs`, `GeneralMenuNavigator.cs` (popup mode)

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

### Browser Hint Accuracy

Per-browser-type tutorial hints were added for 16 browser types (Scry, Surveil, London Mulligan, SelectCards, AssignDamage, etc.). Some hints may be missing, wrong, or incomplete for edge cases:
- New browser types added by game updates won't have specific hints (fall back to generic "BrowserHint")
- Some browsers may behave differently depending on context (e.g. SelectCards with optional vs required selection)
- Hint text may not match actual keybindings if shortcuts are changed in the future

**Action:** When testing browsers in-game, verify Ctrl+F1 returns the correct hint and entry announcements include appropriate instructions. Report any mismatches.

**Files:** `BrowserNavigator.cs` (GetBrowserHintKey), `lang/*.json` (BrowserHint_* keys)

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

1. Manual trigger ordering - allow players to manually choose the order of their triggered abilities when multiple triggers happen simultaneously
2. Display counters on players - announce counters like poison, energy, experience, etc. on players
3. Saga support - announce current chapter, total chapters, and chapter abilities for Saga enchantments
4. Verbose "Big Card" announcements (inspired by Hearthstone Access) - option to include card details inline with action announcements, with user preference toggle for brief vs verbose
5. Game log - accessible scrollable log of recent game events (spells cast, damage dealt, cards drawn, etc.) for reviewing what happened
6. Improve mod settings - reorganize settings into useful categories with category splitting for better navigation
7. Summoning sickness announcement - announce summoning sickness status on creatures, controlled by a user setting

### Tutorial System

**Goal:** Help blind players learn the mod's controls and the game's mechanics through accessible, context-sensitive guidance.

**1. Text readability for existing tutorial messages**
- The game's built-in tutorial (Color Challenge) displays popup messages with instructions and lore
- These are currently not reliably read by the screen reader
- Detect tutorial message panels and announce their text content automatically
- Ensure multi-step tutorials (e.g., "click your creature", "now click the enemy") are announced at each step

**2. Custom tutorial message system**
- Framework for triggering mod-specific tutorial messages at appropriate moments
- Messages should be dismissible (Backspace/Enter) and not block gameplay
- Track which messages the user has already seen (persist across sessions via settings)
- First-launch tutorial covering basic navigation concepts

**3. Looping animation detection**
- The game sometimes enters looping animations waiting for user action (e.g., attack arrow hovering, target selection active) with no audio or text cue
- Detect when the game is stuck in such a loop and prompt the user with what action is expected
- Example: "The game is waiting for you to select a target. Use battlefield navigation (B) to choose a creature, then press Enter."
- Example: "Declare attackers phase. Press Space to confirm attacks or Backspace to cancel."

**4. Contextual explanations for mod-specific controls**
- F1 and F2 menus: explain that F1 opens the help overlay with all shortcuts and F2 opens the mod settings menu
- Attacking: explain that in MTG, attackers always attack the opponent (or a planeswalker) — you do NOT choose which creature to attack. The opponent then decides how to block. This is a key difference from Hearthstone where you pick a target for each attacker. During Declare Attackers, simply toggle your creatures as attacking (Enter) and confirm (Space) — there is no target selection step
- Blocking: explain how to assign blockers during Declare Blockers step (navigate to attacker, press Enter, select blocker)
- Mana costs: explain how mana payment works when a card requires specific colors, and how the mana color picker (Tab/number keys) appears for any-color sources
- Confirming with Space: explain that Space acts as the primary confirm/pass/next button during duels (pass priority, confirm attacks, submit choices)
- Backspace behavior: explain that Backspace is the universal cancel/back/dismiss key (cancel targeting, decline attacks, close popups, go back in menus)

### Polish

1. Check all tutorial messages for completeness and correctness - review every context-sensitive tutorial tip for accuracy, missing steps, and outdated references
2. Better group announcements - improve how element groups are announced when entering/switching groups
3. Next button in deck building screen - make the Next/Done button accessible during deck construction
4. Improve role reading for choose-options browsers - better announce the player's role/task when entering choose-options browsers (e.g., "choose cards to discard", "choose a mode")

### Low Priority / v1.1

1. Auto version checking and auto update - check for new mod versions on launch and optionally auto-update. May be too problematic to implement reliably.
2. Cube and other draft event accessibility - make Cube drafts and similar special draft events fully accessible (pick screens, pack navigation, deck building within event)
3. Cosmetic handling support - accessible navigation and selection for emotes, avatars, card sleeves, card styles, and companions
4. Ctrl+key shortcuts for navigating opponent's cards - additional Ctrl-modified zone shortcuts for quick opponent board access. Highly speculative; unlikely to be implemented unless requested by users.


