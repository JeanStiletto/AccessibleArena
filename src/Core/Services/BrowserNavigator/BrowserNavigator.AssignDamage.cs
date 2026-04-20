using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MelonLoader;
using AccessibleArena.Core.Models;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;

namespace AccessibleArena.Core.Services
{
    public partial class BrowserNavigator
    {
        // AssignDamage browser state
        private bool _isAssignDamage;
        private object _assignDamageBrowserRef;       // AssignDamageBrowser instance
        private System.Collections.IDictionary _spinnerMap; // InstanceId → SpinnerAnimated
        private uint _totalDamage;
        private bool _totalDamageCached;
        private int _assignerIndex;   // 1-based index of current assigner
        private int _assignerTotal;   // total number of damage assigners in this combat
        private const BindingFlags ReflFlags = AllInstanceFlags;

        /// <summary>
        /// Caches state for the AssignDamage browser: browser ref, spinner map, total damage.
        /// </summary>
        private void CacheAssignDamageState()
        {
            try
            {
                // Find GameManager → BrowserManager → CurrentBrowser
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
                    MelonLogger.Msg("[BrowserNavigator] AssignDamage: GameManager not found");
                    return;
                }

                var bmProp = gameManager.GetType().GetProperty("BrowserManager", ReflFlags);
                var browserManager = bmProp?.GetValue(gameManager);
                if (browserManager == null)
                {
                    MelonLogger.Msg("[BrowserNavigator] AssignDamage: BrowserManager not found");
                    return;
                }

                var cbProp = browserManager.GetType().GetProperty("CurrentBrowser", ReflFlags);
                var currentBrowser = cbProp?.GetValue(browserManager);
                if (currentBrowser == null || !currentBrowser.GetType().Name.Contains("AssignDamage"))
                {
                    MelonLogger.Msg($"[BrowserNavigator] AssignDamage: CurrentBrowser is {currentBrowser?.GetType().Name ?? "null"}");
                    return;
                }

                _assignDamageBrowserRef = currentBrowser;
                MelonLogger.Msg($"[BrowserNavigator] AssignDamage: Found browser {currentBrowser.GetType().Name}");

                // Deactivate CardInfoNavigator to prevent Up/Down interference
                // (it runs before BrowserNavigator in the update loop and would intercept spinner keys)
                AccessibleArenaMod.Instance?.CardNavigator?.Deactivate();

                // Cache _idToSpinnerMap
                var spinnerField = currentBrowser.GetType().GetField("_idToSpinnerMap", ReflFlags);
                if (spinnerField != null)
                {
                    _spinnerMap = spinnerField.GetValue(currentBrowser) as System.Collections.IDictionary;
                    MelonLogger.Msg($"[BrowserNavigator] AssignDamage: Spinner map has {_spinnerMap?.Count ?? 0} entries");
                }
                else
                {
                    MelonLogger.Msg("[BrowserNavigator] AssignDamage: _idToSpinnerMap field not found");
                }

                // TotalDamage is cached lazily via EnsureTotalDamageCached()
                // because CurrentInteraction may not be set yet at browser open time
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] AssignDamage cache error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles input specific to the AssignDamage browser.
        /// Up/Down adjusts spinner, Left/Right navigates blockers.
        /// </summary>
        private bool HandleAssignDamageInput()
        {
            // Up arrow: increase damage on current blocker
            // Always consume to prevent EventSystem focus leak
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                AdjustDamageSpinner(true);
                return true;
            }

            // Down arrow: decrease damage on current blocker
            // Always consume to prevent EventSystem focus leak
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                AdjustDamageSpinner(false);
                return true;
            }

