using UnityEngine;
using MelonLoader;

namespace AccessibleArena.Core.Services.ElementGrouping
{
    /// <summary>
    /// Assigns UI elements to groups based on their parent hierarchy and name patterns.
    /// Replaces scattered IsChildOf... methods with unified pattern matching.
    /// </summary>
    public class ElementGroupAssigner
    {
        private readonly OverlayDetector _overlayDetector;
        private int _profileButtonInstanceId;

        public ElementGroupAssigner(OverlayDetector overlayDetector)
        {
            _overlayDetector = overlayDetector;
        }

        /// <summary>
        /// Register the local player's StatusButton instance ID so it can be
        /// recognized and assigned to FriendsPanelProfile during group assignment.
        /// </summary>
        public void SetProfileButtonId(int instanceId) => _profileButtonInstanceId = instanceId;
        public void ClearProfileButtonId() => _profileButtonInstanceId = 0;

        /// <summary>
        /// When true, pool holder cards (PagesMetaCardView in PoolHolder) are classified as
        /// DeckBuilderSideboard instead of DeckBuilderCollection. Set by the navigator when
        /// a deck list exists (the pool shows cards available to add to the deck).
        /// </summary>
        public bool PoolCardsAreSideboard { get; set; }

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

            // Guard: Elements inside social panel that weren't assigned a friend sub-group
            // should be hidden (section headers, background elements, etc.)
            if (parentPath.Contains("SocialUI") || parentPath.Contains("FriendsWidget") ||
                parentPath.Contains("SocialPanel"))
                return ElementGroup.Unknown;

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

            // 8. Exclude Tag buttons (quantity indicators like "4x") from deck list
            // These must be checked here, not in DetermineOverlayGroup, because Unknown
            // is used as "no overlay" signal and would fall through to Content
            if (name == "CustomButton - Tag")
                return ElementGroup.Unknown;

            // 9. Default to Content for everything else
            // (Secondary group removed - those elements now go to Content or Navigation)
            return ElementGroup.Content;
        }

