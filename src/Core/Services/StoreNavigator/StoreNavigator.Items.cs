using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Models;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace AccessibleArena.Core.Services
{
    public partial class StoreNavigator
    {
        #region Item Discovery

        private void DiscoverItems()
        {
            _items.Clear();

            if (_controller == null || _storeItemBaseType == null) return;

            // Find all active StoreItemBase children of the controller
            var storeItems = _controllerGameObject.GetComponentsInChildren(_storeItemBaseType, false);

            foreach (var item in storeItems)
            {
                var mb = item as MonoBehaviour;
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                var itemInfo = ExtractItemInfo(mb);
                if (itemInfo.HasValue)
                {
                    _items.Add(itemInfo.Value);
                }
            }

            // Sort by sibling index for visual order
            _items.Sort((a, b) => a.GameObject.transform.GetSiblingIndex().CompareTo(b.GameObject.transform.GetSiblingIndex()));

            MelonLogger.Msg($"[Store] Discovered {_items.Count} items");
        }

        private ItemInfo? ExtractItemInfo(MonoBehaviour storeItemBase)
        {
            string label = ExtractItemLabel(storeItemBase);
            string description = ExtractItemDescription(storeItemBase);
            var purchaseOptions = ExtractPurchaseOptions(storeItemBase);
            bool hasDetails = HasItemDetails(storeItemBase);

            // Prepend synthetic "Details" option when item has details
            if (hasDetails)
            {
                purchaseOptions.Insert(0, new PurchaseOption
                {
                    ButtonObject = null,
                    PriceText = "Details",
                    CurrencyName = ""
                });
            }

            return new ItemInfo
            {
                StoreItemBase = storeItemBase,
                GameObject = storeItemBase.gameObject,
                Label = label,
                Description = description,
                PurchaseOptions = purchaseOptions,
                HasDetails = hasDetails
            };
        }

        private string ExtractItemLabel(MonoBehaviour storeItemBase)
        {
            // Delegate to UITextExtractor which centralizes all special-case text extraction
            string label = UITextExtractor.TryGetStoreItemLabel(storeItemBase.gameObject);
            if (!string.IsNullOrEmpty(label))
                return TruncateLabel(label);

            // Final fallback: cleaned GameObject name
            string name = storeItemBase.gameObject.name;
            if (name.StartsWith("StoreItem - "))
                name = name.Substring("StoreItem - ".Length);
            return name;
        }

        private string ExtractItemDescription(MonoBehaviour storeItemBase)
        {
            // Collect active text elements that aren't the label or purchase buttons.
            // Known useful elements: Tag_Badge (discount), Tag_Ribbon (discount),
            // Tag_Header (promo), Tag_Footer (promo), Text_Timer (time-limited),
            // Text_ItemLimit (purchase limit), Text_FeatureCallout (callout),
            // Item Description (if active), Text_PriceSlash (original price)
            var parts = new List<string>();

            foreach (var t in storeItemBase.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;

                string raw = t.text?.Trim();
                if (string.IsNullOrEmpty(raw) || raw.Length < 2) continue;

                // Strip rich text tags
                string text = UITextExtractor.StripRichText(raw).Trim();
                if (string.IsNullOrEmpty(text) || text.Length < 2) continue;

                string objName = t.gameObject.name;

                // Skip the item label (already announced separately)
                if (objName == "Text_ItemLabel") continue;

                // Skip purchase button prices (already announced as purchase options)
                bool isButton = false;
                var parent = t.transform.parent;
                while (parent != null && parent != storeItemBase.transform)
                {
                    if (parent.name.StartsWith("MainButton"))
                    {
                        isButton = true;
                        break;
                    }
                    parent = parent.parent;
                }
                if (isButton) continue;

                // Skip generic UI noise
                if (text == "?" || text == "!") continue;

                if (!parts.Contains(text))
                    parts.Add(text);
            }

            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        private List<PurchaseOption> ExtractPurchaseOptions(MonoBehaviour storeItemBase)
        {
            var options = new List<PurchaseOption>();

            AddPurchaseOption(options, storeItemBase, _blueButtonField, Strings.CurrencyGems);
            AddPurchaseOption(options, storeItemBase, _orangeButtonField, Strings.CurrencyGold);
            AddPurchaseOption(options, storeItemBase, _clearButtonField, "");
            AddPurchaseOption(options, storeItemBase, _greenButtonField, "");

            return options;
        }

        private void AddPurchaseOption(List<PurchaseOption> options, MonoBehaviour storeItemBase,
            FieldInfo buttonField, string currencyName)
        {
            if (buttonField == null || _purchaseButtonType == null) return;

            try
            {
                var buttonStruct = buttonField.GetValue(storeItemBase);
                if (buttonStruct == null) return;

                // Get the CustomButton from the PurchaseButton struct
                var customButton = _pbButtonField?.GetValue(buttonStruct);
                if (customButton == null) return;

                var buttonMb = customButton as MonoBehaviour;
                if (buttonMb == null || buttonMb.gameObject == null || !buttonMb.gameObject.activeInHierarchy)
                    return;

                // Get price text from button's TMP_Text child
                string priceText = "";
                var tmpText = buttonMb.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (tmpText != null)
                {
                    priceText = tmpText.text?.Trim() ?? "";
                }

                // Also check the ButtonContainer visibility
                var container = _pbContainerField?.GetValue(buttonStruct) as GameObject;
                if (container != null && !container.activeInHierarchy)
                    return;

                options.Add(new PurchaseOption
                {
                    ButtonObject = buttonMb.gameObject,
                    PriceText = priceText,
                    CurrencyName = currencyName
                });
            }
            catch { /* Reflection may fail on different game versions */ }
        }

        #endregion

        #region Item Announcements

        private string FormatItemAnnouncement(int index)
        {
            if (index < 0 || index >= _items.Count) return "";

            var item = _items[index];
            string optionText = "";

            if (item.PurchaseOptions.Count > 0)
            {
                _currentPurchaseOptionIndex = Math.Min(_currentPurchaseOptionIndex, item.PurchaseOptions.Count - 1);
                var option = item.PurchaseOptions[_currentPurchaseOptionIndex];
                optionText = $", {FormatPurchaseOption(option)}";

                if (item.PurchaseOptions.Count > 1)
                {
                    string pos = Strings.PositionOf(_currentPurchaseOptionIndex + 1, item.PurchaseOptions.Count);
                    if (pos != "") optionText += $", {pos}";
                }
            }

            string descText = !string.IsNullOrEmpty(item.Description) ? $". {item.Description}" : "";
            string itemPos = Strings.PositionOf(index + 1, _items.Count);
            return $"{item.Label}{descText}{optionText}" + (itemPos != "" ? $", {itemPos}" : "");
        }

        private string FormatPurchaseOption(PurchaseOption option)
        {
            // Synthetic Details option
            if (option.ButtonObject == null && option.PriceText == "Details")
                return "Details";
            // If currency name is empty (real money), just show the price
            if (string.IsNullOrEmpty(option.CurrencyName))
                return option.PriceText;
            return $"{option.PriceText} {option.CurrencyName}";
        }

        #endregion

        #region Item Navigation

        private void CyclePurchaseOption(int direction)
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];
            if (item.PurchaseOptions.Count <= 1)
            {
                _announcer.Announce(
                    direction > 0 ? Strings.EndOfList : Strings.BeginningOfList,
                    AnnouncementPriority.Normal);
                return;
            }

            int newIndex = _currentPurchaseOptionIndex + direction;

            if (newIndex < 0)
            {
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }

            if (newIndex >= item.PurchaseOptions.Count)
            {
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            _currentPurchaseOptionIndex = newIndex;

            // Announce just the purchase option
            var option = item.PurchaseOptions[_currentPurchaseOptionIndex];
            string pos = Strings.PositionOf(_currentPurchaseOptionIndex + 1, item.PurchaseOptions.Count);
            _announcer.AnnounceInterrupt(
                $"{FormatPurchaseOption(option)}" + (pos != "" ? $", {pos}" : ""));
        }

        private void ActivateCurrentPurchaseOption()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];
            if (item.PurchaseOptions.Count == 0)
            {
                _announcer.Announce(Strings.NoPurchaseOption, AnnouncementPriority.Normal);
                return;
            }

            if (_currentPurchaseOptionIndex < 0 || _currentPurchaseOptionIndex >= item.PurchaseOptions.Count)
                _currentPurchaseOptionIndex = 0;

            var option = item.PurchaseOptions[_currentPurchaseOptionIndex];

            // Synthetic Details option - open details view instead of purchasing
            if (option.ButtonObject == null && option.PriceText == "Details")
            {
                OpenDetailsView(item);
                return;
            }

            MelonLogger.Msg($"[Store] Activating purchase: {item.Label} - {option.PriceText} {option.CurrencyName}");

            UIActivator.Activate(option.ButtonObject);
        }

        #endregion
    }
}
