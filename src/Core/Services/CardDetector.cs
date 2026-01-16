using UnityEngine;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// Static utility for detecting card GameObjects and basic card operations.
    /// For card data extraction and model access, use CardModelProvider directly.
    ///
    /// Detection: IsCard, GetCardRoot, HasValidTargetsOnBattlefield
    /// Model access: Delegated to CardModelProvider (pass-through methods provided for compatibility)
    /// </summary>
    public static class CardDetector
    {
        // Cache to avoid repeated detection on same objects
        private static readonly Dictionary<int, bool> _isCardCache = new Dictionary<int, bool>();
        private static readonly Dictionary<int, GameObject> _cardRootCache = new Dictionary<int, GameObject>();

        #region Card Detection

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
        /// Checks if any valid targets have HotHighlight (indicating targeting mode).
        /// Scans battlefield, stack, AND player portraits for "any target" spells like Shock.
        /// </summary>
        public static bool HasValidTargetsOnBattlefield()
        {
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy)
                    continue;

                string name = go.name;

                // Check if it's on the battlefield or stack
                Transform current = go.transform;
                bool inTargetZone = false;
                while (current != null)
                {
                    if (current.name.Contains("BattlefieldCardHolder") || current.name.Contains("StackCardHolder"))
                    {
                        inTargetZone = true;
                        break;
                    }
                    current = current.parent;
                }

                // Also check player portrait areas (for "any target" spells)
                bool isPlayerArea = name.Contains("MatchTimer") ||
                                    (name.Contains("Player") && (name.Contains("Portrait") || name.Contains("Avatar")));

                if (!inTargetZone && !isPlayerArea)
                    continue;

                // Check for HotHighlight child (indicates valid target)
                if (HasHotHighlight(go))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a GameObject has an active HotHighlight child, indicating it's a valid target
        /// or can be played/activated. This is the unified method - all callers should use this
        /// instead of implementing their own check.
        /// </summary>
        /// <param name="obj">The GameObject to check (typically a card or player portrait)</param>
        /// <returns>True if an active HotHighlight child exists</returns>
        public static bool HasHotHighlight(GameObject obj)
        {
            if (obj == null) return false;

            foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
            {
                // Skip null and the object itself
                if (child == null || child.gameObject == obj) continue;
                // Skip inactive children
                if (!child.gameObject.activeInHierarchy) continue;

                // HotHighlight variants: HotHighlightBattlefield, HotHighlightHand, etc.
                if (child.name.Contains("HotHighlight"))
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Cache Management

        /// <summary>
        /// Clears all detection and model caches. Call when scene changes.
        /// </summary>
        public static void ClearCache()
        {
            _isCardCache.Clear();
            _cardRootCache.Clear();
            // Also clear model provider cache
            CardModelProvider.ClearCache();
        }

        #endregion

        #region Model Access (Delegated to CardModelProvider)

        // These methods delegate to CardModelProvider for backwards compatibility.
        // New code should use CardModelProvider directly when only needing model data.

        /// <summary>
        /// Gets the DuelScene_CDC component from a card GameObject.
        /// Delegates to CardModelProvider.
        /// </summary>
        public static Component GetDuelSceneCDC(GameObject card)
            => CardModelProvider.GetDuelSceneCDC(card);

        /// <summary>
        /// Gets the Model object from a DuelScene_CDC component.
        /// Delegates to CardModelProvider.
        /// </summary>
        public static object GetCardModel(Component cdcComponent)
            => CardModelProvider.GetCardModel(cdcComponent);

        /// <summary>
        /// Extracts all available information from a card GameObject.
        /// For DuelScene cards, tries Model data first (works for compacted cards).
        /// Falls back to UI text extraction for Meta scene cards or if Model fails.
        /// </summary>
        public static CardInfo ExtractCardInfo(GameObject cardObj)
        {
            if (cardObj == null) return new CardInfo();

            // Try Model-based extraction first (works for compacted battlefield cards)
            var modelInfo = CardModelProvider.ExtractCardInfoFromModel(cardObj);
            if (modelInfo.HasValue && modelInfo.Value.IsValid)
            {
                MelonLogger.Msg($"[CardDetector] Using MODEL extraction: {modelInfo.Value.Name}");
                return modelInfo.Value;
            }

            // Fall back to UI text extraction (for Meta scene cards or if Model fails)
            var uiInfo = ExtractCardInfoFromUI(cardObj);
            MelonLogger.Msg($"[CardDetector] Using UI extraction: {uiInfo.Name ?? "null"} (Model failed: {(modelInfo.HasValue ? "invalid" : "no CDC")})");
            return uiInfo;
        }

        /// <summary>
        /// Extracts card info from UI text elements.
        /// Used for Meta scene cards (rewards, deck building) where Model is not available.
        /// </summary>
        private static CardInfo ExtractCardInfoFromUI(GameObject cardObj)
        {
            var info = new CardInfo();

            if (cardObj == null) return info;

            var cardRoot = GetCardRoot(cardObj);
            if (cardRoot == null) cardRoot = cardObj;

            var texts = cardRoot.GetComponentsInChildren<TMPro.TMP_Text>(true);
            string fallbackName = null; // For reward cards without "Title" element

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

                    default:
                        // Fallback for reward cards: capture first meaningful text that looks like a card name
                        // Skip numeric indicators like "+99", "x4", etc.
                        if (fallbackName == null && content.Length > 2 &&
                            !Regex.IsMatch(content, @"^[\+\-]?\d+$") &&  // Skip "+99", "99", "-5"
                            !Regex.IsMatch(content, @"^x\d+$", RegexOptions.IgnoreCase) &&  // Skip "x4"
                            !Regex.IsMatch(content, @"^\d+/\d+$"))  // Skip "2/3" (P/T)
                        {
                            fallbackName = content;
                        }
                        break;
                }
            }

            // Use fallback name for reward cards if no Title was found
            if (string.IsNullOrEmpty(info.Name) && !string.IsNullOrEmpty(fallbackName))
            {
                info.Name = fallbackName;
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
        /// Builds a list of navigable info blocks for a card.
        /// Order varies by zone: battlefield puts mana cost after rules text.
        /// </summary>
        public static List<CardInfoBlock> GetInfoBlocks(GameObject cardObj, ZoneType zone = ZoneType.Hand)
        {
            var blocks = new List<CardInfoBlock>();
            var info = ExtractCardInfo(cardObj);

            if (!string.IsNullOrEmpty(info.Name))
                blocks.Add(new CardInfoBlock("Name", info.Name));

            // Battlefield: mana cost comes after rules (less important when card is in play)
            bool isBattlefield = zone == ZoneType.Battlefield;

            if (!isBattlefield && !string.IsNullOrEmpty(info.ManaCost))
                blocks.Add(new CardInfoBlock("Mana Cost", info.ManaCost));

            if (!string.IsNullOrEmpty(info.PowerToughness))
                blocks.Add(new CardInfoBlock("Power and Toughness", info.PowerToughness));

            if (!string.IsNullOrEmpty(info.TypeLine))
                blocks.Add(new CardInfoBlock("Type", info.TypeLine));

            if (!string.IsNullOrEmpty(info.RulesText))
                blocks.Add(new CardInfoBlock("Rules", info.RulesText));

            // Battlefield: mana cost after rules
            if (isBattlefield && !string.IsNullOrEmpty(info.ManaCost))
                blocks.Add(new CardInfoBlock("Mana Cost", info.ManaCost));

            if (!string.IsNullOrEmpty(info.FlavorText))
                blocks.Add(new CardInfoBlock("Flavor", info.FlavorText));

            if (!string.IsNullOrEmpty(info.Artist))
                blocks.Add(new CardInfoBlock("Artist", info.Artist));

            return blocks;
        }

        #endregion

        #region Card Categorization (Delegated to CardModelProvider)

        /// <summary>
        /// Gets card category info (creature, land, opponent) in a single Model lookup.
        /// Delegates to CardModelProvider.
        /// </summary>
        public static (bool isCreature, bool isLand, bool isOpponent) GetCardCategory(GameObject card)
            => CardModelProvider.GetCardCategory(card);

        /// <summary>
        /// Checks if a card is a creature. Delegates to CardModelProvider.
        /// </summary>
        public static bool IsCreatureCard(GameObject card)
            => CardModelProvider.IsCreatureCard(card);

        /// <summary>
        /// Checks if a card is a land. Delegates to CardModelProvider.
        /// </summary>
        public static bool IsLandCard(GameObject card)
            => CardModelProvider.IsLandCard(card);

        /// <summary>
        /// Checks if a card belongs to the opponent. Delegates to CardModelProvider.
        /// </summary>
        public static bool IsOpponentCard(GameObject card)
            => CardModelProvider.IsOpponentCard(card);

        #endregion

        #region Text Utilities

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

        #endregion
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
