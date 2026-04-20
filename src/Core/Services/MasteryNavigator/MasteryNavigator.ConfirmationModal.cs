using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Models;
using System;
using System.Collections.Generic;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    public partial class MasteryNavigator
    {
        #region Confirmation Modal State

        // Confirmation modal custom handling (follows StoreNavigator pattern)
        private GameObject _confirmationModalGameObject;     // Cached modal GO for polling
        private bool _wasModalOpen;
        private bool _isConfirmationModalActive;
        private MonoBehaviour _confirmationModalMb;
        private List<(GameObject obj, string label)> _modalElements = new List<(GameObject, string)>();
        private int _modalElementIndex;

        #endregion

        #region Confirmation Modal Reflection

        private Type _confirmationModalType;
        private static readonly string[] ModalPurchaseButtonFields = new[]
        {
            "_buttonGemPurchase", "_buttonCoinPurchase", "_buttonCashPurchase", "_buttonFreePurchase"
        };
        private FieldInfo[] _modalButtonFields;
        private Type _modalPurchaseButtonType;
        private FieldInfo _modalPbButtonField;     // Button (CustomButton)
        private FieldInfo _modalPbLabelField;      // Label (TMP_Text)
        private MethodInfo _modalCloseMethod;
        private FieldInfo _modalLabelField;        // _label (Localize) for item name
        private FieldInfo _modalItemContainerField;  // _storeItemContainer (Transform) for card preview
        private FieldInfo _modalProductListField;    // _productListContainer (Transform) for card tiles
        private bool _modalReflectionCached;

        #endregion

        #region Reflection Caching

        private void EnsureConfirmationModalReflectionCached()
        {
            if (_modalReflectionCached) return;

            _confirmationModalType = FindType("StoreConfirmationModal");
            if (_confirmationModalType != null)
            {
                var flags = AllInstanceFlags;
                _modalButtonFields = new FieldInfo[ModalPurchaseButtonFields.Length];
                for (int i = 0; i < ModalPurchaseButtonFields.Length; i++)
                    _modalButtonFields[i] = _confirmationModalType.GetField(ModalPurchaseButtonFields[i], flags);

                _modalCloseMethod = _confirmationModalType.GetMethod("Close", PublicInstance);
                _modalLabelField = _confirmationModalType.GetField("_label", flags);
                _modalItemContainerField = _confirmationModalType.GetField("_storeItemContainer", flags);
                _modalProductListField = _confirmationModalType.GetField("_productListContainer", flags);

                if (_modalButtonFields[0] != null)
                {
                    _modalPurchaseButtonType = _modalButtonFields[0].FieldType;
                    _modalPbButtonField = _modalPurchaseButtonType.GetField("Button", flags);
                    _modalPbLabelField = _modalPurchaseButtonType.GetField("Label", flags);
                }
            }

            _modalReflectionCached = true;
            Log.Msg("Mastery", $"ConfirmationModal reflection cached. Type={_confirmationModalType != null}, " +
                $"Close={_modalCloseMethod != null}, PbButton={_modalPbButtonField != null}");
        }

        #endregion

        #region Modal Discovery & Announcement

        private MonoBehaviour GetConfirmationModalMb()
        {
            if (_prizeWallConfirmModalField == null || _prizeWallController == null) return null;
            try
            {
                return _prizeWallConfirmModalField.GetValue(_prizeWallController) as MonoBehaviour;
            }
            catch { return null; }
        }

        private void DiscoverConfirmationModalElements()
        {
            _modalElements.Clear();
            _modalElementIndex = 0;

            if (_confirmationModalMb == null) return;

            EnsureConfirmationModalReflectionCached();
            if (_modalButtonFields == null) return;

            // Phase 1: Text blocks from known content containers only.
            // StoreConfirmationModal has _storeItemContainer (reparented item widget with card
            // preview) and _productListContainer (card tiles for bundles). Scanning only these
            // avoids picking up structural UI text like the cancel label.
            var contentContainers = new List<Transform>();
            try
            {
                if (_modalItemContainerField != null)
                {
                    var c = _modalItemContainerField.GetValue(_confirmationModalMb) as Transform;
                    if (c != null && c.gameObject.activeInHierarchy) contentContainers.Add(c);
                }
                if (_modalProductListField != null)
                {
                    var c = _modalProductListField.GetValue(_confirmationModalMb) as Transform;
                    if (c != null && c.gameObject.activeInHierarchy) contentContainers.Add(c);
                }
            }
            catch { /* Reflection may fail on different game versions */ }

            var seenTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var container in contentContainers)
            {
                foreach (var tmp in container.GetComponentsInChildren<TMPro.TMP_Text>(true))
                {
                    if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;

                    string text = UITextExtractor.StripRichText(tmp.text)?.Trim();
                    if (string.IsNullOrEmpty(text) || text.Length < 3) continue;

                    var lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.Length < 3) continue;
                        if (!seenTexts.Add(trimmed)) continue;

                        _modalElements.Add((null, trimmed));
                    }
                }
            }

            // Phase 2: Purchase buttons
            foreach (var field in _modalButtonFields)
            {
                if (field == null) continue;
                try
                {
                    var buttonStruct = field.GetValue(_confirmationModalMb);
                    if (buttonStruct == null) continue;

                    var customButton = _modalPbButtonField?.GetValue(buttonStruct) as MonoBehaviour;
                    if (customButton == null || !customButton.gameObject.activeInHierarchy) continue;

                    // Check Interactable
                    var interactableProp = customButton.GetType().GetProperty("Interactable", AllInstanceFlags);
                    if (interactableProp != null && !(bool)interactableProp.GetValue(customButton)) continue;

                    var labelTmp = _modalPbLabelField?.GetValue(buttonStruct) as TMPro.TMP_Text;
                    string priceText = labelTmp?.text?.Trim() ?? "";

                    if (string.IsNullOrEmpty(priceText))
                        priceText = UITextExtractor.GetText(customButton.gameObject) ?? customButton.gameObject.name;

                    // Derive currency name from field name (icon-only in game, invisible to screen readers)
                    string currencyName = "";
                    string fieldName = field.Name ?? "";
                    if (fieldName.Contains("Gem")) currencyName = Strings.CurrencyGems;
                    else if (fieldName.Contains("Coin")) currencyName = Strings.CurrencyGold;
                    else if (fieldName.Contains("Free")) currencyName = Strings.PrizeWallSphereCost;

                    string buttonLabel = !string.IsNullOrEmpty(currencyName) && !string.IsNullOrEmpty(priceText)
                        ? $"{priceText} {currencyName}" : priceText;

                    _modalElements.Add((customButton.gameObject, $"{buttonLabel}, {Strings.RoleButton}"));
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            // Phase 3: Cancel option (virtual element with null GameObject)
            _modalElements.Add((null, Strings.PopupCancel));

            Log.Msg("Mastery", $"Found {_modalElements.Count} confirmation modal elements");
        }

        private void AnnounceConfirmationModal()
        {
            // Extract item name from modal's _label field
            string labelText = null;
            if (_confirmationModalMb != null && _modalLabelField != null)
            {
                try
                {
                    var localize = _modalLabelField.GetValue(_confirmationModalMb) as MonoBehaviour;
                    if (localize != null)
                    {
                        var tmp = localize.GetComponentInChildren<TMPro.TMP_Text>();
                        if (tmp != null && !string.IsNullOrEmpty(tmp.text?.Trim()))
                            labelText = tmp.text.Trim();
                    }
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            // Fallback to generic text extraction
            if (string.IsNullOrEmpty(labelText))
                labelText = UITextExtractor.GetText(_confirmationModalMb?.gameObject);

            string announcement = "Confirm purchase";
            if (!string.IsNullOrEmpty(labelText))
                announcement += $": {labelText}";
            announcement += $". {_modalElements.Count} options.";

            _announcer.AnnounceInterrupt(announcement);

            if (_modalElements.Count > 0)
            {
                _modalElementIndex = 0;
                _announcer.Announce(
                    Strings.ItemPositionOf(1, _modalElements.Count, _modalElements[0].label),
                    AnnouncementPriority.Normal);
            }
        }

        #endregion

        #region Input Handling (Modal)

        private void HandleConfirmationModalInput()
        {
            if (_modalElements.Count == 0) return;

            // Up/Down navigate modal elements
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveModalElement(-1);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveModalElement(1);
                return;
            }
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                MoveModalElement(shift ? -1 : 1);
                return;
            }

            // Enter/Space activates current element
            bool enterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            bool spacePressed = InputManager.GetKeyDownAndConsume(KeyCode.Space);
            if (enterPressed || spacePressed)
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
                if (_modalElementIndex >= 0 && _modalElementIndex < _modalElements.Count)
                {
                    var elem = _modalElements[_modalElementIndex];
                    Log.Msg("Mastery", $"Activating modal element: {elem.label}");
                    if (elem.obj == null && _modalElementIndex == _modalElements.Count - 1)
                    {
                        // Last element with null obj = synthetic cancel option
                        DismissConfirmationModal();
                    }
                    else if (elem.obj != null)
                    {
                        // Purchase button
                        UIActivator.Activate(elem.obj);
                    }
                    // else: text block — Enter re-reads it (no action)
                }
                return;
            }

            // Backspace/Escape dismisses modal
            if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Escape))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                InputManager.ConsumeKey(KeyCode.Escape);
                DismissConfirmationModal();
                return;
            }
        }

        private void MoveModalElement(int direction)
        {
            int newIndex = _modalElementIndex + direction;
            if (newIndex < 0)
            {
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }
            if (newIndex >= _modalElements.Count)
            {
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }
            _modalElementIndex = newIndex;
            _announcer.AnnounceInterrupt(
                Strings.ItemPositionOf(_modalElementIndex + 1, _modalElements.Count, _modalElements[_modalElementIndex].label));
        }

        private void DismissConfirmationModal()
        {
            if (_confirmationModalMb != null && _modalCloseMethod != null)
            {
                try
                {
                    Log.Msg("Mastery", "Closing confirmation modal via Close()");
                    _modalCloseMethod.Invoke(_confirmationModalMb, null);
                    _announcer.Announce(Strings.Cancelled, AnnouncementPriority.High);
                    return;
                }
                catch (Exception ex)
                {
                    Log.Msg("Mastery", $"Error calling Close(): {ex.Message}");
                }
            }

            Log.Msg("Mastery", "Could not close confirmation modal");
        }

        #endregion
    }
}
