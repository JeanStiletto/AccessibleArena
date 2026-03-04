# HighlightNavigator.cs - Code Index

## File-level Comment
Handles Tab navigation through highlighted/playable cards during normal gameplay.
The game uses HotHighlight to show which cards can be played or activated.
Tab cycles through these cards, replacing the default button-cycling behavior.
This does NOT handle targeting mode - TargetNavigator handles that separately.

## Classes

### HighlightNavigator (line 20)
```csharp
public class HighlightNavigator
```

#### Fields
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

#### Properties
- public bool IsActive (line 34)
- public int HighlightCount (line 35)
- public HighlightedCard CurrentCard (line 36)

#### Constructor
- public HighlightNavigator(IAnnouncementService announcer, ZoneNavigator zoneNavigator) (line 41)

#### Methods - Lifecycle
- public void Activate() (line 50)
- public void Deactivate() (line 59)

#### Methods - Input Handling
- public bool HandleInput() (line 73)
  - Handles Tab input to cycle through highlighted cards and Enter to play
  - Returns true if input was consumed
- private void ActivateCurrentCard() (line 179)
  - Activates (plays) the currently highlighted card
- public void NextCard() (line 239)
- public void PreviousCard() (line 253)
- private void AnnounceCurrentCard() (line 271)
- private ZoneType StringToZoneType(string zone) (line 311)

#### Methods - Discovery
- private void DiscoverHighlightedCards() (line 326)
  - Discovers all cards with active HotHighlight indicators
  - Note: Complex timing logic for hand card discovery after card play
- private string GetParentZone(GameObject card) (line 420)
- private HighlightedCard CreateHighlightedCard(GameObject cardObj, string zoneName) (line 440)
- private string GetZoneDisplayName(string zone) (line 465)

#### Methods - Stack Resolution Detection
- private bool IsStackResolutionPending() (line 482)
  - Checks if there's pending stack resolution that should take priority
- private bool HasResolveButton() (line 502)
  - Checks if a "Resolve" button is currently visible
- private string GetPrimaryButtonText() (line 563)
  - Gets the text of the primary prompt button if one exists

### HighlightedCard (line 604)
```csharp
public class HighlightedCard
```
Information about a highlighted/playable card.

#### Properties
- public GameObject GameObject { get; set; } (line 606)
- public string Name { get; set; } (line 607)
- public string Zone { get; set; } (line 608)
- public uint InstanceId { get; set; } (line 609)
