using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;
using AccessibleArena.Core.Services;

namespace AccessibleArena.Core.Services.PanelDetection
{
    /// <summary>
    /// Single source of truth for panel state in MTGA.
    /// All panel changes flow through this manager.
    /// Detectors report changes here; consumers subscribe to events.
    /// </summary>
    public class PanelStateManager
    {
        #region Singleton

        private static PanelStateManager _instance;
        public static PanelStateManager Instance => _instance;

        #endregion

        #region Events

        /// <summary>
        /// Fired when the active panel changes (panel that filters navigation).
        /// </summary>
        public event Action<PanelInfo, PanelInfo> OnPanelChanged;

        /// <summary>
        /// Fired when ANY panel opens, regardless of whether it filters navigation.
        /// Use this for triggering rescans - home screen doesn't filter but needs rescan.
        /// </summary>
        public event Action<PanelInfo> OnAnyPanelOpened;

        /// <summary>
        /// Fired when PlayBlade state specifically changes (for blade-specific handling).
        /// </summary>
        public event Action<int> OnPlayBladeStateChanged;

        #endregion

        #region Detectors

        // Detectors owned directly by PanelStateManager (simplified from plugin system)
        private HarmonyPanelDetector _harmonyDetector;
        private ReflectionPanelDetector _reflectionDetector;
        private AlphaPanelDetector _alphaDetector;

        #endregion

        #region State

        /// <summary>
        /// The currently active foreground panel (highest priority panel that filters navigation).
        /// Null when no overlay is active.
        /// </summary>
        public PanelInfo ActivePanel { get; private set; }

        /// <summary>
        /// Stack of all active panels, ordered by priority.
        /// Allows tracking nested panels (e.g., popup over settings).
        /// </summary>
        private readonly List<PanelInfo> _panelStack = new List<PanelInfo>();

        /// <summary>
        /// Current PlayBlade visual state (0=Hidden, 1=Events, 2=DirectChallenge, 3=FriendChallenge).
        /// Tracked separately because blade state affects navigation within the blade.
        /// </summary>
        public int PlayBladeState { get; private set; }

