using System.Collections.Generic;
using MelonLoader;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Maps NPE tutorial localization keys to keyboard-focused replacement/hint texts.
    /// Game reminders reference mouse/drag actions; this provider substitutes them with
    /// keyboard navigation instructions appropriate for screen reader users.
    ///
    /// Four mapping modes (checked in order):
    /// 1. Exact reminder key matching: specific reminder loc keys → override for individual reminders
    /// 2. Reminder prefix matching: NPE/Game##/Turn##/ReminderType_Number → matches on ReminderType
    /// 3. Dialog exact key matching: specific dialog loc keys → additional hints triggered by NPC lines
    /// 4. Read-aloud dialog detection: AlwaysReminder keys (error interceptions) read with game's own text
    /// </summary>
    public static class NPETutorialTextProvider
    {
        // Maps exact NPE reminder localization keys to mod localization keys.
        // Checked BEFORE prefix matching, allowing specific reminders to override the default for their type.
        private static readonly Dictionary<string, string> ExactKeyToModKey = new Dictionary<string, string>
        {
            // "Du kannst ein Land pro Zug spielen" - standalone land play reminder (no nearby dialog)
            { "NPE/Game01/Turn03/ActionReminder_0", "NPE_Hint_PlayLand" },
            // Game 3 (aura deck) - enchanting targets: "click on your creature to enchant it"
            { "NPE/Game03/Turn02/TargetReminder_45", "NPE_Hint_EnchantTarget" },
            { "NPE/Game03/Turn04/TargetReminder_49", "NPE_Hint_EnchantTarget" },
            { "NPE/Game03/Turn06/TargetReminder_50", "NPE_Hint_EnchantTarget" },
            { "NPE/Extra/Extra09", "NPE_Hint_EnchantTarget" },
            // Game 2+ - "Beschwöre eine Kreatur, indem du sie ins Spiel ziehst"
            { "NPE/Game02/Turn01/ActionReminder_33", "NPE_Hint_CastCreature" },
            // Game 5 - Infizierende Mumie forces discard: "Wirf eine Ebene ab"
            { "NPE/Game05/Turn02/PickReminder_67", "NPE_Hint_DiscardCard" },
        };

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
            { "TargetReminder", "NPE_Hint_Target" },
        };

        // Maps DeluxeTooltipType enum names to mod localization keys.
        // These are visual-only tutorial popups (animations, card demos) with no text for screen readers.
        // Note: Mana tooltip is suppressed (hint delivered via Sparky_05 dialog at execute time).
        private static readonly Dictionary<string, string> TooltipTypeToModKey = new Dictionary<string, string>
        {
        };

        // Maps exact NPE dialog localization keys to mod localization keys.
        // Add entries here to provide extra hints when specific NPC dialog lines appear.
        // Key = game's loc key (e.g. "NPE/Game01/Turn02/Dialog_3"), Value = mod locale key.
        private static readonly Dictionary<string, string> DialogKeyToModKey = new Dictionary<string, string>
        {
            // "Für Zaubersprüche musst du Mana aus Ländern ziehen" - mana explanation
            { "NPE/Game01/Turn01/Sparky_05", "NPE_Tooltip_Mana" },
            // "Fahre für alle Infos mit der Maus über die Karte" - mouse hover hint, replace with keyboard
            { "NPE/Game01/g1_142", "NPE_DialogHint_CardDetails" },
            // "Du wurdest geblockt!" - hint about battlefield navigation during combat
            { "NPE/Game01/Turn03/g1_t3_143", "NPE_DialogHint_BattlefieldBlocked" },
            // "Dieses Ungetüm zerstört deine Kreaturen, falls du angreifst" - no DontAttackReminder fires
            { "NPE/Game01/Turn05/Sparky_15", "NPE_Hint_SkipAttack" },
            // "Nette Verteidigung" - after first high-cost creature summon, explain mana costs
            { "NPE/Game02/Turn05/Sparky_07", "NPE_DialogHint_ManaCosts" },
            // "I've got nothing. Really. It's in your hands." - hint about land summary shortcuts
            { "NPE/Game04/Turn08/ViperNang_14", "NPE_DialogHint_LandSummary" },
            // "Mach weiter. Er hat bald keine Karten mehr." - hint about opponent hand/library count shortcuts
            { "NPE/Game03/Turn04/g3_t4_145", "NPE_DialogHint_OpponentCounts" },
            // "Mach dich bereit." - opponent's blockers phase, player should cast combat trick
            { "NPE/Game04/Turn07/Sparky_07", "NPE_DialogHint_CastCombatTrick" },
            // Game 3 (aura deck) - "Alles hängt zusammen" - after opponent's creature gets enchanted
            { "NPE/Game03/Turn02/Calubi_04", "NPE_DialogHint_Attachments" },
            // "Der Vogel kann alle Blocker am Boden überfliegen" - explain keywords
            { "NPE/Game03/Turn02/Sparky_01", "NPE_DialogHint_Keywords" },
            // "Manchmal ist Ab durch die Mitte am besten" - after attacking the 0/4 crab and getting blocked
            { "NPE/Game03/Turn03/Calubi_08", "NPE_DialogHint_CreatureHealing" },
        };

        /// <summary>
        /// Gets a keyboard-focused replacement for an NPE reminder, or null if no mapping exists.
        /// </summary>
        /// <param name="npeLocKey">The game's localization key (e.g. "NPE/Game01/Turn03/ActionReminder_0")</param>
        /// <returns>Replacement text from mod localization, or null to use original</returns>
        public static string GetReplacementText(string npeLocKey)
        {
            if (string.IsNullOrEmpty(npeLocKey)) return null;

            // Check exact key overrides first (specific reminders that need different text than their prefix group)
            if (ExactKeyToModKey.TryGetValue(npeLocKey, out string exactModKey))
            {
                string exactReplacement = LocaleManager.Instance.Get(exactModKey);
                if (!string.IsNullOrEmpty(exactReplacement))
                {
                    Log.Msg("NPETutorialText", $"Replaced exact key '{npeLocKey}' with mod text");
                    return exactReplacement;
                }
            }

            // Fall back to prefix matching
            string prefix = ExtractReminderType(npeLocKey);
            if (prefix == null) return null;

            if (PrefixToModKey.TryGetValue(prefix, out string modKey))
            {
                string replacement = LocaleManager.Instance.Get(modKey);
                if (!string.IsNullOrEmpty(replacement))
                {
                    Log.Msg("NPETutorialText", $"Replaced '{prefix}' (key: {npeLocKey}) with mod text");
                    return replacement;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a keyboard-focused replacement for an NPE tooltip, or null if no mapping exists.
        /// Tooltip types are visual-only popups (animations, card demos) with no useful text.
        /// </summary>
        /// <param name="tooltipType">The DeluxeTooltipType enum name (e.g. "Mana", "Combat")</param>
        /// <returns>Replacement text from mod localization, or null to use default format</returns>
        public static string GetTooltipText(string tooltipType)
        {
            if (string.IsNullOrEmpty(tooltipType)) return null;

            if (TooltipTypeToModKey.TryGetValue(tooltipType, out string modKey))
            {
                string text = LocaleManager.Instance.Get(modKey);
                if (!string.IsNullOrEmpty(text))
                {
                    Log.Msg("NPETutorialText", $"Tooltip '{tooltipType}' replaced with mod text");
                    return text;
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
                    Log.Msg("NPETutorialText", $"Dialog hint for key: {dialogLocKey}");
                    return hint;
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if an NPE dialog line should be read aloud as-is (not suppressed as voice-acted NPC chatter).
        /// AlwaysReminder interceptions are error messages (wrong target, can't afford, etc.) that are
        /// essential for blind players to understand why their action was rejected.
        /// </summary>
        /// <param name="dialogLocKey">The dialog's localization key</param>
        /// <returns>True if the dialog text should be announced via screen reader</returns>
        public static bool ShouldReadAloud(string dialogLocKey)
        {
            if (string.IsNullOrEmpty(dialogLocKey)) return false;

            // AlwaysReminder keys are error interceptions (BadTargetting, CantAffordSpell, etc.)
            return dialogLocKey.Contains("/AlwaysReminder_");
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
