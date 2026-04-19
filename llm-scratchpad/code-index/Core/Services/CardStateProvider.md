# CardStateProvider.cs
Path: src/Core/Services/CardStateProvider.cs
Lines: 1088

## public static class CardStateProvider (line 18)

Card state information: attachments, combat state, targeting, and categorization. Uses CardModelProvider for low-level model/CDC access.

### Fields
- private static FieldInfo _attachedToIdField (line 23)
- private static bool _attachedToIdFieldSearched (line 24)
- private static PropertyInfo _zoneTypePropCached (line 27)
- private static bool _zoneTypePropSearched (line 28)
- private static readonly Dictionary<string, MemberInfo> _instanceMemberCache (line 31)
- private static readonly HashSet<string> _instanceMemberSearched (line 32)
- private static PropertyInfo _controllerNumProp (line 35)
- private static PropertyInfo _isBasicLandProp (line 36)
- private static PropertyInfo _isLandButNotBasicProp (line 37)
- private static PropertyInfo _cardTypesProp (line 38)
- private static PropertyInfo _instanceIdProp (line 39)
- private static PropertyInfo _grpIdProp (line 40)
- private static PropertyInfo _subtypesProp (line 41)
- private static bool _modelPropsSearched (line 42)

### Methods
- public static void ClearCache() (line 49)
- private static MemberInfo GetCachedInstanceMember(object instance, string name, BindingFlags flags) (line 73) — tries property first, then field
- private static bool GetBoolFromInstance(object model, string memberName, BindingFlags flags) (line 96)
- private static uint GetUintFromInstance(object model, string memberName, BindingFlags flags) (line 114)
- private static List<uint> GetUintListFromInstance(object model, string fieldName, BindingFlags flags) (line 132)
- private static bool GetBoolFromCard(GameObject card, Func<object, bool> accessor) (line 154)
- private static void EnsureModelPropsSearched(Type modelType) (line 171) — caches ControllerNum, IsBasicLand, IsLandButNotBasic, CardTypes, InstanceId, GrpId, Subtypes
- public static uint GetAttachedToId(object model) (line 193) — AttachedToId is a field, not property
- private static uint GetModelInstanceId(object model) (line 220)
- private static uint GetModelGrpId(object model) (line 239)
- private static List<(object model, uint instanceId, uint grpId)> GetAllBattlefieldCardModels() (line 255)
- public static bool IsGrpIdInNonCommandZone(uint grpId) (line 262) — scans battlefield, stack, graveyards, exile
- public static List<(uint instanceId, uint grpId, string name)> GetAttachments(GameObject card) (line 293)
- public static (uint instanceId, uint grpId, string name)? GetAttachedTo(GameObject card) (line 336)
- public static string GetAttachmentText(GameObject card) (line 379)
- public static bool GetIsAttacking(object model) (line 416)
- public static bool GetIsBlocking(object model) (line 417)
- public static List<uint> GetBlockingIds(object model) (line 418)
- public static List<uint> GetBlockedByIds(object model) (line 419)
- public static uint GetAttackTargetId(object model) (line 420)
- public static string ResolveInstanceIdToName(uint instanceId) (line 426)
- public static string ResolveInstanceIdToNameWithPT(uint instanceId) (line 448)
- public static string GetNonCreatureTypeLabel(GameObject card) (line 475) — Artifact/Enchantment/Planeswalker/Battle
- public static bool GetIsTapped(object model) (line 506)
- public static bool GetHasSummoningSickness(object model) (line 507)
- public static bool GetIsTappedFromCard(GameObject card) (line 510)
- public static bool GetHasSummoningSicknessFromCard(GameObject card) (line 511)
- public static bool GetIsAttackingFromCard(GameObject card) (line 512)
- public static bool GetIsBlockingFromCard(GameObject card) (line 513)
- public static string FormatCounterTypeName(string enumName) (line 519) — "P1P1" -> "+1/+1"
- public static string GetLocalizedCounterTypeName(object counterTypeEnum) (line 543) — uses GreLocProvider key "Enum/CounterType/CounterType_{name}"
- public static List<(string typeName, int count)> GetCountersFromCard(GameObject card) (line 563)
- public static string GetModelZoneTypeName(object model) (line 634) — game's internal zone (may differ from UI holder)
- public static string GetCardZoneTypeName(GameObject card) (line 664)
- public static List<uint> GetTargetIds(object model) (line 677)
- public static List<uint> GetTargetedByIds(object model) (line 678)
- private static List<(object model, uint instanceId, uint grpId)> GetAllStackCardModels() (line 680)
- private static List<(object model, uint instanceId, uint grpId)> GetAllCardModelsInHolder(string holderNameContains) (line 688) — uses DuelHolderCache
- public static string ResolveInstanceIdToNameExtended(uint instanceId) (line 719) — tries battlefield then stack
- public static string GetTargetingText(GameObject card) (line 748)
- public static (bool isAbility, bool isTriggered) IsAbilityOnStack(GameObject cardObj) (line 830) — checks CardTypes enum, language-agnostic
- public static (bool isCreature, bool isLand, bool isOpponent) GetCardCategory(GameObject card) (line 923)
- public static bool IsCreatureCard(GameObject card) (line 991)
- public static bool IsLandCard(GameObject card) (line 1000)
- public static bool IsCreatureOrVehicleCard(GameObject card) (line 1009) — checks CardTypes and Subtypes
- public static bool IsOpponentCard(GameObject card) (line 1048)
- private static bool IsOpponentCardFallback(GameObject card) (line 1057) — uses parent hierarchy "opponent"/"local" names, then screen position
