using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using AccessibleArena.Patches;

namespace AccessibleArena.Core.Services.PanelDetection
{
    /// <summary>
    /// Detector that uses Harmony patches to detect panel state changes.
    /// Event-driven detection for panels that use Show/Hide methods or property setters.
    ///
    /// Handles: PlayBlade, Settings, Blades, SocialUI, NavContentController
    /// </summary>
    public class HarmonyPanelDetector
    {
        public string DetectorId => "HarmonyDetector";

        private PanelStateManager _stateManager;
        private bool _initialized;

        // Track controller instances to their GameObjects for proper panel tracking
        private readonly Dictionary<object, GameObject> _controllerToGameObject = new Dictionary<object, GameObject>();

        public void Initialize(PanelStateManager stateManager)
        {
            if (_initialized)
            {
                MelonLogger.Warning($"[{DetectorId}] Already initialized");
                return;
            }

            _stateManager = stateManager;

            // Subscribe to Harmony patch events
            PanelStatePatch.OnPanelStateChanged += OnHarmonyPanelStateChanged;

            _initialized = true;
            MelonLogger.Msg($"[{DetectorId}] Initialized, subscribed to PanelStatePatch events");
        }

        public void Update()
        {
            // Event-driven detector - no polling needed
            // Periodically clean up stale controller references
            CleanupStaleReferences();
        }

        public void Reset()
        {
            _controllerToGameObject.Clear();
            MelonLogger.Msg($"[{DetectorId}] Reset");
        }

        // Patterns handled by Harmony detector (event-driven via property setters)
        // These panels have patchable setters and/or use animations
        private static readonly string[] HarmonyPatterns = new[]
        {
            "playblade",
            "settings",
            "socialui",
            "friendswidget",
            "eventblade",
            "findmatchblade",
            "deckselectblade",
            "bladecontentview"
        };

        public bool HandlesPanel(string panelName)
        {
            if (string.IsNullOrEmpty(panelName))
                return false;

            var lower = panelName.ToLowerInvariant();
            foreach (var pattern in HarmonyPatterns)
            {
                if (lower.Contains(pattern))
                    return true;
            }
            return false;
        }

