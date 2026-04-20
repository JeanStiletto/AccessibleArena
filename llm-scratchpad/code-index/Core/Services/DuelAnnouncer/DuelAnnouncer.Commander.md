# DuelAnnouncer.Commander.cs
Path: src/Core/Services/DuelAnnouncer/DuelAnnouncer.Commander.cs
Lines: 198

## Top-level comments
- Commander format support (Brawl/Commander): tracks commander GrpIds from MatchManager at startup and zone transfer events; provides queries for opponent commander identity (stored in _commandZoneGrpIds).

## public partial class DuelAnnouncer (line 15)

### Fields
- private static PropertyInfo _mmProp (line 18) — GameManager.MatchManager (lazy cached)
- private static PropertyInfo _localPIProp (line 19) — MatchManager.LocalPlayerInfo
- private static PropertyInfo _opponentPIProp (line 20) — MatchManager.OpponentInfo
- private static PropertyInfo _commanderGrpIdsProp (line 21) — PlayerInfo.CommanderGrpIds
- private static bool _commanderReflectionInitialized (line 22)

### Methods
- public uint GetOpponentCommanderGrpId() (line 28) — queries _commandZoneGrpIds for opponent commander
- public CardInfo? GetOpponentCommanderInfo() (line 42)
- public string GetOpponentCommanderName() (line 52)
- public List<uint> GetAllOpponentCommanderGrpIds() (line 62) — supports partner commanders
- private void PopulateCommandersFromMatchManager() (line 78) — called from Activate(); seeds both local and opponent commanders before zone events arrive
- private void PopulateCommandersForPlayer(object matchManager, PropertyInfo playerInfoProp, bool isOpponent) (line 121)
- private static void InitializeCommanderReflection(object gameManager) (line 146) — lazy-loads reflection properties
