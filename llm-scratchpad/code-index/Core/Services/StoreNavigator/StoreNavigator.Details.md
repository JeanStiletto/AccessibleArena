# StoreNavigator.Details.cs
Path: src/Core/Services/StoreNavigator/StoreNavigator.Details.cs
Lines: 499

## Top-level comments
- Feature partial for the item Details view (synthetic purchase option): extracts tooltip description + card list from StoreDisplayPreconDeck / StoreDisplayCardViewBundle, handles Left/Right card navigation, Up/Down info-block navigation (lazy-built via CardDetector.BuildInfoBlocks), Home/End, Tab, Enter/Space, Backspace, and I-key handoff to ExtendedInfoNavigator.

## public partial class StoreNavigator (line 11)

### Fields
(no fields declared in this partial)

### Methods
- private bool HasItemDetails(MonoBehaviour storeItemBase) (line 15) — true if tooltip has mTerm or _itemDisplay is PreconDeck/CardViewBundle
- private bool HasTooltipText(MonoBehaviour storeItemBase) (line 39) — ignores "MainNav/General/Empty_String" placeholder
- private MonoBehaviour GetItemDisplay(MonoBehaviour storeItemBase) (line 58)
- private void OpenDetailsView(ItemInfo item) (line 72) — extracts description + card entries, announces "Details. {desc}. N cards. {first card}"
- private void CloseDetailsView() (line 112) — also closes ExtendedInfoNavigator if open
- private string ExtractTooltipDescription(MonoBehaviour storeItemBase) (line 132) — calls LocalizedString.ToString() to resolve mTerm
- private void ExtractCardEntries(MonoBehaviour storeItemBase, List<DetailCardEntry> entries) (line 167) — routes to PreconDeck or CardViewBundle extraction based on display type
- private void ExtractFromCardDataList(System.Collections.IList list, List<DetailCardEntry> entries) (line 201) — CardDataForTile list path
- private void ExtractFromBundleCardViews(System.Collections.IList viewList, List<DetailCardEntry> entries) (line 241) — StoreCardView.Card path
- private string FormatCardAnnouncement(DetailCardEntry card, int index) (line 286)
- private void HandleDetailsInput() (line 303) — delegates to ExtendedInfoNavigator when active; I key opens extended info
- private void MoveDetailsCard(int direction) (line 402) — resets block state when card changes
- private void MoveDetailsBlock(int direction) (line 431) — lazy-builds info blocks on first press via CardModelProvider.ExtractCardInfoFromObject / GetCardInfoFromGrpId + CardDetector.BuildInfoBlocks
- private void AnnounceDetailsBlock() (line 487) — respects VerboseAnnouncements setting for verbose-block labels
