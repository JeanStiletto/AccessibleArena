using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Detects and navigates the ManaColorSelector popup that appears when
    /// a mana source produces "any color" (e.g. Ilysian Caryatid).
    /// Polled from DuelNavigator at highest priority (before browser detection).
    /// Number keys 1-6 select colors, Backspace cancels.
    /// </summary>
    public class ManaColorPickerNavigator
    {
        private readonly IAnnouncementService _announcer;

        // Reflection cache
        private sealed class SelectorHandles
        {
            public PropertyInfo IsOpen;
            public FieldInfo SelectionProvider;
            public MethodInfo SelectColor;
            public MethodInfo TryCloseSelector;
        }

        private sealed class ProviderHandles
        {
            public PropertyInfo ValidSelectionCount;
            public MethodInfo GetElementAt;
            public PropertyInfo MaxSelections;
            public PropertyInfo AllSelectionsComplete;
            public PropertyInfo CurrentSelection;
        }

        private sealed class ElementHandles
        {
            public FieldInfo PrimaryColorField;
            public PropertyInfo PrimaryColorProp;
        }

        private static Type _selectorType;
        private static Type _manaColorEnum;
        private static bool _reflectionInitialized;
        private static bool _reflectionFailed;

        private static readonly ReflectionCache<SelectorHandles> _selectorCache = new ReflectionCache<SelectorHandles>(
            builder: t => new SelectorHandles
            {
                IsOpen = t.GetProperty("IsOpen", PublicInstance),
                SelectionProvider = ReflectionWalk.FindField(t, "_selectionProvider", PrivateInstance),
                SelectColor = ReflectionWalk.FindMethod(t, "SelectColor", PrivateInstance),
                TryCloseSelector = t.GetMethod("TryCloseSelector", PublicInstance),
            },
            validator: h => h.IsOpen != null && h.SelectionProvider != null
                         && h.SelectColor != null && h.TryCloseSelector != null,
            logTag: "ManaColorPicker",
            logSubject: "ManaColorSelector");

        private static readonly ReflectionCache<ProviderHandles> _providerCache = new ReflectionCache<ProviderHandles>(
            builder: t => new ProviderHandles
            {
                ValidSelectionCount = FindPropertyOnTypeOrInterfaces(t, "ValidSelectionCount")
                                   ?? FindPropertyOnTypeOrInterfaces(t, "ValidSelectionsCount"),
                GetElementAt = FindMethodOnTypeOrInterfaces(t, "GetElementAt"),
                MaxSelections = FindPropertyOnTypeOrInterfaces(t, "MaxSelections"),
                AllSelectionsComplete = FindPropertyOnTypeOrInterfaces(t, "AllSelectionsComplete"),
                CurrentSelection = FindPropertyOnTypeOrInterfaces(t, "CurrentSelection"),
            },
            validator: h => h.ValidSelectionCount != null && h.GetElementAt != null
                         && h.MaxSelections != null && h.AllSelectionsComplete != null
                         && h.CurrentSelection != null,
            logTag: "ManaColorPicker",
            logSubject: "IManaSelectorProvider");

        private static readonly ReflectionCache<ElementHandles> _elementCache = new ReflectionCache<ElementHandles>(
            builder: t =>
            {
                var h = new ElementHandles { PrimaryColorField = t.GetField("PrimaryColor") };
                if (h.PrimaryColorField == null)
                    h.PrimaryColorProp = t.GetProperty("PrimaryColor");
                return h;
            },
            validator: h => h.PrimaryColorField != null || h.PrimaryColorProp != null,
            logTag: "ManaColorPicker",
            logSubject: "ManaProducedData");

        // State
        private bool _isActive;
        private bool _hasAnnounced;
        private UnityEngine.Object _selectorInstance;
        private object _selectionProvider;
        private List<(int index, int manaColorValue, string displayName)> _availableColors;
        private int _cursorIndex;       // Tab navigation cursor position
        private int _currentSelection;  // Which pick we're on (for multi-pick)
        private int _maxSelections;

        // Polling interval (same as BrowserDetector)
        private float _lastScanTime;
        private const float ScanInterval = 0.1f;

        public bool IsActive => _isActive;

        public ManaColorPickerNavigator(IAnnouncementService announcer)
        {
            _announcer = announcer;
            _availableColors = new List<(int, int, string)>();
        }

        /// <summary>
        /// Called every frame from DuelNavigator. Polls for open ManaColorSelector.
        /// </summary>
        public void Update()
        {
            if (_reflectionFailed)
                return;

            if (!_reflectionInitialized)
                InitializeReflection();

            if (_reflectionFailed || _selectorType == null)
                return;

            float time = Time.time;
            if (time - _lastScanTime < ScanInterval)
                return;
            _lastScanTime = time;

            // Find active selector
            var selectors = UnityEngine.Object.FindObjectsOfType(_selectorType);
            UnityEngine.Object activeSelector = null;

            foreach (var sel in selectors)
            {
                try
                {
                    bool isOpen = (bool)_selectorCache.Handles.IsOpen.GetValue(sel, null);
                    if (isOpen)
                    {
                        activeSelector = sel;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("ManaColorPicker", $"Error checking IsOpen: {ex.Message}");
                }
            }

            if (activeSelector != null && !_isActive)
            {
                Enter(activeSelector);
            }
            else if (activeSelector == null && _isActive)
            {
                Exit();
            }
        }

        /// <summary>
        /// Handles keyboard input when active. Returns true if key was consumed.
        /// Tab/Right = next, Shift+Tab/Left = previous, Home/End = jump,
        /// Enter = select focused color, number keys 1-6 = direct select,
        /// Backspace = cancel.
        /// </summary>
        public bool HandleInput()
        {
            if (!_isActive)
                return false;

            // Announce on first frame after entering
            if (!_hasAnnounced)
            {
                AnnounceAvailableColors();
                _hasAnnounced = true;
            }

            if (_availableColors.Count == 0)
                return false;

            // Tab / Right = next option
            bool isTab = Input.GetKeyDown(KeyCode.Tab) && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift);
            if (isTab || Input.GetKeyDown(KeyCode.RightArrow))
            {
                _cursorIndex = (_cursorIndex + 1) % _availableColors.Count;
                AnnounceCurrent();
                return true;
            }

            // Shift+Tab / Left = previous option
            bool isShiftTab = Input.GetKeyDown(KeyCode.Tab) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            if (isShiftTab || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                _cursorIndex = (_cursorIndex - 1 + _availableColors.Count) % _availableColors.Count;
                AnnounceCurrent();
                return true;
            }

            // Home = first option
            if (Input.GetKeyDown(KeyCode.Home))
            {
                _cursorIndex = 0;
                AnnounceCurrent();
                return true;
            }

            // End = last option
            if (Input.GetKeyDown(KeyCode.End))
            {
                _cursorIndex = _availableColors.Count - 1;
                AnnounceCurrent();
                return true;
            }

            // Enter = select focused color
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                SelectColor(_cursorIndex);
                return true;
            }

            // Number keys 1-6 for direct color selection
            for (int i = 0; i < 6; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    if (i < _availableColors.Count)
                    {
                        SelectColor(i);
                    }
                    else
                    {
                        _announcer.AnnounceInterrupt(Strings.ManaColorPickerInvalidKey);
                    }
                    return true;
                }
            }

            // Backspace to cancel
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                TryCancel();
                return true;
            }

            return false;
        }

        private void Enter(UnityEngine.Object selector)
        {
            _selectorInstance = selector;
            _isActive = true;
            _hasAnnounced = false;

            // Get the selection provider
            try
            {
                _selectionProvider = _selectorCache.Handles.SelectionProvider.GetValue(selector);
                if (_selectionProvider == null)
                {
                    Log.Warn("ManaColorPicker", "_selectionProvider is null");
                    _isActive = false;
                    return;
                }

                if (!_providerCache.EnsureInitialized(_selectionProvider.GetType()))
                {
                    _isActive = false;
                    return;
                }

                ReadAvailableColors();
                ReadSelectionState();
            }
            catch (Exception ex)
            {
                Log.Warn("ManaColorPicker", $"Error entering: {ex.Message}");
                _isActive = false;
            }
        }

        private void Exit()
        {
            _isActive = false;
            _hasAnnounced = false;
            _selectorInstance = null;
            _selectionProvider = null;
            _availableColors.Clear();
            _cursorIndex = 0;
            _currentSelection = 0;
            _maxSelections = 0;
        }

        private void ReadAvailableColors()
        {
            _availableColors.Clear();

            try
            {
                var ph = _providerCache.Handles;
                int count = (int)ph.ValidSelectionCount.GetValue(_selectionProvider, null);

                for (int i = 0; i < count; i++)
                {
                    object element = ph.GetElementAt.Invoke(_selectionProvider, new object[] { i });
                    if (element == null) continue;

                    if (!_elementCache.EnsureInitialized(element.GetType()))
                        continue;

                    var eh = _elementCache.Handles;
                    object colorValue = eh.PrimaryColorField != null
                        ? eh.PrimaryColorField.GetValue(element)
                        : eh.PrimaryColorProp.GetValue(element, null);
                    int colorInt = Convert.ToInt32(colorValue);
                    string colorName = GetColorDisplayName(colorInt);
                    _availableColors.Add((i, colorInt, colorName));
                }
            }
            catch (Exception ex)
            {
                Log.Warn("ManaColorPicker", $"Error reading colors: {ex.Message}");
            }
        }

        private void ReadSelectionState()
        {
            try
            {
                var ph = _providerCache.Handles;
                _maxSelections = (int)ph.MaxSelections.GetValue(_selectionProvider, null);
                _currentSelection = (int)ph.CurrentSelection.GetValue(_selectionProvider, null);
            }
            catch (Exception ex)
            {
                Log.Warn("ManaColorPicker", $"Error reading selection state: {ex.Message}");
                _maxSelections = 1;
                _currentSelection = 0;
            }
        }

        private void AnnounceAvailableColors()
        {
            if (_availableColors.Count == 0)
            {
                _announcer.AnnounceInterrupt(Strings.ManaColorPickerFormat(""));
                return;
            }

            var options = new List<string>();
            for (int i = 0; i < _availableColors.Count; i++)
            {
                options.Add(Strings.ManaColorPickerOptionFormat((i + 1).ToString(), _availableColors[i].displayName));
            }

            string colorList = string.Join(", ", options);
            string announcement = Strings.ManaColorPickerFormat(colorList);

            if (_maxSelections > 1)
            {
                announcement += " (" + Strings.ManaColorPickerSelectionProgress(_currentSelection + 1, _maxSelections) + ")";
            }

            _announcer.AnnounceInterrupt(announcement);
        }

        private void AnnounceCurrent()
        {
            if (_cursorIndex < 0 || _cursorIndex >= _availableColors.Count)
                return;

            var color = _availableColors[_cursorIndex];
            string announcement = Strings.ManaColorPickerOptionFormat((_cursorIndex + 1).ToString(), color.displayName);
            _announcer.AnnounceInterrupt(announcement);
        }

        private void SelectColor(int listIndex)
        {
            var (providerIndex, manaColorValue, displayName) = _availableColors[listIndex];

            try
            {
                // Get the enum value to pass to SelectColor
                object manaColorEnumValue = Enum.ToObject(_manaColorEnum, manaColorValue);

                _selectorCache.Handles.SelectColor.Invoke(_selectorInstance, new object[] { manaColorEnumValue });

                // Check if more selections needed
                bool allComplete = (bool)_providerCache.Handles.AllSelectionsComplete.GetValue(_selectionProvider, null);

                if (allComplete)
                {
                    _announcer.AnnounceInterrupt(Strings.ManaColorPickerDoneFormat(displayName));
                }
                else
                {
                    // Re-read state for next pick
                    ReadSelectionState();
                    ReadAvailableColors();
                    _cursorIndex = 0;
                    string selectedMsg = Strings.ManaColorPickerSelectedFormat(
                        displayName,
                        (_currentSelection + 1).ToString(),
                        _maxSelections.ToString());
                    _announcer.AnnounceInterrupt(selectedMsg);

                    // Announce next set of options
                    _hasAnnounced = false;
                }
            }
            catch (Exception ex)
            {
                Log.Warn("ManaColorPicker", $"Error selecting color: {ex.Message}");
            }
        }

        private void TryCancel()
        {
            try
            {
                _selectorCache.Handles.TryCloseSelector.Invoke(_selectorInstance, null);
                _announcer.AnnounceInterrupt(Strings.ManaColorPickerCancelled);
            }
            catch (Exception ex)
            {
                Log.Warn("ManaColorPicker", $"Error cancelling: {ex.Message}");
            }
        }

        private static string GetColorDisplayName(int manaColorValue)
        {
            switch (manaColorValue)
            {
                case 1: return Strings.ManaWhite;
                case 2: return Strings.ManaBlue;
                case 3: return Strings.ManaBlack;
                case 4: return Strings.ManaRed;
                case 5: return Strings.ManaGreen;
                case 6: return Strings.ManaColorless;
                default: return $"Unknown({manaColorValue})";
            }
        }

        private static void InitializeReflection()
        {
            _reflectionInitialized = true;

            _selectorType = FindType("ManaColorSelector");
            if (_selectorType == null)
            {
                Log.Warn("ManaColorPicker", "ManaColorSelector type not found");
                _reflectionFailed = true;
                return;
            }

            if (!_selectorCache.EnsureInitialized(_selectorType))
            {
                _reflectionFailed = true;
                return;
            }

            // ManaColor enum type (from SelectColor parameter)
            var parameters = _selectorCache.Handles.SelectColor.GetParameters();
            if (parameters.Length == 0)
            {
                Log.Warn("ManaColorPicker", "SelectColor has no parameters");
                _reflectionFailed = true;
                return;
            }
            _manaColorEnum = parameters[0].ParameterType;
        }

        private static PropertyInfo FindPropertyOnTypeOrInterfaces(Type type, string name)
        {
            var prop = type.GetProperty(name, PublicInstance);
            if (prop != null) return prop;

            foreach (var iface in type.GetInterfaces())
            {
                prop = iface.GetProperty(name, PublicInstance);
                if (prop != null) return prop;
            }
            return null;
        }

        private static MethodInfo FindMethodOnTypeOrInterfaces(Type type, string name)
        {
            var method = type.GetMethod(name, PublicInstance);
            if (method != null) return method;

            foreach (var iface in type.GetInterfaces())
            {
                method = iface.GetMethod(name, PublicInstance);
                if (method != null) return method;
            }
            return null;
        }
    }
}
