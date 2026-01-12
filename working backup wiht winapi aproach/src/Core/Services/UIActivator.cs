using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using MelonLoader;
using System.Collections;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

namespace MTGAAccessibility.Core.Services
{
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

    /// <summary>
    /// Centralized UI activation utilities.
    /// Handles clicking buttons, toggling checkboxes, focusing input fields,
    /// and playing cards from hand.
    /// </summary>
    public static class UIActivator
    {
        #region General UI Activation

        /// <summary>
        /// Activates a UI element based on its type (button, toggle, input field, etc.).
        /// </summary>
        public static ActivationResult Activate(GameObject element)
        {
            if (element == null)
                return new ActivationResult(false, "Element is null");

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
                toggle.isOn = !toggle.isOn;
                string state = toggle.isOn ? "Checked" : "Unchecked";
                return new ActivationResult(true, state, ActivationType.Toggle);
            }

            // Try Button
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

            // Fallback: Pointer event simulation
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

            // Full click sequence
            ExecuteEvents.Execute(element, pointer, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(element, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(element, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(element, pointer, ExecuteEvents.pointerClickHandler);

            // Also try on immediate children
            foreach (Transform child in element.transform)
            {
                ExecuteEvents.Execute(child.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            }

            // Try IPointerClickHandler components directly
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
            // For card dropping: the game processes clicks on the held card
            // Send click events even without a raycast target
            Log($"No raycast hit at {screenPosition}, sending global click");

            // Execute pointer events on the EventSystem itself
            // This allows the game to process position-based input
            ExecuteEvents.Execute(eventSystem.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(eventSystem.gameObject, pointer, ExecuteEvents.pointerUpHandler);

            return new ActivationResult(true, "Global click sent", ActivationType.PointerClick);
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
                    if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                    if (mb.GetType().Name != "CustomButton") continue;

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
        public static void PlayCardViaTwoClick(GameObject card, System.Action<bool, string> callback = null, TargetNavigator targetNavigator = null)
        {
            if (card == null)
            {
                callback?.Invoke(false, "Card is null");
                return;
            }

            Log($"=== PLAYING CARD: {card.name} ===");
            MelonCoroutines.Start(PlayCardCoroutine(card, callback, targetNavigator));
        }

        private static IEnumerator PlayCardCoroutine(GameObject card, System.Action<bool, string> callback, TargetNavigator targetNavigator)
        {
            // Step 0: Check if card is a land (lands don't need targeting)
            var cardInfo = CardDetector.ExtractCardInfo(card);
            bool isLand = cardInfo.TypeLine?.ToLower().Contains("land") ?? false;
            if (isLand)
            {
                Log($"Card is a land ({cardInfo.TypeLine}), will skip targeting mode");
            }

            // Step 1: First click (select)
            Log("Step 1: First click (select)");
            var click1 = SimulatePointerClick(card);
            if (!click1.Success)
            {
                Log("Failed at step 1");
                callback?.Invoke(false, "First click failed");
                yield break;
            }

            // Step 2: Wait for selection to register
            yield return new WaitForSeconds(0.2f);

            // Step 3: Second click (pick up)
            Log("Step 2: Second click (pick up)");
            var click2 = SimulatePointerClick(card);
            if (!click2.Success)
            {
                Log("Failed at step 2");
                callback?.Invoke(false, "Second click failed");
                yield break;
            }

            // Step 4: Wait for card to be held
            yield return new WaitForSeconds(0.5f);

            // Step 5: Click screen center using REAL mouse (Windows API)
            // The game checks Input.mousePosition, not event position data
            Log("Step 3: Click screen center via Windows API (confirm play)");

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

            // Step 6: Wait for game to process the play and UI to update
            // Need enough time for targeting UI (Cancel/Submit buttons) to appear
            yield return new WaitForSeconds(0.6f);

            // Step 7: Check if spell needs targeting by looking for "Submit X" button
            // The game shows "Submit 0", "Submit 1", etc. when waiting for target selection
            if (targetNavigator != null)
            {
                if (isLand)
                {
                    Log("Step 4: Land played, skipping targeting mode");
                }
                else
                {
                    bool needsTargeting = IsTargetingModeActive();

                    if (needsTargeting)
                    {
                        Log("Step 4: Submit button detected, entering targeting mode");
                        targetNavigator.EnterTargetMode();
                    }
                    else
                    {
                        Log("Step 4: No Submit button, spell completed");
                    }
                }
            }

            Log("=== CARD PLAY COMPLETE ===");
            callback?.Invoke(true, "Card played");
        }

        #endregion

        #region Helpers

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
        /// Detection methods:
        /// 1. "Submit X" button visible (e.g., "Submit 0", "Submit 1")
        /// 2. "Cancel" button on a PromptButton_Secondary (indicates pending action)
        /// </summary>
        private static bool IsTargetingModeActive()
        {
            // Pattern matches "Submit" followed by optional space and a number
            var submitPattern = new Regex(@"^Submit\s*\d+$", RegexOptions.IgnoreCase);
            bool foundCancelButton = false;

            // Check TMP_Text components (used by StyledButton)
            foreach (var tmpText in GameObject.FindObjectsOfType<TMP_Text>())
            {
                if (tmpText == null || !tmpText.gameObject.activeInHierarchy)
                    continue;

                string text = tmpText.text?.Trim();
                if (string.IsNullOrEmpty(text))
                    continue;

                // Check for Submit X pattern
                if (submitPattern.IsMatch(text))
                {
                    var parent = tmpText.transform.parent;
                    while (parent != null)
                    {
                        string parentName = parent.name.ToLower();
                        if (parentName.Contains("button") || parentName.Contains("prompt"))
                        {
                            Log($"Found Submit button: '{text}' on {parent.name}");
                            return true;
                        }
                        parent = parent.parent;
                    }
                }

                // Check for Cancel button on secondary prompt (indicates pending action)
                if (text.Equals("Cancel", System.StringComparison.OrdinalIgnoreCase))
                {
                    var parent = tmpText.transform.parent;
                    while (parent != null)
                    {
                        string parentName = parent.name;
                        if (parentName.Contains("PromptButton_Secondary"))
                        {
                            Log($"Found Cancel button on secondary prompt: {parentName}");
                            foundCancelButton = true;
                            break;
                        }
                        parent = parent.parent;
                    }
                }
            }

            // Also check legacy Text components
            foreach (var uiText in GameObject.FindObjectsOfType<Text>())
            {
                if (uiText == null || !uiText.gameObject.activeInHierarchy)
                    continue;

                string text = uiText.text?.Trim();
                if (string.IsNullOrEmpty(text))
                    continue;

                if (submitPattern.IsMatch(text))
                {
                    var parent = uiText.transform.parent;
                    while (parent != null)
                    {
                        string parentName = parent.name.ToLower();
                        if (parentName.Contains("button") || parentName.Contains("prompt"))
                        {
                            Log($"Found Submit button: '{text}' on {parent.name}");
                            return true;
                        }
                        parent = parent.parent;
                    }
                }

                if (text.Equals("Cancel", System.StringComparison.OrdinalIgnoreCase))
                {
                    var parent = uiText.transform.parent;
                    while (parent != null)
                    {
                        string parentName = parent.name;
                        if (parentName.Contains("PromptButton_Secondary"))
                        {
                            Log($"Found Cancel button on secondary prompt: {parentName}");
                            foundCancelButton = true;
                            break;
                        }
                        parent = parent.parent;
                    }
                }
            }

            // Cancel button on secondary prompt indicates targeting/pending action
            if (foundCancelButton)
            {
                Log("Targeting mode detected via Cancel button");
                return true;
            }

            return false;
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
