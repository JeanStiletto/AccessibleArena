using System;
using UnityEngine;

namespace AccessibleArena.Core.Utils
{
    /// <summary>
    /// Thin wrapper over the system clipboard so mod features can copy text
    /// (card names, player IDs, announcements) for the user to paste elsewhere.
    /// Uses Unity's <see cref="GUIUtility.systemCopyBuffer"/>, which maps to the
    /// native OS clipboard on Windows.
    /// </summary>
    public static class ClipboardUtil
    {
        /// <summary>
        /// Copies <paramref name="text"/> to the system clipboard.
        /// Returns false (without throwing) for empty text or on any clipboard error.
        /// </summary>
        public static bool Copy(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            try
            {
                GUIUtility.systemCopyBuffer = text;
                Log.Msg("Clipboard", $"Copied {text.Length} chars to clipboard");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Clipboard", "Failed to write to clipboard", ex);
                return false;
            }
        }
    }
}
