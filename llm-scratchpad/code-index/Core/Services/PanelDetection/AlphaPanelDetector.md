# AlphaPanelDetector.cs
Path: src/Core/Services/PanelDetection/AlphaPanelDetector.cs
Lines: 342

## Top-level comments
- Polling-based popup visibility detector using CanvasGroup alpha (thresholds 0.99/0.01). Handles SystemMessageView, Dialog, Modal, Popups. Checks every 10 frames, refreshes cache every 60.

## public class AlphaPanelDetector (line 16)
### Fields
- private PanelStateManager _stateManager (line 21)
- private bool _initialized (line 22)
- private const float VisibleThreshold = 0.99f (line 25)
- private const float HiddenThreshold = 0.01f (line 26)
- private const int CheckIntervalFrames = 10 (line 27)
- private const int CacheRefreshMultiplier = 6 (line 28)
- private int _frameCounter (line 34)
- private readonly Dictionary<int, TrackedPanel> _knownPanels (line 35)
- private readonly HashSet<string> _announcedPanels (line 36)
- public static readonly string[] OwnedPatterns (line 122) — Note: authoritative list; other detectors must exclude these patterns
- private const string PopupPattern = "popup" (line 135)
- private const string PopupBasePattern = "popupbase" (line 136)
### Properties
- public string DetectorId => "AlphaDetector" (line 18)
### Methods
- public void Initialize(PanelStateManager stateManager) (line 48)
- public void Update() (line 61)
- public void Reset() (line 80)
- public void ResetPanel(string panelName) (line 93) — Note: called by PanelStateManager when it removes an alpha-owned panel as invalid; resets WasVisible so popup can be re-detected
- public bool HandlesPanel(string panelName) (line 140)
- private void RefreshPanelCache() (line 170)
- private void CheckForVisibilityChanges() (line 208) — Note: suppresses popups while _stateManager.IsSceneLoading is true
- private void ReportPanelOpened(TrackedPanel panel) (line 251)
- private void ReportPanelClosed(TrackedPanel panel) (line 263)
- private bool IsTrackedPanel(string name) (line 270)
- private bool HasInteractiveChild(GameObject go) (line 276)
- private float GetEffectiveAlpha(GameObject go, CanvasGroup cg) (line 294)
- private void CleanupDestroyedPanels() (line 320) — Note: reports panel closed via name when a tracked visible panel's GameObject is destroyed

## private class TrackedPanel (line 39)
### Properties
- public GameObject GameObject { get; set; } (line 41)
- public CanvasGroup CanvasGroup { get; set; } (line 42)
- public string Name { get; set; } (line 43)
- public bool WasVisible { get; set; } (line 44)
