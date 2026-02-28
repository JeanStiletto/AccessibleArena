using System.Collections.Generic;
using UnityEngine;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Shared static cache for duel card holder GameObjects.
    /// Holders (e.g., "BattlefieldCardHolder", "LocalHand") persist for the entire duel;
    /// only their children change. This avoids repeated FindObjectsOfType scene scans
    /// in ZoneNavigator, BattlefieldNavigator, and DuelAnnouncer.
    /// </summary>
    public static class DuelHolderCache
    {
        private static readonly Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>();

        /// <summary>
        /// Gets a cached card holder container by name pattern.
        /// On first call (or if Unity destroyed the object), finds via scene scan and caches.
        /// </summary>
        public static GameObject GetHolder(string nameContains)
        {
            if (_cache.TryGetValue(nameContains, out var cached) && cached != null)
                return cached;

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go != null && go.activeInHierarchy && go.name.Contains(nameContains))
                {
                    _cache[nameContains] = go;
                    return go;
                }
            }

            _cache.Remove(nameContains);
            return null;
        }

        /// <summary>
        /// Clears all cached holders. Called on duel end (DuelAnnouncer.Deactivate).
        /// </summary>
        public static void Clear()
        {
            _cache.Clear();
        }
    }
}
