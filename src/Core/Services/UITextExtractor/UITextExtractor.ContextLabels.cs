using UnityEngine;
using TMPro;
using AccessibleArena.Core.Models;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    public static partial class UITextExtractor
    {
        /// <summary>
        /// Detects navbar currency buttons (Nav_Coins, Nav_Gems, Nav_WildCard) and provides
        /// proper labeled text. Gold/Gems get "Label: amount", Wildcards get the tooltip
        /// text which contains per-rarity wildcard counts and vault progress.
        /// </summary>
        private static string TryGetCurrencyLabel(GameObject gameObject)
        {
            string name = gameObject.name;

            if (name == "Nav_Coins" || name == "Nav_Gems")
            {
                // Extract the numeric amount from TMP_Text child
                var tmpText = gameObject.GetComponentInChildren<TMP_Text>();
                string amount = tmpText != null ? CleanText(tmpText.text) : "";

                string label = name == "Nav_Coins"
                    ? Models.Strings.CurrencyGold
                    : Models.Strings.CurrencyGems;

                return !string.IsNullOrEmpty(amount)
                    ? LocaleManager.Instance.Format("LabelValue_Format", label, amount)
                    : label;
            }

            if (name == "Nav_Mail")
            {
                // NavBarMailController shows an unread count badge (TMP_Text UnreadMailCount) when
                // there are unread messages. When active it returns just the count ("1", "2", etc.),
                // losing the "Mail" label. Intercept here to always return "Mail: N" or "Mail".
                var tmpText = gameObject.GetComponentInChildren<TMP_Text>();
                string count = tmpText != null ? CleanText(tmpText.text) : "";
                return !string.IsNullOrEmpty(count)
                    ? LocaleManager.Instance.Format("LabelValue_Format", Models.Strings.NavMail, count)
                    : Models.Strings.NavMail;
            }

            if (name == "Nav_WildCard")
            {
                // Read TooltipData.Text from TooltipTrigger component (same pattern as UIActivator)
                // The game's NavBarController.UpdateWildcardTooltip() populates this with
                // localized wildcard counts + vault progress
                string tooltipText = GetWildcardTooltipText(gameObject);
                if (!string.IsNullOrEmpty(tooltipText))
                    return Models.Strings.CurrencyWildcards + ": " + tooltipText;

                return Models.Strings.CurrencyWildcards;
            }

            return null;
        }

        /// <summary>
        /// Reads the wildcard tooltip text from a TooltipTrigger component.
        /// Strips rich text style tags and joins lines with ", " for screen reader flow.
        /// </summary>
        private static string GetWildcardTooltipText(GameObject gameObject)
        {
            var pubFlags = PublicInstance;

            foreach (var comp in gameObject.GetComponents<MonoBehaviour>())
            {
                if (comp == null || comp.GetType().Name != "TooltipTrigger") continue;

                // TooltipData is a public field
                var dataField = comp.GetType().GetField("TooltipData", pubFlags);
                if (dataField == null) continue;

                var data = dataField.GetValue(comp);
                if (data == null) continue;

                // Text is a public property (virtual getter with localization)
                var textProp = data.GetType().GetProperty("Text", pubFlags);
                if (textProp == null) continue;

                var text = textProp.GetValue(data) as string;
                if (string.IsNullOrWhiteSpace(text)) continue;

                // Strip <style="VaultText"> and </style> tags
                text = System.Text.RegularExpressions.Regex.Replace(text, @"</?style[^>]*>", "");
                // Replace newlines with ", " for screen reader flow
                text = text.Replace("\r\n", ", ").Replace("\n", ", ").Replace("\r", ", ");
                // Clean up any double commas or trailing commas
                while (text.Contains(",  ,") || text.Contains(",,"))
                    text = text.Replace(",  ,", ",").Replace(",,", ",");
                text = text.Trim().TrimEnd(',').Trim();

                if (!string.IsNullOrEmpty(text))
                    return text;
            }

            return null;
        }

        /// <summary>
        /// Extracts a readable label for NavTokenController elements (event/draft tokens on the nav bar).
        /// The tooltip text (set by NavBarTokenView.UpdateTokensTooltip) includes the token count and
        /// description via localized strings. Falls back to token type name from child object names.
        /// </summary>
        private static string TryGetNavTokenLabel(GameObject gameObject)
        {
            // The tooltip text is built by NavBarTokenView.TooltipForTokens and contains
            // the count and description. GetWildcardTooltipText reads TooltipTrigger.TooltipData.Text
            // and strips rich-text tags and joins lines with ", ".
            string tooltipText = GetWildcardTooltipText(gameObject);
            if (!string.IsNullOrEmpty(tooltipText))
                return tooltipText;

            // Fallback: read token type from active child object names (Token_JumpIn, Token_Draft, etc.)
            var tokenNames = new System.Collections.Generic.List<string>();
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                var child = gameObject.transform.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                string childName = child.name;
                if (!childName.StartsWith("Token_")) continue;

                // "Token_JumpIn(Clone)" → "Jump In Token"
                string tokenType = childName;
                int cloneIdx = tokenType.IndexOf("(Clone)");
                if (cloneIdx >= 0)
                    tokenType = tokenType.Substring(0, cloneIdx);
                if (tokenType.StartsWith("Token_"))
                    tokenType = tokenType.Substring(6);
                tokenType = System.Text.RegularExpressions.Regex.Replace(tokenType, @"(?<=[a-z])(?=[A-Z])", " ");
                tokenNames.Add($"{tokenType} Token");
            }

            if (tokenNames.Count > 0)
                return string.Join(", ", tokenNames);

            return null;
        }

        /// <summary>
        /// Tries to extract a deck name from a deck entry element (MetaDeckView).
        /// Deck entries have a TMP_InputField with the actual deck name, but buttons
        /// show "Enter deck name..." placeholder. This method finds the real name.
        /// </summary>
        private static string TryGetDeckName(GameObject gameObject)
        {
            // Skip elements that are clearly not deck entries
            string objName = gameObject.name.ToLower();
            if (objName.Contains("folder") ||
                objName.Contains("toggle") ||
                objName.Contains("bot") ||
                objName.Contains("match") ||
                objName.Contains("avatar") ||
                objName.Contains("scrollbar"))
            {
                return null;
            }

            // Walk up the hierarchy looking for deck entry indicators
            Transform current = gameObject.transform;
            int maxLevels = 3; // Only go up 3 levels - deck entries are compact

            while (current != null && maxLevels > 0)
            {
                string name = current.gameObject.name;

                // Skip if we hit a container that's too high up (not a single deck entry)
                if (name.Contains("Content") ||
                    name.Contains("Viewport") ||
                    name.Contains("Scroll") ||
                    name.Contains("Folder"))
                {
                    return null; // Went too far up, not a deck entry
                }

                // Check for deck entry patterns - must be specific
                // Blade_ListItem_Base is the individual deck entry container
                if (name.Contains("Blade_ListItem") ||
                    name.Contains("DeckListItem") ||
                    (name.Contains("DeckView") && !name.Contains("Selector") && !name.Contains("Folder")))
                {
                    // Found a deck entry container - look for TMP_InputField with actual text
                    var inputFields = current.GetComponentsInChildren<TMP_InputField>(true);
                    foreach (var inputField in inputFields)
                    {
                        // Get the actual text value, not placeholder
                        string deckText = inputField.text;
                        if (!string.IsNullOrWhiteSpace(deckText))
                        {
                            // Remove zero-width spaces
                            deckText = deckText.Replace("\u200B", "").Trim();
                            if (!string.IsNullOrWhiteSpace(deckText) && deckText.Length > 1)
                            {
                                return $"{deckText}, deck";
                            }
                        }
                    }

                    // No valid deck name found in this entry
                    return null;
                }

                current = current.parent;
                maxLevels--;
            }

            return null;
        }

        /// <summary>
        /// Tries to extract a booster pack name from a CarouselBooster element.
        /// Uses SealedBoosterView.SetCode and ClientBoosterInfo for pack identification.
        /// </summary>
        private static string TryGetBoosterPackName(GameObject gameObject)
        {
            // Only process elements that look like booster hitboxes
            string objName = gameObject.name.ToLower();
            if (!objName.Contains("hitbox") && !objName.Contains("booster"))
                return null;

            // Walk up the hierarchy looking for CarouselBooster parent
            Transform current = gameObject.transform;
            int maxLevels = 6; // CarouselBooster can be several levels up
            Transform carouselBooster = null;

            while (current != null && maxLevels > 0)
            {
                string name = current.gameObject.name;

                // Found the CarouselBooster container
                if (name.Contains("CarouselBooster"))
                {
                    carouselBooster = current;
                    break;
                }

                // Stop if we hit the main chamber controller (went too far)
                if (name.Contains("BoosterChamber") || name.Contains("ContentController"))
                    break;

                current = current.parent;
                maxLevels--;
            }

            if (carouselBooster == null)
                return null;

            // Try to get pack info from SealedBoosterView component
            string setCode = null;
            string setName = null;
            string packCount = null;
            string packType = null; // "Mythic", "Alchemy", "Bonus", or null for regular

            foreach (var mb in carouselBooster.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;

                if (typeName == "SealedBoosterView")
                {
                    var mbType = mb.GetType();
                    var flags = AllInstanceFlags;

                    // Get SetCode field
                    var setCodeField = mbType.GetField("SetCode", flags);
                    if (setCodeField != null)
                    {
                        setCode = setCodeField.GetValue(mb) as string;
                    }

                    // Get quantity from _quantityText
                    var quantityTextField = mbType.GetField("_quantityText", flags);
                    if (quantityTextField != null)
                    {
                        var quantityText = quantityTextField.GetValue(mb) as TMP_Text;
                        if (quantityText != null && !string.IsNullOrEmpty(quantityText.text))
                        {
                            packCount = quantityText.text.Trim();
                        }
                    }

                    // Detect pack type.
                    // The golden bonus pack has a fixed collation ID (900980).
                    // Mythic and Alchemy packs are identified via their background texture
                    // path, which is set synchronously in SealedBoosterView.Refresh() and
                    // contains type-specific segments like "mythic" or "alchemy".
                    var collationIdProp = mbType.GetProperty("CollationId", flags);
                    if (collationIdProp != null)
                    {
                        var collationId = collationIdProp.GetValue(mb);
                        if (collationId is int cid && cid == 900980)
                            packType = Strings.PackTypeBonus;
                    }

                    if (packType == null)
                    {
                        var bgPathField = mbType.GetField("_boosterBackgroundTexturePath", flags);
                        if (bgPathField != null)
                        {
                            var bgPath = bgPathField.GetValue(mb) as string;
                            if (!string.IsNullOrEmpty(bgPath))
                            {
                                string bgLower = bgPath.ToLowerInvariant();
                                if (bgLower.Contains("mythic"))
                                    packType = Strings.PackTypeMythic;
                                else if (bgLower.Contains("alchemy"))
                                    packType = Strings.PackTypeAlchemy;
                            }
                        }
                    }

                    // Try to get more info from ClientBoosterInfo
                    var infoField = mbType.GetField("_info", flags);
                    if (infoField != null)
                    {
                        var info = infoField.GetValue(mb);
                        if (info != null)
                        {
                            // Try to get display name from ClientBoosterInfo
                            var infoType = info.GetType();

                            // Check for SetName or DisplayName property
                            var setNameProp = infoType.GetProperty("SetName", flags) ?? infoType.GetProperty("DisplayName", flags);
                            if (setNameProp != null)
                            {
                                setName = setNameProp.GetValue(info) as string;
                            }

                            // Also try field access
                            if (string.IsNullOrEmpty(setName))
                            {
                                var setNameField = infoType.GetField("SetName", flags) ?? infoType.GetField("_setName", flags);
                                if (setNameField != null)
                                {
                                    setName = setNameField.GetValue(info) as string;
                                }
                            }
                        }
                    }

                    break;
                }
            }

            // Build the pack name from available data
            string packName = null;

            // Use set name if available, otherwise map set code to name
            if (!string.IsNullOrEmpty(setName))
            {
                packName = setName;
            }
            else if (!string.IsNullOrEmpty(setCode))
            {
                packName = MapSetCodeToName(setCode);
            }

            if (!string.IsNullOrEmpty(packName))
            {
                // Append pack type (Mythic, Alchemy, Bonus) so blind players get the same
                // information sighted players see from the pack's visual appearance.
                string displayName = string.IsNullOrEmpty(packType)
                    ? packName
                    : $"{packName}, {packType}";

                // Include count if available
                if (!string.IsNullOrEmpty(packCount))
                    return $"{displayName} ({packCount})";
                return displayName;
            }

            return null;
        }

        /// <summary>
        /// Extracts the play mode name from FindMatch tab elements.
        /// Element names contain the mode (e.g., "Blade_Tab_Deluxe (OpenPlay)" -> "Open Play").
        /// The displayed text is often a generic translation that doesn't identify the mode.
        /// </summary>
        private static string TryGetPlayModeTabText(GameObject gameObject)
        {
            if (gameObject == null)
                return null;

            string name = gameObject.name;

            // Only process FindMatch tabs (they have CustomTab component and specific naming)
            // Pattern: "Blade_Tab_Deluxe (ModeName)" or "Blade_Tab_Ranked"
            if (!name.StartsWith("Blade_Tab_"))
                return null;

            // Check if we're in the FindMatchTabs context
            Transform parent = gameObject.transform.parent;
            if (parent == null || !parent.name.Contains("Tabs"))
                return null;

            Transform grandparent = parent.parent;
            if (grandparent == null || !grandparent.name.Contains("FindMatchTabs"))
                return null;

            // Extract mode from element name
            string mode = null;

            // Pattern 1: "Blade_Tab_Deluxe (ModeName)" - extract from parentheses
            int parenStart = name.IndexOf('(');
            int parenEnd = name.IndexOf(')');
            if (parenStart > 0 && parenEnd > parenStart)
            {
                mode = name.Substring(parenStart + 1, parenEnd - parenStart - 1);
            }
            // Pattern 2: "Blade_Tab_ModeName" - extract suffix after last underscore
            else if (name.StartsWith("Blade_Tab_"))
            {
                mode = name.Substring("Blade_Tab_".Length);
            }

            if (string.IsNullOrEmpty(mode))
                return null;

            // Clean up mode names for readability
            // Convert camelCase/PascalCase to spaces
            mode = System.Text.RegularExpressions.Regex.Replace(mode, "([a-z])([A-Z])", "$1 $2");

            // Specific mappings for known modes
            switch (mode.ToLowerInvariant())
            {
                case "openplay":
                case "open play":
                    return "Open Play";
                case "ranked":
                    return "Ranked";
                case "brawl":
                    return "Brawl";
                default:
                    // Return the cleaned mode name with proper casing
                    return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(mode.ToLowerInvariant());
            }
        }

        /// <summary>
        /// Extracts enriched label for PlayBlade event tiles.
        /// Detects event tiles by walking parent chain for "EventTile -" naming pattern.
        /// </summary>
        private static string TryGetEventTileLabel(GameObject gameObject)
        {
            if (gameObject == null) return null;

            // Check if element is inside an event tile by walking parent chain
            Transform current = gameObject.transform;
            bool isInsideEventTile = false;
            while (current != null)
            {
                if (current.name.StartsWith("EventTile"))
                {
                    isInsideEventTile = true;
                    break;
                }
                current = current.parent;
            }

            if (!isInsideEventTile) return null;

            return EventAccessor.GetEventTileLabel(gameObject);
        }

        /// <summary>
        /// Extracts enriched label for Jump In packet selection options.
        /// Detects packet elements by walking parent chain for JumpStartPacket component.
        /// </summary>
        private static string TryGetPacketLabel(GameObject gameObject)
        {
            if (gameObject == null) return null;

            // Check if element is inside a JumpStartPacket by walking parent chain
            Transform current = gameObject.transform;
            bool isInsidePacket = false;
            while (current != null)
            {
                foreach (var mb in current.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == "JumpStartPacket")
                    {
                        isInsidePacket = true;
                        break;
                    }
                }
                if (isInsidePacket) break;
                current = current.parent;
            }

            if (!isInsidePacket) return null;

            return EventAccessor.GetPacketLabel(gameObject);
        }

        /// <summary>
        /// Adds the localized currency name to event-page payment buttons. The game writes only
        /// the numeric quantity (e.g. "2000") to the button text and conveys the currency via an
        /// icon — invisible to screen readers. Returns "2000 Edelsteine" / "2000 Gold".
        /// Returns null for non-payment buttons (Play / Start / Token already have proper text).
        /// </summary>
        private static string TryGetEventPaymentButtonLabel(GameObject gameObject)
        {
            return EventAccessor.GetEventPaymentButtonLabel(gameObject);
        }

        /// <summary>
        /// Extracts button labels from DeckManager icon buttons.
        /// These are icon-only buttons with no text, but the element name contains the function
        /// (e.g., "Clone_MainButton_Round" -> "Clone", "Delete_MainButton_Round" -> "Delete").
        /// Tries the button's Localize component first for proper translation, falls back to
        /// English labels extracted from the GO name.
        /// </summary>
        private static string TryGetDeckManagerButtonText(GameObject gameObject)
        {
            if (gameObject == null)
                return null;

            string name = gameObject.name;

            // Check for DeckManager MainButton patterns
            // Pattern: "Function_MainButton_Round" or "Function_MainButtonBlue"
            bool isRoundButton = name.EndsWith("_MainButton_Round");
            bool isBlueButton = name.EndsWith("_MainButtonBlue");

            if (!isRoundButton && !isBlueButton)
                return null;

            // Verify we're in DeckManager context
            Transform current = gameObject.transform;
            bool inDeckManager = false;
            int maxLevels = 5;

            while (current != null && maxLevels > 0)
            {
                if (current.name.Contains("DeckManager"))
                {
                    inDeckManager = true;
                    break;
                }
                current = current.parent;
                maxLevels--;
            }

            if (!inDeckManager)
                return null;

            // Try Localize component first — these buttons may have localized text
            // on children that the standard TMP_Text check misses
            string localizedText = TryGetLocalizeText(gameObject);
            if (!string.IsNullOrEmpty(localizedText))
                return localizedText;

            // Extract function name from element name
            string function = null;

            if (isRoundButton)
            {
                // "Clone_MainButton_Round" -> "Clone"
                int suffixIndex = name.IndexOf("_MainButton_Round");
                if (suffixIndex > 0)
                    function = name.Substring(0, suffixIndex);
            }
            else if (isBlueButton)
            {
                // "EditDeck_MainButtonBlue" -> "EditDeck"
                int suffixIndex = name.IndexOf("_MainButtonBlue");
                if (suffixIndex > 0)
                    function = name.Substring(0, suffixIndex);
            }

            if (string.IsNullOrEmpty(function))
                return null;

            // Clean up function names for readability
            // Convert camelCase/PascalCase to spaces
            function = System.Text.RegularExpressions.Regex.Replace(function, "([a-z])([A-Z])", "$1 $2");

            // Replace underscores with spaces
            function = function.Replace("_", " ");

            // Try game localization keys (MainNav/DeckManager/DeckManager_Top_*)
            string locKey = null;
            switch (function.ToLowerInvariant())
            {
                case "clone":
                    locKey = "MainNav/DeckManager/DeckManager_Top_Clone";
                    break;
                case "delete":
                    locKey = "MainNav/DeckManager/DeckManager_Top_Delete";
                    break;
                case "export":
                    locKey = "MainNav/DeckManager/DeckManager_Top_Export";
                    break;
                case "import":
                    locKey = "MainNav/DeckManager/DeckManager_Top_Import";
                    break;
                case "favorite":
                    locKey = "MainNav/DeckManager/DeckManager_Top_Favorite";
                    break;
                case "edit deck":
                case "editdeck":
                    locKey = "MainNav/DeckManager/DeckManager_Top_Edit";
                    break;
            }

            if (locKey != null)
            {
                string resolved = ResolveLocKey(locKey);
                if (!string.IsNullOrEmpty(resolved))
                    return resolved;
            }

            // Final fallback: cleaned function name with proper casing
            return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(function.ToLowerInvariant());
        }

        /// <summary>
        /// Tries to extract a label from a Store item (StoreItemBase component).
        /// Store items have a _label OptionalObject with text, plus purchase buttons whose
        /// text should not be used as the item label.
        /// </summary>
        public static string TryGetStoreItemLabel(GameObject gameObject)
        {
            if (gameObject == null) return null;

            // Check if this has a StoreItemBase component
            MonoBehaviour storeItemBase = null;
            foreach (var mb in gameObject.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "StoreItemBase")
                {
                    storeItemBase = mb;
                    break;
                }
            }

            // Also check parent hierarchy (element might be a child of the store item)
            if (storeItemBase == null)
            {
                Transform current = gameObject.transform.parent;
                int maxLevels = 3;
                while (current != null && maxLevels > 0)
                {
                    foreach (var mb in current.GetComponents<MonoBehaviour>())
                    {
                        if (mb != null && mb.GetType().Name == "StoreItemBase")
                        {
                            storeItemBase = mb;
                            break;
                        }
                    }
                    if (storeItemBase != null) break;
                    current = current.parent;
                    maxLevels--;
                }
            }

            if (storeItemBase == null) return null;

            var flags = AllInstanceFlags;
            var itemType = storeItemBase.GetType();

            // Try 1: Get label from _label OptionalObject -> GameObject -> TMPro text
            var labelField = itemType.GetField("_label", flags);
            if (labelField != null)
            {
                try
                {
                    var labelObj = labelField.GetValue(storeItemBase);
                    if (labelObj != null)
                    {
                        var optType = labelObj.GetType();
                        var goField = optType.GetField("GameObject", flags);
                        if (goField != null)
                        {
                            var labelGo = goField.GetValue(labelObj) as GameObject;
                            if (labelGo != null && labelGo.activeInHierarchy)
                            {
                                var tmpText = labelGo.GetComponentInChildren<TMP_Text>();
                                if (tmpText != null && !string.IsNullOrEmpty(tmpText.text))
                                {
                                    return CleanText(tmpText.text);
                                }
                            }
                        }

                        // Try the Localize component's text
                        var textField = optType.GetField("Text", flags);
                        if (textField != null)
                        {
                            var localizeComp = textField.GetValue(labelObj) as MonoBehaviour;
                            if (localizeComp != null)
                            {
                                var tmpInLoc = localizeComp.GetComponentInChildren<TMP_Text>();
                                if (tmpInLoc != null && !string.IsNullOrEmpty(tmpInLoc.text))
                                {
                                    return CleanText(tmpInLoc.text);
                                }
                            }
                        }
                    }
                }
                catch { /* Store item label reflection may fail on different UI versions */ }
            }

            // Try 2: Get text from any TMP_Text child (excluding price-like text)
            var texts = storeItemBase.GetComponentsInChildren<TMP_Text>(false);
            foreach (var t in texts)
            {
                string text = t.text?.Trim();
                if (!string.IsNullOrEmpty(text) && text.Length > 2 && !IsPriceText(text))
                {
                    return CleanText(text);
                }
            }

            // Try 3: Use GameObject name, cleaned up
            string name = storeItemBase.gameObject.name;
            if (name.StartsWith("StoreItem - "))
                name = name.Substring("StoreItem - ".Length);
            return name;
        }

        /// <summary>
        /// Checks if text looks like a price (starts with currency symbol or is short numeric).
        /// Used to filter out purchase button text when extracting item labels.
        /// </summary>
        private static bool IsPriceText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            char first = text[0];
            return first == '$' || first == '\u20AC' || first == '\u00A3' ||
                   (char.IsDigit(first) && text.Length < 15);
        }
    }
}
