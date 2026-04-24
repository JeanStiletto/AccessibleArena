# Battlefield Stacking — Implementation Notes

Status: design accepted, implementation pending. Gated by the `BattlefieldStacking` setting (default off).

## Goal

Mirror Arena's visual token-stacking behavior for screen-reader navigation. When the game groups identical cards into a single visual stack, the mod should expose one logical entry announced as `"Cardname ×N"` instead of N separate entries. A user with 11 enemy creatures that visually collapse to 8 stacks should navigate 8 Left/Right steps, not 11.

## Game-side investigation

### Types (Core.dll, namespace `Wotc.Mtga.DuelScene.Universal`)

- `UniversalBattlefieldCardHolder` — scene-level holder. Private `List<UniversalBattlefieldRegion> _regions`.
- `UniversalBattlefieldRegion` — owns groups. Exposes `AllGroups`.
- `IUniversalBattlefieldGroup` / `UniversalBattlefieldGroup` — owns stacks. Exposes `AllStacks` and `VisibleStacks` (virtual stacks when collapsed). Config carries `RegionController` (GREPlayerNum) and `RegionType` (BattlefieldRegionType) — effectively "whose row" and "what row" (creatures / lands / etc.).
- `UniversalBattlefieldStack` — one logical entry. Key fields:
  - `DuelScene_CDC StackParent` — the representative card (what's drawn on top).
  - `List<DuelScene_CDC> AllCards` — every card in the stack (incl. parent).
  - `List<DuelScene_CDC> StackedCards` — everyone below the parent.
  - `HasAttachmentOrExile` — true when the stack carries attachments/exiled cards; game expands it visually.
  - `IsAttackStack` / `IsBlockStack` — stack currently participating in combat.
  - `AttachmentCount`, `ExileCount`, `OldestCard`, `YoungestCard`, `Age`.
  - `Sort()` — reorders `AllCards` and can reassign `StackParent`.

### Stacking rule

`UniversalBattlefieldGroup.TryAddCard` uses `IEqualityComparer<DuelScene_CDC>` (= `Wotc.Mtga.DuelScene.Battlefield.CanStackComparer`) which delegates to `CardViewUtilities.CanStack`. `CanStack` combines hard gates (e.g. `CantStackWithAnything`, `SelectedBy.SetEquals`, workflow's `CanStack`, vehicle crew state, AutoPay highlight mismatch, blocker-ordering via `OrderBlockersByLikeness`) with `IsSame(MtgCardInstance, MtgCardInstance)`.

`IsSame` compares:

- `TitleId`, `ObjectType`, `Controller.InstanceId`, `Owner.InstanceId`
- `IsTapped`, `HasSummoningSickness`, `EnteredZoneThisTurn`, `IsDamagedThisTurn`
- `Power`, `Toughness`, `SuppressedPower`, `SuppressedToughness`, `PowerToughnessInverted`, `Damage`, `Loyalty`, `Defense`, `LoyaltyActivationsRemaining`, `ChooseXResult`
- `Colors`, `Supertypes`, `CardTypes`, `Subtypes`, `RemovedSubtypes`
- `Abilities` (same set), `ActiveAbilityWords`, `Actions.Count`
- `Zone.Type`, `Counters` (type → count), `BoonTriggersInitial/Remaining`
- `AffectorOfQualifications`, `AffectedByQualifications`, `LinkedInfoTitleLocIds`, `LinkedInfoText`, `AffectorOfLinkInfos`, `AffectedByLinkInfos`
- `Targets`, no `TargetedBy`, `AttackTargetId`, `BlockState`, `AttackState`
- `FaceDownState.IsFaceDown` + `OverlayGrpId` + `BaseGrpId`
- `IsObjectCopy`, perpetual effects (`SamePerpetualValues`), `Distributions.Count == 0`
- Plus blocker-similarity (`AreBlockingInstancesSimilar` on `BlockingIds`).

**Implication:** replicating `IsSame` ourselves would drift with every game patch. The right approach is to read the game's already-computed stacks.

### Log evidence

Last duel (`Latest.log` 11:37:27): `EnemyCreatures: 11 cards`. Attackers list: `Gefräßige Roboter 2/1, Enthusiastische Mechanautin 2/2, Roboter 1/1, Ramos die Drachenmaschine 9/9, Roboter 1/1, Phyrexianer, Schrecken 1/1` — multiple identical entries that the game collapses visually.

## Mod-side design

### Setting

`ModSettings.BattlefieldStacking` (default `false`). Exposed in F2 menu as "Battlefield stacking". When off, BattlefieldNavigator behaves exactly as today. When on, the stack-based path engages.

### Data flow

1. A new `BattlefieldStackProvider` reads `UniversalBattlefieldCardHolder._regions` via reflection (hierarchy walk; cached `FieldInfo`). For each region, iterates `AllGroups` → `AllStacks` and yields `(stack, StackParent GameObject, AllCards count)`.
2. `BattlefieldNavigator.DiscoverAndCategorizeCards` builds each row as a `List<StackEntry>` when the setting is on. A `StackEntry` holds:
   - `GameObject Representative` — the `StackParent` GO (focus target).
   - `uint ParentInstanceId` — stable identity across re-scans.
   - `int Count` — `AllCards.Count`.
   - Optional `List<uint> MemberIds` for later debugging.
3. `AnnounceCurrentCard` appends `" ×{Count}"` (locale string) when `Count > 1`. Everything else (combat state, attachments, targeting, type label) is read from `Representative` — `IsSame` guarantees those values are shared across all members.
4. Clicking the entry clicks the `Representative`, same as a single card today.

### Row categorization

Categorize by `StackParent`'s model (`CardStateProvider` already handles ownership + type). Do **not** rely on the game's region/group configuration — keeping our six-row layout (Player/Enemy × Creatures/NonCreatures/Lands) avoids coupling to the game's region types.

### Split-announcement behavior (revised from first draft)

When a user clicks a stack and it splits (e.g. 1 of 4 Goblins becomes an attacker → game creates a 3-stack + 1-stack), the watcher:

1. Detects the split-off card's state change on the now-separated 1-card stack.
2. Announces the state change **as it would for a single card today** — e.g. just `"greift an"`.
3. Then announces the current stack at the user's focus, same format as navigation: `"Goblin ×3"`.

Two short utterances, no combined "1 attacking, 3 remaining" sentence. This keeps the audio pattern identical to normal navigation: state change + cardname. Focus stays on the shrunk stack (still the user's current position) — repeated Enter keeps selecting more until the stack is empty.

### Focus restoration across re-scans

Key by `ParentInstanceId` first, then fall back to same row index clamped. A stack that vanished entirely (all members sacrificed) clamps to neighbor; a stack whose parent changed (game's `Sort()` reassigned) still matches if its old `ParentInstanceId` is now in another stack's `AllCards`.

### Stacks that must be treated as individuals

The game expands stacks visually for:

- Attachments/exile (`HasAttachmentOrExile`) — already each attached card must be individually readable.
- Targeting flows where multiple targets + attachments are present (`UniversalBattlefieldRegion.HandleCardClick`).

When a stack has `HasAttachmentOrExile == true`, emit each `AllCards` entry as its own row item. HotHighlightNavigator's Tab order already separates differently-highlighted cards; `SelectedBy` differences already split stacks at the `CanStack` level, so target highlighting won't be hidden inside a stack.

## Revised edge-case list

1. **Stack splits on state change.** Click Goblin-×4 → game creates Goblin-×3 + Goblin-×1-attacking (`SelectedBy`/`AttackState` diverge, `IsSame` fails). Watcher announces `"greift an"` (normal state-change text for the split-off card), then `"Goblin ×3"` (the shrunk stack at current focus). Two utterances, no composite "1 attacking, 3 remaining" phrasing.
2. **`StackParent` can change.** `Sort()` reassigns parent when attachments disappear or workflow context changes. Don't cache by GameObject — key by `StackParent.InstanceId` each announcement, and re-resolve on every rescan.
3. **HotHighlightNavigator × stacks.** If only 2 of 4 tokens are valid targets, `SelectedBy` already differs, so the game splits them. When the game expands a stack for targeting (multi-target-with-attachments path in `UniversalBattlefieldRegion.HandleCardClick`), our row must fall back to per-card entries, or HotHighlightNavigator's Tab indices and BattlefieldNavigator's row indices will disagree.
4. **Block declaration.** `OrderBlockersByLikeness` keeps identical blockers together only when damage ordering is compatible. Don't assume "two identical 2/2s" always collapse during declare-blockers; read what the game gives.
5. **Damaged vs undamaged.** 1 Goblin with 1 damage this turn ≠ 3 undamaged Goblins → 2 stacks. Announced as `"Goblin 1/1, 1 Schaden"` and `"Goblin 1/1 ×3"` on separate rows.
6. **Tapped vs untapped.** 2 tapped Islands + 3 untapped → 2 stacks. Lands row can actually get noisier with stacking because tapping state flips every turn. Keep Land Summary (`M` / `Shift+M`) as the dense one-line summary; the stacked A/Shift+A is still useful for per-land interaction.
7. **Selected-by-me vs selected-by-opponent / multi-target selection.** Split. Announce explicitly: "1 selected" stack + "3 unselected" stack, each as its own row entry — blind users get a precise handle on multi-select workflows.
8. **Non-creature tokens** (Treasure, Food, Clue, Blood). Land in PlayerNonCreatures row. Make sure the primary-type label logic continues to work with the stack representative.
9. **Creature-lands.** Game's `IsSame` includes current `CardTypes`, so an animated Nissa-land doesn't stack with inert lands. No new risk vs. today.
10. **Face-down tokens** (morph/manifest/cloak). `IsSame` requires same `OverlayGrpId` and `BaseGrpId`. Only stacks if both cards are face-down and otherwise identical.
11. **Focus restoration across re-scans.** Re-focus by `ParentInstanceId`; fall back to clamped row index; fall back to row first entry.
12. **Arrow Up/Down card info.** The parent's info is correct for the group — but if we ever show a count-like summary in detail blocks ("8 +1/+1 counters total"), that would be per-card and wrong. Keep info blocks per-card framing; append `×N` only to the identity line.
13. **Opponent hidden info.** Opponent face-down cards with different real GrpIds look identical to us and stack as the game displays them. Fine.
14. **Activation index when game reorders `AllCards`.** `Sort()` reorders by available-actions / blocker-likeness. Use the explicit `StackParent` property; don't assume `AllCards[0]` is the parent.
15. **`UniversalBattlefieldCardHolder` lookup.** The GameObject currently cached as `"BattlefieldCardHolder"` in `DuelHolderCache` may or may not carry the `UniversalBattlefieldCardHolder` MonoBehaviour directly (regions may be children). First pass: `FindObjectsOfType` by type name, cache once per scene. Verify during the read-only prototype pass.

## Rollout plan

1. Add `BattlefieldStacking` setting (off by default) + F2 menu entry + locale strings. **← this PR.**
2. Read-only prototype: `BattlefieldStackProvider` logs the stack structure every time `DiscoverAndCategorizeCards` runs (behind the flag). Compare against flat card list for a real match. No announcement changes yet.
3. Wire BattlefieldNavigator to use stacks for iteration + announcement when the flag is on. Revised split-announcement behavior.
4. Watcher rework for stack-aware state change.
5. Settings menu description, docs/KNOWN_ISSUES cleanup (item 8 under "Planned Features → Polish" is this feature).

## Files touched (expected)

- `src/Core/Services/ModSettings.cs` — new bool field + JSON.
- `src/Core/Services/ModSettingsNavigator.cs` — menu entry.
- `src/Core/Models/Strings.cs` + `lang/en.json` + `lang/de.json` — strings.
- `src/Core/Services/BattlefieldNavigator.cs` — branch by setting (pending).
- `src/Core/Services/BattlefieldStackProvider.cs` — new, reflection-based (pending).
- `llm-docs/type-index.md` — add stacking-related types.
