using MTGAAccessibility.Core.Models;

namespace MTGAAccessibility.Core.Interfaces
{
    public interface IAnnouncementService
    {
        void Announce(string message, AnnouncementPriority priority = AnnouncementPriority.Normal);
        void AnnounceInterrupt(string message);
        void RepeatLastAnnouncement();
        void Silence();
        void SetEnabled(bool enabled);
        bool IsEnabled { get; }
    }
}
