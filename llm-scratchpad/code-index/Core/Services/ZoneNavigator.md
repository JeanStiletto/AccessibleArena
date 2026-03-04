# ZoneNavigator.cs

## Overview
Handles navigation through game zones and cards within zones.
Zone shortcuts: C (Hand), B (Battlefield), G (Graveyard), X (Exile), S (Stack)
Opponent zones: Shift+G (Opponent Graveyard), Shift+X (Opponent Exile)
Card navigation: Left/Right arrows to move between cards in current zone.

## Enum: ZoneOwner (line 17)
Tracks which navigator set the current zone. Higher priority owners can override lower priority settings.
Priority order: HotHighlightNavigator > BattlefieldNavigator > ZoneNavigator
- None
- ZoneNavigator
- BattlefieldNavigator
- HighlightNavigator
- (DEPRECATED: TargetNavigator)

## Class: ZoneNavigator (line 39)

### State
- private readonly IAnnouncementService _announcer (line 41)
- private Dictionary<ZoneType, ZoneInfo> _zones (line 43)
- private ZoneType _currentZone (line 44)
- private ZoneOwner _zoneOwner (line 45)
- private int _cardIndexInZone (line 46)
- private bool _isActive (line 47)
- private bool _dirty (line 48)
- private static readonly Dictionary<string, ZoneType> ZoneHolderPatterns (line 51)

### Properties
- public bool IsActive (line 66)
- public ZoneType CurrentZone (line 67)
- public ZoneOwner CurrentZoneOwner (line 68)
- public int CardCount (line 69)
- public int HandCardCount (line 70)
- public int StackCardCount (line 75)

### Methods
- public int GetFreshStackCount() (line 82)
  - Gets fresh stack count by scanning cached holder
- public uint GetLocalPlayerId() (line 103)
  - Uses OwnerId from LocalHand or LocalLibrary zone
- public static void SetFocusedGameObject(GameObject gameObject, string caller) (line 130)
  - All navigators should use this for EventSystem focus changes
- public void SetCurrentZone(ZoneType zone, string caller = null) (line 151)
  - Used by BattlefieldNavigator, HotHighlightNavigator, etc.
- private ZoneOwner ParseZoneOwner(string caller) (line 170)
- private void ReclaimZoneOwnership() (line 189)
  - Prevents stale card activation after user navigation

### Navigator References
- private CombatNavigator _combatNavigator (line 203)
- private HotHighlightNavigator _hotHighlightNavigator (line 206)
- public void SetCombatNavigator(CombatNavigator navigator) (line 216)
- public void SetHotHighlightNavigator(HotHighlightNavigator navigator) (line 224)

### Lifecycle
- public ZoneNavigator(IAnnouncementService announcer) (line 208)
- public void MarkDirty() (line 233)
  - Called by DuelAnnouncer when zone contents change
- public void Activate() (line 241)
- public void Deactivate() (line 249)

### Input Handling
- public bool HandleInput() (line 262)
  - Returns true if input was consumed

### Zone Discovery
- public void DiscoverZones() (line 448)
  - Uses DuelHolderCache for cached holder lookups
- private void PopulateCommandZoneFromHand() (line 482)
  - Commanders are in hand holder visually, not CommandCardHolder
- private void DiscoverCardsInZone(ZoneInfo zone) (line 504)
- private void RefreshIfDirty() (line 558)

### Zone Navigation
- public void NavigateToZone(ZoneType zone) (line 585)
- public bool NavigateToSpecificCard(ZoneType zone, GameObject card, bool announceZoneChange) (line 620)
  - Used by HotHighlightNavigator to sync zone position on Tab
- private void NavigateToLibraryZone(ZoneType libraryZone) (line 970)
  - Announces total count, enters navigation if revealed cards exist
- private int GetLibraryTotalCount(ZoneType libraryZone) (line 1010)

### Card Navigation
- public void NextCard() (line 647)
- public void PreviousCard() (line 669)
- public void FirstCard() (line 690)
- public void LastCard() (line 710)

### Card Access
- public List<GameObject> GetCardsInZone(ZoneType zone) (line 731)
- public GameObject GetCurrentCard() (line 741)

### Card Activation
- public void ActivateCurrentCard() (line 756)
  - Hand/Command/Library: two-click approach
  - Other zones: click simulation

### Utilities
- public void LogZoneSummary() (line 816)
- private void AnnounceCurrentCard(bool includeZoneName = false, AnnouncementPriority priority = AnnouncementPriority.Normal) (line 825)
- private static readonly Dictionary<string, ZoneType> GameZoneToModZone (line 918)
- private string GetOriginZoneText(GameObject card) (line 934)
  - E.g., commander in Command zone shown in hand
- private bool HasCardsInCurrentZone() (line 950)
- private void ClearEventSystemSelection() (line 956)

### Hidden Zone Count Announcements
- private int GetZoneCardCount(ZoneType zone) (line 1032)
- private void AnnounceOpponentHandCount() (line 1047)
- private void AnnounceOpponentCommander() (line 1063)

### Helpers
- private string GetZoneName(ZoneType zone) (line 1103)
- private int ParseZoneId(string name) (line 1108)
- private int ParseOwnerId(string name) (line 1114)

## Enum: ZoneType (line 1124)
Hand, Battlefield, Graveyard, Exile, Stack, Library, Command, OpponentHand, OpponentGraveyard, OpponentLibrary, OpponentExile, OpponentCommand, Browser

## Class: ZoneInfo (line 1144)
- ZoneType Type
- GameObject Holder
- int ZoneId
- int OwnerId
- List<GameObject> Cards
