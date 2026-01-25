using UnityEngine;

namespace AccessibleArena.Core.Services.ElementGrouping
{
    /// <summary>
    /// Assigns UI elements to groups based on their parent hierarchy and name patterns.
    /// Replaces scattered IsChildOf... methods with unified pattern matching.
    /// </summary>
    public class ElementGroupAssigner
    {
        private readonly OverlayDetector _overlayDetector;

        public ElementGroupAssigner(OverlayDetector overlayDetector)
        {
            _overlayDetector = overlayDetector;
        }

        /// <summary>
        /// Determine which group an element belongs to.
        /// Returns the appropriate group based on element name and parent hierarchy.
        /// </summary>
        public ElementGroup DetermineGroup(GameObject element)
        {
            if (element == null) return ElementGroup.Unknown;

            string name = element.name;
            string parentPath = GetParentPath(element);

            // 1. Check overlay groups first (highest priority)
            var overlayGroup = DetermineOverlayGroup(element, name, parentPath);
            if (overlayGroup != ElementGroup.Unknown)
                return overlayGroup;

            // 2. Check for Play-related elements (Play button, events, direct challenge, rankings)
            if (IsPlayElement(element, name, parentPath))
                return ElementGroup.Play;

            // 3. Check for Progress-related elements (boosters, mastery, gems, gold, wildcards)
            if (IsProgressElement(name, parentPath))
                return ElementGroup.Progress;

            // 4. Check for Primary actions (main CTA buttons, but not Play button)
            // Primary elements are shown as standalone items at group level
            if (IsPrimaryAction(element, name, parentPath))
                return ElementGroup.Primary;

            // 5. Check for Navigation elements
            if (IsNavigationElement(name, parentPath))
                return ElementGroup.Navigation;

            // 6. Check for Filter controls
            if (IsFilterElement(name, parentPath))
                return ElementGroup.Filters;

            // 7. Check for Settings controls (when settings panel is not overlay)
            if (IsSettingsControl(name, parentPath))
                return ElementGroup.Settings;

            // 8. Default to Content for everything else
            // (Secondary group removed - those elements now go to Content or Navigation)
            return ElementGroup.Content;
        }

        /// <summary>
        /// Check if element belongs to an overlay group.
        /// </summary>
        private ElementGroup DetermineOverlayGroup(GameObject element, string name, string parentPath)
        {
            // Popup/Dialog - be specific to avoid matching "Screenspace Popups" canvas
            // Look for actual popup panel patterns, not just "Popup" substring
            if (parentPath.Contains("SystemMessageView") ||
                parentPath.Contains("ConfirmationDialog") ||
                parentPath.Contains("InviteFriendPopup") ||
                parentPath.Contains("PopupDialog") ||
                (parentPath.Contains("Popup") && !parentPath.Contains("Screenspace Popups")))
                return ElementGroup.Popup;

            // Social/Friends panel
            if (parentPath.Contains("SocialUI") || parentPath.Contains("FriendsWidget") ||
                parentPath.Contains("SocialPanel"))
                return ElementGroup.Social;

            // Play blade
            if (IsInsidePlayBlade(parentPath, name))
                return ElementGroup.PlayBlade;

            // Settings menu (when it's the active overlay)
            if (parentPath.Contains("SettingsMenu") || parentPath.Contains("Content - MainMenu") ||
                parentPath.Contains("Content - Gameplay") || parentPath.Contains("Content - Graphics") ||
                parentPath.Contains("Content - Audio") || parentPath.Contains("Content - Account"))
                return ElementGroup.SettingsMenu;

            // NPE overlay
            if (parentPath.Contains("NPE") || parentPath.Contains("NewPlayerExperience") ||
                parentPath.Contains("StitcherSparky") || parentPath.Contains("Sparky"))
                return ElementGroup.NPE;

            return ElementGroup.Unknown;
        }

