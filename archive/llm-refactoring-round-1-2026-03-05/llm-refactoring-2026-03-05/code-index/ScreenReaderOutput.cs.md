# ScreenReaderOutput.cs Code Index

## File Overview
Static wrapper for Tolk.dll providing screen reader output via P/Invoke.

## Class: ScreenReaderOutput (static) (line 6)

### Private Fields
- private static bool _initialized (line 8)
- private static bool _available (line 9)

### DLL Imports (Tolk.dll)
- private static extern void Tolk_Load() (line 12)
- private static extern void Tolk_Unload() (line 15)
- private static extern bool Tolk_IsLoaded() (line 19)
- private static extern bool Tolk_HasSpeech() (line 23)
- private static extern bool Tolk_Output(string text, bool interrupt) (line 27)
- private static extern bool Tolk_Speak(string text, bool interrupt) (line 33)
- private static extern bool Tolk_Silence() (line 39)
- private static extern IntPtr Tolk_DetectScreenReader() (line 42)

### Public Methods
- public static bool Initialize() (line 44)
  // Loads Tolk library and detects screen reader. Returns true if available.

- public static void Shutdown() (line 64)
  // Unloads Tolk library

- public static void Speak(string text, bool interrupt = false) (line 74)
  // Sends text to screen reader

- public static void SpeakInterrupt(string text) (line 82)
  // Convenience method for Speak(text, true)

- public static void Silence() (line 87)
  // Stops current speech

- public static string GetActiveScreenReader() (line 95)
  // Returns name of active screen reader or "None"/"Unknown"

### Public Properties
- public static bool IsAvailable => _available (line 104)
