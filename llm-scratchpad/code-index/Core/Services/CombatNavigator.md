# CombatNavigator.cs

Handles combat phase navigation.
During Declare Attackers phase:
- Space presses "All Attack" or "X Attack" button
- Backspace presses "No Attacks" button
- Announces attacker selection state when navigating battlefield cards
During Declare Blockers phase:
- Space presses confirm button (X Blocker / Next)
- Backspace presses "No Blocks" or "Cancel Blocks" button
- Tracks selected blockers and announces combined power/toughness

## Class: CombatNavigator (line 22)

### Fields (line 24-46)
- readonly IAnnouncementService _announcer (line 24)
- readonly DuelAnnouncer _duelAnnouncer (line 25)
- HashSet<int> _previousSelectedBlockerIds (line 28)
  Note: Track selected blockers by instance ID for change detection
- Dictionary<int, GameObject> _previousSelectedBlockerObjects (line 29)
- HashSet<int> _previousAssignedBlockerIds (line 32)
  Note: Track assigned blockers (IsBlocking) by instance ID for change detection
- Dictionary<int, GameObject> _previousAssignedBlockerObjects (line 33)
- bool _wasInBlockersPhase (line 36)
  Note: Track if we were in blockers phase last frame (to reset on phase change)
- bool _debugAttackerCards (line 47)
  Note: Debug flag for logging attacker card children (set to true for debugging)

### Properties (line 38)
- bool IsInCombatPhase (line 38)

### Constructor (line 40)
- CombatNavigator(IAnnouncementService announcer, DuelAnnouncer duelAnnouncer) (line 40)

### Attacker Detection (line 49-109)
- bool IsCreatureAttacking(GameObject card) (line 53)
  Note: Checks if creature is currently selected/declared as attacker; uses model data first, falls back to UI child scan for transitional states
- void LogAttackerRelevantChildren(GameObject card) (line 88)
  Note: Debug: logs children related to attack state within CombatIcon_AttackerFrame

### Blocker Detection (line 111-163)
- bool IsCreatureBlocking(GameObject card) (line 115)
  Note: Checks if creature is currently assigned as blocker; uses model data first, falls back to UI child scan
- bool IsCreatureSelectedAsBlocker(GameObject card) (line 140)
  Note: Checks if creature is currently selected (highlighted) as potential blocker; different from IsCreatureBlocking

### Blocker Discovery (line 165-215)
- List<GameObject> FindSelectedBlockers() (line 169)
  Note: Finds all creatures currently selected as blockers; returns cards with both blocker frame and selection highlight
- List<GameObject> FindAssignedBlockers() (line 196)
  Note: Finds all creatures currently assigned as blockers; returns cards with IsBlocking indicator present

### Power/Toughness Parsing (line 217-259)
- (int power, int toughness) GetPowerToughness(GameObject card) (line 221)
  Note: Parses power and toughness from card; returns (0, 0) if not a creature or can't parse
- (int totalPower, int totalToughness) CalculateCombinedStats(List<GameObject> blockers) (line 246)
  Note: Calculates combined power and toughness for a list of blockers

### Blocker Selection Tracking (line 261-394)
- void UpdateBlockerSelection() (line 266)
  Note: Updates blocker selection tracking and announces changes; should be called each frame during declare blockers phase; tracks both selected (potential) and assigned (confirmed) blockers

### Combat State Announcement (line 396-540)
- string GetCombatStateText(GameObject card) (line 401)
  Note: Gets text to append to card announcement indicating active states; uses model data for attacking/blocking/tapped with relationship names, UI scan for frames and selection
- string GetBlockingText(GameObject card) (line 484)
  Note: Resolves BlockingIds to card names for blocker card; returns e.g. "blocking Angel" or "blocking Angel and Dragon"
- string GetBlockedByText(GameObject card) (line 515)
  Note: Resolves BlockedByIds to card names for attacker card; returns e.g. "blocked by Cat" or "blocked by Cat and Bear"

### Input Handling (line 542-588)
- bool HandleInput() (line 546)
  Note: Handles input during combat phases; returns true if input was consumed; tracks blocker selection per frame

### Button Click Methods (line 590-648)
- bool TryClickPrimaryButton() (line 595)
  Note: Finds and clicks primary prompt button; language-agnostic (identifies by GO name, announces localized text); returns true if button found and clicked
- bool TryClickSecondaryButton() (line 625)
  Note: Finds and clicks secondary prompt button; language-agnostic (identifies by GO name, announces localized text); returns true if button found and clicked

### Button Discovery (line 650-706)
- GameObject FindPromptButton(bool isPrimary) (line 656)
  Note: Finds prompt button by type; language-agnostic (uses GO name pattern, not button text); when multiple buttons match, prefers one with full parent CanvasGroup alpha (not fading out)
- float GetParentCanvasGroupAlpha(GameObject obj) (line 692)
  Note: Gets minimum CanvasGroup alpha from button's parent hierarchy; returns 1.0 if no CanvasGroup found; stale buttons from previous phases typically have parent fading to alpha 0

### Utility Methods (line 708-734)
- bool IsInEmotePanel(GameObject obj) (line 713)
  Note: Checks if GameObject is part of emote/communication panel UI; used to filter out emote buttons from combat button searches
