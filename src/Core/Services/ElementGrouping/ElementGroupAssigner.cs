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

            // 0. Filter out Nav_Home (Startseite) - it's a back button handled by Backspace
            if (name == "Nav_Home")
                return ElementGroup.Unknown;

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

            // 3.5. Check for Objectives (daily, weekly, quests, battle pass)
            if (IsObjectiveElement(name, parentPath))
                return ElementGroup.Objectives;

            // 4. Check for Social elements (profile, achievements, mail)
            if (IsSocialElement(name, parentPath))
                return ElementGroup.Social;

            // 5. Check for Primary actions (main CTA buttons, but not Play button)
            // Primary elements are shown as standalone items at group level
            if (IsPrimaryAction(element, name, parentPath))
                return ElementGroup.Primary;

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
            // Deck Builder collection cards (PoolHolder canvas)
            if (parentPath.Contains("PoolHolder") &&
                (name.Contains("MetaCardView") || name.Contains("PagesMetaCardView")))
                return ElementGroup.DeckBuilderCollection;

            // Popup/Dialog - be specific to avoid matching "Screenspace Popups" canvas
            // Look for actual popup panel patterns, not just "Popup" substring
            if (parentPath.Contains("SystemMessageView") ||
                parentPath.Contains("ConfirmationDialog") ||
                parentPath.Contains("InviteFriendPopup") ||
                parentPath.Contains("PopupDialog") ||
                (parentPath.Contains("Popup") && !parentPath.Contains("Screenspace Popups")))
                return ElementGroup.Popup;

            // Friends panel overlay
            if (parentPath.Contains("SocialUI") || parentPath.Contains("FriendsWidget") ||
                parentPath.Contains("SocialPanel"))
                return ElementGroup.FriendsPanel;

            // Play blade - distinguish tabs from content
            if (IsInsidePlayBlade(parentPath, name))
            {
                // Tabs are the navigation buttons at top of PlayBlade
                if (IsPlayBladeTab(name, parentPath))
                    return ElementGroup.PlayBladeTabs;
                return ElementGroup.PlayBladeContent;
            }

            // Settings menu (when it's the active overlay)
            if (parentPath.Contains("SettingsMenu") || parentPath.Contains("Content - MainMenu") ||
                parentPath.Contains("Content - Gameplay") || parentPath.Contains("Content - Graphics") ||
                parentPath.Contains("Content - Audio") || parentPath.Contains("Content - Account"))
                return ElementGroup.SettingsMenu;

            // NPE overlay (but not Objective_NPE which are objectives, not tutorial elements)
            if ((parentPath.Contains("NPE") || parentPath.Contains("NewPlayerExperience") ||
                parentPath.Contains("StitcherSparky") || parentPath.Contains("Sparky")) &&
                !parentPath.Contains("Objective_NPE"))
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
        /// Check if element is a Progress-related element (boosters, mastery, gems, gold, wildcards, tokens).
        /// </summary>
        private bool IsProgressElement(string name, string parentPath)
        {
            // Token controller (event tokens, draft tokens, etc.)
            if (name.Contains("NavTokenController") || name.Contains("Nav_Token"))
                return true;

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
        /// Check if element is an Objective element (daily wins, weekly wins, quests, battle pass).
        /// </summary>
        private bool IsObjectiveElement(string name, string parentPath)
        {
            // Objectives panel and individual objectives
            if (parentPath.Contains("Objectives_Desktop") || parentPath.Contains("ObjectivesLayout"))
                return true;

            // Individual objective types
            if (parentPath.Contains("Objective_Base") || parentPath.Contains("Objective_BattlePass") ||
                parentPath.Contains("Objective_NPE"))
                return true;

            // ObjectiveGraphics is the clickable button for objectives
            if (name == "ObjectiveGraphics" || name.Contains("ObjectiveGraphics"))
                return true;

            return false;
        }

        /// <summary>
        /// Check if element is a Social element (profile, achievements, mail/notifications).
        /// These are the social-related buttons on the home screen.
        /// </summary>
        private bool IsSocialElement(string name, string parentPath)
        {
            // Profile button
            if (name.Contains("Profile") || name.Contains("Avatar"))
                return true;

            // Achievements
            if (name.Contains("Achievement"))
                return true;

            // Mail/Notifications (the numbered entry)
            if (name.Contains("Mail") || name.Contains("Notification") || name.Contains("Inbox"))
                return true;

            // Friends button (opens friends panel)
            if (name.Contains("Friends") && name.Contains("Button"))
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

            // CardFilterView elements (color filters, type filters in deck builder)
            // These are the checkboxes like "CardFilterView Color_White", "CardFilterView Multicolor"
            if (name.Contains("CardFilterView"))
                return true;

            // Advanced Filters button in deck builder
            if (name.Contains("Advanced Filters"))
                return true;

            // Craft/Herstellen filter button
            if (name.Contains("filterButton_Craft"))
                return true;

            // Magnify toggle (card size toggle in collection)
            if (name.Contains("Toggle_Magnify"))
                return true;

            // DeckFilterToggle (show only cards in deck)
            if (name.Contains("DeckFilterToggle"))
                return true;

            // Search fields
            if (name.Contains("Search") && (name.Contains("Field") || name.Contains("Input")))
                return true;

            // Clear search button
            if (name.Contains("Clear Search"))
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
        /// Note: Profile, Mail, Achievements moved to Social group.
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

            // Options button
            if (name == "Options" || name.Contains("OptionsButton"))
                return true;

            return false;
        }

        /// <summary>
        /// Check if element is a PlayBlade tab (Events, Find Match, Recent tabs at top).
        /// </summary>
        private bool IsPlayBladeTab(string name, string parentPath)
        {
            // Tabs have "Blade_Tab_Nav" in their name
            if (name.Contains("Blade_Tab_Nav"))
                return true;

            // Also check parent path for tabs container
            if (parentPath.Contains("Blade_NavTabs") && parentPath.Contains("Tabs_CONTAINER"))
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
