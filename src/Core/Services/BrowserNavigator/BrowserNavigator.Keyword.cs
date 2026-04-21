using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Models;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    public partial class BrowserNavigator
    {
        // KeywordSelection browser state (creature type picker)
        private bool _isKeywordSelection;
        private MonoBehaviour _keywordFilterRef;
        private int _currentKeywordIndex = -1;
        // Letter-jump (menu-style A-Z) for KeywordSelection's show-all phase
        private readonly LetterSearchHandler _keywordLetterSearch = new LetterSearchHandler();

        // KeywordFilter/ChoiceFilter reflection cache
        // Game renamed KeywordFilter → ChoiceFilter (nested Keyword → Choice)
        private static Type _keywordFilterType;

        private sealed class KeywordHandles
        {
            public FieldInfo FilteredKeywords;     // _filteredChoices
            public FieldInfo SelectedKeywords;     // _selectedChoices
            public FieldInfo FilterInput;          // FilterInput
            public FieldInfo ShowAll;              // _showAllChoices
            public MethodInfo OnFilterSubmitted;
            public FieldInfo KeywordDisplayText;   // nested Choice.DisplayText
            public FieldInfo KeywordSearchText;    // nested Choice.SearchText
        }

        private static readonly ReflectionCache<KeywordHandles> _keywordCache = new ReflectionCache<KeywordHandles>(
            builder: filterType =>
            {
                var h = new KeywordHandles
                {
                    FilteredKeywords = filterType.GetField("_filteredChoices", PrivateInstance),
                    SelectedKeywords = filterType.GetField("_selectedChoices", PrivateInstance),
                    FilterInput = filterType.GetField("FilterInput", PrivateInstance),
                    ShowAll = filterType.GetField("_showAllChoices", PrivateInstance),
                    OnFilterSubmitted = filterType.GetMethod("OnFilterSubmitted", PrivateInstance),
                };
                var choiceType = filterType.GetNestedType("Choice", BindingFlags.Public);
                if (choiceType != null)
                {
                    h.KeywordDisplayText = choiceType.GetField("DisplayText", PublicInstance);
                    h.KeywordSearchText = choiceType.GetField("SearchText", PublicInstance);
                }
                return h;
            },
            validator: _ => true,
            logTag: "BrowserNavigator",
            logSubject: "ChoiceFilter");

        /// <summary>
        /// One-time initialization of ChoiceFilter type + reflection cache.
        /// </summary>
        private static void InitKeywordReflection()
        {
            if (_keywordCache.IsInitialized) return;

            _keywordFilterType = FindType("Wotc.Mtga.DuelScene.Interactions.ChoiceFilter");
            if (_keywordFilterType == null)
            {
                Log.Warn("BrowserNavigator", "ChoiceFilter type not found");
                return;
            }
            _keywordCache.EnsureInitialized(_keywordFilterType);
        }

        /// <summary>
        /// Finds the KeywordFilter component on the browser scaffold and caches it.
        /// Also deactivates the TMP_InputField to prevent it from stealing keyboard focus.
        /// </summary>
        private void CacheKeywordFilterState()
        {
            InitKeywordReflection();
            if (_keywordFilterType == null) return;

            try
            {
                // Find KeywordFilter MonoBehaviour in the scaffold
                if (_browserInfo?.BrowserGameObject != null)
                {
                    foreach (var mb in _browserInfo.BrowserGameObject.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (mb != null && mb.GetType() == _keywordFilterType)
                        {
                            _keywordFilterRef = mb;
                            break;
                        }
                    }
                }

                if (_keywordFilterRef == null)
                {
                    // Fallback: search scene
                    foreach (var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
                    {
                        if (mb != null && mb.GetType() == _keywordFilterType)
                        {
                            _keywordFilterRef = mb;
                            break;
                        }
                    }
                }

                if (_keywordFilterRef == null)
                {
                    Log.Warn("BrowserNavigator", "ChoiceFilter component not found");
                    _isKeywordSelection = false;
                    return;
                }

                Log.Msg("BrowserNavigator", $"Found ChoiceFilter: {_keywordFilterRef.gameObject.name}");

                // Deactivate the TMP_InputField to prevent it from stealing keyboard focus
                DeactivateKeywordInputField();

                // Deactivate CardInfoNavigator to prevent Up/Down interference
                AccessibleArenaMod.Instance?.CardNavigator?.Deactivate();
            }
            catch (Exception ex)
            {
                Log.Error("BrowserNavigator", $"CacheKeywordFilterState error: {ex.Message}");
                _isKeywordSelection = false;
            }
        }

        /// <summary>
        /// Deactivates the TMP_InputField on the KeywordFilter to prevent it from
        /// capturing keyboard input intended for our navigator.
        /// </summary>
        private void DeactivateKeywordInputField()
        {
            var h = _keywordCache.Handles;
            if (_keywordFilterRef == null || h?.FilterInput == null) return;

            try
            {
                var filterInput = h.FilterInput.GetValue(_keywordFilterRef);
                if (filterInput == null) return;

                var deactivateMethod = filterInput.GetType().GetMethod("DeactivateInputField",
                    BindingFlags.Public | BindingFlags.Instance);
                deactivateMethod?.Invoke(filterInput, null);

                Log.Msg("BrowserNavigator", "Deactivated KeywordFilter InputField");
            }
            catch (Exception ex)
            {
                Log.Warn("BrowserNavigator", $"Error deactivating InputField: {ex.Message}");
            }
        }

        /// <summary>
        /// True when the KeywordSelection browser is in its "Show All" phase
        /// (ShowAllButton was activated, list expanded from shortlist to full set).
        /// Reads the language-agnostic <c>_showAllChoices</c> bool on ChoiceFilter.
        /// </summary>
        private bool IsKeywordShowAllActive()
        {
            var h = _keywordCache.Handles;
            if (!_isKeywordSelection || _keywordFilterRef == null || h?.ShowAll == null)
                return false;
            try
            {
                return h.ShowAll.GetValue(_keywordFilterRef) is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the count of currently filtered keywords.
        /// </summary>
        private int GetKeywordCount()
        {
            var h = _keywordCache.Handles;
            if (_keywordFilterRef == null || h?.FilteredKeywords == null) return 0;
            var list = h.FilteredKeywords.GetValue(_keywordFilterRef) as IList;
            return list?.Count ?? 0;
        }

        /// <summary>
        /// Gets the DisplayText of a keyword at the given index.
        /// </summary>
        private string GetKeywordDisplayText(int index)
        {
            var h = _keywordCache.Handles;
            if (_keywordFilterRef == null || h?.FilteredKeywords == null || h.KeywordDisplayText == null)
                return null;

            var list = h.FilteredKeywords.GetValue(_keywordFilterRef) as IList;
            if (list == null || index < 0 || index >= list.Count) return null;

            var keyword = list[index]; // boxed struct
            return h.KeywordDisplayText.GetValue(keyword) as string;
        }

        /// <summary>
        /// Checks if a keyword at the given index is currently selected.
        /// </summary>
        private bool IsKeywordSelected(int index)
        {
            var h = _keywordCache.Handles;
            if (_keywordFilterRef == null || h?.FilteredKeywords == null || h.SelectedKeywords == null)
                return false;

            var filteredList = h.FilteredKeywords.GetValue(_keywordFilterRef) as IList;
            var selectedList = h.SelectedKeywords.GetValue(_keywordFilterRef) as IList;
            if (filteredList == null || selectedList == null) return false;
            if (index < 0 || index >= filteredList.Count) return false;

            var keyword = filteredList[index];
            return selectedList.Contains(keyword);
        }

        /// <summary>
        /// Toggles the currently focused keyword by setting the filter text to isolate
        /// the keyword, then invoking OnFilterSubmitted to toggle it.
        /// This approach works even when the keyword is not currently visible in the
        /// InfiniteScroll viewport.
        /// </summary>
        private void ToggleCurrentKeyword()
        {
            var h = _keywordCache.Handles;
            if (_keywordFilterRef == null || h?.FilterInput == null ||
                h.OnFilterSubmitted == null || h.KeywordSearchText == null)
                return;

            int count = GetKeywordCount();
            if (_currentKeywordIndex < 0 || _currentKeywordIndex >= count) return;

            try
            {
                // Get the keyword's SearchText for filtering
                var filteredList = h.FilteredKeywords.GetValue(_keywordFilterRef) as IList;
                if (filteredList == null || _currentKeywordIndex >= filteredList.Count) return;

                var keyword = filteredList[_currentKeywordIndex];
                string searchText = h.KeywordSearchText.GetValue(keyword) as string;
                string displayText = h.KeywordDisplayText?.GetValue(keyword) as string;
                if (string.IsNullOrEmpty(searchText)) return;

                // Get the filter input and save current text
                var filterInput = h.FilterInput.GetValue(_keywordFilterRef);
                if (filterInput == null) return;

                var textProp = filterInput.GetType().GetProperty("text", PublicInstance);
                if (textProp == null) return;

                string savedFilter = textProp.GetValue(filterInput) as string ?? "";

                // Set filter to the keyword's search text.
                // This triggers onValueChanged → OnFilterChanged → rebuilds _filteredKeywords
                // and re-renders the InfiniteScroll with matching items.
                textProp.SetValue(filterInput, searchText);

                // Now call OnFilterSubmitted which finds the best match and toggles it
                h.OnFilterSubmitted.Invoke(_keywordFilterRef, new object[] { searchText });

                // Restore the original filter to bring back the full list
                textProp.SetValue(filterInput, savedFilter);

                // Re-deactivate the input field (setting text may reactivate it)
                DeactivateKeywordInputField();

                // Read updated selection state and announce
                // After restoring filter, _filteredKeywords is rebuilt. Our keyword index
                // should still be valid if the same filter is restored.
                bool isNowSelected = IsKeywordSelected(_currentKeywordIndex);
                string state = isNowSelected ? Strings.KeywordSelectionSelected : Strings.Deselected;
                _announcer.AnnounceInterrupt(Strings.KeywordSelectionToggled(displayText ?? searchText, state));

                Log.Msg("BrowserNavigator", $"KeywordSelection toggled: '{displayText}' → {state}");
            }
            catch (Exception ex)
            {
                Log.Error("BrowserNavigator", $"ToggleCurrentKeyword error: {ex.Message}");
            }
        }

        /// <summary>
        /// Announces the currently focused keyword with selection state and position.
        /// </summary>
        private void AnnounceCurrentKeyword()
        {
            int count = GetKeywordCount();
            if (_currentKeywordIndex < 0 || _currentKeywordIndex >= count) return;

            string displayText = GetKeywordDisplayText(_currentKeywordIndex);
            if (string.IsNullOrEmpty(displayText)) return;

            bool isSelected = IsKeywordSelected(_currentKeywordIndex);
            string pos = Strings.PositionOf(_currentKeywordIndex + 1, count, force: true);
            string position = pos != "" ? $", {pos}" : "";
            string selState = isSelected ? $", {Strings.KeywordSelectionSelected}" : "";

            _announcer.Announce($"{displayText}{selState}{position}", AnnouncementPriority.High);
        }

        /// <summary>
        /// Handles keyboard input for the KeywordSelection browser.
        /// Tab/Left/Right navigate keywords, Enter toggles, Space confirms, Backspace cancels.
        /// Returns true if input was consumed.
        /// </summary>
        private bool HandleKeywordSelectionInput()
        {
            int kwCount = GetKeywordCount();
            bool showAll = IsKeywordShowAllActive();

            // Tab: cycle through keywords → buttons → wrap
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                _keywordLetterSearch.Clear();
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                if (shift)
                {
                    // Shift+Tab backwards
                    if (_currentButtonIndex >= 0)
                    {
                        // On buttons: go to previous button, or wrap to last keyword
                        if (_currentButtonIndex > 0)
                        {
                            _currentButtonIndex--;
                            AnnounceCurrentButton();
                        }
                        else if (kwCount > 0)
                        {
                            _currentButtonIndex = -1;
                            _currentKeywordIndex = kwCount - 1;
                            AnnounceCurrentKeyword();
                        }
                        else
                        {
                            _currentButtonIndex = _browserButtons.Count - 1;
                            AnnounceCurrentButton();
                        }
                    }
                    else if (_currentKeywordIndex > 0)
                    {
                        _currentKeywordIndex--;
                        AnnounceCurrentKeyword();
                    }
                    else if (_browserButtons.Count > 0)
                    {
                        // Wrap from first keyword to last button
                        _currentKeywordIndex = -1;
                        _currentButtonIndex = _browserButtons.Count - 1;
                        AnnounceCurrentButton();
                    }
                    else if (kwCount > 0)
                    {
                        _currentKeywordIndex = kwCount - 1;
                        AnnounceCurrentKeyword();
                    }
                }
                else
                {
                    // Tab forward
                    if (_currentKeywordIndex >= 0)
                    {
                        if (_currentKeywordIndex < kwCount - 1)
                        {
                            _currentKeywordIndex++;
                            AnnounceCurrentKeyword();
                        }
                        else if (_browserButtons.Count > 0)
                        {
                            _currentKeywordIndex = -1;
                            _currentButtonIndex = 0;
                            AnnounceCurrentButton();
                        }
                        else
                        {
                            // Wrap to first keyword
                            _currentKeywordIndex = 0;
                            AnnounceCurrentKeyword();
                        }
                    }
                    else if (_currentButtonIndex >= 0)
                    {
                        if (_currentButtonIndex < _browserButtons.Count - 1)
                        {
                            _currentButtonIndex++;
                            AnnounceCurrentButton();
                        }
                        else if (kwCount > 0)
                        {
                            // Wrap to first keyword
                            _currentButtonIndex = -1;
                            _currentKeywordIndex = 0;
                            AnnounceCurrentKeyword();
                        }
                        else
                        {
                            _currentButtonIndex = 0;
                            AnnounceCurrentButton();
                        }
                    }
                    else if (kwCount > 0)
                    {
                        _currentKeywordIndex = 0;
                        AnnounceCurrentKeyword();
                    }
                    else if (_browserButtons.Count > 0)
                    {
                        _currentButtonIndex = 0;
                        AnnounceCurrentButton();
                    }
                }
                return true;
            }

            // Left/Right: navigate within keywords or buttons
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                _keywordLetterSearch.Clear();
                if (_currentKeywordIndex > 0)
                {
                    _currentKeywordIndex--;
                    AnnounceCurrentKeyword();
                }
                else if (_currentButtonIndex > 0)
                {
                    _currentButtonIndex--;
                    AnnounceCurrentButton();
                }
                return true;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                _keywordLetterSearch.Clear();
                if (_currentKeywordIndex >= 0 && _currentKeywordIndex < kwCount - 1)
                {
                    _currentKeywordIndex++;
                    AnnounceCurrentKeyword();
                }
                else if (_currentButtonIndex >= 0 && _currentButtonIndex < _browserButtons.Count - 1)
                {
                    _currentButtonIndex++;
                    AnnounceCurrentButton();
                }
                return true;
            }

            // Home/End: jump to first/last keyword
            if (Input.GetKeyDown(KeyCode.Home))
            {
                _keywordLetterSearch.Clear();
                if (kwCount > 0)
                {
                    _currentKeywordIndex = 0;
                    _currentButtonIndex = -1;
                    AnnounceCurrentKeyword();
                }
                return true;
            }
            if (Input.GetKeyDown(KeyCode.End))
            {
                _keywordLetterSearch.Clear();
                if (kwCount > 0)
                {
                    _currentKeywordIndex = kwCount - 1;
                    _currentButtonIndex = -1;
                    AnnounceCurrentKeyword();
                }
                return true;
            }

            // Up/Down: consume to prevent CardInfoNavigator from intercepting
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                return true;
            }

            // Enter: toggle keyword or activate button
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (_currentKeywordIndex >= 0 && _currentKeywordIndex < kwCount)
                {
                    ToggleCurrentKeyword();
                }
                else if (_currentButtonIndex >= 0 && _currentButtonIndex < _browserButtons.Count)
                {
                    ActivateCurrentButton();
                    // After activating a button (e.g. Show All), keyword list may change.
                    // Reset keyword index to avoid stale references.
                    _currentKeywordIndex = -1;
                }
                return true;
            }

            // Space: confirm/submit
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ClickConfirmButton();
                return true;
            }

            // Backspace: cancel
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                ClickCancelButton();
                return true;
            }

            // Show-all phase: consume A-Z for menu-style first-letter jump and
            // prevent zone/battlefield shortcuts (C/G/X/S/W, B/A/R, etc.) from
            // falling through to zone navigators. Shortlist phase preserves
            // normal zone navigation — letter keys are not touched there.
            if (showAll && HandleKeywordLetterJump())
                return true;

            return false;
        }

        /// <summary>
        /// Menu-style buffered A-Z jump over the current keyword list.
        /// Consumes any A-Z keydown (with or without Shift) when the show-all
        /// phase is active. Returns true if a letter key was pressed (consumed)
        /// regardless of whether a match was found, so zone hotkeys stay blocked.
        /// </summary>
        private bool HandleKeywordLetterJump()
        {
            KeyCode pressed = KeyCode.None;
            for (KeyCode k = KeyCode.A; k <= KeyCode.Z; k++)
            {
                if (Input.GetKeyDown(k)) { pressed = k; break; }
            }
            if (pressed == KeyCode.None) return false;

            int count = GetKeywordCount();
            if (count == 0) return true;

            var labels = new List<string>(count);
            for (int i = 0; i < count; i++)
                labels.Add(GetKeywordDisplayText(i) ?? "");

            char letter = (char)('A' + (pressed - KeyCode.A));
            int target = _keywordLetterSearch.HandleKey(letter, labels, _currentKeywordIndex);

            if (target >= 0 && target != _currentKeywordIndex)
            {
                _currentKeywordIndex = target;
                _currentButtonIndex = -1;
                AnnounceCurrentKeyword();
            }
            else if (target >= 0)
            {
                AnnounceCurrentKeyword();
            }
            else
            {
                _announcer.AnnounceInterrupt(Strings.LetterSearchNoMatch(_keywordLetterSearch.Buffer));
            }
            return true;
        }
    }
}
