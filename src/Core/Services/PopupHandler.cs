using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.PanelDetection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Shared utility for popup/dialog detection, element discovery, navigation, and dismissal.
    /// Replaces inline popup handling in SettingsMenuNavigator, DraftNavigator,
    /// MasteryNavigator, and StoreNavigator (generic popups only).
    /// Navigation model: Up/Down through flat list of text blocks + buttons (text first, then buttons).
    /// No wraparound - clips at edges with Beginning/End of list.
    /// </summary>
    public class PopupHandler
    {
        #region Types

        private enum PopupItemType { TextBlock, Button, InputField, Dropdown }

        private struct PopupItem
        {
            public PopupItemType Type;
            public GameObject GameObject; // null for text blocks
            public string Label;          // announced text
        }

        #endregion

        #region State

        private readonly string _navigatorId;
        private readonly IAnnouncementService _announcer;
        private readonly InputFieldEditHelper _inputHelper;
        private readonly DropdownEditHelper _dropdownHelper;

        private GameObject _activePopup;
        private readonly List<PopupItem> _items = new List<PopupItem>();
        private int _currentIndex;
        private string _title;

        #endregion

        #region Properties

        public bool IsActive => _activePopup != null;
        public GameObject ActivePopup => _activePopup;

        #endregion

        #region Constructor

        public PopupHandler(string navigatorId, IAnnouncementService announcer)
        {
            _navigatorId = navigatorId;
            _announcer = announcer;
            _inputHelper = new InputFieldEditHelper(announcer);
            _dropdownHelper = new DropdownEditHelper(announcer, navigatorId);
        }

        #endregion

        #region Static Detection

        /// <summary>
        /// Check if a panel is a popup/dialog that should be handled.
        /// </summary>
        public static bool IsPopupPanel(PanelInfo panel)
        {
            if (panel == null) return false;

            if (panel.Type == PanelType.Popup)
                return true;

            string name = panel.Name;
            return name.Contains("SystemMessageView") ||
                   name.Contains("Popup") ||
                   name.Contains("Dialog") ||
                   name.Contains("Modal") ||
                   name.Contains("ChallengeInvite");
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// Called when a popup is detected. Discovers items and announces.
        /// </summary>
        public void OnPopupDetected(GameObject popup)
        {
            if (popup == null) return;

            _activePopup = popup;
            _currentIndex = -1;
            _items.Clear();

            MelonLogger.Msg($"[{_navigatorId}] PopupHandler: popup detected: {popup.name}");

            // Deactivate card info navigator so Up/Down navigates popup items, not card blocks
            AccessibleArenaMod.Instance?.CardNavigator?.Deactivate();

            DiscoverItems();

            MelonLogger.Msg($"[{_navigatorId}] PopupHandler: {CountTextBlocks()} text blocks, {CountInputFields()} input fields, {CountDropdowns()} dropdowns, {CountButtons()} buttons");

            AnnouncePopupOpen();
        }

        /// <summary>
        /// Reset all state. Call when popup closes or navigator deactivates.
        /// </summary>
        public void Clear()
        {
            _inputHelper.Clear();
            _dropdownHelper.Clear();

            _activePopup = null;
            _items.Clear();
            _currentIndex = -1;
            _title = null;
        }

        /// <summary>
        /// Returns false if the popup GameObject is gone.
        /// </summary>
        public bool ValidatePopup()
        {
            if (_activePopup == null || !_activePopup.activeInHierarchy)
            {
                Clear();
                return false;
            }
            return true;
        }

        #endregion

        #region Input Handling

        /// <summary>
        /// Handle popup navigation input. Returns true if input was consumed.
        /// Up/Down/W/S + Tab: navigate items
        /// Enter/Space: activate button (re-read if text block)
        /// Backspace: dismiss via 3-level chain
        /// </summary>
        public bool HandleInput()
        {
            if (_activePopup == null) return false;

            // Dropdown edit mode intercepts all keys first
            if (_dropdownHelper.IsEditing)
            {
                bool consumed = _dropdownHelper.HandleEditing(dir => NavigateItem(dir));
                return consumed;
            }

            // Input field edit mode intercepts all keys first
            if (_inputHelper.IsEditing)
            {
                bool consumed = _inputHelper.HandleEditing(dir => NavigateItem(dir));
                _inputHelper.TrackState();
                return consumed;
            }

            // Up/W/Shift+Tab: previous item
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) ||
                (Input.GetKeyDown(KeyCode.Tab) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))))
            {
                NavigateItem(-1);
                return true;
            }

            // Down/S/Tab: next item
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) ||
                (Input.GetKeyDown(KeyCode.Tab) && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)))
            {
                NavigateItem(1);
                return true;
            }

            // Enter/Space: activate current item
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space))
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
                ActivateCurrentItem();
                return true;
            }

            // Backspace: dismiss popup
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                DismissPopup();
                return true;
            }

            return false;
        }

        #endregion

        #region Dismissal

        /// <summary>
        /// Dismiss the popup using a 3-level chain:
        /// 1. Find cancel button by pattern
        /// 2. SystemMessageView.OnBack(null) via reflection
        /// 3. SetActive(false) as last resort
        /// </summary>
        public void DismissPopup()
        {
            if (_activePopup == null) return;

            // Level 1: Find cancel button
            var cancelButton = FindPopupCancelButton(_activePopup);
            if (cancelButton != null)
            {
                MelonLogger.Msg($"[{_navigatorId}] PopupHandler: clicking cancel button: {cancelButton.name}");
                _announcer?.Announce(Strings.Cancelled, AnnouncementPriority.High);
                UIActivator.Activate(cancelButton);
                return;
            }

            // Level 2: SystemMessageView.OnBack(null)
            MelonLogger.Msg($"[{_navigatorId}] PopupHandler: no cancel button found, trying OnBack()");
            var systemMessageView = FindSystemMessageViewInPopup(_activePopup);
            if (systemMessageView != null && TryInvokeOnBack(systemMessageView))
            {
                MelonLogger.Msg($"[{_navigatorId}] PopupHandler: dismissed via OnBack()");
                _announcer?.Announce(Strings.Cancelled, AnnouncementPriority.High);
                Clear();
                return;
            }

            // Level 3: SetActive(false) fallback
            MelonLogger.Warning($"[{_navigatorId}] PopupHandler: using SetActive(false) fallback");
            _activePopup.SetActive(false);
            _announcer?.Announce(Strings.Cancelled, AnnouncementPriority.High);
            Clear();
        }

        #endregion

        #region Element Discovery

        private void DiscoverItems()
        {
            _items.Clear();
            _title = null;

            if (_activePopup == null) return;

            // Phase 0: Extract title from title/header container
            _title = ExtractTitle();

            // Shared set to prevent input fields and buttons from overlapping
            var addedObjects = new HashSet<GameObject>();

            // Phase 1: Discover text blocks (non-button TMP_Text, excluding title)
            DiscoverTextBlocks();

            // Phase 2: Discover input fields
            DiscoverInputFields(addedObjects);

            // Phase 3: Discover dropdowns
            DiscoverDropdowns(addedObjects);

            // Phase 4: Discover buttons
            DiscoverButtons(addedObjects);

            // Phase 5: Remove text blocks that duplicate button labels (e.g., cosmetic section
            // headers like "Avatare" that also appear as clickable buttons)
            DeduplicateTextBlocksAgainstButtons();

            // Auto-focus first actionable item (input field, dropdown, or button), otherwise first item
            int firstActionable = _items.FindIndex(i => i.Type == PopupItemType.Button || i.Type == PopupItemType.InputField || i.Type == PopupItemType.Dropdown);
            _currentIndex = firstActionable >= 0 ? firstActionable : (_items.Count > 0 ? 0 : -1);
        }

        private void DiscoverTextBlocks()
        {
            // Check if popup contains DeckCostsDetails - if so, skip its raw text
            // and inject structured deck info from DeckInfoProvider instead
            bool hasDeckCosts = HasComponentInChildren(_activePopup, "DeckCostsDetails");

            // Collect transforms to skip: widgets that instantiate children under a separate
            // ItemParent transform (not a descendant of the component itself), so the
            // walk-up-to-component approach (IsInsideComponentByName) can't find them.
            // Instead, find each component's content transform and use IsChildOf.
            var skipTransforms = new List<Transform>();
            if (hasDeckCosts)
            {
                CollectWidgetContentTransforms(_activePopup, "DeckTypesDetails", "ItemParent", skipTransforms);
                CollectWidgetContentTransforms(_activePopup, "DeckColorsDetails", null, skipTransforms);
                CollectWidgetContentTransforms(_activePopup, "CosmeticSelectorController", null, skipTransforms);
            }

            var seenTexts = new HashSet<string>();

            foreach (var tmp in _activePopup.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;

                // Skip text inside buttons
                if (IsInsideButton(tmp.transform, _activePopup.transform)) continue;

                // Skip text inside input fields (placeholder, input text components)
                if (IsInsideInputField(tmp.transform, _activePopup.transform)) continue;

                // Skip text inside dropdowns (caption text, item labels)
                if (IsInsideDropdown(tmp.transform, _activePopup.transform)) continue;

                // Skip title/header text — it's already announced in "Popup: {title}"
                if (IsInsideTitleContainer(tmp.transform, _activePopup.transform)) continue;

                // Skip text inside DeckCostsDetails — replaced by structured deck info
                if (hasDeckCosts && IsInsideComponentByName(tmp.transform, _activePopup.transform, "DeckCostsDetails"))
                    continue;

                // Skip text inside widget content transforms (DeckTypesDetails ItemParent,
                // DeckColorsDetails, CosmeticSelectorController)
                if (skipTransforms.Count > 0 && IsChildOfAny(tmp.transform, skipTransforms))
                    continue;

                string text = UITextExtractor.CleanText(tmp.text);
                if (string.IsNullOrWhiteSpace(text) || text.Length < 3) continue;

                // Split on newlines for readability
                var lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length < 3) continue;
                    if (seenTexts.Contains(trimmed)) continue;

                    seenTexts.Add(trimmed);
                    _items.Add(new PopupItem
                    {
                        Type = PopupItemType.TextBlock,
                        GameObject = null,
                        Label = trimmed
                    });
                    MelonLogger.Msg($"[{_navigatorId}] PopupHandler: found text block: {trimmed}");
                }
            }

            // Inject structured deck info if we skipped DeckCostsDetails text
            if (hasDeckCosts)
            {
                var deckInfo = DeckInfoProvider.GetDeckInfoElements();
                if (deckInfo != null && deckInfo.Count > 0)
                {
                    foreach (var (label, text) in deckInfo)
                    {
                        string combined = $"{label}: {text}";
                        _items.Add(new PopupItem
                        {
                            Type = PopupItemType.TextBlock,
                            GameObject = null,
                            Label = combined
                        });
                        MelonLogger.Msg($"[{_navigatorId}] PopupHandler: injected deck info: {combined}");
                    }
                }
            }
        }

        private void DiscoverInputFields(HashSet<GameObject> addedObjects)
        {
            var discovered = new List<(GameObject obj, string label, float sortOrder)>();

            foreach (var field in _activePopup.GetComponentsInChildren<TMP_InputField>(true))
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
                _items.Add(new PopupItem
                {
                    Type = PopupItemType.InputField,
                    GameObject = obj,
                    Label = label
                });

                MelonLogger.Msg($"[{_navigatorId}] PopupHandler: found input field: {label}");
            }
        }

        private void DiscoverDropdowns(HashSet<GameObject> addedObjects)
        {
            var discovered = new List<(GameObject obj, string label, float sortOrder)>();

            foreach (var mb in _activePopup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (addedObjects.Contains(mb.gameObject)) continue;

                string typeName = mb.GetType().Name;
                bool isDropdown = typeName == "cTMP_Dropdown" ||
                                  mb is TMPro.TMP_Dropdown ||
                                  mb is Dropdown;

                if (!isDropdown) continue;

                string displayValue = BaseNavigator.GetDropdownDisplayValue(mb.gameObject);
                string label = !string.IsNullOrEmpty(displayValue)
                    ? $"{displayValue}, {Strings.RoleDropdown}"
                    : $"{mb.gameObject.name}, {Strings.RoleDropdown}";

                var pos = mb.gameObject.transform.position;
                discovered.Add((mb.gameObject, label, -pos.y * 1000 + pos.x));
                addedObjects.Add(mb.gameObject);
            }

            foreach (var (obj, label, _) in discovered.OrderBy(x => x.sortOrder))
            {
                _items.Add(new PopupItem
                {
                    Type = PopupItemType.Dropdown,
                    GameObject = obj,
                    Label = label
                });

                MelonLogger.Msg($"[{_navigatorId}] PopupHandler: found dropdown: {label}");
            }
        }

        private void DiscoverButtons(HashSet<GameObject> addedObjects)
        {
            var discovered = new List<(GameObject obj, string label, float sortOrder)>();

            // Pass 1: SystemMessageButtonView (MTGA's standard popup buttons)
            foreach (var mb in _activePopup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (addedObjects.Contains(mb.gameObject)) continue;
                if (IsInsideInputField(mb.transform, _activePopup.transform)) continue;
                if (IsInsideDropdown(mb.transform, _activePopup.transform)) continue;

                if (mb.GetType().Name == "SystemMessageButtonView")
                {
                    string label = UITextExtractor.GetText(mb.gameObject);
                    if (string.IsNullOrEmpty(label)) label = mb.gameObject.name;

                    var pos = mb.gameObject.transform.position;
                    discovered.Add((mb.gameObject, label, -pos.y * 1000 + pos.x));
                    addedObjects.Add(mb.gameObject);
                }
            }

            // Pass 2: CustomButton / CustomButtonWithTooltip
            foreach (var mb in _activePopup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (addedObjects.Contains(mb.gameObject)) continue;
                if (IsInsideInputField(mb.transform, _activePopup.transform)) continue;
                if (IsInsideDropdown(mb.transform, _activePopup.transform)) continue;
                if (IsInsideButton(mb.transform, _activePopup.transform)) continue;

                string typeName = mb.GetType().Name;
                if (typeName == "CustomButton" || typeName == "CustomButtonWithTooltip")
                {
                    string label = UITextExtractor.GetText(mb.gameObject);
                    if (string.IsNullOrEmpty(label)) label = mb.gameObject.name;

                    var pos = mb.gameObject.transform.position;
                    discovered.Add((mb.gameObject, label, -pos.y * 1000 + pos.x));
                    addedObjects.Add(mb.gameObject);
                }
            }

            // Pass 3: Standard Unity Buttons
            foreach (var button in _activePopup.GetComponentsInChildren<Button>(true))
            {
                if (button == null || !button.gameObject.activeInHierarchy || !button.interactable) continue;
                if (addedObjects.Contains(button.gameObject)) continue;
                if (IsInsideInputField(button.transform, _activePopup.transform)) continue;
                if (IsInsideDropdown(button.transform, _activePopup.transform)) continue;
                if (IsInsideButton(button.transform, _activePopup.transform)) continue;

                string label = UITextExtractor.GetText(button.gameObject);
                if (string.IsNullOrEmpty(label)) label = button.gameObject.name;

                var pos = button.gameObject.transform.position;
                discovered.Add((button.gameObject, label, -pos.y * 1000 + pos.x));
                addedObjects.Add(button.gameObject);
            }

            // Sort by position, skip dismiss overlays, deduplicate by label
            var seenLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (obj, label, _) in discovered.OrderBy(x => x.sortOrder))
            {
                // Skip background dismiss overlays (click-outside-to-close areas)
                if (IsDismissOverlay(obj))
                {
                    MelonLogger.Msg($"[{_navigatorId}] PopupHandler: skipping dismiss overlay: {obj.name}");
                    continue;
                }

                if (!seenLabels.Add(label))
                {
                    MelonLogger.Msg($"[{_navigatorId}] PopupHandler: skipping duplicate button: {label}");
                    continue;
                }

                _items.Add(new PopupItem
                {
                    Type = PopupItemType.Button,
                    GameObject = obj,
                    Label = label
                });

                MelonLogger.Msg($"[{_navigatorId}] PopupHandler: found button: {label}");
            }
        }

        /// <summary>
        /// Remove text blocks whose label exactly matches a button label.
        /// Handles cases like cosmetic section headers (Avatare, Begleiter, Kartenhüllen)
        /// that appear as both standalone text and clickable buttons.
        /// </summary>
        private void DeduplicateTextBlocksAgainstButtons()
        {
            var buttonLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in _items)
            {
                if (item.Type == PopupItemType.Button)
                    buttonLabels.Add(item.Label);
            }

            if (buttonLabels.Count == 0) return;

            int removed = _items.RemoveAll(item =>
                item.Type == PopupItemType.TextBlock && buttonLabels.Contains(item.Label));

            if (removed > 0)
                MelonLogger.Msg($"[{_navigatorId}] PopupHandler: removed {removed} text blocks duplicating button labels");
        }

        #endregion

        #region Navigation

        private void NavigateItem(int direction)
        {
            if (_items.Count == 0) return;

            int newIndex = _currentIndex + direction;

            if (newIndex < 0)
            {
                _announcer?.AnnounceInterruptVerbose(Strings.BeginningOfList);
                return;
            }

            if (newIndex >= _items.Count)
            {
                _announcer?.AnnounceInterruptVerbose(Strings.EndOfList);
                return;
            }

            _currentIndex = newIndex;
            AnnounceCurrentItem();
        }

        private void ActivateCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];
            if (item.Type == PopupItemType.TextBlock)
            {
                // Re-read text block
                AnnounceCurrentItem();
            }
            else if (item.Type == PopupItemType.InputField && item.GameObject != null)
            {
                _inputHelper.EnterEditMode(item.GameObject);
            }
            else if (item.Type == PopupItemType.Dropdown && item.GameObject != null)
            {
                _dropdownHelper.EnterEditMode(item.GameObject);
            }
            else if (item.Type == PopupItemType.Button && item.GameObject != null)
            {
                MelonLogger.Msg($"[{_navigatorId}] PopupHandler: activating button: {item.Label}");
                _announcer?.AnnounceInterrupt(Strings.Activating(item.Label));
                UIActivator.Activate(item.GameObject);
            }
        }

        #endregion

        #region Announcements

        private void AnnouncePopupOpen()
        {
            // Use extracted title, fall back to first text block
            string context = _title;
            if (string.IsNullOrEmpty(context))
            {
                foreach (var item in _items)
                {
                    if (item.Type == PopupItemType.TextBlock)
                    {
                        context = item.Label;
                        break;
                    }
                }
            }

            string announcement;
            if (!string.IsNullOrEmpty(context))
                announcement = $"Popup: {context}. {_items.Count} items.";
            else
                announcement = $"Popup. {_items.Count} items.";

            _announcer?.AnnounceInterrupt(announcement);

            // Auto-announce focused item (first button)
            if (_currentIndex >= 0 && _currentIndex < _items.Count)
            {
                AnnounceCurrentItem();
            }
        }

        private void AnnounceCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];
            string label = item.Label;

            // For input fields, refresh label with current text content (same as BaseNavigator)
            if (item.Type == PopupItemType.InputField && item.GameObject != null)
            {
                label = BaseNavigator.RefreshElementLabel(item.GameObject, label, UIElementClassifier.ElementRole.TextField);
            }
            else if (item.Type == PopupItemType.Dropdown && item.GameObject != null)
            {
                // Re-read current dropdown value
                string currentValue = BaseNavigator.GetDropdownDisplayValue(item.GameObject);
                if (!string.IsNullOrEmpty(currentValue))
                    label = $"{currentValue}, {Strings.RoleDropdown}";
                else
                    label = $"{label}, {Strings.RoleDropdown}";
            }
            else
            {
                if (item.Type == PopupItemType.Button)
                    label = BaseNavigator.BuildLabel(label, Models.Strings.RoleButton, UIElementClassifier.ElementRole.Button);
            }

            _announcer?.Announce(
                $"{label}, {_currentIndex + 1} of {_items.Count}",
                AnnouncementPriority.Normal);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Extract the popup title from a title/header container.
        /// Returns null if no title container found.
        /// </summary>
        private string ExtractTitle()
        {
            if (_activePopup == null) return null;

            foreach (var tmp in _activePopup.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;
                if (!IsInsideTitleContainer(tmp.transform, _activePopup.transform)) continue;

                string text = UITextExtractor.CleanText(tmp.text);
                if (!string.IsNullOrWhiteSpace(text) && text.Length >= 3)
                    return text.Trim();
            }

            return null;
        }

        /// <summary>
        /// Check if a transform is inside a title/header container.
        /// Walks up from child to stopAt (exclusive), checking for "Title" or "Header" in names.
        /// </summary>
        private static bool IsInsideTitleContainer(Transform child, Transform stopAt)
        {
            // Check the element itself and all parents up to the popup root
            Transform current = child;
            while (current != null && current != stopAt)
            {
                string name = current.name;
                if (name.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Header", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Check if a transform is inside a button (CustomButton, CustomButtonWithTooltip,
        /// SystemMessageButtonView, or Unity Button).
        /// Walks up from child to stopAt (exclusive).
        /// </summary>
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
                        if (typeName == "CustomButton" || typeName == "CustomButtonWithTooltip" ||
                            typeName == "SystemMessageButtonView")
                            return true;
                    }
                }

                // Also check Unity Button
                if (current.GetComponent<Button>() != null)
                    return true;

                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Check if a transform is inside a dropdown component (TMP_Dropdown, Dropdown, or cTMP_Dropdown).
        /// Walks up from child to stopAt (exclusive).
        /// </summary>
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

        /// <summary>
        /// Check if a transform is inside an input field (TMP_InputField).
        /// Walks up from child to stopAt (exclusive).
        /// </summary>
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

        /// <summary>
        /// Find the cancel/close/no button in a popup using 3-pass search by button type.
        /// </summary>
        private GameObject FindPopupCancelButton(GameObject popup)
        {
            if (popup == null) return null;

            string[] cancelPatterns = { "cancel", "close", "back", "no", "abbrechen", "nein", "zurück" };

            // Pass 1: SystemMessageButtonView
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == "SystemMessageButtonView")
                {
                    if (MatchesCancelPattern(mb.gameObject, cancelPatterns))
                        return mb.gameObject;
                }
            }

            // Pass 2: CustomButton / CustomButtonWithTooltip
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                string typeName = mb.GetType().Name;
                if (typeName == "CustomButton" || typeName == "CustomButtonWithTooltip")
                {
                    if (MatchesCancelPattern(mb.gameObject, cancelPatterns))
                        return mb.gameObject;
                }
            }

            // Pass 3: Standard Unity Buttons
            foreach (var button in popup.GetComponentsInChildren<Button>(true))
            {
                if (button == null || !button.gameObject.activeInHierarchy || !button.interactable) continue;
                if (MatchesCancelPattern(button.gameObject, cancelPatterns))
                    return button.gameObject;
            }

            // Pass 4: Find _cancelButton field via reflection on popup controllers
            // Handles popups like CardViewerController where the X/close button has no
            // discoverable text and isn't found by pattern matching.
            foreach (var mb in popup.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                var field = mb.GetType().GetField("_cancelButton",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field == null) continue;

                if (field.GetValue(mb) is MonoBehaviour cancelMb && cancelMb != null
                    && cancelMb.gameObject != null)
                {
                    MelonLogger.Msg($"[{_navigatorId}] PopupHandler: found _cancelButton via reflection on {mb.GetType().Name}");
                    return cancelMb.gameObject;
                }
            }

            return null;
        }

        private static bool MatchesCancelPattern(GameObject obj, string[] patterns)
        {
            string buttonText = UITextExtractor.GetText(obj)?.ToLower() ?? "";
            string buttonName = obj.name.ToLower();

            foreach (var pattern in patterns)
            {
                if (ContainsWord(buttonText, pattern) || ContainsWord(buttonName, pattern))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Check if text contains pattern as a whole word (not as a substring of a larger word).
        /// E.g., "no" matches "No" and "say no" but not "button_outline" or "favorite".
        /// </summary>
        private static bool ContainsWord(string text, string word)
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

        /// <summary>
        /// Find SystemMessageView component: children -> parents -> scene-wide.
        /// </summary>
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

        /// <summary>
        /// Invoke OnBack(null) on a component via reflection.
        /// </summary>
        private bool TryInvokeOnBack(MonoBehaviour component)
        {
            if (component == null) return false;

            var type = component.GetType();
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (method.Name == "OnBack" && method.GetParameters().Length == 1)
                {
                    try
                    {
                        MelonLogger.Msg($"[{_navigatorId}] PopupHandler: invoking {type.Name}.OnBack(null)");
                        method.Invoke(component, new object[] { null });
                        return true;
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[{_navigatorId}] PopupHandler: OnBack error: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
            }

            return false;
        }

        private int CountTextBlocks() => _items.Count(i => i.Type == PopupItemType.TextBlock);
        private int CountButtons() => _items.Count(i => i.Type == PopupItemType.Button);
        private int CountInputFields() => _items.Count(i => i.Type == PopupItemType.InputField);
        private int CountDropdowns() => _items.Count(i => i.Type == PopupItemType.Dropdown);

        /// <summary>
        /// Check if a GameObject has a child component with the given type name.
        /// </summary>
        private static bool HasComponentInChildren(GameObject go, string typeName)
        {
            if (go == null) return false;
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && mb.GetType().Name == typeName)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Check if a transform is inside a component with the given type name.
        /// Walks up from child to stopAt (exclusive).
        /// </summary>
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

        /// <summary>
        /// Check if a button is a full-screen dismiss overlay (click-outside-to-close).
        /// These have no useful functionality beyond Backspace dismissal.
        /// </summary>
        private static bool IsDismissOverlay(GameObject obj)
        {
            string name = obj.name.ToLower();
            return name.Contains("background") || name.Contains("overlay") || name.Contains("backdrop") || name.Contains("dismiss");
        }

        /// <summary>
        /// Find a component by type name and collect its content transform(s) for text filtering.
        /// If fieldName is specified, gets that Transform field from the component.
        /// Otherwise, uses the component's own transform.
        /// </summary>
        private static void CollectWidgetContentTransforms(GameObject popup, string componentTypeName,
            string fieldName, List<Transform> skipTransforms)
        {
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.GetType().Name != componentTypeName) continue;

                if (fieldName != null)
                {
                    var field = mb.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
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
        /// Check if a transform is a descendant of any transform in the list.
        /// </summary>
        private static bool IsChildOfAny(Transform child, List<Transform> parents)
        {
            foreach (var parent in parents)
            {
                if (child.IsChildOf(parent))
                    return true;
            }
            return false;
        }

        #endregion
    }
}
