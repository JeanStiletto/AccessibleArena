using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace AccessibleArena.Core.Services.PanelDetection
{
    /// <summary>
    /// Detector that uses reflection to poll IsOpen properties on menu controllers.
    /// Polling-based detection for panels with IsOpen but no Harmony-patchable methods.
    ///
    /// Handles: Login scene panels, PopupBase descendants
    /// Note: NavContentController is handled by HarmonyDetector via FinishOpen/FinishClose patches.
    /// </summary>
    public class ReflectionPanelDetector
    {
        public string DetectorId => "ReflectionDetector";

        private PanelStateManager _stateManager;
        private bool _initialized;

        // Check interval (frames)
        private const int CheckIntervalFrames = 10;
        private int _frameCounter;

        // Currently tracked panels
        private readonly HashSet<string> _trackedPanels = new HashSet<string>();

        // Controller types to check
        private static readonly string[] ControllerTypes = new[]
        {
            "PopupBase"
            // NavContentController, SettingsMenu handled by Harmony
        };

        // Login scene panel name patterns
        private static readonly string[] LoginPanelPatterns = new[]
        {
            "Panel - WelcomeGate",
            "Panel - Log In",
            "Panel - Register",
            "Panel - ForgotCredentials",
            "Panel - AgeGate",
            "Panel - Language",
            "Panel - Consent",
            "Panel - EULA",
            "Panel - Marketing",
            "Panel - Terms",
            "Panel - Privacy",
            "Panel - UpdatePolicies"
        };

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

            CheckForPanelChanges();
        }

        public void Reset()
        {
            _trackedPanels.Clear();
            _frameCounter = 0;
            MelonLogger.Msg($"[{DetectorId}] Reset");
        }

        #region Panel Ownership (Stage 5.3)

        /// <summary>
        /// ReflectionDetector is the FALLBACK detector - it handles everything NOT claimed
        /// by HarmonyDetector or AlphaDetector.
        ///
        /// Detection method: Polling IsOpen properties on MonoBehaviour controllers.
        ///
        /// OWNED PANELS (by exclusion):
        /// - PopupBase descendants (have IsOpen property)
        /// - Login scene panels (Panel - WelcomeGate, Panel - Log In, etc.)
        /// - Any controller with IsOpen that isn't claimed by other detectors
        ///
        /// EXPLICITLY HANDLES (via LoginPanelPatterns and ControllerTypes):
        /// - PopupBase
        /// - Login panels: Panel - WelcomeGate, Panel - Log In, Panel - Register, etc.
        /// </summary>

        #endregion

        public bool HandlesPanel(string panelName)
        {
            if (string.IsNullOrEmpty(panelName))
                return false;

            var lower = panelName.ToLowerInvariant();

            // Exclude Harmony-owned patterns (use their authoritative list)
            foreach (var pattern in HarmonyPanelDetector.OwnedPatterns)
            {
                if (lower.Contains(pattern))
                    return false;
            }

            // Exclude Alpha-owned patterns (use their authoritative list)
            foreach (var pattern in AlphaPanelDetector.OwnedPatterns)
            {
                if (lower.Contains(pattern))
                    return false;
            }

            // Exclude Alpha's special "popup" pattern (but not "popupbase" which we handle)
            if (lower.Contains("popup") && !lower.Contains("popupbase"))
                return false;

            // ReflectionDetector handles everything else
            // This includes: PopupBase, Login panels, any panel with IsOpen property
            return true;
        }

        private void CheckForPanelChanges()
        {
            var currentPanels = new List<(string id, GameObject obj)>();

            // Check menu controllers with IsOpen
            CheckMenuControllers(currentPanels);

            // Check Login scene panels
            CheckLoginPanels(currentPanels);

            // Find new panels
            foreach (var (panelId, obj) in currentPanels)
            {
                if (!_trackedPanels.Contains(panelId))
                {
                    _trackedPanels.Add(panelId);
                    ReportPanelOpened(panelId, obj);
                }
            }

            // Find closed panels
            var currentIds = currentPanels.Select(p => p.id).ToHashSet();
            var closedPanels = _trackedPanels.Where(p => !currentIds.Contains(p)).ToList();

            foreach (var panelId in closedPanels)
            {
                _trackedPanels.Remove(panelId);
                ReportPanelClosed(panelId);
            }
        }

        private void CheckMenuControllers(List<(string id, GameObject obj)> panels)
        {
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy)
                    continue;

                var type = mb.GetType();
                string typeName = type.Name;

                // Check if this is a controller type we handle
                bool isOwnedController = false;
                var checkType = type;
                while (checkType != null)
                {
                    if (ControllerTypes.Contains(checkType.Name))
                    {
                        isOwnedController = true;
                        break;
                    }
                    checkType = checkType.BaseType;
                }

                if (!isOwnedController)
                    continue;

                // Check IsOpen state
                if (CheckIsOpen(mb, type))
                {
                    string panelId = $"{typeName}:{mb.gameObject.name}";
                    panels.Add((panelId, mb.gameObject));
                }
            }
        }

        private void CheckLoginPanels(List<(string id, GameObject obj)> panels)
        {
            var panelParent = GameObject.Find("Canvas - Camera/PanelParent");
            if (panelParent == null)
                return;

            foreach (Transform child in panelParent.transform)
            {
                if (child == null || !child.gameObject.activeInHierarchy)
                    continue;

                string childName = child.name;

                foreach (var pattern in LoginPanelPatterns)
                {
                    if (childName.StartsWith(pattern))
                    {
                        string panelId = $"LoginPanel:{childName}";
                        panels.Add((panelId, child.gameObject));
                        break;
                    }
                }
            }
        }

        private bool CheckIsOpen(MonoBehaviour mb, Type type)
        {
            // Try IsOpen property
            var isOpenProp = type.GetProperty("IsOpen",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (isOpenProp != null && isOpenProp.PropertyType == typeof(bool))
            {
                try
                {
                    bool isOpen = (bool)isOpenProp.GetValue(mb);
                    if (!isOpen)
                        return false;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[{DetectorId}] Failed to read IsOpen on {type.Name}: {ex.Message}");
                    return false;
                }
            }

            // Try IsOpen() method
            var isOpenMethod = type.GetMethod("IsOpen",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, Type.EmptyTypes, null);

            if (isOpenMethod != null && isOpenMethod.ReturnType == typeof(bool))
            {
                try
                {
                    bool isOpen = (bool)isOpenMethod.Invoke(mb, null);
                    if (!isOpen)
                        return false;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[{DetectorId}] Failed to call IsOpen() on {type.Name}: {ex.Message}");
                    return false;
                }
            }

            // Check IsReadyToShow if available
            var isReadyProp = type.GetProperty("IsReadyToShow",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (isReadyProp != null && isReadyProp.PropertyType == typeof(bool))
            {
                try
                {
                    bool isReady = (bool)isReadyProp.GetValue(mb);
                    if (!isReady)
                        return false;
                }
                catch
                {
                    // Ignore - panel may not have this property
                }
            }

            return true;
        }

        private void ReportPanelOpened(string panelId, GameObject obj)
        {
            PanelType panelType = PanelType.Popup;
            string cleanName = panelId;

            if (panelId.StartsWith("LoginPanel:"))
            {
                panelType = PanelType.Login;
                cleanName = panelId.Substring("LoginPanel:".Length);
            }
            else if (panelId.StartsWith("PopupBase:"))
            {
                panelType = PanelType.Popup;
                cleanName = panelId.Substring("PopupBase:".Length);
            }

            var panelInfo = new PanelInfo(cleanName, panelType, obj, PanelDetectionMethod.Reflection);
            _stateManager.ReportPanelOpened(panelInfo);
            MelonLogger.Msg($"[{DetectorId}] Reported panel opened: {panelId}");
        }

        private void ReportPanelClosed(string panelId)
        {
            // Try to close by name since we may not have the GameObject reference
            string cleanName = panelId;
            if (panelId.Contains(":"))
            {
                cleanName = panelId.Substring(panelId.IndexOf(':') + 1);
            }

            _stateManager.ReportPanelClosedByName(cleanName);
            MelonLogger.Msg($"[{DetectorId}] Reported panel closed: {panelId}");
        }
    }
}
