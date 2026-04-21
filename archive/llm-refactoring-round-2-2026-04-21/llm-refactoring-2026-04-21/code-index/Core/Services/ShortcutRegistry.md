# ShortcutRegistry.cs
Path: src/Core/Services/ShortcutRegistry.cs
Lines: 63

## public class ShortcutRegistry : IShortcutRegistry (line 10)
### Fields
- private readonly List<ShortcutDefinition> _shortcuts (line 12)

### Methods
- public void RegisterShortcut(KeyCode key, Action action, string description) (line 14)
- public void RegisterShortcut(KeyCode key, KeyCode modifier, Action action, string description) (line 19)
- public void UnregisterShortcut(KeyCode key, KeyCode? modifier = null) (line 24)
- public bool ProcessKey(KeyCode key, bool shift, bool ctrl, bool alt) (line 29)
- private bool MatchesModifiers(ShortcutDefinition s, bool shift, bool ctrl, bool alt) (line 44)
- public IEnumerable<ShortcutDefinition> GetAllShortcuts() (line 58)
