using System;
using System.Reflection;
using UnityEngine;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Utils;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Subscribes to <c>UIMessageHandler.EmoteRecievedCallback</c> for the duration of a duel
    /// and announces every incoming emote (filtered by opponent mute state).
    ///
    /// The GRE delivers emotes as <c>UIMessage.OnChat</c> with the emote ID as text. The handler
    /// fires once per incoming network event, so we don't need to poll like the chat readers do.
    /// We resolve the emote ID to its localized full text via <see cref="EmoteService.ResolveEmoteText"/>
    /// and announce at Normal priority so urgent game state still wins.
    /// </summary>
    public class EmoteAnnouncer
    {
        private readonly IAnnouncementService _announcer;

        // Subscription state — torn down on scene change and re-established when the next duel arrives.
        private MonoBehaviour _gameManager;
        private object _uiMessageHandler;
        private Delegate _subscribedHandler;
        private float _retryTimer;

        private sealed class GameManagerHandles
        {
            public PropertyInfo UIMessageHandler;
        }

        private sealed class UIMessageHandles
        {
            public EventInfo EmoteRecievedCallback;
        }

        private static readonly ReflectionCache<GameManagerHandles> _gmCache = new ReflectionCache<GameManagerHandles>(
            builder: t => new GameManagerHandles
            {
                UIMessageHandler = t.GetProperty("UIMessageHandler", PublicInstance),
            },
            validator: h => h.UIMessageHandler != null,
            logTag: "EmoteAnnouncer",
            logSubject: "GameManager");

        private static readonly ReflectionCache<UIMessageHandles> _uiCache = new ReflectionCache<UIMessageHandles>(
            builder: t => new UIMessageHandles
            {
                // EmoteRecievedCallback is declared as `public event Action<string>`; reflect it as an EventInfo
                // so we can build a delegate matching the game's actual handler type without hard-coding it.
                EmoteRecievedCallback = t.GetEvent("EmoteRecievedCallback", PublicInstance),
            },
            validator: h => h.EmoteRecievedCallback != null,
            logTag: "EmoteAnnouncer",
            logSubject: "UIMessageHandler");

        public EmoteAnnouncer(IAnnouncementService announcer)
        {
            _announcer = announcer;
        }

        public void Update()
        {
            if (_uiMessageHandler != null) return;

            // Re-resolve at most once per second to avoid hammering FindObjectsOfType while idle.
            _retryTimer -= Time.deltaTime;
            if (_retryTimer > 0f) return;
            _retryTimer = 1f;

            TrySubscribe();
        }

        public void OnSceneChanged()
        {
            Unsubscribe();
            _uiMessageHandler = null;
            _gameManager = null;
            _retryTimer = 0f;
        }

        private void TrySubscribe()
        {
            try
            {
                _gameManager = EmoteService.FindGameManager();
                if (_gameManager == null) return;

                if (!_gmCache.EnsureInitialized(_gameManager.GetType())) return;
                var gh = _gmCache.Handles;

                var uiHandler = gh.UIMessageHandler.GetValue(_gameManager);
                if (uiHandler == null) return;

                if (!_uiCache.EnsureInitialized(uiHandler.GetType())) return;
                var uh = _uiCache.Handles;

                var handlerType = uh.EmoteRecievedCallback.EventHandlerType;
                var method = typeof(EmoteAnnouncer).GetMethod(
                    nameof(OnEmoteReceived), BindingFlags.NonPublic | BindingFlags.Instance);
                _subscribedHandler = Delegate.CreateDelegate(handlerType, this, method);

                uh.EmoteRecievedCallback.AddEventHandler(uiHandler, _subscribedHandler);
                _uiMessageHandler = uiHandler;
                Log.Msg("EmoteAnnouncer", "Subscribed to UIMessageHandler.EmoteRecievedCallback");
            }
            catch (Exception ex)
            {
                Log.Warn("EmoteAnnouncer", $"Subscribe error: {ex.Message}");
            }
        }

        private void Unsubscribe()
        {
            if (_uiMessageHandler == null || _subscribedHandler == null) return;
            try
            {
                _uiCache.Handles?.EmoteRecievedCallback?.RemoveEventHandler(_uiMessageHandler, _subscribedHandler);
            }
            catch { }
            _subscribedHandler = null;
        }

        // Invoked by UIMessageHandler.Handle_OnChat when any incoming emote arrives.
        private void OnEmoteReceived(string emoteId)
        {
            if (string.IsNullOrEmpty(emoteId)) return;

            // OpponentDialogController already drops display when muted; mirror that gate here so the
            // user doesn't hear emotes they explicitly silenced.
            if (EmoteService.IsOpponentMuted()) return;

            string text = EmoteService.ResolveEmoteText(emoteId) ?? emoteId;
            _announcer.Announce(Strings.OpponentEmoted(text), AnnouncementPriority.Normal);
        }
    }
}
