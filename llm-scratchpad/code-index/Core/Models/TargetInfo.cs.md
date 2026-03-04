# TargetInfo.cs Code Index

## File Overview
Model for valid targets during target selection in duels.

## Class: TargetInfo (line 8)

### Public Properties
- public GameObject GameObject { get; set; } (line 13)
  // The GameObject representing this target (card or player avatar)

- public string Name { get; set; } (line 17)
  // Display name of the target (card name or "Opponent"/"You")

- public uint InstanceId { get; set; } (line 21)
  // Unique instance ID from IEntityView if available

- public CardTargetType Type { get; set; } (line 25)
  // The type of target (creature, player, permanent, etc.)

- public string Details { get; set; } (line 29)
  // Additional details (power/toughness, life total, etc.)

- public bool IsOpponent { get; set; } (line 33)
  // Whether this target belongs to the opponent

### Public Methods
- public string GetAnnouncement() (line 41)
  // Returns a formatted description for screen reader announcement

- public override string ToString() (line 51)

## Enum: CardTargetType (line 60)

### Values
- Unknown (line 62)
- Creature (line 63)
- Player (line 64)
- Permanent (line 65)
- Spell (line 66)
- Planeswalker (line 67)
- Artifact (line 68)
- Enchantment (line 69)
- Land (line 70)
