using UnityEngine;

namespace AccessibleArena.Core.Models
{
    /// <summary>
    /// Information about a valid target during target selection.
    /// </summary>
    public class TargetInfo
    {
        /// <summary>
        /// The GameObject representing this target (card or player avatar).
        /// </summary>
        public GameObject GameObject { get; set; }

        /// <summary>
        /// Display name of the target (card name or "Opponent"/"You").
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Unique instance ID from IEntityView if available.
        /// </summary>
        public uint InstanceId { get; set; }

        /// <summary>
        /// The type of target (creature, player, permanent, etc.).
        /// </summary>
        public CardTargetType Type { get; set; }

        /// <summary>
        /// Additional details (power/toughness, life total, etc.).
        /// </summary>
        public string Details { get; set; }

        /// <summary>
        /// Whether this target belongs to the opponent.
        /// </summary>
        public bool IsOpponent { get; set; }

        /// <summary>
        /// Returns a formatted description for screen reader announcement.
        /// </summary>
        public string GetAnnouncement()
        {
            if (string.IsNullOrEmpty(Details))
                return Name;

            return $"{Name}, {Details}";
        }

        public override string ToString()
        {
            return $"{Name} ({Type}){(IsOpponent ? " [Opponent]" : "")}";
        }
    }

    /// <summary>
    /// Types of targets that can be selected.
    /// </summary>
    public enum CardTargetType
    {
        Unknown,
        Creature,
        Player,
        Permanent,
        Spell,
        Planeswalker,
        Artifact,
        Enchantment,
        Land
    }
}