            // Enter: consume without action (cards aren't toggleable in damage assignment)
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                return true;
            }

            // Space: submit via DoneAction on browser
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SubmitAssignDamage();
                return true;
            }

            // Backspace: undo via UndoAction on browser
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                UndoAssignDamage();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the SpinnerAnimated for the currently focused card by InstanceId.
        /// </summary>
        /// <summary>
        /// Lazily caches TotalDamage from the workflow's MtgDamageAssigner.
        /// Called on first use because CurrentInteraction is null at browser open time.
        /// </summary>
        private void EnsureTotalDamageCached()
        {
            if (_totalDamageCached) return;

            try
            {
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
                    MelonLogger.Msg("[BrowserNavigator] EnsureTotalDamageCached: GameManager not found");
                    return;
                }

                var wcProp = gameManager.GetType().GetProperty("WorkflowController", ReflFlags);
                var workflowController = wcProp?.GetValue(gameManager);
                if (workflowController == null)
                {
                    MelonLogger.Msg($"[BrowserNavigator] EnsureTotalDamageCached: WorkflowController null (prop found: {wcProp != null})");
                    return;
                }

                var cwProp = workflowController.GetType().GetProperty("CurrentWorkflow", ReflFlags);
                var interaction = cwProp?.GetValue(workflowController);
                if (interaction == null)
                {
                    MelonLogger.Msg($"[BrowserNavigator] EnsureTotalDamageCached: CurrentWorkflow null (prop found: {cwProp != null}), WC type: {workflowController.GetType().Name}");
                    return;
                }

                MelonLogger.Msg($"[BrowserNavigator] EnsureTotalDamageCached: Interaction type: {interaction.GetType().Name}");

                // Walk type hierarchy to find _damageAssigner (declared on AssignDamageWorkflow, not base)
                FieldInfo daField = null;
                var searchType = interaction.GetType();
                while (searchType != null && daField == null)
                {
                    daField = searchType.GetField("_damageAssigner", ReflFlags);
                    searchType = searchType.BaseType;
                }
                if (daField == null)
                {
                    MelonLogger.Msg("[BrowserNavigator] EnsureTotalDamageCached: _damageAssigner field not found");
                    return;
                }

                var damageAssigner = daField.GetValue(interaction);
                if (damageAssigner == null)
                {
                    MelonLogger.Msg("[BrowserNavigator] EnsureTotalDamageCached: _damageAssigner value is null");
                    return;
                }

                MelonLogger.Msg($"[BrowserNavigator] EnsureTotalDamageCached: damageAssigner type: {damageAssigner.GetType().Name}");

                // TotalDamage is a public readonly field on the MtgDamageAssigner struct
                var tdField = damageAssigner.GetType().GetField("TotalDamage", PublicInstance);
                if (tdField != null)
                {
                    _totalDamage = (uint)tdField.GetValue(damageAssigner);
                    _totalDamageCached = true;
                    MelonLogger.Msg($"[BrowserNavigator] AssignDamage: TotalDamage = {_totalDamage}");
                }
                else
                {
                    MelonLogger.Msg("[BrowserNavigator] EnsureTotalDamageCached: TotalDamage field not found");
                }

                // Read assigner queue counts: _handledAssigners (List) + _unhandledAssigners (Queue)
                // Current = handledCount + 1, Total = handledCount + unhandledCount + 1
                var iType = interaction.GetType();
                var handledField = iType.GetField("_handledAssigners", ReflFlags);
                var unhandledField = iType.GetField("_unhandledAssigners", ReflFlags);
                if (handledField != null && unhandledField != null)
                {
                    var handled = handledField.GetValue(interaction) as ICollection;
                    var unhandled = unhandledField.GetValue(interaction) as ICollection;
                    int handledCount = handled?.Count ?? 0;
                    int unhandledCount = unhandled?.Count ?? 0;
                    _assignerIndex = handledCount + 1;
                    _assignerTotal = handledCount + unhandledCount + 1;
                    MelonLogger.Msg($"[BrowserNavigator] AssignDamage: Assigner {_assignerIndex} of {_assignerTotal}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] EnsureTotalDamageCached error: {ex.Message}");
            }
        }

        /// <summary>
        /// Submits the damage assignment by invoking DoneAction on the browser.
        /// Note: The generic SimulatePointerClick path on the SubmitButton may also work
        /// (it did before our AssignDamage changes), but DoneAction is the direct event
        /// the game wires to OnButtonCallback("DoneButton"), so we invoke it explicitly.
        /// If this ever breaks, try reverting to SimulatePointerClick on SubmitButton.
        /// </summary>
        private void SubmitAssignDamage()
        {
            if (_assignDamageBrowserRef == null)
            {
                MelonLogger.Msg("[BrowserNavigator] AssignDamage: No browser ref for submit");
                return;
            }

            try
            {
                var browserType = _assignDamageBrowserRef.GetType();
                var doneField = browserType.GetField("DoneAction", ReflFlags);
                if (doneField != null)
                {
                    var doneAction = doneField.GetValue(_assignDamageBrowserRef) as Action;
                    if (doneAction != null)
                    {
                        doneAction.Invoke();
                        _announcer.Announce(Strings.Confirmed, AnnouncementPriority.Normal);
                        MelonLogger.Msg("[BrowserNavigator] AssignDamage: Invoked DoneAction");
                        return;
                    }
                }

                // Fallback: try invoking OnButtonCallback("DoneButton") directly
                var callbackMethod = browserType.GetMethod("OnButtonCallback", ReflFlags);
                if (callbackMethod != null)
                {
                    callbackMethod.Invoke(_assignDamageBrowserRef, new object[] { "DoneButton" });
                    _announcer.Announce(Strings.Confirmed, AnnouncementPriority.Normal);
                    MelonLogger.Msg("[BrowserNavigator] AssignDamage: Called OnButtonCallback(DoneButton)");
                    return;
                }

                MelonLogger.Msg("[BrowserNavigator] AssignDamage: Could not find DoneAction or OnButtonCallback");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] SubmitAssignDamage error: {ex.Message}");
            }
        }

        /// <summary>
        /// Undoes the last damage assignment action via UndoAction on the browser.
        /// </summary>
        private void UndoAssignDamage()
        {
            if (_assignDamageBrowserRef == null)
            {
                MelonLogger.Msg("[BrowserNavigator] AssignDamage: No browser ref for undo");
                return;
            }

            try
            {
                var browserType = _assignDamageBrowserRef.GetType();
                var undoField = browserType.GetField("UndoAction", ReflFlags);
                if (undoField != null)
                {
                    var undoAction = undoField.GetValue(_assignDamageBrowserRef) as Action;
                    if (undoAction != null)
                    {
                        undoAction.Invoke();
                        _announcer.Announce(Strings.Cancelled, AnnouncementPriority.Normal);
                        MelonLogger.Msg("[BrowserNavigator] AssignDamage: Invoked UndoAction");
                        return;
                    }
                }

                MelonLogger.Msg("[BrowserNavigator] AssignDamage: UndoAction not available");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] UndoAssignDamage error: {ex.Message}");
            }
        }

        private object GetSpinnerForCurrentCard()
        {
            if (_spinnerMap == null || _currentCardIndex < 0 || _currentCardIndex >= _browserCards.Count)
                return null;

            var card = _browserCards[_currentCardIndex];
            uint instanceId = GetCardInstanceId(card);
            if (instanceId == 0) return null;

            if (_spinnerMap.Contains(instanceId))
                return _spinnerMap[instanceId];

            return null;
        }

        /// <summary>
        /// Extracts InstanceId from a card's CDC component.
        /// </summary>
        private uint GetCardInstanceId(GameObject card)
        {
            if (card == null) return 0;

            try
            {
                foreach (var mb in card.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    var type = mb.GetType();
                    // DuelScene_CDC or similar CDC types have InstanceId
                    if (type.Name.Contains("CDC"))
                    {
                        var idProp = type.GetProperty("InstanceId", ReflFlags);
                        if (idProp != null)
                            return (uint)idProp.GetValue(mb);

                        // Try via Model.Instance.InstanceId
                        var modelProp = type.GetProperty("Model", ReflFlags);
                        if (modelProp != null)
                        {
                            var model = modelProp.GetValue(mb);
                            if (model != null)
                            {
                                var instProp = model.GetType().GetProperty("Instance", ReflFlags);
                                var instance = instProp?.GetValue(model);
                                if (instance != null)
                                {
                                    var iidProp = instance.GetType().GetProperty("InstanceId", ReflFlags);
                                    if (iidProp != null)
                                        return (uint)iidProp.GetValue(instance);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] GetCardInstanceId error: {ex.Message}");
            }

            return 0;
        }

        /// <summary>
        /// Clicks the spinner up/down button and announces the new value.
        /// </summary>
        private void AdjustDamageSpinner(bool increase)
        {
            EnsureTotalDamageCached();

            var spinner = GetSpinnerForCurrentCard();
            if (spinner == null)
            {
                // No spinner = attacker card, not a blocker
                return;
            }

            try
            {
                var spinnerType = spinner.GetType();

                // Click _upButton or _downButton
                string buttonFieldName = increase ? "_upButton" : "_downButton";
                var buttonField = spinnerType.GetField(buttonFieldName, ReflFlags);
                if (buttonField == null)
                {
                    MelonLogger.Msg($"[BrowserNavigator] AssignDamage: {buttonFieldName} field not found on {spinnerType.Name}");
                    return;
                }

                var button = buttonField.GetValue(spinner) as Button;
                if (button == null)
                {
                    MelonLogger.Msg($"[BrowserNavigator] AssignDamage: {buttonFieldName} is null");
                    return;
                }

                button.onClick.Invoke();

                // Read new value from spinner
                var valueProp = spinnerType.GetProperty("Value", ReflFlags);
                int newValue = 0;
                if (valueProp != null)
                {
                    newValue = (int)valueProp.GetValue(spinner);
                }

                // Announce: "X of Total assigned"
                string announcement = Strings.DamageAssigned(newValue, (int)_totalDamage);

                // Check lethal
                if (IsSpinnerLethal(spinner))
                {
                    announcement = $"{Strings.DamageAssignLethal}, {announcement}";
                }

                _announcer.Announce(announcement, AnnouncementPriority.High);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] AdjustDamageSpinner error: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if the spinner's value text is gold (lethal damage reached).
        /// Lethal color: Color32(254, 176, 0, 255).
        /// </summary>
        private bool IsSpinnerLethal(object spinner)
        {
            try
            {
                var spinnerType = spinner.GetType();
                var textField = spinnerType.GetField("_valueText", ReflFlags);
                if (textField == null) return false;

                var textComponent = textField.GetValue(spinner);
                if (textComponent == null) return false;

                var colorProp = textComponent.GetType().GetProperty("color", ReflFlags);
                if (colorProp == null) return false;

                var color = (Color)colorProp.GetValue(textComponent);
                // Compare to lethal gold: approximately (254/255, 176/255, 0/255, 1)
                return color.r > 0.9f && color.g > 0.6f && color.g < 0.8f && color.b < 0.1f;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Announces a card in AssignDamage mode with name, P/T, lethal status, position.
        /// Does NOT call PrepareForCard so CardInfoNavigator stays inactive
        /// and Up/Down are free for spinner control.
        /// </summary>
        private void AnnounceAssignDamageCard(GameObject card)
        {
            var info = CardDetector.ExtractCardInfo(card);
            string cardName = info.Name ?? "Unknown card";

            var parts = new List<string>();
            parts.Add(cardName);

            // Add P/T
            if (!string.IsNullOrEmpty(info.PowerToughness))
            {
                parts.Add(info.PowerToughness);
            }

            // Check lethal state via spinner
            var spinner = GetSpinnerForCurrentCard();
            if (spinner != null && IsSpinnerLethal(spinner))
            {
                parts.Add(Strings.DamageAssignLethal);
            }

            // Add position
            string pos = Strings.PositionOf(_currentCardIndex + 1, _browserCards.Count, force: true);
            if (pos != "") parts.Add(pos);

            _announcer.Announce(string.Join(", ", parts), AnnouncementPriority.High);
        }

        /// <summary>
        /// Gets the entry announcement for the AssignDamage browser.
        /// "Assign damage. [AttackerName], [Power] damage. [N] blockers"
        /// </summary>
        private string GetAssignDamageEntryAnnouncement(int cardCount, string fallbackName)
        {
            EnsureTotalDamageCached();

            if (_assignDamageBrowserRef == null)
                return Strings.DamageAssignEntry(fallbackName, (int)_totalDamage, cardCount);

            try
            {
                var browserType = _assignDamageBrowserRef.GetType();

                // Get _layout from the browser
                var layoutField = browserType.GetField("_layout", ReflFlags);
                var layout = layoutField?.GetValue(_assignDamageBrowserRef);
                if (layout == null)
                {
                    MelonLogger.Msg("[BrowserNavigator] AssignDamage: _layout not found");
                    return Strings.DamageAssignEntry(fallbackName, (int)_totalDamage, cardCount);
                }

                var layoutType = layout.GetType();

                // Get _attacker (DuelScene_CDC)
                var attackerField = layoutType.GetField("_attacker", ReflFlags);
                object attacker = attackerField?.GetValue(layout);
                string attackerName = "Attacker";
                int power = (int)_totalDamage;

                if (attacker != null)
                {
                    // Get attacker's GameObject and extract name
                    var attackerMb = attacker as MonoBehaviour;
                    if (attackerMb != null)
                    {
                        var attackerInfo = CardDetector.ExtractCardInfo(attackerMb.gameObject);
                        if (!string.IsNullOrEmpty(attackerInfo.Name))
                            attackerName = attackerInfo.Name;
                    }
                }

                // Get _blockers list for count
                var blockersField = layoutType.GetField("_blockers", ReflFlags);
                if (blockersField != null)
                {
                    var blockersList = blockersField.GetValue(layout) as IList;
                    if (blockersList != null)
                    {
                        cardCount = blockersList.Count;
                    }
                }

                string entry = Strings.DamageAssignEntry(attackerName, power, cardCount);
                string pos = Strings.PositionOf(_assignerIndex, _assignerTotal, force: true);
                if (pos != "") entry += $". {pos}";
                return entry;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] AssignDamage entry announcement error: {ex.Message}");
                return Strings.DamageAssignEntry(fallbackName, (int)_totalDamage, cardCount);
            }
        }
    }
}
