# PanelStateManager.cs - Code Index

## File-level Comment
Single source of truth for panel state in MTGA.
All panel changes flow through this manager.
Detectors report changes here; consumers subscribe to events.

## Classes

### PanelStateManager (line 15)
```csharp
public class PanelStateManager
```

#### Singleton
- private static PanelStateManager _instance (line 19)
- public static PanelStateManager Instance (line 20)

#### Events
- public event Action<PanelInfo, PanelInfo> OnPanelChanged (line 29)
  - Fired when the active panel changes
- public event Action<PanelInfo> OnAnyPanelOpened (line 35)
  - Fired when ANY panel opens (use for triggering rescans)
- public event Action<int> OnPlayBladeStateChanged (line 40)
  - Fired when PlayBlade state specifically changes

#### Fields - Detectors
- private HarmonyPanelDetector _harmonyDetector (line 47)
- private ReflectionPanelDetector _reflectionDetector (line 48)
- private AlphaPanelDetector _alphaDetector (line 49)

#### Fields - State
- public PanelInfo ActivePanel { get; private set; } (line 59)
  - Currently active foreground panel (highest priority panel that filters navigation)
- private readonly List<PanelInfo> _panelStack (line 65)
  - Stack of all active panels, ordered by priority
- public int PlayBladeState { get; private set; } (line 71)
  - Current PlayBlade visual state (0=Hidden, 1=Events, 2=DirectChallenge, 3=FriendChallenge)
- public bool IsPlayBladeActive (line 79)
  - Property: whether PlayBlade is currently visible
- private float _lastChangeTime (line 110)
  - Debounce: time of last panel change
- private const float DebounceSeconds = 0.1f (line 111)
- private readonly HashSet<string> _announcedPanels (line 116)
  - Track announced panels to prevent double announcements
- private readonly Dictionary<string, PanelDetectionMethod> _panelDetectorAudit (line 122)
  - DIAGNOSTIC: Track which detector first reported each panel name
- private readonly Dictionary<string, HashSet<PanelDetectionMethod>> _overlapAudit (line 129)
  - DIAGNOSTIC: Track potential overlaps detected during runtime

#### Constructor
- public PanelStateManager() (line 137)

#### Methods - Initialization
- public void Initialize() (line 145)
  - Initialize all panel detectors (call once after construction)
- public void Update() (line 163)
  - Update all detectors (call every frame)

#### Methods - Panel State Management
- public bool ReportPanelOpened(PanelInfo panel) (line 184)
  - Report that a panel has opened (called by detectors)
- public bool ReportPanelClosed(GameObject gameObject) (line 244)
  - Report that a panel has closed (called by detectors)
- public bool ReportPanelClosedByName(string panelName) (line 272)
  - Report that a panel has closed by name (when GameObject reference unavailable)
- public void SetPlayBladeState(int state) (line 290)
  - Update PlayBlade state

#### Methods - Stack Management (Private)
- private void AddToStack(PanelInfo panel) (line 313)
- private void UpdateActivePanel() (line 320)

#### Methods - Query Methods
- public GameObject GetFilterPanel() (line 369)
  - Get the GameObject to use for filtering navigation elements
- public bool IsPanelTypeActive(PanelType type) (line 384)
  - Check if a specific panel type is currently active
- public bool IsPanelActive(string panelName) (line 393)
  - Check if a panel with the given name is currently active
- public bool IsSettingsMenuOpen (line 402)
  - Property: check if Settings menu is currently open
- public IReadOnlyList<PanelInfo> GetPanelStack() (line 407)
  - Get all currently tracked panels (for debugging)
- public bool HasBeenAnnounced(string panelName) (line 415)
  - Check if a panel has been announced
- public void MarkAnnounced(string panelName) (line 423)
  - Mark a panel as announced

#### Methods - Reset
- public void Reset() (line 435)
  - Clear all panel state (for scene changes)
- public void SoftReset() (line 460)
  - Soft reset - keep tracking but clear announced state

#### Methods - Validation
- public void ValidatePanels() (line 473)
  - Validate all tracked panels are still valid (call periodically from Update)

#### Methods - Diagnostic (Stage 5.2 Overlap Audit)
- private void TrackPanelDetectorAudit(PanelInfo panel) (line 506)
  - Track which detector reports each panel for overlap audit
- public void DumpOverlapAudit() (line 540)
  - Dump the overlap audit results to log
- public void ClearAuditData() (line 582)
  - Clear diagnostic audit data
- public void RunStaticOverlapAnalysis() (line 593)
  - Run static analysis of known panel names against each detector's HandlesPanel() method
