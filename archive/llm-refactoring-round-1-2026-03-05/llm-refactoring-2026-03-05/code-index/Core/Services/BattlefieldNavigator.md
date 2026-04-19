# BattlefieldNavigator.cs

Handles battlefield navigation organized into 6 logical rows by card type and ownership.
Row shortcuts: A (Your Lands), R (Your Non-creatures), B (Your Creatures)
               Shift+A (Enemy Lands), Shift+R (Enemy Non-creatures), Shift+B (Enemy Creatures)

## Class: BattlefieldNavigator (line 18)

### Fields
- readonly IAnnouncementService _announcer (line 20)
- readonly ZoneNavigator _zoneNavigator (line 21)
- Dictionary<BattlefieldRow, List<GameObject>> _rows (line 23)
- BattlefieldRow _currentRow (line 24)
- int _currentIndex (line 25)
- bool _isActive (line 26)
- bool _dirty (line 27)
- CombatNavigator _combatNavigator (line 30)
- GameObject _watchedCard (line 33)
- string _watchedStateBefore (line 34)
- float _watchStartTime (line 35)

### Constants
- const float WatchTimeoutSeconds = 3f (line 36)
- static readonly BattlefieldRow[] RowOrder (line 43)

### Properties
- bool IsActive (line 52)
- BattlefieldRow CurrentRow (line 53)

### Constructor
- BattlefieldNavigator(IAnnouncementService announcer, ZoneNavigator zoneNavigator) (line 55)

### Setup Methods
- void SetCombatNavigator(CombatNavigator navigator) (line 70)
- void MarkDirty() (line 79)
- void RefreshIfDirty() (line 87)

### Lifecycle Methods
- void Activate() (line 111)
- void Deactivate() (line 119)

### Input Handling
- bool HandleInput() (line 135)

### Card Discovery & Categorization
- void DiscoverAndCategorizeCards() (line 271)
  Note: Uses DuelHolderCache for cached holder lookup
- BattlefieldRow CategorizeCard(GameObject card) (line 334)
  Note: Uses CardDetector.GetCardCategory for efficient single-lookup detection

### Navigation Methods
- void NavigateToRow(BattlefieldRow row) (line 361)
- bool NavigateToSpecificCard(GameObject card, bool announceRowChange) (line 389)
  Note: Used by HotHighlightNavigator to sync battlefield position on Tab
- void NextRow() (line 420)
- void PreviousRow() (line 445)
- void NextCard() (line 469)
- void PreviousCard() (line 492)
- void FirstCard() (line 515)
- void LastCard() (line 538)
- GameObject GetCurrentCard() (line 560)

### Card Activation
- void ActivateCurrentCard() (line 572)
- void CheckWatchedCardState() (line 608)
  Note: Per-frame check after Enter click on a card, watches for state change
- string GetCardStateSnapshot(GameObject card) (line 647)
- string GetSelectionState(GameObject card) (line 659)

### Announcement Methods
- void AnnounceCurrentCard(bool includeRowName, AnnouncementPriority priority) (line 678)

### Helper Methods
- void ClearEventSystemSelection() (line 718)
- string GetLandSummary(BattlefieldRow landRow) (line 731)
  Note: Builds summary with total count + untapped lands grouped by name
- string GetRowName(BattlefieldRow row) (line 791)

## Enum: BattlefieldRow (line 801)
- EnemyLands (line 803)
- EnemyNonCreatures (line 804)
- EnemyCreatures (line 805)
- PlayerCreatures (line 806)
- PlayerNonCreatures (line 807)
- PlayerLands (line 808)
