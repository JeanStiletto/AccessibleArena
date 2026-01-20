using System;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Core.Interfaces
{
    public interface IContextManager
    {
        GameContext CurrentGameContext { get; }
        INavigableContext ActiveContext { get; }

        void SetContext(GameContext context);
        void PushContext(INavigableContext context);
        void PopContext();

        void RegisterContext(GameContext gameContext, INavigableContext navigableContext);

        event Action<GameContext, GameContext> OnContextChanged;
    }
}
