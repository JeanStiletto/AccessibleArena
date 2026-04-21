# IShortcutRegistry.cs
Path: src/Core/Interfaces/IShortcutRegistry.cs
Lines: 18

## interface IShortcutRegistry (line 8)
### Methods
- void RegisterShortcut(KeyCode key, Action action, string description) (line 10)
- void RegisterShortcut(KeyCode key, KeyCode modifier, Action action, string description) (line 11)
- void UnregisterShortcut(KeyCode key, KeyCode? modifier = null) (line 12)
- bool ProcessKey(KeyCode key, bool shift, bool ctrl, bool alt) (line 14)
- IEnumerable<ShortcutDefinition> GetAllShortcuts() (line 16)
