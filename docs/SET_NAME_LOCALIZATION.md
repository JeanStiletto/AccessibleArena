# Set Name Localization

## Status: WIP - Not working yet

The localization key is confirmed correct but `MapSetCodeToName()` still returns 3-letter codes at runtime. Needs debugging in next session.

## Correct Approach

Localized set names are available via the game's localization system:

```csharp
Languages.ActiveLocProvider.GetLocalizedText("General/Sets/" + setCode)
```

- `Languages` type: `Wotc.Mtga.Loc.Languages` (Core.dll)
- `ActiveLocProvider`: static property returning `IClientLocProvider`
- `IClientLocProvider`: `Wotc.Mtga.Loc.IClientLocProvider` (SharedClientCore.dll)
- Method: `GetLocalizedText(string key)` — single string parameter overload

Examples of localization keys:
- `General/Sets/DSK` -> "Duskmourn: House of Horror" (en), "Duskmourn: Haus des Schreckens" (de)
- `General/Sets/MOM` -> "March of the Machine" (en), "Marcha de las maquinas" (es)
- `General/Sets/FIN` -> "Magic: The Gathering - FINAL FANTASY" (same in all languages)

The `MasteryNavigator` already uses this exact pattern successfully for mastery track names (keys like `MainNav/BattlePass/<trackName>`).

## Current Implementation

`UITextExtractor.MapSetCodeToName()` tries `GetLocalizedSetName()` first, falls back to raw set code.

`GetLocalizedSetName()` caches reflection for `Languages.ActiveLocProvider` and calls `GetLocalizedText("General/Sets/" + setCode)`.

**Problem:** Still returning 3-letter codes. Possible causes to investigate:
- `GetLocalizedText` with just `string` param may not match — the actual method signature is `GetLocalizedText(string, params (string,string)[])`. The single-string overload lookup via `GetMethod("GetLocalizedText", new[] { typeof(string) })` might fail because the method has a params array
- `ActiveLocProvider` might be null at the time of set filter navigation
- The key pattern might need exact casing or different format for some sets
- Reflection initialization might succeed but method invocation might silently fail

## Investigation History (Dead Ends)

### 1. CollationMappingExtensions.GetName() - DEAD END
`Wotc.Mtga.Wrapper.CollationMappingExtensions.GetName()` just returns `.ToString()` which gives the enum name (3-letter code like "BLB"). Not a localized name at all.

### 2. ISetMetadataProvider.FlavorForCollation() - DEAD END
Tried accessing via `ContentController_StoreCarousel._setMetadataProvider` (found the field, provider was non-null). However `FlavorForCollation(collationMapping)` returns empty strings for all sets because `ClientSetCollation.FlavorId` is never populated by the game data at runtime.

### 3. StoreSetFilterToggles._filterDataProvider - DEAD END
This field is declared on `StoreSetFilterToggles` but never assigned by the game — always null.

### 4. ClientSetMetadata - NO DISPLAY NAME
Has `SetCode` (string), `Collations`, `Availability`, `ReleaseDate`, various bool flags, but no display name or loc key field.

### 5. Store UI elements - IMAGES ONLY
The store's set filter UI uses purely graphical elements for set identification:
- `StoreSetFilterToggle`: only has `Image _symbol` (expansion icon sprite)
- `StoreSetFilterDropdownItem`: has `Image _symbol` + `RawImage _logo` (no text)
- Set logos are loaded via `StoreSetUtils.LogoForSetName()` which fetches `Logo.HeaderLogo` textures from `AssetLookupTree`
- The `AssetLookupTree.Blackboard` has a `Language` property set from `Languages.CurrentLanguage`, so logo textures may be locale-specific

## Key Types

- `SetMetadataProvider` (`Core.Shared.Code.CardFilters.SetMetadataProvider`, Core.dll) — runtime implementation of `ISetMetadataProvider`
- `StoreSetFilterModel` (`Core.Meta.MainNavigation.Store.Data`, Core.dll) — `SetSymbol` (string field = 3-letter code), `Availability` (field), `Sets` (List of CollationMapping)
- `StoreSetFilterDropdown` (Core.dll) — mobile dropdown, instantiates `StoreSetFilterDropdownItem` per set
- `StoreSetUtils` (`Core.Meta.MainNavigation.Store.Utils`, Core.dll) — `SpriteForSetName()`, `LogoForSetName()`
- `Logo` (`AssetLookupTree.Payloads.Booster`, Core.dll) — `HeaderLogo` (AltAssetReference of Texture), `TextureRef`

## Previous Approach (Removed)

Before discovering the localization system, a hardcoded `SetCodeMap` dictionary (~85 entries) mapped set codes to English names. This was removed in favor of the dynamic approach, but the dynamic approach isn't working yet.
