using UnityEngine;
using System;
using System.Reflection;
using AccessibleArena.Core.Utils;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Live deck legality in the deck builder, via the game's own validator.
    ///
    /// Sighted players get a color-coded card count (yellow/red on an illegal size) and
    /// modal dialogs from the Done-button save chain; both channels are invisible to a
    /// screen reader until it is too late. This provider runs the same validation the
    /// save chain runs — DeckValidationHelper.CalculateIsDeckLegal, the ownership-free
    /// variant DeckManagerController uses for the deck-tile invalid markers — against the
    /// live working deck (DeckBuilderWidget.GetDeck() = Model.GetServerModel()), and
    /// returns the game's own localized invalid reasons
    /// (ClientSideDeckValidationResultUtils.GetInvalidReasons: below minimum size, banned
    /// cards, too many copies, cards not legal in format, commander problems, restricted
    /// quota, ...).
    ///
    /// Also exposes per-card checks (banned / not format-legal / restricted / outside the
    /// commander's color identity) so deck-list tiles can say WHY a card does not fit
    /// instead of a bare "illegal" tag. The pool only ever shows addable cards — illegal
    /// cards enter a deck through imports, rotation, or a format switch, which is exactly
    /// when these labels matter.
    ///
    /// Everything is read from a WrapperDeckBuilder instance (present only while the
    /// deck builder is open), so all entry points degrade to null elsewhere.
    /// </summary>
    public static class DeckLegalityProvider
    {
        public sealed class DeckStatus
        {
            public bool IsValid;
            public string FormatName;   // localized format name, e.g. "Standard"
            public string Reasons;      // localized sentences, single line; null when valid
        }

        internal sealed class Handles
        {
            // WrapperDeckBuilder members
            public FieldInfo Deckbuilder;        // _deckbuilder (DeckBuilderWidget)
            public FieldInfo CardDatabase;       // _cardDatabase
            public FieldInfo Bans;               // _emergencyCardBansProvider
            public FieldInfo SetMeta;            // _setMetadataProvider
            public FieldInfo Cosmetics;          // _cosmeticsProvider
            public FieldInfo Designer;           // _designerMetadataProvider
            public FieldInfo Loc;                // _localizationManager (IClientLocProvider)
            public MethodInfo GetContextFormat;  // () -> DeckFormat

            // DeckBuilderWidget
            public MethodInfo GetDeck;           // () -> DeckInfo (Model.GetServerModel())

            // Static validator + reasons extension
            public MethodInfo Calculate;         // DeckValidationHelper.CalculateIsDeckLegal(7 args)
            public MethodInfo GetInvalidReasons; // ClientSideDeckValidationResultUtils.GetInvalidReasons(result, loc)
            public PropertyInfo ResultIsValid;   // ClientSideDeckValidationResult.IsValid
            public MethodInfo DeckToConvertible; // ConvertibleToDeck.op_Implicit(DeckInfo) — see builder

            // DeckFormat members
            public MethodInfo FormatGetLocalizedName;
            public MethodInfo FormatIsCardLegal;         // (uint titleId) -> bool
            public MethodInfo FormatIsCardBanned;        // (uint titleId) -> bool
            public MethodInfo FormatIsCardRestricted;    // (uint titleId) -> bool
            public MethodInfo FormatGetRestrictedQuotaMax; // (uint titleId) -> int

            // IEmergencyCardBansProvider
            public MethodInfo IsEmergencyBanned; // (uint titleId) -> bool
        }

        private static readonly ReflectionCache<Handles> _cache = new ReflectionCache<Handles>(
            builder: wrapperType =>
            {
                var h = new Handles
                {
                    Deckbuilder = wrapperType.GetField("_deckbuilder", PrivateInstance),
                    CardDatabase = wrapperType.GetField("_cardDatabase", PrivateInstance),
                    Bans = wrapperType.GetField("_emergencyCardBansProvider", PrivateInstance),
                    SetMeta = wrapperType.GetField("_setMetadataProvider", PrivateInstance),
                    Cosmetics = wrapperType.GetField("_cosmeticsProvider", PrivateInstance),
                    Designer = wrapperType.GetField("_designerMetadataProvider", PrivateInstance),
                    Loc = wrapperType.GetField("_localizationManager", PrivateInstance),
                    GetContextFormat = wrapperType.GetMethod("GetContextFormat", PublicInstance),
                };

                h.GetDeck = h.Deckbuilder?.FieldType.GetMethod("GetDeck", PublicInstance);

                Type helperType = FindType("DeckValidationHelper");
                if (helperType != null)
                {
                    foreach (var m in helperType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        // The 7-arg ownership-free overload (AllowUncollectedCards: true)
                        if (m.Name == "CalculateIsDeckLegal" && m.GetParameters().Length == 7)
                        {
                            h.Calculate = m;
                            break;
                        }
                    }
                }

                if (h.Calculate != null)
                    h.ResultIsValid = h.Calculate.ReturnType.GetProperty("IsValid", PublicInstance);

                // GetDeck() returns DeckInfo, but the validator's deck parameter is
                // ConvertibleToDeck. The game's own call sites compile through the
                // implicit conversion operator — reflection Invoke does NOT apply
                // user-defined conversions, so we must call op_Implicit ourselves
                // (without this every validation throws "cannot be converted")
                if (h.Calculate != null && h.GetDeck != null)
                {
                    Type convertibleType = h.Calculate.GetParameters()[1].ParameterType;
                    if (!convertibleType.IsAssignableFrom(h.GetDeck.ReturnType))
                    {
                        foreach (var m in convertibleType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                        {
                            if (m.Name != "op_Implicit") continue;
                            var ps = m.GetParameters();
                            if (ps.Length == 1
                                && ps[0].ParameterType.IsAssignableFrom(h.GetDeck.ReturnType)
                                && convertibleType.IsAssignableFrom(m.ReturnType))
                            {
                                h.DeckToConvertible = m;
                                break;
                            }
                        }
                        if (h.DeckToConvertible == null)
                            Log.Warn("DeckLegalityProvider",
                                $"No implicit conversion from {h.GetDeck.ReturnType.Name} to {convertibleType.Name} — validation will fail");
                    }
                }

                Type utilsType = FindType("ClientSideDeckValidationResultUtils");
                if (utilsType != null)
                {
                    foreach (var m in utilsType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        if (m.Name == "GetInvalidReasons" && m.GetParameters().Length == 2)
                        {
                            h.GetInvalidReasons = m;
                            break;
                        }
                    }
                }

                var formatType = h.GetContextFormat?.ReturnType;
                if (formatType != null)
                {
                    h.FormatGetLocalizedName = formatType.GetMethod("GetLocalizedName", PublicInstance);
                    h.FormatIsCardLegal = formatType.GetMethod("IsCardLegal", PublicInstance);
                    h.FormatIsCardBanned = formatType.GetMethod("IsCardBanned", PublicInstance);
                    h.FormatIsCardRestricted = formatType.GetMethod("IsCardRestricted", PublicInstance);
                    h.FormatGetRestrictedQuotaMax = formatType.GetMethod("GetRestrictedQuotaMax", PublicInstance);
                }

                h.IsEmergencyBanned = h.Bans?.FieldType.GetMethod("IsTitleIdEmergencyBanned", PublicInstance);

                return h;
            },
            validator: h =>
                h.Deckbuilder != null && h.CardDatabase != null && h.Bans != null
                && h.SetMeta != null && h.Cosmetics != null && h.Designer != null
                && h.Loc != null && h.GetContextFormat != null && h.GetDeck != null
                && h.Calculate != null && h.GetInvalidReasons != null && h.ResultIsValid != null
                && h.FormatGetLocalizedName != null && h.FormatIsCardLegal != null
                && h.FormatIsCardBanned != null && h.FormatIsCardRestricted != null
                && h.FormatGetRestrictedQuotaMax != null && h.IsEmergencyBanned != null,
            logTag: "DeckLegalityProvider",
            logSubject: "WrapperDeckBuilder validation");

        private static MonoBehaviour _cachedWrapper;

        /// <summary>Clear cached component references. Call on scene change.</summary>
        public static void ClearCache()
        {
            _cachedWrapper = null;
        }

        /// <summary>
        /// Validate the live working deck. Null when not in the deck builder or when
        /// reflection is unavailable — callers skip the feature then.
        /// </summary>
        public static DeckStatus GetDeckStatus()
        {
            var wrapper = FindWrapper();
            if (wrapper == null || !_cache.IsInitialized) return null;
            var h = _cache.Handles;

            try
            {
                var format = h.GetContextFormat.Invoke(wrapper, null);
                if (format == null) return null;

                var widget = h.Deckbuilder.GetValue(wrapper);
                if (widget == null) return null;

                var deck = h.GetDeck.Invoke(widget, null);
                if (deck == null) return null;

                if (h.DeckToConvertible != null)
                    deck = h.DeckToConvertible.Invoke(null, new object[] { deck });

                var result = h.Calculate.Invoke(null, new object[]
                {
                    format, deck,
                    h.CardDatabase.GetValue(wrapper),
                    h.Bans.GetValue(wrapper),
                    h.SetMeta.GetValue(wrapper),
                    h.Cosmetics.GetValue(wrapper),
                    h.Designer.GetValue(wrapper),
                });
                if (result == null) return null;

                var status = new DeckStatus
                {
                    IsValid = (bool)h.ResultIsValid.GetValue(result),
                    FormatName = h.FormatGetLocalizedName.Invoke(format, null) as string,
                };

                if (!status.IsValid)
                {
                    string reasons = h.GetInvalidReasons.Invoke(null,
                        new object[] { result, h.Loc.GetValue(wrapper) }) as string;
                    status.Reasons = CleanReasons(reasons);
                }

                return status;
            }
            catch (Exception ex)
            {
                Log.Error("DeckLegalityProvider", $"Error validating deck: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// The game's reasons string is newline-separated sentences; flatten to one line
        /// for a single announcement.
        /// </summary>
        private static string CleanReasons(string reasons)
        {
            if (string.IsNullOrEmpty(reasons)) return null;
            string flat = reasons.Replace("\r\n", " ").Replace("\n", " ").Replace("  ", " ").Trim();
            return flat.Length > 0 ? flat : null;
        }

        #region Per-card checks

        /// <summary>
        /// Per-card legality checker for one deck-list scan. Captures the current format,
        /// the commander color identity, and the printing lookup once; GetReason is then a
        /// cheap dictionary check per tile. Null when unavailable (not in the builder).
        /// </summary>
        public sealed class CardChecker
        {
            internal Handles H;
            internal object Format;
            internal object BansInstance;
            internal object CardDataProvider;
            internal MethodInfo GetPrinting;       // GetCardPrintingById(uint[, string])
            internal PropertyInfo PrintingTitleId;
            internal PropertyInfo PrintingColorIdentity; // ColorIdentityFlags
            internal PropertyInfo PrintingIsBasicLand;
            internal int CommanderColors;
            internal bool CheckCommanderColors;

            /// <summary>
            /// Localized reason why this card does not fit the current format, or null
            /// when the card is fine. Checked in severity order: banned (incl. emergency
            /// bans), not legal in format, outside commander color identity; a card on
            /// the restricted list additionally reports its copy cap.
            /// </summary>
            public string GetReason(uint grpId)
            {
                try
                {
                    var printing = InvokeGetPrinting(grpId);
                    if (printing == null) return null;

                    var titleIdVal = PrintingTitleId.GetValue(printing);
                    if (!(titleIdVal is uint titleId) || titleId == 0) return null;

                    if ((bool)H.FormatIsCardBanned.Invoke(Format, new object[] { titleId })
                        || (bool)H.IsEmergencyBanned.Invoke(BansInstance, new object[] { titleId }))
                        return Models.Strings.CardLegalityBanned;

                    if (!(bool)H.FormatIsCardLegal.Invoke(Format, new object[] { titleId }))
                        return Models.Strings.CardLegalityNotInFormat;

                    if (CheckCommanderColors && PrintingColorIdentity != null)
                    {
                        int identity = Convert.ToInt32(PrintingColorIdentity.GetValue(printing));
                        // Mirrors DeckFormat.CardMatchesCommanderColors: a basic land is
                        // always allowed under a colorless commander
                        bool basicLandException = CommanderColors == 0
                            && PrintingIsBasicLand != null
                            && (bool)PrintingIsBasicLand.GetValue(printing);
                        if (!basicLandException && (identity & ~CommanderColors) != 0)
                            return Models.Strings.CardLegalityCommanderColor;
                    }

                    if ((bool)H.FormatIsCardRestricted.Invoke(Format, new object[] { titleId }))
                    {
                        int max = (int)H.FormatGetRestrictedQuotaMax.Invoke(Format, new object[] { titleId });
                        if (max >= 0)
                            return Models.Strings.CardLegalityRestricted(max);
                    }

                    return null;
                }
                catch
                {
                    return null;
                }
            }

            internal object InvokeGetPrinting(uint grpId)
            {
                var ps = GetPrinting.GetParameters();
                return ps.Length == 2
                    ? GetPrinting.Invoke(CardDataProvider, new object[] { grpId, null })
                    : GetPrinting.Invoke(CardDataProvider, new object[] { grpId });
            }
        }

        // One-shot log flags so a silently unavailable checker is greppable without
        // repeating the line on every deck-list rescan
        private static bool _checkerReadyLogged;
        private static bool _checkerFailureLogged;

        /// <summary>
        /// Build a checker for the current deck-builder session, or null when the builder
        /// is not open / reflection is unavailable.
        /// </summary>
        public static CardChecker CreateCardChecker()
        {
            var wrapper = FindWrapper();
            if (wrapper == null || !_cache.IsInitialized) return null;
            var h = _cache.Handles;

            try
            {
                var format = h.GetContextFormat.Invoke(wrapper, null);
                if (format == null) return null;

                var cardDb = h.CardDatabase.GetValue(wrapper);
                var providerProp = cardDb?.GetType().GetProperty("CardDataProvider", PublicInstance);
                var provider = providerProp?.GetValue(cardDb);
                if (provider == null)
                {
                    WarnCheckerUnavailable("CardDataProvider not found on CardDatabase");
                    return null;
                }

                MethodInfo getPrinting = null;
                foreach (var m in provider.GetType().GetMethods(PublicInstance))
                {
                    if (m.Name != "GetCardPrintingById") continue;
                    var ps = m.GetParameters();
                    if (ps.Length >= 1 && ps.Length <= 2 && ps[0].ParameterType == typeof(uint))
                    {
                        getPrinting = m;
                        break;
                    }
                }
                if (getPrinting == null)
                {
                    WarnCheckerUnavailable("GetCardPrintingById not found");
                    return null;
                }

                var printingType = getPrinting.ReturnType;
                var checker = new CardChecker
                {
                    H = h,
                    Format = format,
                    BansInstance = h.Bans.GetValue(wrapper),
                    CardDataProvider = provider,
                    GetPrinting = getPrinting,
                    PrintingTitleId = printingType.GetProperty("TitleId", PublicInstance),
                    PrintingColorIdentity = printingType.GetProperty("ColorIdentityFlags", PublicInstance),
                    PrintingIsBasicLand = printingType.GetProperty("IsBasicLand", PublicInstance),
                };
                if (checker.PrintingTitleId == null || checker.BansInstance == null)
                {
                    WarnCheckerUnavailable(checker.PrintingTitleId == null
                        ? "TitleId property not found on CardPrintingData"
                        : "emergency-bans provider instance is null");
                    return null;
                }

                // Commander color identity: union over the command zone (companions are
                // not commanders). Only meaningful when a commander is actually set.
                int commanderColors = 0;
                bool hasCommander = false;
                foreach (var cmd in DeckCardProvider.GetCommanderCards())
                {
                    if (!cmd.IsValid || cmd.IsCompanion || cmd.GrpId == 0) continue;
                    var printing = checker.InvokeGetPrinting(cmd.GrpId);
                    if (printing == null || checker.PrintingColorIdentity == null) continue;
                    hasCommander = true;
                    commanderColors |= Convert.ToInt32(checker.PrintingColorIdentity.GetValue(printing));
                }
                checker.CommanderColors = commanderColors;
                checker.CheckCommanderColors = hasCommander;

                if (!_checkerReadyLogged)
                {
                    _checkerReadyLogged = true;
                    Log.Msg("DeckLegalityProvider",
                        $"Card legality checker ready (commander identity check: {hasCommander})");
                }
                return checker;
            }
            catch (Exception ex)
            {
                Log.Error("DeckLegalityProvider", $"Error building card checker: {ex.Message}");
                return null;
            }
        }

        private static void WarnCheckerUnavailable(string reason)
        {
            if (_checkerFailureLogged) return;
            _checkerFailureLogged = true;
            Log.Warn("DeckLegalityProvider", $"Card legality checker unavailable: {reason}");
        }

        #endregion

        private static MonoBehaviour FindWrapper()
        {
            if (_cachedWrapper != null)
            {
                try
                {
                    if (_cachedWrapper.gameObject != null)
                        return _cachedWrapper;
                }
                catch { /* destroyed */ }
                _cachedWrapper = null;
            }

            Type wrapperType = FindType("WrapperDeckBuilder");
            if (wrapperType == null) return null;

            var instances = GameObject.FindObjectsOfType(wrapperType);
            if (instances == null || instances.Length == 0) return null;

            _cachedWrapper = instances[0] as MonoBehaviour;
            if (_cachedWrapper != null)
                _cache.EnsureInitialized(wrapperType);
            return _cachedWrapper;
        }
    }
}
