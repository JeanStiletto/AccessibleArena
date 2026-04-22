using System;
using System.Reflection;
using UnityEngine;
using TMPro;
using AccessibleArena.Core.Utils;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    public static partial class UITextExtractor
    {
        // Cached reflection for Localize component text extraction. The Localize type itself
        // is needed at match sites (component type check), so it's tracked alongside the cache.
        private sealed class LocalizeHandles
        {
            public FieldInfo TextTarget;
            public FieldInfo SerializedCmp;
            public FieldInfo LocKey;
        }

        private static Type _localizeType;
        private static readonly ReflectionCache<LocalizeHandles> _localizeCache = new ReflectionCache<LocalizeHandles>(
            builder: t =>
            {
                var h = new LocalizeHandles { TextTarget = t.GetField("TextTarget", PublicInstance) };
                if (h.TextTarget != null)
                {
                    var targetType = h.TextTarget.FieldType;
                    h.SerializedCmp = targetType.GetField("serializedCmp", PublicInstance);
                    h.LocKey = targetType.GetField("locKey", PublicInstance);
                }
                return h;
            },
            validator: h => h.TextTarget != null,
            logTag: "UITextExtractor",
            logSubject: "Localize");

        /// <summary>
        /// Tries to read localized text from Localize components (Wotc.Mtga.Loc.Localize).
        /// Two strategies: (1) read TMP_Text.text from serializedCmp on inactive children that
        /// GetComponentInChildren(false) misses, (2) resolve the locKey directly via the game's
        /// loc provider when the TMP_Text is empty or null (e.g. icon-only buttons with a
        /// Localize component that hasn't fired DoLocalize yet).
        /// </summary>
        private static string TryGetLocalizeText(GameObject gameObject)
            => TryGetLocalizeTextExcluding(gameObject, null);

        /// <summary>
        /// Same as <see cref="TryGetLocalizeText"/> but skips any Localize component inside
        /// <paramref name="excludeSubtree"/>. Used to look up a sibling label while ignoring
        /// a wrapped widget's own Localize (e.g., an input field's placeholder).
        /// </summary>
        private static string TryGetLocalizeTextExcluding(GameObject gameObject, GameObject excludeSubtree)
        {
            if (gameObject == null) return null;

            if (_localizeType == null)
                _localizeType = FindType("Wotc.Mtga.Loc.Localize");
            if (_localizeType == null) return null;

            if (!_localizeCache.EnsureInitialized(_localizeType)) return null;
            var h = _localizeCache.Handles;

            Transform excludeRoot = excludeSubtree != null ? excludeSubtree.transform : null;

            // Search element and children (including inactive) for Localize components
            var behaviours = gameObject.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in behaviours)
            {
                if (mb == null || mb.GetType() != _localizeType)
                    continue;

                if (excludeRoot != null && mb.transform.IsChildOf(excludeRoot))
                    continue;

                var textTarget = h.TextTarget.GetValue(mb);
                if (textTarget == null)
                    continue;

                // Strategy 1: read TMP_Text.text directly (works when Localize has already run)
                if (h.SerializedCmp != null)
                {
                    var cmp = h.SerializedCmp.GetValue(textTarget) as TMP_Text;
                    if (cmp != null)
                    {
                        string text = CleanText(cmp.text);
                        if (!string.IsNullOrWhiteSpace(text))
                            return text;
                    }
                }

                // Strategy 2: resolve locKey via ActiveLocProvider (works even if TMP_Text
                // is inactive/empty or DoLocalize hasn't fired yet)
                if (h.LocKey != null)
                {
                    string locKey = h.LocKey.GetValue(textTarget) as string;
                    if (!string.IsNullOrEmpty(locKey))
                    {
                        string resolved = ResolveLocKey(locKey);
                        if (!string.IsNullOrEmpty(resolved))
                            return resolved;
                    }
                }
            }

            return null;
        }

        // Cached reflection for Languages.ActiveLocProvider.GetLocalizedText(string, params (string,string)[])
        private sealed class LanguagesHandles
        {
            public FieldInfo ActiveLocProvider;    // public static field on Languages
            public MethodInfo GetLocalizedText;    // instance method on provider's field type
        }

        private static Type _languagesType;
        private static bool _languagesTypeSearched;

        private static readonly ReflectionCache<LanguagesHandles> _languagesCache = new ReflectionCache<LanguagesHandles>(
            builder: t =>
            {
                var h = new LanguagesHandles
                {
                    ActiveLocProvider = t.GetField("ActiveLocProvider", BindingFlags.Public | BindingFlags.Static),
                };
                if (h.ActiveLocProvider != null)
                {
                    h.GetLocalizedText = h.ActiveLocProvider.FieldType.GetMethod("GetLocalizedText",
                        new[] { typeof(string), typeof(ValueTuple<string, string>[]) });
                }
                return h;
            },
            validator: h => h.ActiveLocProvider != null && h.GetLocalizedText != null,
            logTag: "UITextExtractor",
            logSubject: "Languages");

        /// <summary>
        /// Resolves a localization key via Languages.ActiveLocProvider.GetLocalizedText().
        /// Returns null if the key can't be resolved or resolves to itself.
        /// </summary>
        public static string ResolveLocKey(string locKey)
        {
            if (!_languagesTypeSearched)
            {
                _languagesTypeSearched = true;
                _languagesType = FindType("Wotc.Mtga.Loc.Languages");
            }
            if (_languagesType == null) return null;
            if (!_languagesCache.EnsureInitialized(_languagesType)) return null;
            var h = _languagesCache.Handles;

            try
            {
                var locProvider = h.ActiveLocProvider.GetValue(null);
                if (locProvider == null) return null;

                string result = h.GetLocalizedText.Invoke(locProvider,
                    new object[] { locKey, Array.Empty<ValueTuple<string, string>>() }) as string;

                // Localization returns the key itself if not found
                if (string.IsNullOrEmpty(result) || result == locKey)
                    return null;

                return CleanText(result);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a localized set name from the game's localization system.
        /// Uses the key pattern "General/Sets/{setCode}".
        /// </summary>
        private static string GetLocalizedSetName(string setCode)
        {
            return ResolveLocKey("General/Sets/" + setCode);
        }

        /// <summary>
        /// Maps a set code to a human-readable set name.
        /// Tries the game's localization system first (supports all languages),
        /// then falls back to the set code itself.
        /// </summary>
        public static string MapSetCodeToName(string setCode)
        {
            // Try the game's localization system: "General/Sets/{setCode}"
            var localized = GetLocalizedSetName(setCode);
            if (localized != null)
                return localized;

            // If no localization found, return the set code itself
            return setCode;
        }
    }
}
