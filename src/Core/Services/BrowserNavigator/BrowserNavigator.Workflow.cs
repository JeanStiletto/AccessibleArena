using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;

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
                    MelonLogger.Msg($"[BrowserNavigator] WorkflowReflection: GameManager not found");
                    if (BrowserDetector.IsDebugEnabled(BrowserDetector.BrowserTypeWorkflow))
                        MenuDebugHelper.DumpWorkflowSystemDebug("WorkflowDebug");
                    return false;
                }

                // Get WorkflowController
                var wcProp = gameManager.GetType().GetProperty("WorkflowController", flags);
                var workflowController = wcProp?.GetValue(gameManager);

                if (workflowController == null)
                {
                    MelonLogger.Msg($"[BrowserNavigator] WorkflowReflection: WorkflowController not found");
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
                    MelonLogger.Msg($"[BrowserNavigator] WorkflowReflection: No active workflow found");
                    if (BrowserDetector.IsDebugEnabled(BrowserDetector.BrowserTypeWorkflow))
                        MenuDebugHelper.DumpWorkflowSystemDebug("WorkflowDebug");
                    return false;
                }

                var workflowType = currentInteraction.GetType();
                MelonLogger.Msg($"[BrowserNavigator] WorkflowReflection: Found workflow: {workflowType.Name}");

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
                                MelonLogger.Msg($"[BrowserNavigator] WorkflowReflection: Called SubmitSolution()");
                                return true;
                            }
                            else if (parameters.Length == 1 && solution != null)
                            {
                                submitMethod.Invoke(request, new[] { solution });
                                MelonLogger.Msg($"[BrowserNavigator] WorkflowReflection: Called SubmitSolution(solution)");
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
                        MelonLogger.Msg($"[BrowserNavigator] WorkflowReflection: Called {methodName}()");
                        return true;
                    }
                }

                // If nothing worked, log failure
                MelonLogger.Msg($"[BrowserNavigator] WorkflowReflection: Could not find submit method");
                if (BrowserDetector.IsDebugEnabled(BrowserDetector.BrowserTypeWorkflow))
                    MenuDebugHelper.DumpWorkflowSystemDebug("WorkflowDebug");
                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] WorkflowReflection error: {ex.Message}");
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
                                MelonLogger.Msg("[BrowserNavigator] WorkflowCancel: Cancelled ConfirmWidget");
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
                    MelonLogger.Msg("[BrowserNavigator] WorkflowCancel: GameManager not found");
                    return false;
                }

                var wcProp = gameManager.GetType().GetProperty("WorkflowController", flags);
                var workflowController = wcProp?.GetValue(gameManager);
                if (workflowController == null)
                {
                    MelonLogger.Msg("[BrowserNavigator] WorkflowCancel: WorkflowController not found");
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
                    MelonLogger.Msg("[BrowserNavigator] WorkflowCancel: No active workflow found");
                    return false;
                }

                var workflowType = currentInteraction.GetType();
                MelonLogger.Msg($"[BrowserNavigator] WorkflowCancel: Found workflow: {workflowType.Name}");

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
                                MelonLogger.Msg("[BrowserNavigator] WorkflowCancel: Invoked Cancelled on variant");
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
                                MelonLogger.Msg("[BrowserNavigator] WorkflowCancel: Called _request.Undo()");
                                return true;
                            }
                        }
                    }
                }

                MelonLogger.Msg("[BrowserNavigator] WorkflowCancel: No cancel mechanism found");
                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] WorkflowCancel error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clicks the confirm/primary button.
        /// </summary>
        private void ClickConfirmButton()
        {
            MelonLogger.Msg($"[BrowserNavigator] ClickConfirmButton called. Browser: {_browserInfo?.BrowserType}");

            // Signal re-entry on next frame in case the scaffold is reused for a new interaction
            _pendingRescan = true;

            string clickedLabel;

            // Workflow browser: try reflection approach to submit via WorkflowController
            if (_browserInfo?.IsWorkflow == true)
            {
                // First try the reflection approach (access WorkflowController directly)
                if (TrySubmitWorkflowViaReflection())
                {
                    MelonLogger.Msg($"[BrowserNavigator] Workflow submitted via reflection");
                    _announcer.Announce(Strings.Confirmed, AnnouncementPriority.Normal);
                    BrowserDetector.InvalidateCache();
                    return;
                }

                // Fallback: try clicking the button if reflection failed
                MelonLogger.Msg($"[BrowserNavigator] Reflection approach failed, trying button click");
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
                MelonLogger.Msg("[BrowserNavigator] SelectCards workflow submitted via reflection");
                _announcer.Announce(Strings.Confirmed, AnnouncementPriority.Normal);
                BrowserDetector.InvalidateCache();
                return;
            }

            // Try discovered buttons by name pattern (SubmitButton, ConfirmButton, etc.)
            if (TryClickButtonByPatterns(BrowserDetector.ConfirmPatterns, out clickedLabel))
            {
                _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                BrowserDetector.InvalidateCache(); // Force re-detection on next Update
                return;
            }

            // Scaffold browsers backed by a workflow (e.g. SelectCards with 2-button layout):
            // scaffold buttons don't match ConfirmPatterns, but the underlying workflow can submit.
            if (!(_browserInfo?.IsWorkflow == true) && TrySubmitWorkflowViaReflection())
            {
                MelonLogger.Msg($"[BrowserNavigator] Scaffold workflow submitted via reflection");
                _announcer.Announce(Strings.Confirmed, AnnouncementPriority.Normal);
                BrowserDetector.InvalidateCache();
                return;
            }

            // Fallback: PromptButton_Primary (scene search)
            // Skip for OptionalAction, choice-list, and SelectGroup browsers — their buttons are choices,
            // not confirm/cancel, and the global PromptButtons would click unrelated duel phase buttons
            if (!(_browserInfo?.IsOptionalAction == true) && !_isChoiceList && !_isSelectGroup && TryClickPromptButton(BrowserDetector.PromptButtonPrimaryPrefix, out clickedLabel))
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
            MelonLogger.Msg($"[BrowserNavigator] ClickCancelButton called. Browser: {_browserInfo?.BrowserType}");

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

            // SelectCards/SelectCardsMultiZone: SingleButton is the Decline option (see 5046b62)
            if (_isHighlightFilteredBrowser && TryClickButtonByName(BrowserDetector.ButtonSingle, out clickedLabel))
            {
                _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
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
            MelonLogger.Msg("[BrowserNavigator] No cancel button found");
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
                        MelonLogger.Msg($"[BrowserNavigator] Clicked {buttonName}: '{clickedLabel}'");
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
                    MelonLogger.Msg($"[BrowserNavigator] Clicked {buttonName} (scene): '{clickedLabel}'");
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
                    MelonLogger.Msg($"[BrowserNavigator] Clicked {prefix}: '{clickedLabel}'");
                    return true;
                }
            }
            return false;
        }
    }
}
