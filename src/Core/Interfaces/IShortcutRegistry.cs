using System;
using System.Collections.Generic;
using UnityEngine;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Core.Interfaces
{
    public interface IShortcutRegistry
    {
        void RegisterShortcut(KeyCode key, Action action, string description);
        void RegisterShortcut(KeyCode key, KeyCode modifier, Action action, string description);
        void UnregisterShortcut(KeyCode key, KeyCode? modifier = null);

        /// <summary>Removes all shortcuts. Used to re-register after a keybind change.</summary>
        void Clear();

        bool ProcessKey(KeyCode key, bool shift, bool ctrl, bool alt);

        IEnumerable<ShortcutDefinition> GetAllShortcuts();

        /// <summary>Distinct base keys of all registered shortcuts — the set the
        /// input manager polls each frame.</summary>
        IReadOnlyCollection<KeyCode> MonitoredKeys { get; }
    }
}
