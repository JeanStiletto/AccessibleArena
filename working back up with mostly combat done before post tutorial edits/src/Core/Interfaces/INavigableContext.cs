namespace MTGAAccessibility.Core.Interfaces
{
    public interface INavigableContext
    {
        string ContextName { get; }
        bool IsActive { get; }

        INavigable CurrentItem { get; }
        int CurrentIndex { get; }
        int ItemCount { get; }

        bool MoveNext();
        bool MovePrevious();
        bool MoveToFirst();
        bool MoveToLast();
        bool MoveTo(int index);

        void Accept();
        void Cancel();

        void OnEnter();
        void OnExit();
        void Refresh();

        INavigableContext ParentContext { get; set; }
        INavigableContext ActiveChildContext { get; }
    }
}
