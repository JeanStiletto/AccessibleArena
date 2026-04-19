# ChatMessageWatcher.cs
Path: src/Core/Services/ChatMessageWatcher.cs
Lines: 317

## public class ChatMessageWatcher (line 19)

Polls ChatManager conversations for new incoming messages and announces them via AnnouncementService. Skips when ChatNavigator/DuelChatNavigator is active.

### Constants
- private const float PollInterval = 1.5f (line 21)

### Fields
- private readonly IAnnouncementService _announcer (line 23)
- private float _pollTimer (line 24)
- private object _chatManager (line 27)
- private bool _lookupFailed (line 28)
- private bool _lookupAttempted (line 29)
- private bool _loggedLookupFailure (line 30)
- private bool _reflectionInitialized (line 33)
- private FieldInfo _conversationsField (line 34)
- private FieldInfo _messageHistoryField (line 35)
- private FieldInfo _directionField (line 36)
- private FieldInfo _textBodyField (line 37)
- private FieldInfo _textTitleField (line 38)
- private FieldInfo _messageTypeField (line 39)
- private PropertyInfo _friendProp (line 40)
- private PropertyInfo _displayNameProp (line 41)
- private object _directionIncoming (line 42)
- private object _messageTypeChat (line 43)
- private readonly Dictionary<object, int> _knownMessageCounts (line 46)
- private bool _hasCompletedFirstPoll (line 47)
- private bool _loggedFirstPoll (line 48)

### Methods
- public ChatMessageWatcher(IAnnouncementService announcer) (line 50)
- public void Update() (line 55)
- public void OnSceneChanged() (line 81)
- private object GetChatManager() (line 92) — walks SocialUI_V2_Desktop_16x9(Clone) -> SocialUI._socialManager -> ChatManager
- private void EnsureReflectionCached(Type socialUIType) (line 135) — caches Conversation/ChatManager/SocialMessage/Direction/MessageType fields
- private void PollConversations(object chatManager) (line 181)
- private bool IsIncomingChatMessage(object message) (line 264)
- private void AnnounceMessage(object message, object conversation) (line 291) — announces with Strings.ChatMessageIncoming at High priority
