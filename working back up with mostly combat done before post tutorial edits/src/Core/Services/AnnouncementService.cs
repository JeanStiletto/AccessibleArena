using MTGAAccessibility.Core.Interfaces;
using MTGAAccessibility.Core.Models;

namespace MTGAAccessibility.Core.Services
{
    public class AnnouncementService : IAnnouncementService
    {
        private bool _enabled = true;
        private string _lastAnnouncement;

        public bool IsEnabled => _enabled;

        public void Announce(string message, AnnouncementPriority priority = AnnouncementPriority.Normal)
        {
            if (!_enabled || string.IsNullOrEmpty(message))
                return;

            if (message == _lastAnnouncement && priority < AnnouncementPriority.High)
                return;

            _lastAnnouncement = message;

            bool interrupt = priority >= AnnouncementPriority.High;
            ScreenReaderOutput.Speak(message, interrupt);
        }

        public void AnnounceInterrupt(string message)
        {
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

        public string GetLastAnnouncement()
        {
            return _lastAnnouncement;
        }

        public void RepeatLastAnnouncement()
        {
            if (!string.IsNullOrEmpty(_lastAnnouncement))
            {
                ScreenReaderOutput.Speak(_lastAnnouncement, true);
            }
        }
    }
}
