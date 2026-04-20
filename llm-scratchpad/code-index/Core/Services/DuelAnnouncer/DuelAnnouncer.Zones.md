# DuelAnnouncer.Zones.cs
Path: src/Core/Services/DuelAnnouncer/DuelAnnouncer.Zones.cs
Lines: 644

## Top-level comments
- Zone event handling: tracks card counts per zone, processes zone transfers (plays, deaths, mills, bounces), announces zone-specific state changes; reads _lastSpellResolvedTime (set in core) to distinguish land plays from spell resolution.

## public partial class DuelAnnouncer (line 15)

### Fields
- private readonly Dictionary<string, int> _zoneCounts (line 17) — read from core and other partials
- private static readonly HashSet<string> BasicLandNames (line 20) — multilingual land name lookup

### Methods
- private string BuildZoneTransferAnnouncement(object uxEvent) (line 30)
- private string HandleUpdateZoneEvent(object uxEvent) (line 38) — parses zone event, updates counts, marks navigators dirty on change
- private string HandleZoneTransferGroup(object uxEvent) (line 138)
- private string ProcessZoneTransfer(object transfer) (line 187) — dispatches to zone-type handlers; tracks commander GrpIds
- private string ProcessBattlefieldEntry(string fromZone, string reason, string cardName, uint grpId, object cardInstance, bool isOpponent) (line 354)
- private string GetAttachedToName(object cardInstance) (line 434) — finds parent card for auras/equipment
- private string ProcessGraveyardEntry(string fromZone, string reason, string cardName, string ownerPrefix, object transfer) (line 486)
- private string ProcessExileEntry(string fromZone, string reason, string cardName, string ownerPrefix, object transfer) (line 527)
- private string ProcessHandEntry(string fromZone, string reason, string cardName, bool isOpponent) (line 563)
- private bool IsLandByGrpId(uint grpId, object card) (line 604) — checks IsBasicLand, IsLandButNotBasic properties, falls back to name check
