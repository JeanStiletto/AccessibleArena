using MelonLoader;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Centralized debug configuration for the entire mod.
    /// Toggle DebugEnabled to control all debug output.
    /// Maintains a ring buffer of recent log entries for screen reader playback (Shift+F12).
    /// </summary>
    public static class DebugConfig
    {
        /// <summary>
        /// Master toggle for all debug logging. Set to false for release builds.
        /// </summary>
        public static bool DebugEnabled { get; set; } = true;

        // Category toggles (all require DebugEnabled = true to have effect)
        public static bool LogNavigation { get; set; } = true;
        public static bool LogPanelDetection { get; set; } = true;
        public static bool LogFocusTracking { get; set; } = true;
        public static bool LogCardInfo { get; set; } = true;
        public static bool LogActivation { get; set; } = true;
        public static bool LogAnnouncements { get; set; } = true;
        public static bool LogPatches { get; set; } = true;

        // Diagnostic mode for panel detection overlap audit (Stage 5.2)
        // When enabled, logs detailed information about which detector claims each panel
        public static bool LogPanelOverlapDiagnostic { get; set; } = true;

        // Ring buffer for recent log entries (speakable via Shift+F12)
        private const int MaxRecentEntries = 20;
        private static readonly string[] _recentEntries = new string[MaxRecentEntries];
        private static int _recentWriteIndex;
        private static int _recentCount;

        /// <summary>
        /// Log a debug message if DebugEnabled is true.
        /// </summary>
        /// <param name="tag">Component tag (e.g., "FocusTracker", "Navigator")</param>
        /// <param name="message">The message to log</param>
        public static void Log(string tag, string message)
        {
            if (DebugEnabled)
            {
                string entry = $"[{tag}] {message}";
                MelonLogger.Msg(entry);
                AddRecentEntry(entry);
            }
        }

        /// <summary>
        /// Log a debug message if both DebugEnabled and the category flag are true.
        /// </summary>
        /// <param name="categoryEnabled">Category-specific flag (e.g., LogNavigation)</param>
        /// <param name="tag">Component tag</param>
        /// <param name="message">The message to log</param>
        public static void LogIf(bool categoryEnabled, string tag, string message)
        {
            if (DebugEnabled && categoryEnabled)
            {
                string entry = $"[{tag}] {message}";
                MelonLogger.Msg(entry);
                AddRecentEntry(entry);
            }
        }

        private static void AddRecentEntry(string entry)
        {
            _recentEntries[_recentWriteIndex] = entry;
            _recentWriteIndex = (_recentWriteIndex + 1) % MaxRecentEntries;
            if (_recentCount < MaxRecentEntries)
                _recentCount++;
        }

        /// <summary>
        /// Returns the most recent log entries (oldest first), up to <paramref name="count"/>.
        /// </summary>
        public static string[] GetRecentEntries(int count = 5)
        {
            if (_recentCount == 0)
                return System.Array.Empty<string>();

            int take = System.Math.Min(count, _recentCount);
            var result = new string[take];

            // Read from oldest to newest within the requested window
            int startIndex = (_recentWriteIndex - take + MaxRecentEntries) % MaxRecentEntries;
            for (int i = 0; i < take; i++)
            {
                result[i] = _recentEntries[(startIndex + i) % MaxRecentEntries];
            }
            return result;
        }

        /// <summary>
        /// Reset all state to defaults. Used in unit tests to prevent state bleed between tests.
        /// </summary>
        internal static void Reset()
        {
            DebugEnabled = true;
            LogNavigation = true;
            LogPanelDetection = true;
            LogFocusTracking = true;
            LogCardInfo = true;
            LogActivation = true;
            LogAnnouncements = true;
            LogPatches = true;
            LogPanelOverlapDiagnostic = true;
            _recentWriteIndex = 0;
            _recentCount = 0;
            System.Array.Clear(_recentEntries, 0, MaxRecentEntries);
        }
    }
}
