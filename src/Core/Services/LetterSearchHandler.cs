using System;
using System.Collections.Generic;
using UnityEngine;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Handles buffered letter-key navigation for navigators.
    /// Typing a letter jumps to the first matching element. Repeating the same letter
    /// cycles through matches. Typing different letters builds a prefix (e.g., "ST" finds "Store").
    /// The buffer resets after a timeout or when the user navigates with arrow/tab keys.
    /// </summary>
    public class LetterSearchHandler
    {
        private string _buffer = "";
        private float _lastKeyTime;
        private const float BufferTimeoutSeconds = 1.0f;

        public string Buffer => _buffer;

        /// <summary>
        /// Process a letter key press and return the target index, or -1 if no match.
        /// </summary>
        public int HandleKey(char letter, IReadOnlyList<string> labels, int currentIndex)
        {
            float now = Time.time;
            if (now - _lastKeyTime > BufferTimeoutSeconds)
                _buffer = "";
            _lastKeyTime = now;

            char upper = char.ToUpperInvariant(letter);

            // Same letter repeated → cycle to next match
            if (_buffer.Length > 0 && AllSameChar(_buffer, upper))
            {
                _buffer += upper;
                return FindMatch(upper.ToString(), labels, currentIndex + 1);
            }

            // Different letter → extend buffer, search from start
            _buffer += upper;
            return FindMatch(_buffer, labels, 0);
        }

        public void Clear() => _buffer = "";

        private static bool AllSameChar(string s, char c)
        {
            for (int i = 0; i < s.Length; i++)
                if (s[i] != c) return false;
            return true;
        }

        private static int FindMatch(string prefix, IReadOnlyList<string> labels, int startIndex)
        {
            int count = labels.Count;
            for (int i = 0; i < count; i++)
            {
                int idx = (startIndex + i) % count;
                if (labels[idx] != null &&
                    labels[idx].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return idx;
            }
            return -1;
        }
    }
}
