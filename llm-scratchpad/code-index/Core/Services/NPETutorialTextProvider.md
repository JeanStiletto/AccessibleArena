# NPETutorialTextProvider.cs
Path: src/Core/Services/NPETutorialTextProvider.cs
Lines: 203

## Top-level comments
- Maps NPE tutorial localization keys to keyboard-focused replacement and hint texts. Game reminders reference mouse/drag actions; this provider substitutes them with keyboard navigation instructions. Four mapping modes: exact reminder key match, reminder prefix match, exact dialog key match, and AlwaysReminder read-aloud detection.

## public static class NPETutorialTextProvider (line 17)

### Fields
- private static readonly Dictionary<string, string> ExactKeyToModKey (line 21)
- private static readonly Dictionary<string, string> PrefixToModKey (line 37)
- private static readonly Dictionary<string, string> TooltipTypeToModKey (line 52)
- private static readonly Dictionary<string, string> DialogKeyToModKey (line 59)

### Methods
- public static string GetReplacementText(string npeLocKey) (line 90)
- public static string GetTooltipText(string tooltipType) (line 128)
- public static string GetDialogHint(string dialogLocKey) (line 152)
- public static bool ShouldReadAloud(string dialogLocKey) (line 176)
- private static string ExtractReminderType(string npeLocKey) (line 189)
