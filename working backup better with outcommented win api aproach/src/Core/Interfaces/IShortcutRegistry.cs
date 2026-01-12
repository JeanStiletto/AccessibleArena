using System;
using System.Collections.Generic;
using UnityEngine;
using MTGAAccessibility.Core.Models;

namespace MTGAAccessibility.Core.Interfaces
{
    public interface IShortcutRegistry
    {
        void RegisterShortcut(KeyCode key, Action action, string description, GameContext? context = null);
        void RegisterShortcut(KeyCode key, KeyCode modifier, Action action, string description, GameContext? context = null);
        void UnregisterShortcut(KeyCode key, KeyCode? modifier = null);

        bool ProcessKey(KeyCode key, bool shift, bool ctrl, bool alt);

        IEnumerable<ShortcutDefinition> GetShortcutsForContext(GameContext context);
        IEnumerable<ShortcutDefinition> GetGlobalShortcuts();
        IEnumerable<ShortcutDefinition> GetAllShortcuts();
    }
}
