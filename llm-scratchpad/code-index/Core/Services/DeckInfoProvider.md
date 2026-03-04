# DeckInfoProvider.cs

Provides reflection-based access to deck statistics from the game's UI components (DeckMainTitlePanel, DeckCostsDetails, DeckTypesDetails). All data is read from live game UI text, not computed by the mod.

## static class DeckInfoProvider (line 20)

### Constants
- PrivateInstance (BindingFlags) (line 22)
- PublicInstance (BindingFlags) (line 24)
- CostBarFieldNames (string[]) (line 68)
- CostBarLabels (string[]) (line 72)
- TypeLineFieldNames (string[]) (line 78)
- TypeLineDisplayNames (string[]) (line 82)

### Private Fields - Cached Components (line 27)
- _cachedTitlePanel (MonoBehaviour) (line 28)
- _cachedCostsDetails (MonoBehaviour) (line 29)
- _cachedTypesDetails (MonoBehaviour) (line 30)

### Private Fields - DeckMainTitlePanel Reflection (line 32)
- _cardCountLabelField (FieldInfo) (line 33)
- _titlePanelReflectionInit (bool) (line 34)

### Private Fields - DeckCostsDetails Reflection (line 36)
- _costBarFields (FieldInfo[]) (line 37)
- _costBarQuantityLabelField (FieldInfo) (line 38)
- _averageTextField (FieldInfo) (line 39)
- _creaturesItemField (FieldInfo) (line 40)
- _othersItemField (FieldInfo) (line 41)
- _landsItemField (FieldInfo) (line 42)
- _typeLineQuantityField (FieldInfo) (line 43)
- _typeLinePercentField (FieldInfo) (line 44)
- _setDeckMethod (MethodInfo) (line 45)
- _costsDetailsReflectionInit (bool) (line 46)

### Private Fields - Pantry Reflection (line 48)
- _pantryGetModelProviderMethod (MethodInfo) (line 49)
- _modelProperty (PropertyInfo) (line 50)
- _getFilteredMainDeckMethod (MethodInfo) (line 51)
- _pantryReflectionInit (bool) (line 52)

### Private Fields - DeckTypesDetails Reflection (line 54)
- _typesItemParentField (FieldInfo) (line 55)
- _typesSetDeckMethod (MethodInfo) (line 56)
- _lineItemType (Type) (line 57)
- _lineItemNameField (FieldInfo) (line 58)
- _lineItemQuantityField (FieldInfo) (line 59)
- _typesDetailsReflectionInit (bool) (line 60)

### Private Fields - CardDatabase Reflection (line 62)
- _pantryGetCardDatabaseMethod (MethodInfo) (line 63)
- _greLocProviderProperty (PropertyInfo) (line 64)
- _cardDatabaseReflectionInit (bool) (line 65)

### Private Struct - TypeGroup (line 90)
- TypeName (string) (line 92)
- TypeQuantity (string) (line 93)
- Subtypes (List<(string, string)>) (line 94)

### Public Methods
- GetCardCountText() → string (line 101) - Returns card count text like "35 von 60"
- GetDeckInfoElements() → List<(string, string)> (line 142) - Returns two navigable items: card info and mana curve
- GetDeckInfoRows() → List<(string, List<string>)> (line 171) - Returns 2D navigation rows
- ClearCache() (line 481) - Clear cached components on scene change

### Private Methods - Data Building (line 208)
- BuildCardInfoEntries(MonoBehaviour, List<TypeGroup>) → List<string> (line 208) - Note: integrates type details into entries
- ClassifyTypeGroups(...) (line 282) - Categorizes type groups into creature/others/land
- ReadTypeLineCount(MonoBehaviour, FieldInfo) → int (line 314)
- BuildManaCurveEntries(MonoBehaviour) → List<string> (line 332)
- BuildCardInfoText(MonoBehaviour) → string (line 370)
- BuildManaCurveText(MonoBehaviour) → string (line 413)
- GetAverageCostText(MonoBehaviour) → string (line 458)

### Private Methods - Component Discovery (line 488)
- FindTitlePanel() → MonoBehaviour (line 490)
- FindCostsDetails() → MonoBehaviour (line 514)
- PopulateCostsDetails(MonoBehaviour) (line 550) - Note: calls SetDeck via Pantry reflection
- FindTypesDetails() → MonoBehaviour (line 584)
- PopulateTypesDetails(MonoBehaviour) (line 619) - Note: skips if already populated (Destroy deferred)
- GetGreLocProvider() → object (line 666)
- ReadTypeGroups(MonoBehaviour) → List<TypeGroup> (line 693)
- IsValidCached(MonoBehaviour) → bool (line 769)
- IsValidCachedAllowInactive(MonoBehaviour) → bool (line 786)

### Private Methods - Reflection Init (line 802)
- InitializeTitlePanelReflection(Type) (line 804)
- InitializeCostsDetailsReflection(Type) (line 820)
- InitializePantryReflection() (line 889)
- InitializeTypesDetailsReflection(Type) (line 952)
- InitializeCardDatabaseReflection() (line 996)

### Private Methods - TMP_Text Helpers (line 1051)
- FindTmpTextOnObject(GameObject) → object (line 1056)
- GetTmpTextValue(object) → string (line 1087)
