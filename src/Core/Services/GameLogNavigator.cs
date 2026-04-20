using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System.Collections.Generic;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Modal game log navigator. When active, blocks all other input
    /// and allows navigation through duel announcement history
    /// with Up/Down arrows. Newest entries first. Closes with O, Backspace, or Escape.
    /// </summary>
    public class GameLogNavigator
    {
        private readonly IAnnouncementService _announcer;
        private readonly List<string> _items = new List<string>();
        private int _currentIndex;
        private bool _isActive;

        public bool IsActive => _isActive;

        public GameLogNavigator(IAnnouncementService announcer)
        {
            _announcer = announcer;
        }

        /// <summary>
        /// Opens the game log menu. Snapshots current announcement history
        /// in reverse order (newest first). If empty, announces that and does not open.
        /// </summary>
        public void Open()
        {
            _items.Clear();

            var history = _announcer.History;
            for (int i = history.Count - 1; i >= 0; i--)
                _items.Add(history[i]);

            if (_items.Count == 0)
            {
                _announcer.AnnounceInterrupt(Strings.GameLogEmpty);
                return;
            }

            _isActive = true;
            _currentIndex = 0;

            Log.Msg("GameLog", $"Opened with {_items.Count} items");

            string core = $"{Strings.GameLogTitle}. {Strings.ItemCount(_items.Count)}";
            _announcer.AnnounceInterrupt(Strings.WithHint(core, "GameLogInstructions"));
        }

        /// <summary>
        /// Closes the game log menu.
        /// </summary>
        public void Close()
        {
            if (!_isActive) return;

            _isActive = false;
            _currentIndex = 0;

            Log.Msg("GameLog", "Closed");
            _announcer.AnnounceInterrupt(Strings.GameLogClosed);
        }

        /// <summary>
        /// Handle input while game log menu is active.
        /// Returns true if input was handled (blocks other input).
        /// </summary>
        public bool HandleInput()
        {
            if (!_isActive) return false;

            // O, Backspace, or Escape closes the menu
            if (Input.GetKeyDown(KeyCode.O) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return true;
            }

            // Down arrow: next (older) entry
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveNext();
                return true;
            }

            // Up arrow: previous (newer) entry
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                MovePrevious();
                return true;
            }

            // Home: first (newest) entry
            if (Input.GetKeyDown(KeyCode.Home))
            {
                MoveFirst();
                return true;
            }

            // End: last (oldest) entry
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
            string announcement = Strings.HelpItemPosition(_currentIndex + 1, _items.Count, item, force: true);
            _announcer.AnnounceInterrupt(announcement);
        }
    }
}
