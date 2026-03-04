# TargetNavigator.cs - Code Index

## File-level Comment
Handles target selection during spells and abilities.
When the game enters targeting mode, Tab cycles through valid targets.
Enter selects the current target, Backspace cancels.

## Classes

### TargetNavigator (line 15)
```csharp
public class TargetNavigator
```

#### Fields
- private readonly IAnnouncementService _announcer (line 17)
- private readonly ZoneNavigator _zoneNavigator (line 18)
- private bool _isTargeting (line 20)
- private List<TargetInfo> _validTargets (line 21)
- private int _currentIndex = -1 (line 22)

#### Properties
- public bool IsTargeting (line 24)
- public int TargetCount (line 25)
- public TargetInfo CurrentTarget (line 26)

#### Constructor
- public TargetNavigator(IAnnouncementService announcer, ZoneNavigator zoneNavigator) (line 31)

#### Methods - Entry Points
- public bool TryEnterTargetMode(bool requireValidTargets = true) (line 44)
  - Unified entry point for targeting mode (performs all necessary checks)
  - Note: Prefer this over calling EnterTargetMode() directly
- public void EnterTargetMode() (line 90)
  - Called when the game enters targeting mode (discovers targets and announces)
  - Note: Prefer using TryEnterTargetMode() which performs additional checks
- public void ExitTargetMode() (line 118)

#### Methods - Input Handling
- public bool HandleInput() (line 132)
  - Handles input during targeting mode (Tab, Enter, Backspace)
- public void NextTarget() (line 161)
- public void PreviousTarget() (line 172)
- public void SelectCurrentTarget() (line 188)
- public void CancelTargeting() (line 219)
- private void AnnounceCurrentTarget() (line 226)

#### Methods - Zone Detection
- private ZoneType DetermineTargetZone(TargetInfo target) (line 266)
  - Determines the zone type from a target's parent hierarchy
  - Checks for Stack, Graveyard, Exile, Hand before defaulting to Battlefield

#### Methods - Discovery
- private void DiscoverValidTargets() (line 317)
  - Discovers valid targets by scanning for cards with HotHighlight indicators
- private void DiscoverPlayerTargets() (line 378)
  - Discovers player avatars as valid targets when they have HotHighlight
  - Note: Players use MatchTimer objects with structure: MatchTimer > Icon > HoverArea
- private (GameObject highlightRoot, GameObject clickable) FindPlayerMatchTimer(bool isOpponent) (line 424)
  - Finds the player target for targeting spells
  - Searches multiple player-related objects for HotHighlight
  - Returns (highlightRoot, clickableElement) - check HotHighlight on highlightRoot, click clickableElement
- private TargetInfo CreateTargetFromCard(GameObject cardObj) (line 498)
- private CardTargetType DetermineCardTargetType(string typeLine) (line 532)
