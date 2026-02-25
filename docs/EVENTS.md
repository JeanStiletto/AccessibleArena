# Events System

Accessible navigation for MTGA's event system. Covers event tile enrichment on the Play Blade, event page detail, and Jump In packet selection.

---

## General Event Navigation

### Event Tiles (Play Blade)

Event tiles appear in the Play Blade's events tab. Each tile is a `PlayBladeEventTile` with a `MainButton` child used for activation.

**Text enrichment:** `UITextExtractor.TryGetEventTileLabel` detects event tiles by walking the parent chain for "EventTile -" naming pattern, then calls `EventAccessor.GetEventTileLabel` which reads:
- Title text from `_titleText` (Localize -> TMP_Text)
- Ranked indicator from `_rankImage` (active Image)
- Bo3 indicator from `_bestOf3Indicator` (active RectTransform)
- In-progress status from `_attractParent` (active RectTransform)
- Progress pips from `_eventProgressPips` (counts active Fill children)

**Announced format:** "{title}, {progress}, Ranked, Best of 3" (optional parts only when active)

### Event Page

When a user activates an event tile, the game opens `EventPageContentController`. The mod enriches the screen name by reading the event context.

**Screen name:** "Event: {title}" via `EventAccessor.GetEventPageTitle()` which reads:
- `_currentEventContext` -> `PlayerEvent` (public **field**, not property) -> `EventUXInfo.PublicEventName` (preferred, localized)
- Fallback: `EventInfo.InternalEventName` (underscore-separated, cleaned up)

**Event summary:** `EventAccessor.GetEventPageSummary()` reads:
- `PlayerEvent.CurrentWins` / `PlayerEvent.MaxWins` -> "{wins}/{maxWins} wins"

### Event Page Info Navigation (Up/Down)

Event page description text is navigable via Up/Down arrows as virtual info items.

**Info block extraction:** `EventAccessor.GetEventPageInfoBlocks()` scans all active `TMP_Text` in the `EventPageContentController` hierarchy with these filters:
- Skip text inside `CustomButton` or `CustomButtonWithTooltip` parent chain (button labels)
- Skip text inside GameObjects with "Objective" in name (progress milestones)
- Skip text shorter than 5 characters
- Skip blocks that are redundant with the event title (fuzzy match: max 4 words, at least 1/3 of words shared with title, splitting on spaces/colons/hyphens/underscores)
- Long texts split on `\n` into separate blocks for screen reader readability

**Navigation state** in `GeneralMenuNavigator`:
- `_eventInfoBlocks` / `_eventInfoIndex` track current position
- Index `-1` = on the button (default), `0..N-1` = info blocks
- Down from button -> first info block; Down at end -> "End of list"
- Up from first info block -> back to button (re-announces current element); Up from button -> "Beginning of list"
- Tab bypasses info navigation, uses normal element navigation
- Blocks lazy-loaded on first Down press, cleared when controller changes in `PerformRescan()`

---

## Jump In Packet Selection

Jump In is a specific event where the player selects two card packets to build a deck. The packet selection screen is managed by `PacketSelectContentController`.

### Architecture

**Key game types (all in `Wotc.Mtga.Wrapper.PacketSelect` namespace):**
- `PacketSelectContentController` - Main controller, extends `NavContentController`
- `JumpStartPacket` - MonoBehaviour on each packet tile GO
- `PacketInput` - Click handler on same GO as JumpStartPacket (has `CustomTouchButton`)
- `PacketDetails` - Readonly struct: `Name`, `PacketId`, `LandGrpId` (uint), `ArtId`, `RawColors` (string[])
- `ServiceState` - Readonly struct: `SubmittedPackets`, `PacketOptions` (PacketDetails[]), `SubmissionCount()`

**Important internal state:**
- `_packetOptions` - List of JumpStartPacket MonoBehaviours
- `_packetToId` - Dictionary mapping JumpStartPacket to PacketId string
- `_selectedPackId` - Currently selected packet ID
- `_currentState` - ServiceState with submission progress and available options

### Navigation

**Screen detection:** `MenuScreenDetector` recognizes `PacketSelectContentController` and maps it to "Packet Selection" display name.

**Screen name enrichment:** Appends packet number from `EventAccessor.GetPacketScreenSummary()`, e.g., "Packet Selection, Packet 1 of 2".

**Element navigation (Up/Down):**
Packets are navigated as grouped elements via the standard grouped navigation system. Up/Down arrows move between packet tiles (same pattern as other grouped content).

**Info block navigation (Left/Right):**
When focused on a packet tile, Left/Right arrows cycle through info blocks built by `EventAccessor.GetPacketInfoBlocks()`:
- Block 1: Packet name (from `_packTitle` Localize component)
- Block 2: Colors (from `PacketDetails.RawColors`, translated to readable names)
- Block 3+: Featured card info (from `PacketDetails.LandGrpId` via `CardModelProvider.GetCardInfoFromGrpId`)
- Remaining: Description text (long TMP_Text elements from controller, excluding those inside packet tiles)

