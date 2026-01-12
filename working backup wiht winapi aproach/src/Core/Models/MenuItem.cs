using System;
using MTGAAccessibility.Core.Interfaces;

namespace MTGAAccessibility.Core.Models
{
    public class MenuItem : INavigable
    {
        public string Name { get; }
        public string Description { get; }
        public bool IsEnabled { get; set; }
        public Action OnSelect { get; }

        public MenuItem(string name, Action onSelect, string description = null, bool isEnabled = true)
        {
            Name = name;
            OnSelect = onSelect;
            Description = description;
            IsEnabled = isEnabled;
        }

        public string GetAnnouncementText()
        {
            var text = Name;

            if (!IsEnabled)
                text += ", disabled";

            if (!string.IsNullOrEmpty(Description))
                text += $". {Description}";

            return text;
        }

        public void Execute()
        {
            if (IsEnabled)
                OnSelect?.Invoke();
        }
    }
}
