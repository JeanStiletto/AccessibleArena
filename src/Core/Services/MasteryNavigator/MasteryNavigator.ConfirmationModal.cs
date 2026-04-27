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

        private static readonly string[] ModalPurchaseButtonFields = new[]
        {
            "_buttonGemPurchase", "_buttonCoinPurchase", "_buttonCashPurchase", "_buttonFreePurchase"
        };

        private sealed class ModalHandles
        {
            public FieldInfo[] ButtonFields;       // indexed by ModalPurchaseButtonFields
            public MethodInfo CloseMethod;         // Close()
            public FieldInfo Label;                // _label (Localize)
            public FieldInfo ItemContainer;        // _storeItemContainer (Transform)
            public FieldInfo ProductList;          // _productListContainer (Transform)
            public FieldInfo PbButton;             // PurchaseButton.Button (CustomButton)
            public FieldInfo PbLabel;              // PurchaseButton.Label (TMP_Text)
        }

        private readonly ReflectionCache<ModalHandles> _modalCache = new ReflectionCache<ModalHandles>(
            builder: modalType =>
            {
                var h = new ModalHandles
                {
                    ButtonFields = new FieldInfo[ModalPurchaseButtonFields.Length],
                    CloseMethod = modalType.GetMethod("Close", PublicInstance),
                    Label = modalType.GetField("_label", AllInstanceFlags),
                    ItemContainer = modalType.GetField("_storeItemContainer", AllInstanceFlags),
                    ProductList = modalType.GetField("_productListContainer", AllInstanceFlags),
                };
                for (int i = 0; i < ModalPurchaseButtonFields.Length; i++)
                    h.ButtonFields[i] = modalType.GetField(ModalPurchaseButtonFields[i], AllInstanceFlags);

                if (h.ButtonFields[0] != null)
                {
                    var pbType = h.ButtonFields[0].FieldType;
                    h.PbButton = pbType.GetField("Button", AllInstanceFlags);
                    h.PbLabel = pbType.GetField("Label", AllInstanceFlags);
                }
                return h;
            },
            validator: _ => true,
            logTag: "Mastery",
            logSubject: "ConfirmationModal");

        #endregion

        #region Modal Discovery & Announcement

        private MonoBehaviour GetConfirmationModalMb()
        {
            var confirmModalField = _prizeWallCache.Handles?.ConfirmModal;
            if (confirmModalField == null || _prizeWallController == null) return null;
            try
            {
                return confirmModalField.GetValue(_prizeWallController) as MonoBehaviour;
            }
            catch { return null; }
        }

        private void DiscoverConfirmationModalElements()
        {
            _modalElements.Clear();
            _modalElementIndex = 0;

            if (_confirmationModalMb == null) return;

            var modalType = FindType("StoreConfirmationModal");
            if (modalType != null) _modalCache.EnsureInitialized(modalType);
            var m = _modalCache.Handles;
            if (m?.ButtonFields == null) return;

            // Phase 1: Text blocks from known content containers only.
            // StoreConfirmationModal has _storeItemContainer (reparented item widget with card
            // preview) and _productListContainer (card tiles for bundles). Scanning only these
            // avoids picking up structural UI text like the cancel label.
            var contentContainers = new List<Transform>();
            try
            {
                if (m.ItemContainer != null)
                {
                    var c = m.ItemContainer.GetValue(_confirmationModalMb) as Transform;
                    if (c != null && c.gameObject.activeInHierarchy) contentContainers.Add(c);
                }
                if (m.ProductList != null)
                {
                    var c = m.ProductList.GetValue(_confirmationModalMb) as Transform;
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
            foreach (var field in m.ButtonFields)
            {
                if (field == null) continue;
                try
                {
                    var buttonStruct = field.GetValue(_confirmationModalMb);
                    if (buttonStruct == null) continue;

                    var customButton = m.PbButton?.GetValue(buttonStruct) as MonoBehaviour;
                    if (customButton == null || !customButton.gameObject.activeInHierarchy) continue;

                    // Check Interactable
                    var interactableProp = customButton.GetType().GetProperty("Interactable", AllInstanceFlags);
                    if (interactableProp != null && !(bool)interactableProp.GetValue(customButton)) continue;

                    var labelTmp = m.PbLabel?.GetValue(buttonStruct) as TMPro.TMP_Text;
                    string priceText = labelTmp?.text?.Trim() ?? "";

                    if (string.IsNullOrEmpty(priceText))
                        priceText = UITextExtractor.GetText(customButton.gameObject) ?? customButton.gameObject.name;

                    // Currency icon next to the price is invisible to screen readers — derive
                    // the currency name from the reflected field name and prepend it.
                    string currencyName = CurrencyLabels.FromFieldName(field.Name);
                    string buttonLabel = CurrencyLabels.FormatPrice(priceText, currencyName) ?? priceText;

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
            var labelField = _modalCache.Handles?.Label;
            if (_confirmationModalMb != null && labelField != null)
            {
                try
                {
                    var localize = labelField.GetValue(_confirmationModalMb) as MonoBehaviour;
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
            var closeMethod = _modalCache.Handles?.CloseMethod;
            if (_confirmationModalMb != null && closeMethod != null)
            {
                try
                {
                    Log.Msg("Mastery", "Closing confirmation modal via Close()");
                    closeMethod.Invoke(_confirmationModalMb, null);
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
