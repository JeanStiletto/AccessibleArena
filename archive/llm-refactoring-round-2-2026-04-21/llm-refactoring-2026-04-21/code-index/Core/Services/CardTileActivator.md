# CardTileActivator.cs
Path: src/Core/Services/CardTileActivator.cs
Lines: 483

## Top-level comments
- Specialized activation utilities for collection cards, commander/companion/partner slot tiles, and deck entries in the deck builder. Keeps UIActivator focused on generic UI activation.

## public static class CardTileActivator (line 13)

### Fields
- private const int MaxDeckSearchDepth = 5 (line 17)
- private const int MaxDeckViewSearchDepth = 6 (line 18)
- private const string DeckViewTypeName = "DeckView" (line 19)
- private static System.Reflection.MethodInfo _pantryGetFilterProviderMethod (line 26)
- private static System.Reflection.PropertyInfo _filterProperty (line 27)
- private static System.Reflection.MethodInfo _isSetMethod (line 28)
- private static object _commandersEnumValue (line 29)
- private static bool _filterReflectionInit (line 30)

### Methods
- public static bool IsCollectionCard(GameObject element) (line 41) — PagesMetaCardView/MetaCardView inside PoolHolder
- public static bool IsInCommanderContainer(GameObject element) (line 82) — stops at MainDeckContentCONTAINER
- public static bool? IsCommandersFilterActive() (line 104) — lazy-initializes reflection via Pantry/DeckBuilderCardFilterProvider, caches MethodInfo/PropertyInfo
- public static bool IsCommanderSlotCard(GameObject element) (line 153) — "CustomButton - Tile" inside CardTileCommander/Partner/Companion_CONTAINER
- public static bool IsDeckListCard(GameObject element) (line 178) — CustomButton/Tile inside MainDeckContentCONTAINER or MainDeck_MetaCardHolder
- public static ActivationResult TryActivateCollectionCard(GameObject cardElement) (line 203) — sets InputManager.BlockNextEnterKeyUp to prevent auto-craft, then delegates to UIActivator.SimulatePointerClick
- public static bool TrySelectDeck(GameObject deckElement) (line 228) — invokes DeckView.OnDeckClick() directly via reflection
- public static bool IsDeckEntry(GameObject element) (line 276) — excludes TextBox (rename field)
- private static MonoBehaviour FindDeckViewInParents(GameObject element) (line 298)
- public static bool IsDeckSelected(GameObject deckElement) (line 325) — compares DeckView with DeckViewSelector._selectedDeckView
- public static string GetDeckInvalidStatus(GameObject deckElement) (line 344) — reads _animateInvalid/_invalidCardCount/_animateCraftable/_animateUncraftable/_animateInvalidCompanion/_animateUnavailable fields
- public static string GetDeckInvalidTooltip(GameObject deckElement) (line 375) — reads DeckView._tooltipTrigger.TooltipData.Text
- private static TValue GetFieldValue&lt;TValue&gt;(System.Type type, object instance, string fieldName, System.Reflection.BindingFlags flags) (line 418)
- private static MonoBehaviour GetSelectedDeckView() (line 432) — scans scene for DeckViewSelector_Base, reads _selectedDeckView field
- private static void Log(string message) (line 476)
