# DuelAnnouncer.cs

Announces duel events to the screen reader. Receives events from the Harmony patch on UXEventQueue.EnqueuePending.

PRIVACY RULES:
- Only announce publicly visible information
- Never reveal opponent's hand contents
- Never reveal library contents (top of deck, etc.)
- Only announce what a sighted player could see

```
public class DuelAnnouncer (line 24)
  public static DuelAnnouncer Instance { get; private set; } (line 26)

  private readonly IAnnouncementService _announcer (line 28)
  private bool _isActive (line 29)
  private readonly Dictionary<string, DuelEventType> _eventTypeMap (line 31)
  private readonly Dictionary<string, int> _zoneCounts (line 32)
  private string _lastAnnouncement (line 34)
  private DateTime _lastAnnouncementTime (line 35)
  private const float DUPLICATE_THRESHOLD_SECONDS (line 36)
  private uint _localPlayerId (line 38)
  private ZoneNavigator _zoneNavigator (line 42)
  private BattlefieldNavigator _battlefieldNavigator (line 43)
  private DateTime _lastSpellResolvedTime (line 44)
  private int _userTurnCount (line 47)
  private bool _isUserTurn (line 50)
  private string _currentPhase (line 53)
  private string _currentStep (line 54)
  private readonly HashSet<uint> _commandZoneGrpIds (line 57)

  // Pre-compiled regex patterns for zone event parsing
  private static readonly Regex ZoneNamePattern (line 60)
  private static readonly Regex ZoneCountPattern (line 61)
  private static readonly Regex LocalPlayerPattern (line 62)

  private string _pendingPhaseAnnouncement (line 65)
  private float _phaseDebounceTimer (line 66)
  private const float PHASE_DEBOUNCE_SECONDS (line 67)
  private float _lastPhaseChangeTime (line 70)

  public float TimeSinceLastPhaseChange => UnityEngine.Time.time - _lastPhaseChangeTime (line 71)
  public bool IsInDeclareAttackersPhase => _currentPhase == "Combat" && _currentStep == "DeclareAttack" (line 76)
  public bool IsInDeclareBlockersPhase => _currentPhase == "Combat" && _currentStep == "DeclareBlock" (line 81)

  // Gets the current turn and phase information for announcement (T key)
  public string GetTurnPhaseInfo() (line 87)

  // Gets a human-readable description of the current phase and step
  private string GetPhaseDescription() (line 100)

  // Returns true if a spell resolved or permanent entered battlefield within the last specified milliseconds
  public bool DidSpellResolveRecently(int withinMs = 500) (line 109)

  public void SetZoneNavigator(ZoneNavigator navigator) (line 114)
  public void SetBattlefieldNavigator(BattlefieldNavigator navigator) (line 119)

  // Marks both zone and battlefield navigators as dirty so they refresh on next user navigation
  private void MarkNavigatorsDirty() (line 128)

  public DuelAnnouncer(IAnnouncementService announcer) (line 134)
  public void Activate(uint localPlayerId) (line 141)
  public void Deactivate() (line 156)

  // Yields active CDC child GameObjects from a cached holder
  private IEnumerable<GameObject> EnumerateCDCsInHolder(string nameContains) (line 171)

  private static readonly string[] AllZoneHolders (line 184)

  // Call each frame to flush debounced phase announcements
  public void Update() (line 193)

  #region Zone Count Accessors (line 207)
  public int GetOpponentHandCount() (line 213)
  public int GetLocalLibraryCount() (line 223)
  public int GetOpponentLibraryCount() (line 231)
  public uint GetOpponentCommanderGrpId() (line 242)
  public CardInfo? GetOpponentCommanderInfo() (line 263)
  public string GetOpponentCommanderName() (line 273)
  private uint FindOurCommanderGrpId() (line 284)
  #endregion

  private static HashSet<string> _loggedEventTypes (line 318)

  // Called by the Harmony patch when a game event is enqueued
  public void OnGameEvent(object uxEvent) (line 323)

  private DuelEventType ClassifyEvent(object uxEvent) (line 355)
  private string BuildAnnouncement(DuelEventType eventType, object uxEvent) (line 361)

  #region Announcement Builders (line 406)
  private string BuildTurnChangeAnnouncement(object uxEvent) (line 408)
  private string BuildZoneTransferAnnouncement(object uxEvent) (line 443)
  private string HandleUpdateZoneEvent(object uxEvent) (line 451)
  // ... additional announcement builders continue from line 500 onward
  #endregion
```

Note: File is 29123 tokens, only first 500 lines indexed. Contains additional announcement builders, event handlers, and utility methods for tracking game state and announcing events.
