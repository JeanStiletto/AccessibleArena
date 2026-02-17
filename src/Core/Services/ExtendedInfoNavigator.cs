using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System.Collections.Generic;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Modal extended card info navigator. When active, blocks all other input
    /// and allows navigation through keyword descriptions and linked face info
    /// with Up/Down arrows. Closes with I, Backspace, or Escape.
    /// </summary>
    public class ExtendedInfoNavigator
    {
        private readonly IAnnouncementService _announcer;
        private readonly List<string> _items = new List<string>();
        private int _currentIndex;
        private bool _isActive;

        public bool IsActive => _isActive;

        public ExtendedInfoNavigator(IAnnouncementService announcer)
        {
            _announcer = announcer;
        }

        /// <summary>
        /// Opens the extended info menu for the given card.
        /// Builds items from keyword descriptions and linked face info.
        /// If no info is available, announces that and does not open.
        /// </summary>
        public void Open(GameObject card)
        {
            _items.Clear();

            // Keywords: each "Header: Details" from GetKeywordDescriptions is one entry
            _items.AddRange(CardModelProvider.GetKeywordDescriptions(card));

            // Linked face: split into individual field entries
            var linkedFace = CardModelProvider.GetLinkedFaceInfo(card);
            if (linkedFace.HasValue)
            {
                var (label, faceInfo) = linkedFace.Value;
                if (!string.IsNullOrEmpty(faceInfo.Name))
                    _items.Add($"{label}: {faceInfo.Name}");
                if (!string.IsNullOrEmpty(faceInfo.ManaCost))
                    _items.Add($"{Strings.CardInfoManaCost}: {faceInfo.ManaCost}");
                if (!string.IsNullOrEmpty(faceInfo.TypeLine))
                    _items.Add($"{Strings.CardInfoType}: {faceInfo.TypeLine}");
                if (!string.IsNullOrEmpty(faceInfo.PowerToughness))
                    _items.Add($"{Strings.CardInfoPowerToughness}: {faceInfo.PowerToughness}");
                if (!string.IsNullOrEmpty(faceInfo.RulesText))
                    _items.Add($"{Strings.CardInfoRules}: {faceInfo.RulesText}");
            }

            if (_items.Count == 0)
            {
                _announcer.AnnounceInterrupt(Strings.NoExtendedCardInfo);
                return;
            }

            _isActive = true;
            _currentIndex = 0;

            MelonLogger.Msg($"[ExtendedInfo] Opened with {_items.Count} items");

            string core = $"{Strings.ExtendedInfoTitle}. {_items.Count} items";
            _announcer.AnnounceInterrupt(Strings.WithHint(core, "ExtendedInfoInstructions"));
        }

        /// <summary>
        /// Closes the extended info menu.
        /// </summary>
        public void Close()
        {
            if (!_isActive) return;

            _isActive = false;
            _currentIndex = 0;

            MelonLogger.Msg("[ExtendedInfo] Closed");
            _announcer.AnnounceInterrupt(Strings.ExtendedInfoClosed);
        }

        /// <summary>
        /// Handle input while extended info menu is active.
        /// Returns true if input was handled (blocks other input).
        /// </summary>
        public bool HandleInput()
        {
            if (!_isActive) return false;

            // I, Backspace, or Escape closes the menu
            if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
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

            // Block all other input while menu is open
            return true;
        }

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

            string item = _items[_currentIndex];
            string announcement = Strings.HelpItemPosition(_currentIndex + 1, _items.Count, item);
            _announcer.AnnounceInterrupt(announcement);
        }
    }
}
