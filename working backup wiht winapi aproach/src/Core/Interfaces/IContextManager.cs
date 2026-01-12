using System;
using MTGAAccessibility.Core.Models;

namespace MTGAAccessibility.Core.Interfaces
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