        /// <summary>
        /// Check if element is a Play-related element (Play button, events, direct challenge, rankings, learn).
        /// </summary>
        private bool IsPlayElement(GameObject element, string name, string parentPath)
        {
            // Main play button on home screen
            if (name == "MainButton" || name == "MainButtonOutline")
                return true;

            // Check for MainButton component (the big Play button)
            var components = element.GetComponents<MonoBehaviour>();
            foreach (var comp in components)
            {
                if (comp != null && comp.GetType().Name == "MainButton")
                    return true;
            }

            // Direct Challenge button
            if (name.Contains("DirectChallenge"))
                return true;

            // Rankings / Rangliste
            if (name.Contains("Ranking") || name.Contains("Leaderboard") || name.Contains("Rangliste"))
                return true;

            // Events on home screen (Starter Duel, Color Challenge, etc.)
            if (parentPath.Contains("EventWidget") || parentPath.Contains("EventPanel") ||
                parentPath.Contains("HomeEvent") || parentPath.Contains("FeaturedEvent"))
                return true;

            // Home page banners (right side events like Starter Deck Duel, Color Challenge, Ranked)
            if (parentPath.Contains("HomeBanner_Right") || parentPath.Contains("Banners_Right"))
                return true;

            // Event entries by name patterns
            if (name.Contains("StarterDuel") || name.Contains("ColorChallenge") ||
                name.Contains("Event_") || name.Contains("_Event"))
                return true;

            // Campaign entries (Color Challenge is part of campaign)
            if (name.Contains("Campaign") && !parentPath.Contains("CampaignGraph"))
                return true;

            // Learn / Tutorial elements
            if (name.Contains("Learn") || name.Contains("Tutorial"))
                return true;

            return false;
        }

        /// <summary>
        /// Check if element is a Progress-related element (boosters, mastery, gems, gold, wildcards).
        /// </summary>
        private bool IsProgressElement(string name, string parentPath)
        {
            // Booster/Pack elements
            if (name.Contains("Booster") || name.Contains("Pack"))
                return true;

            // Mastery elements
            if (name.Contains("Mastery"))
                return true;

            // Currency buttons (gems, gold, coins)
            if (name.Contains("Gem") || name.Contains("Gold") || name.Contains("Coin") || name.Contains("Currency"))
                return true;

            // Wildcard elements
            if (name.Contains("Wildcard") || name.Contains("WildCard"))
                return true;

            // Vault progress
            if (name.Contains("Vault"))
                return true;

            // Resource/wallet area
            if (parentPath.Contains("Wallet") || parentPath.Contains("ResourceBar") ||
                parentPath.Contains("CurrencyDisplay"))
                return true;

            // Quest/daily rewards
            if (name.Contains("Quest") || name.Contains("DailyReward") || name.Contains("DailyWins"))
                return true;

            return false;
        }

        /// <summary>
        /// Check if element is a primary action button (main CTA, but not Play button).
        /// </summary>
        private bool IsPrimaryAction(GameObject element, string name, string parentPath)
        {
            // Note: MainButton/Play button is now handled by IsPlayElement

            // Submit/Confirm/Continue buttons
            if (name.Contains("Submit") || name.Contains("Confirm") || name.Contains("Continue"))
                return true;

            // Primary button patterns
            if (name.Contains("PrimaryButton") || name.Contains("Button_Primary"))
                return true;

            // New Deck button in Decks screen
            if (name.Contains("NewDeck") || name.Contains("CreateDeck"))
                return true;

            return false;
        }

        /// <summary>
        /// Check if element is a navigation element.
        /// </summary>
        private bool IsNavigationElement(string name, string parentPath)
        {
            // NavBar elements
            if (parentPath.Contains("NavBar") || name.StartsWith("Nav_") || name.Contains("NavItem"))
                return true;

            // Tabs and page selectors
            if (parentPath.Contains("ContentPageSelector") || parentPath.Contains("TabBar") ||
                name.Contains("Tab_") || name.Contains("_Tab"))
                return true;

            // Back buttons
            if (name.Contains("Back") && name.Contains("Button") && !name.Contains("Backer"))
                return true;

            // Format selector tabs (Standard, Historic, etc.)
            if (parentPath.Contains("FormatSelector") || name.Contains("Format_"))
                return true;

            return false;
        }

        /// <summary>
        /// Check if element is a filter control.
        /// </summary>
        private bool IsFilterElement(string name, string parentPath)
        {
            // Filter bars and containers
            if (parentPath.Contains("FilterBar") || parentPath.Contains("CardFilter") ||
                parentPath.Contains("FilterPanel") || parentPath.Contains("SearchBar") ||
                parentPath.Contains("DeckColorFilters"))
                return true;

            // Filter toggles and buttons
            if (name.Contains("Filter_") || name.Contains("_Filter") ||
                name.Contains("FilterToggle") || name.Contains("FilterButton"))
                return true;

            // Mana color filters
            if (name.Contains("ManaFilter") || name.Contains("ColorFilter"))
                return true;

            // Search fields
            if (name.Contains("Search") && (name.Contains("Field") || name.Contains("Input")))
                return true;

            // Sort controls
            if (name.Contains("Sort") && (name.Contains("Button") || name.Contains("Dropdown")))
                return true;

            // Folder toggles in deck list
            if (name.Contains("Folder") && name.Contains("Toggle"))
                return true;

            return false;
        }