State is managed by `_packetBlocks` / `_packetBlockIndex` in `GeneralMenuNavigator`, refreshed each time the grouped navigator moves to a new packet element.

**Text enrichment:** `UITextExtractor.TryGetPacketLabel` detects packet elements by walking the parent chain for `JumpStartPacket` component, then calls `EventAccessor.GetPacketLabel` which returns "{name} ({colors})".

### Activation

**Packet selection (Enter on a packet tile):**
`GeneralMenuNavigator.ActivateCurrentElement()` detects packet context and calls `EventAccessor.ClickPacket()` instead of `UIActivator.Activate()`. This is necessary because:
- The navigable element is `MainButton` (child GO)
- The `CustomTouchButton` and `PacketInput` are on the parent `JumpStartPacket` GO
- `UIActivator`'s pointer simulation on MainButton doesn't reach the click handler on the parent

`ClickPacket` finds the `PacketInput` component via parent walk, then invokes its private `OnClick()` method via reflection, which fires `Clicked?.Invoke(_pack)`.

**Confirm button:**
The confirm button is a standard `CustomButton` and works through normal `UIActivator.Activate()` in `OnElementActivated`.

### Rescan After Activation

After both packet click and confirm button activation, `TriggerRescan()` is called. This is essential because:
- The game processes packet selection/confirmation asynchronously via `UXEventQueue`
- After submission, `OnStateUpdated` fires -> `SetServiceState` destroys old packet GOs and creates new ones
- No panel open/close event fires since `PacketSelectContentController` stays active
- The 0.5s delayed rescan picks up the new GOs

### Reflection Access (EventAccessor)

All packet data access goes through `EventAccessor` (static class, follows `RecentPlayAccessor` pattern):
- Caches `FieldInfo`/`MethodInfo` on first access
- Caches controller reference (`_cachedPacketController`), validated on each call
- `ClearCache()` called on scene changes

**Cached reflection targets:**
- `PacketSelectContentController._packetOptions` (List)
- `PacketSelectContentController._selectedPackId` (string)
- `PacketSelectContentController._currentState` (ServiceState)
- `PacketSelectContentController._packetToId` (Dictionary)
- `PacketSelectContentController._headerText` (Localize)
- `JumpStartPacket._packTitle` (Localize)

---

## Draft Card Picking

When a user activates a draft event (e.g., Quick Draft), the game opens `DraftContentController` within the HomePage scene. The draft pick screen shows cards from a pack that the player must choose from.

### Architecture

**Key game types (in Core.dll):**
- `Wotc.Mtga.Wrapper.Draft.DraftContentController` - Main controller, extends `NavContentController`. Manages both the picking phase (showing packs) and the deck building phase (after all packs done). Has `IsOpen` property.
- `DraftPackHolder` - Extends `MetaCardHolder`, holds pack cards on `DraftableCards_CONTAINER`
- `DraftPackCardView` - Extends `CDCMetaCardView`, individual card view in draft pack. Has `CurrentCard` property and `undoPickButtonObject`.
- `DraftDeckManager` - Non-MonoBehaviour class tracking reserved/selected cards (`_reservedCards`, `_lockedReservedCards`)
- `Wotc.Mtga.Wrapper.Draft.DraftDeckView` - View for the deck being built, has `_confirmPickButton`, `_deckDetailsButton`, `ShowSideboardToggle`
- `DraftVaultProgress` - Simple MonoBehaviour with a single `CustomButton Button` field. One-time popup shown after first draft completion.

### Navigation (DraftNavigator)

**Screen detection:** `MenuScreenDetector` recognizes `DraftContentController` and maps it to "Draft" display name. `DraftNavigator` (priority 78) takes over from `GeneralMenuNavigator` when the draft content controller is active and has pack cards present.

**Picking mode detection:** `DetectScreen()` checks for active `DraftContentController` with `IsOpen == true`, AND verifies `DraftPackHolder` or `DraftPackCardView` components exist in the hierarchy. If no pack cards are found, returns false (assumes deck building mode, handled by `GeneralMenuNavigator`).

**Screen name:** "Draft Pick" or "Draft Pick, X cards" when card count is known. "Draft, Popup" when a popup overlay is active.

### Card Discovery

Cards are discovered by scanning `DraftContentController` children for `DraftPackCardView` components directly (not via `DraftPackHolder.CardViews` property, which may be empty during async loading). This is the same pattern used by `BoosterOpenNavigator` for `BoosterMetaCardView`. Cards are sorted by x-position (left to right).

**Card recognition:** `DraftPackCardView` is registered in:
- `CardDetector.IsCardInternal()` - name check for "DraftPackCardView"
- `CardModelProvider.GetMetaCardView()` - type name check for "DraftPackCardView"

This enables Up/Down card detail reading via `CardInfoNavigator`.

**Card name extraction:** Uses same "Title" `TMP_Text` pattern as `BoosterOpenNavigator`.

### Navigation Keys

