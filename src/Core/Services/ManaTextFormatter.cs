using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AccessibleArena.Core.Models;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Mana-text formatting helpers extracted from CardModelProvider.
    /// Parses MTGA internal mana notation ({oX}, bare oX sequences, standard {X}) and
    /// ManaQuantity arrays into readable text using localized strings from the Strings class.
    /// </summary>
    public static class ManaTextFormatter
    {
        #region Mana Parsing

        /// <summary>
        /// Parses mana symbols in rules text like {oT}, {oR}, {o1}, etc. into readable text.
        /// Also handles bare format like "2oW:" used in activated ability costs.
        /// This matches the pattern used for mana cost presentation.
        /// </summary>
        public static string ParseManaSymbolsInText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Pattern 1: {oX} format with curly braces (MTGA internal notation)
            // Examples: {oT}, {oR}, {oW}, {oU}, {oB}, {oG}, {oC}, {o1}, {o2}, {oX}, {oS}, {oP}, {oE}
            // Also handles hybrid like {oW/U}, {oR/G}, {o2/W}
            text = Regex.Replace(text, @"\{o([^}]+)\}", match =>
            {
                string symbol = match.Groups[1].Value;
                return ConvertManaSymbolToText(symbol);
            });

            // Pattern 2: Bare format for activated ability costs (e.g., "2oW:", "oT:", "3oRoR:")
            // This handles patterns like: [number]o[color] at the start of ability text
            // Pattern breakdown: (optional number)(one or more oX sequences)(colon)
            text = Regex.Replace(text, @"^((\d*)(?:o(\d+|[WUBRGCTXSE]))+):", match =>
            {
                string fullCost = match.Groups[1].Value;
                return ParseBareManaSequence(fullCost) + ":";
            });

            // Pattern 3: Bare mana sequences anywhere in text (from parameterized hanger tooltips)
            // Examples: "Umwandlung o2oW:", "Du kannst o3oW zahlen"
            // Matches: one or more o(digit|color) tokens, not inside words
            text = Regex.Replace(text, @"(?<!\w)((?:o(?:\d+|[WUBRGCTXSE]))+)(?!\w)", match =>
            {
                return ParseBareManaSequence(match.Groups[1].Value);
            });

            // Pattern 4: Standard MTG mana notation {X} (used in ability text from localization DB)
            // Examples: {2}, {W}, {U}, {B}, {R}, {G}, {C}, {X}, {W/U}, {2/W}, {W/P}
            // Ward costs come through as e.g. "Ward {2}" or "Ward—{W}"
            text = Regex.Replace(text, @"\{([0-9XYWUBRGCTS][0-9WUBRGCP/]*)\}", match =>
            {
                string symbol = match.Groups[1].Value;
                return ConvertManaSymbolToText(symbol);
            });

            return text;
        }

        /// <summary>
        /// Parses a bare mana sequence like "2oW", "o2oW", or "oToRoR" into readable text.
        /// Handles both leading-number format (2oW) and o-prefix format (o2oW) for generic mana.
        /// </summary>
        private static string ParseBareManaSequence(string sequence)
        {
            if (string.IsNullOrEmpty(sequence))
                return "";

            var parts = new List<string>();

            // Extract leading number if present (generic mana without 'o' prefix, e.g. "2oW")
            var numberMatch = Regex.Match(sequence, @"^(\d+)");
            if (numberMatch.Success)
            {
                parts.Add(numberMatch.Groups[1].Value);
                sequence = sequence.Substring(numberMatch.Length);
            }

            // Extract all o(digit+|letter) patterns — handles both o2 (generic) and oW (color)
            var symbolMatches = Regex.Matches(sequence, @"o(\d+|[WUBRGCTXSE])");
            foreach (Match m in symbolMatches)
            {
                string symbol = m.Groups[1].Value;
                parts.Add(ConvertSingleManaSymbol(symbol));
            }

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Converts a mana symbol code to readable text.
        /// Uses localized strings from the Strings class.
        /// </summary>
        private static string ConvertManaSymbolToText(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
                return "";

            // Handle hybrid mana (e.g., "W/U", "R/G", "2/W")
            if (symbol.Contains("/"))
            {
                var parts = symbol.Split('/');
                if (parts.Length == 2)
                {
                    // Check for Phyrexian (ends with P)
                    if (parts[1].ToUpper() == "P")
                    {
                        string color = ConvertSingleManaSymbol(parts[0]);
                        return Strings.ManaPhyrexian(color);
                    }

                    string left = ConvertSingleManaSymbol(parts[0]);
                    string right = ConvertSingleManaSymbol(parts[1]);
                    return Strings.ManaHybrid(left, right);
                }
            }

            // Handle compound patterns like "4oW", "2oWoW", "oToR" (number + oX sequences)
            if (symbol.Contains("o"))
            {
                return ParseBareManaSequence(symbol);
            }

            return ConvertSingleManaSymbol(symbol);
        }

        /// <summary>
        /// Converts a single mana symbol character/code to readable text.
        /// Uses localized strings from the Strings class.
        /// </summary>
        private static string ConvertSingleManaSymbol(string symbol)
        {
            switch (symbol.ToUpper())
            {
                // Tap/Untap
                case "T": return Strings.ManaTap;
                case "Q": return Strings.ManaUntap;

                // Colors
                case "W": return Strings.ManaWhite;
                case "U": return Strings.ManaBlue;
                case "B": return Strings.ManaBlack;
                case "R": return Strings.ManaRed;
                case "G": return Strings.ManaGreen;
                case "C": return Strings.ManaColorless;

                // Special
                case "X": return Strings.ManaX;
                case "S": return Strings.ManaSnow;
                case "E": return Strings.ManaEnergy;

                // Generic mana (numbers) - don't need localization
                case "0": case "1": case "2": case "3": case "4":
                case "5": case "6": case "7": case "8": case "9":
                case "10": case "11": case "12": case "13": case "14":
                case "15": case "16":
                    return symbol;

                default:
                    // Return as-is if unknown
                    return symbol;
            }
        }

        /// <summary>
        /// Parses a ManaQuantity[] array into a readable mana cost string.
        /// Each ManaQuantity can represent one or more mana symbols.
        /// Generic mana uses the Quantity property for the actual amount.
        /// </summary>
        internal static string ParseManaQuantityArray(IEnumerable manaQuantities)
        {
            // Track colored mana grouped by symbol label (insertion-ordered)
            var colorCounts = new Dictionary<string, int>();
            var colorOrder = new List<string>();
            // Hybrid/Phyrexian symbols are kept as individual entries (can't be meaningfully grouped)
            var complexSymbols = new List<string>();
            int genericCount = 0;

            foreach (var mq in manaQuantities)
            {
                if (mq == null) continue;

                var mqType = mq.GetType();

                // Get the Count field (how many mana of this type)
                var countField = mqType.GetField("Count", AllInstanceFlags);

                // Get properties: Color, IsGeneric, IsPhyrexian, Hybrid, AltColor
                var colorProp = mqType.GetProperty("Color");
                var isGenericProp = mqType.GetProperty("IsGeneric");
                var isPhyrexianProp = mqType.GetProperty("IsPhyrexian");
                var hybridProp = mqType.GetProperty("Hybrid");
                var altColorProp = mqType.GetProperty("AltColor");

                if (colorProp == null) continue;

                try
                {
                    var color = colorProp.GetValue(mq);
                    bool isGeneric = isGenericProp != null && (bool)isGenericProp.GetValue(mq);
                    bool isPhyrexian = isPhyrexianProp != null && (bool)isPhyrexianProp.GetValue(mq);
                    bool isHybrid = hybridProp != null && (bool)hybridProp.GetValue(mq);

                    string colorName = color?.ToString() ?? "Unknown";

                    // Get the count from the Count field
                    int count = 1;
                    if (countField != null)
                    {
                        var countVal = countField.GetValue(mq);
                        if (countVal is uint uintCount)
                            count = (int)uintCount;
                        else if (countVal is int intCount)
                            count = intCount;
                    }

                    if (isGeneric)
                    {
                        genericCount += count;
                    }
                    else if (isHybrid || isPhyrexian)
                    {
                        // Complex symbols kept individually — can't be collapsed into "N hybrid"
                        string symbol = ConvertManaColorToName(colorName);

                        if (isHybrid && altColorProp != null)
                        {
                            var altColor = altColorProp.GetValue(mq);
                            string altColorName = altColor?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(altColorName) && altColorName != colorName)
                                symbol = Strings.ManaHybrid(symbol, ConvertManaColorToName(altColorName));
                        }

                        if (isPhyrexian)
                            symbol = Strings.ManaPhyrexian(symbol);

                        for (int i = 0; i < count; i++)
                            complexSymbols.Add(symbol);
                    }
                    else
                    {
                        // Simple colored mana — group by label (e.g. {BB} → "2 black")
                        string symbol = ConvertManaColorToName(colorName);
                        if (!colorCounts.ContainsKey(symbol))
                        {
                            colorCounts[symbol] = 0;
                            colorOrder.Add(symbol);
                        }
                        colorCounts[symbol] += count;
                    }
                }
                catch { /* Mana quantity reflection may fail on unexpected types */ }
            }

            var parts = new List<string>();

            bool colorlessLabel = AccessibleArenaMod.Instance?.Settings?.ManaColorlessLabel != false;
            bool groupColors = AccessibleArenaMod.Instance?.Settings?.ManaGroupColors != false;

            // Generic mana first
            if (genericCount > 0)
                parts.Add(colorlessLabel ? $"{genericCount} {Strings.ManaGeneric}" : $"{genericCount}");

            // Simple colors
            foreach (var symbol in colorOrder)
            {
                int cnt = colorCounts[symbol];
                if (groupColors)
                {
                    parts.Add(cnt > 1 ? $"{cnt} {symbol}" : symbol);
                }
                else
                {
                    for (int i = 0; i < cnt; i++)
                        parts.Add(symbol);
                }
            }

            // Complex symbols (hybrid, phyrexian) appended individually
            parts.AddRange(complexSymbols);

            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        /// <summary>
        /// Converts a mana color enum name to a readable name.
        /// </summary>
        internal static string ConvertManaColorToName(string colorEnum)
        {
            switch (colorEnum)
            {
                case "White": case "W": return Strings.ManaWhite;
                case "Blue": case "U": return Strings.ManaBlue;
                case "Black": case "B": return Strings.ManaBlack;
                case "Red": case "R": return Strings.ManaRed;
                case "Green": case "G": return Strings.ManaGreen;
                case "Colorless": case "C": return Strings.ManaColorless;
                case "MultiColor": case "Multicolor": return Strings.ManaMulticolor;
                case "Generic": return Strings.ManaGeneric;
                case "Snow": case "S": return Strings.ManaSnow;
                case "Phyrexian": case "P": return Strings.ManaPhyrexianBare;
                case "X": return Strings.ManaX;
                default: return colorEnum;
            }
        }

        /// <summary>
        /// Merges class level-up cost lines with their following effect lines.
        /// Class enchantments produce abilities like: "{o2oW}: Stufe 2" followed by the effect text.
        /// This merges them into a single line: "{o2oW}: Stufe 2. Effect text here."
        /// Detection: line contains mana symbols ({o), has "}: " separator, text after colon is short and ends with digit.
        /// </summary>
        internal static void MergeClassLevelLines(List<string> lines)
        {
            for (int i = lines.Count - 2; i >= 0; i--)
            {
                var line = lines[i];
                int colonIdx = line.LastIndexOf("}: ");
                if (colonIdx < 0 || !line.Contains("{o")) continue;

                string afterColon = line.Substring(colonIdx + 3).Trim();
                if (afterColon.Length > 0 && afterColon.Length < 15 && char.IsDigit(afterColon[afterColon.Length - 1]))
                {
                    lines[i] = line + ". " + lines[i + 1];
                    lines.RemoveAt(i + 1);
                }
            }
        }

        #endregion
    }
}
