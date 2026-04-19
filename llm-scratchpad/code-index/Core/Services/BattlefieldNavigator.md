# BattlefieldNavigator.cs
Path: src/Core/Services/BattlefieldNavigator.cs
Lines: 847

## public class BattlefieldNavigator (line 18)

Does not extend BaseNavigator — sub-navigator invoked by DuelNavigator. Organizes battlefield into 6 rows by card type and ownership. Row shortcuts A/R/B (+Shift for enemy side), Shift+Up/Down to switch rows, Left/Right within row.

### Fields
- private readonly IAnnouncementService _announcer (line 20)
- private readonly ZoneNavigator _zoneNavigator (line 21)
- private Dictionary<BattlefieldRow, List<GameObject>> _rows (line 23)
- private BattlefieldRow _currentRow = BattlefieldRow.PlayerCreatures (line 24)
- private int _currentIndex = 0 (line 25)
- private bool _isActive (line 26)
- private bool _dirty (line 27)
- private CombatNavigator _combatNavigator (line 30)
- private GameObject _watchedCard (line 33) — after Enter click, watches this card for state change
- private string _watchedStateBefore (line 34)
- private float _watchStartTime (line 35)
- private const float WatchTimeoutSeconds = 3f (line 36)
- private const float WatchCheckIntervalSeconds = 0.1f (line 37)
- private float _lastWatchCheckTime (line 38)
- private static readonly BattlefieldRow[] RowOrder (line 41) — EnemyLands, EnemyNonCreatures, EnemyCreatures, PlayerCreatures, PlayerNonCreatures, PlayerLands

### Properties
- public bool IsActive => _isActive (line 50)
- public BattlefieldRow CurrentRow => _currentRow (line 51)

### Methods
- public BattlefieldNavigator(IAnnouncementService announcer, ZoneNavigator zoneNavigator) (line 53)
- public void SetCombatNavigator(CombatNavigator navigator) (line 68)
- public void MarkDirty() (line 77) — called by DuelAnnouncer when zone contents change
- private void RefreshIfDirty() (line 85)
- public void Activate() (line 109)
- public void Deactivate() (line 118)
- public bool HandleInput() (line 133)
- public void DiscoverAndCategorizeCards() (line 269) — uses DuelHolderCache for cached holder lookup
- private BattlefieldRow CategorizeCard(GameObject card) (line 375) — uses CardDetector.GetCardCategory
- public void NavigateToRow(BattlefieldRow row) (line 400)
- public bool NavigateToSpecificCard(GameObject card, bool announceRowChange) (line 429) — used by HotHighlightNavigator to sync on Tab
- private void NextRow() => MoveRow(1) (line 456)
- private void PreviousRow() => MoveRow(-1) (line 457)
- private void MoveRow(int direction) (line 463) — skips empty rows
- private void NextCard() (line 486)
- private void PreviousCard() (line 509)
- private void FirstCard() (line 532)
- private void LastCard() (line 554)
- public GameObject GetCurrentCard() (line 577)
- private void ActivateCurrentCard() (line 589) — clicks via SimulatePointerClick at card's screen position
- private void CheckWatchedCardState() (line 625) — announces new state only, stops after timeout or change
- private string GetCardStateSnapshot(GameObject card) (line 669) — combat state or selection state
- private string GetSelectionState(GameObject card) (line 681) — checks for "select"/"chosen"/"pick" child names
- private void AnnounceCurrentCard(bool includeRowName = false, AnnouncementPriority priority = AnnouncementPriority.Normal, bool isRowSwitch = false) (line 700)
- private void ClearEventSystemSelection() (line 755)
- public string GetLandSummary(BattlefieldRow landRow) (line 768) — total count + untapped grouped by name
- private string GetRowName(BattlefieldRow row) (line 828)

## public enum BattlefieldRow (line 838)

Row categories organized by card type and ownership (top-to-bottom screen order).

- EnemyLands (line 840) — Shift+A
- EnemyNonCreatures (line 841) — Shift+R (artifacts, enchantments, planeswalkers)
- EnemyCreatures (line 842) — Shift+B
- PlayerCreatures (line 843) — B
- PlayerNonCreatures (line 844) — R
- PlayerLands (line 845) — A
