using System.Collections.Generic;
using MelonLoader;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Maps NPE tutorial reminder localization keys to keyboard-focused replacement texts.
    /// Game reminders reference mouse/drag actions; this provider substitutes them with
    /// keyboard navigation instructions appropriate for screen reader users.
    ///
    /// Game keys follow the pattern: NPE/Game##/Turn##/ReminderType_Number
    /// We match on the ReminderType prefix to determine the category.
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
