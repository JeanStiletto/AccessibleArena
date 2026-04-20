# UITextExtractor.ContextLabels.cs
Path: src/Core/Services/UITextExtractor/UITextExtractor.ContextLabels.cs
Lines: 729

## Top-level comments
- Feature partial for context-specific label extraction: navbar currency/mail/wildcard/tokens, deck-entry names, booster pack names, play-mode tabs, event tiles, Jump In packets, DeckManager icon buttons, and Store item labels.

## public static partial class UITextExtractor (line 8)

### Fields
(no fields declared in this partial)

### Methods
- private static string TryGetCurrencyLabel(GameObject gameObject) (line 15) — handles Nav_Coins/Nav_Gems/Nav_Mail (label + amount/count) and Nav_WildCard (tooltip with per-rarity counts + vault progress)
- private static string GetWildcardTooltipText(GameObject gameObject) (line 65) — reads TooltipTrigger.TooltipData.Text, strips style tags, joins newlines with ", "
- private static string TryGetNavTokenLabel(GameObject gameObject) (line 108) — reads tooltip text built by NavBarTokenView; falls back to active Token_* child names
- private static string TryGetDeckName(GameObject gameObject) (line 148) — walks up to 3 levels looking for Blade_ListItem/DeckListItem/DeckView, reads TMP_InputField text
- private static string TryGetBoosterPackName(GameObject gameObject) (line 217) — reads SealedBoosterView.SetCode/_info/_quantityText, detects Bonus/Mythic/Alchemy via CollationId 900980 and _boosterBackgroundTexturePath
- private static string TryGetPlayModeTabText(GameObject gameObject) (line 383) — extracts mode from "Blade_Tab_Deluxe (OpenPlay)" / "Blade_Tab_Ranked" naming
- private static string TryGetEventTileLabel(GameObject gameObject) (line 447) — delegates to EventAccessor.GetEventTileLabel when inside EventTile parent
- private static string TryGetPacketLabel(GameObject gameObject) (line 473) — delegates to EventAccessor.GetPacketLabel when inside a JumpStartPacket component
- private static string TryGetDeckManagerButtonText(GameObject gameObject) (line 506) — handles "_MainButton_Round"/"_MainButtonBlue" DeckManager icons via Localize first, then loc keys (Clone/Delete/Export/Import/Favorite/Edit)
- public static string TryGetStoreItemLabel(GameObject gameObject) (line 615) — reads StoreItemBase._label OptionalObject, falls back to TMP_Text children (excluding price-like text), then cleaned GO name
- private static bool IsPriceText(string text) (line 721) — starts with $/€/£ or short numeric
