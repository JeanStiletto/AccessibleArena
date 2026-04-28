# Cosmetics — Implementation Plan

Goal: blind players can read and change deck-level cosmetics (avatar, sleeve, pet, emotes) and per-card art styles in the deck builder, mirroring the right-click flow sighted players use today.

**Design principle**: hook the existing sighted UI (`DeckDetailsPopup` and `CardViewerController`) and make it keyboard-navigable, instead of building parallel mod-only UI. Same pattern as `AdvancedFiltersNavigator`. Less code to maintain, fewer state assumptions, automatic feature parity when the game updates.

## 1. Game architecture summary

### 1.1 Per-card art styles (skinCode)

Storage on the deck:
- `DeckBuilderModel` (Core.Code.Decks namespace, Core.dll) holds `Dictionary<uint, string>` of `grpId → skinCode`
- Read: `Model.GetCardSkin(grpId)` → string (null = default art)
- Write: `Model.SetCardSkin(grpId, skinCode)`
- Initial state from `DeckBuilderContext.CardSkinOverride` (server snapshot)

Catalog of available styles for a card:
- `WrapperController.Instance.Store.CardSkinCatalog.TryGetSkins(artId, out List<ArtStyleEntry>)`
- `ArtStyleEntry` fields: `Variant` (the skin code string), `StoreItem`, `StoreSection` (`EStoreSection`), `Source` (`AcquisitionFlags` enum: BattlePass / CodeRedemption / Event / SeasonReward)
- Ownership: `CosmeticsProvider.TryGetOwnedArtStyles(artId, out List<string> owned)`

Sighted right-click flow:
1. `MetaCardView.OnPointerDown` checks `eventData.ConfirmOnlyButtonPressed(InputButton.Right)` → `Holder.OnCardRightClicked` (Action<MetaCardView>)
2. `CardPoolHolder.OnCardRightClickedImpl` → `DeckBuilderActionsHandler.CardRightClicked(cardView, zoomHandler)`
3. → `OpenCardViewer(cardView, zoomHandler, qty)` fires `CardViewerRequested(printingData, currentSkin, qty, OnSkinSelected)`
4. `WrapperDeckBuilder.OpenCardViewer` → `CardViewerUtilities.OpenCardViewer(...)` opens `CardViewerController` popup with carousel
5. On confirm: `OnSkinSelected(skinCode)` → `ModelProvider.ReplaceCardsByTitleId(printing, skinCode)`

We trigger the same flow programmatically and navigate the resulting popup.

### 1.2 Deck-level cosmetics

Storage on the deck (`DeckBuilderModel`):
- `_avatar` (string)
- `_cardBack` (sleeve string)
- `_pet` (string, format `"name.variant"`)
- `_emotes` (List<string>)
- `_deckTileId` + `_deckArtId` (deck-box face — out of scope for v1)

Setters (all on `DeckBuilderModelProvider`): `SetSelectedAvatar`, `SetSelectedSleeve`, `SetSelectedPet`, `SetSelectedEmotes`, `OnDefaultCosmeticSelected(CosmeticType)`.

Catalogs (via `Pantry`): `AvatarCatalog`, `PetCatalog`, `IDeckSleeveProvider`, `IEmoteDataProvider`, `CosmeticsProvider._vanitySelections`.

UI: `DeckDetailsPopup` is the single editor (both viewing and editing). Two trigger paths:
- `OpenDeckDetails()` → `DeckDetailsRequested` → opens at default view (deck title button)
- `OpenDeckDetailsCosmeticsSelector(cardDatabase, type)` → `DeckDetailsCosmeticsSelectorRequested` → jumps to a tab (`DisplayCosmeticsTypes`: Avatar / Sleeve / Pet / Emote / Title)

The popup hosts `CosmeticSelectorController` with `DisplayItem*` tiles — same controller `ProfileNavigator.cs` already handles for profile-side cosmetics.

### 1.3 Out of scope for v1

- **Battlefield / banner / hero** — not stored per-deck. Profile-only via `ClientVanitySelectionsV3`.
- **Store / purchase flow** — surface ownership state as info, but don't drive purchases.
- **`AutoApplyCardStyles` toggle** — settings flag, defer.
- **Deck-box face** (`_deckTileId`, `_deckArtId`) — defer.

## 2. Existing mod hooks to reuse

