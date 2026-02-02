using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using MelonLoader;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
// NOTE: System.Runtime.InteropServices kept for potential WinAPI reactivation
// using System.Runtime.InteropServices;

namespace AccessibleArena.Core.Services
{
    /*
    // =============================================================================
    // WINDOWS API APPROACH - KEPT FOR FUTURE USE (January 2026)
    // =============================================================================
    // DO NOT DELETE OR REFACTOR THIS CODE - it is a proven working fallback!
    //
    // History:
    // - Unity events approach stopped working at some point (unknown cause)
    // - WinAPI approach was implemented and worked reliably
    // - After PC restart, Unity events approach works again
    // - Root cause was likely external factors (mouse position, overlays, etc.)
    //
    // Keep this code available because:
    // - Overlapping overlays may interfere with Unity raycasts in the future
    // - Mouse positioning issues may recur
    // - WinAPI bypasses Unity event system entirely and controls real cursor
    //
    // If Unity events approach fails again, reactivate this WinAPI code.
    // =============================================================================

    /// <summary>
    /// Windows API imports for real mouse control.
    /// Required because the game checks Input.mousePosition (actual cursor)
    /// rather than event position data.
    /// </summary>
    internal static class WinAPI
    {
        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }
    }
    */

    /// <summary>
    /// Centralized UI activation utilities.
    /// Handles clicking buttons, toggling checkboxes, focusing input fields,
    /// and playing cards from hand.
    /// </summary>
    public static class UIActivator
    {
        #region Constants

        // Timing delays for card play sequence
        private const float CardSelectDelay = 0.1f;
        private const float CardPickupDelay = 0.5f;
        private const float CardDropDelay = 0.6f;

        // Hierarchy search depth limits
        private const int MaxDeckSearchDepth = 5;
        private const int MaxDeckViewSearchDepth = 6;

        // Type names for reflection
        private const string CustomButtonTypeName = "CustomButton";
        private const string DeckViewTypeName = "DeckView";
        private const string TooltipTriggerTypeName = "TooltipTrigger";

        // Compiled regex for Submit button detection
        private static readonly Regex SubmitButtonPattern = new Regex(@"^Submit\s*\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Targeting mode detection cache (avoids expensive FindObjectsOfType every call)
        private const float TargetingCacheTimeout = 0.1f;
        private static float _lastTargetingScanTime;
        private static bool _cachedTargetingResult;

        /// <summary>
        /// Checks if a MonoBehaviour is a CustomButton by type name.
        /// </summary>
        private static bool IsCustomButton(MonoBehaviour mb)
        {
            return mb != null && mb.GetType().Name == CustomButtonTypeName;
        }

        #endregion

        #region General UI Activation

        /// <summary>
        /// Activates a UI element based on its type (button, toggle, input field, etc.).
        /// </summary>
        public static ActivationResult Activate(GameObject element)
        {
            if (element == null)
                return new ActivationResult(false, "Element is null");

            // Special handling for collection cards (PagesMetaCardView/MetaCardView in deck builder)
            // These are card display views - the actual click handler is on a child element
            if (IsCollectionCard(element))
            {
                Log($"Detected collection card: {element.name}");
                var result = TryActivateCollectionCard(element);
                if (result.Success)
                    return result;
                Log($"Collection card activation failed, trying fallback");
            }

            // Special handling for deck entries - they need direct selection via DeckViewSelector
            // because MTGA's CustomButton onClick on decks doesn't reliably trigger selection
            if (IsDeckEntry(element))
            {
                Log($"Detected deck entry, trying specialized deck selection");
                if (TrySelectDeck(element))
                {
                    return new ActivationResult(true, "Deck Selected", ActivationType.Button);
                }
                // Fall through to standard activation if specialized selection fails
                Log($"Specialized deck selection failed, falling back to standard activation");
            }

            // Try TMP_InputField
            var tmpInput = element.GetComponent<TMP_InputField>();
            if (tmpInput != null)
            {
                tmpInput.ActivateInputField();
                tmpInput.Select();
                return new ActivationResult(true, "Editing", ActivationType.InputField);
            }

            // Try legacy InputField
            var inputField = element.GetComponent<InputField>();
            if (inputField != null)
            {
                inputField.ActivateInputField();
                inputField.Select();
                return new ActivationResult(true, "Editing", ActivationType.InputField);
            }

            // Try Toggle
            var toggle = element.GetComponent<Toggle>();
            if (toggle != null)
            {
                // Check if this toggle also has CustomButton (like deck folder toggles)
                bool toggleHasCustomButton = HasCustomButtonComponent(element);
                if (toggleHasCustomButton)
                {
                    // For Toggle+CustomButton combo:
                    // 1. Toggle.OnPointerClick - changes state
                    // 2. CustomButton onClick - triggers game filtering logic
                    bool oldState = toggle.isOn;
                    Log($"Toggle with CustomButton: current state {oldState}");

                    // Step 1: Toggle the checkbox state
                    var pointer = CreatePointerEventData(element);
                    ((IPointerClickHandler)toggle).OnPointerClick(pointer);

                    bool newState = toggle.isOn;
                    string state = newState ? "Checked" : "Unchecked";
                    Log($"Toggle state after click: {oldState} -> {newState} ({state})");

                    // Step 2: Trigger CustomButton to run game filtering logic
                    TryInvokeCustomButtonOnClick(element);

                    return new ActivationResult(true, state, ActivationType.Toggle);
                }
                else
                {
                    // Simple toggle without CustomButton - just flip the state.
                    // We handle toggling ourselves; Enter/Space should be consumed by caller
                    // to prevent Unity from also processing and double-toggling.
                    toggle.isOn = !toggle.isOn;
                    string state = toggle.isOn ? "Checked" : "Unchecked";
                    Log($"Toggled {element.name} to {state}");
                    return new ActivationResult(true, state, ActivationType.Toggle);
                }
            }

            // Check for CustomButton BEFORE standard Button
            // MTGA uses CustomButton for most interactions - the game logic responds to pointer events,
            // not to Button.onClick. If element has CustomButton, use pointer simulation.
            // This is critical for buttons like Link_LogOut that have both Button and CustomButton.
            bool hasCustomButton = HasCustomButtonComponent(element);
            if (hasCustomButton)
            {
                // Special handling for NPE reward claim button - onClick listener is broken
                // Must invoke OnClaimClicked_Unity on NPEContentControllerRewards instead
                if (TryInvokeNPERewardClaim(element, out var npeResult))
                    return npeResult;

                // Special handling for UpdatePoliciesPanel - the button's onClick has broken listener reference
                // so we invoke OnAccept directly on the panel controller
                if (TryInvokeUpdatePoliciesAccept(element, out var acceptResult))
                    return acceptResult;

                // Special handling for SystemMessageButtonView (popup dialog buttons)
                // Click() triggers callback, ClearMessageQueue() closes popup
                var systemMsgButton = FindComponentByName(element, "SystemMessageButtonView");
                if (systemMsgButton != null)
                {
                    Log($"SystemMessageButtonView detected on: {element.name}");
                    TryInvokeMethod(systemMsgButton, "Click");
                    TryDismissViaSystemMessageManager();
                    return new ActivationResult(true, "Activated", ActivationType.Button);
                }

                var pointerResult2 = SimulatePointerClick(element);

                // Also try onClick reflection as additional handler
                // BUT skip for deck list cards - they respond to pointer events already,
                // and calling both causes double activation (removes 2 cards instead of 1)
                if (!IsDeckListCard(element))
                {
                    TryInvokeCustomButtonOnClick(element);
                }

                // Special handling for deck builder MainButton - its onClick listener has NULL target
                // We need to find the WrapperDeckBuilder component and invoke the method directly
                if (element.name == "MainButton")
                {
                    var deckBuilderController = FindDeckBuilderController();
                    if (deckBuilderController != null)
                    {
                        Log($"Found deck builder controller: {deckBuilderController.GetType().Name}");
                        if (TryInvokeMethod(deckBuilderController, "OnDeckbuilderDoneButtonClicked"))
                        {
                            Log($"WrapperDeckBuilder.OnDeckbuilderDoneButtonClicked() invoked successfully");
                        }
                        else
                        {
                            Log($"Could not invoke OnDeckbuilderDoneButtonClicked");
                        }
                    }
                    else
                    {
                        Log($"Could not find deck builder controller");
                    }
                }

                return pointerResult2;
            }

            // Try standard Unity Button (only if no CustomButton)
            var button = element.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.Invoke();
                return new ActivationResult(true, "Activated", ActivationType.Button);
            }

            // Try child Button
            var childButton = element.GetComponentInChildren<Button>();
            if (childButton != null)
            {
                Log($"Using child Button: {childButton.gameObject.name}");
                childButton.onClick.Invoke();
                return new ActivationResult(true, "Activated", ActivationType.Button);
            }

            // Try to find the actual clickable element in hierarchy
            // Some containers (like HomeBanner) have the CustomButton on a child, not the root
            var clickableTarget = FindClickableInHierarchy(element);
            if (clickableTarget != null && clickableTarget != element)
            {
                Log($"Found clickable child: {clickableTarget.name}, sending pointer events there");
                return SimulatePointerClick(clickableTarget);
            }

            // Special handling for SystemMessageButtonView (confirmation dialog buttons)
            // These buttons need their Click method invoked directly
            var systemMessageButton = FindComponentByName(element, "SystemMessageButtonView");
            if (systemMessageButton != null)
            {
                Log($"SystemMessageButtonView detected, trying Click method");
                if (TryInvokeMethod(systemMessageButton, "Click"))
                {
                    return new ActivationResult(true, "Activated", ActivationType.Button);
                }
                if (TryInvokeMethod(systemMessageButton, "OnClick"))
                {
                    return new ActivationResult(true, "Activated", ActivationType.Button);
                }
                if (TryInvokeMethod(systemMessageButton, "OnButtonClicked"))
                {
                    return new ActivationResult(true, "Activated", ActivationType.Button);
                }
            }

            // For non-CustomButton elements (CustomButton was handled earlier), try onClick reflection then pointer fallback
            var customButtonResult = TryInvokeCustomButtonOnClick(element);
            if (customButtonResult.Success)
            {
                return customButtonResult;
            }

            // Final fallback: Pointer event simulation
            return SimulatePointerClick(element);
        }

