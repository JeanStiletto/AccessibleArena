using System;
using System.Collections.Generic;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Core.Services
{
    public class AnnouncementService : IAnnouncementService
    {
        private readonly IScreenReaderOutput _output;
        private readonly Func<bool> _verboseEnabled;
        private bool _enabled = true;
        private string _lastAnnouncement;
        private readonly List<string> _history = new List<string>();

        /// <summary>Production constructor — uses real screen reader and live settings.</summary>
        public AnnouncementService()
            : this(new ScreenReaderAdapter(),
                   () => AccessibleArenaMod.Instance?.Settings?.VerboseAnnouncements != false)
        { }

        /// <summary>Testable constructor — inject output and verbose flag.</summary>
        internal AnnouncementService(IScreenReaderOutput output, Func<bool> verboseEnabled)
        {
            _output = output;
            _verboseEnabled = verboseEnabled;
        }

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
            _output.Speak(message, interrupt);
        }

        public void AnnounceInterrupt(string message)
        {
            Announce(message, AnnouncementPriority.Immediate);
        }

        public void AnnounceVerbose(string message, AnnouncementPriority priority = AnnouncementPriority.Normal)
        {
            if (_verboseEnabled())
                Announce(message, priority);
        }

        public void AnnounceInterruptVerbose(string message)
        {
            if (_verboseEnabled())
                Announce(message, AnnouncementPriority.Immediate);
        }

        public void Silence()
        {
            _output.Silence();
        }

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }

        public void RepeatLastAnnouncement()
        {
            if (!string.IsNullOrEmpty(_lastAnnouncement))
            {
                _output.Speak(_lastAnnouncement, true);
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