- `src/Core/Services/AdvancedFiltersNavigator.cs` — the **template**. `BaseNavigator` that detects a popup via `PanelStateManager.IsPanelActive(...)`, discovers interactive elements, navigates them with grid keys.
- `src/Core/Services/ProfileNavigator.cs` — already has reflection on `CosmeticSelectorController`, `DisplayItem*` fields, `AvatarSelectPanel`. Reuse the reflection cache instead of duplicating.
- `src/Core/Services/DeckInfoProvider.cs` — already reaches `Pantry.Get<DeckBuilderModelProvider>().Model` via reflection. Template for read-only state access.
- `src/Core/Constants/GameTypeNames.cs` — has `CosmeticSelectorController`, `AvatarSelectPanel`, `DisplayItem*`, `DeckMainTitlePanel` constants.

## 3. Design

### 3.1 `DeckDetailsNavigator` — deck-wide cosmetics (modeled on `AdvancedFiltersNavigator`)

**No new global hotkey.** Sighted users open the popup via the deck title button at the top of the deck builder; blind users do the same — they navigate the deck builder's existing buttons (already accessible) and activate the deck-title button. When `DeckDetailsPopup(Clone)` becomes active, this navigator takes over.

New file: `src/Core/Services/DeckDetailsNavigator.cs`. `BaseNavigator` subclass.

`DetectScreen()`: `PanelStateManager.IsPanelActive("DeckDetailsPopup(Clone)")`.

Priority: same band as `AdvancedFiltersNavigator` (87) — above battlefield, below settings.

`DiscoverElements()`: build a flat ordered list:
1. Deck name input (`_deckNameInput` TMP_InputField — reuse `BaseNavigator.InputFields` editing helper)
2. Format dropdown (`FormatDropdown` cTMP_Dropdown — reuse `BaseNavigator.Dropdowns`)
3. Cosmetic tiles via `CosmeticSelector` field (`CosmeticSelectorController`):
   - Avatar (`DisplayItemAvatar`)
   - Sleeve (`DisplayItemSleeve`)
   - Pet (`DisplayItemPet`)
   - Emote (`DisplayItemEmote`)
   - Title (`DisplayItemTitle` if not killswitched)
4. Close button(s) (`CloseButtons[]`)

Up/Down moves through the list. Each tile announces current selection ("Avatar: Liliana of the Veil. Press Enter to change.") read from `DeckBuilderModel._avatar` etc. resolved via `AvatarCatalog`/`PetCatalog`/etc. localized name lookups.

Enter on a tile invokes the tile's `OpenSelector()` (existing public method on `DisplayItemCosmeticBase`) — this is what sighted users' clicks do. The tile expands its in-popup selector grid. `DiscoverElements()` re-runs to pick up the now-visible selector contents, and the user navigates them with arrows + Enter to apply, Backspace to back out of the sub-selector.

The `_onSelect` callbacks on `DisplayItemSleeve` / `DisplayItemPet` / etc. are already wired by `DeckDetailsPopup.Init` to `DeckBuilderModelProvider.SetSelected*` — clicking a tile applies it. We don't call setters ourselves; we click the underlying button and let the game's existing wiring fire.

Backspace at top level closes the popup via `CloseButtons[0]`.

### 3.2 `CardViewerNavigator` — per-card art-style picker (`Shift+Enter` trigger)

**Hotkey: `Shift+Enter` on a focused card** in the deck builder (pool, deck list, deck column views). Mirrors the sighted right-click. Conceptually: `Enter` does the default action (add/remove card), `Shift+Enter` opens "additional things you can do on this card" — which today maps cleanly to the existing `CardViewerController` popup (style picker + craft + store).

The trigger is a thin shim — it fires the game's existing right-click code path:
```
ActionsHandler.OpenCardViewer(focusedMetaCardView, zoomHandler);
```
or, if we want to skip craft mode and land directly in cosmetic mode, we call `DeckBuilderActionsHandler.OpenCardViewer` with a non-null `craftSkin` (the current skin) — that's how the popup decides between modes.

When `CardViewerController`'s popup becomes active, a new `CardViewerNavigator` (also `BaseNavigator`, also patterned on `AdvancedFiltersNavigator`) takes over.

New file: `src/Core/Services/CardViewerNavigator.cs`.

`DetectScreen()`: `PanelStateManager.IsPanelActive("CardViewerController(Clone)")` (or the actual instance name — verify in-game).

