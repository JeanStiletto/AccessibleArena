using System;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Contexts.Base
{
    public abstract class BaseMenuContext : BaseNavigableContext
    {
        protected BaseMenuContext(IAnnouncementService announcer) : base(announcer)
        {
        }

        public override void Accept()
        {
            if (CurrentItem is MenuItem menuItem && menuItem.IsEnabled)
            {
                Announcer.Announce(Strings.Activating(menuItem.Name));
                menuItem.Execute();
            }
            else if (CurrentItem != null && !CurrentItem.IsEnabled)
            {
                Announcer.Announce(Strings.ItemDisabled);
            }
        }

        protected void AddMenuItem(string name, Action onSelect, string description = null, bool isEnabled = true)
        {
            AddItem(new MenuItem(name, onSelect, description, isEnabled));
        }

        protected override void AnnounceContext()
        {
            string announcement = $"{ContextName} menu";
            if (Items.Count > 0)
            {
                announcement += $". {Items.Count} options.";
            }
            Announcer.Announce(announcement, AnnouncementPriority.High);
        }
    }
}
