using System.Collections.Generic;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Core.Services
{
    public class AnnouncementService : IAnnouncementService
    {
        private bool _enabled = true;
        private string _lastAnnouncement;
        private readonly List<string> _history = new List<string>();

        public IReadOnlyList<string> History => _history;

        public bool IsEnabled => _enabled;

        public void Announce(string message, AnnouncementPriority priority = AnnouncementPriority.Normal)
        {
            if (!_enabled || string.IsNullOrEmpty(message))
                return;

            if (message == _lastAnnouncement && priority < AnnouncementPriority.High)
                return;

            _lastAnnouncement = message;

            // Log what we're speaking
            MelonLogger.Msg($"[Announce] {priority}: {message}");

            // Only Immediate priority interrupts - let Tolk queue everything else
            bool interrupt = priority == AnnouncementPriority.Immediate;
            ScreenReaderOutput.Speak(message, interrupt);
        }

        public void AnnounceInterrupt(string message)
        {
            Announce(message, AnnouncementPriority.Immediate);
        }

        public void AnnounceVerbose(string message, AnnouncementPriority priority = AnnouncementPriority.Normal)
        {
            if (AccessibleArenaMod.Instance?.Settings?.VerboseAnnouncements != false)
                Announce(message, priority);
        }

        public void AnnounceInterruptVerbose(string message)
        {
            if (AccessibleArenaMod.Instance?.Settings?.VerboseAnnouncements != false)
                Announce(message, AnnouncementPriority.Immediate);
        }

        public void Silence()
        {
            ScreenReaderOutput.Silence();
        }

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }

        public void RepeatLastAnnouncement()
        {
            if (!string.IsNullOrEmpty(_lastAnnouncement))
            {
                ScreenReaderOutput.Speak(_lastAnnouncement, true);
            }
        }

        public void LogToHistory(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                // Skip consecutive duplicates (e.g. two stack-decrease events for the same resolution)
                if (_history.Count > 0 && _history[_history.Count - 1] == message)
                    return;
                _history.Add(message);
            }
        }

        public void ClearHistory()
        {
            _history.Clear();
        }
    }
}
