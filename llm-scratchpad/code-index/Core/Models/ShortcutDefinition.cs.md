# ShortcutDefinition.cs Code Index

## File Overview
Model representing a keyboard shortcut with key, optional modifier, action, and description.

## Class: ShortcutDefinition (line 6)

### Public Properties
- public KeyCode Key { get; set; } (line 8)
- public KeyCode? Modifier { get; set; } (line 9)
- public Action Action { get; set; } (line 10)
- public string Description { get; set; } (line 11)

### Constructor
- public ShortcutDefinition(KeyCode key, Action action, string description, KeyCode? modifier = null) (line 12)

### Public Methods
- public string GetKeyString() (line 20)
  // Returns formatted key name like "Shift+F1" or "Ctrl+R"
