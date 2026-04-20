using MelonLoader;
using AccessibleArena.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Manages all screen navigators. Only one navigator is active at a time.
    /// Handles priority, activation, and lifecycle events.
    /// </summary>
    public class NavigatorManager
    {
        /// <summary>Singleton instance for access from patches</summary>
        public static NavigatorManager Instance { get; private set; }

        private readonly List<IScreenNavigator> _navigators = new List<IScreenNavigator>();
        private IScreenNavigator _activeNavigator;
        private string _currentScene;

        public NavigatorManager()
        {
            Instance = this;
        }

        /// <summary>Currently active navigator, if any</summary>
        public IScreenNavigator ActiveNavigator => _activeNavigator;

        /// <summary>Current scene name</summary>
        public string CurrentScene => _currentScene;

        /// <summary>Register a navigator. Higher priority navigators are checked first.</summary>
        public void Register(IScreenNavigator navigator)
        {
            _navigators.Add(navigator);
            // Sort by priority descending
            _navigators.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            Log.Msg("NavigatorManager", $"Registered: {navigator.NavigatorId} (priority {navigator.Priority})");
        }

        /// <summary>Register multiple navigators</summary>
        public void RegisterAll(params IScreenNavigator[] navigators)
        {
            foreach (var nav in navigators)
            {
                Register(nav);
            }
        }

        /// <summary>Call this every frame from main mod</summary>
        public void Update()
        {
            // Check if a higher-priority navigator should preempt the current one
            // This allows RewardPopupNavigator (86) to take over from GeneralMenuNavigator (15)
            if (_activeNavigator != null)
            {
                foreach (var navigator in _navigators)
                {
                    // Only check navigators with higher priority than the active one
                    if (navigator.Priority <= _activeNavigator.Priority)
                        break; // List is sorted by priority descending, so we can stop here

                    if (navigator == _activeNavigator)
                        continue;

                    // Let the navigator poll (it may activate itself)
                    navigator.Update();

                    if (navigator.IsActive)
                    {
                        Log.Msg("NavigatorManager", $"{navigator.NavigatorId} preempting {_activeNavigator.NavigatorId}");
                        _activeNavigator.Deactivate();
                        _activeNavigator = navigator;
                        return;
                    }
                }

                // No preemption - let active navigator handle updates
                _activeNavigator.Update();

                // Check if it deactivated itself (or was deactivated by RequestActivation during Update)
                if (_activeNavigator == null || !_activeNavigator.IsActive)
                {
                    if (_activeNavigator != null)
                    {
                        Log.Msg("NavigatorManager", $"{_activeNavigator.NavigatorId} deactivated");
                        _activeNavigator = null;
                    }
                }
                return;
            }

            // No active navigator - poll all to find one that activates
            foreach (var navigator in _navigators)
            {
                navigator.Update();

                if (navigator.IsActive)
                {
                    _activeNavigator = navigator;
                    Log.Msg("NavigatorManager", $"{navigator.NavigatorId} activated");
                    return;
                }
            }
        }

        /// <summary>Called when scene changes</summary>
        public void OnSceneChanged(string sceneName)
        {
            _currentScene = sceneName;

            // Notify all navigators
            foreach (var navigator in _navigators)
            {
                navigator.OnSceneChanged(sceneName);
            }

            // Clear active if it deactivated
            if (_activeNavigator != null && !_activeNavigator.IsActive)
            {
                _activeNavigator = null;
            }
            // Note: Don't ForceRescan immediately - wait for panel detection to confirm
            // the new screen is ready (IsReadyToShow). PanelStateManager.OnPanelChanged
            // will trigger the rescan when the screen is fully loaded.
        }

        /// <summary>Force deactivate current navigator</summary>
        public void DeactivateCurrent()
        {
            _activeNavigator?.Deactivate();
            _activeNavigator = null;
        }

        /// <summary>Get navigator by ID</summary>
        public IScreenNavigator GetNavigator(string navigatorId)
        {
            return _navigators.FirstOrDefault(n => n.NavigatorId == navigatorId);
        }

        /// <summary>Get navigator by type</summary>
        public T GetNavigator<T>() where T : class, IScreenNavigator
        {
            return _navigators.OfType<T>().FirstOrDefault();
        }

        /// <summary>Get all registered navigators</summary>
        public IReadOnlyList<IScreenNavigator> GetAllNavigators() => _navigators;

        /// <summary>Check if a specific navigator is currently active</summary>
        public bool IsNavigatorActive(string navigatorId)
        {
            return _activeNavigator?.NavigatorId == navigatorId;
        }

        /// <summary>
        /// Force-activate a navigator by ID, regardless of priority.
        /// Deactivates the current navigator, then polls the target so it can activate.
        /// Used for explicit user actions like F4 opening chat during a duel.
        /// </summary>
        public bool RequestActivation(string navigatorId)
        {
            var target = GetNavigator(navigatorId);
            if (target == null)
            {
                Log.Warn("NavigatorManager", $"RequestActivation: navigator '{navigatorId}' not found");
                return false;
            }

            var previous = _activeNavigator;

            // Deactivate current navigator
            if (_activeNavigator != null && _activeNavigator != target)
            {
                Log.Msg("NavigatorManager", $"RequestActivation: deactivating {_activeNavigator.NavigatorId} for {navigatorId}");
                _activeNavigator.Deactivate();
                _activeNavigator = null;
            }

            // Poll the target so it can detect its screen and activate
            target.Update();

            if (target.IsActive)
            {
                _activeNavigator = target;
                Log.Msg("NavigatorManager", $"RequestActivation: {navigatorId} activated successfully");
                return true;
            }

            // Target didn't activate - restore previous navigator so we don't leave a gap
            if (previous != null && previous != target)
            {
                Log.Msg("NavigatorManager", $"RequestActivation: {navigatorId} did not activate, restoring {previous.NavigatorId}");
                previous.Update(); // Re-poll so it can reactivate
                if (previous.IsActive)
                    _activeNavigator = previous;
            }
            else
            {
                Log.Msg("NavigatorManager", $"RequestActivation: {navigatorId} did not activate");
            }

            return false;
        }

        /// <summary>Check if any navigator is active</summary>
        public bool HasActiveNavigator => _activeNavigator != null;
    }
}
