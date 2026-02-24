using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Utility class to extract readable text from Unity UI GameObjects.
    /// Checks various UI components in priority order to find the best text representation.
    /// </summary>
    public static class UITextExtractor
    {
        /// <summary>
        /// Checks if the GameObject has actual text content (not just object name fallback).
        /// Used to distinguish elements with real labels from those with only internal names.
        /// </summary>
        public static bool HasActualText(GameObject gameObject)
        {
            if (gameObject == null)
                return false;

            // Check for input fields with content
            var tmpInputField = gameObject.GetComponent<TMP_InputField>();
            if (tmpInputField != null && !string.IsNullOrWhiteSpace(tmpInputField.text))
                return true;

            var inputField = gameObject.GetComponent<InputField>();
            if (inputField != null && !string.IsNullOrWhiteSpace(inputField.text))
                return true;

            // Check for TMP text
            var tmpText = gameObject.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                string cleaned = CleanText(tmpText.text);
                if (!string.IsNullOrWhiteSpace(cleaned))
                    return true;
            }

            // Check for legacy Text
            var text = gameObject.GetComponentInChildren<Text>();
            if (text != null)
            {
                string cleaned = CleanText(text.text);
                if (!string.IsNullOrWhiteSpace(cleaned))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Extracts the most relevant text from a UI GameObject.
        /// </summary>
        /// <param name="gameObject">The GameObject to extract text from</param>
        /// <returns>The extracted text, or the GameObject name as fallback</returns>
        public static string GetText(GameObject gameObject)
        {
            if (gameObject == null)
                return string.Empty;

            // Check for special label overrides (buttons with misleading game labels)
            string overrideLabel = GetLabelOverride(gameObject.name);
            if (overrideLabel != null)
                return overrideLabel;

            // Check if this is a deck entry (MetaDeckView) - look for parent with input field containing deck name
            string deckName = TryGetDeckName(gameObject);
            if (!string.IsNullOrEmpty(deckName))
            {
                return deckName;
            }

            // Check if this is a booster pack element - look for CarouselBooster parent with pack name
            string boosterName = TryGetBoosterPackName(gameObject);
            if (!string.IsNullOrEmpty(boosterName))
            {
                return boosterName;
            }

            // Check if this is an objective element - extract full info including progress
            string objectiveText = TryGetObjectiveText(gameObject);
            if (!string.IsNullOrEmpty(objectiveText))
            {
                return objectiveText;
            }

            // Check if this is an NPE objective element (tutorial stages)
            string npeObjectiveText = TryGetNPEObjectiveText(gameObject);
            if (!string.IsNullOrEmpty(npeObjectiveText))
            {
                return npeObjectiveText;
            }

            // Check if this is a store item - extract label from StoreItemBase
            string storeItemLabel = TryGetStoreItemLabel(gameObject);
            if (!string.IsNullOrEmpty(storeItemLabel))
            {
                return storeItemLabel;
            }

            // Check if this is a play mode tab - extract mode from element name
            string playModeText = TryGetPlayModeTabText(gameObject);
            if (!string.IsNullOrEmpty(playModeText))
            {
                return playModeText;
            }

            // Check if this is a DeckManager icon button - extract function from element name
            string deckManagerButtonText = TryGetDeckManagerButtonText(gameObject);
            if (!string.IsNullOrEmpty(deckManagerButtonText))
            {
                return deckManagerButtonText;
            }

            // Check for input fields FIRST (they contain text children that we don't want to read directly)
            var tmpInputField = gameObject.GetComponent<TMP_InputField>();
            if (tmpInputField != null)
            {
                return GetInputFieldText(tmpInputField);
            }

            var inputField = gameObject.GetComponent<InputField>();
            if (inputField != null)
            {
                return GetInputFieldText(inputField);
            }

            // Try Toggle (checkbox)
            var toggle = gameObject.GetComponent<Toggle>();
            if (toggle != null)
            {
                return GetToggleText(toggle);
            }

            // Try Scrollbar
            var scrollbar = gameObject.GetComponent<Scrollbar>();
            if (scrollbar != null)
            {
                return GetScrollbarText(scrollbar);
            }

            // Try Slider
            var slider = gameObject.GetComponent<Slider>();
            if (slider != null)
            {
                return GetSliderText(slider);
            }

            // Try Dropdown
            var tmpDropdown = gameObject.GetComponent<TMP_Dropdown>();
            if (tmpDropdown != null)
            {
                return GetDropdownText(tmpDropdown);
            }

            var dropdown = gameObject.GetComponent<Dropdown>();
            if (dropdown != null)
            {
                return GetDropdownText(dropdown);
            }

            // Try TextMeshPro text
            var tmpText = gameObject.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                string cleaned = CleanText(tmpText.text);
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    return cleaned;
                }
            }

            // Try legacy Unity UI Text
            var text = gameObject.GetComponentInChildren<Text>();
            if (text != null)
            {
                string cleaned = CleanText(text.text);
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    return cleaned;
                }
            }

            // No text found on element itself - check siblings for label text
            // This helps with UI patterns where buttons get labels from sibling elements
            string siblingText = TryGetSiblingLabel(gameObject);
            if (!string.IsNullOrEmpty(siblingText))
            {
                return siblingText;
            }

            // For FriendsWidget elements, try to get label from parent object name
            // Pattern: Button_AddFriend/Backer_Hitbox -> "Add Friend"
            string friendsWidgetLabel = TryGetFriendsWidgetLabel(gameObject);
            if (!string.IsNullOrEmpty(friendsWidgetLabel))
            {
                return friendsWidgetLabel;
            }

            // For Mailbox items, try to get the mail title (skip "Neu" badge)
            string mailboxTitle = TryGetMailboxItemTitle(gameObject);
            if (!string.IsNullOrEmpty(mailboxTitle))
            {
                return mailboxTitle;
            }

            // Try TooltipTrigger LocString as fallback for image-only buttons (e.g., Nav_Settings, Nav_Learn)
            string tooltipText = TryGetTooltipText(gameObject);
            if (!string.IsNullOrEmpty(tooltipText))
            {
                return tooltipText;
            }

            // Fallback to GameObject name (cleaned up)
            return CleanObjectName(gameObject.name);
        }

        /// <summary>
        /// Returns a label override for elements with misleading game labels.
        /// Used to provide better accessibility labels for buttons like the match end "Continue" button.
        /// </summary>
        private static string GetLabelOverride(string objectName)
        {
            // ExitMatchOverlayButton on MatchEndScene shows "View Battlefield" but actually continues to home
            if (objectName == "ExitMatchOverlayButton")
                return "Continue";

            // NewDeckButton shows "Enter deck name..." placeholder but is actually a create deck button
            if (objectName.Contains("NewDeckButton"))
                return "New Deck";

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

            foreach (var mb in carouselBooster.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;

                if (typeName == "SealedBoosterView")
                {
                    var mbType = mb.GetType();
                    var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

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
                // Include count if available
                if (!string.IsNullOrEmpty(packCount))
                    return $"{packName} ({packCount})";
                return packName;
            }

            return null;
        }

        /// <summary>
        /// Maps a set code to a human-readable set name.
        /// Falls back to the set code if no mapping is found.
        /// </summary>
        public static string MapSetCodeToName(string setCode)
        {
            // Common MTGA set codes to names
            // This list can be expanded as needed
            var setCodeMap = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                // Standard sets
                { "MKM", "Murders at Karlov Manor" },
                { "LCI", "The Lost Caverns of Ixalan" },
                { "WOE", "Wilds of Eldraine" },
                { "MOM", "March of the Machine" },
                { "ONE", "Phyrexia: All Will Be One" },
                { "BRO", "The Brothers' War" },
                { "DMU", "Dominaria United" },
                { "SNC", "Streets of New Capenna" },
                { "NEO", "Kamigawa: Neon Dynasty" },
                { "VOW", "Innistrad: Crimson Vow" },
                { "MID", "Innistrad: Midnight Hunt" },
                { "AFR", "Adventures in the Forgotten Realms" },
                { "STX", "Strixhaven" },
                { "KHM", "Kaldheim" },
                { "ZNR", "Zendikar Rising" },
                { "M21", "Core Set 2021" },
                { "IKO", "Ikoria" },
                { "THB", "Theros Beyond Death" },
                { "ELD", "Throne of Eldraine" },
                { "M20", "Core Set 2020" },
                { "WAR", "War of the Spark" },
                { "RNA", "Ravnica Allegiance" },
                { "GRN", "Guilds of Ravnica" },
                // Alchemy sets
                { "YMKM", "Alchemy: Karlov Manor" },
                { "YLCI", "Alchemy: Ixalan" },
                { "YWOE", "Alchemy: Eldraine" },
                { "YMOM", "Alchemy: March of the Machine" },
                { "YONE", "Alchemy: Phyrexia" },
                { "YBRO", "Alchemy: The Brothers' War" },
                { "YDMU", "Alchemy: Dominaria" },
                // Recent/upcoming sets
                { "OTJ", "Outlaws of Thunder Junction" },
                { "BLB", "Bloomburrow" },
                { "DSK", "Duskmourn" },
                { "FDN", "Foundations" },
                { "ACR", "Aetherdrift" },
                // Y26 (Year 2026) Alchemy sets - prefixed format
                { "Y26-FDN", "Y26: Foundations" },
                { "Y26-DSK", "Y26: Duskmourn" },
                { "Y26-BLB", "Y26: Bloomburrow" },
                { "Y26-OTJ", "Y26: Thunder Junction" },
                { "Y26-MKM", "Y26: Karlov Manor" },
                { "Y26-LCI", "Y26: Ixalan" },
                { "Y26-WOE", "Y26: Eldraine" },
                { "Y26-MOM", "Y26: March of the Machine" },
                { "Y26-ONE", "Y26: Phyrexia" },
                { "Y26-BRO", "Y26: Brothers' War" },
                { "Y26-DMU", "Y26: Dominaria" },
                { "Y26-SNC", "Y26: New Capenna" },
                { "Y26-NEO", "Y26: Neon Dynasty" },
                { "Y26-ACR", "Y26: Aetherdrift" },
                // Masters/Special sets
                { "SIR", "Shadows over Innistrad Remastered" },
                { "KTK", "Khans of Tarkir" },
                { "AKR", "Amonkhet Remastered" },
                { "KLR", "Kaladesh Remastered" },
            };

            if (setCodeMap.TryGetValue(setCode, out string name))
            {
                return name;
            }

            // If no mapping found, return the set code itself
            return setCode;
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
        /// Extracts button labels from DeckManager icon buttons.
        /// These are icon-only buttons with no text, but the element name contains the function
        /// (e.g., "Clone_MainButton_Round" -> "Clone", "Delete_MainButton_Round" -> "Delete").
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

            // Specific mappings for known functions
            switch (function.ToLowerInvariant())
            {
                case "clone":
                    return "Clone Deck";
                case "deck details":
                case "deckdetails":
                    return "Deck Details";
                case "delete":
                    return "Delete Deck";
                case "export":
                    return "Export Deck";
                case "import":
                    return "Import Deck";
                case "favorite":
                    return "Favorite";
                case "edit deck":
                case "editdeck":
                    return "Edit Deck";
                default:
                    // Return the cleaned function name with proper casing
                    return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(function.ToLowerInvariant());
            }
        }

        /// <summary>
        /// Extracts text from objective/quest elements with full context.
        /// For quests: includes description + progress (e.g., "Cast 20 spells, 14/20")
        /// For progress indicators: adds type prefix (e.g., "Daily: 250", "Weekly: 5/15")
        /// For wildcard progress: shows rarity and progress (e.g., "Rare Wildcard: 3/6")
        /// </summary>
        private static string TryGetObjectiveText(GameObject gameObject)
        {
            if (gameObject == null || gameObject.name != "ObjectiveGraphics")
                return null;

            var parent = gameObject.transform.parent;
            if (parent == null)
                return null;

            string parentName = parent.name;

            // Check for wildcard progress first (on Packs screen)
            // Parent names: "WildcardProgressUncommon", "Wildcard Progress Rare"
            if (parentName.Contains("WildcardProgress") || parentName.Contains("Wildcard Progress"))
            {
                return TryGetWildcardProgressText(gameObject, parentName);
            }

            // Extract objective type from parent name
            // Format: "Objective_Base(Clone) - QuestNormal" or "Objective_Base(Clone) - Daily"
            string objectiveType = null;
            int dashIndex = parentName.IndexOf(" - ");
            if (dashIndex >= 0 && dashIndex + 3 < parentName.Length)
            {
                objectiveType = parentName.Substring(dashIndex + 3).Trim();
            }

            // For quest objectives (QuestNormal), get description + progress
            if (objectiveType == "QuestNormal")
            {
                string description = null;
                string progress = null;
                string reward = null;

                // Look for TextLine (description), Text_GoalProgress (progress), Circle (reward)
                for (int i = 0; i < gameObject.transform.childCount; i++)
                {
                    var child = gameObject.transform.GetChild(i);
                    string childName = child.name;

                    if (childName == "TextLine")
                    {
                        var tmpText = child.GetComponentInChildren<TMP_Text>();
                        if (tmpText != null)
                            description = CleanText(tmpText.text);
                    }
                    else if (childName == "Text_GoalProgress")
                    {
                        var tmpText = child.GetComponentInChildren<TMP_Text>();
                        if (tmpText != null)
                            progress = CleanText(tmpText.text);
                    }
                    else if (childName == "Circle")
                    {
                        var tmpText = child.GetComponentInChildren<TMP_Text>();
                        if (tmpText != null)
                            reward = CleanText(tmpText.text);
                    }
                }

                // Build the label: "Quest description, progress, reward gold"
                if (!string.IsNullOrEmpty(description))
                {
                    var parts = new System.Collections.Generic.List<string> { description };
                    if (!string.IsNullOrEmpty(progress))
                        parts.Add(progress);
                    if (!string.IsNullOrEmpty(reward))
                        parts.Add($"{reward} gold");
                    return string.Join(", ", parts);
                }
            }
            // For other objective types (Daily, Weekly, BattlePass), add type prefix
            else if (!string.IsNullOrEmpty(objectiveType))
            {
                string mainValue = null;
                string progressValue = null;

                // Look for Circle (main display) and Text_GoalProgress (detailed progress)
                for (int i = 0; i < gameObject.transform.childCount; i++)
                {
                    var child = gameObject.transform.GetChild(i);
                    string childName = child.name;

                    if (childName == "Circle")
                    {
                        var tmpText = child.GetComponentInChildren<TMP_Text>();
                        if (tmpText != null)
                            mainValue = CleanText(tmpText.text);
                    }
                    else if (childName == "Text_GoalProgress")
                    {
                        var tmpText = child.GetComponentInChildren<TMP_Text>();
                        if (tmpText != null)
                            progressValue = CleanText(tmpText.text);
                    }
                }

                // Clean up type names for readability
                string typeLabel = objectiveType;
                if (objectiveType == "BattlePass - Level")
                    typeLabel = "Battle Pass Level";
                else if (objectiveType == "SparkRankTier1")
                    typeLabel = "Spark Rank";

                // Build label based on objective type
                if (objectiveType == "Daily")
                {
                    // Daily: "0/15 wins, 250 gold"
                    if (!string.IsNullOrEmpty(progressValue) && !string.IsNullOrEmpty(mainValue))
                        return $"{typeLabel}: {progressValue} wins, {mainValue} gold";
                    else if (!string.IsNullOrEmpty(progressValue))
                        return $"{typeLabel}: {progressValue}";
                }
                else if (objectiveType == "BattlePass - Level")
                {
                    // BattlePass: "Level 7, 400/1000 EP"
                    if (!string.IsNullOrEmpty(mainValue) && !string.IsNullOrEmpty(progressValue))
                        return $"{typeLabel}: {mainValue}, {progressValue}";
                    else if (!string.IsNullOrEmpty(mainValue))
                        return $"{typeLabel}: {mainValue}";
                }
                else
                {
                    // Weekly, SparkRank, etc: just show progress
                    if (!string.IsNullOrEmpty(progressValue))
                        return $"{typeLabel}: {progressValue}";
                    else if (!string.IsNullOrEmpty(mainValue))
                        return $"{typeLabel}: {mainValue}";
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts text from wildcard progress elements on the Packs screen.
        /// These show progress toward earning wildcards of specific rarities.
        /// Parent names: "WildcardProgressUncommon", "Wildcard Progress Rare"
        /// </summary>
        private static string TryGetWildcardProgressText(GameObject gameObject, string parentName)
        {
            // Extract rarity from parent name
            string rarity = null;
            string parentLower = parentName.ToLowerInvariant();
            if (parentLower.Contains("uncommon"))
                rarity = "Uncommon";
            else if (parentLower.Contains("rare"))
                rarity = "Rare";
            else if (parentLower.Contains("mythic"))
                rarity = "Mythic";
            else if (parentLower.Contains("common"))
                rarity = "Common";

            // Look for progress value in children (same structure as objectives)
            string progressValue = null;
            string fillPercentage = null;

            // Search all child transforms for text elements
            var allTexts = gameObject.GetComponentsInChildren<TMP_Text>(true);
            foreach (var tmpText in allTexts)
            {
                if (tmpText == null) continue;

                string objName = tmpText.gameObject.name;
                string content = CleanText(tmpText.text);

                if (string.IsNullOrEmpty(content)) continue;

                // Text_GoalProgress contains the fraction (e.g., "3/6")
                if (objName == "Text_GoalProgress" || objName.Contains("GoalProgress"))
                {
                    progressValue = content;
                }
                // TextLine may contain additional text
                else if (objName == "TextLine" || objName.Contains("TextLine"))
                {
                    // If it looks like a progress value, use it
                    if (content.Contains("/"))
                        progressValue = content;
                }
            }

            // Also check for Image fill amount as a fallback for percentage
            var images = gameObject.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img == null) continue;
                string imgName = img.gameObject.name.ToLowerInvariant();
                if (imgName.Contains("fill") || imgName.Contains("progress"))
                {
                    if (img.type == Image.Type.Filled && img.fillAmount > 0 && img.fillAmount < 1)
                    {
                        int percent = Mathf.RoundToInt(img.fillAmount * 100);
                        fillPercentage = $"{percent}%";
                    }
                }
            }

            // Build the label
            string label = rarity != null ? $"{rarity} Wildcard" : "Wildcard";

            if (!string.IsNullOrEmpty(progressValue))
                return $"{label}: {progressValue}";
            else if (!string.IsNullOrEmpty(fillPercentage))
                return $"{label}: {fillPercentage}";
            else
                return label;
        }

        /// <summary>
        /// Extracts text from NPE (New Player Experience) objective elements.
        /// These are the tutorial stage indicators (Stage I, II, III, etc.) with completion status.
        /// </summary>
        private static string TryGetNPEObjectiveText(GameObject gameObject)
        {
            if (gameObject == null || !gameObject.name.StartsWith("Objective_NPE"))
                return null;

            var texts = gameObject.GetComponentsInChildren<TMP_Text>(true);

            string romanNumeral = null;
            bool romanNumeralIsActive = false;
            bool isCompleted = false;
            bool isLocked = false;

            // Extract Roman numeral from child elements (check both active and inactive)
            foreach (var text in texts)
            {
                if (text == null) continue;
                string objNameLower = text.gameObject.name.ToLower();

                // Look for RomanNumeral element specifically
                if (objNameLower.Contains("roman") || objNameLower.Contains("numeral"))
                {
                    string content = text.text?.Trim();
                    if (!string.IsNullOrEmpty(content) && content != "\u200B")
                    {
                        content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", "").Trim();
                        if (!string.IsNullOrEmpty(content))
                        {
                            romanNumeral = content;
                            romanNumeralIsActive = text.gameObject.activeInHierarchy;
                        }
                    }
                }
            }

            // If no Roman numeral found from named elements, try to detect from any active text
            if (string.IsNullOrEmpty(romanNumeral))
            {
                foreach (var text in texts)
                {
                    if (text == null || !text.gameObject.activeInHierarchy) continue;
                    string content = text.text?.Trim();
                    if (string.IsNullOrEmpty(content)) continue;
                    content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", "").Trim();

                    // Check if content is a Roman numeral (I, II, III, IV, V, VI, VII, VIII, IX, X)
                    if (System.Text.RegularExpressions.Regex.IsMatch(content, @"^[IVX]+$"))
                    {
                        romanNumeral = content;
                        romanNumeralIsActive = true;
                        break;
                    }
                }
            }

            // Detect completion: if RomanNumeral exists but is inactive, stage is completed
            // (completed stages hide their Roman numeral and show a checkmark instead)
            if (!string.IsNullOrEmpty(romanNumeral) && !romanNumeralIsActive)
            {
                isCompleted = true;
            }

            // Also check for explicit completion/lock indicators in child objects
            foreach (Transform child in gameObject.transform)
            {
                string childName = child.name.ToLower();
                if ((childName.Contains("complete") || childName.Contains("check") || childName.Contains("done"))
                    && child.gameObject.activeInHierarchy)
                    isCompleted = true;
                if (childName.Contains("lock") && child.gameObject.activeInHierarchy)
                    isLocked = true;
            }

            // Build the label
            string stageLabel = "Stage";
            if (!string.IsNullOrEmpty(romanNumeral))
                stageLabel = $"Stage {romanNumeral}";

            string result = stageLabel;

            if (isCompleted)
                result += ". Completed";
            else if (isLocked)
                result += ". Locked";

            return result;
        }

        /// <summary>
        /// Tries to get a label from sibling elements when the element itself has no text.
        /// This handles UI patterns where a button's label comes from a sibling element.
        /// Example: Color Challenge buttons have an "INFO" sibling with the color name.
        /// </summary>
        private static string TryGetSiblingLabel(GameObject gameObject)
        {
            if (gameObject == null) return null;

            var parent = gameObject.transform.parent;
            if (parent == null) return null;

            // Look through siblings for text content
            foreach (Transform sibling in parent)
            {
                // Skip self
                if (sibling.gameObject == gameObject) continue;

                // Skip decorative/structural elements
                string sibName = sibling.name.ToUpper();
                if (sibName.Contains("MASK") ||
                    sibName.Contains("SHADOW") ||
                    sibName.Contains("DIVIDER") ||
                    sibName.Contains("BACKGROUND") ||
                    sibName.Contains("INDICATION"))
                {
                    continue;
                }

                // Try to get text from this sibling
                var tmpText = sibling.GetComponentInChildren<TMP_Text>();
                if (tmpText != null)
                {
                    string cleaned = CleanText(tmpText.text);
                    // Must be meaningful text (not just single char or placeholder)
                    if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length > 1)
                    {
                        return cleaned;
                    }
                }

                var legacyText = sibling.GetComponentInChildren<Text>();
                if (legacyText != null)
                {
                    string cleaned = CleanText(legacyText.text);
                    if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length > 1)
                    {
                        return cleaned;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Tries to get a label from parent object names for FriendsWidget elements.
        /// The friends panel uses Backer_Hitbox children inside parent containers like Button_AddFriend.
        /// Pattern: Button_AddFriend/Backer_Hitbox -> "Add Friend"
        /// </summary>
        private static string TryGetFriendsWidgetLabel(GameObject gameObject)
        {
            if (gameObject == null) return null;

            // Check if we're inside FriendsWidget
            Transform current = gameObject.transform;
            bool insideFriendsWidget = false;
            int maxLevels = 10;

            while (current != null && maxLevels > 0)
            {
                if (current.name.Contains("FriendsWidget"))
                {
                    insideFriendsWidget = true;
                    break;
                }
                current = current.parent;
                maxLevels--;
            }

            if (!insideFriendsWidget) return null;

            // Get the immediate parent name and try to extract a label from it
            var parent = gameObject.transform.parent;
            if (parent == null) return null;

            string parentName = parent.name;

            // Pattern: Button_Something -> "Something"
            if (parentName.StartsWith("Button_"))
            {
                string label = parentName.Substring(7); // Remove "Button_"
                // Clean up: AddFriend -> "Add Friend"
                label = CleanObjectName(label);
                return label;
            }

            // Pattern: Something_Button -> "Something"
            if (parentName.EndsWith("_Button"))
            {
                string label = parentName.Substring(0, parentName.Length - 7); // Remove "_Button"
                label = CleanObjectName(label);
                return label;
            }

            // For other patterns, check if parent has meaningful TMP_Text children
            // that might not be direct children of our element
            var parentTmpText = parent.GetComponentInChildren<TMP_Text>();
            if (parentTmpText != null)
            {
                string text = CleanText(parentTmpText.text);
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 1)
                {
                    return text;
                }
            }

            return null;
        }

        /// <summary>
        /// Tries to get the mail title from a mailbox item, skipping the "Neu/New" badge.
        /// Mailbox items have structure: Mailbox_Blade_ListItem_Base/Button with children containing
        /// the title text and a "Neu" badge for unread items.
        /// </summary>
        private static string TryGetMailboxItemTitle(GameObject gameObject)
        {
            if (gameObject == null) return null;

            // Check if we're inside a Mailbox context
            string path = GetParentPath(gameObject);
            if (!path.Contains("Mailbox")) return null;

            // Walk up to find the Mailbox_Blade_ListItem_Base container
            Transform current = gameObject.transform;
            Transform listItemContainer = null;
            int maxLevels = 5;

            while (current != null && maxLevels > 0)
            {
                if (current.name.Contains("Mailbox_Blade_ListItem"))
                {
                    listItemContainer = current;
                    break;
                }
                current = current.parent;
                maxLevels--;
            }

            if (listItemContainer == null) return null;

            // Get all TMP_Text children and find the title (skip "Neu"/"New" badges)
            var textComponents = listItemContainer.GetComponentsInChildren<TMP_Text>(true);
            string bestTitle = null;
            int bestLength = 0;

            foreach (var tmp in textComponents)
            {
                if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;

                string text = CleanText(tmp.text);
                if (string.IsNullOrWhiteSpace(text)) continue;

                // Skip common badge/indicator texts
                string lower = text.ToLower();
                if (lower == "neu" || lower == "new" || lower == "unread" ||
                    lower == "gelesen" || lower == "read" || text.Length <= 3)
                    continue;

                // Prefer longer text (title is usually longer than other labels)
                if (text.Length > bestLength)
                {
                    bestTitle = text;
                    bestLength = text.Length;
                }
            }

            return bestTitle;
        }

        /// <summary>
        /// Tries to extract label text from a TooltipTrigger's LocString field.
        /// Used as a last-resort fallback for image-only buttons (e.g., Nav_Settings, Nav_Learn)
        /// that have no text content but have a localized tooltip.
        /// </summary>
        private static string TryGetTooltipText(GameObject gameObject)
        {
            if (gameObject == null) return null;

            foreach (var comp in gameObject.GetComponents<MonoBehaviour>())
            {
                if (comp == null || comp.GetType().Name != "TooltipTrigger") continue;

                var locStringField = comp.GetType().GetField("LocString",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (locStringField == null) continue;

                var locString = locStringField.GetValue(comp);
                if (locString == null) continue;

                string text = locString.ToString();
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 1 && text.Length < 60)
                    return text;
            }

            return null;
        }

        private static string GetParentPath(GameObject gameObject)
        {
            if (gameObject == null) return "";
            var sb = new System.Text.StringBuilder();
            Transform current = gameObject.transform;
            while (current != null)
            {
                if (sb.Length > 0) sb.Insert(0, "/");
                sb.Insert(0, current.name);
                current = current.parent;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Gets the type of UI element for additional context.
        /// </summary>
        public static string GetElementType(GameObject gameObject)
        {
            if (gameObject == null)
                return "unknown";

            // Check for card first (before button, since cards may have button-like components)
            if (CardDetector.IsCard(gameObject))
                return "card";

            if (gameObject.GetComponent<Button>() != null)
                return "button";

            if (gameObject.GetComponent<TMP_InputField>() != null || gameObject.GetComponent<InputField>() != null)
                return "text field";

            if (gameObject.GetComponent<Toggle>() != null)
                return "checkbox";

            if (gameObject.GetComponent<TMP_Dropdown>() != null || gameObject.GetComponent<Dropdown>() != null)
                return "dropdown";

            if (gameObject.GetComponent<Slider>() != null)
                return "slider";

            if (gameObject.GetComponent<Scrollbar>() != null)
                return "scrollbar";

            if (gameObject.GetComponent<Selectable>() != null)
                return "control";

            return "item";
        }

        /// <summary>
        /// Get a readable label for an input field from its name or placeholder text.
        /// Used for edit mode announcements. Checks name patterns and placeholder content.
        /// </summary>
        /// <param name="inputField">The input field GameObject</param>
        /// <returns>A readable label extracted from the field</returns>
        public static string GetInputFieldLabel(GameObject inputField)
        {
            if (inputField == null) return "text field";

            string name = inputField.name;

            // Try to extract meaningful label from name
            // Common patterns: "Input Field - Email", "InputField_Username", etc.
            if (name.Contains(" - "))
            {
                var parts = name.Split(new[] { " - " }, System.StringSplitOptions.None);
                if (parts.Length > 1)
                    return parts[1].Trim();
            }

            if (name.Contains("_"))
            {
                var parts = name.Split('_');
                if (parts.Length > 1)
                    return parts[parts.Length - 1].Trim();
            }

            // Check placeholder text
            var tmpInput = inputField.GetComponent<TMP_InputField>();
            if (tmpInput != null && tmpInput.placeholder != null)
            {
                var placeholderText = tmpInput.placeholder.GetComponent<TMP_Text>();
                if (placeholderText != null && !string.IsNullOrEmpty(placeholderText.text))
                    return CleanText(placeholderText.text);
            }

            // Check legacy InputField placeholder
            var legacyInput = inputField.GetComponent<InputField>();
            if (legacyInput != null && legacyInput.placeholder != null)
            {
                var placeholderText = legacyInput.placeholder.GetComponent<Text>();
                if (placeholderText != null && !string.IsNullOrEmpty(placeholderText.text))
                    return CleanText(placeholderText.text);
            }

            // Fallback: clean up the name
            string cleaned = name.Replace("Input Field", "").Replace("InputField", "").Trim();
            return string.IsNullOrEmpty(cleaned) ? "text field" : cleaned;
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

            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
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
                catch { }
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

        /// <summary>
        /// Extracts text specifically from button elements.
        /// Searches all TMP_Text children (including inactive) and returns the first valid text found.
        /// More thorough than GetText() for buttons with multiple text children.
        /// </summary>
        /// <param name="buttonObj">The button GameObject</param>
        /// <param name="fallback">Fallback text if no valid text found (null returns null)</param>
        /// <returns>The button text or fallback</returns>
        public static string GetButtonText(GameObject buttonObj, string fallback = null)
        {
            if (buttonObj == null) return fallback;

            // Search all TMP_Text children including inactive ones
            var texts = buttonObj.GetComponentsInChildren<TMP_Text>(true);
            foreach (var text in texts)
            {
                if (text == null) continue;

                string content = CleanText(text.text);
                // Skip empty or single-character content (often icons)
                if (string.IsNullOrEmpty(content) || content.Length <= 1)
                    continue;

                return content;
            }

            // Try legacy Text components
            var legacyTexts = buttonObj.GetComponentsInChildren<Text>(true);
            foreach (var text in legacyTexts)
            {
                if (text == null) continue;

                string content = CleanText(text.text);
                if (string.IsNullOrEmpty(content) || content.Length <= 1)
                    continue;

                return content;
            }

            return fallback;
        }

        /// <summary>
        /// Try to get a meaningful label for an input field from its parent hierarchy.
        /// Looks for patterns like "OpponentName_inputField" → "Opponent Name"
        /// </summary>
        private static string TryGetInputFieldLabel(GameObject inputFieldObj)
        {
            if (inputFieldObj == null) return null;

            // Check parent and grandparent for meaningful names
            Transform current = inputFieldObj.transform.parent;
            int maxLevels = 3;

            while (current != null && maxLevels > 0)
            {
                string name = current.name;

                // Pattern: Something_inputField → "Something"
                if (name.EndsWith("_inputField", System.StringComparison.OrdinalIgnoreCase))
                {
                    string label = name.Substring(0, name.Length - 11); // Remove "_inputField"
                    return CleanObjectName(label);
                }

                // Pattern: inputField_Something → "Something"
                if (name.StartsWith("inputField_", System.StringComparison.OrdinalIgnoreCase))
                {
                    string label = name.Substring(11); // Remove "inputField_"
                    return CleanObjectName(label);
                }

                // Pattern: Login_inputField Something → extract "Something"
                if (name.Contains("_inputField"))
                {
                    int idx = name.IndexOf("_inputField");
                    // Check for text after _inputField
                    if (idx + 11 < name.Length)
                    {
                        string label = name.Substring(idx + 11).Trim();
                        if (!string.IsNullOrEmpty(label))
                            return CleanObjectName(label);
                    }
                    // Otherwise use text before _inputField
                    string prefix = name.Substring(0, idx);
                    // Remove common prefixes like "Login_"
                    if (prefix.Contains("_"))
                        prefix = prefix.Substring(prefix.LastIndexOf('_') + 1);
                    return CleanObjectName(prefix);
                }

                current = current.parent;
                maxLevels--;
            }

            return null;
        }

        private static string GetInputFieldText(TMP_InputField inputField)
        {
            // Try to get a meaningful label from the parent hierarchy
            string fieldLabel = TryGetInputFieldLabel(inputField.gameObject);

            // If there's user input, report it
            // Try .text first, then fall back to textComponent.text (displayed text)
            string userText = CleanText(inputField.text);
            if (string.IsNullOrWhiteSpace(userText) && inputField.textComponent != null)
            {
                userText = CleanText(inputField.textComponent.text);
            }
            if (!string.IsNullOrWhiteSpace(userText))
            {
                // For password fields, don't read the actual text
                if (inputField.inputType == TMP_InputField.InputType.Password)
                {
                    if (!string.IsNullOrEmpty(fieldLabel))
                        return $"{fieldLabel}, has text";
                    return "password field, has text";
                }

                // Show label and content
                if (!string.IsNullOrEmpty(fieldLabel))
                    return $"{fieldLabel}: {userText}";
                return userText;
            }

            // Try to get placeholder text
            if (inputField.placeholder != null)
            {
                var placeholderText = inputField.placeholder.GetComponent<TMP_Text>();
                if (placeholderText != null)
                {
                    string placeholder = CleanText(placeholderText.text);
                    if (!string.IsNullOrWhiteSpace(placeholder))
                    {
                        string empty = Models.Strings.InputFieldEmpty;
                        if (!string.IsNullOrEmpty(fieldLabel))
                            return $"{fieldLabel}, {empty}";
                        return $"{placeholder}, {empty}";
                    }
                }
            }

            // Use derived label if we have one
            if (!string.IsNullOrEmpty(fieldLabel))
                return $"{fieldLabel}, {Models.Strings.InputFieldEmpty}";

            // Fall back to field name
            string fieldName = CleanObjectName(inputField.gameObject.name);
            if (!string.IsNullOrWhiteSpace(fieldName))
            {
                return $"{fieldName}, {Models.Strings.InputFieldEmpty}";
            }

            return Models.Strings.InputFieldEmpty;
        }

        private static string GetInputFieldText(InputField inputField)
        {
            // Try .text first, then fall back to textComponent.text (displayed text)
            string text = inputField.text;
            if (string.IsNullOrWhiteSpace(text) && inputField.textComponent != null)
            {
                text = inputField.textComponent.text;
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                if (inputField.inputType == InputField.InputType.Password)
                    return "password field, contains text";

                return $"{CleanText(text)}, {Models.Strings.TextField}";
            }

            if (inputField.placeholder != null)
            {
                var placeholderText = inputField.placeholder.GetComponent<Text>();
                if (placeholderText != null && !string.IsNullOrWhiteSpace(placeholderText.text))
                {
                    return $"{CleanText(placeholderText.text)}, {Models.Strings.TextField}, {Models.Strings.InputFieldEmpty}";
                }
            }

            return $"{Models.Strings.TextField}, {Models.Strings.InputFieldEmpty}";
        }

        private static string GetToggleText(Toggle toggle)
        {
            // Only return the label text - UIElementClassifier handles adding "checkbox, checked/unchecked"
            // Try to find associated label
            var label = toggle.GetComponentInChildren<TMP_Text>();
            if (label != null && !string.IsNullOrWhiteSpace(label.text))
            {
                string text = CleanText(label.text);

                // Fix BO3 toggle label: game uses "POSITION" as placeholder text
                if (text.Contains("POSITION"))
                    return Models.Strings.Bo3Toggle();

                return text;
            }

            var legacyLabel = toggle.GetComponentInChildren<Text>();
            if (legacyLabel != null && !string.IsNullOrWhiteSpace(legacyLabel.text))
            {
                string text = CleanText(legacyLabel.text);

                // Fix BO3 toggle label for legacy text too
                if (text.Contains("POSITION"))
                    return Models.Strings.Bo3Toggle();

                return text;
            }

            // Fallback: check all child TMP_Text for "POSITION" placeholder
            // The first GetComponentInChildren may find a different text child
            var allTexts = toggle.GetComponentsInChildren<TMP_Text>(true);
            foreach (var tmp in allTexts)
            {
                if (tmp != null && tmp.text != null && tmp.text.Contains("POSITION"))
                    return Models.Strings.Bo3Toggle();
            }

            // Return empty - UIElementClassifier will use object name as fallback
            return string.Empty;
        }

        private static string GetDropdownText(TMP_Dropdown dropdown)
        {
            // Check if value is within valid range
            if (dropdown.value >= 0 && dropdown.value < dropdown.options.Count)
            {
                string current = dropdown.options[dropdown.value].text;
                return $"{CleanText(current)}, dropdown, {dropdown.value + 1} of {dropdown.options.Count}";
            }

            // No valid selection - try to get label from caption text or object name
            string label = null;
            if (dropdown.captionText != null && !string.IsNullOrWhiteSpace(dropdown.captionText.text))
            {
                label = CleanText(dropdown.captionText.text);
            }

            // Fall back to cleaned object name if caption is empty or generic
            if (string.IsNullOrWhiteSpace(label) || label.ToLower().Contains("select") || label.ToLower().Contains("choose"))
            {
                label = CleanObjectName(dropdown.gameObject.name);
            }

            return $"{label}, dropdown, no selection";
        }

        private static string GetDropdownText(Dropdown dropdown)
        {
            // Check if value is within valid range
            if (dropdown.value >= 0 && dropdown.value < dropdown.options.Count)
            {
                string current = dropdown.options[dropdown.value].text;
                return $"{CleanText(current)}, dropdown, {dropdown.value + 1} of {dropdown.options.Count}";
            }

            // No valid selection - try to get label from caption text or object name
            string label = null;
            if (dropdown.captionText != null && !string.IsNullOrWhiteSpace(dropdown.captionText.text))
            {
                label = CleanText(dropdown.captionText.text);
            }

            // Fall back to cleaned object name if caption is empty or generic
            if (string.IsNullOrWhiteSpace(label) || label.ToLower().Contains("select") || label.ToLower().Contains("choose"))
            {
                label = CleanObjectName(dropdown.gameObject.name);
            }

            return $"{label}, dropdown, no selection";
        }

        private static string GetScrollbarText(Scrollbar scrollbar)
        {
            // Scrollbar value is 0-1, convert to percentage
            int percent = Mathf.RoundToInt(scrollbar.value * 100);

            // Determine direction
            string direction = scrollbar.direction == Scrollbar.Direction.TopToBottom ||
                              scrollbar.direction == Scrollbar.Direction.BottomToTop
                ? "vertical" : "horizontal";

            // For vertical scrollbars, 0 = top, 1 = bottom (or vice versa)
            // Announce position relative to content
            string position;
            if (percent <= 5)
                position = "at top";
            else if (percent >= 95)
                position = "at bottom";
            else
                position = $"{percent} percent";

            return $"scrollbar, {direction}, {position}";
        }

        private static string GetSliderText(Slider slider)
        {
            // Calculate percentage based on slider range
            float range = slider.maxValue - slider.minValue;
            int percent = range > 0
                ? Mathf.RoundToInt((slider.value - slider.minValue) / range * 100)
                : 0;

            // Try to find an associated label
            var label = slider.GetComponentInChildren<TMP_Text>();
            string labelText = null;
            if (label != null)
            {
                labelText = CleanText(label.text);
            }
            else
            {
                var legacyLabel = slider.GetComponentInChildren<Text>();
                if (legacyLabel != null)
                    labelText = CleanText(legacyLabel.text);
            }

            if (!string.IsNullOrWhiteSpace(labelText))
                return $"{labelText}, slider, {percent} percent";

            return $"slider, {percent} percent";
        }

        /// <summary>
        /// Cleans text by removing rich text tags, zero-width spaces, and normalizing whitespace.
        /// </summary>
        public static string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Remove zero-width space (common in MTGA empty fields)
            text = text.Replace("\u200B", "");

            // Remove rich text tags like <color>, <b>, etc.
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");

            // Normalize whitespace
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

            return text.Trim();
        }

        /// <summary>
        /// Extracts the body text from a popup/dialog (SystemMessageView).
        /// Searches for text content in the MessageArea/Scroll View hierarchy.
        /// </summary>
        /// <param name="popupGameObject">The popup root GameObject (e.g., SystemMessageView_Desktop_16x9(Clone))</param>
        /// <returns>The popup body text, or null if not found</returns>
        public static string GetPopupBodyText(GameObject popupGameObject)
        {
            if (popupGameObject == null)
                return null;

            // Search paths for the message content (in priority order)
            string[] searchPaths = new[]
            {
                "SystemMessageView_OK_Cancel/MessageArea/Scroll View/Viewport/Content",
                "SystemMessageView_OK/MessageArea/Scroll View/Viewport/Content",
                "MessageArea/Scroll View/Viewport/Content",
                "MessageArea/Content",
                "Content"
            };

            Transform contentTransform = null;

            foreach (var path in searchPaths)
            {
                contentTransform = popupGameObject.transform.Find(path);
                if (contentTransform != null)
                    break;
            }

            // If exact path not found, search recursively for MessageArea
            if (contentTransform == null)
            {
                contentTransform = FindChildRecursive(popupGameObject.transform, "MessageArea");
                if (contentTransform != null)
                {
                    // Look for text within MessageArea
                    var msgAreaText = contentTransform.GetComponentInChildren<TMP_Text>(true);
                    if (msgAreaText != null)
                    {
                        string text = CleanText(msgAreaText.text);
                        if (!string.IsNullOrWhiteSpace(text))
                            return text;
                    }
                }
            }

            // Try to get text from the content transform
            if (contentTransform != null)
            {
                // Get all TMP_Text components and concatenate meaningful text
                var texts = contentTransform.GetComponentsInChildren<TMP_Text>(true);
                var bodyParts = new System.Collections.Generic.List<string>();

                foreach (var tmpText in texts)
                {
                    if (tmpText == null) continue;

                    string text = CleanText(tmpText.text);

                    // Skip empty, single-char, or button-like text
                    if (string.IsNullOrWhiteSpace(text) || text.Length <= 1)
                        continue;

                    // Skip if it looks like a button label (common button texts)
                    string lower = text.ToLowerInvariant();
                    if (lower == "ok" || lower == "cancel" || lower == "yes" || lower == "no" ||
                        lower == "accept" || lower == "decline" || lower == "close" ||
                        lower == "weiterbearbeiten" || lower == "deck verwerfen" || lower == "abbrechen")
                        continue;

                    bodyParts.Add(text);
                }

                if (bodyParts.Count > 0)
                {
                    return string.Join(" ", bodyParts);
                }
            }

            // Fallback: search for any TMP_Text in popup that's not in ButtonLayout
            var allTexts = popupGameObject.GetComponentsInChildren<TMP_Text>(true);
            foreach (var tmpText in allTexts)
            {
                if (tmpText == null) continue;

                // Skip text inside button containers
                if (IsInsideButtonContainer(tmpText.transform))
                    continue;

                string text = CleanText(tmpText.text);

                // Must be substantial text (likely the message body)
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 10)
                    return text;
            }

            return null;
        }

        /// <summary>
        /// Represents the structured parts of a mail message.
        /// </summary>
        public struct MailContentParts
        {
            public string Title;
            public string Date;
            public string Body;
            public GameObject TitleObject;
            public GameObject DateObject;
            public GameObject BodyObject;
            public bool HasContent => !string.IsNullOrEmpty(Title) || !string.IsNullOrEmpty(Date) || !string.IsNullOrEmpty(Body);
        }

        /// <summary>
        /// Extracts structured mail content parts (title, date, body) from an opened mail message.
        /// </summary>
        public static MailContentParts GetMailContentParts()
        {
            var parts = new MailContentParts();

            // Find the mailbox content view
            var mailboxPanel = GameObject.Find("ContentController - Mailbox_Base(Clone)");
            if (mailboxPanel == null)
                return parts;

            // Find the letter content container
            var letterBase = FindChildRecursive(mailboxPanel.transform, "Mailbox_Letter_Base");
            if (letterBase == null)
            {
                // Try alternate path
                var contentView = mailboxPanel.transform.Find("SafeArea/ViewSection/Mailbox_ContentView");
                if (contentView != null)
                    letterBase = FindChildRecursive(contentView, "CONTENT_Mailbox_Letter");
            }

            if (letterBase == null)
                return parts;

            // Get all TMP_Text components
            var texts = letterBase.GetComponentsInChildren<TMP_Text>(true);

            foreach (var tmpText in texts)
            {
                if (tmpText == null || !tmpText.gameObject.activeInHierarchy)
                    continue;

                string text = CleanText(tmpText.text);
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                string objName = tmpText.gameObject.name.ToLowerInvariant();
                string parentName = tmpText.transform.parent?.name.ToLowerInvariant() ?? "";
                string grandparentName = tmpText.transform.parent?.parent?.name.ToLowerInvariant() ?? "";

                // Skip button labels
                if (IsInsideButtonContainer(tmpText.transform))
                    continue;

                // Identify by object/parent naming patterns
                if (objName.Contains("title") || parentName.Contains("title") ||
                    objName.Contains("header") || parentName.Contains("header") ||
                    objName.Contains("subject"))
                {
                    if (string.IsNullOrEmpty(parts.Title))
                    {
                        parts.Title = text;
                        parts.TitleObject = tmpText.gameObject;
                    }
                }
                else if (objName.Contains("date") || parentName.Contains("date") ||
                         objName.Contains("time") || parentName.Contains("time"))
                {
                    if (string.IsNullOrEmpty(parts.Date))
                    {
                        parts.Date = text;
                        parts.DateObject = tmpText.gameObject;
                    }
                }
                else if (objName.Contains("body") || parentName.Contains("body") ||
                         objName.Contains("content") || parentName.Contains("content") ||
                         objName.Contains("message") || parentName.Contains("message") ||
                         grandparentName.Contains("body") || grandparentName.Contains("content"))
                {
                    // Append to body if we find multiple body texts
                    if (string.IsNullOrEmpty(parts.Body))
                    {
                        parts.Body = text;
                        parts.BodyObject = tmpText.gameObject;
                    }
                    else if (!parts.Body.Contains(text))
                        parts.Body += " " + text;
                }
                else
                {
                    // If no specific match and text is substantial, treat as body
                    if (text.Length > 20 && string.IsNullOrEmpty(parts.Body))
                    {
                        parts.Body = text;
                        parts.BodyObject = tmpText.gameObject;
                    }
                    else if (text.Length > 5 && text.Length <= 50 && string.IsNullOrEmpty(parts.Title))
                    {
                        // Short text without specific container might be title
                        parts.Title = text;
                        parts.TitleObject = tmpText.gameObject;
                    }
                }
            }

            return parts;
        }

        /// <summary>
        /// Extracts the content text from an opened mail message in the Mailbox.
        /// Searches for text in the Mailbox_ContentView area.
        /// </summary>
        /// <returns>The mail content text (title, body, rewards), or null if not found</returns>
        public static string GetMailContentText()
        {
            // Find the mailbox content view
            var mailboxPanel = GameObject.Find("ContentController - Mailbox_Base(Clone)");
            if (mailboxPanel == null)
                return null;

            // Search paths for mail content (in priority order)
            string[] searchPaths = new[]
            {
                "SafeArea/ViewSection/Mailbox_ContentView",
                "SafeArea/ViewSection",
                "Mailbox_ContentView"
            };

            Transform contentView = null;
            foreach (var path in searchPaths)
            {
                contentView = mailboxPanel.transform.Find(path);
                if (contentView != null)
                    break;
            }

            // Fallback: search recursively for ContentView
            if (contentView == null)
            {
                contentView = FindChildRecursive(mailboxPanel.transform, "ContentView");
            }

            if (contentView == null)
                return null;

            // Get all TMP_Text components and extract meaningful content
            var texts = contentView.GetComponentsInChildren<TMP_Text>(true);
            var contentParts = new System.Collections.Generic.List<string>();

            // Track seen text to avoid duplicates
            var seenTexts = new System.Collections.Generic.HashSet<string>();

            foreach (var tmpText in texts)
            {
                if (tmpText == null || !tmpText.gameObject.activeInHierarchy)
                    continue;

                string text = CleanText(tmpText.text);

                // Skip empty or very short text
                if (string.IsNullOrWhiteSpace(text) || text.Length <= 1)
                    continue;

                // Skip duplicate text
                if (seenTexts.Contains(text))
                    continue;
                seenTexts.Add(text);

                // Skip common button labels
                string lower = text.ToLowerInvariant();
                if (lower == "ok" || lower == "close" || lower == "schließen" ||
                    lower == "claim" || lower == "einfordern" || lower == "abholen" ||
                    lower == "neu" || lower == "new" || lower == "unread" || lower == "gelesen")
                    continue;

                // Skip if inside a button container (but keep the text if it's substantial)
                if (IsInsideButtonContainer(tmpText.transform) && text.Length < 20)
                    continue;

                contentParts.Add(text);
            }

            if (contentParts.Count > 0)
            {
                return string.Join(". ", contentParts);
            }

            return null;
        }

        /// <summary>
        /// Finds a child transform by name recursively.
        /// </summary>
        private static Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Contains(name))
                    return child;

                var found = FindChildRecursive(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Checks if a transform is inside a button container.
        /// </summary>
        private static bool IsInsideButtonContainer(Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                string name = current.name.ToLowerInvariant();
                if (name.Contains("button") || name.Contains("btn"))
                    return true;
                current = current.parent;
            }
            return false;
        }

        private static string CleanObjectName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "unknown";

            // Remove common Unity prefixes/suffixes
            name = name.Replace("(Clone)", "");
            name = name.Replace("Button", " button");
            name = name.Replace("Btn", " button");
            name = name.Replace("Toggle", " checkbox");
            name = name.Replace("InputField", " text field");
            name = name.Replace("Dropdown", " dropdown");

            // Convert PascalCase/camelCase to spaces
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");

            // Remove underscores
            name = name.Replace("_", " ");

            // Normalize whitespace
            name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+", " ");

            return name.Trim().ToLower();
        }
    }
}
