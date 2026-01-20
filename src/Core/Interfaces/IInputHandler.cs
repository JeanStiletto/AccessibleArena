using System;
using UnityEngine;

namespace AccessibleArena.Core.Interfaces
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
