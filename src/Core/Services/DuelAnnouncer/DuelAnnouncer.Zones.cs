using MelonLoader;
using AccessibleArena.Core.Utils;
using AccessibleArena.Core.Models;
using System;
using System.Collections.Generic;
using UnityEngine;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    public partial class DuelAnnouncer
    {
        private readonly Dictionary<string, int> _zoneCounts = new Dictionary<string, int>();

        // Basic land names in all supported languages (static to avoid per-call allocation)
        private static readonly HashSet<string> BasicLandNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Plains", "Island", "Swamp", "Mountain", "Forest",           // English
            "Ebene", "Insel", "Sumpf", "Gebirge", "Wald",               // German
            "Plaine", "Île", "Marais", "Montagne", "Forêt",             // French
            "Llanura", "Isla", "Pantano", "Montaña", "Bosque",          // Spanish
            "Pianura", "Isola", "Palude", "Montagna", "Foresta",        // Italian
            "Planície", "Ilha", "Pântano", "Montanha", "Floresta"       // Portuguese
        };

        private string BuildZoneTransferAnnouncement(object uxEvent)
        {
            var typeName = uxEvent.GetType().Name;
            if (typeName == "UpdateZoneUXEvent")
                return HandleUpdateZoneEvent(uxEvent);
            return null;
        }

        // ---- Mass zone-transfer aggregation (board wipes, mass mill/discard, combat deaths) ----

        // Verbs we count-and-list when several cards leave for the same reason+owner in one batch.
        private enum RemovalVerb { Died, Destroyed, Sacrificed, Exiled, PutToGraveyard, Milled, Discarded }

        // Result of processing a single zone transfer: either a ready-made line (Text) or an
        // aggregatable removal (Verb/IsOpponent/CardName) that gets bucketed and counted.
        private class TransferOutcome
        {
            public string Text;
            public bool Aggregatable;
            public RemovalVerb Verb;
            public bool IsOpponent;
            public string CardName;
        }

        // Reserves a removal bucket's position in first-seen output order.
        private class BucketRef
        {
            public string Key;
            public BucketRef(string key) { Key = key; }
        }

        private static TransferOutcome OutcomeText(string text) =>
            string.IsNullOrEmpty(text) ? null : new TransferOutcome { Text = text };

        private static TransferOutcome OutcomeRemoval(RemovalVerb verb, bool isOpponent, string cardName) =>
            string.IsNullOrEmpty(cardName) ? null
            : new TransferOutcome { Aggregatable = true, Verb = verb, IsOpponent = isOpponent, CardName = cardName };

        // Flattens a UX event into its individual ZoneTransferUXEvents. Handles both a bare
        // ZoneTransferUXEvent and a ZoneTransferGroup wrapping a _zoneTransfers list.
        private void CollectTransfers(object uxEvent, List<object> into)
        {
            if (uxEvent == null) return;
            string typeName = uxEvent.GetType().Name;
            if (typeName == "ZoneTransferUXEvent")
            {
                into.Add(uxEvent);
                return;
            }
            if (typeName == "ZoneTransferGroup")
            {
                var list = GetFieldValue<object>(uxEvent, "_zoneTransfers");
                if (list is System.Collections.IEnumerable e)
                    foreach (var t in e) if (t != null) into.Add(t);
            }
        }

        // Turns per-transfer outcomes into announcement lines: aggregatable removals are bucketed
        // by (verb, owner) and rendered as "[owner] N <verb>: name1, name2" when a bucket holds 2+,
        // or the single-card line when it holds one. Non-removal lines pass through in first-seen
        // order; each bucket renders at the position of its first member.
        private List<string> AggregateOutcomes(List<TransferOutcome> outcomes)
        {
            var result = new List<string>();
            if (outcomes == null || outcomes.Count == 0) return result;

            var buckets = new Dictionary<string, List<string>>();
            var bucketVerb = new Dictionary<string, RemovalVerb>();
            var bucketOpp = new Dictionary<string, bool>();
            var slots = new List<object>();

            foreach (var o in outcomes)
            {
                if (o == null) continue;
                if (o.Aggregatable)
                {
                    string key = ((int)o.Verb) + "|" + (o.IsOpponent ? "1" : "0");
                    if (!buckets.TryGetValue(key, out var names))
                    {
                        names = new List<string>();
                        buckets[key] = names;
                        bucketVerb[key] = o.Verb;
                        bucketOpp[key] = o.IsOpponent;
                        slots.Add(new BucketRef(key));
                    }
                    names.Add(o.CardName);
                }
                else if (!string.IsNullOrEmpty(o.Text))
                {
                    slots.Add(o.Text);
                }
            }

            foreach (var slot in slots)
            {
                if (slot is BucketRef br)
                {
                    var names = buckets[br.Key];
                    string ownerPrefix = bucketOpp[br.Key] ? Strings.Duel_OwnerPrefix_Opponent : "";
                    result.Add(names.Count == 1
                        ? SingleRemovalText(bucketVerb[br.Key], ownerPrefix, names[0])
                        : ManyRemovalText(bucketVerb[br.Key], ownerPrefix, names.Count, string.Join(", ", names)));
                }
                else
                {
                    result.Add((string)slot);
                }
            }
            return result;
        }

        private string SingleRemovalText(RemovalVerb verb, string ownerPrefix, string name)
        {
            switch (verb)
            {
                case RemovalVerb.Died: return Strings.Duel_Died(ownerPrefix, name);
                case RemovalVerb.Destroyed: return Strings.Duel_Destroyed(ownerPrefix, name);
                case RemovalVerb.Sacrificed: return Strings.Duel_Sacrificed(ownerPrefix, name);
                case RemovalVerb.Exiled: return Strings.Duel_Exiled(ownerPrefix, name);
                case RemovalVerb.Milled: return Strings.Duel_Milled(ownerPrefix, name);
                case RemovalVerb.Discarded: return Strings.Duel_Discarded(ownerPrefix, name);
                default: return Strings.Duel_WentToGraveyard(ownerPrefix, name);
            }
        }

        private string ManyRemovalText(RemovalVerb verb, string ownerPrefix, int count, string names)
        {
            switch (verb)
            {
                case RemovalVerb.Died: return Strings.Duel_DiedMany(ownerPrefix, count, names);
                case RemovalVerb.Destroyed: return Strings.Duel_DestroyedMany(ownerPrefix, count, names);
                case RemovalVerb.Sacrificed: return Strings.Duel_SacrificedMany(ownerPrefix, count, names);
                case RemovalVerb.Exiled: return Strings.Duel_ExiledMany(ownerPrefix, count, names);
                case RemovalVerb.Milled: return Strings.Duel_MilledMany(ownerPrefix, count, names);
                case RemovalVerb.Discarded: return Strings.Duel_DiscardedMany(ownerPrefix, count, names);
                default: return Strings.Duel_PutToGraveyardMany(ownerPrefix, count, names);
            }
        }

        // Walks a CombatFrame DamageBranch chain and collects the death/removal zone transfers the
        // game nested in its leaf events (combat-damage deaths are pulled out of the normal
        // zone-transfer pipeline into the CombatFrame, so this is the only place to read them).
        private void CollectBranchRemovals(object branch, List<TransferOutcome> outcomes)
        {
            var current = branch;
            int depth = 0;
            while (current != null && depth < 10)
            {
                var leaves = GetFieldValue<object>(current, "_leafEvents");
                if (leaves is System.Collections.IEnumerable le)
                {
                    foreach (var leaf in le)
                    {
                        if (leaf == null) continue;
                        var transfers = new List<object>();
                        CollectTransfers(leaf, transfers);
                        foreach (var t in transfers)
                        {
                            var outcome = ProcessZoneTransfer(t);
                            if (outcome != null) outcomes.Add(outcome);
                        }
                    }
                }
                current = GetFieldValue<object>(current, "_nextBranch");
                depth++;
            }
        }

        private string HandleUpdateZoneEvent(object uxEvent)
        {
            var zoneField = uxEvent.GetType().GetField("_zone", PrivateInstance);
            if (zoneField == null) return null;

            var zoneObj = zoneField.GetValue(uxEvent);
            if (zoneObj == null) return null;

            string zoneStr = zoneObj.ToString();

            // Try to auto-correct local player ID from zone strings containing "(LocalPlayer)"
            TryUpdateLocalPlayerIdFromZoneString(zoneStr);

            bool isLocal = zoneStr.Contains("LocalPlayer") || (!zoneStr.Contains("Opponent") && zoneStr.Contains("Player,"));
            bool isOpponent = zoneStr.Contains("Opponent");

            var zoneMatch = ZoneNamePattern.Match(zoneStr);
            var countMatch = ZoneCountPattern.Match(zoneStr);

            if (!zoneMatch.Success) return null;

            string zoneName = zoneMatch.Groups[1].Value;
            int cardCount = countMatch.Success ? int.Parse(countMatch.Groups[1].Value) : 0;
            string zoneKey = (isOpponent ? "Opp_" : "Local_") + zoneName;

            if (_zoneCounts.TryGetValue(zoneKey, out int previousCount))
            {
                int diff = cardCount - previousCount;
                _zoneCounts[zoneKey] = cardCount;

                if (diff == 0) return null;

                // Zone content changed - mark navigators dirty for lazy refresh
                MarkNavigatorsDirty();

                if (zoneName == "Hand")
                {
                    if (diff > 0)
                    {
                        // Local draws are announced with card names via the ZoneTransfer path
                        // (HandleZoneTransferGroup) — suppress the generic count here to avoid
                        // double-speak. Opponent draws are hidden information, so announce the
                        // count only. (Same suppression pattern used for Graveyard entries.)
                        return isLocal ? null : Strings.Duel_OpponentDrew(diff);
                    }
                    else if (diff < 0 && isOpponent)
                    {
                        return Strings.Duel_OpponentPlayedCard;
                    }
                }
                else if (zoneName == "Battlefield")
                {
                    if (diff > 0)
                    {
                        if (isOpponent)
                            return Strings.Duel_OpponentEnteredBattlefield(diff);
                        _lastSpellResolvedTime = DateTime.Now;
                    }
                    else if (diff < 0)
                    {
                        // Cards leaving the battlefield are announced by name (with the cause verb
                        // and a count for mass events) via the ZoneTransferGroup path, or via the
                        // CombatFrame path for combat-damage deaths. The old generic "N permanents
                        // left battlefield" line here just double-spoke those, so it is suppressed;
                        // the count is still tracked above so navigators refresh.
                    }
                }
                else if (zoneName == "Graveyard" && diff > 0)
                {
                    // Suppress generic "card to graveyard" — ZoneTransferGroup fires with the specific
                    // card name and reason (died, destroyed, discarded, etc.) which is more informative.
                    // We still track the count above for dirty-marking navigators.
                }
                else if (zoneName == "Stack")
                {
                    if (diff > 0)
                    {
                        MelonCoroutines.Start(AnnounceStackCardDelayed());
                        return null;
                    }
                    else if (diff < 0)
                    {
                        _lastSpellResolvedTime = DateTime.Now;
                        // When the stack drains to empty, reset the announced-IDs tracker so the
                        // next thing cast onto a fresh stack is announced again.
                        if (cardCount == 0)
                            ClearStackAnnouncements();
                        // Resolve announcement is handled by ResolutionEventEndedUXEvent
                        // which carries the actual card data from the game engine.
                        return null;
                    }
                }
            }
            else
            {
                _zoneCounts[zoneKey] = cardCount;

                if (zoneName == "Stack" && cardCount > 0)
                {
                    MelonCoroutines.Start(AnnounceStackCardDelayed());
                    return null;
                }
            }

            return null;
        }

        private string HandleZoneTransferGroup(object uxEvent)
        {
            try
            {
                // Log fields for discovery (once)
                LogEventFieldsOnce(uxEvent, "ZONE TRANSFER GROUP");
                if (uxEvent.GetType().Name == "ZoneTransferUXEvent")
                    LogEventFieldsOnce(uxEvent, "ZONE TRANSFER UX EVENT");

                // Flatten into individual transfers. A bare ZoneTransferUXEvent (not a group) has
                // no _zoneTransfers list to unwrap — it IS the individual transfer (e.g. a single
                // land entering the battlefield), so CollectTransfers treats it as one item.
                var transfers = new List<object>();
                CollectTransfers(uxEvent, transfers);
                if (transfers.Count == 0) return null;

                // Local-player draws (Library -> Hand) are aggregated into a single count+names
                // announcement; everything else becomes a TransferOutcome that AggregateOutcomes
                // buckets so mass events (board wipes, mass mill/discard) read as "N <verb>: names".
                var drawNames = new List<string>();
                var outcomes = new List<TransferOutcome>();

                foreach (var transfer in transfers)
                {
                    if (transfer == null) continue;
                    LogEventFieldsOnce(transfer, "ZONE TRANSFER UX EVENT");

                    string drawName = TryGetLocalDrawCardName(transfer);
                    if (drawName != null)
                    {
                        drawNames.Add(drawName);
                        continue;
                    }

                    var outcome = ProcessZoneTransfer(transfer);
                    if (outcome != null)
                        outcomes.Add(outcome);
                }

                var announcements = new List<string>();

                // Announce draws first (the card you drew, then any downstream effects).
                // Skip the pre-game opening hand / mulligan redraws: those fire before turn 1's
                // first phase (so _currentStep is still null) and the Mulligan browser already
                // gives its own grouped hand summary — announcing all 7 names here is redundant.
                if (drawNames.Count > 0 && _currentStep != null)
                    announcements.Add(BuildLocalDrawAnnouncement(drawNames));

                announcements.AddRange(AggregateOutcomes(outcomes));

                return announcements.Count > 0 ? string.Join(". ", announcements) : null;
            }
            catch (Exception ex)
            {
                Log.Warn("DuelAnnouncer", $"Error handling zone transfer: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Builds the local-player draw announcement:
        ///   one card  -> "&lt;name&gt; drawn"
        ///   many cards -> "&lt;count&gt; cards drawn. &lt;name1&gt;, &lt;name2&gt;, ..."
        /// </summary>
        private string BuildLocalDrawAnnouncement(List<string> drawNames)
        {
            if (drawNames.Count == 1)
                return Strings.Duel_DrewCard(drawNames[0]);
            return Strings.Duel_DrewCards(drawNames.Count, string.Join(", ", drawNames));
        }

        /// <summary>
        /// If this transfer is the local player drawing a card (Library -> Hand, owned by us),
        /// returns the drawn card's name; otherwise null. Opponent draws are hidden information
        /// and always return null (those stay a count-only announcement via UpdateZoneUXEvent).
        /// </summary>
        private string TryGetLocalDrawCardName(object transfer)
        {
            string toZoneType = GetFieldValue<object>(transfer, "ToZoneType")?.ToString() ?? "";
            string fromZoneType = GetFieldValue<object>(transfer, "FromZoneType")?.ToString() ?? "";
            if (toZoneType != "Hand" || fromZoneType != "Library")
                return null;

            // Ownership: for draws, both the Library (from) and Hand (to) zone strings carry the
            // owner tag, e.g. "Library (PlayerPlayer: 1 (LocalPlayer), 0 cards)". Mirror the
            // zone-string ownership checks used by ProcessZoneTransfer.
            string fromZoneStr = GetFieldValue<object>(transfer, "FromZone")?.ToString() ?? "";
            string toZoneStr = GetFieldValue<object>(transfer, "ToZone")?.ToString() ?? "";
            TryUpdateLocalPlayerIdFromZoneString(fromZoneStr);
            TryUpdateLocalPlayerIdFromZoneString(toZoneStr);

            string zoneToCheck = fromZoneStr.Contains("Player") ? fromZoneStr : toZoneStr;
            bool isOpponent;
            if (zoneToCheck.Contains("Opponent"))
                isOpponent = true;
            else if (zoneToCheck.Contains("LocalPlayer"))
                isOpponent = false;
            else if (zoneToCheck.Contains($"Player: {_localPlayerId}") || zoneToCheck.Contains($"Player:{_localPlayerId}"))
                isOpponent = false;
            else
                isOpponent = zoneToCheck.Contains("Player"); // some other player ref = opponent
            if (isOpponent)
                return null;

            // Card name from the instance's GrpId
            var cardInstance = GetFieldValue<object>(transfer, "NewInstance")
                ?? GetFieldValue<object>(transfer, "OldInstance");
            if (cardInstance == null) return null;

            uint grpId = 0;
            var printing = GetNestedPropertyValue<object>(cardInstance, "Printing");
            if (printing != null) grpId = GetNestedPropertyValue<uint>(printing, "GrpId");
            if (grpId == 0) grpId = GetNestedPropertyValue<uint>(cardInstance, "GrpId");
            if (grpId == 0) return null;

            string cardName = CardModelProvider.GetNameFromGrpId(grpId);
            return string.IsNullOrEmpty(cardName) ? null : cardName;
        }

        /// <summary>
        /// Processes a single zone transfer event to announce game state changes.
        /// Handles: land plays, creatures dying, cards discarded, exiled, bounced, tokens created, etc.
        /// </summary>
        private TransferOutcome ProcessZoneTransfer(object transfer)
        {
            try
            {
                // Get zone types and reason
                var toZoneType = GetFieldValue<object>(transfer, "ToZoneType");
                var fromZoneType = GetFieldValue<object>(transfer, "FromZoneType");
                var toZone = GetFieldValue<object>(transfer, "ToZone");
                var fromZone = GetFieldValue<object>(transfer, "FromZone");
                var reason = GetFieldValue<object>(transfer, "Reason");

                string toZoneTypeStr = toZoneType?.ToString() ?? "";
                string fromZoneTypeStr = fromZoneType?.ToString() ?? "";
                string toZoneStr = toZone?.ToString() ?? "";
                string fromZoneStr = fromZone?.ToString() ?? "";
                string reasonStr = reason?.ToString() ?? "";

                // Get card instance - the NewInstance field contains the card data
                var newInstance = GetFieldValue<object>(transfer, "NewInstance");

                uint grpId = 0;
                bool isOpponent = false;

                // Use NewInstance first, fall back to OldInstance (e.g. countered abilities have no NewInstance)
                var cardInstance = newInstance;
                if (cardInstance == null)
                {
                    cardInstance = GetFieldValue<object>(transfer, "OldInstance");
                }

                if (cardInstance != null)
                {
                    // Try to get GrpId from the card instance
                    var printing = GetNestedPropertyValue<object>(cardInstance, "Printing");
                    if (printing != null)
                    {
                        grpId = GetNestedPropertyValue<uint>(printing, "GrpId");
                    }
                    if (grpId == 0)
                    {
                        grpId = GetNestedPropertyValue<uint>(cardInstance, "GrpId");
                    }

                    // Check ownership via controller - try multiple property names
                    uint controllerId = GetNestedPropertyValue<uint>(cardInstance, "ControllerSeatId");
                    if (controllerId == 0) controllerId = GetNestedPropertyValue<uint>(cardInstance, "ControllerId");
                    if (controllerId == 0) controllerId = GetNestedPropertyValue<uint>(cardInstance, "OwnerSeatId");
                    if (controllerId == 0) controllerId = GetNestedPropertyValue<uint>(cardInstance, "OwnerNum");
                    if (controllerId == 0) controllerId = GetNestedPropertyValue<uint>(cardInstance, "ControllerNum");

                    // Try Owner property which might be a player object
                    if (controllerId == 0)
                    {
                        var owner = GetNestedPropertyValue<object>(cardInstance, "Owner");
                        if (owner != null)
                        {
                            controllerId = GetNestedPropertyValue<uint>(owner, "SeatId");
                            if (controllerId == 0) controllerId = GetNestedPropertyValue<uint>(owner, "Id");
                            if (controllerId == 0) controllerId = GetNestedPropertyValue<uint>(owner, "PlayerNumber");
                        }
                    }

                    isOpponent = controllerId != 0 && controllerId != _localPlayerId;
                }

                // Check zone strings for ownership hints as fallback
                // Zone format example: "Library (PlayerPlayer: 1 (LocalPlayer), 0 cards)" or "Hand (OpponentPlayer: 2, 5 cards)"
                // For cards entering battlefield from hand, check FromZone (hand) for ownership
                // For cards leaving battlefield, check FromZone (battlefield area might not have owner)
                string zoneToCheck = fromZoneStr;
                if (string.IsNullOrEmpty(zoneToCheck) || !zoneToCheck.Contains("Player"))
                {
                    zoneToCheck = toZoneStr;
                }

                // Try to auto-correct local player ID from zone strings containing "(LocalPlayer)"
                TryUpdateLocalPlayerIdFromZoneString(fromZoneStr);
                TryUpdateLocalPlayerIdFromZoneString(toZoneStr);

                // Check zone strings for ownership - use _localPlayerId dynamically, don't hardcode Player 1/2
                if (zoneToCheck.Contains("Opponent"))
                    isOpponent = true;
                else if (zoneToCheck.Contains("LocalPlayer"))
                    isOpponent = false;
                else if (zoneToCheck.Contains($"Player: {_localPlayerId}") || zoneToCheck.Contains($"Player:{_localPlayerId}"))
                    isOpponent = false;
                else if (zoneToCheck.Contains("Player: ") || zoneToCheck.Contains("Player:"))
                    isOpponent = true; // Contains a player reference but not our ID, so it's opponent

                // Log for debugging (skip for GrpId=0 library shuffles)
                if (grpId != 0)
                    Log.Announce("DuelAnnouncer", $"ZoneTransfer: {fromZoneTypeStr} -> {toZoneTypeStr}, Reason={reasonStr}, GrpId={grpId}, isOpponent={isOpponent}");

                // Skip if no card data
                if (grpId == 0)
                {
                    return null;
                }

                // Get card name
                string cardName = CardModelProvider.GetNameFromGrpId(grpId);
                if (string.IsNullOrEmpty(cardName))
                {
                    return null;
                }

                string ownerPrefix = isOpponent ? Strings.Duel_OwnerPrefix_Opponent : "";
                TransferOutcome outcome = null;

                // Determine outcome based on zone transfer type
                switch (toZoneTypeStr)
                {
                    case "Battlefield":
                        var bfText = ProcessBattlefieldEntry(fromZoneTypeStr, reasonStr, cardName, grpId, newInstance, isOpponent);
                        if (bfText != null)
                            _lastSpellResolvedTime = DateTime.Now;
                        outcome = OutcomeText(bfText);
                        break;

                    case "Graveyard":
                        outcome = ProcessGraveyardOutcome(fromZoneTypeStr, reasonStr, cardName, ownerPrefix, isOpponent, transfer);
                        break;

                    case "Exile":
                        outcome = ProcessExileOutcome(fromZoneTypeStr, reasonStr, cardName, ownerPrefix, isOpponent, transfer);
                        break;

                    case "Hand":
                        outcome = OutcomeText(ProcessHandEntry(fromZoneTypeStr, reasonStr, cardName, isOpponent));
                        break;

                    case "Stack":
                        // Spells on stack are announced via UpdateZoneUXEvent already
                        break;
                }

                if (outcome != null)
                {
                    Log.Announce("DuelAnnouncer", $"Zone transfer outcome: {(outcome.Aggregatable ? outcome.Verb + " " + cardName : outcome.Text)}");
                }

                return outcome;
            }
            catch (Exception ex)
            {
                Log.Warn("DuelAnnouncer", $"Error processing zone transfer: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Process card entering battlefield - lands, tokens, creatures from stack
        /// </summary>
        private string ProcessBattlefieldEntry(string fromZone, string reason, string cardName, uint grpId, object cardInstance, bool isOpponent)
        {
            string owner = isOpponent ? Strings.Duel_Opponent : Strings.Duel_You;

            // Token creation (from None zone with CardCreated reason)
            // Note: Game doesn't provide ownership info for tokens, so we don't announce who created it
            if ((fromZone == "None" || string.IsNullOrEmpty(fromZone)) && reason == "CardCreated")
            {
                return Strings.Duel_TokenCreated(cardName);
            }

            // Check if this card is an aura/equipment attaching to another card
            string attachedToName = GetAttachedToName(cardInstance);

            // Land played (from Hand, not from Stack)
            if (fromZone == "Hand")
            {
                bool isLand = IsLandByGrpId(grpId, cardInstance);
                if (isLand)
                {
                    return Strings.Duel_Played(owner, cardName);
                }
                // Non-land from hand without going through stack (e.g., put onto battlefield effects)
                if (!string.IsNullOrEmpty(attachedToName))
                {
                    return Strings.Duel_Enchanted(cardName, attachedToName);
                }
                return Strings.Duel_EntersBattlefield(cardName);
            }

            // From stack = spell resolved (creature/artifact/enchantment)
            if (fromZone == "Stack")
            {
                // Check if it's an aura that attached to something
                if (!string.IsNullOrEmpty(attachedToName))
                {
                    return Strings.Duel_Enchanted(cardName, attachedToName);
                }
                // We already announce spell cast, so just note it entered
                // Could skip this to avoid double announcement, or make it brief
                return null; // Skip - UpdateZoneUXEvent handles "spell resolved"
            }

            // From graveyard = reanimation
            if (fromZone == "Graveyard")
            {
                if (!string.IsNullOrEmpty(attachedToName))
                {
                    return Strings.Duel_ReturnedFromGraveyardEnchanting(cardName, attachedToName);
                }
                return Strings.Duel_ReturnedFromGraveyard(cardName);
            }

            // From exile = returned from exile
            if (fromZone == "Exile")
            {
                if (!string.IsNullOrEmpty(attachedToName))
                {
                    return Strings.Duel_ReturnedFromExileEnchanting(cardName, attachedToName);
                }
                return Strings.Duel_ReturnedFromExile(cardName);
            }

            // From library = put onto battlefield from library
            if (fromZone == "Library")
            {
                if (!string.IsNullOrEmpty(attachedToName))
                {
                    return Strings.Duel_EntersBattlefieldFromLibraryEnchanting(cardName, attachedToName);
                }
                return Strings.Duel_EntersBattlefieldFromLibrary(cardName);
            }

            return null;
        }

        /// <summary>
        /// Gets the name of the card that this card is attached to (for auras/equipment).
        /// Returns null if not attached to anything.
        /// </summary>
        private string GetAttachedToName(object cardInstance)
        {
            if (cardInstance == null) return null;

            try
            {
                uint attachedToId = GetNestedPropertyValue<uint>(cardInstance, "AttachedToId");
                if (attachedToId == 0) return null;

                Log.Announce("DuelAnnouncer", $"Card has AttachedToId={attachedToId}, scanning battlefield for parent");

                var holder = DuelHolderCache.GetHolder("BattlefieldCardHolder");
                if (holder == null) return null;

                foreach (Transform child in holder.GetComponentsInChildren<Transform>(true))
                {
                    if (child == null || !child.gameObject.activeInHierarchy) continue;

                    var cdc = CardModelProvider.GetDuelSceneCDC(child.gameObject);
                    if (cdc == null) continue;

                    var model = CardModelProvider.GetCardModel(cdc);
                    if (model == null) continue;

                    var instanceId = GetFieldValue<uint>(model, "InstanceId");
                    if (instanceId != attachedToId) continue;

                    var grpId = GetFieldValue<uint>(model, "GrpId");
                    if (grpId > 0)
                    {
                        string parentName = CardModelProvider.GetNameFromGrpId(grpId);
                        if (!string.IsNullOrEmpty(parentName))
                        {
                            Log.Announce("DuelAnnouncer", $"Card is attached to: {parentName} (InstanceId={attachedToId})");
                            return parentName;
                        }
                    }
                }

                Log.Announce("DuelAnnouncer", $"AttachedToId={attachedToId} but parent not found on battlefield");
            }
            catch (Exception ex)
            {
                Log.Announce("DuelAnnouncer", $"Error getting attached-to name: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Process card entering graveyard - death, destruction, discard, mill, counter.
        /// Returns an aggregatable removal (so board wipes / mass mill read as "N <verb>: names")
        /// or a dedicated line for countered spells and the legend rule.
        /// </summary>
        private TransferOutcome ProcessGraveyardOutcome(string fromZone, string reason, string cardName, string ownerPrefix, bool isOpponent, object transfer)
        {
            // Countered spells/abilities and the legend rule read as their own dedicated line.
            if (reason == "Countered")
                return OutcomeText(BuildCounteredAnnouncement(ownerPrefix, cardName, transfer, exiled: false));
            if (reason == "Legend" || reason == "World")
                return OutcomeText(Strings.Duel_LegendRule(ownerPrefix, cardName));

            RemovalVerb? verb = ClassifyGraveyardVerb(fromZone, reason);
            if (verb == null)
                return null; // e.g. Stack -> Graveyard (spell resolved, announced elsewhere)
            return OutcomeRemoval(verb.Value, isOpponent, cardName);
        }

        /// <summary>
        /// Maps the game's ZoneTransferReason (enum .ToString(), e.g. "Destroy", "ZeroToughness")
        /// to the verb we announce. Only creatures can leave via ZeroToughness/Damage/Deathtouch,
        /// so those are the genuine "dies"; everything else uses a type-neutral verb. Unknown
        /// reasons fall back by source zone (never a blanket "died", which mislabels non-creatures).
        /// </summary>
        private RemovalVerb? ClassifyGraveyardVerb(string fromZone, string reason)
        {
            switch (reason)
            {
                case "ZeroToughness":
                case "Damage":
                case "Deathtouch":
                    return RemovalVerb.Died;
                case "Destroy":
                case "DestroyNoRegenerate":
                    return RemovalVerb.Destroyed;
                case "Sacrifice":
                    return RemovalVerb.Sacrificed;
                case "Discard":
                case "Cycle":
                    return RemovalVerb.Discarded;
                case "Mill":
                    return RemovalVerb.Milled;
                case "Put":
                case "ZoneTransferWithoutIdChange":
                case "ZeroLoyalty":
                    return RemovalVerb.PutToGraveyard;
            }

            // Unknown/empty reason: fall back on the source zone.
            switch (fromZone)
            {
                case "Battlefield": return RemovalVerb.PutToGraveyard;
                case "Hand": return RemovalVerb.Discarded;
                case "Library": return RemovalVerb.Milled;
                case "Stack": return null; // spell/ability resolved — announced elsewhere
                default: return RemovalVerb.PutToGraveyard;
            }
        }

        /// <summary>
        /// Process card entering exile. Battlefield -> exile is an aggregatable removal (so mass
        /// exile reads as "N permanents exiled: names"); other sources keep their dedicated lines.
        /// </summary>
        private TransferOutcome ProcessExileOutcome(string fromZone, string reason, string cardName, string ownerPrefix, bool isOpponent, object transfer)
        {
            // Countered spells that exile (e.g., Dissipate, Syncopate)
            if (reason == "Countered")
                return OutcomeText(BuildCounteredAnnouncement(ownerPrefix, cardName, transfer, exiled: true));

            if (fromZone == "Battlefield")
                return OutcomeRemoval(RemovalVerb.Exiled, isOpponent, cardName);
            if (fromZone == "Graveyard")
                return OutcomeText(Strings.Duel_ExiledFromGraveyard(ownerPrefix, cardName));
            if (fromZone == "Hand")
                return OutcomeText(Strings.Duel_ExiledFromHand(ownerPrefix, cardName));
            if (fromZone == "Library")
                return OutcomeText(Strings.Duel_ExiledFromLibrary(ownerPrefix, cardName));
            if (fromZone == "Stack")
                // Spell from stack to exile without Countered reason — "Spell resolved" handles it
                return null;

            return OutcomeRemoval(RemovalVerb.Exiled, isOpponent, cardName);
        }

        /// <summary>
        /// Process card entering hand - bounce, draw (draw handled elsewhere)
        /// </summary>
        private string ProcessHandEntry(string fromZone, string reason, string cardName, bool isOpponent)
        {
            string ownerPrefix = isOpponent ? Strings.Duel_OwnerPrefix_Opponent : "";

            // Bounce from battlefield
            if (fromZone == "Battlefield")
            {
                return Strings.Duel_ReturnedToHand(ownerPrefix, cardName);
            }

            // From library = draw. Local-player draws are aggregated (with card names) in
            // HandleZoneTransferGroup before reaching here, so this path only runs for opponent
            // draws — which are hidden information and announced as a count via UpdateZoneUXEvent.
            if (fromZone == "Library")
            {
                return null;
            }

            // From graveyard = returned to hand
            if (fromZone == "Graveyard")
            {
                return Strings.Duel_ReturnedToHandFromGraveyard(ownerPrefix, cardName);
            }

            // From exile = returned from exile to hand
            if (fromZone == "Exile")
            {
                return Strings.Duel_ReturnedToHandFromExile(ownerPrefix, cardName);
            }

            // From stack with Undo = spell cancelled (player took back)
            if (fromZone == "Stack" && reason == "Undo")
            {
                return Strings.SpellCancelled;
            }

            return null;
        }

        /// <summary>
        /// Checks if a card is a land based on its GrpId or card object.
        /// </summary>
        private bool IsLandByGrpId(uint grpId, object card)
        {
            // Try to get card types from the card object
            if (card != null)
            {
                // Check IsBasicLand property
                var isBasicLand = GetNestedPropertyValue<bool>(card, "IsBasicLand");
                if (isBasicLand) return true;

                // Check IsLandButNotBasic property
                var isLandNotBasic = GetNestedPropertyValue<bool>(card, "IsLandButNotBasic");
                if (isLandNotBasic) return true;

                // Check CardTypes collection
                var cardTypes = GetNestedPropertyValue<object>(card, "CardTypes");
                if (cardTypes is System.Collections.IEnumerable typeEnum)
                {
                    foreach (var ct in typeEnum)
                    {
                        if (ct?.ToString()?.Contains("Land") == true)
                        {
                            return true;
                        }
                    }
                }
            }

            // Fallback: check if card name is a basic land
            string cardName = CardModelProvider.GetNameFromGrpId(grpId);
            if (!string.IsNullOrEmpty(cardName))
            {
                if (BasicLandNames.Contains(cardName))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
