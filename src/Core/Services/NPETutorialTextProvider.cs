using System.Collections.Generic;
using MelonLoader;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Maps NPE tutorial localization keys to keyboard-focused replacement/hint texts.
    /// Game reminders reference mouse/drag actions; this provider substitutes them with
    /// keyboard navigation instructions appropriate for screen reader users.
    ///
    /// Two mapping modes:
    /// 1. Reminder prefix matching: NPE/Game##/Turn##/ReminderType_Number → matches on ReminderType
    /// 2. Dialog exact key matching: specific dialog loc keys → additional hints triggered by NPC lines
    /// </summary>
    public static class NPETutorialTextProvider
    {
        // Maps NPE reminder type prefixes to mod localization keys
        private static readonly Dictionary<string, string> PrefixToModKey = new Dictionary<string, string>
        {
            { "ActionReminder", "NPE_Hint_PlayCard" },
            { "BlockingReminder", "NPE_Hint_AssignBlocker" },
            { "BlockingSubmitReminder", "NPE_Hint_ConfirmBlocks" },
            { "AttackingReminder", "NPE_Hint_AssignAttacker" },
            { "AttackingSubmitReminder", "NPE_Hint_ConfirmAttackers" },
            { "DontAttackReminder", "NPE_Hint_SkipAttack" },
            { "PickReminder", "NPE_Hint_SelectCard" },
        };

        // Maps exact NPE dialog localization keys to mod localization keys.
        // Add entries here to provide extra hints when specific NPC dialog lines appear.
        // Key = game's loc key (e.g. "NPE/Game01/Turn02/Dialog_3"), Value = mod locale key.
        private static readonly Dictionary<string, string> DialogKeyToModKey = new Dictionary<string, string>
        {
            // "I've got nothing. Really. It's in your hands." - hint about land summary shortcuts
            { "NPE/Game04/Turn08/ViperNang_14", "NPE_DialogHint_LandSummary" },
        };

        /// <summary>
        /// Gets a keyboard-focused replacement for an NPE reminder, or null if no mapping exists.
        /// </summary>
        /// <param name="npeLocKey">The game's localization key (e.g. "NPE/Game01/Turn03/ActionReminder_0")</param>
        /// <returns>Replacement text from mod localization, or null to use original</returns>
        public static string GetReplacementText(string npeLocKey)
        {
            if (string.IsNullOrEmpty(npeLocKey)) return null;

            string prefix = ExtractReminderType(npeLocKey);
            if (prefix == null) return null;

            if (PrefixToModKey.TryGetValue(prefix, out string modKey))
            {
                string replacement = LocaleManager.Instance.Get(modKey);
                if (!string.IsNullOrEmpty(replacement))
                {
                    MelonLogger.Msg($"[NPETutorialText] Replaced '{prefix}' (key: {npeLocKey}) with mod text");
                    return replacement;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a hint to announce when a specific NPE dialog line appears, or null if none.
        /// Dialog lines are voice-acted and not read aloud; this provides supplementary
        /// keyboard hints triggered by specific NPC lines.
        /// </summary>
        /// <param name="dialogLocKey">The dialog's localization key</param>
        /// <returns>Hint text from mod localization, or null if no hint for this dialog</returns>
        public static string GetDialogHint(string dialogLocKey)
        {
            if (string.IsNullOrEmpty(dialogLocKey)) return null;

            if (DialogKeyToModKey.TryGetValue(dialogLocKey, out string modKey))
            {
                string hint = LocaleManager.Instance.Get(modKey);
                if (!string.IsNullOrEmpty(hint))
                {
                    MelonLogger.Msg($"[NPETutorialText] Dialog hint for key: {dialogLocKey}");
                    return hint;
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts the reminder type prefix from an NPE localization key.
        /// "NPE/Game01/Turn03/ActionReminder_0" → "ActionReminder"
        /// "NPE/Game01/Turn03/BlockingReminder_59_Handheld" → "BlockingReminder"
        /// </summary>
        private static string ExtractReminderType(string npeLocKey)
        {
            int lastSlash = npeLocKey.LastIndexOf('/');
            if (lastSlash < 0 || lastSlash >= npeLocKey.Length - 1) return null;

            string lastSegment = npeLocKey.Substring(lastSlash + 1);

            // Everything before the first underscore is the type
            int firstUnderscore = lastSegment.IndexOf('_');
            if (firstUnderscore <= 0) return null;

            return lastSegment.Substring(0, firstUnderscore);
        }
    }
}
