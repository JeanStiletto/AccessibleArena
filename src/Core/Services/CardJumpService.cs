using System.Collections.Generic;
using UnityEngine;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Resolves related InstanceIds for a focused card (attachments + targets) and
    /// maps an InstanceId back to the GameObject that hosts it in any duel zone.
    /// Backs the J hotkey in DuelNavigator.
    /// </summary>
    public static class CardJumpService
    {
        public struct JumpLocation
        {
            public GameObject Card;
            public ZoneType Zone;
        }

        private static readonly (string Holder, ZoneType Zone)[] HolderScan = new[]
        {
            ("BattlefieldCardHolder", ZoneType.Battlefield),
            ("StackCardHolder", ZoneType.Stack),
            ("LocalGraveyard", ZoneType.Graveyard),
            ("OpponentGraveyard", ZoneType.OpponentGraveyard),
            ("ExileCardHolder", ZoneType.Exile),
            ("OpponentExile", ZoneType.OpponentExile),
            ("LocalHand", ZoneType.Hand),
            ("OpponentHand", ZoneType.OpponentHand),
            ("CommandCardHolder", ZoneType.Command),
            ("LocalLibrary", ZoneType.Library),
            ("OpponentLibrary", ZoneType.OpponentLibrary),
        };

        /// <summary>
        /// Scans every duel holder for a card whose Model.InstanceId matches.
        /// Returns the top-most card GameObject and its zone, or null if not found
        /// (e.g. the target is in the library or has left play).
        /// </summary>
        public static JumpLocation? ResolveInstanceId(uint instanceId)
        {
            if (instanceId == 0) return null;

            foreach (var (holderName, zone) in HolderScan)
            {
                var holder = DuelHolderCache.GetHolder(holderName);
                if (holder == null) continue;

                var seen = new HashSet<int>();
                foreach (Transform child in holder.GetComponentsInChildren<Transform>(true))
                {
                    if (child == null || !child.gameObject.activeInHierarchy) continue;
                    var go = child.gameObject;
                    if (!CardDetector.IsCard(go)) continue;

                    // Skip cards nested inside another already-seen card (battlefield stacks).
                    bool nested = false;
                    Transform ancestor = go.transform.parent;
                    while (ancestor != null && ancestor != holder.transform)
                    {
                        if (seen.Contains(ancestor.gameObject.GetInstanceID())) { nested = true; break; }
                        ancestor = ancestor.parent;
                    }
                    if (nested) continue;
                    seen.Add(go.GetInstanceID());

                    if (CardStateProvider.GetCardInstanceId(go) == instanceId)
                        return new JumpLocation { Card = go, Zone = zone };
                }
            }
            return null;
        }

        /// <summary>
        /// Builds the ordered cycle of related InstanceIds for a focused card.
        /// Order: attached-to parent first, then attachments (children), then cards
        /// this targets, then cards targeting this. Duplicates removed.
        /// </summary>
        public static List<uint> GetRelatedInstanceIds(GameObject focusedCard)
        {
            var result = new List<uint>();
            if (focusedCard == null) return result;

            var cdc = CardModelProvider.GetDuelSceneCDC(focusedCard);
            if (cdc == null) return result;
            var model = CardModelProvider.GetCardModel(cdc);
            if (model == null) return result;

            uint myId = CardStateProvider.GetCardInstanceId(focusedCard);
            var seen = new HashSet<uint>();
            if (myId != 0) seen.Add(myId);

            uint parentId = CardStateProvider.GetAttachedToId(model);
            if (parentId != 0 && seen.Add(parentId))
                result.Add(parentId);

            foreach (var (id, _, _) in CardStateProvider.GetAttachments(focusedCard))
                if (id != 0 && seen.Add(id))
                    result.Add(id);

            foreach (var id in CardStateProvider.GetTargetIds(model))
                if (id != 0 && seen.Add(id))
                    result.Add(id);

            foreach (var id in CardStateProvider.GetTargetedByIds(model))
                if (id != 0 && seen.Add(id))
                    result.Add(id);

            return result;
        }
    }
}
