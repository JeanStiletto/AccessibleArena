using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

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
        /// Whether PlayBlade is currently visible (state != 0).
        /// </summary>
        public bool IsPlayBladeActive => PlayBladeState != 0;

        /// <summary>
        /// Debounce: Time of last panel change (to prevent rapid-fire events).
        /// </summary>
        private float _lastChangeTime;
        private const float DebounceSeconds = 0.1f;

        /// <summary>
        /// Track announced panels to prevent double announcements.
        /// </summary>
        private readonly HashSet<string> _announcedPanels = new HashSet<string>();

        #endregion

        #region Initialization

        public PanelStateManager()
        {
            _instance = this;
            MelonLogger.Msg("[PanelStateManager] Initialized");
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
            if (PanelRegistry.ShouldIgnorePanel(panel.Name))
            {
                MelonLogger.Msg($"[PanelStateManager] Ignoring panel (in ignore list): {panel.Name}");
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
    }
}
