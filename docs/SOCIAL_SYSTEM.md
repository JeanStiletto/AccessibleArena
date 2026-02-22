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

---

# Challenge Screen (Direct Challenge / Friend Challenge)

Accessible navigation for MTGA's challenge screen. Provides flat navigation of spinners, buttons, and player status.

**Trigger:** Challenge action on a friend tile, or "Challenge" button in social panel
**Close:** Backspace from main level, or Leave button

---

## Navigation Structure

Two-level navigation using the element grouping system:

### Level 1: ChallengeMain (flat list)

All spinners + buttons on the main challenge screen:
- **Mode spinner** (always present) - e.g. "Pioneer-Turnier-Match"
- **Additional spinners** (mode-dependent) - Deck Type, Format, Coin Flip (appear for some modes like "Herausforderungs-Match")
- **Select Deck** button
- **Leave** button (`MainButton_Leave`)
- **Invite** button (in enemy player card, when no opponent invited)
- **Status** button (`UnifiedChallenge_MainButton`) - shows ready/waiting/invalid deck status, prefixed with local player name

### Level 2: Deck Selection (folder-based)

Reuses PlayBladeFolders infrastructure:
- Folder toggles (Meine Decks, Starterdecks, Brawl-Beispieldecks)
- Deck entries within folders
- NewDeck and EditDeck buttons (added as extra elements in folder group)

### Invite Popup

Handled by existing Popup overlay detection (PopupBase). Contains text input, dropdown, friend checkboxes.

---

## Key Behaviors

### Spinner Changes
- Left/Right arrows invoke OnNextValue/OnPreviousValue on spinner
- Game auto-opens DeckSelectBlade on spinner change (game behavior, not mod)
- Mod closes DeckSelectBlade via `Hide()` reflection call after spinner change
- Mod preserves position via `RequestChallengeMainEntryAtIndex()`

### Deck Selection Flow
- Enter on Select Deck -> DeckSelectBlade opens -> folders/decks appear
- Enter on deck -> deck selected -> auto-return to ChallengeMain
- Backspace from folders -> return to ChallengeMain

### Player Status Announcement
- On entering challenge: "Direkte Herausforderung. Du: PlayerName, Status. Gegner: Not invited/PlayerName"
- Local player name extracted from `UnifiedChallengeDisplay._localPlayerDisplay._playerName` (TMP_Text, stripped of rich text tags)
- Main button label enhanced with player name prefix (e.g. "jean stiletto: Ungültiges Deck")

---

## Known Issue: Button Deactivation on DeckSelectBlade

### Problem
When DeckSelectBlade opens (either via Select Deck or auto-opened by spinner change), the game deactivates `MainButton_Leave` and `Invite Button` by setting their parent containers inactive. `FindObjectsOfType<CustomButton>()` only finds active objects, so these buttons disappear from our element list.

### Root Cause (Confirmed via Decompilation)
`PlayBladeController` has two methods that work as a pair:
```
ShowDeckSelector() {
    DeckSelector.Show(...)                              // opens blade
    _unifiedChallengeDisplay.gameObject.SetActive(false) // hides ENTIRE challenge display
}
HideDeckSelector() {
    DeckSelector.Hide()                                  // closes blade
    _unifiedChallengeDisplay.gameObject.SetActive(true)  // restores challenge display
}
```
The `_unifiedChallengeDisplay` GameObject is the parent of `MainButton_Leave` and `Invite Button`. When it's deactivated, all children become `!activeInHierarchy` and invisible to `FindObjectsOfType`.

Our mod was calling `DeckSelectBlade.Hide()` directly, which only does the first half. The `_unifiedChallengeDisplay` was never reactivated, so Leave and Invite stayed invisible.

**Spinner change flow in game code:**
1. `OnChallengeTypeChanged` / `OnDeckTypeChanged` calls `RefreshDeckSelector(allowRefresh: true)`
2. `RefreshDeckSelector` calls `_playBlade.ShowDeckSelector(...)` which opens blade AND hides challenge display
3. Sighted users see the deck selector overlay (challenge display hidden behind it)
4. When done, `HideDeckSelector()` is called, which closes blade AND restores challenge display

### Fix
Call `PlayBladeController.HideDeckSelector()` instead of `DeckSelectBlade.Hide()` directly. This ensures both the blade closure and the challenge display reactivation happen together.