`DiscoverElements()`: build an ordered list driven by which mode the popup is in (`_ContentCraft.activeSelf` vs `_ContentCosmetics.activeSelf` — reflection on private fields):

Cosmetic mode:
1. Card name + current style header (read-only announcement)
2. Style carousel — each `_cosmeticSelectors[i]` is a navigable item:
   - Announce: "Showcase. Owned. Currently applied." / "Borderless. Not owned, 1500 gems, from Bundle." / "Anime. Owned. Press Enter to apply."
   - Source tags from `ArtStyleEntry.Source` and `StoreSection`
   - Left/Right (or Up/Down) cycles — invoke the existing `SetCurrentSelector(int)` method
3. `_selectButton` — Enter applies (fires `_onSelect(skinCode)` → `DeckBuilderActionsHandler.OnSkinSelected` → `ReplaceCardsByTitleId`)
4. `_cosmeticGemsButton` / `_cosmeticGoldButton` — purchase (announce price + currency, but require user confirmation)
5. `_storeButton` — go to store (announce intent)
6. `_cancelButton` — Backspace closes

Craft mode:
1. Card name + craft preview
2. `_CraftPips[]` — quantity selectors
3. `_craftButton`
4. `_cancelButton`

Read-only mode (event/sealed): announce "style fixed by event" and only allow read.

Backspace closes via `_cancelButton.OnClick.Invoke()`.

### 3.3 Read-only inspection — fold into existing card detail blocks

Extend the deck-builder card info-block readout (Arrow Down on a focused card) with one extra line: `"Style: Showcase"` or `"Style: Default art"`, sourced from `Model.GetCardSkin(grpId)` reverse-resolved to `ArtStyleEntry.Variant` localized name.

This is a minor extension of whatever currently builds the card info blocks — no new navigator, just a reader call.

## 4. File plan

New:
- `src/Core/Services/DeckDetailsNavigator.cs` — popup-detect navigator for `DeckDetailsPopup(Clone)`
- `src/Core/Services/CardViewerNavigator.cs` — popup-detect navigator for `CardViewerController(Clone)`
- `src/Core/Services/DeckCosmeticsReader.cs` — small read-only helper: resolve current avatar/sleeve/pet/emote IDs to localized display names; resolve card skin → display name. Shared by both navigators and the info-block extension.

Modified:
- `src/Core/Services/GeneralMenuNavigator/GeneralMenuNavigator.DeckBuilder.cs` — add `Shift+Enter` handler that resolves the focused `MetaCardView` and calls `DeckBuilderActionsHandler.OpenCardViewer`. Extend card info-block readout with current style line.
- `src/Core/Constants/GameTypeNames.cs` — add `CardSkinCatalog`, `CosmeticsProvider`, `DeckBuilderModelProvider`, `DeckBuilderActionsHandler`, `CardViewerController`, `DeckDetailsPopup` constants if missing.
- `CLAUDE.md` (Safe Custom Shortcuts section) — document `Shift+Enter` for the per-card actions menu and note that deck cosmetics reuse the existing deck-title-bar popup.
- `docs/CHANGELOG.md` — under next version.

## 5. Implementation steps (in order)

1. **`DeckCosmeticsReader`** — read-only helpers backed by `ReflectionCache<THandles>`:
   - `GetCurrentAvatarName() / GetCurrentSleeveName() / GetCurrentPetName() / GetCurrentEmoteSummary()` — use `Languages.ActiveLocProvider` for localization
   - `GetCardStyleName(uint grpId)` → `"Default art"` or localized variant name
   - Verify with debug log when entering deck builder.

2. **`DeckDetailsNavigator`** — copy `AdvancedFiltersNavigator` skeleton, change panel name and discovery:
   - Detect `DeckDetailsPopup(Clone)`
   - Discover deck-name input → format dropdown → 5 cosmetic tiles → close button
   - Use existing `BaseNavigator.InputFields` / `BaseNavigator.Dropdowns` mixins
   - Tile activation calls `DisplayItemCosmeticBase.OpenSelector()` (public method) on the tile's component
   - Re-discover after tile activation to find the now-visible selector grid

