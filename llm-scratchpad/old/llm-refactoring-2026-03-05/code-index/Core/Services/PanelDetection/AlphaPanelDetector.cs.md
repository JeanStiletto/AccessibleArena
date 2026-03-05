# AlphaPanelDetector.cs - Code Index

## File-level Comment
Detector that uses CanvasGroup alpha to detect popup visibility.
Polling-based detection for popups without controllers (pure CanvasGroup fade).
Handles: SystemMessageView, Dialog, Modal, Popups (alpha-based visibility)

## Classes

### AlphaPanelDetector (line 16)
```csharp
public class AlphaPanelDetector
```

#### Properties
- public string DetectorId (line 18) - Returns "AlphaDetector"

#### Fields - Configuration
- private const float VisibleThreshold = 0.99f (line 25)
- private const float HiddenThreshold = 0.01f (line 26)
- private const int CheckIntervalFrames = 10 (line 27)
- private const int CacheRefreshMultiplier = 6 (line 28)

#### Fields - State
- private PanelStateManager _stateManager (line 21)
- private bool _initialized (line 22)
- private int _frameCounter (line 34)
- private readonly Dictionary<int, TrackedPanel> _knownPanels (line 35)
- private readonly HashSet<string> _announcedPanels (line 36)

#### Nested Class - TrackedPanel (line 38)
```csharp
private class TrackedPanel
```
- public GameObject GameObject { get; set; } (line 40)
- public CanvasGroup CanvasGroup { get; set; } (line 41)
- public string Name { get; set; } (line 42)
- public bool WasVisible { get; set; } (line 43)

#### Methods - Initialization
- public void Initialize(PanelStateManager stateManager) (line 48)
- public void Update() (line 61)
  - Poll for visibility changes every CheckIntervalFrames
- public void Reset() (line 80)
- public void ResetPanel(string panelName) (line 93)
  - Reset tracking state for a specific panel by name

#### Panel Ownership (Stage 5.3)
- public static readonly string[] OwnedPatterns (line 122)
  - OWNED PATTERNS - AlphaDetector is the authoritative detector for these panels
  - Patterns: systemmessageview, dialog, modal, invitefriend, fullscreenzfbrowsercanvas
- private const string PopupPattern = "popup" (line 135)
  - Special case: "popup" (but NOT "popupbase") is also owned by AlphaDetector
- private const string PopupBasePattern = "popupbase" (line 136)

#### Methods - Panel Handling
- public bool HandlesPanel(string panelName) (line 140)
  - Check if this detector handles a given panel
- private void RefreshPanelCache() (line 170)
  - Discover and cache alpha-based panels
- private void CheckForVisibilityChanges() (line 208)
  - Check tracked panels for alpha changes
- private void ReportPanelOpened(TrackedPanel panel) (line 245)
- private void ReportPanelClosed(TrackedPanel panel) (line 258)
- private bool IsTrackedPanel(string name) (line 264)
  - Check if panel name matches tracking patterns
- private bool HasInteractiveChild(GameObject go) (line 270)
  - Check if panel has interactive elements (Button, CustomButton)
- private float GetEffectiveAlpha(GameObject go, CanvasGroup cg) (line 288)
  - Get effective alpha considering parent CanvasGroups
- private void CleanupDestroyedPanels() (line 314)
  - Remove destroyed panels from tracking
