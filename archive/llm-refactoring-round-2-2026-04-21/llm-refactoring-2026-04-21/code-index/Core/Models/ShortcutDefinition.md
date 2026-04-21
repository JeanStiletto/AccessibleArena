# ShortcutDefinition.cs
Path: src/Core/Models/ShortcutDefinition.cs
Lines: 36

## class ShortcutDefinition (line 6)
### Properties
- public KeyCode Key { get; set; } (line 8)
- public KeyCode? Modifier { get; set; } (line 9)
- public Action Action { get; set; } (line 10)
- public string Description { get; set; } (line 11)
### Methods
- public ShortcutDefinition(KeyCode key, Action action, string description, KeyCode? modifier = null) (line 12)
- public string GetKeyString() (line 20) — Note: maps LeftShift/RightShift→"Shift", LeftControl/RightControl→"Ctrl", LeftAlt/RightAlt→"Alt"; returns "Modifier+Key" or just "Key"
