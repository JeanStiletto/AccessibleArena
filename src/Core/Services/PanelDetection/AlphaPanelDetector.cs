using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace AccessibleArena.Core.Services.PanelDetection
{
    /// <summary>
    /// Detector that uses CanvasGroup alpha to detect popup visibility.
    /// Polling-based detection for popups without controllers (pure CanvasGroup fade).
    ///
    /// Handles: SystemMessageView, Dialog, Modal, Popups (alpha-based visibility)
    /// </summary>
    public class AlphaPanelDetector
    {
        public string DetectorId => "AlphaDetector";

        private PanelStateManager _stateManager;
        private bool _initialized;

        #region Configuration

        private const float VisibleThreshold = 0.99f;
        private const float HiddenThreshold = 0.01f;
        private const int CheckIntervalFrames = 10;
        private const int CacheRefreshMultiplier = 6; // Refresh every 60 frames

        #endregion

        #region State

        private int _frameCounter;
        private readonly Dictionary<int, TrackedPanel> _knownPanels = new Dictionary<int, TrackedPanel>();
        private readonly HashSet<string> _announcedPanels = new HashSet<string>();

        private class TrackedPanel
        {
            public GameObject GameObject { get; set; }
            public CanvasGroup CanvasGroup { get; set; }
            public string Name { get; set; }
            public bool WasVisible { get; set; }
        }

        #endregion

        public void Initialize(PanelStateManager stateManager)
        {
            if (_initialized)
            {
                MelonLogger.Warning($"[{DetectorId}] Already initialized");
                return;
            }

            _stateManager = stateManager;
            _initialized = true;
            MelonLogger.Msg($"[{DetectorId}] Initialized");
        }

        public void Update()
        {
            if (_stateManager == null || !_initialized)
                return;

            _frameCounter++;
            if (_frameCounter % CheckIntervalFrames != 0)
                return;

            // Refresh cache periodically
            if (_knownPanels.Count == 0 || _frameCounter % (CheckIntervalFrames * CacheRefreshMultiplier) == 0)
            {
                RefreshPanelCache();
            }

            CheckForVisibilityChanges();
            CleanupDestroyedPanels();
        }

        public void Reset()
        {
            _knownPanels.Clear();
            _announcedPanels.Clear();
            _frameCounter = 0;
            MelonLogger.Msg($"[{DetectorId}] Reset");
        }

        #region Panel Ownership (Stage 5.3)

        /// <summary>
        /// OWNED PATTERNS - AlphaDetector is the authoritative detector for these panels.
        /// Detection method: Polling CanvasGroup alpha values (0.99/0.01 thresholds).
        ///
        /// Why Alpha: These panels fade in/out using CanvasGroup alpha and don't have
        /// IsOpen properties or patchable methods. They're typically instantiated prefabs
        /// with "(Clone)" suffix.
        ///
        /// Other detectors MUST exclude these patterns in their HandlesPanel() methods.
        /// </summary>
        public static readonly string[] OwnedPatterns = new[]
        {
            "systemmessageview", // Confirmation dialogs
            "dialog",           // Dialog popups
            "modal",            // Modal popups
            "invitefriend"      // Friend invite popup
        };

        /// <summary>
        /// Special case: "popup" (but NOT "popupbase") is also owned by AlphaDetector.
        /// PopupBase has IsOpen property and is handled by ReflectionDetector.
        /// </summary>
        private const string PopupPattern = "popup";
        private const string PopupBasePattern = "popupbase";

        #endregion

        public bool HandlesPanel(string panelName)
        {
            if (string.IsNullOrEmpty(panelName))
                return false;

            var lower = panelName.ToLowerInvariant();

            // DEFENSIVE: Exclude Harmony-owned patterns (should never match, but be safe)
            // This prevents accidental double-detection if a panel name somehow matches both
            foreach (var harmonyPattern in HarmonyPanelDetector.OwnedPatterns)
            {
                if (lower.Contains(harmonyPattern))
                    return false;
            }

            // Special case: "popup" but NOT "popupbase"
            // PopupBase has IsOpen property → ReflectionDetector
            // Other popups use alpha fade → AlphaDetector
            if (lower.Contains(PopupPattern) && !lower.Contains(PopupBasePattern))
                return true;

            // Check owned patterns
            foreach (var pattern in OwnedPatterns)
            {
                if (lower.Contains(pattern))
                    return true;
            }
            return false;
        }

        private void RefreshPanelCache()
        {
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy)
                    continue;

                int id = go.GetInstanceID();
                if (_knownPanels.ContainsKey(id))
                    continue;

                // Check if matches tracked patterns
                if (!IsTrackedPanel(go.name))
                    continue;

                // Must be a clone (instantiated prefab)
                if (!go.name.EndsWith("(Clone)"))
                    continue;

                // Must have interactive elements
                if (!HasInteractiveChild(go))
                    continue;

                // Find CanvasGroup
                var cg = go.GetComponent<CanvasGroup>() ?? go.GetComponentInChildren<CanvasGroup>();

                _knownPanels[id] = new TrackedPanel
                {
                    GameObject = go,
                    CanvasGroup = cg,
                    Name = go.name,
                    WasVisible = false
                };

                MelonLogger.Msg($"[{DetectorId}] Registered popup: {go.name}");
            }
        }

        private void CheckForVisibilityChanges()
        {
            foreach (var kvp in _knownPanels)
            {
                var panel = kvp.Value;
                if (panel.GameObject == null)
                    continue;

                bool isActive = panel.GameObject.activeInHierarchy;
                float currentAlpha = panel.CanvasGroup != null
                    ? GetEffectiveAlpha(panel.GameObject, panel.CanvasGroup)
                    : (isActive ? 1f : -1f);

                bool isFullyVisible = isActive && currentAlpha >= VisibleThreshold;
                bool isFullyHidden = currentAlpha >= 0 && currentAlpha <= HiddenThreshold;

                // Detect visibility changes at stable states only
                if (isFullyVisible && !panel.WasVisible)
                {
                    panel.WasVisible = true;

                    // Check if already announced to prevent duplicates
                    if (!_announcedPanels.Contains(panel.Name))
                    {
                        _announcedPanels.Add(panel.Name);
                        ReportPanelOpened(panel);
                    }
                }
                else if (isFullyHidden && panel.WasVisible)
                {
                    panel.WasVisible = false;
                    _announcedPanels.Remove(panel.Name);
                    ReportPanelClosed(panel);
                }
            }
        }

        private void ReportPanelOpened(TrackedPanel panel)
        {
            var panelInfo = new PanelInfo(
                panel.Name,
                PanelType.Popup,
                panel.GameObject,
                PanelDetectionMethod.Alpha
            );

            _stateManager.ReportPanelOpened(panelInfo);
            MelonLogger.Msg($"[{DetectorId}] Reported popup opened: {panel.Name}");
        }

        private void ReportPanelClosed(TrackedPanel panel)
        {
            _stateManager.ReportPanelClosed(panel.GameObject);
            MelonLogger.Msg($"[{DetectorId}] Reported popup closed: {panel.Name}");
        }

        private bool IsTrackedPanel(string name)
        {
            // Reuse HandlesPanel logic
            return HandlesPanel(name);
        }

        private bool HasInteractiveChild(GameObject go)
        {
            if (go.GetComponentInChildren<Button>(true) != null)
                return true;

            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null)
                    continue;

                string typeName = mb.GetType().Name;
                if (typeName == "CustomButton" || typeName == "CustomButtonWithTooltip")
                    return true;
            }

            return false;
        }

        private float GetEffectiveAlpha(GameObject go, CanvasGroup cg)
        {
            if (cg == null)
            {
                cg = go.GetComponent<CanvasGroup>() ?? go.GetComponentInChildren<CanvasGroup>();
            }

            if (cg == null)
                return go.activeInHierarchy ? 1f : 0f;

            float alpha = cg.alpha;

            // Check parent CanvasGroups
            Transform parent = go.transform.parent;
            while (parent != null)
            {
                var parentCg = parent.GetComponent<CanvasGroup>();
                if (parentCg != null && parentCg.alpha <= HiddenThreshold)
                    return 0f;

                parent = parent.parent;
            }

            return alpha;
        }

        private void CleanupDestroyedPanels()
        {
            var toRemove = _knownPanels
                .Where(kvp => kvp.Value.GameObject == null)
                .ToList();

            foreach (var kvp in toRemove)
            {
                var panel = kvp.Value;

                // Report as closed if it was visible - don't silently remove
                if (panel.WasVisible)
                {
                    MelonLogger.Msg($"[{DetectorId}] Popup destroyed while visible, reporting closed: {panel.Name}");
                    _stateManager.ReportPanelClosedByName(panel.Name);
                    _announcedPanels.Remove(panel.Name);
                }

                _knownPanels.Remove(kvp.Key);
            }
        }
    }
}
