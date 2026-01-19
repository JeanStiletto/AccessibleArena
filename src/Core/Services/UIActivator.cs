using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using MelonLoader;
using System.Collections;
using System.Text.RegularExpressions;
// NOTE: System.Runtime.InteropServices kept for potential WinAPI reactivation
// using System.Runtime.InteropServices;

namespace MTGAAccessibility.Core.Services
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
                    // Simple toggle without CustomButton - just flip the state
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
                Log($"CustomButton detected (early check), trying pointer simulation first");
                var pointerResult = SimulatePointerClick(element);

                // Also try onClick reflection as additional handler
                TryInvokeCustomButtonOnClick(element);

                return pointerResult;
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
            // These buttons need their OnClick method invoked directly
            var systemMessageButton = FindComponentByName(element, "SystemMessageButtonView");
            if (systemMessageButton != null)
            {
                Log($"SystemMessageButtonView detected, trying OnClick method");
                if (TryInvokeMethod(systemMessageButton, "OnClick"))
                {
                    return new ActivationResult(true, "Activated", ActivationType.Button);
                }
                // Also try OnButtonClicked pattern
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
        /// </summary>
        public static bool IsDeckEntry(GameObject element)
        {
            if (element == null) return false;

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
