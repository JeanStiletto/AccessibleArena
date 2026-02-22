# Social System (Friends Panel)

Accessible navigation for MTGA's social/friends panel. Provides hierarchical group navigation with per-friend action sub-navigation via screen reader.

**Trigger:** F4 key (toggle) or NavBar social button
**Close:** Backspace

---

## Navigation Structure

The friends panel uses the element grouping system with overlay filtering. When open, all non-social elements are hidden.

### Groups (Tab / Up/Down at group level)

- **Challenge** - Standalone button to create a challenge
- **Add Friend** - Standalone button to send a friend request
- **Friends** - List of accepted friends (navigable section)
- **Sent Requests** - List of outgoing friend invites (navigable section)
- **Incoming Requests** - List of incoming friend invites (navigable section)
- **Blocked** - List of blocked users (navigable section)

Only sections with entries appear. Empty sections are absent.

### Within a Friend Section

- **Up/Down:** Navigate between friend entries
- **Left/Right:** Cycle available actions for the current friend
- **Enter:** Activate the currently selected action
- **Backspace:** Exit section, return to group level

### Announcements

- Entering a section: "1 of 3. wuternst, Online"
- Cycling actions: "Chat, 1 of 4" / "Challenge, 2 of 4"
- Activating: action is executed (button click or callback invocation)

---

## Available Actions Per Tile Type

### FriendTile (accepted friends)
- **Chat** - Opens chat window (available when friend is online or has chat history)
- **Challenge** - Sends a game challenge (available when challenge is enabled)
- **Unfriend** - Removes friend (always available)
- **Block** - Blocks user (always available)

### InviteOutgoingTile (sent requests)
- **Revoke** - Cancels the outgoing request

### InviteIncomingTile (incoming requests)
- **Accept** - Accepts friend request
- **Decline** - Declines friend request
- **Block** - Blocks the requesting user

### BlockTile (blocked users)
- **Unblock** - Removes the block

---

## Friend Entry Labels

Each friend entry displays: **"name, status"**

- FriendTile: name from `_labelName` (TMP_Text) + status from `_labelStatus` (Localize component)
- InviteOutgoingTile: name from `_labelName` (TMP_Text) + date from `_labelDateSent` (Localize component)

The `Localize` component is not a TMP_Text. To read its displayed text, find the TMP_Text on the same GameObject or its children.

---

## Unity Hierarchy (Runtime)

```
SocialUI_V2_Desktop_16x9(Clone)
  MobileSafeArea
    FriendsWidget_*
      Button_TopBarDismiss
      Button_AddChallenge
        Backer_Hitbox          ← navigable element (Challenge group)
      Button_AddFriend
        Backer_Hitbox          ← navigable element (Add Friend group)
      Bucket_Friends_CONTAINER
        SocialEntittiesListItem_0    ← note: double-t typo in game code
          [FriendTile component]
          Backer_Hitbox        ← navigable element (Friends section)
        SocialEntittiesListItem_1
          ...
      Bucket_SentRequests_CONTAINER
        SocialEntittiesListItem_0
          [InviteOutgoingTile component]
          Backer_Hitbox        ← navigable element (Sent Requests section)
      Bucket_IncomingRequests_CONTAINER
        ...
      Bucket_Blocked_CONTAINER
        ...
```

Navigable elements are `Backer_Hitbox` (CustomButton) children. The tile component (`FriendTile`, `InviteOutgoingTile`, etc.) is on the parent `SocialEntittiesListItem_*` GameObject.

---

## Technical Implementation

### Files

- **`ElementGroup.cs`** - 6 enum values: `FriendsPanelChallenge`, `FriendsPanelAddFriend`, `FriendSectionFriends`, `FriendSectionIncoming`, `FriendSectionOutgoing`, `FriendSectionBlocked`
- **`ElementGroupAssigner.cs`** - `DetermineFriendPanelGroup()` maps elements to groups via parentPath bucket detection
- **`GroupedNavigator.cs`** - Friend section groups exempt from single-element standalone rule
- **`OverlayDetector.cs`** - `FriendsPanel` overlay when social panel is open
- **`FriendInfoProvider.cs`** - Reads tile data and actions via reflection on Core.dll types
- **`GeneralMenuNavigator.cs`** - Friend sub-navigation (Left/Right actions, F4 toggle, panel open/close)
- **`MenuScreenDetector.cs`** - `IsSocialPanelOpen()` detects active friends widget
- **`Strings.cs`** - Localized group names and action labels (14 keys total)

