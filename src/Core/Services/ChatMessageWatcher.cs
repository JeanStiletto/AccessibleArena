using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Polls ChatManager conversations for new incoming messages and announces them
    /// via AnnouncementService regardless of active navigator.
    /// Skips announcements when ChatNavigator is active (it has its own polling).
    /// </summary>
    public class ChatMessageWatcher
    {
        private const float PollInterval = 1.5f;

        private readonly IAnnouncementService _announcer;
        private float _pollTimer;

        // Cached references (cleared on scene change)
        private object _chatManager;
        private bool _lookupFailed;
        private bool _lookupAttempted;
        private bool _loggedLookupFailure;

        private sealed class ChatWatcherHandles
        {
            public FieldInfo SocialManager;
            public FieldInfo Conversations;
            public FieldInfo MessageHistory;
            public FieldInfo Direction;
            public FieldInfo TextBody;
            public FieldInfo TextTitle;
            public FieldInfo MessageType;
            public PropertyInfo Friend;
            public PropertyInfo DisplayName;
            public object DirectionIncoming;
            public object MessageTypeChat;
        }

        private static readonly ReflectionCache<ChatWatcherHandles> _watcherCache = new ReflectionCache<ChatWatcherHandles>(
            builder: socialUIType =>
            {
                var h = new ChatWatcherHandles
                {
                    SocialManager = socialUIType.GetField("_socialManager", PrivateInstance),
                };

                var conversationType = FindType("MTGA.Social.Conversation");
                if (conversationType != null)
                {
                    h.Friend = conversationType.GetProperty("Friend", PublicInstance);
                    h.MessageHistory = conversationType.GetField("MessageHistory", PublicInstance);
                }

                var chatManagerType = FindType("MTGA.Social.ChatManager");
                if (chatManagerType != null)
                    h.Conversations = chatManagerType.GetField("Conversations", PublicInstance);

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
            validator: h => h.Conversations != null && h.MessageHistory != null,
            logTag: "ChatWatcher",
            logSubject: "ChatManager");

        // Track known message counts per conversation (by identity)
        private readonly Dictionary<object, int> _knownMessageCounts = new Dictionary<object, int>();
        private bool _hasCompletedFirstPoll;
        private bool _loggedFirstPoll;

        public ChatMessageWatcher(IAnnouncementService announcer)
        {
            _announcer = announcer;
        }

        public void Update()
        {
            _pollTimer -= Time.deltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = PollInterval;

            // Skip when ChatNavigator or DuelChatNavigator is active (they do their own polling)
            if (NavigatorManager.Instance?.IsNavigatorActive("Chat") == true)
                return;
            if (DuelChatNavigator.IsActive)
                return;

            var chatManager = GetChatManager();
            if (chatManager == null)
            {
                if (!_loggedLookupFailure && _lookupAttempted)
                {
                    _loggedLookupFailure = true;
                    Log.Msg("ChatWatcher", "ChatManager not available (socialManager or chatManager null)");
                }
                return;
            }

            PollConversations(chatManager);
        }

        public void OnSceneChanged()
        {
            _chatManager = null;
            _lookupFailed = false;
            _lookupAttempted = false;
            _loggedLookupFailure = false;
            _knownMessageCounts.Clear();
            _hasCompletedFirstPoll = false;
            _loggedFirstPoll = false;
        }

        private object GetChatManager()
        {
            if (_chatManager != null) return _chatManager;
            if (_lookupFailed) return null;

            _lookupAttempted = true;

            try
            {
                var socialPanel = GameObject.Find("SocialUI_V2_Desktop_16x9(Clone)");
                if (socialPanel == null) return null;

                // Find SocialUI component (use GetComponent by name - same as GeneralMenuNavigator)
                var socialUI = socialPanel.GetComponent(T.SocialUI) as MonoBehaviour;
                if (socialUI == null) return null;

                if (!_watcherCache.EnsureInitialized(socialUI.GetType())) { _lookupFailed = true; return null; }
                var h = _watcherCache.Handles;

                // SocialUI._socialManager -> ISocialManager.ChatManager
                if (h.SocialManager == null) { _lookupFailed = true; return null; }

                var socialManager = h.SocialManager.GetValue(socialUI);
                if (socialManager == null) return null; // Transient - social not connected yet

                var chatManagerProp = socialManager.GetType().GetProperty("ChatManager", PublicInstance);
                if (chatManagerProp == null) { _lookupFailed = true; return null; }

                var cm = chatManagerProp.GetValue(socialManager);
                if (cm == null) return null; // Transient - no chat session yet

                _chatManager = cm;
                Log.Msg("ChatWatcher", $"ChatManager found, monitoring conversations");
                return _chatManager;
            }
            catch (Exception ex)
            {
                Log.Warn("ChatWatcher", $"Lookup error: {ex.Message}");
                _lookupFailed = true;
                return null;
            }
        }

        private void PollConversations(object chatManager)
        {
            var h = _watcherCache.Handles;
            if (h == null || h.Conversations == null || h.MessageHistory == null)
            {
                if (!_loggedFirstPoll)
                {
                    _loggedFirstPoll = true;
                    Log.Warn("ChatWatcher", $"Reflection handles not available");
                }
                return;
            }

            try
            {
                var conversations = h.Conversations.GetValue(chatManager) as IList;
                if (conversations == null)
                {
                    if (!_loggedFirstPoll)
                    {
                        _loggedFirstPoll = true;
                        Log.Msg("ChatWatcher", "Conversations field returned null or not IList");
                    }
                    return;
                }

                if (!_loggedFirstPoll)
                {
                    _loggedFirstPoll = true;
                    Log.Msg("ChatWatcher", $"First poll: {conversations.Count} conversations");
                }

                foreach (var conversation in conversations)
                {
                    if (conversation == null) continue;

                    var history = h.MessageHistory.GetValue(conversation) as IList;
                    if (history == null) continue;

                    int currentCount = history.Count;

                    if (_knownMessageCounts.TryGetValue(conversation, out int knownCount))
                    {
                        // Known conversation - check for count increase
                        if (currentCount > knownCount)
                        {
                            Log.Msg("ChatWatcher", $"Messages changed: {knownCount} -> {currentCount}");
                            for (int i = knownCount; i < currentCount; i++)
                            {
                                var message = history[i];
                                if (IsIncomingChatMessage(message))
                                {
                                    AnnounceMessage(message, conversation);
                                }
                            }
                        }
                    }
                    else if (_hasCompletedFirstPoll)
                    {
                        // New conversation appeared after first poll - likely created by incoming message.
                        // Check all its messages instead of silently recording baseline.
                        Log.Msg("ChatWatcher", $"New conversation detected with {currentCount} messages");
                        for (int i = 0; i < currentCount; i++)
                        {
                            var message = history[i];
                            if (IsIncomingChatMessage(message))
                            {
                                AnnounceMessage(message, conversation);
                            }
                        }
                    }
                    // else: first poll cycle, just record baseline (don't announce old messages)

                    _knownMessageCounts[conversation] = currentCount;
                }

                _hasCompletedFirstPoll = true;
            }
            catch (Exception ex)
            {
                Log.Warn("ChatWatcher", $"Poll error: {ex.Message}");
            }
        }

        private bool IsIncomingChatMessage(object message)
        {
            if (message == null) return false;
            var h = _watcherCache.Handles;
            if (h == null) return false;

            try
            {
                // Check direction
                if (h.Direction != null && h.DirectionIncoming != null)
                {
                    var direction = h.Direction.GetValue(message);
                    if (direction == null || !direction.Equals(h.DirectionIncoming))
                        return false;
                }

                // Check message type
                if (h.MessageType != null && h.MessageTypeChat != null)
                {
                    var msgType = h.MessageType.GetValue(message);
                    if (msgType == null || !msgType.Equals(h.MessageTypeChat))
                        return false;
                }

                return true;
            }
            catch { return false; }
        }

        private void AnnounceMessage(object message, object conversation)
        {
            var h = _watcherCache.Handles;
            if (h == null) return;

            try
            {
                string body = h.TextBody?.GetValue(message)?.ToString();
                if (string.IsNullOrEmpty(body)) return;

                string senderName = h.TextTitle?.GetValue(message)?.ToString();

                // Fall back to conversation friend name
                if (string.IsNullOrEmpty(senderName) && h.Friend != null && h.DisplayName != null)
                {
                    var friend = h.Friend.GetValue(conversation);
                    if (friend != null)
                        senderName = h.DisplayName.GetValue(friend) as string;
                }

                string announcement = !string.IsNullOrEmpty(senderName)
                    ? Strings.ChatMessageIncoming(senderName, body)
                    : body;

                _announcer.Announce(announcement, AnnouncementPriority.High);
            }
            catch { }
        }
    }
}
