using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AccessibleArena.Core.Models;
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

        // Cache for IGreLocProvider.GetLocalizedTextForEnumValue(string, int, bool, string) —
        // resolved off the same GreLocProvider instance as _flavorTextProvider.
        private static MethodInfo _getEnumValueMethod = null;
        private static bool _getEnumValueMethodSearched = false;

        // Cache for artist provider
        private static object _artistProvider = null;
        private static MethodInfo _getArtistMethod = null;
        private static bool _artistProviderSearched = false;

        // Dedup cache for ability-text lookup logs. Same id → same text is pure-function,
        // so we log the first occurrence and only re-log if the resolved text ever differs.
        private static readonly Dictionary<uint, string> _loggedAbilityText = new Dictionary<uint, string>();

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
            _getEnumValueMethod = null;
            _getEnumValueMethodSearched = false;
            _artistProvider = null;
            _getArtistMethod = null;
            _artistProviderSearched = false;
            _loggedAbilityText.Clear();
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

                // Build screen-reader-friendly prefix: "+1" → "Plus 1", "-3" → "Minus 3"
                string prefix;
                if (costStr[0] == '-')
                    prefix = Strings.LoyaltyMinus + " " + costStr.Substring(1);
                else if (costStr[0] == '+')
                    prefix = Strings.LoyaltyPlus + " " + costStr.Substring(1);
                else
                    prefix = Strings.LoyaltyPlus + " " + costStr;

                return prefix + ": ";
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
                    catch { /* Ability text property may throw on some card types */ }
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
                catch { /* GetText() invocation may fail on some ability types */ }
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
                var args = BuildAbilityTextArgs(_getAbilityTextMethod, cardGrpId, abilityId, abilityIds, cardTitleId);
                object result = args != null ? _getAbilityTextMethod.Invoke(_abilityTextProvider, args) : null;

                string text = result?.ToString();
                if (!string.IsNullOrEmpty(text) && !text.StartsWith("$") && !text.StartsWith("#") && !text.StartsWith("Ability #") && !text.Contains("Unknown"))
                {
                    if (!_loggedAbilityText.TryGetValue(abilityId, out var prevText))
                    {
                        _loggedAbilityText[abilityId] = text;
                        Log.Card("CardTextProvider", $"Ability {abilityId} -> {text}");
                    }
                    else if (prevText != text)
                    {
                        _loggedAbilityText[abilityId] = text;
                        Log.Card("CardTextProvider", $"Ability {abilityId} CHANGED: {prevText} -> {text}");
                    }
                    return text;
                }
            }
            catch (Exception ex)
            {
                Log.Card("CardTextProvider", $"Error looking up ability {abilityId}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Builds the argument array for the resolved ability-text method, keyed off its first
        /// parameter type so it works across the API rename. Two live shapes (game 2026.60):
        ///   GetAbilityTextByCardGrpIds(IEnumerable&lt;uint&gt; cardGrpIds, uint abilityGrpId, string lang, bool formatted)
        ///   GetKeywordText(uint abilityGrpId, string lang, bool formatted)
        /// The legacy 6-param GetAbilityTextByCardAbilityGrpId(uint, uint, IEnumerable, uint, ...)
        /// is also still handled. Returns null if the signature is unrecognized. We always request
        /// formatted=false to get raw MTGA mana notation, which ManaTextFormatter parses downstream.
        /// </summary>
        private static object[] BuildAbilityTextArgs(MethodInfo method, uint cardGrpId, uint abilityId, uint[] abilityIds, uint cardTitleId)
        {
            var ps = method.GetParameters();
            if (ps.Length == 0) return null;

            IEnumerable<uint> cardGrpIds = cardGrpId != 0 ? new[] { cardGrpId } : Array.Empty<uint>();
            IEnumerable<uint> abilityIdsList = abilityIds ?? Array.Empty<uint>();

            // Legacy: GetAbilityTextByCardAbilityGrpId(uint cardGrpId, uint abilityGrpId, IEnumerable, uint titleId, string, bool)
            if (ps.Length == 6 && ps[0].ParameterType == typeof(uint))
                return new object[] { cardGrpId, abilityId, abilityIdsList, cardTitleId, null, false };

            // New card-context: GetAbilityTextByCardGrpIds(IEnumerable<uint> cardGrpIds, uint abilityGrpId, string, bool)
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(ps[0].ParameterType) && ps[0].ParameterType != typeof(string))
                return BuildLeading(ps, cardGrpIds, abilityId);

            // Keyword fallback: GetKeywordText(uint abilityGrpId, string, bool)
            if (ps[0].ParameterType == typeof(uint))
                return BuildLeading(ps, abilityId);

            return null;
        }

        /// <summary>
        /// Builds an argument array of the method's parameter length: the supplied leading values
        /// fill the first slots; trailing params get their natural default (null for reference
        /// types — which is the real default for overrideLanguageCode — and default(T) for value
        /// types) except a trailing bool 'formatted', which is forced to false for raw text.
        /// (We pass explicit defaults rather than Type.Missing because the plain Invoke overload
        /// does not honor optional-parameter binding.)
        /// </summary>
        private static object[] BuildLeading(ParameterInfo[] ps, params object[] leading)
        {
            var args = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                if (i < leading.Length)
                    args[i] = leading[i];
                else if (ps[i].ParameterType == typeof(bool) && ps[i].Name == "formatted")
                    args[i] = false;
                else
                    args[i] = ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null;
            }
            return args;
        }

        /// <summary>
        /// Returns the first public instance method on <paramref name="providerType"/> that returns
        /// string and takes a uint as its first parameter. Optional <paramref name="nameFilter"/>
        /// further narrows the search (e.g. to localization-style method names). Skips
        /// <see cref="object"/>-declared methods.
        /// </summary>
        private static MethodInfo FindStringFromUintMethod(Type providerType, Predicate<MethodInfo> nameFilter = null)
        {
            foreach (var m in providerType.GetMethods(PublicInstance))
            {
                if (m.DeclaringType == typeof(object)) continue;
                if (m.ReturnType != typeof(string)) continue;
                if (nameFilter != null && !nameFilter(m)) continue;
                var ps = m.GetParameters();
                if (ps.Length >= 1 && ps[0].ParameterType == typeof(uint))
                    return m;
            }
            return null;
        }

        /// <summary>
        /// Walks scene MonoBehaviours looking for a <c>CardDatabase</c> property and invokes
        /// <paramref name="extractor"/> on each one found. First pass: the GameManager (duel
        /// scene). Second pass: any Card*/Wrapper*/Manager* with a CardDatabase (meta scenes),
        /// wrapped in try/catch because those types are version-sensitive. Returns the first
        /// non-null extractor result, or null if nothing matched.
        /// </summary>
        private static (object provider, MethodInfo method)? SearchCardDatabaseProviders(
            string label,
            Func<object, (object, MethodInfo)?> extractor)
        {
            Log.Card("CardTextProvider", $"Searching for {label}...");

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (mb.GetType().Name != T.GameManager) continue;
                var cardDb = mb.GetType().GetProperty("CardDatabase")?.GetValue(mb);
                if (cardDb != null)
                {
                    var r = extractor(cardDb);
                    if (r.HasValue) return r;
                }
                break;
            }

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;
                var typeName = mb.GetType().Name;
                if (!(typeName.Contains("Card") || typeName.Contains("Wrapper") || typeName.Contains("Manager")))
                    continue;
                try
                {
                    var cardDb = mb.GetType().GetProperty("CardDatabase")?.GetValue(mb);
                    if (cardDb != null)
                    {
                        var r = extractor(cardDb);
                        if (r.HasValue) return r;
                    }
                }
                catch { /* CardDatabase reflection is version-sensitive */ }
            }

            Log.Card("CardTextProvider", $"No {label} found");
            return null;
        }

        /// <summary>
        /// Scans <paramref name="cardDb"/> for properties matching <paramref name="propFilter"/>
        /// and returns the first (provider, method) pair where the provider exposes a
        /// string(uint, ...) method passing <paramref name="methodFilter"/>.
        /// </summary>
        private static (object, MethodInfo)? ExtractProviderFromCardDb(
            object cardDb,
            Predicate<PropertyInfo> propFilter,
            Predicate<MethodInfo> methodFilter,
            string logLabel)
        {
            foreach (var prop in cardDb.GetType().GetProperties(PublicInstance))
            {
                if (!propFilter(prop)) continue;
                var provider = prop.GetValue(cardDb);
                if (provider == null) continue;
                var m = FindStringFromUintMethod(provider.GetType(), methodFilter);
                if (m != null)
                {
                    Log.Card("CardTextProvider", $"Using {prop.Name}.{m.Name} for {logLabel} lookup");
                    return (provider, m);
                }
            }
            return null;
        }

        /// <summary>
        /// Searches for the ability text provider in the game.
        /// </summary>
        private static void FindAbilityTextProvider()
        {
            // The IAbilityTextProvider API is version-sensitive — game 2026.60 refactored it from
            // GetAbilityTextByCardAbilityGrpId(uint cardGrpId, uint abilityGrpId, IEnumerable<uint>
            // abilityIds, uint titleId, ...) to:
            //   GetAbilityTextByCardGrpIds(IEnumerable<uint> cardGrpIds, uint abilityGrpId, ...)  [preferred — card context]
            //   GetKeywordText(uint abilityGrpId, ...)                                            [fallback — ability default text]
            // We can't reuse FindStringFromUintMethod here: the preferred method's first param is
            // IEnumerable<uint>, not uint, so that helper would skip it and silently grab some
            // other string(uint) method (e.g. GetKeywordText with a param count our invoke can't
            // satisfy → "Number of parameters specified does not match the expected number").
            // Resolve by name, newest-API-first, tolerating the old name if a build still has it.
            string[] methodNames = { "GetAbilityTextByCardGrpIds", "GetAbilityTextByCardAbilityGrpId", "GetKeywordText" };
            var result = SearchCardDatabaseProviders("ability text provider", cardDb =>
            {
                foreach (var prop in cardDb.GetType().GetProperties(PublicInstance))
                {
                    if (!(prop.Name.Contains("Text") || prop.Name.Contains("Ability"))) continue;
                    object provider;
                    try { provider = prop.GetValue(cardDb); }
                    catch { continue; }
                    if (provider == null) continue;

                    var providerType = provider.GetType();
                    foreach (var name in methodNames)
                    {
                        MethodInfo m;
                        // GetMethod throws AmbiguousMatchException if the name ever gains an overload;
                        // fall back to the first string-returning overload of that name.
                        try { m = providerType.GetMethod(name, PublicInstance); }
                        catch (AmbiguousMatchException)
                        {
                            m = providerType.GetMethods(PublicInstance)
                                .FirstOrDefault(x => x.Name == name && x.ReturnType == typeof(string));
                        }
                        if (m != null && m.ReturnType == typeof(string))
                        {
                            Log.Card("CardTextProvider", $"Using {prop.Name}.{m.Name} for ability text lookup");
                            return ((object, MethodInfo)?)(provider, m);
                        }
                    }
                }
                return null;
            });
            if (result.HasValue)
            {
                _abilityTextProvider = result.Value.provider;
                _getAbilityTextMethod = result.Value.method;
            }
        }

        /// <summary>
        /// Searches for the flavor text provider. FlavorTextId is a localization key looked up
        /// via GreLocProvider (preferred) or ClientLocProvider (fallback). GreLocProvider has
        /// many string(uint)-returning methods, so the extra name filter narrows to the
        /// localization-style methods (GetString / GetText / Get / *Loc*).
        /// </summary>
        private static void FindFlavorTextProvider()
        {
            var result = SearchCardDatabaseProviders("flavor text provider", cardDb =>
                ExtractProviderFromCardDb(
                    cardDb,
                    p => p.Name == "GreLocProvider",
                    methodFilter: m => m.Name == "GetString" || m.Name == "GetText" || m.Name == "Get" || m.Name.Contains("Loc"),
                    logLabel: "flavor text (GreLocProvider)")
                ?? ExtractProviderFromCardDb(
                    cardDb,
                    p => p.Name == "ClientLocProvider",
                    methodFilter: null,
                    logLabel: "flavor text (ClientLocProvider)"));
            if (result.HasValue)
            {
                _flavorTextProvider = result.Value.provider;
                _getFlavorTextMethod = result.Value.method;
            }
        }

        /// <summary>
        /// Searches for the artist provider in the game.
        /// </summary>
        private static void FindArtistProvider()
        {
            var result = SearchCardDatabaseProviders("artist provider", cardDb =>
                ExtractProviderFromCardDb(
                    cardDb,
                    p => p.Name.Contains("Artist"),
                    methodFilter: null,
                    logLabel: "artist"));
            if (result.HasValue)
            {
                _artistProvider = result.Value.provider;
                _getArtistMethod = result.Value.method;
            }
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
        /// Looks up the localized name for an enum value via GreLocProvider, matching the
        /// path the game uses inside LinkedInfoTextParser (e.g. enumName="SubType", value=42 →
        /// "Wizard"). Reuses the same provider instance as GetLocalizedTextById; the only
        /// extra reflection is locating the (string, int, bool, string) overload.
        /// </summary>
        internal static string GetLocalizedTextForEnumValue(string enumName, int value)
        {
            if (string.IsNullOrEmpty(enumName)) return null;

            if (!_flavorTextProviderSearched)
            {
                _flavorTextProviderSearched = true;
                FindFlavorTextProvider();
            }
            if (_flavorTextProvider == null) return null;

            if (!_getEnumValueMethodSearched)
            {
                _getEnumValueMethodSearched = true;
                _getEnumValueMethod = FindEnumValueMethod(_flavorTextProvider.GetType());
                if (_getEnumValueMethod != null)
                    Log.Card("CardTextProvider", $"Using {_flavorTextProvider.GetType().Name}.{_getEnumValueMethod.Name} for enum-value lookup");
            }
            if (_getEnumValueMethod == null) return null;

            try
            {
                var ps = _getEnumValueMethod.GetParameters();
                object[] args;
                if (ps.Length == 4)
                    args = new object[] { enumName, value, /*formatted*/ true, /*overrideLang*/ null };
                else if (ps.Length == 3)
                    args = new object[] { enumName, value, true };
                else if (ps.Length == 2)
                    args = new object[] { enumName, value };
                else
                    return null;

                var text = _getEnumValueMethod.Invoke(_flavorTextProvider, args) as string;
                if (string.IsNullOrEmpty(text) || text.StartsWith("$"))
                    return null;
                return text;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Finds <c>string GetLocalizedTextForEnumValue(string, int, ...)</c> on the provider —
        /// the non-generic overload from <c>IGreLocProvider</c>. Skips the generic overload
        /// (which constrains on <c>T : Enum</c> and isn't what we want here).
        /// </summary>
        private static MethodInfo FindEnumValueMethod(Type providerType)
        {
            foreach (var m in providerType.GetMethods(PublicInstance))
            {
                if (m.DeclaringType == typeof(object)) continue;
                if (m.IsGenericMethod || m.IsGenericMethodDefinition) continue;
                if (m.ReturnType != typeof(string)) continue;
                if (!m.Name.Equals("GetLocalizedTextForEnumValue", StringComparison.Ordinal)) continue;
                var ps = m.GetParameters();
                if (ps.Length >= 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(int))
                    return m;
            }
            return null;
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
