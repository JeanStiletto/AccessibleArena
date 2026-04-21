using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Models;
using System;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    public partial class StoreNavigator
    {
        #region Modal State Queries

        private bool IsConfirmationModalOpen(MonoBehaviour controller)
        {
            if (_storeCache.Handles.ConfirmationModal == null) return false;

            try
            {
                var modal = _storeCache.Handles.ConfirmationModal.GetValue(controller);
                if (modal == null) return false;

                // StoreConfirmationModal is a MonoBehaviour - check gameObject.activeSelf
                var modalMb = modal as MonoBehaviour;
                if (modalMb != null && modalMb.gameObject != null)
                {
                    return modalMb.gameObject.activeSelf;
                }
            }
            catch { /* Reflection may fail on different game versions */ }

            return false;
        }

        private GameObject GetConfirmationModalGameObject()
        {
            if (_storeCache.Handles.ConfirmationModal == null || _controller == null) return null;

            try
            {
                var modal = _storeCache.Handles.ConfirmationModal.GetValue(_controller) as MonoBehaviour;
                if (modal != null && modal.gameObject != null)
                    return modal.gameObject;
            }
            catch { /* Reflection may fail on different game versions */ }

            return null;
        }

        private MonoBehaviour GetConfirmationModalMb()
        {
            if (_storeCache.Handles.ConfirmationModal == null || _controller == null) return null;
            try
            {
                return _storeCache.Handles.ConfirmationModal.GetValue(_controller) as MonoBehaviour;
            }
            catch { return null; }
        }

        #endregion

        #region Modal Discovery & Announcement

        private void DiscoverConfirmationModalElements()
        {
            _modalElements.Clear();
            _modalElementIndex = 0;

            if (_confirmationModalMb == null || _storeCache.Handles.ModalButtonFields == null) return;

            Log.Msg("Store", $"Discovering confirmation modal elements");

            var flags = AllInstanceFlags;

            // Get the modal's own purchase buttons (not the reparented item widget's)
            foreach (var field in _storeCache.Handles.ModalButtonFields)
            {
                if (field == null) continue;
                try
                {
                    var buttonStruct = field.GetValue(_confirmationModalMb);
                    if (buttonStruct == null) continue;

                    // Get CustomButton from the struct
                    var customButton = _storeCache.Handles.ModalPbButton?.GetValue(buttonStruct) as MonoBehaviour;
                    if (customButton == null || !customButton.gameObject.activeInHierarchy) continue;

                    // Check Interactable property
                    var interactableProp = customButton.GetType().GetProperty("Interactable", flags);
                    if (interactableProp != null)
                    {
                        bool interactable = (bool)interactableProp.GetValue(customButton);
                        if (!interactable) continue;
                    }

                    // Get price text from Label (TMP_Text)
                    var labelTmp = _storeCache.Handles.ModalPbLabel?.GetValue(buttonStruct) as TMPro.TMP_Text;
                    string priceText = labelTmp?.text?.Trim() ?? "";

                    // Also try getting text from button children as fallback
                    if (string.IsNullOrEmpty(priceText))
                        priceText = UITextExtractor.GetText(customButton.gameObject) ?? customButton.gameObject.name;

                    // Derive currency name from field name (icon-only in game, invisible to screen readers)
                    string currencyName = "";
                    string fieldName = field.Name ?? "";
                    if (fieldName.Contains("Gem")) currencyName = Strings.CurrencyGems;
                    else if (fieldName.Contains("Coin")) currencyName = Strings.CurrencyGold;

                    string buttonLabel = !string.IsNullOrEmpty(currencyName) && !string.IsNullOrEmpty(priceText)
                        ? $"{priceText} {currencyName}" : priceText;

                    _modalElements.Add((customButton.gameObject, $"{buttonLabel}, {Strings.RoleButton}"));
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            // Add Cancel option
            _modalElements.Add((null, Strings.PopupCancel));

            Log.Msg("Store", $"Found {_modalElements.Count} confirmation modal elements");
        }

        private void AnnounceConfirmationModal()
        {
            // Extract text from the modal's label and product description
            string labelText = null;
            string descText = null;

            if (_confirmationModalMb != null)
            {
                var flags = AllInstanceFlags;

                // Get _label (Localize) -> TMP_Text
                var labelField = _storeCache.Handles.ConfirmationModalType?.GetField("_label", flags);
                if (labelField != null)
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

                // Get product list text from _productListContainer children
                var productField = _storeCache.Handles.ConfirmationModalType?.GetField("_productListContainer", flags);
                if (productField != null)
                {
                    try
                    {
                        var container = productField.GetValue(_confirmationModalMb) as Transform;
                        if (container != null && container.gameObject.activeInHierarchy)
                        {
                            var texts = container.GetComponentsInChildren<TMPro.TMP_Text>(false);
                            foreach (var t in texts)
                            {
                                string text = t.text?.Trim();
                                if (!string.IsNullOrEmpty(text) && text.Length > 3)
                                {
                                    text = UITextExtractor.StripRichText(text).Trim();
                                    if (text.Length > 3)
                                    {
                                        descText = text;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch { /* Reflection may fail on different game versions */ }
                }
            }

            // Fallback to generic text extraction
            if (string.IsNullOrEmpty(labelText))
                labelText = UITextExtractor.GetText(PopupGameObject ?? _confirmationModalMb?.gameObject);

            string announcement = "Confirm purchase";
            if (!string.IsNullOrEmpty(labelText))
                announcement += $": {labelText}";
            if (!string.IsNullOrEmpty(descText))
                announcement += $". {descText}";
            announcement += $". {_modalElements.Count} options.";

            _announcer.AnnounceInterrupt(announcement);

            if (_modalElements.Count > 0)
            {
                _modalElementIndex = 0;
                _announcer.Announce(Strings.ItemPositionOf(1, _modalElements.Count, _modalElements[0].label), AnnouncementPriority.Normal);
            }
        }

        #endregion

        #region Modal Input

        /// <summary>
        /// Handle input for the confirmation modal (special case with purchase buttons).
        /// Generic popups are handled by the base popup mode infrastructure.
        /// </summary>
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
                    Log.Msg("Store", $"Activating modal element: {elem.label}");
                    if (elem.obj == null)
                    {
                        // Synthetic cancel option
                        DismissConfirmationModal();
                    }
                    else
                    {
                        // Warn Steam users about inaccessible payment overlay for real-money purchases
                        if (SteamOverlayBlocker.IsSteam && elem.obj.name.StartsWith("Cash"))
                        {
                            Log.Msg("Store", "Steam real-money purchase — announcing overlay warning");
                            _announcer.Announce(Strings.SteamPurchaseWarning, AnnouncementPriority.Critical);
                        }
                        UIActivator.Activate(elem.obj);
                    }
                }
                return;
            }

            // Backspace dismisses modal
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
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

        /// <summary>
        /// Dismiss the confirmation modal by calling Close() directly.
        /// </summary>
        private void DismissConfirmationModal()
        {
            if (_confirmationModalMb != null && _storeCache.Handles.ModalClose != null)
            {
                try
                {
                    Log.Msg("Store", "Closing confirmation modal via Close()");
                    _storeCache.Handles.ModalClose.Invoke(_confirmationModalMb, null);
                    _announcer.Announce(Strings.Cancelled, AnnouncementPriority.High);
                    return;
                }
                catch (Exception ex)
                {
                    Log.Msg("Store", $"Error calling Close(): {ex.Message}");
                }
            }

            Log.Msg("Store", "Could not close confirmation modal");
        }

        #endregion
    }
}
