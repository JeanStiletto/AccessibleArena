using System.Collections.Generic;
using UnityEngine;

namespace AccessibleArena.Core.Interfaces
{
    /// <summary>
    /// Interface for screen-specific navigators that handle Tab/Enter navigation
    /// when Unity's EventSystem doesn't work properly.
    /// </summary>
    public interface IScreenNavigator
    {
        /// <summary>Unique identifier for this navigator (used for logging/debugging)</summary>
        string NavigatorId { get; }

        /// <summary>Human-readable screen name announced to user</summary>
        string ScreenName { get; }

        /// <summary>Priority for activation. Higher = checked first. Default: 0</summary>
        int Priority { get; }

        /// <summary>Whether this navigator is currently active and handling input</summary>
        bool IsActive { get; }

        /// <summary>Number of navigable elements</summary>
        int ElementCount { get; }

        /// <summary>Current focused element index</summary>
        int CurrentIndex { get; }

        /// <summary>
        /// Called every frame by NavigatorManager.
        /// Should check for activation conditions if not active.
        /// </summary>
        void Update();

        /// <summary>Forcefully deactivate this navigator</summary>
        void Deactivate();

        /// <summary>Called when scene changes - opportunity to reset state</summary>
        void OnSceneChanged(string sceneName);

        /// <summary>
        /// Force element rediscovery. Called by NavigatorManager after scene change
        /// if the navigator stayed active.
        /// </summary>
        void ForceRescan();

        /// <summary>
        /// Gets the GameObjects of all navigable elements in order.
        /// Used by Tab navigation fallback to use the same elements as arrow key navigation.
        /// </summary>
        IReadOnlyList<GameObject> GetNavigableGameObjects();
    }
}
