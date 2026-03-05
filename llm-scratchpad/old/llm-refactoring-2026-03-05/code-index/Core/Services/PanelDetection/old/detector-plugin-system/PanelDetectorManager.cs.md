# PanelDetectorManager.cs - Code Index

## File-level Comment
Manages all panel detectors and coordinates their updates.
Acts as the entry point for the detection system.

Architecture:
- PanelDetectorManager owns all detectors
- Each detector reports to PanelStateManager
- PanelStateManager fires events that consumers (navigators) subscribe to

## Classes

### PanelDetectorManager (line 15)
```csharp
public class PanelDetectorManager
```

#### Singleton
- private static PanelDetectorManager _instance (line 19)
- public static PanelDetectorManager Instance (line 20)

#### Fields
- private readonly List<IPanelDetector> _detectors (line 24)
- private PanelStateManager _stateManager (line 25)
- private bool _initialized (line 26)

#### Constructor
- public PanelDetectorManager() (line 28)

#### Methods - Initialization
- public void Initialize(PanelStateManager stateManager) (line 37)
  - Initialize the detection system with all detectors
- public void RegisterDetector(IPanelDetector detector) (line 66)
  - Register a detector (call before Initialize() to add custom detectors)

#### Methods - Update
- public void Update() (line 77)
  - Called every frame, updates all polling-based detectors
- public void Reset() (line 94)
  - Reset all detectors (called on scene changes)

#### Methods - Query
- public IPanelDetector GetDetectorForPanel(string panelName) (line 110)
  - Check which detector handles a given panel
- public IReadOnlyList<IPanelDetector> GetDetectors() (line 124)
  - Get all registered detectors (for debugging)
