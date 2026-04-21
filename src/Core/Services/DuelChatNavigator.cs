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
    /// Chat sub-navigator for use within DuelNavigator during friend duels.
    /// Operates like BrowserNavigator: managed by DuelNavigator, not NavigatorManager.
    /// F4 toggles chat open/close without deactivating DuelNavigator, preserving
    /// all sub-navigator state (zone position, focused card, battlefield row).
    ///
    /// Navigation:
    ///   Up/Down: Navigate messages (oldest to newest), input field, send button
    ///   Enter on input: Start editing. While editing: send message
    ///   Tab/Shift+Tab: Next/previous conversation
    ///   Backspace/F4: Close chat and return to duel
    /// </summary>
    public class DuelChatNavigator
    {
        private const float MaxWaitTime = 2.0f;
        private const float MessagePollInterval = 1.0f;
        private const float RescanDelay = 0.3f;

        private readonly IAnnouncementService _announcer;
        private readonly InputFieldEditHelper _inputFieldHelper;
        private readonly Action _onClosed;

        #region State

        private static bool _isActive;
        private bool _isWaitingForChat;
        private float _waitTimer;

        // Navigation
        private readonly List<ChatElement> _elements = new List<ChatElement>();
        private int _currentIndex = -1;

        // Message polling
        private float _messageCheckTimer;
        private int _lastKnownMessageCount;
        private bool _pendingRescan;
        private float _rescanDelay;

        // Current conversation
        private string _currentFriendName;

        #endregion

        #region Cached References

        private MonoBehaviour _socialUI;
        private MonoBehaviour _chatWindow;
        private object _chatManager;

        #endregion

        #region Reflection Cache

        // ISocialManager -> ChatManager (resolved lazily from live SocialManager instance)
        private PropertyInfo _chatManagerProp;

        private sealed class DuelChatHandles
        {
            // SocialUI
            public PropertyInfo ChatVisible;
            public MethodInfo CloseChat;
            public FieldInfo SocialManager;
            public MethodInfo ShowChatWindow;

            // ChatManager
            public PropertyInfo CurrentConversation;
            public FieldInfo Conversations;
            public MethodInfo SelectNextConversation;

            // ChatWindow
            public FieldInfo ChatInputField;
            public FieldInfo SendButton;
            public FieldInfo MessagesView;
            public MethodInfo TrySendMessage;

            // SocialMessagesView
            public FieldInfo ActiveMessages;

            // MessageTile
            public FieldInfo Title;
            public FieldInfo Body;

            // SocialMessage
            public FieldInfo Direction;
            public FieldInfo TextBody;
            public FieldInfo TextTitle;

            // Direction enum
            public object DirectionIncoming;

            // Conversation
            public PropertyInfo Friend;
            public FieldInfo MessageHistory;

            // SocialEntity
            public PropertyInfo DisplayName;
        }

        private static readonly ReflectionCache<DuelChatHandles> _duelChatCache = new ReflectionCache<DuelChatHandles>(
            builder: socialUIType =>
            {
                var h = new DuelChatHandles
                {
                    ChatVisible = socialUIType.GetProperty("ChatVisible", PublicInstance),
                    CloseChat = socialUIType.GetMethod("CloseChat", PublicInstance),
                    SocialManager = socialUIType.GetField("_socialManager", PrivateInstance),
                    ShowChatWindow = socialUIType.GetMethod("ShowChatWindow", PublicInstance),
                };

                var chatWindowType = FindType("ChatWindow");
                if (chatWindowType != null)
                {
                    h.ChatInputField = chatWindowType.GetField("_chatInputField", PrivateInstance);
                    h.SendButton = chatWindowType.GetField("_sendButton", PrivateInstance);
                    h.MessagesView = chatWindowType.GetField("_messagesView", PrivateInstance);
                    h.TrySendMessage = chatWindowType.GetMethod("TrySendMessage", PublicInstance);
                }

                var chatManagerType = FindType("MTGA.Social.ChatManager");
                if (chatManagerType != null)
                {
                    h.CurrentConversation = chatManagerType.GetProperty("CurrentConversation", PublicInstance);
                    h.Conversations = chatManagerType.GetField("Conversations", PublicInstance);
                    h.SelectNextConversation = chatManagerType.GetMethod("SelectNextConversation", PublicInstance);
                }

                var messagesViewType = FindType("SocialMessagesView");
                if (messagesViewType != null)
                    h.ActiveMessages = messagesViewType.GetField("_activeMessages", PrivateInstance);

                var messageTileType = FindType("MessageTile");
                if (messageTileType != null)
                {
                    h.Title = messageTileType.GetField("_title", PrivateInstance);
                    h.Body = messageTileType.GetField("_body", PrivateInstance);
                }

                var socialMessageType = FindType("MTGA.Social.SocialMessage");
                if (socialMessageType != null)
                {
                    h.Direction = socialMessageType.GetField("Direction", PublicInstance);
                    h.TextBody = socialMessageType.GetField("TextBody", PublicInstance);
                    h.TextTitle = socialMessageType.GetField("TextTitle", PublicInstance);
                }

                var directionType = FindType("MTGA.Social.Direction");
                if (directionType != null)
                    h.DirectionIncoming = Enum.Parse(directionType, "Incoming");

                var conversationType = FindType("MTGA.Social.Conversation");
                if (conversationType != null)
                {
                    h.Friend = conversationType.GetProperty("Friend", PublicInstance);
                    h.MessageHistory = conversationType.GetField("MessageHistory", PublicInstance);
                }

                var socialEntityType = FindType("MTGA.Social.SocialEntity") ?? FindType("SocialEntity");
                if (socialEntityType != null)
                    h.DisplayName = socialEntityType.GetProperty("DisplayName", PublicInstance);

                return h;
            },
            validator: h => h.ChatVisible != null && h.SocialManager != null && h.ShowChatWindow != null,
            logTag: "DuelChat",
            logSubject: "SocialUI");

        #endregion

        #region Element Type

        private enum ChatElementType { Message, InputField, SendButton }

        private struct ChatElement
        {
            public GameObject Go;
            public string Label;
            public ChatElementType Type;
        }

        #endregion

        /// <summary>Whether the duel chat sub-navigator is currently active.</summary>
        public static bool IsActive => _isActive;

        /// <summary>Whether the sub-navigator is waiting for the chat window to open.</summary>
        public bool IsWaiting => _isWaitingForChat;

        /// <param name="announcer">Screen reader announcement service</param>
        /// <param name="onClosed">Called when chat closes (for any reason) so DuelNavigator can re-disable SocialUI selectables</param>
        public DuelChatNavigator(IAnnouncementService announcer, Action onClosed)
        {
            _announcer = announcer;
            _onClosed = onClosed;
            _inputFieldHelper = new InputFieldEditHelper(announcer);
        }

        #region Open / Close

        /// <summary>
        /// Open chat window. Called when F4 is pressed during duel.
        /// Caller (DuelNavigator) must restore SocialUI selectables before calling this.
        /// </summary>
        public void Open(MonoBehaviour socialUI)
        {
            if (_isActive || _isWaitingForChat) return;

            _socialUI = socialUI;
            EnsureReflectionCached(socialUI.GetType());

            if (_duelChatCache.Handles.ShowChatWindow == null)
            {
                _announcer.AnnounceInterrupt(Strings.ChatUnavailable);
                Deactivate();
                return;
            }

            try
            {
                // ShowChatWindow(SocialEntity chatFriend = null) - pass null to open last conversation
                _duelChatCache.Handles.ShowChatWindow.Invoke(socialUI, new object[] { null });
            }
            catch (Exception ex)
            {
                Log.Warn("DuelChat", $"ShowChatWindow failed: {ex.Message}");
                _announcer.AnnounceInterrupt(Strings.ChatUnavailable);
                Deactivate();
                return;
            }

            _isWaitingForChat = true;
            _waitTimer = MaxWaitTime;
            Log.Msg("DuelChat", "Waiting for chat window to become visible...");
        }

        /// <summary>
        /// Close chat window and return to duel navigation.
        /// </summary>
        public void Close()
        {
            if (_socialUI != null && _duelChatCache.Handles.CloseChat != null)
            {
                try
                {
                    _duelChatCache.Handles.CloseChat.Invoke(_socialUI, null);
                }
                catch (Exception ex)
                {
                    Log.Warn("DuelChat", $"CloseChat failed: {ex.Message}");
                }
            }

            Deactivate();
            _announcer.AnnounceInterrupt(Strings.ChatClosed);
        }

        private void Deactivate()
        {
            bool wasActiveOrWaiting = _isActive || _isWaitingForChat;
            _isActive = false;
            _isWaitingForChat = false;
            _inputFieldHelper.Clear();
            _elements.Clear();
            _currentIndex = -1;
            _chatWindow = null;
            _chatManager = null;
            _currentFriendName = null;
            _pendingRescan = false;

            if (wasActiveOrWaiting)
                _onClosed?.Invoke();
        }

        #endregion

        #region Update

        /// <summary>
        /// Call each frame from DuelNavigator. Handles wait-for-visibility polling,
        /// chat-visible validation, rescan timers, and new message detection.
        /// </summary>
        public void Update()
        {
            if (_isWaitingForChat)
            {
                _waitTimer -= Time.deltaTime;
                if (_waitTimer <= 0f)
                {
                    Log.Warn("DuelChat", "Timed out waiting for chat visibility");
                    Deactivate();
                    _announcer.AnnounceInterrupt(Strings.ChatUnavailable);
                    return;
                }

                if (IsChatVisible())
                {
                    _isWaitingForChat = false;
                    ActivateChat();
                }
                return;
            }

            if (!_isActive) return;

            // Validate chat is still visible (game may close it externally)
            if (!IsChatVisible())
            {
                Log.Msg("DuelChat", "Chat window closed externally");
                Deactivate();
                return;
            }

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

        #endregion

        #region HandleInput

        /// <summary>
        /// Handle all input when active. Returns true to consume input (prevents duel actions).
        /// Also returns true while waiting for chat to open.
        /// </summary>
        public bool HandleInput()
        {
            // Consume all input while waiting for chat to open
            if (_isWaitingForChat) return true;

            if (!_isActive) return false;

            // F4: close chat (toggle off)
            if (Input.GetKeyDown(KeyCode.F4))
            {
                Close();
                return true;
            }

            // Input field editing mode - delegate to InputFieldEditHelper
            if (_inputFieldHelper.IsEditing)
            {
                HandleEditingInput();
                return true; // Always consume when active
            }

            // Tab/Shift+Tab: switch conversation
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool reverse = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                SwitchConversation(reverse);
                return true;
            }

            // Backspace: close chat
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                Close();
                return true;
            }

            // Up: previous element
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                MovePrevious();
                return true;
            }

            // Down: next element
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveNext();
                return true;
            }

            // Home: first element
            if (Input.GetKeyDown(KeyCode.Home))
            {
                if (_elements.Count > 0)
                {
                    _currentIndex = 0;
                    AnnounceCurrentElement();
                }
                return true;
            }

            // End: last element
            if (Input.GetKeyDown(KeyCode.End))
            {
                if (_elements.Count > 0)
                {
                    _currentIndex = _elements.Count - 1;
                    AnnounceCurrentElement();
                }
                return true;
            }

            // Enter: activate current element
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ActivateCurrentElement();
                return true;
            }

            // Consume all other keys to prevent duel actions while chat is open
            return true;
        }

        private void HandleEditingInput()
        {
            // Enter while editing: send message
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                SendMessage();
                return;
            }

            // F4 while editing: exit edit mode and close chat
            if (Input.GetKeyDown(KeyCode.F4))
            {
                _inputFieldHelper.ExitEditMode();
                Close();
                return;
            }

            // Tab while editing: exit edit mode, switch conversation
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool reverse = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                _inputFieldHelper.ExitEditMode();
                SwitchConversation(reverse);
                return;
            }

            // Delegate Escape, Backspace, arrow keys to InputFieldEditHelper
            _inputFieldHelper.HandleEditing(direction =>
            {
                // Tab callback from helper (shouldn't fire since we intercept Tab above)
                if (direction > 0) MoveNext();
                else MovePrevious();
            });

            // Track state for next frame's Backspace character detection
            _inputFieldHelper.TrackState();
        }

        #endregion

        #region Navigation

        private void MoveNext()
        {
            if (_elements.Count == 0) return;
            _currentIndex = (_currentIndex + 1) % _elements.Count;
            AnnounceCurrentElement();
        }

        private void MovePrevious()
        {
            if (_elements.Count == 0) return;
            _currentIndex = (_currentIndex - 1 + _elements.Count) % _elements.Count;
            AnnounceCurrentElement();
        }

        private void AnnounceCurrentElement()
        {
            if (_currentIndex < 0 || _currentIndex >= _elements.Count) return;
            _announcer.AnnounceInterrupt(_elements[_currentIndex].Label);
        }

        private void ActivateCurrentElement()
        {
            if (_currentIndex < 0 || _currentIndex >= _elements.Count) return;

            var element = _elements[_currentIndex];
            switch (element.Type)
            {
                case ChatElementType.InputField:
                    _inputFieldHelper.EnterEditMode(element.Go);
                    break;

                case ChatElementType.SendButton:
                    SendMessage();
                    break;

                case ChatElementType.Message:
                    // Re-announce the message
                    _announcer.AnnounceInterrupt(element.Label);
                    break;
            }
        }

        #endregion

        #region Chat Actions

        private void SendMessage()
        {
            if (_chatWindow == null || _duelChatCache.Handles.TrySendMessage == null) return;

            if (_inputFieldHelper.IsEditing)
                _inputFieldHelper.ExitEditMode();

            try
            {
                _duelChatCache.Handles.TrySendMessage.Invoke(_chatWindow, null);
                _announcer.Announce(Strings.ChatMessageSent, AnnouncementPriority.High);

                _pendingRescan = true;
                _rescanDelay = RescanDelay;
            }
            catch (Exception ex)
            {
                Log.Warn("DuelChat", $"Send failed: {ex.Message}");
            }
        }

        private void SwitchConversation(bool reverse)
        {
            if (_chatManager == null || _duelChatCache.Handles.SelectNextConversation == null) return;

            int count = GetConversationCount();
            if (count <= 1)
            {
                _announcer.AnnounceInterrupt(Strings.ChatNoConversation);
                return;
            }

            try
            {
                var parameters = _duelChatCache.Handles.SelectNextConversation.GetParameters();
                object[] args;
                if (parameters.Length >= 2)
                    args = new object[] { reverse, false };
                else if (parameters.Length == 1)
                    args = new object[] { reverse };
                else
                    args = Array.Empty<object>();

                _duelChatCache.Handles.SelectNextConversation.Invoke(_chatManager, args);

                _pendingRescan = true;
                _rescanDelay = RescanDelay;

                string announcement = reverse ? Strings.ChatPreviousConversation : Strings.ChatNextConversation;
                _announcer.AnnounceInterrupt(announcement);
            }
            catch (Exception ex)
            {
                Log.Warn("DuelChat", $"Switch conversation failed: {ex.Message}");
            }
        }

        #endregion

        #region Activation & Discovery

        private void ActivateChat()
        {
            var socialPanel = _socialUI?.gameObject;
            if (socialPanel == null)
            {
                _announcer.AnnounceInterrupt(Strings.ChatUnavailable);
                Deactivate();
                return;
            }

            _chatWindow = FindComponentByTypeName(socialPanel, T.ChatWindow);
            if (_chatWindow == null)
            {
                Log.Warn("DuelChat", "ChatWindow not found after visibility confirmed");
                _announcer.AnnounceInterrupt(Strings.ChatUnavailable);
                Deactivate();
                return;
            }

            _chatManager = GetChatManager();
            if (_chatManager == null)
            {
                Log.Warn("DuelChat", "ChatManager not found");
                _announcer.AnnounceInterrupt(Strings.ChatUnavailable);
                Deactivate();
                return;
            }

            DiscoverElements();

            if (_elements.Count == 0)
            {
                Log.Warn("DuelChat", "No elements discovered in chat window");
                _announcer.AnnounceInterrupt(Strings.ChatUnavailable);
                Deactivate();
                return;
            }

            _isActive = true;
            _currentIndex = 0;
            _lastKnownMessageCount = GetMessageCount();
            _messageCheckTimer = 0f;
            _currentFriendName = GetCurrentFriendName();

            string announcement = GetActivationAnnouncement();
            _announcer.AnnounceInterrupt(announcement);
            Log.Msg("DuelChat", $"Activated with {_elements.Count} elements, friend: {_currentFriendName}");
        }

        private void DiscoverElements()
        {
            _elements.Clear();

            // Messages (sorted chronologically)
            DiscoverMessages();

            // Input field
            var inputField = GetChatInputField();
            if (inputField != null)
            {
                _elements.Add(new ChatElement
                {
                    Go = inputField,
                    Label = Strings.ChatInputField,
                    Type = ChatElementType.InputField
                });
            }

            // Send button
            var sendButton = GetSendButton();
            if (sendButton != null)
            {
                _elements.Add(new ChatElement
                {
                    Go = sendButton,
                    Label = Strings.ChatSendButton,
                    Type = ChatElementType.SendButton
                });
            }
        }

        private void DiscoverMessages()
        {
            if (_chatWindow == null || _duelChatCache.Handles.MessagesView == null) return;

            try
            {
                var messagesView = _duelChatCache.Handles.MessagesView.GetValue(_chatWindow) as MonoBehaviour;
                if (messagesView == null || _duelChatCache.Handles.ActiveMessages == null) return;

                var activeMessages = _duelChatCache.Handles.ActiveMessages.GetValue(messagesView);
                if (activeMessages == null) return;

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
                    _elements.Add(new ChatElement
                    {
                        Go = tile.gameObject,
                        Label = label,
                        Type = ChatElementType.Message
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Warn("DuelChat", $"Failed to discover messages: {ex.Message}");
            }
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

        private string GetActivationAnnouncement()
        {
            string friendName = _currentFriendName ?? "?";
            int messageCount = GetMessageCount();

            string announcement = Strings.ChatWith(friendName);
            if (messageCount > 0)
                announcement += ". " + Strings.ChatMessages(messageCount);

            return Strings.WithHint(announcement, "NavigateHint");
        }

        #endregion

        #region Message Polling

        private void CheckForNewMessages()
        {
            _messageCheckTimer -= Time.deltaTime;
            if (_messageCheckTimer > 0f) return;
            _messageCheckTimer = MessagePollInterval;

            int currentCount = GetMessageCount();
            if (currentCount > _lastKnownMessageCount)
            {
                AnnounceLatestMessage();
                _pendingRescan = true;
                _rescanDelay = RescanDelay;
            }
            _lastKnownMessageCount = currentCount;
        }

        private void AnnounceLatestMessage()
        {
            try
            {
                var conversation = _duelChatCache.Handles.CurrentConversation?.GetValue(_chatManager);
                if (conversation == null || _duelChatCache.Handles.MessageHistory == null) return;

                var history = _duelChatCache.Handles.MessageHistory.GetValue(conversation) as IList;
                if (history == null || history.Count == 0) return;

                var latestMessage = history[history.Count - 1];
                if (latestMessage == null || !IsIncomingMessage(latestMessage)) return;

                string body = _duelChatCache.Handles.TextBody?.GetValue(latestMessage)?.ToString();
                string senderName = _duelChatCache.Handles.TextTitle?.GetValue(latestMessage)?.ToString();

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

        #endregion

        #region Scene Change

        public void OnSceneChanged()
        {
            Deactivate();
            _socialUI = null;
            // Keep reflection cache - types don't change between scenes
        }

        #endregion

        #region Helpers

        private bool IsChatVisible()
        {
            if (_socialUI == null || _duelChatCache.Handles.ChatVisible == null) return false;
            try
            {
                return (bool)_duelChatCache.Handles.ChatVisible.GetValue(_socialUI);
            }
            catch { return false; }
        }

        private object GetChatManager()
        {
            if (_socialUI == null || _duelChatCache.Handles.SocialManager == null) return null;
            try
            {
                var socialManager = _duelChatCache.Handles.SocialManager.GetValue(_socialUI);
                if (socialManager == null) return null;

                if (_chatManagerProp == null)
                    _chatManagerProp = socialManager.GetType().GetProperty("ChatManager", PublicInstance);
                if (_chatManagerProp == null) return null;

                return _chatManagerProp.GetValue(socialManager);
            }
            catch { return null; }
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

        private string GetCurrentFriendName()
        {
            if (_chatManager == null || _duelChatCache.Handles.CurrentConversation == null) return null;
            try
            {
                var conversation = _duelChatCache.Handles.CurrentConversation.GetValue(_chatManager);
                if (conversation == null || _duelChatCache.Handles.Friend == null) return null;
                var friend = _duelChatCache.Handles.Friend.GetValue(conversation);
                if (friend == null || _duelChatCache.Handles.DisplayName == null) return null;
                return _duelChatCache.Handles.DisplayName.GetValue(friend) as string;
            }
            catch { return null; }
        }

        private int GetConversationCount()
        {
            if (_chatManager == null || _duelChatCache.Handles.Conversations == null) return 0;
            try
            {
                var conversations = _duelChatCache.Handles.Conversations.GetValue(_chatManager);
                if (conversations is ICollection collection) return collection.Count;
                if (conversations is IList list) return list.Count;
                return 0;
            }
            catch { return 0; }
        }

        private int GetMessageCount()
        {
            if (_chatManager == null || _duelChatCache.Handles.CurrentConversation == null) return 0;
            try
            {
                var conversation = _duelChatCache.Handles.CurrentConversation.GetValue(_chatManager);
                if (conversation == null || _duelChatCache.Handles.MessageHistory == null) return 0;
                var history = _duelChatCache.Handles.MessageHistory.GetValue(conversation) as IList;
                return history?.Count ?? 0;
            }
            catch { return 0; }
        }

        private string GetMessageLabel(object socialMessage, MonoBehaviour tile)
        {
            string title = ReadLocalizeText(tile, _duelChatCache.Handles.Title);
            string body = ReadLocalizeText(tile, _duelChatCache.Handles.Body);

            if (!string.IsNullOrEmpty(body))
            {
                bool isIncoming = IsIncomingMessage(socialMessage);
                if (isIncoming && !string.IsNullOrEmpty(title))
                    return Strings.ChatMessageIncoming(title, body);
                if (!isIncoming)
                    return Strings.ChatMessageOutgoing(body);
                return body;
            }

            return "...";
        }

        private bool IsIncomingMessage(object socialMessage)
        {
            if (socialMessage == null || _duelChatCache.Handles.Direction == null || _duelChatCache.Handles.DirectionIncoming == null) return true;
            try
            {
                var direction = _duelChatCache.Handles.Direction.GetValue(socialMessage);
                return direction != null && direction.Equals(_duelChatCache.Handles.DirectionIncoming);
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
                var locComp = localize as MonoBehaviour;
                if (locComp == null || !locComp.gameObject.activeInHierarchy) return null;
                var tmpText = locComp.GetComponent<TMPro.TMP_Text>();
                return tmpText?.text;
            }
            catch { return null; }
        }

        private GameObject GetChatInputField()
        {
            if (_chatWindow == null || _duelChatCache.Handles.ChatInputField == null) return null;
            try
            {
                var inputField = _duelChatCache.Handles.ChatInputField.GetValue(_chatWindow) as Component;
                return inputField?.gameObject;
            }
            catch { return null; }
        }

        private GameObject GetSendButton()
        {
            if (_chatWindow == null || _duelChatCache.Handles.SendButton == null) return null;
            try
            {
                var button = _duelChatCache.Handles.SendButton.GetValue(_chatWindow) as Component;
                return button?.gameObject;
            }
            catch { return null; }
        }

        #endregion

        #region Reflection Caching

        private void EnsureReflectionCached(Type socialUIType)
        {
            _duelChatCache.EnsureInitialized(socialUIType);
        }

        #endregion
    }
}
