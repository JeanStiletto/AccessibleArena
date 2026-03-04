# FriendInfoProvider.cs

Provides friend tile information for accessibility navigation. Reads display name, status, and available actions from social tile components (FriendTile, InviteOutgoingTile, InviteIncomingTile, BlockTile).

## static class FriendInfoProvider (line 22)

### Constants - Action Identifiers (line 24)
- ActionChat = "chat" (line 25)
- ActionChallenge = "challenge" (line 26)
- ActionUnfriend = "unfriend" (line 27)
- ActionBlock = "block" (line 28)
- ActionRevoke = "revoke" (line 29)
- ActionAccept = "accept" (line 30)
- ActionDecline = "decline" (line 31)
- ActionUnblock = "unblock" (line 32)

### Constants - Tile Types (line 34)
- SocialTileTypeNames (string[]) (line 35)

### Private Fields - Cache (line 43)
- _caches (Dictionary<Type, TileReflectionCache>) (line 44)

### Private Class - TileReflectionCache (line 46)
- LabelName (FieldInfo) (line 48)
- LabelStatus (FieldInfo) (line 49) - Note: Localize component, not TMP_Text
- LabelDateSent (FieldInfo) (line 50) - Note: Localize component
- ChallengeEnabled (FieldInfo) (line 51)
- ButtonRemoveFriend (FieldInfo) (line 52)
- ButtonBlockFriend (FieldInfo) (line 53)
- ButtonChallengeFriend (FieldInfo) (line 54)
- ButtonCancel (FieldInfo) (line 55)
- ButtonAccept (FieldInfo) (line 56)
- ButtonReject (FieldInfo) (line 57)
- ButtonBlock (FieldInfo) (line 58)
- ButtonRemoveBlock (FieldInfo) (line 59)
- CallbackOpenChat (FieldInfo) (line 60)
- CallbackRemoveBlock (FieldInfo) (line 61)
- FriendProp (PropertyInfo) (line 62)
- InviteProp (PropertyInfo) (line 63)
- BlockProp (PropertyInfo) (line 64)

### Public Methods
- GetFriendLabel(GameObject) → string (line 71) - Note: returns "name, status" or "name, date"
- GetFriendActions(GameObject) → List<(string, string)> (line 96) - Note: returns (localized label, action ID) pairs
- ActivateFriendAction(GameObject, string) → bool (line 166)
- GetLocalPlayerInfo(GameObject) → (string, string) (line 230) - Note: returns (fullName, statusText)
- GetStatusButton(GameObject) → GameObject (line 286)
- FindFriendTile(GameObject) → Component (line 315)

### Private Methods
- GetCache(Component) → TileReflectionCache (line 337)
- ReadTMPField(Component, FieldInfo) → string (line 369)
- ReadLocalizeField(Component, FieldInfo) → string (line 395) - Note: reads TMP_Text from Localize component's GameObject
- ClickButton(Component, FieldInfo) → bool (line 422)
- InvokeCallback(Component, FieldInfo) → bool (line 446) - Note: reads entity from Friend/Invite property
- GetFriendEntity(Component, TileReflectionCache) → object (line 499)
- GetInviteEntity(Component, TileReflectionCache) → object (line 509)
- TryInvokeMethod(Component, string) → bool (line 520) - Note: fallback for unmapped tile types
