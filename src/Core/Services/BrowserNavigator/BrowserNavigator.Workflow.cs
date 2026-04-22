using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using MelonLoader;
using AccessibleArena.Core.Models;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    public partial class BrowserNavigator
    {
        // Browser confirm guard: require double Space press in selection browsers
        private bool _browserConfirmWarning;
        private bool _browserConfirmWaitRelease;

        /// <summary>
        /// Tries to submit the current workflow via reflection by accessing GameManager.WorkflowController.
        /// This bypasses the need to click UI elements that may not have standard click handlers.
        /// </summary>
        /// <returns>True if workflow was successfully submitted</returns>
        private bool TrySubmitWorkflowViaReflection()
        {
            var flags = AllInstanceFlags;

            try
            {
                // Find GameManager
                MonoBehaviour gameManager = null;
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == "GameManager")
                    {
                        gameManager = mb;
                        break;
                    }
                }

                if (gameManager == null)
                {
                    Log.Msg("BrowserNavigator", $"WorkflowReflection: GameManager not found");
                    if (BrowserDetector.IsDebugEnabled(BrowserDetector.BrowserTypeWorkflow))
                        MenuDebugHelper.DumpWorkflowSystemDebug("WorkflowDebug");
                    return false;
                }

                // Get WorkflowController
                var wcProp = gameManager.GetType().GetProperty("WorkflowController", flags);
                var workflowController = wcProp?.GetValue(gameManager);

                if (workflowController == null)
                {
                    Log.Msg("BrowserNavigator", $"WorkflowReflection: WorkflowController not found");
                    if (BrowserDetector.IsDebugEnabled(BrowserDetector.BrowserTypeWorkflow))
                        MenuDebugHelper.DumpWorkflowSystemDebug("WorkflowDebug");
                    return false;
                }

                // Get CurrentInteraction - try both property and field
                var wcType = workflowController.GetType();
                object currentInteraction = null;

                // Try property first
                var ciProp = wcType.GetProperty("CurrentInteraction", flags);
                if (ciProp != null)
                {
                    currentInteraction = ciProp.GetValue(workflowController);
                }

                // Try field if property didn't work
                if (currentInteraction == null)
                {
                    var ciField = wcType.GetField("_currentInteraction", flags)
                               ?? wcType.GetField("currentInteraction", flags)
                               ?? wcType.GetField("_current", flags);
                    if (ciField != null)
                    {
                        currentInteraction = ciField.GetValue(workflowController);
                    }
                }

                if (currentInteraction == null)
                {
                    Log.Msg("BrowserNavigator", $"WorkflowReflection: No active workflow found");
                    if (BrowserDetector.IsDebugEnabled(BrowserDetector.BrowserTypeWorkflow))
                        MenuDebugHelper.DumpWorkflowSystemDebug("WorkflowDebug");
                    return false;
                }

                var workflowType = currentInteraction.GetType();
                Log.Msg("BrowserNavigator", $"WorkflowReflection: Found workflow: {workflowType.Name}");

                // Try to submit via _request.SubmitSolution()
                var requestField = workflowType.GetField("_request", flags);
                if (requestField != null)
                {
                    var request = requestField.GetValue(currentInteraction);
                    if (request != null)
                    {
                        // Find solution field
                        var solutionField = workflowType.GetField("_autoTapSolution", flags)
                                         ?? workflowType.GetField("autoTapSolution", flags);
                        var solutionProp = workflowType.GetProperty("AutoTapSolution", flags)
                                        ?? workflowType.GetProperty("Solution", flags);

                        object solution = solutionField?.GetValue(currentInteraction)
                                       ?? solutionProp?.GetValue(currentInteraction);

                        // Try SubmitSolution
                        var submitMethod = request.GetType().GetMethod("SubmitSolution", flags);
                        if (submitMethod != null)
                        {
                            var parameters = submitMethod.GetParameters();
                            if (parameters.Length == 0)
                            {
                                submitMethod.Invoke(request, null);
                                Log.Msg("BrowserNavigator", $"WorkflowReflection: Called SubmitSolution()");
                                return true;
                            }
                            else if (parameters.Length == 1 && solution != null)
                            {
                                submitMethod.Invoke(request, new[] { solution });
                                Log.Msg("BrowserNavigator", $"WorkflowReflection: Called SubmitSolution(solution)");
                                return true;
                            }
                        }
                    }
                }

                // Try direct Submit/Confirm methods on workflow
                foreach (var methodName in new[] { "Submit", "Confirm", "Complete", "Accept", "Close" })
                {
                    var method = workflowType.GetMethod(methodName, flags);
                    if (method != null && method.GetParameters().Length == 0)
                    {
                        method.Invoke(currentInteraction, null);
                        Log.Msg("BrowserNavigator", $"WorkflowReflection: Called {methodName}()");
                        return true;
                    }
                }

                // If nothing worked, log failure
                Log.Msg("BrowserNavigator", $"WorkflowReflection: Could not find submit method");
                if (BrowserDetector.IsDebugEnabled(BrowserDetector.BrowserTypeWorkflow))
                    MenuDebugHelper.DumpWorkflowSystemDebug("WorkflowDebug");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error("BrowserNavigator", $"WorkflowReflection error: {ex.Message}");
                if (BrowserDetector.IsDebugEnabled(BrowserDetector.BrowserTypeWorkflow))
                    MenuDebugHelper.DumpWorkflowSystemDebug("WorkflowDebug");
                return false;
            }
        }

        /// <summary>
        /// Tries to cancel the current workflow via reflection.
        /// First tries to close the ConfirmWidget (ability activation prompt),
        /// then falls back to invoking Cancelled on the workflow variant,
        /// then tries _request.Undo().
        /// </summary>
        /// <returns>True if workflow was successfully cancelled</returns>
        private bool TryCancelWorkflowViaReflection()
        {
            var flags = AllInstanceFlags;

            try
            {
                // Try 1: Find ConfirmWidget and call Cancel() on it
                var confirmWidgetType = FindType("ConfirmWidget");
                if (confirmWidgetType != null)
                {
                    var confirmWidgets = UnityEngine.Object.FindObjectsOfType(confirmWidgetType);
                    foreach (var cw in confirmWidgets)
                    {
                        if (cw == null) continue;
                        var isOpenProp = confirmWidgetType.GetProperty("IsOpen", PublicInstance);
                        if (isOpenProp != null && (bool)isOpenProp.GetValue(cw))
                        {
                            var cancelMethod = confirmWidgetType.GetMethod("Cancel", PublicInstance);
                            if (cancelMethod != null)
                            {
                                cancelMethod.Invoke(cw, null);
                                Log.Msg("BrowserNavigator", "WorkflowCancel: Cancelled ConfirmWidget");
                                return true;
                            }
                        }
                    }
                }

                // Try 2: Navigate to workflow variant and invoke Cancelled
                MonoBehaviour gameManager = null;
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == "GameManager")
                    {
                        gameManager = mb;
                        break;
                    }
                }

                if (gameManager == null)
                {
                    Log.Msg("BrowserNavigator", "WorkflowCancel: GameManager not found");
                    return false;
                }

                var wcProp = gameManager.GetType().GetProperty("WorkflowController", flags);
                var workflowController = wcProp?.GetValue(gameManager);
                if (workflowController == null)
                {
                    Log.Msg("BrowserNavigator", "WorkflowCancel: WorkflowController not found");
                    return false;
                }

                // Get CurrentInteraction
                var wcType = workflowController.GetType();
                object currentInteraction = null;

                var ciProp = wcType.GetProperty("CurrentInteraction", flags);
                if (ciProp != null)
                    currentInteraction = ciProp.GetValue(workflowController);

                if (currentInteraction == null)
                {
                    var ciField = wcType.GetField("_currentInteraction", flags)
                               ?? wcType.GetField("currentInteraction", flags)
                               ?? wcType.GetField("_current", flags);
                    if (ciField != null)
                        currentInteraction = ciField.GetValue(workflowController);
                }

                if (currentInteraction == null)
                {
                    Log.Msg("BrowserNavigator", "WorkflowCancel: No active workflow found");
                    return false;
                }

                var workflowType = currentInteraction.GetType();
                Log.Msg("BrowserNavigator", $"WorkflowCancel: Found workflow: {workflowType.Name}");

                // Try to find _currentVariant and invoke Cancelled
                var variantField = workflowType.GetField("_currentVariant", flags);
                if (variantField != null)
                {
                    var variant = variantField.GetValue(currentInteraction);
                    if (variant != null)
                    {
                        var cancelledField = variant.GetType().GetField("Cancelled", PublicInstance);
                        if (cancelledField != null)
                        {
                            var cancelled = cancelledField.GetValue(variant) as System.Action;
                            if (cancelled != null)
                            {
                                cancelled.Invoke();
                                Log.Msg("BrowserNavigator", "WorkflowCancel: Invoked Cancelled on variant");
                                return true;
                            }
                        }
                    }
                }

                // Try 3: Use _request.Undo() if available
                var requestField = workflowType.GetField("_request", flags);
                if (requestField != null)
                {
                    var request = requestField.GetValue(currentInteraction);
                    if (request != null)
                    {
                        var allowUndoProp = request.GetType().GetProperty("AllowUndo", flags);
                        if (allowUndoProp != null && (bool)allowUndoProp.GetValue(request))
                        {
                            var undoMethod = request.GetType().GetMethod("Undo", flags);
                            if (undoMethod != null && undoMethod.GetParameters().Length == 0)
                            {
                                undoMethod.Invoke(request, null);
                                Log.Msg("BrowserNavigator", "WorkflowCancel: Called _request.Undo()");
                                return true;
                            }
                        }
                    }
                }

                Log.Msg("BrowserNavigator", "WorkflowCancel: No cancel mechanism found");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error("BrowserNavigator", $"WorkflowCancel error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clicks the SelectCards/SelectCardsMultiZone scaffold button identified by
        /// its logical key ("DoneButton" for confirm, "CancelButton" for decline) in
        /// the provider's GetButtonStateData() dict.
        ///
        /// Why logical-key lookup, not ButtonStyle.StyleType.Main:
        /// When 0 cards are selected, the game collapses to a 1-button layout whose
        /// sole entry ("CancelButton") carries StyleType.Main — so a Main-based
        /// heuristic wrongly treats Cancel as Confirm. The dict key is authoritative.
        ///
        /// ButtonStateData.BrowserElementKey names the scaffold slot ("SingleButton",
        /// "2Button_Left", "2Button_Right"); we resolve it via BrowserBase.GetBrowserElement.
        /// Respects Enabled (e.g. DoneButton disabled when no valid selection).
        /// </summary>
        private bool TryClickProviderLogicalButton(string logicalKey, string logTag)
        {
            try
            {
                var currentBrowser = GetCurrentBrowser();
                if (currentBrowser == null)
                {
                    Log.Msg("BrowserNavigator", $"{logTag}: no current browser");
                    return false;
                }

                var dict = GetProviderButtonStateData(currentBrowser);
                if (dict == null)
                {
                    Log.Msg("BrowserNavigator", $"{logTag}: provider returned no button state data");
                    return false;
                }

                if (!dict.Contains(logicalKey))
                {
                    Log.Msg("BrowserNavigator", $"{logTag}: no '{logicalKey}' in dict (keys: {DictKeysToString(dict)})");
                    return false;
                }

                var stateData = dict[logicalKey];
                if (stateData == null)
                {
                    Log.Msg("BrowserNavigator", $"{logTag}: '{logicalKey}' state data is null");
                    return false;
                }

                var stateType = stateData.GetType();
                var elementKeyField = stateType.GetField("BrowserElementKey", PublicInstance);
                var scaffoldKey = elementKeyField?.GetValue(stateData) as string;
                if (string.IsNullOrEmpty(scaffoldKey))
                {
                    Log.Msg("BrowserNavigator", $"{logTag}: '{logicalKey}' has no BrowserElementKey");
                    return false;
                }

                var enabledField = stateType.GetField("Enabled", PublicInstance);
                var isActiveField = stateType.GetField("IsActive", PublicInstance);
                bool enabled = enabledField == null || (bool)enabledField.GetValue(stateData);
                bool isActive = isActiveField == null || (bool)isActiveField.GetValue(stateData);
                if (!isActive)
                {
                    Log.Msg("BrowserNavigator", $"{logTag}: '{logicalKey}' not active");
                    return false;
                }
                if (!enabled)
                {
                    Log.Msg("BrowserNavigator", $"{logTag}: '{logicalKey}' not enabled");
                    return false;
                }

                var element = GetBrowserElementByKey(currentBrowser, scaffoldKey);
                if (element == null)
                {
                    Log.Msg("BrowserNavigator", $"{logTag}: scaffold '{scaffoldKey}' not found");
                    DumpBrowserScaffoldDiagnostics(currentBrowser, dict, scaffoldKey, logTag);
                    return false;
                }
                if (!element.activeInHierarchy)
                {
                    Log.Msg("BrowserNavigator", $"{logTag}: scaffold '{scaffoldKey}' inactive");
                    return false;
                }
                var selectable = element.GetComponent<Selectable>();
                if (selectable != null && !selectable.interactable)
                {
                    Log.Msg("BrowserNavigator", $"{logTag}: scaffold '{scaffoldKey}' not interactable");
                    return false;
                }

                var result = UIActivator.SimulatePointerClick(element);
                if (result.Success)
                {
                    Log.Msg("BrowserNavigator", $"{logTag}: clicked '{scaffoldKey}' (logical='{logicalKey}')");
                    return true;
                }
                Log.Msg("BrowserNavigator", $"{logTag}: click failed on '{scaffoldKey}'");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error("BrowserNavigator", $"{logTag} error: {ex.Message}");
                return false;
            }
        }

        private static string DictKeysToString(IDictionary dict)
        {
            var keys = new List<string>();
            foreach (var k in dict.Keys) keys.Add(k?.ToString() ?? "<null>");
            return string.Join(",", keys);
        }

        /// <summary>
        /// Invokes IDuelSceneBrowserProvider.GetButtonStateData() on the browser's
        /// protected _duelSceneBrowserProvider field (declared on BrowserBase).
        /// Walks the type hierarchy since NonPublic|Instance reflection doesn't find
        /// inherited private/protected fields on derived types.
        /// </summary>
        private IDictionary GetProviderButtonStateData(object browser)
        {
            var providerField = FindFieldWalkingHierarchy(browser.GetType(), "_duelSceneBrowserProvider");
            if (providerField == null) return null;
            var provider = providerField.GetValue(browser);
            if (provider == null) return null;

            var method = provider.GetType().GetMethod("GetButtonStateData", PublicInstance, null, Type.EmptyTypes, null);
            if (method == null) return null;
            return method.Invoke(provider, null) as IDictionary;
        }

        private static FieldInfo FindFieldWalkingHierarchy(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var f = t.GetField(name, PrivateInstance);
                if (f != null) return f;
            }
            return null;
        }

        /// <summary>
        /// DIAGNOSTIC: when GetBrowserElement returns null for a scaffold key the
        /// provider's ButtonStateData points to, dump enough state to figure out why.
        /// Emits: browser concrete type, provider concrete type, ContainsKey result,
        /// and the full uiElementData key set with per-entry GameObject liveness.
        /// Remove once modal/kicker scaffold behaviour is understood.
        /// </summary>
        private void DumpBrowserScaffoldDiagnostics(object browser, IDictionary buttonDict, string missingKey, string logTag)
        {
            try
            {
                var browserType = browser.GetType();
                Log.Msg("BrowserNavigator", $"{logTag}[diag] browser.Type={browserType.FullName}");

                var providerField = FindFieldWalkingHierarchy(browserType, "_duelSceneBrowserProvider");
                var provider = providerField?.GetValue(browser);
                Log.Msg("BrowserNavigator", $"{logTag}[diag] provider.Type={provider?.GetType().FullName ?? "<null>"}");

                // Log each button dict entry with its BrowserElementKey
                if (buttonDict != null)
                {
                    foreach (var k in buttonDict.Keys)
                    {
                        var sd = buttonDict[k];
                        var bekField = sd?.GetType().GetField("BrowserElementKey", PublicInstance);
                        var styleField = sd?.GetType().GetField("StyleType", PublicInstance);
                        var enabledField = sd?.GetType().GetField("Enabled", PublicInstance);
                        var isActiveField = sd?.GetType().GetField("IsActive", PublicInstance);
                        Log.Msg("BrowserNavigator",
                            $"{logTag}[diag] dict['{k}'] BrowserElementKey='{bekField?.GetValue(sd)}' " +
                            $"Style={styleField?.GetValue(sd)} Enabled={enabledField?.GetValue(sd)} IsActive={isActiveField?.GetValue(sd)}");
                    }
                }

                // Dump uiElementData (protected field on BrowserBase)
                var uiField = FindFieldWalkingHierarchy(browserType, "uiElementData");
                if (uiField == null)
                {
                    Log.Msg("BrowserNavigator", $"{logTag}[diag] uiElementData field not found on hierarchy");
                    return;
                }
                var ui = uiField.GetValue(browser) as IDictionary;
                if (ui == null)
                {
                    Log.Msg("BrowserNavigator", $"{logTag}[diag] uiElementData is null");
                    return;
                }

                Log.Msg("BrowserNavigator", $"{logTag}[diag] uiElementData.Count={ui.Count} ContainsKey('{missingKey}')={ui.Contains(missingKey)}");

                foreach (var k in ui.Keys)
                {
                    var entry = ui[k];
                    var goField = entry?.GetType().GetField("GameObject", PublicInstance);
                    var goProp = entry?.GetType().GetProperty("GameObject", PublicInstance);
                    var go = (goField?.GetValue(entry) ?? goProp?.GetValue(entry)) as GameObject;
                    string state;
                    if (go == null)
                        state = "go=<null/destroyed>";
                    else
                        state = $"go.name='{go.name}' active={go.activeInHierarchy}";
                    Log.Msg("BrowserNavigator", $"{logTag}[diag] ui['{k}'] -> {state}");
                }

                // Also report scene-level GameObjects that share the missing name, so
                // we can compare against what _browserButtons discovered.
                int sceneHits = 0;
                foreach (var mb in GameObject.FindObjectsOfType<Transform>())
                {
                    if (mb != null && mb.name == missingKey)
                    {
                        sceneHits++;
                        Log.Msg("BrowserNavigator", $"{logTag}[diag] scene '{missingKey}' at path={GetTransformPath(mb)} active={mb.gameObject.activeInHierarchy}");
                        if (sceneHits >= 5) break;
                    }
                }
                Log.Msg("BrowserNavigator", $"{logTag}[diag] scene Transforms named '{missingKey}': total_logged={sceneHits}");
            }
            catch (Exception ex)
            {
                Log.Error("BrowserNavigator", $"{logTag}[diag] error: {ex.Message}");
            }
        }

        private static string GetTransformPath(Transform t)
        {
            var parts = new List<string>();
            for (var cur = t; cur != null; cur = cur.parent) parts.Add(cur.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private object GetCurrentBrowser()
        {
            var gm = FindGameManagerInstance();
            if (gm == null) return null;

            var bmProp = gm.GetType().GetProperty("BrowserManager", AllInstanceFlags);
            var browserManager = bmProp?.GetValue(gm);
            if (browserManager == null) return null;

            var cbProp = browserManager.GetType().GetProperty("CurrentBrowser", AllInstanceFlags);
            return cbProp?.GetValue(browserManager);
        }

        /// <summary>
        /// Reads BrowserBase.uiElementData[key].GameObject directly. The public
        /// BrowserBase.GetBrowserElement(string) method exists, but invoking it
        /// via reflection across the BrowserBase → SelectCardsBrowser hierarchy
        /// was returning null even when the key was present (diagnostic confirmed
        /// uiElementData contains the key with a live GameObject). Walking to the
        /// protected uiElementData field is the reliable path.
        /// </summary>
        private GameObject GetBrowserElementByKey(object browser, string key)
        {
            var uiField = FindFieldWalkingHierarchy(browser.GetType(), "uiElementData");
            var ui = uiField?.GetValue(browser) as IDictionary;
            if (ui == null || !ui.Contains(key)) return null;
            var entry = ui[key];
            if (entry == null) return null;
            var entryType = entry.GetType();
            var goProp = entryType.GetProperty("GameObject", PublicInstance);
            if (goProp != null) return goProp.GetValue(entry) as GameObject;
            var goField = entryType.GetField("GameObject", PublicInstance);
            return goField?.GetValue(entry) as GameObject;
        }

        private MonoBehaviour FindGameManagerInstance()
        {
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "GameManager")
                    return mb;
            }
            return null;
        }

        /// <summary>
        /// Clicks the confirm/primary button.
        /// </summary>
        private void ClickConfirmButton()
        {
            Log.Msg("BrowserNavigator", $"ClickConfirmButton called. Browser: {_browserInfo?.BrowserType}");

            // Signal re-entry on next frame in case the scaffold is reused for a new interaction
            _pendingRescan = true;

            string clickedLabel;

            // Workflow browser: try reflection approach to submit via WorkflowController
            if (_browserInfo?.IsWorkflow == true)
            {
                // First try the reflection approach (access WorkflowController directly)
                if (TrySubmitWorkflowViaReflection())
                {
                    Log.Msg("BrowserNavigator", $"Workflow submitted via reflection");
                    _announcer.Announce(Strings.Confirmed, AnnouncementPriority.Normal);
                    BrowserDetector.InvalidateCache();
                    return;
                }

                // Fallback: try clicking the button if reflection failed
                Log.Msg("BrowserNavigator", $"Reflection approach failed, trying button click");
                if (_browserButtons.Count > 0 && _currentButtonIndex >= 0)
                {
                    ActivateCurrentButton();
                }
                else
                {
                    _announcer.Announce(Strings.NoButtonSelected, AnnouncementPriority.Normal);
                }
                return;
            }

            // Direct-choice browsers: Space = Enter (activate focused item)
            // These browsers have no separate confirm action — clicking a button IS the choice.
            if (_isSelectGroup || _isChoiceList || _browserInfo?.IsOptionalAction == true)
            {
                if (_browserButtons.Count > 0 && _currentButtonIndex >= 0)
                    ActivateCurrentButton();
                else if (_browserCards.Count > 0 && _currentCardIndex >= 0)
                    ActivateCurrentCard();
                else
                    _announcer.Announce(Strings.NoButtonSelected, AnnouncementPriority.Normal);
                BrowserDetector.InvalidateCache();
                return;
            }

            // London mulligan: click SubmitButton (only if enough cards are on bottom)
            if (_browserInfo?.IsLondon == true)
            {
                // Read required count from game's LondonBrowser, fallback to mod-tracked count
                int required = _zoneNavigator.GetRequiredPutbackCount();
                if (required <= 0) required = _zoneNavigator.MulliganCount;
                int onBottom = _zoneNavigator.BottomCardCount;
                if (required > 0 && onBottom < required)
                {
                    int remaining = required - onBottom;
                    _announcer.Announce(Strings.Duel_NeedMoreForBottom(remaining), AnnouncementPriority.High);
                    _pendingRescan = false; // Don't rescan — nothing changed
                    return;
                }
                if (TryClickButtonByName(BrowserDetector.ButtonSubmit, out clickedLabel))
                {
                    _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                    BrowserDetector.InvalidateCache(); // Force re-detection on next Update
                    return;
                }
            }

            // Mulligan/opening hand: prioritize KeepButton
            if (_browserInfo?.IsMulligan == true)
            {
                if (TryClickButtonByName(BrowserDetector.ButtonKeep, out clickedLabel))
                {
                    _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                    BrowserDetector.InvalidateCache(); // Force re-detection on next Update
                    return;
                }
            }

            // SelectCards/SelectCardsMultiZone: prefer workflow reflection over button patterns.
            // SingleButton in these browsers is often the "Decline" option, not confirm.
            // The correct confirm is submitting the workflow with the current selection.
            if (_isHighlightFilteredBrowser && TrySubmitWorkflowViaReflection())
            {
                Log.Msg("BrowserNavigator", "SelectCards workflow submitted via reflection");
                _announcer.Announce(Strings.Confirmed, AnnouncementPriority.Normal);
                BrowserDetector.InvalidateCache();
                return;
            }

            // SelectCards/SelectCardsMultiZone: click the provider's "DoneButton" directly.
            // Needed when the provider isn't on WorkflowController.CurrentInteraction
            // (e.g. activated-ability cost-payment like Rubble Rouser "{T}, exile a card").
            // Uses the logical key from GetButtonStateData() rather than StyleType.Main,
            // because 1-button layouts flag Cancel as Main when nothing is selected.
            if (_isHighlightFilteredBrowser && TryClickProviderLogicalButton("DoneButton", "BrowserConfirm"))
            {
                _announcer.Announce(Strings.Confirmed, AnnouncementPriority.Normal);
                BrowserDetector.InvalidateCache();
                return;
            }

            // Try discovered buttons by name pattern (SubmitButton, ConfirmButton, etc.)
            // Skip for SelectCards/SelectCardsMultiZone: ConfirmPatterns contains "Single",
            // which wrongly matches "SingleButton". On kicker/modal scaffolds (e.g. Burst
            // Lightning), the sole SingleButton is logically the CancelButton — matching
            // it here would silently cancel the spell on Space.
            if (!_isHighlightFilteredBrowser && TryClickButtonByPatterns(BrowserDetector.ConfirmPatterns, out clickedLabel))
            {
                _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                BrowserDetector.InvalidateCache(); // Force re-detection on next Update
                return;
            }

            // Scaffold browsers backed by a workflow (e.g. SelectCards with 2-button layout):
            // scaffold buttons don't match ConfirmPatterns, but the underlying workflow can submit.
            if (!(_browserInfo?.IsWorkflow == true) && TrySubmitWorkflowViaReflection())
            {
                Log.Msg("BrowserNavigator", $"Scaffold workflow submitted via reflection");
                _announcer.Announce(Strings.Confirmed, AnnouncementPriority.Normal);
                BrowserDetector.InvalidateCache();
                return;
            }

            // Fallback: PromptButton_Primary (scene search)
            // Skip for OptionalAction, choice-list, SelectGroup, and SelectCards* browsers —
            // their buttons are choices, not confirm/cancel, and the global PromptButtons
            // would click unrelated duel phase buttons (observed: clicked duel-level "Cancel"
            // during an activated-ability cost-payment SelectCards prompt).
            if (!(_browserInfo?.IsOptionalAction == true) && !_isChoiceList && !_isSelectGroup && !_isHighlightFilteredBrowser && TryClickPromptButton(BrowserDetector.PromptButtonPrimaryPrefix, out clickedLabel))
            {
                // PromptButton_Primary is a duel-level button (pass/submit), not browser-internal.
                // Clicking it advances the game, which will destroy the scaffold.
                // Clear rescan to avoid stale re-announcement while scaffold lingers.
                _pendingRescan = false;
                _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                BrowserDetector.InvalidateCache(); // Force re-detection on next Update
                return;
            }

            _announcer.Announce(Strings.NoConfirmButton, AnnouncementPriority.Normal);
        }

        /// <summary>
        /// Clicks the cancel/secondary button.
        /// </summary>
        private void ClickCancelButton()
        {
            Log.Msg("BrowserNavigator", $"ClickCancelButton called. Browser: {_browserInfo?.BrowserType}");

            string clickedLabel;

            // Workflow browser: try reflection approach to cancel (ConfirmWidget, variant, or undo)
            if (_browserInfo?.IsWorkflow == true)
            {
                if (TryCancelWorkflowViaReflection())
                {
                    _announcer.Announce(Strings.Cancelled, AnnouncementPriority.Normal);
                    BrowserDetector.InvalidateCache();
                    return;
                }
            }

            // First priority: MulliganButton (doesn't close browser, starts new mulligan)
            if (TryClickButtonByName(BrowserDetector.ButtonMulligan, out clickedLabel))
            {
                // Track mulligan count for London phase
                _zoneNavigator.IncrementMulliganCount();
                _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                BrowserDetector.InvalidateCache(); // Browser will change to London
                return;
            }

            // Second priority: other cancel buttons by pattern
            if (TryClickButtonByPatterns(BrowserDetector.CancelPatterns, out clickedLabel))
            {
                _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                BrowserDetector.InvalidateCache(); // Force re-detection on next Update
                return;
            }

            // SelectCards/SelectCardsMultiZone: click the provider's "CancelButton" directly.
            // The logical key is authoritative — in 1-button layouts (0 selected),
            // SingleButton carries StyleType.Main but its logical key is "CancelButton",
            // so this correctly clicks decline without needing a Main/non-Main heuristic.
            if (_isHighlightFilteredBrowser && TryClickProviderLogicalButton("CancelButton", "BrowserCancel"))
            {
                _announcer.Announce(Strings.Cancelled, AnnouncementPriority.Normal);
                BrowserDetector.InvalidateCache();
                return;
            }

            // Third priority: PromptButton_Secondary
            // Skip for OptionalAction, choice-list, SelectCards browsers — would click unrelated duel phase buttons
            if (!(_browserInfo?.IsOptionalAction == true) && !_isChoiceList && !_isSelectGroup && !_isHighlightFilteredBrowser && TryClickPromptButton(BrowserDetector.PromptButtonSecondaryPrefix, out clickedLabel))
            {
                _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                BrowserDetector.InvalidateCache(); // Force re-detection on next Update
                return;
            }

            // Not finding cancel is OK - some browsers don't have it
            Log.Msg("BrowserNavigator", "No cancel button found");
        }

        /// <summary>
        /// Tries to click a specific button by exact name.
        /// </summary>
        private bool TryClickButtonByName(string buttonName, out string clickedLabel)
        {
            clickedLabel = null;

            // Check discovered buttons first
            foreach (var button in _browserButtons)
            {
                if (button == null) continue;
                if (button.name == buttonName)
                {
                    clickedLabel = UITextExtractor.GetButtonText(button, button.name);
                    var result = UIActivator.SimulatePointerClick(button);
                    if (result.Success)
                    {
                        Log.Msg("BrowserNavigator", $"Clicked {buttonName}: '{clickedLabel}'");
                        return true;
                    }
                }
            }

            // Search scene as fallback
            var go = BrowserDetector.FindActiveGameObject(buttonName);
            if (go != null)
            {
                clickedLabel = UITextExtractor.GetButtonText(go, go.name);
                var result = UIActivator.SimulatePointerClick(go);
                if (result.Success)
                {
                    Log.Msg("BrowserNavigator", $"Clicked {buttonName} (scene): '{clickedLabel}'");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tries to click a button matching the given patterns.
        /// </summary>
        private bool TryClickButtonByPatterns(string[] patterns, out string clickedLabel)
        {
            clickedLabel = null;

            foreach (var button in _browserButtons)
            {
                if (button == null) continue;
                if (BrowserDetector.MatchesButtonPattern(button.name, patterns))
                {
                    clickedLabel = UITextExtractor.GetButtonText(button, button.name);
                    var result = UIActivator.SimulatePointerClick(button);
                    if (result.Success)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Tries to click a PromptButton (Primary or Secondary).
        /// </summary>
        private bool TryClickPromptButton(string prefix, out string clickedLabel)
        {
            clickedLabel = null;

            var buttons = BrowserDetector.FindActiveGameObjects(go => go.name.StartsWith(prefix));
            foreach (var go in buttons)
            {
                var selectable = go.GetComponent<Selectable>();
                if (selectable != null && !selectable.interactable) continue;

                clickedLabel = UITextExtractor.GetButtonText(go, go.name);

                // Skip keyboard hints
                if (prefix == BrowserDetector.PromptButtonSecondaryPrefix &&
                    clickedLabel.Length <= 4 && !clickedLabel.Contains(" "))
                {
                    continue;
                }

                var result = UIActivator.SimulatePointerClick(go);
                if (result.Success)
                {
                    Log.Msg("BrowserNavigator", $"Clicked {prefix}: '{clickedLabel}'");
                    return true;
                }
            }
            return false;
        }
    }
}
