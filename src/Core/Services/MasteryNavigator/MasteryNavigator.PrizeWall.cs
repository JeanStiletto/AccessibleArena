using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    public partial class MasteryNavigator
    {
        #region PrizeWall State

        private MonoBehaviour _prizeWallController;
        private GameObject _prizeWallGameObject;
        private List<PrizeWallItemData> _prizeWallItems = new List<PrizeWallItemData>();
        private int _prizeWallIndex;
        private string _sphereCount;
        private GameObject _prizeWallBackButton;

        private struct PrizeWallItemData
        {
            public GameObject Obj;
            public string Label;       // Item name from UITextExtractor
            public string Cost;        // e.g., "2" (sphere count on button)
            public bool IsOwned;       // Button not interactable or item claimed
        }

        #endregion

        #region PrizeWall Reflection

        private Type _prizeWallControllerType;
        private PropertyInfo _prizeWallIsOpenProp;
        private FieldInfo _prizeWallCurrencyField;          // PrizeWallCurrency _currencyPrizeWall
        private FieldInfo _prizeWallBackButtonField;         // CustomButton _prizeWallBackButton
        private FieldInfo _prizeWallContentsField;           // GameObject _contentsContainer
        private FieldInfo _prizeWallLayoutGroupField;        // HorizontalLayoutGroup _storeButtonLayoutGroup
        private FieldInfo _prizeWallConfirmModalField;       // StoreConfirmationModal _confirmationModal
        private Type _prizeWallCurrencyType;
        private FieldInfo _currencyQuantityField;            // TMP_Text _currencyQuantity
        private bool _prizeWallReflectionInitialized;

        // StoreItemBase purchase button reflection (for extracting cost + owned status)
        private Type _storeItemBaseType;
        private FieldInfo _siBlueButtonField;     // BlueButton (PurchaseButton struct)
        private FieldInfo _siOrangeButtonField;   // OrangeButton
        private FieldInfo _siClearButtonField;    // ClearButton
        private FieldInfo _siGreenButtonField;    // GreenButton
        private Type _purchaseButtonType;
        private FieldInfo _pbButtonField;         // Button (CustomButton)
        private FieldInfo _pbContainerField;      // ButtonContainer (GameObject)
        private bool _storeItemReflectionInitialized;

        #endregion

        #region Screen Detection (PrizeWall)

        private MonoBehaviour FindPrizeWallController()
        {
            // Use cached reference if still valid
            if (_prizeWallController != null && _prizeWallController.gameObject != null &&
                _prizeWallController.gameObject.activeInHierarchy)
                return _prizeWallController;

            _prizeWallController = null;
            _prizeWallGameObject = null;

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == "ContentController_PrizeWall")
                    return mb;
            }

            return null;
        }

        private bool IsPrizeWallOpen(MonoBehaviour controller)
        {
            EnsurePrizeWallReflectionCached(controller.GetType());

            if (_prizeWallIsOpenProp != null)
            {
                try
                {
                    return (bool)_prizeWallIsOpenProp.GetValue(controller);
                }
                catch { return false; }
            }

            return true;
        }

        #endregion

        #region Reflection Caching

        private void EnsurePrizeWallReflectionCached(Type controllerType)
        {
            if (_prizeWallReflectionInitialized && _prizeWallControllerType == controllerType) return;

            _prizeWallControllerType = controllerType;
            var flags = AllInstanceFlags;

            // IsOpen from NavContentController base
            _prizeWallIsOpenProp = controllerType.GetProperty("IsOpen", flags | BindingFlags.FlattenHierarchy);

            // Key fields on ContentController_PrizeWall
            _prizeWallCurrencyField = controllerType.GetField("_currencyPrizeWall", flags);
            _prizeWallBackButtonField = controllerType.GetField("_prizeWallBackButton", flags);
            _prizeWallContentsField = controllerType.GetField("_contentsContainer", flags);
            _prizeWallLayoutGroupField = controllerType.GetField("_storeButtonLayoutGroup", flags);
            _prizeWallConfirmModalField = controllerType.GetField("_confirmationModal", flags);

            // PrizeWallCurrency type -> _currencyQuantity TMP field
            if (_prizeWallCurrencyField != null)
            {
                _prizeWallCurrencyType = _prizeWallCurrencyField.FieldType;
                _currencyQuantityField = _prizeWallCurrencyType?.GetField("_currencyQuantity", flags);
            }

            _prizeWallReflectionInitialized = true;
            MelonLogger.Msg($"[Mastery] PrizeWall reflection cached. Currency={_prizeWallCurrencyField != null}, " +
                $"BackButton={_prizeWallBackButtonField != null}, " +
                $"Contents={_prizeWallContentsField != null}, " +
                $"Layout={_prizeWallLayoutGroupField != null}, " +
                $"CurrencyQty={_currencyQuantityField != null}, " +
                $"ConfirmModal={_prizeWallConfirmModalField != null}");
        }

        private void EnsureStoreItemReflectionCached(Type storeItemType)
        {
            if (_storeItemReflectionInitialized && _storeItemBaseType == storeItemType) return;

            _storeItemBaseType = storeItemType;
            var flags = AllInstanceFlags;

            _siBlueButtonField = storeItemType.GetField("BlueButton", flags);
            _siOrangeButtonField = storeItemType.GetField("OrangeButton", flags);
            _siClearButtonField = storeItemType.GetField("ClearButton", flags);
            _siGreenButtonField = storeItemType.GetField("GreenButton", flags);

            // PurchaseButton struct type from BlueButton field
            if (_siBlueButtonField != null)
            {
                _purchaseButtonType = _siBlueButtonField.FieldType;
                _pbButtonField = _purchaseButtonType.GetField("Button", flags);
                _pbContainerField = _purchaseButtonType.GetField("ButtonContainer", flags);
            }

            _storeItemReflectionInitialized = true;
            MelonLogger.Msg($"[Mastery] StoreItemBase reflection cached. PurchaseButton={_purchaseButtonType != null}, " +
                $"PbButton={_pbButtonField != null}");
        }

        #endregion

        #region Element Discovery (PrizeWall)

        private void DiscoverPrizeWallItems()
        {
            _prizeWallItems.Clear();
            _prizeWallIndex = 0;
            _sphereCount = "0";
            _prizeWallBackButton = null;

            if (_prizeWallController == null) return;

            EnsurePrizeWallReflectionCached(_prizeWallController.GetType());

            // Get sphere count from PrizeWallCurrency._currencyQuantity
            if (_prizeWallCurrencyField != null && _currencyQuantityField != null)
            {
                try
                {
                    var currency = _prizeWallCurrencyField.GetValue(_prizeWallController);
                    if (currency != null)
                    {
                        var tmpText = _currencyQuantityField.GetValue(currency) as TMPro.TMP_Text;
                        if (tmpText != null && !string.IsNullOrEmpty(tmpText.text))
                            _sphereCount = tmpText.text.Trim();
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[Mastery] Error reading sphere count: {ex.Message}");
                }
            }

            // Get back button
            if (_prizeWallBackButtonField != null)
            {
                try
                {
                    var backBtn = _prizeWallBackButtonField.GetValue(_prizeWallController) as MonoBehaviour;
                    if (backBtn != null && backBtn.gameObject != null && backBtn.gameObject.activeInHierarchy)
                        _prizeWallBackButton = backBtn.gameObject;
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            // Find StoreItemBase components under the layout group (the purchasable items)
            Transform layoutParent = null;
            if (_prizeWallLayoutGroupField != null)
            {
                try
                {
                    var layoutGroup = _prizeWallLayoutGroupField.GetValue(_prizeWallController) as Component;
                    if (layoutGroup != null)
                        layoutParent = layoutGroup.transform;
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            // Fallback: search under contents container
            if (layoutParent == null && _prizeWallContentsField != null)
            {
                try
                {
                    var contents = _prizeWallContentsField.GetValue(_prizeWallController) as GameObject;
                    if (contents != null)
                        layoutParent = contents.transform;
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            if (layoutParent != null)
            {
                // Find all active StoreItemBase children - these are the purchasable items
                var discovered = new List<(PrizeWallItemData data, float sortOrder)>();

                foreach (var mb in layoutParent.GetComponentsInChildren<MonoBehaviour>(false))
                {
                    if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                    if (mb.GetType().Name != "StoreItemBase") continue;

                    EnsureStoreItemReflectionCached(mb.GetType());
                    var itemData = ExtractPrizeWallItemData(mb);

                    var pos = mb.transform.position;
                    // Sort by Y descending (top first), then X ascending (left first)
                    discovered.Add((itemData, -pos.y * 1000 + pos.x));
                }

                foreach (var (data, _) in discovered.OrderBy(x => x.sortOrder))
                {
                    _prizeWallItems.Add(data);
                }
            }

            // Insert virtual sphere status item at position 0
            _prizeWallItems.Insert(0, new PrizeWallItemData
            {
                Obj = null,
                Label = Strings.PrizeWallSphereStatus(_sphereCount)
            });

            // Cache confirmation modal GameObject for polling
            _confirmationModalGameObject = null;
            _wasModalOpen = false;
            if (_prizeWallConfirmModalField != null)
            {
                try
                {
                    var modal = _prizeWallConfirmModalField.GetValue(_prizeWallController) as MonoBehaviour;
                    if (modal != null)
                    {
                        _confirmationModalGameObject = modal.gameObject;
                        _wasModalOpen = modal.gameObject.activeInHierarchy;
                    }
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            if (_prizeWallItems.Count > 0 || _prizeWallGameObject != null)
            {
                // Add dummy element for BaseNavigator validation
                AddElement(_prizeWallGameObject ?? _prizeWallController.gameObject, "PrizeWall");
            }

            MelonLogger.Msg($"[Mastery] PrizeWall: {_prizeWallItems.Count} items (incl. status), spheres={_sphereCount}, " +
                $"backButton={_prizeWallBackButton != null}, modal={_confirmationModalGameObject != null}");
        }

        /// <summary>
        /// Extract rich item data from a StoreItemBase: label, cost, and owned status.
        /// Uses UITextExtractor.TryGetStoreItemLabel() for the name, then reads purchase
        /// buttons via reflection (same pattern as StoreNavigator).
        /// </summary>
        private PrizeWallItemData ExtractPrizeWallItemData(MonoBehaviour storeItemBase)
        {
            var data = new PrizeWallItemData { Obj = storeItemBase.gameObject };

            // 1. Get item name via centralized UITextExtractor
            string label = UITextExtractor.TryGetStoreItemLabel(storeItemBase.gameObject);
            if (string.IsNullOrEmpty(label))
            {
                label = UITextExtractor.GetText(storeItemBase.gameObject);
                if (string.IsNullOrEmpty(label))
                    label = storeItemBase.gameObject.name;
                label = UITextExtractor.StripRichText(label).Trim();
            }
            data.Label = label;

            // 2. Extract cost and owned status from purchase buttons
            data.Cost = null;
            data.IsOwned = true; // Assume owned unless we find an active purchase button

            FieldInfo[] buttonFields = { _siGreenButtonField, _siBlueButtonField, _siOrangeButtonField, _siClearButtonField };
            foreach (var bf in buttonFields)
            {
                if (bf == null || _purchaseButtonType == null) continue;

                try
                {
                    var buttonStruct = bf.GetValue(storeItemBase);
                    if (buttonStruct == null) continue;

                    // Check container visibility
                    if (_pbContainerField != null)
                    {
                        var container = _pbContainerField.GetValue(buttonStruct) as GameObject;
                        if (container != null && !container.activeInHierarchy)
                            continue;
                    }

                    // Get the CustomButton
                    var customButton = _pbButtonField?.GetValue(buttonStruct) as MonoBehaviour;
                    if (customButton == null || customButton.gameObject == null ||
                        !customButton.gameObject.activeInHierarchy)
                        continue;

                    // Found an active purchase button — item is NOT owned
                    data.IsOwned = false;

                    // Extract price text
                    if (data.Cost == null)
                    {
                        var tmpText = customButton.GetComponentInChildren<TMPro.TMP_Text>(true);
                        if (tmpText != null)
                        {
                            string price = UITextExtractor.StripRichText(tmpText.text)?.Trim();
                            if (!string.IsNullOrEmpty(price))
                                data.Cost = price;
                        }
                    }
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            // 3. Fallback: if no purchase button fields found, check for any CustomButton
            if (!_storeItemReflectionInitialized || _purchaseButtonType == null)
            {
                foreach (var mb in storeItemBase.GetComponentsInChildren<MonoBehaviour>(false))
                {
                    if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                    if (mb.GetType().Name != "CustomButton") continue;

                    // Found an active button — not owned
                    data.IsOwned = false;

                    if (data.Cost == null)
                    {
                        var tmpText = mb.GetComponentInChildren<TMPro.TMP_Text>(true);
                        if (tmpText != null)
                        {
                            string price = UITextExtractor.StripRichText(tmpText.text)?.Trim();
                            if (!string.IsNullOrEmpty(price))
                                data.Cost = price;
                        }
                    }
                    break;
                }
            }

            return data;
        }

        #endregion

        #region Input Handling (PrizeWall)

        private void HandlePrizeWallInput()
        {
            // Confirmation modal takes priority (custom handler, not base popup mode)
            if (_isConfirmationModalActive)
            {
                HandleConfirmationModalInput();
                return;
            }

            if (_prizeWallItems.Count == 0) return;

            // Up/Shift+Tab: Previous item
            if (Input.GetKeyDown(KeyCode.UpArrow) ||
                (Input.GetKeyDown(KeyCode.Tab) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))))
            {
                if (_prizeWallIndex > 0)
                {
                    _prizeWallIndex--;
                    AnnouncePrizeWallItem();
                }
                else
                {
                    _announcer.AnnounceInterruptVerbose(Strings.BeginningOfList);
                }
                return;
            }

            // Down/Tab: Next item
            if (Input.GetKeyDown(KeyCode.DownArrow) ||
                (Input.GetKeyDown(KeyCode.Tab) && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)))
            {
                if (_prizeWallIndex < _prizeWallItems.Count - 1)
                {
                    _prizeWallIndex++;
                    AnnouncePrizeWallItem();
                }
                else
                {
                    _announcer.AnnounceInterruptVerbose(Strings.EndOfList);
                }
                return;
            }

            // Home: Jump to first
            if (Input.GetKeyDown(KeyCode.Home))
            {
                _prizeWallIndex = 0;
                AnnouncePrizeWallItem();
                return;
            }

            // End: Jump to last
            if (Input.GetKeyDown(KeyCode.End))
            {
                _prizeWallIndex = _prizeWallItems.Count - 1;
                AnnouncePrizeWallItem();
                return;
            }

            // Enter: Activate selected item (find its purchase button)
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
                ActivatePrizeWallItem();
                return;
            }

            // Backspace: Go back (returns to mastery levels, or home if no back button)
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                if (_prizeWallBackButton != null)
                {
                    _announcer.AnnounceInterruptVerbose(Strings.NavigatingBack);
                    UIActivator.Activate(_prizeWallBackButton);
                }
                else
                {
                    NavigateToHome();
                }
                return;
            }

            // F3/Ctrl+R: Re-announce current position
            if (Input.GetKeyDown(KeyCode.F3) ||
                ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.R)))
            {
                AnnouncePrizeWallItem();
                return;
            }
        }

        private void AnnouncePrizeWallItem()
        {
            if (_prizeWallIndex < 0 || _prizeWallIndex >= _prizeWallItems.Count) return;

            var item = _prizeWallItems[_prizeWallIndex];

            // Build rich description: "Label, 2 spheres, owned"
            string description = item.Label;
            if (!string.IsNullOrEmpty(item.Cost))
                description += $", {item.Cost} {Strings.PrizeWallSphereCost}";
            if (item.IsOwned && item.Obj != null) // Obj null = virtual status item
                description += $", {Strings.ProfileItemOwned}";

            _announcer.AnnounceInterrupt(
                Strings.PrizeWallItem(_prizeWallIndex + 1, _prizeWallItems.Count, description));
        }

        private void ActivatePrizeWallItem()
        {
            if (_prizeWallIndex < 0 || _prizeWallIndex >= _prizeWallItems.Count) return;

            var item = _prizeWallItems[_prizeWallIndex];

            // Virtual status item (Obj=null) - just re-announce
            if (item.Obj == null)
            {
                AnnouncePrizeWallItem();
                return;
            }

            // Skip activation for already-owned items
            if (item.IsOwned)
            {
                _announcer.AnnounceInterrupt($"{item.Label}, {Strings.ProfileItemOwned}");
                return;
            }

            // Find the first active CustomButton under this StoreItemBase (the purchase button)
            GameObject buttonToClick = null;
            foreach (var mb in item.Obj.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == "CustomButton")
                {
                    buttonToClick = mb.gameObject;
                    break;
                }
            }

            if (buttonToClick != null)
            {
                _announcer.AnnounceInterrupt(Strings.Activating(item.Label));
                UIActivator.Activate(buttonToClick);
            }
            else
            {
                // Fallback: activate the item itself
                _announcer.AnnounceInterrupt(Strings.Activating(item.Label));
                UIActivator.Activate(item.Obj);
            }
        }

        #endregion
    }
}
