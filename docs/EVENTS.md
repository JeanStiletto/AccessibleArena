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
- `DraftContentController` - Main controller, extends `NavContentController`
- `DraftPackHolder` - Extends `MetaCardHolder`, holds pack cards
- `DraftPackCardView` - Extends `CDCMetaCardView`, individual card view in draft pack
- `DraftDeckManager` - Tracks which cards are reserved/selected for picking
- `DraftColumnMetaCardHolder` - Extends `StaticColumnManager`, holds deck cards being built

### Navigation (DraftNavigator)

**Screen detection:** `MenuScreenDetector` recognizes `DraftContentController` and maps it to "Draft" display name. `DraftNavigator` (priority 78) takes over from `GeneralMenuNavigator` when the draft content controller is active.

**Screen name:** "Draft Pick" or "Draft Pick, X cards" when card count is known.

**Card discovery:** Searches `DraftContentController` children for `DraftPackCardView` components. Falls back to `CDCMetaCardView`/`MetaCardView` within `DraftPackHolder` containers. Cards sorted by x-position (left to right).

**Card name extraction:** Uses same "Title" TMP_Text pattern as `BoosterOpenNavigator`.

**Navigation keys:**
- Left/Right (or A/D): Navigate between cards
- Home/End: Jump to first/last card
- Tab/Shift+Tab: Navigate cards
- Up/Down: Card details (via CardInfoNavigator)
- Enter: Select/toggle a card for picking (clicks the card)
- Space: Confirm selection (clicks confirm button)
- Backspace: Back/exit
- F11: Debug dump current card

**Rescan:** After Enter (card selection) or Space (confirmation), a delayed rescan (~1.5 seconds) picks up pack changes (new pack, fewer cards, etc.).

---

## Files

- `src/Core/Services/EventAccessor.cs` - All reflection-based event/packet data access
- `src/Core/Services/UITextExtractor.cs` - `TryGetEventTileLabel`, `TryGetPacketLabel`
- `src/Core/Services/MenuScreenDetector.cs` - Screen detection for EventPage, PacketSelect
- `src/Core/Services/GeneralMenuNavigator.cs` - Packet navigation, info blocks, activation, rescan
- `src/Core/Services/DraftNavigator.cs` - Draft card picking navigator
- `src/Core/Models/Strings.cs` - Localized strings for event/packet/draft labels
