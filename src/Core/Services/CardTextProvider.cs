using UnityEngine;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Provides localized card text lookups: ability text, flavor text, artist names.
    /// Uses reflection to find game providers (AbilityTextProvider, GreLocProvider, ArtistProvider).
    /// Split from CardModelProvider for separation of concerns.
    /// </summary>
    public static class CardTextProvider
    {
        // Cache for ability text provider
        private static object _abilityTextProvider = null;
        private static MethodInfo _getAbilityTextMethod = null;
        private static bool _abilityTextProviderSearched = false;

        // Cache for flavor text / localization provider
        private static object _flavorTextProvider = null;
        private static MethodInfo _getFlavorTextMethod = null;
        private static bool _flavorTextProviderSearched = false;

        // Cache for artist provider
        private static object _artistProvider = null;
        private static MethodInfo _getArtistMethod = null;
        private static bool _artistProviderSearched = false;

        /// <summary>
        /// Clears all text provider caches. Call when scene changes.
        /// </summary>
        public static void ClearCache()
        {
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

        /// <summary>
        /// Extracts a loyalty cost prefix from an ability object (e.g., "+2: " or "-3: ").
        /// Returns null if the ability has no LoyaltyCost or it is empty/zero.
        /// </summary>
        internal static string GetLoyaltyCostPrefix(object ability, Type abilityType)
        {
            try
            {
                var loyaltyCostProp = abilityType.GetProperty("LoyaltyCost", PublicInstance);
                if (loyaltyCostProp == null) return null;

                var loyaltyCostObj = loyaltyCostProp.GetValue(ability);
                if (loyaltyCostObj == null) return null;

                // LoyaltyCost is a StringBackedInt - extract the raw text value
                string costStr = CardModelProvider.GetStringBackedIntValue(loyaltyCostObj);
                if (string.IsNullOrEmpty(costStr) || costStr == "0") return null;

                // Ensure positive values get "+" prefix
                if (costStr[0] != '+' && costStr[0] != '-' && costStr[0] != '0')
                    costStr = "+" + costStr;

                return costStr + ": ";
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tries to extract text from an ability object by checking common property/method names.
        /// Returns null if no text could be extracted.
        /// </summary>
        internal static string GetAbilityText(object ability, Type abilityType, uint cardGrpId, uint abilityId, uint[] abilityIds, uint cardTitleId)
        {
            // First try to look up via AbilityTextProvider with full card context
            var text = GetAbilityTextFromProvider(cardGrpId, abilityId, abilityIds, cardTitleId);
            if (!string.IsNullOrEmpty(text))
                return text;

            // Try common property names for ability text
            string[] propertyNames = { "Text", "RulesText", "AbilityText", "TextContent", "Description" };
            foreach (var propName in propertyNames)
            {
                var prop = abilityType.GetProperty(propName, PublicInstance);
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
            var getTextMethod = abilityType.GetMethod("GetText", PublicInstance, null, Type.EmptyTypes, null);
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
        /// </summary>
        internal static string GetAbilityTextFromProvider(uint cardGrpId, uint abilityId, uint[] abilityIds, uint cardTitleId)
        {
            if (!_abilityTextProviderSearched || _getAbilityTextMethod == null)
            {
                _abilityTextProviderSearched = true;
                FindAbilityTextProvider();
            }

            if (_getAbilityTextMethod == null || _abilityTextProvider == null)
                return null;

            try
            {
                var parameters = _getAbilityTextMethod.GetParameters();
                object result = null;

                if (parameters.Length == 6)
                {
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
                    result = _getAbilityTextMethod.Invoke(_abilityTextProvider, new object[] { abilityId });
                }

                string text = result?.ToString();
                if (!string.IsNullOrEmpty(text) && !text.StartsWith("$") && !text.StartsWith("#") && !text.StartsWith("Ability #") && !text.Contains("Unknown"))
                {
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"Ability {abilityId} -> {text}");
                    return text;
                }
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"Error looking up ability {abilityId}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Searches for the ability text provider in the game.
        /// </summary>
        private static void FindAbilityTextProvider()
        {
            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"Searching for ability text provider...");

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                var type = mb.GetType();
                if (type.Name == T.GameManager)
                {
                    var cardDbProp = type.GetProperty("CardDatabase");
                    if (cardDbProp != null)
                    {
                        var cardDb = cardDbProp.GetValue(mb);
                        if (cardDb != null)
                        {
                            var cardDbType = cardDb.GetType();

                            foreach (var prop in cardDbType.GetProperties(PublicInstance))
                            {
                                if (prop.Name.Contains("Text") || prop.Name.Contains("Ability"))
                                {
                                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"CardDatabase.{prop.Name} ({prop.PropertyType.Name})");

                                    var provider = prop.GetValue(cardDb);
                                    if (provider != null)
                                    {
                                        var providerType = provider.GetType();
                                        foreach (var m in providerType.GetMethods(PublicInstance))
                                        {
                                            if (m.DeclaringType == typeof(object)) continue;
                                            var paramStr = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                                            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"  {m.Name}({paramStr}) -> {m.ReturnType.Name}");

                                            if (m.ReturnType == typeof(string))
                                            {
                                                var mParams = m.GetParameters();
                                                if (mParams.Length >= 1 && mParams[0].ParameterType == typeof(uint))
                                                {
                                                    _abilityTextProvider = provider;
                                                    _getAbilityTextMethod = m;
                                                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"Using {prop.Name}.{m.Name} for ability text lookup");
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

                                foreach (var prop in cardDbType.GetProperties(PublicInstance))
                                {
                                    if (prop.Name.Contains("Text") || prop.Name.Contains("Ability"))
                                    {
                                        var provider = prop.GetValue(cardDb);
                                        if (provider != null)
                                        {
                                            var providerType = provider.GetType();
                                            foreach (var m in providerType.GetMethods(PublicInstance))
                                            {
                                                if (m.DeclaringType == typeof(object)) continue;
                                                if (m.ReturnType == typeof(string))
                                                {
                                                    var mParams = m.GetParameters();
                                                    if (mParams.Length >= 1 && mParams[0].ParameterType == typeof(uint))
                                                    {
                                                        _abilityTextProvider = provider;
                                                        _getAbilityTextMethod = m;
                                                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"Using {typeName}.CardDatabase.{prop.Name}.{m.Name} for ability text lookup");
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

            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"No ability text provider found");
        }

        /// <summary>
        /// Searches for the flavor text provider in the game.
        /// FlavorTextId is a localization key looked up via GreLocProvider or ClientLocProvider.
        /// </summary>
        private static void FindFlavorTextProvider()
        {
            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"Searching for flavor text provider...");

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                var type = mb.GetType();
                if (type.Name == T.GameManager)
                {
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"Found GameManager, looking for CardDatabase...");
                    var cardDbProp = type.GetProperty("CardDatabase");
                    if (cardDbProp != null)
                    {
                        var cardDb = cardDbProp.GetValue(mb);
                        if (cardDb != null)
                        {
                            var cardDbType = cardDb.GetType();

                            var greLocProp = cardDbType.GetProperty("GreLocProvider");
                            if (greLocProp != null)
                            {
                                var greLocProvider = greLocProp.GetValue(cardDb);
                                if (greLocProvider != null)
                                {
                                    var providerType = greLocProvider.GetType();
                                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"Found GreLocProvider: {providerType.FullName}");

                                    foreach (var m in providerType.GetMethods(PublicInstance))
                                    {
                                        if (m.DeclaringType == typeof(object)) continue;
                                        var paramStr = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"  GreLocProvider.{m.Name}({paramStr}) -> {m.ReturnType.Name}");

                                        if (m.ReturnType == typeof(string) &&
                                            (m.Name == "GetString" || m.Name == "GetText" || m.Name == "Get" || m.Name.Contains("Loc")))
                                        {
                                            var mParams = m.GetParameters();
                                            if (mParams.Length >= 1 && mParams[0].ParameterType == typeof(uint))
                                            {
                                                _flavorTextProvider = greLocProvider;
                                                _getFlavorTextMethod = m;
                                                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"Using GreLocProvider.{m.Name} for flavor text lookup");
                                                return;
                                            }
                                        }
                                    }
                                }
                            }

                            var clientLocProp = cardDbType.GetProperty("ClientLocProvider");
                            if (clientLocProp != null)
                            {
                                var clientLocProvider = clientLocProp.GetValue(cardDb);
                                if (clientLocProvider != null)
                                {
                                    var providerType = clientLocProvider.GetType();
                                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"Found ClientLocProvider: {providerType.FullName}");

                                    foreach (var m in providerType.GetMethods(PublicInstance))
                                    {
                                        if (m.DeclaringType == typeof(object)) continue;
                                        var paramStr = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"  ClientLocProvider.{m.Name}({paramStr}) -> {m.ReturnType.Name}");

                                        if (m.ReturnType == typeof(string))
                                        {
                                            var mParams = m.GetParameters();
                                            if (mParams.Length >= 1 && mParams[0].ParameterType == typeof(uint))
                                            {
                                                _flavorTextProvider = clientLocProvider;
                                                _getFlavorTextMethod = m;
                                                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"Using ClientLocProvider.{m.Name} for flavor text lookup");
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

                                var greLocProp = cardDbType.GetProperty("GreLocProvider");
                                if (greLocProp != null)
                                {
                                    var greLocProvider = greLocProp.GetValue(cardDb);
                                    if (greLocProvider != null)
                                    {
                                        var providerType = greLocProvider.GetType();
                                        foreach (var m in providerType.GetMethods(PublicInstance))
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
                                                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"Using {typeName}.CardDatabase.GreLocProvider.{m.Name} for flavor text lookup");
                                                    return;
                                                }
                                            }
                                        }
                                    }
                                }

                                var clientLocProp = cardDbType.GetProperty("ClientLocProvider");
                                if (clientLocProp != null)
                                {
                                    var clientLocProvider = clientLocProp.GetValue(cardDb);
                                    if (clientLocProvider != null)
                                    {
                                        var providerType = clientLocProvider.GetType();
                                        foreach (var m in providerType.GetMethods(PublicInstance))
                                        {
                                            if (m.DeclaringType == typeof(object)) continue;
                                            if (m.ReturnType == typeof(string))
                                            {
                                                var mParams = m.GetParameters();
                                                if (mParams.Length >= 1 && mParams[0].ParameterType == typeof(uint))
                                                {
                                                    _flavorTextProvider = clientLocProvider;
                                                    _getFlavorTextMethod = m;
                                                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"Using {typeName}.CardDatabase.ClientLocProvider.{m.Name} for flavor text lookup");
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

            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"No flavor text provider found");
        }

        /// <summary>
        /// Searches for the artist provider in the game.
        /// </summary>
        private static void FindArtistProvider()
        {
            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"Searching for artist provider...");

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                var type = mb.GetType();
                if (type.Name == T.GameManager)
                {
                    var cardDbProp = type.GetProperty("CardDatabase");
                    if (cardDbProp != null)
                    {
                        var cardDb = cardDbProp.GetValue(mb);
                        if (cardDb != null)
                        {
                            var cardDbType = cardDb.GetType();

                            foreach (var prop in cardDbType.GetProperties(PublicInstance))
                            {
                                if (prop.Name.Contains("Artist"))
                                {
                                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"CardDatabase.{prop.Name} ({prop.PropertyType.Name})");

                                    var provider = prop.GetValue(cardDb);
                                    if (provider != null)
                                    {
                                        var providerType = provider.GetType();
                                        foreach (var m in providerType.GetMethods(PublicInstance))
                                        {
                                            if (m.DeclaringType == typeof(object)) continue;

                                            if (m.ReturnType == typeof(string))
                                            {
                                                var mParams = m.GetParameters();
                                                if (mParams.Length >= 1 && mParams[0].ParameterType == typeof(uint))
                                                {
                                                    _artistProvider = provider;
                                                    _getArtistMethod = m;
                                                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"Using {prop.Name}.{m.Name} for artist lookup");
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

            DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardTextProvider", $"No artist provider found");
        }

        /// <summary>
        /// Gets the flavor text for a card using its FlavorId.
        /// Uses GreLocProvider.GetLocalizedText via GetLocalizedTextById.
        /// </summary>
        internal static string GetFlavorText(uint flavorId)
        {
            if (flavorId == 0 || flavorId == 1) return null;

            var text = GetLocalizedTextById(flavorId);
            if (text != null && text.Contains("Unknown"))
                return null;
            return text;
        }

        /// <summary>
        /// Looks up a localized text string by its localization ID using GreLocProvider.
        /// Reuses the flavor text provider (same GreLocProvider.GetLocalizedText method).
        /// Works for any loc ID: TypeTextId, SubtypeTextId, FlavorTextId, etc.
        /// </summary>
        internal static string GetLocalizedTextById(uint locId)
        {
            if (locId == 0) return null;

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
                    result = _getFlavorTextMethod.Invoke(_flavorTextProvider, new object[] { locId, null, false });
                }
                else if (parameters.Length == 1)
                {
                    result = _getFlavorTextMethod.Invoke(_flavorTextProvider, new object[] { locId });
                }
                else
                {
                    return null;
                }

                var text = result as string;
                if (!string.IsNullOrEmpty(text) && !text.StartsWith("$"))
                    return text;
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the artist name for a card using its ArtistId.
        /// </summary>
        internal static string GetArtistName(uint artistId)
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
    }
}
