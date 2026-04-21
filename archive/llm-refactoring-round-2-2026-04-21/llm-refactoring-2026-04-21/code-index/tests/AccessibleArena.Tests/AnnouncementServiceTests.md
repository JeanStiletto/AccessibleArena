# AnnouncementServiceTests.cs
Path: tests/AccessibleArena.Tests/AnnouncementServiceTests.cs
Lines: 191

## public class AnnouncementServiceTests (line 11)
### Fields
- private IScreenReaderOutput _output (line 13)
- private AnnouncementService _service (line 14)
### Methods
- private void MakeService(bool verbose = true) (line 16)
- public void SetUp() (line 23)
- public void Announce_Normal_CallsSpeakWithInterruptFalse() (line 28)
- public void Announce_Immediate_CallsSpeakWithInterruptTrue() (line 35)
- public void Announce_NullMessage_DoesNotCallSpeak() (line 42)
- public void Announce_EmptyMessage_DoesNotCallSpeak() (line 49)
- public void Announce_SameMessageTwiceAtNormal_SpeakCalledOnce() (line 58)
- public void Announce_SameMessageAtHigh_BypassesDedup() (line 66)
- public void Announce_DifferentMessages_BothSpoken() (line 74)
- public void SetEnabled_False_SubsequentAnnounceDoesNotSpeak() (line 85)
- public void IsEnabled_ReflectsSetEnabled() (line 93)
- public void AnnounceVerbose_VerboseTrue_CallsSpeak() (line 103)
- public void AnnounceVerbose_VerboseFalse_DoesNotSpeak() (line 111)
- public void Silence_CallsOutputSilence() (line 121)
- public void RepeatLastAnnouncement_SpeaksLastMessageWithInterrupt() (line 130)
- public void RepeatLastAnnouncement_NoLastMessage_DoesNotSpeak() (line 140)
- public void LogToHistory_AddsMessage() (line 149)
- public void LogToHistory_ConsecutiveDuplicate_NotAdded() (line 157)
- public void LogToHistory_NonConsecutiveDuplicate_IsAdded() (line 165)
- public void LogToHistory_NullOrEmpty_NotAdded() (line 174)
- public void ClearHistory_EmptiesHistory() (line 182)
