using System.Collections.Generic;
using UnityEngine;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// The "Mod keybinds" submenu inside the F2 settings overlay. Three levels:
    /// categories (mirroring the F1 help structure) → binds of a category →
    /// key capture for one bind. Owned and driven by ModSettingsNavigator, which
    /// delegates input here while <see cref="IsActive"/>.
    ///
    /// Capture flow: Enter on a bind, then press the new key (optionally holding
    /// Ctrl or Shift). A key already in use warns first; pressing the same key
    /// again moves it here and unbinds the other action. Delete restores the
    /// default, Escape cancels. Every applied change saves immediately.
    /// </summary>
    public class KeybindMenuNavigator
    {
        private enum Level { Categories, Binds, Capture }

        private static readonly KeybindCategory[] Categories =
        {
            KeybindCategory.Global,
            KeybindCategory.DuelZones,
            KeybindCategory.Battlefield,
            KeybindCategory.DuelInfo
        };

        /// <summary>
        /// Keys polled during capture. Includes reserved keys so pressing one gives
        /// an explanation instead of silence. Escape and Delete are handled before
        /// this list (cancel / reset-to-default).
        /// </summary>
        private static readonly KeyCode[] CaptureProbeKeys = BuildCaptureProbeKeys();

        private static KeyCode[] BuildCaptureProbeKeys()
        {
            var keys = new List<KeyCode>();
            for (var k = KeyCode.A; k <= KeyCode.Z; k++) keys.Add(k);
            for (var k = KeyCode.F1; k <= KeyCode.F12; k++) keys.Add(k);
            for (var k = KeyCode.Alpha0; k <= KeyCode.Alpha9; k++) keys.Add(k);
            for (var k = KeyCode.Keypad0; k <= KeyCode.Keypad9; k++) keys.Add(k);
            keys.AddRange(new[]
            {
                KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
                KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Space, KeyCode.Tab,
                KeyCode.Backspace, KeyCode.Home, KeyCode.End,
                KeyCode.PageUp, KeyCode.PageDown, KeyCode.Insert
            });
            return keys.ToArray();
        }

        private readonly IAnnouncementService _announcer;

        private bool _isActive;
        private Level _level;
        private int _categoryIndex;
        private int _bindIndex;
        private List<KeybindAction> _binds = new List<KeybindAction>();
        private KeybindAction _captureAction;
        private KeyChord? _pendingConflictChord;

        public bool IsActive => _isActive;
        public bool IsCapturing => _isActive && _level == Level.Capture;

        public KeybindMenuNavigator(IAnnouncementService announcer)
        {
            _announcer = announcer;
        }

        // Categories plus the trailing "restore all defaults" action
        private int CategoryItemCount => Categories.Length + 1;

        public void Open()
        {
            _isActive = true;
            _level = Level.Categories;
            _categoryIndex = 0;
            _pendingConflictChord = null;

            Log.Msg("KeybindMenuNavigator", "Opened");
            string core = $"{Strings.KeybindMenuTitle}. {Strings.ItemCount(CategoryItemCount)}";
            _announcer.AnnounceInterrupt(Strings.WithHint(core, "KeybindMenuInstructions"));
        }

        public void Close()
        {
            if (!_isActive) return;
            _isActive = false;
            _pendingConflictChord = null;
            Log.Msg("KeybindMenuNavigator", "Closed");
        }

        /// <summary>Handles one frame of input. Always consumes while active.</summary>
        public bool HandleInput()
        {
            if (!_isActive) return false;

            switch (_level)
            {
                case Level.Capture:
                    HandleCaptureInput();
                    return true;
                case Level.Binds:
                    HandleBindsInput();
                    return true;
                default:
                    HandleCategoriesInput();
                    return true;
            }
        }

        #region Categories level

        private void HandleCategoriesInput()
        {
            if (KeyInput.GetKeyDown(KeyCode.Backspace) || KeyInput.GetKeyDown(KeyCode.Escape))
            {
                // Back to the settings list; ModSettingsNavigator announces its item
                Close();
                return;
            }

            if (KeyInput.GetKeyDown(KeyCode.Return) || KeyInput.GetKeyDown(KeyCode.KeypadEnter) || KeyInput.GetKeyDown(KeyCode.Space))
            {
                ActivateCategoryItem();
                return;
            }

            if (Navigate(ref _categoryIndex, CategoryItemCount))
                AnnounceCategoryItem();
        }

        private void ActivateCategoryItem()
        {
            if (_categoryIndex >= Categories.Length)
            {
                Keybinds.Map.ResetAll();
                Keybinds.SaveAndNotify();
                Log.Msg("KeybindMenuNavigator", "All keybinds reset to defaults");
                _announcer.AnnounceInterrupt(Strings.KeybindResetAllDone);
                return;
            }

            _binds = KeybindMap.ActionsInCategory(Categories[_categoryIndex]);
            _bindIndex = 0;
            _level = Level.Binds;

            string core = $"{Strings.KeybindCategoryName(Categories[_categoryIndex])}. {Strings.ItemCount(_binds.Count)}";
            _announcer.AnnounceInterrupt(Strings.WithHint(core, "KeybindListInstructions"));
        }

        private void AnnounceCategoryItem()
        {
            string label = _categoryIndex >= Categories.Length
                ? Strings.KeybindResetAllItem
                : Strings.KeybindCategoryName(Categories[_categoryIndex]);
            _announcer.AnnounceInterrupt(Strings.ItemPositionOf(_categoryIndex + 1, CategoryItemCount, label));
        }

        #endregion

        #region Binds level

        private void HandleBindsInput()
        {
            if (KeyInput.GetKeyDown(KeyCode.Backspace) || KeyInput.GetKeyDown(KeyCode.Escape))
            {
                _level = Level.Categories;
                AnnounceCategoryItem();
                return;
            }

            if (KeyInput.GetKeyDown(KeyCode.Return) || KeyInput.GetKeyDown(KeyCode.KeypadEnter))
            {
                StartCapture();
                return;
            }

            // Quick reset without entering capture. Runs through the same assign
            // flow, so a default whose key another action has taken warns first.
            if (KeyInput.GetKeyDown(KeyCode.Delete))
            {
                var action = _binds[_bindIndex];
                _captureAction = action;
                _level = Level.Capture;
                _pendingConflictChord = null;
                TryAssign(KeybindMap.GetDefinition(action).Default, isReset: true);
                return;
            }

            if (Navigate(ref _bindIndex, _binds.Count))
                AnnounceBindItem();
        }

        private void AnnounceBindItem()
        {
            var action = _binds[_bindIndex];
            _announcer.AnnounceInterrupt(
                Strings.ItemPositionOf(_bindIndex + 1, _binds.Count, DescribeBind(action)));
        }

        private string DescribeBind(KeybindAction action)
        {
            var chord = Keybinds.GetChord(action);
            string label = Strings.KeybindItem(Strings.KeybindActionName(action), Strings.KeyChordLabel(chord));

            var def = KeybindMap.GetDefinition(action);
            if (def.HasShiftVariant && chord.IsBound)
            {
                string variantKey = Strings.KeyChordLabel(chord.WithShift());
                label += ". " + (action == KeybindAction.FullControl
                    ? Strings.KeybindVariantLock(variantKey)
                    : Strings.KeybindVariantOpponent(variantKey));
            }

            if (Keybinds.Map.IsCustomized(action))
                label += ". " + Strings.KeybindCustomizedTag;

            return label;
        }

        #endregion

        #region Capture level

        private void StartCapture()
        {
            _captureAction = _binds[_bindIndex];
            _level = Level.Capture;
            _pendingConflictChord = null;

            _announcer.AnnounceInterrupt(Strings.KeybindCapturePrompt(
                Strings.KeybindActionName(_captureAction),
                Strings.KeyChordLabel(Keybinds.GetChord(_captureAction))));
        }

        private void HandleCaptureInput()
        {
            if (KeyInput.GetKeyDown(KeyCode.Escape))
            {
                EndCapture();
                _announcer.AnnounceInterrupt(Strings.KeybindCaptureCancelled);
                return;
            }

            if (KeyInput.GetKeyDown(KeyCode.Delete))
            {
                TryAssign(KeybindMap.GetDefinition(_captureAction).Default, isReset: true);
                return;
            }

            foreach (var key in CaptureProbeKeys)
            {
                if (!KeyInput.GetKeyDown(key))
                    continue;

                if (KeyInput.GetKey(KeyCode.LeftAlt) || KeyInput.GetKey(KeyCode.RightAlt))
                {
                    _announcer.AnnounceInterrupt(Strings.KeybindAltNotAllowed);
                    return;
                }

                bool ctrl = KeyInput.GetKey(KeyCode.LeftControl) || KeyInput.GetKey(KeyCode.RightControl);
                bool shift = KeyInput.GetKey(KeyCode.LeftShift) || KeyInput.GetKey(KeyCode.RightShift);
                TryAssign(new KeyChord(key, ctrl, shift), isReset: false);
                return;
            }
        }

        private void EndCapture()
        {
            _level = Level.Binds;
            _pendingConflictChord = null;
        }

        private void TryAssign(KeyChord chord, bool isReset)
        {
            var result = Keybinds.Map.Validate(_captureAction, chord, out var conflict);
            string name = Strings.KeybindActionName(_captureAction);

            switch (result)
            {
                case KeybindValidation.SameAsCurrent:
                    EndCapture();
                    _announcer.AnnounceInterrupt(Strings.KeybindSame(name, Strings.KeyChordLabel(chord)));
                    return;

                case KeybindValidation.ReservedNavigation:
                    _pendingConflictChord = null;
                    _announcer.AnnounceInterrupt(Strings.KeybindReservedNavigation(Strings.KeyName(chord.Key)));
                    return;

                case KeybindValidation.ReservedGameKey:
                    _pendingConflictChord = null;
                    _announcer.AnnounceInterrupt(Strings.KeybindReservedGameKey(Strings.KeyName(chord.Key)));
                    return;

                case KeybindValidation.GlobalNeedsCtrlOrFunctionKey:
                    _pendingConflictChord = null;
                    _announcer.AnnounceInterrupt(Strings.KeybindGlobalNeedsCtrl);
                    return;

                case KeybindValidation.TwoModifiers:
                    _pendingConflictChord = null;
                    _announcer.AnnounceInterrupt(Strings.KeybindTwoModifiers);
                    return;

                case KeybindValidation.ShiftReservedForVariant:
                    _pendingConflictChord = null;
                    _announcer.AnnounceInterrupt(Strings.KeybindShiftReserved);
                    return;

                case KeybindValidation.Conflict:
                    if (_pendingConflictChord.HasValue && _pendingConflictChord.Value.Equals(chord))
                    {
                        ApplyChord(chord, isReset);
                        return;
                    }
                    _pendingConflictChord = chord;
                    _announcer.AnnounceInterrupt(Strings.KeybindConflict(
                        Strings.KeyChordLabel(chord), Strings.KeybindActionName(conflict)));
                    return;

                default:
                    ApplyChord(chord, isReset);
                    return;
            }
        }

        private void ApplyChord(KeyChord chord, bool isReset)
        {
            var stolen = Keybinds.Map.Apply(_captureAction, chord);
            Keybinds.SaveAndNotify();
            EndCapture();

            string name = Strings.KeybindActionName(_captureAction);
            string keyLabel = Strings.KeyChordLabel(chord);
            string message = isReset
                ? Strings.KeybindResetDone(name, keyLabel)
                : Strings.KeybindApplied(name, keyLabel);

            var def = KeybindMap.GetDefinition(_captureAction);
            if (def.HasShiftVariant)
            {
                string variantKey = Strings.KeyChordLabel(chord.WithShift());
                message += ". " + (_captureAction == KeybindAction.FullControl
                    ? Strings.KeybindVariantLock(variantKey)
                    : Strings.KeybindVariantOpponent(variantKey));
            }

            if (stolen.HasValue)
                message += ". " + Strings.KeybindUnboundOther(Strings.KeybindActionName(stolen.Value));

            Log.Msg("KeybindMenuNavigator",
                $"{_captureAction} bound to '{chord.ToSerial()}'{(stolen.HasValue ? $", {stolen.Value} unbound" : "")}");
            _announcer.AnnounceInterrupt(message);
        }

        #endregion

        /// <summary>Shared Up/Down/Home/End list navigation. Returns true when the
        /// index changed; announces list edges itself.</summary>
        private bool Navigate(ref int index, int count)
        {
            if (KeyInput.GetKeyDown(KeyCode.UpArrow))
            {
                if (index <= 0)
                {
                    _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                    return false;
                }
                index--;
                return true;
            }

            if (KeyInput.GetKeyDown(KeyCode.DownArrow))
            {
                if (index >= count - 1)
                {
                    _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                    return false;
                }
                index++;
                return true;
            }

            if (KeyInput.GetKeyDown(KeyCode.Home))
            {
                if (index == 0)
                {
                    _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                    return false;
                }
                index = 0;
                return true;
            }

            if (KeyInput.GetKeyDown(KeyCode.End))
            {
                if (index == count - 1)
                {
                    _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                    return false;
                }
                index = count - 1;
                return true;
            }

            return false;
        }
    }
}
