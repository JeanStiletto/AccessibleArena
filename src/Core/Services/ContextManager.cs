using System;
using System.Collections.Generic;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Core.Services
{
    public class ContextManager : IContextManager
    {
        private readonly Dictionary<GameContext, INavigableContext> _contextMap = new Dictionary<GameContext, INavigableContext>();
        private readonly Stack<INavigableContext> _contextStack = new Stack<INavigableContext>();
        private GameContext _currentGameContext = GameContext.Unknown;

        public GameContext CurrentGameContext => _currentGameContext;
        public INavigableContext ActiveContext => _contextStack.Count > 0 ? _contextStack.Peek() : null;

        public event Action<GameContext, GameContext> OnContextChanged;

        public void RegisterContext(GameContext gameContext, INavigableContext navigableContext)
        {
            _contextMap[gameContext] = navigableContext;
        }

        public void SetContext(GameContext context)
        {
            if (context == _currentGameContext)
                return;

            var previousContext = _currentGameContext;

            while (_contextStack.Count > 0)
            {
                var ctx = _contextStack.Pop();
                ctx.OnExit();
            }

            _currentGameContext = context;

            if (_contextMap.TryGetValue(context, out var newContext))
            {
                _contextStack.Push(newContext);
                newContext.OnEnter();
            }

            OnContextChanged?.Invoke(previousContext, context);
        }

        public void PushContext(INavigableContext context)
        {
            if (ActiveContext != null)
            {
                context.ParentContext = ActiveContext;
            }

            _contextStack.Push(context);
            context.OnEnter();
        }

        public void PopContext()
        {
            if (_contextStack.Count > 1)
            {
                var ctx = _contextStack.Pop();
                ctx.OnExit();

                ActiveContext?.OnEnter();
            }
        }

        public bool HasContext(GameContext context)
        {
            return _contextMap.ContainsKey(context);
        }
    }
}