        /// <summary>
        /// Whether PlayBlade is currently visible.
        /// Primary: checks tracked state from Harmony events.
        /// Fallback: checks for Btn_BladeIsOpen button (catches blades without lifecycle events, e.g. CampaignGraph).
        /// </summary>
        public bool IsPlayBladeActive
        {
            get
            {
                // Primary: check Harmony-tracked state
                if (PlayBladeState != 0)
                    return true;

                // Fallback: check for blade buttons (not tracked via events)
                // Stage 3: Btn_BladeIsOpen exists
                var bladeIsOpenButton = UnityEngine.GameObject.Find("Btn_BladeIsOpen");
                if (bladeIsOpenButton != null && bladeIsOpenButton.activeInHierarchy)
                    return true;

                // Stage 2: Btn_BladeIsClosed exists in CampaignGraph context
                var bladeIsClosed = UnityEngine.GameObject.Find("Btn_BladeIsClosed");
                if (bladeIsClosed != null && bladeIsClosed.activeInHierarchy)
                {
                    // Check if it's under CampaignGraphPage (not Home page blade)
                    var parent = bladeIsClosed.transform;
                    while (parent != null)
                    {
                        if (parent.name.Contains("CampaignGraphPage"))
                            return true;
                        parent = parent.parent;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Debounce: Time of last panel change (to prevent rapid-fire events).
        /// </summary>
        private float _lastChangeTime;
        private const float DebounceSeconds = 0.1f;

        /// <summary>
        /// Track announced panels to prevent double announcements.
        /// </summary>
        private readonly HashSet<string> _announcedPanels = new HashSet<string>();

        /// <summary>
        /// DIAGNOSTIC: Track which detector first reported each panel name.
        /// Used for Stage 5.2 overlap audit. Key = panel name, Value = detector method.
        /// </summary>
        private readonly Dictionary<string, PanelDetectionMethod> _panelDetectorAudit =
            new Dictionary<string, PanelDetectionMethod>();

        /// <summary>
        /// DIAGNOSTIC: Track potential overlaps detected during runtime.
        /// Key = panel name, Value = list of detectors that tried to report it.
        /// </summary>
        private readonly Dictionary<string, HashSet<PanelDetectionMethod>> _overlapAudit =
            new Dictionary<string, HashSet<PanelDetectionMethod>>();

        #endregion

        #region Initialization

        public PanelStateManager()
        {
            _instance = this;
        }

        /// <summary>
        /// Initialize all panel detectors.
        /// Call this once after construction.
        /// </summary>
        public void Initialize()
        {
            // Create and initialize detectors
            _harmonyDetector = new HarmonyPanelDetector();
            _harmonyDetector.Initialize(this);

            _reflectionDetector = new ReflectionPanelDetector();
            _reflectionDetector.Initialize(this);

            _alphaDetector = new AlphaPanelDetector();
            _alphaDetector.Initialize(this);

            MelonLogger.Msg("[PanelStateManager] Initialized with 3 detectors");
        }

        /// <summary>
        /// Update all detectors. Call this every frame.
        /// </summary>
        public void Update()
        {
            // Update each detector
            _harmonyDetector?.Update();
            _reflectionDetector?.Update();
            _alphaDetector?.Update();

            // Periodically validate panel state
            ValidatePanels();
        }

        #endregion

        #region Panel State Management

        /// <summary>
        /// Report that a panel has opened.
        /// Called by detectors (Harmony, Reflection, Alpha).
        /// </summary>
        /// <param name="panel">The panel that opened</param>
        /// <returns>True if state changed, false if ignored (duplicate, ignored panel, debounced)</returns>
        public bool ReportPanelOpened(PanelInfo panel)
        {
            if (panel == null || !panel.IsValid)
            {
                MelonLogger.Msg($"[PanelStateManager] Ignoring invalid panel");
                return false;
            }

            // Check if this panel should be ignored
            if (PanelInfo.ShouldIgnorePanel(panel.Name))
            {
                MelonLogger.Msg($"[PanelStateManager] Ignoring panel (in ignore list): {panel.Name}");
                return false;
            }

            // DIAGNOSTIC: Track detector overlap audit (Stage 5.2)
            if (DebugConfig.DebugEnabled && DebugConfig.LogPanelOverlapDiagnostic)
            {
                TrackPanelDetectorAudit(panel);
            }

            // Check if already in stack
            var existing = _panelStack.Find(p => p.GameObject == panel.GameObject);
            if (existing != null)
            {
                MelonLogger.Msg($"[PanelStateManager] Panel already tracked: {panel.Name}");
                return false;
            }

            // Debounce rapid changes
            float currentTime = Time.realtimeSinceStartup;
            if (currentTime - _lastChangeTime < DebounceSeconds)
            {
                MelonLogger.Msg($"[PanelStateManager] Debounced: {panel.Name}");
                // Still add to stack, just don't fire event
                AddToStack(panel);
                return false;
            }

            // Add to stack and update active panel
            AddToStack(panel);
            _lastChangeTime = currentTime;

            MelonLogger.Msg($"[PanelStateManager] Panel opened: {panel}");

            // Fire event for any panel open (triggers rescan even for non-filtering panels)
            OnAnyPanelOpened?.Invoke(panel);

            // Check if this becomes the new active panel (for filtering)
            UpdateActivePanel();

            return true;
        }

        /// <summary>
        /// Report that a panel has closed.
        /// Called by detectors (Harmony, Reflection, Alpha).
        /// </summary>
        /// <param name="gameObject">The GameObject of the panel that closed</param>
        /// <returns>True if state changed</returns>
        public bool ReportPanelClosed(GameObject gameObject)
        {
            if (gameObject == null)
                return false;

            // Find and remove from stack
            var panel = _panelStack.Find(p => p.GameObject == gameObject);
            if (panel == null)
            {
                // Not tracked, ignore
                return false;
            }

            _panelStack.Remove(panel);
            _announcedPanels.Remove(panel.Name);

            MelonLogger.Msg($"[PanelStateManager] Panel closed: {panel.Name}");

            // Update active panel
            UpdateActivePanel();

            return true;
        }

        /// <summary>
        /// Report that a panel has closed by name.
        /// Used when we don't have the GameObject reference.
        /// </summary>
        public bool ReportPanelClosedByName(string panelName)
        {
            if (string.IsNullOrEmpty(panelName))
                return false;

            var panel = _panelStack.Find(p =>
                p.Name.Equals(panelName, StringComparison.OrdinalIgnoreCase) ||
                p.RawGameObjectName.IndexOf(panelName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (panel == null)
                return false;

            return ReportPanelClosed(panel.GameObject);
        }

        /// <summary>
        /// Update PlayBlade state.
        /// </summary>
        public void SetPlayBladeState(int state)
        {
            if (PlayBladeState == state)
                return;

            var oldState = PlayBladeState;
            PlayBladeState = state;

            MelonLogger.Msg($"[PanelStateManager] PlayBlade state: {oldState} -> {state}");

            OnPlayBladeStateChanged?.Invoke(state);

            // If blade opened/closed, that's a panel change
            if ((oldState == 0 && state != 0) || (oldState != 0 && state == 0))
            {
                UpdateActivePanel();
            }
        }

        #endregion

        #region Stack Management

        private void AddToStack(PanelInfo panel)
        {
            _panelStack.Add(panel);
            // Sort by priority (highest first)
            _panelStack.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        private void UpdateActivePanel()
        {
            // Clean up invalid panels
            _panelStack.RemoveAll(p => !p.IsValid);

            // Find highest priority panel that filters navigation
            PanelInfo newActive = null;
            foreach (var panel in _panelStack)
            {
                if (panel.FiltersNavigation && panel.IsValid)
                {
                    newActive = panel;
                    break; // Already sorted by priority
                }
            }

            // Check if active panel changed
            var oldActive = ActivePanel;
            if (oldActive?.GameObject != newActive?.GameObject)
            {
                ActivePanel = newActive;

                MelonLogger.Msg($"[PanelStateManager] Active panel: {oldActive?.Name ?? "none"} -> {newActive?.Name ?? "none"}");

                // Fire event
                OnPanelChanged?.Invoke(oldActive, newActive);
            }
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Get the GameObject to use for filtering navigation elements.
        /// Returns the active panel's GameObject, or null if no filtering needed.
        /// </summary>
        public GameObject GetFilterPanel()
        {
            // Check if active panel is still valid
            if (ActivePanel != null && !ActivePanel.IsValid)
            {
                MelonLogger.Msg($"[PanelStateManager] Active panel became invalid, clearing");
                ReportPanelClosed(ActivePanel.GameObject);
            }

            return ActivePanel?.GameObject;
        }

        /// <summary>
        /// Check if a specific panel type is currently active.
        /// </summary>
        public bool IsPanelTypeActive(PanelType type)
        {
            return _panelStack.Exists(p => p.Type == type && p.IsValid);
        }

        /// <summary>
        /// Check if a panel with the given name is currently active.
        /// Uses Harmony-tracked panel state for precise detection.
        /// </summary>
        public bool IsPanelActive(string panelName)
        {
            return _panelStack.Exists(p => p.Name == panelName && p.IsValid);
        }

        /// <summary>
        /// Check if Settings menu is currently open.
        /// Uses Harmony-tracked panel state for precise detection.
        /// </summary>
        public bool IsSettingsMenuOpen => _panelStack.Exists(p => p.Name == "SettingsMenu" && p.IsValid);

        /// <summary>
        /// Get all currently tracked panels (for debugging).
        /// </summary>
        public IReadOnlyList<PanelInfo> GetPanelStack()
        {
            return _panelStack.AsReadOnly();
        }

        /// <summary>
        /// Check if a panel has been announced (to prevent double announcements).
        /// </summary>
        public bool HasBeenAnnounced(string panelName)
        {
            return _announcedPanels.Contains(panelName);
        }

        /// <summary>
        /// Mark a panel as announced.
        /// </summary>
        public void MarkAnnounced(string panelName)
        {
            _announcedPanels.Add(panelName);
        }

        #endregion

        #region Reset

        /// <summary>
        /// Clear all panel state (for scene changes).
        /// </summary>
        public void Reset()
        {
            MelonLogger.Msg("[PanelStateManager] Reset");

            // Reset all detectors
            _harmonyDetector?.Reset();
            _reflectionDetector?.Reset();
            _alphaDetector?.Reset();

            var oldActive = ActivePanel;
            _panelStack.Clear();
            _announcedPanels.Clear();
            ActivePanel = null;
            PlayBladeState = 0;

            if (oldActive != null)
            {
                OnPanelChanged?.Invoke(oldActive, null);
            }
        }

        /// <summary>
        /// Soft reset - keep tracking but clear announced state.
        /// Used when navigator activates/deactivates.
        /// </summary>
        public void SoftReset()
        {
            _announcedPanels.Clear();
        }

        #endregion

        #region Validation (called periodically)

        /// <summary>
        /// Validate all tracked panels are still valid.
        /// Call this periodically from Update().
        /// </summary>
        public void ValidatePanels()
        {
            bool anyRemoved = false;
            for (int i = _panelStack.Count - 1; i >= 0; i--)
            {
                if (!_panelStack[i].IsValid)
                {
                    MelonLogger.Msg($"[PanelStateManager] Removing invalid panel: {_panelStack[i].Name}");
                    _panelStack.RemoveAt(i);
                    anyRemoved = true;
                }
            }

            if (anyRemoved)
            {
                UpdateActivePanel();
            }
        }

        #endregion

        #region Diagnostic Methods (Stage 5.2 Overlap Audit)

        /// <summary>
        /// Track which detector reports each panel for overlap audit.
        /// Called only when LogPanelOverlapDiagnostic is enabled.
        /// </summary>
        private void TrackPanelDetectorAudit(PanelInfo panel)
        {
            string panelKey = panel.Name;

            // Track all detectors that try to report this panel
            if (!_overlapAudit.TryGetValue(panelKey, out var detectors))
            {
                detectors = new HashSet<PanelDetectionMethod>();
                _overlapAudit[panelKey] = detectors;
            }
            detectors.Add(panel.DetectedBy);

            // Check if this panel was previously reported by a different detector
            if (_panelDetectorAudit.TryGetValue(panelKey, out var previousDetector))
            {
                if (previousDetector != panel.DetectedBy)
                {
                    // OVERLAP DETECTED!
                    MelonLogger.Warning($"[PanelStateManager] OVERLAP DETECTED: Panel '{panelKey}' " +
                        $"previously reported by {previousDetector}, now reported by {panel.DetectedBy}");
                }
            }
            else
            {
                // First time seeing this panel - record which detector owns it
                _panelDetectorAudit[panelKey] = panel.DetectedBy;
                MelonLogger.Msg($"[PanelStateManager] AUDIT: Panel '{panelKey}' owned by {panel.DetectedBy}");
            }
        }

        /// <summary>
        /// Dump the overlap audit results to log.
        /// Call this to see which panels have been reported by multiple detectors.
        /// </summary>
        public void DumpOverlapAudit()
        {
            MelonLogger.Msg("=== PANEL DETECTION OVERLAP AUDIT ===");

            // Find panels reported by multiple detectors
            var overlaps = _overlapAudit.Where(kvp => kvp.Value.Count > 1).ToList();

            if (overlaps.Count == 0)
            {
                MelonLogger.Msg("No overlaps detected - each panel reported by exactly one detector.");
            }
            else
            {
                MelonLogger.Warning($"Found {overlaps.Count} panels with potential overlaps:");
                foreach (var kvp in overlaps)
                {
                    var detectorList = string.Join(", ", kvp.Value);
                    MelonLogger.Warning($"  - '{kvp.Key}': reported by [{detectorList}]");
                }
            }

            // Summary of all tracked panels
            MelonLogger.Msg($"\nTotal panels tracked: {_panelDetectorAudit.Count}");

            var byDetector = _panelDetectorAudit.GroupBy(kvp => kvp.Value)
                .OrderBy(g => g.Key.ToString());

            foreach (var group in byDetector)
            {
                MelonLogger.Msg($"\n{group.Key} ({group.Count()} panels):");
                foreach (var panel in group.OrderBy(p => p.Key))
                {
                    MelonLogger.Msg($"  - {panel.Key}");
                }
            }

            MelonLogger.Msg("=== END OVERLAP AUDIT ===");
        }

        /// <summary>
        /// Clear diagnostic audit data.
        /// </summary>
        public void ClearAuditData()
        {
            _panelDetectorAudit.Clear();
            _overlapAudit.Clear();
            MelonLogger.Msg("[PanelStateManager] Audit data cleared");
        }

        /// <summary>
        /// Run static analysis of known panel names against each detector's HandlesPanel() method.
        /// This identifies potential overlaps in detector ownership claims.
        /// </summary>
        public void RunStaticOverlapAnalysis()
        {
            MelonLogger.Msg("=== STATIC PANEL DETECTION OVERLAP ANALYSIS ===");

            // Display owned patterns from each detector
            MelonLogger.Msg("\n--- Owned Patterns (from detector classes) ---");
            MelonLogger.Msg($"HarmonyDetector.OwnedPatterns: {string.Join(", ", HarmonyPanelDetector.OwnedPatterns)}");
            MelonLogger.Msg($"AlphaDetector.OwnedPatterns: {string.Join(", ", AlphaPanelDetector.OwnedPatterns)}");
            MelonLogger.Msg($"AlphaDetector special: 'popup' (but not 'popupbase')");
            MelonLogger.Msg($"ReflectionDetector: handles everything else (fallback)");

            // Build comprehensive test list from owned patterns + real panel names
            var knownPanelNames = new List<string>();

            // Add test names based on Harmony owned patterns
            foreach (var pattern in HarmonyPanelDetector.OwnedPatterns)
            {
                knownPanelNames.Add(pattern);
                knownPanelNames.Add(char.ToUpper(pattern[0]) + pattern.Substring(1)); // Capitalized
            }

            // Add test names based on Alpha owned patterns
            foreach (var pattern in AlphaPanelDetector.OwnedPatterns)
            {
                knownPanelNames.Add(pattern);
                knownPanelNames.Add(char.ToUpper(pattern[0]) + pattern.Substring(1) + "(Clone)");
            }

            // Add additional real panel names observed in game
            knownPanelNames.AddRange(new[]
            {
                // Real Harmony panels
                "PlayBladeController", "SettingsMenuHost", "EventBladeContentView",
                "FindMatchBladeContentView", "LastPlayedBladeContentView",
                // Reflection panels
                "PopupBase", "Panel - WelcomeGate", "Panel - Log In", "Panel - Register",
                "Panel - AgeGate", "Panel - EULA", "Panel - Consent",
                // Alpha special case
                "Popup(Clone)", "SomePopup(Clone)",
                // Mixed/edge cases (should go to Reflection)
                "NavContentController", "HomePage", "ProfilePage", "StorePage",
                "BoosterChamber", "CampaignGraphPage"
            });

            // Deduplicate
            knownPanelNames = knownPanelNames.Distinct().ToList();

            var overlaps = new List<string>();
            var ownership = new Dictionary<string, List<string>>();

            foreach (var panelName in knownPanelNames)
            {
                var claimers = new List<string>();

                if (_harmonyDetector != null && _harmonyDetector.HandlesPanel(panelName))
                    claimers.Add("Harmony");
                if (_reflectionDetector != null && _reflectionDetector.HandlesPanel(panelName))
                    claimers.Add("Reflection");
                if (_alphaDetector != null && _alphaDetector.HandlesPanel(panelName))
                    claimers.Add("Alpha");

                ownership[panelName] = claimers;

                if (claimers.Count > 1)
                {
                    overlaps.Add($"'{panelName}': claimed by [{string.Join(", ", claimers)}]");
                }
                else if (claimers.Count == 0)
                {
                    MelonLogger.Msg($"  UNCLAIMED: '{panelName}' - no detector handles this panel");
                }
            }

            if (overlaps.Count > 0)
            {
                MelonLogger.Warning($"\nFOUND {overlaps.Count} OVERLAPS:");
                foreach (var overlap in overlaps)
                {
                    MelonLogger.Warning($"  - {overlap}");
                }
            }
            else
            {
                MelonLogger.Msg("\nNo overlaps found - each panel claimed by at most one detector.");
            }

            // Summary by detector
            MelonLogger.Msg("\n--- Ownership Summary ---");
            foreach (var detector in new[] { "Harmony", "Reflection", "Alpha" })
            {
                var panels = ownership.Where(kvp => kvp.Value.Contains(detector))
                    .Select(kvp => kvp.Key).ToList();
                MelonLogger.Msg($"\n{detector} ({panels.Count} panels): {string.Join(", ", panels)}");
            }

            var unclaimed = ownership.Where(kvp => kvp.Value.Count == 0)
                .Select(kvp => kvp.Key).ToList();
            if (unclaimed.Count > 0)
            {
                MelonLogger.Msg($"\nUnclaimed ({unclaimed.Count}): {string.Join(", ", unclaimed)}");
            }

            MelonLogger.Msg("\n=== END STATIC ANALYSIS ===");
        }

        #endregion
    }
}
