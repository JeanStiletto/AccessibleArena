# MasteryNavigator.PrizeWall.cs (partial)
Path: src/Core/Services/MasteryNavigator/MasteryNavigator.PrizeWall.cs
Lines: 543

## Top-level comments
- PrizeWall mode of MasteryNavigator (Spend Spheres content). Discovers purchasable items under the layout group, reads cost + owned status from purchase buttons, and activates the first active CustomButton when user presses Enter. Sets up `_confirmationModalGameObject` for the core Update-loop polling (see ConfirmationModal partial).

## public partial class MasteryNavigator (line 12)

### PrizeWall State
- private MonoBehaviour _prizeWallController (line 16)
- private GameObject _prizeWallGameObject (line 17)
- private List<PrizeWallItemData> _prizeWallItems (line 18)
- private int _prizeWallIndex (line 19)
- private string _sphereCount (line 20)
- private GameObject _prizeWallBackButton (line 21)

### Nested types
- private struct PrizeWallItemData (line 23) — Obj, Label, Cost, IsOwned

### PrizeWall Reflection
- Controller reflection (lines 35-44): _prizeWallControllerType, _prizeWallIsOpenProp, _prizeWallCurrencyField, _prizeWallBackButtonField, _prizeWallContentsField, _prizeWallLayoutGroupField, _prizeWallConfirmModalField, _prizeWallCurrencyType, _currencyQuantityField, _prizeWallReflectionInitialized
- StoreItemBase reflection (lines 47-55): _storeItemBaseType, _siBlueButtonField, _siOrangeButtonField, _siClearButtonField, _siGreenButtonField, _purchaseButtonType, _pbButtonField, _pbContainerField, _storeItemReflectionInitialized

### Screen Detection (PrizeWall)
- private MonoBehaviour FindPrizeWallController() (line 61)
- private bool IsPrizeWallOpen(MonoBehaviour controller) (line 81)

### Reflection Caching
- private void EnsurePrizeWallReflectionCached(Type controllerType) (line 101)
- private void EnsureStoreItemReflectionCached(Type storeItemType) (line 134)

### Element Discovery (PrizeWall)
- private void DiscoverPrizeWallItems() (line 163) — Note: reads sphere count, finds StoreItemBase children under `_storeButtonLayoutGroup`, sorts by Y-desc then X-asc, inserts virtual status item at index 0, caches `_confirmationModalGameObject` for modal polling
- private PrizeWallItemData ExtractPrizeWallItemData(MonoBehaviour storeItemBase) (line 293) — Note: iterates BlueButton/OrangeButton/ClearButton/GreenButton structs; item marked !IsOwned when an active purchase button is found; extracts Cost from button's TMP_Text

### Input Handling (PrizeWall)
- private void HandlePrizeWallInput() (line 386) — Note: delegates to HandleConfirmationModalInput when `_isConfirmationModalActive`; Up/Down/Tab navigate; Home/End jump; Enter ActivatePrizeWallItem; Backspace activates `_prizeWallBackButton` or NavigateToHome; F3/Ctrl+R re-announces
- private void AnnouncePrizeWallItem() (line 479)
- private void ActivatePrizeWallItem() (line 496) — Note: skips virtual status item (Obj==null) and already-owned items; otherwise activates first active CustomButton under the StoreItemBase
