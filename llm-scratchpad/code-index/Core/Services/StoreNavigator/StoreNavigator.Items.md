# StoreNavigator.Items.cs
Path: src/Core/Services/StoreNavigator/StoreNavigator.Items.cs
Lines: 296

## Top-level comments
- Feature partial for item-level navigation: discovers StoreItemBase children, extracts labels/descriptions/purchase options (with synthetic Details option), formats per-item announcements, and handles Left/Right option cycling and Enter activation.

## public partial class StoreNavigator (line 10)

### Fields
(no fields declared in this partial)

### Methods
- private void DiscoverItems() (line 14) — scans controller for active StoreItemBase children, sorted by sibling index
- private ItemInfo? ExtractItemInfo(MonoBehaviour storeItemBase) (line 41) — prepends synthetic "Details" purchase option when HasItemDetails returns true
- private string ExtractItemLabel(MonoBehaviour storeItemBase) (line 70) — delegates to UITextExtractor.TryGetStoreItemLabel; falls back to cleaned GameObject name
- private string ExtractItemDescription(MonoBehaviour storeItemBase) (line 84) — joins active TMP_Text children (badges, timers, callouts); skips label and MainButton-parented texts
- private List<PurchaseOption> ExtractPurchaseOptions(MonoBehaviour storeItemBase) (line 133) — reads Blue (Gems), Orange (Gold), Clear, Green buttons
- private void AddPurchaseOption(...) (line 145) — reads CustomButton from PurchaseButton struct, extracts price text from TMP_Text
- private string FormatItemAnnouncement(int index) (line 190)
- private string FormatPurchaseOption(PurchaseOption option) (line 215) — handles synthetic Details option and empty currency (real money)
- private void CyclePurchaseOption(int direction) (line 230) — announces beginning/end of list at boundaries
- private void ActivateCurrentPurchaseOption() (line 266) — opens details view for synthetic Details option, else UIActivator.Activate
