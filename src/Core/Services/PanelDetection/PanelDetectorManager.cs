using System.Collections.Generic;
using MelonLoader;

namespace AccessibleArena.Core.Services.PanelDetection
{
    /// <summary>
    /// Manages all panel detectors and coordinates their updates.
    /// Acts as the entry point for the detection system.
    ///
    /// Architecture:
    /// - PanelDetectorManager owns all detectors
    /// - Each detector reports to PanelStateManager
    /// - PanelStateManager fires events that consumers (navigators) subscribe to
    /// </summary>
    public class PanelDetectorManager
    {
        #region Singleton

        private static PanelDetectorManager _instance;
        public static PanelDetectorManager Instance => _instance;

        #endregion

        private readonly List<IPanelDetector> _detectors = new List<IPanelDetector>();
        private PanelStateManager _stateManager;
        private bool _initialized;

        public PanelDetectorManager()
        {
            _instance = this;
        }

        /// <summary>
        /// Initialize the detection system with all detectors.
        /// </summary>
        /// <param name="stateManager">The panel state manager to report to.</param>
        public void Initialize(PanelStateManager stateManager)
        {
            if (_initialized)
            {
                MelonLogger.Warning("[PanelDetectorManager] Already initialized");
                return;
            }

            _stateManager = stateManager;

            // Create and register detectors
            RegisterDetector(new HarmonyPanelDetector());
            RegisterDetector(new ReflectionPanelDetector());
            RegisterDetector(new AlphaPanelDetector());

            // Initialize all detectors
            foreach (var detector in _detectors)
            {
                detector.Initialize(stateManager);
                MelonLogger.Msg($"[PanelDetectorManager] Registered: {detector.DetectorId}");
            }

            _initialized = true;
            MelonLogger.Msg($"[PanelDetectorManager] Initialized with {_detectors.Count} detectors");
        }

        /// <summary>
        /// Register a detector. Call before Initialize() to add custom detectors.
        /// </summary>
        public void RegisterDetector(IPanelDetector detector)
        {
            if (!_detectors.Contains(detector))
            {
                _detectors.Add(detector);
            }
        }

        /// <summary>
        /// Called every frame. Updates all polling-based detectors.
        /// </summary>
        public void Update()
        {
            if (!_initialized)
                return;

            foreach (var detector in _detectors)
            {
                detector.Update();
            }

            // Periodically validate panel state
            _stateManager?.ValidatePanels();
        }

        /// <summary>
        /// Reset all detectors. Called on scene changes.
        /// </summary>
        public void Reset()
        {
            foreach (var detector in _detectors)
            {
                detector.Reset();
            }

            _stateManager?.Reset();
            MelonLogger.Msg("[PanelDetectorManager] Reset all detectors");
        }

        /// <summary>
        /// Check which detector handles a given panel.
        /// </summary>
        /// <param name="panelName">The panel name.</param>
        /// <returns>The detector that handles this panel, or null.</returns>
        public IPanelDetector GetDetectorForPanel(string panelName)
        {
            foreach (var detector in _detectors)
            {
                if (detector.HandlesPanel(panelName))
                    return detector;
            }

            return null;
        }

        /// <summary>
        /// Get all registered detectors (for debugging).
        /// </summary>
        public IReadOnlyList<IPanelDetector> GetDetectors()
        {
            return _detectors.AsReadOnly();
        }
    }
}
