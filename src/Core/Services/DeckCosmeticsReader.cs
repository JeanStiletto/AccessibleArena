using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AccessibleArena.Core.Utils;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Read-only access to the current deck's cosmetic state and to per-card art-style names.
    /// Resolves IDs to localized display names by reusing the game's catalogs and
    /// localization keys (same patterns ProfileNavigator uses).
    ///
    /// Backed by reflection caches keyed on Pantry, DeckBuilderModel, and the various
    /// catalog dictionaries. All values come from live game state — no caching of
    /// resolved names beyond the reflection handles themselves, since deck cosmetics
    /// can change at any time via the popup.
    /// </summary>
    public static class DeckCosmeticsReader
    {
        #region Reflection Handles

        private sealed class PantryHandles
        {
            public MethodInfo GetModelProvider;     // Pantry.Get<DeckBuilderModelProvider>()
            public PropertyInfo Model;              // DeckBuilderModelProvider.Model
            public MethodInfo GetCardSkin;          // DeckBuilderModel.GetCardSkin(uint) -> string
            public FieldInfo Avatar;                // DeckBuilderModel._avatar
            public FieldInfo CardBack;              // DeckBuilderModel._cardBack
            public FieldInfo Pet;                   // DeckBuilderModel._pet
            public FieldInfo Emotes;                // DeckBuilderModel._emotes (List<string>)
            public MethodInfo GetCosmetics;         // Pantry.Get<CosmeticsProvider>()
            public MethodInfo GetStoreManager;      // Pantry.Get<StoreManager>()
            public MethodInfo GetCardDatabase;      // Pantry.Get<CardDatabase>()
        }

        private sealed class StoreHandles
        {
            // StoreManager.CardSkinCatalog (CardSkinCatalog : Dictionary<string, ArtStyleEntry>)
            public PropertyInfo CardSkinCatalogProp;
            public FieldInfo CardSkinCatalogField;
            // StoreManager.AvatarCatalog (AvatarCatalog : Dictionary<string, AvatarEntry>)
            public PropertyInfo AvatarCatalogProp;
            public FieldInfo AvatarCatalogField;
            // StoreManager.PetCatalog (PetCatalog : Dictionary<string, PetEntry>)
            public PropertyInfo PetCatalogProp;
            public FieldInfo PetCatalogField;
            // StoreManager.CardbackCatalog (whatever sleeve-data store has)
            public PropertyInfo CardbackCatalogProp;
            public FieldInfo CardbackCatalogField;
        }

        private sealed class CardDatabaseHandles
        {
            public PropertyInfo CardDataProvider;   // CardDatabase.CardDataProvider
            public MethodInfo GetCardPrintingById;  // ICardDataProvider.GetCardPrintingById(uint)
            public PropertyInfo ArtId;              // CardPrintingData.ArtId
            public PropertyInfo ExpansionCode;      // CardPrintingData.ExpansionCode
            public PropertyInfo CollectorNumber;    // CardPrintingData.CollectorNumber
        }

        private sealed class ArtStyleEntryHandles
        {
            public FieldInfo Variant;               // ArtStyleEntry.Variant
            public FieldInfo Id;                    // CatalogEntry.Id
        }

        private static readonly ReflectionCache<PantryHandles> _pantryCache = new ReflectionCache<PantryHandles>(
            builder: pantryType =>
            {
                var h = new PantryHandles();

                Type modelProviderType = FindType("Core.Code.Decks.DeckBuilderModelProvider");
                if (modelProviderType != null)
                {
                    var get = pantryType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                    if (get != null && get.IsGenericMethod)
                        h.GetModelProvider = get.MakeGenericMethod(modelProviderType);
                    h.Model = modelProviderType.GetProperty("Model", PublicInstance)
                           ?? modelProviderType.GetProperty("Model", PrivateInstance);
                    if (h.Model != null)
                    {
                        var modelType = h.Model.PropertyType;
                        h.GetCardSkin = modelType.GetMethod("GetCardSkin", PublicInstance, null, new[] { typeof(uint) }, null);
                        h.Avatar = modelType.GetField("_avatar", AllInstanceFlags);
                        h.CardBack = modelType.GetField("_cardBack", AllInstanceFlags);
                        h.Pet = modelType.GetField("_pet", AllInstanceFlags);
                        h.Emotes = modelType.GetField("_emotes", AllInstanceFlags);
                    }
                }

                Type cosmeticsType = FindType("Wotc.Mtga.Providers.CosmeticsProvider");
                if (cosmeticsType != null)
                {
                    var get = pantryType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                    if (get != null && get.IsGenericMethod)
                        h.GetCosmetics = get.MakeGenericMethod(cosmeticsType);
                }

                Type storeManagerType = FindType("Wizards.Mtga.StoreManager")
                    ?? FindType("StoreManager");
                if (storeManagerType != null)
                {
                    var get = pantryType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                    if (get != null && get.IsGenericMethod)
                        h.GetStoreManager = get.MakeGenericMethod(storeManagerType);
                }

                Type cardDatabaseType = FindType("Wotc.Mtga.Cards.Database.CardDatabase");
                if (cardDatabaseType != null)
                {
                    var get = pantryType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                    if (get != null && get.IsGenericMethod)
                        h.GetCardDatabase = get.MakeGenericMethod(cardDatabaseType);
                }

                return h;
            },
            validator: h => h.GetModelProvider != null && h.Model != null,
            logTag: "DeckCosmeticsReader",
            logSubject: "Pantry");

        private static readonly ReflectionCache<StoreHandles> _storeCache = new ReflectionCache<StoreHandles>(
            builder: storeManagerType =>
            {
                var h = new StoreHandles
                {
                    CardSkinCatalogProp = storeManagerType.GetProperty("CardSkinCatalog", PublicInstance),
                    CardSkinCatalogField = storeManagerType.GetField("CardSkinCatalog", AllInstanceFlags),
                    AvatarCatalogProp = storeManagerType.GetProperty("AvatarCatalog", PublicInstance),
                    AvatarCatalogField = storeManagerType.GetField("AvatarCatalog", AllInstanceFlags),
                    PetCatalogProp = storeManagerType.GetProperty("PetCatalog", PublicInstance),
                    PetCatalogField = storeManagerType.GetField("PetCatalog", AllInstanceFlags),
                    CardbackCatalogProp = storeManagerType.GetProperty("CardbackCatalog", PublicInstance),
                    CardbackCatalogField = storeManagerType.GetField("CardbackCatalog", AllInstanceFlags),
                };
                return h;
            },
            validator: _ => true,
            logTag: "DeckCosmeticsReader",
            logSubject: "StoreManager");

        private static readonly ReflectionCache<CardDatabaseHandles> _cardDatabaseCache = new ReflectionCache<CardDatabaseHandles>(
            builder: cardDatabaseType =>
            {
                var h = new CardDatabaseHandles
                {
                    CardDataProvider = cardDatabaseType.GetProperty("CardDataProvider", PublicInstance),
                };
                if (h.CardDataProvider != null)
                {
                    var providerType = h.CardDataProvider.PropertyType;
                    // Concrete impl is GetCardPrintingById(uint id, string skinCode = null) — two params,
                    // even though most callers omit the second. Match that exact signature.
                    h.GetCardPrintingById = providerType.GetMethod("GetCardPrintingById", PublicInstance, null, new[] { typeof(uint), typeof(string) }, null)
                                         ?? providerType.GetMethod("GetCardPrintingById", PublicInstance, null, new[] { typeof(uint) }, null);
                }
                Type printingType = FindType("GreClient.CardData.CardPrintingData");
                if (printingType != null)
                {
                    h.ArtId = printingType.GetProperty("ArtId", PublicInstance);
                    h.ExpansionCode = printingType.GetProperty("ExpansionCode", PublicInstance);
                    h.CollectorNumber = printingType.GetProperty("CollectorNumber", PublicInstance);
                }
                return h;
            },
            validator: _ => true,
            logTag: "DeckCosmeticsReader",
            logSubject: "CardDatabase");

        private static readonly ReflectionCache<ArtStyleEntryHandles> _artStyleEntryCache = new ReflectionCache<ArtStyleEntryHandles>(
            builder: artStyleType => new ArtStyleEntryHandles
            {
                Variant = artStyleType.GetField("Variant", AllInstanceFlags),
                Id = artStyleType.GetField("Id", AllInstanceFlags),
            },
            validator: _ => true,
            logTag: "DeckCosmeticsReader",
            logSubject: "ArtStyleEntry");

        #endregion

        #region Public API

        /// <summary>
        /// Returns the current deck's avatar id (e.g. "Liliana_VAW") or null/empty if default.
        /// </summary>
        public static string GetCurrentAvatarId() => ReadStringField(GetModel(), GetPantryHandles()?.Avatar);

        /// <summary>
        /// Returns the current deck's sleeve id (e.g. "CardBack_DMU_StainedGlass") or null/empty if default.
        /// </summary>
        public static string GetCurrentSleeveId() => ReadStringField(GetModel(), GetPantryHandles()?.CardBack);

        /// <summary>
        /// Returns the current deck's pet id ("name.variant" format) or null/empty if default.
        /// </summary>
        public static string GetCurrentPetId() => ReadStringField(GetModel(), GetPantryHandles()?.Pet);

        /// <summary>
        /// Returns the current deck's emote id list (size 4 = full set, empty = default).
        /// </summary>
        public static List<string> GetCurrentEmoteIds()
        {
            var model = GetModel();
            var field = GetPantryHandles()?.Emotes;
            if (model == null || field == null) return new List<string>();
            try
            {
                return (field.GetValue(model) as List<string>) ?? new List<string>();
            }
            catch { return new List<string>(); }
        }

        /// <summary>
        /// Localized display name for the current avatar, or "Default" placeholder.
        /// Resolution chain: AvatarCatalog → loc-key fallback → humanized id.
        /// </summary>
        public static string GetCurrentAvatarName()
        {
            string id = GetCurrentAvatarId();
            if (string.IsNullOrEmpty(id)) return Models.Strings.ProfileItemDefault;
            return ResolveAvatarName(id) ?? HumanizeId(id);
        }

        /// <summary>
        /// Localized display name for the current sleeve, or "Default" placeholder.
        /// </summary>
        public static string GetCurrentSleeveName()
        {
            string id = GetCurrentSleeveId();
            if (string.IsNullOrEmpty(id)) return Models.Strings.ProfileItemDefault;
            return ResolveSleeveName(id) ?? HumanizeId(id);
        }

        /// <summary>
        /// Localized display name for the current pet, or "Default" placeholder.
        /// Pet IDs are stored as "name.variant"; we resolve from the pet catalog by name.
        /// </summary>
        public static string GetCurrentPetName()
        {
            string id = GetCurrentPetId();
            if (string.IsNullOrEmpty(id)) return Models.Strings.ProfileItemDefault;
            string petName = id;
            int dot = id.IndexOf('.');
            if (dot > 0) petName = id.Substring(0, dot);
            return ResolvePetName(petName) ?? HumanizeId(petName);
        }

        /// <summary>
        /// Short summary of selected emotes (e.g. "4 emotes selected" or "Default").
        /// </summary>
        public static string GetCurrentEmoteSummary()
        {
            var emotes = GetCurrentEmoteIds();
            if (emotes == null || emotes.Count == 0) return Models.Strings.ProfileItemDefault;
            return $"{emotes.Count} emotes selected";
        }

        /// <summary>
        /// Resolves a raw skin/variant code (e.g. "ShowcaseEtched") to a localized display name,
        /// or returns the "Default art" placeholder when the code is null/empty.
        /// Use this when the per-tile skin code is already known (preferred path for deck-list
        /// rows, where two tiles can share a grpId but differ by skin).
        /// </summary>
        public static string GetStyleNameFromSkinCode(string skinCode)
        {
            if (string.IsNullOrEmpty(skinCode)) return Models.Strings.CosmeticsDefaultArt;
            return HumanizeId(skinCode);
        }

        /// <summary>
        /// Resolves the displayed style for a deck-builder tile. When the tile has a skin override
        /// applied (user-picked variant), returns the humanized skin code. Otherwise falls back to
        /// the printing's set + collector number (e.g. "Dominaria 26") so that two rows of the same
        /// card from different printings (different GrpIds) can be told apart by ear.
        /// </summary>
        public static string GetStyleNameForTile(uint grpId, string skinCode)
        {
            if (!string.IsNullOrEmpty(skinCode))
                return HumanizeId(skinCode);

            string printingLabel = GetPrintingLabel(grpId);
            return printingLabel ?? Models.Strings.CosmeticsDefaultArt;
        }

        private static string GetPrintingLabel(uint grpId)
        {
            if (grpId == 0) return null;
            var printing = GetCardPrinting(grpId);
            if (printing == null) return null;

            var ch = _cardDatabaseCache.IsInitialized ? _cardDatabaseCache.Handles : null;
            if (ch == null) return null;

            string setCode = null;
            string collector = null;
            try { setCode = ch.ExpansionCode?.GetValue(printing) as string; } catch { }
            try { collector = ch.CollectorNumber?.GetValue(printing) as string; } catch { }

            if (string.IsNullOrEmpty(setCode) && string.IsNullOrEmpty(collector)) return null;

            string setName = string.IsNullOrEmpty(setCode) ? null : UITextExtractor.MapSetCodeToName(setCode);
            if (string.IsNullOrEmpty(setName)) setName = setCode;

            if (string.IsNullOrEmpty(collector)) return setName;
            if (string.IsNullOrEmpty(setName)) return collector;
            return $"{setName} {collector}";
        }

        private static object GetCardPrinting(uint grpId)
        {
            var ph = GetPantryHandles();
            if (ph?.GetCardDatabase == null) return null;
            try
            {
                var cardDatabase = ph.GetCardDatabase.Invoke(null, null);
                if (cardDatabase == null) return null;
                _cardDatabaseCache.EnsureInitialized(cardDatabase.GetType());
                var ch = _cardDatabaseCache.IsInitialized ? _cardDatabaseCache.Handles : null;
                if (ch?.CardDataProvider == null || ch.GetCardPrintingById == null) return null;
                var provider = ch.CardDataProvider.GetValue(cardDatabase);
                if (provider == null) return null;
                int paramCount = ch.GetCardPrintingById.GetParameters().Length;
                object[] args = paramCount == 2 ? new object[] { grpId, null } : new object[] { grpId };
                return ch.GetCardPrintingById.Invoke(provider, args);
            }
            catch { return null; }
        }

        /// <summary>
        /// Localized name for the art style applied to the given grpId on the current deck.
        /// Falls back to the model's deck-level skin override dictionary; this is keyed by
        /// grpId only and cannot represent two rows with the same grpId but different skins,
        /// so callers with access to a tile should prefer <see cref="GetStyleNameFromSkinCode"/>.
        /// </summary>
        public static string GetCardStyleName(uint grpId)
        {
            var model = GetModel();
            var ph = GetPantryHandles();
            if (model == null || ph?.GetCardSkin == null) return null;

            string skinCode;
            try
            {
                skinCode = ph.GetCardSkin.Invoke(model, new object[] { grpId }) as string;
            }
            catch { skinCode = null; }

            return GetStyleNameFromSkinCode(skinCode);
        }

        /// <summary>
        /// Read the art-style applied to a specific card-tile GameObject (PagesMetaCardView /
        /// ListMetaCardView_Expanding). Returns the localized style name when the tile has a
        /// non-default skin set on its CardData; returns null for default-art tiles so callers
        /// can suppress the announcement instead of saying "Default art" on every tile.
        /// </summary>
        public static string GetTileStyleName(UnityEngine.GameObject tileObj)
        {
            if (tileObj == null) return null;
            try
            {
                // Walk components looking for one with a 'Card' (CardData) accessor.
                foreach (var comp in tileObj.GetComponents<UnityEngine.MonoBehaviour>())
                {
                    if (comp == null) continue;
                    var cardProp = comp.GetType().GetProperty("Card", PublicInstance);
                    if (cardProp == null) continue;
                    var cardData = cardProp.GetValue(comp);
                    if (cardData == null) continue;

                    var skinProp = cardData.GetType().GetProperty("SkinCode", PublicInstance);
                    string skinCode = skinProp?.GetValue(cardData) as string;
                    if (string.IsNullOrEmpty(skinCode)) return null;

                    return HumanizeId(skinCode);
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Returns true if the deck builder is currently in read-only or sideboarding state.
        /// Used to gate "apply" actions in the navigators.
        /// </summary>
        public static bool IsReadOnly()
        {
            try
            {
                Type pantryType = FindType("Wizards.Mtga.Pantry");
                if (pantryType == null) return false;
                Type contextProviderType = FindType("Core.Code.Decks.DeckBuilderContextProvider");
                if (contextProviderType == null) return false;
                var get = pantryType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                if (get == null || !get.IsGenericMethod) return false;
                var generic = get.MakeGenericMethod(contextProviderType);
                var provider = generic.Invoke(null, null);
                if (provider == null) return false;

                var contextProp = contextProviderType.GetProperty("Context", PublicInstance);
                var context = contextProp?.GetValue(provider);
                if (context == null) return false;

                var isReadOnly = context.GetType().GetProperty("IsReadOnly", PublicInstance);
                var isSideboarding = context.GetType().GetProperty("IsSideboarding", PublicInstance);
                bool ro = isReadOnly != null && (bool)isReadOnly.GetValue(context);
                bool sb = isSideboarding != null && (bool)isSideboarding.GetValue(context);
                return ro || sb;
            }
            catch { return false; }
        }

        /// <summary>
        /// Clears all cached references. Call on scene change.
        /// (Reflection handles persist; this only resets the runtime accessors.)
        /// </summary>
        public static void ClearCache()
        {
            // Reflection caches don't change between scenes, so nothing to reset here.
            // Method retained for parity with other providers (DeckInfoProvider.ClearCache).
        }

        #endregion

        #region Internal helpers

        private static PantryHandles GetPantryHandles()
        {
            if (!_pantryCache.IsInitialized)
            {
                Type pantryType = FindType("Wizards.Mtga.Pantry");
                if (pantryType == null) return null;
                _pantryCache.EnsureInitialized(pantryType);
            }
            return _pantryCache.IsInitialized ? _pantryCache.Handles : null;
        }

        private static object GetModel()
        {
            var ph = GetPantryHandles();
            if (ph == null || ph.GetModelProvider == null || ph.Model == null) return null;
            try
            {
                var provider = ph.GetModelProvider.Invoke(null, null);
                if (provider == null) return null;
                return ph.Model.GetValue(provider);
            }
            catch { return null; }
        }

        private static object GetStoreManager()
        {
            var ph = GetPantryHandles();
            if (ph == null || ph.GetStoreManager == null) return null;
            try { return ph.GetStoreManager.Invoke(null, null); }
            catch { return null; }
        }

        private static StoreHandles GetStoreHandles()
        {
            var sm = GetStoreManager();
            if (sm == null) return null;
            _storeCache.EnsureInitialized(sm.GetType());
            return _storeCache.IsInitialized ? _storeCache.Handles : null;
        }

        private static string ReadStringField(object owner, FieldInfo field)
        {
            if (owner == null || field == null) return null;
            try { return field.GetValue(owner) as string; }
            catch { return null; }
        }

        private static object ReadFromStore(StoreHandles h, PropertyInfo prop, FieldInfo field)
        {
            var sm = GetStoreManager();
            if (sm == null) return null;
            try
            {
                if (prop != null) return prop.GetValue(sm);
                if (field != null) return field.GetValue(sm);
            }
            catch { }
            return null;
        }

        private static string ResolveAvatarName(string avatarId)
        {
            // Try AvatarCatalog (Dictionary<string, AvatarEntry>).
            var sh = GetStoreHandles();
            if (sh != null)
            {
                var catalog = ReadFromStore(sh, sh.AvatarCatalogProp, sh.AvatarCatalogField) as IDictionary;
                if (catalog != null && catalog.Contains(avatarId))
                {
                    // No useful display field on AvatarEntry — fall through to loc key.
                }
            }
            // Loc key pattern used by ProfileNavigator
            return UITextExtractor.ResolveLocKey($"MainNav/Profile/Avatars/{avatarId}_Name");
        }

        private static string ResolveSleeveName(string sleeveId)
        {
            // Direct loc-key patterns first (same as ProfileNavigator's sleeve resolver).
            string[] keys =
            {
                $"MainNav/Cosmetics/CardBack/{sleeveId}",
                $"MainNav/Cosmetics/Sleeve/{sleeveId}",
            };
            foreach (var key in keys)
            {
                string resolved = UITextExtractor.ResolveLocKey(key);
                if (!string.IsNullOrEmpty(resolved)) return resolved;
            }

            // Fall back to set-code prefix style: CardBack_DMU_X -> "Duskmourn - X"
            string stripped = sleeveId.StartsWith("CardBack_") ? sleeveId.Substring(9) : sleeveId;
            int underscoreIdx = stripped.IndexOf('_');
            if (underscoreIdx > 0)
            {
                string setCode = stripped.Substring(0, underscoreIdx);
                string setName = UITextExtractor.MapSetCodeToName(setCode);
                if (!string.IsNullOrEmpty(setName) && setName != setCode)
                    return $"{setName} - {HumanizeId(stripped.Substring(underscoreIdx + 1))}";
            }
            return null;
        }

        private static string ResolvePetName(string petName)
        {
            string[] keys =
            {
                $"MainNav/Cosmetics/Pet/{petName}_Details",
                $"MainNav/Cosmetics/Pet/{petName}_Name",
                $"MainNav/Cosmetics/Pet/{petName}",
                $"MainNav/Cosmetics/Pets/{petName}_Details",
                $"MainNav/Cosmetics/Pets/{petName}",
            };
            foreach (var key in keys)
            {
                string resolved = UITextExtractor.ResolveLocKey(key);
                if (!string.IsNullOrEmpty(resolved)) return resolved;
            }
            return null;
        }

        /// <summary>
        /// Convert internal IDs like "ShowcaseEtched" or "CardBack_DMU_X" to "Showcase Etched"
        /// or "DMU X". Splits on underscore and inserts spaces before camel-case capitals.
        /// </summary>
        public static string HumanizeId(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;

            if (id.StartsWith("CardBack_"))
                id = id.Substring(9);

            id = id.Replace('_', ' ');

            var sb = new System.Text.StringBuilder(id.Length + 8);
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                if (i > 0 && char.IsUpper(c) && id[i - 1] != ' ' && !char.IsUpper(id[i - 1]))
                    sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        #endregion
    }
}