        #endregion

        #region Pointer Simulation

        /// <summary>
        /// Simulates a full pointer click sequence (enter, down, up, click) on an element.
        /// </summary>
        public static ActivationResult SimulatePointerClick(GameObject element)
        {
            if (element == null)
                return new ActivationResult(false, "Element is null");

            var pointer = CreatePointerEventData(element);

            Log($"Simulating pointer events on: {element.name}");

            // Set as selected object in EventSystem - some UI elements require this
            var eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(element);
                Log($"Set EventSystem selected object to: {element.name}");
            }

            // Full click sequence
            ExecuteEvents.Execute(element, pointer, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(element, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(element, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(element, pointer, ExecuteEvents.pointerClickHandler);

            // Also try Submit event - this is what Unity uses for Enter key activation
            if (eventSystem != null)
            {
                var baseEventData = new BaseEventData(eventSystem);
                ExecuteEvents.Execute(element, baseEventData, ExecuteEvents.submitHandler);
            }

            // Also try on immediate children
            foreach (Transform child in element.transform)
            {
                ExecuteEvents.Execute(child.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            }

            // Try IPointerClickHandler components directly
            // NOTE: ExecuteEvents.Execute(pointerClickHandler) doesn't reliably reach Toggle
            // So we need to invoke Toggle directly here
            var clickHandlers = element.GetComponents<IPointerClickHandler>();
            foreach (var handler in clickHandlers)
            {
                Log($"Invoking IPointerClickHandler: {handler.GetType().Name}");
                handler.OnPointerClick(pointer);
            }

            return new ActivationResult(true, "Activated", ActivationType.PointerClick);
        }

        /// <summary>
        /// Simulates a click at a specific screen position using raycast.
        /// If no target is found via raycast, still sends the pointer events
        /// as the game may process position-based clicks (e.g., dropping held cards).
        /// </summary>
        public static ActivationResult SimulateClickAtPosition(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                Log("No EventSystem found");
                return new ActivationResult(false, "No EventSystem");
            }

            var pointer = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                position = screenPosition,
                pressPosition = screenPosition
            };

            // Raycast to find what's at this position
            var raycastResults = new System.Collections.Generic.List<RaycastResult>();
            eventSystem.RaycastAll(pointer, raycastResults);

            if (raycastResults.Count > 0)
            {
                var target = raycastResults[0].gameObject;
                pointer.pointerPress = target;
                pointer.pointerEnter = target;

                Log($"Clicking at position {screenPosition}: {target.name}");

                ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerEnterHandler);
                ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);

                return new ActivationResult(true, $"Clicked {target.name}", ActivationType.PointerClick);
            }

            // No UI raycast hit at this position
            // Don't send "global clicks" to EventSystem - this was causing MTGA to interpret
            // them as "pass priority" during combat phases, leading to unwanted phase auto-skip.
            // The game processes card drops based on Input.mousePosition (real cursor),
            // which DuelNavigator centers when the duel starts. No additional click needed.
            Log($"No raycast hit at {screenPosition}, skipping global click");

            return new ActivationResult(true, "No UI target at position", ActivationType.Unknown);
        }

        /// <summary>
        /// Simulates a click at screen center. Used for "click anywhere to continue" interactions.
        /// </summary>
        public static ActivationResult SimulateScreenCenterClick()
        {
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            var result = SimulateClickAtPosition(screenCenter);

            // Fallback: try clicking CustomButtons if raycast failed
            if (!result.Success)
            {
                Log("No raycast hits, trying CustomButton fallback...");
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
                {
                    if (!IsCustomButton(mb) || !mb.gameObject.activeInHierarchy) continue;

                    var type = mb.GetType();
                    var onClickField = type.GetField("onClick",
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);

                    if (onClickField != null)
                    {
                        var onClick = onClickField.GetValue(mb);
                        if (onClick != null)
                        {
                            var invokeMethod = onClick.GetType().GetMethod("Invoke", System.Type.EmptyTypes);
                            if (invokeMethod != null)
                            {
                                Log($"Invoking onClick on CustomButton: {mb.gameObject.name}");
                                invokeMethod.Invoke(onClick, null);
                                return new ActivationResult(true, $"Invoked {mb.gameObject.name}", ActivationType.Button);
                            }
                        }
                    }
                }
            }

            return result;
        }

