using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.PanelDetection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for the draft card picking screen.
    /// Detects DraftContentController and makes draft pack cards navigable.
    /// Cards are DraftPackCardView (extends CDCMetaCardView) inside a DraftPackHolder.
    /// Enter selects/toggles a card, Space confirms the pick.
    /// </summary>
    public class DraftNavigator : BaseNavigator
    {
        private GameObject _draftControllerObject;
        private int _totalCards;

        // Delayed rescan: initial activation + after card pick
        private bool _rescanPending;
        private int _rescanFrameCounter;
        private bool _initialRescanDone;
        private const int RescanDelayFrames = 90; // ~1.5 seconds at 60fps

        // Popup overlay handling (DraftVaultProgressPopup, gem rewards, etc.)
        private readonly PopupHandler _popupHandler;
        private bool _isPopupActive;

        public override string NavigatorId => "Draft";
        public override string ScreenName => GetScreenName();
        public override int Priority => 78; // Below BoosterOpen (80), above General (15)

        public DraftNavigator(IAnnouncementService announcer) : base(announcer)
        {
            _popupHandler = new PopupHandler("Draft", announcer);
        }

        private string GetScreenName()
        {
            if (_isPopupActive)
                return Strings.ScreenDraftPopup;
            if (_totalCards > 0)
                return Strings.ScreenDraftPickCount(_totalCards);
            return Strings.ScreenDraftPick;
        }

        protected override bool DetectScreen()
        {
            // Look for active DraftContentController
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                var type = mb.GetType();
                if (type.Name != "DraftContentController") continue;

                // Check IsOpen property
                var isOpenProp = type.GetProperty("IsOpen",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (isOpenProp != null && isOpenProp.PropertyType == typeof(bool))
                {
                    try
                    {
                        bool isOpen = (bool)isOpenProp.GetValue(mb);
                        if (isOpen)
                        {
                            // Verify we're in card picking mode (not deck building)
                            // by checking for DraftPackHolder or DraftPackCardView
                            bool hasPackCards = false;
                            foreach (var child in mb.gameObject.GetComponentsInChildren<MonoBehaviour>(false))
                            {
                                if (child == null) continue;
                                string childType = child.GetType().Name;
                                if (childType == "DraftPackHolder" || childType == "DraftPackCardView")
                                {
                                    hasPackCards = true;
                                    break;
                                }
                            }

                            if (!hasPackCards)
                            {
                                MelonLogger.Msg($"[{NavigatorId}] DraftContentController is open but no pack cards found (deck building mode?)");
                                _draftControllerObject = null;
                                return false;
                            }

                            _draftControllerObject = mb.gameObject;
                            return true;
                        }
                    }
                    catch { /* Ignore reflection errors */ }
                }
            }

            _draftControllerObject = null;
            return false;
        }

        protected override void DiscoverElements()
        {
            _totalCards = 0;
            var addedObjects = new HashSet<GameObject>();

            // Find cards in the draft pack
            FindDraftPackCards(addedObjects);

            // Find action buttons (confirm, deck, sideboard)
            FindActionButtons(addedObjects);
        }

        private void FindDraftPackCards(HashSet<GameObject> addedObjects)
        {
            if (_draftControllerObject == null) return;

            var cardEntries = new List<(GameObject obj, float sortOrder)>();

            // Scan the controller hierarchy for DraftPackCardView components directly
            foreach (var mb in _draftControllerObject.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name != "DraftPackCardView") continue;

                var cardObj = mb.gameObject;
                if (addedObjects.Contains(cardObj)) continue;

                float sortOrder = cardObj.transform.position.x;
                cardEntries.Add((cardObj, sortOrder));
                addedObjects.Add(cardObj);
            }

            // Sort cards by position (left to right)
            cardEntries = cardEntries.OrderBy(x => x.sortOrder).ToList();

            MelonLogger.Msg($"[{NavigatorId}] Found {cardEntries.Count} draft cards");

            // Add cards to navigation
            foreach (var (cardObj, _) in cardEntries)
            {
                string cardName = ExtractCardName(cardObj);
                var cardInfo = CardDetector.ExtractCardInfo(cardObj);

                string displayName = !string.IsNullOrEmpty(cardName) ? cardName :
                                     (cardInfo.IsValid ? cardInfo.Name : "Unknown card");

                string label = displayName;
                if (cardInfo.IsValid && !string.IsNullOrEmpty(cardInfo.TypeLine))
                {
                    label += $", {cardInfo.TypeLine}";
                }

                // Check if card is already selected/reserved
                string selectedStatus = GetCardSelectedStatus(cardObj);
                if (!string.IsNullOrEmpty(selectedStatus))
                {
                    label += $", {selectedStatus}";
                }

                AddElement(cardObj, label);
                _totalCards++;
            }

            MelonLogger.Msg($"[{NavigatorId}] Total: {_totalCards} cards");
        }

        private string ExtractCardName(GameObject cardObj)
        {
            // Try to find the Title text element (same pattern as BoosterOpenNavigator)
            var texts = cardObj.GetComponentsInChildren<TMPro.TMP_Text>(true);
            foreach (var text in texts)
            {
                if (text == null) continue;

                string objName = text.gameObject.name;
                if (objName == "Title")
                {
                    string content = text.text?.Trim();
                    if (!string.IsNullOrEmpty(content))
                    {
                        content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", "").Trim();
                        if (!string.IsNullOrEmpty(content))
                            return content;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Check if a card is selected/reserved for picking.
        /// </summary>
        private string GetCardSelectedStatus(GameObject cardObj)
        {
            foreach (Transform child in cardObj.GetComponentsInChildren<Transform>(true))
            {
                if (child == null) continue;
                string name = child.name.ToLowerInvariant();

                if ((name.Contains("select") || name.Contains("highlight") || name.Contains("glow") ||
                     name.Contains("check") || name.Contains("reserved")) &&
                    child.gameObject.activeInHierarchy)
                {
                    if (name.Contains("selectframe") || name.Contains("selected") ||
                        name.Contains("checkmark") || name.Contains("reserved"))
                    {
                        return "selected";
                    }
                }
            }

            return null;
        }

        private void FindActionButtons(HashSet<GameObject> addedObjects)
        {
            if (_draftControllerObject == null) return;

            foreach (var mb in _draftControllerObject.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                string typeName = mb.GetType().Name;
                if (typeName != "CustomButton" && typeName != "CustomButtonWithTooltip") continue;

                var button = mb.gameObject;
                if (addedObjects.Contains(button)) continue;

                string name = button.name;
                string buttonText = UITextExtractor.GetButtonText(button, null);

                if (name.Contains("Confirm") || name.Contains("MainButton_Play") ||
                    (!string.IsNullOrEmpty(buttonText) && (buttonText.Contains("bestätigen") ||
                     buttonText.Contains("Confirm") || buttonText.Contains("confirm"))))
                {
                    string label = !string.IsNullOrEmpty(buttonText) ? buttonText : "Confirm Selection";
                    AddElement(button, $"{label}, button");
                    addedObjects.Add(button);
                    MelonLogger.Msg($"[{NavigatorId}] Found confirm button: {name} -> {label}");
                }
            }
        }

        protected override string GetActivationAnnouncement()
        {
            string countInfo = _totalCards > 0 ? $" {_totalCards} cards." : "";
            return $"Draft Pick. Left and Right to navigate cards, Enter to select, Space to confirm.{countInfo}";
        }

        protected override void HandleInput()
        {
            // Handle custom input first (F1 help, etc.)
            if (HandleCustomInput()) return;

            // Popup mode: route all input through PopupHandler
            if (_isPopupActive)
            {
                _popupHandler.HandleInput();
                return;
            }

            // F11: Dump current card details for debugging
            if (Input.GetKeyDown(KeyCode.F11))
            {
                if (IsValidIndex && _elements[_currentIndex].GameObject != null)
                {
                    MenuDebugHelper.DumpCardDetails(NavigatorId, _elements[_currentIndex].GameObject, _announcer);
                }
                else
                {
                    _announcer?.Announce(Strings.NoCardToInspect, AnnouncementPriority.High);
                }
                return;
            }

            // Left/Right arrows for navigation between cards
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                MovePrevious();
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                MoveNext();
                return;
            }

            // Home/End for quick jump to first/last
            if (Input.GetKeyDown(KeyCode.Home))
            {
                MoveFirst();
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                MoveLast();
                return;
            }

            // Tab/Shift+Tab also navigates
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bool shiftTab = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (shiftTab)
                    MovePrevious();
                else
                    MoveNext();
                return;
            }

            // Enter selects/toggles a card (picks it for drafting)
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (IsValidIndex)
                {
                    var elem = _elements[_currentIndex];
                    MelonLogger.Msg($"[{NavigatorId}] Enter pressed on: {elem.GameObject?.name ?? "null"} (Label: {elem.Label})");

                    // Directly click the card via UIActivator (bypasses CardInfoNavigator redirect)
                    UIActivator.Activate(elem.GameObject);
                    _announcer?.Announce("Activated", AnnouncementPriority.Normal);

                    // Trigger a delayed rescan to pick up selection state changes
                    _rescanPending = true;
                    _rescanFrameCounter = 0;
                }
                return;
            }

            // Space confirms the current selection (clicks confirm button)
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ClickConfirmButton();
                return;
            }

            // Backspace to go back
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                MelonLogger.Msg($"[{NavigatorId}] Backspace pressed");
                ClickBackButton();
                return;
            }
        }

        /// <summary>
        /// Find and click the confirm/submit button.
        /// </summary>
        private void ClickConfirmButton()
        {
            foreach (var elem in _elements)
            {
                if (elem.GameObject == null) continue;
                string label = elem.Label?.ToLowerInvariant() ?? "";
                if (label.Contains("confirm") || label.Contains("bestätigen"))
                {
                    MelonLogger.Msg($"[{NavigatorId}] Clicking confirm button: {elem.GameObject.name}");
                    UIActivator.Activate(elem.GameObject);

                    _rescanPending = true;
                    _rescanFrameCounter = 0;
                    return;
                }
            }

            // Fallback: search all CustomButtons in draft area for confirm
            if (_draftControllerObject != null)
            {
                foreach (var mb in _draftControllerObject.GetComponentsInChildren<MonoBehaviour>(false))
                {
                    if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                    if (mb.GetType().Name != "CustomButton") continue;

                    string name = mb.gameObject.name;
                    string text = UITextExtractor.GetButtonText(mb.gameObject, null);
                    if (name.Contains("Confirm") || name.Contains("MainButton") ||
                        (!string.IsNullOrEmpty(text) && (text.Contains("bestätigen") || text.Contains("Confirm"))))
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Clicking confirm button (fallback): {name}");
                        UIActivator.Activate(mb.gameObject);
                        _rescanPending = true;
                        _rescanFrameCounter = 0;
                        return;
                    }
                }
            }

            _announcer?.Announce("No confirm button found", AnnouncementPriority.Normal);
        }

        /// <summary>
        /// Find and click a back/exit button to leave draft.
        /// </summary>
        private void ClickBackButton()
        {
            if (_draftControllerObject == null) return;

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name != "CustomButton") continue;

                string name = mb.gameObject.name;
                if (name.Contains("MainButtonOutline") || name.Contains("BackButton") ||
                    name.Contains("CloseButton"))
                {
                    string text = UITextExtractor.GetButtonText(mb.gameObject, null);
                    MelonLogger.Msg($"[{NavigatorId}] Clicking back button: {name} ({text})");
                    UIActivator.Activate(mb.gameObject);
                    TriggerCloseRescan();
                    return;
                }
            }
        }

        #region Delayed rescan

        public override void Update()
        {
            // Check if popup is still valid
            if (_isPopupActive && !_popupHandler.ValidatePopup())
            {
                MelonLogger.Msg($"[{NavigatorId}] Popup became invalid, returning to navigation");
                ClearPopupState();
            }

            // Initial rescan after activation (~1.5 seconds for cards to load)
            if (_isActive && !_initialRescanDone)
            {
                _rescanFrameCounter++;
                if (_rescanFrameCounter >= RescanDelayFrames)
                {
                    _initialRescanDone = true;
                    int oldCount = _totalCards;

                    MelonLogger.Msg($"[{NavigatorId}] Initial rescan (current count: {oldCount})");
                    ForceRescan();

                    if (_totalCards > oldCount)
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Found {_totalCards - oldCount} additional cards, {_totalCards} total");
                    }
                }
            }

            // Rescan after card selection or confirmation (~1.5 seconds)
            if (_isActive && _rescanPending)
            {
                _rescanFrameCounter++;
                if (_rescanFrameCounter >= RescanDelayFrames)
                {
                    _rescanPending = false;
                    int oldCount = _totalCards;

                    MelonLogger.Msg($"[{NavigatorId}] Rescanning after action (current count: {oldCount})");
                    ForceRescan();

                    if (_totalCards != oldCount)
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Card count changed: {oldCount} -> {_totalCards}");
                        if (_totalCards == 0)
                        {
                            MelonLogger.Msg($"[{NavigatorId}] No more cards - pack may be complete");
                        }
                    }
                }
            }

            // Deactivation check: if 0 cards and no popup for extended time, re-check screen
            if (_isActive && !_isPopupActive && _initialRescanDone && !_rescanPending && _totalCards == 0)
            {
                _emptyCardCounter++;
                if (_emptyCardCounter >= EmptyCardDeactivateFrames)
                {
                    _emptyCardCounter = 0;
                    if (!DetectScreen())
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Draft picking no longer active after timeout, deactivating");
                        Deactivate();
                        return;
                    }
                }
            }
            else
            {
                _emptyCardCounter = 0;
            }

            // Check for close after back button
            if (_isActive && _closeTriggered)
            {
                _closeRescanCounter++;
                if (_closeRescanCounter >= 60)
                {
                    _closeTriggered = false;
                    if (!DetectScreen())
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Draft screen closed, deactivating navigator");
                        Deactivate();
                    }
                    else
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Still on draft screen, rescanning");
                        ForceRescan();
                    }
                }
            }

            base.Update();
        }

        private bool _closeTriggered;
        private int _closeRescanCounter;
        private int _emptyCardCounter; // Frames with 0 cards and no popup
        private const int EmptyCardDeactivateFrames = 300; // ~5 seconds at 60fps

        protected override void OnActivated()
        {
            base.OnActivated();
            _initialRescanDone = false;
            _rescanFrameCounter = 0;
            _rescanPending = false;
            _closeTriggered = false;
            _closeRescanCounter = 0;
            _emptyCardCounter = 0;
            ClearPopupState();

            // Subscribe to panel changes for popup detection
            if (PanelStateManager.Instance != null)
                PanelStateManager.Instance.OnPanelChanged += OnPanelChanged;
        }

        protected override void OnDeactivating()
        {
            ClearPopupState();

            // Unsubscribe from panel changes
            if (PanelStateManager.Instance != null)
                PanelStateManager.Instance.OnPanelChanged -= OnPanelChanged;
        }

        private void TriggerCloseRescan()
        {
            _closeTriggered = true;
            _closeRescanCounter = 0;
        }

        #endregion

        #region Popup handling

        /// <summary>
        /// Handle panel changes from PanelStateManager - detect popups appearing on top of draft.
        /// </summary>
        private void OnPanelChanged(PanelInfo oldPanel, PanelInfo newPanel)
        {
            if (!_isActive) return;

            if (newPanel != null && PopupHandler.IsPopupPanel(newPanel))
            {
                MelonLogger.Msg($"[{NavigatorId}] Popup detected: {newPanel.Name}");
                _isPopupActive = true;
                _popupHandler.OnPopupDetected(newPanel.GameObject);
            }
            else if (_isPopupActive && newPanel == null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Popup closed, returning to navigation");
                ClearPopupState();
            }
        }

        private void ClearPopupState()
        {
            _isPopupActive = false;
            _popupHandler.Clear();

            // Rescan to see what's on screen now
            _rescanPending = true;
            _rescanFrameCounter = 0;
        }

        #endregion

        protected override bool ValidateElements()
        {
            if (_draftControllerObject == null || !_draftControllerObject.activeInHierarchy)
            {
                MelonLogger.Msg($"[{NavigatorId}] Draft controller no longer active");
                return false;
            }

            return base.ValidateElements();
        }

        public override void OnSceneChanged(string sceneName)
        {
            if (_isActive)
            {
                Deactivate();
            }
            _draftControllerObject = null;
        }
    }
}
