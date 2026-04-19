# TargetInfo.cs
Path: src/Core/Models/TargetInfo.cs
Lines: 72

## class TargetInfo (line 8)
### Properties
- public GameObject GameObject { get; set; } (line 13)
- public string Name { get; set; } (line 18)
- public uint InstanceId { get; set; } (line 23)
- public CardTargetType Type { get; set; } (line 28)
- public string Details { get; set; } (line 33)
- public bool IsOpponent { get; set; } (line 38)
### Methods
- public string GetAnnouncement() (line 43) — Note: returns Name alone when Details is null or empty, else "Name, Details"
- public override string ToString() (line 51)

## enum CardTargetType (line 60)
- Unknown (line 62)
- Creature (line 63)
- Player (line 64)
- Permanent (line 65)
- Spell (line 66)
- Planeswalker (line 67)
- Artifact (line 68)
- Enchantment (line 69)
- Land (line 70)
