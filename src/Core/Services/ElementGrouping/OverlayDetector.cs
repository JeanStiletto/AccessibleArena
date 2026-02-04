using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Services.PanelDetection;

namespace AccessibleArena.Core.Services.ElementGrouping
{
    /// <summary>
    /// Simplified overlay detection that replaces the complex ForegroundLayer system.
    /// Detects which overlay (if any) is currently active and should suppress other groups.
    /// </summary>
    public class OverlayDetector
    {
        private readonly MenuScreenDetector _screenDetector;

        /// <summary>
        /// Get the current foreground panel from PanelStateManager.
        /// This is queried fresh each time to stay in sync with the panel state.
        /// </summary>
        private GameObject ForegroundPanel => PanelStateManager.Instance?.GetFilterPanel();

        public OverlayDetector(MenuScreenDetector screenDetector)
        {
            _screenDetector = screenDetector;
        }

        /// <summary>
        /// Get the currently active overlay group, if any.
        /// Returns null if no overlay is active (normal navigation mode).
        /// </summary>
        public ElementGroup? GetActiveOverlay()
        {
            // Priority order - first match wins (highest priority first)
            var foregroundPanel = ForegroundPanel;

            // 1. Popup dialogs (highest priority)
            if (foregroundPanel != null && IsPopupPanel(foregroundPanel))
                return ElementGroup.Popup;

            // 2. Settings menu
            if (_screenDetector.CheckSettingsMenuOpen())
                return ElementGroup.SettingsMenu;

            // 3. Friends panel overlay
            if (_screenDetector.IsSocialPanelOpen())
                return ElementGroup.FriendsPanel;

            // 4. Rewards popup (after claiming rewards from mail/store/etc.)
            // Must be checked BEFORE mailbox, since rewards can appear while mailbox is still open
            if (IsRewardsPopupOpen())
                return ElementGroup.RewardsPopup;

            // 5. Mailbox panel overlay - distinguish between list and content view
            if (_screenDetector.IsMailboxOpen())
            {
                // Check if mail content is visible (a specific mail is opened)
                if (IsMailContentVisible())
                    return ElementGroup.MailboxContent;
                return ElementGroup.MailboxList;
            }

            // 7. Play blade expanded (return PlayBladeTabs as marker that PlayBlade is active)
            if (PanelStateManager.Instance?.IsPlayBladeActive == true)
                return ElementGroup.PlayBladeTabs;

            // 8. NPE (New Player Experience) overlay
            if (_screenDetector.IsNPERewardsScreenActive())
                return ElementGroup.NPE;

            // Note: DeckBuilderCollection is NOT an overlay - it's a group within the deck builder.
            // If we made it an overlay, it would filter out save buttons, filters, etc.
            // Instead, it's just added to groupOrder so cards get properly grouped.

            // No overlay active
            return null;
        }

        /// <summary>
        /// Check if a panel is a popup/dialog overlay.
        /// </summary>
        private static bool IsPopupPanel(GameObject obj)
        {
            if (obj == null) return false;
            string name = obj.name;
            return name.Contains("Popup") || name.Contains("SystemMessageView");
        }

        /// <summary>
        /// Check if the given GameObject belongs to the currently active overlay.
        /// Used to filter elements - only elements inside the active overlay should be visible.
        /// </summary>
        public bool IsInsideActiveOverlay(GameObject obj)
        {
            var overlay = GetActiveOverlay();
            if (overlay == null) return true; // No overlay active, everything is visible

            return overlay switch
            {
                ElementGroup.Popup => IsInsidePopup(obj),
                ElementGroup.SettingsMenu => IsInsideSettingsMenu(obj),
                ElementGroup.FriendsPanel => IsInsideSocialPanel(obj),
                ElementGroup.MailboxList => IsInsideMailboxList(obj),
                ElementGroup.MailboxContent => IsInsideMailboxContent(obj),
                ElementGroup.RewardsPopup => IsInsideRewardsPopup(obj),
                ElementGroup.PlayBladeTabs => IsInsidePlayBlade(obj),
                ElementGroup.PlayBladeContent => IsInsidePlayBlade(obj),
                ElementGroup.NPE => IsInsideNPEOverlay(obj),
                _ => true
            };
        }

