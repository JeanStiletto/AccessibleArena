# NavigatorManager.cs
Path: src/Core/Services/NavigatorManager.cs
Lines: 209

## Top-level comments
- Manages all screen navigators; only one navigator is active at a time. Handles priority-based preemption, activation, and lifecycle events.

## public class NavigatorManager (line 12)

### Fields
- private readonly List<IScreenNavigator> _navigators = new List<IScreenNavigator>() (line 17)
- private IScreenNavigator _activeNavigator (line 18)
- private string _currentScene (line 19)

### Properties
- public static NavigatorManager Instance { get; private set; } (line 15)
- public IScreenNavigator ActiveNavigator => _activeNavigator (line 27)
- public string CurrentScene => _currentScene (line 30)
- public bool HasActiveNavigator => _activeNavigator != null (line 207)

### Methods
- public NavigatorManager() (line 21)
- public void Register(IScreenNavigator navigator) (line 33)
- public void RegisterAll(params IScreenNavigator[] navigators) (line 42)
- public void Update() (line 51)
- public void OnSceneChanged(string sceneName) (line 108)
- public void DeactivateCurrent() (line 129)
- public IScreenNavigator GetNavigator(string navigatorId) (line 136)
- public T GetNavigator<T>() where T : class, IScreenNavigator (line 142)
- public IReadOnlyList<IScreenNavigator> GetAllNavigators() (line 148)
- public bool IsNavigatorActive(string navigatorId) (line 151)
- public bool RequestActivation(string navigatorId) (line 161) — Force-activates target by ID regardless of priority; restores previous navigator if target fails to activate
