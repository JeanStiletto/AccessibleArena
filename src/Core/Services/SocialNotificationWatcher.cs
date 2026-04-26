using UnityEngine;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Subscribes to ChatManager.NotificationAlert and announces every notification
    /// the game would otherwise show only as a corner toast: incoming chat messages,
    /// friend invites, PVP challenge invites, lobby invites/chat, and tournament-ready
    /// notifications. One subscription replaces the previous polling watcher and
    /// gives blind users feedback for events they could not perceive otherwise.
    /// </summary>
    public class SocialNotificationWatcher
    {
        private readonly IAnnouncementService _announcer;

        // Cached references (cleared on scene change)
        private object _chatManager;
        private Delegate _subscribedHandler;
        private bool _lookupFailed;
        private bool _loggedLookupFailure;
        private float _retryTimer;

        private sealed class WatcherHandles
        {
            public FieldInfo SocialManager;
            public EventInfo NotificationAlertEvent;
            public FieldInfo Direction;
            public FieldInfo TextBody;
            public FieldInfo TextTitle;
            public FieldInfo MessageType;
            public PropertyInfo DisplayName;
            public object DirectionIncoming;
            public object MessageTypeChat;
        }

        private static readonly ReflectionCache<WatcherHandles> _cache = new ReflectionCache<WatcherHandles>(
            builder: socialUIType =>
            {
                var h = new WatcherHandles
                {
                    SocialManager = socialUIType.GetField("_socialManager", PrivateInstance),
                };

                var chatManagerType = FindType("MTGA.Social.ChatManager");
                if (chatManagerType != null)
                    h.NotificationAlertEvent = chatManagerType.GetEvent("NotificationAlert", PublicInstance);

                var socialMessageType = FindType("MTGA.Social.SocialMessage");
                if (socialMessageType != null)
                {
                    h.Direction = socialMessageType.GetField("Direction", PublicInstance);
                    h.TextBody = socialMessageType.GetField("TextBody", PublicInstance);
                    h.TextTitle = socialMessageType.GetField("TextTitle", PublicInstance);
                    h.MessageType = socialMessageType.GetField("Type", PublicInstance);
                }

                var directionType = FindType("MTGA.Social.Direction");
                if (directionType != null)
                    h.DirectionIncoming = Enum.Parse(directionType, "Incoming");

                var messageTypeEnum = FindType("MTGA.Social.MessageType");
                if (messageTypeEnum != null)
                    h.MessageTypeChat = Enum.Parse(messageTypeEnum, "Chat");

                var socialEntityType = FindType("MTGA.Social.SocialEntity") ?? FindType("SocialEntity");
                if (socialEntityType != null)
                    h.DisplayName = socialEntityType.GetProperty("DisplayName", PublicInstance);

                return h;
            },
            validator: h => h.NotificationAlertEvent != null && h.TextBody != null,
            logTag: "SocialNotify",
            logSubject: "ChatManager.NotificationAlert");

        // SocialMessage.Friend is a public readonly field (not a property). Resolved on first use.
        private static FieldInfo _friendField;
        private static bool _friendFieldResolved;

        public SocialNotificationWatcher(IAnnouncementService announcer)
        {
            _announcer = announcer;
        }

        public void Update()
        {
            if (_chatManager != null) return;
            if (_lookupFailed) return;

            // Backoff: only retry the lookup every second to avoid spamming GameObject.Find.
            _retryTimer -= Time.deltaTime;
            if (_retryTimer > 0f) return;
            _retryTimer = 1f;

            TrySubscribe();
        }

        public void OnSceneChanged()
        {
            Unsubscribe();
            _chatManager = null;
            _lookupFailed = false;
            _loggedLookupFailure = false;
            _retryTimer = 0f;
        }

        private void TrySubscribe()
        {
            try
            {
                var socialPanel = GameObject.Find("SocialUI_V2_Desktop_16x9(Clone)");
                if (socialPanel == null) return; // Transient — panel not loaded yet

                var socialUI = socialPanel.GetComponent(T.SocialUI) as MonoBehaviour;
                if (socialUI == null) return;

                if (!_cache.EnsureInitialized(socialUI.GetType())) { _lookupFailed = true; return; }
                var h = _cache.Handles;
                if (h.SocialManager == null) { _lookupFailed = true; return; }

                var socialManager = h.SocialManager.GetValue(socialUI);
                if (socialManager == null) return; // Transient — social not connected yet

                var chatManagerProp = socialManager.GetType().GetProperty("ChatManager", PublicInstance);
                var cm = chatManagerProp?.GetValue(socialManager);
                if (cm == null) return; // Transient — no chat session yet

                // Bind handler delegate matching the event's expected type (Action<SocialMessage>).
                var handlerType = h.NotificationAlertEvent.EventHandlerType;
                var method = typeof(SocialNotificationWatcher).GetMethod(
                    nameof(OnNotificationAlert),
                    BindingFlags.NonPublic | BindingFlags.Instance);
                _subscribedHandler = Delegate.CreateDelegate(handlerType, this, method);

                h.NotificationAlertEvent.AddEventHandler(cm, _subscribedHandler);
                _chatManager = cm;
                Log.Msg("SocialNotify", "Subscribed to ChatManager.NotificationAlert");
            }
            catch (Exception ex)
            {
                if (!_loggedLookupFailure)
                {
                    _loggedLookupFailure = true;
                    Log.Warn("SocialNotify", $"Subscribe error: {ex.Message}");
                }
                _lookupFailed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_chatManager == null || _subscribedHandler == null) return;
            try
            {
                _cache.Handles?.NotificationAlertEvent?.RemoveEventHandler(_chatManager, _subscribedHandler);
            }
            catch { }
            _subscribedHandler = null;
        }

        // Invoked by the game on the main thread when any social notification is raised.
        private void OnNotificationAlert(object socialMessage)
        {
            if (socialMessage == null) return;
            var h = _cache.Handles;
            if (h == null) return;

            try
            {
                // Defensive: only announce incoming notifications (event normally only fires for those,
                // but Challenge construction sets Direction based on sender comparison).
                if (h.Direction != null && h.DirectionIncoming != null)
                {
                    var direction = h.Direction.GetValue(socialMessage);
                    if (direction != null && !direction.Equals(h.DirectionIncoming)) return;
                }

                // For chat messages, defer to the chat navigators when they are visible —
                // they handle their own announcements (and we'd otherwise double-speak).
                bool isChat = false;
                if (h.MessageType != null && h.MessageTypeChat != null)
                {
                    var msgType = h.MessageType.GetValue(socialMessage);
                    isChat = msgType != null && msgType.Equals(h.MessageTypeChat);
                }
                if (isChat)
                {
                    if (NavigatorManager.Instance?.IsNavigatorActive("Chat") == true) return;
                    if (DuelChatNavigator.IsActive) return;
                }

                string body = h.TextBody?.GetValue(socialMessage)?.ToString();
                if (string.IsNullOrEmpty(body)) return;

                string sender = h.TextTitle?.GetValue(socialMessage)?.ToString();
                if (string.IsNullOrEmpty(sender))
                    sender = ResolveFriendDisplayName(socialMessage, h);

                string announcement = !string.IsNullOrEmpty(sender)
                    ? Strings.ChatMessageIncoming(sender, body)
                    : body;

                _announcer.Announce(announcement, AnnouncementPriority.High);
            }
            catch (Exception ex)
            {
                Log.Warn("SocialNotify", $"Handler error: {ex.Message}");
            }
        }

        private static string ResolveFriendDisplayName(object socialMessage, WatcherHandles h)
        {
            if (!_friendFieldResolved)
            {
                _friendField = socialMessage.GetType().GetField("Friend", PublicInstance);
                _friendFieldResolved = true;
            }
            var friend = _friendField?.GetValue(socialMessage);
            if (friend == null || h.DisplayName == null) return null;
            return h.DisplayName.GetValue(friend) as string;
        }
    }
}
