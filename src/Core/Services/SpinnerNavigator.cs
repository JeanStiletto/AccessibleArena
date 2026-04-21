using System;
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
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Detects and navigates SpinnerAnimated widgets used for counter distribution
    /// (e.g. Crashing Wave distributing stun counters across creatures).
    /// Polled from DuelNavigator at high priority (between ChooseXNavigator and BrowserNavigator).
    /// Left/Right navigate between spinners, Up/Down adjust values, Enter submits, Backspace cancels.
    /// Zone shortcuts (B, A, C, G, etc.) pass through so the user can browse the battlefield;
    /// Tab reclaims spinner focus (like BrowserNavigator pattern).
    /// </summary>
    public class SpinnerNavigator
    {
        private readonly IAnnouncementService _announcer;

        // Reflection cache
        private sealed class SpinnerAnimatedHandles
        {
            public PropertyInfo InstanceId;
            public PropertyInfo Value;
            public FieldInfo UpButton;
            public FieldInfo DownButton;
            public FieldInfo Group; // SpinnerAnimated._group
        }

        private sealed class SpinnerGroupHandles
        {
            public PropertyInfo MaxValue;
        }

        private static Type _spinnerAnimatedType;
        private static Type _spinnerGroupType;
        private static bool _reflectionInitialized;
        private static bool _reflectionFailed;

        private static readonly ReflectionCache<SpinnerAnimatedHandles> _spinnerCache = new ReflectionCache<SpinnerAnimatedHandles>(
            builder: t => new SpinnerAnimatedHandles
            {
                InstanceId = t.GetProperty("InstanceId", PublicInstance),
                Value = t.GetProperty("Value", PublicInstance),
                UpButton = t.GetField("_upButton", PrivateInstance),
                DownButton = t.GetField("_downButton", PrivateInstance),
                Group = t.GetField("_group", PrivateInstance),
            },
            validator: h => h.InstanceId != null && h.Value != null && h.UpButton != null && h.DownButton != null,
            logTag: "SpinnerNavigator",
            logSubject: "SpinnerAnimated");

        private static readonly ReflectionCache<SpinnerGroupHandles> _spinnerGroupCache = new ReflectionCache<SpinnerGroupHandles>(
            builder: t => new SpinnerGroupHandles
            {
                MaxValue = t.GetProperty("MaxValue", PublicInstance),
            },
            validator: h => h.MaxValue != null,
            logTag: "SpinnerNavigator",
            logSubject: "SpinnerGroup");

        // State
        private bool _isActive;
        private bool _hasAnnounced;
        private readonly List<MonoBehaviour> _spinners = new List<MonoBehaviour>();
        private int _currentIndex;
        private int _totalMax;
        private bool _hasFocus; // Whether spinner has input focus (vs zone navigation)

        // Polling interval
        private float _lastScanTime;
        private const float ScanInterval = 0.1f;

        public bool IsActive => _isActive;

        public SpinnerNavigator(IAnnouncementService announcer)
        {
            _announcer = announcer;
        }

        /// <summary>
        /// Called every frame from DuelNavigator. Polls for active SpinnerAnimated components.
        /// </summary>
        public void Update()
        {
            if (_reflectionFailed)
                return;

            if (!_reflectionInitialized)
                InitializeReflection();

            if (_reflectionFailed || _spinnerAnimatedType == null)
                return;

            float time = Time.time;
            if (time - _lastScanTime < ScanInterval)
                return;
            _lastScanTime = time;

            var activeSpinners = FindActiveSpinners();

            if (activeSpinners.Count > 0 && !_isActive)
            {
                Enter(activeSpinners);
            }
            else if (activeSpinners.Count == 0 && _isActive)
            {
                Exit();
            }
            else if (_isActive && activeSpinners.Count > 0)
            {
                RefreshSpinners(activeSpinners);
            }
        }

        /// <summary>
        /// Handles keyboard input when active. Returns true if key was consumed.
        /// When spinner has focus: Left/Right navigate spinners, Up/Down adjust values.
        /// Tab always reclaims spinner focus. Zone shortcuts pass through.
        /// Enter/Space submits, Backspace cancels.
        /// </summary>
        public bool HandleInput()
        {
            if (!_isActive)
                return false;

            // Announce on first frame after entering
            if (!_hasAnnounced)
            {
                ClearEventSystemSelection();
                AnnounceEntry();
                _hasAnnounced = true;
            }

            // Tab always reclaims spinner focus (like BrowserNavigator pattern)
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                ClearEventSystemSelection();
                _hasFocus = true;
                AnnounceCurrentSpinner();
                // Deactivate card info navigator so Up/Down controls the spinner
                AccessibleArenaMod.Instance?.CardNavigator?.Deactivate();
                return true;
            }

            // Enter/Space = submit (always consumed when spinner is active)
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                ClearEventSystemSelection();
                Submit();
                return true;
            }

            // Backspace = cancel (always consumed when spinner is active)
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                // Let Shift+Backspace and Ctrl+Backspace pass through for phase skip controls
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                if (!shift && !ctrl)
                {
                    Cancel();
                    return true;
                }
                return false;
            }

            // If spinner doesn't have focus (user navigated to a zone), only consume Tab/Enter/Space/Backspace above.
            // Let zone navigation keys (B, A, C, G, arrows, etc.) pass through.
            if (!_hasFocus)
                return false;

            // Left = previous spinner
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                ClearEventSystemSelection();
                Navigate(-1);
                return true;
            }

            // Right = next spinner
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                ClearEventSystemSelection();
                Navigate(1);
                return true;
            }

            // Up = increment value
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                ClearEventSystemSelection();
                AdjustValue(1);
                return true;
            }

            // Down = decrement value
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                ClearEventSystemSelection();
                AdjustValue(-1);
                return true;
            }

            // Home = jump to first spinner
            if (Input.GetKeyDown(KeyCode.Home))
            {
                ClearEventSystemSelection();
                _currentIndex = 0;
                AnnounceCurrentSpinner();
                return true;
            }

            // End = jump to last spinner
            if (Input.GetKeyDown(KeyCode.End))
            {
                ClearEventSystemSelection();
                _currentIndex = _spinners.Count - 1;
                AnnounceCurrentSpinner();
                return true;
            }

            // When spinner has focus, let zone shortcut keys (B, A, C, G, etc.) release focus
            // so zone navigation can take over. Don't consume them.
            if (IsZoneShortcut())
            {
                _hasFocus = false;
                return false;
            }

            return false;
        }

        /// <summary>
        /// Returns true if a zone navigation shortcut key is being pressed.
        /// These should release spinner focus and pass through to zone/battlefield navigators.
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

        private List<MonoBehaviour> FindActiveSpinners()
        {
            var result = new List<MonoBehaviour>();
            var h = _spinnerCache.Handles;
            var instances = UnityEngine.Object.FindObjectsOfType(_spinnerAnimatedType);
            foreach (var inst in instances)
            {
                try
                {
                    var mb = inst as MonoBehaviour;
                    if (mb == null || !mb.gameObject.activeInHierarchy)
                        continue;

                    uint instanceId = (uint)h.InstanceId.GetValue(mb);
                    if (instanceId == 0)
                        continue;

                    // Verify up/down buttons are active (spinner is interactable)
                    var upBtn = h.UpButton.GetValue(mb) as Button;
                    var downBtn = h.DownButton.GetValue(mb) as Button;
                    if (upBtn == null || downBtn == null)
                        continue;
                    if (!upBtn.gameObject.activeInHierarchy && !downBtn.gameObject.activeInHierarchy)
                        continue;

                    result.Add(mb);
                }
                catch (Exception ex)
                {
                    Log.Warn("SpinnerNavigator", $"Error checking spinner: {ex.Message}");
                }
            }
            return result;
        }

        private void Enter(List<MonoBehaviour> spinners)
        {
            _spinners.Clear();
            _spinners.AddRange(spinners);
            SortSpinnersByPosition();
            _currentIndex = 0;
            _isActive = true;
            _hasFocus = true;
            _hasAnnounced = false;
            _totalMax = ReadGroupMaxValue();

            // Clear EventSystem selection to prevent background buttons from receiving key events
            ClearEventSystemSelection();

            // Deactivate card info navigator so Up/Down controls the spinner, not card blocks
            AccessibleArenaMod.Instance?.CardNavigator?.Deactivate();

            Log.Msg("SpinnerNavigator", $"Entered spinner mode ({_spinners.Count} spinners, max={_totalMax})");
        }

        private void Exit()
        {
            _isActive = false;
            _hasFocus = false;
            _hasAnnounced = false;
            _spinners.Clear();
            _currentIndex = 0;
            _totalMax = 0;
            Log.Msg("SpinnerNavigator", "Exited spinner mode");
        }

        private void RefreshSpinners(List<MonoBehaviour> activeSpinners)
        {
            if (activeSpinners.Count != _spinners.Count)
            {
                _spinners.Clear();
                _spinners.AddRange(activeSpinners);
                SortSpinnersByPosition();
                if (_currentIndex >= _spinners.Count)
                    _currentIndex = _spinners.Count - 1;
            }
        }

        private void SortSpinnersByPosition()
        {
            _spinners.Sort((a, b) =>
            {
                float xA = a.transform.position.x;
                float xB = b.transform.position.x;
                return xA.CompareTo(xB);
            });
        }

        private void Navigate(int direction)
        {
            int newIndex = _currentIndex + direction;
            if (newIndex < 0 || newIndex >= _spinners.Count)
                return;

            _currentIndex = newIndex;
            AnnounceCurrentSpinner();
        }

        private void AdjustValue(int direction)
        {
            if (_currentIndex < 0 || _currentIndex >= _spinners.Count)
                return;

            var spinner = _spinners[_currentIndex];
            var h = _spinnerCache.Handles;

            try
            {
                var buttonField = direction > 0 ? h.UpButton : h.DownButton;
                var button = buttonField.GetValue(spinner) as Button;

                if (button == null || !button.interactable)
                {
                    string limitMsg = direction > 0 ? Strings.SpinnerAtMax : Strings.SpinnerAtMin;
                    _announcer.AnnounceInterrupt(limitMsg);
                    return;
                }

                button.onClick.Invoke();

                // Read new value and announce
                int newValue = (int)h.Value.GetValue(spinner);
                int distributed = GetTotalDistributed();
                int remaining = _totalMax > 0 ? _totalMax - distributed : 0;
                string announcement = _totalMax > 0
                    ? Strings.SpinnerAdjusted(newValue, remaining)
                    : newValue.ToString();
                _announcer.AnnounceInterrupt(announcement);
            }
            catch (Exception ex)
            {
                Log.Warn("SpinnerNavigator", $"Error adjusting value: {ex.Message}");
            }
        }

        private void Submit()
        {
            // Find and click PromptButton_Primary
            foreach (var selectable in UnityEngine.Object.FindObjectsOfType<Selectable>())
            {
                if (selectable == null || !selectable.gameObject.activeInHierarchy)
                    continue;

                if (!selectable.gameObject.name.Contains("PromptButton_Primary"))
                    continue;

                if (!selectable.interactable)
                {
                    _announcer.AnnounceInterrupt(Strings.SpinnerCannotSubmit);
                    return;
                }

                int distributed = GetTotalDistributed();
                UIActivator.SimulatePointerClick(selectable.gameObject);
                _announcer.AnnounceInterrupt(Strings.SpinnerConfirmed(distributed));
                Log.Msg("SpinnerNavigator", $"Submitted: {distributed}");
                return;
            }

            _announcer.AnnounceInterrupt(Strings.SpinnerCannotSubmit);
        }

        private void Cancel()
        {
            // Find and click PromptButton_Secondary (undo/cancel)
            foreach (var selectable in UnityEngine.Object.FindObjectsOfType<Selectable>())
            {
                if (selectable == null || !selectable.gameObject.activeInHierarchy || !selectable.interactable)
                    continue;

                if (selectable.gameObject.name.Contains("PromptButton_Secondary"))
                {
                    UIActivator.SimulatePointerClick(selectable.gameObject);
                    Log.Msg("SpinnerNavigator", "Cancelled via secondary button");
                    return;
                }
            }

            // Fallback: try UndoButton
            var undoButton = GameObject.Find("UndoButton");
            if (undoButton != null && undoButton.activeInHierarchy)
            {
                var button = undoButton.GetComponent<Button>();
                if (button != null && button.interactable)
                {
                    button.onClick.Invoke();
                    Log.Msg("SpinnerNavigator", "Cancelled via UndoButton");
                    return;
                }
            }
        }

        private void AnnounceEntry()
        {
            if (_spinners.Count == 0) return;

            var spinner = _spinners[_currentIndex];
            string cardName = GetSpinnerCardName(spinner);
            int value = (int)_spinnerCache.Handles.Value.GetValue(spinner);

            _announcer.AnnounceInterrupt(Strings.SpinnerEntry(_totalMax, cardName, value));
        }

        private void AnnounceCurrentSpinner()
        {
            if (_currentIndex < 0 || _currentIndex >= _spinners.Count) return;

            var spinner = _spinners[_currentIndex];
            string cardName = GetSpinnerCardName(spinner);
            int value = (int)_spinnerCache.Handles.Value.GetValue(spinner);

            _announcer.AnnounceInterrupt(Strings.SpinnerCard(cardName, value, _currentIndex + 1, _spinners.Count));
        }

        private string GetSpinnerCardName(MonoBehaviour spinner)
        {
            try
            {
                uint instanceId = (uint)_spinnerCache.Handles.InstanceId.GetValue(spinner);
                string name = CardStateProvider.ResolveInstanceIdToNameWithPT(instanceId);
                return name ?? $"Card #{instanceId}";
            }
            catch
            {
                return "Unknown card";
            }
        }

        private int GetTotalDistributed()
        {
            int total = 0;
            var valueProp = _spinnerCache.Handles.Value;
            foreach (var spinner in _spinners)
            {
                try
                {
                    total += (int)valueProp.GetValue(spinner);
                }
                catch { }
            }
            return total;
        }

        private int ReadGroupMaxValue()
        {
            if (_spinners.Count == 0)
                return 0;

            try
            {
                var groupField = _spinnerCache.Handles.Group;

                // Try reading _group field from the spinner directly (set in Awake)
                if (groupField != null)
                {
                    var group = groupField.GetValue(_spinners[0]);
                    if (group != null && _spinnerGroupCache.EnsureInitialized(group.GetType()))
                    {
                        int maxVal = (int)_spinnerGroupCache.Handles.MaxValue.GetValue(group);
                        Log.Msg("SpinnerNavigator", $"Read group max from _group field: {maxVal}");
                        return maxVal;
                    }
                }

                // Fallback: GetComponentInParent
                if (_spinnerGroupType != null)
                {
                    var group = _spinners[0].GetComponentInParent(_spinnerGroupType);
                    if (group != null && _spinnerGroupCache.EnsureInitialized(group.GetType()))
                    {
                        int maxVal = (int)_spinnerGroupCache.Handles.MaxValue.GetValue(group);
                        Log.Msg("SpinnerNavigator", $"Read group max from GetComponentInParent: {maxVal}");
                        return maxVal;
                    }
                    else if (group == null)
                    {
                        Log.Warn("SpinnerNavigator", "SpinnerGroup not found via GetComponentInParent");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("SpinnerNavigator", $"Error reading group max: {ex.Message}");
            }

            return 0;
        }

        private void ClearEventSystemSelection() => ZoneNavigator.ClearFocus("SpinnerNavigator");

        private static void InitializeReflection()
        {
            _reflectionInitialized = true;

            _spinnerAnimatedType = FindType(T.SpinnerAnimated);
            if (_spinnerAnimatedType == null)
            {
                Log.Warn("SpinnerNavigator", "SpinnerAnimated type not found");
                _reflectionFailed = true;
                return;
            }

            _spinnerGroupType = FindType(T.SpinnerGroup);
            if (_spinnerGroupType == null)
                Log.Warn("SpinnerNavigator", "SpinnerGroup type not found");

            if (!_spinnerCache.EnsureInitialized(_spinnerAnimatedType))
                _reflectionFailed = true;

            if (_spinnerGroupType != null)
                _spinnerGroupCache.EnsureInitialized(_spinnerGroupType);
        }
    }
}
