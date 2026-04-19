# CardPoolAccessor.cs
Path: src/Core/Services/CardPoolAccessor.cs
Lines: 379

## public static class CardPoolAccessor (line 17)

Reflection-based access to the game's CardPoolHolder API for collection page navigation in the deck builder. Caches FieldInfo/MethodInfo/PropertyInfo.

### Fields
- private static MonoBehaviour _cachedPoolHolder (line 21) — invalidated on scene change
- private static FieldInfo _pagesField (line 24) — _pages (List<Page>)
- private static FieldInfo _currentPageField (line 25) — _currentPage (int)
- private static FieldInfo _isScrollingField (line 26) — _isScrolling (bool)
- private static FieldInfo _cardDisplayInfosField (line 27) — _cardDisplayInfos (protected)
- private static MethodInfo _scrollNextMethod (line 28) — ScrollNext() (private)
- private static MethodInfo _scrollPreviousMethod (line 29) — ScrollPrevious() (private)
- private static PropertyInfo _pageSizeProperty (line 30) — PageSize (private)
- private static PropertyInfo _pageCountProperty (line 31) — PageCount (private)
- private static Type _pageType (line 34) — CardPoolHolder+Page nested
- private static FieldInfo _pageCardViewsField (line 35) — Page.CardViews (public)
- private static bool _reflectionInitialized (line 37)

### Methods
- public static MonoBehaviour FindCardPoolHolder() (line 43) — walks PoolHolder hierarchy including parent, matches CardPoolHolder/ScrollCardPoolHolder
- private static void InitializeReflection(Type type) (line 114)
- public static List<GameObject> GetCurrentPageCards() (line 163) — reads _pages[1] (visible page)
- public static bool ScrollNext() (line 205)
- public static bool ScrollPrevious() (line 234)
- public static int GetCurrentPageIndex() (line 262)
- public static int GetPageCount() (line 280)
- public static int GetPageSize() (line 298)
- public static bool IsScrolling() (line 316)
- public static int GetTotalCardCount() (line 334) — reads _cardDisplayInfos.Count
- public static bool IsValid() (line 365)
- public static void ClearCache() (line 374) — preserves reflection members
