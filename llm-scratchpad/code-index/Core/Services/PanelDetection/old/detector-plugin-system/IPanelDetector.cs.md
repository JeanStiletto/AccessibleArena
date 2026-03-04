# IPanelDetector.cs - Code Index

## File-level Comment
Interface for panel detection plugins.
Each detector handles specific panel types and reports to PanelStateManager.

## Interfaces

### IPanelDetector (line 7)
```csharp
public interface IPanelDetector
```

#### Properties
- string DetectorId { get; } (line 12)
  - Unique identifier for this detector (for logging)

#### Methods
- void Initialize(PanelStateManager stateManager) (line 18)
  - Initialize the detector with a reference to the state manager (called once during setup)
- void Update() (line 24)
  - Called every frame (polling-based detectors do their work here, event-based may do nothing)
- void Reset() (line 29)
  - Reset detector state (called on scene changes)
- bool HandlesPanel(string panelName) (line 37)
  - Check if this detector owns (is responsible for) a given panel
  - Used to prevent duplicate detection across detectors
