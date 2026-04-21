# DiscardNavigator.cs
Path: src/Core/Services/old/DiscardNavigator.cs
Lines: 315

## Top-level comments
- Handles card selection during discard phases. Detects discard mode via "Submit X" button; Enter toggles selection, Space submits. Zone navigation (C, arrows) handled by ZoneNavigator.

## public class DiscardNavigator (line 19)

### Fields
- private readonly IAnnouncementService _announcer (line 21)
- private readonly ZoneNavigator _zoneNavigator (line 22)
- private static readonly Regex ButtonNumberPattern (line 25)
- private static readonly Regex[] DiscardCountPatterns (line 30)
- private static readonly Regex[] DiscardOnePatterns (line 36)
- private int? _requiredCount = null (line 43)
- private bool _hasLoggedTargetYield = false (line 44)

### Methods
- public DiscardNavigator(IAnnouncementService announcer, ZoneNavigator zoneNavigator) (line 46)
- public bool IsDiscardModeActive() (line 56) — Note: yields to targeting mode when HotHighlight targets exist
- public int? GetRequiredDiscardCount() (line 87)
- public (int count, GameObject button)? GetSubmitButtonInfo() (line 126)
- public bool IsCardSelectedForDiscard(GameObject card) (line 158)
- public string GetSelectionStateText(GameObject card) (line 182)
- public bool HandleInput() (line 196) — Note: caches _requiredCount and announces on first activation
- private void ToggleCurrentCard() (line 231)
- private IEnumerator AnnounceSelectionCountDelayed() (line 259) — Note: waits 0.2s before reading updated count
- private void TrySubmit() (line 276)