        private void OnHarmonyPanelStateChanged(object controller, bool isOpen, string typeName)
        {
            if (_stateManager == null || controller == null)
                return;

            try
            {
                // Get or find the GameObject for this controller
                GameObject gameObject = GetGameObjectForController(controller);
                if (gameObject == null)
                {
                    MelonLogger.Msg($"[{DetectorId}] Could not find GameObject for {typeName}, skipping");
                    return;
                }

                // Determine panel type from the type name
                PanelType panelType = DeterminePanelType(typeName);

                if (isOpen)
                {
                    // Create PanelInfo and report to state manager
                    var panelInfo = new PanelInfo(
                        typeName,
                        panelType,
                        gameObject,
                        PanelDetectionMethod.Harmony
                    );

                    // Handle special case for PlayBlade state
                    // Both "PlayBlade:" and "Blade:" prefixes indicate blade is active
                    if (typeName.StartsWith("PlayBlade:"))
                    {
                        var statePart = typeName.Substring("PlayBlade:".Length);
                        if (statePart == "Generic")
                        {
                            // Generic PlayBlade detected via GameObject name (e.g., CampaignGraph blade)
                            _stateManager.SetPlayBladeState(1);
                            MelonLogger.Msg($"[{DetectorId}] Set PlayBladeState=1 from generic PlayBlade");
                        }
                        else
                        {
                            int bladeState = ParsePlayBladeState(statePart);
                            _stateManager.SetPlayBladeState(bladeState);
                        }
                    }
                    else if (typeName.StartsWith("Blade:"))
                    {
                        // BladeContentView panels (LastPlayed, Events, FindMatch, etc.)
                        // Set PlayBlade active based on content view type
                        var contentView = typeName.Substring("Blade:".Length);
                        int bladeState = ParseBladeContentViewState(contentView);
                        if (bladeState > 0)
                        {
                            _stateManager.SetPlayBladeState(bladeState);
                            MelonLogger.Msg($"[{DetectorId}] Set PlayBladeState={bladeState} from content view: {contentView}");
                        }
                    }

                    _stateManager.ReportPanelOpened(panelInfo);
                    MelonLogger.Msg($"[{DetectorId}] Reported panel opened: {typeName}");
                }
                else
                {
                    // Handle PlayBlade closing
                    if (typeName.StartsWith("PlayBlade:"))
                    {
                        var statePart = typeName.Substring("PlayBlade:".Length);
                        if (statePart == "Hidden" || statePart == "Generic" || ParsePlayBladeState(statePart) == 0)
                        {
                            _stateManager.SetPlayBladeState(0);
                            MelonLogger.Msg($"[{DetectorId}] Set PlayBladeState=0 from PlayBlade closing: {statePart}");
                        }
                    }
                    else if (typeName.StartsWith("Blade:"))
                    {
                        // BladeContentView hiding - blade is closing
                        _stateManager.SetPlayBladeState(0);
                        MelonLogger.Msg($"[{DetectorId}] Set PlayBladeState=0 from content view closing: {typeName}");
                    }

                    // Report panel closed
                    _stateManager.ReportPanelClosed(gameObject);
                    MelonLogger.Msg($"[{DetectorId}] Reported panel closed: {typeName}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[{DetectorId}] Error handling panel state change: {ex.Message}");
            }
        }

        private GameObject GetGameObjectForController(object controller)
        {
            // Check cache first
            if (_controllerToGameObject.TryGetValue(controller, out var cached) && cached != null)
                return cached;

            // Try to get GameObject from MonoBehaviour
            if (controller is MonoBehaviour mb && mb != null)
            {
                _controllerToGameObject[controller] = mb.gameObject;
                return mb.gameObject;
            }

            // Try to get gameObject via reflection
            var type = controller.GetType();
            var gameObjectProp = type.GetProperty("gameObject",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (gameObjectProp != null)
            {
                try
                {
                    var go = gameObjectProp.GetValue(controller) as GameObject;
                    if (go != null)
                    {
                        _controllerToGameObject[controller] = go;
                        return go;
                    }
                }
                catch
                {
                    // Ignore reflection errors
                }
            }

            return null;
        }

        private PanelType DeterminePanelType(string typeName)
        {
            if (typeName.Contains("Settings"))
                return PanelType.Settings;
            if (typeName.Contains("PlayBlade") || typeName.Contains("Blade"))
                return PanelType.Blade;
            if (typeName.Contains("Social"))
                return PanelType.Social;
            if (typeName.Contains("HomePage") || typeName.Contains("Home"))
                return PanelType.ContentPanel;
            if (typeName.Contains("DeckSelect"))
                return PanelType.Blade;

            return PanelType.ContentPanel;
        }

        private int ParsePlayBladeState(string stateName)
        {
            // PlayBladeVisualStates: Hidden=0, Events=1, DirectChallenge=2, FriendChallenge=3
            switch (stateName)
            {
                case "Hidden":
                    return 0;
                case "Events":
                    return 1;
                case "DirectChallenge":
                    return 2;
                case "FriendChallenge":
                    return 3;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Maps BladeContentView names to PlayBlade states.
        /// Returns the blade state (1-3) or 0 if not a recognized blade content view.
        /// </summary>
        private int ParseBladeContentViewState(string contentViewName)
        {
            if (string.IsNullOrEmpty(contentViewName))
                return 0;

            // Map content view names to PlayBlade states
            // LastPlayedBladeContentView, EventBladeContentView, FindMatchBladeContentView, etc.
            if (contentViewName.Contains("LastPlayed"))
                return 1; // Treat as Events state (general play mode)
            if (contentViewName.Contains("Event"))
                return 1; // Events
            if (contentViewName.Contains("FindMatch"))
                return 1; // Find Match (part of Events)
            if (contentViewName.Contains("DirectChallenge"))
                return 2; // Direct Challenge
            if (contentViewName.Contains("FriendChallenge"))
                return 3; // Friend Challenge

            // Any other blade content view - assume blade is active
            return 1;
        }

        private void CleanupStaleReferences()
        {
            // Remove entries where the GameObject has been destroyed
            var toRemove = new List<object>();
            foreach (var kvp in _controllerToGameObject)
            {
                if (kvp.Value == null)
                    toRemove.Add(kvp.Key);
            }

            foreach (var key in toRemove)
            {
                _controllerToGameObject.Remove(key);
            }
        }
    }
}
