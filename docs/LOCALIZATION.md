# Accessible Arena - Localization System

How the mod's string handling works, how to add languages, and how to extend it.

## Architecture Overview

```
lang/en.json          <- Source of truth for all English strings (~360 keys)
lang/{code}.json      <- Translations (de, fr, es, it, pt-BR, ja, ko, ru, pl, zh-CN, zh-TW)
                         All embedded in DLL at build time, extracted on startup.

src/Core/Services/LocaleManager.cs   <- Loads JSON, resolves keys, handles plurals
src/Core/Models/Strings.cs           <- Public API: Strings.ModLoaded, Strings.ZoneWithCount(...)
src/Core/Services/ModSettings.cs     <- Stores Language setting, fires OnLanguageChanged
```

### Flow

1. `AccessibleArenaMod.InitializeServices()` calls `LocaleManager.Initialize(language)` early, before any `Strings.*` usage
2. `LocaleManager` loads `lang/{code}.json` (active) + `lang/en.json` (fallback)
3. `Strings.cs` properties call `L.Get(key)` or `L.Format(key, args)` on every access
4. When the user cycles language in F2 menu, `ModSettings.CycleLanguage()` calls `LocaleManager.SetLanguage()`, fires `OnLanguageChanged`, and HelpNavigator/ModSettingsNavigator rebuild their item lists

### Why properties instead of cached fields?

`Strings.ModLoaded` is a `static string` property (not a field) that calls `L.Get("ModLoaded")` each time. This means:
- Language changes take effect immediately without restarting
- No stale strings cached from a previous language
- Negligible performance cost (dictionary lookup, called only on user-facing events)

## File Format

Locale files are flat JSON key-value pairs in `lang/{code}.json`:

```json
{
  "ModLoaded": "MTGA Accessibility Mod loaded",
  "ZoneWithCount_One": "{0}, 1 card",
  "ZoneWithCount_Format": "{0}, {1} cards",
  "Activating_Format": "Activating {0}"
}
```

### Key naming conventions

- **Simple strings:** `"KeyName": "value"` - used via `L.Get("KeyName")`
- **Format strings:** `"KeyName_Format": "text {0} more {1}"` - used via `L.Format("KeyName_Format", arg0, arg1)`
- **Singular forms:** `"KeyName_One": "1 card"` - used when count == 1
- **Slavic few forms:** `"KeyName_Few": "{0} karty"` - used for counts 2-4 in Russian/Polish

### Why hand-written JSON parser?

The mod runs inside MelonLoader which targets .NET Framework 4.7.2. We avoid external dependencies (no Newtonsoft.Json, no System.Text.Json). The parser in `LocaleManager.ParseFlatJson()` handles our flat key-value structure and supports standard escape sequences (`\n`, `\t`, `\"`, `\uXXXX`).

## Fallback Chain

When `L.Get("SomeKey")` is called:

1. Look in active language dictionary -> return if found
2. Look in English fallback dictionary -> return if found
3. Return the key name itself (e.g., `"SomeKey"`)

This means:
- Incomplete translations gracefully fall back to English
- Missing English keys show the key name (easy to spot in testing)
- The mod never shows blank text

## Pluralization

Three plural rule families, selected by language code:

**OneOther** (en, de, fr, es, it, pt-BR):
- count == 1 -> `_One` key
- everything else -> `_Format` key

**Slavic** (ru, pl):
- count == 1 -> `_One`
- count % 10 in [2,3,4] and count % 100 not in [12,13,14] -> `_Few`
- everything else -> `_Format`

