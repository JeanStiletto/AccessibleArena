# NavigatorManager.cs Code Index

## Summary
Manages all screen navigators. Only one navigator is active at a time. Handles priority, activation, and lifecycle events.

## Classes

### class NavigatorManager (line 13)
```
public static NavigatorManager Instance { get; private set; } (line 16)
private readonly List<IScreenNavigator> _navigators (line 18)
private IScreenNavigator _activeNavigator (line 19)
private string _currentScene (line 20)

public NavigatorManager() (line 22)
public IScreenNavigator ActiveNavigator => _activeNavigator (line 33)
public string CurrentScene => _currentScene (line 36)
public void Register(IScreenNavigator navigator) (line 39)
public void RegisterAll(params IScreenNavigator[] navigators) (line 48)
public void Update() (line 57)
  // NOTE: Checks higher-priority navigators and allows preemption
public void OnSceneChanged(string sceneName) (line 111)
public void DeactivateCurrent() (line 132)
public IScreenNavigator GetNavigator(string navigatorId) (line 139)
public T GetNavigator<T>() where T : class, IScreenNavigator (line 145)
public IReadOnlyList<IScreenNavigator> GetAllNavigators() (line 151)
public bool IsNavigatorActive(string navigatorId) (line 154)
public bool HasActiveNavigator => _activeNavigator != null (line 160)
```
