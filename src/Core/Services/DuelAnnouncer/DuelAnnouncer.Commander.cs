using AccessibleArena.Core.Models;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    public partial class DuelAnnouncer
    {
        /// <summary>
        /// A single command-zone card (commander or emblem), resolved live from the game state.
        /// <see cref="Cdc"/> is the real card GameObject when a card view exists — navigating it
        /// gives the full card experience (actual cost, extended info, counters, attachments).
        /// When no instantiated view exists (e.g. an uncast commander), <see cref="Cdc"/> is null
        /// and only <see cref="GrpId"/> is available for a card-database fallback.
        /// </summary>
        public struct CommandZoneCardRef
        {
            public uint InstanceId;
            public GameObject Cdc;
            public uint GrpId;
        }

        // The opponent's commander is read straight from the live game state, mirroring Arena's
        // own ExamineCommanderCard: GameManager.CurrentGameState.Opponent.CommanderIds gives the
        // instance IDs, ViewManager.TryGetCardView resolves each to a live card view, and the
        // card database (via GrpId) is the fallback when no view exists. This replaces the old
        // MatchManager.PlayerInfo.CommanderGrpIds reflection chain (removed by the game in 2026.60)
        // and the unreliable zone-event ownership tracking (ControllerId is 0 for opponents).
        private sealed class GameManagerHandles
        {
            public PropertyInfo CurrentGameState;
            public PropertyInfo ViewManager;
        }
        private sealed class GameStateHandles
        {
            public PropertyInfo Opponent;
            public PropertyInfo LocalPlayer;
            public PropertyInfo Command;
            public MethodInfo GetCardById;
        }
        private sealed class PlayerHandles
        {
            public FieldInfo CommanderIds;
            public FieldInfo InstanceId;
            public FieldInfo Designations;
            public PropertyInfo IsLocalPlayer;
        }
        private sealed class ViewManagerHandles
        {
            public MethodInfo TryGetCardView;
        }
        private sealed class CardInstanceHandles
        {
            public PropertyInfo GrpId;
            public FieldInfo ObjectType;
            public FieldInfo Controller;
            public FieldInfo InstanceId;
        }
        private sealed class ZoneHandles
        {
            public FieldInfo VisibleCards;
        }

        private static readonly ReflectionCache<GameManagerHandles> _gmCache = new ReflectionCache<GameManagerHandles>(
            builder: t => new GameManagerHandles
            {
                CurrentGameState = t.GetProperty("CurrentGameState", PublicInstance),
                ViewManager = t.GetProperty("ViewManager", PublicInstance),
            },
            validator: h => h.CurrentGameState != null && h.ViewManager != null,
            logTag: "DuelAnnouncer",
            logSubject: "GameManager(Commander)");

        private static readonly ReflectionCache<GameStateHandles> _gsCache = new ReflectionCache<GameStateHandles>(
            builder: t => new GameStateHandles
            {
                Opponent = t.GetProperty("Opponent", PublicInstance),
                LocalPlayer = t.GetProperty("LocalPlayer", PublicInstance),
                Command = t.GetProperty("Command", PublicInstance),
                GetCardById = t.GetMethod("GetCardById", PublicInstance, null, new[] { typeof(uint) }, null),
            },
            validator: h => h.Opponent != null,
            logTag: "DuelAnnouncer",
            logSubject: "MtgGameState");

        private static readonly ReflectionCache<PlayerHandles> _playerCache = new ReflectionCache<PlayerHandles>(
            builder: t => new PlayerHandles
            {
                CommanderIds = t.GetField("CommanderIds", PublicInstance),
                InstanceId = t.GetField("InstanceId", PublicInstance),
                Designations = t.GetField("Designations", PublicInstance),
                IsLocalPlayer = t.GetProperty("IsLocalPlayer", PublicInstance),
            },
            validator: h => h.CommanderIds != null,
            logTag: "DuelAnnouncer",
            logSubject: "MtgPlayer");

        private static readonly ReflectionCache<ViewManagerHandles> _vmCache = new ReflectionCache<ViewManagerHandles>(
            // EntityViewManager implements many interfaces; resolve by name + (uint, out) shape
            // to avoid AmbiguousMatchException from GetMethod(name).
            builder: t => new ViewManagerHandles
            {
                TryGetCardView = t.GetMethods(PublicInstance).FirstOrDefault(m =>
                {
                    if (m.Name != "TryGetCardView") return false;
                    var ps = m.GetParameters();
                    return ps.Length == 2 && ps[0].ParameterType == typeof(uint) && ps[1].IsOut;
                }),
            },
            validator: h => h.TryGetCardView != null,
            logTag: "DuelAnnouncer",
            logSubject: "ViewManager");

        private static readonly ReflectionCache<CardInstanceHandles> _ciCache = new ReflectionCache<CardInstanceHandles>(
            builder: t => new CardInstanceHandles
            {
                GrpId = t.GetProperty("GrpId", PublicInstance),
                ObjectType = t.GetField("ObjectType", PublicInstance),
                Controller = t.GetField("Controller", PublicInstance),
                InstanceId = t.GetField("InstanceId", PublicInstance),
            },
            validator: h => h.GrpId != null,
            logTag: "DuelAnnouncer",
            logSubject: "MtgCardInstance(Commander)");

        private static readonly ReflectionCache<ZoneHandles> _zoneCache = new ReflectionCache<ZoneHandles>(
            builder: t => new ZoneHandles { VisibleCards = t.GetField("VisibleCards", PublicInstance) },
            validator: h => h.VisibleCards != null,
            logTag: "DuelAnnouncer",
            logSubject: "MtgZone");

        // GameManager persists for the duration of a match; cache it and re-find if destroyed.
        private MonoBehaviour _gameManager;

        private MonoBehaviour GetGameManager()
        {
            if (_gameManager != null) return _gameManager;
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "GameManager")
                {
                    _gameManager = mb;
                    return _gameManager;
                }
            }
            return null;
        }

        /// <summary>
        /// Resolves the opponent's commander(s) live from the game state. Supports partner
        /// commanders (multiple entries). Each entry carries the live card GameObject when one
        /// exists, plus the GrpId for a card-database fallback.
        /// </summary>
        public List<CommandZoneCardRef> GetOpponentCommanders()
        {
            var result = new List<CommandZoneCardRef>();
            try
            {
                var gm = GetGameManager();
                if (gm == null) return result;
                if (!_gmCache.EnsureInitialized(gm.GetType())) return result;

                var gameState = _gmCache.Handles.CurrentGameState.GetValue(gm);
                var viewManager = _gmCache.Handles.ViewManager.GetValue(gm);
                if (gameState == null) return result;

                if (!_gsCache.EnsureInitialized(gameState.GetType())) return result;
                var opponent = _gsCache.Handles.Opponent.GetValue(gameState);
                if (opponent == null) return result;

                if (!_playerCache.EnsureInitialized(opponent.GetType())) return result;
                var player = _playerCache.Handles;

                bool vmReady = viewManager != null && _vmCache.EnsureInitialized(viewManager.GetType());

                var commanderIds = player.CommanderIds.GetValue(opponent) as IList;
                if (commanderIds != null && commanderIds.Count > 0)
                {
                    foreach (var idObj in commanderIds)
                    {
                        uint instanceId = System.Convert.ToUInt32(idObj);
                        if (instanceId == 0) continue;

                        GameObject cdc = vmReady ? TryResolveCardView(viewManager, instanceId) : null;
                        uint grpId = GetGrpIdForInstance(gameState, instanceId);

                        result.Add(new CommandZoneCardRef { InstanceId = instanceId, Cdc = cdc, GrpId = grpId });
                        Log.Msg("DuelAnnouncer",
                            $"Opponent commander: instanceId={instanceId}, grpId={grpId}, hasView={(cdc != null)}");
                    }
                    return result;
                }

                // Fallback: no CommanderIds populated yet — read the Commander designation(s).
                AddCommandersFromDesignations(opponent, player.Designations, result);
            }
            catch (System.Exception ex)
            {
                Log.Warn("DuelAnnouncer", $"GetOpponentCommanders failed: {ex.Message}");
            }
            return result;
        }

        /// <summary>
        /// Resolves the emblems currently in the command zone for one side. Emblems are regular
        /// card instances (ObjectType == Emblem) living in MtgGameState.Command — there is no
        /// dedicated examine path for them, so we read them straight from the zone and resolve each
        /// to its live card view. Includes Ring/other-sourced emblems regardless of origin.
        /// </summary>
        /// <param name="opponent">True for the opponent's emblems, false for the local player's.</param>
        public List<CommandZoneCardRef> GetCommandZoneEmblems(bool opponent)
        {
            var result = new List<CommandZoneCardRef>();
            try
            {
                var gm = GetGameManager();
                if (gm == null) return result;
                if (!_gmCache.EnsureInitialized(gm.GetType())) return result;

                var gameState = _gmCache.Handles.CurrentGameState.GetValue(gm);
                var viewManager = _gmCache.Handles.ViewManager.GetValue(gm);
                if (gameState == null) return result;

                if (!_gsCache.EnsureInitialized(gameState.GetType())) return result;
                if (_gsCache.Handles.Command == null) return result;
                var commandZone = _gsCache.Handles.Command.GetValue(gameState);
                if (commandZone == null) return result;

                if (!_zoneCache.EnsureInitialized(commandZone.GetType())) return result;
                var visible = _zoneCache.Handles.VisibleCards.GetValue(commandZone) as IList;
                if (visible == null) return result;

                bool vmReady = viewManager != null && _vmCache.EnsureInitialized(viewManager.GetType());

                foreach (var inst in visible)
                {
                    if (inst == null) continue;
                    if (!_ciCache.EnsureInitialized(inst.GetType())) continue;
                    var ci = _ciCache.Handles;
                    if (ci.ObjectType == null || ci.Controller == null || ci.InstanceId == null) continue;

                    if (ci.ObjectType.GetValue(inst)?.ToString() != "Emblem") continue;

                    var controller = ci.Controller.GetValue(inst);
                    if (!IsControllerOnSide(controller, opponent)) continue;

                    uint instanceId = System.Convert.ToUInt32(ci.InstanceId.GetValue(inst));
                    uint grpId = System.Convert.ToUInt32(ci.GrpId.GetValue(inst));
                    GameObject cdc = vmReady ? TryResolveCardView(viewManager, instanceId) : null;

                    result.Add(new CommandZoneCardRef { InstanceId = instanceId, Cdc = cdc, GrpId = grpId });
                    Log.Msg("DuelAnnouncer",
                        $"Command-zone emblem: instanceId={instanceId}, grpId={grpId}, opponent={opponent}, hasView={(cdc != null)}");
                }
            }
            catch (System.Exception ex)
            {
                Log.Warn("DuelAnnouncer", $"GetCommandZoneEmblems failed: {ex.Message}");
            }
            return result;
        }

        /// <summary>True if the card's controller is on the requested side (opponent vs local).</summary>
        private bool IsControllerOnSide(object controller, bool opponent)
        {
            if (controller == null) return false;
            if (!_playerCache.EnsureInitialized(controller.GetType())) return false;
            if (_playerCache.Handles.IsLocalPlayer == null) return false;
            bool isLocal = (bool)_playerCache.Handles.IsLocalPlayer.GetValue(controller);
            return opponent ? !isLocal : isLocal;
        }

        /// <summary>Invokes ViewManager.TryGetCardView(instanceId, out cdc) and returns the CDC GameObject.</summary>
        private GameObject TryResolveCardView(object viewManager, uint instanceId)
        {
            var args = new object[] { instanceId, null };
            bool ok = (bool)_vmCache.Handles.TryGetCardView.Invoke(viewManager, args);
            if (!ok) return null;
            return (args[1] as Component)?.gameObject;
        }

        /// <summary>Reads MtgCardInstance.GrpId for a commander instance via MtgGameState.GetCardById.</summary>
        private uint GetGrpIdForInstance(object gameState, uint instanceId)
        {
            try
            {
                if (_gsCache.Handles.GetCardById == null) return 0;
                var instance = _gsCache.Handles.GetCardById.Invoke(gameState, new object[] { instanceId });
                if (instance == null) return 0;
                if (!_ciCache.EnsureInitialized(instance.GetType())) return 0;
                return System.Convert.ToUInt32(_ciCache.Handles.GrpId.GetValue(instance));
            }
            catch { return 0; }
        }

        /// <summary>
        /// Reads commander GrpIds from the opponent's Designation list (Type == Commander).
        /// Used only when CommanderIds is empty; produces view-less, GrpId-only entries.
        /// </summary>
        private void AddCommandersFromDesignations(object opponent, FieldInfo designationsField, List<CommandZoneCardRef> result)
        {
            if (designationsField == null) return;
            var designations = designationsField.GetValue(opponent) as IList;
            if (designations == null) return;

            foreach (var d in designations)
            {
                if (d == null) continue;
                var dType = d.GetType();
                var typeField = dType.GetField("Type", PublicInstance);
                if (typeField == null || typeField.GetValue(d)?.ToString() != "Commander") continue;

                var grpField = dType.GetField("GrpId", PublicInstance);
                uint grpId = grpField != null ? System.Convert.ToUInt32(grpField.GetValue(d)) : 0;
                if (grpId == 0) continue;

                result.Add(new CommandZoneCardRef { InstanceId = 0, Cdc = null, GrpId = grpId });
                Log.Msg("DuelAnnouncer", $"Opponent commander from designation: grpId={grpId}");
            }
        }

        /// <summary>
        /// Gets all opponent commander GrpIds (supports partner commanders).
        /// </summary>
        public List<uint> GetAllOpponentCommanderGrpIds()
        {
            var result = new List<uint>();
            foreach (var c in GetOpponentCommanders())
            {
                if (c.GrpId != 0)
                    result.Add(c.GrpId);
            }
            return result;
        }

        /// <summary>
        /// Gets the opponent's commander GrpId (first, for single-commander games).
        /// </summary>
        public uint GetOpponentCommanderGrpId()
        {
            foreach (var c in GetOpponentCommanders())
            {
                if (c.GrpId != 0)
                    return c.GrpId;
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
    }
}
