using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.PanelDetection;
using System.Collections.Generic;
using System.Linq;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for transitional/info screens with few buttons and optional dynamic content.
    /// Handles MatchEnd (victory/defeat), Matchmaking (queue), and GameLoading (startup) screens.
    /// Uses polling to handle UI that loads after the initial scan.
    /// </summary>
    public class LoadingScreenNavigator : BaseNavigator
    {
        public override string NavigatorId => "LoadingScreen";
        public override string ScreenName => GetScreenName();
        public override int Priority => 65;
        protected override bool SupportsCardNavigation => false;

        private enum ScreenMode { None, MatchEnd, PreGame, Matchmaking, GameLoading }
        private ScreenMode _currentMode = ScreenMode.None;

        // Polling for late-loading UI
        private float _pollTimer;
        private const float PollInterval = 0.5f;
        private const float GameLoadingPollInterval = 1.0f;
        private const float MaxPollDuration = 10f;
        private float _pollElapsed;
        private int _lastElementCount;
        private bool _polling;

        // Match result text (cached on discovery)
        private string _matchResultText = "";

        // Continue button reference for Backspace shortcut
        private GameObject _continueButton;

        // PreGame: cancel button reference for Backspace shortcut
        private GameObject _cancelButton;

        // PreGame: timer TMP_Text for live updates
        private TMP_Text _timerText;

        // GameLoading: InfoText reference for status messages
        private TMP_Text _loadingInfoText;
        private string _lastLoadingStatusText = "";

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
                    return string.IsNullOrEmpty(_matchResultText) ? Strings.ScreenMatchEnded : _matchResultText;
                case ScreenMode.PreGame:
                    return Strings.ScreenSearchingForMatch;
                case ScreenMode.Matchmaking:
                    return Strings.ScreenSearchingForMatch;
                case ScreenMode.GameLoading:
                    return Strings.ScreenLoading;
                default:
                    return Strings.ScreenLoading;
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

            if (DetectPreGame())
            {
                _currentMode = ScreenMode.PreGame;
                return true;
            }

            if (DetectMatchmaking())
            {
                _currentMode = ScreenMode.Matchmaking;
                return true;
            }

            if (DetectGameLoading())
            {
                _currentMode = ScreenMode.GameLoading;
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

        private bool DetectPreGame()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).name == "PreGameScene")
                    return true;
            }
            return false;
        }

        private bool DetectGameLoading()
        {
            var scene = SceneManager.GetActiveScene();
            return scene.name == "AssetPrep";
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
                case ScreenMode.PreGame:
                    DiscoverPreGameElements();
                    break;
                case ScreenMode.Matchmaking:
                    DiscoverMatchmakingElements();
                    break;
                case ScreenMode.GameLoading:
                    DiscoverGameLoadingElements();
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

            // 3. Collect info text elements (rank, format, etc.)
            //    These appear after animations, so polling catches them.
            foreach (var root in rootObjects)
            {
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(false))
                {
                    if (text == null) continue;
                    string content = text.text?.Trim();
                    if (string.IsNullOrEmpty(content)) continue;

                    string objName = text.gameObject.name;

                    // Build combined rank info: "Constructed-Rang: Silber Stufe 4"
                    if (objName == "Text_Rank" || objName == "Text_RankFormat")
                    {
                        // Collect both rank parts, add as single combined element below
                        continue;
                    }

                    // Skip text already used for result or generic UI labels
                    if (objName == "text_Title") continue; // Already in announcement
                    if (objName == "text_ClicktoContinue") continue; // Redundant with Backspace hint
                }

                // Find and combine rank info
                string rankText = FindTextByName(root, "Text_Rank");
                string rankFormat = FindTextByName(root, "Text_RankFormat");
                if (!string.IsNullOrEmpty(rankText))
                {
                    string rankLabel = !string.IsNullOrEmpty(rankFormat)
                        ? $"{rankFormat}: {rankText}"
                        : rankText;

                    // Use the Text_Rank GameObject as the navigable element
                    var rankObj = FindChildRecursive(root.transform, "Text_Rank");
                    if (rankObj != null)
                    {
                        AddElement(rankObj, rankLabel);
                        Log($"  ADDED (info): Rank -> '{rankLabel}'");
                    }
                }

                // Find "View Battlefield" text/button if present
                var viewBattlefieldText = FindTextByName(root, "Text");
                if (!string.IsNullOrEmpty(viewBattlefieldText) &&
                    viewBattlefieldText.ToLowerInvariant().Contains("betrachten") ||
                    !string.IsNullOrEmpty(viewBattlefieldText) &&
                    viewBattlefieldText.ToLowerInvariant().Contains("battlefield"))
                {
                    // This might be a clickable element - find its parent with EventTrigger
                    var textObj = FindChildRecursive(root.transform, "Text");
                    if (textObj != null)
                    {
                        // Check if parent has EventTrigger (clickable)
                        var parentET = textObj.GetComponentInParent<EventTrigger>();
                        var targetObj = parentET != null ? parentET.gameObject : textObj;
                        AddElement(targetObj, $"{viewBattlefieldText}, button");
                        Log($"  ADDED (view): {targetObj.name} -> '{viewBattlefieldText}'");
                    }
                }
            }

            // Settings button not added - accessible via Escape shortcut

            Log($"=== MatchEnd discovery complete: {_elements.Count} elements ===");
        }

        private void DiscoverPreGameElements()
        {
            Log("=== Discovering PreGame elements ===");

            var preGameScene = SceneManager.GetSceneByName("PreGameScene");
            if (!preGameScene.IsValid() || !preGameScene.isLoaded)
            {
                Log("PreGameScene not valid/loaded");
                return;
            }

            var rootObjects = preGameScene.GetRootGameObjects();
            Log($"PreGameScene root objects: {rootObjects.Length}");

            // Diagnostic dump (first poll only)
            if (!_dumpedHierarchy)
            {
                _dumpedHierarchy = true;
                foreach (var root in rootObjects)
                {
                    DumpHierarchy(root.transform, 0, 4);
                }
            }

            // Targeted element discovery by name
            TMP_Text queueDetailText = null;
            TMP_Text timerText = null;
            TMP_Text tipsLabel = null;
            TMP_Text matchFoundText = null;
            GameObject cancelButtonObj = null;

            foreach (var root in rootObjects)
            {
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text == null) continue;
                    string objName = text.gameObject.name;

                    switch (objName)
                    {
                        case "text_queue_detail":
                            queueDetailText = text;
                            break;
                        case "text_timer":
                            timerText = text;
                            break;
                        case "TipsLabel":
                            if (text.gameObject.activeInHierarchy)
                                tipsLabel = text;
                            break;
                        case "TipsLabelSpecial":
                            // Only use if TipsLabel is not active
                            if (tipsLabel == null && text.gameObject.activeInHierarchy)
                                tipsLabel = text;
                            break;
                        case "text_MatchFound":
                            if (text.gameObject.activeInHierarchy)
                                matchFoundText = text;
                            break;
                    }
                }

                // Find Cancel button (CustomButton on inner Button_Cancel)
                foreach (var btn in root.GetComponentsInChildren<Component>(true))
                {
                    if (btn == null) continue;
                    if (btn.gameObject.name == "Button_Cancel" &&
                        btn.GetType().Name == "CustomButton" &&
                        btn.gameObject.activeInHierarchy)
                    {
                        cancelButtonObj = btn.gameObject;
                    }
                }
            }

            // Store timer reference for live updates
            _timerText = timerText;

            // 1. Tips/hint text (flavor text that cycles)
            if (tipsLabel != null)
            {
                string tipContent = tipsLabel.text?.Trim();
                if (!string.IsNullOrEmpty(tipContent) && !tipContent.StartsWith("Description"))
                {
                    AddElement(tipsLabel.gameObject, tipContent);
                    Log($"  ADDED (tip): {tipsLabel.gameObject.name} -> '{tipContent.Substring(0, System.Math.Min(50, tipContent.Length))}...'");
                }
            }

            // 2. Timer: combine queue detail + timer into one element
            if (queueDetailText != null && queueDetailText.gameObject.activeInHierarchy)
            {
                string queueLabel = queueDetailText.text?.Trim() ?? "";
                string timerValue = (timerText != null && timerText.gameObject.activeInHierarchy)
                    ? timerText.text?.Trim() ?? ""
                    : "";
                string combined = string.IsNullOrEmpty(timerValue)
                    ? queueLabel
                    : $"{queueLabel} {timerValue}";
                if (!string.IsNullOrEmpty(combined))
                {
                    AddElement(queueDetailText.gameObject, combined);
                    Log($"  ADDED (timer): {combined}");
                }
            }
            else if (timerText != null && timerText.gameObject.activeInHierarchy)
            {
                // Timer visible but no queue detail label
                string timerValue = timerText.text?.Trim() ?? "";
                if (!string.IsNullOrEmpty(timerValue))
                {
                    AddElement(timerText.gameObject, timerValue);
                    Log($"  ADDED (timer only): {timerValue}");
                }
            }

            // 3. Match found text (appears when opponent found)
            if (matchFoundText != null)
            {
                string matchText = matchFoundText.text?.Trim();
                if (!string.IsNullOrEmpty(matchText))
                {
                    AddElement(matchFoundText.gameObject, matchText);
                    Log($"  ADDED (match found): {matchText}");
                }
            }

            // Store cancel button reference for Backspace shortcut (not added as navigable element)
            _cancelButton = cancelButtonObj;
            // Settings button not added - accessible via Escape shortcut

            Log($"=== PreGame discovery complete: {_elements.Count} elements ===");
        }

        private void DiscoverMatchmakingElements()
        {
            Log("=== Discovering Matchmaking elements ===");

            var waitingObj = GameObject.Find("FindMatchWaiting");
            if (waitingObj == null) return;

            // Store cancel button reference for Backspace shortcut (not added as navigable element)
            var cancelButton = FindChildRecursive(waitingObj.transform, "CancelButton")
                ?? FindChildRecursive(waitingObj.transform, "Cancel")
                ?? FindChildRecursive(waitingObj.transform, "Button_Cancel");

            if (cancelButton != null && cancelButton.activeInHierarchy)
            {
                _cancelButton = cancelButton;
                Log($"  Found cancel button: {cancelButton.name} (Backspace shortcut only)");
            }

            Log($"=== Matchmaking discovery complete: {_elements.Count} elements ===");
        }

        private void DiscoverGameLoadingElements()
        {
            Log("=== Discovering GameLoading elements ===");

            // Find InfoText from AssetPrepScreen component (cache reference)
            if (_loadingInfoText == null)
            {
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == "AssetPrepScreen")
                    {
                        var infoField = mb.GetType().GetField("InfoText");
                        if (infoField != null)
                            _loadingInfoText = infoField.GetValue(mb) as TMP_Text;
                        break;
                    }
                }
            }

            if (_loadingInfoText != null && _loadingInfoText.gameObject != null)
            {
                string status = CleanStatusText(_loadingInfoText.text);
                if (!string.IsNullOrEmpty(status))
                {
                    AddElement(_loadingInfoText.gameObject, status);
                    Log($"  ADDED (status): {status}");
                }
            }

            Log($"=== GameLoading discovery complete: {_elements.Count} elements ===");
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
                resultText = Strings.ScreenMatchEnded;

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
                    string result = string.IsNullOrEmpty(_matchResultText) ? Strings.ScreenMatchEnded : _matchResultText;
                    if (_elements.Count > 0)
                        return Strings.WithHint($"{result}. {Strings.ItemCount(_elements.Count)}", "NavigateHint");
                    return result;

                case ScreenMode.PreGame:
                    if (_elements.Count > 0)
                        return Strings.WithHint($"{Strings.ScreenSearchingForMatch}. {Strings.ItemCount(_elements.Count)}", "NavigateHint");
                    return $"{Strings.ScreenSearchingForMatch}.";

                case ScreenMode.Matchmaking:
                    return Strings.WithHint(Strings.ScreenSearchingForMatch, "NavigateHint");

                case ScreenMode.GameLoading:
                    string loadingStatus = _lastLoadingStatusText;
                    if (!string.IsNullOrEmpty(loadingStatus))
                        return $"{Strings.ScreenLoading}. {loadingStatus}";
                    return $"{Strings.ScreenLoading}.";

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

                    case ScreenMode.PreGame:
                    case ScreenMode.Matchmaking:
                        // Activate Cancel
                        if (_cancelButton != null && _cancelButton.activeInHierarchy)
                        {
                            Log("Backspace -> activating Cancel button");
                            UIActivator.SimulatePointerClick(_cancelButton);
                            return true;
                        }
                        break;
                }
                return true; // Consume backspace even if no button found
            }

            return false;
        }

        protected override bool OnElementActivated(int index, GameObject element)
        {
            if (element == null) return false;

            // Use SimulatePointerClick for MatchEnd buttons (StyledButton/PromptButton)
            if (_currentMode == ScreenMode.MatchEnd)
            {
                Log($"Activating MatchEnd button: {element.name}");
                UIActivator.SimulatePointerClick(element);
                return true;
            }

            // PreGame: only buttons are actionable (Cancel, Settings)
            if (_currentMode == ScreenMode.PreGame)
            {
                var label = _elements[index].Label;
                if (label.Contains("button"))
                {
                    Log($"Activating PreGame button: {element.name}");
                    UIActivator.Activate(element);
                    return true;
                }
                return true; // Consume Enter on info elements (no action)
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

                    // Track initial status text for GameLoading
                    if (_currentMode == ScreenMode.GameLoading && _elements.Count > 0)
                        _lastLoadingStatusText = _elements[0].Label;

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
                _pollTimer = _currentMode == ScreenMode.GameLoading ? GameLoadingPollInterval : PollInterval;

                // Preserve current navigation position
                int savedIndex = _currentIndex;

                // Re-discover elements and check if count changed
                _elements.Clear();
                _currentIndex = -1;
                DiscoverElements();

                // Restore index (clamped to new range)
                if (_elements.Count > 0)
                    _currentIndex = System.Math.Min(savedIndex, _elements.Count - 1);
                if (_currentIndex < 0 && _elements.Count > 0)
                    _currentIndex = 0;

                if (_elements.Count != _lastElementCount)
                {
                    Log($"Poll: element count changed {_lastElementCount} -> {_elements.Count}");
                    _lastElementCount = _elements.Count;

                    if (_elements.Count > 0)
                        UpdateEventSystemSelection();

                    // Track status text to avoid double-announcement
                    if (_currentMode == ScreenMode.GameLoading && _elements.Count > 0)
                        _lastLoadingStatusText = _elements[0].Label;

                    _announcer.AnnounceInterrupt(GetActivationAnnouncement());
                }
                else if (_currentMode == ScreenMode.GameLoading && _elements.Count > 0)
                {
                    // Announce status text changes even when element count is unchanged
                    string currentLabel = _elements[0].Label;
                    if (!string.IsNullOrEmpty(currentLabel) && currentLabel != _lastLoadingStatusText)
                    {
                        _lastLoadingStatusText = currentLabel;
                        _announcer.AnnounceInterrupt(currentLabel);
                        Log($"Loading status changed: {currentLabel}");
                    }
                }

                // Stop polling after timeout (PreGame and GameLoading keep polling)
                if (_currentMode != ScreenMode.PreGame && _currentMode != ScreenMode.GameLoading && _pollElapsed >= MaxPollDuration)
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

                case ScreenMode.PreGame:
                    if (!DetectPreGame())
                        return false;
                    break;

                case ScreenMode.Matchmaking:
                    if (!DetectMatchmaking())
                        return false;
                    break;

                case ScreenMode.GameLoading:
                    if (!DetectGameLoading())
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
            _cancelButton = null;
            _timerText = null;
            _loadingInfoText = null;
            _lastLoadingStatusText = "";
            _dumpedHierarchy = false;

            if (_isActive)
            {
                Deactivate();
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Remove rich text tags from TMP_Text content.
        /// </summary>
        private string CleanStatusText(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");
            return text.Trim();
        }

        /// <summary>
        /// Find the text content of a TMP_Text component by its GameObject name within a hierarchy.
        /// </summary>
        private string FindTextByName(GameObject root, string name)
        {
            var obj = FindChildRecursive(root.transform, name);
            if (obj == null) return null;
            var text = obj.GetComponent<TMP_Text>();
            return text != null ? text.text?.Trim() : null;
        }

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