        #endregion

        #region Card Playing

        /// <summary>
        /// Plays a card from hand using the double-click approach:
        /// 1. Click card (selects it)
        /// 2. Wait briefly
        /// 3. Click card again (picks it up)
        /// 4. Wait briefly
        /// 5. Click screen center (confirms play for non-targeted cards)
        /// 6. Enter targeting mode (for targeted cards, Tab cycles through targets)
        /// </summary>
        public static void PlayCardViaTwoClick(GameObject card, System.Action<bool, string> callback = null)
        {
            if (card == null)
            {
                callback?.Invoke(false, "Card is null");
                return;
            }

            Log($"=== PLAYING CARD: {card.name} ===");
            MelonCoroutines.Start(PlayCardCoroutine(card, callback));
        }

        private static IEnumerator PlayCardCoroutine(GameObject card, System.Action<bool, string> callback)
        {
            // Step 0: Check if card is a land (lands use simplified double-click, no screen center needed)
            var cardInfo = CardDetector.ExtractCardInfo(card);
            bool isLand = cardInfo.TypeLine?.ToLower().Contains("land") ?? false;

            // Step 1: First click (select)
            Log("Step 1: First click (select)");
            var click1 = SimulatePointerClick(card);
            if (!click1.Success)
            {
                Log("Failed at step 1");
                callback?.Invoke(false, "First click failed");
                yield break;
            }

            // Brief wait for selection to register
            yield return new WaitForSeconds(CardSelectDelay);

            // Step 1.5: Check if we're in discard/selection mode BEFORE second click
            // If "Submit X" button is showing, first click selected the card for discard
            // Second click would break the discard state, so abort here
            if (IsTargetingModeActive())
            {
                Log("Submit button detected - in discard/selection mode, not playing card");
                Log("First click selected card for discard. Use Submit button to confirm.");
                callback?.Invoke(false, "Discard mode - card selected for discard");
                yield break;
            }

            // Step 2: Second click (pick up for spells, or play for lands)
            Log("Step 2: Second click (pick up/play)");
            var click2 = SimulatePointerClick(card);
            if (!click2.Success)
            {
                Log("Failed at step 2");
                callback?.Invoke(false, "Second click failed");
                yield break;
            }

            // For lands: Double-click is enough to play them, no need to drag to center
            // Exit immediately to avoid unnecessary clicks
            if (isLand)
            {
                Log("Land played via double-click");
                Log("=== CARD PLAY COMPLETE ===");
                callback?.Invoke(true, "Land played");
                yield break;
            }

            // For spells: Wait for card to be held, then drop at screen center
            yield return new WaitForSeconds(CardPickupDelay);

            // Step 3: Click screen center via Unity events (confirm play)
            // Unity events approach works after PC restart (Jan 2026).
            // If this stops working, check for overlapping overlays or mouse positioning
            // issues, and consider reactivating the WinAPI fallback below.
            Log("Step 3: Click screen center via Unity events (confirm play)");

            Vector2 center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            SimulateClickAtPosition(center);

            /*
            // WINAPI APPROACH - DO NOT DELETE, proven fallback if Unity events fail
            // See WinAPI class comment at top of file for history.
            // Move cursor to screen center
            int centerX = Screen.width / 2;
            int centerY = Screen.height / 2;
            WinAPI.SetCursorPos(centerX, centerY);

            // Small delay for cursor to register
            yield return new WaitForSeconds(0.05f);

            // Perform real mouse click
            WinAPI.mouse_event(WinAPI.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            yield return new WaitForSeconds(0.05f);
            WinAPI.mouse_event(WinAPI.MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
            */

            // Step 4: Wait for game to process the play and UI to update
            // Need enough time for targeting UI (Cancel/Submit buttons) to appear
            yield return new WaitForSeconds(CardDropDelay);

            // Note: Targeting mode is now handled by DuelNavigator's auto-detection
            // which checks for spell on stack + HotHighlight targets.
            // This avoids false positives from activated abilities that don't target.

            Log("=== CARD PLAY COMPLETE ===");
            callback?.Invoke(true, "Card played");
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Special handling for NPE reward claim button (NullClaimButton).
        /// The button's onClick listener is broken - we need to invoke OnClaimClicked_Unity
        /// on the NPEContentControllerRewards component directly.
        /// </summary>
        private static bool TryInvokeNPERewardClaim(GameObject element, out ActivationResult result)
        {
            result = default;

            // Only handle NullClaimButton
            if (element.name != "NullClaimButton")
                return false;

            // Verify we're inside NPE-Rewards_Container
            bool insideNPE = false;
            var current = element.transform.parent;
            while (current != null)
            {
                if (current.name == "NPE-Rewards_Container")
                {
                    insideNPE = true;
                    break;
                }
                current = current.parent;
            }

            if (!insideNPE)
                return false;

            Log($"NullClaimButton detected in NPE rewards, looking for NPEContentControllerRewards");

            // Find NPEContentControllerRewards component
            foreach (var behaviour in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (behaviour == null) continue;
                if (behaviour.GetType().Name != "NPEContentControllerRewards") continue;

                Log($"Found NPEContentControllerRewards on: {behaviour.gameObject.name}");

                // Invoke OnClaimClicked_Unity
                var type = behaviour.GetType();
                var method = type.GetMethod("OnClaimClicked_Unity",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (method != null)
                {
                    try
                    {
                        Log($"Invoking NPEContentControllerRewards.OnClaimClicked_Unity()");
                        method.Invoke(behaviour, null);
                        result = new ActivationResult(true, "Reward claimed", ActivationType.Button);
                        return true;
                    }
                    catch (System.Exception ex)
                    {
                        Log($"OnClaimClicked_Unity error: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
                else
                {
                    Log($"OnClaimClicked_Unity method not found on NPEContentControllerRewards");
                }
            }

            Log($"NPEContentControllerRewards not found, falling back to standard activation");
            return false;
        }

        /// <summary>
        /// Special handling for UpdatePoliciesPanel - the button's onClick listener has a broken/null target reference.
        /// We find the panel controller and invoke OnAccept directly.
        /// </summary>
        private static bool TryInvokeUpdatePoliciesAccept(GameObject element, out ActivationResult result)
        {
            result = default;

            // Only handle buttons named MainButton_Register on UpdatePolicies panel
            if (!element.name.Contains("MainButton_Register"))
                return false;

            // Search up from the button to find the panel
            var panel = element.transform;
            while (panel != null && !panel.name.Contains("UpdatePolicies"))
                panel = panel.parent;

            if (panel == null)
                return false;

            // Find UpdatePoliciesPanel component and invoke OnAccept
            foreach (var comp in panel.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (comp == null) continue;
                if (comp.GetType().Name != "UpdatePoliciesPanel") continue;

                var onAcceptMethod = comp.GetType().GetMethod("OnAccept",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (onAcceptMethod != null)
                {
                    try
                    {
                        Log($"Invoking UpdatePoliciesPanel.OnAccept directly");
                        onAcceptMethod.Invoke(comp, null);
                        result = new ActivationResult(true, "Accepted", ActivationType.PointerClick);
                        return true;
                    }
                    catch (System.Exception ex)
                    {
                        Log($"UpdatePoliciesPanel.OnAccept threw: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Tries to invoke CustomButton onClick via reflection.
        /// Some CustomButtons don't respond reliably to pointer events on first click.
        /// </summary>
        private static ActivationResult TryInvokeCustomButtonOnClick(GameObject element)
        {
            foreach (var mb in element.GetComponents<MonoBehaviour>())
            {
                if (!IsCustomButton(mb))
                    continue;

                var type = mb.GetType();

                // Try to find _onClick field (CustomButton uses underscore prefix)
                var onClickField = type.GetField("_onClick",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (onClickField != null)
                {
                    var onClick = onClickField.GetValue(mb);
                    if (onClick != null)
                    {
                        var invokeMethod = onClick.GetType().GetMethod("Invoke", System.Type.EmptyTypes);
                        if (invokeMethod != null)
                        {
                            Log($"Invoking CustomButton.onClick via reflection on: {element.name}");
                            try
                            {
                                invokeMethod.Invoke(onClick, null);
                                return new ActivationResult(true, "Activated", ActivationType.Button);
                            }
                            catch (System.Exception ex)
                            {
                                // Game's event handler may throw (e.g., HomePageBillboard not initialized)
                                // Fall back to pointer simulation
                                Log($"CustomButton.onClick threw exception, falling back to pointer: {ex.InnerException?.Message ?? ex.Message}");
                                return SimulatePointerClick(element);
                            }
                        }
                        else
                        {
                            Log($"CustomButton.onClick has no Invoke() method on: {element.name}");
                        }
                    }
                    else
                    {
                        Log($"CustomButton.onClick is null on: {element.name}");
                    }
                }
                else
                {
                    Log($"CustomButton has no _onClick field on: {element.name}");
                }
            }

            return new ActivationResult(false, "No CustomButton onClick found");
        }

        /// <summary>
        /// Searches the element and its descendants for the actual clickable target.
        /// Returns the first GameObject that has a CustomButton or IPointerClickHandler.
        /// This handles cases where the navigable element is a container (like HomeBanner)
        /// but the actual click handler is on a child.
        /// </summary>
        private static GameObject FindClickableInHierarchy(GameObject root)
        {
            if (root == null) return null;

            // Check root first
            if (HasClickHandler(root))
                return root;

            // Search children recursively (breadth-first to find closest match)
            var queue = new System.Collections.Generic.Queue<Transform>();
            foreach (Transform child in root.transform)
            {
                queue.Enqueue(child);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == null || !current.gameObject.activeInHierarchy)
                    continue;

                if (HasClickHandler(current.gameObject))
                {
                    return current.gameObject;
                }

                // Add children to queue
                foreach (Transform child in current)
                {
                    queue.Enqueue(child);
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a GameObject has a click handler (CustomButton or IPointerClickHandler).
        /// </summary>
        private static bool HasClickHandler(GameObject obj)
        {
            // Check for CustomButton component
            foreach (var mb in obj.GetComponents<MonoBehaviour>())
            {
                if (IsCustomButton(mb))
                    return true;
            }

            // Check for IPointerClickHandler (excluding TooltipTrigger which just shows tooltips)
            var clickHandlers = obj.GetComponents<IPointerClickHandler>();
            foreach (var handler in clickHandlers)
            {
                // TooltipTrigger implements IPointerClickHandler but doesn't activate buttons
                if (handler.GetType().Name != TooltipTriggerTypeName)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a GameObject has a CustomButton component.
        /// Used to determine activation strategy - CustomButtons need pointer events first.
        /// </summary>
        private static bool HasCustomButtonComponent(GameObject obj)
        {
            foreach (var mb in obj.GetComponents<MonoBehaviour>())
            {
                if (IsCustomButton(mb))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Finds a MonoBehaviour component by type name.
        /// </summary>
        private static MonoBehaviour FindComponentByName(GameObject obj, string typeName)
        {
            foreach (var mb in obj.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == typeName)
                    return mb;
            }
            return null;
        }

        /// <summary>
        /// Finds the popup root GameObject by traversing up the hierarchy.
        /// Looks for SystemMessageView or similar popup container names.
        /// </summary>
        private static GameObject FindPopupRoot(GameObject element)
        {
            if (element == null) return null;

            var current = element.transform.parent;
            while (current != null)
            {
                string name = current.gameObject.name;
                if (name.Contains("SystemMessageView") ||
                    name.Contains("Popup") ||
                    name.Contains("Dialog") ||
                    name.Contains("Modal"))
                {
                    return current.gameObject;
                }
                current = current.parent;
            }
            return null;
        }

        /// <summary>
        /// Finds the SystemMessageView component in the element's hierarchy.
        /// Searches both up the hierarchy and in parent's children.
        /// </summary>
        private static MonoBehaviour FindSystemMessageView(GameObject element)
        {
            if (element == null) return null;

            // First, search up the hierarchy
            var current = element.transform;
            while (current != null)
            {
                var systemMsgView = FindComponentByName(current.gameObject, "SystemMessageView");
                if (systemMsgView != null)
                    return systemMsgView;

                current = current.parent;
            }

            // Also check the popup root and its children
            var popupRoot = FindPopupRoot(element);
            if (popupRoot != null)
            {
                // Search in the popup root's hierarchy
                foreach (var mb in popupRoot.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb != null && mb.GetType().Name == "SystemMessageView")
                        return mb;
                }

                // Also search in parent of popup root
                var parent = popupRoot.transform.parent;
                while (parent != null)
                {
                    foreach (var mb in parent.GetComponents<MonoBehaviour>())
                    {
                        if (mb != null && mb.GetType().Name == "SystemMessageView")
                            return mb;
                    }
                    parent = parent.parent;
                }
            }

            // Last resort: find any active SystemMessageView in scene
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "SystemMessageView" && mb.gameObject.activeInHierarchy)
                {
                    Log($"Found SystemMessageView via scene search: {mb.gameObject.name}");
                    return mb;
                }
            }

            return null;
        }

        /// <summary>
        /// Try to find and invoke the internal handleOnClick callback stored in SystemMessageButtonView.
        /// The callback is passed during Init() and stored in a private field.
        /// </summary>
        private static bool TryInvokeStoredCallback(MonoBehaviour systemMsgButton)
        {
            if (systemMsgButton == null) return false;

            var type = systemMsgButton.GetType();
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            // Look for private fields that might store the callback
            string[] possibleCallbackFields = { "_handleOnClick", "_callback", "_onClick", "handleOnClick", "callback", "m_handleOnClick" };
            string[] possibleDataFields = { "_buttonData", "_data", "buttonData", "m_buttonData" };

            object callbackObj = null;
            object buttonDataObj = null;

            // Find callback field
            foreach (var fieldName in possibleCallbackFields)
            {
                var field = type.GetField(fieldName, flags);
                if (field != null)
                {
                    callbackObj = field.GetValue(systemMsgButton);
                    if (callbackObj != null)
                    {
                        Log($"Found callback in field '{fieldName}': {callbackObj.GetType().Name}");
                        break;
                    }
                }
            }

            // Find buttonData field
            foreach (var fieldName in possibleDataFields)
            {
                var field = type.GetField(fieldName, flags);
                if (field != null)
                {
                    buttonDataObj = field.GetValue(systemMsgButton);
                    if (buttonDataObj != null)
                    {
                        Log($"Found buttonData in field '{fieldName}': {buttonDataObj.GetType().Name}");
                        break;
                    }
                }
            }

            // Log all fields for debugging
            Log($"Listing all fields on {type.Name}:");
            foreach (var field in type.GetFields(flags | System.Reflection.BindingFlags.Public))
            {
                var val = field.GetValue(systemMsgButton);
                Log($"  Field: {field.Name} ({field.FieldType.Name}) = {(val != null ? val.ToString() : "null")}");
            }

            // Try to invoke the callback if found
            if (callbackObj != null)
            {
                var invokeMethod = callbackObj.GetType().GetMethod("Invoke");
                if (invokeMethod != null)
                {
                    try
                    {
                        var parameters = invokeMethod.GetParameters();
                        if (parameters.Length == 0)
                        {
                            Log($"Invoking callback with no parameters");
                            invokeMethod.Invoke(callbackObj, null);
                            return true;
                        }
                        else if (parameters.Length == 1 && buttonDataObj != null)
                        {
                            Log($"Invoking callback with buttonData");
                            invokeMethod.Invoke(callbackObj, new object[] { buttonDataObj });
                            return true;
                        }
                        else if (parameters.Length == 1)
                        {
                            Log($"Invoking callback with null parameter");
                            invokeMethod.Invoke(callbackObj, new object[] { null });
                            return true;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log($"Error invoking callback: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Try to invoke OnClickedReturnSource on CustomButton component.
        /// </summary>
        private static bool TryInvokeOnClickedReturnSource(GameObject element)
        {
            foreach (var mb in element.GetComponents<MonoBehaviour>())
            {
                if (mb == null || mb.GetType().Name != "CustomButton") continue;

                var type = mb.GetType();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;

                // Look for OnClickedReturnSource field
                var field = type.GetField("OnClickedReturnSource", flags);
                if (field == null)
                    field = type.GetField("_onClickedReturnSource", flags);
                if (field == null)
                    field = type.GetField("onClickedReturnSource", flags);

                if (field != null)
                {
                    var actionObj = field.GetValue(mb);
                    if (actionObj != null)
                    {
                        Log($"Found OnClickedReturnSource: {actionObj.GetType().Name}");
                        var invokeMethod = actionObj.GetType().GetMethod("Invoke");
                        if (invokeMethod != null)
                        {
                            try
                            {
                                var parameters = invokeMethod.GetParameters();
                                if (parameters.Length == 0)
                                {
                                    invokeMethod.Invoke(actionObj, null);
                                    return true;
                                }
                                else if (parameters.Length == 1)
                                {
                                    // Pass the CustomButton itself as source
                                    invokeMethod.Invoke(actionObj, new object[] { mb });
                                    return true;
                                }
                            }
                            catch (System.Exception ex)
                            {
                                Log($"Error invoking OnClickedReturnSource: {ex.InnerException?.Message ?? ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        Log($"OnClickedReturnSource field found but value is null");
                    }
                }

                // Log all fields on CustomButton for debugging
                Log($"Listing all fields on CustomButton:");
                foreach (var f in type.GetFields(flags))
                {
                    try
                    {
                        var val = f.GetValue(mb);
                        string valStr = val?.ToString() ?? "null";
                        if (valStr.Length > 50) valStr = valStr.Substring(0, 50) + "...";
                        Log($"  Field: {f.Name} ({f.FieldType.Name}) = {valStr}");
                    }
                    catch
                    {
                        Log($"  Field: {f.Name} ({f.FieldType.Name}) = <error reading>");
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Try to dismiss popup via SystemMessageManager singleton.
        /// </summary>
        private static bool TryDismissViaSystemMessageManager()
        {
            // Find SystemMessageManager type in loaded assemblies
            System.Type managerType = null;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in assembly.GetTypes())
                    {
                        if (t.Name == "SystemMessageManager" && !t.Name.Contains("+"))
                        {
                            managerType = t;
                            break;
                        }
                    }
                }
                catch { }
                if (managerType != null) break;
            }

            if (managerType == null) return false;

            // Get singleton Instance
            var instanceProp = managerType.GetProperty("Instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (instanceProp == null) return false;

            var instance = instanceProp.GetValue(null);
            if (instance == null) return false;

            // Try ClearMessageQueue() - this dismisses the current popup
            var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var clearMethod = managerType.GetMethod("ClearMessageQueue", flags, null, System.Type.EmptyTypes, null);
            if (clearMethod != null)
            {
                try
                {
                    clearMethod.Invoke(instance, null);
                    Log($"SystemMessageManager.ClearMessageQueue() succeeded");
                    return true;
                }
                catch (System.Exception ex)
                {
                    Log($"ClearMessageQueue() failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            return false;
        }

        /// <summary>
        /// Try to find PopupManager and close the current popup.
        /// </summary>
        private static bool TryCloseViaPopupManager()
        {
            // Try to find PopupManager singleton
            var popupManagerType = System.Type.GetType("Core.Meta.MainNavigation.PopUps.PopupManager, Core");
            if (popupManagerType == null)
            {
                // Try to find it by searching all types
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var t in assembly.GetTypes())
                        {
                            if (t.Name == "PopupManager")
                            {
                                popupManagerType = t;
                                Log($"Found PopupManager type in {assembly.GetName().Name}");
                                break;
                            }
                        }
                    }
                    catch { }
                    if (popupManagerType != null) break;
                }
            }

            if (popupManagerType == null)
            {
                Log($"PopupManager type not found");
                return false;
            }

            // Get Instance property
            var instanceProp = popupManagerType.GetProperty("Instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (instanceProp == null)
            {
                Log($"PopupManager.Instance property not found");
                return false;
            }

            var instance = instanceProp.GetValue(null);
            if (instance == null)
            {
                Log($"PopupManager.Instance is null");
                return false;
            }

            Log($"Found PopupManager instance: {instance}");

            // Try to find close/dismiss methods
            var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            string[] methodNames = { "Close", "ClosePopup", "Dismiss", "DismissPopup", "Hide", "HidePopup", "OnBack", "CloseCurrentPopup" };
            foreach (var methodName in methodNames)
            {
                var method = popupManagerType.GetMethod(methodName, flags, null, System.Type.EmptyTypes, null);
                if (method != null)
                {
                    try
                    {
                        Log($"Trying PopupManager.{methodName}()");
                        method.Invoke(instance, null);
                        return true;
                    }
                    catch (System.Exception ex)
                    {
                        Log($"PopupManager.{methodName}() failed: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
            }

            // Try OnBack with null ActionContext
            var onBackMethod = popupManagerType.GetMethod("OnBack", flags);
            if (onBackMethod != null)
            {
                try
                {
                    Log($"Trying PopupManager.OnBack(null)");
                    onBackMethod.Invoke(instance, new object[] { null });
                    return true;
                }
                catch (System.Exception ex)
                {
                    Log($"PopupManager.OnBack() failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            // Log available methods
            Log($"Available methods on PopupManager:");
            foreach (var m in popupManagerType.GetMethods(flags))
            {
                Log($"  {m.Name}({string.Join(", ", System.Array.ConvertAll(m.GetParameters(), p => p.ParameterType.Name))})");
            }

            return false;
        }

        /// <summary>
        /// Try to invoke HandleKeyDown(KeyCode, Modifiers) on a component.
        /// This simulates a key press through the game's input handling.
        /// </summary>
        private static bool TryInvokeHandleKeyDown(MonoBehaviour component, KeyCode keyCode)
        {
            if (component == null) return false;

            var type = component.GetType();
            var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            // Find HandleKeyDown method
            foreach (var method in type.GetMethods(flags))
            {
                if (method.Name == "HandleKeyDown")
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length >= 1)
                    {
                        try
                        {
                            Log($"Found HandleKeyDown with {parameters.Length} params, invoking with {keyCode}");

                            if (parameters.Length == 1)
                            {
                                // HandleKeyDown(KeyCode)
                                method.Invoke(component, new object[] { keyCode });
                                return true;
                            }
                            else if (parameters.Length == 2)
                            {
                                // HandleKeyDown(KeyCode, Modifiers) - pass default modifiers
                                var modifiersType = parameters[1].ParameterType;
                                object modifiersValue;

                                if (modifiersType.IsEnum)
                                {
                                    modifiersValue = System.Enum.ToObject(modifiersType, 0);
                                }
                                else if (modifiersType.IsValueType)
                                {
                                    // Struct - create default instance
                                    modifiersValue = System.Activator.CreateInstance(modifiersType);
                                }
                                else
                                {
                                    // Reference type - pass null
                                    modifiersValue = null;
                                }

                                Log($"Modifiers type: {modifiersType.Name}, IsEnum: {modifiersType.IsEnum}, IsValueType: {modifiersType.IsValueType}");
                                method.Invoke(component, new object[] { keyCode, modifiersValue });
                                return true;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Log($"HandleKeyDown error: {ex.InnerException?.Message ?? ex.Message}");
                        }
                    }
                }
            }

            Log($"HandleKeyDown method not found on {type.Name}");
            return false;
        }

        /// <summary>
        /// Tries to invoke OnBack(ActionContext) on a component.
        /// ActionContext can be null - the game handles this gracefully.
        /// </summary>
        private static bool TryInvokeOnBack(MonoBehaviour component)
        {
            if (component == null) return false;

            var type = component.GetType();

            // Try OnBack(ActionContext) first - pass null for context
            var onBackMethod = type.GetMethod("OnBack",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance,
                null,
                new System.Type[] { typeof(object) }, // ActionContext
                null);

            if (onBackMethod == null)
            {
                // Try to find the method with any parameter type
                foreach (var method in type.GetMethods(System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance))
                {
                    if (method.Name == "OnBack")
                    {
                        var parameters = method.GetParameters();
                        if (parameters.Length == 1)
                        {
                            onBackMethod = method;
                            break;
                        }
                    }
                }
            }

            if (onBackMethod != null)
            {
                try
                {
                    Log($"Invoking {type.Name}.OnBack(null)");
                    onBackMethod.Invoke(component, new object[] { null });
                    return true;
                }
                catch (System.Exception ex)
                {
                    Log($"Error invoking OnBack: {ex.InnerException?.Message ?? ex.Message}");
                }
            }
            else
            {
                Log($"OnBack method not found on {type.Name}");
            }

            return false;
        }

        /// <summary>
        /// Finds the WrapperDeckBuilder controller component which handles deck builder button clicks.
        /// The MainButton's OnPlayButton listener has a NULL target, so we need to find it manually.
        /// </summary>
        private static MonoBehaviour FindDeckBuilderController()
        {
            // Look for WrapperDeckBuilder component in the scene
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "WrapperDeckBuilder")
                {
                    return mb;
                }
            }

            // Fallback: try to find DeckBuilderController or similar
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name.Contains("DeckBuilder") && mb.GetType().Name.Contains("Controller"))
                {
                    return mb;
                }
            }

            return null;
        }

        /// <summary>
        /// Tries to invoke a parameterless method on a component by name.
        /// </summary>
        private static bool TryInvokeMethod(MonoBehaviour component, string methodName)
        {
            if (component == null) return false;

            var type = component.GetType();
            var method = type.GetMethod(methodName,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance,
                null,
                System.Type.EmptyTypes,
                null);

            if (method != null)
            {
                try
                {
                    Log($"Invoking {type.Name}.{methodName}()");
                    method.Invoke(component, null);
                    return true;
                }
                catch (System.Exception ex)
                {
                    Log($"Error invoking {methodName}: {ex.InnerException?.Message ?? ex.Message}");
                }
            }
            return false;
        }

        /// <summary>
        /// Creates a PointerEventData centered on the given element.
        /// </summary>
        private static PointerEventData CreatePointerEventData(GameObject element)
        {
            Vector2 screenPos = GetScreenPosition(element);
            return new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                pointerPress = element,
                pointerEnter = element,
                position = screenPos,
                pressPosition = screenPos
            };
        }

        /// <summary>
        /// Gets the screen position of a UI element's center.
        /// </summary>
        private static Vector2 GetScreenPosition(GameObject obj)
        {
            var rectTransform = obj.GetComponent<RectTransform>();
            if (rectTransform == null)
                return new Vector2(Screen.width / 2, Screen.height / 2);

            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            Vector3 center = (corners[0] + corners[2]) / 2f;

            var canvas = obj.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null)
                return canvas.worldCamera.WorldToScreenPoint(center);

            return center;
        }

        private static void Log(string message)
        {
            MelonLogger.Msg($"[UIActivator] {message}");
        }

        /// <summary>
        /// Checks if the game is waiting for target selection.
        /// Detection: Looks for "Submit X" button (e.g., "Submit 0", "Submit 1").
        /// Note: Cancel button alone is not reliable - can appear for other purposes.
        /// Results are cached for TargetingCacheTimeout seconds to avoid expensive scans.
        /// </summary>
        private static bool IsTargetingModeActive()
        {
            // Return cached result if still valid
            float currentTime = Time.time;
            if (currentTime - _lastTargetingScanTime < TargetingCacheTimeout)
            {
                return _cachedTargetingResult;
            }

            // Perform the expensive scan
            _lastTargetingScanTime = currentTime;
            _cachedTargetingResult = ScanForSubmitButton();
            return _cachedTargetingResult;
        }

        /// <summary>
        /// Scans all text components for a Submit button pattern.
        /// </summary>
        private static bool ScanForSubmitButton()
        {
            // Check TMP_Text components (used by StyledButton)
            foreach (var tmpText in GameObject.FindObjectsOfType<TMP_Text>())
            {
                if (tmpText == null || !tmpText.gameObject.activeInHierarchy)
                    continue;

                if (IsSubmitButton(tmpText.text, tmpText.transform))
                    return true;
            }

            // Also check legacy Text components
            foreach (var uiText in GameObject.FindObjectsOfType<Text>())
            {
                if (uiText == null || !uiText.gameObject.activeInHierarchy)
                    continue;

                if (IsSubmitButton(uiText.text, uiText.transform))
                    return true;
            }

            // Note: Cancel button alone is NOT a reliable indicator - there can be Cancel buttons
            // for other purposes (Cancel Attacks, etc.). Only the "Submit X" pattern reliably
            // indicates discard/selection mode.
            return false;
        }

        /// <summary>
        /// Checks if text matches the Submit button pattern and is inside a button/prompt element.
        /// </summary>
        private static bool IsSubmitButton(string text, Transform textTransform)
        {
            string trimmed = text?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return false;

            // Check for "Submit X" pattern (e.g., "Submit 0", "Submit 1")
            if (!SubmitButtonPattern.IsMatch(trimmed))
                return false;

            // Verify it's inside a button or prompt element
            var parent = textTransform.parent;
            while (parent != null)
            {
                string parentName = parent.name.ToLower();
                if (parentName.Contains("button") || parentName.Contains("prompt"))
                {
                    Log($"Found Submit button: '{trimmed}' on {parent.name}");
                    return true;
                }
                parent = parent.parent;
            }

            return false;
        }

        #endregion

        #region Collection Card Activation

        /// <summary>
        /// Checks if an element is a collection card (PagesMetaCardView/MetaCardView in deck builder's PoolHolder).
        /// These cards need special handling because the display view is navigated but the
        /// actual clickable element is a child.
        /// </summary>
        public static bool IsCollectionCard(GameObject element)
        {
            if (element == null) return false;

            // Check if element has PagesMetaCardView or MetaCardView component
            foreach (var mb in element.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName == "PagesMetaCardView" || typeName == "MetaCardView")
                {
                    // Verify it's in PoolHolder (collection area) not deck area
                    Transform current = element.transform;
                    while (current != null)
                    {
                        if (current.name == "PoolHolder")
                            return true;
                        current = current.parent;
                    }
                }
            }

            // Also check by element name for cases where we navigate the view directly
            if (element.name.Contains("PagesMetaCardView") || element.name.Contains("MetaCardView"))
            {
                Transform current = element.transform;
                while (current != null)
                {
                    if (current.name == "PoolHolder")
                        return true;
                    current = current.parent;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if an element is a deck list card (CustomButton - Tile in MainDeckContentCONTAINER).
        /// These cards need special handling to avoid double activation.
        /// </summary>
        public static bool IsDeckListCard(GameObject element)
        {
            if (element == null) return false;

            // Deck list cards are CustomButton - Tile elements inside MainDeckContentCONTAINER
            if (!element.name.Contains("CustomButton") && !element.name.Contains("Tile"))
                return false;

            // Check parent hierarchy for deck list container
            Transform current = element.transform;
            while (current != null)
            {
                string name = current.name;
                if (name.Contains("MainDeckContentCONTAINER") || name.Contains("MainDeck_MetaCardHolder"))
                    return true;
                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// Tries to activate a collection card by invoking OnAddClicked on the PagesMetaCardView.
        /// Collection cards have an OnAddClicked action that adds the card to the deck.
        /// </summary>
        public static ActivationResult TryActivateCollectionCard(GameObject cardElement)
        {
            if (cardElement == null)
                return new ActivationResult(false, "Card element is null");

            Log($"Attempting collection card activation for: {cardElement.name}");

            // Strategy 1: Find PagesMetaCardView and invoke OnAddClicked action
            // The OnAddClicked property is an Action<MetaCardView> that adds the card to deck
            foreach (var mb in cardElement.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName == "PagesMetaCardView" || typeName == "MetaCardView")
                {
                    Log($"Found {typeName} component, looking for OnAddClicked action");

                    // Get the OnAddClicked property which is Action<MetaCardView>
                    var onAddClickedProp = mb.GetType().GetProperty("OnAddClicked",
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);

                    if (onAddClickedProp != null)
                    {
                        try
                        {
                            var onAddClicked = onAddClickedProp.GetValue(mb);
                            if (onAddClicked != null)
                            {
                                Log($"Found OnAddClicked action, invoking...");
                                // It's an Action<MetaCardView>, invoke with the component itself
                                var invokeMethod = onAddClicked.GetType().GetMethod("Invoke");
                                if (invokeMethod != null)
                                {
                                    invokeMethod.Invoke(onAddClicked, new object[] { mb });
                                    Log($"OnAddClicked invoked successfully");
                                    return new ActivationResult(true, "Card Added", ActivationType.Button);
                                }
                            }
                            else
                            {
                                Log($"OnAddClicked property is null");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Log($"Error invoking OnAddClicked: {ex.Message}");
                        }
                    }
                    else
                    {
                        Log($"OnAddClicked property not found on {typeName}");
                    }

                    // Fallback: Try OnRemoveClicked (in case card is already in deck and needs toggling)
                    var onRemoveClickedProp = mb.GetType().GetProperty("OnRemoveClicked",
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);

                    if (onRemoveClickedProp != null)
                    {
                        Log($"Also found OnRemoveClicked - card may support add/remove toggle");
                    }
                }
            }

            // Strategy 2: Find a CustomButton child element (e.g., the card's click area)
            var customButtonChild = FindCustomButtonInHierarchy(cardElement);
            if (customButtonChild != null)
            {
                Log($"Found CustomButton child: {customButtonChild.name}");
                var result = SimulatePointerClick(customButtonChild);
                TryInvokeCustomButtonOnClick(customButtonChild);
                return result;
            }

            // Strategy 3: Try IPointerClickHandler interface on the MetaCardView
            foreach (var mb in cardElement.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (mb is IPointerClickHandler clickHandler)
                {
                    Log($"Invoking IPointerClickHandler.OnPointerClick on {mb.GetType().Name}");
                    var pointer = CreatePointerEventData(cardElement);
                    clickHandler.OnPointerClick(pointer);
                    return new ActivationResult(true, "Activated", ActivationType.PointerClick);
                }
            }

            // Strategy 4: Full pointer simulation on the card view itself
            Log("Fallback: Full pointer simulation on card view");
            return SimulatePointerClick(cardElement);
        }

        /// <summary>
        /// Searches for a CustomButton component anywhere in the element hierarchy.
        /// Returns the first GameObject with a CustomButton found.
        /// </summary>
        private static GameObject FindCustomButtonInHierarchy(GameObject root)
        {
            if (root == null) return null;

            // Check root first
            foreach (var mb in root.GetComponents<MonoBehaviour>())
            {
                if (IsCustomButton(mb))
                    return root;
            }

            // Search all children recursively
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root.transform) continue;
                if (!child.gameObject.activeInHierarchy) continue;

                foreach (var mb in child.GetComponents<MonoBehaviour>())
                {
                    if (IsCustomButton(mb))
                        return child.gameObject;
                }
            }

            return null;
        }

        #endregion

        #region Deck Selection

        /// <summary>
        /// Attempts to select a deck entry by invoking its OnDeckClick method.
        /// This is needed because MTGA's deck CustomButtons don't reliably respond
        /// to standard pointer events - the DeckView.OnDeckClick() method must be called directly.
        /// </summary>
        /// <param name="deckElement">The deck UI element (CustomButton on DeckView_Base/UI)</param>
        /// <returns>True if deck was selected successfully</returns>
        public static bool TrySelectDeck(GameObject deckElement)
        {
            if (deckElement == null) return false;

            Log($"Attempting deck selection for: {deckElement.name}");

            // Find the DeckView component in parent hierarchy
            // Structure: DeckView_Base(Clone)/UI <- we click this
            // The DeckView component is on DeckView_Base(Clone)
            var deckView = FindDeckViewInParents(deckElement);
            if (deckView == null)
            {
                Log("No DeckView component found in parents");
                return false;
            }

            Log($"Found DeckView on: {deckView.gameObject.name}");

            // Invoke OnDeckClick() on the DeckView component
            var deckViewType = deckView.GetType();
            var onDeckClickMethod = deckViewType.GetMethod("OnDeckClick",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (onDeckClickMethod != null && onDeckClickMethod.GetParameters().Length == 0)
            {
                try
                {
                    Log($"Invoking {deckViewType.Name}.OnDeckClick()");
                    onDeckClickMethod.Invoke(deckView, null);
                    return true;
                }
                catch (System.Exception ex)
                {
                    Log($"Error invoking OnDeckClick: {ex.Message}");
                }
            }
            else
            {
                Log("OnDeckClick method not found on DeckView");
            }

            return false;
        }

        /// <summary>
        /// Checks if an element is a deck entry (for specialized handling).
        /// TextBox elements are excluded - they're for renaming, not selecting.
        /// </summary>
        public static bool IsDeckEntry(GameObject element)
        {
            if (element == null) return false;

            // TextBox is for renaming the deck, not selecting it
            if (element.name == "TextBox")
                return false;

            // Check parent hierarchy for DeckView_Base
            Transform current = element.transform;
            int depth = 0;
            while (current != null && depth < MaxDeckSearchDepth)
            {
                if (current.name.Contains("DeckView_Base"))
                    return true;
                current = current.parent;
                depth++;
            }

            return false;
        }

        private static MonoBehaviour FindDeckViewInParents(GameObject element)
        {
            Transform current = element.transform;
            int depth = 0;

            while (current != null && depth < MaxDeckViewSearchDepth)
            {
                // Look for DeckView component
                foreach (var mb in current.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == DeckViewTypeName)
                        return mb;
                }

                current = current.parent;
                depth++;
            }

            return null;
        }

        /// <summary>
        /// Check if a deck is currently selected.
        /// Compares the deck's DeckView with DeckViewSelector._selectedDeckView.
        /// </summary>
        /// <param name="deckElement">The deck UI element (CustomButton on DeckView_Base/UI)</param>
        /// <returns>True if the deck is selected, false otherwise or if not a deck</returns>
        public static bool IsDeckSelected(GameObject deckElement)
        {
            if (deckElement == null) return false;

            var deckView = FindDeckViewInParents(deckElement);
            if (deckView == null) return false;

            // Find DeckViewSelector and get _selectedDeckView
            var selectedDeckView = GetSelectedDeckView();
            if (selectedDeckView == null) return false;

            // Compare if this deck's DeckView matches the selected one
            return deckView == selectedDeckView;
        }

        /// <summary>
        /// Get the currently selected DeckView from DeckViewSelector.
        /// </summary>
        private static MonoBehaviour GetSelectedDeckView()
        {
            // Find DeckViewSelector_Base
            var selectorTransform = GameObject.FindObjectsOfType<Transform>()
                .FirstOrDefault(t => t.name.Contains("DeckViewSelector_Base") && t.gameObject.activeInHierarchy);

            if (selectorTransform == null) return null;

            // Find DeckViewSelector component
            MonoBehaviour selector = null;
            foreach (var mb in selectorTransform.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "DeckViewSelector")
                {
                    selector = mb;
                    break;
                }
            }

            if (selector == null) return null;

            // Read _selectedDeckView field
            try
            {
                var field = selector.GetType().GetField("_selectedDeckView",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (field != null)
                {
                    return field.GetValue(selector) as MonoBehaviour;
                }
            }
            catch (System.Exception ex)
            {
                Log($"Error getting selected deck: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// DEBUG: Inspect a DeckView component to find selection-related properties.
        /// </summary>
        public static void DebugInspectDeckView(GameObject deckElement)
        {
            var deckView = FindDeckViewInParents(deckElement);
            if (deckView == null)
            {
                MelonLogger.Msg("[DeckView DEBUG] No DeckView found");
                return;
            }

            var type = deckView.GetType();
            MelonLogger.Msg($"[DeckView DEBUG] === Inspecting {type.Name} on {deckView.gameObject.name} ===");

            // List all properties
            MelonLogger.Msg("[DeckView DEBUG] Properties:");
            foreach (var prop in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            {
                try
                {
                    var value = prop.GetValue(deckView);
                    MelonLogger.Msg($"[DeckView DEBUG]   {prop.Name} ({prop.PropertyType.Name}) = {value}");
                }
                catch
                {
                    MelonLogger.Msg($"[DeckView DEBUG]   {prop.Name} ({prop.PropertyType.Name}) = <error reading>");
                }
            }

            // List all fields
            MelonLogger.Msg("[DeckView DEBUG] Fields:");
            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            {
                try
                {
                    var value = field.GetValue(deckView);
                    // For complex objects, just show type
                    string valueStr = value == null ? "null" :
                        (value is string || value is bool || value is int || value is float || value is System.Enum)
                            ? value.ToString()
                            : $"<{value.GetType().Name}>";
                    MelonLogger.Msg($"[DeckView DEBUG]   {field.Name} ({field.FieldType.Name}) = {valueStr}");
                }
                catch
                {
                    MelonLogger.Msg($"[DeckView DEBUG]   {field.Name} ({field.FieldType.Name}) = <error reading>");
                }
            }

            MelonLogger.Msg("[DeckView DEBUG] === End Inspection ===");
        }

        #endregion
    }

    /// <summary>
    /// Result of a UI element activation attempt.
    /// </summary>
    public struct ActivationResult
    {
        public bool Success { get; }
        public string Message { get; }
        public ActivationType Type { get; }

        public ActivationResult(bool success, string message, ActivationType type = ActivationType.Unknown)
        {
            Success = success;
            Message = message;
            Type = type;
        }
    }

    /// <summary>
    /// Types of UI elements that can be activated.
    /// </summary>
    public enum ActivationType
    {
        Unknown,
        Button,
        Toggle,
        InputField,
        PointerClick
    }
}
