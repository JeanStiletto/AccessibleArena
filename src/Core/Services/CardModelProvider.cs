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
                if (typeName == "PagesMetaCardView" ||
                    typeName == "MetaCardView" ||
                    typeName == "BoosterMetaCardView" ||
                    typeName == "ListMetaCardView")
                {
                    // Log once when we find a MetaCardView
                    if (!_metaCardViewComponentsLogged)
                    {
                        _metaCardViewComponentsLogged = true;
                        MelonLogger.Msg($"[CardModelProvider] === FOUND MetaCardView: {typeName} on '{card.name}' ===");
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
                MelonLogger.Msg($"[CardModelProvider] Error getting Model: {ex.Message}");
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

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CardModelProvider] Error getting MetaCardView Model: {ex.Message}");
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
            MelonLogger.Msg($"[CardModelProvider] === METACARDVIEW TYPE: {viewType.FullName} ===");

            // Log properties
            var properties = viewType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(metaCardView);
                    string valueStr = value?.ToString() ?? "null";
                    if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                    MelonLogger.Msg($"[CardModelProvider] MetaCardView Property: {prop.Name} = {valueStr} ({prop.PropertyType.Name})");
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[CardModelProvider] MetaCardView Property: {prop.Name} = [Error: {ex.Message}] ({prop.PropertyType.Name})");
                }
            }
            MelonLogger.Msg($"[CardModelProvider] === END METACARDVIEW PROPERTIES ===");
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
                        MelonLogger.Msg("[CardModelProvider] Found GameManager, checking for name lookup methods...");

                        // Try CardDatabase property - get CardTitleProvider or CardNameTextProvider
                        var cardDbProp = type.GetProperty("CardDatabase");
                        if (cardDbProp != null)
                        {
                            var cardDb = cardDbProp.GetValue(mb);
                            if (cardDb != null)
                            {
                                var cardDbType = cardDb.GetType();
                                MelonLogger.Msg($"[CardModelProvider] CardDatabase type: {cardDbType.FullName}");

                                // Try CardTitleProvider
                                var titleProviderProp = cardDbType.GetProperty("CardTitleProvider");
                                if (titleProviderProp != null)
                                {
                                    var titleProvider = titleProviderProp.GetValue(cardDb);
                                    if (titleProvider != null)
                                    {
                                        var providerType = titleProvider.GetType();
                                        MelonLogger.Msg($"[CardModelProvider] CardTitleProvider type: {providerType.FullName}");

                                        // List methods on the provider
                                        foreach (var m in providerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                        {
                                            if (m.DeclaringType == typeof(object)) continue;
                                            MelonLogger.Msg($"[CardModelProvider] CardTitleProvider.{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))}) -> {m.ReturnType.Name}");
                                        }

                                        // Use GetCardTitle(UInt32, Boolean, String) method
                                        var getMethod = providerType.GetMethod("GetCardTitle", new[] { typeof(uint), typeof(bool), typeof(string) });

                                        if (getMethod != null)
                                        {
                                            _idNameProvider = titleProvider;
                                            _getNameMethod = getMethod;
                                            MelonLogger.Msg($"[CardModelProvider] Using CardTitleProvider.GetCardTitle for name lookup");
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
                                        MelonLogger.Msg($"[CardModelProvider] CardNameTextProvider type: {providerType.FullName}");

                                        // List methods on the provider
                                        foreach (var m in providerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                        {
                                            if (m.DeclaringType == typeof(object)) continue;
                                            MelonLogger.Msg($"[CardModelProvider] CardNameTextProvider.{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))}) -> {m.ReturnType.Name}");
                                        }

                                        // Try common method names
                                        var getMethod = providerType.GetMethod("GetName", new[] { typeof(uint) })
                                            ?? providerType.GetMethod("GetText", new[] { typeof(uint) })
                                            ?? providerType.GetMethod("Get", new[] { typeof(uint) });

                                        if (getMethod != null)
                                        {
                                            _idNameProvider = nameProvider;
                                            _getNameMethod = getMethod;
                                            MelonLogger.Msg($"[CardModelProvider] Using CardNameTextProvider.{getMethod.Name} for name lookup");
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
                                MelonLogger.Msg($"[CardModelProvider] LocManager type: {locType.FullName}");

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
                                            MelonLogger.Msg($"[CardModelProvider] Found GetLocalizedText: {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
                                            break;
                                        }
                                    }
                                }

                                if (getTextMethod != null)
                                {
                                    _idNameProvider = locMgr;
                                    _getNameMethod = getTextMethod;
                                    MelonLogger.Msg("[CardModelProvider] Using LocManager.GetLocalizedText for name lookup");
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
                            MelonLogger.Msg($"[CardModelProvider] Found {type.Name} for name lookup");
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
                                            MelonLogger.Msg("[CardModelProvider] Found WrapperController.CardDatabase for name lookup");
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
                MelonLogger.Msg("[CardModelProvider] Searching for Meta scene localization providers...");
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    var type = mb.GetType();
                    string typeName = type.Name;

                    // Log interesting types for discovery
                    if (typeName.Contains("Card") || typeName.Contains("Title") ||
                        typeName.Contains("Name") || typeName.Contains("Loc") ||
                        typeName.Contains("Database") || typeName.Contains("Provider"))
                    {
                        MelonLogger.Msg($"[CardModelProvider] Found potential provider: {typeName}");

                        // Check for CardDatabase property
                        var cardDbProp = type.GetProperty("CardDatabase");
                        if (cardDbProp != null)
                        {
                            MelonLogger.Msg($"[CardModelProvider] {typeName} has CardDatabase property");
                            try
                            {
                                var cardDb = cardDbProp.GetValue(mb);
                                if (cardDb != null)
                                {
                                    var cardDbType = cardDb.GetType();
                                    MelonLogger.Msg($"[CardModelProvider] CardDatabase type: {cardDbType.FullName}");

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
                                                MelonLogger.Msg($"[CardModelProvider] Using {typeName}.CardDatabase.CardTitleProvider for name lookup");
                                                return;
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                MelonLogger.Msg($"[CardModelProvider] Error accessing {typeName}.CardDatabase: {ex.Message}");
                            }
                        }
                    }
                }

                MelonLogger.Msg("[CardModelProvider] IdNameProvider not found - will use UI fallback for names");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CardModelProvider] Error finding IdNameProvider: {ex.Message}");
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
                MelonLogger.Msg($"[CardModelProvider] GrpId is 0, cannot lookup name");
                return null;
            }

            FindIdNameProvider();

            if (_getNameMethod == null)
            {
                MelonLogger.Msg($"[CardModelProvider] No localization method found for GrpId {grpId}");
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
                    MelonLogger.Msg($"[CardModelProvider] GrpId {grpId} -> Name: {name}");
                    return name;
                }

                MelonLogger.Msg($"[CardModelProvider] GrpId {grpId}: No valid name found (result: {name ?? "null"})");
                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CardModelProvider] Error getting name from GrpId {grpId}: {ex.Message}");
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
            MelonLogger.Msg($"[CardModelProvider] === MODEL TYPE: {modelType.FullName} ===");

            var properties = modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(model);
                    string valueStr = value?.ToString() ?? "null";
                    if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                    MelonLogger.Msg($"[CardModelProvider] Property: {prop.Name} = {valueStr} ({prop.PropertyType.Name})");
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[CardModelProvider] Property: {prop.Name} = [Error: {ex.Message}] ({prop.PropertyType.Name})");
                }
            }
            MelonLogger.Msg($"[CardModelProvider] === END MODEL PROPERTIES ===");
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
            MelonLogger.Msg($"[CardModelProvider] === ABILITY TYPE: {abilityType.FullName} ===");

            // Log properties
            var properties = abilityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(ability);
                    string valueStr = value?.ToString() ?? "null";
                    if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                    MelonLogger.Msg($"[CardModelProvider] Ability Property: {prop.Name} = {valueStr} ({prop.PropertyType.Name})");
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[CardModelProvider] Ability Property: {prop.Name} = [Error: {ex.Message}] ({prop.PropertyType.Name})");
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
                        MelonLogger.Msg($"[CardModelProvider] Ability Method: {method.Name}() = {resultStr}");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Msg($"[CardModelProvider] Ability Method: {method.Name}() = [Error: {ex.Message}]");
                    }
                }
            }

            MelonLogger.Msg($"[CardModelProvider] === END ABILITY PROPERTIES ===");
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
                    MelonLogger.Msg($"[CardModelProvider] Ability {abilityId} -> {text}");
                    return text;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CardModelProvider] Error looking up ability {abilityId}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Searches for the ability text provider in the game.
        /// </summary>
        private static void FindAbilityTextProvider()
        {
            MelonLogger.Msg("[CardModelProvider] Searching for ability text provider...");

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
                                    MelonLogger.Msg($"[CardModelProvider] CardDatabase.{prop.Name} ({prop.PropertyType.Name})");

                                    var provider = prop.GetValue(cardDb);
                                    if (provider != null)
                                    {
                                        var providerType = provider.GetType();
                                        foreach (var m in providerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                        {
                                            if (m.DeclaringType == typeof(object)) continue;
                                            var paramStr = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                                            MelonLogger.Msg($"[CardModelProvider]   {m.Name}({paramStr}) -> {m.ReturnType.Name}");

                                            // Look for methods that take uint and return string
                                            if (m.ReturnType == typeof(string))
                                            {
                                                var mParams = m.GetParameters();
                                                if (mParams.Length >= 1 && mParams[0].ParameterType == typeof(uint))
                                                {
                                                    _abilityTextProvider = provider;
                                                    _getAbilityTextMethod = m;
                                                    MelonLogger.Msg($"[CardModelProvider] Using {prop.Name}.{m.Name} for ability text lookup");
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
                                                        MelonLogger.Msg($"[CardModelProvider] Using {typeName}.CardDatabase.{prop.Name}.{m.Name} for ability text lookup");
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

            MelonLogger.Msg("[CardModelProvider] No ability text provider found");
        }

        /// <summary>
        /// Searches for the flavor text provider in the game.
        /// FlavorTextId is a localization key that needs to be looked up via GreLocProvider or ClientLocProvider.
        /// </summary>
        private static void FindFlavorTextProvider()
        {
            MelonLogger.Msg("[CardModelProvider] Searching for flavor text provider...");

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                var type = mb.GetType();
                if (type.Name == "GameManager")
                {
                    MelonLogger.Msg("[CardModelProvider] Found GameManager, looking for CardDatabase...");
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
                                    MelonLogger.Msg($"[CardModelProvider] Found GreLocProvider: {providerType.FullName}");

                                    // Log all methods
                                    foreach (var m in providerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                    {
                                        if (m.DeclaringType == typeof(object)) continue;
                                        var paramStr = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                                        MelonLogger.Msg($"[CardModelProvider]   GreLocProvider.{m.Name}({paramStr}) -> {m.ReturnType.Name}");

                                        // Look for GetString, GetText, or similar methods
                                        if (m.ReturnType == typeof(string) &&
                                            (m.Name == "GetString" || m.Name == "GetText" || m.Name == "Get" || m.Name.Contains("Loc")))
                                        {
                                            var mParams = m.GetParameters();
                                            if (mParams.Length >= 1 && mParams[0].ParameterType == typeof(uint))
                                            {
                                                _flavorTextProvider = greLocProvider;
                                                _getFlavorTextMethod = m;
                                                MelonLogger.Msg($"[CardModelProvider] Using GreLocProvider.{m.Name} for flavor text lookup");
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
                                    MelonLogger.Msg($"[CardModelProvider] Found ClientLocProvider: {providerType.FullName}");

                                    // Log all methods
                                    foreach (var m in providerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                    {
                                        if (m.DeclaringType == typeof(object)) continue;
                                        var paramStr = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                                        MelonLogger.Msg($"[CardModelProvider]   ClientLocProvider.{m.Name}({paramStr}) -> {m.ReturnType.Name}");

                                        // Look for GetString, GetText methods
                                        if (m.ReturnType == typeof(string))
                                        {
                                            var mParams = m.GetParameters();
                                            if (mParams.Length >= 1 && mParams[0].ParameterType == typeof(uint))
                                            {
                                                _flavorTextProvider = clientLocProvider;
                                                _getFlavorTextMethod = m;
                                                MelonLogger.Msg($"[CardModelProvider] Using ClientLocProvider.{m.Name} for flavor text lookup");
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

            MelonLogger.Msg("[CardModelProvider] No flavor text provider found");
        }

        /// <summary>
        /// Searches for the artist provider in the game.
        /// </summary>
        private static void FindArtistProvider()
        {
            MelonLogger.Msg("[CardModelProvider] Searching for artist provider...");

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
                                    MelonLogger.Msg($"[CardModelProvider] CardDatabase.{prop.Name} ({prop.PropertyType.Name})");

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
                                                    MelonLogger.Msg($"[CardModelProvider] Using {prop.Name}.{m.Name} for artist lookup");
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

            MelonLogger.Msg("[CardModelProvider] No artist provider found");
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
                    MelonLogger.Msg($"[CardModelProvider] Flavor text found: {text}");
                    return text;
                }
                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CardModelProvider] Error getting flavor text for id {flavorId}: {ex.Message}");
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
        private static string ConvertManaColorToName(string colorEnum)
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
                MelonLogger.Msg($"[CardModelProvider] ExtractCardInfoFromModel called with: '{cardObj.name}'");
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

                // Also try parent if not found on this object
                if (metaCardView == null && cardObj.transform.parent != null)
                {
                    metaCardView = GetMetaCardView(cardObj.transform.parent.gameObject);
                }

                if (metaCardView != null)
                {
                    // Log properties for discovery (once)
                    LogMetaCardViewProperties(metaCardView);

                    model = GetMetaCardModel(metaCardView);

                    // Log result once
                    if (!_metaCardViewPropertiesLogged)
                    {
                        MelonLogger.Msg($"[CardModelProvider] GetMetaCardModel returned: {(model != null ? model.GetType().Name : "null")}");
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

            var info = new CardInfo();
            var modelType = model.GetType();

            try
            {
                // Name - get from GrpId using CardTitleProvider lookup
                // GrpId is the card database ID, TitleId is a localization key
                var grpIdObj = GetModelPropertyValue(model, modelType, "GrpId");
                if (grpIdObj != null && grpIdObj is uint grpId)
                {
                    info.Name = GetNameFromGrpId(grpId);
                }

                // Mana Cost - parse PrintedCastingCost (ManaQuantity[])
                var castingCost = GetModelPropertyValue(model, modelType, "PrintedCastingCost");
                if (castingCost is IEnumerable costEnum)
                {
                    info.ManaCost = ParseManaQuantityArray(costEnum);
                }

                // Type Line - build from Supertypes + CardTypes + Subtypes
                var typeLineParts = new List<string>();

                // Supertypes (Legendary, Basic, etc.)
                var supertypes = GetModelPropertyValue(model, modelType, "Supertypes");
                if (supertypes is IEnumerable superEnum)
                {
                    foreach (var st in superEnum)
                    {
                        if (st != null)
                        {
                            string s = st.ToString();
                            if (s != "None" && !string.IsNullOrEmpty(s))
                                typeLineParts.Add(s);
                        }
                    }
                }

                // CardTypes (Creature, Land, Instant, etc.)
                var cardTypes = GetModelPropertyValue(model, modelType, "CardTypes");
                if (cardTypes is IEnumerable cardEnum)
                {
                    foreach (var ct in cardEnum)
                    {
                        if (ct != null)
                        {
                            string c = ct.ToString();
                            if (!string.IsNullOrEmpty(c))
                                typeLineParts.Add(c);
                        }
                    }
                }

                // Subtypes (Goblin, Warrior, Forest, etc.) - add after a dash
                var subtypes = GetModelPropertyValue(model, modelType, "Subtypes");
                var subtypeList = new List<string>();
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

                // Power and Toughness - only for creatures (check if CardTypes contains Creature)
                bool isCreature = false;
                var cardTypesForPT = GetModelPropertyValue(model, modelType, "CardTypes");
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

                if (isCreature)
                {
                    var power = GetModelPropertyValue(model, modelType, "Power");
                    var toughness = GetModelPropertyValue(model, modelType, "Toughness");
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
                // Get card context needed for ability text lookup
                uint cardGrpId = 0;
                uint cardTitleId = 0;
                var grpIdVal = GetModelPropertyValue(model, modelType, "GrpId");
                if (grpIdVal is uint gid) cardGrpId = gid;
                var titleIdVal = GetModelPropertyValue(model, modelType, "TitleId");
                if (titleIdVal is uint tid) cardTitleId = tid;

                // Get all ability IDs for the lookup
                var abilityIdsVal = GetModelPropertyValue(model, modelType, "AbilityIds");
                uint[] abilityIds = null;
                if (abilityIdsVal is IEnumerable<uint> aidEnum)
                    abilityIds = aidEnum.ToArray();
                else if (abilityIdsVal is uint[] aidArray)
                    abilityIds = aidArray;

                var abilities = GetModelPropertyValue(model, modelType, "Abilities");
                if (abilities is IEnumerable abilityEnum)
                {
                    var rulesLines = new List<string>();
                    foreach (var ability in abilityEnum)
                    {
                        if (ability == null) continue;

                        // Log ability properties for discovery (only once)
                        LogAbilityProperties(ability);

                        var abilityType = ability.GetType();

                        // Get the ability's Id for the lookup
                        uint abilityId = 0;
                        var abilityIdProp = abilityType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
                        if (abilityIdProp != null)
                        {
                            var idVal = abilityIdProp.GetValue(ability);
                            if (idVal is uint aid) abilityId = aid;
                        }

                        // Try to get text from the ability with card context
                        var textValue = GetAbilityText(ability, abilityType, cardGrpId, abilityId, abilityIds, cardTitleId);
                        if (!string.IsNullOrEmpty(textValue))
                        {
                            rulesLines.Add(textValue);
                        }
                    }

                    if (rulesLines.Count > 0)
                    {
                        // Join all ability texts and parse mana symbols to readable text
                        string rawRulesText = string.Join(" ", rulesLines);
                        info.RulesText = ParseManaSymbolsInText(rawRulesText);
                        MelonLogger.Msg($"[CardModelProvider] Extracted rules text: {info.RulesText}");
                    }
                }

                // Flavor Text - lookup via FlavorTextId (not FlavorId!)
                var flavorIdValue = GetModelPropertyValue(model, modelType, "FlavorTextId");
                if (flavorIdValue != null)
                {
                    uint flavorId = 0;
                    if (flavorIdValue is uint fid) flavorId = fid;
                    else if (flavorIdValue is int fidInt && fidInt > 0) flavorId = (uint)fidInt;

                    MelonLogger.Msg($"[CardModelProvider] FlavorTextId = {flavorId}");

                    if (flavorId > 0)
                    {
                        var flavorText = GetFlavorText(flavorId);
                        if (!string.IsNullOrEmpty(flavorText))
                        {
                            info.FlavorText = flavorText;
                            MelonLogger.Msg($"[CardModelProvider] Extracted flavor text: {info.FlavorText}");
                        }
                        else
                        {
                            MelonLogger.Msg($"[CardModelProvider] FlavorText lookup returned empty for id {flavorId}");
                        }
                    }
                }

                // Artist - try to get from Printing object which may have ArtistCredit
                var printing = GetModelPropertyValue(model, modelType, "Printing");
                if (printing != null)
                {
                    var printingType = printing.GetType();

                    // Try ArtistCredit or Artist property on Printing
                    var artistProp = printingType.GetProperty("ArtistCredit") ??
                                     printingType.GetProperty("Artist") ??
                                     printingType.GetProperty("ArtistName");
                    if (artistProp != null)
                    {
                        var artistValue = artistProp.GetValue(printing);
                        if (artistValue != null)
                        {
                            // Could be a string or an ID
                            if (artistValue is string artistStr && !string.IsNullOrEmpty(artistStr))
                            {
                                info.Artist = artistStr;
                                MelonLogger.Msg($"[CardModelProvider] Extracted artist from Printing: {info.Artist}");
                            }
                            else if (artistValue is uint artistId && artistId > 0)
                            {
                                var artistName = GetArtistName(artistId);
                                if (!string.IsNullOrEmpty(artistName))
                                {
                                    info.Artist = artistName;
                                    MelonLogger.Msg($"[CardModelProvider] Extracted artist: {info.Artist}");
                                }
                            }
                        }
                    }
                    else
                    {
                        // Log available properties on Printing for discovery
                        MelonLogger.Msg($"[CardModelProvider] Printing type: {printingType.Name}, checking for artist properties...");
                        foreach (var prop in printingType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                        {
                            if (prop.Name.ToLower().Contains("artist"))
                            {
                                MelonLogger.Msg($"[CardModelProvider] Found artist-related property: {prop.Name}");
                            }
                        }
                    }
                }

                info.IsValid = !string.IsNullOrEmpty(info.Name);
                return info;
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CardModelProvider] Error extracting model data: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Attachments

        /// <summary>
        /// Gets the list of cards attached to this card (enchantments, equipment, etc.).
        /// Returns empty list if no attachments or if model is not available.
        /// </summary>
        public static List<(uint instanceId, uint grpId, string name)> GetAttachments(GameObject card)
        {
            var attachments = new List<(uint instanceId, uint grpId, string name)>();
            if (card == null) return attachments;

            // First try to get attachments from the visual layer (IBattlefieldStack)
            attachments = GetAttachmentsFromVisualLayer(card);
            if (attachments.Count > 0)
            {
                return attachments;
            }

            // Fallback to data model
            var cdcComponent = GetDuelSceneCDC(card);
            if (cdcComponent == null) return attachments;

            var model = GetCardModel(cdcComponent);
            if (model == null) return attachments;

            try
            {
                var modelType = model.GetType();

                // First check the Instance property which has Children
                var instanceProp = modelType.GetProperty("Instance");
                object instance = instanceProp?.GetValue(model);

                IEnumerable children = null;

                if (instance != null)
                {
                    var instanceType = instance.GetType();
                    var childrenProp = instanceType.GetProperty("Children");
                    if (childrenProp != null)
                    {
                        children = childrenProp.GetValue(instance) as IEnumerable;
                    }
                }

                // Fallback: Try Children directly on model
                if (children == null)
                {
                    var childrenProp = modelType.GetProperty("Children");
                    if (childrenProp != null)
                    {
                        children = childrenProp.GetValue(model) as IEnumerable;
                    }
                }

                if (children == null)
                {
                    return attachments;
                }

                foreach (var child in children)
                {
                    if (child == null) continue;

                    var childType = child.GetType();

                    // Get InstanceId
                    uint instanceId = 0;
                    var instanceIdProp = childType.GetProperty("InstanceId");
                    if (instanceIdProp != null)
                    {
                        var idVal = instanceIdProp.GetValue(child);
                        if (idVal is uint uid) instanceId = uid;
                    }

                    // Get GrpId for name lookup
                    uint grpId = 0;
                    var grpIdProp = childType.GetProperty("GrpId");
                    if (grpIdProp != null)
                    {
                        var gidVal = grpIdProp.GetValue(child);
                        if (gidVal is uint gid) grpId = gid;
                    }

                    // Get name from GrpId
                    string name = null;
                    if (grpId > 0)
                    {
                        name = GetNameFromGrpId(grpId);
                    }

                    if (instanceId > 0)
                    {
                        attachments.Add((instanceId, grpId, name));
                        MelonLogger.Msg($"[CardModelProvider] Found attachment from model: InstanceId={instanceId}, GrpId={grpId}, Name={name ?? "unknown"}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CardModelProvider] Error getting attachments: {ex.Message}");
            }

            return attachments;
        }

        /// <summary>
        /// Gets attachments from the visual layer by looking at child GameObjects with CDC components.
        /// Attachments (auras, equipment) are rendered as child objects of the card they're attached to.
        /// </summary>
        private static List<(uint instanceId, uint grpId, string name)> GetAttachmentsFromVisualLayer(GameObject card)
        {
            var attachments = new List<(uint instanceId, uint grpId, string name)>();
            if (card == null) return attachments;

            try
            {
                // Debug: Log the card's hierarchy info
                string parentName = card.transform.parent != null ? card.transform.parent.name : "null";
                int childCount = card.transform.childCount;
                MelonLogger.Msg($"[CardModelProvider] GetAttachments for {card.name}: parent={parentName}, children={childCount}");

                // Look for child GameObjects that are cards (attachments are rendered as children)
                foreach (Transform child in card.transform)
                {
                    if (child == null || !child.gameObject.activeInHierarchy) continue;

                    // Check if this child has a DuelScene_CDC component (making it a card)
                    var childCdc = GetDuelSceneCDC(child.gameObject);
                    if (childCdc == null) continue;

                    var childModel = GetCardModel(childCdc);
                    if (childModel == null) continue;

                    var childModelType = childModel.GetType();

                    // Get GrpId
                    uint grpId = 0;
                    var grpIdProp = childModelType.GetProperty("GrpId");
                    if (grpIdProp != null)
                    {
                        var gidVal = grpIdProp.GetValue(childModel);
                        if (gidVal is uint gid) grpId = gid;
                    }

                    // Get InstanceId
                    uint instanceId = 0;
                    var instanceIdProp = childModelType.GetProperty("InstanceId");
                    if (instanceIdProp != null)
                    {
                        var idVal = instanceIdProp.GetValue(childModel);
                        if (idVal is uint uid) instanceId = uid;
                    }

                    if (grpId > 0)
                    {
                        string name = GetNameFromGrpId(grpId);
                        attachments.Add((instanceId, grpId, name));
                        MelonLogger.Msg($"[CardModelProvider] Found visual child attachment: {name} (GrpId={grpId}) on {card.name}");
                    }
                }

                // Also check siblings that might be positioned on top of this card (equipment/auras sometimes rendered as siblings)
                if (card.transform.parent != null)
                {
                    // Get this card's InstanceId to find related attachments
                    var thisCdc = GetDuelSceneCDC(card);
                    if (thisCdc != null)
                    {
                        var thisModel = GetCardModel(thisCdc);
                        if (thisModel != null)
                        {
                            var thisModelType = thisModel.GetType();
                            uint thisInstanceId = 0;
                            var instanceIdProp = thisModelType.GetProperty("InstanceId");
                            if (instanceIdProp != null)
                            {
                                var idVal = instanceIdProp.GetValue(thisModel);
                                if (idVal is uint uid) thisInstanceId = uid;
                            }

                            int siblingCount = 0;
                            int siblingCdcCount = 0;

                            // Check siblings for cards that have this card as their Parent
                            foreach (Transform sibling in card.transform.parent)
                            {
                                if (sibling == card.transform) continue;
                                if (sibling == null || !sibling.gameObject.activeInHierarchy) continue;

                                siblingCount++;
                                var siblingCdc = GetDuelSceneCDC(sibling.gameObject);
                                if (siblingCdc == null) continue;

                                siblingCdcCount++;
                                var siblingModel = GetCardModel(siblingCdc);
                                if (siblingModel == null) continue;

                                // Check if this sibling's Parent matches our card
                                var siblingModelType = siblingModel.GetType();
                                var parentProp = siblingModelType.GetProperty("Parent");
                                if (parentProp != null)
                                {
                                    var parent = parentProp.GetValue(siblingModel);
                                    if (parent != null)
                                    {
                                        var parentType = parent.GetType();
                                        var parentIdProp = parentType.GetProperty("InstanceId");
                                        if (parentIdProp != null)
                                        {
                                            var parentIdVal = parentIdProp.GetValue(parent);
                                            uint parentId = parentIdVal is uint pid ? pid : 0;

                                            // Get sibling name for logging
                                            uint sibGrpId = 0;
                                            var sibGrpIdProp = siblingModelType.GetProperty("GrpId");
                                            if (sibGrpIdProp != null)
                                            {
                                                var gidVal = sibGrpIdProp.GetValue(siblingModel);
                                                if (gidVal is uint gid) sibGrpId = gid;
                                            }
                                            string sibName = sibGrpId > 0 ? GetNameFromGrpId(sibGrpId) : sibling.name;

                                            MelonLogger.Msg($"[CardModelProvider] Sibling {sibName} has Parent.InstanceId={parentId}, looking for {thisInstanceId}");

                                            if (parentId == thisInstanceId)
                                            {
                                                // This sibling is attached to our card
                                                uint instanceId = 0;
                                                var sibInstanceIdProp = siblingModelType.GetProperty("InstanceId");
                                                if (sibInstanceIdProp != null)
                                                {
                                                    var idVal = sibInstanceIdProp.GetValue(siblingModel);
                                                    if (idVal is uint uid) instanceId = uid;
                                                }

                                                if (sibGrpId > 0)
                                                {
                                                    // Avoid duplicates
                                                    if (!attachments.Any(a => a.instanceId == instanceId))
                                                    {
                                                        attachments.Add((instanceId, sibGrpId, sibName));
                                                        MelonLogger.Msg($"[CardModelProvider] Found sibling attachment: {sibName} (Parent matches InstanceId={thisInstanceId})");
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Log siblings with null Parent
                                        uint sibGrpId = 0;
                                        var sibGrpIdProp = siblingModelType.GetProperty("GrpId");
                                        if (sibGrpIdProp != null)
                                        {
                                            var gidVal = sibGrpIdProp.GetValue(siblingModel);
                                            if (gidVal is uint gid) sibGrpId = gid;
                                        }
                                        string sibName = sibGrpId > 0 ? GetNameFromGrpId(sibGrpId) : sibling.name;
                                        MelonLogger.Msg($"[CardModelProvider] Sibling {sibName} has Parent=null");
                                    }
                                }
                            }

                            MelonLogger.Msg($"[CardModelProvider] Checked {siblingCount} siblings, {siblingCdcCount} with CDC components");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CardModelProvider] Error getting visual attachments: {ex.Message}");
            }

            return attachments;
        }

        /// <summary>
        /// Gets the card that this card is attached to (for auras/equipment).
        /// Returns null if not attached to anything.
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
                var modelType = model.GetType();
                object parent = null;

                // Debug: Get card name first
                uint cardGrpId = 0;
                var cardGrpIdProp = modelType.GetProperty("GrpId");
                if (cardGrpIdProp != null)
                {
                    var gidVal = cardGrpIdProp.GetValue(model);
                    if (gidVal is uint gid) cardGrpId = gid;
                }
                string cardName = cardGrpId > 0 ? GetNameFromGrpId(cardGrpId) : card.name;

                // Try Parent directly on model first (CardData has Parent property)
                var parentProp = modelType.GetProperty("Parent");
                if (parentProp != null)
                {
                    parent = parentProp.GetValue(model);
                    MelonLogger.Msg($"[CardModelProvider] GetAttachedTo({cardName}): Model.Parent = {(parent != null ? parent.ToString() : "null")}");
                }

                // Also check the Instance property which might have Parent
                if (parent == null)
                {
                    var instanceProp = modelType.GetProperty("Instance");
                    object instance = instanceProp?.GetValue(model);

                    if (instance != null)
                    {
                        var instanceType = instance.GetType();
                        var instParentProp = instanceType.GetProperty("Parent");
                        if (instParentProp != null)
                        {
                            parent = instParentProp.GetValue(instance);
                            MelonLogger.Msg($"[CardModelProvider] GetAttachedTo({cardName}): Instance.Parent = {(parent != null ? parent.ToString() : "null")}");
                        }
                    }
                }

                if (parent == null)
                {
                    MelonLogger.Msg($"[CardModelProvider] GetAttachedTo({cardName}): No parent found");
                    return null;
                }

                var parentType = parent.GetType();

                // Get InstanceId
                uint instanceId = 0;
                var instanceIdProp = parentType.GetProperty("InstanceId");
                if (instanceIdProp != null)
                {
                    var idVal = instanceIdProp.GetValue(parent);
                    if (idVal is uint uid) instanceId = uid;
                }

                // Get GrpId for name lookup
                uint grpId = 0;
                var grpIdProp = parentType.GetProperty("GrpId");
                if (grpIdProp != null)
                {
                    var gidVal = grpIdProp.GetValue(parent);
                    if (gidVal is uint gid) grpId = gid;
                }

                // Get name from GrpId
                string name = null;
                if (grpId > 0)
                {
                    name = GetNameFromGrpId(grpId);
                }

                if (instanceId > 0)
                {
                    MelonLogger.Msg($"[CardModelProvider] Card is attached to: InstanceId={instanceId}, GrpId={grpId}, Name={name ?? "unknown"}");
                    return (instanceId, grpId, name);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CardModelProvider] Error getting attached-to info: {ex.Message}");
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
                    result.Add($"enchanted by {names[0]}");
                }
                else
                {
                    result.Add($"enchanted by {string.Join(", ", names)}");
                }
            }

            // Check if this card IS attached to something (aura/equipment itself)
            var attachedTo = GetAttachedTo(card);
            if (attachedTo.HasValue && !string.IsNullOrEmpty(attachedTo.Value.name))
            {
                result.Add($"attached to {attachedTo.Value.name}");
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

                MelonLogger.Msg($"[CardModelProvider] IsAbilityOnStack: hasSpellType={hasSpellType}, hasAbilityType={hasAbilityType}");

                // If has explicit Ability type or no spell types, it's an ability
                if (hasAbilityType || !hasSpellType)
                {
                    // Try to determine if triggered vs activated
                    // Check for AbilityType, TriggerType, or similar properties
                    var abilityType = GetModelPropertyValue(model, modelType, "AbilityType");
                    var triggerType = GetModelPropertyValue(model, modelType, "TriggerType");
                    var abilityCategory = GetModelPropertyValue(model, modelType, "AbilityCategory");

                    MelonLogger.Msg($"[CardModelProvider] Ability properties: AbilityType={abilityType}, TriggerType={triggerType}, AbilityCategory={abilityCategory}");

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
                        MelonLogger.Msg($"[CardModelProvider] Error in GetCardCategory: {ex.Message}");
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
    }
}
