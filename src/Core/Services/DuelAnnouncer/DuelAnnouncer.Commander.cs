using MelonLoader;
using AccessibleArena.Core.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    public partial class DuelAnnouncer
    {
        // Reflection cache for reading CommanderGrpIds from MatchManager
        private static PropertyInfo _mmProp;       // GameManager.MatchManager
        private static PropertyInfo _localPIProp;  // MatchManager.LocalPlayerInfo
        private static PropertyInfo _opponentPIProp; // MatchManager.OpponentInfo
        private static PropertyInfo _commanderGrpIdsProp; // PlayerInfo.CommanderGrpIds
        private static bool _commanderReflectionInitialized;

        /// <summary>
        /// Gets the opponent's commander GrpId for Brawl/Commander games.
        /// Uses ownership tracked when cards first entered the Command zone.
        /// </summary>
        public uint GetOpponentCommanderGrpId()
        {
            foreach (var kvp in _commandZoneGrpIds)
            {
                if (kvp.Value) // isOpponent == true
                    return kvp.Key;
            }
            return 0;
        }

        /// <summary>
        /// Gets the full CardInfo for the opponent's commander from the card database.
        /// Returns null if not available.
        /// </summary>
        public CardInfo? GetOpponentCommanderInfo()
        {
            uint grpId = GetOpponentCommanderGrpId();
            if (grpId == 0) return null;
            return CardModelProvider.GetCardInfoFromGrpId(grpId);
        }

        /// <summary>
        /// Gets the opponent's commander card name. Convenience wrapper around GetOpponentCommanderGrpId.
        /// </summary>
        public string GetOpponentCommanderName()
        {
            uint grpId = GetOpponentCommanderGrpId();
            if (grpId == 0) return null;
            return CardModelProvider.GetNameFromGrpId(grpId);
        }

        /// <summary>
        /// Gets all opponent commander GrpIds (supports partner commanders).
        /// </summary>
        public List<uint> GetAllOpponentCommanderGrpIds()
        {
            var result = new List<uint>();
            foreach (var kvp in _commandZoneGrpIds)
            {
                if (kvp.Value) // isOpponent == true
                    result.Add(kvp.Key);
            }
            return result;
        }

        /// <summary>
        /// Populates commander GrpIds from MatchManager player info.
        /// Called during Activate() to seed the command zone data before any zone events arrive.
        /// This is essential for the opponent's commander which never generates zone transfer events.
        /// </summary>
        private void PopulateCommandersFromMatchManager()
        {
            try
            {
                // Find GameManager MonoBehaviour
                MonoBehaviour gameManager = null;
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == "GameManager")
                    {
                        gameManager = mb;
                        break;
                    }
                }
                if (gameManager == null)
                {
                    MelonLogger.Msg("[DuelAnnouncer] GameManager not found, skipping commander population");
                    return;
                }

                // Initialize reflection cache once
                if (!_commanderReflectionInitialized)
                    InitializeCommanderReflection(gameManager);
                if (!_commanderReflectionInitialized) return;

                var matchManager = _mmProp.GetValue(gameManager);
                if (matchManager == null) return;

                // Read local player commanders
                PopulateCommandersForPlayer(matchManager, _localPIProp, isOpponent: false);

                // Read opponent commanders
                PopulateCommandersForPlayer(matchManager, _opponentPIProp, isOpponent: true);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Failed to populate commanders from MatchManager: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads CommanderGrpIds from a player info object and populates _commandZoneGrpIds.
        /// </summary>
        private void PopulateCommandersForPlayer(object matchManager, PropertyInfo playerInfoProp, bool isOpponent)
        {
            if (playerInfoProp == null) return;

            var playerInfo = playerInfoProp.GetValue(matchManager);
            if (playerInfo == null) return;

            var grpIds = _commanderGrpIdsProp?.GetValue(playerInfo) as IList;
            if (grpIds == null || grpIds.Count == 0) return;

            string side = isOpponent ? "opponent" : "local";
            foreach (var id in grpIds)
            {
                uint grpId = System.Convert.ToUInt32(id);
                if (grpId == 0) continue;

                _commandZoneGrpIds[grpId] = isOpponent;
                string cardName = CardModelProvider.GetNameFromGrpId(grpId) ?? "Unknown";
                MelonLogger.Msg($"[DuelAnnouncer] Commander from MatchManager: GrpId={grpId} ({cardName}), isOpponent={isOpponent}");
            }
        }

        /// <summary>
        /// Initializes reflection cache for CommanderGrpIds access.
        /// </summary>
        private static void InitializeCommanderReflection(object gameManager)
        {
            try
            {
                var gmType = gameManager.GetType();
                _mmProp = gmType.GetProperty("MatchManager", PublicInstance);
                if (_mmProp == null)
                {
                    MelonLogger.Warning("[DuelAnnouncer] Could not find MatchManager property on GameManager");
                    return;
                }

                var mm = _mmProp.GetValue(gameManager);
                if (mm == null)
                {
                    MelonLogger.Warning("[DuelAnnouncer] MatchManager is null during commander reflection init");
                    return;
                }

                var mmType = mm.GetType();
                _localPIProp = mmType.GetProperty("LocalPlayerInfo", PublicInstance);
                _opponentPIProp = mmType.GetProperty("OpponentInfo", PublicInstance);

                if (_localPIProp == null || _opponentPIProp == null)
                {
                    MelonLogger.Warning("[DuelAnnouncer] Could not find player info properties on MatchManager");
                    return;
                }

                var playerInfo = _localPIProp.GetValue(mm);
                if (playerInfo == null)
                {
                    MelonLogger.Warning("[DuelAnnouncer] LocalPlayerInfo is null during commander reflection init");
                    return;
                }

                _commanderGrpIdsProp = playerInfo.GetType().GetProperty("CommanderGrpIds", PublicInstance);
                if (_commanderGrpIdsProp == null)
                {
                    MelonLogger.Warning("[DuelAnnouncer] Could not find CommanderGrpIds property on PlayerInfo");
                    return;
                }

                _commanderReflectionInitialized = true;
                MelonLogger.Msg("[DuelAnnouncer] Commander reflection initialized successfully");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[DuelAnnouncer] Commander reflection init failed: {ex.Message}");
            }
        }
    }
}
