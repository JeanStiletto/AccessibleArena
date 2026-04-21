# UITextExtractor.Localization.cs
Path: src/Core/Services/UITextExtractor/UITextExtractor.Localization.cs
Lines: 170

## Top-level comments
- Feature partial for game-localization access: reads TMP_Text via Wotc.Mtga.Loc.Localize components, resolves loc keys through Languages.ActiveLocProvider, and maps set codes to localized set names.

## public static partial class UITextExtractor (line 9)

### Fields
- private static Type _localizeType (line 12, static) — Wotc.Mtga.Loc.Localize type cache
- private static FieldInfo _textTargetField (line 13, static)
- private static FieldInfo _serializedCmpField (line 14, static)
- private static FieldInfo _locKeyField (line 15, static)
- private static bool _localizeReflectionResolved (line 16, static)
- private static FieldInfo _activeLocProviderField (line 117, static) — Languages.ActiveLocProvider (public static FIELD, not property)
- private static MethodInfo _getLocalizedTextMethod (line 118, static) — GetLocalizedText(string, params (string,string)[])
- private static bool _locReflectionInitialized (line 119, static)

### Methods
- private static string TryGetLocalizeText(GameObject gameObject) (line 25) — two strategies: read TMP_Text.text from Localize.serializedCmp (incl. inactive children), or resolve locKey directly via ActiveLocProvider
- public static string ResolveLocKey(string locKey) (line 90) — calls ActiveLocProvider.GetLocalizedText with empty locParams; returns null when key resolves to itself
- private static void EnsureLocReflectionCached() (line 121)
- private static string GetLocalizedSetName(string setCode) (line 149) — uses key pattern "General/Sets/{setCode}"
- public static string MapSetCodeToName(string setCode) (line 159) — tries game localization first, falls back to the set code itself
