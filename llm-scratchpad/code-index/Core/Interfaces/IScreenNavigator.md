# IScreenNavigator.cs
Path: src/Core/Interfaces/IScreenNavigator.cs
Lines: 54

## Top-level comments
- Interface for screen-specific navigators handling Tab/Enter navigation when Unity's EventSystem is unavailable.

## interface IScreenNavigator (line 10)
### Properties
- string NavigatorId { get; } (line 13)
- string ScreenName { get; } (line 16)
- int Priority { get; } (line 19)
- bool IsActive { get; } (line 22)
- int ElementCount { get; } (line 25)
- int CurrentIndex { get; } (line 28)
### Methods
- void Update() (line 34) — Note: called every frame by NavigatorManager; should poll activation conditions
- void Deactivate() (line 37)
- void OnSceneChanged(string sceneName) (line 40)
- void ForceRescan() (line 46) — Note: called after scene change when navigator stays active; triggers element rediscovery
- IReadOnlyList<GameObject> GetNavigableGameObjects() (line 52)
