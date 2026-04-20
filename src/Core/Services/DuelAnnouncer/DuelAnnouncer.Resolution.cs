using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    public partial class DuelAnnouncer
    {
        // Cache for instance ID to card name lookup
        private Dictionary<uint, string> _instanceIdToName = new Dictionary<uint, string>();

        // Track the last resolving card for damage correlation
        private string _lastResolvingCardName = null;

        /// <summary>
        /// Tracks if a library manipulation browser (scry, surveil, etc.) is active.
        /// Set to true when MultistepEffectStartedUXEvent fires.
        /// </summary>
        public bool IsLibraryBrowserActive { get; private set; }

        /// <summary>
        /// Info about the current library manipulation effect.
        /// </summary>
        public string CurrentEffectType { get; private set; }

        /// <summary>
        /// Returns true if a spell resolved or a permanent entered battlefield within the last specified milliseconds.
        /// Used to skip targeting mode for lands and non-targeted cards.
        /// </summary>
        public bool DidSpellResolveRecently(int withinMs = 500)
        {
            return (DateTime.Now - _lastSpellResolvedTime).TotalMilliseconds < withinMs;
        }

        private string HandleMultistepEffect(object uxEvent)
        {
            try
            {
                // Log fields for discovery (once)
                LogEventFieldsOnce(uxEvent, "MULTISTEP EFFECT");

                // Extract effect information using correct property names from logs:
                // - AbilityCategory (AbilitySubCategory enum): Scry, Surveil, etc.
                // - Affector (MtgCardInstance): source card
                // - Affected (MtgPlayer): target player
                var abilityCategory = GetFieldValue<object>(uxEvent, "AbilityCategory");
                var affector = GetFieldValue<object>(uxEvent, "Affector");
                var affected = GetFieldValue<object>(uxEvent, "Affected");

                string effectName = abilityCategory?.ToString() ?? "unknown";
                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"MultistepEffect: AbilityCategory={effectName}, Affector={affector}, Affected={affected}");

                // Determine effect type and description
                string effectDescription;
                CurrentEffectType = effectName;

                switch (effectName.ToLower())
                {
                    case "scry":
                        effectDescription = "Scry";
                        break;
                    case "surveil":
                        effectDescription = "Surveil";
                        break;
                    case "look":
                    case "lookat":
                        effectDescription = Strings.Duel_LookAtTopCard;
                        break;
                    case "mill":
                        effectDescription = "Mill";
                        break;
                    default:
                        effectDescription = effectName;
                        break;
                }

                IsLibraryBrowserActive = true;

                // Get card name from affector if available
                string cardName = null;
                if (affector != null)
                {
                    // Try to get GrpId from the affector's Printing property
                    var printingProp = affector.GetType().GetProperty("Printing");
                    if (printingProp != null)
                    {
                        var printing = printingProp.GetValue(affector);
                        if (printing != null)
                        {
                            var grpIdProp = printing.GetType().GetProperty("GrpId");
                            if (grpIdProp != null)
                            {
                                var grpId = grpIdProp.GetValue(printing);
                                if (grpId is uint gid && gid != 0)
                                {
                                    cardName = CardModelProvider.GetNameFromGrpId(gid);
                                }
                                else if (grpId is int gidInt && gidInt != 0)
                                {
                                    cardName = CardModelProvider.GetNameFromGrpId((uint)gidInt);
                                }
                            }
                        }
                    }

                    // Fallback: try direct GrpId on affector
                    if (string.IsNullOrEmpty(cardName))
                    {
                        var directGrpId = GetFieldValue<uint>(affector, "GrpId");
                        if (directGrpId != 0)
                        {
                            cardName = CardModelProvider.GetNameFromGrpId(directGrpId);
                        }
                    }
                }

                // Build announcement based on effect type
                string announcement;
                if (effectName.ToLower() == "scry")
                {
                    announcement = Strings.WithHint(effectDescription, "Duel_ScryHint");
                }
                else if (effectName.ToLower() == "surveil")
                {
                    announcement = Strings.WithHint(effectDescription, "Duel_SurveilHint");
                }
                else
                {
                    announcement = Strings.Duel_EffectHint(effectDescription);
                }

                if (!string.IsNullOrEmpty(cardName))
                {
                    DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Library browser active: {effectDescription} from {cardName}");
                }
                else
                {
                    DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Library browser active: {effectDescription}");
                }

                return announcement;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error handling multistep effect: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Called when library browser is closed (effect resolved).
        /// </summary>
        public void OnLibraryBrowserClosed()
        {
            IsLibraryBrowserActive = false;
            CurrentEffectType = null;
        }

        private string HandleResolutionStarted(object uxEvent)
        {
            try
            {
                // Log fields for discovery (once)
                LogEventFieldsOnce(uxEvent, "RESOLUTION EVENT");

                // Try to get the instigator (source card) info
                var instigatorInstanceId = GetFieldValue<uint>(uxEvent, "InstigatorInstanceId");

                // Try to get card name from various possible fields
                string cardName = null;

                // Try Instigator property (might be a card object)
                var instigator = GetFieldValue<object>(uxEvent, "Instigator");
                if (instigator != null)
                {
                    var gid = GetFieldValue<uint>(instigator, "GrpId");
                    if (gid != 0)
                    {
                        cardName = CardModelProvider.GetNameFromGrpId(gid);
                    }
                }

                // Check if this is an ability (not a spell) resolving
                bool isAbility = false;
                if (instigator != null)
                {
                    var objectType = GetFieldValue<object>(instigator, "ObjectType");
                    if (objectType != null && objectType.ToString() == "Ability")
                        isAbility = true;
                }

                // Store for later correlation with life/damage events
                if (!string.IsNullOrEmpty(cardName))
                {
                    _lastResolvingCardName = cardName;
                    DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Resolution started: {cardName} (InstanceId: {instigatorInstanceId}, isAbility: {isAbility})");
                }

                return null; // Don't announce resolution start, just track it
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error handling resolution: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Handles ResolutionEventEndedUXEvent — fired by the game when a spell or ability
        /// finishes resolving. Carries the Instigator (source card) directly from the game
        /// engine, so this works reliably for all resolutions including auto-resolving
        /// triggered abilities (begin-of-combat, attack triggers, etc.).
        /// </summary>
        private string HandleResolutionEnded(object uxEvent)
        {
            try
            {
                var instigator = GetFieldValue<object>(uxEvent, "Instigator");
                if (instigator == null) return null;

                var gid = GetFieldValue<uint>(instigator, "GrpId");
                if (gid == 0) return null;

                string cardName = CardModelProvider.GetNameFromGrpId(gid);
                if (string.IsNullOrEmpty(cardName)) return null;

                bool isAbility = false;
                var objectType = GetFieldValue<object>(instigator, "ObjectType");
                if (objectType != null && objectType.ToString() == "Ability")
                    isAbility = true;

                // Clear resolution tracking data now that the resolution is complete
                _lastResolvingCardName = null;

                // Delay so the resolve announcement arrives after the cast/trigger
                // announcement (which waits 3 frames for the stack holder to populate).
                string msg = isAbility ? Strings.Duel_AbilityResolved(cardName) : Strings.Duel_Resolved(cardName);
                MelonCoroutines.Start(AnnounceResolvedDelayed(msg));
                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error handling resolution ended: {ex.Message}");
                return null;
            }
        }

        private IEnumerator AnnounceResolvedDelayed(string message)
        {
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            if (!_isActive) yield break;
            AnnounceToLog(message, AnnouncementPriority.Normal);
        }

        /// <summary>
        /// Builds a counter announcement with optional source card name.
        /// Checks the transfer's Instigator for the counterspell source and
        /// whether the countered item is an ability.
        /// </summary>
        private string BuildCounteredAnnouncement(string ownerPrefix, string cardName, object transfer, bool exiled)
        {
            string sourceName = null;
            bool isAbility = false;

            try
            {
                if (transfer != null)
                {
                    // Check if this is an ability being countered
                    var isAbilityField = GetFieldValue<object>(transfer, "IsAbilityBeingCountered");
                    if (isAbilityField != null && isAbilityField is bool b)
                        isAbility = b;

                    // Try to get the Instigator (the counterspell source)
                    var instigator = GetFieldValue<object>(transfer, "Instigator");
                    if (instigator != null)
                    {
                        var gid = GetFieldValue<uint>(instigator, "GrpId");
                        if (gid != 0)
                        {
                            sourceName = CardModelProvider.GetNameFromGrpId(gid);
                        }
                    }

                    // Fallback: Instigator is null for the non-caster — use the currently
                    // resolving card name from ResolutionStarted (which is the counterspell)
                    if (string.IsNullOrEmpty(sourceName) && !string.IsNullOrEmpty(_lastResolvingCardName))
                    {
                        sourceName = _lastResolvingCardName;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Error extracting counter source: {ex.Message}");
            }

            if (isAbility)
            {
                // Ability countered (abilities go to graveyard, not exile)
                if (!string.IsNullOrEmpty(sourceName))
                    return Strings.Duel_AbilityCounteredBy(ownerPrefix, cardName, sourceName);
                return Strings.Duel_AbilityCountered(ownerPrefix, cardName);
            }

            if (!string.IsNullOrEmpty(sourceName))
            {
                return exiled
                    ? Strings.Duel_CounteredAndExiledBy(ownerPrefix, cardName, sourceName)
                    : Strings.Duel_CounteredBy(ownerPrefix, cardName, sourceName);
            }

            // Fallback to existing no-source announcements
            return exiled
                ? Strings.Duel_CounteredAndExiled(ownerPrefix, cardName)
                : Strings.Duel_Countered(ownerPrefix, cardName);
        }

        private IEnumerator AnnounceStackCardDelayed()
        {
            yield return null;
            yield return null;
            yield return null;
            if (!_isActive) yield break;

            GameObject stackCard = GetTopStackCard();

            if (stackCard != null)
            {
                AnnounceToLog(BuildCastAnnouncement(stackCard), AnnouncementPriority.High);
            }
            else
            {
                yield return new WaitForSeconds(0.2f);
                if (!_isActive) yield break;
                stackCard = GetTopStackCard();

                if (stackCard != null)
                    AnnounceToLog(BuildCastAnnouncement(stackCard), AnnouncementPriority.High);
                else
                    AnnounceToLog(Strings.SpellCast, AnnouncementPriority.High);
            }
        }

        private string BuildCastAnnouncement(GameObject cardObj)
        {
            var info = CardDetector.ExtractCardInfo(cardObj);
            var parts = new List<string>();

            // Check if this is an ability rather than a spell
            var (isAbility, isTriggered) = CardStateProvider.IsAbilityOnStack(cardObj);

            if (isAbility)
            {
                // Format: "[Name] triggered, [rules text]" or "[Name] activated, [rules text]"
                string abilityVerb = isTriggered ? Strings.AbilityTriggered : Strings.AbilityActivated;
                parts.Add($"{info.Name ?? Strings.AbilityUnknown} {abilityVerb}");
            }
            else
            {
                // Determine action-type-specific cast prefix from ObjectType
                string castPrefix = GetCastPrefix(cardObj);
                parts.Add($"{castPrefix} {info.Name ?? Strings.SpellUnknown}");

                if (!string.IsNullOrEmpty(info.PowerToughness))
                    parts.Add(info.PowerToughness);
            }

            // Brief mode: skip rules text for own and/or opponent cards based on settings
            bool isOpponent = CardStateProvider.IsOpponentCard(cardObj);
            bool briefOwn = AccessibleArenaMod.Instance?.Settings?.BriefCastAnnouncements == true;
            bool briefOpp = AccessibleArenaMod.Instance?.Settings?.BriefOpponentAnnouncements == true;
            if ((!isOpponent && briefOwn) || (isOpponent && briefOpp))
                return string.Join(", ", parts);

            // Rules text is relevant for both spells and abilities
            if (!string.IsNullOrEmpty(info.RulesText))
                parts.Add(info.RulesText);

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Gets an action-type-specific cast prefix based on the card's ObjectType.
        /// Returns localized prefixes for Adventure, MDFC back face, Split halves, etc.
        /// Falls back to the generic "Cast" prefix if ObjectType is not special.
        /// </summary>
        private string GetCastPrefix(GameObject cardObj)
        {
            try
            {
                var cdcComponent = CardModelProvider.GetDuelSceneCDC(cardObj);
                if (cdcComponent == null) return Strings.SpellCastPrefix;

                var model = CardModelProvider.GetCardModel(cdcComponent);
                if (model == null) return Strings.SpellCastPrefix;

                var modelType = model.GetType();
                var objectTypeVal = CardModelProvider.GetModelPropertyValue(model, modelType, "ObjectType");
                if (objectTypeVal == null) return Strings.SpellCastPrefix;

                // ObjectType is GameObjectType enum - use int comparison to avoid referencing the enum type
                // Note: On the stack, ObjectType is typically Card(1) even for adventure/MDFC faces.
                // The specific prefixes will activate if the game provides a non-Card ObjectType.
                int objectTypeInt = (int)objectTypeVal;

                // Check if this is a land being played (not cast)
                var cardTypes = CardModelProvider.GetModelPropertyValue(model, modelType, "CardTypes") as IEnumerable;
                if (cardTypes != null)
                {
                    foreach (var ct in cardTypes)
                    {
                        if (ct?.ToString() == "Land")
                            return Strings.PlayedLand;
                    }
                }

                // GameObjectType enum values from GreProtobuf.dll:
                // Adventure=10, Mdfcback=11, DisturbBack=12, PrototypeFacet=14,
                // RoomLeft=15, RoomRight=16, Omen=17, SplitLeft=6, SplitRight=7
                switch (objectTypeInt)
                {
                    case 10: return Strings.CastAdventure;
                    case 11: return Strings.CastMdfc;
                    case 12: return Strings.CastDisturb;
                    case 14: return Strings.CastPrototype;
                    case 15:
                    case 16: return Strings.CastRoom;
                    case 17: return Strings.CastOmen;
                    case 6: return Strings.CastSplitLeft;
                    case 7: return Strings.CastSplitRight;
                    default: return Strings.SpellCastPrefix;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error getting cast prefix: {ex.Message}");
                return Strings.SpellCastPrefix;
            }
        }

        private GameObject GetTopStackCard()
        {
            try
            {
                var holder = DuelHolderCache.GetHolder("StackCardHolder");
                if (holder == null) return null;

                foreach (Transform child in holder.GetComponentsInChildren<Transform>(true))
                {
                    if (child != null && child.gameObject.activeInHierarchy && child.name.Contains("CDC #"))
                        return child.gameObject;
                }
            }
            catch { /* Stack holder may not exist in current game state */ }
            return null;
        }
    }
}
