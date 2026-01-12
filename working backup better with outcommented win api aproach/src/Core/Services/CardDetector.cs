using UnityEngine;
using MelonLoader;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// Static utility for detecting and extracting information from card GameObjects.
    /// Cards can appear in many contexts: rewards, hand, battlefield, graveyard, deck building, etc.
    /// This class provides a unified way to detect cards and extract their data.
    /// </summary>
    public static class CardDetector
    {
        // Cache to avoid repeated detection on same objects
        private static readonly Dictionary<int, bool> _isCardCache = new Dictionary<int, bool>();
        private static readonly Dictionary<int, GameObject> _cardRootCache = new Dictionary<int, GameObject>();

        /// <summary>
        /// Checks if a GameObject represents a card.
        /// Uses fast name-based checks first, component checks only as fallback.
        /// Results are cached for performance.
        /// </summary>
        public static bool IsCard(GameObject obj)
        {
            if (obj == null) return false;

            int id = obj.GetInstanceID();
            if (_isCardCache.TryGetValue(id, out bool cached))
                return cached;

            bool result = IsCardInternal(obj);
            _isCardCache[id] = result;
            return result;
        }

        private static bool IsCardInternal(GameObject obj)
        {
            // Fast check 1: Object name patterns (most common)
            string name = obj.name;
            if (name.Contains("CardAnchor") ||
                name.Contains("NPERewardPrefab_IndividualCard") ||
                name.Contains("MetaCardView") ||
                name.Contains("CDC #") ||
                name.Contains("DuelCardView"))
            {
                return true;
            }

            // Fast check 2: Parent name patterns
            var parent = obj.transform.parent;
            if (parent != null)
            {
                string parentName = parent.name;
                if (parentName.Contains("NPERewardPrefab_IndividualCard") ||
                    parentName.Contains("MetaCardView") ||
                    parentName.Contains("CardAnchor"))
                {
                    return true;
                }
            }

            // Slow check: Component names (only if name checks fail)
            // Only check components on the object itself, not children
            foreach (var component in obj.GetComponents<MonoBehaviour>())
            {
                if (component == null) continue;
                string typeName = component.GetType().Name;

                if (typeName == "BoosterMetaCardView" ||
                    typeName == "RewardDisplayCard" ||
                    typeName == "Meta_CDC" ||
                    typeName == "CardView" ||
                    typeName == "DuelCardView" ||
                    typeName == "CardRolloverZoomHandler")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the root card prefab from any card-related GameObject.
        /// Cached for performance.
        /// </summary>
        public static GameObject GetCardRoot(GameObject obj)
        {
            if (obj == null) return null;

            int id = obj.GetInstanceID();
            if (_cardRootCache.TryGetValue(id, out GameObject cached))
                return cached;

            GameObject result = GetCardRootInternal(obj);
            _cardRootCache[id] = result;
            return result;
        }

        private static GameObject GetCardRootInternal(GameObject obj)
        {
            Transform current = obj.transform;
            GameObject bestCandidate = obj;

            while (current != null)
            {
                string name = current.name;

                if (name.Contains("NPERewardPrefab_IndividualCard") ||
                    name.Contains("MetaCardView") ||
                    name.Contains("Prefab - BoosterMetaCardView"))
                {
                    bestCandidate = current.gameObject;
                }

                // Stop at containers
                if (name.Contains("Container") || name.Contains("CONTAINER"))
                    break;

                current = current.parent;
            }

            return bestCandidate;
        }

        /// <summary>
        /// Clears the detection cache. Call when scene changes.
        /// </summary>
        public static void ClearCache()
        {
            _isCardCache.Clear();
            _cardRootCache.Clear();
        }

        /// <summary>
        /// Extracts all available information from a card GameObject.
        /// </summary>
        public static CardInfo ExtractCardInfo(GameObject cardObj)
        {
            var info = new CardInfo();

            if (cardObj == null) return info;

            var cardRoot = GetCardRoot(cardObj);
            if (cardRoot == null) cardRoot = cardObj;

            var texts = cardRoot.GetComponentsInChildren<TMPro.TMP_Text>(true);

            foreach (var text in texts)
            {
                if (text == null || !text.gameObject.activeInHierarchy) continue;

                string rawContent = text.text?.Trim();
                if (string.IsNullOrEmpty(rawContent)) continue;

                string objName = text.gameObject.name;
                string content = CleanText(rawContent);

                // ManaCost uses sprite tags that get stripped by CleanText, so allow empty content for it
                if (string.IsNullOrEmpty(content) && !objName.Equals("ManaCost")) continue;

                switch (objName)
                {
                    case "Title":
                        info.Name = content;
                        break;

                    case "ManaCost":
                        info.ManaCost = ParseManaCost(rawContent);
                        break;

                    case "Type Line":
                        info.TypeLine = content;
                        break;

                    case "Artist Credit Text":
                        info.Artist = content;
                        break;

                    case "Label":
                        if (Regex.IsMatch(content, @"^\d+/\d+$"))
                        {
                            info.PowerToughness = content;
                        }
                        else if (rawContent.Contains("<i>"))
                        {
                            if (string.IsNullOrEmpty(info.FlavorText))
                                info.FlavorText = content;
                            else
                                info.FlavorText += " " + content;
                        }
                        else if (content.Length > 2)
                        {
                            if (string.IsNullOrEmpty(info.RulesText))
                                info.RulesText = content;
                            else
                                info.RulesText += " " + content;
                        }
                        break;
                }
            }

            info.IsValid = !string.IsNullOrEmpty(info.Name);
            return info;
        }

        /// <summary>
        /// Gets a short description of the card (name only).
        /// </summary>
        public static string GetCardName(GameObject cardObj)
        {
            var info = ExtractCardInfo(cardObj);
            return info.Name ?? "Unknown card";
        }

        /// <summary>
        /// Gets a summary of the card (name + type + P/T if creature).
        /// </summary>
        public static string GetCardSummary(GameObject cardObj)
        {
            var info = ExtractCardInfo(cardObj);
            if (!info.IsValid) return "Unknown card";

            var parts = new List<string> { info.Name };

            if (!string.IsNullOrEmpty(info.TypeLine))
                parts.Add(info.TypeLine);

            if (!string.IsNullOrEmpty(info.PowerToughness))
                parts.Add(info.PowerToughness);

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Builds a list of navigable info blocks for a card.
        /// </summary>
        public static List<CardInfoBlock> GetInfoBlocks(GameObject cardObj)
        {
            var blocks = new List<CardInfoBlock>();
            var info = ExtractCardInfo(cardObj);

            if (!string.IsNullOrEmpty(info.Name))
                blocks.Add(new CardInfoBlock("Name", info.Name));

            if (!string.IsNullOrEmpty(info.ManaCost))
                blocks.Add(new CardInfoBlock("Mana Cost", info.ManaCost));

            if (!string.IsNullOrEmpty(info.TypeLine))
                blocks.Add(new CardInfoBlock("Type", info.TypeLine));

            if (!string.IsNullOrEmpty(info.PowerToughness))
                blocks.Add(new CardInfoBlock("Power and Toughness", info.PowerToughness));

            if (!string.IsNullOrEmpty(info.RulesText))
                blocks.Add(new CardInfoBlock("Rules", info.RulesText));

            if (!string.IsNullOrEmpty(info.FlavorText))
                blocks.Add(new CardInfoBlock("Flavor", info.FlavorText));

            if (!string.IsNullOrEmpty(info.Artist))
                blocks.Add(new CardInfoBlock("Artist", info.Artist));

            return blocks;
        }

        private static string ParseManaCost(string rawManaCost)
        {
            var symbols = new List<string>();

            var matches = Regex.Matches(rawManaCost, @"name=""([^""]+)""");
            foreach (Match match in matches)
            {
                string symbol = match.Groups[1].Value;
                symbols.Add(ConvertManaSymbol(symbol));
            }

            if (symbols.Count == 0)
                return CleanText(rawManaCost);

            return string.Join(", ", symbols);
        }

        private static string ConvertManaSymbol(string symbol)
        {
            if (symbol.StartsWith("x"))
                symbol = symbol.Substring(1);

            switch (symbol.ToUpper())
            {
                case "W": return "White";
                case "U": return "Blue";
                case "B": return "Black";
                case "R": return "Red";
                case "G": return "Green";
                case "C": return "Colorless";
                case "S": return "Snow";
                case "X": return "X";
                case "T": return "Tap";
                case "Q": return "Untap";
                case "E": return "Energy";
                case "WU": case "UW": return "White or Blue";
                case "WB": case "BW": return "White or Black";
                case "UB": case "BU": return "Blue or Black";
                case "UR": case "RU": return "Blue or Red";
                case "BR": case "RB": return "Black or Red";
                case "BG": case "GB": return "Black or Green";
                case "RG": case "GR": return "Red or Green";
                case "RW": case "WR": return "Red or White";
                case "GW": case "WG": return "Green or White";
                case "GU": case "UG": return "Green or Blue";
                case "WP": case "PW": return "Phyrexian White";
                case "UP": case "PU": return "Phyrexian Blue";
                case "BP": case "PB": return "Phyrexian Black";
                case "RP": case "PR": return "Phyrexian Red";
                case "GP": case "PG": return "Phyrexian Green";
                default:
                    if (int.TryParse(symbol, out int num))
                        return num.ToString();
                    return symbol;
            }
        }

        private static string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            text = Regex.Replace(text, @"<[^>]+>", "");
            text = text.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }
    }

    /// <summary>
    /// Contains extracted card information.
    /// </summary>
    public struct CardInfo
    {
        public bool IsValid;
        public string Name;
        public string ManaCost;
        public string TypeLine;
        public string PowerToughness;
        public string RulesText;
        public string FlavorText;
        public string Artist;
    }

    /// <summary>
    /// A single navigable block of card information.
    /// </summary>
    public class CardInfoBlock
    {
        public string Label { get; }
        public string Content { get; }

        public CardInfoBlock(string label, string content)
        {
            Label = label;
            Content = content;
        }
    }
}
