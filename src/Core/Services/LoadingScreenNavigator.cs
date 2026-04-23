using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Utils;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.PanelDetection;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static AccessibleArena.Core.Constants.SceneNames;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using SceneNames = AccessibleArena.Core.Constants.SceneNames;

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

        // Survey popup: interactive Good/Bad/Skip buttons, UI starts INACTIVE (animator intro)
        private bool _isSurveyPopup;
        private GameObject _surveyUIContainer;  // The "UI" CanvasGroup child (INACTIVE initially)
        private float _surveyPollTimer;
        private bool _surveyElementsDiscovered;

        // Virtual "View Game Log" element for MatchEnd screen
        private GameObject _viewLogElement;

        // Diagnostic: dump hierarchy once per activation
        private bool _dumpedHierarchy;

        // Poll-loop log suppression: the per-poll Discover* methods are chatty
        // (MatchEnd dumps ~20 lines per tick). Buffer their Log.Nav output during
        // the poll and flush only when the element set actually changed.
        private readonly List<string> _discoveryLogBuffer = new List<string>();
        private bool _bufferDiscoveryLogs;
        private string _lastDiscoverySignature = "";

        private void LogDiscovery(string msg)
        {
            if (_bufferDiscoveryLogs)
                _discoveryLogBuffer.Add(msg);
            else
                Log.Nav(NavigatorId, msg);
        }

        public LoadingScreenNavigator(IAnnouncementService announcer) : base(announcer) { }

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
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name == SceneNames.MatchEndScene && scene.isLoaded)
                    return true;
            }
            return false;
        }

        private bool DetectPreGame()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name == SceneNames.PreGameScene && scene.isLoaded)
                    return true;
            }
            return false;
        }

        private bool DetectGameLoading()
        {
            var scene = SceneManager.GetActiveScene();
            return scene.name == AssetPrep;
        }

        private bool DetectMatchmaking()
        {
            // Matchmaking detection: look for FindMatch active state with cancel button
            // The FindMatch UI appears as a blade/overlay in the MainNavigation scene
            var findMatchObj = GameObject.Find("FindMatchWaiting");
            if (findMatchObj != null && findMatchObj.activeInHierarchy)
            {
                Log.Nav(NavigatorId, "DetectMatchmaking: FindMatchWaiting found and active");
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
            LogDiscovery("=== Discovering MatchEnd elements ===");

            // Get MatchEndScene root objects - only search within this scene
            var matchEndScene = SceneManager.GetSceneByName(SceneNames.MatchEndScene);
            if (!matchEndScene.IsValid() || !matchEndScene.isLoaded)
            {
                LogDiscovery("MatchEndScene not valid/loaded");
                return;
            }

            var rootObjects = matchEndScene.GetRootGameObjects();
            LogDiscovery($"MatchEndScene root objects: {rootObjects.Length}");

            // Filter out CanvasPopup - it hosts survey/overlay popups, not MatchEnd content.
            // Without this, survey buttons can leak into MatchEnd elements during race conditions.
            var filteredRoots = rootObjects.Where(r => r.name != "CanvasPopup").ToArray();

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
            _matchResultText = ExtractMatchResultText(filteredRoots);
            LogDiscovery($"Match result: {_matchResultText}");

            // MatchEndScene uses EventTrigger (not Button/CustomButton) for its click targets.
            // ExitMatchOverlayButton starts INACTIVE and becomes active after animations.
            // Search for known elements by name, including inactive ones (poll for activation).

            // 1. Find ExitMatchOverlayButton (Continue / click to continue)
            foreach (var root in filteredRoots)
            {
                var exitButton = FindChildRecursive(root.transform, "ExitMatchOverlayButton");
                if (exitButton != null)
                {
                    if (exitButton.activeInHierarchy)
                    {
                        _continueButton = exitButton;
                        AddElement(exitButton, BuildLabel("Continue", Models.Strings.RoleButton, UIElementClassifier.ElementRole.Button), default, null, null, UIElementClassifier.ElementRole.Button);
                        LogDiscovery($"  ADDED: ExitMatchOverlayButton (active)");
                    }
                    else
                    {
                        LogDiscovery($"  ExitMatchOverlayButton found but INACTIVE (waiting)");
                    }
                }
            }

            // 2. Find any other clickable elements in MatchEndScene
            //    (CustomButton, Selectable, or EventTrigger-based)
            foreach (var root in filteredRoots)
            {
                // Search for EventTrigger components (MatchEndScene's click mechanism)
                foreach (var et in root.GetComponentsInChildren<EventTrigger>(false))
                {
                    if (et == null) continue;
                    var go = et.gameObject;
                    if (!go.activeInHierarchy) continue;
                    if (go == _continueButton) continue; // Already added
                    if (go.name == "ViewBattlefieldButton") continue; // Useless for blind players

                    string label = UITextExtractor.GetButtonText(go, null);
                    if (string.IsNullOrEmpty(label))
                        label = UITextExtractor.GetText(go);
                    if (string.IsNullOrEmpty(label))
                        label = go.name;

                    AddElement(go, BuildLabel(label, Models.Strings.RoleButton, UIElementClassifier.ElementRole.Button), default, null, null, UIElementClassifier.ElementRole.Button);
                    LogDiscovery($"  ADDED (EventTrigger): {go.name} -> '{label}'");
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

                    AddElement(go, BuildLabel(label, Models.Strings.RoleButton, UIElementClassifier.ElementRole.Button), default, null, null, UIElementClassifier.ElementRole.Button);
                    LogDiscovery($"  ADDED (CustomButton): {go.name} -> '{label}'");
                }
            }

            // 3. Collect rank info and other text elements.
            //    These appear after animations, so polling catches them.
            foreach (var root in filteredRoots)
            {
                // Find and combine rank info with progress data
                string rankText = FindTextByName(root, "Text_Rank");
                string rankFormat = FindTextByName(root, "Text_RankFormat");
                if (!string.IsNullOrEmpty(rankText))
                {
                    string rankLabel = !string.IsNullOrEmpty(rankFormat)
                        ? $"{rankFormat}: {rankText}"
                        : rankText;

                    // Read rank progress from RankDisplay component
                    string progressInfo = ExtractRankProgress(root);
                    if (!string.IsNullOrEmpty(progressInfo))
                        rankLabel = $"{rankLabel}, {progressInfo}";

                    // Use the Text_Rank GameObject as the navigable element
                    var rankObj = FindChildRecursive(root.transform, "Text_Rank");
                    if (rankObj != null)
                    {
                        AddElement(rankObj, rankLabel);
                        LogDiscovery($"  ADDED (info): Rank -> '{rankLabel}'");
                    }
                }
            }

            // 4. Virtual "View Game Log" element to review duel announcements.
            //    Replaces the visual-only "View Battlefield" button (useless for blind players).
            if (_viewLogElement == null)
                _viewLogElement = new GameObject("ViewLog_Virtual");
            AddElement(_viewLogElement, BuildLabel(Models.Strings.ViewGameLog, Models.Strings.RoleButton, UIElementClassifier.ElementRole.Button), default, null, null, UIElementClassifier.ElementRole.Button);
            LogDiscovery($"  ADDED (virtual): ViewLog_Virtual -> '{Models.Strings.ViewGameLog}'");

            // Settings button not added - accessible via Escape shortcut

            LogDiscovery($"=== MatchEnd discovery complete: {_elements.Count} elements ===");
        }

        private void DiscoverPreGameElements()
        {
            LogDiscovery("=== Discovering PreGame elements ===");

            var preGameScene = SceneManager.GetSceneByName(SceneNames.PreGameScene);
            if (!preGameScene.IsValid() || !preGameScene.isLoaded)
            {
                LogDiscovery("PreGameScene not valid/loaded");
                return;
            }

            var rootObjects = preGameScene.GetRootGameObjects();
            LogDiscovery($"PreGameScene root objects: {rootObjects.Length}");

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
                    LogDiscovery($"  ADDED (tip): {tipsLabel.gameObject.name} -> '{tipContent.Substring(0, System.Math.Min(50, tipContent.Length))}...'");
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
                    LogDiscovery($"  ADDED (timer): {combined}");
                }
            }
            else if (timerText != null && timerText.gameObject.activeInHierarchy)
            {
                // Timer visible but no queue detail label
                string timerValue = timerText.text?.Trim() ?? "";
                if (!string.IsNullOrEmpty(timerValue))
                {
                    AddElement(timerText.gameObject, timerValue);
                    LogDiscovery($"  ADDED (timer only): {timerValue}");
                }
            }

            // 3. Match found text (appears when opponent found)
            if (matchFoundText != null)
            {
                string matchText = matchFoundText.text?.Trim();
                if (!string.IsNullOrEmpty(matchText))
                {
                    AddElement(matchFoundText.gameObject, matchText);
                    LogDiscovery($"  ADDED (match found): {matchText}");
                }
            }

            // Store cancel button reference for Backspace shortcut (not added as navigable element)
            _cancelButton = cancelButtonObj;
            // Settings button not added - accessible via Escape shortcut

            LogDiscovery($"=== PreGame discovery complete: {_elements.Count} elements ===");
        }

        private void DiscoverMatchmakingElements()
        {
            LogDiscovery("=== Discovering Matchmaking elements ===");

            var waitingObj = GameObject.Find("FindMatchWaiting");
            if (waitingObj == null) return;

            // Store cancel button reference for Backspace shortcut (not added as navigable element)
            var cancelButton = FindChildRecursive(waitingObj.transform, "CancelButton")
                ?? FindChildRecursive(waitingObj.transform, "Cancel")
                ?? FindChildRecursive(waitingObj.transform, "Button_Cancel");

            if (cancelButton != null && cancelButton.activeInHierarchy)
            {
                _cancelButton = cancelButton;
                LogDiscovery($"  Found cancel button: {cancelButton.name} (Backspace shortcut only)");
            }

            LogDiscovery($"=== Matchmaking discovery complete: {_elements.Count} elements ===");
        }

        private void DiscoverGameLoadingElements()
        {
            LogDiscovery("=== Discovering GameLoading elements ===");

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
                    LogDiscovery($"  ADDED (status): {status}");
                }
            }

            LogDiscovery($"=== GameLoading discovery complete: {_elements.Count} elements ===");
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
                    LogDiscovery($"  Text in scene: '{content}' on {text.gameObject.name}");

                    // Look for victory/defeat keywords (supports multiple languages)
                    string lower = content.ToLowerInvariant();
                    if (lower.Contains("victory") || lower.Contains("defeat") ||
                        lower.Contains("draw") || lower.Contains("concede") ||
                        lower.Contains("win") || lower.Contains("lose") ||
                        lower.Contains("lost") || lower.Contains("won") ||
                        // German
                        lower.Contains("sieg") || lower.Contains("niederlage"))
                    {
                        LogDiscovery($"  Result text candidate: '{content}'");
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
                        return Strings.WithHint(result, "NavigateHint") + $" {Strings.ItemCount(_elements.Count)}.";
                    return result;

                case ScreenMode.PreGame:
                    if (_elements.Count > 0)
                        return Strings.WithHint(Strings.ScreenSearchingForMatch, "NavigateHint") + $" {Strings.ItemCount(_elements.Count)}.";
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
            // O key: open game log on match end screen
            if (_currentMode == ScreenMode.MatchEnd && Input.GetKeyDown(KeyCode.O))
            {
                var logNav = AccessibleArenaMod.Instance?.GameLogNavigator;
                if (logNav != null)
                    logNav.Open();
                return true;
            }

            // Backspace: quick action per mode
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                switch (_currentMode)
                {
                    case ScreenMode.MatchEnd:
                        // Activate Continue (back to menu)
                        if (_continueButton != null && _continueButton.activeInHierarchy)
                        {
                            Log.Nav(NavigatorId, "Backspace -> activating Continue button");
                            UIActivator.SimulatePointerClick(_continueButton);
                        }
                        else
                        {
                            // "Click anywhere to continue" - simulate screen center click
                            Log.Nav(NavigatorId, "Backspace -> simulating screen center click (click to continue)");
                            UIActivator.SimulateScreenCenterClick();
                        }
                        return true;

                    case ScreenMode.PreGame:
                    case ScreenMode.Matchmaking:
                        // Activate Cancel
                        if (_cancelButton != null && _cancelButton.activeInHierarchy)
                        {
                            Log.Nav(NavigatorId, "Backspace -> activating Cancel button");
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

            // Virtual "View Game Log" element — open log navigator instead of clicking
            if (element == _viewLogElement)
            {
                Log.Nav(NavigatorId, "Activating View Game Log");
                var logNav = AccessibleArenaMod.Instance?.GameLogNavigator;
                if (logNav != null)
                    logNav.Open();
                return true;
            }

            // Use SimulatePointerClick for MatchEnd buttons (StyledButton/PromptButton)
            if (_currentMode == ScreenMode.MatchEnd)
            {
                Log.Nav(NavigatorId, $"Activating MatchEnd button: {element.name}");
                UIActivator.SimulatePointerClick(element);
                return true;
            }

            // PreGame: only buttons are actionable (Cancel, Settings)
            if (_currentMode == ScreenMode.PreGame)
            {
                if (_elements[index].Role == UIElementClassifier.ElementRole.Button)
                {
                    Log.Nav(NavigatorId, $"Activating PreGame button: {element.name}");
                    UIActivator.Activate(element);
                    return true;
                }
                return true; // Consume Enter on info elements (no action)
            }

            return false;
        }

        protected override void OnPopupDetected(PanelInfo panel)
        {
            if (panel?.GameObject == null) return;

            // GameEndSurveyPopup: interactive survey with Good/Bad/Skip buttons.
            // UI elements start INACTIVE (animator intro), so we poll for activation.
            if (panel.Name.Contains("Survey"))
            {
                Log.Nav(NavigatorId, $"Survey popup detected: {panel.Name} — entering survey popup mode");
                _isSurveyPopup = true;
                EnterPopupMode(panel.GameObject);
                return;
            }

            base.OnPopupDetected(panel);
        }

        protected override void DiscoverPopupElements(GameObject popup)
        {
            if (_isSurveyPopup)
            {
                DiscoverSurveyElements(popup);
                return;
            }

            base.DiscoverPopupElements(popup);
        }

        /// <summary>
        /// Discover survey popup elements. The survey UI is initially INACTIVE (animator intro),
        /// so we poll for activation. Button_Good/Button_Bad have no text children (emoji faces only),
        /// so we use our own localized labels.
        /// </summary>
        private void DiscoverSurveyElements(GameObject popup)
        {
            _surveyElementsDiscovered = false;
            _surveyUIContainer = null;

            // Find the "UI" CanvasGroup child (contains all interactive elements)
            foreach (Transform child in popup.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "UI" && child.GetComponent<CanvasGroup>() != null)
                {
                    _surveyUIContainer = child.gameObject;
                    break;
                }
            }

            // Read title text even when inactive (TMP_Text.text is populated by Localize component)
            string titleText = null;
            foreach (var tmp in popup.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp != null && tmp.gameObject.name == "Text_Title")
                {
                    titleText = tmp.text?.Trim();
                    break;
                }
            }

            Log.Nav(NavigatorId, $"Survey: UI container {(_surveyUIContainer != null ? "found" : "NOT found")}, " +
                $"active={_surveyUIContainer?.activeInHierarchy}, title='{titleText}'");

            if (_surveyUIContainer != null && _surveyUIContainer.activeInHierarchy)
            {
                // UI is already active — discover real interactive elements
                DiscoverActiveSurveyElements(popup, titleText);
            }
            else
            {
                // UI still inactive (animator intro playing) — show title + hint, start polling
                string label = !string.IsNullOrEmpty(titleText) ? titleText : "Survey";
                _elements.Add(new NavigableElement
                {
                    GameObject = popup,
                    Label = Strings.WithHint(label, "SurveyHint"),
                    Role = UIElementClassifier.ElementRole.TextBlock
                });

                _surveyPollTimer = 0.3f;
                Log.Nav(NavigatorId, "Survey: UI inactive, polling for activation");
            }
        }

        /// <summary>
        /// Discover survey elements when the UI CanvasGroup is active.
        /// </summary>
        private void DiscoverActiveSurveyElements(GameObject popup, string titleText)
        {
            // 1. Title as info text
            if (!string.IsNullOrEmpty(titleText))
            {
                var titleObj = FindChildRecursive(popup.transform, "Text_Title");
                _elements.Add(new NavigableElement
                {
                    GameObject = titleObj,
                    Label = Strings.WithHint(titleText, "SurveyHint"),
                    Role = UIElementClassifier.ElementRole.TextBlock
                });
            }

            // 2. Good button (emoji face only — use our localized label)
            var goodButton = FindChildRecursive(popup.transform, "Button_Good");
            if (goodButton != null && goodButton.activeInHierarchy)
            {
                _elements.Add(new NavigableElement
                {
                    GameObject = goodButton,
                    Label = BuildLabel(Strings.SurveyGood, Models.Strings.RoleButton, UIElementClassifier.ElementRole.Button),
                    Role = UIElementClassifier.ElementRole.Button
                });
                Log.Nav(NavigatorId, $"Survey: ADDED Button_Good -> '{Strings.SurveyGood}'");
            }

            // 3. Bad button (emoji face only — use our localized label)
            var badButton = FindChildRecursive(popup.transform, "Button_Bad");
            if (badButton != null && badButton.activeInHierarchy)
            {
                _elements.Add(new NavigableElement
                {
                    GameObject = badButton,
                    Label = BuildLabel(Strings.SurveyBad, Models.Strings.RoleButton, UIElementClassifier.ElementRole.Button),
                    Role = UIElementClassifier.ElementRole.Button
                });
                Log.Nav(NavigatorId, $"Survey: ADDED Button_Bad -> '{Strings.SurveyBad}'");
            }

            // 4. Skip button — read label from child Text TMP_Text (game-localized "Skip")
            var skipButton = FindChildRecursive(popup.transform, "Button_Secondary");
            if (skipButton != null && skipButton.activeInHierarchy)
            {
                string skipLabel = FindTextByName(popup, "Text") ?? "Skip";
                // The generic "Text" name might match wrong elements. Scope to Button_Secondary's children.
                var skipTextObj = FindChildRecursive(skipButton.transform, "Text");
                if (skipTextObj != null)
                {
                    var skipTmp = skipTextObj.GetComponent<TMP_Text>();
                    if (skipTmp != null && !string.IsNullOrEmpty(skipTmp.text?.Trim()))
                        skipLabel = skipTmp.text.Trim();
                }

                _elements.Add(new NavigableElement
                {
                    GameObject = skipButton,
                    Label = BuildLabel(skipLabel, Models.Strings.RoleButton, UIElementClassifier.ElementRole.Button),
                    Role = UIElementClassifier.ElementRole.Button
                });
                Log.Nav(NavigatorId, $"Survey: ADDED Button_Secondary -> '{skipLabel}'");
            }

            _surveyElementsDiscovered = true;
            Log.Nav(NavigatorId, $"Survey: {_elements.Count} elements discovered (UI active)");
        }

        protected override void OnPopupClosed()
        {
            _isSurveyPopup = false;
            _surveyUIContainer = null;
            _surveyElementsDiscovered = false;
            if (_currentMode != ScreenMode.MatchEnd) return;

            Log.Nav(NavigatorId, "Survey popup closed, re-discovering elements");
            _elements.Clear();
            _currentIndex = -1;
            DiscoverElements();
            _lastDiscoverySignature = string.Join("|", _elements.Select(e => e.Label));

            // Restart polling to catch ExitMatchOverlayButton becoming active
            _pollElapsed = 0f;
            _pollTimer = 0.1f;
            _polling = true;
            _lastElementCount = _elements.Count;

            if (_elements.Count > 0)
            {
                _currentIndex = 0;
                UpdateEventSystemSelection();
            }
            _announcer.AnnounceInterrupt(GetActivationAnnouncement());
        }

        #endregion

        #region Polling (Update)

        protected override void OnActivated()
        {
            StartPolling();
            if (_currentMode == ScreenMode.MatchEnd || _currentMode == ScreenMode.GameLoading)
            {
                EnablePopupDetection();
                // Neither MatchEnd nor GameLoading have non-popup panels, so IsSceneLoading
                // never clears naturally. Clear it so AlphaDetector can detect popups
                // (MatchEnd survey, GameLoading SystemMessageView for forced-restart etc).
                PanelStateManager.Instance?.ClearSceneLoadingGate();
            }
        }

        private void StartPolling()
        {
            _polling = true;
            _pollTimer = PollInterval;
            _pollElapsed = 0f;
            _lastElementCount = _elements.Count;
            Log.Nav(NavigatorId, $"Polling started, initial elements: {_lastElementCount}");
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
                    // Seed the discovery signature so subsequent identical polls don't
                    // re-flush their buffered log lines.
                    _lastDiscoverySignature = string.Join("|", _elements.Select(e => e.Label));
                    _isActive = true;
                    _currentIndex = _elements.Count > 0 ? 0 : -1;
                    OnActivated();

                    if (_elements.Count > 0)
                        UpdateEventSystemSelection();

                    // Track initial status text for GameLoading
                    if (_currentMode == ScreenMode.GameLoading && _elements.Count > 0)
                        _lastLoadingStatusText = _elements[0].Label;

                    _announcer.AnnounceInterrupt(GetActivationAnnouncement());
                    Log.Nav(NavigatorId, $"Activated with {_elements.Count} elements");
                }
                return;
            }

            // Run base Update for input handling, validation, etc.
            base.Update();

            // Survey popup: poll for UI CanvasGroup activation (animator intro delay)
            if (_isActive && _isSurveyPopup && IsInPopupMode && !_surveyElementsDiscovered)
            {
                _surveyPollTimer -= Time.deltaTime;
                if (_surveyPollTimer <= 0)
                {
                    _surveyPollTimer = 0.3f;
                    if (_surveyUIContainer != null && _surveyUIContainer.activeInHierarchy)
                    {
                        Log.Nav(NavigatorId, "Survey: UI became active, rediscovering elements");

                        // Read title from TMP_Text
                        string titleText = null;
                        foreach (var tmp in PopupGameObject.GetComponentsInChildren<TMP_Text>(true))
                        {
                            if (tmp != null && tmp.gameObject.name == "Text_Title")
                            {
                                titleText = tmp.text?.Trim();
                                break;
                            }
                        }

                        _elements.Clear();
                        _currentIndex = -1;
                        DiscoverActiveSurveyElements(PopupGameObject, titleText);

                        if (_elements.Count > 0)
                        {
                            // Focus first actionable (button), skip title text block
                            int firstButton = _elements.FindIndex(e => e.Role == UIElementClassifier.ElementRole.Button);
                            _currentIndex = firstButton >= 0 ? firstButton : 0;
                        }

                        // Re-announce with discovered elements
                        _announcer.AnnounceInterrupt($"Popup: {titleText ?? "Survey"}. {Strings.ItemCount(_elements.Count)}.");
                        if (_currentIndex >= 0 && _currentIndex < _elements.Count)
                            _announcer.Announce(_elements[_currentIndex].Label, AnnouncementPriority.Normal);
                    }
                }
            }

            if (!_isActive || !_polling || IsInPopupMode) return;

            // Poll for new elements
            _pollElapsed += Time.deltaTime;
            _pollTimer -= Time.deltaTime;

            if (_pollTimer <= 0)
            {
                _pollTimer = _currentMode == ScreenMode.GameLoading ? GameLoadingPollInterval : PollInterval;

                // Preserve current navigation position
                int savedIndex = _currentIndex;

                // Re-discover elements and check if count changed.
                // Buffer the chatty discovery logs during polling; flush only when the
                // element set actually changed (otherwise the poll spams identical
                // lines every ~0.5s for the whole lifetime of the screen).
                _elements.Clear();
                _currentIndex = -1;
                _bufferDiscoveryLogs = true;
                _discoveryLogBuffer.Clear();
                DiscoverElements();
                _bufferDiscoveryLogs = false;

                string sig = string.Join("|", _elements.Select(e => e.Label));
                if (sig != _lastDiscoverySignature)
                {
                    foreach (var line in _discoveryLogBuffer)
                        Log.Nav(NavigatorId, line);
                    _lastDiscoverySignature = sig;
                }
                _discoveryLogBuffer.Clear();

                // Restore index (clamped to new range)
                if (_elements.Count > 0)
                    _currentIndex = System.Math.Min(savedIndex, _elements.Count - 1);
                if (_currentIndex < 0 && _elements.Count > 0)
                    _currentIndex = 0;

                if (_elements.Count != _lastElementCount)
                {
                    Log.Nav(NavigatorId, $"Poll: element count changed {_lastElementCount} -> {_elements.Count}");
                    _lastElementCount = _elements.Count;

                    if (_elements.Count > 0)
                        UpdateEventSystemSelection();

                    // GameLoading: announce bare status label at polite priority. Same-text
                    // dedup in AnnouncementService swallows repeats of the same status
                    // (e.g. "Kartendatenbank wird geladen" ticking for many seconds), while
                    // distinct boot steps get spoken once each. Skip the screen-name prefix
                    // and interrupt path — those would defeat the dedup.
                    // PreGame: stay silent — the initial "Suche nach Gegner" is enough;
                    // tips/timer churn doesn't warrant re-announcing the screen name.
                    if (_currentMode == ScreenMode.GameLoading)
                    {
                        if (_elements.Count > 0)
                        {
                            _lastLoadingStatusText = _elements[0].Label;
                            _announcer.Announce(_lastLoadingStatusText, AnnouncementPriority.Normal);
                        }
                    }
                    else if (_currentMode == ScreenMode.PreGame)
                    {
                        if (_elements.Count > 0)
                        {
                            _lastLoadingStatusText = _elements[0].Label;
                            Log.Nav(NavigatorId, $"Status update (silent): {_lastLoadingStatusText}");
                        }
                    }
                    else
                    {
                        _announcer.AnnounceInterrupt(GetActivationAnnouncement());
                    }
                }
                else if (_currentMode == ScreenMode.GameLoading && _elements.Count > 0)
                {
                    string currentLabel = _elements[0].Label;
                    if (currentLabel != _lastLoadingStatusText)
                    {
                        _lastLoadingStatusText = currentLabel;
                        _announcer.Announce(currentLabel, AnnouncementPriority.Normal);
                    }
                }

                // Stop polling after timeout (PreGame and GameLoading keep polling)
                if (_currentMode != ScreenMode.PreGame && _currentMode != ScreenMode.GameLoading && _pollElapsed >= MaxPollDuration)
                {
                    Log.Nav(NavigatorId, $"Polling timeout reached ({MaxPollDuration}s), stopping");
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
                Log.Nav(NavigatorId, "Settings menu detected - deactivating");
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
            Log.Nav(NavigatorId, $"OnSceneChanged: {sceneName}");

            // If active, check whether the current mode is still valid before deactivating.
            // Multiple scenes load in rapid succession during matchmaking (MatchScene, PreGameScene,
            // Battlefield_OM1) — we must not deactivate+reactivate on each one.
            if (_isActive)
            {
                bool modeStillValid = false;
                switch (_currentMode)
                {
                    case ScreenMode.PreGame:
                        modeStillValid = DetectPreGame();
                        break;
                    case ScreenMode.MatchEnd:
                        modeStillValid = DetectMatchEnd();
                        break;
                }

                if (modeStillValid)
                {
                    Log.Nav(NavigatorId, $"Mode {_currentMode} still valid after scene change to {sceneName}, staying active");
                    return;
                }

                // Leaving MatchEnd: clear duel log history so it doesn't go stale
                if (_currentMode == ScreenMode.MatchEnd)
                {
                    Log.Nav(NavigatorId, "Leaving MatchEnd — clearing duel log history");
                    _announcer.ClearHistory();

                    // Close game log navigator if still open
                    var logNav = AccessibleArenaMod.Instance?.GameLogNavigator;
                    if (logNav != null && logNav.IsActive)
                        logNav.Close();
                }

                Deactivate();
            }

            // Full reset
            _polling = false;
            _currentMode = ScreenMode.None;
            _matchResultText = "";
            _continueButton = null;
            _cancelButton = null;
            _timerText = null;
            _loadingInfoText = null;
            _lastLoadingStatusText = "";
            _isSurveyPopup = false;
            _surveyUIContainer = null;
            _surveyElementsDiscovered = false;
            _dumpedHierarchy = false;
            _lastDiscoverySignature = "";
            _discoveryLogBuffer.Clear();
            _bufferDiscoveryLogs = false;

            // Clean up virtual View Log element
            if (_viewLogElement != null)
            {
                Object.Destroy(_viewLogElement);
                _viewLogElement = null;
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
            text = UITextExtractor.StripRichText(text);
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
        /// Extract rank progress info from the RankDisplay component on the match end screen.
        /// Reads pip animator parameters and RankProgress data via reflection.
        /// Returns a formatted string like "4 of 6 wins" or "Rank up! Gold Tier 1" or null.
        /// </summary>
        private string ExtractRankProgress(GameObject root)
        {
            try
            {
                // Find RankDisplay MonoBehaviour in the hierarchy
                MonoBehaviour rankDisplay = null;
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb != null && mb.GetType().Name == "RankDisplay")
                    {
                        rankDisplay = mb;
                        break;
                    }
                }
                if (rankDisplay == null)
                {
                    LogDiscovery("  RankProgress: No RankDisplay found");
                    return null;
                }

                var rdType = rankDisplay.GetType();

                // Read RankUp (public bool field)
                bool rankUp = false;
                var rankUpField = rdType.GetField("RankUp", PublicInstance);
                if (rankUpField != null)
                    rankUp = (bool)rankUpField.GetValue(rankDisplay);

                // Read _rankProgress (private field) for old/new rank data
                var progressField = rdType.GetField("_rankProgress", PrivateInstance);
                object rankProgress = progressField?.GetValue(rankDisplay);

                // Read pip data from private fields on RankDisplay itself
                int maxPips = GetIntField(rdType, rankDisplay, "maxPips");
                int oldStep = GetIntField(rdType, rankDisplay, "oldStep");
                int newStep = GetIntField(rdType, rankDisplay, "newStep");

                LogDiscovery($"  RankProgress: rankUp={rankUp}, oldStep={oldStep}, newStep={newStep}, maxPips={maxPips}");

                if (rankProgress != null)
                {
                    var progressType = rankProgress.GetType();
                    int oldClass = GetIntField(progressType, rankProgress, "oldClass");
                    int newClass = GetIntField(progressType, rankProgress, "newClass");
                    int oldLevel = GetIntField(progressType, rankProgress, "oldLevel");
                    int newLevel = GetIntField(progressType, rankProgress, "newLevel");
                    int seasonOrdinal = GetIntField(progressType, rankProgress, "seasonOrdinal");

                    LogDiscovery($"  RankProgress: oldClass={oldClass} lvl={oldLevel}, newClass={newClass} lvl={newLevel}, season={seasonOrdinal}");

                    // No ranked data (unranked/casual match)
                    if (seasonOrdinal == 0) return null;

                    // Rank up: class or tier changed upward
                    if (rankUp)
                    {
                        string newRankText = ReadNewRankText(root, rdType, rankDisplay);
                        if (!string.IsNullOrEmpty(newRankText))
                            return Strings.RankUp(newRankText);
                    }

                    // Rank down: class went down, or same class but tier went up numerically (tier 1 > tier 2 = demotion)
                    bool rankDown = newClass < oldClass ||
                                    (newClass == oldClass && newLevel > oldLevel);
                    if (rankDown)
                    {
                        string newRankText = ReadNewRankText(root, rdType, rankDisplay);
                        if (!string.IsNullOrEmpty(newRankText))
                            return Strings.RankDown(newRankText);
                    }

                    // Mythic: show percentile or placement instead of wins
                    if (newClass == 7) // RankingClassType.Mythic
                    {
                        return ExtractMythicProgress(root);
                    }
                }

                // Normal case: show steps progress (newStep of maxPips)
                if (maxPips > 0)
                    return Strings.RankStepsProgress(newStep, maxPips);

                return null;
            }
            catch (System.Exception ex)
            {
                LogDiscovery($"  RankProgress error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Read the new rank text from the NewRankText TMP field on MatchEndDisplay,
        /// or fall back to the _rankTierText on RankDisplay.
        /// </summary>
        private string ReadNewRankText(GameObject root, System.Type rdType, MonoBehaviour rankDisplay)
        {
            // Try NewRankText on parent MatchEndDisplay first (set during rank-up animation)
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && mb.GetType().Name == "MatchEndDisplay")
                {
                    var newRankTextField = mb.GetType().GetField("NewRankText", PublicInstance);
                    if (newRankTextField != null)
                    {
                        var tmpText = newRankTextField.GetValue(mb) as TMP_Text;
                        string text = tmpText?.text?.Trim();
                        if (!string.IsNullOrEmpty(text))
                        {
                            LogDiscovery($"  RankProgress: NewRankText = '{text}'");
                            return text;
                        }
                    }
                    break;
                }
            }

            // Fall back to _rankTierText on RankDisplay (shows current rank tier)
            var tierTextField = rdType.GetField("_rankTierText", PrivateInstance);
            if (tierTextField != null)
            {
                var tmpText = tierTextField.GetValue(rankDisplay) as TMP_Text;
                string text = tmpText?.text?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    LogDiscovery($"  RankProgress: _rankTierText = '{text}'");
                    return text;
                }
            }

            return null;
        }

        /// <summary>
        /// Extract Mythic-specific progress (percentile or leaderboard placement)
        /// from the _mythicPlacementText on RankDisplay.
        /// </summary>
        private string ExtractMythicProgress(GameObject root)
        {
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && mb.GetType().Name == "RankDisplay")
                {
                    var mythicTextField = mb.GetType().GetField("_mythicPlacementText", PrivateInstance);
                    if (mythicTextField != null)
                    {
                        var tmpText = mythicTextField.GetValue(mb) as TMP_Text;
                        string text = tmpText?.text?.Trim();
                        if (!string.IsNullOrEmpty(text))
                        {
                            LogDiscovery($"  RankProgress: MythicText = '{text}'");
                            return text; // Already formatted as "#1234" or "95%"
                        }
                    }
                    break;
                }
            }
            return null;
        }

        /// <summary>
        /// Read an int field from an object, returning 0 if not found.
        /// Tries public first, then private instance.
        /// </summary>
        private int GetIntField(System.Type type, object obj, string fieldName)
        {
            var field = type.GetField(fieldName, PublicInstance)
                     ?? type.GetField(fieldName, PrivateInstance);
            if (field != null)
                return System.Convert.ToInt32(field.GetValue(obj));
            return 0;
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
            Log.Nav(NavigatorId, $"  HIERARCHY: {indent}{t.name}{compStr}{activeStr}");

            foreach (Transform child in t)
            {
                DumpHierarchy(child, depth + 1, maxDepth);
            }
        }

        #endregion
    }
}
