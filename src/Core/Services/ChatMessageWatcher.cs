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

        // Reflection cache (never changes)
        private bool _reflectionInitialized;
        private FieldInfo _conversationsField;
        private FieldInfo _messageHistoryField;
        private FieldInfo _directionField;
        private FieldInfo _textBodyField;
        private FieldInfo _textTitleField;
        private FieldInfo _messageTypeField;
        private PropertyInfo _friendProp;
        private PropertyInfo _displayNameProp;
        private object _directionIncoming;
        private object _messageTypeChat;

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

                EnsureReflectionCached(socialUI.GetType());

                // SocialUI._socialManager -> ISocialManager.ChatManager
                var socialManagerField = socialUI.GetType().GetField("_socialManager", PrivateInstance);
                if (socialManagerField == null) { _lookupFailed = true; return null; }

                var socialManager = socialManagerField.GetValue(socialUI);
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

        private void EnsureReflectionCached(Type socialUIType)
        {
            if (_reflectionInitialized) return;
            _reflectionInitialized = true;

            // Conversation
            var conversationType = FindType("MTGA.Social.Conversation");
            if (conversationType != null)
            {
                _friendProp = conversationType.GetProperty("Friend", PublicInstance);
                _messageHistoryField = conversationType.GetField("MessageHistory", PublicInstance);
            }

            // ChatManager
            var chatManagerType = FindType("MTGA.Social.ChatManager");
            if (chatManagerType != null)
            {
                _conversationsField = chatManagerType.GetField("Conversations", PublicInstance);
            }

            // SocialMessage
            var socialMessageType = FindType("MTGA.Social.SocialMessage");
            if (socialMessageType != null)
            {
                _directionField = socialMessageType.GetField("Direction", PublicInstance);
                _textBodyField = socialMessageType.GetField("TextBody", PublicInstance);
                _textTitleField = socialMessageType.GetField("TextTitle", PublicInstance);
                _messageTypeField = socialMessageType.GetField("Type", PublicInstance);
            }

            // Direction enum
            var directionType = FindType("MTGA.Social.Direction");
            if (directionType != null)
                _directionIncoming = Enum.Parse(directionType, "Incoming");

            // MessageType enum
            var messageTypeEnum = FindType("MTGA.Social.MessageType");
            if (messageTypeEnum != null)
                _messageTypeChat = Enum.Parse(messageTypeEnum, "Chat");

            // SocialEntity
            var socialEntityType = FindType("MTGA.Social.SocialEntity") ?? FindType("SocialEntity");
            if (socialEntityType != null)
                _displayNameProp = socialEntityType.GetProperty("DisplayName", PublicInstance);
        }

        private void PollConversations(object chatManager)
        {
            if (_conversationsField == null || _messageHistoryField == null)
            {
                if (!_loggedFirstPoll)
                {
                    _loggedFirstPoll = true;
                    Log.Warn("ChatWatcher", $"Reflection fields null: conversations={_conversationsField != null}, messageHistory={_messageHistoryField != null}");
                }
                return;
            }

            try
            {
                var conversations = _conversationsField.GetValue(chatManager) as IList;
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

                    var history = _messageHistoryField.GetValue(conversation) as IList;
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

            try
            {
                // Check direction
                if (_directionField != null && _directionIncoming != null)
                {
                    var direction = _directionField.GetValue(message);
                    if (direction == null || !direction.Equals(_directionIncoming))
                        return false;
                }

                // Check message type
                if (_messageTypeField != null && _messageTypeChat != null)
                {
                    var msgType = _messageTypeField.GetValue(message);
                    if (msgType == null || !msgType.Equals(_messageTypeChat))
                        return false;
                }

                return true;
            }
            catch { return false; }
        }

        private void AnnounceMessage(object message, object conversation)
        {
            try
            {
                string body = _textBodyField?.GetValue(message)?.ToString();
                if (string.IsNullOrEmpty(body)) return;

                string senderName = _textTitleField?.GetValue(message)?.ToString();

                // Fall back to conversation friend name
                if (string.IsNullOrEmpty(senderName) && _friendProp != null && _displayNameProp != null)
                {
                    var friend = _friendProp.GetValue(conversation);
                    if (friend != null)
                        senderName = _displayNameProp.GetValue(friend) as string;
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
