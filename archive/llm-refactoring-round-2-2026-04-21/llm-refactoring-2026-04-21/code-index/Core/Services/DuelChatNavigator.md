# DuelChatNavigator.cs
Path: src/Core/Services/DuelChatNavigator.cs
Lines: 950

## Top-level comments
- Chat sub-navigator for in-duel F4 toggling. Opens SocialUI.ShowChatWindow, waits up to 2s for ChatVisible, then discovers messages/input-field/send-button via reflection. Tab cycles conversations, Up/Down navigates elements, Enter on input enters edit mode and sends the message. Preserves DuelNavigator state across open/close.

## private enum ChatElementType (line 115, nested in DuelChatNavigator)
- Message (line 115)
- InputField (line 115)
- SendButton (line 115)

## private struct ChatElement (line 117, nested in DuelChatNavigator)
### Fields
- public GameObject Go (line 119)
- public string Label (line 120)
- public ChatElementType Type (line 121)

## public class DuelChatNavigator (line 26)
### Fields
- private const float MaxWaitTime = 2.0f (line 28)
- private const float MessagePollInterval = 1.0f (line 29)
- private const float RescanDelay = 0.3f (line 30)
- private readonly IAnnouncementService _announcer (line 32)
- private readonly InputFieldEditHelper _inputFieldHelper (line 33)
- private readonly Action _onClosed (line 34)
- private static bool _isActive (line 38)
- private bool _isWaitingForChat (line 39)
- private float _waitTimer (line 40)
- private readonly List<ChatElement> _elements = new List<ChatElement>() (line 43)
- private int _currentIndex = -1 (line 44)
- private float _messageCheckTimer (line 47)
- private int _lastKnownMessageCount (line 48)
- private bool _pendingRescan (line 49)
- private float _rescanDelay (line 50)
- private string _currentFriendName (line 53)
- private MonoBehaviour _socialUI (line 59)
- private MonoBehaviour _chatWindow (line 60)
- private object _chatManager (line 61)
- private bool _reflectionInitialized (line 67)
- private PropertyInfo _chatVisibleProp (line 70)
- private MethodInfo _closeChatMethod (line 71)
- private FieldInfo _socialManagerField (line 72)
- private MethodInfo _showChatWindowMethod (line 73)
- private PropertyInfo _chatManagerProp (line 76)
- private PropertyInfo _currentConversationProp (line 79)
- private FieldInfo _conversationsField (line 80)
- private MethodInfo _selectNextConversationMethod (line 81)
- private FieldInfo _chatInputFieldField (line 84)
- private FieldInfo _sendButtonField (line 85)
- private FieldInfo _messagesViewField (line 86)
- private MethodInfo _trySendMessageMethod (line 87)
- private FieldInfo _activeMessagesField (line 90)
- private FieldInfo _titleField (line 93)
- private FieldInfo _bodyField (line 94)
- private FieldInfo _directionField (line 97)
- private FieldInfo _textBodyField (line 98)
- private FieldInfo _textTitleField (line 99)
- private object _directionIncoming (line 102)
- private PropertyInfo _friendProp (line 105)
- private FieldInfo _messageHistoryField (line 106)
- private PropertyInfo _displayNameProp (line 109)
### Properties
- public static bool IsActive (line 127) — Note: static so ChatMessageWatcher can check it without a reference
- public bool IsWaiting (line 130)
### Methods
- public DuelChatNavigator(IAnnouncementService announcer, Action onClosed) (line 134)
- public void Open(MonoBehaviour socialUI) (line 147) — Note: DuelNavigator must restore SocialUI selectables before calling this
- public void Close() (line 182)
- private void Deactivate() (line 200) — Note: invokes onClosed callback so DuelNavigator re-disables SocialUI selectables
- public void Update() (line 225) — Note: times out waiting after 2s, validates ChatVisible each frame, polls for new incoming messages
- public bool HandleInput() (line 279) — Note: returns true for all keys while active or waiting, blocking duel actions
- private void HandleEditingInput() (line 362)
- private void MoveNext() (line 404)
- private void MovePrevious() (line 411)
- private void AnnounceCurrentElement() (line 418)
- private void ActivateCurrentElement() (line 424)
- private void SendMessage() (line 450)
- private void SwitchConversation(bool reverse) (line 471)
- private void ActivateChat() (line 511)
- private void DiscoverElements() (line 560)
- private void DiscoverMessages() (line 592) — Note: sorts tiles by sibling index for chronological order
- private void PerformRescan() (line 638)
- private string GetActivationAnnouncement() (line 655)
- private void CheckForNewMessages() (line 671)
- private void AnnounceLatestMessage() (line 687) — Note: only announces incoming messages; uses High priority
- public void OnSceneChanged() (line 718)
- private bool IsChatVisible() (line 729)
- private object GetChatManager() (line 739)
- private MonoBehaviour FindComponentByTypeName(GameObject go, string typeName) (line 756)
- private string GetCurrentFriendName() (line 766)
- private int GetConversationCount() (line 780)
- private int GetMessageCount() (line 793)
- private string GetMessageLabel(object socialMessage, MonoBehaviour tile) (line 806)
- private bool IsIncomingMessage(object socialMessage) (line 824)
- private string ReadLocalizeText(MonoBehaviour tile, FieldInfo localizeField) (line 835)
- private GameObject GetChatInputField() (line 850)
- private GameObject GetSendButton() (line 861)
- private void EnsureReflectionCached(Type socialUIType) (line 876)
