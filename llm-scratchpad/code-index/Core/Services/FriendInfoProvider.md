# FriendInfoProvider.cs
Path: src/Core/Services/FriendInfoProvider.cs
Lines: 652

## Top-level comments
- Provides friend tile information for accessibility navigation. Reads display name, status, and available actions from social tile components (FriendTile, InviteOutgoingTile, InviteIncomingTile, BlockTile, IncomingChallengeRequestTile, CurrentChallengeTile) via reflection.

## private class TileReflectionCache (line 55, nested in FriendInfoProvider)
### Fields
- public FieldInfo LabelName (line 57) — Note: TMP_Text (all tile types)
- public FieldInfo LabelStatus (line 58) — Note: FriendTile: _labelStatus (Localize component, not TMP_Text)
- public FieldInfo LabelDateSent (line 59) — Note: InviteOutgoingTile: _labelDateSent (Localize component)
- public FieldInfo ChallengeEnabled (line 60)
- public FieldInfo ButtonRemoveFriend (line 61)
- public FieldInfo ButtonBlockFriend (line 62)
- public FieldInfo ButtonChallengeFriend (line 63)
- public FieldInfo ButtonCancel (line 64)
- public FieldInfo ButtonAccept (line 65)
- public FieldInfo ButtonReject (line 66)
- public FieldInfo ButtonBlock (line 67)
- public FieldInfo ButtonRemoveBlock (line 68)
- public FieldInfo ButtonAddFriend (line 69)
- public FieldInfo CallbackOpenChat (line 70)
- public FieldInfo CallbackRemoveBlock (line 71)
- public PropertyInfo FriendProp (line 72)
- public PropertyInfo InviteProp (line 73)
- public PropertyInfo BlockProp (line 74)
- public FieldInfo SenderName (line 77) — Note: IncomingChallengeRequestTile: _senderName (TMP_Text)
- public FieldInfo TitleText (line 78) — Note: CurrentChallengeTile: _titleText (Localize)
- public FieldInfo OpenChallengeButton (line 79) — Note: CurrentChallengeTile: _openChallengeScreenButton (CustomButton)

## public static class FriendInfoProvider (line 24)
### Fields
- private const string ActionChat = "chat" (line 27)
- private const string ActionChallenge = "challenge" (line 28)
- private const string ActionUnfriend = "unfriend" (line 29)
- private const string ActionBlock = "block" (line 30)
- private const string ActionRevoke = "revoke" (line 31)
- private const string ActionAccept = "accept" (line 32)
- private const string ActionDecline = "decline" (line 33)
- private const string ActionUnblock = "unblock" (line 34)
- private const string ActionAcceptChallenge = "acceptChallenge" (line 35)
- private const string ActionDeclineChallenge = "declineChallenge" (line 36)
- private const string ActionBlockChallenger = "blockChallenger" (line 37)
- private const string ActionAddFriendChallenger = "addFriendChallenger" (line 38)
- private const string ActionOpenChallenge = "openChallenge" (line 39)
- private static readonly string[] SocialTileTypeNames (line 42)
- private static readonly Dictionary<Type, TileReflectionCache> _caches (line 53)
### Methods
- public static string GetFriendLabel(GameObject element) (line 86) — Note: returns "name, status" or "name, date" for sent requests; special-cases IncomingChallengeRequestTile and CurrentChallengeTile
- public static List<(string label, string actionId)> GetFriendActions(GameObject element) (line 126) — Note: returns localized labels per tile type; chat only when online or has chat history
- public static bool ActivateFriendAction(GameObject element, string actionId) (line 207)
- public static (string fullName, string statusText) GetLocalPlayerInfo(GameObject socialPanel) (line 286) — Note: reads _socialManager.LocalPlayer.FullName and StatusText.text
- public static GameObject GetStatusButton(GameObject socialPanel) (line 342)
- public static Component FindFriendTile(GameObject element) (line 371) — Note: walks up parent chain looking for any SocialTileTypeNames component
- private static TileReflectionCache GetCache(Component tile) (line 393) — Note: keyed by Type to handle multiple tile types
- private static string ReadTMPField(Component tile, FieldInfo field) (line 429)
- private static string ReadLocalizeField(Component tile, FieldInfo field) (line 455) — Note: Localize sets text on sibling/child TMP_Text on the same GameObject
- private static bool ClickButton(Component tile, FieldInfo buttonField) (line 482)
- private static bool ClickCustomButton(Component tile, FieldInfo buttonField) (line 505) — Note: CustomButton.OnClick is a UnityEvent, invoked via reflection
- private static bool InvokeCallback(Component tile, FieldInfo callbackField) (line 539) — Note: reads Friend/Invite property to pass as single parameter; handles parameterless callbacks too
- private static object GetFriendEntity(Component tile, TileReflectionCache cache) (line 592)
- private static object GetInviteEntity(Component tile, TileReflectionCache cache) (line 602)
- private static bool TryInvokeMethod(Component tile, string actionId) (line 613) — Note: fallback using common button/callback naming patterns
