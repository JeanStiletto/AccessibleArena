# BaseNavigator.Chat.cs
Path: src/Core/Services/BaseNavigator/BaseNavigator.Chat.cs
Lines: 98

## Top-level comments
- Chat window opening (F4 key). Finds SocialUI, caches ShowChatWindow method via reflection, activates ChatNavigator, restores social UI state.

## public partial class BaseNavigator (line 20)
### Fields
- private static MethodInfo _showChatWindowMethod (line 24)
- private static bool _showChatWindowLookupDone (line 25)

### Methods
- protected void OpenChat() (line 31) — find SocialUI, cache method, restore UI, activate ChatNavigator
