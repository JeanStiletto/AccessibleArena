# StoreNavigator.ConfirmationModal.cs
Path: src/Core/Services/StoreNavigator/StoreNavigator.ConfirmationModal.cs
Lines: 312

## Top-level comments
- Feature partial for the StoreConfirmationModal (purchase confirmation dialog). Custom modal handling (not base popup mode): discovers modal purchase buttons, announces label + product description, handles Up/Down/Tab navigation + Enter/Space/Backspace, warns Steam users before real-money purchases.

## public partial class StoreNavigator (line 10)

### Fields
(no fields declared in this partial)

### Methods
- private bool IsConfirmationModalOpen(MonoBehaviour controller) (line 14) — checks modal.gameObject.activeSelf
- private GameObject GetConfirmationModalGameObject() (line 35)
- private MonoBehaviour GetConfirmationModalMb() (line 50)
- private void DiscoverConfirmationModalElements() (line 64) — reads each _modalButtonFields entry, filters by Interactable, derives currency from field name, appends synthetic Cancel option
- private void AnnounceConfirmationModal() (line 124) — extracts _label + _productListContainer text; builds "Confirm purchase: {label}. {desc}. N options."
- private void HandleConfirmationModalInput() (line 208)
- private void MoveModalElement(int direction) (line 269)
- private void DismissConfirmationModal() (line 290) — calls Close() on modal via reflection, announces Cancelled
