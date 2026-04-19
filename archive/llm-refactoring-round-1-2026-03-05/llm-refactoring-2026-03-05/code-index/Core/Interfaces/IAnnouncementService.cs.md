# IAnnouncementService.cs Code Index

## File Overview
Interface for announcing text to screen readers with priority management.

## Interface: IAnnouncementService (line 5)

### Methods
- void Announce(string message, AnnouncementPriority priority = AnnouncementPriority.Normal) (line 7)
  // Announces message with given priority

- void AnnounceInterrupt(string message) (line 8)
  // Announces message immediately, interrupting current speech

- void AnnounceVerbose(string message, AnnouncementPriority priority = AnnouncementPriority.Normal) (line 9)
  // Announces message only if verbose announcements are enabled

- void AnnounceInterruptVerbose(string message) (line 10)
  // Interrupting announce for verbose messages

- void RepeatLastAnnouncement() (line 11)
  // Re-announces the last announcement

- void Silence() (line 12)
  // Stops current speech

- void SetEnabled(bool enabled) (line 13)
  // Enables or disables announcements

### Properties
- bool IsEnabled { get; } (line 14)
  // Whether announcements are enabled
