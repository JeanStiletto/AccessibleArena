using System.Collections.Generic;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Core.Interfaces
{
    public interface IAnnouncementService
    {
        void Announce(string message, AnnouncementPriority priority = AnnouncementPriority.Normal);
        void AnnounceInterrupt(string message);
        void AnnounceVerbose(string message, AnnouncementPriority priority = AnnouncementPriority.Normal);
        void AnnounceInterruptVerbose(string message);
        void RepeatLastAnnouncement();
        void Silence();
        void SetEnabled(bool enabled);
        bool IsEnabled { get; }
        IReadOnlyList<string> History { get; }
        void ClearHistory();
    }
}
