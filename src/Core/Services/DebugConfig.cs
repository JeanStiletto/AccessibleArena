using MelonLoader;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Centralized debug configuration for the entire mod.
    /// Toggle DebugEnabled to control all debug output.
    /// </summary>
    public static class DebugConfig
    {
        /// <summary>
        /// Master toggle for all debug logging. Set to false for release builds.
        /// </summary>
        public static bool DebugEnabled { get; set; } = false;

        // Category toggles (all require DebugEnabled = true to have effect)
        public static bool LogNavigation { get; set; } = true;
        public static bool LogPanelDetection { get; set; } = true;
        public static bool LogFocusTracking { get; set; } = true;
        public static bool LogCardInfo { get; set; } = true;
        public static bool LogActivation { get; set; } = true;
        public static bool LogAnnouncements { get; set; } = true;
        public static bool LogPatches { get; set; } = true;

        /// <summary>
        /// Log a debug message if DebugEnabled is true.
        /// </summary>
        /// <param name="tag">Component tag (e.g., "FocusTracker", "Navigator")</param>
        /// <param name="message">The message to log</param>
        public static void Log(string tag, string message)
        {
            if (DebugEnabled)
                MelonLogger.Msg($"[{tag}] {message}");
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
                MelonLogger.Msg($"[{tag}] {message}");
        }

        /// <summary>
        /// Enable all debug logging (for development/debugging sessions).
        /// </summary>
        public static void EnableAll()
        {
            DebugEnabled = true;
            LogNavigation = true;
            LogPanelDetection = true;
            LogFocusTracking = true;
            LogCardInfo = true;
            LogActivation = true;
            LogAnnouncements = true;
            LogPatches = true;
        }

        /// <summary>
        /// Disable all debug logging (for release/performance).
        /// </summary>
        public static void DisableAll()
        {
            DebugEnabled = false;
        }
    }
}