- Left/Right (or A/D): Navigate between cards
- Home/End: Jump to first/last card
- Tab/Shift+Tab: Navigate cards
- Up/Down: Card details (via CardInfoNavigator)
- Enter: Select/toggle a card for picking (bypasses `ActivateCurrentElement()` to avoid CardInfoNavigator redirect, calls `UIActivator.Activate()` directly)
- Space: Confirm selection (clicks confirm button)
- Backspace: Back/exit
- F11: Debug dump current card

**Enter key bypass:** `BaseNavigator.ActivateCurrentElement()` detects cards and redirects to `CardInfoNavigator` for detail display. In draft, Enter must click the card to select it. `DraftNavigator.HandleInput()` calls `UIActivator.Activate()` directly for cards, bypassing the base class redirect.

### Rescan and Timing

**Initial rescan:** On activation, `_initialRescanDone = false` triggers a delayed rescan at 90 frames (~1.5 seconds). This catches cards that are still loading/animating when the navigator first activates.

**Action rescan:** After Enter (card selection) or Space (confirmation), `_rescanPending = true` triggers another delayed rescan to pick up pack changes (new pack, fewer cards, card count change).

**Deactivation timeout:** When 0 cards and no popup for 300 frames (~5 seconds), re-checks `DetectScreen()`. If draft picking is no longer active (pack holder gone after finalize), deactivates to let `GeneralMenuNavigator` handle the deck builder.

### Popup Handling

After the last card is picked and confirmed, the game's `Coroutine_FinalizeDraft()` runs:
1. `CompleteDraft()` call
2. Wait for inventory update (5 second timeout)
3. Show `DraftVaultProgressPopup` (one-time per account, via `MDNPlayerPrefs`)
4. Show gem rewards panel (if gems awarded)
5. Navigate to deck builder

**Detection:** Follows the same event-based pattern as `MasteryNavigator`. Subscribes to `PanelStateManager.OnPanelChanged` in `OnActivated()`, unsubscribes in `OnDeactivating()`. When a popup panel is detected (name contains "Popup", "SystemMessageView", or "Dialog"), enters popup mode.

**Element discovery** (`DiscoverPopupElements`): Finds `CustomButton`, `CustomButtonWithTooltip`, and Unity `Button` components in the popup hierarchy. Sorts by visual position (top-to-bottom, left-to-right). Also extracts text blocks for Up/Down reading.

**Text blocks** (`GetPopupInfoBlocks`): Same pattern as `EventAccessor.GetEventPageInfoBlocks()`. Scans all `TMP_Text` in popup, filters out button text (walks parent chain for `CustomButton`/`CustomButtonWithTooltip`), splits on newlines, skips text shorter than 3 characters, deduplicates.

**Popup announcement:** "Popup: {first text block}. Up and Down for {N} items" followed by "1 of {N}: {button label}".

**Popup navigation:**
- Tab/Shift+Tab, Left/Right: Navigate between popup buttons with wrapping ("X of N: label")
- Up/Down: Read text blocks (beginning/end of list at boundaries)
- Enter/Space: Activate current popup button (`Strings.Activating()`, `InputManager.ConsumeKey()`)
- Backspace: Dismiss/cancel popup (`Strings.Cancelled`, clicks first button)

**Popup cleanup:** When popup becomes invalid (destroyed/deactivated), clears popup state and triggers rescan to pick up whatever is on screen next.

### State Transitions

```
Draft Event Opened
  -> DraftContentController.IsOpen = true, DraftPackHolder present
  -> DraftNavigator activates (priority 78)
  -> Cards discovered, initial rescan at ~1.5s

Card Picking (repeat for each pack)
  -> Enter: select card, rescan picks up changes
  -> Space: confirm pick, rescan picks up new pack

Last Card Picked + Confirmed
  -> Rescan finds 0 cards, confirm button still present
  -> Finalize coroutine starts

Popup Appears (DraftVaultProgressPopup, first time only)
  -> PanelStateManager.OnPanelChanged fires
  -> DraftNavigator enters popup mode
  -> Text blocks + dismiss button navigable
  -> User dismisses popup

Popup Dismissed
  -> Popup cleared, rescan triggered
  -> If more popups (gem rewards): handled same way
  -> If no popup + 0 cards for ~5s: DetectScreen() returns false
  -> DraftNavigator deactivates

Deck Builder Opens
  -> DraftPackHolder gone, DraftDeckView active
  -> GeneralMenuNavigator handles deck builder
```

---

## Files

- `src/Core/Services/EventAccessor.cs` - All reflection-based event/packet data access
- `src/Core/Services/UITextExtractor.cs` - `TryGetEventTileLabel`, `TryGetPacketLabel`
- `src/Core/Services/MenuScreenDetector.cs` - Screen detection for EventPage, PacketSelect, Draft
- `src/Core/Services/GeneralMenuNavigator.cs` - Packet navigation, info blocks, activation, rescan
- `src/Core/Services/DraftNavigator.cs` - Draft card picking navigator with popup handling
- `src/Core/Services/CardDetector.cs` - Card detection (includes `DraftPackCardView` recognition)
- `src/Core/Services/CardModelProvider.cs` - Card model extraction (includes `DraftPackCardView`)
- `src/Core/Models/Strings.cs` - Localized strings for event/packet/draft labels
