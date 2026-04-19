# ScreenReaderOutput.cs
Path: src/ScreenReaderOutput.cs
Lines: 106

## Top-level comments
- None.

## static class ScreenReaderOutput (line 6)
P/Invoke wrapper around native Tolk.dll. Lives in root `AccessibleArena` namespace (not `Core.Services`).

### Fields
- private static bool _initialized (line 8)
- private static bool _available (line 9)

### P/Invoke signatures
- extern void Tolk_Load() (line 12)
- extern void Tolk_Unload() (line 15)
- extern bool Tolk_IsLoaded() (line 19)
- extern bool Tolk_HasSpeech() (line 23)
- extern bool Tolk_Output(string text, bool interrupt) (line 27)
- extern bool Tolk_Speak(string text, bool interrupt) (line 33)
- extern bool Tolk_Silence() (line 39)
- extern IntPtr Tolk_DetectScreenReader() (line 42)

### Properties
- public static bool IsAvailable (line 104)

### Methods
- public static bool Initialize() (line 44) — Note: swallows `DllNotFoundException` and degrades to silent mode; sets `_initialized=true` regardless.
- public static void Shutdown() (line 64)
- public static void Speak(string text, bool interrupt = false) (line 74) — Note: calls `Tolk_Output` (speak + braille), not `Tolk_Speak`.
- public static void SpeakInterrupt(string text) (line 82)
- public static void Silence() (line 87)
- public static string GetActiveScreenReader() (line 95) — Note: returns `"None"` when unavailable, `"Unknown"` when detection returns null.
