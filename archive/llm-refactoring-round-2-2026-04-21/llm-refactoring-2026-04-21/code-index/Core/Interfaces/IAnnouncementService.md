# IAnnouncementService.cs
Path: src/Core/Interfaces/IAnnouncementService.cs
Lines: 20

## interface IAnnouncementService (line 6)
### Properties
- bool IsEnabled { get; } (line 15)
- IReadOnlyList<string> History { get; } (line 16)
### Methods
- void Announce(string message, AnnouncementPriority priority = AnnouncementPriority.Normal) (line 8)
- void AnnounceInterrupt(string message) (line 9)
- void AnnounceVerbose(string message, AnnouncementPriority priority = AnnouncementPriority.Normal) (line 10)
- void AnnounceInterruptVerbose(string message) (line 11)
- void RepeatLastAnnouncement() (line 12)
- void Silence() (line 13)
- void SetEnabled(bool enabled) (line 14)
- void LogToHistory(string message) (line 17)
- void ClearHistory() (line 18)
