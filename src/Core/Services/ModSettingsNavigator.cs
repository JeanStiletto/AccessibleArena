using System;
using System.Collections.Generic;
using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Utils;

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
            /// <summary>True if activating this item performs an action (e.g. opens a URL) rather than toggling a value.</summary>
            public bool IsAction { get; set; }
            /// <summary>Optional short description of what this setting controls.</summary>
            public string Description { get; set; }
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
                    Toggle = () => _settings.TutorialMessages = !_settings.TutorialMessages,
                    Description = Strings.SettingTutorialMessagesDesc
                },
                new SettingItem
                {
                    Name = Strings.SettingVerboseAnnouncements,
                    GetValue = () => _settings.VerboseAnnouncements ? Strings.SettingOn : Strings.SettingOff,
                    Toggle = () => _settings.VerboseAnnouncements = !_settings.VerboseAnnouncements,
                    Description = Strings.SettingVerboseAnnouncementsDesc
                },
                new SettingItem
                {
                    Name = Strings.SettingBriefCastAnnouncements,
                    GetValue = () => _settings.BriefCastAnnouncements ? Strings.SettingOn : Strings.SettingOff,
                    Toggle = () => _settings.BriefCastAnnouncements = !_settings.BriefCastAnnouncements,
                    Description = Strings.SettingBriefCastAnnouncementsDesc
                },
                new SettingItem
                {
                    Name = Strings.SettingBriefOpponentAnnouncements,
                    GetValue = () => _settings.BriefOpponentAnnouncements ? Strings.SettingOn : Strings.SettingOff,
                    Toggle = () => _settings.BriefOpponentAnnouncements = !_settings.BriefOpponentAnnouncements,
                    Description = Strings.SettingBriefOpponentAnnouncementsDesc
                },
                new SettingItem
                {
                    Name = Strings.SettingPhaseSkipWarning,
                    GetValue = () => _settings.PhaseSkipWarning ? Strings.SettingOn : Strings.SettingOff,
                    Toggle = () => _settings.PhaseSkipWarning = !_settings.PhaseSkipWarning
                },
                new SettingItem
                {
                    Name = Strings.SettingPriorityAnnouncements,
                    GetValue = () => _settings.PriorityAnnouncements ? Strings.SettingOn : Strings.SettingOff,
                    Toggle = () => _settings.PriorityAnnouncements = !_settings.PriorityAnnouncements,
                    Description = Strings.SettingPriorityAnnouncementsDesc
                },
                new SettingItem
                {
                    Name = Strings.SettingPriorityAlarm,
                    GetValue = () => _settings.PriorityAlarm ? Strings.SettingOn : Strings.SettingOff,
                    Toggle = () => _settings.PriorityAlarm = !_settings.PriorityAlarm,
                    Description = Strings.SettingPriorityAlarmDesc
                },
                new SettingItem
                {
                    Name = Strings.SettingPositionCounts,
                    GetValue = () => _settings.PositionCounts ? Strings.SettingOn : Strings.SettingOff,
                    Toggle = () => _settings.PositionCounts = !_settings.PositionCounts,
                    Description = Strings.SettingPositionCountsDesc
                },
                new SettingItem
                {
                    Name = Strings.SettingManaColorlessLabel,
                    GetValue = () => _settings.ManaColorlessLabel ? Strings.SettingOn : Strings.SettingOff,
                    Toggle = () => _settings.ManaColorlessLabel = !_settings.ManaColorlessLabel,
                    Description = Strings.SettingManaColorlessLabelDesc
                },
                new SettingItem
                {
                    Name = Strings.SettingManaGroupColors,
                    GetValue = () => _settings.ManaGroupColors ? Strings.SettingOn : Strings.SettingOff,
                    Toggle = () => _settings.ManaGroupColors = !_settings.ManaGroupColors,
                    Description = Strings.SettingManaGroupColorsDesc
                },
                new SettingItem
                {
                    Name = Strings.SettingBattlefieldStacking,
                    GetValue = () => _settings.BattlefieldStacking ? Strings.SettingOn : Strings.SettingOff,
                    Toggle = () => _settings.BattlefieldStacking = !_settings.BattlefieldStacking,
                    Description = Strings.SettingBattlefieldStackingDesc
                },
                new SettingItem
                {
                    Name = Strings.SettingSpeechBackend,
                    GetValue = DescribeSpeechBackend,
                    Toggle = CycleSpeechBackend,
                    Description = Strings.SettingSpeechBackendDesc
                },
                new SettingItem
                {
                    Name = Strings.SettingCriticalViaSystemVoice,
                    GetValue = () => _settings.CriticalViaSystemVoice ? Strings.SettingOn : Strings.SettingOff,
                    Toggle = () => _settings.CriticalViaSystemVoice = !_settings.CriticalViaSystemVoice,
                    Description = Strings.SettingCriticalViaSystemVoiceDesc
                },
                new SettingItem
                {
                    Name = Strings.SettingUrgentSpeechVolume,
                    GetValue = () => Strings.SettingVolumePercent(_settings.UrgentSpeechVolume),
                    Toggle = CycleUrgentVolume,
                    Description = Strings.SettingUrgentSpeechVolumeDesc
                },
                new SettingItem
                {
                    Name = Strings.SettingCheckForUpdates,
                    GetValue = () => _settings.CheckForUpdates ? Strings.SettingOn : Strings.SettingOff,
                    Toggle = () => _settings.CheckForUpdates = !_settings.CheckForUpdates,
                    Description = Strings.SettingCheckForUpdatesDesc
                },
                new SettingItem
                {
                    Name = Strings.SettingSupport,
                    GetValue = () => Strings.SettingActionOpen,
                    Toggle = () => OpenUrl("https://ko-fi.com/jeanstiletto", Strings.SettingSupport),
                    IsAction = true,
                    Description = Strings.SettingSupportDesc
                },
                new SettingItem
                {
                    Name = Strings.SettingUpdateNotes,
                    GetValue = () => Strings.SettingActionOpen,
                    Toggle = () => OpenUrl("https://github.com/JeanStiletto/AccessibleArena/releases/latest", Strings.SettingUpdateNotes),
                    IsAction = true,
                    Description = Strings.SettingUpdateNotesDesc
                }
            };
        }

        #region Speech backend (Prism)

        /// <summary>
        /// Reads back the speech setting as the preference plus what is actually speaking, so
        /// "I chose JAWS but NVDA is talking" is audible instead of silent.
        /// </summary>
        private string DescribeSpeechBackend()
        {
            string preference = _settings.SpeechBackend;
            string active = ScreenReaderOutput.GetActiveScreenReader();

            if (string.IsNullOrEmpty(preference) ||
                string.Equals(preference, ScreenReaderOutput.AutoBackend, StringComparison.OrdinalIgnoreCase))
                return Strings.SettingSpeechBackendAuto(active);

            if (string.Equals(preference, active, StringComparison.OrdinalIgnoreCase))
                return preference;

            return Strings.SettingSpeechBackendUnavailable(preference, active);
        }

        /// <summary>
        /// Steps to the next backend Prism has compiled in, automatic selection first. The
        /// preference always advances, even when the switch fails, so a reader that is not
        /// running cannot trap the cycle — the previous backend keeps speaking meanwhile, and
        /// the read-back says which one that is.
        /// </summary>
        private void CycleSpeechBackend()
        {
            var choices = new List<string> { ScreenReaderOutput.AutoBackend };
            choices.AddRange(ScreenReaderOutput.GetAvailableBackends());

            int current = choices.FindIndex(
                c => string.Equals(c, _settings.SpeechBackend, StringComparison.OrdinalIgnoreCase));
            string next = choices[(current + 1) % choices.Count];

            _settings.SpeechBackend = next;
            bool applied = ScreenReaderOutput.SelectBackend(next);
            Log.Msg("ModSettingsNavigator",
                    $"Speech backend preference set to '{next}' (applied: {applied}, active: {ScreenReaderOutput.GetActiveScreenReader()})");
        }

        /// <summary>Steps the system-voice volume down in tenths, wrapping from 10% back to 100%.</summary>
        private void CycleUrgentVolume()
        {
            int next = _settings.UrgentSpeechVolume - 10;
            if (next < 10)
                next = 100;

            _settings.UrgentSpeechVolume = next;
            ScreenReaderOutput.SetUrgentVolumePercent(next);
        }

        #endregion

        private void OpenUrl(string url, string name)
        {
            try
            {
                System.Diagnostics.Process.Start(url);
                _announcer.AnnounceInterrupt(Strings.SettingOpeningBrowser(name));
                Log.Msg("ModSettingsNavigator", $"Opened URL for '{name}': {url}");
            }
            catch (Exception ex)
            {
                Log.Msg("ModSettingsNavigator", $"Failed to open URL {url}: {ex.Message}");
            }
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

            Log.Msg("ModSettingsNavigator", "Opened");
            string core = $"{Strings.SettingsMenuTitle}. {Strings.ItemCount(_items.Count)}";
            _announcer.AnnounceInterrupt(Strings.WithHint(core, "SettingsMenuInstructions"));
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

            Log.Msg("ModSettingsNavigator", "Closed");
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

            // F2, Backspace, or Escape closes the menu
            if (KeyInput.GetKeyDown(KeyCode.F2) || KeyInput.GetKeyDown(KeyCode.Backspace) || KeyInput.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return true;
            }

            // Enter or Space: toggle/cycle current setting or enter dropdown
            if (KeyInput.GetKeyDown(KeyCode.Return) || KeyInput.GetKeyDown(KeyCode.Space))
            {
                ActivateCurrentSetting();
                return true;
            }

            // Up arrow: previous item
            if (KeyInput.GetKeyDown(KeyCode.UpArrow))
            {
                MovePrevious();
                return true;
            }

            // Down arrow: next item
            if (KeyInput.GetKeyDown(KeyCode.DownArrow))
            {
                MoveNext();
                return true;
            }

            // Home: first item
            if (KeyInput.GetKeyDown(KeyCode.Home))
            {
                MoveFirst();
                return true;
            }

            // End: last item
            if (KeyInput.GetKeyDown(KeyCode.End))
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
            else if (item.IsAction)
            {
                item.Toggle?.Invoke();
            }
            else
            {
                item.Toggle?.Invoke();
                string newValue = item.GetValue();
                string announcement = Strings.SettingChanged(item.Name, newValue);
                _announcer.AnnounceInterrupt(announcement);
                Log.Msg("ModSettingsNavigator", $"{item.Name} set to {newValue}");
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
            Log.Msg("ModSettingsNavigator", $"Language dropdown opened at {currentName}");
        }

        private void HandleDropdownInput()
        {
            // Enter or Space: confirm selection and apply
            if (KeyInput.GetKeyDown(KeyCode.Return) || KeyInput.GetKeyDown(KeyCode.Space))
            {
                ConfirmDropdown();
                return;
            }

            // Escape or Backspace: cancel, restore original
            if (KeyInput.GetKeyDown(KeyCode.Escape) || KeyInput.GetKeyDown(KeyCode.Backspace))
            {
                CancelDropdown();
                return;
            }

            // Down/Right arrow: next language
            if (KeyInput.GetKeyDown(KeyCode.DownArrow) || KeyInput.GetKeyDown(KeyCode.RightArrow))
            {
                CycleDropdown(1);
                return;
            }

            // Up/Left arrow: previous language
            if (KeyInput.GetKeyDown(KeyCode.UpArrow) || KeyInput.GetKeyDown(KeyCode.LeftArrow))
            {
                CycleDropdown(-1);
                return;
            }

            // Home: first language
            if (KeyInput.GetKeyDown(KeyCode.Home))
            {
                JumpDropdown(0);
                return;
            }

            // End: last language
            if (KeyInput.GetKeyDown(KeyCode.End))
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
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }
            if (newIndex >= total)
            {
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            _dropdownLanguageIndex = newIndex;
            AnnounceDropdownItem();
        }

        private void JumpDropdown(int index)
        {
            if (index == _dropdownLanguageIndex)
            {
                _announcer.AnnounceVerbose(index == 0 ? Strings.BeginningOfList : Strings.EndOfList, AnnouncementPriority.Normal);
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
            Log.Msg("ModSettingsNavigator", $"Language confirmed: {selectedCode} ({selectedName})");
        }

        private void CancelDropdown()
        {
            _isInDropdownMode = false;
            _dropdownLanguageIndex = ModSettings.GetLanguageIndex(_originalLanguageCode);

            _announcer.AnnounceInterrupt(Strings.DropdownClosed);
            Log.Msg("ModSettingsNavigator", "Language dropdown cancelled");
        }

        #endregion

        private void MoveNext()
        {
            if (_currentIndex >= _items.Count - 1)
            {
                // Single item: re-announce it before saying end of list
                if (_items.Count == 1)
                    AnnounceCurrentItem();
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            _currentIndex++;
            AnnounceCurrentItem();
        }

        private void MovePrevious()
        {
            if (_currentIndex <= 0)
            {
                // Single item: re-announce it before saying beginning of list
                if (_items.Count == 1)
                    AnnounceCurrentItem();
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }

            _currentIndex--;
            AnnounceCurrentItem();
        }

        private void MoveFirst()
        {
            if (_currentIndex == 0)
            {
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
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
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
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
            if (!string.IsNullOrEmpty(item.Description))
                valueText = $"{valueText}. {item.Description}";
            string announcement = Strings.SettingItemPosition(_currentIndex + 1, _items.Count, valueText);
            _announcer.AnnounceInterrupt(announcement);
        }
    }
}
