using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Detects and navigates the View_ChooseXInterface popup that appears when
    /// casting X-cost spells, choosing any amount, or choosing a die roll.
    /// Polled from DuelNavigator at high priority (between mana picker and browser).
    /// Up/Down adjust by 1, PageUp/PageDown adjust by 5, Enter submits, Backspace cancels.
    /// Zone shortcuts (B, A, C, G, etc.) release focus so the user can browse zones;
    /// Tab reclaims ChooseX focus.
    /// </summary>
    public class ChooseXNavigator
    {
        private readonly IAnnouncementService _announcer;

        // Reflection cache
        private static Type _viewType;
        private static FieldInfo _rootField;
        private static FieldInfo _upArrowField;
        private static FieldInfo _downArrowField;
        private static FieldInfo _upFiveArrowField;
        private static FieldInfo _downFiveArrowField;
        private static FieldInfo _buttonLabelField;
        private static FieldInfo _confirmButtonField;
        private static bool _reflectionInitialized;
        private static bool _reflectionFailed;

        // State
        private bool _isActive;
        private bool _hasAnnounced;
        private bool _hasFocus; // Whether ChooseX has input focus (vs zone navigation)
        private MonoBehaviour _viewInstance;
        private uint? _maxValue;

        // Polling interval
        private float _lastScanTime;
        private const float ScanInterval = 0.1f;

        public bool IsActive => _isActive;

        public ChooseXNavigator(IAnnouncementService announcer)
        {
            _announcer = announcer;
        }

        /// <summary>
        /// Called every frame from DuelNavigator. Polls for open View_ChooseXInterface.
        /// </summary>
        public void Update()
        {
            if (_reflectionFailed)
                return;

            if (!_reflectionInitialized)
                InitializeReflection();

            if (_reflectionFailed || _viewType == null)
                return;

            float time = Time.time;
            if (time - _lastScanTime < ScanInterval)
                return;
            _lastScanTime = time;

            // Find active View_ChooseXInterface with active _root
            MonoBehaviour activeView = FindActiveView();

            if (activeView != null && !_isActive)
            {
                Enter(activeView);
            }
            else if (activeView == null && _isActive)
            {
                Exit();
            }
        }

        /// <summary>
        /// Handles keyboard input when active. Returns true if key was consumed.
        /// When ChooseX has focus: Up/Down = +1/-1, PageUp/PageDown = +5/-5.
        /// Tab always reclaims focus. Zone shortcuts release focus and pass through.
        /// Enter/Space = submit, Backspace = cancel (always consumed).
        /// </summary>
        public bool HandleInput()
        {
            if (!_isActive)
                return false;

            // Announce on first frame after entering
            if (!_hasAnnounced)
            {
                AnnounceEntry();
                _hasAnnounced = true;
            }

            // Tab always reclaims ChooseX focus
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                _hasFocus = true;
                AnnounceCurrentValue();
                AccessibleArenaMod.Instance?.CardNavigator?.Deactivate();
                return true;
            }

            // Enter/Space = submit (always consumed when active)
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                Submit();
                return true;
            }

            // Backspace = cancel (always consumed, but let Shift/Ctrl modifiers pass through)
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                if (!shift && !ctrl)
                {
                    Cancel();
                    return true;
                }
                return false;
            }

            // If ChooseX doesn't have focus (user navigated to a zone), only consume the above.
            // Let zone navigation keys pass through.
            if (!_hasFocus)
                return false;

            // Up arrow = increment by 1
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                ClickButton(_upArrowField, 1);
                return true;
            }

            // Down arrow = decrement by 1
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                ClickButton(_downArrowField, -1);
                return true;
            }

            // Page Up = increment by 5
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                ClickButton(_upFiveArrowField, 5);
                return true;
            }

            // Page Down = decrement by 5
            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                ClickButton(_downFiveArrowField, -5);
                return true;
            }

            // Zone shortcuts release focus and pass through
            if (IsZoneShortcut())
            {
                _hasFocus = false;
                return false;
            }

            return false;
        }

        /// <summary>
        /// Returns true if a zone navigation shortcut key is being pressed.
        /// </summary>
        private bool IsZoneShortcut()
        {
            return Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.A) ||
                   Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.C) ||
                   Input.GetKeyDown(KeyCode.G) || Input.GetKeyDown(KeyCode.X) ||
                   Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.W) ||
                   Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.L) ||
                   Input.GetKeyDown(KeyCode.V);
        }

        private MonoBehaviour FindActiveView()
        {
            var instances = UnityEngine.Object.FindObjectsOfType(_viewType);
            foreach (var inst in instances)
            {
                try
                {
                    var mb = inst as MonoBehaviour;
                    if (mb == null) continue;

                    var root = _rootField.GetValue(mb) as GameObject;
                    if (root != null && root.activeInHierarchy)
                        return mb;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[ChooseXNavigator] Error checking view: {ex.Message}");
                }
            }
            return null;
        }

        private void Enter(MonoBehaviour view)
        {
            _viewInstance = view;
            _isActive = true;
            _hasFocus = true;
            _hasAnnounced = false;
            _maxValue = FindMaxValue();

            // Deactivate card info navigator so Up/Down controls the spinner, not card blocks
            AccessibleArenaMod.Instance?.CardNavigator?.Deactivate();

            MelonLogger.Msg($"[ChooseXNavigator] Entered ChooseX mode (max={_maxValue?.ToString() ?? "unknown"})");
        }

        private void Exit()
        {
            _isActive = false;
            _hasFocus = false;
            _hasAnnounced = false;
            _viewInstance = null;
            _maxValue = null;
            MelonLogger.Msg("[ChooseXNavigator] Exited ChooseX mode");
        }

        private void AnnounceEntry()
        {
            string labelText = GetLabelText();
            if (string.IsNullOrEmpty(labelText))
                labelText = "0";

            // Check which buttons are available to determine max range
            bool hasFiveButtons = IsButtonActive(_upFiveArrowField);
            string rangeInfo = hasFiveButtons ? " (PageUp/PageDown: +5/-5)" : "";

            string entryText = _maxValue.HasValue
                ? Strings.ChooseXEntryWithMax(labelText, _maxValue.Value)
                : Strings.ChooseXEntry(labelText);

            _announcer.AnnounceInterrupt(entryText + rangeInfo);
        }

        private void AnnounceCurrentValue()
        {
            string labelText = GetLabelText();
            if (!string.IsNullOrEmpty(labelText))
            {
                _announcer.AnnounceInterrupt(labelText);
            }
        }

        private void ClickButton(FieldInfo buttonField, int direction)
        {
            if (_viewInstance == null || buttonField == null) return;

            try
            {
                var button = buttonField.GetValue(_viewInstance) as Button;
                if (button == null || !button.interactable)
                {
                    // Button disabled - already at min/max
                    string limitMsg = direction > 0
                        ? Strings.ChooseXAtMax
                        : Strings.ChooseXAtMin;
                    _announcer.AnnounceInterrupt(limitMsg);
                    return;
                }

                button.onClick.Invoke();

                // Read and announce new value
                string newLabel = GetLabelText();
                if (!string.IsNullOrEmpty(newLabel))
                {
                    // Check if we just arrived at min/max (button now disabled after click)
                    string suffix = "";
                    if (!button.interactable)
                    {
                        suffix = direction > 0
                            ? $", {Strings.ChooseXAtMax}"
                            : $", {Strings.ChooseXAtMin}";
                    }

                    _announcer.AnnounceInterrupt(newLabel + suffix);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ChooseXNavigator] Error clicking button: {ex.Message}");
            }
        }

        private void Submit()
        {
            if (_viewInstance == null) return;

            try
            {
                var confirmButton = _confirmButtonField.GetValue(_viewInstance) as Button;
                if (confirmButton == null || !confirmButton.interactable)
                {
                    _announcer.AnnounceInterrupt(Strings.ChooseXCannotSubmit);
                    return;
                }

                string currentValue = GetLabelText() ?? "?";
                confirmButton.onClick.Invoke();
                _announcer.AnnounceInterrupt(Strings.ChooseXConfirmed(currentValue));
                MelonLogger.Msg($"[ChooseXNavigator] Submitted: {currentValue}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ChooseXNavigator] Error submitting: {ex.Message}");
            }
        }

        private void Cancel()
        {
            // Cancel via the prompt button (Abbrechen) - same mechanism as other workflows
            try
            {
                foreach (var go in GameObject.FindObjectsOfType<GameObject>())
                {
                    if (go == null || !go.activeInHierarchy) continue;
                    if (go.name.Contains("PromptButton_Secondary") || go.name.Contains("PromptButton_Primary"))
                    {
                        // Find the cancel button - check text for cancel-like content
                        var button = go.GetComponent<Button>();
                        if (button != null && button.interactable)
                        {
                            button.onClick.Invoke();
                            _announcer.AnnounceInterrupt(Strings.ChooseXCancelled);
                            MelonLogger.Msg("[ChooseXNavigator] Cancelled");
                            return;
                        }
                    }
                }

                _announcer.AnnounceInterrupt(Strings.ChooseXCancelled);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ChooseXNavigator] Error cancelling: {ex.Message}");
            }
        }

        private string GetLabelText()
        {
            if (_viewInstance == null || _buttonLabelField == null) return null;

            try
            {
                var label = _buttonLabelField.GetValue(_viewInstance);
                if (label == null) return null;

                // TextMeshProUGUI.text
                var textProp = label.GetType().GetProperty("text", PublicInstance);
                if (textProp != null)
                    return textProp.GetValue(label) as string;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ChooseXNavigator] Error reading label: {ex.Message}");
            }
            return null;
        }

        private bool IsButtonActive(FieldInfo buttonField)
        {
            if (_viewInstance == null || buttonField == null) return false;

            try
            {
                var button = buttonField.GetValue(_viewInstance) as Button;
                return button != null && button.gameObject.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Finds the maximum X value by walking the ValueModified event's invocation list
        /// to find the workflow (ChooseXWorkflow or NumericInputWorkflow) and reading its _max field.
        /// Returns null if the max is unreasonably large (game uses int.MaxValue for "unlimited",
        /// where the real limit is enforced by button disabled state / mana availability).
        /// </summary>
        private uint? FindMaxValue()
        {
            if (_viewInstance == null) return null;

            try
            {
                // The ValueModified event backing field stores the delegate
                var eventField = _viewType.GetField("ValueModified", PrivateInstance);
                if (eventField == null) return null;

                var eventDelegate = eventField.GetValue(_viewInstance) as Delegate;
                if (eventDelegate == null) return null;

                foreach (var d in eventDelegate.GetInvocationList())
                {
                    var target = d.Target;
                    if (target == null) continue;

                    // Both ChooseXWorkflow and NumericInputWorkflow have _max (uint)
                    var maxField = target.GetType().GetField("_max", PrivateInstance);
                    if (maxField != null)
                    {
                        uint rawMax = (uint)maxField.GetValue(target);
                        // Game uses huge values (int.MaxValue) for "unlimited" X spells
                        // where mana availability enforces the real limit via button state.
                        // Only announce max when it's a meaningful number.
                        if (rawMax > 100)
                            return null;
                        return rawMax;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ChooseXNavigator] Error finding max value: {ex.Message}");
            }

            return null;
        }

        private static void InitializeReflection()
        {
            _reflectionInitialized = true;

            try
            {
                _viewType = FindType("View_ChooseXInterface");
                if (_viewType == null)
                {
                    MelonLogger.Warning("[ChooseXNavigator] View_ChooseXInterface type not found");
                    _reflectionFailed = true;
                    return;
                }

                MelonLogger.Msg($"[ChooseXNavigator] Found View_ChooseXInterface: {_viewType.FullName}");

                _rootField = _viewType.GetField("_root", PrivateInstance);
                _upArrowField = _viewType.GetField("_upArrowButton", PrivateInstance);
                _downArrowField = _viewType.GetField("_downArrowButton", PrivateInstance);
                _upFiveArrowField = _viewType.GetField("_upFiveArrowButton", PrivateInstance);
                _downFiveArrowField = _viewType.GetField("_downFiveArrowButton", PrivateInstance);
                _buttonLabelField = _viewType.GetField("_buttonLabel", PrivateInstance);
                _confirmButtonField = _viewType.GetField("_confirmationButton", PrivateInstance);

                if (_rootField == null)
                {
                    MelonLogger.Warning("[ChooseXNavigator] _root field not found");
                    _reflectionFailed = true;
                    return;
                }

                if (_upArrowField == null || _downArrowField == null)
                {
                    MelonLogger.Warning("[ChooseXNavigator] Arrow button fields not found");
                    _reflectionFailed = true;
                    return;
                }

                if (_buttonLabelField == null)
                {
                    MelonLogger.Warning("[ChooseXNavigator] _buttonLabel field not found");
                    _reflectionFailed = true;
                    return;
                }

                if (_confirmButtonField == null)
                {
                    MelonLogger.Warning("[ChooseXNavigator] _confirmationButton field not found");
                    _reflectionFailed = true;
                    return;
                }

                MelonLogger.Msg("[ChooseXNavigator] Reflection initialized successfully");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ChooseXNavigator] Reflection init failed: {ex.Message}");
                _reflectionFailed = true;
            }
        }
    }
}