### Evidence from Logs
- Initial scan: 30 CustomButtons -> 5 ChallengeMain elements (including Leave + Invite)
- After DeckSelectBlade opens: 149 CustomButtons -> Leave and Invite not found (parent deactivated)
- After our `DeckSelectBlade.Hide()`: 29 CustomButtons -> Leave and Invite still missing (parent still inactive)
- Using `HideDeckSelector()` would find 30+ CustomButtons again with Leave and Invite restored

---

## Unity Hierarchy (Runtime)

```
ContentController - Popout_Play_Desktop_16x9(Clone)
  Popout
    BladeView_CONTAINER
      FriendChallengeBladeWidget
        VerticalLayoutGroup
          ChallengeOptions
            Backer
              Content
                Popout_ModeMasterParameter     <- Spinner (mode)
                [additional spinners per mode]
        ContextDisplay
          NoDeck                               <- Select Deck button
          DeckDisplay                          <- shows selected deck
        UnifiedChallengesCONTAINER
          Menu
            MainButtons
              MainButton_Leave                 <- Leave button
          EnemyCard_Challenges
            No Player
              Invite Button                    <- Invite button
          UnifiedChallenge_MainButton          <- Status/Ready button
```

---

## Reflection Details

### UnifiedChallengeDisplay (no namespace)
- `_localPlayerDisplay` (ChallengePlayerDisplay) - local player info
- `_enemyPlayerDisplay` (ChallengePlayerDisplay) - opponent info

### Wizards.Mtga.PrivateGame.ChallengePlayerDisplay
- `_playerName` (TMP_Text) - player display name (with rich text color tags)
- `_playerStatus` (Localize) - status text component
- `_noPlayer` (GameObject) - shown when no opponent
- `_playerInvited` (GameObject) - shown when opponent invited but not joined
- `PlayerId` (string property) - player identifier

### DeckSelectBlade
- `Show(EventContext, DeckFormat, Action, Boolean)` - opens deck selection, stores onHide callback
- `Hide()` - closes blade, calls `SetDeckBoxSelected(false)`, invokes `_onHideCallback`
- `IsShowing` (property) - blade visibility state
- WARNING: Call `PlayBladeController.HideDeckSelector()` instead of `DeckSelectBlade.Hide()` directly - see Button Deactivation section

### PlayBladeController
- `ShowDeckSelector(EventContext, DeckFormat, Action, bool)` - opens blade + deactivates `_unifiedChallengeDisplay`
- `HideDeckSelector()` - closes blade + reactivates `_unifiedChallengeDisplay`
- `OnSelectDeckClicked(EventContext, DeckFormat, Action)` - toggles deck selector (show/hide)
- `DeckSelector` (public field) - reference to `DeckSelectBlade`
- `PlayBladeVisualState` - Hidden, Events, or Challenge
- `_unifiedChallengeDisplay` (private field) - the `UnifiedChallengeDisplay` component

### UnifiedChallengeBladeWidget (extends PlayBladeWidget)
- Manages spinners: `_challengeTypeSpinner`, `_deckTypeSpinner`, `_bestOfSpinner`, `_startingPlayerSpinner`
- `OnChallengeTypeChanged` / `OnDeckTypeChanged` -> `RefreshDeckSelector(true)` -> opens DeckSelectBlade
- `_settingsAnimator` controls UI layout (Expand, Tournament, Locked states)
- `UpdateButton()` changes main/secondary button text based on `DeckSelector.IsShowing` and challenge state

---

## Implementation Files

- **`ChallengeNavigationHelper.cs`** - Central helper: HandleEnter, HandleBackspace, OnChallengeOpened/Closed, HandleDeckSelected, player status, CloseDeckSelectBlade
- **`ElementGroupAssigner.cs`** - `IsChallengeContainer()` routes elements to ChallengeMain; NewDeck/EditDeck to PlayBladeFolders; InviteFriendPopup to Popup
- **`GroupedNavigator.cs`** - `_isChallengeContext`, `RequestChallengeMainEntry()`, folder extra elements support
- **`OverlayDetector.cs`** - Returns ChallengeMain overlay when PlayBladeState >= 2; `IsInsideChallengeScreen()` checks
- **`GeneralMenuNavigator.cs`** - Challenge helper integration, spinner rescan, label enhancement, player status in announcements
- **`Strings.cs`** + `lang/*.json` - ChallengeYou, ChallengeOpponent, ChallengeNotInvited, ChallengeInvited, GroupChallengeMain
