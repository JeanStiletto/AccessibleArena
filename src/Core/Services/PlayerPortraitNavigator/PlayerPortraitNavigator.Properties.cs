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

        // Rank reflection cache (GameManager -> MatchManager -> PlayerInfo)
        private static PropertyInfo _matchManagerProp;
        private static PropertyInfo _localPlayerInfoProp;
        private static PropertyInfo _opponentInfoProp;
        private static FieldInfo _rankingClassField;
        private static FieldInfo _rankingTierField;
        private static FieldInfo _mythicPercentileField;
        private static FieldInfo _mythicPlacementField;
        private static bool _rankReflectionInitialized;

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

            if (!_entityReflectionInitialized)
                InitializeEntityReflection(player);

            try
            {
                if (_designationsField != null)
                {
                    var designations = _designationsField.GetValue(player) as IList;
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

                if (_abilitiesField != null)
                {
                    var abilities = _abilitiesField.GetValue(player) as IList;
                    if (abilities != null && abilities.Count > 0)
                        return true;
                }

                if (_dungeonStateField != null)
                {
                    var dungeonState = _dungeonStateField.GetValue(player);
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

                if (!_rankReflectionInitialized)
                    InitializeRankReflection(gameManager);
                if (!_rankReflectionInitialized) return null;

                var matchManager = _matchManagerProp.GetValue(gameManager);
                if (matchManager == null) return null;

                var infoProp = isOpponent ? _opponentInfoProp : _localPlayerInfoProp;
                if (infoProp == null) return null;

                var playerInfo = infoProp.GetValue(matchManager);
                if (playerInfo == null) return null;

                // Read RankingClass enum value (None=-1, Spark=0, Bronze=1, Silver=2, Gold=3, Platinum=4, Diamond=5, Master=6, Mythic=7)
                int rankingClass = System.Convert.ToInt32(_rankingClassField.GetValue(playerInfo));

                if (rankingClass <= 0) return "Unranked";

                // Mythic rank
                if (rankingClass == 7)
                {
                    int placement = _mythicPlacementField != null ? System.Convert.ToInt32(_mythicPlacementField.GetValue(playerInfo)) : 0;
                    if (placement > 0)
                        return $"Mythic #{placement}";

                    float percentile = _mythicPercentileField != null ? System.Convert.ToSingle(_mythicPercentileField.GetValue(playerInfo)) : 0f;
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

                int tier = _rankingTierField != null ? System.Convert.ToInt32(_rankingTierField.GetValue(playerInfo)) : 0;
                if (tier > 0)
                    return $"{rankName} Tier {tier}";

                return rankName;
            }
            catch (System.Exception ex)
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

        /// <summary>
        /// Initializes reflection cache for rank data from MatchManager player info.
        /// </summary>
        private static void InitializeRankReflection(object gameManager)
        {
            try
            {
                var gmType = gameManager.GetType();
                _matchManagerProp = gmType.GetProperty("MatchManager", PublicInstance);
                if (_matchManagerProp == null)
                {
                    MelonLogger.Warning("[PlayerPortrait] Could not find MatchManager property on GameManager");
                    return;
                }

                var matchManager = _matchManagerProp.GetValue(gameManager);
                if (matchManager == null)
                {
                    MelonLogger.Warning("[PlayerPortrait] MatchManager is null");
                    return;
                }

                var mmType = matchManager.GetType();
                _localPlayerInfoProp = mmType.GetProperty("LocalPlayerInfo", PublicInstance);
                _opponentInfoProp = mmType.GetProperty("OpponentInfo", PublicInstance);

                if (_localPlayerInfoProp == null)
                {
                    MelonLogger.Warning("[PlayerPortrait] Could not find LocalPlayerInfo property on MatchManager");
                    return;
                }

                // Get player info type from local player info
                var playerInfo = _localPlayerInfoProp.GetValue(matchManager);
                if (playerInfo == null)
                {
                    MelonLogger.Warning("[PlayerPortrait] LocalPlayerInfo is null");
                    return;
                }

                var piType = playerInfo.GetType();
                var allBindings = AllInstanceFlags;
                _rankingClassField = piType.GetField("RankingClass", allBindings);
                _rankingTierField = piType.GetField("RankingTier", allBindings);
                _mythicPercentileField = piType.GetField("MythicPercentile", allBindings);
                _mythicPlacementField = piType.GetField("MythicPlacement", allBindings);

                if (_rankingClassField == null)
                {
                    // Try as properties instead
                    var rcProp = piType.GetProperty("RankingClass", allBindings);
                    if (rcProp != null)
                    {
                        MelonLogger.Msg("[PlayerPortrait] RankingClass is a property, not a field - logging all members for debugging");
                    }
                    MelonLogger.Warning("[PlayerPortrait] Could not find RankingClass field on player info type " + piType.Name);
                    // Log available fields for debugging
                    foreach (var f in piType.GetFields(allBindings))
                    {
                        MelonLogger.Msg($"[PlayerPortrait]   Field: {f.Name} ({f.FieldType.Name})");
                    }
                    foreach (var p in piType.GetProperties(allBindings))
                    {
                        MelonLogger.Msg($"[PlayerPortrait]   Property: {p.Name} ({p.PropertyType.Name})");
                    }
                    return;
                }

                _rankReflectionInitialized = true;
                MelonLogger.Msg($"[PlayerPortrait] Rank reflection initialized: RankingClass={_rankingClassField.FieldType.Name}, RankingTier={_rankingTierField?.FieldType.Name ?? "null"}");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[PlayerPortrait] Failed to initialize rank reflection: {ex.Message}");
            }
        }
    }
}