**NoPluralForm** (ja, ko, zh-CN, zh-TW):
- Always uses `_Format` (these languages don't distinguish singular/plural)

### Why not ICU MessageFormat?

ICU is powerful but requires a dependency and complex syntax that translators would struggle with. Our simple `_One`/`_Few`/`_Format` suffix convention covers all 12 supported languages and is easy for anyone to edit.

## Message Classification

Two toggle settings in `ModSettings` control what gets announced:

### TutorialMessages (default: On)

Controls tutorial hints - the keyboard instructions appended to activation announcements.

```csharp
// In Strings.cs:
public static string WithHint(string core, string hintKey) =>
    ShowHints ? $"{core}. {L.Get(hintKey)}" : core;

// Usage in BaseNavigator:
return Strings.WithHint(core, "NavigateHint");
// With hints ON:  "Home screen. 5 items. Arrow keys to navigate, Enter to select"
// With hints OFF: "Home screen. 5 items"
```

### VerboseAnnouncements (default: On)

Controls extra detail like counts and positions.

```csharp
// In Strings.cs:
public static string WithDetail(string core, string detail) =>
    ShowVerbose ? $"{core}. {detail}" : core;
```

### Why not filter at the Announce() level?

Filtering happens in `Strings.cs` rather than in the announcement service because:
- The announcement service doesn't know which part of a message is a hint vs. core content
- `WithHint()` and `WithDetail()` let each call site decide exactly what's optional
- No changes needed to the `IAnnouncementService` interface

## How To: Add a New String

1. Add the key and English text to `lang/en.json`:
   ```json
   "MyNewString": "Hello world"
   ```

2. Add a property to `Strings.cs`:
   ```csharp
   public static string MyNewString => L.Get("MyNewString");
   ```

3. Use it in navigator code:
   ```csharp
   _announcer.Announce(Strings.MyNewString, AnnouncementPriority.Normal);
   ```

4. Optionally add translations to other `lang/{code}.json` files. Missing keys fall back to English automatically.

### For format strings with parameters:

```json
"Greeting_Format": "Hello {0}, you have {1} items"
```

```csharp
public static string Greeting(string name, int count) => L.Format("Greeting_Format", name, count);
```

### For pluralized strings:

```json
"ItemCount_One": "1 item",
"ItemCount_Format": "{0} items"
```

```csharp
public static string ItemCount(int count) =>
    count == 1 ? L.Get("ItemCount_One") : L.Format("ItemCount_Format", count);
```

For Slavic languages, also add `"ItemCount_Few": "{0} przedmioty"` in pl.json / `"{0} предмета"` in ru.json.

## How To: Add a New Language

1. Add the language code and display name to `ModSettings.cs`:
   ```csharp
   public static readonly string[] LanguageCodes = { "en", "de", ..., "xx" };
   public static readonly string[] LanguageNames = { "English", "German", ..., "NewLang" };
   ```

2. Add a plural rule in `LocaleManager.cs`:
   ```csharp
   { "xx", PluralRule.OneOther },  // or Slavic, or NoPluralForm
   ```

3. Create `lang/xx.json` by copying `en.json` and translating the values. Keep all keys and `{0}` placeholders intact. The file will be automatically embedded in the DLL on next build.

4. For Slavic languages: add `_Few` keys where `_One` keys exist.

## How To: Add a Hint Toggle

Use `WithHint()` for any activation announcement that includes keyboard instructions:

```csharp
// Before (always shows hint):
return $"{ScreenName}. Arrow keys to navigate, Enter to select.";

// After (hint is togglable):
return Strings.WithHint(ScreenName, "NavigateHint");
```

The hint key (`"NavigateHint"`) must exist in `en.json`.

## File Deployment

All 12 locale JSON files are embedded as .NET resources in the DLL at build time (via `EmbeddedResource` glob in the csproj). On every mod startup, `LocaleManager.EnsureDefaultLocaleFiles()` extracts them to `UserData/AccessibleArena/lang/`. This means:

- **No separate file deployment needed** - the DLL is fully self-contained
- **Mod updates ship updated translations** - files are overwritten on each launch
- **Users can still edit files** - but edits will be reset on next launch (by design, to keep translations in sync with code)

Source files live in `lang/` in the repo. The build embeds them via:

```xml
<EmbeddedResource Include="..\lang\*.json" LogicalName="lang.%(Filename)%(Extension)" />
```

At runtime, the extraction code iterates `Assembly.GetManifestResourceNames()` and writes each `lang.*.json` resource to the lang directory.

## Supported Languages

- **en** - English (OneOther plural rule)
- **de** - German (OneOther)
- **fr** - French (OneOther)
- **es** - Spanish (OneOther)
- **it** - Italian (OneOther)
- **pt-BR** - Portuguese, Brazil (OneOther)
- **ja** - Japanese (NoPluralForm)
- **ko** - Korean (NoPluralForm)
- **ru** - Russian (Slavic, 3 forms)
- **pl** - Polish (Slavic, 3 forms)
- **zh-CN** - Chinese, Simplified (NoPluralForm)
- **zh-TW** - Chinese, Traditional (NoPluralForm)

These match the languages available in MTGA itself.