3. **`CardViewerNavigator`** — popup-detect navigator for `CardViewerController(Clone)`:
   - Mode detection via `_ContentCraft.activeSelf` / `_ContentCosmetics.activeSelf` (reflection)
   - Cosmetic-mode list: carousel selectors + buttons
   - Carousel navigation calls `SetCurrentSelector(int)` (private — reflection)
   - Button clicks via `CustomButton.OnClick.Invoke()`

4. **`Shift+Enter` trigger in deck builder**:
   - Add to `GeneralMenuNavigator.DeckBuilder.cs` (or whichever sub-navigator owns card focus)
   - Resolve focused card's `MetaCardView` and `ICardRolloverZoom` (from `WrapperDeckBuilder` or holder)
   - Call `DeckBuilderActionsHandler.OpenCardViewer(metaCardView, zoomHandler, 1)`

5. **Card info-block extension** — append style line via `DeckCosmeticsReader.GetCardStyleName`.

6. **Edge cases**:
   - Read-only deck (`Context.IsReadOnly` / `IsSideboarding`) — popups still open; navigator should announce "read-only" and block Enter on apply actions, allow read.
   - Limited / Sealed events with `CardSkinOverride` from event data — read-only display.
   - Empty catalog (no owned cosmetics) — selector grid is empty; announce "no alternatives owned".
   - Locale changes mid-session — `Languages.LanguageChangedSignal` listener invalidates display-name cache in `DeckCosmeticsReader`.
   - Popup opened with non-default tab (`OpenDeckDetailsCosmeticsSelector`) — discover handles whichever sub-selector is visible.

7. **Testing checklist** (manual, in-game):
   - Deck builder → activate deck title button → `DeckDetailsPopup` opens → navigate to Sleeve tile → Enter → see selector → pick sleeve → verify deck box updates.
   - Repeat for Avatar, Pet, Emote.
   - Focus a card with multiple styles → `Shift+Enter` → `CardViewerController` opens in cosmetic mode → carousel navigation announces each style + ownership + source → Enter on owned style → verify "applied to N copies" announcement matches deck quantity, verify game saves.
   - Apply default art (carousel index 0 with null variant) → verify reverts.
   - Read-only deck (shared) → popup opens read-only → reading works, Enter on apply blocked with announcement.
   - Sealed/Draft event deck → `Shift+Enter` either disabled or shows event-fixed style only.
   - German locale → all display names localize.

## 6. Open questions for implementation session

- Confirm `Shift+Enter` doesn't collide with anything in current deck-builder card-focus context. `Enter` is reserved per CLAUDE.md, but `Shift+Enter` should be free. Verify in `GeneralMenuNavigator.DeckBuilder.cs`.
- Confirm `DeckDetailsPopup`'s actual GameObject name in scene — `(Clone)` suffix is expected for instantiated prefabs but verify with `PanelStateManager` log on first open.
- `CardViewerController` GameObject name in scene — same verification.
- `DisplayItemTitle` is gated by `IsTitlesDisabled` killswitch — handle when absent.
- Pet `"name.variant"` parsing — confirm format on a real pet via debug log; may need `string.Split('.')` with edge handling.
- Card-focus integration: does deck builder have a single "currently focused card" provider, or does each view (`DeckListView` / `DeckColumnView` / `CardPoolHolder`) track separately? The `Shift+Enter` handler needs to resolve `MetaCardView` from whichever view has focus.

## 7. Risk notes

- We rely on `CosmeticSelectorController.DisplayItemCosmeticBase.OpenSelector()` being a stable public method. It's been stable through the profile-side use; low risk.
- `DeckBuilderActionsHandler.OpenCardViewer` is the same path the game uses for right-click — calling it programmatically should be indistinguishable from a real right-click. Risk only if a future game version ties it to physical input state.
- The cosmetic-mode carousel in `CardViewerController` only populates `_skins` with one entry in the decompiled `Setup` path. Need to verify in-game that the full carousel populates when entering cosmetic mode — there may be a separate fill method we haven't traced. If `_skins.Count == 1`, fall back to enumerating `CardSkinCatalog.TryGetSkins` ourselves and announcing without using the carousel UI (we'd still apply via the popup's `_selectButton`).
- `Coroutine_UpdateAllDecksWithDefaultSleeve` exists — implies account-default sleeve logic that mutates many decks. We touch the *current* deck only via tile activation; the game decides scope. Don't try to be clever.
- Auto-save: game saves via `WrapperDeckBuilder.CacheDeck` on cosmetic change. No extra save call needed on our side.
