# ShortcutRegistry.cs

## Overview
Registry for keyboard shortcuts with modifier key support.

## Class: ShortcutRegistry : IShortcutRegistry (line 10)

### State
- private readonly List<ShortcutDefinition> _shortcuts (line 12)

### Methods
- public void RegisterShortcut(KeyCode key, Action action, string description) (line 14)
- public void RegisterShortcut(KeyCode key, KeyCode modifier, Action action, string description) (line 19)
- public void UnregisterShortcut(KeyCode key, KeyCode? modifier = null) (line 24)
- public bool ProcessKey(KeyCode key, bool shift, bool ctrl, bool alt) (line 29)
  - Returns true if shortcut was found and executed
- private bool MatchesModifiers(ShortcutDefinition s, bool shift, bool ctrl, bool alt) (line 44)
- public IEnumerable<ShortcutDefinition> GetAllShortcuts() (line 58)
