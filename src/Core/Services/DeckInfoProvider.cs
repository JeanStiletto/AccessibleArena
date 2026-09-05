using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using AccessibleArena.Core.Models;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Provides deck statistics for the deck builder's Deck Info group and the deck
    /// details popup. The card count line is read from the game's DeckMainTitlePanel UI
    /// (it carries the format's deck size, e.g. "35/60 Karten"); everything else — type
    /// breakdown with subtypes, basic-land count, mana curve, color distribution — is
    /// computed from the deck model (Pantry → DeckBuilderModelProvider.Model
    /// .GetFilteredMainDeck()), the same list the game hands its own DeckCostsDetails /
    /// DeckTypesDetails / DeckColorsDetails widgets. Computing replaces the old approach
    /// of reading those widgets' TMP texts: the widgets are only repopulated when their
    /// popup opens, so UI reads went stale after every deck edit. The aggregation
    /// algorithms below mirror the game widgets 1:1 so we announce the same numbers a
    /// sighted player sees.
    /// </summary>
    public static class DeckInfoProvider
    {
        // Cached component reference (cleared on scene change)
        private static MonoBehaviour _cachedTitlePanel;        // DeckMainTitlePanel

        private sealed class TitlePanelHandles
        {
            public FieldInfo CardCountLabel;   // _cardCountLabel (Localize)
        }

        private sealed class PantryHandles
        {
            public MethodInfo GetModelProvider;    // Pantry.Get<DeckBuilderModelProvider>()
            public PropertyInfo Model;             // DeckBuilderModelProvider.Model
            public MethodInfo GetFilteredMainDeck; // DeckBuilderModel.GetFilteredMainDeck()
        }

        /// <summary>
        /// Handles for one deck entry. Bound to CardPrintingQuantity (stable type — the
        /// filtered main deck always yields it) plus the CardPrintingData members hanging
        /// off its Printing field.
        /// </summary>
        private sealed class DeckItemHandles
        {
            public FieldInfo Printing;             // CardPrintingQuantity.Printing (CardPrintingData)
            public FieldInfo Quantity;             // CardPrintingQuantity.Quantity (uint)
            public PropertyInfo Types;             // CardPrintingData.Types (IReadOnlyList<CardType>)
            public PropertyInfo Subtypes;          // CardPrintingData.Subtypes (IReadOnlyList<SubType>)
            public PropertyInfo IsBasicLand;       // CardPrintingData.IsBasicLand (bool)
            public PropertyInfo ColorFlags;        // CardPrintingData.ColorFlags (CardColorFlags)
            public PropertyInfo ConvertedManaCost; // CardPrintingData.ConvertedManaCost (uint)
            public PropertyInfo GrpId;             // CardPrintingData.GrpId (uint) — optional, name lookups
            public int[] ColorBits;                // CardColorFlags values for White,Blue,Black,Red,Green
        }

        private static readonly ReflectionCache<TitlePanelHandles> _titlePanelCache = new ReflectionCache<TitlePanelHandles>(
            builder: t => new TitlePanelHandles
            {
                CardCountLabel = t.GetField("_cardCountLabel", PrivateInstance),
            },
            validator: h => h.CardCountLabel != null,
            logTag: "DeckInfoProvider",
            logSubject: "DeckMainTitlePanel");

        private static readonly ReflectionCache<PantryHandles> _pantryCache = new ReflectionCache<PantryHandles>(
            builder: pantryType =>
            {
                var h = new PantryHandles();
                Type modelProviderType = FindType("Core.Code.Decks.DeckBuilderModelProvider");
                if (modelProviderType == null) return h;

                var getMethod = pantryType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                if (getMethod != null && getMethod.IsGenericMethod)
                    h.GetModelProvider = getMethod.MakeGenericMethod(modelProviderType);

                h.Model = modelProviderType.GetProperty("Model", PublicInstance)
                       ?? modelProviderType.GetProperty("Model", PrivateInstance);

                if (h.Model != null)
                {
                    var modelType = h.Model.PropertyType;
                    h.GetFilteredMainDeck = modelType.GetMethod("GetFilteredMainDeck", PublicInstance)
                                         ?? modelType.GetMethod("GetFilteredMainDeck", PrivateInstance);
                }
                return h;
            },
            validator: h => h.GetModelProvider != null && h.Model != null && h.GetFilteredMainDeck != null,
            logTag: "DeckInfoProvider",
            logSubject: "Pantry");

        private static readonly ReflectionCache<DeckItemHandles> _deckItemCache = new ReflectionCache<DeckItemHandles>(
            builder: t =>
            {
                var h = new DeckItemHandles
                {
                    Printing = t.GetField("Printing", PublicInstance),
                    Quantity = t.GetField("Quantity", PublicInstance),
                };

                var printingType = h.Printing?.FieldType;
                if (printingType != null)
                {
                    h.Types = printingType.GetProperty("Types", PublicInstance);
                    h.Subtypes = printingType.GetProperty("Subtypes", PublicInstance);
                    h.IsBasicLand = printingType.GetProperty("IsBasicLand", PublicInstance);
                    h.ColorFlags = printingType.GetProperty("ColorFlags", PublicInstance);
                    h.ConvertedManaCost = printingType.GetProperty("ConvertedManaCost", PublicInstance);
                    h.GrpId = printingType.GetProperty("GrpId", PublicInstance);
                }

                if (h.ColorFlags != null)
                {
                    var flagsType = h.ColorFlags.PropertyType;
                    var bits = new int[ColorBitNames.Length];
                    for (int i = 0; i < ColorBitNames.Length; i++)
                    {
                        try { bits[i] = Convert.ToInt32(Enum.Parse(flagsType, ColorBitNames[i])); }
                        catch { bits[i] = 0; }
                    }
                    h.ColorBits = bits;
                }
                return h;
            },
            validator: h =>
                h.Printing != null && h.Quantity != null && h.Types != null
                && h.Subtypes != null && h.IsBasicLand != null
                && h.ColorFlags != null && h.ConvertedManaCost != null,
            logTag: "DeckInfoProvider",
            logSubject: "CardPrintingQuantity");

        private static readonly string[] ColorBitNames = { "White", "Blue", "Black", "Red", "Green" };

        /// <summary>
        /// The display order the game's DeckTypesDetails uses. Types outside this list are
        /// omitted, matching the game's popup. English enum names — CardType.ToString() is
        /// always English regardless of game language.
        /// </summary>
        private static readonly string[] DisplayTypeOrder =
        {
            "Creature", "Instant", "Sorcery", "Artifact", "Enchantment", "Planeswalker", "Battle", "Land"
        };

        private const int LandOrderIndex = 7;

        private sealed class SubtypeStat
        {
            public string Name;
            public uint Quantity;
        }

        private sealed class TypeGroupStat
        {
            public int Order;               // index into DisplayTypeOrder
            public string Name;             // localized (via GreLocProvider), enum name fallback
            public uint Quantity;
            public readonly Dictionary<int, SubtypeStat> Subtypes = new Dictionary<int, SubtypeStat>();
        }

        private sealed class DeckStats
        {
            public uint Total;                  // all cards
            public uint BasicLands;
            public uint NonLandTotal;           // denominator for color percentages
            public readonly Dictionary<int, TypeGroupStat> TypeGroups = new Dictionary<int, TypeGroupStat>();
            public readonly uint[] CurveBuckets = new uint[6];  // CMC 1-or-less .. 6-or-more, lands excluded
            public uint AvgCards;               // non-land cards (average denominator)
            public uint AvgManaSum;             // sum of CMC * quantity over non-land cards
            public readonly uint[] ColorCounts = new uint[7];   // W,U,B,R,G + Multicolor + Colorless
        }

        /// <summary>
        /// Get the card count text (e.g., "35/60 Karten") from DeckMainTitlePanel.
        /// Reads the rendered TMP_Text from the _cardCountLabel's GameObject.
        /// </summary>
        public static string GetCardCountText()
        {
            var panel = FindTitlePanel();
            if (panel == null) return null;

            try
            {
                if (!_titlePanelCache.IsInitialized) return null;

                // _cardCountLabel is a Localize component (MonoBehaviour)
                var localizeComponent = _titlePanelCache.Handles.CardCountLabel.GetValue(panel);
                if (localizeComponent == null) return null;

                // The Localize component is on a GameObject that also has TMP_Text
                var componentObj = localizeComponent as Component;
                if (componentObj == null) return null;

                // Find TMP_Text on the same GameObject
                var tmpText = FindTmpTextOnObject(componentObj.gameObject);
                if (tmpText != null)
                {
                    string text = GetTmpTextValue(tmpText);
                    if (!string.IsNullOrEmpty(text))
                        return text.Trim();
                }
            }
            catch (Exception ex)
            {
                Log.Error("DeckInfoProvider", $"Error reading card count: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Build all info elements for the Deck Info group / deck details popup:
        /// one flat text per row, entries joined with periods for screen reader pauses.
        /// A row whose label is empty is self-describing (the card count row) and must be
        /// announced without a label prefix.
        /// </summary>
        public static List<(string label, string text)> GetDeckInfoElements()
        {
            var elements = new List<(string label, string text)>();
            foreach (var (label, entries) in GetDeckInfoRows())
                elements.Add((label, string.Join(". ", entries)));
            return elements;
        }

        /// <summary>
        /// Get deck info as rows with individual sub-entries for 2D navigation.
        /// Each row has a label and a list of individual entries navigable with Left/Right.
        /// Row 1 (no label): card count, then one entry per card type in the game's display
        ///   order, with subtype counts (and basic-land count on the land entry)
        /// Row 2 (Mana Curve): CMC buckets (1 or less through 6 or more) and average
        /// Row 3 (Colors): non-land card counts per color, multicolor, colorless
        /// </summary>
        public static List<(string label, List<string> entries)> GetDeckInfoRows()
        {
            var rows = new List<(string label, List<string> entries)>();

            var stats = ComputeDeckStats(GetMainDeckData());

            // Row 1: card count + type breakdown. The count text already says "Karten"/
            // "Cards", so the row carries no label — prefixing one just repeats the word.
            var cardEntries = BuildCardInfoEntries(stats);
            if (cardEntries.Count > 0)
                rows.Add((string.Empty, cardEntries));

            if (stats != null)
            {
                rows.Add((Strings.DeckInfoManaCurve, BuildManaCurveEntries(stats)));

                var colorEntries = BuildColorEntries(stats);
                if (colorEntries.Count > 0)
                    rows.Add((Strings.DeckInfoColors, colorEntries));
            }

            // Last row: legality status with the game's own localized reasons. Sighted
            // players get this only as a color-coded card count plus save-time popups.
            // Self-describing, so no row label. Absent outside the deck builder.
            var legality = DeckLegalityProvider.GetDeckStatus();
            if (legality != null && !string.IsNullOrEmpty(legality.FormatName))
            {
                string entry = legality.IsValid
                    ? Strings.DeckLegalityLegal(legality.FormatName)
                    : Strings.DeckLegalityIllegal(legality.FormatName, legality.Reasons ?? string.Empty);
                rows.Add((string.Empty, new List<string> { entry }));
            }

            return rows;
        }

        /// <summary>
        /// Card info entries: the count line from the game UI, then one entry per card
        /// type. No percentages here — with per-type entries the ratio is audible from the
        /// counts, and a percent on only some entries reads as inconsistent. The land entry
        /// reports how many lands are basic. Subtypes are appended by descending quantity
        /// so the main tribes come first when listening.
        /// </summary>
        private static List<string> BuildCardInfoEntries(DeckStats stats)
        {
            var entries = new List<string>();

            string cardCount = GetCardCountText();
            if (!string.IsNullOrEmpty(cardCount))
                entries.Add(cardCount);

            if (stats == null || stats.Total == 0)
                return entries;

            foreach (var group in stats.TypeGroups.Values.OrderBy(g => g.Order))
            {
                var sb = new StringBuilder();
                sb.Append($"{group.Quantity} {group.Name}");

                if (group.Order == LandOrderIndex && stats.BasicLands > 0)
                    sb.Append($", {Strings.DeckInfoBasicLands((int)stats.BasicLands)}");

                foreach (var sub in group.Subtypes.Values
                    .OrderByDescending(s => s.Quantity)
                    .ThenBy(s => s.Name, StringComparer.CurrentCulture))
                {
                    sb.Append($", {sub.Quantity} {sub.Name}");
                }

                entries.Add(sb.ToString());
            }

            return entries;
        }

        /// <summary>
        /// Mana curve entries: ["1 or less: 4", "2: 8", ..., "6 or more: 2", "Average: 3.5"].
        /// Mirrors DeckCostsDetails: lands are excluded from the buckets and the average.
        /// All buckets are always shown; the average is omitted for a deck with no
        /// non-land cards.
        /// </summary>
        private static List<string> BuildManaCurveEntries(DeckStats stats)
        {
            var entries = new List<string>();

            for (int i = 0; i < stats.CurveBuckets.Length; i++)
            {
                string label = i == 0 ? Strings.DeckInfoCurveOneOrLess
                             : i == stats.CurveBuckets.Length - 1 ? Strings.DeckInfoCurveSixOrMore
                             : (i + 1).ToString();
                entries.Add($"{label}: {stats.CurveBuckets[i]}");
            }

            if (stats.AvgCards > 0)
            {
                double avg = (double)stats.AvgManaSum / stats.AvgCards;
                entries.Add($"{Strings.DeckInfoAverage}: {avg:0.0}");
            }

            return entries;
        }

        /// <summary>
        /// Color entries: ["12 White, 40%", ..., "3 Multicolor, 10%", "2 Colorless, 7%"].
        /// Mirrors DeckColorsDetails: lands are excluded, a multicolor card counts toward
        /// each of its colors and toward Multicolor, percentages are of non-land cards.
        /// Zero-quantity colors are skipped, like the game hides their rows.
        /// </summary>
        private static List<string> BuildColorEntries(DeckStats stats)
        {
            var entries = new List<string>();
            if (stats.NonLandTotal == 0) return entries;

            string[] names =
            {
                Strings.ManaWhite, Strings.ManaBlue, Strings.ManaBlack,
                Strings.ManaRed, Strings.ManaGreen,
                Strings.ManaMulticolor, Strings.ManaColorless
            };

            for (int i = 0; i < names.Length; i++)
            {
                uint qty = stats.ColorCounts[i];
                if (qty == 0) continue;
                entries.Add($"{qty} {names[i]}, {Percent(qty, stats.NonLandTotal)}%");
            }

            return entries;
        }

        private static int Percent(uint quantity, uint total)
        {
            return (int)Math.Round(quantity * 100.0 / total);
        }

        #region Suggest Lands Support

        private sealed class FilterProviderHandles
        {
            public MethodInfo GetFilterProvider;    // Pantry.Get<DeckBuilderCardFilterProvider>()
            public PropertyInfo IsAutoSuggestLandsOn; // DeckBuilderCardFilterProvider.IsAutoSuggestLandsToggleOn
        }

        private static readonly ReflectionCache<FilterProviderHandles> _filterProviderCache = new ReflectionCache<FilterProviderHandles>(
            builder: pantryType =>
            {
                var h = new FilterProviderHandles();
                Type filterProviderType = FindType("DeckBuilderCardFilterProvider");
                if (filterProviderType == null) return h;

                var getMethod = pantryType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                if (getMethod != null && getMethod.IsGenericMethod)
                    h.GetFilterProvider = getMethod.MakeGenericMethod(filterProviderType);

                h.IsAutoSuggestLandsOn = filterProviderType.GetProperty("IsAutoSuggestLandsToggleOn", PublicInstance);
                return h;
            },
            validator: h => h.GetFilterProvider != null && h.IsAutoSuggestLandsOn != null,
            logTag: "DeckInfoProvider",
            logSubject: "DeckBuilderCardFilterProvider");

        /// <summary>
        /// State of the game's "Suggest Lands" auto-toggle. Null when unavailable.
        /// Only meaningful inside the deck builder — the game's getter dereferences the
        /// builder context.
        /// </summary>
        internal static bool? IsAutoSuggestLandsOn()
        {
            try
            {
                if (!_filterProviderCache.IsInitialized)
                {
                    Type pantryType = FindType("Wizards.Mtga.Pantry");
                    if (pantryType == null) return null;
                    if (!_filterProviderCache.EnsureInitialized(pantryType)) return null;
                }

                var fh = _filterProviderCache.Handles;
                var provider = fh.GetFilterProvider.Invoke(null, null);
                if (provider == null) return null;
                return fh.IsAutoSuggestLandsOn.GetValue(provider) as bool?;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Basic-land counts of the current main deck, keyed by card name (printings of
        /// the same basic are merged). Null when the deck model is unavailable — callers
        /// must distinguish that from an empty dictionary (no basics).
        /// Used to announce what the game's land suggester changed.
        /// </summary>
        internal static Dictionary<string, uint> GetBasicLandCounts()
        {
            var deckData = GetMainDeckData();
            if (!(deckData is System.Collections.IEnumerable items)) return null;

            var counts = new Dictionary<string, uint>();
            try
            {
                foreach (var item in items)
                {
                    if (item == null) continue;

                    _deckItemCache.EnsureInitialized(item.GetType());
                    if (!_deckItemCache.IsInitialized) return null;
                    var h = _deckItemCache.Handles;
                    if (h.GrpId == null) return null;

                    var printing = h.Printing.GetValue(item);
                    if (printing == null) continue;
                    if (!(bool)h.IsBasicLand.GetValue(printing)) continue;

                    uint qty = Convert.ToUInt32(h.Quantity.GetValue(item));
                    if (qty == 0) continue;

                    uint grpId = Convert.ToUInt32(h.GrpId.GetValue(printing));
                    string name = CardModelProvider.GetNameFromGrpId(grpId);
                    if (string.IsNullOrEmpty(name))
                        name = $"#{grpId}";

                    counts.TryGetValue(name, out uint existing);
                    counts[name] = existing + qty;
                }
            }
            catch (Exception ex)
            {
                Log.Error("DeckInfoProvider", $"Error counting basic lands: {ex.Message}");
                return null;
            }

            return counts;
        }

        #endregion

        #region Deck Model Aggregation

        /// <summary>
        /// Get the current main deck as the game's IReadOnlyList&lt;CardPrintingQuantity&gt;
        /// via Pantry.Get&lt;DeckBuilderModelProvider&gt;().Model.GetFilteredMainDeck().
        /// </summary>
        private static object GetMainDeckData()
        {
            try
            {
                EnsurePantryInitialized();
                if (!_pantryCache.IsInitialized) return null;

                var ph = _pantryCache.Handles;

                var modelProvider = ph.GetModelProvider.Invoke(null, null);
                if (modelProvider == null) return null;

                var model = ph.Model.GetValue(modelProvider);
                if (model == null) return null;

                return ph.GetFilteredMainDeck.Invoke(model, null);
            }
            catch (Exception ex)
            {
                Log.Error("DeckInfoProvider", $"Error reading deck model: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Aggregate deck statistics from the deck model, replicating the game's widget
        /// algorithms:
        /// - Types (DeckTypesDetails.SetDeck): a card counts toward every one of its types,
        ///   and all of its subtypes are tallied under each of those types.
        /// - Curve (DeckCostsDetails.SetDeck): CMC clamped to 1..6, lands excluded from the
        ///   bucket counts and the average.
        /// - Colors (DeckColorsDetails.SetDeck): lands excluded; multicolor cards count
        ///   toward each color and toward Multicolor; colorless is ColorFlags == None.
        /// Returns null when the model or reflection is unavailable.
        /// </summary>
        private static DeckStats ComputeDeckStats(object deckData)
        {
            if (!(deckData is System.Collections.IEnumerable items)) return null;

            var stats = new DeckStats();

            try
            {
                foreach (var item in items)
                {
                    if (item == null) continue;

                    _deckItemCache.EnsureInitialized(item.GetType());
                    if (!_deckItemCache.IsInitialized) return null;
                    var h = _deckItemCache.Handles;

                    var printing = h.Printing.GetValue(item);
                    if (printing == null) continue;

                    uint qty = Convert.ToUInt32(h.Quantity.GetValue(item));
                    if (qty == 0) continue;

                    stats.Total += qty;

                    // Types: collect (order, enumValue) pairs; also classify creature/land
                    bool isCreature = false, isLand = false;
                    var cardTypes = new List<(int order, int enumValue)>();
                    if (h.Types.GetValue(printing) is System.Collections.IEnumerable types)
                    {
                        foreach (var typeValue in types)
                        {
                            string name = typeValue.ToString();
                            if (name == "Creature") isCreature = true;
                            else if (name == "Land") isLand = true;

                            int order = Array.IndexOf(DisplayTypeOrder, name);
                            if (order >= 0)
                                cardTypes.Add((order, Convert.ToInt32(typeValue)));
                        }
                    }

                    // Subtypes of this card (shared by all of its type groups, like the game)
                    List<(int enumValue, string enumName)> cardSubtypes = null;
                    if (h.Subtypes.GetValue(printing) is System.Collections.IEnumerable subtypes)
                    {
                        cardSubtypes = new List<(int, string)>();
                        foreach (var subValue in subtypes)
                            cardSubtypes.Add((Convert.ToInt32(subValue), subValue.ToString()));
                    }

                    foreach (var (order, enumValue) in cardTypes)
                    {
                        if (!stats.TypeGroups.TryGetValue(order, out var group))
                        {
                            stats.TypeGroups[order] = group = new TypeGroupStat
                            {
                                Order = order,
                                Name = CardTextProvider.GetLocalizedTextForEnumValue("CardType", enumValue)
                                       ?? DisplayTypeOrder[order],
                            };
                        }
                        group.Quantity += qty;

                        if (cardSubtypes == null) continue;
                        foreach (var (subEnum, subName) in cardSubtypes)
                        {
                            if (!group.Subtypes.TryGetValue(subEnum, out var sub))
                            {
                                group.Subtypes[subEnum] = sub = new SubtypeStat
                                {
                                    Name = CardTextProvider.GetLocalizedTextForEnumValue("SubType", subEnum)
                                           ?? subName,
                                };
                            }
                            sub.Quantity += qty;
                        }
                    }

                    // Basic lands
                    if (isLand && (bool)h.IsBasicLand.GetValue(printing))
                        stats.BasicLands += qty;

                    // Mana curve (lands excluded — but a creature land still counts, like the game)
                    uint cmc = Convert.ToUInt32(h.ConvertedManaCost.GetValue(printing));
                    if (isCreature || !isLand)
                        stats.CurveBuckets[Math.Min(Math.Max((int)cmc, 1), 6) - 1] += qty;
                    if (!isLand)
                    {
                        stats.AvgCards += qty;
                        stats.AvgManaSum += qty * cmc;
                    }

                    // Colors (non-land only)
                    if (!isLand)
                    {
                        stats.NonLandTotal += qty;
                        int flags = Convert.ToInt32(h.ColorFlags.GetValue(printing));
                        if (flags == 0)
                        {
                            stats.ColorCounts[6] += qty;    // colorless
                        }
                        else
                        {
                            if ((flags & (flags - 1)) != 0)
                                stats.ColorCounts[5] += qty; // multicolor
                            for (int i = 0; i < ColorBitNames.Length; i++)
                            {
                                int bit = h.ColorBits[i];
                                if (bit != 0 && (flags & bit) == bit)
                                    stats.ColorCounts[i] += qty;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("DeckInfoProvider", $"Error computing deck stats: {ex.Message}");
                return null;
            }

            return stats;
        }

        #endregion

        /// <summary>
        /// Clear cached component references. Call on scene change.
        /// Reflection member caches are preserved (types don't change).
        /// </summary>
        public static void ClearCache()
        {
            _cachedTitlePanel = null;
        }

        #region Component Discovery

        private static MonoBehaviour FindTitlePanel()
        {
            if (IsValidCached(_cachedTitlePanel))
                return _cachedTitlePanel;
            _cachedTitlePanel = null;

            var titlePanelGo = GameObject.Find("TitlePanel_MainDeck");
            if (titlePanelGo == null) return null;

            foreach (var mb in titlePanelGo.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (mb.GetType().Name == T.DeckMainTitlePanel)
                {
                    _cachedTitlePanel = mb;
                    _titlePanelCache.EnsureInitialized(mb.GetType());
                    return _cachedTitlePanel;
                }
            }

            return null;
        }

        private static bool IsValidCached(MonoBehaviour cached)
        {
            if (cached == null) return false;
            try
            {
                return cached.gameObject != null && cached.gameObject.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Reflection Initialization

        /// <summary>
        /// Initialize the Pantry reflection cache by seeding it with the Pantry type
        /// (discovered at runtime via FindType). Idempotent — bails out if already
        /// initialized or if the Pantry type cannot be located.
        /// </summary>
        private static void EnsurePantryInitialized()
        {
            if (_pantryCache.IsInitialized) return;
            Type pantryType = FindType("Wizards.Mtga.Pantry");
            if (pantryType == null)
            {
                Log.Warn("DeckInfoProvider", "Could not find Pantry type");
                return;
            }
            _pantryCache.EnsureInitialized(pantryType);
        }

        #endregion

        #region TMP_Text Helpers

        /// <summary>
        /// Find TMP_Text (TextMeshProUGUI) component on a GameObject.
        /// </summary>
        private static object FindTmpTextOnObject(GameObject go)
        {
            if (go == null) return null;

            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                string typeName = comp.GetType().Name;
                if (typeName == "TextMeshProUGUI" || typeName == "TMP_Text" || typeName == "TextMeshPro")
                {
                    return comp;
                }
            }

            // Also check children
            foreach (var comp in go.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                string typeName = comp.GetType().Name;
                if (typeName == "TextMeshProUGUI" || typeName == "TMP_Text" || typeName == "TextMeshPro")
                {
                    return comp;
                }
            }

            return null;
        }

        /// <summary>
        /// Get .text property from a TMP_Text-like object via reflection.
        /// </summary>
        private static string GetTmpTextValue(object tmpTextComponent)
        {
            if (tmpTextComponent == null) return null;

            try
            {
                var textProp = tmpTextComponent.GetType().GetProperty("text", PublicInstance);
                return textProp?.GetValue(tmpTextComponent) as string;
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
