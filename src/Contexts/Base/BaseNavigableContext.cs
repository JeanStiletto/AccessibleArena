using System.Collections.Generic;
using MTGAAccessibility.Core.Interfaces;
using MTGAAccessibility.Core.Models;

namespace MTGAAccessibility.Contexts.Base
{
    public abstract class BaseNavigableContext : INavigableContext
    {
        protected readonly IAnnouncementService Announcer;
        protected readonly List<INavigable> Items = new List<INavigable>();
        protected int CurrentIdx = -1;

        public abstract string ContextName { get; }
        public virtual bool IsActive { get; protected set; }

        public INavigable CurrentItem =>
            CurrentIdx >= 0 && CurrentIdx < Items.Count
                ? Items[CurrentIdx]
                : null;

        public int CurrentIndex => CurrentIdx;
        public int ItemCount => Items.Count;

        public virtual INavigableContext ParentContext { get; set; }
        public virtual INavigableContext ActiveChildContext { get; protected set; }

        protected BaseNavigableContext(IAnnouncementService announcer)
        {
            Announcer = announcer;
        }

        public virtual bool MoveNext()
        {
            if (Items.Count == 0)
                return false;

            int newIndex = CurrentIdx + 1;
            if (newIndex >= Items.Count)
            {
                newIndex = 0;
            }

            return MoveTo(newIndex);
        }

        public virtual bool MovePrevious()
        {
            if (Items.Count == 0)
                return false;

            int newIndex = CurrentIdx - 1;
            if (newIndex < 0)
            {
                newIndex = Items.Count - 1;
            }

            return MoveTo(newIndex);
        }

        public virtual bool MoveToFirst()
        {
            if (Items.Count == 0)
                return false;

            return MoveTo(0);
        }

        public virtual bool MoveToLast()
        {
            if (Items.Count == 0)
                return false;

            return MoveTo(Items.Count - 1);
        }

        public virtual bool MoveTo(int index)
        {
            if (index < 0 || index >= Items.Count)
                return false;

            CurrentIdx = index;
            AnnounceCurrentItem();
            return true;
        }

        protected virtual void AnnounceCurrentItem()
        {
            if (CurrentItem != null)
            {
                string position = $"{CurrentIdx + 1} of {Items.Count}";
                string announcement = $"{position}: {CurrentItem.GetAnnouncementText()}";
                Announcer.Announce(announcement);
            }
        }

        public abstract void Accept();

        public virtual void Cancel()
        {
            if (ParentContext != null)
            {
                Announcer.Announce(Core.Models.Strings.Back);
            }
        }

        public virtual void OnEnter()
        {
            IsActive = true;
            Refresh();
            AnnounceContext();
        }

        public virtual void OnExit()
        {
            IsActive = false;
        }

        public abstract void Refresh();

        protected virtual void AnnounceContext()
        {
            string announcement = ContextName;
            if (Items.Count > 0)
            {
                announcement += $". {Items.Count} items.";
            }
            Announcer.Announce(announcement, AnnouncementPriority.High);
        }

        protected void ClearItems()
        {
            Items.Clear();
            CurrentIdx = -1;
        }

        protected void AddItem(INavigable item)
        {
            Items.Add(item);
            if (CurrentIdx < 0 && Items.Count > 0)
            {
                CurrentIdx = 0;
            }
        }
    }
}
