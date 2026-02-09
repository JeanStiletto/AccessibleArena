using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Services.PanelDetection;
using System.Collections.Generic;
using System.Linq;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for transitional/info screens with few buttons and optional dynamic content.
    /// Handles MatchEnd (victory/defeat) and Matchmaking (queue) screens.
    /// Uses polling to handle UI that loads after the initial scan.
    /// </summary>
    public class LoadingScreenNavigator : BaseNavigator
    {
        public override string NavigatorId => "LoadingScreen";
        public override string ScreenName => GetScreenName();
        public override int Priority => 65;
        protected override bool SupportsCardNavigation => false;

        private enum ScreenMode { None, MatchEnd, Matchmaking }
        private ScreenMode _currentMode = ScreenMode.None;

        // Polling for late-loading UI
        private float _pollTimer;
        private const float PollInterval = 0.5f;
        private const float MaxPollDuration = 10f;
        private float _pollElapsed;
        private int _lastElementCount;
        private bool _polling;

        // Match result text (cached on discovery)
        private string _matchResultText = "";

        // Continue button reference for Backspace shortcut
        private GameObject _continueButton;

        // Diagnostic: dump hierarchy once per activation
        private bool _dumpedHierarchy;

        public LoadingScreenNavigator(IAnnouncementService announcer) : base(announcer) { }

        private void Log(string message) => DebugConfig.LogIf(DebugConfig.LogNavigation, NavigatorId, message);

        #region Screen Name

        private string GetScreenName()
        {
            switch (_currentMode)
            {
                case ScreenMode.MatchEnd:
                    return string.IsNullOrEmpty(_matchResultText) ? "Match ended" : _matchResultText;
                case ScreenMode.Matchmaking:
                    return "Searching for match";
                default:
                    return "Loading";
            }
        }

        #endregion

        #region Screen Detection

        protected override bool DetectScreen()
        {
            // Yield to settings menu (higher priority overlay)
            if (PanelStateManager.Instance?.IsSettingsMenuOpen == true)
                return false;

            // Check modes in priority order
            if (DetectMatchEnd())
            {
                _currentMode = ScreenMode.MatchEnd;
                return true;
            }

            if (DetectMatchmaking())
            {
                _currentMode = ScreenMode.Matchmaking;
                return true;
            }

            return false;
        }

        private bool DetectMatchEnd()
        {
            // Check all loaded scenes for MatchEndScene (loaded additively)
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).name == "MatchEndScene")
                    return true;
            }
            return false;
        }

        private bool DetectMatchmaking()
        {
            // Matchmaking detection: look for FindMatch active state with cancel button
            // The FindMatch UI appears as a blade/overlay in the MainNavigation scene
            var findMatchObj = GameObject.Find("FindMatchWaiting");
            if (findMatchObj != null && findMatchObj.activeInHierarchy)
            {
                Log("DetectMatchmaking: FindMatchWaiting found and active");
                return true;
            }
            return false;
        }

        #endregion

        #region Element Discovery

        protected override void DiscoverElements()
        {
            _continueButton = null;

            switch (_currentMode)
            {
                case ScreenMode.MatchEnd:
                    DiscoverMatchEndElements();
                    break;
                case ScreenMode.Matchmaking:
                    DiscoverMatchmakingElements();
                    break;
            }
        }

        private void DiscoverMatchEndElements()
        {
            Log("=== Discovering MatchEnd elements ===");

            // Get MatchEndScene root objects - only search within this scene
            var matchEndScene = SceneManager.GetSceneByName("MatchEndScene");
            if (!matchEndScene.IsValid() || !matchEndScene.isLoaded)
            {
                Log("MatchEndScene not valid/loaded");
                return;
            }

            var rootObjects = matchEndScene.GetRootGameObjects();
            Log($"MatchEndScene root objects: {rootObjects.Length}");

            // Diagnostic dump of scene hierarchy (first poll only)
            if (!_dumpedHierarchy)
            {
                _dumpedHierarchy = true;
                foreach (var root in rootObjects)
                {
                    DumpHierarchy(root.transform, 0, 5);
                }
            }

            // Extract match result text from TMP_Text in MatchEndScene
            _matchResultText = ExtractMatchResultText(rootObjects);
            Log($"Match result: {_matchResultText}");

            // MatchEndScene uses EventTrigger (not Button/CustomButton) for its click targets.
            // ExitMatchOverlayButton starts INACTIVE and becomes active after animations.
            // Search for known elements by name, including inactive ones (poll for activation).

            // 1. Find ExitMatchOverlayButton (Continue / click to continue)
            foreach (var root in rootObjects)
            {
                var exitButton = FindChildRecursive(root.transform, "ExitMatchOverlayButton");
                if (exitButton != null)
                {
                    if (exitButton.activeInHierarchy)
                    {
                        _continueButton = exitButton;
                        AddElement(exitButton, "Continue, button");
                        Log($"  ADDED: ExitMatchOverlayButton (active)");
                    }
                    else
                    {
                        Log($"  ExitMatchOverlayButton found but INACTIVE (waiting)");
                    }
                }
            }

            // 2. Find any other clickable elements in MatchEndScene
            //    (CustomButton, Selectable, or EventTrigger-based)
            foreach (var root in rootObjects)
            {
                // Search for EventTrigger components (MatchEndScene's click mechanism)
                foreach (var et in root.GetComponentsInChildren<EventTrigger>(false))
                {
                    if (et == null) continue;
                    var go = et.gameObject;
                    if (!go.activeInHierarchy) continue;
                    if (go == _continueButton) continue; // Already added

                    string label = UITextExtractor.GetButtonText(go, null);
                    if (string.IsNullOrEmpty(label))
                        label = UITextExtractor.GetText(go);
                    if (string.IsNullOrEmpty(label))
                        label = go.name;

                    AddElement(go, $"{label}, button");
                    Log($"  ADDED (EventTrigger): {go.name} -> '{label}'");
                }

                // Search for CustomButton / Selectable as well
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(false))
                {
                    if (mb == null) continue;
                    string typeName = mb.GetType().Name;
                    if (typeName != "CustomButton" && typeName != "StyledButton") continue;

                    var go = mb.gameObject;
                    if (!go.activeInHierarchy) continue;
                    if (!IsVisibleByCanvasGroup(go)) continue;

                    string label = UITextExtractor.GetButtonText(go, null);
                    if (string.IsNullOrEmpty(label))
                        label = UITextExtractor.GetText(go);
                    if (string.IsNullOrEmpty(label))
                        label = go.name;

                    AddElement(go, $"{label}, button");
                    Log($"  ADDED (CustomButton): {go.name} -> '{label}'");
                }
            }

            // 3. Find Nav_Settings button (global - lives in NavBar scene, not MatchEndScene)
            var settingsButton = GameObject.Find("Nav_Settings");
            if (settingsButton != null && settingsButton.activeInHierarchy)
            {
                AddElement(settingsButton, "Settings, button");
                Log($"  ADDED (global): Nav_Settings");
            }

            Log($"=== MatchEnd discovery complete: {_elements.Count} elements ===");
        }

        private void DiscoverMatchmakingElements()
        {
            Log("=== Discovering Matchmaking elements ===");

            var waitingObj = GameObject.Find("FindMatchWaiting");
            if (waitingObj == null) return;

            // Find cancel button
            var cancelButton = FindChildRecursive(waitingObj.transform, "CancelButton")
                ?? FindChildRecursive(waitingObj.transform, "Cancel")
                ?? FindChildRecursive(waitingObj.transform, "Button_Cancel");

            if (cancelButton != null && cancelButton.activeInHierarchy)
            {
                string label = UITextExtractor.GetButtonText(cancelButton, null);
                if (string.IsNullOrEmpty(label)) label = "Cancel";
                AddElement(cancelButton, $"{label}, button");
                Log($"  ADDED cancel: {cancelButton.name} -> '{label}'");
            }

            // Also look for any other visible buttons in the waiting UI
            foreach (var selectable in waitingObj.GetComponentsInChildren<Selectable>(false))
            {
                if (selectable == null || !selectable.interactable) continue;
                var go = selectable.gameObject;
                if (go == cancelButton) continue;

                string label = UITextExtractor.GetButtonText(go, null);
                if (string.IsNullOrEmpty(label)) label = go.name;
                AddElement(go, $"{label}, button");
                Log($"  ADDED: {go.name} -> '{label}'");
            }

            Log($"=== Matchmaking discovery complete: {_elements.Count} elements ===");
        }

        #endregion

        #region Text Extraction

        private string ExtractMatchResultText(GameObject[] rootObjects)
        {
            string resultText = "";

            foreach (var root in rootObjects)
            {
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(false))
                {
                    if (text == null) continue;

                    string content = text.text?.Trim();
                    if (string.IsNullOrEmpty(content)) continue;
                    if (content.Length < 3) continue;

                    // Log all text found in scene for diagnostics
                    Log($"  Text in scene: '{content}' on {text.gameObject.name}");

                    // Look for victory/defeat keywords (supports multiple languages)
                    string lower = content.ToLowerInvariant();
                    if (lower.Contains("victory") || lower.Contains("defeat") ||
                        lower.Contains("draw") || lower.Contains("concede") ||
                        lower.Contains("win") || lower.Contains("lose") ||
                        lower.Contains("lost") || lower.Contains("won") ||
                        // German
                        lower.Contains("sieg") || lower.Contains("niederlage"))
                    {
                        Log($"  Result text candidate: '{content}'");
                        if (string.IsNullOrEmpty(resultText) || content.Length < resultText.Length)
                            resultText = content;
                    }
                }
            }

            if (string.IsNullOrEmpty(resultText))
                resultText = "Match ended";

            return resultText;
        }

        #endregion

        #region Filtering

        /// <summary>
        /// Check if element is visible based on CanvasGroup alpha and interactable state.
        /// </summary>
        private bool IsVisibleByCanvasGroup(GameObject obj)
        {
            // Check own CanvasGroup
            var cg = obj.GetComponent<CanvasGroup>();
            if (cg != null && (cg.alpha <= 0 || !cg.interactable))
                return false;

            // Check parent CanvasGroups
            var parent = obj.transform.parent;
            while (parent != null)
            {
                var parentCG = parent.GetComponent<CanvasGroup>();
                if (parentCG != null && !parentCG.ignoreParentGroups)
                {
                    if (parentCG.alpha <= 0 || !parentCG.interactable)
                        return false;
                }
                parent = parent.parent;
            }
            return true;
        }

        #endregion

        #region Announcements

        protected override string GetActivationAnnouncement()
        {
            switch (_currentMode)
            {
                case ScreenMode.MatchEnd:
                    string result = string.IsNullOrEmpty(_matchResultText) ? "Match ended" : _matchResultText;
                    if (_elements.Count > 0)
                        return $"{result}. {_elements.Count} options. Navigate with arrows, Enter to select. Backspace to continue.";
                    return $"{result}. Backspace to continue.";

                case ScreenMode.Matchmaking:
                    return "Searching for match. Navigate with arrows, Backspace to cancel.";

                default:
                    return base.GetActivationAnnouncement();
            }
        }

        #endregion

        #region Input Handling

        protected override bool HandleCustomInput()
        {
            // Backspace: quick action per mode
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                switch (_currentMode)
                {
                    case ScreenMode.MatchEnd:
                        // Activate Continue (back to menu)
                        if (_continueButton != null && _continueButton.activeInHierarchy)
                        {
                            Log("Backspace -> activating Continue button");
                            UIActivator.SimulatePointerClick(_continueButton);
                        }
                        else
                        {
                            // "Click anywhere to continue" - simulate screen center click
                            Log("Backspace -> simulating screen center click (click to continue)");
                            UIActivator.SimulateScreenCenterClick();
                        }
                        return true;

                    case ScreenMode.Matchmaking:
                        // Activate Cancel
                        if (_elements.Count > 0)
                        {
                            var cancelEl = _elements.FirstOrDefault(e =>
                                e.Label.ToLowerInvariant().Contains("cancel"));
                            if (cancelEl.GameObject != null)
                            {
                                Log("Backspace -> activating Cancel button");
                                UIActivator.SimulatePointerClick(cancelEl.GameObject);
                                return true;
                            }
                        }
                        break;
                }
                return true; // Consume backspace even if no button found
            }

            return false;
        }

        protected override bool OnElementActivated(int index, GameObject element)
        {
            // Use SimulatePointerClick for MatchEnd buttons (StyledButton/PromptButton)
            if (_currentMode == ScreenMode.MatchEnd && element != null)
            {
                Log($"Activating MatchEnd button: {element.name}");
                UIActivator.SimulatePointerClick(element);
                return true; // Suppress default activation
            }
            return false;
        }

        #endregion

        #region Polling (Update)

        protected override void OnActivated()
        {
            // Start polling for late-loading UI
            StartPolling();
        }

        private void StartPolling()
        {
            _polling = true;
            _pollTimer = PollInterval;
            _pollElapsed = 0f;
            _lastElementCount = _elements.Count;
            Log($"Polling started, initial elements: {_lastElementCount}");
        }

        /// <summary>
        /// Override Update to handle 0-element activation (per BEST_PRACTICES.md pattern).
        /// MatchEndScene starts with 0 elements (ExitMatchOverlayButton is INACTIVE),
        /// so we must activate before elements are discovered and poll for them.
        /// </summary>
        public override void Update()
        {
            if (!_isActive)
            {
                // Custom activation: allow 0-element activation with polling
                if (DetectScreen())
                {
                    _elements.Clear();
                    _currentIndex = -1;
                    DiscoverElements();
                    _isActive = true;
                    _currentIndex = _elements.Count > 0 ? 0 : -1;
                    OnActivated();

                    if (_elements.Count > 0)
                        UpdateEventSystemSelection();

                    _announcer.AnnounceInterrupt(GetActivationAnnouncement());
                    Log($"Activated with {_elements.Count} elements");
                }
                return;
            }

            // Run base Update for input handling, validation, etc.
            base.Update();

            if (!_isActive || !_polling) return;

            // Poll for new elements
            _pollElapsed += Time.deltaTime;
            _pollTimer -= Time.deltaTime;

            if (_pollTimer <= 0)
            {
                _pollTimer = PollInterval;

                // Re-discover elements and check if count changed
                _elements.Clear();
                _currentIndex = -1;
                DiscoverElements();

                if (_elements.Count > 0)
                    _currentIndex = 0;

                if (_elements.Count != _lastElementCount)
                {
                    Log($"Poll: element count changed {_lastElementCount} -> {_elements.Count}");
                    _lastElementCount = _elements.Count;

                    if (_elements.Count > 0)
                        UpdateEventSystemSelection();

                    _announcer.AnnounceInterrupt(GetActivationAnnouncement());
                }

                // Stop polling after timeout
                if (_pollElapsed >= MaxPollDuration)
                {
                    Log($"Polling timeout reached ({MaxPollDuration}s), stopping");
                    _polling = false;
                }
            }
        }

        #endregion

        #region Validation & Lifecycle

        protected override bool ValidateElements()
        {
            // Yield to settings menu
            if (PanelStateManager.Instance?.IsSettingsMenuOpen == true)
            {
                Log("Settings menu detected - deactivating");
                return false;
            }

            switch (_currentMode)
            {
                case ScreenMode.MatchEnd:
                    if (!DetectMatchEnd())
                        return false;
                    break;

                case ScreenMode.Matchmaking:
                    if (!DetectMatchmaking())
                        return false;
                    break;
            }

            // During polling, stay active even with 0 elements (waiting for UI to load)
            return _elements.Count > 0 || _polling;
        }

        public override void OnSceneChanged(string sceneName)
        {
            Log($"OnSceneChanged: {sceneName}");

            // Reset state
            _polling = false;
            _currentMode = ScreenMode.None;
            _matchResultText = "";
            _continueButton = null;
            _dumpedHierarchy = false;

            if (_isActive)
            {
                Deactivate();
            }
        }

        #endregion

        #region Helpers

        private GameObject FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child.gameObject;

                var found = FindChildRecursive(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Dump scene hierarchy for diagnostics. Logs object names, components, and active state.
        /// </summary>
        private void DumpHierarchy(Transform t, int depth, int maxDepth)
        {
            if (depth > maxDepth) return;

            string indent = new string(' ', depth * 2);
            var components = t.GetComponents<Component>();
            var compNames = new List<string>();
            foreach (var c in components)
            {
                if (c == null) continue;
                string typeName = c.GetType().Name;
                if (typeName == "Transform" || typeName == "RectTransform") continue;
                compNames.Add(typeName);
            }

            string compStr = compNames.Count > 0 ? $" [{string.Join(", ", compNames)}]" : "";
            string activeStr = t.gameObject.activeInHierarchy ? "" : " (INACTIVE)";
            Log($"  HIERARCHY: {indent}{t.name}{compStr}{activeStr}");

            foreach (Transform child in t)
            {
                DumpHierarchy(child, depth + 1, maxDepth);
            }
        }

        #endregion
    }
}
