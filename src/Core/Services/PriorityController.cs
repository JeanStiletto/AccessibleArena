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
        private FieldInfo _phaseIconsField;

        // Cached reflection for PhaseLadderButton base type
        private FieldInfo _playerStopTypesField;
        private PropertyInfo _stopStateProp;

        // Cached ToggleTransientStop on ButtonPhaseLadder (bypasses AllowStop guard)
        private MethodInfo _toggleTransientStop;

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

        // StopType enum values matching Wotc.Mtgo.Gre.External.Messaging.StopType
        private static readonly string[] StopTypeNames =
        {
            "UpkeepStep",              // 1
            "DrawStep",                // 2
            "PrecombatMainPhase",      // 3
            "BeginCombatStep",         // 4
            "DeclareAttackersStep",    // 5
            "DeclareBlockersStep",     // 6
            "FirstStrikeDamageStep",   // 7a (combined with CombatDamageStep)
            "EndCombatStep",           // 8
            "PostcombatMainPhase",     // 9
            "EndStep"                  // 0
        };

        // Additional StopType for key 7 (CombatDamageStep, paired with FirstStrikeDamageStep)
        private const string CombatDamageStopType = "CombatDamageStep";

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
                    var type = mb.GetType();

                    // PhaseIcons is a public field (List<PhaseLadderButton>) on ButtonPhaseLadder
                    _phaseIconsField = type.GetField("PhaseIcons", BindingFlags.Public | BindingFlags.Instance);

                    // ToggleTransientStop(PhaseLadderButton) is public on ButtonPhaseLadder
                    _toggleTransientStop = type.GetMethod("ToggleTransientStop", BindingFlags.Public | BindingFlags.Instance);

                    MelonLogger.Msg($"[PriorityController] Found ButtonPhaseLadder " +
                        $"(PhaseIcons={_phaseIconsField != null}, ToggleTransientStop={_toggleTransientStop != null})");
                    return _phaseLadder;
                }
            }
            return null;
        }

        /// <summary>
        /// Get a private FieldInfo by walking up the type hierarchy.
        /// Required because GetField with NonPublic doesn't search base classes.
        /// </summary>
        private static FieldInfo GetFieldInHierarchy(Type type, string name)
        {
            while (type != null)
            {
                var field = type.GetField(name,
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null) return field;
                type = type.BaseType;
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
            if (ladder == null || _phaseIconsField == null) return;

            var icons = _phaseIconsField.GetValue(ladder) as IList;
            if (icons == null || icons.Count == 0) return;

            MelonLogger.Msg($"[PriorityController] Building phase stop map from {icons.Count} phase icons");

            // Build a lookup from StopType name to button (prefer non-avatar buttons)
            var stopTypeToButton = new Dictionary<string, object>();

            foreach (var button in icons)
            {
                if (button == null) continue;

                var btnType = button.GetType();

                // Skip AvatarPhaseIcon buttons - they're for player-specific stops
                if (btnType.Name == "AvatarPhaseIcon") continue;

                // Cache reflection for PhaseLadderButton base type (once)
                if (_playerStopTypesField == null)
                {
                    // _playerStopTypes is private on base class PhaseLadderButton
                    _playerStopTypesField = GetFieldInHierarchy(btnType, "_playerStopTypes");
                    // StopState is public on PhaseLadderButton
                    _stopStateProp = btnType.GetProperty("StopState", BindingFlags.Public | BindingFlags.Instance);

                    MelonLogger.Msg($"[PriorityController] Cached button reflection: " +
                        $"_playerStopTypes={_playerStopTypesField != null}, " +
                        $"StopState={_stopStateProp != null}");
                }

                if (_playerStopTypesField == null) continue;

                var stopTypes = _playerStopTypesField.GetValue(button) as IList;
                if (stopTypes == null) continue;

                foreach (var st in stopTypes)
                {
                    string stName = st.ToString();
                    stopTypeToButton[stName] = button;
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

                // Key 7 (index 6) also maps to CombatDamageStep
                if (i == 6 && stopTypeToButton.TryGetValue(CombatDamageStopType, out var combatBtn))
                {
                    if (!buttons.Contains(combatBtn))
                    {
                        buttons.Add(combatBtn);
                    }
                }

                _phaseStopMap[i] = buttons;
            }

            int populatedCount = 0;
            foreach (var kv in _phaseStopMap)
            {
                if (kv.Value.Count > 0) populatedCount++;
            }
            MelonLogger.Msg($"[PriorityController] Phase stop map: {populatedCount}/{_phaseStopMap.Count} keys mapped");
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

            if (_toggleTransientStop == null || _phaseLadder == null)
            {
                MelonLogger.Warning($"[PriorityController] Cannot toggle phase stop - ladder not available");
                return null;
            }

            try
            {
                bool? resultState = null;

                foreach (var button in buttons)
                {
                    bool stateBefore = IsPhaseStopSet(button);

                    // Call ButtonPhaseLadder.ToggleTransientStop(button) directly
                    // This bypasses AllowStop guard on PhaseLadderButton.ToggleStop()
                    _toggleTransientStop.Invoke(_phaseLadder, new object[] { button });

                    bool stateAfter = IsPhaseStopSet(button);
                    MelonLogger.Msg($"[PriorityController] Toggled phase stop index {keyIndex}: {stateBefore} -> {stateAfter}");

                    if (resultState == null)
                    {
                        resultState = stateAfter;
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
        /// Check if a phase stop button is currently set.
        /// Uses StopState property (SettingStatus enum) on PhaseLadderButton.
        /// </summary>
        private bool IsPhaseStopSet(object button)
        {
            try
            {
                if (_stopStateProp != null)
                {
                    var stopState = _stopStateProp.GetValue(button);
                    // SettingStatus.Set means the stop is active
                    return stopState?.ToString() == "Set";
                }
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
            _phaseIconsField = null;
            _toggleTransientStop = null;
            _playerStopTypesField = null;
            _stopStateProp = null;
            _phaseStopMap = null;
            MelonLogger.Msg("[PriorityController] Cache cleared");
        }
    }
}
