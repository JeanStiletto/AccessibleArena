using System;
using System.Collections.Generic;
using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Modal settings menu navigator. When active, blocks all other input
    /// and allows navigating and toggling mod settings with Up/Down arrows.
    /// Language setting uses dropdown-like behavior (Enter to open, Left/Right
    /// to browse, Enter to confirm, Escape/Backspace to cancel).
    /// Closes with Backspace or F2.
    /// </summary>
    public class ModSettingsNavigator
    {
        private readonly IAnnouncementService _announcer;
        private readonly ModSettings _settings;
        private readonly List<SettingItem> _items;
        private int _currentIndex;
        private bool _isActive;

        // Dropdown state for language picker
        private bool _isInDropdownMode;
        private int _dropdownLanguageIndex;
        private string _originalLanguageCode;

        public bool IsActive => _isActive;

        public ModSettingsNavigator(IAnnouncementService announcer, ModSettings settings)
        {
            _announcer = announcer;
            _settings = settings;
            _items = BuildSettingItems();

            // Rebuild menu labels when language changes (so labels show in new language)
            _settings.OnLanguageChanged += () =>
            {
                _items.Clear();
                _items.AddRange(BuildSettingItems());
            };
        }

        /// <summary>
        /// Defines a single setting item in the menu.
        /// </summary>
        private class SettingItem
        {
            public string Name { get; set; }
            public Func<string> GetValue { get; set; }
            public Action Toggle { get; set; }
            /// <summary>True if this item uses dropdown mode instead of simple toggle.</summary>
            public bool IsDropdown { get; set; }
        }

        private List<SettingItem> BuildSettingItems()
        {
            return new List<SettingItem>
            {
                new SettingItem
                {
                    Name = Strings.SettingLanguage,
                    GetValue = () => _settings.GetLanguageDisplayName(),
                    Toggle = null, // Handled by dropdown mode
                    IsDropdown = true
                },
                new SettingItem
                {
                    Name = Strings.SettingTutorialMessages,
                    GetValue = () => _settings.TutorialMessages ? Strings.SettingOn : Strings.SettingOff,
                    Toggle = () => _settings.TutorialMessages = !_settings.TutorialMessages
                },
                new SettingItem
                {
                    Name = Strings.SettingVerboseAnnouncements,
                    GetValue = () => _settings.VerboseAnnouncements ? Strings.SettingOn : Strings.SettingOff,
                    Toggle = () => _settings.VerboseAnnouncements = !_settings.VerboseAnnouncements
                }
            };
        }

        /// <summary>
        /// Toggle the settings menu on/off.
        /// </summary>
        public void Toggle()
        {
            if (_isActive)
                Close();
            else
                Open();
        }

        /// <summary>
        /// Open the settings menu.
        /// </summary>
        public void Open()
        {
            if (_isActive) return;

            _isActive = true;
            _currentIndex = 0;
            _isInDropdownMode = false;

            MelonLogger.Msg("[ModSettingsNavigator] Opened");
            _announcer.AnnounceInterrupt($"{Strings.SettingsMenuTitle}. {Strings.ItemCount(_items.Count)}. {Strings.SettingsMenuInstructions}");
        }

        /// <summary>
        /// Close the settings menu and save settings.
        /// </summary>
        public void Close()
        {
            if (!_isActive) return;

            // If closing while in dropdown mode, cancel the dropdown first
            if (_isInDropdownMode)
                CancelDropdown();

            _isActive = false;
            _currentIndex = 0;

            _settings.Save();

            MelonLogger.Msg("[ModSettingsNavigator] Closed");
            _announcer.AnnounceInterrupt(Strings.SettingsMenuClosed);
        }

        /// <summary>
        /// Handle input while settings menu is active.
        /// Returns true to block all other input.
        /// </summary>
        public bool HandleInput()
        {
            if (!_isActive) return false;

            // Dropdown mode has its own input handling
            if (_isInDropdownMode)
            {
                HandleDropdownInput();
                return true;
            }

            // F2 or Backspace closes the menu
            if (Input.GetKeyDown(KeyCode.F2) || Input.GetKeyDown(KeyCode.Backspace))
            {
                Close();
                return true;
            }

            // Enter or Space: toggle/cycle current setting or enter dropdown
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                ActivateCurrentSetting();
                return true;
            }

            // Up arrow or W: previous item
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                MovePrevious();
                return true;
            }

            // Down arrow or S: next item
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                MoveNext();
                return true;
            }

            // Home: first item
            if (Input.GetKeyDown(KeyCode.Home))
            {
                MoveFirst();
                return true;
            }

            // End: last item
            if (Input.GetKeyDown(KeyCode.End))
            {
                MoveLast();
                return true;
            }

            // Block all other input while settings menu is open
            return true;
        }

        private void ActivateCurrentSetting()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];

            if (item.IsDropdown)
            {
                EnterDropdownMode();
            }
            else
            {
                item.Toggle?.Invoke();
                string newValue = item.GetValue();
                string announcement = Strings.SettingChanged(item.Name, newValue);
                _announcer.AnnounceInterrupt(announcement);
                MelonLogger.Msg($"[ModSettingsNavigator] {item.Name} set to {newValue}");
            }
        }

        #region Dropdown Mode (Language Picker)

        private void EnterDropdownMode()
        {
            _isInDropdownMode = true;
            _originalLanguageCode = _settings.Language;
            _dropdownLanguageIndex = ModSettings.GetLanguageIndex(_settings.Language);

            string currentName = ModSettings.GetLanguageDisplayName(_dropdownLanguageIndex);
            int position = _dropdownLanguageIndex + 1;
            int total = ModSettings.LanguageCodes.Length;

            _announcer.AnnounceInterrupt($"{Strings.DropdownOpened} {Strings.ItemPositionOf(position, total, currentName)}");
            MelonLogger.Msg($"[ModSettingsNavigator] Language dropdown opened at {currentName}");
        }

        private void HandleDropdownInput()
        {
            // Enter or Space: confirm selection and apply
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                ConfirmDropdown();
                return;
            }

            // Escape or Backspace: cancel, restore original
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            {
                CancelDropdown();
                return;
            }

            // Down/Right arrow or S/D: next language
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow)
                || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D))
            {
                CycleDropdown(1);
                return;
            }

            // Up/Left arrow or W/A: previous language
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow)
                || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A))
            {
                CycleDropdown(-1);
                return;
            }

            // Home: first language
            if (Input.GetKeyDown(KeyCode.Home))
            {
                JumpDropdown(0);
                return;
            }

            // End: last language
            if (Input.GetKeyDown(KeyCode.End))
            {
                JumpDropdown(ModSettings.LanguageCodes.Length - 1);
                return;
            }
        }

        private void CycleDropdown(int direction)
        {
            int total = ModSettings.LanguageCodes.Length;
            int newIndex = _dropdownLanguageIndex + direction;

            if (newIndex < 0)
            {
                _announcer.Announce(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }
            if (newIndex >= total)
            {
                _announcer.Announce(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            _dropdownLanguageIndex = newIndex;
            AnnounceDropdownItem();
        }

        private void JumpDropdown(int index)
        {
            if (index == _dropdownLanguageIndex)
            {
                _announcer.Announce(index == 0 ? Strings.BeginningOfList : Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            _dropdownLanguageIndex = index;
            AnnounceDropdownItem();
        }

        private void AnnounceDropdownItem()
        {
            string name = ModSettings.GetLanguageDisplayName(_dropdownLanguageIndex);
            int position = _dropdownLanguageIndex + 1;
            int total = ModSettings.LanguageCodes.Length;
            _announcer.AnnounceInterrupt(Strings.ItemPositionOf(position, total, name));
        }

        private void ConfirmDropdown()
        {
            _isInDropdownMode = false;
            string selectedCode = ModSettings.LanguageCodes[_dropdownLanguageIndex];

            _settings.SetLanguage(selectedCode);

            // Fetch name AFTER language switch so it reads from the new locale
            string selectedName = ModSettings.GetLanguageDisplayName(_dropdownLanguageIndex);
            _announcer.AnnounceInterrupt(Strings.SettingChanged(Strings.SettingLanguage, selectedName));
            MelonLogger.Msg($"[ModSettingsNavigator] Language confirmed: {selectedCode} ({selectedName})");
        }

        private void CancelDropdown()
        {
            _isInDropdownMode = false;
            _dropdownLanguageIndex = ModSettings.GetLanguageIndex(_originalLanguageCode);

            _announcer.AnnounceInterrupt(Strings.DropdownClosed);
            MelonLogger.Msg("[ModSettingsNavigator] Language dropdown cancelled");
        }

        #endregion

        private void MoveNext()
        {
            if (_currentIndex >= _items.Count - 1)
            {
                _announcer.Announce(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            _currentIndex++;
            AnnounceCurrentItem();
        }

        private void MovePrevious()
        {
            if (_currentIndex <= 0)
            {
                _announcer.Announce(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }

            _currentIndex--;
            AnnounceCurrentItem();
        }

        private void MoveFirst()
        {
            if (_currentIndex == 0)
            {
                _announcer.Announce(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }

            _currentIndex = 0;
            AnnounceCurrentItem();
        }

        private void MoveLast()
        {
            int lastIndex = _items.Count - 1;
            if (_currentIndex == lastIndex)
            {
                _announcer.Announce(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            _currentIndex = lastIndex;
            AnnounceCurrentItem();
        }

        private void AnnounceCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];
            string valueText = $"{item.Name}: {item.GetValue()}";
            string announcement = Strings.SettingItemPosition(_currentIndex + 1, _items.Count, valueText);
            _announcer.AnnounceInterrupt(announcement);
        }
    }
}
