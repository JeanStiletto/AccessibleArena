using UnityEngine;
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

            // 3. Social/Friends panel
            if (_screenDetector.IsSocialPanelOpen())
                return ElementGroup.Social;

            // 4. Play blade expanded (return PlayBladeTabs as marker that PlayBlade is active)
            if (PanelStateManager.Instance?.IsPlayBladeActive == true)
                return ElementGroup.PlayBladeTabs;

            // 5. NPE (New Player Experience) overlay
            if (_screenDetector.IsNPERewardsScreenActive())
                return ElementGroup.NPE;

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
                ElementGroup.Social => IsInsideSocialPanel(obj),
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
        /// Check if an element is inside the play blade.
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
    }
}
