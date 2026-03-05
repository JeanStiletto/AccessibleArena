# CardPoolAccessor.cs

Provides reflection-based access to the game's CardPoolHolder API.
Used for collection page navigation in the deck builder.
Caches FieldInfo/MethodInfo/PropertyInfo objects for performance.

## Class: CardPoolAccessor (static) (line 15)

### Cached Reflection Members (line 17-40)
- static readonly BindingFlags PrivateInstance (line 17)
- static readonly BindingFlags PublicInstance (line 19)
- static MonoBehaviour _cachedPoolHolder (line 23)
  Note: Invalidated on scene change via ClearCache
- static FieldInfo _pagesField (line 26)
  Note: _pages (List<Page>)
- static FieldInfo _currentPageField (line 27)
  Note: _currentPage (int)
- static FieldInfo _isScrollingField (line 28)
  Note: _isScrolling (bool)
- static FieldInfo _cardDisplayInfosField (line 29)
  Note: _cardDisplayInfos (protected)
- static MethodInfo _scrollNextMethod (line 30)
  Note: ScrollNext() (private)
- static MethodInfo _scrollPreviousMethod (line 31)
  Note: ScrollPrevious() (private)
- static PropertyInfo _pageSizeProperty (line 32)
  Note: PageSize (private)
- static PropertyInfo _pageCountProperty (line 33)
  Note: PageCount (private)
- static Type _pageType (line 36)
  Note: CardPoolHolder+Page
- static FieldInfo _pageCardViewsField (line 37)
  Note: Page.CardViews (public)
- static bool _reflectionInitialized (line 39)

### Component Search (line 42-110)
- static MonoBehaviour FindCardPoolHolder() (line 45)
  Note: Find and cache the CardPoolHolder component; returns cached if still valid

### Reflection Initialization (line 112-159)
- static void InitializeReflection(Type type) (line 116)
  Note: Initialize all reflection members from CardPoolHolder type; called once when component first found

### Card Access Methods (line 161-202)
- static List<GameObject> GetCurrentPageCards() (line 165)
  Note: Get card GameObjects on currently visible page (_pages[1]); filters out empty slots

### Scroll Navigation (line 204-259)
- static bool ScrollNext() (line 207)
  Note: Navigate to next page; checks boundaries and scrolling state
- static bool ScrollPrevious() (line 236)
  Note: Navigate to previous page; checks boundaries and scrolling state

### State Query Methods (line 261-331)
- static int GetCurrentPageIndex() (line 264)
  Note: Get current page index (0-based)
- static int GetPageCount() (line 282)
  Note: Get total number of pages
- static int GetPageSize() (line 300)
  Note: Get number of cards per page
- static bool IsScrolling() (line 318)
  Note: Check if scroll animation is in progress

### Collection Info Methods (line 333-362)
- static int GetTotalCardCount() (line 336)
  Note: Get total number of cards in filtered collection via _cardDisplayInfos

### Cache Management (line 364-380)
- static bool IsValid() (line 367)
  Note: Check if accessor has valid cached component and reflection is initialized
- static void ClearCache() (line 376)
  Note: Clear cached component reference; call on scene changes; reflection members preserved
