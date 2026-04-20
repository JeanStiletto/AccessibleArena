# ManaTextFormatter.cs
Path: src/Core/Services/ManaTextFormatter.cs
Lines: 339

## Top-level comments
- Mana-text formatting helpers extracted from CardModelProvider (split 8/12). Parses MTGA internal mana notation ({oX}, bare oX sequences, standard {X}) and ManaQuantity arrays into readable text using localized strings from the Strings class. Pure string processing — no reflection state, no caches, no fields.

## public static class ManaTextFormatter (line 15)
### Methods
- public static string ParseManaSymbolsInText(string text) (line 24) — runs 4 regex passes: {oX} curly-brace notation, bare "NoX:" activated-cost prefixes, inline "oX" sequences anywhere in text, and standard {X} MTG notation. Used by 7 external call sites + internally from CardModelProvider.
- private static string ParseBareManaSequence(string sequence) (line 71) — parses "2oW", "o2oW", "oToRoR" into comma-joined readable parts; extracts optional leading number then all `o(digit+|letter)` tokens.
- private static string ConvertManaSymbolToText(string symbol) (line 101) — handles hybrid (W/U), Phyrexian (W/P), and compound patterns containing "o"; delegates singletons to ConvertSingleManaSymbol.
- private static string ConvertSingleManaSymbol(string symbol) (line 138) — switch over mana codes (T, Q, W, U, B, R, G, C, X, S, E) returning Strings.Mana* localizations; numeric 0-16 returned as-is.
- internal static string ParseManaQuantityArray(IEnumerable manaQuantities) (line 177) — iterates ManaQuantity-typed objects via reflection, reads Count field + Color/IsGeneric/IsPhyrexian/Hybrid/AltColor properties, groups simple colors by label, keeps hybrid/phyrexian individual. Respects ModSettings.ManaColorlessLabel and ModSettings.ManaGroupColors.
- internal static string ConvertManaColorToName(string colorEnum) (line 295) — maps ManaColor enum names (White/Blue/Black/Red/Green/Colorless/MultiColor/Generic/Snow/Phyrexian/X) + single-letter codes to Strings.Mana* labels.
- internal static void MergeClassLevelLines(List<string> lines) (line 320) — merges class level-up cost lines with their following effect lines in-place; detection: line contains `{o`, has `}: ` separator, text after colon short (<15) and ends with digit.
