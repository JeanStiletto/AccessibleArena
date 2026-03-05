# AnnouncementService.cs

## Class: AnnouncementService : IAnnouncementService (line 7)

### Fields
- bool _enabled (line 9)
- string _lastAnnouncement (line 10)

### Properties
- bool IsEnabled (line 12)

### Methods
- void Announce(string message, AnnouncementPriority priority = AnnouncementPriority.Normal) (line 14)
  Note: Logs message and uses Tolk for speech output; only Immediate priority interrupts

- void AnnounceInterrupt(string message) (line 32)
  Note: Announces with Immediate priority

- void AnnounceVerbose(string message, AnnouncementPriority priority = AnnouncementPriority.Normal) (line 37)
  Note: Only announces if VerboseAnnouncements setting is enabled

- void AnnounceInterruptVerbose(string message) (line 43)
  Note: Verbose announcement with interruption

- void Silence() (line 49)

- void SetEnabled(bool enabled) (line 54)

- string GetLastAnnouncement() (line 59)

- void RepeatLastAnnouncement() (line 64)
