using System;
using UnityEngine;

namespace MTGAAccessibility.Core.Interfaces
{
    public interface IInputHandler
    {
        void OnUpdate();

        event Action<KeyCode> OnKeyPressed;
        event Action OnNavigateNext;
        event Action OnNavigatePrevious;
        event Action OnAccept;
        event Action OnCancel;
    }
}
