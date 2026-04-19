# ReflectionPanelDetector.cs - Code Index

## File-level Comment
Detector that uses reflection to poll IsOpen properties on menu controllers.
Polling-based detection for panels with IsOpen but no Harmony-patchable methods.
Handles: Login scene panels, PopupBase descendants
Note: NavContentController is handled by HarmonyDetector via FinishOpen/FinishClose patches.

## Classes

### ReflectionPanelDetector (line 17)
```csharp
public class ReflectionPanelDetector
```

#### Properties
- public string DetectorId (line 19) - Returns "ReflectionDetector"

#### Fields
- private PanelStateManager _stateManager (line 21)
- private bool _initialized (line 22)
- private const int CheckIntervalFrames = 10 (line 25)
- private int _frameCounter (line 26)
- private readonly HashSet<string> _trackedPanels (line 29)
  - Currently tracked panels

#### Static Fields - Configuration
- private static readonly string[] ControllerTypes (line 32)
  - Controller types to check: PopupBase
- private static readonly string[] ExcludedTypeNames (line 39)
  - PopupBase descendants that are NOT real popups: PackProgressMeter
- private static readonly string[] LoginPanelPatterns (line 46)
  - Login scene panel name patterns

#### Methods - Initialization
- public void Initialize(PanelStateManager stateManager) (line 62)
- public void Update() (line 75)
  - Poll for panel changes every CheckIntervalFrames
- public void Reset() (line 87)

#### Panel Ownership (Stage 5.3)
- Comment: ReflectionDetector is the FALLBACK detector (lines 94-110)
  - Handles everything NOT claimed by HarmonyDetector or AlphaDetector
  - Detection method: Polling IsOpen properties on MonoBehaviour controllers

#### Methods - Panel Handling
- public bool HandlesPanel(string panelName) (line 114)
  - Check if this detector handles a given panel (returns true for everything not claimed by others)
- private void CheckForPanelChanges() (line 144)
  - Main polling loop - discover open/closed panels
- private void CheckMenuControllers(List<(string id, GameObject obj)> panels) (line 175)
  - Check menu controllers with IsOpen property
- private void CheckLoginPanels(List<(string id, GameObject obj)> panels) (line 220)
  - Check Login scene panels by name pattern
- private bool CheckIsOpen(MonoBehaviour mb, Type type) (line 246)
  - Check if a controller's IsOpen property/method returns true
- private void ReportPanelOpened(string panelId, GameObject obj) (line 307)
- private void ReportPanelClosed(string panelId) (line 328)
