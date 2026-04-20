using System;
using MelonLoader;

namespace AccessibleArena.Core.Utils
{
    /// <summary>
    /// Central logging entry point for the mod. Routes all output through MelonLogger and
    /// maintains a ring buffer of recent entries for screen-reader playback (Shift+F12).
    ///
    /// Call-site shape: <c>Log.Msg("Tag", "message")</c> produces <c>[Tag] message</c>.
    /// Category sugar (<see cref="Nav"/>, <see cref="Focus"/>, ...) is gated on the matching
    /// <see cref="LogNavigation"/>, <see cref="LogFocusTracking"/>, ... flags.
    /// </summary>
    public static class Log
    {
        /// <summary>Master toggle. When false, every helper below is a no-op.</summary>
        public static bool Enabled { get; set; } = true;

        // Category toggles (all require Enabled = true to have effect)
        public static bool LogNavigation { get; set; } = true;
        public static bool LogPanelDetection { get; set; } = true;
        public static bool LogFocusTracking { get; set; } = true;
        public static bool LogCardInfo { get; set; } = true;
        public static bool LogActivation { get; set; } = true;
        public static bool LogAnnouncements { get; set; } = true;
        public static bool LogPatches { get; set; } = true;
        public static bool LogPanelOverlapDiagnostic { get; set; } = true;

        // Ring buffer for recent log entries (speakable via Shift+F12)
        private const int MaxRecentEntries = 20;
        private static readonly string[] _recentEntries = new string[MaxRecentEntries];
        private static int _recentWriteIndex;
        private static int _recentCount;

        // -- Info --

        /// <summary>Unconditional info. Emits <c>[tag] message</c> when <see cref="Enabled"/> is true.</summary>
        public static void Msg(string tag, string message)
        {
            if (!Enabled) return;
            string entry = $"[{tag}] {message}";
            MelonLogger.Msg(entry);
            AddRecentEntry(entry);
        }

        /// <summary>Gated info. Emits only when both <see cref="Enabled"/> and <paramref name="categoryEnabled"/> are true.</summary>
        public static void MsgIf(bool categoryEnabled, string tag, string message)
        {
            if (!Enabled || !categoryEnabled) return;
            string entry = $"[{tag}] {message}";
            MelonLogger.Msg(entry);
            AddRecentEntry(entry);
        }

        // -- Category sugar (gated on matching flag) --

        public static void Nav(string tag, string message) => MsgIf(LogNavigation, tag, message);
        public static void Panel(string tag, string message) => MsgIf(LogPanelDetection, tag, message);
        public static void Focus(string tag, string message) => MsgIf(LogFocusTracking, tag, message);
        public static void Card(string tag, string message) => MsgIf(LogCardInfo, tag, message);
        public static void Activation(string tag, string message) => MsgIf(LogActivation, tag, message);
        public static void Announce(string tag, string message) => MsgIf(LogAnnouncements, tag, message);
        public static void Patch(string tag, string message) => MsgIf(LogPatches, tag, message);
        public static void Overlap(string tag, string message) => MsgIf(LogPanelOverlapDiagnostic, tag, message);

        // -- Warning --

        /// <summary>Warning. Routes through <see cref="MelonLogger.Warning(string)"/> and the ring buffer.</summary>
        public static void Warn(string tag, string message)
        {
            if (!Enabled) return;
            string entry = $"[{tag}] {message}";
            MelonLogger.Warning(entry);
            AddRecentEntry(entry);
        }

        /// <summary>Warning with exception. Output: <c>[tag] message: ex.Message</c>.</summary>
        public static void Warn(string tag, string message, Exception ex)
        {
            if (!Enabled) return;
            string entry = $"[{tag}] {message}: {ex?.Message}";
            MelonLogger.Warning(entry);
            AddRecentEntry(entry);
        }

        // -- Error --

        /// <summary>Error. Routes through <see cref="MelonLogger.Error(string)"/> and the ring buffer.</summary>
        public static void Error(string tag, string message)
        {
            if (!Enabled) return;
            string entry = $"[{tag}] {message}";
            MelonLogger.Error(entry);
            AddRecentEntry(entry);
        }

        /// <summary>Error with exception. Output: <c>[tag] message: ex.Message</c>.</summary>
        public static void Error(string tag, string message, Exception ex)
        {
            if (!Enabled) return;
            string entry = $"[{tag}] {message}: {ex?.Message}";
            MelonLogger.Error(entry);
            AddRecentEntry(entry);
        }

        // -- Ring buffer --

        private static void AddRecentEntry(string entry)
        {
            _recentEntries[_recentWriteIndex] = entry;
            _recentWriteIndex = (_recentWriteIndex + 1) % MaxRecentEntries;
            if (_recentCount < MaxRecentEntries)
                _recentCount++;
        }

        /// <summary>Returns the most recent log entries (oldest first), up to <paramref name="count"/>.</summary>
        public static string[] GetRecentEntries(int count = 5)
        {
            if (_recentCount == 0)
                return Array.Empty<string>();

            int take = Math.Min(count, _recentCount);
            var result = new string[take];

            int startIndex = (_recentWriteIndex - take + MaxRecentEntries) % MaxRecentEntries;
            for (int i = 0; i < take; i++)
            {
                result[i] = _recentEntries[(startIndex + i) % MaxRecentEntries];
            }
            return result;
        }

        /// <summary>Reset all state to defaults. Used by unit tests to prevent state bleed.</summary>
        internal static void Reset()
        {
            Enabled = true;
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
            Array.Clear(_recentEntries, 0, MaxRecentEntries);
        }
    }
}
