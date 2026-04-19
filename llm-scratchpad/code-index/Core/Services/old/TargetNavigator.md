# TargetNavigator.cs
Path: src/Core/Services/old/TargetNavigator.cs
Lines: 554

## Top-level comments
- Handles target selection during spells and abilities. Tab cycles through valid targets, Enter selects the current target, Backspace cancels.

## public class TargetNavigator (line 15)

### Fields
- private readonly IAnnouncementService _announcer (line 17)
- private readonly ZoneNavigator _zoneNavigator (line 18)
- private bool _isTargeting (line 20)
- private List<TargetInfo> _validTargets (line 21)
- private int _currentIndex = -1 (line 22)

### Properties
- public bool IsTargeting => _isTargeting (line 24)
- public int TargetCount => _validTargets.Count (line 25)
- public TargetInfo CurrentTarget (line 26)

### Methods
- public TargetNavigator(IAnnouncementService announcer, ZoneNavigator zoneNavigator) (line 31)
- public bool TryEnterTargetMode(bool requireValidTargets = true) (line 44) — Note: preferred entry point; performs guard checks
- public void EnterTargetMode() (line 90) — Note: legacy; prefer TryEnterTargetMode
- public void ExitTargetMode() (line 118)
- public bool HandleInput() (line 132)
- public void NextTarget() (line 161)
- public void PreviousTarget() (line 173)
- public void SelectCurrentTarget() (line 188) — Note: exits targeting mode immediately after selection; DuelNavigator may re-enter for multi-target spells
- public void CancelTargeting() (line 219)
- private void AnnounceCurrentTarget() (line 226) — Note: sets EventSystem focus and updates ZoneNavigator / CardInfoNavigator
- private ZoneType DetermineTargetZone(TargetInfo target) (line 266)
- private void DiscoverValidTargets() (line 317)
- private void DiscoverPlayerTargets() (line 378)
- private (GameObject highlightRoot, GameObject clickable) FindPlayerMatchTimer(bool isOpponent) (line 424) — Note: HotHighlight lives on MatchTimer; clickable is HoverArea/Icon
- private TargetInfo CreateTargetFromCard(GameObject cardObj) (line 498)
- private CardTargetType DetermineCardTargetType(string typeLine) (line 532) — Note: English-only type-line matching
