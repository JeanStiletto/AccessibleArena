using System;
using System.Collections.Generic;
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
        /// Fallback: checks for Btn_BladeIsOpen button (catches edge cases where Harmony events missed).
        /// Note: CampaignGraph (Color Challenge) is excluded - it has blade-like UI but is a content page.
        /// </summary>
        public bool IsPlayBladeActive
        {
            get
            {
                // Primary: check Harmony-tracked state
                if (PlayBladeState != 0)
                    return true;

                // Fallback: check for blade buttons (not tracked via events)
                // Only for Home page PlayBlade, not CampaignGraph (Color Challenge)
                var bladeIsOpenButton = UnityEngine.GameObject.Find("Btn_BladeIsOpen");
                if (bladeIsOpenButton != null && bladeIsOpenButton.activeInHierarchy)
                {
                    // Exclude CampaignGraph's blade - it's a content page, not a PlayBlade overlay
                    var parent = bladeIsOpenButton.transform;
                    while (parent != null)
                    {
                        if (parent.name.Contains("CampaignGraphPage"))
                            return false;
                        parent = parent.parent;
                    }
                    return true;
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
        /// True after a scene change until the first non-popup panel is reported opened.
        /// During this window, alpha-detected popups are suppressed because scene
        /// initialization can create transient popup GOs (e.g., Store's ConfirmationModal)
        /// that are active with alpha=1 but contain placeholder content.
        /// </summary>
        public bool IsSceneLoading { get; private set; }

        /// <summary>
        /// Clear the scene-loading gate so alpha-detected popups are no longer suppressed.
        /// Call from navigators that own screens without non-popup panels (e.g., MatchEnd).
        /// </summary>
        public void ClearSceneLoadingGate()
        {
            if (!IsSceneLoading) return;
            IsSceneLoading = false;
            MelonLogger.Msg("[PanelStateManager] Scene loading gate cleared externally");
        }

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

            // Decorative panels (e.g. 3D reward animations) are not interactive.
            // Don't track them - they'd block real popups from becoming active.
            if (BaseNavigator.IsDecorativePanel(panel.Name))
            {
                MelonLogger.Msg($"[PanelStateManager] Skipping decorative panel (not tracked): {panel.Name}");
                return false;
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

            // First non-popup panel after scene change clears the loading gate
            if (IsSceneLoading && panel.Type != PanelType.Popup)
            {
                IsSceneLoading = false;
                MelonLogger.Msg($"[PanelStateManager] Scene loading complete (first panel: {panel.Name})");
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
            // Check if blade panels exist before cleanup (to detect blade destruction)
            bool hadBladePanels = PlayBladeState != 0 && _panelStack.Exists(p => p.Type == PanelType.Blade);

            // Clean up invalid panels
            _panelStack.RemoveAll(p => !p.IsValid);

            // If blade panels were removed during cleanup and none remain, reset PlayBladeState.
            // This handles the case where the blade's GameObject is destroyed
            // during a page transition without BladeContentView.Hide firing.
            if (hadBladePanels && !_panelStack.Exists(p => p.Type == PanelType.Blade && p.IsValid))
            {
                MelonLogger.Msg($"[PanelStateManager] Resetting stale PlayBladeState (was {PlayBladeState}) - blade panel destroyed");
                SetPlayBladeState(0);
            }

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
            IsSceneLoading = true;

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
                    var removed = _panelStack[i];
                    MelonLogger.Msg($"[PanelStateManager] Removing invalid panel: {removed.Name}");

                    // Reset AlphaDetector tracking so it can re-detect if still visible
                    if (removed.DetectedBy == PanelDetectionMethod.Alpha)
                        _alphaDetector?.ResetPanel(removed.Name);

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

    }
}
