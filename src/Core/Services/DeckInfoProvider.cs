using UnityEngine;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using AccessibleArena.Core.Models;

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
        private static readonly BindingFlags PrivateInstance =
            BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly BindingFlags PublicInstance =
            BindingFlags.Public | BindingFlags.Instance;

        // Cached component references (cleared on scene change)
        private static MonoBehaviour _cachedTitlePanel;        // DeckMainTitlePanel
        private static MonoBehaviour _cachedCostsDetails;      // DeckCostsDetails

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
                MelonLogger.Error($"[DeckInfoProvider] Error reading card count: {ex.Message}");
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

            var cardEntries = BuildCardInfoEntries(details);
            if (cardEntries.Count > 0)
                rows.Add(("Cards", cardEntries));

            var manaEntries = BuildManaCurveEntries(details);
            if (manaEntries.Count > 0)
                rows.Add(("Mana Curve", manaEntries));

            return rows;
        }

        /// <summary>
        /// Build individual card info entries: ["35 von 60", "20 Creatures (56%)", ...]
        /// </summary>
        private static List<string> BuildCardInfoEntries(MonoBehaviour details)
        {
            var entries = new List<string>();

            string cardCount = GetCardCountText();
            if (!string.IsNullOrEmpty(cardCount))
                entries.Add(cardCount);

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

                        string entry = $"{quantity.Trim()} {TypeLineDisplayNames[i]}";
                        if (_typeLinePercentField != null)
                        {
                            string percent = GetTmpTextValue(_typeLinePercentField.GetValue(typeLineItem));
                            if (!string.IsNullOrEmpty(percent))
                                entry += $" ({percent.Trim()})";
                        }
                        entries.Add(entry);
                    }
                    catch { }
                }
            }

            return entries;
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
                MelonLogger.Error($"[DeckInfoProvider] Error building mana curve entries: {ex.Message}");
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
                    catch { }
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
                MelonLogger.Error($"[DeckInfoProvider] Error building mana curve: {ex.Message}");
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
                MelonLogger.Error($"[DeckInfoProvider] Error reading average cost: {ex.Message}");
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
                if (mb.GetType().Name == "DeckMainTitlePanel")
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
                if (mb.GetType().Name == "DeckCostsDetails")
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
                MelonLogger.Error($"[DeckInfoProvider] Error populating CostsDetails: {ex.Message}");
            }
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

                MelonLogger.Msg($"[DeckInfoProvider] TitlePanel reflection: _cardCountLabel={_cardCountLabelField != null}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[DeckInfoProvider] TitlePanel reflection failed: {ex.Message}");
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

                MelonLogger.Msg($"[DeckInfoProvider] CostsDetails reflection: " +
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
                MelonLogger.Error($"[DeckInfoProvider] CostsDetails reflection failed: {ex.Message}");
            }
        }

        private static void InitializePantryReflection()
        {
            if (_pantryReflectionInit) return;
            _pantryReflectionInit = true;

            try
            {
                // Find Pantry type
                Type pantryType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    pantryType = asm.GetType("Wizards.Mtga.Pantry");
                    if (pantryType != null) break;
                }
                if (pantryType == null)
                {
                    MelonLogger.Warning("[DeckInfoProvider] Could not find Pantry type");
                    return;
                }

                // Find DeckBuilderModelProvider type
                Type modelProviderType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    modelProviderType = asm.GetType("Core.Code.Decks.DeckBuilderModelProvider");
                    if (modelProviderType != null) break;
                }
                if (modelProviderType == null)
                {
                    MelonLogger.Warning("[DeckInfoProvider] Could not find DeckBuilderModelProvider type");
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

                MelonLogger.Msg($"[DeckInfoProvider] Pantry reflection: " +
                    $"pantryGet={_pantryGetModelProviderMethod != null}, " +
                    $"model={_modelProperty != null}, " +
                    $"getFilteredMainDeck={_getFilteredMainDeckMethod != null}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[DeckInfoProvider] Pantry reflection failed: {ex.Message}");
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
