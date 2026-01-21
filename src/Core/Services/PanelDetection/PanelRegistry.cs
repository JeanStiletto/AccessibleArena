using System.Collections.Generic;

namespace AccessibleArena.Core.Services.PanelDetection
{
    /// <summary>
    /// Centralized knowledge about MTGA panels.
    /// Consolidates all special-case handling that was previously scattered throughout the code.
    /// </summary>
    public static class PanelRegistry
    {
        #region Detection Method Assignment

        /// <summary>
        /// Determines which detection system should handle a panel.
        /// Each panel should be detected by exactly ONE system to prevent duplicates.
        /// </summary>
        public static PanelDetectionMethod GetDetectionMethod(string panelName)
        {
            if (string.IsNullOrEmpty(panelName))
                return PanelDetectionMethod.Reflection;

            var lower = panelName.ToLowerInvariant();

            // HARMONY - Event-driven via property setters
            // These panels have patchable setters and/or use animations that alpha detection can't handle
            if (lower.Contains("playblade")) return PanelDetectionMethod.Harmony;  // CRITICAL: slide animation
            if (lower.Contains("settings")) return PanelDetectionMethod.Harmony;
            if (lower.Contains("socialui")) return PanelDetectionMethod.Harmony;
            if (lower.Contains("friendswidget")) return PanelDetectionMethod.Harmony;
            if (lower.Contains("eventblade")) return PanelDetectionMethod.Harmony;
            if (lower.Contains("findmatchblade")) return PanelDetectionMethod.Harmony;
            if (lower.Contains("deckselectblade")) return PanelDetectionMethod.Harmony;
            if (lower.Contains("bladecontentview")) return PanelDetectionMethod.Harmony;

            // ALPHA - CanvasGroup alpha polling
            // These panels fade in/out and don't have IsOpen properties
            if (lower.Contains("systemmessage")) return PanelDetectionMethod.Alpha;
            if (lower.Contains("dialog")) return PanelDetectionMethod.Alpha;
            if (lower.Contains("modal")) return PanelDetectionMethod.Alpha;
            if (lower.Contains("popup") && !lower.Contains("popupbase")) return PanelDetectionMethod.Alpha;
            if (lower.Contains("invitefriend")) return PanelDetectionMethod.Alpha;

            // REFLECTION - IsOpen property polling
            // Everything else (NavContentController descendants, Login panels, etc.)
            return PanelDetectionMethod.Reflection;
        }

        #endregion

        #region Panel Type Classification

        /// <summary>
        /// Classifies a panel name into a PanelType.
        /// </summary>
        public static PanelType ClassifyPanel(string panelName)
        {
            if (string.IsNullOrEmpty(panelName))
                return PanelType.None;

            var lower = panelName.ToLowerInvariant();

            // Login scene panels
            if (lower.Contains("panel -") || lower.Contains("loginpanel") || lower.Contains("welcomepanel"))
                return PanelType.Login;

            // Settings menu
            if (lower.Contains("settings"))
                return PanelType.Settings;

            // Blades (PlayBlade, EventBlade, etc.)
            if (lower.Contains("blade"))
                return PanelType.Blade;

            // Social/Friends
            if (lower.Contains("social") || lower.Contains("friend"))
                return PanelType.Social;

            // Campaign / Color Challenge
            if (lower.Contains("campaign") || lower.Contains("colorchallenge"))
                return PanelType.Campaign;

            // Popups (SystemMessageView, dialogs, modals)
            if (lower.Contains("systemmessage") || lower.Contains("dialog") ||
                lower.Contains("modal") || lower.Contains("popup"))
                return PanelType.Popup;

            // Default: Content panel
            return PanelType.ContentPanel;
        }

        #endregion

        #region Priority

        /// <summary>
        /// Priority for panel stacking. Higher priority panels overlay lower priority ones.
        /// </summary>
        public static int GetPriority(PanelType type)
        {
            return type switch
            {
                PanelType.Popup => 1000,       // Popups always on top
                PanelType.Settings => 500,    // Settings overlays everything except popups
                PanelType.Social => 400,      // Social panel
                PanelType.Blade => 300,       // Play blade, event blade, etc.
                PanelType.Campaign => 250,    // Color Challenge panels
                PanelType.Login => 200,       // Login scene panels
                PanelType.ContentPanel => 100,// Normal content
                PanelType.None => 0,
                _ => 0
            };
        }

        #endregion

        #region Navigation Filtering