        /// <summary>
        /// Check if element belongs to an overlay group.
        /// </summary>
        private ElementGroup DetermineOverlayGroup(GameObject element, string name, string parentPath)
        {
            // Deck Builder collection/sideboard cards (PoolHolder canvas)
            // When a deck list exists, pool cards are sideboard (PoolCardsAreSideboard flag)
            if (parentPath.Contains("PoolHolder") &&
                (name.Contains("MetaCardView") || name.Contains("PagesMetaCardView")))
                return PoolCardsAreSideboard ? ElementGroup.DeckBuilderSideboard : ElementGroup.DeckBuilderCollection;

            // Deck Builder deck list cards (MainDeck_MetaCardHolder)
            // These are the cards currently in your deck, shown as a compact list
            // Note: Tag button exclusion is handled in DetermineGroup, not here (Unknown returns are ignored)
            if (parentPath.Contains("MainDeck_MetaCardHolder") || parentPath.Contains("CardTile_Base"))
            {
                if (name == "CustomButton - Tile")
                    return ElementGroup.DeckBuilderDeckList;
            }

            // Deck Builder sideboard cards (non-MainDeck holders inside MetaCardHolders_Container)
            // These are cards available to add to deck in draft/sealed deck building
            if (parentPath.Contains("MetaCardHolders_Container") && !parentPath.Contains("MainDeck_MetaCardHolder")
                && name == "CustomButton - Tile")
                return ElementGroup.DeckBuilderSideboard;

            // ReadOnly deck builder cards (StaticColumnMetaCardView in column view)
            // These appear when viewing starter/precon decks in read-only mode
            if (name.Contains("StaticColumnMetaCardView") || parentPath.Contains("StaticColumnMetaCardHolder"))
                return ElementGroup.DeckBuilderDeckList;

            // Challenge screen containers -> ChallengeMain group
            // Must be checked BEFORE PlayBlade and Popup, since challenge containers
            // were previously routed through IsInsidePlayBlade.
            // Exception: InviteFriendPopup elements go to Popup (popup overlay takes over)
            if (IsChallengeContainer(parentPath, name))
            {
                // InviteFriendPopup or ChallengeInviteWindow inside challenge -> Popup overlay
                if (parentPath.Contains("InviteFriendPopup") || parentPath.Contains("ChallengeInviteWindow"))
                    return ElementGroup.Popup;

                // "New Deck" and "Edit/Change Deck" go to PlayBladeFolders (deck selection group)
                // GroupedNavigator includes these as extra items alongside folder toggles
                if (name.Contains("NewDeck") || name.Contains("New Deck") || name.Contains("CreateDeck"))
                    return ElementGroup.PlayBladeFolders;
                if (name.Contains("EditDeck") || name.Contains("Edit_Deck"))
                    return ElementGroup.PlayBladeFolders;

                return ElementGroup.ChallengeMain;
            }

            // Popup/Dialog - be specific to avoid matching "Screenspace Popups" canvas
            // Look for actual popup panel patterns, not just "Popup" substring
            // Skip Popup classification for elements inside PlayBlade (InviteFriendPopup is
            // also a challenge container, and "Popup" substring would match it)
            if (!IsInsidePlayBlade(parentPath, name) &&
                (parentPath.Contains("SystemMessageView") ||
                 parentPath.Contains("ConfirmationDialog") ||
                 parentPath.Contains("InviteFriendPopup") ||
                 parentPath.Contains("PopupDialog") ||
                 (parentPath.Contains("Popup") && !parentPath.Contains("Screenspace Popups"))))
                return ElementGroup.Popup;

            // Friends panel overlay - split into sub-groups
            // Skip for elements inside challenge/blade containers - they belong to PlayBlade, not the friend panel
            if ((parentPath.Contains("SocialUI") || parentPath.Contains("FriendsWidget") ||
                 parentPath.Contains("SocialPanel")) &&
                !IsInsidePlayBlade(parentPath, name))
            {
                var friendGroup = DetermineFriendPanelGroup(element, name, parentPath);
                if (friendGroup != ElementGroup.Unknown)
                    return friendGroup;
                // Filter out elements that don't belong to any friend sub-group
                // (section headers, background elements, etc.)
                return ElementGroup.Unknown;
            }

            // Mailbox panel - mail items shown directly as Content (overlay filtering handles the rest)
            // No separate Mailbox group needed since it's already an overlay
            if (parentPath.Contains("Mailbox") || parentPath.Contains("PlayerInbox"))
                return ElementGroup.Content;

            // Play blade - distinguish tabs from content
            // But exclude deck builder header controls (sideboard toggle, deck name, etc.)
            if (IsInsidePlayBlade(parentPath, name))
            {
                // Exclude deck builder header controls - let them be standalone Content items
                if (parentPath.Contains("TitlePanel_Container") ||
                    parentPath.Contains("DeckListView") ||
                    name.Contains("Sideboard") ||
                    name.Contains("DeckName") ||
                    name.Contains("New Deck Name") ||
                    name.Contains("Button_Cardbacks"))
                    return ElementGroup.Content;

                // Exclude FindMatch nav tab entirely - replaced by queue type subgroup entries
                if (name.Contains("Blade_Tab_Nav") && name.Contains("FindMatch"))
                    return ElementGroup.Unknown;

                // Exclude Play button and New Deck button from PlayBlade content
                // They're global UI elements that happen to be inside the blade hierarchy
                if (name == "MainButton" || name == "MainButtonOutline")
                    return ElementGroup.Unknown;
                if (name.Contains("NewDeck") || name.Contains("New Deck") || name.Contains("CreateDeck"))
                    return ElementGroup.Unknown;

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
        /// Determine which friend panel sub-group an element belongs to.
        /// Maps challenge/add-friend buttons to action groups and friend tiles to section groups.
        /// </summary>
        private ElementGroup DetermineFriendPanelGroup(GameObject element, string name, string parentPath)
        {
            // Challenge button (Backer_Hitbox inside Button_AddChallenge)
            if (parentPath.Contains("Button_AddChallenge") || parentPath.Contains("Button_Challenge") ||
                name.Contains("Button_AddChallenge") || name.Contains("Button_Challenge"))
                return ElementGroup.FriendsPanelChallenge;

            // Add Friend button (Backer_Hitbox inside Button_AddFriend)
            if (parentPath.Contains("Button_AddFriend") || name.Contains("Button_AddFriend"))
                return ElementGroup.FriendsPanelAddFriend;

            // Friend entries: Backer_Hitbox inside SocialEntittiesListItem_* within Bucket_*_CONTAINER
            // Use bucket container names for section detection - more reliable than component type matching
            // since different tile types exist (FriendTile, InviteOutgoingTile, InviteIncomingTile, etc.)
            if (parentPath.Contains("SocialEntittiesListItem") || parentPath.Contains("SocialEntitiesListItem"))
            {
                // Only accept the primary clickable element for each tile.
                // Sub-buttons (accept/reject/block) are handled by left/right action cycling
                // via FriendInfoProvider, not as separate navigable entries.
                if (!IsPrimarySocialTileElement(element))
                    return ElementGroup.Unknown;

                if (parentPath.Contains("Bucket_Friends"))
                    return ElementGroup.FriendSectionFriends;
                if (parentPath.Contains("Bucket_SentRequests") || parentPath.Contains("Bucket_Outgoing"))
                    return ElementGroup.FriendSectionOutgoing;
                if (parentPath.Contains("Bucket_IncomingRequests") || parentPath.Contains("Bucket_Incoming"))
                    return ElementGroup.FriendSectionIncoming;
                if (parentPath.Contains("Bucket_Blocked"))
                    return ElementGroup.FriendSectionBlocked;
            }

            // Local player profile button (StatusButton in FriendsWidget)
            if (_profileButtonInstanceId != 0 && element.GetInstanceID() == _profileButtonInstanceId)
                return ElementGroup.FriendsPanelProfile;

            // Fallback: detect section by tile component type when path patterns don't match.
            // Some sections (e.g., Blocked) may use different list item naming or hierarchy.
            var tile = FriendInfoProvider.FindFriendTile(element);
            if (tile != null)
            {
                string tileName = tile.GetType().Name;

                // First try bucket name from the tile's own parent path (tile may be higher in hierarchy)
                string tilePath = GetParentPath(tile.gameObject);
                if (tilePath.Contains("Bucket_Blocked"))
                    return ElementGroup.FriendSectionBlocked;
                if (tilePath.Contains("Bucket_Friends"))
                    return ElementGroup.FriendSectionFriends;
                if (tilePath.Contains("Bucket_SentRequests") || tilePath.Contains("Bucket_Outgoing"))
                    return ElementGroup.FriendSectionOutgoing;
                if (tilePath.Contains("Bucket_IncomingRequests") || tilePath.Contains("Bucket_Incoming"))
                    return ElementGroup.FriendSectionIncoming;

                // Last resort: map by tile type name
                if (tileName == "BlockTile")
                    return ElementGroup.FriendSectionBlocked;
                if (tileName == "FriendTile")
                    return ElementGroup.FriendSectionFriends;
                if (tileName == "InviteOutgoingTile")
                    return ElementGroup.FriendSectionOutgoing;
                if (tileName == "InviteIncomingTile")
                    return ElementGroup.FriendSectionIncoming;

                MelonLogger.Msg($"[ElementGroupAssigner] Social tile fallback matched {tileName} but no section determined, path={tilePath}");
            }

            // Not a recognized friend panel element (headers, dismiss buttons, tab bar, etc.)
            return ElementGroup.Unknown;
        }

        /// <summary>
        /// Check if an element is the primary clickable for its social tile.
        /// Returns false for sub-buttons (accept/reject/block) that should be handled
        /// by left/right action cycling instead of being separate navigable entries.
        /// </summary>
        private static bool IsPrimarySocialTileElement(GameObject element)
        {
            // Backer_Hitbox is always the primary clickable
            if (element.name == "Backer_Hitbox") return true;

            // Walk up to find the SocialEntittiesListItem parent
            Transform current = element.transform.parent;
            while (current != null)
            {
                if (current.name.StartsWith("SocialEntittiesListItem") ||
                    current.name.StartsWith("SocialEntitiesListItem"))
                {
                    // If this list item has a Backer_Hitbox child, only that should be navigable
                    var hitbox = current.Find("Backer_Hitbox");
                    if (hitbox != null && hitbox.gameObject.activeInHierarchy)
                        return false; // Backer_Hitbox exists but we're not it
                    // No Backer_Hitbox (e.g. BlockTile) - accept this element as fallback
                    return true;
                }
                current = current.parent;
            }

            return true;
        }

        /// <summary>
        /// Check if element is a PlayBlade tab (Events, Recent, and queue type tabs like Ranked/OpenPlay/Brawl).
        /// FindMatch nav tab is excluded - replaced by queue type subgroup entries.
        /// </summary>
        private bool IsPlayBladeTab(string name, string parentPath)
        {
            // Queue type tabs (within FindMatch) are promoted to tab level
            if (name.StartsWith("Blade_Tab_Ranked") || name.StartsWith("Blade_Tab_Deluxe"))
                return true;

            // Regular nav tabs (Events, Recent) - but NOT FindMatch
            if (name.Contains("Blade_Tab_Nav"))
            {
                // Exclude FindMatch nav tab - replaced by queue type subgroup entries
                if (name.Contains("FindMatch"))
                    return false;
                return true;
            }

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
            // Mailbox uses Blade_ListItem naming but is NOT a PlayBlade
            if (parentPath.Contains("Mailbox"))
                return false;

            // CampaignGraph (Color Challenge) uses PlayBlade layouts but is a content page, not a PlayBlade overlay
            if (parentPath.Contains("CampaignGraph"))
                return false;

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

            return false;
        }

        /// <summary>
        /// Check if element is inside a challenge screen container.
        /// Challenge containers are separate from PlayBlade - they get their own ChallengeMain group.
        /// </summary>
        private static bool IsChallengeContainer(string parentPath, string name)
        {
            // Challenge options spinners and settings
            // Note: Do NOT match "ChallengeWidget" - it matches ChallengeWidget_Base in the friends panel
            if (parentPath.Contains("ChallengeOptions") || parentPath.Contains("UnifiedChallenges"))
                return true;

            // Challenge play button and deck selection area
            if (parentPath.Contains("Popout_Play") || parentPath.Contains("FriendChallengeBladeWidget"))
                return true;

            // InviteFriendPopup (challenge invite dialog)
            if (parentPath.Contains("InviteFriendPopup"))
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
