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
                        return isLocal
                            ? Strings.Duel_Drew(diff)
                            : Strings.Duel_OpponentDrew(diff);
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
                        int removed = Math.Abs(diff);
                        // Battlefield is a shared zone - can't determine ownership from zone string
                        // Graveyard/Exile announcements will specify correct ownership
                        return Strings.Duel_LeftBattlefield(removed);
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

                // Get the _zoneTransfers list which contains individual ZoneTransferUXEvent items
                var zoneTransfers = GetFieldValue<object>(uxEvent, "_zoneTransfers");
                if (zoneTransfers == null) return null;

                var transferList = zoneTransfers as System.Collections.IEnumerable;
                if (transferList == null) return null;

                var announcements = new List<string>();

                foreach (var transfer in transferList)
                {
                    if (transfer == null) continue;

                    // Log ZoneTransferUXEvent fields once for discovery
                    LogEventFieldsOnce(transfer, "ZONE TRANSFER UX EVENT");

                    // Extract zone transfer details
                    var announcement = ProcessZoneTransfer(transfer);
                    if (!string.IsNullOrEmpty(announcement))
                    {
                        announcements.Add(announcement);
                    }
                }

                if (announcements.Count > 0)
                {
                    return string.Join(". ", announcements);
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.Warn("DuelAnnouncer", $"Error handling zone transfer: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Processes a single zone transfer event to announce game state changes.
        /// Handles: land plays, creatures dying, cards discarded, exiled, bounced, tokens created, etc.
        /// </summary>
        private string ProcessZoneTransfer(object transfer)
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
                string announcement = null;

                // Track commander ownership permanently (for opponent commander detection).
                // Don't overwrite data already seeded from MatchManager — zone transfer events
                // have unreliable isOpponent detection (ControllerId is 0 for opponent commanders).
                if (toZoneTypeStr == "Command" && grpId != 0 && !_commandZoneGrpIds.ContainsKey(grpId))
                {
                    _commandZoneGrpIds[grpId] = isOpponent;
                    Log.Msg("DuelAnnouncer", $"Tracking commander from zone event: GrpId={grpId} ({cardName}), isOpponent={isOpponent}");
                }

                // Determine announcement based on zone transfer type
                switch (toZoneTypeStr)
                {
                    case "Battlefield":
                        announcement = ProcessBattlefieldEntry(fromZoneTypeStr, reasonStr, cardName, grpId, newInstance, isOpponent);
                        if (announcement != null)
                            _lastSpellResolvedTime = DateTime.Now;
                        break;

                    case "Graveyard":
                        announcement = ProcessGraveyardEntry(fromZoneTypeStr, reasonStr, cardName, ownerPrefix, transfer);
                        break;

                    case "Exile":
                        announcement = ProcessExileEntry(fromZoneTypeStr, reasonStr, cardName, ownerPrefix, transfer);
                        break;

                    case "Hand":
                        announcement = ProcessHandEntry(fromZoneTypeStr, reasonStr, cardName, isOpponent);
                        break;

                    case "Stack":
                        // Spells on stack are announced via UpdateZoneUXEvent already
                        break;
                }

                if (!string.IsNullOrEmpty(announcement))
                {
                    Log.Announce("DuelAnnouncer", $"Zone transfer announcement: {announcement}");
                }

                return announcement;
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
        /// Process card entering graveyard - death, destruction, discard, mill, counter
        /// </summary>
        private string ProcessGraveyardEntry(string fromZone, string reason, string cardName, string ownerPrefix, object transfer)
        {
            // Use reason for specific language if available
            switch (reason)
            {
                case "Died":
                    return Strings.Duel_Died(ownerPrefix, cardName);
                case "Destroyed":
                    return Strings.Duel_Destroyed(ownerPrefix, cardName);
                case "Sacrificed":
                    return Strings.Duel_Sacrificed(ownerPrefix, cardName);
                case "Countered":
                    return BuildCounteredAnnouncement(ownerPrefix, cardName, transfer, exiled: false);
                case "Discarded":
                    return Strings.Duel_Discarded(ownerPrefix, cardName);
                case "Milled":
                    return Strings.Duel_Milled(ownerPrefix, cardName);
            }

            // Fallback based on source zone
            switch (fromZone)
            {
                case "Battlefield":
                    return Strings.Duel_Died(ownerPrefix, cardName);
                case "Hand":
                    return Strings.Duel_Discarded(ownerPrefix, cardName);
                case "Stack":
                    // Don't announce "countered" as fallback - countering is only when reason == "Countered"
                    // Normal spell resolution (instant/sorcery) also goes Stack -> Graveyard
                    // "Spell resolved" is already announced via UpdateZoneUXEvent, so skip here
                    return null;
                case "Library":
                    return Strings.Duel_Milled(ownerPrefix, cardName);
                default:
                    return Strings.Duel_WentToGraveyard(ownerPrefix, cardName);
            }
        }

        /// <summary>
        /// Process card entering exile
        /// </summary>
        private string ProcessExileEntry(string fromZone, string reason, string cardName, string ownerPrefix, object transfer)
        {
            // Check for countered spells that exile (e.g., Dissipate, Syncopate)
            if (reason == "Countered")
            {
                return BuildCounteredAnnouncement(ownerPrefix, cardName, transfer, exiled: true);
            }

            if (fromZone == "Battlefield")
            {
                return Strings.Duel_Exiled(ownerPrefix, cardName);
            }
            if (fromZone == "Graveyard")
            {
                return Strings.Duel_ExiledFromGraveyard(ownerPrefix, cardName);
            }
            if (fromZone == "Hand")
            {
                return Strings.Duel_ExiledFromHand(ownerPrefix, cardName);
            }
            if (fromZone == "Library")
            {
                return Strings.Duel_ExiledFromLibrary(ownerPrefix, cardName);
            }
            if (fromZone == "Stack")
            {
                // Spell from stack to exile without Countered reason - could be an effect
                // Skip announcement since "Spell resolved" handles the stack clearing
                return null;
            }
            return Strings.Duel_Exiled(ownerPrefix, cardName);
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

            // From library = draw, but we handle this via UpdateZoneUXEvent with count
            // Don't duplicate the announcement
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
