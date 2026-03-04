# PanelType.cs - Code Index

## File-level Comment
Types of panels in the MTGA UI.
Each type has different detection methods and behaviors.

## Enums

### PanelType (line 7)
```csharp
public enum PanelType
```
Types of panels:
- None = 0 (line 10) - No panel active
- Login = 1 (line 13) - Login scene panels (Welcome, Login, Registration)
- Settings = 2 (line 16) - Settings menu overlay
- Blade = 3 (line 19) - PlayBlade and sub-blades (deck selection, events)
- Social = 4 (line 22) - Friends/Social panel (F4)
- Popup = 5 (line 25) - Alpha-based popups (SystemMessageView, dialogs, modals)
- ContentPanel = 6 (line 28) - NavContentController descendants (home, profile, store)
- Campaign = 7 (line 31) - Color Challenge / Campaign panels

### PanelDetectionMethod (line 38)
```csharp
public enum PanelDetectionMethod
```
Detection method for panels:
- Harmony (line 41) - Event-driven via Harmony patches on property setters
- Reflection (line 44) - Polling via reflection on IsOpen properties
- Alpha (line 47) - Polling via CanvasGroup alpha state
