using UnityEngine;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AccessibleArena.Core.Models;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Provides access to card Model data from the game's internal systems.
    /// Handles reflection-based property access, name lookups, mana parsing, and card info extraction.
    /// Use CardDetector for card detection (IsCard, GetCardRoot).
    /// Related providers: CardTextProvider, CardStateProvider, DeckCardProvider, ExtendedCardInfoProvider.
    /// </summary>
    public static class CardModelProvider
    {
        // Cache for Model reflection to improve performance
        private static Type _cachedModelType = null;
        private static readonly Dictionary<string, PropertyInfo> _modelPropertyCache = new Dictionary<string, PropertyInfo>();
        private static bool _modelPropertiesLogged = false;
        private static bool _abilityPropertiesLogged = false;
        private static bool _listMetaCardHolderLogged = false;

        // Cache for IdNameProvider lookup
        private static object _idNameProvider = null;
        private static MethodInfo _getNameMethod = null;
        private static bool _idNameProviderSearched = false;

        // Cache: abilityId -> (parentCardGrpId, allAbilityIds, cardTitleId)
        // Populated when processing normal cards with abilities, used for ability CDC lookups
        private static readonly Dictionary<uint, (uint cardGrpId, uint[] abilityIds, uint cardTitleId)> _abilityParentCache
            = new Dictionary<uint, (uint, uint[], uint)>();

        /// <summary>
        /// Clears the model provider cache. Call when scene changes.
        /// </summary>
        public static void ClearCache()
        {
            _modelPropertyCache.Clear();
            _cachedModelType = null;
            _idNameProvider = null;
            _getNameMethod = null;
            _idNameProviderSearched = false;
            _abilityPropertiesLogged = false;
            // CDC Model property cache
            _cdcModelProp = null;
            _cdcModelPropType = null;
            // Model instance cache
            _instancePropCached = null;
            _instancePropSearched = false;
            // Delegate to sub-providers
            CardTextProvider.ClearCache();
            CardStateProvider.ClearCache();
            DeckCardProvider.ClearCache();
            ExtendedCardInfoProvider.ClearCache();
        }

        #region Component Access

        /// <summary>
        /// Gets the DuelScene_CDC or Meta_CDC component from a card GameObject.
        /// DuelScene_CDC is for duel cards, Meta_CDC is for menu cards (deck builder, collection).
        /// Returns null if neither is found.
        /// </summary>
        public static Component GetDuelSceneCDC(GameObject card)
        {
            if (card == null) return null;

            // Check this object
            foreach (var component in card.GetComponents<Component>())
            {
                if (component == null) continue;
                string typeName = component.GetType().Name;
                if (typeName == T.DuelSceneCDC || typeName == T.MetaCDC)
                {
                    return component;
                }
            }

            // For Meta cards, the CDC might be on a child named "CardView" or accessed via CardView property
            // Check children for Meta_CDC
            foreach (var component in card.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                string typeName = component.GetType().Name;
                if (typeName == T.MetaCDC)
                {
                    return component;
                }
            }

            return null;
        }

        // Flag for one-time component logging
        private static bool _metaCardViewComponentsLogged = false;

        /// <summary>
        /// Gets a MetaCardView component (PagesMetaCardView, BoosterMetaCardView, etc.) from a card GameObject.
        /// These are used in Meta scenes like deck builder, booster opening, rewards.
        /// </summary>
        public static Component GetMetaCardView(GameObject card)
        {
            if (card == null) return null;

            foreach (var component in card.GetComponents<Component>())
            {
                if (component == null) continue;
                string typeName = component.GetType().Name;
                // Use Contains for ListMetaCardView to also match ListMetaCardView_Expanding (deck list cards)
                // ListCommanderView extends ListMetaCardView_Expanding but has a different name
                if (typeName == T.PagesMetaCardView ||
                    typeName == T.MetaCardView ||
                    typeName == T.BoosterMetaCardView ||
                    typeName == T.DraftPackCardView ||
                    typeName.Contains("ListMetaCardView") ||
                    typeName == "ListCommanderView")
                {
                    // Log once when we find a MetaCardView
                    if (!_metaCardViewComponentsLogged)
                    {
                        _metaCardViewComponentsLogged = true;
                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"=== FOUND MetaCardView: {typeName} on '{card.name}' ===");
                    }
                    return component;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the Model object from a DuelScene_CDC component.
        /// The Model contains card data like Name, Power, Toughness, CardTypes, etc.
        /// Returns null if not available.
        /// </summary>
        // Cache for CDC "Model" property - same type throughout entire duel
        private static PropertyInfo _cdcModelProp = null;
        private static Type _cdcModelPropType = null;

        public static object GetCardModel(Component cdcComponent)
        {
            if (cdcComponent == null) return null;

            try
            {
                var cdcType = cdcComponent.GetType();
                if (cdcType != _cdcModelPropType)
                {
                    _cdcModelProp = cdcType.GetProperty("Model");
                    _cdcModelPropType = cdcType;
                }
                return _cdcModelProp?.GetValue(cdcComponent);
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Error getting Model: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the card data from a MetaCardView component.
        /// MetaCardView typically has a Model property with CardPrintingData or similar.
        /// </summary>
        public static object GetMetaCardModel(Component metaCardView)
        {
            if (metaCardView == null) return null;

            try
            {
                var viewType = metaCardView.GetType();

                // Try Model property first (common pattern)
                var modelProp = viewType.GetProperty("Model");
                if (modelProp != null)
                {
                    var model = modelProp.GetValue(metaCardView);
                    if (model != null) return model;
                }

                // Try CardData property
                var cardDataProp = viewType.GetProperty("CardData");
                if (cardDataProp != null)
                {
                    var cardData = cardDataProp.GetValue(metaCardView);
                    if (cardData != null) return cardData;
                }

                // Try Data property
                var dataProp = viewType.GetProperty("Data");
                if (dataProp != null)
                {
                    var data = dataProp.GetValue(metaCardView);
                    if (data != null) return data;
                }

                // Try Card property (used by ListMetaCardView_Expanding for deck list cards)
                var cardProp = viewType.GetProperty("Card");
                if (cardProp != null)
                {
                    var card = cardProp.GetValue(metaCardView);
                    if (card != null) return card;
                }

                return null;
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Error getting MetaCardView Model: {ex.Message}");
                return null;
            }
        }

        // Flag to only log MetaCardView properties once
        private static bool _metaCardViewPropertiesLogged = false;

        /// <summary>
        /// Logs all properties available on a MetaCardView component for discovery.
        /// Only logs once per session.
        /// </summary>
        public static void LogMetaCardViewProperties(Component metaCardView)
        {
            if (_metaCardViewPropertiesLogged || metaCardView == null) return;
            _metaCardViewPropertiesLogged = true;

            var viewType = metaCardView.GetType();
            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"=== METACARDVIEW TYPE: {viewType.FullName} ===");

            // Log properties
            var properties = viewType.GetProperties(PublicInstance);
            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(metaCardView);
                    string valueStr = value?.ToString() ?? "null";
                    if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"MetaCardView Property: {prop.Name} = {valueStr} ({prop.PropertyType.Name})");
                }
                catch (Exception ex)
                {
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"MetaCardView Property: {prop.Name} = [Error: {ex.Message}] ({prop.PropertyType.Name})");
                }
            }
            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"=== END METACARDVIEW PROPERTIES ===");
        }

        /// <summary>
        /// Logs all properties and methods on a ListMetaCardHolder component for deck list card discovery.
        /// This helps understand how to access the card list with GrpIds for deck cards.
        /// </summary>
        private static void LogListMetaCardHolderProperties(MonoBehaviour holder, Type holderType)
        {
            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"=== LISTMETACARDHOLDER TYPE: {holderType.FullName} ===");
            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"GameObject: {holder.gameObject.name}");

            // Log all properties
            var properties = holderType.GetProperties(AllInstanceFlags);
            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"--- Properties ({properties.Length}) ---");
            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(holder);
                    string valueStr = value?.ToString() ?? "null";
                    string typeName = prop.PropertyType.Name;

                    // For collections, try to get count
                    if (value != null)
                    {
                        var countProp = value.GetType().GetProperty("Count");
                        if (countProp != null)
                        {
                            var count = countProp.GetValue(value);
                            valueStr = $"[Count: {count}] {valueStr}";
                        }

                        // Check if it's an array
                        if (value is System.Array arr)
                        {
                            valueStr = $"[Length: {arr.Length}] {value.GetType().Name}";
                        }
                    }

                    if (valueStr.Length > 150) valueStr = valueStr.Substring(0, 150) + "...";
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"  Property: {prop.Name} = {valueStr} ({typeName})");

                    // If property name suggests cards, log more details
                    if (prop.Name.ToLower().Contains("card") || prop.Name.ToLower().Contains("item") ||
                        prop.Name.ToLower().Contains("data") || prop.Name.ToLower().Contains("model") ||
                        prop.Name.ToLower().Contains("list") || prop.Name.ToLower().Contains("entries"))
                    {
                        LogCollectionContents(value, prop.Name);
                    }
                }
                catch (Exception ex)
                {
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"  Property: {prop.Name} = [Error: {ex.Message}] ({prop.PropertyType.Name})");
                }
            }

            // Log interesting methods that might return card data
            var methods = holderType.GetMethods(PublicInstance);
            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"--- Methods (filtered) ---");
            foreach (var method in methods)
            {
                if (method.DeclaringType == typeof(object) || method.DeclaringType == typeof(MonoBehaviour) ||
                    method.DeclaringType == typeof(UnityEngine.Component) || method.DeclaringType == typeof(UnityEngine.Behaviour))
                    continue;

                string methodName = method.Name.ToLower();
                if (methodName.Contains("card") || methodName.Contains("item") || methodName.Contains("data") ||
                    methodName.Contains("get") || methodName.Contains("model") || methodName.Contains("grp"))
                {
                    var parameters = method.GetParameters();
                    string paramStr = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"  Method: {method.Name}({paramStr}) -> {method.ReturnType.Name}");
                }
            }

            // Log fields as well (sometimes data is stored in fields)
            var fields = holderType.GetFields(AllInstanceFlags);
            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"--- Fields (filtered) ---");
            foreach (var field in fields)
            {
                string fieldName = field.Name.ToLower();
                if (fieldName.Contains("card") || fieldName.Contains("item") || fieldName.Contains("data") ||
                    fieldName.Contains("model") || fieldName.Contains("list") || fieldName.Contains("entries") ||
                    fieldName.Contains("grp"))
                {
                    try
                    {
                        var value = field.GetValue(holder);
                        string valueStr = value?.ToString() ?? "null";

                        if (value != null)
                        {
                            var countProp = value.GetType().GetProperty("Count");
                            if (countProp != null)
                            {
                                var count = countProp.GetValue(value);
                                valueStr = $"[Count: {count}] {valueStr}";
                            }
                            if (value is System.Array arr)
                            {
                                valueStr = $"[Length: {arr.Length}] {value.GetType().Name}";
                            }
                        }

                        if (valueStr.Length > 150) valueStr = valueStr.Substring(0, 150) + "...";
                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"  Field: {field.Name} = {valueStr} ({field.FieldType.Name})");

                        // Log collection contents for card-related fields
                        if (fieldName.Contains("card") || fieldName.Contains("item") || fieldName.Contains("data"))
                        {
                            LogCollectionContents(value, field.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"  Field: {field.Name} = [Error: {ex.Message}] ({field.FieldType.Name})");
                    }
                }
            }

            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"=== END LISTMETACARDHOLDER ===");
        }

        /// <summary>
        /// Logs the contents of a collection to help discover card data structure.
        /// </summary>
        private static void LogCollectionContents(object collection, string collectionName)
        {
            if (collection == null) return;

            try
            {
                // Try to enumerate if it's IEnumerable
                if (collection is System.Collections.IEnumerable enumerable)
                {
                    int index = 0;
                    foreach (var item in enumerable)
                    {
                        if (index >= 3) // Only log first 3 items
                        {
                            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"    ... (more items)");
                            break;
                        }

                        if (item != null)
                        {
                            var itemType = item.GetType();
                            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"    [{index}] Type: {itemType.Name}");

                            // Try to get GrpId or similar properties
                            var grpIdProp = itemType.GetProperty("GrpId") ?? itemType.GetProperty("grpId") ?? itemType.GetProperty("CardGrpId");
                            if (grpIdProp != null)
                            {
                                var grpId = grpIdProp.GetValue(item);
                                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"    [{index}] GrpId: {grpId}");
                            }

                            // Try to get Quantity/Count
                            var qtyProp = itemType.GetProperty("Quantity") ?? itemType.GetProperty("Count") ?? itemType.GetProperty("Amount");
                            if (qtyProp != null)
                            {
                                var qty = qtyProp.GetValue(item);
                                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"    [{index}] Quantity: {qty}");
                            }

                            // Log all properties of the first item only
                            if (index == 0)
                            {
                                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"    [{index}] Item properties:");
                                foreach (var prop in itemType.GetProperties(PublicInstance))
                                {
                                    try
                                    {
                                        var val = prop.GetValue(item);
                                        string valStr = val?.ToString() ?? "null";
                                        if (valStr.Length > 80) valStr = valStr.Substring(0, 80) + "...";
                                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"      {prop.Name} = {valStr} ({prop.PropertyType.Name})");
                                    }
                                    catch { /* Some properties throw on access; skip for debug dump */ }
                                }
                            }
                        }
                        index++;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"    Error enumerating {collectionName}: {ex.Message}");
            }
        }

        #endregion

        #region Name Lookup

        /// <summary>
        /// Finds and caches an IdNameProvider instance for card name lookup.
        /// Searches for GameManager, WrapperController, or IdNameProvider components.
        /// </summary>
        private static void FindIdNameProvider()
        {
            // Retry search if we haven't found a method yet
            if (_idNameProviderSearched && _getNameMethod != null) return;
            _idNameProviderSearched = true;

            try
            {
                // Approach 1: Find GameManager in scene and get its LocManager or CardDatabase
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    var type = mb.GetType();
                    if (type.Name == "GameManager")
                    {
                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Found GameManager, checking for name lookup methods...");

                        // Try CardDatabase property - get CardTitleProvider or CardNameTextProvider
                        var cardDbProp = type.GetProperty("CardDatabase");
                        if (cardDbProp != null)
                        {
                            var cardDb = cardDbProp.GetValue(mb);
                            if (cardDb != null)
                            {
                                var cardDbType = cardDb.GetType();
                                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"CardDatabase type: {cardDbType.FullName}");

                                // Try CardTitleProvider
                                var titleProviderProp = cardDbType.GetProperty("CardTitleProvider");
                                if (titleProviderProp != null)
                                {
                                    var titleProvider = titleProviderProp.GetValue(cardDb);
                                    if (titleProvider != null)
                                    {
                                        var providerType = titleProvider.GetType();
                                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"CardTitleProvider type: {providerType.FullName}");

                                        // List methods on the provider
                                        foreach (var m in providerType.GetMethods(PublicInstance))
                                        {
                                            if (m.DeclaringType == typeof(object)) continue;
                                            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"CardTitleProvider.{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))}) -> {m.ReturnType.Name}");
                                        }

                                        // Use GetCardTitle(UInt32, Boolean, String) method
                                        var getMethod = providerType.GetMethod("GetCardTitle", new[] { typeof(uint), typeof(bool), typeof(string) });

                                        if (getMethod != null)
                                        {
                                            _idNameProvider = titleProvider;
                                            _getNameMethod = getMethod;
                                            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Using CardTitleProvider.GetCardTitle for name lookup");
                                            return;
                                        }
                                    }
                                }

                                // Try CardNameTextProvider
                                var nameProviderProp = cardDbType.GetProperty("CardNameTextProvider");
                                if (nameProviderProp != null)
                                {
                                    var nameProvider = nameProviderProp.GetValue(cardDb);
                                    if (nameProvider != null)
                                    {
                                        var providerType = nameProvider.GetType();
                                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"CardNameTextProvider type: {providerType.FullName}");

                                        // List methods on the provider
                                        foreach (var m in providerType.GetMethods(PublicInstance))
                                        {
                                            if (m.DeclaringType == typeof(object)) continue;
                                            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"CardNameTextProvider.{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))}) -> {m.ReturnType.Name}");
                                        }

                                        // Try common method names
                                        var getMethod = providerType.GetMethod("GetName", new[] { typeof(uint) })
                                            ?? providerType.GetMethod("GetText", new[] { typeof(uint) })
                                            ?? providerType.GetMethod("Get", new[] { typeof(uint) });

                                        if (getMethod != null)
                                        {
                                            _idNameProvider = nameProvider;
                                            _getNameMethod = getMethod;
                                            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Using CardNameTextProvider.{getMethod.Name} for name lookup");
                                            return;
                                        }
                                    }
                                }
                            }
                        }

                        // Try LocManager property - use GetLocalizedText with string key
                        var locProp = type.GetProperty("LocManager");
                        if (locProp != null)
                        {
                            var locMgr = locProp.GetValue(mb);
                            if (locMgr != null)
                            {
                                var locType = locMgr.GetType();
                                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"LocManager type: {locType.FullName}");

                                // Try GetLocalizedText with just string parameter
                                var getTextMethod = locType.GetMethod("GetLocalizedText", new[] { typeof(string) });
                                if (getTextMethod == null)
                                {
                                    // Try with array parameter - pass empty array
                                    var allMethods = locType.GetMethods(PublicInstance);
                                    foreach (var m in allMethods)
                                    {
                                        if (m.Name == "GetLocalizedText" && m.GetParameters().Length >= 1)
                                        {
                                            getTextMethod = m;
                                            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Found GetLocalizedText: {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
                                            break;
                                        }
                                    }
                                }

                                if (getTextMethod != null)
                                {
                                    _idNameProvider = locMgr;
                                    _getNameMethod = getTextMethod;
                                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Using LocManager.GetLocalizedText for name lookup");
                                    return;
                                }
                            }
                        }
                        break;
                    }
                }

                // Approach 2: Search for IdNameProvider component in scene
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    var type = mb.GetType();
                    if (type.Name == "IdNameProvider" || type.Name.EndsWith("IdNameProvider"))
                    {
                        var getNameMethod = type.GetMethod("GetName", new[] { typeof(uint), typeof(bool) });
                        if (getNameMethod != null)
                        {
                            _idNameProvider = mb;
                            _getNameMethod = getNameMethod;
                            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Found {type.Name} for name lookup");
                            return;
                        }
                    }
                }

                // Approach 3: Try WrapperController.Instance
                var wrapperType = FindType("WrapperController");
                if (wrapperType != null)
                {
                    var instanceProp = wrapperType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProp != null)
                    {
                        var instance = instanceProp.GetValue(null);
                        if (instance != null)
                        {
                            var cardDbProp = wrapperType.GetProperty("CardDatabase");
                            if (cardDbProp != null)
                            {
                                var cardDb = cardDbProp.GetValue(instance);
                                if (cardDb != null)
                                {
                                    var cardDbType = cardDb.GetType();
                                    var getNameMethod = cardDbType.GetMethod("GetName", new[] { typeof(uint), typeof(bool) });
                                    if (getNameMethod != null)
                                    {
                                        _idNameProvider = cardDb;
                                        _getNameMethod = getNameMethod;
                                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Found WrapperController.CardDatabase for name lookup");
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }

                // Approach 4: Search for any component with CardDatabase or Title/Name provider
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Searching for Meta scene localization providers...");
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    var type = mb.GetType();
                    string typeName = type.Name;

                    // Log interesting types for discovery
                    if (typeName.Contains("Card") || typeName.Contains("Title") ||
                        typeName.Contains("Name") || typeName.Contains("Loc") ||
                        typeName.Contains("Database") || typeName.Contains("Provider") ||
                        typeName.Contains("MetaCardHolder"))
                    {
                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Found potential provider: {typeName}");

                        // Log all properties on ListMetaCardHolder types (for deck list card discovery)
                        if (typeName.Contains("ListMetaCardHolder") && !_listMetaCardHolderLogged)
                        {
                            _listMetaCardHolderLogged = true;
                            LogListMetaCardHolderProperties(mb, type);
                        }

                        // Check for CardDatabase property
                        var cardDbProp = type.GetProperty("CardDatabase");
                        if (cardDbProp != null)
                        {
                            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"{typeName} has CardDatabase property");
                            try
                            {
                                var cardDb = cardDbProp.GetValue(mb);
                                if (cardDb != null)
                                {
                                    var cardDbType = cardDb.GetType();
                                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"CardDatabase type: {cardDbType.FullName}");

                                    // Try CardTitleProvider
                                    var titleProviderProp = cardDbType.GetProperty("CardTitleProvider");
                                    if (titleProviderProp != null)
                                    {
                                        var titleProvider = titleProviderProp.GetValue(cardDb);
                                        if (titleProvider != null)
                                        {
                                            var providerType = titleProvider.GetType();
                                            var getMethod = providerType.GetMethod("GetCardTitle", new[] { typeof(uint), typeof(bool), typeof(string) });
                                            if (getMethod != null)
                                            {
                                                _idNameProvider = titleProvider;
                                                _getNameMethod = getMethod;
                                                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Using {typeName}.CardDatabase.CardTitleProvider for name lookup");
                                                return;
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Error accessing {typeName}.CardDatabase: {ex.Message}");
                            }
                        }
                    }
                }

                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"IdNameProvider not found - will use UI fallback for names");
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Error finding IdNameProvider: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the card name from a GrpId (card database ID) using CardTitleProvider lookup.
        /// Returns null if lookup fails.
        /// </summary>
        public static string GetNameFromGrpId(uint grpId)
        {
            if (grpId == 0)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"GrpId is 0, cannot lookup name");
                return null;
            }

            FindIdNameProvider();

            if (_getNameMethod == null)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"No localization method found for GrpId {grpId}");
                return null;
            }

            try
            {
                var parameters = _getNameMethod.GetParameters();
                object result = null;

                // GetCardTitle(UInt32, Boolean, String) - call with grpId, false, null
                if (parameters.Length == 3 &&
                    parameters[0].ParameterType == typeof(uint) &&
                    parameters[1].ParameterType == typeof(bool))
                {
                    result = _getNameMethod.Invoke(_idNameProvider, new object[] { grpId, false, null });
                }
                else if (parameters.Length == 1)
                {
                    result = _getNameMethod.Invoke(_idNameProvider, new object[] { grpId });
                }
                else if (parameters.Length == 2)
                {
                    // GetLocalizedText(string, ValueTuple[]) - try string key
                    var emptyArray = Array.CreateInstance(parameters[1].ParameterType.GetElementType() ?? typeof(object), 0);
                    result = _getNameMethod.Invoke(_idNameProvider, new object[] { grpId.ToString(), emptyArray });
                }

                string name = result?.ToString();

                // Check if we got a valid result (not null, not empty, not "Unknown Card Title X")
                if (!string.IsNullOrEmpty(name) && !name.StartsWith("$") && !name.StartsWith("Unknown Card Title"))
                {
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"GrpId {grpId} -> Name: {name}");
                    return name;
                }

                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"GrpId {grpId}: No valid name found (result: {name ?? "null"})");
                return null;
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Error getting name from GrpId {grpId}: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Debug Logging

        /// <summary>
        /// Logs all properties available on the Model object for discovery.
        /// Only logs once per session to avoid log spam.
        /// </summary>
        public static void LogModelProperties(object model)
        {
            if (_modelPropertiesLogged || model == null) return;
            _modelPropertiesLogged = true;

            var modelType = model.GetType();
            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"=== MODEL TYPE: {modelType.FullName} ===");

            var properties = modelType.GetProperties(PublicInstance);
            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(model);
                    string valueStr = value?.ToString() ?? "null";
                    if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Property: {prop.Name} = {valueStr} ({prop.PropertyType.Name})");
                }
                catch (Exception ex)
                {
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Property: {prop.Name} = [Error: {ex.Message}] ({prop.PropertyType.Name})");
                }
            }
            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"=== END MODEL PROPERTIES ===");
        }

        /// <summary>
        /// Logs all properties available on an AbilityPrintingData object for discovery.
        /// Only logs once per session to avoid log spam.
        /// </summary>
        public static void LogAbilityProperties(object ability)
        {
            if (_abilityPropertiesLogged || ability == null) return;
            _abilityPropertiesLogged = true;

            var abilityType = ability.GetType();
            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"=== ABILITY TYPE: {abilityType.FullName} ===");

            // Log properties
            var properties = abilityType.GetProperties(PublicInstance);
            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(ability);
                    string valueStr = value?.ToString() ?? "null";
                    if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Ability Property: {prop.Name} = {valueStr} ({prop.PropertyType.Name})");
                }
                catch (Exception ex)
                {
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Ability Property: {prop.Name} = [Error: {ex.Message}] ({prop.PropertyType.Name})");
                }
            }

            // Also log methods that might return text
            var methods = abilityType.GetMethods(PublicInstance);
            foreach (var method in methods)
            {
                // Only log parameterless methods that return string
                if (method.GetParameters().Length == 0 && method.ReturnType == typeof(string))
                {
                    try
                    {
                        var result = method.Invoke(ability, null);
                        string resultStr = result?.ToString() ?? "null";
                        if (resultStr.Length > 100) resultStr = resultStr.Substring(0, 100) + "...";
                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Ability Method: {method.Name}() = {resultStr}");
                    }
                    catch (Exception ex)
                    {
                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Ability Method: {method.Name}() = [Error: {ex.Message}]");
                    }
                }
            }

            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"=== END ABILITY PROPERTIES ===");
        }

        #endregion

        #region Property Helpers

        /// <summary>
        /// Gets a cached PropertyInfo for performance.
        /// </summary>
        private static PropertyInfo GetCachedProperty(Type modelType, string propertyName)
        {
            if (_cachedModelType != modelType)
            {
                _modelPropertyCache.Clear();
                _cachedModelType = modelType;
            }

            if (!_modelPropertyCache.TryGetValue(propertyName, out var prop))
            {
                prop = modelType.GetProperty(propertyName, PublicInstance);
                _modelPropertyCache[propertyName] = prop;
            }
            return prop;
        }

        /// <summary>
        /// Helper to get a property value from the Model, trying multiple property names.
        /// Returns null if none found.
        /// </summary>
        internal static object GetModelPropertyValue(object model, Type modelType, params string[] propertyNames)
        {
            foreach (var name in propertyNames)
            {
                var prop = GetCachedProperty(modelType, name);
                if (prop != null)
                {
                    try
                    {
                        var value = prop.GetValue(model);
                        if (value != null)
                            return value;
                    }
                    catch { /* Property may not exist on all model types */ }
                }
            }
            return null;
        }

        /// <summary>
        /// Helper to get a string property value from the Model.
        /// </summary>
        private static string GetModelStringProperty(object model, Type modelType, params string[] propertyNames)
        {
            var value = GetModelPropertyValue(model, modelType, propertyNames);
            return value?.ToString();
        }

        /// <summary>
        /// Extracts rules text from a CardData's RulesTextOverride property.
        /// Used for modal spell mode cards where Abilities are cleared but RulesTextOverride
        /// contains the mode-specific ability text (AbilityTextOverride).
        /// </summary>
        private static string TryExtractRulesTextOverride(object dataObj, Type objType, uint cardGrpId, uint cardTitleId)
        {
            try
            {
                var rulesOverride = GetModelPropertyValue(dataObj, objType, "RulesTextOverride");
                if (rulesOverride == null) return null;

                var overrideType = rulesOverride.GetType();
                var abilityGrpIdSetField = overrideType.GetField("_abilityGrpIdSet", PrivateInstance);
                if (abilityGrpIdSetField == null) return null;

                var abilityGrpIds = abilityGrpIdSetField.GetValue(rulesOverride) as System.Collections.IList;
                if (abilityGrpIds == null || abilityGrpIds.Count == 0) return null;

                var sourceGrpIdSetField = overrideType.GetField("_sourceGrpIdSet", PrivateInstance);
                var titleIdField = overrideType.GetField("_titleId", PrivateInstance);

                var sourceGrpIds = sourceGrpIdSetField?.GetValue(rulesOverride) as System.Collections.IList;
                uint overrideTitleId = cardTitleId;
                if (titleIdField != null)
                {
                    var tid = titleIdField.GetValue(rulesOverride);
                    if (tid is uint t && t > 0) overrideTitleId = t;
                }

                var rulesLines = new List<string>();
                foreach (var abilityIdObj in abilityGrpIds)
                {
                    if (!(abilityIdObj is uint abilityId) || abilityId == 0) continue;

                    // Try each source GrpId as context until one resolves the ability text.
                    // The game uses a list-based overload internally; we iterate source GrpIds
                    // to find which one provides the correct card context for resolution.
                    string text = null;
                    if (sourceGrpIds != null)
                    {
                        foreach (var srcObj in sourceGrpIds)
                        {
                            if (!(srcObj is uint srcGrpId) || srcGrpId == 0) continue;
                            text = CardTextProvider.GetAbilityTextFromProvider(srcGrpId, abilityId, null, overrideTitleId);
                            if (!string.IsNullOrEmpty(text)) break;
                        }
                    }

                    // Last resort: use the card's own GrpId (the ability ID itself)
                    if (string.IsNullOrEmpty(text))
                        text = CardTextProvider.GetAbilityTextFromProvider(cardGrpId, abilityId, null, overrideTitleId);

                    if (!string.IsNullOrEmpty(text))
                        rulesLines.Add(text);
                }

                if (rulesLines.Count > 0)
                {
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider",
                        $"RulesTextOverride resolved: {rulesLines.Count} abilities for GrpId {cardGrpId}");
                    return string.Join(" ", rulesLines);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CardModelProvider] RulesTextOverride extraction failed: {ex.Message}");
            }

            return null;
        }

        #endregion

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
            text = Regex.Replace(text, @"^((\d*)(?:o([WUBRGCTXSE]))+):", match =>
            {
                string fullCost = match.Groups[1].Value;
                return ParseBareManaSequence(fullCost) + ":";
            });

            // Pattern 3: Standard MTG mana notation {X} (used in ability text from localization DB)
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
        /// Parses a bare mana sequence like "2oW" or "oToRoR" into readable text.
        /// </summary>
        private static string ParseBareManaSequence(string sequence)
        {
            if (string.IsNullOrEmpty(sequence))
                return "";

            var parts = new List<string>();

            // Extract leading number if present (generic mana)
            var numberMatch = Regex.Match(sequence, @"^(\d+)");
            if (numberMatch.Success)
            {
                parts.Add(numberMatch.Groups[1].Value);
                sequence = sequence.Substring(numberMatch.Length);
            }

            // Extract all oX patterns
            var symbolMatches = Regex.Matches(sequence, @"o([WUBRGCTXSE])");
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
        private static string ParseManaQuantityArray(IEnumerable manaQuantities)
        {
            var symbols = new List<string>();
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
                        // Generic/colorless mana - use the Count field value
                        genericCount += count;
                    }
                    else
                    {
                        string symbol = ConvertManaColorToName(colorName);

                        if (isHybrid && altColorProp != null)
                        {
                            var altColor = altColorProp.GetValue(mq);
                            string altColorName = altColor?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(altColorName) && altColorName != colorName)
                            {
                                symbol = $"{symbol} or {ConvertManaColorToName(altColorName)}";
                            }
                        }

                        if (isPhyrexian)
                        {
                            symbol = $"Phyrexian {symbol}";
                        }

                        // Add symbol for each mana of this color (e.g., {UU} = Blue, Blue)
                        for (int i = 0; i < count; i++)
                        {
                            symbols.Add(symbol);
                        }
                    }
                }
                catch { /* Mana quantity reflection may fail on unexpected types */ }
            }

            // Add generic mana count at the beginning if any
            if (genericCount > 0)
            {
                symbols.Insert(0, genericCount.ToString());
            }

            return symbols.Count > 0 ? string.Join(", ", symbols) : null;
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

        #endregion

        #region Power/Toughness

        /// <summary>
        /// Extracts the actual value from a StringBackedInt object.
        /// <summary>
        /// Merges class level-up cost lines with their following effect lines.
        /// Class enchantments produce abilities like: "{o2oW}: Stufe 2" followed by the effect text.
        /// This merges them into a single line: "{o2oW}: Stufe 2. Effect text here."
        /// Detection: line contains mana symbols ({o), has "}: " separator, text after colon is short and ends with digit.
        /// </summary>
        private static void MergeClassLevelLines(List<string> lines)
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

        /// StringBackedInt has: RawText (for "*" etc), Value (int), DefinedValue (nullable int)
        /// </summary>
        private static string FormatRarityName(string rawRarity)
        {
            if (string.IsNullOrEmpty(rawRarity)) return null;
            // CardRarity enum: None, Land, Common, Uncommon, Rare, MythicRare
            if (rawRarity == "None" || rawRarity == "0") return null;
            if (rawRarity == "MythicRare") return "Mythic Rare";
            return rawRarity;
        }

        internal static string GetStringBackedIntValue(object stringBackedInt)
        {
            if (stringBackedInt == null) return null;

            var type = stringBackedInt.GetType();

            // First try RawText - this handles variable P/T like "*"
            var rawTextProp = type.GetProperty("RawText", PublicInstance);
            if (rawTextProp != null)
            {
                try
                {
                    var rawText = rawTextProp.GetValue(stringBackedInt)?.ToString();
                    if (!string.IsNullOrEmpty(rawText))
                        return rawText;
                }
                catch { /* RawText property may not exist on all StringBackedInt variants */ }
            }

            // Then try Value - the numeric value
            var valueProp = type.GetProperty("Value", PublicInstance);
            if (valueProp != null)
            {
                try
                {
                    var val = valueProp.GetValue(stringBackedInt);
                    if (val != null)
                        return val.ToString();
                }
                catch { /* Value property may not exist on all StringBackedInt variants */ }
            }

            return null;
        }

        #endregion

        #region Card Info Extraction

        /// <summary>
        // Cache for PagesMetaCardView display info reflection
        private static FieldInfo _lastDisplayInfoField = null;
        private static bool _lastDisplayInfoFieldSearched = false;
        private static FieldInfo _availableTitleCountField = null;
        private static FieldInfo _usedTitleCountField = null;

        /// <summary>
        /// Extracts collection-specific quantity info from PagesMetaCardView._lastDisplayInfo.
        /// Sets OwnedCount and UsedInDeckCount on the CardInfo if the card is a collection card.
        /// </summary>
        public static void ExtractCollectionQuantity(GameObject cardObj, ref CardInfo info)
        {
            if (cardObj == null) return;

            var metaCardView = GetMetaCardView(cardObj);
            if (metaCardView == null) return;

            // Only applies to PagesMetaCardView (collection grid cards)
            if (metaCardView.GetType().Name != T.PagesMetaCardView) return;

            try
            {
                // Get the _lastDisplayInfo field via reflection (cached)
                if (!_lastDisplayInfoFieldSearched)
                {
                    _lastDisplayInfoFieldSearched = true;
                    _lastDisplayInfoField = metaCardView.GetType().GetField("_lastDisplayInfo",
                        PrivateInstance);
                }

                if (_lastDisplayInfoField == null) return;

                var displayInfo = _lastDisplayInfoField.GetValue(metaCardView);
                if (displayInfo == null) return;

                // Cache the display info field accessors (public fields on PagesMetaCardViewDisplayInformation)
                if (_availableTitleCountField == null)
                {
                    var displayInfoType = displayInfo.GetType();
                    _availableTitleCountField = displayInfoType.GetField("AvailableTitleCount");
                    _usedTitleCountField = displayInfoType.GetField("UsedTitleCount");
                }

                if (_availableTitleCountField != null)
                    info.OwnedCount = (int)(uint)_availableTitleCountField.GetValue(displayInfo);

                if (_usedTitleCountField != null)
                    info.UsedInDeckCount = (int)(uint)_usedTitleCountField.GetValue(displayInfo);
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider",
                    $"Error extracting collection quantity: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts card information from the game's internal Model data.
        /// This works for battlefield cards that may have hidden/compacted UI text.
        /// Also supports Meta scene cards (deck builder, booster, rewards) via MetaCardView.
        /// Returns null if Model data is not available.
        /// </summary>
        // Flag for one-time object name logging
        private static bool _cardObjNameLogged = false;

        public static CardInfo? ExtractCardInfoFromModel(GameObject cardObj)
        {
            if (cardObj == null) return null;

            // Log what object we're processing (once)
            if (!_cardObjNameLogged)
            {
                _cardObjNameLogged = true;
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"ExtractCardInfoFromModel called with: '{cardObj.name}'");
            }

            object model = null;

            // Try DuelScene_CDC first (for duel cards)
            var cdcComponent = GetDuelSceneCDC(cardObj);
            if (cdcComponent != null)
            {
                model = GetCardModel(cdcComponent);
            }

            // Try MetaCardView if no CDC (for deck builder, booster, rewards)
            if (model == null)
            {
                var metaCardView = GetMetaCardView(cardObj);

                // Search up parent hierarchy if not found on this object (deck list TileButton may be nested)
                if (metaCardView == null)
                {
                    var parent = cardObj.transform.parent;
                    int maxLevels = 5; // Limit search depth
                    while (metaCardView == null && parent != null && maxLevels-- > 0)
                    {
                        metaCardView = GetMetaCardView(parent.gameObject);
                        parent = parent.parent;
                    }
                }

                if (metaCardView != null)
                {
                    // Log properties for discovery (once)
                    LogMetaCardViewProperties(metaCardView);

                    model = GetMetaCardModel(metaCardView);

                    // Log result once
                    if (!_metaCardViewPropertiesLogged)
                    {
                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"GetMetaCardModel returned: {(model != null ? model.GetType().Name : "null")}");
                    }

                    // Log the model properties if we found one
                    if (model != null)
                    {
                        LogModelProperties(model);
                    }
                }
            }

            if (model == null) return null;

            // Log properties for discovery (only once)
            LogModelProperties(model);

            return ExtractCardInfoFromObject(model);
        }

        /// <summary>
        /// Extracts card info from any card data object (Model, CardData, CardPrintingData, etc.)
        /// using generic property access. Tries structured properties first (Supertypes/CardTypes/Subtypes,
        /// ManaQuantity[] PrintedCastingCost), falling back to simpler properties (TypeLine, ManaCost string).
        /// This is the shared extraction logic used by both ExtractCardInfoFromModel and store card details.
        /// </summary>
        public static CardInfo ExtractCardInfoFromObject(object dataObj)
        {
            if (dataObj == null) return new CardInfo();

            var info = new CardInfo();
            var objType = dataObj.GetType();

            try
            {
                // Name - try TitleId via GreLocProvider first (guaranteed localized),
                // fall back to CardTitleProvider lookup by GrpId
                uint cardGrpId = 0;
                var grpIdObj = GetModelPropertyValue(dataObj, objType, "GrpId");
                if (grpIdObj is uint grpId)
                    cardGrpId = grpId;

                var titleIdObj = GetModelPropertyValue(dataObj, objType, "TitleId");
                if (titleIdObj is uint titleId && titleId > 0)
                    info.Name = CardTextProvider.GetLocalizedTextById(titleId);

                // Fall back: try Printing.TitleId
                if (string.IsNullOrEmpty(info.Name))
                {
                    var printingForName = GetModelPropertyValue(dataObj, objType, "Printing");
                    if (printingForName != null)
                    {
                        var printingType = printingForName.GetType();
                        var printingTitleId = GetModelPropertyValue(printingForName, printingType, "TitleId");
                        if (printingTitleId is uint ptid && ptid > 0)
                            info.Name = CardTextProvider.GetLocalizedTextById(ptid);
                    }
                }

                // Fall back: CardTitleProvider by GrpId
                if (string.IsNullOrEmpty(info.Name) && cardGrpId > 0)
                    info.Name = GetNameFromGrpId(cardGrpId);

                // Mana Cost - try PrintedCastingCost (ManaQuantity[]) first, fall back to string
                var castingCost = GetModelPropertyValue(dataObj, objType, "PrintedCastingCost");
                if (castingCost != null && castingCost is IEnumerable costEnum && !(castingCost is string))
                {
                    info.ManaCost = ParseManaQuantityArray(costEnum);
                }
                if (string.IsNullOrEmpty(info.ManaCost))
                {
                    var manaCostProp = objType.GetProperty("ManaCost") ?? objType.GetProperty("CastingCost");
                    if (manaCostProp != null)
                    {
                        var manaCostVal = manaCostProp.GetValue(dataObj);
                        if (manaCostVal is string manaStr && !string.IsNullOrEmpty(manaStr))
                            info.ManaCost = ParseManaSymbolsInText(manaStr);
                    }
                }

                // Type Line - try structured Supertypes + CardTypes + Subtypes first
                var typeLineParts = new List<string>();
                bool hasStructuredTypes = false;

                var supertypes = GetModelPropertyValue(dataObj, objType, "Supertypes");
                if (supertypes is IEnumerable superEnum)
                {
                    foreach (var st in superEnum)
                    {
                        if (st != null)
                        {
                            string s = st.ToString();
                            if (s != "None" && !string.IsNullOrEmpty(s))
                            {
                                typeLineParts.Add(s);
                                hasStructuredTypes = true;
                            }
                        }
                    }
                }

                var cardTypes = GetModelPropertyValue(dataObj, objType, "CardTypes");
                if (cardTypes is IEnumerable cardEnum)
                {
                    foreach (var ct in cardEnum)
                    {
                        if (ct != null)
                        {
                            string c = ct.ToString();
                            if (!string.IsNullOrEmpty(c))
                            {
                                typeLineParts.Add(c);
                                hasStructuredTypes = true;
                            }
                        }
                    }
                }

                var subtypeList = new List<string>();
                var subtypes = GetModelPropertyValue(dataObj, objType, "Subtypes");
                if (subtypes is IEnumerable subEnum)
                {
                    foreach (var sub in subEnum)
                    {
                        if (sub != null)
                        {
                            string s = sub.ToString();
                            if (!string.IsNullOrEmpty(s))
                                subtypeList.Add(s);
                        }
                    }
                }

                // Try localized type line via TypeTextId/SubtypeTextId (uses GreLocProvider)
                uint typeTextId = 0;
                uint subtypeTextId = 0;

                // Check directly on model object first
                var typeTextIdVal = GetModelPropertyValue(dataObj, objType, "TypeTextId");
                if (typeTextIdVal is uint ttid) typeTextId = ttid;
                else if (typeTextIdVal is int ttidInt && ttidInt > 0) typeTextId = (uint)ttidInt;

                var subtypeTextIdVal = GetModelPropertyValue(dataObj, objType, "SubtypeTextId");
                if (subtypeTextIdVal is uint stid) subtypeTextId = stid;
                else if (subtypeTextIdVal is int stidInt && stidInt > 0) subtypeTextId = (uint)stidInt;

                // Try Printing sub-object if not found directly
                if (typeTextId == 0)
                {
                    var printingForType = GetModelPropertyValue(dataObj, objType, "Printing");
                    if (printingForType != null)
                    {
                        var printingType = printingForType.GetType();
                        typeTextIdVal = GetModelPropertyValue(printingForType, printingType, "TypeTextId");
                        if (typeTextIdVal is uint pttid) typeTextId = pttid;
                        else if (typeTextIdVal is int pttidInt && pttidInt > 0) typeTextId = (uint)pttidInt;

                        if (subtypeTextId == 0)
                        {
                            subtypeTextIdVal = GetModelPropertyValue(printingForType, printingType, "SubtypeTextId");
                            if (subtypeTextIdVal is uint pstid) subtypeTextId = pstid;
                            else if (subtypeTextIdVal is int pstidInt && pstidInt > 0) subtypeTextId = (uint)pstidInt;
                        }
                    }
                }

                // Look up localized text
                if (typeTextId > 0)
                {
                    var localizedType = CardTextProvider.GetLocalizedTextById(typeTextId);
                    if (!string.IsNullOrEmpty(localizedType))
                    {
                        string localizedSubtype = subtypeTextId > 0 ? CardTextProvider.GetLocalizedTextById(subtypeTextId) : null;
                        info.TypeLine = !string.IsNullOrEmpty(localizedSubtype)
                            ? localizedType + " - " + localizedSubtype
                            : localizedType;
                    }
                }

                // Fall back to structured enum types (English)
                if (string.IsNullOrEmpty(info.TypeLine) && typeLineParts.Count > 0)
                {
                    info.TypeLine = string.Join(" ", typeLineParts);
                    if (subtypeList.Count > 0)
                        info.TypeLine += " - " + string.Join(" ", subtypeList);
                }

                // Fall back to TypeLine/TypeText string property
                if (string.IsNullOrEmpty(info.TypeLine))
                {
                    var typeLineProp = objType.GetProperty("TypeLine") ?? objType.GetProperty("TypeText");
                    if (typeLineProp != null)
                        info.TypeLine = typeLineProp.GetValue(dataObj)?.ToString();
                }

                // Power/Toughness, Loyalty, and Counters
                bool isCreature = false;
                bool isPlaneswalker = false;
                bool isVehicle = subtypeList.Any(s => s.Contains("Vehicle"));
                if (hasStructuredTypes)
                {
                    var cardTypesForPT = GetModelPropertyValue(dataObj, objType, "CardTypes");
                    if (cardTypesForPT is IEnumerable cardTypesEnumPT)
                    {
                        foreach (var ct in cardTypesEnumPT)
                        {
                            if (ct == null) continue;
                            string ctStr = ct.ToString();
                            if (ctStr.Contains("Creature")) isCreature = true;
                            if (ctStr.Contains("Planeswalker")) isPlaneswalker = true;
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(info.TypeLine))
                {
                    isCreature = info.TypeLine.Contains("Creature");
                    isPlaneswalker = info.TypeLine.Contains("Planeswalker");
                }

                var ptParts = new List<string>();

                // Creature or Vehicle P/T (vehicles have P/T even when not crewed)
                if (isCreature || isVehicle)
                {
                    var power = GetModelPropertyValue(dataObj, objType, "Power");
                    var toughness = GetModelPropertyValue(dataObj, objType, "Toughness");
                    if (power != null && toughness != null)
                    {
                        string powerStr = GetStringBackedIntValue(power);
                        string toughStr = GetStringBackedIntValue(toughness);
                        if (powerStr != null && toughStr != null)
                            ptParts.Add($"{powerStr}/{toughStr}");
                    }
                }

                // Planeswalker loyalty
                if (isPlaneswalker)
                {
                    var loyaltyVal = GetModelPropertyValue(dataObj, objType, "Loyalty");
                    if (loyaltyVal != null)
                    {
                        // Loyalty might be uint or StringBackedInt
                        if (loyaltyVal is uint lUint && lUint > 0)
                            ptParts.Add(Strings.Duel_Loyalty((int)lUint));
                        else
                        {
                            string lStr = GetStringBackedIntValue(loyaltyVal);
                            if (!string.IsNullOrEmpty(lStr) && lStr != "0" &&
                                int.TryParse(lStr, out int lInt) && lInt > 0)
                                ptParts.Add(Strings.Duel_Loyalty(lInt));
                        }
                    }
                }

                // Counters (from Instance on battlefield cards)
                try
                {
                    var instance = GetModelInstance(dataObj);
                    if (instance != null)
                    {
                        var instanceType = instance.GetType();
                        var countersProp = instanceType.GetProperty("Counters", PublicInstance);
                        object countersObj = countersProp?.GetValue(instance);
                        if (countersObj == null)
                        {
                            var countersField = instanceType.GetField("Counters", PublicInstance);
                            countersObj = countersField?.GetValue(instance);
                        }
                        if (countersObj is IEnumerable counterEntries)
                        {
                            foreach (var entry in counterEntries)
                            {
                                if (entry == null) continue;
                                var entryType = entry.GetType();
                                var keyProp = entryType.GetProperty("Key");
                                var valueProp = entryType.GetProperty("Value");
                                if (keyProp == null || valueProp == null) continue;
                                var key = keyProp.GetValue(entry);
                                var value = valueProp.GetValue(entry);
                                if (key == null || value == null) continue;
                                // Skip counters already reflected in displayed values
                                var keyStr = key.ToString();
                                if (keyStr == "Loyalty" || keyStr == "P1P1") continue;
                                int count = 0;
                                if (value is int ci) count = ci;
                                else if (int.TryParse(value.ToString(), out int parsed)) count = parsed;
                                if (count > 0)
                                    ptParts.Add($"{count} {CardStateProvider.FormatCounterTypeName(key.ToString())}");
                            }
                        }
                    }
                }
                catch { /* Counter reflection may fail if Instance layout changes */ }

                if (ptParts.Count > 0)
                    info.PowerToughness = string.Join(", ", ptParts);

                // Rules Text - parse from Abilities array
                uint cardTitleId = 0;
                var titleIdVal = GetModelPropertyValue(dataObj, objType, "TitleId");
                if (titleIdVal is uint tid) cardTitleId = tid;

                var abilityIdsVal = GetModelPropertyValue(dataObj, objType, "AbilityIds");
                uint[] abilityIds = null;
                if (abilityIdsVal is IEnumerable<uint> aidEnum)
                    abilityIds = aidEnum.ToArray();
                else if (abilityIdsVal is uint[] aidArray)
                    abilityIds = aidArray;

                var abilities = GetModelPropertyValue(dataObj, objType, "Abilities");
                if (abilities is IEnumerable abilityEnum)
                {
                    var rulesLines = new List<string>();
                    foreach (var ability in abilityEnum)
                    {
                        if (ability == null) continue;

                        LogAbilityProperties(ability);

                        var abilityType = ability.GetType();

                        uint abilityId = 0;
                        var abilityIdProp = abilityType.GetProperty("Id", PublicInstance);
                        if (abilityIdProp != null)
                        {
                            var idVal = abilityIdProp.GetValue(ability);
                            if (idVal is uint aid) abilityId = aid;
                        }

                        var textValue = CardTextProvider.GetAbilityText(ability, abilityType, cardGrpId, abilityId, abilityIds, cardTitleId);
                        if (!string.IsNullOrEmpty(textValue))
                        {
                            // Prefix planeswalker abilities with loyalty cost (e.g., "+2: " or "-3: ")
                            string loyaltyPrefix = CardTextProvider.GetLoyaltyCostPrefix(ability, abilityType);
                            if (loyaltyPrefix != null)
                                textValue = loyaltyPrefix + textValue;
                            rulesLines.Add(textValue);
                        }
                    }

                    if (rulesLines.Count > 0)
                    {
                        MergeClassLevelLines(rulesLines);
                        var parsedLines = rulesLines.Select(line => ParseManaSymbolsInText(line)).ToList();
                        info.RulesLines = parsedLines;
                        info.RulesText = string.Join(" ", parsedLines);
                    }

                    // Cache ability â†’ parent card mapping for ability CDC lookups
                    if (abilityIds != null && abilityIds.Length > 0 && cardGrpId > 0)
                    {
                        foreach (uint aid in abilityIds)
                        {
                            if (aid > 0)
                                _abilityParentCache[aid] = (cardGrpId, abilityIds, cardTitleId);
                        }
                    }
                }

                // Fallback: if no rules text and AbilityIds is empty, the GrpId might BE an ability ID
                // (e.g., planeswalker ability CDCs in SelectCards browser use ability GrpId as their card GrpId)
                // Only attempt if the parent cache confirms this GrpId is a known ability ID
                if (string.IsNullOrEmpty(info.RulesText) && (abilityIds == null || abilityIds.Length == 0) && cardGrpId > 0
                    && _abilityParentCache.TryGetValue(cardGrpId, out var parentInfo))
                {
                    // Try with full parent card context first (handles abilities that reference card name)
                    var text = CardTextProvider.GetAbilityTextFromProvider(parentInfo.cardGrpId, cardGrpId, parentInfo.abilityIds, parentInfo.cardTitleId);
                    // Fall back to self-reference lookup
                    if (string.IsNullOrEmpty(text))
                        text = CardTextProvider.GetAbilityTextFromProvider(cardGrpId, cardGrpId, new uint[]{ cardGrpId }, cardTitleId);
                    if (!string.IsNullOrEmpty(text))
                        info.RulesText = ParseManaSymbolsInText(text);
                }

                // Fallback: RulesTextOverride (modal spell mode cards in RepeatSelection browser)
                // These fake cards have cleared Abilities but a RulesTextOverride with mode-specific text
                if (string.IsNullOrEmpty(info.RulesText))
                {
                    var overrideText = TryExtractRulesTextOverride(dataObj, objType, cardGrpId, cardTitleId);
                    if (!string.IsNullOrEmpty(overrideText))
                        info.RulesText = ParseManaSymbolsInText(overrideText);
                }

                // Flavor Text - lookup via FlavorTextId
                var flavorIdValue = GetModelPropertyValue(dataObj, objType, "FlavorTextId");
                if (flavorIdValue != null)
                {
                    uint flavorId = 0;
                    if (flavorIdValue is uint fid) flavorId = fid;
                    else if (flavorIdValue is int fidInt && fidInt > 0) flavorId = (uint)fidInt;

                    if (flavorId > 0)
                    {
                        var flavorText = CardTextProvider.GetFlavorText(flavorId);
                        if (!string.IsNullOrEmpty(flavorText))
                            info.FlavorText = flavorText;
                    }
                }

                // Rarity & Artist - try Printing sub-object first, then direct properties
                var printing = GetModelPropertyValue(dataObj, objType, "Printing");

                // Rarity - try direct on dataObj first (CardData.Rarity), then Printing.Rarity
                var rarityProp = objType.GetProperty("Rarity");
                if (rarityProp != null)
                {
                    var rarityValue = rarityProp.GetValue(dataObj);
                    if (rarityValue != null)
                        info.Rarity = FormatRarityName(rarityValue.ToString());
                }
                if (string.IsNullOrEmpty(info.Rarity) && printing != null)
                {
                    rarityProp = printing.GetType().GetProperty("Rarity");
                    if (rarityProp != null)
                    {
                        var rarityValue = rarityProp.GetValue(printing);
                        if (rarityValue != null)
                            info.Rarity = FormatRarityName(rarityValue.ToString());
                    }
                }
                object artistSource = printing ?? dataObj;
                var artistSourceType = artistSource.GetType();

                var artistProp = artistSourceType.GetProperty("ArtistCredit") ??
                                 artistSourceType.GetProperty("Artist") ??
                                 artistSourceType.GetProperty("ArtistName");
                if (artistProp != null)
                {
                    var artistValue = artistProp.GetValue(artistSource);
                    if (artistValue is string artistStr && !string.IsNullOrEmpty(artistStr))
                    {
                        info.Artist = artistStr;
                    }
                    else if (artistValue is uint artistId && artistId > 0)
                    {
                        var artistName = CardTextProvider.GetArtistName(artistId);
                        if (!string.IsNullOrEmpty(artistName))
                            info.Artist = artistName;
                    }
                }
                // If Printing path found nothing, try direct on dataObj
                if (string.IsNullOrEmpty(info.Artist) && printing != null)
                {
                    var directArtistProp = objType.GetProperty("ArtistCredit") ??
                                           objType.GetProperty("Artist") ??
                                           objType.GetProperty("ArtistName");
                    if (directArtistProp != null)
                    {
                        var av = directArtistProp.GetValue(dataObj);
                        if (av is string aStr && !string.IsNullOrEmpty(aStr))
                            info.Artist = aStr;
                        else if (av is uint aId && aId > 0)
                        {
                            var an = CardTextProvider.GetArtistName(aId);
                            if (!string.IsNullOrEmpty(an))
                                info.Artist = an;
                        }
                    }
                }

                // Expansion/Set - try direct ExpansionCode, then Printing.ExpansionCode
                var expCode = GetModelPropertyValue(dataObj, objType, "ExpansionCode");
                if (expCode is string expStr && !string.IsNullOrEmpty(expStr))
                {
                    info.SetName = UITextExtractor.MapSetCodeToName(expStr);
                }
                else if (printing != null)
                {
                    var printExpProp = printing.GetType().GetProperty("ExpansionCode");
                    if (printExpProp != null)
                    {
                        var printExpVal = printExpProp.GetValue(printing)?.ToString();
                        if (!string.IsNullOrEmpty(printExpVal))
                        {
                            info.SetName = UITextExtractor.MapSetCodeToName(printExpVal);
                        }
                    }
                }

                info.IsValid = !string.IsNullOrEmpty(info.Name);
                return info;
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Error extracting card data: {ex.Message}");
                return new CardInfo();
            }
        }

        #endregion


        #region Model Instance Access

        private static PropertyInfo _instancePropCached;
        private static bool _instancePropSearched;

        /// <summary>
        /// Gets the Instance sub-object from a card Model via reflection.
        /// </summary>
        internal static object GetModelInstance(object model)
        {
            if (model == null) return null;
            try
            {
                if (!_instancePropSearched)
                {
                    _instancePropSearched = true;
                    _instancePropCached = model.GetType().GetProperty("Instance");
                }
                if (_instancePropCached != null && _instancePropCached.DeclaringType.IsAssignableFrom(model.GetType()))
                {
                    return _instancePropCached.GetValue(model);
                }
                var prop = model.GetType().GetProperty("Instance");
                return prop?.GetValue(model);
            }
            catch { /* Reflection may fail on different game versions */ }
            return null;
        }

        #endregion

        #region Card Data Lookup by GrpId

        /// <summary>
        /// Gets full CardInfo from a GrpId by looking up CardPrintingData from the CardDatabase.
        /// Works in both menu scenes (via deck holder) and duel scenes (via GameManager.CardDatabase).
        /// Returns null if the card cannot be found.
        /// </summary>
        public static CardInfo? GetCardInfoFromGrpId(uint grpId)
        {
            if (grpId == 0) return null;

            try
            {
                // Try menu-scene path first, then fall back to duel-scene path
                var cardData = GetCardDataFromGrpId(grpId) ?? ExtendedCardInfoProvider.GetCardDataFromGrpIdDuelScene(grpId);
                if (cardData == null) return null;

                var info = ExtractCardInfoFromCardData(cardData, grpId);
                if (info.IsValid)
                    return info;
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Error getting card info for GrpId {grpId}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets CardPrintingData from CardDatabase using GrpId.
        /// </summary>
        internal static object GetCardDataFromGrpId(uint grpId)
        {
            try
            {
                // Find CardDatabase - use the cached holder's CardDatabase property
                if (DeckCardProvider.CachedDeckHolder == null)
                    DeckCardProvider.GetDeckListCards(); // This will populate DeckCardProvider.CachedDeckHolder

                if (DeckCardProvider.CachedDeckHolder == null)
                {
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"GetCardDataFromGrpId: DeckCardProvider.CachedDeckHolder is null");
                    return null;
                }

                MonoBehaviour holderComponent = null;
                foreach (var mb in DeckCardProvider.CachedDeckHolder.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name.Contains("ListMetaCardHolder"))
                    {
                        holderComponent = mb;
                        break;
                    }
                }

                if (holderComponent == null)
                {
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"GetCardDataFromGrpId: holderComponent not found");
                    return null;
                }

                var holderType = holderComponent.GetType();
                var cardDbProp = holderType.GetProperty("CardDatabase");
                if (cardDbProp == null)
                {
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"GetCardDataFromGrpId: CardDatabase property not found");
                    return null;
                }

                var cardDb = cardDbProp.GetValue(holderComponent);
                if (cardDb == null)
                {
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"GetCardDataFromGrpId: CardDatabase value is null");
                    return null;
                }

                // Get CardDataProvider from CardDatabase
                var cardDbType = cardDb.GetType();

                // Try to get CardDataProvider
                var cardDataProviderProp = cardDbType.GetProperty("CardDataProvider");
                if (cardDataProviderProp != null)
                {
                    var cardDataProvider = cardDataProviderProp.GetValue(cardDb);
                    if (cardDataProvider != null)
                    {
                        var providerType = cardDataProvider.GetType();

                        // Try GetCardPrintingById(uint id, string skinCode) -> CardPrintingData
                        var getCardPrintingMethod = providerType.GetMethod("GetCardPrintingById", new[] { typeof(uint), typeof(string) });
                        if (getCardPrintingMethod != null)
                        {
                            var result = getCardPrintingMethod.Invoke(cardDataProvider, new object[] { grpId, null });
                            if (result != null)
                            {
                                return result;
                            }
                        }

                        // Try GetCardRecordById(uint id, string skinCode) -> CardPrintingRecord
                        var getCardRecordMethod = providerType.GetMethod("GetCardRecordById", new[] { typeof(uint), typeof(string) });
                        if (getCardRecordMethod != null)
                        {
                            var result = getCardRecordMethod.Invoke(cardDataProvider, new object[] { grpId, null });
                            if (result != null)
                            {
                                return result;
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Error getting card data for GrpId {grpId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts CardInfo from CardPrintingData object.
        /// </summary>
        public static CardInfo ExtractCardInfoFromCardData(object cardData, uint grpId)
        {
            var info = new CardInfo { IsValid = true };

            try
            {
                var cardType = cardData.GetType();

                // Name - try TitleId via GreLocProvider first, fall back to CardTitleProvider
                var titleIdProp = cardType.GetProperty("TitleId");
                if (titleIdProp != null)
                {
                    var titleIdVal = titleIdProp.GetValue(cardData);
                    uint titleId = 0;
                    if (titleIdVal is uint tid) titleId = tid;
                    else if (titleIdVal is int tidInt && tidInt > 0) titleId = (uint)tidInt;
                    if (titleId > 0)
                        info.Name = CardTextProvider.GetLocalizedTextById(titleId);
                }
                if (string.IsNullOrEmpty(info.Name))
                    info.Name = GetNameFromGrpId(grpId);

                // TypeLine - try localized lookup via TypeTextId/SubtypeTextId first
                var typeTextIdProp = cardType.GetProperty("TypeTextId");
                var subtypeTextIdProp = cardType.GetProperty("SubtypeTextId");
                if (typeTextIdProp != null)
                {
                    var typeTextIdVal = typeTextIdProp.GetValue(cardData);
                    uint typeTextId = 0;
                    if (typeTextIdVal is uint ttid) typeTextId = ttid;
                    else if (typeTextIdVal is int ttidInt && ttidInt > 0) typeTextId = (uint)ttidInt;

                    if (typeTextId > 0)
                    {
                        var localizedType = CardTextProvider.GetLocalizedTextById(typeTextId);
                        if (!string.IsNullOrEmpty(localizedType))
                        {
                            info.TypeLine = localizedType;

                            if (subtypeTextIdProp != null)
                            {
                                var subtypeTextIdVal = subtypeTextIdProp.GetValue(cardData);
                                uint subtypeTextId = 0;
                                if (subtypeTextIdVal is uint stid) subtypeTextId = stid;
                                else if (subtypeTextIdVal is int stidInt && stidInt > 0) subtypeTextId = (uint)stidInt;

                                if (subtypeTextId > 0)
                                {
                                    var localizedSubtype = CardTextProvider.GetLocalizedTextById(subtypeTextId);
                                    if (!string.IsNullOrEmpty(localizedSubtype))
                                        info.TypeLine += " - " + localizedSubtype;
                                }
                            }
                        }
                    }
                }

                // Fall back to TypeLine/TypeText string property
                if (string.IsNullOrEmpty(info.TypeLine))
                {
                    var typeLineProp = cardType.GetProperty("TypeLine") ?? cardType.GetProperty("TypeText");
                    if (typeLineProp != null)
                    {
                        info.TypeLine = typeLineProp.GetValue(cardData)?.ToString();
                    }
                }

                // ManaCost - try structured PrintedCastingCost first (same as MODEL extraction)
                var castingCostProp = cardType.GetProperty("PrintedCastingCost") ?? cardType.GetProperty("ManaCost") ?? cardType.GetProperty("CastingCost");
                if (castingCostProp != null)
                {
                    var castingCostValue = castingCostProp.GetValue(cardData);
                    if (castingCostValue is IEnumerable castingCostEnum && !(castingCostValue is string))
                    {
                        // It's an array/collection - parse it
                        info.ManaCost = ParseManaQuantityArray(castingCostEnum);
                    }
                    else if (castingCostValue is string castingCostStr && !string.IsNullOrEmpty(castingCostStr))
                    {
                        // It's a string - parse symbols
                        info.ManaCost = ParseManaSymbolsInText(castingCostStr);
                    }
                }

                // Power/Toughness and Loyalty
                // Only extract P/T for creatures and vehicles (same guard as duel scene extraction)
                bool isCreatureCard = false;
                bool isPlaneswalkerCard = false;
                bool isVehicleCard = false;
                var cardTypesEnum = cardType.GetProperty("CardTypes")?.GetValue(cardData) as IEnumerable;
                if (cardTypesEnum != null)
                {
                    foreach (var ct in cardTypesEnum)
                    {
                        if (ct == null) continue;
                        string ctStr = ct.ToString();
                        if (ctStr.Contains("Creature")) isCreatureCard = true;
                        if (ctStr.Contains("Planeswalker")) isPlaneswalkerCard = true;
                    }
                }
                var subtypesEnum = cardType.GetProperty("Subtypes")?.GetValue(cardData) as IEnumerable;
                if (subtypesEnum != null)
                {
                    foreach (var st in subtypesEnum)
                    {
                        if (st != null && st.ToString().Contains("Vehicle"))
                            isVehicleCard = true;
                    }
                }

                var ptParts2 = new List<string>();
                if (isCreatureCard || isVehicleCard)
                {
                    var powerProp = cardType.GetProperty("Power");
                    var toughnessProp = cardType.GetProperty("Toughness");
                    if (powerProp != null && toughnessProp != null)
                    {
                        var power = powerProp.GetValue(cardData);
                        var toughness = toughnessProp.GetValue(cardData);
                        if (power != null && toughness != null)
                        {
                            string powerStr = GetStringBackedIntValue(power);
                            string toughStr = GetStringBackedIntValue(toughness);
                            if (!string.IsNullOrEmpty(powerStr) && !string.IsNullOrEmpty(toughStr))
                                ptParts2.Add($"{powerStr}/{toughStr}");
                        }
                    }
                }

                // Planeswalker loyalty (from CardPrintingData)
                var loyaltyProp2 = isPlaneswalkerCard ? cardType.GetProperty("Loyalty") : null;
                if (loyaltyProp2 != null)
                {
                    var loyaltyVal = loyaltyProp2.GetValue(cardData);
                    if (loyaltyVal != null)
                    {
                        if (loyaltyVal is uint lUint && lUint > 0)
                            ptParts2.Add(Strings.Duel_Loyalty((int)lUint));
                        else
                        {
                            string lStr = GetStringBackedIntValue(loyaltyVal);
                            if (!string.IsNullOrEmpty(lStr) && lStr != "0" &&
                                int.TryParse(lStr, out int lInt) && lInt > 0)
                                ptParts2.Add(Strings.Duel_Loyalty(lInt));
                        }
                    }
                }

                if (ptParts2.Count > 0)
                    info.PowerToughness = string.Join(", ", ptParts2);

                // Rules text - try Abilities property
                var abilitiesProp = cardType.GetProperty("Abilities") ?? cardType.GetProperty("IntrinsicAbilities");
                if (abilitiesProp != null)
                {
                    var abilities = abilitiesProp.GetValue(cardData) as System.Collections.IEnumerable;
                    if (abilities != null)
                    {
                        var rulesTexts = new List<string>();
                        foreach (var ability in abilities)
                        {
                            if (ability == null) continue;
                            var abilityType = ability.GetType();

                            // Try to get ability ID and look up text
                            var idProp = abilityType.GetProperty("Id");
                            if (idProp != null)
                            {
                                var abilityId = (uint)idProp.GetValue(ability);
                                var abilityText = CardTextProvider.GetAbilityTextFromProvider(grpId, abilityId, null, 0);
                                if (!string.IsNullOrEmpty(abilityText))
                                {
                                    // Prefix planeswalker abilities with loyalty cost
                                    string loyaltyPrefix = CardTextProvider.GetLoyaltyCostPrefix(ability, abilityType);
                                    if (loyaltyPrefix != null)
                                        abilityText = loyaltyPrefix + abilityText;
                                    rulesTexts.Add(abilityText);
                                }
                            }
                        }

                        if (rulesTexts.Count > 0)
                        {
                            MergeClassLevelLines(rulesTexts);
                            var parsedLines = rulesTexts.Select(line => ParseManaSymbolsInText(line)).ToList();
                            info.RulesLines = parsedLines;
                            info.RulesText = string.Join(" ", parsedLines);
                        }
                    }
                }

                // FlavorText
                var flavorIdProp = cardType.GetProperty("FlavorTextId");
                if (flavorIdProp != null)
                {
                    var flavorId = (uint)flavorIdProp.GetValue(cardData);
                    if (flavorId != 0)
                    {
                        info.FlavorText = CardTextProvider.GetFlavorText(flavorId);
                    }
                }

                // Rarity - CardPrintingData should have rarity directly
                var rarityProp = cardType.GetProperty("Rarity");
                if (rarityProp != null)
                {
                    var rarityValue = rarityProp.GetValue(cardData);
                    if (rarityValue != null)
                        info.Rarity = FormatRarityName(rarityValue.ToString());
                }

                // Artist - CardPrintingData should have artist info directly
                var artistProp = cardType.GetProperty("ArtistCredit") ??
                                 cardType.GetProperty("Artist") ??
                                 cardType.GetProperty("ArtistName");
                if (artistProp != null)
                {
                    var artistValue = artistProp.GetValue(cardData);
                    if (artistValue is string artistStr && !string.IsNullOrEmpty(artistStr))
                    {
                        info.Artist = artistStr;
                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Extracted artist from CardData: {info.Artist}");
                    }
                    else if (artistValue is uint artistId && artistId > 0)
                    {
                        var artistName = CardTextProvider.GetArtistName(artistId);
                        if (!string.IsNullOrEmpty(artistName))
                        {
                            info.Artist = artistName;
                            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Extracted artist by ID: {info.Artist}");
                        }
                    }
                }

                // Expansion/Set
                var expCodeProp = cardType.GetProperty("ExpansionCode");
                if (expCodeProp != null)
                {
                    var expVal = expCodeProp.GetValue(cardData)?.ToString();
                    if (!string.IsNullOrEmpty(expVal))
                        info.SetName = UITextExtractor.MapSetCodeToName(expVal);
                }
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Error extracting card info from data: {ex.Message}");
            }

            return info;
        }

        #endregion

    }
}
