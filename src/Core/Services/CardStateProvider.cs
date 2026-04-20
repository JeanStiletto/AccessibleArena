using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AccessibleArena.Core.Models;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Provides card state information: attachments, combat state, targeting, and categorization.
    /// Extracted from CardModelProvider for separation of concerns.
    /// Uses CardModelProvider for low-level model/CDC access.
    /// </summary>
    public static class CardStateProvider
    {
        #region Cache Fields

        // Attachment cache
        private static FieldInfo _attachedToIdField;
        private static bool _attachedToIdFieldSearched;

        // Zone type cache
        private static PropertyInfo _zoneTypePropCached;
        private static bool _zoneTypePropSearched;

        // Generic MemberInfo cache keyed by member name for Instance sub-object fields/properties
        private static readonly Dictionary<string, MemberInfo> _instanceMemberCache = new Dictionary<string, MemberInfo>();
        private static readonly HashSet<string> _instanceMemberSearched = new HashSet<string>();

        // Model-level PropertyInfo cache (for GetCardCategory, GetModelInstanceId, GetModelGrpId)
        private static PropertyInfo _controllerNumProp;
        private static PropertyInfo _isBasicLandProp;
        private static PropertyInfo _isLandButNotBasicProp;
        private static PropertyInfo _cardTypesProp;
        private static PropertyInfo _instanceIdProp;
        private static PropertyInfo _grpIdProp;
        private static PropertyInfo _subtypesProp;
        private static bool _modelPropsSearched;

        #endregion

        /// <summary>
        /// Clears all cached reflection data. Call on scene change.
        /// </summary>
        public static void ClearCache()
        {
            _attachedToIdField = null;
            _attachedToIdFieldSearched = false;
            _zoneTypePropCached = null;
            _zoneTypePropSearched = false;
            _instanceMemberCache.Clear();
            _instanceMemberSearched.Clear();
            _controllerNumProp = null;
            _isBasicLandProp = null;
            _isLandButNotBasicProp = null;
            _cardTypesProp = null;
            _instanceIdProp = null;
            _grpIdProp = null;
            _subtypesProp = null;
            _modelPropsSearched = false;
        }

        #region Generic Instance Accessors

        /// <summary>
        /// Gets a cached MemberInfo (field or property) from the Instance sub-object of a model.
        /// Tries property first, then field, with the specified binding flags.
        /// </summary>
        private static MemberInfo GetCachedInstanceMember(object instance, string name, BindingFlags flags)
        {
            if (_instanceMemberSearched.Contains(name))
            {
                _instanceMemberCache.TryGetValue(name, out var cached);
                return cached;
            }
            _instanceMemberSearched.Add(name);

            var type = instance.GetType();
            var prop = type.GetProperty(name, flags);
            if (prop != null) { _instanceMemberCache[name] = prop; return prop; }

            var field = type.GetField(name, flags);
            if (field != null) { _instanceMemberCache[name] = field; return field; }

            return null;
        }

        /// <summary>
        /// Gets a scalar value of type T from a cached field/property on model.Instance.
        /// Returns <paramref name="defaultValue"/> if the instance, member, or cast is unavailable.
        /// Used for IsAttacking/IsBlocking/IsTapped/HasSummoningSickness (bool) and
        /// AttackTargetId/AttachedToId (uint).
        /// </summary>
        private static T GetValueFromInstance<T>(object model, string memberName, BindingFlags flags, T defaultValue = default)
        {
            var instance = CardModelProvider.GetModelInstance(model);
            if (instance == null) return defaultValue;
            try
            {
                var member = GetCachedInstanceMember(instance, memberName, flags);
                if (member == null) return defaultValue;
                var val = member is PropertyInfo p ? p.GetValue(instance) : ((FieldInfo)member).GetValue(instance);
                return val is T t ? t : defaultValue;
            }
            catch { return defaultValue; }
        }

        /// <summary>
        /// Gets a List&lt;T&gt; from a cached field on model.Instance. Only fields are
        /// inspected — these members are fields on the game's MtgCardInstance.
        /// Used for BlockingIds, BlockedByIds, TargetIds, TargetedByIds.
        /// </summary>
        private static List<T> GetListFromInstance<T>(object model, string fieldName, BindingFlags flags)
        {
            var result = new List<T>();
            var instance = CardModelProvider.GetModelInstance(model);
            if (instance == null) return result;
            try
            {
                var member = GetCachedInstanceMember(instance, fieldName, flags);
                if (member is FieldInfo fi && fi.GetValue(instance) is IList list)
                    foreach (var item in list)
                        if (item is T typed) result.Add(typed);
            }
            catch { }
            return result;
        }

        /// <summary>
        /// Gets a bool state from a card GameObject by chaining CDC → Model → Instance.
        /// </summary>
        private static bool GetBoolFromCard(GameObject card, Func<object, bool> accessor)
        {
            if (card == null) return false;
            var cdc = CardModelProvider.GetDuelSceneCDC(card);
            if (cdc == null) return false;
            var model = CardModelProvider.GetCardModel(cdc);
            return accessor(model);
        }

        #endregion

        #region Model Property Cache

        /// <summary>
        /// Ensures model-level PropertyInfo objects are cached. Called once per model type.
        /// The model type is constant for the entire duel so these are looked up once and reused.
        /// </summary>
        private static void EnsureModelPropsSearched(Type modelType)
        {
            if (_modelPropsSearched) return;
            _modelPropsSearched = true;
            _controllerNumProp = modelType.GetProperty("ControllerNum");
            _isBasicLandProp = modelType.GetProperty("IsBasicLand");
            _isLandButNotBasicProp = modelType.GetProperty("IsLandButNotBasic");
            _cardTypesProp = modelType.GetProperty("CardTypes");
            _instanceIdProp = modelType.GetProperty("InstanceId");
            _grpIdProp = modelType.GetProperty("GrpId");
            _subtypesProp = modelType.GetProperty("Subtypes");
        }

        #endregion

        #region Attachments

        /// <summary>
        /// Gets the AttachedToId from a card's Model.Instance via reflection.
        /// This is the InstanceId of the card this card is attached to (0 if not attached).
        /// Used by the game's UniversalBattlefieldStack to track attachment relationships.
        /// </summary>
        public static uint GetAttachedToId(object model)
        {
            var instance = CardModelProvider.GetModelInstance(model);
            if (instance == null) return 0;
            try
            {
                // Cache the FieldInfo - AttachedToId is a field on MtgCardInstance, not a property
                if (!_attachedToIdFieldSearched)
                {
                    _attachedToIdFieldSearched = true;
                    _attachedToIdField = instance.GetType().GetField("AttachedToId",
                        AllInstanceFlags);
                }

                if (_attachedToIdField != null)
                {
                    var val = _attachedToIdField.GetValue(instance);
                    if (val is uint id) return id;
                }
            }
            catch { /* Reflection may fail on different game versions */ }
            return 0;
        }

        /// <summary>
        /// Gets the InstanceId from a card's model. Uses cached PropertyInfo.
        /// </summary>
        private static uint GetModelInstanceId(object model)
        {
            if (model == null) return 0;
            try
            {
                EnsureModelPropsSearched(model.GetType());
                if (_instanceIdProp != null)
                {
                    var val = _instanceIdProp.GetValue(model);
                    if (val is uint id) return id;
                }
            }
            catch { /* Reflection may fail on different game versions */ }
            return 0;
        }

        /// <summary>
        /// Gets the GrpId from a card's model. Uses cached PropertyInfo.
        /// </summary>
        private static uint GetModelGrpId(object model)
        {
            if (model == null) return 0;
            try
            {
                EnsureModelPropsSearched(model.GetType());
                if (_grpIdProp != null)
                {
                    var val = _grpIdProp.GetValue(model);
                    if (val is uint id) return id;
                }
            }
            catch { /* Reflection may fail on different game versions */ }
            return 0;
        }

        private static List<(object model, uint instanceId, uint grpId)> GetAllBattlefieldCardModels()
            => GetAllCardModelsInHolder("BattlefieldCardHolder");

        /// <summary>
        /// Checks if a card with the given GrpId exists on the battlefield, stack, graveyard, or exile.
        /// Used to determine if a commander has left the command zone.
        /// </summary>
        public static bool IsGrpIdInNonCommandZone(uint grpId)
        {
            if (grpId == 0) return false;

            string[] holders = { "BattlefieldCardHolder", "StackCardHolder", "LocalGraveyard", "OpponentGraveyard", "ExileCardHolder" };
            foreach (var holderName in holders)
            {
                var holder = DuelHolderCache.GetHolder(holderName);
                if (holder == null) continue;

                foreach (var child in holder.GetComponentsInChildren<Transform>(true))
                {
                    if (child == null || !child.gameObject.activeInHierarchy) continue;

                    var cdc = CardModelProvider.GetDuelSceneCDC(child.gameObject);
                    if (cdc == null) continue;

                    var model = CardModelProvider.GetCardModel(cdc);
                    if (model == null) continue;

                    if (GetModelGrpId(model) == grpId)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the list of cards attached to this card (enchantments, equipment, etc.).
        /// Scans all battlefield cards and checks whose Instance.AttachedToId matches this card's InstanceId.
        /// </summary>
        public static List<(uint instanceId, uint grpId, string name)> GetAttachments(GameObject card)
        {
            var attachments = new List<(uint instanceId, uint grpId, string name)>();
            if (card == null) return attachments;

            var cdcComponent = CardModelProvider.GetDuelSceneCDC(card);
            if (cdcComponent == null) return attachments;

            var model = CardModelProvider.GetCardModel(cdcComponent);
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
                        string name = grpId > 0 ? CardModelProvider.GetNameFromGrpId(grpId) : null;
                        attachments.Add((instanceId, grpId, name));
                        Log.Card("CardStateProvider",
                            $"Found attachment: {name ?? "unknown"} (InstanceId={instanceId}) attached to InstanceId={myInstanceId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Card("CardStateProvider", $"Error getting attachments: {ex.Message}");
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

            var cdcComponent = CardModelProvider.GetDuelSceneCDC(card);
            if (cdcComponent == null) return null;

            var model = CardModelProvider.GetCardModel(cdcComponent);
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
                        string name = grpId > 0 ? CardModelProvider.GetNameFromGrpId(grpId) : null;
                        Log.Card("CardStateProvider",
                            $"Card is attached to: {name ?? "unknown"} (InstanceId={instanceId}, GrpId={grpId})");
                        return (instanceId, grpId, name);
                    }
                }

                Log.Card("CardStateProvider",
                    $"AttachedToId={attachedToId} but parent card not found on battlefield");
            }
            catch (Exception ex)
            {
                Log.Card("CardStateProvider", $"Error getting attached-to info: {ex.Message}");
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

        public static bool GetIsAttacking(object model) => GetValueFromInstance<bool>(model, "IsAttacking", PublicInstance);
        public static bool GetIsBlocking(object model) => GetValueFromInstance<bool>(model, "IsBlocking", PublicInstance);
        public static List<uint> GetBlockingIds(object model) => GetListFromInstance<uint>(model, "BlockingIds", AllInstanceFlags);
        public static List<uint> GetBlockedByIds(object model) => GetListFromInstance<uint>(model, "BlockedByIds", AllInstanceFlags);
        public static uint GetAttackTargetId(object model) => GetValueFromInstance<uint>(model, "AttackTargetId", PublicInstance);

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
                        return grpId > 0 ? CardModelProvider.GetNameFromGrpId(grpId) : null;
                    }
                }
            }
            catch { /* Reflection may fail on different game versions */ }
            return null;
        }

        /// <summary>
        /// Resolves an InstanceId to a card name with P/T appended if available.
        /// Returns null if not found.
        /// </summary>
        public static string ResolveInstanceIdToNameWithPT(uint instanceId)
        {
            if (instanceId == 0) return null;
            try
            {
                var allCards = GetAllBattlefieldCardModels();
                foreach (var (cardModel, id, grpId) in allCards)
                {
                    if (id == instanceId)
                    {
                        string name = grpId > 0 ? CardModelProvider.GetNameFromGrpId(grpId) : null;
                        if (name == null) return null;
                        var info = CardModelProvider.ExtractCardInfoFromObject(cardModel);
                        if (!string.IsNullOrEmpty(info.PowerToughness))
                            return $"{name} {info.PowerToughness}";
                        return name;
                    }
                }
            }
            catch { /* Reflection may fail on different game versions */ }
            return null;
        }

        /// <summary>
        /// Returns the primary non-creature type label (Artifact, Enchantment, Planeswalker, Battle)
        /// for a card on the battlefield. Returns null if not determinable or if it's a creature/land.
        /// </summary>
        public static string GetNonCreatureTypeLabel(GameObject card)
        {
            if (card == null) return null;
            var cdc = CardModelProvider.GetDuelSceneCDC(card);
            if (cdc == null) return null;
            var model = CardModelProvider.GetCardModel(cdc);
            if (model == null) return null;
            try
            {
                EnsureModelPropsSearched(model.GetType());
                if (_cardTypesProp != null)
                {
                    var cardTypes = _cardTypesProp.GetValue(model) as System.Collections.IEnumerable;
                    if (cardTypes != null)
                    {
                        foreach (var cardType in cardTypes)
                        {
                            string typeStr = cardType?.ToString() ?? "";
                            if (typeStr.Contains("Planeswalker")) return Models.Strings.CardType_Planeswalker;
                            if (typeStr.Contains("Artifact")) return Models.Strings.CardType_Artifact;
                            if (typeStr.Contains("Enchantment")) return Models.Strings.CardType_Enchantment;
                            if (typeStr.Contains("Battle")) return Models.Strings.CardType_Battle;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        // IsTapped and HasSummoningSickness are public FIELDS on MtgCardInstance, not properties
        public static bool GetIsTapped(object model) => GetValueFromInstance<bool>(model, "IsTapped", PublicInstance);
        public static bool GetHasSummoningSickness(object model) => GetValueFromInstance<bool>(model, "HasSummoningSickness", PublicInstance);

        // Card GameObject wrappers: chain GetDuelSceneCDC → GetCardModel → accessor
        public static bool GetIsTappedFromCard(GameObject card) => GetBoolFromCard(card, GetIsTapped);
        public static bool GetHasSummoningSicknessFromCard(GameObject card) => GetBoolFromCard(card, GetHasSummoningSickness);
        public static bool GetIsAttackingFromCard(GameObject card) => GetBoolFromCard(card, GetIsAttacking);
        public static bool GetIsBlockingFromCard(GameObject card) => GetBoolFromCard(card, GetIsBlocking);

        /// <summary>
        /// Formats a CounterType enum name into a human-readable string.
        /// E.g., "P1P1" -> "+1/+1", "M1M1" -> "-1/-1", "Loyalty" -> "Loyalty".
        /// </summary>
        public static string FormatCounterTypeName(string enumName)
        {
            if (string.IsNullOrEmpty(enumName)) return enumName;

            // Match patterns like P1P1, M1M0, P0P1, etc.
            if (enumName.Length == 4 &&
                (enumName[0] == 'P' || enumName[0] == 'M') &&
                char.IsDigit(enumName[1]) &&
                (enumName[2] == 'P' || enumName[2] == 'M') &&
                char.IsDigit(enumName[3]))
            {
                char sign1 = enumName[0] == 'P' ? '+' : '-';
                char sign2 = enumName[2] == 'P' ? '+' : '-';
                return $"{sign1}{enumName[1]}/{sign2}{enumName[3]}";
            }

            return enumName;
        }

        /// <summary>
        /// Gets the localized name for a CounterType enum value using the game's GreLocProvider.
        /// Falls back to FormatCounterTypeName if localization is unavailable.
        /// Call with the raw enum object from the Counters dictionary key.
        /// </summary>
        public static string GetLocalizedCounterTypeName(object counterTypeEnum)
        {
            if (counterTypeEnum == null) return "";

            // Resolve via client loc provider: "Enum/CounterType/CounterType_{name}"
            string enumName = counterTypeEnum.ToString();
            string locKey = $"Enum/CounterType/CounterType_{enumName}";
            string localized = UITextExtractor.ResolveLocKey(locKey);
            if (!string.IsNullOrEmpty(localized))
                return localized;

            // Fall back to English enum name formatting (e.g., P1P1 -> +1/+1)
            return FormatCounterTypeName(counterTypeEnum.ToString());
        }

        /// <summary>
        /// Gets all counters on a card from its model's Instance.Counters dictionary.
        /// Returns a list of (typeName, count) tuples for counters with count > 0.
        /// Chains: GetDuelSceneCDC -> GetCardModel -> Instance -> Counters.
        /// </summary>
        public static List<(string typeName, int count)> GetCountersFromCard(GameObject card)
        {
            var result = new List<(string, int)>();
            if (card == null) return result;

            var cdc = CardModelProvider.GetDuelSceneCDC(card);
            if (cdc == null) return result;
            var model = CardModelProvider.GetCardModel(cdc);
            if (model == null) return result;

            var instance = CardModelProvider.GetModelInstance(model);
            if (instance == null) return result;

            try
            {
                var instanceType = instance.GetType();
                // Counters is IReadOnlyDictionary<CounterType, int> - try as property first, then field
                var countersProp = instanceType.GetProperty("Counters", PublicInstance);
                object countersObj = null;
                if (countersProp != null)
                {
                    countersObj = countersProp.GetValue(instance);
                }
                else
                {
                    var countersField = instanceType.GetField("Counters", PublicInstance);
                    if (countersField != null)
                        countersObj = countersField.GetValue(instance);
                }

                if (countersObj == null) return result;

                // Iterate via IEnumerable (each entry is KeyValuePair<CounterType, int>)
                var enumerable = countersObj as IEnumerable;
                if (enumerable == null) return result;

                foreach (var entry in enumerable)
                {
                    if (entry == null) continue;
                    var entryType = entry.GetType();
                    var keyProp = entryType.GetProperty("Key");
                    var valueProp = entryType.GetProperty("Value");
                    if (keyProp == null || valueProp == null) continue;

                    var key = keyProp.GetValue(entry);
                    var value = valueProp.GetValue(entry);
                    if (key == null || value == null) continue;

                    int count = 0;
                    if (value is int i) count = i;
                    else if (int.TryParse(value.ToString(), out int parsed)) count = parsed;

                    if (count > 0)
                    {
                        result.Add((GetLocalizedCounterTypeName(key), count));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Card("CardStateProvider", $"Error reading counters: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Gets the ZoneType string from a card's model (e.g., "Hand", "Command", "Graveyard").
        /// This is the game's internal zone, which may differ from the UI holder zone
        /// (e.g., commander cards are in Command zone but visually placed in the hand holder).
        /// </summary>
        public static string GetModelZoneTypeName(object model)
        {
            if (model == null) return null;
            try
            {
                if (!_zoneTypePropSearched)
                {
                    _zoneTypePropSearched = true;
                    _zoneTypePropCached = model.GetType().GetProperty("ZoneType",
                        PublicInstance);
                }

                PropertyInfo prop = _zoneTypePropCached;
                if (prop != null && !prop.DeclaringType.IsAssignableFrom(model.GetType()))
                    prop = model.GetType().GetProperty("ZoneType", PublicInstance);

                if (prop != null)
                {
                    var val = prop.GetValue(model);
                    return val?.ToString();
                }
            }
            catch { /* Reflection may fail on different game versions */ }
            return null;
        }

        /// <summary>
        /// Gets the ZoneType name from a card GameObject.
        /// Chains: GetDuelSceneCDC -> GetCardModel -> GetModelZoneTypeName.
        /// </summary>
        public static string GetCardZoneTypeName(GameObject card)
        {
            if (card == null) return null;
            var cdc = CardModelProvider.GetDuelSceneCDC(card);
            if (cdc == null) return null;
            var model = CardModelProvider.GetCardModel(cdc);
            return GetModelZoneTypeName(model);
        }

        #endregion

        #region Targeting

        public static List<uint> GetTargetIds(object model) => GetListFromInstance<uint>(model, "TargetIds", AllInstanceFlags);
        public static List<uint> GetTargetedByIds(object model) => GetListFromInstance<uint>(model, "TargetedByIds", AllInstanceFlags);

        private static List<(object model, uint instanceId, uint grpId)> GetAllStackCardModels()
            => GetAllCardModelsInHolder("StackCardHolder");

        /// <summary>
        /// Collects all DuelScene_CDC card models from a named card holder zone.
        /// Uses DuelHolderCache to avoid full scene scan via FindObjectsOfType.
        /// Returns list of (model, instanceId, grpId) for scanning relationships.
        /// </summary>
        private static List<(object model, uint instanceId, uint grpId)> GetAllCardModelsInHolder(string holderNameContains)
        {
            var results = new List<(object model, uint instanceId, uint grpId)>();

            var holder = DuelHolderCache.GetHolder(holderNameContains);
            if (holder == null) return results;

            foreach (var child in holder.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || !child.gameObject.activeInHierarchy) continue;

                var cdc = CardModelProvider.GetDuelSceneCDC(child.gameObject);
                if (cdc == null) continue;

                var model = CardModelProvider.GetCardModel(cdc);
                if (model == null) continue;

                uint instanceId = GetModelInstanceId(model);
                if (instanceId == 0) continue;

                uint grpId = GetModelGrpId(model);
                results.Add((model, instanceId, grpId));
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
                        return grpId > 0 ? CardModelProvider.GetNameFromGrpId(grpId) : null;
                    }
                }
            }
            catch { /* Reflection may fail on different game versions */ }
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

            var cdcComponent = CardModelProvider.GetDuelSceneCDC(card);
            if (cdcComponent == null) return "";

            var model = CardModelProvider.GetCardModel(cdcComponent);
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
                Log.Card("CardStateProvider", $"Error getting targeting text: {ex.Message}");
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

            var cdcComponent = CardModelProvider.GetDuelSceneCDC(cardObj);
            if (cdcComponent == null) return (false, false);

            var model = CardModelProvider.GetCardModel(cdcComponent);
            if (model == null) return (false, false);

            var modelType = model.GetType();

            // Log model properties once to discover ability-specific fields
            CardModelProvider.LogModelProperties(model);

            // Check CardTypes - spells have Instant, Sorcery, Creature, etc.
            // Abilities on the stack won't have these standard spell types
            var cardTypes = CardModelProvider.GetModelPropertyValue(model, modelType, "CardTypes") as IEnumerable;
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

                Log.Card("CardStateProvider", $"IsAbilityOnStack: hasSpellType={hasSpellType}, hasAbilityType={hasAbilityType}");

                // If has explicit Ability type or no spell types, it's an ability
                if (hasAbilityType || !hasSpellType)
                {
                    // Try to determine if triggered vs activated
                    // Check for AbilityType, TriggerType, or similar properties
                    var abilityType = CardModelProvider.GetModelPropertyValue(model, modelType, "AbilityType");
                    var triggerType = CardModelProvider.GetModelPropertyValue(model, modelType, "TriggerType");
                    var abilityCategory = CardModelProvider.GetModelPropertyValue(model, modelType, "AbilityCategory");

                    Log.Card("CardStateProvider", $"Ability properties: AbilityType={abilityType}, TriggerType={triggerType}, AbilityCategory={abilityCategory}");

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

            var cdcComponent = CardModelProvider.GetDuelSceneCDC(card);
            if (cdcComponent != null)
            {
                var model = CardModelProvider.GetCardModel(cdcComponent);
                if (model != null)
                {
                    try
                    {
                        EnsureModelPropsSearched(model.GetType());

                        // Check ownership from ControllerNum
                        if (_controllerNumProp != null)
                        {
                            var controller = _controllerNumProp.GetValue(model);
                            isOpponent = controller?.ToString() == "Opponent";
                        }

                        // Check IsBasicLand property
                        if (_isBasicLandProp != null && (bool)_isBasicLandProp.GetValue(model))
                            isLand = true;

                        // Check IsLandButNotBasic property
                        if (!isLand && _isLandButNotBasicProp != null && (bool)_isLandButNotBasicProp.GetValue(model))
                            isLand = true;

                        // Check CardTypes for Creature and Land
                        if (_cardTypesProp != null)
                        {
                            var cardTypes = _cardTypesProp.GetValue(model) as IEnumerable;
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
                        Log.Card("CardStateProvider", $"Error in GetCardCategory: {ex.Message}");
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
        /// Checks if a card is a creature or has the Vehicle subtype.
        /// Used to determine whether summoning sickness is relevant for a card.
        /// </summary>
        public static bool IsCreatureOrVehicleCard(GameObject card)
        {
            if (card == null) return false;
            var cdc = CardModelProvider.GetDuelSceneCDC(card);
            if (cdc == null) return false;
            var model = CardModelProvider.GetCardModel(cdc);
            if (model == null) return false;

            try
            {
                EnsureModelPropsSearched(model.GetType());

                if (_cardTypesProp != null)
                {
                    var cardTypes = _cardTypesProp.GetValue(model) as IEnumerable;
                    if (cardTypes != null)
                        foreach (var ct in cardTypes)
                            if (ct != null && ct.ToString().Contains("Creature"))
                                return true;
                }

                if (_subtypesProp != null)
                {
                    var subtypes = _subtypesProp.GetValue(model) as IEnumerable;
                    if (subtypes != null)
                        foreach (var st in subtypes)
                            if (st != null && st.ToString().Contains("Vehicle"))
                                return true;
                }
            }
            catch { }

            return false;
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
