using AccessibleArena.Core.Models;
using System.Collections.Generic;
using UnityEngine;

namespace AccessibleArena.Core.Services.PanelDetection
{
    /// <summary>
    /// Information about an active panel.
    /// Provides canonical naming and behavior flags.
    /// Also contains static utility methods for panel classification.
    /// </summary>
    public class PanelInfo
    {
        #region Static Panel Metadata

        /// <summary>
        /// Classifies a panel name into a PanelType.
        /// </summary>
        public static PanelType ClassifyPanel(string panelName)
        {
            if (string.IsNullOrEmpty(panelName))
                return PanelType.None;

            var lower = panelName.ToLowerInvariant();

            if (lower.Contains("panel -") || lower.Contains("loginpanel") || lower.Contains("welcomepanel"))
                return PanelType.Login;
            if (lower.Contains("settings"))
                return PanelType.Settings;
            if (lower.Contains("blade"))
                return PanelType.Blade;
            if (lower.Contains("social") || lower.Contains("friend"))
                return PanelType.Social;
            if (lower.Contains("campaign") || lower.Contains("colorchallenge"))
                return PanelType.Campaign;
            if (lower.Contains("systemmessage") || lower.Contains("dialog") ||
                lower.Contains("modal") || lower.Contains("popup"))
                return PanelType.Popup;

            return PanelType.ContentPanel;
        }

        /// <summary>
        /// Priority for panel stacking. Higher priority panels overlay lower priority ones.
        /// </summary>
        public static int GetPriority(PanelType type)
        {
            return type switch
            {
                PanelType.Popup => 1000,
                PanelType.Settings => 500,
                PanelType.Social => 400,
                PanelType.Blade => 300,
                PanelType.Campaign => 250,
                PanelType.Login => 200,
                PanelType.ContentPanel => 100,
                PanelType.None => 0,
                _ => 0
            };
        }

        /// <summary>
        /// Determines if a panel should filter navigation to only its children.
        /// </summary>
        public static bool ShouldFilterNavigation(PanelType type, string panelName)
        {
            if (!string.IsNullOrEmpty(panelName))
            {
                var lower = panelName.ToLowerInvariant();
                if (lower.Contains("homepage")) return false;
                if (lower.Contains("socialui")) return false;
                if (lower.Contains("settings")) return true;
                if (lower.Contains("friend")) return true;
                if (lower.Contains("popup") || lower.Contains("dialog") || lower.Contains("modal")) return true;
                if (lower.Contains("systemmessage")) return true;
            }

            return type switch
            {
                PanelType.Settings => true,
                PanelType.Social => false,
                PanelType.Popup => true,
                PanelType.Blade => true,
                PanelType.Campaign => false,
                PanelType.Login => true,
                PanelType.ContentPanel => false,
                PanelType.None => false,
                _ => false
            };
        }

        /// <summary>
        /// User-friendly display name for announcements.
        /// </summary>
        public static string GetDisplayName(string panelName, PanelType type)
        {
            if (string.IsNullOrEmpty(panelName))
                return type.ToString();

            var lower = panelName.ToLowerInvariant();

            if (lower.Contains("systemmessage")) return Strings.ScreenConfirmation;
            if (lower.Contains("invitefriend")) return Strings.ScreenInviteFriend;
            if (lower.Contains("friendswidget")) return Strings.ScreenFriends;
            if (lower.Contains("socialui")) return Strings.ScreenSocial;
            if (lower.Contains("settingsmenu")) return Strings.ScreenSettings;
            if (lower.Contains("playblade")) return Strings.ScreenPlay;
            if (lower.Contains("eventblade")) return Strings.ScreenEvents;
            if (lower.Contains("deckselectblade")) return Strings.ScreenDeckSelection;
            if (lower.Contains("findmatchblade")) return Strings.ScreenFindMatch;
            if (lower.Contains("campaigngraph")) return Strings.ScreenColorChallenge;

            if (panelName.StartsWith("Panel - "))
                return panelName.Substring("Panel - ".Length).Replace("(Clone)", "").Trim();

            var display = panelName
                .Replace("ContentController", "")
                .Replace("Controller", "")
                .Replace("(Clone)", "")
                .Replace("_Desktop_16x9", "")
                .Replace("_Desktop", "")
                .Trim();

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

        /// <summary>
        /// Panels that should never trigger rescans or be tracked.
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

        /// <summary>
        /// Canonical name for the panel (e.g., "Settings", "PlayBlade", "Friends").
        /// Used for comparison and logging.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The type of panel, determining its behavior.
        /// </summary>
        public PanelType Type { get; }

        /// <summary>
        /// The actual Unity GameObject for the panel.
        /// Used for element filtering (IsChildOf checks).
        /// </summary>
        public GameObject GameObject { get; }

        /// <summary>
        /// Priority for panel stacking. Higher = more foreground.
        /// Popups (1000) > Settings (500) > Social (400) > Blade (300) > etc.
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// Whether this panel should filter navigation to only its children.
        /// True for overlays (Settings, Social, Popups), false for content panels (HomePage).
        /// </summary>
        public bool FiltersNavigation { get; }

        /// <summary>
        /// User-friendly name for announcements.
        /// Converts internal names to readable format (e.g., "SystemMessageView" -> "Confirmation").
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Which detection method discovered this panel.
        /// Useful for debugging and ensuring no duplicate detection.
        /// </summary>
        public PanelDetectionMethod DetectedBy { get; }

        /// <summary>
        /// Raw GameObject name (for debugging).
        /// </summary>
        public string RawGameObjectName => GameObject?.name ?? "null";

        public PanelInfo(
            string name,
            PanelType type,
            GameObject gameObject,
            PanelDetectionMethod detectedBy)
        {
            Name = name;
            Type = type;
            GameObject = gameObject;
            DetectedBy = detectedBy;
            Priority = GetPriority(type);
            FiltersNavigation = ShouldFilterNavigation(type, name);
            DisplayName = GetDisplayName(name, type);
        }

        /// <summary>
        /// Check if this panel is still valid (GameObject exists and is active).
        /// </summary>
        public bool IsValid => GameObject != null && GameObject.activeInHierarchy;

        public override string ToString()
        {
            return $"PanelInfo({Name}, {Type}, Priority={Priority}, Filters={FiltersNavigation}, Valid={IsValid})";
        }

        public override bool Equals(object obj)
        {
            if (obj is PanelInfo other)
            {
                // Two PanelInfos are equal if they refer to the same GameObject
                return GameObject == other.GameObject;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return GameObject?.GetHashCode() ?? 0;
        }
    }
}
