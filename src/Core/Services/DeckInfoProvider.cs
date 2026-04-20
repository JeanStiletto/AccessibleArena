using UnityEngine;
using MelonLoader;
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

        // Cached reflection members for DeckMainTitlePanel
        private static FieldInfo _cardCountLabelField;         // _cardCountLabel (Localize component)
        private static bool _titlePanelReflectionInit;

        // Cached reflection members for DeckCostsDetails
        private static FieldInfo[] _costBarFields;             // OneOrLessItem through SixOrGreaterItem (CostBarItem)
        private static FieldInfo _costBarQuantityLabelField;   // CostBarItem.QuantityLabel (TMP_Text)
        private static FieldInfo _averageTextField;            // AverageText (TMP_Text)
        private static FieldInfo _creaturesItemField;          // CreaturesItem (TypeLineItem)
        private static FieldInfo _othersItemField;             // OthersItem (TypeLineItem)
        private static FieldInfo _landsItemField;              // LandsItem (TypeLineItem)
        private static FieldInfo _typeLineQuantityField;       // TypeLineItem.Quantity (TMP_Text)
        private static FieldInfo _typeLinePercentField;        // TypeLineItem.Percent (TMP_Text)
        private static MethodInfo _setDeckMethod;              // DeckCostsDetails.SetDeck(IReadOnlyList<CardPrintingQuantity>)
        private static bool _costsDetailsReflectionInit;

        // Cached reflection for Pantry.Get<DeckBuilderModelProvider>().Model.GetFilteredMainDeck()
        private static MethodInfo _pantryGetModelProviderMethod;
        private static PropertyInfo _modelProperty;            // DeckBuilderModelProvider.Model
        private static MethodInfo _getFilteredMainDeckMethod;  // DeckBuilderModel.GetFilteredMainDeck()
        private static bool _pantryReflectionInit;

        // Cached reflection members for DeckTypesDetails
        private static FieldInfo _typesItemParentField;        // ItemParent (Transform)
        private static MethodInfo _typesSetDeckMethod;         // SetDeck(IReadOnlyList<CardPrintingQuantity>, IGreLocProvider)
        private static Type _lineItemType;                     // DeckDetailsLineItem
        private static FieldInfo _lineItemNameField;           // DeckDetailsLineItem.Name (TMP_Text)
        private static FieldInfo _lineItemQuantityField;       // DeckDetailsLineItem.Quantity (TMP_Text)
        private static bool _typesDetailsReflectionInit;

        // Cached reflection for Pantry.Get<CardDatabase>().GreLocProvider
        private static MethodInfo _pantryGetCardDatabaseMethod;
        private static PropertyInfo _greLocProviderProperty;   // CardDatabase.GreLocProvider
        private static bool _cardDatabaseReflectionInit;

        // Cost bar field names matching DeckCostsDetails fields
        private static readonly string[] CostBarFieldNames = new[]
        {
            "OneOrLessItem", "TwoItem", "ThreeItem", "FourItem", "FiveItem", "SixOrGreaterItem"
        };
        private static readonly string[] CostBarLabels = new[]
        {
            "1 or less", "2", "3", "4", "5", "6 or more"
        };

        // Type line item field names
        private static readonly string[] TypeLineFieldNames = new[]
        {
            "CreaturesItem", "OthersItem", "LandsItem"
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
                if (_cardCountLabelField == null) return null;

                // _cardCountLabel is a Localize component (MonoBehaviour)
                var localizeComponent = _cardCountLabelField.GetValue(panel);
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

            if (details == null || _typeLineQuantityField == null)
                return entries;

            // Classify type groups into creature/others/land
            TypeGroup? creatureTypeGroup = null;
            var othersTypeGroups = new List<TypeGroup>();
            TypeGroup? landTypeGroup = null;

            if (typeGroups != null && typeGroups.Count > 0)
                ClassifyTypeGroups(details, typeGroups, out creatureTypeGroup, out othersTypeGroups, out landTypeGroup);

            var typeFields = new FieldInfo[] { _creaturesItemField, _othersItemField, _landsItemField };
            for (int i = 0; i < typeFields.Length; i++)
            {
                if (typeFields[i] == null) continue;
                try
                {
                    var typeLineItem = typeFields[i].GetValue(details);
                    if (typeLineItem == null) continue;

                    string quantity = GetTmpTextValue(_typeLineQuantityField.GetValue(typeLineItem));
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

                    if (_typeLinePercentField != null)
                    {
                        string percent = GetTmpTextValue(_typeLinePercentField.GetValue(typeLineItem));
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

            int creatureCount = ReadTypeLineCount(costsDetails, _creaturesItemField);
            int landCount = ReadTypeLineCount(costsDetails, _landsItemField);

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
            if (costsDetails == null || typeLineField == null || _typeLineQuantityField == null)
                return 0;
            try
            {
                var typeLineItem = typeLineField.GetValue(costsDetails);
                if (typeLineItem == null) return 0;
                string qty = GetTmpTextValue(_typeLineQuantityField.GetValue(typeLineItem));
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

            if (details == null || _costBarFields == null || _costBarQuantityLabelField == null)
                return entries;

            try
            {
                for (int i = 0; i < _costBarFields.Length; i++)
                {
                    if (_costBarFields[i] == null) continue;

                    var barItem = _costBarFields[i].GetValue(details);
                    if (barItem == null) continue;

                    string text = GetTmpTextValue(_costBarQuantityLabelField.GetValue(barItem));
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

            if (details != null && _typeLineQuantityField != null)
            {
                var typeFields = new FieldInfo[] { _creaturesItemField, _othersItemField, _landsItemField };
                for (int i = 0; i < typeFields.Length; i++)
                {
                    if (typeFields[i] == null) continue;
                    try
                    {
                        var typeLineItem = typeFields[i].GetValue(details);
                        if (typeLineItem == null) continue;

                        string quantity = GetTmpTextValue(_typeLineQuantityField.GetValue(typeLineItem));
                        if (string.IsNullOrEmpty(quantity) || quantity.Trim() == "0") continue;

                        if (sb.Length > 0) sb.Append(". ");
                        sb.Append($"{quantity.Trim()} {TypeLineDisplayNames[i]}");

                        if (_typeLinePercentField != null)
                        {
                            string percent = GetTmpTextValue(_typeLinePercentField.GetValue(typeLineItem));
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
            if (details == null || _costBarFields == null || _costBarQuantityLabelField == null)
                return null;

            try
            {
                var sb = new StringBuilder();
                bool first = true;

                for (int i = 0; i < _costBarFields.Length; i++)
                {
                    if (_costBarFields[i] == null) continue;

                    var barItem = _costBarFields[i].GetValue(details);
                    if (barItem == null) continue;

                    string text = GetTmpTextValue(_costBarQuantityLabelField.GetValue(barItem));
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
                if (_averageTextField == null) return null;

                var avgText = _averageTextField.GetValue(details);
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
                    if (!_titlePanelReflectionInit)
                        InitializeTitlePanelReflection(mb.GetType());
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
                    if (!_costsDetailsReflectionInit)
                        InitializeCostsDetailsReflection(mb.GetType());
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
                if (!_pantryReflectionInit)
                    InitializePantryReflection();

                if (_pantryGetModelProviderMethod == null || _modelProperty == null || _getFilteredMainDeckMethod == null)
                    return;

                // Pantry.Get<DeckBuilderModelProvider>()
                var modelProvider = _pantryGetModelProviderMethod.Invoke(null, null);
                if (modelProvider == null) return;

                // .Model
                var model = _modelProperty.GetValue(modelProvider);
                if (model == null) return;

                // .GetFilteredMainDeck()
                var deckData = _getFilteredMainDeckMethod.Invoke(model, null);
                if (deckData == null) return;

                // DeckCostsDetails.SetDeck(deckData)
                if (_setDeckMethod != null)
                {
                    _setDeckMethod.Invoke(costsDetails, new object[] { deckData });
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
                    if (!_typesDetailsReflectionInit)
                        InitializeTypesDetailsReflection(mb.GetType());
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
                if (_typesSetDeckMethod == null) return;

                // Skip if already populated - the game's SetDeck uses Destroy() which is deferred,
                // so calling it again would create duplicates readable in the same frame
                if (_typesItemParentField != null)
                {
                    var itemParent = _typesItemParentField.GetValue(typesDetails) as Transform;
                    if (itemParent != null && itemParent.childCount > 0)
                        return;
                }

                // Get deck data (reuse existing Pantry reflection)
                if (!_pantryReflectionInit)
                    InitializePantryReflection();

                if (_pantryGetModelProviderMethod == null || _modelProperty == null || _getFilteredMainDeckMethod == null)
                    return;

                var modelProvider = _pantryGetModelProviderMethod.Invoke(null, null);
                if (modelProvider == null) return;

                var model = _modelProperty.GetValue(modelProvider);
                if (model == null) return;

                var deckData = _getFilteredMainDeckMethod.Invoke(model, null);
                if (deckData == null) return;

                // Get IGreLocProvider
                var locProvider = GetGreLocProvider();
                if (locProvider == null) return;

                // DeckTypesDetails.SetDeck(deck, locMan)
                _typesSetDeckMethod.Invoke(typesDetails, new object[] { deckData, locProvider });
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
                if (!_cardDatabaseReflectionInit)
                    InitializeCardDatabaseReflection();

                if (_pantryGetCardDatabaseMethod == null || _greLocProviderProperty == null)
                    return null;

                var cardDatabase = _pantryGetCardDatabaseMethod.Invoke(null, null);
                if (cardDatabase == null) return null;

                return _greLocProviderProperty.GetValue(cardDatabase);
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

            if (_typesItemParentField == null || _lineItemNameField == null || _lineItemQuantityField == null)
                return groups;

            try
            {
                var itemParent = _typesItemParentField.GetValue(typesDetails) as Transform;
                if (itemParent == null) return groups;

                TypeGroup currentGroup = default;
                bool expectTypeHeader = true; // First item is always a type header

                for (int i = 0; i < itemParent.childCount; i++)
                {
                    var child = itemParent.GetChild(i);
                    if (child == null || !child.gameObject.activeSelf) continue;

                    // Try to find DeckDetailsLineItem component on this child
                    MonoBehaviour lineItem = null;
                    if (_lineItemType != null)
                    {
                        var comp = child.GetComponent(_lineItemType);
                        lineItem = comp as MonoBehaviour;
                    }

                    if (lineItem == null)
                    {
                        // This is a spacer - next DeckDetailsLineItem will be a type header
                        expectTypeHeader = true;
                        continue;
                    }

                    // Read Name and Quantity
                    string name = GetTmpTextValue(_lineItemNameField.GetValue(lineItem));
                    string quantity = GetTmpTextValue(_lineItemQuantityField.GetValue(lineItem));

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

        private static void InitializeTitlePanelReflection(Type type)
        {
            if (_titlePanelReflectionInit) return;
            try
            {
                _cardCountLabelField = type.GetField("_cardCountLabel", PrivateInstance);
                _titlePanelReflectionInit = true;

                Log.Msg("DeckInfoProvider", $"TitlePanel reflection: _cardCountLabel={_cardCountLabelField != null}");
            }
            catch (Exception ex)
            {
                Log.Error("DeckInfoProvider", $"TitlePanel reflection failed: {ex.Message}");
            }
        }

        private static void InitializeCostsDetailsReflection(Type type)
        {
            if (_costsDetailsReflectionInit) return;
            try
            {
                // Get CostBarItem fields (OneOrLessItem through SixOrGreaterItem)
                _costBarFields = new FieldInfo[CostBarFieldNames.Length];
                Type costBarItemType = null;

                for (int i = 0; i < CostBarFieldNames.Length; i++)
                {
                    _costBarFields[i] = type.GetField(CostBarFieldNames[i], PublicInstance)
                                     ?? type.GetField(CostBarFieldNames[i], PrivateInstance);

                    if (costBarItemType == null && _costBarFields[i] != null)
                    {
                        costBarItemType = _costBarFields[i].FieldType;
                    }
                }

                // Get QuantityLabel from CostBarItem nested class
                if (costBarItemType != null)
                {
                    _costBarQuantityLabelField = costBarItemType.GetField("QuantityLabel", PublicInstance)
                                              ?? costBarItemType.GetField("QuantityLabel", PrivateInstance);
                }

                // Get AverageText (TMP_Text)
                _averageTextField = type.GetField("AverageText", PublicInstance)
                                 ?? type.GetField("AverageText", PrivateInstance);

                // Get TypeLineItem fields
                _creaturesItemField = type.GetField("CreaturesItem", PublicInstance)
                                   ?? type.GetField("CreaturesItem", PrivateInstance);
                _othersItemField = type.GetField("OthersItem", PublicInstance)
                                ?? type.GetField("OthersItem", PrivateInstance);
                _landsItemField = type.GetField("LandsItem", PublicInstance)
                               ?? type.GetField("LandsItem", PrivateInstance);

                // Get Quantity/Percent from TypeLineItem nested class
                Type typeLineItemType = _creaturesItemField?.FieldType ?? _othersItemField?.FieldType;
                if (typeLineItemType != null)
                {
                    _typeLineQuantityField = typeLineItemType.GetField("Quantity", PublicInstance)
                                          ?? typeLineItemType.GetField("Quantity", PrivateInstance);
                    _typeLinePercentField = typeLineItemType.GetField("Percent", PublicInstance)
                                         ?? typeLineItemType.GetField("Percent", PrivateInstance);
                }

                // Get SetDeck method
                _setDeckMethod = type.GetMethod("SetDeck", PublicInstance);

                _costsDetailsReflectionInit = true;

                Log.Msg("DeckInfoProvider", $"CostsDetails reflection: " +
                    $"costBarItemType={costBarItemType?.Name ?? "null"}, " +
                    $"quantityLabel={_costBarQuantityLabelField != null}, " +
                    $"averageText={_averageTextField != null}, " +
                    $"typeLineItemType={typeLineItemType?.Name ?? "null"}, " +
                    $"typeQuantity={_typeLineQuantityField != null}, " +
                    $"typePercent={_typeLinePercentField != null}, " +
                    $"setDeck={_setDeckMethod != null}");
            }
            catch (Exception ex)
            {
                Log.Error("DeckInfoProvider", $"CostsDetails reflection failed: {ex.Message}");
            }
        }

        private static void InitializePantryReflection()
        {
            if (_pantryReflectionInit) return;
            _pantryReflectionInit = true;

            try
            {
                // Find Pantry type
                Type pantryType = FindType("Wizards.Mtga.Pantry");
                if (pantryType == null)
                {
                    Log.Warn("DeckInfoProvider", "Could not find Pantry type");
                    return;
                }

                // Find DeckBuilderModelProvider type
                Type modelProviderType = FindType("Core.Code.Decks.DeckBuilderModelProvider");
                if (modelProviderType == null)
                {
                    Log.Warn("DeckInfoProvider", "Could not find DeckBuilderModelProvider type");
                    return;
                }

                // Pantry.Get<T>() is a generic method - make it specific for DeckBuilderModelProvider
                var getMethod = pantryType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                if (getMethod != null && getMethod.IsGenericMethod)
                {
                    _pantryGetModelProviderMethod = getMethod.MakeGenericMethod(modelProviderType);
                }

                // DeckBuilderModelProvider.Model property
                _modelProperty = modelProviderType.GetProperty("Model", PublicInstance)
                              ?? modelProviderType.GetProperty("Model", PrivateInstance);

                // DeckBuilderModel.GetFilteredMainDeck()
                if (_modelProperty != null)
                {
                    var modelType = _modelProperty.PropertyType;
                    _getFilteredMainDeckMethod = modelType.GetMethod("GetFilteredMainDeck", PublicInstance)
                                              ?? modelType.GetMethod("GetFilteredMainDeck", PrivateInstance);
                }

                Log.Msg("DeckInfoProvider", $"Pantry reflection: " +
                    $"pantryGet={_pantryGetModelProviderMethod != null}, " +
                    $"model={_modelProperty != null}, " +
                    $"getFilteredMainDeck={_getFilteredMainDeckMethod != null}");
            }
            catch (Exception ex)
            {
                Log.Error("DeckInfoProvider", $"Pantry reflection failed: {ex.Message}");
            }
        }

        private static void InitializeTypesDetailsReflection(Type type)
        {
            if (_typesDetailsReflectionInit) return;
            try
            {
                // ItemParent (Transform) - public field
                _typesItemParentField = type.GetField("ItemParent", PublicInstance);

                // SetDeck(IReadOnlyList<CardPrintingQuantity>, IGreLocProvider) - 2 params
                foreach (var method in type.GetMethods(PublicInstance))
                {
                    if (method.Name == "SetDeck" && method.GetParameters().Length == 2)
                    {
                        _typesSetDeckMethod = method;
                        break;
                    }
                }

                // Get DeckDetailsLineItem type from TypePrefab field
                var typePrefabField = type.GetField("TypePrefab", PublicInstance);
                if (typePrefabField != null)
                {
                    _lineItemType = typePrefabField.FieldType;

                    // DeckDetailsLineItem.Name and .Quantity (public TMP_Text fields)
                    _lineItemNameField = _lineItemType.GetField("Name", PublicInstance);
                    _lineItemQuantityField = _lineItemType.GetField("Quantity", PublicInstance);
                }

                _typesDetailsReflectionInit = true;

                Log.Msg("DeckInfoProvider", $"TypesDetails reflection: " +
                    $"itemParent={_typesItemParentField != null}, " +
                    $"setDeck={_typesSetDeckMethod != null}, " +
                    $"lineItemType={_lineItemType?.Name ?? "null"}, " +
                    $"name={_lineItemNameField != null}, " +
                    $"quantity={_lineItemQuantityField != null}");
            }
            catch (Exception ex)
            {
                Log.Error("DeckInfoProvider", $"TypesDetails reflection failed: {ex.Message}");
            }
        }

        private static void InitializeCardDatabaseReflection()
        {
            if (_cardDatabaseReflectionInit) return;
            _cardDatabaseReflectionInit = true;

            try
            {
                // Find Pantry type (may already have it from InitializePantryReflection)
                Type pantryType = FindType("Wizards.Mtga.Pantry");
                if (pantryType == null)
                {
                    Log.Warn("DeckInfoProvider", "Could not find Pantry type for CardDatabase");
                    return;
                }

                // Find CardDatabase type (namespace: Wotc.Mtga.Cards.Database)
                Type cardDatabaseType = FindType("Wotc.Mtga.Cards.Database.CardDatabase");
                if (cardDatabaseType == null)
                {
                    Log.Warn("DeckInfoProvider", "Could not find CardDatabase type");
                    return;
                }

                // Pantry.Get<CardDatabase>()
                var getMethod = pantryType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                if (getMethod != null && getMethod.IsGenericMethod)
                {
                    _pantryGetCardDatabaseMethod = getMethod.MakeGenericMethod(cardDatabaseType);
                }

                // CardDatabase.GreLocProvider property
                _greLocProviderProperty = cardDatabaseType.GetProperty("GreLocProvider", PublicInstance);

                Log.Msg("DeckInfoProvider", $"CardDatabase reflection: " +
                    $"pantryGetCardDb={_pantryGetCardDatabaseMethod != null}, " +
                    $"greLocProvider={_greLocProviderProperty != null}");
            }
            catch (Exception ex)
            {
                Log.Error("DeckInfoProvider", $"CardDatabase reflection failed: {ex.Message}");
            }
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
