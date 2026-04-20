# DuelAnnouncer.Combat.cs
Path: src/Core/Services/DuelAnnouncer/DuelAnnouncer.Combat.cs
Lines: 554

## Top-level comments
- Combat announcements: tracks creature damage, announces attacker declarations, processes combat damage frames, correlates damage with source cards.

## public partial class DuelAnnouncer (line 15)

### Fields
- private Dictionary<uint, uint> _creatureDamage (line 18) — tracks damage per instance ID
- private class DamageInfo (line 21) — holds SourceName, TargetName, Amount

### Properties
- public bool IsInDeclareAttackersPhase (line 31)
- public bool IsInDeclareBlockersPhase (line 36)

### Methods
- private string BuildDamageAnnouncement(object uxEvent) (line 38)
- private string GetDamageTargetName(uint targetPlayerId, uint targetInstanceId) (line 87)
- private string GetDamageSourceName(object uxEvent) (line 106)
- private string GetDamageFlags(object uxEvent) (line 151)
- private List<string> GetAttackingCreaturesInfo() (line 176) — enumerates attacking creatures from battlefield
- private string BuildCombatAnnouncement(object uxEvent) (line 210)
- private string BuildAttackerDeclaredAnnouncement(object uxEvent) (line 243)
- private (int power, int toughness, bool isOpponent) GetCardPowerToughnessAndOwnerByInstanceId(uint instanceId) (line 287) — reflects StringBackedInt properties
- private string HandleCardModelUpdate(object uxEvent) (line 330)
- private string HandleCombatFrame(object uxEvent) (line 380) — parses combat branch chains for multi-target damage
- private DamageInfo ExtractDamageInfo(object damageEvent) (line 471)
- private List<DamageInfo> ExtractDamageChain(object branch) (line 516) — follows _nextBranch for blocker chains
