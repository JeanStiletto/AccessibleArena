# GeneralMenuNavigator.Collection.cs
Path: src/Core/Services/GeneralMenuNavigator/GeneralMenuNavigator.Collection.cs
Lines: 318

## Top-level comments
- Partial class hosting Collection/Deck Builder card-grid paging, filter activation by number key, and packet-selection info-block sub-navigation (Jump In).

## public partial class GeneralMenuNavigator (line 22)
### Fields
- private int _pendingPageRescanFrames (line 25)
- private List<CardInfoBlock> _packetBlocks (line 29)
- private int _packetBlockIndex (line 30)

### Methods
- private bool ActivateCollectionPageButton(bool next) (line 36) — Note: uses CardPoolAccessor.ScrollNext/ScrollPrevious; consumes input during in-progress scroll animation; announces "Page X of Y" and saves group state for rescan restore
- private void SchedulePageRescan() (line 89) — Note: 8-frame safety floor with IsScrolling() short-circuit in Update()
- private bool ActivateCollectionPageButtonFallback(bool next) (line 100)
- private bool IsInCollectionCardContext() (line 154) — Note: checks grouped DeckBuilderCollection/Sideboard/DeckList groups, or falls back to PoolHolder ancestry check for ungrouped mode
- private bool IsInPacketSelectionContext() (line 187)
- private bool ActivateFilterByIndex(int index) (line 206) — Note: 1-9 activates options 1-9, 0 activates option 10; reports inverted toggle state since activation hasn't run yet when we read isOn
- private void RefreshPacketBlocks(GameObject element) (line 264)
- private void HandlePacketBlockNavigation(bool isRight) (line 274)
- private void AnnouncePacketBlock() (line 309)
