using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using AccessibleArena.Core.Models;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Provides reflection-based access to deck statistics from the game's UI components.
    /// Reads DeckMainTitlePanel (card count) and DeckCostsDetails (mana curve, average cost,
    /// type breakdown). All data is read from live game UI text, not computed by the mod.
    ///
    /// DeckCostsDetails has nested CostBarItem (with QuantityLabel TMP_Text for each CMC bucket)
    /// and TypeLineItem (with Quantity/Percent TMP_Text for Creatures/Others/Lands).
    /// The game's SetDeck() method writes to these text fields even when the popup is closed.
    /// </summary>
    public static class DeckInfoProvider
    {

        // Cached component references (cleared on scene change)
        private static MonoBehaviour _cachedTitlePanel;        // DeckMainTitlePanel
        private static MonoBehaviour _cachedCostsDetails;      // DeckCostsDetails
        private static MonoBehaviour _cachedTypesDetails;      // DeckTypesDetails

        private sealed class TitlePanelHandles
        {
            public FieldInfo CardCountLabel;   // _cardCountLabel (Localize)
        }

        private sealed class CostsDetailsHandles
        {
            public FieldInfo[] CostBars;       // OneOrLessItem..SixOrGreaterItem (CostBarItem)
            public FieldInfo CostBarQuantity;  // CostBarItem.QuantityLabel (TMP_Text)
            public FieldInfo AverageText;      // AverageText (TMP_Text)
            public FieldInfo CreaturesItem;    // CreaturesItem (TypeLineItem)
            public FieldInfo OthersItem;       // OthersItem (TypeLineItem)
            public FieldInfo LandsItem;        // LandsItem (TypeLineItem)
            public FieldInfo TypeLineQuantity; // TypeLineItem.Quantity (TMP_Text)
            public FieldInfo TypeLinePercent;  // TypeLineItem.Percent (TMP_Text)
            public MethodInfo SetDeck;         // DeckCostsDetails.SetDeck(...)
        }

        private sealed class PantryHandles
        {
            public MethodInfo GetModelProvider;    // Pantry.Get<DeckBuilderModelProvider>()
            public PropertyInfo Model;             // DeckBuilderModelProvider.Model
            public MethodInfo GetFilteredMainDeck; // DeckBuilderModel.GetFilteredMainDeck()
        }

        private sealed class TypesDetailsHandles
        {
            public FieldInfo ItemParent;      // ItemParent (Transform)
            public MethodInfo SetDeck;        // SetDeck(deck, locProvider)
            public Type LineItemType;         // DeckDetailsLineItem
            public FieldInfo LineItemName;    // DeckDetailsLineItem.Name (TMP_Text)
            public FieldInfo LineItemQuantity;// DeckDetailsLineItem.Quantity (TMP_Text)
        }

        private sealed class CardDatabaseHandles
        {
            public MethodInfo GetCardDatabase;  // Pantry.Get<CardDatabase>()
            public PropertyInfo GreLocProvider; // CardDatabase.GreLocProvider
        }

        // Cost bar / type-line field name tables used by both the cache builder and read sites.
        private static readonly string[] CostBarFieldNames = new[]
        {
            "OneOrLessItem", "TwoItem", "ThreeItem", "FourItem", "FiveItem", "SixOrGreaterItem"
        };

        private static readonly ReflectionCache<TitlePanelHandles> _titlePanelCache = new ReflectionCache<TitlePanelHandles>(
            builder: t => new TitlePanelHandles
            {
                CardCountLabel = t.GetField("_cardCountLabel", PrivateInstance),
            },
            validator: h => h.CardCountLabel != null,
            logTag: "DeckInfoProvider",
            logSubject: "DeckMainTitlePanel");

        private static readonly ReflectionCache<CostsDetailsHandles> _costsDetailsCache = new ReflectionCache<CostsDetailsHandles>(
            builder: t =>
            {
                var h = new CostsDetailsHandles { CostBars = new FieldInfo[CostBarFieldNames.Length] };

                Type costBarItemType = null;
                for (int i = 0; i < CostBarFieldNames.Length; i++)
                {
                    h.CostBars[i] = t.GetField(CostBarFieldNames[i], PublicInstance)
                                 ?? t.GetField(CostBarFieldNames[i], PrivateInstance);
                    if (costBarItemType == null && h.CostBars[i] != null)
                        costBarItemType = h.CostBars[i].FieldType;
                }
                if (costBarItemType != null)
                {
                    h.CostBarQuantity = costBarItemType.GetField("QuantityLabel", PublicInstance)
                                     ?? costBarItemType.GetField("QuantityLabel", PrivateInstance);
                }

                h.AverageText = t.GetField("AverageText", PublicInstance)
                             ?? t.GetField("AverageText", PrivateInstance);

                h.CreaturesItem = t.GetField("CreaturesItem", PublicInstance) ?? t.GetField("CreaturesItem", PrivateInstance);
                h.OthersItem = t.GetField("OthersItem", PublicInstance) ?? t.GetField("OthersItem", PrivateInstance);
                h.LandsItem = t.GetField("LandsItem", PublicInstance) ?? t.GetField("LandsItem", PrivateInstance);

                Type typeLineItemType = h.CreaturesItem?.FieldType ?? h.OthersItem?.FieldType;
                if (typeLineItemType != null)
                {
                    h.TypeLineQuantity = typeLineItemType.GetField("Quantity", PublicInstance)
                                      ?? typeLineItemType.GetField("Quantity", PrivateInstance);
                    h.TypeLinePercent = typeLineItemType.GetField("Percent", PublicInstance)
                                     ?? typeLineItemType.GetField("Percent", PrivateInstance);
                }

                h.SetDeck = t.GetMethod("SetDeck", PublicInstance);
                return h;
            },
            validator: h =>
                h.CostBarQuantity != null && h.TypeLineQuantity != null
                && h.CreaturesItem != null && h.OthersItem != null && h.LandsItem != null
                && h.SetDeck != null,
            logTag: "DeckInfoProvider",
            logSubject: "DeckCostsDetails");

        private static readonly ReflectionCache<PantryHandles> _pantryCache = new ReflectionCache<PantryHandles>(
            builder: pantryType =>
            {
                var h = new PantryHandles();
                Type modelProviderType = FindType("Core.Code.Decks.DeckBuilderModelProvider");
                if (modelProviderType == null) return h;

                var getMethod = pantryType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                if (getMethod != null && getMethod.IsGenericMethod)
                    h.GetModelProvider = getMethod.MakeGenericMethod(modelProviderType);

                h.Model = modelProviderType.GetProperty("Model", PublicInstance)
                       ?? modelProviderType.GetProperty("Model", PrivateInstance);

                if (h.Model != null)
                {
                    var modelType = h.Model.PropertyType;
                    h.GetFilteredMainDeck = modelType.GetMethod("GetFilteredMainDeck", PublicInstance)
                                         ?? modelType.GetMethod("GetFilteredMainDeck", PrivateInstance);
                }
                return h;
            },
            validator: h => h.GetModelProvider != null && h.Model != null && h.GetFilteredMainDeck != null,
            logTag: "DeckInfoProvider",
            logSubject: "Pantry");

        private static readonly ReflectionCache<TypesDetailsHandles> _typesDetailsCache = new ReflectionCache<TypesDetailsHandles>(
            builder: t =>
            {
                var h = new TypesDetailsHandles
                {
                    ItemParent = t.GetField("ItemParent", PublicInstance),
                };
                foreach (var method in t.GetMethods(PublicInstance))
                {
                    if (method.Name == "SetDeck" && method.GetParameters().Length == 2)
                    {
                        h.SetDeck = method;
                        break;
                    }
                }
                var typePrefabField = t.GetField("TypePrefab", PublicInstance);
                if (typePrefabField != null)
                {
                    h.LineItemType = typePrefabField.FieldType;
                    h.LineItemName = h.LineItemType.GetField("Name", PublicInstance);
                    h.LineItemQuantity = h.LineItemType.GetField("Quantity", PublicInstance);
                }
                return h;
            },
            validator: h =>
                h.ItemParent != null && h.SetDeck != null && h.LineItemType != null
                && h.LineItemName != null && h.LineItemQuantity != null,
            logTag: "DeckInfoProvider",
            logSubject: "DeckTypesDetails");

        private static readonly ReflectionCache<CardDatabaseHandles> _cardDatabaseCache = new ReflectionCache<CardDatabaseHandles>(
            builder: pantryType =>
            {
                var h = new CardDatabaseHandles();
                Type cardDatabaseType = FindType("Wotc.Mtga.Cards.Database.CardDatabase");
                if (cardDatabaseType == null) return h;

                var getMethod = pantryType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                if (getMethod != null && getMethod.IsGenericMethod)
                    h.GetCardDatabase = getMethod.MakeGenericMethod(cardDatabaseType);

                h.GreLocProvider = cardDatabaseType.GetProperty("GreLocProvider", PublicInstance);
                return h;
            },
            validator: h => h.GetCardDatabase != null && h.GreLocProvider != null,
            logTag: "DeckInfoProvider",
            logSubject: "CardDatabase");

        private static readonly string[] CostBarLabels = new[]
        {
            "1 or less", "2", "3", "4", "5", "6 or more"
        };

        private static readonly string[] TypeLineDisplayNames = new[]
        {
            "Creatures", "Others", "Lands"
        };

        /// <summary>
        /// Represents a type group read from DeckTypesDetails (e.g., Kreatur with subtypes).
        /// </summary>
        private struct TypeGroup
        {
            public string TypeName;
            public string TypeQuantity;
            public List<(string name, string quantity)> Subtypes;
        }

        /// <summary>
        /// Get the card count text (e.g., "35 von 60") from DeckMainTitlePanel.
        /// Reads the rendered TMP_Text from the _cardCountLabel's GameObject.
        /// </summary>
        public static string GetCardCountText()
        {
            var panel = FindTitlePanel();
            if (panel == null) return null;

            try
            {
                if (!_titlePanelCache.IsInitialized) return null;

                // _cardCountLabel is a Localize component (MonoBehaviour)
                var localizeComponent = _titlePanelCache.Handles.CardCountLabel.GetValue(panel);
                if (localizeComponent == null) return null;

                // The Localize component is on a GameObject that also has TMP_Text
                var componentObj = localizeComponent as Component;
                if (componentObj == null) return null;

                // Find TMP_Text on the same GameObject
                var tmpText = FindTmpTextOnObject(componentObj.gameObject);
                if (tmpText != null)
                {
                    string text = GetTmpTextValue(tmpText);
                    if (!string.IsNullOrEmpty(text))
                        return text.Trim();
                }
            }
            catch (Exception ex)
            {
                Log.Error("DeckInfoProvider", $"Error reading card count: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Build all info elements for the Deck Info group.
        /// Two navigable items (Left/Right):
        /// 1. Card info: card count + type breakdown (non-zero types only)
        /// 2. Mana curve: all CMC buckets (always shown) + average
        /// Periods between values for screen reader pauses.
        /// </summary>
        public static List<(string label, string text)> GetDeckInfoElements()
        {
            var elements = new List<(string label, string text)>();

            var details = FindCostsDetails();
            if (details != null)
            {
                PopulateCostsDetails(details);
            }

            // Item 1: Card count + types
            string cardInfo = BuildCardInfoText(details);
            if (!string.IsNullOrEmpty(cardInfo))
                elements.Add(("Cards", cardInfo));

            // Item 2: Mana curve + average
            string manaCurve = BuildManaCurveText(details);
            if (!string.IsNullOrEmpty(manaCurve))
                elements.Add(("Mana Curve", manaCurve));

            return elements;
        }

        /// <summary>
        /// Get deck info as rows with individual sub-entries for 2D navigation.
        /// Each row has a label and a list of individual entries navigable with Left/Right.
        /// Row 1 (Cards): card count, then type entries (Creatures, Others, Lands)
        /// Row 2 (Mana Curve): CMC buckets (1 or less through 6+) and average
        /// </summary>
        public static List<(string label, List<string> entries)> GetDeckInfoRows()
        {
            var rows = new List<(string label, List<string> entries)>();

            var details = FindCostsDetails();
            if (details != null)
            {
                PopulateCostsDetails(details);
            }

            // Read type groups for enriched card info entries
            List<TypeGroup> typeGroups = null;
            var typesDetails = FindTypesDetails();
            if (typesDetails != null)
            {
                PopulateTypesDetails(typesDetails);
                typeGroups = ReadTypeGroups(typesDetails);
            }

            var cardEntries = BuildCardInfoEntries(details, typeGroups);
            if (cardEntries.Count > 0)
                rows.Add(("Cards", cardEntries));

            var manaEntries = BuildManaCurveEntries(details);
            if (manaEntries.Count > 0)
                rows.Add(("Mana Curve", manaEntries));

            return rows;
        }

        /// <summary>
        /// Build individual card info entries with integrated type details.
        /// Creatures entry includes creature subtypes, Others entry includes individual type
        /// distribution (Instants, Sorceries, etc.), Lands entry includes land subtypes.
        /// Game always outputs types in order: Creature, [Instant,Sorcery,Artifact,Enchantment,
        /// Planeswalker,Battle], Land - so first group = Creature, last = Land, middle = Others.
        /// </summary>
        private static List<string> BuildCardInfoEntries(MonoBehaviour details, List<TypeGroup> typeGroups = null)
        {
            var entries = new List<string>();

            string cardCount = GetCardCountText();
            if (!string.IsNullOrEmpty(cardCount))
                entries.Add(cardCount);

            if (details == null || !_costsDetailsCache.IsInitialized)
                return entries;

            var cdh = _costsDetailsCache.Handles;

            // Classify type groups into creature/others/land
            TypeGroup? creatureTypeGroup = null;
            var othersTypeGroups = new List<TypeGroup>();
            TypeGroup? landTypeGroup = null;

            if (typeGroups != null && typeGroups.Count > 0)
                ClassifyTypeGroups(details, typeGroups, out creatureTypeGroup, out othersTypeGroups, out landTypeGroup);

            var typeFields = new FieldInfo[] { cdh.CreaturesItem, cdh.OthersItem, cdh.LandsItem };
            for (int i = 0; i < typeFields.Length; i++)
            {
                if (typeFields[i] == null) continue;
                try
                {
                    var typeLineItem = typeFields[i].GetValue(details);
                    if (typeLineItem == null) continue;

                    string quantity = GetTmpTextValue(cdh.TypeLineQuantity.GetValue(typeLineItem));
                    if (string.IsNullOrEmpty(quantity) || quantity.Trim() == "0") continue;

                    var sb = new StringBuilder();
                    sb.Append($"{quantity.Trim()} {TypeLineDisplayNames[i]}");

                    // Enrich with type details
                    if (i == 0 && creatureTypeGroup.HasValue && creatureTypeGroup.Value.Subtypes.Count > 0)
                    {
                        // Creatures: append subtypes with quantities
                        foreach (var sub in creatureTypeGroup.Value.Subtypes)
                            sb.Append($", {sub.quantity} {sub.name}");
                    }
                    else if (i == 1 && othersTypeGroups.Count > 0)
                    {
                        // Others: append individual type distribution
                        foreach (var group in othersTypeGroups)
                            sb.Append($", {group.TypeQuantity} {group.TypeName}");
                    }
                    else if (i == 2 && landTypeGroup.HasValue && landTypeGroup.Value.Subtypes.Count > 0)
                    {
                        // Lands: append land subtypes with quantities
                        foreach (var sub in landTypeGroup.Value.Subtypes)
                            sb.Append($", {sub.quantity} {sub.name}");
                    }

                    if (cdh.TypeLinePercent != null)
                    {
                        string percent = GetTmpTextValue(cdh.TypeLinePercent.GetValue(typeLineItem));
                        if (!string.IsNullOrEmpty(percent))
                            sb.Append($", {percent.Trim()}");
                    }

                    entries.Add(sb.ToString());
                }
                catch { /* Type line field reflection may fail on different UI versions */ }
            }

            return entries;
        }

        /// <summary>
        /// Classify type groups into Creature, Others (individual types), and Land.
        /// Uses display order: Creature is always first, Land is always last,
        /// everything in between is individual "Others" types.
        /// </summary>
        private static void ClassifyTypeGroups(MonoBehaviour costsDetails, List<TypeGroup> typeGroups,
            out TypeGroup? creatureGroup, out List<TypeGroup> othersGroups, out TypeGroup? landGroup)
        {
            creatureGroup = null;
            othersGroups = new List<TypeGroup>();
            landGroup = null;

            if (typeGroups.Count == 0) return;

            if (!_costsDetailsCache.IsInitialized) return;
            var cdh = _costsDetailsCache.Handles;

            int creatureCount = ReadTypeLineCount(costsDetails, cdh.CreaturesItem);
            int landCount = ReadTypeLineCount(costsDetails, cdh.LandsItem);

            int startIdx = 0;
            int endIdx = typeGroups.Count - 1;

            if (creatureCount > 0)
            {
                creatureGroup = typeGroups[0];
                startIdx = 1;
            }
            if (landCount > 0 && endIdx >= startIdx)
            {
                landGroup = typeGroups[endIdx];
                endIdx--;
            }
            for (int i = startIdx; i <= endIdx; i++)
                othersGroups.Add(typeGroups[i]);
        }

        /// <summary>
        /// Read the quantity from a TypeLineItem as an integer.
        /// </summary>
        private static int ReadTypeLineCount(MonoBehaviour costsDetails, FieldInfo typeLineField)
        {
            if (costsDetails == null || typeLineField == null || !_costsDetailsCache.IsInitialized)
                return 0;
            try
            {
                var typeLineItem = typeLineField.GetValue(costsDetails);
                if (typeLineItem == null) return 0;
                string qty = GetTmpTextValue(_costsDetailsCache.Handles.TypeLineQuantity.GetValue(typeLineItem));
                if (string.IsNullOrEmpty(qty)) return 0;
                return int.TryParse(qty.Trim(), out int val) ? val : 0;
            }
            catch { return 0; }
        }

        /// <summary>
        /// Build individual mana curve entries: ["1 or less: 4", "2: 8", ..., "Average: 3.5"]
        /// </summary>
        private static List<string> BuildManaCurveEntries(MonoBehaviour details)
        {
            var entries = new List<string>();

            if (details == null || !_costsDetailsCache.IsInitialized)
                return entries;

            var cdh = _costsDetailsCache.Handles;

            try
            {
                for (int i = 0; i < cdh.CostBars.Length; i++)
                {
                    if (cdh.CostBars[i] == null) continue;

                    var barItem = cdh.CostBars[i].GetValue(details);
                    if (barItem == null) continue;

                    string text = GetTmpTextValue(cdh.CostBarQuantity.GetValue(barItem));
                    if (string.IsNullOrEmpty(text)) text = "0";

                    entries.Add($"{CostBarLabels[i]}: {text.Trim()}");
                }

                string avg = GetAverageCostText(details);
                if (!string.IsNullOrEmpty(avg))
                    entries.Add($"Average: {avg}");
            }
            catch (Exception ex)
            {
                Log.Error("DeckInfoProvider", $"Error building mana curve entries: {ex.Message}");
            }

            return entries;
        }

        /// <summary>
        /// Build card info text: "35 von 60. 20 Creatures (56%). 12 Others (33%). 24 Lands (11%)"
        /// Types only included if non-zero.
        /// </summary>
        private static string BuildCardInfoText(MonoBehaviour details)
        {
            var sb = new StringBuilder();

            string cardCount = GetCardCountText();
            if (!string.IsNullOrEmpty(cardCount))
                sb.Append(cardCount);

            if (details != null && _costsDetailsCache.IsInitialized)
            {
                var cdh = _costsDetailsCache.Handles;
                var typeFields = new FieldInfo[] { cdh.CreaturesItem, cdh.OthersItem, cdh.LandsItem };
                for (int i = 0; i < typeFields.Length; i++)
                {
                    if (typeFields[i] == null) continue;
                    try
                    {
                        var typeLineItem = typeFields[i].GetValue(details);
                        if (typeLineItem == null) continue;

                        string quantity = GetTmpTextValue(cdh.TypeLineQuantity.GetValue(typeLineItem));
                        if (string.IsNullOrEmpty(quantity) || quantity.Trim() == "0") continue;

                        if (sb.Length > 0) sb.Append(". ");
                        sb.Append($"{quantity.Trim()} {TypeLineDisplayNames[i]}");

                        if (cdh.TypeLinePercent != null)
                        {
                            string percent = GetTmpTextValue(cdh.TypeLinePercent.GetValue(typeLineItem));
                            if (!string.IsNullOrEmpty(percent))
                                sb.Append($" ({percent.Trim()})");
                        }
                    }
                    catch { /* Type line field reflection may fail on different UI versions */ }
                }
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }

        /// <summary>
        /// Build mana curve text: "1 or less: 4. 2: 8. 3: 12. 4: 6. 5: 3. 6 or more: 2. Average: 3.5"
        /// All CMC buckets always shown, even if zero.
        /// </summary>
        private static string BuildManaCurveText(MonoBehaviour details)
        {
            if (details == null || !_costsDetailsCache.IsInitialized)
                return null;

            var cdh = _costsDetailsCache.Handles;

            try
            {
                var sb = new StringBuilder();
                bool first = true;

                for (int i = 0; i < cdh.CostBars.Length; i++)
                {
                    if (cdh.CostBars[i] == null) continue;

                    var barItem = cdh.CostBars[i].GetValue(details);
                    if (barItem == null) continue;

                    string text = GetTmpTextValue(cdh.CostBarQuantity.GetValue(barItem));
                    if (string.IsNullOrEmpty(text)) text = "0";

                    if (!first) sb.Append(". ");
                    sb.Append($"{CostBarLabels[i]}: {text.Trim()}");
                    first = false;
                }

                string avg = GetAverageCostText(details);
                if (!string.IsNullOrEmpty(avg))
                {
                    if (sb.Length > 0) sb.Append(". ");
                    sb.Append($"Average: {avg}");
                }

                return sb.Length > 0 ? sb.ToString() : null;
            }
            catch (Exception ex)
            {
                Log.Error("DeckInfoProvider", $"Error building mana curve: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get average mana cost text from DeckCostsDetails.
        /// </summary>
        private static string GetAverageCostText(MonoBehaviour details)
        {
            try
            {
                if (!_costsDetailsCache.IsInitialized || _costsDetailsCache.Handles.AverageText == null) return null;

                var avgText = _costsDetailsCache.Handles.AverageText.GetValue(details);
                string avg = GetTmpTextValue(avgText);
                if (!string.IsNullOrEmpty(avg) && avg.Trim() != "0" && avg.Trim() != "0.0")
                    return avg.Trim();
            }
            catch (Exception ex)
            {
                Log.Error("DeckInfoProvider", $"Error reading average cost: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Clear cached component references. Call on scene change.
        /// Reflection member caches are preserved (types don't change).
        /// </summary>
        public static void ClearCache()
        {
            _cachedTitlePanel = null;
            _cachedCostsDetails = null;
            _cachedTypesDetails = null;
        }

        #region Component Discovery

        private static MonoBehaviour FindTitlePanel()
        {
            if (IsValidCached(_cachedTitlePanel))
                return _cachedTitlePanel;
            _cachedTitlePanel = null;

            var titlePanelGo = GameObject.Find("TitlePanel_MainDeck");
            if (titlePanelGo == null) return null;

            foreach (var mb in titlePanelGo.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (mb.GetType().Name == T.DeckMainTitlePanel)
                {
                    _cachedTitlePanel = mb;
                    _titlePanelCache.EnsureInitialized(mb.GetType());
                    return _cachedTitlePanel;
                }
            }

            return null;
        }

        private static MonoBehaviour FindCostsDetails()
        {
            if (IsValidCachedAllowInactive(_cachedCostsDetails))
                return _cachedCostsDetails;
            _cachedCostsDetails = null;

            // DeckCostsDetails is inside DeckDetailsPopup - search with includeInactive=true
            // because the popup may be closed but the game still writes text to it
            var wrapper = GameObject.Find("WrapperDeckBuilder_Desktop_16x9(Clone)");
            if (wrapper == null)
            {
                wrapper = GameObject.Find("DeckListView_Desktop_16x9(Clone)");
            }
            if (wrapper == null) return null;

            foreach (var mb in wrapper.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                if (mb.GetType().Name == T.DeckCostsDetails)
                {
                    _cachedCostsDetails = mb;
                    _costsDetailsCache.EnsureInitialized(mb.GetType());
                    return _cachedCostsDetails;
                }
            }

            return null;
        }

        /// <summary>
        /// Populates DeckCostsDetails with current deck data by calling its SetDeck method.
        /// Uses Pantry.Get&lt;DeckBuilderModelProvider&gt;().Model.GetFilteredMainDeck() to get
        /// the current deck, then passes it to DeckCostsDetails.SetDeck().
        /// This ensures text fields contain real data even when the popup hasn't been opened.
        /// </summary>
        private static void PopulateCostsDetails(MonoBehaviour costsDetails)
        {
            try
            {
                EnsurePantryInitialized();

                if (!_pantryCache.IsInitialized) return;

                var ph = _pantryCache.Handles;

                // Pantry.Get<DeckBuilderModelProvider>()
                var modelProvider = ph.GetModelProvider.Invoke(null, null);
                if (modelProvider == null) return;

                // .Model
                var model = ph.Model.GetValue(modelProvider);
                if (model == null) return;

                // .GetFilteredMainDeck()
                var deckData = ph.GetFilteredMainDeck.Invoke(model, null);
                if (deckData == null) return;

                // DeckCostsDetails.SetDeck(deckData)
                if (_costsDetailsCache.IsInitialized && _costsDetailsCache.Handles.SetDeck != null)
                {
                    _costsDetailsCache.Handles.SetDeck.Invoke(costsDetails, new object[] { deckData });
                }
            }
            catch (Exception ex)
            {
                Log.Error("DeckInfoProvider", $"Error populating CostsDetails: {ex.Message}");
            }
        }

        private static MonoBehaviour FindTypesDetails()
        {
            if (IsValidCachedAllowInactive(_cachedTypesDetails))
                return _cachedTypesDetails;
            _cachedTypesDetails = null;

            // DeckTypesDetails is inside the same wrapper as DeckCostsDetails
            var wrapper = GameObject.Find("WrapperDeckBuilder_Desktop_16x9(Clone)");
            if (wrapper == null)
            {
                wrapper = GameObject.Find("DeckListView_Desktop_16x9(Clone)");
            }
            if (wrapper == null) return null;

            foreach (var mb in wrapper.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                if (mb.GetType().Name == T.DeckTypesDetails)
                {
                    _cachedTypesDetails = mb;
                    _typesDetailsCache.EnsureInitialized(mb.GetType());
                    return _cachedTypesDetails;
                }
            }

            return null;
        }

        /// <summary>
        /// Populate DeckTypesDetails by calling SetDeck(deck, locMan).
        /// Only calls SetDeck if ItemParent has no children (game hasn't populated yet).
        /// Calling SetDeck when items already exist causes duplicates because Unity
        /// defers Destroy() to end of frame, so old + new items coexist during the same frame.
        /// </summary>
        private static void PopulateTypesDetails(MonoBehaviour typesDetails)
        {
            try
            {
                if (!_typesDetailsCache.IsInitialized) return;
                var tdh = _typesDetailsCache.Handles;
                if (tdh.SetDeck == null) return;

                // Skip if already populated - the game's SetDeck uses Destroy() which is deferred,
                // so calling it again would create duplicates readable in the same frame
                if (tdh.ItemParent != null)
                {
                    var itemParent = tdh.ItemParent.GetValue(typesDetails) as Transform;
                    if (itemParent != null && itemParent.childCount > 0)
                        return;
                }

                // Get deck data (reuse existing Pantry reflection)
                EnsurePantryInitialized();

                if (!_pantryCache.IsInitialized) return;

                var ph = _pantryCache.Handles;

                var modelProvider = ph.GetModelProvider.Invoke(null, null);
                if (modelProvider == null) return;

                var model = ph.Model.GetValue(modelProvider);
                if (model == null) return;

                var deckData = ph.GetFilteredMainDeck.Invoke(model, null);
                if (deckData == null) return;

                // Get IGreLocProvider
                var locProvider = GetGreLocProvider();
                if (locProvider == null) return;

                // DeckTypesDetails.SetDeck(deck, locMan)
                tdh.SetDeck.Invoke(typesDetails, new object[] { deckData, locProvider });
            }
            catch (Exception ex)
            {
                Log.Error("DeckInfoProvider", $"Error populating TypesDetails: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the IGreLocProvider via Pantry.Get&lt;CardDatabase&gt;().GreLocProvider.
        /// </summary>
        private static object GetGreLocProvider()
        {
            try
            {
                EnsureCardDatabaseInitialized();

                if (!_cardDatabaseCache.IsInitialized)
                    return null;

                var cdh = _cardDatabaseCache.Handles;

                var cardDatabase = cdh.GetCardDatabase.Invoke(null, null);
                if (cardDatabase == null) return null;

                return cdh.GreLocProvider.GetValue(cardDatabase);
            }
            catch (Exception ex)
            {
                Log.Error("DeckInfoProvider", $"Error getting GreLocProvider: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Read type groups from DeckTypesDetails.ItemParent children.
        /// Children are either DeckDetailsLineItem components (type/subtype) or spacers (no component).
        /// First DeckDetailsLineItem after a spacer (or at start) = type header; rest = subtypes.
        /// </summary>
        private static List<TypeGroup> ReadTypeGroups(MonoBehaviour typesDetails)
        {
            var groups = new List<TypeGroup>();

            if (!_typesDetailsCache.IsInitialized)
                return groups;

            var tdh = _typesDetailsCache.Handles;

            try
            {
                var itemParent = tdh.ItemParent.GetValue(typesDetails) as Transform;
                if (itemParent == null) return groups;

                TypeGroup currentGroup = default;
                bool expectTypeHeader = true; // First item is always a type header

                for (int i = 0; i < itemParent.childCount; i++)
                {
                    var child = itemParent.GetChild(i);
                    if (child == null || !child.gameObject.activeSelf) continue;

                    // Try to find DeckDetailsLineItem component on this child
                    MonoBehaviour lineItem = null;
                    if (tdh.LineItemType != null)
                    {
                        var comp = child.GetComponent(tdh.LineItemType);
                        lineItem = comp as MonoBehaviour;
                    }

                    if (lineItem == null)
                    {
                        // This is a spacer - next DeckDetailsLineItem will be a type header
                        expectTypeHeader = true;
                        continue;
                    }

                    // Read Name and Quantity
                    string name = GetTmpTextValue(tdh.LineItemName.GetValue(lineItem));
                    string quantity = GetTmpTextValue(tdh.LineItemQuantity.GetValue(lineItem));

                    if (string.IsNullOrEmpty(name)) continue;
                    name = name.Trim();
                    quantity = quantity?.Trim() ?? "0";

                    if (expectTypeHeader)
                    {
                        // Save previous group if it has content
                        if (!string.IsNullOrEmpty(currentGroup.TypeName))
                            groups.Add(currentGroup);

                        currentGroup = new TypeGroup
                        {
                            TypeName = name,
                            TypeQuantity = quantity,
                            Subtypes = new List<(string name, string quantity)>()
                        };
                        expectTypeHeader = false;
                    }
                    else
                    {
                        // Subtype line
                        currentGroup.Subtypes.Add((name, quantity));
                    }
                }

                // Don't forget the last group
                if (!string.IsNullOrEmpty(currentGroup.TypeName))
                    groups.Add(currentGroup);
            }
            catch (Exception ex)
            {
                Log.Error("DeckInfoProvider", $"Error reading type groups: {ex.Message}");
            }

            return groups;
        }

        private static bool IsValidCached(MonoBehaviour cached)
        {
            if (cached == null) return false;
            try
            {
                return cached.gameObject != null && cached.gameObject.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validate cached component even if its GameObject is inactive.
        /// Used for DeckCostsDetails which the game writes to even when popup is closed.
        /// </summary>
        private static bool IsValidCachedAllowInactive(MonoBehaviour cached)
        {
            if (cached == null) return false;
            try
            {
                // Just check the object isn't destroyed
                return cached.gameObject != null;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Reflection Initialization

        /// <summary>
        /// Initialize the Pantry reflection cache by seeding it with the Pantry type
        /// (discovered at runtime via FindType). Idempotent — bails out if already
        /// initialized or if the Pantry type cannot be located.
        /// </summary>
        private static void EnsurePantryInitialized()
        {
            if (_pantryCache.IsInitialized) return;
            Type pantryType = FindType("Wizards.Mtga.Pantry");
            if (pantryType == null)
            {
                Log.Warn("DeckInfoProvider", "Could not find Pantry type");
                return;
            }
            _pantryCache.EnsureInitialized(pantryType);
        }

        /// <summary>
        /// Initialize the CardDatabase reflection cache by seeding it with the Pantry type
        /// (the CardDatabase type is resolved inside the builder via FindType).
        /// </summary>
        private static void EnsureCardDatabaseInitialized()
        {
            if (_cardDatabaseCache.IsInitialized) return;
            Type pantryType = FindType("Wizards.Mtga.Pantry");
            if (pantryType == null)
            {
                Log.Warn("DeckInfoProvider", "Could not find Pantry type for CardDatabase");
                return;
            }
            _cardDatabaseCache.EnsureInitialized(pantryType);
        }

        #endregion

        #region TMP_Text Helpers

        /// <summary>
        /// Find TMP_Text (TextMeshProUGUI) component on a GameObject.
        /// </summary>
        private static object FindTmpTextOnObject(GameObject go)
        {
            if (go == null) return null;

            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                string typeName = comp.GetType().Name;
                if (typeName == "TextMeshProUGUI" || typeName == "TMP_Text" || typeName == "TextMeshPro")
                {
                    return comp;
                }
            }

            // Also check children
            foreach (var comp in go.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                string typeName = comp.GetType().Name;
                if (typeName == "TextMeshProUGUI" || typeName == "TMP_Text" || typeName == "TextMeshPro")
                {
                    return comp;
                }
            }

            return null;
        }

        /// <summary>
        /// Get .text property from a TMP_Text-like object via reflection.
        /// </summary>
        private static string GetTmpTextValue(object tmpTextComponent)
        {
            if (tmpTextComponent == null) return null;

            try
            {
                var textProp = tmpTextComponent.GetType().GetProperty("text", PublicInstance);
                return textProp?.GetValue(tmpTextComponent) as string;
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
