# AnnouncementService.cs
Path: src/Core/Services/AnnouncementService.cs
Lines: 124

## public class AnnouncementService : IAnnouncementService (line 9)

### Fields
- private readonly IScreenReaderOutput _output (line 11)
- private readonly Func<bool> _verboseEnabled (line 12)
- private bool _enabled = true (line 13)
- private string _lastAnnouncement (line 14)
- private readonly List<string> _history (line 15)
- private DateTime _criticalActiveUntil = DateTime.MinValue (line 19) — tracks when critical protection window expires
- private const double CriticalCooldownSeconds = 15.0 (line 20)

### Properties
- public IReadOnlyList<string> History => _history (line 35)
- public bool IsEnabled => _enabled (line 37)

### Methods
- public AnnouncementService() (line 23) — production constructor; uses ScreenReaderAdapter and live settings
- internal AnnouncementService(IScreenReaderOutput output, Func<bool> verboseEnabled) (line 29) — testable constructor
- public void Announce(string message, AnnouncementPriority priority = AnnouncementPriority.Normal) (line 39)
- public void AnnounceInterrupt(string message) (line 73)
- public void AnnounceVerbose(string message, AnnouncementPriority priority = AnnouncementPriority.Normal) (line 78)
- public void AnnounceInterruptVerbose(string message) (line 84)
- public void Silence() (line 90)
- public void SetEnabled(bool enabled) (line 95)
- public void RepeatLastAnnouncement() (line 100)
- public void LogToHistory(string message) (line 108) — skips consecutive duplicates
- public void ClearHistory() (line 119)
