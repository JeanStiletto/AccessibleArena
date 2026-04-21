using MelonLoader;
using AccessibleArena.Core.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    public partial class DuelAnnouncer
    {
        // Reflection caches for reading CommanderGrpIds (3-type chain:
        // GameManager → MatchManager → PlayerInfo). Each type is only reachable
        // via a live instance from the previous step, so three caches seeded
        // independently at point of use.
        private sealed class GameManagerHandles
        {
            public PropertyInfo MatchManager;
        }
        private sealed class MatchManagerHandles
        {
            public PropertyInfo LocalPlayerInfo;
            public PropertyInfo OpponentInfo;
        }
        private sealed class PlayerInfoHandles
        {
            public PropertyInfo CommanderGrpIds;
        }

        private static readonly ReflectionCache<GameManagerHandles> _gmCache = new ReflectionCache<GameManagerHandles>(
            builder: t => new GameManagerHandles { MatchManager = t.GetProperty("MatchManager", PublicInstance) },
            validator: h => h.MatchManager != null,
            logTag: "DuelAnnouncer",
            logSubject: "GameManager");

        private static readonly ReflectionCache<MatchManagerHandles> _mmCache = new ReflectionCache<MatchManagerHandles>(
            builder: t => new MatchManagerHandles
            {
                LocalPlayerInfo = t.GetProperty("LocalPlayerInfo", PublicInstance),
                OpponentInfo = t.GetProperty("OpponentInfo", PublicInstance),
            },
            validator: h => h.LocalPlayerInfo != null && h.OpponentInfo != null,
            logTag: "DuelAnnouncer",
            logSubject: "MatchManager");

        private static readonly ReflectionCache<PlayerInfoHandles> _piCache = new ReflectionCache<PlayerInfoHandles>(
            builder: t => new PlayerInfoHandles { CommanderGrpIds = t.GetProperty("CommanderGrpIds", PublicInstance) },
            validator: h => h.CommanderGrpIds != null,
            logTag: "DuelAnnouncer",
            logSubject: "PlayerInfo");

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
                    Log.Msg("DuelAnnouncer", "GameManager not found, skipping commander population");
                    return;
                }

                if (!_gmCache.EnsureInitialized(gameManager.GetType())) return;

                var matchManager = _gmCache.Handles.MatchManager.GetValue(gameManager);
                if (matchManager == null) return;

                if (!_mmCache.EnsureInitialized(matchManager.GetType())) return;
                var mm = _mmCache.Handles;

                // Read local player commanders
                PopulateCommandersForPlayer(matchManager, mm.LocalPlayerInfo, isOpponent: false);

                // Read opponent commanders
                PopulateCommandersForPlayer(matchManager, mm.OpponentInfo, isOpponent: true);
            }
            catch (System.Exception ex)
            {
                Log.Warn("DuelAnnouncer", $"Failed to populate commanders from MatchManager: {ex.Message}");
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

            if (!_piCache.EnsureInitialized(playerInfo.GetType())) return;

            var grpIds = _piCache.Handles.CommanderGrpIds.GetValue(playerInfo) as IList;
            if (grpIds == null || grpIds.Count == 0) return;

            foreach (var id in grpIds)
            {
                uint grpId = System.Convert.ToUInt32(id);
                if (grpId == 0) continue;

                _commandZoneGrpIds[grpId] = isOpponent;
                string cardName = CardModelProvider.GetNameFromGrpId(grpId) ?? "Unknown";
                Log.Msg("DuelAnnouncer", $"Commander from MatchManager: GrpId={grpId} ({cardName}), isOpponent={isOpponent}");
            }
        }
    }
}
