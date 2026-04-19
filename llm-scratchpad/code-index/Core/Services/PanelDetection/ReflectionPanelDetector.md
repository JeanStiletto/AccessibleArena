# ReflectionPanelDetector.cs
Path: src/Core/Services/PanelDetection/ReflectionPanelDetector.cs
Lines: 342

## Top-level comments
- Fallback detector; polls IsOpen (property or method) on MonoBehaviour controllers. Owns PopupBase descendants and Login scene panels (Panel - WelcomeGate, Panel - Log In, etc.). Excludes all Harmony- and Alpha-owned patterns.

## public class ReflectionPanelDetector (line 19)
### Fields
- private PanelStateManager _stateManager (line 23)
- private bool _initialized (line 24)
- private const int CheckIntervalFrames = 10 (line 27)
- private int _frameCounter (line 28)
- private readonly HashSet<string> _trackedPanels (line 31)
- private static readonly string[] ControllerTypes (line 34)
- private static readonly string[] ExcludedTypeNames (line 41)
- private static readonly string[] LoginPanelPatterns (line 47)
### Properties
- public string DetectorId => "ReflectionDetector" (line 20)
### Methods
- public void Initialize(PanelStateManager stateManager) (line 63)
- public void Update() (line 76)
- public void Reset() (line 88)
- public bool HandlesPanel(string panelName) (line 115)
- private void CheckForPanelChanges() (line 145)
- private void CheckMenuControllers(List<(string id, GameObject obj)> panels) (line 176)
- private void CheckLoginPanels(List<(string id, GameObject obj)> panels) (line 221)
- private bool CheckIsOpen(MonoBehaviour mb, Type type) (line 246) — Note: checks IsOpen property, then IsOpen() method, then IsReadyToShow property; swallows reflection exceptions with Warning log
- private void ReportPanelOpened(string panelId, GameObject obj) (line 308)
- private void ReportPanelClosed(string panelId) (line 329)
