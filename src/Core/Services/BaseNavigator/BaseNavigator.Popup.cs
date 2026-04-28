using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.PanelDetection;
using System;
using System.Collections.Generic;
using System.Linq;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    public partial class BaseNavigator
    {
        #region Popup Mode Fields

        private bool _isInPopupMode;
        private GameObject _popupGameObject;
        private List<NavigableElement> _savedElements;
        private int _savedIndex;
        private InputFieldEditHelper _popupInputHelper;
        private DropdownEditHelper _popupDropdownHelper;

        // Stack of popups currently nested below the active one.
        // Used when a popup spawns another popup on top (e.g., DeckDetailsPopup → PetPopUpV2):
        // the underlying popup is pushed here while we navigate the new one, then restored
        // (re-discovered) when the top popup closes. We only stash the GO and the index —
        // on restore we re-run DiscoverPopupElements so subclass overrides re-evaluate any
        // labels that the inner popup may have mutated (e.g., picked a new avatar).
        private struct PopupSnapshot
        {
            public GameObject Popup;
            public int Index;
        }
        private readonly List<PopupSnapshot> _popupStack = new List<PopupSnapshot>();

        #endregion

        #region Popup Mode

        /// <summary>Whether popup mode is currently active</summary>
        protected bool IsInPopupMode => _isInPopupMode;

        /// <summary>The current popup's GameObject</summary>
        protected GameObject PopupGameObject => _popupGameObject;

        /// <summary>
        /// Subscribe to PanelStateManager for popup detection.
        /// Call in OnActivated(). Automatically unsubscribed on deactivation.
        /// </summary>
        protected void EnablePopupDetection()
        {
            if (PanelStateManager.Instance != null)
            {
                PanelStateManager.Instance.OnPanelChanged += OnPopupPanelChanged;
                // OnAnyPanelOpened catches stacked popups that open at the SAME priority as
                // the current popup — those don't update PanelStateManager.ActivePanel, so
                // OnPanelChanged never fires for them (e.g., PetPopUpV2 on top of DeckDetailsPopup).
                PanelStateManager.Instance.OnAnyPanelOpened += OnAnyPanelOpenedHandler;

                // Check if a popup is already active (opened while a different navigator was active)
                var activePanel = PanelStateManager.Instance.ActivePanel;
                if (activePanel != null && !_isInPopupMode && !IsPopupExcluded(activePanel) && IsPopupPanel(activePanel))
                {
                    Log.Msg("{NavigatorId}", $"Popup already active on subscribe: {activePanel.Name}");
                    OnPopupDetected(activePanel);
                }
            }
        }

        /// <summary>
        /// Unsubscribe from PanelStateManager popup detection.
        /// </summary>
        protected void DisablePopupDetection()
        {
            if (PanelStateManager.Instance != null)
            {
                PanelStateManager.Instance.OnPanelChanged -= OnPopupPanelChanged;
                PanelStateManager.Instance.OnAnyPanelOpened -= OnAnyPanelOpenedHandler;
            }
        }

        /// <summary>
        /// PanelStateManager callback for popup detection.
        /// </summary>
        private void OnPopupPanelChanged(PanelInfo oldPanel, PanelInfo newPanel)
        {
            if (!_isActive) return;

            if (newPanel != null && !IsPopupExcluded(newPanel) && IsPopupPanel(newPanel))
            {
                if (!_isInPopupMode)
                {
                    Log.Msg("{NavigatorId}", $"Popup detected: {newPanel.Name}");
                    OnPopupDetected(newPanel);
                }
                else if (newPanel.GameObject != _popupGameObject)
                {
                    // ActivePanel switched to a different popup while we were already in one
                    // (e.g., higher-priority popup opens on top). Stack the current and switch.
                    Log.Msg("{NavigatorId}", $"Stacked popup detected (active changed): {newPanel.Name}");
                    OnPopupDetected(newPanel);
                }
            }
            else if (_isInPopupMode)
            {
                // Popup closed: active panel reverted to the underlying panel (or null).
                // ExitPopupMode pops the popup stack if non-empty; if it fully exits we fire OnPopupClosed.
                Log.Msg("{NavigatorId}", $"Popup closed");
                if (ExitPopupMode())
                    OnPopupClosed();
            }
        }

        /// <summary>
        /// Handler for OnAnyPanelOpened. Catches stacked popups that open at the SAME priority
        /// as the current popup (those don't update ActivePanel and so don't trigger OnPanelChanged).
        /// </summary>
        private void OnAnyPanelOpenedHandler(PanelInfo panel)
        {
            if (!_isActive || !_isInPopupMode) return;
            if (panel == null || !panel.IsValid) return;
            if (IsPopupExcluded(panel) || !IsPopupPanel(panel)) return;
            if (panel.GameObject == _popupGameObject) return;

            Log.Msg("{NavigatorId}", $"Stacked popup detected (any opened): {panel.Name}");
            OnPopupDetected(panel);
        }

        /// <summary>
        /// Check if a panel is a popup/dialog that should be handled.
        /// </summary>
        public static bool IsPopupPanel(PanelInfo panel)
        {
            if (panel == null) return false;
            if (panel.Type == PanelType.Popup) return true;
            string name = panel.Name;
            return name.Contains("SystemMessageView") ||
                   name.Contains("Popup") ||
                   name.Contains("Dialog") ||
                   name.Contains("Modal") ||
                   name.Contains("ChallengeInvite");
        }

        /// <summary>
        /// Enter popup mode: save current elements, discover popup elements, announce.
        /// </summary>
        protected void EnterPopupMode(GameObject popup)
        {
            if (popup == null) return;

            if (_isInPopupMode && _popupGameObject != null && _popupGameObject != popup)
            {
                // Stacked entry: a new popup opened on top of the current one.
                // Push the current popup's state so we can restore it when the new popup closes.
                Log.Msg("{NavigatorId}", $"Stacking popup: {_popupGameObject.name} -> {popup.name}");
                _popupStack.Add(new PopupSnapshot
                {
                    Popup = _popupGameObject,
                    Index = _currentIndex
                });

                // Reset element list and helpers for the new popup, but DO NOT touch _savedElements —
                // those represent the underlying (non-popup) screen and must persist across nesting.
                _popupInputHelper?.Clear();
                _popupDropdownHelper?.Clear();
                _elements.Clear();
                _currentIndex = -1;
                InputManager.BlockSubmitForToggle = false;
            }
            else
            {
                Log.Msg("{NavigatorId}", $"Entering popup mode: {popup.name}");

                // Deactivate card info navigator so Up/Down navigates popup items, not card blocks
                AccessibleArenaMod.Instance?.CardNavigator?.Deactivate();

                // Save current state
                _savedElements = new List<NavigableElement>(_elements);
                _savedIndex = _currentIndex;

                _isInPopupMode = true;
                InputManager.PopupModeActive = true;
                _elements.Clear();
                _currentIndex = -1;

                // Clear toggle submit blocking - popup elements are independent of the underlying screen.
                // The previous element might have been a toggle, leaving BlockSubmitForToggle=true,
                // which would block Enter on popup buttons via EventSystemPatch.
                InputManager.BlockSubmitForToggle = false;
            }

            _popupGameObject = popup;

            // Create helpers for popup input fields and dropdowns
            _popupInputHelper = new InputFieldEditHelper(_announcer);
            _popupDropdownHelper = new DropdownEditHelper(_announcer, NavigatorId);

            // Discover popup elements
            DiscoverPopupElements(popup);

            Log.Msg("{NavigatorId}", $"Popup mode: {_elements.Count} items discovered");

            // Auto-focus first actionable item (input field, dropdown, or button), otherwise first item
            int firstActionable = _elements.FindIndex(e =>
                e.Role == UIElementClassifier.ElementRole.Button ||
                e.Role == UIElementClassifier.ElementRole.TextField ||
                e.Role == UIElementClassifier.ElementRole.Dropdown);
            _currentIndex = firstActionable >= 0 ? firstActionable : (_elements.Count > 0 ? 0 : -1);

            UpdateEventSystemSelection();
            AnnouncePopupOpen();
        }

        /// <summary>
        /// Exit popup mode. If a popup is on the stack, pop and restore it instead of fully exiting.
        /// Returns true if the navigator fully exited popup mode (caller should fire OnPopupClosed),
        /// false if it popped back to a stacked popup (caller should NOT fire OnPopupClosed because
        /// from the navigator's POV we're still inside a popup, just one level shallower).
        /// </summary>
        protected bool ExitPopupMode()
        {
            if (!_isInPopupMode) return false;

            // Drop any stacked snapshots whose popup GO is no longer alive (rare race during teardown).
            while (_popupStack.Count > 0)
            {
                var top = _popupStack[_popupStack.Count - 1];
                if (top.Popup != null && top.Popup.activeInHierarchy)
                    break;
                Log.Msg("{NavigatorId}", $"Discarding stale stacked popup: {top.Popup?.name ?? "<destroyed>"}");
                _popupStack.RemoveAt(_popupStack.Count - 1);
            }

            if (_popupStack.Count > 0)
            {
                var prev = _popupStack[_popupStack.Count - 1];
                _popupStack.RemoveAt(_popupStack.Count - 1);

                Log.Msg("{NavigatorId}", $"Popping back to popup: {prev.Popup.name}");

                // Recreate per-popup helpers for the restored popup
                _popupInputHelper?.Clear();
                _popupDropdownHelper?.Clear();
                _popupInputHelper = new InputFieldEditHelper(_announcer);
                _popupDropdownHelper = new DropdownEditHelper(_announcer, NavigatorId);

                // Re-discover the parent popup so any subclass discovery overrides re-run with
                // fresh data (e.g., cosmetic value the user just changed in the child popup).
                _popupGameObject = prev.Popup;
                _elements.Clear();
                _currentIndex = -1;
                DiscoverPopupElements(prev.Popup);

                _currentIndex = prev.Index;
                if (_currentIndex >= _elements.Count) _currentIndex = _elements.Count - 1;
                if (_currentIndex < 0 && _elements.Count > 0) _currentIndex = 0;

                UpdateEventSystemSelection();
                AnnouncePopupOpen();
                return false;
            }

            Log.Msg("{NavigatorId}", $"Exiting popup mode");
            ClearPopupModeState();
            return true;
        }

        /// <summary>
        /// Clear all popup mode state and restore saved elements.
        /// </summary>
        private void ClearPopupModeState()
        {
            _popupInputHelper?.Clear();
            _popupDropdownHelper?.Clear();
            _popupInputHelper = null;
            _popupDropdownHelper = null;
            _popupStack.Clear();

            _isInPopupMode = false;
            InputManager.PopupModeActive = false;

            // Restore saved elements
            if (_savedElements != null)
            {
                _elements.Clear();
                _elements.AddRange(_savedElements);
                _currentIndex = _savedIndex;
                _savedElements = null;

                // Refresh labels to pick up changes made while popup was open
                // (e.g., deck name edited in DeckDetailsPopup).
                // RefreshElementLabel re-reads live input field text, toggle state, dropdown values.
                for (int i = 0; i < _elements.Count; i++)
                {
                    var elem = _elements[i];
                    if (elem.GameObject != null)
                    {
                        string refreshed = RefreshElementLabel(elem.GameObject, elem.Label, elem.Role);
                        if (refreshed != elem.Label)
                        {
                            elem.Label = refreshed;
                            _elements[i] = elem;
                        }
                    }
                }
            }

            _popupGameObject = null;
        }

        /// <summary>
        /// Validate that the popup is still active.
        /// Also detects content changes (chained popups reusing the same view):
        /// when the game calls CreateButtons(), old button GOs are Destroy()ed
        /// and become null by the next frame — triggering re-discovery.
        /// </summary>
        private bool ValidatePopup()
        {
            if (_popupGameObject == null || !_popupGameObject.activeInHierarchy)
                return false;

            // Check if any interactive element's GO was destroyed (content changed)
            for (int i = 0; i < _elements.Count; i++)
            {
                if (_elements[i].GameObject == null &&
                    _elements[i].Role != UIElementClassifier.ElementRole.TextBlock)
                {
                    Log.Msg("{NavigatorId}", $"Popup element destroyed, re-discovering (chained popup)");
                    _elements.Clear();
                    _currentIndex = -1;
                    DiscoverPopupElements(_popupGameObject);
                    if (_elements.Count > 0)
                    {
                        int firstActionable = _elements.FindIndex(e =>
                            e.Role == UIElementClassifier.ElementRole.Button ||
                            e.Role == UIElementClassifier.ElementRole.TextField ||
                            e.Role == UIElementClassifier.ElementRole.Dropdown);
                        _currentIndex = firstActionable >= 0 ? firstActionable : 0;
                        AnnouncePopupOpen();
                    }
                    break;
                }
            }

            return true;
        }

        /// <summary>
        /// Dismiss the popup using a 4-level chain:
        /// 1. Find cancel button by pattern
        /// 2. Click dismiss overlay (game's click-outside-to-close button)
        /// 3. SystemMessageView.OnBack(null) via reflection
        /// 4. SetActive(false) as last resort
        /// </summary>
        protected void DismissPopup()
        {
            if (!_isInPopupMode || _popupGameObject == null) return;

            // Level 1: Find cancel button
            var cancelButton = FindPopupCancelButton(_popupGameObject);
            if (cancelButton != null)
            {
                Log.Msg("{NavigatorId}", $"Popup: clicking cancel button: {cancelButton.name}");
                _announcer?.Announce(Strings.Cancelled, AnnouncementPriority.High);

                // Try invoking CustomButton.OnClick directly first - bypasses CanvasGroup
                // interactable checks that block pointer simulation during popup animations
                if (TryInvokeCustomButtonOnClick(cancelButton))
                {
                    Log.Msg("{NavigatorId}", $"Popup: dismissed via CustomButton.OnClick.Invoke()");
                    return;
                }

                UIActivator.Activate(cancelButton);
                return;
            }

            // Level 1.5: FriendInvitePanel has no cancel button or dismiss overlay.
            // The game closes it via SocialUI.HandleKeyDown(Escape) -> _friendInvitePanel.Close().
            // Call Close() directly via reflection - it destroys the GO and fires Callback_OnClose.
            if (TryCloseFriendInvitePanel(_popupGameObject))
            {
                Log.Msg("{NavigatorId}", $"Popup: dismissed FriendInvitePanel via Close()");
                _announcer?.Announce(Strings.Cancelled, AnnouncementPriority.High);
                if (ExitPopupMode())
                    OnPopupClosed();
                return;
            }

            // Level 2: Click dismiss overlay (e.g., Blade_DismissButton, BackgroundImage)
            // These are the game's click-outside-to-close buttons. Clicking them closes
            // the popup through the game's own mechanism, so detectors pick up the close.
            var dismissOverlay = FindDismissOverlay(_popupGameObject);
            if (dismissOverlay != null)
            {
                Log.Msg("{NavigatorId}", $"Popup: clicking dismiss overlay: {dismissOverlay.name}");
                _announcer?.Announce(Strings.Cancelled, AnnouncementPriority.High);
                UIActivator.Activate(dismissOverlay);
                return;
            }

            // Level 3: SystemMessageView.OnBack(null)
            Log.Msg("{NavigatorId}", $"Popup: no cancel button or dismiss overlay found, trying OnBack()");
            var systemMessageView = FindSystemMessageViewInPopup(_popupGameObject);
            if (systemMessageView != null && TryInvokeOnBack(systemMessageView))
            {
                Log.Msg("{NavigatorId}", $"Popup: dismissed via OnBack()");
                _announcer?.Announce(Strings.Cancelled, AnnouncementPriority.High);
                if (ExitPopupMode())
                    OnPopupClosed();
                return;
            }

            // Level 4: SetActive(false) fallback
            Log.Warn("{NavigatorId}", $"Popup: using SetActive(false) fallback");
            _popupGameObject.SetActive(false);
            _announcer?.Announce(Strings.Cancelled, AnnouncementPriority.High);
            if (ExitPopupMode())
                OnPopupClosed();
        }

        /// <summary>
        /// Handle input while in popup mode.
        /// Up/Down: navigate, Enter: activate, Backspace: dismiss.
        /// </summary>
        private void HandlePopupInput()
        {
            // Dropdown edit mode intercepts all keys first
            if (_popupDropdownHelper != null && _popupDropdownHelper.IsEditing)
            {
                _popupDropdownHelper.HandleEditing(dir => NavigatePopupItem(dir));
                return;
            }

            // Input field edit mode intercepts all keys first
            if (_popupInputHelper != null && _popupInputHelper.IsEditing)
            {
                _popupInputHelper.HandleEditing(dir => NavigatePopupItem(dir));
                _popupInputHelper.TrackState();
                return;
            }

            // Up/Shift+Tab: previous item
            if (_holdRepeater.Check(KeyCode.UpArrow, () => NavigatePopupItem(-1))) return;
            if (Input.GetKeyDown(KeyCode.Tab) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            {
                NavigatePopupItem(-1);
                return;
            }

            // Down/Tab: next item
            if (_holdRepeater.Check(KeyCode.DownArrow, () => NavigatePopupItem(1))) return;
            if (Input.GetKeyDown(KeyCode.Tab) && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
            {
                NavigatePopupItem(1);
                return;
            }

            // Enter/Space: activate current item
            // Use GetEnterAndConsume which also checks EnterPressedWhileBlocked
            // (defensive: in case BlockSubmitForToggle becomes stale)
            if (InputManager.GetEnterAndConsume() || Input.GetKeyDown(KeyCode.Space))
            {
                InputManager.ConsumeKey(KeyCode.Space);
                ActivatePopupItem();
                return;
            }

            // Left/Right: stepper (e.g., craft count)
            if (_holdRepeater.Check(KeyCode.LeftArrow, () => HandleCarouselArrow(false))) return;
            if (_holdRepeater.Check(KeyCode.RightArrow, () => HandleCarouselArrow(true))) return;

            // Home/End: jump to first/last item
            if (Input.GetKeyDown(KeyCode.Home)) { NavigatePopupToIndex(0); return; }
            if (Input.GetKeyDown(KeyCode.End)) { NavigatePopupToIndex(_elements.Count - 1); return; }

            // Backspace/Escape: dismiss popup
            if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Escape))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                InputManager.ConsumeKey(KeyCode.Escape);
                DismissPopup();
                return;
            }

            // Letter navigation (A-Z) in popups
            if (SupportsLetterNavigation && !UIFocusTracker.IsAnyInputFieldFocused())
            {
                for (KeyCode key = KeyCode.A; key <= KeyCode.Z; key++)
                {
                    if (Input.GetKeyDown(key))
                    {
                        HandleLetterNavigation(key);
                        return;
                    }
                }
            }

            // F4: toggle Friends panel (works even during popups)
            if (Input.GetKeyDown(KeyCode.F4))
                HandleCustomInput();
        }

        private void NavigatePopupItem(int direction)
        {
            _letterSearch.Clear();
            if (_elements.Count == 0) return;

            int newIndex = _currentIndex + direction;

            if (newIndex < 0)
            {
                _announcer?.AnnounceInterruptVerbose(Strings.BeginningOfList);
                return;
            }
            if (newIndex >= _elements.Count)
            {
                _announcer?.AnnounceInterruptVerbose(Strings.EndOfList);
                return;
            }

            _currentIndex = newIndex;
            UpdateEventSystemSelection();
            AnnouncePopupCurrentItem();
        }

        private void NavigatePopupToIndex(int index)
        {
            _letterSearch.Clear();
            if (_elements.Count == 0) return;
            index = Math.Max(0, Math.Min(index, _elements.Count - 1));
            if (index == _currentIndex) { AnnouncePopupCurrentItem(); return; }
            _currentIndex = index;
            UpdateEventSystemSelection();
            AnnouncePopupCurrentItem();
        }

        private void ActivatePopupItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _elements.Count) return;

            var elem = _elements[_currentIndex];

            if (elem.Role == UIElementClassifier.ElementRole.TextBlock)
            {
                // Re-read text block
                AnnouncePopupCurrentItem();
                return;
            }

            if (elem.Role == UIElementClassifier.ElementRole.TextField && elem.GameObject != null)
            {
                _popupInputHelper?.EnterEditMode(elem.GameObject);
                return;
            }

            if (elem.Role == UIElementClassifier.ElementRole.Dropdown && elem.GameObject != null)
            {
                _popupDropdownHelper?.EnterEditMode(elem.GameObject);
                return;
            }

            if (elem.GameObject != null)
            {
                Log.Msg("{NavigatorId}", $"Popup: activating: {elem.Label}");
                _announcer?.AnnounceInterrupt(Strings.Activating(elem.Label));
                // Use CustomButton.Click() directly — popup buttons are keyboard-navigated
                // and SimulatePointerClick fails on first press because _mouseOver is false.
                UIActivator.ActivateViaCustomButtonClick(elem.GameObject);
                OnPopupItemActivated(elem.GameObject);
            }
        }

        #region Popup Announcements

        private void AnnouncePopupOpen()
        {
            string title = ExtractPopupTitle(_popupGameObject);

            // Fall back to first text block
            if (string.IsNullOrEmpty(title))
            {
                foreach (var elem in _elements)
                {
                    if (elem.Role == UIElementClassifier.ElementRole.TextBlock)
                    {
                        title = elem.Label;
                        break;
                    }
                }
            }

            string announcement = !string.IsNullOrEmpty(title)
                ? $"Popup: {title}. {_elements.Count} items."
                : $"Popup. {_elements.Count} items.";

            _announcer?.AnnounceInterrupt(announcement);

            // Auto-announce focused item
            if (_currentIndex >= 0 && _currentIndex < _elements.Count)
                AnnouncePopupCurrentItem();
        }

        private void AnnouncePopupCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _elements.Count) return;

            var elem = _elements[_currentIndex];
            string label = elem.Label;

            // Refresh dynamic labels
            if (elem.Role == UIElementClassifier.ElementRole.TextField && elem.GameObject != null)
            {
                label = RefreshElementLabel(elem.GameObject, label, UIElementClassifier.ElementRole.TextField);
            }
            else if (elem.Role == UIElementClassifier.ElementRole.Dropdown && elem.GameObject != null)
            {
                string currentValue = GetDropdownDisplayValue(elem.GameObject);
                if (!string.IsNullOrEmpty(currentValue))
                {
                    string recentPrefix = Strings.ChallengeInviteRecentOpponents;
                    bool isRecentChallenges = !string.IsNullOrEmpty(label) &&
                        (label.StartsWith(recentPrefix + ":", StringComparison.Ordinal) ||
                         label.StartsWith(recentPrefix + ",", StringComparison.Ordinal));
                    label = isRecentChallenges
                        ? $"{recentPrefix}: {currentValue}, {Strings.RoleDropdown}"
                        : $"{currentValue}, {Strings.RoleDropdown}";
                }
            }
            else if (elem.Role == UIElementClassifier.ElementRole.Toggle && elem.GameObject != null)
            {
                label = RefreshChallengeInviteToggleLabel(elem.GameObject, label);
            }
            else if (elem.Role == UIElementClassifier.ElementRole.Button)
            {
                label = BuildLabel(label, Strings.RoleButton, UIElementClassifier.ElementRole.Button);
            }

            _announcer?.Announce(
                Strings.ItemPositionOf(_currentIndex + 1, _elements.Count, label),
                AnnouncementPriority.Normal);
        }

        #endregion

        #region Popup Element Discovery

        /// <summary>
        /// Discover navigable elements in a popup.
        /// Override for custom discovery logic.
        /// </summary>
        protected virtual void DiscoverPopupElements(GameObject popup)
        {
            if (popup == null) return;

            var addedObjects = new HashSet<GameObject>();

            // Check for DeckCostsDetails for structured deck info
            bool hasDeckCosts = HasComponentInChildren(popup, "DeckCostsDetails");

            var skipTransforms = new List<Transform>();
            if (hasDeckCosts)
            {
                CollectWidgetContentTransforms(popup, "DeckTypesDetails", "ItemParent", skipTransforms);
                CollectWidgetContentTransforms(popup, "DeckColorsDetails", null, skipTransforms);
                CollectWidgetContentTransforms(popup, "CosmeticSelectorController", null, skipTransforms);
            }

            // Phase 0: Challenge invite friend tiles (must run before text blocks so their
            // TMP_Text children are skipped via IsInsideChallengeInviteTile)
            DiscoverChallengeInviteTiles(popup, addedObjects, skipTransforms);

            // Phase 1: Discover text blocks
            DiscoverPopupTextBlocks(popup, hasDeckCosts, skipTransforms);

            // Phase 1b: Add title/header texts as navigable items
            DiscoverPopupTitleTexts(popup);

            // Phase 2: Discover input fields
            DiscoverPopupInputFields(popup, addedObjects);

            // Phase 3: Discover dropdowns
            DiscoverPopupDropdowns(popup, addedObjects);

            // Phase 4: Discover buttons
            // Skip for FriendInvitePanel - it has a broken send button; user submits via Enter
            // in the input field instead (which triggers onSubmit -> HandleSubmitInput correctly)
            if (!HasComponentInChildren(popup, "FriendInvitePanel"))
            {
                DiscoverPopupButtons(popup, addedObjects);

                // Phase 5: Remove text blocks duplicating button labels
                DeduplicateTextBlocksAgainstButtons();
            }

            // Phase 6: Detect stepper elements (e.g., craft quantity in CardViewerPopup)
            DiscoverPopupSteppers(popup);
        }

        private void DiscoverPopupTextBlocks(GameObject popup, bool hasDeckCosts, List<Transform> skipTransforms)
        {
            var seenTexts = new HashSet<string>();

            foreach (var tmp in popup.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;

                if (IsInsideButton(tmp.transform, popup.transform)) continue;
                if (IsInsideInputField(tmp.transform, popup.transform)) continue;
                if (IsInsideDropdown(tmp.transform, popup.transform)) continue;
                if (IsInsideTitleContainer(tmp.transform, popup.transform)) continue;
                if (IsInsideChallengeInviteTile(tmp.transform, popup.transform)) continue;

                if (hasDeckCosts && IsInsideComponentByName(tmp.transform, popup.transform, "DeckCostsDetails"))
                    continue;
                if (skipTransforms.Count > 0 && IsChildOfAny(tmp.transform, skipTransforms))
                    continue;

                string text = UITextExtractor.CleanText(tmp.text);
                if (string.IsNullOrWhiteSpace(text) || text.Length < 3) continue;

                var lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length < 3) continue;
                    if (seenTexts.Contains(trimmed)) continue;

                    seenTexts.Add(trimmed);
                    AddTextBlock(trimmed);
                    Log.Msg("{NavigatorId}", $"Popup: text block: {trimmed}");
                }
            }

            // Inject structured deck info if applicable
            if (hasDeckCosts)
            {
                var deckInfo = DeckInfoProvider.GetDeckInfoElements();
                if (deckInfo != null)
                {
                    foreach (var (label, text) in deckInfo)
                    {
                        string combined = $"{label}: {text}";
                        AddTextBlock(combined);
                        Log.Msg("{NavigatorId}", $"Popup: deck info: {combined}");
                    }
                }
            }
        }

        private void DiscoverPopupTitleTexts(GameObject popup)
        {
            if (popup == null) return;

            var titleTexts = new List<string>();
            var seenTexts = new HashSet<string>();

            // Collect existing text block labels to avoid duplicates
            foreach (var el in _elements)
            {
                if (el.Role == UIElementClassifier.ElementRole.TextBlock && !string.IsNullOrEmpty(el.Label))
                    seenTexts.Add(el.Label);
            }

            foreach (var tmp in popup.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;
                if (!IsInsideTitleContainer(tmp.transform, popup.transform)) continue;
                if (IsInsideButton(tmp.transform, popup.transform)) continue;

                string text = UITextExtractor.CleanText(tmp.text);
                if (string.IsNullOrWhiteSpace(text) || text.Length < 3) continue;

                string trimmed = text.Trim();
                if (seenTexts.Contains(trimmed)) continue;

                seenTexts.Add(trimmed);
                titleTexts.Add(trimmed);
                Log.Msg("{NavigatorId}", $"Popup: title text: {trimmed}");
            }

            // Insert title texts at the beginning so they're navigated first
            for (int i = titleTexts.Count - 1; i >= 0; i--)
            {
                _elements.Insert(0, new NavigableElement
                {
                    GameObject = null,
                    Label = titleTexts[i],
                    Role = UIElementClassifier.ElementRole.TextBlock
                });
            }
        }

        private void DiscoverPopupInputFields(GameObject popup, HashSet<GameObject> addedObjects)
        {
            var discovered = new List<(GameObject obj, string label, float sortOrder)>();

            foreach (var field in popup.GetComponentsInChildren<TMP_InputField>(true))
            {
                if (field == null || !field.gameObject.activeInHierarchy || !field.interactable) continue;
                if (addedObjects.Contains(field.gameObject)) continue;

                string label = UITextExtractor.GetInputFieldLabel(field.gameObject);
                var pos = field.gameObject.transform.position;
                discovered.Add((field.gameObject, label, -pos.y * 1000 + pos.x));
                addedObjects.Add(field.gameObject);
            }

            foreach (var (obj, label, _) in discovered.OrderBy(x => x.sortOrder))
            {
                _elements.Add(new NavigableElement
                {
                    GameObject = obj,
                    Label = label,
                    Role = UIElementClassifier.ElementRole.TextField
                });
                Log.Msg("{NavigatorId}", $"Popup: input field: {label}");
            }
        }

        private void DiscoverPopupDropdowns(GameObject popup, HashSet<GameObject> addedObjects)
        {
            var discovered = new List<(GameObject obj, string label, float sortOrder)>();
            GameObject recentChallengesDropdown = GetChallengeInviteRecentDropdown(popup);

            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (addedObjects.Contains(mb.gameObject)) continue;
                if (IsInsideInputField(mb.transform, popup.transform)) continue;

                string typeName = mb.GetType().Name;
                bool isDropdown = typeName == T.CustomTMPDropdown ||
                                  mb is TMP_Dropdown ||
                                  mb is Dropdown;
                if (!isDropdown) continue;

                string displayValue = GetDropdownDisplayValue(mb.gameObject);
                string namePrefix = (recentChallengesDropdown != null && mb.gameObject == recentChallengesDropdown)
                    ? Strings.ChallengeInviteRecentOpponents
                    : null;

                string label;
                if (namePrefix != null)
                {
                    label = !string.IsNullOrEmpty(displayValue)
                        ? $"{namePrefix}: {displayValue}, {Strings.RoleDropdown}"
                        : $"{namePrefix}, {Strings.RoleDropdown}";
                }
                else
                {
                    label = !string.IsNullOrEmpty(displayValue)
                        ? $"{displayValue}, {Strings.RoleDropdown}"
                        : $"{mb.gameObject.name}, {Strings.RoleDropdown}";
                }

                var pos = mb.gameObject.transform.position;
                discovered.Add((mb.gameObject, label, -pos.y * 1000 + pos.x));
                addedObjects.Add(mb.gameObject);
            }

            foreach (var (obj, label, _) in discovered.OrderBy(x => x.sortOrder))
            {
                _elements.Add(new NavigableElement
                {
                    GameObject = obj,
                    Label = label,
                    Role = UIElementClassifier.ElementRole.Dropdown
                });
                Log.Msg("{NavigatorId}", $"Popup: dropdown: {label}");
            }
        }

        private void DiscoverPopupButtons(GameObject popup, HashSet<GameObject> addedObjects)
        {
            var discovered = new List<(GameObject obj, string label, float sortOrder)>();

            // Pass 1: SystemMessageButtonView
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (addedObjects.Contains(mb.gameObject)) continue;
                if (IsInsideInputField(mb.transform, popup.transform)) continue;
                if (IsInsideDropdown(mb.transform, popup.transform)) continue;

                if (mb.GetType().Name == T.SystemMessageButtonView)
                {
                    string label = UITextExtractor.GetText(mb.gameObject);
                    if (string.IsNullOrEmpty(label)) label = mb.gameObject.name;
                    var pos = mb.gameObject.transform.position;
                    discovered.Add((mb.gameObject, label, -pos.y * 1000 + pos.x));
                    addedObjects.Add(mb.gameObject);
                }
            }

            // Pass 2: CustomButton / CustomButtonWithTooltip
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (addedObjects.Contains(mb.gameObject)) continue;
                if (IsInsideInputField(mb.transform, popup.transform)) continue;
                if (IsInsideDropdown(mb.transform, popup.transform)) continue;
                if (IsInsideButton(mb.transform, popup.transform)) continue;

                string typeName = mb.GetType().Name;
                if (typeName == T.CustomButton || typeName == T.CustomButtonWithTooltip)
                {
                    string label = UITextExtractor.GetText(mb.gameObject);
                    if (string.IsNullOrEmpty(label)) label = mb.gameObject.name;
                    var pos = mb.gameObject.transform.position;
                    discovered.Add((mb.gameObject, label, -pos.y * 1000 + pos.x));
                    addedObjects.Add(mb.gameObject);
                }
            }

            // Pass 3: Standard Unity Buttons
            foreach (var button in popup.GetComponentsInChildren<Button>(true))
            {
                if (button == null || !button.gameObject.activeInHierarchy || !button.interactable) continue;
                if (addedObjects.Contains(button.gameObject)) continue;
                if (IsInsideInputField(button.transform, popup.transform)) continue;
                if (IsInsideDropdown(button.transform, popup.transform)) continue;
                if (IsInsideButton(button.transform, popup.transform)) continue;

                string label = UITextExtractor.GetText(button.gameObject);
                if (string.IsNullOrEmpty(label)) label = button.gameObject.name;
                var pos = button.gameObject.transform.position;
                discovered.Add((button.gameObject, label, -pos.y * 1000 + pos.x));
                addedObjects.Add(button.gameObject);
            }

            // Sort, filter dismiss overlays, deduplicate
            var seenLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (obj, label, _) in discovered.OrderBy(x => x.sortOrder))
            {
                if (IsDismissOverlay(obj))
                {
                    Log.Msg("{NavigatorId}", $"Popup: skipping dismiss overlay: {obj.name}");
                    continue;
                }
                if (ShouldSkipPopupButton(obj))
                {
                    Log.Msg("{NavigatorId}", $"Popup: skipping button (subclass filter): {obj.name}");
                    continue;
                }

                // Allow subclasses to substitute a richer label and bypass the dedup that
                // would otherwise collapse multiple sprite-only items sharing a generic name.
                // Used for cosmetic-selector items (avatar busts, pet rows, sleeve cards).
                string finalLabel = label;
                bool bypassDedup = false;
                if (TryGetCustomPopupButtonLabel(obj, out string custom) && !string.IsNullOrEmpty(custom))
                {
                    finalLabel = custom;
                    bypassDedup = true;
                }

                if (!bypassDedup && !seenLabels.Add(finalLabel))
                {
                    Log.Msg("{NavigatorId}", $"Popup: skipping duplicate button: {finalLabel}");
                    continue;
                }

                _elements.Add(new NavigableElement
                {
                    GameObject = obj,
                    Label = finalLabel,
                    Role = UIElementClassifier.ElementRole.Button
                });
                Log.Msg("{NavigatorId}", $"Popup: button: {finalLabel}");
            }
        }

        /// <summary>
        /// Hook for subclasses to substitute a richer label for a discovered popup button
        /// (e.g., resolving an avatar/pet/sleeve item to its localized name). Returning true
        /// also opts the button out of label-based dedup so multiple sibling items with the
        /// same underlying GameObject name still all become navigable. Default: no override.
        /// </summary>
        protected virtual bool TryGetCustomPopupButtonLabel(GameObject buttonObj, out string label)
        {
            label = null;
            return false;
        }

        /// <summary>
        /// Hook called after a popup item is activated (the click is dispatched). Subclasses
        /// can use this to react to activations — e.g., detecting that an inline selector
        /// expanded inside the current popup and switching into it as a stacked popup.
        /// Default: no-op.
        /// </summary>
        protected virtual void OnPopupItemActivated(GameObject element) { }

        /// <summary>
        /// Hook called for each candidate button before it is added to the popup element
        /// list. Returning true skips the button entirely. Useful for filtering noisy
        /// non-interactive overlays (e.g., right-side preview hitboxes inside cosmetic
        /// popups that have no real keyboard action). Default: do not skip.
        /// </summary>
        protected virtual bool ShouldSkipPopupButton(GameObject buttonObj) => false;

        /// <summary>
        /// Silently re-run popup element discovery without announcing or exiting popup
        /// mode. Preserves the user's current focus by GameObject identity when possible
        /// (else clamps to the prior numeric index). Used when a popup mutates its own
        /// content in response to an action — e.g., the avatar selector updating its
        /// title/bio text blocks when the user previews a different bust.
        /// </summary>
        protected void RefreshPopupElementsSilently()
        {
            if (!_isInPopupMode || _popupGameObject == null) return;

            int savedIndex = _currentIndex;
            GameObject savedFocused = (savedIndex >= 0 && savedIndex < _elements.Count)
                ? _elements[savedIndex].GameObject
                : null;

            _elements.Clear();
            _currentIndex = -1;
            DiscoverPopupElements(_popupGameObject);

            if (savedFocused != null)
            {
                for (int i = 0; i < _elements.Count; i++)
                {
                    if (_elements[i].GameObject == savedFocused)
                    {
                        _currentIndex = i;
                        break;
                    }
                }
            }
            if (_currentIndex < 0) _currentIndex = Math.Min(savedIndex, _elements.Count - 1);
            if (_currentIndex < 0 && _elements.Count > 0) _currentIndex = 0;

            UpdateEventSystemSelection();
            Log.Msg("{NavigatorId}", $"Popup: silent refresh — {_elements.Count} items");
        }

        private void DeduplicateTextBlocksAgainstButtons()
        {
            var buttonLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var elem in _elements)
            {
                if (elem.Role == UIElementClassifier.ElementRole.Button)
                    buttonLabels.Add(elem.Label);
            }
            if (buttonLabels.Count == 0) return;

            int removed = _elements.RemoveAll(e =>
                e.Role == UIElementClassifier.ElementRole.TextBlock && buttonLabels.Contains(e.Label));
            if (removed > 0)
                Log.Msg("{NavigatorId}", $"Popup: removed {removed} text blocks duplicating button labels");
        }

        /// <summary>
        /// Detect stepper elements in the popup via reflection (e.g., craft quantity in CardViewerPopup).
        /// Finds controllers with known increment/decrement methods and a count label.
        /// </summary>
        private void DiscoverPopupSteppers(GameObject popup)
        {
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var type = mb.GetType();
                if (type.Name != "CardViewerController") continue;

                // Get pip objects (shared between stepper and no-stepper paths)
                var pipsField = type.GetField("_CraftPips",
                    PrivateInstance);
                var pipObjects = new HashSet<GameObject>();
                if (pipsField != null)
                {
                    var pips = pipsField.GetValue(mb) as System.Collections.IList;
                    if (pips != null)
                    {
                        foreach (var pip in pips)
                        {
                            var pipMb = pip as MonoBehaviour;
                            if (pipMb != null) pipObjects.Add(pipMb.gameObject);
                        }
                    }
                }

                // Read actual owned count from controller (not pip count which is always 4)
                int ownedCount = 0;
                var collectedQtyField = type.GetField("_collectedQuantity",
                    PrivateInstance);
                if (collectedQtyField != null)
                {
                    ownedCount = (int)collectedQtyField.GetValue(mb);
                }

                // Find the craft count label
                var countLabelField = type.GetField("_craftCountLabel",
                    PrivateInstance);
                var countLabel = countLabelField?.GetValue(mb) as TMP_Text;

                bool hasStepper = countLabel != null && countLabel.gameObject.activeInHierarchy;

                if (hasStepper)
                {
                    // Find increment/decrement methods
                    var increaseMethod = type.GetMethod("Unity_OnCraftIncrease",
                        PublicInstance);
                    var decreaseMethod = type.GetMethod("Unity_OnCraftDecrease",
                        PublicInstance);
                    if (increaseMethod == null || decreaseMethod == null) continue;

                    string countText = UITextExtractor.CleanText(countLabel.text);
                    if (string.IsNullOrEmpty(countText)) countText = "0";

                    // Remove any text block that duplicates the count label
                    _elements.RemoveAll(e =>
                        e.Role == UIElementClassifier.ElementRole.TextBlock &&
                        e.Label == countText);

                    // Find first pip index before removing, so we can insert at that position
                    int pipInsertIndex = _elements.FindIndex(e =>
                        e.GameObject != null && pipObjects.Contains(e.GameObject));

                    // Replace craft pip buttons with single owned count
                    int removedPips = _elements.RemoveAll(e =>
                        e.GameObject != null && pipObjects.Contains(e.GameObject));
                    if (pipInsertIndex < 0 || pipInsertIndex > _elements.Count)
                        pipInsertIndex = _elements.Count;

                    // Insert owned count first, then stepper after it
                    if (removedPips > 0)
                    {
                        GameObject firstPip = null;
                        foreach (var go in pipObjects) { firstPip = go; break; }
                        _elements.Insert(pipInsertIndex, new NavigableElement
                        {
                            GameObject = firstPip,
                            Label = Models.Strings.CardOwned(ownedCount),
                            Role = UIElementClassifier.ElementRole.TextBlock
                        });
                        pipInsertIndex++; // stepper goes after owned
                    }

                    string label = $"{countText}, {Models.Strings.RoleStepperHint}";

                    _elements.Insert(pipInsertIndex, new NavigableElement
                    {
                        GameObject = countLabel.gameObject,
                        Label = label,
                        Role = UIElementClassifier.ElementRole.Stepper,
                        Carousel = new CarouselInfo
                        {
                            HasArrowNavigation = true,
                            OnIncrement = () =>
                            {
                                try { increaseMethod.Invoke(mb, null); }
                                catch (Exception ex) { Log.Warn("{NavigatorId}", $"Craft increment failed: {ex.Message}"); }
                            },
                            OnDecrement = () =>
                            {
                                try { decreaseMethod.Invoke(mb, null); }
                                catch (Exception ex) { Log.Warn("{NavigatorId}", $"Craft decrement failed: {ex.Message}"); }
                            },
                            ReadLabel = () =>
                            {
                                try { return UITextExtractor.CleanText(countLabel.text); }
                                catch { return null; }
                            }
                        }
                    });

                    Log.Msg("{NavigatorId}", $"Popup: craft stepper: {countText}, owned: {ownedCount}");
                }
                else if (pipObjects.Count > 0)
                {
                    // No stepper (fully owned) — replace individual pips with single ownership text
                    int pipInsertIndex = _elements.FindIndex(e =>
                        e.GameObject != null && pipObjects.Contains(e.GameObject));

                    _elements.RemoveAll(e =>
                        e.GameObject != null && pipObjects.Contains(e.GameObject));

                    if (pipInsertIndex < 0 || pipInsertIndex > _elements.Count)
                        pipInsertIndex = _elements.Count;

                    GameObject firstPip = null;
                    foreach (var go in pipObjects) { firstPip = go; break; }

                    _elements.Insert(pipInsertIndex, new NavigableElement
                    {
                        GameObject = firstPip,
                        Label = Models.Strings.CardOwned(ownedCount),
                        Role = UIElementClassifier.ElementRole.TextBlock
                    });

                    Log.Msg("{NavigatorId}", $"Popup: owned: {ownedCount} (no stepper)");
                }

                break;
            }
        }

        #endregion

        #region Popup Helpers

        private string ExtractPopupTitle(GameObject popup)
        {
            if (popup == null) return null;

            foreach (var tmp in popup.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;
                if (!IsInsideTitleContainer(tmp.transform, popup.transform)) continue;

                string text = UITextExtractor.CleanText(tmp.text);
                if (!string.IsNullOrWhiteSpace(text) && text.Length >= 3)
                    return text.Trim();
            }
            return null;
        }

        private static bool IsInsideTitleContainer(Transform child, Transform stopAt)
        {
            Transform current = child;
            while (current != null && current != stopAt)
            {
                string name = current.name;
                if (name.IndexOf("Header", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                int titleIdx = name.IndexOf("Title", StringComparison.OrdinalIgnoreCase);
                if (titleIdx >= 0 && name.IndexOf("Subtitle", StringComparison.OrdinalIgnoreCase) < 0)
                    return true;
                current = current.parent;
            }
            return false;
        }

        private static bool IsInsideButton(Transform child, Transform stopAt)
        {
            Transform current = child.parent;
            while (current != null && current != stopAt)
            {
                foreach (var mb in current.GetComponents<MonoBehaviour>())
                {
                    if (mb != null)
                    {
                        string typeName = mb.GetType().Name;
                        if (typeName == T.CustomButton || typeName == T.CustomButtonWithTooltip ||
                            typeName == T.SystemMessageButtonView)
                            return true;
                    }
                }
                if (current.GetComponent<Button>() != null)
                    return true;
                current = current.parent;
            }
            return false;
        }

        private static bool IsInsideDropdown(Transform child, Transform stopAt)
        {
            Transform current = child.parent;
            while (current != null && current != stopAt)
            {
                if (UIFocusTracker.IsDropdown(current.gameObject))
                    return true;
                current = current.parent;
            }
            return false;
        }

        private static bool IsInsideInputField(Transform child, Transform stopAt)
        {
            Transform current = child.parent;
            while (current != null && current != stopAt)
            {
                if (current.GetComponent<TMP_InputField>() != null)
                    return true;
                current = current.parent;
            }
            return false;
        }

        private static bool IsInsideComponentByName(Transform child, Transform stopAt, string typeName)
        {
            Transform current = child;
            while (current != null && current != stopAt)
            {
                foreach (var mb in current.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == typeName)
                        return true;
                }
                current = current.parent;
            }
            return false;
        }

        protected static bool HasComponentInChildren(GameObject go, string typeName)
        {
            if (go == null) return false;
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && mb.GetType().Name == typeName)
                    return true;
            }
            return false;
        }

        private static bool IsDismissOverlay(GameObject obj)
        {
            string name = obj.name.ToLower();
            return name.Contains("background") || name.Contains("overlay") ||
                   name.Contains("backdrop") || name.Contains("dismiss") ||
                   name.Contains("shield");
        }

        /// <summary>
        /// Find a dismiss overlay button in the popup (e.g., Blade_DismissButton).
        /// Searches both standard Unity Buttons and CustomButton/AdvancedButton components.
        /// Prefers buttons with "dismiss" in the name over generic "background" ones.
        /// </summary>
        private static GameObject FindDismissOverlay(GameObject popup)
        {
            if (popup == null) return null;

            GameObject fallback = null;

            // Scan all MonoBehaviours to catch CustomButton, AdvancedButton, and standard Button
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                string typeName = mb.GetType().Name;
                if (typeName != T.CustomButton && typeName != "AdvancedButton" &&
                    typeName != "Button") continue;
                if (!IsDismissOverlay(mb.gameObject)) continue;

                // Prefer "dismiss" buttons over generic "background" ones
                if (mb.gameObject.name.ToLower().Contains("dismiss"))
                    return mb.gameObject;

                if (fallback == null)
                    fallback = mb.gameObject;
            }

            return fallback;
        }

        private static bool IsChildOfAny(Transform child, List<Transform> parents)
        {
            foreach (var parent in parents)
            {
                if (child.IsChildOf(parent))
                    return true;
            }
            return false;
        }

        private static void CollectWidgetContentTransforms(GameObject popup, string componentTypeName,
            string fieldName, List<Transform> skipTransforms)
        {
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.GetType().Name != componentTypeName) continue;

                if (fieldName != null)
                {
                    var field = mb.GetType().GetField(fieldName, PublicInstance);
                    if (field != null)
                    {
                        var transform = field.GetValue(mb) as Transform;
                        if (transform != null)
                            skipTransforms.Add(transform);
                    }
                }
                else
                {
                    skipTransforms.Add(mb.transform);
                }
                break;
            }
        }

        /// <summary>
        /// Find the cancel/close/no button in a popup using pattern matching + reflection fallback.
        /// </summary>
        private GameObject FindPopupCancelButton(GameObject popup)
        {
            if (popup == null) return null;

            string[] cancelPatterns = { "cancel", "close", "back", "no", "abbrechen", "nein", "zurück" };

            // Pass 1: SystemMessageButtonView
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == T.SystemMessageButtonView && MatchesCancelPattern(mb.gameObject, cancelPatterns))
                    return mb.gameObject;
            }

            // Pass 2: CustomButton / CustomButtonWithTooltip
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                string typeName = mb.GetType().Name;
                if ((typeName == T.CustomButton || typeName == T.CustomButtonWithTooltip) &&
                    MatchesCancelPattern(mb.gameObject, cancelPatterns))
                    return mb.gameObject;
            }

            // Pass 3: Standard Unity Buttons
            foreach (var button in popup.GetComponentsInChildren<Button>(true))
            {
                if (button == null || !button.gameObject.activeInHierarchy || !button.interactable) continue;
                if (MatchesCancelPattern(button.gameObject, cancelPatterns))
                    return button.gameObject;
            }

            // Pass 4: _cancelButton via reflection
            foreach (var mb in popup.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                var field = mb.GetType().GetField("_cancelButton",
                    PrivateInstance);
                if (field == null) continue;

                if (field.GetValue(mb) is MonoBehaviour cancelMb && cancelMb != null && cancelMb.gameObject != null)
                {
                    Log.Msg("{NavigatorId}", $"Popup: found _cancelButton via reflection on {mb.GetType().Name}");
                    return cancelMb.gameObject;
                }
            }

            return null;
        }

        /// <summary>
        /// Try to close a FriendInvitePanel popup via its Close() method.
        /// This popup has no cancel button or dismiss overlay - the game only closes it via Escape.
        /// </summary>
        private static bool TryCloseFriendInvitePanel(GameObject popup)
        {
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && mb.GetType().Name == "FriendInvitePanel")
                {
                    var closeMethod = mb.GetType().GetMethod("Close", PublicInstance);
                    if (closeMethod != null)
                    {
                        closeMethod.Invoke(mb, null);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Find a named button field via reflection on the popup's components and invoke its OnClick.
        /// Returns true if the button was found and invoked.
        /// </summary>
        protected bool TryInvokePopupButtonByFieldName(string fieldName)
        {
            if (_popupGameObject == null) return false;

            foreach (var mb in _popupGameObject.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                var field = mb.GetType().GetField(fieldName,
                    PrivateInstance);
                if (field == null) continue;

                if (field.GetValue(mb) is MonoBehaviour buttonMb && buttonMb != null)
                {
                    Log.Msg("{NavigatorId}", $"Found {fieldName} via reflection on {mb.GetType().Name}");
                    if (TryInvokeCustomButtonOnClick(buttonMb.gameObject))
                    {
                        Log.Msg("{NavigatorId}", $"Invoked {fieldName}.OnClick successfully");
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Try to invoke a CustomButton's OnClick event directly via reflection.
        /// Bypasses CanvasGroup/Selectable interactable checks that block pointer simulation.
        /// </summary>
        private bool TryInvokeCustomButtonOnClick(GameObject buttonObj)
        {
            if (buttonObj == null) return false;

            foreach (var mb in buttonObj.GetComponents<MonoBehaviour>())
            {
                if (mb == null || mb.GetType().Name != T.CustomButton) continue;

                // CustomButton.OnClick is a public property returning a UnityEvent-like type
                var onClickProp = mb.GetType().GetProperty("OnClick",
                    PublicInstance);
                if (onClickProp == null)
                {
                    // Try as field
                    var onClickField = mb.GetType().GetField("_onClick",
                        PrivateInstance);
                    if (onClickField == null) continue;

                    var onClickVal = onClickField.GetValue(mb);
                    if (onClickVal == null) continue;

                    var invokeMethod = onClickVal.GetType().GetMethod("Invoke", Type.EmptyTypes);
                    if (invokeMethod != null)
                    {
                        invokeMethod.Invoke(onClickVal, null);
                        return true;
                    }
                    continue;
                }

                var onClick = onClickProp.GetValue(mb);
                if (onClick == null) continue;

                var invoke = onClick.GetType().GetMethod("Invoke", Type.EmptyTypes);
                if (invoke != null)
                {
                    invoke.Invoke(onClick, null);
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesCancelPattern(GameObject obj, string[] patterns)
        {
            string buttonText = UITextExtractor.GetText(obj)?.ToLower() ?? "";
            string buttonName = obj.name.ToLower();

            foreach (var pattern in patterns)
            {
                if (ContainsCancelWord(buttonText, pattern) || ContainsCancelWord(buttonName, pattern))
                    return true;
            }
            return false;
        }

        private static bool ContainsCancelWord(string text, string word)
        {
            if (string.IsNullOrEmpty(text)) return false;
            int idx = 0;
            while ((idx = text.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
            {
                bool startOk = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
                bool endOk = idx + word.Length >= text.Length || !char.IsLetterOrDigit(text[idx + word.Length]);
                if (startOk && endOk) return true;
                idx += word.Length;
            }
            return false;
        }

        private MonoBehaviour FindSystemMessageViewInPopup(GameObject popup)
        {
            if (popup == null) return null;

            // Search children
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && mb.GetType().Name == "SystemMessageView")
                    return mb;
            }

            // Search up hierarchy
            var current = popup.transform.parent;
            while (current != null)
            {
                foreach (var mb in current.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == "SystemMessageView")
                        return mb;
                }
                current = current.parent;
            }

            // Scene-wide fallback
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "SystemMessageView" && mb.gameObject.activeInHierarchy)
                    return mb;
            }

            return null;
        }

        private bool TryInvokeOnBack(MonoBehaviour component)
        {
            if (component == null) return false;

            var type = component.GetType();
            foreach (var method in type.GetMethods(AllInstanceFlags))
            {
                if (method.Name == "OnBack" && method.GetParameters().Length == 1)
                {
                    try
                    {
                        Log.Msg("{NavigatorId}", $"Popup: invoking {type.Name}.OnBack(null)");
                        method.Invoke(component, new object[] { null });
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("{NavigatorId}", $"Popup: OnBack error: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
            }

            return false;
        }

        #endregion

        #endregion
    }
}
