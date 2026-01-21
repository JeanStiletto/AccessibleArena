using UnityEngine;
using UnityEngine.UI;
using MelonLoader;
using System.Collections.Generic;
using System.Linq;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Unified panel detection system that tracks visibility of menu panels, popups,
    /// and overlays using CanvasGroup alpha state comparison.
    ///
    /// Replaces complex cooldown/timer-based detection with simple state comparison:
    /// - Every N frames, scan for visible panels
    /// - Compare to previous state
    /// - If changed, report what appeared/disappeared
    ///
    /// Uses alpha thresholds at extremes (0.01/0.99) to detect only when animations
    /// are complete, avoiding false triggers during fade transitions.
    /// </summary>
    public class UnifiedPanelDetector
    {
        #region Configuration

        // Only detect visibility changes at animation endpoints
        // >= 0.99 = fully visible (fade-in complete)
        // <= 0.01 = fully hidden (fade-out complete)
        // Between = animating, don't change state
        private const float VisibleThreshold = 0.99f;
        private const float HiddenThreshold = 0.01f;
        private const int CheckIntervalFrames = 10;
        private const int CacheRefreshMultiplier = 6; // Refresh cache every 60 frames (~1 second at 60fps)
        private const float MinPanelSize = 100f;

        // Name patterns for UI elements we track via alpha detection
        // ONLY for panels without IsOpen property or Harmony patches
        // Do NOT include: SettingsMenu (has IsOpen), PlayBlade/Blade (Harmony patches, slide animation)
        // See docs/BEST_PRACTICES.md "Panel Detection Strategy" for decision tree
        private static readonly string[] TrackedPanelPatterns = new[]
        {
            "Popup", "SystemMessageView", "Dialog", "Modal",
            "FriendsWidget", "SocialUI", "InviteFriend"
        };

        #endregion

        #region State

        private readonly string _logPrefix;
        private int _frameCounter;

        // Panel tracking
        private readonly Dictionary<int, PanelInfo> _knownPanels = new Dictionary<int, PanelInfo>();
        // Note: _previouslyVisible removed - now tracking state per-panel with IsStableVisible
        private GameObject _topmostPanel;

        #endregion

        #region Public Types

        /// <summary>
        /// Information about a detected panel change.
        /// </summary>
        public class PanelChangeInfo
        {
            public bool HasChange { get; set; }
            public string AppearedPanelName { get; set; }
            public string DisappearedPanelName { get; set; }
            public GameObject TopmostPanel { get; set; }
        }

        private class PanelInfo
        {
            public GameObject GameObject { get; set; }
            public CanvasGroup CanvasGroup { get; set; }
            public string Name { get; set; }
            public int HierarchyDepth { get; set; }
            public bool IsPopup { get; set; }
            public bool WasVisible { get; set; } // Previous frame visibility
        }

        #endregion

        #region Constructor

        public UnifiedPanelDetector(string logPrefix = "UnifiedPanelDetector")
        {
            _logPrefix = logPrefix;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Reset all tracked state. Call on scene change.
        /// </summary>
        public void Reset()
        {
            _knownPanels.Clear();
            _topmostPanel = null;
            _frameCounter = 0;
        }

        /// <summary>
        /// Check for panel visibility changes. Call every frame from Update().
        /// Only performs actual check every N frames for performance.
        /// </summary>
        /// <returns>Change info if visibility changed, otherwise HasChange=false</returns>
        public PanelChangeInfo CheckForChanges()
        {
            _frameCounter++;

            // Only check every N frames
            if (_frameCounter % CheckIntervalFrames != 0)
            {
                return new PanelChangeInfo { HasChange = false, TopmostPanel = _topmostPanel };
            }

            // Refresh panel cache periodically or if empty
            if (_knownPanels.Count == 0 || _frameCounter % (CheckIntervalFrames * CacheRefreshMultiplier) == 0)
            {
                RefreshPanelCache();
            }

            // Track visibility changes
            var appeared = new List<int>();
            var disappeared = new List<int>();
            PanelInfo topmostInfo = null;
            int highestPriority = int.MinValue;

            foreach (var kvp in _knownPanels)
            {
                var info = kvp.Value;

                // Skip destroyed objects
                if (info.GameObject == null)
                    continue;

                // Check current visibility state
                bool isActive = info.GameObject.activeInHierarchy;

                // Get alpha from CanvasGroup if available
                // Don't force to 0 when inactive - let alpha be the source of truth
                // Some panels briefly become inactive during animations
                float currentAlpha = info.CanvasGroup != null
                    ? GetEffectiveAlpha(info.GameObject, info.CanvasGroup)
                    : (isActive ? 1f : -1f); // -1 means "unknown, skip hidden check"

                // Only detect state changes at animation endpoints
                // This avoids false triggers during fade transitions
                // Note: For "hidden" check, ONLY use alpha threshold, not activeInHierarchy
                // Some panels briefly become inactive during animations but come back
                // currentAlpha == -1 means no CanvasGroup and inactive - skip hidden check
                bool isFullyVisible = isActive && currentAlpha >= VisibleThreshold;
                bool isFullyHidden = currentAlpha >= 0 && currentAlpha <= HiddenThreshold;

                // Detect changes only at stable states
                if (isFullyVisible && !info.WasVisible)
                {
                    appeared.Add(kvp.Key);
                    info.WasVisible = true;
                    MelonLogger.Msg($"[{_logPrefix}] Panel now visible: {info.Name} (alpha={currentAlpha:F2})");
                }
                else if (isFullyHidden && info.WasVisible)
                {
                    disappeared.Add(kvp.Key);
                    info.WasVisible = false;
                }
                // Between thresholds = animating, don't change WasVisible

                // Track topmost among visible panels (use WasVisible since it reflects stable state)
                if (info.WasVisible)
                {
                    int priority = CalculatePanelPriority(info);
                    if (priority > highestPriority)
                    {
                        highestPriority = priority;
                        topmostInfo = info;
                    }
                }
            }

            bool hasChange = appeared.Count > 0 || disappeared.Count > 0;
            _topmostPanel = topmostInfo?.GameObject;

            // Build result
            var result = new PanelChangeInfo
            {
                HasChange = hasChange,
                TopmostPanel = _topmostPanel
            };

            if (hasChange)
            {
                // Get the most significant appeared panel (prefer popups, higher hierarchy depth)
                if (appeared.Count > 0)
                {
                    var appearInfo = appeared
                        .Select(id => _knownPanels.TryGetValue(id, out var info) ? info : null)
                        .Where(i => i != null)
                        .OrderByDescending(i => i.IsPopup ? 1 : 0)
                        .ThenByDescending(i => i.HierarchyDepth)
                        .FirstOrDefault();

                    if (appearInfo != null)
                    {
                        result.AppearedPanelName = CleanPanelName(appearInfo.Name);
                        MelonLogger.Msg($"[{_logPrefix}] Panel appeared: {appearInfo.Name} -> {result.AppearedPanelName}");
                    }
                }

                // Get the most significant disappeared panel
                if (disappeared.Count > 0)
                {
                    var disappearInfo = disappeared
                        .Select(id => _knownPanels.TryGetValue(id, out var info) ? info : null)
                        .Where(i => i != null)
                        .OrderByDescending(i => i.IsPopup ? 1 : 0)
                        .ThenByDescending(i => i.HierarchyDepth)
                        .FirstOrDefault();

                    if (disappearInfo != null)
                    {
                        result.DisappearedPanelName = CleanPanelName(disappearInfo.Name);
                        MelonLogger.Msg($"[{_logPrefix}] Panel disappeared: {disappearInfo.Name}");
                    }
                }

                MelonLogger.Msg($"[{_logPrefix}] Topmost panel: {_topmostPanel?.name ?? "none"}");
            }

            // Clean up destroyed panels
            CleanupDestroyedPanels();

            return result;
        }

        /// <summary>
        /// Get the current topmost visible panel.
        /// </summary>
        public GameObject GetTopmostVisiblePanel()
        {
            return _topmostPanel;
        }

        /// <summary>
        /// Force a refresh of the panel cache. Call on major screen changes.
        /// </summary>
        public void RefreshPanelCache()
        {
            // Search ALL GameObjects for popup patterns, not just those with CanvasGroup
            // The CanvasGroup might be on a child, not on the root popup object
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy)
                    continue;

                int id = go.GetInstanceID();

                // Skip if already known
                if (_knownPanels.ContainsKey(id))
                    continue;

                // Check if this matches a tracked panel pattern
                if (!IsTrackedPanel(go.name))
                    continue;

                // Must have "(Clone)" suffix (instantiated prefab)
                if (!go.name.EndsWith("(Clone)"))
                    continue;

                // Note: ExcludedNamePatterns check removed - if a panel matches TrackedPanelPatterns,
                // we want it regardless of containing "content", "controller", etc.
                // The TrackedPanelPatterns are explicit and take priority.

                // Must have at least one interactive child (button)
                if (!HasInteractiveChild(go))
                    continue;

                // Find CanvasGroup on this object or children
                var cg = go.GetComponent<CanvasGroup>();
                if (cg == null)
                    cg = go.GetComponentInChildren<CanvasGroup>();

                // Add to known panels (cg may be null, we'll check alpha differently)
                var info = new PanelInfo
                {
                    GameObject = go,
                    CanvasGroup = cg,
                    Name = go.name,
                    HierarchyDepth = GetHierarchyDepth(go.transform),
                    IsPopup = true // All panels we track are popups now
                };

                _knownPanels[id] = info;
                MelonLogger.Msg($"[{_logPrefix}] Registered popup panel: {go.name}");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Check if a GameObject is a valid panel candidate.
        /// Only tracks popup/overlay panels, not general UI elements.
        /// </summary>
        private bool IsPanelCandidate(GameObject go)
        {
            if (go == null)
                return false;

            // ONLY track panels that match tracked panel patterns
            // This prevents false positives from banners, cards, etc.
            if (!IsTrackedPanel(go.name))
                return false;

            // Note: ExcludedNamePatterns check removed - TrackedPanelPatterns take priority

            // Must have at least one interactive child (button)
            if (!HasInteractiveChild(go))
                return false;

            return true;
        }

        /// <summary>
        /// Check if the GameObject has any interactive child elements.
        /// </summary>
        private bool HasInteractiveChild(GameObject go)
        {
            // Check for Unity Button
            if (go.GetComponentInChildren<Button>(true) != null)
                return true;

            // Check for MTGA CustomButton
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName == "CustomButton" || typeName == "CustomButtonWithTooltip")
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Check if a panel name indicates a popup/overlay.
        /// </summary>
        private bool IsTrackedPanel(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            // Only match explicit tracked patterns, not just any clone
            foreach (var pattern in TrackedPanelPatterns)
            {
                if (name.Contains(pattern))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Get the effective alpha for a popup panel.
        /// Checks CanvasGroup on this object and children.
        /// </summary>
        private float GetEffectiveAlpha(GameObject go, CanvasGroup cg)
        {
            // If no CanvasGroup provided, try to find one
            if (cg == null)
            {
                cg = go.GetComponent<CanvasGroup>();
                if (cg == null)
                    cg = go.GetComponentInChildren<CanvasGroup>();
            }

            // If still no CanvasGroup, check if GameObject is active (assume visible)
            if (cg == null)
            {
                return go.activeInHierarchy ? 1f : 0f;
            }

            float alpha = cg.alpha;

            // Check parent CanvasGroups - if any parent is fully hidden, so is this
            Transform parent = go.transform.parent;
            while (parent != null)
            {
                var parentCg = parent.GetComponent<CanvasGroup>();
                if (parentCg != null && parentCg.alpha <= HiddenThreshold)
                {
                    return 0f; // Parent is invisible
                }
                parent = parent.parent;
            }

            return alpha;
        }

        /// <summary>
        /// Calculate panel priority for determining topmost.
        /// Higher priority = more on top.
        /// </summary>
        private int CalculatePanelPriority(PanelInfo info)
        {
            int priority = info.HierarchyDepth * 10;

            // Popups get significant priority boost
            if (info.IsPopup)
                priority += 1000;

            // Check Canvas sorting order if available
            var canvas = info.GameObject.GetComponentInParent<Canvas>();
            if (canvas != null)
                priority += canvas.sortingOrder * 100;

            return priority;
        }

        /// <summary>
        /// Get the depth of a transform in the hierarchy (root = 0).
        /// </summary>
        private int GetHierarchyDepth(Transform t)
        {
            int depth = 0;
            while (t.parent != null)
            {
                depth++;
                t = t.parent;
            }
            return depth;
        }

        /// <summary>
        /// Clean up references to destroyed GameObjects.
        /// </summary>
        private void CleanupDestroyedPanels()
        {
            var toRemove = _knownPanels
                .Where(kvp => kvp.Value.GameObject == null)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var id in toRemove)
            {
                _knownPanels.Remove(id);
            }
        }

        /// <summary>
        /// Clean up a panel name for user-friendly announcement.
        /// </summary>
        private string CleanPanelName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Panel";

            // Special case for SystemMessageView - it's a confirmation dialog
            if (name.Contains("SystemMessageView"))
                return "Confirmation";

            // Remove common suffixes
            string clean = name
                .Replace("(Clone)", "")
                .Replace("Popup", "")
                .Replace("_Desktop_16x9", "")
                .Replace("_", " ")
                .Trim();

            // Add spaces before capital letters (InviteFriend -> Invite Friend)
            clean = System.Text.RegularExpressions.Regex.Replace(clean, "([a-z])([A-Z])", "$1 $2");

            if (string.IsNullOrWhiteSpace(clean))
                return "Panel";

            return clean;
        }

        #endregion

        #region Static Utilities

        /// <summary>
        /// Check if a GameObject is a child of (or the same as) a parent GameObject.
        /// </summary>
        public static bool IsChildOf(GameObject child, GameObject parent)
        {
            if (child == null || parent == null)
                return false;

            Transform current = child.transform;
            Transform parentTransform = parent.transform;

            while (current != null)
            {
                if (current == parentTransform)
                    return true;
                current = current.parent;
            }

            return false;
        }

        #endregion
    }
}
