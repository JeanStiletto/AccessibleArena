using UnityEngine;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Diagnostic tool to track panel animation states vs alpha changes.
    /// Use F11 to dump current panel state and start/stop tracking.
    ///
    /// Purpose: Determine if we can unify panel detection on animation lifecycle
    /// methods (FinishOpen/FinishClose) instead of alpha polling.
    /// </summary>
    public class PanelAnimationDiagnostic
    {
        #region Configuration

        private const float AlphaThreshold = 0.5f;
        private const int TrackingIntervalFrames = 5; // Check every 5 frames for fine-grained tracking

        // Patterns to find popup/overlay panels
        private static readonly string[] PanelPatterns = new[]
        {
            "Popup", "SystemMessageView", "Dialog", "Modal", "SettingsMenu",
            "FriendsWidget", "SocialUI", "InviteFriend", "Overlay"
        };

        #endregion

        #region State

        private bool _isTracking;
        private int _frameCounter;
        private readonly Dictionary<int, TrackedPanel> _trackedPanels = new Dictionary<int, TrackedPanel>();
        private float _trackingStartTime;

        #endregion

        #region Tracked Panel Data

        private class TrackedPanel
        {
            public GameObject GameObject { get; set; }
            public string Name { get; set; }
            public string BaseClasses { get; set; }
            public bool HasFinishOpen { get; set; }
            public bool HasFinishClose { get; set; }
            public bool HasIsOpen { get; set; }
            public bool HasIsReadyToShow { get; set; }
            public bool HasAnimator { get; set; }
            public CanvasGroup CanvasGroup { get; set; }
            public Component Animator { get; set; }

            // Tracking data
            public List<StateSnapshot> Snapshots { get; } = new List<StateSnapshot>();
            public float? FirstVisibleTime { get; set; }
            public float? AnimationCompleteTime { get; set; }
            public float? AlphaStableTime { get; set; }
        }

        private class StateSnapshot
        {
            public float Time { get; set; }
            public float Alpha { get; set; }
            public bool IsAnimatorInTransition { get; set; }
            public float NormalizedTime { get; set; }
            public bool IsActiveInHierarchy { get; set; }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Toggle tracking on/off and dump current panel analysis.
        /// </summary>
        public void ToggleTracking()
        {
            if (_isTracking)
            {
                StopTrackingAndReport();
            }
            else
            {
                StartTracking();
            }
        }

        /// <summary>
        /// Call every frame from Update() when tracking is active.
        /// </summary>
        public void Update()
        {
            if (!_isTracking) return;

            _frameCounter++;
            if (_frameCounter % TrackingIntervalFrames != 0) return;

            float currentTime = Time.time - _trackingStartTime;

            foreach (var kvp in _trackedPanels)
            {
                var panel = kvp.Value;
                if (panel.GameObject == null) continue;

                var snapshot = new StateSnapshot
                {
                    Time = currentTime,
                    IsActiveInHierarchy = panel.GameObject.activeInHierarchy,
                    Alpha = GetAlpha(panel),
                    IsAnimatorInTransition = GetAnimatorInTransition(panel),
                    NormalizedTime = GetAnimatorNormalizedTime(panel)
                };

                panel.Snapshots.Add(snapshot);

                // Track first visible time
                if (!panel.FirstVisibleTime.HasValue && snapshot.Alpha >= AlphaThreshold)
                {
                    panel.FirstVisibleTime = currentTime;
                    MelonLogger.Msg($"[Diagnostic] {panel.Name} became visible at {currentTime:F3}s (alpha={snapshot.Alpha:F2})");
                }

                // Track animation complete time
                if (!panel.AnimationCompleteTime.HasValue &&
                    panel.FirstVisibleTime.HasValue &&
                    !snapshot.IsAnimatorInTransition &&
                    snapshot.NormalizedTime >= 0.95f)
                {
                    panel.AnimationCompleteTime = currentTime;
                    MelonLogger.Msg($"[Diagnostic] {panel.Name} animation complete at {currentTime:F3}s");
                }

                // Track alpha stable time (alpha hasn't changed significantly for 0.2 seconds)
                if (!panel.AlphaStableTime.HasValue && panel.FirstVisibleTime.HasValue)
                {
                    var recentSnapshots = panel.Snapshots
                        .Where(s => s.Time > currentTime - 0.2f)
                        .ToList();

                    if (recentSnapshots.Count >= 3)
                    {
                        float maxAlpha = recentSnapshots.Max(s => s.Alpha);
                        float minAlpha = recentSnapshots.Min(s => s.Alpha);
                        if (maxAlpha - minAlpha < 0.02f && maxAlpha >= 0.9f)
                        {
                            panel.AlphaStableTime = currentTime;
                            MelonLogger.Msg($"[Diagnostic] {panel.Name} alpha stable at {currentTime:F3}s (alpha={snapshot.Alpha:F2})");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Dump one-time analysis of all current popup/overlay panels.
        /// Shows base classes and available lifecycle methods.
        /// </summary>
        public void DumpPanelAnalysis()
        {
            MelonLogger.Msg("=== PANEL ANIMATION DIAGNOSTIC ===");
            MelonLogger.Msg("Scanning for popup/overlay panels...\n");

            var foundPanels = new List<(GameObject go, string info)>();

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                bool matchesPattern = false;
                foreach (var pattern in PanelPatterns)
                {
                    if (go.name.Contains(pattern))
                    {
                        matchesPattern = true;
                        break;
                    }
                }
                if (!matchesPattern) continue;

                // Skip if not a clone (instantiated prefab)
                if (!go.name.EndsWith("(Clone)")) continue;

                var info = AnalyzePanel(go);
                foundPanels.Add((go, info));
            }

            if (foundPanels.Count == 0)
            {
                MelonLogger.Msg("No popup/overlay panels found. Try opening Settings or a dialog first.");
            }
            else
            {
                MelonLogger.Msg($"Found {foundPanels.Count} panel(s):\n");
                foreach (var (go, info) in foundPanels)
                {
                    MelonLogger.Msg(info);
                    MelonLogger.Msg("");
                }
            }

            MelonLogger.Msg("=== END DIAGNOSTIC ===");
        }

        #endregion

        #region Private Methods

        private void StartTracking()
        {
            MelonLogger.Msg("=== STARTING ANIMATION TRACKING ===");
            MelonLogger.Msg("Press F11 again to stop and see timing report.\n");

            _isTracking = true;
            _frameCounter = 0;
            _trackingStartTime = Time.time;
            _trackedPanels.Clear();

            // Find all current panels and start tracking
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                bool matchesPattern = false;
                foreach (var pattern in PanelPatterns)
                {
                    if (go.name.Contains(pattern))
                    {
                        matchesPattern = true;
                        break;
                    }
                }
                if (!matchesPattern) continue;
                if (!go.name.EndsWith("(Clone)")) continue;

                var panel = CreateTrackedPanel(go);
                _trackedPanels[go.GetInstanceID()] = panel;
                MelonLogger.Msg($"[Diagnostic] Tracking: {go.name}");
            }

            if (_trackedPanels.Count == 0)
            {
                MelonLogger.Msg("No panels to track. Try opening a popup first, then press F11.");
                _isTracking = false;
            }
        }

        private void StopTrackingAndReport()
        {
            _isTracking = false;
            float totalTime = Time.time - _trackingStartTime;

            MelonLogger.Msg("\n=== ANIMATION TRACKING REPORT ===");
            MelonLogger.Msg($"Tracked for {totalTime:F2} seconds\n");

            foreach (var kvp in _trackedPanels)
            {
                var panel = kvp.Value;
                MelonLogger.Msg($"Panel: {panel.Name}");
                MelonLogger.Msg($"  Base classes: {panel.BaseClasses}");
                MelonLogger.Msg($"  Has FinishOpen: {panel.HasFinishOpen}");
                MelonLogger.Msg($"  Has IsReadyToShow: {panel.HasIsReadyToShow}");
                MelonLogger.Msg($"  Has Animator: {panel.HasAnimator}");
                MelonLogger.Msg($"  Snapshots recorded: {panel.Snapshots.Count}");

                if (panel.FirstVisibleTime.HasValue)
                    MelonLogger.Msg($"  First visible (alpha>=0.5): {panel.FirstVisibleTime:F3}s");
                else
                    MelonLogger.Msg($"  First visible: NOT REACHED");

                if (panel.AnimationCompleteTime.HasValue)
                    MelonLogger.Msg($"  Animation complete: {panel.AnimationCompleteTime:F3}s");
                else
                    MelonLogger.Msg($"  Animation complete: NOT REACHED (may have looping idle)");

                if (panel.AlphaStableTime.HasValue)
                    MelonLogger.Msg($"  Alpha stable (>=0.9, delta<0.02): {panel.AlphaStableTime:F3}s");
                else
                    MelonLogger.Msg($"  Alpha stable: NOT REACHED");

                // Calculate deltas
                if (panel.FirstVisibleTime.HasValue)
                {
                    if (panel.AlphaStableTime.HasValue)
                    {
                        float delta = panel.AlphaStableTime.Value - panel.FirstVisibleTime.Value;
                        MelonLogger.Msg($"  Time from visible to stable: {delta:F3}s");
                    }
                    if (panel.AnimationCompleteTime.HasValue)
                    {
                        float delta = panel.AnimationCompleteTime.Value - panel.FirstVisibleTime.Value;
                        MelonLogger.Msg($"  Time from visible to anim complete: {delta:F3}s");
                    }
                }

                // Show alpha timeline sample
                if (panel.Snapshots.Count > 0)
                {
                    MelonLogger.Msg($"  Alpha timeline (sample):");
                    var samples = panel.Snapshots
                        .Where((s, i) => i % 10 == 0 || i == panel.Snapshots.Count - 1)
                        .Take(10)
                        .ToList();
                    foreach (var s in samples)
                    {
                        string animState = s.IsAnimatorInTransition ? "TRANS" : "IDLE";
                        MelonLogger.Msg($"    {s.Time:F2}s: alpha={s.Alpha:F2}, anim={animState}, norm={s.NormalizedTime:F2}");
                    }
                }

                MelonLogger.Msg("");
            }

            MelonLogger.Msg("=== END REPORT ===");
        }

        private TrackedPanel CreateTrackedPanel(GameObject go)
        {
            var panel = new TrackedPanel
            {
                GameObject = go,
                Name = go.name
            };

            // Find CanvasGroup
            panel.CanvasGroup = go.GetComponent<CanvasGroup>();
            if (panel.CanvasGroup == null)
                panel.CanvasGroup = go.GetComponentInChildren<CanvasGroup>();

            // Find Animator
            var animators = go.GetComponentsInChildren<Component>()
                .Where(c => c != null && c.GetType().Name == "Animator")
                .ToList();
            panel.HasAnimator = animators.Count > 0;
            panel.Animator = animators.FirstOrDefault();

            // Analyze MonoBehaviours for lifecycle methods
            var monoBehaviours = go.GetComponentsInChildren<MonoBehaviour>();
            var baseClassSet = new HashSet<string>();

            foreach (var mb in monoBehaviours)
            {
                if (mb == null) continue;
                var type = mb.GetType();

                // Collect base classes
                var checkType = type;
                while (checkType != null && checkType != typeof(MonoBehaviour))
                {
                    baseClassSet.Add(checkType.Name);
                    checkType = checkType.BaseType;
                }

                // Check for lifecycle methods
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                if (type.GetMethod("FinishOpen", flags) != null)
                    panel.HasFinishOpen = true;
                if (type.GetMethod("FinishClose", flags) != null)
                    panel.HasFinishClose = true;
                if (type.GetProperty("IsOpen", flags) != null)
                    panel.HasIsOpen = true;
                if (type.GetProperty("IsReadyToShow", flags) != null)
                    panel.HasIsReadyToShow = true;
            }

            panel.BaseClasses = string.Join(" -> ", baseClassSet.Take(5));

            return panel;
        }

        private string AnalyzePanel(GameObject go)
        {
            var panel = CreateTrackedPanel(go);

            var lines = new List<string>
            {
                $"PANEL: {go.name}",
                $"  Path: {GetPath(go.transform)}",
                $"  Base classes: {panel.BaseClasses}",
                "",
                "  Lifecycle methods:",
                $"    FinishOpen:     {(panel.HasFinishOpen ? "YES" : "NO")}",
                $"    FinishClose:    {(panel.HasFinishClose ? "YES" : "NO")}",
                $"    IsOpen:         {(panel.HasIsOpen ? "YES" : "NO")}",
                $"    IsReadyToShow:  {(panel.HasIsReadyToShow ? "YES" : "NO")}",
                "",
                "  Animation components:",
                $"    Has Animator:   {(panel.HasAnimator ? "YES" : "NO")}",
                $"    Has CanvasGroup: {(panel.CanvasGroup != null ? "YES" : "NO")}"
            };

            if (panel.CanvasGroup != null)
            {
                lines.Add($"    Current alpha:  {panel.CanvasGroup.alpha:F2}");
            }

            if (panel.HasAnimator && panel.Animator != null)
            {
                bool inTransition = GetAnimatorInTransition(panel);
                float normTime = GetAnimatorNormalizedTime(panel);
                lines.Add($"    In transition:  {inTransition}");
                lines.Add($"    Normalized time: {normTime:F2}");
            }

            return string.Join("\n", lines);
        }

        private float GetAlpha(TrackedPanel panel)
        {
            if (panel.CanvasGroup == null) return 1f;
            return panel.CanvasGroup.alpha;
        }

        private bool GetAnimatorInTransition(TrackedPanel panel)
        {
            if (panel.Animator == null) return false;

            try
            {
                var type = panel.Animator.GetType();
                var method = type.GetMethod("IsInTransition");
                if (method != null)
                {
                    return (bool)method.Invoke(panel.Animator, new object[] { 0 });
                }
            }
            catch { }

            return false;
        }

        private float GetAnimatorNormalizedTime(TrackedPanel panel)
        {
            if (panel.Animator == null) return 1f;

            try
            {
                var type = panel.Animator.GetType();
                var getStateInfo = type.GetMethod("GetCurrentAnimatorStateInfo");
                if (getStateInfo != null)
                {
                    var stateInfo = getStateInfo.Invoke(panel.Animator, new object[] { 0 });
                    var normTimeProp = stateInfo.GetType().GetProperty("normalizedTime");
                    if (normTimeProp != null)
                    {
                        return (float)normTimeProp.GetValue(stateInfo);
                    }
                }
            }
            catch { }

            return 1f;
        }

        private string GetPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null)
            {
                parts.Insert(0, t.name);
                t = t.parent;
                if (parts.Count > 4) // Limit depth
                {
                    parts.Insert(0, "...");
                    break;
                }
            }
            return string.Join("/", parts);
        }

        #endregion
    }
}
