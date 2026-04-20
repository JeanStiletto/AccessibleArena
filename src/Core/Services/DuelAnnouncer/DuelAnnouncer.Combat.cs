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
        // Track previous damage values to detect changes
        private Dictionary<uint, uint> _creatureDamage = new Dictionary<uint, uint>();

        // Simple class to hold damage info extracted from a damage event
        private class DamageInfo
        {
            public string SourceName { get; set; }
            public string TargetName { get; set; }
            public int Amount { get; set; }
        }

        /// <summary>
        /// Returns true if currently in Declare Attackers phase.
        /// </summary>
        public bool IsInDeclareAttackersPhase => _currentPhase == "Combat" && _currentStep == "DeclareAttack";

        /// <summary>
        /// Returns true if currently in Declare Blockers phase.
        /// </summary>
        public bool IsInDeclareBlockersPhase => _currentPhase == "Combat" && _currentStep == "DeclareBlock";

        private string BuildDamageAnnouncement(object uxEvent)
        {
            try
            {
                // Log all fields/properties for discovery (once per session)
                LogEventFieldsOnce(uxEvent, "DAMAGE EVENT");

                var damage = GetFieldValue<int>(uxEvent, "DamageAmount");
                if (damage <= 0) return null;

                // Get target info
                var targetId = GetFieldValue<uint>(uxEvent, "TargetId");
                var targetInstanceId = GetFieldValue<uint>(uxEvent, "TargetInstanceId");
                string targetName = GetDamageTargetName(targetId, targetInstanceId);

                // Get source info - try multiple possible field names
                string sourceName = GetDamageSourceName(uxEvent);

                // Get damage flags
                var damageFlags = GetDamageFlags(uxEvent);

                // Build announcement
                var parts = new List<string>();

                string damageText;
                if (!string.IsNullOrEmpty(sourceName))
                {
                    damageText = Strings.Duel_DamageDeals(sourceName, damage, targetName);
                }
                else
                {
                    damageText = Strings.Duel_DamageAmount(damage, targetName);
                }

                // Append damage type modifiers (lifelink, trample, etc.)
                if (!string.IsNullOrEmpty(damageFlags))
                {
                    damageText += " " + damageFlags;
                }

                return damageText;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error building damage announcement: {ex.Message}");
                return null;
            }
        }

        private string GetDamageTargetName(uint targetPlayerId, uint targetInstanceId)
        {
            // If target is a player
            if (targetPlayerId == _localPlayerId)
                return Strings.Duel_DamageToYou;
            if (targetPlayerId != 0)
                return Strings.Duel_DamageToOpponent;

            // Try to find target card by InstanceId
            if (targetInstanceId != 0)
            {
                string cardName = GetCardNameByInstanceId(targetInstanceId);
                if (!string.IsNullOrEmpty(cardName))
                    return cardName;
            }

            return Strings.Duel_DamageTarget;
        }

        private string GetDamageSourceName(object uxEvent)
        {
            // Try various field names for source identification
            string[] sourceInstanceFields = { "SourceInstanceId", "InstigatorInstanceId", "SourceId", "DamageSourceInstanceId" };
            string[] sourceGrpFields = { "SourceGrpId", "InstigatorGrpId", "GrpId", "DamageSourceGrpId" };

            // First try InstanceId-based lookup (finds the actual card on battlefield)
            foreach (var fieldName in sourceInstanceFields)
            {
                var instanceId = GetFieldValue<uint>(uxEvent, fieldName);
                if (instanceId != 0)
                {
                    string name = GetCardNameByInstanceId(instanceId);
                    if (!string.IsNullOrEmpty(name))
                    {
                        DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Found source from {fieldName}: {name}");
                        return name;
                    }
                }
            }

            // Then try GrpId-based lookup (card database ID)
            foreach (var fieldName in sourceGrpFields)
            {
                var grpId = GetFieldValue<uint>(uxEvent, fieldName);
                if (grpId != 0)
                {
                    string name = CardModelProvider.GetNameFromGrpId(grpId);
                    if (!string.IsNullOrEmpty(name))
                    {
                        DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Found source from {fieldName} (GrpId): {name}");
                        return name;
                    }
                }
            }

            // Check if damage is from combat (during CombatDamage step)
            if (_currentPhase == "Combat" && _currentStep == "CombatDamage")
            {
                return Strings.Duel_CombatDamageSource;
            }

            return null;
        }

        private string GetDamageFlags(object uxEvent)
        {
            var flags = new List<string>();

            // Try to detect damage types/flags
            bool isLifelink = GetFieldValue<bool>(uxEvent, "IsLifelink") || GetFieldValue<bool>(uxEvent, "Lifelink");
            bool isTrample = GetFieldValue<bool>(uxEvent, "IsTrample") || GetFieldValue<bool>(uxEvent, "Trample");
            bool isDeathtouch = GetFieldValue<bool>(uxEvent, "IsDeathtouch") || GetFieldValue<bool>(uxEvent, "Deathtouch");
            bool isInfect = GetFieldValue<bool>(uxEvent, "IsInfect") || GetFieldValue<bool>(uxEvent, "Infect");
            bool isCombat = GetFieldValue<bool>(uxEvent, "IsCombatDamage") || GetFieldValue<bool>(uxEvent, "CombatDamage");

            if (isLifelink) flags.Add("lifelink");
            if (isTrample) flags.Add("trample");
            if (isDeathtouch) flags.Add("deathtouch");
            if (isInfect) flags.Add("infect");
            if (isCombat && !(_currentPhase == "Combat" && _currentStep == "CombatDamage"))
                flags.Add("combat");

            return flags.Count > 0 ? $"({string.Join(", ", flags)})" : null;
        }

        /// <summary>
        /// Gets all creatures currently declared as attackers with their name and P/T.
        /// Looks for cards with "IsAttacking" child indicator (existence, not active state).
        /// </summary>
        private List<string> GetAttackingCreaturesInfo()
        {
            var attackers = new List<string>();
            foreach (var go in EnumerateCDCsInHolder("BattlefieldCardHolder"))
            {
                // Model-based check (authoritative), with UI child fallback
                // Matches CombatNavigator.GetCombatStateText approach
                bool isAttacking = CardStateProvider.GetIsAttackingFromCard(go);
                if (!isAttacking)
                {
                    foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                    {
                        if (child.name == "IsAttacking" && child.gameObject.activeInHierarchy)
                        {
                            isAttacking = true;
                            break;
                        }
                    }
                }

                if (isAttacking)
                {
                    var info = CardDetector.ExtractCardInfo(go);
                    string attackerInfo = info.Name ?? "Unknown";
                    if (!string.IsNullOrEmpty(info.PowerToughness))
                    {
                        attackerInfo += $" {info.PowerToughness}";
                    }
                    attackers.Add(attackerInfo);
                }
            }
            return attackers;
        }

        private string BuildCombatAnnouncement(object uxEvent)
        {
            try
            {
                var type = uxEvent.GetType();
                var typeName = type.Name;

                if (typeName == "ToggleCombatUXEvent")
                {
                    var combatModeField = type.GetField("_CombatMode", PrivateInstance);
                    var modeValue = combatModeField?.GetValue(uxEvent)?.ToString();
                    if (modeValue == "CombatBegun") return Strings.Duel_CombatBegins;
                    return null;
                }

                if (typeName == "AttackLobUXEvent")
                {
                    // Debug: Log all fields once to discover available data
                    LogEventFieldsOnce(uxEvent, "AttackLobUXEvent");
                    return BuildAttackerDeclaredAnnouncement(uxEvent);
                }
                if (typeName == "AttackDecrementUXEvent") return Strings.Duel_AttackerRemoved;

                return null;
            }
            catch
            {
                return null;
            }
        }

        // Track if we've logged AttackLobUXEvent fields (one-time debug)

        private string BuildAttackerDeclaredAnnouncement(object uxEvent)
        {
            try
            {
                // Get attacker InstanceId from _attackerId field
                var attackerId = GetFieldValue<uint>(uxEvent, "_attackerId");

                string cardName = null;
                string powerToughness = null;
                bool isOpponent = false;

                // Look up card by InstanceId
                if (attackerId != 0)
                {
                    cardName = GetCardNameByInstanceId(attackerId);

                    // Get P/T and ownership from the card model
                    var (power, toughness, isOpp) = GetCardPowerToughnessAndOwnerByInstanceId(attackerId);
                    if (power >= 0 && toughness >= 0)
                    {
                        powerToughness = $"{power}/{toughness}";
                    }
                    isOpponent = isOpp;
                }

                // Build announcement with ownership prefix for opponent's attackers
                string ownerPrefix = isOpponent ? Strings.Duel_OwnerPrefix_Opponent : "";

                if (!string.IsNullOrEmpty(cardName))
                {
                    if (!string.IsNullOrEmpty(powerToughness))
                        return Strings.Duel_AttackingPT($"{ownerPrefix}{cardName}", powerToughness);
                    return Strings.Duel_Attacking($"{ownerPrefix}{cardName}");
                }

                return isOpponent ? Strings.Duel_OpponentAttackerDeclared : Strings.Duel_AttackerDeclared;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error building attacker announcement: {ex.Message}");
                return Strings.Duel_AttackerDeclared;
            }
        }

        private (int power, int toughness, bool isOpponent) GetCardPowerToughnessAndOwnerByInstanceId(uint instanceId)
        {
            if (instanceId == 0) return (-1, -1, false);

            try
            {
                string[] holders = { "BattlefieldCardHolder", "StackCardHolder" };
                foreach (var holderName in holders)
                {
                    foreach (var go in EnumerateCDCsInHolder(holderName))
                    {
                        var cdcComponent = CardModelProvider.GetDuelSceneCDC(go);
                        if (cdcComponent == null) continue;

                        var model = CardModelProvider.GetCardModel(cdcComponent);
                        if (model == null) continue;

                        var cid = GetFieldValue<uint>(model, "InstanceId");
                        if (cid != instanceId) continue;

                        // Power/Toughness are StringBackedInt properties, not plain int
                        var powerObj = GetFieldValue<object>(model, "Power");
                        var toughnessObj = GetFieldValue<object>(model, "Toughness");
                        string powerStr = CardModelProvider.GetStringBackedIntValue(powerObj);
                        string toughStr = CardModelProvider.GetStringBackedIntValue(toughnessObj);
                        int power = powerStr != null && int.TryParse(powerStr, out int p) ? p : -1;
                        int toughness = toughStr != null && int.TryParse(toughStr, out int t) ? t : -1;

                        var controller = GetFieldValue<object>(model, "ControllerNum");
                        bool isOpponent = controller?.ToString() == "Opponent";

                        return (power, toughness, isOpponent);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error getting P/T and owner by InstanceId: {ex.Message}");
            }

            return (-1, -1, false);
        }

        private string HandleCardModelUpdate(object uxEvent)
        {
            try
            {
                // Log fields for discovery (once)
                LogEventFieldsOnce(uxEvent, "CARD MODEL UPDATE");

                // Try to extract card instance and damage info
                var instanceId = GetFieldValue<uint>(uxEvent, "InstanceId");
                var damage = GetFieldValue<uint>(uxEvent, "Damage");
                var grpId = GetFieldValue<uint>(uxEvent, "GrpId");

                // Check if damage changed
                if (instanceId != 0 && damage > 0)
                {
                    uint previousDamage = 0;
                    _creatureDamage.TryGetValue(instanceId, out previousDamage);

                    if (damage != previousDamage)
                    {
                        _creatureDamage[instanceId] = damage;
                        uint damageDealt = damage - previousDamage;

                        if (damageDealt > 0)
                        {
                            string cardName = grpId != 0 ? CardModelProvider.GetNameFromGrpId(grpId) : null;
                            if (!string.IsNullOrEmpty(cardName))
                            {
                                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Creature damage: {cardName} now has {damage} damage (dealt: {damageDealt})");

                                // Try to correlate with last resolving card
                                if (!string.IsNullOrEmpty(_lastResolvingCardName))
                                {
                                    return Strings.Duel_DamageDeals(_lastResolvingCardName, (int)damageDealt, cardName);
                                }
                                return Strings.Duel_DamageAmount((int)damageDealt, cardName);
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Error handling card model update: {ex.Message}");
                return null;
            }
        }

        private string HandleCombatFrame(object uxEvent)
        {
            try
            {
                // Log fields for discovery (once)
                LogEventFieldsOnce(uxEvent, "COMBAT FRAME");

                var announcements = new List<string>();

                // Log total damage for analysis
                var opponentDamage = GetFieldValue<int>(uxEvent, "OpponentDamageDealt");
                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"CombatFrame: OpponentDamageDealt={opponentDamage}");

                // Check both branch lists - _branches and _runningBranches
                var branches = GetFieldValue<object>(uxEvent, "_branches");
                var runningBranches = GetFieldValue<object>(uxEvent, "_runningBranches");

                // Log counts for investigation
                int branchCount = 0;
                int runningCount = 0;
                if (branches is System.Collections.IEnumerable bList)
                    foreach (var _ in bList) branchCount++;
                if (runningBranches is System.Collections.IEnumerable rList)
                    foreach (var _ in rList) runningCount++;
                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Branch counts: _branches={branchCount}, _runningBranches={runningCount}");
                if (branches != null)
                {
                    var branchList = branches as System.Collections.IEnumerable;
                    if (branchList != null)
                    {
                        int branchIndex = 0;
                        foreach (var branch in branchList)
                        {
                            if (branch == null) continue;

                            // Get damage chain from this branch (attacker + blocker if present)
                            var damageChain = ExtractDamageChain(branch);

                            // Log for debugging
                            foreach (var dmg in damageChain)
                            {
                                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Branch[{branchIndex}]: {dmg.SourceName} -> {dmg.TargetName}, Amount={dmg.Amount}");
                            }

                            // Build grouped announcement for this combat pair
                            if (damageChain.Count == 1)
                            {
                                // Single damage (unblocked or one-sided)
                                var dmg = damageChain[0];
                                if (dmg.Amount > 0 && !string.IsNullOrEmpty(dmg.SourceName) && !string.IsNullOrEmpty(dmg.TargetName))
                                {
                                    announcements.Add(Strings.Duel_DamageDeals(dmg.SourceName, dmg.Amount, dmg.TargetName));
                                }
                            }
                            else if (damageChain.Count >= 2)
                            {
                                // Combat trade - group attacker and blocker damage together
                                var parts = new List<string>();
                                foreach (var dmg in damageChain)
                                {
                                    if (dmg.Amount > 0 && !string.IsNullOrEmpty(dmg.SourceName) && !string.IsNullOrEmpty(dmg.TargetName))
                                    {
                                        parts.Add(Strings.Duel_DamageDeals(dmg.SourceName, dmg.Amount, dmg.TargetName));
                                    }
                                }
                                if (parts.Count > 0)
                                {
                                    announcements.Add(string.Join(", ", parts));
                                }
                            }
                            branchIndex++;
                        }
                        DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Total branches: {branchIndex}");
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
                MelonLogger.Warning($"[DuelAnnouncer] Error handling combat frame: {ex.Message}");
                return null;
            }
        }

        // Extract damage info from a single damage event
        private DamageInfo ExtractDamageInfo(object damageEvent)
        {
            if (damageEvent == null) return null;

            var info = new DamageInfo();
            info.Amount = GetFieldValue<int>(damageEvent, "Amount");

            // Get source info
            var source = GetFieldValue<object>(damageEvent, "Source");
            if (source != null)
            {
                var sourceGrpId = GetNestedPropertyValue<uint>(source, "GrpId");
                if (sourceGrpId != 0)
                {
                    info.SourceName = CardModelProvider.GetNameFromGrpId(sourceGrpId);
                }
            }

            // Get target info
            var target = GetFieldValue<object>(damageEvent, "Target");
            if (target != null)
            {
                var targetStr = target.ToString();
                if (targetStr.Contains("LocalPlayer"))
                {
                    info.TargetName = Strings.Duel_DamageToYou;
                }
                else if (targetStr.Contains("Opponent"))
                {
                    info.TargetName = Strings.Duel_DamageToOpponent;
                }
                else
                {
                    var targetGrpId = GetNestedPropertyValue<uint>(target, "GrpId");
                    if (targetGrpId != 0)
                    {
                        info.TargetName = CardModelProvider.GetNameFromGrpId(targetGrpId);
                    }
                }
            }

            return info;
        }

        // Extract all damage in a branch chain (follows _nextBranch for blocker damage)
        private List<DamageInfo> ExtractDamageChain(object branch)
        {
            var chain = new List<DamageInfo>();
            var currentBranch = branch;
            int depth = 0;

            // Log BranchDepth from first branch
            var branchDepth = GetNestedPropertyValue<int>(branch, "BranchDepth");
            DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Chain BranchDepth={branchDepth}");

            while (currentBranch != null)
            {
                var damageEvent = GetFieldValue<object>(currentBranch, "_damageEvent");
                if (damageEvent != null)
                {
                    var info = ExtractDamageInfo(damageEvent);
                    if (info != null)
                    {
                        chain.Add(info);
                    }
                }

                // Check what _nextBranch contains
                var nextBranch = GetFieldValue<object>(currentBranch, "_nextBranch");
                DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Chain depth {depth}: _nextBranch={(nextBranch != null ? "exists" : "null")}");

                // Follow the chain to get blocker damage
                currentBranch = nextBranch;
                depth++;

                // Safety limit
                if (depth > 10) break;
            }

            DebugConfig.LogIf(DebugConfig.LogAnnouncements, "DuelAnnouncer", $"Chain total depth: {depth}");
            return chain;
        }
    }
}
