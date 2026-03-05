# UnifiedPanelDetector.cs

## Overview
Unified panel detection system that tracks visibility of menu panels, popups, and overlays using CanvasGroup alpha state comparison.

Replaces complex cooldown/timer-based detection with simple state comparison:
- Every N frames, scan for visible panels
- Compare to previous state
- If changed, report what appeared/disappeared

Uses alpha thresholds at extremes (0.01/0.99) to detect only when animations are complete, avoiding false triggers during fade transitions.

## Class: UnifiedPanelDetector (line 21)

### Configuration
- private const float VisibleThreshold (line 29)
- private const float HiddenThreshold (line 30)
- private const int CheckIntervalFrames (line 31)
- private const int CacheRefreshMultiplier (line 32)
- private const float MinPanelSize (line 33)
- private static readonly string[] TrackedPanelPatterns (line 39)
  - Note: ONLY actual popups/dialogs without IsOpen property
  - Do NOT include: SettingsMenu, PlayBlade, SocialUI, FriendsWidget

### State
- private readonly string _logPrefix (line 48)
- private int _frameCounter (line 49)
- private readonly Dictionary<int, PanelInfo> _knownPanels (line 52)
- private GameObject _topmostPanel (line 54)
- private readonly HashSet<string> _announcedPanelNames (line 58)
  - Persists across Reset() to prevent double announcements

### Public Types
- public class PanelChangeInfo (line 67)
  - bool HasChange
  - string AppearedPanelName
  - string DisappearedPanelName
  - GameObject TopmostPanel
- private class PanelInfo (line 75)
  - GameObject GameObject
  - CanvasGroup CanvasGroup
  - string Name
  - int HierarchyDepth
  - bool IsPopup
  - bool WasVisible

### Constructor
- public UnifiedPanelDetector(string logPrefix = "UnifiedPanelDetector") (line 89)

### Public Methods
- public void Reset() (line 102)
  - Soft reset - preserves announced state
- public void ResetForSceneChange() (line 115)
  - Full reset including announced panels
- public PanelChangeInfo CheckForChanges() (line 127)
  - Call every frame, performs actual check every N frames
- public GameObject GetTopmostVisiblePanel() (line 276)
- public void RefreshPanelCache() (line 284)

### Private Methods
- private bool IsPanelCandidate(GameObject go) (line 345)
- private bool HasInteractiveChild(GameObject go) (line 367)
- private bool IsTrackedPanel(string name) (line 388)
- private float GetEffectiveAlpha(GameObject go, CanvasGroup cg) (line 407)
- private int CalculatePanelPriority(PanelInfo info) (line 445)
  - Higher priority = more on top
- private int GetHierarchyDepth(Transform t) (line 464)
- private void CleanupDestroyedPanels() (line 478)
- private string CleanPanelName(string name) (line 494)

### Static Utilities
- public static bool IsChildOf(GameObject child, GameObject parent) (line 527)
