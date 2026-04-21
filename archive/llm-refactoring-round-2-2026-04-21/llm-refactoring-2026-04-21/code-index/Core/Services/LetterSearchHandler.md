# LetterSearchHandler.cs
Path: src/Core/Services/LetterSearchHandler.cs
Lines: 67

## Top-level comments
- Handles buffered letter-key navigation. Typing a letter jumps to the first matching element; repeating the same letter cycles through matches; different letters extend the buffer as a prefix. Buffer resets after a 1-second timeout.

## public class LetterSearchHandler (line 13)

### Fields
- private string _buffer = "" (line 15)
- private float _lastKeyTime (line 16)
- private const float BufferTimeoutSeconds = 1.0f (line 17)

### Properties
- public string Buffer => _buffer (line 19)

### Methods
- public int HandleKey(char letter, IReadOnlyList<string> labels, int currentIndex) (line 24)
- public void Clear() (line 45)
- private static bool AllSameChar(string s, char c) (line 47)
- private static int FindMatch(string prefix, IReadOnlyList<string> labels, int startIndex) (line 54)
