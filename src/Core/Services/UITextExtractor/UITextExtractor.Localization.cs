using System;
using System.Reflection;
using UnityEngine;
using TMPro;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    public static partial class UITextExtractor
    {
        // Cached reflection for Localize component text extraction
        private static Type _localizeType;
        private static FieldInfo _textTargetField;
        private static FieldInfo _serializedCmpField;
        private static FieldInfo _locKeyField;
        private static bool _localizeReflectionResolved;

        /// <summary>
        /// Tries to read localized text from Localize components (Wotc.Mtga.Loc.Localize).
        /// Two strategies: (1) read TMP_Text.text from serializedCmp on inactive children that
        /// GetComponentInChildren(false) misses, (2) resolve the locKey directly via the game's
        /// loc provider when the TMP_Text is empty or null (e.g. icon-only buttons with a
        /// Localize component that hasn't fired DoLocalize yet).
        /// </summary>
        private static string TryGetLocalizeText(GameObject gameObject)
        {
            if (!_localizeReflectionResolved)
            {
                _localizeReflectionResolved = true;
                _localizeType = FindType("Wotc.Mtga.Loc.Localize");
                if (_localizeType != null)
                {
                    _textTargetField = _localizeType.GetField("TextTarget", PublicInstance);
                    if (_textTargetField != null)
                    {
                        var targetType = _textTargetField.FieldType;
                        _serializedCmpField = targetType.GetField("serializedCmp", PublicInstance);
                        _locKeyField = targetType.GetField("locKey", PublicInstance);
                    }
                }
            }

            if (_localizeType == null || _textTargetField == null)
                return null;

            // Search element and children (including inactive) for Localize components
            var behaviours = gameObject.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in behaviours)
            {
                if (mb == null || mb.GetType() != _localizeType)
                    continue;

                var textTarget = _textTargetField.GetValue(mb);
                if (textTarget == null)
                    continue;

                // Strategy 1: read TMP_Text.text directly (works when Localize has already run)
                if (_serializedCmpField != null)
                {
                    var cmp = _serializedCmpField.GetValue(textTarget) as TMP_Text;
                    if (cmp != null)
                    {
                        string text = CleanText(cmp.text);
                        if (!string.IsNullOrWhiteSpace(text))
                            return text;
                    }
                }

                // Strategy 2: resolve locKey via ActiveLocProvider (works even if TMP_Text
                // is inactive/empty or DoLocalize hasn't fired yet)
                if (_locKeyField != null)
                {
                    string locKey = _locKeyField.GetValue(textTarget) as string;
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

        /// <summary>
        /// Resolves a localization key via Languages.ActiveLocProvider.GetLocalizedText().
        /// Returns null if the key can't be resolved or resolves to itself.
        /// </summary>
        public static string ResolveLocKey(string locKey)
        {
            EnsureLocReflectionCached();
            if (_activeLocProviderField == null || _getLocalizedTextMethod == null)
                return null;

            try
            {
                var locProvider = _activeLocProviderField.GetValue(null);
                if (locProvider == null) return null;

                string result = _getLocalizedTextMethod.Invoke(locProvider,
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

        // Cached reflection for Languages.ActiveLocProvider.GetLocalizedText(string, params (string,string)[])
        private static FieldInfo _activeLocProviderField;
        private static MethodInfo _getLocalizedTextMethod;
        private static bool _locReflectionInitialized;

        private static void EnsureLocReflectionCached()
        {
            if (_locReflectionInitialized) return;
            _locReflectionInitialized = true;

            try
            {
                var languagesType = FindType("Wotc.Mtga.Loc.Languages");
                if (languagesType == null) return;

                // ActiveLocProvider is a public static FIELD, not a property
                _activeLocProviderField = languagesType.GetField("ActiveLocProvider",
                    BindingFlags.Public | BindingFlags.Static);
                if (_activeLocProviderField != null)
                {
                    var locProviderType = _activeLocProviderField.FieldType;
                    // Method signature: GetLocalizedText(string, params (string,string)[])
                    _getLocalizedTextMethod = locProviderType.GetMethod("GetLocalizedText",
                        new[] { typeof(string), typeof(ValueTuple<string, string>[]) });
                }
            }
            catch { /* Reflection may fail on different game versions */ }
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