        /// <summary>
        /// Check if element is a settings control.
        /// </summary>
        private bool IsSettingsControl(string name, string parentPath)
        {
            // Settings-specific controls
            if (parentPath.Contains("Settings") && !parentPath.Contains("SettingsButton"))
            {
                // Sliders, dropdowns, checkboxes within settings
                if (name.Contains("Slider") || name.Contains("Dropdown") ||
                    name.Contains("Toggle") || name.Contains("Checkbox"))
                    return true;

                // Stepper controls
                if (name.Contains("Stepper") || name.Contains("Increment") || name.Contains("Decrement"))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Check if element is a secondary action.
        /// </summary>
        private bool IsSecondaryAction(string name, string parentPath)
        {
            // Settings button (opens settings menu)
            if (name.Contains("Settings") && name.Contains("Button"))
                return true;

            // Help button
            if (name.Contains("Help") && name.Contains("Button"))
                return true;

            // Info buttons
            if (name.Contains("Info") && name.Contains("Button"))
                return true;

            // Note: DirectChallenge moved to Play group

            // Profile/Avatar buttons
            if (name.Contains("Profile") || name.Contains("Avatar"))
                return true;

            // Mail/Notifications
            if (name.Contains("Mail") || name.Contains("Notification"))
                return true;

            // Options button
            if (name == "Options" || name.Contains("OptionsButton"))
                return true;

            return false;
        }

        /// <summary>
        /// Check if element is inside the Play Blade.
        /// </summary>
        private bool IsInsidePlayBlade(string parentPath, string name)
        {
            // Direct blade containers
            if (parentPath.Contains("PlayBlade") || parentPath.Contains("Blade_") ||
                parentPath.Contains("BladeContent") || parentPath.Contains("BladeContainer"))
                return true;

            // FindMatch blade
            if (parentPath.Contains("FindMatch"))
                return true;

            // Filter list items in blade context
            if (name.Contains("FilterListItem") && parentPath.Contains("Blade"))
                return true;

            // Campaign graph (Color Challenge)
            if (parentPath.Contains("CampaignGraph"))
                return true;

            return false;
        }

        /// <summary>
        /// Get the full parent path of an element as a concatenated string.
        /// Used for efficient pattern matching against parent hierarchy.
        /// </summary>
        private static string GetParentPath(GameObject element)
        {
            var pathBuilder = new System.Text.StringBuilder();
            Transform current = element.transform.parent;

            while (current != null)
            {
                if (pathBuilder.Length > 0)
                    pathBuilder.Insert(0, "/");
                pathBuilder.Insert(0, current.name);
                current = current.parent;
            }

            return pathBuilder.ToString();
        }

        /// <summary>
        /// For deck elements, extract the folder name from the parent hierarchy.
        /// Decks are inside DeckFolder_Base which contains a Folder_Toggle sibling with the folder name.
        /// Returns null if not a deck in a folder.
        /// </summary>
        public static string GetFolderNameForDeck(GameObject element)
        {
            if (element == null) return null;

            // Walk up to find DeckFolder_Base parent
            Transform current = element.transform;
            while (current != null)
            {
                if (current.name.Contains("DeckFolder_Base"))
                {
                    // Found the folder container - look for Folder_Toggle child
                    var folderToggle = current.Find("Folder_Toggle");
                    if (folderToggle != null)
                    {
                        // Extract text from the toggle to get folder name
                        string folderName = UITextExtractor.GetText(folderToggle.gameObject);
                        if (!string.IsNullOrEmpty(folderName))
                            return folderName;
                    }
                    break;
                }
                current = current.parent;
            }

            return null;
        }

        /// <summary>
        /// Check if an element is a deck entry (has ", deck" in its label or is a DeckView).
        /// </summary>
        public static bool IsDeckElement(GameObject element, string label)
        {
            if (element == null) return false;

            // Check label pattern
            if (!string.IsNullOrEmpty(label) && label.Contains(", deck"))
                return true;

            // Check parent hierarchy for DeckView
            Transform current = element.transform;
            while (current != null)
            {
                if (current.name.Contains("DeckView_Base"))
                    return true;
                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// Check if an element is a folder toggle.
        /// </summary>
        public static bool IsFolderToggle(GameObject element)
        {
            if (element == null) return false;
            return element.name == "Folder_Toggle" || element.name.Contains("Folder_Toggle");
        }

        /// <summary>
        /// Get the folder name from a folder toggle element.
        /// </summary>
        public static string GetFolderNameFromToggle(GameObject folderToggle)
        {
            if (folderToggle == null) return null;
            return UITextExtractor.GetText(folderToggle);
        }
    }
}