        /// <summary>
        /// Determines if a panel should filter navigation to only its children.
        /// When true, elements outside this panel are hidden from navigation.
        /// </summary>
        public static bool FiltersNavigation(PanelType type, string panelName)
        {
            // Check specific panel names first
            if (!string.IsNullOrEmpty(panelName))
            {
                var lower = panelName.ToLowerInvariant();

                // HomePage is always present, never filters
                if (lower.Contains("homepage")) return false;

                // These specific panels DO filter
                if (lower.Contains("settings")) return true;
                if (lower.Contains("social") || lower.Contains("friend")) return true;
                if (lower.Contains("popup") || lower.Contains("dialog") || lower.Contains("modal")) return true;
                if (lower.Contains("systemmessage")) return true;
            }

            // Default by type
            return type switch
            {
                PanelType.Settings => true,
                PanelType.Social => true,
                PanelType.Popup => true,
                PanelType.Blade => true,      // PlayBlade filters to show only deck selection
                PanelType.Campaign => false,  // Campaign shows along with PlayBlade
                PanelType.Login => true,      // Login panels are the only thing on screen
                PanelType.ContentPanel => false,
                PanelType.None => false,
                _ => false
            };
        }

        #endregion

        #region Display Names

        /// <summary>
        /// User-friendly display name for announcements.
        /// </summary>
        public static string GetDisplayName(string panelName, PanelType type)
        {
            if (string.IsNullOrEmpty(panelName))
                return type.ToString();

            // Specific name mappings
            var lower = panelName.ToLowerInvariant();

            if (lower.Contains("systemmessage")) return "Confirmation";
            if (lower.Contains("invitefriend")) return "Invite Friend";
            if (lower.Contains("friendswidget")) return "Friends";
            if (lower.Contains("socialui")) return "Social";
            if (lower.Contains("settingsmenu")) return "Settings";
            if (lower.Contains("playblade")) return "Play";
            if (lower.Contains("eventblade")) return "Events";
            if (lower.Contains("deckselectblade")) return "Deck Selection";
            if (lower.Contains("findmatchblade")) return "Find Match";
            if (lower.Contains("campaigngraph")) return "Color Challenge";

            // Login panels - extract name
            if (panelName.StartsWith("Panel - "))
                return panelName.Substring("Panel - ".Length).Replace("(Clone)", "").Trim();

            // Remove common suffixes
            var display = panelName
                .Replace("ContentController", "")
                .Replace("Controller", "")
                .Replace("(Clone)", "")
                .Replace("_Desktop_16x9", "")
                .Replace("_Desktop", "")
                .Trim();

            // Add spaces before capitals
            return AddSpacesToCamelCase(display);
        }

        private static string AddSpacesToCamelCase(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var result = new System.Text.StringBuilder();
            result.Append(text[0]);

            for (int i = 1; i < text.Length; i++)
            {
                if (char.IsUpper(text[i]) && !char.IsUpper(text[i - 1]))
                    result.Append(' ');
                result.Append(text[i]);
            }

            return result.ToString();
        }

        #endregion

        #region Panel Name Extraction

        /// <summary>
        /// Extracts canonical panel name from a GameObject name.
        /// Removes suffixes like (Clone), _Desktop_16x9, etc.
        /// </summary>
        public static string ExtractCanonicalName(string gameObjectName)
        {
            if (string.IsNullOrEmpty(gameObjectName))
                return string.Empty;

            var name = gameObjectName
                .Replace("(Clone)", "")
                .Replace("_Desktop_16x9", "")
                .Replace("_Desktop", "")
                .Replace("ContentController - ", "")
                .Replace("ContentController", "")
                .Trim();

            // Remove trailing numbers and whitespace
            name = System.Text.RegularExpressions.Regex.Replace(name, @"\s*\d+$", "");

            return name;
        }

        #endregion

        #region Ignore Lists

        /// <summary>
        /// Panels that should never trigger rescans or be tracked.
        /// These are structural/always-present elements that would cause spurious events.
        /// NOTE: HomePage is NOT ignored - it triggers rescan on load but doesn't filter navigation.
        /// </summary>
        private static readonly HashSet<string> IgnoredPanels = new HashSet<string>(
            System.StringComparer.OrdinalIgnoreCase)
        {
            "NavBar",
            "TopBar"
        };

        /// <summary>
        /// Check if a panel should be ignored entirely.
        /// </summary>
        public static bool ShouldIgnorePanel(string panelName)
        {
            if (string.IsNullOrEmpty(panelName))
                return true;

            foreach (var ignored in IgnoredPanels)
            {
                if (panelName.IndexOf(ignored, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        #endregion
    }
}
