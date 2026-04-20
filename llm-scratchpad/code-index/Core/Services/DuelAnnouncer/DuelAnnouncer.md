# DuelAnnouncer.cs
Path: src/Core/Services/DuelAnnouncer/DuelAnnouncer.cs
Lines: 782

## Top-level comments
- Core class managing game event announcements to the screen reader; receives events from Harmony patch on UXEventQueue.EnqueuePending; applies privacy rules preventing opponent hand/library reveals.

## public partial class DuelAnnouncer (line 25)

### Fields
- public static DuelAnnouncer Instance { get; private set; } (line 27)
- private readonly IAnnouncementService _announcer (line 29)
- private bool _isActive (line 30)
- private readonly Dictionary<string, DuelEventType> _eventTypeMap (line 39)
- private string _lastAnnouncement (line 42) — dedup threshold check
- private DateTime _lastAnnouncementTime (line 43)
- private uint _localPlayerId (line 46)
- private ZoneNavigator _zoneNavigator (line 47)
- private BattlefieldNavigator _battlefieldNavigator (line 48)
- private DateTime _lastSpellResolvedTime (line 49) — read by Zones partial for spell vs land logic
- private readonly Dictionary<uint, bool> _commandZoneGrpIds (line 53) — cross-partial: set here, read by Commander
- private static readonly HashSet<string> _fieldLoggedLabels (line 56)
- private static readonly Regex ZoneNamePattern (line 59)
- private static readonly Regex ZoneCountPattern (line 60)
- private static readonly Regex LocalPlayerPattern (line 61)
- private static HashSet<string> _loggedEventTypes (line 214)
- private static readonly ConcurrentDictionary<(Type, string), MemberInfo> _reflectionCache (line 601)
- private static string _lastManaPool (line 486)

### Properties
- public static string CurrentManaPool (line 492)

### Methods
- public void SetZoneNavigator(ZoneNavigator navigator) (line 63)
- public void SetBattlefieldNavigator(BattlefieldNavigator navigator) (line 68)
- private void MarkNavigatorsDirty() (line 77)
- public DuelAnnouncer(IAnnouncementService announcer) (line 83)
- public void Activate(uint localPlayerId) (line 90)
- public void Deactivate() (line 115)
- public void OnTimerTimeout(bool isLocal, uint timeoutCount) (line 131)
- private IEnumerable<GameObject> EnumerateCDCsInHolder(string nameContains) (line 146)
- public void Update() (line 168) — flushes debounced phase announcements
- public int GetOpponentHandCount() (line 188)
- public int GetLocalLibraryCount() (line 197)
- public int GetOpponentLibraryCount() (line 206)
- public void OnGameEvent(object uxEvent) (line 219) — main event dispatch entry point
- private DuelEventType ClassifyEvent(object uxEvent) (line 252)
- private string BuildAnnouncement(DuelEventType eventType, object uxEvent) (line 258)
- private void LogEventFieldsOnce(object uxEvent, string label) (line 320)
- private void LogEventFields(object uxEvent, string label) (line 326)
- private string BuildRevealAnnouncement(object uxEvent) (line 374)
- private string BuildCountersAnnouncement(object uxEvent) (line 387)
- private string BuildGameEndAnnouncement(object uxEvent) (line 407)
- private T GetNestedPropertyValue<T>(object obj, string propertyName) (line 424)
- private string GetCardNameByInstanceId(uint instanceId) (line 439) — cached lookup across all zone holders
- private string HandleManaPoolEvent(object uxEvent) (line 494)
- private string ParseManaPool(object uxEvent) (line 526)
- private static MemberInfo LookupMember(Type type, string name) (line 606)
- private T GetFieldValue<T>(object obj, string fieldName) (line 613)
- private void TryUpdateLocalPlayerIdFromZoneString(string zoneStr) (line 629)
- private bool IsDuplicateAnnouncement(string announcement) (line 646)
- private AnnouncementPriority GetPriority(DuelEventType eventType) (line 653)
- private Dictionary<string, DuelEventType> BuildEventTypeMap() (line 675)

## DuelEventType (line 756)
Enum for event classification — 22 event types (Ignored, TurnChange, PhaseChange, ZoneTransfer, LifeChange, DamageDealt, CardRevealed, CountersChanged, GameEnd, Combat, TargetSelection, TargetConfirmed, ResolutionStarted, ResolutionEnded, CardModelUpdate, ZoneTransferGroup, CombatFrame, MultistepEffect, ManaPool, NPEDialog, NPEReminder, NPETooltip, NPEWarning).
