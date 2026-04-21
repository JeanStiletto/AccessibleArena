using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Provides reflection-based access to the game's CardPoolHolder API.
    /// Used for collection page navigation in the deck builder.
    /// Caches FieldInfo/MethodInfo/PropertyInfo objects for performance.
    /// </summary>
    public static class CardPoolAccessor
    {

        // Cached component reference (invalidated on scene change via ClearCache)
        private static MonoBehaviour _cachedPoolHolder;

        private sealed class Handles
        {
            // CardPoolHolder members
            public FieldInfo Pages;            // _pages (List<Page>)
            public FieldInfo CurrentPage;      // _currentPage (int)
            public FieldInfo IsScrolling;      // _isScrolling (bool)
            public MethodInfo ScrollNext;      // ScrollNext() (private)
            public MethodInfo ScrollPrevious;  // ScrollPrevious() (private)
            public PropertyInfo PageCount;     // PageCount (private)
            // Nested Page class member (derived from Pages field type's nested Page type)
            public FieldInfo PageCardViews;    // Page.CardViews (public)
        }

        private static readonly ReflectionCache<Handles> _cache = new ReflectionCache<Handles>(
            builder: t =>
            {
                var h = new Handles
                {
                    Pages = t.GetField("_pages", PrivateInstance),
                    CurrentPage = t.GetField("_currentPage", PrivateInstance),
                    IsScrolling = t.GetField("_isScrolling", PrivateInstance),
                    ScrollNext = t.GetMethod("ScrollNext", PrivateInstance),
                    ScrollPrevious = t.GetMethod("ScrollPrevious", PrivateInstance),
                    PageCount = t.GetProperty("PageCount", PrivateInstance),
                };
                var pageType = t.GetNestedType("Page", BindingFlags.NonPublic);
                if (pageType != null)
                    h.PageCardViews = pageType.GetField("CardViews", PublicInstance);
                return h;
            },
            validator: h =>
                h.Pages != null && h.CurrentPage != null && h.IsScrolling != null
                && h.ScrollNext != null && h.ScrollPrevious != null
                && h.PageCount != null && h.PageCardViews != null,
            logTag: "CardPoolAccessor",
            logSubject: "CardPoolHolder");

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
                if (typeName == T.CardPoolHolder || typeName == T.ScrollCardPoolHolder)
                {
                    _cachedPoolHolder = mb;
                    _cache.EnsureInitialized(mb.GetType());
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
                    if (typeName == T.CardPoolHolder || typeName == T.ScrollCardPoolHolder)
                    {
                        _cachedPoolHolder = mb;
                        _cache.EnsureInitialized(mb.GetType());
                        return _cachedPoolHolder;
                    }
                }
                parentSearch = parentSearch.parent;
            }

            return null;
        }

        /// <summary>
        /// Get the card GameObjects on the currently visible page.
        /// Returns only active cards (filters out empty slots).
        /// </summary>
        public static List<GameObject> GetCurrentPageCards()
        {
            var result = new List<GameObject>();

            if (_cachedPoolHolder == null || !_cache.IsInitialized)
                return result;

            try
            {
                var h = _cache.Handles;
                var pages = h.Pages.GetValue(_cachedPoolHolder) as IList;
                if (pages == null || pages.Count < 2)
                    return result;

                // _pages[1] is always the currently visible page
                var currentPage = pages[1];
                if (currentPage == null)
                    return result;

                var cardViews = h.PageCardViews.GetValue(currentPage) as IList;
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
                Log.Error("CardPoolAccessor", $"GetCurrentPageCards failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Navigate to the next page. Returns true if successful.
        /// </summary>
        public static bool ScrollNext()
        {
            if (_cachedPoolHolder == null || !_cache.IsInitialized)
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

                _cache.Handles.ScrollNext.Invoke(_cachedPoolHolder, null);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("CardPoolAccessor", $"ScrollNext failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Navigate to the previous page. Returns true if successful.
        /// </summary>
        public static bool ScrollPrevious()
        {
            if (_cachedPoolHolder == null || !_cache.IsInitialized)
                return false;

            try
            {
                // Check boundaries: _currentPage > 0
                int currentPage = GetCurrentPageIndex();
                if (currentPage <= 0)
                    return false;

                if (IsScrolling())
                    return false;

                _cache.Handles.ScrollPrevious.Invoke(_cachedPoolHolder, null);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("CardPoolAccessor", $"ScrollPrevious failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the current page index (0-based).
        /// </summary>
        public static int GetCurrentPageIndex()
        {
            if (_cachedPoolHolder == null || !_cache.IsInitialized)
                return 0;

            try
            {
                return (int)_cache.Handles.CurrentPage.GetValue(_cachedPoolHolder);
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
            if (_cachedPoolHolder == null || !_cache.IsInitialized)
                return 1;

            try
            {
                return (int)_cache.Handles.PageCount.GetValue(_cachedPoolHolder);
            }
            catch
            {
                return 1;
            }
        }

        /// <summary>
        /// Check if a scroll animation is in progress.
        /// </summary>
        public static bool IsScrolling()
        {
            if (_cachedPoolHolder == null || !_cache.IsInitialized)
                return false;

            try
            {
                return (bool)_cache.Handles.IsScrolling.GetValue(_cachedPoolHolder);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if the CardPoolAccessor has a valid cached component and reflection is initialized.
        /// </summary>
        public static bool IsValid()
        {
            return _cachedPoolHolder != null && _cache.IsInitialized;
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
