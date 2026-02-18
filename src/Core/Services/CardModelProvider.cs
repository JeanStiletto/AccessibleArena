using UnityEngine;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Provides access to card Model data from the game's internal systems.
    /// Handles reflection-based property access, name lookups, and card categorization.
    /// Use CardDetector for card detection (IsCard, GetCardRoot).
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

        // Cache for ability text provider
        private static object _abilityTextProvider = null;
        private static MethodInfo _getAbilityTextMethod = null;
        private static bool _abilityTextProviderSearched = false;

        // Cache for flavor text provider
        private static object _flavorTextProvider = null;
        private static MethodInfo _getFlavorTextMethod = null;
        private static bool _flavorTextProviderSearched = false;

        // Cache for artist provider
        private static object _artistProvider = null;
        private static MethodInfo _getArtistMethod = null;
        private static bool _artistProviderSearched = false;

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
            _abilityTextProvider = null;
            _getAbilityTextMethod = null;
            _abilityTextProviderSearched = false;
            _flavorTextProvider = null;
            _getFlavorTextMethod = null;
            _flavorTextProviderSearched = false;
            _artistProvider = null;
            _getArtistMethod = null;
            _artistProviderSearched = false;
            // Combat state cache
            _instancePropCached = null;
            _instancePropSearched = false;
            _isAttackingProp = null;
            _isAttackingPropSearched = false;
            _isBlockingProp = null;
            _isBlockingPropSearched = false;
            _isTappedField = null;
            _isTappedFieldSearched = false;
            _blockingIdsField = null;
            _blockingIdsFieldSearched = false;
            _blockedByIdsField = null;
            _blockedByIdsFieldSearched = false;
            // Targeting cache
            _targetIdsField = null;
            _targetIdsFieldSearched = false;
            _targetedByIdsField = null;
            _targetedByIdsFieldSearched = false;
            // Extended info cache (keywords + linked faces)
            ClearExtendedInfoCache();
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
                if (typeName == "DuelScene_CDC" || typeName == "Meta_CDC")
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
                if (typeName == "Meta_CDC")
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
                if (typeName == "PagesMetaCardView" ||
                    typeName == "MetaCardView" ||
                    typeName == "BoosterMetaCardView" ||
                    typeName.Contains("ListMetaCardView"))
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
        public static object GetCardModel(Component cdcComponent)
        {
            if (cdcComponent == null) return null;

            try
            {
                var cdcType = cdcComponent.GetType();
                var modelProp = cdcType.GetProperty("Model");
                return modelProp?.GetValue(cdcComponent);
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
            var properties = viewType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
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
            var properties = holderType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
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
            var methods = holderType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
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
            var fields = holderType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
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
                                foreach (var prop in itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                                {
                                    try
                                    {
                                        var val = prop.GetValue(item);
                                        string valStr = val?.ToString() ?? "null";
                                        if (valStr.Length > 80) valStr = valStr.Substring(0, 80) + "...";
                                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"      {prop.Name} = {valStr} ({prop.PropertyType.Name})");
                                    }
                                    catch { }
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
                                        foreach (var m in providerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
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
                                        foreach (var m in providerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
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
                                    var allMethods = locType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
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
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var wrapperType = assembly.GetType("WrapperController");
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
                        break;
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

            var properties = modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
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
            var properties = abilityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
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
            var methods = abilityType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
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

        #region Ability Text

        /// <summary>
        /// Tries to extract text from an ability object by checking common property/method names.
        /// Returns null if no text could be extracted.
        /// </summary>
        private static string GetAbilityText(object ability, Type abilityType, uint cardGrpId, uint abilityId, uint[] abilityIds, uint cardTitleId)
        {
            // First try to look up via AbilityTextProvider with full card context
            var text = GetAbilityTextFromProvider(cardGrpId, abilityId, abilityIds, cardTitleId);
            if (!string.IsNullOrEmpty(text))
                return text;

            // Try common property names for ability text
            string[] propertyNames = { "Text", "RulesText", "AbilityText", "TextContent", "Description" };
            foreach (var propName in propertyNames)
            {
                var prop = abilityType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    try
                    {
                        var value = prop.GetValue(ability);
                        if (value != null)
                        {
                            string propText = value.ToString();
                            if (!string.IsNullOrEmpty(propText))
                                return propText;
                        }
                    }
                    catch { }
                }
            }

            // Try GetText() method (ICardTextEntry interface)
            var getTextMethod = abilityType.GetMethod("GetText", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (getTextMethod != null && getTextMethod.ReturnType == typeof(string))
            {
                try
                {
                    var result = getTextMethod.Invoke(ability, null);
                    if (result != null)
                    {
                        string methodText = result.ToString();
                        if (!string.IsNullOrEmpty(methodText))
                            return methodText;
                    }
                }
                catch { }
            }

            return null;
        }

        /// <summary>
        /// Tries to get ability text using the AbilityTextProvider with full card context.
        /// Method signature: GetAbilityTextByCardAbilityGrpId(cardGrpId, abilityGrpId, abilityIds, cardTitleId, overrideLanguageCode, formatted)
        /// </summary>
        private static string GetAbilityTextFromProvider(uint cardGrpId, uint abilityId, uint[] abilityIds, uint cardTitleId)
        {
            // Try to find the ability text provider - retry if not found
            if (!_abilityTextProviderSearched || _getAbilityTextMethod == null)
            {
                _abilityTextProviderSearched = true;
                FindAbilityTextProvider();
            }

            if (_getAbilityTextMethod == null || _abilityTextProvider == null)
            {
                return null;
            }

            try
            {
                // GetAbilityTextByCardAbilityGrpId(UInt32 cardGrpId, UInt32 abilityGrpId, IEnumerable<uint> abilityIds, UInt32 cardTitleId, String overrideLanguageCode, Boolean formatted)
                var parameters = _getAbilityTextMethod.GetParameters();
                object result = null;

                if (parameters.Length == 6)
                {
                    // Full signature
                    IEnumerable<uint> abilityIdsList = abilityIds ?? Array.Empty<uint>();
                    result = _getAbilityTextMethod.Invoke(_abilityTextProvider, new object[] {
                        cardGrpId,
                        abilityId,
                        abilityIdsList,
                        cardTitleId,
                        null,   // overrideLanguageCode
                        false   // formatted
                    });
                }
                else if (parameters.Length >= 1 && parameters[0].ParameterType == typeof(uint))
                {
                    // Fallback for simpler method signatures
                    result = _getAbilityTextMethod.Invoke(_abilityTextProvider, new object[] { abilityId });
                }

                string text = result?.ToString();
                if (!string.IsNullOrEmpty(text) && !text.StartsWith("$") && !text.Contains("Unknown"))
                {
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Ability {abilityId} -> {text}");
                    return text;
                }
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Error looking up ability {abilityId}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Searches for the ability text provider in the game.
        /// </summary>
        private static void FindAbilityTextProvider()
        {
            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Searching for ability text provider...");

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                var type = mb.GetType();
                if (type.Name == "GameManager")
                {
                    // Try CardDatabase -> AbilityTextProvider
                    var cardDbProp = type.GetProperty("CardDatabase");
                    if (cardDbProp != null)
                    {
                        var cardDb = cardDbProp.GetValue(mb);
                        if (cardDb != null)
                        {
                            var cardDbType = cardDb.GetType();

                            // List all properties on CardDatabase to find text providers
                            foreach (var prop in cardDbType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                            {
                                if (prop.Name.Contains("Text") || prop.Name.Contains("Ability"))
                                {
                                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"CardDatabase.{prop.Name} ({prop.PropertyType.Name})");

                                    var provider = prop.GetValue(cardDb);
                                    if (provider != null)
                                    {
                                        var providerType = provider.GetType();
                                        foreach (var m in providerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                        {
                                            if (m.DeclaringType == typeof(object)) continue;
                                            var paramStr = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                                            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"  {m.Name}({paramStr}) -> {m.ReturnType.Name}");

                                            // Look for methods that take uint and return string
                                            if (m.ReturnType == typeof(string))
                                            {
                                                var mParams = m.GetParameters();
                                                if (mParams.Length >= 1 && mParams[0].ParameterType == typeof(uint))
                                                {
                                                    _abilityTextProvider = provider;
                                                    _getAbilityTextMethod = m;
                                                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Using {prop.Name}.{m.Name} for ability text lookup");
                                                    return;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;
                }
            }

            // Search for other components that might have CardDatabase (Meta scenes)
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;
                var type = mb.GetType();
                string typeName = type.Name;

                // Look for components that might have CardDatabase
                if (typeName.Contains("Card") || typeName.Contains("Wrapper") || typeName.Contains("Manager"))
                {
                    var cardDbProp = type.GetProperty("CardDatabase");
                    if (cardDbProp != null)
                    {
                        try
                        {
                            var cardDb = cardDbProp.GetValue(mb);
                            if (cardDb != null)
                            {
                                var cardDbType = cardDb.GetType();

                                // Look for AbilityTextProvider
                                foreach (var prop in cardDbType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                                {
                                    if (prop.Name.Contains("Text") || prop.Name.Contains("Ability"))
                                    {
                                        var provider = prop.GetValue(cardDb);
                                        if (provider != null)
                                        {
                                            var providerType = provider.GetType();
                                            foreach (var m in providerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                            {
                                                if (m.DeclaringType == typeof(object)) continue;
                                                if (m.ReturnType == typeof(string))
                                                {
                                                    var mParams = m.GetParameters();
                                                    if (mParams.Length >= 1 && mParams[0].ParameterType == typeof(uint))
                                                    {
                                                        _abilityTextProvider = provider;
                                                        _getAbilityTextMethod = m;
                                                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Using {typeName}.CardDatabase.{prop.Name}.{m.Name} for ability text lookup");
                                                        return;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }

            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"No ability text provider found");
        }

        /// <summary>
        /// Searches for the flavor text provider in the game.
        /// FlavorTextId is a localization key that needs to be looked up via GreLocProvider or ClientLocProvider.
        /// </summary>
        private static void FindFlavorTextProvider()
        {
            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Searching for flavor text provider...");

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                var type = mb.GetType();
                if (type.Name == "GameManager")
                {
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Found GameManager, looking for CardDatabase...");
                    var cardDbProp = type.GetProperty("CardDatabase");
                    if (cardDbProp != null)
                    {
                        var cardDb = cardDbProp.GetValue(mb);
                        if (cardDb != null)
                        {
                            var cardDbType = cardDb.GetType();

                            // Try GreLocProvider first - this is for GRE (game rules engine) content like flavor text
                            var greLocProp = cardDbType.GetProperty("GreLocProvider");
                            if (greLocProp != null)
                            {
                                var greLocProvider = greLocProp.GetValue(cardDb);
                                if (greLocProvider != null)
                                {
                                    var providerType = greLocProvider.GetType();
                                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Found GreLocProvider: {providerType.FullName}");

                                    // Log all methods
                                    foreach (var m in providerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                    {
                                        if (m.DeclaringType == typeof(object)) continue;
                                        var paramStr = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"  GreLocProvider.{m.Name}({paramStr}) -> {m.ReturnType.Name}");

                                        // Look for GetString, GetText, or similar methods
                                        if (m.ReturnType == typeof(string) &&
                                            (m.Name == "GetString" || m.Name == "GetText" || m.Name == "Get" || m.Name.Contains("Loc")))
                                        {
                                            var mParams = m.GetParameters();
                                            if (mParams.Length >= 1 && mParams[0].ParameterType == typeof(uint))
                                            {
                                                _flavorTextProvider = greLocProvider;
                                                _getFlavorTextMethod = m;
                                                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Using GreLocProvider.{m.Name} for flavor text lookup");
                                                return;
                                            }
                                        }
                                    }
                                }
                            }

                            // Try ClientLocProvider as fallback
                            var clientLocProp = cardDbType.GetProperty("ClientLocProvider");
                            if (clientLocProp != null)
                            {
                                var clientLocProvider = clientLocProp.GetValue(cardDb);
                                if (clientLocProvider != null)
                                {
                                    var providerType = clientLocProvider.GetType();
                                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Found ClientLocProvider: {providerType.FullName}");

                                    // Log all methods
                                    foreach (var m in providerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                    {
                                        if (m.DeclaringType == typeof(object)) continue;
                                        var paramStr = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"  ClientLocProvider.{m.Name}({paramStr}) -> {m.ReturnType.Name}");

                                        // Look for GetString, GetText methods
                                        if (m.ReturnType == typeof(string))
                                        {
                                            var mParams = m.GetParameters();
                                            if (mParams.Length >= 1 && mParams[0].ParameterType == typeof(uint))
                                            {
                                                _flavorTextProvider = clientLocProvider;
                                                _getFlavorTextMethod = m;
                                                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Using ClientLocProvider.{m.Name} for flavor text lookup");
                                                return;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;
                }
            }

            // Search for other components that might have CardDatabase (Meta scenes)
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;
                var type = mb.GetType();
                string typeName = type.Name;

                // Look for components that might have CardDatabase
                if (typeName.Contains("Card") || typeName.Contains("Wrapper") || typeName.Contains("Manager"))
                {
                    var cardDbProp = type.GetProperty("CardDatabase");
                    if (cardDbProp != null)
                    {
                        try
                        {
                            var cardDb = cardDbProp.GetValue(mb);
                            if (cardDb != null)
                            {
                                var cardDbType = cardDb.GetType();

                                // Try GreLocProvider first
                                var greLocProp = cardDbType.GetProperty("GreLocProvider");
                                if (greLocProp != null)
                                {
                                    var greLocProvider = greLocProp.GetValue(cardDb);
                                    if (greLocProvider != null)
                                    {
                                        var providerType = greLocProvider.GetType();
                                        foreach (var m in providerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                        {
                                            if (m.DeclaringType == typeof(object)) continue;
                                            if (m.ReturnType == typeof(string) &&
                                                (m.Name == "GetString" || m.Name == "GetText" || m.Name == "Get" || m.Name.Contains("Loc")))
                                            {
                                                var mParams = m.GetParameters();
                                                if (mParams.Length >= 1 && mParams[0].ParameterType == typeof(uint))
                                                {
                                                    _flavorTextProvider = greLocProvider;
                                                    _getFlavorTextMethod = m;
                                                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Using {typeName}.CardDatabase.GreLocProvider.{m.Name} for flavor text lookup");
                                                    return;
                                                }
                                            }
                                        }
                                    }
                                }

                                // Try ClientLocProvider as fallback
                                var clientLocProp = cardDbType.GetProperty("ClientLocProvider");
                                if (clientLocProp != null)
                                {
                                    var clientLocProvider = clientLocProp.GetValue(cardDb);
                                    if (clientLocProvider != null)
                                    {
                                        var providerType = clientLocProvider.GetType();
                                        foreach (var m in providerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                        {
                                            if (m.DeclaringType == typeof(object)) continue;
                                            if (m.ReturnType == typeof(string))
                                            {
                                                var mParams = m.GetParameters();
                                                if (mParams.Length >= 1 && mParams[0].ParameterType == typeof(uint))
                                                {
                                                    _flavorTextProvider = clientLocProvider;
                                                    _getFlavorTextMethod = m;
                                                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Using {typeName}.CardDatabase.ClientLocProvider.{m.Name} for flavor text lookup");
                                                    return;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }

            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"No flavor text provider found");
        }

        /// <summary>
        /// Searches for the artist provider in the game.
        /// </summary>
        private static void FindArtistProvider()
        {
            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Searching for artist provider...");

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                var type = mb.GetType();
                if (type.Name == "GameManager")
                {
                    var cardDbProp = type.GetProperty("CardDatabase");
                    if (cardDbProp != null)
                    {
                        var cardDb = cardDbProp.GetValue(mb);
                        if (cardDb != null)
                        {
                            var cardDbType = cardDb.GetType();

                            // Look for properties containing "Artist"
                            foreach (var prop in cardDbType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                            {
                                if (prop.Name.Contains("Artist"))
                                {
                                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"CardDatabase.{prop.Name} ({prop.PropertyType.Name})");

                                    var provider = prop.GetValue(cardDb);
                                    if (provider != null)
                                    {
                                        var providerType = provider.GetType();
                                        foreach (var m in providerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                        {
                                            if (m.DeclaringType == typeof(object)) continue;

                                            // Look for methods that take uint and return string
                                            if (m.ReturnType == typeof(string))
                                            {
                                                var mParams = m.GetParameters();
                                                if (mParams.Length >= 1 && mParams[0].ParameterType == typeof(uint))
                                                {
                                                    _artistProvider = provider;
                                                    _getArtistMethod = m;
                                                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Using {prop.Name}.{m.Name} for artist lookup");
                                                    return;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;
                }
            }

            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"No artist provider found");
        }

        /// <summary>
        /// Gets the flavor text for a card using its FlavorId.
        /// Uses GreLocProvider.GetLocalizedText(locId, overrideLangCode, formatted).
        /// </summary>
        private static string GetFlavorText(uint flavorId)
        {
            if (flavorId == 0 || flavorId == 1) return null; // 1 appears to be a placeholder for "no flavor text"

            if (!_flavorTextProviderSearched)
            {
                _flavorTextProviderSearched = true;
                FindFlavorTextProvider();
            }

            if (_flavorTextProvider == null || _getFlavorTextMethod == null)
                return null;

            try
            {
                var parameters = _getFlavorTextMethod.GetParameters();
                object result;

                if (parameters.Length == 3)
                {
                    // GetLocalizedText(UInt32 locId, String overrideLangCode, Boolean formatted)
                    result = _getFlavorTextMethod.Invoke(_flavorTextProvider, new object[] { flavorId, null, false });
                }
                else if (parameters.Length == 1)
                {
                    result = _getFlavorTextMethod.Invoke(_flavorTextProvider, new object[] { flavorId });
                }
                else
                {
                    return null;
                }

                var text = result as string;
                if (!string.IsNullOrEmpty(text) && !text.StartsWith("$") && !text.Contains("Unknown"))
                {
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Flavor text found: {text}");
                    return text;
                }
                return null;
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Error getting flavor text for id {flavorId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the artist name for a card using its ArtistId.
        /// </summary>
        private static string GetArtistName(uint artistId)
        {
            if (artistId == 0) return null;

            if (!_artistProviderSearched)
            {
                _artistProviderSearched = true;
                FindArtistProvider();
            }

            if (_artistProvider == null || _getArtistMethod == null)
                return null;

            try
            {
                var text = _getArtistMethod.Invoke(_artistProvider, new object[] { artistId }) as string;
                return string.IsNullOrEmpty(text) ? null : text;
            }
            catch
            {
                return null;
            }
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
                prop = modelType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                _modelPropertyCache[propertyName] = prop;
            }
            return prop;
        }

        /// <summary>
        /// Helper to get a property value from the Model, trying multiple property names.
        /// Returns null if none found.
        /// </summary>
        private static object GetModelPropertyValue(object model, Type modelType, params string[] propertyNames)
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
                    catch { }
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

            // Pattern 1: {oX} format with curly braces
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
                var countField = mqType.GetField("Count", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

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
                catch { }
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
                case "White": case "W": return "White";
                case "Blue": case "U": return "Blue";
                case "Black": case "B": return "Black";
                case "Red": case "R": return "Red";
                case "Green": case "G": return "Green";
                case "Colorless": case "C": return "Colorless";
                case "Generic": return "Generic";
                case "Snow": case "S": return "Snow";
                case "Phyrexian": case "P": return "Phyrexian";
                case "X": return "X";
                default: return colorEnum;
            }
        }

        #endregion

        #region Power/Toughness

        /// <summary>
        /// Extracts the actual value from a StringBackedInt object.
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

        private static string GetStringBackedIntValue(object stringBackedInt)
        {
            if (stringBackedInt == null) return null;

            var type = stringBackedInt.GetType();

            // First try RawText - this handles variable P/T like "*"
            var rawTextProp = type.GetProperty("RawText", BindingFlags.Public | BindingFlags.Instance);
            if (rawTextProp != null)
            {
                try
                {
                    var rawText = rawTextProp.GetValue(stringBackedInt)?.ToString();
                    if (!string.IsNullOrEmpty(rawText))
                        return rawText;
                }
                catch { }
            }

            // Then try Value - the numeric value
            var valueProp = type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
            if (valueProp != null)
            {
                try
                {
                    var val = valueProp.GetValue(stringBackedInt);
                    if (val != null)
                        return val.ToString();
                }
                catch { }
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
            if (metaCardView.GetType().Name != "PagesMetaCardView") return;

            try
            {
                // Get the _lastDisplayInfo field via reflection (cached)
                if (!_lastDisplayInfoFieldSearched)
                {
                    _lastDisplayInfoFieldSearched = true;
                    _lastDisplayInfoField = metaCardView.GetType().GetField("_lastDisplayInfo",
                        BindingFlags.NonPublic | BindingFlags.Instance);
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
                // Name - get from GrpId using CardTitleProvider lookup
                uint cardGrpId = 0;
                var grpIdObj = GetModelPropertyValue(dataObj, objType, "GrpId");
                if (grpIdObj is uint grpId)
                {
                    cardGrpId = grpId;
                    info.Name = GetNameFromGrpId(grpId);
                }

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

                if (typeLineParts.Count > 0)
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

                // Power and Toughness - only for creatures
                bool isCreature = false;
                if (hasStructuredTypes)
                {
                    var cardTypesForPT = GetModelPropertyValue(dataObj, objType, "CardTypes");
                    if (cardTypesForPT is IEnumerable cardTypesEnumPT)
                    {
                        foreach (var ct in cardTypesEnumPT)
                        {
                            if (ct != null && ct.ToString().Contains("Creature"))
                            {
                                isCreature = true;
                                break;
                            }
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(info.TypeLine))
                {
                    isCreature = info.TypeLine.Contains("Creature");
                }

                if (isCreature)
                {
                    var power = GetModelPropertyValue(dataObj, objType, "Power");
                    var toughness = GetModelPropertyValue(dataObj, objType, "Toughness");
                    if (power != null && toughness != null)
                    {
                        string powerStr = GetStringBackedIntValue(power);
                        string toughStr = GetStringBackedIntValue(toughness);
                        if (powerStr != null && toughStr != null)
                        {
                            info.PowerToughness = $"{powerStr}/{toughStr}";
                        }
                    }
                }

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
                        var abilityIdProp = abilityType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
                        if (abilityIdProp != null)
                        {
                            var idVal = abilityIdProp.GetValue(ability);
                            if (idVal is uint aid) abilityId = aid;
                        }

                        var textValue = GetAbilityText(ability, abilityType, cardGrpId, abilityId, abilityIds, cardTitleId);
                        if (!string.IsNullOrEmpty(textValue))
                        {
                            rulesLines.Add(textValue);
                        }
                    }

                    if (rulesLines.Count > 0)
                    {
                        string rawRulesText = string.Join(" ", rulesLines);
                        info.RulesText = ParseManaSymbolsInText(rawRulesText);
                    }
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
                        var flavorText = GetFlavorText(flavorId);
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
                        var artistName = GetArtistName(artistId);
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
                            var an = GetArtistName(aId);
                            if (!string.IsNullOrEmpty(an))
                                info.Artist = an;
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

        #region Attachments

        /// <summary>
        /// Gets the AttachedToId from a card's Model.Instance via reflection.
        /// This is the InstanceId of the card this card is attached to (0 if not attached).
        /// Used by the game's UniversalBattlefieldStack to track attachment relationships.
        /// </summary>
        private static FieldInfo _attachedToIdField;
        private static bool _attachedToIdFieldSearched;

        public static uint GetAttachedToId(object model)
        {
            var instance = GetModelInstance(model);
            if (instance == null) return 0;
            try
            {
                // Cache the FieldInfo - AttachedToId is a field on MtgCardInstance, not a property
                if (!_attachedToIdFieldSearched)
                {
                    _attachedToIdFieldSearched = true;
                    _attachedToIdField = instance.GetType().GetField("AttachedToId",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                if (_attachedToIdField != null)
                {
                    var val = _attachedToIdField.GetValue(instance);
                    if (val is uint id) return id;
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Gets the InstanceId from a card's model.
        /// </summary>
        private static uint GetModelInstanceId(object model)
        {
            if (model == null) return 0;
            try
            {
                var prop = model.GetType().GetProperty("InstanceId");
                if (prop != null)
                {
                    var val = prop.GetValue(model);
                    if (val is uint id) return id;
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Gets the GrpId from a card's model.
        /// </summary>
        private static uint GetModelGrpId(object model)
        {
            if (model == null) return 0;
            try
            {
                var prop = model.GetType().GetProperty("GrpId");
                if (prop != null)
                {
                    var val = prop.GetValue(model);
                    if (val is uint id) return id;
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Collects all DuelScene_CDC card models from the battlefield.
        /// Returns list of (model, instanceId, grpId) for scanning attachment relationships.
        /// </summary>
        private static List<(object model, uint instanceId, uint grpId)> GetAllBattlefieldCardModels()
        {
            var results = new List<(object model, uint instanceId, uint grpId)>();

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;
                if (!go.name.Contains("BattlefieldCardHolder")) continue;

                foreach (var child in go.GetComponentsInChildren<Transform>(true))
                {
                    if (child == null || !child.gameObject.activeInHierarchy) continue;

                    var cdc = GetDuelSceneCDC(child.gameObject);
                    if (cdc == null) continue;

                    var model = GetCardModel(cdc);
                    if (model == null) continue;

                    uint instanceId = GetModelInstanceId(model);
                    if (instanceId == 0) continue;

                    uint grpId = GetModelGrpId(model);
                    results.Add((model, instanceId, grpId));
                }
                break; // Only one battlefield holder needed
            }

            return results;
        }

        /// <summary>
        /// Gets the list of cards attached to this card (enchantments, equipment, etc.).
        /// Scans all battlefield cards and checks whose Instance.AttachedToId matches this card's InstanceId.
        /// </summary>
        public static List<(uint instanceId, uint grpId, string name)> GetAttachments(GameObject card)
        {
            var attachments = new List<(uint instanceId, uint grpId, string name)>();
            if (card == null) return attachments;

            var cdcComponent = GetDuelSceneCDC(card);
            if (cdcComponent == null) return attachments;

            var model = GetCardModel(cdcComponent);
            if (model == null) return attachments;

            uint myInstanceId = GetModelInstanceId(model);
            if (myInstanceId == 0) return attachments;

            try
            {
                var allCards = GetAllBattlefieldCardModels();
                foreach (var (cardModel, instanceId, grpId) in allCards)
                {
                    if (instanceId == myInstanceId) continue; // Skip self

                    uint attachedToId = GetAttachedToId(cardModel);
                    if (attachedToId == myInstanceId)
                    {
                        string name = grpId > 0 ? GetNameFromGrpId(grpId) : null;
                        attachments.Add((instanceId, grpId, name));
                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider",
                            $"Found attachment: {name ?? "unknown"} (InstanceId={instanceId}) attached to InstanceId={myInstanceId}");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Error getting attachments: {ex.Message}");
            }

            return attachments;
        }

        /// <summary>
        /// Gets the card that this card is attached to (for auras/equipment).
        /// Uses Model.Instance.AttachedToId to find the parent card on the battlefield.
        /// </summary>
        public static (uint instanceId, uint grpId, string name)? GetAttachedTo(GameObject card)
        {
            if (card == null) return null;

            var cdcComponent = GetDuelSceneCDC(card);
            if (cdcComponent == null) return null;

            var model = GetCardModel(cdcComponent);
            if (model == null) return null;

            try
            {
                uint attachedToId = GetAttachedToId(model);
                if (attachedToId == 0) return null;

                // Find the parent card on the battlefield by its InstanceId
                var allCards = GetAllBattlefieldCardModels();
                foreach (var (cardModel, instanceId, grpId) in allCards)
                {
                    if (instanceId == attachedToId)
                    {
                        string name = grpId > 0 ? GetNameFromGrpId(grpId) : null;
                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider",
                            $"Card is attached to: {name ?? "unknown"} (InstanceId={instanceId}, GrpId={grpId})");
                        return (instanceId, grpId, name);
                    }
                }

                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider",
                    $"AttachedToId={attachedToId} but parent card not found on battlefield");
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Error getting attached-to info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets formatted attachment text for announcing a card.
        /// Returns text describing what's attached to this card AND what this card is attached to.
        /// </summary>
        public static string GetAttachmentText(GameObject card)
        {
            var result = new List<string>();

            // Check what's attached TO this card (enchantments, equipment on a creature)
            var attachments = GetAttachments(card);
            if (attachments.Count > 0)
            {
                var names = attachments
                    .Select(a => a.name ?? "unknown card")
                    .ToList();

                if (names.Count == 1)
                {
                    result.Add(Models.Strings.Card_EnchantedBy(names[0]));
                }
                else
                {
                    result.Add(Models.Strings.Card_EnchantedBy(string.Join(", ", names)));
                }
            }

            // Check if this card IS attached to something (aura/equipment itself)
            var attachedTo = GetAttachedTo(card);
            if (attachedTo.HasValue && !string.IsNullOrEmpty(attachedTo.Value.name))
            {
                result.Add(Models.Strings.Card_AttachedTo(attachedTo.Value.name));
            }

            if (result.Count == 0) return "";
            return ", " + string.Join(", ", result);
        }

        #endregion

        #region Combat State

        // Cache for combat state reflection
        private static PropertyInfo _instancePropCached;
        private static bool _instancePropSearched;
        private static PropertyInfo _isAttackingProp;
        private static bool _isAttackingPropSearched;
        private static PropertyInfo _isBlockingProp;
        private static bool _isBlockingPropSearched;
        private static FieldInfo _isTappedField;
        private static bool _isTappedFieldSearched;
        private static FieldInfo _blockingIdsField;
        private static bool _blockingIdsFieldSearched;
        private static FieldInfo _blockedByIdsField;
        private static bool _blockedByIdsFieldSearched;

        /// <summary>
        /// Gets the Instance object from a card model via cached reflection.
        /// This is the MtgCardInstance that holds combat state fields.
        /// </summary>
        private static object GetModelInstance(object model)
        {
            if (model == null) return null;
            try
            {
                if (!_instancePropSearched)
                {
                    _instancePropSearched = true;
                    _instancePropCached = model.GetType().GetProperty("Instance");
                }
                // If cached type doesn't match (different model type), look up directly
                if (_instancePropCached != null && _instancePropCached.DeclaringType.IsAssignableFrom(model.GetType()))
                {
                    return _instancePropCached.GetValue(model);
                }
                // Fallback: direct lookup
                var prop = model.GetType().GetProperty("Instance");
                return prop?.GetValue(model);
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Returns true if the card model's Instance.IsAttacking property is true.
        /// </summary>
        public static bool GetIsAttacking(object model)
        {
            var instance = GetModelInstance(model);
            if (instance == null) return false;
            try
            {
                if (!_isAttackingPropSearched)
                {
                    _isAttackingPropSearched = true;
                    _isAttackingProp = instance.GetType().GetProperty("IsAttacking",
                        BindingFlags.Public | BindingFlags.Instance);
                }
                if (_isAttackingProp != null)
                {
                    var val = _isAttackingProp.GetValue(instance);
                    if (val is bool b) return b;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Returns true if the card model's Instance.IsBlocking property is true.
        /// </summary>
        public static bool GetIsBlocking(object model)
        {
            var instance = GetModelInstance(model);
            if (instance == null) return false;
            try
            {
                if (!_isBlockingPropSearched)
                {
                    _isBlockingPropSearched = true;
                    _isBlockingProp = instance.GetType().GetProperty("IsBlocking",
                        BindingFlags.Public | BindingFlags.Instance);
                }
                if (_isBlockingProp != null)
                {
                    var val = _isBlockingProp.GetValue(instance);
                    if (val is bool b) return b;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Gets the list of attacker InstanceIds that this blocker is blocking.
        /// Reads Instance.BlockingIds field (List of uint).
        /// </summary>
        public static List<uint> GetBlockingIds(object model)
        {
            var result = new List<uint>();
            var instance = GetModelInstance(model);
            if (instance == null) return result;
            try
            {
                if (!_blockingIdsFieldSearched)
                {
                    _blockingIdsFieldSearched = true;
                    _blockingIdsField = instance.GetType().GetField("BlockingIds",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (_blockingIdsField != null)
                {
                    var val = _blockingIdsField.GetValue(instance);
                    if (val is IList list)
                    {
                        foreach (var item in list)
                        {
                            if (item is uint id) result.Add(id);
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        /// <summary>
        /// Gets the list of blocker InstanceIds blocking this attacker.
        /// Reads Instance.BlockedByIds field (List of uint).
        /// </summary>
        public static List<uint> GetBlockedByIds(object model)
        {
            var result = new List<uint>();
            var instance = GetModelInstance(model);
            if (instance == null) return result;
            try
            {
                if (!_blockedByIdsFieldSearched)
                {
                    _blockedByIdsFieldSearched = true;
                    _blockedByIdsField = instance.GetType().GetField("BlockedByIds",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (_blockedByIdsField != null)
                {
                    var val = _blockedByIdsField.GetValue(instance);
                    if (val is IList list)
                    {
                        foreach (var item in list)
                        {
                            if (item is uint id) result.Add(id);
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        /// <summary>
        /// Resolves an InstanceId to a card name by scanning all battlefield cards.
        /// Returns null if not found.
        /// </summary>
        public static string ResolveInstanceIdToName(uint instanceId)
        {
            if (instanceId == 0) return null;
            try
            {
                var allCards = GetAllBattlefieldCardModels();
                foreach (var (cardModel, id, grpId) in allCards)
                {
                    if (id == instanceId)
                    {
                        return grpId > 0 ? GetNameFromGrpId(grpId) : null;
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Returns true if the card model's Instance.IsTapped field is true.
        /// Note: IsTapped is a public FIELD on MtgCardInstance, not a property.
        /// </summary>
        public static bool GetIsTapped(object model)
        {
            var instance = GetModelInstance(model);
            if (instance == null) return false;
            try
            {
                if (!_isTappedFieldSearched)
                {
                    _isTappedFieldSearched = true;
                    _isTappedField = instance.GetType().GetField("IsTapped",
                        BindingFlags.Public | BindingFlags.Instance);
                }
                if (_isTappedField != null)
                {
                    var val = _isTappedField.GetValue(instance);
                    if (val is bool b) return b;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Checks if a card GameObject is tapped, using model data.
        /// Chains: GetDuelSceneCDC → GetCardModel → GetIsTapped.
        /// </summary>
        public static bool GetIsTappedFromCard(GameObject card)
        {
            if (card == null) return false;
            var cdc = GetDuelSceneCDC(card);
            if (cdc == null) return false;
            var model = GetCardModel(cdc);
            return GetIsTapped(model);
        }

        /// <summary>
        /// Checks if a card GameObject is attacking, using model data.
        /// Chains: GetDuelSceneCDC → GetCardModel → GetIsAttacking.
        /// </summary>
        public static bool GetIsAttackingFromCard(GameObject card)
        {
            if (card == null) return false;
            var cdc = GetDuelSceneCDC(card);
            if (cdc == null) return false;
            var model = GetCardModel(cdc);
            return GetIsAttacking(model);
        }

        /// <summary>
        /// Checks if a card GameObject is blocking, using model data.
        /// Chains: GetDuelSceneCDC → GetCardModel → GetIsBlocking.
        /// </summary>
        public static bool GetIsBlockingFromCard(GameObject card)
        {
            if (card == null) return false;
            var cdc = GetDuelSceneCDC(card);
            if (cdc == null) return false;
            var model = GetCardModel(cdc);
            return GetIsBlocking(model);
        }

        #endregion

        #region Targeting

        // Cache for targeting reflection
        private static FieldInfo _targetIdsField;
        private static bool _targetIdsFieldSearched;
        private static FieldInfo _targetedByIdsField;
        private static bool _targetedByIdsFieldSearched;

        /// <summary>
        /// Gets the list of InstanceIds this card is targeting.
        /// Reads Instance.TargetIds field (List of uint).
        /// </summary>
        public static List<uint> GetTargetIds(object model)
        {
            var result = new List<uint>();
            var instance = GetModelInstance(model);
            if (instance == null) return result;
            try
            {
                if (!_targetIdsFieldSearched)
                {
                    _targetIdsFieldSearched = true;
                    _targetIdsField = instance.GetType().GetField("TargetIds",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (_targetIdsField != null)
                {
                    var val = _targetIdsField.GetValue(instance);
                    if (val is IList list)
                    {
                        foreach (var item in list)
                        {
                            if (item is uint id) result.Add(id);
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        /// <summary>
        /// Gets the list of InstanceIds of cards targeting this card.
        /// Reads Instance.TargetedByIds field (List of uint) from MtgEntity.
        /// </summary>
        public static List<uint> GetTargetedByIds(object model)
        {
            var result = new List<uint>();
            var instance = GetModelInstance(model);
            if (instance == null) return result;
            try
            {
                if (!_targetedByIdsFieldSearched)
                {
                    _targetedByIdsFieldSearched = true;
                    _targetedByIdsField = instance.GetType().GetField("TargetedByIds",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (_targetedByIdsField != null)
                {
                    var val = _targetedByIdsField.GetValue(instance);
                    if (val is IList list)
                    {
                        foreach (var item in list)
                        {
                            if (item is uint id) result.Add(id);
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        /// <summary>
        /// Collects all DuelScene_CDC card models from the stack zone.
        /// Returns list of (model, instanceId, grpId) for name resolution.
        /// </summary>
        private static List<(object model, uint instanceId, uint grpId)> GetAllStackCardModels()
        {
            var results = new List<(object model, uint instanceId, uint grpId)>();

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;
                if (!go.name.Contains("StackCardHolder")) continue;

                foreach (var child in go.GetComponentsInChildren<Transform>(true))
                {
                    if (child == null || !child.gameObject.activeInHierarchy) continue;

                    var cdc = GetDuelSceneCDC(child.gameObject);
                    if (cdc == null) continue;

                    var model = GetCardModel(cdc);
                    if (model == null) continue;

                    uint instanceId = GetModelInstanceId(model);
                    if (instanceId == 0) continue;

                    uint grpId = GetModelGrpId(model);
                    results.Add((model, instanceId, grpId));
                }
                break;
            }

            return results;
        }

        /// <summary>
        /// Resolves an InstanceId to a card name by scanning battlefield and stack cards.
        /// Returns null if not found.
        /// </summary>
        public static string ResolveInstanceIdToNameExtended(uint instanceId)
        {
            if (instanceId == 0) return null;

            // Try battlefield first
            string name = ResolveInstanceIdToName(instanceId);
            if (name != null) return name;

            // Try stack
            try
            {
                var stackCards = GetAllStackCardModels();
                foreach (var (cardModel, id, grpId) in stackCards)
                {
                    if (id == instanceId)
                    {
                        return grpId > 0 ? GetNameFromGrpId(grpId) : null;
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Gets formatted targeting text for announcing a card.
        /// Returns text describing what this card targets and what targets it.
        /// Format: ", targeting X, targeted by Y" (with leading ", " like GetAttachmentText).
        /// </summary>
        public static string GetTargetingText(GameObject card)
        {
            if (card == null) return "";

            var cdcComponent = GetDuelSceneCDC(card);
            if (cdcComponent == null) return "";

            var model = GetCardModel(cdcComponent);
            if (model == null) return "";

            var result = new List<string>();

            try
            {
                // Check what this card targets
                var targetIds = GetTargetIds(model);
                if (targetIds.Count > 0)
                {
                    var names = new List<string>();
                    foreach (var id in targetIds)
                    {
                        string name = ResolveInstanceIdToNameExtended(id);
                        if (!string.IsNullOrEmpty(name))
                            names.Add(name);
                    }
                    if (names.Count == 1)
                    {
                        result.Add(Models.Strings.Card_Targeting(names[0]));
                    }
                    else if (names.Count == 2)
                    {
                        result.Add(Models.Strings.Card_TargetingTwo(names[0], names[1]));
                    }
                    else if (names.Count > 2)
                    {
                        result.Add(Models.Strings.Card_TargetingMany(string.Join(", ", names)));
                    }
                }

                // Check what targets this card
                var targetedByIds = GetTargetedByIds(model);
                if (targetedByIds.Count > 0)
                {
                    var names = new List<string>();
                    foreach (var id in targetedByIds)
                    {
                        string name = ResolveInstanceIdToNameExtended(id);
                        if (!string.IsNullOrEmpty(name))
                            names.Add(name);
                    }
                    if (names.Count == 1)
                    {
                        result.Add(Models.Strings.Card_TargetedBy(names[0]));
                    }
                    else if (names.Count == 2)
                    {
                        result.Add(Models.Strings.Card_TargetedByTwo(names[0], names[1]));
                    }
                    else if (names.Count > 2)
                    {
                        result.Add(Models.Strings.Card_TargetedByMany(string.Join(", ", names)));
                    }
                }
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Error getting targeting text: {ex.Message}");
            }

            if (result.Count == 0) return "";
            return ", " + string.Join(", ", result);
        }

        #endregion

        #region Card Categorization

        /// <summary>
        /// Checks if a card on the stack is a triggered or activated ability rather than a spell.
        /// Returns: (isAbility, isTriggered) - isTriggered distinguishes triggered vs activated.
        /// Language-agnostic: checks CardTypes enum values, not localized type line.
        /// </summary>
        public static (bool isAbility, bool isTriggered) IsAbilityOnStack(GameObject cardObj)
        {
            if (cardObj == null) return (false, false);

            var cdcComponent = GetDuelSceneCDC(cardObj);
            if (cdcComponent == null) return (false, false);

            var model = GetCardModel(cdcComponent);
            if (model == null) return (false, false);

            var modelType = model.GetType();

            // Log model properties once to discover ability-specific fields
            LogModelProperties(model);

            // Check CardTypes - spells have Instant, Sorcery, Creature, etc.
            // Abilities on the stack won't have these standard spell types
            var cardTypes = GetModelPropertyValue(model, modelType, "CardTypes") as IEnumerable;
            if (cardTypes != null)
            {
                bool hasSpellType = false;
                bool hasAbilityType = false;

                foreach (var ct in cardTypes)
                {
                    if (ct == null) continue;
                    string typeStr = ct.ToString();

                    // Check for standard spell types (language-agnostic enum values)
                    if (typeStr == "Instant" || typeStr == "Sorcery" ||
                        typeStr == "Creature" || typeStr == "Artifact" ||
                        typeStr == "Enchantment" || typeStr == "Planeswalker" ||
                        typeStr == "Land" || typeStr == "Battle" ||
                        typeStr == "Kindred")
                    {
                        hasSpellType = true;
                    }

                    // Check for Ability type
                    if (typeStr == "Ability" || typeStr.Contains("Ability"))
                    {
                        hasAbilityType = true;
                    }
                }

                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"IsAbilityOnStack: hasSpellType={hasSpellType}, hasAbilityType={hasAbilityType}");

                // If has explicit Ability type or no spell types, it's an ability
                if (hasAbilityType || !hasSpellType)
                {
                    // Try to determine if triggered vs activated
                    // Check for AbilityType, TriggerType, or similar properties
                    var abilityType = GetModelPropertyValue(model, modelType, "AbilityType");
                    var triggerType = GetModelPropertyValue(model, modelType, "TriggerType");
                    var abilityCategory = GetModelPropertyValue(model, modelType, "AbilityCategory");

                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Ability properties: AbilityType={abilityType}, TriggerType={triggerType}, AbilityCategory={abilityCategory}");

                    if (abilityType != null)
                    {
                        string typeVal = abilityType.ToString();
                        bool isTriggered = typeVal.Contains("Trigger") || typeVal.Contains("Triggered");
                        return (true, isTriggered);
                    }

                    if (triggerType != null)
                    {
                        // If TriggerType exists and is not None/null, it's a triggered ability
                        string triggerVal = triggerType.ToString();
                        bool isTriggered = !string.IsNullOrEmpty(triggerVal) && triggerVal != "None";
                        return (true, isTriggered);
                    }

                    if (abilityCategory != null)
                    {
                        string categoryVal = abilityCategory.ToString();
                        bool isTriggered = categoryVal.Contains("Trigger") || categoryVal.Contains("Triggered");
                        return (true, isTriggered);
                    }

                    // Fallback: if no spell types found, assume triggered ability
                    // (most common case for things going on stack automatically)
                    return (true, true);
                }
            }

            return (false, false);
        }

        /// <summary>
        /// Gets card category info (creature, land, opponent) in a single Model lookup.
        /// More efficient than calling IsCreatureCard/IsLandCard/IsOpponentCard separately.
        /// </summary>
        public static (bool isCreature, bool isLand, bool isOpponent) GetCardCategory(GameObject card)
        {
            if (card == null) return (false, false, false);

            bool isCreature = false;
            bool isLand = false;
            bool isOpponent = false;

            var cdcComponent = GetDuelSceneCDC(card);
            if (cdcComponent != null)
            {
                var model = GetCardModel(cdcComponent);
                if (model != null)
                {
                    try
                    {
                        var modelType = model.GetType();

                        // Check ownership from ControllerNum
                        var controllerProp = modelType.GetProperty("ControllerNum");
                        if (controllerProp != null)
                        {
                            var controller = controllerProp.GetValue(model);
                            isOpponent = controller?.ToString() == "Opponent";
                        }

                        // Check IsBasicLand property
                        var isBasicLandProp = modelType.GetProperty("IsBasicLand");
                        if (isBasicLandProp != null && (bool)isBasicLandProp.GetValue(model))
                            isLand = true;

                        // Check IsLandButNotBasic property
                        if (!isLand)
                        {
                            var isLandNotBasicProp = modelType.GetProperty("IsLandButNotBasic");
                            if (isLandNotBasicProp != null && (bool)isLandNotBasicProp.GetValue(model))
                                isLand = true;
                        }

                        // Check CardTypes for Creature and Land
                        var cardTypesProp = modelType.GetProperty("CardTypes");
                        if (cardTypesProp != null)
                        {
                            var cardTypes = cardTypesProp.GetValue(model) as IEnumerable;
                            if (cardTypes != null)
                            {
                                foreach (var cardType in cardTypes)
                                {
                                    string typeStr = cardType?.ToString() ?? "";
                                    if (typeStr == "Creature" || typeStr.Contains("Creature"))
                                        isCreature = true;
                                    if (typeStr == "Land" || typeStr.Contains("Land"))
                                        isLand = true;
                                }
                            }
                        }

                        return (isCreature, isLand, isOpponent);
                    }
                    catch (Exception ex)
                    {
                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Error in GetCardCategory: {ex.Message}");
                    }
                }
            }

            // Fallback for ownership if Model not available
            isOpponent = IsOpponentCardFallback(card);
            return (isCreature, isLand, isOpponent);
        }

        /// <summary>
        /// Checks if a card is a creature based on its CardTypes from the Model.
        /// For single checks only - use GetCardCategory() when checking multiple properties.
        /// </summary>
        public static bool IsCreatureCard(GameObject card)
        {
            return GetCardCategory(card).isCreature;
        }

        /// <summary>
        /// Checks if a card is a land based on its CardTypes or IsBasicLand/IsLandButNotBasic from the Model.
        /// For single checks only - use GetCardCategory() when checking multiple properties.
        /// </summary>
        public static bool IsLandCard(GameObject card)
        {
            return GetCardCategory(card).isLand;
        }

        /// <summary>
        /// Checks if a card belongs to the opponent.
        /// For single checks only - use GetCardCategory() when checking multiple properties.
        /// </summary>
        public static bool IsOpponentCard(GameObject card)
        {
            return GetCardCategory(card).isOpponent;
        }

        /// <summary>
        /// Fallback method to determine opponent ownership via hierarchy/position.
        /// Used when Model is not available.
        /// </summary>
        private static bool IsOpponentCardFallback(GameObject card)
        {
            if (card == null) return false;

            // Check parent hierarchy for ownership indicators
            // Only use "local"/"opponent" markers, not hardcoded player numbers
            // (local player could be player 1 or 2 depending on game state)
            Transform current = card.transform;
            while (current != null)
            {
                string name = current.name.ToLower();
                if (name.Contains("opponent"))
                    return true;
                if (name.Contains("local"))
                    return false;

                current = current.parent;
            }

            // Final fallback: Check screen position (top 60% = opponent)
            Vector3 screenPos = Camera.main?.WorldToScreenPoint(card.transform.position) ?? Vector3.zero;
            if (screenPos != Vector3.zero)
            {
                return screenPos.y > Screen.height * 0.6f;
            }

            return false;
        }

        #endregion

        #region Deck List Card Support

        /// <summary>
        /// Information about a card in the deck list (MainDeck_MetaCardHolder).
        /// </summary>
        public struct DeckListCardInfo
        {
            public uint GrpId;
            public int Quantity;
            public GameObject TileButton;
            public GameObject TagButton;
            public GameObject CardTileBase;
            public GameObject ViewGameObject; // The ListMetaCardView_Expanding's gameObject
            public bool IsValid => GrpId != 0;
        }

        // Cache for deck list cards to avoid repeated reflection
        private static List<DeckListCardInfo> _cachedDeckListCards = new List<DeckListCardInfo>();
        private static GameObject _cachedDeckHolder = null;
        private static int _cachedDeckListFrame = -1;

        /// <summary>
        /// Clears the deck list card cache, forcing a fresh lookup on next call.
        /// Call this when entering the DeckBuilderDeckList group to ensure fresh data.
        /// </summary>
        public static void ClearDeckListCache()
        {
            _cachedDeckListCards.Clear();
            _cachedDeckHolder = null;
            _cachedDeckListFrame = -1;
        }

        /// <summary>
        /// Gets the full hierarchy path of a transform for debugging.
        /// </summary>
        private static string GetTransformPath(Transform t)
        {
            if (t == null) return "null";
            var path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        /// <summary>
        /// Gets all cards from the MainDeck_MetaCardHolder with their GrpIds and quantities.
        /// Uses caching to avoid repeated reflection calls within the same frame.
        /// </summary>
        public static List<DeckListCardInfo> GetDeckListCards()
        {
            // Return cached result if same frame
            if (_cachedDeckListFrame == Time.frameCount && _cachedDeckListCards.Count > 0)
                return _cachedDeckListCards;

            _cachedDeckListCards.Clear();
            _cachedDeckListFrame = Time.frameCount;

            try
            {
                // Find MainDeck_MetaCardHolder
                // Note: GameObject.Find only finds active objects, but the deck holder may be inactive
                // when entering deck builder without a popup dialog. In that case, we search for it
                // including inactive objects and activate it.
                var deckHolder = GameObject.Find("MainDeck_MetaCardHolder");
                if (deckHolder == null)
                {
                    // Search for inactive holder
                    var allTransforms = GameObject.FindObjectsOfType<Transform>(true);
                    foreach (var t in allTransforms)
                    {
                        if (t.name == "MainDeck_MetaCardHolder")
                        {
                            deckHolder = t.gameObject;
                            // Activate the holder so we can access its components
                            deckHolder.SetActive(true);
                            break;
                        }
                    }

                    if (deckHolder == null)
                    {
                        return _cachedDeckListCards;
                    }
                }

                _cachedDeckHolder = deckHolder;

                // Find ListMetaCardHolder_Expanding component
                MonoBehaviour holderComponent = null;
                foreach (var mb in deckHolder.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name.Contains("ListMetaCardHolder"))
                    {
                        holderComponent = mb;
                        break;
                    }
                }

                if (holderComponent == null)
                {
                    return _cachedDeckListCards;
                }

                // Get CardViews property
                var holderType = holderComponent.GetType();
                var cardViewsProp = holderType.GetProperty("CardViews");
                if (cardViewsProp == null)
                {
                    return _cachedDeckListCards;
                }

                var cardViews = cardViewsProp.GetValue(holderComponent) as System.Collections.IEnumerable;
                if (cardViews == null)
                {
                    return _cachedDeckListCards;
                }

                // Extract card info from each ListMetaCardView_Expanding
                foreach (var cardView in cardViews)
                {
                    if (cardView == null) continue;

                    var viewType = cardView.GetType();
                    var info = new DeckListCardInfo();

                    // Store the view's gameObject for hierarchy checks
                    if (cardView is Component viewComponent)
                    {
                        info.ViewGameObject = viewComponent.gameObject;
                    }

                    // Get Card property which has GrpId
                    var cardProp = viewType.GetProperty("Card");
                    if (cardProp != null)
                    {
                        var card = cardProp.GetValue(cardView);
                        if (card != null)
                        {
                            var cardType = card.GetType();
                            var grpIdProp = cardType.GetProperty("GrpId");
                            if (grpIdProp != null)
                            {
                                info.GrpId = (uint)grpIdProp.GetValue(card);
                            }
                        }
                    }

                    // Get Quantity
                    var qtyProp = viewType.GetProperty("Quantity");
                    if (qtyProp != null)
                    {
                        info.Quantity = (int)qtyProp.GetValue(cardView);
                    }

                    // Get TileButton (the card name button)
                    var tileBtnProp = viewType.GetProperty("TileButton");
                    if (tileBtnProp != null)
                    {
                        var tileBtn = tileBtnProp.GetValue(cardView) as Component;
                        if (tileBtn != null)
                        {
                            info.TileButton = tileBtn.gameObject;
                        }
                    }

                    // Get TagButton (the quantity button)
                    var tagBtnProp = viewType.GetProperty("TagButton");
                    if (tagBtnProp != null)
                    {
                        var tagBtn = tagBtnProp.GetValue(cardView) as Component;
                        if (tagBtn != null)
                        {
                            info.TagButton = tagBtn.gameObject;
                        }
                    }

                    // Get the CardTile_Base parent via CanvasGroup or transform
                    var canvasGroupProp = viewType.GetProperty("CanvasGroup");
                    if (canvasGroupProp != null)
                    {
                        var canvasGroup = canvasGroupProp.GetValue(cardView) as Component;
                        if (canvasGroup != null)
                        {
                            info.CardTileBase = canvasGroup.gameObject;
                        }
                    }

                    if (info.IsValid)
                    {
                        _cachedDeckListCards.Add(info);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Error getting deck list cards: {ex.Message}");
            }

            return _cachedDeckListCards;
        }

        /// <summary>
        /// Gets deck list card info for a specific UI element (TileButton, TagButton, or CardTileBase).
        /// Returns null if the element is not a deck list card.
        /// </summary>
        public static DeckListCardInfo? GetDeckListCardInfo(GameObject element)
        {
            if (element == null) return null;

            // Check if this element is part of a deck card tile
            // It could be the TileButton, TagButton, or CardTileBase itself
            var deckCards = GetDeckListCards();

            foreach (var card in deckCards)
            {
                if (card.TileButton == element ||
                    card.TagButton == element ||
                    card.CardTileBase == element ||
                    card.ViewGameObject == element)
                {
                    return card;
                }

                // Check if element is a child of ViewGameObject (the ListMetaCardView_Expanding's gameObject)
                if (card.ViewGameObject != null && element.transform.IsChildOf(card.ViewGameObject.transform))
                {
                    return card;
                }

                // Also check if element is a child of the CardTileBase (fallback)
                if (card.CardTileBase != null && element.transform.IsChildOf(card.CardTileBase.transform))
                {
                    return card;
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if an element is a deck list card (in MainDeck_MetaCardHolder).
        /// </summary>
        public static bool IsDeckListCard(GameObject element)
        {
            return GetDeckListCardInfo(element) != null;
        }

        // Cache for ShowUnCollectedTreatment field (public field on MetaCardView base class)
        private static FieldInfo _showUnCollectedField = null;
        private static bool _showUnCollectedFieldSearched = false;

        /// <summary>
        /// Checks if a deck list card entry represents unowned (missing) copies
        /// by reading MetaCardView.ShowUnCollectedTreatment (set by SetDisplayInformation).
        /// </summary>
        private static bool CheckDeckListCardUnowned(GameObject viewGameObject)
        {
            if (viewGameObject == null) return false;

            try
            {
                var metaCardView = GetMetaCardView(viewGameObject);
                if (metaCardView == null) return false;

                // ShowUnCollectedTreatment is a public field on MetaCardView (base class)
                // It's set by SetDisplayInformation from displayInformation.Unowned
                if (!_showUnCollectedFieldSearched)
                {
                    _showUnCollectedFieldSearched = true;
                    _showUnCollectedField = metaCardView.GetType().GetField("ShowUnCollectedTreatment");
                }

                if (_showUnCollectedField != null)
                    return (bool)_showUnCollectedField.GetValue(metaCardView);
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider",
                    $"Error checking deck list card unowned: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Extracts full card info for a deck list card using its GrpId.
        /// Includes the quantity from the deck list.
        /// Uses ExtractCardInfoFromModel for consistent extraction with other card types.
        /// </summary>
        public static CardInfo? ExtractDeckListCardInfo(GameObject element)
        {
            var deckCardInfo = GetDeckListCardInfo(element);
            if (deckCardInfo == null || !deckCardInfo.Value.IsValid)
                return null;

            var info = deckCardInfo.Value;

            // Check if this deck list entry is unowned (missing copies)
            bool isUnowned = CheckDeckListCardUnowned(info.ViewGameObject);

            // Use ExtractCardInfoFromModel on the ViewGameObject for consistent extraction
            // This uses the same logic as collection cards, duel cards, etc.
            if (info.ViewGameObject != null)
            {
                var cardInfo = ExtractCardInfoFromModel(info.ViewGameObject);
                if (cardInfo.HasValue && cardInfo.Value.IsValid)
                {
                    var result = cardInfo.Value;
                    result.Quantity = info.Quantity;
                    result.IsUnowned = isUnowned;
                    return result;
                }
            }

            // Fallback: return minimal info with just name and quantity
            string name = GetNameFromGrpId(info.GrpId);
            return new CardInfo
            {
                Name = name ?? $"Card #{info.GrpId}",
                Quantity = info.Quantity,
                IsUnowned = isUnowned,
                IsValid = true
            };
        }

        /// <summary>
        /// Gets full CardInfo from a GrpId by looking up CardPrintingData from the CardDatabase.
        /// Returns null if the card cannot be found.
        /// </summary>
        public static CardInfo? GetCardInfoFromGrpId(uint grpId)
        {
            if (grpId == 0) return null;

            try
            {
                var cardData = GetCardDataFromGrpId(grpId);
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
        private static object GetCardDataFromGrpId(uint grpId)
        {
            try
            {
                // Find CardDatabase - use the cached holder's CardDatabase property
                if (_cachedDeckHolder == null)
                    GetDeckListCards(); // This will populate _cachedDeckHolder

                if (_cachedDeckHolder == null)
                {
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"GetCardDataFromGrpId: _cachedDeckHolder is null");
                    return null;
                }

                MonoBehaviour holderComponent = null;
                foreach (var mb in _cachedDeckHolder.GetComponents<MonoBehaviour>())
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

                // Name
                info.Name = GetNameFromGrpId(grpId);

                // TypeLine
                var typeLineProp = cardType.GetProperty("TypeLine") ?? cardType.GetProperty("TypeText");
                if (typeLineProp != null)
                {
                    info.TypeLine = typeLineProp.GetValue(cardData)?.ToString();
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

                // Power/Toughness - use GetStringBackedIntValue (same as MODEL extraction)
                var powerProp = cardType.GetProperty("Power");
                var toughnessProp = cardType.GetProperty("Toughness");
                if (powerProp != null && toughnessProp != null)
                {
                    var power = powerProp.GetValue(cardData);
                    var toughness = toughnessProp.GetValue(cardData);
                    if (power != null && toughness != null)
                    {
                        // Use GetStringBackedIntValue to properly extract the value
                        string powerStr = GetStringBackedIntValue(power);
                        string toughStr = GetStringBackedIntValue(toughness);
                        if (!string.IsNullOrEmpty(powerStr) && !string.IsNullOrEmpty(toughStr))
                        {
                            info.PowerToughness = $"{powerStr}/{toughStr}";
                        }
                    }
                }

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
                                var abilityText = GetAbilityTextFromProvider(grpId, abilityId, null, 0);
                                if (!string.IsNullOrEmpty(abilityText))
                                {
                                    rulesTexts.Add(abilityText);
                                }
                            }
                        }

                        if (rulesTexts.Count > 0)
                        {
                            info.RulesText = string.Join(" ", rulesTexts);
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
                        info.FlavorText = GetFlavorText(flavorId);
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
                        var artistName = GetArtistName(artistId);
                        if (!string.IsNullOrEmpty(artistName))
                        {
                            info.Artist = artistName;
                            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Extracted artist by ID: {info.Artist}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider", $"Error extracting card info from data: {ex.Message}");
            }

            return info;
        }

        #endregion

        #region Extended Card Info (Keywords + Linked Faces)

        // Cache for linked face reflection
        private static PropertyInfo _linkedFaceTypeProp;
        private static bool _linkedFaceTypePropSearched;
        private static PropertyInfo _linkedFaceGrpIdsProp;
        private static bool _linkedFaceGrpIdsPropSearched;

        // Cache for ability hanger provider (keyword descriptions)
        private static object _abilityHangerProvider;
        private static MethodInfo _getHangerConfigsForCardMethod;
        private static MethodInfo _hangerProviderCleanupMethod;

        // Cache for duel-scene CardDataProvider
        private static object _duelCardDataProvider;
        private static MethodInfo _duelGetCardPrintingMethod;
        private static bool _duelCardDataProviderSearched;

        /// <summary>
        /// Clears extended info caches. Called from ClearCache().
        /// </summary>
        private static void ClearExtendedInfoCache()
        {
            _linkedFaceTypeProp = null;
            _linkedFaceTypePropSearched = false;
            _linkedFaceGrpIdsProp = null;
            _linkedFaceGrpIdsPropSearched = false;
            _abilityHangerProvider = null;
            _getHangerConfigsForCardMethod = null;
            _hangerProviderCleanupMethod = null;
            _duelCardDataProvider = null;
            _duelGetCardPrintingMethod = null;
            _duelCardDataProviderSearched = false;
        }

        /// <summary>
        /// Gets keyword ability descriptions for the focused card.
        /// Uses the game's AbilityHangerBaseConfigProvider (from AbilityHangerBase MonoBehaviour)
        /// to get the same keyword tooltips shown when hovering cards in-game.
        /// Returns Header + Details pairs like "Fliegend: Diese Kreatur kann nur..."
        /// </summary>
        public static List<string> GetKeywordDescriptions(GameObject card)
        {
            var result = new List<string>();
            if (card == null) return result;

            try
            {
                // We need a CDC to call the hanger provider
                var cdc = GetDuelSceneCDC(card);
                if (cdc == null) return result;

                // Ensure hanger provider is cached (retry if not found previously)
                if (_abilityHangerProvider == null)
                {
                    FindAbilityHangerProvider();
                }

                if (_abilityHangerProvider == null || _getHangerConfigsForCardMethod == null)
                    return result;

                var cdcType = cdc.GetType();

                // Get Model (ICardDataAdapter) from CDC
                var modelProp = cdcType.GetProperty("Model", BindingFlags.Public | BindingFlags.Instance);
                if (modelProp == null)
                {
                    // Try base type
                    modelProp = cdcType.BaseType?.GetProperty("Model", BindingFlags.Public | BindingFlags.Instance);
                }
                if (modelProp == null) return result;
                var model = modelProp.GetValue(cdc);
                if (model == null) return result;

                // Get HolderType (CardHolderType enum) from CDC
                var holderTypeProp = cdcType.GetProperty("HolderType", BindingFlags.Public | BindingFlags.Instance)
                    ?? cdcType.BaseType?.GetProperty("HolderType", BindingFlags.Public | BindingFlags.Instance);
                object holderType = null;
                if (holderTypeProp != null)
                    holderType = holderTypeProp.GetValue(cdc);

                // Create CDCViewMetadata struct
                object metadata = CreateCDCViewMetadata(cdc);

                if (holderType == null || metadata == null)
                {
                    MelonLogger.Msg($"[CardModelProvider] [ExtInfo] Missing holderType={holderType != null} metadata={metadata != null}");
                    return result;
                }

                // Call GetHangerConfigsForCard(model, holderType, metadata)
                var configs = _getHangerConfigsForCardMethod.Invoke(
                    _abilityHangerProvider, new object[] { model, holderType, metadata });

                if (configs is IEnumerable configEnum)
                {
                    var seen = new HashSet<string>();
                    foreach (var config in configEnum)
                    {
                        if (config == null) continue;
                        var configType = config.GetType();

                        // HangerConfig is a struct with public readonly fields
                        var headerField = configType.GetField("Header");
                        var detailsField = configType.GetField("Details");

                        string header = headerField?.GetValue(config)?.ToString() ?? "";
                        string details = detailsField?.GetValue(config)?.ToString() ?? "";

                        if (string.IsNullOrEmpty(header) && string.IsNullOrEmpty(details)) continue;

                        // Format: "Header: Details"
                        string text;
                        if (!string.IsNullOrEmpty(header) && !string.IsNullOrEmpty(details))
                            text = $"{header}: {details}";
                        else if (!string.IsNullOrEmpty(header))
                            text = header;
                        else
                            text = details;

                        // Parse mana symbols
                        text = ParseManaSymbolsInText(text);

                        MelonLogger.Msg($"[CardModelProvider] [ExtInfo] Hanger: '{text}'");

                        if (seen.Add(text))
                            result.Add(text);
                    }
                }

                // Cleanup provider internal state
                _hangerProviderCleanupMethod?.Invoke(_abilityHangerProvider, null);

                MelonLogger.Msg($"[CardModelProvider] [ExtInfo] GetKeywordDescriptions: {result.Count} entries");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CardModelProvider] [ExtInfo] Error getting keyword descriptions: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Finds and caches the AbilityHangerBaseConfigProvider from an AbilityHangerBase MonoBehaviour.
        /// Uses Resources.FindObjectsOfTypeAll to include inactive GameObjects.
        /// This provider generates the keyword tooltip data (Header + Details) shown when hovering cards.
        /// </summary>
        private static void FindAbilityHangerProvider()
        {
            // Find the AbilityHangerBase type in loaded assemblies
            Type ahbType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    ahbType = asm.GetType("AbilityHangerBase");
                    if (ahbType != null) break;
                }
                catch { }
            }

            UnityEngine.Object[] instances = null;

            if (ahbType != null)
            {
                // FindObjectsOfTypeAll includes inactive GameObjects
                instances = Resources.FindObjectsOfTypeAll(ahbType);
                MelonLogger.Msg($"[CardModelProvider] Found {instances.Length} AbilityHangerBase instances (including inactive)");
            }
            else
            {
                MelonLogger.Msg($"[CardModelProvider] AbilityHangerBase type not found in assemblies, searching all MonoBehaviours...");
                instances = Resources.FindObjectsOfTypeAll(typeof(MonoBehaviour));
            }

            foreach (var obj in instances)
            {
                if (obj == null) continue;
                var type = obj.GetType();

                // If we searched all MonoBehaviours, filter to hanger types
                if (ahbType == null && !type.Name.Contains("AbilityHanger"))
                    continue;

                // Look for _abilityHangerProvider field (protected in AbilityHangerBase)
                var field = type.GetField("_abilityHangerProvider",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (field == null) continue;

                var provider = field.GetValue(obj);
                if (provider == null)
                {
                    MelonLogger.Msg($"[CardModelProvider] Found {type.Name} but _abilityHangerProvider is null (not yet initialized)");
                    continue;
                }

                var providerType = provider.GetType();

                // Find GetHangerConfigsForCard method
                var getConfigsMethod = providerType.GetMethod("GetHangerConfigsForCard");
                if (getConfigsMethod == null) continue;

                // Find Cleanup method
                var cleanupMethod = providerType.GetMethod("Cleanup");

                _abilityHangerProvider = provider;
                _getHangerConfigsForCardMethod = getConfigsMethod;
                _hangerProviderCleanupMethod = cleanupMethod;

                MelonLogger.Msg($"[CardModelProvider] Found AbilityHangerProvider: {providerType.Name} from {type.Name}");

                // Log method signature for debugging
                var ps = getConfigsMethod.GetParameters();
                MelonLogger.Msg($"[CardModelProvider] GetHangerConfigsForCard({string.Join(", ", ps.Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
                return;
            }

            MelonLogger.Msg($"[CardModelProvider] AbilityHangerProvider not found (will retry on next I key press)");
        }

        /// <summary>
        /// Creates a CDCViewMetadata struct via reflection.
        /// Tries the BASE_CDC constructor first, falls back to the bool constructor.
        /// </summary>
        private static object CreateCDCViewMetadata(Component cdc)
        {
            try
            {
                // Find CDCViewMetadata type
                Type metadataType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        metadataType = asm.GetType("Wotc.Mtga.CardParts.CDCViewMetadata");
                        if (metadataType != null) break;
                    }
                    catch { }
                }
                if (metadataType == null) return null;

                // Try constructor that takes BASE_CDC (or any base type of cdc)
                var cdcType = cdc.GetType();
                while (cdcType != null && cdcType != typeof(object))
                {
                    var ctor = metadataType.GetConstructor(new[] { cdcType });
                    if (ctor != null)
                        return ctor.Invoke(new object[] { cdc });
                    cdcType = cdcType.BaseType;
                }

                // Fallback: use bool constructor with safe defaults
                var boolCtor = metadataType.GetConstructor(new[] {
                    typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
                if (boolCtor != null)
                    return boolCtor.Invoke(new object[] { false, false, false, false, false });
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CardModelProvider] Error creating CDCViewMetadata: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Gets linked face information for double-faced, split, adventure, and room cards.
        /// Returns a label (e.g. "Other face") and full CardInfo for the linked face, or null if none.
        /// </summary>
        public static (string label, CardInfo faceInfo)? GetLinkedFaceInfo(GameObject card)
        {
            if (card == null) return null;

            try
            {
                // Get model
                object model = null;
                var cdc = GetDuelSceneCDC(card);
                if (cdc != null)
                    model = GetCardModel(cdc);
                if (model == null)
                {
                    var metaView = GetMetaCardView(card);
                    if (metaView != null)
                        model = GetMetaCardModel(metaView);
                }
                if (model == null) return null;

                var objType = model.GetType();

                // Get LinkedFaceType (enum stored as int)
                if (!_linkedFaceTypePropSearched)
                {
                    _linkedFaceTypePropSearched = true;
                    _linkedFaceTypeProp = objType.GetProperty("LinkedFaceType", BindingFlags.Public | BindingFlags.Instance);
                }
                if (_linkedFaceTypeProp == null) return null;

                var linkedFaceVal = _linkedFaceTypeProp.GetValue(model);
                if (linkedFaceVal == null) return null;

                int linkedFaceInt = (int)Convert.ChangeType(linkedFaceVal, typeof(int));
                if (linkedFaceInt == 0) return null; // None

                // Get LinkedFaceGrpIds
                if (!_linkedFaceGrpIdsPropSearched)
                {
                    _linkedFaceGrpIdsPropSearched = true;
                    _linkedFaceGrpIdsProp = objType.GetProperty("LinkedFaceGrpIds", BindingFlags.Public | BindingFlags.Instance);
                }
                if (_linkedFaceGrpIdsProp == null) return null;

                var grpIdsVal = _linkedFaceGrpIdsProp.GetValue(model);
                if (grpIdsVal == null) return null;

                // Extract first GrpId from the list
                uint faceGrpId = 0;
                if (grpIdsVal is IEnumerable grpIdsEnum)
                {
                    foreach (var item in grpIdsEnum)
                    {
                        if (item is uint uid && uid > 0) { faceGrpId = uid; break; }
                        if (item is int iid && iid > 0) { faceGrpId = (uint)iid; break; }
                    }
                }

                if (faceGrpId == 0) return null;

                // Look up card data for the linked face GrpId
                var faceData = GetCardDataFromGrpIdDuelScene(faceGrpId);
                if (faceData == null) return null;

                var faceInfo = ExtractCardInfoFromObject(faceData);
                if (!faceInfo.IsValid) return null;

                // Map linked face type to label
                string label = GetLinkedFaceLabel(linkedFaceInt);

                return (label, faceInfo);
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider",
                    $"Error getting linked face info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Maps LinkedFace enum int to a user-facing label.
        /// Values based on decompiled LinkedFace enum.
        /// </summary>
        private static string GetLinkedFaceLabel(int linkedFaceType)
        {
            switch (linkedFaceType)
            {
                case 1:  // DfcFront
                case 2:  // DfcBack
                    return Strings.LinkedFaceOtherFace;
                case 5:  // SplitCard
                case 6:  // SplitHalf
                    return Strings.LinkedFaceOtherHalf;
                case 7:  // AdventureParent
                case 8:  // AdventureChild
                    return Strings.LinkedFaceAdventure;
                case 9:  // MdfcFront
                case 10: // MdfcBack
                    return Strings.LinkedFaceOtherFace;
                case 15: // RoomCard
                case 16: // RoomHalf
                    return Strings.LinkedFaceOtherRoom;
                default:
                    return Strings.LinkedFaceOtherFace;
            }
        }

        /// <summary>
        /// Gets CardData from a GrpId using the duel-scene CardDatabase.
        /// Separate from GetCardDataFromGrpId() which requires _cachedDeckHolder (menu-only).
        /// </summary>
        private static object GetCardDataFromGrpIdDuelScene(uint grpId)
        {
            if (grpId == 0) return null;

            // First try the menu-scene path (works if _cachedDeckHolder is available)
            if (_cachedDeckHolder != null)
            {
                var menuResult = GetCardDataFromGrpId(grpId);
                if (menuResult != null) return menuResult;
            }

            // Try duel-scene path via GameManager.CardDatabase.CardDataProvider
            if (!_duelCardDataProviderSearched)
            {
                _duelCardDataProviderSearched = true;
                FindDuelCardDataProvider();
            }

            if (_duelCardDataProvider == null || _duelGetCardPrintingMethod == null)
                return null;

            try
            {
                var result = _duelGetCardPrintingMethod.Invoke(_duelCardDataProvider, new object[] { grpId, null });
                return result;
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider",
                    $"Error getting card data for GrpId {grpId} in duel scene: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Finds and caches the CardDataProvider from GameManager.CardDatabase for duel scene lookups.
        /// </summary>
        private static void FindDuelCardDataProvider()
        {
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;
                var type = mb.GetType();
                if (type.Name != "GameManager") continue;

                var cardDbProp = type.GetProperty("CardDatabase");
                if (cardDbProp == null) break;

                var cardDb = cardDbProp.GetValue(mb);
                if (cardDb == null) break;

                var cardDbType = cardDb.GetType();
                var cdpProp = cardDbType.GetProperty("CardDataProvider");
                if (cdpProp == null) break;

                var cdp = cdpProp.GetValue(cardDb);
                if (cdp == null) break;

                var cdpType = cdp.GetType();

                // Try GetCardPrintingById(uint id, string skinCode)
                var method = cdpType.GetMethod("GetCardPrintingById", new[] { typeof(uint), typeof(string) });
                if (method == null)
                    method = cdpType.GetMethod("GetCardRecordById", new[] { typeof(uint), typeof(string) });

                if (method != null)
                {
                    _duelCardDataProvider = cdp;
                    _duelGetCardPrintingMethod = method;
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardModelProvider",
                        $"Found duel CardDataProvider: {cdpType.Name}.{method.Name}");
                }
                break;
            }
        }

        #endregion
    }
}
