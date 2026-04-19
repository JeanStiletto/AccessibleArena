# ScreenReaderAdapter.cs
Path: src/Core/Services/ScreenReaderAdapter.cs
Lines: 13

## Top-level comments
- Production implementation of IScreenReaderOutput that delegates to the static Tolk P/Invoke wrapper (ScreenReaderOutput).

## internal sealed class ScreenReaderAdapter : IScreenReaderOutput (line 8)
### Methods
- public void Speak(string text, bool interrupt) (line 10)
- public void Silence() (line 11)
