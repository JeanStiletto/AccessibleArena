using UnityEngine;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AccessibleArena.Core.Services
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
                    typeName == "PagesMetaCardView" ||  // Used by deck builder collection cards
                    typeName == "MetaCardView" ||       // Generic card view component
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
        /// Checks if a GameObject has a HotHighlight child, indicating it's a valid target
        /// or can be played/activated. This is the unified method - all callers should use this
        /// instead of implementing their own check.
        ///
        /// Note: Checks for EXISTENCE of the HotHighlight child, not its active state.
        /// The game may create HotHighlight objects but set them inactive for visual optimization
        /// while the card is still logically playable/targetable (same pattern as IsAttacking/IsBlocking).
        /// </summary>
        /// <param name="obj">The GameObject to check (typically a card or player portrait)</param>
        /// <returns>True if a HotHighlight child exists (regardless of active state)</returns>
        public static bool HasHotHighlight(GameObject obj)
        {
            if (obj == null) return false;

            foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
            {
                // Skip null and the object itself
                if (child == null || child.gameObject == obj) continue;

                // HotHighlight: Check if child EXISTS (not just if active)
                // The indicator may be inactive but present, meaning the card IS playable/targetable
                // Same pattern as IsAttacking/IsBlocking detection
                if (child.name.Contains("HotHighlight"))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if a library card is displayed face-up (revealed by a game effect like
        /// Vizier of the Menagerie, Future Sight, Experimental Frenzy, Courser of Kruphix, etc.).
        /// Uses Model.IsDisplayedFaceDown property - cards with FaceDown=False are visible to players.
        /// This is different from HotHighlight: revealed cards are visible but may not be playable.
        /// </summary>
        private static System.Reflection.PropertyInfo _faceDownProp;
        private static Type _faceDownPropType;

        public static bool IsDisplayedFaceUp(GameObject obj)
        {
            if (obj == null) return false;

            var cdc = CardModelProvider.GetDuelSceneCDC(obj);
            if (cdc == null) return false;

            try
            {
                var model = CardModelProvider.GetCardModel(cdc);
                if (model == null) return false;

                var modelType = model.GetType();
                if (modelType != _faceDownPropType)
                {
                    _faceDownProp = modelType.GetProperty("IsDisplayedFaceDown");
                    _faceDownPropType = modelType;
                }

                if (_faceDownProp == null) return false;

                var val = _faceDownProp.GetValue(model);
                if (val is bool faceDown)
                    return !faceDown;

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// DIAGNOSTIC: Logs ALL GameObjects with HotHighlight across ALL zones.
        /// Use this to verify if game correctly updates highlights when mode changes.
        /// Call on Tab press to see what's highlighted in current game state.
        /// </summary>
        public static void LogAllHotHighlights()
        {
            MelonLogger.Msg("[HotHighlight DIAGNOSTIC] === SCANNING ALL ZONES FOR HOTHLIGHTS ===");

            var highlightedItems = new List<(string zone, string name, string highlightType, string path)>();

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                // Check if this object has HotHighlight
                string highlightType = GetHotHighlightType(go);
                if (highlightType == null) continue;

                // Determine zone from parent hierarchy
                string zone = DetectZone(go);
                string cardName = GetQuickCardName(go);
                string path = GetShortPath(go);

                highlightedItems.Add((zone, cardName, highlightType, path));
            }

            // Group by zone and log
            var byZone = highlightedItems.GroupBy(x => x.zone).OrderBy(g => g.Key);

            int totalCount = highlightedItems.Count;
            MelonLogger.Msg($"[HotHighlight DIAGNOSTIC] Found {totalCount} highlighted objects:");

            foreach (var group in byZone)
            {
                MelonLogger.Msg($"[HotHighlight DIAGNOSTIC]   {group.Key}: {group.Count()} items");
                foreach (var item in group)
                {
                    MelonLogger.Msg($"[HotHighlight DIAGNOSTIC]     - {item.name} ({item.highlightType}) @ {item.path}");
                }
            }

            // Also check for player targets specifically
            LogPlayerTargetHighlights();

            MelonLogger.Msg("[HotHighlight DIAGNOSTIC] === END SCAN ===");
        }

        /// <summary>
        /// Gets the specific HotHighlight type if present.
        /// Returns the name like "HotHighlightBattlefield(Clone)" or null if none.
        ///
        /// Note: Checks for EXISTENCE, not active state (same pattern as IsAttacking/IsBlocking).
        /// </summary>
        public static string GetHotHighlightType(GameObject obj)
        {
            foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || child.gameObject == obj) continue;

                // Check existence, not active state
                if (child.name.Contains("HotHighlight"))
                {
                    return child.name; // Return full name like "HotHighlightBattlefield(Clone)"
                }
            }
            return null;
        }

        /// <summary>
        /// DIAGNOSTIC: Checks HotHighlight status with detailed logging.
        /// Returns (hasActive, hasInactive, childName) to understand why detection might fail.
        /// </summary>
        public static (bool hasActive, bool hasInactive, string childName) GetHotHighlightDiagnostic(GameObject obj)
        {
            bool hasActive = false;
            bool hasInactive = false;
            string foundName = null;

            foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || child.gameObject == obj) continue;

                if (child.name.Contains("HotHighlight"))
                {
                    foundName = child.name;
                    if (child.gameObject.activeInHierarchy)
                    {
                        hasActive = true;
                    }
                    else
                    {
                        hasInactive = true;
                    }
                }
            }
            return (hasActive, hasInactive, foundName);
        }

        /// <summary>
        /// Detects zone from parent hierarchy for diagnostic logging.
        /// </summary>
        private static string DetectZone(GameObject obj)
        {
            Transform current = obj.transform;
            while (current != null)
            {
                string name = current.name;

                if (name.Contains("LocalHand")) return "Hand (Local)";
                if (name.Contains("OpponentHand")) return "Hand (Opponent)";
                if (name.Contains("LocalGraveyard")) return "Graveyard (Local)";
                if (name.Contains("OpponentGraveyard")) return "Graveyard (Opponent)";
                if (name.Contains("ExileCardHolder")) return "Exile";
                if (name.Contains("StackCardHolder")) return "Stack";
                if (name.Contains("BattlefieldCardHolder")) return "Battlefield";
                if (name.Contains("CommandCardHolder")) return "Command";
                if (name.Contains("MatchTimer"))
                {
                    return name.Contains("Opponent") ? "Player (Opponent)" : "Player (Local)";
                }

                current = current.parent;
            }
            return "Unknown";
        }

        /// <summary>
        /// Gets a quick card name without full model lookup.
        /// </summary>
        private static string GetQuickCardName(GameObject obj)
        {
            // Try CDC component first (fast)
            var cdc = GetDuelSceneCDC(obj);
            if (cdc != null)
            {
                var model = GetCardModel(cdc);
                if (model != null)
                {
                    var nameProp = model.GetType().GetProperty("Name") ??
                                   model.GetType().GetProperty("name");
                    if (nameProp != null)
                    {
                        var name = nameProp.GetValue(model)?.ToString();
                        if (!string.IsNullOrEmpty(name)) return name;
                    }
                }
            }

            // Fall back to object name
            string objName = obj.name;
            if (objName.Contains("CDC #")) return objName;
            if (objName.Contains("MatchTimer"))
            {
                return objName.Contains("Opponent") ? "Opponent Player" : "Local Player";
            }
            return objName;
        }

        /// <summary>
        /// Gets a short path for diagnostic logging.
        /// </summary>
        private static string GetShortPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            int depth = 0;
            while (parent != null && depth < 3)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
                depth++;
            }
            return path;
        }

        /// <summary>
        /// Specifically checks and logs player portrait highlights.
        /// </summary>
        private static void LogPlayerTargetHighlights()
        {
            MelonLogger.Msg("[HotHighlight DIAGNOSTIC] Checking player portrait areas specifically:");

            string[] playerPatterns = { "MatchTimer", "PlayerPortrait", "Avatar", "LifeFrame" };

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                bool isPlayerArea = false;
                foreach (var pattern in playerPatterns)
                {
                    if (go.name.Contains(pattern))
                    {
                        isPlayerArea = true;
                        break;
                    }
                }

                if (!isPlayerArea) continue;

                bool hasHighlight = HasHotHighlight(go);
                string highlightType = GetHotHighlightType(go) ?? "none";

                MelonLogger.Msg($"[HotHighlight DIAGNOSTIC]   Player area '{go.name}': hasHighlight={hasHighlight}, type={highlightType}");
            }
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
            // Clear CardPoolHolder reference (component may be destroyed on scene change)
            CardPoolAccessor.ClearCache();
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
        /// For deck list cards, uses GrpId lookup from ListMetaCardHolder.
        /// Falls back to UI text extraction for Meta scene cards or if Model fails.
        /// </summary>
        public static CardInfo ExtractCardInfo(GameObject cardObj)
        {
            if (cardObj == null) return new CardInfo();

            // Check if this is a deck list card (MainDeck_MetaCardHolder)
            var deckListInfo = CardModelProvider.ExtractDeckListCardInfo(cardObj);
            if (deckListInfo.HasValue && deckListInfo.Value.IsValid)
            {
                MelonLogger.Msg($"[CardDetector] Using DECK LIST extraction: {deckListInfo.Value.Name} (Qty: {deckListInfo.Value.Quantity})");
                return deckListInfo.Value;
            }

            // Check if this is a read-only deck card (StaticColumnMetaCardView)
            var readOnlyInfo = CardModelProvider.ExtractReadOnlyDeckCardInfo(cardObj);
            if (readOnlyInfo.HasValue && readOnlyInfo.Value.IsValid)
            {
                MelonLogger.Msg($"[CardDetector] Using READ-ONLY DECK extraction: {readOnlyInfo.Value.Name} (Qty: {readOnlyInfo.Value.Quantity})");
                return readOnlyInfo.Value;
            }

            // Try Model-based extraction first (works for compacted battlefield cards)
            var modelInfo = CardModelProvider.ExtractCardInfoFromModel(cardObj);
            if (modelInfo.HasValue && modelInfo.Value.IsValid)
            {
                var result = modelInfo.Value;
                // For collection cards, also extract owned/used quantities from PagesMetaCardView
                CardModelProvider.ExtractCollectionQuantity(cardObj, ref result);
                MelonLogger.Msg($"[CardDetector] Using MODEL extraction: {result.Name}" +
                    (result.OwnedCount > 0 ? $" (Owned: {result.OwnedCount}, InDeck: {result.UsedInDeckCount})" : ""));
                return result;
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
                        // Skip UI labels like "Faction", "NEW", "expand button", etc.
                        if (fallbackName == null && content.Length > 2 &&
                            !Regex.IsMatch(content, @"^[\+\-]?\d+$") &&  // Skip "+99", "99", "-5"
                            !Regex.IsMatch(content, @"^x\d+$", RegexOptions.IgnoreCase) &&  // Skip "x4"
                            !Regex.IsMatch(content, @"^\d+/\d+$") &&  // Skip "2/3" (P/T)
                            !IsUILabelText(content))  // Skip UI labels
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
        /// Checks if text is a UI label that should not be used as a card name.
        /// These are common UI labels found in card views that are not card names.
        /// </summary>
        private static bool IsUILabelText(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;

            // Common UI labels to skip (case-insensitive comparison)
            string lowerText = text.ToLowerInvariant();

            // Faction/set labels
            if (lowerText == "faction" || lowerText == "new" || lowerText == "neu")
                return true;

            // Button labels
            if (lowerText.Contains("expand") || lowerText.Contains("button") ||
                lowerText.Contains("toggle") || lowerText.Contains("close"))
                return true;

            // Navigation labels
            if (lowerText == "back" || lowerText == "next" || lowerText == "done" ||
                lowerText == "fertig" || lowerText == "weiter" || lowerText == "zurück")
                return true;

            // Filter labels
            if (lowerText == "filter" || lowerText == "search" || lowerText == "suche")
                return true;

            return false;
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
        /// For deck list cards, Quantity is shown right after the name.
        /// </summary>
        public static List<CardInfoBlock> GetInfoBlocks(GameObject cardObj, ZoneType zone = ZoneType.Hand)
        {
            var blocks = new List<CardInfoBlock>();
            var info = ExtractCardInfo(cardObj);

            if (!string.IsNullOrEmpty(info.Name))
                blocks.Add(new CardInfoBlock(Models.Strings.CardInfoName, info.Name));

            // For deck list cards, show quantity right after name (with "missing" if unowned)
            if (info.Quantity > 0)
            {
                string qtyText = info.IsUnowned
                    ? Models.Strings.CardQuantityMissing(info.Quantity)
                    : info.Quantity.ToString();
                blocks.Add(new CardInfoBlock(Models.Strings.CardInfoQuantity, qtyText, false));
            }

            // For collection cards, show owned and in-deck counts as one block
            if (info.OwnedCount > 0)
            {
                string collectionText = info.UsedInDeckCount > 0
                    ? Models.Strings.CardOwnedInDeck(info.OwnedCount, info.UsedInDeckCount)
                    : Models.Strings.CardOwned(info.OwnedCount);
                blocks.Add(new CardInfoBlock(Models.Strings.CardInfoCollection, collectionText, false));
            }

            // Block order varies by zone context
            bool isBattlefield = zone == ZoneType.Battlefield;
            bool isBrowser = zone == ZoneType.Browser;

            // Browser (selection): rules first - these are options, not cards to identify by name
            if (isBrowser)
            {
                if (!string.IsNullOrEmpty(info.RulesText))
                    blocks.Add(new CardInfoBlock(Models.Strings.CardInfoRules, info.RulesText));
                if (!string.IsNullOrEmpty(info.ManaCost))
                    blocks.Add(new CardInfoBlock(Models.Strings.CardInfoManaCost, info.ManaCost));
                if (!string.IsNullOrEmpty(info.PowerToughness))
                    blocks.Add(new CardInfoBlock(Models.Strings.CardInfoPowerToughness, info.PowerToughness));
                if (!string.IsNullOrEmpty(info.TypeLine))
                    blocks.Add(new CardInfoBlock(Models.Strings.CardInfoType, info.TypeLine));
            }
            else
            {
                if (!isBattlefield && !string.IsNullOrEmpty(info.ManaCost))
                    blocks.Add(new CardInfoBlock(Models.Strings.CardInfoManaCost, info.ManaCost));
                if (!string.IsNullOrEmpty(info.PowerToughness))
                    blocks.Add(new CardInfoBlock(Models.Strings.CardInfoPowerToughness, info.PowerToughness));
                if (!string.IsNullOrEmpty(info.TypeLine))
                    blocks.Add(new CardInfoBlock(Models.Strings.CardInfoType, info.TypeLine));
                if (!string.IsNullOrEmpty(info.RulesText))
                    blocks.Add(new CardInfoBlock(Models.Strings.CardInfoRules, info.RulesText));
                // Battlefield: mana cost after rules
                if (isBattlefield && !string.IsNullOrEmpty(info.ManaCost))
                    blocks.Add(new CardInfoBlock(Models.Strings.CardInfoManaCost, info.ManaCost));
            }

            if (!string.IsNullOrEmpty(info.FlavorText))
                blocks.Add(new CardInfoBlock(Models.Strings.CardInfoFlavor, info.FlavorText));

            if (!string.IsNullOrEmpty(info.Rarity))
                blocks.Add(new CardInfoBlock(Models.Strings.CardInfoRarity, info.Rarity));

            if (!string.IsNullOrEmpty(info.Artist))
                blocks.Add(new CardInfoBlock(Models.Strings.CardInfoArtist, info.Artist));

            return blocks;
        }

        /// <summary>
        /// Builds info blocks from a pre-populated CardInfo struct (no GameObject needed).
        /// Uses the same block order as Hand zone.
        /// </summary>
        public static List<CardInfoBlock> BuildInfoBlocks(CardInfo info)
        {
            var blocks = new List<CardInfoBlock>();

            if (!string.IsNullOrEmpty(info.Name))
                blocks.Add(new CardInfoBlock(Models.Strings.CardInfoName, info.Name));

            if (info.Quantity > 0)
                blocks.Add(new CardInfoBlock(Models.Strings.CardInfoQuantity, info.Quantity.ToString(), false));

            if (!string.IsNullOrEmpty(info.ManaCost))
                blocks.Add(new CardInfoBlock(Models.Strings.CardInfoManaCost, info.ManaCost));
            if (!string.IsNullOrEmpty(info.PowerToughness))
                blocks.Add(new CardInfoBlock(Models.Strings.CardInfoPowerToughness, info.PowerToughness));
            if (!string.IsNullOrEmpty(info.TypeLine))
                blocks.Add(new CardInfoBlock(Models.Strings.CardInfoType, info.TypeLine));
            if (!string.IsNullOrEmpty(info.RulesText))
                blocks.Add(new CardInfoBlock(Models.Strings.CardInfoRules, info.RulesText));
            if (!string.IsNullOrEmpty(info.FlavorText))
                blocks.Add(new CardInfoBlock(Models.Strings.CardInfoFlavor, info.FlavorText));
            if (!string.IsNullOrEmpty(info.Rarity))
                blocks.Add(new CardInfoBlock(Models.Strings.CardInfoRarity, info.Rarity));
            if (!string.IsNullOrEmpty(info.Artist))
                blocks.Add(new CardInfoBlock(Models.Strings.CardInfoArtist, info.Artist));

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
        public string Rarity;
        public string Artist;
        /// <summary>
        /// Quantity of this card in a deck list. 0 means not applicable (not a deck list card).
        /// </summary>
        public int Quantity;
        /// <summary>
        /// Owned count for collection cards. 0 means not applicable.
        /// </summary>
        public int OwnedCount;
        /// <summary>
        /// Number of copies already used in the current deck (collection cards). 0 means not applicable.
        /// </summary>
        public int UsedInDeckCount;
        /// <summary>
        /// Whether this deck list entry represents unowned (missing) copies.
        /// </summary>
        public bool IsUnowned;
    }

    /// <summary>
    /// A single navigable block of card information.
    /// </summary>
    public class CardInfoBlock
    {
        public string Label { get; }
        public string Content { get; }
        public bool IsVerbose { get; }

        public CardInfoBlock(string label, string content, bool isVerbose = true)
        {
            Label = label;
            Content = content;
            IsVerbose = isVerbose;
        }
    }
}
