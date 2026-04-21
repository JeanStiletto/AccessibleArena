using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Utils;
using AccessibleArena.Core.Models;
using System;
using System.Collections;
using System.Reflection;
using TMPro;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Property cycling (Life/Effects/Timer/Timeouts/Wins/Rank), visibility filtering,
    /// rank lookup, matchup text, and player username extraction.
    /// </summary>
    public partial class PlayerPortraitNavigator
    {
        // Property list for cycling (Username merged into Life announcement)
        private enum PlayerProperty { Life, Effects, Timer, Timeouts, Wins, Rank }
        private const int PropertyCount = 6;

        // Rank reflection caches (3-type chain: GameManager → MatchManager → PlayerInfo).
        // Each type is only reachable via a live instance from the previous step, so three
        // caches seeded independently at point of use (Commander precedent).
        private sealed class GameManagerRankHandles { public PropertyInfo MatchManager; }
        private sealed class MatchManagerRankHandles
        {
            public PropertyInfo LocalPlayerInfo;
            public PropertyInfo OpponentInfo;
        }
        private sealed class PlayerInfoRankHandles
        {
            public FieldInfo RankingClass;
            public FieldInfo RankingTier;
            public FieldInfo MythicPercentile;
            public FieldInfo MythicPlacement;
        }

        private static readonly ReflectionCache<GameManagerRankHandles> _gmRankCache = new ReflectionCache<GameManagerRankHandles>(
            builder: t => new GameManagerRankHandles { MatchManager = t.GetProperty("MatchManager", PublicInstance) },
            validator: h => h.MatchManager != null,
            logTag: "PlayerPortrait",
            logSubject: "GameManager");

        private static readonly ReflectionCache<MatchManagerRankHandles> _mmRankCache = new ReflectionCache<MatchManagerRankHandles>(
            builder: t => new MatchManagerRankHandles
            {
                LocalPlayerInfo = t.GetProperty("LocalPlayerInfo", PublicInstance),
                OpponentInfo = t.GetProperty("OpponentInfo", PublicInstance),
            },
            validator: h => h.LocalPlayerInfo != null,
            logTag: "PlayerPortrait",
            logSubject: "MatchManager");

        private static readonly ReflectionCache<PlayerInfoRankHandles> _piRankCache = new ReflectionCache<PlayerInfoRankHandles>(
            builder: t => new PlayerInfoRankHandles
            {
                RankingClass = t.GetField("RankingClass", AllInstanceFlags),
                RankingTier = t.GetField("RankingTier", AllInstanceFlags),
                MythicPercentile = t.GetField("MythicPercentile", AllInstanceFlags),
                MythicPlacement = t.GetField("MythicPlacement", AllInstanceFlags),
            },
            validator: h => h.RankingClass != null,
            logTag: "PlayerPortrait",
            logSubject: "PlayerInfo");

        /// <summary>
        /// Gets the display value for a property for the current player.
        /// </summary>
        private string GetPropertyValue(PlayerProperty property)
        {
            bool isOpponent = _currentPlayerIndex == 1;

            switch (property)
            {
                case PlayerProperty.Life:
                    var (localLife, opponentLife) = GetLifeTotals();
                    int life = isOpponent ? opponentLife : localLife;
                    string lifeWithCounters = BuildLifeWithCounters(life, isOpponent);
                    // Include username in life announcement
                    string username = GetPlayerUsername(isOpponent);
                    if (!string.IsNullOrEmpty(username))
                    {
                        return $"{username}, {lifeWithCounters}";
                    }
                    return lifeWithCounters;

                case PlayerProperty.Effects:
                    return GetPlayerEffects(isOpponent);

                case PlayerProperty.Timer:
                    var timerStr = GetTimerFromModel(isOpponent);
                    if (timerStr != null)
                        return Strings.Timer(timerStr);
                    var ropeInfo = GetRopeTimerFromModel(isOpponent);
                    if (ropeInfo != null)
                        return Strings.Timer(ropeInfo.Value.timerText);
                    return Strings.TimerNoMatchClock;

                case PlayerProperty.Timeouts:
                    var timeoutCount = GetTimeoutCount(isOpponent ? "Opponent" : "LocalPlayer");
                    return timeoutCount >= 0 ? Strings.Timeouts(timeoutCount) : Strings.Timeouts(0);

                case PlayerProperty.Wins:
                    var wins = GetWinCount(isOpponent);
                    return wins >= 0 ? Strings.GamesWon(wins) : Strings.WinsNotAvailable;

                case PlayerProperty.Rank:
                    var rank = GetPlayerRank(isOpponent);
                    return !string.IsNullOrEmpty(rank) ? Strings.Rank(rank) : Strings.RankNotAvailable;

                default:
                    return "Unknown property";
            }
        }

        /// <summary>
        /// Checks whether a property has meaningful content for at least one player.
        /// If neither player has content, the property row should be skipped during navigation.
        /// </summary>
        private bool IsPropertyVisible(PlayerProperty property)
        {
            // Life is always visible
            if (property == PlayerProperty.Life) return true;

            switch (property)
            {
                case PlayerProperty.Effects:
                    // Visible if either player has active effects
                    return HasEffectsContent(false) || HasEffectsContent(true);

                case PlayerProperty.Timer:
                    // Visible if either player has a match clock or rope timer
                    return GetTimerFromModel(false) != null || GetTimerFromModel(true) != null
                        || GetRopeTimerFromModel(false) != null || GetRopeTimerFromModel(true) != null;

                case PlayerProperty.Wins:
                    // Visible if either player has won a game (Bo3)
                    return GetWinCount(false) > 0 || GetWinCount(true) > 0;

                case PlayerProperty.Rank:
                    // Visible if either player has rank info
                    return !string.IsNullOrEmpty(GetPlayerRank(false)) || !string.IsNullOrEmpty(GetPlayerRank(true));

                default:
                    // Timeouts and any others: always visible
                    return true;
            }
        }

        /// <summary>
        /// Checks if a player has active effects (designations, abilities, or dungeon state).
        /// Cheaper than building the full effects string.
        /// </summary>
        private bool HasEffectsContent(bool isOpponent)
        {
            var player = GetMtgPlayer(isOpponent);
            if (player == null) return false;

            if (!_entityCache.EnsureInitialized(player.GetType())) return false;
            var h = _entityCache.Handles;

            try
            {
                if (h.Designations != null)
                {
                    var designations = h.Designations.GetValue(player) as IList;
                    if (designations != null)
                    {
                        foreach (var desig in designations)
                        {
                            var typeField = desig.GetType().GetField("Type");
                            if (typeField == null) continue;
                            string typeName = typeField.GetValue(desig).ToString();
                            if (FormatDesignation(typeName, desig, desig.GetType()) != null)
                                return true;
                        }
                    }
                }

                if (h.Abilities != null)
                {
                    var abilities = h.Abilities.GetValue(player) as IList;
                    if (abilities != null && abilities.Count > 0)
                        return true;
                }

                if (h.DungeonState != null)
                {
                    var dungeonState = h.DungeonState.GetValue(player);
                    if (dungeonState != null)
                    {
                        var dungeonGrpIdField = dungeonState.GetType().GetField("DungeonGrpId");
                        if (dungeonGrpIdField != null)
                        {
                            uint dungeonGrpId = (uint)dungeonGrpIdField.GetValue(dungeonState);
                            if (dungeonGrpId > 0) return true;
                        }
                        var completedField = dungeonState.GetType().GetField("CompletedDungeons");
                        if (completedField != null)
                        {
                            var completed = completedField.GetValue(dungeonState) as uint[];
                            if (completed != null && completed.Length > 0) return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Nav("PlayerPortrait", $"Error checking effects content: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Finds the next visible property index when navigating forward or backward.
        /// Returns -1 if no visible property exists in the given direction.
        /// </summary>
        private int FindNextVisibleProperty(int currentIndex, bool forward)
        {
            int step = forward ? 1 : -1;
            int next = currentIndex + step;

            while (next >= 0 && next < PropertyCount)
            {
                if (IsPropertyVisible((PlayerProperty)next))
                    return next;
                next += step;
            }

            return -1; // No visible property found
        }

        /// <summary>
        /// Gets win count for Bo3 matches. Returns 0 for Bo1 games.
        /// </summary>
        private int GetWinCount(bool isOpponent)
        {
            // In Bo1 games, there are no win pips - default to 0
            // For Bo3, we'd need to find the actual match win indicator
            // For now, return 0 as a sensible default (no games won yet in current match)
            return 0;
        }

        /// <summary>
        /// Gets player rank from GameManager.MatchManager player info.
        /// </summary>
        private string GetPlayerRank(bool isOpponent)
        {
            try
            {
                // Find GameManager (same pattern as GetLifeTotals)
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

                if (!_gmRankCache.EnsureInitialized(gameManager.GetType())) return null;

                var matchManager = _gmRankCache.Handles.MatchManager.GetValue(gameManager);
                if (matchManager == null) return null;

                if (!_mmRankCache.EnsureInitialized(matchManager.GetType())) return null;
                var mm = _mmRankCache.Handles;

                var infoProp = isOpponent ? mm.OpponentInfo : mm.LocalPlayerInfo;
                if (infoProp == null) return null;

                var playerInfo = infoProp.GetValue(matchManager);
                if (playerInfo == null) return null;

                if (!_piRankCache.EnsureInitialized(playerInfo.GetType())) return null;
                var pi = _piRankCache.Handles;

                // Read RankingClass enum value (None=-1, Spark=0, Bronze=1, Silver=2, Gold=3, Platinum=4, Diamond=5, Master=6, Mythic=7)
                int rankingClass = Convert.ToInt32(pi.RankingClass.GetValue(playerInfo));

                if (rankingClass <= 0) return "Unranked";

                // Mythic rank
                if (rankingClass == 7)
                {
                    int placement = pi.MythicPlacement != null ? Convert.ToInt32(pi.MythicPlacement.GetValue(playerInfo)) : 0;
                    if (placement > 0)
                        return $"Mythic #{placement}";

                    float percentile = pi.MythicPercentile != null ? Convert.ToSingle(pi.MythicPercentile.GetValue(playerInfo)) : 0f;
                    if (percentile > 0f)
                        return $"Mythic {percentile:0}%";

                    return "Mythic";
                }

                // Standard ranks with tier
                string rankName;
                switch (rankingClass)
                {
                    case 1: rankName = "Bronze"; break;
                    case 2: rankName = "Silver"; break;
                    case 3: rankName = "Gold"; break;
                    case 4: rankName = "Platinum"; break;
                    case 5: rankName = "Diamond"; break;
                    case 6: rankName = "Master"; break;
                    default: rankName = $"Rank {rankingClass}"; break;
                }

                int tier = pi.RankingTier != null ? Convert.ToInt32(pi.RankingTier.GetValue(playerInfo)) : 0;
                if (tier > 0)
                    return $"{rankName} Tier {tier}";

                return rankName;
            }
            catch (Exception ex)
            {
                Log.Nav("PlayerPortrait", $"Error getting rank: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Returns a matchup string for the current duel (e.g. "blindndangerous vs Opponent").
        /// Returns null if either name is unavailable.
        /// </summary>
        public string GetMatchupText()
        {
            string local = GetPlayerUsername(false);
            string opponent = GetPlayerUsername(true);
            if (string.IsNullOrEmpty(local) || string.IsNullOrEmpty(opponent))
                return null;
            return Strings.Duel_Matchup(local, opponent);
        }

        /// <summary>
        /// Gets player username from PlayerNameView.
        /// </summary>
        private string GetPlayerUsername(bool isOpponent)
        {
            string containerName = isOpponent ? "Opponent" : "LocalPlayer";
            Log.Nav("PlayerPortrait", $"Looking for username for {containerName}");

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                // Look for PlayerNameView objects (e.g., LocalPlayerNameView_Desktop_16x9(Clone))
                if (go.name.Contains(containerName) && go.name.Contains("NameView"))
                {
                    Log.Nav("PlayerPortrait", $"Found NameView: {go.name}");

                    // Log all children and their text
                    foreach (Transform child in go.transform)
                    {
                        Log.Nav("PlayerPortrait", $"  NameView child: {child.name}");
                    }

                    // Search for TextMeshPro components
                    var tmpComponents = go.GetComponentsInChildren<TextMeshProUGUI>(true);
                    foreach (var tmp in tmpComponents)
                    {
                        Log.Nav("PlayerPortrait", $"  TMP found: '{tmp.text}' on {tmp.gameObject.name}");
                        if (!string.IsNullOrEmpty(tmp.text) && !tmp.text.Contains("Rank"))
                        {
                            return tmp.text.Trim();
                        }
                    }
                }

                // Also check for NameText objects
                if (go.name.Contains(containerName) && go.name.Contains("NameText"))
                {
                    Log.Nav("PlayerPortrait", $"Found NameText: {go.name}");
                    var tmp = go.GetComponent<TextMeshProUGUI>();
                    if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                    {
                        Log.Nav("PlayerPortrait", $"  NameText value: '{tmp.text}'");
                        return tmp.text.Trim();
                    }
                }
            }
            return null;
        }

    }
}
