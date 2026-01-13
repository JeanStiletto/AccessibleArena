using UnityEngine;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        // Cache for Model reflection to improve performance
        private static Type _cachedModelType = null;
        private static readonly Dictionary<string, PropertyInfo> _modelPropertyCache = new Dictionary<string, PropertyInfo>();
        private static bool _modelPropertiesLogged = false;
        private static bool _abilityPropertiesLogged = false;

        // Cache for IdNameProvider lookup
        private static object _idNameProvider = null;
        private static MethodInfo _getNameMethod = null;
        private static bool _idNameProviderSearched = false;

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
            _modelPropertyCache.Clear();
            _cachedModelType = null;
            // Reset IdNameProvider on scene change - it may be different in new scene
            _idNameProvider = null;
            _getNameMethod = null;
            _idNameProviderSearched = false;
            // Reset ability logging so we log again on scene change
            _abilityPropertiesLogged = false;
            // Reset ability text provider on scene change
            _abilityTextProvider = null;
            _getAbilityTextMethod = null;
            _abilityTextProviderSearched = false;
        }

        #region DuelScene Model Access

        /// <summary>
        /// Gets the DuelScene_CDC component from a card GameObject.
        /// Returns null if not a DuelScene card (e.g., Meta scene cards like rewards, deck building).
        /// </summary>
        public static Component GetDuelSceneCDC(GameObject card)
        {
            if (card == null) return null;

            foreach (var component in card.GetComponents<Component>())
            {
                if (component != null && component.GetType().Name == "DuelScene_CDC")
                {
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
                MelonLogger.Msg($"[CardDetector] Error getting Model: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds and caches an IdNameProvider instance for card name lookup.
        /// Searches for GameManager, WrapperController, or IdNameProvider components.
        /// </summary>
        private static void FindIdNameProvider()
        {
            if (_idNameProviderSearched) return;
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
                        MelonLogger.Msg("[CardDetector] Found GameManager, checking for name lookup methods...");

                        // Try CardDatabase property - get CardTitleProvider or CardNameTextProvider
                        var cardDbProp = type.GetProperty("CardDatabase");
                        if (cardDbProp != null)
                        {
                            var cardDb = cardDbProp.GetValue(mb);
                            if (cardDb != null)
                            {
                                var cardDbType = cardDb.GetType();
                                MelonLogger.Msg($"[CardDetector] CardDatabase type: {cardDbType.FullName}");

                                // Try CardTitleProvider
                                var titleProviderProp = cardDbType.GetProperty("CardTitleProvider");
                                if (titleProviderProp != null)
                                {
                                    var titleProvider = titleProviderProp.GetValue(cardDb);
                                    if (titleProvider != null)
                                    {
                                        var providerType = titleProvider.GetType();
                                        MelonLogger.Msg($"[CardDetector] CardTitleProvider type: {providerType.FullName}");

                                        // List methods on the provider
                                        foreach (var m in providerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                        {
                                            if (m.DeclaringType == typeof(object)) continue;
                                            MelonLogger.Msg($"[CardDetector] CardTitleProvider.{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))}) -> {m.ReturnType.Name}");
                                        }

                                        // Use GetCardTitle(UInt32, Boolean, String) method
                                        var getMethod = providerType.GetMethod("GetCardTitle", new[] { typeof(uint), typeof(bool), typeof(string) });

                                        if (getMethod != null)
                                        {
                                            _idNameProvider = titleProvider;
                                            _getNameMethod = getMethod;
                                            MelonLogger.Msg($"[CardDetector] Using CardTitleProvider.GetCardTitle for name lookup");
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
                                        MelonLogger.Msg($"[CardDetector] CardNameTextProvider type: {providerType.FullName}");

                                        // List methods on the provider
                                        foreach (var m in providerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                        {
                                            if (m.DeclaringType == typeof(object)) continue;
                                            MelonLogger.Msg($"[CardDetector] CardNameTextProvider.{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))}) -> {m.ReturnType.Name}");
                                        }

                                        // Try common method names
                                        var getMethod = providerType.GetMethod("GetName", new[] { typeof(uint) })
                                            ?? providerType.GetMethod("GetText", new[] { typeof(uint) })
                                            ?? providerType.GetMethod("Get", new[] { typeof(uint) });

                                        if (getMethod != null)
                                        {
                                            _idNameProvider = nameProvider;
                                            _getNameMethod = getMethod;
                                            MelonLogger.Msg($"[CardDetector] Using CardNameTextProvider.{getMethod.Name} for name lookup");
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
                                MelonLogger.Msg($"[CardDetector] LocManager type: {locType.FullName}");

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
                                            MelonLogger.Msg($"[CardDetector] Found GetLocalizedText: {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
                                            break;
                                        }
                                    }
                                }

                                if (getTextMethod != null)
                                {
                                    _idNameProvider = locMgr;
                                    _getNameMethod = getTextMethod;
                                    MelonLogger.Msg("[CardDetector] Using LocManager.GetLocalizedText for name lookup");
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
                            MelonLogger.Msg($"[CardDetector] Found {type.Name} for name lookup");
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
                                            MelonLogger.Msg("[CardDetector] Found WrapperController.CardDatabase for name lookup");
                                            return;
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    }
                }

                MelonLogger.Msg("[CardDetector] IdNameProvider not found - will use UI fallback for names");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CardDetector] Error finding IdNameProvider: {ex.Message}");
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
                MelonLogger.Msg($"[CardDetector] GrpId is 0, cannot lookup name");
                return null;
            }

            FindIdNameProvider();

            if (_getNameMethod == null)
            {
                MelonLogger.Msg($"[CardDetector] No localization method found for GrpId {grpId}");
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
                    MelonLogger.Msg($"[CardDetector] GrpId {grpId} -> Name: {name}");
                    return name;
                }

                MelonLogger.Msg($"[CardDetector] GrpId {grpId}: No valid name found (result: {name ?? "null"})");
                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CardDetector] Error getting name from GrpId {grpId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Logs all properties available on the Model object for discovery.
        /// Only logs once per session to avoid log spam.
        /// </summary>
        public static void LogModelProperties(object model)
        {
            if (_modelPropertiesLogged || model == null) return;
            _modelPropertiesLogged = true;

            var modelType = model.GetType();
            MelonLogger.Msg($"[CardDetector] === MODEL TYPE: {modelType.FullName} ===");

            var properties = modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(model);
                    string valueStr = value?.ToString() ?? "null";
                    if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                    MelonLogger.Msg($"[CardDetector] Property: {prop.Name} = {valueStr} ({prop.PropertyType.Name})");
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[CardDetector] Property: {prop.Name} = [Error: {ex.Message}] ({prop.PropertyType.Name})");
                }
            }
            MelonLogger.Msg($"[CardDetector] === END MODEL PROPERTIES ===");
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
            MelonLogger.Msg($"[CardDetector] === ABILITY TYPE: {abilityType.FullName} ===");

            // Log properties
            var properties = abilityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(ability);
                    string valueStr = value?.ToString() ?? "null";
                    if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                    MelonLogger.Msg($"[CardDetector] Ability Property: {prop.Name} = {valueStr} ({prop.PropertyType.Name})");
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[CardDetector] Ability Property: {prop.Name} = [Error: {ex.Message}] ({prop.PropertyType.Name})");
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
                        MelonLogger.Msg($"[CardDetector] Ability Method: {method.Name}() = {resultStr}");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Msg($"[CardDetector] Ability Method: {method.Name}() = [Error: {ex.Message}]");
                    }
                }
            }

            MelonLogger.Msg($"[CardDetector] === END ABILITY PROPERTIES ===");
        }

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

        // Cache for ability text provider
        private static object _abilityTextProvider = null;
        private static MethodInfo _getAbilityTextMethod = null;
        private static bool _abilityTextProviderSearched = false;

        /// <summary>
        /// Tries to get ability text using the AbilityTextProvider with full card context.
        /// Method signature: GetAbilityTextByCardAbilityGrpId(cardGrpId, abilityGrpId, abilityIds, cardTitleId, overrideLanguageCode, formatted)
        /// </summary>
        private static string GetAbilityTextFromProvider(uint cardGrpId, uint abilityId, uint[] abilityIds, uint cardTitleId)
        {
            // First try to find the ability text provider if we haven't already
            if (!_abilityTextProviderSearched)
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
                    MelonLogger.Msg($"[CardDetector] Ability {abilityId} -> {text}");
                    return text;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CardDetector] Error looking up ability {abilityId}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Searches for the ability text provider in the game.
        /// </summary>
        private static void FindAbilityTextProvider()
        {
            MelonLogger.Msg("[CardDetector] Searching for ability text provider...");

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
                                    MelonLogger.Msg($"[CardDetector] CardDatabase.{prop.Name} ({prop.PropertyType.Name})");

                                    var provider = prop.GetValue(cardDb);
                                    if (provider != null)
                                    {
                                        var providerType = provider.GetType();
                                        foreach (var m in providerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                        {
                                            if (m.DeclaringType == typeof(object)) continue;
                                            var paramStr = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                                            MelonLogger.Msg($"[CardDetector]   {m.Name}({paramStr}) -> {m.ReturnType.Name}");

                                            // Look for methods that take uint and return string
                                            if (m.ReturnType == typeof(string))
                                            {
                                                var mParams = m.GetParameters();
                                                if (mParams.Length >= 1 && mParams[0].ParameterType == typeof(uint))
                                                {
                                                    _abilityTextProvider = provider;
                                                    _getAbilityTextMethod = m;
                                                    MelonLogger.Msg($"[CardDetector] Using {prop.Name}.{m.Name} for ability text lookup");
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

            MelonLogger.Msg("[CardDetector] No ability text provider found");
        }

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

        /// <summary>
        /// Parses a ManaQuantity[] array into a readable mana cost string.
        /// Each ManaQuantity represents ONE mana symbol.
        /// </summary>
        private static string ParseManaQuantityArray(IEnumerable manaQuantities)
        {
            var symbols = new List<string>();
            int genericCount = 0;

            foreach (var mq in manaQuantities)
            {
                if (mq == null) continue;

                var mqType = mq.GetType();

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

                    if (isGeneric)
                    {
                        // Generic/colorless mana - count them up
                        genericCount++;
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

                        symbols.Add(symbol);
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

        /// <summary>
        /// Extracts card information from the game's internal Model data.
        /// This works for battlefield cards that may have hidden/compacted UI text.
        /// Returns null if Model data is not available (e.g., Meta scene cards).
        /// </summary>
        public static CardInfo? ExtractCardInfoFromModel(GameObject cardObj)
        {
            if (cardObj == null) return null;

            var cdcComponent = GetDuelSceneCDC(cardObj);
            if (cdcComponent == null) return null;

            var model = GetCardModel(cdcComponent);
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
                        info.RulesText = string.Join(" ", rulesLines);
                        MelonLogger.Msg($"[CardDetector] Extracted rules text: {info.RulesText}");
                    }
                }

                // Flavor Text - not on Model
                // Artist - not on Model

                info.IsValid = !string.IsNullOrEmpty(info.Name);
                return info;
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CardDetector] Error extracting model data: {ex.Message}");
                return null;
            }
        }

        #endregion

        /// <summary>
        /// Extracts all available information from a card GameObject.
        /// For DuelScene cards, tries Model data first (works for compacted cards).
        /// Falls back to UI text extraction for Meta scene cards or if Model fails.
        /// </summary>
        public static CardInfo ExtractCardInfo(GameObject cardObj)
        {
            if (cardObj == null) return new CardInfo();

            // Try Model-based extraction first (works for compacted battlefield cards)
            var modelInfo = ExtractCardInfoFromModel(cardObj);
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

        /// <summary>
        /// Checks if any cards on the battlefield or stack have HotHighlight (indicating targeting mode).
        /// Used to detect when the game is waiting for target selection.
        /// </summary>
        public static bool HasValidTargetsOnBattlefield()
        {
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy)
                    continue;

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

                if (!inTargetZone)
                    continue;

                // Check for HotHighlight child (indicates valid target)
                foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                {
                    if (child.gameObject.activeInHierarchy && child.name.Contains("HotHighlight"))
                    {
                        return true;
                    }
                }
            }

            return false;
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
