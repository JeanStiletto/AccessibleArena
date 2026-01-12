using System;
using UnityEngine;

namespace MTGAAccessibility.Core.Models
{
    public class ShortcutDefinition
    {
        public KeyCode Key { get; set; }
        public KeyCode? Modifier { get; set; }
        public Action Action { get; set; }
        public string Description { get; set; }
        public GameContext? Context { get; set; }

        public ShortcutDefinition(KeyCode key, Action action, string description, GameContext? context = null, KeyCode? modifier = null)
        {
            Key = key;
            Modifier = modifier;
            Action = action;
            Description = description;
            Context = context;
        }

        public string GetKeyString()
        {
            if (Modifier.HasValue)
            {
                string modName = Modifier.Value switch
                {
                    KeyCode.LeftShift or KeyCode.RightShift => "Shift",
                    KeyCode.LeftControl or KeyCode.RightControl => "Ctrl",
                    KeyCode.LeftAlt or KeyCode.RightAlt => "Alt",
                    _ => Modifier.Value.ToString()
                };
                return $"{modName}+{Key}";
            }
            return Key.ToString();
        }
    }
}
