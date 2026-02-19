using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;

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
        private static Type _selectorType;
        private static PropertyInfo _isOpenProp;
        private static FieldInfo _selectionProviderField;
        private static MethodInfo _selectColorMethod;
        private static MethodInfo _tryCloseSelectorMethod;
        private static Type _manaColorEnum;
        private static bool _reflectionInitialized;
        private static bool _reflectionFailed;

        // IManaSelectorProvider reflection cache
        private static PropertyInfo _validSelectionsCountProp;
        private static MethodInfo _getElementAtMethod;
        private static PropertyInfo _maxSelectionsProp;
        private static PropertyInfo _allSelectionsCompleteProp;
        private static PropertyInfo _currentSelectionProp;
        private static Type _providerType;
        private static bool _providerReflectionInitialized;

        // ManaProducedData reflection cache
        private static FieldInfo _primaryColorField;
        private static PropertyInfo _primaryColorProp;
        private static bool _primaryColorIsField; // true=field, false=property

        // State
        private bool _isActive;
        private bool _hasAnnounced;
        private UnityEngine.Object _selectorInstance;
        private object _selectionProvider;
        private List<(int index, int manaColorValue, string displayName)> _availableColors;
        private int _currentSelection;
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
                    bool isOpen = (bool)_isOpenProp.GetValue(sel, null);
                    if (isOpen)
                    {
                        activeSelector = sel;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[ManaColorPicker] Error checking IsOpen: {ex.Message}");
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

            // Number keys 1-6 for color selection
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
                _selectionProvider = _selectionProviderField.GetValue(selector);
                if (_selectionProvider == null)
                {
                    MelonLogger.Warning("[ManaColorPicker] _selectionProvider is null");
                    _isActive = false;
                    return;
                }

                if (!_providerReflectionInitialized)
                    InitializeProviderReflection(_selectionProvider.GetType());

                ReadAvailableColors();
                ReadSelectionState();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManaColorPicker] Error entering: {ex.Message}");
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
            _currentSelection = 0;
            _maxSelections = 0;
        }

        private void ReadAvailableColors()
        {
            _availableColors.Clear();

            try
            {
                int count = (int)_validSelectionsCountProp.GetValue(_selectionProvider, null);

                for (int i = 0; i < count; i++)
                {
                    object element = _getElementAtMethod.Invoke(_selectionProvider, new object[] { i });
                    if (element == null) continue;

                    // First time: discover whether PrimaryColor is a field or property
                    if (_primaryColorField == null && _primaryColorProp == null)
                    {
                        _primaryColorField = element.GetType().GetField("PrimaryColor");
                        if (_primaryColorField != null)
                        {
                            _primaryColorIsField = true;
                        }
                        else
                        {
                            _primaryColorProp = element.GetType().GetProperty("PrimaryColor");
                            if (_primaryColorProp == null)
                            {
                                MelonLogger.Warning("[ManaColorPicker] Cannot find PrimaryColor on element");
                                continue;
                            }
                            _primaryColorIsField = false;
                        }
                    }

                    object colorValue = _primaryColorIsField
                        ? _primaryColorField.GetValue(element)
                        : _primaryColorProp.GetValue(element, null);
                    int colorInt = Convert.ToInt32(colorValue);
                    string colorName = GetColorDisplayName(colorInt);
                    _availableColors.Add((i, colorInt, colorName));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManaColorPicker] Error reading colors: {ex.Message}");
            }
        }

        private void ReadSelectionState()
        {
            try
            {
                _maxSelections = (int)_maxSelectionsProp.GetValue(_selectionProvider, null);
                _currentSelection = (int)_currentSelectionProp.GetValue(_selectionProvider, null);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManaColorPicker] Error reading selection state: {ex.Message}");
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

        private void SelectColor(int listIndex)
        {
            var (providerIndex, manaColorValue, displayName) = _availableColors[listIndex];

            try
            {
                // Get the enum value to pass to SelectColor
                object manaColorEnumValue = Enum.ToObject(_manaColorEnum, manaColorValue);

                _selectColorMethod.Invoke(_selectorInstance, new object[] { manaColorEnumValue });

                // Check if more selections needed
                bool allComplete = (bool)_allSelectionsCompleteProp.GetValue(_selectionProvider, null);

                if (allComplete)
                {
                    _announcer.AnnounceInterrupt(Strings.ManaColorPickerDoneFormat(displayName));
                }
                else
                {
                    // Re-read state for next pick
                    ReadSelectionState();
                    ReadAvailableColors();
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
                MelonLogger.Warning($"[ManaColorPicker] Error selecting color: {ex.Message}");
            }
        }

        private void TryCancel()
        {
            try
            {
                _tryCloseSelectorMethod.Invoke(_selectorInstance, null);
                _announcer.AnnounceInterrupt(Strings.ManaColorPickerCancelled);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManaColorPicker] Error cancelling: {ex.Message}");
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

            try
            {
                // Find ManaColorSelector type from loaded assemblies
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (_selectorType != null)
                        break;

                    try
                    {
                        _selectorType = asm.GetType("ManaColorSelector");
                        if (_selectorType == null)
                        {
                            // Try with namespace variations
                            foreach (var type in asm.GetTypes())
                            {
                                if (type.Name == "ManaColorSelector")
                                {
                                    _selectorType = type;
                                    break;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Some assemblies may throw on GetTypes()
                    }
                }

                if (_selectorType == null)
                {
                    MelonLogger.Warning("[ManaColorPicker] ManaColorSelector type not found");
                    _reflectionFailed = true;
                    return;
                }

                MelonLogger.Msg($"[ManaColorPicker] Found ManaColorSelector: {_selectorType.FullName}");

                // IsOpen property (public)
                _isOpenProp = _selectorType.GetProperty("IsOpen", BindingFlags.Public | BindingFlags.Instance);
                if (_isOpenProp == null)
                {
                    MelonLogger.Warning("[ManaColorPicker] IsOpen property not found");
                    _reflectionFailed = true;
                    return;
                }

                // _selectionProvider field (protected)
                _selectionProviderField = _selectorType.GetField("_selectionProvider",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (_selectionProviderField == null)
                {
                    // Try base class
                    _selectionProviderField = _selectorType.BaseType?.GetField("_selectionProvider",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (_selectionProviderField == null)
                {
                    MelonLogger.Warning("[ManaColorPicker] _selectionProvider field not found");
                    _reflectionFailed = true;
                    return;
                }

                // SelectColor method (protected)
                _selectColorMethod = _selectorType.GetMethod("SelectColor",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (_selectColorMethod == null)
                {
                    // Try base class
                    _selectColorMethod = _selectorType.BaseType?.GetMethod("SelectColor",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (_selectColorMethod == null)
                {
                    MelonLogger.Warning("[ManaColorPicker] SelectColor method not found");
                    _reflectionFailed = true;
                    return;
                }

                // TryCloseSelector method (public)
                _tryCloseSelectorMethod = _selectorType.GetMethod("TryCloseSelector",
                    BindingFlags.Public | BindingFlags.Instance);
                if (_tryCloseSelectorMethod == null)
                {
                    MelonLogger.Warning("[ManaColorPicker] TryCloseSelector method not found");
                    _reflectionFailed = true;
                    return;
                }

                // ManaColor enum type (from SelectColor parameter)
                var parameters = _selectColorMethod.GetParameters();
                if (parameters.Length > 0)
                {
                    _manaColorEnum = parameters[0].ParameterType;
                    MelonLogger.Msg($"[ManaColorPicker] ManaColor enum: {_manaColorEnum.FullName}");
                }
                else
                {
                    MelonLogger.Warning("[ManaColorPicker] SelectColor has no parameters");
                    _reflectionFailed = true;
                    return;
                }

                MelonLogger.Msg("[ManaColorPicker] Reflection initialized successfully");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManaColorPicker] Reflection init failed: {ex.Message}");
                _reflectionFailed = true;
            }
        }

        private static void InitializeProviderReflection(Type providerInstanceType)
        {
            _providerReflectionInitialized = true;

            try
            {
                // Find the IManaSelectorProvider interface
                _providerType = providerInstanceType;

                // Try interface members first, then direct type
                // ValidSelectionCount property
                _validSelectionsCountProp = FindProperty(providerInstanceType, "ValidSelectionCount");
                if (_validSelectionsCountProp == null)
                {
                    // Try ValidSelections.Count pattern
                    _validSelectionsCountProp = FindProperty(providerInstanceType, "ValidSelectionsCount");
                }

                // GetElementAt method
                _getElementAtMethod = FindMethod(providerInstanceType, "GetElementAt");

                // MaxSelections property
                _maxSelectionsProp = FindProperty(providerInstanceType, "MaxSelections");

                // AllSelectionsComplete property
                _allSelectionsCompleteProp = FindProperty(providerInstanceType, "AllSelectionsComplete");

                // CurrentSelection property
                _currentSelectionProp = FindProperty(providerInstanceType, "CurrentSelection");

                if (_validSelectionsCountProp == null)
                    MelonLogger.Warning("[ManaColorPicker] ValidSelectionCount not found");
                if (_getElementAtMethod == null)
                    MelonLogger.Warning("[ManaColorPicker] GetElementAt not found");
                if (_maxSelectionsProp == null)
                    MelonLogger.Warning("[ManaColorPicker] MaxSelections not found");
                if (_allSelectionsCompleteProp == null)
                    MelonLogger.Warning("[ManaColorPicker] AllSelectionsComplete not found");
                if (_currentSelectionProp == null)
                    MelonLogger.Warning("[ManaColorPicker] CurrentSelection not found");

                MelonLogger.Msg($"[ManaColorPicker] Provider reflection initialized for {providerInstanceType.FullName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManaColorPicker] Provider reflection init failed: {ex.Message}");
            }
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            // Search type itself, then all interfaces
            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null) return prop;

            foreach (var iface in type.GetInterfaces())
            {
                prop = iface.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    // Get the implementation via interface map
                    var map = type.GetInterfaceMap(iface);
                    for (int i = 0; i < map.InterfaceMethods.Length; i++)
                    {
                        if (map.InterfaceMethods[i].Name == "get_" + name)
                        {
                            return prop;
                        }
                    }
                    return prop;
                }
            }

            return null;
        }

        private static MethodInfo FindMethod(Type type, string name)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
            if (method != null) return method;

            foreach (var iface in type.GetInterfaces())
            {
                method = iface.GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
                if (method != null) return method;
            }

            return null;
        }
    }
}
