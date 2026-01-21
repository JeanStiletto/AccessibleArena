namespace AccessibleArena.Core.Services.PanelDetection
{
    /// <summary>
    /// Types of panels in the MTGA UI.
    /// Each type has different detection methods and behaviors.
    /// </summary>
    public enum PanelType
    {
        /// <summary>No panel active</summary>
        None = 0,

        /// <summary>Login scene panels (Welcome, Login, Registration)</summary>
        Login = 1,

        /// <summary>Settings menu overlay</summary>
        Settings = 2,

        /// <summary>PlayBlade and sub-blades (deck selection, events, etc.)</summary>
        Blade = 3,

        /// <summary>Friends/Social panel (F4)</summary>
        Social = 4,

        /// <summary>Alpha-based popups (SystemMessageView, dialogs, modals)</summary>
        Popup = 5,

        /// <summary>NavContentController descendants (home, profile, store, etc.)</summary>
        ContentPanel = 6,

        /// <summary>Color Challenge / Campaign panels</summary>
        Campaign = 7
    }

    /// <summary>
    /// Detection method for panels.
    /// Determines which detector handles a panel type.
    /// </summary>
    public enum PanelDetectionMethod
    {
        /// <summary>Event-driven via Harmony patches on property setters</summary>
        Harmony,

        /// <summary>Polling via reflection on IsOpen properties</summary>
        Reflection,

        /// <summary>Polling via CanvasGroup alpha state</summary>
        Alpha
    }
}
