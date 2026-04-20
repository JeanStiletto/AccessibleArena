# MasteryNavigator.ConfirmationModal.cs (partial)
Path: src/Core/Services/MasteryNavigator/MasteryNavigator.ConfirmationModal.cs
Lines: 326

## Top-level comments
- Confirmation modal (StoreConfirmationModal) handling for PrizeWall purchases. Follows the StoreNavigator pattern: modal is reused (not re-instantiated) so PanelStateManager events don't fire reliably — instead, the core `Update` loop polls `_confirmationModalGameObject.activeInHierarchy` and calls `DiscoverConfirmationModalElements` / `AnnounceConfirmationModal` on open transition. This partial owns the modal state + reflection + discovery + input.

## public partial class MasteryNavigator (line 12)

### Confirmation Modal State
- private GameObject _confirmationModalGameObject (line 16) — Note: set by DiscoverPrizeWallItems (PrizeWall partial); polled in core Update loop
- private bool _wasModalOpen (line 17)
- private bool _isConfirmationModalActive (line 18)
- private MonoBehaviour _confirmationModalMb (line 19)
- private List<(GameObject obj, string label)> _modalElements (line 20)
- private int _modalElementIndex (line 21)

### Confirmation Modal Reflection
- private Type _confirmationModalType (line 27)
- private static readonly string[] ModalPurchaseButtonFields (line 28) — Note: _buttonGemPurchase, _buttonCoinPurchase, _buttonCashPurchase, _buttonFreePurchase
- private FieldInfo[] _modalButtonFields (line 32)
- private Type _modalPurchaseButtonType (line 33)
- private FieldInfo _modalPbButtonField (line 34)
- private FieldInfo _modalPbLabelField (line 35)
- private MethodInfo _modalCloseMethod (line 36)
- private FieldInfo _modalLabelField (line 37) — Note: _label (Localize) for item name
- private FieldInfo _modalItemContainerField (line 38) — Note: _storeItemContainer (Transform) for card preview
- private FieldInfo _modalProductListField (line 39) — Note: _productListContainer (Transform) for card tiles
- private bool _modalReflectionCached (line 40)

### Reflection Caching
- private void EnsureConfirmationModalReflectionCached() (line 46) — Note: one-shot init, looks up StoreConfirmationModal via FindType

### Modal Discovery & Announcement
- private MonoBehaviour GetConfirmationModalMb() (line 80) — Note: reads `_confirmationModal` field from PrizeWall controller
- private void DiscoverConfirmationModalElements() (line 90) — Note: Phase 1 scans `_storeItemContainer` + `_productListContainer` TMP_Texts (deduped); Phase 2 adds active purchase buttons with currency name derived from field name (Gem/Coin/Free → Gems/Gold/Sphere label); Phase 3 appends virtual Cancel option (null GameObject)
- private void AnnounceConfirmationModal() (line 185) — Note: resolves item name from `_label` Localize → TMP_Text; announces "Confirm purchase: <item>. N options."

### Input Handling (Modal)
- private void HandleConfirmationModalInput() (line 228) — Note: Up/Down/Tab MoveModalElement; Enter/Space activates (UIActivator.Activate for purchase button, DismissConfirmationModal for virtual cancel); Backspace/Escape dismisses
- private void MoveModalElement(int direction) (line 286) — Note: announces BeginningOfList/EndOfList at boundaries instead of wrapping
- private void DismissConfirmationModal() (line 304) — Note: invokes StoreConfirmationModal.Close() via cached reflection
