# CardModelProvider.cs

Provides access to card Model data from the game's internal systems.
Handles reflection-based property access, name lookups, and card categorization.
Use CardDetector for card detection (IsCard, GetCardRoot).

## Class: CardModelProvider (static) (line 18)

### Cache Fields (line 21-99)
- static Type _cachedModelType (line 21)
- static readonly Dictionary<string, PropertyInfo> _modelPropertyCache (line 22)
- static bool _modelPropertiesLogged (line 23)
- static bool _abilityPropertiesLogged (line 24)
- static bool _listMetaCardHolderLogged (line 25)
- static object _idNameProvider (line 28)
- static MethodInfo _getNameMethod (line 29)
- static bool _idNameProviderSearched (line 30)
- static object _abilityTextProvider (line 33)
- static MethodInfo _getAbilityTextMethod (line 34)
- static bool _abilityTextProviderSearched (line 35)
- static readonly Dictionary<uint, (uint cardGrpId, uint[] abilityIds, uint cardTitleId)> _abilityParentCache (line 39)
- static object _flavorTextProvider (line 42)
- static MethodInfo _getFlavorTextMethod (line 43)
- static bool _flavorTextProviderSearched (line 44)
- static object _artistProvider (line 47)
- static MethodInfo _getArtistMethod (line 48)
- static bool _artistProviderSearched (line 49)

### Cache Management
- static void ClearCache() (line 55)
  Note: Clears all provider caches; call when scene changes

### Component Access Methods (line 102-254)
- static Component GetDuelSceneCDC(GameObject card) (line 109)
  Note: Gets DuelScene_CDC or Meta_CDC component
- static Component GetMetaCardView(GameObject card) (line 146)
  Note: Gets PagesMetaCardView, BoosterMetaCardView, etc. for Meta scenes
- static object GetCardModel(Component cdcComponent) (line 182)
  Note: Gets Model object containing card data
- static object GetMetaCardModel(Component metaCardView) (line 207)
- static void LogMetaCardViewProperties(Component metaCardView) (line 263)
- static void LogListMetaCardHolderProperties(MonoBehaviour holder, Type holderType) (line 294)
- static void LogCollectionContents(object collection, string collectionName) (line 414)

### Name Lookup Methods (line 482-805)
- static void FindIdNameProvider() (line 488)
  Note: Finds and caches an IdNameProvider instance for card name lookup
- static string GetNameFromGrpId(uint grpId) (line 748)
  Note: Gets card name from GrpId using CardTitleProvider lookup

### Debug Logging Methods (line 808-892)
- static void LogModelProperties(object model) (line 814)
  Note: Logs all properties on Model object; only logs once per session
- static void LogAbilityProperties(object ability) (line 844)

### Ability Text Methods (line 895-1000+)
- static string GetLoyaltyCostPrefix(object ability, Type abilityType) (line 901)
  Note: Extracts loyalty cost prefix (e.g., "+2: " or "-3: ")
- static string GetAbilityText(object ability, Type abilityType, uint cardGrpId, uint abilityId, uint[] abilityIds, uint cardTitleId) (line 931)
- static string GetAbilityTextFromProvider(uint cardGrpId, uint abilityId, uint[] abilityIds, uint cardTitleId) (line 983)
  Note: Uses AbilityTextProvider with full card context

Note: File is large (likely 2000+ lines based on first 1000 lines read).
Complete file includes additional methods for:
- Ability text extraction
- Flavor text lookup
- Artist lookup
- Card info extraction from various sources
- Collection quantity extraction
- Deck list card info
- Sideboard card info
- Read-only deck card info
- Combat state detection
- Targeting info
- Attachment info
- Zone type detection
- Extended info (keywords + linked faces)
- Card categorization (creature, land, opponent)

Full file structure available in actual source at C:\Users\fabia\arena\src\Core\Services\CardModelProvider.cs