### Element Group Assignment

Elements inside the social panel are assigned groups based on parentPath patterns:

- `Button_AddChallenge` in path → `FriendsPanelChallenge`
- `Button_AddFriend` in path → `FriendsPanelAddFriend`
- `SocialEntittiesListItem` + `Bucket_Friends` → `FriendSectionFriends`
- `SocialEntittiesListItem` + `Bucket_SentRequests` → `FriendSectionOutgoing`
- `SocialEntittiesListItem` + `Bucket_IncomingRequests` → `FriendSectionIncoming`
- `SocialEntittiesListItem` + `Bucket_Blocked` → `FriendSectionBlocked`

Unmatched social panel elements return `Unknown` (hidden via fallthrough guard).

### Reflection Details

Social tile types live in **Core.dll** (no namespace), NOT Assembly-CSharp.dll.

**FriendTile fields:**
- `_labelName` (TMP_Text) - friend display name
- `_labelStatus` (Localize) - status text (Online/Offline/Away/Busy)
- `_challengeEnabled` (bool) - whether challenge button is active
- `_buttonRemoveFriend` (Button) - unfriend action
- `_buttonBlockFriend` (Button) - block action
- `_buttonChallengeFriend` (Button) - challenge action
- `Callback_OpenChat` (Action\<SocialEntity\>) - chat callback, needs Friend as parameter
- `Friend` (SocialEntity property) - the friend entity with `IsOnline`, `HasChatHistory`, `DisplayName`

**InviteOutgoingTile fields:**
- `_labelName` (TMP_Text) - invitee display name
- `_labelDateSent` (Localize) - date sent text
- `_buttonCancel` (Button) - revoke/cancel action
- `Callback_Reject` (Action\<Invite\>) - reject callback, needs Invite as parameter
- `Invite` (Invite property) - the invite entity

**InviteIncomingTile fields:**
- `_labelName` (TMP_Text) - requester display name
- `_contextClickButton` (CustomButton) - the main clickable element
- `_buttonAccept` (Button) - accept friend request
- `_buttonReject` (Button) - decline friend request
- `_buttonBlock` (Button) - block the requester
- `Callback_Accept`, `Callback_Reject`, `Callback_Block` (Action\<Invite\>)
- `Invite` (Invite property) - the invite entity

**BlockTile fields:**
- `_labelName` (TMP_Text) - blocked user display name
- `_buttonRemoveBlock` (Button) - unblock action
- `Callback_RemoveBlock` (Action\<Block\>) - unblock callback
- `Block` (Block property) - the block entity with `BlockedPlayer.DisplayName`

### Virtualized Scroll View

The `FriendsWidget` uses a **virtualized scroll view** for performance:
- Tiles are only instantiated for entries within the visible viewport
- `SectionBlocks.IsOpen = false` by default (collapsed)
- The mod force-creates BlockTile instances via reflection on FriendsWidget
- BlockTile has NO CustomButton/Backer_Hitbox - discovered via fallback tile scan

### Panel Toggle

Open: `SocialUI.ShowSocialEntitiesList()` via reflection
Close: `SocialUI.CloseFriendsWidget()` via reflection
Detection: `GameObject.Find("SocialUI_V2_Desktop_16x9(Clone)")` + active `FriendsWidget_*` child

### Important Notes

- `SocialEntittiesListItem` has a double-t typo in the game code - match both spellings
- Button onClick handlers internally pass the entity parameter to callbacks, so `ClickButton` works for unfriend/block/revoke
- For Chat, the callback must be invoked directly with the `Friend` entity as parameter (not wired to a button)
- `_challengeEnabled` controls challenge availability; `Friend.IsOnline` / `Friend.HasChatHistory` controls chat availability
- The `Localize` component type is from `Wotc.Mtga.Loc` namespace
