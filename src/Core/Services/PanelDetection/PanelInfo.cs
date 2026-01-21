using UnityEngine;

namespace AccessibleArena.Core.Services.PanelDetection
{
    /// <summary>
    /// Information about an active panel.
    /// Provides canonical naming and behavior flags.
    /// </summary>
    public class PanelInfo
    {
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
            Priority = PanelRegistry.GetPriority(type);
            FiltersNavigation = PanelRegistry.FiltersNavigation(type, name);
            DisplayName = PanelRegistry.GetDisplayName(name, type);
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
