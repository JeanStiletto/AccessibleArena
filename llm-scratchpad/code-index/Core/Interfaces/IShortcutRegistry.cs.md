# IShortcutRegistry.cs Code Index

## File Overview
Interface for registering and processing keyboard shortcuts.

## Interface: IShortcutRegistry (line 8)

### Methods
- void RegisterShortcut(KeyCode key, Action action, string description) (line 10)
  // Registers a shortcut with no modifier key

- void RegisterShortcut(KeyCode key, KeyCode modifier, Action action, string description) (line 11)
  // Registers a shortcut with a modifier key (Shift, Ctrl, Alt)

- void UnregisterShortcut(KeyCode key, KeyCode? modifier = null) (line 12)
  // Removes a shortcut

- bool ProcessKey(KeyCode key, bool shift, bool ctrl, bool alt) (line 14)
  // Processes a key press. Returns true if a shortcut was triggered.

- IEnumerable<ShortcutDefinition> GetAllShortcuts() (line 16)
  // Returns all registered shortcuts for display in help menu
