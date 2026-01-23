namespace AccessibleArena.Core.Services.PanelDetection
{
    /// <summary>
    /// Interface for panel detection plugins.
    /// Each detector handles specific panel types and reports to PanelStateManager.
    /// </summary>
    public interface IPanelDetector
    {
        /// <summary>
        /// Unique identifier for this detector (for logging).
        /// </summary>
        string DetectorId { get; }

        /// <summary>
        /// Initialize the detector with a reference to the state manager.
        /// Called once during setup.
        /// </summary>
        void Initialize(PanelStateManager stateManager);

        /// <summary>
        /// Called every frame. Polling-based detectors do their work here.
        /// Event-based detectors may do nothing in Update().
        /// </summary>
        void Update();

        /// <summary>
        /// Reset detector state. Called on scene changes.
        /// </summary>
        void Reset();

        /// <summary>
        /// Check if this detector owns (is responsible for) a given panel.
        /// Used to prevent duplicate detection across detectors.
        /// </summary>
        /// <param name="panelName">The panel name or type name.</param>
        /// <returns>True if this detector should handle this panel.</returns>
        bool HandlesPanel(string panelName);
    }
}
