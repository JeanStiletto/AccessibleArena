using System;
using System.Reflection;
using AccessibleArena.Core.Utils;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Reads collection completion numbers out of the game's <c>SetCollectionController</c>.
    ///
    /// The controller keeps owned/available counts per (set, metric) pair, where a metric is the
    /// total, one of four rarities, or one of six colors. Nothing about those numbers reaches the
    /// UI as text — the game draws fill bars and a bare percentage — so both the profile summary
    /// and the set collection screen read them from here instead of scraping widgets.
    ///
    /// One-of versus playset: once the player owns at least one of every card in a set (or in one
    /// metric of it), the game silently reuses the same fields for 4-of counting. Callers get that
    /// flag back so the announcement can say which scale a number is on.
    /// </summary>
    public static class SetCollectionDataProvider
    {
        #region Reflection Cache

        private sealed class CollectionHandles
        {
            public MethodInfo GetMetricTotals;
            public MethodInfo IsCollectionComplete;
            public MethodInfo GetMostRecentExpansion;
            public MethodInfo UpdateOwnedCards;
            public MethodInfo UpdateMetricTotalsPerSet;
            public Type MetricsEnumType;
            public FieldInfo NumOwned;
            public FieldInfo NumAvailable;
            public object CountModeUsePlayerInvOneOf;
            public MethodInfo SetMetadataIsAlchemy;
        }

        private static readonly ReflectionCache<CollectionHandles> _cache = new ReflectionCache<CollectionHandles>(
            builder: controllerType =>
            {
                var h = new CollectionHandles
                {
                    GetMetricTotals = controllerType.GetMethod("GetMetricTotals", PublicInstance),
                    IsCollectionComplete = controllerType.GetMethod("IsCollectionComplete", PublicInstance),
                    GetMostRecentExpansion = controllerType.GetMethod("GetMostRecentExpansion", PublicInstance),
                    UpdateOwnedCards = controllerType.GetMethod("UpdateOwnedCards", PublicInstance),
                    UpdateMetricTotalsPerSet = controllerType.GetMethod("UpdateMetricTotalsPerSet", PublicInstance),
                    MetricsEnumType = controllerType.GetNestedType("Metrics", BindingFlags.Public | BindingFlags.NonPublic),
                };

                var totalsType = controllerType.GetNestedType("MetricTotals", BindingFlags.Public | BindingFlags.NonPublic);
                if (totalsType != null)
                {
                    h.NumOwned = totalsType.GetField("numOwned", AllInstanceFlags);
                    h.NumAvailable = totalsType.GetField("numAvailable", AllInstanceFlags);
                }

                // CountMode is a separate top-level enum. UsePlayerInvOneOf is the mode the game
                // itself passes when deciding whether a set has flipped to playset tracking.
                var countModeType = FindType("CountMode");
                if (countModeType != null && Enum.IsDefined(countModeType, "UsePlayerInvOneOf"))
                    h.CountModeUsePlayerInvOneOf = Enum.Parse(countModeType, "UsePlayerInvOneOf");

                var metadataType = FindType("ISetMetadataProvider");
                if (metadataType != null)
                    h.SetMetadataIsAlchemy = metadataType.GetMethod("IsAlchemy", PublicInstance);

                return h;
            },
            validator: h => h.GetMetricTotals != null && h.MetricsEnumType != null
                         && h.NumOwned != null && h.NumAvailable != null,
            logTag: "SetCollectionData",
            logSubject: T.SetCollectionController);

        private static CollectionHandles H => _cache.Handles;

        /// <summary>Prepares the reflection cache for a live controller instance.</summary>
        public static bool EnsureInitialized(object collectionController)
        {
            if (collectionController == null) return false;
            return _cache.EnsureInitialized(collectionController.GetType());
        }

        #endregion

        #region Metrics

        /// <summary>Boxed <c>SetCollectionController.Metrics</c> value, or null if unavailable.</summary>
        public static object GetMetric(string enumName)
        {
            if (H?.MetricsEnumType == null || string.IsNullOrEmpty(enumName)) return null;
            try
            {
                return Enum.IsDefined(H.MetricsEnumType, enumName)
                    ? Enum.Parse(H.MetricsEnumType, enumName)
                    : null;
            }
            catch { return null; }
        }

        /// <summary>The "everything in the set" metric (<c>Metrics.None</c>).</summary>
        public static object TotalMetric => GetMetric("None");

        #endregion

        #region Totals

        /// <summary>
        /// Owned/available for one metric of one set. <paramref name="isPlayset"/> reports that
        /// the numbers are 4-of counts because the set is already complete at one-of.
        /// </summary>
        public static bool TryGetTotals(object collectionController, string expansionCode, object metric,
            bool isAlchemySet, out int owned, out int available, out bool isPlayset)
        {
            owned = 0;
            available = 0;
            isPlayset = false;

            if (collectionController == null || metric == null) return false;
            if (!EnsureInitialized(collectionController)) return false;
            if (string.IsNullOrEmpty(expansionCode)) return false;

            try
            {
                object totals = H.GetMetricTotals.Invoke(collectionController, new[] { (object)expansionCode, metric });
                if (totals == null) return false;

                owned = Convert.ToInt32(H.NumOwned.GetValue(totals));
                available = Convert.ToInt32(H.NumAvailable.GetValue(totals));
                isPlayset = IsCompleteAtOneOf(collectionController, expansionCode, metric, isAlchemySet);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("SetCollectionData", $"GetMetricTotals failed for {expansionCode}: {ex.Message}");
                return false;
            }
        }

        private static bool IsCompleteAtOneOf(object collectionController, string expansionCode, object metric, bool isAlchemySet)
        {
            if (H?.IsCollectionComplete == null || H.CountModeUsePlayerInvOneOf == null) return false;

            try
            {
                object result = H.IsCollectionComplete.Invoke(collectionController,
                    new[] { (object)expansionCode, metric, H.CountModeUsePlayerInvOneOf, (object)isAlchemySet });
                return result as bool? ?? false;
            }
            catch { return false; }
        }

        /// <summary>Percentage floor, matching how the game renders its completion labels.</summary>
        public static int Percent(int owned, int available)
        {
            if (available <= 0) return 0;
            return (int)Math.Floor(owned * 100.0 / available);
        }

        #endregion

        #region Controller Queries

        /// <summary>
        /// Rebuilds the controller's cached counts. The dictionaries are only refreshed on
        /// demand, so anything the player did since the last visit (opening packs, crafting)
        /// would otherwise be missing.
        /// </summary>
        public static void RefreshTotals(object collectionController)
        {
            if (collectionController == null) return;
            if (!EnsureInitialized(collectionController)) return;

            try
            {
                H.UpdateOwnedCards?.Invoke(collectionController, null);
                H.UpdateMetricTotalsPerSet?.Invoke(collectionController, null);
            }
            catch (Exception ex)
            {
                Log.Warn("SetCollectionData", $"Refreshing collection totals failed: {ex.Message}");
            }
        }

        /// <summary>The newest published set — the one the profile badge summarizes.</summary>
        public static object GetMostRecentExpansion(object collectionController)
        {
            if (collectionController == null) return null;
            if (!EnsureInitialized(collectionController)) return null;

            try { return H.GetMostRecentExpansion?.Invoke(collectionController, null); }
            catch (Exception ex)
            {
                Log.Warn("SetCollectionData", $"GetMostRecentExpansion failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>Asks the game's metadata provider whether an expansion is an Alchemy set.</summary>
        public static bool IsAlchemy(object setMetadataProvider, object expansionCode)
        {
            if (setMetadataProvider == null || expansionCode == null) return false;
            if (H?.SetMetadataIsAlchemy == null) return false;

            try
            {
                return H.SetMetadataIsAlchemy.Invoke(setMetadataProvider, new[] { expansionCode }) as bool? ?? false;
            }
            catch { return false; }
        }

        /// <summary>
        /// Localized set name for an expansion code. Alchemy codes ("Y23_ONE") use a dashed
        /// form in the localization table, matching what SetBadge.Init does.
        /// </summary>
        public static string GetSetName(string expansionCode, bool isAlchemySet)
        {
            if (string.IsNullOrEmpty(expansionCode)) return "";
            string lookupCode = isAlchemySet ? expansionCode.Replace('_', '-') : expansionCode;
            return UITextExtractor.MapSetCodeToName(lookupCode);
        }

        #endregion
    }
}
