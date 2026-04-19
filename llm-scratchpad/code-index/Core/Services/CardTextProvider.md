# CardTextProvider.cs
Path: src/Core/Services/CardTextProvider.cs
Lines: 614

## public static class CardTextProvider (line 18)

Localized card text lookups (ability text, flavor text, artist names). Reflects over game providers (AbilityTextProvider, GreLocProvider, ArtistProvider).

### Fields
- private static object _abilityTextProvider (line 21)
- private static MethodInfo _getAbilityTextMethod (line 22)
- private static bool _abilityTextProviderSearched (line 23)
- private static object _flavorTextProvider (line 26)
- private static MethodInfo _getFlavorTextMethod (line 27)
- private static bool _flavorTextProviderSearched (line 28)
- private static object _artistProvider (line 31)
- private static MethodInfo _getArtistMethod (line 32)
- private static bool _artistProviderSearched (line 33)

### Methods
- public static void ClearCache() (line 38)
- internal static string GetLoyaltyCostPrefix(object ability, Type abilityType) (line 55) — builds "+1: " / "-3: " with screen-reader-friendly Plus/Minus prefix
- internal static string GetAbilityText(object ability, Type abilityType, uint cardGrpId, uint abilityId, uint[] abilityIds, uint cardTitleId) (line 90) — tries provider, then properties (Text/RulesText/AbilityText/TextContent/Description), then GetText()
- internal static string GetAbilityTextFromProvider(uint cardGrpId, uint abilityId, uint[] abilityIds, uint cardTitleId) (line 141)
- private static void FindAbilityTextProvider() (line 192) — walks GameManager.CardDatabase.*Text*/*Ability* props
- private static void FindFlavorTextProvider() (line 305) — GreLocProvider first, then ClientLocProvider fallback
- private static void FindArtistProvider() (line 471)
- internal static string GetFlavorText(uint flavorId) (line 530)
- internal static string GetLocalizedTextById(uint locId) (line 545) — reuses flavor text provider; works for TypeTextId, SubtypeTextId, FlavorTextId
- internal static string GetArtistName(uint artistId) (line 590)
