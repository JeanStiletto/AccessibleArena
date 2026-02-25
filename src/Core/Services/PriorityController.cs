using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using MelonLoader;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Reflection wrapper for GameManager.AutoRespManager and ButtonPhaseLadder.
    /// Provides full control toggle and phase stop toggle functionality.
    /// </summary>
    public class PriorityController
    {
        // Cached GameManager instance
        private MonoBehaviour _gameManager;
        private int _gameManagerSearchFrame = -1;

        // Cached AutoResponseManager
        private object _autoRespManager;
        private MethodInfo _toggleFullControl;
        private MethodInfo _toggleLockedFullControl;
        private PropertyInfo _fullControlEnabled;
        private PropertyInfo _fullControlLocked;

        // Cached ButtonPhaseLadder
        private MonoBehaviour _phaseLadder;
        private int _phaseLadderSearchFrame = -1;
        private PropertyInfo _phaseIcons;

        // Phase stop button cache: maps our key index (0-9) to PhaseLadderButton(s)
        private Dictionary<int, List<object>> _phaseStopMap;

        // Phase name cache for announcements
        private static readonly string[] PhaseNames =
        {
            "Upkeep",           // 1 (index 0)
            "Draw",             // 2 (index 1)
            "First main",       // 3 (index 2)
            "Begin combat",     // 4 (index 3)
            "Declare attackers",// 5 (index 4)
            "Declare blockers", // 6 (index 5)
            "Combat damage",    // 7 (index 6)
            "End combat",       // 8 (index 7)
            "Second main",      // 9 (index 8)
            "End step"          // 0 (index 9)
        };

        // Locale keys for phase names
        private static readonly string[] PhaseLocaleKeys =
        {
            "PhaseStop_Upkeep",
            "PhaseStop_Draw",
            "PhaseStop_FirstMain",
            "PhaseStop_BeginCombat",
            "PhaseStop_DeclareAttackers",
            "PhaseStop_DeclareBlockers",
            "PhaseStop_CombatDamage",
            "PhaseStop_EndCombat",
            "PhaseStop_SecondMain",
            "PhaseStop_EndStep"
        };

        // StopType enum values we care about, in order matching our key indices
        // These are the string names from the StopType enum
        private static readonly string[] StopTypeNames =
        {
            "Upkeep",              // 1
            "Draw",                // 2
            "PrecombatMainPhase",  // 3
            "BeginCombat",         // 4
            "DeclareAttackers",    // 5
            "DeclareBlockers",     // 6
            "FirstStrikeDamage",   // 7a (combined with CombatDamage)
            "EndCombat",           // 8
            "PostcombatMainPhase", // 9
            "End"                  // 0
        };

        // Additional StopType for key 7 (CombatDamage, paired with FirstStrikeDamage)
        private const string CombatDamageStopType = "CombatDamage";

        private MonoBehaviour FindGameManager()
        {
            if (_gameManager != null) return _gameManager;

            // Throttle search to once per frame
            int frame = Time.frameCount;
            if (frame == _gameManagerSearchFrame) return null;
            _gameManagerSearchFrame = frame;

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "GameManager")
                {
                    _gameManager = mb;
                    MelonLogger.Msg("[PriorityController] Found GameManager");
                    return _gameManager;
                }
            }
            return null;
        }

        private object GetAutoRespManager()
        {
            if (_autoRespManager != null) return _autoRespManager;

            var gm = FindGameManager();
            if (gm == null) return null;

            var type = gm.GetType();
            var prop = type.GetProperty("AutoRespManager", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
            {
                MelonLogger.Warning("[PriorityController] AutoRespManager property not found on GameManager");
                return null;
            }

            _autoRespManager = prop.GetValue(gm);
            if (_autoRespManager == null)
            {
                MelonLogger.Warning("[PriorityController] AutoRespManager is null");
                return null;
            }

            // Cache methods and properties
            var armType = _autoRespManager.GetType();
            _toggleFullControl = armType.GetMethod("ToggleFullControl", BindingFlags.Public | BindingFlags.Instance);
            _toggleLockedFullControl = armType.GetMethod("ToggleLockedFullControl", BindingFlags.Public | BindingFlags.Instance);
            _fullControlEnabled = armType.GetProperty("FullControlEnabled", BindingFlags.Public | BindingFlags.Instance);
            _fullControlLocked = armType.GetProperty("FullControlLocked", BindingFlags.Public | BindingFlags.Instance);

            MelonLogger.Msg($"[PriorityController] Cached AutoRespManager " +
                $"(ToggleFC={_toggleFullControl != null}, ToggleLocked={_toggleLockedFullControl != null}, " +
                $"FCEnabled={_fullControlEnabled != null}, FCLocked={_fullControlLocked != null})");

            return _autoRespManager;
        }

        /// <summary>
        /// Toggle temporary full control (resets on phase change).
        /// Returns the new state, or null if failed.
        /// </summary>
        public bool? ToggleFullControl()
        {
            var arm = GetAutoRespManager();
            if (arm == null || _toggleFullControl == null)
            {
                MelonLogger.Warning("[PriorityController] Cannot toggle full control - AutoRespManager not available");
                return null;
            }

            try
            {
                _toggleFullControl.Invoke(arm, null);
                return IsFullControlEnabled();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PriorityController] ToggleFullControl failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Toggle locked full control (permanent until toggled off).
        /// Returns the new state, or null if failed.
        /// </summary>
        public bool? ToggleLockFullControl()
        {
            var arm = GetAutoRespManager();
            if (arm == null || _toggleLockedFullControl == null)
            {
                MelonLogger.Warning("[PriorityController] Cannot toggle locked full control - AutoRespManager not available");
                return null;
            }

            try
            {
                _toggleLockedFullControl.Invoke(arm, null);
                return IsFullControlLocked();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PriorityController] ToggleLockedFullControl failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if temporary full control is currently enabled.
        /// </summary>
        public bool IsFullControlEnabled()
        {
            var arm = GetAutoRespManager();
            if (arm == null || _fullControlEnabled == null) return false;

            try
            {
                return (bool)_fullControlEnabled.GetValue(arm);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if locked (permanent) full control is currently enabled.
        /// </summary>
        public bool IsFullControlLocked()
        {
            var arm = GetAutoRespManager();
            if (arm == null || _fullControlLocked == null) return false;

            try
            {
                return (bool)_fullControlLocked.GetValue(arm);
            }
            catch
            {
                return false;
            }
        }

        private MonoBehaviour FindPhaseLadder()
        {
            if (_phaseLadder != null) return _phaseLadder;

            int frame = Time.frameCount;
            if (frame == _phaseLadderSearchFrame) return null;
            _phaseLadderSearchFrame = frame;

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "ButtonPhaseLadder")
                {
                    _phaseLadder = mb;
                    _phaseIcons = mb.GetType().GetProperty("PhaseIcons", BindingFlags.Public | BindingFlags.Instance);
                    MelonLogger.Msg($"[PriorityController] Found ButtonPhaseLadder (PhaseIcons={_phaseIcons != null})");
                    return _phaseLadder;
                }
            }
            return null;
        }

        /// <summary>
        /// Build the mapping from key index (0-9) to PhaseLadderButton objects.
        /// Key 7 maps to two buttons (FirstStrikeDamage + CombatDamage).
        /// </summary>
        private void BuildPhaseStopMap()
        {
            _phaseStopMap = new Dictionary<int, List<object>>();

            var ladder = FindPhaseLadder();
            if (ladder == null || _phaseIcons == null) return;

            var icons = _phaseIcons.GetValue(ladder);
            if (icons == null) return;

            var iconList = icons as IList;
            if (iconList == null) return;

            MelonLogger.Msg($"[PriorityController] Building phase stop map from {iconList.Count} phase icons");

            // Build a lookup from StopType name to button
            var stopTypeToButton = new Dictionary<string, object>();

            foreach (var button in iconList)
            {
                if (button == null) continue;

                var btnType = button.GetType();

                // Read _playerStopTypes field (serialized list of StopType enums)
                var stopTypesField = btnType.GetField("_playerStopTypes",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

                if (stopTypesField == null)
                {
                    // Try public field
                    stopTypesField = btnType.GetField("PlayerStopTypes",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                if (stopTypesField != null)
                {
                    var stopTypes = stopTypesField.GetValue(button) as IList;
                    if (stopTypes != null)
                    {
                        foreach (var st in stopTypes)
                        {
                            string stName = st.ToString();
                            stopTypeToButton[stName] = button;
                            MelonLogger.Msg($"[PriorityController]   StopType '{stName}' -> button");
                        }
                    }
                }
            }

            // Map key indices to buttons
            for (int i = 0; i < StopTypeNames.Length; i++)
            {
                var buttons = new List<object>();

                if (stopTypeToButton.TryGetValue(StopTypeNames[i], out var btn))
                {
                    buttons.Add(btn);
                }

                // Key 7 (index 6) also maps to CombatDamage
                if (i == 6 && stopTypeToButton.TryGetValue(CombatDamageStopType, out var combatBtn))
                {
                    if (!buttons.Contains(combatBtn))
                    {
                        buttons.Add(combatBtn);
                    }
                }

                _phaseStopMap[i] = buttons;
            }

            MelonLogger.Msg($"[PriorityController] Phase stop map built with {_phaseStopMap.Count} entries");
        }

        /// <summary>
        /// Toggle a phase stop by key index (0-9).
        /// Returns (phaseName, isNowSet) or null if failed.
        /// </summary>
        public (string phaseName, bool isSet)? TogglePhaseStop(int keyIndex)
        {
            if (keyIndex < 0 || keyIndex >= PhaseNames.Length)
                return null;

            if (_phaseStopMap == null)
                BuildPhaseStopMap();

            if (_phaseStopMap == null || !_phaseStopMap.TryGetValue(keyIndex, out var buttons) || buttons.Count == 0)
            {
                MelonLogger.Warning($"[PriorityController] No phase button found for index {keyIndex}");
                return null;
            }

            try
            {
                bool? resultState = null;

                foreach (var button in buttons)
                {
                    var btnType = button.GetType();
                    var toggleMethod = btnType.GetMethod("ToggleStop", BindingFlags.Public | BindingFlags.Instance);

                    if (toggleMethod != null)
                    {
                        toggleMethod.Invoke(button, null);

                        // Read the current stop state after toggle
                        if (resultState == null)
                        {
                            resultState = IsPhaseStopSet(button);
                        }
                    }
                    else
                    {
                        MelonLogger.Warning($"[PriorityController] ToggleStop method not found on {btnType.Name}");
                    }
                }

                string phaseName = LocaleManager.Instance?.Get(PhaseLocaleKeys[keyIndex]) ?? PhaseNames[keyIndex];
                return (phaseName, resultState ?? false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PriorityController] TogglePhaseStop failed for index {keyIndex}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if a phase stop button is currently set by reading its visual/state.
        /// </summary>
        private bool IsPhaseStopSet(object button)
        {
            try
            {
                var btnType = button.GetType();

                // Try IsStopSet property
                var isSetProp = btnType.GetProperty("IsStopSet", BindingFlags.Public | BindingFlags.Instance);
                if (isSetProp != null)
                {
                    return (bool)isSetProp.GetValue(button);
                }

                // Try _isStopSet field
                var isSetField = btnType.GetField("_isStopSet",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (isSetField != null)
                {
                    return (bool)isSetField.GetValue(button);
                }

                // Try checking the stop state via the manager
                // If ToggleStop toggled it, assume it's now the opposite
                MelonLogger.Msg("[PriorityController] Could not read stop state directly");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PriorityController] IsPhaseStopSet failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Get the localized phase name for a key index.
        /// </summary>
        public string GetPhaseName(int keyIndex)
        {
            if (keyIndex < 0 || keyIndex >= PhaseLocaleKeys.Length)
                return "Unknown";

            return LocaleManager.Instance?.Get(PhaseLocaleKeys[keyIndex]) ?? PhaseNames[keyIndex];
        }

        /// <summary>
        /// Clear all cached references. Call on scene change.
        /// </summary>
        public void ClearCache()
        {
            _gameManager = null;
            _gameManagerSearchFrame = -1;
            _autoRespManager = null;
            _toggleFullControl = null;
            _toggleLockedFullControl = null;
            _fullControlEnabled = null;
            _fullControlLocked = null;
            _phaseLadder = null;
            _phaseLadderSearchFrame = -1;
            _phaseIcons = null;
            _phaseStopMap = null;
            MelonLogger.Msg("[PriorityController] Cache cleared");
        }
    }
}
