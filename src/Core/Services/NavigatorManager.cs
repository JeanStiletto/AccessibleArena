using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Patches;
using System.Collections.Generic;
using System.Linq;

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

            // Subscribe to panel state changes from Harmony patch
            PanelStatePatch.OnPanelStateChanged += OnPanelStateChanged;
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
            MelonLogger.Msg($"[NavigatorManager] Registered: {navigator.NavigatorId} (priority {navigator.Priority})");
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
            // If we have an active navigator, let it handle updates
            if (_activeNavigator != null)
            {
                _activeNavigator.Update();

                // Check if it deactivated itself
                if (!_activeNavigator.IsActive)
                {
                    MelonLogger.Msg($"[NavigatorManager] {_activeNavigator.NavigatorId} deactivated");
                    _activeNavigator = null;
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
                    MelonLogger.Msg($"[NavigatorManager] {navigator.NavigatorId} activated");
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

        /// <summary>Check if any navigator is active</summary>
        public bool HasActiveNavigator => _activeNavigator != null;

        /// <summary>
        /// Called by Harmony patch when a panel's state changes.
        /// Notifies the active navigator to rescan elements.
        /// </summary>
        private void OnPanelStateChanged(object controller, bool isOpen, string typeName)
        {
            MelonLogger.Msg($"[NavigatorManager] Panel state changed: {typeName} isOpen={isOpen}");

            // Notify active navigator to rescan
            if (_activeNavigator != null)
            {
                // Check if navigator has a rescan method
                if (_activeNavigator is GeneralMenuNavigator generalMenu)
                {
                    generalMenu.OnPanelStateChangedExternal(typeName, isOpen);
                }
                else
                {
                    // For other navigators, force a recheck
                    MelonLogger.Msg($"[NavigatorManager] Active navigator {_activeNavigator.NavigatorId} will recheck on next update");
                }
            }
        }
    }
}
