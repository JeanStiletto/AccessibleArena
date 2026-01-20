using AccessibleArena.Core.Interfaces;

namespace AccessibleArena.Core.Models
{
    public class NavigableItem : INavigable
    {
        public string Name { get; }
        public string Description { get; }
        public bool IsEnabled { get; set; }

        public NavigableItem(string name, string description = null, bool isEnabled = true)
        {
            Name = name;
            Description = description;
            IsEnabled = isEnabled;
        }

        public virtual string GetAnnouncementText()
        {
            if (string.IsNullOrEmpty(Description))
                return Name;

            return $"{Name}. {Description}";
        }
    }
}
