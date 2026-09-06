using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Computes the section key each card belongs to, for Ctrl+PageUp/PageDown section
    /// jumps in the deck builder. Sections mirror the game's own sort criteria, so
    /// boundaries are authoritative rather than a mod-invented ordering:
    /// - Collection pool: SortPool(BasicLandsFirst, [IsNew,] ColorOrder, CMCWithXLast, Title)
    ///   with CardSortHelpers.GetColorSortOrder — basics first, mono W/U/B/R/G, multicolor,
    ///   colorless last; artifacts and lands bucket by color IDENTITY, others by color.
    /// - Deck list / sideboard: SetSortAndFilter(LandLast, CMCWithXLast, ColorOrder, Title)
    ///   — mana-value runs, X spells after them, lands as the final run.
    /// Keys only need to change exactly where the game's sort bucket changes; the pure
    /// boundary math lives in <see cref="SectionJump"/>.
    /// </summary>
    public static class DeckSectionProvider
    {
        // Pool section keys (order matches the game's color sort table)
        private const long PoolKeyBasicLands = 0;
        private const long PoolKeyMonoFirst = 1;   // 1..5 = White, Blue, Black, Red, Green
        private const long PoolKeyMulticolor = 6;
        private const long PoolKeyColorless = 7;

        // Deck list section keys: 0..999 = mana value, X spells and lands sort after
        // all normal mana values (mirrors CMCWithXLast's +1000 and LandLast)
        private const long DeckKeyXSpells = 2000;
        private const long DeckKeyLands = 3000;

        /// <summary>Handles for PagesMetaCardViewDisplayInformation (the pool's per-tile info).</summary>
        private sealed class DisplayInfoHandles
        {
            public FieldInfo Card;                 // Card (CardPrintingData)
        }

        /// <summary>Handles for CardPrintingData (same instance type behind pool and deck lookups).</summary>
        private sealed class PrintingHandles
        {
            public PropertyInfo ColorFlags;           // CardColorFlags (cast colors)
            public PropertyInfo ColorIdentityFlags;   // CardColorFlags (color identity)
            public PropertyInfo Types;                // IReadOnlyList<CardType>
            public PropertyInfo ConvertedManaCost;    // uint
            public PropertyInfo CastingCost;          // IEnumerable<ManaQuantity> (X detection; optional)
            public PropertyInfo IsBasicLandUnlimited; // bool (optional, IsBasicLand fallback)
            public PropertyInfo IsBasicLand;          // bool (optional)
            public PropertyInfo LinkedFaceType;       // LinkedFace enum (optional, split cards)
            public PropertyInfo LinkedFacePrintings;  // IEnumerable<CardPrintingData> (optional)
        }

        private static readonly ReflectionCache<DisplayInfoHandles> _displayInfoCache = new ReflectionCache<DisplayInfoHandles>(
            builder: t => new DisplayInfoHandles
            {
                Card = t.GetField("Card", PublicInstance),
            },
            validator: h => h.Card != null,
            logTag: "DeckSectionProvider",
            logSubject: "PagesMetaCardViewDisplayInformation");

        private static readonly ReflectionCache<PrintingHandles> _printingCache = new ReflectionCache<PrintingHandles>(
            builder: t => new PrintingHandles
            {
                ColorFlags = t.GetProperty("ColorFlags", PublicInstance),
                ColorIdentityFlags = t.GetProperty("ColorIdentityFlags", PublicInstance),
                Types = t.GetProperty("Types", PublicInstance),
                ConvertedManaCost = t.GetProperty("ConvertedManaCost", PublicInstance),
                CastingCost = t.GetProperty("CastingCost", PublicInstance),
                IsBasicLandUnlimited = t.GetProperty("IsBasicLandUnlimited", PublicInstance),
                IsBasicLand = t.GetProperty("IsBasicLand", PublicInstance),
                LinkedFaceType = t.GetProperty("LinkedFaceType", PublicInstance),
                LinkedFacePrintings = t.GetProperty("LinkedFacePrintings", PublicInstance),
            },
            validator: h =>
                h.ColorFlags != null && h.ColorIdentityFlags != null
                && h.Types != null && h.ConvertedManaCost != null,
            logTag: "DeckSectionProvider",
            logSubject: "CardPrintingData");

        // GrpId -> deck section key. Card data never changes within a game session,
        // so this avoids a card-database lookup per tile on every keypress.
        private static readonly Dictionary<uint, long> _deckKeyCache = new Dictionary<uint, long>();

        /// <summary>
        /// One section key per entry of the pool's full sorted card list
        /// (all pages, same order as CardPoolHolder._cardDisplayInfos).
        /// Empty list when the pool or reflection is unavailable.
        /// </summary>
        public static List<long> GetPoolSectionKeys()
        {
            var keys = new List<long>();
            var infos = CardPoolAccessor.GetPoolDisplayInfos();
            if (infos == null || infos.Count == 0)
                return keys;

            try
            {
                _displayInfoCache.EnsureInitialized(infos[0].GetType());
                if (!_displayInfoCache.IsInitialized)
                    return keys;

                long previous = long.MinValue;
                int leadingUnknown = 0;
                foreach (var info in infos)
                {
                    var printing = info != null ? _displayInfoCache.Handles.Card.GetValue(info) : null;
                    long key = previous;
                    if (printing != null && TryGetPoolKey(printing, out long k))
                        key = k;

                    if (key == long.MinValue)
                        leadingUnknown++;   // not-yet-loaded leading entries; backfilled below
                    keys.Add(key);
                    previous = key;
                }

                // Leading placeholders inherit the first real key so they never form
                // a fake section of their own
                if (leadingUnknown > 0 && leadingUnknown < keys.Count)
                {
                    long firstReal = keys[leadingUnknown];
                    for (int i = 0; i < leadingUnknown; i++)
                        keys[i] = firstReal;
                }
                else if (leadingUnknown == keys.Count)
                {
                    keys.Clear();
                }
            }
            catch (Exception ex)
            {
                Log.Error("DeckSectionProvider", $"GetPoolSectionKeys failed: {ex.Message}");
                keys.Clear();
            }

            return keys;
        }

        /// <summary>
        /// Section key for one deck-list/sideboard card. Returns false when the card
        /// data cannot be resolved (the caller should inherit the neighbor's key).
        /// </summary>
        public static bool TryGetDeckCardKey(uint grpId, out long key)
        {
            if (_deckKeyCache.TryGetValue(grpId, out key))
                return true;

            key = 0;
            try
            {
                var printing = CardModelProvider.GetCardDataFromGrpId(grpId);
                if (printing == null)
                    return false;

                _printingCache.EnsureInitialized(printing.GetType());
                if (!_printingCache.IsInitialized)
                    return false;

                var h = _printingCache.Handles;
                if (TypesContain(printing, h, "Land"))
                {
                    key = DeckKeyLands;
                }
                else if (HasXInCastingCost(printing, h))
                {
                    key = DeckKeyXSpells;
                }
                else
                {
                    key = GetEffectiveManaValue(printing, h);
                }

                _deckKeyCache[grpId] = key;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("DeckSectionProvider", $"TryGetDeckCardKey({grpId}) failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Spoken label for a pool section key (localized).</summary>
        public static string PoolSectionLabel(long key)
        {
            switch (key)
            {
                case PoolKeyBasicLands: return Models.Strings.DeckSectionBasicLands;
                case PoolKeyMonoFirst: return Models.Strings.ManaWhite;
                case PoolKeyMonoFirst + 1: return Models.Strings.ManaBlue;
                case PoolKeyMonoFirst + 2: return Models.Strings.ManaBlack;
                case PoolKeyMonoFirst + 3: return Models.Strings.ManaRed;
                case PoolKeyMonoFirst + 4: return Models.Strings.ManaGreen;
                case PoolKeyMulticolor: return Models.Strings.ManaMulticolor;
                case PoolKeyColorless: return Models.Strings.ManaColorless;
                default: return null;
            }
        }

        /// <summary>Spoken label for a deck-list section key (localized).</summary>
        public static string DeckSectionLabel(long key)
        {
            if (key >= DeckKeyLands) return Models.Strings.DeckSectionLands;
            if (key >= DeckKeyXSpells) return Models.Strings.DeckSectionManaValue("X");
            return Models.Strings.DeckSectionManaValue(key);
        }

        /// <summary>
        /// Pool color bucket, mirroring CardSortHelpers.GetColorSortOrder: artifacts and
        /// lands bucket by color identity, everything else by cast colors; basics (which
        /// BasicLandsFirst hoists to the front) get their own bucket.
        /// </summary>
        private static bool TryGetPoolKey(object printing, out long key)
        {
            key = 0;
            _printingCache.EnsureInitialized(printing.GetType());
            if (!_printingCache.IsInitialized)
                return false;

            var h = _printingCache.Handles;

            var basicProp = h.IsBasicLandUnlimited ?? h.IsBasicLand;
            if (basicProp != null && basicProp.GetValue(printing) is true)
            {
                key = PoolKeyBasicLands;
                return true;
            }

            bool artifactOrLand = TypesContain(printing, h, "Artifact") || TypesContain(printing, h, "Land");
            var flagsProp = artifactOrLand ? h.ColorIdentityFlags : h.ColorFlags;
            int flags = Convert.ToInt32(flagsProp.GetValue(printing)) & 0x1F;

            if (flags == 0)
            {
                key = PoolKeyColorless;
                return true;
            }

            // Mono color: bucket per color (flag bits: W=1, U=2, B=4, R=8, G=16)
            if ((flags & (flags - 1)) == 0)
            {
                int bit = 0;
                while ((flags >>= 1) != 0) bit++;
                key = PoolKeyMonoFirst + bit;
                return true;
            }

            key = PoolKeyMulticolor;
            return true;
        }

        private static bool TypesContain(object printing, PrintingHandles h, string typeName)
        {
            if (!(h.Types.GetValue(printing) is IEnumerable types))
                return false;
            foreach (var t in types)
            {
                if (t != null && t.ToString() == typeName)
                    return true;
            }
            return false;
        }

        private static bool HasXInCastingCost(object printing, PrintingHandles h)
        {
            if (h.CastingCost == null)
                return false;
            if (!(h.CastingCost.GetValue(printing) is IEnumerable cost))
                return false;

            foreach (var quantity in cost)
            {
                if (quantity == null) continue;
                var quantityType = quantity.GetType();
                object color = quantityType.GetProperty("Color", PublicInstance)?.GetValue(quantity)
                    ?? quantityType.GetField("Color", PublicInstance)?.GetValue(quantity);
                string name = color?.ToString();
                if (name == "X" || name == "Y")
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Mana value with the game's split-card rule: SplitHalf faces sort by the
        /// cheaper half (CardSortHelpers.GetManaCostSortOrder).
        /// </summary>
        private static long GetEffectiveManaValue(object printing, PrintingHandles h)
        {
            long cmc = Convert.ToInt64(h.ConvertedManaCost.GetValue(printing));

            try
            {
                if (h.LinkedFaceType != null && h.LinkedFacePrintings != null
                    && h.LinkedFaceType.GetValue(printing)?.ToString() == "SplitHalf"
                    && h.LinkedFacePrintings.GetValue(printing) is IEnumerable faces)
                {
                    foreach (var face in faces)
                    {
                        if (face == null) continue;
                        long faceCmc = Convert.ToInt64(h.ConvertedManaCost.GetValue(face));
                        if (faceCmc < cmc) cmc = faceCmc;
                    }
                }
            }
            catch
            {
                // Split-face refinement is best-effort; the printed mana value is fine
            }

            return cmc;
        }

        /// <summary>
        /// Clear cached per-card keys (e.g. on scene change; cheap to rebuild).
        /// Reflection member caches are preserved since types don't change.
        /// </summary>
        public static void ClearCache()
        {
            _deckKeyCache.Clear();
        }
    }
}
