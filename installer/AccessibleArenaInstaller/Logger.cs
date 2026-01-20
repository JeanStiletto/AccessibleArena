using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace AccessibleArenaInstaller
{
    /// <summary>
    /// Simple logger that buffers installation progress and optionally writes to file.
    /// </summary>
    public static class Logger
    {
        private static readonly string LogPath;
        private static readonly StringBuilder LogBuffer = new StringBuilder();
        private static bool _hasErrors = false;

        static Logger()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            LogPath = Path.Combine(desktop, "AccessibleArena_Install.log");
        }

        public static string GetLogPath() => LogPath;
        public static bool HasErrors => _hasErrors;

        public static void Info(string message)
        {
            Log("INFO", message);
        }

        public static void Warning(string message)
        {
            Log("WARN", message);
        }

        public static void Error(string message)
        {
            Log("ERROR", message);
            _hasErrors = true;
        }

        public static void Error(string message, Exception ex)
        {
            Log("ERROR", $"{message}: {ex.Message}");
            Log("ERROR", $"Stack trace: {ex.StackTrace}");
            _hasErrors = true;
        }

        private static void Log(string level, string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logLine = $"[{timestamp}] [{level}] {message}";

            LogBuffer.AppendLine(logLine);

            // Also write to console for debugging
            Console.WriteLine(logLine);
        }

        /// <summary>
        /// Writes all buffered log entries to the log file.
        /// </summary>
        public static void Flush()
        {
            try
            {
                // Add header
                var fullLog = new StringBuilder();
                fullLog.AppendLine("===========================================");
                fullLog.AppendLine("Accessible Arena - Installation Log");
                fullLog.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                fullLog.AppendLine("===========================================");
                fullLog.AppendLine();
                fullLog.Append(LogBuffer);
                fullLog.AppendLine();
                fullLog.AppendLine("===========================================");
                fullLog.AppendLine("End of log");
                fullLog.AppendLine("===========================================");

                File.WriteAllText(LogPath, fullLog.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write log file: {ex.Message}");
            }
        }

        /// <summary>
        /// Asks the user if they want to save the log file, then saves if yes.
        /// </summary>
        /// <param name="alwaysAsk">If true, always asks. If false, only asks if there were errors.</param>
        /// <returns>True if log was saved, false otherwise.</returns>
        public static bool AskAndSave(bool alwaysAsk = false)
        {
            if (!alwaysAsk && !_hasErrors)
            {
                // Success with no errors - don't bother the user
                return false;
            }

            string message = _hasErrors
                ? "There were some warnings or errors during installation.\n\nWould you like to save a log file to your Desktop for troubleshooting?"
                : "Would you like to save a log file to your Desktop?";

            var result = MessageBox.Show(
                message,
                "Save Log File?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                Flush();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Opens the log file in the default text editor.
        /// </summary>
        public static void OpenLogFile()
        {
            try
            {
                if (File.Exists(LogPath))
                {
                    System.Diagnostics.Process.Start(LogPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to open log file: {ex.Message}");
            }
        }
    }
}
