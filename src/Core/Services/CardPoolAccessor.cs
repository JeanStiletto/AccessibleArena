using UnityEngine;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Provides reflection-based access to the game's CardPoolHolder API.
    /// Used for collection page navigation in the deck builder.
    /// Caches FieldInfo/MethodInfo/PropertyInfo objects for performance.
    /// </summary>
    public static class CardPoolAccessor
    {
        private static readonly BindingFlags PrivateInstance =
            BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly BindingFlags PublicInstance =
            BindingFlags.Public | BindingFlags.Instance;

        // Cached component reference (invalidated on scene change via ClearCache)
        private static MonoBehaviour _cachedPoolHolder;

        // Cached reflection members for CardPoolHolder
        private static FieldInfo _pagesField;              // _pages (List<Page>)
        private static FieldInfo _currentPageField;        // _currentPage (int)
        private static FieldInfo _isScrollingField;        // _isScrolling (bool)
        private static FieldInfo _cardDisplayInfosField;   // _cardDisplayInfos (protected)
        private static MethodInfo _scrollNextMethod;       // ScrollNext() (private)
        private static MethodInfo _scrollPreviousMethod;   // ScrollPrevious() (private)
        private static PropertyInfo _pageSizeProperty;     // PageSize (private)
        private static PropertyInfo _pageCountProperty;    // PageCount (private)

        // Cached reflection members for nested Page class
        private static Type _pageType;                     // CardPoolHolder+Page
        private static FieldInfo _pageCardViewsField;      // Page.CardViews (public)

        private static bool _reflectionInitialized;

        /// <summary>
        /// Find and cache the CardPoolHolder component on the PoolHolder hierarchy.
        /// Returns the cached component if still valid, otherwise searches again.
        /// </summary>
        public static MonoBehaviour FindCardPoolHolder()
        {
            // Return cached if still valid
            if (_cachedPoolHolder != null)
            {
                try
                {
                    // Check if the Unity object is still alive
                    if (_cachedPoolHolder.gameObject != null && _cachedPoolHolder.gameObject.activeInHierarchy)
                        return _cachedPoolHolder;
                }
                catch
                {
                    // Object was destroyed
                }
                _cachedPoolHolder = null;
            }

            // Find PoolHolder GameObject
            var poolHolder = GameObject.Find("PoolHolder");
            if (poolHolder == null)
                return null;

            // Search for CardPoolHolder component
            foreach (var mb in poolHolder.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName == "CardPoolHolder" || typeName == "ScrollCardPoolHolder")
                {
                    _cachedPoolHolder = mb;

                    if (!_reflectionInitialized)
                    {
                        InitializeReflection(mb.GetType());
                    }

                    return _cachedPoolHolder;
                }
            }

            // Also check the parent hierarchy (CardPoolHolder may be ON the PoolHolder itself)
            var parentSearch = poolHolder.transform;
            while (parentSearch != null)
            {
                foreach (var mb in parentSearch.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    string typeName = mb.GetType().Name;
                    if (typeName == "CardPoolHolder" || typeName == "ScrollCardPoolHolder")
                    {
                        _cachedPoolHolder = mb;

                        if (!_reflectionInitialized)
                        {
                            InitializeReflection(mb.GetType());
                        }

                        return _cachedPoolHolder;
                    }
                }
                parentSearch = parentSearch.parent;
            }

            return null;
        }

        /// <summary>
        /// Initialize all reflection members from the CardPoolHolder type.
        /// Called once when the component is first found.
        /// </summary>
        private static void InitializeReflection(Type type)
        {
            if (_reflectionInitialized) return;

            try
            {
                // CardPoolHolder fields
                _pagesField = type.GetField("_pages", PrivateInstance);
                _currentPageField = type.GetField("_currentPage", PrivateInstance);
                _isScrollingField = type.GetField("_isScrolling", PrivateInstance);

                // _cardDisplayInfos is protected, declared on CardPoolHolder itself
                _cardDisplayInfosField = type.GetField("_cardDisplayInfos",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

                // Private methods
                _scrollNextMethod = type.GetMethod("ScrollNext", PrivateInstance);
                _scrollPreviousMethod = type.GetMethod("ScrollPrevious", PrivateInstance);

                // Private properties
                _pageSizeProperty = type.GetProperty("PageSize", PrivateInstance);
                _pageCountProperty = type.GetProperty("PageCount", PrivateInstance);

                // Nested Page class
                _pageType = type.GetNestedType("Page", BindingFlags.NonPublic);
                if (_pageType != null)
                {
                    _pageCardViewsField = _pageType.GetField("CardViews", PublicInstance);
                }

                _reflectionInitialized = true;

                MelonLogger.Msg($"[CardPoolAccessor] Reflection init on {type.Name}: " +
                    $"_pages={_pagesField != null}, _currentPage={_currentPageField != null}, " +
                    $"_isScrolling={_isScrollingField != null}, _cardDisplayInfos={_cardDisplayInfosField != null}, " +
                    $"ScrollNext={_scrollNextMethod != null}, ScrollPrev={_scrollPreviousMethod != null}, " +
                    $"PageSize={_pageSizeProperty != null}, PageCount={_pageCountProperty != null}, " +
                    $"PageType={_pageType != null}, CardViews={_pageCardViewsField != null}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CardPoolAccessor] Reflection init failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the card GameObjects on the currently visible page.
        /// Returns only active cards (filters out empty slots).
        /// </summary>
        public static List<GameObject> GetCurrentPageCards()
        {
            var result = new List<GameObject>();

            if (_cachedPoolHolder == null || _pagesField == null || _pageCardViewsField == null)
                return result;

            try
            {
                var pages = _pagesField.GetValue(_cachedPoolHolder) as IList;
                if (pages == null || pages.Count < 2)
                    return result;

                // _pages[1] is always the currently visible page
                var currentPage = pages[1];
                if (currentPage == null)
                    return result;

                var cardViews = _pageCardViewsField.GetValue(currentPage) as IList;
                if (cardViews == null)
                    return result;

                foreach (var cv in cardViews)
                {
                    var mb = cv as MonoBehaviour;
                    if (mb != null && mb.gameObject != null && mb.gameObject.activeInHierarchy)
                    {
                        result.Add(mb.gameObject);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CardPoolAccessor] GetCurrentPageCards failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Navigate to the next page. Returns true if successful.
        /// </summary>
        public static bool ScrollNext()
        {
            if (_cachedPoolHolder == null || _scrollNextMethod == null)
                return false;

            try
            {
                // Check boundaries: _currentPage < PageCount - 1
                int currentPage = GetCurrentPageIndex();
                int pageCount = GetPageCount();
                if (currentPage >= pageCount - 1)
                    return false;

                if (IsScrolling())
                    return false;

                _scrollNextMethod.Invoke(_cachedPoolHolder, null);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CardPoolAccessor] ScrollNext failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Navigate to the previous page. Returns true if successful.
        /// </summary>
        public static bool ScrollPrevious()
        {
            if (_cachedPoolHolder == null || _scrollPreviousMethod == null)
                return false;

            try
            {
                // Check boundaries: _currentPage > 0
                int currentPage = GetCurrentPageIndex();
                if (currentPage <= 0)
                    return false;

                if (IsScrolling())
                    return false;

                _scrollPreviousMethod.Invoke(_cachedPoolHolder, null);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CardPoolAccessor] ScrollPrevious failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the current page index (0-based).
        /// </summary>
        public static int GetCurrentPageIndex()
        {
            if (_cachedPoolHolder == null || _currentPageField == null)
                return 0;

            try
            {
                return (int)_currentPageField.GetValue(_cachedPoolHolder);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the total number of pages.
        /// </summary>
        public static int GetPageCount()
        {
            if (_cachedPoolHolder == null || _pageCountProperty == null)
                return 1;

            try
            {
                return (int)_pageCountProperty.GetValue(_cachedPoolHolder);
            }
            catch
            {
                return 1;
            }
        }

        /// <summary>
        /// Get the number of cards per page.
        /// </summary>
        public static int GetPageSize()
        {
            if (_cachedPoolHolder == null || _pageSizeProperty == null)
                return 0;

            try
            {
                return (int)_pageSizeProperty.GetValue(_cachedPoolHolder);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Check if a scroll animation is in progress.
        /// </summary>
        public static bool IsScrolling()
        {
            if (_cachedPoolHolder == null || _isScrollingField == null)
                return false;

            try
            {
                return (bool)_isScrollingField.GetValue(_cachedPoolHolder);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the total number of cards in the filtered collection.
        /// </summary>
        public static int GetTotalCardCount()
        {
            if (_cachedPoolHolder == null || _cardDisplayInfosField == null)
                return 0;

            try
            {
                var displayInfos = _cardDisplayInfosField.GetValue(_cachedPoolHolder);
                if (displayInfos == null)
                    return 0;

                // IReadOnlyList has Count property
                var countProp = displayInfos.GetType().GetProperty("Count");
                if (countProp != null)
                    return (int)countProp.GetValue(displayInfos);

                // Fallback: try ICollection
                if (displayInfos is ICollection collection)
                    return collection.Count;

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Check if the CardPoolAccessor has a valid cached component and reflection is initialized.
        /// </summary>
        public static bool IsValid()
        {
            return _cachedPoolHolder != null && _reflectionInitialized;
        }

        /// <summary>
        /// Clear the cached component reference. Call on scene changes.
        /// Reflection members are preserved since types don't change.
        /// </summary>
        public static void ClearCache()
        {
            _cachedPoolHolder = null;
        }
    }
}
