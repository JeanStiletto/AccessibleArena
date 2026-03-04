# PanelAnimationDiagnostic.cs Code Index

## Summary
Diagnostic tool to track panel animation states vs alpha changes. Use F11 to dump current panel state and start/stop tracking. Purpose: Determine if we can unify panel detection on animation lifecycle methods (FinishOpen/FinishClose) instead of alpha polling.

## Classes

### class PanelAnimationDiagnostic (line 17)
```
private const float VisibleThreshold = 0.99f (line 22)
private const float HiddenThreshold = 0.01f (line 23)
private const int TrackingIntervalFrames = 5 (line 24)
private static readonly string[] PanelPatterns (line 27)

private bool _isTracking (line 37)
private int _frameCounter (line 38)
private readonly Dictionary<int, TrackedPanel> _trackedPanels (line 39)
private float _trackingStartTime (line 40)

public void ToggleTracking() (line 82)
  // NOTE: Toggle tracking on/off and dump current panel analysis
public void Update() (line 97)
  // NOTE: Call every frame from Update() when tracking is active
public void DumpPanelAnalysis() (line 164)
  // NOTE: Dump one-time analysis of all current popup/overlay panels

private void StartTracking() (line 214)
private void StopTrackingAndReport() (line 253)
private TrackedPanel CreateTrackedPanel(GameObject go) (line 322)
private string AnalyzePanel(GameObject go) (line 377)
private float GetAlpha(TrackedPanel panel) (line 414)
private bool GetAnimatorInTransition(TrackedPanel panel) (line 420)
private float GetAnimatorNormalizedTime(TrackedPanel panel) (line 438)
private string GetPath(Transform t) (line 461)
```

### class TrackedPanel (line 46)
```
public GameObject GameObject { get; set; } (line 48)
public string Name { get; set; } (line 49)
public string BaseClasses { get; set; } (line 50)
public bool HasFinishOpen { get; set; } (line 51)
public bool HasFinishClose { get; set; } (line 52)
public bool HasIsOpen { get; set; } (line 53)
public bool HasIsReadyToShow { get; set; } (line 54)
public bool HasAnimator { get; set; } (line 55)
public CanvasGroup CanvasGroup { get; set; } (line 56)
public Component Animator { get; set; } (line 57)
public List<StateSnapshot> Snapshots { get; } (line 60)
public float? FirstVisibleTime { get; set; } (line 61)
public float? AnimationCompleteTime { get; set; } (line 62)
public float? AlphaStableTime { get; set; } (line 63)
```

### class StateSnapshot (line 66)
```
public float Time { get; set; } (line 68)
public float Alpha { get; set; } (line 69)
public bool IsAnimatorInTransition { get; set; } (line 70)
public float NormalizedTime { get; set; } (line 71)
public bool IsActiveInHierarchy { get; set; } (line 72)
```
