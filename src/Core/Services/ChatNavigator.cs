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

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for the Chat window (opened from friends panel).
    /// Provides keyboard navigation for messages, input field, and send button.
    ///
    /// Navigation:
    ///   Up/Down: Navigate messages (oldest to newest), input field, send button
    ///   Enter on input: Start editing. While editing: send message
    ///   Tab/Shift+Tab: Next/previous conversation
    ///   Backspace: Close chat
    /// </summary>
    public class ChatNavigator : BaseNavigator
    {
        #region Constants

        private const int ChatPriority = 52;

        #endregion

        #region Navigator Identity

        public override string NavigatorId => "Chat";
        public override string ScreenName => Strings.ScreenChat;
        public override int Priority => ChatPriority;
        protected override bool SupportsCardNavigation => false;
        protected override bool AcceptSpaceKey => false;

        #endregion

        #region Cached References

        private MonoBehaviour _socialUI;
        private MonoBehaviour _chatWindow;
        private object _chatManager;
        private GameObject _chatWindowGameObject;

        #endregion

        #region Reflection Cache

        private bool _reflectionInitialized;

        // SocialUI
        private PropertyInfo _chatVisibleProp;
        private MethodInfo _closeChatMethod;
        private FieldInfo _socialManagerField;

        // ISocialManager -> ChatManager
        private PropertyInfo _chatManagerProp;

        // ChatManager
        private PropertyInfo _currentConversationProp;
        private FieldInfo _conversationsField;
        private MethodInfo _selectNextConversationMethod;

        // ChatWindow
        private FieldInfo _chatInputFieldField;
        private FieldInfo _sendButtonField;
        private FieldInfo _messagesViewField;
        private MethodInfo _trySendMessageMethod;

        // SocialMessagesView
        private FieldInfo _activeMessagesField;

        // MessageTile
        private FieldInfo _titleField; // Localize
        private FieldInfo _bodyField;  // Localize
        private PropertyInfo _messageProp; // SocialMessage

        // SocialMessage
        private FieldInfo _directionField;
        private FieldInfo _textBodyField;
        private FieldInfo _textTitleField;

        // Conversation
        private PropertyInfo _friendProp;
        private FieldInfo _messageHistoryField;

        // SocialEntity
        private PropertyInfo _displayNameProp;
        private PropertyInfo _isOnlineProp;

        // Direction enum
        private object _directionIncoming;

        #endregion

        #region State

        private string _currentFriendName;
        private bool _subscribedToMessageAdded;
        private float _rescanDelay;
        private bool _pendingRescan;
        private float _messageCheckTimer;
        private float _messageCheckInterval;
        private int _lastKnownMessageCount;

        #endregion

        #region Constructor

        public ChatNavigator(IAnnouncementService announcer) : base(announcer) { }

        #endregion

        #region Screen Detection

        protected override bool DetectScreen()
        {
            var socialPanel = GameObject.Find("SocialUI_V2_Desktop_16x9(Clone)");
            if (socialPanel == null) return false;

            var socialComp = FindComponentByTypeName(socialPanel, T.SocialUI);
            if (socialComp == null) return false;

            EnsureReflectionCached(socialComp.GetType());

            if (_chatVisibleProp == null) return false;

            try
            {
                bool chatVisible = (bool)_chatVisibleProp.GetValue(socialComp);
                if (!chatVisible) return false;
            }
            catch { return false; }

            _socialUI = socialComp;

            // Find the ChatWindow component
            _chatWindow = FindChatWindow(socialPanel);
            if (_chatWindow == null) return false;

            _chatWindowGameObject = _chatWindow.gameObject;

            // Get ChatManager reference
            _chatManager = GetChatManager();
            if (_chatManager == null) return false;

            return true;
        }

        private MonoBehaviour FindChatWindow(GameObject socialPanel)
        {
            // ChatWindow is instantiated under SocialUI._safeZoneRoot (not MobileSafeArea).
            // Search the entire panel hierarchy for an active ChatWindow component.
            return FindComponentByTypeName(socialPanel, T.ChatWindow);
        }

        private MonoBehaviour FindComponentByTypeName(GameObject go, string typeName)
        {
            foreach (var comp in go.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (comp != null && comp.GetType().Name == typeName)
                    return comp;
            }
            return null;
        }

        private object GetChatManager()
        {
            if (_socialUI == null || _socialManagerField == null) return null;

            try
            {
                var socialManager = _socialManagerField.GetValue(_socialUI);
                if (socialManager == null) return null;

                if (_chatManagerProp == null)
                {
                    _chatManagerProp = socialManager.GetType().GetProperty("ChatManager", PublicInstance);
                }
                if (_chatManagerProp == null) return null;

                return _chatManagerProp.GetValue(socialManager);
            }
            catch { return null; }
        }

        #endregion

        #region Reflection Caching

        private void EnsureReflectionCached(Type socialUIType)
        {
            if (_reflectionInitialized) return;
            _reflectionInitialized = true;

            // SocialUI
            _chatVisibleProp = socialUIType.GetProperty("ChatVisible", PublicInstance);
            _closeChatMethod = socialUIType.GetMethod("CloseChat", PublicInstance);
            _socialManagerField = socialUIType.GetField("_socialManager", PrivateInstance);

            // ChatWindow type
            var chatWindowType = FindType("ChatWindow");
            if (chatWindowType != null)
            {
                _chatInputFieldField = chatWindowType.GetField("_chatInputField", PrivateInstance);
                _sendButtonField = chatWindowType.GetField("_sendButton", PrivateInstance);
                _messagesViewField = chatWindowType.GetField("_messagesView", PrivateInstance);
                _trySendMessageMethod = chatWindowType.GetMethod("TrySendMessage", PublicInstance);
            }

            // ChatManager type
            var chatManagerType = FindType("MTGA.Social.ChatManager");
            if (chatManagerType != null)
            {
                _currentConversationProp = chatManagerType.GetProperty("CurrentConversation", PublicInstance);
                _conversationsField = chatManagerType.GetField("Conversations", PublicInstance);
                _selectNextConversationMethod = chatManagerType.GetMethod("SelectNextConversation", PublicInstance);
            }

            // SocialMessagesView type
            var messagesViewType = FindType("SocialMessagesView");
            if (messagesViewType != null)
            {
                _activeMessagesField = messagesViewType.GetField("_activeMessages", PrivateInstance);
            }

            // MessageTile type
            var messageTileType = FindType("MessageTile");
            if (messageTileType != null)
            {
                _titleField = messageTileType.GetField("_title", PrivateInstance);
                _bodyField = messageTileType.GetField("_body", PrivateInstance);
                _messageProp = messageTileType.GetProperty("Message", PublicInstance);
            }

            // SocialMessage type
            var socialMessageType = FindType("MTGA.Social.SocialMessage");
            if (socialMessageType != null)
            {
                _directionField = socialMessageType.GetField("Direction", PublicInstance);
                _textBodyField = socialMessageType.GetField("TextBody", PublicInstance);
                _textTitleField = socialMessageType.GetField("TextTitle", PublicInstance);
            }

            // Direction enum
            var directionType = FindType("MTGA.Social.Direction");
            if (directionType != null)
            {
                _directionIncoming = Enum.Parse(directionType, "Incoming");
            }

            // Conversation type
            var conversationType = FindType("MTGA.Social.Conversation");
            if (conversationType != null)
            {
                _friendProp = conversationType.GetProperty("Friend", PublicInstance);
                _messageHistoryField = conversationType.GetField("MessageHistory", PublicInstance);
            }

            // SocialEntity type
            var socialEntityType = FindType("MTGA.Social.SocialEntity");
            if (socialEntityType == null) socialEntityType = FindType("SocialEntity");
            if (socialEntityType != null)
            {
                _displayNameProp = socialEntityType.GetProperty("DisplayName", PublicInstance);
                _isOnlineProp = socialEntityType.GetProperty("IsOnline", PublicInstance);
            }
        }

        #endregion

        #region Element Discovery

        protected override void DiscoverElements()
        {
            _currentFriendName = GetCurrentFriendName();

            // Add message tiles from the active messages view
            DiscoverMessages();

            // Add input field
            var inputField = GetChatInputField();
            if (inputField != null)
            {
                AddElement(inputField, Strings.ChatInputField);
            }

            // Add send button
            var sendButton = GetSendButton();
            if (sendButton != null)
            {
                AddElement(sendButton, Strings.ChatSendButton);
            }

            // Ensure at least one element for BaseNavigator validation
            if (_elements.Count == 0 && _chatWindowGameObject != null)
            {
                AddElement(_chatWindowGameObject, Strings.ScreenChat);
            }
        }

        private void DiscoverMessages()
        {
            if (_chatWindow == null || _messagesViewField == null) return;

            try
            {
                var messagesView = _messagesViewField.GetValue(_chatWindow) as MonoBehaviour;
                if (messagesView == null || _activeMessagesField == null) return;

                var activeMessages = _activeMessagesField.GetValue(messagesView);
                if (activeMessages == null) return;

                // _activeMessages is Dictionary<SocialMessage, MessageTile>
                // We need to iterate and sort by sibling index (chronological order)
                var dict = activeMessages as IDictionary;
                if (dict == null || dict.Count == 0) return;

                var tiles = new List<(MonoBehaviour tile, string label, int siblingIndex)>();

                foreach (DictionaryEntry entry in dict)
                {
                    var tile = entry.Value as MonoBehaviour;
                    if (tile == null || !tile.gameObject.activeInHierarchy) continue;

                    string label = GetMessageLabel(entry.Key, tile);
                    int siblingIndex = tile.transform.GetSiblingIndex();
                    tiles.Add((tile, label, siblingIndex));
                }

                // Sort by sibling index (chronological order)
                tiles.Sort((a, b) => a.siblingIndex.CompareTo(b.siblingIndex));

                foreach (var (tile, label, _) in tiles)
                {
                    AddElement(tile.gameObject, label);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Chat] Failed to discover messages: {ex.Message}");
            }
        }

        private string GetMessageLabel(object socialMessage, MonoBehaviour tile)
        {
            // Try to read from the Localize components on the tile (already rendered text)
            string title = ReadLocalizeText(tile, _titleField);
            string body = ReadLocalizeText(tile, _bodyField);

            if (!string.IsNullOrEmpty(body))
            {
                // Determine direction from SocialMessage
                bool isIncoming = IsIncomingMessage(socialMessage);
                if (isIncoming && !string.IsNullOrEmpty(title))
                {
                    return Strings.ChatMessageIncoming(title, body);
                }
                else if (!isIncoming)
                {
                    return Strings.ChatMessageOutgoing(body);
                }
                return body;
            }

            return "...";
        }

        private bool IsIncomingMessage(object socialMessage)
        {
            if (socialMessage == null || _directionField == null || _directionIncoming == null) return true;

            try
            {
                var direction = _directionField.GetValue(socialMessage);
                return direction != null && direction.Equals(_directionIncoming);
            }
            catch { return true; }
        }

        private string ReadLocalizeText(MonoBehaviour tile, FieldInfo localizeField)
        {
            if (localizeField == null) return null;

            try
            {
                var localize = localizeField.GetValue(tile);
                if (localize == null) return null;

                // Localize component - get the text from the underlying TMP_Text
                var locComp = localize as MonoBehaviour;
                if (locComp == null) return null;

                // Check if the parent GO is active (title GO is hidden for outgoing)
                if (!locComp.gameObject.activeInHierarchy) return null;

                var tmpText = locComp.GetComponent<TMPro.TMP_Text>();
                if (tmpText != null)
                {
                    return tmpText.text;
                }
            }
            catch { }

            return null;
        }

        private GameObject GetChatInputField()
        {
            if (_chatWindow == null || _chatInputFieldField == null) return null;

            try
            {
                var inputField = _chatInputFieldField.GetValue(_chatWindow) as Component;
                return inputField?.gameObject;
            }
            catch { return null; }
        }

        private GameObject GetSendButton()
        {
            if (_chatWindow == null || _sendButtonField == null) return null;

            try
            {
                var button = _sendButtonField.GetValue(_chatWindow) as Component;
                return button?.gameObject;
            }
            catch { return null; }
        }

        #endregion

        #region Activation

        protected override void OnActivated()
        {
            SubscribeToMessageAdded();
        }

        protected override void OnDeactivating()
        {
            UnsubscribeFromMessageAdded();
            _chatWindow = null;
            _chatWindowGameObject = null;
            _chatManager = null;
            _socialUI = null;
            _currentFriendName = null;
            _pendingRescan = false;
        }

        protected override string GetActivationAnnouncement()
        {
            string friendName = _currentFriendName ?? "?";
            int messageCount = GetMessageCount();

            string announcement = Strings.ChatWith(friendName);
            if (messageCount > 0)
            {
                announcement += ". " + Strings.ChatMessages(messageCount);
            }

            return Strings.WithHint(announcement, "NavigateHint");
        }

        #endregion

        #region Custom Input

        protected override bool HandleCustomInput()
        {
            // Tab/Shift+Tab: Switch conversations
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool reverse = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                SwitchConversation(reverse);
                return true;
            }

            // Backspace: Close chat (when not editing input field)
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                CloseChat();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Override input field navigation to intercept Enter for sending messages.
        /// </summary>
        protected override void HandleInputFieldNavigation()
        {
            // Enter while editing: send message instead of exiting edit mode
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                SendMessage();
                return;
            }

            // Tab while editing: switch conversation
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool reverse = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                SwitchConversation(reverse);
                return;
            }

            // Let base handle Escape, Backspace character announce, arrow keys etc.
            base.HandleInputFieldNavigation();
        }

        #endregion

        #region Chat Actions

        private void SwitchConversation(bool reverse)
        {
            if (_chatManager == null || _selectNextConversationMethod == null) return;

            // Don't switch if there's only one (or no) conversation
            int conversationCount = GetConversationCount();
            if (conversationCount <= 1)
            {
                _announcer.AnnounceInterrupt(Strings.ChatNoConversation);
                return;
            }

            try
            {
                // SelectNextConversation(bool reverse = false, bool unseenOnly = false)
                var parameters = _selectNextConversationMethod.GetParameters();
                object[] args;
                if (parameters.Length >= 2)
                    args = new object[] { reverse, false };
                else if (parameters.Length == 1)
                    args = new object[] { reverse };
                else
                    args = Array.Empty<object>();

                _selectNextConversationMethod.Invoke(_chatManager, args);

                // Rescan after conversation switch
                _pendingRescan = true;
                _rescanDelay = 0.3f;

                string announcement = reverse ? Strings.ChatPreviousConversation : Strings.ChatNextConversation;
                _announcer.AnnounceInterrupt(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Chat] Failed to switch conversation: {ex.Message}");
            }
        }

        private void CloseChat()
        {
            if (_socialUI == null || _closeChatMethod == null) return;

            try
            {
                _closeChatMethod.Invoke(_socialUI, null);
                _announcer.AnnounceInterrupt(Strings.ChatClosed);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Chat] Failed to close chat: {ex.Message}");
            }
        }

        private void SendMessage()
        {
            if (_chatWindow == null || _trySendMessageMethod == null) return;

            try
            {
                _trySendMessageMethod.Invoke(_chatWindow, null);
                _announcer.Announce(Strings.ChatMessageSent, AnnouncementPriority.High);

                // Rescan to show new message
                _pendingRescan = true;
                _rescanDelay = 0.3f;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Chat] Failed to send message: {ex.Message}");
            }
        }

        #endregion

        #region Message Event Subscription

        private void SubscribeToMessageAdded()
        {
            if (_subscribedToMessageAdded || _chatManager == null) return;

            try
            {
                // MessageAdded is Action<SocialMessage, Conversation>
                // We can't directly match generic Action<T1,T2> with (object,object) signature.
                // Instead, use a lambda approach via DynamicMethod or polling.
                // Simplest reliable approach: poll for new messages via rescan timer.
                // We set up a periodic check instead of direct event subscription.
                _subscribedToMessageAdded = true;

                // Start periodic message check (every ~1 second)
                _messageCheckInterval = 1.0f;
                _messageCheckTimer = 0f;
                _lastKnownMessageCount = GetMessageCount();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Chat] Failed to set up message monitoring: {ex.Message}");
            }
        }

        private void UnsubscribeFromMessageAdded()
        {
            _subscribedToMessageAdded = false;
        }

        /// <summary>
        /// Check for new messages by polling message count.
        /// Called from Update when active.
        /// </summary>
        private void CheckForNewMessages()
        {
            if (!_subscribedToMessageAdded) return;

            _messageCheckTimer -= Time.deltaTime;
            if (_messageCheckTimer > 0f) return;
            _messageCheckTimer = _messageCheckInterval;

            int currentCount = GetMessageCount();
            if (currentCount > _lastKnownMessageCount)
            {
                // New message(s) arrived - announce the latest one
                AnnounceLatestMessage();

                // Schedule rescan to update message list
                _pendingRescan = true;
                _rescanDelay = 0.3f;
            }
            _lastKnownMessageCount = currentCount;
        }

        private void AnnounceLatestMessage()
        {
            try
            {
                var conversation = _currentConversationProp?.GetValue(_chatManager);
                if (conversation == null || _messageHistoryField == null) return;

                var history = _messageHistoryField.GetValue(conversation) as IList;
                if (history == null || history.Count == 0) return;

                var latestMessage = history[history.Count - 1];
                if (latestMessage == null) return;

                // Only announce incoming messages
                if (!IsIncomingMessage(latestMessage)) return;

                string body = GetMessageBodyText(latestMessage);
                string senderName = GetMessageSenderName(latestMessage);

                if (!string.IsNullOrEmpty(body))
                {
                    string announcement = !string.IsNullOrEmpty(senderName)
                        ? Strings.ChatMessageIncoming(senderName, body)
                        : body;
                    _announcer.Announce(announcement, AnnouncementPriority.High);
                }
            }
            catch { }
        }

        private string GetMessageBodyText(object socialMessage)
        {
            if (socialMessage == null || _textBodyField == null) return null;

            try
            {
                var textBody = _textBodyField.GetValue(socialMessage);
                return textBody?.ToString();
            }
            catch { return null; }
        }

        private string GetMessageSenderName(object socialMessage)
        {
            if (socialMessage == null || _textTitleField == null) return null;

            try
            {
                var textTitle = _textTitleField.GetValue(socialMessage);
                return textTitle?.ToString();
            }
            catch { return null; }
        }

        #endregion

        #region Helpers

        private string GetCurrentFriendName()
        {
            if (_chatManager == null || _currentConversationProp == null) return null;

            try
            {
                var conversation = _currentConversationProp.GetValue(_chatManager);
                if (conversation == null) return null;

                if (_friendProp == null) return null;
                var friend = _friendProp.GetValue(conversation);
                if (friend == null) return null;

                if (_displayNameProp == null) return null;
                return _displayNameProp.GetValue(friend) as string;
            }
            catch { return null; }
        }

        private int GetConversationCount()
        {
            if (_chatManager == null || _conversationsField == null) return 0;

            try
            {
                var conversations = _conversationsField.GetValue(_chatManager);
                if (conversations is ICollection collection) return collection.Count;
                if (conversations is IList list) return list.Count;
                return 0;
            }
            catch { return 0; }
        }

        private int GetMessageCount()
        {
            if (_chatManager == null || _currentConversationProp == null) return 0;

            try
            {
                var conversation = _currentConversationProp.GetValue(_chatManager);
                if (conversation == null) return 0;

                if (_messageHistoryField == null) return 0;
                var history = _messageHistoryField.GetValue(conversation) as IList;
                return history?.Count ?? 0;
            }
            catch { return 0; }
        }

        #endregion

        #region Update (rescan timer)

        /// <summary>
        /// Override Update to handle pending rescans (after conversation switch or new message).
        /// </summary>
        public override void Update()
        {
            if (_isActive)
            {
                // Handle pending rescan (after conversation switch or new message)
                if (_pendingRescan)
                {
                    _rescanDelay -= Time.deltaTime;
                    if (_rescanDelay <= 0f)
                    {
                        _pendingRescan = false;
                        PerformRescan();
                    }
                }

                // Poll for new messages
                CheckForNewMessages();
            }

            base.Update();
        }

        private void PerformRescan()
        {
            _elements.Clear();
            _currentIndex = -1;

            DiscoverElements();

            if (_elements.Count > 0)
            {
                _currentIndex = 0;
                _currentFriendName = GetCurrentFriendName();

                string announcement = GetActivationAnnouncement();
                _announcer.AnnounceInterrupt(announcement);
            }
        }

        #endregion
    }
}
