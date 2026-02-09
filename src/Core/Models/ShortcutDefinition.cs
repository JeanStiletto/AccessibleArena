using System;
using UnityEngine;

namespace AccessibleArena.Core.Models
{
    public class ShortcutDefinition
    {
        public KeyCode Key { get; set; }
        public KeyCode? Modifier { get; set; }
        public Action Action { get; set; }
        public string Description { get; set; }
        public ShortcutDefinition(KeyCode key, Action action, string description, KeyCode? modifier = null)
        {
            Key = key;
            Modifier = modifier;
            Action = action;
            Description = description;
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
