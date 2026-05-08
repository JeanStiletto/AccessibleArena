using System;
using System.Reflection;
using UnityEngine;
using AccessibleArena.Core.Utils;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Reflection wrapper around the duel-scene emote/dialog system.
    /// Resolves <c>IEmoteManager</c> from <c>GameManager.Context</c> and exposes:
    ///   - opponent mute state read/toggle (via <c>OpponentDialogController</c>),
    ///   - localized emote text lookup (via <c>EmoteUtils.GetFullLocKey</c> + <c>Languages.ActiveLocProvider</c>).
    ///
    /// All paths are cached. The service is stateless across duels — callers use
    /// <see cref="ClearCache"/> on scene change to drop stale GameManager references.
    /// </summary>
    public static class EmoteService
    {
        // GameManager.Context (public IContext field) + IContext.Get<T> generic method.
        private sealed class GameManagerHandles
        {
            public FieldInfo ContextField;
            public MethodInfo ContextGenericGet;
        }

        // OpponentDialogController members we use (lives on EntityDialogController base).
        private sealed class DialogHandles
        {
            public MethodInfo IsMuted;
            public MethodInfo UpdateIsMuted;
            public FieldInfo AssetLookupSystem; // protected on EntityDialogController
        }

        // IEntityDialogControllerProvider.GetDialogControllerByPlayerType(GREPlayerNum) +
        // IEmoteControllerProvider.GetAllEmoteControllers()
        private sealed class EmoteManagerHandles
        {
            public MethodInfo GetDialogControllerByPlayerType;
            public MethodInfo GetAllEmoteControllers;
        }

        // Cached GameManager.UIMessageHandler property handle (separate cache so we don't
        // collide with the Context field used for IEmoteManager resolution).
        private sealed class GameManagerUIHandles
        {
            public PropertyInfo UIMessageHandler;
        }

        // EmoteUtils.GetFullLocKey/GetPreviewLocKey(string emoteId, AssetLookupSystem) -> string
        private sealed class EmoteUtilsHandles
        {
            public MethodInfo GetFullLocKey;
            public MethodInfo GetPreviewLocKey;
        }

        // EmoteOptionsController state — equipped list (private) plus AssetLookupSystem.
        // We read straight from the controller instead of scraping the Unity panel.
        private sealed class EmoteOptionsHandles
        {
            public FieldInfo EquippedEmoteOptions; // List<EmoteData>
            public FieldInfo AssetLookupSystem;
        }

        // EmoteData has a public readonly Id field; that's all we need for sending.
        private sealed class EmoteDataHandles
        {
            public FieldInfo Id;
        }

        // UIMessageHandler.TrySendEmote(string) -> bool
        private sealed class UIMessageHandles
        {
            public MethodInfo TrySendEmote;
        }

        private static readonly ReflectionCache<GameManagerHandles> _gmCache = new ReflectionCache<GameManagerHandles>(
            builder: t => new GameManagerHandles
            {
                ContextField = t.GetField("Context", PublicInstance),
            },
            validator: h => h.ContextField != null,
            logTag: "EmoteService",
            logSubject: "GameManager");

        private static readonly ReflectionCache<DialogHandles> _dialogCache = new ReflectionCache<DialogHandles>(
            builder: t =>
            {
                // EntityDialogController.IsMuted/UpdateIsMuted are public on the base class.
                var h = new DialogHandles
                {
                    IsMuted = t.GetMethod("IsMuted", PublicInstance, null, Type.EmptyTypes, null),
                    UpdateIsMuted = t.GetMethod("UpdateIsMuted", PublicInstance),
                };
                // _assetLookupSystem is a protected field on EntityDialogController (base of OpponentDialogController).
                // GetField with NonPublic|Instance does not walk the hierarchy, so look explicitly on the base type.
                Type cur = t;
                while (cur != null && h.AssetLookupSystem == null)
                {
                    h.AssetLookupSystem = cur.GetField("_assetLookupSystem", PrivateInstance);
                    cur = cur.BaseType;
                }
                return h;
            },
            validator: h => h.IsMuted != null && h.UpdateIsMuted != null && h.AssetLookupSystem != null,
            logTag: "EmoteService",
            logSubject: "DialogController");

        private static readonly ReflectionCache<EmoteManagerHandles> _mgrCache = new ReflectionCache<EmoteManagerHandles>(
            builder: t => new EmoteManagerHandles
            {
                GetDialogControllerByPlayerType = t.GetMethod("GetDialogControllerByPlayerType", PublicInstance),
                GetAllEmoteControllers = t.GetMethod("GetAllEmoteControllers", PublicInstance),
            },
            validator: h => h.GetDialogControllerByPlayerType != null && h.GetAllEmoteControllers != null,
            logTag: "EmoteService",
            logSubject: "EmoteManager");

        private static readonly ReflectionCache<GameManagerUIHandles> _gmUICache = new ReflectionCache<GameManagerUIHandles>(
            builder: t => new GameManagerUIHandles
            {
                UIMessageHandler = t.GetProperty("UIMessageHandler", PublicInstance),
            },
            validator: h => h.UIMessageHandler != null,
            logTag: "EmoteService",
            logSubject: "GameManager.UIMessageHandler");

        private static readonly ReflectionCache<EmoteUtilsHandles> _utilsCache = new ReflectionCache<EmoteUtilsHandles>(
            builder: t => new EmoteUtilsHandles
            {
                GetFullLocKey = t.GetMethod("GetFullLocKey", BindingFlags.Public | BindingFlags.Static),
                GetPreviewLocKey = t.GetMethod("GetPreviewLocKey", BindingFlags.Public | BindingFlags.Static),
            },
            validator: h => h.GetFullLocKey != null && h.GetPreviewLocKey != null,
            logTag: "EmoteService",
            logSubject: "EmoteUtils");

        private static readonly ReflectionCache<EmoteOptionsHandles> _optionsCache = new ReflectionCache<EmoteOptionsHandles>(
            builder: t => new EmoteOptionsHandles
            {
                EquippedEmoteOptions = t.GetField("_equippedEmoteOptions", PrivateInstance),
                AssetLookupSystem = t.GetField("_assetLookupSystem", PrivateInstance),
            },
            validator: h => h.EquippedEmoteOptions != null && h.AssetLookupSystem != null,
            logTag: "EmoteService",
            logSubject: "EmoteOptionsController");

        private static readonly ReflectionCache<EmoteDataHandles> _emoteDataCache = new ReflectionCache<EmoteDataHandles>(
            builder: t => new EmoteDataHandles
            {
                Id = t.GetField("Id", PublicInstance),
            },
            validator: h => h.Id != null,
            logTag: "EmoteService",
            logSubject: "EmoteData");

        private static readonly ReflectionCache<UIMessageHandles> _uiMsgCache = new ReflectionCache<UIMessageHandles>(
            builder: t => new UIMessageHandles
            {
                TrySendEmote = t.GetMethod("TrySendEmote", PublicInstance, null, new[] { typeof(string) }, null),
            },
            validator: h => h.TrySendEmote != null,
            logTag: "EmoteService",
            logSubject: "UIMessageHandler");

        private static MonoBehaviour _gameManager;
        private static int _gameManagerSearchFrame = -1;

        // Cached enum value GREPlayerNum.Opponent (resolved once).
        private static object _opponentEnum;
        private static bool _opponentEnumResolved;

        // Cached IEmoteManager instance — invalidated on scene change.
        private static object _emoteManager;
        private static int _emoteManagerSearchFrame = -1;

        public static void ClearCache()
        {
            _gameManager = null;
            _gameManagerSearchFrame = -1;
            _emoteManager = null;
            _emoteManagerSearchFrame = -1;
        }

        public static MonoBehaviour FindGameManager()
        {
            if (_gameManager != null) return _gameManager;

            int frame = Time.frameCount;
            if (frame == _gameManagerSearchFrame) return null;
            _gameManagerSearchFrame = frame;

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

        /// <summary>Returns the active <c>IEmoteManager</c>, or null if not yet available.</summary>
        public static object GetEmoteManager()
        {
            if (_emoteManager != null) return _emoteManager;

            int frame = Time.frameCount;
            if (frame == _emoteManagerSearchFrame) return null;
            _emoteManagerSearchFrame = frame;

            var gm = FindGameManager();
            if (gm == null) return null;

            if (!_gmCache.EnsureInitialized(gm.GetType())) return null;
            var gh = _gmCache.Handles;

            try
            {
                var context = gh.ContextField.GetValue(gm);
                if (context == null) return null;

                if (gh.ContextGenericGet == null)
                {
                    var emoteManagerInterface = FindType("Wotc.Mtga.DuelScene.Emotes.IEmoteManager");
                    if (emoteManagerInterface == null) return null;

                    var contextType = context.GetType();
                    var openGet = contextType.GetMethod("Get", BindingFlags.Public | BindingFlags.Instance);
                    if (openGet == null) return null;
                    gh.ContextGenericGet = openGet.MakeGenericMethod(emoteManagerInterface);
                }

                var emoteManager = gh.ContextGenericGet.Invoke(context, null);
                // NullEmoteManager is a placeholder before duel start; treat as not-yet-ready so we keep retrying.
                if (emoteManager == null || emoteManager.GetType().Name == "NullEmoteManager") return null;

                _emoteManager = emoteManager;
                return _emoteManager;
            }
            catch
            {
                return null;
            }
        }

        public static object GetOpponentDialogController()
        {
            var emoteManager = GetEmoteManager();
            if (emoteManager == null) return null;

            if (!_mgrCache.EnsureInitialized(emoteManager.GetType())) return null;
            var mh = _mgrCache.Handles;

            if (!_opponentEnumResolved)
            {
                _opponentEnumResolved = true;
                var grePlayerNumType = FindType("GREPlayerNum");
                if (grePlayerNumType != null && grePlayerNumType.IsEnum)
                {
                    try { _opponentEnum = Enum.Parse(grePlayerNumType, "Opponent"); }
                    catch { _opponentEnum = null; }
                }
            }
            if (_opponentEnum == null) return null;

            try
            {
                return mh.GetDialogControllerByPlayerType.Invoke(emoteManager, new[] { _opponentEnum });
            }
            catch
            {
                return null;
            }
        }

        public static bool IsOpponentMuted()
        {
            var ctrl = GetOpponentDialogController();
            if (ctrl == null) return false;
            if (!_dialogCache.EnsureInitialized(ctrl.GetType())) return false;
            try
            {
                return (bool)_dialogCache.Handles.IsMuted.Invoke(ctrl, null);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Toggles opponent mute state. Returns the new state, or null if unavailable.</summary>
        public static bool? ToggleOpponentMute()
        {
            var ctrl = GetOpponentDialogController();
            if (ctrl == null) return null;
            if (!_dialogCache.EnsureInitialized(ctrl.GetType())) return null;
            try
            {
                bool current = (bool)_dialogCache.Handles.IsMuted.Invoke(ctrl, null);
                _dialogCache.Handles.UpdateIsMuted.Invoke(ctrl, new object[] { !current });
                return !current;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Resolves an emote ID to its localized full text by routing through
        /// <c>EmoteUtils.GetFullLocKey(id, AssetLookupSystem)</c> followed by the loc provider.
        /// Returns null if any step fails — callers should fall back to a generic announcement.
        /// </summary>
        public static string ResolveEmoteText(string emoteId)
        {
            if (string.IsNullOrEmpty(emoteId)) return null;

            var ctrl = GetOpponentDialogController();
            if (ctrl == null) return null;
            if (!_dialogCache.EnsureInitialized(ctrl.GetType())) return null;

            object assetLookupSystem;
            try { assetLookupSystem = _dialogCache.Handles.AssetLookupSystem.GetValue(ctrl); }
            catch { return null; }
            if (assetLookupSystem == null) return null;

            var emoteUtilsType = FindType("EmoteUtils");
            if (emoteUtilsType == null) return null;
            if (!_utilsCache.EnsureInitialized(emoteUtilsType)) return null;

            string locKey;
            try { locKey = _utilsCache.Handles.GetFullLocKey.Invoke(null, new object[] { emoteId, assetLookupSystem }) as string; }
            catch { return null; }
            if (string.IsNullOrEmpty(locKey)) return null;

            // UITextExtractor caches and resolves via Languages.ActiveLocProvider.
            return UITextExtractor.ResolveLocKey(locKey);
        }

        /// <summary>One equipped emote, ready to announce or send.</summary>
        public readonly struct EquippedEmote
        {
            public readonly string Id;
            public readonly string PreviewText;
            public EquippedEmote(string id, string previewText) { Id = id; PreviewText = previewText; }
        }

        /// <summary>
        /// Returns the local player's equipped emotes in their on-wheel order, or null if the
        /// emote system isn't ready yet (pre-duel, controller still being constructed, etc.).
        /// Reads <c>EmoteOptionsController._equippedEmoteOptions</c> directly — no UI scraping.
        /// </summary>
        public static System.Collections.Generic.List<EquippedEmote> GetEquippedEmotes()
        {
            var ctrl = GetLocalEmoteOptionsController();
            if (ctrl == null) return null;
            if (!_optionsCache.EnsureInitialized(ctrl.GetType())) return null;
            var oh = _optionsCache.Handles;

            var list = oh.EquippedEmoteOptions.GetValue(ctrl) as System.Collections.IList;
            if (list == null) return null;

            var assetLookupSystem = oh.AssetLookupSystem.GetValue(ctrl);
            if (assetLookupSystem == null) return null;

            var emoteUtilsType = FindType("EmoteUtils");
            if (emoteUtilsType == null) return null;
            if (!_utilsCache.EnsureInitialized(emoteUtilsType)) return null;
            var uh = _utilsCache.Handles;

            var result = new System.Collections.Generic.List<EquippedEmote>(list.Count);
            foreach (var emoteData in list)
            {
                if (emoteData == null) continue;
                if (!_emoteDataCache.EnsureInitialized(emoteData.GetType())) continue;

                var id = _emoteDataCache.Handles.Id.GetValue(emoteData) as string;
                if (string.IsNullOrEmpty(id)) continue;

                string text = id;
                try
                {
                    var locKey = uh.GetPreviewLocKey.Invoke(null, new object[] { id, assetLookupSystem }) as string;
                    if (!string.IsNullOrEmpty(locKey))
                    {
                        var resolved = UITextExtractor.ResolveLocKey(locKey);
                        if (!string.IsNullOrEmpty(resolved)) text = resolved;
                    }
                }
                catch { /* keep id as fallback text */ }

                result.Add(new EquippedEmote(id, text));
            }
            return result;
        }

        /// <summary>
        /// Sends an emote by writing directly to the GRE through <c>UIMessageHandler.TrySendEmote</c>.
        /// No dependency on the emote panel being open or visible. Returns false if the message
        /// handler is not yet reachable (pre-duel) or the call failed.
        /// </summary>
        public static bool SendEmote(string emoteId)
        {
            if (string.IsNullOrEmpty(emoteId)) return false;

            var gm = FindGameManager();
            if (gm == null) return false;
            if (!_gmUICache.EnsureInitialized(gm.GetType())) return false;

            object uiHandler;
            try { uiHandler = _gmUICache.Handles.UIMessageHandler.GetValue(gm); }
            catch { return false; }
            if (uiHandler == null) return false;

            if (!_uiMsgCache.EnsureInitialized(uiHandler.GetType())) return false;

            try
            {
                var ok = _uiMsgCache.Handles.TrySendEmote.Invoke(uiHandler, new object[] { emoteId });
                return ok is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Closes the local emote wheel if it is currently open. Used after sending an emote
        /// (UIMessageHandler.TrySendEmote bypasses EmoteOptionsController.EmoteClicked, which
        /// is normally what dismisses the panel) and after Backspace cancels.
        /// </summary>
        public static void CloseLocalEmoteWheel()
        {
            var ctrl = GetLocalEmoteOptionsController();
            if (ctrl == null) return;

            try
            {
                // IEmoteController.Close() is public on the interface and on the concrete type.
                var close = ctrl.GetType().GetMethod("Close", PublicInstance, null, Type.EmptyTypes, null);
                close?.Invoke(ctrl, null);
            }
            catch { /* best-effort dismiss */ }
        }

        // Picks the local player's controller out of GetAllEmoteControllers(). Local-side controllers
        // are concrete EmoteOptionsController instances; opponents get CommunicationOptionsController.
        // Type-name match is unambiguous since at most one local player exists per match.
        private static object GetLocalEmoteOptionsController()
        {
            var emoteManager = GetEmoteManager();
            if (emoteManager == null) return null;
            if (!_mgrCache.EnsureInitialized(emoteManager.GetType())) return null;

            System.Collections.IEnumerable controllers;
            try { controllers = _mgrCache.Handles.GetAllEmoteControllers.Invoke(emoteManager, null) as System.Collections.IEnumerable; }
            catch { return null; }
            if (controllers == null) return null;

            foreach (var ctrl in controllers)
            {
                if (ctrl != null && ctrl.GetType().Name == "EmoteOptionsController")
                    return ctrl;
            }
            return null;
        }
    }
}
