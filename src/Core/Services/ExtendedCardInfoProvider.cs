using UnityEngine;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AccessibleArena.Core.Models;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Provides extended card information: keyword descriptions and linked face data.
    /// Extracted from CardModelProvider to keep that class focused on core model access.
    /// </summary>
    public static class ExtendedCardInfoProvider
    {
        // Cache for linked face reflection
        private static PropertyInfo _linkedFaceTypeProp;
        private static bool _linkedFaceTypePropSearched;
        private static PropertyInfo _linkedFaceGrpIdsProp;
        private static bool _linkedFaceGrpIdsPropSearched;

        // Cache for ability hanger provider (keyword descriptions)
        private static object _abilityHangerProvider;
        private static MethodInfo _getHangerConfigsForCardMethod;
        private static MethodInfo _hangerProviderCleanupMethod;

        // Cache for parameterized hanger provider (Cycling, Plot, etc.)
        private static object _parameterizedHangerProvider;
        private static MethodInfo _getParamHangerConfigsMethod;

        // Cache for duel-scene CardDataProvider
        private static object _duelCardDataProvider;
        private static MethodInfo _duelGetCardPrintingMethod;
        private static bool _duelCardDataProviderSearched;

        // Cache for PAPA-constructed provider (non-duel keyword descriptions)
        private static object _papaHangerProvider;
        private static MethodInfo _papaGetConfigsMethod;
        private static MethodInfo _papaCleanupMethod;
        private static bool _papaProviderSearched;

        // Cache for card adapter construction (non-duel)
        private static object _papaCardDataProvider;
        private static MethodInfo _getCardPrintingByIdMethod;
        private static MethodInfo _createInstanceMethod;
        private static ConstructorInfo _cardDataCtor;
        private static object _holderTypeHand;

        /// <summary>
        /// Clears extended info caches.
        /// </summary>
        public static void ClearCache()
        {
            _linkedFaceTypeProp = null;
            _linkedFaceTypePropSearched = false;
            _linkedFaceGrpIdsProp = null;
            _linkedFaceGrpIdsPropSearched = false;
            _abilityHangerProvider = null;
            _getHangerConfigsForCardMethod = null;
            _hangerProviderCleanupMethod = null;
            _parameterizedHangerProvider = null;
            _getParamHangerConfigsMethod = null;
            _duelCardDataProvider = null;
            _duelGetCardPrintingMethod = null;
            _duelCardDataProviderSearched = false;
            _papaHangerProvider = null;
            _papaGetConfigsMethod = null;
            _papaCleanupMethod = null;
            _papaProviderSearched = false;
            _papaCardDataProvider = null;
            _getCardPrintingByIdMethod = null;
            _createInstanceMethod = null;
            _cardDataCtor = null;
            _holderTypeHand = null;
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
                var cdc = CardModelProvider.GetDuelSceneCDC(card);
                if (cdc == null)
                {
                    // Non-duel context: try PAPA-constructed provider for keyword descriptions
                    var papaResult = GetKeywordDescriptionsFromPAPA(card);
                    if (papaResult.Count > 0)
                        return papaResult;
                    // Fallback: extract individual ability texts from card model
                    return GetAbilityTextsFromCardModel(card);
                }

                // Ensure hanger provider is cached (retry if not found previously)
                if (_abilityHangerProvider == null)
                {
                    FindAbilityHangerProvider();
                }

                if (_abilityHangerProvider == null || _getHangerConfigsForCardMethod == null)
                    return result;

                var cdcType = cdc.GetType();

                // Get Model (ICardDataAdapter) from CDC
                var modelProp = cdcType.GetProperty("Model", PublicInstance);
                if (modelProp == null)
                {
                    // Try base type
                    modelProp = cdcType.BaseType?.GetProperty("Model", PublicInstance);
                }
                if (modelProp == null) return result;
                var model = modelProp.GetValue(cdc);
                if (model == null) return result;

                // Get HolderType (CardHolderType enum) from CDC
                var holderTypeProp = cdcType.GetProperty("HolderType", PublicInstance)
                    ?? cdcType.BaseType?.GetProperty("HolderType", PublicInstance);
                object holderType = null;
                if (holderTypeProp != null)
                    holderType = holderTypeProp.GetValue(cdc);

                // Create CDCViewMetadata struct
                object metadata = CreateCDCViewMetadata(cdc);

                if (holderType == null || metadata == null)
                {
                    MelonLogger.Msg($"[ExtendedCardInfoProvider] [ExtInfo] Missing holderType={holderType != null} metadata={metadata != null}");
                    return result;
                }

                // Call GetHangerConfigsForCard(model, holderType, metadata)
                var configs = _getHangerConfigsForCardMethod.Invoke(
                    _abilityHangerProvider, new object[] { model, holderType, metadata });

                var seen = new HashSet<string>();
                CollectHangerConfigs(configs, result, seen);

                // Cleanup provider internal state
                _hangerProviderCleanupMethod?.Invoke(_abilityHangerProvider, null);

                // Also query parameterized hanger provider (Cycling, Plot, etc.)
                QueryParameterizedHangers(model, result, seen);

                MelonLogger.Msg($"[ExtendedCardInfoProvider] [ExtInfo] GetKeywordDescriptions: {result.Count} entries");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[ExtendedCardInfoProvider] [ExtInfo] Error getting keyword descriptions: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Gets keyword descriptions in non-duel context by constructing an AbilityHangerBaseConfigProvider
        /// from PAPA's services. This gives the same Header+Details keyword tooltips as in duels.
        /// Returns empty list if construction fails (caller falls back to GetAbilityTextsFromCardModel).
        /// </summary>
        private static List<string> GetKeywordDescriptionsFromPAPA(GameObject card)
        {
            var result = new List<string>();
            if (card == null) return result;

            try
            {
                // Ensure PAPA provider is constructed
                if (!_papaProviderSearched)
                {
                    _papaProviderSearched = true;
                    ConstructProviderFromPAPA();
                }

                if (_papaHangerProvider == null || _papaGetConfigsMethod == null)
                    return result;

                // Get GrpId from the card's model
                uint grpId = 0;
                var metaCardView = CardModelProvider.GetMetaCardView(card);
                if (metaCardView == null)
                {
                    var parent = card.transform.parent;
                    int maxLevels = 5;
                    while (metaCardView == null && parent != null && maxLevels-- > 0)
                    {
                        metaCardView = CardModelProvider.GetMetaCardView(parent.gameObject);
                        parent = parent.parent;
                    }
                }
                if (metaCardView != null)
                {
                    var model = CardModelProvider.GetMetaCardModel(metaCardView);
                    if (model != null)
                    {
                        var grpIdObj = CardModelProvider.GetModelPropertyValue(model, model.GetType(), "GrpId");
                        if (grpIdObj is uint gid) grpId = gid;
                    }
                }

                if (grpId == 0)
                {
                    MelonLogger.Msg("[ExtendedCardInfoProvider] [ExtInfo] PAPA path: No GrpId found");
                    return result;
                }

                // Create ICardDataAdapter from GrpId
                var cardAdapter = CreateCardDataAdapter(grpId);
                if (cardAdapter == null)
                {
                    MelonLogger.Msg($"[ExtendedCardInfoProvider] [ExtInfo] PAPA path: Could not create adapter for GrpId {grpId}");
                    return result;
                }

                // Create CDCViewMetadata with defaults (no CDC available)
                var metadata = CreateCDCViewMetadata(null);
                if (metadata == null || _holderTypeHand == null)
                    return result;

                // Call GetHangerConfigsForCard(adapter, holderType, metadata)
                var configs = _papaGetConfigsMethod.Invoke(
                    _papaHangerProvider, new object[] { cardAdapter, _holderTypeHand, metadata });

                var seen = new HashSet<string>();
                CollectHangerConfigs(configs, result, seen);

                // Cleanup provider internal state
                _papaCleanupMethod?.Invoke(_papaHangerProvider, null);

                // Also query parameterized hanger provider (Cycling, Plot, etc.)
                QueryParameterizedHangers(cardAdapter, result, seen);

                MelonLogger.Msg($"[ExtendedCardInfoProvider] [ExtInfo] PAPA keyword descriptions: {result.Count} entries for GrpId {grpId}");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[ExtendedCardInfoProvider] [ExtInfo] PAPA keyword descriptions failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Constructs an AbilityHangerBaseConfigProvider from PAPA's services.
        /// PAPA is a singleton MonoBehaviour with CardDatabase, AssetLookupSystem, and ObjectPool.
        /// </summary>
        private static void ConstructProviderFromPAPA()
        {
            try
            {
                // Find PAPA singleton
                MonoBehaviour papa = null;
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == "PAPA")
                    {
                        papa = mb;
                        break;
                    }
                }

                if (papa == null)
                {
                    MelonLogger.Msg("[ExtendedCardInfoProvider] PAPA singleton not found");
                    return;
                }

                var papaType = papa.GetType();

                // Get services from PAPA
                var cardDb = papaType.GetProperty("CardDatabase", PublicInstance)?.GetValue(papa);
                var assetLookup = papaType.GetProperty("AssetLookupSystem", PublicInstance)?.GetValue(papa);
                var objectPool = papaType.GetProperty("ObjectPool", PublicInstance)?.GetValue(papa);

                if (cardDb == null || assetLookup == null || objectPool == null)
                {
                    MelonLogger.Msg($"[ExtendedCardInfoProvider] PAPA services missing: CardDb={cardDb != null}, AssetLookup={assetLookup != null}, ObjectPool={objectPool != null}");
                    return;
                }

                // Get ClientLocProvider from CardDatabase
                var clientLoc = cardDb.GetType().GetProperty("ClientLocProvider", PublicInstance)?.GetValue(cardDb);
                if (clientLoc == null)
                {
                    MelonLogger.Msg("[ExtendedCardInfoProvider] ClientLocProvider not found on CardDatabase");
                    return;
                }

                // Get CardDataProvider for later use in CreateCardDataAdapter
                _papaCardDataProvider = cardDb.GetType().GetProperty("CardDataProvider", PublicInstance)?.GetValue(cardDb);
                if (_papaCardDataProvider != null)
                {
                    _getCardPrintingByIdMethod = _papaCardDataProvider.GetType()
                        .GetMethod("GetCardPrintingById", new[] { typeof(uint) });
                    // Try with two params if one-param version not found
                    if (_getCardPrintingByIdMethod == null)
                    {
                        _getCardPrintingByIdMethod = _papaCardDataProvider.GetType()
                            .GetMethod("GetCardPrintingById", new[] { typeof(uint), typeof(string) });
                    }
                }

                // Find AbilityHangerBaseConfigProvider type
                Type providerType = FindType("Wotc.Mtga.Hangers.AbilityHangers.AbilityHangerBaseConfigProvider");
                if (providerType == null)
                {
                    MelonLogger.Msg("[ExtendedCardInfoProvider] AbilityHangerBaseConfigProvider type not found");
                    return;
                }

                // Find 4-parameter constructor: (AssetLookupSystem, ICardDatabaseAdapter, IClientLocProvider, IObjectPool)
                ConstructorInfo ctor = null;
                foreach (var c in providerType.GetConstructors())
                {
                    if (c.GetParameters().Length == 4)
                    {
                        ctor = c;
                        break;
                    }
                }

                if (ctor == null)
                {
                    MelonLogger.Msg("[ExtendedCardInfoProvider] 4-param constructor not found on AbilityHangerBaseConfigProvider");
                    return;
                }

                // Construct the provider
                var provider = ctor.Invoke(new[] { assetLookup, cardDb, clientLoc, objectPool });
                if (provider == null)
                {
                    MelonLogger.Msg("[ExtendedCardInfoProvider] AbilityHangerBaseConfigProvider construction returned null");
                    return;
                }

                // Cache methods
                _papaGetConfigsMethod = providerType.GetMethod("GetHangerConfigsForCard");
                _papaCleanupMethod = providerType.GetMethod("Cleanup");
                _papaHangerProvider = provider;

                // Find CardHolderType.Hand enum value
                Type holderTypeEnum = FindType("Wotc.Mtga.CardParts.CardHolderType");
                if (holderTypeEnum != null)
                {
                    try { _holderTypeHand = Enum.Parse(holderTypeEnum, "Hand"); }
                    catch { _holderTypeHand = Enum.ToObject(holderTypeEnum, 1); }
                }

                // Find CardData constructor and CreateInstance method for adapter creation
                Type cardDataType = FindType("GreClient.CardData.CardData");
                if (cardDataType != null)
                {
                    foreach (var c in cardDataType.GetConstructors())
                    {
                        var ps = c.GetParameters();
                        if (ps.Length == 2 && ps[0].ParameterType.Name == "MtgCardInstance")
                        {
                            _cardDataCtor = c;
                            break;
                        }
                    }
                }

                Type printingDataType = FindType("Wotc.Mtga.Cards.Database.CardPrintingData");
                if (printingDataType != null)
                {
                    _createInstanceMethod = printingDataType.GetMethod("CreateInstance");
                }

                MelonLogger.Msg($"[ExtendedCardInfoProvider] PAPA provider constructed: " +
                    $"Provider={_papaHangerProvider != null}, GetConfigs={_papaGetConfigsMethod != null}, " +
                    $"CardDataProvider={_papaCardDataProvider != null}, GetPrintingById={_getCardPrintingByIdMethod != null}, " +
                    $"CardDataCtor={_cardDataCtor != null}, CreateInstance={_createInstanceMethod != null}, " +
                    $"HolderType={_holderTypeHand != null}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ExtendedCardInfoProvider] ConstructProviderFromPAPA failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates an ICardDataAdapter from a GrpId using PAPA's CardDataProvider.
        /// Calls CardPrintingData.CreateInstance() then constructs CardData(instance, printing).
        /// </summary>
        private static object CreateCardDataAdapter(uint grpId)
        {
            if (_papaCardDataProvider == null || _getCardPrintingByIdMethod == null ||
                _createInstanceMethod == null || _cardDataCtor == null)
                return null;

            try
            {
                // Get CardPrintingData from GrpId
                object printing;
                var methodParams = _getCardPrintingByIdMethod.GetParameters();
                if (methodParams.Length == 1)
                    printing = _getCardPrintingByIdMethod.Invoke(_papaCardDataProvider, new object[] { grpId });
                else
                    printing = _getCardPrintingByIdMethod.Invoke(_papaCardDataProvider, new object[] { grpId, null });

                if (printing == null) return null;

                // Create MtgCardInstance via CardPrintingData.CreateInstance()
                var instance = _createInstanceMethod.Invoke(printing, null);
                if (instance == null) return null;

                // Construct CardData(MtgCardInstance, CardPrintingData)
                return _cardDataCtor.Invoke(new[] { instance, printing });
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[ExtendedCardInfoProvider] CreateCardDataAdapter failed for GrpId {grpId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Fallback for non-duel contexts: extracts individual ability texts from the card model.
        /// Returns each ability as a separate navigable item for the extended info menu.
        /// </summary>
        private static List<string> GetAbilityTextsFromCardModel(GameObject card)
        {
            var result = new List<string>();
            if (card == null) return result;

            try
            {
                // Get the card model (same path as ExtractCardInfoFromModel)
                object model = null;

                var metaCardView = CardModelProvider.GetMetaCardView(card);
                if (metaCardView == null)
                {
                    var parent = card.transform.parent;
                    int maxLevels = 5;
                    while (metaCardView == null && parent != null && maxLevels-- > 0)
                    {
                        metaCardView = CardModelProvider.GetMetaCardView(parent.gameObject);
                        parent = parent.parent;
                    }
                }

                if (metaCardView != null)
                    model = CardModelProvider.GetMetaCardModel(metaCardView);

                if (model == null)
                {
                    MelonLogger.Msg("[ExtendedCardInfoProvider] [ExtInfo] No model found for non-duel card");
                    return result;
                }

                var objType = model.GetType();

                // Get GrpId and TitleId for ability text lookup
                uint cardGrpId = 0;
                var grpIdObj = CardModelProvider.GetModelPropertyValue(model, objType, "GrpId");
                if (grpIdObj is uint gid) cardGrpId = gid;

                uint cardTitleId = 0;
                var titleIdObj = CardModelProvider.GetModelPropertyValue(model, objType, "TitleId");
                if (titleIdObj is uint tid) cardTitleId = tid;

                // Get all ability IDs for context
                var abilityIdsVal = CardModelProvider.GetModelPropertyValue(model, objType, "AbilityIds");
                uint[] abilityIds = null;
                if (abilityIdsVal is IEnumerable<uint> aidEnum)
                    abilityIds = aidEnum.ToArray();
                else if (abilityIdsVal is uint[] aidArray)
                    abilityIds = aidArray;

                // Extract abilities
                var abilities = CardModelProvider.GetModelPropertyValue(model, objType, "Abilities");
                if (abilities is IEnumerable abilityEnum)
                {
                    var seen = new HashSet<string>();
                    foreach (var ability in abilityEnum)
                    {
                        if (ability == null) continue;
                        var abilityType = ability.GetType();

                        uint abilityId = 0;
                        var idProp = abilityType.GetProperty("Id", PublicInstance);
                        if (idProp != null)
                        {
                            var idVal = idProp.GetValue(ability);
                            if (idVal is uint aid) abilityId = aid;
                        }

                        var textValue = CardTextProvider.GetAbilityText(ability, abilityType, cardGrpId, abilityId, abilityIds, cardTitleId);
                        if (!string.IsNullOrEmpty(textValue))
                        {
                            textValue = CardModelProvider.ParseManaSymbolsInText(textValue);
                            if (seen.Add(textValue))
                            {
                                result.Add(textValue);
                                MelonLogger.Msg($"[ExtendedCardInfoProvider] [ExtInfo] Ability: '{textValue}'");
                            }
                        }
                    }
                }

                // Also try CardPrintingData path if model didn't have abilities
                if (result.Count == 0 && cardGrpId > 0)
                {
                    var cardData = CardModelProvider.GetCardDataFromGrpId(cardGrpId) ?? GetCardDataFromGrpIdDuelScene(cardGrpId);
                    if (cardData != null)
                    {
                        var cardType = cardData.GetType();
                        var abilitiesProp = cardType.GetProperty("Abilities") ?? cardType.GetProperty("IntrinsicAbilities");
                        if (abilitiesProp != null)
                        {
                            var abilitiesFromData = abilitiesProp.GetValue(cardData) as IEnumerable;
                            if (abilitiesFromData != null)
                            {
                                var seen = new HashSet<string>();
                                foreach (var ability in abilitiesFromData)
                                {
                                    if (ability == null) continue;
                                    var aType = ability.GetType();
                                    var idProp = aType.GetProperty("Id");
                                    if (idProp != null)
                                    {
                                        var abilityId = (uint)idProp.GetValue(ability);
                                        var abilityText = CardTextProvider.GetAbilityTextFromProvider(cardGrpId, abilityId, null, 0);
                                        if (!string.IsNullOrEmpty(abilityText))
                                        {
                                            abilityText = CardModelProvider.ParseManaSymbolsInText(abilityText);
                                            if (seen.Add(abilityText))
                                            {
                                                result.Add(abilityText);
                                                MelonLogger.Msg($"[ExtendedCardInfoProvider] [ExtInfo] Ability (data): '{abilityText}'");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                MelonLogger.Msg($"[ExtendedCardInfoProvider] [ExtInfo] GetAbilityTextsFromCardModel: {result.Count} entries");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[ExtendedCardInfoProvider] [ExtInfo] Error extracting ability texts: {ex.Message}");
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
            Type ahbType = FindType("AbilityHangerBase");

            UnityEngine.Object[] instances = null;

            if (ahbType != null)
            {
                // FindObjectsOfTypeAll includes inactive GameObjects
                instances = Resources.FindObjectsOfTypeAll(ahbType);
                MelonLogger.Msg($"[ExtendedCardInfoProvider] Found {instances.Length} AbilityHangerBase instances (including inactive)");
            }
            else
            {
                MelonLogger.Msg($"[ExtendedCardInfoProvider] AbilityHangerBase type not found in assemblies, searching all MonoBehaviours...");
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
                    PrivateInstance | BindingFlags.FlattenHierarchy);
                if (field == null) continue;

                var provider = field.GetValue(obj);
                if (provider == null)
                {
                    MelonLogger.Msg($"[ExtendedCardInfoProvider] Found {type.Name} but _abilityHangerProvider is null (not yet initialized)");
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

                MelonLogger.Msg($"[ExtendedCardInfoProvider] Found AbilityHangerProvider: {providerType.Name} from {type.Name}");

                // Log method signature for debugging
                var ps = getConfigsMethod.GetParameters();
                MelonLogger.Msg($"[ExtendedCardInfoProvider] GetHangerConfigsForCard({string.Join(", ", ps.Select(p => $"{p.ParameterType.Name} {p.Name}"))})");

                // Also extract _parameterizedHangers (handles Cycling, Plot, etc.)
                var paramField = type.GetField("_parameterizedHangers",
                    PrivateInstance | BindingFlags.FlattenHierarchy);
                if (paramField != null)
                {
                    var paramProvider = paramField.GetValue(obj);
                    if (paramProvider != null)
                    {
                        var paramProviderType = paramProvider.GetType();
                        var getConfigsParam = paramProviderType.GetMethod("GetHangerConfigs");
                        if (getConfigsParam != null)
                        {
                            _parameterizedHangerProvider = paramProvider;
                            _getParamHangerConfigsMethod = getConfigsParam;
                            MelonLogger.Msg($"[ExtendedCardInfoProvider] Found ParameterizedHangerProvider: {paramProviderType.Name}");
                        }
                    }
                }

                return;
            }

            MelonLogger.Msg($"[ExtendedCardInfoProvider] AbilityHangerProvider not found (will retry on next I key press)");
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
                Type metadataType = FindType("Wotc.Mtga.CardParts.CDCViewMetadata");
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
                MelonLogger.Msg($"[ExtendedCardInfoProvider] Error creating CDCViewMetadata: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Extracts Header/Details from HangerConfig results and adds formatted strings to the result list.
        /// Shared by both duel and PAPA paths to avoid duplication.
        /// </summary>
        private static void CollectHangerConfigs(object configs, List<string> result, HashSet<string> seen)
        {
            if (!(configs is IEnumerable configEnum)) return;

            foreach (var config in configEnum)
            {
                if (config == null) continue;
                var configType = config.GetType();

                var headerField = configType.GetField("Header");
                var detailsField = configType.GetField("Details");

                string header = headerField?.GetValue(config)?.ToString() ?? "";
                string details = detailsField?.GetValue(config)?.ToString() ?? "";

                // Strip trailing bare mana costs from header (parameterized hangers
                // append card-specific costs like "Umwandlung o2oW" — the user already
                // sees the cost in the rules text, the I menu should just explain the keyword)
                header = Regex.Replace(header, @"\s+(?:o(?:\d+|[WUBRGCTXSE/]))+\s*$", "").Trim();

                if (string.IsNullOrEmpty(header) && string.IsNullOrEmpty(details)) continue;

                string text;
                if (!string.IsNullOrEmpty(header) && !string.IsNullOrEmpty(details))
                    text = $"{header}: {details}";
                else if (!string.IsNullOrEmpty(header))
                    text = header;
                else
                    text = details;

                text = CardModelProvider.ParseManaSymbolsInText(text);

                MelonLogger.Msg($"[ExtendedCardInfoProvider] [ExtInfo] Hanger: '{text}'");

                if (seen.Add(text))
                    result.Add(text);
            }
        }

        /// <summary>
        /// Queries the ParameterizedHangerConfigProvider for keyword descriptions (Cycling, Plot, etc.).
        /// These use a separate ALT tree from the main AbilityHangerBaseConfigProvider.
        /// </summary>
        private static void QueryParameterizedHangers(object model, List<string> result, HashSet<string> seen)
        {
            if (_parameterizedHangerProvider == null || _getParamHangerConfigsMethod == null || model == null)
                return;

            try
            {
                var configs = _getParamHangerConfigsMethod.Invoke(
                    _parameterizedHangerProvider, new object[] { model });

                CollectHangerConfigs(configs, result, seen);
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[ExtendedCardInfoProvider] [ExtInfo] Parameterized hanger query failed: {ex.Message}");
            }
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
                var cdc = CardModelProvider.GetDuelSceneCDC(card);
                if (cdc != null)
                    model = CardModelProvider.GetCardModel(cdc);
                if (model == null)
                {
                    var metaView = CardModelProvider.GetMetaCardView(card);
                    if (metaView != null)
                        model = CardModelProvider.GetMetaCardModel(metaView);
                }
                if (model == null) return null;

                var objType = model.GetType();

                // Get LinkedFaceType (enum stored as int)
                if (!_linkedFaceTypePropSearched)
                {
                    _linkedFaceTypePropSearched = true;
                    _linkedFaceTypeProp = objType.GetProperty("LinkedFaceType", PublicInstance);
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
                    _linkedFaceGrpIdsProp = objType.GetProperty("LinkedFaceGrpIds", PublicInstance);
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

                var faceInfo = CardModelProvider.ExtractCardInfoFromObject(faceData);
                if (!faceInfo.IsValid) return null;

                // Map linked face type to label
                string label = GetLinkedFaceLabel(linkedFaceInt);

                return (label, faceInfo);
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "ExtendedCardInfoProvider",
                    $"Error getting linked face info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets linked token information for cards that create tokens (e.g., Roles, complex tokens).
        /// Reads AbilityIdToLinkedTokenPrinting from the card's Printing sub-object,
        /// deduplicates by GrpId, and extracts CardInfo for each unique token.
        /// </summary>
        public static List<CardInfo> GetLinkedTokenInfos(GameObject card)
        {
            var result = new List<CardInfo>();
            if (card == null) return result;

            try
            {
                // Get model (same approach as GetLinkedFaceInfo)
                object model = null;
                var cdc = CardModelProvider.GetDuelSceneCDC(card);
                if (cdc != null)
                    model = CardModelProvider.GetCardModel(cdc);
                if (model == null)
                {
                    var metaView = CardModelProvider.GetMetaCardView(card);
                    if (metaView != null)
                        model = CardModelProvider.GetMetaCardModel(metaView);
                }
                if (model == null) return result;

                var modelType = model.GetType();

                // Get Printing sub-object from model
                var printing = CardModelProvider.GetModelPropertyValue(model, modelType, "Printing");
                if (printing == null) return result;

                var printingType = printing.GetType();

                // Get AbilityIdToLinkedTokenPrinting property
                var tokenPrintingProp = printingType.GetProperty("AbilityIdToLinkedTokenPrinting", PublicInstance);
                if (tokenPrintingProp == null) return result;

                var tokenPrintingDict = tokenPrintingProp.GetValue(printing);
                if (tokenPrintingDict == null) return result;

                // It's IReadOnlyDictionary<uint, IReadOnlyList<CardPrintingData>>
                // Iterate all values, flatten, deduplicate by GrpId
                var seenGrpIds = new HashSet<uint>();
                var valuesProperty = tokenPrintingDict.GetType().GetProperty("Values");
                if (valuesProperty == null) return result;

                var values = valuesProperty.GetValue(tokenPrintingDict) as IEnumerable;
                if (values == null) return result;

                foreach (var tokenList in values)
                {
                    if (tokenList == null) continue;
                    var tokenListEnum = tokenList as IEnumerable;
                    if (tokenListEnum == null) continue;

                    foreach (var tokenData in tokenListEnum)
                    {
                        if (tokenData == null) continue;

                        // Get GrpId to deduplicate
                        var tokenType = tokenData.GetType();
                        var grpIdProp = tokenType.GetProperty("GrpId");
                        uint grpId = 0;
                        if (grpIdProp != null)
                        {
                            var grpIdVal = grpIdProp.GetValue(tokenData);
                            if (grpIdVal is uint gid) grpId = gid;
                            else if (grpIdVal is int gidInt && gidInt > 0) grpId = (uint)gidInt;
                        }

                        if (grpId > 0 && !seenGrpIds.Add(grpId))
                            continue; // Already processed this token

                        var tokenInfo = CardModelProvider.ExtractCardInfoFromCardData(tokenData, grpId);
                        if (tokenInfo.IsValid)
                        {
                            result.Add(tokenInfo);
                            MelonLogger.Msg($"[ExtendedCardInfoProvider] Token: {tokenInfo.Name} (GrpId {grpId})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[ExtendedCardInfoProvider] Error getting linked token infos: {ex.Message}");
            }

            return result;
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
        /// Separate from CardModelProvider.GetCardDataFromGrpId() which requires _cachedDeckHolder (menu-only).
        /// </summary>
        internal static object GetCardDataFromGrpIdDuelScene(uint grpId)
        {
            if (grpId == 0) return null;

            // First try the menu-scene path (works if _cachedDeckHolder is available)
            var menuResult = CardModelProvider.GetCardDataFromGrpId(grpId);
            if (menuResult != null) return menuResult;

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
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "ExtendedCardInfoProvider",
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
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "ExtendedCardInfoProvider",
                        $"Found duel CardDataProvider: {cdpType.Name}.{method.Name}");
                }
                break;
            }
        }
    }
}
