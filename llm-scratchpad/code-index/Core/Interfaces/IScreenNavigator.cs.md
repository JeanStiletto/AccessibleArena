# IScreenNavigator.cs Code Index

## File Overview
Interface for screen-specific navigators that handle Tab/Enter navigation when Unity's EventSystem doesn't work properly.

## Interface: IScreenNavigator (line 10)

### Properties
- string NavigatorId { get; } (line 13)
  // Unique identifier for this navigator (used for logging/debugging)

- string ScreenName { get; } (line 15)
  // Human-readable screen name announced to user

- int Priority { get; } (line 18)
  // Priority for activation. Higher = checked first. Default: 0

- bool IsActive { get; } (line 21)
  // Whether this navigator is currently active and handling input

- int ElementCount { get; } (line 24)
  // Number of navigable elements

- int CurrentIndex { get; } (line 27)
  // Current focused element index

### Methods
- void Update() (line 34)
  // Called every frame by NavigatorManager. Should check for activation conditions if not active.

- void Deactivate() (line 37)
  // Forcefully deactivate this navigator

- void OnSceneChanged(string sceneName) (line 40)
  // Called when scene changes - opportunity to reset state

- void ForceRescan() (line 46)
  // Force element rediscovery. Called by NavigatorManager after scene change if navigator stayed active.

- IReadOnlyList<GameObject> GetNavigableGameObjects() (line 52)
  // Gets the GameObjects of all navigable elements in order. Used by Tab navigation fallback.
