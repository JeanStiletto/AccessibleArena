# IScreenReaderOutput.cs
Path: src/Core/Interfaces/IScreenReaderOutput.cs
Lines: 12

## Top-level comments
- Abstraction over native screen reader output (Tolk P/Invoke). Injected into AnnouncementService for testability.

## interface IScreenReaderOutput (line 7)
### Methods
- void Speak(string text, bool interrupt) (line 9)
- void Silence() (line 10)
