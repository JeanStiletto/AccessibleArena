# ChatNavigator.cs
Path: src/Core/Services/ChatNavigator.cs
Lines: 856

## public class ChatNavigator : BaseNavigator (line 24)

Navigator for the Chat window (opened from friends panel). Up/Down navigate messages + input field + send button. Tab/Shift+Tab switch conversations. Backspace closes.

### Constants
- private const int ChatPriority = 52 (line 28)

### Properties
- public override string NavigatorId => "Chat" (line 34)
- public override string ScreenName => Strings.ScreenChat (line 35)
- public override int Priority => ChatPriority (line 36)
- protected override bool SupportsCardNavigation => false (line 37)
- protected override bool AcceptSpaceKey => false (line 38)

### Fields
- private MonoBehaviour _socialUI (line 44)
- private MonoBehaviour _chatWindow (line 45)
- private object _chatManager (line 46)
- private GameObject _chatWindowGameObject (line 47)
- private bool _reflectionInitialized (line 53)
- private PropertyInfo _chatVisibleProp (line 56)
- private MethodInfo _closeChatMethod (line 57)
- private MethodInfo _showFriendsListMethod (line 58)
- private FieldInfo _socialManagerField (line 59)
- private PropertyInfo _chatManagerProp (line 62)
- private PropertyInfo _currentConversationProp (line 65)
- private FieldInfo _conversationsField (line 66)
- private MethodInfo _selectNextConversationMethod (line 67)
- private FieldInfo _chatInputFieldField (line 70)
- private FieldInfo _sendButtonField (line 71)
- private FieldInfo _messagesViewField (line 72)
- private MethodInfo _trySendMessageMethod (line 73)
- private FieldInfo _activeMessagesField (line 76)
- private FieldInfo _titleField (line 79) — Localize
- private FieldInfo _bodyField (line 80) — Localize
- private PropertyInfo _messageProp (line 81) — SocialMessage
- private FieldInfo _directionField (line 84)
- private FieldInfo _textBodyField (line 85)
- private FieldInfo _textTitleField (line 86)
- private PropertyInfo _friendProp (line 89)
- private FieldInfo _messageHistoryField (line 90)
- private PropertyInfo _displayNameProp (line 93)
- private PropertyInfo _isOnlineProp (line 94)
- private object _directionIncoming (line 97)
- private string _currentFriendName (line 103)
- private bool _subscribedToMessageAdded (line 104)
- private float _rescanDelay (line 105)
- private bool _pendingRescan (line 106)
- private float _messageCheckTimer (line 107)
- private float _messageCheckInterval (line 108)
- private int _lastKnownMessageCount (line 109)

### Methods
- public ChatNavigator(IAnnouncementService announcer) : base(announcer) (line 115)
- protected override bool DetectScreen() (line 121)
- private MonoBehaviour FindChatWindow(GameObject socialPanel) (line 155)
- private MonoBehaviour FindComponentByTypeName(GameObject go, string typeName) (line 162)
- private object GetChatManager() (line 172)
- private void EnsureReflectionCached(Type socialUIType) (line 196)
- protected override void DiscoverElements() (line 280)
- private void DiscoverMessages() (line 308) — sorts tiles by sibling index (chronological)
- private string GetMessageLabel(object socialMessage, MonoBehaviour tile) (line 351)
- private bool IsIncomingMessage(object socialMessage) (line 375)
- private string ReadLocalizeText(MonoBehaviour tile, FieldInfo localizeField) (line 387)
- private GameObject GetChatInputField() (line 414)
- private GameObject GetSendButton() (line 426)
- protected override void OnActivated() (line 442)
- protected override void OnDeactivating() (line 447) — calls CloseChatSilent to prevent reactivation
- protected override string GetActivationAnnouncement() (line 463)
- protected override bool HandleCustomInput() (line 481) — Tab/Shift+Tab conversation switch, Backspace close
- protected override void HandleInputFieldNavigation() (line 505) — Enter sends message
- private void SwitchConversation(bool reverse) (line 530)
- private void CloseChat() (line 569)
- private void CloseChatSilent() (line 584)
- private void CloseChatAndReturnToFriends() (line 602)
- private void SendMessage() (line 624)
- private void SubscribeToMessageAdded() (line 647) — uses polling not actual event subscription
- private void UnsubscribeFromMessageAdded() (line 671)
- private void CheckForNewMessages() (line 680)
- private void AnnounceLatestMessage() (line 701)
- private string GetMessageBodyText(object socialMessage) (line 731)
- private string GetMessageSenderName(object socialMessage) (line 743)
- private string GetCurrentFriendName() (line 759)
- private int GetConversationCount() (line 778)
- private int GetMessageCount() (line 792)
- public override void Update() (line 815) — handles pending rescans and message polling
- private void PerformRescan() (line 837)
