using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using AccessibleArena.Core.Utils;

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

        // Cached AutoResponseManager instance
        private object _autoRespManager;

        // Cached ButtonPhaseLadder instance
        private MonoBehaviour _phaseLadder;
        private int _phaseLadderSearchFrame = -1;

        private sealed class AutoRespHandles
        {
            public MethodInfo ToggleFullControl;
            public MethodInfo ToggleLockedFullControl;
            public PropertyInfo FullControlEnabled;
            public PropertyInfo FullControlLocked;

            public MethodInfo SetAutoPassOption;
            public PropertyInfo AutoPassEnabled;
            public Type AutoPassOptionType;
            public object OptionUnlessOpponentAction;
            public object OptionTurn;
            public object OptionResolveMyStackEffects;
        }

        private sealed class PhaseLadderHandles
        {
            public FieldInfo PhaseIcons;
            public MethodInfo ToggleTransientStop;
        }

        private sealed class PhaseLadderButtonHandles
        {
            public FieldInfo PlayerStopTypes;
            public PropertyInfo StopState;
        }

        private static readonly ReflectionCache<AutoRespHandles> _autoRespCache = new ReflectionCache<AutoRespHandles>(
            builder: t =>
            {
                var h = new AutoRespHandles
                {
                    ToggleFullControl = t.GetMethod("ToggleFullControl", PublicInstance),
                    ToggleLockedFullControl = t.GetMethod("ToggleLockedFullControl", PublicInstance),
                    FullControlEnabled = t.GetProperty("FullControlEnabled", PublicInstance),
                    FullControlLocked = t.GetProperty("FullControlLocked", PublicInstance),
                    SetAutoPassOption = t.GetMethod("SetAutoPassOption", PublicInstance),
                    AutoPassEnabled = t.GetProperty("AutoPassEnabled", PublicInstance),
                };

                var parameters = h.SetAutoPassOption?.GetParameters();
                if (parameters != null && parameters.Length >= 1)
                {
                    h.AutoPassOptionType = parameters[0].ParameterType;
                    h.OptionUnlessOpponentAction = Enum.ToObject(h.AutoPassOptionType, 5);
                    h.OptionTurn = Enum.ToObject(h.AutoPassOptionType, 1);
                    h.OptionResolveMyStackEffects = Enum.ToObject(h.AutoPassOptionType, 6);
                }

                return h;
            },
            validator: h => h.ToggleFullControl != null && h.SetAutoPassOption != null && h.AutoPassOptionType != null,
            logTag: "PriorityController",
            logSubject: "AutoRespManager");

        private static readonly ReflectionCache<PhaseLadderHandles> _phaseLadderCache = new ReflectionCache<PhaseLadderHandles>(
            builder: t => new PhaseLadderHandles
            {
                PhaseIcons = t.GetField("PhaseIcons", PublicInstance),
                ToggleTransientStop = t.GetMethod("ToggleTransientStop", PublicInstance),
            },
            validator: h => h.PhaseIcons != null && h.ToggleTransientStop != null,
            logTag: "PriorityController",
            logSubject: "ButtonPhaseLadder");

        private static readonly ReflectionCache<PhaseLadderButtonHandles> _phaseLadderButtonCache = new ReflectionCache<PhaseLadderButtonHandles>(
            builder: t => new PhaseLadderButtonHandles
            {
                PlayerStopTypes = ReflectionWalk.FindField(t, "_playerStopTypes", AllInstanceFlags | BindingFlags.DeclaredOnly),
                StopState = t.GetProperty("StopState", PublicInstance),
            },
            validator: h => h.PlayerStopTypes != null && h.StopState != null,
            logTag: "PriorityController",
            logSubject: "PhaseLadderButton");

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
                    Log.Msg("PriorityController", "Found GameManager");
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

            var prop = gm.GetType().GetProperty("AutoRespManager", PublicInstance);
            if (prop == null)
            {
                Log.Warn("PriorityController", "AutoRespManager property not found on GameManager");
                return null;
            }

            _autoRespManager = prop.GetValue(gm);
            if (_autoRespManager == null)
            {
                Log.Warn("PriorityController", "AutoRespManager is null");
                return null;
            }

            _autoRespCache.EnsureInitialized(_autoRespManager.GetType());
            return _autoRespManager;
        }

        /// <summary>
        /// Toggle temporary full control (resets on phase change).
        /// Returns the new state, or null if failed.
        /// </summary>
        public bool? ToggleFullControl()
        {
            var arm = GetAutoRespManager();
            if (arm == null || _autoRespCache.Handles.ToggleFullControl == null)
            {
                Log.Warn("PriorityController", "Cannot toggle full control - AutoRespManager not available");
                return null;
            }

            try
            {
                _autoRespCache.Handles.ToggleFullControl.Invoke(arm, null);
                return IsFullControlEnabled();
            }
            catch (Exception ex)
            {
                Log.Warn("PriorityController", $"ToggleFullControl failed: {ex.Message}");
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
            if (arm == null || _autoRespCache.Handles.ToggleLockedFullControl == null)
            {
                Log.Warn("PriorityController", "Cannot toggle locked full control - AutoRespManager not available");
                return null;
            }

            try
            {
                _autoRespCache.Handles.ToggleLockedFullControl.Invoke(arm, null);
                return IsFullControlLocked();
            }
            catch (Exception ex)
            {
                Log.Warn("PriorityController", $"ToggleLockedFullControl failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if temporary full control is currently enabled.
        /// </summary>
        public bool IsFullControlEnabled()
        {
            var arm = GetAutoRespManager();
            if (arm == null || _autoRespCache.Handles.FullControlEnabled == null) return false;

            try
            {
                return (bool)_autoRespCache.Handles.FullControlEnabled.GetValue(arm);
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
            if (arm == null || _autoRespCache.Handles.FullControlLocked == null) return false;

            try
            {
                return (bool)_autoRespCache.Handles.FullControlLocked.GetValue(arm);
            }
            catch
            {
                return false;
            }
        }

        private bool EnsureAutoPassCached()
        {
            var arm = GetAutoRespManager();
            return arm != null && _autoRespCache.EnsureInitialized(arm.GetType());
        }

        /// <summary>
        /// Check if any auto-pass mode is currently active.
        /// </summary>
        public bool IsAutoPassActive()
        {
            var arm = GetAutoRespManager();
            if (arm == null || _autoRespCache.Handles.AutoPassEnabled == null) return false;

            try { return (bool)_autoRespCache.Handles.AutoPassEnabled.GetValue(arm); }
            catch { return false; }
        }

        /// <summary>
        /// Toggle "pass until opponent action" mode (originally Enter key).
        /// Returns the new state (true = now passing, false = cancelled), or null if failed.
        /// </summary>
        public bool? TogglePassUntilResponse()
        {
            if (!EnsureAutoPassCached()) return null;
            var arm = GetAutoRespManager();
            if (arm == null) return null;

            try
            {
                bool wasEnabled = IsAutoPassActive();
                var option = wasEnabled ? _autoRespCache.Handles.OptionResolveMyStackEffects : _autoRespCache.Handles.OptionUnlessOpponentAction;
                _autoRespCache.Handles.SetAutoPassOption.Invoke(arm, new object[] { option, _autoRespCache.Handles.OptionResolveMyStackEffects });
                Log.Msg("PriorityController", $"TogglePassUntilResponse: {(wasEnabled ? "cancelled" : "activated")}");
                return !wasEnabled;
            }
            catch (Exception ex)
            {
                Log.Warn("PriorityController", $"TogglePassUntilResponse failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Toggle "skip entire turn" mode (originally Shift+Enter key).
        /// Returns the new state (true = now skipping, false = cancelled), or null if failed.
        /// </summary>
        public bool? ToggleSkipTurn()
        {
            if (!EnsureAutoPassCached()) return null;
            var arm = GetAutoRespManager();
            if (arm == null) return null;

            try
            {
                bool wasEnabled = IsAutoPassActive();
                var option = wasEnabled ? _autoRespCache.Handles.OptionResolveMyStackEffects : _autoRespCache.Handles.OptionTurn;
                _autoRespCache.Handles.SetAutoPassOption.Invoke(arm, new object[] { option, _autoRespCache.Handles.OptionResolveMyStackEffects });
                Log.Msg("PriorityController", $"ToggleSkipTurn: {(wasEnabled ? "cancelled" : "activated")}");
                return !wasEnabled;
            }
            catch (Exception ex)
            {
                Log.Warn("PriorityController", $"ToggleSkipTurn failed: {ex.Message}");
                return null;
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
                    _phaseLadderCache.EnsureInitialized(mb.GetType());
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
            var lh = _phaseLadderCache.Handles;
            if (ladder == null || lh?.PhaseIcons == null) return;

            var icons = lh.PhaseIcons.GetValue(ladder) as IList;
            if (icons == null || icons.Count == 0) return;

            Log.Msg("PriorityController", $"Building phase stop map from {icons.Count} phase icons");

            // Build a lookup from StopType name to button (prefer non-avatar buttons)
            var stopTypeToButton = new Dictionary<string, object>();

            foreach (var button in icons)
            {
                if (button == null) continue;

                var btnType = button.GetType();

                // Skip AvatarPhaseIcon buttons - they're for player-specific stops
                if (btnType.Name == "AvatarPhaseIcon") continue;

                _phaseLadderButtonCache.EnsureInitialized(btnType);
                var bh = _phaseLadderButtonCache.Handles;
                if (bh?.PlayerStopTypes == null) continue;

                var stopTypes = bh.PlayerStopTypes.GetValue(button) as IList;
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
            Log.Msg("PriorityController", $"Phase stop map: {populatedCount}/{_phaseStopMap.Count} keys mapped");
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
                Log.Warn("PriorityController", $"No phase button found for index {keyIndex}");
                return null;
            }

            if (_phaseLadderCache.Handles.ToggleTransientStop == null || _phaseLadder == null)
            {
                Log.Warn("PriorityController", $"Cannot toggle phase stop - ladder not available");
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
                    _phaseLadderCache.Handles.ToggleTransientStop.Invoke(_phaseLadder, new object[] { button });

                    bool stateAfter = IsPhaseStopSet(button);
                    Log.Msg("PriorityController", $"Toggled phase stop index {keyIndex}: {stateBefore} -> {stateAfter}");

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
                Log.Warn("PriorityController", $"TogglePhaseStop failed for index {keyIndex}: {ex.Message}");
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
                if (_phaseLadderButtonCache.Handles.StopState != null)
                {
                    var stopState = _phaseLadderButtonCache.Handles.StopState.GetValue(button);
                    // SettingStatus.Set means the stop is active
                    return stopState?.ToString() == "Set";
                }
            }
            catch (Exception ex)
            {
                Log.Warn("PriorityController", $"IsPhaseStopSet failed: {ex.Message}");
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
            _phaseLadder = null;
            _phaseLadderSearchFrame = -1;
            _phaseStopMap = null;
            Log.Msg("PriorityController", "Cache cleared");
        }
    }
}
