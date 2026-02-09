using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Core.Services
{
    public class ShortcutRegistry : IShortcutRegistry
    {
        private readonly List<ShortcutDefinition> _shortcuts = new List<ShortcutDefinition>();

        public void RegisterShortcut(KeyCode key, Action action, string description)
        {
            _shortcuts.Add(new ShortcutDefinition(key, action, description));
        }

        public void RegisterShortcut(KeyCode key, KeyCode modifier, Action action, string description)
        {
            _shortcuts.Add(new ShortcutDefinition(key, action, description, modifier));
        }

        public void UnregisterShortcut(KeyCode key, KeyCode? modifier = null)
        {
            _shortcuts.RemoveAll(s => s.Key == key && s.Modifier == modifier);
        }

        public bool ProcessKey(KeyCode key, bool shift, bool ctrl, bool alt)
        {
            var shortcut = _shortcuts
                .Where(s => s.Key == key && MatchesModifiers(s, shift, ctrl, alt))
                .FirstOrDefault();

            if (shortcut != null)
            {
                shortcut.Action?.Invoke();
                return true;
            }

            return false;
        }

        private bool MatchesModifiers(ShortcutDefinition s, bool shift, bool ctrl, bool alt)
        {
            if (!s.Modifier.HasValue)
                return !shift && !ctrl && !alt;

            return s.Modifier.Value switch
            {
                KeyCode.LeftShift or KeyCode.RightShift => shift && !ctrl && !alt,
                KeyCode.LeftControl or KeyCode.RightControl => ctrl && !shift && !alt,
                KeyCode.LeftAlt or KeyCode.RightAlt => alt && !shift && !ctrl,
                _ => false
            };
        }

        public IEnumerable<ShortcutDefinition> GetAllShortcuts()
        {
            return _shortcuts.AsReadOnly();
        }
    }
}
