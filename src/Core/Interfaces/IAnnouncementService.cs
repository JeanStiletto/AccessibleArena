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

        /// <summary>
        /// The most recent text spoken to the screen reader — i.e. what the user
        /// is currently "on". Used by the copy-to-clipboard shortcut. Null/empty
        /// when nothing has been announced yet.
        /// </summary>
        string LastAnnouncement { get; }

        void Silence();
        void SetEnabled(bool enabled);
        bool IsEnabled { get; }
        IReadOnlyList<string> History { get; }
        void LogToHistory(string message);
        void ClearHistory();
    }
}
