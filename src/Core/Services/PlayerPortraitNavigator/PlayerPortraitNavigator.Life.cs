using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Utils;
using AccessibleArena.Core.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Life totals, counter suffixes, effects (designations/abilities/dungeon),
    /// MtgPlayer lookup, and MtgEntity/MtgPlayer reflection caching.
    /// </summary>
    public partial class PlayerPortraitNavigator
    {
        // MtgEntity/MtgPlayer reflection cache (counters, designations, abilities, dungeon)
        private sealed class EntityHandles
        {
            public FieldInfo Counters;       // MtgEntity.Counters (Dictionary<CounterType, int>)
            public FieldInfo Designations;   // MtgEntity.Designations (List<DesignationData>)
            public FieldInfo Abilities;      // MtgEntity.Abilities (List<AbilityPrintingData>)
            public FieldInfo DungeonState;   // MtgPlayer.DungeonState (DungeonData)
        }

        // Counters/Designations/Abilities live on MtgEntity (base); DungeonState on MtgPlayer (derived).
        // ReflectionWalk handles both — public fields on a base class are reachable from the derived type,
        // and the walk is uniform across all four handles.
        private static readonly ReflectionCache<EntityHandles> _entityCache = new ReflectionCache<EntityHandles>(
            builder: t => new EntityHandles
            {
                Counters = ReflectionWalk.FindField(t, "Counters", PublicInstance),
                Designations = ReflectionWalk.FindField(t, "Designations", PublicInstance),
                Abilities = ReflectionWalk.FindField(t, "Abilities", PublicInstance),
                DungeonState = ReflectionWalk.FindField(t, "DungeonState", PublicInstance),
            },
            validator: _ => true,
            logTag: "PlayerPortrait",
            logSubject: "Entity");

        private void AnnounceLifeTotals()
        {
            var (localLife, opponentLife) = GetLifeTotals();

            string localText = BuildLifeWithCounters(localLife, false);
            string opponentText = BuildLifeWithCounters(opponentLife, true);

            string announcement;
            if (localLife >= 0 && opponentLife >= 0)
            {
                announcement = $"{Strings.You} {localText}. {Strings.Opponent} {opponentText}";
            }
            else if (localLife >= 0)
            {
                announcement = $"{Strings.You} {localText}. {Strings.Opponent} {Strings.LifeNotAvailable}";
            }
            else if (opponentLife >= 0)
            {
                announcement = $"{Strings.You} {Strings.LifeNotAvailable}. {Strings.Opponent} {opponentText}";
            }
            else
            {
                announcement = Strings.LifeNotAvailable;
            }

            // High priority so repeated L presses always re-announce (bypasses duplicate suppression)
            _announcer.Announce(announcement, AnnouncementPriority.High);
        }

        /// <summary>
        /// Builds a life string with optional counter suffix.
        /// E.g. "20 life, 3 Poison, 4 Energy" or just "20 life" if no counters.
        /// </summary>
        private string BuildLifeWithCounters(int life, bool isOpponent)
        {
            if (life < 0) return Strings.LifeNotAvailable;

            string lifeText = Strings.Life(life);
            var player = GetMtgPlayer(isOpponent);
            if (player == null) return lifeText;

            var counters = GetPlayerCounters(player);
            string counterSuffix = FormatCountersForLife(counters);
            if (string.IsNullOrEmpty(counterSuffix)) return lifeText;

            return $"{lifeText}, {counterSuffix}";
        }

        /// <summary>
        /// Gets life totals from GameManager's game state.
        /// Returns (localLife, opponentLife), -1 if not found.
        /// </summary>
        private (int localLife, int opponentLife) GetLifeTotals()
        {
            int localLife = -1;
            int opponentLife = -1;

            try
            {
                // Find GameManager
                MonoBehaviour gameManager = null;
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == T.GameManager)
                    {
                        gameManager = mb;
                        break;
                    }
                }

                if (gameManager == null)
                {
                    Log.Nav("PlayerPortrait", $"GameManager not found");
                    return (-1, -1);
                }

                var gmType = gameManager.GetType();

                // Try CurrentGameState first, then LatestGameState
                object gameState = null;
                var currentStateProp = gmType.GetProperty("CurrentGameState");
                if (currentStateProp != null)
                {
                    gameState = currentStateProp.GetValue(gameManager);
                }

                if (gameState == null)
                {
                    var latestStateProp = gmType.GetProperty("LatestGameState");
                    if (latestStateProp != null)
                    {
                        gameState = latestStateProp.GetValue(gameManager);
                    }
                }

                if (gameState == null)
                {
                    Log.Nav("PlayerPortrait", $"GameState not available");
                    return (-1, -1);
                }

                // Get LocalPlayer and Opponent directly from game state
                var gsType = gameState.GetType();

                // Get local player life
                var localPlayerProp = gsType.GetProperty("LocalPlayer");
                if (localPlayerProp != null)
                {
                    var localPlayer = localPlayerProp.GetValue(gameState);
                    if (localPlayer != null)
                    {
                        localLife = GetPlayerLife(localPlayer);
                        Log.Nav("PlayerPortrait", $"Local player life: {localLife}");
                    }
                }

                // Get opponent life
                var opponentProp = gsType.GetProperty("Opponent");
                if (opponentProp != null)
                {
                    var opponent = opponentProp.GetValue(gameState);
                    if (opponent != null)
                    {
                        opponentLife = GetPlayerLife(opponent);
                        Log.Nav("PlayerPortrait", $"Opponent life: {opponentLife}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Warn("PlayerPortrait", $"Error getting life totals: {ex.Message}");
            }

            return (localLife, opponentLife);
        }

        /// <summary>
        /// Extracts life total from an MtgPlayer object.
        /// </summary>
        private int GetPlayerLife(object player)
        {
            if (player == null) return -1;

            var playerType = player.GetType();
            var bindingFlags = AllInstanceFlags;

            // Try various property names for life
            string[] lifeNames = { "LifeTotal", "Life", "CurrentLife", "StartingLife", "_life", "_lifeTotal", "life", "lifeTotal" };

            // Check properties first
            foreach (var propName in lifeNames)
            {
                var lifeProp = playerType.GetProperty(propName, bindingFlags);
                if (lifeProp != null)
                {
                    try
                    {
                        var lifeVal = lifeProp.GetValue(player);
                        if (lifeVal != null)
                        {
                            if (lifeVal is int intLife) return intLife;
                            if (int.TryParse(lifeVal.ToString(), out int parsed)) return parsed;
                        }
                    }
                    catch { /* Life property may not exist on all player types */ }
                }
            }

            // Check fields
            foreach (var fieldName in lifeNames)
            {
                var lifeField = playerType.GetField(fieldName, bindingFlags);
                if (lifeField != null)
                {
                    try
                    {
                        var lifeVal = lifeField.GetValue(player);
                        if (lifeVal != null)
                        {
                            if (lifeVal is int intLife) return intLife;
                            if (int.TryParse(lifeVal.ToString(), out int parsed)) return parsed;
                        }
                    }
                    catch { /* Life field may not exist on all player types */ }
                }
            }

            // Log all properties and fields for debugging
            Log.Nav("PlayerPortrait", $"MtgPlayer properties:");
            foreach (var prop in playerType.GetProperties(bindingFlags))
            {
                try
                {
                    var val = prop.GetValue(player);
                    Log.Nav("PlayerPortrait", $"  Prop {prop.Name}: {val}");
                }
                catch { /* Some properties throw on access; skip for debug dump */ }
            }

            Log.Nav("PlayerPortrait", $"MtgPlayer fields:");
            foreach (var field in playerType.GetFields(bindingFlags))
            {
                try
                {
                    var val = field.GetValue(player);
                    var valStr = val?.ToString() ?? "null";
                    if (valStr.Length > 50) valStr = valStr.Substring(0, 50) + "...";
                    Log.Nav("PlayerPortrait", $"  Field {field.Name}: {valStr}");
                }
                catch { /* Some fields throw on access; skip for debug dump */ }
            }

            return -1;
        }

        /// <summary>
        /// Gets the raw MtgPlayer object for a player via GameManager -> GameState -> LocalPlayer/Opponent.
        /// Returns null if not available.
        /// </summary>
        private object GetMtgPlayer(bool isOpponent)
        {
            try
            {
                MonoBehaviour gameManager = null;
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == T.GameManager)
                    {
                        gameManager = mb;
                        break;
                    }
                }
                if (gameManager == null) return null;

                var gmType = gameManager.GetType();
                object gameState = gmType.GetProperty("CurrentGameState")?.GetValue(gameManager);
                if (gameState == null)
                    gameState = gmType.GetProperty("LatestGameState")?.GetValue(gameManager);
                if (gameState == null) return null;

                var gsType = gameState.GetType();
                string propName = isOpponent ? "Opponent" : "LocalPlayer";
                return gsType.GetProperty(propName)?.GetValue(gameState);
            }
            catch (Exception ex)
            {
                Log.Nav("PlayerPortrait", $"Error getting MtgPlayer: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets player counters (poison, energy, experience, etc.) from MtgEntity.Counters.
        /// Returns list of (typeName, count) tuples with count > 0.
        /// </summary>
        private List<(string typeName, int count)> GetPlayerCounters(object player)
        {
            var result = new List<(string, int)>();
            if (player == null) return result;

            if (!_entityCache.EnsureInitialized(player.GetType())) return result;
            var h = _entityCache.Handles;
            if (h.Counters == null) return result;

            try
            {
                var countersObj = h.Counters.GetValue(player);
                if (countersObj == null) return result;

                // Iterate via IEnumerable (Dictionary<CounterType, int>)
                var enumerable = countersObj as IEnumerable;
                if (enumerable == null) return result;

                foreach (var entry in enumerable)
                {
                    var entryType = entry.GetType();
                    var keyProp = entryType.GetProperty("Key");
                    var valueProp = entryType.GetProperty("Value");
                    if (keyProp == null || valueProp == null) continue;

                    var key = keyProp.GetValue(entry);
                    int count = (int)valueProp.GetValue(entry);

                    if (count > 0)
                    {
                        result.Add((CardStateProvider.GetLocalizedCounterTypeName(key), count));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Nav("PlayerPortrait", $"Error reading player counters: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Formats a list of counters as a comma-separated suffix for life announcements.
        /// E.g. "3 Poison, 4 Energy". Returns empty string if no counters.
        /// </summary>
        private static string FormatCountersForLife(List<(string typeName, int count)> counters)
        {
            if (counters == null || counters.Count == 0) return "";
            var parts = new List<string>();
            foreach (var (typeName, count) in counters)
            {
                parts.Add(Strings.LifeCounter(count, typeName));
            }
            return string.Join(", ", parts);
        }

        /// <summary>
        /// Gets player effects (designations, abilities, dungeon state) for the Effects property.
        /// Returns a formatted string, or Strings.NoActiveEffects if nothing active.
        /// </summary>
        private string GetPlayerEffects(bool isOpponent)
        {
            var player = GetMtgPlayer(isOpponent);
            if (player == null) return Strings.NoActiveEffects;

            if (!_entityCache.EnsureInitialized(player.GetType())) return Strings.NoActiveEffects;
            var h = _entityCache.Handles;

            var parts = new List<string>();

            // Read designations
            try
            {
                if (h.Designations != null)
                {
                    var designations = h.Designations.GetValue(player) as IList;
                    if (designations != null && designations.Count > 0)
                    {
                        foreach (var desig in designations)
                        {
                            var desigType = desig.GetType();
                            var typeField = desigType.GetField("Type");
                            if (typeField == null) continue;

                            string typeName = typeField.GetValue(desig).ToString();
                            string text = FormatDesignation(typeName, desig, desigType);
                            if (text != null)
                                parts.Add(text);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Nav("PlayerPortrait", $"Error reading designations: {ex.Message}");
            }

            // Read abilities
            try
            {
                if (h.Abilities != null)
                {
                    var abilities = h.Abilities.GetValue(player) as IList;
                    if (abilities != null && abilities.Count > 0)
                    {
                        int abilityCount = 0;
                        foreach (var ability in abilities)
                        {
                            var abilityType = ability.GetType();
                            var idProp = abilityType.GetProperty("Id", PublicInstance);
                            if (idProp == null) continue;

                            uint abilityId = (uint)idProp.GetValue(ability);
                            string abilityText = CardTextProvider.GetAbilityText(
                                ability, abilityType, 0, abilityId, System.Array.Empty<uint>(), 0);

                            if (!string.IsNullOrEmpty(abilityText))
                            {
                                parts.Add(abilityText);
                            }
                            else
                            {
                                abilityCount++;
                            }
                        }

                        // If some abilities had no text, report count
                        if (abilityCount > 0 && parts.Count == 0)
                        {
                            parts.Add(Strings.PlayerAbilityCount(abilityCount));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Nav("PlayerPortrait", $"Error reading abilities: {ex.Message}");
            }

            // Read dungeon state
            try
            {
                if (h.DungeonState != null)
                {
                    var dungeonState = h.DungeonState.GetValue(player);
                    if (dungeonState != null)
                    {
                        var dsType = dungeonState.GetType();
                        var dungeonGrpIdField = dsType.GetField("DungeonGrpId");
                        var currentRoomField = dsType.GetField("CurrentRoomGrpId");
                        var completedField = dsType.GetField("CompletedDungeons");

                        if (dungeonGrpIdField != null)
                        {
                            uint dungeonGrpId = (uint)dungeonGrpIdField.GetValue(dungeonState);
                            if (dungeonGrpId > 0)
                            {
                                string dungeonName = CardModelProvider.GetNameFromGrpId(dungeonGrpId) ?? dungeonGrpId.ToString();
                                string roomName = null;
                                if (currentRoomField != null)
                                {
                                    uint roomGrpId = (uint)currentRoomField.GetValue(dungeonState);
                                    if (roomGrpId > 0)
                                        roomName = CardModelProvider.GetNameFromGrpId(roomGrpId) ?? roomGrpId.ToString();
                                }
                                parts.Add(Strings.DungeonStatus(dungeonName, roomName ?? "?"));
                            }
                        }

                        if (completedField != null)
                        {
                            var completed = completedField.GetValue(dungeonState) as uint[];
                            if (completed != null && completed.Length > 0)
                            {
                                parts.Add(Strings.DungeonsCompleted(completed.Length));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Nav("PlayerPortrait", $"Error reading dungeon state: {ex.Message}");
            }

            if (parts.Count == 0)
                return Strings.NoActiveEffects;

            string label = $"{Strings.PlayerEffects}: ";
            return label + string.Join(". ", parts);
        }

        /// <summary>
        /// Formats a Designation enum value into a localized display string.
        /// Returns null for card-level designations that aren't relevant to player display.
        /// </summary>
        private static string FormatDesignation(string typeName, object desig, System.Type desigType)
        {
            switch (typeName)
            {
                case "Monarch":
                    return Strings.DesignationMonarch;
                case "PlayerSpeed":
                    var valueField = desigType.GetField("Value");
                    if (valueField != null)
                    {
                        var val = valueField.GetValue(desig);
                        if (val is uint speedVal && speedVal > 0)
                            return Strings.DesignationSpeed((int)speedVal);
                    }
                    return Strings.DesignationSpeed(0);
                case "Day":
                    return Strings.DesignationDay;
                case "Night":
                    return Strings.DesignationNight;
                case "CitysBlessing":
                    return Strings.DesignationCitysBlessing;
                default:
                    // Skip card-level designations (Commander, Companion, Monstrous, Renowned, etc.)
                    return null;
            }
        }
    }
}
