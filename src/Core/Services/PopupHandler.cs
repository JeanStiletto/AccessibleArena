using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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

        private enum PopupItemType { TextBlock, Button, InputField }

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

        private GameObject _activePopup;
        private readonly List<PopupItem> _items = new List<PopupItem>();
        private int _currentIndex;
        private string _title;

        // Input field edit mode
        private bool _isEditing;
        private GameObject _editingField;
        private string _prevText;
        private int _prevCaretPos;

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

            DiscoverItems();

            MelonLogger.Msg($"[{_navigatorId}] PopupHandler: {CountTextBlocks()} text blocks, {CountInputFields()} input fields, {CountButtons()} buttons");

            AnnouncePopupOpen();
        }

        /// <summary>
        /// Reset all state. Call when popup closes or navigator deactivates.
        /// </summary>
        public void Clear()
        {
            if (_isEditing)
                ExitEditMode();

            _activePopup = null;
            _items.Clear();
            _currentIndex = -1;
            _title = null;
            _prevText = null;
            _prevCaretPos = 0;
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

            // Input field edit mode intercepts all keys first
            if (_isEditing)
            {
                bool consumed = HandleInputFieldEditing();
                TrackInputFieldState();
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

            // Phase 3: Discover buttons
            DiscoverButtons(addedObjects);

            // Auto-focus first actionable item (input field or button), otherwise first item
            int firstActionable = _items.FindIndex(i => i.Type == PopupItemType.Button || i.Type == PopupItemType.InputField);
            _currentIndex = firstActionable >= 0 ? firstActionable : (_items.Count > 0 ? 0 : -1);
        }

        private void DiscoverTextBlocks()
        {
            var seenTexts = new HashSet<string>();

            foreach (var tmp in _activePopup.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;

                // Skip text inside buttons
                if (IsInsideButton(tmp.transform, _activePopup.transform)) continue;

                // Skip text inside input fields (placeholder, input text components)
                if (IsInsideInputField(tmp.transform, _activePopup.transform)) continue;

                // Skip title/header text — it's already announced in "Popup: {title}"
                if (IsInsideTitleContainer(tmp.transform, _activePopup.transform)) continue;

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

        private void DiscoverButtons(HashSet<GameObject> addedObjects)
        {
            var discovered = new List<(GameObject obj, string label, float sortOrder)>();

            // Pass 1: SystemMessageButtonView (MTGA's standard popup buttons)
            foreach (var mb in _activePopup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (addedObjects.Contains(mb.gameObject)) continue;

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

                string label = UITextExtractor.GetText(button.gameObject);
                if (string.IsNullOrEmpty(label)) label = button.gameObject.name;

                var pos = button.gameObject.transform.position;
                discovered.Add((button.gameObject, label, -pos.y * 1000 + pos.x));
                addedObjects.Add(button.gameObject);
            }

            // Sort by position and add as PopupItems
            foreach (var (obj, label, _) in discovered.OrderBy(x => x.sortOrder))
            {
                _items.Add(new PopupItem
                {
                    Type = PopupItemType.Button,
                    GameObject = obj,
                    Label = label
                });

                MelonLogger.Msg($"[{_navigatorId}] PopupHandler: found button: {label}");
            }
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
                EnterEditMode(item.GameObject);
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
            string suffix = item.Type == PopupItemType.Button ? ", button"
                          : item.Type == PopupItemType.InputField ? ", text field"
                          : "";
            _announcer?.Announce(
                $"{_currentIndex + 1} of {_items.Count}: {item.Label}{suffix}",
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

            string[] cancelPatterns = { "cancel", "close", "no", "abbrechen", "nein", "zurück" };

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

            return null;
        }

        private static bool MatchesCancelPattern(GameObject obj, string[] patterns)
        {
            string buttonText = UITextExtractor.GetText(obj)?.ToLower() ?? "";
            string buttonName = obj.name.ToLower();

            foreach (var pattern in patterns)
            {
                if (buttonText.Contains(pattern) || buttonName.Contains(pattern))
                    return true;
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

        #endregion

        #region Input Field Editing

        private void EnterEditMode(GameObject field)
        {
            _isEditing = true;
            _editingField = field;
            UIFocusTracker.EnterInputFieldEditMode(field);
            UIActivator.Activate(field);
            _announcer?.Announce(Strings.EditingTextField, AnnouncementPriority.Normal);
            TrackInputFieldState();
        }

        private void ExitEditMode()
        {
            _isEditing = false;
            UIFocusTracker.ExitInputFieldEditMode();
            UIFocusTracker.DeactivateFocusedInputField();
            _editingField = null;
        }

        /// <summary>
        /// Handle keys while editing an input field. Returns true if consumed.
        /// </summary>
        private bool HandleInputFieldEditing()
        {
            // Escape: exit edit mode
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ExitEditMode();
                _announcer?.Announce(Strings.ExitedEditMode, AnnouncementPriority.Normal);
                return true;
            }

            // Tab/Shift+Tab: exit edit mode and navigate
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bool shiftTab = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                ExitEditMode();
                NavigateItem(shiftTab ? -1 : 1);
                return true;
            }

            // Backspace: announce deleted char, pass through for actual deletion
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                AnnounceDeletedChar();
                return false;
            }

            // Up/Down: announce field content, reactivate field
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                AnnounceFieldContent();
                ReactivateField();
                return true;
            }

            // Left/Right: announce character at cursor
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                AnnounceCharAtCursor();
                return true;
            }

            // All other keys pass through for typing
            return false;
        }

        /// <summary>
        /// Track input field text/caret for next frame's Backspace detection.
        /// </summary>
        private void TrackInputFieldState()
        {
            var (text, caretPos, _, isValid) = GetFieldInfo();
            if (isValid)
            {
                _prevText = text ?? "";
                _prevCaretPos = caretPos;
            }
            else
            {
                _prevText = "";
                _prevCaretPos = 0;
            }
        }

        /// <summary>
        /// Get info from the editing input field.
        /// </summary>
        private (string text, int caretPos, bool isPassword, bool isValid) GetFieldInfo()
        {
            if (_editingField == null)
                return (null, 0, false, false);

            bool inEditMode = _isEditing && UIFocusTracker.IsEditingInputField();

            var tmpInput = _editingField.GetComponent<TMP_InputField>();
            if (tmpInput != null && (tmpInput.isFocused || inEditMode))
            {
                int caret = tmpInput.isFocused ? tmpInput.stringPosition : (tmpInput.text?.Length ?? 0);
                bool isPw = tmpInput.inputType == TMP_InputField.InputType.Password;
                return (tmpInput.text, caret, isPw, true);
            }

            return (null, 0, false, false);
        }

        private void AnnounceDeletedChar()
        {
            var (currentText, _, isPassword, isValid) = GetFieldInfo();
            if (!isValid) return;

            currentText = currentText ?? "";
            string prevText = _prevText ?? "";

            if (prevText.Length <= currentText.Length)
                return;

            if (isPassword)
            {
                _announcer?.AnnounceInterrupt(Strings.InputFieldStar);
                return;
            }

            char deletedChar = FindDeletedCharacter(prevText, currentText, _prevCaretPos);
            _announcer?.AnnounceInterrupt(Strings.GetCharacterName(deletedChar));
        }

        private char FindDeletedCharacter(string prevText, string currentText, int prevCaretPos)
        {
            int deletedIndex = prevCaretPos - 1;
            if (deletedIndex >= 0 && deletedIndex < prevText.Length)
                return prevText[deletedIndex];

            for (int i = 0; i < currentText.Length; i++)
            {
                if (i >= prevText.Length || prevText[i] != currentText[i])
                {
                    if (i < prevText.Length)
                        return prevText[i];
                    break;
                }
            }

            if (currentText.Length < prevText.Length)
                return prevText[currentText.Length];

            return '?';
        }

        private void AnnounceCharAtCursor()
        {
            var (text, caretPos, isPassword, isValid) = GetFieldInfo();
            if (!isValid) return;

            bool isLeft = Input.GetKeyDown(KeyCode.LeftArrow);
            bool isRight = Input.GetKeyDown(KeyCode.RightArrow);

            if (string.IsNullOrEmpty(text))
            {
                _announcer?.AnnounceInterrupt(Strings.InputFieldEmpty);
                return;
            }

            if (isPassword)
            {
                if (caretPos == 0 && isLeft)
                    _announcer?.AnnounceInterrupt(Strings.InputFieldStart);
                else if (caretPos >= text.Length && isRight)
                    _announcer?.AnnounceInterrupt(Strings.InputFieldEnd);
                else
                    _announcer?.AnnounceInterrupt(Strings.InputFieldStar);
                return;
            }

            if (caretPos == 0 && isLeft)
                _announcer?.AnnounceInterrupt(Strings.InputFieldStart);
            else if (caretPos >= text.Length && isRight)
                _announcer?.AnnounceInterrupt(Strings.InputFieldEnd);
            else if (caretPos >= 0 && caretPos < text.Length)
                _announcer?.AnnounceInterrupt(Strings.GetCharacterName(text[caretPos]));
            else
                _announcer?.AnnounceInterrupt(Strings.InputFieldEnd);
        }

        private void AnnounceFieldContent()
        {
            var (content, _, isPassword, isValid) = GetFieldInfo();
            if (!isValid) return;

            string label = UITextExtractor.GetInputFieldLabel(_editingField);

            if (isPassword)
            {
                string announcement = string.IsNullOrEmpty(content)
                    ? Strings.InputFieldEmptyWithLabel(label)
                    : Strings.InputFieldPasswordWithCount(label, content.Length);
                _announcer?.AnnounceInterrupt(announcement);
            }
            else
            {
                string announcement = string.IsNullOrEmpty(content)
                    ? Strings.InputFieldEmptyWithLabel(label)
                    : Strings.InputFieldContent(label, content);
                _announcer?.AnnounceInterrupt(announcement);
            }
        }

        private void ReactivateField()
        {
            if (_editingField == null || !_editingField.activeInHierarchy) return;

            var tmpInput = _editingField.GetComponent<TMP_InputField>();
            if (tmpInput != null && !tmpInput.isFocused)
            {
                var eventSystem = EventSystem.current;
                if (eventSystem != null)
                    eventSystem.SetSelectedGameObject(_editingField);
                tmpInput.ActivateInputField();
            }
        }

        #endregion
    }
}
