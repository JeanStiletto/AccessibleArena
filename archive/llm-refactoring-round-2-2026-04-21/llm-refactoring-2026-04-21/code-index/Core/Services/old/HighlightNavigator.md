# HighlightNavigator.cs
Path: src/Core/Services/old/HighlightNavigator.cs
Lines: 612

## Top-level comments
- Handles Tab navigation through highlighted/playable cards during normal gameplay. HotHighlight shows which cards can be played or activated. Does NOT handle targeting mode (see TargetNavigator).

## public class HighlightNavigator (line 20)

### Fields
- private readonly IAnnouncementService _announcer (line 22)
- private readonly ZoneNavigator _zoneNavigator (line 23)
- private List<HighlightedCard> _highlightedCards (line 25)
- private int _currentIndex = -1 (line 26)
- private bool _isActive (line 27)
- private bool _pendingRescan (line 28)
- private float _rescanTime (line 29)
- private int _rescanAttempts (line 30)
- private const int MaxRescanAttempts = 5 (line 31)
- private const float RescanDelay = 0.3f (line 32)

### Properties
- public bool IsActive => _isActive (line 34)
- public int HighlightCount => _highlightedCards.Count (line 35)
- public HighlightedCard CurrentCard (line 36)

### Methods
- public HighlightNavigator(IAnnouncementService announcer, ZoneNavigator zoneNavigator) (line 41)
- public void Activate() (line 50)
- public void Deactivate() (line 59)
- public bool HandleInput() (line 73) — Note: Tab cycles, Enter activates; falls back to letting other handlers process Enter when nothing selected
- private void ActivateCurrentCard() (line 179) — Note: uses PlayCardViaTwoClick for hand cards
- public void NextCard() (line 238)
- public void PreviousCard() (line 253)
- private void AnnounceCurrentCard() (line 271) — Note: also syncs EventSystem focus, ZoneNavigator zone, and CardInfoNavigator
- private ZoneType StringToZoneType(string zone) (line 311)
- private void DiscoverHighlightedCards() (line 326) — Note: schedules delayed rescans when only battlefield cards appear (turn-start timing issue)
- private string GetParentZone(GameObject card) (line 420)
- private HighlightedCard CreateHighlightedCard(GameObject cardObj, string zoneName) (line 440)
- private string GetZoneDisplayName(string zone) (line 465)
- private bool IsStackResolutionPending() (line 482)
- private bool HasResolveButton() (line 502)
- private string GetPrimaryButtonText() (line 563)

## public class HighlightedCard (line 604)

### Properties
- public GameObject GameObject { get; set; } (line 606)
- public string Name { get; set; } (line 607)
- public string Zone { get; set; } (line 608)
- public uint InstanceId { get; set; } (line 609)
