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

        bool ProcessKey(KeyCode key, bool shift, bool ctrl, bool alt);

        IEnumerable<ShortcutDefinition> GetAllShortcuts();
    }
}