        /// <summary>
        /// Check if an element is inside a popup dialog.
        /// </summary>
        private bool IsInsidePopup(GameObject obj)
        {
            var foregroundPanel = ForegroundPanel;
            return foregroundPanel != null && MenuPanelTracker.IsChildOf(obj, foregroundPanel);
        }

        /// <summary>
        /// Check if an element is inside the settings menu.
        /// </summary>
        private bool IsInsideSettingsMenu(GameObject obj)
        {
            var settingsPanel = _screenDetector.SettingsContentPanel;
            return settingsPanel != null && MenuPanelTracker.IsChildOf(obj, settingsPanel);
        }

        /// <summary>
        /// Check if an element is inside the social/friends panel.
        /// </summary>
        private bool IsInsideSocialPanel(GameObject obj)
        {
            var socialPanel = GameObject.Find("SocialUI_V2_Desktop_16x9(Clone)");
            return socialPanel != null && MenuPanelTracker.IsChildOf(obj, socialPanel);
        }

        /// <summary>
        /// Check if mail content is visible (a specific mail is opened).
        /// Detects by checking for actual mail content elements, not scroll view infrastructure.
        /// </summary>
        private bool IsMailContentVisible()
        {
            var mailboxPanel = GameObject.Find("ContentController - Mailbox_Base(Clone)");
            if (mailboxPanel == null)
                return false;

            // Find the content view area
            var contentView = mailboxPanel.transform.Find("SafeArea/ViewSection/Mailbox_ContentView");
            if (contentView == null)
                return false;

            // Look for actual mail content buttons (not Viewport scroll infrastructure)
            // Mail content has buttons like "Mehr Informationen", "Einfordern" (Claim), Button_Base
            foreach (var comp in contentView.GetComponentsInChildren<Component>(false))
            {
                if (comp == null || !comp.gameObject.activeInHierarchy)
                    continue;

                if (comp.GetType().Name == "CustomButton")
                {
                    string name = comp.gameObject.name;
                    // Skip Viewport - it's always present for scrolling
                    if (name == "Viewport")
                        continue;

                    // Found an actual content button
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if an element is inside the mailbox mail list (left pane).
        /// </summary>
        private bool IsInsideMailboxList(GameObject obj)
        {
            if (obj == null) return false;

            // Check if element is inside the blade list container (mail list)
            Transform current = obj.transform;
            while (current != null)
            {
                string name = current.name;
                // Mail list is in BladeView_CONTAINER
                if (name.Contains("BladeView_CONTAINER") || name.Contains("Blade_ListItem"))
                    return true;
                // Stop if we hit the content view (wrong pane)
                if (name.Contains("Mailbox_ContentView") || name.Contains("ViewSection"))
                    return false;
                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// Check if an element is inside the mailbox content view (right pane).
        /// </summary>
        private bool IsInsideMailboxContent(GameObject obj)
        {
            if (obj == null) return false;

            // Check if element is inside the content view (mail details)
            Transform current = obj.transform;
            while (current != null)
            {
                string name = current.name;
                // Mail content is in Mailbox_ContentView
                if (name.Contains("Mailbox_ContentView"))
                    return true;
                // Stop if we hit the blade list (wrong pane)
                if (name.Contains("BladeView_CONTAINER") || name.Contains("Blade_ListItem"))
                    return false;
                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// Check if an element is inside the play blade.
        /// Also includes main button from event pages (CampaignGraph) since they coexist with the blade.
        /// </summary>
        private bool IsInsidePlayBlade(GameObject obj)
        {
            Transform current = obj.transform;

            while (current != null)
            {
                string name = current.name;

                // Direct blade containers
                if (name.Contains("PlayBlade") || name.Contains("Blade_"))
                    return true;

                // Blade content markers
                if (name.Contains("BladeContent") || name.Contains("BladeContainer"))
                    return true;

                // Filter list items in play blade
                if (name.Contains("FilterListItem") && current.parent != null &&
                    current.parent.name.Contains("Blade"))
                    return true;

                // Event page main button (Play button on Color Challenge, etc.)
                // These coexist with the blade and should be navigable
                if (name.Contains("CampaignGraphMainButtonModule"))
                    return true;

                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// Check if an element is inside the NPE (New Player Experience) overlay.
        /// </summary>
        private static bool IsInsideNPEOverlay(GameObject obj)
        {
            if (obj == null) return false;

            Transform current = obj.transform;
            while (current != null)
            {
                if (current.name.Contains("NPE") || current.name.Contains("NewPlayerExperience"))
                    return true;
                current = current.parent;
            }

            return false;
        }

        // Cache to avoid logging spam - only log when state changes
        private bool _lastRewardsPopupState = false;

        /// <summary>
        /// Check if the rewards popup is currently open.
        /// The rewards popup appears after claiming rewards from mail, store, or other sources.
        /// Path: Canvas - Screenspace Popups/ContentController - Rewards_Desktop_16x9(Clone)
        /// </summary>
        public bool IsRewardsPopupOpen()
        {
            bool result = CheckRewardsPopupOpenInternal();

            // Only log when state changes to reduce spam
            if (result != _lastRewardsPopupState)
            {
                _lastRewardsPopupState = result;
                MelonLogger.Msg($"[OverlayDetector] IsRewardsPopupOpen changed to: {result}");
            }

            return result;
        }

        private bool CheckRewardsPopupOpenInternal()
        {
            // Look for the rewards controller in Screenspace Popups canvas
            var screenspacePopups = GameObject.Find("Canvas - Screenspace Popups");
            if (screenspacePopups == null)
                return false;

            // Find the rewards controller - it should have ContentController and Rewards in its name
            foreach (Transform child in screenspacePopups.transform)
            {
                if (child.name.Contains("ContentController") && child.name.Contains("Rewards") &&
                    child.gameObject.activeInHierarchy)
                {
                    // Check for Container child
                    var container = child.Find("Container");
                    if (container == null)
                    {
                        // Try searching deeper in case Container is nested
                        foreach (Transform t in child.GetComponentsInChildren<Transform>(true))
                        {
                            if (t.name == "Container")
                            {
                                container = t;
                                break;
                            }
                        }
                    }

                    if (container == null)
                    {
                        // Fallback: if controller is active and has any active children with reward prefabs, consider it open
                        foreach (Transform t in child.GetComponentsInChildren<Transform>(true))
                        {
                            if (t.gameObject.activeInHierarchy && t.name.Contains("RewardPrefab"))
                                return true;
                        }
                        continue;
                    }

                    // Check for RewardsCONTAINER or Buttons
                    var rewardsContainer = container.Find("RewardsCONTAINER");
                    var buttons = container.Find("Buttons");

                    bool rewardsActive = rewardsContainer != null && rewardsContainer.gameObject.activeInHierarchy;
                    bool buttonsActive = buttons != null && buttons.gameObject.activeInHierarchy;

                    if (rewardsActive || buttonsActive)
                        return true;

                    // Additional fallback: check for any RewardPrefab children directly
                    foreach (Transform t in child.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.gameObject.activeInHierarchy && t.name.Contains("RewardPrefab"))
                            return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Get the rewards container transform for element discovery.
        /// Returns null if rewards popup is not open.
        /// </summary>
        public Transform GetRewardsContainer()
        {
            var screenspacePopups = GameObject.Find("Canvas - Screenspace Popups");
            if (screenspacePopups == null)
                return null;

            foreach (Transform child in screenspacePopups.transform)
            {
                if (child.name.Contains("ContentController") && child.name.Contains("Rewards") &&
                    child.gameObject.activeInHierarchy)
                {
                    var container = child.Find("Container");
                    if (container != null)
                    {
                        var rewardsContainer = container.Find("RewardsCONTAINER");
                        if (rewardsContainer != null && rewardsContainer.gameObject.activeInHierarchy)
                            return rewardsContainer;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Check if an element is inside the rewards popup.
        /// </summary>
        private bool IsInsideRewardsPopup(GameObject obj)
        {
            if (obj == null) return false;

            Transform current = obj.transform;
            while (current != null)
            {
                string name = current.name;
                // The rewards popup is under ContentController - Rewards_*
                if (name.Contains("ContentController") && name.Contains("Rewards"))
                    return true;
                // Also check for parent canvas
                if (name == "Canvas - Screenspace Popups")
                {
                    // We reached the canvas but didn't find the rewards controller above us
                    // This means we're in a sibling element (not inside rewards popup)
                    return false;
                }
                current = current.parent;
            }

            return false;
        }
    }
}
